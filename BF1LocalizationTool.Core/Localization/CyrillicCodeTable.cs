// =============================================================================
// BF1LocalizationTool.Core — Localization/CyrillicCodeTable.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Файл-супутник поруч зі згенерованим core.lvl (cyrillic-code-table.json)
//     — записує спільну для ВСІХ шрифтів гри таблицю "яка кирилична літера
//     → який байт-код (0x00-0xFF) вона фізично займає в атласі шрифту".
//     Побудована ОДИН РАЗ під час генерації (GenerateLocalizedCoreCommand,
//     BF1LocalizationTool.Diagnostic) на перетині безпечних кодів УСІХ
//     оброблюваних шрифтів — сама генерація саме цю таблицю й використала
//     для патчу FBOD/BODY, тож файл-супутник ГАРАНТОВАНО збігається з тим,
//     що реально в шрифтах цього конкретного core.lvl.
//
//     ПРИЗНАЧЕННЯ: локалізаційний текст (LoclChunkParser/LocalizationParser)
//     фізично не може зберігати кирилицю ЯК СЕБЕ — рушій шукає гліф за
//     кодом 0x00-0xFF (FBOD — суцільна однобайтова таблиця, розділ 4
//     специфікації), а не за справжнім Unicode-кодпоінтом кирилиці
//     (U+0400-U+04FF). Тому перед записом кожна кирилична літера в
//     перекладі підміняється на "фальшивий" символ у діапазоні U+0000-
//     U+00FF, що відповідає byte-коду її гліфа — і саме цей псевдо-текст
//     іде в існуючі серіалізатори (Encoding.Unicode/Latin1) без змін.
//     При читанні — підміна в зворотний бік, щоб у GUI показувалась
//     справжня, читабельна кирилиця, а не '¶'/'µ'/т.п.
//
//     БЕЗПЕКА М'ЯКИХ ДОНОРІВ: якщо серед кодів таблиці є "м'які" (уже
//     використані ЯКОЮСЬ НЕ-англійською мовою — розділ 5.2 специфікації),
//     підміна МАЄ застосовуватись ЛИШЕ до цільової (української) мови, а
//     не до всіх мов файлу — інакше, наприклад, французьке 'É' (яке
//     фізично лежить на тому самому байт-коді, що й якась кирилична
//     літера) помилково перетвориться на кирилицю при показі. Це не
//     робиться всередині цього класу (він працює з довільним рядком) — це
//     відповідальність викликаючого коду (LvlLocalizationService), який
//     має застосовувати ToGameEncoded/FromGameEncoded ЛИШЕ до записів
//     цільової мови.
// EN: A sidecar file next to a generated core.lvl (cyrillic-code-table.json)
//     — records the table shared by ALL fonts of the game: "which
//     Cyrillic letter → which byte-code (0x00-0xFF) it physically occupies
//     in the font atlas". Built ONCE during generation
//     (GenerateLocalizedCoreCommand, BF1LocalizationTool.Diagnostic) on the
//     intersection of all processed fonts' safe codes — generation itself
//     used this EXACT table to patch FBOD/BODY, so the sidecar file is
//     GUARANTEED to match what's actually in this specific core.lvl's fonts.
//
//     PURPOSE: localization text (LoclChunkParser/LocalizationParser)
//     physically cannot store Cyrillic AS ITSELF — the engine looks up a
//     glyph by a 0x00-0xFF code (FBOD is a contiguous single-byte table,
//     spec section 4), not by the real Cyrillic Unicode codepoint
//     (U+0400-U+04FF). So before writing, each Cyrillic letter in a
//     translation is substituted with a "fake" character in the
//     U+0000-U+00FF range matching its glyph's byte-code — and that
//     pseudo-text goes into the existing serializers (Encoding.Unicode/
//     Latin1) unchanged. On read — the substitution runs in reverse, so
//     the GUI displays real, readable Cyrillic instead of '¶'/'µ'/etc.
//
//     SOFT-DONOR SAFETY: if the table includes "soft" codes (already used
//     by SOME non-English language — spec section 5.2), the substitution
//     MUST be applied ONLY to the target (Ukrainian) language, never to
//     every language in the file — otherwise, e.g., a French 'É' (which
//     physically sits on the same byte-code as some Cyrillic letter) would
//     be wrongly turned into Cyrillic on display. This class does not
//     enforce that scoping itself (it operates on an arbitrary string) —
//     it's the caller's (LvlLocalizationService) responsibility to apply
//     ToGameEncoded/FromGameEncoded ONLY to the target language's entries.
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;

