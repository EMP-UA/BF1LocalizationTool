// =============================================================================
// BF1LocalizationTool.Core — Scripts/Bf2ScriptHost.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Мінімальне «оточення рушія» для Lua50Interpreter: завантажує "scr_"-скрипти
//     з .lvl-файлів, реєструє стандартну бібліотеку Lua та заглушки нативних
//     ScriptCB_*-функцій, і ЗАПИСУЄ всі геометричні виклики.
//
//     ЩО ЦЕ ДАЄ. Дозволяє відповісти на питання «з якими саме числами скрипт
//     звертається до рушія» БЕЗ запуску гри. Геометрія інтерфейсу BF2 не є
//     статичними даними — вона обчислюється в рантаймі, тому прочитати її з
//     байткоду очима неможливо. Прогін через хост дає фактичні аргументи.
//
//     ЧОМУ ЦЕ ВАЖЛИВО ДЛЯ МАЙБУТНІХ ЗМІН. Якщо колись зміниться шрифт
//     (ScriptCB_GetFontHeight повертає інше число) або з'явиться інша
//     роздільність — розкладка поїде, і причину доведеться шукати знову.
//     Замість здогадок достатньо змінити FontHeight/ScreenWidth/ScreenHeight
//     тут і порівняти дві траси: до і після. Різниця й буде відповіддю.
//
//     ЗНАЙДЕНО САМЕ ЦИМ ІНСТРУМЕНТОМ: нативні функції геометрії
//     мають ДВІ форми виклику — з хендлом об'єкта і без нього:
//         ScriptCB_IFObj_SetPos(50, -25, 0)      -- 3 аргументи, БЕЗ хендла
//         ScriptCB_IFObj_SetPos(cp, 50, -25, 0)  -- 4 аргументи, з хендлом
//     Коротка форма використовується всередині блоку AddIF*…EndIFObj, де
//     «поточний об'єкт» неявний. Обгортки, написані з припущенням «перший
//     аргумент завжди хендл», масштабували НЕ ТІ аргументи — зокрема множили
//     координату y на ГОРИЗОНТАЛЬНИЙ коефіцієнт. Виявити це статично
//     неможливо.
//
//     МЕЖІ ЧЕСНОСТІ. Це не емулятор гри. Заглушки повертають правдоподібні, але
//     ВИГАДАНІ значення (висота шрифту, розмір тексту). Тому абсолютні числа
//     треба звіряти з грою; надійними є ПОРІВНЯННЯ двох трас, зроблених за
//     однакових заглушок (vanilla проти патченого файлу).
//
// EN: A minimal "engine environment" for Lua50Interpreter: loads "scr_" scripts
//     from .lvl files, registers the Lua standard library and stubs for native
//     ScriptCB_* functions, and RECORDS every geometry call.
//
//     It answers "which numbers does the script actually pass to the engine"
//     WITHOUT launching the game. BF2 interface geometry is not static data —
//     it is computed at runtime, so it cannot be read off the bytecode by eye.
//
//     DISCOVERED WITH THIS TOOL: geometry natives have TWO calling
//     conventions, with and without the object handle. Wrappers written on the
//     assumption "the first argument is always the handle" scaled the WRONG
//     arguments — multiplying the y coordinate by the HORIZONTAL factor, among
//     others. This is not discoverable statically.
//
//     HONEST LIMITS. Not a game emulator. Stubs return plausible but INVENTED
//     values (font height, text extent). Absolute numbers must be checked
//     against the game; what is reliable is COMPARING two traces taken with
//     identical stubs (vanilla vs a patched file).
// =============================================================================

using System.Text;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Core.Scripts;

// UA: Один зафіксований виклик рушія: ім'я + фактичні аргументи.
// EN: One recorded engine call: name + the actual arguments.
public sealed record NativeCall(string Name, IReadOnlyList<object?> Arguments)
{
    public override string ToString() =>
        $"{Name}({string.Join(", ", Arguments.Select(Lua50Interpreter.Describe))})";
}

