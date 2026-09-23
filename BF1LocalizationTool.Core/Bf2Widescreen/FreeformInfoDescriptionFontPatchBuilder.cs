// =============================================================================
// BF1LocalizationTool.Core — Bf2Widescreen/FreeformInfoDescriptionFontPatchBuilder.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: СТАТУС: НЕ ПРОТЕСТОВАНО (потрібен скріншот 2-3 екранів
//     Галактичного завоювання: чи текст справді більший і влазить без
//     переносу/обрізання — див. "ЯК ПЕРЕВІРЯТИ" в
//     GenerateFreeformInfoDescriptionFontFixCommand.cs) —
//     на екранах "Галактичного завоювання" (найм бійців/техніки, бонуси,
//     переміщення флоту тощо) опис у нижньому інформаційному блоці
//     ("info"-панель) виводиться шрифтом `gamefont_tiny`, який на тлі решти
//     вже збільшеної кирилиці має помітно менший видимий розмір — тоді як у самій панелі
//     є вільний вертикальний простір (висота панелі НЕ залежить від висоти
//     цього конкретного тексту, доведено нижче).
//
//     СПІЛЬНА ФУНКЦІЯ: увесь "info"-блок (заголовок/caption, підзаголовок/
//     subcaption, опис/text) будує ОДНА функція — `ifs_freeform_AddCommonElements`
//     (вкладений прототип `root/42` скрипта `ifs_freeform_main` у shell.lvl).
//     Цю функцію використовують ДЕСЯТКИ екранів Галактичного завоювання
//     (`ifs_freeform_purchase_unit`, `ifs_freeform_purchase_tech`,
//     `ifs_freeform_fleet`, `ifs_freeform_focus`, `ifs_freeform_battle`,
//     `ifs_freeform_battle_mode`, `ifs_freeform_battle_card`,
//     `ifs_freeform_result`, `ifs_freeform_summary`, `ifs_campaign_battle`,
//     `ifs_campaign_battle_card`, `ifs_campaign_summary` та інші) — тому ОДНА
//     ця зміна виправляє їх УСІ одночасно (одна справжня зміна виправляє
//     кілька екранів одночасно, замість окремого фіксу для кожного).
//
//     ТОЧНА ІНСТРУКЦІЯ (підтверджено прямим дизасемблюванням vanilla
//     shell.lvl і ПОБАЙТОВО звірено з реальним поточним локалізованим
//     reference-files\BF2-UA-rem\shell.lvl — слово інструкції ідентичне в
//     обох файлах, 0x154BB9C9, лише абсолютне зміщення файлу різне через
//     ріст таблиці Locl-рядків):
//       pc299: SETTABLE R21[K23('font')] := K103('gamefont_tiny')   (A=21 B=151 C=231)
//     Безпосередньо ВИЩЕ в тій самій функції (pc245 і pc272) той самий вираз
//     `R21[K23('font')] := ...` для caption/subcaption ВЖЕ використовує
//     `K90('gamefont_small')` (RK=218) — тобто константа-ціль ВЖЕ існує в
//     пулі констант цього прототипу, нову створювати не потрібно.
//
//     ФІКС: перепризначити ЛИШЕ операнд C цієї ОДНІЄЇ інструкції — з RK-коду
//     константи 'gamefont_tiny' (231) на RK-код константи 'gamefont_small'
//     (218). Операнди A(=21, регістр таблиці полів) і B(=151, RK 'font') НЕ
//     змінюються. Розмір слова інструкції (4 байти), розмір BODY-чанка,
//     розмір усього файлу, номери всіх інших інструкцій — УСЕ лишається
//     побайтово незмінним: це патч ОДНОГО 4-байтного слова на місці, той
//     самий принцип, що й FontHeadHeightFix (1 байт на шрифт) —
//     ТІЛЬКИ тут одиниця патчу довша (ціле слово інструкції, а не байт
//     константи), бо ціль — не значення константи, а ЯКА константа
//     використовується.
//
//     ІНДЕКСИ КОНСТАНТ ("font"=K23, "gamefont_tiny"=K103, "gamefont_small"=K90)
//     НЕ хардкодяться як сирі числа — вони знаходяться ДИНАМІЧНО за
//     СТРІНГОВИМ значенням у пулі констант цього ж прототипу під час
//     виконання (`FindStringConstantIndex`). Це робить патч стійким до
//     майбутнього перекомпілювання скрипта, яке могло б переставити пул
//     констант в іншому порядку.
//
//     ЧОМУ Є МІСЦЕ ДЛЯ ЗБІЛЬШЕННЯ (перевірено дизасемблюванням pc212-224
//     тієї ж функції): висота всієї "info"-панелі обчислюється як
//     `height := (R15 + R16 + 5.0) + 32.0`, де R15/R16 — висота
//     caption/subcaption-блоків. Ці регістри НЕ залежать від таблиці R21,
//     яку інструкція pc299 заповнює для тексту ОПИСУ (`text`-поле панелі,
//     що зберігається окремо на pc317: `R19[K18('text')] := R20`). Тобто
//     збільшення шрифту тексту опису НЕ впливає на обчислену висоту рамки
//     панелі — рамка вже має запас (видно на скріншотах).
//
//     `gamefont_tiny` (нині 20px, було ванільних 13px) і `gamefont_small`
//     (нині 26px, було ванільних 17px) в реальному локалізованому
//     reference-files\BF2-UA-rem\core.lvl — ОБИДВА вже збільшені механізмом
//     FontHeadHeightFix. Отже переведення тексту опису на `gamefont_small`
//     — це ЗБІЛЬШЕННЯ в межах уже застосованої філософії проєкту (кирилиця
//     стає БІЛЬШОЮ), а не введення нового, ще не збільшеного шрифту.
//
//     ЩО ЦЕ НЕ ПІДТВЕРДЖУЄ: чи `gamefont_small` візуально влазить у наявний
//     простір на ВСІХ перелічених вище екранах без переносу рядків чи
//     обрізання — НЕ перевірено. Це гіпотеза, що вимагає реального
//     скріншот-тесту.
//
// EN: CANDIDATE FIX (NOT YET CONFIRMED BY A REAL IN-GAME TEST) — on the
//     "Galactic Conquest" screens (hiring units/tech, bonuses, fleet moves,
//     etc.) the description in the lower info panel is rendered in
//     `gamefont_tiny`, which has a noticeably smaller visible size than the rest of the already
//     enlarged Cyrillic — while the panel itself has spare vertical room
//     (the panel's height does NOT depend on this particular text's
//     height, proven below).
//
//     SHARED FUNCTION: the whole "info" block (caption, subcaption,
//     description/text) is built by ONE function —
//     `ifs_freeform_AddCommonElements` (nested prototype `root/42` of the
//     `ifs_freeform_main` script in shell.lvl). This function is reused by
//     DOZENS of Galactic Conquest screens (`ifs_freeform_purchase_unit`,
//     `ifs_freeform_purchase_tech`, `ifs_freeform_fleet`,
//     `ifs_freeform_focus`, `ifs_freeform_battle`, `ifs_freeform_battle_mode`,
//     `ifs_freeform_battle_card`, `ifs_freeform_result`,
//     `ifs_freeform_summary`, `ifs_campaign_battle`,
//     `ifs_campaign_battle_card`, `ifs_campaign_summary` and others) — so
//     this ONE change fixes ALL of them at once (one genuine change
//     fixes multiple screens at once, instead of a separate fix per
//     screen).
//
//     THE EXACT INSTRUCTION (confirmed by directly disassembling the
//     vanilla shell.lvl and cross-checked BYTE-FOR-BYTE against the actual,
//     currently localized reference-files\BF2-UA-rem\shell.lvl — the
//     instruction word is identical in both files, 0x154BB9C9, only the
//     file's absolute offset differs because the Locl string table grew):
//       pc299: SETTABLE R21[K23('font')] := K103('gamefont_tiny')   (A=21 B=151 C=231)
//     Immediately ABOVE, in the same function (pc245 and pc272), the same
//     `R21[K23('font')] := ...` expression for the caption/subcaption
//     ALREADY uses `K90('gamefont_small')` (RK=218) — i.e. the target
//     constant ALREADY exists in this prototype's constant pool, no new
//     one needs to be created.
//
//     THE FIX: repoint ONLY the C operand of this ONE instruction — from
//     the RK code of the 'gamefont_tiny' constant (231) to the RK code of
//     the 'gamefont_small' constant (218). Operands A(=21, the field
//     table's register) and B(=151, RK for 'font') are NOT changed. The
//     instruction word's size (4 bytes), the BODY chunk's size, the whole
//     file's size, and every other instruction's number all stay
//     byte-for-byte unchanged: this is an in-place patch of ONE 4-byte
//     word, the same principle as FontHeadHeightFix (1 byte per font) —
//     only here the patched unit is longer (a whole instruction word, not
//     a constant's value byte), because the target isn't a constant's
//     VALUE but WHICH constant is referenced.
//
//     THE CONSTANT INDICES ("font"=K23, "gamefont_tiny"=K103,
//     "gamefont_small"=K90) are NOT hardcoded as raw numbers — they are
//     found DYNAMICALLY by STRING VALUE in this same prototype's constant
//     pool at runtime (`FindStringConstantIndex`). This makes the patch
//     resilient to a future recompile that might reorder the constant pool.
//
//     WHY THERE IS ROOM TO ENLARGE (confirmed by disassembling pc212-224 of
//     the same function): the whole "info" panel's height is computed as
//     `height := (R15 + R16 + 5.0) + 32.0`, where R15/R16 are the
//     caption/subcaption blocks' heights. These registers do NOT depend on
//     the R21 table that instruction pc299 fills in for the DESCRIPTION
//     text (the panel's `text` field, stored separately at pc317:
//     `R19[K18('text')] := R20`). So enlarging the description text's font
//     does NOT affect the panel frame's computed height — the frame
//     already has spare room (visible in the screenshots).
//
//     `gamefont_tiny` (currently 20px, was 13px vanilla) and
//     `gamefont_small` (currently 26px, was 17px vanilla) in the real
//     localized reference-files\BF2-UA-rem\core.lvl are BOTH already
//     enlarged by the FontHeadHeightFix mechanism. So promoting the
//     description text to `gamefont_small` is an ENLARGEMENT within the
//     project's already-applied philosophy (Cyrillic gets BIGGER), not the
//     introduction of a new, not-yet-enlarged font.
//
//     WHAT THIS DOES NOT CONFIRM: whether `gamefont_small` visually fits
//     the available space on ALL the screens listed above without line
//     wrapping or clipping — NOT checked. This is a hypothesis that needs
//     a real screenshot test.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Core.Bf2Widescreen;

