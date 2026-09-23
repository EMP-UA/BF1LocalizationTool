// =============================================================================
// BF1LocalizationTool.Core — Bf2Exe/MovieSubtitleAlphaScanner.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: ЗАКОННІСТЬ І МЕЖІ. Цей файл ЧИТАЄ (і лише читає) локальну копію
//     BattlefrontII.exe, яку користувач ВЖЕ законно має встановленою у себе
//     на диску (гру придбано; жодна копія .exe не постачається з цим
//     репозиторієм і не завантажується звідкись). Аналіз машинного коду
//     власної, законно придбаної програми заради сумісності/усунення
//     дефекту — це реверс-інжиніринг для інтероперабельності, поширена й
//     широко визнана практика правовласницьких спільнот моддингу. Файл:
//       • НІКОЛИ не відкриває .exe на запис і не змінює жодного байта;
//       • НІКОЛИ не вбудовує в код і не публікує вміст самого .exe —
//         лише ЧИСЛОВІ адреси (VA) й короткі мнемоніки інструкцій
//         (напр. "mov", "call") як факти про власну поведінку гри;
//       • НЕ є патчером — цей проєкт принципово не патчить .exe гри.
//     Якщо .exe не знайдено на диску користувача — файл просто нічого не
//     аналізує (винятком) і на цьому все закінчується.
//
//     МЕХАНІЗМ. Функція малювання тексту (VA 0x006D8150) читає підсумковий
//     байт АЛЬФИ (добуток кольору вузла і кольору БАТЬКА по всьому ланцюгу
//     предків, рахований у 0x006C0DE0) і, якщо він 0, не малює НІЧОГО — ні
//     фону, ні гліфів. Це відповідає симптому дефекту, описаного в
//     docs/BF2_MOVIE_SUBTITLE_FIX.md («підпис зникає ПОВНІСТЮ, а не
//     обрізається»): рушій обнуляє цю альфу залежно від аспекту екрана, а
//     сканер шукає, ДЕ саме це відбувається. Пряме сканування всіх записів
//     у зміщення +0x2C..+0x2F по всьому .text дає 1000+ збігів (це
//     зміщення використовують сотні непов'язаних класів), тому потрібне
//     додаткове звуження.
//
//     ЩО РОБИТЬ ЦЕЙ СКАНЕР (ЕТАП 1 — та сама функція). Звужує пошук до
//     функцій, які РАЗОМ:
//       (a) пишуть у [регістр+0x2C..0x2F] (байт кольору/альфи 2D-вузла —
//           той самий клас об'єктів, що й у movieproperties+0x2D0),
//       (b) І в тій самій функції звертаються до одного з трьох глобалів,
//           пов'язаних з аспектом екрана:
//             0x007F5930  — глобал "ws" (широкоформатний коефіцієнт)
//             0x006B1890  — функція-читач ws (fld [0x7F5930]; ret)
//             0x0093E4A4  — реальна ширина екрана (W)
//             0x0093E4A8  — реальна висота екрана (H)
//     Такий збіг (a)+(b) в ОДНІЙ функції — набагато сильніший кандидат, ніж
//     голе (a), і саме це відсіює шум сліпого сканування.
//
//     ЕТАП 2 доповнює ЕТАП 1 там, де зв'язок з ws/W/H не лежить у тій самій
//     функції, що й запис кольору/альфи, а йде через ланцюжок прямих
//     викликів — рушій нерідко виносить обчислення масштабу в окрему
//     підпрограму або передає вже готове число як параметр сеттеру. ЕТАП 2
//     будує граф прямих викликів (лише `call rel32`, E8) по всьому .text і
//     для кожного кандидата ЕТАПУ 1, який пише +0x2C..0x2F, але БЕЗ прямого
//     звернення до ws/W/H, шукає в ЦЬОМУ графі — в ОБИДВА боки (серед
//     функцій, які кандидат сам ВИКЛИКАЄ, і серед функцій, які ВИКЛИКАЮТЬ
//     кандидата) — найкоротший шлях до функції з прямим зверненням до
//     ws/W/H, не глибше MaxIndirectDepth "кроків". Це ловить два патерни,
//     яких ЕТАП 1 в принципі не бачить:
//       • кандидат сам ВИКЛИКАЄ допоміжну функцію, яка обчислює значення з
//         ws/W/H і повертає його (обчислення винесене в підпрограму);
//       • кандидат — узагальнений "сеттер" (напр. SetColor(this, r,g,b,a)),
//         якого ВИКЛИКАЄ інша функція, що САМА рахує аргумент з ws/W/H і
//         передає вже готове число як параметр.
//     Знайдені так кандидати йдуть в ОКРЕМИЙ третій список
//     (IndirectCandidates), із поясненням ланцюжка викликів — щоб не
//     змішувати їх із прямими кандидатами ЕТАПУ 1, які надійніші.
//
//     ВІДОМІ ОБМЕЖЕННЯ ЕТАПУ 2:
//       • бачить ЛИШЕ прямі виклики (E8 call rel32) — виклики через
//         вказівник/vtable (`call [eax+N]`, звичайні для C++ віртуальних
//         методів у цьому рушії) У ГРАФ НЕ ПОТРАПЛЯЮТЬ. Тобто відсутність
//         шляху в графі — НЕ доказ відсутності зв'язку, якщо зв'язок іде
//         через віртуальний виклик;
//       • межа функції — евристика "назад до int3" — ціль виклику, що не
//         потрапляє точно на знайдену межу, просто не додається як вузол
//         графа (тихо ігнорується, не помилка);
//       • глибина обмежена (MaxIndirectDepth), а "хаби" — функції з
//         аномально великою кількістю викликачів/викликаних (типові
//         спільні утиліти, напр. алокатор) — НЕ розгортаються далі
//         (MaxEdgesToExpand), щоб один хаб не з'їв увесь бюджет пошуку.
//     Тобто ЕТАП 2 — це так само лише звуження кандидатів для РУЧНОЇ
//     перевірки, а не доказ: цей проєкт принципово не патчить .exe гри
//     (див. вище).
//
// EN: LEGALITY AND SCOPE. This file READS (and only reads) the local copy of
//     BattlefrontII.exe that the user ALREADY legally owns and has installed
//     on their own disk (the game is purchased; no copy of the .exe ships
//     with, or is downloaded by, this repository). Analyzing the machine
//     code of one's own, legally owned program for compatibility/defect
//     investigation is interoperability reverse engineering, a common and
//     widely recognised practice in rightsholder-respecting modding
//     communities. This file:
//       • NEVER opens the .exe for writing and never changes a single byte;
//       • NEVER embeds or publishes the .exe's own content — only NUMERIC
//         addresses (VAs) and short instruction mnemonics (e.g. "mov",
//         "call") as facts about the game's own behaviour;
//       • is NOT a patcher — this project does not patch the game's .exe on
//         principle.
//     If the .exe is not found on the user's disk, the file simply analyzes
//     nothing (an exception) and that is the end of it.
//
//     MECHANISM. The text-draw function (VA 0x006D8150) reads the final
//     ALPHA byte (the per-channel product of the node's own color and every
//     ANCESTOR's color, computed in 0x006C0DE0) and, if it is 0, draws
//     NOTHING — neither background nor glyphs. That matches the defect
//     described in docs/BF2_MOVIE_SUBTITLE_FIX.md ("the caption vanishes
//     ENTIRELY, rather than being clipped"): the engine zeroes that alpha
//     depending on screen aspect, and this scanner looks for WHERE exactly
//     that happens. A direct scan of every write to offset +0x2C..+0x2F
//     across all of .text produces 1000+ hits (hundreds of unrelated
//     classes use that offset), so a further narrowing is needed.
//
//     WHAT THIS SCANNER DOES (STAGE 1 — same-function). Narrows the search
//     to functions that BOTH:
//       (a) write to [register+0x2C..0x2F] (the color/alpha byte of a 2D
//           node — the same class of object as movieproperties+0x2D0),
//       (b) AND, in that same function, reference one of the three globals
//           tied to screen aspect:
//             0x007F5930  — the "ws" global (widescreen factor)
//             0x006B1890  — the ws reader function (fld [0x7F5930]; ret)
//             0x0093E4A4  — the real screen width (W)
//             0x0093E4A8  — the real screen height (H)
//     A match on (a)+(b) in the SAME function is a far stronger candidate
//     than (a) alone, and that is exactly what filters out the blind scan's
//     noise.
//
//     STAGE 2 extends STAGE 1 for cases where the ws/W/H connection does not
//     live in the same function as the color/alpha write, but reaches it
//     through a chain of direct calls — the engine often factors the scale
//     computation into a separate subroutine, or passes the already-computed
//     number in as a setter's parameter. STAGE 2 builds a direct-call graph
//     (only `call rel32`, opcode E8) across all of .text, and for every
//     STAGE-1 candidate that writes +0x2C..0x2F but has NO direct ws/W/H
//     reference, searches that graph in BOTH directions — among functions
//     the candidate itself CALLS, and among functions that CALL the
//     candidate — for the shortest path to a function with a direct ws/W/H
//     reference, no deeper than MaxIndirectDepth "hops". This catches two
//     patterns STAGE 1 cannot see by construction:
//       • the candidate itself CALLS a helper that computes a value from
//         ws/W/H and returns it (the computation is factored into a
//         subroutine);
//       • the candidate is a generic "setter" (e.g. SetColor(this,r,g,b,a))
//         CALLED BY another function that computes the argument from
//         ws/W/H itself and passes the already-computed number in.
//     Candidates found this way go into a SEPARATE third list
//     (IndirectCandidates), with the call-chain explained — so they are
//     never mixed with the more reliable STAGE-1 (direct) candidates.
//
//     KNOWN LIMITATIONS OF STAGE 2:
//       • it sees ONLY direct calls (E8 call rel32) — calls through a
//         pointer/vtable (`call [eax+N]`, the norm for C++ virtual methods
//         in this engine) are NOT part of the graph. So the absence of a
//         graph path is NOT proof of no connection if the connection goes
//         through a virtual call;
//       • the function-boundary heuristic is the same "back-to-int3" one —
//         a call target that doesn't land exactly on a detected boundary is
//         simply not added as a graph node (silently skipped, not an
//         error);
//       • depth is capped (MaxIndirectDepth), and "hub" functions with an
//         abnormally large number of callers/callees (typical shared
//         utilities, e.g. an allocator) are NOT expanded further
//         (MaxEdgesToExpand), so one hub can't eat the whole search budget.
//     So STAGE 2 is still only a narrowing of candidates for MANUAL review,
//     not a proof: this project does not patch the game's .exe on principle
//     (see above).
// =============================================================================

