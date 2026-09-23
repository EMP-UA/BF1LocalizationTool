# BF2: розкладка екрана «Миттєвий бій» / BF2: Instant Action screen layout

## 1. Чому це окремий файл / Why this is a separate file

**UA:** `ifs_missionselect` (одиночна гра) і `ifs_missionselect_pcMulti`
(мультиплеер) — в грі обидва варіанти підписані «Миттєвий бій», а не
«місія»: чотири списки
(карта, режим, епоха, тип гри), набір чекбоксів, кнопки керування
списком, нижня інформаційна панель. Загальне правило успадкування
прив'язки (`BF2_UI_LAYOUT_FIX.md`) на ньому не сходиться до нуля дефектів
— забагато взаємопов'язаних елементів для однієї спільної прив'язки.
Значення нижче виміряні вручну для кожного елемента й підтверджені
знімками гри. Обидва варіанти екрана (одиночна гра й мультиплеер)
використовують ІДЕНТИЧНИЙ набір виправлень позицій і розмірів — рядки
таблиці дослівно повторюються для обох імен екрана — за одним винятком:
робоча зона списків справді росте лише в мультиплеерному варіанті (див.
нижче).

**EN:** `ifs_missionselect` (single-player) and `ifs_missionselect_pcMulti`
(multiplayer) — in-game both variants are labeled "Instant Action", not
"mission": the most complex menu screen: four
lists (map, mode, era, game type), a set of checkboxes, list-control
buttons, a bottom info panel. The general anchor-inheritance rule
(`BF2_UI_LAYOUT_FIX.md`) doesn't converge to zero defects here — too many
interdependent elements for one shared anchor. The values below were
measured by hand for each element and confirmed by in-game screenshots.
Both variants of the screen (single-player and multiplayer) use an
IDENTICAL set of position and size fixes — the table rows repeat
verbatim for both screen names — with one exception: the lists' working
area only actually grows in the multiplayer variant (see below).

## 2. Чотири списки (карта, режим, епоха, тип гри) / The four lists (map, mode, era, game type)

**UA:** Кожен список має власну підкладку заголовка (`titleBarElement`) і власне
поле-«вікно» самого списку. Обидва виправляються окремими рядками:
підкладка — до побудови екрана (звичайний рядок), а позиція й розмір
вікна списку — ПІСЛЯ побудови (`@post:`), бо `gButtonWindow_fnSetSize`
(поле `winsize`) споживає розмір під час створення попапа, задовго до
точки патчу.

| Список | Підкладка заголовка (до побудови) | Позиція заголовка (`@post:`) | Вікно списку (`@post:`) |
|---|---|---|---|
| `MapListbox` | `bgexpandy=20;bgoffsety=8;bgoffsetx=0;bg_width=295.5` | `posy=-315;posx=-102.0` | `posx=570;posy=511;winsize=414;winsize2=644` |
| `ModeListbox` | `bgexpandy=20;bgoffsety=8;bgoffsetx=0;bg_width=347.2` | `posy=-110;posx=-117.5` | `posx=1026;winsize=482;winsize2=230;posy=305` |
| `EraListbox` | `bgexpandy=20;bgoffsety=8;bgoffsetx=0;bg_width=347.2` | `posy=-187;posx=-117.5` | `posx=1026;winsize=482;winsize2=388;posy=638` |
| `PlayListbox` | `bgexpandy=20;bgoffsety=8;bgoffsetx=0;bg_width=377.6` | `posy=-315;posx=-127.0` | `posx=1534;posy=511;winsize=518;winsize2=644` |

(Значення в авторському масштабі 800×600; при збірці інструмент сам
масштабує їх під цільову роздільність — механізм описано в
`BF2_UI_LAYOUT_FIX.md`.)

**EN:** Each list has its own title backdrop (`titleBarElement`) and its own
list "window" field. Both are fixed with separate rows: the backdrop
before the screen builds (an ordinary row), and the list window's
position and size AFTER the build (`@post:`), because
`gButtonWindow_fnSetSize` (the `winsize` field) consumes the size while
the popup is being created, long before the patch point.

