// =============================================================================
// BF1LocalizationTool.Core — Localization/LoclChunkParser.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Парсить бінарний Locl-чанк з .loc файлів Star Wars Battlefront II (2005).
//
//     Структура файлу (підтверджена hex-аналізом і diagnostic-утилітою):
//
//       [0x00] ucfb  [size]
//       [0x08] Locl  [size]
//       [0x10] NAME  [size=7]  "french\0"
//       [0x1F] 00               ← padding до кратного 4 (НЕ включений у size!)
//       [0x20] BODY  [size]
//       [0x28] записи...
//
//     КЛЮЧОВЕ: size поля НЕ включає alignment padding.
//     Після кожного чанку: наступний = align(dataEnd, 4) = (dataEnd + 3) & ~3
//
//     Формат кожного запису в BODY:
//       [uint32] hash           — 4 байти
//       [uint16] total_size     — повний розмір запису (кратне 4)
//                                 включає: hash(4) + size_field(2) + string + null + pad
//       [total_size-6 байт]    — UTF-16LE рядок + null(2) + padding
//
//     Приклади (підтверджено дампом):
//       "Never\0"  12б + 2pad → total=20  ✓
//       "Jamais\0" 14б + 4pad → total=24  ✓
//
// EN: Parses binary Locl chunk from .loc files of Star Wars Battlefront II (2005).
//     KEY: size field does NOT include alignment padding.
//     After each chunk: next = align(dataEnd, 4) = (dataEnd + 3) & ~3
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Models;
using System.Text;

namespace BF1LocalizationTool.Core.Localization;

public static class LoclChunkParser
{
    // =========================================================================
    // UA: РОБОТА З УЖЕ РОЗПАРСЕНИМ UcfbChunk-ДЕРЕВОМ (без повторного читання байтів)
    //     Використовується коли core.lvl уже прочитаний через UcfbReader і ми
    //     знаходимо "Locl" чанки прямо в дереві — без розпакування на диск
    //     і без сторонніх інструментів (swbf-unmunge більше не потрібен).
    // EN: WORKING WITH AN ALREADY-PARSED UcfbChunk TREE (no raw byte re-read)
    //     Used when core.lvl was already read via UcfbReader and we find
    //     "Locl" chunks directly in the tree — no disk extraction and
    //     no third-party tools needed (swbf-unmunge is no longer required).
    // =========================================================================

    // -------------------------------------------------------------------------
    // UA: Парсить мовний файл напряму з вже розпарсеного "Locl" чанку.
    //     Повертає файл локалізації та посилання на BODY-чанк (для запису
    //     назад). fallbackLanguage використовується якщо NAME-чанк відсутній
    //     або порожній. Повертає null якщо чанк не має BODY — це означає, що
    //     FourCC "Locl" випадково збігся з якимось іншим бінарним чанком
    //     (хибне спрацювання детекції FourCC, яка лише перевіряє друковані
    //     ASCII-байти). Такі чанки безпечно пропускаються, не зупиняючи
    //     завантаження файлу.
    // EN: Parses a language file directly from an already-parsed "Locl"
    //     chunk. Returns the localization file and a reference to the BODY
    //     chunk (for write-back). fallbackLanguage is used if the NAME chunk
    //     is missing or empty. Returns null if the chunk has no BODY —
    //     meaning the FourCC "Locl" coincidentally matched some other binary
    //     chunk (a false positive of FourCC detection, which only checks for
    //     printable ASCII bytes). Such chunks are safely skipped without
    //     stopping the file load.
    // -------------------------------------------------------------------------
    public static (LocalizationFile File, UcfbChunk BodyChunk)? ParseFromUcfbChunk(
        UcfbChunk loclChunk, string fallbackLanguage)
    {
        string languageName = fallbackLanguage;
        UcfbChunk? bodyChunk = null;

        foreach (var child in loclChunk.Children)
        {
            if (child.FourCC == "NAME")
            {
                var data = child.RawData;
                var nullIdx = Array.IndexOf(data, (byte)0);
                var len = nullIdx >= 0 ? nullIdx : data.Length;
                if (len > 0)
                    languageName = Encoding.ASCII.GetString(data, 0, len);
            }
            else if (child.FourCC == "BODY")
            {
                bodyChunk = child;
            }
        }

        // UA: Немає BODY — не справжній Locl-контейнер локалізації, пропускаємо
        // EN: No BODY — not a real localization Locl container, skip
        if (bodyChunk is null)
            return null;

        // UA: Перевикористовуємо вже наявний парсер BODY-даних,
        //     працюючи з RawData напряму (offset=0, length=весь масив)
        // EN: Reuse the existing BODY data parser, operating on
        //     RawData directly (offset=0, length=entire array)
        var entries = ParseBody(bodyChunk.RawData, 0, bodyChunk.RawData.Length);

        // UA: Базова структурна перевірка — без вмісту (зміст перевіряється
        //     окремо в LvlLocalizationService через звірку хешів з відомим
        //     plain-text файлом, бо це набагато надійніший сигнал ніж
        //     аналіз "друкованості" байтів)
        // EN: Basic structural check only — content validation happens
        //     separately in LvlLocalizationService via hash cross-check
        //     against a known plain-text file (much more reliable signal
        //     than analyzing byte "printability")
        if (entries.Count == 0)
            return null;

        var file = new LocalizationFile
        {
            LanguageName = languageName,
            Entries = entries
        };
        file.BuildIndex();

        return (file, bodyChunk);
    }

