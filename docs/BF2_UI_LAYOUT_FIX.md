# BF2: верстка меню під ширші роздільності / BF2: menu layout for wider resolutions

## 1. Причина / Reason

**UA:** Battlefront II позиціонує кожен елемент меню (`shell.lvl`) під фіксовану
роздільність 800×600 (4:3): прив'язку до краю екрана, зміщення в пікселях,
розмір підкладки. На ширших екранах (16:9, 16:10) частина елементів
накладається одна на одну, розходиться в різні боки чи виходить за межі
власної панелі, а фонове зображення виявляється обрізаним. Нижче — усі
класи цього дефекту, кожен зі своєю точною формулою чи набором виміряних
значень. Бойовий HUD (`ingame.lvl`) — окремий розділ наприкінці файлу;
розкладка екрана «Миттєвий бій» (`ifs_missionselect` /
`ifs_missionselect_pcMulti`) — окремий файл, `BF2_MISSIONSELECT_LAYOUT.md`
(це найбільший за обсягом окремий екран, з власними механізмами).

**EN:** Battlefront II positions every menu element (`shell.lvl`) for a fixed
800×600 (4:3) resolution: its screen-edge anchor, its pixel offset, its
backdrop size. At wider resolutions (16:9, 16:10) some elements overlap
each other, drift apart, or run outside their own panel, and the
background image ends up clipped. Below are all classes of this defect,
each with its own exact formula or measured value set. The combat HUD
(`ingame.lvl`) is a separate section at the end of this file; the layout
of the Instant Action screen (`ifs_missionselect` /
`ifs_missionselect_pcMulti`) is a separate file,
`BF2_MISSIONSELECT_LAYOUT.md` (by far the largest single screen, with its
own mechanisms).

**UA:** На відміну від BF1, де підгонка тексту під ширину поля вирішується
посимвольно (розмір гліфа, кернінг, підбір шрифту), у BF2 додана довжина
перекладу найчастіше впирається не в сам текст, а в прив'язані елементи
інтерфейсу — підкладку заголовка, сусідню кнопку, контейнер списку. Тому
переважна більшість виправлень нижче працює на рівні розкладки (позиція
й розмір віджета), а не на рівні гліфів.

**EN:** Unlike BF1, where fitting text to a field's width is resolved per
character (glyph size, kerning, font selection), in BF2 the added length
of a translation most often runs into bound interface elements — a
title backdrop, a neighboring button, a list container — rather than the
text itself. So most of the fixes below operate at the layout level (a
widget's position and size), not at the glyph level.

## 2. Прив'язка виправлення до конкретної роздільності / Tied to one specific resolution

**UA:** Сам механізм застосування виправлень — прив'язки-частки екрана
(`ScreenRelativeX`/`Y`), множник `роздільність_гравця / 800×600` для
полів, що масштабуються, і виправлення обрізаного фону через власне
значення `widescreen`, яке рушій рахує динамічно з РЕАЛЬНОЇ поточної
роздільності (`ScriptCB_GetScreenInfo()`) — не прив'язаний до жодного
конкретного числа й теоретично працює для будь-якої цільової
роздільності. Але конкретні значення в поточній збірці — виміряні
вручну пікселі для екранів, яких не закрило автоматичне правило
групування прив'язок (`ifs_missionselect`, `ifs_mp_sessionlist` та
інші, розділ нижче), а також збільшений розмір шрифту
(`BF2_FONT_SCALING.md` — фіксовані пікселі, не формула) — виміряні й
підтверджені знімками гри саме на 1920×1080. Для іншої цільової
роздільності ці конкретні значення довелося б виміряти заново; сам
механізм перебудовувати не потрібно.

**EN:** The mechanism itself — fractional screen anchors
(`ScreenRelativeX`/`Y`), the `player_resolution / 800x600` multiplier for
scalable fields, and the clipped-background fix that reads the engine's
own dynamically computed `widescreen` value from the REAL current
resolution (`ScriptCB_GetScreenInfo()`) — isn't tied to any one number
and works for any target resolution in principle. But the actual values
shipped in the current build — the hand-measured pixel corrections for
screens the automatic anchor-grouping rule couldn't clear
(`ifs_missionselect`, `ifs_mp_sessionlist` and others, below), and the
enlarged font size (`BF2_FONT_SCALING.md` — a fixed pixel value, not a
formula) — were measured and confirmed by in-game screenshot
specifically at 1920x1080. A different target resolution would need
those specific values re-measured; the mechanism itself would not need
rebuilding.

## 3. Механізм застосування виправлень / How the fix is applied

**UA:** Патч — обгортка навколо виклику `AddIFScreen(таблиця, ім'я)`, який гра
викликає для кожного екрана меню. Обгортка викликається на кожному з
30 реальних місць виклику `AddIFScreen` у `shell.lvl`, перевіряє ім'я
екрана й, за збігом, дописує виправлення в таблицю-аргумент ПЕРЕД тим, як
передати керування оригіналу — кожен вузол шляху до віджета перевіряється
на існування, тож інший білд гри чи інший мод просто не зачіпається.

Джерело значень — вбудований текстовий ресурс `Data/Bf2LayoutTable.txt`
(формат: `екран<TAB>шлях.через.крапку<TAB>поле=число;поле=число`). Поля
записуються в АВТОРСЬКОМУ масштабі 800×600; при збірці інструмент сам
множить кожне числове поле на коефіцієнт `роздільність_гравця / 800×600`
— окрім полів, які масштабуванню не підлягають за змістом: прив'язка
(`ScreenRelativeX`/`ScreenRelativeY`, уже частка екрана, а не піксель),
порядок малювання (`ZPos`) і кольорові канали (`alpha`, `ColorR/G/B`).
Приклад останнього: `ifs_mp_sessionlist` → `listbox.titleBarElement` →
`alpha=1.0` — без цього винятку рушій отримав би `alpha=2.4` на 1920×1080
замість `1.0`.

