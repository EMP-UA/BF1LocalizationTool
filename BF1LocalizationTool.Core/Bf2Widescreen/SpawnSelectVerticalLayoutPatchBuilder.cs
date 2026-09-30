// =============================================================================
// BF1LocalizationTool.Core — Bf2Widescreen/SpawnSelectVerticalLayoutPatchBuilder.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Положення кнопки "Спавн" на екрані вибору бійця (`ifs_pc_spawnselect`,
//     прототип `fnBuildScreen`) — разом із нею рухається напис
//     "Кількість бійців", бо його `y` рахується від `y` кнопки (див.
//     `SpawnSelectUnitCountGapPatchBuilder`).
//
//     pc130-131:
//         MUL R31 := K(0.1) * R3      (R3 — safeH з ScriptCB_GetSafeScreenInfo)
//         SUB R31 := R3 - R31         (y кнопки = 0.90×safeH)
//     Операнд B на pc130 перепризначається на вже наявну в пулі константу
//     0.05 (її читає і pc66 — значення константи НЕ змінюється, тож pc66 не
//     зачіпається): y кнопки = 0.95×safeH. Розмір BODY-чанка й файлу не
//     змінюється.
//
//     Виміряно на скріншотах гри 1920×1080: підпис кнопки y 922-937 ->
//     970-985 (+48 px); напис рухається на ту саму відстань, зазор між
//     ними не змінюється.
//
//     Положення моделі бійця цей крок не змінює: `fStartY`/`fEndY` у
//     `ifs_pc_spawnselect_animateicons` задають 2D-позицію об'єкта
//     (IFObj_fnSetPos), а видиме положення моделі — 3D-трансляція
//     NewIFModel (x/y/z -> ScriptCB_IFModel_SetTranslation, interface_util
//     у common.lvl); значення `fStartY` 0.17 і 0.10 дають на скріншотах
//     гри те саме положення моделі. Положення моделі визначає рушій.
//
// EN: Position of the "Спавн" button on the unit-selection screen
//     (`ifs_pc_spawnselect`, prototype `fnBuildScreen`) — the "Кількість
//     бійців" label moves with it, because its `y` is computed from the
//     button's `y` (see `SpawnSelectUnitCountGapPatchBuilder`).
//
//     pc130-131:
//         MUL R31 := K(0.1) * R3      (R3 — safeH from ScriptCB_GetSafeScreenInfo)
//         SUB R31 := R3 - R31         (button y = 0.90×safeH)
//     Operand B at pc130 is repointed to the 0.05 constant already in the
//     pool (pc66 also reads it — the constant's value is NOT changed, so
//     pc66 is unaffected): button y = 0.95×safeH. The BODY chunk's and the
//     file's size are unchanged.
//
//     Measured on 1920×1080 in-game screenshots: button label y
//     922-937 -> 970-985 (+48 px); the label moves by the same
//     distance, the gap between them is unchanged.
//
//     This step does not change the soldier model's position:
//     `fStartY`/`fEndY` in `ifs_pc_spawnselect_animateicons` set the
//     object's 2D position (IFObj_fnSetPos), while the model's visible
//     position is the NewIFModel 3D translation (x/y/z ->
//     ScriptCB_IFModel_SetTranslation, interface_util in common.lvl);
//     `fStartY` values 0.17 and 0.10 give the same model position in
//     in-game screenshots. The model position is controlled by the engine.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Core.Bf2Widescreen;

public static class SpawnSelectVerticalLayoutPatchBuilder
{
    // UA: Ім'я екрана (scr_/NAME) в ingame.lvl.
    // EN: The screen's name (scr_/NAME) in ingame.lvl.
    public const string ScreenName = "ifs_pc_spawnselect";

