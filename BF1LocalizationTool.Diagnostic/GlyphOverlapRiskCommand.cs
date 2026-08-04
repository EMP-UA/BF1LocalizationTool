// =============================================================================
// BF1LocalizationTool.Diagnostic — GlyphOverlapRiskCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Класифікує КОЖЕН перетин UV-прямокутників (знайдений
//     GlyphRectOverlapCommand) за реальним ризиком для ін'єкції
//     кирилиці, а не просто рахує "скільки перетинів":
//
//     - USED×USED  — обидва коди реально використовує якась мова. Це
//       існуюча поведінка ОРИГІНАЛЬНОЇ гри — ми туди не пишемо, не наш
//       ризик.
//     - DONOR×USED — один код НЕ використовується жодною мовою (кандидат
//       у донори), інший ВИКОРИСТОВУЄТЬСЯ. КРИТИЧНИЙ РИЗИК: перезапис
//       донорського слоту може пошкодити реально видимий гравцю гліф.
//       Такий донор НЕ можна брати для ін'єкції.
//     - DONOR×DONOR — обидва коди не використовуються жодною мовою.
//       Безпечно для гравця (жоден не рендериться в оригіналі), АЛЕ:
//       якщо ми оберемо ОБИДВА ці донори для ін'єкції різних кириличних
//       літер, другий запис частково затре перший. Тому при виборі
//       фінального набору донорів ці пари мають бути взаємовиключними.
//
//     "Використовується" визначається ТАК САМО, як існуюча логіка
//     підбору SafeDonorCodes: код 0-255 — використаний, якщо реально
//     зустрічається в тексті БУДЬ-ЯКОЇ мови (LvlLocalizationService).
//     Код < 128 НЕ вважається зайнятим без перевірки: побайтова перевірка
//     підтверджує, що частина ASCII (у т.ч. деякі латинські літери в
//     BF1, де весь UI-текст — капс) реально ніколи не використовується
//     — тому окремого правила для < 128 немає, лише реальні дані.
// EN: Classifies EVERY UV-rectangle overlap (found by
//     GlyphRectOverlapCommand) by actual risk for Cyrillic injection,
//     rather than just counting "how many overlaps":
//
//     - USED×USED  — both codes are actually used by some language.
//       This is EXISTING behavior of the ORIGINAL game — we don't write
//       there, not our risk.
//     - DONOR×USED — one code is NOT used by any language (donor
//       candidate), the other IS used. CRITICAL RISK: overwriting the
//       donor slot could damage a glyph actually visible to the player.
//       Such a donor CANNOT be used for injection.
//     - DONOR×DONOR — both codes are unused by any language. Safe for
//       the player (neither renders in the original), BUT: if we choose
//       BOTH of these donors to inject different Cyrillic letters, the
//       second write will partially erase the first. So when selecting
//       the final donor set, such pairs must be mutually exclusive.
//
//     "Used" is determined THE SAME WAY as the existing SafeDonorCodes
//     selection logic: code 0-255 — used if it genuinely appears in ANY
//     language's text (LvlLocalizationService). Code < 128 is NOT assumed
//     always-used without verification: a byte-level check confirms part
//     of ASCII (including some Latin letters in BF1, where all UI text is
//     uppercase) is genuinely never used — so there's no separate rule
//     for < 128, only real data.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.Core.Localization;

namespace BF1LocalizationTool.Diagnostic;

public static class GlyphOverlapRiskCommand
{
    private readonly record struct GlyphRect(ushort Code, int MinX, int MaxX, int MinY, int MaxY);