Значення поля може бути рядком у лапках (напр. `halign="hcenter"`) — лише
у звичайному рядку до побудови екрана; решта механізмів запису приймають
тільки числа. Сегмент шляху виду `#N` (N — ціле число, напр.
`playlistorder.#1.radiotext`) — числовий ключ таблиці Lua (`t[N]`), а не
рядковий (`t["N"]`); потрібен для елементів, які рушій зберігає за
числовим індексом, а не рядковим тегом (наприклад, варіанти в групі
радіокнопок).

Більшість рядків пишуться ДО виклику оригінального `AddIFScreen` (поля ще
лежать у таблиці-конфігу й не встигли бути прочитаними рушієм). Частина
значень рушій, однак, читає до того, як патч встигає їх записати, або
перечитує на КОЖНОМУ повторному виклику, а не один раз при побудові
екрана — для них є три додаткові механізми запису, описані в розділах
нижче: `@post:` (одноразовий запис ПІСЛЯ побудови, через дескриптор
об'єкта рушія), `@initlist:` (перехоплення глобальної функції побудови
списків, до того, як екран узагалі побудовано) і постійна обгортка
`Popup_Tutorial.SetPage` (перезастосовується на кожен виклик). Четвертий
механізм, `@hook:` (постійна обгортка довільного рантайм-сеттера
екрана), реалізований і протестований, але наразі жоден рядок таблиці
його не використовує — задача, під яку його спроєктовано (перекриття
вкладками поля `option_buttons.setting`), закрита простішим одноразовим
`@post:` (розділ «Вкладки» нижче).

**EN:** The patch is a wrapper around `AddIFScreen(table, name)`, which the game
calls for every menu screen. The wrapper runs at all 30 real
`AddIFScreen` call sites in `shell.lvl`, checks the screen name and, on a
match, appends corrections to the table argument BEFORE handing off to
the original — every path node to a widget is existence-checked, so a
different game build or another mod is simply left untouched.

The values live in an embedded text resource, `Data/Bf2LayoutTable.txt`
(format: `screen<TAB>dot.separated.path<TAB>field=number;field=number`).
Fields are written in the AUTHORED 800×600 scale; at build time the tool
multiplies every numeric field by `player_resolution / 800x600` on its
own — except fields that aren't meant to scale: the anchor
(`ScreenRelativeX`/`ScreenRelativeY`, already a screen fraction, not a
pixel), draw order (`ZPos`), and color channels (`alpha`, `ColorR/G/B`).
Example of the latter: `ifs_mp_sessionlist` -> `listbox.titleBarElement`
-> `alpha=1.0` — without this exception the engine would get `alpha=2.4`
at 1920x1080 instead of `1.0`.

A field's value may be a quoted string (e.g. `halign="hcenter"`) — only in
an ordinary pre-build row; the other write mechanisms accept numbers
only. A path segment of the form `#N` (N an integer, e.g.
`playlistorder.#1.radiotext`) is a numeric Lua table key (`t[N]`), not a
string one (`t["N"]`); needed for elements the engine stores under a
numeric index rather than a string tag (e.g. the options in a
radio-button group).

Most rows are written BEFORE the original `AddIFScreen` call (the fields
still sit in the config table and haven't been read by the engine yet).
Some values, however, are read by the engine before the patch can write
them, or are re-read on EVERY call rather than once at screen build time
— three further write mechanisms exist for these, covered in the
sections below: `@post:` (a one-time write AFTER the build, through the
engine's own object handle), `@initlist:` (intercepting the global
list-building function before the screen is even built), and a permanent
wrapper around `Popup_Tutorial.SetPage` (re-applied on every call). A
fourth mechanism, `@hook:` (a permanent wrapper around an arbitrary
runtime setter of the screen), is implemented and tested but no current
table row uses it — the problem it was designed for (tabs covering the
`option_buttons.setting` field) is closed by a simpler one-time `@post:`
instead (see "Tabs" below).

## 4. Неоднакові прив'язки в межах однієї групи / Mismatched anchors within one group

**UA:**

**Оригінал.** Прив'язка (`ScreenRelativeX`/`ScreenRelativeY`) — частка
ширини/висоти екрана, до якої позиціонується елемент (наприклад `0.5/0` —
по центру зверху). Віджети однієї візуальної групи (підпис і поле під
ним, кілька елементів однієї панелі) в оригіналі можуть мати РІЗНІ
прив'язки. На 800×600 різниця непомітна — прив'язки збігаються в межах
точності пікселя. На широкому екрані різні прив'язки розводять елементи
групи в різні боки.

**Правило, за яким прив'язка виводиться для нової групи** (застосовується
однаково до будь-якого екрана — жодне число в цьому правилі не підібране
вручну під конкретний випадок):

1. У групу об'єднуються лише елементи, між якими виміряно реальне
   розходження на різних роздільностях; сусідніми вважаються елементи в
   межах 100px одне від одного на авторському екрані (перевірено пороги
   200/100/60/40px: 200px об'єднував явно непов'язані елементи, нижче
   60px частина реальних груп переставала розпізнаватись).
2. Береться центроїд авторської позиції групи на 800×600, виражений як
   частка екрана.
3. Із прив'язок членів групи обирається та, що найближча до цієї частки.
4. Усім членам групи присвоюється ця прив'язка; зміщення (`x`/`y`)
   кожного перераховується так, щоб їхнє взаємне розташування на
   800×600 збереглося піксель-у-піксель.