public static class FreeformInfoDescriptionFontPatchBuilder
{
    // UA: Ім'я екрана (scr_/NAME) в shell.lvl.
    // EN: The screen's name (scr_/NAME) in shell.lvl.
    public const string ScreenName = "ifs_freeform_main";

    // UA: Рядкові маркери (мають бути ВСІ присутні в пулі констант
    //     прототипу), якими однозначно ідентифікується саме
    //     `ifs_freeform_AddCommonElements` серед усіх вкладених прототипів
    //     скрипта — без прив'язки до сирого числового індексу прототипу
    //     (`root/42`), який міг би змінитися при перекомпіляції.
    // EN: String markers (ALL must be present in the prototype's constant
    //     pool) that uniquely identify `ifs_freeform_AddCommonElements`
    //     among all the script's nested prototypes — without relying on
    //     the raw numeric prototype index (`root/42`), which could change
    //     on recompilation.
    private static readonly string[] RequiredMarkers =
        ["font", "gamefont_tiny", "gamefont_small", "caption", "subcaption"];

    private const string FontFieldName = "font";
    private const string OldFontConstantName = "gamefont_tiny";
    private const string NewFontConstantName = "gamefont_small";

    public sealed record Plan(
        long FileOffset,
        int Pc,
        byte[] OldWord,
        byte[] NewWord,
        string PrototypePath);

