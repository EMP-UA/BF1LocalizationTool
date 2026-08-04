// =============================================================================
// BF1LocalizationTool.Core — Fonts/FontGlyphRecord.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Один запис таблиці гліфів FBOD (24 байти). Формат підтверджено в
//     FONT_FORMAT_SPEC.md, розділ 4 — на контрастних символах M/W/i/l/./,.
//     Порядок полів U0,U1,V0,V1 (НЕ U0,V0,U1,V1).
//
//     offset=2 — ПІДТВЕРДЖЕНО (GlyphPageGroupingOverlapCommand, 11 шрифтів,
//     обидві гри, 100% нульових перетинів UV-прямокутників після
//     групування записів за цим байтом): це 0-based ІНДЕКС ТЕКСТУРНОЇ
//     СТОРІНКИ ({ім'я}_texN), якій належить гліф. Для шрифтів з кількома
//     сторінками (напр. gamefont_medium — 4 сторінки) кожен гліф
//     фізично існує ЛИШЕ на ОДНІЙ із них; інші сторінки на тих самих
//     UV-координатах містять пікселі ІНШИХ, не пов'язаних гліфів того
//     самого шрифту.
//
//     ТОЧНИЙ РОЗКЛАД БАЙТІВ (підтверджено з FontGlyphTable.Parse):
//       [0-1]   Code       (u16)
//       [2]     PageIndex
//       [3]     XAdvance
//       [4]     ReservedByte4
//       [5]     InkWidth
//       [6]     Bearing
//       [7]     CellHeight
//       [8-11]  U0 (float)
//       [12-15] U1 (float)
//       [16-19] V0 (float)
//       [20-23] V1 (float)
//
//     ReservedByte4 — призначення НЕВІДОМЕ (не реверс-інжинирено), але
//     значення ЗБЕРІГАЄТЬСЯ при парсингу, щоб FontGlyphTable.Serialize
//     міг точно відтворити цей байт при записі назад, а не мовчки
//     занулити невідомий вміст.
//
//     МЕТРИКИ (кореляція перевірена по КОЖНОМУ гліфу, обидві гри,
//     11 шрифтів — не за середнім за категорією):
//       XAdvance ≈ InkWidth + невелика стала (1-2px, масштабується з
//       розміром шрифту) — тісна кореляція, мале стандартне відхилення
//       (0,40-1,44) по всіх шрифтах. Класична горизонтальна метрика
//       "ширина символу + інтервал до наступного".
//       Bearing НЕ корелює просто з InkWidth (стандартне відхилення
//       2,4-7,6) — це очікувано для СПРАВЖНЬОГО left-side bearing
//       (горизонтальний відступ зліва до чорнила), який природно
//       різниться від форми літери до літери, а не сталий. ЖОДНИХ ознак,
//       що Bearing/XAdvance керують ВЕРТИКАЛЬНИМ позиціонуванням —
//       обидва узгоджуються зі стандартними горизонтальними
//       типографськими метриками.
// EN: A single FBOD glyph table record (24 bytes). Format confirmed in
//     FONT_FORMAT_SPEC.md, section 4 — verified against contrasting
//     characters M/W/i/l/./,.
//     Field order is U0,U1,V0,V1 (NOT U0,V0,U1,V1).
//
//     offset=2 — CONFIRMED (GlyphPageGroupingOverlapCommand, 11 fonts,
//     both games, 100% zero UV-rectangle overlaps after grouping records
//     by this byte): it is the 0-based TEXTURE PAGE INDEX ({name}_texN)
//     this glyph belongs to. For fonts with multiple pages (e.g.
//     gamefont_medium — 4 pages) each glyph physically exists on ONLY
//     ONE of them; the other pages at the same UV coordinates hold
//     pixels of OTHER, unrelated glyphs of the same font.
//
//     EXACT BYTE LAYOUT (confirmed from FontGlyphTable.Parse):
//       [0-1]   Code       (u16)
//       [2]     PageIndex
//       [3]     XAdvance
//       [4]     ReservedByte4
//       [5]     InkWidth
//       [6]     Bearing
//       [7]     CellHeight
//       [8-11]  U0 (float)
//       [12-15] U1 (float)
//       [16-19] V0 (float)
//       [20-23] V1 (float)
//
//     ReservedByte4 — purpose UNKNOWN (not reverse-engineered), but the
//     value is PRESERVED during parsing, so FontGlyphTable.Serialize can
//     reproduce this byte exactly when writing back, instead of silently
//     zeroing unknown content.
//
//     METRICS (correlation checked per INDIVIDUAL glyph, both games,
//     11 fonts — not by category average):
//       XAdvance ≈ InkWidth + a small constant (1-2px, scales with font
//       size) — tight correlation, low standard deviation (0.40-1.44)
//       across all fonts. A classic horizontal metric: "character width
//       + spacing to the next one".
//       Bearing does NOT correlate simply with InkWidth (standard
//       deviation 2.4-7.6) — expected for a GENUINE left-side bearing
//       (horizontal offset to the left of the ink), which naturally
//       varies by letter shape, not a constant. NO evidence that
//       Bearing/XAdvance control VERTICAL positioning — both are
//       consistent with standard horizontal typesetting metrics.
// =============================================================================

namespace BF1LocalizationTool.Core.Fonts;

public sealed record FontGlyphRecord
{
    // UA: Індекс запису в таблиці (0-based) — потрібен, щоб знати
    //     зміщення запису у сирих байтах FBOD при записі назад.
    // EN: Record index in the table (0-based) — needed to know the
    //     record's byte offset in raw FBOD when writing back.
    public required int Index { get; init; }

    public required ushort Code { get; init; }

    // UA: 0-based індекс текстурної сторінки ({ім'я}_texN), якій
    //     фізично належить цей гліф. Використовувати для доступу до
    //     правильних пікселів: font.TexturePages[record.PageIndex].
    //     НІКОЛИ не застосовувати UV-координати цього запису до "будь-
    //     якої"/"першої" сторінки шрифту — на інших сторінках ті самі
    //     UV вказують на пікселі ІНШИХ гліфів.
    // EN: 0-based texture page index ({name}_texN) this glyph physically
    //     belongs to. Use it to access the correct pixels:
    //     font.TexturePages[record.PageIndex]. NEVER apply this record's
    //     UV coordinates to "any"/"the first" page of the font — on
    //     other pages, the same UV points to OTHER glyphs' pixels.
    public required byte PageIndex { get; init; }

    public required byte XAdvance { get; init; }

    // UA: offset+4 — призначення невідоме, значення лише зберігається
    //     для точного round-trip запису (FontGlyphTable.Serialize).
    //     НЕ вигадувати сюди значення "за замовчуванням" — завжди брати
    //     з РЕАЛЬНОГО прочитаного байта (навіть для НОВИХ/змінених
    //     записів копіювати з донора, чий слот перезаписується).
    // EN: offset+4 — purpose unknown, the value is only preserved for
    //     exact round-trip writing (FontGlyphTable.Serialize). Do NOT
    //     invent a "default" value here — always take it from the
    //     ACTUAL byte read (even for NEW/modified records, copy it from
    //     the donor whose slot is being overwritten).
    public required byte ReservedByte4 { get; init; }

    public required byte InkWidth { get; init; }
    public required byte Bearing { get; init; }
    public required byte CellHeight { get; init; }
    public required float U0 { get; init; }
    public required float U1 { get; init; }
    public required float V0 { get; init; }
    public required float V1 { get; init; }
}
