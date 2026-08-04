// =============================================================================
// BF1LocalizationTool.Diagnostic — FontBodyPhantomChildCheckCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: КРИТИЧНА перевірка, що випливає з FourCCPhantomChildScanCommand:
//     "BODY" — саме FourCC, у якому лежать пікселі шрифтових текстур —
//     має 30722/30730 фантомних дітей на 12/20 екземплярів. Це означає,
//     що ЩОНАЙМЕНШЕ ДЕЯКІ BODY-чанки текстур ПОВНІСТЮ (не частково)
//     розпарсились як дерево сміттєвих дітей, замість того, щоб лишитись
//     листком (Children.Count==0) — бо перевірка "повне покриття end" з
//     розділу 6 специфікації випадково виконалась через статистичну
//     природу байтів A4R4G4B4.
//
//     ЦЕ КРИТИЧНО ДЛЯ GlyphAtlasPatcher: UcfbWriter.WriteChunk застосовує
//     replacements ТІЛЬКИ якщо chunk.HasChildren==false. Якщо BODY якогось
//     ДОНОРСЬКОГО шрифту потрапляє в цю пастку — запис нашого
//     сконвертованого гліфа буде МОВЧКИ ПРОІГНОРОВАНИЙ, і замість нього
//     серіалізуються фантомні сміттєві діти, знищуючи всю текстуру.
//
//     Перевіряє КОЖЕН font/texturePage BODY-чанк в обох файлах:
//     HasChildren? Скільки дітей? Це ПРЯМА перевірка перед будь-яким
//     подальшим кодом запису гліфів.
// EN: CRITICAL check stemming from FourCCPhantomChildScanCommand: "BODY"
//     — the exact FourCC holding font texture pixels — has
//     30722/30730 phantom children across 12/20 instances. This means AT
//     LEAST SOME texture BODY chunks were FULLY (not partially) parsed
//     as a tree of junk children instead of remaining a leaf
//     (Children.Count==0) — because the "full end coverage" check from
//     spec section 6 accidentally succeeded due to the statistical
//     nature of A4R4G4B4 bytes.
//
//     THIS IS CRITICAL FOR GlyphAtlasPatcher: UcfbWriter.WriteChunk only
//     applies replacements if chunk.HasChildren==false. If a DONOR font's
//     BODY falls into this trap — writing our converted glyph would be
//     SILENTLY IGNORED, and junk phantom children would be serialized
//     instead, destroying the entire texture.
//
//     Checks EVERY font/texturePage BODY chunk in both files:
//     HasChildren? How many children? This is a DIRECT check before any
//     further glyph-writing code.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class FontBodyPhantomChildCheckCommand
{
    public static void Run(DiagnosticReport report, UcfbChunk root, string label)
    {
        var fonts = FontChunkLocator.FindAll(root);

        report.Log($"=== [{label}] Перевірка HasChildren КОЖНОГО font BODY-чанку (загроза для запису) ===");
        report.Log($"=== [{label}] Checking HasChildren for EVERY font BODY chunk (write-time threat) ===");

        var affectedCount = 0;
        var totalCount = 0;

        foreach (var font in fonts)
        {
            foreach (var page in font.TexturePages)
            {
                totalCount++;
                var fmt = UcfbReader.FindFirst(page.Chunk, "FMT_");
                var body = fmt is not null ? UcfbReader.FindFirst(fmt, "BODY") : null;

                if (body is null)
                {
                    report.Log($"  {font.BaseName}/{page.Name}: BODY не знайдено!");
                    continue;
                }

                var status = body.HasChildren
                    ? $"!! УРАЖЕНО !! HasChildren=true, дітей={body.Children.Count} — ЗАПИС БУДЕ ПРОІГНОРОВАНО UcfbWriter"
                    : "OK, чистий листок (HasChildren=false)";

                if (body.HasChildren) affectedCount++;

                report.Log($"  {font.BaseName}/{page.Name}: DataSize={body.DataSize}, {status}");
            }
        }

        report.Log();
        report.Log(affectedCount == 0
            ? $"UA: Жоден із {totalCount} font BODY-чанків НЕ уражений — GlyphAtlasPatcher можна писати " +
              "БЕЗ додаткового виправлення UcfbWriter для шрифтів (але сам баг лишається загрозою для " +
              "інших частин коду, що записують BODY/DATA/PVS_ будь-де ще)."
            : $"UA: !! {affectedCount} з {totalCount} font BODY-чанків УРАЖЕНІ. GlyphAtlasPatcher " +
              "НЕ МОЖНА писати, поки UcfbWriter/UcfbReader не виправлені (AlwaysLeafFourCC для BODY " +
              "мінімум), інакше запис у ці конкретні текстури мовчки не спрацює.");
    }
}