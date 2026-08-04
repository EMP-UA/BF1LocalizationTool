// =============================================================================
// BF1LocalizationTool.Diagnostic — FontCandidatePreviewCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Одноразовий допоміжний інструмент (НЕ перевірка формату): рендерить
//     той самий кириличний зразок КОЖНИМ кандидатом шрифту поруч, в
//     одному PNG з підписами — щоб порівнювати ВІЗУАЛЬНО, а не за
//     назвами. Використовує звичайний Graphics.DrawString (НЕ
//     GdiGlyphRasterizer) — тут важлива читабельність зразка людиною,
//     а не точна геометрія для донорського слоту.
//
//     Явно перевіряє наявність кожного кандидата через
//     InstalledFontCollection ПЕРЕД спробою малювання — якщо назва не
//     знайдена, GDI+ мовчки підставляє дефолтний шрифт, що дало б
//     оманливий результат порівняння. Кандидат без систем-фонту
//     позначається "НЕ ЗНАЙДЕНО" і пропускається, а не малюється
//     непомітно неправильним шрифтом.
// EN: A one-off auxiliary tool (NOT a format check): renders the same
//     Cyrillic sample with EACH font candidate side by side, in one PNG
//     with labels — for VISUAL comparison, not comparison by name. Uses
//     plain Graphics.DrawString (NOT GdiGlyphRasterizer) — human
//     readability of the sample matters here, not exact geometry for a
//     donor slot.
//
//     Explicitly checks each candidate's presence via
//     InstalledFontCollection BEFORE attempting to draw — if the name
//     isn't found, GDI+ silently substitutes a default font, which would
//     give a misleading comparison. A candidate with no matching system
//     font is marked "NOT FOUND" and skipped, rather than being silently
//     drawn with the wrong font.
// =============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.Versioning;

namespace BF1LocalizationTool.Diagnostic;

[SupportedOSPlatform("windows")]
public static class FontCandidatePreviewCommand
{
    // UA: Зразок навмисно включає складні для звірки кириличні
    //     літери — специфічно українські (Ґ, Є, І, Ї), широкі (Ш, Щ, Ю,
    //     Я, Ж) і вузькі (І) — щоб побачити пропорції кандидата саме на
    //     тих символах, де відмінності найпомітніші.
    // EN: The sample deliberately includes Cyrillic letters that are
    //     hardest to judge — specifically Ukrainian ones (Ґ, Є, І, Ї),
    //     wide ones (Ш, Щ, Ю, Я, Ж), and narrow ones (І) — to see a
    //     candidate's proportions on exactly the characters where
    //     differences are most visible.
    private const string SampleUppercase = "АБВГҐДЕЄЖЗІЇЙЦЧШЩЮЯ";
    private const string SampleLowercase = "абвгґдеєжзіїйцчшщюя";

    private static readonly string[] Candidates =
    [
        "Bahnschrift",
        "Bahnschrift SemiBold",
        "Segoe UI",
        "Segoe UI Semibold",
    ];

    public static void Run(DiagnosticReport report)
    {
        var installed = new InstalledFontCollection();
        var installedNames = installed.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        report.Log("=== Наявність кандидатів шрифту в системі ===");
        report.Log("=== Candidate font availability on this system ===");
        foreach (var candidate in Candidates)
            report.Log($"    {candidate}: {(installedNames.Contains(candidate) ? "знайдено" : "НЕ ЗНАЙДЕНО — буде пропущено")}");

        var availableCandidates = Candidates.Where(c => installedNames.Contains(c)).ToList();
        if (availableCandidates.Count == 0)
        {
            report.Log("UA: Жодного кандидата не знайдено в системі — нема що порівнювати.");
            report.Log("EN: No candidates found on this system — nothing to compare.");
            return;
        }

        const int rowHeight = 120;
        const int fontSizePx = 40;
        const int imageWidth = 900;
        var imageHeight = rowHeight * availableCandidates.Count;

        using var bitmap = new Bitmap(imageWidth, imageHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.FromArgb(255, 10, 10, 20));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            for (var i = 0; i < availableCandidates.Count; i++)
            {
                var candidateName = availableCandidates[i];
                var rowTop = i * rowHeight;

                using var labelFont = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
                g.DrawString(candidateName, labelFont, Brushes.Gray, 10, rowTop + 4);

                using var sampleFont = new Font(candidateName, fontSizePx, FontStyle.Regular, GraphicsUnit.Pixel);
                g.DrawString(SampleUppercase, sampleFont, Brushes.White, 10, rowTop + 22);
                g.DrawString(SampleLowercase, sampleFont, Brushes.White, 10, rowTop + 22 + fontSizePx + 4);
            }
        }

        var dir = Path.Combine(AppContext.BaseDirectory, "diagnostic-output");
        Directory.CreateDirectory(dir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outPath = Path.Combine(dir, $"FontCandidatePreview_{stamp}.png");
        bitmap.Save(outPath, ImageFormat.Png);

        report.Log();
        report.Log($"UA: Збережено у: {outPath}");
        report.Log($"EN: Saved to: {outPath}");
    }
}
