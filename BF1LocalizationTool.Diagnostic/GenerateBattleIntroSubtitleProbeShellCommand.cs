// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateBattleIntroSubtitleProbeShellCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: ДІАГНОСТИЧНИЙ, ОДНОРАЗОВИЙ інструмент — НЕ частина production-патча.
//     Записує окремий shell_subtitle_probe.lvl, у якому екран вступного
//     ролика кампанії ("ifs_campaign_battle_intro") отримує ОДИН статичний
//     текстовий об'єкт ("TEST") по центру екрана — перевірка гіпотези, чи
//     взагалі малюється звичайний Lua/IFText поверх ролика (без жодного
//     патчу .exe). Див. розгорнутий коментар у
//     BF1LocalizationTool.Core/Bf2Widescreen/BattleIntroSubtitleProbePatcher.cs.
//
//     Результат — суто діагностичний: "так, малюється" чи "ні". Ніколи не
//     заявляти "виправлено" без реального знімка екрана з грі — це ще НЕ
//     фікс субтитрів, а перший, найдешевший крок перед побудовою
//     реального таймінгу.
// EN: A DIAGNOSTIC, ONE-OFF tool — NOT part of the production patch. Writes a
//     separate shell_subtitle_probe.lvl in which the campaign intro movie
//     screen ("ifs_campaign_battle_intro") gets ONE static text object
//     ("TEST") centered on screen — a test of the hypothesis that an
//     ordinary Lua/IFText renders on top of the movie at all (no .exe patch
//     involved). See the detailed comment in
//     BF1LocalizationTool.Core/Bf2Widescreen/BattleIntroSubtitleProbePatcher.cs.
//
//     The result is purely diagnostic: "yes, it renders" or "no". Never
//     claim "fixed" without a real in-game screenshot — this is NOT yet a
//     subtitle fix, just the first, cheapest step before building real
//     timing.
// =============================================================================

using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateBattleIntroSubtitleProbeShellCommand
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
        const string screenName = BattleIntroSubtitleProbePatcher.TargetScreenName;

        var scripts = ScriptChunkLocator.FindAll(root);
        if (scripts.All(s => s.Name != screenName))
        {
            report.Log($"UA: '{screenName}' не знайдено — зонд неможливий на цьому файлі.");
            report.Log($"EN: '{screenName}' not found — cannot probe this file.");
            return null;
        }

        report.Log("UA: ДІАГНОСТИЧНИЙ ЗОНД — не production. Додає ОДИН статичний IFText (\"TEST\")");
        report.Log("UA: по центру екрана вступного ролика кампанії, як нове поле таблиці екрана");
        report.Log("UA: (AddIFScreen->AddIFObjContainer сам знаходить і приєднує його — жодного");
        report.Log("UA: хука в Enter/Update, жодного патчу .exe).");
        report.Log("EN: DIAGNOSTIC PROBE — not production. Adds ONE static IFText (\"TEST\") centered");
        report.Log("EN: on the campaign intro movie screen, as a new screen-table field (AddIFScreen->");
        report.Log("EN: AddIFObjContainer finds and attaches it on its own — no Enter/Update hook,");
        report.Log("EN: no .exe patch).");
        report.Log();

        var spec = new BattleIntroSubtitleProbePatcher.ProbeTextSpec { Text = "TEST" };
        BattleIntroSubtitleProbePatcher.ApplyProbe(root, spec);

        var afterScripts = ScriptChunkLocator.FindAll(root);
        report.Log($"UA: top-level 'scr_' ДО={scripts.Count}, ПІСЛЯ={afterScripts.Count} (очікується: без змін).");
        report.Log($"EN: top-level 'scr_' BEFORE={scripts.Count}, AFTER={afterScripts.Count} (expected: unchanged).");

        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, outputFileName);
        UcfbWriter.WriteFile(outputPath, root);

        report.Log($"UA: Записано: \"{outputPath}\" ({new FileInfo(outputPath).Length} байт).");
        report.Log($"EN: Written: \"{outputPath}\" ({new FileInfo(outputPath).Length} bytes).");
        return outputPath;
    }
}
