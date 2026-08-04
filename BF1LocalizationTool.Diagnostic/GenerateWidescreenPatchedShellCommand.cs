// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateWidescreenPatchedShellCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Порівняння топ-рівневих чанків vanilla проти Remaster shell.lvl
//     показує ОДНАКОВУ кількість "scr_"-чанків (90 і там, і там) — тобто
//     РЕАЛЬНО ПРАЦЮЮЧИЙ мод НЕ додає нових "scr_"-чанків у сам shell.lvl,
//     лише редагує BODY вже наявного "shell_interface" НА МІСЦІ (копія
//     оригінальної логіки живе в ОКРЕМОМУ addon-файлі remaster_hook.lvl).
//     Ця команда відтворює той самий підхід через ShellEntryPointPatcher.
//     ApplySplicedInstallerPatch — НІЧОГО не додає до дерева чанків,
//     лише вставляє виклик інсталятора на ПОЧАТОК коду вже наявного
//     "shell_interface" (безпечно, бо переходи в Lua 5.0 відносні —
//     див. коментар у ShellEntryPointPatcher.cs).
//
//     Результат пишеться в ОКРЕМУ теку "widescreen-output" (НЕ в
//     reference-files/BF2, щоб не затерти vanilla-копію, потрібну для
//     подальших порівнянь) — користувач сам копіює файл у
//     GameData\data\_lvl_pc\shell.lvl (з обов'язковим резервним
//     копіюванням оригіналу).
// EN: Comparing top-level chunks of vanilla vs Remaster shell.lvl shows
//     an IDENTICAL "scr_" chunk count (90 in both) — meaning the ACTUALLY
//     WORKING mod does NOT add new "scr_" chunks to shell.lvl itself, it
//     only edits the BODY of the already-existing "shell_interface" IN
//     PLACE (the copy of the original logic lives in a SEPARATE addon
//     file, remaster_hook.lvl). This command reproduces that same
//     approach via ShellEntryPointPatcher.ApplySplicedInstallerPatch —
//     adds NOTHING to the chunk tree, only inserts a call to the
//     installer at the START of the already-existing "shell_interface"
//     code (safe, since Lua 5.0 jumps are relative — see the comment in
//     ShellEntryPointPatcher.cs).
//
//     The result is written to a SEPARATE "widescreen-output" folder (NOT
//     reference-files/BF2, so as not to overwrite the vanilla copy needed
//     for further comparisons) — the user copies the file into
//     GameData\data\_lvl_pc\shell.lvl themselves (with a mandatory backup
//     of the original first).
// =============================================================================

