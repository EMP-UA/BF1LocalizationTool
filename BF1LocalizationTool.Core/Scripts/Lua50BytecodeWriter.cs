// =============================================================================
// BF1LocalizationTool.Core — Scripts/Lua50BytecodeWriter.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Симетричний "зворотній" бік Lua50BytecodeReader — кодує
//     LuaFunctionPrototype назад у валідний Lua 5.0 dump ("scr_"-чанк
//     BODY), який рушій BF2 може виконати. Формат — ТОЙ САМИЙ, що й у
//     читача (жодних нових припущень): сигнатура "\x1bLua", version=0x50,
//     TEST_NUMBER = float(3.14159265358979323846e7), lua_Number як
//     float32, MAXSTACK=128 для RK-операндів.
//
//     Призначення — НЕ повний компілятор Lua-джерела (тут немає
//     лексера/парсера/кодогенератора довільного .lua тексту). Призначення —
//     дозволити програмно ПОБУДУВАТИ невеликі, наперед сплановані функції
//     (bootstrap-заглушка entry-point, wrapper-обгортки для widescreen-
//     фікса — див. Lua50FunctionBuilder) і серіалізувати їх у той самий
//     байт-формат, що й оригінальні скрипти гри.
//
//     Перевірка коректності: round-trip. Write(Parse(real_bytes).Root)
//     ЗНОВУ розібраний через Parse() має дати структурно ідентичний
//     LuaFunctionPrototype (константи, локальні, upvalue-імена,
//     інструкції, вкладені прототипи) — окрім LoadLines (номери рядків
//     для дебагу свідомо НЕ зберігаються Reader'ом, тому Writer пише 0
//     рядків; це не впливає на виконання скрипту рушієм, лише на
//     потенційний дебаг-вивід помилок із номером рядка).
// EN: The symmetric "reverse" side of Lua50BytecodeReader — encodes a
//     LuaFunctionPrototype back into a valid Lua 5.0 dump (a "scr_" chunk
//     BODY) that the BF2 engine can execute. Format is the EXACT SAME as
//     the reader's (no new assumptions): signature "\x1bLua", version=
//     0x50, TEST_NUMBER = float(3.14159265358979323846e7), lua_Number as
//     float32, MAXSTACK=128 for RK operands.
//
//     Purpose — NOT a full Lua-source compiler (no lexer/parser/codegen
//     for arbitrary .lua text is implemented). Purpose — to allow
//     PROGRAMMATICALLY BUILDING small, pre-planned functions (the
//     bootstrap entry-point stub, widescreen-fix wrapper functions — see
//     Lua50FunctionBuilder) and serializing them into the same byte
//     format the game's own scripts use.
//
//     Correctness check: round-trip. Write(Parse(real_bytes).Root),
//     re-parsed via Parse() again, must produce a structurally identical
//     LuaFunctionPrototype (constants, locals, upvalue names,
//     instructions, nested prototypes) — EXCEPT LoadLines (debug line
//     numbers are deliberately NOT retained by the Reader, so the Writer
//     emits 0 lines; this does not affect script execution by the engine,
//     only a potential debug error message's line number).
// =============================================================================

using System.Text;

namespace BF1LocalizationTool.Core.Scripts;

public static class Lua50BytecodeWriter
{
    // UA: ТІ САМІ 10 однобайтових полів заголовка + TEST_NUMBER, що читає
    //     Lua50BytecodeReader.Parse — не вигадані окремо, а буквально
    //     дзеркало підтвердженого набору значень.
    // EN: The EXACT SAME 10 single-byte header fields + TEST_NUMBER that
    //     Lua50BytecodeReader.Parse reads — not invented separately, a
    //     literal mirror of the confirmed value set.
    private static readonly byte[] HeaderTail =
    [
        0x50, // version
        1,    // endian (little)
        4,    // sizeof(int)
        4,    // sizeof(size_t)
        4,    // sizeof(Instruction)
        6,    // SIZE_OP
        8,    // SIZE_A
        9,    // SIZE_B
        9,    // SIZE_C
        4,    // sizeof(lua_Number) — float, НЕ double / float, NOT double
    ];

    private const float TestNumber = 3.14159265358979323846e7f;

