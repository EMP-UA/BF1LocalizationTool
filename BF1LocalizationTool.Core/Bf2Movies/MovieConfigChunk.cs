// =============================================================================
// BF1LocalizationTool.Core — Bf2Movies/MovieConfigChunk.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Читач конфігу відеороликів BF2 — чанк "mcfg". Саме тут, а НЕ у .mvs і
//     не в Lua, лежать субтитри роликів: текст адресується хешем рядка в
//     таблиці Locl (core.lvl), а час подано парою "початок + тривалість".
//
//     ЯК ЦЕ БУЛО ЗНАЙДЕНО. Хеші рядків субтитрів були відомі з розбору Locl;
//     побайтовий пошук цих 4 байтів по файлах гри дав рівно два попадання —
//     core.lvl (сам текст) і mission.lvl. Формат далі звірено з машинним
//     кодом гри: у BattlefrontII.exe за VA 0x006F6089 стоїть буквально
//     `cmp eax, 0x115BFCB9` (хеш директиви "subtitle"), після чого рушій
//     читає uint-хеш і два float-и, рахує `кінець = початок + тривалість`,
//     складає 12-байтові записи (ліміт 256) і зберігає вектор у полях
//     +0x18/+0x1C/+0x20 конфігу, а хеш шрифту — у +0x14. Тобто наведений
//     нижче розбір — не інтерпретація, а дзеркало реального парсера.
//
//     ФОРМАТ (спільний для ucfb-конфігів BF2: .hud, .snd, mcfg):
//       DATA = [хеш директиви : 4][кількість значень : 1]
//              [значення × 4 байти][довжина блоку рядків : 4][рядки\0…]
//       SCOP = вкладена область (діти — знову DATA/SCOP)
//     Значення — це або float, або uint (хеш): тип задається директивою,
//     у самому файлі розрізнення немає, тому зберігаємо обидва прочитання.
//
//     ІЄРАРХІЯ:
//       movieproperties { name(<хеш>)  movie(<хеш>)
//          segmentlist {
//             <блок> { segment(<хеш сегмента>)  font(<хеш>)
//                      color(r, g, b, a)
//                      subtitlelist { subtitle(<хеш Locl>, початок, тривалість) … } } } }
//
//     ЦЕ ЛИШЕ ЧИТАЧ. Він нічого не змінює й нічого не вигадує: невідомі
//     директиви повертаються як є (сирим хешем), щоб виклик міг чесно
//     показати, що саме ще не розпізнано, замість тихого пропуску.
// EN: Reader for BF2's movie configuration — the "mcfg" chunk. This, and NOT
//     the .mvs container or any Lua script, is where movie subtitles live:
//     the text is addressed by a hash into the Locl table (core.lvl), and the
//     timing is given as a "start + duration" pair.
//
//     HOW THIS WAS FOUND. The subtitle string hashes were known from parsing
//     Locl; a byte search for those 4 bytes across the game's files produced
//     exactly two hits — core.lvl (the text itself) and mission.lvl. The
//     format was then verified against the game's own machine code: at VA
//     0x006F6089 BattlefrontII.exe literally contains
//     `cmp eax, 0x115BFCB9` (the hash of the "subtitle" directive), after
//     which the engine reads a uint hash and two floats, computes
//     `end = start + duration`, builds 12-byte records (capped at 256) and
//     stores the vector in config fields +0x18/+0x1C/+0x20, with the font
//     hash at +0x14. So the parsing below mirrors the real parser rather
//     than interpreting the data.
//
//     FORMAT (shared by BF2 ucfb configs: .hud, .snd, mcfg):
//       DATA = [directive hash : 4][value count : 1]
//              [values × 4 bytes][string-block length : 4][strings\0…]
//       SCOP = a nested scope (children are again DATA/SCOP)
//     A value is either a float or a uint (hash); the directive decides,
//     nothing in the file distinguishes them, so both readings are kept.
//
//     THIS IS A READER ONLY. It changes nothing and invents nothing: unknown
//     directives are returned as their raw hash so the caller can honestly
//     report what has not been identified yet, instead of silently skipping.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;

namespace BF1LocalizationTool.Core.Bf2Movies;

