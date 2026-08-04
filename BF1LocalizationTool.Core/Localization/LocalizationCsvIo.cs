// =============================================================================
// BF1LocalizationTool.Core — Localization/LocalizationCsvIo.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Спільний читач/писар для "мосту" CSV-формату, яким LvlLocalizationService
//     обмінюється перекладом із зовнішніми інструментами.
//
//     Формат (5 колонок): "Hash,Ordinal,Original,Translation,ReviewStatus"
//       Hash         — "0xHHHHHHHH", 32-бітний хеш рядка локалізації.
//       Ordinal      — порядковий номер серед записів з ТИМ САМИМ хешем
//                      (0 = перший). ЕМПІРИЧНО ПІДТВЕРДЖЕНО: у core.lvl
//                      (обидві гри) 32-бітний хеш навмисно повторюється для
//                      ДВОХ РІЗНИХ рядків тексту (напр. BF1 english
//                      0xf2e2b10d = "SQUAD COMMANDS" і окремо
//                      "SQUAD\r\nCOMMANDS") — без Ordinal неможливо
//                      відрізнити, якому з двох рядків призначався переклад.
//       Original     — оригінальний текст (лапки екрановані подвоєнням "").
//       Translation  — переклад (може бути порожнім, якщо ще не перекладено).
//       ReviewStatus — вільна позначка вичитки (напр. "+", "-", "+/-" чи
//                      коментар), ЛИШЕ GUI-метадані (MainWindow.EntryRow.
//                      ReviewStatus) — НІКОЛИ не потрапляє в core.lvl,
//                      Translator її не встановлює й не інтерпретує.
//
//     Старіші формати (4 колонки без ReviewStatus; 3 колонки без Ordinal —
//     ordinal мовчки =0) теж підтримуються при читанні, для сумісності зі
//     старими CSV-файлами.
//
//     ВИНЕСЕНО з LvlLocalizationService.ExportCsvAsync/ImportCsvAsync у
//     окремий, публічний клас Core: цей самий формат потрібен і новому
//     консольному ШІ-перекладачу (BF1LocalizationTool.Translator), який
//     працює НАПРЯМУ з CSV-файлами (без завантаженого LvlLocalizationService/
//     core.lvl) — щоб не дублювати парсинг/серіалізацію CSV між GUI і
//     Translator. LvlLocalizationService тепер лише конвертує між своєю
//     внутрішньою моделлю (LocalizationFile/LocalizationEntry) і рядками
//     LocalizationCsvRow, а сам файловий I/O і CSV-екранування — тут.
//
// EN: Shared reader/writer for the CSV "bridge" format LvlLocalizationService
//     uses to exchange translations with external tools.
//
//     Format (5 columns): "Hash,Ordinal,Original,Translation,ReviewStatus"
//       Hash         — "0xHHHHHHHH", 32-bit localization string hash.
//       Ordinal      — position among entries sharing the SAME hash
//                      (0 = first). EMPIRICALLY CONFIRMED: in core.lvl
//                      (both games) a 32-bit hash is deliberately reused for
//                      TWO DIFFERENT strings (e.g. BF1 english 0xf2e2b10d =
//                      "SQUAD COMMANDS" and separately "SQUAD\r\nCOMMANDS")
//                      — without Ordinal there's no way to tell which of the
//                      two strings a translation was meant for.
//       Original     — original text (quotes escaped by doubling "").
//       Translation  — translation (may be empty if not yet translated).
//       ReviewStatus — a free proofreading marker (e.g. "+", "-", "+/-", or
//                      a comment), GUI metadata ONLY (MainWindow.EntryRow.
//                      ReviewStatus) — NEVER ends up in core.lvl, the
//                      Translator neither sets nor interprets it.
//
//     Older formats (4 columns without ReviewStatus; 3 columns without
//     Ordinal — silently ordinal=0) are also supported on read, for
//     backward compatibility with older CSV files.
//
//     EXTRACTED from LvlLocalizationService.ExportCsvAsync/ImportCsvAsync
//     into a separate, public Core class: the console AI translator
//     (BF1LocalizationTool.Translator) needs this exact format too, and
//     works DIRECTLY with CSV files (without a loaded LvlLocalizationService/
//     core.lvl) — so CSV parsing/serialization isn't duplicated between GUI
//     and Translator. LvlLocalizationService now only converts between its
//     internal model (LocalizationFile/LocalizationEntry) and
//     LocalizationCsvRow records; the file I/O and CSV escaping live here.
// =============================================================================

using System.Text;

namespace BF1LocalizationTool.Core.Localization;

