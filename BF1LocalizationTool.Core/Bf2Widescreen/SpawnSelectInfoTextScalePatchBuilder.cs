// =============================================================================
// BF1LocalizationTool.Core — Bf2Widescreen/SpawnSelectInfoTextScalePatchBuilder.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ГЕНЕРАТОР (production, входить у фінальний патч) / GENERATOR (production, part of the final patch)
// =============================================================================
// UA: Масштаб тексту спорядження в комірках класів на екрані вибору бійця
//     (`ifs_pc_spawnselect`, прототип `fnBuildScreen`).
//
//     Кількість слотів R5 = min(max(7, класів команди 1, класів команди 2), 10).
//     Висота комірки R27 = safeH / R5 - 0.03 × safeH - R23, де R23 — висота
//     шрифту заголовка + 3. Масштаб тексту спорядження:
//         HScale = VScale = R27 / (R37 × FontHeight(R18))
//     R37 — на скільки рядків розрахований блок тексту. Ділильник R37
//     обирається на pc240-245:
//         pc240  LOADK R37 := 4.0
//         pc241  LT  (7.0 < R5)   -> інакше JMP на pc246
//         pc243  LT  (R20 < 960.0) -> інакше JMP на pc246   (R20 — висота екрана)
//         pc244  JMP +1                                      (перехід на pc246)
//         pc245  LOADK R37 := 3.0
//     Тобто ділильник 3.0 діє лише при R5 > 7 І висоті екрана менше 960 px.
//     На 1080p за R5 > 7 лишається 4.0: масштаб (і крок рядків) менший, ніж
//     розрахований на висоту комірки.
//
//     Виміряно на скріншотах гри 1920×1080: крок рядків спорядження 18 px
//     при 7 класах (R5 = 7) і 11 px при 9 класах (R5 = 9, карта Mos Eisley,
//     штурм); висота гліфів ≈10 px проти ≈6 px, літери малюються з
//     нерівним вертикальним зсувом.
//
//     Патч: pc244 `JMP +1` -> `JMP +0` (перехід на наступну інструкцію,
//     pc245). Ділильник 3.0 діє за будь-якої висоти екрана, коли R5 > 7.
//     При R5 <= 7 гілка не досягається — комірки з 7 класами не змінюються.
//     Крок рядків при R5 = 9 зростає в 4/3 раза (≈11 -> ≈14.7 px); три
//     рядки займають ≈44 px, чотири — ≈59 px у комірці висотою ≈65 px.
//     Розмір BODY-чанка й файлу не змінюється; змінюється одне слово
//     інструкції.
//
// EN: The scale of the equipment text in the class cells on the
//     unit-selection screen (`ifs_pc_spawnselect`, prototype
//     `fnBuildScreen`).
//
//     The slot count is R5 = min(max(7, team 1 classes, team 2 classes), 10).
//     The cell height is R27 = safeH / R5 - 0.03 × safeH - R23, where R23 is
//     the title font height + 3. The equipment text scale:
//         HScale = VScale = R27 / (R37 × FontHeight(R18))
//     R37 is the number of lines the text block is sized for. The divisor
//     R37 is chosen at pc240-245:
//         pc240  LOADK R37 := 4.0
//         pc241  LT  (7.0 < R5)    -> otherwise JMP to pc246
//         pc243  LT  (R20 < 960.0) -> otherwise JMP to pc246   (R20 — screen height)
//         pc244  JMP +1                                         (jump to pc246)
//         pc245  LOADK R37 := 3.0
//     That is, the divisor 3.0 applies only when R5 > 7 AND the screen height
//     is below 960 px. At 1080p with R5 > 7 the divisor stays 4.0: the scale
//     (and the line pitch) is smaller than the cell height allows.
//
//     Measured on 1920×1080 in-game screenshots: the equipment line pitch is
//     18 px with 7 classes (R5 = 7) and 11 px with 9 classes (R5 = 9, Mos
//     Eisley, Assault); glyph height ≈10 px against ≈6 px, letters are drawn
//     with an uneven vertical offset.
//
//     The patch: pc244 `JMP +1` -> `JMP +0` (a jump to the next instruction,
//     pc245). The divisor 3.0 applies at any screen height whenever R5 > 7.
//     With R5 <= 7 the branch is not reached — cells with 7 classes are not
//     changed. With R5 = 9 the line pitch grows by 4/3 (≈11 -> ≈14.7 px);
//     three lines take ≈44 px, four lines ≈59 px in a cell ≈65 px tall.
//     The BODY chunk's and the file's size are unchanged; one instruction
//     word changes.
//
//     РЕЗУЛЬТАТ У ГРІ / IN-GAME RESULT (1920×1080, Mos Eisley, Assault,
//     9 classes): the line pitch is ≈14 px, the glyphs are legible; with
//     7 classes the pitch is unchanged (18 px).
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Core.Bf2Widescreen;

