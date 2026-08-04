// =============================================================================
// BF1LocalizationTool.Diagnostic — PageIndexByteCorrelationCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Decisive-перевірка гіпотези "offset=2 у FBOD = індекс текстурної
//     сторінки". ReservedByteAnalysisCommand показав ЗБІГ кількості
//     унікальних значень з кількістю сторінок у 11/11 шрифтів — але це
//     лише кореляція кількості. Ця команда перевіряє КОНКРЕТНИЙ зв'язок:
//     для заданого коду друкує (а) значення байта offset=2 в FBOD-записі,
//     і (б) на якій сторінці форма гліфа ВІЗУАЛЬНО повна (непрозорих
//     пікселів найбільше й формує впізнавану літеру) — маємо побачити,
//     що (а) точно вказує на (б).
// EN: Decisive check of the hypothesis "offset=2 in FBOD = texture
//     page index". ReservedByteAnalysisCommand showed a MATCH between the
//     unique value count and page count in 11/11 fonts — but that's just
//     a count correlation. This command checks the SPECIFIC link: for a
//     given code, prints (a) the offset=2 byte value in the FBOD record,
//     and (b) which page the glyph shape is VISUALLY full on (most
//     non-zero pixels, forms a recognizable letter) — we should see that
//     (a) precisely points to (b).
// =============================================================================

using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class PageIndexByteCorrelationCommand
{
    public static void Run(DiagnosticReport report, string lvlFilePath, string fontBaseName)
    {
        var root = UcfbReader.ReadFile(lvlFilePath);
        var font = FontChunkLocator.FindAll(root).FirstOrDefault(f => f.BaseName == fontBaseName);
        if (font is null)
        {
            report.Log($"UA: Шрифт '{fontBaseName}' не знайдено.");
            return;
        }

        var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
        if (fbod is null) { report.Log("UA: FBOD не знайдено."); return; }

        var raw = fbod.RawData;
        var recordCount = raw.Length / 24;

        var pagesPixels = font.TexturePages
            .Select(p => (p.Name, Pixels: FontTexturePixelReader.ReadMip0(p.Chunk)))
            .ToList();

        report.Log($"=== {fontBaseName}: перевірка offset=2 як індексу сторінки для КОЖНОГО коду ===");
        report.Log($"    Сторінок: {pagesPixels.Count} → " + string.Join(", ", pagesPixels.Select((p, i) => $"[{i}]={p.Name}")));
        report.Log();

        var matchCount = 0;
        var mismatchCount = 0;
        var mismatchExamples = new List<string>();

        for (var i = 0; i < recordCount; i++)
        {
            var recOffset = i * 24;
            var code = BitConverter.ToUInt16(raw, recOffset);
            var offset2Value = raw[recOffset + 2];

            var u0 = BitConverter.ToSingle(raw, recOffset + 8);
            var u1 = BitConverter.ToSingle(raw, recOffset + 12);
            var v0 = BitConverter.ToSingle(raw, recOffset + 16);
            var v1 = BitConverter.ToSingle(raw, recOffset + 20);

            // UA: Знаходимо сторінку з НАЙБІЛЬШОЮ щільністю непрозорих
            //     пікселів для цього UV — це ЛИШЕ для звірки з offset=2,
            //     не самостійний доказ: density-евристика тут
            //     використовується не як РІШЕННЯ, а як НЕЗАЛЕЖНИЙ спосіб
            //     перевірки вже сформульованої гіпотези.
            // EN: Find the page with HIGHEST non-zero pixel density for
            //     this UV — this is ONLY for cross-checking against
            //     offset=2, not a standalone proof: the density heuristic
            //     is used here not as the DECISION but as an INDEPENDENT
            //     way to test an already-formed hypothesis.
            var bestPageIdx = -1;
            var bestDensity = -1.0;

            for (var pageIdx = 0; pageIdx < pagesPixels.Count; pageIdx++)
            {
                var texPixels = pagesPixels[pageIdx].Pixels;
                var x0 = (int)Math.Round(u0 * texPixels.Width);
                var x1 = (int)Math.Round(u1 * texPixels.Width);
                var y0 = (int)Math.Round(v0 * texPixels.Height);
                var y1 = (int)Math.Round(v1 * texPixels.Height);

                var minX = Math.Min(x0, x1); var maxX = Math.Max(x0, x1);
                var minY = Math.Min(y0, y1); var maxY = Math.Max(y0, y1);

                var total = 0; var nonZero = 0;
                for (var y = minY; y < maxY; y++)
                {
                    if (y < 0 || y >= texPixels.Height) continue;
                    for (var x = minX; x < maxX; x++)
                    {
                        if (x < 0 || x >= texPixels.Width) continue;
                        var (a, _, _, _) = FontTexturePixelReader.DecodePixel(texPixels.RawA4R4G4B4Pixels, y * texPixels.Width + x);
                        total++;
                        if (a > 0) nonZero++;
                    }
                }

                var density = total == 0 ? 0 : (double)nonZero / total;
                if (density > bestDensity) { bestDensity = density; bestPageIdx = pageIdx; }
            }

            if (offset2Value == bestPageIdx)
            {
                matchCount++;
            }
            else
            {
                mismatchCount++;
                if (mismatchExamples.Count < 15)
                    mismatchExamples.Add($"code=0x{code:X2}: offset2={offset2Value}, найгустіша сторінка=[{bestPageIdx}] (густина={bestDensity:P0})");
            }
        }

        report.Log($"    Збігів (offset2 == найгустіша сторінка): {matchCount}/{recordCount}");
        report.Log($"    Розбіжностей: {mismatchCount}/{recordCount}");

        if (mismatchExamples.Count > 0)
        {
            report.Log("    --- Приклади розбіжностей (до 15) ---");
            foreach (var ex in mismatchExamples) report.Log($"      {ex}");
        }

        var matchRate = recordCount == 0 ? 0 : (double)matchCount / recordCount;
        report.Log();
        report.Log(matchRate >= 0.95
            ? $"    UA: ПІДТВЕРДЖЕНО ({matchRate:P1} збігів) — offset=2 у FBOD є індексом текстурної сторінки."
            : $"    UA: НЕ ПІДТВЕРДЖЕНО ({matchRate:P1} збігів) — кореляція кількості була випадковою, потрібна інша гіпотеза.");
    }
}