    // -------------------------------------------------------------------------
    // UA: Кодує повний BODY-чанк (заголовок + коренева функція, рекурсивно
    //     з усіма вкладеними прототипами).
    // EN: Encodes a full BODY chunk (header + root function, recursively
    //     with all nested prototypes).
    // -------------------------------------------------------------------------
    public static byte[] Write(LuaFunctionPrototype root)
    {
        var w = new Writer();

        w.Bytes([0x1B, (byte)'L', (byte)'u', (byte)'a']); // сигнатура / signature
        w.Bytes(HeaderTail);
        w.F32(TestNumber);

        WriteFunction(w, root);

        // UA: Хвостовий нульовий байт. ЕМПІРИЧНО: ВСІ 99 із 99 реальних
        //     BODY-чанків BF2 (90 shell.lvl + 9 ingame.lvl), а також усі
        //     чанки з інших реально працюючих модифікацій цієї гри (включно
        //     зі згенерованими стороннім інструментарієм, розміром 809Б і
        //     31618Б) містять РІВНО 1 зайвий байт 0x00 після структурного
        //     кінця Lua-дампа: DataSize BODY завжди на 1 більший за
        //     спожите парсером. Призначення НЕ реверс-інжинирено (офіційний
        //     lundump.c такого байта не пише; LVLTool його просто відрізає),
        //     тому це не твердження, що рушій його ВИМАГАЄ — це лише
        //     дотримання формату, підтвердженого на 100% відомих
        //     працездатних зразків, замість того щоб бути єдиним винятком.
        // EN: A trailing zero byte. EMPIRICAL: ALL 99 of 99 real BF2 BODY
        //     chunks (90 shell.lvl + 9 ingame.lvl), as well as every chunk
        //     from other real, working modifications of this game (including
        //     ones generated by third-party tooling, sized 809B and 31618B) carry
        //     EXACTLY 1 extra 0x00 byte past the structural end of the Lua
        //     dump: a BODY's DataSize is always 1 greater than what the
        //     parser consumes. Its purpose is NOT reverse engineered (the
        //     official lundump.c writes no such byte; LVLTool simply strips
        //     it), so no claim is made that the engine REQUIRES it — the format
        //     confirmed on 100% of known-working samples is simply matched
        //     rather than making this the sole exception to it.
        w.U8(0);

        return w.ToArray();
    }

    // -------------------------------------------------------------------------
    // UA: Один прототип функції (WriteFunction, дзеркало LoadFunction).
    //     Порядок ТОЧНО той самий, що й при читанні: source → lineDefined
    //     → nups → numparams → is_vararg → maxstacksize → lines (0, немає
    //     дебаг-інфи) → locals → upvalues → constants (+ вкладені
    //     прототипи всередині) → code.
    // EN: A single function prototype (WriteFunction, mirrors
    //     LoadFunction). The EXACT SAME order as reading: source →
    //     lineDefined → nups → numparams → is_vararg → maxstacksize →
    //     lines (0, no debug info) → locals → upvalues → constants (+
    //     nested prototypes inside) → code.
    // -------------------------------------------------------------------------
    private static void WriteFunction(Writer w, LuaFunctionPrototype proto)
    {
        WriteString(w, proto.Source);
        w.I32(proto.LineDefined);
        w.U8(proto.NumUpvalues);
        w.U8(proto.NumParams);
        w.U8(proto.IsVararg);
        w.U8(proto.MaxStackSize);

        // UA: LoadLines — свідомо 0 (Reader номери рядків не зберігає,
        //     тому нема що чесно записати назад; на виконання скрипту
        //     рушієм це не впливає).
        // EN: LoadLines — deliberately 0 (the Reader doesn't retain line
        //     numbers, so there's nothing honest to write back; this
        //     doesn't affect script execution by the engine).
        w.I32(0);

        w.I32(proto.LocalVariables.Count);
        foreach (var local in proto.LocalVariables)
        {
            WriteString(w, local.Name);
            w.I32(local.StartPc);
            w.I32(local.EndPc);
        }

        w.I32(proto.Upvalues.Count);
        foreach (var up in proto.Upvalues)
            WriteString(w, up.Name);

        w.I32(proto.Constants.Count);
        foreach (var k in proto.Constants)
        {
            switch (k.Kind)
            {
                case LuaConstantKind.Nil:
                    w.U8(0);
                    break;
                case LuaConstantKind.Boolean:
                    w.U8(1);
                    w.U8((byte)(k.BooleanValue ? 1 : 0));
                    break;
                case LuaConstantKind.Number:
                    w.U8(3);
                    w.F32(k.NumberValue);
                    break;
                case LuaConstantKind.String:
                    w.U8(4);
                    WriteString(w, k.StringValue);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"UA: Невідомий LuaConstantKind={k.Kind} — не можна закодувати / " +
                        $"EN: Unknown LuaConstantKind={k.Kind} — cannot encode");
            }
        }

