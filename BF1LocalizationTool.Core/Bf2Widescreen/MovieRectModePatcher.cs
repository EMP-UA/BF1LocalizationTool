// =============================================================================
// BF1LocalizationTool.Core — Bf2Widescreen/MovieRectModePatcher.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: Перемикає екран з відеороликом на ВЛАСНИЙ широкоформатний режим гри —
//     змінюючи РІВНО ОДИН операнд однієї Lua-інструкції. Жодного патчу
//     BattlefrontII.exe, жодного власного оверлея, жодних вгаданих таймінгів.
//
//     ЩО ЦЕ ЗА РЕЖИМ. Уся геометрія відео в BF2 рахується в Lua, а не в
//     рушії. Центральна обгортка — ifelem_shellscreen_fnStartMovie
//     (common.lvl, скрипт "ifelem_shellscreen"), 8 параметрів:
//
//         fnStartMovie(name, p1, p2, mode, x, y, w, h)
//
//     Її 4-й параметр (mode) керує тим, ЯК рахується прямокутник відео перед
//     фінальним нативним викликом ScriptCB_PlayMovie(name, p1, p2, x, y, w, h):
//
//       mode == nil  — обчислення пропускається, у рушій ідуть x/y/w/h, які
//                      передав викликач (саме так робить ванільний
//                      ifs_tutorials і ifs_missionselect — 8-аргументна форма);
//       mode == 2.0  — прямокутник коригується за 4-м значенням
//                      ScriptCB_GetScreenInfo() ("widescreen"):
//                          x = W*(1 - 1/ws)*0.5     w = W/ws
//                          y = H*(1 - 1/ws)*0.5     h = H/ws
//       будь-яке інше істинне значення (у ванілі — 1.0) — прямокутник
//                      розтягується на ВЕСЬ екран: x=0, y=0, w=W, h=H.
//
//     ПРОБЛЕМА. Екрани кампанійських роликів (ifs_campaign_battle_intro,
//     ifs_campaign_turn_intro) передають mode = 1.0, тобто розтягують
//     4:3-відео на весь широкий екран. Ванільні ж навчальні ролики
//     (ifs_tutorials) натомість застосовують ту саму widescreen-корекцію
//     через ScriptCB_GetScreenInfo()[4] ~= 1.0 — тобто ГРА ВЖЕ ВМІЄ
//     коригувати геометрію відео під широкий екран, просто на цих екранах
//     цим не користується.
//
//     ЩО РОБИТЬ ЦЕЙ ПАТЧ. Знаходить у вказаному екрані виклик
//     fnStartMovie з 4 аргументами, знаходить інструкцію LOADK, що
//     завантажує mode, і переставляє її операнд Bx з константи 1.0 на
//     константу 2.0. Кількість інструкцій НЕ змінюється, тому перерахунок
//     відносних переходів не потрібен взагалі; константа 2.0 у цих екранах
//     вже є в пулі (використовується як transition[2]), а якщо раптом
//     немає — додається в кінець (індекси наявних констант не зсуваються).
//
//     БЕЗПЕКА НА 4:3. При ws == 1.0 формула режиму 2 дає рівно x=0, y=0,
//     w=W, h=H — тобто побайтово ту саму поведінку, що й режим 1. Отже на
//     4:3/5:4 патч не змінює НІЧОГО за побудовою, а не "за перевіркою".
//     Підтверджено прогоном реального байткоду у Lua-VM: 800x600 і
//     1280x1024 дають ідентичний прямокутник до і після патча.
//
//     ЧОГО ЦЕЙ ПАТЧ НЕ ОБІЦЯЄ. Він НЕ є доведеним виправленням субтитрів.
//     Виміряно (10 знімків, 10 роздільностей), що підпис ролика малюється
//     лише при аспекті <= 4:3, причому позиція рахується коректно від
//     реальної висоти екрана — тобто це не зсув за екран, а вимкнення
//     промальовки десь у нативному коді. Чи прив'язане це рішення саме до
//     прямокутника відео (тоді патч допоможе) чи до аспекту екрана (тоді
//     ні) — з файлів гри невідомо. Цей патч — найдешевша можлива перевірка
//     цієї розвилки: один операнд проти повного оверлея з таймерами.
//     Жодних заяв "виправлено" без знімка з гри.
// EN: Switches a movie screen to the game's OWN widescreen mode by changing
//     EXACTLY ONE operand of one Lua instruction. No BattlefrontII.exe
//     patch, no custom overlay, no guessed timings.
//
//     WHAT THAT MODE IS. All movie geometry in BF2 is computed in Lua, not
//     in the engine. The central wrapper is
//     ifelem_shellscreen_fnStartMovie (common.lvl, script
//     "ifelem_shellscreen"), 8 parameters:
//
//         fnStartMovie(name, p1, p2, mode, x, y, w, h)
//
//     Its 4th parameter (mode) decides HOW the movie rectangle is computed
//     before the final native ScriptCB_PlayMovie(name, p1, p2, x, y, w, h):
//
//       mode == nil  — computation is skipped and the caller's own x/y/w/h
//                      go straight to the engine (exactly what vanilla
//                      ifs_tutorials and ifs_missionselect do via the
//                      8-argument form);
//       mode == 2.0  — the rect is corrected by the 4th value of
//                      ScriptCB_GetScreenInfo() ("widescreen"):
//                          x = W*(1 - 1/ws)*0.5     w = W/ws
//                          y = H*(1 - 1/ws)*0.5     h = H/ws
//       any other truthy value (1.0 in vanilla) — the rect is stretched
//                      across the WHOLE screen: x=0, y=0, w=W, h=H.
//
//     THE PROBLEM. The campaign movie screens (ifs_campaign_battle_intro,
//     ifs_campaign_turn_intro) pass mode = 1.0, i.e. they stretch a 4:3
//     movie across the full widescreen. Vanilla tutorial movies
//     (ifs_tutorials), by contrast, apply that very widescreen correction
//     via ScriptCB_GetScreenInfo()[4] ~= 1.0 — so THE GAME ALREADY KNOWS
//     how to correct movie geometry for widescreen, these screens simply
//     don't use it.
//
//     WHAT THIS PATCH DOES. Finds the 4-argument fnStartMovie call in the
//     given screen, finds the LOADK instruction that loads mode, and
//     repoints its Bx operand from the 1.0 constant to the 2.0 constant.
//     The instruction count does NOT change, so no relative-jump remapping
//     is needed at all; the 2.0 constant already exists in these screens'
//     pools (used as transition[2]), and if it ever doesn't, it is appended
//     (existing constant indices never shift).
//
//     SAFETY AT 4:3. With ws == 1.0 the mode-2 formula yields exactly x=0,
//     y=0, w=W, h=H — byte-for-byte the same behaviour as mode 1. So at
//     4:3/5:4 the patch changes NOTHING by construction, not "by testing".
//     Confirmed by running the real bytecode in a Lua VM: 800x600 and
//     1280x1024 produce an identical rectangle before and after.
//
//     WHAT THIS PATCH DOES NOT PROMISE. It is NOT a proven subtitle fix.
//     Measurement (10 screenshots, 10 resolutions) shows the movie caption
//     is drawn only at aspect <= 4:3, and that its position is computed
//     correctly from the real screen height — so it is not pushed
//     off-screen, it is switched off somewhere in native code. Whether that
//     decision keys off the movie rectangle (then this patch helps) or off
//     the screen aspect (then it doesn't) cannot be told from the game's
//     files. This patch is the cheapest possible probe of that fork: one
//     operand versus a full timer-driven overlay: no "fixed" claim without
//     an in-game screenshot.
// =============================================================================

