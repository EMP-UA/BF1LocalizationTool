// =============================================================================
// BF1LocalizationTool.FontGenerator — Matching/GlyphMetricModel.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: МОДЕЛЬ ВЕРТИКАЛЬНОГО РЕНДЕРУ ГРИ — виведена й ПІДТВЕРДЖЕНА двома
//     контрольованими експериментами:
//
//       1) Розбір 28 англійських літер gamefont_large напряму з файлу дав
//          ЖОРСТКИЙ інваріант БЕЗ ЖОДНОГО винятку:
//                Bearing + висота_UV_бокса(px) = CellHeight
//       2) Експеримент CellHeight (ж: 19→30 → виросла й сіла на базову
//          лінію) довів, що поле керує саме ВЕРТИКАЛЛЮ рендеру.
//       3) Експеримент метричної моделі (Bearing = CellHeight − бокс для
//          всіх кириличних) довів, що низи літер вирівнюються по спільній
//          базовій лінії — "левітація"/вертикальне стрибання зникає.
//
//     ФОРМУЛА РЕНДЕРУ: гра малює UV-бокс гліфа, вертикально розтягнутий у
//     екранний діапазон [pen+Bearing, pen+CellHeight]. Отже:
//        екранна_висота_чорнила = CellHeight − Bearing
//        верх_чорнила  = pen + Bearing      (Bearing — відступ згори)
//        низ_чорнила   = pen + CellHeight   (базова лінія рядка =
//                                            pen + baselineOffset)
//     Для звичайної літери, що сидить НА базовій лінії, CellHeight =
//     baselineOffset. Хвостаті (р, у, ц, щ, д) мають CellHeight >
//     baselineOffset (на глибину хвоста). Верх (Bearing) визначає, наскільки
//     високо починається чорнило: менший Bearing = вища літера.
//
//     ЦЕЙ КЛАС рахує для КОЖНОЇ кириличної літери правильні Bearing,
//     CellHeight і цільові розміри бокса (BoxWidth×BoxHeight) — з ПРИРОДНОЇ
//     форми літери (реальний растр Bahnschrift), масштабованої під висоту
//     клітинки ЦЬОГО шрифту гри. Це те саме, що робить гра з англійськими
//     літерами — типографія з реальними метриками, а не піксельні хаки:
//     проблема вертикального позиціювання НЕ в пікселях текстури, а в
//     тому, що Bearing/CellHeight мають обчислюватися з форми самої літери,
//     а не копіюватися з випадкового англійського донора.
// EN: THE GAME'S VERTICAL RENDER MODEL — derived and CONFIRMED by two
//     controlled experiments:
//
//       1) Parsing 28 English gamefont_large letters straight from the
//          file gave a HARD invariant with NO exception:
//                Bearing + UV_box_height(px) = CellHeight
//       2) The CellHeight experiment (ж: 19→30 → grew and dropped onto the
//          baseline) proved the field drives the render's VERTICAL axis.
//       3) The metric-model experiment (Bearing = CellHeight − box for all
//          Cyrillic) proved the letter bottoms align on a shared baseline
//          — "levitation" / vertical jumping disappears.
//
//     RENDER FORMULA: the game draws the glyph's UV box stretched
//     vertically into the screen range [pen+Bearing, pen+CellHeight]. So:
//        screen_ink_height = CellHeight − Bearing
//        ink_top    = pen + Bearing      (Bearing is the top inset)
//        ink_bottom = pen + CellHeight   (line baseline = pen +
//                                         baselineOffset)
//     For an ordinary letter sitting ON the baseline, CellHeight =
//     baselineOffset. Descenders (р, у, ц, щ, д) have CellHeight >
//     baselineOffset (by the tail depth). The top (Bearing) sets how high
//     the ink starts: a smaller Bearing = a taller letter.
//
//     THIS CLASS computes, for EACH Cyrillic letter, the correct Bearing,
//     CellHeight and target box size (BoxWidth×BoxHeight) — from the
//     letter's NATURAL shape (real Bahnschrift raster) scaled to THIS game
//     font's cell height. This is exactly what the game does with English
//     letters — typesetting with real metrics, not pixel hacks: the
//     vertical-positioning problem is NOT in the texture pixels, but in
//     the fact that Bearing/CellHeight must be computed from the letter's
//     own shape, not copied from a random English donor.
// =============================================================================

using System.Drawing;
using System.Runtime.Versioning;
using BF1LocalizationTool.FontGenerator.Rasterization;

namespace BF1LocalizationTool.FontGenerator.Matching;

