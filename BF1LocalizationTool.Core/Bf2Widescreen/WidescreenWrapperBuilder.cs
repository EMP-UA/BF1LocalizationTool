// =============================================================================
// BF1LocalizationTool.Core — Bf2Widescreen/WidescreenWrapperBuilder.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Сама формула widescreen-фікса — clean-room реалізація механізму,
//     емпірично виявленого в SWBF2 Remaster-моді (GT Anakin,
//     moddb.com/mods/star-wars-battlefront-ii-full-hd-interface,
//     interface_fixes :: root/13, повністю продизасембльовано вручну):
//
//         x_new = x_orig * (W / 800)
//         y_new = y_orig * (H / 600)
//         width_new = width_orig * (W / 800)
//         height_new = height_orig * (H / 600)
//
//     де W,H — живі значення з ScriptCB_GetScreenInfo(), а 800×600 —
//     "полотно", в якому позиції/розміри задані в оригінальних .lua-
//     скриптах гри (підтверджено: саме ці дільники зустрічались разом
//     із ScriptCB_GetScreenInfo() у 3 реальних функціях з жорсткою
//     hit-test-логікою — див. AnalyzeShellScriptConstantsCommand).
//     width/height ТЕЖ масштабуються (не лише x/y) — підтверджено на
//     реальному Remaster-враппері NewButtonWindow (interface_fixes::
//     root/9). Це необхідно для контейнерів із x=0,y=0 (напр. ProfileBox
//     в ifs_login): сама позиція вже 0, тому без масштабування розміру
//     такий контейнер лишався б фіксованого розміру на будь-якій
//     роздільності.
//
//     НАШ варіант — СПРОЩЕНИЙ порівняно з remaster-модом:
//       - Без "якірних" (anchor) винятків для окремих іменованих
//         віджетів (launch_btn/Check_Era/InfoListbox тощо) — Anakin
//         підібрав їх вручну шляхом візуального тестування в грі;
//         у нас немає способу емпірично підтвердити ці конкретні
//         значення без такого ж тестування, тому свідомо НЕ вигадуємо
//         їх (rule #1) — пряма формула без винятків це чесний,
//         перевірюваний перший крок.
//       - Без forward-передачі vararg-аргументів (NumParams=1,
//         IsVararg=0) — узагальнена ідіома "{...}" + SETLISTO вимагала
//         б опкоду VARARG, якого не було в ЖОДНОМУ з 99 перевірених
//         реальних чанків (ми не можемо емпірично підтвердити його
//         кодування на реальних даних, а тестового Lua-рантайму в нас
//         немає) — тому свідомо НЕ підтримуємо цей рідкісний випадок
//         у v1. Зайві аргументи при виклику просто ігноруються
//         (стандартна, безпечна поведінка Lua — НЕ помилка).
//       - Без CLOSURE-з-upvalues (як у Remaster-моді, де orig/W/H
//         захоплювались через upvalue) — наш wrapper звертається до
//         оригінальної функції через ГЛОБАЛЬНУ змінну з новим іменем
//         (SetGlobal backupName = orig ПЕРЕД встановленням wrapper'а).
//         Це означає NumUpvalues=0 для кожної wrapper-функції — а отже
//         НЕ потрібні "псевдо-інструкції" MOVE/GETUPVAL після CLOSURE
//         (офіційна вимога формату — по одній на кожен upvalue
//         вкладеного прототипу), що прибирає цілий клас потенційних
//         помилок кодування, які неможливо перевірити без реального
//         Lua-рантайму.
// EN: The actual widescreen-fix formula — a clean-room implementation of
//     the mechanism empirically discovered in the SWBF2 Remaster mod
//     (GT Anakin, moddb.com/mods/star-wars-battlefront-ii-full-hd-interface,
//     interface_fixes :: root/13, fully manually disassembled):
//
//         x_new = x_orig * (W / 800)
//         y_new = y_orig * (H / 600)
//
//     where W,H are live values from ScriptCB_GetScreenInfo(), and 800×600
//     is the "canvas" positions/sizes are authored against in the game's
//     original .lua scripts (confirmed: exactly these divisors appeared
//     together with ScriptCB_GetScreenInfo() in 3 real functions with
//     hardcoded hit-test logic — see AnalyzeShellScriptConstantsCommand).
//     width/height are ALSO scaled (not just x/y) — confirmed against the
//     real Remaster NewButtonWindow wrapper (interface_fixes::root/9).
//     This is required for containers with x=0,y=0 (e.g. ifs_login's
//     ProfileBox): the position is already 0, so without scaling the size
//     such a container would stay a fixed size at every resolution.
//
//     OUR version is SIMPLIFIED compared to the remaster mod:
//       - No "anchor" exceptions for specific named widgets
//         (launch_btn/Check_Era/InfoListbox etc.) — Anakin tuned those by
//         hand via in-game visual testing; we have no way to empirically
//         confirm those specific values without the same kind of testing,
//         so we deliberately do NOT invent them (rule #1) — the plain
//         formula with no exceptions is an honest, verifiable first step.
//       - No vararg argument forwarding (NumParams=1, IsVararg=0) — the
//         general "{...}" + SETLISTO idiom would require the VARARG
//         opcode, which appeared in NONE of the 99 checked real chunks
//         (we cannot empirically confirm its encoding against real data,
//         and we have no test Lua runtime) — so this rare case is
//         deliberately NOT supported in v1. Extra call arguments are
//         simply ignored (standard, safe Lua behavior — NOT an error).
//       - No CLOSURE-with-upvalues (unlike the Remaster mod, where
//         orig/W/H were captured via upvalue) — our wrapper reaches the
//         original function through a GLOBAL variable under a new name
//         (SetGlobal backupName = orig BEFORE installing the wrapper).
//         This means NumUpvalues=0 for every wrapper function — so no
//         "pseudo-instructions" (MOVE/GETUPVAL after CLOSURE, one per
//         nested-prototype upvalue per the official format) are needed,
//         removing an entire class of potential encoding mistakes that
//         can't be checked without a real Lua runtime.
// =============================================================================

