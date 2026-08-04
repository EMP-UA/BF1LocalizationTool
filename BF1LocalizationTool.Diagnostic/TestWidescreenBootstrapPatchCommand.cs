// =============================================================================
// BF1LocalizationTool.Diagnostic — TestWidescreenBootstrapPatchCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перевіряє Lua50BytecodeWriter + ShellEntryPointPatcher трьома
//     незалежними тестами, без запуску самої гри:
//
//     1. Round-trip НАШОГО bootstrap-стабу: Build → Write → Parse →
//        порівняти дизасемблерний текст (не сирі байти — Writer свідомо
//        НЕ зберігає номери рядків, тому байти відрізнятимуться, а
//        логіка — ні).
//     2. Round-trip на РЕАЛЬНОМУ існуючому скрипті з shell.lvl (значно
//        суворіший тест: локальні змінні, upvalues, вкладені прототипи,
//        усі опкоди — не лише наш вузький підмножина).
//     3. Повний ApplyBootstrapPatch на копії реального shell.lvl "у
//        пам'яті" — список top-level "scr_" ДО/ПІСЛЯ + дизасемблер нового
//        bootstrap-стабу і повторно розібраної "stock_"-копії.
// EN: Verifies Lua50BytecodeWriter + ShellEntryPointPatcher with three
//     independent tests, without launching the game itself:
//
//     1. Round-trip of OUR bootstrap stub: Build → Write → Parse → compare
//        disassembly text (not raw bytes — the Writer deliberately does
//        NOT retain line numbers, so bytes will differ while the logic
//        won't).
//     2. Round-trip on a REAL existing script from shell.lvl (a much
//        stricter test: locals, upvalues, nested prototypes, every
//        opcode — not just our narrow subset).
//     3. A full ApplyBootstrapPatch on an in-memory copy of real
//        shell.lvl — the list of top-level "scr_" chunks BEFORE/AFTER +
//        disassembly of the new bootstrap stub and of the re-parsed
//        "stock_" copy.
// =============================================================================

