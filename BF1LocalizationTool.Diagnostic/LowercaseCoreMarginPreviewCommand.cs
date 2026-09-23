// =============================================================================
// BF1LocalizationTool.Diagnostic — LowercaseCoreMarginPreviewCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: READ-ONLY перевірка моделі "ядро+виступ" (GlyphMetricModel.
//     ComputeLowercaseCoreMetric) — БЕЗ жодного запису файлу, БЕЗ атласу,
//     БЕЗ пакування, взагалі без торкання output-nodonor\. Читає ЛИШЕ
//     оригінальний core.lvl (той самий вхід, що й генерація) і рахує ті
//     самі опорні метрики (GlyphMetricModel.DeriveReference) та й той
//     самий per-letter розрахунок (ComputeLowercaseCoreMetric), що й
//     GenerateNoDonorCyrillicCoreCommand — просто НЕ рендерить пікселі й
//     НЕ пише файл, лише друкує числа в звіт.
//
//     ПРИЧИНА існування цього файлу: перевірка й генерація — різні дії,
//     і перша не повинна вимагати другої (та сама причина, з якої
//     GlyphOccupancyOverlayCommand — окремий read-only інструмент, а не
//     частина генератора). Ці числа доступні як самостійний,
//     безефектний пункт меню ПЕРЕВІРОК, без запуску повної
//     генерації/перезапису core.lvl. Обидві перевірки (ця й
//     GlyphOccupancyOverlayCommand через RunGlyphOccupancyOverlayManualPick)
//     — самостійні пункти меню категорій ПЕРЕВІРОК, без побічних ефектів.
//
//     ResolveFontFamilyName перевикористовується НАПРЯМУ з
//     GenerateNoDonorCyrillicCoreCommand (internal) — щоб цей прев'ю
//     завжди показував метрики ТОГО САМОГО шрифту-кандидата, який
//     реально піде в генерацію, без ризику розсинхронізації двох копій
//     одного switch.
// EN: READ-ONLY check of the "core+extension" model (GlyphMetricModel.
//     ComputeLowercaseCoreMetric) — with NO file write, NO atlas, NO
//     packing, no touching output-nodonor\ at all. Reads ONLY the original
//     core.lvl (the same input generation uses) and computes the same
//     reference metrics (GlyphMetricModel.DeriveReference) and the same
//     per-letter calculation (ComputeLowercaseCoreMetric) as
//     GenerateNoDonorCyrillicCoreCommand — it just doesn't render pixels or
//     write a file, only prints the numbers to the report.
//
//     WHY THIS FILE EXISTS: checking and generating are different
//     actions, and the first shouldn't require the second (the same
//     reason GlyphOccupancyOverlayCommand is a separate read-only tool
//     rather than part of the generator). These numbers are available as
//     a standalone, side-effect-free item in the CHECKS menu, without
//     running a full core.lvl generation/rewrite. Both checks (this one,
//     and GlyphOccupancyOverlayCommand via
//     RunGlyphOccupancyOverlayManualPick) are standalone items in the
//     CHECKS menu categories, with no side effects.
//
//     ResolveFontFamilyName is reused DIRECTLY from
//     GenerateNoDonorCyrillicCoreCommand (internal) — so this preview
//     always shows the metrics of the EXACT SAME candidate font that
//     generation will actually use, with no risk of two copies of the
//     same switch drifting out of sync.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.FontGenerator.Matching;

namespace BF1LocalizationTool.Diagnostic;

public static class LowercaseCoreMarginPreviewCommand
{
    private static readonly string[] TargetFontBaseNames =
        ["gamefont_large", "gamefont_medium", "gamefont_small", "gamefont_tiny", "gamefont_super_tiny"];

    // UA: і/ї/й — приклад літер із верхнім елементом (крапка/дашок). б/ф
    //     додані як ще два реальні "виступ-зверху" кандидати з коментарів
    //     GlyphMetricModel (стрижень), для повнішої картини за ту саму
    //     ціну проходу.
    // EN: і/ї/й — an example of letters with a top element (dot/breve).
    //     б/ф added as two more real "top extension" candidates from
    //     GlyphMetricModel's comments (the stem), for a fuller picture at
    //     the same pass cost.
    private static readonly char[] SpotCheckLetters = ['і', 'ї', 'й', 'б', 'ф'];

