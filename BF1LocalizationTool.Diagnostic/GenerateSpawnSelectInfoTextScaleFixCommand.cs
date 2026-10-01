// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateSpawnSelectInfoTextScaleFixCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ГЕНЕРАТОР (production, входить у фінальний патч) / GENERATOR (production, part of the final patch)
// =============================================================================
// UA: Записує копію ingame.lvl, у якій на екрані вибору бійців
//     (`ifs_pc_spawnselect`) текст спорядження в комірках класів
//     масштабується ділильником 3.0 (а не 4.0) за будь-якої висоти екрана,
//     коли слотів більше 7 (карти з 8-10 класами, зокрема Mos Eisley,
//     штурм). Механізм і межі —
//     Core/Bf2Widescreen/SpawnSelectInfoTextScalePatchBuilder.cs.
//
//     ЩО ПЕРЕВІРЯЄТЬСЯ ПЕРЕД ЗАПИСОМ І ПІСЛЯ НЬОГО:
//       • структура файлу відповідає проаналізованій (перевірки всередині
//         SpawnSelectInfoTextScalePatchBuilder.BuildPlan; кидає виняток на
//         будь-яку розбіжність);
//       • розмір вихідного файлу дорівнює вхідному, відрізняється не більше
//         байтів, ніж охоплюють точки плану;
//       • повторний розбір вихідного файлу показує зсув переходу на pc244
//         рівним 0.
//
// EN: Writes a copy of ingame.lvl where, on the unit-selection screen
//     (`ifs_pc_spawnselect`), the equipment text in the class cells is
//     scaled with the divisor 3.0 (not 4.0) at any screen height whenever
//     there are more than 7 slots (maps with 8-10 classes, including Mos
//     Eisley, Assault). Mechanism and limits —
//     Core/Bf2Widescreen/SpawnSelectInfoTextScalePatchBuilder.cs.
//
//     CHECKED BEFORE AND AFTER WRITING:
//       • the file structure matches the one analyzed (checks live inside
//         SpawnSelectInfoTextScalePatchBuilder.BuildPlan; throws on any
//         mismatch);
//       • the output size equals the input size, no more bytes differ than
//         the plan's sites cover;
//       • re-parsing the output shows the jump offset at pc244 equal to 0.
// =============================================================================

using System.Security.Cryptography;
using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateSpawnSelectInfoTextScaleFixCommand
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

        SpawnSelectInfoTextScalePatchBuilder.Plan plan;
        try
        {
            var root = UcfbReader.ReadFile(inputBytes);
            plan = SpawnSelectInfoTextScalePatchBuilder.BuildPlan(root);
        }
        catch (Exception ex)
        {
            // UA: Не глушимо — показуємо причину й не пишемо файл.
            // EN: Not swallowed — show the reason and write nothing.
            report.Log($"UA: План не складено — {ex.Message}");
            report.Log($"EN: No plan was built — {ex.Message}");
            return null;
        }

        report.Log($"UA: Екран: {SpawnSelectInfoTextScalePatchBuilder.ScreenName}");
        report.Log($"EN: Screen: {SpawnSelectInfoTextScalePatchBuilder.ScreenName}");
        foreach (var site in plan.Sites)
            report.Log($"    {site.Label}: 0x{site.FileOffset:X}, {site.OldBytes.Length} B");
        report.Log($"UA: fnBuildScreen: pc{SpawnSelectInfoTextScalePatchBuilder.HeightJumpPc} JMP +{SpawnSelectInfoTextScalePatchBuilder.OldHeightJumpOffset} -> JMP +{SpawnSelectInfoTextScalePatchBuilder.NewHeightJumpOffset}: ділильник тексту спорядження {SpawnSelectInfoTextScalePatchBuilder.ReducedDivisor} (замість {SpawnSelectInfoTextScalePatchBuilder.DefaultDivisor}) за будь-якої висоти екрана, коли слотів більше {SpawnSelectInfoTextScalePatchBuilder.SlotCountThreshold}.");
        report.Log($"EN: fnBuildScreen: pc{SpawnSelectInfoTextScalePatchBuilder.HeightJumpPc} JMP +{SpawnSelectInfoTextScalePatchBuilder.OldHeightJumpOffset} -> JMP +{SpawnSelectInfoTextScalePatchBuilder.NewHeightJumpOffset}: equipment text divisor {SpawnSelectInfoTextScalePatchBuilder.ReducedDivisor} (instead of {SpawnSelectInfoTextScalePatchBuilder.DefaultDivisor}) at any screen height whenever there are more than {SpawnSelectInfoTextScalePatchBuilder.SlotCountThreshold} slots.");
        report.Log();

        byte[] outputBytes;
        try
        {
            outputBytes = SpawnSelectInfoTextScalePatchBuilder.Apply(inputBytes, plan);
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
        //     НОВИЙ зсув переходу (не просто "запис не впав з винятком").
        // EN: Check 2 — re-parsing the output confirms the NEW jump offset
        //     (not merely "the write didn't throw").
        try
        {
            var outputRoot = UcfbReader.ReadFile(outputBytes);
            var offset = SpawnSelectInfoTextScalePatchBuilder.ReadCurrentHeightJumpOffset(outputRoot);
            if (offset != SpawnSelectInfoTextScalePatchBuilder.NewHeightJumpOffset)
            {
                report.Log($"UA: ПОМИЛКА перевірки: після запису зсув переходу {offset}; очікувалось " +
                           $"{SpawnSelectInfoTextScalePatchBuilder.NewHeightJumpOffset}.");
                report.Log($"EN: Check FAILED: after writing, the jump offset is {offset}; expected " +
                           $"{SpawnSelectInfoTextScalePatchBuilder.NewHeightJumpOffset}.");
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
        report.Log("UA: 3) Mos Eisley, штурм, екран вибору бійця (9 класів): текст спорядження");
        report.Log("UA:    крупніший (крок рядків ≈15 px замість ≈11 px), літери без стрибків.");
        report.Log("UA: 4) Екран із 7 класами (наприклад, Ki-Adi-Mundi): вигляд без змін.");
        report.Log("UA: 5) Це стосується ЛИШЕ ifs_pc_spawnselect — інші екрани не зачіпаються.");
        report.Log("EN: 1) Back up the current GameData\\data\\_lvl_pc\\ingame.lvl.");
        report.Log("EN: 2) Put this file in its place (renamed to ingame.lvl).");
        report.Log("EN: 3) Mos Eisley, Assault, the unit-selection screen (9 classes): the equipment");
        report.Log("EN:    text is larger (line pitch ≈15 px instead of ≈11 px), no letter jumping.");
        report.Log("EN: 4) A screen with 7 classes (for example, Ki-Adi-Mundi): unchanged.");
        report.Log("EN: 5) This affects ONLY ifs_pc_spawnselect — other screens are unaffected.");

        return outputPath;
    }

    private static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