    // -------------------------------------------------------------------------
    // UA: Серіалізує лише вміст BODY (без обгортки ucfb/Locl/NAME) — для
    //     заміни даних BODY-чанку напряму через UcfbWriter.replacements.
    // EN: Serializes ONLY the BODY content (no ucfb/Locl/NAME wrapper) — for
    //     replacing BODY chunk data directly via UcfbWriter.replacements.
    // -------------------------------------------------------------------------
    public static byte[] BuildBodyBytes(LocalizationFile file, bool useTranslation = true)
    {
        using var bodyMs = new MemoryStream();
        using var bodyWriter = new BinaryWriter(bodyMs);

        foreach (var entry in file.Entries)
        {
            var text = useTranslation ? entry.ActiveText : entry.Original;
            var strBytes = Encoding.Unicode.GetBytes(text);
            var rawSize = 4 + 2 + strBytes.Length + 2;
            var padding = (4 - (rawSize % 4)) % 4;
            var total = (ushort)(rawSize + padding);

            bodyWriter.Write(entry.Hash);
            bodyWriter.Write(total);
            bodyWriter.Write(strBytes);
            bodyWriter.Write((ushort)0);
            for (int i = 0; i < padding; i++)
                bodyWriter.Write((byte)0);
        }

        return bodyMs.ToArray();
    }

    public static LocalizationFile LoadFromLocFile(string filePath)
    {
        var data = File.ReadAllBytes(filePath);
        var languageName = Path.GetFileNameWithoutExtension(filePath);
        return Parse(data, languageName);
    }

    public static LocalizationFile Parse(byte[] data, string languageName)
    {
        if (data.Length < 8)
            throw new InvalidDataException(
                "UA: Файл занадто малий / EN: File too small");

        var rootId = ReadFourCC(data, 0);
        if (rootId != "ucfb")
            throw new InvalidDataException(
                $"UA: Очікується 'ucfb', отримано '{rootId}' / EN: Expected 'ucfb', got '{rootId}'");

        var rootSize = (int)ReadUInt32(data, 4);
        var rootEnd = 8 + rootSize;

        // UA: Шукаємо Locl всередині ucfb
        // EN: Find Locl inside ucfb
        var pos = 8;
        while (pos + 8 <= rootEnd && pos + 8 <= data.Length)
        {
            var chunkId = ReadFourCC(data, pos);
            var chunkSize = (int)ReadUInt32(data, pos + 4);
            var chunkData = pos + 8;
            var chunkEnd = chunkData + chunkSize;

            if (chunkId == "Locl")
                return ParseLocl(data, chunkData, Math.Min(chunkEnd, data.Length), languageName);

            // UA: Вирівнювання після кожного чанку — size НЕ включає padding
            // EN: Align after each chunk — size does NOT include padding
            pos = (chunkEnd + 3) & ~3;
        }

        throw new InvalidDataException(
            "UA: Locl чанк не знайдено / EN: Locl chunk not found");
    }

