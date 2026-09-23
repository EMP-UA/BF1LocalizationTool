// =============================================================================
// BF1LocalizationTool.Diagnostic — MvsContainerScanCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Розбирає контейнер відеороликів `.mvs` (тека GameData\data\_lvl_pc\
//     movies) і друкує його заголовок у читабельному вигляді.
//
//     ЩО ЦЕ ЗА ФОРМАТ (встановлено побайтово на реальних
//     файлах BF2 `training.mvs`, `crawl.mvs`):
//       - `.mvs` — це звичайний ucfb-контейнер (магія 'ucfb'), а НЕ окреме
//         відео. Усередині лежить кілька СЕГМЕНТІВ, кожен зі своїм іменем,
//         і кожен сегмент — це потік Bink ('BIKi', 800x600).
//       - Саме тому скрипти звертаються до нього ПАРОЮ аргументів:
//         `ScriptCB_PlayInGameMovie('ingame.mvs', 'cormon01')` — файл плюс
//         ім'я сегмента (`mission.lvl`, скрипт `cor1c_c`).
//       - Заголовок — це "property bag": послідовність пар
//         [uint32 хеш-імені-поля][uint32 значення]. Хеш той самий FNV-1a,
//         що вже реалізований у `SwbfStringHash` (тобто той самий механізм,
//         яким адресуються рядки локалізації).
//
//     ПІДТВЕРДЖЕНІ ІМЕНА ПОЛІВ (усі збіглися точно, без підгонки):
//         0x0FB40705 = "info"          → розмір блоку заголовка
//         0x8D39BDE6 = "name"          → хеш імені (контейнера чи сегмента)
//         0x40FBDEBD = "NumSegments"   → скільки сегментів у файлі
//         0xB969BE96 = "SegmentInfo"   → початок опису сегмента
//         0x83D03615 = "length"        → довжина даних сегмента
//         0x7DB1D043 = "SegmentEnd"    → кінець опису сегмента
//         0xF3C342E6 = "Segment"       → тип блоку
//     Значення полів `name` — теж хеші, тому вони резолвляться словником,
//     зібраним із РЕАЛЬНИХ рядків гри (див. нижче), а не з вгадувань.
//
//     НАВІЩО: субтитри до внутрішньоігрових роликів (`bMovieSubtitles`)
//     показуються при 800x600, але зникають на 1920x1080 — і це дефект
//     САМОЇ гри, відтворюваний на ваніль­ному `core.lvl` (перевірено
//     на реальній машині). Текст субтитрів при цьому лежить у `Locl`
//     звичайними записами й перекладається інструментом нормально. Щоб
//     зрозуміти, ЧИМ керується показ (таймкоди, прив'язка до сегмента,
//     координати), спершу треба прочитати цей заголовок — чим і займається
//     ця команда.
//
//     ФАЙЛ НЕ ЗМІНЮЄТЬСЯ. Читається ЛИШЕ початок (за замовчуванням 8 МБ),
//     тому команда однаково працює і на `ingame.mvs` (понад 500 МБ) —
//     нічого не завантажується в пам'ять цілком.
//
// EN: Parses a `.mvs` movie container (GameData\data\_lvl_pc\movies) and
//     prints its header in readable form.
//
//     THE FORMAT (established byte-by-byte against the real
//     BF2 `training.mvs` and `crawl.mvs`):
//       - `.mvs` is an ordinary ucfb container ('ucfb' magic), NOT a single
//         movie. It holds several SEGMENTS, each with its own name, and
//         each segment is a Bink stream ('BIKi', 800x600).
//       - That is why scripts address it with a PAIR of arguments:
//         `ScriptCB_PlayInGameMovie('ingame.mvs', 'cormon01')` — the file
//         plus a segment name (`mission.lvl`, script `cor1c_c`).
//       - The header is a "property bag": a sequence of
//         [uint32 field-name hash][uint32 value] pairs. The hash is the very
//         same FNV-1a already implemented in `SwbfStringHash` (i.e. the same
//         mechanism that addresses localization strings).
//
//     CONFIRMED FIELD NAMES (all matched exactly, nothing fudged) — see the
//     UA list above. `name` values are hashes too, so they are resolved via
//     a dictionary built from the game's REAL strings (below), not guesses.
//
//     WHY: subtitles for in-game movies (`bMovieSubtitles`) show at 800x600
//     but disappear at 1920x1080 — a defect of the GAME itself, reproduced
//     on a vanilla `core.lvl` (verified on a real machine). The
//     subtitle text itself lives in `Locl` as ordinary entries and is
//     translated by the tool just fine. To understand what drives their
//     display (timecodes, segment binding, coordinates) this header has to
//     be read first — which is what this command does.
//
//     THE FILE IS NEVER MODIFIED. Only the beginning is read (8 MB by
//     default), so it works the same on `ingame.mvs` (500+ MB) — nothing is
//     ever loaded whole.
// =============================================================================

