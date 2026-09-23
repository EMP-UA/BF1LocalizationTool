// =============================================================================
// BF1LocalizationTool.FontGenerator — Injection/CyrillicFontInjector.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Оркестратор — зшиває вже написані й перевірені шматки для ОДНОГО
//     шрифту в одну дію: ріст без конфліктів → рендер кожної з 66 літер →
//     конвертація в пікселі → патч BODY+FBOD.
//
//     СПІЛЬНА таблиця кодів: цей клас НЕ викликає GlyphDonorMatcher.Assign
//     (крок "яка літера → який донор") сам, окремо для КОЖНОГО шрифту —
//     бо донорський пул різних розмірів шрифту різний (перевірено
//     PerFontSafeDonorCommand), і за такої схеми та сама літера могла б
//     отримати РІЗНИЙ байт-код у gamefont_large і в gamefont_tiny.
//     Оскільки локалізаційний ТЕКСТ (байти) спільний для всієї гри
//     незалежно від того, яким шрифтом його намалюють у конкретному
//     UI-елементі, це означало б, що той самий байт 0xB5 — 'х' в одному
//     розмірі шрифту і зовсім інша літера в іншому.
//
//     Призначення "літера→код" робиться ОДИН РАЗ на всю гру, ЗОВНІ
//     (GenerateLocalizedCoreCommand, на перетині безпечних кодів УСІХ
//     шрифтів — емпірично підтверджено PerFontSafeDonorCommand: 74 коди
//     для BF1, 91 для BF2, за потреби 66), і передається сюди вже
//     готовим списком `assignments`. Цей клас НЕ викликає
//     GlyphDonorMatcher.Assign — лише розділяє призначення по сторінках
//     ЦЬОГО шрифту й росте/рендерить/патчить, використовуючи ВЛАСНУ
//     геометрію цього шрифту (donorRectsByCode) для фактичних розмірів —
//     GrowthResolver рахує width/height/currentRatio заново з переданого
//     прямокутника (перевірено, Matching/GrowthResolver.cs), тож
//     коректність росту не залежить від того, з якого шрифту
//     обчислювався сам assignment.
// EN: Orchestrator — stitches together already-written and verified
//     pieces for ONE font into a single action: conflict-free growth →
//     rendering each of the 66 letters → converting to pixels → patching
//     BODY+FBOD.
//
//     SHARED code table: this class does NOT call GlyphDonorMatcher.Assign
//     itself (the "which letter → which donor" step) separately for
//     EACH font — because the donor pool differs per font size
//     (confirmed by PerFontSafeDonorCommand), and under that scheme the
//     same letter could end up on a DIFFERENT byte-code in
//     gamefont_large vs gamefont_tiny. Since localization TEXT (bytes)
//     is shared across the whole game regardless of which font renders
//     it in a given UI element, that would mean the same byte 0xB5 is
//     'х' in one font size and a completely different letter in
//     another.
//
//     The "letter→code" assignment is made ONCE for the whole game,
//     EXTERNALLY (GenerateLocalizedCoreCommand, on the intersection of
//     ALL fonts' safe codes — empirically confirmed by
//     PerFontSafeDonorCommand: 74 codes for BF1, 91 for BF2, against 66
//     needed), and handed to this class as a ready `assignments` list.
//     This class does not call GlyphDonorMatcher.Assign — it only
//     splits the assignment by THIS font's pages and grows/renders/patches
//     using THIS font's OWN geometry (donorRectsByCode) for the actual
//     sizes — GrowthResolver recomputes width/height/currentRatio fresh
//     from the passed-in rectangle (confirmed, Matching/GrowthResolver.cs),
//     so growth correctness does not depend on which font the assignment
//     itself was computed from.
//
//     СВІДОМО НЕ робить: не вирішує, які донори безпечні (мовний аналіз,
//     геометричні перетини, м'які донори — уся ця логіка вже існує в
//     Diagnostic-проєкті й НЕ дублюється тут; FontGenerator не може
//     посилатись на Diagnostic — залежність лише в один бік,
//     Diagnostic→FontGenerator). Викликаючий код (Diagnostic-команда
//     генерації) відповідає за передачу вже перевіреного й УЗГОДЖЕНОГО
//     між усіма шрифтами гри списку assignments і карт зайнятості.
//
//     XAdvance для нових записів: InkWidth_нового_гліфа + медіана
//     (XAdvance-InkWidth) РЕАЛЬНИХ записів ЦЬОГО САМОГО шрифту —
//     підтверджена FontGlyphMetricsCorrelationCommand тісна кореляція
//     (стандартне відхилення 0,40-1,44 по всіх 11 шрифтах), рахується
//     тут же з originalRecords, а не приймається зовні окремим
//     параметром — джерело даних одне й те саме.
// EN: DELIBERATELY does NOT: decide which donors are safe (language
//     analysis, geometric overlaps, soft donors — all that logic already
//     exists in the Diagnostic project and is NOT duplicated here;
//     FontGenerator cannot reference Diagnostic — the dependency only
//     goes one way, Diagnostic→FontGenerator). The calling code (the
//     Diagnostic generation command) is responsible for supplying an
//     already-vetted, CROSS-FONT-CONSISTENT assignments list and
//     occupancy maps.
//
//     XAdvance for new records: new glyph's InkWidth + the median
//     (XAdvance-InkWidth) of REAL records of THIS SAME font — the tight
//     correlation confirmed by FontGlyphMetricsCorrelationCommand
//     (standard deviation 0.40-1.44 across all 11 fonts), computed right
//     here from originalRecords rather than accepted as a separate
//     external parameter — same data source either way.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.FontGenerator.AtlasPatching;
using BF1LocalizationTool.FontGenerator.Matching;
using BF1LocalizationTool.FontGenerator.PixelConversion;
using BF1LocalizationTool.FontGenerator.Rasterization;

