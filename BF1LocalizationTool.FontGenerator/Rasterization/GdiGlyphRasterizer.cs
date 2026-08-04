// =============================================================================
// BF1LocalizationTool.FontGenerator — Rasterization/GdiGlyphRasterizer.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Реалізація IGlyphRasterizer через System.Drawing.Common (GDI+).
//     Windows-only — це ОК (узгоджено, як і в проєкті SteamWorld Heist).
//
//     КРИТИЧНИЙ ГОТЧА: НЕ малювати текст напряму на прозорому
//     Format32bppArgb растрі. GDI+ (Graphics.DrawString) некоректно
//     композитить анти-аліасинг проти нульової альфи — це дає чорну/темну
//     облямівку навколо кожного гліфа, бо GDI+ рендерить текст так, ніби
//     фон непрозорий чорний, і "просвічує" цей чорний колір крізь
//     напівпрозорі AA-пікселі краю літери.
//
//     ОБХІД ("luminance-to-alpha"):
//       1. Малюємо БІЛИЙ текст на ЧОРНОМУ НЕПРОЗОРОМУ фоні
//          (Format32bppRgb, без альфи — GDI+ тут коректно антиаліасить).
//       2. Яскравість (luminance = R-канал, бо R=G=B для білого тексту
//          без ClearType) кожного пікселя переносимо в Alpha фінального
//          зображення.
//       3. RGB фінального зображення форсуємо в (0xFF,0xFF,0xFF).
//
//     TextRenderingHint.AntiAliasGridFit (НЕ SystemDefault, НЕ
//     ClearTypeGridFit!) обов'язковий — ClearType дає різні значення
//     R/G/B на суб-пікселях краю літери (кольорова бахрома), що ЗЛАМАЄ
//     крок 2 (luminance перестає коректно відображати покриття).
//
//     ПІДТВЕРДЖЕНО (GlyphAtlasStyleAnalyzer, FONT_FORMAT_SPEC.md розділ
//     3.1): донорські гліфи в реальній текстурі BF1 мають RGB=білий і
//     лише варіативну Alpha — саме тому цей растеризатор форсує
//     RGB=(0xFF,0xFF,0xFF) і не зберігає жодного іншого кольору. Якщо це
//     припущення колись треба переглянути — коригується крок запису в
//     BODY (GlyphPixelConverter), растеризатор змінювати не треба.
// EN: IGlyphRasterizer implementation via System.Drawing.Common (GDI+).
//     Windows-only — that's fine (consistent with the SteamWorld Heist
//     project).
//
//     CRITICAL GOTCHA: do NOT draw text directly onto a transparent
//     Format32bppArgb bitmap. GDI+ (Graphics.DrawString) composites
//     anti-aliasing incorrectly against zero alpha — it produces a
//     black/dark fringe around every glyph, because GDI+ renders text as
//     if the background were opaque black, "bleeding" that black through
//     the semi-transparent AA edge pixels.
//
//     WORKAROUND ("luminance-to-alpha"):
//       1. Draw WHITE text on an OPAQUE BLACK background (Format32bppRgb,
//          no alpha — GDI+ anti-aliases correctly here).
//       2. Copy each pixel's luminance (= R channel, since R=G=B for
//          white text without ClearType) into the Alpha channel of the
//          final image.
//       3. Force the final image's RGB to (0xFF,0xFF,0xFF).
//
//     TextRenderingHint.AntiAliasGridFit (NOT SystemDefault, NOT
//     ClearTypeGridFit!) is mandatory — ClearType gives differing R/G/B
//     values on a glyph edge's sub-pixels (color fringing), which BREAKS
//     step 2 (luminance no longer correctly reflects coverage).
//
//     CONFIRMED (GlyphAtlasStyleAnalyzer, FONT_FORMAT_SPEC.md section
//     3.1): real donor glyphs in the BF1 texture have RGB=white with only
//     Alpha varying — which is exactly why this rasterizer forces
//     RGB=(0xFF,0xFF,0xFF) and never preserves any other color. Should
//     that assumption ever need revisiting, the BODY-writing step
//     (GlyphPixelConverter) is what would change, not this rasterizer.
// =============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace BF1LocalizationTool.FontGenerator.Rasterization;

