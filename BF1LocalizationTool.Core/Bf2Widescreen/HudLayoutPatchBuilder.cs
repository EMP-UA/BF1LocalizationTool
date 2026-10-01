// =============================================================================
// BF1LocalizationTool.Core — Bf2Widescreen/HudLayoutPatchBuilder.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ГЕНЕРАТОР (production, входить у фінальний патч) / GENERATOR (production, part of the final patch)
// =============================================================================
// UA: Розкладка правого верхнього кута бойового HUD (`1playerhud`): таймер
//     під мінімапою, значки прапорів (режими з прапорами) і підпис із
//     цифрами відліку «Перемога через» / «Поразка через».
//
//     ФАКТИ. Бойовий HUD — це DATA-конфіг у чанку верхнього рівня `hud_`
//     (не Lua-байткод). Позиція віджета — властивість 0x934f4e0a з тегом 4:
//     у сирих даних властивості float x лежить за зміщенням 5, float y — за
//     зміщенням 9 (частки ширини й висоти екрана). Мінімапа — `player1map`
//     (x 0.9175, y 0.1109). Таймер під мінімапою — група
//     `objectivetimerGROUP` (x 0.8909, y 0.2192): цифри торкаються нижнього
//     краю круга мінімапи (зазор ≈ 1–2 px на 1920×1080) і стоять лівіше від
//     центра круга (центр цифр ≈ 1742 px, центр круга ≈ 1762 px).
//     Групи `VictoryTimerGroup` і `DefeatTimerGroup` (обидві x 0.9192,
//     y 0.1900) — підпис (`victorytext`/`defeattext`) і цифри зворотного
//     відліку; на 1920×1080 підпис стоїть на y ≈ 292 px, цифри — на
//     y ≈ 300 px, тобто там, куди зсув `objectivetimerGROUP` переносить
//     цифри таймера цілі (y ≈ 316 px), і накладалися б на них. Значки
//     прапорів — група `player1flags` (x 0.9249, y 0.2415) з дочірніми
//     віджетами; їхні y відносно групи задають вертикальний стовпчик значків,
//     що частково накладаються.
//
//     ЗМІНА (лише float-и позицій; розмір файлу не змінюється):
//       • `objectivetimerGROUP`: y += TimerShiftY (0.012 ≈ 13 px на 1080p),
//         x += TimerShiftX (0.0106 ≈ 20 px на 1920 px) — центр цифр
//         збігається з центром круга мінімапи;
//       • `VictoryTimerGroup`, `DefeatTimerGroup`: y += VictoryTimerShiftY
//         (0.045 ≈ 49 px на 1080p) — підпис і цифри відліку стоять нижче
//         від таймера цілі; x не змінюється;
//       • `player1flags`: y групи += FlagsGroupShiftY (0.07 ≈ 76 px на
//         1080p) — значки нижче від таймера;
//       • дочірні віджети `player1flags` з власною позицією: їхній y
//         множиться на FlagsSpacingFactor (1.25) — більші проміжки між
//         значками; x не змінюється.
//
//     РЕЗУЛЬТАТ У ГРІ (1920×1080): цифри таймера цілі по центру під колом
//     мінімапи, зазор ≈ 8 px; значки прапорів нижче від таймера, між парами
//     значків більші проміжки; підпис «Перемога через» і цифри відліку нижче
//     від таймера цілі без накладання.
//
//     ЗАХИСТ. План складається лише якщо: чанк `hud_` з ім'ям 1playerhud
//     існує рівно один; кожна група й кожен віджет існують рівно один
//     раз; безпосередньо після кожного стоїть SCOP; у ньому рівно одна
//     властивість позиції з тегом 4 і довжиною ≥ 17 байт; поточні x й y
//     збігаються з очікуваними бітами. Будь-яка розбіжність — виняток,
//     файл не пишеться. Чанки `2playerhud`/`4playerhud` (розділений екран
//     на PC не реалізований) не змінюються.
//
// EN: The layout of the combat HUD's top-right corner (`1playerhud`): the
//     timer under the minimap, the flag icons (flag modes) and the
//     «Перемога через» / «Поразка через» label with its countdown digits.
//
//     FACTS. The combat HUD is a DATA config in the top-level `hud_` chunk
//     (not Lua bytecode). A widget's position is the property 0x934f4e0a
//     with tag 4: in the property's raw data the float x sits at offset 5
//     and the float y at offset 9 (fractions of the screen width and
//     height). The minimap is `player1map` (x 0.9175, y 0.1109). The timer
//     under the minimap is the group `objectivetimerGROUP` (x 0.8909,
//     y 0.2192): the digits touch the lower edge of the minimap circle
//     (gap ≈ 1–2 px at 1920×1080) and sit left of the circle's centre (the
//     digits' centre ≈ 1742 px, the circle's centre ≈ 1762 px). The groups
//     `VictoryTimerGroup` and `DefeatTimerGroup` (both x 0.9192,
//     y 0.1900) hold a label (`victorytext`/`defeattext`) and the countdown
//     digits; at 1920×1080 the label sits at y ≈ 292 px and the digits at
//     y ≈ 300 px, i.e. where the shift of `objectivetimerGROUP` moves the
//     objective timer's digits (y ≈ 316 px), and would overlap them. The
//     flag icons are the group `player1flags` (x 0.9249, y 0.2415) with
//     child widgets; their y relative to the group forms a vertical column
//     of partly overlapping icons.
//
//     CHANGE (position floats only; the file size does not change):
//       • `objectivetimerGROUP`: y += TimerShiftY (0.012 ≈ 13 px at 1080p),
//         x += TimerShiftX (0.0106 ≈ 20 px at 1920 px) — the digits' centre
//         coincides with the minimap circle's centre;
//       • `VictoryTimerGroup`, `DefeatTimerGroup`: y += VictoryTimerShiftY
//         (0.045 ≈ 49 px at 1080p) — the label and the countdown digits sit
//         below the objective timer; x is not changed;
//       • `player1flags`: the group's y += FlagsGroupShiftY (0.07 ≈ 76 px at
//         1080p) — the icons sit below the timer;
//       • the child widgets of `player1flags` that have their own position:
//         their y is multiplied by FlagsSpacingFactor (1.25) — larger gaps
//         between the icons; x is not changed.
//
//     IN-GAME RESULT (1920×1080): the objective timer's digits are centred
//     under the minimap circle, gap ≈ 8 px; the flag icons sit below the
//     timer with larger gaps between the pairs; the «Перемога через» label
//     and the countdown digits sit below the objective timer without
//     overlap.
//
//     SAFEGUARDS. The plan is built only if: exactly one `hud_` chunk named
//     1playerhud exists; each group and each widget exists exactly once; a
//     SCOP directly follows each; that SCOP holds exactly one position
//     property with tag 4 and length >= 17 bytes; the current x and y match
//     the expected values bit for bit. Any mismatch throws and no file is
//     written. The `2playerhud`/`4playerhud` chunks (split screen is not
//     implemented on PC) are not changed.
// =============================================================================

