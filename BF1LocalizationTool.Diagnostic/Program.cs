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
using BF1LocalizationTool.Core.Bf2Movies;
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
        "Відео та субтитри (.mvs-контейнери в data\\_lvl_pc\\movies)",
        "Movies & subtitles (.mvs containers in data\\_lvl_pc\\movies)",
        [
            new MenuItem(
                "Структура .mvs: сегменти, довжини, і пошук прив'язки субтитрів до рядків локалізації",
                "…mvs structure: segments, lengths, and the search for a subtitle-to-localization binding",
                RunMvsContainerScan),
            new MenuItem(
                "★★ ПОВНИЙ ПРОГІН: шукати конфіг роликів (mcfg) у ВСІХ .lvl з reference-files\\BF2 — де ще лежать субтитри і які директиви ще не розпізнано",
                "★★ FULL SWEEP: look for the movie config (mcfg) in ALL .lvl files under reference-files\\BF2 — where else subtitles live and which directives are still unidentified",
                RunScanMovieSubtitleConfig),
            new MenuItem(
                "★ ЗОНД ТАЙМІНГІВ: записати mission_subtitle_probe.lvl — перший субтитр Mygeeto на весь ролик (перевірка: субтитр не малюється взагалі, чи малюється, але не видно)",
                "★ TIMING PROBE: write mission_subtitle_probe.lvl — the first Mygeeto subtitle for the whole movie (tests whether the subtitle is not drawn at all, or drawn but invisible)",
                RunGenerateMovieSubtitleProbeMission),
            new MenuItem(
                "★ ЗОНД ШРИФТА: записати mission_subtitle_font_probe.lvl — шрифт субтитрів gamefont_small -> gamefont_large (перевірка: чи справа в самому шрифті на широкому екрані)",
                "★ FONT PROBE: write mission_subtitle_font_probe.lvl — subtitle font gamefont_small -> gamefont_large (tests whether the font itself is to blame at widescreen)",
                RunGenerateMovieSubtitleFontProbe),
            new MenuItem(
                "★★ ДЖЕРЕЛО АЛЬФИ (лише читання BattlefrontII.exe): звузити список функцій, що пишуть у +0x2C..0x2F вузла ролика/субтитру Й звертаються до ws/W/H",
                "★★ ALPHA SOURCE (BattlefrontII.exe read-only): narrow down functions that write to the movie/subtitle node's +0x2C..0x2F AND reference ws/W/H",
                RunAnalyzeMovieSubtitleAlphaSource),
            new MenuItem(
                "★★ ПОХОДЖЕННЯ d3d9.dll: розпакувати вбудоване джерело, порівняти SHA-256, і перезібрати наживо тулчейном Zig (самодостатньо, довантажується сам)",
                "★★ PROVENANCE of d3d9.dll: extract the bundled source, compare SHA-256, and rebuild it live with the Zig toolchain (self-contained, fetches itself)",
                RunGenerateD3D9FixProvenanceReport),
        ]),

    new MenuCategory(
        "BF2 widescreen: генерація патча (Lua50BytecodeWriter + bootstrap entry-point)",
        "BF2 widescreen: patch generation (Lua50BytecodeWriter + bootstrap entry-point)",
        [
            new MenuItem(
                "Тест Writer'а: round-trip власного стабу + реальних скриптів + повний ApplyBootstrapPatch (у пам'яті)",
                "Writer test: round-trip of the bootstrap stub + real scripts + full ApplyBootstrapPatch (in-memory)",
                RunTestWidescreenBootstrapPatch),
            new MenuItem(
                "ТРАСУВАННЯ: ВИКОНАТИ скрипти інтерфейсу і показати РЕАЛЬНІ аргументи викликів рушія",
                "TRACE: EXECUTE the interface scripts and show the REAL arguments of engine calls",
                RunTraceNativeGeometry),
            new MenuItem(
                "ХРОНОЛОГІЯ ЕКРАНА-СПИСКУ: реально виконати ifs_mp_sessionlist і показати ПОВНИЙ порядок побудови (дефект D)",
                "LIST-SCREEN TIMELINE: really execute ifs_mp_sessionlist and show the FULL build order (defect D)",
                RunSessionListHeaderBlock),
            new MenuItem(
                "★★ ФІКС РОЗКЛАДКИ: записати shell_layout.lvl (лише екрани з ПІДТВЕРДЖЕНИМ знімком ванільним дефектом)",
                "★★ LAYOUT FIX: write shell_layout.lvl (only screens with a screenshot-CONFIRMED vanilla defect)",
                RunGenerateAnchorFixShell),
            new MenuItem(
                "★ ДІАГНОСТИЧНИЙ ЗОНД: записати shell_probe.lvl — показати РЕАЛЬНЕ \"widescreen\" (4-те значення ScriptCB_GetScreenInfo) як Y-позицію напису на екрані Сеансу",
                "★ DIAGNOSTIC PROBE: write shell_probe.lvl — display the REAL \"widescreen\" (4th ScriptCB_GetScreenInfo value) as a label's Y-position on the Session screen",
                RunGenerateScreenInfoProbeShell),
            new MenuItem(
                "★ ТОЧКОВИЙ ТЕСТ: записати shell_bgfix.lvl — примусово bg.localpos_r=w у fnAddBackground (той самий фікс, що вже в production shell_layout.lvl; ізольована збірка для перевірки нових екранів/текстур)",
                "★ SPOT TEST: write shell_bgfix.lvl — force bg.localpos_r=w in fnAddBackground (the same fix already in production shell_layout.lvl; an isolated build for testing new screens/textures)",
                RunGenerateBackgroundSizeFixShell),
            new MenuItem(
                "★ ЗОНД СУБТИТРІВ РОЛИКА: записати shell_subtitle_probe.lvl — статичний IFText (\"TEST\") поверх вступного краула кампанії (перевірка: чи взагалі малюється текст поверх ролика, без .exe)",
                "★ MOVIE SUBTITLE PROBE: write shell_subtitle_probe.lvl — a static IFText (\"TEST\") over the campaign intro crawl (tests whether text renders over the movie at all, no .exe patch)",
                RunGenerateBattleIntroSubtitleProbeShell),
            new MenuItem(
                "★★ РЕЖИМ ПРЯМОКУТНИКА ВІДЕО: записати shell_movierect.lvl — перевести екрани роликів кампанії на ВЛАСНИЙ широкоформатний режим гри (один операнд; на 4:3 не змінює нічого)",
                "★★ MOVIE RECT MODE: write shell_movierect.lvl — switch the campaign movie screens to the game's OWN widescreen mode (one operand; changes nothing at 4:3)",
                RunGenerateMovieRectModeShell),
            new MenuItem(
                "★ ПІДТВЕРДЖЕНО В ГРІ: записати ingame_spawnselect_gapfix.lvl — додає зазор між написом \"Кількість бійців\" і кнопкою \"Відродження\" на екрані вибору бійця (зсув позиції тексту, шрифт НЕ змінюється; докладно — docs/BF2_SPAWNSELECT_GAP_FIX.md)",
                "★ CONFIRMED IN-GAME: write ingame_spawnselect_gapfix.lvl — adds a gap between the \"Кількість бійців\" label and the \"Відродження\" button on the unit-selection screen (shifts the text's position, font is NOT changed; details — docs/BF2_SPAWNSELECT_GAP_FIX.md)",
                RunGenerateSpawnSelectUnitCountGapFix),
            new MenuItem(
                "★ КАНДИДАТ: записати shell_freeform_descfont.lvl — ЗБІЛЬШИТИ шрифт опису в інформаційній панелі екранів Галактичного завоювання з gamefont_tiny на gamefont_small (спільна функція — виправляє десятки екранів одразу; НЕ ПІДТВЕРДЖЕНО у грі)",
                "★ CANDIDATE: write shell_freeform_descfont.lvl — ENLARGE the description font in the Galactic Conquest screens' info panel from gamefont_tiny to gamefont_small (shared function — fixes dozens of screens at once; NOT YET CONFIRMED in-game)",
                RunGenerateFreeformInfoDescriptionFontFix),
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
            new MenuItem(
                "★ ФІКС HEAD: виправити застарілу висоту шрифтів (HEAD[3]) у готовому core.lvl з reference-files\\BF2-UA-rem → font-output-headfix\\BF2\\core.lvl (по 1 байту на шрифт, атлас не чіпається)",
                "★ HEAD FIX: correct the stale font height (HEAD[3]) in the finished core.lvl from reference-files\\BF2-UA-rem → font-output-headfix\\BF2\\core.lvl (1 byte per font, atlas untouched)",
                RunGenerateFontHeadHeightFixCore),
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

    const string fontFamilyName = "FiraSans-SemiBold.ttf"; // UA/EN: значення — шлях файлу, не назва родини (GDI+ обрізає/зливає назви, FONT_FORMAT_SPEC.md 11.13) / value is a file path, not a family name (GDI+ truncates/merges names, FONT_FORMAT_SPEC.md 11.13)

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

    const string fontFamilyName = "FiraSans-SemiBold.ttf"; // UA/EN: значення — шлях файлу, не назва родини (GDI+ обрізає/зливає назви, FONT_FORMAT_SPEC.md 11.13) / value is a file path, not a family name (GDI+ truncates/merges names, FONT_FORMAT_SPEC.md 11.13)

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
//     Тобто ЖОДЕН із двох існуючих пунктів не дає оглянути РЕАЛЬНИЙ файл,
//     який користувач фактично тестував у грі (no-donor результат, або
//     файл, збережений через GUI). Цей варіант — БЕЗ жодного дефолту,
//     діалог вибору файлу показується ЗАВЖДИ, для обох ігор — щоб можна
//     було вказати САМЕ той core.lvl, що стоїть у грі зараз.
// EN: RunGlyphOccupancyOverlay (above) and RunGlyphOccupancyOverlayOnOutput
//     (below) both have a "hardcoded" file choice: the first silently
//     takes reference-files\{Game}\core.lvl IF it exists (and it almost
//     always does — it's the generation's own input file) — and NEVER
//     shows a picker dialog in that case (PickDefaultOrDialogCoreLvl); the
//     second hard-codes reading only output\{Game}\... (the LEGACY donor
//     pipeline's folder), not output-nodonor\ (where the CURRENT no-donor
//     generator actually writes) or the GUI's own output\ (a different
//     .exe, a different folder). So NEITHER existing item lets you inspect
//     the ACTUAL file the user tested in-game (the no-donor result, or a
//     file saved via the GUI). This variant has NO default at all — the
//     file picker always shows, for both games — so you can point it at
//     whichever core.lvl is actually in the game right now.
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

    const string fontFamilyName = "FiraSans-SemiBold.ttf"; // UA/EN: значення — шлях файлу, не назва родини (GDI+ обрізає/зливає назви, FONT_FORMAT_SPEC.md 11.13) / value is a file path, not a family name (GDI+ truncates/merges names, FONT_FORMAT_SPEC.md 11.13)

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
    //     ін'єкції (не вигаданий тут заново). Якщо для якоїсь гри output
    //     ще не згенеровано — WordRenderInspectCommand.Run про це прямо
    //     скаже й пропустить, не впаде.
    // EN: NO game/font selection — always BOTH games and ALL 5
    //     gamefont_*, the SAME list GenerateOneGame uses for the actual
    //     injection (not reinvented here). If a game's output hasn't
    //     been generated yet, WordRenderInspectCommand.Run says so
    //     directly and skips it, doesn't crash.
    string[] fontsToInspect = ["gamefont_large", "gamefont_medium", "gamefont_small", "gamefont_tiny", "gamefont_super_tiny"];
    (string Label, string RelativeGamePath)[] games =
    [
        ("BF1", Path.Combine("Star Wars Battlefront (Classic 2004)", "GameData", "Data", "_LVL_PC")),
        ("BF2", Path.Combine("Star Wars Battlefront II Classic", "GameData", "data", "_lvl_pc")),
    ];

    // UA: ОДНА спільна підтека на весь цей прогін (не плоска купа PNG у
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
    // UA: GlyphOccupancyOverlayCommand дає РЕАЛЬНІ пікселі ВСІЄЇ сторінки
    //     (англійські й кириличні гліфи РАЗОМ, у їхньому справжньому
    //     просторовому контексті) + контур КОЖНОГО гліфа з FBOD, без
    //     жодної додаткової композиції/припущень про вирівнювання (на
    //     відміну від WordRenderInspectCommand, який сам вирізає й
    //     вирівнює літери по одній вигаданій лінії) — прямий спосіб
    //     побачити реальну різницю параметрів між англійським і
    //     вставленим українським текстом. Той самий виклик, той самий
    //     код, що й у RunGlyphOccupancyOverlay (категорія 3, працює з
    //     ОРИГІНАЛЬНИМИ файлами) — тут лише на ВЖЕ ЗГЕНЕРОВАНОМУ output
    //     (обидві гри, автоматично обчислений шлях, як і в
    //     RunWordRenderInspect).
    // EN: GlyphOccupancyOverlayCommand gives the REAL pixels of the WHOLE
    //     page (English and Cyrillic glyphs TOGETHER, in their real
    //     spatial context) + EVERY glyph's outline from FBOD, with no
    //     extra composition/alignment assumptions (unlike
    //     WordRenderInspectCommand, which crops and aligns letters to one
    //     made-up line itself) — a direct way to see the real difference
    //     between the English and the inserted Ukrainian text. The SAME
    //     call, the SAME code as RunGlyphOccupancyOverlay (category 3,
    //     works on ORIGINAL files) — here just on the ALREADY-GENERATED
    //     output (both games, path computed automatically, same as
    //     RunWordRenderInspect).
    (string Label, string RelativeGamePath)[] games =
    [
        ("BF1", Path.Combine("Star Wars Battlefront (Classic 2004)", "GameData", "Data", "_LVL_PC")),
        ("BF2", Path.Combine("Star Wars Battlefront II Classic", "GameData", "data", "_lvl_pc")),
    ];

    // UA: Підтека на ЦЕЙ запуск — уникає плоскої diagnostic-output, де
    //     PNG усіх запусків змішувались би в одну теку, ускладнюючи
    //     перегляд великої кількості файлів одразу.
    // EN: Subfolder for THIS run — avoids a flat diagnostic-output where
    //     PNGs from every run would mix together, making it harder to
    //     review a large batch of files at once.
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
    //     паттерн, що й в RunGlyphOccupancyOverlayOnOutput.
    //     CellHeightHypothesisTestCommand сам іде по УСІХ шрифтах
    //     (FontChunkLocator.FindAll) усередині кожної гри — тут нема чого
    //     звужувати.
    // EN: NO game/font selection — always BOTH games, the same path
    //     pattern as RunGlyphOccupancyOverlayOnOutput.
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
//     один раз, і меню саме їх підхоплює. Це BF2-специфічна фіча
//     (BF1 використовує Lua 4.0 і взагалі інший шлях меню — не
//     звуження, а свідомо заданий із самого початку обсяг задачі, на
//     відміну від шрифтового пайплайна, де обидві гри обов'язкові).
// EN: shell.lvl/ingame.lvl are looked up via the SAME "reference-files"
//     convention as core.lvl (PickDefaultOrDialogCoreLvl) — drop the
//     files into "{exe folder}\reference-files\BF2\shell.lvl" and
//     "...\ingame.lvl" once, and the menu picks them up automatically.
//     This is a BF2-specific feature (BF1 uses Lua 4.0 and an entirely
//     different menu path — not scope-narrowing, but a scope that was
//     BF2-only from the start, unlike the font pipeline where both
//     games are mandatory).
// ===========================================================================
async Task RunAnalyzeShellScriptConstants()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var shellPath = FindGameFile(bf2RefDir, "shell.lvl") ?? Path.Combine(bf2RefDir, "shell.lvl");
    var ingamePath = FindGameFile(bf2RefDir, "ingame.lvl") ?? Path.Combine(bf2RefDir, "ingame.lvl");

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
    var shellPath = FindGameFile(bf2RefDir, "shell.lvl") ?? Path.Combine(bf2RefDir, "shell.lvl");
    var ingamePath = FindGameFile(bf2RefDir, "ingame.lvl") ?? Path.Combine(bf2RefDir, "ingame.lvl");

    var report = new DiagnosticReport("DisassembleScreenLayoutFunctions");
    report.Log("UA: Widescreen-фікс BF2: повний дизасемблер ЛИШЕ функцій, що згадують screen/aspect-ключові слова.");
    report.Log("EN: BF2 widescreen fix: full disassembly of ONLY functions mentioning screen/aspect keywords.");
    report.Log();

    DisassembleScreenLayoutFunctionsCommand.Run(report, shellPath, "shell.lvl", "BF2");
    DisassembleScreenLayoutFunctionsCommand.Run(report, ingamePath, "ingame.lvl", "BF2");

    report.Save();
    await Task.CompletedTask;
}

// ===========================================================================
// UA: Розбір контейнерів відеороликів .mvs. За тим самим "reference-files"-
//     принципом: якщо є тека "reference-files\BF2\movies", беруться всі
//     .mvs звідти; інакше — звичайний діалог вибору файлу. Великі файли
//     (ingame.mvs — понад 500 МБ) читаються ЛИШЕ на початку, тож копіювати
//     чи різати їх сторонніми засобами не треба.
// EN: Scans .mvs movie containers. Same "reference-files" convention: if
//     "reference-files\BF2\movies" exists, every .mvs there is scanned;
//     otherwise a normal file dialog is shown. Large files (ingame.mvs is
//     500+ MB) are read only at the start, so there is no need to copy or
//     slice them with outside tools.
// ===========================================================================
async Task RunMvsContainerScan()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var moviesDir = FindGameDir(bf2RefDir, "movies") ?? Path.Combine(bf2RefDir, "movies");

    // UA: Джерела імен: у скриптах цих файлів лежать імена сегментів
    //     ('cormon01', 'crawl1', ...), якими їх викликає гра.
    // EN: Name sources: these files' scripts hold the segment names
    //     ('cormon01', 'crawl1', ...) the game calls them by.
    var lvlPathsForNames = new[] { "mission.lvl", "shell.lvl", "common.lvl", "ingame.lvl", "inshell.lvl" }
        .Select(f => FindGameFile(bf2RefDir, f))
        .OfType<string>()
        .ToList();

    var corePath = FindGameFile(bf2RefDir, "core.lvl") ?? Path.Combine(bf2RefDir, "core.lvl");

    List<string> targets;
    if (Directory.Exists(moviesDir))
    {
        targets = Directory.GetFiles(moviesDir, "*.mvs").OrderBy(p => p).ToList();
        Console.WriteLine($"UA: Знайдено {targets.Count} .mvs у \"{moviesDir}\".");
        Console.WriteLine($"EN: Found {targets.Count} .mvs files in \"{moviesDir}\".");
        Console.Write("Порожньо = усі, або частина імені (напр. ingame) / Empty = all, or a name part: ");
        var filter = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(filter))
            targets = targets
                .Where(p => Path.GetFileName(p).Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
    }
    else
    {
        var picked = NativeFileDialog.ShowOpenDialog("Виберіть .mvs / Select a .mvs file");
        if (picked is null) { Console.WriteLine("UA: Файл не вибрано."); return; }
        targets = [picked];
    }

    if (targets.Count == 0) { Console.WriteLine("UA: Нічого не вибрано."); return; }

    var report = new DiagnosticReport("MvsContainerScan");
    report.Log("UA: Контейнери відеороликів .mvs — структура заголовка й пошук прив'язки субтитрів.");
    report.Log("EN: .mvs movie containers — header structure and the search for a subtitle binding.");
    report.Log($"UA: Джерела імен сегментів / Segment name sources: {string.Join(", ", lvlPathsForNames.Select(Path.GetFileName))}");
    report.Log($"UA: Локалізація / Localization: {(File.Exists(corePath) ? Path.GetFileName(corePath) : "— немає / none")}");
    report.Log();

    foreach (var target in targets)
    {
        MvsContainerScanCommand.Run(
            report,
            target,
            File.Exists(corePath) ? corePath : null,
            lvlPathsForNames);
        report.Log(new string('=', 78));
        report.Log();
    }

    report.Save();
    await Task.CompletedTask;
}

