// =============================================================================
// BF1LocalizationTool.Diagnostic — GrowthAwareDonorAssignmentPreviewCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Перевіряє, чи вистачить підтвердженого вільного простору
//     (DonorSlotGrowthPotentialCommand + GlyphOccupancyOverlayCommand —
//     обидва підтвердили порожність) на ВЕСЬ алфавіт, а не лише на
//     кілька "особливих" широких літер?
//
//     Поєднує два вже написані шматки:
//       1. Розрахунок потенціалу росту (той самий підхід, що й у
//          DonorSlotGrowthPotentialCommand) — для КОЖНОГО донора.
//       2. GlyphDonorMatcher.Assign — але тепер на РОЗШИРЕНИХ розмірах
//          (Width+FreeLeft+FreeRight, Height+FreeUp+FreeDown), а не на
//          поточних.
//
//     Плюс ОБОВ'ЯЗКОВА перевірка, якої немає в
//     DonorSlotGrowthPotentialCommand: там ріст рахувався для КОЖНОГО
//     донора окремо, у припущенні "тільки він один росте". Якщо
//     реально розширити ВСІ 66 призначених донорів одночасно, два
//     сусідні донори можуть претендувати на ТУ САМУ вільну ділянку.
//     Ця команда перевіряє САМЕ ЦЕ — чи перетинаються розширені
//     прямокутники серед ФАКТИЧНО ПРИЗНАЧЕНИХ 66 донорів.
// EN: Checks whether the confirmed free space
//     (DonorSlotGrowthPotentialCommand + GlyphOccupancyOverlayCommand —
//     both confirmed emptiness) enough for the WHOLE alphabet, not just
//     a handful of "special" wide letters?
//
//     Combines two already-written pieces:
//       1. Growth-potential calculation (the same approach as
//          DonorSlotGrowthPotentialCommand) — for EVERY donor.
//       2. GlyphDonorMatcher.Assign — but now on GROWN sizes
//          (Width+FreeLeft+FreeRight, Height+FreeUp+FreeDown), not
//          current ones.
//
//     Plus a MANDATORY check missing from
//     DonorSlotGrowthPotentialCommand: growth there was computed for
//     EACH donor independently, assuming "only this one grows". Actually
//     enlarging ALL 66 assigned donors at once means two neighboring
//     donors might claim the SAME free area. This command checks EXACTLY
//     that — whether the grown rectangles among the ACTUALLY ASSIGNED 66
//     donors overlap each other.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.FontGenerator.Matching;

namespace BF1LocalizationTool.Diagnostic;

public static class GrowthAwareDonorAssignmentPreviewCommand
{
    private readonly record struct GlyphRect(ushort Code, int MinX, int MaxX, int MinY, int MaxY);
    private readonly record struct GrownDonor(ushort Code, int MinX, int MaxX, int MinY, int MaxY);

