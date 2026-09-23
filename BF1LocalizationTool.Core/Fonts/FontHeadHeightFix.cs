// =============================================================================
// BF1LocalizationTool.Core — Fonts/FontHeadHeightFix.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: ФІКС HEAD — виправлення застарілої висоти шрифту (`HEAD[3]`,
//     `fontHeightPx`) у ВЖЕ згенерованому core.lvl, без перегенерації атласу.
//
//     ПРОБЛЕМА. Під час збільшення гліфів (`FontRepacker.Repack`, scale 1.5)
//     `FontHeightPx` копіюється з вихідного шрифту без змін. Тому в
//     українському core.lvl заявлена висота лишилась ванільною
//     (large 22 / medium 19 / small 17 / tiny 13 / super_tiny 13), хоча
//     найвища комірка гліфа (`FBOD`, байт +7) тепер 32 / 27 / 26 / 19 / 19.
//
//     ДЕ РУШІЙ ЧИТАЄ ЦЕ ЧИСЛО (лише читання BattlefrontII.exe):
//       • завантажувач шрифту (VA 0x006DA035) кладе `HEAD[3]` у поле
//         об'єкта шрифту `+0x0E`;
//       • `ScriptCB_GetFontHeight` (VA 0x00581C50) повертає саме це поле —
//         від нього рахуються `texth`/`bgexpandy` Lua-віджетів;
//       • нативний клас тексту: висота рядка (VA 0x006D8110) =
//         round(`+0x0E` × scaleY + 0.5) + leading; прямокутник тексту
//         (VA 0x006D7E80) = кількість рядків × висота рядка — з нього
//         рамка підказки на екрані завантаження (VA 0x00576912).
//       • розмір самого гліфа (VA 0x006D8985) = його природний розмір ×
//         min(1, 1.2 × висота_рядка / (CellHeight − Bearing)). Для всіх
//         гліфів нинішнього українського core.lvl цей множник уже 1.0
//         (найбільше CellHeight − Bearing: 25/22/19/15/15 при межах
//         26.4/22.8/20.4/15.6/15.6), тож підняття `HEAD[3]` лише підвищує
//         межу і розмір гліфів НЕ змінює — змінюються крок рядків,
//         висота текстових прямокутників і значення `ScriptCB_GetFontHeight`.
//
//     ПРАВИЛО ВИБОРУ ЧИСЛА: зберегти той самий
//     запас між заявленою висотою і найвищою коміркою, що й в ОРИГІНАЛЬНОМУ
//     шрифті гри:
//         нова_висота = max(поточна_висота, макс_комірка_цілі + max(0,
//                           ванільна_висота − ванільна_макс_комірка))
//     Для нинішнього core.lvl: 33 / 28 / 26 / 20 / 20. Нічого не
//     підбирається вручну; якщо висота вже виправлена — зміни нульові.
//
//     ЯК ПИШЕТЬСЯ. Файл НЕ пересеріалізується: змінюється рівно один байт
//     на шрифт за зміщенням `HEAD.FileDataOffset + 3`. Перед записом
//     перевіряється, що там лежить очікуване старе значення.
//
//     ЩО ЦЕ НЕ ВИПРАВЛЯЄ. Генератор (`FontRepacker`) і далі копіює
//     `FontHeightPx` без змін — це свідомий вибір, а не недогляд (причина —
//     у коментарі перед `FontRepacker.RepackWithAdditions`: `FontHeightPx`
//     читають 47 місць у 21 скрипті, і сліпе підняття числа в генераторі
//     ризикує зламати геометрію екранів, які зараз коректні, без перевірки
//     кожного з них у грі). ТОМУ: після КОЖНОЇ регенерації core.lvl через
//     `FontRepacker.Repack`/`RepackWithAdditions` висота знову стає
//     застарілою, і цей фікс (`GenerateFontHeadHeightFixCoreCommand`) треба
//     запускати повторно на новому файлі.
//
// EN: HEAD FIX — corrects the stale font height (`HEAD[3]`, `fontHeightPx`)
//     in an ALREADY generated core.lvl, without regenerating the atlas.
//
//     THE PROBLEM. When glyphs are enlarged (`FontRepacker.Repack`, scale
//     1.5) `FontHeightPx` is copied from the source font unchanged. So the
//     Ukrainian core.lvl still declares the vanilla heights
//     (large 22 / medium 19 / small 17 / tiny 13 / super_tiny 13), while
//     the tallest glyph cell (`FBOD`, byte +7) is now 32 / 27 / 26 / 19 / 19.
//
//     WHERE THE ENGINE READS THIS NUMBER (read-only BattlefrontII.exe):
//       • the font loader (VA 0x006DA035) stores `HEAD[3]` in the font
//         object's field `+0x0E`;
//       • `ScriptCB_GetFontHeight` (VA 0x00581C50) returns exactly that
//         field — Lua widgets derive `texth`/`bgexpandy` from it;
//       • the native text class: line height (VA 0x006D8110) =
//         round(`+0x0E` × scaleY + 0.5) + leading; text rectangle
//         (VA 0x006D7E80) = line count × line height — the loading-screen
//         tip frame is built from it (VA 0x00576912).
//       • the glyph's own size (VA 0x006D8985) = its natural size ×
//         min(1, 1.2 × line_height / (CellHeight − Bearing)). For every
//         glyph in the current Ukrainian core.lvl that factor is already
//         1.0 (largest CellHeight − Bearing: 25/22/19/15/15 against limits
//         26.4/22.8/20.4/15.6/15.6), so raising `HEAD[3]` only raises the
//         limit and does NOT change glyph size — what changes is the line
//         pitch, text rectangle heights and `ScriptCB_GetFontHeight`.
//
//     HOW THE NUMBER IS CHOSEN: keep the same
//     margin between the declared height and the tallest cell as the
//     game's ORIGINAL font has:
//         new_height = max(current_height, target_max_cell + max(0,
//                          vanilla_height − vanilla_max_cell))
//     For the current core.lvl: 33 / 28 / 26 / 20 / 20. Nothing is tuned by
//     hand; if the height is already fixed, the change set is empty.
//
//     HOW IT IS WRITTEN. The file is NOT re-serialized: exactly one byte per
//     font changes, at `HEAD.FileDataOffset + 3`. Before writing, the
//     expected old value is checked at that position.
//
//     WHAT THIS DOES NOT FIX. The generator (`FontRepacker`) still copies
//     `FontHeightPx` unchanged — a deliberate choice, not an oversight (see
//     the comment above `FontRepacker.RepackWithAdditions`: `FontHeightPx`
//     is read from 47 call sites across 21 scripts, and blindly raising the
//     number in the generator risks breaking the geometry of screens that
//     are currently correct, without checking each one in-game). SO: after
//     EVERY core.lvl regeneration via `FontRepacker.Repack`/
//     `RepackWithAdditions`, the height goes stale again, and this fix
//     (`GenerateFontHeadHeightFixCoreCommand`) needs to be re-run on the new
//     file.
// =============================================================================

