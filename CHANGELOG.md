# Changelog

Формат базується на [Keep a Changelog](https://keepachangelog.com/uk/1.1.0/).
Based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### UA: Додано
- Перенесення перекладу між іграми (BF1 ↔ BF2) за збігом англійського
  оригіналу — кнопка «⇄ BF1↔BF2» у GUI
- Захист від перезапису — лише за позначкою вичитки (ReviewStatus), не за
  фактом наявності перекладу: рядки без цієї позначки (включно з уже
  перекладеними Gemini) можуть бути перезаписані донором
- Вікно вирішення конфліктів (`TranslationConflictWindow`) для рядків, де
  донор має кілька різних перекладів одного англійського оригіналу

---

### EN: Added
- Cross-game translation transfer (BF1 ↔ BF2) matching by English
  original — the "⇄ BF1↔BF2" GUI button
- Overwrite protection based solely on the review mark (ReviewStatus),
  not on whether a translation is present: rows without that mark
  (including ones already translated by Gemini) may be overwritten by
  the donor
- Conflict-resolution window (`TranslationConflictWindow`) for strings
  where the donor has several different translations of the same
  English original

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

