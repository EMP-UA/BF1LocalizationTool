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
//     MeasureShape повертає і AspectRatio, і InkBounds.Height — не лише
//     пропорцію. Це важливо: GlyphDonorMatcher підбирає донора не лише
//     за пропорцією, а й за АБСОЛЮТНОЮ висотою, бо FilterOutTooSmall
//     пропускає донорів від 50% до 100%+ висоти великої літери — тобто
//     в межах "безпечних" донорів висота лишається дуже різною, а
//     GlyphBoxFitRenderer розтягує кожну літеру рівно під розмір ЇЇ
//     ВЛАСНОГО донора, без жодного узгодження з сусідніми літерами. Без
//     урахування абсолютної висоти це призводить до "стрибучих" за
//     розміром літер у тому самому слові.
// EN: Measures a letter's NATURAL shape — rasterizes it on a LARGE
//     neutral square canvas (not tied to any specific donor slot) and
//     takes InkBounds from the already-verified GdiGlyphRasterizer.
//     Goal — learn "what shape is this letter" BEFORE picking a donor,
//     so the donor is matched to the shape, not the other way around.
//
//     MeasureShape returns both AspectRatio and InkBounds.Height — not
//     just the ratio. This matters: GlyphDonorMatcher matches a donor by
//     both ratio and ABSOLUTE height, because FilterOutTooSmall lets
//     through donors anywhere from 50% to 100%+ of the capital letter's
//     height — so even among "safe" donors, height varies wildly, and
//     GlyphBoxFitRenderer stretches each letter to exactly fill ITS OWN
//     donor's size, with zero coordination with neighboring letters.
//     Without factoring in absolute height, this leads to letters that
//     visibly "jump" in size within the same word.
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
//     лише "наскільки вона вища за звичайні". Додано для розділення
//     "тіло" (core, спільного розміру для всіх літер регістру) і
//     "виступ" (margin, обмежений зверху/знизу — SteamWorld Heist-підхід,
//     явно застосований підхід) у GlyphBoxFitRenderer.
// EN: AspectRatio — Width/Height of the letter's natural shape.
//     InkHeight — height in pixels on the PROBE canvas (all letters are
//     rasterized at the SAME ProbeFontSizePx, so InkHeight is directly
//     comparable across different letters of THIS alphabet — no separate
//     "on the fly" normalization needed).
//     InkTop — the TOP edge of the ink in ABSOLUTE probe-canvas
//     coordinates (the SAME BaselineY=300 for every letter of the
//     alphabet, so InkTop is directly comparable across letters) — this
//     shows whether a letter extends ABOVE the ordinary x-height
//     (ascender/diacritic: б, і, ї, й, ф) or has a tail BELOW the
//     baseline (у, р, ц, щ), not just "how much taller than normal it
//     is overall". Added to split a letter into "core" (shared size
//     across the whole case) and "extension" (capped top/bottom margin —
//     the SteamWorld Heist approach) in
//     GlyphBoxFitRenderer.
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