using System.Text;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Core.Bf2Widescreen;

public static class MovieRectModePatcher
{
    // UA: Ім'я Lua-обгортки запуску відео (common.lvl / "ifelem_shellscreen").
    // EN: Name of the Lua movie-start wrapper (common.lvl / "ifelem_shellscreen").
    public const string StartMovieGlobal = "ifelem_shellscreen_fnStartMovie";

    // UA: Екрани кампанійських роликів — саме вони передають mode = 1.0.
    // EN: The campaign movie screens — these are the ones passing mode = 1.0.
    public const string BattleIntroScreen = "ifs_campaign_battle_intro";
    public const string TurnIntroScreen = "ifs_campaign_turn_intro";

    // UA: Ванільне значення режиму (розтягнути на весь екран) і цільове
    //     (власна widescreen-корекція гри).
    // EN: The vanilla mode value (stretch fullscreen) and the target one
    //     (the game's own widescreen correction).
    private const float VanillaMode = 1.0f;
    private const float WidescreenMode = 2.0f;

    // UA: Підсумок однієї заміни — для чесного звіту в діагностиці.
    // EN: A summary of one replacement — for an honest diagnostic report.
    public sealed record PatchResult(string ScreenName, string ProtoPath, int Pc, float OldMode, float NewMode);

