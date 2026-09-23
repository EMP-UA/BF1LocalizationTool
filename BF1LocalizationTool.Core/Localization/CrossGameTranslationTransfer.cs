// =============================================================================
// BF1LocalizationTool.Core — Localization/CrossGameTranslationTransfer.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перенесення перекладу МІЖ ІГРАМИ (наприклад, з BF1 у BF2) для рядків,
//     що мають однаковий англійський оригінал.
//
//     ЧОМУ НЕ ЗА ХЕШЕМ: LocalizationEntry.Hash — це хеш ВНУТРІШНЬОГО КЛЮЧА
//     локалізації гри (напр. "level.tat3.objectives.1"), а не хеш тексту.
//     BF1 і BF2 мають повністю окремі простори ключів, тож однаковий текст
//     у двох іграх майже завжди дає РІЗНИЙ Hash — зіставлення за Hash (як
//     у MainWindow.MergeTranslationsFromDonor, для ОДНІЄЇ гри) тут не
//     працює за визначенням. Тому зіставлення — за САМИМ ТЕКСТОМ
//     англійського оригіналу.
//
//     АРХІТЕКТУРНИЙ ПРИНЦИП (той самий, що й у MainWindow.
//     MergeTranslationsFromDonor / BtnOpenWorking_Click): донор постачає
//     ЛИШЕ ТЕКСТ перекладу, ніколи — структуру, шрифти чи розмір. Це
//     забезпечується самою формою Apply(): метод лише читає донора й пише
//     Translation у вже завантажений цільовий LocalizationFile, не
//     торкаючись нічого іншого.
//
//     НЕДЕСТРУКТИВНІСТЬ: захист побудовано на ПОЗНАЧЦІ ВИЧИТКИ
//     (ReviewStatus), а не на самому факті наявності перекладу
//     (LocalizationEntry.IsTranslated) — після пакетного перекладу через
//     Gemini переклад має практично КОЖЕН рядок, тож захист "не чіпати
//     перекладене" на практиці не переносив би нічого. ReviewStatus — це
//     GUI/CSV-метадані (LocalizationCsvRow.ReviewStatus), Core про них не
//     знає (див. LocalizationCsvIo.cs), тому Apply() приймає критерій
//     захисту ЗЗОВНІ, як делегат isProtected(Hash, Ordinal) — викликач
//     (GUI) вирішує сам, спираючись на MainWindow._reviewStatuses.
//
//     НЕОДНОЗНАЧНІСТЬ: якщо той самий англійський оригінал у ДОНОРІ має
//     КІЛЬКА різних українських перекладів (різні контексти в межах однієї
//     гри), рядок вважається конфліктним і НЕ переноситься автоматично —
//     викликач (GUI) повинен спитати користувача, який варіант застосувати
//     (або жоден).
//
// EN: Cross-game translation transfer (e.g. from BF1 into BF2) for strings
//     that share the same English original text.
//
//     WHY NOT BY HASH: LocalizationEntry.Hash is a hash of the game's
//     INTERNAL localization KEY (e.g. "level.tat3.objectives.1"), not a
//     hash of the text. BF1 and BF2 have entirely separate key namespaces,
//     so identical text in the two games almost always yields a DIFFERENT
//     Hash — matching by Hash (as MainWindow.MergeTranslationsFromDonor
//     does, WITHIN one game) doesn't work here by definition. Matching is
//     therefore done on the English original TEXT itself.
//
//     ARCHITECTURAL PRINCIPLE (the same one used in MainWindow.
//     MergeTranslationsFromDonor / BtnOpenWorking_Click): the donor
//     supplies ONLY translation text, never structure, fonts or size.
//     This is enforced by the shape of Apply() itself: it only reads the
//     donor and writes Translation onto an already-loaded target
//     LocalizationFile, touching nothing else.
//
//     NON-DESTRUCTIVE: the protection is built on the REVIEW MARK
//     (ReviewStatus), not on the mere presence of a translation
//     (LocalizationEntry.IsTranslated) — after a batch translation pass
//     through Gemini, practically EVERY row already has a translation, so
//     a "don't touch translated rows" guard would transfer nothing in
//     practice. ReviewStatus is GUI/CSV metadata (LocalizationCsvRow.
//     ReviewStatus), Core doesn't know about it (see LocalizationCsvIo.cs),
//     so Apply() takes the protection criterion from the CALLER, as an
//     isProtected(Hash, Ordinal) delegate — the caller (GUI) decides,
//     based on MainWindow._reviewStatuses.
//
//     AMBIGUITY: if the same English original has SEVERAL distinct
//     Ukrainian translations in the donor (different contexts within the
//     same game), the string is a conflict and is NOT auto-transferred —
//     the caller (GUI) must ask the user which variant to apply (or none).
// =============================================================================

using BF1LocalizationTool.Core.Models;

