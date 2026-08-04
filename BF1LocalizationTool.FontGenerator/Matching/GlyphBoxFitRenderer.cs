// =============================================================================
// BF1LocalizationTool.FontGenerator — Matching/GlyphBoxFitRenderer.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Рендерить символ так, щоб він ЩІЛЬНО заповнив донорський слот
//     точно (targetWidth×targetHeight): БЕЗ ОБРІЗАННЯ (ламає форму
//     штриха), лише масштабування (навіть нерівномірне, якщо
//     GlyphDonorMatcher не знайшов ідеальної пропорції — це помітно менш
//     руйнівно за обрізання).
//
//     Кроки:
//       1. Растеризувати символ на ВЕЛИКОМУ нейтральному canvas (той
//          самий підхід, що й CyrillicGlyphShapeProbe) — отримати
//          InkBounds (реальні межі чорнила).
//       2. Вирізати ЛИШЕ прямокутник InkBounds (без порожніх полів
//          навколо).
//       3. Розтягнути цей вирізаний шматок точно до
//          targetWidth×targetHeight (System.Drawing, high-quality
//          bicubic) — тут і відбувається компенсація невдалого підбору
//          донора, якщо пропорції не збіглись ідеально.
// EN: Renders a character so it TIGHTLY fills the donor slot exactly
//     (targetWidth×targetHeight): NO CROPPING (breaks stroke shape), only
//     scaling (even non-uniform, if GlyphDonorMatcher didn't find a
//     perfect ratio match — noticeably less destructive than cropping).
//
//     Steps:
//       1. Rasterize the character on a LARGE neutral canvas (same
//          approach as CyrillicGlyphShapeProbe) — get InkBounds (the
//          actual ink boundaries).
//       2. Crop OUT ONLY the InkBounds rectangle (no empty margins
//          around it).
//       3. Stretch that cropped piece exactly to
//          targetWidth×targetHeight (System.Drawing, high-quality
//          bicubic) — this is where any imperfect donor-ratio match gets
//          compensated for.
// =============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using BF1LocalizationTool.FontGenerator.Rasterization;

namespace BF1LocalizationTool.FontGenerator.Matching;

[SupportedOSPlatform("windows")]
public static class GlyphBoxFitRenderer
{
    // UA: Той самий запас, що й у CyrillicGlyphShapeProbe — жодна літера
    //     цього алфавіту не обрізається на такому canvas.
    // EN: Same margin as CyrillicGlyphShapeProbe — no letter in this
    //     alphabet clips on this canvas.
    private const int ProbeCanvasSize = 400;
    private const float ProbeFontSizePx = 300;
    private const int ProbeBaselineY = 300;

    // UA: Гарантований прозорий буфер з УСІХ чотирьох боків слоту, для
    //     КОЖНОЇ літери (обидва регістри, RenderToFit і
    //     RenderWithCoreAndMargin). ПРИЧИНА: для ШИРОКИХ літер (natWidth
    //     >= boxWidth — Ж/Ф/Х/Ш/Щ/Д-подібні) тіло масштабується РІВНО під
    //     boxWidth — чорнило впритул до лівого/правого краю слоту, 0px
    //     поля. Це класичний тригер білінійної/mip GPU-фільтрації
    //     текстур: коли чорнило впритул до межі UV-прямокутника,
    //     фільтрація "просочує" сусідній слот на межі — видима тонка
    //     рамка САМЕ на щільно заповнених літерах (підтверджено:
    //     GlyphOccupancyOverlayCommand показує, що самі дані текстури
    //     чисті — кути слотів чорні, жодної рамки в пікселях, тобто
    //     артефакт виникає у GPU-фільтрації, а не в даних). 1px
    //     гарантованого прозорого поля з кожного боку прибирає джерело
    //     (чорнило більше НІКОЛИ не торкається межі слоту), незалежно
    //     від того, чи причина справді у фільтрації.
    // EN: A guaranteed transparent buffer on ALL FOUR sides of the slot,
    //     for EVERY letter (both cases, RenderToFit and
    //     RenderWithCoreAndMargin). REASON: for WIDE letters (natWidth >=
    //     boxWidth — Ж/Ф/Х/Ш/Щ/Д-type) the core is scaled to EXACTLY
    //     boxWidth — ink flush against the slot's left/right edge, 0px
    //     margin. That's the classic trigger for GPU bilinear/mip texture
    //     filtering: when ink sits flush against the UV rect boundary,
    //     filtering bleeds the neighboring slot in at the edge — a
    //     visible thin frame on exactly the tightly-packed letters
    //     (confirmed: GlyphOccupancyOverlayCommand shows the texture data
    //     itself is clean — slot corners are black, no frame baked into
    //     the pixels, so the artifact originates in GPU filtering, not
    //     the data). A guaranteed 1px transparent margin on every side
    //     removes the source (ink never touches the slot boundary
    //     again), regardless of whether filtering bleed is the exact
    //     mechanism.
    // UA: Оголошено internal (не private), бо
    //     GlyphMetricModel.ComputeLowercaseCoreMetric МУСИТЬ ЗНАТИ це
    //     число, щоб додати 2×EdgeInsetPx запасу до фінального boxHeight,
    //     який повертає (див. коментар там) — інакше той самий
    //     гарантований відступ віднімався б ДВІЧІ: раз під час
    //     "натурального" виміру (де він не мав би ефекту — probe-запас
    //     250px величезний), і ще раз під час фінального рендеру (де
    //     тісний бокс уже точно під натуральний розмір, тож -2px там
    //     непотрібно "з'їдали" б виступ, якого не мали чіпати).
    // EN: Declared internal (not private), because
    //     GlyphMetricModel.ComputeLowercaseCoreMetric MUST KNOW this
    //     number to add 2×EdgeInsetPx of headroom to the final boxHeight
    //     it returns (see the comment there) — otherwise the same
    //     guaranteed margin would get subtracted TWICE: once during the
    //     "natural" measurement pass (where it would have no effect — the
    //     250px probe headroom is huge), and once during the final render
    //     pass (where the box is already sized exactly to the natural
    //     extent, so an extra -2px there would needlessly "eat into" an
    //     extension that was never supposed to be touched).
    // UA: Це "поле, на яке викликач РОЗШИРЮЄ слот навколо незмінного
    //     чорнила": фізичний UV-прямокутник = чорнило + 2×SlotPaddingPx з
    //     кожної осі, чорнило рендериться у свій повний розмір і
    //     центрується. Курсорні метрики (InkWidth/XAdvance) лишаються від
    //     ЧОРНИЛА, тож міжлітерні відстані не змінюються; Bearing/
    //     CellHeight розширюються на 1px з кожного боку, щоб екранний
    //     бокс ріс разом зі слотом і масштаб лишався 1:1 (інакше гра
    //     стиснула б вищий слот у ту саму екранну висоту й літера
    //     знову змаліла б).
    // EN: This is "the margin the caller GROWS the slot by, around
    //     unchanged ink": the physical UV rect = ink + 2×SlotPaddingPx on
    //     each axis, the ink is rendered at its full size and centred.
    //     Cursor metrics (InkWidth/XAdvance) stay derived from the INK,
    //     so letter spacing is unchanged; Bearing/CellHeight are extended
    //     by 1px on each side so the on-screen box grows with the slot
    //     and the mapping stays 1:1 (otherwise the game would squeeze the
    //     taller slot into the same screen height and the letter would
    //     shrink again).
    public const int SlotPaddingPx = 1;

    // UA: Лишено для сумісності зі старими викликами/коментарями; більше
    //     НЕ використовується для віднімання від чорнила.
    // EN: Kept for compatibility with older callers/comments; no longer
    //     used to subtract from the ink.
    internal const int EdgeInsetPx = 1;

