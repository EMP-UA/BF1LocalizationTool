// =============================================================================
// BF1LocalizationTool.Diagnostic — UcfbStructuralTreeDiffCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Порівнює ДЕРЕВО З ДЕРЕВОМ (чанк із відповідним чанком за
//     СТРУКТУРНОЮ позицією, не байтовим офсетом) — а не сирий байтовий
//     diff за АБСОЛЮТНИМ зміщенням, який непридатний, коли десь
//     усередині зникає N байт: усі наступні байти фізично ЗСУВАЮТЬСЯ, і
//     порівняння "за офсетом" показує майже ВЕСЬ файл як "відмінний",
//     хоча сусідні чанки насправді байт-в-байт ідентичні — просто
//     зсунуті (підтверджено окремо через UcfbWriteRoundTripCommand).
//
//     Рекурсивно обходить root (з оригіналу) і повторно розпарсений
//     UcfbReader.ReadFile(rewrittenBytes) ОДНОЧАСНО, звіряючи на
//     кожному вузлі: FourCC/Id, DataSize, кількість дітей, і для
//     листків — байт-в-байт RawData. Друкує ПЕРШИЙ вузол, де щось не
//     збігається — це і є справжнє джерело втрати 1024/1308 байт, без
//     жодного артефакту зсуву.
// EN: Compares TREE TO TREE (chunk to its corresponding chunk by
//     STRUCTURAL position, not byte offset) — rather than a raw byte
//     diff by ABSOLUTE offset, which doesn't work when N bytes vanish
//     somewhere mid-stream: all subsequent bytes physically SHIFT, so an
//     offset-based comparison shows almost the ENTIRE file as
//     "different", even though neighboring chunks are actually
//     byte-for-byte identical — just shifted (confirmed separately via
//     UcfbWriteRoundTripCommand).
//
//     Recursively walks root (from the original) and a freshly re-parsed
//     UcfbReader.ReadFile(rewrittenBytes) SIMULTANEOUSLY, checking at
//     every node: FourCC/Id, DataSize, child count, and for leaves —
//     byte-for-byte RawData. Prints the FIRST node where something
//     mismatches — that IS the true source of the 1024/1308-byte loss,
//     with no shift artifact.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class UcfbStructuralTreeDiffCommand
{
    public static void Run(DiagnosticReport report, string lvlFilePath, string label)
    {
        var original = File.ReadAllBytes(lvlFilePath);
        var originalRoot = UcfbReader.ReadFile(original);
        var rewrittenBytes = UcfbWriter.WriteFile(originalRoot, replacements: null);
        var rewrittenRoot = UcfbReader.ReadFile(rewrittenBytes);

        report.Log($"=== [{label}] Структурне порівняння дерев: оригінал vs перезапис (без змін) ===");
        report.Log($"=== [{label}] Structural tree diff: original vs rewrite (no changes) ===");

        var firstMismatch = CompareRecursive(originalRoot, rewrittenRoot, path: "root");

        if (firstMismatch is null)
        {
            report.Log("    UA: Дерева СТРУКТУРНО ідентичні на кожному вузлі. Розбіжність розміру " +
                               "файлу має пояснення ПОЗА структурою чанків (напр. інше вирівнювання ПІСЛЯ " +
                               "останнього top-level чанку, що не є 'дитиною' жодного вузла).");
        }
        else
        {
            report.Log($"    UA: !! ПЕРШИЙ розбіжний вузол: {firstMismatch}");
        }
    }

    // UA: Повертає опис першої розбіжності, або null якщо все збігається.
    // EN: Returns a description of the first mismatch, or null if everything matches.
    private static string? CompareRecursive(UcfbChunk a, UcfbChunk b, string path)
    {
        if (a.Id != b.Id)
            return $"{path}: Id відрізняється ({a.FourCC}/{a.Id:X8} vs {b.FourCC}/{b.Id:X8})";

        if (a.Children.Count != b.Children.Count)
            return $"{path} [{a.FourCC}]: КІЛЬКІСТЬ ДІТЕЙ відрізняється " +
                   $"(оригінал={a.Children.Count}, перезапис={b.Children.Count})";

        if (a.Children.Count == 0)
        {
            // UA: Листок — порівнюємо сирі дані байт-в-байт.
            // EN: Leaf — compare raw data byte-for-byte.
            if (!a.RawData.AsSpan().SequenceEqual(b.RawData))
            {
                var minLen = Math.Min(a.RawData.Length, b.RawData.Length);
                var diffOffset = 0;
                while (diffOffset < minLen && a.RawData[diffOffset] == b.RawData[diffOffset]) diffOffset++;

                return $"{path} [{a.FourCC}]: RawData відрізняється " +
                       $"(оригінал.Length={a.RawData.Length}, перезапис.Length={b.RawData.Length}, " +
                       $"перший розбіжний байт усередині чанку на relOffset={diffOffset})";
            }
            return null;
        }

        for (var i = 0; i < a.Children.Count; i++)
        {
            var childLabel = a.Children[i].IsFourCC ? a.Children[i].FourCC : $"#{i}";
            var mismatch = CompareRecursive(a.Children[i], b.Children[i], $"{path} > {childLabel}");
            if (mismatch is not null) return mismatch;
        }

        return null;
    }
}