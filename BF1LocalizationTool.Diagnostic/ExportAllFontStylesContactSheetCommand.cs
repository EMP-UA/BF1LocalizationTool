// =============================================================================
// BF1LocalizationTool.Diagnostic — ExportAllFontStylesContactSheetCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Допоміжний, не-перевірочний інструмент (продовження
//     ExportTexturePagePngCommand). Один запуск на файл — ОДИН PNG з
//     ПЕРШОЮ текстурною сторінкою КОЖНОГО шрифтового ресурсу файлу,
//     підписаною ім'ям шрифту, складені одна під одною.
//
//     Призначення: підбір Windows-шрифта-замінника перевіряється окремо
//     для КОЖНОГО з мінімум 6 шрифтових ресурсів гри, а не лише для
//     одного зразка (gamefont_medium) — назва "starwars_small", напр.,
//     натякає, що це може бути стилістично ІНШИЙ шрифт (як у SteamWorld
//     Heist, де різні шрифти гри вимагали різних Windows-відповідників).
//
//     Береться лише ПЕРША сторінка кожного шрифту (TexturePages[0]) —
//     для порівняння СТИЛЮ решта сторінок того самого шрифту зазвичай
//     надлишкові (той самий дизайн, інший підмножина символів).
// EN: Auxiliary, non-verification tool (continuation of
//     ExportTexturePagePngCommand). One run per file — ONE PNG with the
//     FIRST texture page of EVERY font resource in the file, labeled
//     with the font name, stacked one under another.
//
//     Purpose: a Windows replacement font is verified separately for
//     EACH of the game's at least 6 font resources, not just one sample
//     (gamefont_medium) — the name "starwars_small", for instance, hints
//     it might be a stylistically DIFFERENT font (as in SteamWorld
//     Heist, where different in-game fonts needed different Windows
//     equivalents).
//
//     Only the FIRST page of each font (TexturePages[0]) is taken — for
//     STYLE comparison the remaining pages of the same font are usually
//     redundant (same design, a different character subset).
// =============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.Versioning;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

[SupportedOSPlatform("windows")]
public static class ExportAllFontStylesContactSheetCommand
{
    private const int LabelHeight = 24;
    private const int Padding = 12;

    public static void Run(DiagnosticReport report, string lvlFilePath, string label)
    {
        var fonts = FontChunkLocator.FindAll(UcfbReader.ReadFile(lvlFilePath));
        if (fonts.Count == 0)
        {
            report.Log("UA: Шрифтових ресурсів не знайдено. / EN: No font resources found.");
            return;
        }

        var pages = fonts
            .Where(f => f.TexturePages.Count > 0)
            .Select(f => (Font: f, Pixels: FontTexturePixelReader.ReadMip0(f.TexturePages[0].Chunk)))
            .ToList();

        var canvasWidth = Padding * 2 + pages.Max(p => p.Pixels.Width);
        var canvasHeight = Padding + pages.Sum(p => LabelHeight + p.Pixels.Height + Padding);

        using var bitmap = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.FromArgb(255, 10, 10, 20));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using var labelFont = new Font("Segoe UI", 14, FontStyle.Regular, GraphicsUnit.Pixel);

            var y = Padding;
            foreach (var (font, pixels) in pages)
            {
                g.DrawString($"{font.BaseName}  ({pixels.Width}x{pixels.Height})", labelFont, Brushes.Gray, Padding, y);
                y += LabelHeight;

                for (var py = 0; py < pixels.Height; py++)
                {
                    for (var px = 0; px < pixels.Width; px++)
                    {
                        var pixelIndex = py * pixels.Width + px;
                        var (a, r, gg, b) = FontTexturePixelReader.DecodePixel(pixels.RawA4R4G4B4Pixels, pixelIndex);
                        if (a == 0) continue; // UA: лишаємо темне тло / EN: leave the dark background
                        bitmap.SetPixel(Padding + px, y + py, Color.FromArgb(255, r, gg, b));
                    }
                }

                y += pixels.Height + Padding;
            }
        }

        var dir = Path.Combine(AppContext.BaseDirectory, "diagnostic-output");
        Directory.CreateDirectory(dir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outPath = Path.Combine(dir, $"FontStylesContactSheet_{label}_{stamp}.png");
        bitmap.Save(outPath, ImageFormat.Png);

        report.Log($"=== [{label}] Контактний аркуш усіх шрифтових ресурсів ({pages.Count} шт.) ===");
        report.Log($"UA: Збережено у: {outPath}");
        report.Log($"EN: Saved to: {outPath}");
    }
}