// UA: Опорні метрики ОДНОГО шрифту гри, виведені з його РЕАЛЬНИХ
//     англійських гліфів + пробного растру. Незмінні для всіх 66 літер
//     цього шрифту.
// EN: Reference metrics of ONE game font, derived from its REAL English
//     glyphs + the probe raster. Constant across all 66 letters of this
//     font.
public sealed record FontMetricReference(
    // UA: Позиція базової лінії від верху рядка (=CellHeight звичайної
    //     літери на лінії; медіана CellHeight англійських записів).
    // EN: Baseline position from the line top (=CellHeight of an ordinary
    //     on-baseline letter; median CellHeight of English records).
    int BaselineOffset,
    // UA: Екранна висота великої літери в пікселях (baselineOffset −
    //     медіана Bearing англійських A-Z).
    // EN: On-screen capital-letter height in pixels (baselineOffset −
    //     median Bearing of English A-Z).
    int CapHeightGame,
    // UA: Природна висота великої кириличної літери в пробному растрі
    //     (медіана ascent'ів усіх ВЕЛИКИХ цілей) — знаменник масштабу
    //     природа→гра ПО ВИСОТІ.
    // EN: Natural capital Cyrillic letter height in the probe raster
    //     (median ascent of all UPPERCASE targets) — the denominator of
    //     the natural→game HEIGHT scale.
    double NaturalCapAscent,
    // UA: ШИРИНА типової великої літери В САМІЙ ГРІ (медіана InkWidth
    //     реальних англійських A-Z; медіана свідомо ігнорує вузькі/широкі
    //     винятки — 'I' та 'M'/'W' — і лишається "типовою" вузькою
    //     шириною). ЧИСЕЛЬНИК окремого масштабу природа→гра ПО ШИРИНІ,
    //     див. коментар у ComputeLetterMetric.
    // EN: Width of a TYPICAL capital IN THE GAME ITSELF (median InkWidth
    //     of the real English A-Z; median deliberately ignores the
    //     narrow/wide exceptions — 'I' and 'M'/'W' — and stays the
    //     "typical" narrow width). NUMERATOR of the separate
    //     natural→game WIDTH scale, see the comment in
    //     ComputeLetterMetric.
    double CapWidthGame,
    // UA: Природна ШИРИНА великої кириличної літери в пробному растрі
    //     (медіана InkWidth усіх ВЕЛИКИХ цілей, той самий набір, що й для
    //     NaturalCapAscent) — ЗНАМЕННИК окремого масштабу природа→гра по
    //     ширині.
    // EN: Natural WIDTH of a capital Cyrillic letter in the probe raster
    //     (median InkWidth of all UPPERCASE targets, the same set as
    //     NaturalCapAscent) — DENOMINATOR of the separate natural→game
    //     width scale.
    double NaturalCapInkWidth,
    // UA: ПІДЛОГА ширини — InkWidth НАЙВУЖЧОЇ рідної англійської
    //     капітелі цього шрифту (типово 'I'). ПРИЧИНА: один widthScale,
    //     калібрований до МЕДІАННОЇ (типової, блочної) капітелі, коректно
    //     звужує широкі літери (О/Д/Ж), але ТАКОЖ стискає вже вузькі
    //     однострокові літери (І/Ї/Й) — їм майже нема куди звужуватись, а
    //     стискаються вони тим самим коефіцієнтом (без підлоги кирилична
    //     'І' виходить тоншою за родину 'I' в тому ж рядку: виміряно
    //     3px проти 5px нативної 'I' і 4px донорської 'І'). Той самий
    //     клас проблеми, що й підлога Bearing для малих літер вище —
    //     один масштаб не може обслужити і "звичайну" і "вузьку" форму
    //     одночасно. ПІДЛОГА не дає ЖОДНІЙ кириличній капітелі вийти
    //     вужчою за найвужчу РІДНУ капітель цього самого шрифту.
    // EN: Width FLOOR — InkWidth of the NARROWEST native English capital
    //     of this font (typically 'I'). CAUSE: one widthScale, calibrated
    //     to the MEDIAN (typical, block) capital, correctly narrows wide
    //     letters (О/Д/Ж), but ALSO squeezes the already-narrow
    //     single-stroke letters (І/Ї/Й) — they have almost no room to
    //     narrow, yet get shrunk by the same factor (without the floor,
    //     Cyrillic 'І' comes out thinner than the native 'I' in the same
    //     line: measured 3px vs 5px for native 'I' and 4px for donor
    //     'І'). Same class of problem as the lowercase Bearing floor
    //     above — one scale can't serve both an "ordinary" and a
    //     "narrow" shape at once. The FLOOR guarantees no Cyrillic
    //     capital ever renders narrower than this font's own narrowest
    //     native capital.
    int CapWidthFloor,
    // UA: "Тіло"/x-height малих літер У ГРІ (медіана Bearing реальних
    //     англійських a-z; baselineOffset − ця медіана). Це НЕ підлога
    //     для клампу Bearing (клампінг до цієї медіани придушив би
    //     виступ і/й/ї/б/ф понад звичайні малі до нуля — "і" виглядала б
    //     врівень з рештою малих, хоча крапка має виступати над), а
    //     ЦІЛЬОВА висота САМЕ "тіла" (core) в моделі ядро+виступи
    //     (ComputeLowercaseCoreMetric нижче): виступи (крапка/дашок/
    //     хвіст) додаються ПОНАД це тіло окремо, через CoreMarginCapPx, а
    //     не відкидаються.
    // EN: The lowercase "core"/x-height IN THE GAME (median Bearing of
    //     the real English a-z; baselineOffset − that median). This is
    //     NOT a clamp floor for Bearing (clamping to this median would
    //     suppress the і/й/ї/б/ф extension above ordinary lowercase down
    //     to zero — "і" would render flush with the rest of the
    //     lowercase, when the dot should protrude above), but the TARGET
    //     height of the "core" itself in the core+extension model
    //     (ComputeLowercaseCoreMetric below): extensions (dot/breve/tail)
    //     are added ON TOP of this core separately, via CoreMarginCapPx,
    //     instead of being discarded.
    int CoreHeightGame,
    // UA: Природна висота "тіла" (x-height) у пробному растрі — медіана
    //     ascent'ів еталонної вибірки CyrillicAlphabet.CoreLowercaseLetters
    //     (літери без висхідних/низхідних елементів). ЗНАМЕННИК масштабу
    //     природа→гра для "тіла" малих літер (окремий від великих —
    //     x-height ≠ cap-height в жодному нормальному шрифті).
    // EN: Natural "core" (x-height) height in the probe raster — median
    //     ascent of the CyrillicAlphabet.CoreLowercaseLetters reference
    //     sample (letters with no ascender/descender). DENOMINATOR of the
    //     natural→game scale for the lowercase "core" (separate from
    //     capitals — x-height ≠ cap-height in any normal font).
    double NaturalCoreAscent,
    // UA: Y-координата ВЕРХУ еталонної x-height-смуги в пробному растрі
    //     (ProbeBaselineY − NaturalCoreAscent, округлено). Все чорнило
    //     ВИЩЕ цієї лінії (для будь-якої малої літери) вважається
    //     "виступом-зверху" (крапка і, дашок й/ї, стрижень б/ф) — GlyphBoxFitRenderer.
    //     ComputeCoreMarginLayout визначає це геометрично, без переліку
    //     літер.
    // EN: Y coordinate of the TOP of the reference x-height band in the
    //     probe raster (ProbeBaselineY − NaturalCoreAscent, rounded). Any
    //     ink ABOVE this line (for any lowercase letter) counts as a "top
    //     extension" (і's dot, й/ї's breve, б/ф's stem) —
    //     GlyphBoxFitRenderer.ComputeCoreMarginLayout determines this
    //     geometrically, with no per-letter list.
    int CoreTopYProbe,
    // UA: Максимум пікселів, дозволених виступу (зверху АБО знизу) понад
    //     "тіло" малої літери. ВИВЕДЕНО з РЕАЛЬНИХ метрик ЦЬОГО шрифту, а
    //     не вгадано: CapHeightGame − CoreHeightGame — тобто виступ ніколи
    //     не може підняти малу літеру вище за високу ВЕЛИКУ літеру цього ж
    //     шрифту (стандартне типографське обмеження: висхідні елементи не
    //     перевищують капітель).
    // EN: Max pixels an extension (top OR bottom) is allowed beyond a
    //     lowercase letter's "core". DERIVED from THIS font's REAL metrics,
    //     not guessed: CapHeightGame − CoreHeightGame — i.e. an extension
    //     can never raise a lowercase letter above this same font's own
    //     capital letter height (standard typographic constraint: ascenders
    //     don't exceed cap-height).
    int CoreMarginCapPx);

