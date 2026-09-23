// =============================================================================
// BF1LocalizationTool.Core — Bf2Widescreen/SpawnSelectUnitCountGapPatchBuilder.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ГЕНЕРАТОР (production, входить у фінальний патч) / GENERATOR (production, part of the final patch)
// =============================================================================
// UA: ВИПРАВЛЕННЯ, ПІДТВЕРДЖЕНЕ РЕАЛЬНИМ ТЕСТОМ У ГРІ — на вихідному стані
//     напис "Кількість бійців" перекривався кнопкою "Відродження" на екрані
//     вибору бійця. Детальний розбір, зі значеннями до/після — у
//     docs/BF2_SPAWNSELECT_GAP_FIX.md.
//
//     ЦЕЙ ПАТЧ НЕ ЧІПАЄ ШРИФТ (на відміну від фіксу висоти шрифту —
//     FontHeadHeightFix.cs). Він зсуває
//     ПОЗИЦІЮ напису "Кількість бійців" — кнопка "Ok"/"Відродження" лишається
//     на своєму місці (0.9×H), а текст підіймається вище на 0.03×H, даючи
//     реальний зазор між ними.
//
//     ЧОМУ ПРОСТА ЗМІНА ОДНІЄЇ КОНСТАНТИ НЕ ПРАЦЮЄ: нижній край
//     текстового блока обчислюється як `(y_кнопки - висота_тексту) +
//     висота_тексту = y_кнопки` — це АЛГЕБРАЇЧНА ТОТОЖНІСТЬ, що виконується
//     для будь-якої висоти. Щоб створити зазор, потрібно відняти від `y`
//     тексту БІЛЬШЕ, ніж сама висота блока (`R16` = 0.20×H) — а серед уже
//     обчислених у цій функції регістрів немає жодного готового значення,
//     більшого за `R16`.
//
//     РІШЕННЯ: ОДНА нова інструкція (`ADD R36 := R16 + R9`, де `R9` = 0.03×H
//     — уже обчислене значення, використане деінде як типовий "запас"), і
//     перепризначення операнда C у наявній інструкції `SUB` (з `R16` на
//     `R36`). Обидві інструкції поміщаються РІВНО в те саме місце, що й
//     раніше займали 2 інструкції: сама ця `SUB` (pc152) і "мертвий",
//     ДОВЕДЕНО зайвий дублікат `SETTABLE font:=R17` (pc159 — БУКВАЛЬНО
//     ідентичний вже виконаному на pc154, отже нічого не змінює). Розмір
//     BODY-чанка, розмір усього файлу, номери всіх інструкцій ПІСЛЯ цього
//     вікна (включно з цілями JMP/FORLOOP на pc172/242/244/259) — УСЕ
//     лишається БАЙТ-У-БАЙТ незмінним. Перевірено: (1) ручним симуляційним
//     трасуванням формули на кожному кроці; (2) round-trip через
//     Lua50BytecodeReader на реальному ingame.lvl — після патчу файл
//     парситься без винятків, і вся решта функції (до pc152 і після pc159)
//     побайтово ідентична оригіналу.
//
//     Реєстр R36 обрано як "мертвий" на момент pc152: востаннє записаний на
//     pc149 (DIV), спожитий одразу на pc150 (SUB), і НЕ читається знову аж
//     до pc173 (де він і так перезаписується заново, вже в тілі циклу нижче
//     по функції) — це перевірено повним переглядом усіх появ R36 у
//     дизасемблюванні. `maxstacksize` НЕ підвищується (36 < 41, реєстр уже
//     в межах наявного стека).
//
//     НАСЛІДОК: `height`/`texth` тексту (0.20×H) — НЕ змінюється, отже
//     власна геометрія блока (перенесення рядків, vcenter тощо, якщо колись
//     застосовується) лишається такою, як спроєктовано. Кнопка "Ok" також
//     НЕ змінюється (окрема інструкція, окрема константа, не зачіпається).
//
//     РЕЗУЛЬТАТ: чистий зазор між написом і кнопкою — збільшений (ФІКС
//     HEAD) кириличний шрифт кнопку не перекриває. Деталі —
//     docs/BF2_SPAWNSELECT_GAP_FIX.md, розділ "Підтвердження".
//
// EN: FIX, CONFIRMED BY A REAL IN-GAME TEST — in the unpatched state the
//     "Кількість бійців" label was covered by the "Відродження" button on
//     the unit-selection screen. Full breakdown, with before/after values —
//     see docs/BF2_SPAWNSELECT_GAP_FIX.md.
//
//     THIS PATCH DOES NOT TOUCH THE FONT (unlike the font-height fix —
//     FontHeadHeightFix.cs). It
//     shifts the POSITION of the "Кількість бійців" label — the "Ok"/
//     "Відродження" button stays exactly where it was (0.9×H), while the
//     text is moved up by 0.03×H, creating a real gap between them.
//
//     WHY A SINGLE-CONSTANT EDIT DOES NOT WORK: the text block's
//     bottom edge is computed as `(button_y - text_height) + text_height =
//     button_y` — an ALGEBRAIC IDENTITY that holds for ANY height. To create
//     a gap, something MORE than the block's own height (`R16` = 0.20×H)
//     must be subtracted for the text's `y` — and none of the registers
//     already computed in this function holds a value bigger than `R16`.
//
//     THE FIX: ONE new instruction (`ADD R36 := R16 + R9`, where `R9` =
//     0.03×H — an already-computed value, used elsewhere as a typical
//     "margin"), plus repointing the C operand of an existing `SUB`
//     instruction (from `R16` to `R36`). Both instructions fit EXACTLY into
//     the space previously occupied by 2 instructions: this same `SUB`
//     (pc152) and a "dead", PROVEN-redundant duplicate `SETTABLE
//     font:=R17` (pc159 — LITERALLY identical to the one already executed
//     at pc154, so it changes nothing). The BODY chunk's size, the whole
//     file's size, and the pc numbers of every instruction AFTER this
//     window (including the JMP/FORLOOP targets at pc172/242/244/259) all
//     stay BYTE-FOR-BYTE unchanged. Verified: (1) by manually tracing the
//     formula step by step; (2) by a round-trip through
//     Lua50BytecodeReader on the real ingame.lvl — after the patch the file
//     parses with no exceptions, and every instruction before pc152 and
//     after pc159 is byte-identical to the original.
//
//     Register R36 was chosen because it's dead at pc152: last written at
//     pc149 (DIV), consumed immediately at pc150 (SUB), and not read again
//     until pc173 (where it's overwritten fresh anyway, inside the loop
//     further down the function) — verified by reviewing every appearance
//     of R36 in the disassembly. `maxstacksize` is NOT raised (36 < 41, the
//     register is already within the existing stack frame).
//
//     CONSEQUENCE: the text's `height`/`texth` (0.20×H) is NOT changed, so
//     the block's own geometry (line wrapping, vcenter, etc., if ever used)
//     stays exactly as designed. The "Ok" button is also NOT touched
//     (a separate instruction, a separate constant, unaffected).
//
//     RESULT: a clean gap between the label and the button — the
//     enlarged (HEAD FIX) Cyrillic font does not cover the button.
//     Details — docs/BF2_SPAWNSELECT_GAP_FIX.md, "Confirmation" section.
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

    // UA: Реєстри, задіяні в патчі (див. коментар вище щодо доведеної
    //     "мертвості" R36 на момент pc152).
    // EN: Registers involved in the patch (see the comment above proving
    //     R36 is "dead" at pc152).
    public const int RegisterTextY = 35; // R35 — y тексту / the text's y
    public const int RegisterButtonY = 31; // R31 — y кнопки "Ok" / the "Ok" button's y
    public const int RegisterTextHeight = 16; // R16 — 0.20×H, texth
    public const int RegisterMargin = 9; // R9 — 0.03×H, вже обчислений запас / already-computed margin
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
        // UA: слот 0 (був SUB, pc152) -> нова інструкція ADD R36:=R16+R9.
        // EN: slot 0 (was SUB, pc152) -> the new ADD R36:=R16+R9 instruction.
        Array.Copy(EncodeAbc(LuaOpcode.Add, RegisterScratch, RegisterTextHeight, RegisterMargin), 0, newWindow, 0, 4);
        // UA: слот 1 (був SETTABLE y:=R35, тепер стає "новим SUB") -> той
        //     самий SUB, що був у слоті 0, але C змінено з R16 на R36.
        // EN: slot 1 (was SETTABLE y:=R35, now becomes the "new SUB") -> the
        //     same SUB that was in slot 0, but with C changed from R16 to R36.
        Array.Copy(EncodeAbc(LuaOpcode.Sub, RegisterTextY, RegisterButtonY, RegisterScratch), 0, newWindow, 4, 4);
        // UA: слоти 2-7 -> старі слоти 1-6 (SETTABLE y, font, halign,
        //     valign, textw, texth) — БЕЗ ЗМІН, просто зсунуті на 1 позицію.
        //     Старий слот 7 (мертвий дублікат) НЕ копіюється нікуди.
        // EN: slots 2-7 -> old slots 1-6 (SETTABLE y, font, halign, valign,
        //     textw, texth) — UNCHANGED, just shifted by 1 position. The old
        //     slot 7 (dead duplicate) is copied nowhere.
        Array.Copy(oldWindow, 1 * 4, newWindow, 2 * 4, 6 * 4);

        var fileOffset = script.BodyChunk.FileDataOffset + window[0].WordOffset;

        return new Plan(fileOffset, oldWindow, newWindow, buildScreen.Path);
    }

    // -------------------------------------------------------------------------
    // UA: Перевірка ПІСЛЯ запису: заново знаходить прототип і повертає, що
    //     саме зараз лежить на pc152/pc153 — щоб підтвердити ADD+SUB, а не
    //     просто "запис не впав з винятком".
    // EN: POST-write verification: re-locates the prototype and returns
    //     what is currently at pc152/pc153 — to confirm ADD+SUB, not merely
    //     "the write didn't throw".
    // -------------------------------------------------------------------------
    public static (LuaInstruction Add, LuaInstruction Sub) ReadCurrentPatch(UcfbChunk ingameLvlRoot)
    {
        var script = ScriptChunkLocator.FindAll(ingameLvlRoot)
            .FirstOrDefault(s => s.Name == ScreenName);
        if (script is null)
            throw new InvalidDataException(
                $"UA: Екран '{ScreenName}' не знайдено серед scr_-ресурсів ingame.lvl. / " +
                $"EN: Screen '{ScreenName}' was not found among ingame.lvl's scr_ resources.");

        var parsed = Lua50BytecodeReader.Parse(script.BodyChunk.RawData);
        var buildScreen = LocateBuildScreenPrototype(parsed.Root, ScreenName);

        var add = buildScreen.Instructions.FirstOrDefault(i => i.Pc == WindowStartPc);
        var sub = buildScreen.Instructions.FirstOrDefault(i => i.Pc == WindowStartPc + 1);
        if (add is null || sub is null)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{buildScreen.Path}: pc={WindowStartPc}/{WindowStartPc + 1} не існують " +
                "при повторній перевірці. / " +
                $"EN: '{ScreenName}'/{buildScreen.Path}: pc={WindowStartPc}/{WindowStartPc + 1} do not exist " +
                "on re-verification.");

        return (add, sub);
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
