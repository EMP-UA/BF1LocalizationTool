// =============================================================================
// BF1LocalizationTool.FontGenerator — AtlasPatching/GlyphAtlasPatcher.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Крок за розділом 10 FONT_FORMAT_SPEC.md: вставка вже
//     сконвертованих пікселів гліфа (готовий byte[] з
//     GlyphPixelConverter.ToA4R4G4B4) у повний масив пікселів ПОТРІБНОЇ
//     текстурної сторінки (за фізичним слотом донора), ПЛЮС оновлення
//     FBOD-запису кожного донора (нові U0-V1 з фактичного прямокутника,
//     новий XAdvance і InkWidth; PageIndex/Bearing/CellHeight/
//     ReservedByte4 завжди копіюються з оригінального запису донора —
//     без вигаданих значень), і підготовка Dictionary<long,byte[]> для
//     UcfbWriter.WriteFile.
//
//     Патчаться РАЗОМ і BODY, і FBOD, бо:
//       а) при цільовому рості прямокутника (підтверджено
//          TargetedGrowthDonorAssignmentPreviewCommand — без регресу на
//          всіх 11 шрифтах) старі U0-V1 оригінального слоту донора вже не
//          описують реальне розташування пікселів;
//       б) XAdvance має оновлюватись пропорційно новій ширині
//          (підтверджено FontGlyphMetricsCorrelationCommand: XAdvance ≈
//          InkWidth + стала, стандартне відхилення 0,40-1,44 по всіх 11
//          шрифтах).
//
//     Контракт UcfbWriter.WriteFile підтверджено РЕАЛЬНИМ використанням
//     у UcfbWriteRoundTripCommand.cs: ключ Dictionary —
//     chunk.FileDataOffset, значення — ПОВНИЙ новий масив байтів чанку.
//
//     FBOD-патчі РІЗНИХ шрифтів НІКОЛИ не конфліктують (кожен шрифт має
//     свій FBOD-чанк з власним FileDataOffset). Патчі на ОДНІЙ сторінці
//     (однаковий BodyChunk.FileDataOffset) групуються на ОДНУ копію BODY.
// EN: Step from FONT_FORMAT_SPEC.md section 10: inserting already
//     converted glyph pixels (a ready byte[] from
//     GlyphPixelConverter.ToA4R4G4B4) into the full pixel array of the
//     NEEDED texture page (at the donor's physical slot), PLUS updating
//     each donor's FBOD record (new U0-V1 from the actual rectangle, new
//     XAdvance and InkWidth; PageIndex/Bearing/CellHeight/ReservedByte4
//     are always copied from the donor's original record — no invented
//     values), and preparing a Dictionary<long,byte[]> for
//     UcfbWriter.WriteFile.
//
//     Both BODY and FBOD are patched together, because:
//       a) with targeted slot growth (confirmed by
//          TargetedGrowthDonorAssignmentPreviewCommand — no regression
//          across all 11 fonts) the donor's original slot U0-V1 no
//          longer describe the actual pixel location;
//       b) XAdvance must update proportionally to the new width
//          (confirmed by FontGlyphMetricsCorrelationCommand: XAdvance ≈
//          InkWidth + a per-font constant, standard deviation 0.40-1.44
//          across all 11 fonts).
//
//     The UcfbWriter.WriteFile contract was confirmed by REAL usage in
//     UcfbWriteRoundTripCommand.cs: Dictionary key is
//     chunk.FileDataOffset, value is the FULL new chunk byte array.
//
//     FBOD patches for DIFFERENT fonts NEVER conflict (each font has its
//     own FBOD chunk with its own FileDataOffset). Patches on the SAME
//     page (identical BodyChunk.FileDataOffset) are grouped onto ONE BODY
//     copy.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Fonts;

namespace BF1LocalizationTool.FontGenerator.AtlasPatching;

