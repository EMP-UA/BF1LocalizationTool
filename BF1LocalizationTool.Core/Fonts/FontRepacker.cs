// =============================================================================
// BF1LocalizationTool.Core — Fonts/FontRepacker.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перепаковує НАЯВНИЙ шрифт у СВІЖИЙ атлас: витягує пікселі кожного
//     гліфа з його оригінальної сторінки, розкладає всі гліфи заново
//     (GlyphAtlasPacker) у нові текстурні сторінки власного розміру,
//     копіює пікселі БАЙТ-У-БАЙТ (без декодування — сирі A4R4G4B4
//     uint16), і перебудовує таблицю гліфів (FBOD) з новими UV/сторінкою.
//
//     ПРИЗНАЧЕННЯ — валідаційний місток перед рендером з TTF: гліфи ті
//     самі (ті самі пікселі, метрики, коди), змінюється ЛИШЕ розкладка
//     атласу й UV. Якщо гра після цього рендерить текст ІДЕНТИЧНО — це
//     доводить, що наш bin-packer + генерація UV/FBOD коректні, ЩЕ ДО
//     того, як додавати найризикованішу частину (свіжий рендер літер).
//
//     Що зберігається БЕЗ ЗМІН у кожному записі: Code, XAdvance, Bearing,
//     CellHeight, InkWidth, ReservedByte4 (курсорні метрики двигуна — не
//     залежать від розкладки атласу, FONT_FORMAT_SPEC.md §4.3-4.4).
//     Що змінюється: PageIndex, U0/U1/V0/V1 (нове фізичне місце гліфа).
//
//     Конвенція UV нового атласу: U0=лівий<U1=правий, V0=верхній<V1=нижній
//     (min(V,·)=верх, §4.2). Пікселі копіюються рядок-за-рядком зверху
//     вниз, тому напрямок узгоджений незалежно від того, який був у
//     оригіналі (оригінал міг мати U0>U1 чи V0>V1 — ми нормалізуємо).
// EN: Repacks an EXISTING font into a FRESH atlas: extracts each glyph's
//     pixels from its original page, re-lays-out all glyphs
//     (GlyphAtlasPacker) into new texture pages of our own size, copies
//     pixels BYTE-FOR-BYTE (no decoding — raw A4R4G4B4 uint16), and
//     rebuilds the glyph table (FBOD) with new UVs/page.
//
//     PURPOSE — a validation bridge before TTF rendering: the glyphs are
//     the same (same pixels, metrics, codes), only the atlas layout and
//     UVs change. If the game then renders text IDENTICALLY, it proves our
//     bin-packer + UV/FBOD generation are correct, BEFORE adding the
//     riskiest part (freshly rendering the letters).
//
//     Preserved UNCHANGED per record: Code, XAdvance, Bearing, CellHeight,
//     InkWidth, ReservedByte4 (engine cursor metrics — independent of
//     atlas layout, FONT_FORMAT_SPEC.md §4.3-4.4). Changed: PageIndex,
//     U0/U1/V0/V1 (the glyph's new physical location).
//
//     New atlas UV convention: U0=left<U1=right, V0=top<V1=bottom
//     (min(V,·)=top, §4.2). Pixels are copied row-by-row top-to-bottom, so
//     the direction is consistent regardless of the original's (which may
//     have had U0>U1 or V0>V1 — we normalize).
// =============================================================================

namespace BF1LocalizationTool.Core.Fonts;