async Task RunTestWidescreenBootstrapPatch()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var shellPath = FindGameFile(bf2RefDir, "shell.lvl") ?? Path.Combine(bf2RefDir, "shell.lvl");

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


// -----------------------------------------------------------------------------
// UA: Трасування нативних викликів: ВИКОНУЄ реальні скрипти інтерфейсу у
//     власному інтерпретаторі Lua 5.0 і показує фактичні аргументи, з якими
//     вони звертаються до рушія. Це відповідає на питання, на яке дизасемблер
//     відповісти не може: який аргумент є координатою x, який y, а який взагалі
//     не є довжиною — бо геометрія BF2 обчислюється в рантаймі.
//     Саме цим інструментом виявлено, що нативні функції мають ДВІ форми
//     виклику (з хендлом об'єкта і без нього) — див. TraceNativeGeometryCommand.
// EN: Native-call tracing: EXECUTES the real interface scripts in a
//     dedicated Lua 5.0 interpreter and shows the actual arguments passed
//     to the engine — something the disassembler cannot show, because
//     BF2 geometry is computed at runtime. This tool revealed that geometry natives have TWO
//     calling conventions (with and without the object handle).
// -----------------------------------------------------------------------------
async Task RunTraceNativeGeometry()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var commonPath = FindGameFile(bf2RefDir, "common.lvl") ?? Path.Combine(bf2RefDir, "common.lvl");
    var shellPath = FindGameFile(bf2RefDir, "shell.lvl") ?? Path.Combine(bf2RefDir, "shell.lvl");

    var report = new DiagnosticReport("TraceNativeGeometry");
    report.Log("UA: Трасування викликів рушія через ВИКОНАННЯ реального байткоду інтерфейсу.");
    report.Log("EN: Tracing engine calls by EXECUTING the real interface bytecode.");
    report.Log($"UA: Потрібні файли: \"{commonPath}\" і \"{shellPath}\".");
    report.Log($"EN: Required files: \"{commonPath}\" and \"{shellPath}\".");
    report.Log();

    // UA: два прогони одного й того ж vanilla-файлу з різною роздільністю —
    //     показують, що САМІ СКРИПТИ роздільність не враховують (числа
    //     однакові), тобто масштабування зобов'язаний робити фікс.
    // EN: two runs of the same vanilla file at different resolutions — they show
    //     the SCRIPTS THEMSELVES ignore resolution (identical numbers), i.e. the
    //     scaling has to come from the fix.
    report.Log("UA: === Прогін A: vanilla @ 800x600 (еталон, рідна роздільність гри) ===");
    report.Log("EN: === Run A: vanilla @ 800x600 (reference, the game's native resolution) ===");
    TraceNativeGeometryCommand.Run(report, commonPath, shellPath, 800, 600);
    report.Log();
    report.Log("UA: === Прогін B: vanilla @ 1920x1080 (без фікса) ===");
    report.Log("EN: === Run B: vanilla @ 1920x1080 (no fix) ===");
    TraceNativeGeometryCommand.Run(report, commonPath, shellPath, 1920, 1080);
    report.Log();
    report.Log("UA: ВИСНОВОК: якщо числа в A і B збігаються — скрипти не масштабуються самі,");
    report.Log("UA: і будь-який widescreen-фікс мусить множити їх ззовні.");
    report.Log("EN: CONCLUSION: if A and B match, the scripts do not scale themselves,");
    report.Log("EN: so any widescreen fix must multiply them from the outside.");

    report.Save();
    await Task.CompletedTask;
}

