// =============================================================================
// BF1LocalizationTool.Core — Bf2Movies/MovieSubtitleFontPatcher.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: ДІАГНОСТИЧНИЙ зонд №2 для субтитрів роликів: підміна ШРИФТА в директиві
//     `font` чанка `mcfg`. Не фікс — експеримент, що звужує коло причин.
//
//     НАВІЩО. Статичний розбір `BattlefrontII.exe` (лише читання) простежив
//     УВЕСЬ шлях субтитру — від кадрового циклу до виклику `SetVisible` — і
//     не знайшов у ньому ЖОДНОЇ умови за роздільністю чи аспектом:
//       • ширококадровий коефіцієнт `ws` читається в підсистемі роликів
//         рівно один раз і лише для аспекту, вужчого за 1.0 (VA 0x006F4E5C);
//       • прямокутник субтитру — «безпечна зона» 90 % екрана з однаковими
//         частками по X і Y (VA 0x006F6590 -> 0x006C0BC0 -> 0x006C14E0);
//       • видимість керується ЛИШЕ булевим полем профілю `bMovieSubtitles`
//         (VA 0x006F692B -> глобал 0x009C8434 <- 0x0061C85B);
//       • кадровий тик викликається безумовно (VA 0x00636368 -> 0x005349F0).
//     Отже причина лежить не в логіці субтитрів, а в самому 2D-текстовому
//     об'єкті. Найдешевша перевірка цієї гілки — змінити ЄДИНИЙ вхідний
//     параметр цього об'єкта, який ще не перевірявся: шрифт.
//
//     ЩО ВІДОМО ПРО ШРИФТ. Усі 38 сегментів `mission.lvl` і єдиний у
//     `shell.lvl` просять один і той самий шрифт — `gamefont_small`. Це
//     доведено, а не вгадано: рушій хешує ІМЕНА РЕСУРСІВ власною функцією
//     (VA 0x00726E50) — FNV-1a з тим самим базисом 0x811C9DC5 і множником
//     0x01000193, але «опусканням регістру» через `c |= 0x20` замість
//     `tolower`. За нею `gamefont_small` = 0x8F4B7122 — рівно те значення,
//     що стоїть у `mcfg`. (`SwbfStringHash` для ключів `Locl` вживає
//     `tolower`, тому на іменах з `_` та цифрами дає інший результат — див.
//     `SwbfResourceNameHash` поруч із ним.)
//
//     ЩО РОБИТЬ ЦЕЙ ЗОНД. Замінює значення директиви `font` у ВСІХ блоках
//     `mcfg` на інший шрифт (за замовчуванням `gamefont_large`). Якщо на
//     широкому екрані підпис З'ЯВИТЬСЯ — причина в конкретному шрифті чи
//     його атласі на цій роздільності, і це лікується ДАНИМИ. Якщо не
//     з'явиться — текстовий об'єкт не потрапляє у список малювання взагалі,
//     і далі треба дивитися саме туди.
//
//     ЧОМУ ЦЕ БЕЗПЕЧНО. Змінюється РІВНО 4 байти на кожну директиву `font`
//     усередині вже наявного запису. Розміри чанків не змінюються, тому
//     ані перебудова дерева, ані перерахунок зміщень не потрібні —
//     `UcfbWriter` запише файл байт-у-байт таким самим, окрім цих чисел.
//
//     ФОРМАТ ЗАПИСУ (звірено з парсером гри, VA 0x006F5DD9):
//       DATA = [хеш 0x274E1290][кількість=1][хеш імені шрифта]
//     тобто значення лежить за зміщенням +5 від початку пайлоада DATA.
//
//     Це зонд, а не виправлення: жодних заяв «працює» без знімка з гри.
//
// EN: DIAGNOSTIC probe #2 for movie subtitles: swapping the FONT in the
//     `font` directive of the `mcfg` chunk. Not a fix — an experiment that
//     narrows down the cause.
//
//     WHY. Static analysis of `BattlefrontII.exe` (read-only) traced the
//     ENTIRE subtitle path — from the frame loop down to the `SetVisible`
//     call — and found NO resolution or aspect condition anywhere in it:
//       • the widescreen factor `ws` is read exactly once in the movie
//         subsystem, and only for aspects narrower than 1.0 (VA 0x006F4E5C);
//       • the subtitle rectangle is the 90 % "safe area" with identical
//         fractions for X and Y (VA 0x006F6590 -> 0x006C0BC0 -> 0x006C14E0);
//       • visibility is driven ONLY by the profile boolean `bMovieSubtitles`
//         (VA 0x006F692B -> global 0x009C8434 <- 0x0061C85B);
//       • the per-frame tick is called unconditionally
//         (VA 0x00636368 -> 0x005349F0).
//     So the cause is not in the subtitle logic but in the 2D text object
//     itself. The cheapest probe of that branch is to change the one input
//     of that object which has not been tested yet: the font.
//
//     WHAT IS KNOWN ABOUT THE FONT. All 38 segments in `mission.lvl` and the
//     single one in `shell.lvl` request the same font — `gamefont_small`.
//     That is proven, not guessed: the engine hashes RESOURCE NAMES with its
//     own function (VA 0x00726E50) — FNV-1a with the same basis 0x811C9DC5
//     and prime 0x01000193, but "lowercasing" via `c |= 0x20` instead of
//     `tolower`. Under it `gamefont_small` = 0x8F4B7122 — exactly the value
//     stored in `mcfg`. (The `SwbfStringHash` for `Locl` keys uses
//     `tolower`, so it differs on names containing `_` or digits — see
//     `SwbfResourceNameHash` next to it.)
//
//     WHAT THIS PROBE DOES. Replaces the `font` directive's value in ALL
//     `mcfg` blocks with another font (`gamefont_large` by default). If the
//     caption APPEARS on a widescreen display, the cause lies with that
//     specific font or its atlas at that resolution, and it is fixable in
//     DATA. If it does not appear, the text object never reaches the draw
//     list at all, and that is where to look next.
//
//     WHY THIS IS SAFE. EXACTLY 4 bytes change per `font` directive, inside
//     an already-existing record. Chunk sizes never change, so neither tree
//     rebuilding nor offset recomputation is needed — `UcfbWriter` writes the
//     file byte-for-byte identical apart from those numbers.
//
//     RECORD FORMAT (verified against the game's parser at VA 0x006F5DD9):
//       DATA = [hash 0x274E1290][count=1][font name hash]
//     so the value sits at offset +5 from the start of the DATA payload.
//
//     This is a probe, not a fix: no "it works" claim without an in-game
//     screenshot.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Core.Bf2Movies;

