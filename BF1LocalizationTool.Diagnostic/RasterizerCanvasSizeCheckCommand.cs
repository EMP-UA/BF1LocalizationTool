// =============================================================================
// BF1LocalizationTool.Diagnostic — RasterizerCanvasSizeCheckCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: ВАЖЛИВЕ УТОЧНЕННЯ щодо GdiGlyphRasterizer.cs: питання
//     "чи розмір донорського прямокутника ЗБІГАЄТЬСЯ з тим, що видає
//     растеризатор" — вже гарантовано КОНСТРУКЦІЄЮ коду, а не емпіричний
//     факт: RasterizedGlyph.Width/Height просто КОПІЮЮТЬСЯ з
//     GlyphRasterizeOptions.CanvasWidth/CanvasHeight (растеризатор сам
//     розмір не вираховує). Перевіряти тут нічого.
//
//     РЕАЛЬНИЙ РИЗИК інший: чи весь пайплайн Rasterize→ToA4R4G4B4 взагалі
//     ВІДПРАЦЬОВУЄ БЕЗ ВИНЯТКУ й дає РІВНО CanvasWidth×CanvasHeight×2
//     байт — на КОЖНОМУ реальному розмірі донорського слоту з обох ігор
//     (не на вигаданому прикладі). Зокрема: GdiGlyphRasterizer.Rasterize
//     кидає ArgumentException, якщо CanvasWidth/CanvasHeight <= 0 — якщо
//     серед реальних UV-прямокутників трапиться вироджений (0 чи
//     від'ємної ширини/висоти після округлення), GlyphAtlasPatcher впаде
//     на цьому слоті.
//
//     Форма символу тут НЕ важлива (беремо нейтральний 'A') — перевіряється
//     САМЕ розмірна механіка пайплайна, а не візуальна коректність гліфа.
// EN: IMPORTANT CLARIFICATION regarding GdiGlyphRasterizer.cs: the
//     question "does the donor rectangle size MATCH what the rasterizer
//     outputs" is already guaranteed by code CONSTRUCTION, not an
//     empirical fact: RasterizedGlyph.Width/Height are simply COPIED from
//     GlyphRasterizeOptions.CanvasWidth/CanvasHeight (the rasterizer never
//     computes size itself). Nothing to verify there.
//
//     The REAL RISK is different: does the whole Rasterize→ToA4R4G4B4
//     pipeline actually RUN WITHOUT THROWING and produce EXACTLY
//     CanvasWidth×CanvasHeight×2 bytes — for EVERY real donor slot size
//     from both games (not a made-up example). Specifically:
//     GdiGlyphRasterizer.Rasterize throws ArgumentException if
//     CanvasWidth/CanvasHeight <= 0 — if any real UV rectangle turns out
//     degenerate (0 or negative width/height after rounding),
//     GlyphAtlasPatcher would crash on that slot.
//
//     The character's shape doesn't matter here (a neutral 'A' is used) —
//     this checks the SIZE MECHANICS of the pipeline only, not visual
//     glyph correctness.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.FontGenerator.PixelConversion;
using BF1LocalizationTool.FontGenerator.Rasterization;
using System.Runtime.Versioning;

namespace BF1LocalizationTool.Diagnostic;

[SupportedOSPlatform("windows")]
public static class RasterizerCanvasSizeCheckCommand
{
    // UA: Нейтральний символ для тесту розмірної механіки — форма не
    //     перевіряється, лише байтова довжина результату.
    // EN: Neutral character for the size-mechanics test — shape isn't
    //     checked, only the result's byte length.
    private const char ProbeCharacter = 'A';
    private const string ProbeFontFamily = "Arial";