using System.Text;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Localization;

namespace BF1LocalizationTool.Core.Bf2Widescreen;

public static class HudLayoutPatchBuilder
{
    /// <summary>
    /// UA: Ім'я `hud_`-чанка, який змінюється. / EN: Name of the `hud_` chunk that is changed.
    /// </summary>
    public const string HudName = "1playerhud";

    /// <summary>
    /// UA: Хеш типу віджета "група" (перший DATA кожної групи). /
    /// EN: Hash of the "group" widget type (the first DATA of each group).
    /// </summary>
    public const uint GroupTypeHash = 0x5fb91e8c;

    /// <summary>
    /// UA: Хеш типу віджета "зображення" (значки прапорів). /
    /// EN: Hash of the "image" widget type (the flag icons).
    /// </summary>
    public const uint ImageTypeHash = 0x46544626;

    /// <summary>
    /// UA: Хеш типу віджета "текст" (числа біля значків). /
    /// EN: Hash of the "text" widget type (the numbers next to the icons).
    /// </summary>
    public const uint TextTypeHash = 0xbde64e3e;

    /// <summary>
    /// UA: Хеш властивості позиції (x, y) віджета. /
    /// EN: Hash of the widget's position (x, y) property.
    /// </summary>
    public const uint PositionPropertyHash = 0x934f4e0a;