Перерахунок ітеративний, до нерухомої точки (не більш як 12 ітерацій).
Фон екрана (прив'язка без заданого розміру, рушій розтягує його сам)
з правила виключено; елемент, чия авторська позиція лежить поза кадром
800×600, виключається з групування.

Виправлені групи (поточний стан таблиці; прив'язку кожного елемента
приведено до єдиного значення):

| Екран | Віджети | Нова прив'язка | Зміщення (`x;y`) |
|---|---|---|---|
| `ifs_login` | `NewBox`, `diff_button`, `diff_listbox`, `listbox`, `profile_button`, `select_diff`, `select_profile` | `0.5/0.5` | своє для кожного (напр. `select_profile`: `x=-140;y=-110`) |
| `ifs_opt_sound` | `buttonlabels`, `buttons`, `radiobuttons`, `sliders` | `ScreenRelativeY=0.23` | `y=0` |
| `ifs_opt_sound` | `res_dropdown_btn`, `modelist_listbox`, `mixer_dropdown_btn`, `mixerlist_listbox` | `0.533/0.235` | — |
| `ifs_opt_pcvideo` | `formcontainergeneral`, `formcontainercustom` | `ScreenRelativeY=0.28` | `y=0` |
| `ifs_freeform_result` | `enemy_result` | `0.5/0.9` | `x=0;y=-120` |

**Фільтр приймання.** У фінальну збірку потрапляє лише той екран, де
виправлення доведено зводить дефекти до нуля; часткове поліпшення (менше
дефектів, але не нуль, або нуль ціною нових дефектів деінде) відхиляється
цілком — екран лишається побайтово ванільним. Екрани, що не пройшли
фільтр цим саме автоматичним правилом (зокрема `ifs_missionselect` і
`ifs_mp_sessionlist` — обидва мають забагато взаємозалежних елементів
для єдиної групової прив'язки), не лишились без виправлення: для них
значення виміряні вручну й застосовані через прямий запис пікселів
(розділи нижче й окремий файл для `ifs_missionselect`).

**EN:**

**Original.** An anchor (`ScreenRelativeX`/`ScreenRelativeY`) is a
fraction of screen width/height an element is positioned against (e.g.
`0.5/0` is top center). Widgets in one visual group (a label and the
field below it, several elements of one panel) can originally carry
different anchors. At 800×600 the difference is invisible — the anchors
agree to within a pixel. At a wider resolution, mismatched anchors pull
the group's elements apart.

**The rule that derives a new group's anchor** (applies identically to
any screen — no number in this rule is hand-picked for a specific case):

1. Only elements with a measured, real mismatch across resolutions are
   grouped together; elements within 100px of each other on the
   authored screen count as neighbors (thresholds of 200/100/60/40px
   were tested: 200px merged clearly unrelated elements, below 60px some
   real groups stopped being detected).
2. The group's authored 800×600 centroid is taken and expressed as a
   fraction of the screen.
3. Among the group members' anchors, the one closest to that fraction is
   chosen.
4. Every member gets that anchor; each one's offset (`x`/`y`) is
   recalculated so their mutual 800×600 positioning is preserved
   pixel-for-pixel.

The recalculation iterates to a fixed point (12 steps at most). The
screen background (an anchor with no set size, which the engine
stretches on its own) is excluded from the rule; an element whose
authored position lies outside the 800×600 frame is excluded from
grouping.

Fixed groups (current table state; every member's anchor unified to one
value):

| Screen | Widgets | New anchor | Offset (`x;y`) |
|---|---|---|---|
| `ifs_login` | `NewBox`, `diff_button`, `diff_listbox`, `listbox`, `profile_button`, `select_diff`, `select_profile` | `0.5/0.5` | own per widget (e.g. `select_profile`: `x=-140;y=-110`) |
| `ifs_opt_sound` | `buttonlabels`, `buttons`, `radiobuttons`, `sliders` | `ScreenRelativeY=0.23` | `y=0` |
| `ifs_opt_sound` | `res_dropdown_btn`, `modelist_listbox`, `mixer_dropdown_btn`, `mixerlist_listbox` | `0.533/0.235` | — |
| `ifs_opt_pcvideo` | `formcontainergeneral`, `formcontainercustom` | `ScreenRelativeY=0.28` | `y=0` |
| `ifs_freeform_result` | `enemy_result` | `0.5/0.9` | `x=0;y=-120` |

**Acceptance filter.** Only a screen where the fix is proven to reduce
defects to zero ships in the final build; a partial improvement (fewer
defects but not zero, or zero at the cost of new defects elsewhere) is
rejected outright — the screen stays byte-identical to vanilla. Screens
that this specific automated rule couldn't clear (notably
`ifs_missionselect` and `ifs_mp_sessionlist` — both have too many
interdependent elements for one shared group anchor) were not left
unfixed: their values were measured by hand and applied as direct pixel
writes instead (sections below, and a dedicated file for
`ifs_missionselect`).

## 5. Позиція й розмір, виміряні вручну (без зміни прив'язки) / Hand-measured position and size (no anchor change)

**UA:** Клас дефектів, де сама прив'язка коректна, а потребують корекції
конкретні пікселі — зсув, ширина тексту чи підкладки, що не зростає разом
зі збільшеним кириличним шрифтом (див. `BF2_FONT_SCALING.md`). Тут немає
єдиної формули: кожен елемент має власні виміряні значення, підтверджені
знімком гри до і після.

| Екран | Віджет(и) | Поля (значення в масштабі 800×600) |
|---|---|---|
| `ifs_login` | `ProfileBox.titleBarElement` | `bgexpandy=3;bgoffsety=1.5;bgoffsetx=-8;x=-200.5` (підкладка центрована відносно рамки вікна; формула — розділ нижче) |
| `ifs_login` | `profile_button` | `x=-2.5` (список профілю центровано відносно тієї ж рамки) |
| `ifs_opt_pccontrols` | `formcontainer` | `y=91` |
| `ifs_opt_pccontrols` | `bindTitle` | `y=235` |
| `ifs_opt_pccontrols` | `buttonlabels`, `buttons`, `sliders`, `radiobuttons`, `Info`, `RInfo` | `y=20` |
| `ifs_mp_sessionlist` | `listbox.titleBarElement` | `bgexpandy=10;bgoffsety=88;alpha=1.0;bgoffsetx=-5` |
| `ifs_mp_sessionlist` | `serverinfo.titleBarElement`, `playerlist.titleBarElement` | `bgexpandy=10;bgoffsetx=-10;bgoffsety=1.5;x=-449.5`/`x=-439.5` |
| `ifs_freeform_load`, `ifs_campaign_load` | `listbox.titleBarElement` | `bgoffsetx=-9;bgoffsety=1.5;x=-777.6` |

Повний перелік виміряних параметрів по кожному екрану й елементу
зберігається у вбудованому файлі даних (`Bf2LayoutTable.txt`) поруч із
кодом генератора — це згенеровані числові дані, а не текст документації,
тому вони не дублюються тут повністю. Для нового екрана значення
визначаються тим самим способом: вимір фактичного розміру на цільовому
шрифті/роздільності, підбір поправки, підтвердження знімком гри до і
після.

