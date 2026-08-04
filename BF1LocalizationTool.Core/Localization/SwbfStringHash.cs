// =============================================================================
// BF1LocalizationTool.Core — Localization/SwbfStringHash.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Хеш-функція, якою Star Wars Battlefront (2004/2005) адресує рядки
//     локалізації. Це FNV-1a, 32 біти, над ASCII-байтами КЛЮЧА.
//
//     ЩО САМЕ ХЕШУЄТЬСЯ — ключ, а НЕ показуваний текст. Ключі мають вигляд
//     крапкового шляху в нижньому регістрі:
//         level.tat3.objectives.1
//         ifs.onlineopt.voicemask
//         common.no
//     а в таблиці `Locl` за цим хешем лежить ВІДОБРАЖУВАНИЙ рядок
//     ("CAPTURE AND HOLD CPS" тощо). Саме тому хешування самого тексту
//     ("TATOOINE: DUNE SEA") ніколи не дає збігу.
//
//     ПІДТВЕРДЖЕНО емпірично на реальному core.lvl BF1 (2446 відомих
//     хешів): ключі, вичеплені з Lua-байткоду самої гри, дають ТОЧНИЙ
//     збіг за цією формулою:
//         fnv1a("level.tat3.objectives.1") = 0x1CDDFE52 → "CAPTURE AND HOLD CPS"
//         fnv1a("ifs.onlineopt.voicemask")  → присутній у таблиці
//         fnv1a("ifs.soundopt.speechvol")   → присутній у таблиці
//         fnv1a("common.no")                → присутній у таблиці
//     FNV-1 (не -1a), djb2, sdbm і CRC32 збігів не дають у жодному з
//     варіантів (raw/lower/upper).
//
//     Ця функція дозволяє ГЕНЕРУВАТИ нові хеші — тобто додавати нові
//     рядки локалізації під власними ключами (не лише правити текст під
//     наявними хешами). Практичний приклад — назва карти аддону Tat3, яка
//     живе поза `Locl` (див. `PatchAddOnMapNameCommand` і
//     FONT_FORMAT_SPEC §13).
//
// EN: The hash function Star Wars Battlefront (2004/2005) uses to address
//     localization strings. It is FNV-1a, 32-bit, over the ASCII bytes of
//     the KEY.
//
//     WHAT IS HASHED — the key, NOT the displayed text. Keys look like a
//     lowercase dotted path (level.tat3.objectives.1, common.no), and the
//     `Locl` table stores the DISPLAYED string under that hash. This is
//     exactly why hashing the text itself ("TATOOINE: DUNE SEA") never
//     matches.
//
//     CONFIRMED empirically against the real BF1 core.lvl (2446 known
//     hashes): keys extracted from the game's own Lua bytecode match
//     EXACTLY under this formula (see the UA list above). FNV-1 (not -1a),
//     djb2, sdbm and CRC32 do NOT match in any raw/lower/upper variant.
//
//     This function allows GENERATING new hashes — i.e. adding new
//     localization strings under our own keys (not just editing text
//     behind existing hashes). Practical example — the Tat3 add-on's map
//     name, which lives outside `Locl` (see `PatchAddOnMapNameCommand` and
//     FONT_FORMAT_SPEC §13).
// =============================================================================

using System.Text;

namespace BF1LocalizationTool.Core.Localization;

public static class SwbfStringHash
{
    // UA: Канонічні константи FNV-1a (32 біт).
    // EN: Canonical FNV-1a (32-bit) constants.
    private const uint OffsetBasis = 2166136261u; // 0x811C9DC5
    private const uint Prime       = 16777619u;   // 0x01000193

    // -------------------------------------------------------------------------
    // UA: Обчислює хеш ключа локалізації.
    //
    //     Регістр: ключі в грі записані малими літерами, тож рядок
    //     примусово переводиться в нижній регістр — це робить виклик
    //     стійким до "level.TAT3.name" проти "level.tat3.name" (одна з
    //     найлегших помилок, яка дала б мовчазно НЕ той хеш). Використано
    //     інваріантну культуру: у турецькій локалі 'I'.ToLower() дає 'ı',
    //     що зіпсувало б хеш непомітно й лише на частині машин.
    //
    //     Кодування: ASCII. Ключі гри — суто латиниця/цифри/крапки, і будь-
    //     який не-ASCII символ тут означав би помилку виклику (кирилиця
    //     має бути в ЗНАЧЕННІ, ніколи в ключі), тому такий випадок краще
    //     виявити явно, ніж мовчки захешувати '?'.
    // EN: Computes a localization key's hash.
    //
    //     Case: the game's keys are lowercase, so the string is forced to
    //     lower case — this makes the call robust against "level.TAT3.name"
    //     vs "level.tat3.name" (one of the easiest mistakes, which would
    //     silently yield the WRONG hash). Invariant culture is used on
    //     purpose: in the Turkish locale 'I'.ToLower() is 'ı', which would
    //     corrupt the hash invisibly and only on some machines.
    //
    //     Encoding: ASCII. The game's keys are pure latin/digits/dots, and
    //     any non-ASCII character here would mean a caller error (Cyrillic
    //     belongs in the VALUE, never in the key), so it is better to
    //     surface that explicitly than to silently hash a '?'.
    // -------------------------------------------------------------------------
    public static uint Compute(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var normalized = key.ToLowerInvariant();

        foreach (var ch in normalized)
        {
            if (ch > 0x7F)
                throw new ArgumentException(
                    $"UA: Ключ локалізації має бути ASCII, знайдено '{ch}' у \"{key}\" " +
                    "(кирилиця належить ЗНАЧЕННЮ, не ключу). / " +
                    $"EN: A localization key must be ASCII, found '{ch}' in \"{key}\" " +
                    "(Cyrillic belongs to the VALUE, not the key).",
                    nameof(key));
        }

        var hash = OffsetBasis;
        foreach (var b in Encoding.ASCII.GetBytes(normalized))
        {
            hash ^= b;
            hash *= Prime;
        }
        return hash;
    }
}