    private static LocalizationFile ParseLocl(
        byte[] data, int start, int end, string languageName)
    {
        string? parsedName = null;
        List<LocalizationEntry>? entries = null;

        var pos = start;
        while (pos + 8 <= end && pos + 8 <= data.Length)
        {
            var subId = ReadFourCC(data, pos);
            var subSize = (int)ReadUInt32(data, pos + 4);
            var subData = pos + 8;
            var subEnd = subData + subSize;

            if (subId == "NAME")
            {
                // UA: Null-terminated ASCII назва мови
                // EN: Null-terminated ASCII language name
                var safeLen = Math.Min(subSize, data.Length - subData);
                var nullIdx = Array.IndexOf(data, (byte)0, subData, safeLen);
                var nameLen = nullIdx >= 0 ? nullIdx - subData : safeLen;
                if (nameLen > 0)
                    parsedName = Encoding.ASCII.GetString(data, subData, nameLen);
            }
            else if (subId == "BODY")
            {
                entries = ParseBody(data, subData, Math.Min(subSize, data.Length - subData));
            }

            // UA: КРИТИЧНО: вирівнювання до 4 байт після кожного підчанку.
            //     Підтверджено дампом: NAME size=7 ("french\0"), dataEnd=31,
            //     наступний чанк (BODY) починається на 32 = align(31, 4)
            // EN: CRITICAL: 4-byte alignment after each sub-chunk.
            //     Confirmed by dump: NAME size=7 ("french\0"), dataEnd=31,
            //     next chunk (BODY) starts at 32 = align(31, 4)
            pos = (subEnd + 3) & ~3;
        }

        if (entries is null)
            throw new InvalidDataException(
                "UA: BODY чанк не знайдено в Locl / EN: BODY chunk not found in Locl");

        if (parsedName is not null)
            languageName = parsedName;

        var file = new LocalizationFile
        {
            LanguageName = languageName,
            Entries = entries
        };
        file.BuildIndex();
        return file;
    }

    // -------------------------------------------------------------------------
    // UA: Перевіряє чи список записів є справжньою локалізацією — звіряючи
    //     частку хешів що збігаються з відомим набором хешів з plain-text
    //     файлу (наприклад "english"). Хеш-ID стабільні між форматами,
    //     тому справжній переклад використовує ТІ САМІ хеші. Випадковий
    //     бінарний "сміттєвий" чанк матиме практично випадкові 32-бітні
    //     числа — шанс масового збігу астрономічно малий.
    // EN: Checks whether a list of entries is genuine localization data —
    //     by cross-referencing the share of hashes matching a known hash
    //     set from a plain-text file (e.g. "english"). Hash IDs are stable
    //     across formats, so real translations use the SAME hashes. A
    //     random binary "garbage" chunk would have effectively random
    //     32-bit numbers — chance of mass overlap is astronomically low.
    // -------------------------------------------------------------------------
    public static bool IsLikelyRealLocalization(
        List<LocalizationEntry> entries, HashSet<uint> knownHashes, double threshold = 0.5)
    {
        // UA: Якщо немає відомого набору хешів для звірки — не можемо
        //     перевірити, тому довіряємо (краще пропустити сумнівне,
        //     ніж відфільтрувати справжнє)
        // EN: If there's no known hash set to cross-check against — we
        //     can't verify, so we trust it (better to let through something
        //     questionable than filter out something real)
        if (knownHashes.Count == 0) return true;

        var matchCount = entries.Count(e => knownHashes.Contains(e.Hash));
        var ratio = (double)matchCount / entries.Count;

        return ratio >= threshold;
    }

    private static List<LocalizationEntry> ParseBody(byte[] data, int start, int size)
    {
        var entries = new List<LocalizationEntry>();
        var pos = start;
        var end = start + size;

        while (pos + 6 <= end && pos + 6 <= data.Length)
        {
            var hash = ReadUInt32(data, pos);
            var totalSize = (int)ReadUInt16(data, pos + 4);

            // UA: total_size = повний розмір запису, кратне 4
            // EN: total_size = full entry size, multiple of 4
            if (totalSize < 6 || totalSize % 4 != 0)
                break;

            var payloadStart = pos + 6;
            var payloadSize = totalSize - 6;

            if (payloadStart + payloadSize > data.Length)
                break;

            // UA: Шукаємо UTF-16LE null terminator у payload
            // EN: Find UTF-16LE null terminator in payload
            var nullPos = FindUtf16Null(data, payloadStart, payloadSize);
            var text = nullPos > 0
                ? Encoding.Unicode.GetString(data, payloadStart, nullPos)
                : string.Empty;

            entries.Add(new LocalizationEntry { Hash = hash, Original = text });

            pos += totalSize;
        }

        return entries;
    }

