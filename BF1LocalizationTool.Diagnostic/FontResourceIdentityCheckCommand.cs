// =============================================================================
// BF1LocalizationTool.Diagnostic — FontResourceIdentityCheckCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Побайтове порівняння ДВОХ шрифтових ресурсів — FBOD і КОЖНОЇ
//     текстурної сторінки. Привід: GlyphDonorAssignmentPreviewCommand
//     показав, що gamefont_tiny і gamefont_super_tiny в BF2 дають
//     АБСОЛЮТНО ІДЕНТИЧНИЙ результат призначення для всіх 66 літер —
//     потрібен факт, а не здогад: чи це справді один і той самий ресурс
//     під двома іменами, чи помилка в коді, яка бере не той шрифт.
//
//     Порівнюються СИРІ байти (RawData FBOD-чанку й RawA4R4G4B4Pixels
//     кожної текстурної сторінки), не похідні обчислення — щоб виключити
//     можливість, що якийсь проміжний крок обчислення випадково дає
//     однаковий результат на різних вхідних даних.
// EN: Byte-for-byte comparison of TWO font resources — the FBOD and
//     EVERY texture page. Reason: GlyphDonorAssignmentPreviewCommand
//     showed that gamefont_tiny and gamefont_super_tiny in BF2 produce
//     an ABSOLUTELY IDENTICAL assignment result for all 66 letters — this
//     needs a fact, not a guess: is it really the same resource under two
//     names, or a code bug picking the wrong font.
//
//     RAW bytes are compared (FBOD chunk RawData and each texture page's
//     RawA4R4G4B4Pixels), not derived computations — to rule out the
//     possibility that some intermediate calculation step coincidentally
//     produces the same result from different underlying data.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class FontResourceIdentityCheckCommand
{
    public static void Run(DiagnosticReport report, UcfbChunk root, string label, string fontBaseNameA, string fontBaseNameB)
    {
        var fonts = FontChunkLocator.FindAll(root);
        var fontA = fonts.FirstOrDefault(f => f.BaseName == fontBaseNameA);
        var fontB = fonts.FirstOrDefault(f => f.BaseName == fontBaseNameB);

        report.Log($"=== [{label}] Побайтове порівняння '{fontBaseNameA}' vs '{fontBaseNameB}' ===");

        if (fontA is null) { report.Log($"UA: '{fontBaseNameA}' не знайдено."); return; }
        if (fontB is null) { report.Log($"UA: '{fontBaseNameB}' не знайдено."); return; }

        var fbodA = UcfbReader.FindFirst(fontA.Chunk, "FBOD");
        var fbodB = UcfbReader.FindFirst(fontB.Chunk, "FBOD");

        if (fbodA is null || fbodB is null)
        {
            report.Log($"UA: FBOD не знайдено в одному з двох ({(fbodA is null ? fontBaseNameA : fontBaseNameB)}).");
        }
        else
        {
            var fbodIdentical = fbodA.RawData.AsSpan().SequenceEqual(fbodB.RawData);
            report.Log($"    FBOD: {fontBaseNameA}={fbodA.RawData.Length} байт, {fontBaseNameB}={fbodB.RawData.Length} байт → " +
                       $"{(fbodIdentical ? "ІДЕНТИЧНІ побайтово" : "РІЗНІ")}");
        }

        report.Log($"    Текстурних сторінок: {fontBaseNameA}={fontA.TexturePages.Count}, {fontBaseNameB}={fontB.TexturePages.Count}");

        var pageCount = Math.Min(fontA.TexturePages.Count, fontB.TexturePages.Count);
        for (var i = 0; i < pageCount; i++)
        {
            var pageA = fontA.TexturePages[i];
            var pageB = fontB.TexturePages[i];

            var pixelsA = FontTexturePixelReader.ReadMip0(pageA.Chunk);
            var pixelsB = FontTexturePixelReader.ReadMip0(pageB.Chunk);

            var sameDimensions = pixelsA.Width == pixelsB.Width && pixelsA.Height == pixelsB.Height;
            var samePixels = sameDimensions && pixelsA.RawA4R4G4B4Pixels.AsSpan().SequenceEqual(pixelsB.RawA4R4G4B4Pixels);

            report.Log($"    Сторінка {i}: {pageA.Name} ({pixelsA.Width}x{pixelsA.Height}) vs {pageB.Name} ({pixelsB.Width}x{pixelsB.Height}) → " +
                       $"{(samePixels ? "ІДЕНТИЧНІ побайтово" : sameDimensions ? "різний вміст, той самий розмір" : "РІЗНИЙ розмір")}");
        }

        if (fontA.TexturePages.Count != fontB.TexturePages.Count)
            report.Log($"    UA: Різна кількість сторінок — порівняно лише перші {pageCount}.");
    }
}
