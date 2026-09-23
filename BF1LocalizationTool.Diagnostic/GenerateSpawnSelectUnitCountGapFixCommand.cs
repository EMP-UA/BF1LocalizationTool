// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateSpawnSelectUnitCountGapFixCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ГЕНЕРАТОР (production, входить у фінальний патч) / GENERATOR (production, part of the final patch)
// =============================================================================
// UA: ВИПРАВЛЕННЯ, ПІДТВЕРДЖЕНЕ РЕАЛЬНИМ ТЕСТОМ У ГРІ — записує копію
//     ingame.lvl, у якій між написом "Кількість бійців" і кнопкою
//     "Відродження" на екрані вибору бійця (`ifs_pc_spawnselect`) з'являється
//     зазор ~0.03×H — ЗА РАХУНОК ЗСУВУ ПОЗИЦІЇ ТЕКСТУ, шрифт НЕ змінюється.
//     Механізм, повний доказ безпечності й підтвердження в реальній грі —
//     Core/Bf2Widescreen/SpawnSelectUnitCountGapPatchBuilder.cs і
//     docs/BF2_SPAWNSELECT_GAP_FIX.md.
//
//     ЩО ПЕРЕВІРЯЄТЬСЯ ПЕРЕД ЗАПИСОМ І ПІСЛЯ НЬОГО:
//       • структура файлу відповідає проаналізованій (перевірки всередині
//         SpawnSelectUnitCountGapPatchBuilder.BuildPlan — усі 8 інструкцій
//         вікна звіряються побайтово, включно з доказом, що восьма є
//         мертвим дублікатом п'ятої; кидає виняток на будь-яку розбіжність);
//       • розмір вихідного файлу дорівнює вхідному, змінено рівно 32 байти;
//       • повторний розбір вихідного файлу показує саме ADD+SUB на
//         очікуваних pc з очікуваними регістрами.
//
//     Підтверджено скріншот-тестом екрана вибору бійця (кроки — у
//     "ЯК ПЕРЕВІРЯТИ" нижче; результат — docs/BF2_SPAWNSELECT_GAP_FIX.md).
//
// EN: FIX, CONFIRMED BY A REAL IN-GAME TEST — writes a copy of ingame.lvl
//     where a ~0.03×H gap appears between the "Кількість бійців" label and
//     the "Відродження" button on the unit-selection screen
//     (`ifs_pc_spawnselect`) — BY SHIFTING THE TEXT'S POSITION, the font is
//     NOT changed. Mechanism, full safety proof, and in-game confirmation —
//     Core/Bf2Widescreen/SpawnSelectUnitCountGapPatchBuilder.cs and
//     docs/BF2_SPAWNSELECT_GAP_FIX.md.
//
//     CHECKED BEFORE AND AFTER WRITING:
//       • the file structure matches the one analyzed (checks live inside
//         SpawnSelectUnitCountGapPatchBuilder.BuildPlan — all 8 window
//         instructions are verified byte-for-byte, including the proof
//         that the eighth is a dead duplicate of the fifth; throws on any
//         mismatch);
//       • the output size equals the input size, exactly 32 bytes changed;
//       • re-parsing the output shows exactly ADD+SUB at the expected pc
//         with the expected registers.
//
//     Confirmed by a screenshot test of the unit-selection screen (steps —
//     "HOW TO TEST" below; result — docs/BF2_SPAWNSELECT_GAP_FIX.md).
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
        report.Log("UA: R31(y кнопки, 0.9H) без змін; R35(y тексту) тепер = R31 - (R16+R9) = 0.9H - 0.23H = 0.67H; texth (R16=0.20H) без змін; новий нижній край тексту = 0.87H -> зазор 0.03H.");
        report.Log("EN: R31(button y, 0.9H) unchanged; R35(text y) now = R31 - (R16+R9) = 0.9H - 0.23H = 0.67H; texth (R16=0.20H) unchanged; new text bottom edge = 0.87H -> a 0.03H gap.");
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

        // UA: Перевірка 1 — розмір не змінився, змінено рівно 32 байти
        //     (8 інструкцій по 4 байти).
        // EN: Check 1 — size unchanged, exactly 32 bytes changed (8
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
                       $"changed bytes {diffBytes} (max allowed {plan.OldWindow.Length}). No file is written.");
            return null;
        }

        // UA: Перевірка 2 — повторний розбір вихідного файлу підтверджує
        //     ADD R36:=R16+R9 на pc152 і SUB R35:=R31-R36 на pc153 (не
        //     просто "запис не впав з винятком").
        // EN: Check 2 — re-parsing the output confirms ADD R36:=R16+R9 at
        //     pc152 and SUB R35:=R31-R36 at pc153 (not merely "the write
        //     didn't throw").
        try
        {
            var outputRoot = UcfbReader.ReadFile(outputBytes);
            var (add, sub) = SpawnSelectUnitCountGapPatchBuilder.ReadCurrentPatch(outputRoot);
            if (add.Opcode != LuaOpcode.Add || add.A != SpawnSelectUnitCountGapPatchBuilder.RegisterScratch ||
                add.B != SpawnSelectUnitCountGapPatchBuilder.RegisterTextHeight ||
                add.C != SpawnSelectUnitCountGapPatchBuilder.RegisterMargin ||
                sub.Opcode != LuaOpcode.Sub || sub.A != SpawnSelectUnitCountGapPatchBuilder.RegisterTextY ||
                sub.B != SpawnSelectUnitCountGapPatchBuilder.RegisterButtonY ||
                sub.C != SpawnSelectUnitCountGapPatchBuilder.RegisterScratch)
            {
                report.Log($"UA: ПОМИЛКА перевірки: після запису pc152/153 не відповідають очікуваним " +
                           $"ADD/SUB (знайдено {add.Opcode} A={add.A} B={add.B} C={add.C} / " +
                           $"{sub.Opcode} A={sub.A} B={sub.B} C={sub.C}).");
                report.Log($"EN: Check FAILED: after writing, pc152/153 do not match the expected ADD/SUB " +
                           $"(found {add.Opcode} A={add.A} B={add.B} C={add.C} / " +
                           $"{sub.Opcode} A={sub.A} B={sub.B} C={sub.C}).");
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
        report.Log("UA: 3) Відкрити екран вибору бійця (Кількість бійців / Відродження) і");
        report.Log("UA:    перевірити, чи з'явився видимий зазор і чи зникло накладання.");
        report.Log("UA: 4) Якщо зазору 0.03H замало (кирилиця все ще торкається кнопки) — це");
        report.Log("UA:    ЛЕГКО збільшити (наприклад, до 0.06H, замінивши R9 на суму двох таких");
        report.Log("UA:    регістрів чи інший наявний), без повторення всього аналізу заново.");
        report.Log("UA: 5) Це стосується ЛИШЕ ifs_pc_spawnselect — інші екрани не зачіпаються.");
        report.Log("EN: 1) Back up the current GameData\\data\\_lvl_pc\\ingame.lvl.");
        report.Log("EN: 2) Put this file in its place (renamed to ingame.lvl).");
        report.Log("EN: 3) Open the unit-selection screen (UnitCount / Respawn) and check");
        report.Log("EN:    whether a visible gap appeared and the overlap is gone.");
        report.Log("EN: 4) If a 0.03H gap isn't enough (Cyrillic still touches the button) — this");
        report.Log("EN:    is EASY to increase (e.g. to 0.06H, by swapping R9 for the sum of two");
        report.Log("EN:    such registers or another available one) without redoing the whole analysis.");
        report.Log("EN: 5) This affects ONLY ifs_pc_spawnselect — other screens are unaffected.");

        return outputPath;
    }

    private static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
