// =============================================================================
// BF1LocalizationTool.Core — Scripts/Lua50FunctionBuilder.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Невеликий "асемблер" для програмної побудови LuaFunctionPrototype
//     вручну — інструкція за інструкцією, з менеджментом пулу констант і
//     patch-механізмом для forward-переходів (JMP на ще не відому pc).
//
//     ЦЕ НЕ компілятор Lua-джерела: тут немає лексера/парсера/довільного
//     кодогенератора — лише low-level будівельні блоки, якими вручну
//     складаються НЕВЕЛИКІ, наперед сплановані функції (bootstrap-
//     заглушка entry-point, майбутні wrapper-обгортки widescreen-фікса).
//     Це свідомий вибір: обсяг задачі — кілька десятків заздалегідь
//     відомих інструкцій, а не довільний Lua-текст, тому повний
//     компілятор був би значно дорожчим і непотрібним рівнем складності.
//
//     Побудовано СИМЕТРИЧНО до Lua50BytecodeReader/Writer: той самий
//     MAXSTACK=128 для RK-кодування (Rk(constIndex) = constIndex +
//     MaxStack), той самий формат LuaInstruction/LuaConstant.
// EN: A small "assembler" for programmatically building a
//     LuaFunctionPrototype by hand — instruction by instruction, with
//     constant-pool management and a patch mechanism for forward jumps
//     (a JMP to a not-yet-known pc).
//
//     THIS IS NOT a Lua-source compiler. No lexer/parser/
//     arbitrary codegen — only low-level building blocks used to hand-
//     assemble SMALL, pre-planned functions (the bootstrap entry-point
//     stub, future widescreen-fix wrapper functions). This is a
//     deliberate choice: the scope here is a few dozen known-in-advance
//     instructions, not arbitrary Lua text, so a full compiler would be a
//     much more expensive, unnecessary level of complexity.
//
//     Built SYMMETRICALLY to Lua50BytecodeReader/Writer: the same
//     MAXSTACK=128 for RK encoding (Rk(constIndex) = constIndex +
//     MaxStack), the same LuaInstruction/LuaConstant shapes.
// =============================================================================

namespace BF1LocalizationTool.Core.Scripts;

public sealed class Lua50FunctionBuilder
{
    private readonly List<LuaConstant> _constants = [];
    private readonly List<LuaFunctionPrototype> _nested = [];
    private readonly List<LuaLocalVariable> _locals = [];
    private readonly List<LuaUpvalue> _upvalues = [];
    private readonly List<LuaInstruction> _instructions = [];

    public string? Source { get; set; }
    public int LineDefined { get; set; }
    public byte NumParams { get; set; }
    public byte IsVararg { get; set; }
    public byte MaxStackSize { get; set; } = 2;

    // -------------------------------------------------------------------------
    // UA: Додає константу в пул (без дедуплікації — дедуплікація не
    //     обов'язкова для коректності, лише для розміру; ці функції
    //     невеликі, це не критично). Повертає ІНДЕКС у Constants (для
    //     GETGLOBAL/SETGLOBAL/LOADK/CLOSURE — прямий Bx-індекс; для
    //     RK-операндів (SETTABLE/ADD/EQ тощо) — обгорнути через Rk(...)).
    // EN: Adds a constant to the pool (NO DEDUP — see the warning below).
    //     Returns the INDEX into Constants (for
    //
    //     WARNING: because there is no dedup, every call appends a new entry.
    //     For functions that address constants through RK this matters: the
    //     C field is 9 bits (0..511) and constants start at MaxStack (128),
    //     so only indices < 383 are RK-addressable. A generator that emits
    //     many constants MUST dedup on its own — measured case: 880 constants
    //     instead of 77 on one generated screen function
    //     (see Bf2Widescreen/AnchorInheritancePatchBuilder.ConstantCache).
    //     GETGLOBAL/SETGLOBAL/LOADK/CLOSURE — a direct Bx index; for RK
    //     operands (SETTABLE/ADD/EQ etc.) — wrap via Rk(...)).
    // -------------------------------------------------------------------------
    public int AddStringConstant(string value)
    {
        _constants.Add(new LuaConstant { Kind = LuaConstantKind.String, ValueOffset = -1, StringValue = value });
        return _constants.Count - 1;
    }

    public int AddNumberConstant(float value)
    {
        _constants.Add(new LuaConstant { Kind = LuaConstantKind.Number, ValueOffset = -1, NumberValue = value });
        return _constants.Count - 1;
    }

    public int AddNilConstant()
    {
        _constants.Add(new LuaConstant { Kind = LuaConstantKind.Nil, ValueOffset = -1 });
        return _constants.Count - 1;
    }

    public int AddBooleanConstant(bool value)
    {
        _constants.Add(new LuaConstant { Kind = LuaConstantKind.Boolean, ValueOffset = -1, BooleanValue = value });
        return _constants.Count - 1;
    }

