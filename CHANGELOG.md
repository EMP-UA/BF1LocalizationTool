# Changelog

Формат базується на [Keep a Changelog](https://keepachangelog.com/uk/1.0.0/).
Based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

## [1.0.0] — 2026-08-04

### UA: Додано
- Читання/запис `core.lvl` напряму через власний `UcfbReader`/`UcfbWriter`
  (ucfb chunk-based формат Pandemic Studios, 4-байтове вирівнювання)
- Підтримка бінарного `Locl`-формату (UTF-16LE) для Battlefront II
- Підтримка plain-text `0xHASH text` формату (Latin1) для BF1 і BF2
- Автовизначення версії гри (BF1/BF2) за шляхом до файлу
- Генерація кириличного `core.lvl` реальними Unicode-кодами, без донорських
  символів (`FONT_FORMAT_SPEC.md` §7)
- Автоматичне збільшення шрифту для BF2 (без нативної підтримки 1080p)
- Переклад назви карти аддону Tat3 (патч `addme.script` + новий запис `Locl`)
- WPF GUI: темна/світла тема, кольорове підсвічування рядків за статусом
  перекладу, фільтри, таблиця редагування перекладу
- Виявлення дублікатів оригінального тексту й перевірка узгодженості
  перекладу між дублями
- Вікно порівняння файлів (`CompareWindow`)
- `ValidationService` — перевірка збереження технічних маркерів (`%s`, `{btn...}` тощо)
- `TechnicalStringService` — автовизначення рядків, що не потребують перекладу
- `AutoSaveService` — автоматичне збереження прогресу
- Експорт/імпорт CSV для зовнішнього batch-перекладу
- `BF1LocalizationTool.Diagnostic` — консольний аналізатор байтової
  структури `.loc`/`.lvl` і генератор кириличних шрифтів

### UA: Відомі обмеження
- Коефіцієнт збільшення шрифту BF2 (×1.5) підібраний емпірично, не
  гарантовано оптимальний для кожного UI-екрана
- Підсистема "BF2 widescreen" (верстка меню під 1080p) — експериментальна,
  не інтегрована в основний робочий процес

---

### EN: Added
- Direct `core.lvl` read/write via custom `UcfbReader`/`UcfbWriter`
  (Pandemic Studios ucfb chunk format, 4-byte alignment)
- Binary `Locl` format support (UTF-16LE) for Battlefront II
- Plain-text `0xHASH text` format support (Latin1) for BF1 and BF2
- Automatic game version detection (BF1/BF2) based on file path
- Cyrillic `core.lvl` generation with real Unicode code points, no donor
  characters (`FONT_FORMAT_SPEC.md` §7)
- Automatic font enlargement for BF2 (no native 1080p support)
- Translatable Tat3 add-on map name (patches `addme.script` + a new
  `Locl` record)
- WPF GUI: dark/light theme, whole-row color coding by translation
  status, filters, translation editing table
- Duplicate original-text detection and consistency checking across
  duplicate translations
- File comparison window (`CompareWindow`)
- `ValidationService` — checks technical markers are preserved (`%s`, `{btn...}`, etc.)
- `TechnicalStringService` — auto-detects strings that don't need translation
- `AutoSaveService` — automatic translation progress saving
- CSV export/import for external batch translation
- `BF1LocalizationTool.Diagnostic` — console byte-structure analyzer for
  `.loc`/`.lvl` and Cyrillic font generator

### EN: Known limitations
- The BF2 font enlargement factor (×1.5) was chosen empirically and
  isn't guaranteed optimal for every UI screen
- The "BF2 widescreen" subsystem (1080p menu layout) is experimental
  and not integrated into the main workflow
