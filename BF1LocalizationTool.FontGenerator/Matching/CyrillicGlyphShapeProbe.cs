// =============================================================================
// BF1LocalizationTool.FontGenerator — Matching/CyrillicGlyphShapeProbe.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Вимірює ПРИРОДНУ форму літери — растеризує її на ВЕЛИКОМУ
//     нейтральному квадратному canvas (без прив'язки до жодного
//     конкретного донорського слоту) і бере InkBounds уже перевіреного
//     GdiGlyphRasterizer. Мета — дізнатись "яка ця літера за формою" ще
//     ДО вибору донора, щоб підібрати донора під форму, а не навпаки.
//
//     Повертає і пропорцію (AspectRatio), і InkBounds.Height, бо самої
//     пропорції для підбору донора недостатньо: FilterOutTooSmall
//     пропускає донорів від 50% до 100%+ висоти великої літери — тобто в
//     межах "безпечних" донорів висота лишається дуже різною, а
//     GlyphBoxFitRenderer розтягує кожну літеру рівно під розмір ЇЇ
//     ВЛАСНОГО донора, без жодного узгодження з сусідніми літерами. Тому
//     GlyphDonorMatcher враховує при підборі ще й АБСОЛЮТНУ висоту
//     (InkBounds.Height), не лише пропорцію.
// EN: Measures a letter's NATURAL shape — rasterizes it on a LARGE
//     neutral square canvas (not tied to any specific donor slot) and
//     takes InkBounds from the already-verified GdiGlyphRasterizer.
//     Goal — learn "what shape is this letter" BEFORE picking a donor,
//     so the donor is matched to the shape, not the other way around.
//
//     Returns both the ratio (AspectRatio) and InkBounds.Height, because
//     the ratio alone is not enough to pick a good donor: FilterOutTooSmall
//     lets through donors anywhere from 50% to 100%+ of the capital
//     letter's height — so even among "safe" donors, height varies
//     wildly, and GlyphBoxFitRenderer stretches each letter to exactly
//     fill ITS OWN donor's size, with zero coordination with neighboring
//     letters. GlyphDonorMatcher therefore factors in ABSOLUTE height
//     (InkBounds.Height) too, not just ratio.
// =============================================================================

using BF1LocalizationTool.FontGenerator.Rasterization;

namespace BF1LocalizationTool.FontGenerator.Matching;

// UA: AspectRatio — Width/Height природної форми літери.
//     InkHeight — висота в пікселях ПРОБНОГО canvas (усі літери
//     растеризуються при ОДНАКОВОМУ ProbeFontSizePx, тому InkHeight
//     напряму порівнюваний між різними літерами ЦЬОГО алфавіту — не
//     потрібна окрема нормалізація "на льоту").
//     InkTop — ВЕРХНЯ межа чорнила В АБСОЛЮТНИХ координатах пробного
//     canvas (той самий BaselineY=300 для КОЖНОЇ літери алфавіту, тому
//     InkTop напряму порівнюваний між літерами — дає змогу визначити, чи
//     літера виступає ВИЩЕ звичайного "x-height" (вершник/діакритика:
//     б, і, ї, й, ф) чи має хвіст НИЖЧЕ базової лінії (у, р, ц, щ), а не
//     лише "наскільки вона вища за звичайні". Використовується для
//     розділення на "тіло" (core, спільного розміру для всіх літер
//     регістру) і "виступ" (margin, обмежений зверху/знизу — підхід,
//     запозичений з SteamWorld Heist) у GlyphBoxFitRenderer.
// EN: AspectRatio — Width/Height of the letter's natural shape.
//     InkHeight — height in pixels on the PROBE canvas (all letters are
//     rasterized at the SAME ProbeFontSizePx, so InkHeight is directly
//     comparable across different letters of THIS alphabet — no separate
//     "on the fly" normalization needed).
//     InkTop — the TOP edge of the ink in ABSOLUTE probe-canvas
//     coordinates (the SAME BaselineY=300 for every letter of the
//     alphabet, so InkTop is directly comparable across letters) — lets
//     us tell whether a letter extends ABOVE the ordinary x-height
//     (ascender/diacritic: б, і, ї, й, ф) or has a tail BELOW the
//     baseline (у, р, ц, щ), not just "how much taller than normal it
//     is overall". Used to split a letter into "core" (shared size
//     across the whole case) and "extension" (capped top/bottom margin —
//     an approach borrowed from SteamWorld Heist) in GlyphBoxFitRenderer.
public readonly record struct GlyphShapeMeasurement(double AspectRatio, int InkHeight, int InkTop);

public static class CyrillicGlyphShapeProbe
{
    // UA: Із запасом з усіх боків — жодна кирилична літера цього
    //     алфавіту не підходить настільки близько до країв, щоб
    //     обрізатись при FontSizePx=300 на canvas 400x400.
    // EN: Generous margin on all sides — no Cyrillic letter in this
    //     alphabet comes close enough to the edges to clip at
    //     FontSizePx=300 on a 400x400 canvas.
    private const int ProbeCanvasSize = 400;
    private const float ProbeFontSizePx = 300;
    private const int ProbeBaselineY = 300;

    public static GlyphShapeMeasurement MeasureShape(char character, string fontFamilyName)
    {
        var rasterizer = new GdiGlyphRasterizer();
        var options = new GlyphRasterizeOptions
        {
            FontFamilyName = fontFamilyName,
            FontSizePx = ProbeFontSizePx,
            CanvasWidth = ProbeCanvasSize,
            CanvasHeight = ProbeCanvasSize,
            BaselineY = ProbeBaselineY
        };

        var glyph = rasterizer.Rasterize(character, options);
        var ink = glyph.InkBounds;

        if (ink.Width <= 0 || ink.Height <= 0)
            throw new InvalidOperationException(
                $"UA: Гліф '{character}' шрифтом '{fontFamilyName}' не намалював жодного пікселя — перевір, чи шрифт підтримує цей символ. / " +
                $"EN: Glyph '{character}' with font '{fontFamilyName}' produced no pixels — check whether the font supports this character.");

        return new GlyphShapeMeasurement((double)ink.Width / ink.Height, ink.Height, ink.Top);
    }

    // UA: Лишається для зворотної сумісності — делегує MeasureShape.
    // EN: Kept for backward compatibility — delegates to MeasureShape.
    public static double MeasureAspectRatio(char character, string fontFamilyName) =>
        MeasureShape(character, fontFamilyName).AspectRatio;
}
