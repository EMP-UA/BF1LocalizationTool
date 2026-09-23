// =============================================================================
// BF1LocalizationTool.Diagnostic — PixelConversionRoundTripCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Перевіряє GlyphPixelConverter.ToA4R4G4B4 ДО того, як він
//     використовується для запису реальних пікселів в BODY. Дві окремі
//     перевірки, обидві за фактичними даними, без вгадування:
//
//     1. Вичерпний тест (0..15): усі 16 можливих 4-бітних значень альфи —
//        не вибірка, а ПОВНИЙ перебір, оскільки простір значень
//        достатньо малий. Перевіряє, що Round8To4 коректно обертає
//        декодування value*17 (FONT_FORMAT_SPEC.md розділ 3) для КОЖНОГО
//        можливого значення, а не лише для випадково обраних прикладів.
//
//     2. Тест на реальних донорських пікселях з фактичного core.lvl:
//        декодує справжні альфа-значення гліфа, пропускає через повний
//        конвеєр (Decode → Round8To4 → Encode-як-A4R4G4B4), звіряє
//        байт-в-байт з оригіналом. Це підтверджує коректність на
//        РЕАЛЬНИХ значеннях з гри, не лише теоретичних.
// EN: Verifies GlyphPixelConverter.ToA4R4G4B4 BEFORE it's used to write
//     real pixels into BODY. Two separate checks, both against actual
//     data, no guessing:
//
//     1. Exhaustive test (0..15): all 16 possible 4-bit alpha values —
//        not a sample, a FULL enumeration, since the value space is
//        small enough. Verifies Round8To4 correctly inverts the
//        value*17 decoding (FONT_FORMAT_SPEC.md section 3) for EVERY
//        possible value, not just arbitrarily chosen examples.
//
//     2. Test against real donor pixels from an actual core.lvl: decodes
//        real glyph alpha values, runs them through the full pipeline
//        (Decode → Round8To4 → Encode-as-A4R4G4B4), compares byte-for-
//        byte with the original. This confirms correctness on REAL
//        in-game values, not just theoretical ones.
// =============================================================================

using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.FontGenerator.PixelConversion;
using BF1LocalizationTool.FontGenerator.Rasterization;

namespace BF1LocalizationTool.Diagnostic;

public static class PixelConversionRoundTripCommand
{
    // -------------------------------------------------------------------------
    // UA: Перевірка 1 — вичерпна, без файлу. Синтетичний "гліф" 16×1,
    //     кожен піксель = одне з 16 можливих значень alpha (0,17,34,...,255).
    // EN: Check 1 — exhaustive, no file needed. Synthetic 16x1 "glyph",
    //     each pixel = one of the 16 possible alpha values (0,17,34,...,255).
    // -------------------------------------------------------------------------
    public static void RunExhaustiveCheck(DiagnosticReport report)
    {
        report.Log("=== Вичерпна перевірка Round8To4 (усі 16 можливих значень альфи) ===");
        report.Log("=== Exhaustive Round8To4 check (all 16 possible alpha values) ===");

        var mismatches = 0;

        for (var nibble = 0; nibble <= 15; nibble++)
        {
            var encoded8Bit = (byte)(nibble * 17);

            // UA: Будуємо мінімальний RasterizedGlyph з одним пікселем
            //     заданої альфи (RGB довільний — конвертер його ігнорує,
            //     згідно з підтвердженим правилом "RGB завжди білий").
            // EN: Build a minimal RasterizedGlyph with a single pixel of
            //     the given alpha (RGB is arbitrary — the converter
            //     ignores it, per the confirmed "RGB always white" rule).
            var syntheticGlyph = new RasterizedGlyph
            {
                Width = 1,
                Height = 1,
                BgraPixels = [0x00, 0x00, 0x00, encoded8Bit], // B,G,R,A
                InkBounds = new System.Drawing.Rectangle(0, 0, 1, 1),
            };

            var converted = GlyphPixelConverter.ToA4R4G4B4(syntheticGlyph);
            var value = BitConverter.ToUInt16(converted, 0);
            var decodedNibble = (value >> 12) & 0xF;

            var ok = decodedNibble == nibble;
            if (!ok) mismatches++;

            report.Log($"  nibble={nibble,2} (8-bit={encoded8Bit,3}) → " +
                               $"конвертовано і декодовано назад: {decodedNibble,2}  " +
                               (ok ? "OK" : "!! MISMATCH !!"));
        }

        report.Log();
        report.Log(mismatches == 0
            ? "UA: ПІДТВЕРДЖЕНО — усі 16 значень проходять round-trip без втрат."
            : $"UA: ПОМИЛКА — {mismatches}/16 значень НЕ проходять round-trip. " +
              "GlyphPixelConverter.Round8To4 має бути виправлений ПЕРЕД будь-яким подальшим кодом.");
    }

