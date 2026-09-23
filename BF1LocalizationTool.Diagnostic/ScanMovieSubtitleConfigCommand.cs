// =============================================================================
// BF1LocalizationTool.Diagnostic — ScanMovieSubtitleConfigCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Локальний прогін по ВСІХ .lvl файлах гри у пошуку конфігу роликів
//     ("mcfg") і субтитрів у ньому.
//
//     НАВІЩО. Субтитри роликів були знайдені в mission.lvl, але перевірено
//     на той момент було рівно один файл із 505. Питання «чи не лежить
//     друга частина конфігу десь іще — у myg1.lvl, у side\, у sound\»
//     залишалось відкритим, і відповісти на нього припущенням не можна.
//     Ця команда відповідає на нього перебором: вона проходить усе дерево
//     гри й показує, у яких саме файлах є "mcfg", скільки там сегментів і
//     субтитрів, і — окремо — які директиви ще НЕ розпізнано.
//
//     ЧОМУ САМЕ ЛОКАЛЬНО. Обхід кількох гігабайтів .lvl — робота для
//     процесора, а не для аналізу вручну; на звичайному сучасному CPU це
//     хвилини. Назовні віддається короткий звіт, а не гігабайти даних.
//
//     ЩО ЦЕ НЕ РОБИТЬ. Нічого не змінює у файлах гри — лише читає.
// EN: A local sweep over ALL of the game's .lvl files looking for the movie
//     configuration chunk ("mcfg") and the subtitles inside it.
//
//     WHY. Movie subtitles were found in mission.lvl, but at that point
//     exactly one file out of 505 had been checked. The question "is a second
//     part of the config sitting somewhere else — in myg1.lvl, side\, sound\"
//     was still open, and it cannot be answered by assumption. This command
//     answers it by brute force: it walks the whole game tree and reports
//     which files contain "mcfg", how many segments and subtitles are in
//     them, and — separately — which directives have NOT been identified yet.
//
//     WHY LOCALLY. Walking several gigabytes of .lvl files is a job for a
//     CPU, not for manual analysis; on an ordinary modern CPU it is minutes.
//     What leaves the machine is a short report, not gigabytes of data.
//
//     WHAT IT DOES NOT DO. It never modifies the game's files — read only.
// =============================================================================

