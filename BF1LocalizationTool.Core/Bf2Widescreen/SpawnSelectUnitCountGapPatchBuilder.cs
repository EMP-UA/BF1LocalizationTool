// =============================================================================
// BF1LocalizationTool.Core — Bf2Widescreen/SpawnSelectUnitCountGapPatchBuilder.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Положення напису "Кількість бійців" на екрані вибору бійця
//     (`ifs_pc_spawnselect`, прототип `fnBuildScreen`). Докладно —
//     docs/BF2_SPAWNSELECT_GAP_FIX.md.
//
//     ЯК РУШІЙ СТАВИТЬ НАПИС: NewIFText з `valign="bottom"`, `texth` = R16
//     (0.20×H), `y` = R35. У ванільному коді `y = R31 - R16` (R31 — `y`
//     кнопки "Спавн"), тож нижній край текстового блока збігається з `y`
//     кнопки за будь-якої висоти шрифту. Над кнопку текст піднімають самі
//     рядки Locl: оригінали "Unit Count: %d\r\n\r\n" і
//     "Unit Count: %d  Max: %d\r\n\r\n" закінчуються двома порожніми
//     рядками, які при вирівнюванні по низу стоять ПІД видимим текстом.
//     Переклад зберігає ці переноси так само, як оригінал.
//
//     Два порожні рядки `gamefont_large` піднімають текст на ≈66 px над
//     кнопкою на 1920×1080 (крок рядка 33 px виміряно на трирядковому
//     написі на скріншоті гри). Патч опускає блок рівно на ОДИН рядок його
//     ж шрифту:
//         y = R31 - (R16 - R23),
//     де R23 = ScriptCB_GetFontHeight(шрифт напису) + 3 (pc61-64). Видимий
//     зазор між текстом і кнопкою дорівнює одному порожньому рядку мінус
//     3 px — розрахунково ≈31 px на 1920×1080 (на скріншоті гри: низ тексту
//     y≈891, верх підпису кнопки y≈923). Зазор масштабується разом зі шрифтом,
//     бо R23 обчислюється з висоти того самого шрифту.
//
//     РЕАЛІЗАЦІЯ: 8-словне вікно pc152-159 переписується на місці —
//     слот 0: `SUB R36 := R16 - R23`; слот 1: `SUB R35 := R31 - R36`;
//     слоти 2-7: інструкції pc153-158 без змін, зсунуті на одну позицію.
//     pc159 — ДОВЕДЕНО мертвий дублікат pc154 (`SETTABLE font:=R17`,
//     побітово ідентичний) і в нове вікно не копіюється. Розмір BODY-чанка,
//     розмір файлу й номери всіх інструкцій після вікна (включно з цілями
//     JMP/FORLOOP на pc172/242/244/259) не змінюються.
//
//     РЕГІСТРИ: R36 вільний на pc152 — востаннє записаний на pc149 (DIV),
//     спожитий на pc150 (SUB), наступний запис — pc173 у тілі циклу. R23
//     лише ЧИТАЄТЬСЯ: записаний на pc63-64, між pc65 і pc151 жодна
//     інструкція не має A=23 (перевіряється в `BuildPlan`), далі
//     читається в циклі сітки класів (pc186, pc212, pc220) — значення
//     там те саме. `maxstacksize` не змінюється (36 < 41).
//
//     ЩО НЕ ЗМІНЮЄТЬСЯ: `texth`, шрифт напису, положення кнопки. Кнопку
//     "Спавн" і модель бійця зсуває `SpawnSelectVerticalLayoutPatchBuilder`;
//     напис рухається разом із кнопкою, бо його `y` рахується від R31.
//
// EN: Position of the "Кількість бійців" label on the unit-selection
//     screen (`ifs_pc_spawnselect`, prototype `fnBuildScreen`). Details —
//     docs/BF2_SPAWNSELECT_GAP_FIX.md.
//
//     HOW THE ENGINE PLACES THE LABEL: a NewIFText with `valign="bottom"`,
//     `texth` = R16 (0.20×H), `y` = R35. The vanilla code sets
//     `y = R31 - R16` (R31 — the "Спавн" button's `y`), so the text block's
//     bottom edge equals the button's `y` for any font height. The Locl
//     strings themselves lift the text above the button: the originals
//     "Unit Count: %d\r\n\r\n" and "Unit Count: %d  Max: %d\r\n\r\n" end
//     with two empty lines, which sit BELOW the visible text under bottom
//     alignment. The translation keeps these line breaks exactly as the
//     original does.
//
//     Two empty `gamefont_large` lines lift the text ≈66 px above the
//     button at 1920×1080 (the 33 px line pitch is measured on the
//     three-line label in an in-game screenshot). The patch lowers the block by exactly ONE
//     line of its own font:
//         y = R31 - (R16 - R23),
//     where R23 = ScriptCB_GetFontHeight(the label's font) + 3 (pc61-64).
//     The visible gap between text and button equals one empty line minus
//     3 px — ≈31 px at 1920×1080 by calculation (in an in-game screenshot: text bottom
//     y≈891, button label top y≈923). The gap scales with the font, because R23
//     is computed from that same font's height.
//
//     IMPLEMENTATION: the 8-word window pc152-159 is rewritten in place —
//     slot 0: `SUB R36 := R16 - R23`; slot 1: `SUB R35 := R31 - R36`;
//     slots 2-7: instructions pc153-158 unchanged, shifted by one
//     position. pc159 is a PROVEN dead duplicate of pc154 (`SETTABLE
//     font:=R17`, bit-identical) and is not copied into the new window.
//     The BODY chunk's size, the file size and the pc numbers of every
//     instruction after the window (including the JMP/FORLOOP targets at
//     pc172/242/244/259) are unchanged.
//
//     REGISTERS: R36 is free at pc152 — last written at pc149 (DIV),
//     consumed at pc150 (SUB), next written at pc173 inside the loop. R23
//     is only READ: written at pc63-64, no instruction between pc65 and
//     pc151 has A=23 (checked in `BuildPlan`), and it is read later in the
//     class-grid loop (pc186, pc212, pc220) — with the same value there.
//     `maxstacksize` is unchanged (36 < 41).
//
//     NOT CHANGED: `texth`, the label's font, the button's position. The
//     "Спавн" button and the soldier model are moved by
//     `SpawnSelectVerticalLayoutPatchBuilder`; the label moves together
//     with the button, because its `y` is computed from R31.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Core.Bf2Widescreen;

