// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateFreeformInfoDescriptionFontFixCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: СТАТУС: НЕ ПРОТЕСТОВАНО (потрібен скріншот 2-3 екранів
//     Галактичного завоювання, щоб підтвердити: текст у нижньому
//     інформаційному блоці справді більший і влазить без
//     переносу/обрізання — див. "ЯК ПЕРЕВІРЯТИ" нижче) —
//     записує копію shell.lvl, у якій опис у нижньому інформаційному блоці
//     ("info"-панелі) екранів "Галактичного завоювання" переведено з
//     шрифту `gamefont_tiny` на `gamefont_small` — ЗБІЛЬШЕННЯ, не
//     зменшення, бо в панелі є вільне місце. Механізм, повний доказ і перелік
//     екранів, які це виправляє (спільна функція
//     `ifs_freeform_AddCommonElements`) — Core/Bf2Widescreen/
//     FreeformInfoDescriptionFontPatchBuilder.cs.
//
//     ЩО ПЕРЕВІРЯЄТЬСЯ ПЕРЕД ЗАПИСОМ І ПІСЛЯ НЬОГО:
//       • структура файлу відповідає проаналізованій (перевірки всередині
//         FreeformInfoDescriptionFontPatchBuilder.BuildPlan — прототип
//         знаходиться за маркерними константами, індекси "font"/
//         "gamefont_tiny"/"gamefont_small" резолвляться динамічно, а не
//         хардкодяться, і знаходиться РІВНО одна відповідна інструкція;
//         кидає виняток на будь-яку розбіжність);
//       • розмір вихідного файлу дорівнює вхідному, змінено НЕ БІЛЬШЕ 4
//         байтів (одне слово інструкції; фактично — 2 байти, бо C-операнд
//         змінюється лише в межах старших бітів свого поля, перевірено
//         емпірично на реальних байтах shell.lvl);
//       • повторний розбір вихідного файлу підтверджує, що на записаному
//         pc тепер справді SETTABLE font:=gamefont_small.
//
//     Це НЕ заява «виправлено». Потрібен реальний
//     скріншот-тест хоча б кількох екранів Галактичного завоювання (список
//     — у "ЯК ПЕРЕВІРЯТИ" нижче).
//
// EN: STATUS: NOT TESTED (needs a screenshot of 2-3 Galactic
//     Conquest screens confirming the description in the lower info
//     panel is actually bigger and fits without wrapping/clipping —
//     see "HOW TO TEST" below) — writes a
//     copy of shell.lvl where the description in the lower info panel of
//     the "Galactic Conquest" screens is switched from the `gamefont_tiny`
//     font to `gamefont_small` — an ENLARGEMENT, not a shrink, since the
//     panel has spare room. Mechanism, full proof, and the list of screens this
//     fixes (the shared `ifs_freeform_AddCommonElements` function) —
//     Core/Bf2Widescreen/FreeformInfoDescriptionFontPatchBuilder.cs.
//
//     CHECKED BEFORE AND AFTER WRITING:
//       • the file structure matches the one analyzed (checks live inside
//         FreeformInfoDescriptionFontPatchBuilder.BuildPlan — the
//         prototype is located via marker constants, the "font"/
//         "gamefont_tiny"/"gamefont_small" indices are resolved
//         dynamically rather than hardcoded, and exactly one matching
//         instruction is found; throws on any mismatch);
//       • the output size equals the input size, AT MOST 4 bytes changed
//         (one instruction word; empirically only 2 bytes actually differ,
//         since the C operand's change only touches the field's higher
//         bits — confirmed on real shell.lvl bytes);
//       • re-parsing the output confirms that the recorded pc now really
//         holds SETTABLE font:=gamefont_small.
//
//     This is NOT a "fixed" claim. A real screenshot test
//     of at least a few Galactic Conquest screens is required (list —
//     "HOW TO TEST" below).
// =============================================================================