// UA: Один рядок CSV-мосту локалізації (без прив'язки до внутрішньої моделі
//     LocalizationFile — саме тому це придатно для Translator, який ніколи
//     не завантажує core.lvl напряму).
// EN: One row of the localization CSV bridge (not tied to the internal
//     LocalizationFile model — this is exactly why it's usable by the
//     Translator, which never loads core.lvl directly).
// UA: ReviewStatus — 5-та, ОПЦІЙНА колонка (за замовчуванням null): вільна
//     позначка проходження вичитки, напр. "+", "-", "+/-" чи довільний
//     коментар (той самий принцип, що й review TSV у SWH.LocEditor — не
//     переклад, а ознака ЩО й КОЛИ саме перевірено). Це виключно
//     GUI-метадані — жодна з ланок пайплайна, що пише в core.lvl
//     (LvlLocalizationService.SaveAsync/BuildGameEncodedClone), не бачить і
//     не читає це поле, тож воно НІКОЛИ не потрапляє у сам файл гри.
//     Translator також не чіпає це поле (не встановлює й не інтерпретує) —
//     воно просто проходить крізь WriteAsync/ReadAsync незмінним, якщо вже
//     є у файлі.
// EN: ReviewStatus — the 5th, OPTIONAL column (defaults to null): a free
//     proofreading-pass marker, e.g. "+", "-", "+/-", or a free-form
//     comment (same principle as the review TSV in SWH.LocEditor — not the
//     translation itself, but a marker of WHAT and WHEN was checked). This
//     is purely GUI metadata — none of the pipeline stages that write
//     core.lvl (LvlLocalizationService.SaveAsync/BuildGameEncodedClone) see
//     or read this field, so it NEVER ends up in the actual game file. The
//     Translator doesn't touch this field either (doesn't set or
//     interpret it) — it just passes through WriteAsync/ReadAsync
//     unchanged if already present in the file.
public record LocalizationCsvRow(uint Hash, int Ordinal, string Original, string? Translation, string? ReviewStatus = null);

public static class LocalizationCsvIo
{
    public const string Header = "Hash,Ordinal,Original,Translation,ReviewStatus";

    // -------------------------------------------------------------------------
    // UA: Будує текст CSV (UTF-8, без запису у файл) — спільна логіка для
    //     WriteAsync (файловий запис) і GUI-у (MainWindow.BuildCsvContent,
    //     той самий формат для автозбереження й "Експорт CSV") — щоб
    //     екранування лапок не дублювалось між ними.
    // EN: Builds CSV text (UTF-8, no file write) — shared logic for
    //     WriteAsync (file write) and the GUI (MainWindow.BuildCsvContent,
    //     same format for autosave and "Export CSV") — so quote-escaping
    //     isn't duplicated between them.
    // -------------------------------------------------------------------------
    public static string BuildCsvText(IEnumerable<LocalizationCsvRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);