public static class SpawnSelectInfoTextScalePatchBuilder
{
    // UA: Ім'я екрана (scr_/NAME) в ingame.lvl.
    // EN: The screen's name (scr_/NAME) in ingame.lvl.
    public const string ScreenName = "ifs_pc_spawnselect";

    // UA: pc гілки вибору ділильника R37 у fnBuildScreen.
    // EN: pcs of the R37 divisor selection branch in fnBuildScreen.
    public const int DefaultDivisorPc = 240;
    public const int SlotCountTestPc = 241;
    public const int SlotCountJumpPc = 242;
    public const int HeightTestPc = 243;
    public const int HeightJumpPc = 244;
    public const int ReducedDivisorPc = 245;

    public const int RegisterDivisor = 37; // R37
    public const int RegisterSlotCount = 5; // R5
    public const int RegisterScreenHeight = 20; // R20
    public const float SlotCountThreshold = 7f;
    public const float ScreenHeightThreshold = 960f;
    public const float DefaultDivisor = 4f;
    public const float ReducedDivisor = 3f;

    // UA: Зсув переходу на pc244: до патча +1 (на pc246), після — 0 (на pc245).
    // EN: The jump offset at pc244: +1 before the patch (to pc246), 0 after (to pc245).
    public const int OldHeightJumpOffset = 1;
    public const int NewHeightJumpOffset = 0;

    public sealed record PatchSite(
        string Label,
        long FileOffset,
        byte[] OldBytes,
        byte[] NewBytes);

    public sealed record Plan(
        IReadOnlyList<PatchSite> Sites,
        string BuildScreenPrototypePath);

    // -------------------------------------------------------------------------
    // UA: Знаходить `ifs_pc_spawnselect`, розбирає BODY, перевіряє всю гілку
    //     pc240-245 (опкоди, регістри, значення констант, цілі переходів) і
    //     повертає план. Кидає виняток на будь-яку невідповідність.
    // EN: Finds `ifs_pc_spawnselect`, parses the BODY, verifies the whole
    //     pc240-245 branch (opcodes, registers, constant values, jump
    //     targets) and returns the plan. Throws on any mismatch.
    // -------------------------------------------------------------------------
    public static Plan BuildPlan(UcfbChunk ingameLvlRoot)
    {
        var script = FindScript(ingameLvlRoot);

        var parsed = Lua50BytecodeReader.Parse(script.BodyChunk.RawData);
        if (parsed.LeftoverBytes != 1)
            throw new InvalidDataException(
                $"UA: '{ScreenName}': після розбору лишилось {parsed.LeftoverBytes} байт (очікувався 1) — " +
                "формат BODY інший, ніж підтверджений. / " +
                $"EN: '{ScreenName}': {parsed.LeftoverBytes} bytes left over after parsing (expected 1) — " +
                "the BODY format differs from the confirmed one.");

        var buildScreen = LocateSinglePrototype(parsed.Root);
        VerifyBranch(buildScreen);

        var jump = RequirePc(buildScreen, HeightJumpPc);
        RequireWordOffset(jump);
        var jumpBx = jump.Bx ?? throw Mismatch(buildScreen, jump, "JMP");
        if (jump.SBx != OldHeightJumpOffset)
            throw Mismatch(buildScreen, jump, $"JMP {OldHeightJumpOffset} (вихідний файл / the original file)");

        var site = new PatchSite(
            $"{buildScreen.Path} pc={HeightJumpPc}",
            script.BodyChunk.FileDataOffset + jump.WordOffset,
            EncodeJump(jump.A, jumpBx),
            EncodeJump(jump.A, jumpBx - OldHeightJumpOffset + NewHeightJumpOffset));

        return new Plan([site], buildScreen.Path);
    }

