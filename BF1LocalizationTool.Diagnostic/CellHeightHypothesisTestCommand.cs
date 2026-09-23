// =============================================================================
// BF1LocalizationTool.Diagnostic — CellHeightHypothesisTestCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: КОНТРОЛЬОВАНИЙ ЕКСПЕРИМЕНТ, не постійне виправлення. Мета — дати
//     відповідь на ОДНЕ конкретне питання, перш ніж чіпати
//     GlyphBoxFitRenderer/GrowthResolver: чи керує FBOD-поле CellHeight
//     вертикальним масштабуванням/позиціюванням гліфа в самій грі?
//
//     Контекст (GenerateLocalizedCoreCommand, аналіз усіх 66 літер):
//     CellHeight для НОВИХ кириличних записів ЗАВЖДИ копіюється з донора
//     без змін (GlyphAtlasPatcher.BuildReplacements) — і воно РЕАЛЬНО
//     різне: більшість gamefont_large-записів мають CellHeight=30, але
//     є явні "викиди" (е/в=34, ю=32, і=28, ж=19). FontGlyphRecord.cs
//     перевіряє ЛИШЕ кореляцію CellHeight з розміром чорнила — чи саме
//     воно керує видимим розміром/позицією, ця перевірка не покриває.
//     У грі спостерігається: "ж" (з CellHeight=19, майже вдвічі менше за
//     сусідів) "левітує" — сидить явно вище базової лінії сусідніх літер.
//
//     Що робить цей інструмент:
//       1. Читає ВЖЕ згенерований output/core.lvl (не оригінал — щоб не
//          дублювати всю логіку призначення/росту/рендеру, лише
//          перезаписати ОДНЕ поле поверх уже коректно вставлених
//          пікселів/UV).
//       2. Для КОЖНОГО шрифту рахує "типове" CellHeight — медіану серед
//          ЗВИЧАЙНИХ (не кириличних, не зачеплених ін'єкцією) записів
//          цього шрифту — і примусово ставить ЦЕ значення для ВСІХ
//          кириличних записів (за таблицею cyrillic-code-table.json).
//          УСЕ решта (U0-V1, XAdvance, InkWidth, Bearing, ReservedByte4,
//          пікселі BODY) лишається БЕЗ ЖОДНИХ змін.
//       3. Пише результат у ОКРЕМУ теку "output-celltest" (НЕ поверх
//          "output") — щоб можна було порівняти обидва варіанти в грі
//          без повторної генерації.
//
//     Якщо після цього "ж" (і будь-які інші літери з нетиповим CellHeight)
//     перестають "стрибати"/"левітувати" в грі — CellHeight підтверджено
//     як вертикальний драйвер, і REAL fix має примусово вирівнювати це
//     поле в самій продакшн-логіці (CyrillicFontInjector/GlyphAtlasPatcher),
//     а не лише в тестовій теці. Якщо нічого не зміниться — гіпотеза
//     відхилена, і треба шукати причину деінде.
// EN: A CONTROLLED EXPERIMENT, not a permanent fix. Goal — answer ONE
//     specific question before touching GlyphBoxFitRenderer/GrowthResolver:
//     does the FBOD field CellHeight drive a glyph's vertical
//     scaling/positioning in the actual game?
//
//     Context (GenerateLocalizedCoreCommand, full 66-letter analysis):
//     CellHeight for NEW Cyrillic records is ALWAYS copied from the donor
//     unchanged (GlyphAtlasPatcher.BuildReplacements) — and it genuinely
//     varies: most gamefont_large records have CellHeight=30, but there
//     are clear outliers (е/в=34, ю=32, і=28, ж=19). FontGlyphRecord.cs
//     checks ONLY the CORRELATION of CellHeight with ink size — whether
//     it actually drives visible size/position is not covered by that
//     check. Observed in-game: "ж" (CellHeight=19, almost
//     half its neighbors') "levitates" — sits noticeably above the
//     neighboring letters' baseline.
//
//     What this tool does:
//       1. Reads the ALREADY-generated output/core.lvl (not the original —
//          to avoid duplicating the whole assignment/growth/render logic,
//          just overwrite ONE field on top of already-correctly-inserted
//          pixels/UV).
//       2. For EACH font, computes a "typical" CellHeight — the median
//          among ORDINARY (non-Cyrillic, untouched by injection) records
//          of that font — and forces THAT value onto ALL Cyrillic records
//          (per cyrillic-code-table.json). EVERYTHING else (U0-V1,
//          XAdvance, InkWidth, Bearing, ReservedByte4, BODY pixels) is
//          left COMPLETELY UNCHANGED.
//       3. Writes the result into a SEPARATE "output-celltest" folder
//          (NOT over "output") — so both variants can be compared in-game
//          without regenerating anything.
//
//     If afterward "ж" (and any other letter with an atypical CellHeight)
//     stops "jumping"/"levitating" in-game — CellHeight is confirmed as a
//     vertical driver, and the REAL fix must force this field uniform in
//     the production logic itself (CyrillicFontInjector/GlyphAtlasPatcher),
//     not just in the test folder. If nothing changes — the hypothesis is
//     rejected, and the cause must be sought elsewhere.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Localization;

