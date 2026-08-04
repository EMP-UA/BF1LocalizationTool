// =============================================================================
// BF1LocalizationTool.FontGenerator — PixelConversion/GlyphPixelConverter.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Конвертує растеризований гліф (32bpp BGRA з RasterizedGlyph,
//     RGB=білий/Alpha=покриття — див. GdiGlyphRasterizer) у сирі байти
//     D3DFMT_A4R4G4B4 для запису в BODY того самого розміру (canvas
//     розмір НЕ змінюється — FONT_FORMAT_SPEC.md, розділ 5: "перезаписати
//     існуючий гліф-слот").
//
//     СХЕМА ЗАПИСУ RGB (емпірично підтверджено GlyphAtlasStyleAnalyzer на
//     543819 пікселях, ОБИДВІ гри, УСІ шрифти/сторінки/гліфи —
//     FONT_FORMAT_SPEC.md розділ 3.1):
//       - Alpha>0 → RGB завжди білий (0xFFF) в оригіналі, БЕЗ ЖОДНОГО
//         винятку (0 аномалій на весь датасет).
//       - Alpha=0 → RGB в оригіналі буває і білий, і нульовий, розкид
//         хаотичний, без закономірності за грою/шрифтом/сторінкою — тобто
//         не існує параметра, яким можна коректно "відтворити" оригінал.
//         Оскільки Alpha=0 завжди невидимо на екрані незалежно від RGB,
//         ця конкретна невизначеність не впливає на результат.
//     ВИСНОВОК: конвертер ЗАВЖДИ пише RGB=0xFFF, незалежно від Alpha.
//     Жодного параметра "стилю гри/ресурсу" немає — бо немає підтвердженої
//     даними закономірності, яку такий параметр міг би відображати.
//
//     Розширення 8→4 біт: округлення до найближчого 4-бітного значення
//     (round(v/17)), а не проста truncate (>>4) — бо декодування робить
//     value*17 (FONT_FORMAT_SPEC.md розділ 3), і округлення дає найменшу
//     похибку round-trip замість систематичного заниження.
// EN: Converts a rasterized glyph (32bpp BGRA from RasterizedGlyph,
//     RGB=white/Alpha=coverage — see GdiGlyphRasterizer) into raw
//     D3DFMT_A4R4G4B4 bytes for writing into a same-sized BODY slot
//     (canvas size unchanged — FONT_FORMAT_SPEC.md, section 5:
//     "overwrite existing glyph slot").
//
//     RGB WRITE SCHEME (empirically confirmed by GlyphAtlasStyleAnalyzer
//     across 543819 pixels, BOTH games, ALL fonts/pages/glyphs —
//     FONT_FORMAT_SPEC.md section 3.1):
//       - Alpha>0 → RGB is always white (0xFFF) in the original, with NO
//         EXCEPTIONS (0 anomalies across the whole dataset).
//       - Alpha=0 → RGB in the original is sometimes white, sometimes
//         zero, with a chaotic spread not tied to game/font/page — i.e.
//         no parameter exists that could correctly "replicate" the
//         original. Since Alpha=0 is always invisible on screen
//         regardless of RGB, this particular ambiguity doesn't affect
//         the visible result.
//     CONCLUSION: the converter ALWAYS writes RGB=0xFFF, regardless of
//     Alpha. There is no "game/resource style" parameter — because no
//     data-confirmed pattern exists for such a parameter to represent.
//
//     8→4 bit reduction: rounding to the nearest 4-bit value
//     (round(v/17)), not simple truncation (>>4) — since decoding does
//     value*17 (FONT_FORMAT_SPEC.md section 3), rounding gives the
//     smallest round-trip error instead of systematic under-shoot.
// =============================================================================

using BF1LocalizationTool.FontGenerator.Rasterization;

namespace BF1LocalizationTool.FontGenerator.PixelConversion;

public static class GlyphPixelConverter
{
    // -------------------------------------------------------------------------
    // UA: Конвертує один растеризований гліф у сирі A4R4G4B4-байти.
    //     Розмір результату = glyph.Width * glyph.Height * 2 байти —
    //     точно те, що очікує UcfbWriter-заміна того самого BODY-слоту
    //     (розмір заміни МОЖЕ відрізнятись від оригіналу за API
    //     UcfbWriter, але для стратегії "перезаписати існуючий слот"
    //     він має збігатись з розміром донора).
    // EN: Converts a single rasterized glyph into raw A4R4G4B4 bytes.
    //     Result size = glyph.Width * glyph.Height * 2 bytes — exactly
    //     what a UcfbWriter replacement of that same BODY slot expects
    //     (UcfbWriter's API allows a differing replacement size, but for
    //     the "overwrite existing slot" strategy it must match the
    //     donor's size).
    // -------------------------------------------------------------------------
    public static byte[] ToA4R4G4B4(RasterizedGlyph glyph) => ToA4R4G4B4(glyph, 1.0);

