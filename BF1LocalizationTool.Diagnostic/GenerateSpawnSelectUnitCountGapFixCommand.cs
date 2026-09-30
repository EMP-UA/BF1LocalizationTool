// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateSpawnSelectUnitCountGapFixCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Записує копію ingame.lvl, у якій напис "Кількість бійців" на екрані
//     вибору бійця (`ifs_pc_spawnselect`) опущено на один рядок власного
//     шрифту відносно ванільного положення: з двох кінцевих порожніх
//     рядків оригіналу ("Unit Count: %d\r\n\r\n") над кнопкою лишається
//     один — видимий зазор ≈31 px на 1920×1080. Шрифт і переклад не
//     змінюються. Механізм і повний доказ безпечності —
//     Core/Bf2Widescreen/SpawnSelectUnitCountGapPatchBuilder.cs.
//
//     ЩО ПЕРЕВІРЯЄТЬСЯ ПЕРЕД ЗАПИСОМ І ПІСЛЯ НЬОГО:
//       • структура файлу відповідає проаналізованій (перевірки всередині
//         SpawnSelectUnitCountGapPatchBuilder.BuildPlan — усі 8 інструкцій
//         вікна звіряються побайтово, включно з доказом, що восьма є
//         мертвим дублікатом третьої, і що R23 не змінюється між pc65 і
//         вікном; кидає виняток на будь-яку розбіжність);
//       • розмір вихідного файлу дорівнює вхідному, відрізняється не більше
//         32 байтів;
//       • повторний розбір вихідного файлу показує саме
//         `SUB R36:=R16-R23` і `SUB R35:=R31-R36` на очікуваних pc.
//
//     Реальний скріншот-тест екрана вибору бійця — у "ЯК ПЕРЕВІРЯТИ" нижче.
//
// EN: Writes a copy of ingame.lvl where the "Кількість бійців" label on
//     the unit-selection screen (`ifs_pc_spawnselect`) is lowered by one
//     line of its own font relative to the vanilla position: of the
//     original's two trailing empty lines ("Unit Count: %d\r\n\r\n"), one
//     remains above the button — a visible gap of ≈31 px at 1920×1080. The
//     font and the translation are unchanged. Mechanism and full safety
//     proof — Core/Bf2Widescreen/SpawnSelectUnitCountGapPatchBuilder.cs.
//
//     CHECKED BEFORE AND AFTER WRITING:
//       • the file structure matches the one analyzed (checks live inside
//         SpawnSelectUnitCountGapPatchBuilder.BuildPlan — all 8 window
//         instructions are verified byte-for-byte, including the proof
//         that the eighth is a dead duplicate of the third, and that R23
//         is unchanged between pc65 and the window; throws on any mismatch);
//       • the output size equals the input size, at most 32 bytes differ;
//       • re-parsing the output shows exactly `SUB R36:=R16-R23` and
//         `SUB R35:=R31-R36` at the expected pc.
//
//     A real screenshot test of the unit-selection screen — "HOW TO TEST" below.
// =============================================================================

