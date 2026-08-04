// =============================================================================
// BF1LocalizationTool.Diagnostic — BodySizeConsistencyCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перевіряє формулу "BODY.Length == Width×Height×2" (без padding) на
//     КОЖНІЙ текстурній сторінці КОЖНОГО шрифту, в обох іграх — не лише
//     на прикладі з розділу 3 специфікації (gamefont_super_tiny_tex0).
//     GlyphAtlasPatcher рахує офсет запису пікселя як (row*texW + col)*2
//     БЕЗ жодного padding; якби хоч одна сторінка мала інший фактичний
//     розмір BODY (напр. через вирівнювання рядків), цей офсет був би
//     хибним і зіпсував би сусідні рядки.
// EN: Verifies the formula "BODY.Length == Width×Height×2" (no padding)
//     on EVERY texture page of EVERY font, in both games — not just the
//     single example in spec section 3 (gamefont_super_tiny_tex0).
//     GlyphAtlasPatcher computes a pixel's write offset as
//     (row*texW + col)*2 with NO padding; if even one page had a
//     different actual BODY size (e.g. due to row alignment), that
//     offset would be wrong and would corrupt neighboring rows.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;

namespace BF1LocalizationTool.Diagnostic;

public static class BodySizeConsistencyCommand
{
    public static void Run(DiagnosticReport report, UcfbChunk root, string label)
    {
        var fonts = FontChunkLocator.FindAll(root);

        var totalChecked = 0;
        var mismatches = new List<string>();

        foreach (var font in fonts)
        {
            foreach (var page in font.TexturePages)
            {
                totalChecked++;

                var texPixels = FontTexturePixelReader.ReadMip0(page.Chunk);
                var expectedLength = texPixels.Width * texPixels.Height * 2;
                var actualLength = texPixels.RawA4R4G4B4Pixels.Length;

                if (actualLength != expectedLength)
                    mismatches.Add(
                        $"{font.BaseName}/{page.Name}: {texPixels.Width}x{texPixels.Height}, " +
                        $"очікувано {expectedLength} байт, фактично {actualLength} байт " +
                        $"(різниця {actualLength - expectedLength})");
            }
        }

        report.Log($"=== [{label}] Розмір BODY = Width×Height×2 без padding (перевірка перед GlyphAtlasPatcher) ===");
        report.Log($"    Перевірено текстурних сторінок (усі шрифти): {totalChecked}");
        report.Log($"    Розбіжностей розміру: {mismatches.Count}");

        if (mismatches.Count > 0)
        {
            report.Log("    --- Розбіжності ---");
            foreach (var m in mismatches)
                report.Log($"      {m}");
        }

        report.Log(mismatches.Count == 0
            ? "    UA: ПІДТВЕРДЖЕНО — на КОЖНІЙ текстурній сторінці BODY.Length == Width×Height×2, padding немає. " +
              "GlyphAtlasPatcher може рахувати офсет пікселя як (row*texW+col)*2 без додаткової перевірки."
            : "    UA: ЗНАЙДЕНО РОЗБІЖНІСТЬ — формула офсету (row*texW+col)*2 НЕ підходить для перелічених сторінок " +
              "без додаткового з'ясування фактичного вирівнювання рядків.");
    }
}
