# BF2: зазор між написом "Кількість бійців" і кнопкою "Відродження" (`ingame.lvl`) / BF2: gap between the "Кількість бійців" label and the "Відродження" button (`ingame.lvl`)

## 1. Дефект / The bug

**UA:** На екрані вибору бійця (`ifs_pc_spawnselect`, `ingame.lvl`) напис
"Кількість бійців: N" торкається або перекривається кнопкою
"Відродження". Причина — збільшений кириличний шрифт (див.
`BF2_FONT_SCALING.md`): нижній край тексту сягає верхнього краю кнопки,
хоча в оригіналі, з дрібнішим латинським шрифтом, вони не стикались.

**EN:** On the unit-selection screen (`ifs_pc_spawnselect`, `ingame.lvl`), the
"Кількість бійців: N" label touches or is covered by the "Відродження"
button. The cause is the enlarged Cyrillic font (see
`BF2_FONT_SCALING.md`): the text's bottom edge reaches the button's top
edge, whereas in the original, with a smaller Latin font, they didn't
touch.

## 2. Причина / Root cause

**UA:** Нижній край текстового блока обчислюється Lua-скриптом екрана як
`(y_кнопки - висота_тексту) + висота_тексту = y_кнопки` — алгебраїчна
тотожність, що виконується для БУДЬ-ЯКОЇ висоти тексту. Це означає, що
збільшення шрифту саме по собі не створює зазору: потрібно відняти від
`y` тексту БІЛЬШЕ, ніж власна висота блока (`R16` = 0.20×H), а серед
уже обчислених у цій функції регістрів немає жодного готового значення,
більшого за `R16`.

**EN:** The text block's bottom edge is computed by the screen's Lua script as
`(button_y - text_height) + text_height = button_y` — an algebraic
identity that holds for ANY text height. This means enlarging the font
by itself never creates a gap: something MORE than the block's own
height (`R16` = 0.20×H) must be subtracted from the text's `y`, and none
of the registers already computed in this function holds a value bigger
than `R16`.

## 3. Виправлення / The fix

**UA:** Одна нова інструкція `ADD R36 := R16 + R9` (де `R9` = 0.03×H — уже
обчислене в тій самій функції значення, використане деінде як типовий
запас) і перепризначення операнда C наявної інструкції `SUB` з `R16` на
`R36`. Обидві інструкції поміщаються рівно в місце, яке раніше займали
дві: сам цей `SUB` (pc152) і мертвий, доведено зайвий дублікат
`SETTABLE font:=R17` (pc159 — буквально ідентичний уже виконаному на
pc154, отже нічого не змінює). Розмір `BODY`-чанка, розмір усього
файлу та номери всіх інструкцій після цього вікна (включно з цілями
`JMP`/`FORLOOP` на pc172/242/244/259) лишаються байт-у-байт незмінними.
Кнопка "Відродження" (0.9×H) і власна геометрія тексту (`height`/
`texth` = 0.20×H) не змінюються — рухається лише позиція напису, вгору
на 0.03×H.

Регістр `R36` обрано як мертвий на момент pc152: востаннє записаний на
pc149 (`DIV`), спожитий одразу на pc150 (`SUB`), і не читається знову
аж до pc173 (де й так перезаписується заново, вже в тілі циклу нижче
по функції). `maxstacksize` не підвищується (36 < 41 — регістр уже в
межах наявного стека).

Змінено рівно 32 байти файлу; перевірено round-trip через
`Lua50BytecodeReader` на реальному `ingame.lvl` — файл парситься без
винятків, а решта функції (до pc152 і після pc159) побайтово ідентична
оригіналу. Стосується лише екрана `ifs_pc_spawnselect` — інші екрани
не зачіпаються.

**EN:** One new instruction, `ADD R36 := R16 + R9` (where `R9` = 0.03×H, a
value already computed in the same function and used elsewhere as a
typical margin), plus repointing the C operand of an existing `SUB`
instruction from `R16` to `R36`. Both instructions fit exactly into the
space previously occupied by two: this same `SUB` (pc152) and a dead,
proven-redundant duplicate `SETTABLE font:=R17` (pc159 — literally
identical to the one already executed at pc154, so it changes nothing).
The `BODY` chunk's size, the whole file's size, and the pc numbers of
every instruction after this window (including the `JMP`/`FORLOOP`
targets at pc172/242/244/259) all stay byte-for-byte unchanged. The
"Відродження" button (0.9×H) and the text's own geometry (`height`/
`texth` = 0.20×H) are not changed — only the label's position moves, up
by 0.03×H.

Register `R36` was chosen because it's dead at pc152: last written at
pc149 (`DIV`), consumed immediately at pc150 (`SUB`), and not read
again until pc173 (where it's overwritten fresh anyway, inside the loop
further down the function). `maxstacksize` is not raised (36 < 41 — the
register is already within the existing stack frame).

Exactly 32 bytes of the file change; verified by a round-trip through
`Lua50BytecodeReader` on the real `ingame.lvl` — the file parses with no
exceptions, and the rest of the function (before pc152 and after pc159)
is byte-identical to the original. Affects only the `ifs_pc_spawnselect`
screen — other screens are unaffected.

## 4. Підтвердження / Confirmation

**UA:** Перевірено реальною грою: на екрані вибору бійця напис "Кількість
бійців: N" відображається з чистим видимим зазором над кнопкою
"Відродження" — збільшений кириличний шрифт кнопку не перекриває.

**EN:** Verified with a real game session: on the unit-selection screen, the
"Кількість бійців: N" label renders with a clean, visible gap above the
"Відродження" button — the enlarged Cyrillic font does not cover the
button.
## 5. Перелік класів притиснутий до верху екрана / The class list sits flush against the top of the screen

**UA:** На тому самому екрані (`ifs_pc_spawnselect`, `ingame.lvl`) перелік
класів для найму за повного складу (7 і більше комірок) має значно менший
відступ зверху, ніж знизу. `ifs_pc_SpawnSelect_fnBuildScreen` обчислює
позицію комірки `i` як `y_i = R24 + i·(R9 + R27 + R23 + 2.0)`, де
`R24 = 15.0 + R23` (константа `15.0` — базовий відступ зверху, R23 —
висота шрифту заголовка + 3.0). Вимір на реальному знімку з повним
переліком: верх першої комірки y≈22px, низ сьомої y≈999px (відступ знизу
81px). Δ=(81−22)/2=29.5, округлено до 30.

Виправлення — та сама техніка точкового патчу константи, що й у розділі
3: константа `15.0` (pc65, ADD R24 := K(15.0) + R23) замінена на `45.0`
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

The fix uses the same targeted constant-patch technique as section 3: the
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
