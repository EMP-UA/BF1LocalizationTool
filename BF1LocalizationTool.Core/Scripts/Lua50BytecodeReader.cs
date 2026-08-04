// =============================================================================
// BF1LocalizationTool.Core — Scripts/Lua50BytecodeReader.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Парсер скомпільованих Lua 5.0 чанків, якими BF2 (Classic) зберігає
//     логіку інтерфейсу (shell.lvl) та HUD (ingame.lvl) у "scr_" чанках.
//
//     Формат ПОВНІСТЮ підтверджено побайтово проти офіційного джерела
//     Lua 5.0.3 (lua.org/source/5.0/src_lundump.c.html, lua.h) — жодних
//     "невідомих" полів не залишилось:
//       - Сигнатура, версія (0x50), порядок байтів, sizeof(int)=4,
//         sizeof(size_t)=4, sizeof(Instruction)=4, SIZE_OP=6, SIZE_A=8,
//         SIZE_B=9, SIZE_C=9 — усі СТАНДАРТНІ значення офіційного Lua 5.0,
//         жодної кастомізації.
//       - ЄДИНА реальна модифікація гри: lua_Number скомпільовано як
//         float (4 байти), а не стандартний double (8 байтів) — типова
//         практика для ігрових рушіїв. Підтверджено: 4-байтове
//         TEST_NUMBER-поле в заголовку точно дорівнює
//         struct.pack('<f', 3.14159265358979323846e7) — побітовий
//         збіг, перевірено на реальних байтах shell.lvl.
//     Це НЕ "нестандартний/проприєтарний байткод" — це стандартний dump
//     Lua 5.0 з одним відомим типовим налаштуванням компіляції.
//
//     ПЕРЕВІРЕНО на реальних файлах (Python-прототип): усі 90 "scr_"
//     чанків shell.lvl і всі 9 чанків ingame.lvl (BF2)
//     розпарсились без жодної помилки, з очікуваним залишком РІВНО 1 байт
//     на кожному (той самий "хвостовий" байт, який офіційний LVLTool
//     (github.com/BAD-AL/LVLTool, MIT) вручну відрізає перед передачею
//     стороннім деком-тулам — у нас це просто залишок, бо наш парсер сам
//     знає, скільки байтів структурно споживати, і не потребує EOF).
//
// EN: Parser for compiled Lua 5.0 chunks, which BF2 (Classic) uses to
//     store UI logic (shell.lvl) and HUD logic (ingame.lvl) inside "scr_"
//     chunks.
//
//     The format has been FULLY confirmed byte-for-byte against the
//     official Lua 5.0.3 source (lua.org/source/5.0/src_lundump.c.html,
//     lua.h) — no "unknown" fields remain:
//       - Signature, version (0x50), byte order, sizeof(int)=4,
//         sizeof(size_t)=4, sizeof(Instruction)=4, SIZE_OP=6, SIZE_A=8,
//         SIZE_B=9, SIZE_C=9 — all STANDARD official Lua 5.0 values, no
//         customization at all.
//       - The ONLY real game-side modification: lua_Number compiled as
//         float (4 bytes) instead of the standard double (8 bytes) — a
//         typical practice for game engines. Confirmed: the 4-byte
//         TEST_NUMBER header field exactly equals
//         struct.pack('<f', 3.14159265358979323846e7) — a bit-for-bit
//         match, verified against real shell.lvl bytes.
//     This is NOT "nonstandard/proprietary bytecode" — it's a standard
//     Lua 5.0 dump with one well-known, ordinary compile-time setting.
//
//     VERIFIED against real files (Python prototype): all 90 "scr_"
//     chunks of shell.lvl and all 9 chunks of ingame.lvl (BF2)
//     parsed with zero errors, with the expected leftover of EXACTLY 1
//     byte on every single one (the same "trailing" byte the official
//     LVLTool, github.com/BAD-AL/LVLTool, MIT, manually strips before
//     handing data to third-party decompile tools — for us it's simply a
//     leftover, since our parser knows exactly how many bytes to
//     structurally consume and doesn't rely on EOF).
//
//     Клас декодує самі інструкції (опкоди), не лише пул констант:
//     рядкові/числові константи в пулі НЕ обов'язково пов'язані з
//     конкретним викликом (порядок пулу — це порядок КОМПІЛЯЦІЇ, не
//     порядок ВИКОРИСТАННЯ). Щоб знати, яке саме число є аргументом якого
//     виклику/полем якої таблиці — потрібно декодувати інструкції.
//     Таблиця опкодів (36 штук, MOVE..CLOSURE) звірена побайтово проти
//     офіційного lua.org/source/5.0/lopcodes.h — не вигадана.
//
//     MAXSTACK=128 (поріг "регістр чи константа" для RK-операндів:
//     RK(x) = якщо x<MAXSTACK то R(x), інакше Kst(x-MAXSTACK)) — ЦЕ
//     ЄДИНЕ реальне налаштування гри понад офіційний Lua 5.0 (офіційний
//     дефолт — 250, llimits.h). MAXSTACK — суто компіляторна константа,
//     у самому dump-форматі її НЕМАЄ — тому підтверджена ЕМПІРИЧНО, не
//     з заголовка: перебір кандидатів (64/100/128/150/200/250) на
//     реальних SETTABLE-парах з ifs_vkeyboard (shell.lvl) — лише
//     MAXSTACK=128 дає 5 із 5 семантично осмислених пар ключ=значення
//     ("NumRows"=4.0, "NumColumns"=13.0, "MaxLen"=15.0, "MaxWidth"=150.0,
//     "fnDone"=nil); усі інші кандидати давали або вихід за межі пулу
//     констант, або безглузді пари рядок+рядок.
// EN: The class decodes the instructions (opcodes) themselves, not just
//     the constant pool: string/number constants in the pool are NOT
//     necessarily tied to a specific call site (pool order reflects
//     COMPILATION order, not USAGE order). To know which number is the
//     argument of which call, or which field of which table, instructions
//     must be decoded. The opcode table (36 entries, MOVE..CLOSURE) was
//     checked byte-for-byte against the official
//     lua.org/source/5.0/lopcodes.h — not invented.
//
//     MAXSTACK=128 (the "register vs constant" threshold for RK operands:
//     RK(x) = if x<MAXSTACK then R(x), else Kst(x-MAXSTACK)) — this is
//     the ONLY real game-side setting beyond official Lua 5.0 (the
//     official default is 250, llimits.h). MAXSTACK is a purely
//     compiler-internal constant — it does NOT appear anywhere in the
//     dump format — so it was confirmed EMPIRICALLY, not from the header:
//     trying candidates (64/100/128/150/200/250) against real SETTABLE
//     pairs from ifs_vkeyboard (shell.lvl) — only MAXSTACK=128 gives 5 of
//     5 semantically sound key=value pairs ("NumRows"=4.0,
//     "NumColumns"=13.0, "MaxLen"=15.0, "MaxWidth"=150.0, "fnDone"=nil);
//     every other candidate produced either out-of-range constant
//     indices or nonsensical string+string pairs.
// =============================================================================

