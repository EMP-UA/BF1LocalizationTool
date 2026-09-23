// =============================================================================
// BF1LocalizationTool.Diagnostic — SoftDonorAnalysis.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Винесено з Program.cs (було 3 локальні функції + 3 records, доступні
//     ЛИШЕ всередині Program.cs). SoftDonorGeometryCheckCommand.cs
//     потребує ТОЧНО той самий список м'яких кандидатів, що бачить
//     користувач у "Мовний аналіз" — тому обчислення винесено в один
//     спільний клас, а не продубльовано вдруге.
// EN: Extracted from Program.cs (used to be 3 local functions + 3
//     records, accessible ONLY inside Program.cs).
//     SoftDonorGeometryCheckCommand.cs needs EXACTLY the same soft
//     candidate list the user sees in "Language analysis" — so the
//     computation is extracted into one shared class instead of being
//     duplicated a second time.
// =============================================================================

using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Localization;

namespace BF1LocalizationTool.Diagnostic;

// UA: Один шрифт у зведенні (без прямого доступу до UcfbChunk — для
//     легкого порівняння між двома завантаженими файлами).
// EN: One font in the summary (no direct UcfbChunk access — for easy
//     comparison between two loaded files).
public record FontSummary(string BaseName, int TexturePageCount);

// UA: Використання байт-кодів однією мовою: код → кількість входжень.
// EN: One language's byte code usage: code → occurrence count.
public record LangUsage(string Language, int EntryCount, Dictionary<int, int> CodeCounts);

// UA: Компактне зведення по одному файлу.
// EN: Compact summary for one file.
public record FileSummary(string FilePath, List<FontSummary> Fonts, List<LangUsage> Languages, HashSet<int> SafeDonorCodes);

// UA: Один "м'який" кандидат — код, ніколи не використовуваний
//     англійською, з сумарним впливом (кількість входжень) і переліком
//     мов, які він зачепить.
// EN: One "soft" candidate — a code English never uses, with its total
//     impact (occurrence count) and the list of languages it affects.
public record SoftDonorCandidate(int Code, int TotalOccurrences, HashSet<string> Languages);

public static class SoftDonorAnalysis
{
    // UA: Скільки кодів потрібно для повного кириличного алфавіту без
    //     жодного переиспользування гліфів (33 українські літери × 2
    //     регістри). ЄДИНЕ джерело цього числа — не дублюється більше
    //     ніде в проєкті.
    // EN: How many codes are needed for a full Cyrillic alphabet with
    //     zero glyph reuse (33 Ukrainian letters × 2 cases). The SINGLE
    //     source of this number — not duplicated anywhere else in the
    //     project.
    public const int NeededGlyphCodes = 66;

    public static async Task<FileSummary> BuildSummaryAsync(string filePath)
    {
        var root = UcfbReader.ReadFile(filePath);

        var fonts = FontChunkLocator.FindAll(root)
            .Select(f => new FontSummary(f.BaseName, f.TexturePages.Count))
            .ToList();

        var service = new LvlLocalizationService();
        await service.LoadAsync(filePath);

        var languages = new List<LangUsage>();
        var usedByAny = new HashSet<int>();

        foreach (var lang in service.AvailableLanguages)
        {
            var file = service.GetLanguageFile(lang);
            if (file is null) continue;

            var counts = new Dictionary<int, int>();
            foreach (var entry in file.Entries)
                foreach (var c in entry.Original)
                    if (c is >= (char)0 and <= (char)255)
                        counts[c] = counts.GetValueOrDefault(c) + 1;

            usedByAny.UnionWith(counts.Keys);
            languages.Add(new LangUsage(lang, file.Entries.Count, counts));
        }

        // UA: Увесь діапазон 0-255 рахується РЕАЛЬНИМИ даними, без
        //     винятків для ASCII — окреме припущення "0-127 завжди
        //     зайнятий" не звірене б було з реальними даними. Побайтова
        //     перевірка (обидві гри) показує: 46/128 кодів ASCII у BF1 і
        //     30/128 у BF2 РЕАЛЬНО ніколи не зустрічаються в жодній з
        //     6 мов (у т.ч. самі латинські літери b,f,h,j,k,q,r,v,w,x,y,z
        //     у BF1, бо весь текст UI — капс).
        // EN: The full 0-255 range is counted from REAL data, no
        //     exceptions for ASCII — a separate "0-127 always used"
        //     assumption would go unchecked against real data. A
        //     byte-level check (both games) shows: 46/128 ASCII codes in
        //     BF1 and 30/128 in BF2 are genuinely NEVER used by any of
        //     the 6 languages (including the Latin letters
        //     b,f,h,j,k,q,r,v,w,x,y,z themselves in BF1, since all UI
        //     text there is uppercase).
        var safe = Enumerable.Range(0, 256).Except(usedByAny).ToHashSet();
        return new FileSummary(filePath, fonts, languages, safe);
    }