using System.Security.Cryptography;
using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateSpawnSelectUnitCountGapFixCommand
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

        SpawnSelectUnitCountGapPatchBuilder.Plan plan;
        try
        {
            var root = UcfbReader.ReadFile(inputBytes);
            plan = SpawnSelectUnitCountGapPatchBuilder.BuildPlan(root);
        }
        catch (Exception ex)
        {
            // UA: Не глушимо — показуємо причину й не пишемо файл.
            // EN: Not swallowed — show the reason and write nothing.
            report.Log($"UA: План не складено — {ex.Message}");
            report.Log($"EN: No plan was built — {ex.Message}");
            return null;
        }

        report.Log($"UA: Екран: {SpawnSelectUnitCountGapPatchBuilder.ScreenName}, прототип: {plan.PrototypePath}");
        report.Log($"EN: Screen: {SpawnSelectUnitCountGapPatchBuilder.ScreenName}, prototype: {plan.PrototypePath}");
        report.Log($"UA: pc={SpawnSelectUnitCountGapPatchBuilder.WindowStartPc}, зміщення у файлі 0x{plan.FileOffset:X}, довжина вікна {plan.OldWindow.Length} байт");
        report.Log($"EN: pc={SpawnSelectUnitCountGapPatchBuilder.WindowStartPc}, file offset 0x{plan.FileOffset:X}, window length {plan.OldWindow.Length} bytes");
        report.Log("UA: R35(y тексту) = R31 - (R16 - R23): R31 — y кнопки, R16 — texth (0.20H), R23 — висота рядка шрифту напису + 3. Нижній край блока = R31 + R23; з двох кінцевих порожніх рядків Locl над кнопкою лишається один (≈31 px на 1920×1080).");
        report.Log("EN: R35(text y) = R31 - (R16 - R23): R31 — button y, R16 — texth (0.20H), R23 — the label font's line height + 3. Block bottom edge = R31 + R23; of the Locl string's two trailing empty lines, one remains above the button (≈31 px at 1920×1080).");
        report.Log();

        byte[] outputBytes;
        try
        {
            outputBytes = SpawnSelectUnitCountGapPatchBuilder.Apply(inputBytes, plan);
        }
        catch (Exception ex)
        {
            report.Log($"UA: Патч не застосовано — {ex.Message}");
            report.Log($"EN: Patch not applied — {ex.Message}");
            return null;
        }

        // UA: Перевірка 1 — розмір збігається з вхідним, відрізняється не більше 32 байтів
        //     (8 інструкцій по 4 байти).
        // EN: Check 1 — size unchanged, at most 32 bytes differ (8
        //     instructions of 4 bytes each).
        var diffBytes = 0;
        for (var i = 0; i < inputBytes.Length; i++)
        {
            if (inputBytes[i] != outputBytes[i])
                diffBytes++;
        }

        if (outputBytes.Length != inputBytes.Length || diffBytes > plan.OldWindow.Length)
        {
            report.Log($"UA: ПОМИЛКА перевірки: розмір {outputBytes.Length}/{inputBytes.Length}, " +
                       $"змінених байтів {diffBytes} (макс. дозволено {plan.OldWindow.Length}). Файл не записується.");
            report.Log($"EN: Check FAILED: size {outputBytes.Length}/{inputBytes.Length}, " +
                       $"differing bytes {diffBytes} (max allowed {plan.OldWindow.Length}). No file is written.");
            return null;
        }

        // UA: Перевірка 2 — повторний розбір вихідного файлу підтверджує
        //     SUB R36:=R16-R23 на pc152 і SUB R35:=R31-R36 на pc153 (не
        //     просто "запис не впав з винятком").
        // EN: Check 2 — re-parsing the output confirms SUB R36:=R16-R23 at
        //     pc152 and SUB R35:=R31-R36 at pc153 (not merely "the write
        //     didn't throw").
        try
        {
            var outputRoot = UcfbReader.ReadFile(outputBytes);
            var (scratch, textY) = SpawnSelectUnitCountGapPatchBuilder.ReadCurrentPatch(outputRoot);
            if (scratch.Opcode != LuaOpcode.Sub || scratch.A != SpawnSelectUnitCountGapPatchBuilder.RegisterScratch ||
                scratch.B != SpawnSelectUnitCountGapPatchBuilder.RegisterTextHeight ||
                scratch.C != SpawnSelectUnitCountGapPatchBuilder.RegisterLineHeight ||
                textY.Opcode != LuaOpcode.Sub || textY.A != SpawnSelectUnitCountGapPatchBuilder.RegisterTextY ||
                textY.B != SpawnSelectUnitCountGapPatchBuilder.RegisterButtonY ||
                textY.C != SpawnSelectUnitCountGapPatchBuilder.RegisterScratch)
            {
                report.Log($"UA: ПОМИЛКА перевірки: після запису pc152/153 не відповідають очікуваним " +
                           $"SUB/SUB (знайдено {scratch.Opcode} A={scratch.A} B={scratch.B} C={scratch.C} / " +
                           $"{textY.Opcode} A={textY.A} B={textY.B} C={textY.C}).");
                report.Log($"EN: Check FAILED: after writing, pc152/153 do not match the expected SUB/SUB " +
                           $"(found {scratch.Opcode} A={scratch.A} B={scratch.B} C={scratch.C} / " +
                           $"{textY.Opcode} A={textY.A} B={textY.B} C={textY.C}).");
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
        report.Log("UA: 3) core.lvl має містити переклад \"Кількість бійців: %d\" з тими самими");
        report.Log("UA:    двома кінцевими переносами рядка, що й оригінал \"Unit Count: %d\".");
        report.Log("UA: 4) Відкрити екран вибору бійця і звірити зазор між написом і кнопкою");
        report.Log("UA:    (очікувано ≈31 px на 1920×1080) — для відкритого класу (1 рядок) і");
        report.Log("UA:    для закритого (напис про розблокування + лічильник, 3 рядки).");
        report.Log("UA: 5) Це стосується ЛИШЕ ifs_pc_spawnselect — інші екрани не зачіпаються.");
        report.Log("EN: 1) Back up the current GameData\\data\\_lvl_pc\\ingame.lvl.");
        report.Log("EN: 2) Put this file in its place (renamed to ingame.lvl).");
        report.Log("EN: 3) core.lvl must contain the \"Кількість бійців: %d\" translation with the same");
        report.Log("EN:    two trailing line breaks as the original \"Unit Count: %d\".");
        report.Log("EN: 4) Open the unit-selection screen and check the gap between label and button");
        report.Log("EN:    (expected ≈31 px at 1920×1080) — for an unlocked class (1 line) and for");
        report.Log("EN:    a locked one (unlock message + counter, 3 lines).");
        report.Log("EN: 5) This affects ONLY ifs_pc_spawnselect — other screens are unaffected.");

        return outputPath;
    }

    private static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
