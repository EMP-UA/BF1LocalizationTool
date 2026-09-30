# BF2: екран вибору бійця (`ingame.lvl`) — напис "Кількість бійців", кнопка "Спавн", перелік класів / BF2: unit-selection screen (`ingame.lvl`) — "Unit Count" label, "Spawn" button, class list

## 1. Розкладка напису й кнопки / Label and button layout

**UA:** На екрані вибору бійця (`ifs_pc_spawnselect`, `ingame.lvl`)
`ifs_pc_SpawnSelect_fnBuildScreen` створює напис "Кількість бійців: N"
як `NewIFText` з `valign="bottom"`, висотою блока `texth` = `R16`
(0.20×safeH) і координатою `y` = `R35`. У ванільному коді
`y = R31 − R16`, де `R31` — `y` кнопки "Спавн". Нижній край текстового
блока обчислюється як `(y_кнопки − висота_блока) + висота_блока =
y_кнопки` — алгебраїчна тотожність, що виконується для БУДЬ-ЯКОЇ висоти
блока й шрифту. Тому збільшення шрифту зазору не створює: над кнопкою
текст піднімають лише завершальні рядки самого тексту.

Оригінальні рядки `Locl` — `Unit Count: %d\r\n\r\n` і
`Unit Count: %d  Max: %d\r\n\r\n` — закінчуються двома порожніми
рядками. За `valign="bottom"` вони стоять ПІД видимим текстом і
піднімають його над кнопкою. Переклад у `core.lvl` зберігає ці два
завершальні переноси так само, як оригінал; без них нижній край видимого
тексту збігається з `y` кнопки, і текст торкається кнопки.

**EN:** On the unit-selection screen (`ifs_pc_spawnselect`, `ingame.lvl`),
`ifs_pc_SpawnSelect_fnBuildScreen` creates the "Unit Count: N" label as
a `NewIFText` with `valign="bottom"`, block height `texth` = `R16`
(0.20×safeH) and coordinate `y` = `R35`. In the vanilla code
`y = R31 − R16`, where `R31` is the "Spawn" button's `y`. The text
block's bottom edge is computed as `(button_y − block_height) +
block_height = button_y` — an algebraic identity that holds for ANY
block height and font. So enlarging the font creates no gap by itself:
only the trailing lines of the text itself lift it above the button.

The original `Locl` strings — `Unit Count: %d\r\n\r\n` and
`Unit Count: %d  Max: %d\r\n\r\n` — end with two empty lines. Under
`valign="bottom"` they sit BELOW the visible text and lift it above the
button. The `core.lvl` translation keeps these two trailing line breaks
exactly as the original does; without them the visible text's bottom
edge coincides with the button's `y` and the text touches the button.

## 2. Положення напису / Label position

**UA:** Два порожні рядки шрифту `gamefont_large` піднімають видимий
текст на ≈66 px над кнопкою на 1920×1080 (крок рядка 33 px виміряно на
трирядковому написі на скріншоті гри). Патч опускає блок напису рівно на
ОДИН рядок його шрифту:

    y = R31 − (R16 − R23)

де `R23 = ScriptCB_GetFontHeight(шрифт напису) + 3` (pc61-64 прототипу).
Видимий зазор між текстом і кнопкою дорівнює одному порожньому рядку
мінус 3 px: розрахунково ≈31 px на 1920×1080, на скріншотах гри
≈30–32 px. Зазор масштабується разом зі шрифтом, бо `R23` обчислюється
з висоти того самого шрифту. Шрифт, `texth` і положення кнопки цим
патчем не змінюються.

Реалізація — переписування 8-словного вікна pc152–pc159 на місці:

- слот 0: `SUB R36 := R16 − R23`;
- слот 1: `SUB R35 := R31 − R36`;
- слоти 2–7: інструкції pc153–pc158 без змін, зсунуті на одну позицію;
- pc159 — доведено мертвий дублікат pc154 (`SETTABLE font:=R17`,
  побітово ідентичний), у нове вікно не копіюється.

Розмір `BODY`-чанка, розмір усього файлу та номери всіх інструкцій після
вікна (включно з цілями `JMP`/`FORLOOP` на pc172/242/244/259) лишаються
байт-у-байт незмінними.

Регістри: `R36` вільний на pc152 — востаннє записаний на pc149 (`DIV`),
спожитий на pc150 (`SUB`), наступний запис — pc173 у тілі циклу. `R23`
лише читається: записаний на pc63–64, між pc65 і pc151 жодна інструкція
не має `A=23` (`BuildPlan` це перевіряє), далі читається в циклі сітки
класів (pc186, pc212, pc220) — значення там те саме. `maxstacksize` не
підвищується (36 < 41). `BuildPlan` кидає виняток на будь-яку
розбіжність структури; перевірено round-trip через
`Lua50BytecodeReader`. Стосується лише екрана `ifs_pc_spawnselect`.

Реалізація — Core/Bf2Widescreen/SpawnSelectUnitCountGapPatchBuilder.cs,
BF1LocalizationTool.Diagnostic/GenerateSpawnSelectUnitCountGapFixCommand.cs.

**EN:** Two empty `gamefont_large` lines lift the visible text ≈66 px
above the button at 1920×1080 (the 33 px line pitch is measured on the
three-line label in an in-game screenshot). The patch lowers the label
block by exactly ONE line of its own font:

    y = R31 − (R16 − R23)

where `R23 = ScriptCB_GetFontHeight(the label's font) + 3` (pc61-64 of
the prototype). The visible gap between the text and the button equals
one empty line minus 3 px: ≈31 px at 1920×1080 by calculation, ≈30–32 px
in in-game screenshots. The gap scales with the font, because `R23` is
computed from that same font's height. The font, `texth` and the button
position are not changed by this patch.

