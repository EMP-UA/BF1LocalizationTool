// =============================================================================
// BF1LocalizationTool.Diagnostic — GlyphPixelDumpCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Діагностична команда для ЄДИНОЇ мети: перевірити перед написанням
//     FontGenerator-конвертера A4R4G4B4, чи справді донорські гліфи в
//     BODY мають RGB=білий (0xFF,0xFF,0xFF) і лише варіативну Alpha
//     (покриття), як передбачено в GdiGlyphRasterizer.
//
//     Виводить по кожному пікселю обраного гліфа (за кодом символу):
//     A, R, G, B у 8-бітному вигляді + позначку "RGB=WHITE" чи
//     "RGB≠WHITE" — щоб одразу було видно відхилення, не гортаючи
//     сирий hex вручну.
//
//     Це НЕ частина production-шляху FontGenerator — лише одноразова
//     (чи повторювана за потреби) перевірка припущення.
// EN: Diagnostic command with a SINGLE purpose: verify, before writing
//     the FontGenerator A4R4G4B4 converter, whether donor glyphs in
//     BODY truly have RGB=white (0xFF,0xFF,0xFF) with only Alpha
//     (coverage) varying, as assumed in GdiGlyphRasterizer.
//
//     Prints, for every pixel of a chosen glyph (by character code),
//     A, R, G, B as 8-bit values + an "RGB=WHITE" / "RGB≠WHITE" flag —
//     so deviations are visible immediately, without manually paging
//     through raw hex.
//
//     This is NOT part of the FontGenerator production path — just a
//     one-time (or repeatable, if needed) assumption check.
// =============================================================================

using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GlyphPixelDumpCommand
{
    // UA: Викликати з існуючого меню Diagnostic (напр. новий пункт
    //     "3 — Дамп пікселів гліфа"). lvlFilePath — шлях до core.lvl,
    //     обраний через уже наявний GetOpenFileNameW-пікер.
    // EN: Call from the existing Diagnostic menu (e.g. new item
    //     "3 — Dump glyph pixels"). lvlFilePath — path to core.lvl,
    //     chosen via the already-existing GetOpenFileNameW picker.
    public static void Run(DiagnosticReport report, string lvlFilePath, string fontBaseName, string texturePageName, ushort code)
    {
        var root = UcfbReader.ReadFile(lvlFilePath);
        var fonts = FontChunkLocator.FindAll(root);

        var font = fonts.FirstOrDefault(f => f.BaseName == fontBaseName);
        if (font is null)
        {
            report.Log($"UA: Шрифт '{fontBaseName}' не знайдено в цьому файлі / " +
                               $"EN: Font '{fontBaseName}' not found in this file");
            return;
        }

        var page = font.TexturePages.FirstOrDefault(p => p.Name == texturePageName);
        if (page is null)
        {
            report.Log($"UA: Текстурна сторінка '{texturePageName}' не знайдена. Доступні: " +
                               string.Join(", ", font.TexturePages.Select(p => p.Name)));
            return;
        }

        var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
        if (fbod is null)
        {
            report.Log("UA: FBOD не знайдено в цьому шрифті / EN: FBOD not found in this font");
            return;
        }

        var glyphs = FontGlyphTable.Parse(fbod.RawData);
        var glyph = FontGlyphTable.FindByCode(glyphs, code);
        if (glyph is null)
        {
            report.Log($"UA: Код 0x{code:X2} відсутній у таблиці гліфів цього шрифту / " +
                               $"EN: Code 0x{code:X2} is not present in this font's glyph table");
            return;
        }

        var texPixels = FontTexturePixelReader.ReadMip0(page.Chunk);

        var x0 = (int)Math.Round(glyph.U0 * texPixels.Width);
        var x1 = (int)Math.Round(glyph.U1 * texPixels.Width);
        var y0 = (int)Math.Round(glyph.V0 * texPixels.Height);
        var y1 = (int)Math.Round(glyph.V1 * texPixels.Height);

        // UA: На випадок якщо V0 > V1 у сирих даних — беремо min/max,
        //     напрямок осі V (top-down чи bottom-up) ще НЕ перевірявся.
        // EN: In case V0 > V1 in raw data — take min/max, V-axis
        //     direction (top-down or bottom-up) not yet verified.
        var minX = Math.Min(x0, x1);
        var maxX = Math.Max(x0, x1);
        var minY = Math.Min(y0, y1);
        var maxY = Math.Max(y0, y1);

        report.Log($"UA: Гліф code=0x{code:X2} ({(char)code}), " +
                           $"UV=({glyph.U0:F4},{glyph.V0:F4})..({glyph.U1:F4},{glyph.V1:F4}), " +
                           $"пікселі x=[{minX}..{maxX}) y=[{minY}..{maxY}), " +
                           $"текстура {texPixels.Width}x{texPixels.Height}, formatCode=0x{texPixels.FormatCode:X}");
        report.Log();

        var nonWhiteCount = 0;
        var totalCount = 0;

        for (var y = minY; y < maxY; y++)
        {
            var row = "";
            for (var x = minX; x < maxX; x++)
            {
                var pixelIndex = y * texPixels.Width + x;
                var (a, r, g, b) = FontTexturePixelReader.DecodePixel(texPixels.RawA4R4G4B4Pixels, pixelIndex);

                var isWhite = r == 0xFF && g == 0xFF && b == 0xFF;
                totalCount++;
                if (!isWhite) nonWhiteCount++;

                row += $"A{a:X2}R{r:X2}G{g:X2}B{b:X2}{(isWhite ? " " : "!")} ";
            }
            report.Log(row);
        }

        report.Log();
        report.Log(nonWhiteCount == 0
            ? $"UA: ПІДТВЕРДЖЕНО — усі {totalCount} пікселів мають RGB=білий. " +
              "Alpha несе покриття. Припущення в GdiGlyphRasterizer коректне."
            : $"UA: УВАГА — {nonWhiteCount}/{totalCount} пікселів НЕ мають RGB=білий (позначені '!'). " +
              "Припущення НЕВІРНЕ для цього шрифту/гри — конвертер A4R4G4B4 має враховувати справжню схему кольорів.");
    }
}