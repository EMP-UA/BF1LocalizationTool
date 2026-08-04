// =============================================================================
// BF1LocalizationTool.Diagnostic — FontGlyphCodeRangeCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Дамп СИРОГО списку кодів, визначених у FBOD конкретного шрифту —
//     жодної фільтрації (не "безпечні донори", не конкретний код на
//     вході). Мета — перевірити ФАКТОМ, а не візуальним враженням з
//     розмитого 128x128 PNG: чи `starwars_small` взагалі текстовий шрифт
//     (коди переважно у друкованому ASCII 0x20-0x7E, як gamefont_*), чи
//     це набір іконок/символів (коди розкидані поза цим діапазоном).
//     Якщо це іконки — питання підбору кириличного Windows-шрифту для
//     нього просто відпадає, немає що перекладати.
// EN: Dump of the RAW code list defined in a specific font's FBOD — no
//     filtering (not "safe donors", not a specific code as input). Goal:
//     verify with a FACT, not a visual impression from a blurry 128x128
//     PNG, whether `starwars_small` is a text font at all (codes mostly
//     in printable ASCII 0x20-0x7E, like gamefont_*), or a set of
//     icons/symbols (codes scattered outside that range). If it's icons,
//     the question of picking a Cyrillic Windows font for it is moot —
//     there's nothing to translate.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class FontGlyphCodeRangeCommand
{
    public static void RunAll(DiagnosticReport report, UcfbChunk root, string label)
    {
        var fonts = FontChunkLocator.FindAll(root);
        if (fonts.Count == 0)
        {
            report.Log($"UA: [{label}] Шрифтових ресурсів не знайдено. / EN: No font resources found.");
            return;
        }

        foreach (var font in fonts)
        {
            RunOne(report, font.Chunk, font.BaseName, label);
            report.Log();
        }
    }

    public static void Run(DiagnosticReport report, UcfbChunk root, string label, string fontBaseName)
    {
        var font = FontChunkLocator.FindAll(root).FirstOrDefault(f => f.BaseName == fontBaseName);
        if (font is null) { report.Log($"UA: [{label}] Шрифт '{fontBaseName}' не знайдено. / EN: Font '{fontBaseName}' not found."); return; }

        RunOne(report, font.Chunk, fontBaseName, label);
    }

    private static void RunOne(DiagnosticReport report, UcfbChunk fontChunk, string fontBaseName, string label)
    {
        var fbod = UcfbReader.FindFirst(fontChunk, "FBOD");
        if (fbod is null) { report.Log($"UA: [{label}] FBOD у '{fontBaseName}' не знайдено. / EN: FBOD not found in '{fontBaseName}'."); return; }

        var glyphs = FontGlyphTable.Parse(fbod.RawData);
        var codes = glyphs.Select(g => (int)g.Code).OrderBy(c => c).ToList();

        const int printableAsciiMin = 0x20;
        const int printableAsciiMax = 0x7E;
        var printableAsciiCount = codes.Count(c => c is >= printableAsciiMin and <= printableAsciiMax);
        var latin1SupplementCount = codes.Count(c => c is >= 0x80 and <= 0xFF);
        var otherCount = codes.Count - printableAsciiCount - latin1SupplementCount;

        report.Log($"=== [{label}] {fontBaseName}: сирий список кодів FBOD ({codes.Count} шт.) ===");
        report.Log($"    У друкованому ASCII (0x20-0x7E, звичайні літери/цифри/пунктуація): {printableAsciiCount}");
        report.Log($"    У Latin-1 Supplement (0x80-0xFF, розширена латиниця/спецсимволи): {latin1SupplementCount}");
        report.Log($"    Поза обома діапазонами (нетипово для звичайного тексту): {otherCount}");
        report.Log();

        // UA: Друкуємо весь список компактно, по 16 кодів на рядок —
        //     людині достатньо глянути, чи це суцільний блок (типово для
        //     тексту) чи розкидані острівці (типово для іконок/індексів).
        // EN: Print the full list compactly, 16 codes per row — enough
        //     for a human to see at a glance whether it's one contiguous
        //     block (typical for text) or scattered islands (typical for
        //     icons/indices).
        for (var i = 0; i < codes.Count; i += 16)
        {
            var row = codes.Skip(i).Take(16).Select(c => $"0x{c:X2}");
            report.Log("    " + string.Join(" ", row));
        }

        report.Log();
        report.Log(printableAsciiCount >= codes.Count / 2
            ? "UA: Більшість кодів у звичайному ASCII-діапазоні — схоже на ЗВИЧАЙНИЙ ТЕКСТОВИЙ шрифт."
            : "UA: Більшість кодів ПОЗА звичайним ASCII-діапазоном — схоже на НАБІР ІКОНОК/СИМВОЛІВ, не на текст.");
    }
}