    public static async Task Run(DiagnosticReport report, UcfbChunk root, string filePath, string label)
    {
        // UA: Той самий підхід, що вже є в Program.cs для SafeDonorCodes —
        //     не новий, не вигаданий алгоритм.
        // EN: The same approach already used in Program.cs for
        //     SafeDonorCodes — not a new, invented algorithm.
        var service = new LvlLocalizationService();
        await service.LoadAsync(filePath);

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
        var usedByAnyLanguage = new HashSet<int>();
        foreach (var lang in service.AvailableLanguages)
        {
            var file = service.GetLanguageFile(lang);
            if (file is null) continue;
            foreach (var entry in file.Entries)
                foreach (var c in entry.Original)
                    if (c is >= (char)0 and <= (char)255)
                        usedByAnyLanguage.Add(c);
        }

        bool IsUsed(ushort code) => IsPrintableAscii(code) || usedByAnyLanguage.Contains(code);

        var fonts = FontChunkLocator.FindAll(root);

        var usedUsedCount = 0;
        var donorUsedCount = 0;
        var donorDonorCount = 0;
        var donorUsedExamples = new List<string>();
        var donorDonorExamples = new List<string>();

        // UA: Коди, які НІКОЛИ не можна брати для ін'єкції, бо їхній
        //     UV-прямокутник перетинається з реально використовуваним
        //     гліфом хоч на одній сторінці хоч одного шрифту.
        // EN: Codes that can NEVER be used for injection, because their
        //     UV rectangle overlaps a genuinely used glyph on at least
        //     one page of at least one font.
        var unsafeDonorCodes = new HashSet<int>();

        // UA: Пари донорів, які взаємно виключні (не можна обирати обидва
        //     в фінальний набір ін'єкції).
        // EN: Mutually exclusive donor pairs (cannot select both into the
        //     final injection set).
        var mutuallyExclusiveDonorPairs = new List<(int, int)>();

        foreach (var font in fonts)
        {
            var fbod = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbod is null) continue;

            var glyphs = FontGlyphTable.Parse(fbod.RawData);

            foreach (var page in font.TexturePages)
            {
                var texPixels = FontTexturePixelReader.ReadMip0(page.Chunk);

                var rects = new List<GlyphRect>();
                foreach (var glyph in glyphs)
                {
                    var x0 = (int)Math.Round(glyph.U0 * texPixels.Width);
                    var x1 = (int)Math.Round(glyph.U1 * texPixels.Width);
                    var y0 = (int)Math.Round(glyph.V0 * texPixels.Height);
                    var y1 = (int)Math.Round(glyph.V1 * texPixels.Height);
                    rects.Add(new GlyphRect(glyph.Code,
                        Math.Min(x0, x1), Math.Max(x0, x1),
                        Math.Min(y0, y1), Math.Max(y0, y1)));
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

                        if (aUsed && bUsed)
                        {
                            usedUsedCount++;
                        }
                        else if (aUsed != bUsed)
                        {
                            donorUsedCount++;
                            var donorCode = aUsed ? b.Code : a.Code;
                            unsafeDonorCodes.Add(donorCode);
                            if (donorUsedExamples.Count < 10)
                                donorUsedExamples.Add(
                                    $"{font.BaseName}/{page.Name}: донор 0x{donorCode:X2} " +
                                    $"перетинається з ВИКОРИСТОВУВАНИМ 0x{(aUsed ? a.Code : b.Code):X2}");
                        }
                        else
                        {
                            donorDonorCount++;
                            mutuallyExclusiveDonorPairs.Add((a.Code, b.Code));
                            if (donorDonorExamples.Count < 10)
                                donorDonorExamples.Add(
                                    $"{font.BaseName}/{page.Name}: донор 0x{a.Code:X2} " +
                                    $"перетинається з донором 0x{b.Code:X2}");
                        }
                    }
                }
            }
        }

        report.Log($"=== [{label}] Класифікація ризику перетинів UV-прямокутників ===");
        report.Log($"    USED × USED (існуюча поведінка гри, не наш ризик):     {usedUsedCount}");
        report.Log($"    DONOR × USED (КРИТИЧНО — донор НЕ можна ін'єктувати):  {donorUsedCount}");
        report.Log($"    DONOR × DONOR (взаємовиключні пари при виборі набору): {donorDonorCount}");
        report.Log();
        report.Log($"    Унікальних донорських кодів, НАЗАВЖДИ виключених через DONOR×USED: {unsafeDonorCodes.Count}");
        if (unsafeDonorCodes.Count > 0)
            report.Log($"      {string.Join(", ", unsafeDonorCodes.OrderBy(x => x).Select(x => $"0x{x:X2}"))}");

        if (donorUsedExamples.Count > 0)
        {
            report.Log("    --- Приклади DONOR×USED (до 10) ---");
            foreach (var ex in donorUsedExamples) report.Log($"      {ex}");
        }

        if (donorDonorExamples.Count > 0)
        {
            report.Log("    --- Приклади DONOR×DONOR (до 10) ---");
            foreach (var ex in donorDonorExamples) report.Log($"      {ex}");
        }
    }
}