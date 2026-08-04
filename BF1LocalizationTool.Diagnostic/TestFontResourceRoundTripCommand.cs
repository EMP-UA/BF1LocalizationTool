// =============================================================================
// BF1LocalizationTool.Diagnostic — TestFontResourceRoundTripCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Round-trip перевірка FontResourceReader + FontResourceBuilder: перш
//     ніж будувати складну логіку (свіжий атлас, bin-packing, рендер),
//     спершу доводимо, що reader+builder взагалі вміють ВІДТВОРИТИ
//     наявний font-чанк БАЙТ-У-БАЙТ.
//
//     Для КОЖНОГО шрифту в core.lvl:
//       1. Серіалізуємо оригінальний font-контейнер (UcfbWriter).
//       2. Read → FontResourceData → Build → знову UcfbWriter.
//       3. Порівнюємо байти. Мають бути ІДЕНТИЧНІ (0 розбіжностей).
//     Якщо ідентично — reader+builder точно відтворюють формат, і на цій
//     основі можна безпечно будувати генерацію свіжого атласу. Якщо ні —
//     звіт показує ПЕРШЕ зміщення розбіжності, щоб одразу бачити, яке поле
//     ще не зрозуміле (а не гадати).
// EN: Round-trip validation of FontResourceReader + FontResourceBuilder:
//     before building complex logic (fresh atlas, bin-packing, rendering),
//     first prove reader+builder can REPRODUCE an existing font chunk
//     BYTE-FOR-BYTE.
//
//     For EACH font in core.lvl:
//       1. Serialize the original font container (UcfbWriter).
//       2. Read → FontResourceData → Build → UcfbWriter again.
//       3. Compare bytes. Must be IDENTICAL (0 differences).
//     If identical — reader+builder reproduce the format exactly, and the
//     fresh-atlas generation can be safely built on top. If not — the
//     report shows the FIRST differing offset, so the not-yet-understood
//     field is visible immediately (no guessing).
// =============================================================================

using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class TestFontResourceRoundTripCommand
{
    public static void Run(DiagnosticReport report, string coreLvlPath)
    {
        if (!File.Exists(coreLvlPath))
        {
            report.Log($"UA: core.lvl не знайдено: {coreLvlPath} — пропускаю.");
            report.Log($"EN: core.lvl not found: {coreLvlPath} — skipping.");
            return;
        }

        report.Log($"UA: Читаю {coreLvlPath}...");
        report.Log($"EN: Reading {coreLvlPath}...");
        var root = UcfbReader.ReadFile(coreLvlPath);
        var fonts = FontChunkLocator.FindAll(root);
        report.Log($"UA: Знайдено шрифтів: {fonts.Count} ({string.Join(", ", fonts.Select(f => f.BaseName))})");
        report.Log($"EN: Fonts found: {fonts.Count} ({string.Join(", ", fonts.Select(f => f.BaseName))})");
        report.Log();

        var allIdentical = true;

        foreach (var font in fonts)
        {
            var originalBytes = UcfbWriter.WriteFile(font.Chunk);

            FontResourceData data;
            byte[] rebuiltBytes;
            try
            {
                data = FontResourceReader.Read(font.Chunk);
                var rebuilt = FontResourceBuilder.Build(data);
                rebuiltBytes = UcfbWriter.WriteFile(rebuilt);
            }
            catch (Exception ex)
            {
                allIdentical = false;
                report.Log($"UA: [{font.BaseName}] ВИНЯТОК під час read/build: {ex.Message}");
                report.Log($"EN: [{font.BaseName}] EXCEPTION during read/build: {ex.Message}");
                continue;
            }

            var identical = originalBytes.AsSpan().SequenceEqual(rebuiltBytes);
            var firstDiff = identical ? -1 : FirstDifference(originalBytes, rebuiltBytes);

            report.Log(
                $"UA: [{font.BaseName}] сторінок={data.Pages.Count} гліфів={data.Glyphs.Count} " +
                $"висота={data.FontHeightPx}px | оригінал={originalBytes.Length}Б перебудовано={rebuiltBytes.Length}Б | " +
                $"ІДЕНТИЧНО={identical}" + (identical ? "" : $" (перша розбіжність на offset {firstDiff})"));
            report.Log(
                $"EN: [{font.BaseName}] pages={data.Pages.Count} glyphs={data.Glyphs.Count} " +
                $"height={data.FontHeightPx}px | original={originalBytes.Length}B rebuilt={rebuiltBytes.Length}B | " +
                $"IDENTICAL={identical}" + (identical ? "" : $" (first diff at offset {firstDiff})"));

            if (!identical)
            {
                allIdentical = false;
                DumpContext(report, originalBytes, rebuiltBytes, firstDiff);
            }
        }

        report.Log();
        report.Log(allIdentical
            ? "UA: РЕЗУЛЬТАТ: усі шрифти round-trip ІДЕНТИЧНІ — reader+builder відтворюють формат точно. Можна будувати генерацію свіжого атласу."
            : "UA: РЕЗУЛЬТАТ: Є розбіжності — див. offset'и вище. НЕ будувати генерацію, доки не з'ясовано поле.");
        report.Log(allIdentical
            ? "EN: RESULT: all fonts round-trip IDENTICAL — reader+builder reproduce the format exactly. Fresh-atlas generation can be built."
            : "EN: RESULT: differences exist — see offsets above. Do NOT build generation until the field is understood.");
    }

    private static int FirstDifference(byte[] a, byte[] b)
    {
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++)
            if (a[i] != b[i]) return i;
        return n; // UA: розбіжність у довжині / EN: length differs
    }

    private static void DumpContext(DiagnosticReport report, byte[] a, byte[] b, int at)
    {
        var start = Math.Max(0, at - 8);
        var len = Math.Min(24, Math.Min(a.Length, b.Length) - start);
        if (len <= 0) return;
        report.Log($"UA/EN: orig[{start}..]={Convert.ToHexString(a, start, len)}");
        report.Log($"UA/EN: new [{start}..]={Convert.ToHexString(b, start, len)}");
    }
}
