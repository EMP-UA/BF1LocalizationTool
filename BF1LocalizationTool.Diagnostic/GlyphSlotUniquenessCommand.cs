// =============================================================================
// BF1LocalizationTool.Diagnostic — GlyphSlotUniquenessCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Одна з передумовних перевірок для GlyphAtlasPatcher.
//     GlyphAtlasPatcher перезаписує пікселі donor-коду
//     ЗА ЙОГО ФІЗИЧНИМ СЛОТОМ (PageIndex + піксельний прямокутник). Якщо
//     раптом ІНШИЙ код (напр. використовуваний мовою символ) вказує на
//     ТОЙ САМИЙ фізичний слот — перезапис одного зіпсує інший.
//
//     На відміну від GlyphRectOverlapCommand/GlyphOverlapRiskCommand
//     (перевіряють ПЕРЕТИНИ прямокутників — частковий overlap), ця
//     перевірка шукає РІВНО ОДНАКОВІ прямокутники (PageIndex + округлені
//     x0/x1/y0/y1 збігаються АБСОЛЮТНО) — саме такий випадок і означає
//     "два коди ділять один слот".
// EN: One of the precondition checks for GlyphAtlasPatcher.
//     GlyphAtlasPatcher overwrites a donor code's
//     pixels AT ITS PHYSICAL SLOT (PageIndex + pixel rectangle). If some
//     OTHER code (e.g. a character actually used by a language) points
//     at the SAME physical slot — overwriting one corrupts the other.
//
//     Unlike GlyphRectOverlapCommand/GlyphOverlapRiskCommand (which check
//     for rectangle INTERSECTION — partial overlap), this check looks for
//     EXACTLY IDENTICAL rectangles (PageIndex + rounded x0/x1/y0/y1 match
//     ABSOLUTELY) — that specific case is what "two codes share one slot"
//     means.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GlyphSlotUniquenessCommand
{
    private readonly record struct Slot(byte PageIndex, int MinX, int MaxX, int MinY, int MaxY);

    public static void Run(DiagnosticReport report, UcfbChunk root, string label)
    {
        var fonts = FontChunkLocator.FindAll(root);

        var totalChecked = 0;
        var sharedSlots = new List<string>();

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var glyphs = FontGlyphTable.Parse(fbod.RawData);
            var bySlot = new Dictionary<Slot, List<ushort>>();

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

                    var slot = new Slot(glyph.PageIndex, Math.Min(x0, x1), Math.Max(x0, x1), y0, y1);

                    if (!bySlot.TryGetValue(slot, out var codes))
                        bySlot[slot] = codes = [];
                    codes.Add(glyph.Code);
                }
            }

            foreach (var (slot, codes) in bySlot)
            {
                if (codes.Count <= 1) continue;

                sharedSlots.Add(
                    $"{font.BaseName} PageIndex={slot.PageIndex} x=[{slot.MinX}..{slot.MaxX}) y=[{slot.MinY}..{slot.MaxY}): " +
                    $"коди {string.Join(", ", codes.Select(c => $"0x{c:X2}"))} ділять ОДИН слот");
            }
        }

        report.Log($"=== [{label}] Унікальність фізичного слоту (PageIndex + прямокутник) на код (перевірка перед GlyphAtlasPatcher) ===");
        report.Log($"    Перевірено гліф-записів: {totalChecked}");
        report.Log($"    Слотів, які ділять 2+ коди: {sharedSlots.Count}");

        if (sharedSlots.Count > 0)
        {
            report.Log("    --- Спільні слоти ---");
            foreach (var m in sharedSlots)
                report.Log($"      {m}");
        }

        report.Log(sharedSlots.Count == 0
            ? "    UA: ПІДТВЕРДЖЕНО — кожен фізичний слот (PageIndex + прямокутник) належить РІВНО одному коду. " +
              "GlyphAtlasPatcher може перезаписувати слот donor-коду, не боячись зачепити інший символ."
            : "    UA: ЗНАЙДЕНО СПІЛЬНІ СЛОТИ — перед перезаписом перелічених вище кодів перевірити вручну, " +
              "чи один із них дійсно безпечний донор, а не помилково зарахований.");
    }
}