using Iced.Intel;

namespace BF1LocalizationTool.Core.Bf2Exe;

// UA: Один результат ЕТАПУ 1: функція, що пише в байт кольору/альфи 2D-вузла.
// EN: One STAGE-1 finding: a function that writes to a 2D node's color/alpha byte.
public sealed record AlphaWriteCandidate(
    ulong FunctionStart,
    ulong InstructionAddress,
    byte ColorByteOffset,      // 0x2C..0x2F
    string InstructionText,    // напр. "or byte [ecx+0x2F], 0xFF" / e.g. "or byte [ecx+0x2F], 0xFF"
    bool ReferencesAspectGlobals,
    IReadOnlyList<string> AspectReferenceDetails); // порожньо, якщо ReferencesAspectGlobals=false

// UA: Один результат ЕТАПУ 2: той самий запис кольору/альфи, але зв'язок з
//     ws/W/H знайдено НЕ в тій самій функції, а через ланцюжок прямих
//     викликів (callee або caller) — дивись PathDescription.
// EN: One STAGE-2 finding: the same color/alpha write, but the ws/W/H
//     connection was found NOT in the same function, but through a chain of
//     direct calls (callee or caller) — see PathDescription.
public sealed record IndirectAlphaWriteCandidate(
    ulong FunctionStart,
    ulong InstructionAddress,
    byte ColorByteOffset,
    string InstructionText,
    IReadOnlyList<string> PathDescription,        // людиночитний ланцюжок / human-readable hop chain
    IReadOnlyList<string> AspectReferenceDetails); // деталі звернення до ws/W/H у КІНЦЕВІЙ функції ланцюжка