// UA: Один запит на патч: "запиши ЦІ байти пікселів у ЦЕЙ прямокутник
//     ЦІЄЇ сторінки, І онови FBOD-запис цього коду відповідно". Розмір
//     прямокутника МОЖЕ відрізнятись від оригінального донора (цільовий
//     ріст, підтверджений TargetedGrowthDonorAssignmentPreviewCommand —
//     покращення без регресу на всіх 11 шрифтах обох ігор) — саме тому
//     FBOD патчиться разом із пікселями: якщо прямокутник виріс, старі
//     U0-V1 не описують реальну позицію.
// EN: One patch request: "write THESE pixel bytes into THIS rectangle of
//     THIS page, AND update this code's FBOD record accordingly".
//     Rectangle size CAN differ from the original donor (targeted
//     growth, confirmed by TargetedGrowthDonorAssignmentPreviewCommand —
//     improvement with no regression across all 11 fonts in both games)
//     — that's exactly why FBOD is patched together with the pixels: if
//     the rectangle grew, the old U0-V1 no longer describe the real
//     position.
public sealed record GlyphPatch
{
    // UA: Оригінальний запис ЦЬОГО коду з FBOD ПЕРЕД патчем — джерело
    //     PageIndex/Bearing/CellHeight/ReservedByte4, які ЗАВЖДИ
    //     копіюються без змін (жодних підстав їх міняти — Bearing/
    //     CellHeight не корелюють із розміром UV настільки чітко, щоб
    //     вигадувати для них нову формулу; ReservedByte4 взагалі
    //     невідомого призначення). Також дає Code для пошуку запису в
    //     списку при перезаписі.
    // EN: The ORIGINAL record of THIS code from FBOD before patching —
    //     source of PageIndex/Bearing/CellHeight/ReservedByte4, which
    //     are ALWAYS copied unchanged (no grounds to change them —
    //     Bearing/CellHeight don't correlate with UV size cleanly enough
    //     to invent a new formula for them; ReservedByte4 is of
    //     completely unknown purpose). Also supplies Code to locate the
    //     record in the list when replacing it.
    public required FontGlyphRecord DonorRecord { get; init; }

    // UA: BODY-чанк ЦІЄЇ КОНКРЕТНОЇ текстурної сторінки (напр.
    //     texPixels.BodyChunk із FontTexturePixelReader.ReadMip0).
    // EN: The BODY chunk of THIS SPECIFIC texture page (e.g.
    //     texPixels.BodyChunk from FontTexturePixelReader.ReadMip0).
    public required UcfbChunk BodyChunk { get; init; }

    // UA: Повна ширина/висота текстурної сторінки в пікселях (напр.
    //     texPixels.Width/Height) — потрібні для обчислення офсету
    //     (row*TextureWidth+col)*2, підтвердженого
    //     BodySizeConsistencyCommand для КОЖНОЇ сторінки обох ігор, і
    //     для перерахунку U0-V1 з пікселів назад у частки [0,1].
    // EN: Full width/height of the texture page in pixels (e.g.
    //     texPixels.Width/Height) — needed to compute the offset
    //     (row*TextureWidth+col)*2, confirmed by
    //     BodySizeConsistencyCommand for EVERY page of both games, and
    //     to convert U0-V1 back from pixels into [0,1] fractions.
    public required int TextureWidth { get; init; }
    public required int TextureHeight { get; init; }

    // UA: Верхній лівий кут ПРЯМОКУТНИКА, У ЯКИЙ ПИШЕМО (не обов'язково
    //     оригінальний прямокутник донора — може бути результатом
    //     цільового росту, TargetedGrowthDonorAssignmentPreviewCommand).
    // EN: Top-left corner of the rectangle WE'RE WRITING INTO (not
    //     necessarily the donor's original rectangle — may be the result
    //     of targeted growth, TargetedGrowthDonorAssignmentPreviewCommand).
    public required int RectMinX { get; init; }
    public required int RectMinY { get; init; }