        foreach (var row in rows)
        {
            var original = row.Original.Replace("\"", "\"\"");
            var translation = (row.Translation ?? string.Empty).Replace("\"", "\"\"");
            var reviewStatus = (row.ReviewStatus ?? string.Empty).Replace("\"", "\"\"");
            sb.AppendLine($"0x{row.Hash:x8},{row.Ordinal},\"{original}\",\"{translation}\",\"{reviewStatus}\"");
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // UA: Записує рядки у CSV-файл (UTF-8, 5-колонковий формат).
    // EN: Writes rows to a CSV file (UTF-8, 5-column format).
    // -------------------------------------------------------------------------
    public static async Task WriteAsync(string csvPath, IEnumerable<LocalizationCsvRow> rows) =>
        await File.WriteAllTextAsync(csvPath, BuildCsvText(rows), Encoding.UTF8);

    // -------------------------------------------------------------------------
    // UA: Читає рядки з CSV-файлу. Підтримує найновіший (5-колонковий, +
    //     ReviewStatus), новий (4-колонковий) і старий (3-колонковий, без
    //     Ordinal) формат. Записи що не парсяться — пропускаються мовчки.
    //
    //     КРИТИЧНО: файл читається ВЕСЬ одним текстовим блоком і парситься
    //     власним посимвольним автоматом (ParseCsvRecords), де \r/\n
    //     рахуються роздільником ЗАПИСУ ЛИШЕ ПОЗА лапками — точно як у
    //     RFC4180. Це необхідно, бо в реальних CSV-файлах локалізації
    //     (ЕМПІРИЧНО ПІДТВЕРДЖЕНО: 421 рядок, 89 BF1 + 332 BF2, напр.
    //     "Bonuses\r\n(1/3) {OptionR}...") зустрічається БУКВАЛЬНИЙ \r\n
    //     УСЕРЕДИНІ самого тексту — частина значення, взята в лапки, а не
    //     роздільник рядків. Читання по рядках (напр. через
    //     File.ReadAllLinesAsync) розрізало б ОДИН логічний CSV-запис на
    //     ДВА "рядки" для кожного такого випадку, зсуваючи колонки
    //     Original/Translation як для цього, так і для сусідніх записів.
    // EN: Reads rows from a CSV file. Supports the newest (5-column, +
    //     ReviewStatus), new (4-column), and old (3-column, no Ordinal)
    //     formats. Records that fail to parse are silently skipped.
    //
    //     CRITICAL: the file is read as ONE text blob and parsed with a
    //     character-by-character state machine (ParseCsvRecords), where
    //     \r/\n only count as a RECORD separator OUTSIDE quotes — exactly
    //     per RFC4180. This is necessary because real localization CSV
    //     files (EMPIRICALLY CONFIRMED: 421 rows, 89 BF1 + 332 BF2, e.g.
    //     "Bonuses\r\n(1/3) {OptionR}...") contain a LITERAL \r\n INSIDE
    //     the text itself — part of the quoted value, not a line
    //     separator. Reading line-by-line (e.g. via
    //     File.ReadAllLinesAsync) would split ONE logical CSV record into
    //     TWO "lines" for every such case, shifting the Original/
    //     Translation columns for that record and any neighboring ones.
    // -------------------------------------------------------------------------
    public static async Task<List<LocalizationCsvRow>> ReadAsync(string csvPath)
    {
        var text = await File.ReadAllTextAsync(csvPath, Encoding.UTF8);
        var records = ParseCsvRecords(text);
        var result = new List<LocalizationCsvRow>();

        foreach (var parts in records.Skip(1)) // UA: пропускаємо заголовок / EN: skip header
        {
            if (parts.Length < 3) continue;
            if (!TryParseHash(parts[0], out var hash)) continue;

            // UA: Три формати підтримуються при читанні: найновіший
            //     (5 колонок, +ReviewStatus), новий (4 колонки, Ordinal
            //     другою) і старий (3 колонки, без Ordinal — ordinal=0).
            //     ReviewStatus є ЛИШЕ у 5-колонковому форматі — у решти
            //     мовчки лишається null (немає звідки взяти).
            // EN: Three formats supported on read: newest (5 columns,
            //     +ReviewStatus), new (4 columns, Ordinal second), and old
            //     (3 columns, no Ordinal). ReviewStatus exists ONLY in the
            //     5-column format — for the others it silently stays null
            //     (nothing to read it from).
            var ordinal = 0;
            var originalIdx = 1;
            var translationIdx = 2;
            var reviewStatusIdx = -1;
            if (parts.Length >= 4 && int.TryParse(parts[1], out var parsedOrdinal))
            {
                ordinal = parsedOrdinal;
                originalIdx = 2;
                translationIdx = 3;
                reviewStatusIdx = 4;
            }

            var original = parts[originalIdx];
            var translation = parts.Length > translationIdx ? parts[translationIdx] : string.Empty;
            var reviewStatus = reviewStatusIdx >= 0 && parts.Length > reviewStatusIdx ? parts[reviewStatusIdx] : string.Empty;

            result.Add(new LocalizationCsvRow(hash, ordinal, original,
                string.IsNullOrWhiteSpace(translation) ? null : translation.Trim(),
                string.IsNullOrWhiteSpace(reviewStatus) ? null : reviewStatus.Trim()));
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // UA: Парсить ВЕСЬ текст CSV-файлу в список записів (кожен запис —
    //     масив полів). Лапко-свідомий посимвольний автомат: "" всередині
    //     лапок = один символ ", кома поза лапками = роздільник поля,
    //     \r/\n/\r\n ПОЗА лапками = роздільник запису. Усередині лапок
    //     \r та \n — звичайні символи значення (див. коментар до ReadAsync
    //     про те, чому це критично для реальних файлів локалізації).
    // EN: Parses the ENTIRE CSV file text into a list of records (each
    //     record — an array of fields). Quote-aware character-by-character
    //     state machine: "" inside quotes = one " character, comma outside
    //     quotes = field separator, \r/\n/\r\n OUTSIDE quotes = record
    //     separator. Inside quotes, \r and \n are regular value characters
    //     (see the ReadAsync comment for why this matters for real
    //     localization files).
    // -------------------------------------------------------------------------
    private static List<string[]> ParseCsvRecords(string text)
    {
        var records = new List<string[]>();
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var i = 0;
        var n = text.Length;

        void EndField() { fields.Add(current.ToString()); current.Clear(); }
        void EndRecord() { EndField(); records.Add([.. fields]); fields.Clear(); }

        while (i < n)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < n && text[i + 1] == '"') { current.Append('"'); i += 2; continue; }
                    inQuotes = false; i++; continue;
                }
                current.Append(c); i++; continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true; i++; continue;
                case ',':
                    EndField(); i++; continue;
                case '\r':
                    i++;
                    if (i < n && text[i] == '\n') i++;
                    EndRecord(); continue;
                case '\n':
                    i++;
                    EndRecord(); continue;
                default:
                    current.Append(c); i++; continue;
            }
        }

        // UA: Останній запис, якщо файл не закінчується символом нового рядка
        // EN: Final record, if the file doesn't end with a newline character
        if (current.Length > 0 || fields.Count > 0)
            EndRecord();

        return records;
    }

    public static bool TryParseHash(string s, out uint hash)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out hash);
        return uint.TryParse(s, out hash);
    }
}