    /// <summary>
    /// UA: Зміщення x-float від початку RawData властивості позиції: хеш (4) + тег (1). /
    /// EN: Offset of the x float from the start of the position property's RawData: hash (4) + tag (1).
    /// </summary>
    public const int PositionXOffset = 5;

    /// <summary>
    /// UA: Зміщення y-float: хеш (4) + тег (1) + x (4). /
    /// EN: Offset of the y float: hash (4) + tag (1) + x (4).
    /// </summary>
    public const int PositionYOffset = 9;

    /// <summary>
    /// UA: Мінімальна довжина RawData властивості позиції (хеш 4 + тег 1 + x 4 + y 4). /
    /// EN: Minimum RawData length of the position property (hash 4 + tag 1 + x 4 + y 4).
    /// </summary>
    public const int PositionMinLength = 17;

    /// <summary>
    /// UA: Тег числової властивості з float-компонентами. / EN: Tag of the numeric property with float components.
    /// </summary>
    public const byte PositionTag = 4;

    /// <summary>UA: Зсув y таймера (частка висоти екрана). / EN: The timer's y shift (fraction of the screen height).</summary>
    public const float TimerShiftY = 0.012f;

    /// <summary>UA: Зсув x таймера (частка ширини екрана). / EN: The timer's x shift (fraction of the screen width).</summary>
    public const float TimerShiftX = 0.0106f;

    /// <summary>
    /// UA: Зсув y груп `VictoryTimerGroup` і `DefeatTimerGroup` (підпис із цифрами відліку). /
    /// EN: The y shift of the `VictoryTimerGroup` and `DefeatTimerGroup` groups (the label with the countdown digits).
    /// </summary>
    public const float VictoryTimerShiftY = 0.045f;

    /// <summary>UA: Зсув y групи значків прапорів. / EN: The y shift of the flag-icon group.</summary>
    public const float FlagsGroupShiftY = 0.07f;

    /// <summary>UA: Множник y дочірніх віджетів значків прапорів. / EN: The y multiplier of the flag-icon child widgets.</summary>
    public const float FlagsSpacingFactor = 1.25f;

    /// <summary>
    /// UA: Одна правка позиції: група (і, за потреби, її дочірній віджет), очікувані та нові x/y. /
    /// EN: One position edit: a group (and, if needed, its child widget), the expected and new x/y.
    /// </summary>
    public sealed record Edit(string Group, string? Widget, float OldX, float OldY, float NewX, float NewY)
    {
        public string Label => Widget is null ? $"{HudName}/{Group}" : $"{HudName}/{Group}/{Widget}";
    }

    public sealed record PatchSite(
        string Label,
        long FileOffset,
        byte[] OldBytes,
        byte[] NewBytes);

    public sealed record Plan(IReadOnlyList<PatchSite> Sites);

    private static Edit FlagIcon(string widget, float x, float y) =>
        new("player1flags", widget, x, y, x, y * FlagsSpacingFactor);

