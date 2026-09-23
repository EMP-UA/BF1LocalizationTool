// =============================================================================
// BF1LocalizationTool.Diagnostic — PerFontSafeDonorCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: ВСТАНОВЛЕНО (GlyphPageGroupingOverlapCommand: 0 перетинів у
//     11/11 шрифтів після групування за FontGlyphRecord.PageIndex):
//     FBOD.offset=2 визначає ЄДИНУ сторінку, якій гліф насправді
//     належить. Тому перетин рахується ЛИШЕ між гліфами з ОДНАКОВИМ
//     PageIndex — точно так, як гліфи фізично лежать в атласі.
//     Застосування спільного списку гліфів до КОЖНОЇ сторінки шрифту
//     без урахування PageIndex дає хибно завищені "перетини" (напр.
//     для gamefont_medium — 0 з 90 безпечних донорів, артефакт методу
//     підрахунку, не факт).
// EN: CONFIRMED (GlyphPageGroupingOverlapCommand: 0 overlaps in 11/11
//     fonts after grouping by FontGlyphRecord.PageIndex): FBOD.offset=2
//     determines the SINGLE page a glyph actually belongs to. So
//     overlap is counted ONLY between glyphs with the SAME PageIndex —
//     exactly how glyphs are physically laid out in the atlas. Applying
//     the shared glyph list to EVERY page of a font without accounting
//     for PageIndex produces falsely inflated "overlaps" (e.g. for
//     gamefont_medium — 0 of 90 safe donors, an artifact of the
//     counting method, not a fact).
// =============================================================================
// UA: UvRectBoundsCheckCommand/RasterizerCanvasSizeCheckCommand/
//     GlyphSlotUniquenessCommand (перевірки перед GlyphAtlasPatcher)
//     фіксують, що в КОЖНОМУ шрифті обох ігор є коди з ГЕОМЕТРИЧНО
//     ВИРОДЖЕНИМ UV-прямокутником (CanvasWidth==0 або CanvasHeight==0) —
//     0x20/0xA0 (пробіли, площа 0×0 — очікувано), а в BF2 ще й 0x5F, 0xAF,
//     0xB7 (підкреслення/макрон/середня крапка — ширина або висота 0,
//     ймовірно артефакт округлення тонкої лінії при бекінгу шрифту;
//     причина не з'ясовувалась глибше, бо для GlyphAtlasPatcher вона не
//     має значення — важливий сам факт нульової площі).
//
//     Такий код не використовує жодна мова, тож сам собою проходить
//     фільтр невикористаних кодів, АЛЕ й не має жодного пікселя, який
//     можна перезаписати — стратегія "перезаписати існуючий гліф-слот"
//     (FONT_FORMAT_SPEC.md розділ 5) вимагає слоту з площею > 0.
//
//     Тому такі коди ЯВНО виключаються з "ефективно безпечних донорів"
//     ОКРЕМИМ фільтром (не через геометричний перетин — вони ні з чим
//     не перетинаються, площа 0×0 не перетинається ні з чим).
// EN: UvRectBoundsCheckCommand/RasterizerCanvasSizeCheckCommand/
//     GlyphSlotUniquenessCommand (pre-GlyphAtlasPatcher checks) find
//     that EVERY font in both games has codes with a GEOMETRICALLY
//     DEGENERATE UV rectangle (CanvasWidth==0 or CanvasHeight==0) —
//     0x20/0xA0 (spaces, 0×0 area — expected), and in BF2 also 0x5F,
//     0xAF, 0xB7 (underscore/macron/middle dot — zero width or height,
//     likely a thin-line rounding artifact from font baking; the cause
//     wasn't investigated further since it doesn't matter for
//     GlyphAtlasPatcher — the zero-area fact itself is what matters).
//
//     Such a code is unused by any language, so it passes the unused-
//     code filter on its own, BUT also has zero pixels to overwrite —
//     the "overwrite existing glyph slot" strategy (FONT_FORMAT_SPEC.md
//     section 5) requires a slot with area > 0.
//
//     Such codes are EXPLICITLY excluded from "effectively safe
//     donors" via a SEPARATE filter (not via geometric overlap — a 0×0
//     area never overlaps anything).
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Localization;
using BF1LocalizationTool.FontGenerator.Matching;