// UA: Відомі директиви mcfg. Імена розкрито підбором за FNV-1a проти
//     рядків самого BattlefrontII.exe — тобто це справжні імена з гри, а не
//     довільно підібрані підписи. Три хеші лишились нерозкритими; вони свідомо
//     не перейменовані, щоб не видавати здогад за факт.
// EN: Known mcfg directives. The names were recovered by FNV-1a matching
//     against strings inside BattlefrontII.exe itself — i.e. these are the
//     game's real names, not invented labels. Three hashes remain
//     unresolved; they are deliberately left un-named rather than dressed up
//     as facts.
public static class MovieConfigDirectives
{
    public const uint MovieProperties = 0xB0E44C26; // movieproperties
    public const uint Name = 0x8D39BDE6;            // name
    public const uint Movie = 0xAEF39CCF;           // movie
    public const uint SegmentList = 0x0A8325F2;     // segmentlist
    public const uint Segment = 0xF3C342E6;         // segment
    public const uint Font = 0x274E1290;            // font
    public const uint Color = 0x3D7E6258;           // color
    public const uint SubtitleList = 0x09836605;    // subtitlelist
    public const uint Subtitle = 0x115BFCB9;        // subtitle
    public const uint FadeInTime = 0x5083ECD9;      // fadeintime
    public const uint FadeOutTime = 0x798DC484;     // fadeouttime

    // UA: Ще не розкриті. 0xE396FE20 відкриває блок одного сегмента,
    //     0xCA04EFE0 несе хеш файлу ролика, 0x4D5AF670 трапляється в
    //     обробнику запуску. Імена невідомі — саме тому вони тут числами.
    // EN: Not yet resolved. 0xE396FE20 opens a per-segment block, 0xCA04EFE0
    //     carries the movie-file hash, 0x4D5AF670 appears in the start
    //     handler. Their names are unknown — hence the raw numbers.
    public const uint UnknownSegmentBlock = 0xE396FE20;
    public const uint UnknownMovieFile = 0xCA04EFE0;
    public const uint UnknownStartField = 0x4D5AF670;

    public static string Describe(uint hash) => hash switch
    {
        MovieProperties => "movieproperties",
        Name => "name",
        Movie => "movie",
        SegmentList => "segmentlist",
        Segment => "segment",
        Font => "font",
        Color => "color",
        SubtitleList => "subtitlelist",
        Subtitle => "subtitle",
        FadeInTime => "fadeintime",
        FadeOutTime => "fadeouttime",
        UnknownSegmentBlock => "<блок сегмента / segment block>",
        UnknownMovieFile => "<файл ролика / movie file>",
        UnknownStartField => "<поле запуску / start field>",
        _ => $"0x{hash:X8}",
    };

    // UA: Чи є хеш відомою директивою — потрібне, щоб звіт міг окремо
    //     перелічити те, що ще не з'ясовано.
    // EN: Whether the hash is a known directive — lets the report list
    //     separately what still isn't known.
    public static bool IsKnown(uint hash) => Describe(hash)[0] != '0';
}

// UA: Одне значення директиви у двох прочитаннях одночасно.
// EN: A single directive value, in both readings at once.
public readonly record struct MovieConfigValue(uint Raw, float Number);

// UA: Один рядок субтитра. DataOffset — абсолютне зміщення першого байта
//     ПАЙЛОАДУ його DATA-чанка у файлі; воно потрібне, щоб змінити час
//     на місці, не зачіпаючи розмірів (див. MovieSubtitleProbePatcher).
// EN: One subtitle line. DataOffset is the absolute file offset of the first
//     byte of its DATA chunk PAYLOAD; it is what allows editing the timing in
//     place without touching any sizes (see MovieSubtitleProbePatcher).
public sealed record MovieSubtitleLine
{
    public required uint LocalizationHash { get; init; }
    public required float StartSeconds { get; init; }
    public required float DurationSeconds { get; init; }
    public required long DataOffset { get; init; }

    public float EndSeconds => StartSeconds + DurationSeconds;
}

// UA: Один сегмент ролика зі своїми субтитрами.
//
//     MovieNameHash / MovieFileHash — хеші директив "name"/"movie" з
//     БАТЬКІВСЬКОГО блоку "movieproperties" (ім'я й файл усього ролика, а
//     не сегмента). Обидві директиви читаються як values[0].Raw — числовий
//     хеш, а не рядок: супровідний рядковий блок payload'у для "name"/
//     "movie" завжди порожній (довжина=0). Перевірка підбором хеша:
//     fnv1a("ingame") == 0x985C8F54 == прочитане значення директиви
//     "movie". Нульове значення на mission.lvl/shell.lvl означає не
//     помилку читання, а відсутність самих директив у файлі: Flush()
//     додає сегмент лише за наявності субтитрів, а movieproperties у
//     inshell.lvl субтитрів не має.
// EN: One movie segment together with its subtitles.
//
//     MovieNameHash / MovieFileHash — the "name"/"movie" directive hashes
//     from the PARENT "movieproperties" block (the whole movie's name and
//     file, not the segment's). Both directives are read as values[0].Raw —
//     a numeric hash, not a string: the payload's string block for
//     "name"/"movie" is always empty (length=0). Confirmed by a hash
//     match: fnv1a("ingame") == 0x985C8F54 == the read "movie" directive
//     value. A zero value on mission.lvl/shell.lvl does not mean a read
//     failure, only that the directives are simply absent there: Flush()
//     only adds a segment when it has subtitles, and inshell.lvl's
//     movieproperties has none.
public sealed record MovieSegmentConfig
{
    public required uint SegmentHash { get; init; }
    public uint FontHash { get; init; }
    public float[]? Color { get; init; }
    public uint MovieNameHash { get; init; }
    public uint MovieFileHash { get; init; }
    public required IReadOnlyList<MovieSubtitleLine> Subtitles { get; init; }
}