    // -------------------------------------------------------------------------
    // UA: Перевірка ПІСЛЯ запису: заново розбирає файл, перевіряє гілку й
    //     повертає поточний зсув переходу на pc244.
    // EN: POST-write verification: re-parses the file, verifies the branch
    //     and returns the current jump offset at pc244.
    // -------------------------------------------------------------------------
    public static int ReadCurrentHeightJumpOffset(UcfbChunk ingameLvlRoot)
    {
        var script = FindScript(ingameLvlRoot);
        var parsed = Lua50BytecodeReader.Parse(script.BodyChunk.RawData);

        var buildScreen = LocateSinglePrototype(parsed.Root);
        var jump = RequirePc(buildScreen, HeightJumpPc);
        if (jump.Opcode != LuaOpcode.Jmp)
            throw Mismatch(buildScreen, jump, "JMP");

        return jump.SBx ?? throw Mismatch(buildScreen, jump, "JMP");
    }

    // UA: Перевіряє pc240-246 у ДОВІЛЬНОМУ допустимому стані: pc244 — це JMP із
    //     зсувом 1 (вихідний файл) або 0 (після патча); решта інструкцій і
    //     константи незмінні.
    // EN: Verifies pc240-246 in ANY permitted state: pc244 is a JMP with the
    //     offset 1 (the original file) or 0 (after the patch); the other
    //     instructions and the constants are fixed.
    private static void VerifyBranch(LuaFunctionPrototype proto)
    {
        var defaultDivisor = RequirePc(proto, DefaultDivisorPc);
        if (defaultDivisor.Opcode != LuaOpcode.LoadK || defaultDivisor.A != RegisterDivisor ||
            !IsNumber(proto, defaultDivisor.Bx ?? -1, DefaultDivisor))
            throw Mismatch(proto, defaultDivisor, $"LOADK R{RegisterDivisor} := {DefaultDivisor}");

        var slotTest = RequirePc(proto, SlotCountTestPc);
        if (slotTest.Opcode != LuaOpcode.Lt || slotTest.A != 0 || slotTest.C != RegisterSlotCount ||
            slotTest.B is null || slotTest.B < 128 ||
            !IsNumber(proto, slotTest.B.Value - 128, SlotCountThreshold))
            throw Mismatch(proto, slotTest, $"LT A=0 B=<{SlotCountThreshold}> C={RegisterSlotCount}");

        var slotJump = RequirePc(proto, SlotCountJumpPc);
        RequireJumpTo(proto, slotJump, ReducedDivisorPc + 1);

        var heightTest = RequirePc(proto, HeightTestPc);
        if (heightTest.Opcode != LuaOpcode.Lt || heightTest.A != 0 || heightTest.B != RegisterScreenHeight ||
            heightTest.C is null || heightTest.C < 128 ||
            !IsNumber(proto, heightTest.C.Value - 128, ScreenHeightThreshold))
            throw Mismatch(proto, heightTest, $"LT A=0 B={RegisterScreenHeight} C=<{ScreenHeightThreshold}>");

        var heightJump = RequirePc(proto, HeightJumpPc);
        if (heightJump.Opcode != LuaOpcode.Jmp)
            throw Mismatch(proto, heightJump, "JMP");
        if (heightJump.SBx != OldHeightJumpOffset && heightJump.SBx != NewHeightJumpOffset)
            throw Mismatch(proto, heightJump, $"JMP {OldHeightJumpOffset} або/or {NewHeightJumpOffset}");

        var reducedDivisor = RequirePc(proto, ReducedDivisorPc);
        if (reducedDivisor.Opcode != LuaOpcode.LoadK || reducedDivisor.A != RegisterDivisor ||
            !IsNumber(proto, reducedDivisor.Bx ?? -1, ReducedDivisor))
            throw Mismatch(proto, reducedDivisor, $"LOADK R{RegisterDivisor} := {ReducedDivisor}");

        // UA: pc246 — перший виклик ScriptCB_GetFontHeight після вибору ділильника.
        // EN: pc246 — the first ScriptCB_GetFontHeight call after the divisor selection.
        var next = RequirePc(proto, ReducedDivisorPc + 1);
        var nextIndex = next.Bx ?? -1;
        if (next.Opcode != LuaOpcode.GetGlobal || nextIndex < 0 || nextIndex >= proto.Constants.Count ||
            proto.Constants[nextIndex].Kind != LuaConstantKind.String ||
            proto.Constants[nextIndex].StringValue != "ScriptCB_GetFontHeight")
            throw Mismatch(proto, next, "GETGLOBAL ScriptCB_GetFontHeight");
    }

