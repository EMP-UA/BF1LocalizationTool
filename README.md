# BF1LocalizationTool

> Інструмент для локалізації Star Wars: Battlefront (Classic 2004) та Battlefront II (2005)
> Localization editor for Star Wars: Battlefront (Classic 2004) and Battlefront II (2005)

[![Version](https://img.shields.io/badge/Version-1.0.0-8A46C1.svg)](https://github.com/EMP-UA/BF1LocalizationTool/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-8A46C1.svg)](LICENSE)
[![Platform: Windows 10 | 11](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-C989F3.svg)](https://github.com/EMP-UA/BF1LocalizationTool)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-8A46C1.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

*Silence will fall.* ⚡

---

## UA: Що це

WPF-редактор для створення та редагування українських (і будь-яких інших)
локалізацій **Star Wars: Battlefront (Classic 2004)** та **Star Wars: Battlefront II (2005)** (Steam).

Інструмент читає і пише `core.lvl` напряму в пам'яті через власний
`UcfbReader`/`UcfbWriter` — без сторонніх утиліт (QuickBMS, munge/unmunge тощо).
Уся логіка перетворень відкрита, що дозволяє верифікувати мод на NexusMods.

## EN: What this is

A WPF editor for creating and editing Ukrainian (or any other) localizations of
**Star Wars: Battlefront (Classic 2004)** and **Star Wars: Battlefront II (2005)** (Steam).

The tool reads and writes `core.lvl` directly in memory via its own
`UcfbReader`/`UcfbWriter` — no third-party utilities required (no QuickBMS,
no munge/unmunge). All transformation logic is open source, allowing mod
verification on NexusMods.

---

## Формат файлів / File format

Гра зберігає ресурси у `.lvl` файлах — ієрархічних **ucfb**-контейнерах
(Pandemic Studios), з 4-байтовим вирівнюванням між чанками:

```
[4 байти] Id       — FourCC або uint32 хеш
[4 байти] DataSize — розмір даних (little-endian, без padding)
[N байтів] Data    — вкладені чанки або сирі дані
[padding]          — вирівнювання до 4 байтів (не входить у DataSize)
```

Рядки локалізації знаходяться в `core.lvl → localization → [мова]` у **двох**
можливих форматах:

| Формат | Ігри | Кодування | Кирилиця |
|---|---|---|---|
| Plain-text `.txt` (`0xHASH text\n`) | BF1, BF2 | Latin1 (ISO-8859-1) | ❌ не підтримується |
| Бінарний `Locl` (hash `uint32` + totalSize `uint16` + текст) | BF2 | UTF-16LE | ✅ повна підтримка |

```
0xe2fc1d61 PRESS X TO CONTINUE
0x9bbcda3c CLONE WARS CAMPAIGN
```

Версія гри визначається автоматично за шляхом до файлу (наявність
`"Battlefront II"` у шляху → BF2, інакше → BF1); формат (`.txt` чи бінарний
`Locl`) визначається за фактичним вмістом чанка.

## File format (EN)

Game resources are stored in `.lvl` files — hierarchical **ucfb** containers
(Pandemic Studios), 4-byte aligned between chunks (see table/diagram above).
Localization strings live under `core.lvl → localization → [language]` in one
of two formats: plain-text `.txt` (Latin1, both games) or binary `Locl`
(UTF-16LE, BF2 only — the only fully Unicode-safe channel, including Cyrillic).

Game version is auto-detected from the file path; format is auto-detected
from the actual chunk content.

---

## ✅ Кирилиця в `.txt`-каналі (BF1) / Cyrillic in the `.txt` channel (BF1)

**UA:** Сам по собі `.txt`-формат (Latin1) не підтримує кирилицю для
BF1. `BF1LocalizationTool.Diagnostic` (розповсюджується лише як
вихідний код — `dotnet run --project BF1LocalizationTool.Diagnostic`)
вирішує це, ін'єктуючи нові кириличні гліфи напряму в шрифтовий атлас
`core.lvl` реальними Unicode-кодами, без обрізання жодного англійського
гліфа — деталі формату й фінальний вибір шрифтів `FONT_FORMAT_SPEC.md`
§7. Рендер потребує кількох файлів шрифтів (SIL OFL, Google Fonts, не
входять у репозиторій) у `Fonts\` — точний перелік §7.6.

Аддон Tat3 (BF1, «Палац Джабби») має власну таблицю тексту (2391
спільний рядок із базовою грою, 22 унікальні) і не містить власних
шрифтів — використовує атлас базової гри. Назва карти аддону — єдиний
рядок BF1 поза `Locl`; інструмент робить її перекладною так само (§8,
§8.3).

Увесь текст BF1 підтверджено лежить рівно у двох файлах
(`Data\_LVL_PC\core.lvl`, `AddOn\Tat3\...\core.lvl`), шрифти — лише в
першому (§8).

Метрична модель гліфів верифікована побайтовим сканом повного алфавіту
обох ігор (§7.5, §7.7–§7.8). Два відкриті обмеження: коефіцієнт
збільшення шрифту BF2 (×1,5) підібраний емпірично, не доведено
оптимальний; підсистема "BF2 widescreen" (верстка меню під 1080p)
існує в коді як експериментальна, не інтегрована в основний робочий
процес.

**EN:** The `.txt` format (Latin1) does not support Cyrillic for BF1 on
its own. `BF1LocalizationTool.Diagnostic` (distributed as source only —
`dotnet run --project BF1LocalizationTool.Diagnostic`) solves this by
injecting new Cyrillic glyphs directly into the `core.lvl` font atlas
using real Unicode code points, with no existing English glyph
overwritten — format details and the final font choice are in
`FONT_FORMAT_SPEC.md` §7. Rendering requires a few font files (SIL OFL,
Google Fonts, not bundled in this repository) in `Fonts\` — exact list
in §7.6.

The Tat3 add-on (BF1, "Jabba's Palace") has its own text table (2391
strings shared with the base game, 22 unique) and carries no fonts of
its own — it uses the base game's atlas. The add-on's map name is
BF1's only string outside `Locl`; the tool makes it translatable the
same way (§8, §8.3).

All of BF1's text is confirmed to live in exactly two files
(`Data\_LVL_PC\core.lvl`, `AddOn\Tat3\...\core.lvl`), fonts only in the
first (§8).

The glyph metric model is verified via a byte-level scan of the full
alphabet in both games (§7.5, §7.7–§7.8). Two open limitations remain:
the BF2 font enlargement factor (×1.5) was chosen empirically and isn't
proven optimal; a "BF2 widescreen" subsystem (1080p menu layout) exists
in the code as experimental and isn't integrated into the main
workflow.

---

## Можливості GUI / GUI features

**UA:**
- 🎨 Темна/світла тема (dark/light theme)
- 🔍 Фільтри рядків за мовою, статусом перекладу, технічністю — три статуси
  (Технічний / Перекладено / Без перекладу) взаємовиключні
- ✅ **ValidationService** — перевірка збереження технічних маркерів
  (`%s %d %i %f %c %u`, `{btn...}`, `[X]`, `\n \t`) в перекладі, жорстке
  відхилення російських літер (`ыэёъ`), що потрапили в переклад
- ⚠️ **Поріг "задовгого" перекладу** (⚙ у статус-барі) — configurable
  `оригінал×коефіцієнт+запас` (за замовчуванням 1,3×+4), позначає
  переклади, що ризикують не влізти в UI, у фільтрі «Проблемні» —
  калібровано на реальних випадках обрізання тексту в грі
- 🤖 **TechnicalStringService** — автовизначення рядків, що не потребують
  перекладу (URL, числа, ідентифікатори `WORD_WORD`/`word_word`, назви клавіш,
  чит-коди, позначення техніки, copyright-текст тощо)
- 💾 **AutoSaveService** — автозбереження прогресу перекладу
- 📝 **SimpleLogger** — логування дій для діагностики
- 📤 Експорт/імпорт CSV для batch-перекладу через зовнішні сервіси або
  окремі локальні інструменти (напр. Gemini-based пайплайн з глосарієм і
  валідацією маркерів — не входить у цей репозиторій, див. розділ
  "Робочий процес" нижче)

**EN:**
- 🎨 Dark/light theme
- 🔍 Row filters by language, translation status, technical-ness — three
  mutually exclusive statuses (Technical / Translated / Untranslated)
- ✅ **ValidationService** — checks that technical markers are preserved
  (`%s %d %i %f %c %u`, `{btn...}`, `[X]`, `\n \t`) in the translation, and
  strictly rejects Russian letters (`ыэёъ`) that end up in a translation
- ⚠️ **"Too long" translation threshold** (⚙ in the status bar) —
  configurable `original×coefficient+margin` (default 1.3×+4), flags
  translations at risk of not fitting the UI in the "Issues" filter —
  calibrated against real in-game text-truncation cases
- 🤖 **TechnicalStringService** — auto-detects strings that don't need
  translation (URLs, numbers, `WORD_WORD`/`word_word` identifiers, key
  names, cheat codes, vehicle/unit designations, copyright text, etc.)
- 💾 **AutoSaveService** — autosaves translation progress
- 📝 **SimpleLogger** — logs actions for diagnostics
- 📤 CSV export/import for batch translation via external services or
  separate local tools (e.g. a Gemini-based pipeline with a glossary and
  marker validation — not part of this repository, see the "Workflow"
  section below)

---

## Архітектура / Architecture

```
BF1LocalizationTool/
├── BF1LocalizationTool.Core/              # Бізнес-логіка / Business logic
│   ├── Chunks/
│   │   └── UcfbChunk.cs                   # Модель чанку / Chunk model
│   ├── IO/
│   │   ├── UcfbReader.cs                  # Читання .lvl (4-byte align) / Read .lvl
│   │   └── UcfbWriter.cs                  # Запис .lvl (4-byte align) / Write .lvl
│   ├── Localization/
│   │   ├── LocalizationParser.cs          # Plain-text 0xHASH формат / Plain-text parser
│   │   ├── LoclChunkParser.cs             # Бінарний Locl (UTF-16LE) / Binary Locl parser
│   │   └── LvlLocalizationService.cs      # Фасад, автодетект BF1/BF2 / Facade, BF1/BF2 autodetect
│   ├── Models/
│   │   ├── LocalizationEntry.cs           # Один рядок / Single string
│   │   └── LocalizationFile.cs            # Один мовний файл / One language file
│   ├── Fonts/                              # Розбір і патчинг бінарного формату шрифту
│   │                                        # (таблиця гліфів, донорський підбір,
│   │                                        # HEAD-фікс висоти) — FONT_FORMAT_SPEC.md
│   │                                        # Font binary format parsing/patching
│   │                                        # (glyph table, donor matching, HEAD height fix)
│   ├── Scripts/                            # Lua 5.0 bytecode: читання/запис,
│   │                                        # побудова функцій для BF2-скриптів
│   │                                        # Lua 5.0 bytecode read/write,
│   │                                        # function builder for BF2 scripts
│   ├── Bf2Widescreen/                      # BF2: розкладка меню під нестандартну
│   │                                        # роздільність (Data/Bf2LayoutTable.txt),
│   │                                        # субтитри вступного ролика кампанії,
│   │                                        # d3d9.dll-проксі для субтитрів роликів —
│   │                                        # docs/BF2_UI_LAYOUT_FIX.md
│   │                                        # BF2: menu layout for non-standard
│   │                                        # resolutions, campaign intro subtitles,
│   │                                        # d3d9.dll proxy for movie subtitles
│   ├── Bf2Movies/                          # BF2: шрифт субтитрів вступних роликів
│   │                                        # BF2: intro movie subtitle font
│   └── Bf2Exe/                             # BF2: аналіз .exe БЕЗ його патчингу
│                                            # (файл .exe гри не змінюється)
│                                            # BF2: .exe analysis without patching it
│                                            # (the game's .exe is never modified)
│
├── BF1LocalizationTool.GUI/                # WPF інтерфейс / WPF interface
│   ├── App.xaml / MainWindow.xaml(.cs)     # UI, EntryRow (статуси, поріг довжини)
│   ├── CompareWindow.xaml(.cs)             # Порівняння перекладів / Translation comparison
│   └── Services/
│       ├── ValidationService.cs           # Перевірка маркерів / Marker validation
│       ├── TechnicalStringService.cs      # Детектор технічних рядків / Technical string detector
│       ├── AutoSaveService.cs             # Автозбереження / Autosave
│       └── SimpleLogger.cs                # Логування / Logging
│
├── BF1LocalizationTool.FontGenerator/      # Генерація/ін'єкція гліфів кирилиці
│                                            # (растеризація, метричні моделі,
│                                            # донорський + no-donor конвеєри —
│                                            # FONT_FORMAT_SPEC.md §7)
│                                            # Cyrillic glyph generation/injection
│                                            # (rasterization, metric models,
│                                            # donor + no-donor pipelines)
│
├── BF1LocalizationTool.Diagnostic/         # Консольний інструмент: реверс-
│                                            # інжиніринг формату + генерація
│                                            # кириличного core.lvl (лише
│                                            # з коду, не входить у реліз) —
│                                            # FONT_FORMAT_SPEC.md
│                                            # Console tool: format reverse-
│                                            # engineering + Cyrillic
│                                            # core.lvl generation (source
│                                            # only, not part of the release)
│                                            # — see FONT_FORMAT_SPEC.md
│
└── installer/                              # Inno Setup: встановлювачі готового перекладу
    │                                        # (GameData\ + Readme.txt), поза .NET-рішенням
    │                                        # Inno Setup: end-user installers for the
    │                                        # finished translation, outside the .NET solution
    ├── bf1_installer.iss                   # Battlefront (2004) / Star Wars: Battlefront
    └── bf2_installer.iss                   # Battlefront II (2005) / Star Wars: Battlefront II
```

---

## Документація / Documentation

**UA:** Технічні деталі виправлень локалізації — по одному файлу на
тему в `docs/`. Усі шість — нові файли (жоден не замінює наявний
документ, кожен покриває свою, окрему частину):

- [`docs/BF2_UI_LAYOUT_FIX.md`](docs/BF2_UI_LAYOUT_FIX.md) — верстка
  меню під ширші роздільності: прив'язки елементів, обрізаний фон,
  вкладки, спливне вікно довідки, бойовий HUD.
- [`docs/BF2_MISSIONSELECT_LAYOUT.md`](docs/BF2_MISSIONSELECT_LAYOUT.md)
  — окремо екран «Миттєвий бій» (`ifs_missionselect` /
  `ifs_missionselect_pcMulti`): найскладніший екран меню, для якого
  загальне правило з `BF2_UI_LAYOUT_FIX.md` не сходиться до нуля
  дефектів, тому значення виміряні й задокументовані окремо.
- [`docs/BF2_FONT_SCALING.md`](docs/BF2_FONT_SCALING.md) — збільшення
  шрифту під 1080p: значення поля `HEAD` до/після на шрифт, формула
  розрахунку нової висоти, джерело кириличних гліфів.
- [`docs/BF2_MOVIE_SUBTITLE_FIX.md`](docs/BF2_MOVIE_SUBTITLE_FIX.md) —
  фікс зникнення субтитрів під заголовком відеоролика на будь-якій
  роздільності, відмінній від 4:3/5:4 (підтверджений баг оригінальної
  гри, відтворюється і на ванільних файлах).
- [`docs/BF2_SPAWNSELECT_GAP_FIX.md`](docs/BF2_SPAWNSELECT_GAP_FIX.md)
  — зазор між написом "Кількість бійців" і кнопкою "Відродження" на
  екрані вибору бійця (`ingame.lvl`), потрібен через збільшений
  кириличний шрифт.
- [`docs/BF1_TAT3_ADDON_LOCALIZATION.md`](docs/BF1_TAT3_ADDON_LOCALIZATION.md)
  — локалізація офіційного аддону Tat3 для BF1: власний `core.lvl`
  аддону та переведення назви карти на звичайний механізм `Locl`.

**EN:** Technical detail on the localization fixes — one file per
topic under `docs/`. All six are new files (none replaces an existing
document, each covers its own separate part):

- [`docs/BF2_UI_LAYOUT_FIX.md`](docs/BF2_UI_LAYOUT_FIX.md) — menu
  layout for wider resolutions: element anchors, the clipped
  background, tabs, the help popup, the combat HUD.
- [`docs/BF2_MISSIONSELECT_LAYOUT.md`](docs/BF2_MISSIONSELECT_LAYOUT.md)
  — the Instant Action screen on its own (`ifs_missionselect` /
  `ifs_missionselect_pcMulti`): the most complex menu screen, where the
  general rule from `BF2_UI_LAYOUT_FIX.md` doesn't converge to zero
  defects, so its values are measured and documented separately.
- [`docs/BF2_FONT_SCALING.md`](docs/BF2_FONT_SCALING.md) — font
  enlargement for 1080p: each font's `HEAD` field value before/after,
  the new-height formula, the Cyrillic glyph source.
- [`docs/BF2_MOVIE_SUBTITLE_FIX.md`](docs/BF2_MOVIE_SUBTITLE_FIX.md) —
  fixing the movie-title subtitle that disappears at any resolution
  other than 4:3/5:4 (a confirmed vanilla-game bug, reproducible on
  unmodified files too).
- [`docs/BF2_SPAWNSELECT_GAP_FIX.md`](docs/BF2_SPAWNSELECT_GAP_FIX.md)
  — the gap between the "Кількість бійців" label and the
  "Відродження" button on the unit-selection screen (`ingame.lvl`),
  needed because of the enlarged Cyrillic font.
- [`docs/BF1_TAT3_ADDON_LOCALIZATION.md`](docs/BF1_TAT3_ADDON_LOCALIZATION.md)
  — localizing the official Tat3 add-on for BF1: its own separate
  `core.lvl` and switching the map name to the ordinary `Locl`
  mechanism.

---

## Робочий процес / Workflow

**UA:** Оригінал і мова перекладу — обидві `english` (гра ніколи не мала
українського слоту; переклад пишеться поверх English, мови за
замовчуванням на ліцензійній копії — так само, як у SteamWorld та
Empire at War). Для кирилиці GUI відкриває файл, уже згенерований
`BF1LocalizationTool.Diagnostic` (розділ вище), а не сирий файл гри.
Переклад редагується прямо в таблиці, з фільтрами й валідатором
маркерів; для batch-перекладу через зовнішній сервіс — експорт/імпорт
CSV (`Hash,Ordinal,Original,Translation`), сумісний з будь-яким
зовнішнім інструментом, що читає й пише ці чотири колонки.

**EN:** Both the original and translation language are `english` (the
game never had a Ukrainian slot; the translation is written over
English, the default language on a licensed copy — same as in
SteamWorld and Empire at War). For Cyrillic, the GUI opens the file
already generated by `BF1LocalizationTool.Diagnostic` (section above),
not the game's raw file. Translation is edited directly in the table,
with filters and marker validation; for batch translation via an
external service — CSV export/import (`Hash,Ordinal,Original,Translation`),
compatible with any external tool that reads and writes those four
columns.

---

## Встановлення / Installation

**Вимоги / Requirements:**
- Windows 10/11
- Star Wars: Battlefront (Classic 2004) і/або Battlefront II у Steam
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
  — лише для варіанту `generic` (`win-x64`/`win-x86` самодостатні, .NET
  встановлювати не треба)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — лише
  якщо потрібна генерація кириличного `core.lvl`
  (`BF1LocalizationTool.Diagnostic` розповсюджується лише як вихідний
  код, готового `.exe` для нього нема)

**Готовий білд / Prebuilt release:**
Дивіться [Releases](https://github.com/EMP-UA/BF1LocalizationTool/releases) —
готовий `.zip` з GUI-редактором (`win-x64`/`win-x86`/`generic`). Реліз
містить лише GUI; `BF1LocalizationTool.Diagnostic` — окремо, з коду.
See [Releases](https://github.com/EMP-UA/BF1LocalizationTool/releases) for a
ready-to-run `.zip` with the GUI editor (`win-x64`/`win-x86`/`generic`).
The release contains the GUI only; `BF1LocalizationTool.Diagnostic` is
built from source separately.

**Інсталятор (Inno Setup) / Inno Setup installer:**
Інсталятор — не альтернатива WPF-редактору для лінивих, а інструмент з
іншим призначенням. WPF-редактор потрібен тим, хто хоче сам щось
виправити в тексті перекладу. Інсталятор, натомість, — перевірюваний
доказ того, що `.exe` локалізації не робить нічого зайвого: увесь його
вихідний код відкритий (скрипти нижче), а сама дія зводиться до
копіювання файлів гри й запису версії в реєстр користувача — це видно
з коду, а не лише зі слів. У `installer/` є два готові скрипти Inno
Setup — по одному на кожну гру:

- [`installer/bf1_installer.iss`](installer/bf1_installer.iss) — Star
  Wars: Battlefront (Classic, 2004), Steam AppID `1058020`.
- [`installer/bf2_installer.iss`](installer/bf2_installer.iss) — Star
  Wars: Battlefront II (Classic, 2005), Steam AppID `6060`.

Обидва скрипти самостійно шукають теку гри в Steam через реєстр Windows
(з фолбеком на типовий шлях `steamapps\common\...`), пишуть версію в
реєстр користувача для виявлення повторного встановлення й дають
однокнопкове видалення. Компілятор Inno Setup бере локалізовані файли з
теки `GameData\` поряд зі скриптом (генерується інструментом окремо —
у репозиторії її нема, оскільки це похідні файли, а не вихідний код) і
пакує їх у самостійний `.exe`.

The installer isn't an alternative to the WPF editor for people who'd
rather skip it — it serves a different purpose. The WPF editor is for
anyone who wants to edit the translation text themselves. The
installer, instead, is verifiable proof that the localization's
`.exe` does nothing extra: its full source is open (the scripts
below), and the action itself amounts to copying the game's files and
writing a version marker to the user's own registry — visible in the
code, not just claimed. `installer/` has two ready Inno Setup scripts,
one per game:

- [`installer/bf1_installer.iss`](installer/bf1_installer.iss) — Star
  Wars: Battlefront (Classic, 2004), Steam AppID `1058020`.
- [`installer/bf2_installer.iss`](installer/bf2_installer.iss) — Star
  Wars: Battlefront II (Classic, 2005), Steam AppID `6060`.

Both scripts locate the game's Steam folder on their own via the Windows
registry (falling back to the default `steamapps\common\...` path),
record the installed version in the user's registry for reinstall
detection, and provide one-click uninstall. The Inno Setup compiler
pulls the localized files from a `GameData\` folder next to the script
(generated by the tool separately — not checked into the repository,
since it's derived output rather than source) and packages them into a
self-contained `.exe`.

---

## Файли гри: що замінюється, що нове / Game files: what's replaced, what's new

**UA:**

**Battlefront (Classic, 2004):**
- **Замінюється:**
  - `Data\_LVL_PC\core.lvl` — локалізація записує в нього українські
    рядки інтерфейсу та додає кириличні гліфи (реальними Unicode-кодами,
    без заміни наявних латинських слотів) поверх ванільного файла.
    Рушій цієї гри вже коректно масштабує інтерфейс на будь-якій
    роздільності, тому правки верстки чи розміру шрифту не знадобились.
  - `GameData\AddOn\Tat3\Data\_lvl_pc\core.lvl` — окрема таблиця
    локалізації офіційного аддону Tat3 ("Jabba's Palace"): 22 власні
    нові рядки місії поверх 2369 дубльованих з бази; без власних
    шрифтів — аддон використовує вже завантажений атлас основної гри.
    Деталі — [`docs/BF1_TAT3_ADDON_LOCALIZATION.md`](docs/BF1_TAT3_ADDON_LOCALIZATION.md).
  - `GameData\AddOn\Tat3\addme.script` — назва карти аддону переведена
    зі жорстко заданого англійського рядка на звичайний ключ
    локалізації, щоб перекладатись тим самим шляхом, що й решта тексту.
- **Нових файлів немає.**

**Battlefront II (Classic, 2005):**
- **Замінюється:**
  - `Data\_lvl_pc\core.lvl` — українські рядки, кириличні гліфи (як і
    для BF1) та збільшена висота шрифтів (поле `HEAD`; значення до/після
    — [`docs/BF2_FONT_SCALING.md`](docs/BF2_FONT_SCALING.md)) для
    читабельності кирилиці на сучасних екранах.
  - `Data\_lvl_pc\shell.lvl` — прив'язки елементів меню під широкі
    екрани ([`docs/BF2_UI_LAYOUT_FIX.md`](docs/BF2_UI_LAYOUT_FIX.md),
    [`docs/BF2_MISSIONSELECT_LAYOUT.md`](docs/BF2_MISSIONSELECT_LAYOUT.md)):
    на відміну від першої частини, рушій цієї гри розрахований лише під
    800×600 і сам широкий екран не підтримує. На відміну від BF1, де
    підгонка тексту під ширину поля вирішується посимвольно (розмір
    гліфа, кернінг, підбір шрифту), у BF2 додана довжина перекладу
    найчастіше впирається не в сам текст, а в прив'язані елементи
    інтерфейсу — підкладку заголовка, сусідню кнопку, контейнер списку.
  - `Data\_lvl_pc\ingame.lvl` — та сама верстка меню налаштувань, паузи
    й лобі мережевої гри, що й у `shell.lvl` (ці екрани відкриваються і
    з головного меню, і в бою —
    [`docs/BF2_UI_LAYOUT_FIX.md`](docs/BF2_UI_LAYOUT_FIX.md) §14), а
    також зазор між написом "Кількість бійців" і кнопкою "Відродження"
    на екрані вибору бійця, щоб збільшений кириличний шрифт не
    перекривав кнопку —
    [`docs/BF2_SPAWNSELECT_GAP_FIX.md`](docs/BF2_SPAWNSELECT_GAP_FIX.md).
- **Новий файл:** `d3d9.dll` — ставиться в теку гри поряд із
  `BattlefrontII.exe` (стандартний порядок пошуку DLL у Windows: тека
  застосунку перевіряється раніше за System32). Виправляє зникнення
  субтитрів відеороликів на будь-якій роздільності, відмінній від
  4:3/5:4 — деталі й підтвердження в реальній грі —
  [`docs/BF2_MOVIE_SUBTITLE_FIX.md`](docs/BF2_MOVIE_SUBTITLE_FIX.md).
  Перед перезаписом чужого файла з такою назвою робиться резервна копія
  (`d3d9.dll.bf1backup`); під час роботи пише лог
  `bf2_widescreen_fix.log` у тій самій теці — без жодних мережевих
  з'єднань; про вміст і призначення логу —
  [`tools/bf2_d3d9_widescreen_fix/README.md`](tools/bf2_d3d9_widescreen_fix/README.md).

**EN:**

**Battlefront (Classic, 2004):**
- **Replaced:**
  - `Data\_LVL_PC\core.lvl` — the localization writes Ukrainian
    interface strings into it and adds Cyrillic glyphs (as real Unicode
    code points, with no existing Latin slot replaced) on top of the
    vanilla file. This game's engine already scales the interface
    correctly at any resolution, so no layout or font-size changes were
    needed.
  - `GameData\AddOn\Tat3\Data\_lvl_pc\core.lvl` — a separate
    localization table for the official Tat3 add-on ("Jabba's
    Palace"): 22 of its own new mission strings on top of 2369 entries
    duplicated from the base table; no fonts of its own — the add-on
    reuses the base game's already-loaded atlas. Details —
    [`docs/BF1_TAT3_ADDON_LOCALIZATION.md`](docs/BF1_TAT3_ADDON_LOCALIZATION.md).
  - `GameData\AddOn\Tat3\addme.script` — the add-on's map name is
    switched from a hardcoded English string to an ordinary
    localization key, so it translates through the same path as
    everything else.
- **No new files.**

**Battlefront II (Classic, 2005):**
- **Replaced:**
  - `Data\_lvl_pc\core.lvl` — Ukrainian strings, Cyrillic glyphs (same
    as BF1), and enlarged font height (the `HEAD` field; before/after
    values in
    [`docs/BF2_FONT_SCALING.md`](docs/BF2_FONT_SCALING.md)) for Cyrillic
    readability on modern screens.
  - `Data\_lvl_pc\shell.lvl` — menu element anchors for wide screens
    ([`docs/BF2_UI_LAYOUT_FIX.md`](docs/BF2_UI_LAYOUT_FIX.md),
    [`docs/BF2_MISSIONSELECT_LAYOUT.md`](docs/BF2_MISSIONSELECT_LAYOUT.md)):
    unlike the first game, this engine is built for 800x600 only and has
    no native widescreen support. Unlike BF1, where fitting text to a
    field's width is resolved per character (glyph size, kerning, font
    selection), in BF2 the added length of a translation most often runs
    into bound interface elements — a title backdrop, a neighboring
    button, a list container — rather than the text itself.
  - `Data\_lvl_pc\ingame.lvl` — the same options-menu, pause-menu, and
    multiplayer-lobby layout as `shell.lvl` (these screens open both
    from the main menu and during battle —
    [`docs/BF2_UI_LAYOUT_FIX.md`](docs/BF2_UI_LAYOUT_FIX.md) §14), plus
    a gap between the "Кількість бійців" label and the "Відродження"
    button on the unit-selection screen, so the enlarged Cyrillic font
    doesn't cover the button —
    [`docs/BF2_SPAWNSELECT_GAP_FIX.md`](docs/BF2_SPAWNSELECT_GAP_FIX.md).
- **New file:** `d3d9.dll` — placed in the game folder next to
  `BattlefrontII.exe` (the standard Windows DLL search order: the
  application folder is checked before System32). Fixes movie subtitles
  disappearing at any resolution other than 4:3/5:4 — details and
  in-game confirmation in
  [`docs/BF2_MOVIE_SUBTITLE_FIX.md`](docs/BF2_MOVIE_SUBTITLE_FIX.md).
  Before overwriting an existing file of the same name, a backup is made
  (`d3d9.dll.bf1backup`); at runtime it writes a `bf2_widescreen_fix.log`
  log file in the same folder — with no network connections of any
  kind; for the log's contents and purpose, see
  [`tools/bf2_d3d9_widescreen_fix/README.md`](tools/bf2_d3d9_widescreen_fix/README.md).

---

## Подяки / Credits

- Спільнота [Gametoast](https://gametoast.com/) — документація форматів
- [swbf-unmunge](https://github.com/PrismaticFlower/swbf-unmunge) — початковий аналіз структури ucfb
- **[Sofia Sans](https://fonts.google.com/specimen/Sofia+Sans+Extra+Condensed)**
  (Lettersoup — Botio Nikoltchev, Ani Petrova) — шрифт для рендеру
  кириличних гліфів BF1 (`Sofia Sans Extra Condensed`).
- **[Unbounded](https://fonts.google.com/specimen/Unbounded)** (Web3
  Foundation / Studio Koto / NaN / Parity Technologies — Luke Prowse,
  Jean-Baptiste Morizot, Fátima Lazaro, Florian Runge) і **[Exo 2](https://fonts.google.com/specimen/Exo+2)**
  (Natanael Gama) — шрифти для рендеру кириличних гліфів BF2.

  **UA:** усі три — **SIL Open Font License**, вільно доступні на Google
  Fonts. **Цей репозиторій НЕ містить `.ttf`-файлів** (вони публічні,
  і посилання вище ведуть напряму до джерела) — точний перелік
  потрібних файлів і куди їх класти: `FONT_FORMAT_SPEC.md` §7.6.
  Обрані навмисно замість системного `Bahnschrift` (заборона
  розповсюдження).
  **EN:** all three are **SIL Open Font License**, freely available on
  Google Fonts. **This repository does NOT bundle `.ttf` files** (they
  are public, and the links above go straight to the source) — the
  exact list of files and where to put them is in
  `FONT_FORMAT_SPEC.md` §7.6. Chosen deliberately instead of the
  system `Bahnschrift` font (redistribution forbidden).

  Який файл рендерить який ігровий шрифт / Which file renders which in-game font:

  | Гра / Game | Розмір / Size | Файл / File |
  |---|---|---|
  | BF1 | усі 5 розмірів / all 5 sizes | `SofiaSansExtraCondensed-Bold.ttf` |
  | BF2 | `gamefont_large` | `Unbounded-Bold.ttf` |
  | BF2 | `gamefont_medium` | `Unbounded-Black.ttf` |
  | BF2 | `gamefont_small` | `Unbounded-ExtraBold.ttf` |
  | BF2 | `gamefont_tiny` / `gamefont_super_tiny` | `Exo2-ExtraBold.ttf` |

---

## 💜 Підтримка / Support the Project

**UA:** Якщо цей інструмент виявився корисним — підтримати можна тут:
**EN:** If you find this tool useful — support is appreciated:

- ☕ [Ko-fi](https://ko-fi.com/emp_ua) — **EN:** International
- 🏦 [Monobank](https://send.monobank.ua/jar/7PnVgizntU) — **UA:** Україна
- 💳 [StreamElements](https://streamelements.com/emp_ua/tip) — PayPal

---

## 📺 Автор / Author

**EMP_UA** — **UA:** Український контент-мейкер та локалізатор ігор. **EN:** Ukrainian content creator & game localizer.
[YouTube](https://www.youtube.com/@EMPs_UA) • [Twitch](https://www.twitch.tv/emp_ua) • [Discord](https://discord.gg/QdmgsCgPkp) • [Telegram](https://t.me/EMP_UA) • [Website](https://emp-ua.com)

---

### ⚖️ Copyright Note / Примітка щодо авторських прав

**UA:** Увесь код і скрипти в цьому репозиторії — авторська робота, надана виключно для некомерційного використання фанатами та для технічної прозорості перед майданчиками модів (напр. Nexus Mods). Оригінальні активи, тексти та бінарні формати гри належать Pandemic Studios / LucasArts; цей репозиторій не містить жодних видобутих файлів гри.

**EN:** All code and scripts in this repository are original work, provided solely for non-commercial fan use and for technical transparency toward mod platforms (e.g. Nexus Mods). The original assets, text, and binary formats belong to Pandemic Studios / LucasArts; this repository contains no extracted game files.

---

*© EMP_UA — MIT License*