Implementation — an in-place rewrite of the 8-word window pc152–pc159:

- slot 0: `SUB R36 := R16 − R23`;
- slot 1: `SUB R35 := R31 − R36`;
- slots 2–7: the pc153–pc158 instructions unchanged, shifted by one
  position;
- pc159 — a proven-dead duplicate of pc154 (`SETTABLE font:=R17`,
  bit-identical), not copied into the new window.

The `BODY` chunk's size, the whole file's size and the pc numbers of
every instruction after the window (including the `JMP`/`FORLOOP` targets
at pc172/242/244/259) stay byte-for-byte unchanged.

Registers: `R36` is free at pc152 — last written at pc149 (`DIV`),
consumed at pc150 (`SUB`), next written at pc173 inside the loop body.
`R23` is only read: it is written at pc63–64, no instruction between pc65
and pc151 has `A=23` (`BuildPlan` checks this), and it is read afterwards
in the class-grid loop (pc186, pc212, pc220) — the value there is the
same. `maxstacksize` is not raised (36 < 41). `BuildPlan` throws on any
structural mismatch; verified by a round-trip through
`Lua50BytecodeReader`. Affects only the `ifs_pc_spawnselect` screen.

Implementation — Core/Bf2Widescreen/SpawnSelectUnitCountGapPatchBuilder.cs,
BF1LocalizationTool.Diagnostic/GenerateSpawnSelectUnitCountGapFixCommand.cs.

## 3. Положення кнопки "Спавн" / "Spawn" button position

**UA:** У pc130–131 прототипу `fnBuildScreen`:

    MUL R31 := K(0.1) × R3      (R3 — safeH з ScriptCB_GetSafeScreenInfo)
    SUB R31 := R3 − R31         (y кнопки = 0.90 × safeH)

Операнд B інструкції pc130 вказує на константу 0.05, що вже є в пулі
констант (її читає й pc66; значення константи не змінюється, тож pc66
не зачіпається): `y` кнопки = 0.95 × safeH. Розмір `BODY`-чанка й файлу
не змінюється. Виміряно на скріншотах гри 1920×1080: підпис кнопки
y 922–937 → 970–985 (+48 px). Напис "Кількість бійців" рухається на ту
саму відстань, бо його `y` рахується від `R31`; зазор між ними не
змінюється. `BuildPlan` перевіряє, що константа 0.05 у пулі рівно одна й
що pc131 — `SUB R31 := R3 − R31`.

