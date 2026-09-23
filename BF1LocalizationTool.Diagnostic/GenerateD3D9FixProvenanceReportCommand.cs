// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateD3D9FixProvenanceReportCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: Пункт меню (BF1LocalizationTool.Diagnostic → "Відео та субтитри" →
//     "★★ ПОХОДЖЕННЯ d3d9.dll") виконує три дії й друкує їхні сирі
//     результати:
//       - видобуває з ВЛАСНИХ ВБУДОВАНИХ РЕСУРСІВ цієї збірки програми і
//         скомпільований d3d9.dll, і його джерело (.cpp/.def) —
//         MovieSubtitleD3D9FixProvenance.cs, той самий проєкт Core;
//       - рахує SHA-256 усіх трьох файлів НАЖИВО й звіряє з константою
//         RecordedDllSha256 у тому ж класі;
//       - завантажує (з перевіркою SHA-256, кешує) портативний
//         компілятор Zig з ziglang.org і НИМ перезбирає щойно видобутий
//         .cpp/.def тим самим рецептом (MovieSubtitleD3D9FixZigBuilder.cs,
//         Core) і звіряє SHA-256 результату з вбудованим файлом — Zig є
//         canonical тулчейном вбудованого файла (RecordedDllSha256
//         записано саме з Zig-збірки), тож цей крок і є сама перевірка
//         відтворюваності.
//     Якщо мережі немає — друкує це як факт, а не мовчить.
//
//     Програма тут виконує ДІЮ й друкує сирі факти (хеші, збіг/незбіг,
//     розмір, наявність рядка експорту) — вона НЕ формулює висновків типу
//     "доведено"/"підтверджено"/"найсильніший доказ". Інтерпретація цих
//     фактів (що саме вони означають для походження фіксу й чому)
//     лишається за людиною, яка їх переглядає.
// EN: The menu item (BF1LocalizationTool.Diagnostic → "Movies &
//     subtitles" → "★★ PROVENANCE of d3d9.dll") performs three actions and
//     prints their raw results:
//       - extracts, from THIS BUILD's OWN EMBEDDED RESOURCES, both the
//         compiled d3d9.dll and its source (.cpp/.def) —
//         MovieSubtitleD3D9FixProvenance.cs, same Core project;
//       - hashes all three files LIVE with SHA-256 and compares against
//         the RecordedDllSha256 constant in that same class;
//       - downloads (SHA-256-verified, cached) the portable Zig compiler
//         from ziglang.org and uses it to rebuild the just-extracted
//         .cpp/.def (MovieSubtitleD3D9FixZigBuilder.cs, Core) and compares
//         the result's SHA-256 against the bundled file — Zig is the
//         canonical toolchain of the bundled file (RecordedDllSha256 was
//         recorded from the Zig build itself), so this step IS the
//         reproducibility check.
//     If no network is available, it prints that as a fact rather than
//     staying silent.
//
//     The program here performs an ACTION and prints raw facts (hashes,
//     match/mismatch, size, export-string presence) — it does NOT state
//     conclusions like "proven"/"confirmed"/"strongest proof". Interpreting
//     those facts (what they mean for the fix's provenance, and why) is
//     left to whoever reviews them.
// =============================================================================

