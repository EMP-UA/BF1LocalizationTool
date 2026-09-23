// =============================================================================
// BF1LocalizationTool.Diagnostic — AnalyzeMovieSubtitleAlphaSourceCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Викликає BF1LocalizationTool.Core.Bf2Exe.MovieSubtitleAlphaScanner —
//     дивись докладний коментар про мету, межі й законність аналізу на
//     початку того файлу. Тут лише формує звіт: НЕ пише і не патчить нічого,
//     ЛИШЕ читає вже наявну на диску користувача копію BattlefrontII.exe.
//
//     МЕХАНІЗМ повного зникнення підпису (ворота альфа-каналу в 0x006D8150)
//     описаний у коментарі сканера вище. Пряме (ЕТАП 1) звуження кандидатів
//     саме по собі не гарантує влучення в потрібну функцію: перевірені
//     прямі кандидати нерідко виявляються хибними спрацюваннями. Ця команда
//     тому запускає й ЕТАП 2 (граф прямих викликів, callee/caller) — щоб
//     зловити НЕпрямий зв'язок з ws/W/H, який ЕТАП 1 в принципі не може
//     побачити. Вона НЕ дає готової відповіді, лише список кандидатів для
//     ручної перевірки.
//
// EN: Calls BF1LocalizationTool.Core.Bf2Exe.MovieSubtitleAlphaScanner — see
//     the detailed comment on purpose, scope and legality at the top of that
//     file. This command only builds a report: it writes and patches
//     nothing, it ONLY reads the copy of BattlefrontII.exe already on the
//     user's own disk.
//
//     The MECHANISM of the caption's total disappearance (the alpha-channel
//     gate at 0x006D8150) is described in the scanner's own comment above.
//     A direct (STAGE 1) candidate match does not by itself guarantee the
//     right function: checked direct candidates often turn out to be false
//     positives. This command therefore also runs STAGE 2 (a direct-call
//     graph, callee/caller) to catch an INDIRECT connection to ws/W/H that
//     STAGE 1 cannot see by construction. It does NOT hand over a ready
//     answer, only a list of candidates for manual review.
// =============================================================================

using BF1LocalizationTool.Core.Bf2Exe;

namespace BF1LocalizationTool.Diagnostic;

public static class AnalyzeMovieSubtitleAlphaSourceCommand
{
    // UA: Скільки кандидатів (другорядних/ЕТАП-1-негативних, і непрямих/
    //     ЕТАП-2-позитивних) друкувати поіменно — решта йде лише як
    //     кількість, інакше звіт нечитабельний (блаженний скан давав 1000+
    //     збігів, див. коментар у сканері).
    // EN: How many candidates (secondary/STAGE-1-negative, and
    //     indirect/STAGE-2-positive) to print by name — the rest is
    //     reported only as a count, otherwise the report is unreadable (a
    //     blind scan produced 1000+ hits, see the scanner's comment).
    private const int MaxSecondaryListed = 40;
    private const int MaxIndirectListed = 60;