    // UA: "Спільна висота для регістру + доповнення" замість "розтягнути
    //     точно під розмір донора". Причина: без спільного обмеження по
    //     висоті та нижній лінії RenderToFit розтягує (навіть НЕРІВНОМІРНО
    //     по X і Y окремо) КОЖНУ
    //     літеру рівно під розмір ЇЇ ВЛАСНОГО донора — а розміри донорів
    //     фізично різні (успадковані від чужих гліфів), тож навіть коли
    //     підбір пропорції вдалий для КОЖНОЇ літери окремо, візуально
    //     сусідні літери в одному слові виглядають різного розміру, бо
    //     немає СПІЛЬНОГО критерію висоти між ними.
    //
    //     Цей метод: 1) рахує НАТУРАЛЬНУ пропорцію літери (без спотворення
    //     — ширина/висота InkBounds), 2) масштабує до targetHeight (той
    //     самий для ВСІХ літер цього регістру шрифту), 3) якщо натуральна
    //     ширина при targetHeight НЕ влазить у boxWidth — зменшує (ніколи
    //     не збільшує понад targetHeight) so natural aspect ratio ніколи
    //     не спотворюється, лише масштаб змінюється, 4) доповнює
    //     прозорими пікселями до boxWidth×boxHeight: по X — по центру; по
    //     Y — ЗАЛЕЖНО від того, чи ЦЯ КОНКРЕТНА літера має справжній хвіст
    //     ПІД базовою лінією (виміряно відносно ProbeBaselineY, спільного
    //     для всього алфавіту): якщо ні (звичайні літери, і "і"/"ї"/"й" —
    //     ці ВИЩІ через виступ ЗВЕРХУ, не хвіст знизу) — знизу (база = низ
    //     клітинки); якщо так (у, р, ц, щ — реальний хвіст) — зверху (тіло
    //     вирівнюється з рештою по верху, хвіст звисає в зарезервований
    //     простір знизу). Деталі — коментар нижче біля обчислення
    //     hasDescender. Прямого поля "baseline" в FBOD немає (підтверджено,
    //     FontGlyphRecord.cs) — це найкраще наближення без нього.
    //
    //     РЕЗУЛЬТАТ: замість "усі різні через розтягування під різні
    //     донори", переважна більшість літер регістру виходять РІВНО
    //     targetHeight заввишки (однаковий розмір), і жодна НЕ спотворена
    //     нерівномірним розтягуванням by X vs Y окремо (RenderToFit МІГ
    //     розтягувати по X і Y різними коефіцієнтами — цей метод завжди
    //     зберігає один спільний коефіцієнт).
    // EN: "Shared height per case + padding" instead of "stretch to
    //     exactly fill the donor's size". Reason: without a shared
    //     height/baseline constraint, RenderToFit stretches (even
    //     NON-UNIFORMLY in X and Y separately) EVERY
    //     letter to exactly fill ITS OWN donor's size — but donor sizes
    //     are physically different (inherited from unrelated glyphs), so
    //     even when the ratio match is good for EACH letter individually,
    //     neighboring letters in the same word visually look
    //     different-sized, because there's no SHARED height criterion
    //     between them.
    //
    //     This method: 1) computes the letter's NATURAL proportion
    //     (undistorted — InkBounds width/height), 2) scales to
    //     targetHeight (the SAME for ALL letters of this font's case),
    //     3) if the natural width at targetHeight doesn't fit boxWidth —
    //     shrinks (never grows beyond targetHeight) so the natural aspect
    //     ratio is NEVER distorted, only the overall scale changes, 4)
    //     pads with transparent pixels to boxWidth×boxHeight: horizontally
    //     centered, vertically anchored to the BOTTOM (so every letter's
    //     bottom lands on the same level — an approximation of a shared
    //     baseline, since FBOD has no direct "baseline" field — confirmed,
    //     FontGlyphRecord.cs).
    //
    //     RESULT: instead of "everyone different because stretched to
    //     different donors", the large majority of letters in a case come
    //     out EXACTLY targetHeight tall (uniform size), and NONE are
    //     distorted by independent X-vs-Y stretch factors (RenderToFit
    //     COULD stretch X and Y by different factors — this method always
    //     preserves one shared scale factor).
    public static RasterizedGlyph RenderWithSharedHeight(
        char character, string fontFamilyName, int targetHeight, int boxWidth, int boxHeight)
    {
        if (targetHeight <= 0 || boxWidth <= 0 || boxHeight <= 0)
            throw new ArgumentException(
                $"UA: targetHeight/boxWidth/boxHeight мають бути > 0 (отримано {targetHeight}/{boxWidth}x{boxHeight}) / " +
                $"EN: targetHeight/boxWidth/boxHeight must be > 0 (got {targetHeight}/{boxWidth}x{boxHeight})");

        var rasterizer = new GdiGlyphRasterizer();
        var probeOptions = new GlyphRasterizeOptions
        {
            FontFamilyName = fontFamilyName,
            FontSizePx = ProbeFontSizePx,
            CanvasWidth = ProbeCanvasSize,
            CanvasHeight = ProbeCanvasSize,
            BaselineY = ProbeBaselineY
        };

        var probe = rasterizer.Rasterize(character, probeOptions);
        var ink = probe.InkBounds;

        if (ink.Width <= 0 || ink.Height <= 0)
            throw new InvalidOperationException(
                $"UA: Гліф '{character}' шрифтом '{fontFamilyName}' не намалював жодного пікселя. / " +
                $"EN: Glyph '{character}' with font '{fontFamilyName}' produced no pixels.");

        var naturalAspect = ink.Width / (double)ink.Height;

        // UA: Малювання нижче ГАРАНТОВАНО влазить у [0,boxWidth]x
        //     [0,boxHeight] — жодного обрізання пікселів немає, лише
        //     масштабування. heightLimit = МЕНША з (targetHeight,
        //     boxHeight) — ОБМЕЖЕНА одразу, ДО того, як рахуємо ширину.
        //     Ширина рахується ВІД heightLimit, тож завжди узгоджена з
        //     висотою (для НАЙТІСНІШИХ донорів — боксів МЕНШИХ за
        //     targetHeight — обмеження висоти ДО обчислення ширини
        //     критично важливе: інакше ширина й висота розходяться і
        //     пропорція СПОТВОРЮЄТЬСЯ, нерівномірне стиснення). Якщо
        //     навіть ця ширина не влазить у boxWidth — зменшуємо висоту
        //     ще раз ВІД цієї ширини (той самий прийом, коректний в обидва
        //     боки).
        // EN: The draw call below is GUARANTEED to fit within
        //     [0,boxWidth]x[0,boxHeight] — no pixel cropping happens,
        //     only scaling. heightLimit = the SMALLER of (targetHeight,
        //     boxHeight) — capped UP FRONT, BEFORE computing width. Width
        //     is derived FROM heightLimit, so it's always consistent with
        //     the height (for the TIGHTEST donors — boxes SMALLER than
        //     targetHeight — capping height before deriving width matters:
        //     otherwise width and height would no longer match each
        //     other, DISTORTING the proportion via a non-uniform squeeze).
        //     If even that width doesn't fit boxWidth — shrink the height
        //     again FROM that width (the same trick, correct both ways).
        var heightLimit = Math.Min(targetHeight, boxHeight);
        var effectiveHeight = heightLimit;
        var effectiveWidth = (int)Math.Round(effectiveHeight * naturalAspect);
        if (effectiveWidth > boxWidth)
        {
            effectiveWidth = boxWidth;
            effectiveHeight = (int)Math.Round(effectiveWidth / naturalAspect);
        }
        effectiveHeight = Math.Clamp(effectiveHeight, 1, boxHeight);
        effectiveWidth = Math.Clamp(effectiveWidth, 1, boxWidth);

        // UA: "і" і "у" обидві "виносні", але виступ у РІЗНІ боки —
        //     крапка над "і" має сідати на СПІЛЬНУ БАЗОВУ ЛІНІЮ знизу, як
        //     і звичайні літери, а хвіст "у" має звисати ПІД цю лінію.
        //     Приклеювання ВСІХ "високих" літер до низу клітинки вирівняло
        //     б їх за верхом хвоста "у", а не за базовою лінією — для "і"
        //     це неправильно.
        //
        //     ProbeBaselineY (=300, той самий канвас, що й в усіх
        //     вимірах цього алфавіту) — СПІЛЬНА базова лінія для КОЖНОЇ
        //     літери, бо всі растеризуються з ОДНАКОВИМ BaselineY. Тому
        //     ink.Bottom-ProbeBaselineY каже: чи ЦЯ КОНКРЕТНА літера
        //     фізично звисає під базову лінію (справжній хвіст, "у","р",
        //     "ц","щ") чи вся сидить НА чи ВИЩЕ неї (звичайні літери,
        //     і "і"/"ї"/"й"/"б"/"ф" — вони ВИЩІ через виступ ВГОРІ, а не
        //     хвіст знизу). Поріг 8% власної висоти — щоб шум
        //     рендерингу/округлення не хибно позначив звичайну літеру
        //     як "з хвостом".
        //
        //     Літери БЕЗ хвоста (descentPx мала) — низом до низу клітинки
        //     (база = низ клітинки).
        //     Літери З хвостом (у, р, ц, щ...) — ВЕРХОМ до верху клітинки
        //     (щоб їхня "тіла" вирівнювалось по верху так само, як у
        //     звичайних літер, а хвіст звисав у ЗАРЕЗЕРВОВАНИЙ додатковий
        //     простір знизу — той самий принцип, що réserve 10% зверху/
        //     знизу під хвости в SteamWorld Heist, лише тут це виходить
        //     природно з того, що "хвостаті" літери вже отримали ВИЩУ
        //     спільну ціль-висоту через кластеризацію в
        //     CyrillicFontInjector).
        // EN: "і" and "у" are both "tall", but the extra height goes in
        //     OPPOSITE directions — the dot above "і" should sit on the
        //     SAME shared BASELINE at the bottom, like ordinary letters,
        //     while "у"'s tail should hang BELOW that line.
        //     Bottom-anchoring ALL "tall" letters would align them by the
        //     top of "у"'s tail, not by the baseline — wrong for "і".
        //
        //     ProbeBaselineY (=300, the same canvas used for every
        //     measurement in this alphabet) is a SHARED baseline for
        //     EVERY letter, since all are rasterized with the SAME
        //     BaselineY. So ink.Bottom-ProbeBaselineY tells us: does THIS
        //     SPECIFIC letter physically hang below the baseline (a real
        //     descender, "у","р","ц","щ") or does it sit entirely ON or
        //     ABOVE it (ordinary letters, and "і"/"ї"/"й"/"б"/"ф" — those
        //     are TALLER because of extension ABOVE, not a tail below). An
        //     8%-of-own-height threshold guards against rendering/rounding
        //     noise falsely flagging an ordinary letter as "has a tail".
        //
        //     Letters WITHOUT a tail (small descentPx) — bottom-anchored
        //     (baseline = box bottom).
        //     Letters WITH a tail (у, р, ц, щ...) — TOP-anchored (so their
        //     "body" aligns at the top the same way ordinary letters do,
        //     and the tail hangs into the EXTRA reserved space at the
        //     bottom — the same idea as reserving 10% top/bottom for
        //     tails in SteamWorld Heist, except here it falls out
        //     naturally from "tailed" letters already getting a TALLER
        //     shared target height via the clustering in
        //     CyrillicFontInjector).
        var descentPx = ink.Bottom - ProbeBaselineY;
        var hasDescender = descentPx > ink.Height * 0.08;

        using var probeBitmap = BgraBytesToBitmap(probe.BgraPixels, ProbeCanvasSize, ProbeCanvasSize);
        using var croppedBitmap = probeBitmap.Clone(ink, PixelFormat.Format32bppArgb);
        using var resizedBitmap = ProgressiveResize(croppedBitmap, effectiveWidth, effectiveHeight);

        var offsetX = (boxWidth - effectiveWidth) / 2;
        var offsetY = hasDescender
            ? 0                                 // UA: верхом — тіло вирівнюється з рештою, хвіст звисає вниз / EN: top-anchored — body aligns with the rest, tail hangs down
            : boxHeight - effectiveHeight;       // UA: низом — база сідає на спільну нижню лінію / EN: bottom-anchored — base sits on the shared line

        using var paddedBitmap = new Bitmap(boxWidth, boxHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(paddedBitmap))
        {
            g.Clear(Color.Transparent);
            g.DrawImage(resizedBitmap, offsetX, offsetY, effectiveWidth, effectiveHeight);
        }

        var resultBytes = BitmapToBgraBytes(paddedBitmap, boxWidth, boxHeight);

        return new RasterizedGlyph
        {
            Width = boxWidth,
            Height = boxHeight,
            BgraPixels = resultBytes,
            InkBounds = new Rectangle(offsetX, offsetY, effectiveWidth, effectiveHeight)
        };
    }