    // UA: RK-кодування константи для операндів B/C, що можуть бути або
    //     регістром, або константою (SETTABLE/GETTABLE/ADD.../EQ...) —
    //     дзеркало Lua50BytecodeReader.ResolveRK у зворотному напрямку.
    // EN: RK-encodes a constant for B/C operands that may be either a
    //     register or a constant (SETTABLE/GETTABLE/ADD.../EQ...) — the
    //     mirror of Lua50BytecodeReader.ResolveRK in the reverse
    //     direction.
    public static int Rk(int constantIndex) => constantIndex + Lua50BytecodeReader.MaxStack;

    public int AddUpvalue(string name)
    {
        _upvalues.Add(new LuaUpvalue(name));
        return _upvalues.Count - 1;
    }

    public int AddNestedPrototype(LuaFunctionPrototype proto)
    {
        _nested.Add(proto);
        return _nested.Count - 1;
    }

    public void AddLocal(string name, int startPc, int endPc) =>
        _locals.Add(new LuaLocalVariable(name, startPc, endPc));

    // -------------------------------------------------------------------------
    // UA: Емітує iABC-інструкцію (MOVE/GETTABLE/SETTABLE/ADD.../CALL/
    //     RETURN тощо). Повертає pc щойно доданої інструкції — потрібно
    //     для forward-переходів (patch пізніше).
    // EN: Emits an iABC instruction (MOVE/GETTABLE/SETTABLE/ADD.../CALL/
    //     RETURN etc.). Returns the pc of the just-added instruction —
    //     needed for forward jumps (patched later).
    // -------------------------------------------------------------------------
    public int Emit(LuaOpcode opcode, int a, int b = 0, int c = 0)
    {
        var pc = _instructions.Count;
        _instructions.Add(new LuaInstruction { Pc = pc, Opcode = opcode, A = a, B = b, C = c, WordOffset = -1 });
        return pc;
    }

    // UA: Емітує iABx-інструкцію (GETGLOBAL/SETGLOBAL/LOADK/CLOSURE/
    //     SETLIST/SETLISTO) — Bx прямий (беззнаковий) індекс.
    // EN: Emits an iABx instruction (GETGLOBAL/SETGLOBAL/LOADK/CLOSURE/
    //     SETLIST/SETLISTO) — Bx is a direct (unsigned) index.
    public int EmitABx(LuaOpcode opcode, int a, int bx)
    {
        var pc = _instructions.Count;
        _instructions.Add(new LuaInstruction
        {
            Pc = pc, Opcode = opcode, A = a, Bx = bx, SBx = bx - MaxArgSBx, WordOffset = -1,
        });
        return pc;
    }

    // UA: Емітує iAsBx-інструкцію (JMP/FORLOOP/TFORPREP) з ВЖЕ ВІДОМИМ
    //     відносним зміщенням sbx. Для forward-переходів (ціль ще не
    //     відома) — див. EmitJumpPlaceholder/PatchJump нижче.
    // EN: Emits an iAsBx instruction (JMP/FORLOOP/TFORPREP) with an
    //     ALREADY KNOWN relative offset sbx. For forward jumps (target
    //     not yet known) — see EmitJumpPlaceholder/PatchJump below.
    public int EmitAsBx(LuaOpcode opcode, int a, int sbx)
    {
        var pc = _instructions.Count;
        var bx = sbx + MaxArgSBx;
        _instructions.Add(new LuaInstruction { Pc = pc, Opcode = opcode, A = a, Bx = bx, SBx = sbx, WordOffset = -1 });
        return pc;
    }

    // UA: Заглушка для forward JMP (ціль невідома в момент емісії) —
    //     обов'язково викликати PatchJump(pc, targetPc) перед Build().
    // EN: A placeholder for a forward JMP (target unknown at emission
    //     time) — PatchJump(pc, targetPc) MUST be called before Build().
    public int EmitJumpPlaceholder(int a = 0) => EmitAsBx(LuaOpcode.Jmp, a, sbx: 0);