    /// <summary>
    /// UA: Усі правки (очікувані значення взято з вихідного `1playerhud`). /
    /// EN: All edits (the expected values are taken from the original `1playerhud`).
    /// </summary>
    public static readonly IReadOnlyList<Edit> Edits =
    [
        new("objectivetimerGROUP", null,
            0.8909410238265991f, 0.21921099722385406f,
            0.8909410238265991f + TimerShiftX, 0.21921099722385406f + TimerShiftY),
        new("VictoryTimerGroup", null,
            0.9192439913749695f, 0.18999700248241425f,
            0.9192439913749695f, 0.18999700248241425f + VictoryTimerShiftY),
        new("DefeatTimerGroup", null,
            0.9192439913749695f, 0.18999700248241425f,
            0.9192439913749695f, 0.18999700248241425f + VictoryTimerShiftY),
        new("player1flags", null,
            0.9249470233917236f, 0.2415200024843216f,
            0.9249470233917236f, 0.2415200024843216f + FlagsGroupShiftY),
        FlagIcon("player1_1stperson_hastheflag", 0.0f, 0.1856440007686615f),
        FlagIcon("player1.flag.friend.team.carried", 0.017311999574303627f, -0.0330980010330677f),
        FlagIcon("player1.flag.friend.carried.number", 0.045155998319387436f, 0.008999999612569809f),
        FlagIcon("player1.flag.friend.dropped", -0.007348000071942806f, -0.007379999849945307f),
        FlagIcon("player1.flag.friend.dropped.number", 0.045155998319387436f, 0.08900000154972076f),
        FlagIcon("player1.flag.enemy.carried", 0.0029299999587237835f, 0.11624500155448914f),
        FlagIcon("player1.flag.enemy.team.carried", 0.019843999296426773f, 0.07653199881315231f),
        FlagIcon("player1.flag.enemy.carried.number", 0.045155998319387436f, 0.16899999976158142f),
        FlagIcon("player1.flag.enemy.dropped", -0.00685400003567338f, 0.10397899895906448f),
        FlagIcon("player1.flag.enemy.dropped.number", 0.045155998319387436f, 0.24899999797344208f),
    ];

    // -------------------------------------------------------------------------
    // UA: Складає план: перевіряє структуру й повертає точки заміни.
    // EN: Builds the plan: checks the structure and returns the patch sites.
    // -------------------------------------------------------------------------
    public static Plan BuildPlan(UcfbChunk ingameLvlRoot)
    {
        var hud = FindHud(ingameLvlRoot);
        var sites = new List<PatchSite>();

        foreach (var edit in Edits)
        {
            var position = FindPosition(hud, edit);
            var (x, y) = ReadXY(position);

            if (!SameBits(x, edit.OldX) || !SameBits(y, edit.OldY))
                throw new InvalidDataException(
                    $"UA: {edit.Label}: (x, y) = ({x:R}, {y:R}), очікувалось ({edit.OldX:R}, {edit.OldY:R}) — " +
                    "файл відрізняється від проаналізованого, патч НЕ застосовується. / " +
                    $"EN: {edit.Label}: (x, y) = ({x:R}, {y:R}), expected ({edit.OldX:R}, {edit.OldY:R}) — " +
                    "the file differs from the analyzed one, the patch is NOT applied.");

            if (!SameBits(edit.NewX, edit.OldX))
                sites.Add(new PatchSite(
                    $"{edit.Label} x",
                    position.FileDataOffset + PositionXOffset,
                    BitConverter.GetBytes(edit.OldX),
                    BitConverter.GetBytes(edit.NewX)));

            if (!SameBits(edit.NewY, edit.OldY))
                sites.Add(new PatchSite(
                    $"{edit.Label} y",
                    position.FileDataOffset + PositionYOffset,
                    BitConverter.GetBytes(edit.OldY),
                    BitConverter.GetBytes(edit.NewY)));
        }

        return new Plan(sites);
    }