    // UA: Альтернатива RenderWithSharedHeight для рендеру з окремим
    //     масштабуванням тіла й виступів (RenderWithSharedHeight лишається
    //     в файлі як єдиний affine-варіант, не викликається продакшн-кодом,
    //     лишена для довідки; основний рендер ін'єкції — RenderToFit,
    //     CyrillicFontInjector.GrowAndRenderGlyphs).
    //     ПРИЧИНА: RenderWithSharedHeight масштабує ВЕСЬ гліф ОДНИМ
    //     спільним коефіцієнтом до targetHeight = scale × TargetHeightFraction.
    //     Це МАТЕМАТИЧНО єдиний affine-масштаб на всю літеру — а отже і
    //     "тіло" (x-height частина), і "хвіст"/"виступ" (діакритика,
    //     висхідний/спускний елемент) масштабуються РАЗОМ, на ту саму
    //     пропорцію. Виміряні TargetHeightFraction ('ф'=113%, 'й'=110%,
    //     'у'/'р'/'б'=101%, 'ц'/'д'/'щ'=90% проти 'а'/'о'=74%) означають,
    //     що 'ф' виходить ~53% БІЛЬШОЮ за 'а' ЦІЛКОМ — включно з тілом, не
    //     лише виступом. У грі, на дрібних піксельних шрифтах (бокси
    //     7-30px), це читається як "інша, більша літера" (капсоподібний
    //     ефект), а не як природний виступ.
    //
    //     Підхід — резервувати частину клітинки зверху й знизу під
    //     виносні елементи, а не масштабувати всю літеру одним
    //     коефіцієнтом (запозичено з SteamWorld Heist), — вимагає ОКРЕМОГО
    //     масштабування тіла й виступів, тож не міг бути реалізований у
    //     межах RenderWithSharedHeight, який математично не може
    //     розділити тіло й виступ (один affine-коефіцієнт масштабує все
    //     разом).
    //
    //     Цей метод РОЗДІЛЯЄ гліф по вертикалі на "тіло" (core — перетин
    //     природного чорнила з еталонною смугою x-height [coreTopY,
    //     ProbeBaselineY], та сама для ВСІХ літер регістру) і до ДВОХ
    //     "виступів" (extension — частина чорнила ВИЩЕ/НИЖЧЕ цієї смуги):
    //       1. Тіло масштабується до coreHeight — ОДНАКОВОГО для ВСІХ
    //          літер регістру (як RenderWithSharedHeight, але ЛИШЕ для
    //          тіла, не для всього гліфа).
    //       2. Кожен виступ масштабується ТИМ САМИМ коефіцієнтом, що й
    //          тіло (щоб товщина штриха лишалась узгодженою), АЛЕ якщо
    //          результат перевищує marginCapPx — стискається ЦІЛІСНО
    //          (зі збереженням власної пропорції) до marginCapPx. Це і є
    //          "резервоване поле" — виступ НІКОЛИ не роздуває тіло, лише
    //          сам обмежений зверху.
    //       3. Якщо тіло+виступи разом не влазять у boxHeight — спершу
    //          стискаються виступи (другорядні), і лише як останній
    //          засіб — тіло теж (рідкісний випадок найтісніших донорів).
    // EN: An alternative to RenderWithSharedHeight that scales the core
    //     and extensions separately (RenderWithSharedHeight stays in the
    //     file as the single-affine variant, not called by production
    //     code, kept for reference; the injection's actual render path is
    //     RenderToFit, via CyrillicFontInjector.GrowAndRenderGlyphs).
    //     REASON: RenderWithSharedHeight scales the WHOLE glyph by ONE
    //     shared factor to targetHeight = scale × TargetHeightFraction.
    //     That is MATHEMATICALLY a single affine scale for the entire
    //     letter — so both the "core" (x-height part) and the
    //     "tail"/"extension" (diacritic, ascender/descender) get scaled
    //     TOGETHER, by the same ratio. Measured TargetHeightFraction
    //     values ('ф'=113%, 'й'=110%, 'у'/'р'/'б'=101%, 'ц'/'д'/'щ'=90%
    //     vs 'а'/'о'=74%) mean 'ф' comes out ~53% BIGGER than 'а'
    //     ENTIRELY — including the core, not just the extension. In-game,
    //     on tiny pixel fonts (7-30px boxes), that reads as "a different,
    //     bigger letter" (a capital-like artifact), not a natural
    //     extension.
    //
    //     The approach — reserving part of the cell at the top and
    //     bottom for extensions instead of scaling the whole letter by
    //     one factor (borrowed from SteamWorld Heist) — requires scaling
    //     the core and the extensions SEPARATELY, so it could not be
    //     implemented within RenderWithSharedHeight, which mathematically
    //     cannot separate core from extension (one affine factor scales
    //     everything together).
    //
    //     This method SPLITS the glyph vertically into a "core" (the
    //     overlap of natural ink with the reference x-height band
    //     [coreTopY, ProbeBaselineY], the SAME band for EVERY letter of
    //     the case) and up to TWO "extensions" (ink ABOVE/BELOW that
    //     band):
    //       1. The core is scaled to coreHeight — the SAME for EVERY
    //          letter of the case (like RenderWithSharedHeight, but ONLY
    //          for the core, not the whole glyph).
    //       2. Each extension is scaled by the SAME factor as the core
    //          (so stroke thickness stays consistent), BUT if the result
    //          exceeds marginCapPx — it's shrunk AS A WHOLE (preserving
    //          its own aspect) down to marginCapPx. This IS the
    //          "reserved margin" — an extension can NEVER inflate the
    //          core, it's only capped at the top itself.
    //       3. If core+extensions together don't fit boxHeight — the
    //          extensions (secondary) shrink first, and only as a last
    //          resort does the core shrink too (rare, only the tightest
    //          donors).
    // UA: Усі проміжні геометричні значення обчислення тіло+виступи —
    //     винесено в окрему структуру, щоб Diagnostic-шар міг ЗАПИТАТИ
    //     ці самі числа (через ComputeCoreMarginLayout нижче) для
    //     логування, БЕЗ дублювання математики й БЕЗ порушення правила
    //     "FontGenerator не посилається на Diagnostic" (просто повертає
    //     дані, а не малює/логує їх сам).
    // EN: All intermediate core+extension geometry values — extracted
    //     into a struct so the Diagnostic layer can QUERY these exact
    //     numbers (via ComputeCoreMarginLayout below) for logging,
    //     WITHOUT duplicating the math and WITHOUT breaking the
    //     "FontGenerator doesn't reference Diagnostic" rule (it just
    //     returns data, doesn't log/draw it itself).
    // UA: NatAboveRenderH/NatBelowRenderH — висота виступу ПІСЛЯ
    //     стиснення під ширину боксу, але ДО обмеження marginCapPx.
    //     Порівняння з фінальним AboveRenderH/BelowRenderH дає змогу
    //     відрізнити ДВА РІЗНІ механізми обрізання: (а) marginCapPx
    //     зрізає РЕАЛЬНИЙ виступ (NatAboveRenderH > marginCapPx →
    //     AboveRenderH=marginCapPx, справжній хвіст/діакритика втрачені),
    //     від (б) стиснення під ширину, яке зменшує ВИСОТУ ТІЛА
    //     (CoreRenderH < coreHeight, коли NatWidth > BoxWidth) — це
    //     ДВА окремих джерела непослідовного розміру, а не одне.
    // EN: NatAboveRenderH/NatBelowRenderH — extension height AFTER
    //     width-fit shrink, but BEFORE the marginCapPx limit. Comparing
    //     to the final AboveRenderH/BelowRenderH tells apart TWO
    //     DIFFERENT clipping mechanisms: (a) marginCapPx cutting a REAL
    //     extension (NatAboveRenderH > marginCapPx → AboveRenderH capped,
    //     a genuine tail/diacritic gets lost), vs (b) width-fit shrink
    //     reducing the CORE HEIGHT itself (CoreRenderH < coreHeight, when
    //     NatWidth > BoxWidth) — TWO separate sources of inconsistent
    //     size, not one.
    public readonly record struct CoreMarginLayout(
        int InkLeft, int InkTop, int InkRight, int InkBottom,
        int CoreTopY, int CoreBandHeight, double CoreScale,
        int CoreT, int CoreB, int AboveSrcH, int BelowSrcH,
        int NatWidth,
        int CoreRenderW, int CoreRenderH,
        int NatAboveRenderH, int NatBelowRenderH,
        int AboveRenderW, int AboveRenderH,
        int BelowRenderW, int BelowRenderH,
        int CoreTop, int CoreBottom, int CoreOffsetX,
        int BoxWidth, int BoxHeight);