namespace BF1LocalizationTool.Diagnostic;

public static class PerFontSafeDonorCommand
{
    private readonly record struct GlyphRect(ushort Code, int MinX, int MaxX, int MinY, int MaxY);

    public static async Task Run(DiagnosticReport report, UcfbChunk root, string filePath, string label, int neededGlyphCodes)
    {
        var service = new LvlLocalizationService();
        await service.LoadAsync(filePath);

        // UA: ВСТАНОВЛЕНО (реальний скріншот BF1 — "Exit to
        //     Windows" показало "WINDOГs": мала 'w' 0x77, "не знайдена" в
        //     Locl-тексті core.lvl, насправді десь використовується поза
        //     ним). "Не знайдено в Locl" НЕ доводить "гра ніколи не
        //     покаже" — тому ДРУКОВНІ ASCII (0x20-0x7E) ЗАВЖДИ "зайняті",
        //     незалежно від сканування Locl. Лише недруковні керівні
        //     коди (0x00-0x1F, 0x7F) лишаються кандидатами через реальні
        //     дані — їх не може містити жоден легітимний рядок.
        // EN: CONFIRMED (real BF1 screenshot — "Exit to Windows"
        //     showed "WINDOГs": lowercase 'w' 0x77, "not found" in
        //     core.lvl's Locl text, is actually used somewhere outside
        //     it). "Not found in Locl" does NOT prove "the game will
        //     never show this" — so PRINTABLE ASCII (0x20-0x7E) is
        //     ALWAYS "used", regardless of the Locl scan. Only
        //     non-printable control codes (0x00-0x1F, 0x7F) remain real-
        //     data candidates — no legitimate string can contain them.
        bool IsPrintableAscii(ushort code) => code is >= 0x20 and <= 0x7E;
        var usedByAnyLanguage = new HashSet<int>();
        foreach (var lang in service.AvailableLanguages)
        {
            var file = service.GetLanguageFile(lang);
            if (file is null) continue;
            foreach (var entry in file.Entries)
                foreach (var c in entry.Original)
                    if (c is >= (char)0 and <= (char)255)
                        usedByAnyLanguage.Add(c);
        }

        bool IsUsed(ushort code) => IsPrintableAscii(code) || usedByAnyLanguage.Contains(code);

        var globalSafeCandidates = Enumerable.Range(0, 256).Where(x => !IsUsed((ushort)x)).ToHashSet();

        // UA: М'які кандидати, ТОЧНО той самий набір і та сама умова
        //     активації (`< neededGlyphCodes`), що й у GenerateLocalizedCoreCommand,
        //     щоб перетин нижче відображав РЕАЛЬНИЙ пул генерації, а не
        //     гіпотетичний окремий підрахунок.
        // EN: Soft candidates, the EXACT SAME set and activation
        //     condition (`< neededGlyphCodes`) as in GenerateLocalizedCoreCommand,
        //     so the intersection below reflects the REAL generation pool,
        //     not a separate hypothetical count.
        // UA: УЗГОДЖЕНО З GenerateLocalizedCoreCommand: рішення "чи
        //     потрібні м'які донори" приймається ПІСЛЯ фільтрації
        //     шрифтів, за РЕАЛЬНИМ перетином (intersection) нижче — а не
        //     за грубим підрахунком (globalSafeCandidates.Count <
        //     neededGlyphCodes) ДО фільтрації, який ненадійний: на BF2,
        //     після розширення пулу ASCII, груба оцінка піднімається
        //     вище 66, через що груба перевірка вимкнула б м'які донори,
        //     тоді як РЕАЛЬНИЙ перетин без них — лише 57 (замість 91 з
        //     м'якими донорами), і команда генерації відмовилася б
        //     писати файл. Тому м'які кандидати рахуються ЗАВЖДИ
        //     (дешево), а чи вони справді потрібні — видно з РЕАЛЬНОГО
        //     перетину нижче.
        // EN: ALIGNED WITH GenerateLocalizedCoreCommand: the "are soft
        //     donors needed" decision is made AFTER filtering the fonts,
        //     from the REAL intersection below — not from a crude count
        //     (globalSafeCandidates.Count < neededGlyphCodes) BEFORE
        //     filtering, which is unreliable: on BF2, after widening the
        //     ASCII pool, the crude estimate rises above 66, which would
        //     disable soft donors under the crude check, while the REAL
        //     intersection without them is only 57 (instead of 91 with
        //     soft donors) — the generation command would refuse to write
        //     the file. So soft candidates are ALWAYS computed (cheap),
        //     and whether they're actually needed shows up from the REAL
        //     intersection below.
        var summary = await SoftDonorAnalysis.BuildSummaryAsync(filePath);
        var softCandidateCodes = SoftDonorAnalysis.ComputeSoftCandidates(summary).Select(c => c.Code).ToHashSet();

        var englishUsed = summary.Languages
            .Where(l => l.Language.Contains("english", StringComparison.OrdinalIgnoreCase))
            .SelectMany(l => l.CodeCounts.Keys)
            .ToHashSet();

        report.Log($"=== [{label}] Ефективно безпечні донори ПО КОЖНОМУ РОЗМІРУ ШРИФТУ (перетин лише в межах PageIndex) ===");
        report.Log($"=== [{label}] Effectively safe donors PER FONT SIZE (overlap only within PageIndex) ===");
        report.Log($"    Глобальних кандидатів (не використовує жодна мова): {globalSafeCandidates.Count}");
        report.Log($"    Потрібно кодів для повного кириличного алфавіту: {neededGlyphCodes}");
        report.Log();

        var fonts = FontChunkLocator.FindAll(root);

        // UA: Фінальний пул ЦЬОГО шрифту (після перетину, нульової
        //     площі, І фільтра "замалий донор" — GlyphDonorMatcher.FilterOutTooSmall,
        //     ТОЧНО той самий виклик, що в GenerateLocalizedCoreCommand).
        //     Збирається для перетину між шрифтами нижче — без фільтра
        //     замалих донорів перетин був би завищений (оптимістичний),
        //     бо реальна генерація додатково відсіює замалі слоти окремо
        //     для кожного шрифту.
        // EN: This font's final pool (after overlap, zero area, AND
        //     the "donor too small" filter — GlyphDonorMatcher.FilterOutTooSmall,
        //     the EXACT SAME call used by GenerateLocalizedCoreCommand).
        //     Collected for the cross-font intersection below — without the
        //     too-small filter the intersection would be overstated
        //     (optimistic), since real generation additionally excludes
        //     too-small slots separately per font.
        var perFontFinalPools = new List<(string FontName, HashSet<int> BaseOnly, HashSet<int> WithSoft)>();

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var glyphs = FontGlyphTable.Parse(fbod.RawData);

            var unsafeForThisFont = new HashSet<int>();
            var unsafeSoftForThisFont = new HashSet<int>();

            // UA: Коди з геометрично виродженим слотом (площа 0) — не
            //     через перетин з іншим кодом, а самі по собі не мають
            //     жодного пікселя для перезапису.
            // EN: Codes with a geometrically degenerate slot (zero area)
            //     — not from overlapping another code, but with no
            //     pixels of their own to overwrite.
            var zeroAreaForThisFont = new HashSet<int>();

            // UA: Ширина/висота кожного коду в ЦЬОМУ шрифті, потрібна
            //     для GlyphDonorMatcher.FilterOutTooSmall/GetReferenceCapHeight
            //     нижче (той самий підхід, що в GenerateLocalizedCoreCommand).
            // EN: Width/height of each code in THIS font, needed for
            //     GlyphDonorMatcher.FilterOutTooSmall/GetReferenceCapHeight
            //     below (same approach as GenerateLocalizedCoreCommand).
            var geometryByCode = new Dictionary<ushort, (int Width, int Height)>();

            // UA: Групуємо ЛИШЕ за PageIndex цього конкретного запису —
            //     не за номером текстурної сторінки в циклі ззовні.
            // EN: Group ONLY by this specific record's PageIndex — not
            //     by an outer loop's texture page number.
            var byPage = glyphs.GroupBy(g => g.PageIndex);

            foreach (var pageGroup in byPage)
            {
                if (pageGroup.Key >= font.TexturePages.Count)
                    continue; // UA: аномалія — PageIndex поза межами наявних сторінок / EN: anomaly — PageIndex out of range

                var texPixels = FontTexturePixelReader.ReadMip0(font.TexturePages[pageGroup.Key].Chunk);

                var rects = pageGroup.Select(g =>
                {
                    var x0 = (int)Math.Round(g.U0 * texPixels.Width);
                    var x1 = (int)Math.Round(g.U1 * texPixels.Width);
                    var y0 = (int)Math.Round(g.V0 * texPixels.Height);
                    var y1 = (int)Math.Round(g.V1 * texPixels.Height);
                    return new GlyphRect(g.Code, Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1), Math.Max(y0, y1));
                }).ToList();

                foreach (var r in rects)
                {
                    geometryByCode[r.Code] = (r.MaxX - r.MinX, r.MaxY - r.MinY);
                    if (r.MaxX - r.MinX <= 0 || r.MaxY - r.MinY <= 0)
                        zeroAreaForThisFont.Add(r.Code);
                }

                for (var i = 0; i < rects.Count; i++)
                {
                    for (var j = i + 1; j < rects.Count; j++)
                    {
                        var a = rects[i];
                        var b = rects[j];

                        var overlapsX = a.MinX < b.MaxX && b.MinX < a.MaxX;
                        var overlapsY = a.MinY < b.MaxY && b.MinY < a.MaxY;
                        if (!(overlapsX && overlapsY)) continue;

                        var aUsed = IsUsed(a.Code);
                        var bUsed = IsUsed(b.Code);

                        if (aUsed != bUsed)
                            unsafeForThisFont.Add(aUsed ? b.Code : a.Code);

                        if (softCandidateCodes.Contains(a.Code) && englishUsed.Contains(b.Code))
                            unsafeSoftForThisFont.Add(a.Code);
                        if (softCandidateCodes.Contains(b.Code) && englishUsed.Contains(a.Code))
                            unsafeSoftForThisFont.Add(b.Code);
                    }
                }
            }

