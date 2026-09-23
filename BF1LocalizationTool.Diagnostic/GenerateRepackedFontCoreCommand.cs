// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateRepackedFontCoreCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: Валідаційний місток перед рендером з TTF. Бере наявний core.lvl,
//     ПЕРЕПАКОВУЄ кожен шрифт у свіжий атлас (ті самі пікселі/метрики/коди,
//     лише нова розкладка й UV — FontRepacker), і записує новий core.lvl
//     у font-output/. Мета — довести В ГРІ, що bin-packer + генерація
//     UV/FBOD + FontResourceBuilder дають ПРАЦЮЮЧИЙ шрифт, ЩЕ ДО того, як
//     додавати свіжий рендер літер зі збільшенням.
//
//     Очікування: у грі текст має виглядати ІДЕНТИЧНО оригіналу (гліфи ті
//     самі, змінилось лише де вони лежать у текстурі). Якщо ідентично —
//     writer-конвеєр повністю робочий на реальному двигуні. Якщо текст
//     побитий/зсунутий — проблема в UV/packing, і це видно до
//     найскладнішої частини.
//
//     Разом із записом робить IN-MEMORY self-check: витягує пікселі
//     КОЖНОГО гліфа з оригіналу й з перепакованого і порівнює байт-у-байт
//     (мають збігтися — копіювання сирих A4R4G4B4 без втрат).
// EN: A validation bridge before TTF rendering. Takes an existing core.lvl,
//     REPACKS each font into a fresh atlas (same pixels/metrics/codes, only
//     a new layout and UVs — FontRepacker), and writes a new core.lvl to
//     font-output/. The goal is to prove IN-GAME that the bin-packer +
//     UV/FBOD generation + FontResourceBuilder produce a WORKING font,
//     BEFORE adding fresh, enlarged letter rendering.
//
//     Expectation: in-game the text must look IDENTICAL to the original
//     (same glyphs, only their location in the texture changed). If
//     identical — the writer pipeline fully works on the real engine. If
//     text is broken/shifted — the issue is in UVs/packing, visible before
//     the hardest part.
//
//     Alongside writing, it does an IN-MEMORY self-check: extracts EACH
//     glyph's pixels from the original and from the repacked font and
//     compares byte-for-byte (must match — lossless raw A4R4G4B4 copy).
// =============================================================================

