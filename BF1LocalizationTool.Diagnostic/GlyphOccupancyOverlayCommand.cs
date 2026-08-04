// =============================================================================
// BF1LocalizationTool.Diagnostic — GlyphOccupancyOverlayCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Застереження з DonorSlotGrowthPotentialCommand: "вільний простір"
//     там означає лише "не покритий ЖОДНИМ FBOD-записом ЦЬОГО шрифту" —
//     це ще не доказ, що там справді порожньо/чорно. Могло б бути щось
//     інше (недописаний mip-рівень, залишок іншого ресурсу).
//
//     Ця перевірка малює РЕАЛЬНІ пікселі текстури (як
//     ExportAllFontStylesContactSheetCommand) і поверх них — тонкий
//     контур КОЖНОГО відомого гліфа (лаймовий). Усе, що ПОЗА контурами
//     й водночас має видимі білі пікселі — сигнал "там щось є, хоч FBOD
//     мовчить, перевірити окремо перед довірою". Усе, що поза контурами
//     й порожнє (темне) — вільний простір підтверджено ВІЗУАЛЬНО, не
//     лише відсутністю запису в таблиці.
//
//     Робиться одразу по УСІХ сторінках УСІХ шрифтів обох ігор — це
//     діагностика (PNG), НЕ редагування файлу гри.
// EN: Caveat from DonorSlotGrowthPotentialCommand: "free space" there
//     only means "not covered by ANY FBOD record of THIS font" — that's
//     not yet proof it's genuinely blank/black. Could be something else
//     (an unfinished mip level, a leftover from another resource).
//
//     This check draws the REAL texture pixels (like
//     ExportAllFontStylesContactSheetCommand) and, on top, a thin outline
//     (lime) around EVERY known glyph. Anything OUTSIDE the outlines that
//     still shows visible white pixels is a signal: "something's there
//     even though FBOD is silent about it — check separately before
//     trusting it." Anything outside the outlines and empty (dark) —
//     free space confirmed VISUALLY, not just by absence of a record.
//
//     Done for ALL pages of ALL fonts in both games at once — this is
//     diagnostics (PNG), NOT editing the game file.
// =============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.Versioning;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

[SupportedOSPlatform("windows")]
public static class GlyphOccupancyOverlayCommand
{
    private readonly record struct GlyphRect(ushort Code, int MinX, int MaxX, int MinY, int MaxY);
    private const int Zoom = 3;
    private const int LabelHeight = 22;
    private const int Padding = 12;

    // UA: outputDir — опційна конкретна тека для ЦЬОГО запуску, щоб
    //     десятки PNG не накопичувались у спільній плоскій
    //     diagnostic-output. null (за замовчуванням) використовує плоску
    //     diagnostic-output — сумісно з наявним викликом №3 меню
    //     (перевірка ОРИГІНАЛЬНОГО файлу), для якого підпапка непотрібна.
    // EN: outputDir — an optional specific folder for THIS run, so dozens
    //     of PNGs don't pile up in the shared flat diagnostic-output.
    //     null (default) uses the flat diagnostic-output — compatible
    //     with the existing menu item 3 call (checking the ORIGINAL
    //     file), which doesn't need a subfolder.
    public static void Run(DiagnosticReport report, UcfbChunk root, string label, string? outputDir = null)
    {
        var fonts = FontChunkLocator.FindAll(root);

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var glyphs = FontGlyphTable.Parse(fbod.RawData);
            var rectsByPage = glyphs.GroupBy(g => g.PageIndex).ToDictionary(g => g.Key, g => g.ToList());

            for (var pageIndex = 0; pageIndex < font.TexturePages.Count; pageIndex++)
            {
                var page = font.TexturePages[pageIndex];
                var texPixels = FontTexturePixelReader.ReadMip0(page.Chunk);
                var texWidth = texPixels.Width;
                var texHeight = texPixels.Height;

                var rects = rectsByPage.TryGetValue((byte)pageIndex, out var pageGlyphs)
                    ? pageGlyphs.Select(g =>
                    {
                        var x0 = (int)Math.Round(g.U0 * texWidth);
                        var x1 = (int)Math.Round(g.U1 * texWidth);
                        var y0 = (int)Math.Round(g.V0 * texHeight);
                        var y1 = (int)Math.Round(g.V1 * texHeight);
                        return new GlyphRect(g.Code, Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1), Math.Max(y0, y1));
                    }).Where(r => r.MaxX - r.MinX > 0 && r.MaxY - r.MinY > 0).ToList()
                    : [];

                var canvasWidth = texWidth * Zoom;
                var canvasHeight = texHeight * Zoom + LabelHeight;

                using var bitmap = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.FromArgb(255, 10, 10, 20));
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    using var labelFont = new Font("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel);
                    g.DrawString($"{font.BaseName}/{page.Name} ({texWidth}x{texHeight}) — {rects.Count} відомих гліфів",
                        labelFont, Brushes.Gray, Padding, 2);

                    // UA: Реальні пікселі текстури, x Zoom.
                    // EN: Real texture pixels, x Zoom.
                    using var pageBitmap = new Bitmap(texWidth, texHeight, PixelFormat.Format32bppArgb);
                    for (var y = 0; y < texHeight; y++)
                        for (var x = 0; x < texWidth; x++)
                        {
                            var pixelIndex = y * texWidth + x;
                            var (a, r, gg, b) = FontTexturePixelReader.DecodePixel(texPixels.RawA4R4G4B4Pixels, pixelIndex);
                            if (a > 0) pageBitmap.SetPixel(x, y, Color.FromArgb(255, r, gg, b));
                        }

                    g.DrawImage(pageBitmap, new Rectangle(0, LabelHeight, canvasWidth, texHeight * Zoom));

                    // UA: Контур КОЖНОГО відомого гліфа — лаймовий, 1px
                    //     (у зумованих координатах).
                    // EN: Outline of EVERY known glyph — lime, 1px (in
                    //     zoomed coordinates).
                    using var outlinePen = new Pen(Color.Lime, 1);
                    foreach (var r in rects)
                    {
                        var rectX = r.MinX * Zoom;
                        var rectY = LabelHeight + r.MinY * Zoom;
                        var rectW = (r.MaxX - r.MinX) * Zoom;
                        var rectH = (r.MaxY - r.MinY) * Zoom;
                        g.DrawRectangle(outlinePen, rectX, rectY, rectW, rectH);
                    }
                }

                var dir = outputDir ?? Path.Combine(AppContext.BaseDirectory, "diagnostic-output");
                Directory.CreateDirectory(dir);
                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var outPath = Path.Combine(dir, $"GlyphOccupancy_{label}_{font.BaseName}_{page.Name}_{stamp}.png");
                bitmap.Save(outPath, ImageFormat.Png);

                report.Log($"=== [{label}] {font.BaseName}/{page.Name}: контурна перевірка зайнятості ===");
                report.Log($"UA: Збережено у: {outPath}");
                report.Log($"EN: Saved to: {outPath}");
            }
        }
    }
}