    private static bool IsNumber(LuaFunctionPrototype proto, int index, float value)
    {
        if (index < 0 || index >= proto.Constants.Count)
            return false;
        var constant = proto.Constants[index];
        return constant.Kind == LuaConstantKind.Number && constant.NumberValue == value;
    }

    // UA: Перехід на pc<target>: pc + 1 + sBx == target.
    // EN: A jump to pc<target>: pc + 1 + sBx == target.
    private static void RequireJumpTo(LuaFunctionPrototype proto, LuaInstruction jump, int targetPc)
    {
        if (jump.Opcode != LuaOpcode.Jmp || jump.Pc + 1 + jump.SBx != targetPc)
            throw Mismatch(proto, jump, $"JMP -> pc {targetPc}");
    }

    // -------------------------------------------------------------------------
    // UA: Застосовує план до КОПІЇ байтів файлу. Перед кожним записом
    //     звіряє СТАРІ байти за зміщенням — той самий принцип, що й
    //     SpawnSelectVerticalLayoutPatchBuilder.Apply.
    // EN: Applies the plan to a COPY of the file bytes. Before each write it
    //     checks the OLD bytes at the offset — the same principle as
    //     SpawnSelectVerticalLayoutPatchBuilder.Apply.
    // -------------------------------------------------------------------------
    public static byte[] Apply(byte[] ingameLvlBytes, Plan plan)
    {
        var output = (byte[])ingameLvlBytes.Clone();

        foreach (var site in plan.Sites)
        {
            if (site.FileOffset < 0 || site.FileOffset + site.OldBytes.Length > output.LongLength)
                throw new InvalidDataException(
                    $"UA: {site.Label}: зміщення 0x{site.FileOffset:X} (довжина {site.OldBytes.Length}) поза " +
                    $"файлом (розмір {output.LongLength}). / " +
                    $"EN: {site.Label}: the offset 0x{site.FileOffset:X} (length {site.OldBytes.Length}) is " +
                    $"outside the file (size {output.LongLength}).");

            for (var i = 0; i < site.OldBytes.Length; i++)
            {
                if (output[site.FileOffset + i] != site.OldBytes[i])
                    throw new InvalidDataException(
                        $"UA: {site.Label}: за зміщенням 0x{site.FileOffset + i:X} очікувався байт " +
                        $"0x{site.OldBytes[i]:X2}, а лежить 0x{output[site.FileOffset + i]:X2}. / " +
                        $"EN: {site.Label}: at offset 0x{site.FileOffset + i:X} expected byte " +
                        $"0x{site.OldBytes[i]:X2}, found 0x{output[site.FileOffset + i]:X2}.");
            }

            Array.Copy(site.NewBytes, 0, output, (int)site.FileOffset, site.NewBytes.Length);
        }

        return output;
    }