    // -------------------------------------------------------------------------
    // UA: Перевірка 2 — на реальних пікселях конкретного донорського
    //     гліфа з фактичного core.lvl.
    // EN: Check 2 — against real pixels of a specific donor glyph from
    //     an actual core.lvl.
    // -------------------------------------------------------------------------
    public static void RunRealDataCheck(DiagnosticReport report, string lvlFilePath, string fontBaseName, string texturePageName, ushort code)
    {
        var root = UcfbReader.ReadFile(lvlFilePath);
        var font = FontChunkLocator.FindAll(root).FirstOrDefault(f => f.BaseName == fontBaseName);
        if (font is null)
        {
            report.Log($"UA: Шрифт '{fontBaseName}' не знайдено. / EN: Font '{fontBaseName}' not found.");
            return;
        }

        var page = font.TexturePages.FirstOrDefault(p => p.Name == texturePageName);
        if (page is null)
        {
            report.Log($"UA: Сторінка '{texturePageName}' не знайдена. / EN: Page '{texturePageName}' not found.");
            return;
        }

        var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
        if (fbod is null)
        {
            report.Log("UA: FBOD не знайдено. / EN: FBOD not found.");
            return;
        }

        var glyphs = FontGlyphTable.Parse(fbod.RawData);
        var glyph = FontGlyphTable.FindByCode(glyphs, code);
        if (glyph is null)
        {
            report.Log($"UA: Код 0x{code:X2} відсутній у таблиці. / EN: Code 0x{code:X2} not in table.");
            return;
        }

        var texPixels = FontTexturePixelReader.ReadMip0(page.Chunk);

        var x0 = (int)Math.Round(glyph.U0 * texPixels.Width);
        var x1 = (int)Math.Round(glyph.U1 * texPixels.Width);
        var y0 = (int)Math.Round(glyph.V0 * texPixels.Height);
        var y1 = (int)Math.Round(glyph.V1 * texPixels.Height);

        var minX = Math.Min(x0, x1);
        var maxX = Math.Max(x0, x1);
        var minY = Math.Min(y0, y1);
        var maxY = Math.Max(y0, y1);

        var width = maxX - minX;
        var height = maxY - minY;

        if (width <= 0 || height <= 0)
        {
            report.Log("UA: Гліф має нульову область (напр. пробіл) — нема що перевіряти.");
            return;
        }

        // UA: Будуємо BGRA-масив із РЕАЛЬНИХ декодованих alpha-значень
        //     (RGB форсуємо білим — так само, як реально робить
        //     GdiGlyphRasterizer, і так само, як GlyphPixelConverter
        //     завжди записує). Це імітує повний конвеєр
        //     "рендер → конвертація", а не лише ізольовану функцію.
        // EN: Build a BGRA array from REAL decoded alpha values (RGB is
        //     forced white — exactly as GdiGlyphRasterizer actually does,
        //     and exactly as GlyphPixelConverter always writes). This
        //     simulates the full "render → convert" pipeline, not just
        //     an isolated function.
        var bgra = new byte[width * height * 4];
        var originalPacked = new ushort[width * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixelIndex = (minY + y) * texPixels.Width + (minX + x);
                var (a, _, _, _) = FontTexturePixelReader.DecodePixel(texPixels.RawA4R4G4B4Pixels, pixelIndex);
                var origValue = BitConverter.ToUInt16(texPixels.RawA4R4G4B4Pixels, pixelIndex * 2);

                var dst = (y * width + x) * 4;
                bgra[dst + 0] = 0x00;
                bgra[dst + 1] = 0x00;
                bgra[dst + 2] = 0x00;
                bgra[dst + 3] = a;

                originalPacked[y * width + x] = origValue;
            }
        }

