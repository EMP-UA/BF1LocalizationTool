// =============================================================================
// BF1LocalizationTool.Core — Fonts/FontResourceReader.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Витягує повністю розібрану FontResourceData з наявного font-чанка
//     (UcfbChunk-дерево з core.lvl) — симетрично до FontResourceBuilder.
//     Використовується (1) для round-trip перевірки самого builder'а
//     (прочитати → перебудувати → порівняти байти), і (2) як БАЗА для
//     генерації свіжого атласу: беремо реальні метадані/таблицю гліфів
//     оригіналу за основу, замінюючи лише пікселі й UV на власні.
//
//     Навігація по дереву — точно за структурою з FontResourceBuilder
//     (font → NAME/HEAD/FTEX/FBOD; FTEX → пари NAME+tex_; tex_ → NAME/INFO/
//     FMT_; FMT_ → INFO(W,H)/FACE → LVL_ → INFO/BODY).
// EN: Extracts a fully-parsed FontResourceData from an existing font chunk
//     (a UcfbChunk tree from core.lvl) — symmetric to FontResourceBuilder.
//     Used (1) to round-trip-validate the builder itself (read → rebuild →
//     compare bytes), and (2) as a BASE for fresh-atlas generation: take
//     the original's real metadata/glyph table as a starting point,
//     replacing only pixels and UVs with our own.
//
//     Tree navigation follows exactly the FontResourceBuilder structure
//     (font → NAME/HEAD/FTEX/FBOD; FTEX → NAME+tex_ pairs; tex_ → NAME/
//     INFO/FMT_; FMT_ → INFO(W,H)/FACE → LVL_ → INFO/BODY).
// =============================================================================

using System.Text;
using BF1LocalizationTool.Core.Chunks;

namespace BF1LocalizationTool.Core.Fonts;

public static class FontResourceReader
{
    public static FontResourceData Read(UcfbChunk fontContainer)
    {
        var nameChunk = Child(fontContainer, "NAME") ?? throw Missing("NAME", "font");
        var headChunk = Child(fontContainer, "HEAD") ?? throw Missing("HEAD", "font");
        var ftex = Child(fontContainer, "FTEX") ?? throw Missing("FTEX", "font");
        var fbod = Child(fontContainer, "FBOD") ?? throw Missing("FBOD", "font");

        if (headChunk.RawData.Length < 4)
            throw new InvalidDataException(
                $"UA: HEAD завакороткий ({headChunk.RawData.Length}Б, очікується ≥4) / " +
                $"EN: HEAD too short ({headChunk.RawData.Length}B, expected ≥4)");

        var baseName = NameText(nameChunk);
        var fontHeightPx = headChunk.RawData[3]; // UA: байт 3 = висота / EN: byte 3 = height

        var pages = new List<FontTexturePageData>();
        foreach (var texChunk in ftex.Children.Where(c => c.FourCC == "tex_"))
            pages.Add(ReadPage(texChunk));

        var glyphs = FontGlyphTable.Parse(fbod.RawData);

        return new FontResourceData
        {
            BaseName = baseName,
            FontHeightPx = fontHeightPx,
            Pages = pages,
            Glyphs = glyphs,
        };
    }

    private static FontTexturePageData ReadPage(UcfbChunk texChunk)
    {
        var pageName = NameText(Child(texChunk, "NAME") ?? throw Missing("NAME", "tex_"));
        var fmt = Child(texChunk, "FMT_") ?? throw Missing("FMT_", "tex_");
        var fmtInfo = Child(fmt, "INFO") ?? throw Missing("INFO", "FMT_");

        if (fmtInfo.RawData.Length < 8)
            throw new InvalidDataException(
                $"UA: FMT_/INFO завакороткий ({fmtInfo.RawData.Length}Б) / EN: FMT_/INFO too short ({fmtInfo.RawData.Length}B)");

        var format = BitConverter.ToUInt32(fmtInfo.RawData, 0);
        var width = BitConverter.ToUInt16(fmtInfo.RawData, 4);
        var height = BitConverter.ToUInt16(fmtInfo.RawData, 6);

        var face = Child(fmt, "FACE") ?? throw Missing("FACE", "FMT_");
        var lvl = Child(face, "LVL_") ?? throw Missing("LVL_", "FACE");
        var body = Child(lvl, "BODY") ?? throw Missing("BODY", "LVL_");

        return new FontTexturePageData
        {
            Name = pageName,
            Width = width,
            Height = height,
            Format = format,
            BodyPixels = body.RawData,
        };
    }

    private static UcfbChunk? Child(UcfbChunk parent, string fourCc) =>
        parent.Children.FirstOrDefault(c => c.FourCC == fourCc);

    private static string NameText(UcfbChunk nameChunk) =>
        Encoding.ASCII.GetString(nameChunk.RawData).TrimEnd('\0');

    private static InvalidDataException Missing(string child, string parent) =>
        new($"UA: у '{parent}' немає дочірнього '{child}' / EN: '{parent}' has no '{child}' child");
}