namespace BF1LocalizationTool.Diagnostic;

public static class CellHeightHypothesisTestCommand
{
    // UA: outputCoreLvlPath — уже згенерований output/core.lvl (звідси
    //     читаємо ГОТОВЕ дерево з коректно вставленими пікселями/UV).
    //     testOutputCoreLvlPath — куди писати варіант із примусовим
    //     CellHeight (окрема тека, "output" не чіпається).
    // EN: outputCoreLvlPath — the already-generated output/core.lvl (the
    //     READY tree with correctly inserted pixels/UV is read from
    //     here). testOutputCoreLvlPath — where to write the forced-
    //     CellHeight variant (a separate folder, "output" is untouched).
    public static void Run(DiagnosticReport report, string outputCoreLvlPath, string testOutputCoreLvlPath, string label)
    {
        if (!File.Exists(outputCoreLvlPath))
        {
            report.Log($"UA: [{label}] Файл не знайдено: {outputCoreLvlPath} — спершу згенеруй (7 → 1).");
            report.Log($"EN: [{label}] File not found: {outputCoreLvlPath} — generate it first (7 → 1).");
            return;
        }

        var codeTable = CyrillicCodeTable.TryLoadNextTo(outputCoreLvlPath);
        if (codeTable is null)
        {
            report.Log($"UA: [{label}] Файл-супутник cyrillic-code-table.json не знайдено поруч із {outputCoreLvlPath}.");
            report.Log($"EN: [{label}] Sidecar cyrillic-code-table.json not found next to {outputCoreLvlPath}.");
            return;
        }

        // UA: HashSet<ushort>, не <byte> — FontGlyphRecord.Code є ushort
        //     (порівнюється напряму з r.Code нижче, без приведення типів
        //     туди-сюди на кожному записі).
        // EN: HashSet<ushort>, not <byte> — FontGlyphRecord.Code is ushort
        //     (compared directly against r.Code below, no back-and-forth
        //     casting per record).
        var injectedCodes = codeTable.Letters.Select(e => (ushort)e.Code).ToHashSet();

        var root = UcfbReader.ReadFile(outputCoreLvlPath);
        var fonts = FontChunkLocator.FindAll(root);
        var replacements = new Dictionary<long, byte[]>();

        report.Log($"UA: [{label}] Гіпотеза CellHeight — примусово уніфікується для {injectedCodes.Count} кириличних кодів на шрифт:");
        report.Log($"EN: [{label}] CellHeight hypothesis — forced uniform for {injectedCodes.Count} Cyrillic codes per font:");

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var records = FontGlyphTable.Parse(fbod.RawData).ToList();

            // UA: "Типове" CellHeight цього шрифту — медіана серед
            //     ЗВИЧАЙНИХ (не кириличних) записів. Це те значення, яке
            //     рушій бачить для абсолютної більшості англійського
            //     тексту цього шрифту — найбезпечніший кандидат на
            //     "нейтральне" значення для експерименту.
            // EN: This font's "typical" CellHeight — the median among
            //     ORDINARY (non-Cyrillic) records. This is the value the
            //     engine sees for the vast majority of this font's
            //     English text — the safest candidate for a "neutral"
            //     experimental value.
            var ordinaryCellHeights = records
                .Where(r => !injectedCodes.Contains(r.Code))
                .Select(r => (int)r.CellHeight)
                .OrderBy(v => v)
                .ToList();

            if (ordinaryCellHeights.Count == 0)
            {
                report.Log($"UA: [{label}] {font.BaseName}: немає звичайних (некириличних) записів — пропущено.");
                report.Log($"EN: [{label}] {font.BaseName}: no ordinary (non-Cyrillic) records — skipped.");
                continue;
            }

            var typicalCellHeight = (byte)ordinaryCellHeights[ordinaryCellHeights.Count / 2];

            var changedCount = 0;
            var updatedRecords = new List<FontGlyphRecord>(records.Count);
            var beforeAfter = new List<(char Character, ushort Code, byte Old, byte New)>();

            foreach (var r in records)
            {
                if (injectedCodes.Contains(r.Code) && r.CellHeight != typicalCellHeight)
                {
                    var ch = codeTable.Letters.First(e => e.Code == r.Code).Character[0];
                    beforeAfter.Add((ch, r.Code, r.CellHeight, typicalCellHeight));
                    updatedRecords.Add(r with { CellHeight = typicalCellHeight });
                    changedCount++;
                }
                else
                {
                    updatedRecords.Add(r);
                }
            }

            replacements[fbod.FileDataOffset] = FontGlyphTable.Serialize(updatedRecords);

            report.Log($"UA: [{label}] {font.BaseName}: типове CellHeight={typicalCellHeight} (медіана {ordinaryCellHeights.Count} звичайних записів). Змінено {changedCount} кириличних записів:");
            report.Log($"EN: [{label}] {font.BaseName}: typical CellHeight={typicalCellHeight} (median of {ordinaryCellHeights.Count} ordinary records). Changed {changedCount} Cyrillic records:");
            foreach (var (ch, code, oldV, newV) in beforeAfter.OrderBy(x => x.Character))
                report.Log($"    {ch}  code=0x{code:X2}  CellHeight {oldV} → {newV}");
            report.Log();
        }