// -----------------------------------------------------------------------------
// UA: Хронологія побудови ОДНОГО екрана-списку (за замовчуванням
//     ifs_mp_sessionlist): реально виконує його скрипт і друкує ОДИН
//     впорядкований журнал "AddIFObjectBase зареєстрував об'єкт X" одразу за
//     яким ідуть геометричні натив-виклики для НЬОГО Ж — замість того, щоб
//     здогадуватись про зв'язок filterRow/listbox/titleBarElement/
//     ResortButtons із кінцевої статичної таблиці (дефект D, Bf2LayoutTable.txt).
//     Див. розгорнуте пояснення в SessionListHeaderBlockCommand.cs.
// EN: Build timeline of ONE list screen (default ifs_mp_sessionlist): really
//     executes its script and prints ONE ordered log — "AddIFObjectBase
//     registered object X" immediately followed by the geometry native calls
//     FOR THAT SAME OBJECT — instead of guessing the filterRow/listbox/
//     titleBarElement/ResortButtons relationship from the final static table
//     (defect D, Bf2LayoutTable.txt). See SessionListHeaderBlockCommand.cs
//     for the full rationale.
// -----------------------------------------------------------------------------
async Task RunSessionListHeaderBlock()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var commonPath = FindGameFile(bf2RefDir, "common.lvl") ?? Path.Combine(bf2RefDir, "common.lvl");
    var shellPath = FindGameFile(bf2RefDir, "shell.lvl") ?? Path.Combine(bf2RefDir, "shell.lvl");

    var report = new DiagnosticReport("SessionListHeaderBlock");
    report.Log("UA: Реальне виконання ifs_mp_sessionlist через Bf2ScriptHost — читаємо, перш ніж знову правити фікс (дефект D).");
    report.Log("EN: Really executing ifs_mp_sessionlist via Bf2ScriptHost — read first, before touching the fix again (defect D).");
    report.Log($"UA: Потрібні файли: \"{commonPath}\" і \"{shellPath}\".");
    report.Log($"EN: Required files: \"{commonPath}\" and \"{shellPath}\".");
    report.Log();

    // UA: 800x600 — авторська роздільність (еталонна поведінка, без жодного
    //     масштабування) — з неї варто починати читання.
    // EN: 800x600 — the authored resolution (reference behaviour, no
    //     scaling involved) — the right place to start reading from.
    report.Log("UA: === 800x600 (авторська роздільність, еталон) ===");
    report.Log("EN: === 800x600 (authored resolution, reference) ===");
    SessionListHeaderBlockCommand.Run(report, commonPath, shellPath, 800, 600);

    // -----------------------------------------------------------------
    // UA: ДРУГИЙ прогін — 1920x1080, ТІ САМІ (незайманий, ванільний)
    //     shell.lvl/common.lvl, БЕЗ жодного патча цього інструменту. Мета — перевірити
    //     ключову гіпотезу зі знайденої у 800x600 формули (titleBarElement.
    //     x/y/width обчислюються з listbox.width/height САМИМ скриптом, а
    //     не статичними числами): чи ванільний скрипт САМ перераховує
    //     listbox.width/height при зміні ScriptCB_GetSafeScreenInfo (тобто
    //     реагує на ширший екран без жодного зовнішнього втручання). Якщо
    //     так — і listbox, і titleBarElement, і ResortButtons мають лишитись
    //     широкими/на місці АВТОМАТИЧНО, а bgoffsety/bgexpandy/@post:
    //     ResortButtons posy з Bf2LayoutTable.txt можуть бути НЕ компенсацією
    //     дефекту, а його ПРИЧИНОЮ (зайвий виклик SetBackgroundSize, якого
    //     ваніль на titleBarElement взагалі не робить).
    // EN: SECOND run — 1920x1080, the SAME untouched vanilla shell.lvl/
    //     common.lvl, with NONE of this tool's patch applied. Goal: test the key
    //     hypothesis from the formula found at 800x600 (titleBarElement's x/
    //     y/width are computed BY THE SCRIPT ITSELF from listbox.width/
    //     height, not static numbers): does the vanilla script itself
    //     recompute listbox.width/height when ScriptCB_GetSafeScreenInfo
    //     changes (i.e. does it react to a wider screen with zero
    //     external intervention). If so — listbox, titleBarElement AND
    //     ResortButtons should all stay wide/in place AUTOMATICALLY, and the
    //     bgoffsety/bgexpandy/@post:ResortButtons posy rows in
    //     Bf2LayoutTable.txt may not be compensating for the defect at all —
    //     they may BE the defect (an extra SetBackgroundSize call vanilla
    //     never makes on titleBarElement in the first place).
    // -----------------------------------------------------------------
    report.Log();
    report.Log("UA: === 1920x1080 (той самий ВАНІЛЬНИЙ shell.lvl, патч цього інструменту тут відсутній) ===");
    report.Log("EN: === 1920x1080 (the SAME VANILLA shell.lvl, none of this tool's patch involved) ===");
    SessionListHeaderBlockCommand.Run(report, commonPath, shellPath, 1920, 1080);

    // -----------------------------------------------------------------
    // UA: ТРЕТІЙ прогін — 1920x1080, ЗІ встановленою СПРАВЖНЬОЮ обгорткою
    //     AddIFScreen (AnchorInheritancePatchBuilder.BuildInstallerScript,
    //     scale=1.0 — рівно той код і той параметр, що йде у
    //     widescreen-output/shell_layout.lvl). Другий прогін (вище) показав
    //     ФОРМУЛУ ваніль-скрипта; цей показує, що з нею робить САМЕ ЦЕЙ
    //     патч (Bf2LayoutTable.txt) — реальні bgoffsety/bgexpandy/alpha,
    //     реальний @post:listbox.skin/@post:ResortButtons тощо, застосовані
    //     через ТОЙ САМИЙ код, що ставиться у гру. Мета — побачити ЧИСЛОМ,
    //     куди насправді потрапляє titleBarElement на цій роздільності,
    //     замість здогадки за скріншотом.
    // EN: THIRD run — 1920x1080, WITH the REAL AddIFScreen wrapper installed
    //     (AnchorInheritancePatchBuilder.BuildInstallerScript, scale=1.0 —
    //     the exact code and parameter shipped in
    //     widescreen-output/shell_layout.lvl). The second run (above) showed
    //     the vanilla script's FORMULA; this one shows what THIS TOOL'S patch
    //     (Bf2LayoutTable.txt) does to it — the real bgoffsety/bgexpandy/
    //     alpha, the real @post:listbox.skin/@post:ResortButtons etc.,
    //     applied through the EXACT code that ships in the game. Goal: see
    //     NUMERICALLY where titleBarElement actually ends up at this
    //     resolution, instead of guessing from a screenshot.
    // -----------------------------------------------------------------
    report.Log();
    report.Log("UA: === 1920x1080, З ПАТЧЕМ ЦЬОГО ІНСТРУМЕНТУ (та сама обгортка AddIFScreen, що й у shell_layout.lvl) ===");
    report.Log("EN: === 1920x1080, WITH THIS TOOL'S PATCH (the same AddIFScreen wrapper as in shell_layout.lvl) ===");
    SessionListHeaderBlockCommand.Run(report, commonPath, shellPath, 1920, 1080,
        installWidescreenPatchScale: 1.0f);

    report.Save();
    await Task.CompletedTask;
}