public sealed record MovieConfigScanResult
{
    public required string SourceFile { get; init; }
    public required IReadOnlyList<MovieSegmentConfig> Segments { get; init; }

    // UA: Хеші директив, яких немає в MovieConfigDirectives — чесний перелік
    //     того, що лишилось нерозпізнаним у цьому файлі.
    // EN: Directive hashes absent from MovieConfigDirectives — an honest list
    //     of what remains unidentified in this file.
    public required IReadOnlyDictionary<uint, int> UnknownDirectives { get; init; }

    public int SubtitleCount => Segments.Sum(s => s.Subtitles.Count);
}

public static class MovieConfigChunk
{
    // -------------------------------------------------------------------------
    // UA: Розбирає один DATA-пайлоад. Повертає false, якщо байтів менше за
    //     мінімально можливий запис — краще пропустити з поверненням false,
    //     ніж читати за межами масиву.
    // EN: Parses one DATA payload. Returns false if there are fewer bytes than
    //     the smallest possible record — better to skip with false than to
    //     read past the end of the array.
    // -------------------------------------------------------------------------
    public static bool TryParseData(byte[] raw, out uint hash, out MovieConfigValue[] values)
        => TryParseData(raw, out hash, out values, out _);

    // -------------------------------------------------------------------------
    // UA: Те саме, але й з рядковим блоком payload'у (хвіст
    //     "[довжина:4][рядки\0…]" з формату вгорі файлу). Для "name"/"movie"
    //     цей блок ЗАВЖДИ порожній (довжина=0) — ім'я й файл ролика лежать
    //     не тут, а в НУМЕРИЧНОМУ значенні (values[0]), як і решта
    //     директив; перевірено побайтово (див. коментар до
    //     MovieSegmentConfig). Метод лишається — рядковий блок теоретично
    //     може нести дані для директив, ще не зустрінутих.
    // EN: The same, but with the payload's string block too (the
    //     "[length:4][strings\0…]" tail from the format at the top of this
    //     file). For "name"/"movie" this block is ALWAYS empty (length=0) —
    //     the movie's name and file live not here but in the NUMERIC value
    //     (values[0]), like every other directive; verified byte-for-byte
    //     (see the comment on MovieSegmentConfig). The method
    //     stays — the string block may still carry data for directives not
    //     yet encountered.
    // -------------------------------------------------------------------------
    public static bool TryParseData(byte[] raw, out uint hash, out MovieConfigValue[] values,
        out string[] strings)
    {
        hash = 0;
        values = [];
        strings = [];
        if (raw.Length < 5)
            return false;

        hash = BitConverter.ToUInt32(raw, 0);
        int count = raw[4];
        var end = 5 + 4 * count;
        if (end > raw.Length)
            return false;

        values = new MovieConfigValue[count];
        for (var i = 0; i < count; i++)
        {
            var at = 5 + 4 * i;
            values[i] = new MovieConfigValue(
                BitConverter.ToUInt32(raw, at),
                BitConverter.ToSingle(raw, at));
        }

        // UA: Рядковий блок — необов'язковий хвіст. Коротші payload'и (як у
        //     "segment"/"font"/"subtitle") на ньому просто закінчуються, тож
        //     відсутність цих 4 байтів — не помилка формату.
        // EN: The string block is an optional tail. Shorter payloads (like
        //     "segment"/"font"/"subtitle") simply end before it, so the
        //     absence of these 4 bytes is not a format error.
        if (end + 4 > raw.Length)
            return true;

        var stringBlockLen = (int)BitConverter.ToUInt32(raw, end);
        var stringsStart = end + 4;
        if (stringBlockLen <= 0 || stringsStart + stringBlockLen > raw.Length)
            return true;

        var text = System.Text.Encoding.ASCII.GetString(raw, stringsStart, stringBlockLen);
        strings = text.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        return true;
    }