    // -------------------------------------------------------------------------
    // UA: !!! КРИТИЧНО !!! Емітує TEST з ПРАВИЛЬНИМ операндом.
    //
    //     Офіційна семантика Lua 5.0 (lopcodes.h / lvm.c):
    //         OP_TEST  A B C:  if (l_isfalse(R(B)) == C) then pc++
    //                          else { R(A) := R(B); <виконати наступний JMP> }
    //     Тобто ПЕРЕВІРЯЄТЬСЯ РЕГІСТР **B**, а A — це лише РЕГІСТР-ПРИЙМАЧ
    //     (куди копіюється значення в "else"-гілці). Це відрізняється від
    //     Lua 5.1+, де TEST має форму "A C" без B — звідси й помилка.
    //
    //     ПОМИЛКОВИЙ ВАРІАНТ, якого слід уникати: Emit(LuaOpcode.Test,
    //     a: N, c: 0) без явного B — тоді B лишається 0 за замовчуванням,
    //     і замість поля таблиці перевіряється регістр R(0) (сама
    //     таблиця-аргумент), який ЗАВЖДИ truthy → умовний блок виконується
    //     БЕЗУМОВНО → арифметика над nil, коли поля x/y/width/height
    //     немає → помилка Lua-рантайму на першому ж виклику
    //     NewIFContainer → ЧОРНИЙ ЕКРАН замість меню.
    //
    //     ЕМПІРИЧНО ПІДТВЕРДЖЕНО на реальному байткоді: з 1702 інструкцій
    //     TEST у vanilla shell.lvl 1671 (98,2%) мають A == B, і ЖОДНА не
    //     має B=0 при A!=0. Той самий патерн (TEST A=n B=n C=0) — у
    //     реально працюючому сторонньому wrapper-скрипті (interface_fixes
    //     root/9 pc 60, root/13 pc 10/39/56/79).
    //
    //     Тому цей хелпер НЕ приймає A і B окремо: A=B=register — єдина
    //     форма, потрібна для ідіоми "if <вираз> then ... end", і саме
    //     так її генерує офіційний компілятор Lua 5.0.
    //         c: 0 → "if register then <блок> end"
    //         c: 1 → "if not register then <блок> end"
    //     Одразу після TEST має йти JMP на кінець блоку.
    // EN: !!! CRITICAL !!! Emits TEST with the CORRECT operand.
    //
    //     Official Lua 5.0 semantics (lopcodes.h / lvm.c):
    //         OP_TEST  A B C:  if (l_isfalse(R(B)) == C) then pc++
    //                          else { R(A) := R(B); <run the next JMP> }
    //     That is, the register being TESTED is **B**; A is merely the
    //     DESTINATION register (where the value is copied in the "else"
    //     branch). This differs from Lua 5.1+, where TEST is "A C" with no
    //     B — which is exactly where the mistake came from.
    //
    //     THE MISTAKE TO AVOID: Emit(LuaOpcode.Test, a: N, c: 0) without
    //     an explicit B leaves B at its default of 0 — instead of the
    //     table field, register R(0) (the argument table itself) gets
    //     tested, and it is ALWAYS truthy — so the conditional block runs
    //     UNCONDITIONALLY → arithmetic on nil whenever x/y/width/height is
    //     absent → a Lua runtime error on the very first NewIFContainer
    //     call → BLACK SCREEN instead of the menu.
    //
    //     EMPIRICALLY CONFIRMED against real bytecode: of 1702 TEST
    //     instructions in vanilla shell.lvl, 1671 (98.2%) have A == B, and
    //     NONE has B=0 while A!=0. The same pattern (TEST A=n B=n C=0)
    //     appears in an actually-working third-party wrapper script
    //     (interface_fixes root/9 pc 60, root/13 pc 10/39/56/79).
    //
    //     Hence this helper does NOT take A and B separately: A=B=register
    //     is the only form needed for the "if <expr> then ... end" idiom,
    //     and it's exactly what the official Lua 5.0 compiler emits.
    //         c: 0 → "if register then <block> end"
    //         c: 1 → "if not register then <block> end"
    //     A JMP to the end of the block must immediately follow the TEST.
    // -------------------------------------------------------------------------
    public int EmitTest(int register, int c = 0) =>
        Emit(LuaOpcode.Test, a: register, b: register, c: c);

    // UA: Патчить раніше емітований JMP/FORLOOP/TFORPREP на реальну
    //     цільову pc. sBx = targetPc − (jmpPc + 1) — офіційна формула
    //     відносного переходу (PC вже інкрементовано на момент виконання
    //     переходу, тому "+1").
    // EN: Patches a previously emitted JMP/FORLOOP/TFORPREP to a real
    //     target pc. sBx = targetPc − (jmpPc + 1) — the official relative
    //     jump formula (PC is already incremented by the time the jump
    //     executes, hence the "+1").
    public void PatchJump(int jmpPc, int targetPc)
    {
        var sbx = targetPc - (jmpPc + 1);
        var bx = sbx + MaxArgSBx;
        _instructions[jmpPc] = _instructions[jmpPc] with { Bx = bx, SBx = sbx };
    }

    // UA: Поточна кількість уже емітованих інструкцій — зручно як
    //     "майбутня pc" перед емісією наступної (напр. для PatchJump).
    // EN: The current count of already-emitted instructions — handy as
    //     the "future pc" before emitting the next one (e.g. for
    //     PatchJump).
    public int NextPc => _instructions.Count;

    private const int MaxArgSBx = ((1 << 18) - 1) >> 1;

    // -------------------------------------------------------------------------
    // UA: Завершує побудову й повертає готовий LuaFunctionPrototype,
    //     придатний для Lua50BytecodeWriter.Write.
    // EN: Finalizes the build and returns a ready LuaFunctionPrototype,
    //     suitable for Lua50BytecodeWriter.Write.
    // -------------------------------------------------------------------------
    public LuaFunctionPrototype Build(string path = "root", int depth = 0) => new()
    {
        Source = Source,
        LineDefined = LineDefined,
        NumUpvalues = (byte)_upvalues.Count,
        NumParams = NumParams,
        IsVararg = IsVararg,
        MaxStackSize = MaxStackSize,
        Constants = _constants,
        NestedPrototypes = _nested,
        LocalVariables = _locals,
        Upvalues = _upvalues,
        Instructions = _instructions,
        SizeCode = _instructions.Count,
        Depth = depth,
        Path = path,
    };
}
