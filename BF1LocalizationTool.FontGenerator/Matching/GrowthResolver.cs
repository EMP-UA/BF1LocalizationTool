// =============================================================================
// BF1LocalizationTool.FontGenerator — Matching/GrowthResolver.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Продакшн-версія алгоритму, обкатаного в
//     TargetedGrowthDonorAssignmentPreviewCommand (Diagnostic-проєкт):
//     цільовий (не максимальний) ріст донора до природної пропорції
//     ЙОГО ПРИЗНАЧЕНОЇ літери — але тут, на відміну від діагностики, яка
//     лише ВИЯВЛЯЛА конфлікти (два донори claim'ять ту саму ділянку),
//     конфлікт РОБИТЬСЯ НЕМОЖЛИВИМ ЗА КОНСТРУКЦІЄЮ:
//
//       1. Сортуємо всі 66 призначень за розміром користі (найгірше
//          базове відхилення — першим).
//       2. Обробляємо в цьому порядку: кожен донор росте, звіряючись із
//          картою зайнятості, яка ВЖЕ враховує ріст усіх попередніх у
//          черзі (не статичний "вільно за FBOD", а динамічний "вільно
//          ЗАРАЗ").
//       3. Одразу після росту — застовплюємо нову ділянку в тій самій
//          карті, щоб наступні в черзі бачили її як зайняту.
//
//     Літери з найбільшою потребою мають пріоритет — вони найбільше
//     виграють від росту, тож справедливо, що вони перші.
//
//     Той самий запобіжник "ріст ніколи не погіршує" (Math.Ceiling у
//     розрахунку потрібного росту інколи трохи перевищує ідеальну
//     пропорцію — непомітно на поганих донорах, помітно на вже
//     відмінних) — підтверджено TargetedGrowthDonorAssignmentPreviewCommand:
//     після цього запобіжника жодного регресу на всіх 11 шрифтах обох
//     ігор.
// EN: Production version of the algorithm piloted in
//     TargetedGrowthDonorAssignmentPreviewCommand (Diagnostic project):
//     targeted (not maximal) growth of a donor toward its ASSIGNED
//     letter's natural ratio — but here, unlike the diagnostic which
//     only DETECTED conflicts (two donors claiming the same area), a
//     conflict is MADE IMPOSSIBLE BY CONSTRUCTION:
//
//       1. Sort all 66 assignments by benefit size (worst baseline
//          deviation first).
//       2. Process in that order: each donor grows against an occupancy
//          map that ALREADY accounts for the growth of everyone earlier
//          in the queue (not a static "free per FBOD", but a dynamic
//          "free RIGHT NOW").
//       3. Immediately after growing — claim the new area in that same
//          map, so everyone later in the queue sees it as occupied.
//
//     Letters with the greatest need get priority — they benefit most
//     from growth, so it's fair they go first.
//
//     The same "growth never regresses" safeguard (Math.Ceiling in the
//     needed-growth calculation sometimes slightly overshoots the ideal
//     ratio — unnoticeable on poor donors, noticeable on already-
//     excellent ones) — confirmed by
//     TargetedGrowthDonorAssignmentPreviewCommand: with this safeguard,
//     zero regressions across all 11 fonts in both games.
// =============================================================================

namespace BF1LocalizationTool.FontGenerator.Matching;

public sealed record GlyphRectBounds(int MinX, int MinY, int MaxX, int MaxY)
{
    public int Width => MaxX - MinX;
    public int Height => MaxY - MinY;
}

public sealed record GrownGlyphPlacement(char Character, ushort DonorCode, GlyphRectBounds Rect);

