// =============================================================================
// BF1LocalizationTool.Diagnostic — Program.cs
// UA: Аналізатор структури .loc/.lvl файлів. Використовує ТОЙ САМИЙ
//     перевірений UcfbReader/FontChunkLocator/LvlLocalizationService, що й
//     GUI/Core — без власного дублюючого парсера.
//
//     Запуск БЕЗ аргументів (двоклік по .exe, F5 у Visual Studio) —
//     інтерактивне КАТЕГОРИЗОВАНЕ меню (спершу категорія, тоді конкретна
//     перевірка в ній) — цикл, не завершується після одного файлу:
//       1. Мовний аналіз (частота байт-кодів, безпечні донорні коди)
//       2. Пікселі й растеризація (RGB/Alpha, стиль атласу, round-trip)
//       3. FBOD-геометрія та донори (перетини, орієнтація, page index)
//       4. UcfbReader/Writer цілісність (round-trip запису, слек, розмір)
//       5. Фантомні діти й hex-дампи (парсер, PIPE/INFO, DATA-чанки)
//       6. GlyphAtlasPatcher: перевірка перед записом
//       0. Вихід
//     Кожна перевірка одночасно ЖИВЕ пише в консоль (як завжди) і
//     накопичує повний звіт у DiagnosticReport.cs, який в кінці зберігає
//     файл у СПІЛЬНУ теку "diagnostic-output" БІЛЯ .exe — так усі звіти
//     живуть в одному місці, і об'ємний вивід не губиться при прокрутці
//     консолі.
//
//     Запуск З аргументами — детальний режим для одного файлу:
//       BF1Diagnostic.exe <шлях> [--tree] [--grep термін]
//                                [--dump-font <ім'я> <шлях.bin>]
//
// EN: .loc/.lvl file structure analyzer. Uses the SAME proven
//     UcfbReader/FontChunkLocator/LvlLocalizationService as the GUI/Core —
//     no separate duplicated parser.
//
//     Launched WITHOUT arguments (double-click the .exe, F5 in Visual
//     Studio) — interactive CATEGORIZED menu (category first, then a
//     specific check within it) — runs in a loop, doesn't exit after one
//     file:
//       1. Language analysis (byte-code frequency, safe donor codes)
//       2. Pixels & rasterization (RGB/Alpha, atlas style, round-trip)
//       3. FBOD geometry & donors (overlaps, orientation, page index)
//       4. UcfbReader/Writer integrity (write round-trip, slack, size)
//       5. Phantom children & hex dumps (parser, PIPE/INFO, DATA chunks)
//       6. GlyphAtlasPatcher: pre-write checks
//       0. Exit
//     Every check simultaneously writes LIVE to the console (as always)
//     and accumulates a full report via DiagnosticReport.cs, which at the
//     end saves a file to a SHARED "diagnostic-output" folder NEXT TO the
//     .exe — so all reports live in one place, and large output isn't
//     lost when scrolling the console.
//
//     Launched WITH arguments — detailed single-file mode:
//       BF1Diagnostic.exe <path> [--tree] [--grep term]
//                                [--dump-font <name> <path.bin>]
// =============================================================================

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Localization;
using BF1LocalizationTool.Core.Scripts;
using BF1LocalizationTool.Diagnostic;

// UA: Примусово встановлюємо UTF-8 для консолі. Без цього консоль Windows
//     (типово OEM 866 чи інша застаріла кодова сторінка) коректно показує
//     звичайну кирилицю, але замінює на '?' специфічно українські літери
//     (і, ї, є, ґ), яких немає в OEM 866 (він розрахований на російську).
// EN: Force UTF-8 for the console. Without this, the Windows console
//     (typically OEM 866 or another legacy codepage) correctly displays
//     regular Cyrillic, but replaces Ukrainian-specific letters (і, ї, є,
//     ґ) with '?', since those aren't in OEM 866 (designed for Russian).
Console.OutputEncoding = Encoding.UTF8;

// UA: Скільки кодів потрібно для повного кириличного алфавіту без жодного
//     переиспользування гліфів (33 українські літери × 2 регістри).
// EN: How many codes are needed for a full Cyrillic alphabet with zero
//     glyph reuse (33 Ukrainian letters × 2 cases).
if (args.Length > 0)
{
    await RunDetailedSingleFileMode(args);
    return;
}

if (!OperatingSystem.IsWindows())
{
    PrintUsage();
    return;
}

await RunInteractiveMenu();
return;

// ===========================================================================
// UA: ІНТЕРАКТИВНЕ КАТЕГОРИЗОВАНЕ МЕНЮ — цикл, не завершується після
//     одного аналізу. Спершу вибір категорії, тоді — конкретної перевірки
//     всередині неї ("0" завжди повертає на рівень вище).
// EN: INTERACTIVE CATEGORIZED MENU — a loop, doesn't exit after one
//     analysis. Category first, then a specific check within it ("0"
//     always goes back one level).
// ===========================================================================
async Task RunInteractiveMenu()
{
    var categories = BuildCategories();

    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("=== BF1LocalizationTool.Diagnostic ===");
        for (var i = 0; i < categories.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {categories[i].TitleUA}");
            Console.WriteLine($"   {categories[i].TitleEN}");
        }
        Console.WriteLine("0. Вихід / Exit");
        Console.Write("> ");
        var choice = Console.ReadLine()?.Trim();

        if (choice is null or "0")
            return;

        if (!int.TryParse(choice, out var categoryIndex) || categoryIndex < 1 || categoryIndex > categories.Count)
        {
            Console.WriteLine("UA: Невідомий вибір. / EN: Unknown choice.");
            continue;
        }

        await RunCategoryMenu(categories[categoryIndex - 1]);
    }
}

async Task RunCategoryMenu(MenuCategory category)
{
    while (true)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {category.TitleUA} / {category.TitleEN} ===");
        for (var i = 0; i < category.Items.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {category.Items[i].DescUA}");
            Console.WriteLine($"   {category.Items[i].DescEN}");
        }
        Console.WriteLine("0. Назад / Back");
        Console.Write("> ");
        var choice = Console.ReadLine()?.Trim();

        if (choice is null or "0")
            return;

        if (!int.TryParse(choice, out var itemIndex) || itemIndex < 1 || itemIndex > category.Items.Count)
        {
            Console.WriteLine("UA: Невідомий вибір. / EN: Unknown choice.");
            continue;
        }

        try
        {
            await category.Items[itemIndex - 1].Action();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UA: Помилка аналізу: {ex.Message}");
            Console.WriteLine($"EN: Analysis error: {ex.Message}");
        }
    }
}

