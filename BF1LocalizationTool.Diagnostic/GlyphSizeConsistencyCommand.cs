// =============================================================================
// BF1LocalizationTool.Diagnostic — GlyphSizeConsistencyCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Перевіряє, ЧИ ЗБІГАЄТЬСЯ розмір, який дає FBOD (ink_width, cell_h),
//     з розміром, який дає UV-прямокутник (round(U1×w)-round(U0×w),
//     round(V1×h)-round(V0×h)) — ПО ВСІХ гліфах усіх шрифтів/сторінок,
//     обидві гри, а не на одному вручну обраному прикладі.
//
//     Це два НЕЗАЛЕЖНІ джерела розміру гліфа в форматі. Якщо вони не
//     збігаються (навіть на 1 піксель через округлення) — GlyphAtlasPatcher
//     має орієнтуватись на UV-прямокутник (він визначає фізичне місце
//     запису в атласі), а ink_width/cell_h лишаються окремим метаданим
//     курсора (xadvance/bearing), не розміром для запису пікселів.
//     Якщо збігаються завжди — обидва джерела взаємозамінні, і
//     GlyphAtlasPatcher може використати будь-яке.
// EN: Verifies WHETHER the size given by FBOD (ink_width, cell_h)
//     MATCHES the size given by the UV rectangle (round(U1×w)-round(U0×w),
//     round(V1×h)-round(V0×h)) — across ALL glyphs of all fonts/pages,
//     both games, not a single manually chosen example.
//
//     These are two INDEPENDENT sources of glyph size in the format. If
//     they don't match (even by 1 pixel due to rounding) — GlyphAtlasPatcher
//     must rely on the UV rectangle (it determines the physical write
//     location in the atlas), and ink_width/cell_h remain separate cursor
//     metadata (xadvance/bearing), not the pixel-write size. If they
//     always match — both sources are interchangeable, and
//     GlyphAtlasPatcher can use either.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GlyphSizeConsistencyCommand
{
    public static void Run(DiagnosticReport report, UcfbChunk root, string label)
    {
        var fonts = FontChunkLocator.FindAll(root);

        var totalGlyphsChecked = 0;
        var widthMismatches = new List<string>();
        var heightMismatches = new List<string>();

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var glyphs = FontGlyphTable.Parse(fbod.RawData);

            foreach (var page in font.TexturePages)
            {
                var texPixels = FontTexturePixelReader.ReadMip0(page.Chunk);

                foreach (var glyph in glyphs)
                {
                    var x0 = (int)Math.Round(glyph.U0 * texPixels.Width);
                    var x1 = (int)Math.Round(glyph.U1 * texPixels.Width);
                    var y0 = (int)Math.Round(glyph.V0 * texPixels.Height);
                    var y1 = (int)Math.Round(glyph.V1 * texPixels.Height);

                    var uvWidth = Math.Abs(x1 - x0);
                    var uvHeight = Math.Abs(y1 - y0);

                    totalGlyphsChecked++;

                    if (uvWidth != glyph.InkWidth)
                        widthMismatches.Add(
                            $"{font.BaseName}/{page.Name} code=0x{glyph.Code:X2}: " +
                            $"FBOD.InkWidth={glyph.InkWidth}, UV-розрахунок={uvWidth}");

                    if (uvHeight != glyph.CellHeight)
                        heightMismatches.Add(
                            $"{font.BaseName}/{page.Name} code=0x{glyph.Code:X2}: " +
                            $"FBOD.CellHeight={glyph.CellHeight}, UV-розрахунок={uvHeight}");
                }
            }
        }

        report.Log($"=== [{label}] Узгодженість розміру: FBOD (ink_width/cell_h) проти UV-прямокутника ===");
        report.Log($"    Перевірено гліф-записів (усі шрифти × усі сторінки × усі гліфи): {totalGlyphsChecked}");
        report.Log($"    Розбіжність ширини (InkWidth vs UV):  {widthMismatches.Count}");
        report.Log($"    Розбіжність висоти (CellHeight vs UV): {heightMismatches.Count}");

        if (widthMismatches.Count > 0)
        {
            report.Log("    --- Приклади розбіжностей ширини (до 10) ---");
            foreach (var m in widthMismatches.Take(10))
                report.Log($"      {m}");
        }

        if (heightMismatches.Count > 0)
        {
            report.Log("    --- Приклади розбіжностей висоти (до 10) ---");
            foreach (var m in heightMismatches.Take(10))
                report.Log($"      {m}");
        }

        report.Log(widthMismatches.Count == 0 && heightMismatches.Count == 0
            ? "    UA: ПІДТВЕРДЖЕНО — FBOD і UV-прямокутник дають ІДЕНТИЧНИЙ розмір для КОЖНОГО гліфа."
            : "    UA: РОЗБІЖНІСТЬ — FBOD.InkWidth/CellHeight і UV-розрахунок НЕ завжди збігаються. " +
              "GlyphAtlasPatcher має орієнтуватись на UV-прямокутник для розміру запису пікселів, " +
              "а ink_width/cell_h лишити окремим метаданим курсора.");
    }
}