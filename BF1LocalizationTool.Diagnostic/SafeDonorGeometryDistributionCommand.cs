// =============================================================================
// BF1LocalizationTool.Diagnostic — SafeDonorGeometryDistributionCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: Перед тим як проектувати алгоритм ПІДБОРУ донора під форму цільової
//     кириличної літери (замість силуваного вписування/обрізання) —
//     перевірка факту: чи є серед ефективно придатних донорів КОЖНОГО
//     шрифту реальне розмаїття розмірів слоту, чи всі вони приблизно
//     однакової ширини/висоти (тоді "розумний підбір" не дасть переваги
//     над послідовним призначенням, і основним інструментом стає
//     масштабування, не вибір).
//
//     Слоти донорів ЖОРСТКО ФІКСОВАНІ (та сама причина, що й у
//     SteamWorld Heist) — стратегія "перезаписати існуючий слот"
//     (FONT_FORMAT_SPEC.md розділ 5) свідомо НЕ рухає й НЕ змінює розмір
//     сусідніх гліфів в атласі, тож підбір донора — єдиний важіль, який
//     взагалі є, крім незначного масштабування.
//
//     Бере ОБИДВА пули водночас — базові безпечні донори
//     (PerFontSafeDonorCommand) і, якщо базових не вистачає на
//     neededGlyphCodes, додатково м'які донори (SoftDonorGeometryCheckCommand)
//     — щоб бачити ПОВНУ реальну картину, включно з BF2, де без м'яких
//     донорів картина неповна.
// EN: Before designing an algorithm that MATCHES a donor to a target
//     Cyrillic letter's shape (instead of forcing a fit / cropping) —
//     verify a fact: is there real size variety among EVERY font's
//     effectively usable donors, or are they all roughly the same
//     width/height (in which case "smart matching" gives no advantage
//     over sequential assignment, and scaling becomes the primary tool,
//     not selection).
//
//     Donor slots are RIGIDLY FIXED (the same reason as in SteamWorld
//     Heist) — the "overwrite existing slot" strategy
//     (FONT_FORMAT_SPEC.md section 5) deliberately does NOT move or
//     resize neighboring glyphs in the atlas, so donor matching is the
//     only lever available at all, besides minor scaling.
//
//     Takes BOTH pools at once — basic safe donors
//     (PerFontSafeDonorCommand) and, if basic ones fall short of
//     neededGlyphCodes, soft donors too (SoftDonorGeometryCheckCommand)
//     — to see the FULL real picture, including BF2, where the picture
//     is incomplete without soft donors.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class SafeDonorGeometryDistributionCommand
{
    private readonly record struct GlyphRect(ushort Code, int MinX, int MaxX, int MinY, int MaxY);
    private readonly record struct DonorGeometry(ushort Code, int Width, int Height, bool IsSoftDonor);

    public static async Task Run(DiagnosticReport report, UcfbChunk root, string filePath, string label, int neededGlyphCodes)
    {
        var summary = await SoftDonorAnalysis.BuildSummaryAsync(filePath);

        // UA: ВСТАНОВЛЕНО (реальний скріншот BF1 — "Exit to
        //     Windows" показало "WINDOГs": мала 'w', "не знайдена" в
        //     Locl, реально використовується поза ним). ДРУКОВНІ ASCII
        //     (0x20-0x7E) ЗАВЖДИ "зайняті" незалежно від Locl-сканування;
        //     лише недруковні керівні коди (0x00-0x1F, 0x7F) лишаються
        //     кандидатами через реальні дані.
        // EN: CONFIRMED (real BF1 screenshot — "Exit to Windows"
        //     showed "WINDOГs": lowercase 'w', "not found" in Locl, is
        //     actually used outside it). PRINTABLE ASCII (0x20-0x7E) is
        //     ALWAYS "used" regardless of the Locl scan; only non-printable
        //     control codes (0x00-0x1F, 0x7F) remain real-data candidates.
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

        report.Log($"=== [{label}] Розподіл розмірів ефективно придатних донорів ПО КОЖНОМУ шрифту ===");
        report.Log($"=== [{label}] Size distribution of effectively usable donors PER FONT ===");
        report.Log($"    Базових кандидатів: {baseSafeCandidates.Count}, потрібно: {neededGlyphCodes}, м'які донори враховуються: {(needsSoftDonors ? "так" : "ні (базових вистачає)")}");
        report.Log();

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

                        // UA: базовий критерій — перетин з БУДЬ-ЯКИМ використаним кодом
                        // EN: base criterion — overlap with ANY used code
                        var aUsed = IsUsed(a.Code);
                        var bUsed = IsUsed(b.Code);
                        if (aUsed != bUsed)
                            unsafeBase.Add(aUsed ? b.Code : a.Code);

                        // UA: м'який критерій — перетин конкретно з англійським кодом
                        // EN: soft criterion — overlap specifically with an English code
                        if (softCandidateCodes.Contains(a.Code) && englishUsed.Contains(b.Code))
                            unsafeSoft.Add(a.Code);
                        if (softCandidateCodes.Contains(b.Code) && englishUsed.Contains(a.Code))
                            unsafeSoft.Add(b.Code);
                    }
                }
            }

            var donors = new List<DonorGeometry>();
            foreach (var code in baseSafeCandidates.Except(unsafeBase).Except(zeroArea))
                if (geometryByCode.TryGetValue((ushort)code, out var geo))
                    donors.Add(new DonorGeometry((ushort)code, geo.Width, geo.Height, IsSoftDonor: false));

            if (needsSoftDonors)
                foreach (var code in softCandidateCodes.Except(unsafeSoft).Except(zeroArea))
                    if (geometryByCode.TryGetValue((ushort)code, out var geo))
                        donors.Add(new DonorGeometry((ushort)code, geo.Width, geo.Height, IsSoftDonor: true));

            report.Log($"  {font.BaseName}: {donors.Count} донорів усього ({donors.Count(d => !d.IsSoftDonor)} базових + {donors.Count(d => d.IsSoftDonor)} м'яких)");

            if (donors.Count == 0) { report.Log(); continue; }

            var widths = donors.Select(d => d.Width).OrderBy(w => w).ToList();
            var heights = donors.Select(d => d.Height).OrderBy(h => h).ToList();
            report.Log($"    Ширина: min={widths.First()} max={widths.Last()} унікальних значень={widths.Distinct().Count()}");
            report.Log($"    Висота: min={heights.First()} max={heights.Last()} унікальних значень={heights.Distinct().Count()}");

            report.Log("    Код   Ш×В      Пул");
            foreach (var d in donors.OrderBy(d => d.Width).ThenBy(d => d.Height))
                report.Log($"    0x{d.Code:X2}  {d.Width}x{d.Height}{(d.IsSoftDonor ? "   м'який" : "   базовий")}");

            report.Log();
        }
    }
}