// ===========================================================================
// UA: Опис усіх категорій і пунктів меню — ЄДИНЕ місце, де перелічені всі
//     26 перевірок. Кожен пункт лише посилається на Run*-обгортку нижче —
//     сама логіка перевірки НЕ дублюється тут.
// EN: Description of all menu categories and items — the ONE place that
//     lists all 26 checks. Each item just references a Run* wrapper
//     below — the actual check logic is NOT duplicated here.
// ===========================================================================
List<MenuCategory> BuildCategories() =>
[
    new MenuCategory(
        "Мовний аналіз (частота байт-кодів, безпечні донорні коди)",
        "Language analysis (byte-code frequency, safe donor codes)",
        [
            new MenuItem(
                "Проаналізувати ОБИДВІ гри (BF1 + BF2) — спільне й відмінне",
                "Analyze BOTH games (BF1 + BF2) — common ground & differences",
                AnalyzeBothGames),
            new MenuItem(
                "Лише BF1 (Classic 2004)",
                "BF1 only",
                () => AnalyzeSingleGame("BF1 (Classic 2004)", "AnalyzeSingleGame_BF1")),
            new MenuItem(
                "Лише BF2 (Classic)",
                "BF2 only",
                () => AnalyzeSingleGame("BF2 (Classic)", "AnalyzeSingleGame_BF2")),
        ]),

    new MenuCategory(
        "Пікселі й растеризація (RGB/Alpha, стиль атласу, round-trip конвертера)",
        "Pixels & rasterization (RGB/Alpha, atlas style, converter round-trip)",
        [
            new MenuItem(
                "Дамп пікселів гліфа (перевірка RGB/Alpha перед FontGenerator)",
                "Dump glyph pixels (verify RGB/Alpha before FontGenerator)",
                RunGlyphPixelDump),
            new MenuItem(
                "Стиль прозорих пікселів: ОБИДВІ гри одним запуском (без гіпотез, лише лічильники)",
                "Transparent-pixel style: BOTH games in one run (no hypotheses, raw counters)",
                RunGlyphAtlasStyleReport),
            new MenuItem(
                "Перевірка round-trip конвертера пікселів (перед записом у BODY)",
                "Pixel converter round-trip check (before writing to BODY)",
                RunPixelConversionRoundTripCheck),
            new MenuItem(
                "Порівняння кандидатів шрифту кирилицею поруч (Bahnschrift/Segoe UI)",
                "Side-by-side Cyrillic comparison of font candidates (Bahnschrift/Segoe UI)",
                RunFontCandidatePreview),
            new MenuItem(
                "Контактний аркуш УСІХ шрифтових ресурсів файлу (чи всі один стиль, чи різні?)",
                "Contact sheet of ALL font resources in a file (one style or several?)",
                RunExportAllFontStylesContactSheet),
        ]),

    new MenuCategory(
        "FBOD-геометрія та донори (перетини, орієнтація, page index)",
        "FBOD geometry & donors (overlaps, orientation, page index)",
        [
            new MenuItem(
                "Перевірка напрямку осі V (ASCII-рендер асиметричного гліфа, напр. L або ,)",
                "V-axis direction check (ASCII render of an asymmetric glyph, e.g. L or ,)",
                RunGlyphOrientationCheck),
            new MenuItem(
                "Перевірка узгодженості розміру гліфа: FBOD (ink_width/cell_h) vs UV-прямокутник",
                "Glyph size consistency check: FBOD (ink_width/cell_h) vs UV rectangle",
                RunGlyphSizeConsistencyCheck),
            new MenuItem(
                "Перевірка перетину UV-прямокутників гліфів на одній сторінці (безпека сусідів)",
                "UV rectangle overlap check for glyphs on the same page (neighbor safety)",
                RunGlyphRectOverlapCheck),
            new MenuItem(
                "Класифікація ризику перетинів (донор×використаний vs донор×донор)",
                "Overlap risk classification (donor×used vs donor×donor)",
                RunGlyphOverlapRiskCheck),
            new MenuItem(
                "Ефективно безпечні донори ПО КОЖНОМУ шрифту окремо (виправлення per-font геометрії)",
                "Effectively safe donors PER FONT separately (per-font geometry fix)",
                RunPerFontSafeDonorCheck),
            new MenuItem(
                "КРИТИЧНО: чи належить гліф лише одній сторінці, чи всім? (перед довірою до перетинів)",
                "CRITICAL: does a glyph belong to one page only, or all? (before trusting overlap results)",
                RunGlyphPageAssignmentCheck),
            new MenuItem(
                "Повне дерево чанків шрифту — перевірка кількості FBOD (факт, без евристик)",
                "Full font chunk tree — FBOD count check (fact-based, no heuristics)",
                RunFontResourceTreeDump),
            new MenuItem(
                "Аналіз \"зарезервованих\" байтів FBOD (чи не кодують номер сторінки?)",
                "FBOD \"reserved\" byte analysis (do they encode a page number?)",
                RunReservedByteAnalysis),
            new MenuItem(
                "Остаточна перевірка: offset=2 FBOD = індекс сторінки? (звірка з реальними пікселями)",
                "Final check: does FBOD offset=2 equal page index? (cross-checked with real pixels)",
                RunPageIndexByteCorrelation),
            new MenuItem(
                "Перевірка перетинів ПІСЛЯ групування за offset=2 (сильніший тест гіпотези сторінки)",
                "Overlap check AFTER grouping by offset=2 (stronger page-index hypothesis test)",
                RunGlyphPageGroupingOverlapCheck),
            new MenuItem(
                "Вертикальна геометрія реальних ASCII-гліфів (спільна комірка чи tight crop? — перед формулою BaselineY)",
                "Vertical geometry of real ASCII glyphs (shared cell or tight crop? — before the BaselineY formula)",
                RunGlyphVerticalGeometryProbe),
            new MenuItem(
                "Сирий список кодів УСІХ шрифтів обох ігор (текст чи набір іконок?)",
                "Raw code list of ALL fonts in both games (text or an icon set?)",
                RunFontGlyphCodeRange),
            new MenuItem(
                "Побайтове порівняння ДВОХ шрифтових ресурсів (чи справді ідентичні, чи помилка коду?)",
                "Byte-for-byte comparison of TWO font resources (genuinely identical, or a code bug?)",
                RunFontResourceIdentityCheck),
            new MenuItem(
                "Кореляція метрик FBOD (XAdvance/Bearing/CellHeight) по КОЖНОМУ гліфу, не за середнім",
                "FBOD metrics correlation (XAdvance/Bearing/CellHeight) per INDIVIDUAL glyph, not averaged",
                RunFontGlyphMetricsCorrelation),
        ]),

    new MenuCategory(
        "UcfbReader/Writer цілісність (round-trip запису, слек, розбіжності розміру)",
        "UcfbReader/Writer integrity (write round-trip, slack, size discrepancies)",
        [
            new MenuItem(
                "Round-trip перевірка UcfbWriter на реальному файлі (перед GlyphAtlasPatcher, файл НЕ змінюється)",
                "UcfbWriter round-trip check on a real file (before GlyphAtlasPatcher, file NOT modified)",
                RunUcfbWriteRoundTripCheck),
            new MenuItem(
                "Перевірка \"хвоста\" файлу поза деревом ucfb",
                "Check for a file \"tail\" outside the ucfb tree",
                RunUcfbFileSizeDiscrepancyCheck),
            new MenuItem(
                "КРИТИЧНО: чи ламає розмір файлу read→write БЕЗ жодних змін? (базовий контроль)",
                "CRITICAL: does read→write with ZERO changes alter file size? (baseline control)",
                RunUcfbWriterNoOpRoundTripCheck),
            new MenuItem(
                "Структурне порівняння дерев (оригінал vs перезапис) — точне джерело втрати байт",
                "Structural tree diff (original vs rewrite) — exact source of byte loss",
                RunUcfbStructuralTreeDiff),
            new MenuItem(
                "Пошук \"слеку\" (зарезервованого простору) в контейнерах ucfb (обидві гри)",
                "Searching for \"slack\" (reserved space) in ucfb containers (both games)",
                RunContainerSlackSpaceCheck),
        ]),

    new MenuCategory(
        "Фантомні діти й hex-дампи (парсер, PIPE/INFO, DATA-чанки)",
        "Phantom children & hex dumps (parser, PIPE/INFO, DATA chunks)",
        [
            new MenuItem(
                "Прямий hex-дамп підозрілого DATA-чанку (перевірка гіпотези міспарсингу)",
                "Direct hex dump of suspicious DATA chunk (misparse hypothesis check)",
                RunSoundDataMisparseHexDump),
            new MenuItem(
                "Повний скан фантомних дітей по КОЖНОМУ FourCC (список кандидатів для виправлення)",
                "Full phantom-child scan for EVERY FourCC (candidate list for the fix)",
                RunFourCCPhantomChildScan),
            new MenuItem(
                "КРИТИЧНО: чи уражені font BODY-чанки фантомними дітьми? (загроза запису)",
                "CRITICAL: are font BODY chunks affected by phantom children? (write-time threat)",
                RunFontBodyPhantomChildCheck),
            new MenuItem(
                "Прямий hex-дамп PIPE>INFO чанку (що саме в загублених 4 байтах?)",
                "Direct hex dump of PIPE>INFO chunk (what's in the lost 4 bytes?)",
                RunPipeInfoHexDump),
            new MenuItem(
                "Вичерпна перевірка КОЖНОГО INFO-чанку (є винятки зі справжньою підструктурою?)",
                "Exhaustive check of EVERY INFO chunk (any exceptions with real substructure?)",
                RunInfoChunkExhaustiveCheck),
        ]),

    new MenuCategory(
        "GlyphAtlasPatcher: перевірка перед записом",
        "GlyphAtlasPatcher: pre-write checks",
        [
            new MenuItem(
                "Жоден UV-прямокутник не виходить за межі своєї текстурної сторінки (з урахуванням PageIndex)",
                "#1: no UV rectangle exceeds its texture page bounds (PageIndex-aware)",
                RunUvRectBoundsCheck),
            new MenuItem(
                "BODY кожної сторінки = Width×Height×2 без padding (усі сторінки, не лише _tex0)",
                "#2: every page's BODY = Width×Height×2 with no padding (all pages, not just _tex0)",
                RunBodySizeConsistencyCheck),
            new MenuItem(
                "Наскрізний прогін Rasterize→ToA4R4G4B4 на РЕАЛЬНИХ розмірах слотів (без винятків, точна довжина)",
                "#3: end-to-end Rasterize→ToA4R4G4B4 run on REAL slot sizes (no exceptions, exact length)",
                RunRasterizerCanvasSizeCheck),
            new MenuItem(
                "Жоден фізичний слот (PageIndex+прямокутник) не використовується двома кодами",
                "#4: no physical slot (PageIndex+rectangle) is shared by two codes",
                RunGlyphSlotUniquenessCheck),
            new MenuItem(
                "Геометрична перевірка м'яких донорів (коли базових бракує — БФ2: 60 з 66)",
                "Geometric check of soft donors (when basic ones fall short — BF2: 60 of 66)",
                RunSoftDonorGeometryCheck),
            new MenuItem(
                "Розподіл розмірів донорів по кожному шрифту (чи є розмаїття для підбору під форму літери?)",
                "Donor size distribution per font (is there variety for shape-based matching?)",
                RunSafeDonorGeometryDistribution),
            new MenuItem(
                "Прев'ю призначення донорів реальному алфавіту (GlyphDonorMatcher на реальних розмірах)",
                "Preview of real alphabet-to-donor assignment (GlyphDonorMatcher on real sizes)",
                RunGlyphDonorAssignmentPreview),
            new MenuItem(
                "Рендер-прев'ю ОДНОГО шрифту: усі 66 призначень намальовані в реальному розмірі слоту (PNG)",
                "Render preview of ONE font: all 66 assignments drawn at real slot size (PNG)",
                RunGlyphFitRenderPreview),
            new MenuItem(
                "Потенціал розширення донорів у вільний простір текстури (без зачіпання сусідів)",
                "Donor growth potential into free texture space (without touching neighbors)",
                RunDonorSlotGrowthPotential),
            new MenuItem(
                "Контурна перевірка зайнятості: реальні пікселі + межі КОЖНОГО гліфа, усі сторінки (PNG)",
                "Occupancy outline check: real pixels + EVERY glyph's bounds, all pages (PNG)",
                RunGlyphOccupancyOverlay),
            new MenuItem(
                "Те саме, але БЕЗ дефолтного reference-files — діалог вибору файлу завжди (для перевірки САМЕ того core.lvl, що зараз у грі)",
                "Same, but WITHOUT the reference-files default — file picker always shown (to inspect the EXACT core.lvl currently in the game)",
                RunGlyphOccupancyOverlayManualPick),
            new MenuItem(
                "Прев'ю моделі ядро+виступ для малих літер (і/ї/й/б/ф): CapHeightGame/CoreHeightGame/CoreMarginCapPx + фактичний виступ по кожному шрифту (БЕЗ генерації)",
                "Lowercase core+extension model preview (і/ї/й/б/ф): CapHeightGame/CoreHeightGame/CoreMarginCapPx + actual extension per font (NO generation)",
                RunLowercaseCoreMarginPreview),
            new MenuItem(
                "Цільове розширення призначених донорів + порівняння особливих і звичайних літер",
                "Targeted growth of assigned donors + special-vs-ordinary letter comparison",
                RunTargetedGrowthDonorAssignmentPreview),
        ]),

    new MenuCategory(
        "⚠ ГЕНЕРАЦІЯ: створити локалізований core.lvl (ПИШЕ файл, не лише перевіряє)",
        "⚠ GENERATION: create a localized core.lvl (WRITES a file, not just checks)",
        [
            new MenuItem(
                "Порівняти кандидатів шрифту з Fonts\\ проти РЕАЛЬНИХ метрик ванільного шрифту гри (widthScale по кожній грі/розміру, БЕЗ запису файлу)",
                "Compare Fonts\\ font candidates against the REAL vanilla game font metrics (widthScale per game/size, NO file written)",
                RunFontCandidateComparison),
            new MenuItem(
                "★ БЕЗ ДОНОРІВ: згенерувати кириличний core.lvl прямими Unicode-кодами (доведено в грі; тека \"output-nodonor\", без файлу-таблиці)",
                "★ NO DONORS: generate a Cyrillic core.lvl with direct Unicode codes (proven in-game; \"output-nodonor\" folder, no sidecar table)",
                RunGenerateNoDonorCyrillicCore),
            new MenuItem(
                "Згенерувати кириличний core.lvl (ДОНОРСЬКИЙ, застарілий підхід — усі gamefont_*, новий файл, оригінал не чіпається)",
                "Generate a Cyrillic core.lvl (DONOR-based, legacy approach — all gamefont_*, new file, original untouched)",
                RunGenerateLocalizedCore),
            new MenuItem(
                "Збільшене прев'ю РЕАЛЬНИХ запатчених пікселів для тестових слів (з уже згенерованого output, PNG)",
                "Zoomed-in preview of the REAL patched pixels for test words (from an already-generated output, PNG)",
                RunWordRenderInspect),
            new MenuItem(
                "Уся текстурна сторінка ЦІЛКОМ (англійська+вставлена кирилиця РАЗОМ, реальні пікселі, контур КОЖНОГО гліфа) — з output",
                "The WHOLE texture page (English+inserted Cyrillic TOGETHER, real pixels, EVERY glyph's outline) — from output",
                RunGlyphOccupancyOverlayOnOutput),
            new MenuItem(
                "ЕКСПЕРИМЕНТ: примусово однакове CellHeight для кирилиці (окрема тека \"output-celltest\", перевірка гіпотези вертикального розміру)",
                "EXPERIMENT: force uniform CellHeight for Cyrillic (separate \"output-celltest\" folder, tests the vertical-size hypothesis)",
                RunCellHeightHypothesisTest),
            new MenuItem(
                "ЕКСПЕРИМЕНТ: метрична модель Bearing=CellHeight−бокс (окрема тека \"output-metrictest\", доводить формулу вирівнювання базової лінії)",
                "EXPERIMENT: metric model Bearing=CellHeight−box (separate \"output-metrictest\" folder, proves the baseline-alignment formula)",
                RunMetricBaselineTest),
            new MenuItem(
                "ЕКСПЕРИМЕНТ: УСІ 66 літер БЕЗ донорів, реальні Unicode-коди, в УСІХ шрифтах BF2 (окрема тека \"new-glyph-test\", перевіряє чи гра відмальовує коди поза донорським набором)",
                "EXPERIMENT: ALL 66 letters WITHOUT donors, real Unicode codes, in EVERY BF2 font (separate \"new-glyph-test\" folder, tests whether the game renders codes outside the donor set)",
                RunNewGlyphCodeExperiment),
            new MenuItem(
                "★ АДДОН Tat3: зробити назву карти перекладною (патч addme.script + новий запис Locl, тека \"output-addon\") — після цього рядок з'являється в GUI як звичайний",
                "★ Tat3 ADD-ON: make the map name translatable (patch addme.script + new Locl record, \"output-addon\" folder) — the string then shows up in the GUI as an ordinary row",
                RunPatchAddOnMapName),
        ]),

    new MenuCategory(
        "BF2 widescreen: Lua-байткод shell.lvl/ingame.lvl (без сторонніх .exe)",
        "BF2 widescreen: shell.lvl/ingame.lvl Lua bytecode (no third-party .exe)",
        [
            new MenuItem(
                "Розібрати shell.lvl + ingame.lvl: усі float/string константи (HIGHLIGHTS + повний дамп)",
                "Parse shell.lvl + ingame.lvl: all float/string constants (HIGHLIGHTS + full dump)",
                RunAnalyzeShellScriptConstants),
            new MenuItem(
                "Дизасемблер ЛИШЕ функцій за ключовими словами (ScreenRelative/GetSafeScreenInfo/aspect тощо)",
                "Disassembly of ONLY functions matching keywords (ScreenRelative/GetSafeScreenInfo/aspect etc.)",
                RunDisassembleScreenLayoutFunctions),
        ]),

    new MenuCategory(
        "BF2 widescreen: генерація патча (Lua50BytecodeWriter + bootstrap entry-point)",
        "BF2 widescreen: patch generation (Lua50BytecodeWriter + bootstrap entry-point)",
        [
            new MenuItem(
                "Тест Writer'а: round-trip власного стабу + реальних скриптів + повний ApplyBootstrapPatch (у пам'яті)",
                "Writer test: round-trip of our own stub + real scripts + full ApplyBootstrapPatch (in-memory)",
                RunTestWidescreenBootstrapPatch),
            new MenuItem(
                "ЗАПИСАТИ патчений shell.lvl на диск (widescreen-output/shell.lvl) — для реального тесту в грі",
                "WRITE the patched shell.lvl to disk (widescreen-output/shell.lvl) — for a real in-game test",
                RunGenerateWidescreenPatchedShell),
            new MenuItem(
                "ДІАГНОСТИКА: shell.lvl з маркерним зсувом +300px замість формули (перевірити, чи викликається NewIFContainer)",
                "DIAGNOSTIC: shell.lvl with a +300px marker offset instead of the formula (check whether NewIFContainer is called)",
                RunGenerateWidescreenDebugMarkerShell),
        ]),

    new MenuCategory(
        "Шрифти: генерація свіжого атласу (font-writer)",
        "Fonts: fresh-atlas generation (font-writer)",
        [
            new MenuItem(
                "Round-trip тест FontResourceReader+Builder (перебудова font-чанка байт-у-байт)",
                "Round-trip test of FontResourceReader+Builder (rebuild font chunk byte-for-byte)",
                RunTestFontResourceRoundTrip),
            new MenuItem(
                "ПЕРЕПАКУВАТИ шрифти у свіжий атлас → font-output/core.lvl (ті самі гліфи, нова розкладка — тест у грі)",
                "REPACK fonts into a fresh atlas → font-output/core.lvl (same glyphs, new layout — in-game test)",
                RunGenerateRepackedFontCore),
            new MenuItem(
                "ЗБІЛЬШИТИ шрифт BF2 ×1.5 (upscale) → font-output-enlarged/BF2/core.lvl (тест: текст більший)",
                "ENLARGE BF2 font ×1.5 (upscale) → font-output-enlarged/BF2/core.lvl (test: bigger text)",
                RunGenerateEnlargedFontCore),
        ]),
];

async Task AnalyzeSingleGame(string label, string commandSlug)
{
    var picked = NativeFileDialog.ShowOpenDialog($"Виберіть core.lvl для {label} / Select core.lvl for {label}");
    if (picked is null)
    {
        Console.WriteLine("UA: Файл не вибрано. / EN: No file selected.");
        return;
    }

    var report = new DiagnosticReport(commandSlug);

    report.Log($"=== {label}: {picked} ===");
    var summary = await SoftDonorAnalysis.BuildSummaryAsync(picked);
    SoftDonorAnalysis.PrintSummary(summary, report.Log);
    SoftDonorAnalysis.PrintSoftDonorSuggestions(summary, SoftDonorAnalysis.NeededGlyphCodes, report.Log);

    report.Save();
}

