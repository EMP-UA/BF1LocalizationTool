// =============================================================================
// BF1LocalizationTool.Diagnostic — SoundDataMisparseHexDumpCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: ПРЯМА перевірка гіпотези, народженої з ContainerSlackSpaceCommand:
//     дрібні звукові чанки "DATA" (DataSize=9) парсяться як контейнери з
//     ОДНІЄЮ фантомною дитиною (8-байтний заголовок без даних), а не як
//     справжні листки. Друкує СИРІ БАЙТИ одного такого DATA-чанку прямо
//     з файлу (заголовок Id+DataSize і всі 9 байт вмісту) в hex і ASCII
//     — щоб ВІЗУАЛЬНО побачити, чи це насправді короткий рядок/параметр
//     (типове сире значення), чи там і справді валідний вкладений чанк.
//     Не вгадування — пряме читання конкретних байтів файлу.
// EN: DIRECT check of the hypothesis born from ContainerSlackSpaceCommand:
//     small sound "DATA" chunks (DataSize=9) are parsed as containers
//     with ONE phantom child (an 8-byte header with no data), rather
//     than as genuine leaves. Prints the RAW BYTES of one such DATA
//     chunk straight from the file (the Id+DataSize header and all 9
//     content bytes) in hex and ASCII — to VISUALLY see whether it's
//     actually a short string/parameter (a typical raw value), or
//     whether there truly is a valid nested chunk. Not guessing — direct
//     reading of specific file bytes.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class SoundDataMisparseHexDumpCommand
{
    public static void Run(DiagnosticReport report, string lvlFilePath)
    {
        var data = File.ReadAllBytes(lvlFilePath);
        var root = UcfbReader.ReadFile(data);

        var found = FindFirstSuspicious(root, path: "root");
        if (found is null)
        {
            report.Log("UA: Жодного підозрілого DATA-чанку з дітьми не знайдено.");
            return;
        }

        var (chunk, path) = found.Value;

        report.Log($"=== Сирі байти підозрілого чанку: {path} ===");
        report.Log($"=== Raw bytes of suspicious chunk: {path} ===");
        report.Log($"    FileDataOffset (початок ДАНИХ, після заголовка Id+DataSize): {chunk.FileDataOffset}");
        report.Log($"    Заявлений DataSize: {chunk.DataSize}, дітей розпарсено: {chunk.Children.Count}");
        report.Log();

        // UA: Друкуємо заголовок (8 байт ПЕРЕД FileDataOffset) + всі
        //     дані чанку — все, що реально лежить у файлі.
        // EN: Print the header (8 bytes BEFORE FileDataOffset) + all
        //     chunk data — everything actually present in the file.
        var headerStart = (int)chunk.FileDataOffset - 8;
        var totalLen = 8 + (int)chunk.DataSize;
        var bytes = data.Skip(headerStart).Take(totalLen).ToArray();

        report.Log($"    Hex (заголовок + дані, {bytes.Length} байт): {Convert.ToHexString(bytes)}");
        report.Log($"    ASCII: {string.Concat(bytes.Select(b => b is >= 0x20 and <= 0x7E ? (char)b : '.'))}");
        report.Log();

        report.Log("    --- Як TryParseChildren це розпарсив ---");
        foreach (var child in chunk.Children)
        {
            var childLabel = child.IsFourCC ? child.FourCC : $"0x{child.Id:X8}(hash)";
            report.Log($"      Дитина: Id={childLabel}, DataSize={child.DataSize}, RawData.Length={child.RawData.Length}");
        }

        report.Log();
        report.Log("    UA: Якщо ASCII вище виглядає як читабельний рядок чи очевидні сирі байти " +
                           "параметра (а НЕ як логічний вкладений FourCC) — це підтверджує МІСПАРСИНГ: " +
                           "TryParseChildren помилково прийняв перші 8 байт сирих даних за заголовок " +
                           "крихітного чанку, бо залишок (1-3 байти) формально виглядає як 'природне " +
                           "завершення' циклу. Це ЩЕ ОДИН, менш очевидний варіант бага з розділу 6 " +
                           "специфікації — там ловили катастрофічну втрату 90%+ даних, тут — тиху втрату " +
                           "кількох байт на КОЖНОМУ такому дрібному чанку.");
    }

    private static (UcfbChunk Chunk, string Path)? FindFirstSuspicious(UcfbChunk chunk, string path)
    {
        // UA: Підозрілий = має дітей, ЛИШЕ 1 дитину, і ця дитина не має
        //     власних даних (RawData.Length == 0) — типовий "фантомний"
        //     випадок, коли справжніх вкладених чанків там бути не мало.
        // EN: Suspicious = has children, has EXACTLY 1 child, and that
        //     child has no data of its own (RawData.Length == 0) — the
        //     typical "phantom" case where real nested chunks shouldn't
        //     exist at all.
        if (chunk.HasChildren && chunk.Children.Count == 1 && chunk.Children[0].RawData.Length == 0)
            return (chunk, path);

        foreach (var child in chunk.Children)
        {
            var childLabel = child.IsFourCC ? child.FourCC : "#hash";
            var result = FindFirstSuspicious(child, $"{path} > {childLabel}");
            if (result is not null) return result;
        }

        return null;
    }
}