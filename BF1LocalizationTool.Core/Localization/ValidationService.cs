// =============================================================================
// BF1LocalizationTool.Core — Localization/ValidationService.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перевіряє що технічні маркери з оригіналу збережені в перекладі.
//     Маркери: %s %d %i %f %c %u \n \t [змінні] (X) тощо.
//
//     ПЕРЕНЕСЕНО з BF1LocalizationTool.GUI.Services у Core: логіка не має
//     WPF-залежностей і потрібна також консольному ШІ-перекладачу
//     (BF1LocalizationTool.Translator) для перевірки якості перекладу,
//     отриманого від Gemini, ПЕРЕД записом назад у CSV — без дублювання
//     коду між GUI і Translator.
//
//     Перевірка МНОЖИННА, не лише типова: Except() сам по собі перевіряє
//     лише "чи є такий ТИП маркера взагалі", не КІЛЬКІСТЬ — якщо в
//     оригіналі два "%s", а переклад містить лише один, тип-перевірка
//     пропустила б це як валідне. Тому кількість кожного маркера
//     рахується і порівнюється явно — втрата навіть одного повторного
//     входження ловиться.
//
//     Перевірка на літери ыэёъ (яких немає в українському алфавіті —
//     ознака, що переклад зіскочив у російську) живе саме в цьому
//     СПІЛЬНОМУ Core-сервісі, яким GUI вже й так користується для
//     перевірки кожного рядка (RowViewModel.Validate), а Translator.
//     Pipeline (консольний проєкт) — так само: русизми автоматично
//     потрапляють у фільтр "⚠ Проблемні" в GUI без дублювання логіки.
//     Також спільні ContainsCyrillic/MinLengthForCyrillicCheck — те саме
//     Unicode-визначення "чи є кирилиця", яким користуються і Translator
//     (Warning-перевірка), і GUI (класифікація "не перекладено") — без
//     дублювання regex у трьох місцях.
// EN: Checks that technical markers from original are preserved in translation.
//     Markers: %s %d %i %f %c %u \n \t [variables] (X) etc.
//
//     MOVED from BF1LocalizationTool.GUI.Services into Core: this logic has
//     no WPF dependency and is also needed by the console AI translator
//     (BF1LocalizationTool.Translator) to validate translations returned by
//     Gemini BEFORE writing them back to CSV — avoids duplicating code
//     between GUI and Translator.
//
//     The check is MULTIPLICITY-AWARE, not just type-based: a plain
//     Except() only checks "does this marker TYPE exist at all", not the
//     COUNT — if the original had two "%s" but the translation had only
//     one, a type-only check would pass that as valid. Each marker's
//     occurrences are therefore counted and compared explicitly — losing
//     even one repeated occurrence is caught.
//
//     The check for ыэёъ letters (not in the Ukrainian alphabet — a sign
//     the translation slipped into Russian) lives in this SHARED Core
//     service, which the GUI already uses to validate every row
//     (RowViewModel.Validate) and which Translator.Pipeline (the console
//     project) also uses — so Russian slips automatically land in the
//     GUI's "⚠ Issues" filter with no duplicated logic. Also shared:
//     ContainsCyrillic/MinLengthForCyrillicCheck — the same Unicode-based
//     "does this contain Cyrillic" definition used by both the Translator
//     (Warning check) and the GUI ("untranslated" classification) — no
//     regex duplicated across three places.
// =============================================================================

using System.Text.RegularExpressions;

namespace BF1LocalizationTool.Core.Localization;

public record ValidationResult(bool IsValid, string Message);

public static class ValidationService
{
    // UA: Мінімальна довжина, з якої взагалі має сенс перевіряти "чи є
    //     кирилиця" — коротше вже відсіяне TechnicalStringService до
    //     відправки в Gemini (Translator) або є законним коротким кодом,
    //     який людина могла лишити свідомо (GUI).
    // EN: Minimum length from which checking "is there Cyrillic" even
    //     makes sense — anything shorter is either already filtered out
    //     by TechnicalStringService before reaching Gemini (Translator),
    //     or is a legitimate short code a human may have left on purpose (GUI).
    public const int MinLengthForCyrillicCheck = 3;

    private static readonly Regex CyrillicRegex = new(@"\p{IsCyrillic}", RegexOptions.Compiled);

    // UA: Літери, яких немає в українському алфавіті — їхня наявність
    //     зазвичай означає, що переклад зіскочив у російську.
    // EN: Letters not in the Ukrainian alphabet — their presence
    //     typically means the translation slipped into Russian.
    private static readonly Regex RussianOnlyLettersRegex = new(@"[ыэёъЫЭЁЪ]", RegexOptions.Compiled);

    public static bool ContainsCyrillic(string text) => CyrillicRegex.IsMatch(text);

    // UA: Патерни технічних маркерів що мають бути збережені в перекладі
    // EN: Patterns of technical markers that must be preserved in translation
    // UA: %s %d — змінні формату; {btn...} — кнопкові маркери (.loc); [X] (X) — клавіші
    // EN: %s %d — format vars; {btn...} — button markers (.loc); [X] (X) — keys
    private static readonly Regex MarkerRegex = new(
        @"%[sdifcux%]|\{[^}]+\}|\[[^\]]+\]|\(.\)|\\\w",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static ValidationResult Validate(string original, string? translation)
    {
        if (string.IsNullOrWhiteSpace(translation))
            return new ValidationResult(true, string.Empty); // UA: порожній = не перекладено, не помилка / EN: empty = untranslated, not an error

        if (RussianOnlyLettersRegex.IsMatch(translation))
            return new ValidationResult(false,
                "UA: Літери ыэёъ відсутні в українському алфавіті (зіскочило в російську) / " +
                "EN: Letters ыэёъ are not in the Ukrainian alphabet (slipped into Russian)");

        var originalCounts = GetMarkerCounts(original);
        if (originalCounts.Count == 0)
            return new ValidationResult(true, string.Empty);

        var translationCounts = GetMarkerCounts(translation);

        // UA: Порівнюємо КІЛЬКІСТЬ кожного маркера, не лише наявність.
        // EN: Compare the COUNT of each marker, not just presence.
        var problems = new List<string>();
        foreach (var (marker, count) in originalCounts.OrderBy(kv => kv.Key))
        {
            var translatedCount = translationCounts.GetValueOrDefault(marker);
            if (translatedCount < count)
                problems.Add(count == 1 ? marker : $"{marker} (×{count}→×{translatedCount})");
        }

        if (problems.Count == 0)
            return new ValidationResult(true, string.Empty);

        var msg = $"UA: Відсутні/втрачені маркери: {string.Join(", ", problems)} / " +
                  $"EN: Missing/lost markers: {string.Join(", ", problems)}";
        return new ValidationResult(false, msg);
    }

    private static Dictionary<string, int> GetMarkerCounts(string text)
    {
        return MarkerRegex.Matches(text)
            .Select(m => m.Value.ToLowerInvariant())
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