async Task AnalyzeBothGames()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано. / EN: BF1 file not selected."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано. / EN: BF2 file not selected."); return; }

    var report = new DiagnosticReport("AnalyzeBothGames");

    report.Log($"=== BF1: {bf1Path} ===");
    var bf1 = await SoftDonorAnalysis.BuildSummaryAsync(bf1Path);
    SoftDonorAnalysis.PrintSummary(bf1, report.Log);

    report.Log("");
    report.Log($"=== BF2: {bf2Path} ===");
    var bf2 = await SoftDonorAnalysis.BuildSummaryAsync(bf2Path);
    SoftDonorAnalysis.PrintSummary(bf2, report.Log);

    // -------------------------------------------------------------------
    // UA: Порівняння — спільне для обох ігор і окремо особливості кожної.
    // EN: Comparison — common to both games and each game's own specifics.
    // -------------------------------------------------------------------
    report.Log("");
    report.Log("=== СПІЛЬНЕ ДЛЯ ОБОХ ІГОР / COMMON TO BOTH GAMES ===");
    var bf1FontNames = bf1.Fonts.Select(f => f.BaseName).ToHashSet();
    var bf2FontNames = bf2.Fonts.Select(f => f.BaseName).ToHashSet();
    var commonFonts = bf1FontNames.Intersect(bf2FontNames).OrderBy(x => x).ToList();
    report.Log($"Спільні шрифтові ресурси ({commonFonts.Count}): {string.Join(", ", commonFonts)}");

    var commonSafe = bf1.SafeDonorCodes.Intersect(bf2.SafeDonorCodes).OrderBy(x => x).ToList();
    report.Log($"Спільні безпечні донорні коди (безпечні в ОБОХ іграх): {commonSafe.Count} шт.");
    report.Log("  " + string.Join(", ", commonSafe.Select(x => $"0x{x:X2}")));

    report.Log("");
    report.Log("=== ОСОБЛИВОСТІ BF1 / BF1-SPECIFIC ===");
    var bf1OnlyFonts = bf1FontNames.Except(bf2FontNames).OrderBy(x => x).ToList();
    report.Log($"Шрифти лише в BF1: {(bf1OnlyFonts.Count == 0 ? "(немає)" : string.Join(", ", bf1OnlyFonts))}");
    var bf1OnlySafe = bf1.SafeDonorCodes.Except(bf2.SafeDonorCodes).OrderBy(x => x).ToList();
    report.Log($"Коди безпечні лише в BF1 (в BF2 вже зайняті): {bf1OnlySafe.Count} шт.");

    report.Log("");
    report.Log("=== ОСОБЛИВОСТІ BF2 / BF2-SPECIFIC ===");
    var bf2OnlyFonts = bf2FontNames.Except(bf1FontNames).OrderBy(x => x).ToList();
    report.Log($"Шрифти лише в BF2: {(bf2OnlyFonts.Count == 0 ? "(немає)" : string.Join(", ", bf2OnlyFonts))}");
    var bf2OnlySafe = bf2.SafeDonorCodes.Except(bf1.SafeDonorCodes).OrderBy(x => x).ToList();
    report.Log($"Коди безпечні лише в BF2 (в BF1 вже зайняті): {bf2OnlySafe.Count} шт.");

    // -------------------------------------------------------------------
    // UA: BF1 і BF2 — ДВА НЕЗАЛЕЖНІ файли з окремими LocalizationParser-
    //     викликами й окремими шрифтовими атласами. Немає жодної технічної
    //     причини змушувати їх ділити ОДНУ таблицю кодування — це просто
    //     два словники в коді. Тому нестачу рахуємо ОКРЕМО для кожної гри:
    //     якщо в однієї є надлишок (як у BF1: 90 із 66 потрібних), вона не
    //     повинна "позичати" нічого лише заради уявної спільності з іншою.
    // EN: BF1 and BF2 are TWO INDEPENDENT files with separate
    //     LocalizationParser calls and separate font atlases. There's no
    //     technical reason to force them to share ONE encoding table — it's
    //     just two dictionaries in code. So the shortfall is calculated
    //     SEPARATELY per game: if one has a surplus (like BF1: 90 of the
    //     66 needed), it shouldn't "borrow" anything just for the sake of
    //     an artificial shared table with the other.
    // -------------------------------------------------------------------
    report.Log("");
    report.Log($"=== BF1: чи вистачає {SoftDonorAnalysis.NeededGlyphCodes} кодів для повного кириличного алфавіту? ===");
    if (bf1.SafeDonorCodes.Count >= SoftDonorAnalysis.NeededGlyphCodes)
        report.Log($"UA: Так — {bf1.SafeDonorCodes.Count} безпечних кодів, надлишок {bf1.SafeDonorCodes.Count - SoftDonorAnalysis.NeededGlyphCodes}. Позичати нічого не треба.");
    else
        SoftDonorAnalysis.PrintSoftDonorSuggestions(bf1, SoftDonorAnalysis.NeededGlyphCodes, report.Log);

    report.Log("");
    report.Log($"=== BF2: чи вистачає {SoftDonorAnalysis.NeededGlyphCodes} кодів для повного кириличного алфавіту? ===");
    if (bf2.SafeDonorCodes.Count >= SoftDonorAnalysis.NeededGlyphCodes)
        report.Log($"UA: Так — {bf2.SafeDonorCodes.Count} безпечних кодів, надлишок {bf2.SafeDonorCodes.Count - SoftDonorAnalysis.NeededGlyphCodes}. Позичати нічого не треба.");
    else
        SoftDonorAnalysis.PrintSoftDonorSuggestions(bf2, SoftDonorAnalysis.NeededGlyphCodes, report.Log);

    report.Save();
}

// ===========================================================================
// UA: Дамп пікселів одного гліфа — перевірка припущення "RGB=білий,
//     Alpha=покриття" перед написанням A4R4G4B4-конвертера в FontGenerator.
//     Питає шлях до core.lvl, базове ім'я шрифту, ім'я текстурної
//     сторінки й код символу — все, крім останнього, можна підглянути у
//     виводі "Мовного аналізу" цього ж меню (список шрифтів і сторінок).
// EN: Dump pixels of a single glyph — verifies the "RGB=white,
//     Alpha=coverage" assumption before writing the A4R4G4B4 converter
//     in FontGenerator. Asks for core.lvl path, font base name, texture
//     page name, and character code — all but the last can be checked
//     from this same menu's "Language analysis" output (font/page listing).
// ===========================================================================
async Task RunGlyphPixelDump()
{
    var picked = NativeFileDialog.ShowOpenDialog("Виберіть core.lvl / Select core.lvl");
    if (picked is null)
    {
        Console.WriteLine("UA: Файл не вибрано. / EN: No file selected.");
        return;
    }

    Console.Write("Базове ім'я шрифту (напр. gamefont_super_tiny) / Font base name: ");
    var fontBaseName = Console.ReadLine()?.Trim() ?? "";

    Console.Write("Ім'я текстурної сторінки (напр. gamefont_super_tiny_tex0) / Texture page name: ");
    var texturePageName = Console.ReadLine()?.Trim() ?? "";

    Console.Write("Код символу в hex (напр. 4D для 'M', 2E для '.') / Character code in hex: ");
    var codeInput = Console.ReadLine()?.Trim() ?? "";

    if (!ushort.TryParse(codeInput, System.Globalization.NumberStyles.HexNumber, null, out var code))
    {
        Console.WriteLine("UA: Невірний hex-код. / EN: Invalid hex code.");
        return;
    }

    var report = new DiagnosticReport("GlyphPixelDump");
    GlyphPixelDumpCommand.Run(report, picked, fontBaseName, texturePageName, code);
    report.Save();

    await Task.CompletedTask;
}

async Task RunGlyphAtlasStyleReport()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано. / EN: BF1 file not selected."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано. / EN: BF2 file not selected."); return; }

    var report = new DiagnosticReport("GlyphAtlasStyleReport");
    GlyphAtlasStyleReportCommand.RunBoth(report, bf1Path, bf2Path);
    report.Save();

    await Task.CompletedTask;
}

async Task RunPixelConversionRoundTripCheck()
{
    var report = new DiagnosticReport("PixelConversionRoundTrip");
    PixelConversionRoundTripCommand.RunExhaustiveCheck(report);

    report.Log();
    Console.Write("Перевірити ще й на реальних гліфах з файлів? (y/n): ");
    if (Console.ReadLine()?.Trim().ToLowerInvariant() != "y")
    {
        report.Save();
        return;
    }

    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); report.Save(); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); report.Save(); return; }

    // UA: Внутрішній цикл — файли обрано один раз, перевірку можна
    //     повторювати скільки завгодно (напр. одразу M, ., і кілька
    //     кириличних донорів), поки не введеш порожній рядок замість коду.
    // EN: Inner loop — files chosen once, the check can be repeated as
    //     many times as needed (e.g. M, ., and several Cyrillic donors
    //     right away), until an empty line is entered instead of a code.
    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("Введи: ім'я_шрифту ім'я_сторінки код_hex (через пробіл), або порожній рядок щоб вийти.");
        Console.WriteLine("Напр.: gamefont_super_tiny gamefont_super_tiny_tex0 4D");
        Console.Write("> ");
        var line = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(line))
            break;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 ||
            !ushort.TryParse(parts[2], System.Globalization.NumberStyles.HexNumber, null, out var code))
        {
            Console.WriteLine("UA: Формат: ім'я_шрифту ім'я_сторінки код_hex. Спробуй ще раз.");
            continue;
        }

        PixelConversionRoundTripCommand.RunRealDataCheckBoth(report, bf1Path, bf2Path, parts[0], parts[1], code);
    }

    report.Save();
    await Task.CompletedTask;
}

async Task RunFontCandidatePreview()
{
    var report = new DiagnosticReport("FontCandidatePreview");
    FontCandidatePreviewCommand.Run(report);
    report.Save();

    await Task.CompletedTask;
}

async Task RunExportAllFontStylesContactSheet()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var report = new DiagnosticReport("FontStylesContactSheet");
    ExportAllFontStylesContactSheetCommand.Run(report, bf1Path, "BF1");
    ExportAllFontStylesContactSheetCommand.Run(report, bf2Path, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunFontGlyphCodeRange()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("FontGlyphCodeRange");
    FontGlyphCodeRangeCommand.RunAll(report, bf1Root, "BF1");
    report.Log();
    FontGlyphCodeRangeCommand.RunAll(report, bf2Root, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunFontResourceIdentityCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("FontResourceIdentityCheck");
    // UA: Прицільно gamefont_tiny vs gamefont_super_tiny — саме ця пара
    //     дала ідентичний результат у GlyphDonorAssignmentPreviewCommand
    //     для BF2. Перевіряємо ОБИДВІ гри для повноти (BF1 могла давати
    //     різні результати саме тому, що ресурси різні — це теж треба
    //     підтвердити фактом, а не мовчки припустити).
    // EN: Specifically gamefont_tiny vs gamefont_super_tiny — this exact
    //     pair produced an identical result in
    //     GlyphDonorAssignmentPreviewCommand for BF2. Checking BOTH games
    //     for completeness (BF1 may have differed precisely because the
    //     resources differ — that also needs confirming with a fact, not
    //     a silent assumption).
    FontResourceIdentityCheckCommand.Run(report, bf1Root, "BF1", "gamefont_tiny", "gamefont_super_tiny");
    report.Log();
    FontResourceIdentityCheckCommand.Run(report, bf2Root, "BF2", "gamefont_tiny", "gamefont_super_tiny");
    report.Save();

    await Task.CompletedTask;
}

async Task RunGlyphOrientationCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("GlyphOrientationCheck");

    report.Log();
    GlyphOrientationCheckCommand.PrintAvailable(report, bf1Root, "BF1");
    GlyphOrientationCheckCommand.PrintAvailable(report, bf2Root, "BF2");

    // UA: Будуємо КОНКРЕТНИЙ, копійовуваний приклад команди з РЕАЛЬНИХ
    //     даних щойно надрукованого списку (перший шрифт/сторінка з BF1),
    //     а не абстрактну пораду типу "обери асиметричний символ". Символ
    //     'L' (0x4C) обраний як асиметричний за формою, але шрифт/сторінка —
    //     фактично існуючі в цьому конкретному файлі.
    // EN: Build a CONCRETE, copy-pasteable example command from the REAL
    //     data of the list just printed (first font/page from BF1), instead
    //     of an abstract suggestion like "pick an asymmetric character". The
    //     character 'L' (0x4C) is chosen for its asymmetric shape, but the
    //     font/page are actually present in this specific file.
    var exampleFonts = FontChunkLocator.FindAll(bf1Root);
    var exampleFont = exampleFonts.FirstOrDefault();
    var examplePage = exampleFont?.TexturePages.FirstOrDefault();
    var exampleCommand = exampleFont is not null && examplePage is not null
        ? $"{exampleFont.BaseName} {examplePage.Name} 4C"
        : null;

    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("Введи: ім'я_шрифту ім'я_сторінки код_hex (зі списку вище), або порожній рядок щоб вийти.");
        if (exampleCommand is not null)
            Console.WriteLine($"Приклад (можна скопіювати як є, 'L'=0x4C — асиметрична форма): {exampleCommand}");
        Console.Write("> ");
        var line = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(line))
            break;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 ||
            !ushort.TryParse(parts[2], System.Globalization.NumberStyles.HexNumber, null, out var code))
        {
            Console.WriteLine("UA: Формат: ім'я_шрифту ім'я_сторінки код_hex. Спробуй ще раз.");
            continue;
        }

        GlyphOrientationCheckCommand.Run(report, bf1Root, "BF1", parts[0], parts[1], code);
        report.Log();
        GlyphOrientationCheckCommand.Run(report, bf2Root, "BF2", parts[0], parts[1], code);
    }

    report.Save();
    await Task.CompletedTask;
}

async Task RunGlyphSizeConsistencyCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("GlyphSizeConsistency");
    report.Log();
    GlyphSizeConsistencyCommand.Run(report, bf1Root, "BF1");
    report.Log();
    GlyphSizeConsistencyCommand.Run(report, bf2Root, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunGlyphRectOverlapCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("GlyphRectOverlap");
    report.Log();
    GlyphRectOverlapCommand.Run(report, bf1Root, "BF1");
    report.Log();
    GlyphRectOverlapCommand.Run(report, bf2Root, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunGlyphOverlapRiskCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("GlyphOverlapRisk");
    report.Log();
    await GlyphOverlapRiskCommand.Run(report, bf1Root, bf1Path, "BF1");
    report.Log();
    await GlyphOverlapRiskCommand.Run(report, bf2Root, bf2Path, "BF2");
    report.Save();
}

async Task RunPerFontSafeDonorCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("PerFontSafeDonor");
    report.Log();
    await PerFontSafeDonorCommand.Run(report, bf1Root, bf1Path, "BF1", SoftDonorAnalysis.NeededGlyphCodes);
    report.Log();
    await PerFontSafeDonorCommand.Run(report, bf2Root, bf2Path, "BF2", SoftDonorAnalysis.NeededGlyphCodes);
    report.Save();
}

async Task RunGlyphPageAssignmentCheck()
{
    Console.Write("Виберіть шрифт для перевірки: 1=BF1, 2=BF2 / Choose which game's font to check: 1=BF1, 2=BF2: ");
    var gameChoice = Console.ReadLine()?.Trim();
    var promptLabel = gameChoice == "2" ? "BF2 (Classic)" : "BF1 (Classic 2004)";

    var picked = (gameChoice == "2" ? PickBf2CoreLvl() : PickBf1CoreLvl());
    if (picked is null) { Console.WriteLine("UA: Файл не вибрано. / EN: No file selected."); return; }

    var report = new DiagnosticReport("GlyphPageAssignment");

    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("Введи: ім'я_шрифту код_hex (напр. gamefont_medium 4D), або порожній рядок щоб вийти.");
        Console.WriteLine("Enter: font_base_name code_hex (e.g. gamefont_medium 4D), or empty line to exit.");
        Console.Write("> ");
        var line = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(line)) break;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !ushort.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out var code))
        {
            Console.WriteLine("UA: Формат: ім'я_шрифту код_hex. Спробуй ще раз. / EN: Format: font_base_name code_hex. Try again.");
            continue;
        }

        GlyphPageAssignmentCheckCommand.Run(report, picked, parts[0], code);
    }

    report.Save();
    await Task.CompletedTask;
}

async Task RunFontResourceTreeDump()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("FontResourceTreeDump");
    report.Log();
    FontResourceTreeDumpCommand.RunAll(report, bf1Root, "BF1", verbose: false);
    report.Log();
    FontResourceTreeDumpCommand.RunAll(report, bf2Root, "BF2", verbose: false);

    // UA: Якщо десь знайдеться >1 FBOD — запропонувати детальний
    //     verbose-дамп саме ЦЬОГО шрифту, а не вгадувати наперед, який
    //     варто показати повністю.
    // EN: If anywhere >1 FBOD is found — offer a detailed verbose dump
    //     of exactly THAT font, rather than guessing upfront which one
    //     to show in full.
    Console.WriteLine();
    Console.Write("Показати повне дерево для конкретного шрифту? Введи ім'я або порожній рядок щоб пропустити: ");
    var fontName = Console.ReadLine()?.Trim();
    if (!string.IsNullOrEmpty(fontName))
    {
        report.Log();
        report.Log("--- BF1 ---");
        FontResourceTreeDumpCommand.RunAll(report, bf1Root, "BF1", verbose: true);
    }

    report.Save();
    await Task.CompletedTask;
}