    // -------------------------------------------------------------------------
    // UA: Знаходить `ifs_freeform_main`, розбирає його BODY через
    //     Lua50BytecodeReader, знаходить прототип `ifs_freeform_AddCommonElements`
    //     за маркерними константами, динамічно резолвить індекси констант
    //     "font"/"gamefont_tiny"/"gamefont_small", знаходить РІВНО одну
    //     інструкцію SETTABLE, що встановлює font:=gamefont_tiny, і
    //     повертає план патчу. Кидає виняток на БУДЬ-яку невідповідність
    //     очікуваній структурі.
    // EN: Finds `ifs_freeform_main`, parses its BODY via
    //     Lua50BytecodeReader, locates the `ifs_freeform_AddCommonElements`
    //     prototype via marker constants, dynamically resolves the
    //     "font"/"gamefont_tiny"/"gamefont_small" constant indices, finds
    //     EXACTLY one SETTABLE instruction setting font:=gamefont_tiny, and
    //     returns the patch plan. Throws on ANY mismatch with the expected
    //     structure.
    // -------------------------------------------------------------------------
    public static Plan BuildPlan(UcfbChunk shellLvlRoot)
    {
        var script = ScriptChunkLocator.FindAll(shellLvlRoot)
            .FirstOrDefault(s => s.Name == ScreenName);
        if (script is null)
            throw new InvalidDataException(
                $"UA: Скрипт '{ScreenName}' не знайдено серед scr_-ресурсів shell.lvl. / " +
                $"EN: Script '{ScreenName}' was not found among shell.lvl's scr_ resources.");

        var parsed = Lua50BytecodeReader.Parse(script.BodyChunk.RawData);
        if (parsed.LeftoverBytes != 1)
            throw new InvalidDataException(
                $"UA: '{ScreenName}': після розбору лишилось {parsed.LeftoverBytes} байт (очікувався 1) — " +
                "формат BODY інший, ніж підтверджений. / " +
                $"EN: '{ScreenName}': {parsed.LeftoverBytes} bytes left over after parsing (expected 1) — " +
                "the BODY format differs from the confirmed one.");

        var addCommonElements = LocateAddCommonElementsPrototype(parsed.Root, ScreenName);

        var fontIdx = FindStringConstantIndex(addCommonElements, FontFieldName, ScreenName, addCommonElements.Path);
        var tinyIdx = FindStringConstantIndex(addCommonElements, OldFontConstantName, ScreenName, addCommonElements.Path);
        var smallIdx = FindStringConstantIndex(addCommonElements, NewFontConstantName, ScreenName, addCommonElements.Path);

        var rkFont = fontIdx + Lua50BytecodeReader.MaxStack;
        var rkTiny = tinyIdx + Lua50BytecodeReader.MaxStack;
        var rkSmall = smallIdx + Lua50BytecodeReader.MaxStack;

        var matches = addCommonElements.Instructions
            .Where(i => i.Opcode == LuaOpcode.SetTable && i.B == rkFont && i.C == rkTiny)
            .ToList();
        if (matches.Count != 1)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{addCommonElements.Path}: знайдено {matches.Count} інструкцій " +
                $"SETTABLE font:=gamefont_tiny, очікувалась рівно 1. / " +
                $"EN: '{ScreenName}'/{addCommonElements.Path}: found {matches.Count} SETTABLE " +
                "font:=gamefont_tiny instructions, expected exactly 1.");

