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

    // UA: Швидкий доступ до запису за хешем
    // EN: Fast access to entry by hash
    private Dictionary<uint, LocalizationEntry>? _hashIndex;

    // UA: Будує індекс для швидкого пошуку (викликається після завантаження)
    // EN: Builds index for fast lookup (call after loading)
    public void BuildIndex()
    {
        _hashIndex = [];
        foreach (var entry in Entries)
            _hashIndex[entry.Hash] = entry;
    }

    // UA: Знайти запис за хешем
    // EN: Find entry by hash
    public LocalizationEntry? GetByHash(uint hash)
    {
        if (_hashIndex is not null)
            return _hashIndex.TryGetValue(hash, out var entry) ? entry : null;

        return Entries.FirstOrDefault(e => e.Hash == hash);
    }

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
