// =============================================================================
// BF1LocalizationTool.Diagnostic — SessionListHeaderBlockCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: ВИКОНУЄ реальний скрипт ОДНОГО екрана (за замовчуванням
//     "ifs_mp_sessionlist") через Bf2ScriptHost і показує ПОВНУ, впорядковану
//     хронологію його побудови: кожен виклик AddIFObjectBase (реєстрація
//     об'єкта рушієм) і кожен геометричний натив між ними — в ТІЙ САМІЙ
//     послідовності, в якій це робить сам скрипт.
//
//     ЧОМУ ЦЯ КОМАНДА ІСНУЄ (дефект D, ifs_mp_sessionlist.listbox.
//     titleBarElement — див. Bf2LayoutTable.txt). Чотирнадцять ітерацій
//     підбирали ОДНЕ число (bgoffsety) для titleBarElement, спираючись лише
//     на КІНЦЕВИЙ стан його таблиці (VM-дамп показав titleBarElement.y=nil)
//     і на піксельні виміри знімків. Це довело, що поле не задане статично,
//     але НІКОЛИ не показало, ЯК сам скрипт розташовує filterRow/listbox/
//     titleBarElement/ResortButtons одне відносно одного під час побудови —
//     чи є спільний якір/контейнер, чи спільна локальна змінна, яку
//     проігноровано, дивлячись лише на статичну таблицю. Ванільний знімок
//     (og mp.jpg) показує, що рушій УМІЄ малювати ці блоки окремо, з
//     відступами, без жодного розповзання — тобто причина дефекту не в
//     обмеженні рушія, а в тому, що цей фікс рухає titleBarElement (bgoffsety)
//     і ResortButtons (posy) кожен окремо, підібраними числами, без
//     збереження авторського зв'язку між ними. Ця команда — спосіб побачити
//     той зв'язок (якщо він є) РЕАЛЬНИМ виконанням, а не гадкою.
//
//     МЕХАНІЗМ. AddIFObjectBase (interface_util) — спільна точка, через яку
//     проходить КОЖЕН віджет екрана: вона читає x/y/ZPos/width/height/
//     titleText/bg* з таблиці об'єкта і реєструє його в рушії. Обгортаючи її
//     (зберігши оригінал і підмінивши глобал) та обгортаючи кожен
//     геометричний натив (Bf2ScriptHost.GeometryNatives) так само,
//     отримується ОДИН хронологічний журнал: "об'єкт #N зареєстровано з такими
//     полями" одразу за яким ідуть "натив X викликаний з такими аргументами"
//     — саме той порядок, у якому це відбувається в грі.
//
//     ЧЕСНІ МЕЖІ. Bf2ScriptHost — не емулятор гри: ScriptCB_GetSafeScreenInfo
//     і ScriptCB_IFText_GetTextExtent повертають ПРАВДОПОДІБНІ, АЛЕ ВИГАДАНІ
//     числа (0.9×екран, фіксований розмір тексту). Тому абсолютні пікселі з
//     цього прогону НЕ замінюють перевірку знімком гри.
//     Що тут надійне — САМА СТРУКТУРА: порядок побудови, які поля присутні/
//     відсутні (nil), чи повторюється те саме число в різних об'єктах
//     (ознака спільного якоря чи локальної змінної в оригінальному Lua).
//
//     ГЕНЕРАЛІЗАЦІЯ. Параметр screenName навмисно не захардкожений на
//     "ifs_mp_sessionlist" — той самий прогін застосовний до будь-якого з
//     інших екранів-списків (задача "поширити фікс синіх блоків за межі
//     ifs_mp_sessionlist"), без нового коду.
//
// EN: EXECUTES the real script of ONE screen (default "ifs_mp_sessionlist")
//     via Bf2ScriptHost and prints the FULL, ordered timeline of its build:
//     every AddIFObjectBase call (the engine's object registration) and every
//     geometry native between them — in the EXACT sequence the script itself
//     performs them.
//
//     WHY THIS COMMAND EXISTS (defect D, ifs_mp_sessionlist.listbox.
//     titleBarElement — see Bf2LayoutTable.txt). Fourteen iterations tuned
//     ONE number (bgoffsety) for titleBarElement based only on its FINAL
//     table state (a VM dump showed titleBarElement.y=nil) and on pixel
//     measurements of screenshots. That proved the field is not set
//     statically, but it never showed HOW the script itself places
//     filterRow/listbox/titleBarElement/ResortButtons relative to one
//     another during construction — whether there is a shared anchor/
//     container, or a shared local variable missed by only looking at
//     the static table. The vanilla screenshot (og mp.jpg) shows the engine
//     CAN draw these blocks separately, with gaps, with no bleed at all — so
//     the defect's cause is not an engine limitation — the fix here moves
//     titleBarElement (bgoffsety) and ResortButtons (posy) each
//     independently, with tuned numbers, without preserving whatever
//     authored relationship ties them together. This command is a way to
//     SEE that relationship (if one exists) by real execution, not a guess.
//
//     MECHANISM. AddIFObjectBase (interface_util) is the shared choke point
//     every widget on a screen passes through: it reads x/y/ZPos/width/
//     height/titleText/bg* from the object's table and registers it with the
//     engine. Wrapping it (saving the original, replacing the global) and
//     wrapping every geometry native (Bf2ScriptHost.GeometryNatives) the same
//     way gives ONE chronological log: "object #N registered with these
//     fields" immediately followed by "native X called with these
//     arguments" — the exact order the game itself follows.
//
//     HONEST LIMITS. Bf2ScriptHost is not a game emulator:
//     ScriptCB_GetSafeScreenInfo and ScriptCB_IFText_GetTextExtent return
//     PLAUSIBLE BUT INVENTED numbers (0.9x screen, a fixed text size). So the
//     absolute pixels from this run do NOT replace an in-game screenshot
//     check. What IS reliable here is the STRUCTURE: build
//     order, which fields are present/absent (nil), whether the same number
//     recurs across different objects (a sign of a shared anchor or a shared
//     Lua local in the original source).
//
//     GENERALISATION. The screenName parameter is deliberately not
//     hard-coded to "ifs_mp_sessionlist" — the same run applies to any other
//     list screen (the "extend the blue-block fix beyond ifs_mp_sessionlist"
//     task) with no new code.
// =============================================================================