// UA: ОДИН новий (ще не розміщений) гліф для RepackWithAdditions — тіло
//     Record слугує ШАБЛОНОМ (Code/XAdvance/Bearing/CellHeight/InkWidth/
//     ReservedByte4 беруться як є), а PageIndex/U0/U1/V0/V1 у ньому
//     ІГНОРУЮТЬСЯ — пакувальник підставляє свої, щойно визначить місце.
//     PixelsA4R4G4B4 — щільно упакований (без padding між рядками) буфер
//     Width×Height×2 байт, рядок-за-рядком, той самий формат, що повертає
//     GlyphPixelConverter.ToA4R4G4B4.
// EN: ONE new (not yet placed) glyph for RepackWithAdditions — Record
//     serves as a TEMPLATE (Code/XAdvance/Bearing/CellHeight/InkWidth/
//     ReservedByte4 taken as-is), while its PageIndex/U0/U1/V0/V1 are
//     IGNORED — the packer substitutes its own once it decides the spot.
//     PixelsA4R4G4B4 is a tightly packed (no inter-row padding) Width×
//     Height×2-byte buffer, row-by-row, the same format
//     GlyphPixelConverter.ToA4R4G4B4 returns.
public readonly record struct NewGlyphInput(FontGlyphRecord Record, byte[] PixelsA4R4G4B4, int Width, int Height);

public static class FontRepacker
{
    private const uint FormatA4R4G4B4 = 0x1A;
    private const int BytesPerPixel = 2;

