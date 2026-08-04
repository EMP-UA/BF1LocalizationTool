// =============================================================================
// BF1LocalizationTool.Core — Models/LocalizationFile.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Представляє один мовний файл локалізації (наприклад english.txt).
//     Містить упорядкований список записів LocalizationEntry.
//     Порядок записів зберігається — важливо для точного запису назад у .lvl.
// EN: Represents one language localization file (e.g. english.txt).
//     Contains an ordered list of LocalizationEntry records.
//     Entry order is preserved — important for accurate write-back to .lvl.
// =============================================================================

namespace BF1LocalizationTool.Core.Models;

public class LocalizationFile
{
    // UA: Назва мови (без розширення, наприклад "english", "uk_english")
    // EN: Language name (without extension, e.g. "english", "uk_english")
    public string LanguageName { get; init; } = string.Empty;

    // UA: Впорядкований список записів
    // EN: Ordered list of entries
    public List<LocalizationEntry> Entries { get; init; } = [];

    // UA: Швидкий доступ до записів за хешем — ГРУПА, не один запис.
    //     ЕМПІРИЧНО ПІДТВЕРДЖЕНО (прямий побайтовий парсинг core.lvl,
    //     обидві гри): той самий 32-бітний хеш навмисно використовується
    //     ДЛЯ ДВОХ РІЗНИХ рядків тексту — напр. BF1 english,
    //     0xf2e2b10d = і "SQUAD COMMANDS", і "SQUAD\r\nCOMMANDS"
    //     (короткий/переносний варіант того самого UI-елемента під різні
    //     контексти). Це не випадкова колізія (53 дублікати на 2457
    //     записів статистично неможливі для випадкового 32-бітного хеша
    //     — очікується ~0,0007) і не помилка запису/парсингу — так
    //     влаштовані самі дані гри. У BF1 і BF2, у ВСІХ 6 мовах кожної
    //     гри, максимум 2 входження на хеш (жодного потрійного). Саме тому
    //     індекс зберігає List<LocalizationEntry> на хеш (ГРУПУ), а не
    //     єдиний запис — інакше довелось би довільно обирати, який із двох
    //     варіантів "перемагає", і зіставлення оригінал↔переклад між
    //     різними файлами для будь-якого рядка з дубльованим хешем було б
    //     непередбачуваним.
    // EN: Fast access to entries by hash — a GROUP, not a single entry.
    //     EMPIRICALLY CONFIRMED (direct byte-level parsing of core.lvl,
    //     both games): the same 32-bit hash is deliberately used for TWO
    //     DIFFERENT strings of text — e.g. BF1 english, 0xf2e2b10d = both
    //     "SQUAD COMMANDS" and "SQUAD\r\nCOMMANDS" (short/wrapped variant
    //     of the same UI element for different contexts). This is not a
    //     random collision (53 duplicates among 2457 entries is
    //     statistically impossible for a random 32-bit hash — ~0.0007
    //     expected) and not a read/parse bug — it's how the game's own
    //     data is structured. In BOTH BF1 and BF2, across ALL 6 languages
    //     of each game, at most 2 occurrences per hash (never three).
    //     That's exactly why the index stores a List<LocalizationEntry>
    //     per hash (a GROUP), not a single entry — otherwise one of the
    //     two variants would have to arbitrarily "win", and
    //     original↔translation pairing across files would be
    //     unpredictable for any duplicated-hash row.
    private Dictionary<uint, List<LocalizationEntry>>? _hashGroups;

    // UA: Будує індекс для швидкого пошуку (викликається після завантаження)
    // EN: Builds index for fast lookup (call after loading)
    public void BuildIndex()
    {
        _hashGroups = [];
        foreach (var entry in Entries)
        {
            if (!_hashGroups.TryGetValue(entry.Hash, out var group))
                _hashGroups[entry.Hash] = group = [];
            group.Add(entry);
        }
    }

    // UA: Знайти запис за хешем І порядковим номером серед записів з
    //     ТИМ САМИМ хешем (0 = перший у файлі, 1 = другий за наявності
    //     дубліката). ordinal=0 за замовчуванням підходить для 99%
    //     викликів (немає дубліката — єдиний запис і так під номером 0);
    //     явно вказуй ordinal лише коли зіставляєш ДВА файли рядок-в-
    //     рядок (MainWindow.RefreshGrid, CompareWindow.RunComparison) —
    //     інакше при дублікаті ризикуєш звірити НЕ ту пару варіантів.
    // EN: Find an entry by hash AND ordinal position among entries
    //     sharing the SAME hash (0 = first in the file, 1 = second if a
    //     duplicate exists). ordinal=0 default suits 99% of calls (no
    //     duplicate — the single entry is ordinal 0 anyway); pass an
    //     explicit ordinal only when cross-referencing TWO files row-by-
    //     row (MainWindow.RefreshGrid, CompareWindow.RunComparison) —
    //     otherwise a duplicate risks pairing the WRONG variant.
    public LocalizationEntry? GetByHash(uint hash, int ordinal = 0)
    {
        if (_hashGroups is not null)
            return _hashGroups.TryGetValue(hash, out var group) && ordinal < group.Count
                ? group[ordinal]
                : null;

        var matches = Entries.Where(e => e.Hash == hash).ToList();
        return ordinal < matches.Count ? matches[ordinal] : null;
    }

    // UA: Скільки записів мають цей хеш (0 = немає, 1 = звичайний
    //     випадок, 2 = дубльований UI-варіант — див. коментар вище).
    // EN: How many entries share this hash (0 = none, 1 = normal case,
    //     2 = duplicated UI variant — see comment above).
    public int CountByHash(uint hash) =>
        _hashGroups is not null && _hashGroups.TryGetValue(hash, out var group)
            ? group.Count
            : Entries.Count(e => e.Hash == hash);

    // UA: Кількість перекладених записів
    // EN: Number of translated entries
    public int TranslatedCount => Entries.Count(e => e.IsTranslated);

    // UA: Загальна кількість записів
    // EN: Total number of entries
    public int TotalCount => Entries.Count;

    // UA: Відсоток завершення перекладу
    // EN: Translation completion percentage
    public double CompletionPercent =>
        TotalCount == 0 ? 0 : (double)TranslatedCount / TotalCount * 100;

    public override string ToString() =>
        $"{LanguageName}: {TranslatedCount}/{TotalCount} ({CompletionPercent:F1}%)";
}
