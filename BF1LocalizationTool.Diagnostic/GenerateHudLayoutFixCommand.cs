// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateHudLayoutFixCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ГЕНЕРАТОР (production, входить у фінальний патч) / GENERATOR (production, part of the final patch)
// =============================================================================
// UA: Записує копію ingame.lvl, у якій у бойовому HUD (`1playerhud`):
//       • таймер під мінімапою (`objectivetimerGROUP`) опущено нижче від
//         круга мінімапи й відцентровано по x відносно нього;
//       • значки прапорів (`player1flags`) опущено нижче від таймера, а
//         проміжки між ними збільшено;
//       • підпис із цифрами відліку «Перемога через» / «Поразка через»
//         (`VictoryTimerGroup`, `DefeatTimerGroup`) опущено нижче від таймера.
//     Механізм і межі — Core/Bf2Widescreen/HudLayoutPatchBuilder.cs.
//
//     ЩО ПЕРЕВІРЯЄТЬСЯ ПЕРЕД ЗАПИСОМ І ПІСЛЯ НЬОГО:
//       • структура файлу відповідає проаналізованій (перевірки всередині
//         HudLayoutPatchBuilder.BuildPlan; кидає виняток на будь-яку
//         розбіжність);
//       • розмір вихідного файлу дорівнює вхідному, відрізняється не більше
//         байтів, ніж охоплюють точки плану;
//       • повторний розбір вихідного файлу показує нові (x, y) кожної правки.
//
// EN: Writes a copy of ingame.lvl where, in the combat HUD (`1playerhud`):
//       • the timer under the minimap (`objectivetimerGROUP`) is lowered
//         below the minimap circle and centred on it horizontally;
//       • the flag icons (`player1flags`) are lowered below the timer and
//         the gaps between them are enlarged;
//       • the label with the countdown digits «Перемога через» / «Поразка
//         через» (`VictoryTimerGroup`, `DefeatTimerGroup`) is lowered below
//         the timer.
//     Mechanism and limits — Core/Bf2Widescreen/HudLayoutPatchBuilder.cs.
//
//     CHECKED BEFORE AND AFTER WRITING:
//       • the file structure matches the one analyzed (checks live inside
//         HudLayoutPatchBuilder.BuildPlan; throws on any mismatch);
//       • the output size equals the input, no more bytes differ than the
//         plan's sites cover;
//       • re-parsing the output shows the new (x, y) of every edit.
// =============================================================================

using System.Security.Cryptography;
using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateHudLayoutFixCommand
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

        HudLayoutPatchBuilder.Plan plan;
        try
        {
            var root = UcfbReader.ReadFile(inputBytes);
            plan = HudLayoutPatchBuilder.BuildPlan(root);
        }
        catch (Exception ex)
        {
            // UA: Не глушимо — показуємо причину й не пишемо файл.
            // EN: Not swallowed — show the reason and write nothing.
            report.Log($"UA: План не складено — {ex.Message}");
            report.Log($"EN: No plan was built — {ex.Message}");
            return null;
        }

        report.Log($"UA: HUD: {HudLayoutPatchBuilder.HudName}");
        report.Log($"EN: HUD: {HudLayoutPatchBuilder.HudName}");
        foreach (var site in plan.Sites)
        {
            var oldValue = BitConverter.ToSingle(site.OldBytes, 0);
            var newValue = BitConverter.ToSingle(site.NewBytes, 0);
            report.Log($"    {site.Label}: 0x{site.FileOffset:X}, {site.OldBytes.Length} B, {oldValue:R} -> {newValue:R}");
        }
        report.Log($"UA: Таймер: y +{HudLayoutPatchBuilder.TimerShiftY}, x +{HudLayoutPatchBuilder.TimerShiftX} (частки екрана); мінімапа не змінюється.");
        report.Log($"EN: Timer: y +{HudLayoutPatchBuilder.TimerShiftY}, x +{HudLayoutPatchBuilder.TimerShiftX} (screen fractions); the minimap is not changed.");
        report.Log($"UA: Підпис і цифри «Перемога/Поразка через»: y +{HudLayoutPatchBuilder.VictoryTimerShiftY}.");
        report.Log($"EN: The «victory/defeat in» label and digits: y +{HudLayoutPatchBuilder.VictoryTimerShiftY}.");
        report.Log($"UA: Значки прапорів: y групи +{HudLayoutPatchBuilder.FlagsGroupShiftY}; y дочірніх віджетів ×{HudLayoutPatchBuilder.FlagsSpacingFactor}.");
        report.Log($"EN: Flag icons: the group's y +{HudLayoutPatchBuilder.FlagsGroupShiftY}; the child widgets' y ×{HudLayoutPatchBuilder.FlagsSpacingFactor}.");
        report.Log();

        byte[] outputBytes;
        try
        {
            outputBytes = HudLayoutPatchBuilder.Apply(inputBytes, plan);
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
        if (outputBytes.Length == inputBytes.Length)
        {
            for (var i = 0; i < inputBytes.Length; i++)
            {
                if (inputBytes[i] != outputBytes[i])
                    diffBytes++;
            }
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
        //     НОВІ (x, y) кожної правки (не просто "запис не впав з винятком").
        // EN: Check 2 — re-parsing the output confirms the NEW (x, y) of every
        //     edit (not merely "the write didn't throw").
        try
        {
            var outputRoot = UcfbReader.ReadFile(outputBytes);
            var mismatch = HudLayoutPatchBuilder.FindMismatchAfterApply(outputRoot);
            if (mismatch is not null)
            {
                report.Log($"UA: ПОМИЛКА перевірки після запису: {mismatch}");
                report.Log($"EN: Check FAILED after writing: {mismatch}");
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
        report.Log("UA: 3) Бій із таймером: між нижнім краєм круга мінімапи й цифрами є видимий");
        report.Log("UA:    зазор (≈ 13 px на 1080p), цифри по центру під колом.");
        report.Log("UA: 4) Режим із прапорами (CTF): значки прапорів нижче від таймера, проміжки");
        report.Log("UA:    між значками більші, значки не накладаються на таймер.");
        report.Log("UA:    Підпис «Перемога через» / «Поразка через» з цифрами відліку стоїть нижче від");
        report.Log("UA:    таймера цілі й не накладається на нього.");
        report.Log("UA: 5) Мінімапа, рахунок команд і решта HUD лишаються на своїх місцях.");
        report.Log("EN: 1) Back up the current GameData\\data\\_lvl_pc\\ingame.lvl.");
        report.Log("EN: 2) Put this file in its place (renamed to ingame.lvl).");
        report.Log("EN: 3) A battle with a timer: there is a visible gap between the bottom of the");
        report.Log("EN:    minimap circle and the digits (≈ 13 px at 1080p), the digits centred under the circle.");
        report.Log("EN: 4) A flag mode (CTF): the flag icons are below the timer, the gaps between");
        report.Log("EN:    the icons are larger, the icons do not overlap the timer.");
        report.Log("EN:    The «victory in» / «defeat in» label with the countdown digits sits below the");
        report.Log("EN:    objective timer and does not overlap it.");
        report.Log("EN: 5) The minimap, the team score and the rest of the HUD stay in place.");

        return outputPath;
    }

    private static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
