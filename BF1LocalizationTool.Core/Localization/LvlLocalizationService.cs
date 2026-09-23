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
//     у дереві (не за шляхом до файлу і не за наявністю теки на диску).
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
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Models;

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

    // UA: Таблиця кирилиця↔байт-код і мова, до якої вона застосована.
    //     СВІДОМО не встановлюється автоматично при LoadAsync — викликач
    //     (GUI) вирішує сам, викликаючи ApplyCyrillicCodeTable() ЯВНО, якщо
    //     поруч із відкритим файлом знайдено файл-супутник. Diagnostic-
    //     команди цей метод ніколи не викликають, тож їхній аналіз сирих
    //     байтів лишається без змін. Див. заголовок CyrillicCodeTable.cs.
    // EN: Cyrillic↔byte-code table and the language it's applied to.
    //     DELIBERATELY not set automatically by LoadAsync — the caller
    //     (GUI) decides by calling ApplyCyrillicCodeTable() EXPLICITLY, if
    //     a sidecar file is found next to the opened file. Diagnostic
    //     commands never call this method, so their raw-byte analysis
    //     stays unchanged. See CyrillicCodeTable.cs header.
    private CyrillicCodeTable? _codeTable;
    private string? _codeTableTargetLanguage;

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

    // UA: true, якщо для поточного файлу застосовано таблицю кирилиця↔
    //     байт-код (тобто знайдено й підвантажено файл-супутник
    //     cyrillic-code-table.json). Використовується GUI, щоб вирішити,
    //     чи показувати попередження "кирилиця не підтримується".
    // EN: true if a Cyrillic↔byte-code table has been applied to the
    //     current file (i.e. a cyrillic-code-table.json sidecar was found
    //     and loaded). Used by the GUI to decide whether to show the
    //     "Cyrillic not supported" warning.
    public bool HasCyrillicCodeTable => _codeTable is not null;

    // UA: true, якщо ШРИФТИ цього core.lvl РЕАЛЬНО містять кириличні гліфи
    //     (є FBOD-записи з кодами в діапазоні Unicode-кирилиці 0x0400-0x04FF).
    //     Це прямий ЕМПІРИЧНИЙ сигнал "гра відмалює кирилицю", на відміну від
    //     HasCyrillicCodeTable, який відображав лише ЗАСТАРІЛИЙ донорський
    //     підхід (кирилиця зберігалась як перепризначені байт-коди 0-255 +
    //     файл-таблиця). У новому підході БЕЗ донорів
    //     (GenerateNoDonorCyrillicCoreCommand) гліфи лежать під СВОЇМИ
    //     справжніми Unicode-кодами і файл-таблиця не потрібна — тож саме
    //     ЦЯ перевірка (а не HasCyrillicCodeTable) має керувати попередженням
    //     "шрифт без кириличних гліфів" при збереженні: інакше коректний
    //     no-donor білд давав би хибне попередження.
    // EN: true if THIS core.lvl's FONTS actually contain Cyrillic glyphs
    //     (FBOD records with codes in the Unicode Cyrillic range
    //     0x0400-0x04FF). This is a direct EMPIRICAL "the game will render
    //     Cyrillic" signal, unlike HasCyrillicCodeTable which only reflected
    //     the LEGACY donor approach (Cyrillic stored as remapped byte-codes
    //     0-255 + a sidecar table). In the new NO-donor approach
    //     (GenerateNoDonorCyrillicCoreCommand) glyphs live at their REAL
    //     Unicode codes with no sidecar — so it's THIS check (not
    //     HasCyrillicCodeTable) that should drive the "font has no Cyrillic
    //     glyphs" save warning: otherwise a correct no-donor build would
    //     raise a false warning.
    public bool FontHasCyrillicGlyphs()
    {
        if (_root is null) return false;
        foreach (var font in FontChunkLocator.FindAll(_root))
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;
            var records = FontGlyphTable.Parse(fbod.RawData);
            if (records.Any(r => r.Code is >= 0x0400 and <= 0x04FF))
                return true;
        }
        return false;
    }

    // =========================================================================
    // UA: Скільки шрифтових ресурсів (gamefont_*) НЕСЕ САМ цей файл.
    //
    //     Потрібно, щоб відрізнити "файл базової гри" (несе 5–6 шрифтів)
    //     від "файлу аддону" (несе РІВНО 0 — підтверджено побайтово на
    //     vanilla GameData\AddOn\Tat3\...\core.lvl: 0 FBOD, 0 FTEX і навіть
    //     ЖОДНОЇ згадки імені gamefont_* у даних). Це не випадковість, а
    //     вимога формату: аддон довантажується ПОВЕРХ уже завантаженої
    //     базової гри, і другий комплект ресурсів з тими самими іменами —
    //     конфлікт. Тому запис шрифтів у файл аддону — це не "надлишок",
    //     а поломка.
    // EN: How many font resources (gamefont_*) THIS file itself carries.
    //
    //     Needed to tell a "base game file" (carries 5–6 fonts) from an
    //     "add-on file" (carries EXACTLY 0 — verified byte-level on the
    //     vanilla GameData\AddOn\Tat3\...\core.lvl: 0 FBOD, 0 FTEX and not
    //     even a single gamefont_* name mentioned in its data). That's not
    //     incidental but a format requirement: an add-on is loaded ON TOP
    //     of the already-loaded base game, and a second set of resources
    //     under the same names is a conflict. So writing fonts into an
    //     add-on file isn't "redundant" — it's breakage.
    // =========================================================================
    public int FontResourceCount() =>
        _root is null ? 0 : FontChunkLocator.FindAll(_root).Count();

    // =========================================================================
    // UA: Множина хешів рядків локалізації для мови — "відбиток документа".
    //     Переклад на неї НЕ впливає (хеш прив'язаний до РЯДКА-ключа, не до
    //     тексту), тож розбіжність множин означає саме "це різні файли",
    //     а не "по-різному перекладено". Використовується GUI, щоб не дати
    //     зберегти суміш "Оригінал з аддону + Робочий з базової гри".
    // EN: The set of localization string hashes for a language — a
    //     "document fingerprint". Translation does NOT affect it (the hash
    //     is tied to the string KEY, not its text), so a set mismatch means
    //     "these are different files", not "translated differently". Used
    //     by the GUI to refuse saving a mix of "Original from the add-on +
    //     Working from the base game".
    // =========================================================================
    public HashSet<uint> GetHashSet(string language)
    {
        var file = GetLanguageFile(language);
        return file is null
            ? new HashSet<uint>()
            : file.Entries.Select(e => e.Hash).ToHashSet();
    }

    // =========================================================================
    // UA: ПЕРЕСАДКА ШРИФТІВ З ОРИГІНАЛУ В РОБОЧИЙ ФАЙЛ.
    //
    //     ПРИЧИНА: SaveAsync серіалізує _root РОБОЧОГО файлу ЦІЛКОМ,
    //     підмінюючи лише локалізаційні чанки. Отже в гру їхали б шрифти З
    //     РОБОЧОГО файлу, а не з (перегенерованого) оригіналу. Якщо
    //     робочий файл відкрито через "Відкрити робочий" (а його діалог за
    //     замовчуванням веде саме в output\ — теку РАНІШЕ ЗБЕРЕЖЕНИХ
    //     файлів), користувач мовчки отримував би СТАРІ шрифти, скільки б
    //     разів не перегенеровував.
    //
    //     ЩО РОБИТЬ: копіює ДІТЕЙ кожного шрифтового чанка з source у
    //     однойменний (BaseName) шрифтовий чанк цього файлу. Той самий
    //     прийом мутації дерева, що вже перевірено працює в
    //     GenerateNoDonorCyrillicCoreCommand / GenerateEnlargedFontCoreCommand
    //     (font.Chunk.Children.Clear/AddRange + UcfbWriter серіалізує вже
    //     ЗМІНЕНЕ дерево). Локалізаційні чанки НЕ чіпаються — текст
    //     лишається той, що в робочому файлі.
    //
    //     Повертає кількість фактично пересаджених шрифтів (0 = не було
    //     що пересаджувати або source порожній), щоб викликач міг це
    //     показати в статусі, а не мовчати.
    // EN: TRANSPLANT FONTS FROM THE ORIGINAL INTO THE WORKING FILE.
    //
    //     REASON: SaveAsync serializes the WORKING file's _root in FULL,
    //     replacing only the localization chunks. So the fonts shipping to
    //     the game would come FROM THE WORKING FILE, not from the
    //     (regenerated) original. If the working file was opened via "Open
    //     working" (whose dialog defaults to output\ — the folder of
    //     PREVIOUSLY SAVED files), the user would silently get OLD fonts no
    //     matter how many times they regenerated.
    //
    //     WHAT IT DOES: copies the CHILDREN of every font chunk from
    //     source into the same-named (BaseName) font chunk of this file.
    //     The same tree-mutation technique already proven in
    //     GenerateNoDonorCyrillicCoreCommand / GenerateEnlargedFontCoreCommand
    //     (font.Chunk.Children.Clear/AddRange + UcfbWriter serializes the
    //     ALREADY-MODIFIED tree). Localization chunks are untouched — the
    //     text stays whatever the working file holds.
    //
    //     Returns how many fonts were actually transplanted (0 = nothing
    //     to transplant, or an empty source), so the caller can surface it
    //     in the status bar instead of staying silent.
    // =========================================================================
    public int AdoptFontsFrom(LvlLocalizationService source)
    {
        if (_root is null || source._root is null) return 0;

        var sourceFonts = FontChunkLocator.FindAll(source._root)
            .ToDictionary(f => f.BaseName, StringComparer.OrdinalIgnoreCase);
        if (sourceFonts.Count == 0) return 0;

        var adopted = 0;
        foreach (var targetFont in FontChunkLocator.FindAll(_root))
        {
            if (!sourceFonts.TryGetValue(targetFont.BaseName, out var sourceFont)) continue;

            targetFont.Chunk.Children.Clear();
            targetFont.Chunk.Children.AddRange(sourceFont.Chunk.Children);
            adopted++;
        }

        return adopted;
    }

    // =========================================================================
    // UA: КИРИЛИЧНИЙ КОДЕК — ЯВНЕ (не автоматичне) підключення таблиці
    //     "літера→байт-код", згенерованої GenerateLocalizedCoreCommand.
    //     Застосовується ЛИШЕ до записів targetLanguage — на практиці це
    //     "english_loc" (переклад пишеться ПОВЕРХ English, мови за
    //     замовчуванням на ліцензійній копії гри; в грі ніколи не було
    //     окремого українського слоту — той самий підхід, що й у
    //     попередніх проєктах SteamWorld/Empire at War). Інші мови
    //     (french, german, italian, spanish, uk_english...) НІКОЛИ не
    //     декодуються цією таблицею, бо частина кодів може бути "м'якими"
    //     донорами, тобто фізично зайнятими акцентованими літерами ІНШИХ
    //     мов (наприклад французьке É) — застосування до них зіпсувало б
    //     показ цих мов. Див. заголовок CyrillicCodeTable.cs.
    //
    //     Одразу декодує вже завантажений Original цільової мови
    //     (псевдо-код → справжня кирилиця), щоб GUI показав читабельний
    //     текст одразу після відкриття файлу.
    // EN: CYRILLIC CODEC — EXPLICIT (not automatic) wiring of the
    //     "letter→byte-code" table produced by GenerateLocalizedCoreCommand.
    //     Applied ONLY to targetLanguage's entries — in practice this is
    //     "english_loc" (the translation is written OVER English, the
    //     default language on a licensed game copy; the game never had a
    //     separate Ukrainian slot — same approach as the prior SteamWorld/
    //     Empire at War projects). Other languages (french, german,
    //     italian, spanish, uk_english...) are NEVER decoded by this
    //     table, because some codes may be "soft" donors, i.e. physically
    //     occupied by OTHER languages' accented letters (e.g. French É) —
    //     applying it to them would corrupt those languages' display. See
    //     CyrillicCodeTable.cs header.
    //
    //     Immediately decodes the target language's already-loaded
    //     Original text (pseudo-code → real Cyrillic), so the GUI shows
    //     readable text right after opening the file.
    // =========================================================================
    public void ApplyCyrillicCodeTable(CyrillicCodeTable table, string targetLanguage)
    {
        _codeTable = table;
        _codeTableTargetLanguage = targetLanguage;

        if (_records.TryGetValue(targetLanguage, out var rec))
            foreach (var entry in rec.File.Entries)
                entry.Original = table.FromGameEncoded(entry.Original);
    }

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

            // UA: Для мови, до якої підключено кириличний кодек — серіалізуємо
            //     ТИМЧАСОВУ копію з підміненими на псевдо-коди символами,
            //     а не сам rec.File (щоб у пам'яті/GUI лишалась справжня,
            //     читабельна кирилиця — підміна лише "на виході в байти").
            // EN: For the language with the Cyrillic codec wired up —
            //     serialize a TEMPORARY clone with characters substituted
            //     for pseudo-codes, not rec.File itself (so real, readable
            //     Cyrillic stays in memory/GUI — substitution only "on the
            //     way out to bytes").
            var fileToSerialize = rec.File;
            if (_codeTable is not null &&
                lang.Equals(_codeTableTargetLanguage, StringComparison.OrdinalIgnoreCase))
                fileToSerialize = BuildGameEncodedClone(rec.File, _codeTable);

            var newBytes = rec.FileType == LocalizationFileType.PlainText
                ? LocalizationParser.SerializeToBytes(fileToSerialize, useTranslation: true)
                : LoclChunkParser.BuildBodyBytes(fileToSerialize, useTranslation: true);

            replacements[rec.ReplaceChunk.FileDataOffset] = newBytes;
        }

        await Task.Run(() => UcfbWriter.WriteFile(outputPath, _root, replacements));
    }

    // UA: Клонує мовний файл із текстом, пропущеним через ToGameEncoded
    //     (кирилиця → псевдо-байт-код), не чіпаючи оригінал у пам'яті.
    // EN: Clones a language file with text passed through ToGameEncoded
    //     (Cyrillic → pseudo byte-code), without touching the in-memory
    //     original.
    private static LocalizationFile BuildGameEncodedClone(LocalizationFile source, CyrillicCodeTable table)
    {
        var clone = new LocalizationFile { LanguageName = source.LanguageName };
        foreach (var entry in source.Entries)
        {
            clone.Entries.Add(new LocalizationEntry
            {
                Hash = entry.Hash,
                Original = table.ToGameEncoded(entry.Original),
                Translation = entry.Translation is null ? null : table.ToGameEncoded(entry.Translation),
                IsTechnical = entry.IsTechnical,
            });
        }
        clone.BuildIndex();
        return clone;
    }

    // =========================================================================
    // UA: ЕКСПОРТ / ІМПОРТ CSV
    //     Формат і фактичний файловий I/O винесені в LocalizationCsvIo
    //     (спільний з консольним ШІ-перекладачем BF1LocalizationTool.
    //     Translator) — тут лишається лише конвертація між внутрішньою
    //     моделлю (LocalizationFile/LocalizationEntry) і LocalizationCsvRow.
    // EN: EXPORT / IMPORT CSV
    //     Format and actual file I/O extracted into LocalizationCsvIo
    //     (shared with the console AI translator BF1LocalizationTool.
    //     Translator) — only the conversion between the internal model
    //     (LocalizationFile/LocalizationEntry) and LocalizationCsvRow
    //     remains here.
    // =========================================================================
    public async Task ExportCsvAsync(string language, string csvPath)
    {
        var file = GetLanguageFile(language)
            ?? throw new ArgumentException(
                $"UA: Мова '{language}' не знайдена / EN: Language '{language}' not found");

        var rows = new List<LocalizationCsvRow>();
        var occurrence = new Dictionary<uint, int>();
        foreach (var entry in file.Entries)
        {
            var ordinal = occurrence.TryGetValue(entry.Hash, out var n) ? n : 0;
            occurrence[entry.Hash] = ordinal + 1;

            rows.Add(new LocalizationCsvRow(entry.Hash, ordinal, entry.Original, entry.Translation));
        }

        await LocalizationCsvIo.WriteAsync(csvPath, rows);
    }

    public async Task ImportCsvAsync(string targetLanguage, string csvPath)
    {
        var file = GetLanguageFile(targetLanguage)
            ?? throw new ArgumentException(
                $"UA: Мова '{targetLanguage}' не знайдена / EN: Language '{targetLanguage}' not found");

        var rows = await LocalizationCsvIo.ReadAsync(csvPath);
        var imported = 0;

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Translation)) continue;

            var entry = file.GetByHash(row.Hash, row.Ordinal);
            if (entry is null) continue;

            entry.Translation = row.Translation;
            imported++;
        }
    }
}