        var testOutputDir = Path.GetDirectoryName(testOutputCoreLvlPath)!;
        Directory.CreateDirectory(testOutputDir);
        var newBytes = UcfbWriter.WriteFile(root, replacements);
        File.WriteAllBytes(testOutputCoreLvlPath, newBytes);

        // UA: cyrillic-code-table.json теж копіюється поруч — жодне поле
        //     кодування (байт-код↔літера) не змінилось, лише CellHeight,
        //     тож GUI-кодек однаково коректний і для цього тестового файлу.
        // EN: cyrillic-code-table.json is also copied alongside — no
        //     encoding field (byte-code↔letter) changed, only CellHeight,
        //     so the GUI codec is equally correct for this test file too.
        var sourceTablePath = Path.Combine(Path.GetDirectoryName(outputCoreLvlPath)!, CyrillicCodeTable.DefaultFileName);
        var destTablePath = Path.Combine(testOutputDir, CyrillicCodeTable.DefaultFileName);
        if (File.Exists(sourceTablePath))
            File.Copy(sourceTablePath, destTablePath, overwrite: true);

        report.Log($"UA: [{label}] ЕКСПЕРИМЕНТАЛЬНИЙ файл (окремо від \"output\", той не зачеплено): {testOutputCoreLvlPath}");
        report.Log($"EN: [{label}] EXPERIMENTAL file (separate from \"output\", which is untouched): {testOutputCoreLvlPath}");
        report.Log($"UA: [{label}] Постав ЦЕЙ файл у гру ЗАМІСТЬ core.lvl, зроби скріншот — якщо \"ж\" (та інші вирівняні літери) перестали стрибати/левітувати, CellHeight підтверджено як причина.");
        report.Log($"EN: [{label}] Put THIS file into the game INSTEAD of core.lvl, take a screenshot — if \"ж\" (and other now-uniform letters) stop jumping/levitating, CellHeight is confirmed as the cause.");
        report.Log();
    }
}
