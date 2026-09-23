// =============================================================================
// BF1LocalizationTool.Core — Bf2Movies/MovieSubtitleProbePatcher.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: ДІАГНОСТИЧНИЙ патч конфігу роликів (mcfg у mission.lvl). Не фікс —
//     експеримент, що має відповісти на одне-єдине питання:
//
//         на широкому екрані субтитр ролика НЕ МАЛЮЄТЬСЯ ВЗАГАЛІ —
//         чи малюється, але його не видно?
//
//     ЯК. Одному рядку субтитрів примусово ставиться `початок = 0` і
//     `тривалість = 999` (тобто він мав би висіти протягом усього ролика),
//     а прозорість кольору — на максимум. Після цього:
//       • якщо на 1920x1080 на екрані НЕМАЄ нічого — доведено, що виклику
//         малювання не відбувається (проблема у вимкненні, не в геометрії);
//       • якщо щось з'явилось — проблема у часі/позиції, і тоді її можна
//         лікувати з даних.
//     Контроль на 800x600 показує, що патч узагалі діє: там рядок має
//     висіти весь ролик замість своїх 2.23 секунди.
//
//     ЧОМУ ЦЕ БЕЗПЕЧНО. Змінюються ЛИШЕ значення float у вже наявних
//     записах — жоден розмір не змінюється, структура файлу лишається
//     побайтово тією ж, окрім самих чисел. Тому не потрібні ні
//     перебудова чанків, ні перерахунок зміщень.
//
//     ФОРМАТ ЗАПИСУ (звірено з парсером гри, VA 0x006F6089):
//       DATA = [хеш 0x115BFCB9][3][хеш рядка Locl][початок][тривалість][0]
//     тобто початок лежить за зміщенням +9, тривалість за +13 від початку
//     пайлоада DATA.
// EN: A DIAGNOSTIC patch of the movie configuration (mcfg in mission.lvl).
//     Not a fix — an experiment meant to answer exactly one question:
//
//         on a widescreen display, is the movie subtitle NOT DRAWN AT ALL,
//         or is it drawn but invisible?
//
//     HOW. One subtitle line is forced to `start = 0` and `duration = 999`
//     (so it should stay on screen for the whole movie), and the colour's
//     alpha is set to maximum. After that:
//       • if at 1920x1080 there is NOTHING on screen — it is proven that no
//         draw call happens (the problem is suppression, not geometry);
//       • if something appears — the problem is timing/position, and that
//         can be treated from data.
//     The 800x600 control shows the patch works at all: there the line
//     should stay for the whole movie instead of its 2.23 seconds.
//
//     WHY THIS IS SAFE. ONLY float values inside already-existing records are
//     changed — no size changes at all, the file stays byte-for-byte the same
//     apart from the numbers themselves. So neither chunk rebuilding nor
//     offset recomputation is needed.
//
//     RECORD FORMAT (verified against the game's parser at VA 0x006F6089):
//       DATA = [hash 0x115BFCB9][3][Locl string hash][start][duration][0]
//     so start sits at +9 and duration at +13 from the start of the DATA
//     payload.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Core.Bf2Movies;

public static class MovieSubtitleProbePatcher
{
    // UA: Зміщення полів усередині пайлоада DATA запису "subtitle".
    // EN: Field offsets inside the "subtitle" DATA payload.
    private const int HashAt = 5;
    private const int StartAt = 9;
    private const int DurationAt = 13;

    // UA: Хеш сегмента вступного ролика місії на Mygeeto — саме той ролик,
    //     на якому знято всі 10 контрольних знімків.
    // EN: Segment hash of the Mygeeto mission intro movie — the very movie
    //     all 10 reference screenshots were taken from.
    public const uint MygeetoSegmentHash = 0x7AAE1D61;

    public sealed record ProbeResult(
        uint SegmentHash,
        int PatchedLines,
        float OldStart,
        float OldDuration,
        bool ColourForced);

