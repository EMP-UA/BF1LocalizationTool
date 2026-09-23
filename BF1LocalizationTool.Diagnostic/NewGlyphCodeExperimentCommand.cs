// =============================================================================
// BF1LocalizationTool.Diagnostic — NewGlyphCodeExperimentCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: ЕКСПЕРИМЕНТАЛЬНА команда: перевіряє, чи гра взагалі відмальовує
//     гліфи на кодах, яких НЕ було в оригінальному файлі. Уся наявна
//     донорська інфраструктура (CyrillicFontInjector,
//     GlyphAtlasPatcher.BuildReplacements) свідомо ПАТЧИТЬ лише ІСНУЮЧІ
//     коди — BuildReplacements навіть кидає виняток, якщо код не знайдено
//     серед originalRecords.
//
//     МЕТА: якщо гра малює гліфи на нових кодах — донорський підхід (з
//     таблицею відповідності cyrillic-code-table.json) можна замінити
//     прямим Unicode-кодуванням без жодного маппінгу байтів.
//
//     Тест охоплює УСІ 66 літер українського алфавіту (CyrillicAlphabet.
//     AllLetters), кожна — під СВОЇМ РЕАЛЬНИМ Unicode-кодом (напр. 'А' =
//     0x0410), і в УСІХ шрифтах гри (gamefont_large/medium/small/tiny/
//     super_tiny), а не лише в одному розмірі — бо різні екрани гри
//     малюють текст РІЗними розмірами шрифту, і якби кирилицю додати
//     лише в один шрифт, "негативний" результат на іншому екрані нічого
//     не довів би (можливо, там просто інший, непропатчений шрифт).
//     Літери, для яких на жодній сторінці шрифту не знайшлось вільного
//     місця потрібного розміру, ПРОПУСКАЮТЬСЯ (не форсуються) і чітко
//     перелічуються в звіті — щоб не приховувати межі цього тесту.
//
//     ІЗОЛЬОВАНО НАВМИСНО: НЕ чіпає GlyphAtlasPatcher/CyrillicFontInjector
//     (робочий, ретельно перевірений на 11 шрифтах обох ігор код) — уся
//     логіка цього одноразового тесту самодостатня в цьому файлі, для
//     нульового ризику для робочого донорського конвеєра. Пише лише в
//     окрему теку "new-glyph-test/" — вхідний reference-files/BF2/core.lvl
//     НІКОЛИ не змінюється.
//
//     ReservedByte4 нових записів: призначення поля невідоме (див.
//     FontGlyphRecord.cs) — оскільки для СПРАВЖНЬОГО нового гліфа немає
//     "донора", з якого копіювати реальне значення, беремо його з
//     ПЕРШОГО існуючого запису тієї ж сторінки — це РЕАЛЬНИЙ байт із
//     цього ж файлу, а не вигадане число.
// EN: EXPERIMENTAL command: tests whether the game renders glyphs at
//     codes that were NOT in the original file at all. The entire
//     existing donor infrastructure (CyrillicFontInjector,
//     GlyphAtlasPatcher.BuildReplacements) deliberately PATCHES existing
//     codes only — BuildReplacements even throws if a code isn't found
//     among originalRecords.
//
//     GOAL: if the game does render glyphs at new codes, the donor
//     approach (with its cyrillic-code-table.json mapping) could be
//     replaced by direct Unicode encoding with no byte mapping.
//
//     The test covers ALL 66 letters of the Ukrainian alphabet
//     (CyrillicAlphabet.AllLetters), each under its OWN REAL Unicode code
//     (e.g. 'А' = 0x0410), and in EVERY game font (gamefont_large/medium/
//     small/tiny/super_tiny), not just one size — because different game
//     screens draw text with DIFFERENT font sizes, and if Cyrillic were
//     added to only one font, a "negative" result on another screen would
//     prove nothing (that screen might just use a different, unpatched
//     font). Letters that don't fit anywhere on a given font's pages are
//     SKIPPED (never forced) and clearly listed in the report — so the
//     test's limits aren't hidden.
//
//     DELIBERATELY ISOLATED: does NOT touch GlyphAtlasPatcher/
//     CyrillicFontInjector (working code, thoroughly verified across 11
//     fonts in both games) — all of this one-off test's logic is
//     self-contained in this file, for zero risk to the working donor
//     pipeline. Only writes to a separate "new-glyph-test/" folder — the
//     input reference-files/BF2/core.lvl is NEVER modified.
//
//     New records' ReservedByte4: the field's purpose is unknown (see
//     FontGlyphRecord.cs) — since a GENUINELY new glyph has no "donor" to
//     copy a real value from, it's taken from the FIRST existing record
//     on the same page — a REAL byte from this same file, not an invented
//     number.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;
using BF1LocalizationTool.Core.IO;
using BF1LocalizationTool.FontGenerator.Matching;
using BF1LocalizationTool.FontGenerator.PixelConversion;

