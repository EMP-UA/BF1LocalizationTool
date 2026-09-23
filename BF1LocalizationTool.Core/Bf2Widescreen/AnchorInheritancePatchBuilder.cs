// =============================================================================
// BF1LocalizationTool.Core — Bf2Widescreen/AnchorInheritancePatchBuilder.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ГЕНЕРАТОР (production, входить у фінальний патч) / GENERATOR (production, part of the final patch)
// =============================================================================
// UA: Механізм застосування таблиці виправлень розкладки BF2.
//
//     Таблиця виправлень (Data/Bf2LayoutTable.txt) містить 485 рядків на 30
//     екранів. Кожен рядок додається лише після виміру на реальному знімку
//     гри; сама таблиця не зберігає статус перевірки по рядках — це формат
//     числових значень, а не журнал підтверджень.
//
//     ВСТАНОВЛЕНІ ФАКТИ, НА ЯКИХ БАЗУЄТЬСЯ ПІДХІД:
//       • рушій не потребує глобального «виправлення»: ваніль на 1920×1080
//         правильна на простих екранах, а підміна ScriptCB_GetScreenInfo
//         ламає 667 елементів, що коректно самокомпенсуються;
//       • зростання розмірів разом з екраном (×W/800, ×H/600) — ПРАВИЛЬНА
//         поведінка, а не дефект: екран аудіо у ванілі на 1920 бездоганний;
//       • фон рушій розтягує сам;
//       • виправляється лише те, що видно як дефект на знімку ванільної гри
//         в цільовій роздільності;
//       • частковий фікс рівноцінний регресії: екран або доведено до нуля
//         дефектів, або лишається ванільним.
//
//     МЕХАНІЗМ. Обгортка `AddIFScreen(таблиця, ім'я)` встановлюється на всіх
//     30 реальних місцях виклику. Звичайні рядки пишуться ПЕРЕД делегуванням
//     оригіналу (поля вже лежать у таблицях до цього моменту і не
//     переписуються); рядки `@post:` — ПІСЛЯ (коли віджет уже має
//     дескриптор рушія `cp`, див. `PostPrefix` нижче). Кожен вузол шляху
//     перевіряється на існування, тож відсутній віджет (інша збірка гри,
//     інший мод) просто пропускається.
//
//     Таблиця — вбудований ресурс `Data/Bf2LayoutTable.txt`, заповнений
//     вручну за результатами прямого запуску скриптів меню гри й
//     піксельного виміру знімків.
//
// EN: Mechanism for applying the BF2 layout correction table.
//
//     The correction table (Data/Bf2LayoutTable.txt) holds 485 rows across
//     30 screens. Each row is added only after measuring a real in-game
//     screenshot; the table itself does not store a per-row verification
//     status — it is a format of numeric values, not a confirmation log.
//
//     ESTABLISHED FACTS THE APPROACH RELIES ON:
//       • no global "fix" is needed: vanilla at 1920x1080 is correct on
//         simple screens, and overriding ScriptCB_GetScreenInfo breaks 667
//         correctly self-compensating elements;
//       • sizes growing with the screen (xW/800, xH/600) is CORRECT
//         behaviour, not a defect: the vanilla audio screen at 1920 is
//         flawless;
//       • the engine stretches the background itself;
//       • only defects visible on a vanilla screenshot at the target
//         resolution are touched;
//       • a partial fix counts as a regression: a screen is either proven
//         to zero defects or left vanilla.
//
//     MECHANISM. A wrapper around `AddIFScreen(table, name)` is installed at
//     all 30 real call sites. Ordinary rows are written BEFORE delegating to
//     the original (fields already sit in the tables by then and are not
//     overwritten); `@post:` rows run AFTER (once the widget already holds
//     the engine's `cp` handle, see `PostPrefix` below). Each path node is
//     existence-checked, so a missing widget (a different game build or
//     mod) is simply skipped.
//
//     The table is the embedded resource `Data/Bf2LayoutTable.txt`,
//     hand-filled from directly executing the game's own menu scripts
//     and pixel-measuring screenshots.
// =============================================================================

using System.Globalization;
using System.Reflection;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Core.Bf2Widescreen;

public static class AnchorInheritancePatchBuilder
{
    // UA: Розмір екрана, під який писався авторський макет.
    // EN: The screen size the layout was authored for.
    public const float LayoutWidth = 800f;
    public const float LayoutHeight = 600f;

    private const string BackupName = "_ws_o_AddIFScreen";
    private const string TableResource = "BF1LocalizationTool.Core.Bf2Widescreen.Data.Bf2LayoutTable.txt";

    // UA: Прив'язка — це ЧАСТКА екрана, а не піксельна довжина, тож
    //     масштабуванню вона не підлягає за визначенням.
    //
    //     ЦІ ЧОТИРИ ПОЛЯ ВИКЛЮЧЕНІ З МАСШТАБУВАННЯ: BuildChunkFunction
    //     інакше множить АБСОЛЮТНО КОЖНЕ поле на коефіцієнт роздільності
    //     `scale` (~2.4× для 1920×1080 проти авторських 800×600). Це коректно
    //     для пікселів (bgexpandy, bgoffsety, x, y, width…), але:
    //       • ZPos — порядок малювання (сортувальний ключ), не довжина;
    //       • alpha — прозорість 0..1, не довжина;
    //       • ColorR/ColorG/ColorB — канали кольору, не довжина.
    //     Без цього списку, наприклад, `alpha=1.0` записувалось б як
    //     `1.0 * scale` (тобто 2.4 замість 1.0).
    // EN: An anchor is a screen FRACTION, not a pixel length, so by definition
    //     it must never be scaled.
    //
    //     THESE FOUR FIELDS ARE EXCLUDED FROM SCALING: otherwise
    //     BuildChunkFunction multiplies LITERALLY EVERY field by the
    //     resolution scale factor `scale` (~2.4x for 1920x1080 against the
    //     authored 800x600). That is correct for pixel values (bgexpandy,
    //     bgoffsety, x, y, width…), but:
    //       • ZPos is a draw-order sort key, not a length;
    //       • alpha is 0..1 opacity, not a length;
    //       • ColorR/ColorG/ColorB are color channels, not a length.
    //     Without this list, e.g. `alpha=1.0` would be written as
    //     `1.0 * scale` (i.e. 2.4 instead of 1.0).
    private static readonly HashSet<string> UnscaledFields =
        ["ScreenRelativeX", "ScreenRelativeY", "ZPos", "alpha", "ColorR", "ColorG", "ColorB"];

    // UA: Псевдошлях, що позначає запис у ГЛОБАЛИ Lua замість полів таблиці
    //     екрана (див. розгорнуте пояснення в BuildChunkFunction). Символ '@'
    //     не може трапитись в імені справжнього віджета, тож зіткнення виключене.
    // EN: Pseudo-path marking a write to Lua GLOBALS instead of screen-table
    //     fields (see the detailed rationale in BuildChunkFunction). '@' cannot
    //     occur in a real widget name, so a collision is impossible.
    private const string GlobalsPath = "@globals";

    // -------------------------------------------------------------------------
    // UA: Псевдошлях "@screen" — поля пишуться НАПРЯМУ в КОРІНЬ таблиці
    //     екрана (параметр функції-чанка), без жодного проходу GETTABLE.
    //     Потрібен для скалярних полів, що лежать прямо на екрані, а не у
    //     вкладеному віджеті — приклад: `movieX`/`movieY`/`movieW`/`movieH`
    //     на `ifs_missionselect`, які читає `ifelem_shellscreen_fnStartMovie`
    //     напряму з `this.movieX` тощо (без проміжної таблиці-віджета).
    //     Звичайний шлях (навіть однослівний, напр. "_Tabs") ЗАВЖДИ робить
    //     перший GETTABLE від кореня — цього не уникнути без спецшляху, бо
    //     `BuildChunkFunction` трактує Path як шлях ДО контейнера, чиї поля
    //     потім пишуться (SETTABLE виконується на РЕЗУЛЬТАТІ проходу, не на
    //     самому корені). Формат рядка: "ecran\t@screen\tmovieX=200;...".
    // EN: The "@screen" pseudo-path — fields are written DIRECTLY onto the
    //     screen table's ROOT (the chunk function's parameter), with no
    //     GETTABLE walk at all. Needed for scalar fields that live straight
    //     on the screen, not inside a nested widget — example:
    //     `movieX`/`movieY`/`movieW`/`movieH` on `ifs_missionselect`, read by
    //     `ifelem_shellscreen_fnStartMovie` directly off `this.movieX` etc.
    //     (no intermediate widget table). An ordinary path (even a one-word
    //     one like "_Tabs") ALWAYS does a first GETTABLE off the root — that
    //     cannot be avoided without a special path, because
    //     `BuildChunkFunction` treats Path as the route TO a container whose
    //     fields then get written (SETTABLE runs on the walk's RESULT, not on
    //     the root itself). Row format: "screen\t@screen\tmovieX=200;...".
    private const string ScreenPath = "@screen";

    // -------------------------------------------------------------------------
    // UA: Префікс шляху для правок «ПІСЛЯ ПОБУДОВИ». Звичайні рядки таблиці
    //     пишуть поля в таблицю-конфіг, і рушій читає їх КОЛИСЬ — інколи вже
    //     до точки патчу (доведено: btnw у RoundIFButtonLabel_fnSetSize,
    //     use_y у Build_Use). Такі поля виправити записом неможливо в принципі.
    //
    //     Ці ж рядки діють інакше: після повернення з оригінального
    //     AddIFScreen кожен віджет уже має дескриптор об'єкта рушія в полі
    //     'cp' (перевірено у VM: до виклику nil, після — реальне значення,
    //     23 дескриптори на екран). Маючи 'cp', нативний сеттер викликається
    //     напряму на вже створеному об'єкті, тож питання «коли рушій прочитав
    //     поле конфігу» зникає повністю.
    //
    //     Формат: "@post:шлях.до.віджета", наприклад "@post:action.misc.label".
    // EN: Path prefix for "AFTER BUILD" fixes. Ordinary table rows write into
    //     the config table, which the engine reads at SOME point — sometimes
    //     already before this patch point (proven: btnw in
    //     RoundIFButtonLabel_fnSetSize, use_y in Build_Use). Such fields simply
    //     cannot be fixed by writing.
    //
    //     These rows work differently: once the original AddIFScreen returns,
    //     every widget already holds an engine object handle in its 'cp' field
    //     (VM-verified: nil before the call, a real value after, 23 handles per
    //     screen). Given 'cp', the native setter is called directly on the
    //     already-created object, so "when did the engine read the config
    //     field" stops mattering entirely.
    //
    //     Формат: "@post:шлях.до.віджета", наприклад "@post:action.misc.label".
    //     Якщо перший сегмент починається з '@', коренем є ГЛОБАЛЬНА таблиця, а
    //     не таблиця екрана: "@post:@Popup_Tutorial.title" (див. пояснення в
    //     BuildPostChunkFunction).
    // EN: Format: "@post:path.to.widget", e.g. "@post:action.misc.label".
    //     If the first segment starts with '@', the root is a GLOBAL table
    //     rather than the screen table: "@post:@Popup_Tutorial.title" (see the
    //     rationale in BuildPostChunkFunction).
    private const string PostPrefix = "@post:";

    // -------------------------------------------------------------------------
    // UA: Псевдошлях "@postscreen" — ПІСЛЯ-БУДОВНИЙ близнюк "@screen": поля
    //     пишуться SETTABLE напряму в КОРІНЬ таблиці екрана (без GETTABLE-
    //     проходу, без 'cp'), але ПІСЛЯ оригінального AddIFScreen, а не до
    //     нього. Потрібен через порядок запису у власному конструкторі
    //     екрана (f35, ifs_missionselect):
    //         pc92: SETTABLE R0["movieW"] = 510.0   (константа, БЕЗУМОВНО)
    //         pc93: SETTABLE R0["movieH"] = 400.0   (константа, БЕЗУМОВНО)
    //     — жодного TEST/TESTSET перед цими інструкціями немає: конструктор
    //     переписує movieW/movieH БЕЗУМОВНО, не питаючи "чи вже є
    //     значення". "@screen" (пре-білд) пише СВОЄ значення ДО виклику
    //     оригінального AddIFScreen — тобто до того, як f35 узагалі
    //     запуститься, — тож щойно AddIFScreen делегує керування f35, цей
    //     запис одразу затирається літералами 510/400. "@post:" тут не
    //     підходить: він завжди робить GETTABLE-прохід по шляху, трактуючи
    //     перший сегмент як вкладений об'єкт з дескриптором 'cp' або
    //     подальшими полями — а movieW/movieH є ПРОСТИМ ЧИСЛОМ на корені,
    //     не таблицею й не віджетом, тож GETTABLE R0["movieW"] дав би число,
    //     а не таблицю для подальшого проходу. Формат рядка:
    //     "екран\t@postscreen\tmovieW=125;...".
    // EN: The "@postscreen" pseudo-path — the AFTER-BUILD twin of "@screen":
    //     fields are written via SETTABLE straight onto the screen table's
    //     root (no GETTABLE walk, no 'cp'), but AFTER the original
    //     AddIFScreen call rather than before it. Needed because of the
    //     write order inside the screen's own constructor (f35,
    //     ifs_missionselect):
    //         pc92: SETTABLE R0["movieW"] = 510.0   (literal, UNCONDITIONAL)
    //         pc93: SETTABLE R0["movieH"] = 400.0   (literal, UNCONDITIONAL)
    //     — no TEST/TESTSET guards this pair at all: the constructor
    //     overwrites movieW/movieH unconditionally, never checking "is a
    //     value already there". "@screen" (pre-build) writes its value
    //     BEFORE the original AddIFScreen call — i.e. before f35 even runs —
    //     so the moment AddIFScreen hands off to f35, that write is
    //     immediately clobbered by the 510/400 literals. "@post:" cannot
    //     substitute: it always performs a GETTABLE walk that treats the
    //     first path segment as a nested object holding a 'cp' handle or
    //     further fields — but movieW/movieH are PLAIN NUMBERS on the root,
    //     not a table or widget, so GETTABLE R0["movieW"] would yield a
    //     number where a table is expected for the walk to continue. Row
    //     format: "screen\t@postscreen\tmovieW=125;...".
    private const string PostScreenPath = "@postscreen";

    // -------------------------------------------------------------------------
    // UA: Дозволені операції «після побудови»: псевдополе -> (натив, скільки
    //     числових аргументів після дескриптора). Список НАВМИСНО короткий:
    //     кожен натив тут має бути підтверджений у переліку експортів
    //     BattlefrontII.exe (577 ScriptCB_*, з них 108 рушій має, але жоден
    //     скрипт не викликає — саме звідси ці два).
    //
    //     Для 2-аргументної операції значення беруться з полів <ім'я> та
    //     <ім'я2> (наприклад textw=..;textw2=..), бо формат таблиці — плоскі
    //     пари «поле=число», а SetTextBox потребує обидва разом.
    //
    //     ЖОРСТКЕ ПРАВИЛО: сюди можна додавати ЛИШЕ нативи, чия сигнатура
    //     ПІДТВЕРДЖЕНА дизасемблюванням реального виклику в скриптах гри.
    //     Наявності імені в exe НЕ ДОСИТЬ. Перевірено в грі:
    //     ScriptCB_IFObj_SetWidthHeight(cp, w, h) — ім'я в exe є, але
    //     скрипти його не викликають, і здогадана сигнатура призводить до
    //     того, що всі нижні кнопки ЗНИКАЮТЬ з екрана.
    //     Обидва нативи нижче мають підтверджені місця виклику в
    //     interface_util (common.lvl):
    //       root/15 pc3-6 : GETGLOBAL SetLeading; GETTABLE cp; MOVE v; CALL b=3
    //                       -> SetLeading(cp, value)
    //       root/18 pc3-7 : GETGLOBAL SetTextBox; GETTABLE cp; MOVE w; MOVE h;
    //                       CALL b=4 -> SetTextBox(cp, w, h)
    // EN: Allowed "after build" operations: pseudo-field -> (native, number of
    //     numeric arguments after the handle).
    //
    //     For the 2-argument operation the values come from fields <name> and
    //     <name>2 (e.g. textw=..;textw2=..), because the table format is flat
    //     "field=number" pairs while SetTextBox needs both at once.
    //
    //     HARD RULE: only natives whose signature is CONFIRMED by
    //     disassembling a real call in the game's own scripts may be
    //     listed here. Presence of the name in the exe is NOT enough.
    //     Tested in-game: ScriptCB_IFObj_SetWidthHeight(cp, w, h) — the
    //     name exists in the exe, but no script calls it, and the guessed
    //     signature makes every bottom button VANISH from the screen.
    //     Both natives below have confirmed call sites in interface_util
    //     (common.lvl):
    //       root/15 pc3-6 : GETGLOBAL SetLeading; GETTABLE cp; MOVE v; CALL b=3
    //                       -> SetLeading(cp, value)
    //       root/18 pc3-7 : GETGLOBAL SetTextBox; GETTABLE cp; MOVE w; MOVE h;
    //                       CALL b=4 -> SetTextBox(cp, w, h)
    private static readonly Dictionary<string, (string Native, int Args)> PostOps =
        new(StringComparer.Ordinal)
        {
            ["leading"] = ("ScriptCB_IFText_SetLeading", 1),
            ["textw"] = ("ScriptCB_IFText_SetTextBox", 2),
        };