**EN:** A defect class where the anchor itself is correct but specific pixel
values need correcting — an offset, a text or backdrop width that
doesn't grow along with the enlarged Cyrillic font (see
`BF2_FONT_SCALING.md`). There's no single formula here: each element has
its own measured values, confirmed by an in-game screenshot before and
after.

| Screen | Widget(s) | Fields (values in the 800×600 scale) |
|---|---|---|
| `ifs_login` | `ProfileBox.titleBarElement` | `bgexpandy=3;bgoffsety=1.5;bgoffsetx=-8;x=-200.5` (the backdrop is centered on the window frame; formula in the section below) |
| `ifs_login` | `profile_button` | `x=-2.5` (the profile list is centered on the same frame) |
| `ifs_opt_pccontrols` | `formcontainer` | `y=91` |
| `ifs_opt_pccontrols` | `bindTitle` | `y=235` |
| `ifs_opt_pccontrols` | `buttonlabels`, `buttons`, `sliders`, `radiobuttons`, `Info`, `RInfo` | `y=20` |
| `ifs_mp_sessionlist` | `listbox.titleBarElement` | `bgexpandy=10;bgoffsety=88;alpha=1.0;bgoffsetx=-5` |
| `ifs_mp_sessionlist` | `serverinfo.titleBarElement`, `playerlist.titleBarElement` | `bgexpandy=10;bgoffsetx=-10;bgoffsety=1.5;x=-449.5`/`x=-439.5` |
| `ifs_freeform_load`, `ifs_campaign_load` | `listbox.titleBarElement` | `bgoffsetx=-9;bgoffsety=1.5;x=-777.6` |

The full list of measured parameters per screen and element lives in the
embedded data file (`Bf2LayoutTable.txt`) next to the generator code —
that's generated numeric data, not documentation text, so it isn't fully
duplicated here. For a new screen, the values are derived the same way:
measure the actual size at the target font/resolution, work out the
correction, confirm with an in-game screenshot before and after.

## 6. Пропорції синіх підкладок після фіксу висоти шрифту / Blue backdrop proportions after the font-height fix

**UA:** `NewButtonWindow` (заголовок-«плашка» вгорі спливних вікон і списків)
рахує висоту текстового поля рушійною формулою `texth = висота_шрифту +
6`, а підкладку розширює на `bgexpandy` (ванільне значення — `3`, тобто
пів пікселя знизу й зверху від текстового поля на кожен бік). До фіксу
висоти шрифту (`BF2_FONT_SCALING.md`, поле `HEAD`) рушій читав ЗАСТАРІЛЕ
значення висоти — підкладка виходила нижчою за текст, і це компенсувалося
вручну підібраними великими `bgexpandy`/`bgoffsety` (напр. `20`/`8`,
`12`/`6`). Після фіксу `HEAD` рушій рахує `texth` від СПРАВЖНЬОЇ висоти
шрифту, і ванільна формула сама дає правильні пропорції — великі
компенсаційні значення стали зайвими й замінені на близькі до ванільних
(`bgexpandy=3`, `bgoffsety=1,5…3` залежно від елемента). Це стосується
підкладок заголовків Галактичного завоювання (розділ нижче), чотирьох
списків «Миттєвого бою» (`BF2_MISSIONSELECT_LAYOUT.md`) і підкладки
профілю (розділ вище).

**EN:** `NewButtonWindow` (the title "plate" atop popups and lists) computes
the text field's height with the engine's own formula `texth =
font_height + 6`, and expands the backdrop by `bgexpandy` (vanilla value
`3` — half a pixel above and below the text field on each side). Before
the font-height fix (`BF2_FONT_SCALING.md`, the `HEAD` field), the engine
read a STALE height value — the backdrop came out shorter than the text,
which was compensated with large, hand-picked `bgexpandy`/`bgoffsety`
values (e.g. `20`/`8`, `12`/`6`). After the `HEAD` fix, the engine
computes `texth` from the REAL font height, and the vanilla formula
produces correct proportions on its own — the large compensating values
became unnecessary and were replaced with values close to vanilla
(`bgexpandy=3`, `bgoffsety=1.5..3` depending on the element). This
applies to the Galactic Conquest title backdrops (next section), the four
Instant Action lists (`BF2_MISSIONSELECT_LAYOUT.md`), and the profile
backdrop (section above).

## 7. Спільна група екранів Галактичного завоювання / The shared Galactic Conquest screen group

**UA:** 13 екранів Галактичного завоювання й кампанії (`ifs_campaign_battle`,
`ifs_campaign_battle_card`, `ifs_campaign_summary`, `ifs_freeform_battle`,
`ifs_freeform_battle_card`, `ifs_freeform_battle_mode`,
`ifs_freeform_fleet`, `ifs_freeform_focus`, `ifs_freeform_main`,
`ifs_freeform_purchase_tech`, `ifs_freeform_purchase_unit`,
`ifs_freeform_result`, `ifs_freeform_summary`) будують свій нижній
інформаційний блок, заголовок, іконку гравця й ряд кнопок дій ОДНІЄЮ
спільною функцією (`ifs_freeform_AddCommonElements`) — тому одні й ті самі
виміряні значення виправляють усі 13 екранів одночасно, без повторного
виміру на кожному:

| Віджет | Поля |
|---|---|
| `info.skin` | `localpos_l=-836.8;localpos_r=836.8` (розширення інформаційної панелі) |
| `info.caption`, `info.subcaption`, `info.text` | `x=-820.6;textw=1641.6` |
| `title.text` | `bgexpandy=8;bgoffsetx=-12;bgoffsety=3` (підкладка заголовка; формула — розділ вище) |
| `player.icon` | `localpos_l=-13;localpos_r=13` |
| `player` | `y=116` |
| `action.misc.label`, `action.accept.label`, `action.back.label`, `action.help.label` | `textw=220;bg_width=220;x=-110` (ряд кнопок дій знизу) |

Окремо на `ifs_freeform_purchase_unit` є ще один рядок — запис у ГЛОБАЛЬНУ
змінну Lua, а не в поле віджета (псевдошлях `@globals`):
`ifs_purchase_tech_use_y=3.45`.

