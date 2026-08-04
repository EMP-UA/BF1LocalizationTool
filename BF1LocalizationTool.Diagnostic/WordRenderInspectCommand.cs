// =============================================================================
// BF1LocalizationTool.Diagnostic — WordRenderInspectCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: НЕ перевіряє формулу, НЕ рендерить окремо/повторно — читає РЕАЛЬНІ,
//     УЖЕ ЗАПАТЧЕНІ пікселі напряму з "output"-файлу (той самий core.lvl,
//     який гра фактично завантажує), для конкретних слів реального
//     перекладу, і складає їх у ОДНЕ велике зображення з зумом і
//     напрямною лінією — щоб дивитись на реальні пікселі напряму, при
//     збільшенні, де можна виміряти піксель-в-піксель, а не здогадуватись
//     по стиснутому, дрібному скріншоту гри під кутом.
//
//     Джерело даних — FontChunkLocator/FontGlyphTable/
//     FontTexturePixelReader, ті самі, перевірені утиліти, що й в усіх
//     інших read-only перевірках; НЕ дублює жодної логіки рендеру з
//     CyrillicFontInjector/GlyphBoxFitRenderer — просто ЧИТАЄ, що ті вже
//     записали в output-файл.
//
//     Літера→код бере з cyrillic-code-table.json (сусід output core.lvl,
//     GenerateLocalizedCoreCommand) — та сама таблиця, яку GUI використовує
//     для показу перекладу; якщо файлу нема, значить генерацію (7→1) ще
//     не запускали для цього output.
// EN: Does NOT check a formula, does NOT re-render separately — reads
//     REAL, ALREADY-PATCHED pixels directly from the "output" file (the
//     exact core.lvl the game actually loads), for specific words of the
//     real translation, and composites them into ONE large image with
//     zoom and a guide line — to look at real pixels directly, at a size
//     where pixel-for-pixel measurement is possible, instead of guessing
//     from a compressed, tiny, angled in-game screenshot.
//
//     Data source — FontChunkLocator/FontGlyphTable/
//     FontTexturePixelReader, the SAME verified utilities used by every
//     other read-only check; does NOT duplicate any rendering logic from
//     CyrillicFontInjector/GlyphBoxFitRenderer — it just READS what they
//     already wrote to the output file.
//
//     Letter→code comes from cyrillic-code-table.json (sits next to the
//     output core.lvl, written by GenerateLocalizedCoreCommand) — the
//     same table the GUI uses to display the translation; if the file is
//     missing, generation (7→1) hasn't been run yet for this output.
// =============================================================================

using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.Versioning;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Localization;

namespace BF1LocalizationTool.Diagnostic;

[SupportedOSPlatform("windows")]
public static class WordRenderInspectCommand
{
    private const int Zoom = 10;
    private const int LetterGapPx = 3;
    private const int WordGapPx = 24;
    private const int RowPaddingTop = 24;
    private const int RowPaddingBottom = 40;
    private const int LeftPadding = 12;

    // UA: Реальні рядки перекладу (bf1.txt поруч з output) — репрезентативний
    //     набір слів для перевірки вертикального вирівнювання літер.
    // EN: Real translation lines (bf1.txt next to output) — a
    //     representative set of words for checking letters' vertical
    //     alignment.
    private static readonly string[] TestWords =
    [
        "Один", "гравець", "Мультиплеєр", "Налаштування", "Менеджер", "Профілей"
    ];