    public static void PrintSummary(FileSummary s, Action<string> log)
    {
        log("Шрифтові ресурси / Font resources:");
        foreach (var f in s.Fonts)
            log($"  {f.BaseName} — {f.TexturePageCount} текстурних сторінок / texture pages");

        log("");
        log("Використання байт-кодів 0-255 по мовах (код×кількість входжень):");
        log("Byte code 0-255 usage per language (code×occurrence count):");
        foreach (var lang in s.Languages)
        {
            var codesStr = lang.CodeCounts.Count == 0
                ? "(жодного / none)"
                : string.Join(", ", lang.CodeCounts.OrderBy(kv => kv.Key).Select(kv => $"0x{kv.Key:X2}×{kv.Value}"));
            log($"  {lang.Language} ({lang.EntryCount} рядків / lines): {codesStr}");
        }

        log("");
        log($"Безпечні донорні коди (не використані ЖОДНОЮ мовою): {s.SafeDonorCodes.Count} шт.");
        log($"Safe donor codes (unused by ANY language): {s.SafeDonorCodes.Count} pcs.");
        log("  " + string.Join(", ", s.SafeDonorCodes.OrderBy(x => x).Select(x => $"0x{x:X2}")));
    }

    // UA: Чиста обчислювальна функція (без виводу) — коди, яких НІКОЛИ не
    //     використовує англійська, ранжовані за найменшим впливом
    //     (найменша сумарна частота, найменше уражених мов). Викликається
    //     і з PrintSoftDonorSuggestions (друк для людини), і з
    //     SoftDonorGeometryCheckCommand (подальша геометрична перевірка
    //     ТОГО САМОГО списку).
    // EN: Pure computation (no printing) — codes English NEVER uses,
    //     ranked by lowest impact (lowest total frequency, fewest
    //     affected languages). Called both from PrintSoftDonorSuggestions
    //     (human-readable printout) and from SoftDonorGeometryCheckCommand
    //     (further geometric check of the SAME list).
    public static List<SoftDonorCandidate> ComputeSoftCandidates(FileSummary s)
    {
        var englishUsed = s.Languages
            .Where(l => l.Language.Contains("english", StringComparison.OrdinalIgnoreCase))
            .SelectMany(l => l.CodeCounts.Keys)
            .ToHashSet();

        var candidates = new Dictionary<int, (int Total, HashSet<string> Langs)>();
        foreach (var lang in s.Languages)
        {
            if (lang.Language.Contains("english", StringComparison.OrdinalIgnoreCase))
                continue; // UA: англійську ніколи не чіпаємо / EN: never touch English
            foreach (var (code, count) in lang.CodeCounts)
            {
                if (englishUsed.Contains(code)) continue; // UA: подвійна страховка / EN: double safety
                if (!candidates.TryGetValue(code, out var v))
                    v = (0, []);
                v.Total += count;
                v.Langs.Add(lang.Language);
                candidates[code] = v;
            }
        }

        return candidates
            .OrderBy(kv => kv.Value.Total).ThenBy(kv => kv.Value.Langs.Count)
            .Select(kv => new SoftDonorCandidate(kv.Key, kv.Value.Total, kv.Value.Langs))
            .ToList();
    }

    // UA: Якщо безпечних кодів не вистачає на neededGlyphCodes — друкує
    //     м'які кандидати (для людини, у порядку зростання впливу).
    // EN: If safe codes aren't enough for neededGlyphCodes — prints the
    //     soft candidates (for a human, in ascending order of impact).
    public static void PrintSoftDonorSuggestions(FileSummary s, int neededGlyphCodes, Action<string> log)
    {
        if (s.SafeDonorCodes.Count >= neededGlyphCodes)
            return;

        log("");
        log($"УВАГА: безпечних кодів менше, ніж потрібно для {neededGlyphCodes} гліфів (бракує {neededGlyphCodes - s.SafeDonorCodes.Count}).");
        log($"WARNING: fewer safe codes than needed for {neededGlyphCodes} glyphs (short by {neededGlyphCodes - s.SafeDonorCodes.Count}).");

        log("--- М'які кандидати (НІКОЛИ не англійська; відсортовано за мінімальним впливом) ---");
        log("--- Soft candidates (NEVER English; sorted by lowest impact) ---");
        foreach (var c in ComputeSoftCandidates(s))
            log($"  0x{c.Code:X2}: {c.TotalOccurrences} входжень / occurrences, мови / languages: [{string.Join(", ", c.Languages.OrderBy(x => x))}]");
    }
}