    // UA: scale — коефіцієнт ЗБІЛЬШЕННЯ гліфів (1.0 = точне перепакування без
    //     зміни розміру, гарантовано байт-у-байт як валідовано; >1.0 = кожен
    //     гліф масштабується bilinear-ресемплінгом, а метрики XAdvance/Bearing/
    //     CellHeight/InkWidth множаться на scale). fontHeightPx НЕ чіпаємо:
    //     емпірично (баг зі сторінкою 256) доведено, що екранний розмір бере
    //     двигун із НОРМОВАНОГО UV-прольоту при незмінному fontHeightPx, тож
    //     збільшення пікселів гліфа вже дає більший текст, а чіпання
    //     fontHeightPx ризикувало б подвійним масштабуванням.
    // EN: scale — the glyph ENLARGEMENT factor (1.0 = exact repack with no size
    //     change, guaranteed byte-for-byte as validated; >1.0 = each glyph is
    //     bilinearly resampled, and the XAdvance/Bearing/CellHeight/InkWidth
    //     metrics are multiplied by scale). fontHeightPx is left ALONE:
    //     empirically (the page-size-256 bug) the engine derives on-screen size
    //     from the NORMALIZED UV span with fontHeightPx unchanged, so enlarging
    //     the glyph pixels already yields bigger text, and touching fontHeightPx
    //     would risk double-scaling.
    public static FontResourceData Repack(FontResourceData src, int pageWidth, int pageHeight, int padding = 1, float scale = 1.0f)
    {
        var scaling = scale != 1.0f;
        // UA: 1. Витягнути прямокутник пікселів кожного гліфа з ЙОГО сторінки.
        //     Разом зберігаємо ДРОБОВУ частину лівого/верхнього UV-краю
        //     (FracX/FracY) — це "фаза" семплінгу, яку треба відтворити 1-в-1.
        // EN: 1. Extract each glyph's pixel rectangle from ITS page. Also store
        //     the FRACTIONAL part of the left/top UV edge (FracX/FracY) — the
        //     sampling "phase" that must be reproduced 1:1.
        var srcRects = new (int Page, int X, int Y, int W, int H, float FracX, float FracY)[src.Glyphs.Count];
        for (var i = 0; i < src.Glyphs.Count; i++)
        {
            var g = src.Glyphs[i];
            if (g.PageIndex >= src.Pages.Count)
                throw new InvalidDataException(
                    $"UA: Гліф code={g.Code} посилається на сторінку {g.PageIndex}, а їх лише {src.Pages.Count} / " +
                    $"EN: Glyph code={g.Code} references page {g.PageIndex}, but there are only {src.Pages.Count}");

            var page = src.Pages[g.PageIndex];

            // UA: Вирізаємо блок текселів, який ВЛАСНЕ покриває UV гліфа. Лівий/
            //     верхній край беремо через FLOOR (перший тексель блоку), ширину/
            //     висоту — округленням розмаху. ДРОБОВА частина (minUpx - x0) —
            //     це фаза UV-краю. КРИТИЧНО: BF2 і BF1 мають РІЗНУ конвенцію
            //     (емпірично: BF2 = 100% дробова 0.5 = центр текселя D3D9; BF1 =
            //     100% дробова 0.0 = край текселя). Тому НЕ хардкодимо зсув —
            //     зберігаємо оригінальну FracX/FracY і відтворюємо її нижче.
            //     Так тест тотожності (гліф не переміщено) дає РІВНО ванільний
            //     UV для ОБОХ ігор.
            // EN: Extract the texel block the glyph's UV ACTUALLY covers. Left/
            //     top edge via FLOOR (block's first texel), width/height by
            //     rounding the span. The FRACTIONAL part (minUpx - x0) is the
            //     UV edge phase. CRITICAL: BF2 and BF1 use DIFFERENT conventions
            //     (empirically: BF2 = 100% frac 0.5 = D3D9 texel center; BF1 =
            //     100% frac 0.0 = texel edge). So we DON'T hardcode an offset —
            //     we store the original FracX/FracY and reproduce it below. Thus
            //     the identity test (glyph not relocated) yields EXACTLY the
            //     vanilla UV for BOTH games.
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
            srcRects[i] = (g.PageIndex, x0, y0, w, h, (float)(minUpx - x0), (float)(minVpx - y0));
        }

        // UA: 2. Розкласти всі гліфи заново. При scale>1 packer отримує
        //     ЗБІЛЬШЕНІ розміри (нульові лишаються нульовими — пробіл).
        // EN: 2. Re-lay-out all glyphs. With scale>1 the packer gets ENLARGED
        //     sizes (zero stays zero — space glyph).
        var sizes = srcRects
            .Select(r => scaling ? (ScaleDim(r.W, scale), ScaleDim(r.H, scale)) : (r.W, r.H))
            .ToList();
        var placements = GlyphAtlasPacker.Pack(sizes, pageWidth, pageHeight, padding);
        var pageCount = placements.Count == 0 ? 1 : placements.Max(p => p.PageIndex) + 1;

        // UA: 3. Порожні (прозорі) нові сторінки. / EN: 3. Empty (transparent) new pages.
        var newPagePixels = new byte[pageCount][];
        for (var p = 0; p < pageCount; p++)
            newPagePixels[p] = new byte[pageWidth * pageHeight * BytesPerPixel];

        // UA: 4. Скопіювати пікселі кожного гліфа на нове місце (рядок-за-рядком).
        // EN: 4. Copy each glyph's pixels to its new spot (row-by-row).
        for (var i = 0; i < src.Glyphs.Count; i++)
        {
            var (srcPage, sx, sy, w, h, _, _) = srcRects[i];
            if (w == 0 || h == 0) continue; // UA: пробіл тощо / EN: space etc.

            var dst = placements[i];
            var srcBuf = src.Pages[srcPage].BodyPixels;
            var srcW = src.Pages[srcPage].Width;
            var dstBuf = newPagePixels[dst.PageIndex];

            if (!scaling)
            {
                // UA: Точна копія (scale==1) — рядок-за-рядком, без втрат.
                // EN: Exact copy (scale==1) — row-by-row, lossless.
                for (var row = 0; row < h; row++)
                {
                    var srcOffset = ((sy + row) * srcW + sx) * BytesPerPixel;
                    var dstOffset = ((dst.Y + row) * pageWidth + dst.X) * BytesPerPixel;
                    Buffer.BlockCopy(srcBuf, srcOffset, dstBuf, dstOffset, w * BytesPerPixel);
                }
            }
            else
            {
                // UA: Bilinear-ресемплінг блоку w×h → dst.Width×dst.Height.
                // EN: Bilinear resample of the w×h block → dst.Width×dst.Height.
                ResampleBlock(srcBuf, srcW, sx, sy, w, h,
                              dstBuf, pageWidth, dst.X, dst.Y, dst.Width, dst.Height);
            }
        }

        // UA: 5. Нові записи FBOD — метрики ті самі, UV/сторінка нові.
        //     ЗБЕРІГАЄМО НАПРЯМОК оригіналу (§4.2 — U0/U1, V0/V1 не завжди
        //     зростають; якщо оригінал був "дзеркальний" U0>U1, двигун
        //     дзеркалить гліф при рендері — новий запис має зберегти те
        //     саме відношення, інакше гліф відрендериться перевернутим).
        //     Пікселі в атласі лежать у екранній орієнтації (копіювались
        //     [min..max] зверху-вниз), дзеркалення — суто ефект напрямку UV.
        // EN: 5. New FBOD records — same metrics, new UVs/page. PRESERVE the
        //     original DIRECTION (§4.2 — U0/U1, V0/V1 aren't always
        //     ascending; if the original was "mirrored" U0>U1, the engine
        //     mirrors the glyph at render time — the new record must keep
        //     the same relation, else the glyph renders flipped). Pixels in
        //     the atlas are in screen orientation (copied [min..max]
        //     top-to-bottom), mirroring is purely a UV-direction effect.
        var newGlyphs = new List<FontGlyphRecord>(src.Glyphs.Count);
        for (var i = 0; i < src.Glyphs.Count; i++)
        {
            var g = src.Glyphs[i];
            var pl = placements[i];

            // UA: Відтворюємо ТОЧНУ UV-фазу оригіналу через збережену
            //     дробову частину FracX/FracY (BF2=0.5, BF1=0.0 — конвенції
            //     різні!). Блок гліфа лежить у нових текселях [pl.X, pl.X+width),
            //     а UV-край = (pl.X + FracX)/W, тобто та сама фаза, що й у ванілі.
            //     ПЕРЕВІРКА ТОТОЖНОСТІ: якщо гліф НЕ переміщено (pl.X = перший
            //     src-тексель), то (pl.X + FracX) = оригінальний minU·W → UV
            //     байт-у-байт ванільний, і для BF2, і для BF1. Розмах (span) =
            //     width/W (розмір гліфа не змінюється). Правий/нижній край <1.0
            //     гарантовано відступом packer-а (pl.X+width <= W-1).
            // EN: Reproduce the original's EXACT UV phase via the stored
            //     fractional part FracX/FracY (BF2=0.5, BF1=0.0 — different
            //     conventions!). The glyph block sits in new texels
            //     [pl.X, pl.X+width), and the UV edge = (pl.X + FracX)/W — the
            //     same phase as vanilla. IDENTITY CHECK: if a glyph is NOT
            //     relocated (pl.X = first src texel), then (pl.X + FracX) =
            //     original minU·W → UV byte-for-byte vanilla, for both BF2 and
            //     BF1. Span = width/W (glyph size unchanged). The right/bottom
            //     edge stays < 1.0 thanks to the packer margin (pl.X+width<=W-1).
            var (_, _, _, _, _, fracX, fracY) = srcRects[i];
            var leftU = (pl.X + fracX) / pageWidth;
            var rightU = (pl.X + pl.Width + fracX) / pageWidth;
            var topV = (pl.Y + fracY) / pageHeight;
            var bottomV = (pl.Y + pl.Height + fracY) / pageHeight;

            var uFlipped = g.U0 > g.U1; // UA: оригінал дзеркальний по X / EN: original mirrored on X
            var vFlipped = g.V0 > g.V1; // UA: оригінал дзеркальний по Y / EN: original mirrored on Y

            // UA: При scale>1 множимо горизонтальні/вертикальні метрики курсора
            //     на scale (пропорційне збільшення відступів і висоти комірки),
            //     інакше літери накладались би/лишались "дрібно" розставленими.
            //     ReservedByte4 і Code НЕ чіпаємо. При scale==1 — метрики ті самі.
            // EN: With scale>1 multiply the cursor horizontal/vertical metrics by
            //     scale (proportional advance/bearing/cell-height), else letters
            //     would overlap / stay "small"-spaced. ReservedByte4 and Code are
            //     untouched. With scale==1 the metrics are unchanged.
            newGlyphs.Add(g with
            {
                PageIndex = (byte)pl.PageIndex,
                XAdvance = scaling ? ScaleByte(g.XAdvance, scale) : g.XAdvance,
                InkWidth = scaling ? ScaleByte(g.InkWidth, scale) : g.InkWidth,
                Bearing = scaling ? ScaleByte(g.Bearing, scale) : g.Bearing,
                CellHeight = scaling ? ScaleByte(g.CellHeight, scale) : g.CellHeight,
                U0 = uFlipped ? rightU : leftU,
                U1 = uFlipped ? leftU : rightU,
                V0 = vFlipped ? bottomV : topV,
                V1 = vFlipped ? topV : bottomV,
            });
        }

        // UA: 6. Нові сторінки-моделі. / EN: 6. New page models.
        var newPages = new List<FontTexturePageData>(pageCount);
        for (var p = 0; p < pageCount; p++)
            newPages.Add(new FontTexturePageData
            {
                Name = $"{src.BaseName}_tex{p}",
                Width = pageWidth,
                Height = pageHeight,
                Format = FormatA4R4G4B4,
                BodyPixels = newPagePixels[p],
            });

        return new FontResourceData
        {
            BaseName = src.BaseName,
            FontHeightPx = src.FontHeightPx,
            Pages = newPages,
            Glyphs = newGlyphs,
        };
    }