    public static byte[] Serialize(LocalizationFile file, bool useTranslation = true)
    {
        // UA: BODY
        // EN: BODY
        using var bodyMs = new MemoryStream();
        using var bodyWriter = new BinaryWriter(bodyMs);

        foreach (var entry in file.Entries)
        {
            var text = useTranslation ? entry.ActiveText : entry.Original;
            var strBytes = Encoding.Unicode.GetBytes(text);
            var rawSize = 4 + 2 + strBytes.Length + 2; // hash + size_field + str + null
            var padding = (4 - (rawSize % 4)) % 4;
            var total = (ushort)(rawSize + padding);

            bodyWriter.Write(entry.Hash);
            bodyWriter.Write(total);
            bodyWriter.Write(strBytes);
            bodyWriter.Write((ushort)0);
            for (int i = 0; i < padding; i++)
                bodyWriter.Write((byte)0);
        }

        var bodyData = bodyMs.ToArray();

        // UA: NAME — вирівнюємо до 4 байт (padding НЕ включається у size)
        // EN: NAME — align to 4 bytes (padding NOT included in size)
        var nameRaw = Encoding.ASCII.GetBytes(file.LanguageName + "\0");
        var nameSize = nameRaw.Length; // UA: реальний розмір без padding / EN: real size without padding
        var namePadded = new byte[(nameSize + 3) & ~3];
        Array.Copy(nameRaw, namePadded, nameSize);

        // UA: Locl = NAME + BODY
        // EN: Locl = NAME + BODY
        using var loclMs = new MemoryStream();
        using var loclWriter = new BinaryWriter(loclMs);

        loclWriter.Write(Encoding.ASCII.GetBytes("NAME"));
        loclWriter.Write((uint)nameSize);          // UA: лише реальний розмір / EN: real size only
        loclWriter.Write(namePadded);              // UA: з padding байтами / EN: with padding bytes

        loclWriter.Write(Encoding.ASCII.GetBytes("BODY"));
        loclWriter.Write((uint)bodyData.Length);
        loclWriter.Write(bodyData);

        var loclData = loclMs.ToArray();

        // UA: ucfb = Locl
        // EN: ucfb = Locl
        using var rootMs = new MemoryStream();
        using var rootWriter = new BinaryWriter(rootMs);

        rootWriter.Write(Encoding.ASCII.GetBytes("ucfb"));

        using var wrapMs = new MemoryStream();
        using var wrapWriter = new BinaryWriter(wrapMs);
        wrapWriter.Write(Encoding.ASCII.GetBytes("Locl"));
        wrapWriter.Write((uint)loclData.Length);
        wrapWriter.Write(loclData);
        var wrapData = wrapMs.ToArray();

        rootWriter.Write((uint)wrapData.Length);
        rootWriter.Write(wrapData);

        return rootMs.ToArray();
    }

    public static void SaveToLocFile(LocalizationFile file, string filePath,
        bool useTranslation = true)
    {
        var data = Serialize(file, useTranslation);
        var tempPath = filePath + ".tmp";
        File.WriteAllBytes(tempPath, data);
        File.Move(tempPath, filePath, overwrite: true);
    }

    // -------------------------------------------------------------------------
    // UA: Низькорівневі читачі
    // EN: Low-level readers
    // -------------------------------------------------------------------------

    private static string ReadFourCC(byte[] data, int offset) =>
        offset + 4 <= data.Length
            ? Encoding.ASCII.GetString(data, offset, 4)
            : "????";

    private static uint ReadUInt32(byte[] data, int offset) =>
        BitConverter.ToUInt32(data, offset);

    private static ushort ReadUInt16(byte[] data, int offset) =>
        BitConverter.ToUInt16(data, offset);

    private static int FindUtf16Null(byte[] data, int start, int maxLen)
    {
        var end = Math.Min(start + maxLen, data.Length - 1);
        for (var i = start; i < end; i += 2)
            if (data[i] == 0 && data[i + 1] == 0)
                return i - start;
        return maxLen;
    }
}