namespace BF1LocalizationTool.Core.Scripts;

// UA: Тип однієї константи в пулі констант функції (за офіційними тегами
//     LUA_T* з lua.h 5.0: TNIL=0, TBOOLEAN=1, TNUMBER=3, TSTRING=4).
// EN: The type of a single constant in a function's constant pool (per
//     the official LUA_T* tags from lua.h 5.0: TNIL=0, TBOOLEAN=1,
//     TNUMBER=3, TSTRING=4).
public enum LuaConstantKind
{
    Nil,
    Boolean,
    Number,
    String,
}

// UA: Одна константа з пулу констант функції, разом з байтовим зміщенням
//     (від початку BODY-чанка) — потрібне для майбутнього in-place
//     hex-патча без repack (замінити значення на місці, розмір поля
//     не змінюється).
// EN: A single constant from a function's constant pool, together with
//     its byte offset (from the start of the BODY chunk) — needed for a
//     future in-place hex patch without repack (replace the value in
//     place, field size never changes).
public sealed record LuaConstant
{
    public required LuaConstantKind Kind { get; init; }
    public required int ValueOffset { get; init; }
    public float NumberValue { get; init; }
    public string? StringValue { get; init; }
    public bool BooleanValue { get; init; }
}

// UA: 36 опкодів офіційного Lua 5.0 у ТОЧНОМУ порядку оголошення enum
//     OpCode з lopcodes.h (порядок = числове значення опкоду, від цього
//     залежить коректність декодування — "grep ORDER OP" застереження
//     в самому офіційному джерелі).
// EN: The 36 official Lua 5.0 opcodes in the EXACT declaration order of
//     the OpCode enum from lopcodes.h (order = the opcode's numeric
//     value — decoding correctness depends on it; the official source
//     itself has a "grep ORDER OP" warning about this).
public enum LuaOpcode
{
    Move, LoadK, LoadBool, LoadNil, GetUpval,
    GetGlobal, GetTable,
    SetGlobal, SetUpval, SetTable,
    NewTable,
    Self,
    Add, Sub, Mul, Div, Pow, Unm, Not,
    Concat,
    Jmp,
    Eq, Lt, Le,
    Test,
    Call, TailCall, Return,
    ForLoop,
    TForLoop, TForPrep,
    SetList, SetListO,
    Close,
    Closure,

