// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateFontHeadHeightFixCoreCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: ФІКС HEAD. Записує копію ВЖЕ згенерованого українського core.lvl, у
//     якій виправлено лише заявлену висоту шрифтів (`HEAD[3]`). Атлас,
//     гліфи й решта файлу — байт-у-байт ті самі. Механізм, адреси в exe і
//     правило вибору числа — BF1LocalizationTool.Core/Fonts/FontHeadHeightFix.cs.
//
//     ЩО ПЕРЕВІРЯЄТЬСЯ ПЕРЕД ЗАПИСОМ І ПІСЛЯ НЬОГО:
//       • розмір вихідного файлу дорівнює вхідному;
//       • кількість змінених байтів дорівнює кількості змінених шрифтів;
//       • повторне читання вихідного файлу дає саме заплановані висоти;
//       • SHA-256 входу й виходу записуються у звіт (походження файлу).
//
//     Це не заява «виправлено». Висота впливає на всі тексти відповідного
//     шрифту, тому після встановлення потрібна повна перевірка екранів
//     (список — у звіті).
//
// EN: HEAD FIX. Writes a copy of an ALREADY generated Ukrainian core.lvl in
//     which only the declared font height (`HEAD[3]`) is corrected. The
//     atlas, glyphs and the rest of the file are byte-for-byte identical.
//     Mechanism, exe addresses and the rule for the number —
//     BF1LocalizationTool.Core/Fonts/FontHeadHeightFix.cs.
//
//     CHECKED BEFORE AND AFTER WRITING:
//       • the output size equals the input size;
//       • the number of changed bytes equals the number of changed fonts;
//       • re-reading the output yields exactly the planned heights;
//       • input and output SHA-256 are written to the report (provenance).
//
//     This is not a "fixed" claim. The height affects every text in the
//     corresponding font, so a full screen check is required after
//     installing (the list is in the report).
// =============================================================================

