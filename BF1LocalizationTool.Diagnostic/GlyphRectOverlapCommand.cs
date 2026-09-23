// =============================================================================
// BF1LocalizationTool.Diagnostic — GlyphRectOverlapCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Перевіряє, ЧИ можуть UV-прямокутники РІЗНИХ гліфів на ОДНІЙ
//     текстурній сторінці перетинатись, і який мінімальний відступ
//     (padding) є між сусідніми прямокутниками. Це напряму впливає на
//     безпеку GlyphAtlasPatcher: якщо прямокутники перетинаються —
//     запис нового гліфа рівно в межах його UV-прямокутника може
//     зіпсувати сусідній гліф. Якщо є padding >= 1px — запис у межах
//     точного прямокутника безпечний.
//
//     Перевіряє ПО ВСІХ парах гліфів на кожній сторінці (не вибірково) —
//     для 226 гліфів це ~25000 пар на сторінку, цілком прийнятно.
// EN: Verifies WHETHER UV rectangles of DIFFERENT glyphs on the SAME
//     texture page can overlap, and what the minimum padding is between
//     adjacent rectangles. This directly affects GlyphAtlasPatcher's
//     safety: if rectangles overlap — writing a new glyph exactly within
//     its UV rectangle could corrupt a neighboring glyph. If there's
//     padding >= 1px — writing within the exact rectangle is safe.
//
//     Checks ALL pairs of glyphs on each page (not a sample) — for 226
//     glyphs that's ~25000 pairs per page, entirely feasible.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GlyphRectOverlapCommand
{
    private readonly record struct GlyphRect(ushort Code, int MinX, int MaxX, int MinY, int MaxY);

    public static void Run(DiagnosticReport report, UcfbChunk root, string label)
    {
        var fonts = FontChunkLocator.FindAll(root);

        var totalPagesChecked = 0;
        var totalPairsChecked = 0;
        var overlapCount = 0;
        var minPaddingOverall = int.MaxValue;
        var overlapExamples = new List<string>();

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var glyphs = FontGlyphTable.Parse(fbod.RawData);

            foreach (var page in font.TexturePages)
            {
                var texPixels = FontTexturePixelReader.ReadMip0(page.Chunk);
                totalPagesChecked++;

                var rects = new List<GlyphRect>();
                foreach (var glyph in glyphs)
                {
                    var x0 = (int)Math.Round(glyph.U0 * texPixels.Width);
                    var x1 = (int)Math.Round(glyph.U1 * texPixels.Width);
                    var y0 = (int)Math.Round(glyph.V0 * texPixels.Height);
                    var y1 = (int)Math.Round(glyph.V1 * texPixels.Height);

                    rects.Add(new GlyphRect(
                        glyph.Code,
                        Math.Min(x0, x1), Math.Max(x0, x1),
                        Math.Min(y0, y1), Math.Max(y0, y1)));
                }

                // UA: Перевіряємо КОЖНУ пару прямокутників на цій сторінці.
                // EN: Check EVERY pair of rectangles on this page.
                for (var i = 0; i < rects.Count; i++)
                {
                    for (var j = i + 1; j < rects.Count; j++)
                    {
                        totalPairsChecked++;
                        var a = rects[i];
                        var b = rects[j];

                        // UA: Перетин прямокутників по X і Y (стандартна
                        //     AABB-перевірка).
                        // EN: Rectangle overlap on X and Y (standard AABB
                        //     check).
                        var overlapsX = a.MinX < b.MaxX && b.MinX < a.MaxX;
                        var overlapsY = a.MinY < b.MaxY && b.MinY < a.MaxY;

                        if (overlapsX && overlapsY)
                        {
                            overlapCount++;
                            if (overlapExamples.Count < 10)
                                overlapExamples.Add(
                                    $"{font.BaseName}/{page.Name}: code=0x{a.Code:X2} " +
                                    $"[{a.MinX}..{a.MaxX})x[{a.MinY}..{a.MaxY}) ПЕРЕТИНАЄТЬСЯ з " +
                                    $"code=0x{b.Code:X2} [{b.MinX}..{b.MaxX})x[{b.MinY}..{b.MaxY})");
                        }
                        else
                        {
                            // UA: Мінімальна "відстань" між прямокутниками
                            //     (0, якщо впритул по одній з осей, і вони
                            //     перекриваються по іншій — тобто буквально
                            //     сусідні пікселі).
                            // EN: Minimum "gap" between rectangles (0 if
                            //     they're flush along one axis while
                            //     overlapping on the other — i.e. literally
                            //     adjacent pixels).
                            var gapX = overlapsY ? Math.Max(a.MinX - b.MaxX, b.MinX - a.MaxX) : int.MaxValue;
                            var gapY = overlapsX ? Math.Max(a.MinY - b.MaxY, b.MinY - a.MaxY) : int.MaxValue;
                            var gap = Math.Min(gapX, gapY);

                            if (gap != int.MaxValue && gap < minPaddingOverall)
                                minPaddingOverall = gap;
                        }
                    }
                }
            }
        }

        report.Log($"=== [{label}] Перетин UV-прямокутників гліфів (усі пари на кожній сторінці) ===");
        report.Log($"    Сторінок перевірено: {totalPagesChecked}");
        report.Log($"    Пар гліфів перевірено: {totalPairsChecked}");
        report.Log($"    Перетинів знайдено: {overlapCount}");
        report.Log($"    Мінімальний відступ між НЕ-перетнутими сусідніми прямокутниками: " +
                           (minPaddingOverall == int.MaxValue ? "н/д" : $"{minPaddingOverall}px"));

        if (overlapExamples.Count > 0)
        {
            report.Log("    --- Приклади перетинів (до 10) ---");
            foreach (var ex in overlapExamples)
                report.Log($"      {ex}");
        }

        report.Log(overlapCount == 0
            ? "    UA: ПІДТВЕРДЖЕНО — жоден UV-прямокутник не перетинається з іншим на тій самій сторінці. " +
              "Запис рівно в межах донорського прямокутника безпечний для сусідів."
            : "    UA: УВАГА — знайдено перетини прямокутників. Запис рівно в межах UV-прямокутника " +
              "МОЖЕ зачепити сусідній гліф. Потрібне додаткове дослідження перед GlyphAtlasPatcher.");
    }
}