| List | Title backdrop (pre-build) | Title position (`@post:`) | List window (`@post:`) |
|---|---|---|---|
| `MapListbox` | `bgexpandy=20;bgoffsety=8;bgoffsetx=0;bg_width=295.5` | `posy=-315;posx=-102.0` | `posx=570;posy=511;winsize=414;winsize2=644` |
| `ModeListbox` | `bgexpandy=20;bgoffsety=8;bgoffsetx=0;bg_width=347.2` | `posy=-110;posx=-117.5` | `posx=1026;winsize=482;winsize2=230;posy=305` |
| `EraListbox` | `bgexpandy=20;bgoffsety=8;bgoffsetx=0;bg_width=347.2` | `posy=-187;posx=-117.5` | `posx=1026;winsize=482;winsize2=388;posy=638` |
| `PlayListbox` | `bgexpandy=20;bgoffsety=8;bgoffsetx=0;bg_width=377.6` | `posy=-315;posx=-127.0` | `posx=1534;posy=511;winsize=518;winsize2=644` |

(Values are in the authored 800x600 scale; the tool scales them to the
target resolution itself at build time — the mechanism is described in
`BF2_UI_LAYOUT_FIX.md`.)

## 3. Кнопки керування списком карт (`map_buttons`) / The map list's control buttons (`map_buttons`)

**UA:** Сама панель кнопок позиціонується звичайним рядком
`@post:map_buttons posx=1624;posy=861`. Кожна кнопка всередині панелі має
власний виміряний горизонтальний зсув (`@post:`, лише `posx` — вертикаль
успадковується від батьківської панелі):

| Кнопка | `posx` |
|---|---|
| `select_btn` («Вибрати все») | `-1051.0` |
| `add_btn` («Додати») | `-595.5` |
| `launch_btn` («Запуск») | `-595.5` |
| `remove_btn` («Видалити») | `-86.0` |
| `remove_all_btn` | `-87.0` |
| `up_btn` | `-6.0` |
| `down_btn` | `14.0` |

**EN:** The button panel itself is positioned by an ordinary row
`@post:map_buttons posx=1624;posy=861`. Each button inside the panel has
its own measured horizontal offset (`@post:`, only `posx` — the vertical
is inherited from the parent panel):

| Button | `posx` |
|---|---|
| `select_btn` ("Select all") | `-1051.0` |
| `add_btn` ("Add") | `-595.5` |
| `launch_btn` ("Launch") | `-595.5` |
| `remove_btn` ("Remove") | `-86.0` |
| `remove_all_btn` | `-87.0` |
| `up_btn` | `-6.0` |
| `down_btn` | `14.0` |

## 4. Чекбокси ери та режиму гри / Era and game-mode checkboxes

**UA:** Три чекбокси-перемикачі виправлено окремими рядками `@post:`:

```
Era_C_box       posx=793;posy=486
Era_G_box       posx=793;posy=500
mode_checkbox   posx=793;posy=204
```

**EN:** Three toggle checkboxes are fixed with separate `@post:` rows:

```
Era_C_box       posx=793;posy=486
Era_G_box       posx=793;posy=500
mode_checkbox   posx=793;posy=204
```

## 5. Нижня інформаційна панель (`InfoboxBot`) / The bottom info panel (`InfoboxBot`)

**UA:** Опис карти/режиму внизу екрана відцентровано на середню колонку макета
(а не на весь екран): `@post:InfoboxBot posx=731.5;posy=960`.

**EN:** The map/mode description at the bottom of the screen is centered on the
layout's middle column (not on the whole screen):
`@post:InfoboxBot posx=731.5;posy=960`.

## 6. Вкладки та перекриття поля налаштувань / Tabs and the settings-field overlap

**UA:** Обидва варіанти екрана мають вкладки (`_Tabs`, `_Tabs1`) — застосовується
спільний для всього меню зсув `y=31` (`BF2_UI_LAYOUT_FIX.md`, розділ
«Вкладки»). Поле `option_buttons.setting`, яке вкладки затуляли окремо
від загальної шапки, виправлено рядком `@post:option_buttons.setting
posx=-100;posy=-15` — той самий рядок застосовано й до `ifs_instant_options`.

