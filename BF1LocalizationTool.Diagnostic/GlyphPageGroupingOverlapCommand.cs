// =============================================================================
// BF1LocalizationTool.Diagnostic — GlyphPageGroupingOverlapCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: PageIndexByteCorrelationCommand дав НЕОДНОЗНАЧНИЙ результат
//     (70-88% збігів із "найгустішою сторінкою") — недостатньо для
//     підтвердження. Причина: "найгустіша сторінка" — та сама ненадійна
//     евристика (сторінки, що не належать гліфу, теж містять РЕАЛЬНИЙ
//     чужий вміст із випадково високою густиною — підтверджено ще на
//     M=0x4D, де tex1/tex2 показали не шум, а фрагменти інших символів).
//
//     Ця команда перевіряє гіпотезу ІНАКШЕ, без густини: групує гліфи
//     за значенням offset=2, і рахує перетини UV-прямокутників ЛИШЕ В
//     МЕЖАХ своєї групи (ігноруючи інші сторінки повністю) — точно так,
//     як писав би GlyphAtlasPatcher, якби довіряв offset=2. Якщо
//     гіпотеза правильна — кількість перетинів має впасти з тисяч
//     (як показав GlyphRectOverlapCommand при наївному застосуванні всіх
//     гліфів до всіх сторінок) до одиниць чи нуля. Це перевірка
//     ПРАКТИЧНОГО НАСЛІДКУ гіпотези, незалежна від суб'єктивної оцінки
//     "яка сторінка виглядає повнішою".
// EN: PageIndexByteCorrelationCommand gave an AMBIGUOUS result (70-88%
//     match with "densest page") — not enough to confirm. Reason:
//     "densest page" is the same unreliable heuristic (pages that don't
//     belong to a glyph still contain REAL foreign content with
//     coincidentally high density — already confirmed on M=0x4D, where
//     tex1/tex2 showed not noise but fragments of other characters).
//
//     This command tests the hypothesis DIFFERENTLY, without density:
//     groups glyphs by their offset=2 value, and counts UV rectangle
//     overlaps ONLY WITHIN each group (ignoring other pages entirely) —
//     exactly as GlyphAtlasPatcher would write if it trusted offset=2.
//     If the hypothesis is correct — the overlap count should drop from
//     the thousands (as GlyphRectOverlapCommand showed when naively
//     applying every glyph to every page) to near zero. This tests the
//     hypothesis's PRACTICAL CONSEQUENCE, independent of subjective
//     "which page looks fuller" judgment.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GlyphPageGroupingOverlapCommand
{
    private readonly record struct GlyphRect(ushort Code, int MinX, int MaxX, int MinY, int MaxY);

    public static void RunAll(DiagnosticReport report, UcfbChunk root, string label)
    {
        var fonts = FontChunkLocator.FindAll(root);

        report.Log($"=== [{label}] Перетини ПІСЛЯ групування за offset=2 (перевірка гіпотези без густини) ===");
        report.Log($"=== [{label}] Overlaps AFTER grouping by offset=2 (density-free hypothesis check) ===");

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var raw = fbod.RawData;
            var recordCount = raw.Length / 24;
            var pageCount = font.TexturePages.Count;

            // UA: Групуємо записи за byte offset=2, читаючи напряму з
            //     сирих байтів FBOD (не через FontGlyphTable — той поки
            //     не має цього поля).
            // EN: Group records by the offset=2 byte, reading directly
            //     from raw FBOD bytes (not via FontGlyphTable — it
            //     doesn't have this field yet).
            var groups = new Dictionary<int, List<(ushort Code, float U0, float U1, float V0, float V1)>>();
            var outOfRangeCount = 0;

            for (var i = 0; i < recordCount; i++)
            {
                var recOffset = i * 24;
                var code = BitConverter.ToUInt16(raw, recOffset);
                var offset2Value = raw[recOffset + 2];
                var u0 = BitConverter.ToSingle(raw, recOffset + 8);
                var u1 = BitConverter.ToSingle(raw, recOffset + 12);
                var v0 = BitConverter.ToSingle(raw, recOffset + 16);
                var v1 = BitConverter.ToSingle(raw, recOffset + 20);

                if (offset2Value >= pageCount)
                {
                    outOfRangeCount++;
                    continue; // UA: поза межами кількості сторінок — аномалія, рахуємо окремо
                }

                if (!groups.TryGetValue(offset2Value, out var list))
                    groups[offset2Value] = list = [];
                list.Add((code, u0, u1, v0, v1));
            }

            var totalOverlapsAfterGrouping = 0;
            var overlapExamples = new List<string>();

            foreach (var (pageIdx, entries) in groups)
            {
                if (pageIdx >= font.TexturePages.Count) continue;
                var texPixels = FontTexturePixelReader.ReadMip0(font.TexturePages[pageIdx].Chunk);

                var rects = entries.Select(e =>
                {
                    var x0 = (int)Math.Round(e.U0 * texPixels.Width);
                    var x1 = (int)Math.Round(e.U1 * texPixels.Width);
                    var y0 = (int)Math.Round(e.V0 * texPixels.Height);
                    var y1 = (int)Math.Round(e.V1 * texPixels.Height);
                    return new GlyphRect(e.Code, Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1), Math.Max(y0, y1));
                }).ToList();

                for (var i = 0; i < rects.Count; i++)
                {
                    for (var j = i + 1; j < rects.Count; j++)
                    {
                        var a = rects[i]; var b = rects[j];
                        var overlapsX = a.MinX < b.MaxX && b.MinX < a.MaxX;
                        var overlapsY = a.MinY < b.MaxY && b.MinY < a.MaxY;
                        if (overlapsX && overlapsY)
                        {
                            totalOverlapsAfterGrouping++;
                            if (overlapExamples.Count < 5)
                                overlapExamples.Add($"сторінка[{pageIdx}]: 0x{a.Code:X2} перетинається з 0x{b.Code:X2}");
                        }
                    }
                }
            }

            report.Log();
            report.Log($"  {font.BaseName} ({pageCount} сторінок, {recordCount} записів):");
            report.Log($"    Розмір груп: " + string.Join(", ", groups.OrderBy(g => g.Key).Select(g => $"page[{g.Key}]={g.Value.Count}шт")));
            if (outOfRangeCount > 0)
                report.Log($"    !! offset=2 ПОЗА межами кількості сторінок: {outOfRangeCount} записів (аномалія)");
            report.Log($"    Перетинів ПІСЛЯ групування: {totalOverlapsAfterGrouping} (було тисячі до групування — див. GlyphRectOverlapCommand)");

            if (overlapExamples.Count > 0)
                foreach (var ex in overlapExamples) report.Log($"      {ex}");

            report.Log(totalOverlapsAfterGrouping == 0 && outOfRangeCount == 0
                ? "    UA: СИЛЬНО ПІДТВЕРДЖУЄ гіпотезу — 0 перетинів після групування за offset=2."
                : totalOverlapsAfterGrouping < recordCount / 20
                    ? "    UA: ЙМОВІРНО ПІДТВЕРДЖУЄ — перетинів значно менше, ніж до групування, але не нуль."
                    : "    UA: НЕ ПІДТВЕРДЖУЄ — перетинів все ще багато навіть після групування.");
        }
    }
}