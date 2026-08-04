// =============================================================================
// BF1LocalizationTool.Diagnostic — FontCandidateComparisonCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: ПОСТІЙНИЙ, повторно-запускний інструмент — заміняє одноразовий
//     зовнішній аналіз реальними даними прямо в тулчейні, для повної
//     достовірності звірки того, що в грі, з тим, що додається.
//
//     Читає РЕАЛЬНІ метрики ВАНІЛЬНОГО (ще без кирилиці) шрифту гри —
//     ті самі байти FBOD/HEAD, що й GenerateNoDonorCyrillicCoreCommand
//     використовує для генерації (жодного дублювання чи припущень: той
//     самий FontChunkLocator/FontGlyphTable з Core-проєкту). Для КОЖНОГО
//     кандидата шрифту, знайденого в Fonts\ (через PrivateFontRegistry —
//     той самий шлях/логіка, що й у продакшн-генерації), викликає
//     GlyphMetricModel.DeriveReference — ту саму функцію, яку продакшн
//     генерація викликає з ЦИМ САМИМ шрифтом. Це НЕ окрема, паралельна
//     математика (ризик розбіжності з реальною генерацією) — це той
//     самий код, застосований для порівняння ДО того, як писати файл.
//
//     Ключове число — aspectRatio = widthScale / heightScale, БЕЗРОЗМІРНЕ:
//        widthScale  = CapWidthGame / NaturalCapInkWidth
//        heightScale = CapHeightGame / NaturalCapAscent
//     1.0 = літера масштабується РІВНОМІРНО (природна пропорція
//     кандидата вже збігається з пропорцією шрифту гри, нуль
//     спотворення). <1.0 = літеру доведеться СТИСКАТИ по горизонталі
//     (кандидат природно ширший за шрифт гри). >1.0 = РОЗТЯГУВАТИ
//     (кандидат природно вужчий).
//
//     ВАЖЛИВО: показник спотворення — це САМЕ aspectRatio (відношення
//     widthScale до heightScale), а НЕ widthScale сам по собі. widthScale
//     — це переведення одиниць (пікселі гри ÷ пікселі пробного растру
//     ProbeFontSizePx=300), у ньому взагалі немає висоти, тож він НЕ
//     МОЖЕ показувати спотворення: шрифти з різною висотою капітелі
//     (напр. BF1 CapHeightGame=22px і BF2 CapHeightGame=11px) можуть
//     дати ОДНАКОВИЙ widthScale для того самого кандидата, попри
//     протилежні реальні пропорції (BF1 ≈0.50 — дуже вузький; BF2
//     ≈1.00 — майже квадратний). Сортування за близькістю widthScale до
//     1.0 системно обирало б найвужчий шрифт (Extra Condensed Thin) для
//     обох ігор незалежно від їхньої реальної пропорції. widthScale сам
//     по собі лишається правильним у ПРОДАКШН-генерації
//     (GlyphMetricModel.ComputeLetterMetric), де він і має бути
//     переведенням одиниць — коректним показником спотворення саме тут,
//     у діагностиці, є лише відношення aspectRatio.
// EN: A PERMANENT, re-runnable tool — replaces a one-off external
//     analysis with real data directly in the toolchain, for full
//     reliability when comparing what's in the game against what's
//     being added.
//
//     Reads REAL metrics of the VANILLA (still Cyrillic-free) game font —
//     the SAME FBOD/HEAD bytes GenerateNoDonorCyrillicCoreCommand uses
//     for generation (no duplication or assumptions: the SAME
//     FontChunkLocator/FontGlyphTable from the Core project). For EVERY
//     font candidate found in Fonts\ (via PrivateFontRegistry — the SAME
//     path/logic as production generation), calls
//     GlyphMetricModel.DeriveReference — the SAME function production
//     generation calls with THIS SAME font. This is NOT separate,
//     parallel math (risk of drifting from the real generation) — it's
//     the SAME code, applied for comparison BEFORE writing a file.
//
//     The key number is aspectRatio = widthScale / heightScale,
//     DIMENSIONLESS:
//        widthScale  = CapWidthGame / NaturalCapInkWidth
//        heightScale = CapHeightGame / NaturalCapAscent
//     1.0 = the letter scales UNIFORMLY (the candidate's natural
//     proportion already matches the game font's, zero distortion).
//     <1.0 = the letter must be SQUEEZED horizontally (candidate is
//     naturally wider than the game font). >1.0 = STRETCHED (candidate
//     is naturally narrower).
//
//     IMPORTANT: the distortion metric is aspectRatio (the ratio of
//     widthScale to heightScale), NOT widthScale alone. widthScale is a
//     unit conversion (game pixels ÷ probe-raster pixels,
//     ProbeFontSizePx=300); it contains no height at all, so it CANNOT
//     express distortion: fonts with different cap heights (e.g. BF1
//     CapHeightGame=22px and BF2 CapHeightGame=11px) can produce the
//     SAME widthScale for the same candidate despite opposite real
//     proportions (BF1 ≈0.50 — very narrow; BF2 ≈1.00 — nearly square).
//     Sorting by closeness of widthScale to 1.0 would systematically
//     pick the narrowest font (Extra Condensed Thin) for both games
//     regardless of their real proportion. widthScale itself stays
//     correct in PRODUCTION generation (GlyphMetricModel.ComputeLetterMetric),
//     where it is meant to be a unit conversion — aspectRatio is the
//     only valid distortion metric here, in diagnostics.
// =============================================================================