public sealed record AlphaScanResult(
    ulong ImageBase,
    ulong TextSectionStartVa,
    ulong TextSectionEndVa,
    int FunctionsScanned,
    IReadOnlyList<AlphaWriteCandidate> PrimaryCandidates,
    IReadOnlyList<IndirectAlphaWriteCandidate> IndirectCandidates,
    IReadOnlyList<AlphaWriteCandidate> SecondaryCandidates);

public static class MovieSubtitleAlphaScanner
{
    // UA: Адреси, підтверджені статичним аналізом .exe (див. МЕХАНІЗМ вище).
    // EN: Addresses confirmed by static analysis of the .exe (see MECHANISM above).
    public const ulong WidescreenFactorGlobalVa = 0x007F5930; // "ws"
    public const ulong WidescreenFactorGetterVa = 0x006B1890; // fld [0x7F5930]; ret
    public const ulong ScreenWidthGlobalVa = 0x0093E4A4;      // реальна W / real W
    public const ulong ScreenHeightGlobalVa = 0x0093E4A8;     // реальна H / real H

    private static readonly HashSet<byte> ColorByteOffsets = [0x2C, 0x2D, 0x2E, 0x2F];

    // UA: Розмір, у межах якого шукається "тіло" однієї функції від її
    //     початку (той самий орієнтир, що й у Python-скретчпаді розслідування:
    //     жодна з реально розібраних тут функцій не була довшою за це).
    // EN: Bound within which a single function's "body" is searched from its
    //     start (the same bound used in the investigation's Python
    //     scratchpad: none of the functions actually disassembled there
    //     was longer than this).
    private const int MaxFunctionBytes = 0x800;

