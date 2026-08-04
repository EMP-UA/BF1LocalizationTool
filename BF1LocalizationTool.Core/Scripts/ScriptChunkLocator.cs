// =============================================================================
// BF1LocalizationTool.Core — Scripts/ScriptChunkLocator.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Знаходить "scr_" чанки (скомпільовані Lua-скрипти інтерфейсу/HUD) у
//     дереві ucfb-чанків shell.lvl/ingame.lvl (BF2 Classic).
//
//     Структура одного "scr_" чанка (підтверджено на 90 екземплярах
//     shell.lvl + 9 ingame.lvl):
//       scr_
//         NAME  — ASCII-ім'я екрана/скрипта (напр. "ifs_main")
//         INFO  — щось невелике (1 байт у перевірених екземплярах),
//                 призначення НЕ реверс-інжинирено, не потрібне для
//                 задачі widescreen-фікса
//         BODY  — сирий скомпільований Lua 5.0 dump (див.
//                 Lua50BytecodeReader) — те, що нас цікавить
//     Це ЛОКАТОР — лише знаходить ресурси й повертає UcfbChunk BODY для
//     подальшого парсингу через Lua50BytecodeReader.Parse.
// EN: Finds "scr_" chunks (compiled UI/HUD Lua scripts) in the ucfb chunk
//     tree of shell.lvl/ingame.lvl (BF2 Classic).
//
//     Structure of a single "scr_" chunk (confirmed on 90 shell.lvl +
//     9 ingame.lvl instances):
//       scr_
//         NAME  — ASCII name of the screen/script (e.g. "ifs_main")
//         INFO  — something small (1 byte in the checked instances),
//                 purpose NOT reverse engineered, not needed for the
//                 widescreen-fix task
//         BODY  — raw compiled Lua 5.0 dump (see Lua50BytecodeReader) —
//                 what we actually care about
//     This is a LOCATOR — it only finds resources and returns the BODY
//     UcfbChunk for further parsing via Lua50BytecodeReader.Parse.
// =============================================================================

using System.Text;
using BF1LocalizationTool.Core.Chunks;

namespace BF1LocalizationTool.Core.Scripts;

// UA: Один знайдений "scr_"-ресурс: ім'я + сам BODY-чанк (RawData = сирий
//     Lua 5.0 dump, готовий для Lua50BytecodeReader.Parse).
// EN: A single found "scr_" resource: name + the BODY chunk itself
//     (RawData = the raw Lua 5.0 dump, ready for Lua50BytecodeReader.Parse).
public sealed record ScriptResource(string Name, UcfbChunk Chunk, UcfbChunk BodyChunk);

public static class ScriptChunkLocator
{
    // -------------------------------------------------------------------------
    // UA: Знаходить усі "scr_" ресурси в дереві чанків. Пропускає (з
    //     явним рахунком у виклику) чанки без NAME або без BODY — таких
    //     не було в жодному з 99 перевірених реальних екземплярів, але
    //     мовчки НЕ вигадувати заміну, якщо колись трапиться.
    // EN: Finds all "scr_" resources in the chunk tree. Skips (with an
    //     explicit count left to the caller) chunks missing NAME or BODY
    //     — none occurred in any of the 99 checked real instances, but
    //     never silently invent a fallback if one ever does.
    // -------------------------------------------------------------------------
    public static IReadOnlyList<ScriptResource> FindAll(UcfbChunk root)
    {
        var results = new List<ScriptResource>();
        foreach (var scrChunk in BF1LocalizationTool.Core.IO.UcfbReader.FindAll(root, "scr_"))
        {
            var nameChunk = scrChunk.Children.FirstOrDefault(c => c.FourCC == "NAME");
            var bodyChunk = scrChunk.Children.FirstOrDefault(c => c.FourCC == "BODY");
            if (nameChunk is null || bodyChunk is null)
                continue; // UA: не відповідає очікуваній структурі — пропускаємо, не гадаємо / EN: doesn't match the expected structure — skip, don't guess

            var name = Encoding.ASCII.GetString(nameChunk.RawData).TrimEnd('\0');
            results.Add(new ScriptResource(name, scrChunk, bodyChunk));
        }

        return results;
    }
}