    // -------------------------------------------------------------------------
    // UA: Мутує root.Children напряму (той самий патерн, що й інші патчери
    //     цього проєкту) — після виклику root готовий для UcfbWriter.WriteFile.
    //     Кидає виняток, якщо цільового екрана/виклику немає — краще впасти
    //     явно, ніж мовчки віддати незмінений файл під виглядом патча.
    // EN: Mutates root.Children directly (the same pattern as this project's
    //     other patchers) — after the call root is ready for
    //     UcfbWriter.WriteFile. Throws if the target screen/call is missing —
    //     better to fail loudly than to silently hand back an unchanged file
    //     dressed up as a patch.
    // -------------------------------------------------------------------------
    public static PatchResult ApplyWidescreenMode(UcfbChunk root, string screenName)
    {
        var screenChunk = root.Children.FirstOrDefault(c =>
            c.FourCC == "scr_" &&
            c.Children.FirstOrDefault(n => n.FourCC == "NAME") is { } nameChunk &&
            Encoding.ASCII.GetString(nameChunk.RawData).TrimEnd('\0') == screenName);

        if (screenChunk is null)
            throw new InvalidOperationException(
                $"UA: Екран '{screenName}' не знайдено серед top-level 'scr_'-чанків / " +
                $"EN: Screen '{screenName}' not found among top-level 'scr_' chunks");

        var bodyIndex = screenChunk.Children.FindIndex(c => c.FourCC == "BODY");
        if (bodyIndex < 0)
            throw new InvalidOperationException(
                $"UA: '{screenName}' не має дочірнього чанка BODY / EN: '{screenName}' has no BODY child chunk");

        var bodyChunk = screenChunk.Children[bodyIndex];
        var (newBody, result) = BuildPatchedBody(bodyChunk.RawData, screenName);

        screenChunk.Children[bodyIndex] = new UcfbChunk
        {
            Id = bodyChunk.Id,
            FourCC = bodyChunk.FourCC,
            DataSize = (uint)newBody.Length,
            RawData = newBody,
        };

        return result;
    }

    // -------------------------------------------------------------------------
    // UA: Той самий патч, але на рівні сирих байтів BODY — зручно для тестів.
    // EN: The same patch at the raw BODY-bytes level — convenient for tests.
    // -------------------------------------------------------------------------
    public static (byte[] Body, PatchResult Result) BuildPatchedBody(byte[] originalBodyBytes, string screenName)
    {
        var original = Lua50BytecodeReader.Parse(originalBodyBytes).Root;

        // UA: Обгортка викликається зсередини Enter — тобто у ВКЛАДЕНОМУ
        //     прототипі, а не в корені. Обходимо все дерево прототипів.
        // EN: The wrapper is called from inside Enter — i.e. from a NESTED
        //     prototype, not the root. Walk the whole prototype tree.
        var located = LocateModeLoad(original, path: "root")
            ?? throw new InvalidOperationException(
                $"UA: В екрані '{screenName}' не знайдено виклик {StartMovieGlobal} з 4 аргументами та " +
                "константним режимом — структура відрізняється від очікуваної, не гадаємо. / " +
                $"EN: No 4-argument {StartMovieGlobal} call with a constant mode found in screen " +
                $"'{screenName}' — the structure differs from what was expected; not guessing.");

        var (proto, protoPath, loadPc, oldMode) = located;

        if (Math.Abs(oldMode - WidescreenMode) < 0.0001f)
            throw new InvalidOperationException(
                $"UA: '{screenName}' вже використовує режим {WidescreenMode} — патч не потрібен. / " +
                $"EN: '{screenName}' already uses mode {WidescreenMode} — no patch needed.");

        if (Math.Abs(oldMode - VanillaMode) > 0.0001f)
            throw new InvalidOperationException(
                $"UA: Очікувався ванільний режим {VanillaMode}, знайдено {oldMode} — не чіпаємо, " +
                "бо це вже не та поведінка, яку тут досліджено. / " +
                $"EN: Expected the vanilla mode {VanillaMode}, found {oldMode} — leaving it alone, " +
                "this is no longer the investigated behaviour.");

        // UA: Індекс константи 2.0; якщо її раптом немає — додаємо в КІНЕЦЬ,
        //     щоб індекси наявних констант не зсунулись.
        // EN: Index of the 2.0 constant; if absent, append at the END so that
        //     existing constant indices never shift.
        var constants = new List<LuaConstant>(proto.Constants);
        var targetIndex = constants.FindIndex(k =>
            k.Kind == LuaConstantKind.Number && Math.Abs(k.NumberValue - WidescreenMode) < 0.0001f);
        if (targetIndex < 0)
        {
            constants.Add(new LuaConstant
            {
                Kind = LuaConstantKind.Number,
                ValueOffset = -1,
                NumberValue = WidescreenMode,
            });
            targetIndex = constants.Count - 1;
        }

        // UA: Єдина зміна коду — операнд Bx однієї інструкції LOADK.
        //     Довжина коду не змінюється, тому переходи чіпати не треба.
        // EN: The only code change — the Bx operand of a single LOADK.
        //     The code length is unchanged, so jumps need no adjustment.
        var instructions = new List<LuaInstruction>(proto.Instructions);
        instructions[loadPc] = instructions[loadPc] with { Bx = targetIndex, SBx = targetIndex - MaxArgSBx };

        var patchedProto = proto with { Constants = constants, Instructions = instructions };
        var newRoot = ReplacePrototype(original, proto, patchedProto);

        return (Lua50BytecodeWriter.Write(newRoot),
                new PatchResult(screenName, protoPath, loadPc, oldMode, WidescreenMode));
    }