    // UA: Пакує РАЗОМ і ІСНУЮЧІ гліфи шрифту (їхні пікселі переносяться
    //     1-в-1, без масштабування — завжди scale=1 семантика), і НОВІ
    //     (щойно зрендерені, передані як сирі пікселі+метрики) — через
    //     GlyphAtlasPacker (сортування за спаданням висоти + автоматичний
    //     перехід на нову сторінку, коли місця не залишилось), той самий
    //     пакувальник, що й Repack. Завдяки цьому жодна літера не
    //     пропускається через брак місця: кількість сторінок зростає
    //     рівно настільки, скільки треба.
    // EN: Packs the font's EXISTING glyphs (pixels carried over 1:1, no
    //     scaling — always scale=1 semantics) TOGETHER with NEW ones
    //     (freshly rendered, passed as raw pixels+metrics), via
    //     GlyphAtlasPacker (descending-height sort + automatic new-page
    //     spillover) — the same packer Repack uses. This guarantees no
    //     letter is ever skipped for lack of space: the page count simply
    //     grows to fit.
    public static FontResourceData RepackWithAdditions(
        FontResourceData src, IReadOnlyList<NewGlyphInput> additions, int pageWidth, int pageHeight, int padding = 1)
    {
        // UA: 1. Вирізаємо прямокутники ІСНУЮЧИХ гліфів (та сама логіка,
        //     що й Repack при scale=1 — floor/round, фаза UV збережена).
        // EN: 1. Extract EXISTING glyphs' rectangles (same logic as Repack
        //     at scale=1 — floor/round, UV phase preserved).
        var srcRects = new (int Page, int X, int Y, int W, int H, float FracX, float FracY)[src.Glyphs.Count];
        for (var i = 0; i < src.Glyphs.Count; i++)
        {
            var g = src.Glyphs[i];
            if (g.PageIndex >= src.Pages.Count)
                throw new InvalidDataException(
                    $"UA: Гліф code={g.Code} посилається на сторінку {g.PageIndex}, а їх лише {src.Pages.Count} / " +
                    $"EN: Glyph code={g.Code} references page {g.PageIndex}, but there are only {src.Pages.Count}");

            var page = src.Pages[g.PageIndex];
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
            srcRects[i] = (g.PageIndex, x0, y0, w, h, (float)(minUpx - x0), (float)(minVpx - y0));
        }

        // UA: 2. Один спільний список розмірів — існуючі гліфи ПЕРЕД новими
        //     (порядок індексів зберігається пакувальником, лише сортування
        //     всередині нього для щільності полиць).
        // EN: 2. One combined size list — existing glyphs BEFORE new ones
        //     (index order is preserved by the packer; sorting happens only
        //     internally, for shelf density).
        var sizes = new List<(int Width, int Height)>(src.Glyphs.Count + additions.Count);
        for (var i = 0; i < src.Glyphs.Count; i++) sizes.Add((srcRects[i].W, srcRects[i].H));
        foreach (var a in additions) sizes.Add((a.Width, a.Height));

        var placements = GlyphAtlasPacker.Pack(sizes, pageWidth, pageHeight, padding);
        var pageCount = placements.Count == 0 ? 1 : placements.Max(p => p.PageIndex) + 1;

        var newPagePixels = new byte[pageCount][];
        for (var p = 0; p < pageCount; p++)
            newPagePixels[p] = new byte[pageWidth * pageHeight * BytesPerPixel];

        // UA: 3a. Копіюємо пікселі ІСНУЮЧИХ гліфів (byte-for-byte, як Repack scale=1).
        // EN: 3a. Copy EXISTING glyphs' pixels (byte-for-byte, like Repack at scale=1).
        for (var i = 0; i < src.Glyphs.Count; i++)
        {
            var (srcPage, sx, sy, w, h, _, _) = srcRects[i];
            if (w == 0 || h == 0) continue;

            var dst = placements[i];
            var srcBuf = src.Pages[srcPage].BodyPixels;
            var srcW = src.Pages[srcPage].Width;
            var dstBuf = newPagePixels[dst.PageIndex];
            for (var row = 0; row < h; row++)
            {
                var srcOffset = ((sy + row) * srcW + sx) * BytesPerPixel;
                var dstOffset = ((dst.Y + row) * pageWidth + dst.X) * BytesPerPixel;
                Buffer.BlockCopy(srcBuf, srcOffset, dstBuf, dstOffset, w * BytesPerPixel);
            }
        }

        // UA: 3b. Копіюємо пікселі НОВИХ гліфів — джерело вже щільно
        //     упаковане (Width×Height×2, без міжрядкового padding), тому
        //     sx=sy=0, srcW=Width.
        // EN: 3b. Copy NEW glyphs' pixels — the source is already tightly
        //     packed (Width×Height×2, no inter-row padding), so sx=sy=0,
        //     srcW=Width.
        for (var j = 0; j < additions.Count; j++)
        {
            var (w, h) = (additions[j].Width, additions[j].Height);
            if (w == 0 || h == 0) continue;

            var dst = placements[src.Glyphs.Count + j];
            var srcBuf = additions[j].PixelsA4R4G4B4;
            var dstBuf = newPagePixels[dst.PageIndex];
            for (var row = 0; row < h; row++)
            {
                var srcOffset = row * w * BytesPerPixel;
                var dstOffset = ((dst.Y + row) * pageWidth + dst.X) * BytesPerPixel;
                Buffer.BlockCopy(srcBuf, srcOffset, dstBuf, dstOffset, w * BytesPerPixel);
            }
        }

        // UA: 4a. Нові записи для ІСНУЮЧИХ гліфів — метрики незмінні,
        //     UV-фаза відтворена точно (як Repack scale=1).
        // EN: 4a. New records for EXISTING glyphs — metrics unchanged, UV
        //     phase reproduced exactly (like Repack at scale=1).
        var newGlyphs = new List<FontGlyphRecord>(src.Glyphs.Count + additions.Count);
        for (var i = 0; i < src.Glyphs.Count; i++)
        {
            var g = src.Glyphs[i];
            var pl = placements[i];
            var (_, _, _, _, _, fracX, fracY) = srcRects[i];
            var leftU = (pl.X + fracX) / pageWidth;
            var rightU = (pl.X + pl.Width + fracX) / pageWidth;
            var topV = (pl.Y + fracY) / pageHeight;
            var bottomV = (pl.Y + pl.Height + fracY) / pageHeight;
            var uFlipped = g.U0 > g.U1;
            var vFlipped = g.V0 > g.V1;

            newGlyphs.Add(g with
            {
                PageIndex = (byte)pl.PageIndex,
                U0 = uFlipped ? rightU : leftU,
                U1 = uFlipped ? leftU : rightU,
                V0 = vFlipped ? bottomV : topV,
                V1 = vFlipped ? topV : bottomV,
            });
        }

        // UA: 4b. Нові записи для НОВИХ гліфів — щойно зрендерені, без
        //     дзеркалення й без фази (пряме відображення прямокутника, та
        //     сама конвенція, що й раніше в TryPlaceLetter).
        // EN: 4b. New records for NEW glyphs — freshly rendered, no
        //     mirroring, no phase (straightforward rect mapping, the same
        //     convention TryPlaceLetter used before).
        for (var j = 0; j < additions.Count; j++)
        {
            var pl = placements[src.Glyphs.Count + j];
            newGlyphs.Add(additions[j].Record with
            {
                PageIndex = (byte)pl.PageIndex,
                U0 = pl.X / (float)pageWidth,
                U1 = (pl.X + pl.Width) / (float)pageWidth,
                V0 = pl.Y / (float)pageHeight,
                V1 = (pl.Y + pl.Height) / (float)pageHeight,
            });
        }

        // UA: 5. FBOD МУСИТЬ лишатись відсортованим за Code (бінарний
        //     пошук гри, FONT_FORMAT_SPEC.md §11.3) — FontResourceBuilder.Build
        //     пише Glyphs як є, сортування не робить.
        // EN: 5. FBOD MUST stay sorted by Code (the game's binary search,
        //     FONT_FORMAT_SPEC.md §11.3) — FontResourceBuilder.Build writes
        //     Glyphs as-is, it does not sort.
        newGlyphs = newGlyphs.OrderBy(gl => gl.Code).ToList();

        var newPages = new List<FontTexturePageData>(pageCount);
        for (var p = 0; p < pageCount; p++)
            newPages.Add(new FontTexturePageData
            {
                Name = $"{src.BaseName}_tex{p}",
                Width = pageWidth,
                Height = pageHeight,
                Format = FormatA4R4G4B4,
                BodyPixels = newPagePixels[p],
            });

        return new FontResourceData
        {
            BaseName = src.BaseName,
            FontHeightPx = src.FontHeightPx,
            Pages = newPages,
            Glyphs = newGlyphs,
        };
    }

