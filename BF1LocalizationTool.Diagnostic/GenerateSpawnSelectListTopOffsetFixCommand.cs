// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateSpawnSelectListTopOffsetFixCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ГЕНЕРАТОР (production, входить у фінальний патч) / GENERATOR (production, part of the final patch)
// =============================================================================
// UA: ВИПРАВЛЕННЯ, ПІДТВЕРДЖЕНЕ РЕАЛЬНИМ ТЕСТОМ У ГРІ — записує копію
//     ingame.lvl, у якій перелік класів на екрані вибору бійця
//     (`ifs_pc_spawnselect`) зсунуто на 30 px вниз, щоб зрівняти відступи
//     зверху й знизу за повного (7-коміркового) переліку. Механізм, повний
//     доказ безпечності й підтвердження в реальній грі —
//     Core/Bf2Widescreen/SpawnSelectListTopOffsetPatchBuilder.cs і
//     docs/BF2_SPAWNSELECT_GAP_FIX.md.
//
//     ЩО ПЕРЕВІРЯЄТЬСЯ ПЕРЕД ЗАПИСОМ І ПІСЛЯ НЬОГО:
//       • структура файлу відповідає проаналізованій (перевірки всередині
//         SpawnSelectListTopOffsetPatchBuilder.BuildPlan — опкод, регістри
//         й числове значення константи на pc65, і що ця константа ніде
//         більше в прототипі не використовується; кидає виняток на будь-яку
//         розбіжність);
//       • розмір вихідного файлу дорівнює вхідному, змінено рівно 4 байти;
//       • повторний розбір вихідного файлу показує нове числове значення
//         (45.0) на очікуваному pc.
//
//     Перелік перевірок у грі — у "ЯК ПЕРЕВІРЯТИ" нижче; результат —
//     docs/BF2_SPAWNSELECT_GAP_FIX.md.
//
// EN: FIX, CONFIRMED BY A REAL IN-GAME TEST — writes a copy of ingame.lvl
//     where the class list on the unit-selection screen
//     (`ifs_pc_spawnselect`) is shifted 30 px down, to equalize the top and
//     bottom gaps with a full (7-slot) list. Mechanism, full safety proof
//     and in-game confirmation — Core/Bf2Widescreen/
//     SpawnSelectListTopOffsetPatchBuilder.cs and
//     docs/BF2_SPAWNSELECT_GAP_FIX.md.
//
//     CHECKED BEFORE AND AFTER WRITING:
//       • the file structure matches the one analyzed (checks live inside
//         SpawnSelectListTopOffsetPatchBuilder.BuildPlan — the opcode,
//         registers and the constant's numeric value at pc65, and that
//         this constant is used nowhere else in the prototype; throws on
//         any mismatch);
//       • the output size equals the input size, exactly 4 bytes changed;
//       • re-parsing the output shows the new numeric value (45.0) at the
//         expected pc.
//
//     The in-game check list — "HOW TO TEST" below; result —
//     docs/BF2_SPAWNSELECT_GAP_FIX.md.
// =============================================================================