    // -------------------------------------------------------------------------
    // UA: Операції «після побудови», які викликають LUA-МЕТОД, а не натив.
    //     Відмінність принципова: натив приймає дескриптор 'cp', а метод —
    //     САМУ ТАБЛИЦЮ віджета. Тому вони емітуються окремо.
    //
    //     Навіщо: деякі речі рушій не вміє змінити одним сеттером, зате гра
    //     має готовий метод, який робить це узгоджено. Приклад —
    //     gButtonWindow_fnSetSize(self, w, h) з ifelem_buttonwindow (root/3):
    //       self.width = w; self.height = h
    //       if self.skin then gButtonWindowSkin_fnSetSize(self.skin, w, h)
    //     а той, своєю чергою, викликає IFBorder_fnSetTexturePos із прямокутником
    //     (-w/2, -h/2, w/2, h/2), тобто РОЗТЯГУЄ САМУ ПІДКЛАДКУ. Зробити те саме
    //     набором окремих записів полів неможливо: поля width/height
    //     споживаються під час створення попапа, задовго до будь-якої
    //     точки патчу.
    //
    //     Те саме правило, що й для нативів: додавати лише те, чия сигнатура
    //     підтверджена дизасемблюванням (numparams і порядок аргументів).
    // EN: "After build" operations that call a LUA METHOD rather than a native.
    //     The difference matters: a native takes the 'cp' handle, a method takes
    //     the widget TABLE itself. Hence they are emitted separately.
    //
    //     Why: some things the engine cannot change through a single setter, yet
    //     the game already ships a method that does it coherently. Example —
    //     gButtonWindow_fnSetSize(self, w, h) from ifelem_buttonwindow (root/3):
    //       self.width = w; self.height = h
    //       if self.skin then gButtonWindowSkin_fnSetSize(self.skin, w, h)
    //     which in turn calls IFBorder_fnSetTexturePos with the rectangle
    //     (-w/2, -h/2, w/2, h/2), i.e. it STRETCHES THE BACKDROP ITSELF. The same
    //     cannot be achieved by writing fields: width/height are consumed while
    //     the popup is created, long before any patch point of ours.
    //
    //     Same rule as for natives: only add entries whose signature is confirmed
    //     by disassembly (numparams and argument order).
    //
    // UA: "bordersize" -> BorderRectSkin_fnSetSize(self, w, h) з ifelem_borderrect
    //     (root/0, numparams=3). Сигнатуру звірено дизасемблюванням ПОВНІСТЮ,
    //     а не за іменем:
    //       pc0-5   : якщо w < 32 то cornerW = w*0.5 інакше 16
    //       pc6-11  : якщо h < 32 то cornerH = h*0.5 інакше 16
    //       pc12-20 : GETGLOBAL IFBorder_fnSetTexturePos;
    //                 CALL a=5 b=8 -> 7 аргументів у r6..r12:
    //                 (self, w*-0.5, h*-0.5, w*0.5, h*0.5, cornerW, cornerH)
    //     Тобто підкладка розтягується СИМЕТРИЧНО ВІД ВЛАСНОГО ЦЕНТРА, а кути
    //     дев'ятизонної текстури лишаються цілими (не розтягуються), доки
    //     сторона >= 32 px. Саме тому це безпечніше за «здогадані» сеттери:
    //     функція сама рахує кути й не спотворює рамку.
    //
    //     ВАЖЛИВО ПРО ЦЕНТР: оскільки розтяг симетричний, самої зміни розміру
    //     мало — якщо потрібен зсув краю лише в один бік, разом із
    //     "bordersize" треба задати ще й "posy" (обидва в одному рядку
    //     таблиці; порядок виконання в BuildPostChunkFunction: спершу
    //     Lua-методи, потім posy).
    // EN: "bordersize" -> BorderRectSkin_fnSetSize(self, w, h) from
    //     ifelem_borderrect (root/0, numparams=3). Signature verified by FULL
    //     disassembly, not by name:
    //       pc0-5   : if w < 32 then cornerW = w*0.5 else 16
    //       pc6-11  : if h < 32 then cornerH = h*0.5 else 16
    //       pc12-20 : GETGLOBAL IFBorder_fnSetTexturePos;
    //                 CALL a=5 b=8 -> 7 args in r6..r12:
    //                 (self, w*-0.5, h*-0.5, w*0.5, h*0.5, cornerW, cornerH)
    //     So the backdrop stretches SYMMETRICALLY ABOUT ITS OWN CENTRE while
    //     the nine-slice corners stay intact (unstretched) as long as the side
    //     is >= 32 px. That is precisely why this is safer than "guessed"
    //     setters: the function computes the corners itself and does not
    //     distort the frame.
    //
    //     NOTE ON THE CENTRE: because the stretch is symmetric, resizing alone
    //     is not enough — if only one edge must move, pair "bordersize" with
    //     "posy" (both on the same table row; execution order in
    //     BuildPostChunkFunction is Lua methods first, then posy).
    private static readonly Dictionary<string, (string Fn, int Args)> PostLuaOps =
        new(StringComparer.Ordinal)
        {
            ["winsize"] = ("gButtonWindow_fnSetSize", 2),
            ["bordersize"] = ("BorderRectSkin_fnSetSize", 2),
        };

    // -------------------------------------------------------------------------
    // UA: Окрема операція: зсунути елемент по ВЕРТИКАЛІ, не чіпаючи x і z.
    //     Реалізується через IFObj_fnSetPos(self, x, y, z) з interface_util
    //     (root/2, numparams=4). Ключова властивість, заради якої обрано саме
    //     її: функція NIL-ТОЛЕРАНТНА — кожен аргумент проходить через ідіому
    //     "новий or поточний", тож передавши nil замість x і z, вони лишаються
    //     недоторканими, а змінюється тільки y.
    //     Це важливо: z відповідає за порядок накладання, і виставити його
    //     навмання означало б сховати елемент за фоном — рівно та категорія
    //     помилки, що вже спричиняла зникнення кнопок.
    //     Наприкінці функція сама викликає ScriptCB_IFObj_SetPos(cp, x, y, z),
    //     тобто зміна одразу застосовується до об'єкта рушія.
    // EN: A separate operation: shift an element VERTICALLY without touching x
    //     or z. Implemented via IFObj_fnSetPos(self, x, y, z) from
    //     interface_util (root/2, numparams=4). The decisive property is that
    //     the function is NIL-TOLERANT — each argument goes through a
    //     "new or current" idiom, so passing nil for x and z leaves them intact
    //     and changes only y.
    //     That matters: z drives draw order, and setting it blindly would hide
    //     the element behind the background — exactly the class of mistake that
    //     already caused the vanished buttons.
    //     At the end the function itself calls ScriptCB_IFObj_SetPos(cp, x, y,
    //     z), so the change reaches the engine object immediately.
    private const string PosYOp = "posy";
    private const string PosYFn = "IFObj_fnSetPos";

    // UA: Горизонтальний близнюк "posy" — той самий нативний виклик, той самий
    //     нуль-толерантний контракт, просто заповнює x замість y (лишаючи y і z
    //     недоторканими, якщо вказано лише "posx"). Додано після того, як
    //     дефект D-2 (синя смуга заголовків ifs_mp_sessionlist) показав, що
    //     bgexpandx/bgoffsetx на FlashyText-фоні мають різку нелінійність/
    //     насичення вже за межами приблизно ±1000 — тобто НЕ придатні для
    //     зсуву на сотні пікселів. IFObj_fnSetPos, навпаки, вже двічі
    //     підтверджено ТОЧНО лінійним (1:1) для Y на цьому ж класі об'єктів
    //     (serverinfo, listbox.titleBarElement) — тож для горизонтальних
    //     зсувів того самого порядку логічно очікувати ту саму поведінку.
    // EN: The horizontal twin of "posy" — same native call, same nil-tolerant
    //     contract, just fills x instead of y (leaving y and z untouched when
    //     only "posx" is given). Added after defect D-2 (the ifs_mp_sessionlist
    //     header bar) showed that bgexpandx/bgoffsetx on a FlashyText backdrop
    //     have sharp non-linearity/saturation beyond roughly ±1000 — unsuitable
    //     for shifts of hundreds of pixels. IFObj_fnSetPos, by contrast, has
    //     already been confirmed EXACTLY linear (1:1) for Y twice on this same
    //     class of object (serverinfo, listbox.titleBarElement) — so the same
    //     behavior is the reasonable expectation for horizontal shifts of a
    //     similar magnitude.
    private const string PosXOp = "posx";

    // -------------------------------------------------------------------------
    // UA: "@hook:" — ПОСТІЙНА ОБГОРТКА РАНТАЙМ-СЕТТЕРА.
    //
    //     ЧОМУ ЦЕ ОКРЕМИЙ МЕХАНІЗМ, А НЕ ЧЕРГОВЕ ЧИСЛО В ТАБЛИЦІ.
    //     Геометрія тече ДВОМА каналами — таблиці
    //     конструкторів і рантайм-сеттери. "@post:" накриває лише перший:
    //     він виконується ОДИН раз, одразу після AddIFScreen. Усе, що гра
    //     пересуває пізніше (перемикання вкладки, вибір карти, зміна
    //     сторінки), затирає це значення, і жодне інше число в
    //     Bf2LayoutTable.txt цього не змінить.
    //     Виміряно на `ifs_missionselect` (7 знімків, по одному на вкладку):
    //     у стані "Сеанс" `posy` діє з нахилом рівно 1.000,
    //     у стані будь-якої вкладки налаштувань — не діє взагалі (попіксельний
    //     diff двох збірок: різниця нуль). Це і є "Канал 2" у чистому вигляді.
    //
    //     ФОРМАТ РЯДКА: "@hook:<ГлобальнаФункція>:<шлях.до.віджета>".
    //     Приклад:
    //       ifs_missionselect  @hook:ifelem_tabmanager_SetSelected:option_buttons.setting  posx=-100;posy=-15
    //
    //     ЩО РОБИТЬ ОБГОРТКА (на КОЖЕН виклик, а не один раз):
    //       1) делегує оригіналу (збереженому в глобалі "<Fn>_ws_o"),
    //          передаючи ті самі аргументи — уся штатна поведінка лишається;
    //       2) ПІСЛЯ повернення оригіналу заново накладає поправку на
    //          вказаний віджет через уже підтверджений IFObj_fnSetPos.
    //
    //     ЧОМУ ТАБЛИЦЮ ЕКРАНА БЕРЕМО З ГЛОБАЛА, А НЕ З АРГУМЕНТІВ: кожен
    //     екран `ifs_*` живе у глобалі під власним іменем (перевірено у VM:
    //     vm.G["ifs_missionselect"] — та сама таблиця, що й будувалась). Це
    //     дозволяє одній обгортці обслуговувати кілька екранів і не залежати
    //     від того, які саме аргументи має конкретний сеттер.
    //
    //     БЕЗПЕКА (та сама триярусна дисципліна, що й у BuildPostChunkFunction):
    //       * немає оригіналу -> обгортка нічого не викликає й тихо виходить;
    //       * немає глобала екрана або будь-якого вузла шляху -> крок
    //         пропускається (інша збірка/мод);
    //       * немає самого IFObj_fnSetPos -> крок пропускається.
    //     Встановлення ІДЕМПОТЕНТНЕ: якщо "<Fn>_ws_o" уже існує, повторне
    //     обгортання не відбувається (інакше кожен AddIFScreen додавав би ще
    //     один шар і рекурсія з'їла б стек).
    //
    //     ФІКСОВАНІ 6 ПАРАМЕТРІВ: обгортка оголошена з numparams=6 і передає
    //     рівно їх. Це покриває всі поточні цілі (найдовша —
    //     ifelem_tabmanager_SetPos, np=6); зайві nil-и Lua мовчки відкидає,
    //     якщо оригінал приймає менше. Результат оригіналу НЕ повертається
    //     (CALL з c=1): усі цілі цього класу — процедури-сеттери.
    // EN: "@hook:" — A PERMANENT WRAPPER AROUND A RUNTIME SETTER.
    //
    //     WHY THIS IS A SEPARATE MECHANISM, NOT YET ANOTHER NUMBER.
    //     Geometry flows through TWO channels —
    //     constructor tables and runtime setters. "@post:" covers only the
    //     first: it runs ONCE, right after AddIFScreen. Anything the game
    //     moves later (tab switch, map selection, page change) overwrites that
    //     value, and no other number in Bf2LayoutTable.txt can change that.
    //     Measured on `ifs_missionselect` (7 screenshots, one per tab):
    //     in the "Сеанс" state the `posy` correction applies with a slope of
    //     exactly 1.000; in any settings-tab state it does not apply at all
    //     (per-pixel diff of the two builds: zero difference). That is
    //     "Channel 2" in its purest form.
    //
    //     ROW FORMAT: "@hook:<GlobalFunction>:<path.to.widget>".
    //     Example:
    //       ifs_missionselect  @hook:ifelem_tabmanager_SetSelected:option_buttons.setting  posx=-100;posy=-15
    //
    //     WHAT THE WRAPPER DOES (on EVERY call, not once):
    //       1) delegates to the original (kept in the global "<Fn>_ws_o"),
    //          forwarding the same arguments — all stock behaviour stays;
    //       2) AFTER the original returns, re-applies the correction to the
    //          named widget through the already-proven IFObj_fnSetPos.
    //
    //     WHY THE SCREEN TABLE COMES FROM A GLOBAL, NOT FROM THE ARGUMENTS:
    //     every `ifs_*` screen lives in a global under its own name
    //     (VM-verified: vm.G["ifs_missionselect"] is the very table that was
    //     built). That lets one wrapper serve several screens without
    //     depending on which arguments a particular setter happens to take.
    //
    //     SAFETY (the same three-layer discipline as BuildPostChunkFunction):
    //       * no original -> the wrapper calls nothing and exits quietly;
    //       * no screen global, or any path node missing -> the step is
    //         skipped (a different build/mod);
    //       * no IFObj_fnSetPos -> the step is skipped.
    //     Installation is IDEMPOTENT: if "<Fn>_ws_o" already exists, no second
    //     wrapping happens (otherwise every AddIFScreen would add another
    //     layer and the recursion would eat the stack).
    //
    //     FIXED 6 PARAMETERS: the wrapper is declared with numparams=6 and
    //     forwards exactly those. That covers every current target (the
    //     longest is ifelem_tabmanager_SetPos, np=6); Lua silently drops the
    //     extra nils when the original takes fewer. The original's result is
    //     NOT returned (CALL with c=1): every target of this class is a
    //     setter procedure.
    // -------------------------------------------------------------------------
    private const string HookPrefix = "@hook:";
    private const string HookBackupSuffix = "_ws_o";
    private const int HookWrapperParams = 6;

    // -------------------------------------------------------------------------
    // UA: "@initlist:" — ЄДИНА ТОЧКА, ДЕ ЩЕ МОЖНА ВПЛИНУТИ НА РОЗМІР СПИСКУ.
    //
    //     ЗАДАЧА: списки "Карта"/"Список" на ifs_missionselect(_pcMulti) мають
    //     фіксовану "робочу зону" — 22 видимі рядки завширшки 146px — і вона
    //     НЕ росте при збільшенні рамки через "@post: winsize". Через це
    //     вміст висить посеред великої рамки замість того, щоб заповнювати її
    //     від лівого верхнього краю, як в оригіналі.
    //
    //     ЧОМУ НЕ ПРАЦЮЄ ЖОДЕН НАЯВНИЙ МЕХАНІЗМ (перевірено дизасемблюванням
    //     ifs_missionselect_pcmulti, root):
    //         pc441-444: fnBuildScreen(таблиця екрана)   <- будує ВСЕ, разом з
    //                    fnAddListboxes -> ListManager_fnInitList
    //         pc447-450: AddIFScreen(таблиця екрана, ім'я)
    //     Тобто списки вже ПОБУДОВАНІ, коли керування доходить до AddIFScreen —
    //     єдиної точки входу. Ні звичайні рядки (до AddIFScreen), ні
    //     "@post:" (після) не встигають: `ListManager_fnInitList` читає
    //     showcount/width ОДИН РАЗ і одразу створює рівно `showcount`
    //     віджетів-рядків через CreateFn. Пізніший запис у таблицю-дескриптор
    //     (навіть через "@globals.поле") вже нічого не змінює.
    //
    //     РІШЕННЯ: обгорнути САМУ `ListManager_fnInitList` — вона ГЛОБАЛЬНА і
    //     живе в common.lvl (ifelem_listmanager), тобто існує задовго до
    //     екранів. Обгортка отримує (listbox, layout), порівнює `layout` з
    //     названим ГЛОБАЛОМ-дескриптором і, якщо збіглось, дописує в нього
    //     потрібні поля — і лише тоді делегує оригіналу. Момент ідеальний:
    //     це рівно та мить, коли оригінал ось-ось прочитає ці поля.
    //
    //     ФОРМАТ РЯДКА: "@initlist:<ГлобальнаТаблиця-дескриптор>", напр.
    //       ifs_missionselect  @initlist:ifs_mspc_MapList_layout  showcount=41;width=386
    //     Ім'я екрана в рядку — лише для групування й читабельності: обгортка
    //     ставиться ОДИН РАЗ і діє глобально (порівняння йде за самою таблицею,
    //     а не за екраном).
    //
    //     ЗНАЧЕННЯ НЕ МАСШТАБУЮТЬСЯ. Виміряно, що ці поля рушій трактує як
    //     фінальні пікселі (нативні showcount=22 * (yHeight20+ySpacing-5) =
    //     330px — рівно висота видимої зони на 1920x1080), тож множення на
    //     `scale` тут було б помилкою.
    //
    //     БЕЗПЕКА: (1) немає такої глобальної функції — обгортка не ставиться;
    //     (2) уже обгорнуто — виходимо (ідемпотентність, як у "@hook:");
    //     (3) немає такого глобала-дескриптора — порівняння просто не збігається;
    //     (4) немає резерву оригіналу — не викликаємо нічого (замість помилки).
    // EN: "@initlist:" — THE ONLY REMAINING POINT WHERE A LIST'S SIZE CAN STILL
    //     BE INFLUENCED.
    //
    //     THE PROBLEM: the "Карта"/"Список" lists on ifs_missionselect(_pcMulti)
    //     have a fixed "working area" — 22 visible rows, 146px wide — and it
    //     does NOT grow when the frame is enlarged via "@post: winsize". So the
    //     content floats in the middle of a large frame instead of filling it
    //     from the top-left corner the way the original does.
    //
    //     WHY NO EXISTING MECHANISM WORKS (verified by disassembling
    //     ifs_missionselect_pcmulti, root):
    //         pc441-444: fnBuildScreen(screen table)  <- builds EVERYTHING,
    //                    including fnAddListboxes -> ListManager_fnInitList
    //         pc447-450: AddIFScreen(screen table, name)
    //     So the lists are ALREADY BUILT by the time control reaches
    //     AddIFScreen — the only entry point. Neither ordinary rows (before
    //     AddIFScreen) nor "@post:" (after) are early enough:
    //     `ListManager_fnInitList` reads showcount/width ONCE and immediately
    //     creates exactly `showcount` row widgets via CreateFn. Writing to the
    //     descriptor table later (even via "@globals.field") changes nothing.
    //
    //     THE FIX: wrap `ListManager_fnInitList` itself — it is GLOBAL and
    //     lives in common.lvl (ifelem_listmanager), i.e. it exists long before
    //     any screen. The wrapper receives (listbox, layout), compares `layout`
    //     against the named descriptor GLOBAL and, on a match, writes the
    //     requested fields into it — and only then delegates to the original.
    //     The timing is exact: this is the very moment before the original
    //     reads those fields.
    //
    //     ROW FORMAT: "@initlist:<GlobalDescriptorTable>", e.g.
    //       ifs_missionselect  @initlist:ifs_mspc_MapList_layout  showcount=41;width=386
    //     The screen name in the row is only for grouping and readability: the
    //     wrapper is installed ONCE and acts globally (the match is on the
    //     table itself, not on the screen).
    //
    //     VALUES ARE NOT SCALED. These fields are measured to be final pixels
    //     for the engine (native showcount=22 * (yHeight20+ySpacing-5) = 330px
    //     — exactly the visible area's height at 1920x1080), so multiplying by
    //     `scale` here would be a mistake.
    //
    //     SAFETY: (1) no such global function — the wrapper is not installed;
    //     (2) already wrapped — bail out (idempotent, as with "@hook:");
    //     (3) no such descriptor global — the comparison simply never matches;
    //     (4) no backup of the original — nothing is called (instead of erroring).
    // -------------------------------------------------------------------------
    private const string InitListPrefix = "@initlist:";
    private const string InitListFn = "ListManager_fnInitList";

