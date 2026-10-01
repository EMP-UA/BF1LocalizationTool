// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateBackgroundSizeFixShellCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: Записує окремий shell_bgfix.lvl, у якому глобальну функцію
//     ifelem_shellscreen_fnAddBackground обгорнуто так, що після виклику
//     оригіналу bg.localpos_r дорівнює рівно ширині екрана (w) замість
//     w × widescreen (≈2560 при 1920, тобто на 33% ширше за екран;
//     widescreen = (1920/1080)/(800/600) ≈ 1.3333). Той самий фікс входить
//     у production (GenerateAnchorFixShellCommand -> shell_layout.lvl);
//     цей файл будує його ізольовано, без решти патча розкладки.
//
//     Контейнер фону оригіналу ширший за екран на 33% і обрізає праву
//     чверть фонової картинки. Висота (bg.localpos_b := h) множника не має
//     й не змінюється.
//
//     Функція ifelem_shellscreen_fnAddBackground спільна для 69 з 82
//     екранів ifs_*; фікс вмикається глобально, так само як у production.
//     Механізм і дизасемблювання — у коментарі до BgFixFn в
//     AnchorInheritancePatchBuilder.cs.
// EN: Writes a separate shell_bgfix.lvl in which the global function
//     ifelem_shellscreen_fnAddBackground is wrapped so that, after the
//     original returns, bg.localpos_r equals exactly the screen width (w)
//     instead of w x widescreen (~2560 at 1920, i.e. 33% wider than the
//     screen; widescreen = (1920/1080)/(800/600) ~= 1.3333). The same fix
//     is part of production (GenerateAnchorFixShellCommand ->
//     shell_layout.lvl); this file builds it in isolation, without the rest
//     of the layout patch.
//
//     The original's background container is 33% wider than the screen and
//     clips the right quarter of the background picture. The height
//     (bg.localpos_b := h) has no multiplier and is not changed.
//
//     ifelem_shellscreen_fnAddBackground is shared by 69 of 82 ifs_*
//     screens; the fix is enabled globally, as in production. Mechanism and
//     disassembly — in the comment on BgFixFn in
//     AnchorInheritancePatchBuilder.cs.
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