namespace BF1LocalizationTool.Core.Localization;

// UA: Індекс "англійський оригінал → перелік РІЗНИХ українських перекладів,
//     знайдених у донорі". Довжина списку 1 = безпечно; >1 = конфлікт.
// EN: Index "English original → list of DISTINCT Ukrainian translations
//     found in the donor". List length 1 = safe; >1 = conflict.
public static class CrossGameTranslationTransfer
{
    // -------------------------------------------------------------------------
    // UA: Будує індекс з пари файлів ОДНІЄЇ гри (джерело — англ. оригінал,
    //     перекладена мова — цільова, напр. українська), зіставлених за
    //     (Hash, Ordinal) — так само, як MainWindow.MergeTranslationsFromDonor
    //     зіставляє в межах однієї гри. Технічні рядки (IsTechnical)
    //     виключені — вони не є текстом, що бачить гравець.
    // EN: Builds the index from a pair of files of ONE game (source =
    //     English original, translated = target language, e.g. Ukrainian),
    //     paired by (Hash, Ordinal) — the same way MainWindow.
    //     MergeTranslationsFromDonor pairs entries within one game.
    //     Technical strings (IsTechnical) are excluded — they aren't text
    //     the player sees.
    // -------------------------------------------------------------------------
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildIndex(
        LocalizationFile donorSourceFile,
        LocalizationFile donorTranslatedFile)
    {
        var byOriginal = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var occurrence = new Dictionary<uint, int>();

        foreach (var e in donorSourceFile.Entries)
        {
            var ordinal = occurrence.TryGetValue(e.Hash, out var n) ? n : 0;
            occurrence[e.Hash] = ordinal + 1;

            if (e.IsTechnical) continue;
            if (string.IsNullOrWhiteSpace(e.Original)) continue;

            var translatedEntry = donorTranslatedFile.GetByHash(e.Hash, ordinal);
            if (translatedEntry is null) continue;

            // UA: Той самий прийом, що й у MergeTranslationsFromDonor: якщо
            //     в пам'яті ще немає Translation, але Original перекладеного
            //     файлу вже відрізняється від англійського — це вже
            //     "запечений" у файл переклад (раніше збережений робочий
            //     файл), теж придатний як джерело.
            // EN: Same trick as MergeTranslationsFromDonor: if there's no
            //     in-memory Translation yet, but the translated file's
            //     Original already differs from the English one — that's a
            //     translation already "baked" into the file (a previously
            //     saved working file), also usable as a source.
            var text = translatedEntry.Translation
                       ?? (translatedEntry.Original != e.Original ? translatedEntry.Original : null);
            if (string.IsNullOrWhiteSpace(text)) continue;

            if (!byOriginal.TryGetValue(e.Original, out var list))
                byOriginal[e.Original] = list = [];
            if (!list.Contains(text, StringComparer.Ordinal))
                list.Add(text);
        }

        return ToReadOnlyIndex(byOriginal);
    }

    // -------------------------------------------------------------------------
    // UA: Той самий індекс, але з CSV-рядків (LocalizationCsvIo) — там
    //     Original/Translation уже попарно в одному рядку, окремого
    //     зіставлення за (Hash, Ordinal) не потрібно.
    // EN: The same index, built from CSV rows (LocalizationCsvIo) — there
    //     Original/Translation are already paired in one row, no separate
    //     (Hash, Ordinal) matching is needed.
    // -------------------------------------------------------------------------
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildIndex(
        IEnumerable<LocalizationCsvRow> donorRows)
    {
        var byOriginal = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var row in donorRows)
        {
            if (string.IsNullOrWhiteSpace(row.Original)) continue;
            if (string.IsNullOrWhiteSpace(row.Translation)) continue;

            if (!byOriginal.TryGetValue(row.Original, out var list))
                byOriginal[row.Original] = list = [];
            if (!list.Contains(row.Translation, StringComparer.Ordinal))
                list.Add(row.Translation);
        }