public sealed class GdiGlyphRasterizer : IGlyphRasterizer
{
    public RasterizedGlyph Rasterize(char character, GlyphRasterizeOptions options)
    {
        if (options.CanvasWidth <= 0 || options.CanvasHeight <= 0)
            throw new ArgumentException(
                "UA: CanvasWidth/CanvasHeight мають бути > 0 / " +
                "EN: CanvasWidth/CanvasHeight must be > 0", nameof(options));

        var width = options.CanvasWidth;
        var height = options.CanvasHeight;

        // UA: Крок 1 — біле на чорному, БЕЗ альфи (Format32bppRgb).
        //     Саме відсутність альфа-каналу тут уникає GDI+ багу з
        //     чорною облямівкою.
        // EN: Step 1 — white on black, WITHOUT alpha (Format32bppRgb).
        //     The absence of an alpha channel here is exactly what
        //     avoids the GDI+ black-fringe bug.
        using var grayscaleBitmap = new Bitmap(width, height, PixelFormat.Format32bppRgb);

        // UA: `options.FontFamilyName` — ВІДНОСНИЙ ШЛЯХ файлу шрифту (не
        //     назва родини), і `PrivateFontRegistry.Get` повертає ПАРУ
        //     (FontFamily, FontStyle), де стиль ГАРАНТОВАНО той, що несе
        //     САМЕ ЦЕЙ файл (ізольована колекція на файл,
        //     PrivateFontRegistry.cs). Це важливо, бо коли кілька .ttf
        //     зливаються в ОДНУ family (класичний GDI-квартет
        //     Regular/Bold/Italic/BoldItalic — родина "Fira Sans" без
        //     суфікса), `new Font(family, size, FontStyle.Regular, unit)`
        //     не завжди чесно обирає саме Regular (розділ 11.13
        //     FONT_FORMAT_SPEC.md). `options.Style` не використовується
        //     для приватних шрифтів (див. GlyphRasterizeOptions).
        // EN: `options.FontFamilyName` is a RELATIVE FILE PATH (not a
        //     family name), and `PrivateFontRegistry.Get` returns a
        //     (FontFamily, FontStyle) PAIR whose style is GUARANTEED to
        //     match what THIS file actually carries (one isolated
        //     collection per file, PrivateFontRegistry.cs). This matters
        //     because when several .ttf files merge into ONE family (the
        //     classic GDI quartet Regular/Bold/Italic/BoldItalic — bare
        //     "Fira Sans"), `new Font(family, size, FontStyle.Regular, unit)`
        //     does not always honestly pick Regular (FONT_FORMAT_SPEC.md
        //     section 11.13). `options.Style` is not used for private
        //     fonts (see GlyphRasterizeOptions).
        var (resolvedFamily, resolvedStyle) = PrivateFontRegistry.Get(options.FontFamilyName);

        using (var g = Graphics.FromImage(grayscaleBitmap))
        using (var font = new Font(resolvedFamily, options.FontSizePx, resolvedStyle, GraphicsUnit.Pixel))
        {
            // UA: g.Clear() тут ДОЗВОЛЕНО (і обов'язково) — це власний
            //     ізольований canvas одного гліфа, а НЕ спільний атлас.
            //     Заборона "g.Clear() forbidden" зі SteamWorld Heist
            //     стосується запису В АТЛАС (GlyphAtlasPatcher.ApplyPixelPatch,
            //     AtlasPatching-проєкт) — там Clear() стер би сусідні
            //     гліфи. Тут такого ризику немає.
            // EN: g.Clear() is ALLOWED here (and required) — this is a
            //     private, isolated single-glyph canvas, NOT the shared
            //     atlas. The "g.Clear() forbidden" rule from SteamWorld
            //     Heist applies to WRITING INTO THE ATLAS
            //     (GlyphAtlasPatcher.ApplyPixelPatch, the AtlasPatching
            //     project) — there, Clear() would erase neighboring
            //     glyphs. No such risk here.
            g.Clear(Color.Black);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var text = character.ToString();

            // UA: Позиціонування за базовою лінією: BaselineY заданий
            //     викликаючим кодом (формула тут не вираховується).
            //     Використовуємо ascent шрифту, щоб верх гліфа опинився
            //     на BaselineY - ascent. GetCellAscent/GetEmHeight
            //     запитуються для resolvedStyle (не options.Style) —
            //     метрики мають відповідати ТОМУ САМОМУ стилю, яким
            //     РЕАЛЬНО намальовано текст (font сконструйований з
            //     resolvedStyle вище) — інакше, для родин зі злитими
            //     стилями (де resolvedStyle міг би виявитись не Regular),
            //     ascent рахувався б за ЧУЖИМ стилем.
            // EN: Baseline positioning: BaselineY is supplied by the
            //     caller (formula not derived here). We use the font's
            //     ascent so the glyph's top lands at BaselineY - ascent.
            //     GetCellAscent/GetEmHeight are queried for resolvedStyle
            //     (not options.Style) — the metrics must match the SAME
            //     style the text was ACTUALLY drawn with (the font was
            //     constructed with resolvedStyle above) — otherwise, for
            //     families with merged styles (where resolvedStyle could
            //     turn out not to be Regular), ascent would be computed
            //     against the WRONG style.
            var ascentPx = font.FontFamily.GetCellAscent(resolvedStyle) *
                           font.Size / font.FontFamily.GetEmHeight(resolvedStyle);

            var drawX = 0f;
            var drawY = options.BaselineY - ascentPx;

            g.DrawString(text, font, Brushes.White, drawX, drawY,
                StringFormat.GenericTypographic);
        }

        // UA: Крок 2+3 — переносимо luminance → alpha, форсуємо RGB=білий.
        // EN: Step 2+3 — copy luminance → alpha, force RGB=white.
        var bgraPixels = new byte[width * height * 4];
        var grayscaleData = grayscaleBitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppRgb);

