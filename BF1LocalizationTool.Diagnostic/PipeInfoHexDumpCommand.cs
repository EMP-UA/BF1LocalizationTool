// =============================================================================
// BF1LocalizationTool.Diagnostic — PipeInfoHexDumpCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Прямий hex-дамп ОДНОГО root>SHDR>PIPE>INFO чанку — щоб побачити,
//     ЩО САМЕ лежить в останніх 4 "загублених" байтах (DataSize=16,
//     розпарсено дітей лише на 12), знайдених ContainerSlackSpaceCommand
//     після виправлення AlwaysLeafFourCC. Друкує ВЕСЬ вміст чанку
//     (заголовок + усі 16 байт даних) в hex, і ОКРЕМО виділяє останні 4
//     байти — саме вони не потрапили в жоден дочірній чанк.
// EN: Direct hex dump of ONE root>SHDR>PIPE>INFO chunk — to see WHAT
//     EXACTLY lies in the last 4 "lost" bytes (DataSize=16, but only 12
//     bytes were parsed into children), found by ContainerSlackSpaceCommand
//     after the AlwaysLeafFourCC fix. Prints the ENTIRE chunk content
//     (header + all 16 data bytes) in hex, and SEPARATELY highlights the
//     last 4 bytes — exactly the ones that never made it into any child
//     chunk.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class PipeInfoHexDumpCommand
{
    public static void Run(DiagnosticReport report, string lvlFilePath)
    {
        var data = File.ReadAllBytes(lvlFilePath);
        var root = UcfbReader.ReadFile(data);

        var info = FindFirstPipeInfo(root);
        if (info is null) { report.Log("UA: Жодного PIPE>INFO не знайдено."); return; }

        report.Log("=== Повний hex-дамп одного root>SHDR>PIPE>INFO чанку ===");
        report.Log("=== Full hex dump of one root>SHDR>PIPE>INFO chunk ===");
        report.Log($"    FileDataOffset={info.FileDataOffset}, DataSize={info.DataSize}, " +
                           $"дітей розпарсено={info.Children.Count}");
        report.Log();

        var headerStart = (int)info.FileDataOffset - 8;
        var totalLen = 8 + (int)info.DataSize;
        var bytes = data.Skip(headerStart).Take(totalLen).ToArray();

        report.Log($"    Заголовок (Id+DataSize, 8 байт): {Convert.ToHexString(bytes, 0, 8)}");
        report.Log($"    Усі дані ({info.DataSize} байт): {Convert.ToHexString(bytes, 8, (int)info.DataSize)}");
        report.Log();

        report.Log("    --- Розпарсені діти (перші 12 байт) ---");
        foreach (var child in info.Children)
        {
            var childLabel = child.IsFourCC ? child.FourCC : $"0x{child.Id:X8}(hash)";
            report.Log($"      Id={childLabel}, DataSize={child.DataSize}, RawData: {Convert.ToHexString(child.RawData)}");
        }

        var childrenCoveredBytes = info.Children.Sum(c => 8 + c.RawData.Length);
        var tailBytes = bytes.Skip(8 + childrenCoveredBytes).Take((int)info.DataSize - childrenCoveredBytes).ToArray();

        report.Log();
        report.Log($"    --- ОСТАННІ {tailBytes.Length} байти, що НЕ потрапили в жодну дитину (\"загублені\") ---");
        report.Log($"    Hex: {Convert.ToHexString(tailBytes)}");
        report.Log($"    Як uint32 (LE): {BitConverter.ToUInt32(tailBytes, 0)}");
        report.Log($"    Як float (LE): {BitConverter.ToSingle(tailBytes, 0)}");
        report.Log($"    ASCII: {string.Concat(tailBytes.Select(b => b is >= 0x20 and <= 0x7E ? (char)b : '.'))}");
        report.Log($"    Чи всі нулі: {tailBytes.All(b => b == 0)}");
    }

    private static UcfbChunk? FindFirstPipeInfo(UcfbChunk chunk)
    {
        if (chunk.FourCC == "PIPE")
        {
            var info = chunk.Children.FirstOrDefault(c => c.FourCC == "INFO");
            if (info is not null) return info;
        }

        foreach (var child in chunk.Children)
        {
            var found = FindFirstPipeInfo(child);
            if (found is not null) return found;
        }

        return null;
    }
}