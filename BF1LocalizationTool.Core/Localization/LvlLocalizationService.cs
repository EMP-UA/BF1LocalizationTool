// =============================================================================
// BF1LocalizationTool.Core — Localization/LvlLocalizationService.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Фасад високого рівня для роботи з локалізацією .lvl файлу.
//
//     ПОВНІСТЮ САМОДОСТАТНІЙ ПАЙПЛАЙН — без сторонніх інструментів
//     (swbf-unmunge більше не потрібен), без проміжних файлів на диску.
//     Читання й запис відбуваються прямо в пам'яті через UcfbReader/UcfbWriter,
//     для BF1 (.txt) і BF2 (.txt + бінарний .loc) однаковим механізмом:
//     знаходимо потрібний чанк у дереві → будуємо нові байти → підміняємо
//     за зміщенням файлу.
//
//     Версія гри визначається автоматично за наявністю "Locl" чанків
//     у дереві (не за шляхом до файлу і не за наявністю папки на диску).
//
// EN: High-level facade for working with .lvl file localization.
//
//     FULLY SELF-CONTAINED PIPELINE — no third-party tools required
//     (swbf-unmunge is no longer needed), no intermediate files on disk.
//     Reading and writing happen entirely in memory via UcfbReader/UcfbWriter,
//     using the same mechanism for BF1 (.txt) and BF2 (.txt + binary .loc):
//     find the target chunk in the tree → build new bytes → replace
//     at the file offset.
//
//     Game version is detected automatically by presence of "Locl" chunks
//     in the tree (not by file path and not by presence of a disk folder).
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Models;
using System.Text;

namespace BF1LocalizationTool.Core.Localization;

// UA: Версія гри — впливає лише на UI-підказки, не на логіку читання/запису
//     (вона уже уніфікована для обох ігор)
// EN: Game version — affects only UI hints, not read/write logic
//     (which is already unified for both games)
public enum GameVersion { BF1, BF2 }

// UA: Тип сховища мовного файлу — визначає яким серіалізатором користуватись
// EN: Language file storage type — determines which serializer to use
public enum LocalizationFileType { PlainText, LoclBinary }

// UA: Внутрішній запис: файл локалізації + посилання на чанк для запису назад
// EN: Internal record: localization file + chunk reference for write-back
internal record LanguageFileRecord(
    LocalizationFile File,
    LocalizationFileType FileType,
    UcfbChunk ReplaceChunk);

public class LvlLocalizationService
{
    public GameVersion GameVersion { get; private set; } = GameVersion.BF1;

    private UcfbChunk? _root;
    private Dictionary<string, LanguageFileRecord> _records = [];

    // =========================================================================
    // UA: ЗАВАНТАЖЕННЯ — повністю в пам'яті, без диска
    // EN: LOADING — fully in memory, no disk
    // =========================================================================
    public async Task LoadAsync(string lvlFilePath)
    {
        _records = [];

        var data = await Task.Run(() => File.ReadAllBytes(lvlFilePath));
        _root = await Task.Run(() => UcfbReader.ReadFile(data));

        // UA: Plain-text мови (.txt-формат) — спільні для BF1 і BF2
        // EN: Plain-text languages (.txt format) — shared by BF1 and BF2
        var plainTextLanguages = await Task.Run(() => LocalizationParser.ParseAll(_root));
        foreach (var (lang, pair) in plainTextLanguages)
            _records[lang] = new LanguageFileRecord(pair.File, LocalizationFileType.PlainText, pair.DataChunk);

        // UA: Набір відомих хешів для відсіювання хибних Locl-чанків.
        //     BF1 і BF2 використовують однакові хеші у plain-text і бінарному
        //     форматах — справжній Locl дасть ~100% збігів, випадковий бінарник — 0%.
        // EN: Known hash set for filtering false-positive Locl chunks.
        //     BF1 and BF2 use identical hashes in plain-text and binary formats —
        //     a real Locl gives ~100% matches, random binary gives ~0%.
        var knownHashes = new HashSet<uint>();
        if (plainTextLanguages.Count > 0)
        {
            var refFile = plainTextLanguages.TryGetValue("english", out var ep)
                ? ep.File
                : plainTextLanguages.First().Value.File;
            foreach (var e in refFile.Entries)
                knownHashes.Add(e.Hash);
        }

        // UA: Бінарні Locl-чанки (.loc формат) — лише BF2.
        //     ВАЖЛИВО: GameVersion визначається після фільтрації — не за
        //     наявністю будь-якого чанку з FourCC "Locl" у дереві (це може
        //     бути випадковий збіг), а за наявністю реальних мовних записів
        //     що пройшли перевірку хешів.
        // EN: Binary Locl chunks (.loc format) — BF2 only.
        //     IMPORTANT: GameVersion is determined AFTER filtering — not by
        //     mere presence of any "Locl" FourCC chunk in the tree (could be
        //     coincidental), but by presence of real language entries that
        //     passed the hash cross-check.
        var loclChunks = await Task.Run(() => UcfbReader.FindAll(_root, "Locl").ToList());
        var realLoclCount = 0;

        for (var loclIndex = 0; loclIndex < loclChunks.Count; loclIndex++)
        {
            var loclChunk = loclChunks[loclIndex];
            var parsed = await Task.Run(() =>
                LoclChunkParser.ParseFromUcfbChunk(loclChunk, $"loc_{loclIndex}"));

            if (parsed is null) continue;

            var (file, bodyChunk) = parsed.Value;

            // UA: Ключова перевірка: якщо knownHashes непорожній і менше
            //     половини хешів збігаються — це не реальна локалізація
            // EN: Key check: if knownHashes is non-empty and less than
            //     half the hashes match — this is not real localization
            if (knownHashes.Count > 0 &&
                !LoclChunkParser.IsLikelyRealLocalization(file.Entries, knownHashes))
                continue;

            var key = file.LanguageName + "_loc";
            _records[key] = new LanguageFileRecord(file, LocalizationFileType.LoclBinary, bodyChunk);
            realLoclCount++;
        }

        // UA: GameVersion визначається за шляхом до файлу — надійніше ніж
        //     рахувати Locl чанки (обидві гри використовують Locl формат).
        //     "Battlefront II" в шляху = BF2, інакше = BF1.
        // EN: GameVersion determined by file path — more reliable than
        //     counting Locl chunks (both games use Locl format).
        //     "Battlefront II" in path = BF2, otherwise = BF1.
        GameVersion = lvlFilePath.Contains("Battlefront II", StringComparison.OrdinalIgnoreCase)
            ? GameVersion.BF2
            : GameVersion.BF1;
    }

