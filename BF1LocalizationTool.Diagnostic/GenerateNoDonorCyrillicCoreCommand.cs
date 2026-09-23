// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateNoDonorCyrillicCoreCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ГЕНЕРАТОР (production, входить у фінальний патч) / GENERATOR (production, part of the final patch)
// =============================================================================
// UA: ПРОДАКШН-ГЕНЕРАЦІЯ кириличного core.lvl БЕЗ донорів — доведений
//     робочий підхід (перевірено в грі: гра рендерить прямі
//     Unicode-коди поза оригінальним набором чисто, без донорських
//     нерівностей). Замінює донорський GenerateLocalizedCore.
//
//     Три ключові відкриття, кожне ПІДТВЕРДЖЕНО реальними даними файлу під
//     час покрокового тесту (див. NewGlyphCodeExperimentCommand), і всі три
//     тут враховані:
//       1. HEAD[0:2] (u16 LE) — це КІЛЬКІСТЬ ГЛІФІВ шрифту. Її треба
//          оновити на нову загальну кількість, інакше гра читає лише перші
//          N записів і не бачить нових.
//       2. Гра шукає гліф БІНАРНИМ пошуком → уся таблиця FBOD має бути
//          ВІДСОРТОВАНА за кодом. Кириличні коди всі > 0xFF, тож стають
//          відсортованим хвостом після оригінальних англійських.
//       3. Гліфи від рендеру+бікубічного зменшення майже цілком
//          напівпрозорі (~8% повної альфи проти ~63% в оригіналі) →
//          виглядають сірими. alphaGain (виміряно ≈1.5) відновлює суцільне
//          "тіло" літери до рівня оригіналу (GlyphPixelConverter).
//
//     БЕЗ ФАЙЛУ-ТАБЛИЦІ (cyrillic-code-table.json): на відміну від
//     донорського підходу, тут КОД гліфа = РЕАЛЬНИЙ Unicode-код літери.
//     Тобто GUI пише український текст у Locl як звичайний UTF-16LE (що він
//     і так робить), і гра малює його напряму — жодного маппінгу байтів не
//     потрібно. Це прибирає цілий шар складності донорського конвеєра.
//
//     Ширина ВЕЛИКИХ і малих калібрується окремо (GlyphMetricModel),
//     висота ВЕЛИКИХ — лінійним capital-scale (ComputeLetterMetric),
//     висота МАЛИХ — моделлю
//     ядро (x-height, спільне для всіх) + виступ (крапка/дашок/хвіст,
//     обмежений CoreMarginCapPx). Колір (alphaGain) — єдине, що справді
//     НЕ чіпає розмір/форму.
//
//     ІЗОЛЬОВАНО від донорського конвеєра (GlyphAtlasPatcher/
//     CyrillicFontInjector не чіпаються) — самодостатня логіка тут.
//     Оригінальний файл НІКОЛИ не перезаписується.
// EN: PRODUCTION GENERATION of a Cyrillic core.lvl WITHOUT donors — the
//     proven working approach (verified in-game: the game
//     renders direct Unicode codes outside the original set cleanly, with
//     none of the donor unevenness). Replaces the donor-based
//     GenerateLocalizedCore.
//
//     Three key discoveries, each CONFIRMED by real file data during the
//     step-by-step test (see NewGlyphCodeExperimentCommand), all handled
//     here:
//       1. HEAD[0:2] (u16 LE) is the font's GLYPH COUNT. It must be updated
//          to the new total, else the game reads only the first N records
//          and never sees the new ones.
//       2. The game looks glyphs up by BINARY SEARCH → the whole FBOD table
//          must be SORTED by code. Cyrillic codes are all > 0xFF, so they
//          become a sorted tail after the original English ones.
//       3. Glyphs from render + bicubic downscale are almost entirely
//          semi-transparent (~8% full alpha vs ~63% in the original) →
//          look gray. alphaGain (measured ≈1.5) restores the letter's solid
//          "body" to the original's level (GlyphPixelConverter).
//
//     NO SIDECAR TABLE (cyrillic-code-table.json): unlike the donor
//     approach, here the glyph CODE = the letter's REAL Unicode code. So
//     the GUI writes Ukrainian text into Locl as ordinary UTF-16LE (which
//     it already does), and the game renders it directly — no byte mapping
//     needed. This removes a whole layer of donor-pipeline complexity.
//
//     Width for BOTH cases is calibrated separately (GlyphMetricModel); height
//     for UPPERCASE uses a linear capital-scale (ComputeLetterMetric);
//     height for LOWERCASE uses a core (shared x-height) + extension
//     (dot/breve/tail, capped by CoreMarginCapPx) model. Color (alphaGain)
//     is the ONLY thing that truly never touches size/shape.
//
//     ISOLATED from the donor pipeline (GlyphAtlasPatcher/
//     CyrillicFontInjector untouched) — self-contained logic here. The
//     original file is NEVER overwritten.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.FontGenerator.Matching;
using BF1LocalizationTool.FontGenerator.PixelConversion;
// UA: PadRasterized конструює RasterizedGlyph (обгортання чорнила
//     прозорим полем без масштабування).
// EN: PadRasterized constructs a RasterizedGlyph (wrapping ink in a
//     transparent margin without scaling).
using BF1LocalizationTool.FontGenerator.Rasterization;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateNoDonorCyrillicCoreCommand
{
    // UA: Bahnschrift (системний шрифт Windows,
    //     ліцензія забороняє розповсюдження) замінено на Fira Sans
    //     SemiBold (SIL OFL, вільно розповсюджується, дизайн Carrois Type
    //     Design + кирилиця Botio Nikoltchev — чистий європейський
    //     провенанс, той самий шрифт уже використано в проєкті SWH).
    //     Завантажується з .ttf у теці Fonts\ біля .exe через
    //     PrivateFontRegistry, НЕ з системного реєстру шрифтів —
    //     відтворювано незалежно від того, що встановлено на машині.
    //
    //     Значення тепер ВІДНОСНИЙ ШЛЯХ ФАЙЛУ
    //     (`"FiraSans-SemiBold.ttf"`), а НЕ назва родини (`"Fira Sans
    //     SemiBold"`). ПРИЧИНА: GDI+ обрізає family-назви до 31 символа
    //     (LOGFONT-ліміт) і зливає кілька файлів в одну family, якщо їхні
    //     назви родини збігаються — обидва підтверджено реальним
    //     логом-регресією (FONT_FORMAT_SPEC.md, розділ 11.13). Ім'я файлу
    //     — рядок файлової системи, жодного з цих обмежень немає.
    //     PrivateFontRegistry.Get шукає САМЕ за ним. Насиченість
    //     (SemiBold) не змінюється — це виправлення механізму пошуку, не
    //     зміна вибору шрифту.
    // EN: Bahnschrift (a Windows system font,
    //     license forbids redistribution) replaced with Fira Sans
    //     SemiBold (SIL OFL, freely redistributable, designed by Carrois
    //     Type Design + Cyrillic by Botio Nikoltchev — clean European
    //     provenance, the same font already used in the SWH project).
    //     Loaded from a .ttf in the Fonts\ folder next to the .exe via
    //     PrivateFontRegistry, NOT from the system font registry —
    //     reproducible regardless of what's installed on a given machine.
    //
    //     The value is now a RELATIVE FILE PATH
    //     (`"FiraSans-SemiBold.ttf"`), not a family name (`"Fira Sans
    //     SemiBold"`). REASON: GDI+ truncates family names to 31
    //     characters (a LOGFONT limit) and merges several files into one
    //     family when their family names collide — both confirmed by a
    //     real regression in a log (FONT_FORMAT_SPEC.md, section 11.13).
    //     A file name is a filesystem string, subject to neither limit.
    //     PrivateFontRegistry.Get looks up by exactly that. The weight
    //     (SemiBold) does not change — this is a lookup-mechanism fix,
    //     not a font choice change.
    // UA: Константа ЗАМІНЕНА на резолвер за (гра,
    //     розмір шрифту). ПРИЧИНА: реальний прогін `FontCandidateComparisonCommand`
    //     на розширеному пулі кандидатів (57 кандидатів, Fira Sans + Sofia Sans + Exo2 + Unbounded,
    //     FONT_FORMAT_SPEC.md розділ 11.18) показав, що ОДИН шрифт на
    //     обидві гри й усі 5 розмірів — не найкращий підбір за реальними
    //     даними:
    //       - BF1 (ціль капітелей ≈0.50, дуже вузька) — ОДНОСТАЙНО всі
    //         5 розмірів: SofiaSansExtraCondensed-Bold (стиснення лише
    //         2.5-8%, було 18-22% на Fira Sans).
    //       - BF2 (ціль ≈1.00 large/medium/small, ≈0.857 tiny/super_tiny)
    //         — large/medium/small тримають Unbounded (Bold/Black/
    //         ExtraBold — легкий підбір ваги під спад нативної щільності
    //         гри 0.551→0.529 з розміром), а tiny/super_tiny ПЕРЕМИКАЮТЬСЯ
    //         на Exo2-ExtraBold: Unbounded там дає лише aspect=0.722
    //         (26-28% стиснення) — це вже не шум, реальна невідповідність
    //         природної пропорції Unbounded дрібнішій цілі.
    //     Рішення: прошити ТОЧНІ переможці по розміру (не
    //     компроміс на одну вагу).
    // EN: The constant REPLACED by a (game, font
    //     size) resolver. REASON: a real `FontCandidateComparisonCommand`
    //     run over the expanded candidate pool (57 candidates,
    //     Fira Sans + Sofia Sans + Exo2 + Unbounded, FONT_FORMAT_SPEC.md
    //     section 11.18) showed ONE font for both games and all 5 sizes
    //     is not the best fit by real data:
    //       - BF1 (capital target ≈0.50, very narrow) — UNANIMOUS across
    //         all 5 sizes: SofiaSansExtraCondensed-Bold (squeeze only
    //         2.5-8%, was 18-22% with Fira Sans).
    //       - BF2 (target ≈1.00 large/medium/small, ≈0.857 tiny/
    //         super_tiny) — large/medium/small stick with Unbounded
    //         (Bold/Black/ExtraBold — a light weight adjustment tracking
    //         the game's own native density decline 0.551→0.529 with
    //         size), while tiny/super_tiny SWITCH to Exo2-ExtraBold:
    //         Unbounded only reaches aspect=0.722 there (26-28% squeeze)
    //         — not noise, a real mismatch between Unbounded's natural
    //         proportion and the smaller target.
    //     Decision: wire the EXACT per-size winners (not a
    //     single-weight compromise).
    //
    //     UA: ЗМІНЕНО — internal (було private): LowercaseCoreMarginPreviewCommand
    //     (read-only діагностика "ядро+виступ", БЕЗ генерації) навмисно
    //     перевикористовує САМЕ цей вибір шрифту, а не копіює switch —
    //     інакше довелось би тримати два джерела істини й вручну
    //     синхронізувати їх при кожній зміні кандидата.
    //     EN: CHANGED — internal (was private): LowercaseCoreMarginPreviewCommand
    //     (read-only "core+extension" diagnostics, NO generation)
    //     deliberately reuses THIS EXACT font choice instead of copying the
    //     switch — otherwise there'd be two sources of truth to keep in
    //     sync by hand every time the candidate changes.
    internal static string ResolveFontFamilyName(string label, string fontBaseName) => (label, fontBaseName) switch
    {
        ("BF1", _) => "SofiaSansExtraCondensed-Bold.ttf",
        ("BF2", "gamefont_large") => "Unbounded-Bold.ttf",
        ("BF2", "gamefont_medium") => "Unbounded-Black.ttf",
        ("BF2", "gamefont_small") => "Unbounded-ExtraBold.ttf",
        ("BF2", "gamefont_tiny") => "Exo2-ExtraBold.ttf",
        ("BF2", "gamefont_super_tiny") => "Exo2-ExtraBold.ttf",
        _ => throw new InvalidOperationException(
            $"UA: Немає обраного шрифту-кандидата для {label}/{fontBaseName} — додай гілку в ResolveFontFamilyName " +
            $"(і, за потреби, новий .ttf у Fonts\\). / " +
            $"EN: No chosen font candidate for {label}/{fontBaseName} — add a branch to ResolveFontFamilyName " +
            $"(and, if needed, a new .ttf in Fonts\\)."),
    };

    // UA: Множник альфи. ЗНАЧЕННЯ 1.0 (без підсилення) — донорський і
    //     no-donor підходи МАЛЮЮТЬ тим самим шрифтом (Bahnschrift
    //     SemiBold) і тим самим рендером (RenderToFit), тож підсилення
    //     альфи не потрібне: no-donor конвеєр з ним виглядав "ширшим в
    //     обводці" за донорський лише через саме підсилення, яке
    //     донорський конвеєр не застосовує. Виміряно на 'В': донор — 63%
    //     непрозорості (близько до рідних 69%), no-donor з gain=1.5 — 72%
    //     (перебір, потовщені краї штриха); при gain=1.0 середня
    //     непрозорість відповідає донорському варіанту. Параметр лишено
    //     для тонкого підстроювання, якщо колись знадобиться, але за
    //     замовчуванням — без підсилення.
    // EN: Alpha multiplier. VALUE 1.0 (no boost) — the donor and
    //     no-donor approaches DRAW with the same font (Bahnschrift
    //     SemiBold) and the same renderer (RenderToFit), so an alpha
    //     boost is not needed: with one applied, the no-donor pipeline
    //     looked "wider in the stroke" than the donor version only
    //     because of that boost, which the donor pipeline does not
    //     apply. Measured on 'В': donor opacity 63% (close to native
    //     69%), no-donor at gain=1.5 = 72% (too much, thickened stroke
    //     edges); at gain=1.0 the mean opacity matches the donor variant.
    //     The parameter stays for fine-tuning if ever needed, but the
    //     default is no boost.
    private const double AlphaGain = 1.0;

    // UA: starwars_small свідомо НЕ включений — це набір HUD/UI-іконок, не
    //     текстовий шрифт (підтверджено FontGlyphCodeRangeCommand). Той
    //     самий список, що й донорський GenerateOneGame.
    // EN: starwars_small deliberately NOT included — it's a HUD/UI icon set,
    //     not a text font (confirmed by FontGlyphCodeRangeCommand). Same
    //     list as the donor GenerateOneGame.
    private static readonly string[] TargetFontBaseNames =
        ["gamefont_large", "gamefont_medium", "gamefont_small", "gamefont_tiny", "gamefont_super_tiny"];

    // UA: Повертає ШЛЯХ до записаного файлу (не void Task). ПРИЧИНА:
    //     збільшення шрифту BF2 (BF2 НЕ підтримує 1080p нативно, на
    //     відміну від BF1) було окремим, неприєднаним кроком, що
    //     вимагало ручного імпорту через GUI після кожного запуску.
    //     Повернене значення дозволяє виклику (Program.cs) відразу
    //     ланцюжком прогнати BF2-результат через
    //     GenerateEnlargedFontCoreCommand — ОДНА дія меню замість
    //     ручного дволанкового процесу.
    // EN: Returns the PATH of the written file (not void Task). REASON:
    //     BF2 font enlargement (BF2 does NOT support 1080p natively,
    //     unlike BF1) was a separate, unchained step that required a
    //     manual GUI re-import after every run. The return value lets
    //     the caller (Program.cs) immediately chain the BF2 result
    //     through GenerateEnlargedFontCoreCommand — ONE menu action
    //     instead of a manual two-step process.
    // UA: Обгортає вже відрендерене чорнило прозорим полем завширшки pad
    //     з КОЖНОГО боку, НЕ масштабуючи й не обрізаючи його. Прозорі
    //     пікселі пишуться як BGRA(255,255,255, A=0) — білий RGB при
    //     нульовій альфі. Це та сама конвенція, що й у ванільних шрифтах
    //     гри (FONT_FORMAT_SPEC.md §3: RGB=0xFFF скрізь, де A>0, а при
    //     A=0 значення довільне) — і саме білий RGB гарантує, що
    //     білінійна фільтрація на межі гліфа не дасть темної облямівки.
    // EN: Wraps already-rendered ink in a transparent margin `pad` wide on
    //     EVERY side, without scaling or cropping it. Transparent pixels
    //     are written as BGRA(255,255,255, A=0) — white RGB at zero alpha.
    //     That's the same convention as the game's vanilla fonts
    //     (FONT_FORMAT_SPEC.md §3: RGB=0xFFF wherever A>0, arbitrary at
    //     A=0) — and white RGB is precisely what stops bilinear filtering
    //     at a glyph edge from producing a dark fringe.
    private static RasterizedGlyph PadRasterized(RasterizedGlyph src, int pad, int slotWidth, int slotHeight)
    {
        if (pad <= 0) return src;

        var dst = new byte[slotWidth * slotHeight * 4];
        for (var i = 0; i < dst.Length; i += 4)
        {
            dst[i] = 255;     // B
            dst[i + 1] = 255; // G
            dst[i + 2] = 255; // R
            dst[i + 3] = 0;   // A
        }

        for (var y = 0; y < src.Height; y++)
        {
            var srcRow = y * src.Width * 4;
            var dstRow = ((y + pad) * slotWidth + pad) * 4;
            Array.Copy(src.BgraPixels, srcRow, dst, dstRow, src.Width * 4);
        }

        return new RasterizedGlyph
        {
            Width = slotWidth,
            Height = slotHeight,
            BgraPixels = dst,
            InkBounds = new System.Drawing.Rectangle(
                src.InkBounds.X + pad, src.InkBounds.Y + pad, src.InkBounds.Width, src.InkBounds.Height)
        };
    }

    public static async Task<string?> Run(DiagnosticReport report, string inputFilePath, string label)
    {
        if (!File.Exists(inputFilePath))
        {
            report.Log($"UA: Не знайдено {inputFilePath} — генерацію скасовано. / EN: {inputFilePath} not found — generation cancelled.");
            return null;
        }

        report.Log($"UA: [{label}] Генерація кириличного core.lvl БЕЗ донорів (alphaGain={AlphaGain}). / " +
                   $"EN: [{label}] Generating a Cyrillic core.lvl WITHOUT donors (alphaGain={AlphaGain}).");
        report.Log();

        var root = UcfbReader.ReadFile(inputFilePath);
        var fonts = FontChunkLocator.FindAll(root);

        var placedByFont = new Dictionary<string, int>();

        foreach (var fontBaseName in TargetFontBaseNames)
        {
            var font = fonts.FirstOrDefault(f => f.BaseName == fontBaseName);
            if (font is null)
            {
                report.Log($"UA: [{label}] {fontBaseName}: шрифт відсутній — пропущено. / EN: [{label}] {fontBaseName}: font not present — skipped.");
                continue;
            }

            var fbodChunk = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbodChunk is null)
            {
                report.Log($"UA: [{label}] {fontBaseName}: FBOD не знайдено — пропущено. / EN: [{label}] {fontBaseName}: FBOD not found — skipped.");
                continue;
            }

            var originalRecords = FontGlyphTable.Parse(fbodChunk.RawData);

            var allEnglishCellHeights = originalRecords.Select(r => (int)r.CellHeight).ToList();
            var englishCapBearings = originalRecords
                .Where(r => r.Code is >= (ushort)'A' and <= (ushort)'Z')
                .Select(r => (int)r.Bearing)
                .ToList();

            // UA: Медіанна InkWidth рідних A-Z цього
            //     шрифту. Калібрує ОКРЕМИЙ ширинний масштаб у
            //     GlyphMetricModel (замість успадкування ширшого
            //     природного співвідношення Bahnschrift) — корінь
            //     реального обрізання тексту в грі (gamefont_large,
            //     "ОДНОКОРИСТУВАЦ"). Див. коментар у
            //     GlyphMetricModel.DeriveReference.
            // EN: Median InkWidth of this font's own
            //     native A-Z. Calibrates a SEPARATE width scale in
            //     GlyphMetricModel (instead of inheriting Bahnschrift's
            //     own, wider natural ratio) — the root cause of real
            //     in-game text clipping (gamefont_large, "ОДНОКОРИСТУВАЦ").
            //     See the comment in
            //     GlyphMetricModel.DeriveReference.
            var englishCapInkWidths = originalRecords
                .Where(r => r.Code is >= (ushort)'A' and <= (ushort)'Z')
                .Select(r => (int)r.InkWidth)
                .ToList();

            // UA: Bearing реальних англійських малих (a-z) цього шрифту —
            //     вхід у GlyphMetricModel.DeriveReference для ЦІЛЬОВОЇ
            //     висоти "тіла" (CoreHeightGame) моделі ядро+виступи
            //     (ComputeLowercaseCoreMetric).
            // EN: Bearing of this font's real English lowercase (a-z) — an
            //     input to GlyphMetricModel.DeriveReference for the "core"
            //     (x-height) TARGET height (CoreHeightGame) of the
            //     core+extension model (ComputeLowercaseCoreMetric).
            var englishLowerBearings = originalRecords
                .Where(r => r.Code is >= (ushort)'a' and <= (ushort)'z')
                .Select(r => (int)r.Bearing)
                .ToList();

            // UA: ОБИРАЄМО ШРИФТ ТУТ, за (гра, конкретний gamefont_*) — не
            //     одна константа на весь виклик. Див. коментар біля
            //     ResolveFontFamilyName вище.
            // EN: FONT IS CHOSEN HERE, per (game, specific gamefont_*) —
            //     not one constant for the whole call. See the comment by
            //     ResolveFontFamilyName above.
            var fontFamilyName = ResolveFontFamilyName(label, fontBaseName);
            report.Log($"UA: [{label}] {fontBaseName}: шрифт-кандидат — {fontFamilyName}. / EN: [{label}] {fontBaseName}: font candidate — {fontFamilyName}.");

            FontMetricReference metricReference;
            try
            {
                metricReference = GlyphMetricModel.DeriveReference(
                    allEnglishCellHeights, englishCapBearings, CyrillicAlphabet.UppercaseLetters, fontFamilyName,
                    englishCapInkWidths, englishLowerBearings);
            }
            catch (Exception ex)
            {
                report.Log($"UA: [{label}] {fontBaseName}: опорні метрики не виведено ({ex.Message}) — пропущено. / " +
                           $"EN: [{label}] {fontBaseName}: reference metrics failed ({ex.Message}) — skipped.");
                continue;
            }

            var lowercaseSet = new HashSet<char>(CyrillicAlphabet.LowercaseLetters);

            // UA: ОДИН спільний aboveScale/belowScale
            //     для ВСЬОГО алфавіту малих літер ЦЬОГО шрифту, порахований
            //     ЗАЗДАЛЕГІДЬ (перед per-letter циклом нижче). ПРИЧИНА
            //     (реальні дані BF2 gamefont_medium): жорсткий per-letter
            //     клемп до
            //     CoreMarginCapPx різав "і" (природно 1-2px під крапку) і
            //     "б" (природно 3-6px під петлю) до РІВНО того самого
            //     значення — обидві виходили однаковою висотою. Функція
            //     сама скановує весь регістр (той самий ComputeCoreMarginLayout,
            //     що й LowercaseCoreMarginPreviewCommand, — жодного
            //     дублювання математики) і рахує коефіцієнт, що стискає
            //     НАЙБІЛЬШИЙ природний виступ рівно до CoreMarginCapPx;
            //     решта масштабується ТІЄЮ Ж пропорцією. boxSizeForChar тут
            //     — та сама ширина, що й реальний рендер нижче рахує через
            //     ComputeLetterMetric (жодного дублювання).
            // EN: ONE shared aboveScale/belowScale for
            //     the WHOLE lowercase alphabet of THIS font, computed
            //     UP FRONT (before the per-letter loop below). REASON
            //     (real data from BF2 gamefont_medium): the hard per-letter
            //     clamp to
            //     CoreMarginCapPx cut "і" (naturally 1-2px for the dot) and
            //     "б" (naturally 3-6px for the loop) down to EXACTLY the
            //     same value — both came out the same height. The function
            //     itself scans the whole case (the SAME ComputeCoreMarginLayout
            //     that LowercaseCoreMarginPreviewCommand uses — no math
            //     duplication) and computes the factor that shrinks the
            //     LARGEST natural extension down to exactly
            //     CoreMarginCapPx; everything else scales by the SAME
            //     ratio. boxSizeForChar here is the SAME width the real
            //     render below computes via ComputeLetterMetric (no
            //     duplication).
            // UA: boxSizeForChar тут повертає BoxWidth (реальний, ширинний
            //     масштаб не чіпається) + LowercaseProbeBoxHeightBound
            //     (НЕ m.BoxHeight!) — той самий великий запас, що
            //     ComputeLowercaseCoreMetric сам використовує для
            //     ПЕРШОГО, "натурального" виміру. Реальна тісна BoxHeight
            //     конкретної літери вже обрізана до ЇЇ ВЛАСНОГО природного
            //     розміру — вимірювати "природний запас" відносно НЕЇ дало
            //     б завжди 0 (самореференція).
            // EN: boxSizeForChar here returns BoxWidth (real, width scale
            //     untouched) + LowercaseProbeBoxHeightBound (NOT
            //     m.BoxHeight!) — the same large headroom
            //     ComputeLowercaseCoreMetric itself uses for the FIRST,
            //     "natural" measurement pass. A specific letter's real
            //     tight BoxHeight is already clipped to ITS OWN natural
            //     size — measuring "natural headroom" against it would
            //     always show 0 (self-reference).
            var extensionScale = GlyphBoxFitRenderer.ComputeAlphabetExtensionScale(
                CyrillicAlphabet.LowercaseLetters, fontFamilyName,
                metricReference.CoreHeightGame, metricReference.CoreTopYProbe, metricReference.CoreMarginCapPx,
                ch =>
                {
                    var m = GlyphMetricModel.ComputeLetterMetric(ch, fontFamilyName, metricReference);
                    return (m.BoxWidth, GlyphMetricModel.LowercaseProbeBoxHeightBound);
                });

            report.Log($"UA: [{label}] {fontBaseName}: пропорційний кап виступів — aboveScale={extensionScale.AboveScale:F2} " +
                       $"(найбільший природний виступ-зверху {extensionScale.MaxNaturalAboveH}px→{metricReference.CoreMarginCapPx}px), " +
                       $"belowScale={extensionScale.BelowScale:F2} (найбільший природний виступ-знизу {extensionScale.MaxNaturalBelowH}px→{metricReference.CoreMarginCapPx}px). / " +
                       $"EN: [{label}] {fontBaseName}: proportional extension cap — aboveScale={extensionScale.AboveScale:F2} " +
                       $"(largest natural top extension {extensionScale.MaxNaturalAboveH}px→{metricReference.CoreMarginCapPx}px), " +
                       $"belowScale={extensionScale.BelowScale:F2} (largest natural bottom extension {extensionScale.MaxNaturalBelowH}px→{metricReference.CoreMarginCapPx}px).");

            // UA: Нові літери РЕНДЕРяться (без розміщення), а МІСЦЕ
            //     визначає FontRepacker.RepackWithAdditions — той самий
            //     пакувальник (GlyphAtlasPacker), що вже перевірено працює
            //     в GenerateEnlargedFontCoreCommand, і додає СТІЛЬКИ
            //     сторінок, скільки треба. Це принципово: наївний
            //     top-left скан по ІСНУЮЧИХ сторінках без росту (без
            //     додавання нових сторінок) дає масові пропуски "немає
            //     місця" — реальний прогін показав саме це (BF2
            //     gamefont_large 23/66, gamefont_small 41/66), бо нові
            //     кандидати шрифтів (розділи 11.16-11.20) підібрані з
            //     МЕНШИМ спотворенням аспекту, а отже фізично ШИРШІ
            //     бокси, ніж сильно стиснутий Fira Sans. Жодна літера не
            //     пропускається через брак місця.
            // EN: New letters are RENDERED (without placing them), and
            //     FontRepacker.RepackWithAdditions decides placement — the
            //     same packer (GlyphAtlasPacker) already proven in
            //     GenerateEnlargedFontCoreCommand, adding AS MANY pages as
            //     needed. This matters: a naive top-left scan over
            //     EXISTING pages without growth (without adding new
            //     pages) causes mass "no space" skips — a real run showed
            //     exactly that (BF2 gamefont_large 23/66, gamefont_small
            //     41/66), because the newer font candidates (sections
            //     11.16-11.20) were chosen with LESS aspect distortion,
            //     hence physically WIDER boxes than the heavily-squeezed
            //     Fira Sans. No letter is skipped for lack of space.
            var srcResource = FontResourceReader.Read(font.Chunk);

            // UA: ReservedByte4 — призначення досі невідоме (FONT_FORMAT_SPEC.md
            //     §4.1), і воно НЕ константа: реально варіюється 0-6 навіть у
            //     межах однієї сторінки (перевірено побайтово на BF1
            //     reference-files). Раніше донорський підхід копіював його з
            //     конкретного донорського слоту; тут донора немає, тож беремо
            //     НАЙЧАСТІШЕ значення СЕРЕД РЕАЛЬНИХ гліфів ЦЬОГО шрифту (не
            //     довільний перший-з-масиву) — найкраща доступна оцінка,
            //     враховуючи, що зміст байта невідомий.
            // EN: ReservedByte4 — purpose still unknown (FONT_FORMAT_SPEC.md
            //     §4.1), and it is NOT constant: it genuinely varies 0-6 even
            //     within one page (verified byte-for-byte on BF1
            //     reference-files). The old donor approach copied it from a
            //     specific donor slot; with no donor here, this uses the
            //     MOST COMMON value AMONG THIS FONT'S real glyphs (not an
            //     arbitrary first-in-array) — the best available estimate,
            //     given the byte's meaning is unknown.
            var modeReservedByte4 = srcResource.Glyphs.Count > 0
                ? srcResource.Glyphs.GroupBy(g => g.ReservedByte4).OrderByDescending(g => g.Count()).First().Key
                : (byte)0;

            var newGlyphs = new List<NewGlyphInput>();
            var skipped = new List<char>();

            foreach (var ch in CyrillicAlphabet.AllLetters)
            {
                var code = (ushort)ch;
                if (originalRecords.Any(r => r.Code == code) || newGlyphs.Any(g => g.Record.Code == code))
                {
                    skipped.Add(ch);
                    continue;
                }

                var isLowercase = lowercaseSet.Contains(ch);

                LetterTargetMetric metric;
                try
                {
                    // UA: Ширину рахуємо ЗАВЖДИ через ComputeLetterMetric
                    //     (widthScale/CapWidthFloor — не змінені цим
                    //     фіксом, стосуються лише ширини). Для МАЛИХ літер
                    //     висотну частину (Bearing/CellHeight/BoxHeight)
                    //     ПОВНІСТЮ перераховуємо моделлю ядро+виступи —
                    //     замінює лінійний capital-scale, який штучно
                    //     розтягував "і" (див. коментар у
                    //     ComputeLowercaseCoreMetric).
                    // EN: Width is ALWAYS computed via ComputeLetterMetric
                    //     (widthScale/CapWidthFloor — untouched by this
                    //     fix, width-only). For LOWERCASE letters the
                    //     height part (Bearing/CellHeight/BoxHeight) is
                    //     FULLY recomputed by the core+extension model —
                    //     replaces the linear capital-scale that
                    //     artificially stretched "і" (see the comment in
                    //     ComputeLowercaseCoreMetric).
                    var widthMetric = GlyphMetricModel.ComputeLetterMetric(ch, fontFamilyName, metricReference);
                    metric = isLowercase
                        ? GlyphMetricModel.ComputeLowercaseCoreMetric(
                            ch, fontFamilyName, metricReference, widthMetric.BoxWidth,
                            extensionScale.AboveScale, extensionScale.BelowScale)
                        : widthMetric;
                }
                catch (Exception) { skipped.Add(ch); continue; }

                // UA: Рендер БЕЗ прив'язки до конкретної сторінки чи
                //     координат — місце визначає RepackWithAdditions.
                // EN: Rendering WITHOUT binding to a specific page or
                //     coordinates — placement is decided by
                //     RepackWithAdditions.
                // UA: ЧОРНИЛО рендериться у СВІЙ повний розмір
                //     (metric.BoxWidth×BoxHeight) — жодного стиснення.
                //     Прозоре поле додається НАВКОЛО, збільшенням слоту
                //     на 2×SlotPaddingPx по кожній осі (див. коментар біля
                //     SlotPaddingPx). Саме це прибирає "рамку слоту" в
                //     BF1, НЕ звужуючи літери: стиснення ЧОРНИЛА в межі
                //     boxWidth×boxHeight звужує самі літери, а рендер у
                //     ПОВНИЙ розмір із прозорим полем НАВКОЛО — ні.
                // EN: The INK is rendered at its FULL size
                //     (metric.BoxWidth×BoxHeight) — no shrinking at all.
                //     The transparent margin is added AROUND it by growing
                //     the slot by 2×SlotPaddingPx on each axis (see the
                //     SlotPaddingPx comment). This is what removes BF1's
                //     "slot frame" WITHOUT narrowing letters: shrinking
                //     the INK to fit within boxWidth×boxHeight narrows
                //     the letters themselves, while rendering at FULL
                //     size with a transparent margin AROUND it does not.
                var pad = GlyphBoxFitRenderer.SlotPaddingPx;
                var slotWidth = metric.BoxWidth + 2 * pad;
                var slotHeight = metric.BoxHeight + 2 * pad;

                var rasterized = isLowercase
                    ? GlyphBoxFitRenderer.RenderWithCoreAndMargin(
                        ch, fontFamilyName, metricReference.CoreHeightGame, metricReference.CoreTopYProbe,
                        metricReference.CoreMarginCapPx, metric.BoxWidth, metric.BoxHeight,
                        extensionScale.AboveScale, extensionScale.BelowScale)
                    : GlyphBoxFitRenderer.RenderToFit(ch, fontFamilyName, metric.BoxWidth, metric.BoxHeight);

                var padded = PadRasterized(rasterized, pad, slotWidth, slotHeight);
                var pixelBytes = GlyphPixelConverter.ToA4R4G4B4(padded, AlphaGain);

                var xAdvancePadding = originalRecords
                    .Where(r => r.InkWidth > 0)
                    .Select(r => r.XAdvance - r.InkWidth)
                    .OrderBy(d => d)
                    .ToList();
                var medianPadding = xAdvancePadding.Count > 0 ? xAdvancePadding[xAdvancePadding.Count / 2] : 2;

                var template = new FontGlyphRecord
                {
                    Index = originalRecords.Count + newGlyphs.Count,
                    Code = code,
                    PageIndex = 0, // UA: заглушка, перезапише RepackWithAdditions / EN: placeholder, RepackWithAdditions overwrites it
                    XAdvance = (byte)Math.Clamp(metric.BoxWidth + medianPadding, 0, 255),
                    ReservedByte4 = modeReservedByte4,
                    InkWidth = (byte)Math.Clamp(metric.BoxWidth, 0, 255),
                    // UA: Bearing−pad / CellHeight+pad — екранний бокс росте
                    //     рівно на стільки ж, на скільки виріс слот, тож
                    //     масштаб лишається 1:1, а ЧОРНИЛО опиняється рівно
                    //     на тих самих екранних рядках, що й без поля (поле
                    //     прозоре). Без цієї пари гра стиснула б більший
                    //     слот у стару екранну висоту — і літера змаліла б,
                    //     тобто це повторило б попередню помилку іншим
                    //     шляхом.
                    // EN: Bearing−pad / CellHeight+pad — the on-screen box
                    //     grows by exactly as much as the slot did, so the
                    //     mapping stays 1:1 and the INK lands on exactly the
                    //     same screen rows as it would with no margin (the
                    //     margin is transparent). Without this pair the game
                    //     would squeeze the larger slot into the old screen
                    //     height — shrinking the letter, i.e. repeating the
                    //     previous mistake by another route.
                    Bearing = (byte)Math.Clamp(metric.Bearing - pad, 0, 254),
                    CellHeight = (byte)Math.Clamp(metric.CellHeight + pad, 1, 255),
                    U0 = 0, U1 = 0, V0 = 0, V1 = 0, // UA: заглушка / EN: placeholder
                };
                newGlyphs.Add(new NewGlyphInput(template, pixelBytes, slotWidth, slotHeight));
            }

            if (newGlyphs.Count == 0)
            {
                report.Log($"UA: [{label}] {fontBaseName}: жодної літери не розміщено — пропущено. / EN: [{label}] {fontBaseName}: no letter placed — skipped.");
                continue;
            }

            var repacked = FontRepacker.RepackWithAdditions(
                srcResource, newGlyphs, srcResource.Pages[0].Width, srcResource.Pages[0].Height);

            var rebuilt = FontResourceBuilder.Build(repacked);
            font.Chunk.Children.Clear();
            font.Chunk.Children.AddRange(rebuilt.Children);

            placedByFont[fontBaseName] = newGlyphs.Count;
            report.Log($"UA: [{label}] {fontBaseName}: додано {newGlyphs.Count}/66 літер" +
                       (skipped.Count > 0 ? $", пропущено (дублікат коду): {new string(skipped.ToArray())}" : "") +
                       $". Сторінок {srcResource.Pages.Count}→{repacked.Pages.Count}, HEAD → {repacked.Glyphs.Count}, відсортовано за кодом. / " +
                       $"EN: [{label}] {fontBaseName}: added {newGlyphs.Count}/66 letters" +
                       (skipped.Count > 0 ? $", skipped (duplicate code): {new string(skipped.ToArray())}" : "") +
                       $". Pages {srcResource.Pages.Count}→{repacked.Pages.Count}, HEAD → {repacked.Glyphs.Count}, sorted by code.");
        }

        if (placedByFont.Count == 0)
        {
            report.Log($"UA: [{label}] Жодного шрифту не оброблено — файл НЕ записано. / EN: [{label}] No font processed — file NOT written.");
            return null;
        }

        // UA: Той самий відносний шлях, що й у грі — щоб скопіювати теку
        //     "output-nodonor" напряму поверх встановленої гри.
        // EN: The same relative path as in the game — so the "output-nodonor"
        //     folder can be copied straight over the installed game.
        var relativeGamePath = label == "BF2"
            ? Path.Combine("Star Wars Battlefront II Classic", "GameData", "data", "_lvl_pc")
            : Path.Combine("Star Wars Battlefront (Classic 2004)", "GameData", "Data", "_LVL_PC");

        var outputDir = Path.Combine(AppContext.BaseDirectory, "output-nodonor", relativeGamePath);
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "core.lvl");

        // UA: Без replacements-словника: шрифтові чанки
        //     тепер мутовані НАПРЯМУ (font.Chunk.Children.Clear/AddRange
        //     вище), той самий підхід, що й GenerateEnlargedFontCoreCommand.
        //     WriteFile серіалізує ВЖЕ ЗМІНЕНЕ дерево.
        // EN: No replacements dictionary: font chunks
        //     are now mutated DIRECTLY (font.Chunk.Children.Clear/AddRange
        //     above), the same approach GenerateEnlargedFontCoreCommand
        //     uses. WriteFile serializes the ALREADY-MODIFIED tree.
        File.WriteAllBytes(outputPath, UcfbWriter.WriteFile(root));

        report.Log();
        report.Log($"UA: [{label}] Оригінал НЕ змінено. Новий файл: {outputPath}");
        report.Log($"EN: [{label}] Original untouched. New file: {outputPath}");
        report.Log($"UA: [{label}] Файл-таблиця (cyrillic-code-table.json) НЕ потрібна — коди гліфів = реальний Unicode, GUI пише текст як є.");
        report.Log($"EN: [{label}] No sidecar table (cyrillic-code-table.json) needed — glyph codes = real Unicode, the GUI writes text as-is.");

        // UA: Round-trip перевірка.
        // EN: Round-trip check.
        report.Log();
        report.Log($"UA: [{label}] Round-trip перевірка... / EN: [{label}] Round-trip check...");
        try
        {
            var rereadRoot = UcfbReader.ReadFile(outputPath);
            var rereadFonts = FontChunkLocator.FindAll(rereadRoot);

            foreach (var (fontBaseName, addedCount) in placedByFont)
            {
                var rereadFont = rereadFonts.FirstOrDefault(f => f.BaseName == fontBaseName);
                var rereadFbod = rereadFont is not null ? UcfbReader.FindFirst(rereadFont.Chunk, "FBOD") : null;
                if (rereadFbod is null)
                {
                    report.Log($"UA: [{label}] {fontBaseName}: КРИТИЧНО — FBOD не знайдено. / EN: [{label}] {fontBaseName}: CRITICAL — FBOD not found.");
                    continue;
                }

                var rr = FontGlyphTable.Parse(rereadFbod.RawData);
                var head = UcfbReader.FindFirst(rereadFont!.Chunk, "HEAD");
                var headCount = head is not null && head.RawData.Length >= 2 ? BitConverter.ToUInt16(head.RawData, 0) : -1;
                var sorted = Enumerable.Range(0, rr.Count - 1).All(i => rr[i].Code <= rr[i + 1].Code);

                report.Log($"UA: [{label}] {fontBaseName}: {rr.Count} записів (+{addedCount}), HEAD={headCount} " +
                           $"({(headCount == rr.Count ? "OK" : "ПОМИЛКА")}), відсортовано: {(sorted ? "ТАК" : "НІ")}. / " +
                           $"EN: [{label}] {fontBaseName}: {rr.Count} records (+{addedCount}), HEAD={headCount} " +
                           $"({(headCount == rr.Count ? "OK" : "MISMATCH")}), sorted: {(sorted ? "YES" : "NO")}.");
            }

            report.Log($"UA: [{label}] Round-trip завершено. / EN: [{label}] Round-trip complete.");
        }
        catch (Exception ex)
        {
            report.Log($"UA: [{label}] КРИТИЧНО — round-trip провалився: {ex.Message} / EN: [{label}] CRITICAL — round-trip failed: {ex.Message}");
            return null;
        }

        report.Log();
        report.Log($"UA: [{label}] ДАЛІ: у GUI відкрий цей core.lvl як \"Оригінал\" і \"Робочий файл\", зроби \"Імпорт CSV\" з " +
                   $"перекладом (translator-work/{label}.csv), збережи. Попередження про \"відсутні кириличні гліфи\" — хибне (гліфи вже тут), тисни Так.");
        report.Log($"EN: [{label}] NEXT: in the GUI open this core.lvl as \"Original\" and \"Working file\", do \"Import CSV\" with the " +
                   $"translation (translator-work/{label}.csv), save. The \"no Cyrillic glyphs\" warning is a false alarm (glyphs are already here), click Yes.");

        await Task.CompletedTask;
        return outputPath;
    }
}
