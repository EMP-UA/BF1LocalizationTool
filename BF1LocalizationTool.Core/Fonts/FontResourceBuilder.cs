// =============================================================================
// BF1LocalizationTool.Core — Fonts/FontResourceBuilder.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Будує ПОВНИЙ font-чанк (UcfbChunk-дерево) з нуля з FontResourceData —
//     симетрично до FontResourceReader. Це ключова НОВА здатність: досі ми
//     лише ПАТЧИЛИ наявні атласи через донорів (обмежено розміром чужих
//     слотів); тепер можемо згенерувати повністю СВІЖИЙ ресурс власного
//     розміру (усі гліфи начисто з TTF, без донорської тісноти) — і для
//     BF2, і для BF1 (формат спільний, FONT_FORMAT_SPEC.md).
//
//     Точна структура дерева (перевірено на реальному core.lvl BF2, усі 5
//     шрифтів):
//       font
//       ├── NAME  = baseName + '\0'
//       ├── HEAD  = glyphCount(u16 LE) | pageCount(u8) | fontHeightPx(u8) | 00 00
//       │           (не "E2 00" константа, а кількість гліфів,
//       │           FONT_FORMAT_SPEC.md §11.2, детально нижче біля
//       │           BuildHead)
//       ├── FTEX
//       │   ├── NAME = '{baseName}_tex0\0'   (сусідній до tex_, ПЕРЕД ним)
//       │   ├── tex_
//       │   │   ├── NAME = '{baseName}_tex0\0'
//       │   │   ├── INFO = 01 00 00 00 | format(u32)        (8 байт)
//       │   │   └── FMT_
//       │   │       ├── INFO = format(u32) | W(u16) | H(u16) | 01 00 01 00 01 07 00 00
//       │   │       └── FACE
//       │   │           └── LVL_
//       │   │               ├── INFO = 00 00 00 00 | bodySize(u32)
//       │   │               └── BODY = сирі пікселі (W*H*2)
//       │   ├── NAME = '{baseName}_tex1\0'
//       │   └── tex_ (та сама структура) ...
//       └── FBOD  = таблиця гліфів (24 байти/запис)
//
//     Усі FourCC у дереві шрифту ДРУКОВАНІ (font/NAME/HEAD/FTEX/tex_/INFO/
//     FMT_/FACE/LVL_/BODY/FBOD) — жодних хешованих ID, тому будуємо через
//     UcfbChunk.FourCcToId. Розмір DataSize НЕ виставляємо (=0) — UcfbWriter
//     перераховує його сам (підтверджено в UcfbWriter.cs). Вирівнювання на
//     4 байти й padding робить UcfbWriter при серіалізації.
// EN: Builds a COMPLETE font chunk (UcfbChunk tree) from scratch out of a
//     FontResourceData — symmetric to FontResourceReader. This is the key
//     NEW capability: until now we only PATCHED existing atlases via donors
//     (bounded by other glyphs' slot sizes); now we can generate a fully
//     FRESH resource at our own size (all glyphs rendered clean from a TTF,
//     no donor tightness) — for both BF2 and BF1 (shared format, see
//     FONT_FORMAT_SPEC.md).
//
//     Exact tree structure (verified against real core.lvl BF2, all 5
//     fonts) — see the UA block above. All FourCCs in the font
//     tree are PRINTABLE (no hashed IDs), so we build via
//     UcfbChunk.FourCcToId. We do NOT set DataSize (=0) — UcfbWriter
//     recomputes it (confirmed in UcfbWriter.cs). 4-byte alignment/padding
//     is handled by UcfbWriter at serialization time.
// =============================================================================

using System.Text;
using BF1LocalizationTool.Core.Chunks;

namespace BF1LocalizationTool.Core.Fonts;

public static class FontResourceBuilder
{
    // UA: Формат пікселів шрифтів обох ігор — завжди D3DFMT_A4R4G4B4 (0x1A).
    // EN: Both games' font pixel format — always D3DFMT_A4R4G4B4 (0x1A).
    public const uint FormatA4R4G4B4 = 0x1A;

    // UA: Сталий "хвіст" FMT_INFO після format+W+H (8 байт) — однаковий на
    //     всіх шрифтах і сторінках обох ігор (перевірено). Призначення
    //     окремих байтів не критичне; відтворюється точно.
    // EN: The constant FMT_INFO "tail" after format+W+H (8 bytes) — the
    //     same on all fonts/pages of both games (verified). The individual
    //     bytes' meaning isn't critical; reproduced exactly.
    private static readonly byte[] FmtInfoTail = [0x01, 0x00, 0x01, 0x00, 0x01, 0x07, 0x00, 0x00];

    public static UcfbChunk Build(FontResourceData font)
    {
        var children = new List<UcfbChunk>
        {
            Leaf("NAME", AsciiZ(font.BaseName)),
            // UA: glyphCount = font.Glyphs.Count, а НЕ передане окремо поле —
            //     завжди узгоджено з реальним FBOD, що пишеться нижче (жоден
            //     виклик не може випадково розсинхронити HEAD і FBOD).
            // EN: glyphCount = font.Glyphs.Count, not a separately-passed
            //     field — always consistent with the actual FBOD written
            //     below (no caller can accidentally desync HEAD from FBOD).
            Leaf("HEAD", BuildHead((ushort)font.Glyphs.Count, (byte)font.Pages.Count, font.FontHeightPx)),
            BuildFtex(font),
            Leaf("FBOD", FontGlyphTable.Serialize(font.Glyphs)),
        };

        return Container("font", children);
    }