using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class SessionListHeaderBlockCommand
{
    // UA: Поля, за якими можна впізнати об'єкт, не знаючи його імені в
    //     батьківській таблиці (AddIFObjectBase не отримує шлях — лише саму
    //     таблицю об'єкта). Список свідомо покриває і "звичайну" геометрію
    //     (x/y/ZPos/width/height), і поля FlashyText-фону (bg*), бо саме
    //     titleBarElement — FlashyText без власних x/y.
    // EN: Fields that identify an object without knowing its name in
    //     the parent table (AddIFObjectBase never receives a path — only the
    //     object's own table). The list covers both "ordinary" geometry
    //     (x/y/ZPos/width/height) and FlashyText background fields (bg*),
    //     since titleBarElement itself is a FlashyText with no x/y of its own.
    private static readonly string[] IdentityFields =
    [
        "type", "x", "y", "ZPos", "width", "height",
        "ScreenRelativeX", "ScreenRelativeY",
        "localpos_l", "localpos_t", "localpos_r", "localpos_b",
        "bgoffsetx", "bgoffsety", "bgexpandx", "bgexpandy",
        "titleText", "flashy", "startdelay", "alpha",
    ];

    public static void Run(
        DiagnosticReport report,
        string commonLvlPath,
        string shellLvlPath,
        double screenWidth,
        double screenHeight,
        string screenName = "ifs_mp_sessionlist",
        float? installWidescreenPatchScale = null)
    {
        if (!File.Exists(commonLvlPath) || !File.Exists(shellLvlPath))
        {
            report.Log($"UA: не знайдено {commonLvlPath} або {shellLvlPath} — прогін пропущено.");
            report.Log($"EN: {commonLvlPath} or {shellLvlPath} not found — run skipped.");
            return;
        }

        var host = new Bf2ScriptHost { ScreenWidth = screenWidth, ScreenHeight = screenHeight };
        host.LoadLevel(commonLvlPath);
        host.LoadLevel(shellLvlPath);

        var allScriptNames = host.ScriptNames.OrderBy(n => n, StringComparer.Ordinal).ToList();
        report.Log($"UA: Завантажено скриптів: {allScriptNames.Count}. Екран для тесту: {screenName} @ {screenWidth}x{screenHeight}.");
        report.Log($"EN: Scripts loaded: {allScriptNames.Count}. Test screen: {screenName} @ {screenWidth}x{screenHeight}.");
        report.Log($"UA: Усі завантажені імена: {string.Join(", ", allScriptNames)}");
        report.Log($"EN: All loaded names: {string.Join(", ", allScriptNames)}");

        if (!host.ScriptNames.Contains(screenName))
        {
            report.Log($"UA: скрипта \"{screenName}\" немає серед завантажених — перевір ім'я " +
                       "(воно береться з чанка NAME усередині scr_, регістр важливий).");
            report.Log($"EN: script \"{screenName}\" is not among the loaded ones — check the name " +
                       "(it comes from the NAME chunk inside scr_, case-sensitive).");
            return;
        }

        var stubbed = host.StubUnknownNatives();
        report.Log($"UA: Заглушено нативних функцій рушія: {stubbed}.");
        report.Log($"EN: Engine natives stubbed: {stubbed}.");

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

        var addIfObjectBase = host.GetGlobal("AddIFObjectBase");
        if (addIfObjectBase is null)
        {
            report.Log("UA: 'AddIFObjectBase' не визначився — примітиви інтерфейсу не завантажились повністю.");
            report.Log("EN: 'AddIFObjectBase' is not defined — interface primitives did not fully load.");
            return;
        }

        // -----------------------------------------------------------------
        // UA: ОПЦІЙНО — встановити СПРАВЖНЮ обгортку AddIFScreen з
        //     AnchorInheritancePatchBuilder.BuildInstallerScript (той самий
        //     код, що йде у shell_layout.lvl через ShellEntryPointPatcher).
        //     Так екран виконується РЕАЛЬНИМ патчем (усі рядки
        //     Bf2LayoutTable.txt для цього screenName), а не голим ванільним
        //     скриптом — це і показує, які значення bgoffsety/posy/ScreenRelative
        //     тощо ФАКТИЧНО потрапляють у гру на цій роздільності, замість
        //     здогадок. Встановлюється ПРЯМИМ викликом інсталятора як функції
        //     (без потреби виконувати "shell_interface" цілком — той тягне за
        //     собою багато "ifs_*"-екранів, які свідомо не виконуються).
        // EN: OPTIONAL — install the REAL AddIFScreen wrapper from
        //     AnchorInheritancePatchBuilder.BuildInstallerScript (the exact
        //     code that ships inside shell_layout.lvl via
        //     ShellEntryPointPatcher). This makes the screen run under the
        //     REAL patch (every Bf2LayoutTable.txt row for this screenName),
        //     not the bare vanilla script — showing what bgoffsety/posy/
        //     ScreenRelative etc. ACTUALLY end up as at this resolution,
        //     instead of guessing. Installed by calling the installer
        //     prototype directly (no need to run "shell_interface" as a
        //     whole — that pulls in many "ifs_*" screens deliberately left
        //     unexecuted here).
        // -----------------------------------------------------------------
        if (installWidescreenPatchScale is { } patchScale)
        {
            report.Log();
            report.Log($"UA: Встановлюю СПРАВЖНЮ обгортку AddIFScreen (Bf2LayoutTable.txt, scale={patchScale:0.##}) — той самий код, що й у shell_layout.lvl.");
            report.Log($"EN: Installing the REAL AddIFScreen wrapper (Bf2LayoutTable.txt, scale={patchScale:0.##}) — the exact code shipped in shell_layout.lvl.");
            var installerProto = AnchorInheritancePatchBuilder.BuildInstallerScript(patchScale);
            host.Interpreter.Call(new LuaClosure(installerProto, []), []);
            var wrapped = host.GetGlobal("AddIFScreen");
            var backedUp = host.GetGlobal("_ws_o_AddIFScreen");
            report.Log($"UA: AddIFScreen замінено: {wrapped is not null}. Оригінал збережено як _ws_o_AddIFScreen: {backedUp is not null}.");
            report.Log($"EN: AddIFScreen replaced: {wrapped is not null}. Original backed up as _ws_o_AddIFScreen: {backedUp is not null}.");
        }

        // -----------------------------------------------------------------
        // UA: ПЕРЕД спробою виконати екран — розібрати його байткод
        //     НЕЗАЛЕЖНО від Bf2ScriptHost (він не віддає сирий byte-код
        //     назовні) і перевірити КОЖЕН GETGLOBAL на наявність у поточному
        //     оточенні. Це перетворює голе "спроба викликати не-функцію
        //     (nil)" на конкретне ім'я ЩЕ ДО падіння — типова причина: скрипт
        //     екрана залежить від СПІЛЬНОГО helper-скрипта для родини
        //     "ifs_mp_*" (за аналогією з ifs_freeform_AddCommonElements для
        //     кампанії), якого немає серед InterfacePrimitives і який ще
        //     не виконано.
        // EN: BEFORE attempting to execute the screen — parse its bytecode
        //     INDEPENDENTLY of Bf2ScriptHost (it does not expose raw
        //     bytecode) and check EVERY GETGLOBAL against the current
        //     environment. This turns a bare "attempt to call a non-function
        //     (nil)" into a concrete name BEFORE the crash — the typical
        //     cause: the screen script depends on a SHARED helper script for
        //     the "ifs_mp_*" family (by analogy with
        //     ifs_freeform_AddCommonElements for the campaign side) that is
        //     not part of InterfacePrimitives and has not been executed
        //     yet.
        // -----------------------------------------------------------------
        var root = UcfbReader.ReadFile(shellLvlPath);
        var resource = ScriptChunkLocator.FindAll(root).FirstOrDefault(r => r.Name == screenName);
        if (resource is not null)
        {
            var proto = Lua50BytecodeReader.Parse(resource.BodyChunk.RawData).Root;
            var globalNames = CollectGlobalReads(proto, [])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            var missing = globalNames.Where(n => host.GetGlobal(n) is null).ToList();

            report.Log();
            report.Log($"UA: Глобалів, до яких звертається \"{screenName}\" (GETGLOBAL, рекурсивно по вкладених функціях): {globalNames.Count}.");
            report.Log($"EN: Globals \"{screenName}\" reads (GETGLOBAL, recursive across nested functions): {globalNames.Count}.");
            if (missing.Count > 0)
            {
                report.Log($"UA: З них ВІДСУТНІ в поточному оточенні (nil) — найімовірніші причини падіння: {string.Join(", ", missing)}");
                report.Log($"EN: Of those, MISSING from the current environment (nil) — most likely reasons for the failure: {string.Join(", ", missing)}");

                // -------------------------------------------------------------
                // UA: Не гадати назву відсутнього скрипта-постачальника, а
                //     ОБЧИСЛИТИ: серед УСІХ завантажених імен шукаємо
                //     найдовший префікс, що збігається з відсутнім глобалом
                //     (напр. "ifelem_tabmanager_Create" -> скрипт
                //     "ifelem_tabmanager", якщо такий реально завантажений).
                //     Поріг довжини 4 — щоб не спрацьовувати на випадкові
                //     короткі збіги.
                // EN: Don't guess the missing provider script's name —
                //     COMPUTE it: among ALL loaded names, find the longest
                //     prefix that matches a missing global (e.g.
                //     "ifelem_tabmanager_Create" -> the "ifelem_tabmanager"
                //     script, if one is actually loaded). Length threshold 4
                //     avoids matching on accidental short prefixes.
                // -------------------------------------------------------------
                report.Log();
                report.Log("UA: Кандидати-постачальники (найдовший префікс імені завантаженого скрипта, що збігається з відсутнім глобалом):");
                report.Log("EN: Candidate provider scripts (longest loaded-script-name prefix matching a missing global):");
                foreach (var m in missing)
                {
                    var candidate = allScriptNames
                        .Where(s => s.Length >= 4 && m.StartsWith(s, StringComparison.Ordinal))
                        .OrderByDescending(s => s.Length)
                        .FirstOrDefault();
                    report.Log(candidate is not null
                        ? $"    {m}  <-  {candidate}"
                        : $"    {m}  <-  (не знайдено серед завантажених імен / not found among loaded names)");
                }

                // -------------------------------------------------------------
                // UA: АВТОМАТИЧНО довиконати ЛИШЕ кандидатів, чиє ім'я НЕ
                //     починається на "ifs_" — за спостереженим правилом,
                //     "ifs_*" завжди позначає ІНШИЙ ЕКРАН (сусідній, не
                //     потрібний для побудови ЦЬОГО), а спільні helper-скрипти
                //     (ifelem_*, ifelm_*, listmanager тощо) — ні. Виконання
                //     сусіднього екрана тут забруднило б журнал ЙОГО
                //     об'єктами/викликами й могло б впасти на СВОЇЙ окремій
                //     відсутній залежності — тому свідомо НЕ чіпаємо "ifs_*".
                // EN: AUTOMATICALLY execute ONLY the candidates whose name
                //     does NOT start with "ifs_" — per the observed
                //     convention, "ifs_*" always names ANOTHER SCREEN
                //     (a sibling, not needed to build THIS one), while shared
                //     helper scripts (ifelem_*, ifelm_*, listmanager, etc.)
                //     do not. Executing a sibling screen here would pollute
                //     the log with ITS OWN objects/calls and could fail on
                //     ITS OWN separate missing dependency — so "ifs_*" is
                //     deliberately left untouched.
                // -------------------------------------------------------------
                // -------------------------------------------------------------
                // UA: Префіксний збіг (вище) знаходить лише ЧАСТИНУ реальних
                //     постачальників — гра сама не тримається єдиної угоди
                //     іменування: "ListManager_fn*" постачає "ifelem_
                //     listmanager", "Popup_Ok" постачає "popup_ok",
                //     "gCurHiliteButton"/"AddPCTitleText" постачає
                //     "shell_interface" — жодного спільного префікса. Тому
                //     тут НЕ вгадується назва постачальника вдруге, а ЧЕСНО
                //     довиконуємо ВСІ завантажені скрипти, крім самих екранів
                //     (префікс "ifs_" — окремі, самодостатні екрани; як
                //     побічний ефект їх виконувати і небезпечно, і не
                //     потрібно) і крім самого screenName. Кожен — через
                //     TryExecute (НЕ host.Execute напряму: Execute позначає
                //     скрипт "виконаним" ще ДО спроби, щоб не зациклити
                //     взаємні ScriptCB_DoFile, а це означає, що повторний
                //     Execute() після невдачі — тихий no-op; TryExecute знімає
                //     позначку після невдалої спроби, тож наступний прохід —
                //     СПРАВЖНЯ повторна спроба). Кілька проходів поспіль (доки
                //     хоч один дає прогрес) розв'язують залежності і МІЖ
                //     самими спільними скриптами.
                // EN: The prefix match (above) finds only PART of the real
                //     providers — the game itself has no single naming
                //     convention: "ListManager_fn*" is provided by "ifelem_
                //     listmanager", "Popup_Ok" by "popup_ok", "gCurHilite
                //     Button"/"AddPCTitleText" by "shell_interface" — no
                //     shared prefix at all. So instead of guessing the
                //     provider's name a second time, EVERY loaded script is HONESTLY
                //     executed except the screens themselves
                //     (prefix "ifs_" — separate, self-contained screens;
                //     running them as a side effect is both risky and
                //     unnecessary) and except screenName itself. Each one via
                //     TryExecute (NOT host.Execute directly: Execute marks a
                //     script "executed" BEFORE attempting it, to avoid
                //     infinite recursion on mutual ScriptCB_DoFile calls —
                //     which means a repeated Execute() after a failure is a
                //     silent no-op; TryExecute clears the mark after a failed
                //     attempt, so the next pass is a GENUINE retry). Several
                //     passes in a row (as long as at least one makes
                //     progress) resolve dependencies BETWEEN shared scripts
                //     too.
                // -------------------------------------------------------------
                var resolveCandidates = allScriptNames
                    .Where(n => n != screenName && !n.StartsWith("ifs_", StringComparison.Ordinal))
                    .ToList();
                var stillToRun = new List<string>(resolveCandidates);
                var resolvedOk = new List<string>();
                var resolveError = new Dictionary<string, string>();

                for (var pass = 0; pass < resolveCandidates.Count && stillToRun.Count > 0; pass++)
                {
                    var progressed = false;
                    foreach (var name in stillToRun.ToList())
                    {
                        if (host.TryExecute(name, out var err))
                        {
                            resolvedOk.Add(name);
                            stillToRun.Remove(name);
                            resolveError.Remove(name);
                            progressed = true;
                        }
                        else
                        {
                            resolveError[name] = err ?? "?";
                        }
                    }
                    if (!progressed) break;
                }

                report.Log();
                report.Log($"UA: Фаза довиконання ВСІХ спільних скриптів (не \"ifs_*\", крім \"{screenName}\"): " +
                           $"успішно {resolvedOk.Count} з {resolveCandidates.Count}.");
                report.Log($"EN: Resolution phase for ALL shared scripts (not \"ifs_*\", except \"{screenName}\"): " +
                           $"{resolvedOk.Count} of {resolveCandidates.Count} succeeded.");
                if (stillToRun.Count > 0)
                {
                    report.Log($"UA: Не вдалося виконати ({stillToRun.Count}): " +
                               string.Join("; ", stillToRun.Take(20).Select(n => $"{n} [{resolveError.GetValueOrDefault(n, "?")}]")));
                    report.Log($"EN: Failed to execute ({stillToRun.Count}): " +
                               string.Join("; ", stillToRun.Take(20).Select(n => $"{n} [{resolveError.GetValueOrDefault(n, "?")}]")));
                }

                var stillMissing = missing.Where(n => host.GetGlobal(n) is null).ToList();
                report.Log();
                report.Log($"UA: Після довиконання серед ПОЧАТКОВОГО списку досі відсутні: " +
                           $"{(stillMissing.Count == 0 ? "жодного" : string.Join(", ", stillMissing))}");
                report.Log($"EN: Still missing from the ORIGINAL list after auto-execution: " +
                           $"{(stillMissing.Count == 0 ? "none" : string.Join(", ", stillMissing))}");
            }
            else
            {
                report.Log("UA: Усі звернені глобали визначені — якщо виконання все одно впаде, причина НЕ у відсутній функції верхнього рівня.");
                report.Log("EN: All referenced globals are defined — if execution still fails, the cause is NOT a missing top-level function.");
            }
        }
        else
        {
            report.Log($"UA: не вдалося повторно знайти ресурс \"{screenName}\" для розбору байткоду (несподівано — вище він же завантажився).");
            report.Log($"EN: could not re-locate the \"{screenName}\" resource for bytecode parsing (unexpected — it loaded fine above).");
        }

        // -----------------------------------------------------------------
        // UA: ОДИН хронологічний журнал. AddIFObjectBase обгорнуто напряму;
        //     кожен геометричний натив обгорнуто ПОВЕРХ уже наявної
        //     записуючої заглушки з Bf2ScriptHost (RegisterEnvironment), тож
        //     host.RecordedCalls і далі заповнюється так само — цей прогін
        //     нічого не забирає, лише додає єдиний порядок.
        // EN: ONE chronological log. AddIFObjectBase is wrapped directly;
        //     every geometry native is wrapped ON TOP of the recording stub
        //     Bf2ScriptHost already installed (RegisterEnvironment), so
        //     host.RecordedCalls keeps filling exactly as before — this run
        //     takes nothing away, it only adds a single shared order.
        // -----------------------------------------------------------------
        // -----------------------------------------------------------------
        // UA: Обгортка AddIFScreen ЛИШЕ для захоплення ПОСИЛАННЯ на
        //     таблицю конфігурації екрана `t` (поведінку не змінює:
        //     виклик одразу делегується далі, як був). Мета: після
        //     виконання порівняти ПОСИЛАННЯ (не позицію в журналі) між
        //     `t.listbox.titleBarElement` і кожною таблицею, що пройшла
        //     через AddIFObjectBase, — щоб точно з'ясувати, якому саме
        //     obj# відповідає шлях "listbox.titleBarElement" з
        //     Bf2LayoutTable.txt, замість здогадки за порядком друку
        //     (AddIFObjectBase шляху не отримує, див. коментар вище).
        //     Порівняння за посиланням дає прямий доказ там, де порядок
        //     друку журналу може бути неоднозначним.
        // EN: An AddIFScreen wrapper ONLY to capture a REFERENCE to the
        //     screen's config table `t` (no behavior change: the call is
        //     delegated onward immediately, as before). Goal: after
        //     execution, compare REFERENCES (not log position) between
        //     `t.listbox.titleBarElement` and every table that passed
        //     through AddIFObjectBase, to determine exactly which obj#
        //     the Bf2LayoutTable.txt path "listbox.titleBarElement"
        //     corresponds to, instead of guessing from print order
        //     (AddIFObjectBase gets no path, see the comment above).
        //     Comparing by reference gives direct proof where log print
        //     order can be ambiguous.
        // -----------------------------------------------------------------
        LuaTable? capturedScreenTable = null;
        var addIfScreenCurrent = host.GetGlobal("AddIFScreen");
        if (addIfScreenCurrent is not null)
        {
            LuaNative screenWrapper = args =>
            {
                if (args.Count > 1 && args[0] is LuaTable st && args[1] is string sn && sn == screenName)
                    capturedScreenTable = st;
                return host.Interpreter.Call(addIfScreenCurrent, args);
            };
            host.Interpreter.Globals.Set("AddIFScreen", screenWrapper);
        }

        var capturedObjects = new List<(int Index, LuaTable Table)>();

        var trace = new List<string>();
        var objectIndex = 0;

        LuaNative objectWrapper = args =>
        {
            objectIndex++;
            if (args.Count > 0 && args[0] is LuaTable t)
            {
                trace.Add($"[obj #{objectIndex}] AddIFObjectBase {{{DescribeWidget(t)}}}");
                capturedObjects.Add((objectIndex, t));
            }
            else
                trace.Add($"[obj #{objectIndex}] AddIFObjectBase (арг. #0 не таблиця / arg #0 not a table)");
            return host.Interpreter.Call(addIfObjectBase, args);
        };
        host.Interpreter.Globals.Set("AddIFObjectBase", objectWrapper);

        foreach (var name in Bf2ScriptHost.GeometryNatives)
        {
            var original = host.GetGlobal(name);
            if (original is null) continue; // UA: не мало б статись — заглушки реєструються для всіх / EN: shouldn't happen — stubs cover all of them
            LuaNative wrapped = args =>
            {
                var argsText = string.Join(", ", args.Select(Lua50Interpreter.Describe));
                trace.Add($"    -> {name}({argsText})");
                return host.Interpreter.Call(original, args);
            };
            host.Interpreter.Globals.Set(name, wrapped);
        }

        report.Log();
        report.Log($"UA: === Хронологія побудови \"{screenName}\" ({screenWidth}x{screenHeight}) ===");
        report.Log($"EN: === Build timeline of \"{screenName}\" ({screenWidth}x{screenHeight}) ===");

        host.RecordedCalls.Clear();
        try
        {
            host.Execute(screenName);
        }
        catch (LuaRuntimeException ex)
        {
            report.Log($"UA: Виконання \"{screenName}\" зупинилось: {ex.Message}");
            report.Log($"EN: Execution of \"{screenName}\" stopped: {ex.Message}");
            report.Log("UA: Журнал ДО зупинки все одно друкується нижче — він чесний до цієї точки.");
            report.Log("EN: The log UP TO the stop is still printed below — it is honest up to that point.");
        }

        if (trace.Count == 0)
        {
            report.Log("UA: жодного об'єкта чи геометричного виклику не зафіксовано.");
            report.Log("EN: no object or geometry call was recorded.");
        }
        foreach (var line in trace) report.Log(line);

        report.Log();
        report.Log($"UA: Об'єктів зареєстровано: {objectIndex}. Геометричних викликів: " +
                   $"{trace.Count(l => l.StartsWith("    ->", StringComparison.Ordinal))}.");
        report.Log($"EN: Objects registered: {objectIndex}. Geometry calls: " +
                   $"{trace.Count(l => l.StartsWith("    ->", StringComparison.Ordinal))}.");

        // -----------------------------------------------------------------
        // UA: Перевірка ПОСИЛАНЬ (див. коментар вище біля capturedScreenTable):
        //     остаточна, а не позиційна відповідь на питання "якому obj# з
        //     журналу відповідає шлях listbox.titleBarElement".
        // EN: REFERENCE check (see the comment above near capturedScreenTable):
        //     a final, not positional, answer to "which obj# in the log does
        //     listbox.titleBarElement actually correspond to".
        // -----------------------------------------------------------------
        if (capturedScreenTable is not null)
        {
            report.Log();
            report.Log("UA: === Перевірка ПОСИЛАНЬ: якому obj# справді відповідає шлях (не за порядком друку) ===");
            report.Log("EN: === REFERENCE check: which obj# a path actually resolves to (not by print order) ===");

            void ReportPathTarget(string path)
            {
                object? cur = capturedScreenTable;
                foreach (var segment in path.Split('.'))
                {
                    if (cur is LuaTable ct) cur = ct.Get(segment);
                    else { cur = null; break; }
                }

                if (cur is LuaTable target)
                {
                    var match = capturedObjects.FirstOrDefault(o => ReferenceEquals(o.Table, target));
                    var matchLabel = match.Table is null ? "жоден / none" : $"obj #{match.Index}";
                    report.Log($"UA: \"{path}\" -> {{{DescribeWidget(target)}}} -> {matchLabel} у журналі AddIFObjectBase.");
                    report.Log($"EN: \"{path}\" -> {{{DescribeWidget(target)}}} -> {matchLabel} in the AddIFObjectBase log.");
                }
                else
                {
                    report.Log($"UA: \"{path}\" -> не таблиця/не знайдено (значення: {Lua50Interpreter.Describe(cur)}).");
                    report.Log($"EN: \"{path}\" -> not a table/not found (value: {Lua50Interpreter.Describe(cur)}).");
                }
            }

            ReportPathTarget("listbox.titleBarElement");
            ReportPathTarget("listbox.skin");
            ReportPathTarget("serverinfo.titleBarElement");
            ReportPathTarget("playerlist.titleBarElement");
        }
        else
        {
            report.Log();
            report.Log($"UA: AddIFScreen для \"{screenName}\" не перехоплено (можливо, екран не викликає його сам на собі) — перевірку посилань пропущено.");
            report.Log($"EN: AddIFScreen for \"{screenName}\" was not intercepted (the screen may not call it on itself) — reference check skipped.");
        }

        if (host.MissingScripts.Count > 0)
        {
            report.Log();
            report.Log($"UA: Скрипти, яких бракувало (ScriptCB_DoFile у нікуди): " +
                       $"{string.Join(", ", host.MissingScripts.Distinct().Take(20))}");
            report.Log($"EN: Missing scripts (ScriptCB_DoFile into nowhere): " +
                       $"{string.Join(", ", host.MissingScripts.Distinct().Take(20))}");
        }

        report.Log();
        report.Log("UA: НА ЩО ДИВИТИСЬ. (1) Чи є в журналі об'єкт з ознаками filterRow/\"Internet\" " +
                   "ПЕРЕД об'єктом з flashy=1.0 (titleBarElement) — і чи повторюється якесь число " +
                   "(x/y/width) між ними: це і був би пропущений спільний якір. (2) Чи titleBarElement " +
                   "(flashy=1.0) взагалі отримує SetBackgroundSize з не-nil аргументами у ваніль, чи " +
                   "всі чотири nil, як уже підтверджено VM-дампом. (3) Порядок AddIFObjectBase-викликів " +
                   "щодо listbox/listbox.skin — чи є там натяк на СПІЛЬНИЙ контейнер, а не окремі об'єкти.");
        report.Log("EN: WHAT TO LOOK FOR. (1) Is there an object in the log with filterRow/\"Internet\" " +
                   "traits BEFORE the flashy=1.0 object (titleBarElement) — and does some number (x/y/" +
                   "width) repeat between them: that would be the missed shared anchor. (2) Does " +
                   "titleBarElement (flashy=1.0) get a SetBackgroundSize with non-nil arguments in " +
                   "vanilla at all, or are all four nil, as the VM dump already confirmed. (3) The order " +
                   "of AddIFObjectBase calls around listbox/listbox.skin — any hint of a SHARED " +
                   "container rather than separate objects.");
    }

    // -------------------------------------------------------------------------
    // UA: Рекурсивно збирає ВСІ імена, до яких звертається прототип (і його
    //     вкладені функції) через опкод GETGLOBAL — тобто реальні виклики
    //     глобальних функцій/читання глобальних змінних, а не довільні рядки
    //     з пулу констант (на відміну від Bf2ScriptHost.CollectNativeNames,
    //     який навмисно бере ВЕСЬ пул констант і фільтрує за префіксом
    //     "ScriptCB_" — тут префікса нема, тож потрібен саме опкод-рівень).
    // EN: Recursively collects EVERY name a prototype (and its nested
    //     functions) reads via the GETGLOBAL opcode — i.e. real global
    //     function calls/variable reads, not arbitrary strings from the
    //     constant pool (unlike Bf2ScriptHost.CollectNativeNames, which
    //     deliberately takes the WHOLE constant pool and filters by the
    //     "ScriptCB_" prefix — there is no such prefix here, so this needs
    //     opcode-level precision instead).
    // -------------------------------------------------------------------------
    private static List<string> CollectGlobalReads(LuaFunctionPrototype proto, List<string> into)
    {
        foreach (var instr in proto.Instructions)
        {
            if (instr.Opcode != LuaOpcode.GetGlobal || instr.Bx is not { } bx) continue;
            if (bx < 0 || bx >= proto.Constants.Count) continue;
            var k = proto.Constants[bx];
            if (k.Kind == LuaConstantKind.String && k.StringValue is { } s)
                into.Add(s);
        }

        foreach (var nested in proto.NestedPrototypes)
            CollectGlobalReads(nested, into);

        return into;
    }

    private static string DescribeWidget(LuaTable t)
    {
        var parts = new List<string>();
        foreach (var field in IdentityFields)
        {
            var v = t.Get(field);
            if (v is not null) parts.Add($"{field}={Lua50Interpreter.Describe(v)}");
        }
        return parts.Count > 0 ? string.Join(", ", parts) : "(жодного відомого поля / none of the known fields)";
    }
}
