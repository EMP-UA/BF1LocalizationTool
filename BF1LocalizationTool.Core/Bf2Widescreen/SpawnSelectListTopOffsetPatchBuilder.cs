// =============================================================================
// BF1LocalizationTool.Core — Bf2Widescreen/SpawnSelectListTopOffsetPatchBuilder.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ГЕНЕРАТОР (production, входить у фінальний патч) / GENERATOR (production, part of the final patch)
// =============================================================================
// UA: ВИПРАВЛЕННЯ, ПІДТВЕРДЖЕНЕ РЕАЛЬНИМ ТЕСТОМ У ГРІ — перелік класів на
//     екрані вибору бійця (`ifs_pc_spawnselect`) стояв впритул до верхнього
//     краю екрана: за повного переліку (7 і більше комірок) відступ зверху
//     був значно менший за відступ знизу. Детальний розбір, зі значеннями
//     до/після — у docs/BF2_SPAWNSELECT_GAP_FIX.md.
//
//     ФОРМУЛА (диз-я `ifs_pc_SpawnSelect_fnBuildScreen`, той самий
//     прототип, що й у `SpawnSelectUnitCountGapPatchBuilder`):
//         y_i = R24 + i·(R9 + R27 + R23 + 2.0)   (pc185-190, i — індекс
//               комірки, 0-based)
//         R24 = 15.0 + R23                        (pc65: ADD R24 := K(15.0) + R23)
//         R23 = ScriptCB_GetFontHeight(шрифт заголовка) + 3.0   (pc61-64)
//     Тобто `R24` — БАЗОВИЙ відступ зверху для комірки i=0, і рівно ОДНА
//     константа (`15.0`, pc65, операнд B) задає його долю, що не залежить
//     від шрифту. Підйом усього переліку на Δ пікселів (без зміни кроку
//     сітки, розмірів комірок чи шрифту) досягається додаванням Δ ЛИШЕ до
//     цієї константи — так само, як `SpawnSelectUnitCountGapPatchBuilder`
//     додає свій запас (R9) до вже обчисленого значення, не чіпаючи решту
//     формули.
//
//     Δ = 30.0 px, підібрано за виміром на реальному знімку гри з повним
//     (7-комірковим) переліком: зсув усього переліку вниз на Δ (обидві
//     межі рухаються РАЗОМ, бо крок і висота сітки не змінюються) рівняє
//     відступи зверху й знизу. Той самий Δ прибирає диспропорцію для 1…7
//     комірок (сітка завжди розрахована щонайменше на 7).
//
//     ЧОМУ САМЕ ЦЯ КОНСТАНТА, А НЕ ЗАГАЛЬНА ТАБЛИЦЯ РОЗКЛАДКИ
//     (`Bf2LayoutTable.txt`/`AnchorInheritancePatchBuilder`): цей екран
//     реєструється через `NewIFShellScreen{Enter=...}` — гачок
//     `AddIFScreen`, на який спирається загальний `@post:`-механізм,
//     МОЖЕ спрацювати лише опосередковано (через спільний
//     `gIFShellScreenTemplate_fnEnter`, визначений поза цим скриптом), а
//     контейнер `Info`, що містить сітку, водночас несе `SideModel0`/
//     `SideModel1` (значки сторони) — зсув `Info` через `posy` зсунув би й
//     їх, чого формула-джерело (лише сітка) не потребує. Пряма правка ОДНІЄЇ
//     константи в тілі `fnBuildScreen`, що впливає РІВНО на сітку і на
//     жоден інший елемент екрана, безпечніша й перевіряється так само явно,
//     як уже застосований фікс `SpawnSelectUnitCountGapPatchBuilder`.
//
//     ПЕРЕВІРЕНО: (1) диз-ю `ifs_pc_SpawnSelect_fnBuildScreen`; (2)
//     константа `15.0` (pc65, операнд B) використовується в усьому
//     прототипі РІВНО один раз (перевіряється в `BuildPlan` — жодна інша
//     інструкція не читає той самий індекс константи); (3) виконанням
//     самої функції в незалежній Lua 5.0 VM з підставними
//     `ScriptCB_GetSafeScreenInfo`/`GetTeamClassCount` — обчислене `y_0`
//     збігається з `15.0+R23`, пораховане вручну. Механізм, повний доказ
//     безпечності й підтвердження в реальній грі — цей клас і
//     docs/BF2_SPAWNSELECT_GAP_FIX.md.
//
// EN: FIX, CONFIRMED BY A REAL IN-GAME TEST — the class list on the
//     unit-selection screen (`ifs_pc_spawnselect`) sat flush against the
//     top edge: with a full list (7+ slots) the top gap was much smaller
//     than the bottom gap. Full breakdown, with before/after values — see
//     docs/BF2_SPAWNSELECT_GAP_FIX.md.
//
//     THE FORMULA (disassembly of `ifs_pc_SpawnSelect_fnBuildScreen`, the
//     same prototype as in `SpawnSelectUnitCountGapPatchBuilder`):
//         y_i = R24 + i·(R9 + R27 + R23 + 2.0)   (pc185-190, i = the 0-based
//               slot index)
//         R24 = 15.0 + R23                        (pc65: ADD R24 := K(15.0) + R23)
//         R23 = ScriptCB_GetFontHeight(title font) + 3.0   (pc61-64)
//     So `R24` is the BASE top offset for slot i=0, and exactly ONE
//     constant (`15.0`, pc65, operand B) supplies the font-independent part
//     of it. Raising the whole list by Δ pixels (without touching the grid
//     pitch, cell sizes or the font) is done by adding Δ to ONLY this
//     constant — the same technique `SpawnSelectUnitCountGapPatchBuilder`
//     uses to add its own margin (R9) to an already-computed value, without
//     touching the rest of the formula.
//
//     Δ = 30.0 px, chosen from a measurement on a real in-game screenshot
//     with a full (7-slot) list: shifting the whole list down by Δ (both
//     edges move TOGETHER, since the grid's pitch and height do not
//     change) equalizes the top and bottom gaps. The same Δ removes the
//     imbalance for 1…7 slots (the grid is always sized for at least 7).
//
//     WHY THIS CONSTANT AND NOT THE GENERIC LAYOUT TABLE
//     (`Bf2LayoutTable.txt`/`AnchorInheritancePatchBuilder`): this screen is
//     registered via `NewIFShellScreen{Enter=...}` — the `AddIFScreen` hook
//     the generic `@post:` mechanism relies on could only fire indirectly
//     (through a shared `gIFShellScreenTemplate_fnEnter` defined outside
//     this script), and the `Info` container that holds the grid also
//     carries `SideModel0`/`SideModel1` (the side icons) — shifting `Info`
//     via `posy` would move those too, which the source of the imbalance
//     (the grid alone) does not need. A direct edit of ONE constant inside
//     `fnBuildScreen`'s body, affecting EXACTLY the grid and no other
//     screen element, is safer and just as explicitly verifiable as the
//     already-applied `SpawnSelectUnitCountGapPatchBuilder` fix.
//
//     VERIFIED: (1) by disassembling `ifs_pc_SpawnSelect_fnBuildScreen`;
//     (2) the `15.0` constant (pc65, operand B) is used EXACTLY once in the
//     whole prototype (checked in `BuildPlan` — no other instruction reads
//     the same constant index); (3) by running the function itself in an
//     independent Lua 5.0 VM with stand-in
//     `ScriptCB_GetSafeScreenInfo`/`GetTeamClassCount` — the computed `y_0`
//     matches `15.0+R23` calculated by hand. Mechanism, full safety proof
//     and the real-game confirmation — this class and
//     docs/BF2_SPAWNSELECT_GAP_FIX.md.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Core.Bf2Widescreen;

