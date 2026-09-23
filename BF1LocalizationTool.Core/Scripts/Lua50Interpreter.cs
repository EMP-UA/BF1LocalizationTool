// =============================================================================
// BF1LocalizationTool.Core — Scripts/Lua50Interpreter.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Виконавець байткоду Lua 5.0 — третій, останній елемент тріади
//     Reader → Writer → Interpreter.
//
//     НАВІЩО. Статичний аналіз показує, ЩО написано у скрипті, але не показує,
//     з якими ЧИСЛАМИ він насправді викликає рушій: геометрія інтерфейсу BF2
//     рахується в рантаймі (цикли, умови, похідні від ширини батька тощо).
//     Нативні функції НЕ завжди приймають хендл об'єкта першим аргументом —
//     статичним аналізом байткоду цього не видно, лише прогоном:
//
//         ScriptCB_IFObj_SetPos(50, -25, 0)      -- 3 аргументи, БЕЗ хендла
//         ScriptCB_IFObj_SetPos(cp, 50, -25, 0)  -- 4 аргументи, з хендлом
//
//     Обидві форми співіснують: коротка використовується всередині блоку
//     AddIF*…EndIFObj, де «поточний об'єкт» неявний. Виявити це припущеннями
//     неможливо — лише виконанням.
//
//     ЩО ЦЕ НЕ Є. Це не емулятор гри. Рушійні функції тут — заглушки
//     (див. Bf2ScriptHost), які лише ЗАПИСУЮТЬ аргументи. Мета — отримати
//     фактичну послідовність викликів, а не намалювати меню.
//
//     Семантика опкодів — за офіційним lvm.c 5.0.2. Місця, звірені з реальним
//     байткодом BF2:
//       TEST      — перевіряється R(B); A лише регістр-приймач (у Lua 5.1+
//                   форма інша, звідси типова помилка);
//       числовий for — немає OP_FORPREP: компілятор емітує init -= step і JMP
//                   на FORLOOP у кінці тіла;
//       RK(x)     — x < 128 ? регістр : константа (MAXSTACK=128 у збірці BF2,
//                   див. Lua50BytecodeReader.MaxStack).
//
// EN: A Lua 5.0 bytecode executor — the third and final piece of the
//     Reader → Writer → Interpreter triad.
//
//     WHY. Static analysis shows WHAT a script contains but not WHICH NUMBERS
//     it actually passes to the engine: BF2 interface geometry is computed at
//     runtime (loops, conditionals, values derived from a parent's width, …).
//     Native functions do NOT always take the object handle as the first
//     argument — static bytecode analysis alone cannot show this, only
//     running it can:
//
//         ScriptCB_IFObj_SetPos(50, -25, 0)      -- 3 args, NO handle
//         ScriptCB_IFObj_SetPos(cp, 50, -25, 0)  -- 4 args, with handle
//
//     Both forms coexist; the short one is used inside an AddIF*…EndIFObj block
//     where the "current object" is implicit. No amount of guessing reveals
//     this — only execution does.
//
//     WHAT THIS IS NOT. Not a game emulator. Engine functions are stubs (see
//     Bf2ScriptHost) that merely RECORD their arguments. The goal is the actual
//     call sequence, not rendering a menu.
//
//     Opcode semantics follow the official lvm.c 5.0.2.
// =============================================================================

using System.Globalization;

namespace BF1LocalizationTool.Core.Scripts;

// -----------------------------------------------------------------------------
// UA: Таблиця Lua. Числові й рядкові ключі в одному словнику; 1 і 1.0 — той
//     самий ключ (як у справжньому Lua), тому цілі числа нормалізуються.
// EN: A Lua table. Numeric and string keys share one dictionary; 1 and 1.0 are
//     the same key (as in real Lua), so integral values are normalised.
// -----------------------------------------------------------------------------
public sealed class LuaTable
{
    public Dictionary<object, object?> Hash { get; } = new();

    private static object NormaliseKey(object key) =>
        key is double d && Math.Abs(d % 1) < double.Epsilon && Math.Abs(d) < long.MaxValue
            ? (long)d
            : key;

    public object? Get(object? key)
    {
        if (key is null) return null;
        return Hash.TryGetValue(NormaliseKey(key), out var v) ? v : null;
    }

    public void Set(object? key, object? value)
    {
        if (key is null) return;
        var k = NormaliseKey(key);
        if (value is null) Hash.Remove(k);
        else Hash[k] = value;
    }

