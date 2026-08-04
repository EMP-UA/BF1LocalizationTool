// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateEnlargedFontCoreCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Крок ЗБІЛЬШЕННЯ шрифту (upscale-first). Бере наявний core.lvl і
//     перепаковує КОЖЕН шрифт у свіжий атлас із коефіцієнтом scale>1:
//     кожен гліф масштабується bilinear-ресемплінгом, а метрики курсора
//     (XAdvance/Bearing/CellHeight/InkWidth) множаться на scale. Коди й
//     таблиця символів НЕ змінюються — тому GUI та вже перекладений текст
//     лишаються сумісними.
//
//     ПРИЗНАЧЕННЯ: емпірично довести В ГРІ, що текст стає БІЛЬШИМ і що
//     верстка лишається коректною (бо масштабуються ВЖЕ ПРАВИЛЬНІ ванільні
//     метрики — на відміну від рендеру з нуля, де метрики/базову лінію ще
//     треба виводити). Розмитість від масштабування бітмапів очікувана й
//     тимчасова — наступний крок замінить upscale на чіткий рендер із TTF.
//
//     ВАЖЛИВО: сторінки лишаються ТОГО САМОГО розміру, що оригінал (екранний
//     розмір бере двигун із нормованого UV-прольоту — див. FontRepacker).
//     Більші гліфи → більший UV-проліт → більший текст. Кількість сторінок
//     зростає (гліфи більші) — це нормально, HEAD.pageCount ставиться вірно.
// EN: The font ENLARGEMENT step (upscale-first). Takes an existing core.lvl
//     and repacks EACH font into a fresh atlas with a scale>1 factor: each
//     glyph is bilinearly resampled, and the cursor metrics
//     (XAdvance/Bearing/CellHeight/InkWidth) are multiplied by scale. The
//     codes and character table are UNCHANGED — so the GUI and already-
//     translated text stay compatible.
//
//     PURPOSE: prove IN-GAME that text becomes BIGGER and that the layout
//     stays correct (because ALREADY-CORRECT vanilla metrics are scaled —
//     unlike from-scratch rendering, where metrics/baseline are not yet
//     derived). Blur from bitmap upscaling is expected and temporary — the
//     next step replaces the upscale with crisp TTF rendering.
//
//     IMPORTANT: pages stay the SAME size as the original (the engine derives
//     on-screen size from the normalized UV span — see FontRepacker). Bigger
//     glyphs → bigger UV span → bigger text. The page COUNT grows (glyphs are
//     bigger) — that's fine, HEAD.pageCount is set correctly.
// =============================================================================

using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateEnlargedFontCoreCommand
{
    public static string? Run(DiagnosticReport report, string coreLvlPath, string outputDir, float scale)
    {
        if (!File.Exists(coreLvlPath))
        {
            report.Log($"UA: core.lvl не знайдено: {coreLvlPath} — пропускаю.");
            report.Log($"EN: core.lvl not found: {coreLvlPath} — skipping.");
            return null;
        }

        report.Log($"UA: Читаю {coreLvlPath} (коефіцієнт збільшення ×{scale})...");
        report.Log($"EN: Reading {coreLvlPath} (enlargement factor ×{scale})...");
        var root = UcfbReader.ReadFile(coreLvlPath);
        var fonts = FontChunkLocator.FindAll(root);

        foreach (var font in fonts)
        {
            var original = FontResourceReader.Read(font.Chunk);

            var pageW = original.Pages[0].Width;
            var pageH = original.Pages[0].Height;
            var enlarged = FontRepacker.Repack(original, pageW, pageH, scale: scale);

            report.Log(
                $"UA: [{font.BaseName}] сторінок {original.Pages.Count}→{enlarged.Pages.Count} ({pageW}×{pageH}), " +
                $"гліфів={enlarged.Glyphs.Count}, ×{scale}");
            report.Log(
                $"EN: [{font.BaseName}] pages {original.Pages.Count}→{enlarged.Pages.Count} ({pageW}×{pageH}), " +
                $"glyphs={enlarged.Glyphs.Count}, ×{scale}");

            var rebuilt = FontResourceBuilder.Build(enlarged);
            font.Chunk.Children.Clear();
            font.Chunk.Children.AddRange(rebuilt.Children);
        }

        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "core.lvl");
        UcfbWriter.WriteFile(outputPath, root);

        var origSize = new FileInfo(coreLvlPath).Length;
        var newSize = new FileInfo(outputPath).Length;

        report.Log();
        report.Log($"UA: Записано збільшений core.lvl: {outputPath} (оригінал={origSize}Б, новий={newSize}Б)");
        report.Log($"EN: Wrote enlarged core.lvl: {outputPath} (original={origSize}B, new={newSize}B)");
        report.Log("UA: Тепер перевірка В ГРІ: текст має бути БІЛЬШИМ, верстка коректною (розмитість бітмапів — тимчасова, далі TTF).");
        report.Log("EN: Now the IN-GAME check: text must be BIGGER with correct layout (bitmap blur is temporary, TTF next).");

        return outputPath;
    }
}