            var effectivelySafe = globalSafeCandidates.Except(unsafeForThisFont).Except(zeroAreaForThisFont).ToList();
            var deficit = neededGlyphCodes - effectivelySafe.Count;

            report.Log($"  {font.BaseName}:");
            report.Log($"    Виключено через геометричний перетин у ЦЬОМУ шрифті: {unsafeForThisFont.Count}");
            report.Log($"    Виключено через нульову площу слоту (нема пікселів для перезапису): {zeroAreaForThisFont.Count(c => globalSafeCandidates.Contains(c))}");
            report.Log($"    Ефективно безпечних кодів: {effectivelySafe.Count}");
            report.Log(deficit <= 0
                ? $"    UA: Достатньо — надлишок {-deficit}."
                : $"    UA: НЕ ВИСТАЧАЄ {deficit} кодів для повного алфавіту в цьому шрифті!");

            // UA: Фінальний пул ЦЬОГО шрифту так, як його РЕАЛЬНО
            //     побачить GenerateLocalizedCoreCommand: базові + (якщо
            //     потрібно) м'які безпечні коди, мінус нульова площа, МІНУС
            //     фільтр "замалий донор" (порівняно з медіанною висотою A-Z
            //     САМЕ ЦЬОГО шрифту).
            // EN: This font's final pool exactly as
            //     GenerateLocalizedCoreCommand will REALLY see it: base +
            //     (if needed) soft safe codes, minus zero area, MINUS the
            //     "donor too small" filter (vs THIS font's own A-Z median
            //     height).
            var candidatePoolBaseOnly = globalSafeCandidates.Except(unsafeForThisFont).Except(zeroAreaForThisFont);
            var candidatePoolWithSoft = candidatePoolBaseOnly
                .Union(softCandidateCodes.Except(unsafeSoftForThisFont).Except(zeroAreaForThisFont));

