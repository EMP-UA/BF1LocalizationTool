// =============================================================================
// BF1LocalizationTool.Diagnostic — GlyphFitRenderPreviewCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Останній крок перед рішенням щодо спотворення в BF1 (~40-50%
//     log-відхилення за GlyphDonorAssignmentPreviewCommand): не вірити
//     цифрі на слово, а ПОБАЧИТИ реальний рендер. Для ОДНОГО шрифту
//     (обраного вручну) рендерить УСІ 66 призначень
//     (GlyphDonorMatcher + GlyphBoxFitRenderer, той самий пайплайн, що
//     піде в GlyphAtlasPatcher) у сітку — кожна клітинка РІВНО в
//     реальному розмірі донорського слоту (з масштабом x6 для видимості,
//     розміри слотів тут 1-30px — на екрані такими їх не оцінити).
//
//     Не пише нічого у файл гри — лише PNG для перегляду.
// EN: The final step before deciding on the BF1 distortion (~40-50%
//     log-deviation per GlyphDonorAssignmentPreviewCommand): don't take
//     the number's word for it, SEE the real render. For ONE font
//     (chosen manually), renders ALL 66 assignments
//     (GlyphDonorMatcher + GlyphBoxFitRenderer, the same pipeline that
//     will go into GlyphAtlasPatcher) into a grid — each cell EXACTLY at
//     the donor slot's real size (x6 zoom for visibility, slot sizes
//     here are 1-30px — unusable for judgment on screen otherwise).
//
//     Writes nothing to the game file — just a PNG for viewing.
// =============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.FontGenerator.Matching;

namespace BF1LocalizationTool.Diagnostic;

[SupportedOSPlatform("windows")]
public static class GlyphFitRenderPreviewCommand
{
    private readonly record struct GlyphRect(ushort Code, int MinX, int MaxX, int MinY, int MaxY);

    private const int Zoom = 6;
    private const int CellPadding = 16;
    private const int LabelHeight = 16;
    private const int Columns = 8;

    public static async Task Run(DiagnosticReport report, UcfbChunk root, string filePath, string label, string fontBaseName, string fontFamilyName, int neededGlyphCodes)
    {
        var summary = await SoftDonorAnalysis.BuildSummaryAsync(filePath);

        // UA: ДРУКОВНІ ASCII-символи (0x20-0x7E) завжди вважаються
        //     "зайнятими", незалежно від результату Locl-сканування:
        //     деякі рядки гри (напр. "Exit to Windows", де мала 'w'
        //     показується напряму) використовують друковні ASCII-коди
        //     поза таблицею Locl, тож саме лише Locl-сканування не
        //     доводить, що такий код вільний. Лише недруковні керівні
        //     коди (0x00-0x1F, 0x7F) лишаються кандидатами на основі
        //     даних реального сканування.
        // EN: PRINTABLE ASCII (0x20-0x7E) is always considered "used",
        //     regardless of the Locl scan result: some game strings
        //     (e.g. "Exit to Windows", where the lowercase 'w' is shown
        //     directly) use printable ASCII codes outside the Locl
        //     table, so the Locl scan alone doesn't prove such a code is
        //     free. Only non-printable control codes (0x00-0x1F, 0x7F)
        //     remain candidates based on real scan data.
        bool IsPrintableAscii(ushort code) => code is >= 0x20 and <= 0x7E;
        var usedByAnyLanguage = summary.Languages.SelectMany(l => l.CodeCounts.Keys).ToHashSet();
        bool IsUsed(ushort code) => IsPrintableAscii(code) || usedByAnyLanguage.Contains(code);

        var englishUsed = summary.Languages
            .Where(l => l.Language.Contains("english", StringComparison.OrdinalIgnoreCase))
            .SelectMany(l => l.CodeCounts.Keys)
            .ToHashSet();

        var baseSafeCandidates = Enumerable.Range(0, 256).Where(x => !IsUsed((ushort)x)).ToHashSet();
        var needsSoftDonors = baseSafeCandidates.Count < neededGlyphCodes;
        var softCandidateCodes = needsSoftDonors
            ? SoftDonorAnalysis.ComputeSoftCandidates(summary).Select(c => c.Code).ToHashSet()
            : [];

        var font = FontChunkLocator.FindAll(root).FirstOrDefault(f => f.BaseName == fontBaseName);
        if (font is null) { report.Log($"UA: [{label}] Шрифт '{fontBaseName}' не знайдено."); return; }

        var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
        if (fbod is null) { report.Log($"UA: [{label}] FBOD у '{fontBaseName}' не знайдено."); return; }

        var glyphs = FontGlyphTable.Parse(fbod.RawData);
        var unsafeBase = new HashSet<int>();
        var unsafeSoft = new HashSet<int>();
        var zeroArea = new HashSet<int>();
        var geometryByCode = new Dictionary<ushort, (int Width, int Height)>();

        foreach (var pageGroup in glyphs.GroupBy(g => g.PageIndex))
        {
            if (pageGroup.Key >= font.TexturePages.Count) continue;

            var texPixels = FontTexturePixelReader.ReadMip0(font.TexturePages[pageGroup.Key].Chunk);

            var rects = pageGroup.Select(g =>
            {
                var x0 = (int)Math.Round(g.U0 * texPixels.Width);
                var x1 = (int)Math.Round(g.U1 * texPixels.Width);
                var y0 = (int)Math.Round(g.V0 * texPixels.Height);
                var y1 = (int)Math.Round(g.V1 * texPixels.Height);
                return new GlyphRect(g.Code, Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1), Math.Max(y0, y1));
            }).ToList();

            foreach (var r in rects)
            {
                geometryByCode[r.Code] = (r.MaxX - r.MinX, r.MaxY - r.MinY);
                if (r.MaxX - r.MinX <= 0 || r.MaxY - r.MinY <= 0)
                    zeroArea.Add(r.Code);
            }

            for (var i = 0; i < rects.Count; i++)
            {
                for (var j = i + 1; j < rects.Count; j++)
                {
                    var a = rects[i];
                    var b = rects[j];

                    var overlapsX = a.MinX < b.MaxX && b.MinX < a.MaxX;
                    var overlapsY = a.MinY < b.MaxY && b.MinY < a.MaxY;
                    if (!(overlapsX && overlapsY)) continue;

                    var aUsed = IsUsed(a.Code);
                    var bUsed = IsUsed(b.Code);
                    if (aUsed != bUsed)
                        unsafeBase.Add(aUsed ? b.Code : a.Code);

                    if (softCandidateCodes.Contains(a.Code) && englishUsed.Contains(b.Code))
                        unsafeSoft.Add(a.Code);
                    if (softCandidateCodes.Contains(b.Code) && englishUsed.Contains(a.Code))
                        unsafeSoft.Add(b.Code);
                }
            }
        }