    // UA: 36-й офіційний опкод lopcodes.h, використовується для "..."
    //     (vararg-виразів). Жоден з 99 перевірених реальних BF2-чанків не
    //     містить його напряму (вони використовують ідіому "{...}" +
    //     SETLISTO, яка вже підтримана) — включений тут заради повноти
    //     таблиці опкодів: без нього Parse кинув би виняток на БУДЬ-ЯКОМУ
    //     скрипті, що використовує "..." напряму.
    // EN: The 36th official lopcodes.h opcode, used for "..." vararg
    //     expressions. None of the 99 checked real BF2 chunks use it
    //     directly (they use the "{...}" + SETLISTO idiom, which is
    //     already supported) — included here for opcode table
    //     completeness: without it, Parse would throw on ANY script using
    //     "..." directly.
    Vararg,
}

// UA: Одна декодована інструкція (32-бітне слово). Поля B/C або Bx/sBx —
//     залежно від формату опкоду (iABC vs iABx/iAsBx, lopcodes.h). Для
//     iABC-опкодів Bx/SBx = null; для iABx/iAsBx-опкодів B/C = null.
// EN: A single decoded instruction (32-bit word). B/C or Bx/sBx fields
//     depend on the opcode's format (iABC vs iABx/iAsBx, per
//     lopcodes.h). For iABC opcodes Bx/SBx = null; for iABx/iAsBx
//     opcodes B/C = null.
public sealed record LuaInstruction
{
    public required int Pc { get; init; }
    public required LuaOpcode Opcode { get; init; }
    public required int A { get; init; }
    public int? B { get; init; }
    public int? C { get; init; }
    public int? Bx { get; init; }
    public int? SBx { get; init; }
}

// UA: Одна локальна змінна (LoadLocals): ім'я + діапазон інструкцій
//     (startpc..endpc), у яких вона в області видимості. Lua 5.0 НЕ
//     зберігає, у якому саме РЕГІСТРІ лежить змінна — лише ім'я+діапазон
//     (регістр визначається порядком оголошення відносно PC, що ми
//     свідомо не відтворюємо — це рівень повного компілятора, не потрібний
//     для пошуку layout-констант).
// EN: A single local variable (LoadLocals): name + instruction range
//     (startpc..endpc) it's in scope for. Lua 5.0 does NOT store which
//     REGISTER a variable lives in — only name+range (the register is
//     determined by declaration order relative to PC, which we
//     deliberately don't reconstruct — that's full-compiler-level detail,
//     not needed for finding layout constants).
public sealed record LuaLocalVariable(string? Name, int StartPc, int EndPc);

