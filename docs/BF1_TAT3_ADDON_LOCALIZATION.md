# BF1: локалізація аддону Tat3 ("Jabba's Palace") / BF1: localizing the Tat3 add-on ("Jabba's Palace")

## 1. Чому це окремий файл / Why this is a separate file

**UA:** Tat3 — офіційний безкоштовний аддон-карта до Star Wars: Battlefront
(Classic, 2004), доступний у Steam-версії гри. Він зберігає власний,
окремий набір ресурсів під `GameData\AddOn\Tat3\` зі своїм `core.lvl` і
своїм скриптом підключення (`addme.script`) — обидва потребують окремої
обробки, поза основним `core.lvl` базової гри.

**EN:** Tat3 is an official free add-on map for Star Wars: Battlefront
(Classic, 2004), available in the Steam version of the game. It keeps
its own, separate set of resources under `GameData\AddOn\Tat3\`, with
its own `core.lvl` and its own hookup script (`addme.script`) — both
need handling separate from the base game's main `core.lvl`.

## 2. Три файли / Three files

**UA:**

| Файл | Що змінюється |
|---|---|
| `GameData\Data\_LVL_PC\core.lvl` (база) | основні рядки інтерфейсу та кириличні гліфи (спільно з рештою BF1) плюс один додатковий запис `Locl` (ключ `level.tat3.name`) для назви карти аддону |
| `GameData\AddOn\Tat3\Data\_lvl_pc\core.lvl` (аддон) | власна таблиця `Locl` аддону: 2391 запис усього, з яких 2369 — точний дублікат базової таблиці, а 22 — справді нові рядки місії аддону; шрифтів усередині немає — аддон використовує вже завантажений атлас базової гри; той самий запис `level.tat3.name`, що й у базі, додається сюди теж, як нешкідлива страховка |
| `GameData\AddOn\Tat3\addme.script` | константа `showstr` замінена з жорстко заданого англійського рядка "TATOOINE: JABBA" на ключ локалізації "level.tat3.name" |

**EN:**

| File | What changes |
|---|---|
| `GameData\Data\_LVL_PC\core.lvl` (base) | the main interface strings and Cyrillic glyphs (shared with the rest of BF1) plus one additional `Locl` record (key `level.tat3.name`) for the add-on's map name |
| `GameData\AddOn\Tat3\Data\_lvl_pc\core.lvl` (add-on) | the add-on's own `Locl` table: 2391 entries total, of which 2369 are an exact duplicate of the base table and 22 are genuinely new add-on mission strings; no fonts inside — the add-on reuses the base game's already-loaded atlas; the same `level.tat3.name` record as in the base file is also added here, as a harmless safety net |
| `GameData\AddOn\Tat3\addme.script` | the `showstr` constant is rewritten from the hardcoded English string "TATOOINE: JABBA" to the localization key "level.tat3.name" |

## 3. Чому назва карти жила поза `Locl` / Why the map name lived outside `Locl`

**UA:** Екран вибору карти малює назву через `IFText_fnSetString` —
локалізований виклик: він хешує передане значення як ключ і шукає його в
`Locl`. Базові карти передають туди саме ключ (звідси переклад у грі —
наприклад "ТАТУЇН: МОРЕ ПІСКУ"), а аддон передавав готовий англійський
рядок напряму — пошук нічого не знаходив, і гра показувала рядок як є. Це
була єдина неперекладна назва у списку карт.

**EN:** The map-select screen draws the name via `IFText_fnSetString` —
the localized call: it hashes the given value as a key and looks it up
in `Locl`. Base maps pass a key there (hence the translated names in
game, e.g. "ТАТУЇН: МОРЕ ПІСКУ"), while the add-on passed a ready
English string directly — the lookup found nothing, and the game showed
the string verbatim. This was the only untranslatable name in the map
list.

## 4. Чому заміна в `addme.script` безпечна / Why the `addme.script` rewrite is safe

**UA:** "TATOOINE: JABBA" і "level.tat3.name" — обидва рівно 15 символів.
Тому Lua-префікс довжини (16 = 15+NUL) і всі три поля розмірів контейнера
(`BODY`, `scr_`, `ucfb`) лишаються незмінними, а патч зводиться до заміни
15 байтів на місці без перерахунку розмірів файлу.

**EN:** "TATOOINE: JABBA" and "level.tat3.name" are both exactly 15
characters. The Lua length prefix (16 = 15+NUL) and all three container
size fields (`BODY`, `scr_`, `ucfb`) therefore stay unchanged, and the
patch is a 15-byte in-place replacement with no file-size
recalculation.

## 5. Підтвердження / Confirmation

**UA:** Реальним тестом підтверджено, що `IFText_fnSetString` справді
робить пошук за хешем із фолбеком на сирий рядок: запис лише в
аддонному `core.lvl` (без запису в базовому) змушує гру показати
буквально "level.tat3.name" замість перекладеної назви — це й довело
механізм пошуку, і показало, що резолвиться запис саме з БАЗОВОГО
`core.lvl`, а не з аддонного.

**EN:** A real test confirmed that `IFText_fnSetString` does perform a
hash lookup with a raw-string fallback: a record placed only in the
add-on's `core.lvl` (with no record in the base file) makes the game
display "level.tat3.name" literally instead of the translated name —
proving the lookup mechanism, and showing that the record resolves
from the BASE `core.lvl`, not the add-on's.