    // UA: floor + затиск у [0, max]. Лівий/верхній край блоку гліфа беремо
    //     через FLOOR (half-texel: центр текселя n = n+0.5, тож floor(n+0.5)=n
    //     — правильний перший тексель). Затиск на випадок дрібних float-похибок.
    // EN: floor + clamp to [0, max]. The glyph block's left/top edge is taken
    //     via FLOOR (half-texel: texel n's center is n+0.5, so floor(n+0.5)=n —
    //     the correct first texel). Clamp guards against tiny float errors.
    private static int FloorClamp(float v, int max)
    {
        var r = (int)Math.Floor(v);
        return r < 0 ? 0 : r > max ? max : r;
    }

    // UA: Масштаб розміру гліфа: 0 лишається 0 (пробіл), інакше округлення
    //     з мінімумом 1 (гліф не має "зникнути" в 0px).
    // EN: Scale a glyph dimension: 0 stays 0 (space), else round with a
    //     minimum of 1 (a glyph must not "vanish" to 0px).
    private static int ScaleDim(int d, float scale) =>
        d == 0 ? 0 : Math.Max(1, (int)Math.Round(d * scale, MidpointRounding.AwayFromZero));

    // UA: Масштаб байтової метрики з затиском у [0, 255].
    // EN: Scale a byte metric with clamping to [0, 255].
    private static byte ScaleByte(byte b, float scale) =>
        (byte)Math.Clamp((int)Math.Round(b * scale, MidpointRounding.AwayFromZero), 0, 255);