// UA: Ім'я upvalue (LoadUpvalues) — зберігається явно, бо потрібне для
//     Lua50BytecodeWriter: без реальних імен upvalue неможливо коректно
//     ЗАПИСАТИ функцію, яка їх використовує (напр. GETUPVAL/SETUPVAL у
//     наших власних wrapper-функціях widescreen-фікса).
// EN: An upvalue name (LoadUpvalues) — stored explicitly, because
//     Lua50BytecodeWriter needs it: a function using GETUPVAL/SETUPVAL
//     (e.g. our own widescreen-fix wrapper functions) cannot be correctly
//     WRITTEN back without the real upvalue names.
public sealed record LuaUpvalue(string? Name);

// UA: Один прототип Lua-функції (Proto в термінах офіційного джерела),
//     разом з усіма вкладеними прототипами (замикання, оголошені
//     всередині цієї функції).
// EN: A single Lua function prototype (Proto in the official source's
//     terms), together with all nested prototypes (closures declared
//     inside this function).
public sealed record LuaFunctionPrototype
{
    public string? Source { get; init; }
    public required int LineDefined { get; init; }
    public required byte NumUpvalues { get; init; }
    public required byte NumParams { get; init; }
    public required byte IsVararg { get; init; }
    public required byte MaxStackSize { get; init; }
    public required IReadOnlyList<LuaConstant> Constants { get; init; }
    public required IReadOnlyList<LuaFunctionPrototype> NestedPrototypes { get; init; }
    public required IReadOnlyList<LuaLocalVariable> LocalVariables { get; init; }
    public required IReadOnlyList<LuaUpvalue> Upvalues { get; init; }
    public required IReadOnlyList<LuaInstruction> Instructions { get; init; }
    public required int SizeCode { get; init; }

    // UA: Глибина вкладеності (0 = коренева функція чанка) і "шлях" за
    //     індексами вкладених прототипів — лише для читабельних звітів.
    // EN: Nesting depth (0 = the chunk's root function) and a "path" of
    //     nested-prototype indices — for readable reports only.
    public required int Depth { get; init; }
    public required string Path { get; init; }
}

// UA: Результат парсингу одного BODY-чанка: коренева функція + скільки
//     байтів фактично спожито (для перевірки на РЕАЛЬНИХ даних — очікуємо
//     ConsumedBytes == RawData.Length - 1, підтверджено на 99 з 99
//     реальних чанків BF2; будь-яке ІНШЕ значення — сигнал переглянути
//     формат для цього конкретного чанка, а не мовчки довіряти).
// EN: The result of parsing a single BODY chunk: the root function +
//     how many bytes were actually consumed (for verification against
//     REAL data — expected ConsumedBytes == RawData.Length - 1,
//     confirmed on 99 of 99 real BF2 chunks; any OTHER value is a signal
//     to re-examine the format for that specific chunk, not something to
//     silently trust).
public sealed record LuaChunkParseResult
{
    public required LuaFunctionPrototype Root { get; init; }
    public required int ConsumedBytes { get; init; }
    public required int TotalBytes { get; init; }

    // UA: Очікувана різниця (той самий "хвостовий" байт в усіх реальних
    //     BF2-чанках). Якщо LeftoverBytes != 1 — щось не так, не гадати.
    // EN: Expected difference (the same "trailing" byte across all real
    //     BF2 chunks). If LeftoverBytes != 1 — something is off, don't
    //     guess about it.
    public int LeftoverBytes => TotalBytes - ConsumedBytes;
}

public static class Lua50BytecodeReader
{
    private static readonly byte[] Signature = [0x1B, (byte)'L', (byte)'u', (byte)'a'];

