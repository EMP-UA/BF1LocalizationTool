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

**UA:** Сам по собі `.txt`-формат (Latin1) не підтримує кирилицю для BF1.
Підтримка додається окремим кроком, який ін'єктує нові гліфи кирилиці
напряму в шрифтовий атлас `core.lvl`: `BF1LocalizationTool.Diagnostic`
вміє це робити (реальні Unicode-коди, без обрізання жодного англійського
гліфа) — докладно в [`FONT_FORMAT_SPEC.md`](FONT_FORMAT_SPEC.md), розділ
7. Після ін'єкції гліфів GUI пише український текст як звичайний
UTF-16LE, і гра відображає його напряму.

**Обов'язковий порядок дій, щоб кирилиця запрацювала:**
0. Завантажити **5 файлів** шрифтів (SIL OFL, безкоштовно, з Google
   Fonts — [Sofia Sans](https://fonts.google.com/specimen/Sofia+Sans+Extra+Condensed),
   [Unbounded](https://fonts.google.com/specimen/Unbounded),
   [Exo 2](https://fonts.google.com/specimen/Exo+2): кнопка «Download
   family», потрібні файли лежать у теці `static\` архіву) і покласти
   їх **напряму** (без підтек) у `BF1LocalizationTool.Diagnostic\Fonts\`,
   з іменами рівно такими:
   `SofiaSansExtraCondensed-Bold.ttf`, `Unbounded-Bold.ttf`,
   `Unbounded-Black.ttf`, `Unbounded-ExtraBold.ttf`, `Exo2-ExtraBold.ttf`.
   Ці файли **не входять у репозиторій** (публічно доступні на Google
   Fonts, завантажуються окремо). Шрифт **НЕ** береться із системного
   реєстру Windows — деталі `FONT_FORMAT_SPEC.md` §7.6.
1. **Diagnostic tool** → категорія 7 (⚠ ГЕНЕРАЦІЯ) → пункт **2**
   (★ БЕЗ ДОНОРІВ: згенерувати кириличний core.lvl прямими Unicode-кодами)
   → вибір гри (BF2 автоматично отримує ще й збільшення шрифту в тому ж
   кроці — BF2 не підтримує 1080p нативно, BF1 підтримує).
2. Отриманий `output-nodonor\...\core.lvl` відкрити в GUI як "Оригінал" і
   "Робочий файл" — попередження про "відсутні кириличні гліфи" в цьому
   випадку хибне (гліфи вже додані на кроці 1), можна ігнорувати.
3. Перекладати/імпортувати CSV як зазвичай (розділ "Робочий процес"
   нижче) — далі усе працює як зі звичайним `core.lvl`.

**Аддон Tat3 (BF1, «Палац Джабби») — окремий прохід.** Аддон має ВЛАСНИЙ
`core.lvl` з власною таблицею тексту: 2391 рядок дублює базову гру і лише
**22 унікальні** (назви кімнат, точки захоплення, цілі місії). Шрифтів він
не містить і не повинен — бере атлас базової гри. Порядок:
1. **Diagnostic** → категорія 7 → пункт **9** («★ АДДОН Tat3: зробити назву
   карти перекладною») — інакше назва карти в списку лишиться англійською
   (вона живе в `addme.script`, поза таблицею тексту; деталі —
   [`FONT_FORMAT_SPEC.md`](FONT_FORMAT_SPEC.md) §8).
   **Запускати ПІСЛЯ генерації кирилиці (кат. 7 → пункт 2) і ДО перекладу в GUI** —
   команда спитає шлях до БАЗОВОГО `core.lvl`, бо саме проти базової
   таблиці шел резолвить назви карт (§8.3). Новий рядок після цього
   з'явиться в GUI як звичайний неперекладений.
2. Тека `output-addon\` дзеркалить теку гри
   (`output-addon\GameData\Data\_LVL_PC\core.lvl` — базовий,
   `output-addon\GameData\AddOn\Tat3\...\core.lvl` — аддонний;
   `addme.script` там-таки, копіюється як є). Кожен `core.lvl` окремо
   відкрити в GUI як **«Оригінал»**, далі **«Новий робочий з оригіналу»**
   (НЕ «Відкрити робочий»!).
3. «Імпорт CSV» з перекладу базової гри — підтягне 2391 спільний рядок за
   хешем; лишиться доперекласти 22 унікальні.
4. Зберегти. Результат має важити **~1 МБ і не містити шрифтів**; якщо
   вийшло ~4.9 МБ — узято не той робочий файл (GUI попереджає про це).

**Повна відповідь «де лежить текст»:** увесь текст BF1 — рівно у двох
файлах (`Data\_LVL_PC\core.lvl` і `AddOn\Tat3\...\core.lvl`), шрифти —
лише в першому. Перевірено суцільним скануванням усієї інсталяції;
див. §8.

**Точність гліфів і відомі обмеження (див. `FONT_FORMAT_SPEC.md`
§7.5, §7.7–§7.8):** метрична модель "ядро+виступ" для малих літер
(§7.5) і порядок кроків BF2-збільшення (§7.7) верифіковані побайтовим
сканом ПОВНОГО алфавіту (усі 5 розмірів, обидві гри) — §7.8; рендер
коректний в обох іграх, без "рамки" навколо великих літер BF1 чи
"плаваючих" (не на базовій лінії) `і`/`ї`/`й` у BF2. `LvlLocalizationService.SaveAsync`
завжди бере шрифти з ОРИГІНАЛЬНОГО файлу, а не з робочого
(`AdoptFontsFrom`) — кількість пересаджених гліфів видно в статус-рядку
GUI; саме тому кожен робочий процес вимагає окремо відкритий "Оригінал".
Назва карти аддону Tat3 — єдиний рядок BF1 поза `Locl` (§8) — так само
перекладна й відображається українською.

Два відкритих обмеження: коефіцієнт збільшення шрифту BF2 (×1,5) підібраний
емпірично і не є доведено оптимальним; окрема підсистема "BF2 widescreen"
(коректна ГЕОМЕТРІЯ/верстка меню під 1080p, на відміну від просто розміру
шрифту) існує в коді, але позначена як експериментальна і не під'єднана до
основного робочого процесу — за замовчуванням застосовується лише
збільшення розміру шрифту, без переверстки геометрії меню.

**EN:** The `.txt` format (Latin1) does not support Cyrillic for BF1 on
its own. Support is added by a separate step that injects new Cyrillic
glyphs directly into the `core.lvl` font atlas:
`BF1LocalizationTool.Diagnostic` does this (real Unicode code points, no
existing English glyph is overwritten) — details in
[`FONT_FORMAT_SPEC.md`](FONT_FORMAT_SPEC.md), section 7. After the
glyphs are injected, the GUI writes Ukrainian text as ordinary UTF-16LE,
and the game renders it directly.

**Required steps for Cyrillic to work:**
0. Download **5 font files** (SIL OFL, free, from Google Fonts —
   [Sofia Sans](https://fonts.google.com/specimen/Sofia+Sans+Extra+Condensed),
   [Unbounded](https://fonts.google.com/specimen/Unbounded),
   [Exo 2](https://fonts.google.com/specimen/Exo+2): "Download family"
   button, the needed files are in the archive's `static\` folder) and
   place them **directly** (no subfolders) in
   `BF1LocalizationTool.Diagnostic\Fonts\`, named exactly:
   `SofiaSansExtraCondensed-Bold.ttf`, `Unbounded-Bold.ttf`,
   `Unbounded-Black.ttf`, `Unbounded-ExtraBold.ttf`, `Exo2-ExtraBold.ttf`.
   These files are **not included in this repository** (publicly
   available on Google Fonts, downloaded separately). The font is
   **NOT** taken from the Windows system registry — details in
   `FONT_FORMAT_SPEC.md` §7.6.
1. **Diagnostic tool** → category 7 (⚠ GENERATION) → item **2**
   (★ NO DONORS: generate a Cyrillic core.lvl with direct Unicode codes)
   → pick the game (BF2 automatically also gets font enlargement in the
   same step — BF2 doesn't support 1080p natively, BF1 does).
2. Open the resulting `output-nodonor\...\core.lvl` in the GUI as
   "Original" and "Working file" — the "missing Cyrillic glyphs" warning
   is a false alarm here (glyphs were already added in step 1), safe to
   ignore.
3. Translate/import CSV as usual (see "Workflow" below) — everything else
   works like a normal `core.lvl`.

**Tat3 add-on (BF1, "Jabba's Palace") — a separate pass.** The add-on
ships its OWN `core.lvl` with its own text table: 2391 strings duplicate
the base game and only **22 are unique** (room names, capture points,
mission objectives). It carries no fonts and shouldn't — it uses the base
game's atlas. Order:
1. **Diagnostic** → category 7 → item **9** ("★ Tat3 ADD-ON: make the map
   name translatable") — otherwise the map name in the list stays English
   (it lives in `addme.script`, outside the text table; details —
   [`FONT_FORMAT_SPEC.md`](FONT_FORMAT_SPEC.md) §8). **Run AFTER Cyrillic
   generation (category 7 → item 2) and BEFORE translating in the GUI** —
   the command asks for the BASE `core.lvl` path, since the shell resolves
   map names against the base table (§8.3). The new string then shows
   up in the GUI as an ordinary untranslated row.
2. The `output-addon\` folder mirrors the game folder
   (`output-addon\GameData\Data\_LVL_PC\core.lvl` — base,
   `output-addon\GameData\AddOn\Tat3\...\core.lvl` — add-on;
   `addme.script` sits alongside, copy it as-is). Open each `core.lvl`
   separately in the GUI as **"Original"**, then **"New working from
   original"** (NOT "Open working file"!).
3. "Import CSV" from the base game's translation — pulls in the 2391
   shared strings by hash; only the 22 unique ones remain to translate.
4. Save. The result should weigh **~1 MB and carry no fonts**; if it came
   out ~4.9 MB, the wrong working file was picked (the GUI warns about
   this).

**The full answer to "where does the text live":** all of BF1's text sits
in exactly two files (`Data\_LVL_PC\core.lvl` and
`AddOn\Tat3\...\core.lvl`), fonts only in the first. Verified by scanning
the entire installation; see §8.

**Glyph accuracy and known limitations (see `FONT_FORMAT_SPEC.md`
§7.5, §7.7–§7.8):** the lowercase "core+extension" metric model (§7.5)
and the BF2 enlargement step order (§7.7) are verified via a byte-level
scan of the FULL alphabet (all 5 sizes, both games) — §7.8; rendering is
correct in both games, with no "frame" around BF1 capitals and no BF2
і/ї/й floating off the shared baseline.
`LvlLocalizationService.SaveAsync` always adopts fonts from the ORIGINAL
file, not the working file (`AdoptFontsFrom`) — the count of adopted
glyphs is surfaced in the GUI status bar, which is why every workflow
requires a separately opened "Original". The Tat3 add-on's map name —
BF1's only string outside `Locl` (§8) — is likewise translatable and
renders in Ukrainian.

Two open limitations remain: the BF2 font enlargement factor (×1.5) was
chosen empirically and isn't proven optimal; a separate "BF2 widescreen"
subsystem (correct menu LAYOUT/geometry for 1080p, distinct from just
font size) exists in the code but is marked experimental and isn't wired
into the main workflow — only font size scaling is applied by default,
without adjusting menu geometry.

---

## Можливості GUI / GUI features

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
│   └── Models/
│       ├── LocalizationEntry.cs           # Один рядок / Single string
│       └── LocalizationFile.cs            # Один мовний файл / One language file
│
├── BF1LocalizationTool.GUI/                # WPF інтерфейс / WPF interface
│   ├── App.xaml / MainWindow.xaml(.cs)     # UI, EntryRow (статуси, поріг довжини)
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
                                             # інжиніринг формату + PRODUCTION
                                             # генерація кириличного core.lvl
                                             # (категорія меню 7) — див.
                                             # FONT_FORMAT_SPEC.md
                                             # Console tool: format reverse-
                                             # engineering + PRODUCTION Cyrillic
                                             # core.lvl generation (menu
                                             # category 7) — see
                                             # FONT_FORMAT_SPEC.md
```

---

## Робочий процес / Workflow

### Ручний переклад / Manual translation
1. Відкрити `GameData/Data/_LVL_PC/core.lvl` (BF1 або BF2) — **для
   кирилиці спершу згенерувати шрифт через Diagnostic tool**, див. розділ
   "Кирилиця в `.txt`-каналі" вище, і відкрити ЗГЕНЕРОВАНИЙ файл, не
   сирий файл гри
2. Оригінал і мова перекладу — обидві `english` (гра ніколи не мала
   українського слоту; переклад пишеться поверх English, мови за
   замовчуванням на ліцензійній копії — так само, як у SteamWorld та
   Empire at War)
3. Редагувати рядки в колонці «Переклад» прямо в таблиці
4. Скористатись фільтрами і валідатором маркерів для перевірки
5. Зберегти модифікований `core.lvl`

### Batch-переклад через зовнішній сервіс / Batch translation via external service
1. Відкрити `core.lvl`
2. Експортувати CSV (`⬆ Експорт CSV`, формат `Hash,Ordinal,Original,Translation`)
3. Перекласти CSV будь-яким зовнішнім засобом. Один із варіантів —
   окремий локальний консольний інструмент (Gemini Batch API, з
   глосарієм і валідацією технічних маркерів), що **не входить у цей
   репозиторій** і не є частиною публічної збірки; CSV-формат
   експорту/імпорту сумісний з будь-яким інструментом, що читає й пише
   ці чотири колонки
4. Імпортувати CSV з перекладами (`⬇ Імпорт CSV`)
5. Перевірити валідатором і зберегти

---

## Встановлення / Installation

**Вимоги / Requirements:**
- Windows 10/11
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Star Wars: Battlefront (Classic 2004) і/або Battlefront II у Steam

**Готовий білд / Prebuilt release:**
Дивіться [Releases](https://github.com/EMP-UA/BF1LocalizationTool/releases) —
готовий `.zip` з `BF1LocalizationTool.exe`.
See [Releases](https://github.com/EMP-UA/BF1LocalizationTool/releases) for a
ready-to-run `.zip` with `BF1LocalizationTool.exe`.

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

  UA: усі три — **SIL Open Font License**, вільно доступні на Google
  Fonts. **Цей репозиторій НЕ містить `.ttf`-файлів** (вони публічні,
  і посилання вище ведуть напряму до джерела) — конкретний перелік
  потрібних файлів і куди їх покласти див. у розділі «Кирилиця в
  `.txt`-каналі» нижче та `FONT_FORMAT_SPEC.md` §7.6. Обрані навмисно
  замість системного `Bahnschrift` (заборона розповсюдження).
  EN: all three are **SIL Open Font License**, freely available on
  Google Fonts. **This repository does NOT bundle `.ttf` files** (they
  are public, and the links above go straight to the source) — see the
  "Cyrillic in the `.txt` channel" section below and
  `FONT_FORMAT_SPEC.md` §7.6 for the exact list of files and where to
  put them. Chosen deliberately instead of the system `Bahnschrift`
  font (redistribution forbidden).

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
[YouTube](https://www.youtube.com/@EMPs_UA) • [Twitch](https://www.twitch.tv/emp_ua) • [Discord](https://discord.gg/QdmgsCgPkp) • [Telegram](https://t.me/EMP_UA) • [Website](https://emp-ua-site.pages.dev)

---

### ⚖️ Copyright Note / Примітка щодо авторських прав

**UA:** Увесь код і скрипти в цьому репозиторії — авторська робота, надана виключно для некомерційного використання фанатами та для технічної прозорості перед майданчиками модів (напр. Nexus Mods). Оригінальні активи, тексти та бінарні формати гри належать Pandemic Studios / LucasArts; цей репозиторій не містить жодних видобутих файлів гри.

**EN:** All code and scripts in this repository are original work, provided solely for non-commercial fan use and for technical transparency toward mod platforms (e.g. Nexus Mods). The original assets, text, and binary formats belong to Pandemic Studios / LucasArts; this repository contains no extracted game files.

---

*© EMP_UA — MIT License*