using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Core.Bf2Widescreen;

public static class WidescreenWrapperBuilder
{
    // UA: Референсне "полотно" 800×600 — ЕМПІРИЧНО підтверджене значення
    //     (не вигадане): саме ці дільники (MUL потім DIV на 800.0/600.0)
    //     знайдено разом із ScriptCB_GetScreenInfo() у трьох реальних
    //     функціях shell.lvl/ingame.lvl (ifs_freeform_purchase_unit
    //     root/8, ifs_freeform_purchase_tech root/11, ingame.lvl
    //     ifs_pc_spawnselect root/1).
    // EN: The reference 800×600 "canvas" — an EMPIRICALLY confirmed value
    //     (not invented): exactly these divisors (MUL then DIV by
    //     800.0/600.0) were found together with ScriptCB_GetScreenInfo()
    //     in three real shell.lvl/ingame.lvl functions
    //     (ifs_freeform_purchase_unit root/8, ifs_freeform_purchase_tech
    //     root/11, ingame.lvl ifs_pc_spawnselect root/1).
    public const float ReferenceWidth = 800f;
    public const float ReferenceHeight = 600f;

    // -------------------------------------------------------------------------
    // UA: Будує ОДНУ wrapper-функцію: params=1 (t — таблиця позиції з
    //     опціональними полями x/y), vararg=0. Логіка:
    //         local W, H = ScriptCB_GetScreenInfo()
    //         if t.x then t.x = t.x * W / 800 end
    //         if t.y then t.y = t.y * H / 600 end
    //         return <backupGlobalName>(t)
    //     TEST-семантика (офіційна, lopcodes.h): "TEST A C: якщо
    //     bool(R(A)) != C, тоді pc++ (пропустити наступну — зазвичай
    //     JMP)". Тому ідіома "TEST R,C=0" + "JMP -> кінець_блоку"
    //     виконує рівно "if R then БЛОК end" — підтверджено на реальних
    //     чанках багаторазово (умовні розгалуження в ifs_*).
    // EN: Builds ONE wrapper function: params=1 (t — a position table with
    //     optional x/y fields), vararg=0. Logic:
    //         local W, H = ScriptCB_GetScreenInfo()
    //         if t.x then t.x = t.x * W / 800 end
    //         if t.y then t.y = t.y * H / 600 end
    //         return <backupGlobalName>(t)
    //     TEST semantics (official, lopcodes.h): "TEST A C: if
    //     bool(R(A)) != C, then pc++ (skip the next instruction —
    //     usually a JMP)". So the idiom "TEST R,C=0" + "JMP -> end_of_
    //     block" implements exactly "if R then BLOCK end" — confirmed
    //     against real chunks repeatedly (conditional branches in
    //     ifs_*).
    // -------------------------------------------------------------------------
    public static LuaFunctionPrototype BuildTableXyScaleWrapperFunction(string backupGlobalName, string path)
    {
        var b = new Lua50FunctionBuilder
        {
            NumParams = 1,
            IsVararg = 0,
        };

        var kScreenInfo = b.AddStringConstant("ScriptCB_GetScreenInfo");
        var kX = b.AddStringConstant("x");
        var kRefW = b.AddNumberConstant(ReferenceWidth);
        var kY = b.AddStringConstant("y");
        var kRefH = b.AddNumberConstant(ReferenceHeight);
        var kWidth = b.AddStringConstant("width");
        var kHeight = b.AddStringConstant("height");
        var kBackup = b.AddStringConstant(backupGlobalName);

        // local W, H = ScriptCB_GetScreenInfo()  -- R1=W, R2=H
        b.EmitABx(LuaOpcode.GetGlobal, a: 1, bx: kScreenInfo);
        b.Emit(LuaOpcode.Call, a: 1, b: 1, c: 3); // UA: 0 аргументів, 2 результати / EN: 0 args, 2 results

        // if t.x then t.x = t.x * W / 800 end
        b.Emit(LuaOpcode.GetTable, a: 3, b: 0, c: Lua50FunctionBuilder.Rk(kX));
        b.Emit(LuaOpcode.Test, a: 3, c: 0);
        var jmpSkipX = b.EmitJumpPlaceholder();
        b.Emit(LuaOpcode.Mul, a: 3, b: 3, c: 1);
        b.Emit(LuaOpcode.Div, a: 3, b: 3, c: Lua50FunctionBuilder.Rk(kRefW));
        b.Emit(LuaOpcode.SetTable, a: 0, b: Lua50FunctionBuilder.Rk(kX), c: 3);
        b.PatchJump(jmpSkipX, b.NextPc);

        // if t.y then t.y = t.y * H / 600 end
        b.Emit(LuaOpcode.GetTable, a: 3, b: 0, c: Lua50FunctionBuilder.Rk(kY));
        b.Emit(LuaOpcode.Test, a: 3, c: 0);
        var jmpSkipY = b.EmitJumpPlaceholder();
        b.Emit(LuaOpcode.Mul, a: 3, b: 3, c: 2);
        b.Emit(LuaOpcode.Div, a: 3, b: 3, c: Lua50FunctionBuilder.Rk(kRefH));
        b.Emit(LuaOpcode.SetTable, a: 0, b: Lua50FunctionBuilder.Rk(kY), c: 3);
        b.PatchJump(jmpSkipY, b.NextPc);

        // UA: width/height ТЕЖ масштабуються — ЕМПІРИЧНО підтверджено:
        //     реальний Remaster-враппер NewButtonWindow (interface_fixes::
        //     root/9, pc 21-29) масштабує САМЕ width*W/800, height*H/600,
        //     а не лише x/y. Це важливо для контейнерів з фіксованим
        //     width/height (напр. ProfileBox з ifs_login — x=0,y=0): без
        //     масштабування розміру такий контейнер лишався б
        //     width=400/height=300 на будь-якій роздільності.
        // EN: width/height are ALSO scaled — EMPIRICALLY confirmed: the
        //     real Remaster NewButtonWindow wrapper (interface_fixes::
        //     root/9, pc 21-29) scales width*W/800, height*H/600 too, not
        //     just x/y. This matters for containers with fixed
        //     width/height (e.g. ifs_login's ProfileBox — x=0,y=0):
        //     without scaling the size, such a container would stay
        //     width=400/height=300 at every resolution.
        // if t.width then t.width = t.width * W / 800 end
        b.Emit(LuaOpcode.GetTable, a: 3, b: 0, c: Lua50FunctionBuilder.Rk(kWidth));
        b.Emit(LuaOpcode.Test, a: 3, c: 0);
        var jmpSkipWidth = b.EmitJumpPlaceholder();
        b.Emit(LuaOpcode.Mul, a: 3, b: 3, c: 1);
        b.Emit(LuaOpcode.Div, a: 3, b: 3, c: Lua50FunctionBuilder.Rk(kRefW));
        b.Emit(LuaOpcode.SetTable, a: 0, b: Lua50FunctionBuilder.Rk(kWidth), c: 3);
        b.PatchJump(jmpSkipWidth, b.NextPc);

        // if t.height then t.height = t.height * H / 600 end
        b.Emit(LuaOpcode.GetTable, a: 3, b: 0, c: Lua50FunctionBuilder.Rk(kHeight));
        b.Emit(LuaOpcode.Test, a: 3, c: 0);
        var jmpSkipHeight = b.EmitJumpPlaceholder();
        b.Emit(LuaOpcode.Mul, a: 3, b: 3, c: 2);
        b.Emit(LuaOpcode.Div, a: 3, b: 3, c: Lua50FunctionBuilder.Rk(kRefH));
        b.Emit(LuaOpcode.SetTable, a: 0, b: Lua50FunctionBuilder.Rk(kHeight), c: 3);
        b.PatchJump(jmpSkipHeight, b.NextPc);

        // return <backupGlobalName>(t)
        b.EmitABx(LuaOpcode.GetGlobal, a: 4, bx: kBackup);
        b.Emit(LuaOpcode.Move, a: 5, b: 0);
        b.Emit(LuaOpcode.TailCall, a: 4, b: 2, c: 0); // UA: 1 аргумент (t) / EN: 1 argument (t)
        b.Emit(LuaOpcode.Return, a: 4, b: 0);

        b.MaxStackSize = 6; // R0..R5

        return b.Build(path: path);
    }

