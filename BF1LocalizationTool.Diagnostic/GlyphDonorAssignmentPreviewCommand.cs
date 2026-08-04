// =============================================================================
// BF1LocalizationTool.Diagnostic — GlyphDonorAssignmentPreviewCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Прев'ю РЕАЛЬНОГО результату GlyphDonorMatcher (FontGenerator
//     проєкт) — на справжніх розмірах донорів кожного шрифту, перш ніж
//     довіряти цьому алгоритму призначення в GlyphAtlasPatcher. Не
//     записує нічого у файл — лише показує, яка літера якому донору
//     призначена і НАСКІЛЬКИ близька пропорція (щоб оком оцінити якість
//     призначення, а не вірити на слово алгоритму).
//
//     Геометрія донорів збирається ТИМ САМИМ фільтром, що й
//     PerFontSafeDonorCommand/SoftDonorGeometryCheckCommand (перетин у
//     межах PageIndex, виключення нульової площі, м'які донори коли
//     базових не вистачає) — щоб призначення спиралось на ту саму
//     множину, яку ми вже перевірили як дійсно придатну для перезапису.
// EN: Preview of the REAL GlyphDonorMatcher (FontGenerator project)
//     result — on each font's actual donor sizes, before trusting this
//     assignment algorithm inside GlyphAtlasPatcher. Writes nothing to a
//     file — just shows which letter got assigned to which donor and HOW
//     CLOSE the ratio is (to judge assignment quality by eye, not take
//     the algorithm's word for it).
//
//     Donor geometry is gathered with the SAME filter as
//     PerFontSafeDonorCommand/SoftDonorGeometryCheckCommand (overlap
//     within PageIndex only, zero-area exclusion, soft donors when basic
//     ones fall short) — so the assignment relies on the exact same set
//     we've already verified as genuinely safe to overwrite.
// =============================================================================

using System.Runtime.Versioning;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.FontGenerator.Matching;

namespace BF1LocalizationTool.Diagnostic;

[SupportedOSPlatform("windows")]
public static class GlyphDonorAssignmentPreviewCommand
{
    private readonly record struct GlyphRect(ushort Code, int MinX, int MaxX, int MinY, int MaxY);

