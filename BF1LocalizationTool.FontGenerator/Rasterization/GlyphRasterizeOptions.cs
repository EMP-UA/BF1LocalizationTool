// =============================================================================
// BF1LocalizationTool.FontGenerator — Rasterization/GlyphRasterizeOptions.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Параметри растеризації одного гліфа.
//     Розмір canvas ЗАДАЄТЬСЯ ЗОВНІ (з існуючого донорського FBOD-запису:
//     ink_width × cell_h) — растеризатор НЕ вирішує розмір шрифту сам,
//     бо ми переписуємо існуючий слот атласу без зміни його геометрії
//     (стратегія з FONT_FORMAT_SPEC.md, розділ 5: "перезаписати існуючий
//     гліф-слот").
// EN: Parameters for rasterizing a single glyph.
//     Canvas size is supplied EXTERNALLY (from the existing donor FBOD
//     entry: ink_width × cell_h) — the rasterizer does NOT decide font
//     size itself, since we're overwriting an existing atlas slot without
//     changing its geometry (strategy from FONT_FORMAT_SPEC.md, section 5:
//     "overwrite existing glyph slot").
// =============================================================================

using System.Drawing;

namespace BF1LocalizationTool.FontGenerator.Rasterization;

public sealed record GlyphRasterizeOptions
{
    // UA: Назва системного шрифту (має підтримувати кирилицю).
    // EN: System font family name (must support Cyrillic).
    public required string FontFamilyName { get; init; }

    // UA: Розмір шрифту в пікселях (GraphicsUnit.Pixel). Підбирається
    //     емпірично зовнішнім кодом так, щоб гліф влазив у CanvasHeight
    //     з розумними полями — растеризатор сам це не підбирає.
    // EN: Font size in pixels (GraphicsUnit.Pixel). Tuned empirically by
    //     the caller so the glyph fits within CanvasHeight with reasonable
    //     margins — the rasterizer does not auto-fit this itself.
    public required float FontSizePx { get; init; }

    // UA: Точна ширина/висота canvas у пікселях — БЕРЕТЬСЯ з UV-прямокутника
    //     донорського гліфа (round(U1×texWidth)-round(U0×texWidth) і
    //     аналогічно по висоті), НЕ з ink_width/cell_h у FBOD.
    //     ПЕРЕВІРЕНО (GlyphSizeConsistencyCommand, 3616 гліфів BF1 +
    //     1582 BF2): ink_width/cell_h систематично РОЗХОДЯТЬСЯ з
    //     UV-розміром — особливо cell_h (різниця до 10 px), бо це висота
    //     комірки шрифту для курсора, а не розмір фактичної ділянки
    //     пікселів в атласі. Записувати гліф потрібно рівно в межах
    //     UV-прямокутника донора, інакше запис вилізе за межі виділеного
    //     слоту або залишить його частину незаписаною.
    // EN: Exact canvas width/height in pixels — TAKEN from the donor
    //     glyph's UV rectangle (round(U1×texWidth)-round(U0×texWidth) and
    //     similarly for height), NOT from ink_width/cell_h in FBOD.
    //     VERIFIED (GlyphSizeConsistencyCommand, 3616 BF1 glyphs + 1582
    //     BF2 glyphs): ink_width/cell_h systematically DIVERGE from the
    //     UV-derived size — especially cell_h (difference up to 10 px),
    //     since it's the font's cell height for cursor positioning, not
    //     the actual atlas pixel region size. The glyph must be written
    //     exactly within the donor's UV rectangle, or the write will
    //     overflow the allocated slot or leave part of it unwritten.
    public required int CanvasWidth { get; init; }
    public required int CanvasHeight { get; init; }

    // UA: Зміщення базової лінії від верху canvas у пікселях — параметр
    //     ПРОБНОГО растеризатора, не гри. Для вимірювання форми літер це
    //     завжди фіксоване значення пробного canvas (ProbeBaselineY=300,
    //     однакове для КОЖНОГО виміру цього алфавіту — CyrillicGlyphShapeProbe/
    //     GlyphBoxFitRenderer/GlyphMetricModel). Фактичне позиціювання
    //     гліфа НА ЕКРАНІ гри визначають поля FBOD Bearing/CellHeight
    //     (GlyphMetricModel), а не ця константа — растеризатор сам нічого
    //     не знає про базову лінію гри.
    // EN: Baseline offset from the top of the canvas, in pixels — a
    //     parameter of the PROBE rasterizer, not the game. For measuring
    //     letter shapes this is always the fixed probe-canvas value
    //     (ProbeBaselineY=300, the same for EVERY measurement across this
    //     alphabet — CyrillicGlyphShapeProbe/GlyphBoxFitRenderer/
    //     GlyphMetricModel). The letter's actual on-screen position in the
    //     game is governed by the FBOD Bearing/CellHeight fields
    //     (GlyphMetricModel), not by this constant — the rasterizer itself
    //     knows nothing about the game's baseline.
    public required int BaselineY { get; init; }

    public FontStyle Style { get; init; } = FontStyle.Regular;
}