    // -------------------------------------------------------------------------
    // UA: ДІАГНОСТИЧНА обгортка (НЕ фікс) — замість формули x*W/800, y*H/600
    //     робить БЕЗУМОВНИЙ фіксований зсув (t.x=t.x+offset, t.y=t.y+offset).
    //     Призначення: перевірити емпірично, чи ця конкретна wrapper-функція
    //     (NewIFContainer) взагалі ВИКЛИКАЄТЬСЯ для конкретного екрана — якщо
    //     після встановлення такої "маркерної" обгортки жоден елемент
    //     візуально не зрушується — це означає, що екран будується ЧЕРЕЗ
    //     ІНШУ функцію (напр. NewButtonWindow, чию сигнатуру ми ще не
    //     продизасемблювали), а не через NewIFContainer, і формулу масштабу
    //     там застосовувати нема сенсу, доки ми це не з'ясуємо.
    // EN: A DIAGNOSTIC wrapper (NOT the fix) — instead of the x*W/800,
    //     y*H/600 formula, does an UNCONDITIONAL fixed offset
    //     (t.x=t.x+offset, t.y=t.y+offset). Purpose: empirically check
    //     whether this particular wrapped function (NewIFContainer) is even
    //     CALLED for a given screen — if after installing this "marker"
    //     wrapper nothing visually moves, that means the screen is built
    //     through a DIFFERENT function (e.g. NewButtonWindow, whose
    //     signature we haven't disassembled yet), not NewIFContainer, and
    //     applying the scale formula there would be pointless until we find
    //     out.
    // -------------------------------------------------------------------------
    public static LuaFunctionPrototype BuildDebugMarkerOffsetWrapperFunction(string backupGlobalName, string path, float offset = 300f)
    {
        var b = new Lua50FunctionBuilder
        {
            NumParams = 1,
            IsVararg = 0,
        };

        var kX = b.AddStringConstant("x");
        var kOffset = b.AddNumberConstant(offset);
        var kY = b.AddStringConstant("y");
        var kBackup = b.AddStringConstant(backupGlobalName);

        // if t.x then t.x = t.x + offset end
        b.Emit(LuaOpcode.GetTable, a: 1, b: 0, c: Lua50FunctionBuilder.Rk(kX));
        b.Emit(LuaOpcode.Test, a: 1, c: 0);
        var jmpSkipX = b.EmitJumpPlaceholder();
        b.Emit(LuaOpcode.Add, a: 1, b: 1, c: Lua50FunctionBuilder.Rk(kOffset));
        b.Emit(LuaOpcode.SetTable, a: 0, b: Lua50FunctionBuilder.Rk(kX), c: 1);
        b.PatchJump(jmpSkipX, b.NextPc);

        // if t.y then t.y = t.y + offset end
        b.Emit(LuaOpcode.GetTable, a: 1, b: 0, c: Lua50FunctionBuilder.Rk(kY));
        b.Emit(LuaOpcode.Test, a: 1, c: 0);
        var jmpSkipY = b.EmitJumpPlaceholder();
        b.Emit(LuaOpcode.Add, a: 1, b: 1, c: Lua50FunctionBuilder.Rk(kOffset));
        b.Emit(LuaOpcode.SetTable, a: 0, b: Lua50FunctionBuilder.Rk(kY), c: 1);
        b.PatchJump(jmpSkipY, b.NextPc);

        // return <backupGlobalName>(t)
        b.EmitABx(LuaOpcode.GetGlobal, a: 2, bx: kBackup);
        b.Emit(LuaOpcode.Move, a: 3, b: 0);
        b.Emit(LuaOpcode.TailCall, a: 2, b: 2, c: 0);
        b.Emit(LuaOpcode.Return, a: 2, b: 0);

        b.MaxStackSize = 4; // R0..R3

        return b.Build(path: path);
    }

