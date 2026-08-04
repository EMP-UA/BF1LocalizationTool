// =============================================================================
// BF1LocalizationTool.Core — Scripts/LuaDisassembler.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Читабельний дизасемблер-лістинг одного Lua50BytecodeReader-прототипу
//     (аналог "luac -l" стандартного дистрибутиву Lua) — з підстановкою
//     РЕАЛЬНИХ імен/значень замість "сирих" індексів там, де це можна
//     зробити напевно за офіційним форматом (не гадаючи):
//       - GETGLOBAL/SETGLOBAL/LOADK: Bx — ПРЯМИЙ індекс у пул констант
//         (Kst(Bx)) — офіційно завжди константа, без RK-порогу.
//       - CLOSURE: Bx — індекс у список ВКЛАДЕНИХ прототипів (KPROTO[Bx]),
//         НЕ пул констант — інша таблиця.
//       - GETTABLE(C)/SETTABLE(B,C)/SELF(C)/ADD..POW(B,C)/EQ..LE(B,C):
//         RK-операнди — Lua50BytecodeReader.ResolveRK (поріг MAXSTACK=128,
//         підтверджений емпірично, див. коментар Lua50BytecodeReader).
//     Мета — дати змогу вручну прочитати логіку конкретної функції
//     (напр. побудову екрана меню з ScreenRelativeX/Y) без потреби
//     виконувати сторонній декомпілятор.
// EN: A readable disassembly listing of a single Lua50BytecodeReader
//     prototype (equivalent to stock Lua's "luac -l") — substituting REAL
//     names/values for "raw" indices wherever that can be done with
//     certainty per the official format (no guessing):
//       - GETGLOBAL/SETGLOBAL/LOADK: Bx is a DIRECT index into the
//         constant pool (Kst(Bx)) — officially always a constant, no
//         RK threshold involved.
//       - CLOSURE: Bx is an index into the list of NESTED prototypes
//         (KPROTO[Bx]), NOT the constant pool — a different table.
//       - GETTABLE(C)/SETTABLE(B,C)/SELF(C)/ADD..POW(B,C)/EQ..LE(B,C):
//         RK operands — Lua50BytecodeReader.ResolveRK (MAXSTACK=128
//         threshold, empirically confirmed, see Lua50BytecodeReader's
//         comment).
//     Goal — let a human manually read a specific function's logic (e.g.
//     building a menu screen with ScreenRelativeX/Y) without needing to
//     run a third-party decompiler.
// =============================================================================

namespace BF1LocalizationTool.Core.Scripts;

public static class LuaDisassembler
{
    // -------------------------------------------------------------------------
    // UA: Формує повний лістинг прототипу + усіх вкладених (рекурсивно),
    //     кожен рядок — один PC. labelPrefix — довільний текст на початку
    //     кожного функціонального заголовка (напр. ім'я scr_-ресурсу).
    // EN: Builds a full listing of a prototype + all nested ones
    //     (recursively), one PC per line. labelPrefix — arbitrary text at
    //     the start of every function header (e.g. the scr_ resource
    //     name).
    // -------------------------------------------------------------------------
    public static void Disassemble(LuaFunctionPrototype proto, string labelPrefix, List<string> output, int indent = 0)
    {
        var pad = new string(' ', indent * 2);
        output.Add($"{pad}{labelPrefix} [{proto.Path}] params={proto.NumParams} vararg={proto.IsVararg} " +
                   $"maxstack={proto.MaxStackSize} nlocals={proto.LocalVariables.Count} nconstants={proto.Constants.Count}");

        foreach (var local in proto.LocalVariables)
            output.Add($"{pad}  .local \"{local.Name}\" pc=[{local.StartPc}..{local.EndPc}]");

        foreach (var instr in proto.Instructions)
            output.Add($"{pad}  {FormatInstruction(instr, proto)}");

        foreach (var nested in proto.NestedPrototypes)
            Disassemble(nested, labelPrefix, output, indent + 1);
    }

