// =============================================================================
// BF1LocalizationTool.Diagnostic — AnalyzeShellScriptConstantsCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перший практичний крок widescreen-фікса меню BF2 (Classic): повний,
//     безпечний (без виконання СТОРОННІХ .exe) дамп усіх Lua-констант
//     (чисел і рядків) з КОЖНОГО "scr_"-чанка shell.lvl (головне меню) та
//     ingame.lvl (HUD), через власний Lua50BytecodeReader
//     (BF1LocalizationTool.Core.Scripts) — формат підтверджено побайтово
//     проти офіційного джерела Lua 5.0.3.
//
//     Мета — знайти float-константи координат/масштабів layout (і рядкові
//     константи типу "ScreenRelativeX"/"AspectRatio" тощо), які можна
//     буде попатчити in-place (без repack), НЕ декомпілюючи самі
//     інструкції — див. коментар класу Lua50BytecodeReader.
//
//     Звіт має 3 секції:
//       1. HIGHLIGHTS — константи, чиє ім'я/значення натякає на
//          екран/aspect/розмір (ключові слова: screen, aspect, position,
//          width, height, resolution, ratio, viewport, scale) — це
//          найкоротший, найкорисніший для першого читання блок.
//       2. Зведена таблиця по КОЖНОМУ чанку (ім'я, кількість
//          чисел/рядків, LeftoverBytes-статус парсингу).
//       3. Повний дамп усіх констант по КОЖНОМУ чанку — для подальшого
//          grep'у вручну, коли HIGHLIGHTS недостатньо.
// EN: The first practical step of the BF2 (Classic) menu widescreen fix:
//     a full, safe (no THIRD-PARTY .exe execution) dump of all Lua
//     constants (numbers and strings) from EVERY "scr_" chunk of
//     shell.lvl (main menu) and ingame.lvl (HUD), via our own
//     Lua50BytecodeReader (BF1LocalizationTool.Core.Scripts) — format
//     confirmed byte-for-byte against the official Lua 5.0.3 source.
//
//     Goal — find float constants for layout coordinates/scales (and
//     string constants like "ScreenRelativeX"/"AspectRatio" etc.) that
//     can later be patched in place (no repack), WITHOUT decompiling the
//     instructions themselves — see the Lua50BytecodeReader class
//     comment.
//
//     The report has 3 sections:
//       1. HIGHLIGHTS — constants whose name/value hints at
//          screen/aspect/size (keywords: screen, aspect, position,
//          width, height, resolution, ratio, viewport, scale) — the
//          shortest, most useful block to read first.
//       2. A summary table per chunk (name, number/string counts,
//          LeftoverBytes parse status).
//       3. A full dump of every constant per chunk — for manual grep
//          when HIGHLIGHTS isn't enough.
// =============================================================================

