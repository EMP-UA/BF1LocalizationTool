// =============================================================================
// BF1LocalizationTool.Diagnostic — DonorSlotGrowthPotentialCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перевіряє, чи можна розширити UV-прямокутник ОДНОГО донора у
//     ДІЙСНО ВІЛЬНИЙ простір текстури (не займаючи жодного сусіднього
//     гліфа), не змінюючи розмір самої текстурної сторінки. Це можливо
//     завдяки UcfbWriter.cs — заміна чанку МОЖЕ бути іншого розміру за
//     оригінал (payloadData = replacement, довжина оновлюється сама).
//     Розширення самої сторінки (128→256 і т.д.) НЕ розглядається — це
//     змінило б офсет (row*texW+col)*2 для АБСОЛЮТНО ВСІХ гліфів
//     сторінки, це вже не точкова правка.
//
//     Будує карту зайнятості пікселів (bool[,]) з УСІХ реальних
//     прямокутників сторінки (не лише донорів — жодного гліфа, що
//     насправді використовується, зачіпати не можна), потім для КОЖНОГО
//     донора-кандидата рахує, скільки вільних пікселів є ліворуч,
//     праворуч, зверху, знизу — до першого зайнятого пікселя АБО межі
//     текстури. Це геометричний факт, обчислений напряму з реальних
//     прямокутників усіх гліфів — не оцінка, не здогад.
// EN: Checks whether a single donor's UV rectangle can be enlarged into
//     GENUINELY FREE texture space (without touching any neighbor glyph),
//     without changing the texture page's own size. This is possible
//     thanks to UcfbWriter.cs — a chunk replacement CAN be a different
//     size than the original (payloadData = replacement, length updates
//     itself). Enlarging the page itself (128→256 etc.) is NOT
//     considered — that would change the (row*texW+col)*2 offset for
//     ABSOLUTELY EVERY glyph on the page, which is no longer a targeted
//     edit.
//
//     Builds a pixel occupancy map (bool[,]) from ALL real rectangles on
//     the page (not just donors — no glyph that's actually in use may be
//     touched), then for EVERY candidate donor counts how many free
//     pixels exist left, right, up, down — up to the first occupied
//     pixel OR the texture boundary. This is a geometric fact computed
//     directly from real glyph rectangles — not an estimate, not a guess.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class DonorSlotGrowthPotentialCommand
{
    private readonly record struct GlyphRect(ushort Code, int MinX, int MaxX, int MinY, int MaxY);
    private readonly record struct GrowthResult(ushort Code, int Width, int Height, int FreeLeft, int FreeRight, int FreeUp, int FreeDown);

    public static async Task Run(DiagnosticReport report, UcfbChunk root, string filePath, string label, int neededGlyphCodes)
    {
        var summary = await SoftDonorAnalysis.BuildSummaryAsync(filePath);

        // UA: ДРУКОВНІ ASCII-символи (0x20-0x7E) завжди вважаються
        //     "зайнятими", незалежно від результату Locl-сканування:
        //     деякі рядки гри (напр. "Exit to Windows", де мала 'w'
        //     показується напряму) використовують друковні ASCII-коди
        //     поза таблицею Locl, тож саме лише Locl-сканування не
        //     доводить, що такий код вільний. Лише недруковні керівні
        //     коди (0x00-0x1F, 0x7F) лишаються кандидатами на основі
        //     даних реального сканування.
        // EN: PRINTABLE ASCII (0x20-0x7E) is always considered "used",
        //     regardless of the Locl scan result: some game strings
        //     (e.g. "Exit to Windows", where the lowercase 'w' is shown
        //     directly) use printable ASCII codes outside the Locl
        //     table, so the Locl scan alone doesn't prove such a code is
        //     free. Only non-printable control codes (0x00-0x1F, 0x7F)
        //     remain candidates based on real scan data.
        bool IsPrintableAscii(ushort code) => code is >= 0x20 and <= 0x7E;
        var usedByAnyLanguage = summary.Languages.SelectMany(l => l.CodeCounts.Keys).ToHashSet();
        bool IsUsed(ushort code) => IsPrintableAscii(code) || usedByAnyLanguage.Contains(code);

        var englishUsed = summary.Languages
            .Where(l => l.Language.Contains("english", StringComparison.OrdinalIgnoreCase))
            .SelectMany(l => l.CodeCounts.Keys)
            .ToHashSet();

        var baseSafeCandidates = Enumerable.Range(0, 256).Where(x => !IsUsed((ushort)x)).ToHashSet();
        var needsSoftDonors = baseSafeCandidates.Count < neededGlyphCodes;
        var softCandidateCodes = needsSoftDonors
            ? SoftDonorAnalysis.ComputeSoftCandidates(summary).Select(c => c.Code).ToHashSet()
            : [];

        var fonts = FontChunkLocator.FindAll(root);

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var glyphs = FontGlyphTable.Parse(fbod.RawData);
            var results = new List<GrowthResult>();

            foreach (var pageGroup in glyphs.GroupBy(g => g.PageIndex))
            {
                if (pageGroup.Key >= font.TexturePages.Count) continue;

                var texPixels = FontTexturePixelReader.ReadMip0(font.TexturePages[pageGroup.Key].Chunk);
                var texWidth = texPixels.Width;
                var texHeight = texPixels.Height;

                // UA: УСІ реальні прямокутники цієї сторінки (не лише
                //     донори) — карта зайнятості будується з повної
                //     картини, інакше "вільний" простір міг би виявитись
                //     чужим гліфом.
                // EN: ALL real rectangles on this page (not just donors)
                //     — the occupancy map is built from the full picture,
                //     otherwise "free" space could turn out to belong to
                //     another glyph.
                var rects = pageGroup.Select(g =>
                {
                    var x0 = (int)Math.Round(g.U0 * texWidth);
                    var x1 = (int)Math.Round(g.U1 * texWidth);
                    var y0 = (int)Math.Round(g.V0 * texHeight);
                    var y1 = (int)Math.Round(g.V1 * texHeight);
                    return new GlyphRect(g.Code, Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1), Math.Max(y0, y1));
                }).ToList();

                var occupied = new bool[texWidth, texHeight];
                foreach (var r in rects)
                {
                    if (r.MaxX - r.MinX <= 0 || r.MaxY - r.MinY <= 0) continue; // UA: вироджені — не займають пікселів / EN: degenerate — occupy no pixels
                    for (var x = r.MinX; x < r.MaxX; x++)
                        for (var y = r.MinY; y < r.MaxY; y++)
                            occupied[x, y] = true;
                }

                bool ColumnFree(int x, int minY, int maxY)
                {
                    for (var y = minY; y < maxY; y++)
                        if (occupied[x, y]) return false;
                    return true;
                }

                bool RowFree(int y, int minX, int maxX)
                {
                    for (var x = minX; x < maxX; x++)
                        if (occupied[x, y]) return false;
                    return true;
                }

                // UA: Донори (базові+м'які), присутні саме на цій сторінці.
                // EN: Donors (base+soft) present specifically on this page.
                var donorsOnThisPage = rects.Where(r =>
                    (baseSafeCandidates.Contains(r.Code) || softCandidateCodes.Contains(r.Code)) &&
                    r.MaxX - r.MinX > 0 && r.MaxY - r.MinY > 0);

                foreach (var d in donorsOnThisPage)
                {
                    var freeLeft = 0;
                    while (d.MinX - freeLeft - 1 >= 0 && ColumnFree(d.MinX - freeLeft - 1, d.MinY, d.MaxY))
                        freeLeft++;

                    var freeRight = 0;
                    while (d.MaxX + freeRight < texWidth && ColumnFree(d.MaxX + freeRight, d.MinY, d.MaxY))
                        freeRight++;

                    var freeUp = 0;
                    while (d.MinY - freeUp - 1 >= 0 && RowFree(d.MinY - freeUp - 1, d.MinX, d.MaxX))
                        freeUp++;

                    var freeDown = 0;
                    while (d.MaxY + freeDown < texHeight && RowFree(d.MaxY + freeDown, d.MinX, d.MaxX))
                        freeDown++;

                    results.Add(new GrowthResult(d.Code, d.MaxX - d.MinX, d.MaxY - d.MinY, freeLeft, freeRight, freeUp, freeDown));
                }
            }

            var growable = results.Where(r => r.FreeLeft + r.FreeRight + r.FreeUp + r.FreeDown > 0).ToList();

            report.Log($"=== [{label}] {font.BaseName}: потенціал розширення донорів у вільний простір ===");
            report.Log($"    Донорів перевірено: {results.Count}, з можливістю рости хоч якось: {growable.Count}");

            if (growable.Count > 0)
            {
                report.Log("    Код   Поточний Ш×В  +Ліво +Право +Верх +Низ  Потенційний Ш×В");
                foreach (var r in growable.OrderByDescending(r => (r.FreeLeft + r.FreeRight) * (r.Width + r.FreeLeft + r.FreeRight))
                                          .ThenByDescending(r => r.FreeUp + r.FreeDown))
                {
                    var newWidth = r.Width + r.FreeLeft + r.FreeRight;
                    var newHeight = r.Height + r.FreeUp + r.FreeDown;
                    report.Log($"    0x{r.Code:X2}  {r.Width}x{r.Height,-10} {r.FreeLeft,-6}{r.FreeRight,-7}{r.FreeUp,-6}{r.FreeDown,-5} {newWidth}x{newHeight}");
                }
            }

            report.Log();
        }
    }
}
