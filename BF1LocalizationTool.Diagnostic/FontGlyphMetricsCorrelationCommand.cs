// =============================================================================
// BF1LocalizationTool.Diagnostic — FontGlyphMetricsCorrelationCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Продовження GlyphVerticalGeometryProbeCommand — там середні за
//     категоріями (baseline/descender/ascender) намітили, що
//     Bearing/XAdvance — швидше ГОРИЗОНТАЛЬНІ метрики (масштаб значень
//     близький до InkWidth, не до CellHeight), а різниця по категоріях
//     пояснювалась вибором конкретних вузьких літер (l,t,f,k) у
//     пробнику, а не вертикальним призначенням поля.
//
//     Ця перевірка йде точніше — по КОЖНОМУ окремому гліфу ВСЬОГО
//     шрифту (не лише ASCII-пробнику), рахує похідні різниці
//     (XAdvance-InkWidth, XAdvance-UVWidth, Bearing-InkWidth,
//     CellHeight-UVHeight) і перевіряє факт: чи хоч одна з них СТАЛА
//     (однакова, чи майже однакова) для всіх гліфів шрифту — стала
//     різниця означає адитивну формулу (метрика = інша метрика + стала).
// EN: Continuation of GlyphVerticalGeometryProbeCommand — there,
//     category averages (baseline/descender/ascender) hinted that
//     Bearing/XAdvance are likely HORIZONTAL metrics (value scale close
//     to InkWidth, not to CellHeight), and the per-category difference
//     was explained by the specific narrow letters (l,t,f,k) chosen for
//     the probe, not a vertical purpose for the field.
//
//     This check goes more precisely — over EVERY individual glyph of
//     the WHOLE font (not just the ASCII probe), computing derived
//     differences (XAdvance-InkWidth, XAdvance-UVWidth,
//     Bearing-InkWidth, CellHeight-UVHeight) and checking a fact:
//     whether any of them is CONSTANT (the same, or nearly the same)
//     across every glyph in the font — a constant difference means an
//     additive formula (metric = other metric + constant).
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class FontGlyphMetricsCorrelationCommand
{
    public static void Run(DiagnosticReport report, UcfbChunk root, string label)
    {
        var fonts = FontChunkLocator.FindAll(root);

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var glyphs = FontGlyphTable.Parse(fbod.RawData);

            var diffXAdvanceInkWidth = new List<int>();
            var diffXAdvanceUvWidth = new List<int>();
            var diffBearingInkWidth = new List<int>();
            var diffCellHeightUvHeight = new List<int>();

            var rows = new List<string>();

            foreach (var pageGroup in glyphs.GroupBy(g => g.PageIndex))
            {
                if (pageGroup.Key >= font.TexturePages.Count) continue;

                var texPixels = FontTexturePixelReader.ReadMip0(font.TexturePages[pageGroup.Key].Chunk);

                foreach (var g in pageGroup)
                {
                    var x0 = (int)Math.Round(g.U0 * texPixels.Width);
                    var x1 = (int)Math.Round(g.U1 * texPixels.Width);
                    var y0 = (int)Math.Round(g.V0 * texPixels.Height);
                    var y1 = (int)Math.Round(g.V1 * texPixels.Height);
                    var uvWidth = Math.Abs(x1 - x0);
                    var uvHeight = Math.Abs(y1 - y0);

                    if (uvWidth <= 0 || uvHeight <= 0) continue; // UA: вироджені — вже відомо / EN: degenerate — already known

                    var dXAdvInk = g.XAdvance - g.InkWidth;
                    var dXAdvUv = g.XAdvance - uvWidth;
                    var dBearingInk = g.Bearing - g.InkWidth;
                    var dCellUv = g.CellHeight - uvHeight;

                    diffXAdvanceInkWidth.Add(dXAdvInk);
                    diffXAdvanceUvWidth.Add(dXAdvUv);
                    diffBearingInkWidth.Add(dBearingInk);
                    diffCellHeightUvHeight.Add(dCellUv);

                    rows.Add($"0x{g.Code:X2} UV={uvWidth}x{uvHeight} InkWidth={g.InkWidth} XAdvance={g.XAdvance} " +
                             $"Bearing={g.Bearing} CellHeight={g.CellHeight} | XAdv-Ink={dXAdvInk} XAdv-UVw={dXAdvUv} " +
                             $"Bearing-Ink={dBearingInk} Cell-UVh={dCellUv}");
                }
            }

            report.Log($"=== [{label}] {font.BaseName}: кореляція метрик FBOD по КОЖНОМУ гліфу ({rows.Count} шт.) ===");

            void ReportDiff(string title, List<int> diffs)
            {
                if (diffs.Count == 0) { report.Log($"    {title}: (немає даних)"); return; }
                var distinct = diffs.Distinct().OrderBy(x => x).ToList();
                var avg = diffs.Average();
                var stdDev = Math.Sqrt(diffs.Average(d => Math.Pow(d - avg, 2)));
                report.Log($"    {title}: середнє={avg:F2}, стандартне відхилення={stdDev:F2}, " +
                           $"унікальних значень={distinct.Count} (діапазон {distinct.First()}..{distinct.Last()})");
            }

            ReportDiff("XAdvance - InkWidth", diffXAdvanceInkWidth);
            ReportDiff("XAdvance - UV_Width", diffXAdvanceUvWidth);
            ReportDiff("Bearing - InkWidth", diffBearingInkWidth);
            ReportDiff("CellHeight - UV_Height", diffCellHeightUvHeight);

            report.Log("    --- Перші 15 гліфів (для ручної перевірки) ---");
            foreach (var row in rows.Take(15))
                report.Log($"    {row}");

            report.Log();
        }
    }
}
