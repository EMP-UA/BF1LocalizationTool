// =============================================================================
// BF1LocalizationTool.GUI — Services/TechnicalStringService.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Автоматично визначає рядки що НЕ потребують перекладу. Критерії:
//       1. Порожній рядок
//       2. Чистий URL
//       3. Лише цифри (включно з десятковими: "5.1", "7.1")
//       4. Лише форматні змінні ({btn...}, %s) без жодного слова
//       5. Один символ
//       6. Короткі символьні комбінації (≤4, без літер)
//       7. Системний код-константа: WORD_WORD (ALL_CAPS З ПІДКРЕСЛЕННЯМИ)
//       8. Внутрішній ідентифікатор: word_word, word1 (snake_case/код, без пробілів)
//       9. Назва клавіші/кнопки введення (розширений словник)
//      10. Числовий множник: "10x", "11x", "6x"
//      11. Скорочення осі джойстика: Ax, Ay, Rx, Ry тощо
//      12. Юридичний/copyright текст (RAD Game Tools, libpng, Bink Video, "Copyright")
//
//     НЕ вважаються технічними:
//       "< Save as New >", "<NOT SPECIFIED>" — текст в дужках з реальними словами
//       "{btn_l2} Zoom {btn_r2}" — має слово "Zoom" для перекладу
//       "-- LOCKED --" — статусний індикатор з реальним словом
//       "2-flag CTF" — назва ігрового режиму, межовий випадок (ручна позначка)
//
// EN: Auto-detects strings that do NOT need translation. See criteria above.
//     NOT technical: bracketed UI text with real words, marker+word combos,
//     status indicators with real words, borderline game-mode abbreviations
//     (use manual "⚙ Tech." toggle for those).
// =============================================================================

using System.Text.RegularExpressions;

namespace BF1LocalizationTool.GUI.Services;

