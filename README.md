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
└── installer/                              # Inno Setup: встановлювач готового
                                             # перекладу (GameData\ + Readme.txt)
                                             # для кінцевого користувача гри,
                                             # поза .NET-рішенням інструменту
                                             # Inno Setup: end-user installer
                                             # for the finished translation
                                             # (GameData\ + Readme.txt),
                                             # outside the tool's .NET solution
```

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
  і посилання вище ведуть напряму до джерела) — точний перелік
  потрібних файлів і куди їх класти: `FONT_FORMAT_SPEC.md` §7.6.
  Обрані навмисно замість системного `Bahnschrift` (заборона
  розповсюдження).
  EN: all three are **SIL Open Font License**, freely available on
  Google Fonts. **This repository does NOT bundle `.ttf` files** (they
  are public, and the links above go straight to the source) — the
  exact list of files and where to put them is in
  `FONT_FORMAT_SPEC.md` §7.6. Chosen deliberately instead of the
  system `Bahnschrift` font (redistribution forbidden).

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