using System.Diagnostics;
using BF1LocalizationTool.Core.Bf2Movies;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Localization;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class ScanMovieSubtitleConfigCommand
{
    public static void Run(DiagnosticReport report, string gameRootDir)
    {
        if (!Directory.Exists(gameRootDir))
        {
            report.Log($"UA: Теку не знайдено: {gameRootDir}");
            report.Log($"EN: Directory not found: {gameRootDir}");
            return;
        }

        var files = Directory.EnumerateFiles(gameRootDir, "*.lvl", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        report.Log($"UA: Файлів .lvl до перевірки: {files.Count}");
        report.Log($"EN: .lvl files to check: {files.Count}");
        report.Log();

        var sw = Stopwatch.StartNew();
        var results = new List<MovieConfigScanResult>();
        var nameCandidates = new HashSet<string>(StringComparer.Ordinal);
        long totalBytes = 0;
        var failed = new List<(string File, string Error)>();

        // UA: ДІАГНОСТИКА "name"/"movie" НАЖИВО. Дві спроби прочитати ці
        //     директиви (числовий хеш, потім рядковий блок) обидві дали
        //     0x0 / порожньо геть у КОЖНОГО сегмента без винятку — а це вже
        //     не "не резолвиться", це "гадаємо наосліп по коментарю, а не
        //     дивимось на байти". Тому замість третьої здогадки — сирий
        //     hex перших знайдених "name"/"movie" DATA-чанків, щоб побудувати
        //     розбір за фактом, а не за формулою з коментаря.
        // EN: LIVE "name"/"movie" DIAGNOSTICS. Two attempts to read these
        //     directives (a numeric hash, then a string block) both came back
        //     0x0 / empty for EVERY segment without exception — that is no
        //     longer "does not resolve", it is "guessing blind from a comment
        //     instead of looking at the bytes". So instead of a third guess:
        //     the raw hex of the first few "name"/"movie" DATA chunks found,
        //     to build the parser from fact rather than from a documented
        //     formula.
        var nameMovieDumps = new List<string>();
        const int MaxNameMovieDumps = 20;

        // UA: Статус прогресу — ПРЯМО в консоль (report.Log лише накопичує
        //     буфер і виводиться цілком аж на report.Save() в кінці, тож без
        //     цього екран мовчить на весь час обходу, і незрозуміло, чи
        //     програма ще працює, чи зависла). Раз на ~2 с або раз на 25
        //     файлів — що настане раніше — один рядок з номером файлу.
        // EN: Progress status — straight to the console (report.Log only
        //     accumulates a buffer and prints it all at once on report.Save()
        //     at the end, so without this the screen stays silent for the
        //     whole sweep and it is unclear whether the program is still
        //     working or has hung). Roughly every 2 s or every 25 files,
        //     whichever comes first — one line with the file number.
        var lastStatusAt = sw.Elapsed;
        var processed = 0;

        foreach (var path in files)
        {
            try
            {
                totalBytes += new FileInfo(path).Length;
                var root = UcfbReader.ReadFile(path);

                var scan = MovieConfigChunk.Scan(root, path);
                if (scan.Segments.Count > 0 || scan.UnknownDirectives.Count > 0)
                    results.Add(scan);

                if (nameMovieDumps.Count < MaxNameMovieDumps)
                {
                    foreach (var mcfg in UcfbReader.FindAll(root, "mcfg"))
                        DumpNameMovieRawBytes(mcfg, path, nameMovieDumps, MaxNameMovieDumps);
                }

                // UA: Попутно збираємо рядкові константи скриптів — з них
                //     потім розкриваємо хеші імен сегментів. Це той самий
                //     прийом, що вже застосований у MvsContainerScanCommand.
                // EN: Along the way, script string constants are collected — they
                //     later resolve the segment-name hashes. Same technique
                //     already used in MvsContainerScanCommand.
                foreach (var script in ScriptChunkLocator.FindAll(root))
                {
                    LuaChunkParseResult parsed;
                    try { parsed = Lua50BytecodeReader.Parse(script.BodyChunk.RawData); }
                    catch { continue; }
                    CollectStrings(parsed.Root, nameCandidates);
                }
            }
            catch (Exception ex)
            {
                failed.Add((path, ex.Message));
            }
            finally
            {
                processed++;
                if (processed % 25 == 0 || sw.Elapsed - lastStatusAt >= TimeSpan.FromSeconds(2)
                    || processed == files.Count)
                {
                    Console.WriteLine(
                        $"UA: ...оброблено {processed}/{files.Count} ({totalBytes / 1048576.0:F0} МБ, " +
                        $"{sw.Elapsed.TotalSeconds:F0} с) — {Path.GetFileName(path)}");
                    lastStatusAt = sw.Elapsed;
                }
            }
        }
        Console.WriteLine();

        sw.Stop();
        report.Log($"UA: Прочитано {totalBytes / 1048576.0:F1} МБ за {sw.Elapsed.TotalSeconds:F1} с.");
        report.Log($"EN: Read {totalBytes / 1048576.0:F1} MB in {sw.Elapsed.TotalSeconds:F1} s.");
        if (failed.Count > 0)
        {
            report.Log($"UA: Не вдалося розібрати файлів: {failed.Count} (перелічені нижче).");
            report.Log($"EN: Files that failed to parse: {failed.Count} (listed below).");
            foreach (var (f, e) in failed.Take(20))
                report.Log($"    {Path.GetFileName(f)}: {e}");
        }
        report.Log();

        // UA: Хеш → ім'я сегмента, за зібраними рядками.
        //
        //     Зібрані рядки — це УСІ рядкові константи Lua, а не лише
        //     ключі-ідентифікатори: серед них трапляється й показуваний
        //     текст (наприклад, імена в титрах), який ЗАКОНОМІРНО може
        //     містити не-ASCII. SwbfStringHash.Compute навмисно хешує лише
        //     ключі (їхній формат — суто ASCII-шлях на кшталт
        //     "level.tat3.objectives.1") і кидає виняток на будь-що інше —
        //     тому такі рядки відсіюємо ще до виклику, а не покладаємось
        //     на виняток як на механізм фільтрації.
        // EN: Hash → segment name, from the collected strings.
        //
        //     The collected strings are ALL Lua string constants, not only
        //     key identifiers: some of them are displayed text (e.g. credits
        //     names), which can legitimately be non-ASCII. SwbfStringHash.
        //     Compute deliberately hashes only keys (their format is a pure
        //     ASCII dotted path like "level.tat3.objectives.1") and throws on
        //     anything else — so such strings are filtered out before the
        //     call, rather than relying on the exception as a filter.
        var byHash = new Dictionary<uint, string>();
        foreach (var s in nameCandidates)
        {
            if (!IsAsciiOnly(s))
                continue;

            byHash.TryAdd(SwbfStringHash.Compute(s), s);
        }

        report.Log("UA: === Файли, що містять конфіг роликів (mcfg) ===");
        report.Log("EN: === Files containing movie configuration (mcfg) ===");
        if (results.Count == 0)
        {
            report.Log("UA: ЖОДНОГО. Це означало б, що mission.lvl теж не прочитався — перевірте шлях.");
            report.Log("EN: NONE. That would mean mission.lvl did not parse either — check the path.");
            return;
        }

        foreach (var r in results)
        {
            report.Log($"  {Path.GetFileName(r.SourceFile)}: сегментів/segments={r.Segments.Count}, " +
                       $"субтитрів/subtitles={r.SubtitleCount}");
            report.Log($"      {r.SourceFile}");
        }

        report.Log();
        report.Log("UA: === Сирі байти перших \"name\"/\"movie\" DATA-чанків ===");
        report.Log("EN: === Raw bytes of the first \"name\"/\"movie\" DATA chunks ===");
        if (nameMovieDumps.Count == 0)
        {
            report.Log("UA: Жодного DATA-чанка з хешем 'name'/'movie' у жодному mcfg не зустрілось —");
            report.Log("UA: тобто цих директив тут узагалі немає, і питання не в форматі значення.");
            report.Log("EN: No DATA chunk with the 'name'/'movie' hash occurred in any mcfg at all —");
            report.Log("EN: meaning these directives are simply absent here, not misparsed.");
        }
        else
        {
            foreach (var line in nameMovieDumps)
                report.Log(line);
        }

        report.Log();
        report.Log("UA: === Сегменти з субтитрами (з таймінгами) ===");
        report.Log("EN: === Segments with subtitles (with timings) ===");
        foreach (var r in results)
        {
            foreach (var seg in r.Segments)
            {
                var name = byHash.TryGetValue(seg.SegmentHash, out var n) ? n : $"0x{seg.SegmentHash:X8}";
                var colour = seg.Color is { Length: >= 3 }
                    ? $"color=({string.Join(", ", seg.Color.Select(c => c.ToString("0.#")))})"
                    : "color=<не задано / not set>";

                // UA: Ім'я/файл ролика з БАТЬКІВСЬКОГО movieproperties — це
                //     ЧИСЛОВИЙ хеш (як і хеш сегмента), тому резолвиться він
                //     через той самий пул рядків. Підтверджено побайтово:
                //     fnv1a("ingame") == прочитане значення
                //     "movie" в inshell.lvl.
                // EN: The movie's name/file from the PARENT movieproperties is
                //     a NUMERIC hash (just like the segment hash), so it is
                //     resolved through the same string pool. Confirmed
                //     byte-for-byte: fnv1a("ingame") == the
                //     read "movie" value in inshell.lvl.
                var movieName = seg.MovieNameHash != 0
                    ? (byHash.TryGetValue(seg.MovieNameHash, out var mn) ? mn : $"0x{seg.MovieNameHash:X8}")
                    : "<немає / none>";
                var movieFile = seg.MovieFileHash != 0
                    ? (byHash.TryGetValue(seg.MovieFileHash, out var mf) ? mf : $"0x{seg.MovieFileHash:X8}")
                    : "<немає / none>";

                report.Log($"  [{Path.GetFileName(r.SourceFile)}] {name}: {seg.Subtitles.Count} рядк. / lines, " +
                           $"font=0x{seg.FontHash:X8}, {colour}");
                report.Log($"      movieproperties.name={movieName}, movieproperties.movie={movieFile}");
                foreach (var line in seg.Subtitles)
                {
                    report.Log($"        {line.StartSeconds,7:F2} с .. {line.EndSeconds,7:F2} с  " +
                               $"(трив./dur {line.DurationSeconds,5:F2})  hash=0x{line.LocalizationHash:X8}  " +
                               $"@0x{line.DataOffset:X}");
                }
            }
        }

        // UA: Найважливіша частина звіту — те, що ще НЕ з'ясовано.
        // EN: The most important part of the report — what is NOT known yet.
        report.Log();
        report.Log("UA: === Нерозпізнані директиви mcfg ===");
        report.Log("EN: === Unidentified mcfg directives ===");
        var allUnknown = new Dictionary<uint, int>();
        foreach (var r in results)
            foreach (var (h, c) in r.UnknownDirectives)
                allUnknown[h] = allUnknown.GetValueOrDefault(h) + c;

        if (allUnknown.Count == 0)
        {
            report.Log("UA: Немає — усі директиви впізнані.");
            report.Log("EN: None — every directive is recognised.");
        }
        else
        {
            foreach (var (h, c) in allUnknown.OrderByDescending(kv => kv.Value))
                report.Log($"    0x{h:X8}  × {c}");
            report.Log("UA: Якщо серед них є директива з позицією або аспектом — це саме те, що шукається.");
            report.Log("EN: If any of these is a position or aspect directive, that is exactly what this search is for.");
        }
    }

    // -------------------------------------------------------------------------
    // UA: Чи складається рядок лише з ASCII. Ключі гри (те, що дійсно варто
    //     подавати в SwbfStringHash.Compute) — завжди ASCII за форматом;
    //     показуваний текст, зібраний разом з ними з тих самих Lua-констант,
    //     може бути будь-яким — і саме тому фільтр стоїть тут, а не
    //     покладається на виняток усередині Compute.
    // EN: Whether a string is ASCII-only. The game's keys (what is actually
    //     worth feeding to SwbfStringHash.Compute) are always ASCII by
    //     format; displayed text, collected together with them from the same
    //     Lua constants, can be anything — hence the filter lives here,
    //     rather than relying on the exception inside Compute.
    // -------------------------------------------------------------------------
    private static bool IsAsciiOnly(string s)
    {
        foreach (var ch in s)
            if (ch > 0x7F)
                return false;
        return true;
    }

    // -------------------------------------------------------------------------
    // UA: Знаходить перші кілька DATA-чанків із хешем "name"/"movie" в
    //     дереві mcfg і дописує в `into` їхній повний сирий hex — без жодної
    //     інтерпретації формату, щоб побудувати розбір за фактичними байтами,
    //     а не за вже двічі помильним припущенням.
    // EN: Finds the first few DATA chunks with the "name"/"movie" hash in the
    //     mcfg tree and appends their full raw hex to `into` — with no
    //     interpretation of the format at all, so the parser can be built
    //     from the actual bytes instead of an assumption that has already
    //     been wrong twice.
    // -------------------------------------------------------------------------
    private static void DumpNameMovieRawBytes(UcfbChunk node, string sourceFile,
        List<string> into, int max)
    {
        foreach (var child in node.Children)
        {
            if (into.Count >= max)
                return;

            if (child.FourCC == "DATA" && child.RawData.Length >= 4)
            {
                var hash = BitConverter.ToUInt32(child.RawData, 0);
                if (hash == MovieConfigDirectives.Name || hash == MovieConfigDirectives.Movie)
                {
                    var label = hash == MovieConfigDirectives.Name ? "name" : "movie";
                    var hex = Convert.ToHexString(child.RawData);
                    into.Add($"  [{Path.GetFileName(sourceFile)}] '{label}' @0x{child.FileDataOffset:X}, " +
                             $"довжина/length={child.RawData.Length}: {hex}");
                }
            }
            else if (child.FourCC == "SCOP")
            {
                DumpNameMovieRawBytes(child, sourceFile, into, max);
            }
        }
    }

    private static void CollectStrings(LuaFunctionPrototype proto, HashSet<string> into)
    {
        foreach (var k in proto.Constants)
            if (k.Kind == LuaConstantKind.String && k.StringValue is { Length: > 0 } s)
                into.Add(s);

        foreach (var nested in proto.NestedPrototypes)
            CollectStrings(nested, into);
    }
}