    public static void Run(DiagnosticReport report, string exePath)
    {
        report.Log("UA: ПОШУК ДЖЕРЕЛА АЛЬФИ СУБТИТРУ — читає ЛИШЕ BattlefrontII.exe, нічого не пише.");
        report.Log("EN: SUBTITLE ALPHA SOURCE SEARCH — READS BattlefrontII.exe ONLY, writes nothing.");
        report.Log($"UA: Файл: \"{exePath}\"");
        report.Log($"EN: File: \"{exePath}\"");
        report.Log();

        AlphaScanResult result;
        try
        {
            result = MovieSubtitleAlphaScanner.Scan(exePath);
        }
        catch (Exception ex)
        {
            report.Log($"UA: Аналіз не виконано — {ex.Message}");
            report.Log($"EN: Analysis did not run — {ex.Message}");
            return;
        }

        report.Log($"UA: ImageBase 0x{result.ImageBase:X8}, .text = 0x{result.TextSectionStartVa:X8}..0x{result.TextSectionEndVa:X8}, " +
                   $"проскановано функцій: {result.FunctionsScanned}.");
        report.Log($"EN: ImageBase 0x{result.ImageBase:X8}, .text = 0x{result.TextSectionStartVa:X8}..0x{result.TextSectionEndVa:X8}, " +
                   $"functions scanned: {result.FunctionsScanned}.");
        report.Log();

        // ---------------------------------------------------------------
        // ЕТАП 1 / STAGE 1 — пряме звернення в тій самій функції
        // ---------------------------------------------------------------
        report.Log("UA: ЕТАП 1 — ГОЛОВНІ КАНДИДАТИ: пишуть у байт кольору/альфи [reg+0x2C..0x2F] " +
                   "І в тій самій функції звертаються до ws/W/H. Прямий збіг сам по собі не " +
                   "гарантує влучення — перевірені кандидати нерідко виявляються хибними " +
                   "спрацюваннями, див. нижче ЕТАП 2.");
        report.Log("EN: STAGE 1 — PRIMARY CANDIDATES: write to the color/alpha byte [reg+0x2C..0x2F] " +
                   "AND, in the same function, reference ws/W/H. A direct match alone does not " +
                   "guarantee the right function — checked candidates often turn out to be " +
                   "false positives, see STAGE 2 below.");
        report.Log();

        if (result.PrimaryCandidates.Count == 0)
        {
            report.Log("UA: (жодного в цьому прогоні.)");
            report.Log("EN: (none in this run.)");
        }
        else
        {
            foreach (var c in result.PrimaryCandidates)
            {
                report.Log($"  функція/function 0x{c.FunctionStart:X8}   @0x{c.InstructionAddress:X8}   " +
                           $"{c.InstructionText}   (+0x{c.ColorByteOffset:X2})");
                foreach (var detail in c.AspectReferenceDetails)
                    report.Log($"      -> {detail}");
            }
        }

        // ---------------------------------------------------------------
        // ЕТАП 2 / STAGE 2 — непрямий зв'язок через граф прямих викликів
        // ---------------------------------------------------------------
        report.Log();
        report.Log($"UA: ЕТАП 2 — НЕПРЯМІ КАНДИДАТИ: пишуть у +0x2C..0x2F, ПРЯМОГО звернення до " +
                   $"ws/W/H у своїй функції НЕМАЄ, але в графі прямих викликів (callee АБО caller, " +
                   $"не глибше 3 кроків) знайдено функцію, яка до ws/W/H звертається. " +
                   $"Знайдено: {result.IndirectCandidates.Count} шт. " +
                   $"(бачить ЛИШЕ виклики call rel32 — не віртуальні/vtable-виклики, див. коментар " +
                   $"у сканері).");
        report.Log($"EN: STAGE 2 — INDIRECT CANDIDATES: write to +0x2C..0x2F, have NO direct ws/W/H " +
                   $"reference in their own function, but a function that DOES reference ws/W/H was " +
                   $"found in the direct-call graph (callee OR caller, no deeper than 3 hops). " +
                   $"Found: {result.IndirectCandidates.Count} item(s). " +
                   $"(sees ONLY call rel32 calls — not virtual/vtable calls, see the scanner's " +
                   $"comment).");
        report.Log();

        if (result.IndirectCandidates.Count == 0)
        {
            report.Log("UA: (жодного — навіть до глибини 3 кроків прямих викликів зв'язку з ws/W/H не");
            report.Log("UA: знайдено. Це теж результат: якщо тригер справді залежить від ws/W/H, тригер або");
            report.Log("UA: глибший за 3 кроки, або йде через ВІРТУАЛЬНИЙ виклик (vtable), який цей");
            report.Log("UA: сканер не бачить, або не є прямим зверненням до ws/W/H взагалі — напр.");
            report.Log("UA: обчислюється з чогось ІНШОГО, теж залежного від аспекту, але не з цих 3");
            report.Log("UA: конкретних глобалів/функції-читача.");
            report.Log("EN: (none — even up to 3 hops of direct calls, no connection to ws/W/H was");
            report.Log("EN: found. This is also a result: if the trigger genuinely depends on ws/W/H, the");
            report.Log("EN: trigger is either deeper than 3 hops, or goes through a VIRTUAL call");
            report.Log("EN: (vtable) this scanner cannot see, or isn't a direct reference to ws/W/H");
            report.Log("EN: at all — e.g. computed from something ELSE that is also aspect-dependent,");
            report.Log("EN: but not these 3 specific globals/reader function.");
        }
        else
        {
            foreach (var c in result.IndirectCandidates.Take(MaxIndirectListed))
            {
                report.Log($"  функція/function 0x{c.FunctionStart:X8}   @0x{c.InstructionAddress:X8}   " +
                           $"{c.InstructionText}   (+0x{c.ColorByteOffset:X2})");
                report.Log($"      шлях/path: {string.Join(" ", c.PathDescription)}");
                foreach (var detail in c.AspectReferenceDetails)
                    report.Log($"      -> {detail}");
            }

            if (result.IndirectCandidates.Count > MaxIndirectListed)
            {
                var rest = result.IndirectCandidates.Count - MaxIndirectListed;
                report.Log($"  ... і ще {rest} / ... and {rest} more");
            }
        }

        // ---------------------------------------------------------------
        // Другорядні (ані ЕТАП 1, ані ЕТАП 2 нічого не знайшли)
        // ---------------------------------------------------------------
        report.Log();
        report.Log($"UA: Другорядні кандидати (пишуть у +0x2C..0x2F, але НІ прямого, НІ непрямого " +
                   $"(до 3 кроків) зв'язку з ws/W/H не знайдено): {result.SecondaryCandidates.Count} шт.");
        report.Log($"EN: Secondary candidates (write to +0x2C..0x2F, but NEITHER a direct NOR an " +
                   $"indirect (up to 3 hops) connection to ws/W/H was found): {result.SecondaryCandidates.Count} items.");
        report.Log("UA: (для довідки — не пріоритет; спершу варто перевірити ЕТАП 1, потім ЕТАП 2 вище.)");
        report.Log("EN: (for reference — not a priority; check STAGE 1, then STAGE 2 above first.)");
        report.Log();

        foreach (var c in result.SecondaryCandidates.Take(MaxSecondaryListed))
        {
            report.Log($"  функція/function 0x{c.FunctionStart:X8}   @0x{c.InstructionAddress:X8}   " +
                       $"{c.InstructionText}   (+0x{c.ColorByteOffset:X2})");
        }

        if (result.SecondaryCandidates.Count > MaxSecondaryListed)
        {
            var rest = result.SecondaryCandidates.Count - MaxSecondaryListed;
            report.Log($"  ... і ще {rest} / ... and {rest} more");
        }

        report.Log();
        report.Log("UA: НАСТУПНИЙ КРОК: для кожного кандидата ЕТАПУ 1, а тепер і ЕТАПУ 2, вручну");
        report.Log("UA: підняти повний контекст функції (і, для ЕТАПУ 2, усіх функцій за шляхом)");
        report.Log("UA: (напр. Python+capstone) і перевірити, ЩО САМЕ");
        report.Log("UA: записується — константа, чи значення, обчислене з ws/W/H:");
        report.Log("UA: нічого не \"виправлено\" без підтвердження знімком з гри.");
        report.Log("EN: NEXT STEP: for each STAGE-1 candidate, and now each STAGE-2 candidate, manually");
        report.Log("EN: pull up the function's full context (and, for STAGE 2, every function along the");
        report.Log("EN: path) (e.g. Python+capstone) and check WHAT is");
        report.Log("EN: actually being written — a constant, or a value computed from ws/W/H:");
        report.Log("EN: nothing is \"fixed\" without confirmation via an in-game screenshot.");
    }
}