        return ToReadOnlyIndex(byOriginal);
    }

    // UA: Допоміжне: Dictionary<string, List<string>> → Dictionary<string,
    //     IReadOnlyList<string>> без залежності від конкретного синтаксису
    //     лямбд з явним типом повернення.
    // EN: Helper: Dictionary<string, List<string>> → Dictionary<string,
    //     IReadOnlyList<string>> without relying on explicit-return-type
    //     lambda syntax.
    private static Dictionary<string, IReadOnlyList<string>> ToReadOnlyIndex(
        Dictionary<string, List<string>> source)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(source.Count, StringComparer.Ordinal);
        foreach (var (key, value) in source)
            result[key] = value;
        return result;
    }

    // -------------------------------------------------------------------------
    // UA: Розділяє індекс на "безпечні" (рівно один варіант перекладу) і
    //     "конфліктні" (кілька різних варіантів — потребують рішення
    //     користувача).
    // EN: Splits the index into "safe" (exactly one translation variant)
    //     and "conflicting" (several different variants — need a user
    //     decision).
    // -------------------------------------------------------------------------
    public static TransferPlan BuildPlan(IReadOnlyDictionary<string, IReadOnlyList<string>> donorIndex)
    {
        var autoFill = new Dictionary<string, string>(StringComparer.Ordinal);
        var conflicts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var (original, translations) in donorIndex)
        {
            if (translations.Count == 1)
                autoFill[original] = translations[0];
            else
                conflicts[original] = translations;
        }

        return new TransferPlan(autoFill, conflicts);
    }

    // -------------------------------------------------------------------------
    // UA: Застосовує план до вже завантаженого ЦІЛЬОВОГО файлу (пара
    //     джерело-мова/цільова-мова тієї ж гри, зіставлена за (Hash,
    //     Ordinal) — так само, як цільовий бік MergeTranslationsFromDonor).
    //     conflictResolutions — рішення користувача по кожному конфлікту
    //     з BuildPlan (Original → обраний переклад); Original, відсутній
    //     у цьому словнику, вважається пропущеним користувачем.
    //
    //     isProtected(Hash, Ordinal) — критерій "не чіпати", наданий
    //     ВИКЛИКАЧЕМ (GUI): Core не має уявлення про ReviewStatus (це
    //     GUI/CSV-метадані), тож саме викликач вирішує, який запис уже
    //     ВИЧИТАНИЙ і не підлягає перезапису. НЕ перевіряється
    //     LocalizationEntry.IsTranslated — після пакетного перекладу через
    //     Gemini цей прапорець true майже завжди, тож він непридатний як
    //     критерій захисту (див. заголовок файлу).
    // EN: Applies the plan to an already-loaded TARGET file pair (source-
    //     language/target-language of the SAME game, paired by (Hash,
    //     Ordinal) — the same way the target side of
    //     MergeTranslationsFromDonor works). conflictResolutions — the
    //     user's per-conflict decisions from BuildPlan (Original → chosen
    //     translation); an Original absent from this dictionary is treated
    //     as skipped by the user.
    //
    //     isProtected(Hash, Ordinal) — the "leave alone" criterion, supplied
    //     by the CALLER (GUI): Core has no notion of ReviewStatus (that's
    //     GUI/CSV metadata), so the caller decides which entry is already
    //     REVIEWED and must not be overwritten. LocalizationEntry.
    //     IsTranslated is NOT checked — after a batch translation pass
    //     through Gemini that flag is true almost everywhere, so it's
    //     unusable as a protection criterion (see the file header).
    // -------------------------------------------------------------------------
    public static TransferResult Apply(
        LocalizationFile targetSourceFile,
        LocalizationFile targetTranslatedFile,
        IReadOnlyDictionary<string, string> autoFill,
        IReadOnlyDictionary<string, string> conflictResolutions,
        Func<uint, int, bool> isProtected)
    {
        var autoFilled = 0;
        var conflictResolved = 0;
        var protectedSkipped = 0;
        var occurrence = new Dictionary<uint, int>();

        foreach (var e in targetSourceFile.Entries)
        {
            var ordinal = occurrence.TryGetValue(e.Hash, out var n) ? n : 0;
            occurrence[e.Hash] = ordinal + 1;

            if (e.IsTechnical) continue;

            var targetEntry = targetTranslatedFile.GetByHash(e.Hash, ordinal);
            if (targetEntry is null) continue;

            if (isProtected(e.Hash, ordinal))
            {
                protectedSkipped++;
                continue;
            }

            if (autoFill.TryGetValue(e.Original, out var autoText))
            {
                targetEntry.Translation = autoText;
                autoFilled++;
            }
            else if (conflictResolutions.TryGetValue(e.Original, out var resolvedText))
            {
                targetEntry.Translation = resolvedText;
                conflictResolved++;
            }
        }

        return new TransferResult(autoFilled, conflictResolved, protectedSkipped);
    }
}

// UA: Безпечні (однозначні) переноси й конфлікти (кілька варіантів), що
//     потребують рішення користувача.
// EN: Safe (unambiguous) transfers and conflicts (several variants) that
//     need a user decision.
public readonly record struct TransferPlan(
    IReadOnlyDictionary<string, string> AutoFill,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Conflicts);

// UA: Підсумок застосування — для рядка статусу в GUI. ProtectedSkipped —
//     скільки записів пропущено через isProtected (вже вичитані), а не
//     через відсутність збігу.
// EN: Application summary — for the GUI status line. ProtectedSkipped —
//     how many entries were skipped via isProtected (already reviewed),
//     not for lack of a match.
public readonly record struct TransferResult(
    int AutoFilled,
    int ConflictResolved,
    int ProtectedSkipped);
