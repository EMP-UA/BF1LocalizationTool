// =============================================================================
// BF1LocalizationTool.FontGenerator — Rasterization/IGlyphRasterizer.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Контракт растеризації одного Unicode-символу в bitmap заданого
//     розміру з прозорим фоном (RGB=білий, Alpha=покриття гліфа).
// EN: Contract for rasterizing a single Unicode character into a bitmap
//     of a given size with a transparent background (RGB=white,
//     Alpha=glyph coverage).
// =============================================================================

namespace BF1LocalizationTool.FontGenerator.Rasterization;

public interface IGlyphRasterizer
{
    // UA: Растеризує один символ. Кидає ArgumentException якщо
    //     CanvasWidth/CanvasHeight <= 0.
    // EN: Rasterizes a single character. Throws ArgumentException if
    //     CanvasWidth/CanvasHeight <= 0.
    RasterizedGlyph Rasterize(char character, GlyphRasterizeOptions options);
}