    // -------------------------------------------------------------------------
    // UA: Застосовує план до копії байтів; перед записом кожної точки
    //     перевіряє, що старі байти лежать на своєму місці.
    // EN: Applies the plan to a copy of the bytes; before writing each site
    //     checks that the old bytes are in place.
    // -------------------------------------------------------------------------
    public static byte[] Apply(byte[] ingameLvlBytes, Plan plan)
    {
        var output = (byte[])ingameLvlBytes.Clone();

        foreach (var site in plan.Sites)
        {
            if (site.FileOffset < 0 || site.FileOffset + site.OldBytes.Length > output.LongLength)
                throw new InvalidDataException(
                    $"UA: {site.Label}: зміщення 0x{site.FileOffset:X} (довжина {site.OldBytes.Length}) поза " +
                    $"файлом (розмір {output.LongLength}). / " +
                    $"EN: {site.Label}: the offset 0x{site.FileOffset:X} (length {site.OldBytes.Length}) is " +
                    $"outside the file (size {output.LongLength}).");

            for (var i = 0; i < site.OldBytes.Length; i++)
            {
                if (output[site.FileOffset + i] != site.OldBytes[i])
                    throw new InvalidDataException(
                        $"UA: {site.Label}: за зміщенням 0x{site.FileOffset + i:X} очікувався байт " +
                        $"0x{site.OldBytes[i]:X2}, а лежить 0x{output[site.FileOffset + i]:X2}. / " +
                        $"EN: {site.Label}: at offset 0x{site.FileOffset + i:X} expected byte " +
                        $"0x{site.OldBytes[i]:X2}, found 0x{output[site.FileOffset + i]:X2}.");
            }

            Array.Copy(site.NewBytes, 0, output, (int)site.FileOffset, site.NewBytes.Length);
        }

        return output;
    }

    // -------------------------------------------------------------------------
    // UA: Повторна перевірка вихідного файлу: повертає null, якщо (x, y) КОЖНОЇ
    //     правки дорівнюють новим значенням, інакше — опис першої розбіжності.
    // EN: Re-verification of the output file: returns null if the (x, y) of
    //     EVERY edit equal the new values, otherwise a description of the
    //     first mismatch.
    // -------------------------------------------------------------------------
    public static string? FindMismatchAfterApply(UcfbChunk ingameLvlRoot)
    {
        var hud = FindHud(ingameLvlRoot);
        foreach (var edit in Edits)
        {
            var (x, y) = ReadXY(FindPosition(hud, edit));
            if (!SameBits(x, edit.NewX) || !SameBits(y, edit.NewY))
                return $"{edit.Label}: (x, y) = ({x:R}, {y:R}); " +
                       $"expected ({edit.NewX:R}, {edit.NewY:R}).";
        }
        return null;
    }

