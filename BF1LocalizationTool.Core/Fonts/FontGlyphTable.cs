// =============================================================================
// BF1LocalizationTool.Core — Fonts/FontGlyphTable.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Парсить сирі байти чанку FBOD у список FontGlyphRecord, і навпаки —
//     серіалізує список назад у сирі байти для запису.
//     FBOD — лист (Children.Count == 0), фіксовані записи по 24 байти,
//     без пропусків (FONT_FORMAT_SPEC.md, розділ 4).
//
//     Serialize використовується в GlyphAtlasPatcher для запису
//     локалізованого шрифту. Записує рівно records.Count * 24 байти, у
//     ТОМУ САМОМУ порядку полів, що й Parse читає — включно з
//     ReservedByte4 (значення береться з самого запису, Serialize
//     нічого не вигадує й не занулює).
// EN: Parses raw FBOD chunk bytes into a list of FontGlyphRecord, and
//     the reverse — serializes the list back into raw bytes for writing.
//     FBOD is a leaf chunk (Children.Count == 0), fixed 24-byte records,
//     no gaps (FONT_FORMAT_SPEC.md, section 4).
//
//     Serialize is used by GlyphAtlasPatcher to write the localized
//     font. Writes exactly records.Count * 24 bytes, in the SAME field
//     order Parse reads — including ReservedByte4 (the value comes from
//     the record itself, Serialize never invents or zeroes it).
// =============================================================================

namespace BF1LocalizationTool.Core.Fonts;

public static class FontGlyphTable
{
    private const int RecordSize = 24;

    public static IReadOnlyList<FontGlyphRecord> Parse(byte[] fbodRawData)
    {
        if (fbodRawData.Length % RecordSize != 0)
            throw new InvalidDataException(
                $"UA: Розмір FBOD ({fbodRawData.Length}) не кратний {RecordSize} байтам — " +
                "формат не відповідає очікуваному або дані пошкоджені / " +
                $"EN: FBOD size ({fbodRawData.Length}) is not a multiple of {RecordSize} bytes — " +
                "format does not match expectations, or data is corrupted");

        var count = fbodRawData.Length / RecordSize;
        var records = new List<FontGlyphRecord>(count);

        for (var i = 0; i < count; i++)
        {
            var offset = i * RecordSize;

            records.Add(new FontGlyphRecord
            {
                Index = i,
                Code = BitConverter.ToUInt16(fbodRawData, offset + 0),
                // UA: offset+2 — індекс текстурної сторінки (див. коментар
                //     у FontGlyphRecord.PageIndex для обґрунтування).
                // EN: offset+2 — texture page index (see the rationale in
                //     FontGlyphRecord.PageIndex).
                PageIndex = fbodRawData[offset + 2],
                XAdvance = fbodRawData[offset + 3],
                // UA: offset+4 — зберігається (призначення невідоме,
                //     значення — ТЕ САМЕ, що в файлі, не вигадане).
                // EN: offset+4 — preserved (purpose unknown, value is
                //     the SAME as in the file, not invented).
                ReservedByte4 = fbodRawData[offset + 4],
                InkWidth = fbodRawData[offset + 5],
                Bearing = fbodRawData[offset + 6],
                CellHeight = fbodRawData[offset + 7],
                U0 = BitConverter.ToSingle(fbodRawData, offset + 8),
                U1 = BitConverter.ToSingle(fbodRawData, offset + 12),
                V0 = BitConverter.ToSingle(fbodRawData, offset + 16),
                V1 = BitConverter.ToSingle(fbodRawData, offset + 20),
            });
        }

        return records;
    }

    // -------------------------------------------------------------------------
    // UA: Серіалізує список записів НАЗАД у сирі байти FBOD — точна
    //     інверсія Parse. Записи пишуться в ПОРЯДКУ СПИСКУ (не за
    //     record.Index) — виклик коду відповідає за те, щоб список мав
    //     той самий порядок, що й оригінал (типово: Parse → змінити
    //     потрібні записи на місці в тому самому списку → Serialize,
    //     ніколи не пересортовувати).
    //
    //     Довжина результату завжди records.Count * 24 — сумісно з
    //     UcfbWriter.WriteFile (розмір заміни чанку МОЖЕ відрізнятись
    //     від оригіналу, підтверджено в UcfbWriter.cs), хоча для FBOD
    //     кількість записів зазвичай не змінюється (лише вміст
    //     конкретних записів).
    // EN: Serializes the record list BACK into raw FBOD bytes — the
    //     exact inverse of Parse. Records are written in LIST ORDER (not
    //     by record.Index) — the calling code is responsible for the
    //     list having the same order as the original (typically: Parse →
    //     modify the needed records in place in that same list →
    //     Serialize, never re-sort).
    //
    //     Result length is always records.Count * 24 — compatible with
    //     UcfbWriter.WriteFile (a chunk replacement's size CAN differ
    //     from the original, confirmed in UcfbWriter.cs), though for
    //     FBOD the record count typically doesn't change (only specific
    //     records' content does).
    // -------------------------------------------------------------------------
    public static byte[] Serialize(IReadOnlyList<FontGlyphRecord> records)
    {
        var result = new byte[records.Count * RecordSize];

        for (var i = 0; i < records.Count; i++)
        {
            var r = records[i];
            var offset = i * RecordSize;

            BitConverter.GetBytes(r.Code).CopyTo(result, offset + 0);
            result[offset + 2] = r.PageIndex;
            result[offset + 3] = r.XAdvance;
            result[offset + 4] = r.ReservedByte4;
            result[offset + 5] = r.InkWidth;
            result[offset + 6] = r.Bearing;
            result[offset + 7] = r.CellHeight;
            BitConverter.GetBytes(r.U0).CopyTo(result, offset + 8);
            BitConverter.GetBytes(r.U1).CopyTo(result, offset + 12);
            BitConverter.GetBytes(r.V0).CopyTo(result, offset + 16);
            BitConverter.GetBytes(r.V1).CopyTo(result, offset + 20);
        }

        return result;
    }

    // UA: Знайти запис за кодом символу. Повертає null, якщо коду нема
    //     в таблиці (напр. код поза діапазоном донорів цього шрифту).
    //     Кожен код зустрічається РІВНО ОДИН РАЗ на весь шрифт (не по
    //     разу на кожну сторінку) — сторінка визначається полем
    //     PageIndex цього єдиного запису, не повторним пошуком.
    // EN: Find a record by character code. Returns null if the code
    //     isn't in the table (e.g. code outside this font's donor range).
    //     Each code appears EXACTLY ONCE for the whole font (not once
    //     per page) — the page is determined by this single record's
    //     PageIndex field, not by a repeated lookup.
    public static FontGlyphRecord? FindByCode(IReadOnlyList<FontGlyphRecord> records, ushort code) =>
        records.FirstOrDefault(r => r.Code == code);
}