// UA: Widescreen-фікс. ОБСЯГ НАВМИСНО ВУЗЬКИЙ: правляться лише ті екрани,
//     де ванільний дефект ПІДТВЕРДЖЕНО знімком екрана. ПРИЧИНА: синтетичні
//     метрики («позиція змінилась із роздільністю», «розмір масштабується»)
//     можуть позначати як дефект НОРМАЛЬНУ адаптивну поведінку — наприклад,
//     екран аудіо у ванілі на 1920×1080 коректний сам по собі, хоча без
//     підтвердження знімком патч міг би розсунути підписи й повзунки на
//     ньому на 340 px, зламавши робочий екран.
// EN: Widescreen fix. SCOPE IS DELIBERATELY NARROW: only screens whose
//     vanilla defect is CONFIRMED by a screenshot are touched. REASON:
//     synthetic metrics ("position changed with resolution", "size
//     scales") can flag NORMAL responsive behaviour as a defect — for
//     example, the audio screen is correct in vanilla at 1920x1080 on
//     its own, though without screenshot confirmation a patch could push
//     its labels and sliders 340 px apart, breaking a working screen.
async Task RunGenerateAnchorFixShell()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var shellPath = FindGameFile(bf2RefDir, "shell.lvl") ?? Path.Combine(bf2RefDir, "shell.lvl");
    var outputDir = Path.Combine(AppContext.BaseDirectory, "widescreen-output");

    var report = new DiagnosticReport("GenerateAnchorFixShell");
    report.Log("UA: Widescreen-фікс BF2 — лише підтверджені знімком дефекти.");
    report.Log("EN: BF2 widescreen fix — only screenshot-confirmed defects.");
    report.Log($"UA: Вхідний (vanilla) shell.lvl: \"{shellPath}\" — цей файл НЕ змінюється.");
    report.Log($"EN: Input (vanilla) shell.lvl: \"{shellPath}\" — this file is NOT modified.");
    report.Log();

    var outputPath = GenerateAnchorFixShellCommand.Run(
        report, shellPath, outputDir, "shell_layout.lvl", scale: 1.0f);

    if (outputPath is not null)
    {
        report.Log();
        report.Log("UA: === Як тестувати ===");
        report.Log("EN: === How to test ===");
        report.Log(@"UA: 1. Резервна копія: ...\GameData\data\_lvl_pc\shell.lvl -> shell.lvl.backup");
        report.Log(@"EN: 1. Back up: ...\GameData\data\_lvl_pc\shell.lvl -> shell.lvl.backup");
        report.Log($"UA: 2. Скопіюйте \"{outputPath}\" на місце shell.lvl.");
        report.Log($"EN: 2. Copy \"{outputPath}\" over shell.lvl.");
        report.Log("UA: 3. Перевіряти лише екран ВИБОРУ ПРОФІЛЮ: підпис і список мають стояти ВСЕРЕДИНІ рамки вікна.");
        report.Log("EN: 3. Check only the PROFILE SELECTION screen: the label and list must sit INSIDE the window frame.");
        report.Log("UA: 4. Решта екранів мають лишитись ТОЧНО такими, як у ванілі — патч їх не чіпає. Якщо десь є відмінність від ванілі, це помилка.");
        report.Log("EN: 4. Every other screen must look EXACTLY as in vanilla — the patch does not touch them. Any difference is a bug.");
    }

    report.Save();
    await Task.CompletedTask;
}

// UA: ДІАГНОСТИЧНИЙ ЗОНД — НЕ production. Див. розгорнутий коментар у
//     GenerateScreenInfoProbeShellCommand.cs і в AnchorInheritancePatchBuilder.cs
//     (розділ "ДІАГНОСТИЧНИЙ ЗОНД" перед BuildDispatchWrapper). Мета — дізнатись
//     РЕАЛЬНЕ значення "widescreen" (4-те значення ScriptCB_GetScreenInfo) на
//     справжніх 1920x1080, бо Python-VM завжди підставляє заглушку 1.0, а
//     користувач не має доступу до консолі/логу гри. Результат читається з
//     ОДНОГО знімка екрана — без жодного стороннього інструменту.
// EN: DIAGNOSTIC PROBE — NOT production. See the detailed comment in
//     GenerateScreenInfoProbeShellCommand.cs and in
//     AnchorInheritancePatchBuilder.cs (the "DIAGNOSTIC PROBE" section before
//     BuildDispatchWrapper). Goal: learn the REAL "widescreen" value (the 4th
//     ScriptCB_GetScreenInfo return value) at actual 1920x1080, since the
//     Python VM always stubs it as 1.0 and the user has no access to the
//     game's console/log. The result is read off a SINGLE screenshot — no
//     external tool needed.
async Task RunGenerateScreenInfoProbeShell()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var shellPath = FindGameFile(bf2RefDir, "shell.lvl") ?? Path.Combine(bf2RefDir, "shell.lvl");
    var outputDir = Path.Combine(AppContext.BaseDirectory, "widescreen-output-probe");

    var report = new DiagnosticReport("GenerateScreenInfoProbeShell");
    report.Log("UA: ДІАГНОСТИЧНИЙ ЗОНД BF2 — реальне значення \"widescreen\", НЕ production-патч.");
    report.Log("EN: BF2 DIAGNOSTIC PROBE — the real \"widescreen\" value, NOT a production patch.");
    report.Log($"UA: Вхідний (vanilla) shell.lvl: \"{shellPath}\" — цей файл НЕ змінюється.");
    report.Log($"EN: Input (vanilla) shell.lvl: \"{shellPath}\" — this file is NOT modified.");
    report.Log();

    var outputPath = GenerateScreenInfoProbeShellCommand.Run(report, shellPath, outputDir, "shell_probe.lvl");

    if (outputPath is not null)
    {
        report.Log();
        report.Log("UA: === Як тестувати ===");
        report.Log("EN: === How to test ===");
        report.Log(@"UA: 1. Резервна копія: ...\GameData\data\_lvl_pc\shell.lvl -> shell.lvl.backup (ЯКЩО ще нема — не перезаписуйте вже зроблену копію ванілі!)");
        report.Log(@"EN: 1. Back up: ...\GameData\data\_lvl_pc\shell.lvl -> shell.lvl.backup (IF not already done — do not overwrite an existing vanilla backup!)");
        report.Log($"UA: 2. Скопіюйте \"{outputPath}\" на місце shell.lvl (це ТИМЧАСОВА заміна лише для цього тесту).");
        report.Log($"EN: 2. Copy \"{outputPath}\" over shell.lvl (a TEMPORARY swap for this test only).");
        report.Log("UA: 3. Перейдіть: Миттєвий бій -> Сеанс. Зробіть ОДИН знімок екрана.");
        report.Log("EN: 3. Navigate to: Instant Action -> Session. Take ONE screenshot.");
        report.Log("UA: 4. Виміряйте піксельну Y-координату ВЕРХУ напису \"Налаштування:\" (він буде зсунутий вниз — це очікувано й нормально для цього тесту).");
        report.Log("EN: 4. Measure the pixel Y coordinate of the TOP of the \"Налаштування:\"/\"Settings:\" label (it will be shifted down — expected and normal for this test).");
        report.Log("UA: 5. Поділіть цю Y-координату на 500 — це і є РЕАЛЬНЕ значення \"widescreen\" на цій роздільності. Повідомте це число.");
        report.Log("EN: 5. Divide that Y coordinate by 500 — that is the REAL \"widescreen\" value at this resolution. Report that number back.");
        report.Log("UA: 6. Після тесту ОБОВ'ЯЗКОВО поверніть shell_layout.lvl (або оригінальний ванільний shell.lvl) — цей файл НЕ для постійного використання.");
        report.Log("EN: 6. After testing, MAKE SURE to restore shell_layout.lvl (or the original vanilla shell.lvl) — this file is NOT for permanent use.");
    }

    report.Save();
    await Task.CompletedTask;
}

