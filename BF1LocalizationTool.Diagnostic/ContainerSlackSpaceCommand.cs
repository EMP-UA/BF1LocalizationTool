// =============================================================================
// BF1LocalizationTool.Diagnostic — ContainerSlackSpaceCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перевіряє, для КОЖНОГО контейнера (HasChildren=true) в дереві, чи
//     chunk.DataSize ТОЧНО дорівнює сумі (8+дані+padding) для кожної
//     дитини — за тим самим алгоритмом, що виконує UcfbWriter.
//
//     Контейнер може декларувати DataSize, БІЛЬШИЙ за фактично потрібний
//     для точного вміщення дітей + padding між ними — тобто мати "слек":
//     зарезервований невикористаний простір ПІСЛЯ останньої дитини, але
//     В МЕЖАХ заявленого DataSize. Такий слек НІКОЛИ не потрапляє в
//     модель UcfbChunk як окремий дочірній чанк (TryParseChildren
//     зупиняється, щойно залишилось < 8 байт), і UcfbWriter НЕ відтворює
//     цей слек при записі — тому файл після round-trip коротший рівно на
//     суму слеку по всіх контейнерах дерева.
// EN: Checks, for EVERY container (HasChildren=true) in the tree, whether
//     chunk.DataSize EXACTLY equals the sum of (8+data+padding) for each
//     child — using the exact same algorithm UcfbWriter performs.
//
//     A container may declare a DataSize LARGER than actually needed to
//     exactly fit its children plus inter-child padding — i.e. it has
//     "slack": reserved unused space AFTER the last child, but WITHIN the
//     declared DataSize. Such slack never enters the UcfbChunk model as a
//     separate child chunk (TryParseChildren stops as soon as fewer than
//     8 bytes remain), and UcfbWriter does NOT reproduce this slack when
//     writing — so a file is shorter after a round-trip by exactly the
//     sum of slack across all containers in the tree.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class ContainerSlackSpaceCommand
{
    public static void Run(DiagnosticReport report, string lvlFilePath, string label)
    {
        var root = UcfbReader.ReadFile(File.ReadAllBytes(lvlFilePath));

        report.Log($"=== [{label}] Пошук \"слеку\" (зарезервованого простору) в контейнерах ===");
        report.Log($"=== [{label}] Searching for \"slack\" (reserved space) in containers ===");

        var totalSlack = 0L;
        var slackFindings = new List<string>();

        WalkAndCheck(root, "root", ref totalSlack, slackFindings);

        report.Log($"    Перевірено дерево, сумарний слек знайдено: {totalSlack} байт");

        if (slackFindings.Count > 0)
        {
            report.Log("    --- Контейнери зі слеком (до 20) ---");
            foreach (var f in slackFindings.Take(20))
                report.Log($"      {f}");
        }

        report.Log();
        report.Log(totalSlack > 0
            ? $"    UA: ЗНАЙДЕНО — сумарний слек {totalSlack} байт пояснює (або частково пояснює) втрату " +
              "розміру при round-trip. UcfbWriter НЕ зберігає цей слек, бо серіалізує лише фактичних дітей."
            : "    UA: Слеку не знайдено на рівні контейнерів — причина втрати розміру ще ГЛИБША/ІНША, " +
              "потрібне подальше дослідження (можливо, у самому корені чи в обробці останнього чанку файлу).");
    }

    private static void WalkAndCheck(UcfbChunk chunk, string path, ref long totalSlack, List<string> findings)
    {
        if (chunk.HasChildren)
        {
            long computedSize = 0;
            foreach (var child in chunk.Children)
            {
                var childDataLen = child.HasChildren ? SumSerializedSize(child) : (long)child.RawData.Length;
                var written = 8 + childDataLen;
                var padding = (4 - (written % 4)) % 4;
                computedSize += written + padding;
            }

            var slack = (long)chunk.DataSize - computedSize;
            if (slack != 0)
            {
                var label = chunk.IsFourCC ? chunk.FourCC : $"0x{chunk.Id:X8}";
                findings.Add($"{path} > [{label}]: DataSize заявлено={chunk.DataSize}, " +
                              $"фактично потрібно для дітей={computedSize}, слек={slack}");
                totalSlack += slack;
            }

            foreach (var child in chunk.Children)
            {
                var childLabel = child.IsFourCC ? child.FourCC : $"#{chunk.Children.IndexOf(child)}";
                WalkAndCheck(child, $"{path} > {childLabel}", ref totalSlack, findings);
            }
        }
    }

    // UA: Рекурсивно рахує, скільки байт СЕРІАЛІЗУЄ UcfbWriter для
    //     цього контейнера (без його власного заголовка 8 байт) — щоб
    //     порівняти з DataSize його БАТЬКА.
    // EN: Recursively computes how many bytes UcfbWriter would SERIALIZE
    //     for this container (excluding its own 8-byte header) — to
    //     compare against its PARENT's DataSize.
    private static long SumSerializedSize(UcfbChunk chunk)
    {
        if (!chunk.HasChildren)
            return chunk.RawData.Length;

        long total = 0;
        foreach (var child in chunk.Children)
        {
            var childDataLen = child.HasChildren ? SumSerializedSize(child) : (long)child.RawData.Length;
            var written = 8 + childDataLen;
            var padding = (4 - (written % 4)) % 4;
            total += written + padding;
        }
        return total;
    }
}