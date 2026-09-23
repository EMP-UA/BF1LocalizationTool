// =============================================================================
// BF1LocalizationTool.Diagnostic — PatchAddOnMapNameCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ГЕНЕРАТОР (production, входить у фінальний патч) / GENERATOR (production, part of the final patch)
// =============================================================================
// UA: Робить назву карти аддону ПЕРЕКЛАДНОЮ.
//
//     ПРОБЛЕМА. Назва карти аддону — єдиний рядок тексту BF1, що живе поза
//     таблицею `Locl`. Вона лежить константою в Lua-байткоді файлу
//     `AddOn\{Аддон}\addme.script`:
//
//         mapluafile = "TAT3"
//         showstr    = "TATOOINE: JABBA"     ← ось вона
//
//     Екран вибору карти малює її через `IFText_fnSetString` — а це
//     ЛОКАЛІЗОВАНИЙ виклик: він хешує передане значення як КЛЮЧ і шукає
//     його в `Locl`. Базові карти передають туди саме ключ (тому в грі
//     видно "ТАТУЇН: МОРЕ ПІСКУ"), а аддон передає готовий англійський
//     рядок — пошук не знаходить нічого й гра показує сам рядок як є.
//     Звідси єдина неперекладена назва в списку карт.
//
//     РІШЕННЯ (два кроки, обидва цією командою):
//       1. У `addme.script` замінити значення `showstr` з "TATOOINE: JABBA"
//          на КЛЮЧ "level.tat3.name".
//       2. У `core.lvl` БАЗОВОЇ гри (саме він резолвить ключі екрана
//          вибору карти — див. CoreTarget нижче) додати НОВИЙ запис
//          `Locl` з хешем `SwbfStringHash.Compute("level.tat3.name")` і
//          англійським значенням "TATOOINE: JABBA". Той самий запис
//          додається і в `core.lvl` аддону — нешкідлива страховка, не
//          обов'язкова умова роботи.
//
//     ЧОМУ значення саме АНГЛІЙСЬКЕ, а не одразу українське: після цієї
//     команди рядок стає ЗВИЧАЙНИМ рядком локалізації й з'являється в GUI
//     як усі інші — його перекладають, вичитують і експортують у CSV тим
//     самим шляхом. Інакше переклад цього одного рядка жив би окремо від
//     решти й неминуче розійшовся б з нею.
//
//     ЧОМУ ЗАМІНА БЕЗПЕЧНА: "TATOOINE: JABBA" і "level.tat3.name" — обидва
//     рівно 15 символів. Тому Lua-префікс довжини (16 = 15+NUL) і всі три
//     поля розмірів контейнера (`BODY`, `scr_`, `ucfb`) лишаються
//     НЕЗМІННИМИ, а патч зводиться до заміни 15 байтів на місці. Якщо
//     колись знадобиться ключ ІНШОЇ довжини — доведеться узгоджено
//     оновити чотири поля розміру; команда це явно перевіряє і
//     відмовляється працювати, а не псує файл мовчки.
//
//     ПІДТВЕРДЖЕНО В ГРІ: те, що `IFText_fnSetString` робить пошук за
//     хешем із фолбеком на сирий рядок, підтверджено реальним тестом —
//     запис лише в аддонному core.lvl примушує гру показати буквально
//     "level.tat3.name" замість перекладу, що й доводить механізм. Той
//     самий тест показав, що резолвиться запис саме з БАЗОВОГО core.lvl,
//     а не з аддонного — див. коментар до CoreTarget нижче.
//
// EN: Makes an add-on's map name TRANSLATABLE.
//
//     PROBLEM. The add-on map name is the only BF1 text string living
//     outside the `Locl` table. It sits as a constant in the Lua bytecode
//     of `AddOn\{AddOn}\addme.script` (`showstr = "TATOOINE: JABBA"`).
//     The map-select screen draws it via `IFText_fnSetString`, which is
//     the LOCALIZED call: it hashes the given value as a KEY and looks it
//     up in `Locl`. Base maps pass a key (hence the translated names in
//     game); the add-on passes a ready English string, the lookup finds
//     nothing, and the game shows the string verbatim.
//
//     SOLUTION (two steps, both done here): rewrite `showstr` to the key
//     "level.tat3.name", and add a NEW `Locl` record under
//     `SwbfStringHash.Compute("level.tat3.name")` holding the English
//     value in the BASE game's core.lvl (it is the one that resolves the
//     map-select screen's keys — see CoreTarget below); the same record is
//     also added to the add-on's core.lvl as a harmless safety net, not a
//     requirement. The value is kept ENGLISH on purpose: the string then becomes
//     an ORDINARY localization row and is translated/reviewed/exported
//     through the GUI and CSV exactly like every other row, instead of
//     living in a separate place and drifting out of sync.
//
//     WHY THE REWRITE IS SAFE: "TATOOINE: JABBA" and "level.tat3.name" are
//     both exactly 15 characters, so the Lua length prefix (16 = 15+NUL)
//     and all three container size fields (`BODY`, `scr_`, `ucfb`) stay
//     UNCHANGED — the patch is a 15-byte in-place replacement. Should a
//     key of a DIFFERENT length ever be needed, four size fields would
//     have to be updated in sync; the command checks this explicitly and
//     refuses rather than silently corrupting the file.
//
//     CONFIRMED IN GAME: that `IFText_fnSetString` performs a hash lookup
//     with a raw-string fallback is confirmed by a real test — a record
//     placed only in the add-on's core.lvl makes the game display
//     "level.tat3.name" literally instead of the translation, which is
//     the proof. The same test showed the record resolves from the BASE
//     core.lvl, not the add-on's — see the CoreTarget comment below.
// =============================================================================