    // -------------------------------------------------------------------------
    // UA: Обходить усі "mcfg" чанки дерева й збирає сегменти з субтитрами.
    //     Стан (поточний сегмент/шрифт/колір) тягнеться по обходу так само,
    //     як його тягне рушій: директиви застосовуються до блоку, у якому
    //     зустрілись.
    // EN: Walks every "mcfg" chunk in the tree and collects segments with
    //     subtitles. State (current segment/font/colour) is carried along the
    //     walk exactly as the engine carries it: a directive applies to the
    //     block it appears in.
    // -------------------------------------------------------------------------
    public static MovieConfigScanResult Scan(UcfbChunk root, string sourceFile)
    {
        var segments = new List<MovieSegmentConfig>();
        var unknown = new Dictionary<uint, int>();

        foreach (var mcfg in BF1LocalizationTool.Core.IO.UcfbReader.FindAll(root, "mcfg"))
            WalkBlock(mcfg, segments, unknown);

        return new MovieConfigScanResult
        {
            SourceFile = sourceFile,
            Segments = segments,
            UnknownDirectives = unknown,
        };
    }

    private sealed class BlockState
    {
        public uint SegmentHash;
        public uint FontHash;
        public float[]? Color;
        public List<MovieSubtitleLine> Subtitles = [];
        public bool HasSubtitleList;

        // UA: Ім'я/файл ролика з "movieproperties" — рівень ВИЩИЙ за сегмент,
        //     тому на новому "segment" ЦІ поля НЕ скидаються: вони лишаються
        //     чинними для всіх сегментів одного movieproperties, аж доки їх
        //     не перепише наступна пара name/movie.
        // EN: The movie's name/file from "movieproperties" — a level ABOVE
        //     the segment, so these are NOT reset on a new "segment": they
        //     stay valid for every segment of one movieproperties, until the
        //     next name/movie pair overwrites them.
        public uint MovieNameHash;
        public uint MovieFileHash;
    }

    private static void WalkBlock(UcfbChunk node, List<MovieSegmentConfig> segments,
        Dictionary<uint, int> unknown)
    {
        var state = new BlockState();
        WalkInto(node, state, segments, unknown);
        Flush(state, segments);
    }

    private static void WalkInto(UcfbChunk node, BlockState state,
        List<MovieSegmentConfig> segments, Dictionary<uint, int> unknown)
    {
        foreach (var child in node.Children)
        {
            if (child.FourCC == "DATA")
            {
                if (!TryParseData(child.RawData, out var hash, out var values))
                    continue;

                if (!MovieConfigDirectives.IsKnown(hash))
                    unknown[hash] = unknown.GetValueOrDefault(hash) + 1;

                switch (hash)
                {
                    case MovieConfigDirectives.Segment when values.Length > 0:
                        // UA: Новий сегмент — попередній завершено.
                        // EN: A new segment — the previous one is complete.
                        Flush(state, segments);
                        state.SegmentHash = values[0].Raw;
                        state.FontHash = 0;
                        state.Color = null;
                        state.Subtitles = [];
                        state.HasSubtitleList = false;
                        break;

                    case MovieConfigDirectives.Font when values.Length > 0:
                        state.FontHash = values[0].Raw;
                        break;

                    case MovieConfigDirectives.Name when values.Length > 0:
                        state.MovieNameHash = values[0].Raw;
                        break;

                    case MovieConfigDirectives.Movie when values.Length > 0:
                        state.MovieFileHash = values[0].Raw;
                        break;

                    case MovieConfigDirectives.Color when values.Length >= 3:
                        state.Color = values.Select(v => v.Number).ToArray();
                        break;

                    case MovieConfigDirectives.SubtitleList:
                        state.HasSubtitleList = true;
                        break;

                    case MovieConfigDirectives.Subtitle when values.Length >= 3:
                        state.Subtitles.Add(new MovieSubtitleLine
                        {
                            LocalizationHash = values[0].Raw,
                            StartSeconds = values[1].Number,
                            DurationSeconds = values[2].Number,
                            DataOffset = child.FileDataOffset,
                        });
                        break;
                }
            }
            else if (child.FourCC == "SCOP")
            {
                WalkInto(child, state, segments, unknown);
            }
        }
    }

    private static void Flush(BlockState state, List<MovieSegmentConfig> segments)
    {
        if (state.SegmentHash == 0 || state.Subtitles.Count == 0)
            return;

        segments.Add(new MovieSegmentConfig
        {
            SegmentHash = state.SegmentHash,
            FontHash = state.FontHash,
            Color = state.Color,
            MovieNameHash = state.MovieNameHash,
            MovieFileHash = state.MovieFileHash,
            Subtitles = state.Subtitles,
        });

        state.Subtitles = [];
        state.SegmentHash = 0;
    }
}