using System.Buffers.Binary;
using System.Text;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Localization;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class MvsContainerScanCommand
{
    // UA: Скільки байт початку файлу читати. Заголовок реальних файлів —
    //     кілька кілобайт (у `crawl.mvs` — 2008 Б), але запас потрібен на
    //     випадок, якщо в `ingame.mvs` із десятками сегментів (і, можливо,
    //     таблицею субтитрів) він значно більший.
    // EN: How many bytes of the file start to read. Real headers are a few
    //     KB (2008 B in `crawl.mvs`), but headroom is needed in case
    //     `ingame.mvs`, with dozens of segments (and possibly a subtitle
    //     table), has a much larger one.
    private const int DefaultHeaderReadBytes = 8 * 1024 * 1024;

    // UA: Магія ucfb — та сама, що й у .lvl (див. UcfbReader).
    // EN: The ucfb magic — the same as in .lvl (see UcfbReader).
    private const uint MagicUcfb = 0x62666375;

    // UA: Імена полів заголовка, підтверджені збігом хешів (жодного
    //     "приблизно" — кожне дає точний FNV-1a цього значення).
    // EN: Header field names confirmed by hash match (nothing approximate —
    //     each yields exactly this FNV-1a value).
    private static readonly string[] KnownFieldNames =
    [
        "info", "name", "NumSegments", "SegmentInfo", "SegmentEnd", "Segment",
        "length", "size", "offset", "count", "type", "data", "body",
        // UA: Кандидати, які ще не траплялись, але коштують нуль — якщо
        //     котрийсь зустрінеться в `ingame.mvs`, він одразу отримає ім'я.
        // EN: Candidates not seen yet but free to try — if any shows up in
        //     `ingame.mvs` it gets a name immediately.
        "Subtitle", "Subtitles", "SubtitleInfo", "SubtitleEnd", "NumSubtitles",
        "SubtitleCount", "SubtitleStart", "SubtitleText", "SubtitleHash",
        "Caption", "Captions", "Text", "TextInfo", "TextEnd", "NumTexts",
        "start", "end", "time", "startTime", "endTime", "frame", "startFrame",
        "endFrame", "duration", "fps", "hash", "key", "index", "id",
        "AudioInfo", "AudioEnd", "NumAudio", "width", "height", "flags",
    ];

    public static void Run(
        DiagnosticReport report,
        string mvsFilePath,
        string? coreLvlPathForLocl,
        IReadOnlyList<string> lvlPathsForNames,
        int headerReadBytes = DefaultHeaderReadBytes)
    {
        report.Log("UA: === Розбір контейнера відеороликів .mvs ===");
        report.Log("EN: === .mvs movie container scan ===");
        report.Log($"    {mvsFilePath}");
        report.Log();

        if (!File.Exists(mvsFilePath))
        {
            report.Log($"UA: Файл не знайдено: {mvsFilePath}");
            report.Log($"EN: File not found: {mvsFilePath}");
            return;
        }

        // ---------------------------------------------------------------------
        // UA: 1. Читаємо ЛИШЕ початок файлу — цього досить для заголовка, і
        //        це дозволяє працювати з `ingame.mvs` (>500 МБ) без ризику
        //        з'їсти всю пам'ять.
        // EN: 1. Read ONLY the start of the file — enough for the header, and
        //        it lets `ingame.mvs` (>500 MB) be handled without eating all
        //        memory.
        // ---------------------------------------------------------------------
        long fileLength;
        byte[] head;
        using (var fs = new FileStream(mvsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            fileLength = fs.Length;
            var toRead = (int)Math.Min(headerReadBytes, fileLength);
            head = new byte[toRead];
            var got = fs.Read(head, 0, toRead);
            if (got < toRead)
                Array.Resize(ref head, got);
        }

        report.Log($"    Розмір файлу / File size: {fileLength:N0} байт");
        report.Log($"    Прочитано початку / Head read: {head.Length:N0} байт");

        if (head.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(head) != MagicUcfb)
        {
            report.Log("UA: Це не ucfb-контейнер — магія 'ucfb' відсутня.");
            report.Log("EN: Not a ucfb container — the 'ucfb' magic is missing.");
            return;
        }

        // ---------------------------------------------------------------------
        // UA: 2. Будуємо словник "хеш → ім'я" з РЕАЛЬНИХ джерел:
        //        а) імена полів заголовка (вище);
        //        б) усі рядкові константи Lua з переданих .lvl — саме звідти
        //           беруться імена сегментів ('cormon01', 'crawl1', ...);
        //        в) хеші рядків локалізації з core.lvl — щоб одразу впізнати
        //           значення, яке насправді є посиланням на текст субтитру.
        // EN: 2. Build a "hash → name" dictionary from REAL sources:
        //        a) the header field names above;
        //        b) every Lua string constant from the given .lvl files —
        //           that is where segment names ('cormon01', 'crawl1', ...)
        //           come from;
        //        c) localization string hashes from core.lvl — so a value
        //           that is really a subtitle text reference is recognized
        //           right away.
        // ---------------------------------------------------------------------
        var names = new Dictionary<uint, string>();
        foreach (var field in KnownFieldNames)
            names.TryAdd(SwbfStringHash.Compute(field), field);

        var scriptStringCount = 0;
        foreach (var lvlPath in lvlPathsForNames)
        {
            if (!File.Exists(lvlPath)) continue;
            try
            {
                var lvlRoot = UcfbReader.ReadFile(lvlPath);
                foreach (var script in ScriptChunkLocator.FindAll(lvlRoot))
                {
                    LuaChunkParseResult parsed;
                    try { parsed = Lua50BytecodeReader.Parse(script.BodyChunk.RawData); }
                    catch { continue; } // UA: не Lua або інша версія — просто пропускаємо

                    CollectStrings(parsed.Root, names, ref scriptStringCount);
                }
            }
            catch (Exception ex)
            {
                report.Log($"UA: Не вдалося прочитати {Path.GetFileName(lvlPath)}: {ex.Message}");
            }
        }

        var loclTexts = new Dictionary<uint, string>();
        if (coreLvlPathForLocl is not null && File.Exists(coreLvlPathForLocl))
            LoadLoclTexts(coreLvlPathForLocl, loclTexts, report);

        report.Log($"    Імен зі скриптів / Names from scripts: {scriptStringCount}");
        report.Log($"    Рядків локалізації / Localization strings: {loclTexts.Count}");
        report.Log();

        // ---------------------------------------------------------------------
        // UA: 3. Проходимо вкладені 8-байтові заголовки чанків, доки не
        //        натрапимо на поле "info" — воно відкриває property bag.
        // EN: 3. Walk the nested 8-byte chunk headers until the "info" field
        //        is hit — it opens the property bag.
        // ---------------------------------------------------------------------
        var infoHash = SwbfStringHash.Compute("info");
        var pos = 0;
        var depth = 0;
        while (pos + 8 <= head.Length && depth < 8)
        {
            var id = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(pos));
            var size = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(pos + 4));
            if (id == infoHash) break;

            report.Log($"    [обгортка / wrapper {depth}] id=0x{id:X8} size={size:N0}");
            pos += 8;
            depth++;
        }

        if (pos + 8 > head.Length)
        {
            report.Log("UA: Поле 'info' не знайдено на початку файлу — формат інший.");
            report.Log("EN: No 'info' field near the file start — different layout.");
            return;
        }

        var infoSize = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(pos + 4));
        report.Log();
        report.Log($"    info @ 0x{pos:X}, розмір блоку / block size = {infoSize:N0} байт");
        report.Log();

        // ---------------------------------------------------------------------
        // UA: 4. Друкуємо property bag парами. Відступ збільшується всередині
        //        SegmentInfo…SegmentEnd, щоб структура читалась очима.
        // EN: 4. Print the property bag pair by pair. Indentation grows inside
        //        SegmentInfo…SegmentEnd so the structure reads at a glance.
        // ---------------------------------------------------------------------
        var segmentInfoHash = SwbfStringHash.Compute("SegmentInfo");
        var segmentEndHash = SwbfStringHash.Compute("SegmentEnd");

        var bagStart = pos + 8;
        var bagEnd = (int)Math.Min(bagStart + infoSize, head.Length);
        var indent = 0;
        var unknownFields = new SortedDictionary<uint, int>();
        var subtitleHits = new List<(int Offset, uint Hash, string Text)>();

        // UA: Опис сегментів у порядку появи — потрібен, щоб потім обчислити
        //     фізичне розташування даних кожного з них (див. крок 6).
        //     `Extra` — значення поля 0x809608B6: РОЗМІР блоку, що йде одразу
        //     ПІСЛЯ Bink-потоку сегмента (перевірено арифметично: початок
        //     наступного сегмента = початок поточного + length + Extra, точний
        //     збіг із фактичними позиціями 'BIKi' у файлі).
        // EN: Segments in order of appearance — needed to compute where each
        //     one's data physically sits (see step 6). `Extra` is the value of
        //     field 0x809608B6: the SIZE of the block that follows the
        //     segment's Bink stream (verified arithmetically: next segment
        //     start = current start + length + Extra, matching the actual
        //     'BIKi' positions in the file exactly).
        var segments = new List<(string Name, uint Length, uint Extra)>();
        string? pendingName = null;
        uint pendingLength = 0;
        var lengthHash = SwbfStringHash.Compute("length");
        var nameHash = SwbfStringHash.Compute("name");
        const uint ExtraBlockField = 0x809608B6;

        report.Log("    зміщення | поле                       | значення");
        report.Log("    offset   | field                      | value");
        report.Log("    ---------+----------------------------+---------------------------------");

        for (var p = bagStart; p + 8 <= bagEnd; p += 8)
        {
            var key = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(p));
            var val = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(p + 4));

            // UA: Порожній "хвіст" заголовка — далі лише нулі, друкувати нічого.
            // EN: Empty header "tail" — nothing but zeros from here on.
            if (key == 0 && val == 0) continue;

            if (key == segmentEndHash && indent > 0) indent--;

            var keyName = names.TryGetValue(key, out var kn) ? kn : null;
            if (keyName is null) unknownFields[key] = unknownFields.GetValueOrDefault(key) + 1;

            var keyText = keyName ?? $"0x{key:X8} (?)";
            var valText = DescribeValue(val, names, loclTexts, out var isSubtitleText);

            if (isSubtitleText)
                subtitleHits.Add((p + 4, val, loclTexts[val]));

            var keyColumn = new string(' ', indent * 2) + keyText;
            report.Log($"    {p,8:X4} | {keyColumn,-26} | {valText}");

            // UA: Накопичуємо опис сегмента: ім'я → довжина → розмір блоку.
            // EN: Accumulate the segment description: name → length → block size.
            // UA: Ім'я сегмента зберігаємо навіть коли хеш не резолвиться —
            //     інакше один невідомий сегмент збив би розрахунок усіх
            //     наступних (вони йдуть у файлі підряд).
            // EN: Keep the segment name even when the hash does not resolve —
            //     otherwise one unknown segment would throw off the layout of
            //     every following one (they sit back to back in the file).
            if (key == nameHash && p > bagStart)
                pendingName = names.TryGetValue(val, out var segName) ? segName : $"0x{val:X8}";
            else if (key == lengthHash)
                pendingLength = val;
            else if (key == ExtraBlockField && pendingName is not null)
            {
                segments.Add((pendingName, pendingLength, val));
                pendingName = null;
                pendingLength = 0;
            }

            if (key == segmentInfoHash) indent++;
        }

        report.Log();

        // ---------------------------------------------------------------------
        // UA: 5. Підсумок — що лишилось нерозпізнаним і чи знайшлись
        //        посилання на тексти локалізації (тобто субтитри).
        // EN: 5. Summary — what stayed unrecognized, and whether any
        //        localization text references (i.e. subtitles) were found.
        // ---------------------------------------------------------------------
        if (unknownFields.Count > 0)
        {
            report.Log($"UA: Нерозпізнані поля ({unknownFields.Count}) — імена ще не підібрані:");
            report.Log($"EN: Unrecognized fields ({unknownFields.Count}) — names not resolved yet:");
            foreach (var (h, n) in unknownFields)
                report.Log($"      0x{h:X8}  ×{n}");
            report.Log();
        }
        else
        {
            report.Log("UA: Усі поля заголовка розпізнані.");
            report.Log("EN: Every header field was recognized.");
            report.Log();
        }

        if (subtitleHits.Count > 0)
        {
            report.Log($"UA: !! ЗНАЙДЕНО {subtitleHits.Count} посилань на рядки локалізації —");
            report.Log("    це і є прив'язка субтитрів до відео:");
            report.Log($"EN: !! FOUND {subtitleHits.Count} localization string references —");
            report.Log("    this is the movie-to-subtitle binding:");
            foreach (var (off, h, text) in subtitleHits.Take(200))
            {
                var shown = text.Length > 90 ? text[..90] + "…" : text;
                report.Log($"      @0x{off:X4}  0x{h:X8}  \"{shown}\"");
            }
            report.Log();
        }
        else
        {
            report.Log("UA: Посилань на рядки локалізації в заголовку НЕМАЄ — субтитри");
            report.Log("    прив'язуються не тут (шукати далі: у самому потоці сегмента");
            report.Log("    або в нативному коді гри).");
            report.Log("EN: NO localization string references in the header — subtitles are");
            report.Log("    bound elsewhere (look further: inside the segment stream itself,");
            report.Log("    or in the game's native code).");
            report.Log();
        }

        // ---------------------------------------------------------------------
        // UA: 6. Де фізично лежать потоки Bink — корисно для звірки з `length`.
        // EN: 6. Where the Bink streams physically start — useful to
        //        cross-check against `length`.
        // ---------------------------------------------------------------------
        var binkOffsets = FindBinkOffsets(head);
        report.Log($"UA: Потоків Bink у прочитаному початку: {binkOffsets.Count}");
        report.Log($"EN: Bink streams within the head read: {binkOffsets.Count}");
        foreach (var off in binkOffsets.Take(20))
            report.Log($"      @0x{off:X}  {Encoding.ASCII.GetString(head, off, 4)}");
        report.Log();

        // ---------------------------------------------------------------------
        // UA: 7. ГОЛОВНЕ. Після заголовка йде чанк 'data', а в ньому — сегменти
        //        підряд: [потік Bink довжиною `length`][блок розміром `Extra`].
        //        Саме цей блок — єдине місце у файлі, куди можуть бути записані
        //        дані субтитрів, тож читаємо його ТОЧКОВО (seek), не тягнучи
        //        півгігабайта. Для кожного сегмента друкуємо, порожній блок чи
        //        ні, і якщо не порожній — його вміст.
        // EN: 7. THE MAIN PART. After the header comes a 'data' chunk holding
        //        the segments back to back: [Bink stream of `length` bytes]
        //        [block of `Extra` bytes]. That block is the only place in the
        //        file where subtitle data could live, so it is read POINTWISE
        //        (seek) instead of pulling half a gigabyte. For each segment,
        //        whether the block is empty is printed, and if not — its contents.
        // ---------------------------------------------------------------------
        if (segments.Count == 0)
        {
            report.Log("UA: Сегментів не розпізнано — крок 7 пропущено.");
            report.Log("EN: No segments recognized — step 7 skipped.");
            return;
        }

        var dataHash = SwbfStringHash.Compute("data");
        var dataStart = -1;
        for (var p = bagEnd - 8; p + 8 <= head.Length && p < bagEnd + 64; p += 4)
        {
            if (p < 0) continue;
            if (BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(p)) == dataHash)
            {
                dataStart = p + 8;
                break;
            }
        }

        if (dataStart < 0)
        {
            report.Log("UA: Чанк 'data' після заголовка не знайдено — розкладку сегментів");
            report.Log("    обчислити неможливо.");
            report.Log("EN: No 'data' chunk after the header — the segment layout cannot");
            report.Log("    be computed.");
            return;
        }

        report.Log($"UA: Дані сегментів починаються з 0x{dataStart:X}.");
        report.Log($"EN: Segment data starts at 0x{dataStart:X}.");
        report.Log();
        report.Log("    сегмент     | Bink @        | блок @        | розмір | вміст");
        report.Log("    segment     | Bink at       | block at      | size   | contents");
        report.Log("    ------------+---------------+---------------+--------+-------------------");

        using (var fs = new FileStream(mvsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            long cursor = dataStart;
            foreach (var (segName, segLength, segExtra) in segments)
            {
                var blockOffset = cursor + segLength;
                var summary = ReadAndDescribeBlock(fs, blockOffset, segExtra, fileLength, names, loclTexts, report);

                report.Log($"    {segName,-11} | {cursor,13:N0} | {blockOffset,13:N0} | {segExtra,6:N0} | {summary}");

                cursor = blockOffset + segExtra;
            }
        }

        report.Log();
        report.Log("UA: Якщо у стовпці \"вміст\" скрізь \"порожньо\" — субтитрів у файлі немає");
        report.Log("    взагалі, і рушій бере їх деінде. Якщо десь є дані — це і є та сама");
        report.Log("    таблиця, задля якої писалась ця команда.");
        report.Log("EN: If the \"contents\" column says \"empty\" everywhere, the file holds no");
        report.Log("    subtitles at all and the engine takes them from elsewhere. Any segment");
        report.Log("    with data is the very table this command was written for.");
        report.Log();
    }

    // -------------------------------------------------------------------------
    // UA: Читає блок після сегмента й коротко описує його вміст. Сам блок
    //     починається парою [ім'я][розмір] (те саме поле 0x809608B6), далі —
    //     корисні дані. Порожній блок = самі нулі: у `crawl.mvs` саме так, бо
    //     його текст "запечений" у кадри відео.
    // EN: Reads the block that follows a segment and briefly describes it. The
    //     block itself opens with an [id][size] pair (the same 0x809608B6
    //     field), then the payload. An empty block is all zeros: that is the
    //     case in `crawl.mvs`, whose text is baked into the video frames.
    // -------------------------------------------------------------------------
    private static string ReadAndDescribeBlock(
        FileStream fs, long offset, uint size, long fileLength,
        IReadOnlyDictionary<uint, string> names,
        IReadOnlyDictionary<uint, string> loclTexts,
        DiagnosticReport report)
    {
        if (size == 0) return "— (розмір 0 / size 0)";
        if (offset < 0 || offset + size > fileLength) return "!! ПОЗА МЕЖАМИ ФАЙЛУ / OUT OF FILE BOUNDS";

        var buffer = new byte[size];
        fs.Seek(offset, SeekOrigin.Begin);
        var read = fs.Read(buffer, 0, buffer.Length);
        if (read < buffer.Length) Array.Resize(ref buffer, read);

        // UA: Пропускаємо власний заголовок блоку (8 байт), якщо він є.
        // EN: Skip the block's own 8-byte header if present.
        var payloadStart = buffer.Length >= 8 ? 8 : 0;
        var nonZero = 0;
        for (var i = payloadStart; i < buffer.Length; i++)
            if (buffer[i] != 0) nonZero++;

        if (nonZero == 0) return "порожньо (самі нулі) / empty (all zeros)";

        // UA: Є дані — друкуємо їх повністю нижче таблиці, бо це саме те, заради
        //     чого все робилось; у самому рядку лишаємо стислий підсумок.
        // EN: There is payload — dump it fully below the table, since that is
        //     the whole point; the row itself keeps a short summary.
        report.Log();
        report.Log($"      !! НЕПОРОЖНІЙ БЛОК @0x{offset:X} ({size} байт, ненульових {nonZero}):");
        report.Log($"      !! NON-EMPTY BLOCK @0x{offset:X} ({size} bytes, {nonZero} non-zero):");

        var shown = Math.Min(buffer.Length, 512);
        for (var i = 0; i < shown; i += 16)
        {
            var len = Math.Min(16, shown - i);
            var hex = Convert.ToHexString(buffer, i, len);
            report.Log($"         {offset + i:X8}  {hex}");
        }

        // UA: Спроба прочитати як пари uint32 — раптом це [хеш рядка][таймкод].
        // EN: Try reading as uint32 pairs — in case it is [string hash][timecode].
        for (var i = payloadStart; i + 4 <= buffer.Length; i += 4)
        {
            var v = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(i));
            if (v == 0) continue;

            if (loclTexts.TryGetValue(v, out var text))
            {
                var t = text.Length > 70 ? text[..70] + "…" : text;
                report.Log($"         +{i:X4}  0x{v:X8}  ТЕКСТ / TEXT \"{t}\"");
            }
            else if (names.TryGetValue(v, out var nm))
            {
                report.Log($"         +{i:X4}  0x{v:X8}  \"{nm}\"");
            }
        }
        report.Log();

        return $"ДАНІ / DATA ({nonZero} ненульових байт)";
    }

    // -------------------------------------------------------------------------
    // UA: Опис значення: спершу як відоме ім'я (хеш), потім як текст
    //     локалізації, і лише потім — як число/hex. Порядок навмисний:
    //     збіг із іменем однозначніший за "схоже на невелике число".
    // EN: Describe a value: first as a known name (hash), then as a
    //     localization string, and only then as a number/hex. The order is
    //     deliberate: a name match is less ambiguous than "looks like a
    //     small number".
    // -------------------------------------------------------------------------
    private static string DescribeValue(
        uint value,
        IReadOnlyDictionary<uint, string> names,
        IReadOnlyDictionary<uint, string> loclTexts,
        out bool isSubtitleText)
    {
        isSubtitleText = false;

        // UA: Малі значення — це довжини, лічильники й зміщення, а не хеші.
        //     Поріг захищає від випадкового "збігу" з хешем рядка: у таблиці
        //     локалізації трапляються й малі хеші (0x000109AE тощо), тож без
        //     нього лічильник сегментів міг би бути показаний як текст.
        // EN: Small values are lengths, counters and offsets, not hashes. The
        //     threshold guards against an accidental "match" with a string
        //     hash: the localization table does contain small hashes
        //     (0x000109AE etc.), so without it a segment counter could be
        //     rendered as text.
        if (value < 0x0001_0000)
            return $"{value:N0}  (0x{value:X8})";

        if (names.TryGetValue(value, out var name))
            return $"\"{name}\"  (хеш / hash 0x{value:X8})";

        if (loclTexts.TryGetValue(value, out var text))
        {
            isSubtitleText = true;
            var shown = text.Length > 60 ? text[..60] + "…" : text;
            return $"ТЕКСТ / TEXT \"{shown}\"  (0x{value:X8})";
        }

        // UA: Не впізнали — показуємо обидві форми: як число (раптом це
        //     довжина/зміщення) і як hex (раптом це ще не підібраний хеш).
        // EN: Not recognized — show both forms: as a number (in case it is a
        //     length/offset) and as hex (in case it is a hash not yet
        //     resolved).
        return $"{value:N0}  (0x{value:X8})";
    }

    // -------------------------------------------------------------------------
    // UA: Збирає рядкові константи прототипу й усіх вкладених — саме там
    //     лежать імена сегментів, якими їх викликають скрипти.
    // EN: Collects string constants of a prototype and all nested ones —
    //     that is where the segment names used by scripts live.
    // -------------------------------------------------------------------------
    private static void CollectStrings(
        LuaFunctionPrototype proto, Dictionary<uint, string> names, ref int counter)
    {
        foreach (var k in proto.Constants)
        {
            if (k.Kind != LuaConstantKind.String || k.StringValue is null) continue;

            var s = k.StringValue;
            // UA: Хеш рахується лише для ASCII-ключів (див. SwbfStringHash).
            // EN: The hash is ASCII-only (see SwbfStringHash).
            if (s.Length == 0 || s.Any(ch => ch > 0x7F)) continue;

            if (names.TryAdd(SwbfStringHash.Compute(s), s))
                counter++;
        }

        foreach (var nested in proto.NestedPrototypes)
            CollectStrings(nested, names, ref counter);
    }

    // -------------------------------------------------------------------------
    // UA: Витягує пари "хеш → відображуваний текст" з усіх Locl-чанків
    //     core.lvl. Потрібно, щоб упізнати значення, яке є субтитром.
    // EN: Pulls "hash → displayed text" pairs from every Locl chunk of
    //     core.lvl. Needed to recognize a value that is a subtitle.
    // -------------------------------------------------------------------------
    private static void LoadLoclTexts(
        string coreLvlPath, Dictionary<uint, string> into, DiagnosticReport report)
    {
        try
        {
            var root = UcfbReader.ReadFile(coreLvlPath);
            var loclIndex = 0;
            foreach (var locl in UcfbReader.FindAll(root, "Locl"))
            {
                var parsed = LoclChunkParser.ParseFromUcfbChunk(locl, $"loc_{loclIndex++}");
                if (parsed is null) continue;

                foreach (var entry in parsed.Value.File.Entries)
                    into.TryAdd(entry.Hash, entry.Original);
            }
        }
        catch (Exception ex)
        {
            report.Log($"UA: Не вдалося прочитати локалізацію з {Path.GetFileName(coreLvlPath)}: {ex.Message}");
            report.Log($"EN: Could not read localization from {Path.GetFileName(coreLvlPath)}: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // UA: Шукає сигнатури потоків Bink ('BIK' + літера версії).
    // EN: Finds Bink stream signatures ('BIK' + a version letter).
    // -------------------------------------------------------------------------
    private static List<int> FindBinkOffsets(byte[] data)
    {
        var result = new List<int>();
        for (var i = 0; i + 4 <= data.Length; i++)
        {
            if (data[i] != (byte)'B' || data[i + 1] != (byte)'I' || data[i + 2] != (byte)'K') continue;
            var v = data[i + 3];
            if (v is >= (byte)'a' and <= (byte)'z') result.Add(i);
        }
        return result;
    }
}