public sealed class Bf2ScriptHost
{
    // UA: Нативні функції, чиї аргументи є ДОВЖИНАМИ. Саме їх треба масштабувати
    //     при widescreen-фіксі, і саме їх записує патч. Перелік отримано
    //     скануванням усіх 481 нативної ScriptCB_* у shell/common/ingame/mission.
    // EN: Natives whose arguments are LENGTHS — the ones a widescreen fix must
    //     scale, and the ones the patch records. Obtained by scanning all 481 native
    //     ScriptCB_* calls across shell/common/ingame/mission.
    public static readonly string[] GeometryNatives =
    [
        "ScriptCB_IFObj_SetPos",
        "ScriptCB_IFObj_SetScreenPosition",
        "ScriptCB_IFObj_CreateHotSpot",
        "ScriptCB_IFImage_SetRect",
        "ScriptCB_IFBorder_SetRect",
        "ScriptCB_IFText_SetTextBox",
        "ScriptCB_IFText_SetLeading",
        "ScriptCB_IFFlashyText_SetBackgroundSize",
        "ScriptCB_IFModel_SetTranslation",
    ];

    private readonly Dictionary<string, byte[]> _scripts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _executed = new(StringComparer.Ordinal);

    public Lua50Interpreter Interpreter { get; } = new();
    public List<NativeCall> RecordedCalls { get; } = [];
    public List<string> MissingScripts { get; } = [];

    // UA: Параметри «рушія», якими можна керувати з тесту. Саме їх варто
    //     міняти, щоб побачити вплив іншої роздільності чи іншого шрифту.
    // EN: "Engine" parameters the test can control. Change these to observe the
    //     effect of a different resolution or a different font.
    public double ScreenWidth { get; init; } = 800;
    public double ScreenHeight { get; init; } = 600;
    public double FontHeight { get; init; } = 20;

    public Bf2ScriptHost() => RegisterEnvironment();

    // -------------------------------------------------------------------------
    // UA: Завантажує всі "scr_"-скрипти з .lvl у таблицю імен (не виконує їх).
    // EN: Loads all "scr_" scripts from a .lvl into the name table (no execution).
    // -------------------------------------------------------------------------
    public void LoadLevel(string lvlPath)
    {
        var root = UcfbReader.ReadFile(lvlPath);
        foreach (var script in ScriptChunkLocator.FindAll(root))
            _scripts[script.Name] = script.BodyChunk.RawData;
    }

    public IReadOnlyCollection<string> ScriptNames => _scripts.Keys;

    // -------------------------------------------------------------------------
    // UA: Виконує скрипт за іменем (як ScriptCB_DoFile у грі). Повторний виклик
    //     ігнорується — гра теж не перезавантажує вже виконані скрипти без потреби.
    // EN: Executes a script by name (like the game's ScriptCB_DoFile). Repeated
    //     calls are ignored.
    // -------------------------------------------------------------------------
    public void Execute(string name)
    {
        if (!_executed.Add(name)) return;
        if (!_scripts.TryGetValue(name, out var body))
        {
            MissingScripts.Add(name);
            return;
        }

        var proto = Lua50BytecodeReader.Parse(body).Root;
        Interpreter.Call(new LuaClosure(proto, []), []);
    }

    public object? GetGlobal(string name) => Interpreter.Globals.Get(name);

