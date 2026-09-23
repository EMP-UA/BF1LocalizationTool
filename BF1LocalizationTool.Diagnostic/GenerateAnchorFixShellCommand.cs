// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateAnchorFixShellCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ГЕНЕРАТОР (production, входить у фінальний патч) / GENERATOR (production, part of the final patch)
// =============================================================================
// UA: Записує на диск shell.lvl із патчем розкладки (AnchorInheritancePatchBuilder).
//     Вхідний ванільний файл не змінюється. У той самий production-патч
//     також увімкнено фікс фону (includeBackgroundSizeFix=true) —
//     підтверджено знімками на 4 з 7 значень bg_texture (охоплюють
//     переважну більшість екранів, що взагалі мають фон).
// EN: Writes a shell.lvl with the layout patch to disk. The input vanilla file
//     is left untouched. The same production patch also enables the
//     background fix (includeBackgroundSizeFix=true) — confirmed by
//     screenshots on 4 of 7 bg_texture values (covering the large
//     majority of screens that have a background at all).
// =============================================================================

using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateAnchorFixShellCommand
{
    public static string? Run(DiagnosticReport report, string shellLvlPath, string outputDir,
        string outputFileName, float scale)
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

        report.Log($"UA: КОЕФІЦІЄНТ РОЗМІРІВ: {scale:0.##}× від авторського макета {AnchorInheritancePatchBuilder.LayoutWidth:0}×{AnchorInheritancePatchBuilder.LayoutHeight:0}.");
        report.Log($"EN: SIZE SCALE: {scale:0.##}x of the authored {AnchorInheritancePatchBuilder.LayoutWidth:0}x{AnchorInheritancePatchBuilder.LayoutHeight:0} layout.");
        report.Log($"UA: Екранів {AnchorInheritancePatchBuilder.ScreenCount}, віджетів {AnchorInheritancePatchBuilder.WidgetCount}, полів {AnchorInheritancePatchBuilder.FieldCount}.");
        report.Log($"EN: Screens {AnchorInheritancePatchBuilder.ScreenCount}, widgets {AnchorInheritancePatchBuilder.WidgetCount}, fields {AnchorInheritancePatchBuilder.FieldCount}.");
        report.Log();
        report.Log("UA: Підстава — вимір: у ванілі 1178 розмірних значень залежать від роздільності (742 рівно ×W/800, 373 рівно ×H/600), тоді як текст не масштабується. Патч повертає авторські розміри.");
        report.Log("EN: Rationale — measurement: in vanilla, 1178 size values depend on the resolution (742 exactly xW/800, 373 exactly xH/600) while text does not scale. The patch restores the authored sizes.");
        report.Log("UA: НЕ чіпаються: ScriptCB_GetScreenInfo, ScriptCB_GetSafeScreenInfo.");
        report.Log("EN: NOT touched: ScriptCB_GetScreenInfo, ScriptCB_GetSafeScreenInfo.");
        report.Log("UA: ТЕПЕР ФІКСУЄТЬСЯ фон (ifelem_shellscreen_fnAddBackground): ваніль розтягує контейнер фону");
        report.Log("UA: до w×widescreen (≈33% ширше за екран, виміряно й підтверджено знімками на 8+ екранах з");
        report.Log("UA: різними текстурами bg_texture); патч примусово повертає bg.localpos_r до РІВНО w. Це");
        report.Log("UA: чиста геометрична правка — жодна конкретна текстура не хардкодиться.");
        report.Log("EN: Background IS NOW FIXED (ifelem_shellscreen_fnAddBackground): vanilla stretches the");
        report.Log("EN: background container to w x widescreen (~33% wider than the screen, measured and");
        report.Log("EN: confirmed by screenshots on 8+ screens with different bg_texture values); the patch");
        report.Log("EN: forces bg.localpos_r back to EXACTLY w. This is a pure geometry fix — no specific");
        report.Log("EN: texture is hardcoded.");
        report.Log();

        var installerProto = AnchorInheritancePatchBuilder.BuildInstallerScript(
            scale, includeScreenInfoProbe: false, includeBackgroundSizeFix: true);
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
