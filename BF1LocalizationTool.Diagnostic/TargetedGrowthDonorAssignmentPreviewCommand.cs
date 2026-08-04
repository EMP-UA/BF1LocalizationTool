// =============================================================================
// BF1LocalizationTool.Diagnostic — TargetedGrowthDonorAssignmentPreviewCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: На відміну від GrowthAwareDonorAssignmentPreviewCommand, який
//     росте КОЖЕН донор МАКСИМАЛЬНО в усіх напрямках ще ДО призначення —
//     такий підхід перекручує пропорції в гірший бік (середнє відхилення
//     зростає замість зменшитись — 0,045→0,285 у [BF2] gamefont_large,
//     0,462→0,657 у [BF1] gamefont_large), ця команда росте donor slots
//     ЦІЛЕСПРЯМОВАНО, ПІСЛЯ призначення, і лише настільки, наскільки
//     потрібно для КОНКРЕТНОЇ вже призначеної пари (літера, донор).
//
//     Порядок:
//       1. Спершу звичайне призначення НА ПОТОЧНИХ (не вирощених)
//          розмірах — той самий GlyphDonorMatcher, що й завжди.
//       2. Для КОЖНОЇ вже призначеної пари (літера, донор) — рахуємо,
//          ЧИ ПОТРІБНЕ й НАСКІЛЬКИ росте цей ОДИН донор, щоб наблизитись
//          саме до природної пропорції ЦІЄЇ літери (не до максимуму
//          вільного простору). Ростимо лише в ТУ вісь (ширина або
//          висота), яка зменшує розбіжність, і РІВНО стільки, скільки
//          потрібно — не більше.
//       3. Перевіряємо конфлікти (два ПРИЗНАЧЕНИХ донори після
//          ЦІЛЬОВОГО росту претендують на ту саму вільну ділянку).
//
//     Додатково — окремий блок: порівняння середнього відхилення
//     "особливих" літер (широких багатоштрихових; зі спускним
//     елементом; з рискою/крапкою над літерою) проти звичайних
//     baseline-літер — щоб бачити, чи справді "особливі" літери
//     системно гірше вписуються в наявних донорів.
// EN: Unlike GrowthAwareDonorAssignmentPreviewCommand, which grows EVERY
//     donor MAXIMALLY in all directions BEFORE assignment — an approach
//     that distorts ratios for the worse (average deviation goes UP
//     instead of down — 0.045→0.285 in [BF2] gamefont_large,
//     0.462→0.657 in [BF1] gamefont_large) — this command grows donor
//     slots in a TARGETED way, AFTER assignment, and only as much as the
//     SPECIFIC already-assigned pair (letter, donor) needs.
//
//     Order:
//       1. First, ordinary assignment on CURRENT (non-grown) sizes — the
//          same GlyphDonorMatcher as always.
//       2. For EACH already-assigned pair (letter, donor) — compute
//          WHETHER and HOW MUCH this ONE donor needs to grow to approach
//          THAT letter's natural ratio specifically (not the maximum
//          free space). Grow only along the ONE axis (width or height)
//          that reduces the mismatch, and by EXACTLY as much as needed —
//          no more.
//       3. Check conflicts (two assigned donors, after TARGETED growth,
//          claiming the same free area).
//
//     Additionally — a separate block: comparing the average deviation
//     of "special" letters (wide multi-stroke; with a descender; with a
//     mark/dot above the letter) against ordinary baseline letters — to
//     see whether "special" letters are systematically harder to fit
//     into existing donors.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.FontGenerator.Matching;

namespace BF1LocalizationTool.Diagnostic;

public static class TargetedGrowthDonorAssignmentPreviewCommand
{
    private readonly record struct GlyphRect(ushort Code, int MinX, int MaxX, int MinY, int MaxY);
    private readonly record struct GrowthPotential(int FreeLeft, int FreeRight, int FreeUp, int FreeDown);

    // UA: "Особливі" форми — навмисно з перетинами (напр. Щ одразу
    //     широка ТА зі спускним елементом), бо перевіряємо "чи має хоч
    //     одну складну рису", а не взаємовиключну категорію.
    // EN: "Special" shapes — deliberately overlapping (e.g. Щ is both
    //     wide AND has a descender), since we check "has at least one
    //     complex trait", not a mutually-exclusive category.
    private const string WideMultiStroke = "ЖШЩЮжшщю";
    private const string HasDescender = "РУДФЦЩрудфцщ";
    private const string HasAscenderMark = "ЙЇҐБІйїґбі";