    // UA: Розмір прямокутника — МАЄ дорівнювати розміру, з яким
    //     викликався GdiGlyphRasterizer (options.CanvasWidth/Height), і,
    //     відповідно, розміру PixelBytesA4R4G4B4.
    // EN: Rectangle size — MUST equal the size GdiGlyphRasterizer was
    //     called with (options.CanvasWidth/Height), and, correspondingly,
    //     the size of PixelBytesA4R4G4B4.
    public required int CanvasWidth { get; init; }
    public required int CanvasHeight { get; init; }

    // UA: Готові байти з GlyphPixelConverter.ToA4R4G4B4(rasterizedGlyph).
    //     Довжина МАЄ бути точно CanvasWidth*CanvasHeight*2 —
    //     перевіряється в BuildReplacements, не мовчки обрізається чи
    //     доповнюється.
    // EN: Ready bytes from GlyphPixelConverter.ToA4R4G4B4(rasterizedGlyph).
    //     Length MUST be exactly CanvasWidth*CanvasHeight*2 — checked in
    //     BuildReplacements, never silently truncated or padded.
    public required byte[] PixelBytesA4R4G4B4 { get; init; }

    // UA: Нове значення XAdvance для FBOD-запису. НЕ обчислюється тут —
    //     викликаючий код відповідає за формулу (підтверджено:
    //     XAdvance ≈ InkWidth + стала для цього шрифту,
    //     FontGlyphMetricsCorrelationCommand, стандартне відхилення
    //     0,40-1,44 по всіх 11 шрифтах — тісна кореляція, але сама стала
    //     різна для кожного шрифту, тому рахується зовні).
    // EN: New XAdvance value for the FBOD record. NOT computed here —
    //     the calling code is responsible for the formula (confirmed:
    //     XAdvance ≈ InkWidth + a per-font constant,
    //     FontGlyphMetricsCorrelationCommand, standard deviation
    //     0.40-1.44 across all 11 fonts — tight correlation, but the
    //     constant itself differs per font, so it's computed externally).
    public required byte NewXAdvance { get; init; }

    // UA: Нове значення InkWidth. Рекомендація (не примус): = CanvasWidth,
    //     бо GlyphBoxFitRenderer заповнює прямокутник впритул (без полів)
    //     — узгоджено з підтвердженою "tight crop" природою слотів BF1.
    // EN: New InkWidth value. A recommendation (not enforced): =
    //     CanvasWidth, since GlyphBoxFitRenderer fills the rectangle
    //     edge-to-edge (no margins) — consistent with the confirmed
    //     "tight crop" nature of BF1 slots.
    public required byte NewInkWidth { get; init; }

    // UA: Bearing і CellHeight (метрична модель, GlyphMetricModel)
    //     задаються явно й записуються, а НЕ копіюються з донора — інакше
    //     нова кирилична літера успадкувала б вертикальні метрики
    //     випадкового англійського донора, що ламає вертикальне
    //     вирівнювання гліфа (підтверджено двома експериментами — див.
    //     GlyphMetricModel.cs). Інваріант гри (перевірено на 28
    //     англійських літерах): Bearing + висота_бокса(px) = CellHeight;
    //     викликач (CyrillicFontInjector) гарантує CanvasHeight =
    //     CellHeight − Bearing, тож масштаб рендеру гри = 1 (різкість).
    // EN: Bearing and CellHeight (metric model, GlyphMetricModel) are set
    //     explicitly and written, NOT copied from the donor — otherwise a
    //     new Cyrillic letter would inherit the vertical metrics of a
    //     random English donor, breaking the glyph's vertical alignment
    //     (confirmed by two experiments — see GlyphMetricModel.cs). The
    //     game's invariant (verified on 28 English letters): Bearing +
    //     box_height(px) = CellHeight; the caller (CyrillicFontInjector)
    //     guarantees CanvasHeight = CellHeight − Bearing, so the game's
    //     render scale = 1 (crisp).
    public required byte NewBearing { get; init; }
    public required byte NewCellHeight { get; init; }

