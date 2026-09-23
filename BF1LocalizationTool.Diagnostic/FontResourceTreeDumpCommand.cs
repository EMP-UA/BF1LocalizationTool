// =============================================================================
// BF1LocalizationTool.Diagnostic — FontResourceTreeDumpCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Структурний факт-чек кількості FBOD-чанків. Проходить УСІ шрифти
//     ОБОХ ігор автоматично (FontChunkLocator.FindAll) — не один вручну
//     обраний приклад, оскільки попередній аналіз уже показав, що
//     поведінка суттєво відрізняється між розмірами шрифту й іграми
//     (gamefont_medium показав 0 безпечних донорів, gamefont_large — 55;
//     BF1 і BF2 мають різну кількість сторінок для однакових базових
//     імен) — тож припущення "один приклад репрезентує всі" саме тут
//     необґрунтоване.
// EN: Structural fact-check of FBOD chunk counts. Walks ALL fonts of
//     BOTH games automatically (FontChunkLocator.FindAll) — not a single
//     manually chosen example, since prior analysis already showed
//     behavior differs substantially between font sizes and games
//     (gamefont_medium showed 0 safe donors, gamefont_large showed 55;
//     BF1 and BF2 have different page counts for identical base names)
//     — so "one example represents all" is unwarranted here.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class FontResourceTreeDumpCommand
{
    // UA: verbose=true друкує повне дерево (корисно для одного шрифту),
    //     verbose=false — лише підсумок по кожному шрифту (для проходу
    //     по всіх одразу, щоб вивід не був завеликим).
    // EN: verbose=true prints the full tree (useful for a single font),
    //     verbose=false — summary per font only (for scanning all at
    //     once, so output doesn't become excessive).
    public static void RunAll(DiagnosticReport report, UcfbChunk root, string label, bool verbose)
    {
        var fonts = FontChunkLocator.FindAll(root);

        report.Log($"=== [{label}] Кількість FBOD-чанків по КОЖНОМУ шрифту ===");
        report.Log($"=== [{label}] FBOD chunk count for EVERY font ===");

        foreach (var font in fonts)
        {
            var fbodCounter = 0;
            if (verbose)
            {
                report.Log();
                report.Log($"--- {font.BaseName} ---");
                DumpRecursive(report, font.Chunk, depth: 0, ref fbodCounter);
            }
            else
            {
                CountOnly(font.Chunk, ref fbodCounter);
            }

            report.Log($"  {font.BaseName}: FBOD-чанків знайдено = {fbodCounter}" +
                               (fbodCounter <= 1 ? "  (OK)" : "  !! БІЛЬШЕ ОДНОГО !!"));
        }
    }

    private static void CountOnly(UcfbChunk chunk, ref int fbodCounter)
    {
        if (chunk.FourCC == "FBOD") fbodCounter++;
        foreach (var child in chunk.Children)
            CountOnly(child, ref fbodCounter);
    }

    private static void DumpRecursive(DiagnosticReport report, UcfbChunk chunk, int depth, ref int fbodCounter)
    {
        var indent = new string(' ', depth * 2);
        var label = chunk.IsFourCC ? chunk.FourCC : $"0x{chunk.Id:X8}(hash)";
        var recordHint = chunk.RawData.Length > 0 && chunk.RawData.Length % 24 == 0 && !chunk.HasChildren
            ? $"  [можливо таблиця 24-байтних записів: {chunk.RawData.Length / 24} шт.]"
            : "";

        if (chunk.FourCC == "FBOD")
        {
            fbodCounter++;
            report.Log($"{indent}[{label}] size={chunk.RawData.Length}{recordHint}  <<< [FBOD #{fbodCounter}]" +
                               (fbodCounter == 1 ? "  ← САМЕ ЦЕЙ повертає FindFirst!" : "  ← ІГНОРУЄТЬСЯ попереднім кодом!"));
        }
        else
        {
            report.Log($"{indent}[{label}] size={chunk.RawData.Length}{recordHint}");
        }

        foreach (var child in chunk.Children)
            DumpRecursive(report, child, depth + 1, ref fbodCounter);
    }
}