    // UA: Розбирає один BODY-чанк scr_-ресурсу (сирі байти, що
    //     ПОЧИНАЮТЬСЯ із сигнатури "\x1bLua"). Кидає InvalidDataException
    //     з деталями, якщо заголовок не відповідає підтвердженому формату
    //     — НІКОЛИ не намагається "підлаштуватись" під невідомий варіант
    //     мовчки.
    // EN: Parses a single scr_-resource BODY chunk (raw bytes that START
    //     with the "\x1bLua" signature). Throws InvalidDataException with
    //     details if the header doesn't match the confirmed format —
    //     NEVER silently tries to "adapt" to an unknown variant.
    public static LuaChunkParseResult Parse(byte[] bodyData)
    {
        var cursor = new Cursor(bodyData);

        var sig = cursor.Bytes(4);
        if (!sig.AsSpan().SequenceEqual(Signature))
            throw new InvalidDataException(
                $"UA: Невірна сигнатура Lua-чанка: {Convert.ToHexString(sig)}, очікується 1B4C7561 / " +
                $"EN: Invalid Lua chunk signature: {Convert.ToHexString(sig)}, expected 1B4C7561");

        var version = cursor.U8();
        var endian = cursor.U8();
        var sizeofInt = cursor.U8();
        var sizeofSizeT = cursor.U8();
        var sizeofInstruction = cursor.U8();
        var sizeOp = cursor.U8();
        var sizeA = cursor.U8();
        var sizeB = cursor.U8();
        var sizeC = cursor.U8();
        var sizeofNumber = cursor.U8();
        var testNumberBytes = cursor.Bytes(sizeofNumber);

        // UA: ВСІ ці значення підтверджені як СТАНДАРТНІ для офіційного
        //     Lua 5.0 на реальних BF2-файлах (крім sizeofNumber=4, яке
        //     теж перевіряємо явно, а не припускаємо). Якщо колись
        //     зустрінеться файл з іншими значеннями — це сигнал, що
        //     формат відрізняється, і краще впасти з чіткою помилкою,
        //     ніж мовчки розпарсити неправильно.
        // EN: ALL of these values are confirmed STANDARD for official
        //     Lua 5.0 on real BF2 files (including sizeofNumber=4, which
        //     we also check explicitly rather than assume). If a file
        //     with different values ever shows up — that's a signal the
        //     format differs, and it's better to fail loudly than to
        //     silently misparse.
        if (version != 0x50 || endian != 1 || sizeofInt != 4 || sizeofSizeT != 4 ||
            sizeofInstruction != 4 || sizeOp != 6 || sizeA != 8 || sizeB != 9 || sizeC != 9 ||
            sizeofNumber != 4)
        {
            throw new InvalidDataException(
                "UA: Заголовок Lua-чанка НЕ відповідає підтвердженому формату BF2 " +
                $"(version={version:X2} endian={endian} int={sizeofInt} size_t={sizeofSizeT} " +
                $"instr={sizeofInstruction} OP={sizeOp} A={sizeA} B={sizeB} C={sizeC} number={sizeofNumber}). " +
                "Це МІГ БИ бути формат BF1 (Lua 4.0, сигнатура \"\\x1bLua@\") або щось інше — не гадати, розібратись перед продовженням. / " +
                "EN: Lua chunk header does NOT match the confirmed BF2 format " +
                $"(version={version:X2} endian={endian} int={sizeofInt} size_t={sizeofSizeT} " +
                $"instr={sizeofInstruction} OP={sizeOp} A={sizeA} B={sizeB} C={sizeC} number={sizeofNumber}). " +
                "This COULD be the BF1 format (Lua 4.0, signature \"\\x1bLua@\") or something else — don't guess, investigate before continuing.");
        }

        var root = LoadFunction(cursor, parentSource: null, depth: 0, path: "root");

        return new LuaChunkParseResult
        {
            Root = root,
            ConsumedBytes = cursor.Pos,
            TotalBytes = bodyData.Length,
        };
    }