using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateRepackedFontCoreCommand
{
    public static string? Run(DiagnosticReport report, string coreLvlPath, string outputDir)
    {
        if (!File.Exists(coreLvlPath))
        {
            report.Log($"UA: core.lvl не знайдено: {coreLvlPath} — пропускаю.");
            report.Log($"EN: core.lvl not found: {coreLvlPath} — skipping.");
            return null;
        }

        report.Log($"UA: Читаю {coreLvlPath}...");
        report.Log($"EN: Reading {coreLvlPath}...");
        report.Log("UA: ВАЖЛИВО: перепаковуємо в сторінки ТОГО САМОГО розміру, що оригінал — екранний розмір гліфа залежить від НОРМОВАНОГО UV-прольоту (частки текстури), тому зміна розміру сторінки змінила б розмір тексту. Тут ціль — ІДЕНТИЧНО.");
        report.Log("EN: IMPORTANT: repacking into pages of the SAME size as the original — a glyph's on-screen size depends on the NORMALIZED UV span (fraction of the texture), so changing the page size would change the text size. Here the goal is IDENTICAL.");
        var root = UcfbReader.ReadFile(coreLvlPath);
        var fonts = FontChunkLocator.FindAll(root);

        var allPixelsMatch = true;

        foreach (var font in fonts)
        {
            var original = FontResourceReader.Read(font.Chunk);

            // UA: Розмір нової сторінки = розмір оригінальної (усі сторінки
            //     одного шрифту однакові — перевірено). Так нормований
            //     UV-проліт кожного гліфа зберігається → однаковий екранний
            //     розмір. Кількість сторінок може змінитись (packing), розмір — ні.
            // EN: New page size = the original's page size (all pages of a
            //     font are equal — verified). This preserves each glyph's
            //     normalized UV span → same on-screen size. The page COUNT
            //     may change (packing), the page SIZE does not.
            var pageW = original.Pages[0].Width;
            var pageH = original.Pages[0].Height;
            var repacked = FontRepacker.Repack(original, pageW, pageH);

            var mismatches = CountPixelMismatches(original, repacked);
            if (mismatches != 0) allPixelsMatch = false;

            report.Log(
                $"UA: [{font.BaseName}] сторінок {original.Pages.Count}→{repacked.Pages.Count} ({pageW}×{pageH}), " +
                $"гліфів={repacked.Glyphs.Count} | піксельних розбіжностей={mismatches} (очікується 0)");
            report.Log(
                $"EN: [{font.BaseName}] pages {original.Pages.Count}→{repacked.Pages.Count} ({pageW}×{pageH}), " +
                $"glyphs={repacked.Glyphs.Count} | pixel mismatches={mismatches} (expected 0)");

            // UA: Підмінити вміст font-контейнера на перебудований (in-place,
            //     List мутабельний навіть під init-властивістю).
            // EN: Replace the font container's contents with the rebuilt one
            //     (in-place, the List is mutable even under an init property).
            var rebuilt = FontResourceBuilder.Build(repacked);
            font.Chunk.Children.Clear();
            font.Chunk.Children.AddRange(rebuilt.Children);
        }

        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "core.lvl");
        UcfbWriter.WriteFile(outputPath, root);

        var origSize = new FileInfo(coreLvlPath).Length;
        var newSize = new FileInfo(outputPath).Length;

        report.Log();
        report.Log($"UA: Записано перепакований core.lvl: {outputPath} (оригінал={origSize}Б, новий={newSize}Б)");
        report.Log($"EN: Wrote repacked core.lvl: {outputPath} (original={origSize}B, new={newSize}B)");
        report.Log(allPixelsMatch
            ? "UA: Self-check: усі пікселі гліфів збіглися байт-у-байт. Тепер перевірка В ГРІ: текст має виглядати ІДЕНТИЧНО."
            : "UA: Self-check: Є піксельні розбіжності — НЕ тестувати в грі, спершу розібратись.");
        report.Log(allPixelsMatch
            ? "EN: Self-check: all glyph pixels matched byte-for-byte. Now the IN-GAME check: text must look IDENTICAL."
            : "EN: Self-check: pixel mismatches exist — do NOT test in-game, investigate first.");

        return allPixelsMatch ? outputPath : null;
    }

    // -------------------------------------------------------------------------
    // UA: Порівнює пікселі КОЖНОГО гліфа: витягує прямокутник з оригіналу
    //     (за його UV+сторінкою) і з перепакованого, порівнює байти.
    //     Повертає кількість гліфів, чиї пікселі НЕ збіглися.
    // EN: Compares EACH glyph's pixels: extracts the rectangle from the
    //     original (by its UV+page) and from the repacked, compares bytes.
    //     Returns the number of glyphs whose pixels did NOT match.
    // -------------------------------------------------------------------------
    private static int CountPixelMismatches(FontResourceData a, FontResourceData b)
    {
        var mismatches = 0;
        for (var i = 0; i < a.Glyphs.Count; i++)
        {
            var pa = ExtractGlyphPixels(a, i);
            var pb = ExtractGlyphPixels(b, i);
            if (!pa.AsSpan().SequenceEqual(pb))
                mismatches++;
        }
        return mismatches;
    }

    private static byte[] ExtractGlyphPixels(FontResourceData font, int glyphIndex)
    {
        var g = font.Glyphs[glyphIndex];
        var page = font.Pages[g.PageIndex];
        // UA: FLOOR лівого/верхнього краю + округлення розмаху — та сама
        //     half-texel-конвенція, що й FontRepacker (тексель n має центр
        //     n+0.5, тож floor(u·W)=перший тексель) — узгоджена з тим, як
        //     FontRepacker обчислює (pl.X+0.5); ClampRound тут давав би
        //     хибні "розбіжності" в self-check.
        // EN: FLOOR of the left/top edge + rounded span — the same half-texel
        //     convention as FontRepacker (texel n has center n+0.5, so
        //     floor(u·W)=first texel) — matching how FontRepacker computes
        //     (pl.X+0.5); ClampRound here would report false self-check
        //     "mismatches".
        var minUpx = Math.Min(g.U0, g.U1) * page.Width;
        var maxUpx = Math.Max(g.U0, g.U1) * page.Width;
        var minVpx = Math.Min(g.V0, g.V1) * page.Height;
        var maxVpx = Math.Max(g.V0, g.V1) * page.Height;
        var x0 = FloorClamp(minUpx, page.Width);
        var y0 = FloorClamp(minVpx, page.Height);
        var w = (int)Math.Round(maxUpx - minUpx, MidpointRounding.AwayFromZero);
        var h = (int)Math.Round(maxVpx - minVpx, MidpointRounding.AwayFromZero);
        if (x0 + w > page.Width) w = page.Width - x0;
        if (y0 + h > page.Height) h = page.Height - y0;

        var buf = new byte[w * h * 2];
        for (var row = 0; row < h; row++)
        {
            var srcOffset = ((y0 + row) * page.Width + x0) * 2;
            Buffer.BlockCopy(page.BodyPixels, srcOffset, buf, row * w * 2, w * 2);
        }
        return buf;
    }

    private static int FloorClamp(float v, int max)
    {
        var r = (int)Math.Floor(v);
        return r < 0 ? 0 : r > max ? max : r;
    }
}