**EN:** 13 Galactic Conquest and campaign screens (`ifs_campaign_battle`,
`ifs_campaign_battle_card`, `ifs_campaign_summary`, `ifs_freeform_battle`,
`ifs_freeform_battle_card`, `ifs_freeform_battle_mode`,
`ifs_freeform_fleet`, `ifs_freeform_focus`, `ifs_freeform_main`,
`ifs_freeform_purchase_tech`, `ifs_freeform_purchase_unit`,
`ifs_freeform_result`, `ifs_freeform_summary`) build their bottom info
panel, title, player icon, and row of action buttons through ONE shared
function (`ifs_freeform_AddCommonElements`) — so the same measured
values fix all 13 screens at once, with no need to re-measure each one:

| Widget | Fields |
|---|---|
| `info.skin` | `localpos_l=-836.8;localpos_r=836.8` (widening the info panel) |
| `info.caption`, `info.subcaption`, `info.text` | `x=-820.6;textw=1641.6` |
| `title.text` | `bgexpandy=8;bgoffsetx=-12;bgoffsety=3` (title backdrop; formula in the section above) |
| `player.icon` | `localpos_l=-13;localpos_r=13` |
| `player` | `y=116` |
| `action.misc.label`, `action.accept.label`, `action.back.label`, `action.help.label` | `textw=220;bg_width=220;x=-110` (the bottom row of action buttons) |

`ifs_freeform_purchase_unit` alone carries one more row — a write to a
Lua GLOBAL variable rather than a widget field (the `@globals`
pseudo-path): `ifs_purchase_tech_use_y=3.45`.

## 8. Значення, які рушій уже спожив до точки патчу (`@post:`) / Values the engine already consumed before the patch point (`@post:`)

**UA:**

**Проблема.** Частина полів рушій читає ще ДО того, як патч встигає їх
записати — наприклад `btnw` споживається всередині
`RoundIFButtonLabel_fnSetSize`, а `use_y` — всередині `Build_Use`, обидва
до виклику `AddIFScreen`. Записати таке поле в таблицю-конфіг марно: воно
вже прочитане.

**Рішення.** Після повернення з оригінального `AddIFScreen` кожен віджет
уже має дескриптор об'єкта рушія в полі `cp` (підтверджено у VM: до
виклику — `nil`, після — реальне значення). Маючи `cp`, патч викликає
нативний сеттер рушія напряму на вже створеному об'єкті — питання «коли
рушій прочитав поле конфігу» зникає повністю. Формат рядка таблиці:
`@post:шлях.до.віджета`.

Дозволені операції (кожна підтверджена дизасемблюванням реального виклику
в скриптах гри, а не лише наявністю імені в списку експортів рушія):

| Поле в таблиці | Викликає | Аргументи |
|---|---|---|
| `leading` | нативний `ScriptCB_IFText_SetLeading(cp, value)` | 1 число |
| `textw` (+ `textw2`) | нативний `ScriptCB_IFText_SetTextBox(cp, w, h)` | 2 числа |
| `winsize` (+ `winsize2`) | Lua-метод `gButtonWindow_fnSetSize(self, w, h)` — розтягує саму підкладку вікна | 2 числа |
| `bordersize` (+ `bordersize2`) | Lua-метод `BorderRectSkin_fnSetSize(self, w, h)` — підкладка розтягується симетрично від центру, кути дев'ятизонної текстури лишаються цілими, доки сторона ≥ 32px | 2 числа |
| `posy` | нативний `IFObj_fnSetPos(self, x, y, z)`, `x`/`z` передаються як `nil` (нуль-толерантна функція — міняє лише `y`) | 1 число |
| `posx` | той самий `IFObj_fnSetPos`, змінює лише `x` | 1 число |

Приклад — `ifs_mp_sessionlist` (заголовок списку сесій висить над
контентом через розширений вертикально фон):

```
@post:serverinfo        posy=675
@post:playerlist         posy=675
@post:ResortButtons       posy=91
@post:LoginAsText1        posy=922
@post:LoginAsText2        posy=944
@post:listbox.skin        bordersize=1839;bordersize2=360;posy=78
```

Якщо перший сегмент шляху починається з `@`, коренем є ГЛОБАЛЬНА таблиця
Lua, а не таблиця екрана — так виправлено підпис у спливному вікні
довідки (наступний розділ): `@post:@Popup_Tutorial.title leading=5`.

**EN:**

**The problem.** Some fields are read by the engine BEFORE the patch can
write them — for example `btnw` is consumed inside
`RoundIFButtonLabel_fnSetSize`, and `use_y` inside `Build_Use`, both
before the `AddIFScreen` call. Writing such a field into the config table
is pointless — it's already been read.

**The fix.** Once the original `AddIFScreen` returns, every widget
already holds an engine object handle in its `cp` field (VM-verified: nil
before the call, a real value after). Given `cp`, the patch calls the
engine's native setter directly on the already-created object — the
question of "when did the engine read the config field" stops mattering
entirely. Row format: `@post:path.to.widget`.

Allowed operations (each confirmed by disassembling a real call in the
game's own scripts, not just by the name existing in the engine's export
list):

| Table field | Calls | Arguments |
|---|---|---|
| `leading` | native `ScriptCB_IFText_SetLeading(cp, value)` | 1 number |
| `textw` (+ `textw2`) | native `ScriptCB_IFText_SetTextBox(cp, w, h)` | 2 numbers |
| `winsize` (+ `winsize2`) | Lua method `gButtonWindow_fnSetSize(self, w, h)` — stretches the window's own backdrop | 2 numbers |
| `bordersize` (+ `bordersize2`) | Lua method `BorderRectSkin_fnSetSize(self, w, h)` — the backdrop stretches symmetrically about its center, the nine-slice corners stay intact as long as the side is >= 32px | 2 numbers |
| `posy` | native `IFObj_fnSetPos(self, x, y, z)`, `x`/`z` passed as `nil` (nil-tolerant — changes only `y`) | 1 number |
| `posx` | the same `IFObj_fnSetPos`, changes only `x` | 1 number |

