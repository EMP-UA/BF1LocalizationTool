// =============================================================================
// BF1LocalizationTool.Diagnostic — TraceNativeGeometryCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: ВИКОНУЄ реальні скрипти інтерфейсу BF2 (Lua50Interpreter + Bf2ScriptHost)
//     і показує, з якими САМЕ числами вони звертаються до рушія.
//
//     ЧОМУ ЦЯ КОМАНДА ІСНУЄ. Widescreen-фікс зводиться до перемноження координат
//     на коефіцієнт роздільності. Але щоб множити правильно, треба знати, ЯКИЙ
//     аргумент нативної функції є координатою x, який — y, а який взагалі не є
//     довжиною. Дизасемблер цього не показує: геометрія рахується в рантаймі,
//     і перевірити це можна лише запуском реальних скриптів, а не читанням
//     байткоду очима. Ця команда й виконує такий запуск.
//
//     ЩО ВОНА ПОКАЗУЄ. Нативні функції геометрії мають ДВІ форми:
//         ScriptCB_IFObj_SetPos(50, -25, 0)      -- 3 аргументи, БЕЗ хендла
//         ScriptCB_IFObj_SetPos(cp, 50, -25, 0)  -- 4 аргументи, з хендлом
//     Коротка форма використовується всередині блоку AddIF*…EndIFObj. Обгортка,
//     яка вважає перший аргумент завжди хендлом, помножить координату y на
//     ГОРИЗОНТАЛЬНИЙ коефіцієнт, а x не зачепить узагалі — тому форму кожного
//     виклику перевіряють цією командою, а не припущенням.
//
//     ЯК ЦИМ КОРИСТУВАТИСЬ ПРИ ЗМІНІ ШРИФТУ. Якщо зміниться шрифт (інша висота
//     від ScriptCB_GetFontHeight) — розкладка поїде. Запустіть цю команду двічі
//     з різним FontHeight у Bf2ScriptHost і порівняйте траси: рядки, що
//     відрізняються, і є місцями, залежними від метрик шрифту.
//
//     МЕЖІ ЧЕСНОСТІ. Заглушки повертають правдоподібні, але ВИГАДАНІ значення
//     (розмір тексту, наявність файлів). Тому абсолютні числа не є істиною в
//     останній інстанції — надійним є ПОРІВНЯННЯ двох трас за однакових
//     заглушок: vanilla проти патченого файлу.
//
// EN: EXECUTES the real BF2 interface scripts (Lua50Interpreter + Bf2ScriptHost)
//     and shows exactly which numbers they pass to the engine.
//
//     WHY IT EXISTS. A widescreen fix boils down to multiplying coordinates by a
//     resolution factor — but to multiply correctly you must know WHICH argument
//     of a native is x, which is y, and which is not a length at all. The
//     disassembler cannot show this because geometry is computed at runtime,
//     and the only way to check it is running the real scripts, not reading
//     bytecode by eye — which is exactly what this command does.
//
//     WHAT IT SHOWS: geometry natives have TWO calling
//     conventions, with and without the object handle. A wrapper that treats
//     the first argument as always the handle multiplies y by the HORIZONTAL
//     factor and leaves x untouched — so each call's form is checked with
//     this command rather than assumed.
//
//     HONEST LIMITS. Stubs return plausible but INVENTED values, so absolute
//     numbers are not gospel; what is reliable is COMPARING two traces taken
//     with identical stubs (vanilla vs a patched file).
// =============================================================================