using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateWidescreenPatchedShellCommand
{
    // UA: debugMarker=true — замість формули масштабу встановлює
    //     БЕЗУМОВНИЙ зсув +300px (WidescreenWrapperBuilder.
    //     BuildDebugMarkerOffsetWrapperFunction). Мета — емпірично
    //     перевірити, чи NewIFContainer взагалі викликається для
    //     конкретного екрана, ДО того як довіряти формулі x*W/800.
    //     Пишеться в ОКРЕМИЙ файл (outputFileName), щоб не затирати
    //     "бойову" версію з реальною формулою.
    // EN: debugMarker=true — instead of the scale formula, installs an
    //     UNCONDITIONAL +300px offset (WidescreenWrapperBuilder.
    //     BuildDebugMarkerOffsetWrapperFunction). Purpose — empirically
    //     check whether NewIFContainer is even called for a given screen,
    //     BEFORE trusting the x*W/800 formula. Written to a SEPARATE file
    //     (outputFileName) so it doesn't overwrite the "real" formula
    //     build.
    public static string? Run(DiagnosticReport report, string shellLvlPath, string outputDir, bool debugMarker = false, string outputFileName = "shell.lvl")
    {
        if (!File.Exists(shellLvlPath))
        {
            report.Log($"UA: shell.lvl не знайдено за шляхом {shellLvlPath}.");
            report.Log($"EN: shell.lvl not found at {shellLvlPath}.");
            return null;
        }

        const string entryName = "shell_interface";

        report.Log($"UA: Читаю {shellLvlPath}...");
        report.Log($"EN: Reading {shellLvlPath}...");
        var root = UcfbReader.ReadFile(shellLvlPath);

        var before = ScriptChunkLocator.FindAll(root).Select(s => s.Name).ToList();
        if (!before.Contains(entryName))
        {
            report.Log($"UA: '{entryName}' не знайдено — патч неможливий на цьому файлі.");
            report.Log($"EN: '{entryName}' not found — patch is not possible on this file.");
            return null;
        }

        report.Log($"UA: Функції, для яких встановлюється widescreen-обгортка: {string.Join(", ", WidescreenWrapperBuilder.ConfirmedTableXyFunctions)}");
        report.Log($"EN: Functions the widescreen wrapper is installed for: {string.Join(", ", WidescreenWrapperBuilder.ConfirmedTableXyFunctions)}");
        report.Log("UA: Формула застосовується БЕЗУМОВНО до ВСІХ викликів обох функцій по всій грі (не лише для окремих екранів, як робить Remaster-мод) — див. коментар WidescreenWrapperBuilder.ConfirmedTableXyFunctions.");
        report.Log("EN: The formula is applied UNCONDITIONALLY to ALL calls of both functions across the whole game (not just specific screens, as the Remaster mod does) — see the WidescreenWrapperBuilder.ConfirmedTableXyFunctions comment.");
        if (debugMarker)
        {
            report.Log("UA: !!! РЕЖИМ ДІАГНОСТИКИ !!! Замість формули масштабу — БЕЗУМОВНИЙ зсув +300px. Це НЕ фікс, лише перевірка, чи NewIFContainer взагалі викликається для цього екрана.");
            report.Log("EN: !!! DIAGNOSTIC MODE !!! Instead of the scale formula — an UNCONDITIONAL +300px offset. This is NOT the fix, only a check whether NewIFContainer is called for this screen at all.");
        }
        else
        {
            report.Log("UA: Anchor-винятки (як у Remaster-моді) НЕ реалізовані — пряма формула x*W/800, y*H/600 без винятків.");
            report.Log("EN: Anchor exceptions (like in the Remaster mod) are NOT implemented — the plain formula x*W/800, y*H/600 with no exceptions.");
        }
        report.Log("UA: Метод патчингу: ApplySplicedInstallerPatch (вставка коду В ІСНУЮЧУ функцію, БЕЗ нових scr_-чанків — див. ShellEntryPointPatcher.cs).");
        report.Log("EN: Patch method: ApplySplicedInstallerPatch (splices code INTO the existing function, adds NO new scr_ chunks — see ShellEntryPointPatcher.cs).");
        report.Log();

        var installerProto = debugMarker
            ? WidescreenWrapperBuilder.BuildWidescreenWrapperScript(
                WidescreenWrapperBuilder.ConfirmedTableXyFunctions,
                (backup, path) => WidescreenWrapperBuilder.BuildDebugMarkerOffsetWrapperFunction(backup, path))
            : WidescreenWrapperBuilder.BuildWidescreenWrapperScript(WidescreenWrapperBuilder.ConfirmedTableXyFunctions);

        ShellEntryPointPatcher.ApplySplicedInstallerPatch(root, entryName, installerProto);

        var after = ScriptChunkLocator.FindAll(root).Select(s => s.Name).ToList();
        report.Log($"UA: top-level 'scr_' ДО={before.Count}, ПІСЛЯ={after.Count} (очікується: без змін — жодного нового чанка не додано)");
        report.Log($"EN: top-level 'scr_' BEFORE={before.Count}, AFTER={after.Count} (expected: unchanged — no new chunk added)");

        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, outputFileName);
        UcfbWriter.WriteFile(outputPath, root);

        var originalSize = new FileInfo(shellLvlPath).Length;
        var patchedSize = new FileInfo(outputPath).Length;

        report.Log();
        report.Log($"UA: Патчений файл записано: {outputPath}");
        report.Log($"EN: Patched file written: {outputPath}");
        report.Log($"UA: Розмір: оригінал={originalSize}Б, патчений={patchedSize}Б (різниця={patchedSize - originalSize:+#;-#;0}Б — лише BODY '{entryName}' збільшився, нових чанків немає)");
        report.Log($"EN: Size: original={originalSize}B, patched={patchedSize}B (delta={patchedSize - originalSize:+#;-#;0}B — only '{entryName}'s BODY grew, no new chunks)");

        // UA: Перевірка: перепарсити ЩОЙНО записаний патчений chunk напряму
        //     з дерева (не з диска) і показати перші кілька інструкцій —
        //     має бути CLOSURE+CALL, потім ОРИГІНАЛЬНИЙ перший опкод.
        // EN: Verification: re-parse the JUST-written patched chunk
        //     directly from the tree (not from disk) and show the first
        //     few instructions — should be CLOSURE+CALL, then the
        //     ORIGINAL first opcode.
        var patchedEntryChunk = root.Children.First(c =>
            c.FourCC == "scr_" &&
            c.Children.FirstOrDefault(n => n.FourCC == "NAME") is { } n &&
            System.Text.Encoding.ASCII.GetString(n.RawData).TrimEnd('\0') == entryName);
        var patchedBody = patchedEntryChunk.Children.First(c => c.FourCC == "BODY");
        var reparsed = Lua50BytecodeReader.Parse(patchedBody.RawData);
        report.Log($"UA: Перепарсинг патченого '{entryName}': залишок={reparsed.LeftoverBytes} байт, top-level scr_-чанків={after.Count} (без змін)");
        report.Log($"EN: Re-parse of patched '{entryName}': leftover={reparsed.LeftoverBytes} bytes, top-level scr_ chunks={after.Count} (unchanged)");
        var dis = new List<string>();
        LuaDisassembler.Disassemble(reparsed.Root, entryName, dis);
        report.Log("UA: Перші рядки дизасемблера (мають починатись з CLOSURE+CALL інсталятора, потім оригінальний код):");
        report.Log("EN: First disassembly lines (should start with the installer's CLOSURE+CALL, then original code):");
        foreach (var line in dis.Take(6))
            report.Log(line);
        report.Log();

        report.Log("UA: НЕ ПЕРЕВІРЕНО досі ГРОЮ (лише структурно): чи 'shell_interface' коректно виконується як звичайний скрипт з тими ж INFO/NAME-полями, що й оригінал. Структурна логіка (відносні переходи, вільний регістр, індекс вкладеного прототипу) — обґрунтована, але це перший реальний ігровий тест цього конкретного механізму splice.");
        report.Log("EN: NOT verified by the GAME yet (structural only): whether 'shell_interface' executes correctly as an ordinary script with the same INFO/NAME fields as the original. The structural logic (relative jumps, free register, nested-prototype index) is sound, but this is the first real in-game test of this specific splice mechanism.");

        return outputPath;
    }
}
