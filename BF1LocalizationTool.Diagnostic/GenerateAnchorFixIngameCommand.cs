// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateAnchorFixIngameCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ГЕНЕРАТОР (production, входить у фінальний патч) / GENERATOR (production, part of the final patch)
// =============================================================================
// UA: Записує ingame.lvl із тим самим гачком таблиці розкладки
//     (AnchorInheritancePatchBuilder), що й shell_layout.lvl, але лише з
//     рядками, які стосуються екранів, відкритих під час бою:
//       • ifs_opt_* (скрипти з common.lvl, які ingame.lvl завантажує через
//         game_interface) — лише виправлення обрізаного тексту
//         (resetbutton/autodetectbutton, logoInfos.envmorphing);
//       • ifs_opt_* — горизонтальний зсув рядів вкладок (_Tabs*, лише x);
//       • ifs_pausemenu — усі рядки (меню паузи існує лише в бою);
//       • ifs_mp_lobby — Helptext_Misc (лобі мережевої гри існує лише в
//         ingame.lvl, тож у shell_layout.lvl цей рядок не діє).
//     Вертикальні рядки вкладок (_Tabs y=31) і анкерні зсуви shell НЕ переносяться: у бою
//     немає шапки з ім'ям профілю, яку ті рядки відкривали.
//     Вхід — уже локалізований ingame.lvl (reference-files\BF2-UA-rem), бо він
//     містить інші патчі; вхідний файл не змінюється.
// EN: Writes an ingame.lvl with the same layout-table hook
//     (AnchorInheritancePatchBuilder) as shell_layout.lvl, but only with the
//     rows for screens that open during a battle:
//       • ifs_opt_* (common.lvl scripts that ingame.lvl loads through
//         game_interface) — only the clipped-text fixes
//         (resetbutton/autodetectbutton, logoInfos.envmorphing);
//       • ifs_opt_* — the horizontal shift of the tab rows (_Tabs*, x only);
//       • ifs_pausemenu — every row (the pause menu exists only in battle);
//       • ifs_mp_lobby — Helptext_Misc (the multiplayer lobby exists only in
//         ingame.lvl, so this row has no effect in shell_layout.lvl).
//     The vertical tab rows (_Tabs y=31) and the shell anchor shifts are NOT carried
//     over: a battle has no menu header with the profile name that those rows
//     uncovered. The input is the already localized ingame.lvl
//     (reference-files\BF2-UA-rem), since it carries other patches; the input
//     file is not modified.
// =============================================================================

using System.Text;
using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateAnchorFixIngameCommand
{
    // UA: Точка входу ingame.lvl (завантажує ifelem_* і ifs_* через ScriptCB_DoFile).
    // EN: The ingame.lvl entry point (loads ifelem_* and ifs_* via ScriptCB_DoFile).
    private const string EntryName = "game_interface";

    // UA: Ім'я резервної копії AddIFScreen, яке пише інсталятор; його наявність
    //     у файлі означає, що гачок уже встановлено.
    // EN: The AddIFScreen backup name the installer writes; its presence in the
    //     file means the hook is already installed.
    private const string HookMarker = "_ws_o_AddIFScreen";

    private static readonly HashSet<string> OptionsTextPaths = new(StringComparer.Ordinal)
    {
        "resetbutton.label",
        "autodetectbutton.label",
        "logoInfos.envmorphing",
    };

    // UA: Які рядки таблиці переносяться в ingame.lvl:
    //     • ifs_opt_*: виправлення тексту (OptionsTextPaths) і горизонтальний
    //       зсув рядів вкладок (_Tabs*, лише поле x — вертикальні y=31 не
    //       переносяться);
    //     • ifs_pausemenu: усі рядки (меню паузи існує лише в бою);
    //     • ifs_mp_lobby: Helptext_Misc.
    // EN: Which table rows are carried over into ingame.lvl:
    //     • ifs_opt_*: text fixes (OptionsTextPaths) and the horizontal shift of
    //       the tab rows (_Tabs*, the x field only — the vertical y=31 rows are
    //       not carried over);
    //     • ifs_pausemenu: every row (the pause menu exists only in battle);
    //     • ifs_mp_lobby: Helptext_Misc.
    public static bool KeepRow(string screen, AnchorInheritancePatchBuilder.WidgetEntry entry) =>
        (screen.StartsWith("ifs_opt_", StringComparison.Ordinal) &&
            (OptionsTextPaths.Contains(entry.Path) ||
             (entry.Path.StartsWith("_Tabs", StringComparison.Ordinal) &&
              entry.Texts.Count == 0 && entry.Fields.Count > 0 && entry.Fields.All(f => f.Key == "x")))) ||
        screen == "ifs_pausemenu" ||
        (screen == "ifs_mp_lobby" && entry.Path == "Helptext_Misc.label");

    public static string? Run(DiagnosticReport report, string ingameLvlPath, string outputDir, string outputFileName)
    {
        if (!File.Exists(ingameLvlPath))
        {
            report.Log($"UA: ingame.lvl не знайдено за шляхом {ingameLvlPath}.");
            report.Log($"EN: ingame.lvl not found at {ingameLvlPath}.");
            return null;
        }

        var raw = File.ReadAllBytes(ingameLvlPath);
        if (IndexOf(raw, Encoding.ASCII.GetBytes(HookMarker)) >= 0)
        {
            report.Log($"UA: У файлі вже є гачок розкладки ('{HookMarker}') — повторне встановлення скасовано.");
            report.Log($"EN: The file already contains the layout hook ('{HookMarker}') — reinstall cancelled.");
            return null;
        }

        var root = UcfbReader.ReadFile(ingameLvlPath);
        var before = ScriptChunkLocator.FindAll(root).Select(s => s.Name).ToList();
        if (!before.Contains(EntryName))
        {
            report.Log($"UA: '{EntryName}' не знайдено — патч неможливий на цьому файлі.");
            report.Log($"EN: '{EntryName}' not found — cannot patch this file.");
            return null;
        }

        report.Log("UA: Рядки таблиці розкладки, перенесені в ingame.lvl:");
        report.Log("EN: Layout-table rows carried over into ingame.lvl:");
        var kept = 0;
        foreach (var (screen, entries) in AnchorInheritancePatchBuilder.Table.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            foreach (var entry in entries.Where(e => KeepRow(screen, e)))
            {
                var fields = string.Join(";", entry.Fields.Select(f => $"{f.Key}={f.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
                report.Log($"    {screen}  {entry.Path}  {fields}");
                kept++;
            }
        }
        report.Log($"UA: Усього рядків: {kept}. / EN: Rows total: {kept}.");
        report.Log();

        var installerProto = AnchorInheritancePatchBuilder.BuildInstallerScript(
            1.0f, includeScreenInfoProbe: false, includeBackgroundSizeFix: false,
            rowFilter: KeepRow, includePopupTutorialFix: false);
        ShellEntryPointPatcher.ApplySplicedInstallerPatch(root, EntryName, installerProto);

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

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= haystack.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
