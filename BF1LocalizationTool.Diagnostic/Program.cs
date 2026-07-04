// =============================================================================
// BF1LocalizationTool.Diagnostic — Program.cs
// UA: Аналізатор структури .loc/.lvl файлів для налагодження парсера.
//     Запуск: BF1Diagnostic.exe <шлях до файлу>
//     Виводить точну байтову структуру і зберігає у .txt поруч з файлом.
// EN: .loc/.lvl file structure analyzer for parser debugging.
//     Usage: BF1Diagnostic.exe <file path>
//     Prints exact byte structure and saves .txt next to the file.
// =============================================================================

using System.Text;

if (args.Length == 0)
{
    Console.WriteLine("UA: Використання: BF1Diagnostic.exe <шлях до файлу>");
    Console.WriteLine("EN: Usage: BF1Diagnostic.exe <file path>");
    Console.WriteLine();
    Console.WriteLine("Drag & drop a .loc or .lvl file onto this exe.");
    return;
}

var filePath = args[0];
if (!File.Exists(filePath))
{
    Console.WriteLine($"File not found: {filePath}");
    return;
}

var outPath = filePath + ".diagnostic.txt";
var data = File.ReadAllBytes(filePath);
var sb = new StringBuilder();

void Log(string line)
{
    Console.WriteLine(line);
    sb.AppendLine(line);
}

Log($"=== BF2 Loc Diagnostic ===");
Log($"File: {filePath}");
Log($"Size: {data.Length} bytes (0x{data.Length:X})");
Log("");

// UA: Дамп перших 256 байт як hex+ascii
// EN: Hex+ASCII dump of first 256 bytes
Log("--- First 256 bytes ---");
var dumpLen = Math.Min(256, data.Length);
for (int i = 0; i < dumpLen; i += 16)
{
    var hex = string.Join(" ", Enumerable.Range(i, Math.Min(16, dumpLen - i))
        .Select(j => data[j].ToString("X2")));
    var ascii = string.Concat(Enumerable.Range(i, Math.Min(16, dumpLen - i))
        .Select(j => data[j] >= 0x20 && data[j] < 0x7F ? (char)data[j] : '.'));
    Log($"  {i,6:X4}: {hex,-48} {ascii}");
}
Log("");

// UA: Рекурсивний аналіз ucfb-дерева
// EN: Recursive ucfb tree analysis
Log("--- Chunk tree ---");
AnalyzeChunks(data, 0, data.Length, 0);

Log("");
Log("--- Raw chunk scan (looking for known FourCCs) ---");
ScanForChunks(data);

File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
Log("");
Log($"Saved to: {outPath}");

// ===========================================================================
static void AnalyzeChunks(byte[] data, int start, int end, int depth)
{
    var indent = new string(' ', depth * 2);
    var pos = start;

    while (pos + 8 <= end && pos + 8 <= data.Length)
    {
        var id       = ReadFourCC(data, pos);
        var size     = (int)ReadUInt32(data, pos + 4);
        var dataStart = pos + 8;
        var dataEnd  = dataStart + size;

        // UA: Обмеження щоб не вийти за межі
        // EN: Clamp to file bounds
        var clampedEnd = Math.Min(dataEnd, data.Length);

        Console.WriteLine($"{indent}[{pos:X6}] '{id}' (0x{(uint)ReadUInt32(data, pos):X8}) " +
                          $"size={size} (0x{size:X}) dataStart={dataStart:X6} dataEnd={dataEnd:X6}");

        // UA: Якщо це відомий контейнер — рекурсуємо всередину
        // EN: If this is a known container — recurse inside
        if (id == "ucfb" || id == "Locl")
        {
            AnalyzeChunks(data, dataStart, clampedEnd, depth + 1);
        }
        else if (id == "NAME")
        {
            // UA: Виводимо вміст NAME як ASCII
            // EN: Print NAME content as ASCII
            var nameLen = Math.Min(size, clampedEnd - dataStart);
            var raw = Encoding.ASCII.GetString(data, dataStart, nameLen);
            var printable = raw.Replace("\0", "\\0");
            Console.WriteLine($"{indent}  → NAME content ({size} bytes): '{printable}'");
            Console.WriteLine($"{indent}  → Hex: {HexDump(data, dataStart, nameLen)}");
            Console.WriteLine($"{indent}  → dataEnd={dataEnd} aligned={(dataEnd + 3) & ~3}");
        }
        else if (id == "BODY")
        {
            Console.WriteLine($"{indent}  → BODY found at {pos:X6}, {size} bytes of entries");
            // UA: Перші 32 байти BODY
            // EN: First 32 bytes of BODY
            var bodyPreview = Math.Min(32, clampedEnd - dataStart);
            Console.WriteLine($"{indent}  → First {bodyPreview} bytes: {HexDump(data, dataStart, bodyPreview)}");
        }

        // UA: Просте просування — БЕЗ вирівнювання
        // EN: Simple advancement — WITHOUT alignment
        var nextNoAlign = dataEnd;
        // UA: З вирівнюванням до 4 байт
        // EN: With 4-byte alignment
        var nextAligned = (dataEnd + 3) & ~3;

        if (nextNoAlign != nextAligned && nextNoAlign < data.Length)
        {
            Console.WriteLine($"{indent}  *** ALIGNMENT NOTE: next={nextNoAlign:X6} aligned={nextAligned:X6}");
            Console.WriteLine($"{indent}  *** Bytes at {nextNoAlign:X6}: {HexDump(data, nextNoAlign, Math.Min(8, data.Length - nextNoAlign))}");
            Console.WriteLine($"{indent}  *** Bytes at {nextAligned:X6}: {HexDump(data, nextAligned, Math.Min(8, data.Length - nextAligned))}");
        }

        // UA: Просуваємось ОБОМА способами і показуємо що там є
        // EN: Advance BOTH ways and show what's there
        pos = nextAligned;
    }
}

// ===========================================================================
// UA: Сканує весь файл побайтно шукаючи відомі FourCC
// EN: Scans entire file byte by byte for known FourCCs
static void ScanForChunks(byte[] data)
{
    var known = new[] { "ucfb", "Locl", "NAME", "BODY", "INFO", "HEAD", "FTEX" };
    for (int i = 0; i + 4 <= data.Length; i++)
    {
        var fcc = Encoding.ASCII.GetString(data, i, 4);
        if (known.Contains(fcc))
        {
            var sizeOk = i + 8 <= data.Length;
            var size = sizeOk ? ReadUInt32(data, i + 4) : 0;
            Console.WriteLine($"  [{i:X6}] '{fcc}' size={size} (0x{size:X})");
        }
    }
}

static string ReadFourCC(byte[] data, int offset) =>
    offset + 4 <= data.Length
        ? Encoding.ASCII.GetString(data, offset, 4)
        : "????";

static uint ReadUInt32(byte[] data, int offset) =>
    offset + 4 <= data.Length
        ? BitConverter.ToUInt32(data, offset)
        : 0;

static string HexDump(byte[] data, int offset, int length)
{
    if (offset >= data.Length) return "(out of bounds)";
    var len = Math.Min(length, data.Length - offset);
    return string.Join(" ", Enumerable.Range(offset, len).Select(i => data[i].ToString("X2")));
}