using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class TraceNativeGeometryCommand
{
    // -------------------------------------------------------------------------
    // UA: commonLvlPath — common.lvl (там живуть interface_util та ifelem_*),
    //     shellLvlPath   — shell.lvl або ingame.lvl; може бути ПАТЧЕНИМ файлом,
    //                      тоді трасу видно вже з урахуванням застосованих обгорток.
    // EN: commonLvlPath — common.lvl (home of interface_util and ifelem_*),
    //     shellLvlPath  — shell.lvl or ingame.lvl; may be a PATCHED file, in
    //                     which case the trace already reflects the wrappers applied to it.
    // -------------------------------------------------------------------------
    public static void Run(
        DiagnosticReport report,
        string commonLvlPath,
        string shellLvlPath,
        double screenWidth,
        double screenHeight,
        string? installerEntryScript = null)
    {
        if (!File.Exists(commonLvlPath) || !File.Exists(shellLvlPath))
        {
            report.Log($"UA: не знайдено {commonLvlPath} або {shellLvlPath} — трасування пропущено.");
            report.Log($"EN: {commonLvlPath} or {shellLvlPath} not found — trace skipped.");
            return;
        }

        var host = new Bf2ScriptHost { ScreenWidth = screenWidth, ScreenHeight = screenHeight };
        host.LoadLevel(commonLvlPath);
        host.LoadLevel(shellLvlPath);

        report.Log($"UA: Завантажено скриптів: {host.ScriptNames.Count}. Екран для тесту: {screenWidth}x{screenHeight}.");
        report.Log($"EN: Scripts loaded: {host.ScriptNames.Count}. Test screen: {screenWidth}x{screenHeight}.");

        var stubbed = host.StubUnknownNatives();
        report.Log($"UA: Заглушено нативних функцій рушія: {stubbed} (усі, згадані у скриптах).");
        report.Log($"EN: Engine natives stubbed: {stubbed} (every one mentioned in the scripts).");
        report.Log();

        try
        {
            host.ExecuteInterfacePrimitives();
        }
        catch (LuaRuntimeException ex)
        {
            report.Log($"UA: Виконання примітивів інтерфейсу зупинилось: {ex.Message}");
            report.Log($"EN: Execution of interface primitives stopped: {ex.Message}");
            return;
        }

        var missingBase = host.GetGlobal("AddIFObjectBase");
        if (missingBase is null)
        {
            report.Log("UA: 'AddIFObjectBase' не визначився — примітиви інтерфейсу не завантажились повністю.");
            report.Log("EN: 'AddIFObjectBase' is not defined — interface primitives did not fully load.");
            return;
        }

        // UA: якщо задано скрипт-інсталятор (widescreen-патч), він виконується
        //     ПІСЛЯ примітивів — рівно так, як це відбувається в грі.
        // EN: if an installer script is given (the widescreen patch), run it
        //     AFTER the primitives — exactly as it happens in the game.
        if (installerEntryScript is not null)
        {
            try
            {
                host.Execute(installerEntryScript);
                report.Log($"UA: Скрипт-інсталятор '{installerEntryScript}' виконано.");
                report.Log($"EN: Installer script '{installerEntryScript}' executed.");
            }
            catch (LuaRuntimeException ex)
            {
                report.Log($"UA: Інсталятор '{installerEntryScript}' впав: {ex.Message}");
                report.Log($"EN: Installer '{installerEntryScript}' failed: {ex.Message}");
            }
            report.Log();
        }

        // UA: Зонд — таблиця з відомими наперед значеннями по КОЖНОМУ полю
        //     геометрії. Відомі входи означають, що будь-яке число у трасі можна
        //     однозначно співвіднести з полем і перевірити коефіцієнт.
        // EN: A probe table with known values for EVERY geometry field. Known
        //     inputs mean every number in the trace maps unambiguously back to a
        //     field, so the applied factor can be verified.
        var probe = new LuaTable();
        probe.Set("type", "image");
        probe.Set("x", 50.0);
        probe.Set("y", -25.0);
        probe.Set("ScreenRelativeX", 0.5);
        probe.Set("ScreenRelativeY", 0.5);
        probe.Set("localpos_l", -100.0);
        probe.Set("localpos_t", -75.0);
        probe.Set("localpos_r", 100.0);
        probe.Set("localpos_b", 75.0);

        report.Log("UA: Зонд (вхідні значення): x=50, y=-25, ScreenRelative=0.5/0.5, localpos=-100/-75/100/75");
        report.Log("EN: Probe (input values): x=50, y=-25, ScreenRelative=0.5/0.5, localpos=-100/-75/100/75");
        report.Log();

        host.RecordedCalls.Clear();
        try
        {
            host.Interpreter.Call(missingBase, [probe]);
        }
        catch (LuaRuntimeException ex)
        {
            report.Log($"UA: AddIFObjectBase зупинився: {ex.Message}");
            report.Log($"EN: AddIFObjectBase stopped: {ex.Message}");
        }

        report.Log("UA: --- ЗАФІКСОВАНІ ВИКЛИКИ РУШІЯ ---");
        report.Log("EN: --- RECORDED ENGINE CALLS ---");
        if (host.RecordedCalls.Count == 0)
        {
            report.Log("UA: жодного геометричного виклику не зафіксовано.");
            report.Log("EN: no geometry calls recorded.");
        }
        foreach (var call in host.RecordedCalls)
        {
            report.Log($"    {call}");
            report.Log($"      UA: кількість аргументів = {call.Arguments.Count} " +
                       "— ЗВЕРНІТЬ УВАГУ: наявність хендла об'єкта визначається саме нею.");
            report.Log($"      EN: argument count = {call.Arguments.Count} " +
                       "— NOTE: this is what tells you whether the object handle is present.");
        }

        if (host.MissingScripts.Count > 0)
        {
            report.Log();
            report.Log($"UA: Скрипти, яких бракувало (виклики ScriptCB_DoFile у нікуди): " +
                       $"{string.Join(", ", host.MissingScripts.Distinct().Take(20))}");
            report.Log($"EN: Missing scripts (ScriptCB_DoFile calls into nowhere): " +
                       $"{string.Join(", ", host.MissingScripts.Distinct().Take(20))}");
        }
    }

    // -------------------------------------------------------------------------
    // UA: Порівняння двох трас — головний робочий режим. Абсолютні числа
    //     залежать від заглушок, а ось РІЗНИЦЯ між vanilla і патчем є чесною,
    //     бо заглушки в обох прогонах однакові.
    // EN: Comparing two traces is the main working mode. Absolute numbers depend
    //     on the stubs, but the DIFFERENCE between vanilla and a patch is honest
    //     because the stubs are identical in both runs.
    // -------------------------------------------------------------------------
    public static void Compare(
        DiagnosticReport report,
        string commonLvlPath,
        string vanillaLvlPath,
        string patchedLvlPath,
        double vanillaWidth, double vanillaHeight,
        double patchedWidth, double patchedHeight,
        string? installerEntryScript)
    {
        report.Log("UA: === ТРАСА 1: vanilla ===");
        report.Log("EN: === TRACE 1: vanilla ===");
        Run(report, commonLvlPath, vanillaLvlPath, vanillaWidth, vanillaHeight);
        report.Log();
        report.Log("UA: === ТРАСА 2: патчений файл ===");
        report.Log("EN: === TRACE 2: patched file ===");
        Run(report, commonLvlPath, patchedLvlPath, patchedWidth, patchedHeight, installerEntryScript);
        report.Log();
        report.Log($"UA: Очікуваний коефіцієнт: X = {patchedWidth}/{vanillaWidth} = {patchedWidth / vanillaWidth:0.###}, " +
                   $"Y = {patchedHeight}/{vanillaHeight} = {patchedHeight / vanillaHeight:0.###}.");
        report.Log($"EN: Expected factor: X = {patchedWidth / vanillaWidth:0.###}, Y = {patchedHeight / vanillaHeight:0.###}.");
        report.Log("UA: Кожне число другої траси має дорівнювати відповідному числу першої, помноженому на коефіцієнт ПРАВИЛЬНОЇ осі.");
        report.Log("EN: Every number in the second trace must equal the matching first-trace number times the factor of the CORRECT axis.");
    }
}