    public static async Task Run(DiagnosticReport report, UcfbChunk root, string filePath, string label, string fontFamilyName, int neededGlyphCodes)
    {
        var summary = await SoftDonorAnalysis.BuildSummaryAsync(filePath);

        // UA: ДРУКОВНІ ASCII (0x20-0x7E) ЗАВЖДИ вважаються "зайнятими"
        //     незалежно від Locl-сканування, оскільки гра використовує їх
        //     і поза таблицею `Locl` (напр. дослівні англійські рядки, що
        //     не проходять через хеш-пошук — див. `PatchAddOnMapNameCommand`).
        //     Лише недруковні керівні коди (0x00-0x1F, 0x7F) лишаються
        //     кандидатами через реальні дані.
        // EN: PRINTABLE ASCII (0x20-0x7E) is ALWAYS treated as "used"
        //     regardless of the Locl scan, because the game also uses them
        //     outside the `Locl` table (e.g. literal English strings that
        //     bypass the hash lookup — see `PatchAddOnMapNameCommand`).
        //     Only non-printable control codes (0x00-0x1F, 0x7F) remain
        //     real-data candidates.
        bool IsPrintableAscii(ushort code) => code is >= 0x20 and <= 0x7E;
        var usedByAnyLanguage = summary.Languages.SelectMany(l => l.CodeCounts.Keys).ToHashSet();
        bool IsUsed(ushort code) => IsPrintableAscii(code) || usedByAnyLanguage.Contains(code);

        var englishUsed = summary.Languages
            .Where(l => l.Language.Contains("english", StringComparison.OrdinalIgnoreCase))
            .SelectMany(l => l.CodeCounts.Keys)
            .ToHashSet();

        var baseSafeCandidates = Enumerable.Range(0, 256).Where(x => !IsUsed((ushort)x)).ToHashSet();
        var needsSoftDonors = baseSafeCandidates.Count < neededGlyphCodes;
        var softCandidateCodes = needsSoftDonors
            ? SoftDonorAnalysis.ComputeSoftCandidates(summary).Select(c => c.Code).ToHashSet()
            : [];

        var fonts = FontChunkLocator.FindAll(root);

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var glyphs = FontGlyphTable.Parse(fbod.RawData);
            var unsafeBase = new HashSet<int>();
            var unsafeSoft = new HashSet<int>();
            var zeroArea = new HashSet<int>();
            var geometryByCode = new Dictionary<ushort, (int Width, int Height)>();
            var rectByCode = new Dictionary<ushort, GlyphRect>();
            var growthByCode = new Dictionary<ushort, GrowthPotential>();

            foreach (var pageGroup in glyphs.GroupBy(g => g.PageIndex))
            {
                if (pageGroup.Key >= font.TexturePages.Count) continue;

                var texPixels = FontTexturePixelReader.ReadMip0(font.TexturePages[pageGroup.Key].Chunk);
                var texWidth = texPixels.Width;
                var texHeight = texPixels.Height;

                var rects = pageGroup.Select(g =>
                {
                    var x0 = (int)Math.Round(g.U0 * texWidth);
                    var x1 = (int)Math.Round(g.U1 * texWidth);
                    var y0 = (int)Math.Round(g.V0 * texHeight);
                    var y1 = (int)Math.Round(g.V1 * texHeight);
                    return new GlyphRect(g.Code, Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1), Math.Max(y0, y1));
                }).ToList();

                foreach (var r in rects)
                {
                    geometryByCode[r.Code] = (r.MaxX - r.MinX, r.MaxY - r.MinY);
                    rectByCode[r.Code] = r;
                    if (r.MaxX - r.MinX <= 0 || r.MaxY - r.MinY <= 0)
                        zeroArea.Add(r.Code);
                }

                for (var i = 0; i < rects.Count; i++)
                {
                    for (var j = i + 1; j < rects.Count; j++)
                    {
                        var a = rects[i];
                        var b = rects[j];
                        var overlapsX = a.MinX < b.MaxX && b.MinX < a.MaxX;
                        var overlapsY = a.MinY < b.MaxY && b.MinY < a.MaxY;
                        if (!(overlapsX && overlapsY)) continue;

                        var aUsed = IsUsed(a.Code);
                        var bUsed = IsUsed(b.Code);
                        if (aUsed != bUsed)
                            unsafeBase.Add(aUsed ? b.Code : a.Code);

                        if (softCandidateCodes.Contains(a.Code) && englishUsed.Contains(b.Code))
                            unsafeSoft.Add(a.Code);
                        if (softCandidateCodes.Contains(b.Code) && englishUsed.Contains(a.Code))
                            unsafeSoft.Add(b.Code);
                    }
                }

                var occupied = new bool[texWidth, texHeight];
                foreach (var r in rects)
                {
                    if (r.MaxX - r.MinX <= 0 || r.MaxY - r.MinY <= 0) continue;
                    for (var x = r.MinX; x < r.MaxX; x++)
                        for (var y = r.MinY; y < r.MaxY; y++)
                            occupied[x, y] = true;
                }

                bool ColumnFree(int x, int minY, int maxY)
                {
                    for (var y = minY; y < maxY; y++)
                        if (occupied[x, y]) return false;
                    return true;
                }

                bool RowFree(int y, int minX, int maxX)
                {
                    for (var x = minX; x < maxX; x++)
                        if (occupied[x, y]) return false;
                    return true;
                }

                foreach (var r in rects)
                {
                    if (r.MaxX - r.MinX <= 0 || r.MaxY - r.MinY <= 0) continue;

                    var freeLeft = 0;
                    while (r.MinX - freeLeft - 1 >= 0 && ColumnFree(r.MinX - freeLeft - 1, r.MinY, r.MaxY))
                        freeLeft++;
                    var freeRight = 0;
                    while (r.MaxX + freeRight < texWidth && ColumnFree(r.MaxX + freeRight, r.MinY, r.MaxY))
                        freeRight++;
                    var freeUp = 0;
                    while (r.MinY - freeUp - 1 >= 0 && RowFree(r.MinY - freeUp - 1, r.MinX, r.MaxX))
                        freeUp++;
                    var freeDown = 0;
                    while (r.MaxY + freeDown < texHeight && RowFree(r.MaxY + freeDown, r.MinX, r.MaxX))
                        freeDown++;

                    growthByCode[r.Code] = new GrowthPotential(freeLeft, freeRight, freeUp, freeDown);
                }
            }

