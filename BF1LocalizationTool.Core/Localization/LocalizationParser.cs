// =============================================================================
// BF1LocalizationTool.Core — Localization/LocalizationParser.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Парсить локалізаційні чанки з розпакованого ucfb-дерева.
//
//     Структура у core.lvl:
//       [хеш-чанк "localization"]
//         [хеш-чанк "english"]
//           NAME → "english"
//           DATA → текст у форматі "0xHASH рядок\n"
//         [хеш-чанк "french"]
//           ...
//
//     swbf-unmunge підтвердив що після розпакування отримуємо plain-text файли —
//     тобто дані чанку зберігаються як ASCII текст безпосередньо в DATA.
//
// EN: Parses localization chunks from an extracted ucfb tree.
//
//     Structure in core.lvl:
//       [hash-chunk "localization"]
//         [hash-chunk "english"]
//           NAME → "english"
//           DATA → text in format "0xHASH string\n"
//         [hash-chunk "french"]
//           ...
//
//     swbf-unmunge confirmed that extracted output is plain-text files —
//     meaning chunk data is stored as ASCII text directly in DATA.
// =============================================================================

// =============================================================================
// UA: ВАЖЛИВО ПРО КОДУВАННЯ / IMPORTANT ABOUT ENCODING
// =============================================================================
// UA: Plain-text .txt чанки локалізації (BF1 і частково BF2) — однобайтове
//     кодування Latin1 (ISO-8859-1), що зберігає © é та інші західноєвропейські
//     символи коректно (на відміну від чистого ASCII, який давав "??").
//
//     ОБМЕЖЕННЯ: Latin1 НЕ підтримує кирилицю. Якщо в перекладі є українські
//     літери, вони будуть втрачені/замінені на '?' при записі в .txt формат.
//     Бінарний .loc формат (BF2, LoclChunkParser.cs) використовує UTF-16LE
//     і коректно зберігає будь-який Unicode, включно з кирилицею — це
//     єдиний наразі повністю Unicode-безпечний канал локалізації.
//
//     Для BF1 (де є лише .txt формат) повноцінна кирилична локалізація
//     вимагає окремої роботи над шрифтовим атласом (як було зроблено
//     для SteamWorld Heist) — заміни гліфів і відповідного байт-мапінгу.
//
// EN: Plain-text .txt localization chunks (BF1 and partly BF2) use single-byte
//     Latin1 (ISO-8859-1) encoding, which preserves © é and other Western
//     European characters correctly (unlike pure ASCII, which produced "??").
//
//     LIMITATION: Latin1 does NOT support Cyrillic. If translation contains
//     Ukrainian letters, they will be lost/replaced with '?' when written
//     to .txt format. The binary .loc format (BF2, LoclChunkParser.cs) uses
//     UTF-16LE and correctly stores any Unicode, including Cyrillic — this
//     is currently the only fully Unicode-safe localization channel.
//
//     For BF1 (which only has .txt format), full Cyrillic localization
//     requires separate work on the font glyph atlas (similar to what was
//     done for SteamWorld Heist) — glyph replacement and matching byte mapping.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace BF1LocalizationTool.Core.Localization;

public static partial class LocalizationParser
{
    // UA: Регулярний вираз для рядка локалізації: "0xHHHHHHHH текст"
    // EN: Regex for a localization line: "0xHHHHHHHH text"
    [GeneratedRegex(@"^0x([0-9a-fA-F]{8})\s+(.+)$", RegexOptions.Compiled)]
    private static partial Regex EntryPattern();

    // UA: Відомі назви мовних файлів у BF1
    // EN: Known language file names in BF1
    public static readonly IReadOnlyList<string> KnownLanguages =
    [
        "english", "french", "german", "italian", "spanish", "uk_english"
    ];