    // UA: Параметри пошуку ЕТАПУ 2 — навмисно консервативні (документовано
    //     вище в заголовку файлу): глибина в "кроках" графа викликів, як
    //     ліміт відвіданих вузлів на ОДИН пошук (запобігає вибуху на
    //     хабах), так і ліміт розгортання ребер одного вузла (сам вузол-хаб
    //     все одно перевіряється на прямий ws/W/H-зв'язок — просто його
    //     сусіди далі не розгортаються).
    // EN: STAGE-2 search parameters — deliberately conservative (documented
    //     above in the file header): depth in call-graph "hops", a cap on
    //     visited nodes per SINGLE search (prevents blowup on hubs), and a
    //     cap on how many edges of one node get expanded (the hub node
    //     itself is still checked for a direct ws/W/H link — only its
    //     neighbours stop being expanded further).
    private const int MaxIndirectDepth = 3;
    private const int MaxVisitedPerSearch = 2000;
    private const int MaxEdgesToExpand = 300;

    public static AlphaScanResult Scan(string exePath)
    {
        if (!File.Exists(exePath))
            throw new FileNotFoundException(
                "UA: BattlefrontII.exe не знайдено — сканер аналізує лише вже наявну на диску " +
                "копію гри, нічого не завантажує. / " +
                "EN: BattlefrontII.exe not found — the scanner only analyzes a copy already on " +
                "disk, it downloads nothing.", exePath);

        // UA: FileAccess.Read явно — цей файл НІКОЛИ не відкриває .exe на запис.
        // EN: FileAccess.Read explicitly — this file NEVER opens the .exe for writing.
        var bytes = File.ReadAllBytes(exePath);

        var (imageBase, textStartOffset, textVa, textSize) = ReadPeTextSection(bytes);
        var textEndVa = textVa + (ulong)textSize;

        // UA: Повна карта функцій (ключ — адреса початку) — потрібна ОБОМ
        //     етапам: ЕТАПУ 1 для (a)+(b)-збігу, ЕТАПУ 2 для графа викликів.
        // EN: The full function map (keyed by start address) — needed by
        //     BOTH stages: STAGE 1 for the (a)+(b) match, STAGE 2 for the
        //     call graph.
        var functions = new Dictionary<ulong, FunctionInfo>();

        // UA: Евристичний пошук меж функції: назад до кінця попереднього
        //     int3-заповнювача (0xCC) від компілятора. Не ідеально, але
        //     достатньо для звуження кандидатів під ручну перевірку.
        // EN: A heuristic search for function boundaries: back to the end
        //     of the previous compiler int3 (0xCC) padding run. Not
        //     perfect, but enough to narrow candidates for manual review.
        for (var i = textStartOffset; i < textStartOffset + textSize - 1; i++)
        {
            if (bytes[i] != 0xCC || bytes[i + 1] == 0xCC)
                continue;

            var functionStartOffset = i + 1;
            var functionStartVa = textVa + (ulong)(functionStartOffset - textStartOffset);
            var len = Math.Min(MaxFunctionBytes, textStartOffset + textSize - functionStartOffset);
            if (len <= 0)
                continue;

            var info = ScanOneFunction(bytes, functionStartOffset, len, functionStartVa);
            functions[functionStartVa] = info;
        }

        // UA: Зворотний індекс графа — "хто викликає цю адресу" — будується
        //     ОДИН раз для всіх функцій одразу (а не окремо на кожен пошук).
        // EN: The reverse graph index — "who calls this address" — built
        //     ONCE for all functions together (not separately per search).
        var callersOf = new Dictionary<ulong, List<ulong>>();
        foreach (var fn in functions.Values)
            foreach (var target in fn.CallTargets)
            {
                if (!callersOf.TryGetValue(target, out var list))
                    callersOf[target] = list = new List<ulong>();
                list.Add(fn.Start);
            }

        var primary = new List<AlphaWriteCandidate>();
        var indirect = new List<IndirectAlphaWriteCandidate>();
        var secondary = new List<AlphaWriteCandidate>();

        // UA: Кеш результату ЕТАПУ 2 на ОДНУ функцію — щоб не повторювати
        //     однаковий BFS-пошук для кожного окремого запису кольору в
        //     тій самій функції (буває кілька записів на функцію, напр.
        //     +0x2C і +0x2E в одній).
        // EN: STAGE-2 result cache per FUNCTION — so the same BFS search
        //     isn't repeated for every individual color write within one
        //     function (a function can hold several writes, e.g. +0x2C
        //     and +0x2E in one).
        var indirectResultCache = new Dictionary<ulong, IndirectSearchResult?>();

        foreach (var fn in functions.Values)
        {
            if (fn.ColorWrites.Count == 0)
                continue;

            foreach (var (address, offset, text) in fn.ColorWrites)
            {
                if (fn.HasDirectAspectRef)
                {
                    primary.Add(new AlphaWriteCandidate(fn.Start, address, offset, text, true, fn.AspectRefDetails));
                    continue;
                }

                if (!indirectResultCache.TryGetValue(fn.Start, out var hop))
                {
                    hop = FindIndirectAspectPath(fn.Start, functions, callersOf);
                    indirectResultCache[fn.Start] = hop;
                }

                if (hop is not null)
                {
                    indirect.Add(new IndirectAlphaWriteCandidate(
                        fn.Start, address, offset, text, hop.PathDescription, hop.AspectDetails));
                }
                else
                {
                    secondary.Add(new AlphaWriteCandidate(fn.Start, address, offset, text, false, []));
                }
            }
        }

        return new AlphaScanResult(imageBase, textVa, textEndVa, functions.Count, primary, indirect, secondary);
    }