using System.Security.Cryptography;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateFontHeadHeightFixCoreCommand
{
    public static string? Run(DiagnosticReport report, string targetCorePath, string referenceCorePath,
        string outputDir, string outputFileName)
    {
        if (!File.Exists(targetCorePath))
        {
            report.Log($"UA: Цільовий core.lvl не знайдено: \"{targetCorePath}\".");
            report.Log($"EN: Target core.lvl not found: \"{targetCorePath}\".");
            return null;
        }

        if (!File.Exists(referenceCorePath))
        {
            report.Log($"UA: Оригінальний (ванільний) core.lvl не знайдено: \"{referenceCorePath}\".");
            report.Log($"EN: Original (vanilla) core.lvl not found: \"{referenceCorePath}\".");
            return null;
        }

        if (Path.GetFullPath(targetCorePath).Equals(Path.GetFullPath(referenceCorePath),
                StringComparison.OrdinalIgnoreCase))
        {
            report.Log("UA: Цільовий і оригінальний файл — один і той самий. Зупинено.");
            report.Log("EN: The target and the original are the same file. Stopped.");
            return null;
        }

        var targetBytes = File.ReadAllBytes(targetCorePath);
        var referenceBytes = File.ReadAllBytes(referenceCorePath);

        report.Log($"UA: Ціль / EN: target   : \"{targetCorePath}\" ({targetBytes.Length} B)");
        report.Log($"    SHA-256: {Sha256(targetBytes)}");
        report.Log($"UA: Оригінал / EN: vanilla: \"{referenceCorePath}\" ({referenceBytes.Length} B)");
        report.Log($"    SHA-256: {Sha256(referenceBytes)}");
        report.Log();

        IReadOnlyList<FontHeadHeightFix.FontHeightChange> plan;
        try
        {
            plan = FontHeadHeightFix.Plan(UcfbReader.ReadFile(targetBytes), UcfbReader.ReadFile(referenceBytes));
        }
        catch (Exception ex)
        {
            // UA: Не глушимо — показуємо причину й не пишемо файл.
            // EN: Not swallowed — show the reason and write nothing.
            report.Log($"UA: План не складено — {ex.Message}");
            report.Log($"EN: No plan was built — {ex.Message}");
            return null;
        }

        report.Log("UA: шрифт | ваніль HEAD/макс.комірка (запас) | ціль HEAD/макс.комірка | нова HEAD | зміщення");
        report.Log("EN: font | vanilla HEAD/max cell (margin) | target HEAD/max cell | new HEAD | offset");
        foreach (var c in plan)
        {
            report.Log($"  {c.Name,-20} {c.ReferenceHeight,3}/{c.ReferenceMaxCell,-3} (+{c.ReferenceMargin})   " +
                       $"{c.OldHeight,3}/{c.TargetMaxCell,-3}   -> {c.NewHeight,3}" +
                       $"{(c.Changed ? "" : "  (без змін / unchanged)")}   0x{c.FileOffset:X}");
        }
        report.Log();

        var changedCount = plan.Count(c => c.Changed);
        if (changedCount == 0)
        {
            report.Log("UA: Висота вже відповідає правилу в усіх шрифтах — файл не записується.");
            report.Log("EN: The height already follows the rule in every font — no file is written.");
            return null;
        }

        byte[] outputBytes;
        try
        {
            outputBytes = FontHeadHeightFix.Apply(targetBytes, plan);
        }
        catch (Exception ex)
        {
            report.Log($"UA: Патч не застосовано — {ex.Message}");
            report.Log($"EN: Patch not applied — {ex.Message}");
            return null;
        }

        // UA: Перевірка 1 — змінено рівно по одному байту на змінений шрифт.
        // EN: Check 1 — exactly one byte changed per changed font.
        var diffBytes = 0;
        for (var i = 0; i < targetBytes.Length; i++)
        {
            if (targetBytes[i] != outputBytes[i])
                diffBytes++;
        }

        if (outputBytes.Length != targetBytes.Length || diffBytes != changedCount)
        {
            report.Log($"UA: ПОМИЛКА перевірки: розмір {outputBytes.Length}/{targetBytes.Length}, " +
                       $"змінених байтів {diffBytes} замість {changedCount}. Файл не записується.");
            report.Log($"EN: Check FAILED: size {outputBytes.Length}/{targetBytes.Length}, " +
                       $"changed bytes {diffBytes} instead of {changedCount}. No file is written.");
            return null;
        }

        // UA: Перевірка 2 — повторне читання дає саме заплановані висоти.
        // EN: Check 2 — re-reading yields exactly the planned heights.
        var reread = FontHeadHeightFix.ReadFonts(UcfbReader.ReadFile(outputBytes))
            .ToDictionary(f => f.Name, f => f.HeadHeight, StringComparer.OrdinalIgnoreCase);
        foreach (var c in plan)
        {
            if (!reread.TryGetValue(c.Name, out var h) || h != c.NewHeight)
            {
                report.Log($"UA: ПОМИЛКА перевірки: '{c.Name}' після запису читається як {h}, очікувалось {c.NewHeight}.");
                report.Log($"EN: Check FAILED: '{c.Name}' reads back as {h}, expected {c.NewHeight}.");
                return null;
            }
        }

        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, outputFileName);
        var tempPath = outputPath + ".tmp";
        File.WriteAllBytes(tempPath, outputBytes);
        File.Move(tempPath, outputPath, overwrite: true);

        report.Log($"UA: Записано: \"{outputPath}\" ({outputBytes.Length} B), змінено байтів: {diffBytes}.");
        report.Log($"EN: Written: \"{outputPath}\" ({outputBytes.Length} B), bytes changed: {diffBytes}.");
        report.Log($"    SHA-256: {Sha256(outputBytes)}");
        report.Log();

        report.Log("UA: ЯК ПЕРЕВІРЯТИ / EN: HOW TO TEST");
        report.Log("UA: 1) Резервна копія поточного GameData\\data\\_lvl_pc\\core.lvl.");
        report.Log("UA: 2) Покласти цей файл замість нього (перейменувавши на core.lvl).");
        report.Log("UA: 3) Переглянути екрани завантаження (підказка, опис місії кампанії) і");
        report.Log("UA:    ПОВТОРНО всі вже виправлені екрани з Bf2LayoutTable.txt: рядки з");
        report.Log("UA:    bgexpandy/leading/bgoffsety (ifs_login ProfileBox, ifs_mp_sessionlist,");
        report.Log("UA:    заголовки й Popup_Tutorial на 13 екранах Галактичного завоювання) —");
        report.Log("UA:    їхні значення підбирались під СТАРУ висоту.");
        report.Log("UA: 4) Окремо: ifs_opt_sound та інші екрани, що зараз виглядають правильно.");
        report.Log("EN: 1) Back up the current GameData\\data\\_lvl_pc\\core.lvl.");
        report.Log("EN: 2) Put this file in its place (renamed to core.lvl).");
        report.Log("EN: 3) Check the loading screens (tip, campaign mission description) and");
        report.Log("EN:    RE-CHECK every screen already fixed via Bf2LayoutTable.txt: rows with");
        report.Log("EN:    bgexpandy/leading/bgoffsety (ifs_login ProfileBox, ifs_mp_sessionlist,");
        report.Log("EN:    the titles and Popup_Tutorial on the 13 Galactic Conquest screens) —");
        report.Log("EN:    their values were tuned for the OLD height.");
        report.Log("EN: 4) Separately: ifs_opt_sound and other screens that currently look right.");

        return outputPath;
    }

    private static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
