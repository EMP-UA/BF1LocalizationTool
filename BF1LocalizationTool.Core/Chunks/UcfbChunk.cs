// =============================================================================
// BF1LocalizationTool.Core — Chunks/UcfbChunk.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Базова модель чанку ucfb-контейнера.
//     Формат Star Wars Battlefront (2004) зберігає всі ресурси у файлах .lvl,
//     що є ієрархічними ucfb-контейнерами:
//       [4 байти] Id       — FourCC або uint32 хеш
//       [4 байти] DataSize — розмір даних (LE), без урахування 8-байтового заголовку
//       [N байтів] Data    — або вкладені чанки, або сирі дані
// EN: Base model of a ucfb container chunk.
//     Star Wars Battlefront (2004) stores all assets in .lvl files,
//     which are hierarchical ucfb containers:
//       [4 bytes] Id       — FourCC or uint32 hash
//       [4 bytes] DataSize — data size (LE), excluding the 8-byte header
//       [N bytes] Data     — either nested chunks or raw data
// =============================================================================

namespace BF1LocalizationTool.Core.Chunks;

public class UcfbChunk
{
    // UA: Числовий ідентифікатор чанку (перші 4 байти)
    // EN: Numeric chunk identifier (first 4 bytes)
    public uint Id { get; init; }

    // UA: ASCII-рядок з 4 символів — лише якщо всі байти друковані (0x20..0x7E)
    //     Наприклад: "ucfb", "NAME", "HEAD", "BODY", "FTEX"
    //     Якщо Id є хешем — порожній рядок
    // EN: 4-character ASCII string — only if all bytes are printable (0x20..0x7E)
    //     Examples: "ucfb", "NAME", "HEAD", "BODY", "FTEX"
    //     Empty string if Id is a hash
    public string FourCC { get; init; } = string.Empty;

    // UA: Розмір даних у байтах (не враховує 8-байтовий заголовок)
    // EN: Size of data in bytes (does not include the 8-byte header)
    public uint DataSize { get; init; }

    // UA: Сирі байти даних цього чанку
    // EN: Raw data bytes of this chunk
    public byte[] RawData { get; init; } = [];

    // UA: Вкладені дочірні чанки (якщо дані містять підструктуру)
    // EN: Nested child chunks (if data contains a sub-structure)
    public List<UcfbChunk> Children { get; init; } = [];

    // UA: Зміщення початку даних від початку файлу (для точкового запису назад)
    // EN: Offset of data start from file beginning (for precise write-back)
    public long FileDataOffset { get; init; }

    // UA: true якщо Id є читабельним FourCC (не хеш)
    // EN: true if Id is a readable FourCC (not a hash)
    public bool IsFourCC => FourCC.Length == 4;

    // UA: true якщо чанк має вкладені дочірні чанки
    // EN: true if the chunk has nested child chunks
    public bool HasChildren => Children.Count > 0;

    // UA: Перетворює uint Id на рядок FourCC якщо всі 4 байти друковані
    // EN: Converts uint Id to FourCC string if all 4 bytes are printable
    public static string IdToFourCC(uint id)
    {
        var bytes = new byte[4];
        bytes[0] = (byte)(id & 0xFF);
        bytes[1] = (byte)((id >> 8) & 0xFF);
        bytes[2] = (byte)((id >> 16) & 0xFF);
        bytes[3] = (byte)((id >> 24) & 0xFF);

        foreach (var b in bytes)
            if (b < 0x20 || b > 0x7E)
                return string.Empty;

        return System.Text.Encoding.ASCII.GetString(bytes);
    }

    public override string ToString() =>
        IsFourCC
            ? $"[{FourCC}] size={DataSize}"
            : $"[0x{Id:X8}] size={DataSize}";
}