    private static ScriptResource FindScript(UcfbChunk ingameLvlRoot)
    {
        var script = ScriptChunkLocator.FindAll(ingameLvlRoot)
            .FirstOrDefault(s => s.Name == ScreenName);
        if (script is null)
            throw new InvalidDataException(
                $"UA: Екран '{ScreenName}' не знайдено серед scr_-ресурсів ingame.lvl. / " +
                $"EN: Screen '{ScreenName}' was not found among ingame.lvl's scr_ resources.");
        return script;
    }

    // UA: Знаходить прототип `fnBuildScreen` не за голим індексом, а за
    //     вмістом його пулу констант — усі рядки-маркери мають бути
    //     присутні, і такий прототип має бути рівно один.
    // EN: Finds the `fnBuildScreen` prototype not by a bare index but by its
    //     constant pool's content — every marker string must be present, and
    //     there must be exactly one such prototype.
    private static LuaFunctionPrototype LocateSinglePrototype(LuaFunctionPrototype root)
    {
        string[] required = ["UnitCount", "NewPCIFButton", "ifs.SpawnDisplay.Spawn", "InfoText", "HScale", "VScale"];

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
                $"UA: '{ScreenName}': знайдено {candidates.Count} вкладених прототипів із маркерними " +
                $"рядками ({string.Join(", ", required)}), очікувався рівно 1. / " +
                $"EN: '{ScreenName}': found {candidates.Count} nested prototypes with the marker strings " +
                $"({string.Join(", ", required)}), expected exactly 1.");

        return candidates[0];
    }

    private static LuaInstruction RequirePc(LuaFunctionPrototype proto, int pc)
    {
        var instruction = proto.Instructions.FirstOrDefault(ins => ins.Pc == pc);
        if (instruction is null)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{proto.Path}: інструкції з pc={pc} не існує. / " +
                $"EN: '{ScreenName}'/{proto.Path}: no instruction with pc={pc} exists.");
        return instruction;
    }

    private static void RequireWordOffset(LuaInstruction instruction)
    {
        if (instruction.WordOffset < 0)
            throw new InvalidDataException(
                "UA: інструкція без дійсного зміщення (WordOffset<0) — внутрішня помилка парсера. / " +
                "EN: an instruction with no valid offset (WordOffset<0) — a parser-internal error.");
    }

    private static InvalidDataException Mismatch(LuaFunctionPrototype proto, LuaInstruction instruction,
        string expected) =>
        new(
            $"UA: '{ScreenName}'/{proto.Path} pc={instruction.Pc}: очікувалось {expected}, знайдено " +
            $"{instruction.Opcode} A={instruction.A} B={instruction.B} C={instruction.C} Bx={instruction.Bx}. " +
            "Файл відрізняється від проаналізованого — патч НЕ застосовується. / " +
            $"EN: '{ScreenName}'/{proto.Path} pc={instruction.Pc}: expected {expected}, found " +
            $"{instruction.Opcode} A={instruction.A} B={instruction.B} C={instruction.C} Bx={instruction.Bx}. " +
            "The file differs from the one analyzed — the patch is NOT applied.");

    // UA: Кодує iAsBx-слово JMP за тим самим бітовим розкладом, що й
    //     Lua50BytecodeReader.DecodeInstruction (opcode у бітах 0-5, Bx у
    //     бітах 6-23, A у бітах 24-31).
    // EN: Encodes a JMP iAsBx word using the same bit layout as
    //     Lua50BytecodeReader.DecodeInstruction (opcode in bits 0-5, Bx in
    //     bits 6-23, A in bits 24-31).
    private static byte[] EncodeJump(int a, int bx)
    {
        var word = ((uint)a << 24) | ((uint)bx << 6) | (uint)LuaOpcode.Jmp;
        return BitConverter.GetBytes(word);
    }
}
