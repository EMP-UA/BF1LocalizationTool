// =============================================================================
// BF1LocalizationTool.Core — Fonts/FontChunkLocator.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Знаходить шрифтові ресурси в дереві ucfb-чанків core.lvl.
//     Базові імена шрифтів знайдені реверс-інжинірингом через
//     BF1LocalizationTool.Diagnostic (гістограма NAME-чанків реального
//     core.lvl BF1 та BF2):
//       BF1: gamefont_large, gamefont_medium, gamefont_small, gamefont_tiny,
//            gamefont_super_tiny, starwars_small (лише BF1)
//       BF2: те саме, БЕЗ starwars_small
//     Кожен базовий шрифт має 1+ текстурних сторінок '{ім'я}_texN'
//     (кількість сторінок різна між BF1/BF2 і між розмірами шрифту).
//     Це ЛОКАТОР — він лише знаходить, ДЕ лежать ресурси в дереві, і
//     повертає самі UcfbChunk (з доступом до RawData) для подальшого
//     аналізу формату. Розбір внутрішнього бінарного формату шрифту
//     (таблиця гліфів, ширини символів, мапінг байт↔гліф) винесено в
//     окремі типи — FontResourceReader, FontGlyphTable, FontGlyphRecord,
//     FontTexturePixelReader.
// EN: Finds font resources in the core.lvl ucfb chunk tree.
//     Base font names found via reverse engineering with
//     BF1LocalizationTool.Diagnostic (NAME chunk histogram of a real BF1
//     and BF2 core.lvl):
//       BF1: gamefont_large, gamefont_medium, gamefont_small, gamefont_tiny,
//            gamefont_super_tiny, starwars_small (BF1 only)
//       BF2: same, WITHOUT starwars_small
//     Each base font has 1+ texture pages '{name}_texN' (page count varies
//     between BF1/BF2 and between font sizes).
//     This is a LOCATOR — it only finds WHERE resources live in the tree,
//     and returns the UcfbChunk objects themselves (with RawData access)
//     for further format analysis. Parsing the font's internal binary
//     format (glyph table, character widths, byte↔glyph mapping) lives in
//     separate types — FontResourceReader, FontGlyphTable,
//     FontGlyphRecord, FontTexturePixelReader.
// =============================================================================

using System.Text;
using BF1LocalizationTool.Core.Chunks;

namespace BF1LocalizationTool.Core.Fonts;

// UA: Одна текстурна сторінка шрифту (напр. "gamefont_medium_tex0")
// EN: A single font texture page (e.g. "gamefont_medium_tex0")
public sealed record FontTexturePage(string Name, UcfbChunk Chunk);

// UA: Один шрифтовий ресурс з усіма своїми текстурними сторінками.
//     Chunk — контейнер, що містить NAME=BaseName і всі дочірні _texN;
//     Chunk.RawData містить усі байти ресурсу (метадані + текстури).
// EN: A single font resource with all of its texture pages.
//     Chunk — the container holding NAME=BaseName and all child _texN;
//     Chunk.RawData holds all bytes of the resource (metadata + textures).
public sealed record FontResource(string BaseName, UcfbChunk Chunk, IReadOnlyList<FontTexturePage> TexturePages);

public static class FontChunkLocator
{
    // UA: Базові імена шрифтів, спільні для BF1 і BF2.
    //     "starwars_small" зустрічається лише в BF1 — включений тут же,
    //     бо якщо відповідного NAME немає в дереві, він просто не потрапить
    //     у результат (не потрібен окремий параметр GameVersion).
    // EN: Base font names shared between BF1 and BF2.
    //     "starwars_small" only occurs in BF1 — included here too: if the
    //     corresponding NAME isn't in the tree, it simply won't appear in
    //     the result (no separate GameVersion parameter needed).
    private static readonly string[] KnownFontBaseNames =
    [
        "gamefont_large",
        "gamefont_medium",
        "gamefont_small",
        "gamefont_tiny",
        "gamefont_super_tiny",
        "starwars_small",
    ];

    // -------------------------------------------------------------------------
    // UA: Знаходить усі відомі шрифтові ресурси в дереві чанків.
    //     "Контейнер шрифту" — це чанк, серед безпосередніх дітей якого є
    //     NAME з текстом, що дорівнює одному з KnownFontBaseNames.
    //     Текстурна сторінка — вкладений (на будь-якій глибині всередині
    //     контейнера шрифту) чанк, серед дітей якого є NAME, що починається
    //     з "{BaseName}_tex".
    // EN: Finds all known font resources in the chunk tree.
    //     A "font container" is a chunk whose immediate children include a
    //     NAME whose text equals one of KnownFontBaseNames.
    //     A texture page is a nested (at any depth inside the font
    //     container) chunk whose children include a NAME starting with
    //     "{BaseName}_tex".
    // -------------------------------------------------------------------------
    public static IReadOnlyList<FontResource> FindAll(UcfbChunk root)
    {
        var results = new List<FontResource>();
        foreach (var baseName in KnownFontBaseNames)
        {
            var container = FindContainerWithName(root, baseName);
            if (container is null)
                continue; // UA: цей шрифт відсутній у цій грі/файлі / EN: this font isn't present in this game/file

            var texPrefix = baseName + "_tex";
            var pages = new List<FontTexturePage>();
            CollectTexturePages(container, texPrefix, pages);
            pages.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            results.Add(new FontResource(baseName, container, pages));
        }

        return results;
    }

    // -------------------------------------------------------------------------
    // UA: Рекурсивно шукає ПЕРШИЙ чанк, серед прямих дітей якого є
    //     NAME == name.
    // EN: Recursively finds the FIRST chunk whose direct children include a
    //     NAME == name.
    // -------------------------------------------------------------------------
    private static UcfbChunk? FindContainerWithName(UcfbChunk chunk, string name)
    {
        if (chunk.Children.Any(c => c.FourCC == "NAME" && NameText(c) == name))
            return chunk;

        foreach (var child in chunk.Children)
        {
            var found = FindContainerWithName(child, name);
            if (found is not null)
                return found;
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // UA: Рекурсивно збирає всі чанки всередині containerScope, серед прямих
    //     дітей яких є NAME, що починається з texPrefix (без дублів за іменем).
    // EN: Recursively collects all chunks inside containerScope whose direct
    //     children include a NAME starting with texPrefix (deduped by name).
    // -------------------------------------------------------------------------
    private static void CollectTexturePages(UcfbChunk containerScope, string texPrefix, List<FontTexturePage> acc)
    {
        foreach (var child in containerScope.Children)
        {
            var nameChild = child.Children.FirstOrDefault(c => c.FourCC == "NAME" && NameText(c).StartsWith(texPrefix, StringComparison.Ordinal));
            if (nameChild is not null && acc.All(p => p.Name != NameText(nameChild)))
                acc.Add(new FontTexturePage(NameText(nameChild), child));

            CollectTexturePages(child, texPrefix, acc);
        }
    }

    // -------------------------------------------------------------------------
    private static string NameText(UcfbChunk nameChunk) =>
        Encoding.ASCII.GetString(nameChunk.RawData).TrimEnd('\0');
}