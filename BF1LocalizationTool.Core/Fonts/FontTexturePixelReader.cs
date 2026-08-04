// =============================================================================
// BF1LocalizationTool.Core — Fonts/FontTexturePixelReader.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Знаходить FMT_/INFO (для width/height) і BODY (сирі пікселі,
//     D3DFMT_A4R4G4B4) всередині чанку однієї текстурної сторінки
//     (FontTexturePage.Chunk, тобто "tex_" за FONT_FORMAT_SPEC.md,
//     розділ 2). Читає ЛИШЕ mip 0 (перший FACE/LVL_) — цього достатньо
//     для перевірки й для ін'єкції гліфів (менші mip-рівні для дрібного
//     тексту в UI не критичні).
//
//     ЗАСТЕРЕЖЕННЯ: точне розташування width/height у 16-байтному INFO
//     (offset 4 і 6, за розділом 3 специфікації) підтверджено лише
//     непрямо. Якщо GetWidth/GetHeight повертають абсурдні значення
//     (0, від'ємні через переповнення, чи не 128 для *_super_tiny) —
//     це сигнал звірити offset'и хекс-дампом через Diagnostic, а не
//     довіряти цьому коду наосліп.
// EN: Finds FMT_/INFO (for width/height) and BODY (raw pixels,
//     D3DFMT_A4R4G4B4) inside a single texture page chunk
//     (FontTexturePage.Chunk, i.e. "tex_" per FONT_FORMAT_SPEC.md,
//     section 2). Reads ONLY mip 0 (first FACE/LVL_) — sufficient for
//     verification and for glyph injection (smaller mip levels for
//     small on-screen text aren't critical here).
//
//     CAVEAT: the exact location of width/height within the 16-byte
//     INFO (offset 4 and 6, per spec section 3) is only indirectly
//     confirmed. If GetWidth/GetHeight return nonsensical values
//     (0, negative-looking via overflow, or not 128 for *_super_tiny)
//     — that's a signal to re-verify offsets via a hex dump through
//     Diagnostic, not to blindly trust this code.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Core.Fonts;

public sealed record FontTexturePixels(
    ushort Width,
    ushort Height,
    uint FormatCode,
    byte[] RawA4R4G4B4Pixels,
    UcfbChunk BodyChunk);

public static class FontTexturePixelReader
{
    public static FontTexturePixels ReadMip0(UcfbChunk texturePageChunk)
    {
        var fmt = UcfbReader.FindFirst(texturePageChunk, "FMT_")
            ?? throw new InvalidDataException(
                "UA: FMT_ не знайдено всередині текстурної сторінки / " +
                "EN: FMT_ not found inside texture page");

        var fmtInfo = fmt.Children.FirstOrDefault(c => c.FourCC == "INFO")
            ?? throw new InvalidDataException(
                "UA: INFO не знайдено всередині FMT_ / " +
                "EN: INFO not found inside FMT_");

        if (fmtInfo.RawData.Length < 16)
            throw new InvalidDataException(
                $"UA: FMT_/INFO закороткий ({fmtInfo.RawData.Length} байт, очікується 16) / " +
                $"EN: FMT_/INFO too short ({fmtInfo.RawData.Length} bytes, expected 16)");

        var formatCode = BitConverter.ToUInt32(fmtInfo.RawData, 0);
        var width = BitConverter.ToUInt16(fmtInfo.RawData, 4);
        var height = BitConverter.ToUInt16(fmtInfo.RawData, 6);

        var body = UcfbReader.FindFirst(fmt, "BODY")
            ?? throw new InvalidDataException(
                "UA: BODY не знайдено (FMT_ → FACE → LVL_ → BODY) / " +
                "EN: BODY not found (FMT_ → FACE → LVL_ → BODY)");

        return new FontTexturePixels(width, height, formatCode, body.RawData, body);
    }

    // UA: Декодує один A4R4G4B4 піксель (little-endian uint16) у 8-біт
    //     на канал (розширення 4→8 біт: value * 17, FONT_FORMAT_SPEC.md
    //     розділ 3).
    // EN: Decodes a single A4R4G4B4 pixel (little-endian uint16) into
    //     8 bits per channel (4→8 bit expansion: value * 17,
    //     FONT_FORMAT_SPEC.md section 3).
    public static (byte A, byte R, byte G, byte B) DecodePixel(byte[] rawPixels, int pixelIndex)
    {
        var value = BitConverter.ToUInt16(rawPixels, pixelIndex * 2);
        var a4 = (value >> 12) & 0xF;
        var r4 = (value >> 8) & 0xF;
        var g4 = (value >> 4) & 0xF;
        var b4 = value & 0xF;
        return ((byte)(a4 * 17), (byte)(r4 * 17), (byte)(g4 * 17), (byte)(b4 * 17));
    }
}