    // UA: Кнопка "Спавн": pc інструкції MUL, що обчислює нижній відступ
    //     кнопки як частку safeH, і регістри формули.
    // EN: The "Спавн" button: pc of the MUL computing the button's bottom
    //     margin as a fraction of safeH, and the formula's registers.
    public const int ButtonTargetPc = 130;
    public const int RegisterButtonY = 31; // R31 — y кнопки / the button's y
    public const int RegisterSafeHeight = 3; // R3 — safeH
    public const float OldButtonMarginFraction = 0.1f;
    public const float NewButtonMarginFraction = 0.05f;

    public sealed record PatchSite(
        string Label,
        long FileOffset,
        byte[] OldBytes,
        byte[] NewBytes);

    public sealed record Plan(
        IReadOnlyList<PatchSite> Sites,
        string ButtonPrototypePath);

    // -------------------------------------------------------------------------
    // UA: Знаходить `ifs_pc_spawnselect`, розбирає BODY, перевіряє цільові
    //     інструкції кнопки (опкод, регістри, числові значення констант) і
    //     повертає план. Кидає виняток на будь-яку невідповідність.
    // EN: Finds `ifs_pc_spawnselect`, parses the BODY, verifies the
    //     button's target instructions (opcode, registers, constant values)
    //     and returns the plan. Throws on any mismatch.
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

        var buildScreen = LocateSinglePrototype(parsed.Root,
            ["UnitCount", "NewPCIFButton", "ifs.SpawnDisplay.Spawn"]);

        var sites = new List<PatchSite>
        {
            BuildButtonSite(script, buildScreen),
        };