    public IReadOnlyList<string> AvailableLanguages =>
        _records.Keys.OrderBy(x => x).ToList();

    public LocalizationFile? GetLanguageFile(string language) =>
        _records.TryGetValue(language, out var rec) ? rec.File : null;

    public LocalizationFileType? GetFileType(string language) =>
        _records.TryGetValue(language, out var rec) ? rec.FileType : null;

    // =========================================================================
    // UA: ЗБЕРЕЖЕННЯ — єдиний механізм для BF1 і BF2.
    //     outputPath — шлях обраний користувачем (НІКОЛИ не оригінал гри,
    //     якщо тільки сам користувач свідомо туди не вказав).
    //     targetLanguage — яку саме мову замінити (null = всі завантажені).
    // EN: SAVING — unified mechanism for BF1 and BF2.
    //     outputPath — user-chosen path (NEVER the game's original,
    //     unless the user explicitly points there themselves).
    //     targetLanguage — which language to replace (null = all loaded).
    // =========================================================================
    public async Task SaveAsync(string outputPath, string? targetLanguage = null)
    {
        if (_root is null)
            throw new InvalidOperationException(
                "UA: Файл не завантажено / EN: File not loaded");

        var replacements = new Dictionary<long, byte[]>();

        foreach (var (lang, rec) in _records)
        {
            if (targetLanguage is not null &&
                !lang.Equals(targetLanguage, StringComparison.OrdinalIgnoreCase))
                continue;

            var newBytes = rec.FileType == LocalizationFileType.PlainText
                ? LocalizationParser.SerializeToBytes(rec.File, useTranslation: true)
                : LoclChunkParser.BuildBodyBytes(rec.File, useTranslation: true);

            replacements[rec.ReplaceChunk.FileDataOffset] = newBytes;
        }

        await Task.Run(() => UcfbWriter.WriteFile(outputPath, _root, replacements));
    }

    // =========================================================================
    // UA: ЕКСПОРТ / ІМПОРТ CSV
    // EN: EXPORT / IMPORT CSV
    // =========================================================================
    public async Task ExportCsvAsync(string language, string csvPath)
    {
        var file = GetLanguageFile(language)
            ?? throw new ArgumentException(
                $"UA: Мова '{language}' не знайдена / EN: Language '{language}' not found");

        var sb = new StringBuilder();
        sb.AppendLine("Hash,Original,Translation");

        foreach (var entry in file.Entries)
        {
            var original = entry.Original.Replace("\"", "\"\"");
            var translation = (entry.Translation ?? string.Empty).Replace("\"", "\"\"");
            sb.AppendLine($"0x{entry.Hash:x8},\"{original}\",\"{translation}\"");
        }

        await File.WriteAllTextAsync(csvPath, sb.ToString(), Encoding.UTF8);
    }

    public async Task ImportCsvAsync(string targetLanguage, string csvPath)
    {
        var file = GetLanguageFile(targetLanguage)
            ?? throw new ArgumentException(
                $"UA: Мова '{targetLanguage}' не знайдена / EN: Language '{targetLanguage}' not found");

        var lines = await File.ReadAllLinesAsync(csvPath, Encoding.UTF8);
        var imported = 0;

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = SplitCsvLine(line);
            if (parts.Length < 3) continue;
            if (!TryParseHash(parts[0], out var hash)) continue;

            var translation = parts[2].Trim();
            if (string.IsNullOrWhiteSpace(translation)) continue;

            var entry = file.GetByHash(hash);
            if (entry is null) continue;

            entry.Translation = translation;
            imported++;
        }
    }

    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                { current.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            { result.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }

        result.Add(current.ToString());
        return [.. result];
    }

    private static bool TryParseHash(string s, out uint hash)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out hash);
        return uint.TryParse(s, out hash);
    }
}