namespace BF1LocalizationTool.Core.Localization;

public sealed record CyrillicCodeTableEntry(
    [property: JsonPropertyName("character")] string Character,
    [property: JsonPropertyName("code")] byte Code);

public sealed class CyrillicCodeTable
{
    public const string DefaultFileName = "cyrillic-code-table.json";

    [JsonPropertyName("game")]
    public required string Game { get; init; }

    // UA: З якого шрифту взято геометрію для самого ПРИЗНАЧЕННЯ
    //     (GlyphDonorMatcher.Assign) — лише для довідки/діагностики,
    //     на кодування/декодування не впливає.
    // EN: Which font's geometry drove the ASSIGNMENT itself
    //     (GlyphDonorMatcher.Assign) — reference/diagnostic only, does not
    //     affect encoding/decoding.
    [JsonPropertyName("referenceFont")]
    public required string ReferenceFont { get; init; }

    [JsonPropertyName("generatedAtUtc")]
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    [JsonPropertyName("letters")]
    public required IReadOnlyList<CyrillicCodeTableEntry> Letters { get; init; }

    [JsonIgnore]
    private Dictionary<char, byte>? _toCode;
    [JsonIgnore]
    private Dictionary<byte, char>? _toCharacter;

    private void BuildIndexIfNeeded()
    {
        if (_toCode is not null) return;

        _toCode = new Dictionary<char, byte>();
        _toCharacter = new Dictionary<byte, char>();

        foreach (var entry in Letters)
        {
            if (entry.Character.Length != 1)
                throw new InvalidDataException(
                    $"UA: Запис таблиці кодів має бути ОДНИМ символом, отримано '{entry.Character}' / " +
                    $"EN: Code table entry must be a SINGLE character, got '{entry.Character}'");

            var ch = entry.Character[0];
            _toCode[ch] = entry.Code;
            _toCharacter[entry.Code] = ch;
        }
    }

    // UA: Кирилиця → псевдо-символ (байт-код гліфа). Символи поза
    //     таблицею (латиниця, цифри, пунктуація) лишаються без змін.
    //     Викликати ЛИШЕ перед записом байтів цільової мови.
    // EN: Cyrillic → pseudo-character (glyph byte-code). Characters
    //     outside the table (Latin letters, digits, punctuation) pass
    //     through unchanged. Call ONLY before writing the target
    //     language's bytes.
    public string ToGameEncoded(string text)
    {
        BuildIndexIfNeeded();

        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (_toCode!.TryGetValue(chars[i], out var code))
                chars[i] = (char)code;

        return new string(chars);
    }

    // UA: Псевдо-символ (байт-код гліфа) → справжня кирилиця, для показу
    //     людині. Символи поза таблицею лишаються без змін. Викликати
    //     ЛИШЕ на записах цільової мови одразу після завантаження.
    // EN: Pseudo-character (glyph byte-code) → real Cyrillic, for display
    //     to a human. Characters outside the table pass through unchanged.
    //     Call ONLY on the target language's entries right after loading.
    public string FromGameEncoded(string text)
    {
        BuildIndexIfNeeded();

        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (chars[i] <= 0xFF && _toCharacter!.TryGetValue((byte)chars[i], out var ch))
                chars[i] = ch;

        return new string(chars);
    }

    // UA: Шукає файл-супутник у ТІЙ САМІЙ теці, що й переданий core.lvl.
    //     Повертає null, якщо файлу немає (звичайний, "не перевірений"
    //     чи не пройшов через генерацію core.lvl) — це НЕ помилка.
    // EN: Looks for the sidecar file in the SAME folder as the given
    //     core.lvl. Returns null if the file doesn't exist (a plain,
    //     "not-yet-generated-through" core.lvl) — this is NOT an error.
    public static CyrillicCodeTable? TryLoadNextTo(string lvlFilePath)
    {
        var dir = Path.GetDirectoryName(lvlFilePath);
        if (string.IsNullOrEmpty(dir)) return null;

        var path = Path.Combine(dir, DefaultFileName);
        return File.Exists(path) ? Load(path) : null;
    }

    public static CyrillicCodeTable Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CyrillicCodeTable>(json)
            ?? throw new InvalidDataException(
                $"UA: Не вдалось розпарсити таблицю кодів: {path} / " +
                $"EN: Failed to parse code table: {path}");
    }

    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