    // -------------------------------------------------------------------------
    // UA: Будує ПОВНИЙ кореневий скрипт, який встановлює wrapper для
    //     КОЖНОЇ вказаної глобальної функції:
    //         <backupName> = <origName>          -- зберегти оригінал
    //         <origName> = function(t) ... end     -- CLOSURE, 0 upvalues
    //     Саме цей скрипт передається як wrapperBodyBytes у
    //     ShellEntryPointPatcher.ApplyBootstrapPatch.
    //     buildWrapperFunc — фабрика конкретної wrapper-функції (за
    //     замовчуванням — реальна формула масштабу; для діагностики можна
    //     передати BuildDebugMarkerOffsetWrapperFunction).
    // EN: Builds the FULL root script that installs a wrapper for EVERY
    //     given global function:
    //         <backupName> = <origName>          -- preserve the original
    //         <origName> = function(t) ... end     -- CLOSURE, 0 upvalues
    //     This is exactly the script passed as wrapperBodyBytes to
    //     ShellEntryPointPatcher.ApplyBootstrapPatch.
    //     buildWrapperFunc — the factory for the actual per-function
    //     wrapper (defaults to the real scale formula; for diagnostics you
    //     can pass BuildDebugMarkerOffsetWrapperFunction instead).
    // -------------------------------------------------------------------------
    public static LuaFunctionPrototype BuildWidescreenWrapperScript(
        IReadOnlyList<string> targetFunctionNames,
        Func<string, string, LuaFunctionPrototype>? buildWrapperFunc = null)
    {
        buildWrapperFunc ??= BuildTableXyScaleWrapperFunction;
        var root = new Lua50FunctionBuilder { NumParams = 0, IsVararg = 0, MaxStackSize = 1 };

        for (var i = 0; i < targetFunctionNames.Count; i++)
        {
            var origName = targetFunctionNames[i];
            var backupName = $"_rema_orig_{origName}";

            var wrapperProto = buildWrapperFunc(backupName, $"root/{i}");
            var nestedIndex = root.AddNestedPrototype(wrapperProto);

            var kOrig = root.AddStringConstant(origName);
            var kBackup = root.AddStringConstant(backupName);

            // <backupName> = <origName>
            root.EmitABx(LuaOpcode.GetGlobal, a: 0, bx: kOrig);
            root.EmitABx(LuaOpcode.SetGlobal, a: 0, bx: kBackup);

            // <origName> = <wrapper closure>  (0 upvalues — без псевдо-інструкцій MOVE/GETUPVAL)
            root.EmitABx(LuaOpcode.Closure, a: 0, bx: nestedIndex);
            root.EmitABx(LuaOpcode.SetGlobal, a: 0, bx: kOrig);
        }

        root.Emit(LuaOpcode.Return, a: 0, b: 1);

        return root.Build();
    }

