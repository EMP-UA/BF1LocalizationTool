// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateMovieRectModeShellCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: Записує shell_movierect.lvl — екрани кампанійських роликів переведено
//     на ВЛАСНИЙ широкоформатний режим гри (той самий, яким ваніль коригує
//     навчальні ролики в ifs_tutorials). Зміна — рівно один операнд однієї
//     Lua-інструкції на екран; ні .exe, ні оверлеїв, ні таймерів.
//     Розгорнуте пояснення механізму — у
//     BF1LocalizationTool.Core/Bf2Widescreen/MovieRectModePatcher.cs.
//
//     МЕТА ТЕСТУ — розвилка, яку неможливо вирішити з файлів гри: підпис
//     ролика вимикається нативним кодом при аспекті ширшому за 4:3, і
//     питання лише в тому, від ЧОГО залежить це рішення — від прямокутника
//     відео (тоді цей патч його поверне) чи від аспекту екрана (тоді ні).
//     Один операнд проти повного оверлея — найдешевша можлива перевірка.
// EN: Writes shell_movierect.lvl — the campaign movie screens switched to
//     the game's OWN widescreen mode (the same one vanilla uses to correct
//     tutorial movies in ifs_tutorials). The change is exactly one operand
//     of one Lua instruction per screen; no .exe, no overlays, no timers.
//     Full explanation of the mechanism is in
//     BF1LocalizationTool.Core/Bf2Widescreen/MovieRectModePatcher.cs.
//
//     THE POINT OF THE TEST is a fork the game's files cannot settle: the
//     movie caption is switched off by native code at aspects wider than
//     4:3, and the only question is WHAT that decision keys off — the movie
//     rectangle (then this patch brings it back) or the screen aspect (then
//     it doesn't). One operand versus a full overlay is the cheapest
//     possible way to find out.
// =============================================================================

using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateMovieRectModeShellCommand
{
    public static string? Run(DiagnosticReport report, string shellLvlPath, string outputDir,
        string outputFileName)
    {
        if (!File.Exists(shellLvlPath))
        {
            report.Log($"UA: shell.lvl не знайдено за шляхом {shellLvlPath}.");
            report.Log($"EN: shell.lvl not found at {shellLvlPath}.");
            return null;
        }

        var root = UcfbReader.ReadFile(shellLvlPath);
        var scripts = ScriptChunkLocator.FindAll(root).Select(s => s.Name).ToHashSet();

        // UA: Обидва екрани кампанійських роликів. Turn intro патчимо теж —
        //     він має ту саму ваду й ту саму структуру.
        // EN: Both campaign movie screens. Turn intro is patched too — it has
        //     the same flaw and the same structure.
        var targets = new[]
        {
            MovieRectModePatcher.BattleIntroScreen,
            MovieRectModePatcher.TurnIntroScreen,
        };

        report.Log("UA: Переводимо екрани роликів на ВЛАСНИЙ широкоформатний режим гри (mode 1.0 -> 2.0).");
        report.Log("UA: На 4:3 (ws=1.0) формула режиму 2 дає той самий прямокутник, що й режим 1 —");
        report.Log("UA: тобто на 4:3/5:4 не змінюється НІЧОГО за побудовою.");
        report.Log("EN: Switching the movie screens to the game's OWN widescreen mode (mode 1.0 -> 2.0).");
        report.Log("EN: At 4:3 (ws=1.0) the mode-2 formula yields the same rect as mode 1 —");
        report.Log("EN: so at 4:3/5:4 NOTHING changes, by construction.");
        report.Log();

        var patched = 0;
        foreach (var screen in targets)
        {
            if (!scripts.Contains(screen))
            {
                report.Log($"UA: '{screen}' у цьому файлі немає — пропускаємо.");
                report.Log($"EN: '{screen}' is not in this file — skipping.");
                continue;
            }

            try
            {
                var r = MovieRectModePatcher.ApplyWidescreenMode(root, screen);
                report.Log($"UA: {r.ScreenName}: прототип {r.ProtoPath}, pc={r.Pc}, режим {r.OldMode} -> {r.NewMode}.");
                report.Log($"EN: {r.ScreenName}: prototype {r.ProtoPath}, pc={r.Pc}, mode {r.OldMode} -> {r.NewMode}.");
                patched++;
            }
            catch (Exception ex)
            {
                // UA: Не глушимо — показуємо причину й рахуємо як НЕ пропатчене.
                // EN: Not swallowed — show the reason and count it as NOT patched.
                report.Log($"UA: {screen}: НЕ пропатчено — {ex.Message}");
                report.Log($"EN: {screen}: NOT patched — {ex.Message}");
            }
        }

        if (patched == 0)
        {
            report.Log("UA: Жодного екрана не пропатчено — файл не записуємо.");
            report.Log("EN: No screen was patched — not writing a file.");
            return null;
        }

        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, outputFileName);
        UcfbWriter.WriteFile(outputPath, root);

        report.Log();
        report.Log($"UA: Записано: \"{outputPath}\" ({new FileInfo(outputPath).Length} байт), екранів: {patched}.");
        report.Log($"EN: Written: \"{outputPath}\" ({new FileInfo(outputPath).Length} bytes), screens: {patched}.");
        return outputPath;
    }
}
