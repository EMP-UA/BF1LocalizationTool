# Changelog

Формат базується на [Keep a Changelog](https://keepachangelog.com/uk/1.0.0/).
Based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [1.0.0] — 2026-07-04

### UA: Додано
- Читання/запис `core.lvl` напряму через власний `UcfbReader`/`UcfbWriter`
  (ucfb chunk-based формат Pandemic Studios, 4-байтове вирівнювання)
- Підтримка бінарного `Locl`-формату (UTF-16LE) для Battlefront II
- Підтримка plain-text `0xHASH text` формату (Latin1) для BF1 і BF2
- Автовизначення версії гри (BF1/BF2) за шляхом до файлу
- WPF GUI: темна/світла тема, фільтри, таблиця редагування перекладу
- `ValidationService` — перевірка збереження технічних маркерів (`%s`, `{btn...}` тощо)
- `TechnicalStringService` — автовизначення рядків, що не потребують перекладу
- `AutoSaveService` — автоматичне збереження прогресу
- Експорт/імпорт CSV для зовнішнього batch-перекладу
- `BF1LocalizationTool.Diagnostic` — консольний аналізатор байтової структури `.loc`/`.lvl`

### UA: Відомі обмеження
- Plain-text `.txt`-канал (BF1, частково BF2) не підтримує кирилицю (Latin1)

---

### EN: Added
- Direct `core.lvl` read/write via custom `UcfbReader`/`UcfbWriter`
  (Pandemic Studios ucfb chunk format, 4-byte alignment)
- Binary `Locl` format support (UTF-16LE) for Battlefront II
- Plain-text `0xHASH text` format support (Latin1) for BF1 and BF2
- Automatic game version detection (BF1/BF2) based on file path
- WPF GUI: dark/light theme, filters, translation editing table
- `ValidationService` — checks technical markers are preserved (`%s`, `{btn...}`, etc.)
- `TechnicalStringService` — auto-detects strings that don't need translation
- `AutoSaveService` — automatic translation progress saving
- CSV export/import for external batch translation
- `BF1LocalizationTool.Diagnostic` — console byte-structure analyzer for `.loc`/`.lvl`

### EN: Known limitations
- Plain-text `.txt` channel (BF1, partly BF2) does not support Cyrillic (Latin1)