Example — `ifs_mp_sessionlist` (the session-list header floats above its
content because the background was stretched vertically):

```
@post:serverinfo        posy=675
@post:playerlist         posy=675
@post:ResortButtons       posy=91
@post:LoginAsText1        posy=922
@post:LoginAsText2        posy=944
@post:listbox.skin        bordersize=1839;bordersize2=360;posy=78
```

If the path's first segment starts with `@`, the root is a Lua GLOBAL
table rather than the screen table — this is how the label in the help
popup was fixed (next section): `@post:@Popup_Tutorial.title leading=5`.

## 9. Вкладки затуляють ім'я профілю та версію гри / Tabs covering the profile name and game version

**UA:** На 17 екранах меню з вкладками (`ifs_careerstats`, `ifs_freeform_fleet`,
`ifs_freeform_pickscenario`, `ifs_instant_options`, `ifs_login`,
`ifs_missionselect`, `ifs_missionselect_pcMulti`, `ifs_mp_sessionlist`,
`ifs_mpgs_friends`, `ifs_mpgs_login`, `ifs_opt_general`, `ifs_opt_mp`,
`ifs_opt_pccontrols`, `ifs_opt_pcvideo`, `ifs_opt_sound`,
`ifs_sp_briefing`, `ifs_sp_campaign`, `ifs_sp_gc_main`) контейнер вкладок
(`_Tabs`/`_Tabs1`/`_Tabs2` — окремий об'єкт `NewIFContainer` із власним
`y`) на широкому екрані затуляє напис імені профілю й версії гри над
собою. Виправлення — одноманітний зсув усіх контейнерів вкладок вниз на
однакову величину, `_Tabs y=31` (і так само для `_Tabs1`/`_Tabs2`, де
вони є) — застосовано до ВСІХ екранів із вкладками одразу, а не лише
до тих, де текст імені профілю справді присутній: інакше висота шапки
відрізнялася б від екрана до екрана.

Окремо, ряд вкладок горизонтально ширший за розрахункову позицію: заокруглений
кінець крайньої правої вкладки малюється на кілька пікселів ширше за
ширину, яку рахує сама верстка вкладок (`ifelem_tabmanager`), тому
правий край ряду виходить за межі екрана й обрізається. Виправлення —
додатковий горизонтальний зсув контейнера вкладок ліворуч, тим самим
рядком `x` (напр. `ifs_login _Tabs x=-11.5`); величина залежить від
кількості вкладок у ряду — `-11.5` для рядів з 4 і 6 вкладками, `-13.65`
для ряду з одиночної гри.

**EN:** On 17 menu screens with tabs (`ifs_careerstats`, `ifs_freeform_fleet`,
`ifs_freeform_pickscenario`, `ifs_instant_options`, `ifs_login`,
`ifs_missionselect`, `ifs_missionselect_pcMulti`, `ifs_mp_sessionlist`,
`ifs_mpgs_friends`, `ifs_mpgs_login`, `ifs_opt_general`, `ifs_opt_mp`,
`ifs_opt_pccontrols`, `ifs_opt_pcvideo`, `ifs_opt_sound`,
`ifs_sp_briefing`, `ifs_sp_campaign`, `ifs_sp_gc_main`), the tab container
(`_Tabs`/`_Tabs1`/`_Tabs2` — a separate `NewIFContainer` object with its
own `y`) covers the profile-name-and-game-version label above it at a
wide resolution. The fix is a uniform downward shift of every tab
container by the same amount, `_Tabs y=31` (and likewise for
`_Tabs1`/`_Tabs2` where present) — applied to ALL screens with tabs at
once, not only the ones where the profile label is actually present:
otherwise the header height would differ from screen to screen.

Separately, the tab row is horizontally wider than its computed position:
the rounded end of the rightmost tab is drawn a few pixels wider than the
width the tab layout itself (`ifelem_tabmanager`) computes, so the row's
right edge runs past the screen edge and gets clipped. The fix is an
extra leftward shift of the tab container, the same `x` row (e.g.
`ifs_login _Tabs x=-11.5`); the amount depends on how many tabs are in
the row — `-11.5` for the 4- and 6-tab rows, `-13.65` for the
single-player row.

## 10. Спливне вікно довідки: текст перекриває кнопки / The help popup: text overlapping the buttons

**UA:**

**Проблема.** Спливне вікно довідки (кнопки «Назад»/«OK»/«Далі») будується
методом `Popup_Tutorial.SetPage(self, page)`, який рушій викликає на
КОЖНУ зміну сторінки. Одноразовий запис `@post:` тут не працює: сам
`SetPage` всередині перебудовує заголовок і кнопки на основі реального
виміру тексту (`gPopup_fnSetTitle_Internal` → `IFText_fnGetDisplayRect`),
тож будь-який одноразовий запис затирається вже першим викликом.

**Рішення.** Постійна обгортка навколо `Popup_Tutorial.SetPage`, що
виконується на кожен виклик: (1) делегує оригіналу — уся штатна поведінка
(текст сторінки, видимість кнопок) лишається; (2) міряє РЕАЛЬНУ висоту
абзацу тим самим нативом, що вже використовує сам рушій
(`IFText_fnGetDisplayRect` на `Popup_Tutorial.title.cp`); (3) ставить
`Popup_Tutorial.buttons` одразу під виміряним низом тексту (`y2`) плюс
запас 8px, через `IFObj_fnSetPos`. Обгортка ідемпотентна (повторне
обгортання неможливе) й встановлюється лише після появи глобала
`Popup_Tutorial` у пам'яті (він живе в скрипті, який вантажиться пізніше
за точку входу патча).

Запас 8px обрано за аналогією з іншими виправленнями цього документа.
Виправлення підтверджено реальним знімком гри на сторінці з найдовшим
текстом серед усіх сторінок довідки («Переміщення флотів», 4/7, вкладка
«Перемістити»): абзац повністю вміщається в межах спливного вікна, а
кнопки Назад/OK/Далі стоять одразу під текстом, без перекриття. Оскільки
обгортка на кожен виклик міряє реальну висоту тексту, а не підганяє
відступ під конкретну сторінку, результат поширюється й на решту сторінок
довідки з коротшим текстом.