        var target = matches[0];
        if (target.WordOffset < 0)
            throw new InvalidDataException(
                "UA: інструкція без дійсного зміщення (WordOffset<0) — внутрішня помилка парсера. / " +
                "EN: an instruction with no valid offset (WordOffset<0) — a parser-internal error.");

        var oldWord = EncodeAbc(LuaOpcode.SetTable, target.A, rkFont, rkTiny);
        var newWord = EncodeAbc(LuaOpcode.SetTable, target.A, rkFont, rkSmall);

        var fileOffset = script.BodyChunk.FileDataOffset + target.WordOffset;

        return new Plan(fileOffset, target.Pc, oldWord, newWord, addCommonElements.Path);
    }

    // -------------------------------------------------------------------------
    // UA: Перевірка ПІСЛЯ запису: заново знаходить прототип і повертає
    //     інструкцію на записаному pc — щоб підтвердити, що C тепер
    //     справді вказує на 'gamefont_small', а не просто "запис не впав
    //     з винятком".
    // EN: POST-write verification: re-locates the prototype and returns
    //     the instruction at the recorded pc — to confirm C now really
    //     points at 'gamefont_small', not merely "the write didn't throw".
    // -------------------------------------------------------------------------
    public static LuaInstruction ReadCurrentPatch(UcfbChunk shellLvlRoot, int pc)
    {
        var script = ScriptChunkLocator.FindAll(shellLvlRoot)
            .FirstOrDefault(s => s.Name == ScreenName);
        if (script is null)
            throw new InvalidDataException(
                $"UA: Скрипт '{ScreenName}' не знайдено серед scr_-ресурсів shell.lvl. / " +
                $"EN: Script '{ScreenName}' was not found among shell.lvl's scr_ resources.");

        var parsed = Lua50BytecodeReader.Parse(script.BodyChunk.RawData);
        var addCommonElements = LocateAddCommonElementsPrototype(parsed.Root, ScreenName);

        var instruction = addCommonElements.Instructions.FirstOrDefault(i => i.Pc == pc);
        if (instruction is null)
            throw new InvalidDataException(
                $"UA: '{ScreenName}'/{addCommonElements.Path}: інструкції з pc={pc} не існує при " +
                "повторній перевірці. / " +
                $"EN: '{ScreenName}'/{addCommonElements.Path}: no instruction with pc={pc} exists on " +
                "re-verification.");

        return instruction;
    }

    // -------------------------------------------------------------------------
    // UA: Знаходить прототип "AddCommonElements" не за голим числовим
    //     індексом (`root/42`), а за вмістом його пулу констант (усі
    //     маркери мають бути присутні одночасно), і вимагає РІВНО одного
    //     кандидата.
    // EN: Finds the "AddCommonElements" prototype not by a bare numeric
    //     index (`root/42`), but by its constant pool's content (all
    //     markers must be present at once), and requires EXACTLY one
    //     candidate.
    // -------------------------------------------------------------------------
    private static LuaFunctionPrototype LocateAddCommonElementsPrototype(LuaFunctionPrototype root, string screenName)
    {
        bool HasAllMarkers(LuaFunctionPrototype proto)
        {
            var strings = proto.Constants
                .Where(k => k.Kind == LuaConstantKind.String)
                .Select(k => k.StringValue)
                .ToHashSet(StringComparer.Ordinal);
            return RequiredMarkers.All(strings.Contains);
        }

        var candidates = AllPrototypes(root).Where(HasAllMarkers).ToList();
        if (candidates.Count != 1)
            throw new InvalidDataException(
                $"UA: '{screenName}': знайдено {candidates.Count} вкладених прототипів із маркерними " +
                $"рядками ({string.Join(", ", RequiredMarkers)}), очікувався рівно 1. / " +
                $"EN: '{screenName}': found {candidates.Count} nested prototypes with the marker strings " +
                $"({string.Join(", ", RequiredMarkers)}), expected exactly 1.");

        return candidates[0];
    }

    // UA: Рекурсивно збирає прототип + УСІ вкладені прототипи (на відміну
    //     від Task A, ціль тут — не безпосередній нащадок кореня, тож
    //     обхід має бути рекурсивним, а не лише по root.NestedPrototypes).
    // EN: Recursively collects a prototype + ALL nested prototypes (unlike
    //     Task A, the target here isn't a direct child of the root, so the
    //     walk must be recursive, not just root.NestedPrototypes).
    private static IEnumerable<LuaFunctionPrototype> AllPrototypes(LuaFunctionPrototype proto)
    {
        yield return proto;
        foreach (var nested in proto.NestedPrototypes)
        foreach (var p in AllPrototypes(nested))
            yield return p;
    }

    // -------------------------------------------------------------------------
    // UA: Знаходить індекс РІВНО ОДНІЄЇ рядкової константи з заданим
    //     значенням у пулі констант прототипу. Кидає виняток, якщо рядка
    //     немає взагалі, або якщо він трапляється більше одного разу
    //     (неоднозначність — краще впасти з чіткою помилкою, ніж вгадувати,
    //     який саме індекс мався на увазі).
    // EN: Finds the index of EXACTLY ONE string constant with the given
    //     value in the prototype's constant pool. Throws if the string is
    //     missing entirely, or if it occurs more than once (ambiguity —
    //     better to fail loudly than to guess which index was meant).
    // -------------------------------------------------------------------------
    private static int FindStringConstantIndex(LuaFunctionPrototype proto, string value, string screenName, string protoPath)
    {
        var matches = new List<int>();
        for (var i = 0; i < proto.Constants.Count; i++)
        {
            if (proto.Constants[i].Kind == LuaConstantKind.String &&
                proto.Constants[i].StringValue == value)
                matches.Add(i);
        }

        if (matches.Count != 1)
            throw new InvalidDataException(
                $"UA: '{screenName}'/{protoPath}: рядкова константа '{value}' трапляється " +
                $"{matches.Count} раз(ів) у пулі констант, очікувався рівно 1. / " +
                $"EN: '{screenName}'/{protoPath}: string constant '{value}' occurs {matches.Count} " +
                "time(s) in the constant pool, expected exactly 1.");

        return matches[0];
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
    // UA: Застосовує план до КОПІЇ байтів файлу. Перед записом звіряє
    //     СТАРІ 4 байти за зміщенням — інакше зміщення вказує не туди (той
    //     самий принцип, що й SpawnSelectUnitCountGapPatchBuilder.Apply /
    //     FontHeadHeightFix.Apply).
    // EN: Applies the plan to a COPY of the file bytes. Before writing, it
    //     checks the OLD 4 bytes at the offset — otherwise the offset
    //     points somewhere else (same principle as
    //     SpawnSelectUnitCountGapPatchBuilder.Apply / FontHeadHeightFix.Apply).
    // -------------------------------------------------------------------------
    public static byte[] Apply(byte[] shellLvlBytes, Plan plan)
    {
        var output = (byte[])shellLvlBytes.Clone();

        if (plan.FileOffset < 0 || plan.FileOffset + plan.OldWord.Length > output.LongLength)
            throw new InvalidDataException(
                $"UA: Слово за зміщенням 0x{plan.FileOffset:X} (довжина {plan.OldWord.Length}) поза файлом " +
                $"(розмір {output.LongLength}). / " +
                $"EN: The word at offset 0x{plan.FileOffset:X} (length {plan.OldWord.Length}) is outside " +
                $"the file (size {output.LongLength}).");

        for (var i = 0; i < plan.OldWord.Length; i++)
        {
            if (output[plan.FileOffset + i] != plan.OldWord[i])
                throw new InvalidDataException(
                    $"UA: За зміщенням 0x{plan.FileOffset + i:X} очікувався байт 0x{plan.OldWord[i]:X2}, а " +
                    $"лежить 0x{output[plan.FileOffset + i]:X2}. / " +
                    $"EN: At offset 0x{plan.FileOffset + i:X} expected byte 0x{plan.OldWord[i]:X2}, found " +
                    $"0x{output[plan.FileOffset + i]:X2}.");
        }

        Array.Copy(plan.NewWord, 0, output, (int)plan.FileOffset, plan.NewWord.Length);

        return output;
    }
}