public static class SpawnSelectUnitCountGapPatchBuilder
{
    // UA: Ім'я екрана (scr_/NAME) в ingame.lvl.
    // EN: The screen's name (scr_/NAME) in ingame.lvl.
    public const string ScreenName = "ifs_pc_spawnselect";

    // UA: pc першої інструкції 8-словного "вікна", яке тут переписується
    //     (старий SUB, що обчислює y тексту).
    // EN: pc of the first instruction in the 8-word "window" being rewritten
    //     (the old SUB that computes the text's y).
    public const int WindowStartPc = 152;
    public const int WindowLength = 8;

    // UA: Регістри, задіяні в патчі (див. коментар вище щодо вільного
    //     R36 на pc152 і незмінного R23 між pc65 і pc151).
    // EN: Registers involved in the patch (see the comment above on R36
    //     being free at pc152 and R23 being unchanged between pc65 and pc151).
    public const int RegisterTextY = 35; // R35 — y тексту / the text's y
    public const int RegisterButtonY = 31; // R31 — y кнопки "Ok" / the "Ok" button's y
    public const int RegisterTextHeight = 16; // R16 — 0.20×H, texth
    public const int RegisterLineHeight = 23; // R23 — висота рядка шрифту напису + 3 (pc61-64) / the label font's line height + 3 (pc61-64)
    public const int RegisterScratch = 36; // R36 — вільний на момент pc152 / dead at pc152
    public const int RegisterTextTable = 34; // R34 — таблиця NewIFText, що будується / the NewIFText table being built

    public sealed record Plan(
        long FileOffset,
        byte[] OldWindow,
        byte[] NewWindow,
        string PrototypePath);

    // -------------------------------------------------------------------------
    // UA: Знаходить `ifs_pc_spawnselect`, розбирає його BODY через
    //     Lua50BytecodeReader, перевіряє РІВНО ті 8 інструкцій, що
    //     переписуються (opcode+A+B+C кожної — включно з тим, що остання,
    //     pc159, ПОБІТОВО ідентична pc154, доводячи її "мертвість"), і
    //     повертає план патчу. Кидає виняток на БУДЬ-яку невідповідність.
    // EN: Finds `ifs_pc_spawnselect`, parses its BODY via
    //     Lua50BytecodeReader, verifies EXACTLY the 8 rewritten instructions
    //     (opcode+A+B+C of each — including that the last one,
    //     pc159, is BIT-IDENTICAL to pc154, proving it's "dead"), and
    //     returns the patch plan. Throws on ANY mismatch.
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