            var donors = new List<DonorSlot>();
            foreach (var code in baseSafeCandidates.Except(unsafeBase).Except(zeroArea))
                if (geometryByCode.TryGetValue((ushort)code, out var geo))
                    donors.Add(new DonorSlot((ushort)code, geo.Width, geo.Height));

            if (needsSoftDonors)
                foreach (var code in softCandidateCodes.Except(unsafeSoft).Except(zeroArea))
                    if (geometryByCode.TryGetValue((ushort)code, out var geo))
                        donors.Add(new DonorSlot((ushort)code, geo.Width, geo.Height));

            var allFontGlyphs = geometryByCode.Select(kv => new KnownGlyphGeometry(kv.Key, kv.Value.Width, kv.Value.Height)).ToList();
            var referenceCapHeight = GlyphDonorMatcher.GetReferenceCapHeight(allFontGlyphs);
            donors = GlyphDonorMatcher.FilterOutTooSmall(donors, referenceCapHeight);

            report.Log($"=== [{label}] {font.BaseName}: цільове розширення призначених донорів ===");

            if (donors.Count < CyrillicAlphabet.AllLetters.Count)
            {
                report.Log($"    UA: Донорів замало ({donors.Count} < {CyrillicAlphabet.AllLetters.Count}).");
                report.Log();
                continue;
            }

            // UA: Крок 1 — звичайне призначення на ПОТОЧНИХ розмірах.
            // EN: Step 1 — ordinary assignment on CURRENT sizes.
            var baseline = GlyphDonorMatcher.Assign(CyrillicAlphabet.AllLetters, donors, fontFamilyName, referenceCapHeight);
            var baselineAvgDeviation = baseline.Average(a => Math.Abs(Math.Log(a.DonorAspectRatio) - Math.Log(a.TargetAspectRatio)));

            // UA: Крок 2 — цільове розширення КОЖНОЇ вже призначеної пари.
            // EN: Step 2 — targeted growth of EACH already-assigned pair.
            var grownRects = new Dictionary<ushort, GlyphRect>();
            var improved = new List<(char Character, ushort Code, double OldDeviation, double NewDeviation, int NewWidth, int NewHeight)>();