using System.Text;
using BF1LocalizationTool.Core.Localization;
using BF1LocalizationTool.Core.Models;

namespace BF1LocalizationTool.Diagnostic;

public static class PatchAddOnMapNameCommand
{
    // UA: Поточне (ванільне) значення showstr і ключ, яким його замінюємо.
    //     Довжини МАЮТЬ збігатися — див. заголовок.
    // EN: The current (vanilla) showstr value and the key replacing it.
    //     The lengths MUST match — see the header.
    private const string VanillaShowStr = "TATOOINE: JABBA";
    private const string LocalizationKey = "level.tat3.name";

    // UA: coreTargets — файли core.lvl, у які додається новий запис `Locl`.
    //
    //     ⚠ КРИТИЧНО, ПЕРЕВІРЕНО В ГРІ: запис МАЄ лежати в core.lvl
    //     БАЗОВОЇ ГРИ, а не аддону.
    //     Екран вибору карти — це ШЕЛ, і він резолвить ключі проти таблиці
    //     базової гри; core.lvl аддону на той момент ще не є активною
    //     таблицею (він вантажиться разом з місією — саме тому зламаний
    //     аддонний файл гасив текст НА МАПІ, а меню лишалось цілим).
    //     Доказ конвенції: ключі назв базових карт
    //     (`planets.tatooine.mapname1` → "TATOOINE: DUNE SEA") згадуються в
    //     `Shell\ENG\shell.lvl`, а їхні ЗНАЧЕННЯ лежать у базовому
    //     `core.lvl` — знайдено брутфорсом (захешовано всі ASCII-рядки гри).
    //
    //     Запис лише в аддонному core.lvl призводить до того, що гра
    //     показує буквально "level.tat3.name" — це й є доказ, що
    //     `IFText_fnSetString` таки робить пошук і має фолбек на сирий
    //     рядок: механізм правильний, важливий саме адресат.
    // EN: coreTargets — the core.lvl files the new `Locl` record is added to.
    //
    //     ⚠ CRITICAL, VERIFIED IN GAME: the record MUST live in the BASE
    //     GAME's core.lvl,
    //     not the add-on's. The map-select screen is the SHELL, and it
    //     resolves keys against the base game's table; the add-on's core.lvl
    //     is not the active table at that point (it loads with the mission —
    //     which is precisely why a broken add-on file blanked text ON THE
    //     MAP while the menus stayed intact).
    //     Convention proof: the base maps' name keys
    //     (`planets.tatooine.mapname1` → "TATOOINE: DUNE SEA") are referenced
    //     from `Shell\ENG\shell.lvl` while their VALUES sit in the base
    //     `core.lvl` — found by brute force (hashing every ASCII string in
    //     the game).
    //
    //     Putting the record only into the add-on's core.lvl makes the
    //     game display "level.tat3.name" literally — which is itself the
    //     proof that `IFText_fnSetString` does perform a lookup and falls
    //     back to the raw string: the mechanism is right, only the
    //     destination matters.
    // UA: Один файл на обробку: звідки взяти і КУДИ його потім класти в грі.
    //     RelativeGamePath — шлях ВІДНОСНО теки гри, напр.
    //     "GameData\Data\_LVL_PC\core.lvl". Саме він визначає розкладку
    //     вихідної теки: результат лягає за тим самим шляхом, тож
    //     користувачеві не треба гадати, який файл куди — структура
    //     збігається з реальною інсталяцією (той самий принцип уже діє в
    //     GUI та інших генеруючих командах).
    //
    //     ЧОМУ НЕ СУФІКСИ В ІМЕНІ: базовий і аддонний core.lvl
    //     називаються однаково, тож іменування на кшталт
    //     "core._LVL_PC.lvl" і "core.AddOn.lvl" лишає незрозумілим, який
    //     файл куди йде в реальній інсталяції. Дзеркалення шляху знімає
    //     це питання повністю.
    // EN: One file to process: where to read it from and WHERE it belongs
    //     in the game. RelativeGamePath is relative to the game folder,
    //     e.g. "GameData\Data\_LVL_PC\core.lvl". It drives the output
    //     layout: results are written under that same path, so the user
    //     never has to guess which file goes where — the structure matches
    //     a real installation (the same principle already used by the GUI
    //     and other generating commands).
    //
    //     WHY NOT NAME SUFFIXES: the base and add-on core.lvl share a
    //     name, so naming them side by side as "core._LVL_PC.lvl" and
    //     "core.AddOn.lvl" leaves it unclear which file goes where in a
    //     real installation. Mirroring the path removes that ambiguity
    //     entirely.
    public readonly record struct CoreTarget(string SourcePath, string RelativeGamePath);