**EN:** Both screen variants have tabs (`_Tabs`, `_Tabs1`) — the shared
menu-wide shift `y=31` applies (`BF2_UI_LAYOUT_FIX.md`, "Tabs" section).
The `option_buttons.setting` field, covered by the tabs separately from
the general header, is fixed with `@post:option_buttons.setting
posx=-100;posy=-15` — the same row is also applied to
`ifs_instant_options`.

## 7. Робоча зона списків: чому вона росте лише в мультиплеері / The lists' working area: why it only grows in multiplayer

**UA:** Робоча зона списків «Карта»/«Список» — 22 видимі рядки заввишки 146px —
не росте автоматично разом зі збільшеною через `winsize` рамкою: до
моменту, коли керування взагалі доходить до `AddIFScreen` (єдиної точки
входу патча), списки вже повністю побудовані функцією
`ListManager_fnInitList`, яка читає `showcount`/`width` ОДИН РАЗ і одразу
створює рівно стільки віджетів-рядків. Ні звичайний рядок (до
`AddIFScreen`), ні `@post:` (після) не встигають.

Рішення — обгортка навколо самої `ListManager_fnInitList` (глобальна
функція, існує задовго до побудови будь-якого екрана): при кожному
виклику вона звіряє другий аргумент (`layout`) з названою глобальною
таблицею-дескриптором і, якщо збіглося, дописує в неї потрібні поля
ПЕРЕД тим, як оригінал їх прочитає. Формат рядка: `@initlist:
<ГлобальнаТаблиця-дескриптор>`. Значення не масштабуються — рушій
трактує їх як фінальні пікселі.

Задіяно лише для `ifs_missionselect_pcMulti`:

```
@initlist:ifs_mspc_MapList_layout   showcount=38;width=386;yTop=-262
@initlist:ifs_mspc_PlayList_layout  showcount=38;width=490;yTop=-262
```

(`ifs_missionselect`, одиночна гра, використовує ті самі списки без цього
виправлення — робоча зона там лишається авторського розміру.)

**EN:** The "Map"/"List" lists' working area — 22 visible rows, 146px tall —
doesn't grow automatically along with the frame enlarged via `winsize`:
by the time control even reaches `AddIFScreen` (the patch's only entry
point), the lists are already fully built by `ListManager_fnInitList`,
which reads `showcount`/`width` ONCE and immediately creates exactly
that many row widgets. Neither an ordinary row (before `AddIFScreen`) nor
`@post:` (after) is early enough.

The fix is a wrapper around `ListManager_fnInitList` itself (a global
function that exists long before any screen is built): on every call it
compares the second argument (`layout`) against a named global
descriptor table and, on a match, writes the requested fields into it
BEFORE the original reads them. Row format: `@initlist:
<GlobalDescriptorTable>`. Values are not scaled — the engine treats them
as final pixels.

Used only for `ifs_missionselect_pcMulti`:

```
@initlist:ifs_mspc_MapList_layout   showcount=38;width=386;yTop=-262
@initlist:ifs_mspc_PlayList_layout  showcount=38;width=490;yTop=-262
```

(`ifs_missionselect`, single-player, uses the same lists without this
fix — its working area stays at the authored size.)

## 8. Незавершене калібрування розміру відео (лише `ifs_missionselect`) / Unfinished movie-size calibration (`ifs_missionselect` only)

**UA:** Окремо в таблиці є один рядок з псевдошляхом `@postscreen` —
ПІСЛЯ-будовний запис НАПРЯМУ в корінь таблиці екрана (потрібен саме тут,
бо конструктор екрана переписує `movieW`/`movieH` БЕЗУМОВНИМИ літералами
одразу після виклику `AddIFScreen`, тож ані звичайний рядок, ані
`@post:` не встигають): `@postscreen movieW=125;movieH=125`. Це
калібрувальна проба, а не підтверджений фікс — значення ще не звірене
знімком гри.

**EN:** The table separately carries one row with the `@postscreen` pseudo-path
— an AFTER-BUILD write straight onto the screen table's root (needed
specifically here because the screen's own constructor overwrites
`movieW`/`movieH` with UNCONDITIONAL literals right after the
`AddIFScreen` call, so neither an ordinary row nor `@post:` is early
enough): `@postscreen movieW=125;movieH=125`. This is a calibration
probe, not a confirmed fix — the value hasn't been checked against an
in-game screenshot yet.
