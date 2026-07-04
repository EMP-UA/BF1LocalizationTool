# BF1LocalizationTool

> Інструмент для локалізації Star Wars: Battlefront (Classic 2004) та Battlefront II (2005)
> Localization editor for Star Wars: Battlefront (Classic 2004) and Battlefront II (2005)

[![License: MIT](https://img.shields.io/badge/License-MIT-8A46C1.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-C989F3.svg)](https://github.com/EMP-UA/BF1LocalizationTool)
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

## ⚠️ Відоме обмеження / Known limitation

**UA:** Plain-text `.txt` формат (BF1, і частково BF2) використовує Latin1 —
**кирилиця не підтримується** і буде замінена на `?` при збереженні.
Інструмент попереджає про це перед записом. Повноцінна українська локалізація
BF1 через `.txt`-канал наразі неможлива без окремої роботи над шрифтовим
атласом гри (заміна гліфів + бітмапінг, за аналогією з проєктом для
SteamWorld Heist). Для BF2 рекомендується бінарний `Locl`-канал (UTF-16LE) —
він коректно зберігає кирилицю без обмежень.

**EN:** The plain-text `.txt` format (BF1, and partly BF2) uses Latin1 —
**Cyrillic is not supported** and will be replaced with `?` on save. The tool
warns before writing. Full Ukrainian localization of BF1 via the `.txt`
channel currently requires separate work on the game's font atlas (glyph
replacement + bitmap mapping, similar to the SteamWorld Heist project). For
BF2, use the binary `Locl` channel (UTF-16LE) instead — it stores Cyrillic
correctly with no limitations.

---

## Можливості GUI / GUI features

- 🎨 Темна/світла тема (dark/light theme)
- 🔍 Фільтри рядків за мовою, статусом перекладу, технічністю
- ✅ **ValidationService** — перевірка збереження технічних маркерів
  (`%s %d %i %f %c %u`, `{btn...}`, `[X]`, `\n \t`) в перекладі
- 🤖 **TechnicalStringService** — автовизначення рядків, що не потребують
  перекладу (URL, числа, ідентифікатори `WORD_WORD`/`word_word`, назви клавіш,
  copyright-текст тощо)
- 💾 **AutoSaveService** — автозбереження прогресу перекладу
- 📝 **SimpleLogger** — логування дій для діагностики
- 📤 Експорт/імпорт CSV для batch-перекладу через зовнішні сервіси

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
│   ├── App.xaml / MainWindow.xaml(.cs)     # UI
│   └── Services/
│       ├── ValidationService.cs           # Перевірка маркерів / Marker validation
│       ├── TechnicalStringService.cs      # Детектор технічних рядків / Technical string detector
│       ├── AutoSaveService.cs             # Автозбереження / Autosave
│       └── SimpleLogger.cs                # Логування / Logging
│
└── BF1LocalizationTool.Diagnostic/         # Консольний аналізатор .loc/.lvl
                                             # для налагодження парсера
                                             # Console .loc/.lvl byte-structure
                                             # analyzer used during parser development
```

---

## Робочий процес / Workflow

### Ручний переклад / Manual translation
1. Відкрити `GameData/Data/_LVL_PC/core.lvl` (BF1 або BF2)
2. Вибрати оригінал (`english`) і мову перекладу (`uk_english` тощо)
3. Редагувати рядки в колонці «Переклад» прямо в таблиці
4. Скористатись фільтрами і валідатором маркерів для перевірки
5. Зберегти модифікований `core.lvl`

### Batch-переклад через зовнішній сервіс / Batch translation via external service
1. Відкрити `core.lvl`
2. Експортувати CSV (`⬆ Експорт CSV`)
3. Перекласти CSV (окремий інструмент — Gemini Batch API, у розробці)
4. Імпортувати CSV з перекладами (`⬇ Імпорт CSV`)
5. Перевірити валідатором і зберегти

---

## Встановлення / Installation

**Вимоги / Requirements:**
- Windows 10/11
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Star Wars: Battlefront (Classic 2004) і/або Battlefront II у Steam

**Зборка з вихідного коду / Build from source:**
```bash
git clone https://github.com/EMP-UA/BF1LocalizationTool
cd BF1LocalizationTool
dotnet build -c Release
```

**Готовий білд / Prebuilt release:**
Дивіться [Releases](https://github.com/EMP-UA/BF1LocalizationTool/releases) —
готовий `.zip` з `BF1LocalizationTool.exe`.
See [Releases](https://github.com/EMP-UA/BF1LocalizationTool/releases) for a
ready-to-run `.zip` with `BF1LocalizationTool.exe`.

---

## Подяки / Credits

- Спільнота [Gametoast](https://gametoast.com/) — документація форматів
- [swbf-unmunge](https://github.com/PrismaticFlower/swbf-unmunge) — початковий аналіз структури ucfb

---

## Підтримати / Support

[![Ko-fi](https://img.shields.io/badge/Ko--fi-EMP__UA-8A46C1?logo=ko-fi)](https://ko-fi.com/emp_ua)
[![Twitch](https://img.shields.io/badge/Twitch-EMP__UA-8A46C1?logo=twitch)](https://twitch.tv/emp_ua)

---

*© EMP_UA — MIT License*