public static class SpawnSelectListTopOffsetPatchBuilder
{
    // UA: Ім'я екрана (scr_/NAME) в ingame.lvl.
    // EN: The screen's name (scr_/NAME) in ingame.lvl.
    public const string ScreenName = "ifs_pc_spawnselect";

    // UA: pc інструкції ADD, що обчислює R24 (базовий відступ зверху,
    //     формула — вище).
    // EN: pc of the ADD instruction computing R24 (the base top offset,
    //     formula above).
    public const int TargetPc = 65;

    public const int RegisterBaseOffset = 24; // R24 — базовий відступ / the base offset
    public const int RegisterTitleHeight = 23; // R23 — висота заголовка+3 / title height+3

    public const float OldConstantValue = 15.0f;
    public const float ShiftPx = 30.0f;
    public const float NewConstantValue = OldConstantValue + ShiftPx;

    public sealed record Plan(
        long FileOffset,
        byte[] OldValueBytes,
        byte[] NewValueBytes,
        string PrototypePath);

    // -------------------------------------------------------------------------
    // UA: Знаходить `ifs_pc_spawnselect`, розбирає його BODY, перевіряє, що
    //     на `TargetPc` лежить РІВНО очікувана інструкція (ADD, A=R24,
    //     C=R23, B — константа з числовим значенням `OldConstantValue`), що
    //     ця константа НІДЕ БІЛЬШЕ в прототипі не використовується, і
    //     повертає план патчу. Кидає виняток на будь-яку невідповідність.
    // EN: Finds `ifs_pc_spawnselect`, parses its BODY, verifies that
    //     `TargetPc` holds EXACTLY the expected instruction (ADD, A=R24,
    //     C=R23, B — a constant with numeric value `OldConstantValue`),
    //     that this constant is used NOWHERE ELSE in the prototype, and
    //     returns the patch plan. Throws on any mismatch.
    // -------------------------------------------------------------------------
    public static Plan BuildPlan(UcfbChunk ingameLvlRoot)
    {
        var script = ScriptChunkLocator.FindAll(ingameLvlRoot)
            .FirstOrDefault(s => s.Name == ScreenName);
        if (script is null)
            throw new InvalidDataException(
                $"UA: Екран '{ScreenName}' не знайдено серед scr_-ресурсів ingame.lvl. / " +
                $"EN: Screen '{ScreenName}' was not found among ingame.lvl's scr_ resources.");

        var parsed = Lua50BytecodeReader.Parse(script.BodyChunk.RawData);
        if (parsed.LeftoverBytes != 1)
            throw new InvalidDataException(
                $"UA: '{ScreenName}': після розбору лишилось {parsed.LeftoverBytes} байт (очікувався 1) — " +
                "формат BODY інший, ніж підтверджений. / " +
                $"EN: '{ScreenName}': {parsed.LeftoverBytes} bytes left over after parsing (expected 1) — " +
                "the BODY format differs from the confirmed one.");

        var buildScreen = LocateBuildScreenPrototype(parsed.Root, ScreenName);

        var instruction = buildScreen.Instructions.FirstOrDefault(ins => ins.Pc == TargetPc);
        if (instruction is null)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{buildScreen.Path}: інструкції з pc={TargetPc} не існує. / " +
                $"EN: '{ScreenName}'/{buildScreen.Path}: no instruction with pc={TargetPc} exists.");

