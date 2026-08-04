// =============================================================================
// BF1LocalizationTool.Diagnostic — GlyphOrientationCheckCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перевіряє напрямок осі V (top-down чи bottom-up) для ОБОХ core.lvl
//     одним запуском. Перелік доступних шрифтів/сторінок друкується ОКРЕМО
//     для кожного файлу з РЕАЛЬНИХ даних (FontChunkLocator), а не як
//     хардкоджений приклад — бо набір шрифтів/сторінок НЕ ідентичний між
//     BF1 і BF2 (starwars_small лише в BF1; кількість _texN сторінок
//     відрізняється навіть для однакових базових імен — підтверджено
//     GlyphAtlasStyleAnalyzer раніше: gamefont_super_tiny має 2 сторінки
//     в BF1, лише 1 в BF2).
// EN: Verifies V-axis direction (top-down or bottom-up) for BOTH core.lvl
//     files in one run. Available fonts/pages are printed SEPARATELY for
//     each file from REAL data (FontChunkLocator), not as a hardcoded
//     example — because the set of fonts/pages is NOT identical between
//     BF1 and BF2 (starwars_small is BF1-only; the number of _texN pages
//     differs even for identical base names — confirmed earlier by
//     GlyphAtlasStyleAnalyzer: gamefont_super_tiny has 2 pages in BF1,
//     only 1 in BF2).
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GlyphOrientationCheckCommand
{
    // -------------------------------------------------------------------------
    // UA: Друкує РЕАЛЬНИЙ перелік шрифтів і сторінок одного файлу — щоб
    //     людина обирала з фактично наявного, а не з вгаданого прикладу.
    // EN: Prints the REAL list of fonts and pages for one file — so the
    //     human picks from what's actually present, not a guessed example.
    // -------------------------------------------------------------------------
    public static void PrintAvailable(DiagnosticReport report, UcfbChunk root, string label)
    {
        var fonts = FontChunkLocator.FindAll(root);
        report.Log($"  Доступно в {label}:");
        foreach (var font in fonts)
        {
            var pages = string.Join(", ", font.TexturePages.Select(p => p.Name));
            report.Log($"    {font.BaseName}: {pages}");
        }
    }

    public static void Run(DiagnosticReport report, UcfbChunk root, string label, string fontBaseName, string texturePageName, ushort code)
    {
        var font = FontChunkLocator.FindAll(root).FirstOrDefault(f => f.BaseName == fontBaseName);
        if (font is null)
        {
            report.Log($"  [{label}] Шрифт '{fontBaseName}' відсутній у цьому файлі — пропускаю.");
            return;
        }

        var page = font.TexturePages.FirstOrDefault(p => p.Name == texturePageName);
        if (page is null)
        {
            report.Log($"  [{label}] Сторінка '{texturePageName}' відсутня у цьому шрифті. Доступні: " +
                               string.Join(", ", font.TexturePages.Select(p => p.Name)));
            return;
        }

        var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
        if (fbod is null)
        {
            report.Log($"  [{label}] FBOD не знайдено.");
            return;
        }

        var glyphs = FontGlyphTable.Parse(fbod.RawData);
        var glyph = FontGlyphTable.FindByCode(glyphs, code);
        if (glyph is null)
        {
            report.Log($"  [{label}] Код 0x{code:X2} відсутній у таблиці цього шрифту — пропускаю.");
            return;
        }

        var texPixels = FontTexturePixelReader.ReadMip0(page.Chunk);

        var x0 = (int)Math.Round(glyph.U0 * texPixels.Width);
        var x1 = (int)Math.Round(glyph.U1 * texPixels.Width);
        var y0 = (int)Math.Round(glyph.V0 * texPixels.Height);
        var y1 = (int)Math.Round(glyph.V1 * texPixels.Height);

        var minX = Math.Min(x0, x1);
        var maxX = Math.Max(x0, x1);
        var minY = Math.Min(y0, y1);
        var maxY = Math.Max(y0, y1);

        report.Log($"=== [{label}] {fontBaseName}/{texturePageName}, code=0x{code:X2} ('{(char)code}') ===");
        report.Log($"    x=[{minX}..{maxX}) y=[{minY}..{maxY}), texture {texPixels.Width}x{texPixels.Height}");
        report.Log("    row0 = НАЙМЕНШИЙ V (min(V0,V1)) — так наш код читає й писатиме всюди.");

        for (var y = minY; y < maxY; y++)
        {
            var row = "";
            for (var x = minX; x < maxX; x++)
            {
                var pixelIndex = y * texPixels.Width + x;
                var (a, _, _, _) = FontTexturePixelReader.DecodePixel(texPixels.RawA4R4G4B4Pixels, pixelIndex);
                row += a switch { >= 192 => "#", >= 64 => "+", > 0 => ".", _ => " " };
            }
            report.Log($"    row{y - minY,2}: [{row}]");
        }
    }
}