using System.Security.Cryptography;
using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateSpawnSelectListTopOffsetFixCommand
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

        SpawnSelectListTopOffsetPatchBuilder.Plan plan;
        try
        {
            var root = UcfbReader.ReadFile(inputBytes);
            plan = SpawnSelectListTopOffsetPatchBuilder.BuildPlan(root);
        }
        catch (Exception ex)
        {
            // UA: Не глушимо — показуємо причину й не пишемо файл.
            // EN: Not swallowed — show the reason and write nothing.
            report.Log($"UA: План не складено — {ex.Message}");
            report.Log($"EN: No plan was built — {ex.Message}");
            return null;
        }

        report.Log($"UA: Екран: {SpawnSelectListTopOffsetPatchBuilder.ScreenName}, прототип: {plan.PrototypePath}");
        report.Log($"EN: Screen: {SpawnSelectListTopOffsetPatchBuilder.ScreenName}, prototype: {plan.PrototypePath}");
        report.Log($"UA: pc={SpawnSelectListTopOffsetPatchBuilder.TargetPc}, зміщення у файлі 0x{plan.FileOffset:X}, довжина {plan.OldValueBytes.Length} байти");
        report.Log($"EN: pc={SpawnSelectListTopOffsetPatchBuilder.TargetPc}, file offset 0x{plan.FileOffset:X}, length {plan.OldValueBytes.Length} bytes");
        report.Log($"UA: Базовий відступ зверху (R24 = константа + R23): {SpawnSelectListTopOffsetPatchBuilder.OldConstantValue} -> {SpawnSelectListTopOffsetPatchBuilder.NewConstantValue} (+{SpawnSelectListTopOffsetPatchBuilder.ShiftPx} px); крок і висота сітки, шрифт — без змін.");
        report.Log($"EN: Base top offset (R24 = constant + R23): {SpawnSelectListTopOffsetPatchBuilder.OldConstantValue} -> {SpawnSelectListTopOffsetPatchBuilder.NewConstantValue} (+{SpawnSelectListTopOffsetPatchBuilder.ShiftPx} px); the grid's pitch, height and font are unchanged.");
        report.Log();

        byte[] outputBytes;
        try
        {
            outputBytes = SpawnSelectListTopOffsetPatchBuilder.Apply(inputBytes, plan);
        }
        catch (Exception ex)
        {
            report.Log($"UA: Патч не застосовано — {ex.Message}");
            report.Log($"EN: Patch not applied — {ex.Message}");
            return null;
        }

        // UA: Перевірка 1 — розмір не змінився, змінено рівно 4 байти
        //     (один float).
        // EN: Check 1 — size unchanged, exactly 4 bytes changed (one
        //     float).
        var diffBytes = 0;
        for (var i = 0; i < inputBytes.Length; i++)
        {
            if (inputBytes[i] != outputBytes[i])
                diffBytes++;
        }

        if (outputBytes.Length != inputBytes.Length || diffBytes > plan.OldValueBytes.Length)
        {
            report.Log($"UA: ПОМИЛКА перевірки: розмір {outputBytes.Length}/{inputBytes.Length}, " +
                       $"змінених байтів {diffBytes} (макс. дозволено {plan.OldValueBytes.Length}). Файл не записується.");
            report.Log($"EN: Check FAILED: size {outputBytes.Length}/{inputBytes.Length}, " +
                       $"changed bytes {diffBytes} (max allowed {plan.OldValueBytes.Length}). No file is written.");
            return null;
        }

        // UA: Перевірка 2 — повторний розбір вихідного файлу підтверджує
        //     НОВЕ числове значення на очікуваному pc (не просто "запис не
        //     впав з винятком").
        // EN: Check 2 — re-parsing the output confirms the NEW numeric
        //     value at the expected pc (not merely "the write didn't
        //     throw").
        try
        {
            var outputRoot = UcfbReader.ReadFile(outputBytes);
            var current = SpawnSelectListTopOffsetPatchBuilder.ReadCurrentValue(outputRoot);
            if (current != SpawnSelectListTopOffsetPatchBuilder.NewConstantValue)
            {
                report.Log($"UA: ПОМИЛКА перевірки: після запису pc={SpawnSelectListTopOffsetPatchBuilder.TargetPc} " +
                           $"дає {current}, очікувалось {SpawnSelectListTopOffsetPatchBuilder.NewConstantValue}.");
                report.Log($"EN: Check FAILED: after writing, pc={SpawnSelectListTopOffsetPatchBuilder.TargetPc} " +
                           $"reads {current}, expected {SpawnSelectListTopOffsetPatchBuilder.NewConstantValue}.");
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

        report.Log($"UA: Записано: \"{outputPath}\" ({outputBytes.Length} B), змінено байтів: {diffBytes}.");
        report.Log($"EN: Written: \"{outputPath}\" ({outputBytes.Length} B), bytes changed: {diffBytes}.");
        report.Log($"    SHA-256: {Sha256(outputBytes)}");
        report.Log();

        report.Log("UA: ЯК ПЕРЕВІРЯТИ / EN: HOW TO TEST");
        report.Log("UA: 1) Резервна копія поточного GameData\\data\\_lvl_pc\\ingame.lvl.");
        report.Log("UA: 2) Покласти цей файл замість нього (перейменувавши на ingame.lvl).");
        report.Log("UA:    Вихідний файл = увесь вхідний файл + лише цей зсув: усі патчі, що вже є");
        report.Log("UA:    у вхідному файлі (гачок розкладки, фікс \"Кількість бійців\"), зберігаються.");
        report.Log("UA:    Вхідним має бути той самий ingame.lvl, що стоїть у грі, а не ванільний:");
        report.Log("UA:    ванільний відрізняється ще й чанками моделей та анімацій.");
        report.Log("UA: 3) Відкрити екран вибору бійця з повним переліком (7 і більше комірок,");
        report.Log("UA:    напр. коли стає доступний герой) і звірити, чи відступи зверху й");
        report.Log("UA:    знизу переліку тепер приблизно рівні.");
        report.Log("UA: 4) Перевірити з 1 класом (мінімум) — та сама сітка (щонайменше 7 комірок),");
        report.Log("UA:    зсув однаковий незалежно від кількості найнятих класів.");
        report.Log("UA: 5) Це стосується ЛИШЕ ifs_pc_spawnselect — інші екрани не зачіпаються.");
        report.Log("EN: 1) Back up the current GameData\\data\\_lvl_pc\\ingame.lvl.");
        report.Log("EN: 2) Put this file in its place (renamed to ingame.lvl).");
        report.Log("EN:    The output = the whole input file + only this shift: every patch already");
        report.Log("EN:    in the input (the layout hook, the \"Кількість бійців\" fix) is kept.");
        report.Log("EN:    The input must be the same ingame.lvl the game uses, not the vanilla one:");
        report.Log("EN:    the vanilla file also differs in model and animation chunks.");
        report.Log("EN: 3) Open the unit-selection screen with a full list (7+ slots, e.g.");
        report.Log("EN:    once the hero becomes available) and check whether the list's top");
        report.Log("EN:    and bottom gaps are now roughly equal.");
        report.Log("EN: 4) Check with 1 class (the minimum) too — the same grid (at least 7");
        report.Log("EN:    slots), the shift is the same regardless of how many classes are recruited.");
        report.Log("EN: 5) This affects ONLY ifs_pc_spawnselect — other screens are unaffected.");

        return outputPath;
    }

    private static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