        if (instruction.Opcode != LuaOpcode.Add || instruction.A != RegisterBaseOffset ||
            instruction.C != RegisterTitleHeight || instruction.B is null || instruction.B < 128)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{buildScreen.Path} pc={TargetPc}: очікувалось ADD A={RegisterBaseOffset} " +
                $"B=<константа> C={RegisterTitleHeight}, знайдено {instruction.Opcode} A={instruction.A} " +
                $"B={instruction.B} C={instruction.C}. Файл відрізняється від проаналізованого — патч НЕ " +
                "застосовується. / " +
                $"EN: '{ScreenName}'/{buildScreen.Path} pc={TargetPc}: expected ADD A={RegisterBaseOffset} " +
                $"B=<constant> C={RegisterTitleHeight}, found {instruction.Opcode} A={instruction.A} " +
                $"B={instruction.B} C={instruction.C}. The file differs from the one analyzed — the patch is " +
                "NOT applied.");

        var constantIndex = instruction.B.Value - 128;
        if (constantIndex < 0 || constantIndex >= buildScreen.Constants.Count)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{buildScreen.Path} pc={TargetPc}: індекс константи {constantIndex} поза " +
                "межами пулу констант. / " +
                $"EN: '{ScreenName}'/{buildScreen.Path} pc={TargetPc}: constant index {constantIndex} is out " +
                "of the constant pool's range.");

        var constant = buildScreen.Constants[constantIndex];
        if (constant.Kind != LuaConstantKind.Number || constant.NumberValue != OldConstantValue)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{buildScreen.Path} pc={TargetPc}: константа B очікувалась числом " +
                $"{OldConstantValue}, знайдено {constant.Kind}={constant.NumberValue}. Патч НЕ застосовується. / " +
                $"EN: '{ScreenName}'/{buildScreen.Path} pc={TargetPc}: constant B was expected to be the " +
                $"number {OldConstantValue}, found {constant.Kind}={constant.NumberValue}. The patch is NOT applied.");

        // UA: Доказ "мертвості" будь-якого іншого використання: жодна інша
        //     інструкція прототипу не адресує РІВНО той самий індекс
        //     константи через операнд B чи C (RK-кодування, зсув 128).
        //     Якщо адресує — константа спільна з чимось ще, і зміна її
        //     значення зачепила б і те, невідоме нам, місце.
        // EN: Proof that nothing else shares this constant: no other
        //     instruction in the prototype addresses the EXACT same
        //     constant index via operand B or C (RK encoding, bias 128). If
        //     one does, the constant is shared with something else, and
        //     changing its value would also affect that unknown spot.
        var rk = instruction.B.Value;
        var otherUses = buildScreen.Instructions
            .Where(ins => ins.Pc != TargetPc && (ins.B == rk || ins.C == rk))
            .ToList();
        if (otherUses.Count > 0)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{buildScreen.Path}: константа {OldConstantValue} (індекс {constantIndex}) " +
                $"використовується ще в {otherUses.Count} інструкції(ях) (напр. pc={otherUses[0].Pc}) — " +
                "точковий патч ризикований, потрібен окремий аналіз. / " +
                $"EN: '{ScreenName}'/{buildScreen.Path}: the constant {OldConstantValue} (index {constantIndex}) " +
                $"is also used by {otherUses.Count} other instruction(s) (e.g. pc={otherUses[0].Pc}) — a " +
                "targeted patch is risky here and needs separate analysis.");

        // UA: ValueOffset вказує на БАЙТ ТЕГУ константи (LoadConstants);
        //     сам float йде одразу після нього (тег 3 = LUA_TNUMBER, 1 байт
        //     + 4 байти float).
        // EN: ValueOffset points at the constant's TAG byte (LoadConstants);
        //     the float itself follows immediately after it (tag 3 =
        //     LUA_TNUMBER, 1 byte + a 4-byte float).
        var fileOffset = script.BodyChunk.FileDataOffset + constant.ValueOffset + 1;

        return new Plan(
            fileOffset,
            BitConverter.GetBytes(OldConstantValue),
            BitConverter.GetBytes(NewConstantValue),
            buildScreen.Path);
    }

    // -------------------------------------------------------------------------
    // UA: Перевірка ПІСЛЯ запису: заново знаходить прототип і повертає
    //     поточне числове значення константи на `TargetPc` — щоб
    //     підтвердити НОВЕ значення, а не просто "запис не впав з винятком".
    // EN: POST-write verification: re-locates the prototype and returns the
    //     current numeric value of the constant at `TargetPc` — to confirm
    //     the NEW value, not merely "the write didn't throw".
    // -------------------------------------------------------------------------
    public static float ReadCurrentValue(UcfbChunk ingameLvlRoot)
    {
        var script = ScriptChunkLocator.FindAll(ingameLvlRoot)
            .FirstOrDefault(s => s.Name == ScreenName);
        if (script is null)
            throw new InvalidDataException(
                $"UA: Екран '{ScreenName}' не знайдено серед scr_-ресурсів ingame.lvl. / " +
                $"EN: Screen '{ScreenName}' was not found among ingame.lvl's scr_ resources.");

        var parsed = Lua50BytecodeReader.Parse(script.BodyChunk.RawData);
        var buildScreen = LocateBuildScreenPrototype(parsed.Root, ScreenName);

        var instruction = buildScreen.Instructions.FirstOrDefault(ins => ins.Pc == TargetPc);
        if (instruction?.B is null)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{buildScreen.Path}: pc={TargetPc} не існує при повторній перевірці. / " +
                $"EN: '{ScreenName}'/{buildScreen.Path}: pc={TargetPc} does not exist on re-verification.");

        var constantIndex = instruction.B.Value - 128;
        return buildScreen.Constants[constantIndex].NumberValue;
    }

    // -------------------------------------------------------------------------
    // UA: Знаходить прототип "fnBuildScreen" не за голим індексом, а за
    //     вмістом його пулу констант (усі три рядки мають бути присутні) —
    //     той самий маркер, що й у `SpawnSelectUnitCountGapPatchBuilder`
    //     (той самий прототип обох фіксів).
    // EN: Finds the "fnBuildScreen" prototype not by a bare index, but by
    //     its constant pool's content (all three strings must be present)
    //     — the same marker as in `SpawnSelectUnitCountGapPatchBuilder`
    //     (both fixes share this one prototype).
    // -------------------------------------------------------------------------
    private static LuaFunctionPrototype LocateBuildScreenPrototype(LuaFunctionPrototype root, string screenName)
    {
        string[] required = ["UnitCount", "NewPCIFButton", "ifs.SpawnDisplay.Spawn"];

        bool HasAllMarkers(LuaFunctionPrototype proto)
        {
            var strings = proto.Constants
                .Where(k => k.Kind == LuaConstantKind.String)
                .Select(k => k.StringValue)
                .ToHashSet(StringComparer.Ordinal);
            return required.All(strings.Contains);
        }

        var candidates = root.NestedPrototypes.Where(HasAllMarkers).ToList();
        if (candidates.Count != 1)
            throw new InvalidDataException(
                $"UA: '{screenName}': знайдено {candidates.Count} вкладених прототипів із маркерними " +
                $"рядками ({string.Join(", ", required)}), очікувався рівно 1. / " +
                $"EN: '{screenName}': found {candidates.Count} nested prototypes with the marker strings " +
                $"({string.Join(", ", required)}), expected exactly 1.");

        return candidates[0];
    }

    // -------------------------------------------------------------------------
    // UA: Застосовує план до КОПІЇ байтів файлу. Перед записом звіряє СТАРІ
    //     4 байти за зміщенням — інакше зміщення вказує не туди (той самий
    //     принцип, що й FontHeadHeightFix.Apply/SpawnSelectUnitCountGapPatchBuilder.Apply).
    // EN: Applies the plan to a COPY of the file bytes. Before writing, it
    //     checks the OLD 4 bytes at the offset — otherwise the offset
    //     points somewhere else (same principle as
    //     FontHeadHeightFix.Apply/SpawnSelectUnitCountGapPatchBuilder.Apply).
    // -------------------------------------------------------------------------
    public static byte[] Apply(byte[] ingameLvlBytes, Plan plan)
    {
        var output = (byte[])ingameLvlBytes.Clone();

        if (plan.FileOffset < 0 || plan.FileOffset + plan.OldValueBytes.Length > output.LongLength)
            throw new InvalidDataException(
                $"UA: Зміщення 0x{plan.FileOffset:X} (довжина {plan.OldValueBytes.Length}) поза файлом " +
                $"(розмір {output.LongLength}). / " +
                $"EN: The offset 0x{plan.FileOffset:X} (length {plan.OldValueBytes.Length}) is outside the " +
                $"file (size {output.LongLength}).");

        for (var i = 0; i < plan.OldValueBytes.Length; i++)
        {
            if (output[plan.FileOffset + i] != plan.OldValueBytes[i])
                throw new InvalidDataException(
                    $"UA: За зміщенням 0x{plan.FileOffset + i:X} очікувався байт 0x{plan.OldValueBytes[i]:X2}, а " +
                    $"лежить 0x{output[plan.FileOffset + i]:X2}. / " +
                    $"EN: At offset 0x{plan.FileOffset + i:X} expected byte 0x{plan.OldValueBytes[i]:X2}, found " +
                    $"0x{output[plan.FileOffset + i]:X2}.");
        }

        Array.Copy(plan.NewValueBytes, 0, output, (int)plan.FileOffset, plan.NewValueBytes.Length);

        return output;
    }
}
