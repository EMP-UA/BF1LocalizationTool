// =============================================================================
// BF1LocalizationTool.Diagnostic — UcfbFileSizeDiscrepancyCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: UcfbWriteRoundTripCommand показав розбіжність розміру файлу
//     (оригінал 4899596 байт, після запису тієї самої заміни розміру —
//     4898572 байт, різниця 1024 байти) при тому, що ВСІ перевірені
//     компоненти (контрольний патерн, сусідні сторінки, FBOD) збіглися.
//     Це НЕ можна залишати непоясненим — можлива причина: оригінальний
//     файл має "хвіст" (байти ПІСЛЯ завершення кореневого ucfb-дерева,
//     напр. вирівнювання до розміру сектора/блоку), які UcfbReader
//     ніколи не парсив у модель UcfbChunk, і тому UcfbWriter природно
//     не міг їх відтворити.
//
//     Перевіряє: root.DataSize (заявлений розмір кореневого чанку) +
//     8 (заголовок) ПРОТИ фактичної довжини файлу — якщо є різниця,
//     це і є той самий "хвіст", ПОЗА деревом, про існування якого
//     специфікація нічого не каже.
// EN: UcfbWriteRoundTripCommand showed a file size discrepancy (original
//     4899596 bytes, after writing the SAME-size replacement — 4898572
//     bytes, a 1024-byte difference), even though ALL checked components
//     (control pattern, neighboring pages, FBOD) matched. This CANNOT be
//     left unexplained — a likely cause: the original file has a "tail"
//     (bytes AFTER the root ucfb tree ends, e.g. alignment to a
//     sector/block size) that UcfbReader never parsed into the UcfbChunk
//     model, and which UcfbWriter therefore couldn't naturally reproduce.
//
//     Checks: root.DataSize (the root chunk's declared size) + 8
//     (header) AGAINST the actual file length — if there's a gap, that
//     IS the same "tail", outside the tree, which the spec says nothing
//     about.
// =============================================================================

using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class UcfbFileSizeDiscrepancyCommand
{
    public static void Run(DiagnosticReport report, string lvlFilePath)
    {
        var data = File.ReadAllBytes(lvlFilePath);
        var root = UcfbReader.ReadFile(data);

        var declaredTreeSize = 8L + root.DataSize; // UA: заголовок кореня (8) + його DataSize
        var actualFileSize = data.LongLength;
        var tailBytes = actualFileSize - declaredTreeSize;

        report.Log($"=== Перевірка \"хвоста\" файлу поза деревом ucfb: {lvlFilePath} ===");
        report.Log($"=== Checking for a file \"tail\" outside the ucfb tree: {lvlFilePath} ===");
        report.Log($"    Фактичний розмір файлу: {actualFileSize}");
        report.Log($"    Розмір, задекларований деревом (8 + root.DataSize): {declaredTreeSize}");
        report.Log($"    Різниця (\"хвіст\" поза деревом): {tailBytes}");

        if (tailBytes > 0)
        {
            var tailStart = (int)declaredTreeSize;
            var previewLength = (int)Math.Min(64, tailBytes);
            var preview = data.Skip(tailStart).Take(previewLength).ToArray();

            report.Log($"    Перші {previewLength} байт хвоста (hex): {Convert.ToHexString(preview)}");
            report.Log($"    Чи весь хвіст — нулі: {data.Skip(tailStart).All(b => b == 0)}");
            report.Log();
            report.Log("    UA: ПІДТВЕРДЖЕНО — файл МАЄ дані ПОЗА кореневим ucfb-деревом, які " +
                               "UcfbReader ніколи не бачив і UcfbWriter не може відтворити. Це пояснює " +
                               "розбіжність розміру в UcfbWriteRoundTripCommand: UcfbWriter.WriteFile " +
                               "МАЄ бути виправлений, щоб зберігати цей хвіст (дописати його в кінець " +
                               "виводу), інакше кожен збережений файл втрачатиме ці байти.");
        }
        else if (tailBytes == 0)
        {
            report.Log();
            report.Log("    UA: Хвоста немає — весь файл повністю покривається деревом ucfb. " +
                               "Розбіжність розміру з UcfbWriteRoundTripCommand має ІНШУ причину, " +
                               "не цю — потрібне подальше дослідження.");
        }
        else
        {
            report.Log();
            report.Log("    UA: !! АНОМАЛІЯ — дерево заявляє БІЛЬШИЙ розмір, ніж сам файл. " +
                               "Це вказує на ще один, окремий баг парсингу.");
        }
    }
}