    // UA: Внутрішнє (ще не поділене на primary/secondary) звірення однієї
    //     функції — тепер збирає ще й список прямих цілей виклику
    //     (call rel32), потрібний ЕТАПУ 2 для графа.
    // EN: The internal (not yet split into primary/secondary) scan of one
    //     function — now also collects the list of direct call targets
    //     (call rel32), needed by STAGE 2 for the graph.
    private sealed class FunctionInfo
    {
        public required ulong Start;
        public bool HasDirectAspectRef;
        public List<string> AspectRefDetails { get; } = [];
        public List<(ulong Address, byte Offset, string Text)> ColorWrites { get; } = [];
        public List<ulong> CallTargets { get; } = [];
    }

    private sealed record IndirectSearchResult(IReadOnlyList<string> PathDescription, IReadOnlyList<string> AspectDetails);

    private static FunctionInfo ScanOneFunction(byte[] bytes, int fileOffset, int length, ulong functionStartVa)
    {
        var info = new FunctionInfo { Start = functionStartVa };

        var functionBytes = new byte[length];
        Array.Copy(bytes, fileOffset, functionBytes, 0, length);

        var decoder = Decoder.Create(32, new ByteArrayCodeReader(functionBytes));
        decoder.IP = functionStartVa;
        var endIp = functionStartVa + (ulong)length;

        while (decoder.IP < endIp)
        {
            var instr = decoder.Decode();
            if (instr.IsInvalid)
                break;

            // UA: (a) власний запис у байт кольору/альфи: mov/or/and [reg+0x2C..0x2F], ...
            //     Перевіряємо ЛИШЕ операнд-приймач (Op0) і ЛИШЕ форму "[регістр+зміщення]"
            //     (MemoryBase != None) — без базового регістра (пряма адреса) це вже інше
            //     поле, не поле конкретного об'єкта.
            // EN: (a) an own write to the color/alpha byte: mov/or/and [reg+0x2C..0x2F], ...
            //     Only the destination operand (Op0) and ONLY the "[reg+disp]"
            //     form (MemoryBase != None) is checked — without a base register (absolute address)
            //     it is a different kind of field, not a per-object one.
            if ((instr.Mnemonic == Mnemonic.Mov || instr.Mnemonic == Mnemonic.Or || instr.Mnemonic == Mnemonic.And)
                && instr.Op0Kind == OpKind.Memory
                && instr.MemoryBase != Register.None
                && instr.MemoryDisplacement64 <= 0x2F
                && ColorByteOffsets.Contains((byte)instr.MemoryDisplacement64))
            {
                info.ColorWrites.Add((instr.IP, (byte)instr.MemoryDisplacement64, instr.ToString()));
            }

            // UA: (b) звернення до аспект-залежних глобалів — на БУДЬ-ЯКОМУ операнді,
            //     будь-якою інструкцією (не лише запис): пряма адресація [0xVA]
            //     (MemoryBase == None, без регістра) для глобалів-змінних.
            // EN: (b) a reference to the aspect-related globals — on ANY operand, by
            //     ANY instruction (not just a write): absolute addressing [0xVA]
            //     (MemoryBase == None, no register) for the global variables.
            for (var opIdx = 0; opIdx < instr.OpCount; opIdx++)
            {
                if (instr.GetOpKind(opIdx) != OpKind.Memory || instr.MemoryBase != Register.None)
                    continue;

                var disp = instr.MemoryDisplacement64;
                if (disp == WidescreenFactorGlobalVa)
                {
                    info.HasDirectAspectRef = true;
                    info.AspectRefDetails.Add($"[0x{WidescreenFactorGlobalVa:X8}] (глобал ws / ws global) @ 0x{instr.IP:X8}");
                }
                else if (disp == ScreenWidthGlobalVa)
                {
                    info.HasDirectAspectRef = true;
                    info.AspectRefDetails.Add($"[0x{ScreenWidthGlobalVa:X8}] (реальна W / real W) @ 0x{instr.IP:X8}");
                }
                else if (disp == ScreenHeightGlobalVa)
                {
                    info.HasDirectAspectRef = true;
                    info.AspectRefDetails.Add($"[0x{ScreenHeightGlobalVa:X8}] (реальна H / real H) @ 0x{instr.IP:X8}");
                }
                break; // одна інструкція — максимум один операнд пам'яті / one instruction — at most one memory operand
            }

            // UA: Прямий виклик (E8 call rel32) — потрібен і як (b)-збіг (виклик
            //     САМЕ функції-читача ws), і як загальне ребро графа для ЕТАПУ 2
            //     (незалежно від того, куди саме він веде).
            // EN: A direct call (E8 call rel32) — needed both as a (b)-match (a
            //     call to the ws-reader function specifically) and as a generic
            //     graph edge for STAGE 2 (regardless of where it leads).
            if (instr.Mnemonic == Mnemonic.Call && instr.Op0Kind == OpKind.NearBranch32)
            {
                var target = instr.NearBranchTarget;
                info.CallTargets.Add(target);
                if (target == WidescreenFactorGetterVa)
                {
                    info.HasDirectAspectRef = true;
                    info.AspectRefDetails.Add($"call 0x{WidescreenFactorGetterVa:X8} (GetWidescreenFactor/ws) @ 0x{instr.IP:X8}");
                }
            }

            if (instr.Mnemonic == Mnemonic.Ret || instr.Mnemonic == Mnemonic.Retf)
                break;
        }

        return info;
    }

