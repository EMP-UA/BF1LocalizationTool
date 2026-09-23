// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateMovieSubtitleProbeMissionCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: Записує mission_subtitle_probe.lvl — ДІАГНОСТИЧНУ копію mission.lvl, у
//     якій першому рядку субтитрів вступного ролика Mygeeto примусово
//     виставлено `початок = 0`, `тривалість = 999` і повністю непрозорий
//     колір. Це не фікс і не production-файл: це експеримент, який має
//     розвести дві принципово різні причини зникнення субтитрів на
//     широкому екрані.
//
//     ЩО САМЕ ВІН РОЗВОДИТЬ:
//       • на 1920x1080 НІЧОГО не з'явилось  -> виклику малювання немає
//         взагалі; рішення приймається до звернення до даних, і з даних
//         його не полагодити;
//       • на 1920x1080 підпис З'ЯВИВСЯ       -> малювання відбувається, а
//         ванільна невидимість — питання часу або позиції, тобто саме те,
//         що лікується з файлів гри.
//     Контроль на 800x600 показує, що патч узагалі подіяв: там перший
//     рядок має висіти весь ролик замість своїх 2.23 секунди.
//
//     МЕЖІ ЧЕСНОСТІ. Ця команда нічого не доводить сама по собі — доводять
//     знімки з гри. Тому в звіті нижче виписано, які саме два знімки
//     потрібні й що кожен із них означає.
// EN: Writes mission_subtitle_probe.lvl — a DIAGNOSTIC copy of mission.lvl in
//     which the first subtitle line of the Mygeeto intro movie is forced to
//     `start = 0`, `duration = 999` and a fully opaque colour. This is not a
//     fix and not a production file: it is an experiment meant to separate two
//     fundamentally different causes of the subtitles vanishing on a
//     widescreen display.
//
//     WHAT IT SEPARATES:
//       • at 1920x1080 NOTHING appears  -> no draw call happens at all; the
//         decision is taken before the data is consulted, and no amount of
//         data editing can fix it;
//       • at 1920x1080 the caption APPEARS -> drawing does happen, and the
//         vanilla invisibility is a matter of timing or position — exactly
//         the kind of thing that is fixable from the game's files.
//     The 800x600 control shows the patch took effect at all: there the first
//     line should stay for the whole movie instead of its 2.23 seconds.
//
//     LIMITS OF HONESTY. This command proves nothing by itself — the in-game
//     screenshots do. Hence the report below spells out which two screenshots
//     are needed and what each of them means.
// =============================================================================

using BF1LocalizationTool.Core.Bf2Movies;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateMovieSubtitleProbeMissionCommand
{
    public static string? Run(DiagnosticReport report, string missionLvlPath, string outputDir,
        string outputFileName, uint segmentHash)
    {
        if (!File.Exists(missionLvlPath))
        {
            report.Log($"UA: mission.lvl не знайдено за шляхом {missionLvlPath}.");
            report.Log($"EN: mission.lvl not found at {missionLvlPath}.");
            return null;
        }

        var root = UcfbReader.ReadFile(missionLvlPath);

        // UA: Спершу — що взагалі є у файлі. Якщо сегмента немає, звіт має
        //     показати, які є, а не просто впасти з "не знайдено".
        // EN: First — what is actually in the file. If the segment is missing,
        //     the report must show which ones exist, not just fail with
        //     "not found".
        var scan = MovieConfigChunk.Scan(root, missionLvlPath);
        report.Log($"UA: У файлі знайдено сегментів із субтитрами: {scan.Segments.Count}, " +
                   $"рядків усього: {scan.SubtitleCount}.");
        report.Log($"EN: Segments with subtitles found in the file: {scan.Segments.Count}, " +
                   $"lines in total: {scan.SubtitleCount}.");

        var target = scan.Segments.FirstOrDefault(s => s.SegmentHash == segmentHash);
        if (target is null)
        {
            report.Log($"UA: Сегмент 0x{segmentHash:X8} у цьому файлі відсутній. Наявні:");
            report.Log($"EN: Segment 0x{segmentHash:X8} is absent from this file. Present ones:");
            foreach (var s in scan.Segments)
                report.Log($"    0x{s.SegmentHash:X8} — {s.Subtitles.Count} рядк. / lines");
            return null;
        }

        report.Log();
        report.Log($"UA: Ціль — сегмент 0x{segmentHash:X8}, рядків: {target.Subtitles.Count}. " +
                   "Ванільні таймінги ДО патча:");
        report.Log($"EN: Target — segment 0x{segmentHash:X8}, lines: {target.Subtitles.Count}. " +
                   "Vanilla timings BEFORE the patch:");
        foreach (var line in target.Subtitles)
        {
            report.Log($"        {line.StartSeconds,7:F2} с .. {line.EndSeconds,7:F2} с  " +
                       $"(трив./dur {line.DurationSeconds,5:F2})  hash=0x{line.LocalizationHash:X8}");
        }

        MovieSubtitleProbePatcher.ProbeResult result;
        try
        {
            result = MovieSubtitleProbePatcher.Apply(root, segmentHash);
        }
        catch (Exception ex)
        {
            // UA: Не глушимо — мовчазний «успіх» без змін гірший за помилку.
            // EN: Not swallowed — a silent "success" with no change is worse
            //     than an error.
            report.Log($"UA: Патч НЕ застосовано — {ex.Message}");
            report.Log($"EN: Patch NOT applied — {ex.Message}");
            return null;
        }

        report.Log();
        report.Log($"UA: Рядок 0: початок {result.OldStart:F2} -> 0.00, " +
                   $"тривалість {result.OldDuration:F2} -> 999.00. " +
                   $"Решту рядків ({result.PatchedLines - 1}) зсунуто за межі ролика. " +
                   $"Колір {(result.ColourForced ? "доведено до 255,255,255,255" : "не змінювався")}.");
        report.Log($"EN: Line 0: start {result.OldStart:F2} -> 0.00, " +
                   $"duration {result.OldDuration:F2} -> 999.00. " +
                   $"The remaining lines ({result.PatchedLines - 1}) were pushed past the movie's end. " +
                   $"Colour {(result.ColourForced ? "forced to 255,255,255,255" : "left unchanged")}.");

        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, outputFileName);
        UcfbWriter.WriteFile(outputPath, root);

        report.Log();
        report.Log($"UA: Записано: \"{outputPath}\" ({new FileInfo(outputPath).Length} байт).");
        report.Log($"EN: Written: \"{outputPath}\" ({new FileInfo(outputPath).Length} bytes).");

        // UA: Побайтова перевірка обіцянки «розмір не змінюється»: якщо
        //     довжина вихідного файлу не збіглася з вхідним — щось поза
        //     задумом, і про це треба сказати вголос, а не змовчати.
        // EN: A byte-level check of the "no size change" promise: if the
        //     output length does not match the input, something went beyond
        //     the design and must be said out loud, not passed over.
        var inLen = new FileInfo(missionLvlPath).Length;
        var outLen = new FileInfo(outputPath).Length;
        if (inLen != outLen)
        {
            report.Log($"UA: УВАГА: розмір змінився ({inLen} -> {outLen}). Патч мав змінювати ЛИШЕ числа.");
            report.Log($"EN: WARNING: size changed ({inLen} -> {outLen}). The patch was meant to change ONLY numbers.");
        }
        else
        {
            report.Log("UA: Розмір збігається з оригіналом — змінені лише значення, як і задумано.");
            report.Log("EN: Size matches the original — only values changed, as designed.");
        }

        return outputPath;
    }
}
