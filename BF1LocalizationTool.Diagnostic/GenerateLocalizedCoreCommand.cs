// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateLocalizedCoreCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: НЕ ПЕРЕВІРКА — ця команда РЕАЛЬНО ПИШЕ файл (окрема категорія
//     меню, явно позначена, щоб не сплутати з рештою 30+ read-only
//     перевірок). Збирає ті самі дані, що вже багато разів перевірялись
//     окремо (безпечні донори — базові й м'які, геометрія, карти
//     зайнятості), і викликає CyrillicFontInjector
//     (BF1LocalizationTool.FontGenerator) для кожного переданого шрифту.
//
//     СПІЛЬНА таблиця кодів (підтверджено PerFontSafeDonorCommand —
//     перетин безпечних кодів усіх шрифтів дає 74 для BF1 і 91 для BF2,
//     потрібно 66): "яка літера → який байт-код" вирішується ОДИН РАЗ
//     для всієї гри, а не окремо для кожного шрифту — кожен виклик
//     CyrillicFontInjector отримує вже готове призначення, а не сам
//     викликає GlyphDonorMatcher.Assign на власному донорському пулі.
//     Це важливо, бо донорський пул різних розмірів шрифту різний: якби
//     призначення рахувалось окремо для кожного шрифту, той самий байт
//     міг би стати різними літерами в різних розмірах шрифту. Текст
//     (байти) спільний для всієї гри незалежно від того, яким шрифтом
//     його намалюють, тож єдине спільне призначення — архітектурна
//     вимога, а не дрібниця.
//
//     Тепер команда працює у ДВА ПРОХОДИ:
//       1. Аналізує КОЖЕН шрифт окремо (геометрія, перетини, нульова
//          площа, замалі донори) — без призначення кодів.
//       2. Перетинає результати УСІХ шрифтів → один спільний пул безпечних
//          кодів на всю гру → ОДНЕ призначення "літера→код"
//          (GlyphDonorMatcher.Assign, за геометрією РЕФЕРЕНСНОГО шрифту —
//          найбільшого наявного, найменше шуму округлення) → те саме
//          призначення передається в CyrillicFontInjector для КОЖНОГО
//          шрифту, який далі росте/рендерить/патчить, використовуючи
//          ВЛАСНУ геометрію (без відхилення в коректності росту — див.
//          GrowthResolver.cs).
//
//     БЕЗПЕКА:
//       - Оригінальний файл НІКОЛИ не перезаписується — результат
//         завжди йде в окрему теку "output" біля .exe, з тим самим
//         відносним шляхом, що й у реальній грі (щоб можна було
//         скопіювати "output" напряму поверх встановленої гри).
//       - Одразу після запису — round-trip перевірка (перечитати новий
//         файл, підтвердити, що FBOD кожного зачепленого шрифту
//         парситься без винятку і має ту саму кількість записів).
// EN: NOT A CHECK — this command ACTUALLY WRITES a file (a separate,
//     explicitly labeled menu category, so it isn't confused with the
//     other 30+ read-only checks). Gathers the same data already
//     verified separately many times (safe donors — base and soft,
//     geometry, occupancy maps), and calls CyrillicFontInjector
//     (BF1LocalizationTool.FontGenerator) for each supplied font.
//
//     SHARED code table (confirmed by PerFontSafeDonorCommand — the
//     intersection of all fonts' safe codes is 74 for BF1 and 91 for
//     BF2, 66 needed): "which letter → which byte-code" is decided ONCE
//     for the whole game, not separately per font — each
//     CyrillicFontInjector call receives an already-computed assignment
//     rather than running its own GlyphDonorMatcher.Assign on its own
//     donor pool. This matters because the donor pool differs per font
//     size: if the assignment were computed separately per font, the
//     same byte could become different letters in different font sizes.
//     Since the text (bytes) is shared across the whole game regardless
//     of which font renders it, one shared assignment is an
//     architectural requirement, not a minor detail.
//
//     The command now works in TWO PASSES:
//       1. Analyze EACH font separately (geometry, overlaps, zero area,
//          too-small donors) — without assigning codes yet.
//       2. Intersect ALL fonts' results → one shared safe-code pool for
//          the whole game → ONE "letter→code" assignment
//          (GlyphDonorMatcher.Assign, using the REFERENCE font's geometry
//          — the largest one available, least rounding noise) → that same
//          assignment is handed to CyrillicFontInjector for EVERY font,
//          which then grows/renders/patches using its OWN geometry (no
//          loss of growth correctness — see GrowthResolver.cs).
//
//     SAFETY:
//       - The original file is NEVER overwritten — the result always
//         goes into a separate "output" folder next to the .exe, with
//         the same relative path as in the real game (so "output" can
//         be copied straight over the installed game).
//       - Immediately after writing — a round-trip check (re-read the
//         new file, confirm each affected font's FBOD parses without an
//         exception and has the same record count).
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Localization;
using BF1LocalizationTool.FontGenerator.Injection;
using BF1LocalizationTool.FontGenerator.Matching;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateLocalizedCoreCommand
{
    private readonly record struct GlyphRect(ushort Code, int MinX, int MaxX, int MinY, int MaxY);

    // UA: Результат аналізу ОДНОГО шрифту (перед призначенням кодів) —
    //     усе, що потрібно і для обчислення СПІЛЬНОГО перетину, і (після
    //     нього) для власне ін'єкції ЦЬОГО шрифту.
    // EN: Analysis result for ONE font (before code assignment) —
    //     everything needed both for computing the SHARED intersection
    //     and (afterward) for actually injecting THIS font.
    private sealed record FontAnalysis(
        string BaseName,
        UcfbChunk FbodChunk,
        IReadOnlyList<FontGlyphRecord> OriginalRecords,
        Dictionary<ushort, GlyphRectBounds> RectByCode,
        Dictionary<ushort, (int Width, int Height)> GeometryByCode,
        Dictionary<byte, PageInjectionContext> PagesByIndex,
        // UA: ДВА окремих фінальних пули замість одного — БЕЗ м'яких
        //     донорів (нульовий ризик для інших мов) і З ними (ризик
        //     для мов, які реально використовують цей код). Рішення "чи
        //     додавати м'які донори" приймається ПІСЛЯ повної фільтрації
        //     й перетину всіх шрифтів (а не на основі грубої оцінки
        //     кількості кандидатів ДО фільтрації перетинів/геометрії/
        //     висоти, яка ненадійна — грубий підрахунок не відображає
        //     РЕАЛЬНИЙ розмір пулу після фільтрації) — м'які донори
        //     підключаються, лише якщо БЕЗ них справді не вистачає.
        // EN: TWO separate final pools instead of one — WITHOUT soft
        //     donors (zero risk to other languages) and WITH them (risk
        //     to languages that actually use that code). The decision
        //     "whether to add soft donors" is made AFTER full filtering
        //     and intersection across all fonts (not from a crude
        //     candidate count taken BEFORE overlap/geometry/height
        //     filtering, which is unreliable — a crude count doesn't
        //     reflect the REAL pool size after filtering) — soft donors
        //     are only pulled in if the pool is genuinely insufficient
        //     without them.
        HashSet<int> FinalSafePoolBaseOnly,
        HashSet<int> FinalSafePoolWithSoft,
        // UA: Потрібен для GlyphDonorMatcher.Assign — переводить висоту
        //     донора референсного шрифту в частку, порівнянну з
        //     відносною висотою цільової літери (виправлення "стрибучих"
        //     літер — див. заголовок GlyphDonorMatcher.cs).
        // EN: Needed by GlyphDonorMatcher.Assign — converts the reference
        //     font's donor height into a fraction comparable to the
        //     target letter's relative height ("jumping" letters fix —
        //     see GlyphDonorMatcher.cs header).
        int ReferenceCapHeight);

    public static async Task Run(
        DiagnosticReport report, string inputFilePath, string label,
        string fontFamilyName, int neededGlyphCodes, IReadOnlyList<string> fontBaseNamesToInject)
    {
        var root = UcfbReader.ReadFile(inputFilePath);
        var summary = await SoftDonorAnalysis.BuildSummaryAsync(inputFilePath);

        // UA: ВСТАНОВЛЕНО (реальний скріншот BF1 — пункт меню "Exit to
        //     Windows"): гра показала "WINDOГs" замість "Windows" — код
        //     'w' (0x77) насправді використовується, хоча НЕ зустрічається
        //     в жодному з 6 мов Locl-текстів core.lvl (десь-інде — жорстко
        //     вшитий рядок, інший .lvl-файл — джерело не з'ясовувалось,
        //     бо для рішення це не має значення). Це показує: "не знайдено
        //     в Locl-тексті" НЕ ДОВОДИТЬ "гра ніколи це не покаже" —
        //     сканування Locl має сліпу зону на будь-який текст поза ним.
        //
        //     Тому ДРУКОВНІ ASCII-символи (0x20-0x7E — літери, цифри,
        //     пунктуація: усе, що ТЕОРЕТИЧНО могло б зустрітись як
        //     видимий текст ДЕСЬ у грі) ЗАВЖДИ вважаються зайнятими,
        //     незалежно від результату сканування Locl — англійський
        //     текст (латиниця) принципово не чіпається донорським
        //     підбором. Лишаються доступними лише НЕДРУКОВНІ керівні коди
        //     (0x00-0x1F, 0x7F) — їх не може містити жоден легітимний
        //     рядок тексту, і коди 128-255, де сканування Locl 6 мов
        //     лишається єдиним і достатнім джерелом істини (перевірено
        //     багаторазово в проєкті без подібних збоїв).
        // EN: CONFIRMED (real BF1 screenshot — "Exit to Windows" menu
        //     item): the game showed "WINDOГs" instead of "Windows" —
        //     'w' (0x77) IS actually used, though it never appears in any
        //     of the 6 languages' Locl text in core.lvl (somewhere else —
        //     a hardcoded string, another .lvl file — the exact source
        //     wasn't chased down, since it doesn't matter for the fix).
        //     This shows: "not found in Locl text" does NOT prove "the
        //     game will never display this" — scanning Locl has a blind
        //     spot for any text outside it.
        //
        //     So PRINTABLE ASCII (0x20-0x7E — letters, digits,
        //     punctuation: anything that could THEORETICALLY appear as
        //     visible text SOMEWHERE in the game) is ALWAYS treated as
        //     used, regardless of the Locl scan result — English text
        //     (Latin script) is deliberately never touched by donor
        //     selection. Only NON-PRINTABLE control codes (0x00-0x1F,
        //     0x7F) remain eligible — no legitimate text string can
        //     contain them — and codes 128-255, where scanning the 6
        //     languages' Locl text remains the sole and sufficient
        //     source of truth (already verified repeatedly in the
        //     project, with no similar failures).
        bool IsPrintableAscii(ushort code) => code is >= 0x20 and <= 0x7E;
        var usedByAnyLanguage = summary.Languages.SelectMany(l => l.CodeCounts.Keys).ToHashSet();
        bool IsUsed(ushort code) => IsPrintableAscii(code) || usedByAnyLanguage.Contains(code);

        var englishUsed = summary.Languages
            .Where(l => l.Language.Contains("english", StringComparison.OrdinalIgnoreCase))
            .SelectMany(l => l.CodeCounts.Keys)
            .ToHashSet();

        var baseSafeCandidates = Enumerable.Range(0, 256).Where(x => !IsUsed((ushort)x)).ToHashSet();

        // UA: М'які кандидати рахуються ЗАВЖДИ (дешева операція), а
        //     РІШЕННЯ використовувати їх чи ні приймається нижче, ПІСЛЯ
        //     реальної фільтрації й перетину.
        // EN: Soft candidates are ALWAYS computed (cheap), and the
        //     DECISION to use them or not is made below, AFTER real
        //     filtering and intersection.
        var softCandidateCodes = SoftDonorAnalysis.ComputeSoftCandidates(summary).Select(c => c.Code).ToHashSet();

        var fonts = FontChunkLocator.FindAll(root);

        // =====================================================================
        // UA: ПРОХІД 1 — аналіз кожного шрифту окремо, без призначення
        //     кодів літерам.
        // EN: PASS 1 — analyze each font separately, without assigning
        //     codes to letters yet.
        // =====================================================================
        var analyses = new List<FontAnalysis>();

        foreach (var fontBaseName in fontBaseNamesToInject)
        {
            var font = fonts.FirstOrDefault(f => f.BaseName == fontBaseName);
            if (font is null) { report.Log($"UA: [{label}] Шрифт '{fontBaseName}' не знайдено — пропущено."); continue; }

            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) { report.Log($"UA: [{label}] FBOD у '{fontBaseName}' не знайдено — пропущено."); continue; }

            var originalRecords = FontGlyphTable.Parse(fbod.RawData);
            var unsafeBase = new HashSet<int>();
            var unsafeSoft = new HashSet<int>();
            var zeroArea = new HashSet<int>();
            var geometryByCode = new Dictionary<ushort, (int Width, int Height)>();
            var rectByCode = new Dictionary<ushort, GlyphRectBounds>();
            var pagesByIndex = new Dictionary<byte, PageInjectionContext>();

            foreach (var pageGroup in originalRecords.GroupBy(g => g.PageIndex))
            {
                if (pageGroup.Key >= font.TexturePages.Count) continue;

                var texPixels = FontTexturePixelReader.ReadMip0(font.TexturePages[pageGroup.Key].Chunk);
                var texWidth = texPixels.Width;
                var texHeight = texPixels.Height;

                var rects = pageGroup.Select(g =>
                {
                    var x0 = (int)Math.Round(g.U0 * texWidth);
                    var x1 = (int)Math.Round(g.U1 * texWidth);
                    var y0 = (int)Math.Round(g.V0 * texHeight);
                    var y1 = (int)Math.Round(g.V1 * texHeight);
                    return new GlyphRect(g.Code, Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1), Math.Max(y0, y1));
                }).ToList();

                foreach (var r in rects)
                {
                    geometryByCode[r.Code] = (r.MaxX - r.MinX, r.MaxY - r.MinY);
                    rectByCode[r.Code] = new GlyphRectBounds(r.MinX, r.MinY, r.MaxX, r.MaxY);
                    if (r.MaxX - r.MinX <= 0 || r.MaxY - r.MinY <= 0)
                        zeroArea.Add(r.Code);
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
                            unsafeBase.Add(aUsed ? b.Code : a.Code);

                        if (softCandidateCodes.Contains(a.Code) && englishUsed.Contains(b.Code))
                            unsafeSoft.Add(a.Code);
                        if (softCandidateCodes.Contains(b.Code) && englishUsed.Contains(a.Code))
                            unsafeSoft.Add(b.Code);
                    }
                }

                var occupied = new bool[texWidth, texHeight];
                foreach (var r in rects)
                {
                    if (r.MaxX - r.MinX <= 0 || r.MaxY - r.MinY <= 0) continue;
                    for (var x = r.MinX; x < r.MaxX; x++)
                        for (var y = r.MinY; y < r.MaxY; y++)
                            occupied[x, y] = true;
                }

                pagesByIndex[pageGroup.Key] = new PageInjectionContext(
                    texPixels.BodyChunk, texWidth, texHeight, occupied);
            }

            // UA: ДВА пули — базовий (нульовий ризик) і базовий+м'який
            //     (ризик для мов, що використовують ці коди). Обидва
            //     будуються ЗАВЖДИ; рішення, який саме йде в перетин,
            //     приймається нижче, ПІСЛЯ перетину всіх шрифтів.
            // EN: TWO pools — base-only (zero risk) and base+soft (risk
            //     to languages using those codes). Both are ALWAYS built;
            //     the decision on which one to actually use is made
            //     below, AFTER intersecting all fonts.
            var candidatePoolBaseOnly = baseSafeCandidates.Except(unsafeBase).Except(zeroArea);
            var candidatePoolWithSoft = candidatePoolBaseOnly
                .Union(softCandidateCodes.Except(unsafeSoft).Except(zeroArea));

            var allFontGlyphs = geometryByCode
                .Select(kv => new KnownGlyphGeometry(kv.Key, kv.Value.Width, kv.Value.Height))
                .ToList();

            HashSet<int> finalSafePoolBaseOnly;
            HashSet<int> finalSafePoolWithSoft;
            int referenceCapHeight;
            try
            {
                referenceCapHeight = GlyphDonorMatcher.GetReferenceCapHeight(allFontGlyphs);

                List<DonorSlot> ToDonorSlots(IEnumerable<int> pool) => pool
                    .Where(c => geometryByCode.ContainsKey((ushort)c))
                    .Select(c => new DonorSlot((ushort)c, geometryByCode[(ushort)c].Width, geometryByCode[(ushort)c].Height))
                    .ToList();

                finalSafePoolBaseOnly = GlyphDonorMatcher.FilterOutTooSmall(ToDonorSlots(candidatePoolBaseOnly), referenceCapHeight)
                    .Select(d => (int)d.Code).ToHashSet();
                finalSafePoolWithSoft = GlyphDonorMatcher.FilterOutTooSmall(ToDonorSlots(candidatePoolWithSoft), referenceCapHeight)
                    .Select(d => (int)d.Code).ToHashSet();
            }
            catch (InvalidOperationException)
            {
                report.Log($"UA: [{label}] {fontBaseName}: немає жодної A-Z для еталонної висоти — пропущено.");
                continue;
            }

            analyses.Add(new FontAnalysis(
                fontBaseName, fbod, originalRecords, rectByCode, geometryByCode, pagesByIndex,
                finalSafePoolBaseOnly, finalSafePoolWithSoft, referenceCapHeight));
        }

        if (analyses.Count == 0)
        {
            report.Log($"UA: [{label}] Жодного шрифту не проаналізовано — файл НЕ записано.");
            return;
        }

        // =====================================================================
        // UA: ПЕРЕТИН — спочатку СПРОБА без м'яких донорів (нульовий
        //     ризик для інших мов). Лише якщо цього перетину РЕАЛЬНО не
        //     вистачає — рахується ДРУГИЙ перетин з м'якими донорами.
        //     Рішення "чи потрібні м'які донори" базується на РЕАЛЬНОМУ
        //     перетині ПІСЛЯ фільтрації, а не на грубому підрахунку ДО
        //     неї — грубий підрахунок ненадійний (напр. на BF2, після
        //     розширення пулу ASCII, груба оцінка кандидатів піднімається
        //     вище 66, тоді як РЕАЛЬНИЙ перетин без м'яких донорів може
        //     виявитись лише 57, а з ними — 91).
        // EN: INTERSECTION — first TRY without soft donors (zero risk to
        //     other languages). Only if that intersection is genuinely
        //     insufficient is a SECOND intersection computed, with soft
        //     donors included. The "are soft donors needed" decision is
        //     based on the REAL intersection AFTER filtering, not a
        //     crude count taken BEFORE it — a crude count is unreliable
        //     (e.g. on BF2, after widening the ASCII pool, the crude
        //     candidate estimate rises above 66, while the REAL
        //     intersection without soft donors can turn out to be only
        //     57, and 91 with them).
        // =====================================================================
        var sharedPoolBaseOnly = analyses[0].FinalSafePoolBaseOnly;
        foreach (var a in analyses.Skip(1))
            sharedPoolBaseOnly = sharedPoolBaseOnly.Intersect(a.FinalSafePoolBaseOnly).ToHashSet();

        HashSet<int> sharedPool;
        bool usedSoftDonors;
        if (sharedPoolBaseOnly.Count >= CyrillicAlphabet.AllLetters.Count)
        {
            sharedPool = sharedPoolBaseOnly;
            usedSoftDonors = false;
        }
        else
        {
            var sharedPoolWithSoft = analyses[0].FinalSafePoolWithSoft;
            foreach (var a in analyses.Skip(1))
                sharedPoolWithSoft = sharedPoolWithSoft.Intersect(a.FinalSafePoolWithSoft).ToHashSet();

            report.Log($"UA: [{label}] Без м'яких донорів перетин дає лише {sharedPoolBaseOnly.Count} " +
                       $"(потрібно {CyrillicAlphabet.AllLetters.Count}) — підключаю м'які донори.");
            report.Log($"EN: [{label}] Without soft donors the intersection yields only {sharedPoolBaseOnly.Count} " +
                       $"(need {CyrillicAlphabet.AllLetters.Count}) — pulling in soft donors.");

            sharedPool = sharedPoolWithSoft;
            usedSoftDonors = true;
        }

        if (sharedPool.Count < CyrillicAlphabet.AllLetters.Count)
        {
            report.Log($"UA: [{label}] КРИТИЧНО — перетин безпечних кодів усіх шрифтів дає лише {sharedPool.Count} " +
                       $"(навіть з м'якими донорами), потрібно {CyrillicAlphabet.AllLetters.Count}. " +
                       "Єдина таблиця неможлива без поступок — файл НЕ записано.");
            report.Log($"EN: [{label}] CRITICAL — the intersection of all fonts' safe codes yields only {sharedPool.Count} " +
                       $"(even with soft donors), need {CyrillicAlphabet.AllLetters.Count}. " +
                       "A single shared table is impossible without compromises — file NOT written.");
            return;
        }

        if (usedSoftDonors)
        {
            report.Log($"UA: [{label}] УВАГА — таблиця використовує м'які донори (коди, які ВИКОРИСТОВУЄ якась " +
                       "неанглійська мова гри). Перевір ці мови в грі після встановлення — символ, що раніше " +
                       "показувався там, тепер стане кириличною літерою.");
            report.Log($"EN: [{label}] WARNING — the table uses soft donors (codes some non-English game " +
                       "language DOES use). Check those languages in-game after installing — a character that " +
                       "used to display there will now become a Cyrillic letter.");
        }

        // =====================================================================
        // UA: ПРИЗНАЧЕННЯ — ОДИН РАЗ на всю гру, з геометрією РЕФЕРЕНСНОГО
        //     шрифту (gamefont_large, якщо є серед оброблюваних — найбільша
        //     текстура, найменше шуму округлення в пропорціях; інакше
        //     перший оброблений шрифт). Той самий Character→DonorCode
        //     повторно використовується для КОЖНОГО шрифту нижче — росте й
        //     рендериться він і надалі окремо на кожен розмір (це не
        //     міняється, GrowthResolver рахує геометрію заново з
        //     ВЛАСНОГО прямокутника кожного шрифту), змінюється лише те,
        //     що БАЙТ-КОД для літери тепер один на всю гру.
        // EN: ASSIGNMENT — ONCE for the whole game, using the REFERENCE
        //     font's geometry (gamefont_large if present among the
        //     processed fonts — the largest texture, least rounding noise
        //     in aspect ratios; otherwise the first processed font). The
        //     same Character→DonorCode mapping is reused for EVERY font
        //     below — growth/rendering is still computed separately per
        //     font size (unchanged, GrowthResolver recomputes geometry
        //     fresh from EACH font's OWN rectangle), only the BYTE-CODE for
        //     a letter is now one per game.
        // =====================================================================
        var referenceAnalysis = analyses.FirstOrDefault(a => a.BaseName == "gamefont_large") ?? analyses[0];

        var referenceDonors = sharedPool
            .Select(c => new DonorSlot(
                (ushort)c,
                referenceAnalysis.GeometryByCode[(ushort)c].Width,
                referenceAnalysis.GeometryByCode[(ushort)c].Height))
            .ToList();

        var sharedAssignments = GlyphDonorMatcher.Assign(
            CyrillicAlphabet.AllLetters, referenceDonors, fontFamilyName, referenceAnalysis.ReferenceCapHeight);

        report.Log($"UA: [{label}] Спільна таблиця 'літера→код' побудована на основі '{referenceAnalysis.BaseName}' " +
                   $"({sharedPool.Count} кодів у перетині {analyses.Count} шрифтів, потрібно {CyrillicAlphabet.AllLetters.Count}).");
        report.Log($"EN: [{label}] Shared 'letter→code' table built from '{referenceAnalysis.BaseName}' " +
                   $"({sharedPool.Count} codes in the intersection of {analyses.Count} fonts, {CyrillicAlphabet.AllLetters.Count} needed).");

        // UA: Деталі по КОЖНІЙ літері — щоб бачити конкретні числа для
        //     "підозрілих" літер (напр. 'в'/'ж' на скріншотах BF1
        //     виглядають як крихітні фрагменти), а не гадати. HeightFrac
        //     — відносна висота (ціль: до еталонної великої кириличної
        //     літери; донор: до referenceCapHeight шрифту) — саме цей
        //     доданок мав виправити "стрибучі" літери.
        // EN: Per-letter details — to see concrete numbers for
        //     "suspicious" letters (e.g. 'в'/'ж' in BF1 screenshots look
        //     like tiny fragments), instead of guessing. HeightFrac —
        //     relative height (target: to the reference capital Cyrillic
        //     letter; donor: to the font's referenceCapHeight) — this is
        //     the term meant to fix "jumping" letters.
        report.Log($"UA: [{label}] Деталі призначення (66 літер):");
        report.Log($"EN: [{label}] Assignment details (66 letters):");
        report.Log("    Літера  Донор  Розмір(WxH)  Пропорція ціль/донор  HeightFrac ціль/донор");
        foreach (var a in sharedAssignments.OrderBy(a => a.Character))
        {
            report.Log($"    {a.Character}       0x{a.DonorCode:X2}   {a.CanvasWidth,3}x{a.CanvasHeight,-3}      " +
                       $"{a.TargetAspectRatio,5:F2} / {a.DonorAspectRatio,-5:F2}         " +
                       $"{a.TargetHeightFraction,5:P0} / {a.DonorHeightFraction,-5:P0}");
        }
        report.Log();

        // =====================================================================
        // UA: ПРОХІД 2 — власне ін'єкція. Той самий sharedAssignments для
        //     кожного шрифту; donorRectsByCode береться з ВЛАСНОЇ геометрії
        //     ЦЬОГО шрифту (rectByCode), не з референсного.
        // EN: PASS 2 — the actual injection. The SAME sharedAssignments for
        //     every font; donorRectsByCode comes from THIS font's OWN
        //     geometry (rectByCode), not the reference font's.
        // =====================================================================
        var allReplacements = new Dictionary<long, byte[]>();
        var injectedFontNames = new List<string>();

        foreach (var analysis in analyses)
        {
            var donorRectsByCode = sharedAssignments.ToDictionary(
                a => a.DonorCode,
                a => analysis.RectByCode[a.DonorCode]);

            var result = CyrillicFontInjector.InjectFont(
                analysis.FbodChunk, analysis.OriginalRecords, sharedAssignments,
                donorRectsByCode, analysis.PagesByIndex, fontFamilyName);

            report.Log($"UA: [{label}] {analysis.BaseName}: додано {result.LettersInjected} літер.");
            injectedFontNames.Add(analysis.BaseName);

            // UA: Фактичний розмір ПІСЛЯ росту (GrowthResolver): якщо
            //     PostAspect/TargetAspect близькі, ріст компенсував
            //     поганий донор; якщо PostWxH ≈ PreWxH (майже без змін),
            //     навколо донора немає вільного місця для росту.
            // EN: The actual size AFTER growth (GrowthResolver): when
            //     PostAspect/TargetAspect are close, growth compensated
            //     for a bad donor; when PostWxH ≈ PreWxH (barely changed),
            //     there is no free space around that donor to grow into.
            report.Log($"UA: [{label}] {analysis.BaseName}: розмір ДО/ПІСЛЯ росту (66 літер):");
            report.Log($"EN: [{label}] {analysis.BaseName}: size BEFORE/AFTER growth (66 letters):");
            report.Log("    Літера  Донор  До(WxH)   Після(WxH)  Пропорція ціль/після");
            var assignmentByChar = sharedAssignments.ToDictionary(a => a.Character);
            foreach (var p in result.Placements.OrderBy(p => p.Character))
            {
                var preRect = analysis.RectByCode[p.DonorCode];
                var target = assignmentByChar[p.Character];
                var postAspect = p.Rect.Height == 0 ? 0.0 : (double)p.Rect.Width / p.Rect.Height;
                var grew = p.Rect.Width != preRect.Width || p.Rect.Height != preRect.Height;
                report.Log($"    {p.Character}       0x{p.DonorCode:X2}   {preRect.Width,3}x{preRect.Height,-3}   " +
                           $"{p.Rect.Width,3}x{p.Rect.Height,-3}{(grew ? "*" : " ")}      " +
                           $"{target.TargetAspectRatio,5:F2} / {postAspect,-5:F2}");
            }
            report.Log("    (* = ріст змінив розмір слоту відносно донора до росту / growth changed the slot size vs. the pre-growth donor)");
            report.Log();

            // UA: МЕТРИЧНА МОДЕЛЬ (GlyphMetricModel) — замість старої
            //     таблиці core/margin. Показує для КОЖНОЇ з 66 літер точні
            //     значення, що пішли у FBOD: природні ascent/descent
            //     (пробний растр), масштаб природа→гра, цільовий розмір
            //     бокса й самі Bearing/CellHeight. Перевірка інваріанта
            //     Bearing+BoxH=CellHeight робиться прямо в колонці "OK?".
            //     ФактБокс — реальний розмір слоту після ResolveMetricSlots
            //     (може бути менший за ціль, якщо донор замалий і не було
            //     куди рости, напр. 'ж'): якщо ФактБокс.H < BoxH цілі —
            //     гра трохи домасштабує по вертикалі (єдиний край, де
            //     масштаб ≠ 1).
            // EN: METRIC MODEL (GlyphMetricModel) — replaces the old
            //     core/margin table. Shows, for EACH of the 66 letters, the
            //     exact values that went into FBOD: natural ascent/descent
            //     (probe raster), the natural→game scale, the target box
            //     size and the Bearing/CellHeight themselves. The invariant
            //     Bearing+BoxH=CellHeight is checked right in the "OK?"
            //     column. ФактБокс — the actual slot size after
            //     ResolveMetricSlots (may be smaller than target if the
            //     donor is too small and had nowhere to grow, e.g. 'ж'): if
            //     ФактБокс.H < the target BoxH, the game upscales slightly
            //     vertically (the only edge where scale ≠ 1).
            var reference = result.MetricReference;
            var debugLetters = CyrillicAlphabet.AllLetters.Where(c => result.MetricsByChar.ContainsKey(c) &&
                result.Placements.Any(p => p.Character == c)).ToList();
            if (debugLetters.Count > 0)
            {
                report.Log($"UA: [{label}] {analysis.BaseName}: МЕТРИЧНА модель, УСІ 66 літер " +
                           $"(baselineOffset={reference.BaselineOffset}, capHeightGame={reference.CapHeightGame}, natCapAscent={reference.NaturalCapAscent:F1}):");
                report.Log($"EN: [{label}] {analysis.BaseName}: METRIC model, ALL 66 letters " +
                           $"(baselineOffset={reference.BaselineOffset}, capHeightGame={reference.CapHeightGame}, natCapAscent={reference.NaturalCapAscent:F1}):");
                report.Log("    Літера  natAsc/natDesc  scale   Ціль(WxH)  ФактБокс(WxH)  Bearing  CellHeight  OK?(Bear+BoxH=Cell)");
                foreach (var ch in debugLetters)
                {
                    var m = result.MetricsByChar[ch];
                    var placement = result.Placements.First(p => p.Character == ch);
                    var invariantOk = m.Bearing + m.BoxHeight == m.CellHeight;
                    report.Log($"    {ch}       {m.NaturalAscent,3}/{m.NaturalDescent,-3}         " +
                               $"{m.Scale,5:F3}   {m.BoxWidth,3}x{m.BoxHeight,-3}     " +
                               $"{placement.Rect.Width,3}x{placement.Rect.Height,-3}       " +
                               $"{m.Bearing,4}     {m.CellHeight,4}       {(invariantOk ? "OK" : "!!")}");
                }
                report.Log();
            }

            // UA: Детальне логування всіх параметрів. Показує ПОЛЯ FBOD,
            //     які код НІКОЛИ не змінює — вони завжди КОПІЮЮТЬСЯ з
            //     донора (GlyphAtlasPatcher.BuildReplacements,
            //     задокументовано там же: "PageIndex/Bearing/CellHeight/
            //     ReservedByte4 — усе решта НЕ вказано тут, тож `with`
            //     копіює їх з donor без змін"). Якщо ГРА використовує
            //     CellHeight/Bearing для ВЕРТИКАЛЬНОГО позиціонування чи
            //     масштабування гліфа під час рендеру (а не лише як
            //     метадані) — тоді нова кирилична літера успадковує це
            //     значення від НЕПОВ'ЯЗАНОЇ англійської літери/символу
            //     донора — і жодне вдосконалення пікселів у PNG цього не
            //     виправить, бо рушій може перемасштабовувати/зсувати
            //     квад ПОВЕРХ уже намальованих пікселів, за цим чужим
            //     числом. FontGlyphRecord.cs перевіряє кореляцію
            //     Bearing/XAdvance з вертикальним позиціонуванням і
            //     кореляцію CellHeight з розміром чорнила (53-76%
            //     відхилення), але не перевіряє CellHeight/Bearing саме як
            //     "джерело стрибання". Мета цього логу — дати сирі дані
            //     для порівняння: чи в донорів "стрибучих" (за
            //     скріншотом) літер CellHeight/Bearing систематично інші,
            //     ніж у "спокійних".
            // EN: Detailed logging of all parameters. Shows the FBOD
            //     fields this code NEVER changes — they're always COPIED
            //     from the donor (GlyphAtlasPatcher.BuildReplacements,
            //     documented there: "PageIndex/Bearing/CellHeight/
            //     ReservedByte4 — everything else is NOT specified here,
            //     so `with` copies them from donor unchanged"). If the
            //     GAME uses CellHeight/Bearing for VERTICAL positioning or
            //     scaling of the glyph AT RENDER TIME (not just as
            //     metadata) — then the new Cyrillic letter inherits this
            //     value from the UNRELATED English letter/symbol donor —
            //     and no amount of PNG pixel refinement would fix that,
            //     since the engine could be rescaling/offsetting the quad
            //     ON TOP OF already-correct pixels, by this unrelated
            //     inherited number. FontGlyphRecord.cs checks the
            //     correlation of Bearing/XAdvance with vertical
            //     positioning and the correlation of CellHeight with ink
            //     size (53-76% deviation), but does not check
            //     CellHeight/Bearing specifically as a "jumping" source.
            //     This log's purpose is to give raw data for comparison:
            //     do the donors of "jumping" letters (per the screenshot)
            //     have systematically different CellHeight/Bearing than
            //     the "calm" ones.
            report.Log($"UA: [{label}] {analysis.BaseName}: поля FBOD, УСПАДКОВАНІ від донора без змін (для перевірки — чи саме вони спричиняють стрибання):");
            report.Log($"EN: [{label}] {analysis.BaseName}: FBOD fields INHERITED from the donor unchanged (to check whether THEY cause the jumping):");
            report.Log("    Літера  Донор  Сторінка  CellHeight  Bearing  ReservedByte4  InkWidth(нов./new)  XAdvance(нов./new)");
            var originalByCode = analysis.OriginalRecords.ToDictionary(r => r.Code);
            foreach (var p in result.Placements.OrderBy(p => p.Character))
            {
                if (!originalByCode.TryGetValue(p.DonorCode, out var donorRecord))
                    continue;

                report.Log($"    {p.Character}       0x{p.DonorCode:X2}   {donorRecord.PageIndex,-9}{donorRecord.CellHeight,-12}{donorRecord.Bearing,-9}{donorRecord.ReservedByte4,-15}");
            }
            report.Log();

            foreach (var kv in result.Replacements)
                allReplacements[kv.Key] = kv.Value;
        }

        if (injectedFontNames.Count == 0)
        {
            report.Log($"UA: [{label}] Жодного шрифту не оброблено — файл НЕ записано.");
            return;
        }

        // UA: Окрема тека "output" біля .exe (не поруч із оригіналом —
        //     щоб не смітити в теці гри) з ТОЧНО тим самим відносним
        //     шляхом, що й у самій грі — досить скопіювати вміст "output"
        //     напряму поверх встановленої гри. Файл називається просто
        //     "core.lvl" (не з часовою міткою) саме для цього — щоб
        //     збігався з реальним іменем; КОЖЕН запуск перезаписує
        //     попередній результат генерації в цій теці (це не оригінал
        //     гри — той не чіпається ніколи).
        // EN: A separate "output" folder next to the .exe (not next to
        //     the original — to avoid cluttering the game folder) with
        //     EXACTLY the same relative path as in the actual game —
        //     copying the "output" folder's contents straight over the
        //     installed game is enough. The file is simply named
        //     "core.lvl" (no timestamp) for exactly this reason — so it
        //     matches the real file name; EACH run overwrites the
        //     previous generation result in this folder (this is not the
        //     game's original — that is never touched).
        var relativeGamePath = label == "BF2"
            ? Path.Combine("Star Wars Battlefront II Classic", "GameData", "data", "_lvl_pc")
            : Path.Combine("Star Wars Battlefront (Classic 2004)", "GameData", "Data", "_LVL_PC");

        var outputDir = Path.Combine(AppContext.BaseDirectory, "output", relativeGamePath);
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "core.lvl");

        var newBytes = UcfbWriter.WriteFile(root, allReplacements);
        File.WriteAllBytes(outputPath, newBytes);

        report.Log();
        report.Log($"UA: [{label}] Оригінал НЕ змінено. Новий файл (перезаписує попередній прогін): {outputPath}");
        report.Log($"EN: [{label}] Original untouched. New file (overwrites the previous run): {outputPath}");
        report.Log($"UA: [{label}] Скопіюй теку \"output\" напряму поверх встановленої гри — шлях уже збігається.");
        report.Log($"EN: [{label}] Copy the \"output\" folder straight over the installed game — the path already matches.");

        // UA: Файл-супутник поруч із core.lvl — та сама sharedAssignments,
        //     якою РЕАЛЬНО патчились шрифти вище, тож GUI (кодек кирилиці)
        //     ГАРАНТОВАНО побачить точно ті самі байт-коди, що фізично є
        //     в атласах цього конкретного файлу. Рішення: файл-супутник,
        //     а не окремий чанк у core.lvl і не перерахунок логіки в Core.
        // EN: Sidecar file next to core.lvl — the exact sharedAssignments
        //     that ACTUALLY patched the fonts above, so the GUI (Cyrillic
        //     codec) is GUARANTEED to see the exact same byte-codes
        //     physically present in this specific file's atlases.
        //     Decision: a sidecar file, not a separate core.lvl chunk and
        //     not re-deriving the logic in Core.
        var codeTable = new CyrillicCodeTable
        {
            Game = label,
            ReferenceFont = referenceAnalysis.BaseName,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Letters = sharedAssignments
                .Select(a => new CyrillicCodeTableEntry(a.Character.ToString(), (byte)a.DonorCode))
                .ToList(),
        };
        var codeTablePath = Path.Combine(outputDir, CyrillicCodeTable.DefaultFileName);
        codeTable.Save(codeTablePath);

        report.Log($"UA: [{label}] Таблиця 'літера→код' для GUI збережена поруч: {codeTablePath}");
        report.Log($"EN: [{label}] 'Letter→code' table for the GUI saved alongside: {codeTablePath}");

        // UA: Round-trip перевірка одразу після запису — перечитуємо
        //     НОВИЙ файл і підтверджуємо, що FBOD кожного зачепленого
        //     шрифту парситься без винятку й має ту саму кількість
        //     записів (кількість глифів НЕ мала змінитись — патчились
        //     лише існуючі коди, жоден не додавався й не видалявся).
        // EN: Round-trip check right after writing — re-read the NEW
        //     file and confirm each affected font's FBOD parses without
        //     an exception and has the same record count (glyph count
        //     should NOT have changed — only existing codes were
        //     patched, none added or removed).
        report.Log();
        report.Log($"UA: [{label}] Round-trip перевірка нового файлу...");
        try
        {
            var rereadRoot = UcfbReader.ReadFile(outputPath);
            var rereadFonts = FontChunkLocator.FindAll(rereadRoot);

            foreach (var fontBaseName in injectedFontNames)
            {
                var rereadFont = rereadFonts.FirstOrDefault(f => f.BaseName == fontBaseName);
                if (rereadFont is null)
                {
                    report.Log($"UA: [{label}] {fontBaseName}: КРИТИЧНО — шрифт не знайдено після перезапису.");
                    continue;
                }

                var rereadFbod = UcfbReader.FindFirst(rereadFont.Chunk, "FBOD");
                if (rereadFbod is null)
                {
                    report.Log($"UA: [{label}] {fontBaseName}: КРИТИЧНО — FBOD не знайдено після перезапису.");
                    continue;
                }

                var rereadRecords = FontGlyphTable.Parse(rereadFbod.RawData);
                report.Log($"UA: [{label}] {fontBaseName}: перечитано успішно, {rereadRecords.Count} записів у FBOD.");
            }

            report.Log($"UA: [{label}] Round-trip перевірка ПРОЙДЕНА — файл коректно перечитується.");
        }
        catch (Exception ex)
        {
            report.Log($"UA: [{label}] КРИТИЧНО — round-trip перевірка ПРОВАЛИЛАСЬ: {ex.Message}");
            report.Log($"EN: [{label}] CRITICAL — round-trip check FAILED: {ex.Message}");
        }
    }
}