            var allFontGlyphs = geometryByCode
                .Select(kv => new KnownGlyphGeometry(kv.Key, kv.Value.Width, kv.Value.Height))
                .ToList();

            HashSet<int> finalPoolBaseOnly;
            HashSet<int> finalPoolWithSoft;
            try
            {
                var referenceCapHeight = GlyphDonorMatcher.GetReferenceCapHeight(allFontGlyphs);

                List<DonorSlot> ToDonorSlots(IEnumerable<int> pool) => pool
                    .Where(c => geometryByCode.ContainsKey((ushort)c))
                    .Select(c => new DonorSlot((ushort)c, geometryByCode[(ushort)c].Width, geometryByCode[(ushort)c].Height))
                    .ToList();

                finalPoolBaseOnly = GlyphDonorMatcher.FilterOutTooSmall(ToDonorSlots(candidatePoolBaseOnly), referenceCapHeight)
                    .Select(d => (int)d.Code).ToHashSet();
                finalPoolWithSoft = GlyphDonorMatcher.FilterOutTooSmall(ToDonorSlots(candidatePoolWithSoft), referenceCapHeight)
                    .Select(d => (int)d.Code).ToHashSet();
            }
            catch (InvalidOperationException)
            {
                // UA: Немає жодної A-Z у цьому шрифті — еталонну висоту
                //     обчислити нема з чого, фільтр "замалий" пропускаємо
                //     (реальний запуск впав би з тим самим винятком — тут
                //     лише не зупиняємо звіт заради решти шрифтів).
                // EN: No A-Z in this font — no basis to compute a reference
                //     height, skip the "too small" filter (a real run would
                //     throw the same exception — here the report simply continues
                //     for the remaining fonts).
                finalPoolBaseOnly = candidatePoolBaseOnly.ToHashSet();
                finalPoolWithSoft = candidatePoolWithSoft.ToHashSet();
            }

