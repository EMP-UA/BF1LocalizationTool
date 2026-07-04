// =============================================================================
// BF1LocalizationTool.GUI — Services/ValidationService.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Перевіряє що технічні маркери з оригіналу збережені в перекладі.
//     Маркери: %s %d %i %f %c %u \n \t [змінні] (X) тощо.
// EN: Checks that technical markers from original are preserved in translation.
//     Markers: %s %d %i %f %c %u \n \t [variables] (X) etc.
// =============================================================================

using System.Text.RegularExpressions;

namespace BF1LocalizationTool.GUI.Services;

public record ValidationResult(bool IsValid, string Message);

public static class ValidationService
{
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

        var originalMarkers = GetMarkers(original);
        if (originalMarkers.Count == 0)
            return new ValidationResult(true, string.Empty);

        var translationMarkers = GetMarkers(translation);
        var missing = originalMarkers.Except(translationMarkers).ToList();

        if (missing.Count == 0)
            return new ValidationResult(true, string.Empty);

        var msg = $"UA: Відсутні маркери: {string.Join(", ", missing)} / " +
                  $"EN: Missing markers: {string.Join(", ", missing)}";
        return new ValidationResult(false, msg);
    }

    private static List<string> GetMarkers(string text)
    {
        return MarkerRegex.Matches(text)
            .Select(m => m.Value.ToLowerInvariant())
            .OrderBy(x => x)
            .ToList();
    }
}
