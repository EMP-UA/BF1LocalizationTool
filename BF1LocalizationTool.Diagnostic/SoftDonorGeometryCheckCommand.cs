// =============================================================================
// BF1LocalizationTool.Diagnostic — SoftDonorGeometryCheckCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Продовження перевірок перед GlyphAtlasPatcher — специфічно для
//     BF2, де базових безпечних донорів (не використаних ЖОДНОЮ мовою)
//     не вистачає на повний кириличний алфавіт (60 із 66 потрібних,
//     PerFontSafeDonorCommand). "М'які" кандидати
//     (SoftDonorAnalysis.ComputeSoftCandidates) — коди, використовувані
//     ЯКОЮСЬ НЕ-англійською мовою, але НІКОЛИ англійською — уже
//     обчислюються й друкуються в "Мовний аналіз", АЛЕ той список
//     враховує ЛИШЕ мовну ознаку, зовсім не геометрію атласу.
//
//     Ця перевірка бере ТОЙ САМИЙ список м'яких кандидатів і застосовує
//     до нього ту саму геометричну логіку, що й PerFontSafeDonorCommand
//     (перетин лише в межах PageIndex, нульова площа), АЛЕ з іншим
//     критерієм "з ким не можна перетинатись": не з будь-яким
//     використаним кодом, а конкретно з кодом, використовуваним
//     англійською (код англійської мови донорський підбір ніколи не чіпає).
//     Перетин м'якого кандидата з кодом іншої НЕ-англійської мови —
//     очікуваний і прийнятний (обидва однаково "жертвуються").
//
//     Додатково повідомляє про пари м'яких кандидатів, що перетинаються
//     МІЖ СОБОЮ — такі не можна брати одночасно в один шрифт, це
//     обмеження для алгоритму вибору донорів у GlyphAtlasPatcher,
//     а не привід відкидати обидва кандидати.
// EN: Continuation of the pre-GlyphAtlasPatcher checks — specifically
//     for BF2, where basic safe donors (unused by ANY language) fall
//     short of the full Cyrillic alphabet (60 of 66 needed,
//     PerFontSafeDonorCommand). "Soft" candidates
//     (SoftDonorAnalysis.ComputeSoftCandidates) — codes used by SOME
//     non-English language but NEVER by English — are already computed
//     and printed in "Language analysis", BUT that list only accounts
//     for language usage, not atlas geometry at all.
//
//     This check takes the SAME soft candidate list and applies the same
//     geometric logic as PerFontSafeDonorCommand (overlap only within
//     PageIndex, zero area), BUT with a different "who it must not
//     overlap with" criterion: not any used code, but specifically a
//     code used by English (donor selection never touches English code).
//     A soft candidate overlapping another non-English
//     language's code is expected and acceptable (both are equally
//     being "sacrificed").
//
//     Also reports pairs of soft candidates that overlap EACH OTHER —
//     those can't both be taken into the same font at once; that's a
//     constraint for GlyphAtlasPatcher's donor-selection algorithm, not
//     a reason to discard both candidates.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class SoftDonorGeometryCheckCommand
{
    private readonly record struct GlyphRect(ushort Code, int MinX, int MaxX, int MinY, int MaxY);

    public static async Task Run(DiagnosticReport report, UcfbChunk root, string filePath, string label, int neededGlyphCodes)
    {
        var summary = await SoftDonorAnalysis.BuildSummaryAsync(filePath);

        report.Log($"=== [{label}] Геометрична перевірка м'яких донорів ===");
        report.Log($"=== [{label}] Geometric check of soft donors ===");

        if (summary.SafeDonorCodes.Count >= neededGlyphCodes)
        {
            report.Log($"    UA: Базових безпечних кодів ({summary.SafeDonorCodes.Count}) вже достатньо для {neededGlyphCodes} — м'які донори не потрібні, перевірку пропущено.");
            report.Log($"    EN: Basic safe codes ({summary.SafeDonorCodes.Count}) already suffice for {neededGlyphCodes} — soft donors not needed, check skipped.");
            return;
        }

        var englishUsed = summary.Languages
            .Where(l => l.Language.Contains("english", StringComparison.OrdinalIgnoreCase))
            .SelectMany(l => l.CodeCounts.Keys)
            .ToHashSet();

        var softCandidates = SoftDonorAnalysis.ComputeSoftCandidates(summary);
        var candidateCodes = softCandidates.Select(c => c.Code).ToHashSet();

        report.Log($"    Базових безпечних кодів: {summary.SafeDonorCodes.Count}, потрібно: {neededGlyphCodes}, бракує: {neededGlyphCodes - summary.SafeDonorCodes.Count}");
        report.Log($"    М'яких кандидатів (мовна ознака, без геометрії): {candidateCodes.Count}");
        report.Log();

        var fonts = FontChunkLocator.FindAll(root);

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var glyphs = FontGlyphTable.Parse(fbod.RawData);

            var unsafeAgainstEnglish = new HashSet<int>();
            var zeroArea = new HashSet<int>();
            var mutualOverlaps = new List<string>();

            foreach (var pageGroup in glyphs.GroupBy(g => g.PageIndex))
            {
                if (pageGroup.Key >= font.TexturePages.Count)
                    continue; // UA: аномалія — уже зафіксована в UvRectBoundsCheckCommand / EN: anomaly — already caught by UvRectBoundsCheckCommand

                var texPixels = FontTexturePixelReader.ReadMip0(font.TexturePages[pageGroup.Key].Chunk);

                var rects = pageGroup.Select(g =>
                {
                    var x0 = (int)Math.Round(g.U0 * texPixels.Width);
                    var x1 = (int)Math.Round(g.U1 * texPixels.Width);
                    var y0 = (int)Math.Round(g.V0 * texPixels.Height);
                    var y1 = (int)Math.Round(g.V1 * texPixels.Height);
                    return new GlyphRect(g.Code, Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1), Math.Max(y0, y1));
                }).ToList();

                foreach (var r in rects)
                    if (candidateCodes.Contains(r.Code) && (r.MaxX - r.MinX <= 0 || r.MaxY - r.MinY <= 0))
                        zeroArea.Add(r.Code);

                for (var i = 0; i < rects.Count; i++)
                {
                    for (var j = i + 1; j < rects.Count; j++)
                    {
                        var a = rects[i];
                        var b = rects[j];

                        var overlapsX = a.MinX < b.MaxX && b.MinX < a.MaxX;
                        var overlapsY = a.MinY < b.MaxY && b.MinY < a.MaxY;
                        if (!(overlapsX && overlapsY)) continue;

                        var aIsCandidate = candidateCodes.Contains(a.Code);
                        var bIsCandidate = candidateCodes.Contains(b.Code);

                        if (aIsCandidate && englishUsed.Contains(b.Code))
                            unsafeAgainstEnglish.Add(a.Code);
                        if (bIsCandidate && englishUsed.Contains(a.Code))
                            unsafeAgainstEnglish.Add(b.Code);

                        if (aIsCandidate && bIsCandidate)
                            mutualOverlaps.Add($"0x{a.Code:X2} <-> 0x{b.Code:X2}");
                    }
                }
            }

            var geometricallySafe = candidateCodes.Except(unsafeAgainstEnglish).Except(zeroArea).ToList();

            report.Log($"  {font.BaseName}:");
            report.Log($"    Виключено — перетинається з англійським гліфом: {unsafeAgainstEnglish.Count}");
            report.Log($"    Виключено — нульова площа слоту: {zeroArea.Count}");
            report.Log($"    Геометрично придатних м'яких донорів: {geometricallySafe.Count} із {candidateCodes.Count}");

            if (mutualOverlaps.Count > 0)
            {
                report.Log($"    УВАГА: {mutualOverlaps.Count} пар м'яких кандидатів перетинаються МІЖ СОБОЮ (не можна брати обидва одночасно в цей шрифт):");
                foreach (var m in mutualOverlaps.Take(10))
                    report.Log($"      {m}");
            }

            report.Log();
        }
    }
}
