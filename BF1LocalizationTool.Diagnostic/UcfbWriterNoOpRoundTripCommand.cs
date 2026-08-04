// =============================================================================
// BF1LocalizationTool.Diagnostic — UcfbWriterNoOpRoundTripCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: КОНТРОЛЬНИЙ тест, що доповнює UcfbWriteRoundTripCommand:
//     UcfbFileSizeDiscrepancyCommand виключає гіпотезу "хвіст поза
//     деревом" (0 байт різниці) — отже, будь-яка розбіжність розміру
//     виникає ВСЕРЕДИНІ серіалізації UcfbWriter, а не поза нею. Ця
//     команда перевіряє: чи розбіжність з'являється НАВІТЬ БЕЗ жодної
//     заміни (replacements=null) — тобто чи сам цикл read → write (без
//     будь-яких змін) вже змінює розмір файлу.
//
//     Якщо ТАК — це системна проблема вирівнювання (padding) в
//     UcfbWriter/UcfbReader, що загрожує КОЖНОМУ збереженню файлу цим
//     інструментом, а не лише запису гліфів. GlyphAtlasPatcher НЕ можна
//     писати, поки це не виправлено — інакше кожен збережений файл буде
//     мати непередбачувану структурну розбіжність з оригіналом.
// EN: The CONTROL test that complements UcfbWriteRoundTripCommand:
//     UcfbFileSizeDiscrepancyCommand rules out the "tail outside the
//     tree" hypothesis (0 bytes difference) — so any size discrepancy
//     arises INSIDE UcfbWriter's serialization, not outside it. This
//     command checks: does a discrepancy appear even WITHOUT any
//     replacement (replacements=null) — i.e. does the read → write
//     cycle alone (with zero changes) already alter the file size?
//
//     If YES — this is a systemic alignment (padding) problem in
//     UcfbWriter/UcfbReader that threatens EVERY file save by this tool,
//     not just glyph writing. GlyphAtlasPatcher must NOT be written until
//     this is fixed — otherwise every saved file would have an
//     unpredictable structural mismatch with the original.
// =============================================================================

using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class UcfbWriterNoOpRoundTripCommand
{
    public static void Run(DiagnosticReport report, string lvlFilePath)
    {
        var originalBytes = File.ReadAllBytes(lvlFilePath);
        var root = UcfbReader.ReadFile(originalBytes);

        // UA: Жодних замін — чистий read → write.
        // EN: No replacements — pure read → write.
        var rewrittenBytes = UcfbWriter.WriteFile(root, replacements: null);

        report.Log($"=== Контрольний тест: read → write БЕЗ жодних змін ===");
        report.Log($"=== Control test: read → write with NO changes ===");
        report.Log($"    Оригінал: {originalBytes.LongLength} байт");
        report.Log($"    Після write(root, null): {rewrittenBytes.LongLength} байт");
        report.Log($"    Різниця: {originalBytes.LongLength - rewrittenBytes.LongLength}");

        var identical = originalBytes.AsSpan().SequenceEqual(rewrittenBytes);
        report.Log($"    Байт-в-байт ідентичні: {(identical ? "ТАК" : "НІ")}");

        if (identical)
        {
            report.Log();
            report.Log("    UA: ПІДТВЕРДЖЕНО — read → write без жодних змін дає файл, БАЙТ-В-БАЙТ " +
                               "ідентичний оригіналу. Причина попередньої втрати розміру (1024/1308 байт) " +
                               "остаточно усунута виправленням AlwaysLeafFourCC у UcfbReader (додано \"INFO\") — " +
                               "корінь проблеми знайдено й усунуто, а не замасковано.");
            report.Log("    EN: CONFIRMED — read → write with zero changes produces a file BYTE-FOR-BYTE " +
                               "identical to the original. The cause of the previous size loss (1024/1308 bytes) " +
                               "has been permanently resolved by the AlwaysLeafFourCC fix in UcfbReader (added " +
                               "\"INFO\") — the root cause was found and fixed, not masked.");
            return;
        }

        // UA: Розбіжність є навіть БЕЗ змін — знаходимо ПЕРШИЙ байт,
        //     де оригінал і перезаписана версія розходяться, щоб
        //     локалізувати проблему в дереві.
        // EN: Discrepancy exists even WITHOUT changes — find the FIRST
        //     byte where the original and rewritten versions diverge, to
        //     localize the problem in the tree.
        var minLength = Math.Min(originalBytes.Length, rewrittenBytes.Length);
        var firstDiffOffset = -1;
        for (var i = 0; i < minLength; i++)
        {
            if (originalBytes[i] != rewrittenBytes[i])
            {
                firstDiffOffset = i;
                break;
            }
        }

        report.Log();
        report.Log("    UA: !! КРИТИЧНО — розбіжність існує НАВІТЬ БЕЗ жодної заміни! Це системна " +
                           "проблема UcfbWriter, що впливає на КОЖНЕ збереження файлу цим інструментом.");
        report.Log("    EN: !! CRITICAL — discrepancy exists even WITHOUT any replacement! This is a " +
                           "systemic UcfbWriter problem affecting EVERY file save by this tool.");

        if (firstDiffOffset >= 0)
        {
            var contextStart = Math.Max(0, firstDiffOffset - 16);
            report.Log($"    Перший байт розбіжності: offset={firstDiffOffset}");
            report.Log($"    Оригінал  (контекст): {Convert.ToHexString(originalBytes, contextStart, Math.Min(48, originalBytes.Length - contextStart))}");
            report.Log($"    Перезапис (контекст): {Convert.ToHexString(rewrittenBytes, contextStart, Math.Min(48, rewrittenBytes.Length - contextStart))}");
        }
        else
        {
            report.Log("    Усі спільні байти ідентичні — розбіжність лише в ДОВЖИНІ (напр. довший " +
                               "хвіст в одному з файлів після спільної частини).");
        }

        report.Log();
        report.Log("    UA: GlyphAtlasPatcher НЕ МОЖНА писати, поки ця розбіжність не пояснена й " +
                           "не виправлена в UcfbWriter/UcfbReader.");
    }
}