    // UA: Довжина масивної частини (table.getn) — рахуємо 1..n підряд.
    // EN: Length of the array part (table.getn) — count 1..n consecutively.
    public int Count()
    {
        var n = 0;
        while (Hash.ContainsKey((long)(n + 1))) n++;
        return n;
    }
}

// UA: Замикання = прототип + захоплені upvalue-комірки.
// EN: A closure = prototype + captured upvalue cells.
public sealed record LuaClosure(LuaFunctionPrototype Proto, IReadOnlyList<LuaCell> Upvalues);

// UA: Комірка upvalue — спільна між замиканнями, тому саме клас, не значення.
// EN: An upvalue cell — shared between closures, hence a class, not a value.
public sealed class LuaCell
{
    public object? Value;
    public LuaCell(object? value = null) => Value = value;
}

// UA: Нативна функція-заглушка: приймає аргументи, повертає список результатів.
// EN: A native stub: takes arguments, returns a list of results.
public delegate IReadOnlyList<object?> LuaNative(IReadOnlyList<object?> args);

public sealed class LuaRuntimeException(string message) : Exception(message);

public sealed class Lua50Interpreter
{
    public LuaTable Globals { get; } = new();

    // UA: Запобіжник від нескінченних циклів у скриптах, що чекають на стан
    //     рушія, який тут не відтворюється. Краще впасти з чіткою помилкою.
    // EN: A guard against infinite loops in scripts waiting on engine state
    //     that is not reproduced here. Better to fail loudly.
    public long MaxSteps { get; set; } = 50_000_000;

    private long _steps;

    public IReadOnlyList<object?> Call(object? callee, IReadOnlyList<object?> args)
    {
        switch (callee)
        {
            case LuaNative native:
                return native(args);
            case LuaClosure closure:
                return Run(closure, args);
            default:
                throw new LuaRuntimeException(
                    $"UA: спроба викликати не-функцію ({Describe(callee)}) / " +
                    $"EN: attempt to call a non-function ({Describe(callee)})");
        }
    }

    // UA: Розширює масив регістрів, якщо опкод звертається за його межі.
    // EN: Grows the register array if an opcode reaches beyond it.
    private static void EnsureRegisters(ref object?[] registers, int index)
    {
        if (index < registers.Length) return;
        Array.Resize(ref registers, index + 32);
    }

    private static bool Truthy(object? v) => v is not (null or false);

