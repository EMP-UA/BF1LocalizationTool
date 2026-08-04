// =============================================================================
// BF1LocalizationTool.FontGenerator — Matching/CyrillicAlphabet.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: 33 українські літери, обидва регістри — 66 цілей, точно стільки,
//     скільки SoftDonorAnalysis.NeededGlyphCodes (Diagnostic-проєкт)
//     вважає потрібним для повного алфавіту без жодного переиспользування
//     гліфів.
// EN: 33 Ukrainian letters, both cases — 66 targets, exactly matching
//     SoftDonorAnalysis.NeededGlyphCodes (Diagnostic project), which is
//     what's needed for a full alphabet with zero glyph reuse.
// =============================================================================

namespace BF1LocalizationTool.FontGenerator.Matching;

public static class CyrillicAlphabet
{
    public static readonly IReadOnlyList<char> UppercaseLetters =
        "АБВГҐДЕЄЖЗИІЇЙКЛМНОПРСТУФХЦЧШЩЬЮЯ".ToCharArray();

    public static readonly IReadOnlyList<char> LowercaseLetters =
        "абвгґдеєжзиіїйклмнопрстуфхцчшщьюя".ToCharArray();

    public static IReadOnlyList<char> AllLetters { get; } =
        [.. UppercaseLetters, .. LowercaseLetters];

    // UA: Вузька, свідомо КОНСЕРВАТИВНА вибірка малих
    //     літер БЕЗ висхідних (б,в,д-петля,і,ї,й,ф,ь) і БЕЗ низхідних
    //     (р,у,ц,щ) елементів — використовується ЛИШЕ як ЕТАЛОННИЙ ЗРАЗОК
    //     для вимірювання природної висоти "x-height" ЦЬОГО шрифту
    //     (GlyphMetricModel.DeriveReference → NaturalCoreAscent/CoreTopY).
    //     Це НЕ список "особливих" літер для ручного втручання в рендер —
    //     кожна літера (включно з тими, що сюди не увійшли) все одно
    //     проходить ОДНАКОВЕ, повністю автоматичне обчислення
    //     (GlyphBoxFitRenderer.ComputeCoreMarginLayout: перетин реального
    //     чорнила з еталонною смугою [coreTopY, baseline] визначає "тіло"
    //     й "виступи" геометрично, без жодних if-по-літері). Вибірка лише
    //     ЗАДАЄ, ДЕ саме проходить ця еталонна смуга — обрано літери, у
    //     яких x-height-зона наочно однакова в БУДЬ-якому кириличному
    //     sans-serif шрифті (перевірено на Fira Sans).
    // EN: A narrow, deliberately CONSERVATIVE sample of
    //     lowercase letters WITHOUT ascenders (б,в,д-loop,і,ї,й,ф,ь) and
    //     WITHOUT descenders (р,у,ц,щ) — used ONLY as a REFERENCE SAMPLE
    //     for measuring THIS font's natural "x-height" (GlyphMetricModel.
    //     DeriveReference → NaturalCoreAscent/CoreTopY). This is NOT a list
    //     of "special" letters for manual per-letter render intervention —
    //     every letter (including ones not in this list) still goes
    //     through the SAME, fully automatic computation
    //     (GlyphBoxFitRenderer.ComputeCoreMarginLayout: the overlap of the
    //     real ink with the reference band [coreTopY, baseline]
    //     geometrically determines "core" vs "extension", with no
    //     per-letter if-branches at all). The sample only PINS DOWN where
    //     that reference band sits — letters chosen because their x-height
    //     zone is visually consistent across virtually any Cyrillic
    //     sans-serif font (verified on Fira Sans).
    public static readonly IReadOnlyList<char> CoreLowercaseLetters =
        "аоеснимшж".ToCharArray();
}