// UA: Цільові метрики ОДНІЄЇ літери: розмір бокса, куди пишемо пікселі
//     (BoxWidth×BoxHeight), і поля FBOD (Bearing/CellHeight), які кажуть
//     грі, ДЕ і ЯК ВИСОКО намалювати цей бокс. Інваріант завжди
//     виконується: Bearing + BoxHeight = CellHeight.
// EN: Target metrics of ONE letter: the box size we write pixels into
//     (BoxWidth×BoxHeight), and the FBOD fields (Bearing/CellHeight) that
//     tell the game WHERE and HOW TALL to draw that box. The invariant
//     always holds: Bearing + BoxHeight = CellHeight.
public sealed record LetterTargetMetric(
    char Character,
    int BoxWidth,
    int BoxHeight,
    byte Bearing,
    byte CellHeight,
    // UA: Довідково для діагностики — природні ascent/descent (пробний
    //     растр) і масштаб, з яких усе пораховано.
    // EN: For diagnostics — the natural ascent/descent (probe raster) and
    //     the scale everything was computed from.
    int NaturalAscent,
    int NaturalDescent,
    double Scale);

[SupportedOSPlatform("windows")]
public static class GlyphMetricModel
{
    // UA: Ті самі константи пробного растру, що й у CyrillicGlyphShapeProbe
    //     / GlyphBoxFitRenderer — усі літери міряються при ОДНАКОВОМУ
    //     розмірі шрифту з базовою лінією на ProbeBaselineY, тож ascent/
    //     descent напряму порівнянні між літерами.
    // EN: The same probe-raster constants as CyrillicGlyphShapeProbe /
    //     GlyphBoxFitRenderer — all letters are measured at the SAME font
    //     size with the baseline at ProbeBaselineY, so ascent/descent are
    //     directly comparable across letters.
    private const int ProbeCanvasSize = 400;
    private const float ProbeFontSizePx = 300;
    private const int ProbeBaselineY = 300;

