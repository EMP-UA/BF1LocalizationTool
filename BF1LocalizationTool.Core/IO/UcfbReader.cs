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
//
//     Певні FourCC за форматом ЗАВЖДИ листкові (сирі дані) — навіть якщо
//     перші байти їхнього вмісту випадково нагадують валідний заголовок
//     вкладеного чанку (Id+DataSize). Без явного винятку статистично
//     "шумні" бінарні дані (звукові семпли в snd_>DATA/PVS_, шейдерні
//     параметри в PIPE>INFO тощо) можуть ВИПАДКОВО, "успішно" розпарситись
//     як дерево фантомних дітей — перевірка "повне покриття end" (нижче,
//     розділ 6 специфікації) випадково ВИКОНУЄТЬСЯ для таких даних, тому
//     НЕ ловиться захистом hitInvalidChild. Оскільки UcfbWriter серіалізує
//     ВЕСЬ root, будь-яке таке хибне розпарсення призвело б до втрати
//     "слеку" цих ділянок навіть при read→write БЕЗ жодних навмисних змін.
//
//     Захист: явний список AlwaysLeafFourCC — чанки з цими FourCC
//     НІКОЛИ не намагаються парситись як контейнери, незалежно від того,
//     чи їхній вміст випадково "виглядає" як валідне піддерево.
// EN: Reads a .lvl file and builds a UcfbChunk tree.
//     Algorithm is recursive: each chunk tries to parse its data
//     as a set of nested chunks. If data does not match the structure —
//     stored as RawData.
//     File magic: "ucfb" (0x62666375 LE)
//
//     Certain FourCC are, by format, ALWAYS leaves (raw data) — even if
//     the first bytes of their content coincidentally resemble a valid
//     nested chunk header (Id+DataSize). Without an explicit exception,
//     statistically "noisy" binary data (sound samples in snd_>DATA/PVS_,
//     shader parameters in PIPE>INFO, etc.) can COINCIDENTALLY,
//     "successfully" parse as a tree of phantom children — the "full end
//     coverage" check (below, spec section 6) accidentally SUCCEEDS for
//     such data, so it isn't caught by the hitInvalidChild guard. Since
//     UcfbWriter serializes the ENTIRE root, any such misparse would lose
//     the "slack" of these regions even on a read→write with zero
//     intentional changes.
//
//     Guard: an explicit AlwaysLeafFourCC list — chunks with these FourCC
//     are NEVER attempted as containers, regardless of whether their
//     content coincidentally "looks like" a valid subtree.
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

    // UA: FourCC, які за форматом ЗАВЖДИ є листками (сирі дані), навіть
    //     якщо перші байти їхнього вмісту випадково нагадують валідний
    //     заголовок вкладеного чанку. ПІДТВЕРДЖЕНО емпірично:
    //       - "BODY": сирі пікселі текстур (шрифтові НЕ уражені — 0/16+7 —
    //         але FourCC той самий деінде в дереві).
    //       - "DATA": сирі звукові параметри в snd_ (FourCCPhantomChildScanCommand).
    //       - "PVS_": сирі дані в snd_ (100% екземплярів уражені).
    //       - "INFO": ВИЧЕРПНО перевірено (InfoChunkExhaustiveCheckCommand,
    //         497 екземплярів BF1 + 602 BF2, 0 винятків): кожен INFO —
    //         фіксований запис (шейдерні параметри в PIPE>INFO, метадані
    //         текстур у FMT_>INFO/LVL_>INFO тощо), ніколи не контейнер за
    //         задумом формату. Хибний парсинг (шаблон [flag,N,ID,N], де
    //         сирі байти випадково нагадують заголовок Id+DataSize=N)
    //         спричиняв стабільний 4-байтний "слек" на кожному шейдерному
    //         PIPE>INFO, що при округленні вгору по дереву давало сумарну
    //         втрату 76 байт (BF1) / 80 байт (BF2) при read→write round-trip
    //         навіть БЕЗ жодних навмисних змін (UcfbWriterNoOpRoundTripCommand).
    // EN: FourCC that are, by format, ALWAYS leaves (raw data), even if the
    //     first bytes of their content coincidentally resemble a valid
    //     nested chunk header. EMPIRICALLY CONFIRMED:
    //       - "BODY": raw texture pixels (font ones are NOT affected —
    //         0/16+7 — but the same FourCC appears elsewhere in the tree).
    //       - "DATA": raw sound parameters in snd_ (FourCCPhantomChildScanCommand).
    //       - "PVS_": raw data in snd_ (100% of instances affected).
    //       - "INFO": EXHAUSTIVELY verified (InfoChunkExhaustiveCheckCommand,
    //         497 BF1 + 602 BF2 instances, 0 exceptions): every INFO is a
    //         fixed-layout record (shader parameters in PIPE>INFO, texture
    //         metadata in FMT_>INFO/LVL_>INFO etc.), never a container by
    //         format design. Misparsing (the [flag,N,ID,N] pattern, where
    //         raw bytes coincidentally resemble an Id+DataSize=N header)
    //         caused a consistent 4-byte "slack" on every shader PIPE>INFO,
    //         which cascaded up the tree to a total loss of 76 bytes (BF1) /
    //         80 bytes (BF2) on a read→write round-trip even with ZERO
    //         intentional changes (UcfbWriterNoOpRoundTripCommand).
    private static readonly HashSet<string> AlwaysLeafFourCC = ["BODY", "DATA", "PVS_", "INFO"];

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
            Children = TryParseChildren(data, dataOffset, (int)actualSize, fourCC)
        };

        return chunk;
    }

    // -------------------------------------------------------------------------
    // UA: Намагається розпарсити вміст чанку як набір вкладених чанків.
    //     Якщо вміст не відповідає структурі — повертає порожній список.
    //     Умова валідності: перший дочірній чанк має dataSize <= доступного місця.
    //
    //     parentFourCC перевіряється ПЕРШИМ, до будь-якої спроби парсингу.
    //     Якщо він у AlwaysLeafFourCC — одразу повертаємо порожній список,
    //     НЕ намагаючись інтерпретувати сирі байти як дерево, незалежно
    //     від того, чи вони випадково "успішно" покрили б увесь end.
    // EN: Tries to parse chunk contents as a set of nested chunks.
    //     Returns empty list if content does not match the structure.
    //     Validity condition: first child chunk dataSize <= available space.
    //
    //     parentFourCC is checked FIRST, before any parsing attempt. If
    //     it's in AlwaysLeafFourCC — immediately return an empty list,
    //     WITHOUT attempting to interpret raw bytes as a tree, regardless
    //     of whether they would coincidentally "successfully" cover the
    //     entire end.
    // -------------------------------------------------------------------------
    private static List<UcfbChunk> TryParseChildren(byte[] data, int offset, int size, string parentFourCC)
    {
        if (AlwaysLeafFourCC.Contains(parentFourCC))
            return []; // UA: свідомо не намагаємось — завжди сирі дані за форматом
                       // EN: deliberately not attempted — always raw data by format

        if (size < ChunkHeaderSize)
            return [];

        var children = new List<UcfbChunk>();
        var pos = offset;
        var end = offset + size;
        var hitInvalidChild = false;

        while (pos + ChunkHeaderSize <= end)
        {
            var childDataSize = BitConverter.ToUInt32(data, pos + 4);

            // UA: Перевірка що розмір дочірнього чанку не виходить за межі
            // EN: Check that child chunk size does not exceed bounds
            if (pos + ChunkHeaderSize + childDataSize > end)
            {
                hitInvalidChild = true;
                break;
            }

            var child = ParseChunk(data, pos);
            children.Add(child);

            var rawNext = pos + ChunkHeaderSize + (int)childDataSize;
            pos = (rawNext + 3) & ~3;
        }

        if (hitInvalidChild)
            return [];

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