    // UA: ЕТАП 2 — BFS в ОБИДВА боки графа прямих викликів (callee-ребра
    //     "вперед", caller-ребра "назад") від функції-кандидата, у пошуках
    //     НАЙКОРОТШОГО шляху до функції з прямим ws/W/H-зверненням. Повертає
    //     null, якщо в межах MaxIndirectDepth/MaxVisitedPerSearch нічого не
    //     знайдено (це теж корисний результат — див. заголовок файлу).
    // EN: STAGE 2 — BFS in BOTH directions of the direct-call graph (callee
    //     edges "forward", caller edges "backward") from a candidate
    //     function, looking for the SHORTEST path to a function with a
    //     direct ws/W/H reference. Returns null if nothing is found within
    //     MaxIndirectDepth/MaxVisitedPerSearch (that, too, is a useful
    //     result — see the file header).
    private static IndirectSearchResult? FindIndirectAspectPath(
        ulong startFunction,
        Dictionary<ulong, FunctionInfo> functions,
        Dictionary<ulong, List<ulong>> callersOf)
    {
        var visited = new HashSet<ulong> { startFunction };
        var queue = new Queue<(ulong Va, int Depth, List<string> Path)>();
        queue.Enqueue((startFunction, 0, [$"0x{startFunction:X8} (кандидат / candidate)"]));

        while (queue.Count > 0 && visited.Count < MaxVisitedPerSearch)
        {
            var (va, depth, path) = queue.Dequeue();

            if (depth > 0 && functions.TryGetValue(va, out var hitInfo) && hitInfo.HasDirectAspectRef)
                return new IndirectSearchResult(path, hitInfo.AspectRefDetails);

            if (depth >= MaxIndirectDepth)
                continue;

            if (functions.TryGetValue(va, out var selfInfo))
            {
                var callees = selfInfo.CallTargets;
                if (callees.Count <= MaxEdgesToExpand)
                    foreach (var callee in callees)
                        if (visited.Add(callee))
                            queue.Enqueue((callee, depth + 1, [.. path, $"--callee--> 0x{callee:X8}"]));
            }

            if (callersOf.TryGetValue(va, out var callers) && callers.Count <= MaxEdgesToExpand)
                foreach (var caller in callers)
                    if (visited.Add(caller))
                        queue.Enqueue((caller, depth + 1, [.. path, $"--caller--> 0x{caller:X8}"]));
        }

        return null;
    }