            foreach (var a in baseline)
            {
                var rect = rectByCode[a.DonorCode];
                var growth = growthByCode[a.DonorCode];

                var width = rect.MaxX - rect.MinX;
                var height = rect.MaxY - rect.MinY;
                var currentRatio = (double)width / height;

                int newMinX = rect.MinX, newMaxX = rect.MaxX, newMinY = rect.MinY, newMaxY = rect.MaxY;

                if (a.TargetAspectRatio > currentRatio)
                {
                    // UA: Ціль ВІДНОСНО ШИРША — ростемо лише ширину, рівно
                    //     стільки, скільки потрібно (не більше вільного
                    //     простору, і не більше за потребу).
                    // EN: Target is RELATIVELY WIDER — grow width only,
                    //     exactly as much as needed (capped by both
                    //     available free space AND actual need).
                    var desiredWidth = height * a.TargetAspectRatio;
                    var neededGrow = (int)Math.Ceiling(desiredWidth - width);
                    var growRight = Math.Min(growth.FreeRight, neededGrow);
                    var remaining = neededGrow - growRight;
                    var growLeft = Math.Min(growth.FreeLeft, Math.Max(0, remaining));

                    newMaxX = rect.MaxX + growRight;
                    newMinX = rect.MinX - growLeft;
                }
                else if (a.TargetAspectRatio < currentRatio)
                {
                    // UA: Ціль ВІДНОСНО ВИЩА — ростемо лише висоту.
                    // EN: Target is RELATIVELY TALLER — grow height only.
                    var desiredHeight = width / a.TargetAspectRatio;
                    var neededGrow = (int)Math.Ceiling(desiredHeight - height);
                    var growDown = Math.Min(growth.FreeDown, neededGrow);
                    var remaining = neededGrow - growDown;
                    var growUp = Math.Min(growth.FreeUp, Math.Max(0, remaining));

                    newMaxY = rect.MaxY + growDown;
                    newMinY = rect.MinY - growUp;
                }

                grownRects[a.DonorCode] = new GlyphRect(a.DonorCode, newMinX, newMaxX, newMinY, newMaxY);

                var newWidth = newMaxX - newMinX;
                var newHeight = newMaxY - newMinY;
                var newRatio = (double)newWidth / newHeight;
                var oldDeviation = Math.Abs(Math.Log(currentRatio) - Math.Log(a.TargetAspectRatio));
                var newDeviation = Math.Abs(Math.Log(newRatio) - Math.Log(a.TargetAspectRatio));

                // UA: Запобіжник "ріст ніколи не погіршує": цілочисельне
                //     округлення (Math.Ceiling у розрахунку потрібного
                //     росту) інколи трохи ПЕРЕВИЩУЄ ідеальну пропорцію —
                //     непомітно на поганих донорах, але помітно на вже
                //     відмінних (BF2 gamefont_large/medium/small мали
                //     0,03-0,08 відхилення до росту — навіть 1px
                //     перевищення там відносно велике). Якщо розрахований
                //     ріст не покращує результат — відкочуємо до
                //     оригінального розміру, а не застосовуємо його.
                // EN: "Growth never regresses" safeguard: integer rounding
                //     (Math.Ceiling in the needed-growth calculation)
                //     sometimes slightly OVERSHOOTS the ideal ratio —
                //     unnoticeable on poorly-fitting donors, but
                //     noticeable on already-excellent ones (BF2
                //     gamefont_large/medium/small had 0.03-0.08 deviation
                //     before growth — even a 1px overshoot there is
                //     relatively large). If the computed growth doesn't
                //     improve the result — revert to the original size
                //     instead of applying it.
                if (newDeviation >= oldDeviation)
                {
                    grownRects[a.DonorCode] = rect;
                    newWidth = width;
                    newHeight = height;
                    newDeviation = oldDeviation;
                }

                if (newWidth != width || newHeight != height)
                    improved.Add((a.Character, a.DonorCode, oldDeviation, newDeviation, newWidth, newHeight));
            }

            var newAvgDeviation = baseline.Average(a =>
            {
                var r = grownRects[a.DonorCode];
                var ratio = (double)(r.MaxX - r.MinX) / (r.MaxY - r.MinY);
                return Math.Abs(Math.Log(ratio) - Math.Log(a.TargetAspectRatio));
            });