    // -------------------------------------------------------------------------
    // UA: HEAD (6 байт): glyphCount(u16 LE) | pageCount(u8) | fontHeightPx(u8) | 00 00.
    //     Перші 2 байти HEAD — це КІЛЬКІСТЬ ГЛІФІВ шрифту, критична для
    //     того, щоб гра побачила ВСІ записи FBOD (інакше читає лише перші
    //     N=старе значення). Тому glyphCount тут завжди похідне від
    //     font.Glyphs.Count (див. коментар біля виклику вище), а не
    //     хардкоджене число: розсинхронізація HEAD і FBOD означала б, що
    //     гра прочитає лише перші N записів (усі англійські, оскільки FBOD
    //     відсортований за кодом — розділ 11.3) і не побачить жодного
    //     гліфа понад це число.
    // EN: HEAD (6 bytes): glyphCount(u16 LE) | pageCount(u8) | fontHeightPx(u8) | 00 00.
    //     HEAD's first 2 bytes are the font's GLYPH COUNT, critical for
    //     the game to see ALL FBOD records (otherwise it only reads the
    //     first N=old value). That's why glyphCount here is always
    //     derived from font.Glyphs.Count (see the comment at the call
    //     site above), never a hardcoded number: a HEAD/FBOD mismatch
    //     would mean the game reads only the first N records (all
    //     English, since FBOD is sorted by code — section 11.3) and never
    //     sees any glyph beyond that count.
    // -------------------------------------------------------------------------
    private static byte[] BuildHead(ushort glyphCount, byte pageCount, byte fontHeightPx)
    {
        var head = new byte[6];
        BitConverter.GetBytes(glyphCount).CopyTo(head, 0);
        head[2] = pageCount;
        head[3] = fontHeightPx;
        // head[4..5] = 0x00, 0x00 (стала частина / constant part)
        return head;
    }

    // -------------------------------------------------------------------------
    // UA: FTEX: для КОЖНОЇ сторінки — сусідній NAME (перед tex_) + сам tex_.
    // EN: FTEX: for EACH page — a sibling NAME (before tex_) + the tex_.
    // -------------------------------------------------------------------------
    private static UcfbChunk BuildFtex(FontResourceData font)
    {
        var kids = new List<UcfbChunk>(font.Pages.Count * 2);
        foreach (var page in font.Pages)
        {
            ValidatePage(page);
            kids.Add(Leaf("NAME", AsciiZ(page.Name)));
            kids.Add(BuildTexPage(page));
        }

        return Container("FTEX", kids);
    }

    private static UcfbChunk BuildTexPage(FontTexturePageData page)
    {
        // tex_INFO (8 байт): 01 00 00 00 | format(u32)
        var texInfo = new byte[8];
        BitConverter.GetBytes((uint)1).CopyTo(texInfo, 0);
        BitConverter.GetBytes(page.Format).CopyTo(texInfo, 4);

        var texKids = new List<UcfbChunk>
        {
            Leaf("NAME", AsciiZ(page.Name)),
            Leaf("INFO", texInfo),
            BuildFmt(page),
        };

        return Container("tex_", texKids);
    }

    private static UcfbChunk BuildFmt(FontTexturePageData page)
    {
        // FMT_INFO (16 байт): format(u32) | W(u16) | H(u16) | tail(8)
        var fmtInfo = new byte[16];
        BitConverter.GetBytes(page.Format).CopyTo(fmtInfo, 0);
        BitConverter.GetBytes((ushort)page.Width).CopyTo(fmtInfo, 4);
        BitConverter.GetBytes((ushort)page.Height).CopyTo(fmtInfo, 6);
        FmtInfoTail.CopyTo(fmtInfo, 8);

        // LVL_INFO (8 байт): 00 00 00 00 | bodySize(u32)
        var lvlInfo = new byte[8];
        BitConverter.GetBytes((uint)page.BodyPixels.Length).CopyTo(lvlInfo, 4);

        var lvl = Container("LVL_", [Leaf("INFO", lvlInfo), Leaf("BODY", page.BodyPixels)]);
        var face = Container("FACE", [lvl]);
        return Container("FMT_", [Leaf("INFO", fmtInfo), face]);
    }

    // -------------------------------------------------------------------------
    private static void ValidatePage(FontTexturePageData page)
    {
        var expected = page.Width * page.Height * 2;
        if (page.BodyPixels.Length != expected)
            throw new InvalidOperationException(
                $"UA: Сторінка '{page.Name}': BodyPixels={page.BodyPixels.Length}Б, а для {page.Width}x{page.Height} A4R4G4B4 очікується {expected}Б / " +
                $"EN: Page '{page.Name}': BodyPixels={page.BodyPixels.Length}B, but {page.Width}x{page.Height} A4R4G4B4 expects {expected}B");
    }

    private static byte[] AsciiZ(string s) => Encoding.ASCII.GetBytes(s + "\0");

    private static UcfbChunk Leaf(string fourCc, byte[] data) => new()
    {
        Id = UcfbChunk.FourCcToId(fourCc),
        FourCC = fourCc,
        DataSize = 0, // UA: перераховується UcfbWriter / EN: recomputed by UcfbWriter
        RawData = data,
    };

    private static UcfbChunk Container(string fourCc, List<UcfbChunk> children) => new()
    {
        Id = UcfbChunk.FourCcToId(fourCc),
        FourCC = fourCc,
        DataSize = 0,
        Children = children,
    };
}