// UA: ДІАГНОСТИЧНИЙ ЗОНД — НЕ production і НЕ фікс субтитрів. Перевіряє
//     гіпотезу: чи малюється звичайний Lua/IFText поверх вступного ролика
//     кампанії, БЕЗ жодного патчу .exe. Див. розгорнутий коментар у
//     BattleIntroSubtitleProbePatcher.cs. Результат читається з ОДНОГО
//     знімка екрана на 1920x1080: якщо напис "TEST" видно поверх краулу —
//     гіпотеза підтверджена, і наступний крок — реальний таймінг субтитрів
//     (Lua-таймер + текст з Locl); якщо ні — треба переглянути підхід.
// EN: DIAGNOSTIC PROBE — NOT production and NOT a subtitle fix. Tests the
//     hypothesis that an ordinary Lua/IFText renders on top of the
//     campaign intro movie, with NO .exe patch at all. See the detailed
//     comment in BattleIntroSubtitleProbePatcher.cs. The result is read off ONE
//     screenshot at 1920x1080: if "TEST" is visible over the crawl, the
//     hypothesis is confirmed and the next step is real subtitle timing
//     (a Lua timer + text from Locl); if not, the approach needs revisiting.
async Task RunGenerateBattleIntroSubtitleProbeShell()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var shellPath = FindGameFile(bf2RefDir, "shell.lvl") ?? Path.Combine(bf2RefDir, "shell.lvl");
    var outputDir = Path.Combine(AppContext.BaseDirectory, "widescreen-output-subtitle-probe");

    var report = new DiagnosticReport("GenerateBattleIntroSubtitleProbeShell");
    report.Log("UA: ЗОНД СУБТИТРІВ РОЛИКА BF2 — перевірка гіпотези, НЕ production-патч.");
    report.Log("EN: BF2 MOVIE SUBTITLE PROBE — hypothesis test, NOT a production patch.");
    report.Log($"UA: Вхідний (vanilla) shell.lvl: \"{shellPath}\" — цей файл НЕ змінюється.");
    report.Log($"EN: Input (vanilla) shell.lvl: \"{shellPath}\" — this file is NOT modified.");
    report.Log();

    var outputPath = GenerateBattleIntroSubtitleProbeShellCommand.Run(
        report, shellPath, outputDir, "shell_subtitle_probe.lvl");

    if (outputPath is not null)
    {
        report.Log();
        report.Log("UA: === Як тестувати ===");
        report.Log("EN: === How to test ===");
        report.Log(@"UA: 1. Резервна копія: ...\GameData\data\_lvl_pc\shell.lvl -> shell.lvl.backup (ЯКЩО ще нема — не перезаписуйте вже зроблену копію ванілі!)");
        report.Log(@"EN: 1. Back up: ...\GameData\data\_lvl_pc\shell.lvl -> shell.lvl.backup (IF not already done — do not overwrite an existing vanilla backup!)");
        report.Log($"UA: 2. Скопіюйте \"{outputPath}\" на місце shell.lvl (це ТИМЧАСОВА заміна лише для цього тесту).");
        report.Log($"EN: 2. Copy \"{outputPath}\" over shell.lvl (a TEMPORARY swap for this test only).");
        report.Log("UA: 3. Запустіть кампанію так, щоб побачити вступний ролик (краул) БУДЬ-ЯКОЇ місії на 1920x1080.");
        report.Log("EN: 3. Start the campaign so the intro movie (crawl) of ANY mission plays at 1920x1080.");
        report.Log("UA: 4. Зробіть ОДИН знімок екрана, поки грає ролик. Подивіться, чи є напис \"TEST\" по центру екрана, поверх відео.");
        report.Log("EN: 4. Take ONE screenshot while the movie plays. Check whether \"TEST\" appears centered on screen, over the video.");
        report.Log("UA: 5. Повідомте результат: видно \"TEST\" чи ні (жодних висновків без цього знімка).");
        report.Log("EN: 5. Report the result: is \"TEST\" visible or not (no conclusions without this screenshot).");
        report.Log("UA: 6. Після тесту поверніть оригінальний shell.lvl (цей файл НЕ для постійного використання).");
        report.Log("EN: 6. After testing, restore the original shell.lvl (this file is NOT for permanent use).");
    }

    report.Save();
    await Task.CompletedTask;
}

// UA: Перевірка розвилки щодо субтитрів роликів БЕЗ патчу .exe: екрани
//     кампанійських роликів переводяться на власний широкоформатний режим
//     гри (той самий, яким ваніль коригує навчальні ролики). Механізм і
//     чесні межі того, що це доводить, — у MovieRectModePatcher.cs.
// EN: Tests the movie-subtitle fork WITHOUT an .exe patch: the campaign movie
//     screens are switched to the game's own widescreen mode (the one vanilla
//     uses for tutorial movies). The mechanism, and the honest limits of what
//     this proves, are documented in MovieRectModePatcher.cs.
async Task RunGenerateMovieRectModeShell()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var shellPath = FindGameFile(bf2RefDir, "shell.lvl") ?? Path.Combine(bf2RefDir, "shell.lvl");
    var outputDir = Path.Combine(AppContext.BaseDirectory, "widescreen-output-movierect");

    var report = new DiagnosticReport("GenerateMovieRectModeShell");
    report.Log("UA: РЕЖИМ ПРЯМОКУТНИКА ВІДЕО — використовуємо власний механізм гри, не оверлей.");
    report.Log("EN: MOVIE RECT MODE — using the game's own mechanism, not an overlay.");
    report.Log($"UA: Вхідний (vanilla) shell.lvl: \"{shellPath}\" — цей файл НЕ змінюється.");
    report.Log($"EN: Input (vanilla) shell.lvl: \"{shellPath}\" — this file is NOT modified.");
    report.Log();

    var outputPath = GenerateMovieRectModeShellCommand.Run(
        report, shellPath, outputDir, "shell_movierect.lvl");

    if (outputPath is not null)
    {
        report.Log();
        report.Log("UA: === Як тестувати ===");
        report.Log("EN: === How to test ===");
        report.Log(@"UA: 1. Резервна копія: ...\GameData\data\_lvl_pc\shell.lvl -> shell.lvl.backup (ЯКЩО ще нема).");
        report.Log(@"EN: 1. Back up: ...\GameData\data\_lvl_pc\shell.lvl -> shell.lvl.backup (IF not already done).");
        report.Log($"UA: 2. Скопіюйте \"{outputPath}\" на місце shell.lvl.");
        report.Log($"EN: 2. Copy \"{outputPath}\" over shell.lvl.");
        report.Log("UA: 3. На 1920x1080 запустіть кампанію так, щоб побачити вступний ролик (краул).");
        report.Log("EN: 3. At 1920x1080, start the campaign so the intro movie (crawl) plays.");
        report.Log("UA: 4. Знімок. Дивимось ДВІ речі: (а) чи змінився розмір/положення самого ролика;");
        report.Log("UA:    (б) чи з'явився підпис-субтитр унизу.");
        report.Log("EN: 4. Screenshot. Look for TWO things: (a) did the movie's size/position change;");
        report.Log("EN:    (b) did the subtitle caption appear at the bottom.");
        report.Log("UA: 5. Якщо (а) так, а (б) ні — рішення про субтитри залежить від аспекту ЕКРАНА, а не прямокутника.");
        report.Log("EN: 5. If (a) yes but (b) no — the subtitle decision keys off the SCREEN aspect, not the rect.");
        report.Log("UA: 6. Якщо (а) ні — значить widescreen-значення ScriptCB_GetScreenInfo дорівнює 1.0 на цій системі.");
        report.Log("EN: 6. If (a) no — then the ScriptCB_GetScreenInfo widescreen value is 1.0 on this system.");
        report.Log("UA: 7. Для контролю: на 800x600 усе має лишитись ТОЧНО як у ванілі.");
        report.Log("EN: 7. Control check: at 800x600 everything must stay EXACTLY as in vanilla.");
    }

    report.Save();
    await Task.CompletedTask;
}

// UA: ПІДТВЕРДЖЕНО В ГРІ. Вхід — ВАНІЛЬНИЙ ingame.lvl з
//     reference-files\BF2 (копії у reference-files\BF2-UA-rem наразі
//     немає — на відміну від ФІКС HEAD, який бере вже локалізований
//     core.lvl). Зсуває позицію напису "Кількість бійців", шрифт не
//     чіпає. Механізм, точна інструкція, повний доказ безпечності й
//     підтвердження реальною грою — Core/Bf2Widescreen/
//     SpawnSelectUnitCountGapPatchBuilder.cs та docs/BF2_SPAWNSELECT_GAP_FIX.md.
// EN: CONFIRMED IN-GAME. Input — the VANILLA ingame.lvl
//     from reference-files\BF2 (no reference-files\BF2-UA-rem copy exists
//     yet — unlike HEAD FIX, which takes an already localized core.lvl).
//     Shifts the "Кількість бійців" label's position, the font is
//     untouched. Mechanism, exact instruction, full safety proof and the
//     real-game confirmation — Core/Bf2Widescreen/
//     SpawnSelectUnitCountGapPatchBuilder.cs and docs/BF2_SPAWNSELECT_GAP_FIX.md.
async Task RunGenerateSpawnSelectUnitCountGapFix()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var ingamePath = FindGameFile(bf2RefDir, "ingame.lvl");
    var outputDir = Path.Combine(AppContext.BaseDirectory, "widescreen-output-spawnselect-gapfix");

    var report = new DiagnosticReport("GenerateSpawnSelectUnitCountGapFix");
    report.Log("UA: ПІДТВЕРДЖЕНО РЕАЛЬНИМ ТЕСТОМ У ГРІ — докладно в docs/BF2_SPAWNSELECT_GAP_FIX.md.");
    report.Log("EN: CONFIRMED BY A REAL IN-GAME TEST — details in docs/BF2_SPAWNSELECT_GAP_FIX.md.");
    report.Log();

    if (ingamePath is null)
    {
        report.Log($"UA: ingame.lvl не знайдено ніде під \"{bf2RefDir}\" (шукали рекурсивно).");
        report.Log($"EN: ingame.lvl not found anywhere under \"{bf2RefDir}\" (searched recursively).");
        report.Save();
        return;
    }

    report.Log($"UA: Вхідний (vanilla) ingame.lvl: \"{ingamePath}\" — цей файл НЕ змінюється.");
    report.Log($"EN: Input (vanilla) ingame.lvl: \"{ingamePath}\" — this file is NOT modified.");
    report.Log();

    GenerateSpawnSelectUnitCountGapFixCommand.Run(report, ingamePath, outputDir, "ingame_spawnselect_gapfix.lvl");

    report.Save();
    await Task.CompletedTask;
}