        int minX = width, minY = height, maxX = -1, maxY = -1;

        try
        {
            unsafe
            {
                var srcBase = (byte*)grayscaleData.Scan0;
                var stride = grayscaleData.Stride;

                for (var y = 0; y < height; y++)
                {
                    var srcRow = srcBase + y * stride;
                    for (var x = 0; x < width; x++)
                    {
                        // UA: Format32bppRgb порядок байтів: B, G, R, (X=255)
                        // EN: Format32bppRgb byte order: B, G, R, (X=255)
                        var r = srcRow[x * 4 + 2];

                        // UA: R=G=B для чисто білого тексту без ClearType —
                        //     беремо R як luminance/alpha напряму, без
                        //     формули зваженої яскравості (не потрібна,
                        //     бо канали й так рівні).
                        // EN: R=G=B for pure white text without ClearType —
                        //     take R as luminance/alpha directly, no
                        //     weighted-luminance formula needed (channels
                        //     are already equal).
                        var alpha = r;

                        var dstIndex = (y * width + x) * 4;
                        bgraPixels[dstIndex + 0] = 0xFF; // B
                        bgraPixels[dstIndex + 1] = 0xFF; // G
                        bgraPixels[dstIndex + 2] = 0xFF; // R
                        bgraPixels[dstIndex + 3] = alpha; // A

                        if (alpha == 0) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
        }
        finally
        {
            grayscaleBitmap.UnlockBits(grayscaleData);
        }

        // UA: maxX == -1 означає "жодного видимого пікселя" (напр. пробіл)
        // EN: maxX == -1 means "no visible pixels" (e.g. a space)
        var inkBounds = maxX == -1
            ? new Rectangle(0, 0, 0, 0)
            : new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);

        return new RasterizedGlyph
        {
            Width = width,
            Height = height,
            BgraPixels = bgraPixels,
            InkBounds = inkBounds
        };
    }
}