namespace BF1LocalizationTool.FontGenerator.Injection;

// UA: Контекст ОДНІЄЇ текстурної сторінки, потрібний оркестратору.
//     Occupied МУТУЄТЬСЯ під час InjectFont — передавай робочу копію.
// EN: Context of ONE texture page, needed by the orchestrator. Occupied
//     is MUTATED during InjectFont — pass a working copy.
public sealed record PageInjectionContext(UcfbChunk BodyChunk, int Width, int Height, bool[,] Occupied);

public sealed record FontInjectionResult(
    Dictionary<long, byte[]> Replacements,
    IReadOnlyList<GrownGlyphPlacement> Placements,
    int LettersInjected,
    FontMetricReference MetricReference,
    IReadOnlyDictionary<char, LetterTargetMetric> MetricsByChar);

// UA: Результат GrowAndRenderGlyphs — рендер-плейсменти ПЛЮС опорні
//     метрики шрифту (FontMetricReference) і цільові метрики КОЖНОЇ
//     літери (LetterTargetMetric: Bearing/CellHeight/розмір бокса), які
//     фактично пішли в FBOD. Винесено в окремий record, щоб Diagnostic-шар
//     міг ЗАЛОГУВАТИ ці точні числа й перевірити коректність, а не "на око".
// EN: Result of GrowAndRenderGlyphs — render placements PLUS the font's
//     reference metrics (FontMetricReference) and EACH letter's target
//     metrics (LetterTargetMetric: Bearing/CellHeight/box size) that
//     actually went into FBOD. Pulled into its own record so the
//     Diagnostic layer can LOG these exact numbers and verify correctness
//     rather than "by eye".
public sealed record GrowAndRenderResult(
    IReadOnlyList<RenderedGlyphPlacement> Placements,
    FontMetricReference MetricReference,
    IReadOnlyDictionary<char, LetterTargetMetric> MetricsByChar);

// UA: Результат росту+рендеру ОДНІЄЇ літери — ДО побудови патча. Винесено
//     окремо (замість того, щоб лишати це деталлю реалізації InjectFont),
//     щоб ТОЧНО ТОЙ САМИЙ код (GrowAndRenderGlyphs), який пише пікселі в
//     реальну гру, можна було перевикористати в діагностичному прев'ю
//     (напр. новий інструмент для збільшеного перегляду конкретного
//     слова) — БЕЗ ризику, що прев'ю з часом розійдеться з реальною
//     ін'єкцією (уже траплялось: GlyphFitRenderPreviewCommand.cs
//     використовує СТАРИЙ RenderToFit і не показує актуальну картину).
// EN: The result of growing+rendering ONE letter — BEFORE building a
//     patch. Pulled out separately (instead of leaving it as an
//     implementation detail of InjectFont) so the EXACT SAME code
//     (GrowAndRenderGlyphs) that writes pixels into the real game can be
//     reused by diagnostic previews (e.g. a new tool for a zoomed view of
//     a specific word) — WITHOUT the risk of the preview drifting out of
//     sync with the real injection over time (already happened once:
//     GlyphFitRenderPreviewCommand.cs uses the OLD RenderToFit and
//     doesn't show the current picture).
public sealed record RenderedGlyphPlacement(
    char Character, ushort DonorCode, GlyphRectBounds Rect, RasterizedGlyph Rendered,
    // UA: Метричні поля FBOD ЦІЄЇ літери (GlyphMetricModel) — пишуться в
    //     запис замість копіювання з донора (виправлення "стрибання").
    // EN: This letter's FBOD metric fields (GlyphMetricModel) — written
    //     into the record instead of being copied from the donor (the
    //     "jumping" fix).
    byte Bearing, byte CellHeight);