    // UA: Лише для повідомлень про помилки (напр. "gamefont_medium code=0x41") —
    //     не впливає на саму логіку запису.
    // EN: For error messages only (e.g. "gamefont_medium code=0x41") — does
    //     not affect the write logic itself.
    public string? DebugLabel { get; init; }
}

public static class GlyphAtlasPatcher
{
    // UA: Приймає БУДЬ-ЯКУ кількість патчів ОДНОГО ШРИФТУ (виклик для
    //     кількох шрифтів — окремо на кожен, чи об'єднання словників
    //     після) і повертає ГОТОВИЙ Dictionary для
    //     UcfbWriter.WriteFile(root, replacements) — і BODY, і FBOD.
    //
    //     fbodChunk/originalRecords — FBOD-чанк і його ПОВНИЙ (уже
    //     розпарсений) список записів ЦЬОГО шрифту. Для кожного патчу
    //     запис із таким самим Code, що й patch.DonorRecord.Code,
    //     замінюється на оновлений (нові U0-V1/XAdvance/InkWidth,
    //     PageIndex/Bearing/CellHeight/ReservedByte4 — копія з
    //     DonorRecord). Записи, яких не торкається жоден патч,
    //     переносяться в незмінному вигляді.
    //
    //     Патчі на ОДНІЙ і тій самій сторінці (однаковий
    //     BodyChunk.FileDataOffset) групуються й накладаються послідовно
    //     на ОДНУ спільну копію оригінального BODY — оригінальний масив
    //     bodyChunk.RawData НІКОЛИ не мутується напряму (Clone()), кожен
    //     виклик повертає нові байти.
    // EN: Accepts ANY number of patches for ONE FONT (call separately per
    //     font for several fonts, or merge the resulting dictionaries
    //     afterward) and returns a READY Dictionary for
    //     UcfbWriter.WriteFile(root, replacements) — both BODY and FBOD.
    //
    //     fbodChunk/originalRecords — the FBOD chunk and its FULL
    //     (already parsed) record list for THIS font. For each patch, the
    //     record with the same Code as patch.DonorRecord.Code is replaced
    //     with an updated one (new U0-V1/XAdvance/InkWidth,
    //     PageIndex/Bearing/CellHeight/ReservedByte4 — copied from
    //     DonorRecord). Records untouched by any patch are carried over
    //     unchanged.
    //
    //     Patches on the SAME page (identical BodyChunk.FileDataOffset)
    //     are grouped and applied sequentially onto ONE shared copy of
    //     the original BODY — the original bodyChunk.RawData array is
    //     NEVER mutated directly (Clone()), each call returns fresh bytes.
    public static Dictionary<long, byte[]> BuildReplacements(
        IEnumerable<GlyphPatch> patches,
        UcfbChunk fbodChunk,
        IReadOnlyList<FontGlyphRecord> originalRecords)
    {
        var patchList = patches.ToList();
        var replacements = new Dictionary<long, byte[]>();

        foreach (var pageGroup in patchList.GroupBy(p => p.BodyChunk.FileDataOffset))
        {
            var bodyChunk = pageGroup.First().BodyChunk;
            var patchedBytes = (byte[])bodyChunk.RawData.Clone();

            foreach (var patch in pageGroup)
                ApplyPixelPatch(patchedBytes, patch);

            replacements[bodyChunk.FileDataOffset] = patchedBytes;
        }

        // UA: FBOD — ОДИН чанк на весь шрифт, тож усі патчі цього виклику
        //     (усіх сторінок) застосовуються до ОДНОГО спільного списку
        //     записів, і серіалізуються РАЗОМ в кінці.
        // EN: FBOD — ONE chunk for the whole font, so all patches in this
        //     call (across all pages) are applied to ONE shared record
        //     list, and serialized TOGETHER at the end.
        var updatedRecords = originalRecords.ToList();
        var recordIndexByCode = new Dictionary<ushort, int>();
        for (var i = 0; i < updatedRecords.Count; i++)
            recordIndexByCode[updatedRecords[i].Code] = i;

        foreach (var patch in patchList)
        {
            var label = patch.DebugLabel ?? $"code=0x{patch.DonorRecord.Code:X2}";

            if (!recordIndexByCode.TryGetValue(patch.DonorRecord.Code, out var recordIndex))
                throw new ArgumentException(
                    $"UA: {label}: код 0x{patch.DonorRecord.Code:X2} відсутній у originalRecords цього FBOD. / " +
                    $"EN: {label}: code 0x{patch.DonorRecord.Code:X2} is not present in this FBOD's originalRecords.");

            var donor = patch.DonorRecord;

            // UA: Той самий напрямок осей U/V, що й в оригінальному
            //     записі — не припускаємо U0<U1/V0<V1 наосліп (формат
            //     цього НІКОЛИ не гарантував, попередні перевірки завжди
            //     явно робили Math.Min/Max при читанні саме тому).
            // EN: Same U/V axis direction as the original record — never
            //     blindly assume U0<U1/V0<V1 (the format never guaranteed
            //     this, which is exactly why earlier checks always did an
            //     explicit Math.Min/Max on read).
            var uIncreasing = donor.U1 >= donor.U0;
            var vIncreasing = donor.V1 >= donor.V0;

            var newU0Pixel = patch.RectMinX;
            var newU1Pixel = patch.RectMinX + patch.CanvasWidth;
            var newV0Pixel = patch.RectMinY;
            var newV1Pixel = patch.RectMinY + patch.CanvasHeight;

            var newU0 = (uIncreasing ? newU0Pixel : newU1Pixel) / (float)patch.TextureWidth;
            var newU1 = (uIncreasing ? newU1Pixel : newU0Pixel) / (float)patch.TextureWidth;
            var newV0 = (vIncreasing ? newV0Pixel : newV1Pixel) / (float)patch.TextureHeight;
            var newV1 = (vIncreasing ? newV1Pixel : newV0Pixel) / (float)patch.TextureHeight;

            updatedRecords[recordIndex] = donor with
            {
                U0 = newU0,
                U1 = newU1,
                V0 = newV0,
                V1 = newV1,
                XAdvance = patch.NewXAdvance,
                InkWidth = patch.NewInkWidth,
                // UA: Bearing/CellHeight (метрична модель) задаються явно з
                //     патча, а НЕ копіюються з донора (див. коментар біля
                //     GlyphPatch.NewBearing/NewCellHeight та GlyphMetricModel).
                // EN: Bearing/CellHeight (metric model) are set explicitly
                //     from the patch, NOT copied from the donor (see the
                //     comment at GlyphPatch.NewBearing/NewCellHeight and
                //     GlyphMetricModel).
                Bearing = patch.NewBearing,
                CellHeight = patch.NewCellHeight,
                // UA: PageIndex/ReservedByte4/Code/Index — усе решта НЕ
                //     вказано тут, тож `with` копіює їх з donor без змін.
                // EN: PageIndex/ReservedByte4/Code/Index — everything else
                //     is NOT specified here, so `with` copies them from
                //     donor unchanged.
            };
        }

        replacements[fbodChunk.FileDataOffset] = FontGlyphTable.Serialize(updatedRecords);

        return replacements;
    }

