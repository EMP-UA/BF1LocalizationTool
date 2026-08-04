// =============================================================================
// BF1LocalizationTool.Diagnostic — GlyphVerticalGeometryProbeCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Крок ПЕРЕД виведенням формули GlyphRasterizeOptions.BaselineY
//     (TODO, зафіксований прямо в GlyphRasterizeOptions.cs).
//
//     ВАЖЛИВЕ ПОПЕРЕДНЄ ПИТАННЯ, яке спершу треба закрити фактами, а не
//     припущенням: чи UV-прямокутник гліфа взагалі є "спільною коміркою"
//     зі сталою висотою для всіх літер шрифту (тоді в ній можна шукати
//     єдину лінію-базу, як EngineBaseline factor у SteamWorld Heist) —
//     ЧИ ЖЕ він щільно обрізаний по чорнилу (tight crop) під КОЖНУ літеру
//     окремо (тоді єдиної "базової лінії у відсотках від CanvasHeight" не
//     існує в принципі, бо в кожного гліфа своя висота прямокутника, і
//     чорнило торкається обох країв рамки завжди).
//
//     GlyphSizeConsistencyCommand уже показав натяк: ink_width/cell_h з
//     FBOD систематично РОЗХОДЯТЬСЯ з UV-розміром — тобто UV-прямокутник
//     НЕ дорівнює номінальній комірці шрифту. Але це ще не відповідає на
//     питання "чи однакова висота в різних ЛІТЕР ОДНОГО шрифту" — саме
//     це і перевіряє ця команда, на РЕАЛЬНИХ ASCII-гліфах трьох категорій
//     форми (щоб відрізнити спільну комірку від щільного обрізання):
//       - baseline-контур (x-height, без виносних елементів): a c e m n
//         o r s u v w x z — очікувано "сидять" на базовій лінії знизу
//       - зі спускним елементом (descender): g j p q y — очікувано
//         виступають НИЖЧЕ базової лінії
//       - з висхідним елементом/великі літери (ascender/caps): b d f h
//         k l t B D H M T — очікувано сягають майже верху комірки
//
//     Для кожного знайденого коду друкує: розмір прямокутника (CanvasWidth
//     ×CanvasHeight) і чи торкається чорнило (Alpha>0) ВЕРХНЬОГО і
//     НИЖНЬОГО рядка прямокутника. Якщо в межах ОДНІЄЇ категорії висота
//     завжди різна І чорнило завжди торкається обох країв — це щільне
//     обрізання (BaselineY-формула у відсотках НЕ застосовна, потрібна
//     інша стратегія розміщення). Якщо висота однакова в межах шрифту, а
//     чорнило торкається країв ЛИШЕ у відповідних категоріях (ascender
//     згори, descender знизу) — це спільна комірка, і формулу можна
//     вивести з різниці висот між категоріями.
// EN: Step BEFORE deriving a formula for
//     GlyphRasterizeOptions.BaselineY (a TODO documented right in
//     GlyphRasterizeOptions.cs).
//
//     IMPORTANT PRIOR QUESTION that needs settling with facts, not an
//     assumption: is a glyph's UV rectangle a "shared cell" with a
//     constant height across all letters of a font (in which case a
//     single baseline line can be sought within it, like the
//     EngineBaseline factor from SteamWorld Heist) — OR is it tightly
//     cropped to ink (tight crop) PER LETTER individually (in which case
//     no single "baseline as a percentage of CanvasHeight" exists at
//     all, since every glyph has its own rectangle height, and ink
//     always touches both edges of the frame).
//
//     GlyphSizeConsistencyCommand already hinted at this: FBOD's
//     ink_width/cell_h systematically DIVERGE from the UV-derived size —
//     meaning the UV rectangle does NOT equal the font's nominal cell.
//     But that doesn't yet answer "is height the same across different
//     LETTERS of the SAME font" — which is exactly what this command
//     checks, on REAL ASCII glyphs from three shape categories (to tell
//     a shared cell apart from a tight crop):
//       - baseline-sitting (x-height, no ascender/descender): a c e m n
//         o r s u v w x z — expected to "sit" on the baseline at the
//         bottom
//       - with a descender: g j p q y — expected to extend BELOW the
//         baseline
//       - with an ascender / capital letters: b d f h k l t B D H M T —
//         expected to reach nearly the top of the cell
//
//     For every code found, prints: rectangle size (CanvasWidth×
//     CanvasHeight) and whether ink (Alpha>0) touches the TOP and BOTTOM
//     row of the rectangle. If, within ONE category, height always
//     differs AND ink always touches both edges — that's a tight crop
//     (a percentage-based BaselineY formula does NOT apply, a different
//     placement strategy is needed). If height is the same across the
//     font, and ink touches the edges ONLY in the matching categories
//     (ascender at top, descender at bottom) — that's a shared cell, and
//     a formula can be derived from the height difference between
//     categories.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GlyphVerticalGeometryProbeCommand
{
    private enum ShapeCategory { BaselineSitter, Descender, Ascender }

    private static readonly (char Character, ShapeCategory Category)[] ProbeSet =
    [
        ('a', ShapeCategory.BaselineSitter), ('c', ShapeCategory.BaselineSitter),
        ('e', ShapeCategory.BaselineSitter), ('m', ShapeCategory.BaselineSitter),
        ('n', ShapeCategory.BaselineSitter), ('o', ShapeCategory.BaselineSitter),
        ('r', ShapeCategory.BaselineSitter), ('s', ShapeCategory.BaselineSitter),
        ('u', ShapeCategory.BaselineSitter), ('v', ShapeCategory.BaselineSitter),
        ('w', ShapeCategory.BaselineSitter), ('x', ShapeCategory.BaselineSitter),
        ('z', ShapeCategory.BaselineSitter),

        ('g', ShapeCategory.Descender), ('j', ShapeCategory.Descender),
        ('p', ShapeCategory.Descender), ('q', ShapeCategory.Descender),
        ('y', ShapeCategory.Descender),

        ('b', ShapeCategory.Ascender), ('d', ShapeCategory.Ascender),
        ('f', ShapeCategory.Ascender), ('h', ShapeCategory.Ascender),
        ('k', ShapeCategory.Ascender), ('l', ShapeCategory.Ascender),
        ('t', ShapeCategory.Ascender),
        ('B', ShapeCategory.Ascender), ('D', ShapeCategory.Ascender),
        ('H', ShapeCategory.Ascender), ('M', ShapeCategory.Ascender),
        ('T', ShapeCategory.Ascender),
    ];

    public static void Run(DiagnosticReport report, UcfbChunk root, string label)
    {
        var fonts = FontChunkLocator.FindAll(root);

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var glyphs = FontGlyphTable.Parse(fbod.RawData);

            report.Log($"=== [{label}] {font.BaseName}: вертикальна геометрія + метадані FBOD реальних ASCII-гліфів ===");
            report.Log($"    {"Символ",-6}{"Категорія",-16}{"CanvasW",-8}{"CanvasH",-8}{"XAdv",-6}{"InkW",-6}{"Bearing",-8}{"CellH",-6}{"Чорн.згори",-12}{"Чорн.знизу",-12}");

            var heightsBySeenCategory = new Dictionary<ShapeCategory, HashSet<int>>
            {
                [ShapeCategory.BaselineSitter] = [],
                [ShapeCategory.Descender] = [],
                [ShapeCategory.Ascender] = []
            };

            var topRowsBySeenCategory = new Dictionary<ShapeCategory, List<int>>
            {
                [ShapeCategory.BaselineSitter] = [],
                [ShapeCategory.Descender] = [],
                [ShapeCategory.Ascender] = []
            };

            var bottomRowsBySeenCategory = new Dictionary<ShapeCategory, List<int>>
            {
                [ShapeCategory.BaselineSitter] = [],
                [ShapeCategory.Descender] = [],
                [ShapeCategory.Ascender] = []
            };

            // UA: Накопичення XAdvance/InkWidth/Bearing/CellHeight по
            //     категорії — шукаємо кореляцію фактом: чи Bearing
            //     систематично різний для ascender/descender/baseline
            //     (натяк на вертикальне позиціонування), чи однаковий
            //     (тоді це, ймовірно, горизонтальний side bearing, як і
            //     підказує назва поля).
            // EN: Accumulate XAdvance/InkWidth/Bearing/CellHeight per
            //     category — looking for a factual correlation: whether
            //     Bearing systematically differs for
            //     ascender/descender/baseline (hinting at vertical
            //     positioning), or stays uniform (then it's likely a
            //     horizontal side bearing, as the field name suggests).
            var xAdvanceBySeenCategory = new Dictionary<ShapeCategory, List<int>>
            {
                [ShapeCategory.BaselineSitter] = [],
                [ShapeCategory.Descender] = [],
                [ShapeCategory.Ascender] = []
            };
            var inkWidthBySeenCategory = new Dictionary<ShapeCategory, List<int>>
            {
                [ShapeCategory.BaselineSitter] = [],
                [ShapeCategory.Descender] = [],
                [ShapeCategory.Ascender] = []
            };
            var bearingBySeenCategory = new Dictionary<ShapeCategory, List<int>>
            {
                [ShapeCategory.BaselineSitter] = [],
                [ShapeCategory.Descender] = [],
                [ShapeCategory.Ascender] = []
            };
            var cellHeightBySeenCategory = new Dictionary<ShapeCategory, List<int>>
            {
                [ShapeCategory.BaselineSitter] = [],
                [ShapeCategory.Descender] = [],
                [ShapeCategory.Ascender] = []
            };

            foreach (var (character, category) in ProbeSet)
            {
                var code = (ushort)character;
                var glyph = FontGlyphTable.FindByCode(glyphs, code);
                if (glyph is null) continue; // UA: не кожен ASCII-символ обов'язково є в цьому конкретному шрифті / EN: not every ASCII character is necessarily present in this specific font

                if (glyph.PageIndex >= font.TexturePages.Count) continue; // UA: аномалія — уже зафіксована в UvRectBoundsCheckCommand / EN: anomaly — already caught by UvRectBoundsCheckCommand

                var texPixels = FontTexturePixelReader.ReadMip0(font.TexturePages[glyph.PageIndex].Chunk);

                var x0 = (int)Math.Round(glyph.U0 * texPixels.Width);
                var x1 = (int)Math.Round(glyph.U1 * texPixels.Width);
                var y0 = (int)Math.Round(Math.Min(glyph.V0, glyph.V1) * texPixels.Height);
                var y1 = (int)Math.Round(Math.Max(glyph.V0, glyph.V1) * texPixels.Height);

                var minX = Math.Min(x0, x1);
                var maxX = Math.Max(x0, x1);
                var canvasWidth = maxX - minX;
                var canvasHeight = y1 - y0;

                if (canvasWidth <= 0 || canvasHeight <= 0)
                    continue; // UA: вироджений слот — уже зафіксовано в UvRectBoundsCheckCommand/RasterizerCanvasSizeCheckCommand / EN: degenerate slot — already caught elsewhere

                var topRowHasInk = RowHasInk(texPixels.RawA4R4G4B4Pixels, texPixels.Width, minX, maxX, y0);
                var bottomRowHasInk = RowHasInk(texPixels.RawA4R4G4B4Pixels, texPixels.Width, minX, maxX, y1 - 1);

                heightsBySeenCategory[category].Add(canvasHeight);
                topRowsBySeenCategory[category].Add(y0);
                bottomRowsBySeenCategory[category].Add(y1);
                xAdvanceBySeenCategory[category].Add(glyph.XAdvance);
                inkWidthBySeenCategory[category].Add(glyph.InkWidth);
                bearingBySeenCategory[category].Add(glyph.Bearing);
                cellHeightBySeenCategory[category].Add(glyph.CellHeight);

                report.Log($"    {character,-6}{category,-16}{canvasWidth,-8}{canvasHeight,-8}{glyph.XAdvance,-6}{glyph.InkWidth,-6}{glyph.Bearing,-8}{glyph.CellHeight,-6}{(topRowHasInk ? "так" : "ні"),-12}{(bottomRowHasInk ? "так" : "ні"),-12}");
            }

            report.Log();
            foreach (var category in (ShapeCategory[])[ShapeCategory.BaselineSitter, ShapeCategory.Descender, ShapeCategory.Ascender])
            {
                var heights = heightsBySeenCategory[category];
                var tops = topRowsBySeenCategory[category];
                var bottoms = bottomRowsBySeenCategory[category];

                var heightsDesc = heights.Count == 0
                    ? "(жодного знайденого символу з цієї категорії)"
                    : heights.Count == 1
                        ? $"стала висота {heights.First()}px для всіх"
                        : $"РІЗНА висота: {string.Join(", ", heights.OrderBy(h => h))}";

                var positionDesc = tops.Count == 0
                    ? ""
                    : $" | середній y0(абс)={tops.Average():F1}, середній y1(абс)={bottoms.Average():F1}";

                report.Log($"    Категорія {category}: {heightsDesc}{positionDesc}");

                if (tops.Count > 0)
                {
                    var avgXAdvance = xAdvanceBySeenCategory[category].Average();
                    var avgInkWidth = inkWidthBySeenCategory[category].Average();
                    var avgBearing = bearingBySeenCategory[category].Average();
                    var avgCellHeight = cellHeightBySeenCategory[category].Average();
                    report.Log($"        середнє: XAdvance={avgXAdvance:F1} InkWidth={avgInkWidth:F1} Bearing={avgBearing:F1} CellHeight={avgCellHeight:F1}");
                }
            }

            report.Log();
        }
    }

    private static bool RowHasInk(byte[] rawPixels, int textureWidth, int minX, int maxX, int y)
    {
        for (var x = minX; x < maxX; x++)
        {
            var pixelIndex = y * textureWidth + x;
            var (a, _, _, _) = FontTexturePixelReader.DecodePixel(rawPixels, pixelIndex);
            if (a > 0) return true;
        }
        return false;
    }
}