// UA: КАНДИДАТ (НЕ ПІДТВЕРДЖЕНО У ГРІ). Вхід — УЖЕ ЛОКАЛІЗОВАНИЙ shell.lvl
//     (reference-files\BF2-UA-rem\shell.lvl; якщо його немає — вибір файлу
//     вручну) — той самий принцип, що й ФІКС HEAD: патчимо ГОТОВИЙ файл,
//     а не ванільний, бо результат має бути одразу придатним для тесту в
//     грі (з усією наявною локалізацією). На відміну від ФІКС HEAD, тут не
//     потрібен ванільний файл-еталон — індекси констант ("font",
//     "gamefont_tiny", "gamefont_small") резолвляться динамічно з ТОГО Ж
//     самого файлу, що патчиться. Механізм, точна інструкція й повний
//     доказ безпечності — Core/Bf2Widescreen/
//     FreeformInfoDescriptionFontPatchBuilder.cs.
// EN: CANDIDATE (NOT CONFIRMED IN-GAME). Input — the ALREADY LOCALIZED
//     shell.lvl (reference-files\BF2-UA-rem\shell.lvl; if missing — a
//     manual file picker) — the same principle as HEAD FIX: patch the
//     FINISHED file, not the vanilla one, so the result is immediately
//     ready for an in-game test (with all existing localization intact).
//     Unlike HEAD FIX, no vanilla reference file is needed here — the
//     constant indices ("font", "gamefont_tiny", "gamefont_small") are
//     resolved dynamically from the SAME file being patched. Mechanism,
//     exact instruction and full safety proof — Core/Bf2Widescreen/
//     FreeformInfoDescriptionFontPatchBuilder.cs.
async Task RunGenerateFreeformInfoDescriptionFontFix()
{
    var refRoot = Path.Combine(AppContext.BaseDirectory, "reference-files");
    var targetPath = Path.Combine(refRoot, "BF2-UA-rem", "shell.lvl");
    var outputDir = Path.Combine(AppContext.BaseDirectory, "widescreen-output-freeform-descfont");

    var report = new DiagnosticReport("GenerateFreeformInfoDescriptionFontFix");
    report.Log("UA: КАНДИДАТ НА ВИПРАВЛЕННЯ — ЩЕ НЕ ПІДТВЕРДЖЕНО РЕАЛЬНИМ ТЕСТОМ У ГРІ.");
    report.Log("EN: CANDIDATE FIX — NOT YET CONFIRMED BY A REAL IN-GAME TEST.");
    report.Log();

    if (!File.Exists(targetPath))
    {
        report.Log($"UA: \"{targetPath}\" не знайдено — оберіть локалізований shell.lvl вручну.");
        report.Log($"EN: \"{targetPath}\" not found — pick the localized shell.lvl manually.");
        var picked = NativeFileDialog.ShowOpenDialog("Виберіть локалізований shell.lvl / Select the localized shell.lvl");
        if (picked is null)
        {
            report.Log("UA: Файл не вибрано. / EN: No file selected.");
            report.Save();
            return;
        }
        targetPath = picked;
    }

    report.Log($"UA: Вхідний (уже локалізований) shell.lvl: \"{targetPath}\".");
    report.Log($"EN: Input (already localized) shell.lvl: \"{targetPath}\".");
    report.Log();

    GenerateFreeformInfoDescriptionFontFixCommand.Run(report, targetPath, outputDir, "shell_freeform_descfont.lvl");

    report.Save();
    await Task.CompletedTask;
}

// UA: ПОВНИЙ ПРОГІН по всіх .lvl гри у пошуку конфігу роликів. Причина
//     існування команди — чесно закрити питання, яке досі стояло на
//     припущенні: субтитри роликів знайдені в mission.lvl, але перевірено
//     на той момент був рівно один файл із понад п'яти сотень. Робота
//     процесорна, тож виконується локально; назовні йде короткий звіт.
//     Розгорнуте пояснення — у ScanMovieSubtitleConfigCommand.cs.
// EN: A FULL SWEEP over all of the game's .lvl files looking for the movie
//     config. The command exists to honestly close a question that until now
//     rested on an assumption: movie subtitles were found in mission.lvl, but
//     at that point exactly one file out of five hundred-odd had been checked.
//     The work is CPU-bound, so it runs locally; what leaves the machine is a
//     short report. Full explanation is in ScanMovieSubtitleConfigCommand.cs.
async Task RunScanMovieSubtitleConfig()
{
    // UA: Копія файлів гри вже лежить поруч із програмою, у
    //     reference-files\BF2 — там само, звідки беруть вхідні дані всі інші
    //     команди. Питати шлях діалогом означало б перепитувати те, що вже
    //     відомо, тому просто йдемо по всьому дереву цієї теки.
    // EN: A copy of the game's files already sits next to the program, in
    //     reference-files\BF2 — the same place every other command takes its
    //     input from. Asking for a path in a dialog would mean asking about
    //     something already known, so this simply sweeps that folder's tree.
    var gameRoot = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");

    var report = new DiagnosticReport("ScanMovieSubtitleConfig");
    report.Log("UA: ПОВНИЙ ПРОГІН: конфіг роликів (mcfg) у всіх .lvl файлах гри. Файли лише читаються.");
    report.Log("EN: FULL SWEEP: movie config (mcfg) across every .lvl file of the game. Files are read only.");
    report.Log($"UA: Корінь пошуку: \"{gameRoot}\"");
    report.Log($"EN: Search root: \"{gameRoot}\"");
    report.Log();

    ScanMovieSubtitleConfigCommand.Run(report, gameRoot);

    report.Save();
    await Task.CompletedTask;
}

// UA: ДІАГНОСТИЧНИЙ ЗОНД — НЕ фікс і НЕ production. Відповідає рівно на одне
//     питання: на широкому екрані субтитр ролика не малюється взагалі — чи
//     малюється, але його не видно? Механізм і межі того, що це доводить, —
//     у MovieSubtitleProbePatcher.cs.
// EN: A DIAGNOSTIC PROBE — not a fix, not production. It answers exactly one
//     question: on a widescreen display, is the movie subtitle not drawn at
//     all, or drawn but invisible? The mechanism, and the honest limits of
//     what it proves, are in MovieSubtitleProbePatcher.cs.
async Task RunGenerateMovieSubtitleProbeMission()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");

    // UA: mission.lvl, на відміну від shell.lvl/core.lvl, ніколи не лежав
    //     пласко просто в reference-files\BF2 — лише вкладено, у
    //     GameData\data\_lvl_pc\ (теку з ПОВНИМ деревом гри, додану пізніше
    //     для повного прогону mcfg). Тому шлях шукаємо рекурсивно, а не
    //     припускаємо пласку структуру.
    // EN: mission.lvl, unlike shell.lvl/core.lvl, was never placed flat
    //     directly in reference-files\BF2 — only nested, under
    //     GameData\data\_lvl_pc\ (the full game tree folder added later for
    //     the mcfg full sweep). So the path is searched recursively rather
    //     than assumed flat.
    var missionPath = FindGameFile(bf2RefDir, "mission.lvl");
    var outputDir = Path.Combine(AppContext.BaseDirectory, "movie-output-subtitle-probe");

    var report = new DiagnosticReport("GenerateMovieSubtitleProbeMission");
    report.Log("UA: ЗОНД ТАЙМІНГІВ СУБТИТРІВ — діагностика, НЕ production-патч.");
    report.Log("EN: SUBTITLE TIMING PROBE — diagnostics, NOT a production patch.");
    if (missionPath is null)
    {
        report.Log($"UA: mission.lvl не знайдено ніде під \"{bf2RefDir}\" (шукали рекурсивно).");
        report.Log($"EN: mission.lvl not found anywhere under \"{bf2RefDir}\" (searched recursively).");
        report.Save();
        return;
    }
    report.Log($"UA: Вхідний (vanilla) mission.lvl: \"{missionPath}\" — цей файл НЕ змінюється.");
    report.Log($"EN: Input (vanilla) mission.lvl: \"{missionPath}\" — this file is NOT modified.");
    report.Log();

    var outputPath = GenerateMovieSubtitleProbeMissionCommand.Run(
        report, missionPath, outputDir, "mission_subtitle_probe.lvl",
        MovieSubtitleProbePatcher.MygeetoSegmentHash);

    if (outputPath is not null)
    {
        report.Log();
        report.Log("UA: === Як тестувати ===");
        report.Log("EN: === How to test ===");
        report.Log(@"UA: 1. Резервна копія: ...\GameData\data\_lvl_pc\mission.lvl -> mission.lvl.backup (ЯКЩО ще нема).");
        report.Log(@"EN: 1. Back up: ...\GameData\data\_lvl_pc\mission.lvl -> mission.lvl.backup (IF not already done).");
        report.Log($"UA: 2. Скопіюйте \"{outputPath}\" на місце mission.lvl.");
        report.Log($"EN: 2. Copy \"{outputPath}\" over mission.lvl.");
        report.Log("UA: 3. КОНТРОЛЬ, 800x600: запустіть першу місію кампанії (Mygeeto).");
        report.Log("UA:    Перший підпис має висіти ВЕСЬ ролик, а не зникати через 2.23 с.");
        report.Log("UA:    Якщо він поводиться як у ванілі — патч не подіяв, і далі йти немає сенсу.");
        report.Log("EN: 3. CONTROL, 800x600: start the first campaign mission (Mygeeto).");
        report.Log("EN:    The first caption must stay for the WHOLE movie instead of vanishing after 2.23 s.");
        report.Log("EN:    If it behaves as in vanilla, the patch did not take effect and there is no point going on.");
        report.Log("UA: 4. ДОСЛІД, 1920x1080: той самий ролик, знімок.");
        report.Log("EN: 4. EXPERIMENT, 1920x1080: the same movie, screenshot.");
        report.Log("UA: 5. Тлумачення:");
        report.Log("UA:    (а) підпис З'ЯВИВСЯ -> малювання відбувається; ванільна невидимість — це час або позиція,");
        report.Log("UA:        а отже лікується з даних гри, без .exe;");
        report.Log("UA:    (б) підпису НЕМАЄ   -> виклику малювання немає взагалі; рішення приймається раніше за");
        report.Log("UA:        звернення до цих даних, і жодне редагування таймінгів його не змінить.");
        report.Log("EN: 5. Reading the result:");
        report.Log("EN:    (a) the caption APPEARS -> drawing happens; the vanilla invisibility is timing or position,");
        report.Log("EN:        hence fixable from the game's data, without touching the .exe;");
        report.Log("EN:    (b) NO caption          -> no draw call at all; the decision is taken before this data is");
        report.Log("EN:        consulted, and no timing edit will change it.");
        report.Log("UA: 6. Після тесту поверніть оригінальний mission.lvl — цей файл НЕ для постійного вжитку.");
        report.Log("EN: 6. After testing, restore the original mission.lvl — this file is NOT for permanent use.");
    }

    report.Save();
    await Task.CompletedTask;
}