    private static void ApplyPixelPatch(byte[] pageBytes, GlyphPatch patch)
    {
        var label = patch.DebugLabel ?? $"[{patch.RectMinX},{patch.RectMinY}]";

        // UA: Захисні перевірки — ПОВТОРЮЮТЬ логіку вже пройдених
        //     UvRectBoundsCheckCommand/RasterizerCanvasSizeCheckCommand,
        //     але тут вони не діагностика "про всяк випадок": якщо
        //     виклик коду поза межами (напр. хтось підставить чужий
        //     TextureWidth), ЦЕЙ метод фізично пише байти в масив — тому
        //     перевірка обов'язкова саме тут, а не лише "десь раніше
        //     в діагностиці".
        // EN: Defensive checks — they REPEAT the logic already covered by
        //     UvRectBoundsCheckCommand/RasterizerCanvasSizeCheckCommand,
        //     but here they aren't a "just in case" diagnostic: if the
        //     calling code is out of bounds (e.g. someone passes a
        //     mismatched TextureWidth), THIS method physically writes
        //     bytes into the array — so the check is mandatory right
        //     here, not just "somewhere earlier in diagnostics".
        if (patch.CanvasWidth <= 0 || patch.CanvasHeight <= 0)
            throw new ArgumentException(
                $"UA: {label}: CanvasWidth/CanvasHeight мають бути > 0 (отримано {patch.CanvasWidth}x{patch.CanvasHeight}) / " +
                $"EN: {label}: CanvasWidth/CanvasHeight must be > 0 (got {patch.CanvasWidth}x{patch.CanvasHeight})");

        var expectedPixelBytesLength = patch.CanvasWidth * patch.CanvasHeight * 2;
        if (patch.PixelBytesA4R4G4B4.Length != expectedPixelBytesLength)
            throw new ArgumentException(
                $"UA: {label}: PixelBytesA4R4G4B4 має довжину {patch.PixelBytesA4R4G4B4.Length}, очікується {expectedPixelBytesLength} (CanvasWidth*CanvasHeight*2) / " +
                $"EN: {label}: PixelBytesA4R4G4B4 length is {patch.PixelBytesA4R4G4B4.Length}, expected {expectedPixelBytesLength} (CanvasWidth*CanvasHeight*2)");

        if (patch.RectMinX < 0 || patch.RectMinY < 0 ||
            patch.RectMinX + patch.CanvasWidth > patch.TextureWidth ||
            patch.RectMinY + patch.CanvasHeight > patch.TextureHeight)
            throw new ArgumentException(
                $"UA: {label}: прямокутник [{patch.RectMinX}..{patch.RectMinX + patch.CanvasWidth}) x " +
                $"[{patch.RectMinY}..{patch.RectMinY + patch.CanvasHeight}) виходить за межі текстури " +
                $"{patch.TextureWidth}x{patch.TextureHeight} / " +
                $"EN: {label}: rectangle [{patch.RectMinX}..{patch.RectMinX + patch.CanvasWidth}) x " +
                $"[{patch.RectMinY}..{patch.RectMinY + patch.CanvasHeight}) exceeds texture bounds " +
                $"{patch.TextureWidth}x{patch.TextureHeight}");

        var expectedPageBytesLength = patch.TextureWidth * patch.TextureHeight * 2;
        if (pageBytes.Length != expectedPageBytesLength)
            throw new ArgumentException(
                $"UA: {label}: розмір масиву сторінки {pageBytes.Length} не дорівнює TextureWidth*TextureHeight*2={expectedPageBytesLength} " +
                $"— підозра на невідповідний TextureWidth/TextureHeight у цьому патчі / " +
                $"EN: {label}: page array size {pageBytes.Length} does not equal TextureWidth*TextureHeight*2={expectedPageBytesLength} " +
                $"— suspect a mismatched TextureWidth/TextureHeight in this patch");

        // UA: Копіюємо рядок за рядком — кожен рядок сторінки має
        //     TextureWidth*2 байт, кожен рядок гліфа — CanvasWidth*2 байт.
        // EN: Copy row by row — each page row is TextureWidth*2 bytes,
        //     each glyph row is CanvasWidth*2 bytes.
        for (var row = 0; row < patch.CanvasHeight; row++)
        {
            var destOffset = ((patch.RectMinY + row) * patch.TextureWidth + patch.RectMinX) * 2;
            var srcOffset = row * patch.CanvasWidth * 2;
            Array.Copy(patch.PixelBytesA4R4G4B4, srcOffset, pageBytes, destOffset, patch.CanvasWidth * 2);
        }
    }
}