Положення 3D-моделі бійця цей патч не змінює. `fStartY`/`fEndY` у
`ifs_pc_spawnselect_animateicons` задають 2D-позицію об'єкта
(`IFObj_fnSetPos`), а видиме положення моделі — 3D-трансляція `NewIFModel`
(x/y/z → `ScriptCB_IFModel_SetTranslation`, `interface_util` у
`common.lvl`). Значення `fStartY` 0.17 і 0.10, а також різні значення
y 3D-трансляції `SideModel0`/`SideModel1` дають на скріншотах гри те саме
положення моделі: положення моделі визначає рушій.

Реалізація — Core/Bf2Widescreen/SpawnSelectVerticalLayoutPatchBuilder.cs,
BF1LocalizationTool.Diagnostic/GenerateSpawnSelectVerticalLayoutFixCommand.cs.

**EN:** At pc130–131 of the `fnBuildScreen` prototype:

    MUL R31 := K(0.1) × R3      (R3 — safeH from ScriptCB_GetSafeScreenInfo)
    SUB R31 := R3 − R31         (button y = 0.90 × safeH)

Operand B of pc130 points to the 0.05 constant already in the constant
pool (pc66 also reads it; the constant's value is not changed, so pc66 is
unaffected): button `y` = 0.95 × safeH. The `BODY` chunk's and the file's
size are unchanged. Measured on 1920×1080 in-game screenshots: button
label y 922–937 → 970–985 (+48 px). The "Unit Count" label moves by the
same distance, because its `y` is computed from `R31`; the gap between
them is unchanged. `BuildPlan` verifies that the 0.05 constant occurs
exactly once in the pool and that pc131 is `SUB R31 := R3 − R31`.

The soldier 3D model's position is not changed by this patch.
`fStartY`/`fEndY` in `ifs_pc_spawnselect_animateicons` set the object's
2D position (`IFObj_fnSetPos`), while the model's visible position is the
`NewIFModel` 3D translation (x/y/z → `ScriptCB_IFModel_SetTranslation`,
`interface_util` in `common.lvl`). `fStartY` values 0.17 and 0.10, and
different y values of the `SideModel0`/`SideModel1` 3D translation, give
the same model position in in-game screenshots: the model position is
controlled by the engine.

Implementation — Core/Bf2Widescreen/SpawnSelectVerticalLayoutPatchBuilder.cs,
BF1LocalizationTool.Diagnostic/GenerateSpawnSelectVerticalLayoutFixCommand.cs.

## 4. Підтвердження / Confirmation

**UA:** Перевірено реальною грою (1920×1080) на скріншотах: між написом
"Кількість бійців: N" і кнопкою "Спавн" видно чистий зазор ≈30–32 px;
підпис кнопки зсунувся на +48 px (розділ 3).

**EN:** Verified with a real game session (1920×1080) on screenshots: a
clean gap of ≈30–32 px is visible between the "Unit Count: N" label and
the "Spawn" button; the button label sits +48 px lower (section 3).

## 5. Перелік класів притиснутий до верху екрана / The class list sits flush against the top of the screen

**UA:** На тому самому екрані (`ifs_pc_spawnselect`, `ingame.lvl`) перелік
класів для найму за повного складу (7 і більше комірок) має значно менший
відступ зверху, ніж знизу. `ifs_pc_SpawnSelect_fnBuildScreen` обчислює
позицію комірки `i` як `y_i = R24 + i·(R9 + R27 + R23 + 2.0)`, де
`R24 = 15.0 + R23` (константа `15.0` — базовий відступ зверху, R23 —
висота шрифту заголовка + 3.0). Вимір на реальному знімку з повним
переліком: верх першої комірки y≈22px, низ сьомої y≈999px (відступ знизу
81px). Δ=(81−22)/2=29.5, округлено до 30.

