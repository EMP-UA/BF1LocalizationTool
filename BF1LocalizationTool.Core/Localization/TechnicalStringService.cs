// =============================================================================
// BF1LocalizationTool.Core — Localization/TechnicalStringService.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Автоматично визначає рядки що НЕ потребують перекладу. Критерії:
//       1. Порожній рядок
//       2. Чистий URL
//       3. Лише цифри (включно з десятковими: "5.1", "7.1")
//       4. Лише форматні змінні ({btn...}, %s) + обрамлююча пунктуація
//          (напр. "%s." — крапка в кінці НЕ робить рядок перекладним)
//       5. Один символ
//       6. Короткі символьні комбінації (≤4, без літер)
//       7. Системний код-константа: WORD_WORD (ALL_CAPS З ПІДКРЕСЛЕННЯМИ)
//       8. Внутрішній ідентифікатор: word_word, word1 (snake_case/код, без пробілів)
//       9. Назва клавіші/кнопки введення (розширений словник)
//      10. Числовий множник: "10x", "11x", "6x"
//      11. Скорочення осі джойстика: Ax, Ay, Rx, Ry тощо
//      12. Юридичний/copyright текст (RAD Game Tools, libpng, Bink Video, "Copyright")
//      13. Куровані списки, які РЕГЕКСОМ надійно не відрізнити від живого
//          тексту (див. нижче) — джерело: translator-review.txt, реальні
//          рядки, які ШІ-перекладач сам класифікував як "залишити як є".
//
//     ЧОМУ КУРОВАНІ СПИСКИ, А НЕ РЕГЕКС:
//       - Чит-код "FODDER" неможливо відрізнити патерном від пункту меню
//         "LOAD" — обидва ALL-CAPS-слово. Тому чит-коди — явний список.
//       - Дефіс-абревіатури як "AT-ST" регекс `^[A-Z]+(-[A-Z]+)+$` зловив
//         би, АЛЕ разом із ними хибно зловив би "AUTO-SAVE"/"ANTI-ALIAS"
//         (реальні налаштування, що ПОТРЕБУЮТЬ перекладу). Тому позначення
//         техніки — теж явний список відомого набору Battlefront.
//       - "TIE Bomber/Fighter/Interceptor" СВІДОМО НЕ тут: попри "TIE" без
//         кирилиці, слова Bomber/Fighter/Interceptor ПЕРЕКЛАДНІ
//         (Бомбардувальник/Винищувач/Перехоплювач) — емпірично видно в
//         translator-review, де ШІ інколи їх не переклав; занесення їх у
//         технічні сховало б реальну роботу.
//
//     НЕ вважаються технічними:
//       "< Save as New >", "<NOT SPECIFIED>" — текст в дужках з реальними словами
//       "{btn_l2} Zoom {btn_r2}" — має слово "Zoom" для перекладу
//       "-- LOCKED --" — статусний індикатор з реальним словом
//       "2-flag CTF" — назва ігрового режиму, межовий випадок (ручна позначка)
//       "TIE Bomber", "Space Overview", "Load" тощо — живий текст, перекладний
//
//     ПЕРЕНЕСЕНО з BF1LocalizationTool.GUI.Services у Core: логіка не має
//     WPF-залежностей і потрібна також консольному ШІ-перекладачу
//     (BF1LocalizationTool.Translator) для локальної фільтрації рядків
//     ПЕРЕД відправкою на переклад — без дублювання коду між GUI і
//     Translator. Публічний API (клас/метод) не змінився, тож існуючий
//     GUI-код працює без правок логіки, лише зміна using.
//
// EN: Auto-detects strings that do NOT need translation. See criteria above.
//     NOT technical: bracketed UI text with real words, marker+word combos,
//     status indicators with real words, borderline game-mode abbreviations
//     (use manual "⚙ Tech." toggle for those).
//
//     MOVED from BF1LocalizationTool.GUI.Services into Core: this logic has
//     no WPF dependency and is also needed by the console AI translator
//     (BF1LocalizationTool.Translator) for local string filtering BEFORE
//     sending anything to the API — avoids duplicating code between GUI and
//     Translator. Public API (class/method) unchanged, so existing GUI code
//     works with just a using-directive change, no logic changes.
// =============================================================================

