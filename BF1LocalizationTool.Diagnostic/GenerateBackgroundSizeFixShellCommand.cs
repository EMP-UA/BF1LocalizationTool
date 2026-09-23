// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateBackgroundSizeFixShellCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: ПІДТВЕРДЖЕНИЙ ЗНІМКАМИ фікс, УВІМКНЕНИЙ У PRODUCTION
//     (GenerateAnchorFixShellCommand -> shell_layout.lvl). Цей файл
//     лишається окремим ІЗОЛЬОВАНИМ інструментом для точкового
//     тестування нових екранів/текстур без перезбирання основного
//     патча — записує окремий shell_bgfix.lvl, у якому обгорнуто
//     глобальну ifelem_shellscreen_fnAddBackground так, щоб ПІСЛЯ
//     виклику оригіналу примусово повернути bg.localpos_r до РІВНО
//     ширини екрана (w), замість ванільного w × widescreen (≈2560 при
//     1920, тобто на 33% ширше за екран — підтверджено діагностичним
//     зондом, реальне widescreen = 1920/1080 / (800/600) ≈ 1.3333).
//
//     ЩО ПІДТВЕРДЖЕНО: пікселева звірка "до/після" показала, що
//     контейнер справді на 33% ширший за екран і обрізає праву чверть
//     фонової картинки (не "шви від тайлінгу", а обрізаний правий
//     край — корінь і фікс ті самі). Питання висоти закрито аналізом:
//     bg.localpos_b := h не має множника в жодному з перевірених
//     випадків, симетричного бага немає.
//
//     Функція ifelem_shellscreen_fnAddBackground СПІЛЬНА для 69 з 82
//     екранів ifs_* — цей файл вмикає фікс ГЛОБАЛЬНО, так само як і
//     production. З 7 реально знайдених значень bg_texture перевірено
//     4 (iface_bgmeta_space, iface_bg_1, single_player_campaign,
//     profile_manager — разом 16 з ~20 фактичних використань).
//     Лишились неперевіреними: single_player_conquest (той самий код,
//     що вже перевірений single_player_campaign), single_player_option,
//     і одне динамічне значення в ifs_tutorials — саме для таких
//     точкових перевірок і лишається ця окрема збірка.
// EN: A fix CONFIRMED BY SCREENSHOTS, ENABLED IN PRODUCTION
//     (GenerateAnchorFixShellCommand -> shell_layout.lvl). This file
//     remains a separate ISOLATED tool for spot-testing new
//     screens/textures without rebuilding the main patch — it writes a
//     separate shell_bgfix.lvl in which the global
//     ifelem_shellscreen_fnAddBackground is wrapped so that, AFTER the
//     original runs, bg.localpos_r is forced back to EXACTLY the screen
//     width (w), instead of the vanilla w x widescreen (~2560 at 1920, i.e.
//     33% wider than the screen — confirmed by the diagnostic probe, real
//     widescreen = 1920/1080 / (800/600) ~= 1.3333).
//
//     WHAT WAS CONFIRMED: a before/after pixel comparison showed the
//     container really is 33% wider than the screen and clips the right
//     quarter of the background artwork (not "tiling seams", but a
//     clipped right edge — same root cause and fix though). The height
//     question is closed by analysis: bg.localpos_b := h has no
//     multiplier in any checked case, so there is no symmetric bug
//     there.
//
//     ifelem_shellscreen_fnAddBackground is SHARED by 69 of 82 ifs_*
//     screens — this file enables the fix GLOBALLY, same as production. Of
//     7 actually-found bg_texture values, 4 are confirmed
//     (iface_bgmeta_space, iface_bg_1, single_player_campaign,
//     profile_manager — 16 of ~20 actual usages total). Still unverified:
//     single_player_conquest (same code as the already-confirmed
//     single_player_campaign), single_player_option, and one dynamic value
//     in ifs_tutorials — this separate build remains exactly for spot-
//     checking those.
// =============================================================================

using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateBackgroundSizeFixShellCommand
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
        const string entryName = "shell_interface";

        var before = ScriptChunkLocator.FindAll(root).Select(s => s.Name).ToList();
        if (!before.Contains(entryName))
        {
            report.Log($"UA: '{entryName}' не знайдено — патч неможливий на цьому файлі.");
            report.Log($"EN: '{entryName}' not found — cannot patch this file.");
            return null;
        }

        report.Log("UA: ЕКСПЕРИМЕНТАЛЬНИЙ ФІКС — не production, не підтверджено знімком.");
        report.Log("UA: Обгортає ifelem_shellscreen_fnAddBackground (СПІЛЬНА для 69/82 екранів)");
        report.Log("UA: і примусово повертає bg.localpos_r до w (замість w × widescreen ≈ w×1.333).");
        report.Log("EN: EXPERIMENTAL FIX — not production, not confirmed by a screenshot.");
        report.Log("EN: Wraps ifelem_shellscreen_fnAddBackground (SHARED by 69/82 screens)");
        report.Log("EN: and forces bg.localpos_r back to w (instead of w x widescreen ~= w*1.333).");
        report.Log();

        // UA: includeBackgroundSizeFix=true — ЄДИНА відмінність від
        //     GenerateAnchorFixShellCommand. scale=1.0 — той самий параметр,
        //     що й у production, для решти звичайних рядків Bf2LayoutTable.txt.
        // EN: includeBackgroundSizeFix=true — the ONLY difference from
        //     GenerateAnchorFixShellCommand. scale=1.0 — the same parameter
        //     as production, for the rest of the ordinary Bf2LayoutTable.txt rows.
        var installerProto = AnchorInheritancePatchBuilder.BuildInstallerScript(
            scale: 1.0f, includeScreenInfoProbe: false, includeBackgroundSizeFix: true);
        ShellEntryPointPatcher.ApplySplicedInstallerPatch(root, entryName, installerProto);

        var after = ScriptChunkLocator.FindAll(root).Select(s => s.Name).ToList();
        report.Log($"UA: top-level 'scr_' ДО={before.Count}, ПІСЛЯ={after.Count} (очікується: без змін).");
        report.Log($"EN: top-level 'scr_' BEFORE={before.Count}, AFTER={after.Count} (expected: unchanged).");

        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, outputFileName);
        UcfbWriter.WriteFile(outputPath, root);

        report.Log($"UA: Записано: \"{outputPath}\" ({new FileInfo(outputPath).Length} байт).");
        report.Log($"EN: Written: \"{outputPath}\" ({new FileInfo(outputPath).Length} bytes).");
        return outputPath;
    }
}