using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.FontGenerator.Matching;
using BF1LocalizationTool.FontGenerator.Rasterization;

namespace BF1LocalizationTool.Diagnostic;

public static class FontCandidateComparisonCommand
{
    private static readonly string[] TargetFontBaseNames =
        ["gamefont_large", "gamefont_medium", "gamefont_small", "gamefont_tiny", "gamefont_super_tiny"];

    // UA: ДРУГИЙ, НЕЗАЛЕЖНИЙ показник: ЩІЛЬНІСТЬ ШТРИХА
    //     (насиченість/weight). ПРИЧИНА: сам aspect (пропорція)
    //     принципово не може відповісти на питання насиченості, і
    //     покладатись на око тут суперечить правилу "оригінал — наш
    //     вказівник". Але насиченість ТЕЖ вимірна з реальних даних:
    //     беремо РЕАЛЬНІ пікселі атласу ванільного шрифту (ті самі
    //     байти, що малює гра), рахуємо СЕРЕДНЮ альфу в межах
    //     UV-прямокутника кожної англійської капітелі A-Z і беремо
    //     медіану. Це безрозмірне число 0..1 — частка "чорнила" в
    //     клітинці літери, тобто прямий проксі товщини пера.
    //
    //     Потім те саме міряємо для КОЖНОГО кандидата: рендеримо ту саму
    //     A-Z тим самим GdiGlyphRasterizer (той самий растеризатор, що й
    //     продакшн — не паралельна реалізація) у розмірі, підібраному так,
    //     щоб висота чорнила збіглась з CapHeightGame ЦЬОГО шрифту гри,
    //     і рахуємо ту саму середню альфу. Δ до рідної щільності — і є
    //     об'єктивна відповідь "яка насиченість відповідає оригіналу".
    //
    //     ⚠️ Свідоме обмеження: щільність міряється на ПРЯМОМУ рендері,
    //     БЕЗ ширинного стиснення/розтягнення (aspect). Стиснення на 20-40%
    //     ущільнює вертикальні штрихи й трохи ПІДНІМЕ фактичну щільність
    //     у грі — тобто для сильно стиснутих варіантів реальна щільність
    //     буде дещо ВИЩОЮ за виміряну тут. Показник лишається придатним
    //     для ПОРІВНЯННЯ кандидатів між собою (усі в однакових умовах),
    //     але не є точним прогнозом фінального результату.
    // EN: A SECOND, INDEPENDENT metric: STROKE DENSITY (weight). REASON:
    //     aspect (proportion) fundamentally
    //     cannot answer the weight question, and falling back on
    //     eyeballing contradicts the "the original is our guide" rule.
    //     But weight is ALSO measurable from real data: take the REAL
    //     atlas pixels of the vanilla font (the same bytes the game
    //     draws), compute the MEAN alpha within each English capital
    //     A-Z's UV rectangle, and take the median. That's a dimensionless
    //     0..1 number — the "ink" fraction of a letter's cell, i.e. a
    //     direct proxy for pen thickness.
    //
    //     Then measure the same for EVERY candidate: render the same A-Z
    //     with the same GdiGlyphRasterizer (the same rasterizer
    //     production uses — not a parallel implementation) at a size
    //     chosen so the ink height matches THIS game font's
    //     CapHeightGame, and compute the same mean alpha. The Δ to the
    //     native density is the objective answer to "which weight matches
    //     the original".
    //
    //     ⚠️ Deliberate limitation: density is measured on a DIRECT
    //     render, WITHOUT the width squeeze/stretch (aspect). A 20-40%
    //     squeeze compacts vertical strokes and will slightly RAISE the
    //     actual in-game density — so heavily squeezed variants will end
    //     up somewhat DENSER in game than measured here. The metric stays
    //     valid for COMPARING candidates against each other (all under
    //     identical conditions), but is not an exact prediction of the
    //     final result.
    private const int DensityProbeCanvas = 256;