using System.Text;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Core.Fonts;

public static class FontHeadHeightFix
{
    // UA: HEAD = glyphCount(u16) | pageCount(u8) | fontHeightPx(u8) | 00 00.
    // EN: HEAD = glyphCount(u16) | pageCount(u8) | fontHeightPx(u8) | 00 00.
    public const int HeadHeightIndex = 3;

    // UA: Запис FBOD — 24 байти; CellHeight — байт +7 (FONT_FORMAT_SPEC.md).
    // EN: An FBOD record is 24 bytes; CellHeight is byte +7 (FONT_FORMAT_SPEC.md).
    public const int FbodRecordSize = 24;
    public const int FbodCellHeightOffset = 7;

    // UA: Стан одного шрифту у файлі.
    // EN: The state of one font in a file.
    public sealed record FontHeightInfo(
        string Name,
        byte HeadHeight,
        byte MaxCellHeight,
        int GlyphCount,
        long HeadHeightFileOffset);

    // UA: Запланована зміна одного шрифту (Changed == false — нічого не пишемо).
    // EN: The planned change for one font (Changed == false — nothing is written).
    public sealed record FontHeightChange(
        string Name,
        byte OldHeight,
        byte NewHeight,
        byte TargetMaxCell,
        byte ReferenceHeight,
        byte ReferenceMaxCell,
        long FileOffset)
    {
        public bool Changed => OldHeight != NewHeight;
        public int ReferenceMargin => Math.Max(0, ReferenceHeight - ReferenceMaxCell);
    }