        return new Plan(sites, buildScreen.Path);
    }

    // UA: pc130 `MUL R31 := K(0.1) * R3` -> `MUL R31 := K(0.05) * R3`;
    //     pc131 має бути `SUB R31 := R3 - R31`.
    // EN: pc130 `MUL R31 := K(0.1) * R3` -> `MUL R31 := K(0.05) * R3`;
    //     pc131 must be `SUB R31 := R3 - R31`.
    private static PatchSite BuildButtonSite(ScriptResource script, LuaFunctionPrototype buildScreen)
    {
        var mul = RequirePc(buildScreen, ButtonTargetPc);
        if (mul.Opcode != LuaOpcode.Mul || mul.A != RegisterButtonY || mul.C != RegisterSafeHeight ||
            mul.B is null || mul.B < 128)
            throw Mismatch(buildScreen, mul,
                $"MUL A={RegisterButtonY} B=<константа/constant> C={RegisterSafeHeight}");

        var oldConstant = ConstantAt(buildScreen, mul.B.Value - 128, mul.Pc);
        if (oldConstant.Kind != LuaConstantKind.Number || oldConstant.NumberValue != OldButtonMarginFraction)
            throw Mismatch(buildScreen, mul, $"B = {OldButtonMarginFraction}");

        var sub = RequirePc(buildScreen, ButtonTargetPc + 1);
        if (sub.Opcode != LuaOpcode.Sub || sub.A != RegisterButtonY || sub.B != RegisterSafeHeight ||
            sub.C != RegisterButtonY)
            throw Mismatch(buildScreen, sub, $"SUB A={RegisterButtonY} B={RegisterSafeHeight} C={RegisterButtonY}");

        var newIndexes = buildScreen.Constants
            .Select((k, i) => (k, i))
            .Where(t => t.k.Kind == LuaConstantKind.Number && t.k.NumberValue == NewButtonMarginFraction)
            .Select(t => t.i)
            .ToList();
        if (newIndexes.Count != 1)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{buildScreen.Path}: константа {NewButtonMarginFraction} має бути в пулі " +
                $"рівно один раз, знайдено {newIndexes.Count} — патч НЕ застосовується. / " +
                $"EN: '{ScreenName}'/{buildScreen.Path}: the constant {NewButtonMarginFraction} must be in the " +
                $"pool exactly once, found {newIndexes.Count} — the patch is NOT applied.");

        var oldB = mul.B.Value;
        var newB = 128 + newIndexes[0];
        RequireWordOffset(mul);

        return new PatchSite(
            $"{buildScreen.Path} pc={ButtonTargetPc}",
            script.BodyChunk.FileDataOffset + mul.WordOffset,
            EncodeAbc(LuaOpcode.Mul, RegisterButtonY, oldB, RegisterSafeHeight),
            EncodeAbc(LuaOpcode.Mul, RegisterButtonY, newB, RegisterSafeHeight));
    }

    // -------------------------------------------------------------------------
    // UA: Перевірка ПІСЛЯ запису: заново розбирає файл і повертає поточну
    //     частку safeH для кнопки (константа за операндом B на pc130).
    // EN: POST-write verification: re-parses the file and returns the
    //     button's current safeH fraction (the constant behind operand B at
    //     pc130).
    // -------------------------------------------------------------------------
    public static float ReadCurrentButtonMarginFraction(UcfbChunk ingameLvlRoot)
    {
        var script = FindScript(ingameLvlRoot);
        var parsed = Lua50BytecodeReader.Parse(script.BodyChunk.RawData);

        var buildScreen = LocateSinglePrototype(parsed.Root,
            ["UnitCount", "NewPCIFButton", "ifs.SpawnDisplay.Spawn"]);

        var mulButton = RequirePc(buildScreen, ButtonTargetPc);
        if (mulButton.B is null)
            throw new InvalidDataException(
                "UA: pc130 без операнда-константи при повторній перевірці. / " +
                "EN: pc130 has no constant operand on re-verification.");

        return ConstantAt(buildScreen, mulButton.B.Value - 128, mulButton.Pc).NumberValue;
    }

    // -------------------------------------------------------------------------
    // UA: Застосовує план до КОПІЇ байтів файлу. Перед кожним записом
    //     звіряє СТАРІ байти за зміщенням — той самий принцип, що й
    //     SpawnSelectListTopOffsetPatchBuilder.Apply.
    // EN: Applies the plan to a COPY of the file bytes. Before each write it
    //     checks the OLD bytes at the offset — the same principle as
    //     SpawnSelectListTopOffsetPatchBuilder.Apply.
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

    // UA: Знаходить вкладений прототип не за голим індексом, а за вмістом
    //     його пулу констант — усі рядки-маркери мають бути присутні, і
    //     такий прототип має бути рівно один.
    // EN: Finds a nested prototype not by a bare index but by its constant
    //     pool's content — every marker string must be present, and there
    //     must be exactly one such prototype.
    private static LuaFunctionPrototype LocateSinglePrototype(LuaFunctionPrototype root, string[] required)
    {
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

    private static LuaConstant ConstantAt(LuaFunctionPrototype proto, int index, int pc)
    {
        if (index < 0 || index >= proto.Constants.Count)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{proto.Path} pc={pc}: індекс константи {index} поза межами пулу. / " +
                $"EN: '{ScreenName}'/{proto.Path} pc={pc}: constant index {index} is out of the pool's range.");
        return proto.Constants[index];
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
            $"{instruction.Opcode} A={instruction.A} B={instruction.B} C={instruction.C}. Файл відрізняється " +
            "від проаналізованого — патч НЕ застосовується. / " +
            $"EN: '{ScreenName}'/{proto.Path} pc={instruction.Pc}: expected {expected}, found " +
            $"{instruction.Opcode} A={instruction.A} B={instruction.B} C={instruction.C}. The file differs " +
            "from the one analyzed — the patch is NOT applied.");

    // UA: Кодує iABC-слово за тим самим бітовим розкладом, що й
    //     Lua50BytecodeReader.DecodeInstruction (opcode у бітах 0-5, C у
    //     бітах 6-14, B у бітах 15-23, A у бітах 24-31).
    // EN: Encodes an iABC word using the same bit layout as
    //     Lua50BytecodeReader.DecodeInstruction (opcode in bits 0-5, C in
    //     bits 6-14, B in bits 15-23, A in bits 24-31).
    private static byte[] EncodeAbc(LuaOpcode opcode, int a, int b, int c)
    {
        var word = ((uint)a << 24) | ((uint)b << 15) | ((uint)c << 6) | (uint)opcode;
        return BitConverter.GetBytes(word);
    }
}