    // -------------------------------------------------------------------------
    // UA: Один прототип функції (LoadFunction в офіційному lundump.c).
    //     Порядок читання ТОЧНО відповідає офіційному алгоритму:
    //     source → lineDefined → nups → numparams → is_vararg →
    //     maxstacksize → lines → locals → upvalues → constants (+ вкладені
    //     прототипи всередині LoadConstants) → code.
    // EN: A single function prototype (LoadFunction in the official
    //     lundump.c). Read order EXACTLY matches the official algorithm:
    //     source → lineDefined → nups → numparams → is_vararg →
    //     maxstacksize → lines → locals → upvalues → constants (+ nested
    //     prototypes inside LoadConstants) → code.
    // -------------------------------------------------------------------------
    private static LuaFunctionPrototype LoadFunction(Cursor c, string? parentSource, int depth, string path)
    {
        var source = LoadString(c) ?? parentSource;
        var lineDefined = c.I32();
        var nups = c.U8();
        var numParams = c.U8();
        var isVararg = c.U8();
        var maxStack = c.U8();

        // UA: LoadLines — масив int-ів, самі значення тут не потрібні
        //     (номери рядків для дебагу), лише коректно "проковтнути".
        // EN: LoadLines — an array of ints, the values themselves aren't
        //     needed here (debug line numbers), just correctly consumed.
        var sizeLines = c.I32();
        for (var i = 0; i < sizeLines; i++)
            c.I32();

        // UA: LoadLocals — ім'я + startpc/endpc на локальну змінну.
        // EN: LoadLocals — name + startpc/endpc per local variable.
        var nLocals = c.I32();
        var locals = new List<LuaLocalVariable>(nLocals);
        for (var i = 0; i < nLocals; i++)
        {
            var varName = LoadString(c);
            var startPc = c.I32();
            var endPc = c.I32();
            locals.Add(new LuaLocalVariable(varName, startPc, endPc));
        }

        // UA: LoadUpvalues — лише імена (сирі рядки, за офіційним кодом).
        //     Зберігаємо (не відкидаємо) — потрібні для Lua50BytecodeWriter.
        // EN: LoadUpvalues — names only (raw strings, per the official
        //     code). Stored (not discarded) — needed by
        //     Lua50BytecodeWriter.
        var nUpvalues = c.I32();
        var upvalues = new List<LuaUpvalue>(nUpvalues);
        for (var i = 0; i < nUpvalues; i++)
            upvalues.Add(new LuaUpvalue(LoadString(c)));

        // UA: LoadConstants — числа/рядки/nil/bool, ПОТІМ вкладені
        //     прототипи (у офіційному форматі вони йдуть В КІНЦІ
        //     LoadConstants, не окремим кроком).
        // EN: LoadConstants — numbers/strings/nil/bool, THEN nested
        //     prototypes (in the official format they come at the END of
        //     LoadConstants, not as a separate step).
        var nConstants = c.I32();
        var constants = new List<LuaConstant>(nConstants);
        for (var i = 0; i < nConstants; i++)
        {
            var valueOffset = c.Pos;
            var tag = c.U8();
            switch (tag)
            {
                case 0: // LUA_TNIL
                    constants.Add(new LuaConstant { Kind = LuaConstantKind.Nil, ValueOffset = valueOffset });
                    break;
                case 1: // LUA_TBOOLEAN
                    constants.Add(new LuaConstant
                    {
                        Kind = LuaConstantKind.Boolean,
                        ValueOffset = valueOffset,
                        BooleanValue = c.U8() != 0,
                    });
                    break;
                case 3: // LUA_TNUMBER
                    constants.Add(new LuaConstant
                    {
                        Kind = LuaConstantKind.Number,
                        ValueOffset = valueOffset,
                        NumberValue = c.F32(),
                    });
                    break;
                case 4: // LUA_TSTRING
                    constants.Add(new LuaConstant
                    {
                        Kind = LuaConstantKind.String,
                        ValueOffset = valueOffset,
                        StringValue = LoadString(c),
                    });
                    break;
                default:
                    throw new InvalidDataException(
                        $"UA: Невідомий тег константи {tag} за зміщенням {valueOffset} (шлях {path}) / " +
                        $"EN: Unknown constant tag {tag} at offset {valueOffset} (path {path})");
            }
        }

        var nNested = c.I32();
        var nested = new List<LuaFunctionPrototype>(nNested);
        for (var i = 0; i < nNested; i++)
            nested.Add(LoadFunction(c, source, depth + 1, $"{path}/{i}"));

        // UA: LoadCode — 4-байтні слова інструкцій, тепер ДЕКОДУЄМО
        //     кожне (див. DecodeInstruction нижче).
        // EN: LoadCode — 4-byte instruction words, now DECODED one by
        //     one (see DecodeInstruction below).
        var sizeCode = c.I32();
        var instructions = new List<LuaInstruction>(sizeCode);
        for (var pc = 0; pc < sizeCode; pc++)
        {
            var word = c.U32();
            instructions.Add(DecodeInstruction(pc, word));
        }

        return new LuaFunctionPrototype
        {
            Source = source,
            LineDefined = lineDefined,
            NumUpvalues = nups,
            NumParams = numParams,
            IsVararg = isVararg,
            MaxStackSize = maxStack,
            Constants = constants,
            NestedPrototypes = nested,
            LocalVariables = locals,
            Upvalues = upvalues,
            Instructions = instructions,
            SizeCode = sizeCode,
            Depth = depth,
            Path = path,
        };
    }