    public static void Run(DiagnosticReport report, UcfbChunk root, string label)
    {
        var rasterizer = new GdiGlyphRasterizer();
        var fonts = FontChunkLocator.FindAll(root);

        var totalChecked = 0;
        var degenerateSlots = new List<string>();
        var pipelineFailures = new List<string>();
        var lengthMismatches = new List<string>();

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var glyphs = FontGlyphTable.Parse(fbod.RawData);

            foreach (var pageGroup in glyphs.GroupBy(g => g.PageIndex))
            {
                if (pageGroup.Key >= font.TexturePages.Count)
                    continue; // UA: аномалія — уже зафіксована в UvRectBoundsCheckCommand / EN: anomaly — already caught by UvRectBoundsCheckCommand

                var texPixels = FontTexturePixelReader.ReadMip0(font.TexturePages[pageGroup.Key].Chunk);

                foreach (var glyph in pageGroup)
                {
                    totalChecked++;

                    var x0 = (int)Math.Round(glyph.U0 * texPixels.Width);
                    var x1 = (int)Math.Round(glyph.U1 * texPixels.Width);
                    var y0 = (int)Math.Round(Math.Min(glyph.V0, glyph.V1) * texPixels.Height);
                    var y1 = (int)Math.Round(Math.Max(glyph.V0, glyph.V1) * texPixels.Height);

                    var canvasWidth = Math.Abs(x1 - x0);
                    var canvasHeight = y1 - y0;

                    var slotDesc = $"{font.BaseName}/{font.TexturePages[pageGroup.Key].Name} code=0x{glyph.Code:X2} " +
                                   $"({canvasWidth}x{canvasHeight})";

                    if (canvasWidth <= 0 || canvasHeight <= 0)
                    {
                        degenerateSlots.Add(slotDesc);
                        continue; // UA: не викликаємо Rasterize — свідомо кине ArgumentException / EN: don't call Rasterize — it would deliberately throw ArgumentException
                    }

                    var options = new GlyphRasterizeOptions
                    {
                        FontFamilyName = ProbeFontFamily,
                        FontSizePx = canvasHeight,
                        CanvasWidth = canvasWidth,
                        CanvasHeight = canvasHeight,
                        BaselineY = canvasHeight
                    };

                    try
                    {
                        var rasterized = rasterizer.Rasterize(ProbeCharacter, options);
                        var converted = GlyphPixelConverter.ToA4R4G4B4(rasterized);

                        var expectedLength = canvasWidth * canvasHeight * 2;
                        if (converted.Length != expectedLength)
                            lengthMismatches.Add(
                                $"{slotDesc}: очікувано {expectedLength} байт, фактично {converted.Length} байт");
                    }
                    catch (Exception ex)
                    {
                        pipelineFailures.Add($"{slotDesc}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
        }

        report.Log($"=== [{label}] Наскрізний прогін Rasterize→ToA4R4G4B4 на РЕАЛЬНИХ розмірах слотів (перевірка перед GlyphAtlasPatcher) ===");
        report.Log($"    Перевірено гліф-записів: {totalChecked}");
        report.Log($"    Вироджені слоти (CanvasWidth/Height <= 0, Rasterize впав би одразу): {degenerateSlots.Count}");
        report.Log($"    Винятків під час Rasterize/ToA4R4G4B4: {pipelineFailures.Count}");
        report.Log($"    Розбіжностей довжини результату (≠ CanvasWidth×CanvasHeight×2): {lengthMismatches.Count}");

        if (degenerateSlots.Count > 0)
        {
            report.Log("    --- Вироджені слоти (до 10) ---");
            foreach (var m in degenerateSlots.Take(10))
                report.Log($"      {m}");
        }

        if (pipelineFailures.Count > 0)
        {
            report.Log("    --- Винятки пайплайна (до 10) ---");
            foreach (var m in pipelineFailures.Take(10))
                report.Log($"      {m}");
        }

        if (lengthMismatches.Count > 0)
        {
            report.Log("    --- Розбіжності довжини (до 10) ---");
            foreach (var m in lengthMismatches.Take(10))
                report.Log($"      {m}");
        }

        report.Log(degenerateSlots.Count == 0 && pipelineFailures.Count == 0 && lengthMismatches.Count == 0
            ? "    UA: ПІДТВЕРДЖЕНО — на КОЖНОМУ реальному розмірі донорського слоту пайплайн Rasterize→ToA4R4G4B4 " +
              "відпрацьовує без винятку й дає рівно CanvasWidth×CanvasHeight×2 байт. GlyphAtlasPatcher може довіряти " +
              "розміру, взятому напряму з UV-прямокутника донора."
            : "    UA: ЗНАЙДЕНО ПРОБЛЕМНІ СЛОТИ — GlyphAtlasPatcher має явно виключати перелічені вище коди зі списку " +
              "безпечних донорів (не намагатись растеризувати в них заміну) або обробляти виняток без падіння всього процесу.");
    }
}