using System.Security.Cryptography;
using BF1LocalizationTool.Core.Bf2Widescreen;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateFreeformInfoDescriptionFontFixCommand
{
    public static string? Run(DiagnosticReport report, string shellLvlPath, string outputDir, string outputFileName)
    {
        if (!File.Exists(shellLvlPath))
        {
            report.Log($"UA: shell.lvl не знайдено: \"{shellLvlPath}\".");
            report.Log($"EN: shell.lvl not found: \"{shellLvlPath}\".");
            return null;
        }

        var inputBytes = File.ReadAllBytes(shellLvlPath);
        report.Log($"UA: Вхід / EN: input: \"{shellLvlPath}\" ({inputBytes.Length} B)");
        report.Log($"    SHA-256: {Sha256(inputBytes)}");
        report.Log();

        FreeformInfoDescriptionFontPatchBuilder.Plan plan;
        try
        {
            var root = UcfbReader.ReadFile(inputBytes);
            plan = FreeformInfoDescriptionFontPatchBuilder.BuildPlan(root);
        }
        catch (Exception ex)
        {
            // UA: Не глушимо — показуємо причину й не пишемо файл.
            // EN: Not swallowed — show the reason and write nothing.
            report.Log($"UA: План не складено — {ex.Message}");
            report.Log($"EN: No plan was built — {ex.Message}");
            return null;
        }

        report.Log($"UA: Скрипт: {FreeformInfoDescriptionFontPatchBuilder.ScreenName}, прототип: {plan.PrototypePath}");
        report.Log($"EN: Script: {FreeformInfoDescriptionFontPatchBuilder.ScreenName}, prototype: {plan.PrototypePath}");
        report.Log($"UA: pc={plan.Pc}, зміщення у файлі 0x{plan.FileOffset:X}, змінюється рівно 1 слово (4 байти)");
        report.Log($"EN: pc={plan.Pc}, file offset 0x{plan.FileOffset:X}, exactly 1 word (4 bytes) is changed");
        report.Log("UA: R21[K23('font')] := K103('gamefont_tiny')  ->  R21[K23('font')] := K90('gamefont_small'); операнди A/B без змін, розмір файлу без змін.");
        report.Log("EN: R21[K23('font')] := K103('gamefont_tiny')  ->  R21[K23('font')] := K90('gamefont_small'); A/B operands unchanged, file size unchanged.");
        report.Log();

        byte[] outputBytes;
        try
        {
            outputBytes = FreeformInfoDescriptionFontPatchBuilder.Apply(inputBytes, plan);
        }
        catch (Exception ex)
        {
            report.Log($"UA: Патч не застосовано — {ex.Message}");
            report.Log($"EN: Patch not applied — {ex.Message}");
            return null;
        }

        // UA: Перевірка 1 — розмір не змінився, змінено НЕ БІЛЬШЕ 4 байтів
        //     (1 інструкція, 4-байтне слово).
        // EN: Check 1 — size unchanged, AT MOST 4 bytes changed (1
        //     instruction, a 4-byte word).
        var diffBytes = 0;
        for (var i = 0; i < inputBytes.Length; i++)
        {
            if (inputBytes[i] != outputBytes[i])
                diffBytes++;
        }

        if (outputBytes.Length != inputBytes.Length || diffBytes > plan.OldWord.Length)
        {
            report.Log($"UA: ПОМИЛКА перевірки: розмір {outputBytes.Length}/{inputBytes.Length}, " +
                       $"змінених байтів {diffBytes} (макс. дозволено {plan.OldWord.Length}). Файл не записується.");
            report.Log($"EN: Check FAILED: size {outputBytes.Length}/{inputBytes.Length}, " +
                       $"changed bytes {diffBytes} (max allowed {plan.OldWord.Length}). No file is written.");
            return null;
        }

        // UA: Перевірка 2 — повторний розбір вихідного файлу підтверджує
        //     SETTABLE font:=gamefont_small саме на очікуваному pc (не
        //     просто "запис не впав з винятком").
        // EN: Check 2 — re-parsing the output confirms SETTABLE
        //     font:=gamefont_small at the expected pc (not merely "the
        //     write didn't throw").
        try
        {
            var outputRoot = UcfbReader.ReadFile(outputBytes);
            var current = FreeformInfoDescriptionFontPatchBuilder.ReadCurrentPatch(outputRoot, plan.Pc);

            var expectedWord = BitConverter.ToUInt32(plan.NewWord, 0);
            var actualWord = ((uint)current.A << 24) | ((uint)(current.B ?? 0) << 15) |
                              ((uint)(current.C ?? 0) << 6) | (uint)current.Opcode;

            if (current.Opcode != LuaOpcode.SetTable || actualWord != expectedWord)
            {
                report.Log($"UA: ПОМИЛКА перевірки: після запису pc={plan.Pc} не відповідає очікуваному " +
                           $"SETTABLE (знайдено {current.Opcode} A={current.A} B={current.B} C={current.C}).");
                report.Log($"EN: Check FAILED: after writing, pc={plan.Pc} does not match the expected " +
                           $"SETTABLE (found {current.Opcode} A={current.A} B={current.B} C={current.C}).");
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
        report.Log("UA: 1) Резервна копія поточного GameData\\data\\_lvl_pc\\shell.lvl.");
        report.Log("UA: 2) Покласти цей файл замість нього (перейменувавши на shell.lvl).");
        report.Log("UA: 3) Відкрити кілька екранів Галактичного завоювання (найм бійців,");
        report.Log("UA:    бонуси, переміщення флоту тощо) і перевірити, чи опис у нижньому");
        report.Log("UA:    інформаційному блоці став більшим і чи влазить без переносу/обрізання.");
        report.Log("UA: 4) Оскільки функція СПІЛЬНА для десятків екранів, одного раунду");
        report.Log("UA:    скріншотів (2-3 різні екрани) достатньо для підтвердження.");
        report.Log("UA: 5) Якщо десь текст не влазить — це стосується ВСІХ екранів одразу");
        report.Log("UA:    (та сама функція), тож рішення (напр. інший шрифт або менші відступи)");
        report.Log("UA:    також буде єдиним для всіх.");
        report.Log("EN: 1) Back up the current GameData\\data\\_lvl_pc\\shell.lvl.");
        report.Log("EN: 2) Put this file in its place (renamed to shell.lvl).");
        report.Log("EN: 3) Open a few Galactic Conquest screens (hiring units, bonuses,");
        report.Log("EN:    fleet moves, etc.) and check whether the description in the lower");
        report.Log("EN:    info panel got bigger and fits without wrapping/clipping.");
        report.Log("EN: 4) Since the function is SHARED across dozens of screens, one round");
        report.Log("EN:    of screenshots (2-3 different screens) is enough to confirm it.");
        report.Log("EN: 5) If the text doesn't fit somewhere — that affects ALL screens at once");
        report.Log("EN:    (the same function), so the remedy (e.g. a different font or smaller");
        report.Log("EN:    margins) will also be a single change for all of them.");

        return outputPath;
    }

    private static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