    // -------------------------------------------------------------------------
    // UA: Знаходить і парсить всі локалізаційні файли з дерева чанків.
    //     Повертає словник: назва мови → LocalizationFile.
    //     Якщо localPair — пара (оригінал, переклад) для порівняння.
    // EN: Finds and parses all localization files from the chunk tree.
    //     Returns dictionary: language name → LocalizationFile.
    // -------------------------------------------------------------------------
    public static Dictionary<string, (LocalizationFile File, UcfbChunk DataChunk)> ParseAll(UcfbChunk root)
    {
        var result = new Dictionary<string, (LocalizationFile, UcfbChunk)>(StringComparer.OrdinalIgnoreCase);

        // UA: Шукаємо всі чанки з NAME що відповідає відомим мовам
        // EN: Search for all chunks whose NAME matches known languages
        var nameCandidates = FindLocalizationNameChunks(root);

        foreach (var (languageName, dataChunk) in nameCandidates)
        {
            var file = ParseDataChunk(languageName, dataChunk);
            if (file is not null)
                result[languageName] = (file, dataChunk);
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // UA: Парсить один мовний файл з DATA-чанку.
    //     Повертає null якщо вміст не відповідає формату локалізації.
    // EN: Parses one language file from a DATA chunk.
    //     Returns null if content does not match localization format.
    // -------------------------------------------------------------------------
    public static LocalizationFile? ParseDataChunk(string languageName, UcfbChunk dataChunk)
    {
        string text;

        try
        {
            // UA: Дані зберігаються як ASCII (підтверджено swbf-unmunge)
            // EN: Data is stored as ASCII (confirmed by swbf-unmunge)
            text = Encoding.Latin1.GetString(dataChunk.RawData).TrimEnd('\0');
        }
        catch
        {
            return null;
        }

        var entries = ParseText(text);
        if (entries.Count == 0)
            return null;

        var file = new LocalizationFile
        {
            LanguageName = languageName,
            Entries = entries
        };
        file.BuildIndex();
        return file;
    }

    // -------------------------------------------------------------------------
    // UA: Парсить текст у форматі "0xHASH рядок\n" у список записів.
    //     Рядки що не відповідають формату — ігноруються.
    // EN: Parses text in "0xHASH string\n" format into a list of entries.
    //     Lines not matching the format are ignored.
    // -------------------------------------------------------------------------
    public static List<LocalizationEntry> ParseText(string text)
    {
        var entries = new List<LocalizationEntry>();
        var pattern = EntryPattern();

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.TrimEnd('\r');
            var match = pattern.Match(trimmed);
            if (!match.Success) continue;

            var hash = Convert.ToUInt32(match.Groups[1].Value, 16);
            var value = match.Groups[2].Value;

            entries.Add(new LocalizationEntry
            {
                Hash = hash,
                Original = value
            });
        }

        return entries;
    }

    // -------------------------------------------------------------------------
    // UA: Серіалізує LocalizationFile назад у текстовий формат BF1.
    //     useTranslation=true — записує переклад (якщо є), інакше оригінал.
    // EN: Serializes LocalizationFile back to BF1 text format.
    //     useTranslation=true — writes translation (if any), else original.
    // -------------------------------------------------------------------------
    public static string SerializeToText(LocalizationFile file, bool useTranslation = true)
    {
        var sb = new StringBuilder(file.Entries.Count * 60);

        foreach (var entry in file.Entries)
            sb.AppendLine(entry.Serialize(useTranslation));

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // UA: Серіалізує у байти ASCII для запису назад у чанк.
    //     Розмір може відрізнятись від оригінального — UcfbWriter це враховує.
    // EN: Serializes to ASCII bytes for writing back to chunk.
    //     Size may differ from original — UcfbWriter handles this.
    // -------------------------------------------------------------------------
    public static byte[] SerializeToBytes(LocalizationFile file, bool useTranslation = true)
    {
        var text = SerializeToText(file, useTranslation);
        return Encoding.Latin1.GetBytes(text);
    }

    // -------------------------------------------------------------------------
    // UA: Завантажує LocalizationFile з plain-text .txt файлу
    //     (для роботи з файлами що вже витягнуті swbf-unmunge)
    // EN: Loads LocalizationFile from a plain-text .txt file
    //     (for working with files already extracted by swbf-unmunge)
    // -------------------------------------------------------------------------
    public static LocalizationFile LoadFromTextFile(string filePath)
    {
        var languageName = Path.GetFileNameWithoutExtension(filePath);
        var text = File.ReadAllText(filePath, Encoding.Latin1);
        var entries = ParseText(text);

        var file = new LocalizationFile
        {
            LanguageName = languageName,
            Entries = entries
        };
        file.BuildIndex();
        return file;
    }

    // -------------------------------------------------------------------------
    // UA: Зберігає LocalizationFile у plain-text .txt файл
    // EN: Saves LocalizationFile to a plain-text .txt file
    // -------------------------------------------------------------------------
    public static void SaveToTextFile(LocalizationFile file, string filePath,
        bool useTranslation = true)
    {
        var text = SerializeToText(file, useTranslation);
        File.WriteAllText(filePath, text, Encoding.Latin1);
    }

    // -------------------------------------------------------------------------
    // UA: Внутрішній метод: обходить дерево чанків і знаходить пари
    //     (languageName, dataChunk) для відомих мов.
    //     Структура: NAME-чанк містить назву мови,
    //                наступний чанк містить дані (або це батьківський DATA-чанк).
    // EN: Internal: traverses chunk tree and finds pairs
    //     (languageName, dataChunk) for known languages.
    //     Structure: NAME chunk contains language name,
    //                next chunk contains data (or parent DATA chunk).
    // -------------------------------------------------------------------------
    private static List<(string Language, UcfbChunk DataChunk)>
        FindLocalizationNameChunks(UcfbChunk root)
    {
        var result = new List<(string, UcfbChunk)>();
        SearchInChunk(root, result);
        return result;
    }

    private static void SearchInChunk(
        UcfbChunk chunk,
        List<(string, UcfbChunk)> result)
    {
        // UA: КРИТИЧНО: ніколи не заходимо всередину "Locl" чанків — це бінарний
        //     UTF-16LE формат (BF2), а не plain-text. Його NAME+BODY структура
        //     теж містить назву мови, тому без цього захисту ми б помилково
        //     прийняли бінарні дані за ASCII текст і отримали "сміття".
        //     Бінарні Locl-чанки обробляються окремо в LoclChunkParser.
        // EN: CRITICAL: never recurse into "Locl" chunks — that's binary
        //     UTF-16LE format (BF2), not plain-text. Its NAME+BODY structure
        //     also contains a language name, so without this guard we'd
        //     mistakenly treat binary data as ASCII text and get "garbage".
        //     Binary Locl chunks are handled separately in LoclChunkParser.
        if (chunk.FourCC == "Locl")
            return;

        // UA: Шукаємо серед дочірніх: NAME-чанк з відомою мовою,
        //     після якого йде DATA-чанк з текстом
        // EN: Search among children: NAME chunk with known language,
        //     followed by DATA chunk with text
        for (int i = 0; i < chunk.Children.Count; i++)
        {
            var child = chunk.Children[i];

            if (child.FourCC == "NAME" && i + 1 < chunk.Children.Count)
            {
                var name = ReadNameString(child);
                if (KnownLanguages.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    var dataChunk = chunk.Children[i + 1];
                    result.Add((name, dataChunk));
                    continue;
                }
            }

            // UA: Рекурсивний пошук вглиб
            // EN: Recursive search deeper
            SearchInChunk(child, result);
        }
    }

    // UA: Читає null-terminated ASCII рядок з NAME-чанку
    // EN: Reads null-terminated ASCII string from NAME chunk
    private static string ReadNameString(UcfbChunk nameChunk)
    {
        var data = nameChunk.RawData;
        var nullIdx = Array.IndexOf(data, (byte)0);
        var length = nullIdx >= 0 ? nullIdx : data.Length;
        return Encoding.ASCII.GetString(data, 0, length);
    }
}