    // -------------------------------------------------------------------------
    // UA: Як Execute, але НЕ блокує майбутні спроби, якщо ця впала з помилкою.
    //     Execute позначає скрипт "виконаним" ще ДО спроби запустити його —
    //     це навмисно, щоб узаємні ScriptCB_DoFile не зациклились. Але це ж
    //     означає, що ПОВТОРНИЙ Execute(name) після невдачі — це тихий no-op:
    //     він не кидає виняток і нічого не виконує, тож будь-який код, що
    //     сподівається "спробувати ще раз наступного проходу", насправді
    //     нічого не робить і хибно виглядає як "більше не падає". TryExecute
    //     знімає позначку одразу після невдалої спроби, тож наступний виклик
    //     (наприклад, коли скрипт, від якого цей залежав, уже виконано) є
    //     СПРАВЖНЬОЮ повторною спробою.
    // EN: Like Execute, but does NOT permanently block future attempts when
    //     this one fails. Execute marks a script "executed" BEFORE attempting
    //     it — deliberately, so mutual ScriptCB_DoFile calls cannot recurse
    //     forever. But that also means a REPEATED Execute(name) after a
    //     failure is a silent no-op: it throws nothing and runs nothing, so
    //     any code hoping to "try again next pass" actually does nothing and
    //     looks like it stopped failing. TryExecute clears the mark right
    //     after a failed attempt, so the next call (e.g. once a script this
    //     one depended on has since run) is a GENUINE retry.
    // -------------------------------------------------------------------------
    public bool TryExecute(string name, out string? error)
    {
        try
        {
            Execute(name);
            error = null;
            return true;
        }
        // UA: навмисно широкий catch (не лише LuaRuntimeException) — тут
        //     ДЕСЯТКИ довільних скриптів прогоняються best-effort; крім помилок
        //     виконання Lua, малоймовірний, але можливий і збій розбору
        //     байткоду (InvalidDataException) чи внутрішня хиба інтерпретатора
        //     на несподіваній формі даних. Жодна з них не повинна зупиняти
        //     решту прогону.
        // EN: deliberately broad catch (not just LuaRuntimeException) — this
        //     runs DOZENS of arbitrary scripts best-effort; besides Lua
        //     runtime errors, a bytecode-parse failure (InvalidDataException)
        //     or an interpreter-internal fault on unexpected data shape is
        //     unlikely but possible. None of them should stop the rest of
        //     the run.
        catch (Exception ex)
        {
            _executed.Remove(name);
            error = ex.Message;
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // UA: Реєструє заглушку для КОЖНОЇ нативної функції, згаданої у завантажених
    //     скриптах, якої ще немає серед глобалів. Без цього виконання впало б на
    //     першому ж невідомому виклику. Заглушка повертає nil — цього досить,
    //     бо тут важлива лише геометрія, а не логіка меню.
    // EN: Registers a stub for EVERY native mentioned in the loaded scripts that
    //     is not already a global. Without this, execution would fail on the
    //     first unknown call. Stubs return nil, which suffices because only
    //     geometry matters here, not menu logic.
    // -------------------------------------------------------------------------
    public int StubUnknownNatives()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var body in _scripts.Values)
        {
            LuaChunkParseResult parsed;
            try { parsed = Lua50BytecodeReader.Parse(body); }
            catch (InvalidDataException) { continue; }
            CollectNativeNames(parsed.Root, names);
        }

        var added = 0;
        foreach (var name in names)
        {
            if (Interpreter.Globals.Get(name) is not null) continue;
            Interpreter.Globals.Set(name, MakeRecordingStub(name));
            added++;
        }
        return added;
    }

    private static void CollectNativeNames(LuaFunctionPrototype proto, HashSet<string> into)
    {
        foreach (var k in proto.Constants)
            if (k.Kind == LuaConstantKind.String && k.StringValue is { } s &&
                s.StartsWith("ScriptCB_", StringComparison.Ordinal))
                into.Add(s);

        foreach (var nested in proto.NestedPrototypes)
            CollectNativeNames(nested, into);
    }

    private LuaNative MakeRecordingStub(string name)
    {
        var isGeometry = GeometryNatives.Contains(name);
        return args =>
        {
            if (isGeometry) RecordedCalls.Add(new NativeCall(name, args.ToList()));
            return [];
        };
    }