    public static async Task Run(
        DiagnosticReport report,
        string addmeScriptPath,
        string addmeRelativeGamePath,
        IReadOnlyList<CoreTarget> coreTargets,
        string outputDir)
    {
        report.Log("=== UA: Патч назви карти аддону / EN: Add-on map name patch ===");
        report.Log();

        var keyHash = SwbfStringHash.Compute(LocalizationKey);
        report.Log($"UA: Ключ / EN: key = \"{LocalizationKey}\"  →  hash = 0x{keyHash:x8}");
        report.Log();

        // ---------------------------------------------------------------------
        // UA: КРОК 1 — addme.script
        // EN: STEP 1 — addme.script
        // ---------------------------------------------------------------------
        if (VanillaShowStr.Length != LocalizationKey.Length)
        {
            // UA: Захист від майбутньої зміни констант: різна довжина
            //     означає, що побайтова заміна вже НЕ коректна.
            // EN: Guards against a future constant change: different
            //     lengths mean the in-place replacement is no longer valid.
            report.Log(
                $"UA: ПОМИЛКА — довжини не збігаються ({VanillaShowStr.Length} проти {LocalizationKey.Length}); " +
                "побайтова заміна неможлива, потрібне узгоджене оновлення полів розміру. / " +
                $"EN: ERROR — length mismatch ({VanillaShowStr.Length} vs {LocalizationKey.Length}); " +
                "in-place replacement is impossible, size fields would need a coordinated update.");
            return;
        }

        if (!File.Exists(addmeScriptPath))
        {
            report.Log($"UA: Не знайдено / EN: not found: {addmeScriptPath}");
            return;
        }

        var scriptBytes = await File.ReadAllBytesAsync(addmeScriptPath);
        var needle = Encoding.ASCII.GetBytes(VanillaShowStr);
        var offset = IndexOf(scriptBytes, needle);

        if (offset < 0)
        {
            // UA: Або файл уже пропатчено, або це інший аддон/версія —
            //     у будь-якому разі мовчки продовжувати не можна.
            // EN: Either already patched, or a different add-on/version —
            //     silently continuing is not acceptable either way.
            var already = IndexOf(scriptBytes, Encoding.ASCII.GetBytes(LocalizationKey)) >= 0;
            report.Log(already
                ? "UA: addme.script уже містить ключ — крок 1 пропущено (ідемпотентно). / " +
                  "EN: addme.script already contains the key — step 1 skipped (idempotent)."
                : $"UA: У addme.script не знайдено \"{VanillaShowStr}\" — це інший аддон або інша версія файлу. / " +
                  $"EN: \"{VanillaShowStr}\" not found in addme.script — different add-on or file version.");
            if (!already) return;
        }
        else
        {
            Encoding.ASCII.GetBytes(LocalizationKey).CopyTo(scriptBytes, offset);

            var scriptOut = Path.Combine(outputDir, addmeRelativeGamePath);
            Directory.CreateDirectory(Path.GetDirectoryName(scriptOut)!);
            await File.WriteAllBytesAsync(scriptOut, scriptBytes);

            report.Log($"UA: addme.script — замінено {needle.Length} байтів на офсеті {offset} / " +
                       $"EN: addme.script — replaced {needle.Length} bytes at offset {offset}");
            report.Log($"    \"{VanillaShowStr}\"  →  \"{LocalizationKey}\"");
            report.Log($"    → {scriptOut}");
            report.Log();
        }

        // ---------------------------------------------------------------------
        // UA: КРОК 2 — новий запис у Locl аддонного core.lvl
        // EN: STEP 2 — a new Locl record in the add-on's core.lvl
        // ---------------------------------------------------------------------
        foreach (var (corePath, relativeGamePath) in coreTargets)
        {
            if (!File.Exists(corePath))
            {
                report.Log($"UA: Не знайдено, пропущено / EN: not found, skipped: {corePath}");
                continue;
            }

            var service = new LvlLocalizationService();
            await service.LoadAsync(corePath);

            var added = 0;
            var skipped = 0;

            // UA: Додаємо в УСІ мовні таблиці файлу. Причина: гравець може
            //     запустити гру будь-якою мовою, і тоді читатиметься саме її
            //     таблиця — запис лише в "english" залишив би назву зламаною
            //     (показувався б сам ключ) для решти мов.
            // EN: Added to ALL language tables in the file. Reason: the
            //     player may run the game in any language, and that
            //     language's table is what gets read — adding only to
            //     "english" would leave the name broken (showing the raw
            //     key) for the rest.
            foreach (var lang in service.AvailableLanguages)
            {
                var file = service.GetLanguageFile(lang);
                if (file is null) continue;

                if (file.Entries.Any(e => e.Hash == keyHash))
                {
                    skipped++;
                    continue;
                }

                file.Entries.Add(new LocalizationEntry
                {
                    Hash = keyHash,
                    Original = VanillaShowStr,
                });
                file.BuildIndex();
                added++;
            }

            // UA: Вихід лягає за ІГРОВИМ шляхом — базовий і аддонний
            //     core.lvl мають однакове ім'я, і саме розкладка тек (а не
            //     суфікси в імені) робить очевидним, який файл куди.
            // EN: The output goes to its IN-GAME path — the base and add-on
            //     core.lvl share a filename, and it is the folder layout
            //     (not name suffixes) that makes it obvious which goes where.
            var coreOut = Path.Combine(outputDir, relativeGamePath);
            Directory.CreateDirectory(Path.GetDirectoryName(coreOut)!);
            await service.SaveAsync(coreOut);

            report.Log($"UA: {corePath}");
            report.Log($"    Locl — додано записів: {added}, вже було: {skipped} / " +
                       $"records added: {added}, already present: {skipped}");
            report.Log($"    → {relativeGamePath}");
        }

        report.Log();
        report.Log("UA: Розкладка вихідної теки повторює теку гри — копіювати «як є», зберігаючи шляхи. / " +
                   "EN: The output folder mirrors the game folder — copy \"as is\", preserving paths.");
        report.Log($"UA: Далі: обидва core.lvl провести через GUI (Оригінал → Новий робочий → Імпорт CSV); рядок " +
                   $"\"{VanillaShowStr}\" з'явиться в таблиці як звичайний неперекладений. addme.script " +
                   "копіюється напряму, він не потребує обробки. / " +
                   $"EN: Next: run both core.lvl through the GUI (Original → New working → Import CSV); the string " +
                   $"\"{VanillaShowStr}\" will appear in the grid as an ordinary untranslated row. addme.script " +
                   "is copied as-is, it needs no processing.");
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