async Task RunReservedByteAnalysis()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("ReservedByteAnalysis");
    report.Log();
    ReservedByteAnalysisCommand.RunAll(report, bf1Root, "BF1");
    report.Log();
    ReservedByteAnalysisCommand.RunAll(report, bf2Root, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunPageIndexByteCorrelation()
{
    Console.Write("Гра: 1=BF1, 2=BF2 / Game: 1=BF1, 2=BF2: ");
    var gameChoice = Console.ReadLine()?.Trim();
    var promptLabel = gameChoice == "2" ? "BF2 (Classic)" : "BF1 (Classic 2004)";

    var picked = (gameChoice == "2" ? PickBf2CoreLvl() : PickBf1CoreLvl());
    if (picked is null) { Console.WriteLine("UA: Файл не вибрано."); return; }

    Console.Write("Ім'я шрифту (раджу gamefont_medium — 4 сторінки, найбільше шансів помітити помилку): ");
    var fontBaseName = Console.ReadLine()?.Trim() ?? "";

    var report = new DiagnosticReport("PageIndexByteCorrelation");
    PageIndexByteCorrelationCommand.Run(report, picked, fontBaseName);
    report.Save();

    await Task.CompletedTask;
}

async Task RunGlyphPageGroupingOverlapCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("GlyphPageGroupingOverlap");
    report.Log();
    GlyphPageGroupingOverlapCommand.RunAll(report, bf1Root, "BF1");
    report.Log();
    GlyphPageGroupingOverlapCommand.RunAll(report, bf2Root, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunGlyphVerticalGeometryProbe()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("GlyphVerticalGeometryProbe");
    GlyphVerticalGeometryProbeCommand.Run(report, bf1Root, "BF1");
    GlyphVerticalGeometryProbeCommand.Run(report, bf2Root, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunUcfbWriteRoundTripCheck()
{
    Console.Write("Гра: 1=BF1, 2=BF2 / Game: 1=BF1, 2=BF2: ");
    var gameChoice = Console.ReadLine()?.Trim();
    var promptLabel = gameChoice == "2" ? "BF2 (Classic)" : "BF1 (Classic 2004)";

    var picked = (gameChoice == "2" ? PickBf2CoreLvl() : PickBf1CoreLvl());
    if (picked is null) { Console.WriteLine("UA: Файл не вибрано."); return; }

    Console.Write("Ім'я шрифту (напр. gamefont_medium): ");
    var fontBaseName = Console.ReadLine()?.Trim() ?? "";
    Console.Write("Ім'я текстурної сторінки (напр. gamefont_medium_tex0): ");
    var texturePageName = Console.ReadLine()?.Trim() ?? "";

    var report = new DiagnosticReport("UcfbWriteRoundTrip");
    UcfbWriteRoundTripCommand.Run(report, picked, fontBaseName, texturePageName);
    report.Save();

    await Task.CompletedTask;
}

async Task RunUcfbFileSizeDiscrepancyCheck()
{
    var picked = NativeFileDialog.ShowOpenDialog("Виберіть core.lvl / Select core.lvl");
    if (picked is null) { Console.WriteLine("UA: Файл не вибрано."); return; }

    var report = new DiagnosticReport("UcfbFileSizeDiscrepancy");
    UcfbFileSizeDiscrepancyCommand.Run(report, picked);
    report.Save();

    await Task.CompletedTask;
}

async Task RunUcfbWriterNoOpRoundTripCheck()
{
    var picked = NativeFileDialog.ShowOpenDialog("Виберіть core.lvl / Select core.lvl (файл НЕ буде змінено)");
    if (picked is null) { Console.WriteLine("UA: Файл не вибрано."); return; }

    var report = new DiagnosticReport("UcfbWriterNoOpRoundTrip");
    UcfbWriterNoOpRoundTripCommand.Run(report, picked);
    report.Save();

    await Task.CompletedTask;
}

async Task RunUcfbStructuralTreeDiff()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var report = new DiagnosticReport("UcfbStructuralTreeDiff");
    report.Log();
    UcfbStructuralTreeDiffCommand.Run(report, bf1Path, "BF1");
    report.Log();
    UcfbStructuralTreeDiffCommand.Run(report, bf2Path, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunContainerSlackSpaceCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var report = new DiagnosticReport("ContainerSlackSpace");
    report.Log();
    ContainerSlackSpaceCommand.Run(report, bf1Path, "BF1");
    report.Log();
    ContainerSlackSpaceCommand.Run(report, bf2Path, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunSoundDataMisparseHexDump()
{
    var picked = NativeFileDialog.ShowOpenDialog("Виберіть core.lvl / Select core.lvl");
    if (picked is null) { Console.WriteLine("UA: Файл не вибрано."); return; }

    var report = new DiagnosticReport("SoundDataMisparseHexDump");
    SoundDataMisparseHexDumpCommand.Run(report, picked);
    report.Save();

    await Task.CompletedTask;
}

async Task RunFourCCPhantomChildScan()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var report = new DiagnosticReport("FourCCPhantomChildScan");
    report.Log();
    FourCCPhantomChildScanCommand.Run(report, bf1Path, "BF1");
    report.Log();
    FourCCPhantomChildScanCommand.Run(report, bf2Path, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunFontBodyPhantomChildCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("FontBodyPhantomChildCheck");
    report.Log();
    FontBodyPhantomChildCheckCommand.Run(report, bf1Root, "BF1");
    report.Log();
    FontBodyPhantomChildCheckCommand.Run(report, bf2Root, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunPipeInfoHexDump()
{
    var picked = NativeFileDialog.ShowOpenDialog("Виберіть core.lvl / Select core.lvl");
    if (picked is null) { Console.WriteLine("UA: Файл не вибрано."); return; }

    var report = new DiagnosticReport("PipeInfoHexDump");
    PipeInfoHexDumpCommand.Run(report, picked);
    report.Save();

    await Task.CompletedTask;
}

async Task RunInfoChunkExhaustiveCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var report = new DiagnosticReport("InfoChunkExhaustiveCheck");
    report.Log();
    InfoChunkExhaustiveCheckCommand.Run(report, bf1Path, "BF1");
    report.Log();
    InfoChunkExhaustiveCheckCommand.Run(report, bf2Path, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunSoftDonorGeometryCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("SoftDonorGeometryCheck");
    await SoftDonorGeometryCheckCommand.Run(report, bf1Root, bf1Path, "BF1", SoftDonorAnalysis.NeededGlyphCodes);
    report.Log();
    await SoftDonorGeometryCheckCommand.Run(report, bf2Root, bf2Path, "BF2", SoftDonorAnalysis.NeededGlyphCodes);
    report.Save();
}

async Task RunSafeDonorGeometryDistribution()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("SafeDonorGeometryDistribution");
    await SafeDonorGeometryDistributionCommand.Run(report, bf1Root, bf1Path, "BF1", SoftDonorAnalysis.NeededGlyphCodes);
    report.Log();
    await SafeDonorGeometryDistributionCommand.Run(report, bf2Root, bf2Path, "BF2", SoftDonorAnalysis.NeededGlyphCodes);
    report.Save();
}

async Task RunGlyphDonorAssignmentPreview()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    const string fontFamilyName = "FiraSans-SemiBold.ttf"; // UA/EN: значення — ВІДНОСНИЙ ШЛЯХ ФАЙЛУ, не назва родини (GDI+ обрізає/зливає family-назви, FONT_FORMAT_SPEC.md 11.13) / the value is a RELATIVE FILE PATH, not a family name (GDI+ truncates/merges family names, FONT_FORMAT_SPEC.md 11.13)

    var report = new DiagnosticReport("GlyphDonorAssignmentPreview");
    await GlyphDonorAssignmentPreviewCommand.Run(report, bf1Root, bf1Path, "BF1", fontFamilyName, SoftDonorAnalysis.NeededGlyphCodes);
    await GlyphDonorAssignmentPreviewCommand.Run(report, bf2Root, bf2Path, "BF2", fontFamilyName, SoftDonorAnalysis.NeededGlyphCodes);
    report.Save();
}

async Task RunGlyphFitRenderPreview()
{
    Console.Write("Гра: 1=BF1, 2=BF2 / Game: 1=BF1, 2=BF2: ");
    var gameChoice = Console.ReadLine()?.Trim();
    var promptLabel = gameChoice == "2" ? "BF2 (Classic)" : "BF1 (Classic 2004)";

    var picked = (gameChoice == "2" ? PickBf2CoreLvl() : PickBf1CoreLvl());
    if (picked is null) { Console.WriteLine("UA: Файл не вибрано."); return; }

    Console.Write("Базове ім'я шрифту (напр. gamefont_large) / Font base name: ");
    var fontBaseName = Console.ReadLine()?.Trim() ?? "";

    var root = UcfbReader.ReadFile(picked);

    const string fontFamilyName = "FiraSans-SemiBold.ttf"; // UA/EN: значення — ВІДНОСНИЙ ШЛЯХ ФАЙЛУ, не назва родини (GDI+ обрізає/зливає family-назви, FONT_FORMAT_SPEC.md 11.13) / the value is a RELATIVE FILE PATH, not a family name (GDI+ truncates/merges family names, FONT_FORMAT_SPEC.md 11.13)

    var report = new DiagnosticReport("GlyphFitRenderPreview");
    await GlyphFitRenderPreviewCommand.Run(report, root, picked, gameChoice == "2" ? "BF2" : "BF1", fontBaseName, fontFamilyName, SoftDonorAnalysis.NeededGlyphCodes);
    report.Save();
}

async Task RunDonorSlotGrowthPotential()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("DonorSlotGrowthPotential");
    await DonorSlotGrowthPotentialCommand.Run(report, bf1Root, bf1Path, "BF1", SoftDonorAnalysis.NeededGlyphCodes);
    await DonorSlotGrowthPotentialCommand.Run(report, bf2Root, bf2Path, "BF2", SoftDonorAnalysis.NeededGlyphCodes);
    report.Save();
}

async Task RunGlyphOccupancyOverlay()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("GlyphOccupancyOverlay");
    GlyphOccupancyOverlayCommand.Run(report, bf1Root, "BF1");
    GlyphOccupancyOverlayCommand.Run(report, bf2Root, "BF2");
    report.Save();

    await Task.CompletedTask;
}

// UA: RunGlyphOccupancyOverlay (вище) і RunGlyphOccupancyOverlayOnOutput
//     (нижче) обидва мають "зашитий" вибір файлу: перший мовчки бере
//     reference-files\{Гра}\core.lvl, ЯКЩО він існує (а він майже завжди
//     існує — це вхідний файл для самої генерації) — і НІКОЛИ не показує
//     діалог вибору в цьому випадку (PickDefaultOrDialogCoreLvl); другий
//     жорстко читає лише output\{Гра}\... (тека ДОНОРСЬКОГО легасі-
//     конвеєра), а не output-nodonor\ (тека, куди РЕАЛЬНО пише поточний
//     no-donor генератор) чи GUI-шну output\ (інший .exe, інша тека).
//     Тобто ЖОДЕН із двох існуючих пунктів не дає оглянути ДОВІЛЬНИЙ
//     конкретний файл, встановлений у гру (no-donor результат, або файл,
//     збережений через GUI). Цей варіант — БЕЗ жодного дефолту, діалог
//     вибору файлу показується ЗАВЖДИ, для обох ігор — щоб можна було
//     вказати САМЕ той core.lvl, що стоїть у грі зараз.
// EN: RunGlyphOccupancyOverlay (above) and RunGlyphOccupancyOverlayOnOutput
//     (below) both have a "hardcoded" file choice: the first silently
//     takes reference-files\{Game}\core.lvl IF it exists (and it almost
//     always does — it's the generation's own input file) — and NEVER
//     shows a picker dialog in that case (PickDefaultOrDialogCoreLvl); the
//     second hard-codes reading only output\{Game}\... (the LEGACY donor
//     pipeline's folder), not output-nodonor\ (where the CURRENT no-donor
//     generator actually writes) or the GUI's own output\ (a different
//     .exe, a different folder). So NEITHER existing item lets you inspect
//     an ARBITRARY specific file installed in the game (the no-donor
//     result, or a file saved via the GUI). This variant has NO default at
//     all — the file picker always shows, for both games — so you can
//     point it at whichever core.lvl is actually in the game right now.
async Task RunGlyphOccupancyOverlayManualPick()
{
    Console.WriteLine("UA: Оберіть core.lvl BF1 (той САМЕ файл, що зараз стоїть у грі / тестувався). / " +
                       "EN: Pick the BF1 core.lvl (the EXACT file currently in the game / being tested).");
    var bf1Path = NativeFileDialog.ShowOpenDialog("Виберіть core.lvl для BF1 / Select core.lvl for BF1");
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    Console.WriteLine("UA: Оберіть core.lvl BF2 (той САМЕ файл, що зараз стоїть у грі / тестувався). / " +
                       "EN: Pick the BF2 core.lvl (the EXACT file currently in the game / being tested).");
    var bf2Path = NativeFileDialog.ShowOpenDialog("Виберіть core.lvl для BF2 / Select core.lvl for BF2");
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("GlyphOccupancyOverlay_ManualPick");
    GlyphOccupancyOverlayCommand.Run(report, bf1Root, "BF1");
    GlyphOccupancyOverlayCommand.Run(report, bf2Root, "BF2");
    report.Save();

    await Task.CompletedTask;
}

// UA: READ-ONLY — рахує й друкує ті самі числа моделі "ядро+виступ", що
//     реально піде в GenerateNoDonorCyrillicCoreCommand (та сама
//     ResolveFontFamilyName), але без жодного запису файлу чи атласу. За
//     замовчуванням бере reference-files\{Гра}\core.lvl (як і сама
//     генерація) — саме звідти беруться реальні Bearing/CellHeight
//     англійських гліфів, від яких залежить уся модель, тож дефолтний
//     пікер тут коректний (на відміну від occupancy overlay вище, де
//     дефолт заважав побачити ЗГЕНЕРОВАНИЙ файл).
// EN: READ-ONLY — computes and prints the exact same "core+extension"
//     model numbers that would actually go into
//     GenerateNoDonorCyrillicCoreCommand (the same ResolveFontFamilyName),
//     but with no file or atlas write at all. Defaults to
//     reference-files\{Game}\core.lvl (same as generation itself) — that's
//     where the real English glyphs' Bearing/CellHeight the whole model
//     depends on actually live, so the default picker is correct here
//     (unlike the occupancy overlay above, where the default got in the
//     way of seeing the GENERATED file).
async Task RunLowercaseCoreMarginPreview()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var report = new DiagnosticReport("LowercaseCoreMarginPreview");
    LowercaseCoreMarginPreviewCommand.Run(report, bf1Path, "BF1");
    LowercaseCoreMarginPreviewCommand.Run(report, bf2Path, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunTargetedGrowthDonorAssignmentPreview()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    const string fontFamilyName = "FiraSans-SemiBold.ttf"; // UA/EN: значення — ВІДНОСНИЙ ШЛЯХ ФАЙЛУ, не назва родини (GDI+ обрізає/зливає family-назви, FONT_FORMAT_SPEC.md 11.13) / the value is a RELATIVE FILE PATH, not a family name (GDI+ truncates/merges family names, FONT_FORMAT_SPEC.md 11.13)

    var report = new DiagnosticReport("TargetedGrowthDonorAssignmentPreview");
    await TargetedGrowthDonorAssignmentPreviewCommand.Run(report, bf1Root, bf1Path, "BF1", fontFamilyName, SoftDonorAnalysis.NeededGlyphCodes);
    await TargetedGrowthDonorAssignmentPreviewCommand.Run(report, bf2Root, bf2Path, "BF2", fontFamilyName, SoftDonorAnalysis.NeededGlyphCodes);
    report.Save();
}

async Task RunFontGlyphMetricsCorrelation()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("FontGlyphMetricsCorrelation");
    FontGlyphMetricsCorrelationCommand.Run(report, bf1Root, "BF1");
    FontGlyphMetricsCorrelationCommand.Run(report, bf2Root, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunGenerateLocalizedCore()
{
    Console.Write("Гра: 1=BF1, 2=BF2, 3=обидві / Game: 1=BF1, 2=BF2, 3=both: ");
    var gameChoice = Console.ReadLine()?.Trim();

    Console.WriteLine("UA: УВАГА — ця дія створить/перезапише core.lvl у теці \"output\" біля .exe (оригінал не змінюється).");
    Console.WriteLine("EN: WARNING — this creates/overwrites core.lvl in the \"output\" folder next to the .exe (original is not modified).");
    Console.Write("Продовжити? y/n: ");
    if (Console.ReadLine()?.Trim().ToLowerInvariant() != "y")
    {
        Console.WriteLine("UA: Скасовано.");
        return;
    }

    if (gameChoice is "1" or "3")
        await GenerateOneGame("BF1", PickBf1CoreLvl());

    if (gameChoice is "2" or "3")
        await GenerateOneGame("BF2", PickBf2CoreLvl());
}

async Task RunWordRenderInspect()
{
    // UA: БЕЗ вибору гри/шрифту — завжди ОБИДВІ гри й УСІ 5 gamefont_*,
    //     той самий список, що й GenerateOneGame використовує для самої
    //     ін'єкції (не вигаданий тут заново): вибіркова перевірка лише
    //     частини комбінацій могла б пропустити регресію в тих, що
    //     лишились неперевіреними. Якщо для якоїсь гри output ще не
    //     згенеровано — WordRenderInspectCommand.Run про це прямо скаже й
    //     пропустить, не впаде.
    // EN: NO game/font selection — always BOTH games and ALL 5
    //     gamefont_*, the SAME list GenerateOneGame uses for the actual
    //     injection (not reinvented here): checking only a subset of
    //     combinations could miss a regression in the ones left
    //     unchecked. If a game's output hasn't been generated yet,
    //     WordRenderInspectCommand.Run says so directly and skips it,
    //     doesn't crash.
    string[] fontsToInspect = ["gamefont_large", "gamefont_medium", "gamefont_small", "gamefont_tiny", "gamefont_super_tiny"];
    (string Label, string RelativeGamePath)[] games =
    [
        ("BF1", Path.Combine("Star Wars Battlefront (Classic 2004)", "GameData", "Data", "_LVL_PC")),
        ("BF2", Path.Combine("Star Wars Battlefront II Classic", "GameData", "data", "_lvl_pc")),
    ];

    // UA: ОДНА спільна підпапка на весь цей прогін (не плоска купа PNG у
    //     diagnostic-output) — усі 10 зображень (2 гри × 5 шрифтів)
    //     лежать разом.
    // EN: ONE shared subfolder for this whole run (not a flat pile of
    //     PNGs in diagnostic-output) — all 10 images (2 games × 5 fonts)
    //     sit together.
    var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    var runOutputDir = Path.Combine(AppContext.BaseDirectory, "diagnostic-output", $"WordRenderInspect_{stamp}");

    var report = new DiagnosticReport("WordRenderInspect");
    foreach (var (gameLabel, relativeGamePath) in games)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "output", relativeGamePath, "core.lvl");
        foreach (var fontBaseName in fontsToInspect)
            WordRenderInspectCommand.Run(report, outputPath, gameLabel, fontBaseName, runOutputDir);
    }
    report.Save();

    Console.WriteLine($"UA: Усі зображення (обидві гри, усі шрифти) — у: {runOutputDir}");
    Console.WriteLine($"EN: All images (both games, all fonts) — in: {runOutputDir}");

    await Task.CompletedTask;
}

async Task RunGlyphOccupancyOverlayOnOutput()
{
    // UA: GlyphOccupancyOverlayCommand дає реальні пікселі ВСІЄЇ сторінки
    //     (англійські й кириличні гліфи РАЗОМ, у їхньому справжньому
    //     просторовому контексті) + контур КОЖНОГО гліфа з FBOD, без
    //     жодної додаткової композиції/припущень про вирівнювання (на
    //     відміну від WordRenderInspectCommand, який сам вирізає й
    //     вирівнює літери по одній вигаданій лінії) — це дає пряме
    //     порівняння параметрів між англійським і вставленим українським
    //     гліфом. Тут той самий виклик, той самий код, що й
    //     RunGlyphOccupancyOverlay (категорія 3), лише на ВЖЕ
    //     ЗГЕНЕРОВАНОМУ output (обидві гри, автоматично обчислений шлях,
    //     як і в RunWordRenderInspect).
    // EN: GlyphOccupancyOverlayCommand provides real pixels of the WHOLE
    //     page (English and Cyrillic glyphs TOGETHER, in their real
    //     spatial context) + EVERY glyph's outline from FBOD, with no
    //     extra composition/alignment assumptions (unlike
    //     WordRenderInspectCommand, which crops and aligns letters to one
    //     made-up line itself) — giving a direct comparison of parameters
    //     between an English glyph and its inserted Ukrainian counterpart.
    //     This is the SAME call, the SAME code, as RunGlyphOccupancyOverlay
    //     (category 3), just on the ALREADY-GENERATED output (both games,
    //     path computed automatically, same as RunWordRenderInspect).
    (string Label, string RelativeGamePath)[] games =
    [
        ("BF1", Path.Combine("Star Wars Battlefront (Classic 2004)", "GameData", "Data", "_LVL_PC")),
        ("BF2", Path.Combine("Star Wars Battlefront II Classic", "GameData", "data", "_lvl_pc")),
    ];

    // UA: Підпапка на ЦЕЙ запуск (той самий принцип, що й для
    //     WordRenderInspectCommand) — усі зображення цього прогону лежать
    //     разом, а не плоскою купою в diagnostic-output, де десятки PNG
    //     важко переглянути.
    // EN: Subfolder for THIS run (same principle as for
    //     WordRenderInspectCommand) — all images from this run sit
    //     together, instead of a flat pile in diagnostic-output where
    //     dozens of PNGs are hard to review.
    var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    var runOutputDir = Path.Combine(AppContext.BaseDirectory, "diagnostic-output", $"GlyphOccupancyOverlay_Output_{stamp}");
    Directory.CreateDirectory(runOutputDir);

    var report = new DiagnosticReport("GlyphOccupancyOverlay_Output");
    foreach (var (label, relativeGamePath) in games)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "output", relativeGamePath, "core.lvl");
        if (!File.Exists(outputPath))
        {
            report.Log($"UA: [{label}] Файл не знайдено: {outputPath} — спершу згенеруй (7 → 1).");
            report.Log($"EN: [{label}] File not found: {outputPath} — generate it first (7 → 1).");
            continue;
        }

        var root = UcfbReader.ReadFile(outputPath);
        GlyphOccupancyOverlayCommand.Run(report, root, label, runOutputDir);
    }
    report.Save();

    await Task.CompletedTask;
}

async Task RunCellHeightHypothesisTest()
{
    // UA: БЕЗ вибору гри/шрифту — завжди ОБИДВІ гри, той самий шлях-
    //     паттерн, що й в RunGlyphOccupancyOverlayOnOutput: вибіркова
    //     перевірка лише однієї гри могла б пропустити регресію в іншій.
    //     CellHeightHypothesisTestCommand сам іде по УСІХ шрифтах
    //     (FontChunkLocator.FindAll) усередині кожної гри — тут нема чого
    //     звужувати.
    // EN: NO game/font selection — always BOTH games, the same path
    //     pattern as RunGlyphOccupancyOverlayOnOutput: checking only one
    //     game could miss a regression in the other.
    //     CellHeightHypothesisTestCommand itself walks ALL fonts
    //     (FontChunkLocator.FindAll) inside each game — nothing to narrow
    //     here.
    (string Label, string RelativeGamePath)[] games =
    [
        ("BF1", Path.Combine("Star Wars Battlefront (Classic 2004)", "GameData", "Data", "_LVL_PC")),
        ("BF2", Path.Combine("Star Wars Battlefront II Classic", "GameData", "data", "_lvl_pc")),
    ];

    var report = new DiagnosticReport("CellHeightHypothesisTest");
    foreach (var (label, relativeGamePath) in games)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "output", relativeGamePath, "core.lvl");
        var testOutputPath = Path.Combine(AppContext.BaseDirectory, "output-celltest", relativeGamePath, "core.lvl");
        CellHeightHypothesisTestCommand.Run(report, outputPath, testOutputPath, label);
    }
    report.Save();

    await Task.CompletedTask;
}