    // UA: Функції, чиї виклики ЕМПІРИЧНО підтверджено як "таблиця з
    //     опціональними x/y" (НЕ width/height — тих ми свідомо НЕ чіпаємо,
    //     див. нижче):
    //       - NewIFContainer: повний дизасемблер interface_fixes::root/13.
    //       - NewButtonWindow: повний дизасемблер interface_fixes::root/9
    //         — той самий шаблон t.x/t.y/GETUPVAL(W,H)/MUL/DIV
    //         підтверджено НАПРЯМУ в реальних викликах (root/9 pc 11-29),
    //         НЕЗАЛЕЖНО від titleText-гейту, який Remaster застосовує лише
    //         до 4 конкретних екранів (ifs.controls.General.map,
    //         ifs.missionselect.selectera/playlist/selectmode) — тобто сам
    //         Remaster НЕ вважає за потрібне рескейлити НАШІ x/y для решти
    //         викликів NewButtonWindow, лише для цих чотирьох.
    //
    //     СВІДОМЕ РІШЕННЯ: на відміну від Remaster-мода (per-екранний,
    //     вручну підібраний гейтинг через
    //     titleText/ім'я екрана — метод, що вимагає такого ж плейтестингу,
    //     яким займався сам автор, і копіювання його рішень було б
    //     копіюванням чужої творчої роботи, а не механізму), ми
    //     ЗАСТОСОВУЄМО формулу x*W/800, y*H/600 БЕЗУМОВНО до ВСІХ викликів
    //     обох функцій, без жодного відбору за назвою екрана. Це системний,
    //     а не точковий фікс: КОЖЕН елемент, збудований через ці дві
    //     функції з абсолютними x/y (а не ScreenRelativeX/Y — ті вже
    //     незалежні від роздільності й нашим wrapper'ом не чіпаються,
    //     оскільки перевіряється лише t.x/t.y), перераховується однаково,
    //     по всій грі.
    //
    //     НЕ включені сюди (свідомо): AddIFScreen (per-widget ручне
    //     підлаштування конкретних НАЗВАНИХ піделементів на конкретних
    //     названих екранах — 203+ констант, не формула, а результат
    //     плейтестингу автора), NewIFImage/NewPCIFButton (точкові фікси
    //     ОДНОГО конкретного елемента кожен — GameSpy-лого, кнопка
    //     "Reset" — не мають нічого спільного з widescreen),
    //     ScriptCB_GetFontHeight/NewIFText (підміна шрифтів на "_rema"-
    //     варіанти — окремий, паралельний напрямок роботи, не геометрія;
    //     див. Task #17).
    // EN: Functions whose call convention is EMPIRICALLY confirmed as
    //     "a table with optional x/y" (NOT width/height — those are
    //     deliberately left untouched, see below):
    //       - NewIFContainer: full disassembly of interface_fixes::root/13.
    //       - NewButtonWindow: full disassembly of interface_fixes::root/9
    //         — the exact same t.x/t.y/GETUPVAL(W,H)/MUL/DIV
    //         pattern confirmed DIRECTLY in real call sites (root/9 pc
    //         11-29), INDEPENDENT of the titleText gate the Remaster mod
    //         applies to only 4 specific screens (ifs.controls.General.map,
    //         ifs.missionselect.selectera/playlist/selectmode) — i.e. the
    //         Remaster mod itself doesn't consider it necessary to rescale
    //         x/y for the rest of NewButtonWindow's callers, only these
    //         four.
    //
    //     A DELIBERATE DECISION: unlike the Remaster mod (per-screen,
    //     hand-picked gating by titleText/
    //     screen name — a method that requires the same kind of playtesting
    //     its author did, and copying his choices would mean copying his
    //     creative work, not a mechanism), we APPLY the x*W/800, y*H/600
    //     formula UNCONDITIONALLY to ALL calls of both functions, with no
    //     screen-name filtering at all. This is a systemic fix, not a
    //     point patch: EVERY element built through these two functions with
    //     absolute x/y (not ScreenRelativeX/Y — those are already
    //     resolution-independent and untouched by our wrapper, since it
    //     only checks t.x/t.y) gets recalculated the same way, everywhere
    //     in the game.
    //
    //     Deliberately NOT included here: AddIFScreen (per-widget manual
    //     tuning of specific NAMED sub-elements on specific named screens —
    //     203+ constants, not a formula, but the result of the author's own
    //     playtesting), NewIFImage/NewPCIFButton (point fixes for ONE
    //     specific element each — the GameSpy logo, the "Reset" button —
    //     unrelated to widescreen), ScriptCB_GetFontHeight/NewIFText (font
    //     substitution with "_rema" variants — a separate, parallel
    //     workstream, not geometry; see Task #17).
    public static readonly string[] ConfirmedTableXyFunctions = ["NewIFContainer", "NewButtonWindow"];
}
