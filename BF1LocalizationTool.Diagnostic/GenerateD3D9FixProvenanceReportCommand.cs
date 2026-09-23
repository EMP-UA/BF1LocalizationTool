// =============================================================================
// BF1LocalizationTool.Diagnostic — GenerateD3D9FixProvenanceReportCommand.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: Пункт меню (BF1LocalizationTool.Diagnostic → "Відео та субтитри" →
//     "★★ ПОХОДЖЕННЯ d3d9.dll") виконує чотири дії й друкує їхні сирі
//     результати:
//       - видобуває з ВЛАСНИХ ВБУДОВАНИХ РЕСУРСІВ цієї збірки програми і
//         скомпільований d3d9.dll, і його джерело (.cpp/.def) —
//         MovieSubtitleD3D9FixProvenance.cs, той самий проєкт Core;
//       - рахує SHA-256 усіх трьох файлів НАЖИВО й звіряє з константою
//         RecordedDllSha256 у тому ж класі;
//       - завантажує (з перевіркою SHA-256, кешує) портативний
//         компілятор Zig з ziglang.org і НИМ перезбирає щойно видобутий
//         .cpp/.def тим самим рецептом (MovieSubtitleD3D9FixZigBuilder.cs,
//         Core) і звіряє SHA-256 результату з вбудованим файлом — саме
//         Zig-збірка зараз є canonical (RecordedDllSha256 записано з
//         Zig-збірки, не з MinGW-збірки);
//       - якщо на машині знайдено компілятор MinGW-w64
//         (i686-w64-mingw32-g++ або g++) — перезбирає той самий .cpp/.def
//         рецептом BuildCommand і теж звіряє SHA-256 результату з
//         вбудованим файлом; оскільки вбудований файл зараз зібраний
//         Zig-ом, цей крок звітує про розбіжність хешів — це очікуваний
//         факт (інший компілятор/рантайм дає інші байти з ідентичного
//         .cpp/.def за визначенням), а не ознака проблеми з фіксом.
//     Якщо компілятора чи мережі немає — друкує це як факт і показує
//     точний рецепт для ручного відтворення, а не мовчить.
//
//     Програма тут виконує ДІЮ й друкує сирі факти (хеші, збіг/незбіг,
//     розмір, наявність рядка експорту) — вона НЕ формулює висновків типу
//     "доведено"/"підтверджено"/"найсильніший доказ". Інтерпретація цих
//     фактів (що саме вони означають для походження фіксу й чому)
//     лишається за людиною, яка їх переглядає.
// EN: The menu item (BF1LocalizationTool.Diagnostic → "Movies &
//     subtitles" → "★★ PROVENANCE of d3d9.dll") performs four actions and
//     prints their raw results:
//       - extracts, from THIS BUILD's OWN EMBEDDED RESOURCES, both the
//         compiled d3d9.dll and its source (.cpp/.def) —
//         MovieSubtitleD3D9FixProvenance.cs, same Core project;
//       - hashes all three files LIVE with SHA-256 and compares against
//         the RecordedDllSha256 constant in that same class;
//       - downloads (SHA-256-verified, cached) the portable Zig compiler
//         from ziglang.org and uses it to rebuild the just-extracted
//         .cpp/.def (MovieSubtitleD3D9FixZigBuilder.cs, Core) and compares
//         the result's SHA-256 against the bundled file — the Zig build is
//         currently canonical (RecordedDllSha256 was recorded from the Zig
//         build, not the MinGW build);
//       - if a MinGW-w64 compiler (i686-w64-mingw32-g++ or g++) is found
//         on the machine — rebuilds the same .cpp/.def with the BuildCommand
//         recipe and also compares the result's SHA-256 against the bundled
//         file; since the bundled file is currently Zig-built, this step
//         reports a hash mismatch — an expected fact (a different
//         compiler/runtime produces different bytes from identical
//         .cpp/.def by definition), not a sign of a problem with the fix.
//     If no compiler or network is available, it prints that as a fact
//     and shows the exact recipe for manual reproduction, rather than
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

        report.Log();
        report.Log("UA: --- Перезбірка: MinGW (якщо i686-w64-mingw32-g++ або g++ є на цій машині) ---");
        report.Log("EN: --- Rebuild: MinGW (if i686-w64-mingw32-g++ or g++ is on this machine) ---");
        TryMingwRebuildAndCompare(report, outputDir, cppPath, defPath, dllHash);
    }

    private static void TryMingwRebuildAndCompare(
        DiagnosticReport report, string workDir, string cppPath, string defPath, string bundledDllHash)
    {
        var compiler = FindMingwCompiler(report);

        if (compiler is null)
        {
            report.Log("UA:    Придатного 32-бітного MinGW-w64 компілятора не знайдено на цій машині — крок пропущено.");
            report.Log("UA:    Це НЕ означає, що з фіксом щось не так: рецепт нижче можна виконати на будь-");
            report.Log("UA:    якій машині з MinGW-w64 (MSYS2, WinLibs, або в CI на GitHub) і звірити хеш вручну");
            report.Log($"UA:    з тим, що надруковано вище в розділі SHA-256 ({bundledDllHash}). Щоб цей крок запрацював");
            report.Log("UA:    на ЦІЙ машині ПРЯМО ЗАРАЗ (без git, локально): встановіть MSYS2 (msys2.org) і в його");
            report.Log("UA:    консолі виконайте \"pacman -S mingw-w64-i686-gcc\", тоді додайте до PATH теку");
            report.Log("UA:    C:\\msys64\\mingw32\\bin (там лежить сам g++.exe) — або розпакуйте портативний");
            report.Log("UA:    32-бітний білд з winlibs.com (без інсталятора, просто zip) і додайте його bin\\ до PATH.");
            report.Log($"EN:    No suitable 32-bit MinGW-w64 compiler was found on this machine — step skipped.");
            report.Log("EN:    This does NOT mean anything is wrong with the fix: the recipe below can be run");
            report.Log("EN:    on any machine with MinGW-w64 (MSYS2, WinLibs, or a GitHub CI runner) and the hash");
            report.Log($"EN:    checked by hand against what was printed in the SHA-256 section above ({bundledDllHash}). To make");
            report.Log("EN:    this step work on THIS machine RIGHT NOW (no git, purely local): install MSYS2");
            report.Log("EN:    (msys2.org), run \"pacman -S mingw-w64-i686-gcc\" in its shell, then add");
            report.Log("EN:    C:\\msys64\\mingw32\\bin to PATH (that's where g++.exe lives) — or unzip a portable");
            report.Log("EN:    32-bit build from winlibs.com (no installer, just a zip) and add its bin\\ to PATH.");
            report.Log($"    {MovieSubtitleD3D9FixProvenance.BuildCommand}");
            return;
        }

        var rebuiltFileName = "rebuilt_d3d9.dll";
        var rebuiltPath = Path.Combine(workDir, rebuiltFileName);
        if (File.Exists(rebuiltPath))
            File.Delete(rebuiltPath);

        var psi = new ProcessStartInfo
        {
            FileName = compiler,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = true,
        };
        foreach (var arg in new[]
                 {
                     "-shared", "-O2", "-s", "-static", "-static-libgcc", "-static-libstdc++",
                     Path.GetFileName(cppPath), Path.GetFileName(defPath),
                     "-o", rebuiltFileName,
                     "-Wl,--enable-stdcall-fixup", "-Wl,--no-insert-timestamp",
                 })
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Process.Start повернув null / Process.Start returned null.");

            // UA: Читаємо stderr ДО WaitForExit — стандартний спосіб уникнути
            //     дедлоку, коли перенаправлений лише один потік. Очікування —
            //     циклом із періодичним "серцебиттям", а не одним мовчазним
            //     блокуванням, щоб було видно, що процес не завис.
            // EN: Read stderr BEFORE WaitForExit — the standard way to avoid a
            //     deadlock when only one stream is redirected. The wait is a
            //     loop with a periodic "heartbeat", not one silent block, so
            //     it's visible the process isn't hung.
            var stderrTask = Task.Run(() => proc.StandardError.ReadToEnd());

            var heartbeat = TimeSpan.FromSeconds(5);
            var timeout = TimeSpan.FromSeconds(60);
            var elapsed = TimeSpan.Zero;
            var exited = false;

            while (elapsed < timeout)
            {
                exited = proc.WaitForExit((int)heartbeat.TotalMilliseconds);
                if (exited)
                    break;

                elapsed += heartbeat;
                report.Log($"UA:    ... MinGW ще компілює ({(int)elapsed.TotalSeconds}с)");
                report.Log($"EN:    ... MinGW still compiling ({(int)elapsed.TotalSeconds}s)");
            }

            if (!exited)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* UA: найкраще зусилля / EN: best effort */ }
                report.Log("UA:    Компіляція не завершилась за 60с — перервано. Крок пропущено (НЕ провал фіксу).");
                report.Log("EN:    Compilation did not finish within 60s — aborted. Step skipped (NOT a fix failure).");
                return;
            }

            // UA: Процес уже завершився (exited == true) — StandardError вже
            //     закрито процесом, тож .Result тут не блокує довше, ніж
            //     потрібно, аби дочитати те, що вже написано.
            // EN: The process has already exited (exited == true) — the
            //     process closed StandardError, so .Result here doesn't
            //     block any longer than needed to finish reading what's
            //     already written.
            var stderr = stderrTask.Result;

            if (proc.ExitCode != 0 || !File.Exists(rebuiltPath))
            {
                report.Log("UA:    Компіляція НЕ вдалась — цей крок НЕ підтверджує фікс (дивись stderr нижче).");
                report.Log("EN:    Compilation FAILED — this step does NOT confirm the fix (see stderr below).");
                if (!string.IsNullOrWhiteSpace(stderr))
                    report.Log(stderr.Trim());
                return;
            }

            var rebuiltBytes = File.ReadAllBytes(rebuiltPath);
            var rebuiltHash = MovieSubtitleD3D9FixProvenance.ComputeSha256Hex(rebuiltBytes);
            var identical = string.Equals(rebuiltHash, bundledDllHash, StringComparison.OrdinalIgnoreCase);

            report.Log($"UA:    Перезібрано: {rebuiltPath}");
            report.Log($"UA:    SHA-256 перезібраного файла: {rebuiltHash}");
            report.Log($"UA:    Байт-у-байт збіг із вбудованим файлом: {(identical ? "так" : "ні")}");
            report.Log($"EN:    Rebuilt: {rebuiltPath}");
            report.Log($"EN:    SHA-256 of the rebuilt file: {rebuiltHash}");
            report.Log($"EN:    Byte-for-byte match with the bundled file: {(identical ? "yes" : "no")}");
        }
        catch (Exception ex)
        {
            report.Log($"UA:    Не вдалось запустити компілятор: {ex.Message}. Крок пропущено (НЕ провал фіксу).");
            report.Log($"EN:    Could not run the compiler: {ex.Message}. Step skipped (NOT a fix failure).");
        }
    }

    /// <summary>
    /// UA: Шукає придатний 32-бітний MinGW-w64 компілятор на цій машині.
    ///     Спершу перевіряє класичну крос-компіляторну назву
    ///     (i686-w64-mingw32-g++, як у MSYS2/Ubuntu mingw-w64 пакетах), а
    ///     потім — просто "g++" (так на native Windows встановленнях типу
    ///     MSYS2 mingw32-шелла чи winlibs.com називається сам компілятор,
    ///     БЕЗ префіксу), і в другому випадку додатково перевіряє через
    ///     "-dumpmachine", що знайдений g++ дійсно цільовий i686+mingw32, а
    ///     не якийсь інший (наприклад, 64-бітний чи MSVC-сумісний) компілятор.
    /// EN: Looks for a suitable 32-bit MinGW-w64 compiler on this machine.
    ///     First checks the classic cross-compiler name (i686-w64-mingw32-g++,
    ///     as used by MSYS2/Ubuntu mingw-w64 packages), then plain "g++"
    ///     (how native Windows installs like the MSYS2 mingw32 shell or
    ///     winlibs.com name the compiler itself, with NO prefix) — in the
    ///     second case additionally verifying via "-dumpmachine" that the
    ///     found g++ genuinely targets i686+mingw32, not some other (e.g.
    ///     64-bit or MSVC-compatible) compiler.
    /// </summary>
    private static string? FindMingwCompiler(DiagnosticReport report)
    {
        var prefixed = FindOnPath("i686-w64-mingw32-g++");
        if (prefixed is not null)
        {
            report.Log($"UA:    Знайдено: {prefixed}");
            report.Log($"EN:    Found: {prefixed}");
            return prefixed;
        }

        var plain = FindOnPath("g++");
        if (plain is not null)
        {
            var target = TryGetDumpMachine(plain);
            if (target is not null &&
                target.Contains("i686", StringComparison.OrdinalIgnoreCase) &&
                target.Contains("mingw32", StringComparison.OrdinalIgnoreCase))
            {
                report.Log($"UA:    Знайдено: {plain} (ціль/-dumpmachine: {target})");
                report.Log($"EN:    Found: {plain} (target/-dumpmachine: {target})");
                return plain;
            }

            report.Log($"UA:    Знайдено g++ на PATH ({plain}), але його ціль ({target ?? "не вдалось визначити"}) — НЕ i686-...-mingw32; пропущено (треба саме 32-бітний Windows-цільовий MinGW).");
            report.Log($"EN:    Found g++ on PATH ({plain}), but its target ({target ?? "could not be determined"}) is NOT i686-...-mingw32; skipped (need a 32-bit Windows-targeting MinGW specifically).");
        }

        return null;
    }

    /// <summary>
    /// UA: Запускає "compilerPath -dumpmachine" і повертає його вивід (наприклад,
    ///     "i686-w64-mingw32") або null, якщо компілятор не запустився чи впав.
    /// EN: Runs "compilerPath -dumpmachine" and returns its output (e.g.
    ///     "i686-w64-mingw32"), or null if the compiler failed to start or run.
    /// </summary>
    private static string? TryGetDumpMachine(string compilerPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = compilerPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("-dumpmachine");

            using var proc = Process.Start(psi);
            if (proc is null)
                return null;

            var stdout = proc.StandardOutput.ReadToEnd();
            var exited = proc.WaitForExit(5_000);
            if (!exited)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* UA: найкраще зусилля / EN: best effort */ }
                return null;
            }

            return proc.ExitCode == 0 ? stdout.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// UA: Шукає виконуваний файл на PATH (з урахуванням PATHEXT на Windows) і
    ///     повертає повний шлях до нього, або null, якщо не знайдено. На відміну
    ///     від System.Diagnostics.Process, який деколи покладається на пошук
    ///     оболонкою, це явний, передбачуваний пошук.
    /// EN: Searches PATH (honoring PATHEXT on Windows) for an executable and
    ///     returns its full path, or null if not found. Unlike relying on
    ///     Process/the shell to resolve bare names, this is an explicit,
    ///     predictable search.
    /// </summary>
    private static string? FindOnPath(string exeName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var exts = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE").Split(';')
            : [string.Empty];

        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;

            foreach (var ext in exts)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(dir, exeName + ext);
                }
                catch (ArgumentException)
                {
                    // UA: деякі записи PATH бувають биті/містять недопустимі символи — пропускаємо.
                    // EN: some PATH entries are malformed/contain invalid characters — skip them.
                    continue;
                }

                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
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