        var donors = new List<DonorSlot>();
        foreach (var code in baseSafeCandidates.Except(unsafeBase).Except(zeroArea))
            if (geometryByCode.TryGetValue((ushort)code, out var geo))
                donors.Add(new DonorSlot((ushort)code, geo.Width, geo.Height));

        if (needsSoftDonors)
            foreach (var code in softCandidateCodes.Except(unsafeSoft).Except(zeroArea))
                if (geometryByCode.TryGetValue((ushort)code, out var geo))
                    donors.Add(new DonorSlot((ushort)code, geo.Width, geo.Height));

        var allFontGlyphs = geometryByCode.Select(kv => new KnownGlyphGeometry(kv.Key, kv.Value.Width, kv.Value.Height)).ToList();
        var referenceCapHeight = GlyphDonorMatcher.GetReferenceCapHeight(allFontGlyphs);
        donors = GlyphDonorMatcher.FilterOutTooSmall(donors, referenceCapHeight);

        report.Log($"UA: [{label}] {fontBaseName}: еталонна висота великої літери (медіана A-Z) = {referenceCapHeight}px, донорів після фільтра розміру: {donors.Count}");

        if (donors.Count < CyrillicAlphabet.AllLetters.Count)
        {
            report.Log($"UA: [{label}] {fontBaseName}: донорів замало ({donors.Count} < {CyrillicAlphabet.AllLetters.Count}).");
            return;
        }

        var assignments = GlyphDonorMatcher.Assign(CyrillicAlphabet.AllLetters, donors, fontFamilyName, referenceCapHeight)
            .OrderBy(a => a.Character)
            .ToList();

        var maxCellWidth = assignments.Max(a => a.CanvasWidth) * Zoom;
        var maxCellHeight = assignments.Max(a => a.CanvasHeight) * Zoom;
        var rows = (int)Math.Ceiling(assignments.Count / (double)Columns);

        var imageWidth = Columns * (maxCellWidth + CellPadding) + CellPadding;
        var imageHeight = rows * (maxCellHeight + LabelHeight + CellPadding) + CellPadding;

        using var bitmap = new Bitmap(imageWidth, imageHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.FromArgb(255, 10, 10, 20));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using var labelFont = new Font("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel);

            for (var i = 0; i < assignments.Count; i++)
            {
                var a = assignments[i];
                var col = i % Columns;
                var row = i / Columns;
                var cellX = CellPadding + col * (maxCellWidth + CellPadding);
                var cellY = CellPadding + row * (maxCellHeight + LabelHeight + CellPadding);

                g.DrawString($"{a.Character} 0x{a.DonorCode:X2}", labelFont, Brushes.Gray, cellX, cellY);

                var rendered = GlyphBoxFitRenderer.RenderToFit(a.Character, fontFamilyName, a.CanvasWidth, a.CanvasHeight);

                using var glyphBitmap = new Bitmap(a.CanvasWidth, a.CanvasHeight, PixelFormat.Format32bppArgb);
                var data = glyphBitmap.LockBits(new Rectangle(0, 0, a.CanvasWidth, a.CanvasHeight), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                for (var y = 0; y < a.CanvasHeight; y++)
                    Marshal.Copy(rendered.BgraPixels, y * a.CanvasWidth * 4, data.Scan0 + y * data.Stride, a.CanvasWidth * 4);
                glyphBitmap.UnlockBits(data);

                var destRect = new Rectangle(cellX, cellY + LabelHeight, a.CanvasWidth * Zoom, a.CanvasHeight * Zoom);
                g.DrawImage(glyphBitmap, destRect);
            }
        }

        var dir = Path.Combine(AppContext.BaseDirectory, "diagnostic-output");
        Directory.CreateDirectory(dir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outPath = Path.Combine(dir, $"GlyphFitPreview_{label}_{fontBaseName}_{stamp}.png");
        bitmap.Save(outPath, ImageFormat.Png);

        report.Log($"=== [{label}] {fontBaseName}: рендер-прев'ю {assignments.Count} призначень ===");
        report.Log($"UA: Збережено у: {outPath}");
        report.Log($"EN: Saved to: {outPath}");
    }
}
