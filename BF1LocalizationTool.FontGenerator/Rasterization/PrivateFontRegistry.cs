// =============================================================================
// BF1LocalizationTool.FontGenerator — Rasterization/PrivateFontRegistry.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Завантажує шрифти з .ttf-файлів у теці біля .exe (той самий підхід,
//     що й SWH.FontTool), а НЕ через системний реєстр шрифтів Windows.
//
//     ЧОМУ: до цього класу `GdiGlyphRasterizer` створював `Font` за
//     РЯДКОМ-НАЗВОЮ (`new Font(fontFamilyName, ...)`) — GDI+ шукає таку
//     назву ЛИШЕ серед шрифтів, ВСТАНОВЛЕНИХ у систему. Досі
//     використовувався `Bahnschrift SemiBold` — системний шрифт Windows,
//     ліцензія якого explicitly ЗАБОРОНЯЄ копіювання на інші системи чи
//     розповсюдження. Перехід на `PrivateFontCollection` вирішує це:
//     відтворюваність збірки + легальне бандлення відкритого (OFL) шрифту.
//
// UA: Ключ пошуку — "ВІДНОСНИЙ ШЛЯХ ФАЙЛУ", а НЕ "назва родини". ПРИЧИНА
//     (корінь бага, знайденого в реальному лозі):
//
//     1. ОБРІЗАННЯ. Win32 `LOGFONT.lfFaceName` = `LF_FACESIZE` (32 байти
//        з нулем-термінатором → 31 символ). GDI+ `FontFamily.Name` для
//        ПРИВАТНИХ шрифтів проходить через цей самий шлях і ОБРІЗАЄТЬСЯ.
//        Реальний доказ з логу: "Fira Sans Extra Condensed Mediu" (не
//        Medium), "...SemiB" (не SemiBold), "...Extra" (не ExtraBold —
//        і це ЗБІГАЄТЬСЯ з обрізаним "ExtraLight", РІЗНІ файли дають
//        ОДНАКОВИЙ ключ). Ім'я файлу (напр.
//        "FiraSansExtraCondensed-ExtraBold.ttf") — рядок ФАЙЛОВОЇ
//        СИСТЕМИ, жодного ліміту в 31 символ немає.
//
//     2. ЗЛИТТЯ СТИЛІВ. Коли кілька .ttf МАПЛЯТЬСЯ на ОДНУ family-назву
//        (класичний GDI-квартет Regular/Bold/Italic/BoldItalic — так
//        влаштований сам "Fira Sans" без суфікса), GDI+ зберігає їх як
//        ОДНУ FontFamily з кількома доступними стилями, а
//        `new Font(family, size, FontStyle.Regular, unit)` — ПІДТВЕРДЖЕНО
//        емпірично (розділ 11.13 FONT_FORMAT_SPEC.md) — не завжди чесно
//        обирає саме Regular: виміряна природна пропорція родини
//        "Fira Sans" (0.774) збіглась НЕ з окремо виміряним Regular
//        (0.702), а з Bold (0.778) — тобто GDI+ мовчки підмінив стиль.
//
//     ФІКС: ОДНА `PrivateFontCollection` НА КОЖЕН ФАЙЛ, а не одна спільна
//     на всі 36. Ізольована колекція з ОДНИМ файлом всередині фізично не
//     може змішати стилі — FontFamily, яку вона видає, має РІВНО ті
//     стилі, що несе САМЕ ЦЕЙ файл (для звичайних static-інстансів —
//     рівно один). Ключ пошуку — ВІДНОСНИЙ ШЛЯХ файлу (унікальний за
//     конструкцією, файлова система, а не LOGFONT, обрізання неможливе).
// EN: Loads fonts from .ttf files in a folder next to the .exe (the same
//     approach as SWH.FontTool), NOT via the Windows system font registry.
//
//     WHY: until this class, `GdiGlyphRasterizer` built a `Font` by NAME
//     STRING — GDI+ only looks that up among SYSTEM-INSTALLED fonts.
//     `Bahnschrift SemiBold` was used until now — its license explicitly
//     FORBIDS copying to other systems or redistribution. Switching to
//     `PrivateFontCollection` fixes that: reproducible builds + legal
//     bundling of an open (OFL) font.
//
// EN: The lookup key is a "RELATIVE FILE PATH", not a "family name".
//     REASON (the root of a bug found in a real log):
//
//     1. TRUNCATION. Win32 `LOGFONT.lfFaceName` = `LF_FACESIZE` (32 bytes
//        incl. null terminator → 31 characters). GDI+'s `FontFamily.Name`
//        for PRIVATE fonts goes through that same path and gets
//        TRUNCATED. Real proof from the log: "Fira Sans Extra Condensed
//        Mediu" (not Medium), "...SemiB" (not SemiBold), "...Extra" (not
//        ExtraBold — and it COLLIDES with truncated "ExtraLight", two
//        DIFFERENT files producing the SAME key). A file name (e.g.
//        "FiraSansExtraCondensed-ExtraBold.ttf") is a FILESYSTEM string —
//        no 31-character limit at all.
//
//     2. STYLE MERGING. When several .ttf files MAP to ONE family name
//        (the classic GDI quartet Regular/Bold/Italic/BoldItalic — how
//        bare "Fira Sans" is built), GDI+ stores them as ONE FontFamily
//        with several available styles, and
//        `new Font(family, size, FontStyle.Regular, unit)` — EMPIRICALLY
//        CONFIRMED (FONT_FORMAT_SPEC.md section 11.13) — doesn't always
//        honestly pick Regular: the measured natural proportion of family
//        "Fira Sans" (0.774) matched NOT the separately-measured Regular
//        (0.702) but Bold (0.778) — i.e. GDI+ silently substituted the
//        style.
//
//     FIX: ONE `PrivateFontCollection` PER FILE, not one shared collection
//     for all 36. An isolated collection holding a single file physically
//     cannot mix styles — the FontFamily it reports has EXACTLY the
//     style(s) that ONE file carries (exactly one, for ordinary static
//     instances). The lookup key is the file's RELATIVE PATH (unique by
//     construction, a filesystem string, not LOGFONT — truncation is
//     impossible).
// =============================================================================