    // UA: Растеризує символ на пробному canvas і повертає ЛИШЕ
    //     обчислену геометрію (без пікселів/малювання) — для
    //     діагностичного логування точних проміжних чисел без
    //     повторного написання формул.
    // EN: Rasterizes the character on the probe canvas and returns ONLY
    //     the computed geometry (no pixels/drawing) — for diagnostic
    //     logging of exact intermediate numbers without rewriting the
    //     formulas.
    public static CoreMarginLayout ComputeCoreMarginLayout(
        char character, string fontFamilyName, int coreHeight, int coreTopY, int marginCapPx, int boxWidth, int boxHeight,
        double aboveScale = 1.0, double belowScale = 1.0)
    {
        var (ink, _) = RasterizeProbe(character, fontFamilyName);
        return ComputeLayout(ink, coreHeight, coreTopY, marginCapPx, boxWidth, boxHeight, aboveScale, belowScale);
    }

    // UA: Рахує ОДИН спільний aboveScale/belowScale для ВСЬОГО алфавіту
    //     малих літер цього шрифту (не для однієї літери). ПРИЧИНА: на
    //     розмірах BF2 (CapHeightGame 7-11px) CoreMarginCapPx = лише
    //     2-3px. Клампінг КОЖНОЇ літери окремо до marginCapPx незалежно
    //     від природного розміру виступу дав би: "і" (природно потребує
    //     1-2px під крапку) і "б" (природно потребує 3-6px під петлю)
    //     ОБИДВІ обрізались би до РІВНО того самого marginCapPx і виходили
    //     б однаковою висотою (підтверджено: BF2 gamefont_medium 'і'
    //     BoxHeight=11px, 'б' BoxHeight=11px — буквально ідентично при
    //     такому клампінгу). Замість цього: перший прохід по ВСЬОМУ
    //     алфавіту регістру знаходить НАЙБІЛЬШИЙ природний виступ (до
    //     жодного клампу), і рахує коефіцієнт, що стискає САМЕ ЙОГО рівно
    //     до marginCapPx — решта літер масштабується ТІЄЮ Ж пропорцією,
    //     тож "і" лишається ПОМІТНО меншою за "б", а не зливається з нею.
    // EN: Computes ONE shared aboveScale/belowScale for the WHOLE
    //     lowercase alphabet of this font (not a single letter). REASON:
    //     at BF2's sizes (CapHeightGame 7-11px), CoreMarginCapPx is only
    //     2-3px. Clamping EVERY letter individually down to marginCapPx
    //     regardless of its natural extension size would give: "і"
    //     (naturally needs 1-2px for the dot) and "б" (naturally needs
    //     3-6px for the loop) BOTH cut to EXACTLY the same marginCapPx
    //     and coming out the same height (confirmed: BF2 gamefont_medium
    //     'і' BoxHeight=11px, 'б' BoxHeight=11px — literally identical
    //     under that clamping). Instead: a first pass over the WHOLE case
    //     alphabet finds the LARGEST natural extension (before any
    //     clamping), and computes the factor that shrinks THAT ONE down
    //     to exactly marginCapPx — every other letter is scaled by the
    //     SAME ratio, so "і" stays VISIBLY smaller than "б" instead of
    //     merging with it.
    public readonly record struct AlphabetExtensionScale(
        double AboveScale, double BelowScale, int MaxNaturalAboveH, int MaxNaturalBelowH);