using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class TestWidescreenBootstrapPatchCommand
{
    public static void Run(DiagnosticReport report, string shellLvlPath)
    {
        report.Log("UA: === Тест 1: round-trip власного bootstrap-стабу ===");
        report.Log("EN: === Test 1: round-trip of our own bootstrap stub ===");
        report.Log();
        RunBootstrapRoundTrip(report);
        report.Log();

        report.Log("UA: === Тест 1б: round-trip widescreen-wrapper скрипту (сама формула) ===");
        report.Log("EN: === Test 1b: round-trip of the widescreen wrapper script (the actual formula) ===");
        report.Log();
        RunWrapperRoundTrip(report);
        report.Log();

        if (!File.Exists(shellLvlPath))
        {
            report.Log($"UA: shell.lvl не знайдено за шляхом {shellLvlPath} — тести 2 і 3 пропущено.");
            report.Log($"EN: shell.lvl not found at {shellLvlPath} — tests 2 and 3 skipped.");
            return;
        }

        report.Log("UA: === Тест 2: round-trip реального скрипту з shell.lvl ===");
        report.Log("EN: === Test 2: round-trip of a real script from shell.lvl ===");
        report.Log();
        var root = UcfbReader.ReadFile(shellLvlPath);
        RunRealScriptRoundTrip(report, root);
        report.Log();

        report.Log("UA: === Тест 3: повний ApplyBootstrapPatch (у пам'яті, файл НЕ перезаписується) ===");
        report.Log("EN: === Test 3: full ApplyBootstrapPatch (in-memory, file NOT overwritten) ===");
        report.Log();
        RunFullPatchDryRun(report, shellLvlPath);
    }

    // -------------------------------------------------------------------------
    private static void RunBootstrapRoundTrip(DiagnosticReport report)
    {
        var built = ShellEntryPointPatcher.BuildBootstrapStub("rema_widescreen_wrapper", "stock_shell_interface");
        var bytes = Lua50BytecodeWriter.Write(built);

        LuaChunkParseResult reparsed;
        try
        {
            reparsed = Lua50BytecodeReader.Parse(bytes);
        }
        catch (Exception ex)
        {
            report.Log($"UA: ПОМИЛКА: Writer видав байти, які Reader не зміг розпарсити: {ex.Message}");
            report.Log($"EN: ERROR: the Writer produced bytes the Reader failed to parse: {ex.Message}");
            return;
        }

        var beforeLines = new List<string>();
        LuaDisassembler.Disassemble(built, "built", beforeLines);

        var afterLines = new List<string>();
        LuaDisassembler.Disassemble(reparsed.Root, "built", afterLines);

        var identical = beforeLines.SequenceEqual(afterLines);
        report.Log($"UA: bodyBytes.Length={bytes.Length}, LeftoverBytes після повторного парсингу={reparsed.LeftoverBytes} (очікується 0 — увесь запис мусить бути спожитий)");
        report.Log($"EN: bodyBytes.Length={bytes.Length}, LeftoverBytes after re-parsing={reparsed.LeftoverBytes} (expected 0 — the whole write must be consumed)");
        report.Log($"UA: Дизасемблер до/після ІДЕНТИЧНИЙ: {identical}");
        report.Log($"EN: Disassembly before/after IDENTICAL: {identical}");

        if (!identical)
        {
            report.Log("UA: --- ДО (побудовано) ---");
            report.Log("EN: --- BEFORE (built) ---");
            foreach (var line in beforeLines) report.Log(line);
            report.Log("UA: --- ПІСЛЯ (Write→Parse) ---");
            report.Log("EN: --- AFTER (Write→Parse) ---");
            foreach (var line in afterLines) report.Log(line);
        }
        else
        {
            foreach (var line in beforeLines) report.Log(line);
        }
    }

    // -------------------------------------------------------------------------
    private static void RunWrapperRoundTrip(DiagnosticReport report)
    {
        var built = WidescreenWrapperBuilder.BuildWidescreenWrapperScript(WidescreenWrapperBuilder.ConfirmedTableXyFunctions);
        var bytes = Lua50BytecodeWriter.Write(built);

        LuaChunkParseResult reparsed;
        try
        {
            reparsed = Lua50BytecodeReader.Parse(bytes);
        }
        catch (Exception ex)
        {
            report.Log($"UA: ПОМИЛКА: wrapper-скрипт не пройшов повторний парсинг: {ex.Message}");
            report.Log($"EN: ERROR: the wrapper script failed to re-parse: {ex.Message}");
            return;
        }

        var beforeLines = new List<string>();
        LuaDisassembler.Disassemble(built, "wrapper", beforeLines);
        var afterLines = new List<string>();
        LuaDisassembler.Disassemble(reparsed.Root, "wrapper", afterLines);

        var identical = beforeLines.SequenceEqual(afterLines);
        report.Log($"UA: bodyBytes.Length={bytes.Length}, LeftoverBytes={reparsed.LeftoverBytes} (очікується 0), дизасемблер ідентичний={identical}");
        report.Log($"EN: bodyBytes.Length={bytes.Length}, LeftoverBytes={reparsed.LeftoverBytes} (expected 0), disassembly identical={identical}");
        report.Log();
        foreach (var line in beforeLines) report.Log(line);
    }

    // -------------------------------------------------------------------------
    private static void RunRealScriptRoundTrip(DiagnosticReport report, UcfbChunk root)
    {
        var scripts = ScriptChunkLocator.FindAll(root);

        // UA: беремо кілька МАЛЕНЬКИХ реальних скриптів (не найбільший —
        //     для читабельного звіту), але з реальною складністю
        //     (locals/upvalues/nested), щоб тест був чесним, а не
        //     тривіальним.
        // EN: pick a few SMALL real scripts (not the biggest — for a
        //     readable report), but with real complexity
        //     (locals/upvalues/nested), so the test is honest, not
        //     trivial.
        var candidates = scripts
            .OrderBy(s => s.BodyChunk.RawData.Length)
            .Where(s => s.BodyChunk.RawData.Length > 200) // UA: не порожні заглушки / EN: not empty stubs
            .Take(5)
            .ToList();

        var allIdentical = true;
        foreach (var script in candidates)
        {
            LuaChunkParseResult original;
            try
            {
                original = Lua50BytecodeReader.Parse(script.BodyChunk.RawData);
            }
            catch (Exception ex)
            {
                report.Log($"UA: [{script.Name}] ПОМИЛКА первинного парсингу (пропускаємо): {ex.Message}");
                report.Log($"EN: [{script.Name}] initial parse ERROR (skipping): {ex.Message}");
                continue;
            }

            var rewritten = Lua50BytecodeWriter.Write(original.Root);

            LuaChunkParseResult reparsed;
            try
            {
                reparsed = Lua50BytecodeReader.Parse(rewritten);
            }
            catch (Exception ex)
            {
                allIdentical = false;
                report.Log($"UA: [{script.Name}] ПОМИЛКА: Writer видав неваліду для реального скрипту: {ex.Message}");
                report.Log($"EN: [{script.Name}] ERROR: the Writer produced invalid output for a real script: {ex.Message}");
                continue;
            }

            var beforeLines = new List<string>();
            LuaDisassembler.Disassemble(original.Root, script.Name, beforeLines);
            var afterLines = new List<string>();
            LuaDisassembler.Disassemble(reparsed.Root, script.Name, afterLines);

            var identical = beforeLines.SequenceEqual(afterLines);
            allIdentical &= identical;

            report.Log($"UA: [{script.Name}] оригінал={script.BodyChunk.RawData.Length}Б, перезаписано={rewritten.Length}Б, дизасемблер ідентичний={identical}, залишок після реparse={reparsed.LeftoverBytes}");
            report.Log($"EN: [{script.Name}] original={script.BodyChunk.RawData.Length}B, rewritten={rewritten.Length}B, disassembly identical={identical}, leftover after re-parse={reparsed.LeftoverBytes}");

            if (!identical)
            {
                var firstDiff = Enumerable.Range(0, Math.Min(beforeLines.Count, afterLines.Count))
                    .FirstOrDefault(i => beforeLines[i] != afterLines[i], -1);
                report.Log($"UA:   ПЕРША розбіжність: рядок {firstDiff}");
                report.Log($"UA:   ДО:    {(firstDiff >= 0 && firstDiff < beforeLines.Count ? beforeLines[firstDiff] : "<немає>")}");
                report.Log($"UA:   ПІСЛЯ: {(firstDiff >= 0 && firstDiff < afterLines.Count ? afterLines[firstDiff] : "<немає>")}");
            }
        }

        report.Log();
        report.Log($"UA: Усього перевірено {candidates.Count} реальних скриптів, УСІ ідентичні={allIdentical}");
        report.Log($"EN: Checked {candidates.Count} real scripts total, ALL identical={allIdentical}");
    }

    // -------------------------------------------------------------------------
    private static void RunFullPatchDryRun(DiagnosticReport report, string shellLvlPath)
    {
        // UA: свіже читання (окремо від Тесту 2), щоб мутація в
        //     ApplyBootstrapPatch не впливала на попередній тест.
        // EN: a fresh read (separate from Test 2), so the mutation in
        //     ApplyBootstrapPatch doesn't affect the previous test.
        var root = UcfbReader.ReadFile(shellLvlPath);

        var before = ScriptChunkLocator.FindAll(root).Select(s => s.Name).ToList();
        report.Log($"UA: top-level 'scr_' ДО патча: {before.Count} штук");
        report.Log($"EN: top-level 'scr_' BEFORE patch: {before.Count} entries");

        const string entryName = "shell_interface";
        const string stockName = "stock_shell_interface";
        const string wrapperName = "rema_widescreen_wrapper";

        if (!before.Contains(entryName))
        {
            report.Log($"UA: '{entryName}' не знайдено серед скриптів — патч неможливий на цьому файлі.");
            report.Log($"EN: '{entryName}' not found among scripts — patch is not possible on this file.");
            return;
        }

        // UA: СПРАВЖНІЙ widescreen-wrapper (формула x*W/800, y*H/600 для
        //     NewIFContainer) — не заглушка. Round-trip самого wrapper-
        //     скрипту вже перевірено окремо в Тесті 1б.
        // EN: the REAL widescreen wrapper (the x*W/800, y*H/600 formula
        //     for NewIFContainer) — not a placeholder. The wrapper
        //     script's own round-trip was already verified separately in
        //     Test 1b.
        var wrapperProto = WidescreenWrapperBuilder.BuildWidescreenWrapperScript(WidescreenWrapperBuilder.ConfirmedTableXyFunctions);
        var wrapperBytes = Lua50BytecodeWriter.Write(wrapperProto);

        ShellEntryPointPatcher.ApplyBootstrapPatch(root, entryName, stockName, wrapperName, wrapperBytes);

        var after = ScriptChunkLocator.FindAll(root).Select(s => s.Name).ToList();
        report.Log($"UA: top-level 'scr_' ПІСЛЯ патча: {after.Count} штук (очікується {before.Count + 2}: +stock, +wrapper)");
        report.Log($"EN: top-level 'scr_' AFTER patch: {after.Count} entries (expected {before.Count + 2}: +stock, +wrapper)");
        report.Log($"UA: Нові імена: {string.Join(", ", after.Except(before))}");
        report.Log($"EN: New names: {string.Join(", ", after.Except(before))}");

        // UA: перевірка, що новий entry-point (bootstrap) сам коректно
        //     парситься назад із мутованого дерева.
        // EN: verify the new entry-point (bootstrap) itself parses back
        //     correctly from the mutated tree.
        var patchedScripts = ScriptChunkLocator.FindAll(root);
        var newEntry = patchedScripts.First(s => s.Name == entryName);
        var stockCopy = patchedScripts.First(s => s.Name == stockName);

        var newEntryParsed = Lua50BytecodeReader.Parse(newEntry.BodyChunk.RawData);
        var stockCopyParsed = Lua50BytecodeReader.Parse(stockCopy.BodyChunk.RawData);

        report.Log($"UA: Новий '{entryName}' (bootstrap) розпарсився: {newEntryParsed.LeftoverBytes} байт залишку (очікується 0)");
        report.Log($"EN: New '{entryName}' (bootstrap) parsed: {newEntryParsed.LeftoverBytes} leftover bytes (expected 0)");
        report.Log($"UA: '{stockName}' (клон оригіналу) розпарсився: {stockCopyParsed.LeftoverBytes} байт залишку (очікується 1, як і в оригіналі)");
        report.Log($"EN: '{stockName}' (clone of the original) parsed: {stockCopyParsed.LeftoverBytes} leftover bytes (expected 1, same as the original)");
        report.Log();

        var bootstrapLines = new List<string>();
        LuaDisassembler.Disassemble(newEntryParsed.Root, entryName, bootstrapLines);
        report.Log("UA: --- Дизасемблер нового bootstrap-entry-point ---");
        report.Log("EN: --- Disassembly of the new bootstrap entry-point ---");
        foreach (var line in bootstrapLines) report.Log(line);

        // UA: серіалізувати ВЕСЬ мутований файл у пам'ять (не на диск!) —
        //     остання перевірка, що UcfbWriter коректно обходить дерево
        //     з новододаними чанками (padding/вирівнювання тощо).
        // EN: serialize the ENTIRE mutated file in memory (NOT to disk!) —
        //     a final check that UcfbWriter correctly walks the tree with
        //     the newly added chunks (padding/alignment etc.).
        var fullFileBytes = UcfbWriter.WriteFile(root);
        report.Log();
        report.Log($"UA: Повний файл серіалізовано в пам'яті: {fullFileBytes.Length} байт (лише перевірка, диск НЕ чіпали).");
        report.Log($"EN: Full file serialized in memory: {fullFileBytes.Length} bytes (verification only, disk was NOT touched).");
    }
}