    // UA: Виводить опорні метрики шрифту.
    //       allEnglishCellHeights — CellHeight УСІХ реальних (англійських)
    //         записів цього шрифту (для baselineOffset).
    //       englishCapBearings — Bearing реальних англійських A-Z (для
    //         CapHeightGame = baselineOffset − медіана).
    //       uppercaseTargets — великі кириличні цілі (для природної висоти
    //         великої літери в пробному растрі).
    // EN: Derives the font's reference metrics.
    //       allEnglishCellHeights — CellHeight of ALL real (English)
    //         records of this font (for baselineOffset).
    //       englishCapBearings — Bearing of the real English A-Z (for
    //         CapHeightGame = baselineOffset − median).
    //       uppercaseTargets — the uppercase Cyrillic targets (for the
    //         capital's natural height in the probe raster).
    public static FontMetricReference DeriveReference(
        IReadOnlyList<int> allEnglishCellHeights,
        IReadOnlyList<int> englishCapBearings,
        IReadOnlyList<char> uppercaseTargets,
        string fontFamilyName,
        IReadOnlyList<int>? englishCapInkWidths = null,
        IReadOnlyList<int>? englishLowerBearings = null,
        IReadOnlyList<char>? lowercaseCoreTargets = null)
    {
        if (allEnglishCellHeights.Count == 0)
            throw new ArgumentException(
                "UA: Немає жодного англійського CellHeight для baselineOffset. / " +
                "EN: No English CellHeight for baselineOffset.", nameof(allEnglishCellHeights));
        if (englishCapBearings.Count == 0)
            throw new ArgumentException(
                "UA: Немає жодного англійського Bearing A-Z для CapHeightGame. / " +
                "EN: No English A-Z Bearing for CapHeightGame.", nameof(englishCapBearings));
        if (uppercaseTargets.Count == 0)
            throw new ArgumentException(
                "UA: Немає великих кириличних цілей. / " +
                "EN: No uppercase Cyrillic targets.", nameof(uppercaseTargets));

        var baselineOffset = Median(allEnglishCellHeights);
        var capBearing = Median(englishCapBearings);
        var capHeightGame = Math.Max(1, baselineOffset - capBearing);

        // UA: Природні виміри (ascent І ширина) — з ОДНОГО растеризованого
        //     прогону на літеру.
        // EN: Natural measurements (ascent AND width) — from a SINGLE
        //     rasterize pass per letter.
        var capNatural = uppercaseTargets
            .Select(c => MeasureNatural(c, fontFamilyName))
            .Where(m => m.Ascent > 0)
            .ToList();

        if (capNatural.Count == 0)
            throw new InvalidOperationException(
                "UA: Жодна велика кирилична літера не намалювала чорнила над базовою лінією. / " +
                "EN: No uppercase Cyrillic letter produced ink above the baseline.");

        var naturalCapAscent = Median(capNatural.Select(m => m.Ascent).ToList());

        // UA: Ширина калібрується ОКРЕМИМ масштабом, а НЕ успадковує
        //     висотний scale. ЕМПІРИЧНО (реальний core.lvl, donor vs
        //     no-donor, gamefont_large): якби ширина рахувалась ЯК
        //     natural.InkWidth * (CapHeightGame/NaturalCapAscent) — тобто
        //     висотним масштабом — кириличні великі виходили б СИСТЕМНО
        //     ширші за рідні англійські капітелі цього ж шрифту (медіана
        //     XAdvance 13px): 'О' 18px замість 11 (донор), 'Д' 22 замість
        //     17, 'Ж' 31 замість 17 (+82%!). Причина: Bahnschrift SemiBold
        //     має ПРИРОДНО ширші пропорції (ширина/висота), ніж вузький
        //     конденсований шрифт гри — масштаб, калібрований ЛИШЕ по
        //     висоті, тягнув би за собою й цю "ширшавість" один-в-один.
        //     Тому ширина калібрується ДО МЕДІАННОЇ ширини РІДНИХ
        //     англійських A-Z ЦЬОГО шрифту (загальний параметр шрифту, а
        //     не донорські слоти конкретних літер), а не до природної
        //     ширини Bahnschrift.
        // EN: Width is calibrated by a SEPARATE scale, not inherited from
        //     the height scale. EMPIRICALLY (real core.lvl, donor vs
        //     no-donor, gamefont_large): if width were computed AS
        //     natural.InkWidth * (CapHeightGame/NaturalCapAscent) — i.e.
        //     the HEIGHT scale — Cyrillic capitals would come out
        //     SYSTEMATICALLY wider than this font's own native English
        //     capitals (median XAdvance 13px): 'О' 18px instead of 11
        //     (donor), 'Д' 22 instead of 17, 'Ж' 31 instead of 17 (+82%!).
        //     Cause: Bahnschrift SemiBold has NATURALLY wider proportions
        //     (width/height) than the game's narrow condensed font — a
        //     scale calibrated ONLY by height would drag that "wideness"
        //     along 1:1. So width is calibrated to the MEDIAN width of
        //     THIS font's own native English A-Z (a general font
        //     parameter, not specific letters' donor slots), not to
        //     Bahnschrift's natural width.
        var naturalCapInkWidth = Median(capNatural.Select(m => m.InkWidth).ToList());

        // UA: ВАЖЛИВО — зворотна сумісність. Донорський конвеєр
        //     (CyrillicFontInjector) викликає DeriveReference БЕЗ
        //     englishCapInkWidths, бо донорський підхід (масштабування
        //     під фіксований розмір донорського слоту) вже дає візуально
        //     коректний результат і не потребує окремого масштабу
        //     ширини. Якщо аргумент не передано, підбираємо capWidthGame
        //     так, щоб widthScale = capWidthGame/naturalCapInkWidth ТОЧНО
        //     дорівнював heightScale-для-ширини (capHeightGame/
        //     naturalCapAscent) — тобто поведінка викликів БЕЗ цього
        //     параметра лишається побайтово ідентичною до варіанту з
        //     успадкованим від висоти масштабом ширини.
        // EN: IMPORTANT — backward compatibility. The donor pipeline
        //     (CyrillicFontInjector) calls DeriveReference WITHOUT
        //     englishCapInkWidths, because the donor approach (scaling to
        //     a fixed donor slot size) already produces a visually
        //     correct result and doesn't need a separate width scale. If
        //     the argument isn't supplied, capWidthGame is picked so that
        //     widthScale = capWidthGame/naturalCapInkWidth is EXACTLY the
        //     height-scale-for-width (capHeightGame/naturalCapAscent) —
        //     i.e. callers without this parameter keep byte-identical
        //     behavior to the variant where the width scale is inherited
        //     from height.
        var capWidthGame = englishCapInkWidths is { Count: > 0 }
            ? Median(englishCapInkWidths)
            : naturalCapInkWidth * capHeightGame / naturalCapAscent;

        // UA: Підлога = InkWidth найвужчої рідної капітелі. 0 (без ефекту),
        //     якщо englishCapInkWidths не передано — донорський конвеєр
        //     (CyrillicFontInjector) НЕ зачіпається (той самий принцип
        //     зворотної сумісності, що й для capWidthGame вище).
        // EN: Floor = InkWidth of the narrowest native capital. 0 (no
        //     effect) if englishCapInkWidths isn't supplied — the donor
        //     pipeline (CyrillicFontInjector) stays untouched (same
        //     backward-compat principle as capWidthGame above).
        var capWidthFloor = englishCapInkWidths is { Count: > 0 }
            ? englishCapInkWidths.Min()
            : 0;

        // UA: Опорні метрики "тіла" (core) малих літер, для моделі
        //     ядро+виступи. Той самий принцип зворотної сумісності, що й
        //     вище для ширини: донорський конвеєр (CyrillicFontInjector)
        //     НЕ передає englishLowerBearings — для нього ці три поля
        //     просто не використовуються (RenderWithCoreAndMargin не
        //     викликається з донорського шляху), тож fallback-значення
        //     (0) абсолютно безпечні.
        // EN: Reference metrics for the lowercase "core", for the
        //     core+extension model. Same backward-compat principle as the
        //     width fields above: the donor pipeline
        //     (CyrillicFontInjector) does NOT pass englishLowerBearings —
        //     these three fields simply go unused for it
        //     (RenderWithCoreAndMargin is never called from the donor
        //     path), so the fallback (0) is perfectly safe.
        int coreHeightGame;
        int coreMarginCapPx;
        if (englishLowerBearings is { Count: > 0 })
        {
            var coreBearingMedian = Median(englishLowerBearings);
            coreHeightGame = Math.Max(1, baselineOffset - coreBearingMedian);
            coreMarginCapPx = Math.Max(1, capHeightGame - coreHeightGame);
        }
        else
        {
            coreHeightGame = 0;
            coreMarginCapPx = 0;
        }

        var coreTargets = lowercaseCoreTargets is { Count: > 0 }
            ? lowercaseCoreTargets
            : CyrillicAlphabet.CoreLowercaseLetters;
        var coreNatural = coreTargets
            .Select(c => MeasureNatural(c, fontFamilyName))
            .Where(m => m.Ascent > 0)
            .ToList();
        // UA: Median повертає int, тож naturalCoreAscent — ЦІЛЕ число
        //     пікселів пробного растру; жодного округлення не потрібно
        //     (Math.Round(int) — до того ж неоднозначний виклик:
        //     decimal-vs-double перевантаження, CS0121). У полі
        //     FontMetricReference тип double лише для узгодженості з
        //     NaturalCapAscent — неявне розширення int→double безпечне.
        // EN: Median returns int, so naturalCoreAscent is a WHOLE number
        //     of probe-raster pixels; no rounding needed (and
        //     Math.Round(int) is an ambiguous call anyway: decimal-vs-double
        //     overload, CS0121). The FontMetricReference field is double
        //     only for consistency with NaturalCapAscent — the implicit
        //     int→double widening is safe.
        var naturalCoreAscent = coreNatural.Count > 0
            ? Median(coreNatural.Select(m => m.Ascent).ToList())
            : naturalCapAscent; // UA: запобіжник, не мало б траплятись / EN: fallback, shouldn't happen
        var coreTopYProbe = ProbeBaselineY - naturalCoreAscent;

        return new FontMetricReference(
            baselineOffset, capHeightGame, naturalCapAscent, capWidthGame, naturalCapInkWidth, capWidthFloor,
            coreHeightGame, naturalCoreAscent, coreTopYProbe, coreMarginCapPx);
    }

