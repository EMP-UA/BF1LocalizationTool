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
    // UA: Шаблон спільний із TranslationCaseAdapter (маркери, чий регістр
    //     не змінюється при узгодженні регістру перекладу).
    // EN: The pattern is shared with TranslationCaseAdapter (markers whose
    //     case is never changed when aligning a translation's case).
    internal const string MarkerPattern = @"%[sdifcux%]|\{[^}]+\}|\[[^\]]+\]|\(.\)|\\\w";

    // UA: Маркери, які ПЕРЕВІРЯЄ валідація:
    //       • формат (%s %d …) і підстановки у фігурних дужках ({OptionR},
    //         {btna} …) — рушій підставляє їх сам;
    //       • назви клавіш у квадратних дужках, які гравець має побачити
    //         саме такими: 1-3 символи ([E], [1], [+], [TK]), F1-F12 і
    //         відомі імена (SPACE, SPACEBAR, ENTER, ALT, SHIFT, CTRL, TAB,
    //         ESC, CAPS LOCK, MOUSE BUTTON n, MOUSE WHEEL UP/DOWN);
    //       • кнопка в круглих дужках, НЕ приліплена до слова ("press (X)");
    //       • текстові екранування (\n).
    //     Решта дужок у рядках BF1/BF2 — звичайний текст, який перекладають:
    //     позначки ([locked]/[LOCKED], [Screen saver also on]), множина,
    //     приліплена до слова (Map(s) → мапу(и)). Повний MarkerPattern
    //     лишається для TranslationCaseAdapter — там він лише захищає ці
    //     фрагменти від зміни регістру.
    // EN: Markers VALIDATION checks:
    //       • format specifiers (%s %d …) and curly-brace substitutions
    //         ({OptionR}, {btna} …) — the engine substitutes them itself;
    //       • key names in square brackets, which the player must see as-is:
    //         1-3 characters ([E], [1], [+], [TK]), F1-F12 and known names
    //         (SPACE, SPACEBAR, ENTER, ALT, SHIFT, CTRL, TAB, ESC, CAPS LOCK,
    //         MOUSE BUTTON n, MOUSE WHEEL UP/DOWN);
    //       • a button in round brackets NOT glued to a word ("press (X)");
    //       • text escapes (\n).
    //     Other brackets in BF1/BF2 strings are ordinary, translatable text:
    //     labels ([locked]/[LOCKED], [Screen saver also on]), a plural glued
    //     to a word (Map(s) → мапу(и)). The full MarkerPattern stays for
    //     TranslationCaseAdapter — there it only shields these fragments
    //     from case changes.
    internal const string ValidationMarkerPattern =
        @"%[sdifcux%]|\{[^}]+\}" +
        @"|\[(?:[A-Z0-9+\-]{1,3}|F\d{1,2}|SPACE|SPACEBAR|ENTER|ALT|SHIFT|CTRL|TAB|ESC|CAPS LOCK" +
        @"|MOUSE BUTTON \d|MOUSE WHEEL (?:UP|DOWN))\]" +
        @"|(?<!\p{L})\(.\)|\\\w";

    private static readonly Regex MarkerRegex = new(
        ValidationMarkerPattern,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static ValidationResult Validate(string original, string? translation)
    {
        if (string.IsNullOrWhiteSpace(translation))
            return new ValidationResult(true, string.Empty); // UA: порожній = не перекладено, не помилка / EN: empty = untranslated, not an error

        if (RussianOnlyLettersRegex.IsMatch(translation))
            return new ValidationResult(false,
                "UA: Літери ыэёъ відсутні в українському алфавіті (зіскочило в російську) / " +
                "EN: Letters ыэёъ are not in the Ukrainian alphabet (slipped into Russian)");

        var originalCounts    = GetMarkerCounts(original);
        var translationCounts = GetMarkerCounts(translation);
        if (originalCounts.Count == 0 && translationCounts.Count == 0)
            return new ValidationResult(true, string.Empty);

        // UA: Порівнюється КІЛЬКІСТЬ кожного маркера в обидва боки: за
        //     об'єднанням ключів оригіналу й перекладу, тож маркер, якого
        //     в оригіналі немає зовсім, теж потрапляє в порівняння.
        // EN: The COUNT of each marker is compared in both directions, over
        //     the union of the original's and the translation's keys, so a
        //     marker absent from the original entirely is compared too.
        var lost  = new List<string>();
        var extra = new List<string>();
        foreach (var marker in originalCounts.Keys.Union(translationCounts.Keys).OrderBy(k => k))
        {
            var count           = originalCounts.GetValueOrDefault(marker);
            var translatedCount = translationCounts.GetValueOrDefault(marker);
            if (translatedCount < count)
                lost.Add(count == 1 ? marker : $"{marker} (×{count}→×{translatedCount})");
            else if (translatedCount > count)
                extra.Add($"{marker} (×{count}→×{translatedCount})");
        }

        if (lost.Count == 0 && extra.Count == 0)
            return new ValidationResult(true, string.Empty);

        var parts = new List<string>();
        if (lost.Count > 0)
            parts.Add($"UA: Відсутні/втрачені маркери: {string.Join(", ", lost)} / " +
                      $"EN: Missing/lost markers: {string.Join(", ", lost)}");
        if (extra.Count > 0)
            parts.Add($"UA: Зайві маркери (у перекладі більше, ніж в оригіналі): {string.Join(", ", extra)} / " +
                      $"EN: Extra markers (more in the translation than in the original): {string.Join(", ", extra)}");
        return new ValidationResult(false, string.Join("; ", parts));
    }

    // UA: Справжній перенос рядка В САМИХ ДАНИХ (не текстове екранування
    //     на кшталт "\n" — те вже ловить \\w у ValidationMarkerPattern вище). LoclChunkParser
    //     читає рядок як є (Encoding.Unicode.GetString), без жодного
    //     розекранування — тому CRLF у ванільному core.lvl (BF2) це СПРАВЖНІ
    //     символи CR(0x0D)/LF(0x0A) у даних, підтверджено прямим розбором
    //     ванільного english core.lvl (BF2): 332 записи містять реальний CRLF.
    //     Без цього регексу втрата переносу рядка в перекладі проходила БЕЗ
    //     жодної позначки "Проблема" — цей шаблон закриває саме цю прогалину.
    // EN: A real line break IN THE DATA ITSELF (not text-escaped like "\n" —
    //     \\w in ValidationMarkerPattern above already catches that). LoclChunkParser
    //     reads the string as-is (Encoding.Unicode.GetString), with no
    //     unescaping at all — so CRLF in vanilla core.lvl (BF2) is a REAL
    //     CR(0x0D)/LF(0x0A) character in the data, confirmed by direct parsing
    //     of the vanilla English core.lvl (BF2): 332 entries contain a real
    //     CRLF. Without this regex, a translation dropping a line break passed
    //     with NO "Issue" flag at all — this pattern closes exactly that gap.
    public static readonly Regex LineBreakRegex = new(@"\r\n|\r|\n", RegexOptions.Compiled);

    private static Dictionary<string, int> GetMarkerCounts(string text)
    {
        var counts = MarkerRegex.Matches(text)
            .Select(m => m.Value.ToLowerInvariant())
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());

        // UA: Реальні переноси рядка рахуються ОКРЕМО й додаються під тим
        //     самим ключем "\n", що й текстове екранування: у повідомленні
        //     про втрачений маркер немає різниці між буквальним текстом "\n"
        //     і справжнім символом у даних.
        // EN: Real line breaks are counted SEPARATELY and merged under the
        //     same "\n" key as the text escape: the missing-marker message
        //     does not distinguish literal text "\n" from an actual
        //     character in the data.
        var lineBreakCount = LineBreakRegex.Matches(text).Count;
        if (lineBreakCount > 0)
            counts["\n"] = counts.GetValueOrDefault("\n") + lineBreakCount;

        return counts;
    }
}
