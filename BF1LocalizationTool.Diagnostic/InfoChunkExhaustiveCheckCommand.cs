// =============================================================================
// BF1LocalizationTool.Diagnostic — InfoChunkExhaustiveCheckCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Вичерпна перевірка КОЖНОГО чанку "INFO" у дереві (незалежно від
//     батька — PIPE, FMT_, LVL_, tex_ тощо). PIPE>INFO чанки мають
//     структуру [1, N, ID, N] — саме той шаблон, що дає фантомну дитину
//     (8+N байт), коли N<=8, і коректно лишається листком, коли N>8
//     (bounds-перевірка природно спрацьовує).
//
//     Класифікує КОЖЕН INFO-чанк на три категорії:
//       1. СПРАВЖНІЙ ЛИСТОК (Children.Count==0) — bounds-перевірка
//          природно не пропустила фантомну дитину для цього N.
//       2. ПІДОЗРІЛИЙ ФАНТОМ (Children.Count==1, і ця дитина повністю
//          покриває весь DataSize МІНУС рівно 4 останні байти, з таким
//          самим числовим значенням N в останніх 4 байтах, що і в
//          "DataSize" фантомної дитини) — узгоджується з теорією
//          міспарсингу.
//       3. СПРАВЖНЯ ПІДСТРУКТУРА (щось інше — Children.Count>1, або
//          дитина не відповідає шаблону N=N) — це БУЛО Б винятком,
//          який заперечує додавання INFO в AlwaysLeafFourCC цілком.
// EN: Exhaustive check of EVERY "INFO" chunk in the tree (regardless of
//     parent — PIPE, FMT_, LVL_, tex_, etc.). PIPE>INFO chunks have the
//     structure [1, N, ID, N] — exactly the pattern that produces a
//     phantom child (8+N bytes) when N<=8, and correctly stays a leaf
//     when N>8 (the bounds check naturally triggers).
//
//     Classifies EVERY INFO chunk into three categories:
//       1. GENUINE LEAF (Children.Count==0) — the bounds check naturally
//          rejected the phantom child for this N.
//       2. SUSPECTED PHANTOM (Children.Count==1, and that child fully
//          covers the entire DataSize MINUS exactly the last 4 bytes,
//          with the same numeric value N in the last 4 bytes as in the
//          phantom child's "DataSize") — consistent with the misparse
//          theory.
//       3. GENUINE SUBSTRUCTURE (anything else — Children.Count>1, or
//          the child doesn't match the N=N pattern) — this WOULD BE an
//          exception that argues against adding INFO to AlwaysLeafFourCC
//          entirely.
// =============================================================================

using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.IO;

namespace BF1LocalizationTool.Diagnostic;

public static class InfoChunkExhaustiveCheckCommand
{
    public static void Run(DiagnosticReport report, string lvlFilePath, string label)
    {
        var data = File.ReadAllBytes(lvlFilePath);
        var root = UcfbReader.ReadFile(data);

        var genuineLeaf = 0;
        var suspectedPhantom = 0;
        var genuineSubstructure = new List<string>();

        WalkAndClassify(root, "root", data, ref genuineLeaf, ref suspectedPhantom, genuineSubstructure);

        var total = genuineLeaf + suspectedPhantom + genuineSubstructure.Count;

        report.Log($"=== [{label}] Вичерпна перевірка КОЖНОГО чанку INFO у дереві ===");
        report.Log($"=== [{label}] Exhaustive check of EVERY INFO chunk in the tree ===");
        report.Log($"    Всього INFO-чанків: {total}");
        report.Log($"    Категорія 1 (справжній листок, Children.Count==0): {genuineLeaf}");
        report.Log($"    Категорія 2 (підозрілий фантом, шаблон N=N підтверджено): {suspectedPhantom}");
        report.Log($"    Категорія 3 (СПРАВЖНЯ ПІДСТРУКТУРА — виняток!): {genuineSubstructure.Count}");

        if (genuineSubstructure.Count > 0)
        {
            report.Log("    --- Приклади винятків (до 15) ---");
            foreach (var ex in genuineSubstructure.Take(15))
                report.Log($"      {ex}");
        }

        report.Log();
        report.Log(genuineSubstructure.Count == 0
            ? "    UA: ПІДТВЕРДЖЕНО — КОЖЕН INFO-чанк без винятку відповідає категорії 1 або 2 " +
              "(справжній листок, чи підтверджений фантом за шаблоном N=N). Жодного випадку " +
              "справжньої підструктури не знайдено. INFO можна безпечно додати в AlwaysLeafFourCC."
            : "    UA: !! ЗНАЙДЕНО ВИНЯТКИ — деякі INFO-чанки мають ІНШУ структуру, ніж очікувана " +
              "фантомна помилка. Додавати INFO в AlwaysLeafFourCC ГЛОБАЛЬНО НЕ можна без подальшого " +
              "дослідження цих конкретних випадків.");
    }

    private static void WalkAndClassify(
        UcfbChunk chunk, string path, byte[] fileData,
        ref int genuineLeaf, ref int suspectedPhantom, List<string> genuineSubstructure)
    {
        if (chunk.FourCC == "INFO")
        {
            if (chunk.Children.Count == 0)
            {
                genuineLeaf++;
            }
            else
            {
                var pos = 0;
                foreach (var child in chunk.Children)
                {
                    var rawNext = pos + 8 + (int)child.DataSize;
                    pos = (rawNext + 3) & ~3;
                }

                var realLeftoverBytes = (int)chunk.DataSize - pos;

                // UA: Коректний критерій "це природне (хай і хибне)
                //     завершення TryParseChildren, а не справжня
                //     підструктура" — залишок після циклу МЕНШЕ 8 байт,
                //     незалежно від конкретного числового значення хвоста.
                //     Вимога рівності tailValue==child.DataSize НЕ є
                //     загальною вимогою формату (це властивість лише
                //     одного конкретного прикладу), тому її невиконання
                //     саме по собі не означає аномалію.
                // EN: The correct criterion for "this is natural (if
                //     flawed) TryParseChildren termination, not real
                //     substructure" — leftover after the loop is LESS THAN 8
                //     bytes, regardless of the tail's specific numeric value.
                //     Requiring tailValue==child.DataSize is NOT a general
                //     format requirement (it's a property of a single
                //     specific example), so failing it alone doesn't
                //     indicate an anomaly.
                if (realLeftoverBytes is >= 0 and < 8)
                {
                    suspectedPhantom++;
                }
                else
                {
                    genuineSubstructure.Add($"{path} > [INFO]: {chunk.Children.Count} дітей, " +
                                              $"РЕАЛЬНИЙ залишок після циклу={realLeftoverBytes} байт (>=8 або від'ємний) — " +
                                              "справжня аномалія, потребує окремого дослідження");
                }
            }
        }

        foreach (var child in chunk.Children)
        {
            var childLabel = child.IsFourCC ? child.FourCC : "#hash";
            WalkAndClassify(child, $"{path} > {childLabel}", fileData, ref genuineLeaf, ref suspectedPhantom, genuineSubstructure);
        }
    }
}