    // UA: Рахує цільові метрики однієї літери за виведеною моделлю.
    // EN: Computes one letter's target metrics per the derived model.
    public static LetterTargetMetric ComputeLetterMetric(
        char character, string fontFamilyName, FontMetricReference reference)
    {
        var natural = MeasureNatural(character, fontFamilyName);

        // UA: Масштаб природа→гра ПО ВИСОТІ: щоб велика літера вийшла
        //     CapHeightGame пікселів заввишки. Використовується для
        //     ascent/descent (вертикальна вісь — незалежна від масштабу
        //     ширини нижче).
        // EN: natural→game HEIGHT scale: so a capital comes out
        //     CapHeightGame pixels tall. Used for ascent/descent (vertical
        //     axis — independent of the width scale below).
        var scale = reference.CapHeightGame / reference.NaturalCapAscent;

        // UA: Масштаб природа→гра ПО ШИРИНІ — ОКРЕМИЙ від висотного (див.
        //     коментар у DeriveReference біля CapWidthGame/
        //     NaturalCapInkWidth). Калібрований до медіанної ширини
        //     РІДНИХ англійських капітелей ЦЬОГО шрифту, а не до
        //     природних пропорцій Bahnschrift.
        // EN: natural→game WIDTH scale — SEPARATE from the height one
        //     (see the comment in DeriveReference near CapWidthGame/
        //     NaturalCapInkWidth). Calibrated to the median width of THIS
        //     font's own native English capitals, not to Bahnschrift's
        //     natural proportions.
        var widthScale = reference.CapWidthGame / reference.NaturalCapInkWidth;

        var ascentGame = (int)Math.Round(natural.Ascent * scale);
        var descentGame = (int)Math.Round(natural.Descent * scale);

        // UA: CellHeight = базова лінія + хвіст; Bearing = базова лінія −
        //     висота над лінією. Обидва затиснуті в байт; BoxHeight
        //     виводиться як CellHeight − Bearing, щоб інваріант виконувався
        //     ТОЧНО навіть після затиску (тоді екранна висота = висоті
        //     фізичного бокса, масштаб гри = 1, різкість максимальна).
        // EN: CellHeight = baseline + descender; Bearing = baseline −
        //     height above the line. Both clamped to a byte; BoxHeight is
        //     derived as CellHeight − Bearing so the invariant holds
        //     EXACTLY even after clamping (then the on-screen height =
        //     physical box height, game scale = 1, maximum sharpness).
        var cellHeight = Math.Clamp(reference.BaselineOffset + descentGame, 1, 255);
        var bearing = Math.Clamp(reference.BaselineOffset - ascentGame, 0, 254);
        if (bearing >= cellHeight) bearing = cellHeight - 1; // UA: гарантія BoxHeight>=1 / EN: guarantee BoxHeight>=1

        var boxHeight = cellHeight - bearing;
        var boxWidth = Math.Max(1, (int)Math.Round(natural.InkWidth * widthScale));

        // UA: Застосовуємо підлогу ТІЛЬКИ до ВЕЛИКИХ літер — вона
        //     виведена з InkWidth найвужчої рідної КАПІТЕЛІ (типово 'I'),
        //     тож застосовувати її до малих (природно вужчих за великі)
        //     не мало б сенсу. char.IsUpper коректно визначає регістр і
        //     для кириличних символів (категорія Unicode), окремий набір
        //     не потрібен.
        // EN: Apply the floor ONLY to UPPERCASE letters — it's derived
        //     from the InkWidth of the narrowest native CAPITAL (typically
        //     'I'), so applying it to lowercase (naturally narrower than
        //     uppercase) wouldn't make sense. char.IsUpper correctly
        //     detects case for Cyrillic characters too (Unicode category),
        //     no separate set needed.
        if (char.IsUpper(character) && boxWidth < reference.CapWidthFloor)
            boxWidth = reference.CapWidthFloor;

        return new LetterTargetMetric(
            character, boxWidth, boxHeight, (byte)bearing, (byte)cellHeight,
            natural.Ascent, natural.Descent, scale);
    }