    // UA: Викликається для КОЖНОЇ гри й КОЖНОГО шрифту окремо — за
    //     стандартним правилом проєкту "перевірка ЗАВЖДИ вичерпна по
    //     обох іграх/усіх шрифтах" (та сама вимога вже застосована в
    //     GenerateLocalizedCoreCommand — усі 5 gamefont_* завжди разом,
    //     не вибірково): вибіркова перевірка лише частини комбінацій
    //     могла б пропустити регресію в тих, що лишились неперевіреними.
    //     runOutputDir — ОДНА спільна підпапка на весь запуск (Program.cs
    //     створює її РАЗ, timestamp у назві), а не плоска купа PNG у
    //     diagnostic-output — так усі 10 (2 гри × 5 шрифтів) зображень
    //     цього прогону лежать РАЗОМ, окремо від інших перевірок.
    // EN: Called for EACH game and EACH font separately — per the
    //     project's standing rule "checks are ALWAYS exhaustive across
    //     both games/all fonts" (the same requirement already applied in
    //     GenerateLocalizedCoreCommand — all 5 gamefont_* always
    //     together, never selective): checking only a subset of
    //     combinations could miss a regression in the ones left
    //     unchecked. runOutputDir — ONE shared subfolder for the whole
    //     run (Program.cs creates it ONCE, a timestamp in the name),
    //     instead of a flat pile of PNGs in diagnostic-output — so all 10
    //     (2 games × 5 fonts) images from this run sit TOGETHER, separate
    //     from other checks.
    public static void Run(DiagnosticReport report, string outputLvlPath, string label, string fontBaseName, string runOutputDir)
    {
        if (!File.Exists(outputLvlPath))
        {
            report.Log($"UA: [{label}] Файл не знайдено: {outputLvlPath}");
            report.Log($"EN: [{label}] File not found: {outputLvlPath}");
            return;
        }

        var codeTable = CyrillicCodeTable.TryLoadNextTo(outputLvlPath);
        if (codeTable is null)
        {
            report.Log($"UA: [{label}] Немає cyrillic-code-table.json поруч із файлом — спершу згенеруй локалізований core.lvl (Головне меню → 7 → 1).");
            report.Log($"EN: [{label}] No cyrillic-code-table.json next to the file — generate the localized core.lvl first (Main menu → 7 → 1).");
            return;
        }

        var root = UcfbReader.ReadFile(outputLvlPath);
        var font = FontChunkLocator.FindAll(root).FirstOrDefault(f => f.BaseName == fontBaseName);
        if (font is null)
        {
            report.Log($"UA: [{label}] Шрифт '{fontBaseName}' не знайдено у файлі.");
            return;
        }

        var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
        if (fbod is null)
        {
            report.Log($"UA: [{label}] FBOD у '{fontBaseName}' не знайдено.");
            return;
        }

        var glyphs = FontGlyphTable.Parse(fbod.RawData);
        var glyphByCode = glyphs.ToDictionary(g => g.Code);

        // UA: Кешуємо декодовані пікселі кожної сторінки — читаємо кожну
        //     сторінку РІВНО ОДИН РАЗ, а не на кожну літеру.
        // EN: Cache decoded pixels per page — read each page EXACTLY
        //     ONCE, not once per letter.
        var pagePixelsCache = new Dictionary<byte, FontTexturePixels>();
        FontTexturePixels GetPage(byte pageIndex)
        {
            if (pagePixelsCache.TryGetValue(pageIndex, out var cached)) return cached;
            var pixels = FontTexturePixelReader.ReadMip0(font.TexturePages[pageIndex].Chunk);
            pagePixelsCache[pageIndex] = pixels;
            return pixels;
        }

        // UA: Для кожного слова — список (char, box) реальних, уже
        //     запатчених прямокутників; символи поза таблицею кодів
        //     (пробіл, латиниця) — пропускаються з попередженням.
        // EN: For each word — a list of (char, box) of real, already
        //     patched rectangles; characters outside the code table
        //     (space, Latin) are skipped with a warning.
        var wordBoxes = new List<List<(char Character, Rectangle SrcRect, byte PageIndex)>>();
        foreach (var word in TestWords)
        {
            var boxes = new List<(char, Rectangle, byte)>();
            foreach (var ch in word)
            {
                if (!codeTable.Letters.Any(e => e.Character.Length == 1 && e.Character[0] == ch))
                {
                    report.Log($"UA: [{label}] '{ch}' відсутня в таблиці кодів — пропущено в слові \"{word}\".");
                    continue;
                }
                var encoded = codeTable.ToGameEncoded(ch.ToString());
                var code = (ushort)encoded[0];

                if (!glyphByCode.TryGetValue(code, out var glyph))
                {
                    report.Log($"UA: [{label}] Код 0x{code:X2} ('{ch}') відсутній у FBOD '{fontBaseName}'.");
                    continue;
                }

                var page = GetPage(glyph.PageIndex);
                var x0 = (int)Math.Round(Math.Min(glyph.U0, glyph.U1) * page.Width);
                var x1 = (int)Math.Round(Math.Max(glyph.U0, glyph.U1) * page.Width);
                var y0 = (int)Math.Round(Math.Min(glyph.V0, glyph.V1) * page.Height);
                var y1 = (int)Math.Round(Math.Max(glyph.V0, glyph.V1) * page.Height);

                boxes.Add((ch, new Rectangle(x0, y0, x1 - x0, y1 - y0), glyph.PageIndex));
            }
            if (boxes.Count > 0) wordBoxes.Add(boxes);
        }

        if (wordBoxes.Count == 0)
        {
            report.Log($"UA: [{label}] Жодної літери з тестових слів не знайдено — нічого малювати.");
            return;
        }

        // UA: Спільний "рядок вирівнювання" для ВСІХ літер усіх слів —
        //     низ найвищого боксу серед усіх (найбільша реальна висота).
        //     Кожен бокс малюється НИЗОМ до цієї лінії (припущення для
        //     ВІЗУАЛІЗАЦІЇ, узгоджене з тим, як GlyphBoxFitRenderer
        //     розміщує "тіло" — не твердження про те, як САМА ГРА
        //     насправді вирівнює квади, це тут НЕВІДОМО і не перевіряється).
        //     Мета: побачити, чи в межах ЦЬОГО зображення висота "тіла"
        //     ВІЗУАЛЬНО однакова для звичайних літер, і чи виступи
        //     ("хвости"/"вершники") дійсно НЕ роздувають тіло.
        // EN: A shared "alignment row" for ALL letters of ALL words — the
        //     bottom of the tallest box among all of them (the biggest
        //     real height). Each box is drawn bottom-aligned to this line
        //     (a VISUALIZATION assumption, consistent with how
        //     GlyphBoxFitRenderer places the "core" — NOT a claim about
        //     how the GAME ITSELF actually aligns quads, which is
        //     UNKNOWN here and not being tested). Goal: see whether,
        //     within THIS image, the "core" height LOOKS consistent for
        //     ordinary letters, and whether extensions ("tails"/"riders")
        //     really do NOT inflate the core.
        var maxBoxHeight = wordBoxes.SelectMany(w => w).Max(b => b.SrcRect.Height);

        var rowHeight = maxBoxHeight * Zoom + RowPaddingTop + RowPaddingBottom;
        var imageHeight = rowHeight * wordBoxes.Count;
        var imageWidth = LeftPadding + wordBoxes.Max(w =>
            w.Sum(b => b.SrcRect.Width * Zoom + LetterGapPx * Zoom) + WordGapPx * Zoom);

        using var bitmap = new Bitmap(imageWidth, imageHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.FromArgb(255, 15, 15, 25));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var labelFont = new Font("Segoe UI", 10, FontStyle.Regular, GraphicsUnit.Pixel);

            for (var row = 0; row < wordBoxes.Count; row++)
            {
                var boxes = wordBoxes[row];
                var rowTop = row * rowHeight;
                var baselineY = rowTop + RowPaddingTop + maxBoxHeight * Zoom;

                // UA: Напрямна лінія — спільна база вирівнювання для ЦЬОГО рядка.
                // EN: Guide line — the shared alignment base for THIS row.
                using var linePen = new Pen(Color.FromArgb(180, 220, 40, 40), 1);
                g.DrawLine(linePen, LeftPadding, baselineY, imageWidth - LeftPadding, baselineY);

                var cursorX = LeftPadding;
                foreach (var (ch, srcRect, pageIndex) in boxes)
                {
                    var page = GetPage(pageIndex);
                    using var glyphBitmap = new Bitmap(srcRect.Width, srcRect.Height, PixelFormat.Format32bppArgb);

                    // UA: Розмір БОКСУ (з FBOD, після росту) і реальна
                    //     висота чорнила всередині нього — це ДВІ РІЗНІ
                    //     речі: без контуру боксу неможливо відрізнити
                    //     "чорнило маленьке, бокс великий, прозоре поле
                    //     навколо" від "чорнило справді велике". Тому
                    //     рахуємо РЕАЛЬНУ висоту непрозорих (Alpha>0)
                    //     рядків — inkTop/inkBottom — і малюємо КОНТУР
                    //     самого боксу окремим кольором навколо кожної
                    //     літери.
                    // EN: The BOX size (from FBOD, after growth) and the
                    //     real ink height inside it are TWO DIFFERENT
                    //     things: without a box outline there's no way to
                    //     tell "small ink, big box, transparent margin
                    //     around it" apart from "the ink itself is big".
                    //     So we compute the REAL height of opaque
                    //     (Alpha>0) rows — inkTop/inkBottom — and draw the
                    //     box's OWN outline in a separate color around
                    //     every letter.
                    var inkTop = -1;
                    var inkBottom = -1;
                    for (var yy = 0; yy < srcRect.Height; yy++)
                    {
                        var rowHasInk = false;
                        for (var xx = 0; xx < srcRect.Width; xx++)
                        {
                            var pixelIndex = (srcRect.Y + yy) * page.Width + (srcRect.X + xx);
                            var (a, r, gg, b) = FontTexturePixelReader.DecodePixel(page.RawA4R4G4B4Pixels, pixelIndex);
                            glyphBitmap.SetPixel(xx, yy, Color.FromArgb(a, r, gg, b));
                            if (a > 0) rowHasInk = true;
                        }
                        if (rowHasInk)
                        {
                            if (inkTop < 0) inkTop = yy;
                            inkBottom = yy;
                        }
                    }
                    var inkHeight = inkTop < 0 ? 0 : inkBottom - inkTop + 1;

                    var destHeight = srcRect.Height * Zoom;
                    var destWidth = srcRect.Width * Zoom;
                    var destY = baselineY - destHeight;
                    g.DrawImage(glyphBitmap, new Rectangle(cursorX, destY, destWidth, destHeight));

                    // UA: Контур БОКСУ (не чорнила) — щоб бачити прозорі поля.
                    // EN: The BOX's outline (not the ink) — to see transparent margins.
                    using var boxPen = new Pen(Color.FromArgb(160, 40, 200, 80), 1);
                    g.DrawRectangle(boxPen, cursorX, destY, destWidth - 1, destHeight - 1);

                    // UA: Підпис — бокс(з FBOD)/чорнило(реально непрозорі рядки).
                    // EN: Label — box(from FBOD)/ink(actually opaque rows).
                    g.DrawString($"{ch} box={srcRect.Height} ink={inkHeight}", labelFont, Brushes.Gray, cursorX, baselineY + 4);

                    cursorX += destWidth + LetterGapPx * Zoom;
                }

                // UA: Слово-роздільник (порожній рядок після кожного слова —
                //     тут одне слово на рядок, тож не потрібен, лишено для
                //     майбутнього "кілька слів на рядок").
                // EN: Word separator (blank gap after each word — here it's
                //     one word per row, so unused, left in for a future
                //     "several words per row" layout).
                _ = WordGapPx;
            }
        }

        Directory.CreateDirectory(runOutputDir);
        var outPath = Path.Combine(runOutputDir, $"{label}_{fontBaseName}.png");
        bitmap.Save(outPath, ImageFormat.Png);

        report.Log($"UA: [{label}] {fontBaseName}: реальні запатчені пікселі {TestWords.Length} тестових слів, зум x{Zoom}.");
        report.Log($"EN: [{label}] {fontBaseName}: real patched pixels for {TestWords.Length} test words, zoom x{Zoom}.");
        report.Log($"UA: Червона лінія — спільна база вирівнювання (низ найвищого боксу), НЕ база самої гри (та невідома).");
        report.Log($"EN: Red line — a shared alignment base (bottom of the tallest box), NOT the game's own baseline (that's unknown).");
        report.Log($"UA: Збережено у: {outPath}");
        report.Log($"EN: Saved to: {outPath}");
    }
}