using System.Text.RegularExpressions;

namespace BF1LocalizationTool.Core.Localization;

public static class TechnicalStringService
{
    private static readonly Regex PureUrl = new(
        @"^\s*https?://\S+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // UA: Цифри з опціональною десятковою частиною: "42", "5.1", "7.1"
    // EN: Digits with optional decimal part: "42", "5.1", "7.1"
    private static readonly Regex NumericOrDecimal = new(
        @"^\d+(\.\d+)?$", RegexOptions.Compiled);

    // UA: Лише форматні змінні, пробіли та обрамлююча пунктуація — без жодного слова.
    //     Ловить: "%s", "{btn_a}", "<   %s   >", "[ %d ]", "%s." , "%d:", "%s!"
    //     Пунктуація .,:;!?…- включена в обрамлюючий клас: без неї "%s." не
    //     зловилось б лише через крапку в кінці, хоча перекладати там
    //     нічого — лише змінна й знак.
    // EN: Only format variables, spaces, and surrounding punctuation — no real words.
    //     Catches: "%s", "{btn_a}", "<   %s   >", "[ %d ]", "%s.", "%d:", "%s!"
    //     Punctuation .,:;!?…- is included in the surrounding class: without
    //     it, "%s." wouldn't match just because of the trailing dot, even
    //     though there's nothing to translate — only a variable and a mark.
    private static readonly Regex OnlyFormatVars = new(
        @"^[\s<>\[\]().,:;!?…\-]*((%[sdifcux%]|\{[^}]+\})[\s<>\[\]().,:;!?…\-]*)+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // UA: Чит-коди гри (вводяться гравцем ДОСЛІВНО — мають лишатись байт-у-байт).
    //     Джерело: translator-review.txt (ШІ повернув їх без змін). Регексом
    //     не відрізнити від слів меню, тому явний список.
    // EN: Game cheat codes (typed VERBATIM by the player — must stay byte-for-byte).
    //     Source: translator-review.txt (the AI returned them unchanged). Not
    //     distinguishable from menu words by regex, hence an explicit list.
    private static readonly HashSet<string> GameCheatCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "KILLERQUEEN", "THATSNOMOON", "GOTPOPCORN", "NOHONOR", "IGIVEUP",
        "LORDOFSITH", "LETSDANCE", "ALITTLEHELP", "WHYYOUFAIL", "YOURMASTERS",
        "CHEWIERULES", "FODDER", "CARBONCOPY", "SUPERFODDER", "D347H 574R",
    };

    // UA: Позначення техніки/юнітів усесвіту Star Wars — залишаються як є за
    //     конвенцією (впізнавані, як-от "AT-ST"). Відомий скінченний набір
    //     Battlefront, тому явний список (регекс дефісів хибно ловив би
    //     "AUTO-SAVE" тощо). НЕ містить "TIE Bomber/Fighter/..." — вони
    //     перекладні (див. заголовок). За потреби транслітерації — зняти
    //     позначку вручну.
    // EN: Star Wars vehicle/unit designations — kept as-is by convention
    //     (recognizable, e.g. "AT-ST"). A known finite Battlefront set, hence
    //     an explicit list (a hyphen regex would wrongly catch "AUTO-SAVE"
    //     etc.). Does NOT include "TIE Bomber/Fighter/..." — those are
    //     translatable (see header). Untoggle manually if transliteration is wanted.
    private static readonly HashSet<string> VehicleUnitDesignations = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT-AT", "AT-ST", "AT-TE", "AT-PT", "AT-RT", "AT-XT",
        "IFT-T", "IFT-X", "SPHA-T",
        "X-WING", "Y-WING", "A-WING", "B-WING", "K-WING", "V-WING",
        "AAT", "MTT", "MAF", "STAP",
    };

