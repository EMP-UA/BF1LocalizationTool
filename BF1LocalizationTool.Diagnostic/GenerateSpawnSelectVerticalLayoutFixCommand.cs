// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateSpawnSelectVerticalLayoutFixCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Записує копію ingame.lvl, у якій на екрані вибору бійця
//     (`ifs_pc_spawnselect`) кнопку "Спавн" разом із написом
//     "Кількість бійців" опущено: y кнопки 0.90×safeH -> 0.95×safeH.
//     Механізм і доказ безпечності —
//     Core/Bf2Widescreen/SpawnSelectVerticalLayoutPatchBuilder.cs.
//
//     ЩО ПЕРЕВІРЯЄТЬСЯ ПЕРЕД ЗАПИСОМ І ПІСЛЯ НЬОГО:
//       • структура файлу відповідає проаналізованій (перевірки всередині
//         SpawnSelectVerticalLayoutPatchBuilder.BuildPlan; кидає виняток на
//         будь-яку розбіжність);
//       • розмір вихідного файлу дорівнює вхідному, відрізняється не більше
//         байтів, ніж охоплюють точки плану;
//       • повторний розбір вихідного файлу показує нову частку safeH.
//
// EN: Writes a copy of ingame.lvl where, on the unit-selection screen
//     (`ifs_pc_spawnselect`), the "Спавн" button together with the
//     "Кількість бійців" label is lowered: button y 0.90×safeH ->
//     0.95×safeH. Mechanism and safety proof —
//     Core/Bf2Widescreen/SpawnSelectVerticalLayoutPatchBuilder.cs.
//
//     CHECKED BEFORE AND AFTER WRITING:
//       • the file structure matches the one analyzed (checks live inside
//         SpawnSelectVerticalLayoutPatchBuilder.BuildPlan; throws on any
//         mismatch);
//       • the output size equals the input size, no more bytes differ than
//         the plan's sites cover;
//       • re-parsing the output shows the new safeH fraction.
// =============================================================================