    // -------------------------------------------------------------------------
    // UA: Читає висоту HEAD і найвищу комірку FBOD для кожного `font`-чанка.
    //     Кидає виняток на будь-яку структурну невідповідність — мовчазний
    //     пропуск шрифту дав би «успіх» із неповним фіксом.
    // EN: Reads the HEAD height and the tallest FBOD cell for every `font`
    //     chunk. Throws on any structural mismatch — silently skipping a font
    //     would yield a "success" with an incomplete fix.
    // -------------------------------------------------------------------------
    public static IReadOnlyList<FontHeightInfo> ReadFonts(UcfbChunk root)
    {
        var result = new List<FontHeightInfo>();

        foreach (var font in UcfbReader.FindAll(root, "font"))
        {
            var nameChunk = font.Children.FirstOrDefault(c => c.FourCC == "NAME");
            var head = font.Children.FirstOrDefault(c => c.FourCC == "HEAD");
            var fbod = font.Children.FirstOrDefault(c => c.FourCC == "FBOD");
            if (nameChunk is null || head is null || fbod is null)
                continue; // UA: не шрифтовий ресурс гри / EN: not a game font resource

            var name = Encoding.ASCII.GetString(nameChunk.RawData).TrimEnd('\0');

            if (head.RawData.Length < HeadHeightIndex + 1)
                throw new InvalidDataException(
                    $"UA: HEAD шрифту '{name}' коротший за {HeadHeightIndex + 1} байт. / " +
                    $"EN: HEAD of font '{name}' is shorter than {HeadHeightIndex + 1} bytes.");

            if (fbod.RawData.Length == 0 || fbod.RawData.Length % FbodRecordSize != 0)
                throw new InvalidDataException(
                    $"UA: FBOD шрифту '{name}' має розмір {fbod.RawData.Length}, не кратний {FbodRecordSize}. / " +
                    $"EN: FBOD of font '{name}' has size {fbod.RawData.Length}, not a multiple of {FbodRecordSize}.");

            var glyphs = fbod.RawData.Length / FbodRecordSize;
            byte maxCell = 0;
            for (var i = 0; i < glyphs; i++)
            {
                var cell = fbod.RawData[i * FbodRecordSize + FbodCellHeightOffset];
                if (cell > maxCell)
                    maxCell = cell;
            }

            result.Add(new FontHeightInfo(
                name,
                head.RawData[HeadHeightIndex],
                maxCell,
                glyphs,
                head.FileDataOffset + HeadHeightIndex));
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // UA: Правило: той самий запас, що в оригіналі; ніколи не зменшує.
    // EN: Rule: the same margin as the original; never decreases.
    // -------------------------------------------------------------------------
    public static byte DeriveHeight(byte currentHeight, byte targetMaxCell,
        byte referenceHeight, byte referenceMaxCell)
    {
        var margin = Math.Max(0, referenceHeight - referenceMaxCell);
        var derived = targetMaxCell + margin;
        if (derived > byte.MaxValue)
            throw new InvalidDataException(
                $"UA: Обчислена висота {derived} не вміщується в один байт. / " +
                $"EN: The computed height {derived} does not fit into one byte.");

        return (byte)Math.Max(currentHeight, derived);
    }

    // -------------------------------------------------------------------------
    // UA: План змін: для кожного шрифту цілі шукається однойменний шрифт в
    //     оригінальному (ванільному) core.lvl. Немає пари — виняток.
    // EN: The change plan: for every target font a same-named font is looked
    //     up in the original (vanilla) core.lvl. No match — an exception.
    // -------------------------------------------------------------------------
    public static IReadOnlyList<FontHeightChange> Plan(UcfbChunk targetRoot, UcfbChunk referenceRoot)
    {
        var target = ReadFonts(targetRoot);
        var reference = ReadFonts(referenceRoot)
            .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        if (target.Count == 0)
            throw new InvalidDataException(
                "UA: У цільовому файлі не знайдено жодного font-чанка. / " +
                "EN: No font chunk was found in the target file.");

        var plan = new List<FontHeightChange>(target.Count);
        foreach (var font in target)
        {
            if (!reference.TryGetValue(font.Name, out var original))
                throw new InvalidDataException(
                    $"UA: Шрифту '{font.Name}' немає в оригінальному core.lvl — запас не визначити. / " +
                    $"EN: Font '{font.Name}' is missing from the original core.lvl — the margin cannot be determined.");

            var newHeight = DeriveHeight(font.HeadHeight, font.MaxCellHeight,
                original.HeadHeight, original.MaxCellHeight);

            plan.Add(new FontHeightChange(
                font.Name,
                font.HeadHeight,
                newHeight,
                font.MaxCellHeight,
                original.HeadHeight,
                original.MaxCellHeight,
                font.HeadHeightFileOffset));
        }

        return plan;
    }

    // -------------------------------------------------------------------------
    // UA: Застосовує план до КОПІЇ байтів файлу. Перед кожним записом звіряє
    //     старе значення за зміщенням — інакше зміщення вказує не туди.
    // EN: Applies the plan to a COPY of the file bytes. Before each write the
    //     old value at the offset is checked — otherwise the offset is wrong.
    // -------------------------------------------------------------------------
    public static byte[] Apply(byte[] targetBytes, IReadOnlyList<FontHeightChange> plan)
    {
        var output = (byte[])targetBytes.Clone();

        foreach (var change in plan.Where(c => c.Changed))
        {
            if (change.FileOffset < 0 || change.FileOffset >= output.LongLength)
                throw new InvalidDataException(
                    $"UA: Зміщення 0x{change.FileOffset:X} для '{change.Name}' поза файлом. / " +
                    $"EN: Offset 0x{change.FileOffset:X} for '{change.Name}' is outside the file.");

            var actual = output[change.FileOffset];
            if (actual != change.OldHeight)
                throw new InvalidDataException(
                    $"UA: За зміщенням 0x{change.FileOffset:X} ('{change.Name}') очікувалось {change.OldHeight}, а лежить {actual}. / " +
                    $"EN: At offset 0x{change.FileOffset:X} ('{change.Name}') expected {change.OldHeight}, found {actual}.");

            output[change.FileOffset] = change.NewHeight;
        }

        return output;
    }
}
