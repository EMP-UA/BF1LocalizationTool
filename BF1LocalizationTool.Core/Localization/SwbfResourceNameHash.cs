// =============================================================================
// BF1LocalizationTool.Core — Localization/SwbfResourceNameHash.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: ДРУГИЙ хеш рушія — той, яким Battlefront II адресує ІМЕНА РЕСУРСІВ
//     (шрифти, сегменти роликів, назви відеофайлів), на відміну від
//     `SwbfStringHash`, що обслуговує КЛЮЧІ локалізації `Locl`.
//
//     ЧОМУ ЦЕ ОКРЕМА ФУНКЦІЯ, А НЕ КОПІЯ. Обидві — FNV-1a 32 біти з тим
//     самим базисом 0x811C9DC5 і множником 0x01000193. Різниця рівно в
//     одному: як символ зводиться до нижнього регістру.
//
//         `SwbfStringHash`  : tolower(c)      — класичне опускання регістру
//         ця функція        : c | 0x20        — побітове АБО, як у рушії
//
//     Для латинських літер результат однаковий, тому на іменах на кшталт
//     "ingame" обидві дають те саме значення (саме тому розбіжність довго
//     не помічалась). Але для `_` (0x5F -> 0x7F), для цифр і для будь-якого
//     символу з бітом 0x20 у нулі результати РОЗХОДЯТЬСЯ. Через це хеші
//     імен виду `gamefont_small` за `SwbfStringHash` не збігаються ні з
//     чим у грі: для ідентифікації РЕСУРСІВ (шрифт, сегмент ролика, файл)
//     потрібна саме ЦЯ функція, а не `SwbfStringHash`.
//
//     ЗВІДКИ ВЗЯТО (не здогад — дизасемблювання, тільки читання exe):
//     `BattlefrontII.exe`, VA 0x00726E50 (обгортка-конструктор рядка —
//     0x00726D20, яка кладе результат у перше поле об'єкта):
//
//         h = 0x811C9DC5
//         для кожного байта c:
//             c = знакове розширення до int    ; movsx ecx, cl
//             c |= 0x20                        ; or ecx, 0x20
//             h ^= c
//             h *= 0x01000193                  ; imul eax, eax, 0x1000193
//
//     ПЕРЕВІРКА (усі п'ять збіглися точно): імена шрифтів, вичитані з
//     `core.lvl`, дають рівно ті значення, що лежать у таблиці всередині
//     самого exe за адресами 0x007DF534..0x007DF544:
//         gamefont_large      -> 0xB3E564DE
//         gamefont_medium     -> 0xAA8876FC
//         gamefont_small      -> 0x8F4B7122   (шрифт субтитрів у mcfg)
//         gamefont_tiny       -> 0x46F060D5   (запасний шрифт субтитрів)
//         gamefont_super_tiny -> 0x548190D5
//
//     КОЛИ ЩО ВЖИВАТИ:
//       • ключ локалізації (`level.tat3.objectives.1`) -> `SwbfStringHash`;
//       • ім'я ресурсу (шрифт, сегмент ролика, файл) -> ЦЯ функція.
//     Якщо в імені немає ні `_`, ні цифр, ні великих літер поза ASCII —
//     обидві дадуть однаковий результат, але покладатися на це не варто.
//
// EN: The engine's SECOND hash — the one Battlefront II uses to address
//     RESOURCE NAMES (fonts, movie segments, movie file names), as opposed
//     to `SwbfStringHash`, which serves `Locl` localization KEYS.
//
//     WHY A SEPARATE FUNCTION AND NOT A COPY. Both are 32-bit FNV-1a with
//     the same basis 0x811C9DC5 and prime 0x01000193. They differ in exactly
//     one thing: how a character is lowercased.
//
//         `SwbfStringHash`  : tolower(c)      — proper lowercasing
//         this function     : c | 0x20        — a bitwise OR, as the engine does
//
//     For Latin letters the result is identical, so names like "ingame"
//     hash the same under both (which is why the discrepancy went unnoticed
//     for a long time). But for `_` (0x5F -> 0x7F), for digits and for any
//     character with bit 0x20 clear, the results DIVERGE. That is why hashes
//     of names such as `gamefont_small` under `SwbfStringHash` match nothing
//     in the game: identifying RESOURCE names (fonts, movie segments, files)
//     requires this function, not `SwbfStringHash`.
//
//     SOURCE (not a guess — disassembly, read-only): `BattlefrontII.exe`,
//     VA 0x00726E50 (the string-constructor wrapper 0x00726D20 stores the
//     result into the object's first field):
//
//         h = 0x811C9DC5
//         for each byte c:
//             c = sign-extended to int         ; movsx ecx, cl
//             c |= 0x20                        ; or ecx, 0x20
//             h ^= c
//             h *= 0x01000193                  ; imul eax, eax, 0x1000193
//
//     VERIFICATION (all five matched exactly): the font names read out of
//     `core.lvl` produce precisely the values stored in the table inside the
//     exe at 0x007DF534..0x007DF544 — see the list above.
//
//     WHEN TO USE WHICH:
//       • a localization key (`level.tat3.objectives.1`) -> `SwbfStringHash`;
//       • a resource name (font, movie segment, file) -> THIS function.
//     If a name has no `_`, no digits and no non-ASCII, both give the same
//     result — but it is not safe to rely on that.
// =============================================================================

namespace BF1LocalizationTool.Core.Localization;

public static class SwbfResourceNameHash
{
    private const uint OffsetBasis = 2166136261u; // 0x811C9DC5
    private const uint Prime = 16777619u;         // 0x01000193

    // -------------------------------------------------------------------------
    // UA: Обчислює хеш імені ресурсу так само, як це робить рушій.
    //     Ім'я має бути ASCII: рушій працює з однобайтовими рядками, і
    //     будь-який не-ASCII символ дав би тут інший результат, ніж у грі,
    //     тому такий випадок краще відхилити явно, ніж тихо порахувати
    //     неправильно.
    // EN: Computes a resource-name hash exactly as the engine does. The name
    //     must be ASCII: the engine works with single-byte strings, and any
    //     non-ASCII character would hash differently here than in the game,
    //     so such a case is better rejected loudly than silently miscomputed.
    // -------------------------------------------------------------------------
    public static uint Compute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var hash = OffsetBasis;

        foreach (var ch in name)
        {
            if (ch > 0x7F)
                throw new ArgumentException(
                    $"UA: Ім'я ресурсу мусить бути ASCII, знайдено '{ch}' у \"{name}\". / " +
                    $"EN: A resource name must be ASCII, found '{ch}' in \"{name}\".",
                    nameof(name));

            // UA: Рушій розширює байт ЗІ ЗНАКОМ, потім робить `c |= 0x20`.
            //     Для ASCII (0..0x7F) знакове розширення нічого не змінює,
            //     тож достатньо самого АБО — але лишаємо коментар, щоб
            //     подальші правки не «оптимізували» логіку хибно, якщо
            //     колись доведеться рахувати не-ASCII.
            // EN: The engine sign-extends the byte, then applies `c |= 0x20`.
            //     For ASCII (0..0x7F) sign extension changes nothing, so the
            //     OR alone suffices — the comment stays so a future edit does
            //     not "optimize" the logic wrongly if non-ASCII ever matters.
            var c = (uint)(ch | 0x20);

            hash ^= c;
            hash *= Prime;
        }

        return hash;
    }
}