async Task RunMetricBaselineTest()
{
    // UA: БЕЗ вибору гри/шрифту — обидві гри, усі шрифти всередині
    //     (MetricBaselineTestCommand сам іде по FontChunkLocator.FindAll),
    //     той самий шлях-паттерн, що й решта output-команд.
    // EN: NO game/font selection — both games, all fonts inside
    //     (MetricBaselineTestCommand walks FontChunkLocator.FindAll
    //     itself), same path pattern as the other output commands.
    (string Label, string RelativeGamePath)[] games =
    [
        ("BF1", Path.Combine("Star Wars Battlefront (Classic 2004)", "GameData", "Data", "_LVL_PC")),
        ("BF2", Path.Combine("Star Wars Battlefront II Classic", "GameData", "data", "_lvl_pc")),
    ];

    var report = new DiagnosticReport("MetricBaselineTest");
    foreach (var (label, relativeGamePath) in games)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "output", relativeGamePath, "core.lvl");
        var testOutputPath = Path.Combine(AppContext.BaseDirectory, "output-metrictest", relativeGamePath, "core.lvl");
        MetricBaselineTestCommand.Run(report, outputPath, testOutputPath, label);
    }
    report.Save();

    await Task.CompletedTask;
}

// ===========================================================================
// UA: shell.lvl/ingame.lvl шукаються за тим самим "reference-files"-
//     принципом, що й core.lvl (PickDefaultOrDialogCoreLvl) — покласти
//     файли у "{тека exe}\reference-files\BF2\shell.lvl" і "...\ingame.lvl"
//     один раз, і меню саме їх підхоплює. Це BF2-специфічна фіча за
//     задумом (BF1 використовує Lua 4.0 і взагалі інший шлях меню), на
//     відміну від шрифтового пайплайна, де обидві гри обов'язкові.
// EN: shell.lvl/ingame.lvl are looked up via the SAME "reference-files"
//     convention as core.lvl (PickDefaultOrDialogCoreLvl) — drop the
//     files into "{exe folder}\reference-files\BF2\shell.lvl" and
//     "...\ingame.lvl" once, and the menu picks them up automatically.
//     This is a BF2-specific feature by design (BF1 uses Lua 4.0 and an
//     entirely different menu path), unlike the font pipeline where both
//     games are mandatory.
// ===========================================================================
async Task RunAnalyzeShellScriptConstants()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var shellPath = Path.Combine(bf2RefDir, "shell.lvl");
    var ingamePath = Path.Combine(bf2RefDir, "ingame.lvl");

    var report = new DiagnosticReport("AnalyzeShellScriptConstants");
    report.Log("UA: Widescreen-фікс BF2: розбір Lua-байткоду shell.lvl (головне меню) та ingame.lvl (HUD).");
    report.Log("EN: BF2 widescreen fix: shell.lvl (main menu) and ingame.lvl (HUD) Lua bytecode analysis.");
    report.Log($"UA: Якщо файл не знайдено — постав його в \"{bf2RefDir}\" (як для core.lvl).");
    report.Log($"EN: If a file isn't found — place it in \"{bf2RefDir}\" (same as core.lvl).");
    report.Log();

    AnalyzeShellScriptConstantsCommand.Run(report, shellPath, "shell.lvl", "BF2");
    AnalyzeShellScriptConstantsCommand.Run(report, ingamePath, "ingame.lvl", "BF2");

    report.Save();
    await Task.CompletedTask;
}

async Task RunDisassembleScreenLayoutFunctions()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var shellPath = Path.Combine(bf2RefDir, "shell.lvl");
    var ingamePath = Path.Combine(bf2RefDir, "ingame.lvl");

    var report = new DiagnosticReport("DisassembleScreenLayoutFunctions");
    report.Log("UA: Widescreen-фікс BF2: повний дизасемблер ЛИШЕ функцій, що згадують screen/aspect-ключові слова.");
    report.Log("EN: BF2 widescreen fix: full disassembly of ONLY functions mentioning screen/aspect keywords.");
    report.Log();

    DisassembleScreenLayoutFunctionsCommand.Run(report, shellPath, "shell.lvl", "BF2");
    DisassembleScreenLayoutFunctionsCommand.Run(report, ingamePath, "ingame.lvl", "BF2");

    report.Save();
    await Task.CompletedTask;
}

async Task RunTestWidescreenBootstrapPatch()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var shellPath = Path.Combine(bf2RefDir, "shell.lvl");

    var report = new DiagnosticReport("TestWidescreenBootstrapPatch");
    report.Log("UA: Widescreen-фікс BF2: перевірка Lua50BytecodeWriter + ShellEntryPointPatcher (round-trip + dry-run патча).");
    report.Log("EN: BF2 widescreen fix: verification of Lua50BytecodeWriter + ShellEntryPointPatcher (round-trip + patch dry-run).");
    report.Log($"UA: shell.lvl береться з \"{shellPath}\" (як для core.lvl) — якщо нема, тести 2-3 буде пропущено.");
    report.Log($"EN: shell.lvl is taken from \"{shellPath}\" (same as core.lvl) — if missing, tests 2-3 will be skipped.");
    report.Log();

    TestWidescreenBootstrapPatchCommand.Run(report, shellPath);

    report.Save();
    await Task.CompletedTask;
}

async Task RunGenerateWidescreenPatchedShell()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var shellPath = Path.Combine(bf2RefDir, "shell.lvl");
    var outputDir = Path.Combine(AppContext.BaseDirectory, "widescreen-output");

    var report = new DiagnosticReport("GenerateWidescreenPatchedShell");
    report.Log("UA: Widescreen-фікс BF2: генерація РЕАЛЬНОГО патченого shell.lvl на диск (для тесту в самій грі).");
    report.Log("EN: BF2 widescreen fix: generating a REAL patched shell.lvl to disk (for testing in the actual game).");
    report.Log($"UA: Вхідний (vanilla) shell.lvl: \"{shellPath}\" — цей файл НЕ змінюється.");
    report.Log($"EN: Input (vanilla) shell.lvl: \"{shellPath}\" — this file is NOT modified.");
    report.Log($"UA: Вихідна тека: \"{outputDir}\"");
    report.Log($"EN: Output folder: \"{outputDir}\"");
    report.Log();

    var outputPath = GenerateWidescreenPatchedShellCommand.Run(report, shellPath, outputDir);

    report.Save();

    if (outputPath is not null)
    {
        report.Log();
        report.Log("UA: === Як протестувати в грі ===");
        report.Log("EN: === How to test in-game ===");
        report.Log("UA: 1. ЗРОБІТЬ РЕЗЕРВНУ КОПІЮ оригінального файлу зі свого встановлення гри:");
        report.Log("EN: 1. BACK UP the original file from your game installation:");
        report.Log(@"UA:    ...\Star Wars Battlefront II Classic\GameData\data\_lvl_pc\shell.lvl -> shell.lvl.backup");
        report.Log(@"EN:    ...\Star Wars Battlefront II Classic\GameData\data\_lvl_pc\shell.lvl -> shell.lvl.backup");
        report.Log($"UA: 2. Скопіюйте \"{outputPath}\" на місце оригінального shell.lvl.");
        report.Log($"EN: 2. Copy \"{outputPath}\" over the original shell.lvl.");
        report.Log("UA: 3. Запустіть гру, перейдіть у головне меню — перевірте, чи не зникли/не зламались екрани (особливо ті, що використовують NewIFContainer).");
        report.Log("EN: 3. Launch the game, go to the main menu — check that no screens vanished/broke (especially ones using NewIFContainer).");
        report.Log("UA: 4. Якщо гра не запускається або меню зламане — поверніть backup-файл на місце.");
        report.Log("EN: 4. If the game fails to launch or the menu is broken — restore the backup file.");
        report.Save();
    }

    await Task.CompletedTask;
}

async Task RunGenerateWidescreenDebugMarkerShell()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var shellPath = Path.Combine(bf2RefDir, "shell.lvl");
    var outputDir = Path.Combine(AppContext.BaseDirectory, "widescreen-output");
    const string outputFileName = "shell_debug_marker.lvl";

    var report = new DiagnosticReport("GenerateWidescreenDebugMarkerShell");
    report.Log("UA: ДІАГНОСТИКА: генерація shell.lvl з БЕЗУМОВНИМ зсувом +300px (замість формули x*W/800) — перевірити, чи NewIFContainer взагалі викликається для конкретного екрана.");
    report.Log("EN: DIAGNOSTIC: generating shell.lvl with an UNCONDITIONAL +300px offset (instead of the x*W/800 formula) — check whether NewIFContainer is even called for a given screen.");
    report.Log($"UA: Вхідний (vanilla) shell.lvl: \"{shellPath}\" — цей файл НЕ змінюється.");
    report.Log($"EN: Input (vanilla) shell.lvl: \"{shellPath}\" — this file is NOT modified.");
    report.Log($"UA: Вихідний файл: \"{Path.Combine(outputDir, outputFileName)}\" (ОКРЕМА назва — не плутати з \"бойовою\" shell.lvl).");
    report.Log($"EN: Output file: \"{Path.Combine(outputDir, outputFileName)}\" (SEPARATE name — don't confuse it with the \"real\" shell.lvl).");
    report.Log();

    var outputPath = GenerateWidescreenPatchedShellCommand.Run(report, shellPath, outputDir, debugMarker: true, outputFileName: outputFileName);

    report.Save();

    if (outputPath is not null)
    {
        report.Log();
        report.Log("UA: === Як протестувати в грі ===");
        report.Log("EN: === How to test in-game ===");
        report.Log("UA: 1. ЗРОБІТЬ РЕЗЕРВНУ КОПІЮ оригінального файлу зі свого встановлення гри (якщо ще не зроблено):");
        report.Log("EN: 1. BACK UP the original file from your game installation (if not already done):");
        report.Log(@"UA:    ...\Star Wars Battlefront II Classic\GameData\data\_lvl_pc\shell.lvl -> shell.lvl.backup");
        report.Log(@"EN:    ...\Star Wars Battlefront II Classic\GameData\data\_lvl_pc\shell.lvl -> shell.lvl.backup");
        report.Log($"UA: 2. Скопіюйте \"{outputPath}\" на місце оригінального shell.lvl (перейменувавши на shell.lvl).");
        report.Log($"EN: 2. Copy \"{outputPath}\" over the original shell.lvl (renaming it to shell.lvl).");
        report.Log("UA: 3. Запустіть гру, дійдіть до екрана SINGLEPLAYER/CAMPAIGN.");
        report.Log("EN: 3. Launch the game, navigate to the SINGLEPLAYER/CAMPAIGN screen.");
        report.Log("UA: 4. Якщо кнопки (Training/Space Overview/...) ЗРУШИЛИСЬ на ~300px — NewIFContainer викликається для цього екрана, і формулу масштабу можна довіряти (проблема була деінде). Якщо НІЧОГО не зрушилось — цей екран будується через ІНШУ функцію (напр. NewButtonWindow), і формулу треба застосовувати саме до неї.");
        report.Log("EN: 4. If the buttons (Training/Space Overview/...) SHIFTED by ~300px — NewIFContainer IS called for this screen, and the scale formula can be trusted (the problem was elsewhere). If NOTHING shifted — this screen is built through a DIFFERENT function (e.g. NewButtonWindow), and the formula needs to target that one instead.");
        report.Log("UA: 5. Поверніть backup-файл на місце після тесту.");
        report.Log("EN: 5. Restore the backup file after testing.");
        report.Save();
    }

    await Task.CompletedTask;
}

async Task RunTestFontResourceRoundTrip()
{
    var report = new DiagnosticReport("TestFontResourceRoundTrip");
    report.Log("UA: Font-writer: round-trip перевірка FontResourceReader + FontResourceBuilder на реальному core.lvl.");
    report.Log("EN: Font-writer: round-trip validation of FontResourceReader + FontResourceBuilder on a real core.lvl.");
    report.Log();

    // UA: Перевіряємо обидві гри, якщо їхні core.lvl лежать у reference-files.
    // EN: Check both games if their core.lvl files are in reference-files.
    foreach (var (game, sub) in new[] { ("BF2", "BF2"), ("BF1", "BF1") })
    {
        var corePath = Path.Combine(AppContext.BaseDirectory, "reference-files", sub, "core.lvl");
        report.Log($"UA: === {game} ===");
        report.Log($"EN: === {game} ===");
        TestFontResourceRoundTripCommand.Run(report, corePath);
        report.Log();
    }

    report.Save();
    await Task.CompletedTask;
}

async Task RunGenerateRepackedFontCore()
{
    var report = new DiagnosticReport("GenerateRepackedFontCore");
    report.Log("UA: Font-writer: перепакування шрифтів у свіжий атлас (валідація packing+UV перед рендером з TTF).");
    report.Log("EN: Font-writer: repack fonts into a fresh atlas (validate packing+UV before TTF rendering).");
    report.Log("UA: Гліфи ті самі (ті самі пікселі/метрики/коди) — змінюється лише розкладка атласу. У грі текст має бути ІДЕНТИЧНИЙ.");
    report.Log("EN: Glyphs are the same (same pixels/metrics/codes) — only the atlas layout changes. In-game text must be IDENTICAL.");
    report.Log();

    foreach (var (game, sub) in new[] { ("BF2", "BF2"), ("BF1", "BF1") })
    {
        var corePath = Path.Combine(AppContext.BaseDirectory, "reference-files", sub, "core.lvl");
        var outputDir = Path.Combine(AppContext.BaseDirectory, "font-output", sub);
        report.Log($"UA: === {game} ===");
        report.Log($"EN: === {game} ===");
        var outputPath = GenerateRepackedFontCoreCommand.Run(report, corePath, outputDir);
        if (outputPath is not null)
        {
            report.Log($"UA:   → скопіюйте \"{outputPath}\" (з backup!) у GameData\\data\\_lvl_pc\\core.lvl і перевірте текст у грі.");
            report.Log($"EN:   → copy \"{outputPath}\" (with a backup!) to GameData\\data\\_lvl_pc\\core.lvl and check text in-game.");
        }
        report.Log();
    }

    report.Save();
    await Task.CompletedTask;
}

async Task RunNewGlyphCodeExperiment()
{
    var report = new DiagnosticReport("NewGlyphCodeExperiment");
    await NewGlyphCodeExperimentCommand.Run(report);
    report.Save();
}