    private static double? ToNumber(object? v) => v switch
    {
        double d => d,
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) => r,
        _ => null,
    };

    public static string Describe(object? v) => v switch
    {
        null => "nil",
        bool b => b ? "true" : "false",
        double d => Math.Abs(d % 1) < double.Epsilon
            ? ((long)d).ToString(CultureInfo.InvariantCulture)
            : d.ToString("R", CultureInfo.InvariantCulture),
        string s => s,
        LuaTable => "table",
        _ => "function",
    };

    // -------------------------------------------------------------------------
    // UA: Виконує одне замикання. Регістри — локальний масив; "top" потрібен
    //     лише для викликів зі змінною кількістю значень (B=0 / C=0).
    // EN: Executes one closure. Registers are a local array; "top" is needed
    //     only for calls with a variable number of values (B=0 / C=0).
    // -------------------------------------------------------------------------
    private IReadOnlyList<object?> Run(LuaClosure closure, IReadOnlyList<object?> args)
    {
        var proto = closure.Proto;
        var code = proto.Instructions;
        var constants = proto.Constants;
        // UA: MaxStackSize — це максимум, потрібний САМІЙ функції, але виклики
        //     зі змінною кількістю результатів (C=0) і SETLISTO можуть записати
        //     далі. Тому масив росте за потреби, а не падає з IndexError.
        // EN: MaxStackSize is what the function itself needs, but calls with a
        //     variable result count (C=0) and SETLISTO may write beyond it, so
        //     the array grows on demand instead of throwing IndexError.
        var registers = new object?[Math.Max(proto.MaxStackSize + 16, 64)];
        var top = 0;

        for (var i = 0; i < proto.NumParams; i++)
            registers[i] = i < args.Count ? args[i] : null;

        // UA: vararg — Lua 5.0 кладе таблицю "arg" у регістр numparams і додає
        //     поле n. Саме на цю ідіому спирається обгортка CreateHotSpot.
        // EN: vararg — Lua 5.0 puts the "arg" table into register numparams and
        //     adds field n. The CreateHotSpot wrapper relies on this idiom.
        if (proto.IsVararg != 0)
        {
            var extra = new LuaTable();
            var n = 0;
            for (var i = proto.NumParams; i < args.Count; i++) extra.Set((double)(++n), args[i]);
            extra.Set("n", (double)n);
            registers[proto.NumParams] = extra;
        }

        object? ConstantValue(int index)
        {
            var k = constants[index];
            return k.Kind switch
            {
                LuaConstantKind.Nil => null,
                LuaConstantKind.Boolean => k.BooleanValue,
                LuaConstantKind.Number => (double)k.NumberValue,
                LuaConstantKind.String => k.StringValue,
                _ => null,
            };
        }

        object? Rk(int x) => x < Lua50BytecodeReader.MaxStack
            ? registers[x]
            : ConstantValue(x - Lua50BytecodeReader.MaxStack);

        var pc = 0;
        while (true)
        {
            if (++_steps > MaxSteps)
                throw new LuaRuntimeException(
                    "UA: перевищено ліміт кроків — імовірно нескінченний цикл / " +
                    "EN: step limit exceeded — likely an infinite loop");

            var ins = code[pc++];
            var a = ins.A;
            EnsureRegisters(ref registers, a + 8);

            switch (ins.Opcode)
            {
                case LuaOpcode.Move: registers[a] = registers[ins.B!.Value]; break;
                case LuaOpcode.LoadK: registers[a] = ConstantValue(ins.Bx!.Value); break;
                case LuaOpcode.LoadBool:
                    registers[a] = ins.B!.Value != 0;
                    if (ins.C!.Value != 0) pc++;
                    break;
                case LuaOpcode.LoadNil:
                    for (var i = a; i <= ins.B!.Value; i++) registers[i] = null;
                    break;
                case LuaOpcode.GetUpval: registers[a] = closure.Upvalues[ins.B!.Value].Value; break;
                case LuaOpcode.SetUpval: closure.Upvalues[ins.B!.Value].Value = registers[a]; break;
                case LuaOpcode.GetGlobal: registers[a] = Globals.Get(ConstantValue(ins.Bx!.Value)); break;
                case LuaOpcode.SetGlobal: Globals.Set(ConstantValue(ins.Bx!.Value), registers[a]); break;

                case LuaOpcode.GetTable:
                {
                    if (registers[ins.B!.Value] is not LuaTable t)
                        throw new LuaRuntimeException(
                            $"UA: індексація не-таблиці ({Describe(registers[ins.B!.Value])}) / " +
                            $"EN: indexing a non-table ({Describe(registers[ins.B!.Value])})");
                    registers[a] = t.Get(Rk(ins.C!.Value));
                    break;
                }
                case LuaOpcode.SetTable:
                {
                    if (registers[a] is not LuaTable t)
                        throw new LuaRuntimeException(
                            $"UA: запис у не-таблицю ({Describe(registers[a])}) / " +
                            $"EN: writing into a non-table ({Describe(registers[a])})");
                    t.Set(Rk(ins.B!.Value), Rk(ins.C!.Value));
                    break;
                }
                case LuaOpcode.NewTable: registers[a] = new LuaTable(); break;
                case LuaOpcode.Self:
                {
                    var t = registers[ins.B!.Value];
                    registers[a + 1] = t;
                    registers[a] = t is LuaTable lt ? lt.Get(Rk(ins.C!.Value)) : null;
                    break;
                }

                case LuaOpcode.Add or LuaOpcode.Sub or LuaOpcode.Mul or LuaOpcode.Div or LuaOpcode.Pow:
                {
                    var x = ToNumber(Rk(ins.B!.Value));
                    var y = ToNumber(Rk(ins.C!.Value));
                    if (x is null || y is null)
                        throw new LuaRuntimeException(
                            $"UA: арифметика над не-числом у {ins.Opcode} / " +
                            $"EN: arithmetic on a non-number in {ins.Opcode}");
                    registers[a] = ins.Opcode switch
                    {
                        LuaOpcode.Add => x.Value + y.Value,
                        LuaOpcode.Sub => x.Value - y.Value,
                        LuaOpcode.Mul => x.Value * y.Value,
                        LuaOpcode.Div => y.Value != 0 ? x.Value / y.Value : double.PositiveInfinity,
                        _ => Math.Pow(x.Value, y.Value),
                    };
                    break;
                }
                case LuaOpcode.Unm:
                {
                    var x = ToNumber(registers[ins.B!.Value])
                            ?? throw new LuaRuntimeException(
                                "UA: унарний мінус над не-числом / EN: unary minus on a non-number");
                    registers[a] = -x;
                    break;
                }
                case LuaOpcode.Not: registers[a] = !Truthy(registers[ins.B!.Value]); break;
                case LuaOpcode.Concat:
                {
                    var parts = new List<string>();
                    for (var i = ins.B!.Value; i <= ins.C!.Value; i++) parts.Add(Describe(registers[i]));
                    registers[a] = string.Concat(parts);
                    break;
                }

                case LuaOpcode.Jmp: pc += ins.SBx!.Value; break;

                case LuaOpcode.Eq or LuaOpcode.Lt or LuaOpcode.Le:
                {
                    var x = Rk(ins.B!.Value);
                    var y = Rk(ins.C!.Value);
                    bool result;
                    if (ins.Opcode == LuaOpcode.Eq)
                    {
                        result = (x, y) switch
                        {
                            (null, null) => true,
                            (double dx, double dy) => Math.Abs(dx - dy) < double.Epsilon,
                            (string sx, string sy) => sx == sy,
                            (bool bx, bool by) => bx == by,
                            _ => ReferenceEquals(x, y),
                        };
                    }
                    else
                    {
                        var nx = ToNumber(x);
                        var ny = ToNumber(y);
                        if (nx is not null && ny is not null)
                            result = ins.Opcode == LuaOpcode.Lt ? nx < ny : nx <= ny;
                        else if (x is string sx2 && y is string sy2)
                            result = ins.Opcode == LuaOpcode.Lt
                                ? string.CompareOrdinal(sx2, sy2) < 0
                                : string.CompareOrdinal(sx2, sy2) <= 0;
                        else
                            throw new LuaRuntimeException(
                                $"UA: порівняння {Describe(x)} та {Describe(y)} / " +
                                $"EN: comparing {Describe(x)} and {Describe(y)}");
                    }
                    if (result != (a != 0)) pc++;
                    break;
                }

                // UA: ОФІЦІЙНА семантика: перевіряється R(B), A — лише приймач.
                // EN: OFFICIAL semantics: R(B) is tested, A is only the target.
                case LuaOpcode.Test:
                {
                    var rb = registers[ins.B!.Value];
                    if (!Truthy(rb) == (ins.C!.Value != 0)) pc++;
                    else registers[a] = rb;
                    break;
                }

                case LuaOpcode.Call or LuaOpcode.TailCall:
                {
                    var b = ins.B!.Value;
                    var callArgs = new List<object?>();
                    var argEnd = b > 0 ? a + b : top;
                    for (var i = a + 1; i < argEnd; i++) callArgs.Add(registers[i]);

                    var results = Call(registers[a], callArgs);
                    if (ins.Opcode == LuaOpcode.TailCall) return results;

                    var c = ins.C!.Value;
                    if (c == 0)
                    {
                        for (var i = 0; i < results.Count; i++) registers[a + i] = results[i];
                        top = a + results.Count;
                    }
                    else
                    {
                        for (var i = 0; i < c - 1; i++)
                            registers[a + i] = i < results.Count ? results[i] : null;
                    }
                    break;
                }

                case LuaOpcode.Return:
                {
                    var b = ins.B!.Value;
                    var end = b > 0 ? a + b - 1 : top;
                    var res = new List<object?>();
                    for (var i = a; i < end; i++) res.Add(registers[i]);
                    return res;
                }

                // UA: у Lua 5.0 немає OP_FORPREP — підготовка робиться як
                //     init -= step + JMP на цей FORLOOP (підтверджено на
                //     реальному байткоді ifelem_tabmanager_SetPos).
                // EN: Lua 5.0 has no OP_FORPREP — setup is init -= step plus a
                //     JMP to this FORLOOP (confirmed on real bytecode).
                case LuaOpcode.ForLoop:
                {
                    var step = ToNumber(registers[a + 2])!.Value;
                    var idx = ToNumber(registers[a])!.Value + step;
                    var limit = ToNumber(registers[a + 1])!.Value;
                    if (step > 0 ? idx <= limit : idx >= limit)
                    {
                        registers[a] = idx;
                        pc += ins.SBx!.Value;
                    }
                    break;
                }
                case LuaOpcode.TForPrep:
                {
                    if (registers[a] is LuaTable)
                    {
                        registers[a + 1] = registers[a];
                        registers[a] = Globals.Get("next");
                    }
                    pc += ins.SBx!.Value;
                    break;
                }
                case LuaOpcode.TForLoop:
                {
                    // UA: База виклику — A+2, а кількість результатів — C+1
                    //     (НЕ C і НЕ A+C+2, як було раніше). Обидва числа
                    //     доведені на реальному корпусі гри, а не взяті з
                    //     довідника:
                    //       • база A+2: у ifs_mpgs_pclogin root/28 тіло циклу
                    //         читає R(A+2) як КЛЮЧ і R(A+3) як ЗНАЧЕННЯ;
                    //       • кількість C+1: УСІ 164 інструкції TFORLOOP у
                    //         common.lvl + shell.lvl мають C=1, але 91 з них
                    //         читає ДВІ змінні циклу. З одним результатом
                    //         значення лишалось би nil.
                    //     Ціна помилки була не в аварії, а в тихій НЕПОВНОТІ:
                    //     обхід дітей віджета (AddIFObjContainer) зупинявся на
                    //     першому кроці, і замість ~9000 викликів рушія траса
                    //     показувала ~90. Будь-який висновок, зроблений з такої
                    //     траси, був би заниженим у сто разів.
                    // EN: Call base is A+2 and the result count is C+1 (NOT C,
                    //     and NOT A+C+2 as before). Both proven against the real
                    //     game corpus: the loop body of ifs_mpgs_pclogin root/28
                    //     reads R(A+2) as the KEY and R(A+3) as the VALUE; and
                    //     ALL 164 TFORLOOP instructions carry C=1 while 91 of
                    //     them read TWO loop variables. The cost of the old bug
                    //     was not a crash but silent INCOMPLETENESS: the widget
                    //     child walk stopped after one step, so a trace showed
                    //     ~90 engine calls instead of ~9000.
                    var results = ins.C!.Value + 1;
                    var cb = a + 2;
                    EnsureRegisters(ref registers, cb + results + 3);
                    var res = Call(registers[a], new[] { registers[a + 1], registers[a + 2] });
                    for (var i = 0; i < results; i++)
                        registers[cb + i] = i < res.Count ? res[i] : null;
                    if (registers[cb] is null) pc++;
                    break;
                }

                case LuaOpcode.SetList or LuaOpcode.SetListO:
                {
                    // UA: LFIELDS_PER_FLUSH у Lua 5.0 дорівнює 32 (значення 50
                    //     з'явилось лише у 5.1). Помилкове значення змушує
                    //     читати чужі регістри — виявлено прогоном реального
                    //     ifelem_helptext, який падав з виходом за межі масиву.
                    // EN: LFIELDS_PER_FLUSH is 32 in Lua 5.0 (50 only since
                    //     5.1). A wrong value reads foreign registers — found by
                    //     running the real ifelem_helptext, which overran the
                    //     register array.
                    const int fieldsPerFlush = 32;
                    var t = (LuaTable)registers[a]!;
                    var bx = ins.Bx!.Value;
                    var baseIndex = ins.Opcode == LuaOpcode.SetList ? bx / fieldsPerFlush * fieldsPerFlush : 0;
                    var count = ins.Opcode == LuaOpcode.SetList
                        ? bx % fieldsPerFlush + 1
                        : Math.Max(0, top - a - 1);
                    EnsureRegisters(ref registers, a + count);
                    for (var i = 1; i <= count; i++)
                        if (registers[a + i] is not null) t.Set((double)(baseIndex + i), registers[a + i]);
                    break;
                }

                case LuaOpcode.Close: break;

                case LuaOpcode.Closure:
                {
                    var nested = proto.NestedPrototypes[ins.Bx!.Value];
                    var ups = new List<LuaCell>(nested.NumUpvalues);
                    // UA: за форматом після CLOSURE йде рівно nups псевдо-інструкцій
                    //     MOVE/GETUPVAL — вони не виконуються, а описують захоплення.
                    // EN: per the format, CLOSURE is followed by exactly nups
                    //     MOVE/GETUPVAL pseudo-instructions describing the capture.
                    for (var i = 0; i < nested.NumUpvalues; i++)
                    {
                        var pseudo = code[pc++];
                        ups.Add(pseudo.Opcode == LuaOpcode.Move
                            ? new LuaCell(registers[pseudo.B!.Value])
                            : closure.Upvalues[pseudo.B!.Value]);
                    }
                    registers[a] = new LuaClosure(nested, ups);
                    break;
                }

                case LuaOpcode.Vararg: break;

                default:
                    throw new LuaRuntimeException(
                        $"UA: нереалізований опкод {ins.Opcode} / " +
                        $"EN: unimplemented opcode {ins.Opcode}");
            }
        }
    }
}