    public static async Task Run(DiagnosticReport report, UcfbChunk root, string filePath, string label, string fontFamilyName, int neededGlyphCodes)
    {
        var summary = await SoftDonorAnalysis.BuildSummaryAsync(filePath);

        // UA: ДРУКОВНІ ASCII-символи (0x20-0x7E) завжди вважаються
        //     "зайнятими", незалежно від результату Locl-сканування:
        //     деякі рядки гри (напр. "Exit to Windows", де мала 'w'
        //     показується напряму) використовують друковні ASCII-коди
        //     поза таблицею Locl, тож саме лише Locl-сканування не
        //     доводить, що такий код вільний. Лише недруковні керівні
        //     коди (0x00-0x1F, 0x7F) лишаються кандидатами на основі
        //     даних реального сканування.
        // EN: PRINTABLE ASCII (0x20-0x7E) is always considered "used",
        //     regardless of the Locl scan result: some game strings
        //     (e.g. "Exit to Windows", where the lowercase 'w' is shown
        //     directly) use printable ASCII codes outside the Locl
        //     table, so the Locl scan alone doesn't prove such a code is
        //     free. Only non-printable control codes (0x00-0x1F, 0x7F)
        //     remain candidates based on real scan data.
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

            foreach (var pageGroup in glyphs.GroupBy(g => g.PageIndex))
            {
                if (pageGroup.Key >= font.TexturePages.Count) continue;

                var texPixels = FontTexturePixelReader.ReadMip0(font.TexturePages[pageGroup.Key].Chunk);

                var rects = pageGroup.Select(g =>
                {
                    var x0 = (int)Math.Round(g.U0 * texPixels.Width);
                    var x1 = (int)Math.Round(g.U1 * texPixels.Width);
                    var y0 = (int)Math.Round(g.V0 * texPixels.Height);
                    var y1 = (int)Math.Round(g.V1 * texPixels.Height);
                    return new GlyphRect(g.Code, Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1), Math.Max(y0, y1));
                }).ToList();

                foreach (var r in rects)
                {
                    geometryByCode[r.Code] = (r.MaxX - r.MinX, r.MaxY - r.MinY);
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
            }

            var donors = new List<DonorSlot>();
            foreach (var code in baseSafeCandidates.Except(unsafeBase).Except(zeroArea))
                if (geometryByCode.TryGetValue((ushort)code, out var geo))
                    donors.Add(new DonorSlot((ushort)code, geo.Width, geo.Height));

            if (needsSoftDonors)
                foreach (var code in softCandidateCodes.Except(unsafeSoft).Except(zeroArea))
                    if (geometryByCode.TryGetValue((ushort)code, out var geo))
                        donors.Add(new DonorSlot((ushort)code, geo.Width, geo.Height));

            // UA: Відсіюємо занадто дрібних донорів (ймовірно, діакритика/
            //     пунктуація, не повноцінні літери) — еталон береться з
            //     РЕАЛЬНИХ A-Z ЦЬОГО САМОГО шрифту, не вигаданий поріг.
            // EN: Filter out donors that are too small (likely
            //     diacritics/punctuation, not full letters) — the
            //     reference comes from REAL A-Z glyphs of THIS SAME font,
            //     not a made-up threshold.
            var allFontGlyphs = geometryByCode.Select(kv => new KnownGlyphGeometry(kv.Key, kv.Value.Width, kv.Value.Height)).ToList();
            var referenceCapHeight = GlyphDonorMatcher.GetReferenceCapHeight(allFontGlyphs);
            var beforeSizeFilter = donors.Count;
            donors = GlyphDonorMatcher.FilterOutTooSmall(donors, referenceCapHeight);

            report.Log($"=== [{label}] {font.BaseName}: призначення донорів ({donors.Count} доступно з {beforeSizeFilter} після фільтра розміру, потрібно {CyrillicAlphabet.AllLetters.Count}) ===");
            report.Log($"    Еталонна висота великої літери (медіана A-Z цього шрифту): {referenceCapHeight}px");

            if (donors.Count < CyrillicAlphabet.AllLetters.Count)
            {
                report.Log($"    UA: Донорів замало ({donors.Count} < {CyrillicAlphabet.AllLetters.Count}) — призначення пропущено.");
                report.Log();
                continue;
            }

            List<GlyphAssignment> assignments;
            try
            {
                assignments = GlyphDonorMatcher.Assign(CyrillicAlphabet.AllLetters, donors, fontFamilyName, referenceCapHeight);
            }
            catch (Exception ex)
            {
                report.Log($"    UA: Помилка призначення: {ex.Message}");
                report.Log();
                continue;
            }

            report.Log("    Літера  Донор  Слот(Ш×В)  Пропорція_цілі  Пропорція_донора  Відхилення");
            foreach (var a in assignments.OrderBy(a => a.Character))
            {
                var deviationPercent = (a.DonorAspectRatio / a.TargetAspectRatio - 1.0) * 100.0;
                report.Log($"    {a.Character}       0x{a.DonorCode:X2}   {a.CanvasWidth}x{a.CanvasHeight,-6} {a.TargetAspectRatio,6:F2}          {a.DonorAspectRatio,6:F2}            {deviationPercent,6:F1}%");
            }

            var avgAbsDeviation = assignments.Average(a => Math.Abs(Math.Log(a.DonorAspectRatio) - Math.Log(a.TargetAspectRatio)));
            report.Log($"    Середнє логарифмічне відхилення пропорції: {avgAbsDeviation:F3} (0 = ідеально)");
            report.Log();
        }
    }
}
