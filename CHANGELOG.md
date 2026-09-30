# Changelog

Формат базується на [Keep a Changelog](https://keepachangelog.com/uk/1.1.0/).
Based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.1.0] — 2026-09-30

### UA: Додано
- Перенесення перекладу між іграми (BF1 ↔ BF2) за збігом англійського
  оригіналу — кнопка «⇄ BF1↔BF2» у GUI; донор задається двома файлами
  (спершу ванільний, потім перекладений), щоб коректно розпізнавати
  переклад і тоді, коли гра-донор пише його в ту саму мовну секцію,
  що й оригінал (BF1)
- Збіг оригіналів — без урахування регістру й пробілів (англійський
  текст BF1 зберігається ВЕЛИКИМИ, BF2 — звичайним регістром); регістр
  перенесеного перекладу узгоджується з рядком цільової гри
  (`TranslationCaseAdapter`)
- Вікно з підсумком перенесення (змінено / без змін / пропущено
  вичитаних / без відповідника)
- Захист від перезапису — лише за позначкою вичитки (ReviewStatus), не за
  фактом наявності перекладу: рядки без цієї позначки (включно з уже
  перекладеними Gemini) можуть бути перезаписані донором
- Вікно вирішення конфліктів (`TranslationConflictWindow`) для рядків, де
  донор має кілька різних перекладів одного англійського оригіналу
- Виділення кількох рядків таблиці (Ctrl/Shift + клік); правий клік по
  вже виділеному рядку зберігає виділення
- Контекстне меню «Вичитка для виділених»: позначки «+», «-», «+/-»,
  власний текст (`ReviewMarkPromptWindow`) і очищення — для всіх
  виділених рядків одразу
- Фільтр «Вичитано» (рядки з позначкою «+» або «+/-») і лічильник таких
  рядків
- Багаторядкове редагування перекладу: Shift+Enter / Ctrl+Enter вставляють
  перенос рядка, Enter підтверджує клітинку
- Маркер «↵» перед справжніми переносами рядка в колонках «Оригінал» і
  «Переклад»

### UA: Виправлено
- `ValidationService` порівнює кількість кожного маркера в обидва боки:
  маркер, якого в оригіналі немає, а в перекладі є, також позначається
  як проблема; переноси рядка (справжні та `\n`) враховуються
- Перевіряються: формат (`%s`, `%d` …), підстановки `{…}`, назви клавіш у
  квадратних дужках (`[E]`, `[SPACE]`, `[F1]` …), кнопка в круглих дужках,
  не приліплена до слова, екранування `\n`; решта дужок (`[locked]`,
  `Map(s)`) — звичайний текст
- Автозбереження читає рядки таблиці через `Dispatcher` (доступ до
  колекції з потоку таймера не кидає `InvalidOperationException`)
- Багаторядкове виділення працює в режимі `SelectionMode="Extended"`
  (спільний стиль `DataGrid` задає `Single`, тому режим заданий локально)

---

### EN: Added
- Cross-game translation transfer (BF1 ↔ BF2) matching by English
  original — the "⇄ BF1↔BF2" GUI button; the donor is given as two
  files (vanilla first, then translated), so the translation is
  correctly recognized even when the donor game writes it into the
  same language section as the original (BF1)
- Original matching ignores case and whitespace (BF1's English text is
  stored in UPPER CASE, BF2's in normal case); the transferred
  translation's case is aligned with the target game's row
  (`TranslationCaseAdapter`)
- Transfer summary dialog (changed / unchanged / reviewed skipped / no
  match)
- Overwrite protection based solely on the review mark (ReviewStatus),
  not on whether a translation is present: rows without that mark
  (including ones already translated by Gemini) may be overwritten by
  the donor
- Conflict-resolution window (`TranslationConflictWindow`) for strings
  where the donor has several different translations of the same
  English original
- Multi-row selection in the table (Ctrl/Shift + click); a right click on
  an already selected row keeps the selection
- "Review for selected" context menu: marks "+", "-", "+/-", custom text
  (`ReviewMarkPromptWindow`) and clear — applied to all selected rows at
  once
- "Reviewed" filter (rows marked "+" or "+/-") and a counter of such rows
- Multi-line translation editing: Shift+Enter / Ctrl+Enter insert a line
  break, Enter commits the cell
- A "↵" marker before real line breaks in the "Original" and
  "Translation" columns

### EN: Fixed
- `ValidationService` compares the count of each marker in both
  directions: a marker absent from the original but present in the
  translation is also flagged as an issue; line breaks (real and `\n`)
  are counted
- Checked: format specifiers (`%s`, `%d` …), `{…}` substitutions, key
  names in square brackets (`[E]`, `[SPACE]`, `[F1]` …), a button in round
  brackets not glued to a word, `\n` escapes; other brackets (`[locked]`,
  `Map(s)`) are ordinary text
- Autosave reads the table rows through `Dispatcher` (accessing the
  collection from the timer thread does not throw
  `InvalidOperationException`)
- Multi-row selection works with `SelectionMode="Extended"` (the shared
  `DataGrid` style sets `Single`, so the mode is set locally)

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