        w.I32(proto.NestedPrototypes.Count);
        foreach (var nested in proto.NestedPrototypes)
            WriteFunction(w, nested);

        w.I32(proto.Instructions.Count);
        foreach (var instr in proto.Instructions)
            w.U32(EncodeInstruction(instr));
    }

    // -------------------------------------------------------------------------
    // UA: Зворотне до DecodeInstruction (Lua50BytecodeReader) кодування
    //     32-бітного слова. Instr.Bx уже містить "сире" (unsigned) поле
    //     Bx і для iABx, і для iAsBx-опкодів (Reader завжди заповнює й
    //     Bx, і SBx=Bx-MAXARG_sBx одночасно) — тому кодуємо напряму з
    //     Bx, а не перераховуємо з SBx.
    // EN: The reverse of DecodeInstruction (Lua50BytecodeReader) — encodes
    //     the 32-bit word. Instr.Bx already holds the "raw" (unsigned) Bx
    //     field for both iABx AND iAsBx opcodes (the Reader always fills
    //     in both Bx and SBx=Bx-MAXARG_sBx together) — encoding happens
    //     directly from Bx, not by recomputing from SBx.
    // -------------------------------------------------------------------------
    private static uint EncodeInstruction(LuaInstruction instr)
    {
        var op = (uint)instr.Opcode;
        var a = (uint)instr.A;

        if (instr.Bx.HasValue)
        {
            var bx = (uint)instr.Bx.Value;
            return op | (bx << 6) | (a << 24);
        }

        var b = (uint)(instr.B ?? 0);
        var c = (uint)(instr.C ?? 0);
        return op | (c << 6) | (b << 15) | (a << 24);
    }

    // -------------------------------------------------------------------------
    // UA: WriteString — дзеркало LoadString: null → розмір 0 (без байтів
    //     самого рядка); інакше довжина ВКЛЮЧНО з кінцевим '\0' + самі
    //     байти (latin1 — так само byte-в-byte, як і при читанні) + '\0'.
    // EN: WriteString — mirrors LoadString: null → size 0 (no string
    //     bytes); otherwise the length INCLUDING the trailing '\0' + the
    //     bytes themselves (latin1 — byte-for-byte, same as reading) +
    //     '\0'.
    // -------------------------------------------------------------------------
    private static void WriteString(Writer w, string? s)
    {
        if (s is null)
        {
            w.U32(0);
            return;
        }

        var raw = Encoding.Latin1.GetBytes(s);
        w.U32((uint)(raw.Length + 1));
        w.Bytes(raw);
        w.U8(0);
    }

    // -------------------------------------------------------------------------
    // UA: Мінімальний послідовний буфер для запису LE-полів (дзеркало
    //     Cursor у Lua50BytecodeReader).
    // EN: A minimal sequential buffer for writing LE fields (mirrors
    //     Cursor in Lua50BytecodeReader).
    // -------------------------------------------------------------------------
    private sealed class Writer
    {
        private readonly List<byte> _buf = [];

        public void U8(byte b) => _buf.Add(b);

        public void Bytes(byte[] bytes) => _buf.AddRange(bytes);

        public void I32(int v) => _buf.AddRange(BitConverter.GetBytes(v));

        public void U32(uint v) => _buf.AddRange(BitConverter.GetBytes(v));

        public void F32(float v) => _buf.AddRange(BitConverter.GetBytes(v));

        public byte[] ToArray() => [.. _buf];
    }
}