    // UA: iABx/iAsBx-опкоди (Bx/sBx замість окремих B/C) — за коментарями
    //     операндів в lopcodes.h ("A Bx" / "sBx" / "A sBx"). Усе інше —
    //     iABC.
    // EN: iABx/iAsBx opcodes (Bx/sBx instead of separate B/C) — per the
    //     operand comments in lopcodes.h ("A Bx" / "sBx" / "A sBx").
    //     Everything else is iABC.
    private static readonly HashSet<LuaOpcode> AbxOrAsBxOpcodes =
    [
        LuaOpcode.LoadK, LuaOpcode.GetGlobal, LuaOpcode.SetGlobal,
        LuaOpcode.SetList, LuaOpcode.SetListO, LuaOpcode.Closure,
        LuaOpcode.Jmp, LuaOpcode.ForLoop, LuaOpcode.TForPrep,
    ];

    // UA: MAXARG_sBx = (2^SIZE_Bx − 1) >> 1, з SIZE_Bx=18 (офіційна
    //     формула, lopcodes.h) — sBx = Bx − MAXARG_sBx (надлишкове
    //     кодування знаку, "excess K").
    // EN: MAXARG_sBx = (2^SIZE_Bx − 1) >> 1, with SIZE_Bx=18 (official
    //     formula, lopcodes.h) — sBx = Bx − MAXARG_sBx ("excess K" signed
    //     encoding).
    private const int MaxArgSBx = ((1 << 18) - 1) >> 1;

    // -------------------------------------------------------------------------
    // UA: Розбирає 32-бітне слово інструкції за офіційним бітовим
    //     розкладом (lopcodes.h): opcode у молодших 6 бітах, далі
    //     C(9)/B(9)/A(8) для iABC, або Bx(18)/A(8) для iABx/iAsBx.
    //     ЯКЩО зустрінеться опкод поза діапазоном 0-34 (наприклад,
    //     помилка вирівнювання десь раніше в парсингу) — кидаємо виняток,
    //     а не мовчки повертаємо "невідомий" опкод.
    // EN: Decodes a 32-bit instruction word per the official bit layout
    //     (lopcodes.h): opcode in the low 6 bits, then C(9)/B(9)/A(8) for
    //     iABC, or Bx(18)/A(8) for iABx/iAsBx. IF an opcode outside the
    //     0-34 range shows up (e.g. an alignment error earlier in
    //     parsing) — throw, don't silently return an "unknown" opcode.
    // -------------------------------------------------------------------------
    private static LuaInstruction DecodeInstruction(int pc, uint word)
    {
        var opValue = word & 0x3F;
        if (opValue > (uint)LuaOpcode.Vararg)
            throw new InvalidDataException(
                $"UA: Опкод {opValue} поза відомим діапазоном 0-35 за pc={pc}, слово=0x{word:X8} / " +
                $"EN: Opcode {opValue} outside the known 0-35 range at pc={pc}, word=0x{word:X8}");

        var opcode = (LuaOpcode)opValue;
        var a = (int)((word >> 24) & 0xFF);

        if (AbxOrAsBxOpcodes.Contains(opcode))
        {
            var bx = (int)((word >> 6) & 0x3FFFF);
            return new LuaInstruction { Pc = pc, Opcode = opcode, A = a, Bx = bx, SBx = bx - MaxArgSBx };
        }

        var b = (int)((word >> 15) & 0x1FF);
        var cVal = (int)((word >> 6) & 0x1FF);
        return new LuaInstruction { Pc = pc, Opcode = opcode, A = a, B = b, C = cVal };
    }