Окремо, на 13 екранах Галактичного завоювання й кампанії (той самий
перелік, що й у розділі про спільну групу вище) міжрядковий інтервал
заголовка попапу звужено рядком таблиці `@post:@Popup_Tutorial.title
leading=5`.

**EN:**

**The problem.** The help popup (the Back/OK/Next buttons) is built by
`Popup_Tutorial.SetPage(self, page)`, which the engine calls on EVERY
page change. A one-time `@post:` write doesn't work here: `SetPage`
itself re-lays-out the title and buttons from a real text measurement
(`gPopup_fnSetTitle_Internal` -> `IFText_fnGetDisplayRect`), so any
one-time write gets overwritten by the very first call.

**The fix.** A permanent wrapper around `Popup_Tutorial.SetPage`, run on
every call: (1) delegates to the original — all stock behavior (page
text, button visibility) stays; (2) measures the REAL paragraph height
with the same native the engine itself already uses
(`IFText_fnGetDisplayRect` on `Popup_Tutorial.title.cp`); (3) places
`Popup_Tutorial.buttons` right below the measured text bottom (`y2`) plus
an 8px margin, via `IFObj_fnSetPos`. The wrapper is idempotent (no
double-wrapping) and installs only once the `Popup_Tutorial` global
exists in memory (it lives in a script loaded later than the patch's
entry point).

The 8px margin was chosen by analogy with the other fixes in this
document. The fix is confirmed by an in-game screenshot of the page with
the longest text among all help pages ("Fleet Relocation", 4/7, Relocate
tab): the paragraph fits fully inside the popup, and the Back/OK/Next
buttons sit right below the text with no overlap. Since the wrapper
measures the real text height on every call rather than tuning the
margin for one specific page, the result carries over to the remaining,
shorter-text help pages.

Separately, on the same 13 Galactic Conquest and campaign screens listed
above, the popup title's line spacing is tightened by the table row
`@post:@Popup_Tutorial.title leading=5`.

## 11. Обрізаний фон (`fnAddBackground`) / Clipped background (`fnAddBackground`)

**UA:**

**Причина.** Функція `ifelem_shellscreen_fnAddBackground` (спільна для
всіх екранів меню з фоном) будує контейнер фонового зображення на `w ×
widescreen` замість просто `w`, де `widescreen` — 4-те значення
`ScriptCB_GetScreenInfo()`. На 1920×1080 це дає `widescreen ≈ 1.3333` =
`(1920/1080)/(800/600)` — співвідношення «пропорція екрана / авторська
пропорція 4:3». Тобто контейнер фону на 33% ширший за екран, а
координати текстури (`uvs_r`/`uvs_t`) при цьому НІКОЛИ не виставляються
(лишаються заводськими значеннями `NewIFImage`) — тому права чверть
фонового зображення виявляється обрізаною за межі видимої області.
Висота (`localpos_b := h`) цієї помилки не має в жодному з перевірених
випадків.

**Виправлення.** Обгортка навколо `ifelem_shellscreen_fnAddBackground`:
викликає оригінал, тоді сама питає `ScriptCB_GetScreenInfo()` і примусово
повертає `bg.localpos_r` до РІВНО `w` (без множника). Це чиста
геометрична правка — жодна конкретна текстура не хардкодиться, фікс діє
однаково для будь-якого фону.

**Підтвердження.** Пікселева звірка знімків до/після підтвердила: до
фіксу контейнер справді на 33% ширший за екран і обрізає праву чверть
фонової картинки; після фіксу зображення повертається цілим, без нових
дефектів. Перевірено на 4 з 7 реально знайдених у `shell.lvl` значень
`bg_texture` (`iface_bgmeta_space` — 8 екранів, `iface_bg_1` — 5,
`single_player_campaign` — 2, `profile_manager` — 1; разом 16 із ~20
фактичних викликів `fnAddBackground`). Фікс входить у той самий
production-патч, що й решта верстки меню (не окрема збірка).

**EN:**