using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class AnalyzeShellScriptConstantsCommand
{
    // UA: Ключові слова для секції HIGHLIGHTS — лише в рядкових
    //     константах (порівняння без урахування регістру). Список
    //     навмисно короткий і конкретний — краще пропустити щось і
    //     знайти в повному дампі (секція 3), ніж засмітити HIGHLIGHTS
    //     сотнями випадкових збігів.
    // EN: Keywords for the HIGHLIGHTS section — string constants only
    //     (case-insensitive). The list is deliberately short and
    //     specific — better to miss something and find it in the full
    //     dump (section 3) than to flood HIGHLIGHTS with hundreds of
    //     accidental matches.
    private static readonly string[] HighlightKeywords =
    [
        "screen", "aspect", "position", "width", "height",
        "resolution", "ratio", "viewport", "scale",
    ];

    private sealed record ConstantHit(string ChunkName, string FunctionPath, LuaConstant Constant);

    public static void Run(DiagnosticReport report, string lvlFilePath, string fileLabel, string gameLabel)
    {
        if (!File.Exists(lvlFilePath))
        {
            report.Log($"UA: [{gameLabel}] Файл не знайдено: {lvlFilePath}");
            report.Log($"EN: [{gameLabel}] File not found: {lvlFilePath}");
            return;
        }

        var root = UcfbReader.ReadFile(lvlFilePath);
        var scripts = ScriptChunkLocator.FindAll(root);

        report.Log($"UA: [{gameLabel}] {fileLabel}: знайдено {scripts.Count} \"scr_\" чанків.");
        report.Log($"EN: [{gameLabel}] {fileLabel}: found {scripts.Count} \"scr_\" chunks.");
        report.Log();

        var allHits = new List<ConstantHit>();
        var summaryLines = new List<string>();
        var fullDumpLines = new List<string>();
        var okCount = 0;
        var warnCount = 0;
        var failCount = 0;

        foreach (var script in scripts)
        {
            LuaChunkParseResult parsed;
            try
            {
                parsed = Lua50BytecodeReader.Parse(script.BodyChunk.RawData);
            }
            catch (Exception ex)
            {
                failCount++;
                summaryLines.Add($"    {script.Name,-32} ПОМИЛКА ПАРСИНГУ / PARSE ERROR: {ex.Message}");
                continue;
            }

            var constants = new List<(string Path, LuaConstant Constant)>();
            CollectConstants(parsed.Root, constants);

            var numberCount = constants.Count(x => x.Constant.Kind == LuaConstantKind.Number);
            var stringCount = constants.Count(x => x.Constant.Kind == LuaConstantKind.String);

            // UA: На 99 з 99 реальних чанків (shell.lvl+ingame.lvl) LeftoverBytes
            //     == 1. Будь-яке інше значення — сигнал переглянути цей
            //     конкретний чанк вручну, а не мовчки довіряти результату.
            // EN: On 99 of 99 real chunks (shell.lvl+ingame.lvl) LeftoverBytes
            //     == 1. Any other value is a signal to manually re-examine
            //     this specific chunk, not silently trust the result.
            var status = parsed.LeftoverBytes == 1 ? "OK" : $"WARN(leftover={parsed.LeftoverBytes})";
            if (parsed.LeftoverBytes == 1) okCount++; else warnCount++;

            summaryLines.Add($"    {script.Name,-32} numbers={numberCount,4}  strings={stringCount,4}  {status}");

            fullDumpLines.Add($"--- {fileLabel} :: {script.Name} ({status}, {constants.Count} constants) ---");
            foreach (var (path, constant) in constants)
            {
                var valueText = constant.Kind switch
                {
                    LuaConstantKind.Number => constant.NumberValue.ToString("G9"),
                    LuaConstantKind.String => $"\"{constant.StringValue}\"",
                    LuaConstantKind.Boolean => constant.BooleanValue.ToString(),
                    _ => "nil",
                };
                fullDumpLines.Add($"    [{path}] {constant.Kind}: {valueText}");

                if (constant.Kind == LuaConstantKind.String && constant.StringValue is not null &&
                    HighlightKeywords.Any(kw => constant.StringValue.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                {
                    allHits.Add(new ConstantHit(script.Name, path, constant));
                }
            }
            fullDumpLines.Add("");
        }

        // UA: Секція 1 — HIGHLIGHTS
        // EN: Section 1 — HIGHLIGHTS
        report.Log($"UA: === {fileLabel}: HIGHLIGHTS (рядки з ключовими словами screen/aspect/position/width/height/resolution/ratio/viewport/scale) ===");
        report.Log($"EN: === {fileLabel}: HIGHLIGHTS (strings matching screen/aspect/position/width/height/resolution/ratio/viewport/scale) ===");
        if (allHits.Count == 0)
        {
            report.Log("    (жодного збігу / no matches)");
        }
        else
        {
            foreach (var hit in allHits)
                report.Log($"    {hit.ChunkName,-32} [{hit.FunctionPath}] \"{hit.Constant.StringValue}\"");
        }
        report.Log();

        // UA: Секція 2 — зведена таблиця
        // EN: Section 2 — summary table
        report.Log($"UA: === {fileLabel}: зведена таблиця по чанках (OK={okCount}, WARN={warnCount}, FAIL={failCount}) ===");
        report.Log($"EN: === {fileLabel}: per-chunk summary table (OK={okCount}, WARN={warnCount}, FAIL={failCount}) ===");
        foreach (var line in summaryLines)
            report.Log(line);
        report.Log();

        // UA: Секція 3 — повний дамп
        // EN: Section 3 — full dump
        report.Log($"UA: === {fileLabel}: ПОВНИЙ дамп усіх констант по кожному чанку ===");
        report.Log($"EN: === {fileLabel}: FULL dump of every constant per chunk ===");
        foreach (var line in fullDumpLines)
            report.Log(line);
        report.Log();
    }

    // -------------------------------------------------------------------------
    private static void CollectConstants(LuaFunctionPrototype proto, List<(string Path, LuaConstant Constant)> acc)
    {
        foreach (var constant in proto.Constants)
            if (constant.Kind is LuaConstantKind.Number or LuaConstantKind.String)
                acc.Add((proto.Path, constant));

        foreach (var nested in proto.NestedPrototypes)
            CollectConstants(nested, acc);
    }
}