public static class GrowthResolver
{
    // UA: occupied — карта зайнятості ВСІХ реальних гліфів сторінки
    //     (не лише донорів), індексована [x,y], розміром
    //     [textureWidth,textureHeight]. МУТУЄТЬСЯ під час виклику —
    //     передавай робочу копію, якщо потрібна оригінальна карта після.
    //     originalRectsByCode — прямокутники ВСІХ донорів, задіяних у
    //     assignments (з ТІЄЇ Ж сторінки, що описує occupied).
    // EN: occupied — occupancy map of ALL real glyphs on the page (not
    //     just donors), indexed [x,y], sized [textureWidth,textureHeight].
    //     MUTATED during the call — pass a working copy if you need the
    //     original map afterward. originalRectsByCode — rectangles of
    //     ALL donors involved in assignments (from the SAME page that
    //     occupied describes).
    public static List<GrownGlyphPlacement> ResolveTargetedGrowth(
        IReadOnlyList<GlyphAssignment> assignments,
        IReadOnlyDictionary<ushort, GlyphRectBounds> originalRectsByCode,
        bool[,] occupied,
        int textureWidth,
        int textureHeight)
    {
        var result = new List<GrownGlyphPlacement>();

        // UA: Найбільша користь — першою в черзі.
        // EN: Greatest benefit — first in the queue.
        var ordered = assignments
            .OrderByDescending(a => Math.Abs(Math.Log(a.DonorAspectRatio) - Math.Log(a.TargetAspectRatio)))
            .ToList();

        foreach (var a in ordered)
        {
            if (!originalRectsByCode.TryGetValue(a.DonorCode, out var rect))
                continue; // UA: донор не на цій сторінці — обробляється іншим викликом / EN: donor isn't on this page — handled by another call

            var width = rect.Width;
            var height = rect.Height;
            var currentRatio = (double)width / height;

            var freeLeft = 0;
            while (rect.MinX - freeLeft - 1 >= 0 && ColumnFree(occupied, rect.MinX - freeLeft - 1, rect.MinY, rect.MaxY))
                freeLeft++;
            var freeRight = 0;
            while (rect.MaxX + freeRight < textureWidth && ColumnFree(occupied, rect.MaxX + freeRight, rect.MinY, rect.MaxY))
                freeRight++;
            var freeUp = 0;
            while (rect.MinY - freeUp - 1 >= 0 && RowFree(occupied, rect.MinY - freeUp - 1, rect.MinX, rect.MaxX))
                freeUp++;
            var freeDown = 0;
            while (rect.MaxY + freeDown < textureHeight && RowFree(occupied, rect.MaxY + freeDown, rect.MinX, rect.MaxX))
                freeDown++;

            int newMinX = rect.MinX, newMaxX = rect.MaxX, newMinY = rect.MinY, newMaxY = rect.MaxY;

            if (a.TargetAspectRatio > currentRatio)
            {
                var desiredWidth = height * a.TargetAspectRatio;
                var neededGrow = (int)Math.Ceiling(desiredWidth - width);
                var growRight = Math.Min(freeRight, neededGrow);
                var remaining = neededGrow - growRight;
                var growLeft = Math.Min(freeLeft, Math.Max(0, remaining));

                newMaxX = rect.MaxX + growRight;
                newMinX = rect.MinX - growLeft;
            }
            else if (a.TargetAspectRatio < currentRatio)
            {
                var desiredHeight = width / a.TargetAspectRatio;
                var neededGrow = (int)Math.Ceiling(desiredHeight - height);
                var growDown = Math.Min(freeDown, neededGrow);
                var remaining = neededGrow - growDown;
                var growUp = Math.Min(freeUp, Math.Max(0, remaining));

                newMaxY = rect.MaxY + growDown;
                newMinY = rect.MinY - growUp;
            }

            var newWidth = newMaxX - newMinX;
            var newHeight = newMaxY - newMinY;
            var newRatio = (double)newWidth / newHeight;
            var oldDeviation = Math.Abs(Math.Log(currentRatio) - Math.Log(a.TargetAspectRatio));
            var newDeviation = Math.Abs(Math.Log(newRatio) - Math.Log(a.TargetAspectRatio));

            // UA: Запобіжник "ріст ніколи не погіршує" — див. заголовок
            //     файлу.
            // EN: "Growth never regresses" safeguard — see file header.
            if (newDeviation >= oldDeviation)
            {
                newMinX = rect.MinX; newMaxX = rect.MaxX;
                newMinY = rect.MinY; newMaxY = rect.MaxY;
            }

            // UA: Застовплюємо нову ділянку — наступні в черзі побачать
            //     її як зайняту.
            // EN: Claim the new area — everyone later in the queue will
            //     see it as occupied.
            for (var x = newMinX; x < newMaxX; x++)
                for (var y = newMinY; y < newMaxY; y++)
                    occupied[x, y] = true;

            result.Add(new GrownGlyphPlacement(a.Character, a.DonorCode, new GlyphRectBounds(newMinX, newMinY, newMaxX, newMaxY)));
        }

        return result;
    }