    // UA: Мінімальний, лише-для-читання розбір заголовків PE — рівно стільки,
    //     скільки треба, щоб знайти ImageBase і межі секції .text. Свідомо БЕЗ
    //     сторонньої PE-бібліотеки: формат заголовків PE32 незмінний і
    //     публічно задокументований (Microsoft PE/COFF specification), тут
    //     немає нічого специфічного для гри чи EA.
    // EN: A minimal, read-only parse of the PE headers — exactly enough to
    //     find the ImageBase and the .text section bounds. Deliberately
    //     WITHOUT a third-party PE library: the PE32 header layout is fixed
    //     and publicly documented (the Microsoft PE/COFF specification),
    //     nothing here is specific to the game or to EA.
    private static (ulong ImageBase, int TextFileOffset, ulong TextVa, int TextSize) ReadPeTextSection(byte[] bytes)
    {
        var peHeaderOffset = BitConverter.ToInt32(bytes, 0x3C);
        var fileHeaderOffset = peHeaderOffset + 4; // за сигнатурою "PE\0\0" / past the "PE\0\0" signature
        var numberOfSections = BitConverter.ToUInt16(bytes, fileHeaderOffset + 2);
        var sizeOfOptionalHeader = BitConverter.ToUInt16(bytes, fileHeaderOffset + 16);

        var optionalHeaderOffset = fileHeaderOffset + 20;
        var magic = BitConverter.ToUInt16(bytes, optionalHeaderOffset);
        if (magic != 0x10B) // IMAGE_NT_OPTIONAL_HDR32_MAGIC
            throw new NotSupportedException(
                "UA: Це не 32-бітний PE-файл (не PE32) — цей сканер написано саме під " +
                "BattlefrontII.exe (2004), не під інші виконувані файли. / " +
                "EN: Not a 32-bit PE file (not PE32) — this scanner is written specifically " +
                "for BattlefrontII.exe (2004), not for other executables.");

        var imageBase = BitConverter.ToUInt32(bytes, optionalHeaderOffset + 0x1C);

        var sectionHeadersOffset = optionalHeaderOffset + sizeOfOptionalHeader;
        for (var s = 0; s < numberOfSections; s++)
        {
            var sectionOffset = sectionHeadersOffset + s * 40;
            var name = System.Text.Encoding.ASCII.GetString(bytes, sectionOffset, 8).TrimEnd('\0');
            if (name != ".text")
                continue;

            var virtualAddress = BitConverter.ToUInt32(bytes, sectionOffset + 12);
            var sizeOfRawData = BitConverter.ToUInt32(bytes, sectionOffset + 16);
            var pointerToRawData = BitConverter.ToUInt32(bytes, sectionOffset + 20);

            return (imageBase, (int)pointerToRawData, imageBase + virtualAddress, (int)sizeOfRawData);
        }

        throw new InvalidDataException(
            "UA: Секцію .text не знайдено в PE-заголовках — файл не схожий на очікуваний " +
            "BattlefrontII.exe. / " +
            "EN: .text section not found in the PE headers — the file doesn't look like the " +
            "expected BattlefrontII.exe.");
    }
}