    // UA: Замінює лінійний скейл ComputeLetterMetric (калібрований під
    //     ВЕЛИКУ літеру) для МАЛИХ літер. ПРИЧИНА: ComputeLetterMetric
    //     масштабує natural.Ascent ОДНИМ коефіцієнтом
    //     scale=CapHeightGame/NaturalCapAscent — коефіцієнтом, виведеним із
    //     ВЕЛИКИХ літер. Застосований до "і" (де natural.Ascent включає
    //     крапку, тобто вже "видовжений" відносно звичайної малої), цей
    //     коефіцієнт систематично РОЗДУВАВ би ascent і робив "і"
    //     непропорційно високою/тонкою рискою. Простий кламп Bearing до
    //     медіани звичайних малих, зі свого боку, придушив би виступ
    //     УСІХ малих, чий природний Bearing < медіани — включно з
    //     "і"/"й"/"ї"/"б"/"ф" — до нуля (крапка "і" не виступала б над
    //     рештою малих).
    //
    //     Ця модель виправляє ОБИДВІ вади ОДНИМ, повністю автоматичним
    //     механізмом (без жодного списку "особливих" літер): "тіло"
    //     (x-height) КОЖНОЇ малої літери масштабується під CoreHeightGame
    //     (спільний, як у звичайних малих), а частина чорнила ВИЩЕ/НИЖЧЕ
    //     еталонної x-height-смуги — окремий "виступ", що додається
    //     ПОНАД тіло, але НІКОЛИ не перевищує CoreMarginCapPx (виведено з
    //     CapHeightGame цього ж шрифту, не вгадано). Яка саме частина —
    //     "тіло" чи "виступ" — визначається GEOMETRICALLY GlyphBoxFitRenderer.
    //     ComputeCoreMarginLayout (перетин реального InkBounds із
    //     смугою) — той самий код малює 'а' (без виступів), 'і' (виступ
    //     зверху) і 'р' (виступ знизу) без жодної спеціальної гілки на
    //     літеру.
    //
    //     Спершу міряємо ГЕОМЕТРІЮ (без пікселів, дешево) з ЗАВІДОМО
    //     великим boxHeight-обмеженням (probeBoxHeightBound), щоб жоден
    //     виступ не був вимушено урізаний через тісний бокс — так
    //     отримуємо СПРАВЖНІ природні розміри тіла+виступів цієї літери.
    //     Потім будуємо ТОЧНИЙ (без запасу) бокс саме під ці розміри —
    //     інваріант Bearing+BoxHeight=CellHeight виконується так само
    //     точно, як і в ComputeLetterMetric.
    // EN: Replaces the ComputeLetterMetric linear scale (calibrated to a
    //     CAPITAL) for LOWERCASE letters. REASON: ComputeLetterMetric
    //     scales natural.Ascent by ONE factor
    //     scale=CapHeightGame/NaturalCapAscent — a factor derived from
    //     CAPITALS. Applied to "і" (whose natural.Ascent includes the dot,
    //     i.e. already "extended" relative to an ordinary lowercase),
    //     this factor would systematically INFLATE the ascent and make
    //     "і" a disproportionately tall/thin bar. A plain Bearing clamp
    //     to the median of ordinary lowercase, in turn, would suppress the
    //     extension of EVERY lowercase whose natural Bearing is below the
    //     median — including "і"/"й"/"ї"/"б"/"ф" — down to zero (the dot
    //     of "і" wouldn't protrude above the rest of the lowercase).
    //
    //     This model fixes BOTH defects with ONE, fully automatic
    //     mechanism (no "special letter" list at all): every lowercase
    //     letter's "core" (x-height) is scaled to CoreHeightGame (shared,
    //     like ordinary lowercase), and any ink ABOVE/BELOW the reference
    //     x-height band is a separate "extension" added ON TOP of the
    //     core, but NEVER exceeding CoreMarginCapPx (derived from this
    //     same font's CapHeightGame, not guessed). Which part is "core"
    //     vs "extension" per letter is decided GEOMETRICALLY by
    //     GlyphBoxFitRenderer.ComputeCoreMarginLayout (overlap of the
    //     real InkBounds with the band) — the same code draws 'а' (no
    //     extension), 'і' (top extension) and 'р' (bottom extension) with
    //     no per-letter special case at all.
    //
    //     First we measure the GEOMETRY (no pixels, cheap) with a
    //     deliberately large boxHeight bound (probeBoxHeightBound), so no
    //     extension is forcibly clipped by a too-tight box — giving the
    //     letter's TRUE natural core+extension sizes. Then we build the
    //     EXACT (no slack) box around those sizes — the invariant
    //     Bearing+BoxHeight=CellHeight holds exactly, same as in
    //     ComputeLetterMetric.
    // UA: Public const класу (а не private усередині методу), бо
    //     GlyphBoxFitRenderer.ComputeAlphabetExtensionScale (пропорційний
    //     кап виступів, GenerateNoDonorCyrillicCoreCommand /
    //     LowercaseCoreMarginPreviewCommand) МУСИТЬ міряти "природний"
    //     (НЕобрізаний) виступ КОЖНОЇ малої літери — тобто той самий
    //     великий запас boxHeight, що й тут, а не реальну тісну висоту
    //     слоту конкретної літери (яка й так уже обрізана до її ЖЕ
    //     природного розміру — вимір за виміром дав би завжди 0 запасу).
    //     Одна спільна константа замість двох копій одного магічного
    //     числа 250 в різних проєктах.
    // EN: A class-level public const (not a method-local private one),
    //     because GlyphBoxFitRenderer.ComputeAlphabetExtensionScale
    //     (proportional extension cap, GenerateNoDonorCyrillicCoreCommand /
    //     LowercaseCoreMarginPreviewCommand) MUST measure each lowercase
    //     letter's "natural" (UNCLIPPED) extension — i.e. the same large
    //     boxHeight headroom as here, not a specific letter's real tight
    //     slot height (which is already clipped to its OWN natural size —
    //     measuring against itself would always show zero headroom). One
    //     shared constant instead of two copies of the same magic number
    //     250 in different projects.
    public const int LowercaseProbeBoxHeightBound = 250;

