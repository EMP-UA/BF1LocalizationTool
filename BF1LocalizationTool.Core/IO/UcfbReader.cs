// =============================================================================
// BF1LocalizationTool.Core — IO/UcfbReader.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Читає .lvl файл і будує дерево UcfbChunk.
//     Алгоритм рекурсивний: кожен чанк намагається розпарсити свої дані
//     як набір вкладених чанків. Якщо дані не відповідають структурі —
//     зберігаються як RawData.
//     Магічний рядок файлу: "ucfb" (0x62666375 LE)
// EN: Reads a .lvl file and builds a UcfbChunk tree.
//     Algorithm is recursive: each chunk tries to parse its data
//     as a set of nested chunks. If data does not match the structure —
//     stored as RawData.
//     File magic: "ucfb" (0x62666375 LE)
// =============================================================================

using BF1LocalizationTool.Core.Chunks;

namespace BF1LocalizationTool.Core.IO;

public static class UcfbReader
{
    // UA: Магічний ідентифікатор кореневого чанку "ucfb"
    // EN: Root chunk magic identifier "ucfb"
    private const uint MagicUcfb = 0x62666375;

    // UA: Мінімальний розмір заголовку одного чанку (Id + DataSize)
    // EN: Minimum header size of a single chunk (Id + DataSize)
    private const int ChunkHeaderSize = 8;

    // -------------------------------------------------------------------------
    // UA: Читає .lvl файл і повертає кореневий ucfb-чанк.
    //     Кидає InvalidDataException якщо файл не є ucfb.
    // EN: Reads a .lvl file and returns the root ucfb chunk.
    //     Throws InvalidDataException if the file is not a ucfb.
    // -------------------------------------------------------------------------
    public static UcfbChunk ReadFile(string filePath)
    {
        var data = File.ReadAllBytes(filePath);
        return ReadFile(data);
    }

    public static UcfbChunk ReadFile(byte[] data)
    {
        if (data.Length < ChunkHeaderSize)
            throw new InvalidDataException(
                "UA: Файл занадто малий для ucfb / EN: File too small to be ucfb");

        var magic = BitConverter.ToUInt32(data, 0);
        if (magic != MagicUcfb)
            throw new InvalidDataException(
                $"UA: Невірна магія файлу 0x{magic:X8}, очікується 0x{MagicUcfb:X8} ('ucfb') / " +
                $"EN: Invalid file magic 0x{magic:X8}, expected 0x{MagicUcfb:X8} ('ucfb')");

        // UA: Парсимо кореневий чанк, починаючи з offset=0
        // EN: Parse root chunk starting at offset=0
        return ParseChunk(data, 0);
    }

    // -------------------------------------------------------------------------
    // UA: Парсить один чанк з масиву байтів за вказаним зміщенням.
    //     Повертає чанк з усіма вкладеними дочірніми чанками.
    // EN: Parses a single chunk from a byte array at the given offset.
    //     Returns a chunk with all nested child chunks.
    // -------------------------------------------------------------------------
    private static UcfbChunk ParseChunk(byte[] data, int offset)
    {
        var id = BitConverter.ToUInt32(data, offset);
        var dataSize = BitConverter.ToUInt32(data, offset + 4);
        var dataOffset = offset + ChunkHeaderSize;

        // UA: Захист від виходу за межі
        // EN: Guard against out-of-bounds
        var availableSize = (uint)(data.Length - dataOffset);
        var actualSize = Math.Min(dataSize, availableSize);

        var rawData = new byte[actualSize];
        Array.Copy(data, dataOffset, rawData, 0, actualSize);

        var fourCC = UcfbChunk.IdToFourCC(id);

        var chunk = new UcfbChunk
        {
            Id = id,
            FourCC = fourCC,
            DataSize = dataSize,
            RawData = rawData,
            FileDataOffset = dataOffset,
            Children = TryParseChildren(data, dataOffset, (int)actualSize)
        };

        return chunk;
    }

    // -------------------------------------------------------------------------
    // UA: Намагається розпарсити вміст чанку як набір вкладених чанків.
    //     Якщо вміст не відповідає структурі — повертає порожній список.
    //     Умова валідності: перший дочірній чанк має dataSize <= доступного місця.
    // EN: Tries to parse chunk contents as a set of nested chunks.
    //     Returns empty list if content does not match the structure.
    //     Validity condition: first child chunk dataSize <= available space.
    // -------------------------------------------------------------------------
    private static List<UcfbChunk> TryParseChildren(byte[] data, int offset, int size)
    {
        if (size < ChunkHeaderSize)
            return [];

        var children = new List<UcfbChunk>();
        var pos = offset;
        var end = offset + size;

        while (pos + ChunkHeaderSize <= end)
        {
            var childDataSize = BitConverter.ToUInt32(data, pos + 4);

            // UA: Перевірка що розмір дочірнього чанку не виходить за межі
            // EN: Check that child chunk size does not exceed bounds
            if (pos + ChunkHeaderSize + childDataSize > end)
                break;

            var child = ParseChunk(data, pos);
            children.Add(child);

            // UA: КРИТИЧНО: вирівнювання до 4 байт після кожного чанку.
            //     ucfb NOT включає padding у DataSize, але фізично вирівнює
            //     наступний чанк до 4 байт. Без цього BODY пропускається
            //     після NAME з нечітким розміром ("french\0"=7, "german\0"=7,
            //     "uk_english\0"=11 байт) — і всі такі мови не завантажуються.
            //     Підтверджено hex-дампом: NAME size=7, наступний чанк на +16 а не +15.
            // EN: CRITICAL: 4-byte alignment after each chunk.
            //     ucfb does NOT include padding in DataSize, but physically aligns
            //     the next chunk to 4 bytes. Without this BODY is skipped after
            //     NAME with odd size ("french\0"=7, "german\0"=7,
            //     "uk_english\0"=11 bytes) — all such languages fail to load.
            //     Confirmed by hex dump: NAME size=7, next chunk at +16 not +15.
            var rawNext = pos + ChunkHeaderSize + (int)childDataSize;
            pos = (rawNext + 3) & ~3;
        }

        // UA: Якщо не вдалось розпарсити жодного дочірнього чанку — сирі дані
        // EN: If no child chunks could be parsed — raw data
        return children.Count > 0 ? children : [];
    }

    // -------------------------------------------------------------------------
    // UA: Допоміжний метод: знайти всі чанки з заданим FourCC у дереві
    // EN: Helper: find all chunks with given FourCC in the tree
    // -------------------------------------------------------------------------
    public static IEnumerable<UcfbChunk> FindAll(UcfbChunk root, string fourCC)
    {
        if (root.FourCC == fourCC)
            yield return root;

        foreach (var child in root.Children)
            foreach (var found in FindAll(child, fourCC))
                yield return found;
    }

    // -------------------------------------------------------------------------
    // UA: Допоміжний метод: знайти перший чанк з заданим FourCC у дереві
    // EN: Helper: find first chunk with given FourCC in the tree
    // -------------------------------------------------------------------------
    public static UcfbChunk? FindFirst(UcfbChunk root, string fourCC) =>
        FindAll(root, fourCC).FirstOrDefault();
}
