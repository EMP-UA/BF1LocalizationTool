// =============================================================================
// BF1LocalizationTool.Core — Fonts/GlyphAtlasStyleAnalyzer.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Проходить УСІ відомі шрифти, УСІ їхні текстурні сторінки, і УСІ
//     гліфи з FBOD (не один вручну обраний символ) — щоб статистично
//     підтвердити чи спростувати конвенцію запису прозорих пікселів
//     перед написанням FontGenerator-конвертера. Навмисно НЕ зупиняється
//     на першому знайденому шаблоні: рахує кожен піксель кожного гліфа.
// EN: Walks THROUGH ALL known fonts, ALL their texture pages, and ALL
//     glyphs from FBOD (not a single manually chosen character) — to
//     statistically confirm or refute the transparent-pixel encoding
//     convention before writing the FontGenerator converter. Deliberately
//     does NOT stop at the first pattern found: counts every pixel of
//     every glyph.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Core.Fonts;

public static class GlyphAtlasStyleAnalyzer
{
    public static IReadOnlyList<GlyphTransparentPixelStats> AnalyzeFile(UcfbChunk root)
    {
        var fonts = FontChunkLocator.FindAll(root);
        var results = new List<GlyphTransparentPixelStats>();

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null)
                continue; // UA: не мало б трапитись, але не падаємо / EN: shouldn't happen, but don't crash

            var glyphs = FontGlyphTable.Parse(fbod.RawData);

            foreach (var page in font.TexturePages)
                results.Add(AnalyzePage(font.BaseName, page, glyphs));
        }

        return results;
    }

    private static GlyphTransparentPixelStats AnalyzePage(
        string fontBaseName, FontTexturePage page, IReadOnlyList<FontGlyphRecord> glyphs)
    {
        var texPixels = FontTexturePixelReader.ReadMip0(page.Chunk);

        long total = 0, whiteOnZero = 0, zeroOnZero = 0, otherOnZero = 0, anomalyOnNonZero = 0;

        foreach (var glyph in glyphs)
        {
            var x0 = (int)Math.Round(glyph.U0 * texPixels.Width);
            var x1 = (int)Math.Round(glyph.U1 * texPixels.Width);
            var y0 = (int)Math.Round(glyph.V0 * texPixels.Height);
            var y1 = (int)Math.Round(glyph.V1 * texPixels.Height);

            // UA: min/max — напрямок осі V (top-down/bottom-up) ще не
            //     перевірявся окремо, той самий захист що й у
            //     GlyphPixelDumpCommand.
            // EN: min/max — V-axis direction (top-down/bottom-up) not
            //     separately verified yet, same guard as in
            //     GlyphPixelDumpCommand.
            var minX = Math.Min(x0, x1);
            var maxX = Math.Max(x0, x1);
            var minY = Math.Min(y0, y1);
            var maxY = Math.Max(y0, y1);

            for (var y = minY; y < maxY; y++)
            {
                if (y < 0 || y >= texPixels.Height) continue;

                for (var x = minX; x < maxX; x++)
                {
                    if (x < 0 || x >= texPixels.Width) continue;

                    var pixelIndex = y * texPixels.Width + x;
                    var (a, r, g, b) = FontTexturePixelReader.DecodePixel(texPixels.RawA4R4G4B4Pixels, pixelIndex);

                    total++;
                    var isWhite = r == 0xFF && g == 0xFF && b == 0xFF;
                    var isZero = r == 0 && g == 0 && b == 0;

                    if (a == 0)
                    {
                        if (isWhite) whiteOnZero++;
                        else if (isZero) zeroOnZero++;
                        else otherOnZero++;
                    }
                    else if (!isWhite)
                    {
                        anomalyOnNonZero++;
                    }
                }
            }
        }

        return new GlyphTransparentPixelStats
        {
            FontBaseName = fontBaseName,
            TexturePageName = page.Name,
            TotalGlyphs = glyphs.Count,
            TotalPixelsChecked = total,
            ZeroAlphaWhiteRgbCount = whiteOnZero,
            ZeroAlphaZeroRgbCount = zeroOnZero,
            ZeroAlphaOtherRgbCount = otherOnZero,
            NonZeroAlphaNonWhiteRgbCount = anomalyOnNonZero,
        };
    }
}