            report.Log($"    UA: Фінальний пул БЕЗ м'яких донорів (з фільтром замалих): {finalPoolBaseOnly.Count}; З м'якими донорами: {finalPoolWithSoft.Count}");
            report.Log($"    EN: Final pool WITHOUT soft donors (with too-small filter): {finalPoolBaseOnly.Count}; WITH soft donors: {finalPoolWithSoft.Count}");
            report.Log();

            perFontFinalPools.Add((font.BaseName, finalPoolBaseOnly, finalPoolWithSoft));
        }

        // UA: ГОЛОВНЕ ПИТАННЯ: чи можна призначити "яка літера → який
        //     байт-код" ОДИН РАЗ на всю гру (а не окремо на кожен розмір
        //     шрифту)? Відповідь — так, ЯКЩО перетин фінальних пулів УСІХ
        //     розмірів шрифту цієї гри містить не менше neededGlyphCodes
        //     кодів: код у перетині гарантовано безпечний і має пікселі
        //     для перезапису в КОЖНОМУ розмірі шрифту, тож призначення
        //     літери на цей код можна зробити ОДИН раз і повторно
        //     використати для CyrillicFontInjector кожного шрифту (сам
        //     рендер/масштаб і надалі рахується окремо на кожен розмір —
        //     це не міняється, міняється лише ЯКИЙ байт-код обирається
        //     для літери).
        // EN: THE KEY QUESTION: can "which letter → which byte-code" be
        //     assigned ONCE for the whole game (not separately per font
        //     size)? Answer — yes, IF the intersection of the final pools
        //     of ALL font sizes in this game contains at least
        //     neededGlyphCodes codes: a code in the intersection is
        //     guaranteed safe and has pixels to overwrite in EVERY font
        //     size, so the letter-to-code assignment can be made ONCE and
        //     reused for every font's CyrillicFontInjector call (the actual
        //     render/scale is still computed separately per font size —
        //     that doesn't change, only WHICH byte-code is picked for the
        //     letter does).
        report.Log($"=== [{label}] Чи можливе ЄДИНЕ призначення 'літера→код' на ВСЮ гру? / Is a SINGLE game-wide 'letter→code' assignment possible? ===");

        if (perFontFinalPools.Count == 0)
        {
            report.Log("    UA: Жодного шрифту не оброблено — перевірку неможливо виконати.");
            report.Log("    EN: No fonts processed — check cannot be performed.");
            return;
        }

        var intersectionBaseOnly = perFontFinalPools[0].BaseOnly;
        foreach (var (_, codes, _) in perFontFinalPools.Skip(1))
            intersectionBaseOnly = intersectionBaseOnly.Intersect(codes).ToHashSet();

        report.Log($"    UA: Шрифтів перевірено: {perFontFinalPools.Count} ({string.Join(", ", perFontFinalPools.Select(f => f.FontName))})");
        report.Log($"    EN: Fonts checked: {perFontFinalPools.Count} ({string.Join(", ", perFontFinalPools.Select(f => f.FontName))})");
        report.Log($"    UA: Кодів, безпечних і НЕ замалих у КОЖНОМУ з цих шрифтів одночасно (БЕЗ м'яких донорів): {intersectionBaseOnly.Count}");
        report.Log($"    EN: Codes safe and NOT too small in EVERY one of these fonts simultaneously (WITHOUT soft donors): {intersectionBaseOnly.Count}");

        var intersection = intersectionBaseOnly;
        var usedSoftDonors = false;
        if (intersection.Count < neededGlyphCodes)
        {
            var intersectionWithSoft = perFontFinalPools[0].WithSoft;
            foreach (var (_, _, codes) in perFontFinalPools.Skip(1))
                intersectionWithSoft = intersectionWithSoft.Intersect(codes).ToHashSet();

            report.Log($"    UA: Без м'яких донорів не вистачає — перетин З м'якими донорами: {intersectionWithSoft.Count}");
            report.Log($"    EN: Without soft donors it's short — intersection WITH soft donors: {intersectionWithSoft.Count}");

            intersection = intersectionWithSoft;
            usedSoftDonors = true;
        }

        var sharedDeficit = neededGlyphCodes - intersection.Count;
        if (sharedDeficit <= 0)
        {
            report.Log($"    UA: ✓ ДОСТАТНЬО — надлишок {-sharedDeficit}{(usedSoftDonors ? " (з м'якими донорами)" : "")}. Єдина таблиця 'літера→код' на всю гру МОЖЛИВА без додаткових поступок.");
            report.Log($"    EN: ✓ ENOUGH — surplus {-sharedDeficit}{(usedSoftDonors ? " (with soft donors)" : "")}. A single game-wide 'letter→code' table IS POSSIBLE with no further compromises.");
        }
        else
        {
            report.Log($"    UA: ✗ НЕ ВИСТАЧАЄ {sharedDeficit} кодів для єдиної таблиці на всю гру (хоча окремо на кожен шрифт — вистачало), навіть з м'якими донорами.");
            report.Log($"    EN: ✗ SHORT BY {sharedDeficit} codes for a single game-wide table (even though each font alone had enough), even with soft donors.");
            report.Log($"    UA: Коди в перетині: {string.Join(", ", intersection.OrderBy(x => x).Select(x => $"0x{x:X2}"))}");
            report.Log($"    EN: Codes in the intersection: {string.Join(", ", intersection.OrderBy(x => x).Select(x => $"0x{x:X2}"))}");
        }
    }
}