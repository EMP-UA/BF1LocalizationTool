// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateMovieSubtitleFontProbeCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: Записує `mission_subtitle_font_probe.lvl` — копію `mission.lvl`, у якій
//     шрифт субтитрів роликів (`gamefont_small`) замінено на інший
//     (за замовчуванням `gamefont_large`). Механізм і його обґрунтування —
//     у BF1LocalizationTool.Core/Bf2Movies/MovieSubtitleFontPatcher.cs.
//
//     ЩО ЦЕЙ ТЕСТ РОЗДІЛЯЄ. Статичний аналіз exe (лише читання) показав, що
//     в шляху субтитрів немає жодної умови за аспектом, шрифт `gamefont_small`
//     реально існує, а видимість керується лише опцією профілю. Лишилось
//     дві можливості, і цей зонд їх розводить:
//       • підпис З'ЯВИВСЯ на 16:9 -> справа в самому шрифті/атласі на цій
//         роздільності -> лікується ДАНИМИ;
//       • підпис НЕ з'явився -> текстовий об'єкт не потрапляє в список
//         малювання -> шукати далі саме там.
//     Обидва результати корисні, тому «невдалого» результату тут немає.
//
// EN: Writes `mission_subtitle_font_probe.lvl` — a copy of `mission.lvl` with
//     the movie-subtitle font (`gamefont_small`) swapped for another one
//     (`gamefont_large` by default). The mechanism and its rationale live in
//     BF1LocalizationTool.Core/Bf2Movies/MovieSubtitleFontPatcher.cs.
//
//     WHAT THIS TEST SEPARATES. Static analysis of the exe (read-only) showed
//     that the subtitle path carries no aspect condition, that `gamefont_small`
//     really exists, and that visibility is driven only by the profile
//     option. Two possibilities remain, and this probe tells them apart:
//       • the caption APPEARS at 16:9 -> the font/atlas at that resolution is
//         to blame -> fixable in DATA;
//       • the caption does NOT appear -> the text object never reaches the
//         draw list -> keep looking there.
//     Both outcomes are informative, so there is no "failed" result here.
// =============================================================================

using BF1LocalizationTool.Core.Bf2Movies;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateMovieSubtitleFontProbeCommand
{
    public static string? Run(DiagnosticReport report, string missionLvlPath, string outputDir,
        string outputFileName, uint newFontHash)
    {
        if (!File.Exists(missionLvlPath))
        {
            report.Log($"UA: mission.lvl не знайдено за шляхом {missionLvlPath}.");
            report.Log($"EN: mission.lvl not found at {missionLvlPath}.");
            return null;
        }

        var root = UcfbReader.ReadFile(missionLvlPath);

        MovieSubtitleFontPatcher.FontPatchResult result;
        try
        {
            result = MovieSubtitleFontPatcher.Apply(root, newFontHash);
        }
        catch (Exception ex)
        {
            // UA: Не глушимо — показуємо причину й не пишемо файл.
            // EN: Not swallowed — show the reason and write nothing.
            report.Log($"UA: Патч не застосовано — {ex.Message}");
            report.Log($"EN: Patch not applied — {ex.Message}");
            return null;
        }

        report.Log($"UA: Директив 'font' змінено: {result.PatchedDirectives}.");
        report.Log($"EN: 'font' directives changed: {result.PatchedDirectives}.");
        report.Log($"UA: Було / EN: was : {MovieSubtitleFontPatcher.DescribeFont(result.OldHash)} " +
                   $"(0x{result.OldHash:X8})");
        report.Log($"UA: Стало / EN: now: {MovieSubtitleFontPatcher.DescribeFont(result.NewHash)} " +
                   $"(0x{result.NewHash:X8})");
        report.Log();

        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, outputFileName);
        UcfbWriter.WriteFile(outputPath, root);

        var original = new FileInfo(missionLvlPath).Length;
        var written = new FileInfo(outputPath).Length;
        report.Log($"UA: Записано: \"{outputPath}\" ({written} байт; оригінал {original} байт).");
        report.Log($"EN: Written: \"{outputPath}\" ({written} bytes; original {original} bytes).");

        // UA: Розмір мусить збігтися побайтово — патч змінює лише значення.
        // EN: The size must match exactly — the patch only changes values.
        if (written != original)
        {
            report.Log("UA: УВАГА: розмір відрізняється від оригіналу. Патч мав змінювати ЛИШЕ");
            report.Log("UA: значення, не структуру — це слід перевірити, перш ніж ставити у гру.");
            report.Log("EN: WARNING: the size differs from the original. The patch should change");
            report.Log("EN: ONLY values, not structure — check this before putting it in the game.");
        }

        report.Log();
        report.Log("UA: ЯК ПЕРЕВІРЯТИ / EN: HOW TO TEST");
        report.Log("UA: 1) Зробіть резервну копію оригінального mission.lvl.");
        report.Log("UA: 2) Покладіть цей файл замість нього (перейменувавши на mission.lvl).");
        report.Log("UA: 3) Пройдіть до вступного ролика Mygeeto на 1920x1080 і зробіть знімок.");
        report.Log("UA: 4) Контроль: той самий ролик на 800x600 — підпис має бути видно завжди.");
        report.Log("EN: 1) Back up the original mission.lvl.");
        report.Log("EN: 2) Put this file in its place (renamed to mission.lvl).");
        report.Log("EN: 3) Reach the Mygeeto intro movie at 1920x1080 and take a screenshot.");
        report.Log("EN: 4) Control: the same movie at 800x600 — the caption must always show.");

        return outputPath;
    }
}