namespace BF1LocalizationTool.Diagnostic;

public static class NewGlyphCodeExperimentCommand
{
    // UA: Синхронізовано з production-командою
    //     (GenerateNoDonorCyrillicCoreCommand): Bahnschrift → Fira Sans
    //     SemiBold, через PrivateFontRegistry (.ttf у Fonts\, не система).
    //     Значення тепер ВІДНОСНИЙ ШЛЯХ ФАЙЛУ, не
    //     назва родини (GDI+ обрізає/зливає family-назви — див.
    //     GenerateNoDonorCyrillicCoreCommand.cs і FONT_FORMAT_SPEC.md
    //     розділ 11.13).
    // EN: Synced with the production command
    //     (GenerateNoDonorCyrillicCoreCommand): Bahnschrift → Fira Sans
    //     SemiBold, via PrivateFontRegistry (.ttf in Fonts\, not system).
    //     The value is now a RELATIVE FILE PATH, not
    //     a family name (GDI+ truncates/merges family names — see
    //     GenerateNoDonorCyrillicCoreCommand.cs and FONT_FORMAT_SPEC.md
    //     section 11.13).
    private const string FontFamilyName = "FiraSans-SemiBold.ttf";

    private static readonly string[] TargetFontBaseNames =
        ["gamefont_large", "gamefont_medium", "gamefont_small", "gamefont_tiny", "gamefont_super_tiny"];

    private sealed class PageState
    {
        public required UcfbChunk BodyChunk { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required bool[,] Occupied { get; init; }
        public required FontGlyphRecord FirstRecord { get; init; }
        public required byte[] PatchedBody { get; init; }
    }