            report.Log($"    Середнє log-відхилення: без росту {baselineAvgDeviation:F3} → з цільовим ростом {newAvgDeviation:F3}");
            report.Log($"    Донорів, які реально виросли: {improved.Count} із {baseline.Count}");

            if (improved.Count > 0)
            {
                report.Log("    Літера  Донор  Було→Стало (відхилення)  Новий розмір");
                foreach (var im in improved.OrderByDescending(x => x.OldDeviation - x.NewDeviation).Take(20))
                    report.Log($"    {im.Character}       0x{im.Code:X2}   {im.OldDeviation:F3} → {im.NewDeviation:F3}         {im.NewWidth}x{im.NewHeight}");
            }

            // UA: Крок 3 — перевірка конфліктів ПІСЛЯ цільового (не
            //     максимального) росту.
            // EN: Step 3 — conflict check AFTER targeted (not maximal)
            //     growth.
            var conflicts = new List<string>();
            var grownList = baseline.Select(a => grownRects[a.DonorCode]).ToList();
            for (var i = 0; i < grownList.Count; i++)
            {
                for (var j = i + 1; j < grownList.Count; j++)
                {
                    var x = grownList[i];
                    var y = grownList[j];
                    var overlapsX = x.MinX < y.MaxX && y.MinX < x.MaxX;
                    var overlapsY = x.MinY < y.MaxY && y.MinY < x.MaxY;
                    if (overlapsX && overlapsY)
                        conflicts.Add($"0x{x.Code:X2} <-> 0x{y.Code:X2}");
                }
            }

            report.Log($"    Конфліктів розширення (два призначених донори накладаються один на одного): {conflicts.Count}");
            if (conflicts.Count > 0)
            {
                report.Log("    УВАГА, конфлікти (до 15):");
                foreach (var c in conflicts.Take(15))
                    report.Log($"      {c}");
            }

            // UA: Окремий блок — чи справді "особливі" літери (широкі
            //     багатоштрихові; зі спускним елементом; з рискою/крапкою
            //     зверху) системно гірші за звичайні. Рахується на
            //     БАЗОВОМУ (без росту) відхиленні — саме воно показує,
            //     наскільки важко вписати літеру в наявний пул донорів
            //     "як є".
            // EN: Separate block — are "special" letters (wide
            //     multi-stroke; with a descender; with a mark/dot above)
            //     really systematically worse than ordinary ones.
            //     Computed on BASELINE (no growth) deviation — that's
            //     what shows how hard it is to fit a letter into the
            //     existing donor pool "as is".
            report.Log();
            report.Log("    --- Особливі форми vs звичайні (базове відхилення, без росту) ---");

            bool IsWide(char c) => WideMultiStroke.Contains(c);
            bool IsDescender(char c) => HasDescender.Contains(c);
            bool IsAscenderMark(char c) => HasAscenderMark.Contains(c);
            bool IsSpecial(char c) => IsWide(c) || IsDescender(c) || IsAscenderMark(c);

            var baselineDeviationByChar = baseline.ToDictionary(
                a => a.Character,
                a => Math.Abs(Math.Log(a.DonorAspectRatio) - Math.Log(a.TargetAspectRatio)));

            void ReportGroup(string title, Func<char, bool> predicate)
            {
                var group = baselineDeviationByChar.Where(kv => predicate(kv.Key)).ToList();
                if (group.Count == 0) { report.Log($"    {title}: (жодної літери в цій групі)"); return; }
                var avg = group.Average(kv => kv.Value);
                var chars = string.Join("", group.Select(kv => kv.Key));
                report.Log($"    {title}: {group.Count} літер, середнє відхилення {avg:F3} — {chars}");
            }

            ReportGroup("Широкі багатоштрихові (Ж/Ш/Щ/Ю)", IsWide);
            ReportGroup("Зі спускним елементом (Р/У/Д/Ф/Ц/Щ)", IsDescender);
            ReportGroup("З рискою/крапкою зверху (Й/Ї/Ґ/Б/І)", IsAscenderMark);
            ReportGroup("УСІ особливі разом", IsSpecial);
            ReportGroup("Звичайні (без жодної особливої риси)", c => !IsSpecial(c));

            report.Log();
        }
    }
}