    // -------------------------------------------------------------------------
    private void RegisterEnvironment()
    {
        var g = Interpreter.Globals;

        void Native(string name, LuaNative fn) => g.Set(name, fn);
        static object?[] One(object? v) => [v];

        // --- стандартна бібліотека / standard library --------------------------
        Native("print", _ => []);
        Native("tostring", a => One(Lua50Interpreter.Describe(a.Count > 0 ? a[0] : null)));
        Native("type", a => One(a.Count == 0 || a[0] is null ? "nil"
            : a[0] is bool ? "boolean"
            : a[0] is double ? "number"
            : a[0] is string ? "string"
            : a[0] is LuaTable ? "table" : "function"));
        Native("unpack", a =>
        {
            if (a.Count == 0 || a[0] is not LuaTable t) return [];
            var res = new List<object?>();
            for (var i = 1; i <= t.Count(); i++) res.Add(t.Get((double)i));
            return res;
        });

        // UA: next/pairs/ipairs — потрібні для узагальнених циклів у скриптах.
        // EN: next/pairs/ipairs — required by generic for-loops in the scripts.
        LuaNative next = a =>
        {
            if (a.Count == 0 || a[0] is not LuaTable t) return [];
            var keys = t.Hash.Keys.ToList();
            if (a.Count < 2 || a[1] is null)
                return keys.Count > 0 ? [keys[0], t.Hash[keys[0]]] : [];
            var key = a[1] is double d && Math.Abs(d % 1) < double.Epsilon ? (long)d : a[1]!;
            var i = keys.FindIndex(k => k.Equals(key));
            return i >= 0 && i + 1 < keys.Count ? [keys[i + 1], t.Hash[keys[i + 1]]] : [];
        };
        g.Set("next", next);
        Native("pairs", a => [next, a.Count > 0 ? a[0] : null, null]);
        Native("ipairs", a =>
        {
            var t = a.Count > 0 ? a[0] as LuaTable : null;
            var index = 0;
            LuaNative iterator = _ =>
            {
                index++;
                var v = t?.Get((double)index);
                return v is null ? [] : [(double)index, v];
            };
            return [iterator, t, 0.0];
        });

        var table = new LuaTable();
        table.Set("getn", (LuaNative)(a => One(a[0] is LuaTable t ? (double)t.Count() : 0.0)));
        table.Set("insert", (LuaNative)(a =>
        {
            if (a[0] is LuaTable t && a.Count > 1) t.Set((double)(t.Count() + 1), a[^1]);
            return [];
        }));
        table.Set("remove", (LuaNative)(_ => []));
        g.Set("table", table);

        var math = new LuaTable();
        static double Num(object? v) => v is double d ? d : 0;
        math.Set("floor", (LuaNative)(a => One(Math.Floor(Num(a[0])))));
        math.Set("ceil", (LuaNative)(a => One(Math.Ceiling(Num(a[0])))));
        math.Set("abs", (LuaNative)(a => One(Math.Abs(Num(a[0])))));
        math.Set("max", (LuaNative)(a => One(a.Select(Num).Max())));
        math.Set("min", (LuaNative)(a => One(a.Select(Num).Min())));
        math.Set("mod", (LuaNative)(a => One(Num(a[0]) % Num(a[1]))));
        // UA: детерміноване значення — щоб дві траси були порівнюваними.
        // EN: a deterministic value so two traces stay comparable.
        math.Set("random", (LuaNative)(_ => One(0.5)));
        g.Set("math", math);

        var str = new LuaTable();
        str.Set("len", (LuaNative)(a => One(a[0] is string s ? (double)s.Length : 0.0)));
        str.Set("upper", (LuaNative)(a => One(a[0] is string s ? s.ToUpperInvariant() : a[0])));
        str.Set("lower", (LuaNative)(a => One(a[0] is string s ? s.ToLowerInvariant() : a[0])));
        str.Set("format", (LuaNative)(a => One(a.Count > 0 ? a[0] : null)));
        g.Set("string", str);

        // --- рушійні функції, що мають повертати осмислене ---------------------
        Native("ScriptCB_DoFile", a =>
        {
            if (a.Count > 0 && a[0] is string name) Execute(name);
            return [];
        });
        Native("ReadDataFile", _ => []);
        Native("ScriptCB_GetScreenInfo", _ => [ScreenWidth, ScreenHeight, 1.0, 1.0]);
        Native("ScriptCB_GetSafeScreenInfo", _ => [ScreenWidth * 0.9, ScreenHeight * 0.9, 1.0]);
        Native("ScriptCB_GetFontHeight", _ => One(FontHeight));
        Native("ScriptCB_IFText_GetTextExtent", _ => [100.0, FontHeight]);
        Native("ScriptCB_IFText_GetDisplayRect", _ => [0.0, 0.0, 100.0, FontHeight]);
        Native("ScriptCB_getlocalizestr", a => One(a.Count > 0 ? a[0] : null));
        Native("ScriptCB_ununicode", a => One(a.Count > 0 ? a[0] : null));
        Native("ScriptCB_usprintf", a => One(a.Count > 0 ? a[0] : null));
        Native("ScriptCB_GetShellActive", _ => One(1.0));
        g.Set("gPlatformStr", "PC");

        // UA: геометричні — записуємо аргументи й нічого не повертаємо.
        // EN: geometry ones — record the arguments and return nothing.
        foreach (var name in GeometryNatives)
            Native(name, MakeRecordingStub(name));
    }

    // -------------------------------------------------------------------------
    // UA: Готує середовище так, як це робить сама гра до побудови екранів:
    //     виконує "globals" і примітиви інтерфейсу. Саме після цього стають
    //     доступні NewIFContainer/NewButtonWindow/AddIFObjectBase тощо.
    // EN: Prepares the environment the way the game does before building
    //     screens: runs "globals" and the interface primitives, after which
    //     NewIFContainer/NewButtonWindow/AddIFObjectBase become available.
    // -------------------------------------------------------------------------
    public static readonly string[] InterfacePrimitives =
    [
        "globals", "interface_util", "ifelem_button", "ifelem_roundbutton",
        "ifelem_flatbutton", "ifelem_buttonwindow", "ifelem_segline",
        "ifelem_titlebar", "ifelem_borderrect", "ifelem_helptext",
    ];

    public void ExecuteInterfacePrimitives()
    {
        foreach (var name in InterfacePrimitives)
            Execute(name);
    }
}