    public static async Task Run(DiagnosticReport report)
    {
        var inputPath = Path.Combine(AppContext.BaseDirectory, "reference-files", "BF2", "core.lvl");
        if (!File.Exists(inputPath))
        {
            report.Log($"UA: Не знайдено {inputPath} — тест скасовано. / EN: {inputPath} not found — test cancelled.");
            return;
        }

        var root = UcfbReader.ReadFile(inputPath);
        var fonts = FontChunkLocator.FindAll(root);

        var replacements = new Dictionary<long, byte[]>();
        var placedByFont = new Dictionary<string, List<char>>();
        var skippedByFont = new Dictionary<string, List<char>>();

        foreach (var fontBaseName in TargetFontBaseNames)
        {
            var font = fonts.FirstOrDefault(f => f.BaseName == fontBaseName);
            if (font is null)
            {
                report.Log($"UA: [{fontBaseName}] Шрифт відсутній у BF2 — пропущено. / EN: [{fontBaseName}] Font not present in BF2 — skipped.");
                continue;
            }

            var fbodChunk = UcfbReader.FindFirst(font.Chunk, "FBOD");
            if (fbodChunk is null)
            {
                report.Log($"UA: [{fontBaseName}] FBOD не знайдено — пропущено. / EN: [{fontBaseName}] FBOD not found — skipped.");
                continue;
            }

            var originalRecords = FontGlyphTable.Parse(fbodChunk.RawData);

            // -----------------------------------------------------------------
            // UA: Опорні метрики цього шрифту — той самий підхід, що й
            //     CyrillicFontInjector.
            // EN: This font's reference metrics — the same approach as
            //     CyrillicFontInjector.
            // -----------------------------------------------------------------
            var allEnglishCellHeights = originalRecords.Select(r => (int)r.CellHeight).ToList();
            var englishCapBearings = originalRecords
                .Where(r => r.Code is >= (ushort)'A' and <= (ushort)'Z')
                .Select(r => (int)r.Bearing)
                .ToList();

            FontMetricReference metricReference;
            try
            {
                metricReference = GlyphMetricModel.DeriveReference(
                    allEnglishCellHeights, englishCapBearings, CyrillicAlphabet.UppercaseLetters, FontFamilyName);
            }
            catch (Exception ex)
            {
                report.Log($"UA: [{fontBaseName}] Не вдалося вивести опорні метрики: {ex.Message} — пропущено. / " +
                           $"EN: [{fontBaseName}] Could not derive reference metrics: {ex.Message} — skipped.");
                continue;
            }

            // -----------------------------------------------------------------
            // UA: Карти зайнятості КОЖНОЇ сторінки цього шрифту — з УСІХ
            //     реальних записів, ПЛЮС мутована робоча копія BODY (буде
            //     патчитись по одній літері за раз).
            // EN: Occupancy maps for EACH page of this font — from ALL real
            //     records, PLUS a mutable working copy of BODY (patched one
            //     letter at a time).
            // -----------------------------------------------------------------
            var pagesByIndex = new Dictionary<byte, PageState>();
            foreach (var pageGroup in originalRecords.GroupBy(r => r.PageIndex))
            {
                if (pageGroup.Key >= font.TexturePages.Count) continue;

                var texPixels = FontTexturePixelReader.ReadMip0(font.TexturePages[pageGroup.Key].Chunk);
                var texWidth = texPixels.Width;
                var texHeight = texPixels.Height;
                var occupied = new bool[texWidth, texHeight];

                foreach (var r in pageGroup)
                {
                    var x0 = (int)Math.Round(Math.Min(r.U0, r.U1) * texWidth);
                    var x1 = (int)Math.Round(Math.Max(r.U0, r.U1) * texWidth);
                    var y0 = (int)Math.Round(Math.Min(r.V0, r.V1) * texHeight);
                    var y1 = (int)Math.Round(Math.Max(r.V0, r.V1) * texHeight);
                    for (var x = Math.Max(0, x0); x < Math.Min(texWidth, x1); x++)
                        for (var y = Math.Max(0, y0); y < Math.Min(texHeight, y1); y++)
                            occupied[x, y] = true;
                }

                pagesByIndex[pageGroup.Key] = new PageState
                {
                    BodyChunk = texPixels.BodyChunk,
                    Width = texWidth,
                    Height = texHeight,
                    Occupied = occupied,
                    FirstRecord = pageGroup.First(),
                    PatchedBody = (byte[])texPixels.BodyChunk.RawData.Clone(),
                };
            }

            // -----------------------------------------------------------------
            // UA: Розміщуємо КОЖНУ з 66 літер під її РЕАЛЬНИМ Unicode-кодом.
            //     Перша сторінка з вільним місцем потрібного розміру виграє;
            //     якщо жодна не підходить — літера пропускається (і це
            //     видно в звіті), а не форсується у зайняте місце.
            // EN: Place EACH of the 66 letters under its REAL Unicode code.
            //     The first page with enough free space wins; if none fit
            //     — the letter is skipped (visible in the report), never
            //     forced into occupied space.
            // -----------------------------------------------------------------
            var newRecords = new List<FontGlyphRecord>();
            var placed = new List<char>();
            var skipped = new List<char>();

            foreach (var ch in CyrillicAlphabet.AllLetters)
            {
                var code = (ushort)ch;
                if (originalRecords.Any(r => r.Code == code) || newRecords.Any(r => r.Code == code))
                {
                    skipped.Add(ch); // UA: код вже зайнятий (не мало б траплятись для кирилиці) / EN: code already taken (shouldn't happen for Cyrillic)
                    continue;
                }

                LetterTargetMetric metric;
                try { metric = GlyphMetricModel.ComputeLetterMetric(ch, FontFamilyName, metricReference); }
                catch (Exception) { skipped.Add(ch); continue; }

                var didPlace = false;
                foreach (var (pageIndex, page) in pagesByIndex.OrderBy(kv => kv.Key))
                {
                    if (!TryFindFreeRect(page.Occupied, page.Width, page.Height, metric.BoxWidth, metric.BoxHeight, out var fx, out var fy))
                        continue;

                    ClaimRect(page.Occupied, fx, fy, metric.BoxWidth, metric.BoxHeight);

                    var rasterized = GlyphBoxFitRenderer.RenderToFit(ch, FontFamilyName, metric.BoxWidth, metric.BoxHeight);
                    var pixelBytes = GlyphPixelConverter.ToA4R4G4B4(rasterized);

                    for (var row = 0; row < metric.BoxHeight; row++)
                    {
                        var destOffset = ((fy + row) * page.Width + fx) * 2;
                        var srcOffset = row * metric.BoxWidth * 2;
                        Array.Copy(pixelBytes, srcOffset, page.PatchedBody, destOffset, metric.BoxWidth * 2);
                    }

                    var xAdvancePadding = originalRecords
                        .Where(r => r.InkWidth > 0)
                        .Select(r => r.XAdvance - r.InkWidth)
                        .OrderBy(d => d)
                        .ToList();
                    var medianPadding = xAdvancePadding.Count > 0 ? xAdvancePadding[xAdvancePadding.Count / 2] : 2;
                    var newXAdvance = (byte)Math.Clamp(metric.BoxWidth + medianPadding, 0, 255);

                    newRecords.Add(new FontGlyphRecord
                    {
                        Index = originalRecords.Count + newRecords.Count,
                        Code = code,
                        PageIndex = pageIndex,
                        XAdvance = newXAdvance,
                        ReservedByte4 = page.FirstRecord.ReservedByte4,
                        InkWidth = (byte)Math.Clamp(metric.BoxWidth, 0, 255),
                        Bearing = metric.Bearing,
                        CellHeight = metric.CellHeight,
                        U0 = fx / (float)page.Width,
                        U1 = (fx + metric.BoxWidth) / (float)page.Width,
                        V0 = fy / (float)page.Height,
                        V1 = (fy + metric.BoxHeight) / (float)page.Height,
                    });

                    placed.Add(ch);
                    didPlace = true;
                    break;
                }

                if (!didPlace) skipped.Add(ch);
            }

            placedByFont[fontBaseName] = placed;
            skippedByFont[fontBaseName] = skipped;

            report.Log($"UA: [{fontBaseName}] Розміщено {placed.Count}/66 літер. " +
                       (skipped.Count > 0 ? $"Пропущено (немає місця): {new string(skipped.ToArray())}" : "Пропущених немає.") +
                       $" / EN: [{fontBaseName}] Placed {placed.Count}/66 letters. " +
                       (skipped.Count > 0 ? $"Skipped (no space): {new string(skipped.ToArray())}" : "None skipped."));

            if (newRecords.Count == 0) continue;

            foreach (var page in pagesByIndex.Values)
                replacements[page.BodyChunk.FileDataOffset] = page.PatchedBody;

            // -----------------------------------------------------------------
            // UA: КРИТИЧНО (знайдено після другого тесту в грі — "частина
            //     літер, частина боксів"; ЕМПІРИЧНО підтверджено, що
            //     оригінальні 226 записів СТРОГО відсортовані за кодом
            //     0x1E→0xFF, без жодної інверсії — тож гра майже напевно
            //     шукає гліф БІНАРНИМ пошуком, який працює лише на
            //     відсортованому масиві). Перша версія дописувала нові
            //     записи в кінець у порядку АЛФАВІТУ (А,Б,В,Г,Ґ,Д,Е,Є...),
            //     а не за кодом — Ґ(0x0490) опинявся перед Д(0x0414),
            //     Є(0x0404) — аж наприкінці: суцільні інверсії. Через це
            //     бінарний пошук знаходив лише частину кириличних кодів
            //     (звідси "частина літер, частина боксів"). СОРТУЄМО весь
            //     масив за кодом — оскільки всі кириличні коди > 0xFF, вони
            //     стають відсортованим хвостом після оригінальних, і пошук
            //     працює для всіх. record.Index — суто C#-службова
            //     нумерація (гра її не читає), тож переупорядкування безпечне.
            // EN: CRITICAL (found after the second in-game test — "some
            //     letters, some boxes"; EMPIRICALLY confirmed the original
            //     226 records are STRICTLY sorted by code 0x1E→0xFF with no
            //     inversion — so the game almost certainly looks glyphs up
            //     by BINARY SEARCH, which only works on a sorted array). The
            //     first version appended new records in ALPHABET order
            //     (А,Б,В,Г,Ґ,Д,Е,Є...), not code order — Ґ(0x0490) landed
            //     before Д(0x0414), Є(0x0404) at the very end: many
            //     inversions. So binary search found only some Cyrillic
            //     codes (hence "some letters, some boxes"). SORT the whole
            //     array by code — since all Cyrillic codes are > 0xFF, they
            //     become a sorted tail after the originals, and lookup works
            //     for all. record.Index is plain C# bookkeeping (the game
            //     doesn't read it), so reordering is safe.
            var updatedRecords = originalRecords.Concat(newRecords)
                .OrderBy(r => r.Code)
                .ToList();
            replacements[fbodChunk.FileDataOffset] = FontGlyphTable.Serialize(updatedRecords);

            // -----------------------------------------------------------------
            // UA: КРИТИЧНО: перші 2 байти HEAD — це КІЛЬКІСТЬ ГЛІФІВ у
            //     шрифті (u16 LE). Якщо цей лічильник не оновити — гра читає
            //     лише стільки гліфів, скільки в ньому записано, і ФІЗИЧНО
            //     не бачить нових записів понад це число. Лічильник
            //     оновлюється на нову загальну кількість (тут, у ЦЬОМУ файлі
            //     — інлайн-патч, не через FontResourceBuilder).
            //     `FontResourceBuilder.BuildHead` сам виводить glyphCount з
            //     `font.Glyphs.Count` (не хардкодить його) — див.
            //     FONT_FORMAT_SPEC.md §7.1.
            // EN: CRITICAL: HEAD's first 2 bytes are the font's GLYPH COUNT
            //     (u16 LE). If this count isn't updated, the game reads only
            //     as many glyphs as it records, and PHYSICALLY never sees
            //     records added past that number. The count is updated to
            //     the new total here (in THIS file — an inline patch, not
            //     via FontResourceBuilder). `FontResourceBuilder.BuildHead`
            //     itself derives glyphCount from `font.Glyphs.Count` (not
            //     hardcoded) — see FONT_FORMAT_SPEC.md §7.1.
            var headChunk = UcfbReader.FindFirst(font.Chunk, "HEAD");
            if (headChunk is not null && headChunk.RawData.Length >= 2)
            {
                var patchedHead = (byte[])headChunk.RawData.Clone();
                var oldCount = BitConverter.ToUInt16(patchedHead, 0);
                var newCount = (ushort)updatedRecords.Count;
                BitConverter.GetBytes(newCount).CopyTo(patchedHead, 0);
                replacements[headChunk.FileDataOffset] = patchedHead;
                report.Log($"UA: [{fontBaseName}] HEAD: лічильник гліфів {oldCount} → {newCount}. / " +
                           $"EN: [{fontBaseName}] HEAD: glyph count {oldCount} → {newCount}.");
            }
            else
            {
                report.Log($"UA: [{fontBaseName}] УВАГА — HEAD не знайдено або закороткий, лічильник гліфів НЕ оновлено (нові гліфи гра, ймовірно, не побачить). / " +
                           $"EN: [{fontBaseName}] WARNING — HEAD not found or too short, glyph count NOT updated (the game likely won't see the new glyphs).");
            }
        }

        if (replacements.Count == 0)
        {
            report.Log("UA: Жодної літери не вдалося розмістити в жодному шрифті — файл НЕ записано. / " +
                       "EN: No letter could be placed in any font — file NOT written.");
            return;
        }

        var outputDir = Path.Combine(AppContext.BaseDirectory, "new-glyph-test", "BF2");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "core.lvl");