    // -------------------------------------------------------------------------
    // UA: ХУК НА Popup_Tutorial.SetPage (вікно довідки, кнопки Назад/OK/Далі).
    //
    //     ЧОМУ НЕ "@post": одноразовий "@post" пише значення ОДИН РАЗ, одразу
    //     після побудови екрана. Дизасемблювання конструктора попапу показало:
    //     Popup_Tutorial.SetPage(self, page) викликається на КОЖНУ зміну
    //     сторінки (і на перший показ попапу теж) і сама, зсередини, викликає
    //     gPopup_fnSetTitle_Internal — той нативно переукладає title/buttons на
    //     основі РЕАЛЬНОГО виміру тексту (IFText_fnGetDisplayRect). Тобто
    //     будь-який одноразовий запис миттєво затирається першим-таки викликом
    //     SetPage. Це саме та причина, чому спроба
    //     "@post:@Popup_Tutorial winsize=..." дала нульовий ефект.
    //
    //     РІШЕННЯ: не одноразовий запис, а ПОСТІЙНА ОБГОРТКА самого SetPage,
    //     що виконується на КОЖЕН його виклик:
    //       1) делегує оригіналу (Popup_Tutorial._ws_o_SetPage) — уся наявна
    //          поведінка (текст сторінки, видимість Назад/Далі) лишається;
    //       2) міряє РЕАЛЬНУ висоту абзацу нативом IFText_fnGetDisplayRect
    //          на Popup_Tutorial.title.cp (те саме, що вже робить сам рушій
    //          усередині gPopup_fnSetTitle_Internal — підтверджений виклик,
    //          не здогад);
    //       3) ставить Popup_Tutorial.buttons ПІСЛЯ виміряного низу тексту
    //          (y2) плюс невеликий запас, через уже підтверджений
    //          IFObj_fnSetPos.
    //     Це "страхувальний" фікс НАД власною (як з'ясувалось, недостатньою)
    //     логікою рушія, а не заміна її.
    //
    //     ВСТАНОВЛЕННЯ: Popup_Tutorial — глобал, якого може ще не існувати в
    //     момент роботи кореневого інсталятора (він живе в скрипті
    //     popup_tutorial, що вантажиться пізніше за точку входу). Тому
    //     встановлення відкладене: BuildPopupTutorialInstallStep викликається
    //     БЕЗУМОВНО після КОЖНОГО реального AddIFScreen (дешево — кілька
    //     nil-перевірок) і сама собою ідемпотентна: якщо
    //     Popup_Tutorial._ws_o_SetPage вже існує — вихід одразу, повторне
    //     обгортання неможливе.
    //
    //     СТАТУС: НЕ ПРОТЕСТОВАНО. Запас (margin) обчислено з логіки (невеликий
    //     проміжок після реально виміряного низу тексту), АЛЕ не підтверджено
    //     знімком гри — обов'язково перевірити на всіх трьох вкладках ГЗ
    //     (Перемістити/Бонус/Бійці) перед тим, як вважати завершеним.
    // EN: HOOK ON Popup_Tutorial.SetPage (the help window, Back/OK/Next
    //     buttons).
    //
    //     WHY NOT "@post": a one-time "@post" write runs ONCE, right after
    //     the screen builds. Disassembling the popup's own constructor showed:
    //     Popup_Tutorial.SetPage(self, page) runs on EVERY page change (and on
    //     the popup's first show too) and itself calls
    //     gPopup_fnSetTitle_Internal, which natively re-lays-out title/buttons
    //     from a REAL text measurement (IFText_fnGetDisplayRect). So any
    //     one-time write gets overwritten by the very first SetPage call.
    //     That is exactly why "@post:@Popup_Tutorial winsize=..." had zero
    //     effect.
    //
    //     FIX: not a one-time write, but a PERMANENT WRAPPER around SetPage
    //     itself, run on EVERY call:
    //       1) delegates to the original (Popup_Tutorial._ws_o_SetPage) — all
    //          existing behaviour (page text, Back/Next visibility) stays;
    //       2) measures the REAL paragraph height via the native
    //          IFText_fnGetDisplayRect on Popup_Tutorial.title.cp (the same
    //          call the engine itself already makes inside
    //          gPopup_fnSetTitle_Internal — a confirmed call, not a guess);
    //       3) places Popup_Tutorial.buttons AFTER the measured text bottom
    //          (y2) plus a small margin, via the already-confirmed
    //          IFObj_fnSetPos.
    //     This is a safety-net fix LAYERED ON TOP of the engine's own (as it
    //     turns out, insufficient) logic, not a replacement for it.
    //
    //     INSTALLATION: Popup_Tutorial is a global that may not exist yet
    //     when the root installer runs (it lives in the popup_tutorial
    //     script, loaded later than the entry point). So installation is
    //     deferred: BuildPopupTutorialInstallStep runs UNCONDITIONALLY after
    //     EVERY real AddIFScreen call (cheap — a few nil checks) and is
    //     itself idempotent: if Popup_Tutorial._ws_o_SetPage already exists,
    //     it exits immediately — double-wrapping is impossible.
    //
    //     STATUS: NOT TESTED. The margin is derived from reasoning (a small gap
    //     after the actually-measured text bottom), NOT yet confirmed by an
    //     in-game screenshot — must be checked on all three GC tabs
    //     (Move/Bonus/Troops) before this is considered done.
    // -------------------------------------------------------------------------
    private const string PopupTutorialGlobal = "Popup_Tutorial";
    private const string SetPageField = "SetPage";
    private const string SetPageBackupField = "_ws_o_SetPage";
    private const string GetDisplayRectFn = "IFText_fnGetDisplayRect";
    private const float PopupButtonSafetyMargin = 8f;

    // UA: "gButtonWindow_fnSetSize" (розтягування самої рамки попапа) НЕ
    //     є тут константою навмисно — спроба викликати його з цього хука
    //     зламала відображення рамки взагалі (див. коментар у
    //     BuildPopupTutorialSetPageWrapper) і була відкликана. PostLuaOps
    //     нижче й далі містить його для ІНШОГО, підтвердженого механізму
    //     (одноразовий @post) — це окремий, непов'язаний випадок.
    // EN: "gButtonWindow_fnSetSize" (stretching the popup's own border) is
    //     deliberately NOT a constant here — calling it from this hook broke
    //     the border's rendering entirely (see the comment in
    //     BuildPopupTutorialSetPageWrapper) and was withdrawn. PostLuaOps
    //     below still carries it for a DIFFERENT, confirmed mechanism
    //     (the one-time @post) — that is a separate, unrelated case.

    // -------------------------------------------------------------------------
    // UA: Сама обгортка Popup_Tutorial.SetPage(self, page) — див. пояснення
    //     вище. numparams=2 (self, page), без апвалью — усе читається з
    //     полів self.
    // EN: The Popup_Tutorial.SetPage(self, page) wrapper itself — see the
    //     rationale above. numparams=2 (self, page), no upvalues — everything
    //     is read from self's fields.
    // -------------------------------------------------------------------------
    private static LuaFunctionPrototype BuildPopupTutorialSetPageWrapper(string path)
    {
        var b = new Lua50FunctionBuilder { NumParams = 2, IsVararg = 0, MaxStackSize = 14 };
        var k = new ConstantCache(b);

        // UA: 1) делегувати оригіналу: self._ws_o_SetPage(self, page)
        // EN: 1) delegate to the original: self._ws_o_SetPage(self, page)
        b.Emit(LuaOpcode.GetTable, a: 2, b: 0, c: Lua50FunctionBuilder.Rk(k.Str(SetPageBackupField)));
        b.EmitTest(register: 2, c: 0);
        var noOriginal = b.EmitJumpPlaceholder();
        b.Emit(LuaOpcode.Move, a: 3, b: 0);   // self
        b.Emit(LuaOpcode.Move, a: 4, b: 1);   // page
        b.Emit(LuaOpcode.Call, a: 2, b: 3, c: 1);
        b.PatchJump(noOriginal, b.NextPc);

        // UA: 2) виміряти реальну висоту self.title і 3) опустити self.buttons
        //     нижче неї — кожен крок з власним nil-захистом (та сама
        //     трирівнева дисципліна, що й у BuildPostChunkFunction).
        // EN: 2) measure self.title's real height, 3) drop self.buttons below
        //     it — each step nil-guarded (the same three-layer discipline as
        //     in BuildPostChunkFunction).
        b.Emit(LuaOpcode.GetTable, a: 2, b: 0, c: Lua50FunctionBuilder.Rk(k.Str("title")));
        b.EmitTest(register: 2, c: 0);
        var noTitle = b.EmitJumpPlaceholder();

        // UA: 'cp' тут — лише перевірка готовності віджета (той самий guard,
        //     що й скрізь у файлі), а НЕ аргумент виклику. Дизасемблювання
        //     gPopup_fnSetTitle_Internal (pc36-38: GETTABLE self.title ->
        //     CALL IFText_fnGetDisplayRect) показало: рушій передає ТАБЛИЦЮ
        //     self.title, а НЕ title.cp — IFText_fnGetDisplayRect, як і
        //     IFObj_fnSetPos, є lua-обгорткою, що сама читає .cp всередині.
        // EN: 'cp' here is only a readiness guard (the same pattern used
        //     throughout this file), NOT the call argument. Disassembling
        //     gPopup_fnSetTitle_Internal (pc36-38: GETTABLE self.title ->
        //     CALL IFText_fnGetDisplayRect) showed: the engine passes the
        //     self.title TABLE, not title.cp — IFText_fnGetDisplayRect, like
        //     IFObj_fnSetPos, is a Lua wrapper that reads .cp internally.
        b.Emit(LuaOpcode.GetTable, a: 3, b: 2, c: Lua50FunctionBuilder.Rk(k.Str("cp")));
        b.EmitTest(register: 3, c: 0);
        var noCp = b.EmitJumpPlaceholder();

        b.EmitABx(LuaOpcode.GetGlobal, a: 4, bx: k.Str(GetDisplayRectFn));
        b.EmitTest(register: 4, c: 0);
        var noNative = b.EmitJumpPlaceholder();

        b.Emit(LuaOpcode.Move, a: 5, b: 2);                  // self.title (ТАБЛИЦЯ, не cp!)
        b.Emit(LuaOpcode.Call, a: 4, b: 2, c: 5);             // 1 арг, 4 результати -> r4..r7 = x1,y1,x2,y2

        b.EmitTest(register: 7, c: 0);                        // y2 присутнє?
        var noY2 = b.EmitJumpPlaceholder();

        // UA: Рамку попапа тут НЕ розтягують через gButtonWindow_fnSetSize —
        //     цей виклик ламає рендер чорної підкладки попапа. Кнопки
        //     (нижче) зсуваються під текст незалежно від рамки.
        // EN: The popup's border is NOT stretched here via
        //     gButtonWindow_fnSetSize — that call breaks the popup's
        //     black-backdrop rendering. The buttons (below) are shifted
        //     under the text independently of the border.

        b.Emit(LuaOpcode.GetTable, a: 8, b: 0, c: Lua50FunctionBuilder.Rk(k.Str("buttons")));
        b.EmitTest(register: 8, c: 0);
        var noButtons = b.EmitJumpPlaceholder();

        b.EmitABx(LuaOpcode.GetGlobal, a: 9, bx: k.Str(PosYFn));
        b.EmitTest(register: 9, c: 0);
        var noPosFn = b.EmitJumpPlaceholder();

        b.Emit(LuaOpcode.Move, a: 10, b: 8);                              // self = buttons
        b.Emit(LuaOpcode.LoadNil, a: 11, b: 11);                          // x = nil
        b.Emit(LuaOpcode.Add, a: 12, b: 7, c: Lua50FunctionBuilder.Rk(k.Num(PopupButtonSafetyMargin))); // y = y2 + запас
        b.Emit(LuaOpcode.LoadNil, a: 13, b: 13);                          // z = nil
        b.Emit(LuaOpcode.Call, a: 9, b: 5, c: 1);                         // 4 аргументи

        b.PatchJump(noPosFn, b.NextPc);
        b.PatchJump(noButtons, b.NextPc);
        b.PatchJump(noY2, b.NextPc);
        b.PatchJump(noNative, b.NextPc);
        b.PatchJump(noCp, b.NextPc);
        b.PatchJump(noTitle, b.NextPc);

        b.Emit(LuaOpcode.Return, a: 0, b: 1);
        return b.Build(path: path);
    }

    // -------------------------------------------------------------------------
    // UA: Крок встановлення хука: якщо Popup_Tutorial існує і ще не
    //     обгорнутий — зберегти оригінальний SetPage і підмінити його. Без
    //     параметрів, викликається безумовно з BuildDispatchWrapper.
    // EN: The install step: if Popup_Tutorial exists and is not yet
    //     wrapped — back up the original SetPage and replace it. No
    //     parameters, called unconditionally from BuildDispatchWrapper.
    // -------------------------------------------------------------------------
    private static LuaFunctionPrototype BuildPopupTutorialInstallStep(string path)
    {
        var b = new Lua50FunctionBuilder { NumParams = 0, IsVararg = 0, MaxStackSize = 4 };
        var k = new ConstantCache(b);

        b.EmitABx(LuaOpcode.GetGlobal, a: 0, bx: k.Str(PopupTutorialGlobal));
        b.EmitTest(register: 0, c: 0);
        var noGlobal = b.EmitJumpPlaceholder();

        // UA: вже обгорнуто? (ідемпотентність — не обгортати вдруге)
        // EN: already wrapped? (idempotency — do not double-wrap)
        b.Emit(LuaOpcode.GetTable, a: 1, b: 0, c: Lua50FunctionBuilder.Rk(k.Str(SetPageBackupField)));
        b.EmitTest(register: 1, c: 1);   // c=1: якщо r1 ВІДСУТНЄ (falsy) — виконати блок далі
        var alreadyWrapped = b.EmitJumpPlaceholder();

        b.Emit(LuaOpcode.GetTable, a: 2, b: 0, c: Lua50FunctionBuilder.Rk(k.Str(SetPageField)));
        b.EmitTest(register: 2, c: 0);
        var noSetPage = b.EmitJumpPlaceholder();

        b.Emit(LuaOpcode.SetTable, a: 0, b: Lua50FunctionBuilder.Rk(k.Str(SetPageBackupField)), c: 2);
        var nested = b.AddNestedPrototype(BuildPopupTutorialSetPageWrapper($"{path}/PopupTutorialSetPage"));
        b.EmitABx(LuaOpcode.Closure, a: 3, bx: nested);
        b.Emit(LuaOpcode.SetTable, a: 0, b: Lua50FunctionBuilder.Rk(k.Str(SetPageField)), c: 3);

        b.PatchJump(noSetPage, b.NextPc);
        b.PatchJump(alreadyWrapped, b.NextPc);
        b.PatchJump(noGlobal, b.NextPc);

        b.Emit(LuaOpcode.Return, a: 0, b: 1);
        return b.Build(path: path);
    }

    // -------------------------------------------------------------------------
    // UA: Групує рядки "@hook:" за іменем обгортаної функції: одна обгортка
    //     на функцію, усередині — всі поправки всіх екранів, що її замовили.
    //     Порядок детермінований (SortedDictionary), щоб згенерований байткод
    //     не «плавав» між збірками.
    // EN: Groups "@hook:" rows by the wrapped function's name: one wrapper per
    //     function, carrying every correction from every screen that asked for
    //     it. The order is deterministic (SortedDictionary) so the generated
    //     bytecode does not drift between builds.
    // -------------------------------------------------------------------------
    // -------------------------------------------------------------------------
    // UA: Збирає всі рядки "@initlist:" з усіх екранів у детермінованому
    //     порядку: одна обгортка на всю гру, всередині — по одній гілці
    //     порівняння на кожну названу глобальну таблицю-дескриптор.
    // EN: Collects every "@initlist:" row from every screen in a deterministic
    //     order: one wrapper for the whole game, carrying one comparison branch
    //     per named global descriptor table.
    // -------------------------------------------------------------------------
    private static List<(string Global, WidgetEntry Entry)> InitListTargets()
    {
        var list = new List<(string, WidgetEntry)>();

        foreach (var screen in Table.Keys.OrderBy(s => s, StringComparer.Ordinal))
            foreach (var entry in Table[screen].Where(e => e.Path.StartsWith(InitListPrefix, StringComparison.Ordinal))
                                               .OrderBy(e => e.Path, StringComparer.Ordinal))
            {
                var name = entry.Path[InitListPrefix.Length..];
                if (name.Length == 0 || name.Contains('.') || name.Contains(':'))
                    throw new InvalidDataException(
                        $"UA: {TableResource}: рядок \"{entry.Path}\" — очікується формат " +
                        $"\"{InitListPrefix}<ГлобальнаТаблиця>\" (одне ім'я глобала, без крапок). / " +
                        $"EN: {TableResource}: row \"{entry.Path}\" — expected the format " +
                        $"\"{InitListPrefix}<GlobalTable>\" (a single global name, no dots).");

                // UA: дублікати шкідливі — друга гілка мовчки перезаписала б першу
                // EN: duplicates are harmful — a second branch would silently override the first
                if (list.Any(t => string.Equals(t.Item1, name, StringComparison.Ordinal)))
                    throw new InvalidDataException(
                        $"UA: {TableResource}: глобал \"{name}\" згадано в \"{InitListPrefix}\" двічі. / " +
                        $"EN: {TableResource}: global \"{name}\" appears twice in \"{InitListPrefix}\" rows.");

                list.Add((name, entry));
            }

        return list;
    }