    public static AlphabetExtensionScale ComputeAlphabetExtensionScale(
        IEnumerable<char> lowercaseLetters, string fontFamilyName,
        int coreHeight, int coreTopY, int marginCapPx,
        Func<char, (int BoxWidth, int BoxHeight)> boxSizeForChar)
    {
        var maxAbove = 0;
        var maxBelow = 0;
        foreach (var ch in lowercaseLetters)
        {
            (int BoxWidth, int BoxHeight) box;
            try { box = boxSizeForChar(ch); }
            catch { continue; }

            try
            {
                var layout = ComputeCoreMarginLayout(ch, fontFamilyName, coreHeight, coreTopY, marginCapPx, box.BoxWidth, box.BoxHeight);
                maxAbove = Math.Max(maxAbove, layout.NatAboveRenderH);
                maxBelow = Math.Max(maxBelow, layout.NatBelowRenderH);
            }
            catch { /* UA: та сама толерантність, що й у per-letter циклі виклика. / EN: same tolerance as the caller's per-letter loop. */ }
        }

        var aboveScale = maxAbove > marginCapPx ? marginCapPx / (double)maxAbove : 1.0;
        var belowScale = maxBelow > marginCapPx ? marginCapPx / (double)maxBelow : 1.0;
        return new AlphabetExtensionScale(aboveScale, belowScale, maxAbove, maxBelow);
    }

    private static (Rectangle Ink, RasterizedGlyph Probe) RasterizeProbe(char character, string fontFamilyName)
    {
        var rasterizer = new GdiGlyphRasterizer();
        var probeOptions = new GlyphRasterizeOptions
        {
            FontFamilyName = fontFamilyName,
            FontSizePx = ProbeFontSizePx,
            CanvasWidth = ProbeCanvasSize,
            CanvasHeight = ProbeCanvasSize,
            BaselineY = ProbeBaselineY
        };

        var probe = rasterizer.Rasterize(character, probeOptions);
        var ink = probe.InkBounds;

        if (ink.Width <= 0 || ink.Height <= 0)
            throw new InvalidOperationException(
                $"UA: Гліф '{character}' шрифтом '{fontFamilyName}' не намалював жодного пікселя. / " +
                $"EN: Glyph '{character}' with font '{fontFamilyName}' produced no pixels.");

        return (ink, probe);
    }