        var newBytes = UcfbWriter.WriteFile(root, replacements);
        File.WriteAllBytes(outputPath, newBytes);

        report.Log();
        report.Log($"UA: Записано: {outputPath} (оригінал не змінено). / EN: Written: {outputPath} (original untouched).");

        // ---------------------------------------------------------------------
        // UA: Round-trip перевірка — по КОЖНОМУ обробленому шрифту.
        // EN: Round-trip check — for EACH processed font.
        // ---------------------------------------------------------------------
        report.Log();
        report.Log("UA: Round-trip перевірка... / EN: Round-trip check...");
        try
        {
            var rereadRoot = UcfbReader.ReadFile(outputPath);
            var rereadFonts = FontChunkLocator.FindAll(rereadRoot);

            foreach (var fontBaseName in placedByFont.Keys.Where(k => placedByFont[k].Count > 0))
            {
                var rereadFont = rereadFonts.FirstOrDefault(f => f.BaseName == fontBaseName);
                var rereadFbod = rereadFont is not null ? UcfbReader.FindFirst(rereadFont.Chunk, "FBOD") : null;
                if (rereadFbod is null)
                {
                    report.Log($"UA: [{fontBaseName}] КРИТИЧНО — FBOD не знайдено після перезапису. / EN: [{fontBaseName}] CRITICAL — FBOD not found after rewrite.");
                    continue;
                }

                var rereadRecords = FontGlyphTable.Parse(rereadFbod.RawData);
                var expectedNew = placedByFont[fontBaseName];
                var foundCodes = expectedNew.Count(ch => rereadRecords.Any(r => r.Code == (ushort)ch));

                var rereadHead = UcfbReader.FindFirst(rereadFont!.Chunk, "HEAD");
                var headCount = rereadHead is not null && rereadHead.RawData.Length >= 2
                    ? BitConverter.ToUInt16(rereadHead.RawData, 0)
                    : -1;
                var headOk = headCount == rereadRecords.Count;

                var sorted = Enumerable.Range(0, rereadRecords.Count - 1)
                    .All(i => rereadRecords[i].Code <= rereadRecords[i + 1].Code);

                report.Log($"UA: [{fontBaseName}] Перечитано {rereadRecords.Count} записів, з {expectedNew.Count} нових знайдено {foundCodes}. " +
                           $"HEAD-лічильник={headCount} ({(headOk ? "OK" : "НЕ ЗБІГАЄТЬСЯ")}). " +
                           $"Відсортовано за кодом: {(sorted ? "ТАК (бінарний пошук гри спрацює)" : "НІ — ПОМИЛКА")}. / " +
                           $"EN: [{fontBaseName}] Reread {rereadRecords.Count} records, found {foundCodes} of {expectedNew.Count} new ones. " +
                           $"HEAD count={headCount} ({(headOk ? "OK" : "MISMATCH")}). " +
                           $"Sorted by code: {(sorted ? "YES (game's binary search will work)" : "NO — BUG")}.");
            }

            report.Log("UA: Round-trip перевірка завершена. / EN: Round-trip check complete.");
        }
        catch (Exception ex)
        {
            report.Log($"UA: КРИТИЧНО — round-trip провалився: {ex.Message} / EN: CRITICAL — round-trip failed: {ex.Message}");
            return;
        }

        // ---------------------------------------------------------------------
        // UA: Готовий рядок для копіювання — усі УСПІШНО розміщені літери
        //     (перетин по шрифтах, що реально знадобляться) для зручного
        //     тесту в GUI.
        // EN: Ready-to-copy string — all SUCCESSFULLY placed letters (across
        //     the fonts that will actually be used) for convenient GUI testing.
        // ---------------------------------------------------------------------
        var anyPlaced = placedByFont.Values.SelectMany(v => v).Distinct().OrderBy(c => c).ToArray();
        report.Log();
        report.Log($"UA: Рядок для тесту (усі розміщені літери, будь-де): {new string(anyPlaced)}");
        report.Log($"EN: Test string (all placed letters, any font): {new string(anyPlaced)}");

        report.Log();
        report.Log("UA: НАСТУПНІ КРОКИ (реальний тест можливий ЛИШЕ в грі):");
        report.Log("EN: NEXT STEPS (the real test is only possible IN-GAME):");
        report.Log($"UA:   1. У GUI відкрийте \"Оригінал\" І \"Робочий файл\" — обидва: {outputPath}");
        report.Log($"EN:   1. In the GUI, open BOTH \"Original\" AND \"Working file\" as: {outputPath}");
        report.Log("UA:   2. Ця тека НЕ має cyrillic-code-table.json — навмисно (перевіряємо СИРИЙ Unicode, без маппінгу).");
        report.Log("EN:   2. This folder has NO cyrillic-code-table.json — deliberate (testing RAW Unicode, no mapping).");
        report.Log("UA:   3. ВАЖЛИВО — не один рядок: впишіть кириличний переклад у КІЛЬКА коротких, точно впізнаваних рядків " +
                   "(напр. назви пунктів головного меню — те, що ви побачите одразу після запуску, без заглиблення в підменю), і збережіть.");
        report.Log("EN:   3. IMPORTANT — not just one row: type a Cyrillic translation into SEVERAL short, definitely-recognizable " +
                   "strings (e.g. main-menu item labels — what you see right after launch, no need to dig into submenus), and save.");
        report.Log("UA:   4. При попередженні \"шрифт без кириличних гліфів\" — тисніть Так (очікувано).");
        report.Log("EN:   4. On the \"font has no Cyrillic glyphs\" warning — click Yes (expected).");
        report.Log("UA:   5. Скопіюйте new-glyph-test/BF2/core.lvl поверх встановленої гри (З BACKUP!) і перевірте усі змінені екрани.");
        report.Log("EN:   5. Copy new-glyph-test/BF2/core.lvl over the installed game (WITH A BACKUP!) and check every changed screen.");
        report.Log("UA: Якщо кирилиця з'явиться ХОЧА Б на одному екрані — гра відмальовує коди поза оригінальним набором. " +
                   "Якщо НІДЕ (на кількох перевірених екранах різного розміру шрифту) — підхід без донорів не працює на цій грі.");
        report.Log("EN: If Cyrillic shows up on AT LEAST one screen — the game does render codes outside the original set. " +
                   "If NOWHERE (across several checked screens of different font sizes) — the no-donor approach doesn't work on this game.");

        await Task.CompletedTask;
    }

    private static bool TryFindFreeRect(bool[,] occupied, int texWidth, int texHeight, int w, int h, out int foundX, out int foundY)
    {
        for (var y = 0; y <= texHeight - h; y++)
        {
            for (var x = 0; x <= texWidth - w; x++)
            {
                if (IsRectFree(occupied, x, y, w, h))
                {
                    foundX = x;
                    foundY = y;
                    return true;
                }
            }
        }

        foundX = 0;
        foundY = 0;
        return false;
    }

    private static bool IsRectFree(bool[,] occupied, int x0, int y0, int w, int h)
    {
        for (var x = x0; x < x0 + w; x++)
            for (var y = y0; y < y0 + h; y++)
                if (occupied[x, y]) return false;
        return true;
    }

    private static void ClaimRect(bool[,] occupied, int x0, int y0, int w, int h)
    {
        for (var x = x0; x < x0 + w; x++)
            for (var y = y0; y < y0 + h; y++)
                occupied[x, y] = true;
    }
}