    // UA: Сторонні продукти/бібліотеки/титри розробників + системні/IME-клавіші
    //     та мережеві мітки — власні назви й службові позначки, що не
    //     перекладаються. Джерело: translator-review.txt.
    // EN: Third-party products/libraries/dev credits + system/IME keys and
    //     network labels — proper names and service markers, not translated.
    //     Source: translator-review.txt.
    private static readonly HashSet<string> ProperNamesAndServiceLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        // UA: продукти/бібліотеки/титри / EN: products/libraries/credits
        "OpenAL", "OptiMatch", "GameSpy", "GameSpy ID",
        "Lucas Licensing", "Beta Breakers", "Burning Goddesses", "Enzyme Labs",
        "Star Wars Battlefront",
        // UA: системні/IME-клавіші та елементи введення / EN: system/IME keys and input controls
        //     "Return" — клавіша Return/Enter (підтверджено користувачем; у BF1
        //     як "RETURN", у BF2 як "Return" — збіг без урахування регістру).
        //     "Return" — the Return/Enter key (user-confirmed; "RETURN" in BF1,
        //     "Return" in BF2 — case-insensitive match covers both).
        "KANA", "KANJI", "CONVERT", "NOCONVERT", "SYSTEM RQ", "D-Pad", "Return",
        // UA: мережеві мітки / EN: network labels
        "IP:", "IP :", "T1+", "T1 * 2",
    };

    private static readonly Regex OnlySymbols = new(
        @"^[^a-zA-ZЀ-ӿ\d]+$", RegexOptions.Compiled);

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
            // UA: РОЗШИРЕНІ мультимедійні клавіші клавіатури — та сама
            //     категорія, що й "Volume Up/Down/Mute" вище, лише інші
            //     конкретні клавіші (той самий екран перепризначення
            //     клавіш у грі).
            // EN: EXTENDED multimedia keyboard keys — the SAME category as
            //     "Volume Up/Down/Mute" above, just different specific
            //     keys (the same key-rebinding screen in the game).
            @"Mute|Play Pause|Web Home|App Menu Key|Wake|" +
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
        @"^[-ÿ\s]{8,}$", RegexOptions.Compiled);

    // UA: Короткі UI-абревіатури, що традиційно не перекладаються в іграх,
    //     включно з "CEO"/"ID"/"XL" — знайдено емпірично в реальних "Без
    //     перекладу" рядках core.lvl (BF1 і BF2): усі три — універсальні
    //     скорочення, які в українській локалізації ігор традиційно лишають
    //     як є (немає короткого відповідника без втрати впізнаваності:
    //     "ID" не "Ід.", "CEO" не абревіатура з укр. слів, "XL" — розмір,
    //     як у S/M/L/XL). "ID" зустрічається В ОБОХ іграх незалежно — не
    //     випадковість.
    // EN: Short UI abbreviations conventionally left untranslated in games,
    //     including "CEO"/"ID"/"XL" — found empirically in real
    //     "Untranslated" core.lvl strings (BF1 and BF2): all three are
    //     universal abbreviations conventionally kept as-is in Ukrainian
    //     game localization (no short equivalent without losing
    //     recognizability: "ID" not "Ід.", "CEO" isn't a Ukrainian-letter
    //     abbreviation, "XL" is a size code like S/M/L/XL). "ID" appears
    //     in BOTH games independently — not a coincidence.
    private static readonly HashSet<string> UiAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "ON", "OFF", "OK", "VS.", "VS", "VSYNC", "PC", "PSI",
        "XBOX", "XBOX LIVE", "PS2", "PS3", "PS4", "USB", "LED", "FPS",
        "HUD", "GUI", "AI", "NPC", "OS", "RAM", "CPU", "GPU",
        "CEO", "ID", "XL"
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
        // UA: Куровані списки (див. заголовок — чому не регекс).
        // EN: Curated lists (see header — why not a regex).
        if (GameCheatCodes.Contains(t)) return true;
        if (VehicleUnitDesignations.Contains(t)) return true;
        if (ProperNamesAndServiceLabels.Contains(t)) return true;
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