// UA: ЗОНД ШРИФТА СУБТИТРІВ. Наступний крок після зонда таймінгів: той
//     показав, що форсовані дані на 16:9 нічого не змінюють, а розбір exe
//     показав, що умови за аспектом у шляху субтитрів немає взагалі. Лишився
//     один неперевірений вхід текстового об'єкта — шрифт.
//     Обґрунтування й адреси — у MovieSubtitleFontPatcher.cs.
// EN: SUBTITLE FONT PROBE. The next step after the timing probe: that one
//     showed forced data changes nothing at 16:9, and the exe analysis
//     showed the subtitle path has no aspect condition at all. One untested
//     input of the text object remains — the
//     font. Rationale and addresses are in MovieSubtitleFontPatcher.cs.
async Task RunGenerateMovieSubtitleFontProbe()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var missionPath = FindGameFile(bf2RefDir, "mission.lvl");
    var outputDir = Path.Combine(AppContext.BaseDirectory, "movie-output-subtitle-font-probe");

    var report = new DiagnosticReport("GenerateMovieSubtitleFontProbe");
    report.Log("UA: ЗОНД ШРИФТА СУБТИТРІВ — діагностика, НЕ production-патч.");
    report.Log("EN: SUBTITLE FONT PROBE — diagnostics, NOT a production patch.");
    if (missionPath is null)
    {
        report.Log($"UA: mission.lvl не знайдено ніде під \"{bf2RefDir}\" (шукали рекурсивно).");
        report.Log($"EN: mission.lvl not found anywhere under \"{bf2RefDir}\" (searched recursively).");
        report.Save();
        return;
    }

    report.Log($"UA: Вхідний (vanilla) mission.lvl: \"{missionPath}\" — цей файл НЕ змінюється.");
    report.Log($"EN: Input (vanilla) mission.lvl: \"{missionPath}\" — this file is NOT modified.");
    report.Log();

    GenerateMovieSubtitleFontProbeCommand.Run(
        report, missionPath, outputDir, "mission_subtitle_font_probe.lvl",
        MovieSubtitleFontPatcher.GameFontLarge);

    report.Save();
    await Task.CompletedTask;
}

// UA: Дивись докладний коментар про мету/межі/законність на початку
//     BF1LocalizationTool.Core/Bf2Exe/MovieSubtitleAlphaScanner.cs — ЛИШЕ
//     читає BattlefrontII.exe, нічого не пише й не патчить.
// EN: See the detailed purpose/scope/legality comment at the top of
//     BF1LocalizationTool.Core/Bf2Exe/MovieSubtitleAlphaScanner.cs — READS
//     BattlefrontII.exe ONLY, writes and patches nothing.
async Task RunAnalyzeMovieSubtitleAlphaSource()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var exePath = FindGameFile(bf2RefDir, "BattlefrontII.exe");

    var report = new DiagnosticReport("AnalyzeMovieSubtitleAlphaSource");
    report.Log("UA: ПОШУК ДЖЕРЕЛА АЛЬФИ СУБТИТРУ.");
    report.Log("EN: SUBTITLE ALPHA SOURCE SEARCH.");
    if (exePath is null)
    {
        report.Log($"UA: BattlefrontII.exe не знайдено ніде під \"{bf2RefDir}\" (шукали рекурсивно).");
        report.Log($"EN: BattlefrontII.exe not found anywhere under \"{bf2RefDir}\" (searched recursively).");
        report.Save();
        return;
    }

    AnalyzeMovieSubtitleAlphaSourceCommand.Run(report, exePath);

    report.Save();
    await Task.CompletedTask;
}

// UA: КОНКРЕТНИЙ пункт меню, що доводить походження d3d9.dll (для
//     модерації NexusMods тощо) — розпаковує з ВЛАСНИХ вбудованих
//     ресурсів ЦІЄЇ ЗБІРКИ й бінарник, і його джерело, рахує SHA-256
//     наживо, і, за наявності компілятора, реально перезбирає та звіряє
//     байт-у-байт. Сама логіка — у GenerateD3D9FixProvenanceReportCommand.cs
//     (BF1LocalizationTool.Diagnostic) поверх MovieSubtitleD3D9FixProvenance.cs
//     (BF1LocalizationTool.Core).
// EN: The CONCRETE menu item that proves d3d9.dll's provenance (for
//     NexusMods moderation, etc) — extracts, from THIS BUILD's own
//     embedded resources, both the binary and its source, hashes them
//     live with SHA-256, and, if a compiler is present, actually rebuilds
//     and compares byte-for-byte. The logic itself lives in
//     GenerateD3D9FixProvenanceReportCommand.cs (BF1LocalizationTool.Diagnostic)
//     on top of MovieSubtitleD3D9FixProvenance.cs (BF1LocalizationTool.Core).
async Task RunGenerateD3D9FixProvenanceReport()
{
    var outputDir = Path.Combine(AppContext.BaseDirectory, "d3d9-fix-provenance");

    var report = new DiagnosticReport("GenerateD3D9FixProvenanceReport");
    await GenerateD3D9FixProvenanceReportCommand.RunAsync(report, outputDir);

    report.Save();
}

// UA: Шукає файл/теку гри за іменем під заданим коренем — РЕКУРСИВНО.
//     Структура reference-files\<Гра>\ НЕ фіксована: одні файли (shell.lvl,
//     core.lvl) лежать пласко ще з початку проєкту, інші (mission.lvl,
//     movies\) з'явились пізніше вкладеними в повне дерево гри
//     (GameData\data\_lvl_pc\…), і наперед невідомо, що саме де опиниться
//     наступного разу — тому кожен пошук файлу/теки під reference-files\
//     має йти саме так, а не за жорстко зашитим пласким шляхом.
//     Повертає перший знайдений збіг (case-insensitive) або null.
// EN: Finds a game file/folder by name under the given root — RECURSIVELY.
//     The reference-files\<Game>\ layout is NOT fixed: some files (shell.lvl,
//     core.lvl) have sat there flat since early in the project, others
//     (mission.lvl, movies\) arrived later nested inside the full game tree
//     (GameData\data\_lvl_pc\…), and there is no telling in advance what
//     lands where next time — so every lookup under reference-files\ should
//     go this way, not through a hardcoded flat path.
//     Returns the first match found (case-insensitive) or null.
static string? FindGameFile(string rootDir, string fileName)
{
    if (!Directory.Exists(rootDir))
        return null;

    return Directory.EnumerateFiles(rootDir, fileName, SearchOption.AllDirectories)
        .FirstOrDefault();
}

// UA: Те саме, але для теки (наприклад "movies") — той самий принцип: не
//     припускати пласку структуру.
// EN: The same, but for a folder (e.g. "movies") — the same principle: do
//     not assume a flat layout.
static string? FindGameDir(string rootDir, string dirName)
{
    if (!Directory.Exists(rootDir))
        return null;

    return Directory.EnumerateDirectories(rootDir, dirName, SearchOption.AllDirectories)
        .FirstOrDefault();
}