    private static CoreMarginLayout ComputeLayout(
        Rectangle ink, int coreHeight, int coreTopY, int marginCapPx, int boxWidth, int boxHeight,
        double aboveScale = 1.0, double belowScale = 1.0)
    {
        // UA: usableWidth/usableHeight — розмір слоту, доступний тілу й
        //     виступам для розміщення. Рівний ПОВНОМУ переданому боксу
        //     (boxWidth/boxHeight): чорнило займає ввесь переданий бокс, а
        //     не бокс мінус EdgeInsetPx. Гарантований прозорий буфер
        //     (SlotPaddingPx) додає ВИКЛИКАЧ, збільшуючи САМ СЛОТ ПЕРЕД
        //     викликом цього методу, а не цей метод — забирати місце в
        //     готового чорнила означало б звужувати штрих (див. коментар
        //     біля SlotPaddingPx і в RenderToFit нижче).
        // EN: usableWidth/usableHeight — the slot size available for the
        //     core and extensions to lay out in. Equal to the FULL box
        //     passed in (boxWidth/boxHeight): the ink fills the entire
        //     passed-in box, not box minus EdgeInsetPx. The guaranteed
        //     transparent buffer (SlotPaddingPx) is added by the CALLER,
        //     by growing the SLOT ITSELF before calling this method, not
        //     by this method — taking space away from finished ink would
        //     narrow the stroke (see the comment by SlotPaddingPx and in
        //     RenderToFit below).
        var usableWidth = boxWidth;
        var usableHeight = boxHeight;

        var coreBandHeight = ProbeBaselineY - coreTopY;
        if (coreBandHeight <= 0)
            throw new ArgumentException(
                $"UA: coreTopY ({coreTopY}) має бути ВИЩЕ за ProbeBaselineY ({ProbeBaselineY}). / " +
                $"EN: coreTopY ({coreTopY}) must be ABOVE ProbeBaselineY ({ProbeBaselineY}).");
        var coreScale = coreHeight / (double)coreBandHeight;

        // UA: Перетин природного чорнила з еталонною смугою x-height —
        //     це і є "тіло". Усе, що ВИЩЕ coreTopY — виступ-зверху; усе,
        //     що НИЖЧЕ ProbeBaselineY — виступ-знизу.
        // EN: The overlap of natural ink with the reference x-height band
        //     is the "core". Anything ABOVE coreTopY is the top
        //     extension; anything BELOW ProbeBaselineY is the bottom
        //     extension.
        var coreT = Math.Max(ink.Top, coreTopY);
        var coreB = Math.Min(ink.Bottom, ProbeBaselineY);
        if (coreB <= coreT)
        {
            // UA: Гліф взагалі не перетинається з еталонною смугою — не
            //     мало б траплятись для реальних літер цього алфавіту;
            //     запобіжник — усе чорнило рахується "тілом".
            // EN: The glyph doesn't overlap the reference band at all —
            //     shouldn't happen for real letters of this alphabet;
            //     fallback — treat all ink as "core".
            coreT = ink.Top;
            coreB = ink.Bottom;
        }

        var aboveSrcH = Math.Max(0, coreT - ink.Top);
        var belowSrcH = Math.Max(0, ink.Bottom - coreB);

        // UA: Натуральні розміри (до обмеження полів і boxHeight) — усі
        //     три шматки спершу масштабуються ОДНИМ coreScale, щоб
        //     товщина штриха була узгоджена між тілом і виступом.
        // EN: Natural sizes (before margin/boxHeight limits) — all three
        //     pieces are first scaled by the SAME coreScale, so stroke
        //     thickness stays consistent between core and extension.
        var natWidth = Math.Max(1, (int)Math.Round(ink.Width * coreScale));
        var coreRenderH = Math.Max(1, (int)Math.Round((coreB - coreT) * coreScale));
        var aboveRenderH = aboveSrcH > 0 ? Math.Max(1, (int)Math.Round(aboveSrcH * coreScale)) : 0;
        var belowRenderH = belowSrcH > 0 ? Math.Max(1, (int)Math.Round(belowSrcH * coreScale)) : 0;
        var coreRenderW = natWidth;
        var aboveRenderW = aboveSrcH > 0 ? natWidth : 0;
        var belowRenderW = belowSrcH > 0 ? natWidth : 0;

        // UA: Якщо натуральна ширина (та сама для всіх трьох шматків) не
        //     влазить у бокс — стискаємо ЛИШЕ ШИРИНУ всіх трьох шматків,
        //     ВИСОТУ не чіпаємо. Висота тіла МАЄ лишатись ЗАВЖДИ рівно
        //     coreHeight, однакова для КОЖНОЇ малої літери регістру,
        //     незалежно від того, наскільки вузький слот — інакше
        //     виникає розсинхронізація з Bearing (рахується в
        //     ComputeLowercaseCoreMetric від ПОВНОГО CoreHeightGame): якщо
        //     тіло стиснути й по висоті теж, вужчі літери (напр. 'і')
        //     не діставали б до спільної базової лінії (інша висота
        //     клітинки, ніж у решти) і візуально "висіли б у повітрі".
        //     Тому занадто широке чорнило стискається ЛИШЕ по
        //     горизонталі. Так, це неоднорідний масштаб (аспект трохи
        //     спотворюється) — але для вузьких літер ('і' — фактично
        //     вертикальна риска) це візуально непомітно, тоді як РІЗНА
        //     висота тіла помітна одразу й ламає спільну базову лінію.
        // EN: If the natural width (same for all three pieces) doesn't
        //     fit the box — shrink ONLY the WIDTH of all three pieces,
        //     HEIGHT is left untouched. The core height MUST always stay
        //     exactly coreHeight, identical for EVERY lowercase letter of
        //     the case, no matter how narrow the slot — otherwise it gets
        //     out of sync with Bearing (derived in
        //     ComputeLowercaseCoreMetric from the FULL CoreHeightGame): if
        //     the core were also shrunk vertically, narrower letters
        //     (e.g. 'і') would fall short of the shared baseline (a
        //     different cell height than everything else) and visually
        //     "float". So over-wide ink is squeezed HORIZONTALLY only.
        //     Yes, that's a non-uniform scale (slight aspect distortion)
        //     — but on narrow letters ('і' is effectively a vertical bar)
        //     it's visually unnoticeable, whereas a DIFFERENT core height
        //     is immediately obvious and breaks the shared baseline.
        if (natWidth > usableWidth)
        {
            coreRenderW = usableWidth;
            if (aboveRenderH > 0) aboveRenderW = usableWidth;
            if (belowRenderH > 0) belowRenderW = usableWidth;
        }

        // UA: Знімок висоти виступу ПІСЛЯ стиснення під ширину, але ДО
        //     обмеження marginCapPx — для діагностики (CoreMarginLayout),
        //     щоб відрізнити "зрізано полем" від "стиснуто шириною".
        // EN: Snapshot of extension height AFTER width-fit shrink, but
        //     BEFORE the marginCapPx limit — for diagnostics
        //     (CoreMarginLayout), to tell "cut by the margin" apart from
        //     "shrunk by width".
        var natAboveRenderH = aboveRenderH;
        var natBelowRenderH = belowRenderH;

        // UA: РЕЗЕРВОВАНЕ ПОЛЕ — головна відмінність від
        //     RenderWithSharedHeight: якщо виступ (навіть після
        //     узгодженого масштабування вище) все ще перевищує
        //     marginCapPx — стискаємо ЛИШЕ ЙОГО (зі збереженням його
        //     власної пропорції), тіло НЕ чіпаємо.
        // EN: RESERVED MARGIN — the main difference from
        //     RenderWithSharedHeight: if an extension (even after the
        //     consistent scaling above) still exceeds marginCapPx —
        //     shrink ONLY IT (preserving its own aspect), the core is
        //     left untouched.
        // UA: Обмеження ПРОПОРЦІЙНЕ (aboveScale/belowScale, див. коментар
        //     біля ComputeAlphabetExtensionScale вище), а не жорсткий
        //     кламп "усе понад marginCapPx = рівно marginCapPx" — інакше
        //     всі виступи, що перевищують marginCapPx, зливались би в
        //     однакову висоту (див. ComputeAlphabetExtensionScale). Виклик
        //     БЕЗ явного aboveScale/belowScale (default 1.0) зводиться до
        //     жорсткого клампу через safety-стелю нижче (запобіжник для
        //     викликачів, які не порахували scale).
        // EN: The limiting is PROPORTIONAL (aboveScale/belowScale, see the
        //     comment by ComputeAlphabetExtensionScale above), not a hard
        //     "everything above marginCapPx = exactly marginCapPx" clamp
        //     — otherwise every extension exceeding marginCapPx would
        //     collapse to the same height (see
        //     ComputeAlphabetExtensionScale). A call WITHOUT an explicit
        //     aboveScale/belowScale (default 1.0) reduces to the hard
        //     clamp via the safety ceiling below (a guard for callers
        //     that didn't compute a scale).
        if (aboveRenderH > 0)
        {
            aboveRenderW = Math.Max(1, (int)Math.Round(aboveRenderW * aboveScale));
            aboveRenderH = Math.Max(1, (int)Math.Round(aboveRenderH * aboveScale));
            if (aboveRenderH > marginCapPx)
            {
                var capShrink = marginCapPx / (double)aboveRenderH;
                aboveRenderW = Math.Max(1, (int)Math.Round(aboveRenderW * capShrink));
                aboveRenderH = marginCapPx;
            }
        }
        if (belowRenderH > 0)
        {
            belowRenderW = Math.Max(1, (int)Math.Round(belowRenderW * belowScale));
            belowRenderH = Math.Max(1, (int)Math.Round(belowRenderH * belowScale));
            if (belowRenderH > marginCapPx)
            {
                var capShrink = marginCapPx / (double)belowRenderH;
                belowRenderW = Math.Max(1, (int)Math.Round(belowRenderW * capShrink));
                belowRenderH = marginCapPx;
            }
        }

        // UA: Якщо тіло+виступи разом усе одно не влазять у boxHeight —
        //     спершу стискаємо ВИСТУПИ (другорядні), і лише як останній
        //     засіб — тіло теж (рідкісний випадок найтісніших донорів).
        // EN: If core+extensions together still don't fit boxHeight —
        //     shrink EXTENSIONS first (secondary), and only as a last
        //     resort, the core too (rare, tightest donors only).
        var totalHeight = aboveRenderH + coreRenderH + belowRenderH;
        if (totalHeight > usableHeight)
        {
            var overflow = totalHeight - usableHeight;
            var marginsSum = aboveRenderH + belowRenderH;
            if (marginsSum > 0)
            {
                var shrinkAbove = (int)Math.Round(overflow * (aboveRenderH / (double)marginsSum));
                var shrinkBelow = overflow - shrinkAbove;
                aboveRenderH = Math.Max(0, aboveRenderH - shrinkAbove);
                belowRenderH = Math.Max(0, belowRenderH - shrinkBelow);
                if (aboveRenderH == 0) aboveRenderW = 0;
                if (belowRenderH == 0) belowRenderW = 0;
            }

            totalHeight = aboveRenderH + coreRenderH + belowRenderH;
            if (totalHeight > usableHeight)
            {
                var scaleDown = usableHeight / (double)totalHeight;
                coreRenderW = Math.Max(1, (int)Math.Round(coreRenderW * scaleDown));
                coreRenderH = Math.Max(1, (int)Math.Round(coreRenderH * scaleDown));
                aboveRenderW = (int)Math.Round(aboveRenderW * scaleDown);
                aboveRenderH = (int)Math.Round(aboveRenderH * scaleDown);
                belowRenderW = (int)Math.Round(belowRenderW * scaleDown);
                belowRenderH = (int)Math.Round(belowRenderH * scaleDown);
            }
        }

        coreRenderW = Math.Clamp(coreRenderW, 1, usableWidth);
        coreRenderH = Math.Clamp(coreRenderH, 1, usableHeight);

        // UA: База (baseline) = низ КОРИСНОЇ області (низ бокса мінус
        //     EdgeInsetPx) мінус місце під виступ-знизу (0, якщо його
        //     нема) — тіло ЗАВЖДИ сідає на цю лінію, так само, як звичайна
        //     літера без жодного виступу. +EdgeInsetPx тут і в
        //     coreOffsetX нижче — те саме гарантоване поле, що й у
        //     RenderToFit (див. коментар біля EdgeInsetPx).
        // EN: Baseline = bottom of the USABLE area (box bottom minus
        //     EdgeInsetPx) minus room for the bottom extension (0 if there
        //     is none) — the core ALWAYS sits on this line, the same as an
        //     ordinary letter with no extension at all. +EdgeInsetPx here
        //     and in coreOffsetX below — the same guaranteed margin as in
        //     RenderToFit (see the comment by EdgeInsetPx).
        var coreBottom = boxHeight - belowRenderH;
        var coreTop = coreBottom - coreRenderH;
        var coreOffsetX = (usableWidth - coreRenderW) / 2;

        return new CoreMarginLayout(
            ink.Left, ink.Top, ink.Right, ink.Bottom,
            coreTopY, coreBandHeight, coreScale,
            coreT, coreB, aboveSrcH, belowSrcH,
            natWidth,
            coreRenderW, coreRenderH,
            natAboveRenderH, natBelowRenderH,
            aboveRenderW, aboveRenderH,
            belowRenderW, belowRenderH,
            coreTop, coreBottom, coreOffsetX,
            boxWidth, boxHeight);
    }

