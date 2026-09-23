// =============================================================================
// BF1LocalizationTool.Diagnostic — ContainerSlackSpaceCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: UcfbStructuralTreeDiffCommand показав, що дерева ІДЕНТИЧНІ на
//     кожному вузлі (Id, кількість дітей, RawData листків) — але файл
//     після round-trip коротший на 1024/1308 байт. Це можливо ЛИШЕ якщо
//     розбіжність ховається в тому, як РАХУЄТЬСЯ РОЗМІР КОНТЕЙНЕРІВ:
//     якийсь контейнер у ОРИГІНАЛІ має DataSize, який заявляє БІЛЬШИЙ
//     розмір, ніж фактично потрібно для точного вміщення його дітей +
//     padding між ними (тобто має "слек" — зарезервований невикористаний
//     простір ПІСЛЯ останньої дитини, але В МЕЖАХ заявленого DataSize).
//     Такий слек НІКОЛИ не потрапляє в модель UcfbChunk як окремий
//     дочірній чанк (TryParseChildren просто зупиняється, коли
//     "залишилось < 8 байт" — слек МЕНШИЙ за 8 байт непомітний за
//     конструкцією, але слек БІЛЬШИЙ за 8 байт МАВ БИ або спричинити
//     hitInvalidChild, або (якщо збігається з валідним, хоч і чужим,
//     заголовком) призвести до фантомного дочірнього чанку — обидва
//     випадки мали б проявитись раніше. Якщо жодного з них не сталося,
//     а слек все ж є — TryParseChildren МОЖЕ мати ще один, досі не
//     задокументований крайовий випадок).
//
//     Перевіряє для КОЖНОГО контейнера (HasChildren=true) в дереві: чи
//     chunk.DataSize ТОЧНО дорівнює сумі (8+дані+padding) для кожної
//     дитини — точно за тим самим алгоритмом, що виконує UcfbWriter.
// EN: UcfbStructuralTreeDiffCommand showed the trees are IDENTICAL at
//     every node (Id, child count, leaf RawData) — yet the file after a
//     round-trip is shorter by 1024/1308 bytes. This is possible ONLY if
//     the discrepancy hides in how CONTAINER SIZE IS COMPUTED: some
//     container in the ORIGINAL has a DataSize declaring a LARGER size
//     than actually needed to exactly fit its children + inter-child
//     padding (i.e. it has "slack" — reserved unused space AFTER the
//     last child, but WITHIN the declared DataSize). Such slack never
//     enters the UcfbChunk model as a separate child chunk
//     (TryParseChildren simply stops when "fewer than 8 bytes remain" —
//     slack SMALLER than 8 bytes is invisible by construction, but slack
//     LARGER than 8 bytes should have either triggered hitInvalidChild,
//     or (if it coincidentally matches a valid-looking, foreign header)
//     produced a phantom child chunk — both cases should have surfaced
//     earlier. If neither happened, yet slack still exists —
//     TryParseChildren may have another, still-undocumented edge case).
//
//     Checks, for EVERY container (HasChildren=true) in the tree: whether
//     chunk.DataSize EXACTLY equals the sum of (8+data+padding) for each
//     child — using the exact same algorithm UcfbWriter performs.
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