public static class TechnicalStringService
{
    private static readonly Regex PureUrl = new(
        @"^\s*https?://\S+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // UA: Цифри з опціональною десятковою частиною: "42", "5.1", "7.1"
    // EN: Digits with optional decimal part: "42", "5.1", "7.1"
    private static readonly Regex NumericOrDecimal = new(
        @"^\d+(\.\d+)?$", RegexOptions.Compiled);

    // UA: Лише форматні змінні, пробіли та обрамлюючі дужки/символи — без жодного слова.
    //     Ловить: "%s", "{btn_a}", "<   %s   >", "[ %d ]"
    // EN: Only format variables, spaces, and surrounding brackets — no real words.
    //     Catches: "%s", "{btn_a}", "<   %s   >", "[ %d ]"
    private static readonly Regex OnlyFormatVars = new(
        @"^[\s<>\[\]()]*((%[sdifcux%]|\{[^}]+\})[\s<>\[\]()]*)+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex OnlySymbols = new(
        @"^[^a-zA-Z\u0400-\u04FF\d]+$", RegexOptions.Compiled);

    // UA: ABC_DEF — мінімум одне підкреслення, ВЕЛИКІ літери/цифри
    // EN: ABC_DEF — at least one underscore, UPPERCASE letters/digits
    private static readonly Regex CodeConstant = new(
        @"^[A-Z][A-Z0-9]*(_[A-Z0-9]+)+$", RegexOptions.Compiled);

    // UA: Внутрішній ідентифікатор: слово_слово або слово+цифра, без пробілів.
    //     Ловить: "reb_destruct1", "imp_destruct3", "Abnt_c1", "Abnt_c2"
    //     НЕ ловить: звичайні слова без підкреслення/без приліплених цифр
    // EN: Internal identifier: word_word or word+digit, no spaces.
    //     Catches: "reb_destruct1", "imp_destruct3", "Abnt_c1", "Abnt_c2"
    private static readonly Regex InternalIdentifier = new(
        @"^[A-Za-z][A-Za-z]*(_[A-Za-z0-9]+|[0-9]+)+$", RegexOptions.Compiled);

    // UA: Множник: "10x", "11x", "6x"
    // EN: Multiplier: "10x", "11x", "6x"
    private static readonly Regex Multiplier = new(
        @"^\d+[xX]$", RegexOptions.Compiled);

    // UA: Скорочення осей джойстика — точний список (короткі, легко сплутати зі словами)
    // EN: Joystick axis abbreviations — exact list (short, easy to confuse with words)
    private static readonly HashSet<string> AxisAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    { "Ax", "Ay", "Az", "Rx", "Ry", "Rz", "Vx", "Vy", "Vz" };

    // UA: Назви клавіш/кнопок введення — розширений словник
    // EN: Input key/button names — extended dictionary
    private static readonly Regex KeyName = new(
        @"^(" +
            @"Page (Up|Down)|" +
            @"Num Pad (\d|Enter|Comma|Equals|Plus|Minus|Period|Multiply|Divide)|" +
            @"Joystick (Hat \d+ (Up|Down|Left|Right)|Button \d+|" +
                @"R?[XYZ] ?[+-]?|Slider \d+ ?[+-]?|POV ?\d*)|" +
            @"(Left|Right) (Shift|Alt|Ctrl|Control|Arrow|Mouse Button \d*|Windows Key)|" +
            @"Mouse (Button|Wheel|X Axis|Y Axis) ?\d*( (Up|Down))?|" +
            @"(Up|Down) Arrow|" +
            @"Volume (Up|Down|Mute)|" +
            @"Num Lock|Caps ?Lock|Scroll Lock|Print Screen|Windows Key|" +
            @"F1[0-9]|F[1-9]|" +
            @"Tab|Pause|Insert|Delete|Home|End|Backspace|Enter|Escape|Esc|Space|Yen|" +
            @"Up|Down|Left|Right|Shift|Alt|Ctrl|Control" +
        @")$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // UA: Юридичні/copyright позначення сторонніх бібліотек — не перекладаються за конвенцією
    // EN: Legal/copyright notices of third-party libraries — not translated by convention
    private static readonly string[] LegalMarkers =
    {
        "Copyright", "RAD Game Tools", "libpng", "zlib", "Bink Video",
        "All rights reserved", "GameSpy Industries", "Dolby"
    };

    // UA: Дамп символів шрифтового атласу: суцільний алфавіт/цифри/пунктуація
    //     без пробілів між словами (тобто це НЕ речення, а таблиця гліфів)
    //     Приклад: "ABCDEFGHIJKLMNOPQRSTUVWXYZ-.' 1234567890"
    // EN: Font glyph atlas character dump: solid alphabet/digits/punctuation
    //     with no word-spacing (i.e. NOT a sentence, but a glyph table)
    //     Example: "ABCDEFGHIJKLMNOPQRSTUVWXYZ-.' 1234567890"
    private static readonly Regex FontGlyphDump = new(
        @"^[A-Z\d\s\-\.'""]{15,}$", RegexOptions.Compiled);

    // UA: Дамп розширених Latin-1 символів (¡-¿, À-ÿ) — продовження шрифтового
    //     тест-рядка для діакритики/спецсимволів. Часто виглядає як "mojibake"
    //     (Â¡Ã· тощо), якщо UTF-8 байти прочитані як Latin1, але по суті це
    //     службовий рядок гри для перевірки гліфів, не текст для перекладу.
    // EN: Extended Latin-1 symbol dump (¡-¿, À-ÿ) — continuation of the font
    //     test string for diacritics/special chars. Often looks like "mojibake"
    //     (Â¡Ã· etc.) if UTF-8 bytes are misread as Latin1, but it's essentially
    //     the game's internal glyph-test string, not translatable text.
    private static readonly Regex ExtendedGlyphDump = new(
        @"^[\u0080-\u00FF\s]{8,}$", RegexOptions.Compiled);

    // UA: Короткі UI-абревіатури що традиційно не перекладаються в іграх
    // EN: Short UI abbreviations conventionally left untranslated in games
    private static readonly HashSet<string> UiAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "ON", "OFF", "OK", "VS.", "VS", "VSYNC", "PC", "PSI",
        "XBOX", "XBOX LIVE", "PS2", "PS3", "PS4", "USB", "LED", "FPS",
        "HUD", "GUI", "AI", "NPC", "OS", "RAM", "CPU", "GPU"
    };

    public static bool IsTechnical(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        var t = text.Trim();

        if (PureUrl.IsMatch(text)) return true;
        if (NumericOrDecimal.IsMatch(t)) return true;
        if (OnlyFormatVars.IsMatch(text)) return true;
        if (t.Length == 1) return true;
        if (t.Length <= 4 && OnlySymbols.IsMatch(t)) return true;
        if (CodeConstant.IsMatch(t)) return true;
        if (Multiplier.IsMatch(t)) return true;
        if (AxisAbbreviations.Contains(t)) return true;
        if (KeyName.IsMatch(t)) return true;
        if (InternalIdentifier.IsMatch(t)) return true;
        if (UiAbbreviations.Contains(t)) return true;
        // UA: Перевіряємо дамп гліфів лише якщо немає реальних пробілів-між-словами
        //     (інакше речення типу "ALL CAPS WARNING TEXT" теж попадуть)
        //     Критерій: довжина ≥15 і немає жодного слова довшого за 1 символ
        //     розділеного простим пробілом — тобто це або одне довге "слово"
        //     без пробілів, або послідовність одиночних літер/символів
        // EN: Check glyph dump only if there's no real word-spacing
        //     (otherwise sentences like "ALL CAPS WARNING TEXT" would match too)
        if (t.Length >= 15 && FontGlyphDump.IsMatch(t) &&
            !System.Text.RegularExpressions.Regex.IsMatch(t, @"[A-Z]{2,}\s[A-Z]{2,}"))
            return true;
        if (t.Length >= 8 && ExtendedGlyphDump.IsMatch(t))
            return true;

        // UA: Юридичний текст — шукаємо підрядок, бо це повне речення з маркером всередині
        // EN: Legal text — substring search, since it's a full sentence with marker inside
        foreach (var marker in LegalMarkers)
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}