async Task RunFontCandidateComparison()
{
    var report = new DiagnosticReport("FontCandidateComparison");
    await FontCandidateComparisonCommand.Run(report);
    report.Save();
}

// UA: BF2 не підтримує 1080p нативно (на відміну від BF1) — тому шрифти
//     BF2 треба ЗБІЛЬШУВАТИ. Порядок кроків: спершу
//     GenerateEnlargedFontCoreCommand збільшує ВАНІЛЬНИЙ (ще без
//     кирилиці) файл — застосовуючи upscale ЛИШЕ до англійських бітмапів
//     (для них це неминуче: немає векторного джерела гри, лише запечені
//     пікселі). ПОТІМ GenerateNoDonorCyrillicCoreCommand читає ВЖЕ
//     ЗБІЛЬШЕНИЙ файл — його GlyphMetricModel.DeriveReference САМ вимірює
//     baselineOffset/CapHeightGame/CoreHeightGame з РЕАЛЬНИХ (уже
//     збільшених) англійських записів, тож кириличні гліфи рендеряться з
//     Fira Sans ОДРАЗУ під фінальний, великий розмір — ОДИН прохід
//     TTF→піксель, без повторного bicubic-розмиття, яке виникло б, якби
//     кирилицю відрендерити в малий атлас і лише потім масштабувати ВЕСЬ
//     атлас разом. Жодних змін у самій математиці
//     GenerateNoDonorCyrillicCoreCommand не потрібно — вона й так рахує
//     все з вхідного файлу "як є".
// EN: BF2 does NOT support 1080p natively (unlike BF1) — so BF2 fonts
//     need to be ENLARGED. Step order: GenerateEnlargedFontCoreCommand
//     first enlarges the VANILLA (still Cyrillic-free) file — applying
//     the upscale ONLY to the English bitmaps (unavoidable for them: no
//     vector source exists for the game's own font, only baked pixels).
//     THEN GenerateNoDonorCyrillicCoreCommand reads the ALREADY-enlarged
//     file — its GlyphMetricModel.DeriveReference measures baselineOffset/
//     CapHeightGame/CoreHeightGame from the REAL (already-enlarged) English
//     records itself, so Cyrillic glyphs render from Fira Sans DIRECTLY at
//     the final, big size — ONE TTF→pixel pass, with none of the repeated
//     bicubic blur that would result from rendering Cyrillic into a small
//     atlas first and only then upscaling the WHOLE atlas together. No
//     changes to GenerateNoDonorCyrillicCoreCommand's own math are needed
//     — it already measures everything from the input file "as is".
// UA: Патч назви карти аддону Tat3 — робить її звичайним рядком
//     локалізації (див. розлогий заголовок PatchAddOnMapNameCommand).
//     Вхід береться з reference-files\BF1\AddOn\ — тобто з ВАНІЛЬНИХ копій,
//     а не з теки гри: команда нічого не змінює на місці, усе пишеться в
//     окрему теку "output-addon", як і решта генеруючих команд.
// EN: Tat3 add-on map name patch — turns it into an ordinary localization
//     string (see PatchAddOnMapNameCommand's long header). Input comes from
//     reference-files\BF1\AddOn\ — i.e. VANILLA copies, not the game folder:
//     the command changes nothing in place, everything is written into a
//     separate "output-addon" folder, like every other generating command.
async Task RunPatchAddOnMapName()
{
    var report = new DiagnosticReport("PatchAddOnMapName");

    var addOnDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF1", "AddOn");
    var addme = Path.Combine(addOnDir, "addme.script");
    var addOnCore = Path.Combine(addOnDir, "core.lvl");
    var outputDir = Path.Combine(AppContext.BaseDirectory, "output-addon");

    // UA: Шляхи ВІДНОСНО теки гри — за ними ж розкладається вихідна тека,
    //     тож її вміст копіюється в гру «як є», без здогадок «що куди».
    // EN: Paths RELATIVE to the game folder — the output folder is laid out
    //     the same way, so its contents copy into the game "as is", with no
    //     guessing about which file goes where.
    var addmeRel   = Path.Combine("GameData", "AddOn", "Tat3", "addme.script");
    var baseCoreRel = Path.Combine("GameData", "Data", "_LVL_PC", "core.lvl");
    var addOnCoreRel = Path.Combine("GameData", "AddOn", "Tat3", "Data", "_lvl_pc", "core.lvl");

    // UA: БАЗОВИЙ core.lvl — саме він вирішує справу (див. заголовок
    //     PatchAddOnMapNameCommand: шел резолвить ключі проти таблиці
    //     базової гри). Типово пропонуємо вже згенерований кириличний
    //     файл з output-nodonor, бо запускати цю команду треба ПІСЛЯ
    //     пункту 1 (генерація кирилиці) і ПЕРЕД перекладом у GUI — тоді
    //     новий рядок пройде звичайний шлях перекладу разом з рештою.
    // EN: The BASE core.lvl is what actually matters (see
    //     PatchAddOnMapNameCommand's header: the shell resolves keys
    //     against the base game's table). We default to the already
    //     generated Cyrillic file in output-nodonor, because this command
    //     should run AFTER item 1 (Cyrillic generation) and BEFORE
    //     translating in the GUI — so the new string goes through the
    //     normal translation path together with everything else.
    var defaultBase = Path.Combine(AppContext.BaseDirectory, "output-nodonor",
        "Star Wars Battlefront (Classic 2004)", "GameData", "Data", "_LVL_PC", "core.lvl");

    Console.WriteLine("UA: Шлях до БАЗОВОГО core.lvl (Enter = типовий з output-nodonor) /");
    Console.WriteLine("EN: Path to the BASE core.lvl (Enter = default from output-nodonor):");
    Console.WriteLine($"    [{defaultBase}]");
    Console.Write("> ");
    var typed = Console.ReadLine()?.Trim().Trim('"');
    var baseCore = string.IsNullOrWhiteSpace(typed) ? defaultBase : typed;

    report.Log($"UA: Аддон / EN: add-on: {addOnDir}");
    report.Log($"UA: База / EN: base:   {baseCore}");
    report.Log($"UA: Вихід / EN: output: {outputDir}");
    report.Log();

    // UA: Обидва файли: базовий — той, що реально працює; аддонний —
    //     страхувальник (запис там нешкідливий, і покласти в обидва
    //     дешевше, ніж ризикувати помилково визначеною активною таблицею).
    // EN: Both files: the base one is what actually works; the add-on one
    //     is a safety net (the record is harmless there, and covering both
    //     is cheaper than risking a misidentified active table).
    await PatchAddOnMapNameCommand.Run(
        report,
        addme, addmeRel,
        [
            new PatchAddOnMapNameCommand.CoreTarget(baseCore, baseCoreRel),
            new PatchAddOnMapNameCommand.CoreTarget(addOnCore, addOnCoreRel),
        ],
        outputDir);
    report.Save();
}

async Task RunGenerateNoDonorCyrillicCore()
{
    const float bf2EnlargeScale = 1.5f;

    Console.Write("Гра: 1=BF1, 2=BF2, 3=обидві / Game: 1=BF1, 2=BF2, 3=both: ");
    var choice = Console.ReadLine()?.Trim();

    var report = new DiagnosticReport("GenerateNoDonorCyrillicCore");

    if (choice is "1" or "3")
    {
        var bf1 = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF1", "core.lvl");
        await GenerateNoDonorCyrillicCoreCommand.Run(report, bf1, "BF1");
        report.Log();
    }
    if (choice is "2" or "3")
    {
        var bf2Vanilla = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2", "core.lvl");

        report.Log($"UA: [BF2] Крок 1/2 — збільшення ВАНІЛЬНОГО шрифту ×{bf2EnlargeScale} (BF2 не підтримує 1080p нативно, ще без кирилиці). / " +
                   $"EN: [BF2] Step 1/2 — enlarging the VANILLA font ×{bf2EnlargeScale} (BF2 doesn't support 1080p natively, still no Cyrillic).");

        // UA: Тимчасова тека ПОЗА output-nodonor — це лише проміжний вхід
        //     для наступного кроку (Cyrillic-ін'єкції), не фінальний
        //     результат. Прибирається одразу після використання.
        // EN: Temp folder OUTSIDE output-nodonor — this is only an
        //     intermediate input for the next step (Cyrillic injection),
        //     not the final result. Cleaned up right after use.
        var bf2EnlargeTempDir = Path.Combine(AppContext.BaseDirectory, "_enlarge-tmp-bf2");
        var enlargedVanillaPath = GenerateEnlargedFontCoreCommand.Run(report, bf2Vanilla, bf2EnlargeTempDir, bf2EnlargeScale);

        if (enlargedVanillaPath is not null)
        {
            report.Log();
            report.Log("UA: [BF2] Крок 2/2 — свіжий рендер кирилиці (Fira Sans) НАПРЯМУ у вже збільшений атлас. / " +
                       "EN: [BF2] Step 2/2 — fresh Cyrillic render (Fira Sans) DIRECTLY into the already-enlarged atlas.");

            var bf2LocalizedPath = await GenerateNoDonorCyrillicCoreCommand.Run(report, enlargedVanillaPath, "BF2");

            try { Directory.Delete(bf2EnlargeTempDir, recursive: true); } catch { /* UA: не критично / EN: not critical */ }

            if (bf2LocalizedPath is not null)
            {
                report.Log();
                report.Log($"UA: [BF2] Готово — фінальний локалізований+збільшений файл (без подвійного розмиття кирилиці): {bf2LocalizedPath}");
                report.Log($"EN: [BF2] Done — final localized+enlarged file (no double blur on Cyrillic): {bf2LocalizedPath}");
            }
        }
        else
        {
            report.Log("UA: [BF2] КРИТИЧНО — крок збільшення провалився, кирилицю НЕ додано. / EN: [BF2] CRITICAL — enlarge step failed, Cyrillic NOT added.");
        }
    }
    if (choice is not ("1" or "2" or "3"))
        report.Log("UA: Невідомий вибір — нічого не зроблено. / EN: Unknown choice — nothing done.");

    report.Save();
}

async Task RunGenerateEnlargedFontCore()
{
    const float scale = 1.5f;
    var report = new DiagnosticReport("GenerateEnlargedFontCore");
    report.Log($"UA: Font-writer: ЗБІЛЬШЕННЯ шрифту BF2 ×{scale} (upscale наявних гліфів + масштаб метрик).");
    report.Log($"EN: Font-writer: ENLARGE BF2 font ×{scale} (upscale existing glyphs + scale metrics).");
    report.Log("UA: Коди/таблиця символів НЕ змінюються — GUI і переклад лишаються сумісними. Розмитість тимчасова (далі TTF).");
    report.Log("EN: Codes/character table UNCHANGED — GUI and translation stay compatible. Blur is temporary (TTF next).");
    report.Log();

    // UA: Лише BF2 — саме воно потребує збільшення (BF2 не підтримує
    //     1080p нативно, на відміну від BF1). Питання розміру для BF1
    //     лишається відкритим.
    // EN: BF2 only — it's the one that needs enlarging (BF2 doesn't
    //     support 1080p natively, unlike BF1). The size question for BF1
    //     stays open.
    var corePath = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2", "core.lvl");
    var outputDir = Path.Combine(AppContext.BaseDirectory, "font-output-enlarged", "BF2");
    var outputPath = GenerateEnlargedFontCoreCommand.Run(report, corePath, outputDir, scale);
    if (outputPath is not null)
    {
        report.Log($"UA:   → скопіюйте \"{outputPath}\" (з backup!) у GameData\\data\\_lvl_pc\\core.lvl і перевірте текст у грі.");
        report.Log($"EN:   → copy \"{outputPath}\" (with a backup!) to GameData\\data\\_lvl_pc\\core.lvl and check text in-game.");
    }

    report.Save();
    await Task.CompletedTask;
}

async Task GenerateOneGame(string label, string? picked)
{
    if (picked is null) { Console.WriteLine($"UA: Файл {label} не вибрано — пропущено."); return; }

    const string fontFamilyName = "FiraSans-SemiBold.ttf"; // UA/EN: значення — ВІДНОСНИЙ ШЛЯХ ФАЙЛУ, не назва родини (GDI+ обрізає/зливає family-назви, FONT_FORMAT_SPEC.md 11.13) / the value is a RELATIVE FILE PATH, not a family name (GDI+ truncates/merges family names, FONT_FORMAT_SPEC.md 11.13)

    // UA: starwars_small свідомо НЕ включений — підтверджено (візуально
    //     й через FontGlyphCodeRangeCommand), що це набір іконок HUD/UI,
    //     не текстовий шрифт.
    // EN: starwars_small is deliberately NOT included — confirmed
    //     (visually and via FontGlyphCodeRangeCommand) to be a set of
    //     HUD/UI icons, not a text font.
    string[] fontsToInject = ["gamefont_large", "gamefont_medium", "gamefont_small", "gamefont_tiny", "gamefont_super_tiny"];

    var report = new DiagnosticReport($"GenerateLocalizedCore_{label}");
    await GenerateLocalizedCoreCommand.Run(report, picked, label, fontFamilyName, SoftDonorAnalysis.NeededGlyphCodes, fontsToInject);
    report.Save();
}