    // -------------------------------------------------------------------------
    // UA: Застосовує експеримент до вказаного сегмента. Мутує масиви RawData
    //     напряму: розміри не змінюються, тому UcfbWriter запише все як є.
    //     Кидає виняток, якщо сегмента або субтитрів немає — мовчазний
    //     «успіх» без змін був би гіршим за помилку.
    // EN: Applies the experiment to the given segment. Mutates the RawData
    //     arrays directly: sizes never change, so UcfbWriter writes them
    //     as-is. Throws if the segment or its subtitles are missing — a silent
    //     "success" with no changes would be worse than an error.
    // -------------------------------------------------------------------------
    public static ProbeResult Apply(UcfbChunk root, uint segmentHash,
        float forcedStart = 0.0f, float forcedDuration = 999.0f, bool forceOpaqueColour = true)
    {
        var target = FindSegmentBlock(root, segmentHash)
            ?? throw new InvalidOperationException(
                $"UA: Сегмент 0x{segmentHash:X8} із субтитрами не знайдено в mcfg / " +
                $"EN: Segment 0x{segmentHash:X8} with subtitles not found in mcfg");

        var (subtitleChunks, colourChunk) = target;

        var first = subtitleChunks[0].RawData;
        var oldStart = BitConverter.ToSingle(first, StartAt);
        var oldDuration = BitConverter.ToSingle(first, DurationAt);

        // UA: Перший рядок — на весь ролик. Решту зсуваємо за межі
        //     тривалості, щоб вони не перекривали картину експерименту.
        // EN: The first line covers the whole movie. The rest are pushed past
        //     the movie's length so they do not muddy the experiment.
        BitConverter.GetBytes(forcedStart).CopyTo(first, StartAt);
        BitConverter.GetBytes(forcedDuration).CopyTo(first, DurationAt);

        for (var i = 1; i < subtitleChunks.Count; i++)
        {
            var raw = subtitleChunks[i].RawData;
            BitConverter.GetBytes(99999.0f).CopyTo(raw, StartAt);
            BitConverter.GetBytes(0.01f).CopyTo(raw, DurationAt);
        }

        var colourForced = false;
        if (forceOpaqueColour && colourChunk is not null &&
            MovieConfigChunk.TryParseData(colourChunk.RawData, out _, out var vals) && vals.Length >= 4)
        {
            // UA: color(r, g, b, a) — доводимо все до 255.
            // EN: color(r, g, b, a) — bring everything up to 255.
            for (var i = 0; i < 4; i++)
                BitConverter.GetBytes(255.0f).CopyTo(colourChunk.RawData, 5 + 4 * i);
            colourForced = true;
        }

        return new ProbeResult(segmentHash, subtitleChunks.Count, oldStart, oldDuration, colourForced);
    }

    // -------------------------------------------------------------------------
    // UA: Знаходить у дереві блок потрібного сегмента: список його
    //     "subtitle"-чанків і чанк "color" того ж блоку. Обхід повторює
    //     логіку MovieConfigChunk: директиви належать блоку, в якому стоять,
    //     а новий "segment" починає новий блок.
    // EN: Finds the block of the requested segment in the tree: the list of
    //     its "subtitle" chunks and that block's "color" chunk. The walk
    //     mirrors MovieConfigChunk: directives belong to the block they
    //     appear in, and a new "segment" starts a new block.
    // -------------------------------------------------------------------------
    private static (List<UcfbChunk> Subtitles, UcfbChunk? Colour)? FindSegmentBlock(
        UcfbChunk root, uint segmentHash)
    {
        foreach (var mcfg in UcfbReader.FindAll(root, "mcfg"))
        {
            uint current = 0;
            UcfbChunk? colour = null;
            var lines = new List<UcfbChunk>();
            (List<UcfbChunk>, UcfbChunk?)? found = null;

            void Walk(UcfbChunk node)
            {
                foreach (var child in node.Children)
                {
                    if (found is not null)
                        return;

                    if (child.FourCC == "DATA" &&
                        MovieConfigChunk.TryParseData(child.RawData, out var hash, out var vals))
                    {
                        switch (hash)
                        {
                            case MovieConfigDirectives.Segment when vals.Length > 0:
                                if (current == segmentHash && lines.Count > 0)
                                {
                                    found = (lines, colour);
                                    return;
                                }
                                current = vals[0].Raw;
                                colour = null;
                                lines = [];
                                break;

                            case MovieConfigDirectives.Color:
                                colour = child;
                                break;

                            case MovieConfigDirectives.Subtitle:
                                lines.Add(child);
                                break;
                        }
                    }
                    else if (child.FourCC == "SCOP")
                    {
                        Walk(child);
                    }
                }
            }

            Walk(mcfg);

            if (found is null && current == segmentHash && lines.Count > 0)
                found = (lines, colour);

            if (found is not null)
                return found;
        }

        return null;
    }
}