    // -------------------------------------------------------------------------
    // UA: Обгортка ListManager_fnInitList(listbox, layout) — див. розгорнуте
    //     пояснення біля InitListPrefix. numparams=2, без апвалью: і резерв
    //     оригіналу, і таблиці-дескриптори беруться з глобалів, тож обгортку
    //     можна створювати будь-коли.
    // EN: The ListManager_fnInitList(listbox, layout) wrapper — see the detailed
    //     rationale near InitListPrefix. numparams=2, no upvalues: both the
    //     original's backup and the descriptor tables come from globals, so the
    //     wrapper can be created at any time.
    // -------------------------------------------------------------------------
    private static LuaFunctionPrototype BuildInitListWrapper(
        List<(string Global, WidgetEntry Entry)> targets, string path)
    {
        var b = new Lua50FunctionBuilder { NumParams = 2, IsVararg = 0, MaxStackSize = 8 };
        var k = new ConstantCache(b);

        const int layoutReg = 1;   // UA: r1 — другий параметр / EN: r1 — the second parameter
        const int scratch = 2;     // UA: r2 — робочий / EN: r2 — scratch

        // UA: 1) ДО оригіналу: якщо це шукана таблиця-дескриптор — дописати поля.
        // EN: 1) BEFORE the original: if this is the target descriptor table, write the fields.
        foreach (var (global, entry) in targets)
        {
            var exits = new List<int>();

            b.EmitABx(LuaOpcode.GetGlobal, a: scratch, bx: k.Str(global));
            b.EmitTest(register: scratch, c: 0);
            exits.Add(b.EmitJumpPlaceholder());

            // UA: EQ з A=0: якщо рівні — пропустити наступний JMP і зайти в тіло.
            // EN: EQ with A=0: when equal, skip the following JMP and enter the body.
            b.Emit(LuaOpcode.Eq, a: 0, b: layoutReg, c: scratch);
            exits.Add(b.EmitJumpPlaceholder());

            foreach (var (name, value) in entry.Fields)
                b.Emit(LuaOpcode.SetTable, a: layoutReg,
                    b: Lua50FunctionBuilder.Rk(k.Str(name)),
                    c: Lua50FunctionBuilder.Rk(k.Num(value)));

            foreach (var e in exits) b.PatchJump(e, b.NextPc);
        }

        // UA: 2) делегувати оригіналу тими самими аргументами (виклик іде на
        //     ГЛОБАЛ-резерв, інакше обгортка викликала б саму себе).
        // EN: 2) delegate to the original with the same arguments (the call
        //     targets the backup GLOBAL, or the wrapper would call itself).
        b.EmitABx(LuaOpcode.GetGlobal, a: scratch, bx: k.Str(InitListFn + HookBackupSuffix));
        b.EmitTest(register: scratch, c: 0);
        var noOriginal = b.EmitJumpPlaceholder();

        b.Emit(LuaOpcode.Move, a: scratch + 1, b: 0);
        b.Emit(LuaOpcode.Move, a: scratch + 2, b: layoutReg);
        b.Emit(LuaOpcode.Call, a: scratch, b: 3, c: 1);

        b.PatchJump(noOriginal, b.NextPc);

        b.Emit(LuaOpcode.Return, a: 0, b: 1);

        if (k.Count >= 383)
            throw new InvalidOperationException(
                $"Перевищено ліміт адресації RK / RK addressing limit exceeded: {k.Count}");

        return b.Build(path: path);
    }

    private static SortedDictionary<string, List<(string Screen, WidgetEntry Entry)>> HookGroups()
    {
        var map = new SortedDictionary<string, List<(string, WidgetEntry)>>(StringComparer.Ordinal);

        foreach (var screen in Table.Keys.OrderBy(s => s, StringComparer.Ordinal))
            foreach (var entry in Table[screen].Where(e => e.Path.StartsWith(HookPrefix, StringComparison.Ordinal))
                                               .OrderBy(e => e.Path, StringComparer.Ordinal))
            {
                var rest = entry.Path[HookPrefix.Length..];
                var colon = rest.IndexOf(':');
                if (colon <= 0 || colon == rest.Length - 1)
                    throw new InvalidDataException(
                        $"UA: {TableResource}: рядок \"{entry.Path}\" — очікується формат " +
                        $"\"{HookPrefix}<ГлобальнаФункція>:<шлях.до.віджета>\". / " +
                        $"EN: {TableResource}: row \"{entry.Path}\" — expected the format " +
                        $"\"{HookPrefix}<GlobalFunction>:<path.to.widget>\".");

                var fn = rest[..colon];
                if (!map.TryGetValue(fn, out var list)) map[fn] = list = [];
                list.Add((screen, entry));
            }

        return map;
    }

    // -------------------------------------------------------------------------
    // UA: Сама обгортка рантайм-сеттера — див. розгорнуте пояснення біля
    //     HookPrefix. numparams=6, без апвалью: усе береться з глобалів
    //     (оригінал, таблиця екрана, IFObj_fnSetPos), тому обгортку можна
    //     створювати будь-коли й скільки завгодно разів.
    // EN: The runtime-setter wrapper itself — see the detailed rationale near
    //     HookPrefix. numparams=6, no upvalues: everything comes from globals
    //     (the original, the screen table, IFObj_fnSetPos), so the wrapper can
    //     be created at any time, any number of times.
    // -------------------------------------------------------------------------
    private static LuaFunctionPrototype BuildHookWrapper(
        string fn, List<(string Screen, WidgetEntry Entry)> items, string path)
    {
        var b = new Lua50FunctionBuilder { NumParams = HookWrapperParams, IsVararg = 0, MaxStackSize = 16 };
        var k = new ConstantCache(b);

        const int scratch = HookWrapperParams;        // UA: r6 — робочий / EN: r6 — scratch
        const int callBase = HookWrapperParams + 1;   // UA: r7 — база викликів / EN: r7 — call base

        // UA: 1) делегувати оригіналу тими самими аргументами. Виклик іде на
        //     ГЛОБАЛ-резерв "<Fn>_ws_o", а не на саме ім'я — інакше обгортка
        //     викликала б саму себе.
        // EN: 1) delegate to the original with the same arguments. The call
        //     targets the "<Fn>_ws_o" backup GLOBAL, not the name itself —
        //     otherwise the wrapper would call itself.
        b.EmitABx(LuaOpcode.GetGlobal, a: scratch, bx: k.Str(fn + HookBackupSuffix));
        b.EmitTest(register: scratch, c: 0);
        var noOriginal = b.EmitJumpPlaceholder();

        for (var i = 0; i < HookWrapperParams; i++)
            b.Emit(LuaOpcode.Move, a: callBase + i, b: i);
        b.Emit(LuaOpcode.Call, a: scratch, b: HookWrapperParams + 1, c: 1);

        b.PatchJump(noOriginal, b.NextPc);

        // UA: 2) заново накласти поправки — ПІСЛЯ оригіналу, бо саме він щойно
        //     переставив елемент.
        // EN: 2) re-apply the corrections — AFTER the original, since that is
        //     what has just moved the element.
        foreach (var (screen, entry) in items)
        {
            var rest = entry.Path[HookPrefix.Length..];
            var parts = rest[(rest.IndexOf(':') + 1)..].Split('.');
            var exits = new List<int>();

            b.EmitABx(LuaOpcode.GetGlobal, a: scratch, bx: k.Str(screen));
            b.EmitTest(register: scratch, c: 0);
            exits.Add(b.EmitJumpPlaceholder());

            foreach (var segment in parts)
            {
                b.Emit(LuaOpcode.GetTable, a: scratch, b: scratch,
                       c: Lua50FunctionBuilder.Rk(k.Str(segment)));
                b.EmitTest(register: scratch, c: 0);
                exits.Add(b.EmitJumpPlaceholder());
            }

            var fields = entry.Fields.ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal);
            var hasPosX = fields.TryGetValue(PosXOp, out var posX);
            var hasPosY = fields.TryGetValue(PosYOp, out var posY);
            if (!hasPosX && !hasPosY)
                throw new InvalidDataException(
                    $"UA: {TableResource}: рядок \"{entry.Path}\" не має ні \"{PosXOp}\", ні " +
                    $"\"{PosYOp}\" — обгортка не мала б чого накладати. / " +
                    $"EN: {TableResource}: row \"{entry.Path}\" has neither \"{PosXOp}\" nor " +
                    $"\"{PosYOp}\" — the wrapper would have nothing to re-apply.");

            b.EmitABx(LuaOpcode.GetGlobal, a: callBase, bx: k.Str(PosYFn));
            b.EmitTest(register: callBase, c: 0);
            exits.Add(b.EmitJumpPlaceholder());

            b.Emit(LuaOpcode.Move, a: callBase + 1, b: scratch);              // self
            if (hasPosX) b.EmitABx(LuaOpcode.LoadK, a: callBase + 2, bx: k.Num(posX));
            else b.Emit(LuaOpcode.LoadNil, a: callBase + 2, b: callBase + 2); // x
            if (hasPosY) b.EmitABx(LuaOpcode.LoadK, a: callBase + 3, bx: k.Num(posY));
            else b.Emit(LuaOpcode.LoadNil, a: callBase + 3, b: callBase + 3); // y
            b.Emit(LuaOpcode.LoadNil, a: callBase + 4, b: callBase + 4);      // z = nil
            b.Emit(LuaOpcode.Call, a: callBase, b: 5, c: 1);

            foreach (var e in exits) b.PatchJump(e, b.NextPc);
        }

        b.Emit(LuaOpcode.Return, a: 0, b: 1);

        if (k.Count >= 383)
            throw new InvalidOperationException(
                $"Перевищено ліміт адресації RK / RK addressing limit exceeded: {k.Count}");