    private static double NativeInkDensity(FontResource font, IReadOnlyList<FontGlyphRecord> records)
    {
        var pixelsByPage = new Dictionary<byte, FontTexturePixels>();
        var densities = new List<double>();

        for (var ch = 'A'; ch <= 'Z'; ch++)
        {
            var r = records.FirstOrDefault(x => x.Code == (ushort)ch);
            if (r is null) continue;
            if (r.PageIndex >= font.TexturePages.Count) continue;

            if (!pixelsByPage.TryGetValue(r.PageIndex, out var tex))
            {
                tex = FontTexturePixelReader.ReadMip0(font.TexturePages[r.PageIndex].Chunk);
                pixelsByPage[r.PageIndex] = tex;
            }

            var x0 = (int)Math.Round(Math.Min(r.U0, r.U1) * tex.Width);
            var x1 = (int)Math.Round(Math.Max(r.U0, r.U1) * tex.Width);
            var y0 = (int)Math.Round(Math.Min(r.V0, r.V1) * tex.Height);
            var y1 = (int)Math.Round(Math.Max(r.V0, r.V1) * tex.Height);
            if (x1 <= x0 || y1 <= y0) continue;

            double sum = 0;
            var count = 0;
            for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++)
                {
                    var (a, _, _, _) = FontTexturePixelReader.DecodePixel(tex.RawA4R4G4B4Pixels, y * tex.Width + x);
                    sum += a / 255.0;
                    count++;
                }
            if (count > 0) densities.Add(sum / count);
        }