    public static async Task Run(DiagnosticReport report, UcfbChunk root, string filePath, string label, string fontFamilyName, int neededGlyphCodes)
    {
        var summary = await SoftDonorAnalysis.BuildSummaryAsync(filePath);

        // UA: ВСТАНОВЛЕНО (реальний скріншот BF1 — "Exit to
        //     Windows" показало "WINDOГs": мала 'w', "не знайдена" в
        //     Locl, реально використовується поза ним). ДРУКОВНІ ASCII
        //     (0x20-0x7E) ЗАВЖДИ "зайняті" незалежно від Locl-сканування;
        //     лише недруковні керівні коди (0x00-0x1F, 0x7F) лишаються
        //     кандидатами через реальні дані.
        // EN: CONFIRMED (real BF1 screenshot — "Exit to Windows"
        //     showed "WINDOГs": lowercase 'w', "not found" in Locl, is
        //     actually used outside it). PRINTABLE ASCII (0x20-0x7E) is
        //     ALWAYS "used" regardless of the Locl scan; only non-printable
        //     control codes (0x00-0x1F, 0x7F) remain real-data candidates.
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
            var unsafeBase = new HashSet<int>();
            var unsafeSoft = new HashSet<int>();
            var zeroArea = new HashSet<int>();
            var geometryByCode = new Dictionary<ushort, (int Width, int Height)>();
            var grownRectByCode = new Dictionary<ushort, GrownDonor>();

            foreach (var pageGroup in glyphs.GroupBy(g => g.PageIndex))
            {
                if (pageGroup.Key >= font.TexturePages.Count) continue;

                var texPixels = FontTexturePixelReader.ReadMip0(font.TexturePages[pageGroup.Key].Chunk);
                var texWidth = texPixels.Width;
                var texHeight = texPixels.Height;

                var rects = pageGroup.Select(g =>
                {
                    var x0 = (int)Math.Round(g.U0 * texWidth);
                    var x1 = (int)Math.Round(g.U1 * texWidth);
                    var y0 = (int)Math.Round(g.V0 * texHeight);
                    var y1 = (int)Math.Round(g.V1 * texHeight);
                    return new GlyphRect(g.Code, Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1), Math.Max(y0, y1));
                }).ToList();

                foreach (var r in rects)
                {
                    geometryByCode[r.Code] = (r.MaxX - r.MinX, r.MaxY - r.MinY);
                    if (r.MaxX - r.MinX <= 0 || r.MaxY - r.MinY <= 0)
                        zeroArea.Add(r.Code);
                }

                for (var i = 0; i < rects.Count; i++)
                {
                    for (var j = i + 1; j < rects.Count; j++)
                    {
                        var a = rects[i];
                        var b = rects[j];
                        var overlapsX = a.MinX < b.MaxX && b.MinX < a.MaxX;
                        var overlapsY = a.MinY < b.MaxY && b.MinY < a.MaxY;
                        if (!(overlapsX && overlapsY)) continue;

                        var aUsed = IsUsed(a.Code);
                        var bUsed = IsUsed(b.Code);
                        if (aUsed != bUsed)
                            unsafeBase.Add(aUsed ? b.Code : a.Code);

                        if (softCandidateCodes.Contains(a.Code) && englishUsed.Contains(b.Code))
                            unsafeSoft.Add(a.Code);
                        if (softCandidateCodes.Contains(b.Code) && englishUsed.Contains(a.Code))
                            unsafeSoft.Add(b.Code);
                    }
                }

                // UA: Карта зайнятості з УСІХ реальних прямокутників цієї
                //     сторінки — та сама логіка, що й
                //     DonorSlotGrowthPotentialCommand.
                // EN: Occupancy map from ALL real rectangles on this page
                //     — same logic as DonorSlotGrowthPotentialCommand.
                var occupied = new bool[texWidth, texHeight];
                foreach (var r in rects)
                {
                    if (r.MaxX - r.MinX <= 0 || r.MaxY - r.MinY <= 0) continue;
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

                foreach (var r in rects)
                {
                    if (r.MaxX - r.MinX <= 0 || r.MaxY - r.MinY <= 0) continue;

                    var freeLeft = 0;
                    while (r.MinX - freeLeft - 1 >= 0 && ColumnFree(r.MinX - freeLeft - 1, r.MinY, r.MaxY))
                        freeLeft++;

                    var freeRight = 0;
                    while (r.MaxX + freeRight < texWidth && ColumnFree(r.MaxX + freeRight, r.MinY, r.MaxY))
                        freeRight++;

                    var freeUp = 0;
                    while (r.MinY - freeUp - 1 >= 0 && RowFree(r.MinY - freeUp - 1, r.MinX, r.MaxX))
                        freeUp++;

                    var freeDown = 0;
                    while (r.MaxY + freeDown < texHeight && RowFree(r.MaxY + freeDown, r.MinX, r.MaxX))
                        freeDown++;

                    grownRectByCode[r.Code] = new GrownDonor(
                        r.Code, r.MinX - freeLeft, r.MaxX + freeRight, r.MinY - freeUp, r.MaxY + freeDown);
                }
            }

            var donors = new List<DonorSlot>();
            foreach (var code in baseSafeCandidates.Except(unsafeBase).Except(zeroArea))
                if (grownRectByCode.TryGetValue((ushort)code, out var grown))
                    donors.Add(new DonorSlot((ushort)code, grown.MaxX - grown.MinX, grown.MaxY - grown.MinY));

            if (needsSoftDonors)
                foreach (var code in softCandidateCodes.Except(unsafeSoft).Except(zeroArea))
                    if (grownRectByCode.TryGetValue((ushort)code, out var grown))
                        donors.Add(new DonorSlot((ushort)code, grown.MaxX - grown.MinX, grown.MaxY - grown.MinY));

            var allFontGlyphs = geometryByCode.Select(kv => new KnownGlyphGeometry(kv.Key, kv.Value.Width, kv.Value.Height)).ToList();
            var referenceCapHeight = GlyphDonorMatcher.GetReferenceCapHeight(allFontGlyphs);
            donors = GlyphDonorMatcher.FilterOutTooSmall(donors, referenceCapHeight);

            report.Log($"=== [{label}] {font.BaseName}: призначення НА РОЗШИРЕНИХ розмірах (потенціал росту враховано) ===");

            if (donors.Count < CyrillicAlphabet.AllLetters.Count)
            {
                report.Log($"    UA: Донорів замало навіть із розширенням ({donors.Count} < {CyrillicAlphabet.AllLetters.Count}).");
                report.Log();
                continue;
            }

            var assignments = GlyphDonorMatcher.Assign(CyrillicAlphabet.AllLetters, donors, fontFamilyName, referenceCapHeight);

            var avgAbsDeviation = assignments.Average(a => Math.Abs(Math.Log(a.DonorAspectRatio) - Math.Log(a.TargetAspectRatio)));
            report.Log($"    Середнє логарифмічне відхилення пропорції (НА РОЗШИРЕНИХ розмірах): {avgAbsDeviation:F3}");

            // UA: КРИТИЧНА перевірка — чи перетинаються розширені
            //     прямокутники серед ФАКТИЧНО ПРИЗНАЧЕНИХ 66 донорів
            //     (ріст рахувався незалежно для кожного, тут перевіряємо,
            //     чи не claim'ять вони одну й ту саму вільну ділянку).
            // EN: CRITICAL check — whether grown rectangles among the
            //     ACTUALLY ASSIGNED 66 donors overlap (growth was
            //     computed independently for each; here it's checked whether
            //     they claim the same free area).
            var assignedGrown = assignments
                .Select(a => grownRectByCode[a.DonorCode])
                .ToList();

            var conflicts = new List<string>();
            for (var i = 0; i < assignedGrown.Count; i++)
            {
                for (var j = i + 1; j < assignedGrown.Count; j++)
                {
                    var a = assignedGrown[i];
                    var b = assignedGrown[j];
                    var overlapsX = a.MinX < b.MaxX && b.MinX < a.MaxX;
                    var overlapsY = a.MinY < b.MaxY && b.MinY < a.MaxY;
                    if (overlapsX && overlapsY)
                        conflicts.Add($"0x{a.Code:X2} <-> 0x{b.Code:X2}");
                }
            }

            report.Log($"    Конфліктів розширення (два ПРИЗНАЧЕНИХ донори claim'ять ту саму ділянку): {conflicts.Count}");
            if (conflicts.Count > 0)
            {
                report.Log("    UA: КОНФЛІКТИ (до 15):");
                foreach (var c in conflicts.Take(15))
                    report.Log($"      {c}");
            }

            report.Log();
        }
    }
}