    // UA: НОВА МОДЕЛЬ (метрична) — розміщує слот ЦІЛЬОВОГО розміру
    //     (targetW×targetH з GlyphMetricModel), а не "росте під пропорцію".
    //     Причина: після виведення моделі рендеру гри розмір бокса тепер
    //     визначає GlyphMetricModel (висота = CellHeight−Bearing під
    //     природну форму літери), а не довільний ріст. Здебільшого
    //     цільова висота МЕНША за донора (донори 19-30px, цілі 16-22px) —
    //     тоді слот просто СТИСКАЄТЬСЯ (місце не потрібне). Якщо ж донор
    //     МЕНШИЙ за ціль (напр. 'ж', донор 0x88 ≈ 11px, ціль ≈16px) —
    //     РОСТЕМО у вільний простір (та сама карта зайнятості, що вже
    //     враховує попередні в черзі). Якщо місця бракує — беремо
    //     найбільше можливе (клемп); тоді екранна висота трохи менша за
    //     ідеал, але це рідкісний край, а не системна проблема.
    //
    //     Прив'язка: лівий-нижній кут ЦІЛЬОВОГО бокса збігається з
    //     лівим-нижнім кутом донора (низ = базова лінія в атласі; ширина
    //     росте вправо, як і природний напрям тексту). Порядок обробки —
    //     той самий "найбільша користь першою", і кожен застовплений слот
    //     одразу займає місце для наступних.
    // EN: NEW (metric) MODEL — places a slot of a TARGET size (targetW×
    //     targetH from GlyphMetricModel), rather than "growing toward an
    //     aspect ratio". Reason: after deriving the game's render model,
    //     the box size is now decided by GlyphMetricModel (height =
    //     CellHeight−Bearing for the letter's natural shape), not arbitrary
    //     growth. Usually the target height is SMALLER than the donor
    //     (donors 19-30px, targets 16-22px) — then the slot simply SHRINKS
    //     (no space needed). If the donor is SMALLER than the target (e.g.
    //     'ж', donor 0x88 ≈ 11px, target ≈16px) — we GROW into free space
    //     (the same occupancy map that already accounts for earlier ones in
    //     the queue). If space is short — take the largest possible (clamp);
    //     then the on-screen height is a bit under ideal, but that's a rare
    //     edge, not a systemic problem.
    //
    //     Anchor: the TARGET box's bottom-left corner coincides with the
    //     donor's bottom-left (bottom = baseline in the atlas; width grows
    //     to the right, matching text's natural direction). Processing
    //     order is the same "greatest benefit first", and each claimed slot
    //     immediately occupies space for the next.
    public static List<GrownGlyphPlacement> ResolveMetricSlots(
        IReadOnlyList<GlyphAssignment> assignments,
        IReadOnlyDictionary<ushort, GlyphRectBounds> originalRectsByCode,
        IReadOnlyDictionary<ushort, (int TargetWidth, int TargetHeight)> targetsByCode,
        bool[,] occupied,
        int textureWidth,
        int textureHeight)
    {
        var result = new List<GrownGlyphPlacement>();

        // UA: Найбільша потреба в рості (ціль набагато більша за донора) —
        //     першою, щоб їй дістався вільний простір, поки він ще є.
        // EN: The greatest growth need (target much larger than donor) —
        //     first, so it gets free space while it's still available.
        var ordered = assignments
            .Where(a => originalRectsByCode.ContainsKey(a.DonorCode) && targetsByCode.ContainsKey(a.DonorCode))
            .OrderByDescending(a =>
            {
                var rect = originalRectsByCode[a.DonorCode];
                var (tw, th) = targetsByCode[a.DonorCode];
                return Math.Max(0, tw - rect.Width) + Math.Max(0, th - rect.Height);
            })
            .ToList();

        foreach (var a in ordered)
        {
            var rect = originalRectsByCode[a.DonorCode];
            var (targetW, targetH) = targetsByCode[a.DonorCode];

            // UA: Спершу звільняємо власні клітинки донора (їх перезаписуємо
            //     — вони доступні), щоб сканування вільного простору навколо
            //     не спотикалось об самого донора.
            // EN: First free the donor's own cells (we overwrite them — they
            //     are available), so scanning free space around doesn't trip
            //     over the donor itself.
            for (var x = rect.MinX; x < rect.MaxX; x++)
                for (var y = rect.MinY; y < rect.MaxY; y++)
                    occupied[x, y] = false;

            // UA: Низ і лівий край фіксовані на донорі (базова лінія в
            //     атласі). Спочатку по ВИСОТІ: якщо ціль нижча — стискаємо
            //     (низ на місці, верх опускається). Якщо вища — тягнемо
            //     верх угору у вільний простір, скільки є.
            // EN: Bottom and left edges are pinned to the donor (baseline in
            //     the atlas). HEIGHT first: if the target is shorter —
            //     shrink (bottom stays, top moves down). If taller — pull
            //     the top up into free space, as much as available.
            var bottom = rect.MaxY;
            var left = rect.MinX;

            var desiredTop = bottom - targetH;
            int newTop;
            if (desiredTop >= rect.MinY)
            {
                // UA: Ціль не вища за донора — просто стискаємо зверху.
                // EN: Target no taller than the donor — just shrink from top.
                newTop = desiredTop;
            }
            else
            {
                // UA: Ціль вища — тягнемо верх угору у вільний простір над
                //     донором, поки рядок вільний по всій цільовій ширині
                //     (щонайменше по ширині донора).
                // EN: Target taller — pull the top up into free space above
                //     the donor, while the row is free across the target
                //     width (at least the donor width).
                var scanWidth = Math.Max(rect.Width, Math.Min(targetW, textureWidth - left));
                var freeUp = 0;
                while (rect.MinY - freeUp - 1 >= 0 &&
                       RowFree(occupied, rect.MinY - freeUp - 1, left, left + scanWidth))
                    freeUp++;
                newTop = rect.MinY - Math.Min(freeUp, rect.MinY - desiredTop);
            }

            // UA: Тепер по ШИРИНІ: якщо ціль ширша — тягнемо правий край
            //     вправо у вільний простір (текст іде вправо), скільки є;
            //     якщо вужча — стискаємо праворуч. Лівий край не рухаємо.
            // EN: Now WIDTH: if the target is wider — pull the right edge
            //     right into free space (text flows right), as much as
            //     available; if narrower — shrink from the right. The left
            //     edge stays.
            var newRight = left + targetW;
            if (newRight > rect.MaxX)
            {
                var freeRight = 0;
                while (rect.MaxX + freeRight < textureWidth &&
                       ColumnFree(occupied, rect.MaxX + freeRight, newTop, bottom))
                    freeRight++;
                newRight = rect.MaxX + Math.Min(freeRight, newRight - rect.MaxX);
            }

            var placedRect = new GlyphRectBounds(left, newTop, newRight, bottom);

            // UA: Застовплюємо фактичний слот (і власні донорські клітинки
            //     назад, якщо ціль виявилась меншою за донора) — наступні в
            //     черзі бачитимуть це як зайняте.
            // EN: Claim the actual slot (and the donor's own cells back if
            //     the target ended up smaller than the donor) — the next in
            //     the queue will see it as occupied.
            for (var x = rect.MinX; x < rect.MaxX; x++)
                for (var y = rect.MinY; y < rect.MaxY; y++)
                    occupied[x, y] = true;
            for (var x = placedRect.MinX; x < placedRect.MaxX; x++)
                for (var y = placedRect.MinY; y < placedRect.MaxY; y++)
                    if (x >= 0 && x < textureWidth && y >= 0 && y < textureHeight)
                        occupied[x, y] = true;

            result.Add(new GrownGlyphPlacement(a.Character, a.DonorCode, placedRect));
        }

        return result;
    }

    private static bool ColumnFree(bool[,] occupied, int x, int minY, int maxY)
    {
        for (var y = minY; y < maxY; y++)
            if (occupied[x, y]) return false;
        return true;
    }

    private static bool RowFree(bool[,] occupied, int y, int minX, int maxX)
    {
        for (var x = minX; x < maxX; x++)
            if (occupied[x, y]) return false;
        return true;
    }
}