    // -------------------------------------------------------------------------
    // UA: Bilinear-ресемплінг прямокутника A4R4G4B4 (uint16 LE) з
    //     src-блоку (srcW-широка сторінка, зона sx,sy,w,h) у dst-блок
    //     (dstW-широка сторінка, зона dstX,dstY,dstWidth,dstHeight).
    //     Кожен канал (A,R,G,B по 4 біти) інтерполюється окремо в діапазоні
    //     0..15, координати відображаються за центрами пікселів
    //     ((d+0.5)·src/dst − 0.5), краї затискаються. Для гліфів (RGB=білий,
    //     Alpha=покриття) це згладжено збільшує форму без артефактів меж.
    // EN: Bilinear resample of an A4R4G4B4 (uint16 LE) rectangle from the
    //     src block (srcW-wide page, region sx,sy,w,h) into the dst block
    //     (dstW-wide page, region dstX,dstY,dstWidth,dstHeight). Each channel
    //     (A,R,G,B, 4 bits) is interpolated separately in 0..15, coordinates
    //     mapped by pixel centers ((d+0.5)·src/dst − 0.5), edges clamped. For
    //     glyphs (RGB=white, Alpha=coverage) this smoothly enlarges the shape
    //     without edge artifacts.
    // -------------------------------------------------------------------------
    private static void ResampleBlock(
        byte[] srcBuf, int srcW, int sx, int sy, int w, int h,
        byte[] dstBuf, int dstW, int dstX, int dstY, int dstWidth, int dstHeight)
    {
        for (var dy = 0; dy < dstHeight; dy++)
        {
            var fy = (dy + 0.5) * h / dstHeight - 0.5;
            if (fy < 0) fy = 0; else if (fy > h - 1) fy = h - 1;
            var y0 = (int)Math.Floor(fy);
            var y1 = Math.Min(y0 + 1, h - 1);
            var ty = fy - y0;

            for (var dx = 0; dx < dstWidth; dx++)
            {
                var fx = (dx + 0.5) * w / dstWidth - 0.5;
                if (fx < 0) fx = 0; else if (fx > w - 1) fx = w - 1;
                var x0 = (int)Math.Floor(fx);
                var x1 = Math.Min(x0 + 1, w - 1);
                var tx = fx - x0;

                var p00 = ReadPixel(srcBuf, srcW, sx + x0, sy + y0);
                var p10 = ReadPixel(srcBuf, srcW, sx + x1, sy + y0);
                var p01 = ReadPixel(srcBuf, srcW, sx + x0, sy + y1);
                var p11 = ReadPixel(srcBuf, srcW, sx + x1, sy + y1);

                var a = Bilerp(p00.a, p10.a, p01.a, p11.a, tx, ty);
                var r = Bilerp(p00.r, p10.r, p01.r, p11.r, tx, ty);
                var g = Bilerp(p00.g, p10.g, p01.g, p11.g, tx, ty);
                var b = Bilerp(p00.b, p10.b, p01.b, p11.b, tx, ty);

                WritePixel(dstBuf, dstW, dstX + dx, dstY + dy, a, r, g, b);
            }
        }
    }

    private static (int a, int r, int g, int b) ReadPixel(byte[] buf, int pageW, int x, int y)
    {
        var off = (y * pageW + x) * BytesPerPixel;
        var val = (ushort)(buf[off] | (buf[off + 1] << 8));
        return ((val >> 12) & 0xF, (val >> 8) & 0xF, (val >> 4) & 0xF, val & 0xF);
    }

    private static void WritePixel(byte[] buf, int pageW, int x, int y, int a, int r, int g, int b)
    {
        var off = (y * pageW + x) * BytesPerPixel;
        var val = (ushort)(((a & 0xF) << 12) | ((r & 0xF) << 8) | ((g & 0xF) << 4) | (b & 0xF));
        buf[off] = (byte)(val & 0xFF);
        buf[off + 1] = (byte)(val >> 8);
    }

    private static int Bilerp(int c00, int c10, int c01, int c11, double tx, double ty)
    {
        var top = c00 + (c10 - c00) * tx;
        var bot = c01 + (c11 - c01) * tx;
        var v = top + (bot - top) * ty;
        var r = (int)Math.Round(v, MidpointRounding.AwayFromZero);
        return r < 0 ? 0 : r > 15 ? 15 : r;
    }
}