    public static RasterizedGlyph RenderWithCoreAndMargin(
        char character, string fontFamilyName, int coreHeight, int coreTopY, int marginCapPx, int boxWidth, int boxHeight,
        double aboveScale = 1.0, double belowScale = 1.0)
    {
        if (coreHeight <= 0 || boxWidth <= 0 || boxHeight <= 0 || marginCapPx < 0)
            throw new ArgumentException(
                $"UA: coreHeight/boxWidth/boxHeight мають бути > 0, marginCapPx >= 0 (отримано coreHeight={coreHeight}, box={boxWidth}x{boxHeight}, margin={marginCapPx}) / " +
                $"EN: coreHeight/boxWidth/boxHeight must be > 0, marginCapPx >= 0 (got coreHeight={coreHeight}, box={boxWidth}x{boxHeight}, margin={marginCapPx})");

        var (ink, probe) = RasterizeProbe(character, fontFamilyName);
        var layout = ComputeLayout(ink, coreHeight, coreTopY, marginCapPx, boxWidth, boxHeight, aboveScale, belowScale);
        var usableWidth = boxWidth;

        var coreT = layout.CoreT;
        var coreB = layout.CoreB;
        var coreRenderW = layout.CoreRenderW;
        var coreRenderH = layout.CoreRenderH;
        var aboveRenderW = layout.AboveRenderW;
        var aboveRenderH = layout.AboveRenderH;
        var belowRenderW = layout.BelowRenderW;
        var belowRenderH = layout.BelowRenderH;
        var aboveSrcH = layout.AboveSrcH;
        var belowSrcH = layout.BelowSrcH;
        var coreTop = layout.CoreTop;
        var coreBottom = layout.CoreBottom;
        var coreOffsetX = layout.CoreOffsetX;

        using var probeBitmap = BgraBytesToBitmap(probe.BgraPixels, ProbeCanvasSize, ProbeCanvasSize);
        using var paddedBitmap = new Bitmap(boxWidth, boxHeight, PixelFormat.Format32bppArgb);

        var pieces = new List<Rectangle>();
        using (var g = Graphics.FromImage(paddedBitmap))
        {
            g.Clear(Color.Transparent);

            var coreSrcRect = new Rectangle(ink.Left, coreT, ink.Width, coreB - coreT);
            using (var coreCropped = probeBitmap.Clone(coreSrcRect, PixelFormat.Format32bppArgb))
            using (var coreResized = ProgressiveResize(coreCropped, coreRenderW, coreRenderH))
            {
                g.DrawImage(coreResized, coreOffsetX, coreTop, coreRenderW, coreRenderH);
            }
            pieces.Add(new Rectangle(coreOffsetX, coreTop, coreRenderW, coreRenderH));

            if (aboveRenderW > 0 && aboveRenderH > 0)
            {
                var srcRect = new Rectangle(ink.Left, ink.Top, ink.Width, aboveSrcH);
                var offsetX = (usableWidth - aboveRenderW) / 2;
                var offsetY = Math.Max(0, coreTop - aboveRenderH);
                using (var cropped = probeBitmap.Clone(srcRect, PixelFormat.Format32bppArgb))
                using (var resized = ProgressiveResize(cropped, aboveRenderW, aboveRenderH))
                {
                    g.DrawImage(resized, offsetX, offsetY, aboveRenderW, aboveRenderH);
                }
                pieces.Add(new Rectangle(offsetX, offsetY, aboveRenderW, aboveRenderH));
            }

            if (belowRenderW > 0 && belowRenderH > 0)
            {
                var srcRect = new Rectangle(ink.Left, coreB, ink.Width, belowSrcH);
                var offsetX = (usableWidth - belowRenderW) / 2;
                using (var cropped = probeBitmap.Clone(srcRect, PixelFormat.Format32bppArgb))
                using (var resized = ProgressiveResize(cropped, belowRenderW, belowRenderH))
                {
                    g.DrawImage(resized, offsetX, coreBottom, belowRenderW, belowRenderH);
                }
                pieces.Add(new Rectangle(offsetX, coreBottom, belowRenderW, belowRenderH));
            }
        }

        var resultBytes = BitmapToBgraBytes(paddedBitmap, boxWidth, boxHeight);
        var unionRect = pieces.Aggregate(Rectangle.Union);

        return new RasterizedGlyph
        {
            Width = boxWidth,
            Height = boxHeight,
            BgraPixels = resultBytes,
            InkBounds = unionRect
        };
    }