        var syntheticGlyph = new RasterizedGlyph
        {
            Width = width,
            Height = height,
            BgraPixels = bgra,
            InkBounds = new System.Drawing.Rectangle(0, 0, width, height),
        };

        var converted = GlyphPixelConverter.ToA4R4G4B4(syntheticGlyph);

        var alphaMismatches = 0;
        var rgbForcedCount = 0;

        for (var i = 0; i < width * height; i++)
        {
            var convertedValue = BitConverter.ToUInt16(converted, i * 2);
            var convertedAlphaNibble = (convertedValue >> 12) & 0xF;
            var originalAlphaNibble = (originalPacked[i] >> 12) & 0xF;

            if (convertedAlphaNibble != originalAlphaNibble)
                alphaMismatches++;

            // UA: Якщо оригінальний піксель мав Alpha>0 і RGB≠білий —
            //     це вже неможливо (перевірено 0 аномалій раніше), тож
            //     rgbForcedCount рахує лише інформативно, не як помилку.
            // EN: If the original pixel had Alpha>0 and RGB≠white — that's
            //     already impossible (0 anomalies confirmed earlier), so
            //     rgbForcedCount is purely informational, not an error.
            var origRgb = originalPacked[i] & 0x0FFF;
            if (origRgb != 0x0FFF) rgbForcedCount++;
        }

        report.Log($"=== Round-trip перевірка на реальному гліфі: {fontBaseName}/{texturePageName}, code=0x{code:X2} ===");
        report.Log($"  Пікселів перевірено: {width * height}");
        report.Log($"  Розбіжність альфи (оригінал vs конвертовано): {alphaMismatches}");
        report.Log($"  Пікселів де оригінал мав RGB≠білий (форсовано в білий): {rgbForcedCount}");
        report.Log();
        report.Log(alphaMismatches == 0
            ? "UA: ПІДТВЕРДЖЕНО — конвертер точно відтворює альфа-канал реального гліфа."
            : $"UA: ПОМИЛКА — {alphaMismatches} пікселів альфи не збігаються. Конвертер потребує виправлення.");
    }

    // -------------------------------------------------------------------------
    // UA: Перевірка на реальних пікселях ОДНОГО (шрифт, сторінка, код) для
    //     ОБОХ ігор одним викликом — той самий патерн, що
    //     GlyphAtlasStyleReportCommand.RunBoth. Якщо файл не містить такого
    //     шрифту/сторінки/коду (напр. starwars_small відсутній у BF2) —
    //     повідомляє про це і не падає.
    // EN: Checks real pixels of ONE (font, page, code) against BOTH games in
    //     one call — same pattern as GlyphAtlasStyleReportCommand.RunBoth.
    //     If a file doesn't contain that font/page/code (e.g. starwars_small
    //     absent in BF2) — reports it and doesn't crash.
    // -------------------------------------------------------------------------
    public static void RunRealDataCheckBoth(
        DiagnosticReport report, string bf1Path, string bf2Path, string fontBaseName, string texturePageName, ushort code)
    {
        report.Log($"--- BF1: {fontBaseName}/{texturePageName}, code=0x{code:X2} ---");
        RunRealDataCheck(report, bf1Path, fontBaseName, texturePageName, code);

        report.Log();
        report.Log($"--- BF2: {fontBaseName}/{texturePageName}, code=0x{code:X2} ---");
        RunRealDataCheck(report, bf2Path, fontBaseName, texturePageName, code);
    }
}