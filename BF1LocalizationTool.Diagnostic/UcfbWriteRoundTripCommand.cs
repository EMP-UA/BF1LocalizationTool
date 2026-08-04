// =============================================================================
// BF1LocalizationTool.Diagnostic — UcfbWriteRoundTripCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перевірка перед GlyphAtlasPatcher: чи UcfbWriter коректно
//     записує заміну для ОДНОГО листового чанку (BODY текстурної
//     сторінки шрифту) на РЕАЛЬНОМУ core.lvl, і чи файл після цього
//     лишається коректним (round-trip save → read → save, звірка з
//     контрольним патерном і з байт-в-байт незмінністю СУСІДНІХ чанків).
//     НЕ модифікує оригінальний файл на диску — працює лише в пам'яті
//     й/або записує в окремий тимчасовий файл, який видаляється.
//
//     Тестовий патерн: НЕ реальний гліф (ще не готовий GlyphAtlasPatcher),
//     а тривіально перевірюваний — усі пікселі BODY замінюються на
//     один і той самий контрольний 16-бітний код (0x0FFF — RGB=білий,
//     Alpha=0, узгоджено з підтвердженою схемою запису). Мета — не
//     перевірити растеризацію, а лише механіку UcfbWriter/UcfbReader.
// EN: Check before GlyphAtlasPatcher: does UcfbWriter correctly
//     write a replacement for a SINGLE leaf chunk (a font texture page's
//     BODY) on a REAL core.lvl, and does the file remain valid afterward
//     (round-trip save → read → save, verified against a control pattern
//     and byte-for-byte unchanged NEIGHBORING chunks). Does NOT modify
//     the original file on disk — works in memory and/or writes to a
//     separate temp file that gets deleted.
//
//     Test pattern: NOT a real glyph (GlyphAtlasPatcher isn't ready yet),
//     but a trivially verifiable one — every BODY pixel is replaced with
//     the same control 16-bit value (0x0FFF — RGB=white, Alpha=0,
//     consistent with the confirmed write scheme). The goal is to test
//     UcfbWriter/UcfbReader mechanics, not rasterization.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class UcfbWriteRoundTripCommand
{
    public static void Run(DiagnosticReport report, string lvlFilePath, string fontBaseName, string texturePageName)
    {
        var originalBytes = File.ReadAllBytes(lvlFilePath);
        var root = UcfbReader.ReadFile(originalBytes);

        var font = FontChunkLocator.FindAll(root).FirstOrDefault(f => f.BaseName == fontBaseName);
        if (font is null) { report.Log($"UA: Шрифт '{fontBaseName}' не знайдено. / EN: Font '{fontBaseName}' not found."); return; }

        var page = font.TexturePages.FirstOrDefault(p => p.Name == texturePageName);
        if (page is null) { report.Log($"UA: Сторінка '{texturePageName}' не знайдена. / EN: Page '{texturePageName}' not found."); return; }

        var texPixels = FontTexturePixelReader.ReadMip0(page.Chunk);
        var bodyChunk = texPixels.BodyChunk;
        var originalBodyBytes = bodyChunk.RawData;

        report.Log($"=== Round-trip перевірка запису: {fontBaseName}/{texturePageName} ===");
        report.Log($"=== Write round-trip check: {fontBaseName}/{texturePageName} ===");
        report.Log($"    BODY.FileDataOffset={bodyChunk.FileDataOffset}, розмір={originalBodyBytes.Length} байт");

        // UA: Контрольний патерн — весь BODY заповнюється значенням
        //     0x0FFF (little-endian: 0xFF, 0x0F) — легко впізнати в
        //     hex-дампі, узгоджено з підтвердженою схемою RGB=білий.
        // EN: Control pattern — the entire BODY is filled with 0x0FFF
        //     (little-endian: 0xFF, 0x0F) — easy to spot in a hex dump,
        //     consistent with the confirmed RGB=white scheme.
        var testPattern = new byte[originalBodyBytes.Length];
        for (var i = 0; i < testPattern.Length; i += 2)
        {
            testPattern[i] = 0xFF;
            testPattern[i + 1] = 0x0F;
        }

        var replacements = new Dictionary<long, byte[]> { [bodyChunk.FileDataOffset] = testPattern };
        var writtenBytes = UcfbWriter.WriteFile(root, replacements);

        report.Log($"    Розмір файлу: оригінал={originalBytes.Length}, після запису={writtenBytes.Length}");

        // UA: Перечитуємо ЗАПИСАНІ байти заново — незалежна перевірка,
        //     а не довіра до того, що ми щойно самі згенерували.
        // EN: Re-read the WRITTEN bytes from scratch — an independent
        //     check, not trusting what we just generated ourselves.
        UcfbChunk rereadRoot;
        try
        {
            rereadRoot = UcfbReader.ReadFile(writtenBytes);
        }
        catch (Exception ex)
        {
            report.Log($"    !! КРИТИЧНО: файл після запису НЕ парситься: {ex.Message}");
            report.Log($"    !! CRITICAL: file does not parse after writing: {ex.Message}");
            return;
        }

        var rereadFont = FontChunkLocator.FindAll(rereadRoot).FirstOrDefault(f => f.BaseName == fontBaseName);
        var rereadPage = rereadFont?.TexturePages.FirstOrDefault(p => p.Name == texturePageName);
        if (rereadPage is null)
        {
            report.Log("    !! КРИТИЧНО: після перезапису шрифт/сторінка не знайдені при повторному читанні.");
            return;
        }

        var rereadTexPixels = FontTexturePixelReader.ReadMip0(rereadPage.Chunk);
        var patternMatches = rereadTexPixels.RawA4R4G4B4Pixels.SequenceEqual(testPattern);

        report.Log($"    Контрольний патерн збігається після перезапису: {(patternMatches ? "ТАК" : "НІ — ПОМИЛКА")}");

        // UA: Перевіряємо, що ІНШІ сторінки цього ж шрифту лишились
        //     байт-в-байт незмінними (не зачеплені записом у BODY цієї
        //     сторінки).
        // EN: Verify that OTHER pages of the same font remained
        //     byte-for-byte unchanged (not affected by writing to this
        //     page's BODY).
        var neighborMismatches = 0;
        foreach (var neighborPage in font.TexturePages.Where(p => p.Name != texturePageName))
        {
            var originalNeighborPixels = FontTexturePixelReader.ReadMip0(neighborPage.Chunk);
            var rereadNeighbor = rereadFont!.TexturePages.First(p => p.Name == neighborPage.Name);
            var rereadNeighborPixels = FontTexturePixelReader.ReadMip0(rereadNeighbor.Chunk);

            var neighborMatches = originalNeighborPixels.RawA4R4G4B4Pixels.SequenceEqual(rereadNeighborPixels.RawA4R4G4B4Pixels);
            if (!neighborMatches) neighborMismatches++;

            report.Log($"    Сусідня сторінка {neighborPage.Name} незмінна: {(neighborMatches ? "ТАК" : "НІ — ПОМИЛКА")}");
        }

        // UA: Перевіряємо FBOD цього ж шрифту — теж має лишитись
        //     незмінним (ми міняли лише пікселі, не таблицю гліфів).
        // EN: Verify FBOD of the same font — should also remain
        //     unchanged (we only modified pixels, not the glyph table).
        var originalFbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
        var rereadFbod = UcfbReader.FindFirst(rereadFont!.Chunk, "FBOD");
        var fbodMatches = originalFbod is not null && rereadFbod is not null &&
                           originalFbod.RawData.SequenceEqual(rereadFbod.RawData);
        report.Log($"    FBOD цього шрифту незмінний: {(fbodMatches ? "ТАК" : "НІ — ПОМИЛКА")}");

        // UA: Перевіряємо ще один цикл save → read → save — щоб
        //     переконатись у стабільності (не лише "один раз спрацювало").
        // EN: Verify one more save → read → save cycle — to confirm
        //     stability (not just "worked once").
        var secondWriteBytes = UcfbWriter.WriteFile(rereadRoot, null);
        var stableAfterSecondSave = secondWriteBytes.SequenceEqual(writtenBytes);
        report.Log($"    Стабільність повторного save (без нових замін): {(stableAfterSecondSave ? "ТАК" : "НІ — ПОМИЛКА")}");

        var allOk = patternMatches && neighborMismatches == 0 && fbodMatches && stableAfterSecondSave;
        report.Log();
        report.Log(allOk
            ? "UA: ПІДТВЕРДЖЕНО — UcfbWriter коректно записує заміну BODY, не зачіпаючи сусідів і FBOD, файл лишається стабільним."
            : "UA: ЗНАЙДЕНО ПРОБЛЕМИ — див. позначки вище. GlyphAtlasPatcher НЕ можна писати, поки це не виправлено.");
        report.Log();
        report.Log("UA: Оригінальний файл на диску НЕ змінено — вся перевірка відбувалась у пам'яті.");
        report.Log("EN: The original file on disk was NOT modified — the whole check happened in memory.");
    }
}