**Cause.** `ifelem_shellscreen_fnAddBackground` (shared by every menu
screen with a background) builds the background container at `w x
widescreen` instead of plain `w`, where `widescreen` is the 4th value of
`ScriptCB_GetScreenInfo()`. At 1920x1080 that gives `widescreen ~=
1.3333` = `(1920/1080)/(800/600)` — the "screen aspect / authored 4:3
aspect" ratio. So the background container is 33% wider than the screen,
while the texture coordinates (`uvs_r`/`uvs_t`) are NEVER set (staying at
`NewIFImage`'s factory defaults) — so the right quarter of the background
image ends up clipped outside the visible area. The height
(`localpos_b := h`) has no such error in any checked case.

**Fix.** A wrapper around `ifelem_shellscreen_fnAddBackground`: calls the
original, then asks `ScriptCB_GetScreenInfo()` itself and forces
`bg.localpos_r` back to EXACTLY `w` (no multiplier). This is a pure
geometry fix — no specific texture is hardcoded, and it works identically
for any background.

**Confirmation.** A pixel-level comparison of before/after screenshots
confirmed: before the fix the container really is 33% wider than the
screen and clips the right quarter of the background artwork; after the
fix the image comes back whole, with no new defects. Verified on 4 of 7
actually-found `bg_texture` values in `shell.lvl` (`iface_bgmeta_space` —
8 screens, `iface_bg_1` — 5, `single_player_campaign` — 2,
`profile_manager` — 1; 16 of ~20 actual `fnAddBackground` calls total).
This fix ships in the same production patch as the rest of the menu
layout work, not a separate build.

## 12. Ще не задіяний, але реалізований механізм / An implemented mechanism not currently in use

**UA:** Обгортка `@hook:<ГлобальнаФункція>:<шлях.до.віджета>` дозволяє
перезастосовувати виправлення на КОЖЕН виклик довільного рантайм-сеттера
екрана (а не лише один раз при побудові) — потрібна для полів, які
пізніше перезаписує сама гра (наприклад перемикання вкладки). Механізм
реалізований і протестований (нуль-толерантні аргументи, ідемпотентне
встановлення), але жоден рядок поточної таблиці його не використовує:
задача, під яку його спроєктовано, — перекриття `option_buttons.setting`
вкладками на `ifs_missionselect` — вирішена простішим одноразовим
`@post:option_buttons.setting posx=-100;posy=-15` (той самий рядок діє й
на `ifs_missionselect_pcMulti`, і на `ifs_instant_options`).

**EN:** The `@hook:<GlobalFunction>:<path.to.widget>` wrapper lets a correction
be re-applied on EVERY call of an arbitrary runtime setter of a screen
(not just once at build time) — needed for fields the game later
overwrites itself (e.g. switching tabs). The mechanism is implemented and
tested (nil-tolerant arguments, idempotent installation), but no row in
the current table uses it: the problem it was designed for — tabs
covering `option_buttons.setting` on `ifs_missionselect` — is solved by a
simpler one-time `@post:option_buttons.setting posx=-100;posy=-15`
instead (the same row also applies to `ifs_missionselect_pcMulti` and
`ifs_instant_options`).

## 13. Бойовий HUD (`ingame.lvl`): напис «Кількість бійців» / Combat HUD (`ingame.lvl`): the "unit count" label

**UA:** На екрані вибору бійця (`ifs_pc_spawnselect`, у `ingame.lvl`) той самий
шрифт `gamefont_large`, чию висоту (поле `HEAD`) цей інструмент збільшив
із 22 до 33px (+50%, див. `BF2_FONT_SCALING.md`) для читабельності
кирилиці, робить видимим прихований у ванілі нульовий зазор між написом
«Кількість бійців» і кнопкою «Відродження» — обидва елементи торкаються
однієї координати за побудовою формули верстки, незалежно від висоти
шрифту. Це окремий, спеціально виправлений дефект: повний розбір формули,
точна інструкція байткоду і підтвердження реальним знімком гри —
[`BF2_SPAWNSELECT_GAP_FIX.md`](BF2_SPAWNSELECT_GAP_FIX.md).

**EN:** On the unit-selection screen (`ifs_pc_spawnselect`, in `ingame.lvl`), the
same `gamefont_large` font — whose height (the `HEAD` field) this tool
enlarged from 22 to 33px (+50%, see `BF2_FONT_SCALING.md`) for Cyrillic
readability — exposes a zero gap between the "Кількість бійців" label and
the "Відродження" button that was hidden in vanilla: both elements meet
at the same coordinate by construction of the layout formula, regardless
of font height. This is a separate, specifically fixed defect: the full
formula breakdown, the exact bytecode instruction, and confirmation by a
real in-game screenshot — [`BF2_SPAWNSELECT_GAP_FIX.md`](BF2_SPAWNSELECT_GAP_FIX.md).

## 14. Другий патч розкладки для екранів у бою (`ingame.lvl`) / Second layout patch for in-battle screens (`ingame.lvl`)

**UA:** Меню налаштувань (`ifs_opt_*`), меню паузи (`ifs_pausemenu`) і лобі
мережевої гри (`ifs_mp_lobby`) відкриваються і з головного меню (`shell.lvl`),
і під час бою (`ingame.lvl`, який довантажує ці ж скрипти з `common.lvl`
через `game_interface`) — це один і той самий Lua-код, побудований тим самим
`AddIFScreen`. Тому інсталятор із розділу 3 вміє збирати ДРУГИЙ,
окремий патч для `ingame.lvl` — той самий гачок на `AddIFScreen`, та сама
таблиця виправлень, але з фільтром рядків (`GenerateAnchorFixIngameCommand.
KeepRow`):

* `ifs_opt_*` — лише виправлення обрізаного тексту (`resetbutton.label`,
  `autodetectbutton.label`, `logoInfos.envmorphing`) і горизонтальний зсув
  рядів вкладок (`_Tabs*`, поле `x`);
* `ifs_pausemenu` — усі рядки (це меню існує лише в бою);
* `ifs_mp_lobby` — рядок `Helptext_Misc.label`.

Вертикальний зсув рядів вкладок (`_Tabs* y=31`) і всі рядки, специфічні
для екранів головного меню (`ifs_login`, `ifs_missionselect` тощо), у
`ingame.lvl` не переносяться: у бою немає шапки з іменем профілю й версії
гри, яку той зсув відкривав. Довідковий попап (`Popup_Tutorial`) також
не встановлюється (`includePopupTutorialFix=false`) — з тих самих причин.

Значення для `ifs_sp_briefing` і `ifs_mp_lobby` виміряні тим самим
способом, що й решта таблиці, але без окремого підтвердження знімком
конкретно цих двох екранів — обидва відкриваються лише в процесі
проходження кампанії чи мережевої сесії, а не з головного меню напряму.

**EN:** The options menu (`ifs_opt_*`), the pause menu (`ifs_pausemenu`), and
the multiplayer lobby (`ifs_mp_lobby`) open both from the main menu
(`shell.lvl`) and during battle (`ingame.lvl`, which loads the same
scripts from `common.lvl` via `game_interface`) — it's the same Lua code,
built by the same `AddIFScreen`. So the installer from section 3 can also
build a SECOND, separate patch for `ingame.lvl` — the same `AddIFScreen`
hook, the same correction table, but with a row filter
(`GenerateAnchorFixIngameCommand.KeepRow`):

* `ifs_opt_*` — only the clipped-text fixes (`resetbutton.label`,
  `autodetectbutton.label`, `logoInfos.envmorphing`) and the tab rows'
  horizontal shift (`_Tabs*`, the `x` field);
* `ifs_pausemenu` — every row (this menu exists only in battle);
* `ifs_mp_lobby` — the `Helptext_Misc.label` row.

The tab rows' vertical shift (`_Tabs* y=31`) and every row specific to
main-menu screens (`ifs_login`, `ifs_missionselect`, etc.) are not carried
into `ingame.lvl`: battle has no header with the profile name and game
version for that shift to uncover. The help popup (`Popup_Tutorial`) is
also not installed (`includePopupTutorialFix=false`), for the same
reason.

The values for `ifs_sp_briefing` and `ifs_mp_lobby` are measured the same
way as the rest of the table, but without a separate in-game screenshot
confirming those two screens specifically — both only open in the course
of playing a campaign or a multiplayer session, not directly from the
main menu.
