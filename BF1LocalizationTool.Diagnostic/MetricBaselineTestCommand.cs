// =============================================================================
// BF1LocalizationTool.Diagnostic — MetricBaselineTestCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: КОНТРОЛЬОВАНИЙ ЕКСПЕРИМЕНТ — доводить ПОВНУ модель рендеру,
//     виведену з англійського шрифту, перш ніж переписувати продакшн.
//
//     ВИВЕДЕНА МОДЕЛЬ (інваріант перевірено на 28 англійських літерах
//     gamefont_large, БЕЗ ЖОДНОГО винятку):
//         Bearing + висота_UV_бокса(px) = CellHeight
//     Гра малює UV-бокс, вертикально розтягнутий у екранний діапазон
//     [pen+Bearing, pen+CellHeight]; базова лінія рядка = pen+CellHeight
//     (=30 для gamefont_large — медіана CellHeight звичайних літер);
//     Bearing — відступ ВЕРХУ чорнила від верху рядка; CellHeight —
//     позиція НИЗУ чорнила. Підтверджено експериментом CellHeight
//     (ж: 19→30 → виросла й сіла на лінію): поле керує саме вертикаллю.
//
//     ЩО РОБИТЬ ЦЕЙ ТЕСТ (мінімальна зміна, що доводить модель):
//       Для КОЖНОГО кириличного запису (за cyrillic-code-table.json):
//         CellHeight := базовий_відступ шрифту (медіана CellHeight
//                       звичайних, некириличних записів — =30 для large).
//                       Тобто НИЗ бокса сідає рівно на спільну базову
//                       лінію рядка.
//         Bearing    := CellHeight − висота_UV_бокса(px), затиснуте [0,..].
//                       Так CellHeight−Bearing = висота_бокса → рендер
//                       БЕЗ вертикального розтягу (пікселі чіткі), а низ
//                       бокса — на базовій лінії.
//       УСЕ решта (U0-V1, XAdvance, InkWidth, ReservedByte4, пікселі
//       BODY) — БЕЗ змін. Пишеться в ОКРЕМУ теку "output-metrictest".
//
//     СВІДОМЕ СПРОЩЕННЯ ЦЬОГО ТЕСТУ (не продакшн): усі літери трактуються
//     як такі, що сидять НА базовій лінії (низ бокса = базова лінія). Для
//     літер зі СПРАВЖНІМ хвостом нижче базової (р, у, ц, щ, д) це поставить
//     хвіст трохи зависоко — але для ДОВЕДЕННЯ моделі досить перевірити,
//     що ВЕСЬ рядок вирівнявся по низу (літери перестали стрибати по
//     вертикалі). Якщо так — модель підтверджено, і продакшн-фікс уже
//     рахуватиме descent коректно (CellHeight = базовий_відступ + хвіст).
// EN: A CONTROLLED EXPERIMENT — proves the FULL render model
//     derived from the English font, before rewriting production.
//
//     DERIVED MODEL (invariant verified on 28 English gamefont_large
//     letters, with NO exception):
//         Bearing + UV_box_height(px) = CellHeight
//     The game draws the UV box stretched vertically into screen range
//     [pen+Bearing, pen+CellHeight]; the line baseline = pen+CellHeight
//     (=30 for gamefont_large — the median CellHeight of ordinary
//     letters); Bearing is the ink TOP inset from the line top;
//     CellHeight is the ink BOTTOM position. Confirmed by the CellHeight
//     experiment (ж: 19→30 → grew and dropped to the baseline): the field
//     drives the vertical axis.
//
//     WHAT THIS TEST DOES (the minimal change that proves the model):
//       For EACH Cyrillic record (per cyrillic-code-table.json):
//         CellHeight := the font's baseline offset (median CellHeight of
//                       ordinary, non-Cyrillic records — =30 for large).
//                       So the box BOTTOM lands exactly on the line's
//                       shared baseline.
//         Bearing    := CellHeight − UV_box_height(px), clamped [0,..].
//                       So CellHeight−Bearing = box height → rendered with
//                       NO vertical stretch (crisp pixels), box bottom on
//                       the baseline.
//       EVERYTHING else (U0-V1, XAdvance, InkWidth, ReservedByte4, BODY
//       pixels) is UNCHANGED. Written to a SEPARATE "output-metrictest"
//       folder.
//
//     DELIBERATE SIMPLIFICATION OF THIS TEST (not production): every
//     letter is treated as sitting ON the baseline (box bottom = baseline).
//     For letters with a REAL descender (р, у, ц, щ, д) this places the
//     tail slightly too high — but to PROVE the model it's enough to see
//     that the WHOLE line aligned along the bottom (letters stopped
//     jumping vertically). If so — the model is confirmed, and the
//     production fix will compute descent correctly (CellHeight = baseline
//     offset + tail).
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Localization;

namespace BF1LocalizationTool.Diagnostic;

public static class MetricBaselineTestCommand
{
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

        var injectedCodes = codeTable.Letters.Select(e => (ushort)e.Code).ToHashSet();
        var charByCode = codeTable.Letters.ToDictionary(e => (ushort)e.Code, e => e.Character[0]);