public static class CyrillicFontInjector
{
    public static FontInjectionResult InjectFont(
        UcfbChunk fbodChunk,
        IReadOnlyList<FontGlyphRecord> originalRecords,
        IReadOnlyList<GlyphAssignment> assignments,
        IReadOnlyDictionary<ushort, GlyphRectBounds> donorRectsByCode,
        IReadOnlyDictionary<byte, PageInjectionContext> pagesByIndex,
        string fontFamilyName)
    {
        var recordByCode = originalRecords.ToDictionary(r => r.Code);
        var growResult = GrowAndRenderGlyphs(assignments, donorRectsByCode, pagesByIndex, recordByCode, fontFamilyName);
        var rendered = growResult.Placements;

        // UA: Медіана (XAdvance-InkWidth) РЕАЛЬНИХ записів цього шрифту
        //     — та сама стала, що й в FontGlyphMetricsCorrelationCommand.
        // EN: Median (XAdvance-InkWidth) of this font's REAL records —
        //     the same constant as in FontGlyphMetricsCorrelationCommand.
        var xAdvancePadding = ComputeMedianXAdvancePadding(originalRecords);

        // UA: Крок 4 — конвертація в пікселі й побудова патчів, з уже
        //     готового rendered (Крок 1-3 — GrowAndRenderGlyphs).
        // EN: Step 4 — pixel conversion and building patches, from the
        //     already-computed rendered list (Steps 1-3 —
        //     GrowAndRenderGlyphs).
        var patches = new List<GlyphPatch>();
        foreach (var r in rendered)
        {
            var donorRecord = recordByCode[r.DonorCode];
            var page = pagesByIndex[donorRecord.PageIndex];
            var pixelBytes = GlyphPixelConverter.ToA4R4G4B4(r.Rendered);

            // UA: InkWidth нового гліфа = РЕАЛЬНА ширина чорнила
            //     (r.Rendered.InkBounds.Width), а НЕ повна ширина слоту —
            //     RenderWithCoreAndMargin МОЖЕ лишати прозорі поля по
            //     боках, коли натуральна пропорція літери вужча за
            //     донора.
            // EN: New glyph's InkWidth = the ACTUAL ink width
            //     (r.Rendered.InkBounds.Width), NOT the full slot width —
            //     RenderWithCoreAndMargin MAY leave transparent side
            //     margins when the letter's natural proportion is
            //     narrower than the donor.
            var newInkWidth = (byte)Math.Clamp(r.Rendered.InkBounds.Width, 0, 255);
            var newXAdvance = (byte)Math.Clamp(r.Rendered.InkBounds.Width + xAdvancePadding, 0, 255);

            patches.Add(new GlyphPatch
            {
                DonorRecord = donorRecord,
                BodyChunk = page.BodyChunk,
                TextureWidth = page.Width,
                TextureHeight = page.Height,
                RectMinX = r.Rect.MinX,
                RectMinY = r.Rect.MinY,
                CanvasWidth = r.Rect.Width,
                CanvasHeight = r.Rect.Height,
                PixelBytesA4R4G4B4 = pixelBytes,
                NewXAdvance = newXAdvance,
                NewInkWidth = newInkWidth,
                // UA: Метричні поля (GlyphMetricModel) — пишуться явно.
                // EN: Metric fields (GlyphMetricModel) — written explicitly.
                NewBearing = r.Bearing,
                NewCellHeight = r.CellHeight,
                DebugLabel = $"{r.Character} → 0x{r.DonorCode:X2}",
            });
        }

        var replacements = GlyphAtlasPatcher.BuildReplacements(patches, fbodChunk, originalRecords);
        var allPlacements = rendered.Select(r => new GrownGlyphPlacement(r.Character, r.DonorCode, r.Rect)).ToList();

        return new FontInjectionResult(replacements, allPlacements, allPlacements.Count, growResult.MetricReference, growResult.MetricsByChar);
    }

