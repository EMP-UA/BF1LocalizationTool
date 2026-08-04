// =============================================================================
// BF1LocalizationTool.Core — Fonts/FontResourceModel.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Проста, повністю розібрана модель ОДНОГО шрифтового ресурсу — рівно
//     стільки полів, скільки потрібно, щоб ВІДТВОРИТИ font-чанк байт-у-байт
//     (FontResourceBuilder) або зчитати наявний (FontResourceReader).
//
//     На відміну від FontChunkLocator (який лише знаходить UcfbChunk у
//     дереві для аналізу), ця модель — це вже РОЗІБРАНІ значення: розміри
//     текстур, сирі пікселі, таблиця гліфів, метадані HEAD. Її достатньо,
//     щоб згенерувати повністю НОВИЙ ресурс з нуля (свіжий атлас), а не
//     лише патчити наявний через донорів.
//
//     Формат HEAD (6 байт, FONT_FORMAT_SPEC.md §11.2,
//     `FontResourceBuilder.BuildHead`): `glyphCount(u16 LE) |
//     pageCount(u8) | fontHeightPx(u8) | 00 00`. Перші 2 байти — це
//     КІЛЬКІСТЬ ГЛІФІВ шрифту (=226 для всіх ВАНІЛЬНИХ шрифтів).
//     fontHeightPx корелює з розміром шрифту (large=22, medium=19,
//     small=17, tiny=13, super_tiny=13). Останні два байти (00 00) —
//     сталі на всіх шрифтах. glyphCount у цій моделі НЕ зберігається
//     окремим полем — `FontResourceBuilder.Build` завжди виводить його з
//     `Glyphs.Count` (розділ нижче), тож розсинхронізація HEAD/FBOD
//     неможлива за конструкцією.
// EN: A simple, fully-parsed model of ONE font resource — exactly enough
//     fields to REPRODUCE a font chunk byte-for-byte (FontResourceBuilder)
//     or read an existing one (FontResourceReader).
//
//     Unlike FontChunkLocator (which only finds UcfbChunk nodes in the tree
//     for analysis), this model holds the PARSED values: texture sizes, raw
//     pixels, glyph table, HEAD metadata. It is enough to generate a
//     completely NEW resource from scratch (a fresh atlas), not just patch
//     an existing one via donors.
//
//     HEAD format (6 bytes, FONT_FORMAT_SPEC.md §11.2,
//     `FontResourceBuilder.BuildHead`): `glyphCount(u16 LE) | pageCount(u8)
//     | fontHeightPx(u8) | 00 00`. The first 2 bytes are the font's GLYPH
//     COUNT (=226 for all VANILLA fonts). fontHeightPx correlates with
//     font size (large=22, medium=19, small=17, tiny=13, super_tiny=13).
//     The last two bytes (00 00) are constant across all fonts. glyphCount
//     isn't stored as a separate field in this model —
//     `FontResourceBuilder.Build` always derives it from `Glyphs.Count`
//     (below), so HEAD/FBOD can never desync by construction.
// =============================================================================

namespace BF1LocalizationTool.Core.Fonts;

// UA: Одна текстурна сторінка з РОЗІБРАНИМИ значеннями (не сирий чанк).
//     Format завжди 0x1A (D3DFMT_A4R4G4B4) для шрифтів обох ігор.
//     BodyPixels.Length МАЄ дорівнювати Width*Height*2.
// EN: A single texture page with PARSED values (not the raw chunk).
//     Format is always 0x1A (D3DFMT_A4R4G4B4) for both games' fonts.
//     BodyPixels.Length MUST equal Width*Height*2.
public sealed record FontTexturePageData
{
    public required string Name { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required uint Format { get; init; }
    public required byte[] BodyPixels { get; init; }
}

// UA: Повний шрифтовий ресурс, готовий до запису. HeadFontHeightPx і
//     сторінки — усе, що визначає HEAD (кількість сторінок береться з
//     Pages.Count при записі, тому в HEAD не дублюється тут).
// EN: A full font resource, ready to write. HeadFontHeightPx and the pages
//     are everything HEAD needs (the page count is taken from Pages.Count
//     at write time, so it isn't duplicated in HEAD here).
public sealed record FontResourceData
{
    public required string BaseName { get; init; }

    // UA: Байт 3 HEAD — висота шрифту в пікселях (метрика рядка двигуна).
    // EN: HEAD byte 3 — the font's pixel height (engine line metric).
    public required byte FontHeightPx { get; init; }

    public required IReadOnlyList<FontTexturePageData> Pages { get; init; }
    public required IReadOnlyList<FontGlyphRecord> Glyphs { get; init; }
}