    public static void Run(DiagnosticReport report, string inputFilePath, string label)
    {
        if (!File.Exists(inputFilePath))
        {
            report.Log($"UA: Не знайдено {inputFilePath} — пропущено. / EN: {inputFilePath} not found — skipped.");
            return;
        }

        var root = UcfbReader.ReadFile(inputFilePath);
        var fonts = FontChunkLocator.FindAll(root);

        report.Log($"=== [{label}] Прев'ю моделі ядро+виступ (READ-ONLY, {inputFilePath}) ===");
        report.Log($"=== [{label}] Core+extension model preview (READ-ONLY, {inputFilePath}) ===");
        report.Log();

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
            var englishCapInkWidths = originalRecords
                .Where(r => r.Code is >= (ushort)'A' and <= (ushort)'Z')
                .Select(r => (int)r.InkWidth)
                .ToList();
            var englishLowerBearings = originalRecords
                .Where(r => r.Code is >= (ushort)'a' and <= (ushort)'z')
                .Select(r => (int)r.Bearing)
                .ToList();

            string fontFamilyName;
            try
            {
                // UA: Той самий вибір, що й реальна генерація — internal
                //     метод GenerateNoDonorCyrillicCoreCommand, не копія.
                // EN: The exact same choice as real generation — the
                //     internal GenerateNoDonorCyrillicCoreCommand method,
                //     not a copy.
                fontFamilyName = GenerateNoDonorCyrillicCoreCommand.ResolveFontFamilyName(label, fontBaseName);
            }
            catch (Exception ex)
            {
                report.Log($"UA: [{label}] {fontBaseName}: немає обраного шрифту-кандидата ({ex.Message}) — пропущено. / " +
                           $"EN: [{label}] {fontBaseName}: no chosen font candidate ({ex.Message}) — skipped.");
                continue;
            }

            FontMetricReference metricReference;
            try
            {
                metricReference = GlyphMetricModel.DeriveReference(
                    allEnglishCellHeights, englishCapBearings, CyrillicAlphabet.UppercaseLetters, fontFamilyName,
                    englishCapInkWidths, englishLowerBearings);
            }
            catch (Exception ex)
            {
                report.Log($"UA: [{label}] {fontBaseName} ({fontFamilyName}): опорні метрики не виведено ({ex.Message}) — пропущено. / " +
                           $"EN: [{label}] {fontBaseName} ({fontFamilyName}): reference metrics failed ({ex.Message}) — skipped.");
                continue;
            }

            report.Log($"UA: [{label}] {fontBaseName} ({fontFamilyName}) — " +
                       $"CapHeightGame={metricReference.CapHeightGame}px, CoreHeightGame={metricReference.CoreHeightGame}px, " +
                       $"CoreMarginCapPx={metricReference.CoreMarginCapPx}px (макс. дозволений виступ крапки/хвоста понад тіло малої літери). / " +
                       $"EN: [{label}] {fontBaseName} ({fontFamilyName}) — " +
                       $"CapHeightGame={metricReference.CapHeightGame}px, CoreHeightGame={metricReference.CoreHeightGame}px, " +
                       $"CoreMarginCapPx={metricReference.CoreMarginCapPx}px (max allowed dot/tail protrusion above the lowercase core).");

            // UA: Той самий пропорційний кап, що й
            //     GenerateNoDonorCyrillicCoreCommand тепер реально
            //     застосовує при генерації (GlyphBoxFitRenderer.
            //     ComputeAlphabetExtensionScale, спільна логіка, жодного
            //     дублювання математики). БЕЗ цього прев'ю показувало б
            //     СТАРІ (до фіксу) числа жорсткого per-letter клампу —
            //     розсинхронізація з тим, що генерація насправді робить.
            // EN: The same proportional cap that
            //     GenerateNoDonorCyrillicCoreCommand now actually applies
            //     during generation (GlyphBoxFitRenderer.
            //     ComputeAlphabetExtensionScale, shared logic, no math
            //     duplication). WITHOUT this the preview would show the
            //     OLD (pre-fix) hard per-letter clamp numbers — drifting
            //     out of sync with what generation actually does.
            // UA: boxSizeForChar повертає LowercaseProbeBoxHeightBound
            //     (НЕ m.BoxHeight) — той самий великий запас, який
            //     ComputeLowercaseCoreMetric сам використовує для
            //     "натурального" виміру (див. коментар у GlyphMetricModel).
            //     Реальна тісна BoxHeight конкретної літери вже обрізана
            //     до ЇЇ ВЛАСНОГО природного розміру — самореференція дала
            //     б завжди 0 запасу.
            // EN: boxSizeForChar returns LowercaseProbeBoxHeightBound (NOT
            //     m.BoxHeight) — the same large headroom
            //     ComputeLowercaseCoreMetric itself uses for the "natural"
            //     measurement (see the comment in GlyphMetricModel). A
            //     specific letter's real tight BoxHeight is already
            //     clipped to ITS OWN natural size — self-reference would
            //     always show 0 headroom.
            var extensionScale = GlyphBoxFitRenderer.ComputeAlphabetExtensionScale(
                CyrillicAlphabet.LowercaseLetters, fontFamilyName,
                metricReference.CoreHeightGame, metricReference.CoreTopYProbe, metricReference.CoreMarginCapPx,
                ch2 =>
                {
                    var m = GlyphMetricModel.ComputeLetterMetric(ch2, fontFamilyName, metricReference);
                    return (m.BoxWidth, GlyphMetricModel.LowercaseProbeBoxHeightBound);
                });

            report.Log($"UA: [{label}] {fontBaseName}: пропорційний кап виступів — aboveScale={extensionScale.AboveScale:F2} " +
                       $"(найбільший природний виступ-зверху {extensionScale.MaxNaturalAboveH}px→{metricReference.CoreMarginCapPx}px), " +
                       $"belowScale={extensionScale.BelowScale:F2} (найбільший природний виступ-знизу {extensionScale.MaxNaturalBelowH}px→{metricReference.CoreMarginCapPx}px). / " +
                       $"EN: [{label}] {fontBaseName}: proportional extension cap — aboveScale={extensionScale.AboveScale:F2} " +
                       $"(largest natural top extension {extensionScale.MaxNaturalAboveH}px→{metricReference.CoreMarginCapPx}px), " +
                       $"belowScale={extensionScale.BelowScale:F2} (largest natural bottom extension {extensionScale.MaxNaturalBelowH}px→{metricReference.CoreMarginCapPx}px).");

            foreach (var ch in SpotCheckLetters)
            {
                try
                {
                    // UA: boxHeight тут = LowercaseProbeBoxHeightBound (той
                    //     самий великий запас, НЕ widthMetric.BoxHeight —
                    //     той рахований для ВЕЛИКИХ літер лінійним
                    //     capital-scale і не має стосунку до реальної
                    //     тісної висоти, яку модель ядро+виступи ще
                    //     тільки визначить). Це відтворює ПЕРШИЙ крок
                    //     ComputeLowercaseCoreMetric — виміряти природний
                    //     (НЕобрізаний) розмір; сам метод не приймає
                    //     aboveScale/belowScale, тож прев'ю викликає
                    //     ComputeCoreMarginLayout напряму, щоб показати
                    //     ЦИФРИ З пропорційним капом.
                    // EN: boxHeight here = LowercaseProbeBoxHeightBound
                    //     (the same large headroom, NOT widthMetric.
                    //     BoxHeight — that one is computed for UPPERCASE
                    //     via the linear capital-scale and has nothing to
                    //     do with the real tight height the core+extension
                    //     model is about to determine). This reproduces
                    //     ComputeLowercaseCoreMetric's FIRST step —
                    //     measuring the natural (UNCLIPPED) size; the
                    //     method itself doesn't accept aboveScale/
                    //     belowScale, so the preview calls
                    //     ComputeCoreMarginLayout directly to show numbers
                    //     WITH the proportional cap applied.
                    var widthMetric = GlyphMetricModel.ComputeLetterMetric(ch, fontFamilyName, metricReference);
                    var layout = GlyphBoxFitRenderer.ComputeCoreMarginLayout(
                        ch, fontFamilyName, metricReference.CoreHeightGame, metricReference.CoreTopYProbe,
                        metricReference.CoreMarginCapPx, widthMetric.BoxWidth, GlyphMetricModel.LowercaseProbeBoxHeightBound,
                        extensionScale.AboveScale, extensionScale.BelowScale);

                    var boxHeightFinal = layout.AboveRenderH + layout.CoreRenderH + layout.BelowRenderH;
                    report.Log($"UA:   [{label}] {fontBaseName} '{ch}': тіло={layout.CoreRenderH}px, " +
                               $"виступ-зверху={layout.AboveRenderH}px (природний {layout.NatAboveRenderH}px), " +
                               $"виступ-знизу={layout.BelowRenderH}px (природний {layout.NatBelowRenderH}px), " +
                               $"BoxHeight(підсумок)={boxHeightFinal}px. / " +
                               $"EN:   [{label}] {fontBaseName} '{ch}': core={layout.CoreRenderH}px, " +
                               $"top-extension={layout.AboveRenderH}px (natural {layout.NatAboveRenderH}px), " +
                               $"bottom-extension={layout.BelowRenderH}px (natural {layout.NatBelowRenderH}px), " +
                               $"BoxHeight(final)={boxHeightFinal}px.");
                }
                catch (Exception ex)
                {
                    report.Log($"UA:   [{label}] {fontBaseName} '{ch}': не вдалось порахувати ({ex.Message}). / " +
                               $"EN:   [{label}] {fontBaseName} '{ch}': failed to compute ({ex.Message}).");
                }
            }

            report.Log();
        }
    }
}
