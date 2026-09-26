// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateD3D9FixBuildCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ГЕНЕРАТОР (production, входить у фінальний патч) / GENERATOR (production, part of the final patch)
// =============================================================================
// UA: Збирає d3d9.dll для гри напряму з джерела, вбудованого в цю програму
//     (той самий Bundled cpp/def, який звіряє GenerateD3D9FixProvenanceReportCommand),
//     тією самою командою Zig, з тим самим -o іменем виводу ("zig_d3d9.dll"),
//     що й перевірка походження, — саме тому результат збігається байт-у-байт
//     із MovieSubtitleD3D9FixProvenance.RecordedDllSha256: ім'я, передане
//     лінкеру через -o, вбудовується в PE-файл, тож інша -o-назва дає інші
//     байти навіть з тим самим кодом і компілятором. На відміну від
//     перевірки походження, цей клас не призначений для доказу — він лише
//     кладе готовий файл під кінцевим іменем "d3d9.dll" у задану теку виводу
//     (перейменування копіюванням, без зміни вмісту файлу).
//
//     Сама компіляція відбувається в окремій тимчасовій теці поруч із .exe
//     (AppContext.BaseDirectory\_final-assembly-d3d9-build-tmp), а не прямо
//     в теці виводу outputDir. Причина: лінкер лишає поруч із результатом
//     побічні файли збірки — зокрема d3d9_proxy.lib, імпорт-бібліотеку, яку
//     лінкер генерує з LIBRARY-секції d3d9_proxy.def (LIBRARY d3d9) під час
//     компіляції DLL з .def-файлом. Такі побічні файли не мають потрапляти
//     у GameData-теку, куди вміст final-assembly-output копіюється напряму
//     поверх інсталяції гри. У outputDir копіюється лише готовий d3d9.dll,
//     після чого вся тимчасова тека видаляється — це прибирає всі побічні
//     файли лінкера незалежно від їхньої точної назви. Тимчасова тека — як і
//     всі шляхи в цьому класі — обчислюється відносно AppContext.BaseDirectory,
//     без жодних захардкоджених абсолютних шляхів.
// EN: Builds the game's d3d9.dll directly from the source embedded in this
//     application (the same bundled cpp/def that GenerateD3D9FixProvenanceReportCommand
//     compares against), with the same Zig command and the same -o output
//     name ("zig_d3d9.dll") as the provenance check — which is exactly why
//     the result matches MovieSubtitleD3D9FixProvenance.RecordedDllSha256
//     byte-for-byte: the name passed to the linker via -o is embedded in the
//     PE file, so a different -o name yields different bytes even from
//     identical source and compiler. Unlike the provenance check, this class
//     is not meant to prove anything — it only places the finished file
//     under the final name "d3d9.dll" in the given output folder (a rename
//     by copying, which does not change the file's contents).
//
//     The compilation itself happens in a separate temporary folder next to
//     the .exe (AppContext.BaseDirectory\_final-assembly-d3d9-build-tmp),
//     not directly in the output folder outputDir. Reason: the linker leaves
//     build side-artifacts next to its result — notably d3d9_proxy.lib, the
//     import library the linker generates from the LIBRARY section of
//     d3d9_proxy.def (LIBRARY d3d9) when compiling a DLL with a .def file.
//     Such side-artifacts must not end up in the GameData folder, where
//     final-assembly-output's contents are copied straight over the game
//     installation. Only the finished d3d9.dll is copied into outputDir,
//     after which the whole temporary folder is deleted — this removes every
//     linker side-artifact regardless of its exact name. The temporary
//     folder — like every path in this class — is resolved relative to
//     AppContext.BaseDirectory, with no hardcoded absolute paths.
// =============================================================================

using BF1LocalizationTool.Core.Bf2Widescreen;

namespace BF1LocalizationTool.Diagnostic;