Виправлення — точковий патч константи: константа `15.0` (pc65, ADD R24 := K(15.0) + R23) замінюється на `45.0`
(+30px). Крок сітки, розміри комірок і шрифт не змінюються. `BuildPlan`
перевіряє, що ця константа використовується в прототипі РІВНО один раз,
перш ніж дозволити патч. Загальна таблиця розкладки (`Bf2LayoutTable.txt`)
тут не застосовна: екран реєструється через `NewIFShellScreen`, не прямим
`AddIFScreen`, а контейнер `Info` разом із сіткою несе й бічні значки
(`SideModel0`/`SideModel1`), яких зсув через `posy` торкнувся б зайво.
Відступи зверху/знизу після зсуву за повного (7-комірковим) переліку:
≈52px / ≈50px (були 22px / 81px).

Реалізація — Core/Bf2Widescreen/SpawnSelectListTopOffsetPatchBuilder.cs,
BF1LocalizationTool.Diagnostic/GenerateSpawnSelectListTopOffsetFixCommand.cs.

**EN:** On the same screen (`ifs_pc_spawnselect`, `ingame.lvl`), the class
list to recruit from, with a full roster (7+ slots), has a much smaller
top gap than bottom gap. `ifs_pc_SpawnSelect_fnBuildScreen` computes slot
`i`'s position as `y_i = R24 + i·(R9 + R27 + R23 + 2.0)`, where
`R24 = 15.0 + R23` (the `15.0` constant is the base top offset, R23 is the
title font's height + 3.0). Measured on a real screenshot with a full
list: first slot's top at y≈22px, seventh slot's bottom at y≈999px (81px
bottom gap). Δ=(81−22)/2=29.5, rounded to 30.

The fix is a targeted constant patch: the
`15.0` constant (pc65, ADD R24 := K(15.0) + R23) is replaced with `45.0`
(+30px). The grid pitch, cell sizes and font are unchanged. `BuildPlan`
verifies this constant is used EXACTLY once in the prototype before
allowing the patch. The generic layout table (`Bf2LayoutTable.txt`) does
not apply here: the screen is registered via `NewIFShellScreen`, not a
direct `AddIFScreen`, and the `Info` container that holds the grid also
carries the side icons (`SideModel0`/`SideModel1`), which a `posy` shift
would needlessly move too. Post-shift top/bottom gaps with a full
(7-slot) list: ≈52px / ≈50px (were 22px / 81px).

Implementation — Core/Bf2Widescreen/SpawnSelectListTopOffsetPatchBuilder.cs,
BF1LocalizationTool.Diagnostic/GenerateSpawnSelectListTopOffsetFixCommand.cs.

## 6. Порядок застосування / Application order

**UA:** У фінальній збірці `ingame.lvl` (`BF2_FINAL_FILES_GENERATION.md`,
розділ 3) патчі застосовуються послідовно: розкладка в бою → положення
напису (розділ 2) → зсув переліку класів (розділ 5) → положення кнопки
(розділ 3). Кожен патч працює з вікном або константою свого прототипу й
не залежить від інших; самостійні пункти меню кожного патча приймають
відповідний вхідний файл окремо.

**EN:** In the final `ingame.lvl` assembly (`BF2_FINAL_FILES_GENERATION.md`,
section 3) the patches are applied in sequence: in-battle layout → label
position (section 2) → class-list offset (section 5) → button position
(section 3). Each patch works on its own window or constant of the
prototype and does not depend on the others; the standalone menu item of
each patch takes its own input file.

## 7. Ванільні дефекти / Vanilla defects

**UA:** Обидва дефекти належать ванільній грі, тому цим проєктом не
виправляються.

- Лічильник "Кількість бійців" показує 0 завжди, якщо доступний лише
  один клас.
- Print Screen у бою призводить до краху гри. Відтворюється на повністю
  ванільних файлах гри з вимкненим `d3d9.dll`. Поза боєм (зокрема на
  екрані вибору бійця) крах не відтворюється. Обхід — знімок екрана
  засобами Steam (F12).

**EN:** Both defects belong to the vanilla game, so this project does not
fix them.

- The "Unit Count" counter always shows 0 when only one class is
  available.
- Print Screen in combat crashes the game. It reproduces on fully vanilla
  game files with `d3d9.dll` disabled. Outside combat (including the
  unit-selection screen) the crash does not reproduce. Workaround — a
  screenshot through Steam (F12).