        return b.Build(path: path);
    }

    // UA: Ліміт адресації RK: поле C інструкції — 9 біт (0..511), а константи
    //     починаються з індексу MaxStack (128), тож через RK доступні лише
    //     константи з індексом < 383. Тому шляхи одного екрана ріжуться на
    //     частини так, щоб таблиця констант кожної функції лишалась у бюджеті.
    // EN: RK addressing limit: the C field is 9 bits (0..511) and constants
    //     start at MaxStack (128), so only indices < 383 are RK-addressable.
    //     A screen's paths are therefore split into budget-sized chunks.
    private const int ConstantBudget = 300;

    /// <summary>
    /// UA: Авторські значення одного віджета: шлях у таблиці екрана -> поле -> значення.
    /// EN: Authored values of one widget: path within the screen table -> field -> value.
    /// </summary>
    public sealed record WidgetEntry(string Path, IReadOnlyList<KeyValuePair<string, float>> Fields);

    private static IReadOnlyDictionary<string, List<WidgetEntry>>? _table;

    // -------------------------------------------------------------------------
    // UA: Таблиця читається з вбудованого ресурсу, а не зашита в код: її
    //     значення виміряні з даних гри, і таблиця має лишатись легко
    //     перегенерованою, а не правитись вручну.
    // EN: The table is read from an embedded resource rather than hard-coded:
    //     its values are measured from the game's own data, and the table
    //     must stay easy to regenerate rather than hand-edited.
    // -------------------------------------------------------------------------
    public static IReadOnlyDictionary<string, List<WidgetEntry>> Table => _table ??= LoadTable();

    private static IReadOnlyDictionary<string, List<WidgetEntry>> LoadTable()
    {
        using var stream = typeof(AnchorInheritancePatchBuilder).Assembly
                               .GetManifestResourceStream(TableResource)
                           ?? throw new InvalidOperationException(
                               $"Вбудований ресурс не знайдено / embedded resource not found: {TableResource}");
        using var reader = new StreamReader(stream);

        var result = new Dictionary<string, List<WidgetEntry>>(StringComparer.Ordinal);
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Length == 0 || line[0] == '#') continue;   // UA: коментарі / EN: comments
            var parts = line.Split('\t');
            if (parts.Length != 3) continue;

            var fields = new List<KeyValuePair<string, float>>();
            foreach (var pair in parts[2].Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                if (eq <= 0) continue;

                // UA: Помилку розбору називаємо ПОІМЕННО — рядок, екран, віджет,
                //     поле. Реальний випадок: файл не закінчувався переносом
                //     рядка, дописаний блок приклеївся до останнього значення
                //     ("y=-110# UA: ..."), і базове повідомлення .NET показувало
                //     лише обрізаний рядок без жодної вказівки, ДЕ шукати.
                // EN: Name the parse failure PRECISELY — line, screen, widget,
                //     field. Real case: the file lacked a trailing newline, an
                //     appended block glued itself onto the last value
                //     ("y=-110# UA: ..."), and .NET's default message showed only
                //     a truncated string with no hint WHERE to look.
                if (!float.TryParse(pair[(eq + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    throw new InvalidDataException(
                        $"UA: {TableResource}, рядок {lineNumber}: поле \"{pair}\" віджета " +
                        $"\"{parts[1]}\" на екрані \"{parts[0]}\" — не число. " +
                        $"Найчастіша причина: рядок склеївся з наступним (файл без переносу в кінці). / " +
                        $"EN: {TableResource}, line {lineNumber}: field \"{pair}\" of widget " +
                        $"\"{parts[1]}\" on screen \"{parts[0]}\" is not a number. " +
                        $"Most common cause: the row got glued to the next one (file missing a trailing newline).");

                fields.Add(new KeyValuePair<string, float>(pair[..eq], value));
            }

            if (fields.Count == 0) continue;
            if (!result.TryGetValue(parts[0], out var list))
                result[parts[0]] = list = [];
            list.Add(new WidgetEntry(parts[1], fields));
        }
        return result;
    }

    public static int ScreenCount => Table.Count;
    public static int WidgetCount => Table.Values.Sum(v => v.Count);
    public static int FieldCount => Table.Values.Sum(v => v.Sum(e => e.Fields.Count));

    // -------------------------------------------------------------------------
    // UA: Дедуплікація констант. Базовий будівник додає НОВУ константу на
    //     кожен виклик Add*Constant, через що таблиця роздувається і швидко
    //     впирається в ліміт адресації RK (виміряно: 880 констант замість 77
    //     на найбільшому екрані). Тут кожне унікальне значення додається раз.
    // EN: Constant deduplication. The base builder appends a NEW constant on
    //     every Add*Constant call, inflating the table and quickly hitting the
    //     RK limit (measured: 880 constants instead of 77 on the largest
    //     screen). Here each distinct value is added exactly once.
    // -------------------------------------------------------------------------
    private sealed class ConstantCache(Lua50FunctionBuilder builder)
    {
        private readonly Dictionary<string, int> _strings = new(StringComparer.Ordinal);
        private readonly Dictionary<float, int> _numbers = [];

        public int Str(string value)
        {
            if (!_strings.TryGetValue(value, out var i))
                _strings[value] = i = builder.AddStringConstant(value);
            return i;
        }

        public int Num(float value)
        {
            if (!_numbers.TryGetValue(value, out var i))
                _numbers[value] = i = builder.AddNumberConstant(value);
            return i;
        }

        public int Count => _strings.Count + _numbers.Count;
    }

    private static List<List<WidgetEntry>> ChunkScreen(List<WidgetEntry> entries)
    {
        var chunks = new List<List<WidgetEntry>>();
        var current = new List<WidgetEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            var need = new HashSet<string>(StringComparer.Ordinal);
            // UA: "@globals" — не шлях у таблиці, тож сегменти в константи не йдуть;
            //     константами стають лише імена глобалів та їхні значення (нижче).
            //     "@screen" так само не має проходу GETTABLE — сегментів шляху
            //     просто немає (сам рядок "@screen" сегментом не є).
            // EN: "@globals" is not a table path, so its segments consume no
            //     constants; only the global names and values do (below).
            //     "@screen" likewise has no GETTABLE walk — there are no path
            //     segments to speak of (the literal "@screen" is not one).
            if (entry.Path != GlobalsPath && entry.Path != ScreenPath)
                foreach (var part in entry.Path.Split('.')) need.Add("s:" + part);
            foreach (var (name, value) in entry.Fields)
            {
                // UA: "@globals"-поле з крапкою в імені (напр. "ifs_mspc_MapList_layout.width")
                //     емітить ДВІ окремі рядкові константи (ім'я глобалу + ім'я підполя,
                //     див. блок нижче), а не одну — рахуємо так само тут, інакше оцінка
                //     бюджету констант розходиться з реальним емітом.
                // EN: A "@globals" field whose name contains a dot (e.g.
                //     "ifs_mspc_MapList_layout.width") emits TWO separate string
                //     constants (the global's name + the subfield's name, see the block
                //     below), not one — count it the same way here, or the constant-
                //     budget estimate would diverge from what actually gets emitted.
                if (entry.Path == GlobalsPath && name.Contains('.'))
                {
                    var gparts = name.Split('.');
                    need.Add("s:" + gparts[0]);
                    need.Add("s:" + gparts[1]);
                }
                else
                {
                    need.Add("s:" + name);
                }
                need.Add("n:" + value.ToString("R", CultureInfo.InvariantCulture));
            }

            if (current.Count > 0 && seen.Union(need).Count() > ConstantBudget)
            {
                chunks.Add(current);
                current = [];
                seen = [];
            }

            current.Add(entry);
            seen.UnionWith(need);
        }

        if (current.Count > 0) chunks.Add(current);
        return chunks;
    }

    // -------------------------------------------------------------------------
    // UA: Функція однієї порції одного екрана: f(t) — пройти шлях кожного
    //     віджета, перевіряючи КОЖЕН вузол на існування, і записати значення.
    //     Відсутній вузол (інша збірка гри, інший мод) просто пропускається.
    // EN: One chunk of one screen: f(t) walks each widget's path, checking
    //     EVERY node for existence, and writes the values. A missing node
    //     (a different game build or mod) is simply skipped.
    // -------------------------------------------------------------------------
    private static LuaFunctionPrototype BuildChunkFunction(List<WidgetEntry> entries, float scale, string path)
    {
        var b = new Lua50FunctionBuilder { NumParams = 1, IsVararg = 0, MaxStackSize = 4 };
        var k = new ConstantCache(b);

        foreach (var entry in entries)
        {
            // -----------------------------------------------------------------
            // UA: Псевдошлях "@globals" — поля є не ключами таблиці екрана, а
            //     ГЛОБАЛАМИ Lua. Потрібен для елементів, які взагалі не живуть у
            //     таблиці екрана: 3D-моделі (наприклад коробки бонусів на
            //     ifs_freeform_purchase_tech) позиціюються викликом
            //     IFModel_fnSetTranslation, що читає глобал КОЖНОГО кадру через
            //     GETGLOBAL, а не одноразово при побудові. Тому запис глобала в
            //     обгортці AddIFScreen (тобто вже ПІСЛЯ того, як скрипт екрана
            //     виставив своє значення) діє — на відміну від полів, що
            //     "запікаються" синхронно.
            //     Перевага перед зміною самих віджетів: рухається СЛУЖБОВА
            //     константа розкладки, а не розмір намальованого елемента, тож
            //     графіка не деградує.
            // EN: The "@globals" pseudo-path — the fields are Lua GLOBALS, not
            //     keys of the screen table. Needed for elements that do not live
            //     in the screen table at all: 3D models (e.g. the bonus boxes on
            //     ifs_freeform_purchase_tech) are placed by IFModel_fnSetTranslation,
            //     which re-reads the global via GETGLOBAL EVERY frame rather than
            //     once at build time. So writing the global from the AddIFScreen
            //     wrapper (i.e. AFTER the screen's own script assigned it) does
            //     take effect — unlike fields that get baked synchronously.
            //     Advantage over editing the widgets themselves: it moves a
            //     LAYOUT constant rather than the size of a drawn element, so the
            //     artwork is not degraded.
            // -----------------------------------------------------------------
            if (entry.Path == GlobalsPath)
            {
                foreach (var (name, value) in entry.Fields)
                {
                    // ---------------------------------------------------------
                    // UA: РОЗШИРЕННЯ "@globals":
                    //     ім'я поля з КРАПКОЮ (напр. "SomeGlobalTable.field") означає
                    //     не сам глобал, а ПІДПОЛЕ глобальної ТАБЛИЦІ — GETGLOBAL
                    //     <перша частина>, потім SETTABLE result[<друга частина>]=value,
                    //     замість повного перезапису глобалу скаляром (як робить
                    //     простий "@globals" вище). Безпека — так само, як у звичайного
                    //     "@post:": вузол перевіряється на існування (TEST) перед
                    //     записом, щоб інша збірка гри/мод без цього глобалу не впала в
                    //     помилку "SETTABLE на nil".
                    //
                    //     МЕЖІ ЗАСТОСУВАННЯ (на прикладі дефекту "робоча зона Карта/Список не
                    //     росте разом з рамкою"): `ListManager_fnInitList` читає ширину рядка
                    //     й кількість видимих рядків (`showcount`) з окремої глобальної
                    //     таблиці-дескриптора (`ifs_mspc_MapList_layout` тощо), а не з
                    //     ButtonWindow.width. Дотична "@globals" з крапкою могла б переписати
                    //     `ifs_mspc_MapList_layout.width/showcount`, але лише ПІСЛЯ того, як
                    //     конструктор екрана (root/78 у ifs_missionselect_pcMulti) вже їх
                    //     виставив, а `ListManager_fnInitList` одразу створив фіксовану
                    //     кількість рядків-віджетів через `CreateFn`. `root/78` виконується як
                    //     частина скрипта екрана, ДО виклику `AddIFScreen(t, name)` — тобто до
                    //     першої миті, коли обгортка-патч отримує керування. Тому для цього
                    //     дефекту й "regular" (до AddIFScreen), і "@post:" (після AddIFScreen)
                    //     хуки однаково запізнюються: рядки-віджети вже створені зі старими
                    //     width/showcount, і повторний запис глобалу цього не змінює. Фікс
                    //     саме цього дефекту вимагав би патчити сам байткод `root/78` (його
                    //     нативні константи 170/220/356) — це інший, значно ризикованіший клас
                    //     правки, ніж усе, що робить цей файл, і тут не реалізований. Механізм
                    //     "@globals.field" лишається загальною безпечною можливістю для інших
                    //     глобалів, які реально перечитуються пізніше — просто не для цього
                    //     конкретного дефекту.
                    // EN: "@globals" EXTENSION:
                    //     a field name containing a DOT (e.g. "SomeGlobalTable.field")
                    //     does not mean the global itself, but a SUBFIELD of a global
                    //     TABLE — GETGLOBAL <first part>, then SETTABLE
                    //     result[<second part>]=value, instead of fully overwriting the
                    //     global with a scalar (as the plain "@globals" case above
                    //     does). Safety matches the ordinary "@post:" pattern: the node
                    //     is existence-checked (TEST) before writing, so a different
                    //     game build/mod without this global does not crash with
                    //     "SETTABLE on nil".
                    //
                    //     SCOPE LIMITS (illustrated by the "Карта/Список working area does not
                    //     grow with the frame" defect): `ListManager_fnInitList` reads a row's
                    //     width and the visible row count (`showcount`) from a separate global
                    //     descriptor table (`ifs_mspc_MapList_layout` etc.), not from
                    //     ButtonWindow.width. A dotted "@globals" row could overwrite
                    //     `ifs_mspc_MapList_layout.width/showcount`, but only AFTER the
                    //     screen's constructor (root/78 in ifs_missionselect_pcMulti) has
                    //     already set them, and `ListManager_fnInitList` has already created a
                    //     fixed number of row widgets via `CreateFn`. `root/78` runs as part
                    //     of the screen's own script, BEFORE `AddIFScreen(t, name)` is called
                    //     — i.e. before the very first moment the wrapper gets control. So for
                    //     this defect, both "regular" (before AddIFScreen) and "@post:" (after
                    //     AddIFScreen) hooks are equally too late: the row widgets already
                    //     exist with the old width/showcount, and rewriting the global
                    //     afterward changes nothing. Fixing this specific defect would require
                    //     patching `root/78`'s own bytecode (its native constants 170/220/356)
                    //     — a categorically different, materially riskier class of edit than
                    //     anything else in this file, and not implemented here. The
                    //     "@globals.field" mechanism remains a general, safe capability for
                    //     other globals that genuinely get re-read later — just not for this
                    //     particular defect.
                    // ---------------------------------------------------------
                    if (name.Contains('.'))
                    {
                        var gparts = name.Split('.');
                        if (gparts.Length != 2)
                            throw new InvalidOperationException(
                                $"'@globals' підтримує лише GlobalName.field (одна крапка) / '@globals' only supports GlobalName.field (one dot): {name}");

                        b.EmitABx(LuaOpcode.GetGlobal, a: 1, bx: k.Str(gparts[0]));
                        b.EmitTest(register: 1, c: 0);
                        var skip = b.EmitJumpPlaceholder();

                        b.Emit(LuaOpcode.SetTable, a: 1,
                            b: Lua50FunctionBuilder.Rk(k.Str(gparts[1])),
                            c: Lua50FunctionBuilder.Rk(k.Num(value)));

                        b.PatchJump(skip, b.NextPc);
                        continue;
                    }

                    b.EmitABx(LuaOpcode.LoadK, a: 1, bx: k.Num(value));
                    b.EmitABx(LuaOpcode.SetGlobal, a: 1, bx: k.Str(name));
                }
                continue;
            }

            // -----------------------------------------------------------------
            // UA: Псевдошлях "@screen" — без GETTABLE-проходу: поля пишуться
            //     СЕТTABLE напряму на R0 (параметр-корінь, сама таблиця
            //     екрана). На відміну від "@globals", тут відсутність вузла
            //     перевіряти нема потреби — R0 як мінімум завжди таблиця
            //     (це аргумент AddIFScreen), тож ризику "записати в non-table"
            //     немає. Масштабування (value*scale) працює так само, як для
            //     звичайних рядків — це пікселі, а не частка/колір/порядок.
            // EN: The "@screen" pseudo-path — no GETTABLE walk: fields are
            //     written via SETTABLE straight onto R0 (the root parameter,
            //     the screen table itself). Unlike "@globals", there is no
            //     node to existence-check here — R0 is always at least a
            //     table (it's AddIFScreen's own argument), so there is no
            //     risk of "writing into a non-table". Scaling (value*scale)
            //     works the same as for ordinary rows — these are pixels, not
            //     a fraction/color/draw-order.
            // -----------------------------------------------------------------
            if (entry.Path == ScreenPath)
            {
                foreach (var (name, value) in entry.Fields)
                {
                    var final = UnscaledFields.Contains(name) ? value : value * scale;
                    b.Emit(LuaOpcode.SetTable, a: 0,
                        b: Lua50FunctionBuilder.Rk(k.Str(name)),
                        c: Lua50FunctionBuilder.Rk(k.Num(final)));
                }
                continue;
            }

            var parts = entry.Path.Split('.');
            var exits = new List<int>();

            b.Emit(LuaOpcode.GetTable, a: 1, b: 0, c: Lua50FunctionBuilder.Rk(k.Str(parts[0])));
            b.EmitTest(register: 1, c: 0);
            exits.Add(b.EmitJumpPlaceholder());

            for (var i = 1; i < parts.Length; i++)
            {
                b.Emit(LuaOpcode.GetTable, a: 1, b: 1, c: Lua50FunctionBuilder.Rk(k.Str(parts[i])));
                b.EmitTest(register: 1, c: 0);
                exits.Add(b.EmitJumpPlaceholder());
            }

            foreach (var (name, value) in entry.Fields)
            {
                var final = UnscaledFields.Contains(name) ? value : value * scale;
                b.Emit(LuaOpcode.SetTable, a: 1,
                    b: Lua50FunctionBuilder.Rk(k.Str(name)),
                    c: Lua50FunctionBuilder.Rk(k.Num(final)));
            }

            // UA: наступний віджет починається знову від кореня, тож усі виходи
            //     ведуть саме сюди
            foreach (var e in exits) b.PatchJump(e, b.NextPc);
        }

        b.Emit(LuaOpcode.Return, a: 0, b: 1);

        if (k.Count >= 383)
            throw new InvalidOperationException(
                $"Перевищено ліміт адресації RK / RK addressing limit exceeded: {k.Count}");

        return b.Build(path: path);
    }

    // -------------------------------------------------------------------------
    // UA: Порція правок «ПІСЛЯ ПОБУДОВИ»: f(t) проходить шлях віджета, бере
    //     його дескриптор 'cp' і викликає нативний сеттер напряму.
    //
    //     ТРИ РІВНІ ЗАХИСТУ, бо це нова, ще не обкатана в грі гілка, а історія
    //     цього патчера вже знає «миттєвий виліт у головне меню» від однієї
    //     невдалої вставки:
    //       1) кожен вузол шляху перевіряється на існування (інша збірка/мод);
    //       2) 'cp' перевіряється на nil (віджет міг не зареєструватись);
    //       3) САМ НАТИВ перевіряється на nil — якщо рушій його не експортує,
    //          виклик просто пропускається замість помилки рантайму.
    //     Пункт 3 критичний: перелік нативів знято з конкретного exe, а в
    //     користувача може бути інша збірка гри.
    // EN: One chunk of "AFTER BUILD" fixes: f(t) walks the widget path, takes
    //     its 'cp' handle and calls the native setter directly.
    //
    //     THREE GUARD LAYERS, since this branch is new and not yet exercised
    //     in-game, and this patcher's history already includes an "instant
    //     crash to main menu" from one bad insertion:
    //       1) every path node is existence-checked (different build/mod);
    //       2) 'cp' is nil-checked (the widget may not have registered);
    //       3) THE NATIVE ITSELF is nil-checked — if the engine does not export
    //          it, the call is skipped instead of raising a runtime error.
    //     Point 3 is critical: the native list was taken from one specific exe,
    //     and the user may be running a different build of the game.
    // -------------------------------------------------------------------------
    private static LuaFunctionPrototype BuildPostChunkFunction(List<WidgetEntry> entries, string path)
    {
        var b = new Lua50FunctionBuilder { NumParams = 1, IsVararg = 0, MaxStackSize = 8 };
        var k = new ConstantCache(b);

        foreach (var entry in entries)
        {
            var parts = entry.Path[PostPrefix.Length..].Split('.');
            var exits = new List<int>();

            // -----------------------------------------------------------------
            // UA: Корінь шляху. Звичайно це таблиця екрана (параметр t, r0), але
            //     якщо перший сегмент починається з '@' — це ГЛОБАЛЬНА таблиця.
            //     Потрібно для попапів: Popup_Tutorial (вікно довідки) не
            //     проходить через AddIFScreen узагалі — він будується один раз
            //     при завантаженні скрипта popup_tutorial і живе в глобалі.
            //     Перевірено у VM: одразу після завантаження скрипта
            //     Popup_Tutorial.title.cp вже має значення, тобто об'єкт рушія
            //     створено і його можна міняти нативом. Отже обгортати
            //     CreatePopupInC (ризикована правка перевіреного інсталятора)
            //     НЕ ПОТРІБНО — досить дотягнутись до глобала з наявного гачка.
            // EN: The path root. Normally the screen table (parameter t, r0), but
            //     if the first segment starts with '@' it is a GLOBAL table.
            //     Needed for popups: Popup_Tutorial (the help window) never goes
            //     through AddIFScreen at all — it is built once when the
            //     popup_tutorial script loads and lives in a global. VM-verified:
            //     right after that script loads, Popup_Tutorial.title.cp already
            //     holds a value, i.e. the engine object exists and can be mutated
            //     via a native. So wrapping CreatePopupInC (a risky change to the
            //     proven installer) is NOT needed — reaching the global from the
            //     existing hook is enough.
            // -----------------------------------------------------------------
            if (parts[0].StartsWith('@'))
                b.EmitABx(LuaOpcode.GetGlobal, a: 1, bx: k.Str(parts[0][1..]));
            else
                b.Emit(LuaOpcode.GetTable, a: 1, b: 0, c: Lua50FunctionBuilder.Rk(k.Str(parts[0])));

            b.EmitTest(register: 1, c: 0);
            exits.Add(b.EmitJumpPlaceholder());

            for (var i = 1; i < parts.Length; i++)
            {
                b.Emit(LuaOpcode.GetTable, a: 1, b: 1, c: Lua50FunctionBuilder.Rk(k.Str(parts[i])));
                b.EmitTest(register: 1, c: 0);
                exits.Add(b.EmitJumpPlaceholder());
            }

            var fields = entry.Fields.ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal);

            // -----------------------------------------------------------------
            // UA: Спершу LUA-МЕТОДИ: вони працюють із самою таблицею (r1), тож
            //     мають виконатись до пошуку 'cp' — інакше запис у попап без
            //     власного 'cp' зірвався б на перевірці, якої йому не треба.
            // EN: LUA METHODS first: they operate on the table itself (r1), so
            //     they must run before the 'cp' lookup — otherwise an entry on a
            //     table without its own 'cp' would bail out on a check it does
            //     not need.
            // -----------------------------------------------------------------
            foreach (var (name, (fn, argc)) in PostLuaOps)
            {
                if (!fields.TryGetValue(name, out var a1)) continue;
                float a2 = 0f;
                if (argc == 2 && !fields.TryGetValue(name + "2", out a2))
                    throw new InvalidDataException(
                        $"UA: {TableResource}: операція \"{name}\" на \"{entry.Path}\" потребує також " +
                        $"поле \"{name}2\". / EN: {TableResource}: operation \"{name}\" on \"{entry.Path}\" " +
                        $"also requires the field \"{name}2\".");

                // UA: r2 <- функція; якщо її нема (інша збірка) — крок пропускаємо
                // EN: r2 <- the function; if absent (another build), skip the step
                b.EmitABx(LuaOpcode.GetGlobal, a: 2, bx: k.Str(fn));
                b.EmitTest(register: 2, c: 0);
                var skipLua = b.EmitJumpPlaceholder();

                b.Emit(LuaOpcode.Move, a: 3, b: 1);                  // self — сама таблиця / the table
                b.EmitABx(LuaOpcode.LoadK, a: 4, bx: k.Num(a1));
                if (argc == 2) b.EmitABx(LuaOpcode.LoadK, a: 5, bx: k.Num(a2));
                b.Emit(LuaOpcode.Call, a: 2, b: 2 + argc, c: 1);

                b.PatchJump(skipLua, b.NextPc);
            }

            // UA: зсув по X і/або Y: IFObj_fnSetPos(self, x, y, nil). Обидва
            //     поля опційні й незалежні — якщо задано лише "posx", y (і z)
            //     лишаються nil (недоторкані) завдяки нуль-толерантності
            //     функції, і навпаки.
            // EN: X and/or Y shift: IFObj_fnSetPos(self, x, y, nil). Both
            //     fields are optional and independent — if only "posx" is
            //     given, y (and z) stay nil (untouched) thanks to the
            //     function's nil-tolerance, and vice versa.
            var hasPosX = fields.TryGetValue(PosXOp, out var posX);
            var hasPosY = fields.TryGetValue(PosYOp, out var posY);
            if (hasPosX || hasPosY)
            {
                b.EmitABx(LuaOpcode.GetGlobal, a: 2, bx: k.Str(PosYFn));
                b.EmitTest(register: 2, c: 0);
                var skipPos = b.EmitJumpPlaceholder();

                b.Emit(LuaOpcode.Move, a: 3, b: 1);                  // self
                if (hasPosX) b.EmitABx(LuaOpcode.LoadK, a: 4, bx: k.Num(posX));
                else b.Emit(LuaOpcode.LoadNil, a: 4, b: 4);          // x
                if (hasPosY) b.EmitABx(LuaOpcode.LoadK, a: 5, bx: k.Num(posY));
                else b.Emit(LuaOpcode.LoadNil, a: 5, b: 5);          // y
                b.Emit(LuaOpcode.LoadNil, a: 6, b: 6);               // z = nil
                b.Emit(LuaOpcode.Call, a: 2, b: 5, c: 1);            // 4 аргументи / 4 args

                b.PatchJump(skipPos, b.NextPc);
            }

            // UA: далі — НАТИВИ, яким потрібен дескриптор. Якщо у віджета нема
            //     'cp', виходимо: але Lua-методи вище вже відпрацювали.
            // EN: now the NATIVES, which need the handle. If the widget has no
            //     'cp', bail out — but the Lua methods above already ran.
            if (PostOps.Keys.Any(fields.ContainsKey))
            {
                b.Emit(LuaOpcode.GetTable, a: 2, b: 1, c: Lua50FunctionBuilder.Rk(k.Str("cp")));
                b.EmitTest(register: 2, c: 0);
                exits.Add(b.EmitJumpPlaceholder());
            }

            foreach (var (name, (native, argc)) in PostOps)
            {
                if (!fields.TryGetValue(name, out var v1)) continue;
                // UA: для 2-аргументної операції другий аргумент обов'язковий
                // EN: for a 2-argument operation the second argument is required
                float v2 = 0f;
                if (argc == 2 && !fields.TryGetValue(name + "2", out v2))
                    throw new InvalidDataException(
                        $"UA: {TableResource}: операція \"{name}\" на \"{entry.Path}\" потребує також " +
                        $"поле \"{name}2\". / EN: {TableResource}: operation \"{name}\" on \"{entry.Path}\" " +
                        $"also requires the field \"{name}2\".");

                // UA: r3 <- натив; якщо рушій його не має — пропускаємо цю операцію
                // EN: r3 <- native; if the engine lacks it, skip this operation
                b.EmitABx(LuaOpcode.GetGlobal, a: 3, bx: k.Str(native));
                b.EmitTest(register: 3, c: 0);
                var skipOp = b.EmitJumpPlaceholder();

                b.Emit(LuaOpcode.Move, a: 4, b: 2);                  // дескриптор / handle
                b.EmitABx(LuaOpcode.LoadK, a: 5, bx: k.Num(v1));
                if (argc == 2) b.EmitABx(LuaOpcode.LoadK, a: 6, bx: k.Num(v2));

                // UA: CALL a=3, b=1+кількість аргументів, c=1 (нуль результатів)
                // EN: CALL a=3, b=1+argument count, c=1 (zero results)
                b.Emit(LuaOpcode.Call, a: 3, b: 2 + argc, c: 1);

                b.PatchJump(skipOp, b.NextPc);
            }

            foreach (var e in exits) b.PatchJump(e, b.NextPc);
        }

        b.Emit(LuaOpcode.Return, a: 0, b: 1);

        if (k.Count >= 383)
            throw new InvalidOperationException(
                $"Перевищено ліміт адресації RK / RK addressing limit exceeded: {k.Count}");

        return b.Build(path: path);
    }

    // -------------------------------------------------------------------------
    // UA: Функція "@postscreen": f(t) — SETTABLE напряму на R0 (та сама
    //     таблиця екрана, яку щойно наповнив і повернув f(t) конструктора
    //     власного скрипта екрана), БЕЗ GETTABLE-проходу. Дзеркало гілки
    //     "@screen" у BuildChunkFunction — різниця лише в тому, ЩО викликає
    //     цю функцію (обгортка AddIFScreen, ПІСЛЯ делегування оригіналу, а
    //     не до нього). Існування вузла перевіряти не треба з тієї ж причини,
    //     що й для "@screen": R0 тут завжди таблиця (аргумент, з яким щойно
    //     відпрацював оригінальний AddIFScreen).
    // EN: The "@postscreen" function: f(t) — SETTABLE straight onto R0 (the
    //     same screen table the screen's own constructor just populated and
    //     returned from), with NO GETTABLE walk. Mirrors the "@screen" branch
    //     in BuildChunkFunction — the only difference is WHO calls this
    //     function (the AddIFScreen wrapper, AFTER delegating to the
    //     original, not before). No node-existence check is needed for the
    //     same reason as "@screen": R0 here is always a table (the argument
    //     the original AddIFScreen just finished working with).
    // -------------------------------------------------------------------------
    private static LuaFunctionPrototype BuildPostScreenChunkFunction(List<WidgetEntry> entries, float scale, string path)
    {
        var b = new Lua50FunctionBuilder { NumParams = 1, IsVararg = 0, MaxStackSize = 2 };
        var k = new ConstantCache(b);

        foreach (var entry in entries)
        foreach (var (name, value) in entry.Fields)
        {
            var final = UnscaledFields.Contains(name) ? value : value * scale;
            b.Emit(LuaOpcode.SetTable, a: 0,
                b: Lua50FunctionBuilder.Rk(k.Str(name)),
                c: Lua50FunctionBuilder.Rk(k.Num(final)));
        }

        b.Emit(LuaOpcode.Return, a: 0, b: 1);

        if (k.Count >= 383)
            throw new InvalidOperationException(
                $"Перевищено ліміт адресації RK / RK addressing limit exceeded: {k.Count}");

        return b.Build(path: path);
    }

    // =========================================================================
    // UA: ДІАГНОСТИЧНИЙ ЗОНД "widescreen". НІКОЛИ не входить у
    //     production-збірку (Bf2LayoutTable.txt/shell_layout.lvl) — вмикається
    //     ЛИШЕ прапорцем includeScreenInfoProbe у BuildInstallerScript, який
    //     використовує окрема діагностична команда (GenerateScreenInfoProbeShellCommand,
    //     вихід "shell_probe.lvl"). За замовчуванням (false) три методи нижче
    //     не викликаються НІЗВІДКИ — production-байткод лишається побайтово
    //     тим самим.
    //
    //     НАВІЩО: свіже, точне трасування ifelem_shellscreen_fnAddBackground
    //     показало формулу `localpos_r = w * widescreen` (4-те значення
    //     ScriptCB_GetScreenInfo, НЕ 3-тє "v"). Пітонівський
    //     VM завжди підставляє widescreen=1.0 (заглушка), тож справжнє значення
    //     на реальних 1920×1080 невідоме — а від нього залежить, чи контейнер
    //     фону побудований ЗАВЕЛИКИМ (якщо widescreen≠1.0), чи дефект — лише в
    //     непроставлених uvs_r/uvs_t на коректному за розміром контейнері
    //     (якщо widescreen=1.0). Користувач не має доступу до консолі/логу гри
    //     (підтверджено прямим питанням), тож єдиний канал виводу — вже
    //     ДОВІРЕНА піксельна методика цього проєкту: перетворити невидиме число
    //     на видиму Y-позицію підпису "Налаштування:" (той самий віджет
    //     option_buttons.setting, той самий IFObj_fnSetPos, що й у вже
    //     ПІДТВЕРДЖЕНОМУ рядку "ifs_missionselect @post:option_buttons.setting"
    //     нижче в таблиці) — і зчитати результат з ОДНОГО знімка екрана.
    //
    //     МЕХАНІЗМ (два кроки, обидва ідемпотентні й прозорі):
    //       1) BuildScreenInfoProbeInstallStep — раз обгортає глобальну
    //          ScriptCB_GetScreenInfo (той самий безумовний ідемпотентний
    //          крок на КОЖЕН AddIFScreen, що й у Popup_Tutorial-хука, з тієї ж
    //          причини обережності: раптом натив ще не зареєстрований).
    //       2) BuildScreenInfoProbeWrapper — САМА обгортка: викликає
    //          оригінал, копіює його 4 результати в 4 нових глобали
    //          (gWSProbe_W/H/V/Widescreen) через SETGLOBAL (не займає регістр
    //          джерела), і повертає ті самі 4 значення БЕЗ ЗМІН — повністю
    //          прозорий passthrough. Це критично: цей натив викликається
    //          вкрай часто на майже кожному екрані, зламане повернене
    //          значення зламало б усю гру.
    //       3) BuildScreenInfoProbeDisplayStep — бесспокійний, ЖОРСТКО
    //          прив'язаний до ifs_missionselect крок: читає gWSProbe_Widescreen
    //          (на момент побудови missionselect зонд уже спрацював десятки
    //          разів — цей екран НЕ перший AddIFScreen у сесії), множить на
    //          500 (щоб дробове число ~1.0-2.4 стало піксельно вимірною
    //          відстанню) і подає як posY у той самий, уже перевірений виклик
    //          IFObj_fnSetPos(option_buttons.setting, nil, y, nil).
    //
    //     ЯК ЧИТАТИ РЕЗУЛЬТАТ: один знімок екрана "Миттєвий бій -> Сеанс",
    //     виміряти піксельну Y-координату верху напису "Налаштування:" і
    //     поділити на 500 — це і є реальне widescreen на цій роздільності.
    // EN: DIAGNOSTIC PROBE for "widescreen". NEVER part of the
    //     production build (Bf2LayoutTable.txt/shell_layout.lvl) — enabled
    //     ONLY by the includeScreenInfoProbe flag on BuildInstallerScript,
    //     used by a separate diagnostic command (GenerateScreenInfoProbeShellCommand,
    //     output "shell_probe.lvl"). By default (false) the three methods
    //     below are called from NOWHERE — the production bytecode stays
    //     byte-for-byte identical.
    //
    //     WHY: a fresh, precise re-trace of ifelem_shellscreen_fnAddBackground
    //     found the formula `localpos_r = w * widescreen` (the 4th
    //     ScriptCB_GetScreenInfo value, not the 3rd "v"). The Python VM
    //     always stubs widescreen=1.0, so the real value at
    //     actual 1920x1080 is unknown — and it decides whether the background
    //     container is built OVERSIZED (if widescreen != 1.0) or the defect is
    //     purely un-set uvs_r/uvs_t on a correctly-sized container (if
    //     widescreen == 1.0). The user has no access to the game's
    //     console/log (confirmed by a direct question), so the only output
    //     channel is this project's already-TRUSTED pixel technique: turn the
    //     invisible number into the visible Y-position of the
    //     "Налаштування:"/"Settings:" label (the same option_buttons.setting
    //     widget, the same IFObj_fnSetPos call already used by the CONFIRMED
    //     "ifs_missionselect @post:option_buttons.setting" row further down in
    //     the table) — readable from a SINGLE screenshot.
    //
    //     MECHANISM (two steps, both idempotent and transparent):
    //       1) BuildScreenInfoProbeInstallStep — wraps the global
    //          ScriptCB_GetScreenInfo once (the same unconditional, idempotent,
    //          per-AddIFScreen step as the Popup_Tutorial hook, for the same
    //          reason: the native might not be registered yet).
    //       2) BuildScreenInfoProbeWrapper — the wrapper itself: calls the
    //          original, copies its 4 results into 4 new globals
    //          (gWSProbe_W/H/V/Widescreen) via SETGLOBAL (does not consume the
    //          source register), then returns the same 4 values UNCHANGED — a
    //          fully transparent passthrough. Critical: this native is called
    //          extremely often on almost every screen; a broken return value
    //          would break the whole game.
    //       3) BuildScreenInfoProbeDisplayStep — a bespoke step hard-tied to
    //          ifs_missionselect: reads gWSProbe_Widescreen (by the time
    //          missionselect is built the probe has already fired dozens of
    //          times — this screen is not the session's first AddIFScreen),
    //          multiplies by 500 (turning a ~1.0-2.4 fraction into a
    //          pixel-measurable distance) and feeds it as posY into the same,
    //          already-proven IFObj_fnSetPos(option_buttons.setting, nil, y,
    //          nil) call.
    //
    //     HOW TO READ THE RESULT: one screenshot of "Instant Action -> Session",
    //     measure the pixel Y coordinate of the "Налаштування:"/"Settings:"
    //     label's top edge and divide by 500 — that is the real widescreen
    //     value at that resolution.
    // =========================================================================
    private const string ScreenInfoFn = "ScriptCB_GetScreenInfo";
    private const string ScreenInfoBackupField = "ScriptCB_GetScreenInfo_ws_o";
    private const string ScreenInfoProbeW = "gWSProbe_W";
    private const string ScreenInfoProbeH = "gWSProbe_H";
    private const string ScreenInfoProbeV = "gWSProbe_V";
    private const string ScreenInfoProbeWidescreen = "gWSProbe_Widescreen";
    private const string ScreenInfoProbeScreen = "ifs_missionselect";
    private const float ScreenInfoProbeMultiplier = 500f;

    // UA: Прозора обгортка нативу — 0 параметрів, 4 результати без змін.
    // EN: The transparent native wrapper — 0 params, 4 unchanged results.
    private static LuaFunctionPrototype BuildScreenInfoProbeWrapper(string path)
    {
        var b = new Lua50FunctionBuilder { NumParams = 0, IsVararg = 0, MaxStackSize = 4 };
        var k = new ConstantCache(b);

        // r0 <- оригінал; CALL 0 аргументів, 4 результати -> r0..r3
        // r0 <- original; CALL 0 args, 4 results -> r0..r3
        b.EmitABx(LuaOpcode.GetGlobal, a: 0, bx: k.Str(ScreenInfoBackupField));
        b.Emit(LuaOpcode.Call, a: 0, b: 1, c: 5);

        // SETGLOBAL не забирає регістр-джерело — тож r0..r3 лишаються цілими
        // для RETURN нижче.
        // SETGLOBAL does not consume the source register — r0..r3 stay intact
        // for the RETURN below.
        b.EmitABx(LuaOpcode.SetGlobal, a: 0, bx: k.Str(ScreenInfoProbeW));
        b.EmitABx(LuaOpcode.SetGlobal, a: 1, bx: k.Str(ScreenInfoProbeH));
        b.EmitABx(LuaOpcode.SetGlobal, a: 2, bx: k.Str(ScreenInfoProbeV));
        b.EmitABx(LuaOpcode.SetGlobal, a: 3, bx: k.Str(ScreenInfoProbeWidescreen));

        b.Emit(LuaOpcode.Return, a: 0, b: 5);

        if (k.Count >= 383)
            throw new InvalidOperationException(
                $"Перевищено ліміт адресації RK / RK addressing limit exceeded: {k.Count}");

        return b.Build(path: path);
    }

    // UA: Крок встановлення — ідемпотентний, той самий стиль, що й
    //     BuildPopupTutorialInstallStep.
    // EN: The install step — idempotent, the same style as
    //     BuildPopupTutorialInstallStep.
    private static LuaFunctionPrototype BuildScreenInfoProbeInstallStep(string path)
    {
        var b = new Lua50FunctionBuilder { NumParams = 0, IsVararg = 0, MaxStackSize = 3 };
        var k = new ConstantCache(b);

        // вже встановлено? (c=1 -> перехід, якщо backup-глобал ІСНУЄ)
        // already installed? (c=1 -> jump when the backup global EXISTS)
        b.EmitABx(LuaOpcode.GetGlobal, a: 0, bx: k.Str(ScreenInfoBackupField));
        b.EmitTest(register: 0, c: 1);
        var alreadyInstalled = b.EmitJumpPlaceholder();

        // оригінал є в цій збірці гри?
        // does this build of the game have the original?
        b.EmitABx(LuaOpcode.GetGlobal, a: 1, bx: k.Str(ScreenInfoFn));
        b.EmitTest(register: 1, c: 0);
        var noOriginal = b.EmitJumpPlaceholder();

        b.EmitABx(LuaOpcode.SetGlobal, a: 1, bx: k.Str(ScreenInfoBackupField));
        var nested = b.AddNestedPrototype(BuildScreenInfoProbeWrapper($"{path}/ScreenInfoProbe"));
        b.EmitABx(LuaOpcode.Closure, a: 2, bx: nested);
        b.EmitABx(LuaOpcode.SetGlobal, a: 2, bx: k.Str(ScreenInfoFn));

        b.PatchJump(noOriginal, b.NextPc);
        b.PatchJump(alreadyInstalled, b.NextPc);

        b.Emit(LuaOpcode.Return, a: 0, b: 1);
        return b.Build(path: path);
    }

    // UA: Бесспокійний крок ВІДОБРАЖЕННЯ — лише для ifs_missionselect. f(t):
    //     t.option_buttons.setting (той самий шлях, що й підтверджений
    //     "@post:option_buttons.setting"), помножене на 500 значення
    //     gWSProbe_Widescreen як posY.
    // EN: The bespoke DISPLAY step — ifs_missionselect only. f(t):
    //     t.option_buttons.setting (the same path as the confirmed
    //     "@post:option_buttons.setting"), gWSProbe_Widescreen x500 as posY.
    private static LuaFunctionPrototype BuildScreenInfoProbeDisplayStep(string path)
    {
        var b = new Lua50FunctionBuilder { NumParams = 1, IsVararg = 0, MaxStackSize = 9 };
        var k = new ConstantCache(b);
        var exits = new List<int>();

        b.Emit(LuaOpcode.GetTable, a: 1, b: 0, c: Lua50FunctionBuilder.Rk(k.Str("option_buttons")));
        b.EmitTest(register: 1, c: 0);
        exits.Add(b.EmitJumpPlaceholder());

        b.Emit(LuaOpcode.GetTable, a: 1, b: 1, c: Lua50FunctionBuilder.Rk(k.Str("setting")));
        b.EmitTest(register: 1, c: 0);
        exits.Add(b.EmitJumpPlaceholder());

        b.EmitABx(LuaOpcode.GetGlobal, a: 2, bx: k.Str(ScreenInfoProbeWidescreen));
        b.EmitTest(register: 2, c: 0);
        exits.Add(b.EmitJumpPlaceholder());

        // r3 = r2 * 500 — переводимо безрозмірний множник у піксельно вимірну
        // відстань.
        // r3 = r2 * 500 — turns the dimensionless multiplier into a
        // pixel-measurable distance.
        b.Emit(LuaOpcode.Mul, a: 3, b: 2, c: Lua50FunctionBuilder.Rk(k.Num(ScreenInfoProbeMultiplier)));

        b.EmitABx(LuaOpcode.GetGlobal, a: 4, bx: k.Str(PosYFn));
        b.EmitTest(register: 4, c: 0);
        exits.Add(b.EmitJumpPlaceholder());

        b.Emit(LuaOpcode.Move, a: 5, b: 1);                  // self
        b.Emit(LuaOpcode.LoadNil, a: 6, b: 6);               // x = nil
        b.Emit(LuaOpcode.Move, a: 7, b: 3);                  // y = обчислене / computed
        b.Emit(LuaOpcode.LoadNil, a: 8, b: 8);               // z = nil
        b.Emit(LuaOpcode.Call, a: 4, b: 5, c: 1);            // 4 аргументи / 4 args

        foreach (var e in exits) b.PatchJump(e, b.NextPc);

        b.Emit(LuaOpcode.Return, a: 0, b: 1);

        if (k.Count >= 383)
            throw new InvalidOperationException(
                $"Перевищено ліміт адресації RK / RK addressing limit exceeded: {k.Count}");

        return b.Build(path: path);
    }
    // =========================================================================

    // =========================================================================
    // UA: Фікс "оверскан фону". Вмикається прапорцем includeBackgroundSizeFix у
    //     BuildInstallerScript — і окремою діагностичною командою
    //     (GenerateBackgroundSizeFixShellCommand, вихід "shell_bgfix.lvl",
    //     для точкового тестування без перезбирання основного патча), і
    //     production-командою (GenerateAnchorFixShellCommand, вихід
    //     "shell_layout.lvl"). За замовчуванням прапорець лишається `false`
    //     (нічого не викликає сам по собі) — обидва місця виклику передають
    //     `true` явно.
    //
    //     ЩО ПІДТВЕРДЖЕНО ЗОНДОМ widescreen: реальне
    //     `widescreen` на 1920×1080 = 1.3333(3) = (1920/1080)/(800/600) —
    //     точно співвідношення "екранний аспект / авторський 4:3 аспект".
    //     Свіже дизасемблювання `ifelem_shellscreen_fnAddBackground`
    //     (common.lvl, ifelem_shellscreen, nested[15]) дало ПОВНУ, реєстрову
    //     картину (не лише формулу для localpos_r, а й КУДИ саме зберігається
    //     фоновий об'єкт):
    //         SETTABLE R0[K0('bg')] := nil                 -- pc0, обнулення
    //         ... NewIFImage(...) ...                       -- pc1-12
    //         SETTABLE R0[K0('bg')] := R2                   -- pc12: bg = НОВИЙ NewIFImage
    //         ... w,h,v,widescreen = ScriptCB_GetScreenInfo() ...  -- pc14-19
    //         bg.localpos_r := w * widescreen                -- pc20-22 (2560 при w=1920)
    //         bg.localpos_b := h                              -- pc23-24
    //         bg.uvs_b := v                                   -- pc25-26
    //     КЛЮЧОВИЙ ФАКТ: `localpos_r`/`localpos_b`/`uvs_b` НЕ входять у
    //     початкову конструкторську таблицю `NewIFImage` (pc2-10 туди кладуть
    //     лише ScreenRelativeX/Y, UseSafezone, ZPos, texture, localpos_l,
    //     localpos_t, inert) — вони дописуються ОКРЕМИМИ SETTABLE вже НА
    //     ГОТОВИЙ повернений об'єкт (`R6 := R0['bg']`, тричі, після CALL).
    //     Це означає: сам ВАНІЛЬНИЙ скрипт покладається на те, що запис цих
    //     полів ПІСЛЯ створення IFImage реально впливає на рендер — інакше
    //     гра сама не робила б так. Тобто ЩЕ ОДИН такий самий запис (доданий,
    //     ПІСЛЯ оригіналу) має спрацювати так само надійно.
    //
    //     Ім'я поля на екранній таблиці — буквально `bg` (підтверджено:
    //     `ifs_missionselect` встановлює власний `t.bg_texture =
    //     "iface_bgmeta_space"` до виклику `AddIFScreen` — дизасемблювання
    //     shell.lvl/ifs_missionselect, root pc65 — і `NewIFShellScreen_common`
    //     викликає `fnAddBackground(t, t.bg_texture)` лише коли
    //     `ScriptCB_GetShellActive()` істинний; інакше `t.bg = nil`).
    //
    //     ЦЕЙ ФІКС: контейнер фону навмисно (чи ні — невідомо) будується НА
    //     33% ШИРШИМ за екран (`w × widescreen` замість `w`), тоді як
    //     `uvs_r`/`uvs_t` НІКОЛИ не проставляються (лишаються на заводських
    //     значеннях `NewIFImage`). Обгортка `ifelem_shellscreen_fnAddBackground`
    //     (глобальна функція — виявлено тим самим CLOSURE+SETGLOBAL прийомом,
    //     іменем підтверджено в дизасемблюванні) ПІСЛЯ виклику оригіналу
    //     примусово повертає `bg.localpos_r` до РІВНО `w` (без множника) —
    //     той самий підхід, що й `bg.localpos_b := h` вище (яке ЖОДНОГО разу
    //     не викликало скарг: висота фону завжди відповідала екрану, лише
    //     ширина — ні).
    //
    //     ПІДТВЕРДЖЕНО ЗНІМКАМИ: пікселева звірка показала, що ДО фіксу
    //     контейнер справді на 33% ширший за екран і обрізає праву чверть
    //     фонової картинки (обрізаний правий край); ПІСЛЯ фіксу
    //     контент повертається, без нових дефектів. Перевірено на 4 з 7
    //     реально знайдених значень `bg_texture` (`iface_bgmeta_space` — 8
    //     екранів, `iface_bg_1` — 5, `single_player_campaign` — 2,
    //     `profile_manager` — 1; разом 16 з ~20 фактичних використань
    //     `fnAddBackground` у shell.lvl). НЕ ПЕРЕВІРЕНІ (потребують окремого
    //     скріншот-тесту через GenerateBackgroundSizeFixShellCommand):
    //     `single_player_conquest` (той самий код, що вже підтверджений
    //     `single_player_campaign`, тому низький ризик), `single_player_option`
    //     і одне динамічне значення в `ifs_tutorials`. Питання висоти закрито
    //     аналізом без зміни коду: `localpos_b := h` не має множника в жодному
    //     з перевірених випадків. Фікс — у production
    //     (`GenerateAnchorFixShellCommand`/`shell_layout.lvl`), поряд з
    //     ізольованою діагностичною збіркою для точкових тестів нових
    //     екранів/текстур.
    // EN: The "background overscan" fix. Enabled by an includeBackgroundSizeFix flag on
    //     BuildInstallerScript — used both by a separate diagnostic command
    //     (GenerateBackgroundSizeFixShellCommand, output "shell_bgfix.lvl",
    //     for spot-testing without rebuilding the main patch) and by
    //     the production command (GenerateAnchorFixShellCommand, output
    //     "shell_layout.lvl"). The flag still defaults to `false` (calls
    //     nothing on its own) — both call sites pass `true` explicitly.
    //
    //     WHAT THE widescreen PROBE CONFIRMED: the real
    //     `widescreen` at 1920x1080 = 1.3333(3) = (1920/1080)/(800/600) —
    //     exactly the "screen aspect / authored 4:3 aspect" ratio. A fresh
    //     disassembly of `ifelem_shellscreen_fnAddBackground` (common.lvl,
    //     ifelem_shellscreen, nested[15]) gave the FULL, register-precise
    //     picture (not just the localpos_r formula, but WHERE the background
    //     object is actually stored):
    //         SETTABLE R0[K0('bg')] := nil                 -- pc0, cleared first
    //         ... NewIFImage(...) ...                       -- pc1-12
    //         SETTABLE R0[K0('bg')] := R2                   -- pc12: bg = the NEW NewIFImage
    //         ... w,h,v,widescreen = ScriptCB_GetScreenInfo() ...  -- pc14-19
    //         bg.localpos_r := w * widescreen                -- pc20-22 (2560 when w=1920)
    //         bg.localpos_b := h                              -- pc23-24
    //         bg.uvs_b := v                                   -- pc25-26
    //     KEY FACT: `localpos_r`/`localpos_b`/`uvs_b` are NOT part of the
    //     initial `NewIFImage` constructor table (pc2-10 only set
    //     ScreenRelativeX/Y, UseSafezone, ZPos, texture, localpos_l,
    //     localpos_t, inert) — they are added by SEPARATE SETTABLEs onto the
    //     ALREADY-RETURNED object (`R6 := R0['bg']`, three times, after the
    //     CALL). This means the VANILLA script itself relies on writing these
    //     fields AFTER IFImage creation actually affecting the render —
    //     otherwise the game wouldn't do it this way. So ONE MORE such write
    //     (ours, AFTER the original) should work exactly as reliably.
    //
    //     The field name on the screen table is literally `bg` (confirmed:
    //     `ifs_missionselect` sets its own `t.bg_texture =
    //     "iface_bgmeta_space"` before calling `AddIFScreen` — disassembly of
    //     shell.lvl/ifs_missionselect, root pc65 — and
    //     `NewIFShellScreen_common` calls `fnAddBackground(t, t.bg_texture)`
    //     only when `ScriptCB_GetShellActive()` is true; otherwise `t.bg =
    //     nil`).
    //
    //     THIS FIX: the background container is built (whether deliberately
    //     or not — unknown) 33% WIDER than the screen (`w x widescreen`
    //     instead of `w`), while `uvs_r`/`uvs_t` are NEVER set (staying at
    //     `NewIFImage`'s factory defaults). The wrapper around
    //     `ifelem_shellscreen_fnAddBackground` (a global function — found by
    //     the same CLOSURE+SETGLOBAL technique, name confirmed by
    //     disassembly), AFTER the original runs, forces `bg.localpos_r` back
    //     to EXACTLY `w` (no multiplier) — the same approach as
    //     `bg.localpos_b := h` above (which has NEVER drawn a complaint: the
    //     background's height always matched the screen, only its width
    //     didn't).
    //
    //     CONFIRMED BY SCREENSHOTS: a pixel-level comparison showed that
    //     BEFORE the fix the container really is 33% wider than the screen
    //     and clips the right quarter of the background artwork (a clipped
    //     right edge); AFTER the fix the content comes back, with no new
    //     defects.
    //     Verified on 4 of 7 actually-found `bg_texture` values
    //     (`iface_bgmeta_space` — 8 screens, `iface_bg_1` — 5,
    //     `single_player_campaign` — 2, `profile_manager` — 1; 16 of ~20
    //     actual `fnAddBackground` usages in shell.lvl total). NOT VERIFIED
    //     (need a separate screenshot test via
    //     GenerateBackgroundSizeFixShellCommand): `single_player_conquest`
    //     (same code path as the already-confirmed `single_player_campaign`,
    //     so low risk), `single_player_option`, and one dynamic value in
    //     `ifs_tutorials`. The height question is closed by analysis, no code
    //     change: `localpos_b := h`
    //     has no multiplier in any checked case. The fix is
    //     in production (`GenerateAnchorFixShellCommand`/`shell_layout.lvl`),
    //     alongside the isolated diagnostic build kept for spot-testing new
    //     screens/textures.
    // =========================================================================
    private const string BgFixFn = "ifelem_shellscreen_fnAddBackground";
    private const string BgFixBackupField = "ifelem_shellscreen_fnAddBackground_ws_o";
    private const string BgFixField = "bg";
    private const string BgFixLocalPosR = "localpos_r";

    // UA: Сама обгортка. Сигнатура ТОЧНО як в оригіналу: 2 параметри
    //     (self, texture), 0 повернених значень (весь ефект — мутація
    //     таблиці self за посиланням). Викликає оригінал, тоді сам питає
    //     ScriptCB_GetScreenInfo() (незалежно від зонда — цей фікс має
    //     працювати й без нього) і повертає bg.localpos_r до w.
    // EN: The wrapper itself. Signature EXACTLY like the original: 2
    //     parameters (self, texture), 0 return values (all effect is via
    //     mutating the self table by reference). Calls the original, then
    //     asks ScriptCB_GetScreenInfo() itself (independent of the probe —
    //     this fix must work without it too) and resets bg.localpos_r to w.
    private static LuaFunctionPrototype BuildBackgroundSizeFixWrapper(string path)
    {
        var b = new Lua50FunctionBuilder { NumParams = 2, IsVararg = 0, MaxStackSize = 7 };
        var k = new ConstantCache(b);
        var exits = new List<int>();

        // 1) делегувати оригіналу(self, texture) — 2 аргументи, 0 результатів
        // 1) delegate to original(self, texture) — 2 args, 0 results
        b.EmitABx(LuaOpcode.GetGlobal, a: 2, bx: k.Str(BgFixBackupField));
        b.EmitTest(register: 2, c: 0);
        var noOriginal = b.EmitJumpPlaceholder();

        b.Emit(LuaOpcode.Move, a: 3, b: 0);
        b.Emit(LuaOpcode.Move, a: 4, b: 1);
        b.Emit(LuaOpcode.Call, a: 2, b: 3, c: 1);

        b.PatchJump(noOriginal, b.NextPc);

        // 2) w, _, _, _ = ScriptCB_GetScreenInfo() — незалежно від зонда
        // 2) w, _, _, _ = ScriptCB_GetScreenInfo() — independent of the probe
        b.EmitABx(LuaOpcode.GetGlobal, a: 2, bx: k.Str(ScreenInfoFn));
        b.EmitTest(register: 2, c: 0);
        exits.Add(b.EmitJumpPlaceholder());

        b.Emit(LuaOpcode.Call, a: 2, b: 1, c: 5);   // 0 аргументів, 4 результати -> r2..r5

        // 3) self.bg.localpos_r = w (r2)
        // 3) self.bg.localpos_r = w (r2)
        b.Emit(LuaOpcode.GetTable, a: 6, b: 0, c: Lua50FunctionBuilder.Rk(k.Str(BgFixField)));
        b.EmitTest(register: 6, c: 0);
        exits.Add(b.EmitJumpPlaceholder());

        b.Emit(LuaOpcode.SetTable, a: 6,
               b: Lua50FunctionBuilder.Rk(k.Str(BgFixLocalPosR)),
               c: 2);

        foreach (var e in exits) b.PatchJump(e, b.NextPc);

        b.Emit(LuaOpcode.Return, a: 0, b: 1);

        if (k.Count >= 383)
            throw new InvalidOperationException(
                $"Перевищено ліміт адресації RK / RK addressing limit exceeded: {k.Count}");

        return b.Build(path: path);
    }

    // UA: Ідемпотентний крок встановлення — той самий стиль, що й для
    //     Popup_Tutorial і зонда widescreen.
    // EN: Idempotent install step — the same style as for Popup_Tutorial and
    //     the widescreen probe.
    private static LuaFunctionPrototype BuildBackgroundSizeFixInstallStep(string path)
    {
        var b = new Lua50FunctionBuilder { NumParams = 0, IsVararg = 0, MaxStackSize = 3 };
        var k = new ConstantCache(b);

        b.EmitABx(LuaOpcode.GetGlobal, a: 0, bx: k.Str(BgFixBackupField));
        b.EmitTest(register: 0, c: 1);
        var alreadyInstalled = b.EmitJumpPlaceholder();

        b.EmitABx(LuaOpcode.GetGlobal, a: 1, bx: k.Str(BgFixFn));
        b.EmitTest(register: 1, c: 0);
        var noOriginal = b.EmitJumpPlaceholder();

        b.EmitABx(LuaOpcode.SetGlobal, a: 1, bx: k.Str(BgFixBackupField));
        var nested = b.AddNestedPrototype(BuildBackgroundSizeFixWrapper($"{path}/BackgroundSizeFix"));
        b.EmitABx(LuaOpcode.Closure, a: 2, bx: nested);
        b.EmitABx(LuaOpcode.SetGlobal, a: 2, bx: k.Str(BgFixFn));

        b.PatchJump(noOriginal, b.NextPc);
        b.PatchJump(alreadyInstalled, b.NextPc);

        b.Emit(LuaOpcode.Return, a: 0, b: 1);
        return b.Build(path: path);
    }
    // =========================================================================

    // -------------------------------------------------------------------------
    // UA: Обгортка AddIFScreen(таблиця, ім'я): вибрати екран за іменем,
    //     застосувати його порції і віддати керування оригіналу.
    // EN: The AddIFScreen(table, name) wrapper: dispatch on the name, run that
    //     screen's chunks, then delegate to the original.
    // -------------------------------------------------------------------------
    private static LuaFunctionPrototype BuildDispatchWrapper(float scale, string path,
        bool includeScreenInfoProbe = false, bool includeBackgroundSizeFix = false)
    {
        var b = new Lua50FunctionBuilder { NumParams = 2, IsVararg = 0, MaxStackSize = 6 };
        var k = new ConstantCache(b);
        var kOrig = k.Str(BackupName);

        var toTail = new List<int>();

        // UA: РОЗГЛЯНУТО Й ВІДХИЛЕНО — шаблонне правило "*" на всі екрани.
        //     Спокуса: PCTitleText_Profile / PCTitleText_Title (ім'я профілю та
        //     версія гри) мають однакові значення скрізь, де є. Вимір показав,
        //     що "скрізь" — це не вся гра: у shell.lvl 78 екранів, ці написи є
        //     лише на 14, а вкладки, які їх затуляють, — на 17; водночас те й
        //     інше є на 13. Отже екран ifs_sp_campaign_list має напис БЕЗ
        //     вкладок, і шаблон зсунув би його дарма — зміна там, де дефекту
        //     нема (порушення правила 1). Явний перелік екранів точніший і
        //     дешевший (26 записів проти уявних 324), тож механізм не потрібен.
        // EN: CONSIDERED AND REJECTED — a "*" wildcard rule for all screens.
        //     Tempting because PCTitleText_Profile / PCTitleText_Title (profile
        //     name and game version) hold identical values wherever they exist.
        //     Measurement showed "wherever" is not the whole game: shell.lvl has
        //     78 screens, these labels appear on only 14, and the tabs that cover
        //     them on 17 — with both present on 13. So ifs_sp_campaign_list has
        //     the label WITHOUT tabs, and a wildcard would move it for nothing —
        //     a change where no defect exists (violating rule 1). An explicit
        //     screen list is more precise and cheaper (26 rows, not the imagined
        //     324), so the mechanism isn't needed.

        foreach (var screen in Table.Keys.OrderBy(s => s, StringComparer.Ordinal))
        {
            // UA: рядки "@post:" виконуються ПІСЛЯ оригінального AddIFScreen,
            //     решта — як раніше, ДО нього
            // EN: "@post:" rows run AFTER the original AddIFScreen, the rest
            //     run before it, as before
            // UA: "@hook:" — теж НЕ пре-білд: це не запис у таблицю конструктора,
            //     а встановлення обгортки (нижче). Без цього виключення рядок
            //     потрапив би у BuildChunkFunction і був би витлумачений як шлях
            //     віджета з масштабованими полями.
            // EN: "@hook:" is NOT pre-build either: it installs a wrapper (below)
            //     rather than writing into a constructor table. Without this
            //     exclusion the row would reach BuildChunkFunction and be read as a
            //     widget path with scalable fields.
            var pre = Table[screen].Where(e => !e.Path.StartsWith(PostPrefix, StringComparison.Ordinal)
                                            && !e.Path.StartsWith(HookPrefix, StringComparison.Ordinal)
                                            && !e.Path.StartsWith(InitListPrefix, StringComparison.Ordinal)
                                            && e.Path != PostScreenPath)
                                   .OrderBy(e => e.Path, StringComparer.Ordinal).ToList();
            if (pre.Count == 0) continue;

            b.Emit(LuaOpcode.Eq, a: 0, b: 1, c: Lua50FunctionBuilder.Rk(k.Str(screen)));
            var nextScreen = b.EmitJumpPlaceholder();

            var chunks = ChunkScreen(pre);
            for (var i = 0; i < chunks.Count; i++)
            {
                var nested = b.AddNestedPrototype(
                    BuildChunkFunction(chunks[i], scale, $"{path}/{screen}#{i}"));
                b.EmitABx(LuaOpcode.Closure, a: 2, bx: nested);
                b.Emit(LuaOpcode.Move, a: 3, b: 0);
                b.Emit(LuaOpcode.Call, a: 2, b: 2, c: 1);
            }

            toTail.Add(b.EmitJumpPlaceholder());
            b.PatchJump(nextScreen, b.NextPc);
        }

        foreach (var j in toTail) b.PatchJump(j, b.NextPc);

        // ---------------------------------------------------------------------
        // UA: Встановлення обгортки "@initlist:" — НАВМИСНО ДО виклику
        //     оригінального AddIFScreen, на відміну від "@hook:" нижче.
        //     ПРИЧИНА: обгортка має стояти раніше, ніж ІНШИЙ екран побудує свої
        //     списки, а побудова кожного екрана відбувається в його власному
        //     чанку ПЕРЕД його ж викликом AddIFScreen. Тобто для екрана-цілі
        //     рятує лише те, що ДО нього AddIFScreen викликав хтось інший:
        //     у shell.lvl ifs_boot/ifs_legal/ifs_start ідуть 6-8-ми, а
        //     ifs_missionselect — 11-м, ifs_missionselect_pcmulti — 88-м, тож
        //     запас великий. Розміщення ДО оригіналу додає ще один екран запасу
        //     задарма. Крок ідемпотентний (перевірка "<Fn>_ws_o вже є"), тож
        //     виконується реально лише один раз за гру.
        // EN: Installing the "@initlist:" wrapper — DELIBERATELY BEFORE the
        //     original AddIFScreen call, unlike the "@hook:" installs below.
        //     WHY: the wrapper must be in place before ANOTHER screen builds its
        //     lists, and every screen builds inside its own chunk BEFORE its own
        //     AddIFScreen call. So the target screen is only saved by some other
        //     screen having called AddIFScreen earlier: in shell.lvl
        //     ifs_boot/ifs_legal/ifs_start are 6th-8th while ifs_missionselect is
        //     11th and ifs_missionselect_pcmulti 88th — a comfortable margin.
        //     Installing before the original buys one more screen of margin for
        //     free. The step is idempotent (the "<Fn>_ws_o already exists"
        //     check), so it really runs only once per game session.
        // ---------------------------------------------------------------------
        var initListTargets = InitListTargets();
        if (initListTargets.Count > 0)
        {
            var initWrapper = b.AddNestedPrototype(
                BuildInitListWrapper(initListTargets, $"{path}/initlist#{InitListFn}"));

            // UA: немає такої функції в цій збірці гри — нічого не робимо
            // EN: no such function in this build of the game — do nothing
            b.EmitABx(LuaOpcode.GetGlobal, a: 2, bx: k.Str(InitListFn));
            b.EmitTest(register: 2, c: 0);
            var noInitFn = b.EmitJumpPlaceholder();

            // UA: уже обгорнуто? (c=1 -> перехід, якщо резерв ІСНУЄ)
            // EN: already wrapped? (c=1 -> jump when the backup EXISTS)
            b.EmitABx(LuaOpcode.GetGlobal, a: 3, bx: k.Str(InitListFn + HookBackupSuffix));
            b.EmitTest(register: 3, c: 1);
            var alreadyInit = b.EmitJumpPlaceholder();

            b.EmitABx(LuaOpcode.SetGlobal, a: 2, bx: k.Str(InitListFn + HookBackupSuffix));
            b.EmitABx(LuaOpcode.Closure, a: 3, bx: initWrapper);
            b.EmitABx(LuaOpcode.SetGlobal, a: 3, bx: k.Str(InitListFn));

            b.PatchJump(alreadyInit, b.NextPc);
            b.PatchJump(noInitFn, b.NextPc);
        }

        b.EmitABx(LuaOpcode.GetGlobal, a: 2, bx: kOrig);
        b.Emit(LuaOpcode.Move, a: 3, b: 0);
        b.Emit(LuaOpcode.Move, a: 4, b: 1);
        b.Emit(LuaOpcode.Call, a: 2, b: 3, c: 1);

        // ---------------------------------------------------------------------
        // UA: Безумовний, ідемпотентний крок встановлення хука на
        //     Popup_Tutorial.SetPage (див. пояснення біля
        //     BuildPopupTutorialInstallStep). Викликається на КОЖЕН реальний
        //     AddIFScreen, бо Popup_Tutorial може ще не існувати в момент
        //     роботи кореневого інсталятора — щойно з'явиться, перший-таки
        //     виклик тут його обгорне; усі наступні вийдуть одразу через
        //     nil/вже-обгорнуто перевірку.
        // EN: An unconditional, idempotent install step for the
        //     Popup_Tutorial.SetPage hook (see the rationale near
        //     BuildPopupTutorialInstallStep). Called on EVERY real
        //     AddIFScreen, because Popup_Tutorial may not exist yet when the
        //     root installer runs — the first call after it appears wraps
        //     it; every later call exits immediately via the nil/already-
        //     wrapped check.
        // ---------------------------------------------------------------------
        var popupInstall = b.AddNestedPrototype(BuildPopupTutorialInstallStep($"{path}/PopupTutorialInstall"));
        b.EmitABx(LuaOpcode.Closure, a: 2, bx: popupInstall);
        b.Emit(LuaOpcode.Call, a: 2, b: 1, c: 1);

        // ---------------------------------------------------------------------
        // UA: ДІАГНОСТИЧНИЙ ЗОНД "widescreen" — крок ВСТАНОВЛЕННЯ обгортки
        //     (див. розгорнутий коментар вище, перед BuildDispatchWrapper).
        //     УВІМКНЕНО ЛИШЕ якщо includeScreenInfoProbe=true. За
        //     замовчуванням (production, shell_layout.lvl) цей блок не додає
        //     ЖОДНОЇ інструкції. Крок ВІДОБРАЖЕННЯ (запис Y-позиції) навмисно
        //     НЕ тут — див. пояснення в кінці функції, перед фінальним Return.
        // EN: The "widescreen" DIAGNOSTIC PROBE — the wrapper INSTALL step
        //     (see the detailed comment above, right before
        //     BuildDispatchWrapper). ENABLED ONLY when
        //     includeScreenInfoProbe=true. By default (production,
        //     shell_layout.lvl) this block adds NO instructions at all. The
        //     DISPLAY step (writing the Y-position) is deliberately NOT
        //     here — see the rationale at the end of this function, right
        //     before the final Return.
        // ---------------------------------------------------------------------
        if (includeScreenInfoProbe)
        {
            var probeInstall = b.AddNestedPrototype(
                BuildScreenInfoProbeInstallStep($"{path}/ScreenInfoProbeInstall"));
            b.EmitABx(LuaOpcode.Closure, a: 2, bx: probeInstall);
            b.Emit(LuaOpcode.Call, a: 2, b: 1, c: 1);
        }

        // ---------------------------------------------------------------------
        // UA: Фікс "оверскан фону" — крок встановлення обгортки (див.
        //     розгорнутий коментар вище, перед BuildDispatchWrapper).
        //     Прапорець includeBackgroundSizeFix за замовчуванням `false`
        //     (тоді цей блок не додає ЖОДНОЇ інструкції), production-виклик
        //     (GenerateAnchorFixShellCommand) передає
        //     `true` явно — фікс УВІМКНЕНО в shell_layout.lvl. Порядок ТУТ не
        //     має значення (на відміну від зонда) — сама обгортка діє
        //     ПОВНІСТЮ всередині одного виклику fnAddBackground, задовго до
        //     того, як @post:/@postscreen цього ж екрана взагалі запускаються.
        // EN: The "background overscan" fix — the wrapper INSTALL step (see
        //     the detailed comment above, right before BuildDispatchWrapper).
        //     The includeBackgroundSizeFix flag defaults to `false` (then
        //     this block adds NO instructions at all), the production call
        //     site (GenerateAnchorFixShellCommand) passes
        //     `true` explicitly — the fix is ENABLED in shell_layout.lvl.
        //     Order does NOT matter here (unlike the probe) — the wrapper
        //     acts ENTIRELY inside one fnAddBackground call, long before this
        //     screen's own @post:/@postscreen even run.
        // ---------------------------------------------------------------------
        if (includeBackgroundSizeFix)
        {
            var bgFixInstall = b.AddNestedPrototype(
                BuildBackgroundSizeFixInstallStep($"{path}/BackgroundSizeFixInstall"));
            b.EmitABx(LuaOpcode.Closure, a: 2, bx: bgFixInstall);
            b.Emit(LuaOpcode.Call, a: 2, b: 1, c: 1);
        }

        // ---------------------------------------------------------------------
        // UA: Встановлення обгорток "@hook:" — так само безумовно й
        //     ідемпотентно, як крок вище. Чому саме тут, а не в кореневому
        //     інсталяторі: обгортані сеттери живуть у скриптах інтерфейсу
        //     (ifelem_*, interface_util, самі екрани), які завантажуються
        //     ПІЗНІШЕ за точку входу — на момент роботи кореня їх ще нема.
        //     Перший же AddIFScreen після їх появи обгорне, усі наступні
        //     виходять одразу через перевірку "<Fn>_ws_o вже є".
        // EN: Installing the "@hook:" wrappers — as unconditional and
        //     idempotent as the step above. Why here and not in the root
        //     installer: the setters being wrapped live in interface scripts
        //     (ifelem_*, interface_util, the screens themselves) that load
        //     LATER than the entry point — they do not exist yet when the root
        //     runs. The first AddIFScreen after they appear wraps them; every
        //     later one exits at once via the "<Fn>_ws_o already exists" check.
        // ---------------------------------------------------------------------
        foreach (var (hookFn, hookItems) in HookGroups())
        {
            var wrapper = b.AddNestedPrototype(
                BuildHookWrapper(hookFn, hookItems, $"{path}/hook#{hookFn}"));

            // UA: немає такої функції в цій збірці гри — нічого не робимо
            // EN: no such function in this build of the game — do nothing
            b.EmitABx(LuaOpcode.GetGlobal, a: 2, bx: k.Str(hookFn));
            b.EmitTest(register: 2, c: 0);
            var noHookFn = b.EmitJumpPlaceholder();

            // UA: уже обгорнуто? (c=1 -> перехід, якщо резерв ІСНУЄ)
            // EN: already wrapped? (c=1 -> jump when the backup EXISTS)
            b.EmitABx(LuaOpcode.GetGlobal, a: 3, bx: k.Str(hookFn + HookBackupSuffix));
            b.EmitTest(register: 3, c: 1);
            var alreadyHooked = b.EmitJumpPlaceholder();

            b.EmitABx(LuaOpcode.SetGlobal, a: 2, bx: k.Str(hookFn + HookBackupSuffix));
            b.EmitABx(LuaOpcode.Closure, a: 3, bx: wrapper);
            b.EmitABx(LuaOpcode.SetGlobal, a: 3, bx: k.Str(hookFn));

            b.PatchJump(alreadyHooked, b.NextPc);
            b.PatchJump(noHookFn, b.NextPc);
        }

        // ---------------------------------------------------------------------
        // UA: ГАЧОК «ПІСЛЯ ПОБУДОВИ». Саме тут — і ніде раніше — віджети вже
        //     мають дескриптори 'cp'. Вставка безпечна: усі PatchJump вище вже
        //     виконані, тож додавання інструкцій між CALL і RETURN не зсуває
        //     жодного переходу. Оригінал викликано через CALL (не TAILCALL),
        //     тому керування сюди повертається.
        // EN: THE "AFTER BUILD" HOOK. Here — and nowhere earlier — the widgets
        //     already hold their 'cp' handles. The insertion is safe: every
        //     PatchJump above has already run, so adding instructions between
        //     CALL and RETURN shifts no jump target. The original was invoked
        //     with CALL (not TAILCALL), so control returns here.
        // ---------------------------------------------------------------------
        var postTail = new List<int>();
        foreach (var screen in Table.Keys.OrderBy(s => s, StringComparer.Ordinal))
        {
            var post = Table[screen].Where(e => e.Path.StartsWith(PostPrefix, StringComparison.Ordinal))
                                    .OrderBy(e => e.Path, StringComparer.Ordinal).ToList();
            if (post.Count == 0) continue;

            b.Emit(LuaOpcode.Eq, a: 0, b: 1, c: Lua50FunctionBuilder.Rk(k.Str(screen)));
            var nextScreen = b.EmitJumpPlaceholder();

            var nested = b.AddNestedPrototype(
                BuildPostChunkFunction(post, $"{path}/{screen}#post"));
            b.EmitABx(LuaOpcode.Closure, a: 2, bx: nested);
            b.Emit(LuaOpcode.Move, a: 3, b: 0);
            b.Emit(LuaOpcode.Call, a: 2, b: 2, c: 1);

            postTail.Add(b.EmitJumpPlaceholder());
            b.PatchJump(nextScreen, b.NextPc);
        }
        foreach (var j in postTail) b.PatchJump(j, b.NextPc);

        // ---------------------------------------------------------------------
        // UA: "@postscreen" — та сама точка (ПІСЛЯ CALL оригіналу, ДО RETURN),
        //     що й гачок "@post:" вище, але без GETTABLE-проходу й без 'cp' —
        //     SETTABLE напряму в R0 (див. пояснення біля PostScreenPath і
        //     BuildPostScreenChunkFunction).
        // EN: "@postscreen" — the same point (AFTER the original's CALL,
        //     BEFORE RETURN) as the "@post:" hook above, but with no GETTABLE
        //     walk and no 'cp' — a direct SETTABLE onto R0 (see the rationale
        //     by PostScreenPath and BuildPostScreenChunkFunction).
        // ---------------------------------------------------------------------
        var postScreenTail = new List<int>();
        foreach (var screen in Table.Keys.OrderBy(s => s, StringComparer.Ordinal))
        {
            var postScreen = Table[screen].Where(e => e.Path == PostScreenPath).ToList();
            if (postScreen.Count == 0) continue;

            b.Emit(LuaOpcode.Eq, a: 0, b: 1, c: Lua50FunctionBuilder.Rk(k.Str(screen)));
            var nextScreen = b.EmitJumpPlaceholder();

            var nested = b.AddNestedPrototype(
                BuildPostScreenChunkFunction(postScreen, scale, $"{path}/{screen}#postscreen"));
            b.EmitABx(LuaOpcode.Closure, a: 2, bx: nested);
            b.Emit(LuaOpcode.Move, a: 3, b: 0);
            b.Emit(LuaOpcode.Call, a: 2, b: 2, c: 1);

            postScreenTail.Add(b.EmitJumpPlaceholder());
            b.PatchJump(nextScreen, b.NextPc);
        }
        foreach (var j in postScreenTail) b.PatchJump(j, b.NextPc);

        // ---------------------------------------------------------------------
        // UA: ДІАГНОСТИЧНИЙ ЗОНД "widescreen" — крок ВІДОБРАЖЕННЯ. НАВМИСНО
        //     останній перед Return, а НЕ одразу після встановлення обгортки
        //     (вище, біля popupInstall): якщо викликати цей крок РАНІШЕ за
        //     цикл "@post:", підтверджений рядок
        //     "ifs_missionselect @post:option_buttons.setting posx=-100;posy=-15"
        //     виконується ПІСЛЯ і щоразу ЗАТИРАЄ щойно записане зондом
        //     значення назад на -15, у тій самій функції, до того як гравець
        //     побачить хоч один кадр. Зонд виконується ОСТАННІМ — після
        //     "@post:" і "@postscreen" — тож саме він, а не навпаки, пише
        //     фінальне значення posY.
        // EN: The "widescreen" DIAGNOSTIC PROBE — the DISPLAY step.
        //     DELIBERATELY last before Return, not right after the wrapper
        //     install (above, near popupInstall): calling this step BEFORE
        //     the "@post:" loop lets the confirmed row
        //     "ifs_missionselect @post:option_buttons.setting posx=-100;posy=-15"
        //     run AFTER it and clobber the probe's freshly-written value
        //     back to -15 every time, within the same function call, before
        //     the player ever sees a single frame. The probe runs LAST —
        //     after "@post:" and "@postscreen" — so it, not the other way
        //     around, writes the final posY.
        // ---------------------------------------------------------------------
        if (includeScreenInfoProbe)
        {
            b.Emit(LuaOpcode.Eq, a: 0, b: 1, c: Lua50FunctionBuilder.Rk(k.Str(ScreenInfoProbeScreen)));
            var notProbeScreen = b.EmitJumpPlaceholder();

            var probeDisplay = b.AddNestedPrototype(
                BuildScreenInfoProbeDisplayStep($"{path}/ScreenInfoProbeDisplay"));
            b.EmitABx(LuaOpcode.Closure, a: 2, bx: probeDisplay);
            b.Emit(LuaOpcode.Move, a: 3, b: 0);
            b.Emit(LuaOpcode.Call, a: 2, b: 2, c: 1);

            b.PatchJump(notProbeScreen, b.NextPc);
        }

        b.Emit(LuaOpcode.Return, a: 0, b: 1);

        return b.Build(path: path);
    }

    // -------------------------------------------------------------------------
    // UA: Кореневий скрипт-інсталятор — вставляється у тіло точки входу через
    //     ShellEntryPointPatcher.ApplySplicedInstallerPatch.
    // EN: Root installer spliced into the entry point.
    // -------------------------------------------------------------------------
    // UA: includeScreenInfoProbe — див. коментар "ДІАГНОСТИЧНИЙ ЗОНД" перед
    //     BuildDispatchWrapper. За замовчуванням false: production-виклики
    //     (GenerateAnchorFixShellCommand -> shell_layout.lvl) не змінюються.
    //     Проба вмикається лише окремою діагностичною командою.
    // EN: includeScreenInfoProbe — see the "DIAGNOSTIC PROBE" comment before
    //     BuildDispatchWrapper. Defaults to false: production callers
    //     (GenerateAnchorFixShellCommand -> shell_layout.lvl) are unchanged.
    //     The probe is enabled only by a separate diagnostic command.
    // UA: includeBackgroundSizeFix — див. коментар "фікс оверскан фону"
    //     перед BuildDispatchWrapper. Параметр за замовчуванням false;
    //     production-виклик (GenerateAnchorFixShellCommand ->
    //     shell_layout.lvl) передає `true` явно — підтверджено знімками на
    //     4 з 7 значень bg_texture.
    //     Окрема діагностична команда (shell_bgfix.lvl) лишається для
    //     точкового тестування нових екранів/текстур без перезбирання
    //     основного патча.
    // EN: includeBackgroundSizeFix — see the "background overscan fix"
    //     comment before BuildDispatchWrapper. The parameter defaults to
    //     false; the production call
    //     (GenerateAnchorFixShellCommand -> shell_layout.lvl) passes `true`
    //     explicitly — confirmed by screenshots on 4 of 7 bg_texture values. The separate diagnostic
    //     command (shell_bgfix.lvl) remains for spot-testing new
    //     screens/textures without rebuilding the main patch.
    public static LuaFunctionPrototype BuildInstallerScript(float scale = 1.0f,
        bool includeScreenInfoProbe = false, bool includeBackgroundSizeFix = false)
    {
        var root = new Lua50FunctionBuilder { NumParams = 0, IsVararg = 0, MaxStackSize = 4 };

        var kName = root.AddStringConstant("AddIFScreen");
        var kBackup = root.AddStringConstant(BackupName);

        root.EmitABx(LuaOpcode.GetGlobal, a: 0, bx: kName);
        root.EmitTest(register: 0, c: 0);
        var skip = root.EmitJumpPlaceholder();

        root.EmitABx(LuaOpcode.SetGlobal, a: 0, bx: kBackup);
        var nested = root.AddNestedPrototype(BuildDispatchWrapper(
            scale, "root/layoutfix", includeScreenInfoProbe, includeBackgroundSizeFix));
        root.EmitABx(LuaOpcode.Closure, a: 0, bx: nested);
        root.EmitABx(LuaOpcode.SetGlobal, a: 0, bx: kName);

        root.PatchJump(skip, root.NextPc);
        root.Emit(LuaOpcode.Return, a: 0, b: 1);
        return root.Build();
    }
}