public static class MovieSubtitleFontPatcher
{
    // UA: Зміщення значення всередині пайлоада DATA директиви "font".
    // EN: Offset of the value inside the "font" DATA payload.
    private const int ValueAt = 5;

    // UA: Хеші імен шрифтів, обчислені ХЕШЕМ РУШІЯ (не хешем `Locl`).
    //     Усі п'ять узято з таблиці в самому exe (0x007DF534..0x007DF544) і
    //     звірено з іменами реальних ресурсів у `core.lvl`.
    // EN: Font-name hashes computed with the ENGINE'S hash (not the `Locl`
    //     one). All five come from the table inside the exe itself
    //     (0x007DF534..0x007DF544) and match real resource names in `core.lvl`.
    public const uint GameFontLarge = 0xB3E564DE;      // gamefont_large
    public const uint GameFontMedium = 0xAA8876FC;     // gamefont_medium
    public const uint GameFontSmall = 0x8F4B7122;      // gamefont_small  <- ваніль / vanilla
    public const uint GameFontTiny = 0x46F060D5;       // gamefont_tiny
    public const uint GameFontSuperTiny = 0x548190D5;  // gamefont_super_tiny

    // UA: Людські імена — лише для звіту діагностики.
    // EN: Human-readable names — for the diagnostic report only.
    public static string DescribeFont(uint hash) => hash switch
    {
        GameFontLarge => "gamefont_large",
        GameFontMedium => "gamefont_medium",
        GameFontSmall => "gamefont_small",
        GameFontTiny => "gamefont_tiny",
        GameFontSuperTiny => "gamefont_super_tiny",
        _ => $"0x{hash:X8} (невідоме ім'я / unknown name)",
    };

    public sealed record FontPatchResult(int PatchedDirectives, uint OldHash, uint NewHash);

    // -------------------------------------------------------------------------
    // UA: Замінює шрифт у всіх директивах `font` дерева. Мутує RawData
    //     напряму — розміри не змінюються. Кидає виняток, якщо не знайдено
    //     жодної директиви: мовчазний «успіх» без змін гірший за помилку.
    // EN: Replaces the font in every `font` directive in the tree. Mutates
    //     RawData directly — sizes never change. Throws if no directive was
    //     found: a silent "success" with no changes is worse than an error.
    // -------------------------------------------------------------------------
    public static FontPatchResult Apply(UcfbChunk root, uint newFontHash)
    {
        var patched = 0;
        uint oldHash = 0;

        foreach (var mcfg in UcfbReader.FindAll(root, "mcfg"))
            Walk(mcfg);

        if (patched == 0)
            throw new InvalidOperationException(
                "UA: У файлі немає жодної директиви 'font' усередині mcfg — нічого патчити. / " +
                "EN: The file has no 'font' directive inside mcfg — nothing to patch.");

        return new FontPatchResult(patched, oldHash, newFontHash);

        void Walk(UcfbChunk node)
        {
            foreach (var child in node.Children)
            {
                if (child.FourCC == "DATA" &&
                    child.RawData.Length >= ValueAt + 4 &&
                    MovieConfigChunk.TryParseData(child.RawData, out var hash, out var values) &&
                    hash == MovieConfigDirectives.Font &&
                    values.Length > 0)
                {
                    if (oldHash == 0)
                        oldHash = values[0].Raw;

                    BitConverter.GetBytes(newFontHash).CopyTo(child.RawData, ValueAt);
                    patched++;
                }
                else if (child.FourCC == "SCOP")
                {
                    Walk(child);
                }
            }
        }
    }
}