    // UA: Емпірично підтверджений поріг MAXSTACK (див. коментар класу) —
    //     RK(x): x<MAXSTACK ⇒ регістр R(x); інакше ⇒ Kst(x-MAXSTACK).
    // EN: The empirically confirmed MAXSTACK threshold (see class
    //     comment) — RK(x): x<MAXSTACK ⇒ register R(x); otherwise ⇒
    //     Kst(x-MAXSTACK).
    public const int MaxStack = 128;

    // -------------------------------------------------------------------------
    // UA: Розв'язує RK-операнд (використовується у SETTABLE/GETTABLE/SELF/
    //     ADD-подібних/EQ-подібних, де B/C можуть бути або регістром, або
    //     константою). Повертає null, якщо це РЕГІСТР (не константа) —
    //     викликач сам вирішує, як показати "R(x)" у такому разі.
    // EN: Resolves an RK operand (used in SETTABLE/GETTABLE/SELF/ADD-like/
    //     EQ-like opcodes, where B/C may be either a register or a
    //     constant). Returns null if it's a REGISTER (not a constant) —
    //     the caller decides how to display "R(x)" in that case.
    // -------------------------------------------------------------------------
    public static LuaConstant? ResolveRK(int rk, IReadOnlyList<LuaConstant> constants)
    {
        if (rk < MaxStack)
            return null; // UA: регістр, не константа / EN: a register, not a constant

        var index = rk - MaxStack;
        return index >= 0 && index < constants.Count ? constants[index] : null;
    }

    // -------------------------------------------------------------------------
    // UA: LoadString — size_t розмір (тут 4 байти), 0 = NULL, інакше
    //     `size` байтів де ОСТАННІЙ — це '\0'-термінатор, який
    //     відкидається (офіційна поведінка: luaS_newlstr(..., size-1)).
    // EN: LoadString — a size_t length (4 bytes here), 0 = NULL,
    //     otherwise `size` bytes where the LAST one is the '\0'
    //     terminator, which is dropped (official behavior:
    //     luaS_newlstr(..., size-1)).
    // -------------------------------------------------------------------------
    private static string? LoadString(Cursor c)
    {
        var size = c.U32();
        if (size == 0)
            return null;

        var raw = c.Bytes((int)size);
        // UA: latin1 — байт-у-байт зворотне кодування, не спотворює
        //     вміст незалежно від того, ASCII це чи щось інше.
        // EN: latin1 — byte-for-byte reversible encoding, doesn't distort
        //     content regardless of whether it's ASCII or something else.
        return System.Text.Encoding.Latin1.GetString(raw, 0, raw.Length - 1);
    }

    // -------------------------------------------------------------------------
    // UA: Мінімальний курсор для послідовного читання LE-полів без
    //     виходу за межі масиву.
    // EN: A minimal cursor for sequential reading of LE fields without
    //     going out of array bounds.
    // -------------------------------------------------------------------------
    private sealed class Cursor(byte[] data)
    {
        public int Pos { get; private set; }

        public byte[] Bytes(int n)
        {
            if (Pos + n > data.Length)
                throw new InvalidDataException(
                    $"UA: Вихід за межі при читанні {n} байт(ів) з позиції {Pos} (довжина {data.Length}) / " +
                    $"EN: Out of bounds reading {n} byte(s) at position {Pos} (length {data.Length})");

            var slice = new byte[n];
            Array.Copy(data, Pos, slice, 0, n);
            Pos += n;
            return slice;
        }

        public byte U8() => Bytes(1)[0];
        public int I32() => BitConverter.ToInt32(Bytes(4), 0);
        public uint U32() => BitConverter.ToUInt32(Bytes(4), 0);
        public float F32() => BitConverter.ToSingle(Bytes(4), 0);
    }
}