    // -------------------------------------------------------------------------
    // UA: Перевантаження з alphaGain — множник ПОКРИТТЯ (альфи) у 8-бітному
    //     домені ПЕРЕД квантуванням у 4 біти. Причина (ЕМПІРИЧНО виміряно на
    //     реальному атласі): гліфи, згенеровані рендером+сильним
    //     бікубічним зменшенням (RenderToFit/ProgressiveResize з ~200px до
    //     ~10px), майже цілком складаються з напівпрозорих країв — лише ~8%
    //     чорнила має ПОВНУ альфу, тоді як в оригінальних гліфах гри ~63%
    //     (великі) / ~51% (малі). Через це кирилиця виглядає сірою/тьмяною
    //     проти англійської. alphaGain піднімає покриття (з клампом до 255),
    //     відновлюючи суцільне "тіло" літери. Виміряно: gain≈1.5 виводить
    //     нашу частку повної альфи до ~65%, майже точно як оригінал.
    //     gain=1.0 — тотожність (значення за замовчуванням; донорський
    //     конвеєр викликає без alphaGain і лишається незачепленим).
    // EN: Overload with alphaGain — a multiplier on COVERAGE (alpha) in the
    //     8-bit domain BEFORE quantizing to 4 bits. Reason (EMPIRICALLY
    //     measured on the real atlas): glyphs produced by
    //     render + heavy bicubic downscale (RenderToFit/ProgressiveResize
    //     from ~200px to ~10px) are almost entirely semi-transparent edges
    //     — only ~8% of ink reaches FULL alpha, whereas the game's original
    //     glyphs are ~63% (uppercase) / ~51% (lowercase). This makes
    //     Cyrillic look gray/dim next to English. alphaGain lifts coverage
    //     (clamped to 255), restoring the letter's solid "body". Measured:
    //     gain≈1.5 brings our full-alpha fraction to ~65%, nearly matching
    //     the original. gain=1.0 is the identity (the default; the donor
    //     pipeline calls without alphaGain and is unaffected).
    // -------------------------------------------------------------------------
    public static byte[] ToA4R4G4B4(RasterizedGlyph glyph, double alphaGain)
    {
        var expectedBgraLength = glyph.Width * glyph.Height * 4;
        if (glyph.BgraPixels.Length != expectedBgraLength)
            throw new ArgumentException(
                $"UA: RasterizedGlyph.BgraPixels має довжину {glyph.BgraPixels.Length}, " +
                $"очікується {expectedBgraLength} (Width*Height*4) / " +
                $"EN: RasterizedGlyph.BgraPixels length is {glyph.BgraPixels.Length}, " +
                $"expected {expectedBgraLength} (Width*Height*4)", nameof(glyph));

        var pixelCount = glyph.Width * glyph.Height;
        var result = new byte[pixelCount * 2];

        for (var i = 0; i < pixelCount; i++)
        {
            // UA: RasterizedGlyph.BgraPixels — порядок B,G,R,A на піксель
            //     (як задокументовано в RasterizedGlyph.cs).
            // EN: RasterizedGlyph.BgraPixels — B,G,R,A byte order per
            //     pixel (as documented in RasterizedGlyph.cs).
            var srcOffset = i * 4;
            var alpha8 = glyph.BgraPixels[srcOffset + 3];

            if (alphaGain != 1.0 && alpha8 > 0)
                alpha8 = (byte)Math.Clamp((int)Math.Round(alpha8 * alphaGain), 0, 255);

            var a4 = Round8To4(alpha8);

            // UA: RGB завжди 0xF (білий) — див. обґрунтування у заголовку
            //     файлу. Не залежить від значення alpha8 чи вхідного RGB.
            // EN: RGB always 0xF (white) — see file header rationale.
            //     Independent of alpha8 or the input RGB value.
            const int r4 = 0xF;
            const int g4 = 0xF;
            const int b4 = 0xF;

            var value = (ushort)((a4 << 12) | (r4 << 8) | (g4 << 4) | b4);

            var dstOffset = i * 2;
            result[dstOffset + 0] = (byte)(value & 0xFF);        // little-endian low byte
            result[dstOffset + 1] = (byte)((value >> 8) & 0xFF); // little-endian high byte
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // UA: Округлення 8-бітного каналу (0-255) до найближчого 4-бітного
    //     значення (0-15), сумісне з декодуванням value*17
    //     (FontTexturePixelReader.DecodePixel). round(v/17), затиснуте
    //     до [0,15] на випадок v=255 (255/17=15.0 рівно, без переповнення,
    //     але Math.Round може дати 15.0 → 15, залишаємо Clamp для безпеки).
    // EN: Rounds an 8-bit channel (0-255) to the nearest 4-bit value
    //     (0-15), compatible with the value*17 decoding
    //     (FontTexturePixelReader.DecodePixel). round(v/17), clamped to
    //     [0,15] in case v=255 (255/17=15.0 exactly, no overflow, but
    //     Math.Round could yield 15.0 → 15; Clamp kept for safety).
    // -------------------------------------------------------------------------
    private static int Round8To4(byte value8) =>
        Math.Clamp((int)Math.Round(value8 / 17.0, MidpointRounding.AwayFromZero), 0, 15);
}