public static class GenerateD3D9FixBuildCommand
{
    public static async Task<string?> RunAsync(DiagnosticReport report, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var workDir = Path.Combine(AppContext.BaseDirectory, "_final-assembly-d3d9-build-tmp");
        if (Directory.Exists(workDir))
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* не критично */ }
        }
        Directory.CreateDirectory(workDir);

        var cppBytes = MovieSubtitleD3D9FixProvenance.GetBundledCppSourceBytes();
        var defBytes = MovieSubtitleD3D9FixProvenance.GetBundledDefSourceBytes();
        var cppPath = Path.Combine(workDir, "d3d9_proxy.cpp");
        var defPath = Path.Combine(workDir, "d3d9_proxy.def");
        File.WriteAllBytes(cppPath, cppBytes);
        File.WriteAllBytes(defPath, defBytes);

        report.Log("UA: Джерело видобуто з вбудованих ресурсів цієї програми (не з диска, не з інтернету).");
        report.Log("EN: Source extracted from this application's own embedded resources (not from disk, not from the internet).");

        var ensure = await MovieSubtitleD3D9FixZigBuilder.EnsureZigAvailableAsync(msg => report.Log(msg));
        report.Log(ensure.WasDownloadedThisRun
            ? "UA: Zig завантажено й перевірено щойно (SHA-256 офіційного архіву) — надалі використовуватиметься з кешу."
            : "UA: Використано раніше закешований Zig — повторного завантаження не було.");
        report.Log(ensure.WasDownloadedThisRun
            ? "EN: Zig was just downloaded and verified (official archive SHA-256) — future runs will use the cache."
            : "EN: Used a previously cached Zig — no re-download happened.");

        var compileResult = await MovieSubtitleD3D9FixZigBuilder.CompileAsync(
            ensure.ZigExePath, workDir, "d3d9_proxy.cpp", "d3d9_proxy.def", "zig_d3d9.dll",
            msg => report.Log(msg));

        if (!compileResult.Success || compileResult.OutputPath is null)
        {
            report.Log("UA: Збірка Zig-ом не вдалась.");
            report.Log("EN: The Zig build failed.");
            if (!string.IsNullOrWhiteSpace(compileResult.StdErr))
                report.Log(compileResult.StdErr.Trim());
            try { Directory.Delete(workDir, recursive: true); } catch { }
            return null;
        }

        var bytes = File.ReadAllBytes(compileResult.OutputPath);
        var hash = MovieSubtitleD3D9FixProvenance.ComputeSha256Hex(bytes);
        var matchesRecorded = string.Equals(
            hash, MovieSubtitleD3D9FixProvenance.RecordedDllSha256, StringComparison.OrdinalIgnoreCase);

        report.Log($"UA: Зібрано: {compileResult.OutputPath} (SHA-256: {hash})");
        report.Log($"EN: Built: {compileResult.OutputPath} (SHA-256: {hash})");
        report.Log($"UA: Збіг із MovieSubtitleD3D9FixProvenance.RecordedDllSha256: {(matchesRecorded ? "так" : "ні — цей білд відрізняється від записаної константи")}");
        report.Log($"EN: Match with MovieSubtitleD3D9FixProvenance.RecordedDllSha256: {(matchesRecorded ? "yes" : "no — this build differs from the recorded constant")}");
        if (!matchesRecorded)
        {
            report.Log("UA: Цей крок збирає з -o zig_d3d9.dll — тим самим рецептом, що й перевірка походження, з якої записано RecordedDllSha256, — тож розбіжність тут означає реальну зміну: або .cpp/.def джерела, або версії/кешу Zig.");
            report.Log("EN: This step builds with -o zig_d3d9.dll — the same recipe as the provenance check that RecordedDllSha256 was recorded from — so a mismatch here means a real change: either the .cpp/.def source, or the Zig version/cache.");
        }

        var finalPath = Path.Combine(outputDir, "d3d9.dll");
        File.Copy(compileResult.OutputPath, finalPath, overwrite: true);

        try { Directory.Delete(workDir, recursive: true); } catch { }

        return finalPath;
    }
}
