// =============================================================================
// BF1LocalizationTool.Diagnostic — UcfbNoOpByteDiffCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Точна локалізація байтових втрат при read → write БЕЗ жодних змін
//     (UcfbWriterNoOpRoundTripCommand), БЕЗ жодних припущень про
//     структуру дерева ucfb: знаходить НАЙДОВШИЙ СПІЛЬНИЙ ПРЕФІКС і
//     НАЙДОВШИЙ СПІЛЬНИЙ СУФІКС між оригіналом і перезаписом. Якщо
//     різниця — це один суцільний "видалений" шматок (не розкидані зміни
//     по всьому файлу), префікс+суфікс точно окреслять межі цього
//     шматка. Якщо ж різниця викликана зсувом байтів через
//     видалення/вставку ПОСЕРЕД потоку (не в самому кінці) —
//     використовуй UcfbStructuralTreeDiffCommand, який порівнює дерево з
//     деревом за структурною позицією, а не байтовим офсетом.
// EN: Precise localization of byte loss during read → write with NO
//     changes (UcfbWriterNoOpRoundTripCommand), with NO assumptions about
//     the ucfb tree structure: finds the LONGEST COMMON PREFIX and
//     LONGEST COMMON SUFFIX between the original and the rewrite. If the
//     difference is one contiguous "removed" chunk (not scattered changes
//     throughout the file), prefix+suffix will precisely bracket that
//     chunk's boundaries. If the difference instead comes from a byte
//     shift caused by a deletion/insertion MID-STREAM (not at the very
//     end), use UcfbStructuralTreeDiffCommand instead, which compares
//     tree to tree by structural position rather than byte offset.
// =============================================================================

using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class UcfbNoOpByteDiffCommand
{
    public static void Run(DiagnosticReport report, string lvlFilePath, string label)
    {
        var original = File.ReadAllBytes(lvlFilePath);
        var root = UcfbReader.ReadFile(original);
        var rewritten = UcfbWriter.WriteFile(root, replacements: null);

        report.Log($"=== [{label}] Точна локалізація розбіжності: read → write без змін ===");
        report.Log($"=== [{label}] Precise discrepancy location: read → write with no changes ===");
        report.Log($"    Оригінал: {original.LongLength} байт, Перезапис: {rewritten.LongLength} байт, " +
                           $"різниця: {original.LongLength - rewritten.LongLength}");

        if (original.AsSpan().SequenceEqual(rewritten))
        {
            report.Log("    UA: Файли ідентичні — розбіжності немає.");
            return;
        }

        // UA: Найдовший спільний ПРЕФІКС.
        // EN: Longest common PREFIX.
        var minLen = Math.Min(original.Length, rewritten.Length);
        var prefixLen = 0;
        while (prefixLen < minLen && original[prefixLen] == rewritten[prefixLen])
            prefixLen++;

        // UA: Найдовший спільний СУФІКС, рахуючи з кінця обох масивів,
        //     не заходячи на вже враховану префіксом ділянку.
        // EN: Longest common SUFFIX, counting from the end of both
        //     arrays, without overlapping the prefix-covered region.
        var suffixLen = 0;
        var maxSuffix = minLen - prefixLen;
        while (suffixLen < maxSuffix &&
               original[original.Length - 1 - suffixLen] == rewritten[rewritten.Length - 1 - suffixLen])
            suffixLen++;

        var removedFromOriginal = original.Length - prefixLen - suffixLen;
        var insertedInRewritten = rewritten.Length - prefixLen - suffixLen;

        report.Log($"    Спільний префікс: {prefixLen} байт (розбіжність починається на offset={prefixLen})");
        report.Log($"    Спільний суфікс: {suffixLen} байт (з кінця файлу)");
        report.Log($"    Ділянка, що відрізняється в ОРИГІНАЛІ: {removedFromOriginal} байт " +
                           $"[{prefixLen}..{original.Length - suffixLen})");
        report.Log($"    Ділянка, що відрізняється в ПЕРЕЗАПИСІ: {insertedInRewritten} байт " +
                           $"[{prefixLen}..{rewritten.Length - suffixLen})");

        var origDiffRegion = original.Skip(prefixLen).Take(Math.Min(removedFromOriginal, 128)).ToArray();
        var rewrittenDiffRegion = rewritten.Skip(prefixLen).Take(Math.Min(insertedInRewritten, 128)).ToArray();

        report.Log($"    Оригінал у ділянці розбіжності (до 128 байт, hex): {Convert.ToHexString(origDiffRegion)}");
        report.Log($"    Перезапис у ділянці розбіжності (до 128 байт, hex): {Convert.ToHexString(rewrittenDiffRegion)}");
        report.Log($"    Чи ділянка в ОРИГІНАЛІ — усі нулі: {original.Skip(prefixLen).Take(removedFromOriginal).All(b => b == 0)}");

        // UA: Ділянка лежить у файлі за абсолютним зміщенням prefixLen —
        //     корисно для подальшого hex-перегляду вручну.
        // EN: The region lies in the file at absolute offset prefixLen —
        //     useful for further manual hex inspection.
        report.Log($"    Абсолютне зміщення розбіжності у файлі: {prefixLen}");
    }
}