    private const int MaxArgSBx = ((1 << 18) - 1) >> 1;

    // -------------------------------------------------------------------------
    // UA: Шукає в дереві прототипів виклик fnStartMovie рівно з 4 аргументами
    //     і повертає прототип + pc інструкції LOADK, що завантажує 4-й
    //     аргумент (режим), + його поточне значення. Форма з 8 аргументами
    //     свідомо ігнорується: там прямокутник уже задає сам викликач, і
    //     чіпати його не треба.
    // EN: Finds a fnStartMovie call with exactly 4 arguments in the prototype
    //     tree and returns the prototype + the pc of the LOADK loading the
    //     4th argument (mode) + its current value. The 8-argument form is
    //     deliberately ignored: there the caller already supplies the rect,
    //     and it must not be touched.
    // -------------------------------------------------------------------------
    private static (LuaFunctionPrototype Proto, string Path, int LoadPc, float Mode)? LocateModeLoad(
        LuaFunctionPrototype proto, string path)
    {
        var consts = proto.Constants;
        var globalIndex = -1;
        for (var i = 0; i < consts.Count; i++)
        {
            if (consts[i].Kind == LuaConstantKind.String && consts[i].StringValue == StartMovieGlobal)
            {
                globalIndex = i;
                break;
            }
        }

        if (globalIndex >= 0)
        {
            var ins = proto.Instructions;
            for (var i = 0; i < ins.Count; i++)
            {
                if (ins[i].Opcode != LuaOpcode.GetGlobal || ins[i].Bx != globalIndex)
                    continue;

                var callReg = ins[i].A;

                // UA: Найближчий CALL на тому ж регістрі — це шуканий виклик.
                // EN: The nearest CALL on the same register is the target call.
                for (var j = i + 1; j < ins.Count; j++)
                {
                    if (ins[j].Opcode != LuaOpcode.Call || ins[j].A != callReg)
                        continue;

                    // UA: B = кількість аргументів + 1. Потрібне рівно 4.
                    // EN: B = argument count + 1. Exactly 4 is required.
                    if (ins[j].B != 5)
                        break;

                    // UA: 4-й аргумент (режим) лежить у регістрі callReg+4.
                    //     Шукаємо ОСТАННЮ інструкцію LOADK у цей регістр між
                    //     GETGLOBAL і CALL.
                    // EN: The 4th argument (mode) lives in register callReg+4.
                    //     Find the LAST LOADK into that register between the
                    //     GETGLOBAL and the CALL.
                    var modeReg = callReg + 4;
                    for (var k = j - 1; k > i; k--)
                    {
                        if (ins[k].Opcode != LuaOpcode.LoadK || ins[k].A != modeReg)
                            continue;

                        var kIdx = ins[k].Bx;
                        if (kIdx is not int ki || ki < 0 || ki >= consts.Count ||
                            consts[ki].Kind != LuaConstantKind.Number)
                            break;

                        return (proto, path, k, consts[ki].NumberValue);
                    }

                    break;
                }
            }
        }

        for (var n = 0; n < proto.NestedPrototypes.Count; n++)
        {
            var found = LocateModeLoad(proto.NestedPrototypes[n], $"{path}/{n}");
            if (found is not null)
                return found;
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // UA: Повертає копію дерева прототипів, у якій `oldProto` замінено на
    //     `newProto` (порівняння за посиланням — саме той екземпляр, який
    //     повернув LocateModeLoad). LuaFunctionPrototype — record, тобто
    //     незмінний, тому "заміна на місці" неможлива і дерево
    //     перебудовується згори вниз.
    // EN: Returns a copy of the prototype tree with `oldProto` replaced by
    //     `newProto` (compared by reference — the exact instance
    //     LocateModeLoad returned). LuaFunctionPrototype is a record, i.e.
    //     immutable, so there is no in-place replacement and the tree is
    //     rebuilt top-down.
    // -------------------------------------------------------------------------
    private static LuaFunctionPrototype ReplacePrototype(
        LuaFunctionPrototype current, LuaFunctionPrototype oldProto, LuaFunctionPrototype newProto)
    {
        if (ReferenceEquals(current, oldProto))
            return newProto;

        var nested = current.NestedPrototypes
            .Select(n => ReplacePrototype(n, oldProto, newProto))
            .ToList();

        return current with { NestedPrototypes = nested };
    }
}
