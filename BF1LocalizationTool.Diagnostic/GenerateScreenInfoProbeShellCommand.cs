// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateScreenInfoProbeShellCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: ДІАГНОСТИЧНИЙ, ОДНОРАЗОВИЙ інструмент — НЕ частина production-патча.
//     Записує окремий shell_probe.lvl, у якому обгорнуто нативну
//     ScriptCB_GetScreenInfo (прозорий passthrough) так, щоб її 4-те
//     повернене значення ("widescreen") стало видимим на екрані
//     "Миттєвий бій -> Сеанс" як Y-позиція напису "Налаштування:", помножена
//     на 500. Причина: реальне значення "widescreen" на 1920x1080 невідоме
//     (Python-VM завжди підставляє заглушку 1.0), а користувач не має
//     доступу до консолі чи логу гри — тож єдиний канал виводу лишається
//     піксельний, той самий, яким уже перевірено десятки інших правок у
//     цьому проєкті. Див. розгорнутий коментар "ДІАГНОСТИЧНИЙ ЗОНД" у
//     AnchorInheritancePatchBuilder.cs (перед BuildDispatchWrapper).
//
//     Цей файл НЕ чіпає Bf2LayoutTable.txt і НЕ впливає на
//     GenerateAnchorFixShellCommand/shell_layout.lvl: обидва викликають
//     AnchorInheritancePatchBuilder.BuildInstallerScript з
//     includeScreenInfoProbe=false (за замовчуванням), а цей — з true.
// EN: A DIAGNOSTIC, ONE-OFF tool — NOT part of the production patch. Writes a
//     separate shell_probe.lvl in which the native ScriptCB_GetScreenInfo is
//     wrapped (a transparent passthrough) so its 4th return value
//     ("widescreen") becomes visible on the "Instant Action -> Session"
//     screen as the Y-position of the "Налаштування:"/"Settings:" label,
//     multiplied by 500. Reason: the real "widescreen" value at 1920x1080 is
//     unknown (the Python VM always stubs it as 1.0), and the user has no
//     access to the game's console or log — so the only remaining output
//     channel is the pixel-measurement technique this project has already
//     used to verify dozens of other fixes. See the detailed "DIAGNOSTIC
//     PROBE" comment in AnchorInheritancePatchBuilder.cs (right before
//     BuildDispatchWrapper).
//
//     This file does NOT touch Bf2LayoutTable.txt and does NOT affect
//     GenerateAnchorFixShellCommand/shell_layout.lvl: both call
//     AnchorInheritancePatchBuilder.BuildInstallerScript with
//     includeScreenInfoProbe=false (the default); this one passes true.
// =============================================================================

using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateScreenInfoProbeShellCommand
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

        report.Log("UA: ДІАГНОСТИЧНИЙ ЗОНД — не production. Обгортає ScriptCB_GetScreenInfo");
        report.Log("UA: (прозорий passthrough) і показує його 4-те значення (\"widescreen\")");
        report.Log("UA: як Y-позицію напису \"Налаштування:\" x500 на екрані Сеансу.");
        report.Log("EN: DIAGNOSTIC PROBE — not production. Wraps ScriptCB_GetScreenInfo");
        report.Log("EN: (transparent passthrough) and displays its 4th value (\"widescreen\")");
        report.Log("EN: as the \"Налаштування:\"/\"Settings:\" label's Y-position x500 on the Session screen.");
        report.Log();

        // UA: scale=1.0 — той самий параметр, що й у production; тут він
        //     впливає лише на решту звичайних рядків Bf2LayoutTable.txt, які
        //     все одно потрапляють у той самий BuildInstallerScript.
        //     includeScreenInfoProbe=true — ЄДИНА відмінність від
        //     GenerateAnchorFixShellCommand.
        // EN: scale=1.0 — the same parameter as production; here it only
        //     affects the rest of the ordinary Bf2LayoutTable.txt rows, which
        //     still go through the same BuildInstallerScript.
        //     includeScreenInfoProbe=true — the ONLY difference from
        //     GenerateAnchorFixShellCommand.
        var installerProto = AnchorInheritancePatchBuilder.BuildInstallerScript(
            scale: 1.0f, includeScreenInfoProbe: true);
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
