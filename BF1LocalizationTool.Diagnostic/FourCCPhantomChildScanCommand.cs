// =============================================================================
// BF1LocalizationTool.Diagnostic — FourCCPhantomChildScanCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Узагальнює факт, знайдений SoundDataMisparseHexDumpCommand на
//     ОДНОМУ прикладі (root>snd_>DATA): дитина з DataSize=0 і без
//     власних дітей — сигнатура ФАНТОМНОГО чанку (перші 4-8 байт сирих
//     даних батька випадково прочитані як заголовок Id+DataSize=0).
//     Проходить УВЕСЬ дерево ОБОХ файлів і для КОЖНОГО FourCC, що хоч
//     раз є контейнером, рахує: скільки разів серед його дітей є така
//     підозріла "нульова" дитина. Це дає ПОВНИЙ список кандидатів для
//     AlwaysLeafFourCC замість виправлення по одному вручну знайденому
//     випадку.
// EN: Generalizes the fact found by SoundDataMisparseHexDumpCommand on
//     ONE example (root>snd_>DATA): a child with DataSize=0 and no
//     children of its own is the signature of a PHANTOM chunk (the first
//     4-8 bytes of the parent's raw data coincidentally read as an
//     Id+DataSize=0 header). Walks the ENTIRE tree of BOTH files and,
//     for EVERY FourCC that acts as a container at least once, counts
//     how many times such a suspicious "zero" child appears among its
//     children. This gives a COMPLETE candidate list for AlwaysLeafFourCC
//     instead of fixing one manually found case at a time.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class FourCCPhantomChildScanCommand
{
    private sealed class Stats
    {
        public int ContainerOccurrences;
        public int OccurrencesWithSuspiciousChild;
        public int TotalSuspiciousChildren;
    }

    public static void Run(DiagnosticReport report, string lvlFilePath, string label)
    {
        var root = UcfbReader.ReadFile(File.ReadAllBytes(lvlFilePath));
        var byFourCC = new Dictionary<string, Stats>();

        Walk(root, byFourCC);

        report.Log($"=== [{label}] Скан ФАНТОМНИХ дітей по КОЖНОМУ FourCC-контейнеру ===");
        report.Log($"=== [{label}] Phantom-child scan for EVERY FourCC container ===");
        report.Log();

        foreach (var (fourCC, stats) in byFourCC.OrderByDescending(kv => kv.Value.TotalSuspiciousChildren))
        {
            if (stats.TotalSuspiciousChildren == 0) continue;

            report.Log($"  [{fourCC}]: контейнер {stats.ContainerOccurrences} разів у дереві, " +
                               $"з фантомними дітьми {stats.OccurrencesWithSuspiciousChild} разів " +
                               $"(усього {stats.TotalSuspiciousChildren} фантомних дітей)");
        }

        report.Log();
        report.Log("--- Контейнери БЕЗ жодного фантома (ймовірно, справжні структуровані піддерева) ---");
        foreach (var (fourCC, stats) in byFourCC.Where(kv => kv.Value.TotalSuspiciousChildren == 0))
            report.Log($"  [{fourCC}]: {stats.ContainerOccurrences} разів, фантомів немає");

        report.Log();
        report.Log("UA: FourCC зі списку вище (з фантомними дітьми) — кандидати для AlwaysLeafFourCC.");
        report.Log("EN: FourCC from the list above (with phantom children) — candidates for AlwaysLeafFourCC.");
    }

    private static void Walk(UcfbChunk chunk, Dictionary<string, Stats> byFourCC)
    {
        if (chunk.HasChildren)
        {
            var key = chunk.IsFourCC ? chunk.FourCC : $"0x{chunk.Id:X8}(hash)";
            if (!byFourCC.TryGetValue(key, out var stats))
                byFourCC[key] = stats = new Stats();

            stats.ContainerOccurrences++;

            // UA: Підозріла дитина = заявлений DataSize==0 і сама без
            //     дітей — типова сигнатура фантома (справжні NAME/FTEX/
            //     тощо завжди мають реальний ненульовий вміст).
            // EN: Suspicious child = declared DataSize==0 and itself has
            //     no children — the typical phantom signature (real
            //     NAME/FTEX/etc. always carry actual non-zero content).
            var suspiciousCount = chunk.Children.Count(c => c.DataSize == 0 && c.Children.Count == 0);
            if (suspiciousCount > 0)
            {
                stats.OccurrencesWithSuspiciousChild++;
                stats.TotalSuspiciousChildren += suspiciousCount;
            }

            foreach (var child in chunk.Children)
                Walk(child, byFourCC);
        }
    }
}