        return densities.Count > 0 ? Median(densities) : double.NaN;
    }

    // UA: Щільність кандидата при висоті чорнила == capHeightGame. Розмір
    //     шрифту підбирається бінарним пошуком по РЕАЛЬНОМУ растру 'H'
    //     (не за метриками TTF — щоб врахувати саме растеризацію, як у грі).
    // EN: Candidate density at ink height == capHeightGame. Font size is
    //     found by binary search on the REAL raster of 'H' (not from TTF
    //     metrics — so that rasterization itself is accounted for, as
    //     in-game).
    private static double CandidateInkDensity(string fontFamilyName, int capHeightGame)
    {
        var rasterizer = new GdiGlyphRasterizer();

        GlyphRasterizeOptions Opt(float sizePx) => new()
        {
            FontFamilyName = fontFamilyName,
            FontSizePx = sizePx,
            CanvasWidth = DensityProbeCanvas,
            CanvasHeight = DensityProbeCanvas,
            BaselineY = DensityProbeCanvas * 3 / 4,
        };

        float lo = 2, hi = DensityProbeCanvas / 2f, chosen = capHeightGame;
        for (var i = 0; i < 24; i++)
        {
            var mid = (lo + hi) / 2f;
            var probe = rasterizer.Rasterize('H', Opt(mid));
            var h = probe.InkBounds.Height;
            chosen = mid;
            if (h == capHeightGame) break;
            if (h < capHeightGame) lo = mid; else hi = mid;
        }

        var densities = new List<double>();
        for (var ch = 'A'; ch <= 'Z'; ch++)
        {
            var g = rasterizer.Rasterize(ch, Opt(chosen));
            var ink = g.InkBounds;
            if (ink.Width <= 0 || ink.Height <= 0) continue;

            double sum = 0;
            var count = 0;
            for (var y = ink.Top; y < ink.Bottom; y++)
                for (var x = ink.Left; x < ink.Right; x++)
                {
                    // UA: BGRA, альфа — четвертий байт / EN: BGRA, alpha is the 4th byte
                    sum += g.BgraPixels[(y * g.Width + x) * 4 + 3] / 255.0;
                    count++;
                }
            if (count > 0) densities.Add(sum / count);
        }

        return densities.Count > 0 ? Median(densities) : double.NaN;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }

    public static Task Run(DiagnosticReport report)
    {
        List<string> candidates;
        try
        {
            PrivateFontRegistry.EnsureLoaded();
            // UA: Кандидати ідентифікуються ВІДНОСНИМ ШЛЯХОМ файлу
            //     (LoadedPaths), а не назвою родини — назви родин можуть
            //     бути обрізаними/неоднозначними (напр. "...Extra"
            //     збігається і з ExtraBold, і з ExtraLight —
            //     FONT_FORMAT_SPEC.md розділ 11.13), тоді як файловий шлях
            //     ("FiraSansExtraCondensed-ExtraBold.ttf") завжди
            //     однозначний.
            // EN: Candidates are identified by file RELATIVE PATH
            //     (LoadedPaths), not family name — family names can be
            //     truncated/ambiguous (e.g. "...Extra" collides with both
            //     ExtraBold and ExtraLight — FONT_FORMAT_SPEC.md section
            //     11.13), whereas the file path
            //     ("FiraSansExtraCondensed-ExtraBold.ttf") is always
            //     unambiguous.
            // UA: Курсивні (Italic) файли виключаються з кандидатів.
            //     Причина: курсив нахиляє контур, тому його bounding-box
            //     ширший за той самий гліф у прямому накресленні — це
            //     підвищує "природний" InkWidth і штучно наближає aspect
            //     до цілі, не роблячи курсив насправді придатним для
            //     заміни ПРЯМИХ англійських капітелей у грі (вимірювальний
            //     артефакт, а не коректний збіг). У грі кирилиця має бути
            //     прямою (як оригінал — п.1 стандартних правил), тож
            //     курсив як кандидат на продакшн-шрифт не розглядається
            //     взагалі — виключається ще на етапі списку кандидатів, а
            //     не покладається на сортування/вердикт.
            // EN: Italic files are excluded from candidates. Reason:
            //     slanting the outline widens its bounding box relative to
            //     the same glyph upright, which inflates "natural"
            //     InkWidth and artificially pulls aspect toward the
            //     target without making italic actually suitable for
            //     replacing UPRIGHT English capitals in-game (a
            //     measurement artifact, not a genuine match). In-game
            //     Cyrillic must be upright (matching the original — rule
            //     1), so italic is never a valid production candidate —
            //     excluded at the candidate-list stage rather than relying
            //     on sorting/verdict to catch it.
            candidates = PrivateFontRegistry.LoadedPaths()
                .Where(n => !n.Contains("Italic", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            report.Log($"UA: Fonts\\ не завантажено: {ex.Message} / EN: Fonts\\ failed to load: {ex.Message}");
            return Task.CompletedTask;
        }

        if (candidates.Count == 0)
        {
            report.Log("UA: У Fonts\\ немає жодного придатного шрифту — порівнювати нема з чим. / " +
                       "EN: No usable font in Fonts\\ — nothing to compare.");
            return Task.CompletedTask;
        }

        report.Log($"UA: Знайдено {candidates.Count} кандидат(ів) шрифту в Fonts\\: {string.Join(", ", candidates)}");
        report.Log($"EN: Found {candidates.Count} font candidate(s) in Fonts\\: {string.Join(", ", candidates)}");
        report.Log();
        report.Log("UA: aspect = widthScale/heightScale (БЕЗРОЗМІРНЕ). 1.000 = літера масштабується РІВНОМІРНО, нуль спотворення. " +
                   "<1.0 = кандидат природно ШИРШИЙ за шрифт гри, його доведеться СТИСКАТИ. >1.0 = природно ВУЖЧИЙ, доведеться РОЗТЯГУВАТИ. " +
                   "Наприклад 0.600 = стиснення на 40%, 1.950 = розтягнення майже вдвічі.");
        report.Log("EN: aspect = widthScale/heightScale (DIMENSIONLESS). 1.000 = the letter scales UNIFORMLY, zero distortion. " +
                   "<1.0 = candidate is naturally WIDER than the game font and must be SQUEEZED. >1.0 = naturally NARROWER, must be STRETCHED. " +
                   "E.g. 0.600 = squeezed by 40%, 1.950 = stretched almost 2×.");

        var games = new[]
        {
            ("BF1", Path.Combine(AppContext.BaseDirectory, "reference-files", "BF1", "core.lvl")),
            ("BF2", Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2", "core.lvl")),
        };

        foreach (var (label, path) in games)
        {
            if (!File.Exists(path))
            {
                report.Log();
                report.Log($"UA: [{label}] {path} не знайдено — пропущено. / EN: [{label}] {path} not found — skipped.");
                continue;
            }

            var root = UcfbReader.ReadFile(path);
            var fonts = FontChunkLocator.FindAll(root);

            foreach (var fontBaseName in TargetFontBaseNames)
            {
                var font = fonts.FirstOrDefault(f => f.BaseName == fontBaseName);
                if (font is null) continue;

                var fbodChunk = UcfbReader.FindFirst(font.Chunk, "FBOD");
                if (fbodChunk is null) continue;

                var originalRecords = FontGlyphTable.Parse(fbodChunk.RawData);
                var allEnglishCellHeights = originalRecords.Select(r => (int)r.CellHeight).ToList();
                var englishCapBearings = originalRecords
                    .Where(r => r.Code is >= (ushort)'A' and <= (ushort)'Z')
                    .Select(r => (int)r.Bearing).ToList();
                var englishCapInkWidths = originalRecords
                    .Where(r => r.Code is >= (ushort)'A' and <= (ushort)'Z')
                    .Select(r => (int)r.InkWidth).ToList();
                var englishLowerBearings = originalRecords
                    .Where(r => r.Code is >= (ushort)'a' and <= (ushort)'z')
                    .Select(r => (int)r.Bearing).ToList();

                report.Log();
                report.Log($"-- [{label}] {fontBaseName} --");

                var nativeDensity = NativeInkDensity(font, originalRecords);

                var results = new List<(string Candidate, FontMetricReference Reference, double Aspect, double Density)>();
                foreach (var candidate in candidates)
                {
                    try
                    {
                        var reference = GlyphMetricModel.DeriveReference(
                            allEnglishCellHeights, englishCapBearings, CyrillicAlphabet.UppercaseLetters, candidate,
                            englishCapInkWidths, englishLowerBearings);

                        // UA: Обидва масштаби — ті самі, що реально застосовує
                        //     GlyphMetricModel.ComputeLetterMetric. Їхнє
                        //     ВІДНОШЕННЯ безрозмірне: одиниці пробного растру
                        //     й пікселі гри скорочуються, лишається чиста
                        //     пропорція форми.
                        // EN: Both scales are exactly what
                        //     GlyphMetricModel.ComputeLetterMetric actually
                        //     applies. Their RATIO is dimensionless: probe-raster
                        //     units and game pixels cancel out, leaving pure
                        //     shape proportion.
                        var widthScale = reference.CapWidthGame / reference.NaturalCapInkWidth;
                        var heightScale = reference.CapHeightGame / reference.NaturalCapAscent;
                        var aspect = widthScale / heightScale;
                        var density = CandidateInkDensity(candidate, reference.CapHeightGame);
                        results.Add((candidate, reference, aspect, density));
                    }
                    catch (Exception ex)
                    {
                        report.Log($"   [{candidate}] УВАГА — не вдалось виміряти: {ex.Message} / [{candidate}] WARNING — could not measure: {ex.Message}");
                    }
                }

                if (results.Count > 0)
                {
                    var gameAspect = results[0].Reference.CapWidthGame / (double)results[0].Reference.CapHeightGame;
                    report.Log($"   ОРИГІНАЛ ГРИ: пропорція CapWidthGame={results[0].Reference.CapWidthGame:F1}px / CapHeightGame={results[0].Reference.CapHeightGame}px = {gameAspect:F3}, " +
                               $"щільність штриха={nativeDensity:F3}");
                    report.Log($"   GAME ORIGINAL: proportion {gameAspect:F3}, stroke density {nativeDensity:F3}");
                }

                // UA: Сортуємо за СУМАРНИМ відхиленням від оригіналу по ОБОХ
                //     осях (пропорція + щільність), нормованим у частках:
                //     жоден показник окремо не є достатнім (aspect не бачить
                //     насиченості, density не бачить пропорції).
                // EN: Sorted by the COMBINED deviation from the original on
                //     BOTH axes (proportion + density), normalized as
                //     fractions: neither metric alone is sufficient (aspect
                //     is blind to weight, density is blind to proportion).
                var ordered = results
                    .OrderBy(r => Math.Abs(r.Aspect - 1.0)
                                  + (double.IsNaN(r.Density) || double.IsNaN(nativeDensity)
                                      ? 0
                                      : Math.Abs(r.Density - nativeDensity) / Math.Max(0.001, nativeDensity)))
                    .ToList();

                for (var i = 0; i < ordered.Count; i++)
                {
                    var r = ordered[i];
                    var natAspect = r.Reference.NaturalCapInkWidth / r.Reference.NaturalCapAscent;
                    var shapeVerdict = r.Aspect < 0.95 ? $"стиснення {(1 - r.Aspect) * 100:F0}%"
                        : r.Aspect > 1.05 ? $"розтягнення {(r.Aspect - 1) * 100:F0}%"
                        : "форма OK";
                    var densDelta = double.IsNaN(r.Density) ? double.NaN : r.Density - nativeDensity;
                    var densVerdict = double.IsNaN(densDelta) ? "щільність н/д"
                        : Math.Abs(densDelta) / Math.Max(0.001, nativeDensity) < 0.06 ? "штрих OK"
                        : densDelta > 0 ? $"штрих жирніший {densDelta / nativeDensity * 100:F0}%"
                        : $"штрих тонший {-densDelta / nativeDensity * 100:F0}%";
                    var mark = i == 0 ? "←" : " ";
                    report.Log(
                        $"  {mark} {r.Candidate,-38} aspect={r.Aspect:F3} (природна {natAspect:F3})  " +
                        $"щільність={r.Density:F3}  [{shapeVerdict}; {densVerdict}]");
                }
            }
        }

        report.Log();
        report.Log("UA: Обидва показники — з РЕАЛЬНИХ даних: пропорція з FBOD-метрик, щільність з пікселів атласу " +
                   "(тих самих байтів, що малює гра). Сортування — за сумарним відхиленням по обох осях, бо жоден " +
                   "окремо не достатній: aspect сліпий до насиченості, щільність сліпа до пропорції.");
        report.Log("EN: Both metrics come from REAL data: proportion from FBOD metrics, density from atlas pixels " +
                   "(the same bytes the game draws). Sorted by the combined deviation on both axes, since neither " +
                   "alone is sufficient: aspect is blind to weight, density is blind to proportion.");
        report.Log();
        report.Log("UA: ЩО ЦЕЙ ІНСТРУМЕНТ НЕ МІРЯЄ (не роби з нього єдиного судді): (1) МАЛІ літери — їхня вертикаль " +
                   "рахується окремою моделлю ядро+виступ (FONT_FORMAT_SPEC.md, розділ 11.8); (2) читабельність " +
                   "форми на 7-10px (тонкі штрихи зникають, товсті заливають отвори 'о'/'а'/'е') — це видно лише " +
                   "оком у грі; (3) щільність міряна БЕЗ ширинного стиснення, тож для сильно стиснутих варіантів " +
                   "реальна буде трохи вищою.");
        report.Log("EN: WHAT THIS TOOL DOES NOT MEASURE (don't treat it as the sole judge): (1) LOWERCASE — its " +
                   "vertical axis uses the separate core+extension model (FONT_FORMAT_SPEC.md, section 11.8); " +
                   "(2) shape legibility at 7-10px (thin strokes vanish, heavy ones fill in the counters of " +
                   "'o'/'a'/'e') — only visible by eye in-game; (3) density is measured WITHOUT the width squeeze, " +
                   "so heavily squeezed variants will run slightly denser in reality.");

        return Task.CompletedTask;
    }
}