    // UA: aboveScale/belowScale (default 1.0 = без пропорційного капу)
    //     дозволяють викликачу застосувати ТОЙ САМИЙ пропорційний
    //     коефіцієнт стиснення виступів, що й рендер (GlyphBoxFitRenderer.
    //     RenderWithCoreAndMargin, ComputeAlphabetExtensionScale). Це
    //     КРИТИЧНО: Bearing/CellHeight/BoxHeight, які повертає цей метод,
    //     стають РЕАЛЬНОЮ геометрією слоту в FBOD — якщо порахувати їх
    //     БЕЗ пропорційного капу (тобто під БІЛЬШИЙ, жорстко клампований
    //     виступ), а потім РЕНДЕРИТИ пікселі викликом
    //     GlyphBoxFitRenderer.RenderWithCoreAndMargin З капом — слот
    //     вийде розрахований на БІЛЬШИЙ виступ, ніж той, що реально
    //     намальований усередині нього (зайвий прозорий запас, і, гірше,
    //     Bearing/CellHeight більше НЕ відповідають фактичному
    //     положенню чорнила). Виклик БЕЗ аргументів (default 1.0)
    //     відповідає жорсткому капу без пропорційного масштабування.
    // EN: aboveScale/belowScale (default 1.0 = no proportional cap) let
    //     the caller apply the SAME proportional extension-shrink factor
    //     as the render does (GlyphBoxFitRenderer.RenderWithCoreAndMargin,
    //     ComputeAlphabetExtensionScale). This is CRITICAL: the
    //     Bearing/CellHeight/BoxHeight this method returns become the
    //     REAL FBOD slot geometry — computing them WITHOUT the
    //     proportional cap (i.e. for a BIGGER, hard-clamped extension)
    //     and then RENDERING pixels via
    //     GlyphBoxFitRenderer.RenderWithCoreAndMargin WITH the cap would
    //     size the slot for a BIGGER extension than what's actually drawn
    //     inside it (wasted transparent headroom, and worse, Bearing/
    //     CellHeight no longer match the ink's real position). A call
    //     WITHOUT the arguments (default 1.0) corresponds to the hard cap
    //     with no proportional scaling.
    public static LetterTargetMetric ComputeLowercaseCoreMetric(
        char character, string fontFamilyName, FontMetricReference reference, int boxWidth,
        double aboveScale = 1.0, double belowScale = 1.0)
    {
        var layout = GlyphBoxFitRenderer.ComputeCoreMarginLayout(
            character, fontFamilyName, reference.CoreHeightGame, reference.CoreTopYProbe,
            reference.CoreMarginCapPx, boxWidth, LowercaseProbeBoxHeightBound, aboveScale, belowScale);

        // UA: boxHeight — сума виміряних тіла й виступів (без додаткового
        //     запасу): гарантований прозорий буфер (SlotPaddingPx)
        //     розширює СЛОТ навколо вже готового чорнила й додається
        //     викликачем окремо (див. SlotPaddingPx і коментар у
        //     GenerateNoDonorCyrillicCoreCommand), а не тут.
        // EN: boxHeight — the sum of the measured core and extensions (no
        //     extra headroom here): the guaranteed transparent buffer
        //     (SlotPaddingPx) grows the SLOT around the already-finished
        //     ink and is added separately by the caller (see
        //     SlotPaddingPx and the comment in
        //     GenerateNoDonorCyrillicCoreCommand), not here.
        var boxHeight = Math.Max(1, layout.AboveRenderH + layout.CoreRenderH + layout.BelowRenderH);
        var bearing = Math.Clamp(reference.BaselineOffset - reference.CoreHeightGame - layout.AboveRenderH, 0, 254);
        var cellHeight = Math.Clamp(bearing + boxHeight, 1, 255);
        if (bearing >= cellHeight) bearing = cellHeight - 1; // UA: гарантія BoxHeight>=1 / EN: guarantee BoxHeight>=1
        boxHeight = cellHeight - bearing;

        // UA: BoxWidth ЗАВЖДИ дорівнює параметру boxWidth, що надійшов
        //     (той самий, що й для ВЕЛИКИХ літер — ширинний масштаб цим
        //     фіксом не чіпається). RenderWithCoreAndMargin центрує
        //     тіло/виступи ВСЕРЕДИНІ цієї ширини самостійно — вузькі
        //     render-частини (layout.CoreRenderW тощо) НЕ мають підміняти
        //     фактичну ширину бокса.
        // EN: BoxWidth ALWAYS equals the incoming boxWidth parameter (the
        //     same one used for UPPERCASE — the width scale is untouched
        //     by this fix). RenderWithCoreAndMargin centers the
        //     core/extensions WITHIN that width itself — the narrower
        //     render pieces (layout.CoreRenderW etc.) must NOT stand in for
        //     the actual box width.
        return new LetterTargetMetric(
            character, boxWidth, boxHeight, (byte)bearing, (byte)cellHeight,
            reference.CoreHeightGame + layout.AboveRenderH, layout.BelowRenderH, layout.CoreScale);
    }