    // UA: Кроки 1-3 (ріст без конфліктів → еталонна геометрія "тіла" на
    //     регістр → рендер КОЖНОЇ літери через RenderWithCoreAndMargin) —
    //     винесено з InjectFont ОКРЕМИМ публічним методом, щоб діагностичні
    //     прев'ю могли викликати ТОЧНО ТОЙ САМИЙ код, що й реальна
    //     ін'єкція (див. коментар біля RenderedGlyphPlacement).
    // EN: Steps 1-3 (conflict-free growth → per-case "core" reference
    //     geometry → render EVERY letter via RenderWithCoreAndMargin) —
    //     pulled out of InjectFont as a SEPARATE public method, so
    //     diagnostic previews can call the EXACT SAME code as the real
    //     injection (see the comment next to RenderedGlyphPlacement).
    public static GrowAndRenderResult GrowAndRenderGlyphs(
        IReadOnlyList<GlyphAssignment> assignments,
        IReadOnlyDictionary<ushort, GlyphRectBounds> donorRectsByCode,
        IReadOnlyDictionary<byte, PageInjectionContext> pagesByIndex,
        IReadOnlyDictionary<ushort, FontGlyphRecord> recordByCode,
        string fontFamilyName)
    {
        // UA: Крок 1 (яка літера → який байт-код) уже зроблено ЗОВНІ, ОДИН
        //     РАЗ на всю гру (GenerateLocalizedCoreCommand) — див. заголовок
        //     файлу. Тут лишається метрична модель + рендер ДЛЯ ЦЬОГО шрифту.
        // EN: Step 1 (which letter → which byte-code) is already done
        //     EXTERNALLY, ONCE for the whole game (GenerateLocalizedCoreCommand)
        //     — see file header. Only the metric model + render FOR THIS
        //     font remain here.
        //
        //     ПЕРЕРОБЛЕНО НА МЕТРИЧНУ МОДЕЛЬ (GlyphMetricModel): увесь
        //     попередній піксельний core/margin-підхід (і shared-height, і
        //     4 цілі-висоти, і core/margin) розв'язував НЕ ТУ задачу —
        //     проблема "стрибання" була не в пікселях текстури, а в тому,
        //     що вертикальні метрики FBOD (Bearing/CellHeight) сліпо
        //     копіювались із випадкового англійського донора. Доведено
        //     двома контрольованими експериментами (див. GlyphMetricModel):
        //       - CellHeight керує вертикальним масштабом (ж: 19→30 виросла
        //         й сіла на лінію);
        //       - інваріант гри Bearing + висота_бокса = CellHeight (28
        //         англійських літер, без винятку);
        //       - метричний тест (Bearing = CellHeight − бокс) вирівняв
        //         базову лінію в грі.
        //     Тепер: рахуємо правильні Bearing/CellHeight/розмір бокса з
        //     ПРИРОДНОЇ форми кожної літери, розміщуємо слот цільового
        //     розміру, рендеримо тісним кропом (RenderToFit) і пишемо
        //     метрики в FBOD (GlyphAtlasPatcher).

        // UA: Крок 2 — опорні метрики ЦЬОГО шрифту з його РЕАЛЬНИХ
        //     англійських записів (baselineOffset, capHeightGame) + пробний
        //     растр великих кириличних (природна висота великої літери).
        // EN: Step 2 — this font's reference metrics from its REAL English
        //     records (baselineOffset, capHeightGame) + the probe raster of
        //     uppercase Cyrillic (the capital's natural height).
        var allEnglishCellHeights = recordByCode.Values.Select(r => (int)r.CellHeight).ToList();
        var englishCapBearings = recordByCode.Values
            .Where(r => r.Code is >= (ushort)'A' and <= (ushort)'Z')
            .Select(r => (int)r.Bearing)
            .ToList();

        var metricReference = GlyphMetricModel.DeriveReference(
            allEnglishCellHeights, englishCapBearings, CyrillicAlphabet.UppercaseLetters, fontFamilyName);

        // UA: Крок 3 — цільові метрики КОЖНОЇ літери (розмір бокса +
        //     Bearing/CellHeight) з її природної форми.
        // EN: Step 3 — EACH letter's target metrics (box size +
        //     Bearing/CellHeight) from its natural shape.
        var metricsByChar = assignments.ToDictionary(
            a => a.Character,
            a => GlyphMetricModel.ComputeLetterMetric(a.Character, fontFamilyName, metricReference));

        // UA: Крок 4 — розміщення слота ЦІЛЬОВОГО розміру, ОКРЕМО по кожній
        //     сторінці (карта зайнятості й розміри — свої для кожної).
        //     ResolveMetricSlots: здебільшого стискає донора до цілі (місце
        //     не потрібне), а де донор замалий ('ж') — росте у вільний
        //     простір.
        // EN: Step 4 — placing a slot of the TARGET size, SEPARATELY per
        //     page (occupancy map and dimensions are each page's own).
        //     ResolveMetricSlots: usually shrinks the donor to the target
        //     (no space needed), and where the donor is too small ('ж') —
        //     grows into free space.
        var assignmentsByPage = assignments
            .GroupBy(a => recordByCode[a.DonorCode].PageIndex)
            .ToList();

        var allPlacements = new List<GrownGlyphPlacement>();
        foreach (var pageGroup in assignmentsByPage)
        {
            if (!pagesByIndex.TryGetValue(pageGroup.Key, out var page))
                throw new ArgumentException(
                    $"UA: Сторінка з PageIndex={pageGroup.Key} відсутня в pagesByIndex. / " +
                    $"EN: Page with PageIndex={pageGroup.Key} is missing from pagesByIndex.");

            var rectsForThisPage = pageGroup.ToDictionary(a => a.DonorCode, a => donorRectsByCode[a.DonorCode]);
            var targetsForThisPage = pageGroup.ToDictionary(
                a => a.DonorCode,
                a => (metricsByChar[a.Character].BoxWidth, metricsByChar[a.Character].BoxHeight));

            var placements = GrowthResolver.ResolveMetricSlots(
                pageGroup.ToList(), rectsForThisPage, targetsForThisPage, page.Occupied, page.Width, page.Height);

            allPlacements.AddRange(placements);
        }

        // UA: Крок 5 — рендер КОЖНОЇ літери тісним кропом у її слот
        //     (RenderToFit: вирізати чорнило, розтягнути точно під розмір
        //     слоту — жодних полів, форма зберігається бо слот має цільову
        //     пропорцію), і проносимо метричні Bearing/CellHeight у FBOD.
        //     Для звичайного випадку (слот = цільовому розміру) масштаб
        //     рендеру гри = 1: різко й точного розміру.
        // EN: Step 5 — render EACH letter with a tight crop into its slot
        //     (RenderToFit: crop the ink, stretch exactly to the slot size
        //     — no margins, shape preserved since the slot has the target
        //     aspect), and carry the metric Bearing/CellHeight into FBOD.
        //     In the common case (slot = target size) the game's render
        //     scale = 1: crisp and exactly sized.
        var result = new List<RenderedGlyphPlacement>();
        foreach (var placement in allPlacements)
        {
            var metric = metricsByChar[placement.Character];
            var rasterized = GlyphBoxFitRenderer.RenderToFit(
                placement.Character, fontFamilyName, placement.Rect.Width, placement.Rect.Height);

            result.Add(new RenderedGlyphPlacement(
                placement.Character, placement.DonorCode, placement.Rect, rasterized,
                metric.Bearing, metric.CellHeight));
        }

        return new GrowAndRenderResult(result, metricReference, metricsByChar);
    }

    private static int ComputeMedianXAdvancePadding(IReadOnlyList<FontGlyphRecord> records)
    {
        var diffs = records
            .Where(r => r.InkWidth > 0)
            .Select(r => r.XAdvance - r.InkWidth)
            .OrderBy(d => d)
            .ToList();

        if (diffs.Count == 0)
            throw new InvalidOperationException(
                "UA: Жодного запису з InkWidth>0 — неможливо обчислити медіану XAdvance-InkWidth. / " +
                "EN: No record with InkWidth>0 — cannot compute the XAdvance-InkWidth median.");

        return diffs[diffs.Count / 2];
    }
}