        var root = UcfbReader.ReadFile(outputCoreLvlPath);
        var fonts = FontChunkLocator.FindAll(root);
        var replacements = new Dictionary<long, byte[]>();

        report.Log($"UA: [{label}] Тест метричної моделі — Bearing = CellHeight − висота_бокса, CellHeight = базовий_відступ шрифту:");
        report.Log($"EN: [{label}] Metric-model test — Bearing = CellHeight − box_height, CellHeight = font baseline offset:");
        report.Log();

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var records = FontGlyphTable.Parse(fbod.RawData).ToList();

            // UA: Базовий відступ рядка = медіана CellHeight ЗВИЧАЙНИХ
            //     (некириличних) записів — той самий підхід, що й у тесті
            //     CellHeight; для gamefont_large це 30.
            // EN: Line baseline offset = median CellHeight of ORDINARY
            //     (non-Cyrillic) records — the same approach as the
            //     CellHeight test; for gamefont_large this is 30.
            var ordinaryCellHeights = records
                .Where(r => !injectedCodes.Contains(r.Code))
                .Select(r => (int)r.CellHeight)
                .OrderBy(v => v)
                .ToList();

            if (ordinaryCellHeights.Count == 0)
            {
                report.Log($"UA: [{label}] {font.BaseName}: немає звичайних записів — пропущено.");
                continue;
            }

            var baselineOffset = ordinaryCellHeights[ordinaryCellHeights.Count / 2];

            // UA: Висота UV-бокса в пікселях — потрібні розміри сторінки,
            //     на якій лежить гліф (page = record.PageIndex).
            // EN: UV box height in pixels — needs the dimensions of the
            //     page the glyph lives on (page = record.PageIndex).
            var pageHeights = new Dictionary<int, int>();
            for (var i = 0; i < font.TexturePages.Count; i++)
                pageHeights[i] = FontTexturePixelReader.ReadMip0(font.TexturePages[i].Chunk).Height;

            var updatedRecords = new List<FontGlyphRecord>(records.Count);
            var rows = new List<(char Ch, ushort Code, int BoxH, byte OldBearing, byte NewBearing, byte OldCell, byte NewCell)>();

            foreach (var r in records)
            {
                if (!injectedCodes.Contains(r.Code) || !pageHeights.TryGetValue(r.PageIndex, out var texHeight))
                {
                    updatedRecords.Add(r);
                    continue;
                }

                var boxHeightPx = (int)Math.Round(Math.Abs(r.V1 - r.V0) * texHeight);
                var newCell = (byte)Math.Clamp(baselineOffset, 0, 255);
                var newBearing = (byte)Math.Clamp(baselineOffset - boxHeightPx, 0, 255);

                rows.Add((charByCode[r.Code], r.Code, boxHeightPx, r.Bearing, newBearing, r.CellHeight, newCell));
                updatedRecords.Add(r with { Bearing = newBearing, CellHeight = newCell });
            }

            replacements[fbod.FileDataOffset] = FontGlyphTable.Serialize(updatedRecords);

            report.Log($"UA: [{label}] {font.BaseName}: базовий_відступ={baselineOffset}. Оновлено {rows.Count} кириличних записів:");
            report.Log("    Літера  Код   Бокс(H)  Bearing(стар→нов)  CellHeight(стар→нов)");
            foreach (var (ch, code, boxH, ob, nb, oc, nc) in rows.OrderBy(x => x.Ch))
                report.Log($"    {ch}       0x{code:X2}   {boxH,4}     {ob,3} → {nb,-3}          {oc,3} → {nc,-3}");
            report.Log();
        }

        var testOutputDir = Path.GetDirectoryName(testOutputCoreLvlPath)!;
        Directory.CreateDirectory(testOutputDir);
        var newBytes = UcfbWriter.WriteFile(root, replacements);
        File.WriteAllBytes(testOutputCoreLvlPath, newBytes);

        var sourceTablePath = Path.Combine(Path.GetDirectoryName(outputCoreLvlPath)!, CyrillicCodeTable.DefaultFileName);
        var destTablePath = Path.Combine(testOutputDir, CyrillicCodeTable.DefaultFileName);
        if (File.Exists(sourceTablePath))
            File.Copy(sourceTablePath, destTablePath, overwrite: true);

        report.Log($"UA: [{label}] ТЕСТОВИЙ файл (окремо, \"output\" не зачеплено): {testOutputCoreLvlPath}");
        report.Log($"EN: [{label}] TEST file (separate, \"output\" untouched): {testOutputCoreLvlPath}");
        report.Log($"UA: [{label}] Постав ЦЕЙ файл у гру ЗАМІСТЬ core.lvl. Якщо весь рядок вирівнявся по НИЗУ (літери більше не стрибають по вертикалі) — метричну модель підтверджено, і продакшн можна переписувати на Bearing/CellHeight.");
        report.Log($"EN: [{label}] Put THIS file into the game INSTEAD of core.lvl. If the whole line aligned along the BOTTOM (letters no longer jump vertically) — the metric model is confirmed, and production can be rewritten onto Bearing/CellHeight.");
        report.Log();
    }
}
