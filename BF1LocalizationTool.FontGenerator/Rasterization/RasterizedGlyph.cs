// =============================================================================
// BF1LocalizationTool.FontGenerator — Rasterization/RasterizedGlyph.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Результат растеризації одного гліфа.
// EN: Result of rasterizing a single glyph.
// =============================================================================

using System.Drawing;

namespace BF1LocalizationTool.FontGenerator.Rasterization;

public sealed record RasterizedGlyph
{
    public required int Width { get; init; }
    public required int Height { get; init; }

    // UA: 32bpp ARGB пікселі, порядок байтів як у System.Drawing
    //     (B, G, R, A на піксель), row-major, top-left origin.
    //     RGB завжди (0xFF,0xFF,0xFF) — колір несе лише Alpha (покриття
    //     гліфа). Див. коментар у GdiGlyphRasterizer щодо ЧОМУ саме так.
    // EN: 32bpp ARGB pixels, byte order as in System.Drawing
    //     (B, G, R, A per pixel), row-major, top-left origin.
    //     RGB is always (0xFF,0xFF,0xFF) — only Alpha carries glyph
    //     coverage. See comment in GdiGlyphRasterizer for WHY.
    public required byte[] BgraPixels { get; init; }

    // UA: Обрізана (trimmed) прямокутна область непрозорих пікселів
    //     у координатах canvas. Порожній Rectangle (Width=0), якщо гліф
    //     не намалював жодного видимого пікселя (напр. пробіл).
    //     Використовується наступним кроком для обчислення нових
    //     ink_width/bearing у FBOD.
    // EN: Trimmed bounding rectangle of non-transparent pixels, in canvas
    //     coordinates. Empty Rectangle (Width=0) if the glyph produced no
    //     visible pixels (e.g. space). Used by the next step to compute
    //     new ink_width/bearing in FBOD.
    public required Rectangle InkBounds { get; init; }
}