// UA: ПІДТВЕРДЖЕНИЙ ЗНІМКАМИ фікс — УЖЕ в production
//     (shell_layout.lvl через GenerateAnchorFixShellCommand). Ця команда —
//     окрема ІЗОЛЬОВАНА збірка (shell_bgfix.lvl) для точкового тестування
//     нових екранів/текстур bg_texture без перезбирання основного патча.
//     Див. розгорнутий коментар у GenerateBackgroundSizeFixShellCommand.cs і
//     в AnchorInheritancePatchBuilder.cs (розділ "фікс оверскан фону" перед
//     BuildDispatchWrapper).
// EN: A fix CONFIRMED BY SCREENSHOTS — already in
//     production (shell_layout.lvl via GenerateAnchorFixShellCommand). This
//     command is a separate ISOLATED build (shell_bgfix.lvl) for
//     spot-testing new screens/bg_texture values without rebuilding the
//     main patch. See the detailed comment in
//     GenerateBackgroundSizeFixShellCommand.cs and in
//     AnchorInheritancePatchBuilder.cs (the "background overscan fix"
//     section before BuildDispatchWrapper).
async Task RunGenerateBackgroundSizeFixShell()
{
    var bf2RefDir = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2");
    var shellPath = FindGameFile(bf2RefDir, "shell.lvl") ?? Path.Combine(bf2RefDir, "shell.lvl");
    var outputDir = Path.Combine(AppContext.BaseDirectory, "widescreen-output-bgfix");

    var report = new DiagnosticReport("GenerateBackgroundSizeFixShell");
    report.Log("UA: Фікс фону BF2 — вже в production (shell_layout.lvl). Ця збірка — для точкових тестів.");
    report.Log("EN: BF2 background fix — already in production (shell_layout.lvl). This build is for spot tests.");
    report.Log($"UA: Вхідний (vanilla) shell.lvl: \"{shellPath}\" — цей файл НЕ змінюється.");
    report.Log($"EN: Input (vanilla) shell.lvl: \"{shellPath}\" — this file is NOT modified.");
    report.Log();

    var outputPath = GenerateBackgroundSizeFixShellCommand.Run(report, shellPath, outputDir, "shell_bgfix.lvl");

    if (outputPath is not null)
    {
        report.Log();
        report.Log("UA: === Як тестувати ===");
        report.Log("EN: === How to test ===");
        report.Log(@"UA: 1. Резервна копія: ...\GameData\data\_lvl_pc\shell.lvl -> shell.lvl.backup (ЯКЩО ще нема).");
        report.Log(@"EN: 1. Back up: ...\GameData\data\_lvl_pc\shell.lvl -> shell.lvl.backup (IF not already done).");
        report.Log($"UA: 2. Скопіюйте \"{outputPath}\" на місце shell.lvl (це ТИМЧАСОВА заміна лише для цього тесту).");
        report.Log($"EN: 2. Copy \"{outputPath}\" over shell.lvl (a TEMPORARY swap for this test only).");
        report.Log("UA: 3. Перейдіть на екран із ЩЕ НЕ перевіреною текстурою bg_texture (напр. \"Миттєвий бій -> Налаштування\" для single_player_option). Фон має бути на весь екран, без чорних дір і швів.");
        report.Log("EN: 3. Navigate to a screen with a bg_texture NOT yet checked (e.g. \"Instant Action -> Options\" for single_player_option). The background should fill the screen, no black gaps or seams.");
        report.Log("UA: 4. Зробіть знімок(и) і надішліть — за правилом 3 нічого не вважається виправленим без знімка з нулем дефектів.");
        report.Log("EN: 4. Take screenshot(s) and send them — per rule 3, nothing counts as fixed without a zero-defect screenshot.");
        report.Log("UA: 5. Після тесту поверніть shell_layout.lvl (production, вже з цим фіксом) або ванільний shell.lvl.");
        report.Log("EN: 5. After testing, restore shell_layout.lvl (production, already has this fix) or the vanilla shell.lvl.");
    }

    report.Save();
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
//     BF2 треба ЗБІЛЬШУВАТИ. Порядок кроків фіксований і важливий:
//     спершу GenerateEnlargedFontCoreCommand збільшує ВАНІЛЬНИЙ (ще без
//     кирилиці) файл — цей upscale застосовується ЛИШЕ до англійських
//     бітмапів (для них це неминуче: немає векторного джерела гри,
//     лише запечені пікселі). ПОТІМ GenerateNoDonorCyrillicCoreCommand
//     читає ВЖЕ ЗБІЛЬШЕНИЙ файл — його GlyphMetricModel.DeriveReference
//     САМ вимірює baselineOffset/CapHeightGame/CoreHeightGame з
//     РЕАЛЬНИХ (уже збільшених) англійських записів, тож кириличні
//     гліфи рендеряться з Fira Sans ОДРАЗУ під фінальний, великий
//     розмір — ОДИН прохід TTF→піксель, без повторного
//     bicubic-розмиття. Зворотний порядок (спершу кирилиця в
//     маленький атлас, потім ×1.5 bicubic-upscale всього атласу
//     разом) дає ПОДВІЙНЕ розмиття кириличних пікселів
//     (TTF-рендер-у-малий-бокс, потім upscale-у-великий) — саме тому
//     цей порядок кроків не можна міняти місцями. Жодних змін у самій
//     математиці GenerateNoDonorCyrillicCoreCommand не потрібно — вона
//     й так рахує все з вхідного файлу "як є".
// EN: BF2 does NOT support 1080p natively (unlike BF1) — so BF2 fonts
//     need to be ENLARGED. The step order is fixed and matters:
//     GenerateEnlargedFontCoreCommand first enlarges the VANILLA (still
//     Cyrillic-free) file — this upscale applies ONLY to the English
//     bitmaps (unavoidable for them: no vector source exists for the
//     game's own font, only baked pixels). THEN
//     GenerateNoDonorCyrillicCoreCommand reads the ALREADY-enlarged
//     file — its GlyphMetricModel.DeriveReference measures
//     baselineOffset/CapHeightGame/CoreHeightGame from the REAL
//     (already-enlarged) English records itself, so Cyrillic glyphs
//     render from Fira Sans DIRECTLY at the final, big size — ONE
//     TTF→pixel pass, no repeated bicubic blur. The reverse order
//     (Cyrillic into a small atlas first, then ×1.5 bicubic-upscaling
//     the whole atlas together) causes DOUBLE blur on the Cyrillic
//     pixels (TTF-render-into-small-box, then upscale-into-big-box) —
//     which is exactly why this step order must not be swapped. No
//     changes to GenerateNoDonorCyrillicCoreCommand's own math were
//     needed — it already measures everything from the input file "as is".
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
    //     against the base game's table). The default is the already
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
    //     страхувальник (запис там нешкідливий, а помилитись, яка таблиця
    //     активна, легко, тож дешевше покласти в обидва).
    // EN: Both files: the base one is what actually works; the add-on one
    //     is a safety net (the record is harmless there, and it's easy to
    //     get "which table is active" wrong, so covering both is the
    //     cheaper choice).
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

// UA: ФІКС HEAD. Вхід — уже згенерований український core.lvl
//     (reference-files\BF2-UA-rem\core.lvl; якщо його немає — вибір файлу),
//     еталон запасу висоти — ванільний core.lvl з reference-files\BF2.
//     Механізм і адреси — Core/Fonts/FontHeadHeightFix.cs; причина —
//     FONT_FORMAT_SPEC.md.
// EN: HEAD FIX. Input — the already generated Ukrainian core.lvl
//     (reference-files\BF2-UA-rem\core.lvl; if missing — a file picker),
//     the height-margin reference — the vanilla core.lvl from
//     reference-files\BF2. Mechanism and addresses — Core/Fonts/
//     FontHeadHeightFix.cs; cause — FONT_FORMAT_SPEC.md.
async Task RunGenerateFontHeadHeightFixCore()
{
    var refRoot = Path.Combine(AppContext.BaseDirectory, "reference-files");
    var vanillaPath = FindGameFile(Path.Combine(refRoot, "BF2"), "core.lvl");
    var targetPath = Path.Combine(refRoot, "BF2-UA-rem", "core.lvl");
    var outputDir = Path.Combine(AppContext.BaseDirectory, "font-output-headfix", "BF2");

    var report = new DiagnosticReport("GenerateFontHeadHeightFixCore");
    report.Log("UA: ФІКС HEAD — виправлення заявленої висоти шрифтів у готовому core.lvl.");
    report.Log("EN: HEAD FIX — correcting the declared font height in a finished core.lvl.");
    report.Log();

    if (!File.Exists(targetPath))
    {
        report.Log($"UA: \"{targetPath}\" не знайдено — оберіть український core.lvl вручну.");
        report.Log($"EN: \"{targetPath}\" not found — pick the Ukrainian core.lvl manually.");
        var picked = NativeFileDialog.ShowOpenDialog("Виберіть український core.lvl / Select the Ukrainian core.lvl");
        if (picked is null)
        {
            report.Log("UA: Файл не вибрано. / EN: No file selected.");
            report.Save();
            return;
        }
        targetPath = picked;
    }

    if (vanillaPath is null)
    {
        report.Log($"UA: Ванільний core.lvl не знайдено ніде під \"{Path.Combine(refRoot, "BF2")}\" (шукали рекурсивно).");
        report.Log($"EN: Vanilla core.lvl not found anywhere under \"{Path.Combine(refRoot, "BF2")}\" (searched recursively).");
        report.Save();
        return;
    }

    GenerateFontHeadHeightFixCoreCommand.Run(report, targetPath, vanillaPath, outputDir, "core.lvl");

    report.Save();
    await Task.CompletedTask;
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

    // UA: Лише BF2 — саме тут потрібне збільшення шрифту (меню+гра).
    //     Питання розміру для BF1 лишається відкритим.
    // EN: BF2 only — this is where the font needs enlarging (menu+game).
    //     The size question for BF1 stays open.
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

    const string fontFamilyName = "FiraSans-SemiBold.ttf"; // UA/EN: значення — шлях файлу, не назва родини (GDI+ обрізає/зливає назви, FONT_FORMAT_SPEC.md 11.13) / value is a file path, not a family name (GDI+ truncates/merges names, FONT_FORMAT_SPEC.md 11.13)

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
//     Вивід через DiagnosticReport — зберігає у спільну теку
//     "diagnostic-output" біля .exe, а не поруч із вхідним файлом.
// EN: DETAILED MODE for one file (launched with arguments):
//     full chunk histogram, all NAME strings, --tree, --grep, --dump-font.
//     Output goes through DiagnosticReport — saves to the shared
//     "diagnostic-output" folder next to the .exe, instead of next to the
//     input file.
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
//     звичайний діалог вибору файлу.
//
//     Щоб скористатись: одноразово створи теки
//     "{тека exe}\reference-files\BF1\" і "...\BF2\" та поклади туди
//     відповідні core.lvl.
// EN: Automatic core.lvl lookup in a fixed folder next to the .exe — so
//     the file doesn't need to be picked manually every time. Looks for
//     "{exe folder}\reference-files\BF1\core.lvl" and "...\BF2\core.lvl".
//     If found — uses it directly (with a message stating exactly which
//     file, so there's no surprise about what actually got opened). If
//     not — falls back to the regular file picker dialog.
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
