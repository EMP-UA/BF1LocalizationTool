// =============================================================================
// BF1LocalizationTool.Diagnostic — ReservedByteAnalysisCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: FONT_FORMAT_SPEC.md розділ 4 позначає байти offset=2 і offset=4
//     FBOD-запису як "(=0), зарезервовано" — це припущення виникло на
//     ОБМЕЖЕНІЙ вибірці під час первинного реверс-інжинірингу і НІКОЛИ
//     не перевірялось систематично на багатосторінкових шрифтах (де
//     FontResourceTreeDumpCommand щойно спростував гіпотезу "прихований
//     другий FBOD", залишивши питання "чому лише одна сторінка дає
//     впізнавану форму" ВІДКРИТИМ).
//
//     Перевіряє: чи справді ці байти ЗАВЖДИ 0 для ВСІХ 226 записів
//     кожного шрифту, чи вони варіюються — і якщо варіюються, чи
//     значення КОРЕЛЮЄ з кількістю сторінок цього шрифту (напр. якщо у
//     шрифту 4 сторінки, чи байт приймає значення 0,1,2,3).
//
//     Це проста фактична перевірка байтів, БЕЗ жодної інтерпретації
//     пікселів чи форм гліфів.
// EN: FONT_FORMAT_SPEC.md section 4 marks bytes at offset=2 and offset=4
//     of an FBOD record as "(=0), reserved" — this assumption arose from
//     a LIMITED sample during initial reverse engineering and was NEVER
//     systematically verified on multi-page fonts (where
//     FontResourceTreeDumpCommand just disproved the "hidden second
//     FBOD" hypothesis, leaving the question "why does only one page
//     give a recognizable shape" OPEN).
//
//     Verifies: whether these bytes are truly ALWAYS 0 for ALL 226
//     records of every font, or whether they vary — and if they vary,
//     whether the value CORRELATES with that font's page count (e.g. if
//     a font has 4 pages, does the byte take values 0,1,2,3).
//
//     This is a plain factual byte check, with NO interpretation of
//     pixels or glyph shapes.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class ReservedByteAnalysisCommand
{
    public static void RunAll(DiagnosticReport report, UcfbChunk root, string label)
    {
        var fonts = FontChunkLocator.FindAll(root);

        report.Log($"=== [{label}] Аналіз \"зарезервованих\" байтів FBOD (offset 2 і offset 4) ===");
        report.Log($"=== [{label}] FBOD \"reserved\" byte analysis (offset 2 and offset 4) ===");

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var raw = fbod.RawData;
            var recordCount = raw.Length / 24;

            var offset2Values = new Dictionary<byte, int>();
            var offset4Values = new Dictionary<byte, int>();

            for (var i = 0; i < recordCount; i++)
            {
                var recOffset = i * 24;
                var b2 = raw[recOffset + 2];
                var b4 = raw[recOffset + 4];

                offset2Values[b2] = offset2Values.GetValueOrDefault(b2) + 1;
                offset4Values[b4] = offset4Values.GetValueOrDefault(b4) + 1;
            }

            report.Log();
            report.Log($"  {font.BaseName} ({font.TexturePages.Count} сторінок, {recordCount} записів):");
            report.Log($"    offset=2: унікальних значень={offset2Values.Count} → " +
                               string.Join(", ", offset2Values.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}шт")));
            report.Log($"    offset=4: унікальних значень={offset4Values.Count} → " +
                               string.Join(", ", offset4Values.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}шт")));

            var suspect2 = offset2Values.Count > 1 && offset2Values.Count <= font.TexturePages.Count + 1;
            var suspect4 = offset4Values.Count > 1 && offset4Values.Count <= font.TexturePages.Count + 1;

            if (suspect2 || suspect4)
                report.Log($"    UA: !! ПІДОЗРІЛО — кількість унікальних значень узгоджується з кількістю сторінок ({font.TexturePages.Count})!");
        }
    }
}