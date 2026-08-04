// =============================================================================
// BF1LocalizationTool.Core — Fonts/GlyphAtlasPacker.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Простий shelf (полицевий) bin-packer: розкладає N прямокутників
//     (гліф-комірок) у текстурні сторінки заданого розміру, переливаючись
//     на нову сторінку, коли поточна заповнена. Детермінований, повністю
//     тестується БЕЗ гри й БЕЗ GDI.
//
//     Це НОВА можливість, потрібна для генерації свіжого атласу: досі ми
//     лише вписували кирилицю в наявні донорські слоти (позиції фіксовані
//     оригіналом); тепер САМІ обираємо позиції всіх гліфів.
//
//     Алгоритм: гліфи сортуються за спаданням висоти (щоб полиці були
//     щільніші), кладуться зліва направо в поточну полицю; коли не влазить
//     по ширині — нова полиця (нижче на висоту найвищого в попередній);
//     коли не влазить по висоті — нова сторінка. Повертає розміщення в
//     ТОМУ САМОМУ порядку індексів, що й вхід (сортування — лише
//     внутрішнє, для ефективності; порядок записів FBOD зберігається
//     викликаючим кодом окремо).
//
//     Padding між гліфами (за замовчуванням 1px) — щоб білінійна
//     фільтрація двигуна не "затягувала" пікселі сусіднього гліфа в край
//     поточного. UV вказують на ТОЧНІ пікселі гліфа, padding лишається
//     прозорим бордюром навколо.
// EN: A simple shelf bin-packer: lays out N rectangles (glyph cells) into
//     texture pages of a given size, spilling to a new page when the
//     current one is full. Deterministic, fully testable WITHOUT the game
//     and WITHOUT GDI.
//
//     This is a NEW capability needed for fresh-atlas generation: until now
//     we only fit Cyrillic into existing donor slots (positions fixed by
//     the original); now we choose ALL glyphs' positions ourselves.
//
//     Algorithm: glyphs are sorted by descending height (for tighter
//     shelves), placed left-to-right on the current shelf; when a glyph
//     doesn't fit by width — a new shelf (below by the tallest of the
//     previous); when it doesn't fit by height — a new page. Returns
//     placements in the SAME index order as the input (the sort is
//     internal only, for efficiency; FBOD record order is preserved
//     separately by the caller).
//
//     Padding between glyphs (default 1px) — so the engine's bilinear
//     filtering doesn't bleed a neighbor glyph's pixels into the current
//     one's edge. UVs point to the EXACT glyph pixels; padding stays a
//     transparent border around them.
// =============================================================================

namespace BF1LocalizationTool.Core.Fonts;

// UA: Розміщення одного гліфа: на якій сторінці й де саме (лівий-верхній
//     кут X,Y) лежить його прямокутник Width×Height у новому атласі.
// EN: Placement of one glyph: which page and where (top-left X,Y) its
//     Width×Height rectangle sits in the new atlas.
public sealed record GlyphPlacement(int PageIndex, int X, int Y, int Width, int Height);

public static class GlyphAtlasPacker
{
    public static IReadOnlyList<GlyphPlacement> Pack(
        IReadOnlyList<(int Width, int Height)> glyphs,
        int pageWidth,
        int pageHeight,
        int padding = 1)
    {
        var placements = new GlyphPlacement[glyphs.Count];

        // UA: Порядок укладки — за спаданням висоти (щільніші полиці).
        //     Нульові гліфи (пробіл: 0×0) кладуться теж, з нульовим розміром.
        // EN: Packing order — descending height (tighter shelves). Zero
        //     glyphs (space: 0×0) are placed too, with zero size.
        var order = Enumerable.Range(0, glyphs.Count)
            .OrderByDescending(i => glyphs[i].Height)
            .ThenByDescending(i => glyphs[i].Width)
            .ToList();

        var page = 0;
        var shelfX = 0;
        var shelfY = 0;
        var shelfHeight = 0;

        foreach (var i in order)
        {
            var (w, h) = glyphs[i];

            if (w > pageWidth || h > pageHeight)
                throw new InvalidOperationException(
                    $"UA: Гліф #{i} ({w}×{h}) більший за сторінку {pageWidth}×{pageHeight} — збільшіть розмір сторінки / " +
                    $"EN: Glyph #{i} ({w}×{h}) is larger than the page {pageWidth}×{pageHeight} — increase the page size");

            // UA: Не влазить по ширині → нова полиця. / EN: Doesn't fit by width → new shelf.
            if (shelfX + w + padding > pageWidth)
            {
                shelfX = 0;
                shelfY += shelfHeight + padding;
                shelfHeight = 0;
            }

            // UA: Не влазить по висоті → нова сторінка. / EN: Doesn't fit by height → new page.
            if (shelfY + h + padding > pageHeight)
            {
                page++;
                shelfX = 0;
                shelfY = 0;
                shelfHeight = 0;
            }

            placements[i] = new GlyphPlacement(page, shelfX, shelfY, w, h);

            shelfX += w + padding;
            if (h > shelfHeight) shelfHeight = h;
        }

        return placements;
    }
}