    public static RasterizedGlyph RenderToFit(char character, string fontFamilyName, int targetWidth, int targetHeight)
    {
        if (targetWidth <= 0 || targetHeight <= 0)
            throw new ArgumentException(
                $"UA: targetWidth/targetHeight мають бути > 0 (отримано {targetWidth}x{targetHeight}) / " +
                $"EN: targetWidth/targetHeight must be > 0 (got {targetWidth}x{targetHeight})");

        var rasterizer = new GdiGlyphRasterizer();
        var probeOptions = new GlyphRasterizeOptions
        {
            FontFamilyName = fontFamilyName,
            FontSizePx = ProbeFontSizePx,
            CanvasWidth = ProbeCanvasSize,
            CanvasHeight = ProbeCanvasSize,
            BaselineY = ProbeBaselineY
        };

        var probe = rasterizer.Rasterize(character, probeOptions);
        var ink = probe.InkBounds;

        if (ink.Width <= 0 || ink.Height <= 0)
            throw new InvalidOperationException(
                $"UA: Гліф '{character}' шрифтом '{fontFamilyName}' не намалював жодного пікселя. / " +
                $"EN: Glyph '{character}' with font '{fontFamilyName}' produced no pixels.");

        using var probeBitmap = BgraBytesToBitmap(probe.BgraPixels, ProbeCanvasSize, ProbeCanvasSize);
        using var croppedBitmap = probeBitmap.Clone(ink, PixelFormat.Format32bppArgb);

        // UA: Прогресивне (мipmap-подібне) зменшення — не одним стрибком
        //     bicubic, а кроками не більше ×2 за раз. Один різкий
        //     bicubic-ресемплінг при сильному стисненні (напр. Ш/Щ/Ж/Ю,
        //     де кілька тонких паралельних штрихів стискаються в
        //     непропорційно вузький донорський слот) дає муар — сусідні
        //     штрихи "зливаються"/"миготять" непередбачувано, бо bicubic
        //     розрахований на помірне масштабування, не на різке
        //     проріджування дрібних деталей. Кроки по ×2 — кожен окремо
        //     достатньо м'який для bicubic, а сумарно дають куди чистіший
        //     результат (та сама ідея, що й у mip-мапах текстур).
        // EN: Progressive (mipmap-like) downscaling — not one bicubic
        //     jump, but steps of at most ×2 each. A single sharp bicubic
        //     resample under heavy compression (e.g. Ш/Щ/Ж/Ю, where
        //     several thin parallel strokes get squeezed into a
        //     disproportionately narrow donor slot) produces moiré —
        //     neighboring strokes "merge"/"flicker" unpredictably, since
        //     bicubic is tuned for moderate scaling, not sharp thinning of
        //     fine detail. ×2 steps are each mild enough for bicubic, and
        //     together give a much cleaner result (the same idea as
        //     texture mipmaps).
        // UA: Той самий гарантований 1px прозорий буфер, що й у
        //     RenderWithCoreAndMargin (EdgeInsetPx, див. коментар біля
        //     константи вище) — без нього чорнило впритул до
        //     targetWidth×targetHeight (0px поля з усіх боків для КОЖНОЇ
        //     великої літери) провокує ту саму GPU-фільтраційну "рамку",
        //     описану біля SlotPaddingPx.
        //
        //     Чорнило рендериться в ПОВНИЙ переданий розмір (targetWidth×
        //     targetHeight), а не в `targetWidth - 2*EdgeInsetPx`: віднімати
        //     відступ ВІД ЧОРНИЛА всередині слоту незмінного розміру
        //     звужує штрих (виміряно: BF1 'І' — чорнило 3px замість 5,
        //     'А' — 10 замість 13; вузькі літери втрачали б до 40% товщини
        //     штриха, широкі — до чверті). Замість цього прозоре поле
        //     створює ВИКЛИКАЧ, передаючи слот, БІЛЬШИЙ за чорнило на
        //     2×SlotPaddingPx (див. GenerateNoDonorCyrillicCoreCommand).
        // EN: The same guaranteed 1px transparent buffer as in
        //     RenderWithCoreAndMargin (EdgeInsetPx, see the comment by the
        //     constant above) — without it, ink flush against
        //     targetWidth×targetHeight (0px margin on every side for
        //     EVERY capital letter) triggers the same GPU filtering
        //     "frame" artifact described by SlotPaddingPx.
        //
        //     The ink is rendered at the FULL size passed in (targetWidth×
        //     targetHeight), not at `targetWidth - 2*EdgeInsetPx`:
        //     subtracting the margin FROM THE INK inside a fixed-size slot
        //     narrows the stroke (measured: BF1 'І' ink 3px instead of 5,
        //     'А' 10 instead of 13; narrow letters would lose up to 40% of
        //     stroke thickness, wide ones up to a quarter). Instead, the
        //     transparent margin is created by the CALLER passing a slot
        //     LARGER than the ink by 2×SlotPaddingPx (see
        //     GenerateNoDonorCyrillicCoreCommand).
        var insetWidth = targetWidth;
        var insetHeight = targetHeight;
        using var resizedBitmap = ProgressiveResize(croppedBitmap, insetWidth, insetHeight);

        var insetOffsetX = 0;
        var insetOffsetY = 0;
        using var paddedBitmap = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(paddedBitmap))
        {
            g.Clear(Color.Transparent);
            g.DrawImage(resizedBitmap, insetOffsetX, insetOffsetY, insetWidth, insetHeight);
        }

        var resultBytes = BitmapToBgraBytes(paddedBitmap, targetWidth, targetHeight);

        return new RasterizedGlyph
        {
            Width = targetWidth,
            Height = targetHeight,
            BgraPixels = resultBytes,
            InkBounds = new Rectangle(insetOffsetX, insetOffsetY, insetWidth, insetHeight)
        };
    }

    // UA: Зменшує bitmap до targetWidth×targetHeight кроками не більше ×2
    //     за раз (замість одного різкого стрибка) — щоб уникнути муару на
    //     дрібних паралельних штрихах при сильному стисненні. Останній
    //     крок точно влучає в targetWidth×targetHeight. Не звільняє
    //     вхідний source (належить викликаючому коду), звільняє лише свої
    //     проміжні bitmap.
    // EN: Shrinks a bitmap to targetWidth×targetHeight in steps of at
    //     most ×2 each (instead of one sharp jump) — to avoid moiré on
    //     thin parallel strokes under heavy compression. The final step
    //     lands exactly on targetWidth×targetHeight. Does NOT dispose the
    //     input source (owned by the caller), only disposes its own
    //     intermediate bitmaps.
    private static Bitmap ProgressiveResize(Bitmap source, int targetWidth, int targetHeight)
    {
        var current = source;
        var ownsCurrent = false;

        while (current.Width > targetWidth * 2 || current.Height > targetHeight * 2)
        {
            var nextWidth = Math.Max(targetWidth, current.Width / 2);
            var nextHeight = Math.Max(targetHeight, current.Height / 2);

            var next = new Bitmap(nextWidth, nextHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(next))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(current, new Rectangle(0, 0, nextWidth, nextHeight));
            }

            if (ownsCurrent) current.Dispose();
            current = next;
            ownsCurrent = true;
        }

        if (current.Width == targetWidth && current.Height == targetHeight)
            return current;

        var final = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(final))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(current, new Rectangle(0, 0, targetWidth, targetHeight));
        }

        if (ownsCurrent) current.Dispose();
        return final;
    }

    // UA: Порядок байтів B,G,R,A на піксель — той самий, що документовано
    //     в RasterizedGlyph.cs і фактично використовується
    //     GdiGlyphRasterizer. Копіюємо рядок за рядком з урахуванням
    //     Stride (НЕ припускаємо Stride==width*4 наосліп).
    // EN: B,G,R,A byte order per pixel — the same one documented in
    //     RasterizedGlyph.cs and actually used by GdiGlyphRasterizer.
    //     Copied row by row respecting Stride (NOT blindly assuming
    //     Stride==width*4).
    private static Bitmap BgraBytesToBitmap(byte[] bgraPixels, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < height; y++)
                Marshal.Copy(bgraPixels, y * width * 4, data.Scan0 + y * data.Stride, width * 4);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return bitmap;
    }

    private static byte[] BitmapToBgraBytes(Bitmap bitmap, int width, int height)
    {
        var result = new byte[width * height * 4];
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < height; y++)
                Marshal.Copy(data.Scan0 + y * data.Stride, result, y * width * 4, width * 4);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return result;
    }
}