    // UA: Природні виміри літери в пробному растрі: ascent (baseline −
    //     верх чорнила), descent (низ чорнила − baseline, >=0), ширина
    //     чорнила. Той самий растеризатор і константи, що й скрізь.
    // EN: A letter's natural measurements in the probe raster: ascent
    //     (baseline − ink top), descent (ink bottom − baseline, >=0), ink
    //     width. The same rasterizer and constants as everywhere.
    private static (int Ascent, int Descent, int InkWidth) MeasureNatural(
        char character, string fontFamilyName)
    {
        var rasterizer = new GdiGlyphRasterizer();
        var probe = rasterizer.Rasterize(character, new GlyphRasterizeOptions
        {
            FontFamilyName = fontFamilyName,
            FontSizePx = ProbeFontSizePx,
            CanvasWidth = ProbeCanvasSize,
            CanvasHeight = ProbeCanvasSize,
            BaselineY = ProbeBaselineY
        });

        var ink = probe.InkBounds;
        if (ink.Width <= 0 || ink.Height <= 0)
            throw new InvalidOperationException(
                $"UA: Гліф '{character}' шрифтом '{fontFamilyName}' не намалював жодного пікселя. / " +
                $"EN: Glyph '{character}' with font '{fontFamilyName}' produced no pixels.");

        var ascent = ProbeBaselineY - ink.Top;                 // UA: верх над лінією / EN: top above baseline
        var descent = Math.Max(0, ink.Bottom - ProbeBaselineY); // UA: хвіст під лінією / EN: tail below baseline
        return (ascent, descent, ink.Width);
    }

    private static int Median(IReadOnlyList<int> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }
}