        // UA: R23 має тримати висоту рядка шрифту напису: pc64 —
        //     `ADD R23 := R23 + 3`, і між pc65 та початком вікна жодна
        //     інструкція не пише в R23 (жодна не має A=23).
        // EN: R23 must hold the label font's line height: pc64 is
        //     `ADD R23 := R23 + 3`, and no instruction between pc65 and the
        //     window's start writes R23 (none has A=23).
        var lineHeightInstruction = buildScreen.Instructions.FirstOrDefault(ins => ins.Pc == 64);
        if (lineHeightInstruction is null || lineHeightInstruction.Opcode != LuaOpcode.Add ||
            lineHeightInstruction.A != RegisterLineHeight || lineHeightInstruction.B != RegisterLineHeight)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{buildScreen.Path} pc=64: очікувалось ADD A={RegisterLineHeight} " +
                $"B={RegisterLineHeight} (висота рядка + 3). Файл відрізняється від проаналізованого — патч НЕ " +
                "застосовується. / " +
                $"EN: '{ScreenName}'/{buildScreen.Path} pc=64: expected ADD A={RegisterLineHeight} " +
                $"B={RegisterLineHeight} (line height + 3). The file differs from the one analyzed — the patch " +
                "is NOT applied.");

        var lineHeightWriters = buildScreen.Instructions
            .Where(ins => ins.Pc > 64 && ins.Pc < WindowStartPc && ins.A == RegisterLineHeight)
            .ToList();
        if (lineHeightWriters.Count > 0)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{buildScreen.Path}: R{RegisterLineHeight} змінюється між pc65 і " +
                $"pc{WindowStartPc - 1} (напр. pc={lineHeightWriters[0].Pc}) — патч НЕ застосовується. / " +
                $"EN: '{ScreenName}'/{buildScreen.Path}: R{RegisterLineHeight} is changed between pc65 and " +
                $"pc{WindowStartPc - 1} (e.g. pc={lineHeightWriters[0].Pc}) — the patch is NOT applied.");

        var window = new LuaInstruction[WindowLength];
        for (var i = 0; i < WindowLength; i++)
        {
            var pc = WindowStartPc + i;
            var instruction = buildScreen.Instructions.FirstOrDefault(ins => ins.Pc == pc);
            if (instruction is null)
                throw new InvalidDataException(
                    $"UA: '{ScreenName}'/{buildScreen.Path}: інструкції з pc={pc} не існує. / " +
                    $"EN: '{ScreenName}'/{buildScreen.Path}: no instruction with pc={pc} exists.");
            window[i] = instruction;
        }

        RequireInstruction(window[0], LuaOpcode.Sub, 35, 31, RegisterTextHeight, ScreenName, buildScreen.Path);
        RequireInstruction(window[1], LuaOpcode.SetTable, 34, 171, 35, ScreenName, buildScreen.Path);
        RequireInstruction(window[2], LuaOpcode.SetTable, 34, 193, 17, ScreenName, buildScreen.Path);
        RequireInstruction(window[3], LuaOpcode.SetTable, 34, 199, 200, ScreenName, buildScreen.Path);
        RequireInstruction(window[4], LuaOpcode.SetTable, 34, 201, 202, ScreenName, buildScreen.Path);
        RequireInstruction(window[5], LuaOpcode.SetTable, 34, 203, 15, ScreenName, buildScreen.Path);
        RequireInstruction(window[6], LuaOpcode.SetTable, 34, 204, RegisterTextHeight, ScreenName, buildScreen.Path);
        // UA: pc159 має бути ПОБІТОВО тим самим, що й pc154 (window[2]) —
        //     саме це доводить, що це мертвий дублікат, а не щось значуще.
        // EN: pc159 must be BIT-IDENTICAL to pc154 (window[2]) — this is
        //     exactly what proves it's a dead duplicate, not something meaningful.
        RequireInstruction(window[7], LuaOpcode.SetTable, 34, 193, 17, ScreenName, buildScreen.Path);
        if (window[7].A != window[2].A || window[7].B != window[2].B || window[7].C != window[2].C ||
            window[7].Opcode != window[2].Opcode)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{buildScreen.Path}: pc159 очікувався ІДЕНТИЧНИМ pc154 (доказ " +
                "мертвого коду), але відрізняється — патч НЕ застосовується. / " +
                $"EN: '{ScreenName}'/{buildScreen.Path}: pc159 was expected to be IDENTICAL to pc154 " +
                "(proof of dead code), but differs — the patch is NOT applied.");

        // UA: 8 інструкцій мають бути СУЦІЛЬНИМИ 32 байтами (жодних
        //     пропусків) — інакше зміщення нижче буде хибним.
        // EN: The 8 instructions must be a CONTIGUOUS 32 bytes (no gaps) —
        //     otherwise the offset below would be wrong.
        for (var i = 0; i < WindowLength; i++)
        {
            if (window[i].WordOffset < 0)
                throw new InvalidDataException(
                    "UA: інструкція без дійсного зміщення (WordOffset<0) — внутрішня помилка парсера. / " +
                    "EN: an instruction with no valid offset (WordOffset<0) — a parser-internal error.");
            if (i > 0 && window[i].WordOffset != window[0].WordOffset + i * 4)
                throw new InvalidDataException(
                    $"UA: '{ScreenName}'/{buildScreen.Path}: pc={WindowStartPc + i} не йде одразу за " +
                    "попередньою інструкцією (несуцільне вікно) — патч НЕ застосовується. / " +
                    $"EN: '{ScreenName}'/{buildScreen.Path}: pc={WindowStartPc + i} does not immediately " +
                    "follow the previous instruction (non-contiguous window) — the patch is NOT applied.");
        }

        var oldWindow = new byte[WindowLength * 4];
        for (var i = 0; i < WindowLength; i++)
            Array.Copy(EncodeAbc(window[i].Opcode, window[i].A, window[i].B!.Value, window[i].C!.Value),
                0, oldWindow, i * 4, 4);

        var newWindow = new byte[WindowLength * 4];
        // UA: слот 0 -> SUB R36 := R16 - R23 (висота блока мінус один рядок).
        // EN: slot 0 -> SUB R36 := R16 - R23 (block height minus one line).
        Array.Copy(EncodeAbc(LuaOpcode.Sub, RegisterScratch, RegisterTextHeight, RegisterLineHeight), 0, newWindow, 0, 4);
        // UA: слот 1 -> SUB R35 := R31 - R36 (y тексту від y кнопки).
        // EN: slot 1 -> SUB R35 := R31 - R36 (the text's y from the button's y).
        Array.Copy(EncodeAbc(LuaOpcode.Sub, RegisterTextY, RegisterButtonY, RegisterScratch), 0, newWindow, 4, 4);
        // UA: слоти 2-7 -> інструкції pc153-158 (SETTABLE y, font, halign,
        //     valign, textw, texth) без змін, зсунуті на 1 позицію. pc159
        //     (мертвий дублікат) не копіюється.
        // EN: slots 2-7 -> instructions pc153-158 (SETTABLE y, font, halign,
        //     valign, textw, texth) unchanged, shifted by 1 position. pc159
        //     (the dead duplicate) is not copied.
        Array.Copy(oldWindow, 1 * 4, newWindow, 2 * 4, 6 * 4);

        var fileOffset = script.BodyChunk.FileDataOffset + window[0].WordOffset;

        return new Plan(fileOffset, oldWindow, newWindow, buildScreen.Path);
    }

    // -------------------------------------------------------------------------
    // UA: Перевірка ПІСЛЯ запису: заново знаходить прототип і повертає, що
    //     саме зараз лежить на pc152/pc153 — щоб підтвердити обидва SUB, а
    //     не просто "запис не впав з винятком".
    // EN: POST-write verification: re-locates the prototype and returns
    //     what is currently at pc152/pc153 — to confirm both SUBs, not
    //     merely "the write didn't throw".
    // -------------------------------------------------------------------------
    public static (LuaInstruction Scratch, LuaInstruction TextY) ReadCurrentPatch(UcfbChunk ingameLvlRoot)
    {
        var script = ScriptChunkLocator.FindAll(ingameLvlRoot)
            .FirstOrDefault(s => s.Name == ScreenName);
        if (script is null)
            throw new InvalidDataException(
                $"UA: Екран '{ScreenName}' не знайдено серед scr_-ресурсів ingame.lvl. / " +
                $"EN: Screen '{ScreenName}' was not found among ingame.lvl's scr_ resources.");

        var parsed = Lua50BytecodeReader.Parse(script.BodyChunk.RawData);
        var buildScreen = LocateBuildScreenPrototype(parsed.Root, ScreenName);

        var scratch = buildScreen.Instructions.FirstOrDefault(i => i.Pc == WindowStartPc);
        var textY = buildScreen.Instructions.FirstOrDefault(i => i.Pc == WindowStartPc + 1);
        if (scratch is null || textY is null)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{buildScreen.Path}: pc={WindowStartPc}/{WindowStartPc + 1} не існують " +
                "при повторній перевірці. / " +
                $"EN: '{ScreenName}'/{buildScreen.Path}: pc={WindowStartPc}/{WindowStartPc + 1} do not exist " +
                "on re-verification.");

        return (scratch, textY);
    }

    // -------------------------------------------------------------------------
    // UA: Знаходить прототип "fnBuildScreen" не за голим індексом, а за
    //     вмістом його пулу констант (усі три рядки мають бути присутні).
    // EN: Finds the "fnBuildScreen" prototype not by a bare index, but by
    //     its constant pool's content (all three strings must be present).
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

    private static void RequireInstruction(LuaInstruction instruction, LuaOpcode opcode, int a, int b, int c,
        string screenName, string protoPath)
    {
        if (instruction.Opcode != opcode || instruction.A != a || instruction.B != b || instruction.C != c)
            throw new InvalidDataException(
                $"UA: '{screenName}'/{protoPath} pc={instruction.Pc}: очікувалось {opcode} A={a} B={b} C={c}, " +
                $"знайдено {instruction.Opcode} A={instruction.A} B={instruction.B} C={instruction.C}. " +
                "Файл відрізняється від проаналізованого — патч НЕ застосовується. / " +
                $"EN: '{screenName}'/{protoPath} pc={instruction.Pc}: expected {opcode} A={a} B={b} C={c}, " +
                $"found {instruction.Opcode} A={instruction.A} B={instruction.B} C={instruction.C}. " +
                "The file differs from the one analyzed — the patch is NOT applied.");
    }

    // UA: Кодує iABC-слово за тим самим бітовим розкладом, що й
    //     Lua50BytecodeReader.DecodeInstruction (opcode у бітах 0-5, C у
    //     бітах 6-14, B у бітах 15-23, A у бітах 24-31) — навмисно
    //     НЕЗАЛЕЖНО від Lua50BytecodeWriter, щоб round-trip через нього не
    //     був потрібен для такого точкового патчу.
    // EN: Encodes an iABC word using the same bit layout as
    //     Lua50BytecodeReader.DecodeInstruction (opcode in bits 0-5, C in
    //     bits 6-14, B in bits 15-23, A in bits 24-31) — deliberately
    //     INDEPENDENT of Lua50BytecodeWriter, so a full round-trip through
    //     it isn't needed for a patch this targeted.
    private static byte[] EncodeAbc(LuaOpcode opcode, int a, int b, int c)
    {
        var word = ((uint)a << 24) | ((uint)b << 15) | ((uint)c << 6) | (uint)opcode;
        return BitConverter.GetBytes(word);
    }

    // -------------------------------------------------------------------------
    // UA: Застосовує план до КОПІЇ байтів файлу. Перед записом звіряє СТАРІ
    //     32 байти за зміщенням — інакше зміщення вказує не туди (той самий
    //     принцип, що й FontHeadHeightFix.Apply).
    // EN: Applies the plan to a COPY of the file bytes. Before writing, it
    //     checks the OLD 32 bytes at the offset — otherwise the offset
    //     points somewhere else (same principle as FontHeadHeightFix.Apply).
    // -------------------------------------------------------------------------
    public static byte[] Apply(byte[] ingameLvlBytes, Plan plan)
    {
        var output = (byte[])ingameLvlBytes.Clone();

        if (plan.FileOffset < 0 || plan.FileOffset + plan.OldWindow.Length > output.LongLength)
            throw new InvalidDataException(
                $"UA: Вікно за зміщенням 0x{plan.FileOffset:X} (довжина {plan.OldWindow.Length}) поза файлом " +
                $"(розмір {output.LongLength}). / " +
                $"EN: The window at offset 0x{plan.FileOffset:X} (length {plan.OldWindow.Length}) is outside " +
                $"the file (size {output.LongLength}).");

        for (var i = 0; i < plan.OldWindow.Length; i++)
        {
            if (output[plan.FileOffset + i] != plan.OldWindow[i])
                throw new InvalidDataException(
                    $"UA: За зміщенням 0x{plan.FileOffset + i:X} очікувався байт 0x{plan.OldWindow[i]:X2}, а " +
                    $"лежить 0x{output[plan.FileOffset + i]:X2}. / " +
                    $"EN: At offset 0x{plan.FileOffset + i:X} expected byte 0x{plan.OldWindow[i]:X2}, found " +
                    $"0x{output[plan.FileOffset + i]:X2}.");
        }

        Array.Copy(plan.NewWindow, 0, output, (int)plan.FileOffset, plan.NewWindow.Length);

        return output;
    }
}
