// =============================================================================
// BF1LocalizationTool.Diagnostic — GlyphAtlasStyleReportCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Друкує розбивку GlyphAtlasStyleAnalyzer по КОЖНІЙ (шрифт, сторінка)
//     парі окремо, а тоді — загальний підсумок.
//
//     КАТЕГОРІЇ НАЗВАНІ НЕЙТРАЛЬНО, БЕЗ прив'язки до гри (напр. НЕ
//     "стиль BF1"/"стиль BF2"): реальні дані показують мішанину в межах
//     ОДНОГО файлу ОДНІЄЇ гри (напр. gamefont_large_tex0 88% білий, tex1
//     21% білий) — тобто "стиль" не є властивістю гри чи навіть ресурсу,
//     і приписування категорій до конкретної гри було б неточним.
//     Інструмент лише РЕЄСТРУЄ факти (скільки пікселів якого типу), і
//     не робить висновок за нас — висновок робить людина за підсумком.
// EN: Prints GlyphAtlasStyleAnalyzer's breakdown per EACH (font, page)
//     pair, then an overall summary.
//
//     CATEGORIES ARE NAMED NEUTRALLY, with NO game attribution (e.g. NOT
//     "BF1 style"/"BF2 style"): real data shows a mix WITHIN a SINGLE file
//     of a SINGLE game (e.g. gamefont_large_tex0 88% white, tex1 21%
//     white) — meaning "style" is not a property of the game or even the
//     resource, and attributing categories to a specific game would be
//     inaccurate. The tool only RECORDS facts (pixel counts per
//     category), and does not draw the conclusion for us — the human
//     draws the conclusion from the summary.
// =============================================================================

using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GlyphAtlasStyleReportCommand
{
    // UA: Аналіз ОДНОГО файлу. label — лише для заголовка виводу
    //     (напр. "core.lvl (шлях)"), НЕ визначає жодної логіки аналізу.
    // EN: Analysis of a SINGLE file. label — only for the output header
    //     (e.g. "core.lvl (path)"), does NOT drive any analysis logic.
    public static IReadOnlyList<GlyphTransparentPixelStats> Run(DiagnosticReport report, string lvlFilePath, string label)
    {
        var root = UcfbReader.ReadFile(lvlFilePath);
        var stats = GlyphAtlasStyleAnalyzer.AnalyzeFile(root);

        report.Log();
        report.Log($"=== Стиль прозорих пікселів: {label} ({lvlFilePath}) ===");
        report.Log($"=== Transparent-pixel style: {label} ({lvlFilePath}) ===");

        if (stats.Count == 0)
        {
            report.Log("UA: Шрифтів не знайдено в цьому файлі. / EN: No fonts found in this file.");
            return stats;
        }

        foreach (var s in stats)
        {
            report.Log($"  {s.FontBaseName} / {s.TexturePageName}: " +
                $"{s.TotalGlyphs} гліфів, {s.TotalPixelsChecked} пікселів — " +
                $"A0&RGB=white: {s.ZeroAlphaWhiteRgbCount}, " +
                $"A0&RGB=0: {s.ZeroAlphaZeroRgbCount}, " +
                $"A0&RGB=інше: {s.ZeroAlphaOtherRgbCount}, " +
                $"A>0&RGB≠white: {s.NonZeroAlphaNonWhiteRgbCount}");
        }

        PrintTotals(report, stats, label);
        return stats;
    }

    // UA: Друкує лише суму лічильників — БЕЗ жодного висновку/вердикту
    //     (ні "коректно", ні "стиль гри X") — це проста арифметика над
    //     фактично виміряними числами, інтерпретацію лишаємо людині.
    // EN: Prints only the summed counters — WITHOUT any conclusion or
    //     verdict (no "correct", no "game X's style") — this is plain
    //     arithmetic over actually measured numbers, interpretation is
    //     left to the human.
    private static void PrintTotals(DiagnosticReport report, IReadOnlyList<GlyphTransparentPixelStats> stats, string label)
    {
        var totalWhite = stats.Sum(s => s.ZeroAlphaWhiteRgbCount);
        var totalZero = stats.Sum(s => s.ZeroAlphaZeroRgbCount);
        var totalOther = stats.Sum(s => s.ZeroAlphaOtherRgbCount);
        var totalAnomaly = stats.Sum(s => s.NonZeroAlphaNonWhiteRgbCount);
        var totalPixels = stats.Sum(s => s.TotalPixelsChecked);

        report.Log();
        report.Log($"  --- Підсумок {label}: усього {totalPixels} пікселів ---");
        report.Log($"  Alpha=0, RGB=білий:  {totalWhite}");
        report.Log($"  Alpha=0, RGB=0:      {totalZero}");
        report.Log($"  Alpha=0, RGB=інше:   {totalOther}");
        report.Log($"  Alpha>0, RGB≠білий:  {totalAnomaly}");
    }

    // UA: Аналіз ДВОХ файлів (BF1 + BF2) ОДНИМ викликом — той самий
    //     патерн, що AnalyzeBothGames() у Program.cs. Не примушує
    //     запускати команду двічі вручну.
    // EN: Analysis of TWO files (BF1 + BF2) in ONE call — same pattern
    //     as AnalyzeBothGames() in Program.cs. Doesn't force running the
    //     command twice manually.
    public static void RunBoth(DiagnosticReport report, string bf1Path, string bf2Path)
    {
        var bf1Stats = Run(report, bf1Path, "BF1 (Classic 2004)");
        var bf2Stats = Run(report, bf2Path, "BF2 (Classic)");

        report.Log();
        report.Log("=== Об'єднаний підсумок (BF1 + BF2 разом) / Combined summary (BF1 + BF2 together) ===");
        PrintTotals(report, [.. bf1Stats, .. bf2Stats], "BF1+BF2 разом");
    }
}