using System.Diagnostics;
using BF1LocalizationTool.Core.Bf2Widescreen;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateD3D9FixProvenanceReportCommand
{
    public static async Task RunAsync(DiagnosticReport report, string outputDir)
    {
        report.Log("UA: === Походження d3d9.dll: видобування, хеші, дві незалежні перезбірки ===");
        report.Log("EN: === Provenance of d3d9.dll: extraction, hashes, two independent rebuilds ===");
        report.Log();

        Directory.CreateDirectory(outputDir);

        var dllBytes = MovieSubtitleD3D9FixProvenance.GetBundledDllBytes();
        var cppBytes = MovieSubtitleD3D9FixProvenance.GetBundledCppSourceBytes();
        var defBytes = MovieSubtitleD3D9FixProvenance.GetBundledDefSourceBytes();

        var dllHash = MovieSubtitleD3D9FixProvenance.ComputeSha256Hex(dllBytes);
        var cppHash = MovieSubtitleD3D9FixProvenance.ComputeSha256Hex(cppBytes);
        var defHash = MovieSubtitleD3D9FixProvenance.ComputeSha256Hex(defBytes);

        var dllPath = Path.Combine(outputDir, "MovieSubtitleD3D9Fix.dll");
        var cppPath = Path.Combine(outputDir, "d3d9_proxy.cpp");
        var defPath = Path.Combine(outputDir, "d3d9_proxy.def");
        File.WriteAllBytes(dllPath, dllBytes);
        File.WriteAllBytes(cppPath, cppBytes);
        File.WriteAllBytes(defPath, defBytes);

        report.Log("UA: --- Видобуто з вбудованих ресурсів цієї програми (не з інтернету, не з окремого файла на диску) у: ---");
        report.Log($"UA:    {outputDir}");
        report.Log("EN: --- Extracted from this program's own embedded resources (not from the internet, not from a separate file on disk) into: ---");
        report.Log($"EN:    {outputDir}");
        report.Log($"UA:    - {dllPath}  (скомпільований фікс, той самий, що .iss-інсталятор кладе в GameData\\d3d9.dll)");
        report.Log($"UA:    - {cppPath} / {defPath}  (вихідний код, з якого його зібрано)");
        report.Log($"EN:    - {dllPath}  (the compiled fix, the same one the .iss installer places into GameData\\d3d9.dll)");
        report.Log($"EN:    - {cppPath} / {defPath}  (the source code it was built from)");
        report.Log();

        report.Log("UA: --- SHA-256, пораховані наживо з файлів, що зараз лежать у цій збірці програми ---");
        report.Log("EN: --- SHA-256, computed live from the files present in this program build right now ---");
        report.Log($"    MovieSubtitleD3D9Fix.dll : {dllHash}");
        report.Log($"    d3d9_proxy.cpp           : {cppHash}");
        report.Log($"    d3d9_proxy.def           : {defHash}");
        report.Log();

        var matchesRecorded = string.Equals(
            dllHash, MovieSubtitleD3D9FixProvenance.RecordedDllSha256, StringComparison.OrdinalIgnoreCase);
        report.Log("UA: --- Звірка з MovieSubtitleD3D9FixProvenance.RecordedDllSha256 ---");
        report.Log("EN: --- Comparison against MovieSubtitleD3D9FixProvenance.RecordedDllSha256 ---");
        report.Log($"UA:    Записаний хеш: {MovieSubtitleD3D9FixProvenance.RecordedDllSha256}");
        report.Log($"EN:    Recorded hash: {MovieSubtitleD3D9FixProvenance.RecordedDllSha256}");
        report.Log($"UA:    Збіг: {(matchesRecorded ? "так" : "НІ")}");
        report.Log($"EN:    Match: {(matchesRecorded ? "yes" : "NO")}");
        report.Log();

        report.Log("UA: --- Перезбірка: Zig (самозавантажується, canonical-тулчейн вбудованого файла) ---");
        report.Log("EN: --- Rebuild: Zig (self-downloading, the canonical toolchain of the bundled file) ---");
        await TryZigRebuildAsync(report, outputDir, cppPath, defPath, dllHash);
    }

    /// <summary>
    /// UA: Zig-перезбірка. Самодостатня жива перезбірка LLVM-based
    ///     компілятором, що сам завантажується з перевіркою SHA-256 (див.
    ///     MovieSubtitleD3D9FixZigBuilder.cs). Zig — canonical тулчейн
    ///     вбудованого файла (RecordedDllSha256 записано з Zig-збірки), тож
    ///     цей крок звіряє результат байт-у-байт із bundledDllHash.
    /// EN: Zig rebuild. A self-contained live rebuild with an LLVM-based
    ///     compiler that fetches itself with a SHA-256 check (see
    ///     MovieSubtitleD3D9FixZigBuilder.cs). Zig is the canonical toolchain
    ///     of the bundled file (RecordedDllSha256 was recorded from the Zig
    ///     build), so this step compares the result byte-for-byte against
    ///     bundledDllHash.
    /// </summary>
    private static async Task TryZigRebuildAsync(
        DiagnosticReport report, string workDir, string cppPath, string defPath, string bundledDllHash)
    {
        try
        {
            var ensure = await MovieSubtitleD3D9FixZigBuilder.EnsureZigAvailableAsync(
                msg => report.Log(msg));

            report.Log(ensure.WasDownloadedThisRun
                ? "UA:    Zig завантажено й перевірено щойно (див. вище) — надалі використовуватиметься з кешу."
                : "UA:    Використано раніше закешований Zig — повторного завантаження не було.");
            report.Log(ensure.WasDownloadedThisRun
                ? "EN:    Zig was just downloaded and verified (see above) — future runs will use the cache."
                : "EN:    Used a previously cached Zig — no re-download happened.");

            var compileResult = await MovieSubtitleD3D9FixZigBuilder.CompileAsync(
                ensure.ZigExePath, workDir,
                Path.GetFileName(cppPath), Path.GetFileName(defPath),
                "zig_d3d9.dll",
                msg => report.Log(msg));

            if (!compileResult.Success || compileResult.OutputPath is null)
            {
                report.Log("UA:    Компіляція Zig-ом НЕ вдалась — цей крок НЕ підтверджує фікс (дивись stderr нижче).");
                report.Log("EN:    Compilation with Zig FAILED — this step does NOT confirm the fix (see stderr below).");
                if (!string.IsNullOrWhiteSpace(compileResult.StdErr))
                    report.Log(compileResult.StdErr.Trim());
                return;
            }

            var zigBytes = File.ReadAllBytes(compileResult.OutputPath);
            var zigHash = MovieSubtitleD3D9FixProvenance.ComputeSha256Hex(zigBytes);

            // UA: Проста перевірка "чи взагалі схоже на правильний DLL" — шукаємо
            //     ASCII-рядок "Direct3DCreate9" у байтах результату (те саме ім'я,
            //     яке має бути експортоване через .def). Це НЕ повний розбір PE/
            //     таблиці експортів — навмисно проста евристика, узгоджена з
            //     іншими byte-search перевірками в цьому проєкті, а не претензія
            //     на вичерпний PE-аналіз.
            // EN: A simple "does this even look like a correct DLL" check — search
            //     for the ASCII string "Direct3DCreate9" in the result's bytes (the
            //     same name that must be exported via the .def file). This is NOT a
            //     full PE/export-table parse — deliberately a simple heuristic,
            //     consistent with other byte-search checks in this project, not a
            //     claim of exhaustive PE analysis.
            var containsExportName = ContainsAsciiMarker(zigBytes, "Direct3DCreate9");
            var identical = string.Equals(zigHash, bundledDllHash, StringComparison.OrdinalIgnoreCase);

            report.Log($"UA:    Зібрано Zig-ом: {compileResult.OutputPath}");
            report.Log($"UA:    Розмір: {zigBytes.Length} байт, SHA-256: {zigHash}");
            report.Log($"UA:    Рядок \"Direct3DCreate9\" у байтах результату: {(containsExportName ? "є" : "НЕМАЄ")}");
            report.Log($"UA:    Байт-у-байт збіг із вбудованим файлом: {(identical ? "так" : "ні")}");
            report.Log($"EN:    Built with Zig: {compileResult.OutputPath}");
            report.Log($"EN:    Size: {zigBytes.Length} bytes, SHA-256: {zigHash}");
            report.Log($"EN:    \"Direct3DCreate9\" string present in the result: {(containsExportName ? "yes" : "NO")}");
            report.Log($"EN:    Byte-for-byte match with the bundled file: {(identical ? "yes" : "no")}");
        }
        catch (InvalidOperationException ex)
        {
            // UA: Саме цей тип кидає EnsureZigAvailableAsync при розбіжності SHA-256
            //     завантаженого архіву — недовірений файл НІКОЛИ не запускається.
            // EN: This is the exact type EnsureZigAvailableAsync throws on a
            //     downloaded-archive SHA-256 mismatch — an untrusted file is NEVER run.
            report.Log($"UA:    {ex.Message}");
            report.Log("UA:    Крок пропущено (НЕ провал фіксу — це провал перевірки цілісності завантаження).");
            report.Log("EN:    Step skipped (NOT a fix failure — this is a download-integrity check failure).");
        }
        catch (Exception ex)
        {
            report.Log($"UA:    Крок Zig не вдався: {ex.Message}. Крок пропущено (НЕ провал фіксу). Можливо, немає");
            report.Log("UA:    мережі для завантаження компілятора, або антивірус заблокував виконання zig.exe.");
            report.Log($"EN:    Zig step failed: {ex.Message}. Step skipped (NOT a fix failure). Possibly no network");
            report.Log("EN:    to download the compiler, or an antivirus blocked zig.exe from running.");
        }
    }

    /// <summary>
    /// UA: Чи міститься ASCII-рядок marker десь у byte[] haystack. Простий,
    ///     навмисно "тупий" пошук підрядка — достатньо для евристики "це схоже
    ///     на правильно зібраний DLL", без претензії на розбір формату PE.
    /// EN: Whether the ASCII string marker occurs anywhere in byte[] haystack.
    ///     A simple, deliberately "dumb" substring search — good enough for the
    ///     "this looks like a correctly built DLL" heuristic, without claiming
    ///     to parse the PE format.
    /// </summary>
    private static bool ContainsAsciiMarker(byte[] haystack, string marker)
    {
        var needle = System.Text.Encoding.ASCII.GetBytes(marker);
        if (needle.Length == 0 || haystack.Length < needle.Length)
            return false;

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return true;
        }

        return false;
    }
}
