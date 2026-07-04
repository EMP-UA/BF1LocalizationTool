// =============================================================================
// BF1LocalizationTool.Core — IO/UcfbWriter.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Записує дерево UcfbChunk назад у бінарний .lvl файл.
//
//     Стратегія запису:
//       1. Якщо чанк має Children — рекурсивно серіалізує дочірні чанки,
//          розраховує новий DataSize.
//       2. Якщо чанк не має Children — записує RawData як є.
//          Виняток: якщо для чанку є замінні дані (replacements),
//          записує їх замість RawData — розмір оновлюється автоматично.
//
//     Це дозволяє змінювати вміст локалізаційних чанків без повного
//     перерозбору всієї структури файлу.
//
// EN: Writes a UcfbChunk tree back to a binary .lvl file.
//
//     Write strategy:
//       1. If chunk has Children — recursively serialize child chunks,
//          calculate new DataSize.
//       2. If chunk has no Children — write RawData as-is.
//          Exception: if replacement data exists for the chunk,
//          write it instead of RawData — size is updated automatically.
//
//     This allows modifying localization chunk content without fully
//     re-parsing the entire file structure.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;

namespace BF1LocalizationTool.Core.IO;

public static class UcfbWriter
{
    // UA: Замінні дані для конкретних чанків (FileDataOffset → нові байти)
    //     Використовується щоб підмінити вміст DATA-чанку локалізації
    // EN: Replacement data for specific chunks (FileDataOffset → new bytes)
    //     Used to replace localization DATA chunk content
    public static byte[] WriteFile(UcfbChunk root,
        Dictionary<long, byte[]>? replacements = null)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        WriteChunk(writer, root, replacements);

        return ms.ToArray();
    }

    // -------------------------------------------------------------------------
    // UA: Записує файл на диск. Спочатку серіалізує в пам'ять,
    //     потім атомарно записує (через тимчасовий файл).
    // EN: Writes file to disk. First serializes to memory,
    //     then atomically writes (via temp file).
    // -------------------------------------------------------------------------
    public static void WriteFile(string filePath, UcfbChunk root,
        Dictionary<long, byte[]>? replacements = null)
    {
        var data = WriteFile(root, replacements);

        // UA: Атомарний запис: спочатку в тимчасовий файл, потім переміщуємо
        // EN: Atomic write: first to temp file, then move
        var tempPath = filePath + ".tmp";
        File.WriteAllBytes(tempPath, data);
        File.Move(tempPath, filePath, overwrite: true);
    }

    // -------------------------------------------------------------------------
    // UA: Рекурсивно серіалізує один чанк у BinaryWriter
    // EN: Recursively serializes a single chunk to BinaryWriter
    // -------------------------------------------------------------------------
    private static void WriteChunk(BinaryWriter writer, UcfbChunk chunk,
        Dictionary<long, byte[]>? replacements)
    {
        writer.Write(chunk.Id);

        byte[] payloadData;

        if (chunk.HasChildren)
        {
            // UA: Є дочірні чанки — серіалізуємо їх у тимчасовий буфер.
            //     КРИТИЧНО: після кожного дочірнього чанку додаємо вирівнювальні
            //     байти до 4 байт — точно як в оригінальному ucfb форматі.
            //     Поле DataSize в заголовку чанку НЕ включає padding (зберігає
            //     лише реальний розмір даних), але наступний чанк фізично
            //     починається на вирівняній позиції. Без цього повторне
            //     відкриття збереженого файлу не знайде BODY після NAME
            //     з нечітким розміром (7, 11 байт тощо).
            // EN: Has child chunks — serialize them to temp buffer.
            //     CRITICAL: after each child chunk add alignment padding bytes
            //     to 4 bytes — exactly as in original ucfb format.
            //     DataSize field in chunk header does NOT include padding (stores
            //     only actual data size), but the next chunk physically starts
            //     at an aligned position. Without this, re-opening a saved file
            //     fails to find BODY after NAME with odd size (7, 11 bytes etc.)
            using var childBuffer = new MemoryStream();
            using var childWriter = new BinaryWriter(childBuffer);

            foreach (var child in chunk.Children)
            {
                var beforePos = childBuffer.Position;
                WriteChunk(childWriter, child, replacements);
                var afterPos = childBuffer.Position;

                // UA: Padding щоб наступний чанк починався на кратній 4 позиції
                // EN: Padding so the next chunk starts at a 4-byte aligned position
                var written  = (int)(afterPos - beforePos);
                var padding  = (4 - (written % 4)) % 4;
                for (var i = 0; i < padding; i++)
                    childWriter.Write((byte)0);
            }

            payloadData = childBuffer.ToArray();
        }
        else if (replacements is not null &&
                 replacements.TryGetValue(chunk.FileDataOffset, out var replacement))
        {
            payloadData = replacement;
        }
        else
        {
            payloadData = chunk.RawData;
        }

        // UA: Записуємо розмір даних (БЕЗ padding) і самі дані
        // EN: Write data size (WITHOUT padding) and the data itself
        writer.Write((uint)payloadData.Length);
        writer.Write(payloadData);

        // UA: КРИТИЧНО: вирівнювання до 4 байт після кожного чанку.
        //     DataSize НЕ включає padding, але наступний чанк починається
        //     на вирівняній позиції — точно як в оригінальному ucfb форматі.
        // EN: CRITICAL: 4-byte alignment after each chunk.
        //     DataSize does NOT include padding, but next chunk starts
        //     at an aligned position — exactly as in original ucfb format.
        var padCount = (4 - (payloadData.Length % 4)) % 4;
        for (var i = 0; i < padCount; i++)
            writer.Write((byte)0);
    }
}
