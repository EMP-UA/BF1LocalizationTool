// =============================================================================
// BF1LocalizationTool.Diagnostic — GlyphPageAssignmentCheckCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: КРИТИЧНА ПЕРЕВІРКА, виявлена аналізом PerFontSafeDonorCommand:
//     FBOD-запис (24 байти) НЕ МІСТИТЬ поля "номер сторінки". Усі
//     попередні команди (GlyphAtlasStyleAnalyzer, GlyphRectOverlapCommand,
//     GlyphOverlapRiskCommand, PerFontSafeDonorCommand) застосовували
//     ВЕСЬ спільний список гліфів ДО КОЖНОЇ сторінки шрифту з кількома
//     _texN — це НЕПЕРЕВІРЕНЕ припущення, що кожен гліф фізично присутній
//     на КОЖНІЙ сторінці. Підозріло екстремальний результат
//     (gamefont_medium: 0 з 90 безпечних) вказує, що це припущення,
//     ймовірно, ХИБНЕ — швидше кожен гліф належить лише ОДНІЙ конкретній
//     сторінці, і застосування його UV до "чужої" сторінки читає
//     випадкові, не пов'язані пікселі.
//
//     Метод перевірки: для symmetричного/асиметричного відомого символу
//     (напр. 'L'=0x4C чи 'M'=0x4D) друкує ASCII-рендер його UV-
//     прямокутника НА КОЖНІЙ сторінці шрифту окремо. Якщо форма
//     впізнавана лише на ОДНІЙ сторінці (а на решті — шум/порожнеча/
//     не пов'язана форма) — гіпотеза підтверджена, і весь попередній
//     аналіз перетинів для multi-page шрифтів потребує повторення з
//     урахуванням фактичної належності гліфа до сторінки.
// EN: CRITICAL CHECK surfaced by PerFontSafeDonorCommand's analysis: an
//     FBOD record (24 bytes) contains NO "page number" field. All
//     previous commands (GlyphAtlasStyleAnalyzer, GlyphRectOverlapCommand,
//     GlyphOverlapRiskCommand, PerFontSafeDonorCommand) applied the
//     ENTIRE shared glyph list TO EVERY page of a multi-_texN font —
//     an UNVERIFIED assumption that every glyph is physically present
//     on EVERY page. A suspiciously extreme result (gamefont_medium: 0
//     of 90 safe) suggests this assumption is likely FALSE — more
//     plausibly each glyph belongs to only ONE specific page, and
//     applying its UV to a "foreign" page reads random, unrelated pixels.
//
//     Verification method: for a known symmetric/asymmetric character
//     (e.g. 'L'=0x4C or 'M'=0x4D), prints an ASCII render of its UV
//     rectangle on EVERY page of the font separately. If the shape is
//     recognizable on only ONE page (with noise/emptiness/unrelated
//     shapes on the rest) — the hypothesis is confirmed, and all
//     previous overlap analysis for multi-page fonts needs to be redone
//     accounting for actual glyph-to-page ownership.
// =============================================================================

using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GlyphPageAssignmentCheckCommand
{
    public static void Run(DiagnosticReport report, string lvlFilePath, string fontBaseName, ushort code)
    {
        var root = UcfbReader.ReadFile(lvlFilePath);
        var font = FontChunkLocator.FindAll(root).FirstOrDefault(f => f.BaseName == fontBaseName);
        if (font is null)
        {
            report.Log($"UA: Шрифт '{fontBaseName}' не знайдено.");
            return;
        }

        var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
        if (fbod is null)
        {
            report.Log("UA: FBOD не знайдено.");
            return;
        }

        var glyphs = FontGlyphTable.Parse(fbod.RawData);
        var glyph = FontGlyphTable.FindByCode(glyphs, code);
        if (glyph is null)
        {
            report.Log($"UA: Код 0x{code:X2} відсутній у таблиці.");
            return;
        }

        report.Log($"=== {fontBaseName}, code=0x{code:X2} ('{(char)code}') — рендер на КОЖНІЙ сторінці шрифту ===");
        report.Log($"    Сторінок у цього шрифту: {font.TexturePages.Count}");
        report.Log();

        foreach (var page in font.TexturePages)
        {
            var texPixels = FontTexturePixelReader.ReadMip0(page.Chunk);

            var x0 = (int)Math.Round(glyph.U0 * texPixels.Width);
            var x1 = (int)Math.Round(glyph.U1 * texPixels.Width);
            var y0 = (int)Math.Round(glyph.V0 * texPixels.Height);
            var y1 = (int)Math.Round(glyph.V1 * texPixels.Height);

            var minX = Math.Min(x0, x1);
            var maxX = Math.Max(x0, x1);
            var minY = Math.Min(y0, y1);
            var maxY = Math.Max(y0, y1);

            var nonZeroAlphaCount = 0;
            var totalCount = 0;

            report.Log($"  --- {page.Name} (texture {texPixels.Width}x{texPixels.Height}) ---");
            for (var y = minY; y < maxY; y++)
            {
                var row = "";
                for (var x = minX; x < maxX; x++)
                {
                    var pixelIndex = y * texPixels.Width + x;
                    var (a, _, _, _) = FontTexturePixelReader.DecodePixel(texPixels.RawA4R4G4B4Pixels, pixelIndex);
                    totalCount++;
                    if (a > 0) nonZeroAlphaCount++;
                    row += a switch { >= 192 => "#", >= 64 => "+", > 0 => ".", _ => " " };
                }
                report.Log($"    [{row}]");
            }
            report.Log($"    (непрозорих пікселів: {nonZeroAlphaCount}/{totalCount})");
            report.Log();
        }
    }
}