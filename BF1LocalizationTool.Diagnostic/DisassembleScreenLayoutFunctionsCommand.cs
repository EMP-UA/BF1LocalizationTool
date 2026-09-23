// =============================================================================
// BF1LocalizationTool.Diagnostic — DisassembleScreenLayoutFunctionsCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Другий крок widescreen-фікса BF2 (після
//     AnalyzeShellScriptConstantsCommand, який лише перелічив константи
//     без контексту виклику). Тут — ПОВНИЙ дизасемблер (через
//     Lua50BytecodeReader + LuaDisassembler, MAXSTACK=128 підтверджено
//     емпірично) ЛИШЕ для функцій, чий ВЛАСНИЙ пул констант містить
//     ключове слово (ScreenRelativeX/Y, GetSafeScreenInfo тощо) — щоб не
//     видавати дизасемблер УСІХ 99 чанків (мільйони рядків), а лише
//     реально релевантні функції.
//
//     Мета — прочитати РЕАЛЬНУ логіку побудови layout (не просто список
//     чисел без контексту) і знайти, чи є десь жорстко закодоване
//     припущення про 4:3/роздільність, яке можна буде патчити in-place.
// EN: The second step of the BF2 widescreen fix (after
//     AnalyzeShellScriptConstantsCommand, which only listed constants
//     without call context). Here — a FULL disassembly (via
//     Lua50BytecodeReader + LuaDisassembler, MAXSTACK=128 empirically
//     confirmed) ONLY for functions whose OWN constant pool contains a
//     keyword (ScreenRelativeX/Y, GetSafeScreenInfo, etc.) — so as not to
//     dump ALL 99 chunks' disassembly (millions of lines), only the
//     actually relevant functions.
//
//     Goal — read the REAL layout-building logic (not just a
//     context-free number list) and find whether a hardcoded 4:3/
//     resolution assumption exists anywhere that could be patched
//     in-place.
// =============================================================================

using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class DisassembleScreenLayoutFunctionsCommand
{
    private static readonly string[] TargetKeywords =
    [
        "ScreenRelative", "GetSafeScreenInfo", "GetScreenInfo",
        "AspectRatio", "aspect", "SafeZone", "resolution",
    ];

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

        report.Log($"UA: === {fileLabel}: дизасемблер функцій за ключовими словами ({string.Join(", ", TargetKeywords)}) ===");
        report.Log($"EN: === {fileLabel}: disassembly of functions matching keywords ({string.Join(", ", TargetKeywords)}) ===");
        report.Log();

        var matchedCount = 0;
        foreach (var script in scripts)
        {
            LuaChunkParseResult parsed;
            try
            {
                parsed = Lua50BytecodeReader.Parse(script.BodyChunk.RawData);
            }
            catch (Exception ex)
            {
                report.Log($"UA: [{gameLabel}] {script.Name}: ПОМИЛКА ПАРСИНГУ: {ex.Message}");
                report.Log($"EN: [{gameLabel}] {script.Name}: PARSE ERROR: {ex.Message}");
                continue;
            }

            var matches = new List<LuaFunctionPrototype>();
            CollectMatching(parsed.Root, matches);

            if (matches.Count == 0)
                continue;

            matchedCount += matches.Count;
            foreach (var proto in matches)
            {
                var lines = new List<string>();
                LuaDisassembler.Disassemble(proto, $"{fileLabel} :: {script.Name}", lines);
                foreach (var line in lines)
                    report.Log(line);
                report.Log();
            }
        }

        report.Log($"UA: [{gameLabel}] {fileLabel}: {matchedCount} функцій відповідали ключовим словам.");
        report.Log($"EN: [{gameLabel}] {fileLabel}: {matchedCount} functions matched the keywords.");
        report.Log();
    }

    // -------------------------------------------------------------------------
    // UA: Перевіряє ВЛАСНИЙ (не успадкований від дітей) пул констант
    //     кожного прототипу окремо — щоб знайти саме ту функцію, яка
    //     РЕАЛЬНО згадує ключове слово, а не будь-якого її предка/нащадка.
    // EN: Checks each prototype's OWN (not inherited from children)
    //     constant pool separately — to find the SPECIFIC function that
    //     ACTUALLY mentions the keyword, not any of its ancestors/
    //     descendants.
    // -------------------------------------------------------------------------
    private static void CollectMatching(LuaFunctionPrototype proto, List<LuaFunctionPrototype> acc)
    {
        var hasMatch = proto.Constants.Any(k =>
            k.Kind == LuaConstantKind.String && k.StringValue is not null &&
            TargetKeywords.Any(kw => k.StringValue.Contains(kw, StringComparison.OrdinalIgnoreCase)));

        if (hasMatch)
            acc.Add(proto);

        foreach (var nested in proto.NestedPrototypes)
            CollectMatching(nested, acc);
    }
}
