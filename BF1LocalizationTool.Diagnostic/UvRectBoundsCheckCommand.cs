// =============================================================================
// BF1LocalizationTool.Diagnostic — UvRectBoundsCheckCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: GlyphAtlasPatcher писатиме пікселі за адресою
//     (row*texW + col)*2 у масив BODY конкретної сторінки — якщо
//     x0/x1/y0/y1 хоч одного донора вийде за межі texW/texH через
//     похибку округлення round(U×texW), запис вилізе за масив.
//
//     Перевіряється КОЖЕН FBOD-запис (не лише безпечні донори — про
//     всяк випадок, щоб знати реальні межі формату), З УРАХУВАННЯМ
//     FontGlyphRecord.PageIndex (розділ 4.1 специфікації) — сторінка
//     для перевірки MinX/MaxX/MinY/MaxY береться саме та, якій гліф
//     фізично належить, а не перша-ліпша.
// EN: GlyphAtlasPatcher will write pixels at address
//     (row*texW + col)*2 into a specific page's BODY array — if any
//     donor's x0/x1/y0/y1 exceeds texW/texH due to round(U×texW)
//     rounding error, the write goes out of array bounds.
//
//     EVERY FBOD record is checked (not just safe donors — just in case,
//     to know the format's real limits), RESPECTING
//     FontGlyphRecord.PageIndex (spec section 4.1) — the page used for
//     the MinX/MaxX/MinY/MaxY bounds check is the one the glyph actually
//     belongs to, not an arbitrary one.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class UvRectBoundsCheckCommand
{
    public static void Run(DiagnosticReport report, UcfbChunk root, string label)
    {
        var fonts = FontChunkLocator.FindAll(root);

        var totalChecked = 0;
        var pageIndexOutOfRange = new List<string>();
        var boundsViolations = new List<string>();

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var glyphs = FontGlyphTable.Parse(fbod.RawData);

            // UA: Групуємо за PageIndex КОЖНОГО запису — так само, як
            //     PerFontSafeDonorCommand (єдине правильне джерело
            //     сторінки для перевірки цього гліфа).
            // EN: Group by EACH record's PageIndex — same as
            //     PerFontSafeDonorCommand (the only correct source of
            //     which page to check this glyph against).
            foreach (var pageGroup in glyphs.GroupBy(g => g.PageIndex))
            {
                if (pageGroup.Key >= font.TexturePages.Count)
                {
                    foreach (var g in pageGroup)
                        pageIndexOutOfRange.Add(
                            $"{font.BaseName} code=0x{g.Code:X2}: PageIndex={pageGroup.Key}, " +
                            $"а сторінок лише {font.TexturePages.Count}");
                    continue;
                }

                var page = font.TexturePages[pageGroup.Key];
                var texPixels = FontTexturePixelReader.ReadMip0(page.Chunk);

                foreach (var glyph in pageGroup)
                {
                    totalChecked++;

                    var x0 = (int)Math.Round(glyph.U0 * texPixels.Width);
                    var x1 = (int)Math.Round(glyph.U1 * texPixels.Width);
                    var y0 = (int)Math.Round(Math.Min(glyph.V0, glyph.V1) * texPixels.Height);
                    var y1 = (int)Math.Round(Math.Max(glyph.V0, glyph.V1) * texPixels.Height);

                    var minX = Math.Min(x0, x1);
                    var maxX = Math.Max(x0, x1);

                    if (minX < 0 || maxX > texPixels.Width || y0 < 0 || y1 > texPixels.Height)
                        boundsViolations.Add(
                            $"{font.BaseName}/{page.Name} code=0x{glyph.Code:X2}: " +
                            $"x=[{minX}..{maxX}) y=[{y0}..{y1}), текстура {texPixels.Width}x{texPixels.Height}");
                }
            }
        }

        report.Log($"=== [{label}] Межі UV-прямокутника відносно текстури сторінки (перевірка перед GlyphAtlasPatcher) ===");
        report.Log($"    Перевірено гліф-записів (з урахуванням PageIndex): {totalChecked}");
        report.Log($"    Аномалій PageIndex поза межами наявних сторінок: {pageIndexOutOfRange.Count}");
        report.Log($"    Виходів прямокутника за межі текстури: {boundsViolations.Count}");

        if (pageIndexOutOfRange.Count > 0)
        {
            report.Log("    --- Аномалії PageIndex (до 10) ---");
            foreach (var m in pageIndexOutOfRange.Take(10))
                report.Log($"      {m}");
        }

        if (boundsViolations.Count > 0)
        {
            report.Log("    --- Виходи за межі (до 10) ---");
            foreach (var m in boundsViolations.Take(10))
                report.Log($"      {m}");
        }

        report.Log(boundsViolations.Count == 0 && pageIndexOutOfRange.Count == 0
            ? "    UA: ПІДТВЕРДЖЕНО — жоден UV-прямокутник не виходить за межі своєї текстурної сторінки. " +
              "GlyphAtlasPatcher може довіряти round(U×texW)/round(V×texH) без додаткового clamp."
            : "    UA: ЗНАЙДЕНО ПОРУШЕННЯ — перед написанням GlyphAtlasPatcher з'ясувати причину (можливо, потрібен " +
              "явний clamp/захист меж при записі пікселів).");
    }
}