async Task RunUvRectBoundsCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("UvRectBoundsCheck");
    report.Log();
    UvRectBoundsCheckCommand.Run(report, bf1Root, "BF1");
    report.Log();
    UvRectBoundsCheckCommand.Run(report, bf2Root, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunBodySizeConsistencyCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("BodySizeConsistency");
    report.Log();
    BodySizeConsistencyCommand.Run(report, bf1Root, "BF1");
    report.Log();
    BodySizeConsistencyCommand.Run(report, bf2Root, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunRasterizerCanvasSizeCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("RasterizerCanvasSizeCheck");
    report.Log();
    RasterizerCanvasSizeCheckCommand.Run(report, bf1Root, "BF1");
    report.Log();
    RasterizerCanvasSizeCheckCommand.Run(report, bf2Root, "BF2");
    report.Save();

    await Task.CompletedTask;
}

async Task RunGlyphSlotUniquenessCheck()
{
    var bf1Path = PickBf1CoreLvl();
    if (bf1Path is null) { Console.WriteLine("UA: Файл BF1 не вибрано."); return; }

    var bf2Path = PickBf2CoreLvl();
    if (bf2Path is null) { Console.WriteLine("UA: Файл BF2 не вибрано."); return; }

    var bf1Root = UcfbReader.ReadFile(bf1Path);
    var bf2Root = UcfbReader.ReadFile(bf2Path);

    var report = new DiagnosticReport("GlyphSlotUniqueness");
    report.Log();
    GlyphSlotUniquenessCommand.Run(report, bf1Root, "BF1");
    report.Log();
    GlyphSlotUniquenessCommand.Run(report, bf2Root, "BF2");
    report.Save();

    await Task.CompletedTask;
}

// ===========================================================================
// UA: ДЕТАЛЬНИЙ РЕЖИМ для одного файлу (запуск з аргументами):
//     повна гістограма чанків, усі NAME-рядки, --tree, --grep, --dump-font.
//     Використовує DiagnosticReport — зберігає у спільну теку
//     "diagnostic-output" біля .exe, а не поруч із вхідним файлом.
// EN: DETAILED MODE for one file (launched with arguments):
//     full chunk histogram, all NAME strings, --tree, --grep, --dump-font.
//     Uses DiagnosticReport — saves to the shared "diagnostic-output"
//     folder next to the .exe, instead of next to the input file.
// ===========================================================================
async Task RunDetailedSingleFileMode(string[] cliArgs)
{
    var filePath = cliArgs[0];
    var showTree = cliArgs.Contains("--tree");
    var grepIdx = Array.IndexOf(cliArgs, "--grep");
    var grepTerm = grepIdx >= 0 && grepIdx + 1 < cliArgs.Length ? cliArgs[grepIdx + 1] : null;
    var dumpIdx = Array.IndexOf(cliArgs, "--dump-font");
    var dumpName = dumpIdx >= 0 && dumpIdx + 1 < cliArgs.Length ? cliArgs[dumpIdx + 1] : null;
    var dumpOutput = dumpIdx >= 0 && dumpIdx + 2 < cliArgs.Length ? cliArgs[dumpIdx + 2] : null;

    if (!File.Exists(filePath))
    {
        Console.WriteLine($"File not found: {filePath}");
        return;
    }

    var report = new DiagnosticReport($"DetailedSingleFile_{Path.GetFileNameWithoutExtension(filePath)}");
    var rawData = File.ReadAllBytes(filePath);

    report.Log("=== BF Diagnostic ===");
    report.Log($"File: {filePath}");
    report.Log($"Size: {rawData.Length} bytes (0x{rawData.Length:X})");
    report.Log("");

    report.Log("--- First 256 bytes ---");
    var dumpLen = Math.Min(256, rawData.Length);
    for (int i = 0; i < dumpLen; i += 16)
    {
        var hex = string.Join(" ", Enumerable.Range(i, Math.Min(16, dumpLen - i)).Select(j => rawData[j].ToString("X2")));
        var ascii = string.Concat(Enumerable.Range(i, Math.Min(16, dumpLen - i)).Select(j => rawData[j] >= 0x20 && rawData[j] < 0x7F ? (char)rawData[j] : '.'));
        report.Log($"  {i,6:X4}: {hex,-48} {ascii}");
    }
    report.Log("");

    UcfbChunk root;
    try
    {
        root = UcfbReader.ReadFile(rawData);
    }
    catch (InvalidDataException ex)
    {
        report.Log($"ERROR: {ex.Message}");
        report.Save();
        return;
    }

    var histogram = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var nameEntries = new List<(long Offset, string Text)>();
    var grepHits = new List<(long Offset, string Context)>();
    const int grepSizeCap = 8 * 1024 * 1024;

    report.Log(showTree ? "--- Chunk tree ---" : "--- Scanning full chunk tree (pass --tree to print it) ---");
    Walk(root, 0);

    report.Log("");
    report.Log("--- Chunk type histogram (усі знайдені типи чанків на всіх рівнях) ---");
    foreach (var (id, count) in histogram)
        report.Log($"  '{id}'  ×{count}");

    report.Log("");
    report.Log("--- All NAME chunk contents found anywhere in the tree ---");
    foreach (var (offset, text) in nameEntries)
        report.Log($"  [{offset:X6}] '{text}'");

    report.Log("");
    report.Log("--- Font resources (BF1LocalizationTool.Core.Fonts.FontChunkLocator) ---");
    var fonts = FontChunkLocator.FindAll(root);
    if (fonts.Count == 0)
    {
        report.Log("  (жодного відомого шрифтового ресурсу не знайдено / no known font resource found)");
    }
    else
    {
        foreach (var font in fonts)
        {
            report.Log($"  {font.BaseName}  (container size={font.Chunk.RawData.Length} bytes, offset=0x{font.Chunk.FileDataOffset:X})");
            foreach (var page in font.TexturePages)
                report.Log($"    - {page.Name}  (size={page.Chunk.RawData.Length} bytes, offset=0x{page.Chunk.FileDataOffset:X})");
        }
    }

    report.Log("");
    report.Log("--- Byte-code usage 128-255 per language (LvlLocalizationService) ---");
    var service = new LvlLocalizationService();
    await service.LoadAsync(filePath);

    var usedByAny = new HashSet<int>();
    foreach (var lang in service.AvailableLanguages)
    {
        var file = service.GetLanguageFile(lang);
        if (file is null) continue;

        var counts = new Dictionary<int, int>();
        foreach (var entry in file.Entries)
            foreach (var c in entry.Original)
                if (c is >= (char)128 and <= (char)255)
                    counts[c] = counts.GetValueOrDefault(c) + 1;

        usedByAny.UnionWith(counts.Keys);
        var codesStr = counts.Count == 0
            ? "(жодного / none)"
            : string.Join(", ", counts.OrderBy(kv => kv.Key).Select(kv => $"0x{kv.Key:X2}×{kv.Value}"));
        report.Log($"  {lang} ({file.Entries.Count} рядків): {codesStr}");
    }

    var unusedByAll = Enumerable.Range(128, 128).Except(usedByAny).OrderBy(x => x).ToList();
    report.Log("");
    report.Log($"--- Безпечні донорні коди: {unusedByAll.Count} шт. / Safe donor codes: {unusedByAll.Count} pcs. ---");
    report.Log("  " + string.Join(", ", unusedByAll.Select(x => $"0x{x:X2}")));

    if (grepTerm != null)
    {
        report.Log("");
        report.Log($"--- Grep results for \"{grepTerm}\" ---");
        if (grepHits.Count == 0)
            report.Log("  (нічого не знайдено / nothing found)");
        foreach (var (offset, context) in grepHits)
            report.Log($"  [{offset:X6}] {context}");
    }

    if (dumpName != null && dumpOutput != null)
    {
        report.Log("");
        var target = fonts.FirstOrDefault(f => f.BaseName == dumpName);
        if (target == null)
            report.Log($"--dump-font: шрифт '{dumpName}' не знайдено серед: {string.Join(", ", fonts.Select(f => f.BaseName))}");
        else
        {
            File.WriteAllBytes(dumpOutput, target.Chunk.RawData);
            report.Log($"--dump-font: збережено {target.Chunk.RawData.Length} байт у {dumpOutput}");
        }
    }

    report.Save();

    void Walk(UcfbChunk chunk, int depth)
    {
        var displayId = chunk.IsFourCC ? chunk.FourCC : $"0x{chunk.Id:X8}";
        histogram[displayId] = histogram.GetValueOrDefault(displayId) + 1;

        if (showTree)
        {
            // UA: Повне дерево виводиться лише в консоль (не в звіт) — на
            //     великих файлах воно може бути тисячі рядків, що зробило б
            //     збережений .txt непридатним для читання.
            // EN: The full tree is printed to the console only (not into
            //     the report) — on large files it can be thousands of
            //     lines, which would make the saved .txt unreadable.
            var indent = new string(' ', depth * 2);
            Console.WriteLine($"{indent}[{chunk.FileDataOffset:X6}] '{displayId}' size={chunk.DataSize} (0x{chunk.DataSize:X})");
        }

        if (chunk.FourCC == "NAME")
        {
            var text = Encoding.ASCII.GetString(chunk.RawData).Replace("\0", "\\0");
            nameEntries.Add((chunk.FileDataOffset, text));
        }

        if (grepTerm != null && chunk.RawData.Length > 0 && chunk.RawData.Length <= grepSizeCap)
        {
            var text = Encoding.ASCII.GetString(chunk.RawData);
            var matchIdx = text.IndexOf(grepTerm, StringComparison.OrdinalIgnoreCase);
            if (matchIdx >= 0)
            {
                const int contextRadius = 40;
                var winStart = Math.Max(0, matchIdx - contextRadius);
                var winLen = Math.Min(text.Length - winStart, grepTerm.Length + contextRadius * 2);
                var context = text.Substring(winStart, winLen).Replace("\0", "\\0");
                grepHits.Add((chunk.FileDataOffset + winStart, $"'{displayId}' → …{context}…"));
            }
        }

        foreach (var child in chunk.Children)
            Walk(child, depth + 1);
    }
}

static void PrintUsage()
{
    Console.WriteLine("UA: Використання: BF1Diagnostic.exe <шлях до файлу> [--tree] [--grep термін] [--dump-font <ім'я> <шлях.bin>]");
    Console.WriteLine("EN: Usage: BF1Diagnostic.exe <file path> [--tree] [--grep term] [--dump-font <name> <path.bin>]");
    Console.WriteLine();
    Console.WriteLine("Drag & drop a .loc or .lvl file onto this exe, or pass flags on the command line.");
}

// ===========================================================================
// UA: Автоматичний пошук core.lvl у фіксованій теці біля .exe — щоб не
//     вибирати файл вручну щоразу. Шукає
//     "{тека exe}\reference-files\BF1\core.lvl" і "...\BF2\core.lvl".
//     Якщо знайдено — використовує напряму (з повідомленням, звідки саме,
//     щоб не було сюрпризом, який файл насправді відкрився). Якщо ні —
//     як і раніше, звичайний діалог вибору файлу.
//
//     Щоб скористатись: одноразово створи теки
//     "{тека exe}\reference-files\BF1\" і "...\BF2\" та поклади туди
//     відповідні core.lvl.
// EN: Automatic core.lvl lookup in a fixed folder next to the .exe — so
//     the file doesn't need to be picked manually every time. Looks for
//     "{exe folder}\reference-files\BF1\core.lvl" and "...\BF2\core.lvl".
//     If found — uses it directly (with a message stating exactly which
//     file, so there's no surprise about what actually got opened). If
//     not — falls back to the regular file picker dialog, as before.
//
//     To use: once, create the folders
//     "{exe folder}\reference-files\BF1\" and "...\BF2\" and place the
//     corresponding core.lvl files there.
// ===========================================================================
string? PickDefaultOrDialogCoreLvl(string gameLabel, string defaultSubfolder)
{
    var defaultPath = Path.Combine(AppContext.BaseDirectory, "reference-files", defaultSubfolder, "core.lvl");
    if (File.Exists(defaultPath))
    {
        Console.WriteLine($"UA: Знайдено core.lvl за замовчуванням для {gameLabel}: {defaultPath}");
        Console.WriteLine($"EN: Found default core.lvl for {gameLabel}: {defaultPath}");
        return defaultPath;
    }

    return NativeFileDialog.ShowOpenDialog($"Виберіть core.lvl для {gameLabel} / Select core.lvl for {gameLabel}");
}

string? PickBf1CoreLvl() => PickDefaultOrDialogCoreLvl("BF1 (Classic 2004)", "BF1");
string? PickBf2CoreLvl() => PickDefaultOrDialogCoreLvl("BF2 (Classic)", "BF2");

// UA: Один пункт меню всередині категорії: два рядки опису (укр+англ) і
//     дія, яку він запускає.
// EN: One menu item inside a category: two description lines (UA+EN) and
//     the action it triggers.
record MenuItem(string DescUA, string DescEN, Func<Task> Action);

// UA: Одна категорія верхнього рівня меню — назва (укр+англ) і список
//     пунктів усередині.
// EN: One top-level menu category — a name (UA+EN) and the list of items
//     inside it.
record MenuCategory(string TitleUA, string TitleEN, List<MenuItem> Items);

// ===========================================================================
// UA: Нативний Windows "Open File" діалог через comdlg32.dll (GetOpenFileNameW).
//     Свідомо БЕЗ UseWindowsForms/UseWPF і без TargetFramework=net10.0-windows —
//     проєкт лишається звичайним крос-платформним консольним net10.0.
// EN: Native Windows "Open File" dialog via comdlg32.dll (GetOpenFileNameW).
//     Deliberately WITHOUT UseWindowsForms/UseWPF and without
//     TargetFramework=net10.0-windows — the project stays a plain
//     cross-platform net10.0 console app.
// ===========================================================================
[SupportedOSPlatform("windows")]
static class NativeFileDialog
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string? lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public string? lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int flagsEx;
    }

    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_PATHMUSTEXIST = 0x00000800;
    private const int OFN_EXPLORER = 0x00080000;

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetOpenFileNameW(ref OPENFILENAME ofn);

    public static string? ShowOpenDialog(string title)
    {
        var fileBuffer = Marshal.AllocHGlobal(2048 * sizeof(char));
        try
        {
            Marshal.WriteInt16(fileBuffer, 0, 0);
            var ofn = new OPENFILENAME
            {
                lStructSize = Marshal.SizeOf<OPENFILENAME>(),
                lpstrFilter = "SWBF level/localization (*.lvl;*.loc)\0*.lvl;*.loc\0All files (*.*)\0*.*\0\0",
                lpstrFile = fileBuffer,
                nMaxFile = 2048,
                lpstrTitle = title,
                Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_EXPLORER
            };
            return GetOpenFileNameW(ref ofn) ? Marshal.PtrToStringUni(fileBuffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
        }
    }
}