    // -------------------------------------------------------------------------
    private static string FormatInstruction(LuaInstruction i, LuaFunctionPrototype proto)
    {
        var opName = i.Opcode.ToString().ToUpperInvariant();
        var line = $"[{i.Pc,4}] {opName,-10}";

        switch (i.Opcode)
        {
            case LuaOpcode.GetGlobal:
            case LuaOpcode.SetGlobal:
            case LuaOpcode.LoadK:
            {
                var k = i.Bx.HasValue && i.Bx.Value >= 0 && i.Bx.Value < proto.Constants.Count
                    ? proto.Constants[i.Bx.Value]
                    : null;
                return $"{line} A={i.A} Bx={i.Bx}  {FormatConstant(k)}";
            }

            case LuaOpcode.Closure:
            {
                var p = i.Bx.HasValue && i.Bx.Value >= 0 && i.Bx.Value < proto.NestedPrototypes.Count
                    ? proto.NestedPrototypes[i.Bx.Value]
                    : null;
                return $"{line} A={i.A} Bx={i.Bx}  ; proto {p?.Path ?? "?"}";
            }

            // UA: RK-операнди — за офіційними коментарями операндів у
            //     lopcodes.h ("RK(x)" явно в описі). Для GETTABLE/SELF
            //     (нижче) лише один операнд RK (C); тут — обидва (B і C).
            // EN: RK operands — per the official operand comments in
            //     lopcodes.h ("RK(x)" explicitly in the description). For
            //     GETTABLE/SELF (below) only one operand is RK (C); here
            //     — both (B and C).
            case LuaOpcode.SetTable:
            case LuaOpcode.Add:
            case LuaOpcode.Sub:
            case LuaOpcode.Mul:
            case LuaOpcode.Div:
            case LuaOpcode.Pow:
            case LuaOpcode.Eq:
            case LuaOpcode.Lt:
            case LuaOpcode.Le:
            {
                var bK = i.B.HasValue ? Lua50BytecodeReader.ResolveRK(i.B.Value, proto.Constants) : null;
                var cK = i.C.HasValue ? Lua50BytecodeReader.ResolveRK(i.C.Value, proto.Constants) : null;
                return $"{line} A={i.A} B={i.B}{RkSuffix(bK)} C={i.C}{RkSuffix(cK)}";
            }

            case LuaOpcode.GetTable:
            case LuaOpcode.Self:
            {
                var cK = i.C.HasValue ? Lua50BytecodeReader.ResolveRK(i.C.Value, proto.Constants) : null;
                return $"{line} A={i.A} B={i.B} C={i.C}{RkSuffix(cK)}";
            }

            case LuaOpcode.Jmp:
            case LuaOpcode.ForLoop:
            case LuaOpcode.TForPrep:
                return $"{line} A={i.A} sBx={i.SBx}  ; -> pc={i.Pc + 1 + (i.SBx ?? 0)}";

            case LuaOpcode.Call:
            case LuaOpcode.TailCall:
                return $"{line} A={i.A} B={i.B} C={i.C}  ; R({i.A})(R({i.A + 1})..R({i.A + (i.B ?? 1) - 1}))";

            default:
                return i.Bx.HasValue
                    ? $"{line} A={i.A} Bx={i.Bx} sBx={i.SBx}"
                    : $"{line} A={i.A} B={i.B} C={i.C}";
        }
    }

    private static string RkSuffix(LuaConstant? k) => k is null ? "" : $"({FormatConstant(k)})";

    private static string FormatConstant(LuaConstant? k) => k?.Kind switch
    {
        LuaConstantKind.String => $"\"{k.StringValue}\"",
        LuaConstantKind.Number => k.NumberValue.ToString("G9"),
        LuaConstantKind.Boolean => k.BooleanValue.ToString(),
        LuaConstantKind.Nil => "nil",
        _ => "?",
    };
}