using System.Security.Cryptography;
using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateSpawnSelectVerticalLayoutFixCommand
{
    public static string? Run(DiagnosticReport report, string ingameLvlPath, string outputDir, string outputFileName)
    {
        if (!File.Exists(ingameLvlPath))
        {
            report.Log($"UA: ingame.lvl не знайдено: \"{ingameLvlPath}\".");
            report.Log($"EN: ingame.lvl not found: \"{ingameLvlPath}\".");
            return null;
        }

        var inputBytes = File.ReadAllBytes(ingameLvlPath);
        report.Log($"UA: Вхід / EN: input: \"{ingameLvlPath}\" ({inputBytes.Length} B)");
        report.Log($"    SHA-256: {Sha256(inputBytes)}");
        report.Log();

        SpawnSelectVerticalLayoutPatchBuilder.Plan plan;
        try
        {
            var root = UcfbReader.ReadFile(inputBytes);
            plan = SpawnSelectVerticalLayoutPatchBuilder.BuildPlan(root);
        }
        catch (Exception ex)
        {
            // UA: Не глушимо — показуємо причину й не пишемо файл.
            // EN: Not swallowed — show the reason and write nothing.
            report.Log($"UA: План не складено — {ex.Message}");
            report.Log($"EN: No plan was built — {ex.Message}");
            return null;
        }

        report.Log($"UA: Екран: {SpawnSelectVerticalLayoutPatchBuilder.ScreenName}");
        report.Log($"EN: Screen: {SpawnSelectVerticalLayoutPatchBuilder.ScreenName}");
        foreach (var site in plan.Sites)
            report.Log($"    {site.Label}: 0x{site.FileOffset:X}, {site.OldBytes.Length} B");
        report.Log($"UA: Кнопка «Спавн» (R31 = safeH - k×safeH): k {SpawnSelectVerticalLayoutPatchBuilder.OldButtonMarginFraction} -> {SpawnSelectVerticalLayoutPatchBuilder.NewButtonMarginFraction}; напис «Кількість бійців» рухається разом із кнопкою.");
        report.Log($"EN: \"Спавн\" button (R31 = safeH - k×safeH): k {SpawnSelectVerticalLayoutPatchBuilder.OldButtonMarginFraction} -> {SpawnSelectVerticalLayoutPatchBuilder.NewButtonMarginFraction}; the \"Кількість бійців\" label moves with the button.");
        report.Log();

        byte[] outputBytes;
        try
        {
            outputBytes = SpawnSelectVerticalLayoutPatchBuilder.Apply(inputBytes, plan);
        }
        catch (Exception ex)
        {
            report.Log($"UA: Патч не застосовано — {ex.Message}");
            report.Log($"EN: Patch not applied — {ex.Message}");
            return null;
        }

        // UA: Перевірка 1 — розмір збігається з вхідним, відрізняється не більше байтів,
        //     ніж сумарна довжина всіх точок плану.
        // EN: Check 1 — size equals the input, no more bytes differ than the
        //     plan's sites cover in total.
        var maxChanged = plan.Sites.Sum(s => s.OldBytes.Length);
        var diffBytes = 0;
        for (var i = 0; i < inputBytes.Length; i++)
        {
            if (inputBytes[i] != outputBytes[i])
                diffBytes++;
        }

        if (outputBytes.Length != inputBytes.Length || diffBytes > maxChanged)
        {
            report.Log($"UA: ПОМИЛКА перевірки: розмір {outputBytes.Length}/{inputBytes.Length}, " +
                       $"змінених байтів {diffBytes} (макс. дозволено {maxChanged}). Файл не записується.");
            report.Log($"EN: Check FAILED: size {outputBytes.Length}/{inputBytes.Length}, " +
                       $"differing bytes {diffBytes} (max allowed {maxChanged}). No file is written.");
            return null;
        }

        // UA: Перевірка 2 — повторний розбір вихідного файлу підтверджує
        //     НОВУ частку safeH (не просто "запис не впав з винятком").
        // EN: Check 2 — re-parsing the output confirms the NEW safeH
        //     fraction (not merely "the write didn't throw").
        try
        {
            var outputRoot = UcfbReader.ReadFile(outputBytes);
            var button = SpawnSelectVerticalLayoutPatchBuilder.ReadCurrentButtonMarginFraction(outputRoot);
            if (button != SpawnSelectVerticalLayoutPatchBuilder.NewButtonMarginFraction)
            {
                report.Log($"UA: ПОМИЛКА перевірки: після запису кнопка k={button}; очікувалось " +
                           $"{SpawnSelectVerticalLayoutPatchBuilder.NewButtonMarginFraction}.");
                report.Log($"EN: Check FAILED: after writing, button k={button}; expected " +
                           $"{SpawnSelectVerticalLayoutPatchBuilder.NewButtonMarginFraction}.");
                return null;
            }
        }
        catch (Exception ex)
        {
            report.Log($"UA: ПОМИЛКА перевірки: повторний розбір вихідного файлу не вдався — {ex.Message}");
            report.Log($"EN: Check FAILED: re-parsing the output failed — {ex.Message}");
            return null;
        }

        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, outputFileName);
        var tempPath = outputPath + ".tmp";
        File.WriteAllBytes(tempPath, outputBytes);
        File.Move(tempPath, outputPath, overwrite: true);

        report.Log($"UA: Записано: \"{outputPath}\" ({outputBytes.Length} B), байтів, що відрізняються від вхідного: {diffBytes}.");
        report.Log($"EN: Written: \"{outputPath}\" ({outputBytes.Length} B), bytes differing from the input: {diffBytes}.");
        report.Log($"    SHA-256: {Sha256(outputBytes)}");
        report.Log();

        report.Log("UA: ЯК ПЕРЕВІРЯТИ / EN: HOW TO TEST");
        report.Log("UA: 1) Резервна копія поточного GameData\\data\\_lvl_pc\\ingame.lvl.");
        report.Log("UA: 2) Покласти цей файл замість нього (перейменувавши на ingame.lvl).");
        report.Log("UA: 3) Екран вибору бійця, відкритий клас: кнопка «Спавн» нижче, зазор між");
        report.Log("UA:    написом «Кількість бійців» і кнопкою такий самий, як без цього кроку.");
        report.Log("UA: 4) Закритий клас (напис про розблокування, 3 рядки): напис цілий, над кнопкою.");
        report.Log("UA: 5) Це стосується ЛИШЕ ifs_pc_spawnselect — інші екрани не зачіпаються.");
        report.Log("EN: 1) Back up the current GameData\\data\\_lvl_pc\\ingame.lvl.");
        report.Log("EN: 2) Put this file in its place (renamed to ingame.lvl).");
        report.Log("EN: 3) Unit-selection screen, unlocked class: the \"Спавн\" button is lower, the");
        report.Log("EN:    gap between the \"Кількість бійців\" label and the button is unchanged.");
        report.Log("EN: 4) Locked class (unlock message, 3 lines): the label is intact, above the button.");
        report.Log("EN: 5) This affects ONLY ifs_pc_spawnselect — other screens are unaffected.");

        return outputPath;
    }

    private static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