using System.Drawing;
using System.Drawing.Text;

namespace BF1LocalizationTool.FontGenerator.Rasterization;

public static class PrivateFontRegistry
{
    private sealed record LoadedFont(PrivateFontCollection Collection, FontFamily Family, FontStyle Style);

    // UA: Стилі, які GDI+ взагалі розрізняє (FontStyle — це бітова маска,
    //     але Font-конструктор і IsStyleAvailable оперують лише цими
    //     чотирма класичними комбінаціями).
    // EN: The styles GDI+ actually distinguishes (FontStyle is a bit mask,
    //     but the Font constructor and IsStyleAvailable only operate on
    //     these four classic combinations).
    private static readonly FontStyle[] StandardStyles =
        [FontStyle.Regular, FontStyle.Bold, FontStyle.Italic, FontStyle.Bold | FontStyle.Italic];

    private static readonly Dictionary<string, LoadedFont> ByPath = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;
    private static string? _loadedFrom;

    // UA: Ледаче завантаження — перший виклик Get() з дефолтним шляхом сам
    //     ініціює сканування. EnsureLoaded можна викликати явно (напр. на
    //     старті команди), щоб одразу побачити помилку конфігурації, а не
    //     на першому гліфі посеред довгого прогону.
    // EN: Lazy loading — the first Get() call with the default path
    //     triggers the scan itself. EnsureLoaded can be called explicitly
    //     (e.g. at command startup) to surface a configuration error
    //     immediately, rather than on the first glyph mid-run.
    public static void EnsureLoaded(string? fontsDirectory = null)
    {
        var dir = fontsDirectory ?? DefaultFontsDirectory();

        if (_loaded && string.Equals(_loadedFrom, dir, StringComparison.OrdinalIgnoreCase))
            return;

        if (_loaded)
            throw new InvalidOperationException(
                $"UA: PrivateFontRegistry вже завантажено з іншої теки (\"{_loadedFrom}\"), " +
                $"повторне завантаження з \"{dir}\" в межах одного процесу не підтримується. / " +
                $"EN: PrivateFontRegistry was already loaded from a different folder (\"{_loadedFrom}\"), " +
                $"reloading from \"{dir}\" within the same process isn't supported.");

        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException(
                $"UA: Тека шрифтів не знайдена: \"{dir}\". Поклади .ttf-файли туди " +
                "(підтеки дозволені), напр. Fonts\\Fira_Sans\\*.ttf — та сама конвенція, " +
                "що й reference-files\\ для core.lvl. / " +
                $"EN: Fonts folder not found: \"{dir}\". Put .ttf files there " +
                "(subfolders allowed), e.g. Fonts\\Fira_Sans\\*.ttf — the same convention " +
                "as reference-files\\ for core.lvl.");

        var ttfFiles = Directory.EnumerateFiles(dir, "*.ttf", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (ttfFiles.Count == 0)
            throw new InvalidOperationException(
                $"UA: У теці \"{dir}\" немає жодного .ttf-файлу. / " +
                $"EN: No .ttf files found in \"{dir}\".");

        var skipped = new List<string>();
        foreach (var path in ttfFiles)
        {
            // UA: ОКРЕМА колекція на КОЖЕН файл — див. коментар угорі файлу
            //     (пункт 2, "злиття стилів"). Колекція НІКОЛИ не звільняється
            //     (GDI+ PrivateFontCollection не має Remove) — лишається живою
            //     на весь час роботи процесу, щоб FontFamily-посилання лишались
            //     валідними; для ~40 файлів це не проблема пам'яті.
            // EN: A SEPARATE collection PER file — see the file-header comment
            //     (point 2, "style merging"). The collection is NEVER disposed
            //     (GDI+ PrivateFontCollection has no Remove) — it stays alive
            //     for the process lifetime so the FontFamily reference stays
            //     valid; for ~40 files this is not a memory concern.
            var collection = new PrivateFontCollection();
            collection.AddFontFile(path);

            if (collection.Families.Length == 0)
            {
                skipped.Add(path);
                continue;
            }

            var family = collection.Families[0];
            var style = StandardStyles.FirstOrDefault(s => family.IsStyleAvailable(s), FontStyle.Regular);
            var relativePath = Path.GetRelativePath(dir, path);
            ByPath[relativePath] = new LoadedFont(collection, family, style);
        }

        if (skipped.Count > 0)
        {
            // UA: НЕ фатально — записуємо, які файли пропущено, і продовжуємо
            //     з рештою. Причина пропуску (пошкоджений файл, не-TrueType
            //     .ttf тощо) видно лише повторним ручним оглядом файлу.
            // EN: NOT fatal — record which files were skipped and continue
            //     with the rest. The skip reason (corrupt file, non-TrueType
            //     .ttf, etc.) is only visible by manually inspecting the file.
            Console.WriteLine(
                $"UA: УВАГА — {skipped.Count} .ttf не дали жодної родини, пропущено: {string.Join(", ", skipped)}. / " +
                $"EN: WARNING — {skipped.Count} .ttf produced no family, skipped: {string.Join(", ", skipped)}.");
        }

        if (ByPath.Count == 0)
            throw new InvalidOperationException(
                $"UA: Жоден .ttf у \"{dir}\" не завантажився успішно. / " +
                $"EN: No .ttf in \"{dir}\" loaded successfully.");

        _loaded = true;
        _loadedFrom = dir;
    }

    // UA: Повертає (FontFamily, FontStyle) за ВІДНОСНИМ ШЛЯХОМ файлу
    //     (відносно теки Fonts\, напр. "FiraSans-SemiBold.ttf" або
    //     "Fira_Sans\FiraSans-SemiBold.ttf", якщо покладено в підтеку) —
    //     НЕ за назвою родини. Стиль ГАРАНТОВАНО відповідає тому, що
    //     реально несе САМЕ ЦЕЙ файл (ізольована колекція, без злиття —
    //     див. коментар угорі файлу). Кидає з переліком УСІХ реально
    //     завантажених шляхів, якщо не знайдено.
    // EN: Returns (FontFamily, FontStyle) by the file's RELATIVE PATH
    //     (relative to the Fonts\ folder, e.g. "FiraSans-SemiBold.ttf" or
    //     "Fira_Sans\FiraSans-SemiBold.ttf" if placed in a subfolder) —
    //     NOT by family name. The style is GUARANTEED to match what THIS
    //     file actually carries (isolated collection, no merging — see the
    //     file-header comment). Throws with a list of ALL actually loaded
    //     paths if not found.
    public static (FontFamily Family, FontStyle Style) Get(string relativePath)
    {
        EnsureLoaded();

        if (ByPath.TryGetValue(relativePath, out var loaded))
            return (loaded.Family, loaded.Style);

        throw new InvalidOperationException(
            $"UA: Файл шрифту \"{relativePath}\" не знайдено серед завантажених. " +
            $"Доступні: {string.Join(", ", ByPath.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))}. Перевір теку {_loadedFrom}. / " +
            $"EN: Font file \"{relativePath}\" not found among loaded fonts. " +
            $"Available: {string.Join(", ", ByPath.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))}. Check the folder {_loadedFrom}.");
    }

    public static IReadOnlyCollection<string> LoadedPaths()
    {
        EnsureLoaded();
        return ByPath.Keys;
    }

    private static string DefaultFontsDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "Fonts");
}