    private static bool SameBits(float a, float b) =>
        BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);

    private static (float X, float Y) ReadXY(UcfbChunk position) =>
        (BitConverter.ToSingle(position.RawData, PositionXOffset),
         BitConverter.ToSingle(position.RawData, PositionYOffset));

    private static UcfbChunk FindHud(UcfbChunk ingameLvlRoot)
    {
        var expectedHash = SwbfResourceNameHash.Compute(HudName);

        var matches = new List<UcfbChunk>();
        foreach (var hud in FindAllByFourCC(ingameLvlRoot, "hud_"))
        {
            var nameChunk = hud.Children.FirstOrDefault(c => c.FourCC == "NAME");
            if (nameChunk is null || nameChunk.RawData.Length < 4)
                continue;
            if (BitConverter.ToUInt32(nameChunk.RawData, 0) == expectedHash)
                matches.Add(hud);
        }

        if (matches.Count != 1)
            throw new InvalidDataException(
                $"UA: чанків hud_ з ім'ям '{HudName}' знайдено {matches.Count}, очікувався рівно 1. / " +
                $"EN: found {matches.Count} hud_ chunks named '{HudName}', expected exactly 1.");
        return matches[0];
    }

    private static IEnumerable<UcfbChunk> FindAllByFourCC(UcfbChunk root, string fourCC)
    {
        if (root.FourCC == fourCC)
            yield return root;
        foreach (var child in root.Children)
            foreach (var found in FindAllByFourCC(child, fourCC))
                yield return found;
    }

    // -------------------------------------------------------------------------
    // UA: Знаходить DATA-властивість позиції для правки: для групи — це
    //     безпосередня дитина SCOP групи; для віджета — безпосередня дитина
    //     SCOP, що стоїть одразу після DATA-імені віджета всередині SCOP групи.
    // EN: Finds the position DATA property for an edit: for a group it is a
    //     direct child of the group's SCOP; for a widget it is a direct
    //     child of the SCOP that directly follows the widget's name DATA
    //     inside the group's SCOP.
    // -------------------------------------------------------------------------
    private static UcfbChunk FindPosition(UcfbChunk hud, Edit edit)
    {
        var groupScope = FindNamedScope(hud, edit.Group, [GroupTypeHash], edit.Label);
        var owner = edit.Widget is null
            ? groupScope
            : FindNamedScope(groupScope, edit.Widget, [ImageTypeHash, TextTypeHash], edit.Label);

        var positions = owner.Children
            .Where(c => c.FourCC == "DATA" && c.RawData.Length >= 5 &&
                        BitConverter.ToUInt32(c.RawData, 0) == PositionPropertyHash)
            .ToList();
        if (positions.Count != 1)
            throw new InvalidDataException(
                $"UA: {edit.Label}: властивостей позиції {positions.Count}, очікувалась рівно 1. / " +
                $"EN: {edit.Label}: {positions.Count} position properties, expected exactly 1.");

        var position = positions[0];
        if (position.RawData[4] != PositionTag || position.RawData.Length < PositionMinLength)
            throw new InvalidDataException(
                $"UA: {edit.Label}: властивість позиції має тег {position.RawData[4]} і довжину " +
                $"{position.RawData.Length}; очікувалось тег {PositionTag}, довжина ≥ {PositionMinLength}. / " +
                $"EN: {edit.Label}: the position property has tag {position.RawData[4]} and length " +
                $"{position.RawData.Length}; expected tag {PositionTag}, length >= {PositionMinLength}.");

        return position;
    }

    // -------------------------------------------------------------------------
    // UA: Шукає серед дітей `parent` рівно один DATA (тег 1, один із типів
    //     `typeHashes`, рядок-ім'я `name`) і повертає SCOP, що стоїть одразу
    //     після нього.
    // EN: Looks among `parent`'s children for exactly one DATA (tag 1, one of
    //     `typeHashes`, name string `name`) and returns the SCOP directly
    //     after it.
    // -------------------------------------------------------------------------
    private static UcfbChunk FindNamedScope(UcfbChunk parent, string name, uint[] typeHashes, string label)
    {
        var indexes = new List<int>();
        for (var i = 0; i < parent.Children.Count; i++)
        {
            var c = parent.Children[i];
            if (c.FourCC != "DATA" || c.RawData.Length < 13 || c.RawData[4] != 1)
                continue;
            if (!typeHashes.Contains(BitConverter.ToUInt32(c.RawData, 0)))
                continue;
            if (ReadName(c.RawData) == name)
                indexes.Add(i);
        }

        if (indexes.Count != 1)
            throw new InvalidDataException(
                $"UA: {label}: '{name}' знайдено {indexes.Count} раз(и), очікувався рівно 1. / " +
                $"EN: {label}: '{name}' was found {indexes.Count} time(s), expected exactly 1.");

        var next = indexes[0] + 1;
        if (next >= parent.Children.Count || parent.Children[next].FourCC != "SCOP")
            throw new InvalidDataException(
                $"UA: {label}: після '{name}' немає SCOP. / " +
                $"EN: {label}: no SCOP follows '{name}'.");

        return parent.Children[next];
    }

    // UA/EN: [hash:u32][tag:u8][count:u32][strLen:u32(+null)][ascii][00] -> ім'я / name.
    private static string ReadName(byte[] raw)
    {
        var strLen = BitConverter.ToUInt32(raw, 9);
        if (strLen == 0 || strLen > raw.Length - 13)
            return string.Empty;
        return Encoding.ASCII.GetString(raw, 13, (int)strLen - 1);
    }
}
