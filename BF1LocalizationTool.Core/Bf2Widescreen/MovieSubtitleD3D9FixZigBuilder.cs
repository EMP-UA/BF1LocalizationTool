// =============================================================================
// BF1LocalizationTool.Core — Bf2Widescreen/MovieSubtitleD3D9FixZigBuilder.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: Нативна, нічим не доповнена компіляція d3d9.dll ЦІЄЮ програмою —
//     без MSYS2, без Visual Studio C++ workload, без
//     будь-якого застосунку, який користувач мав би встановлювати вручну.
//     Клас завантажує ОФІЦІЙНИЙ, портативний компілятор Zig з ziglang.org
//     (сам Zig несе в собі повний Clang — тобто повноцінний C/C++
//     компілятор, а не лише свою власну мову), перевіряє його SHA-256
//     проти зашитого в код значення (щоб ніколи не виконувати недовірений
//     завантажений файл), кешує на диску (завантажується РІВНО ОДИН раз),
//     і використовує для компіляції ТОГО САМОГО d3d9_proxy.cpp/.def, що
//     вже вбудований у програму (MovieSubtitleD3D9FixProvenance.cs).
//
//     ЧОМУ ЦЕ КАНОНІЧНИЙ ШЛЯХ: саме цей механізм (запущений із Visual
//     Studio, без стороннього, окремо встановлюваного тулчейна) і зібрав
//     той бінарник, що реально протестований у грі, — тому вбудований у
//     програму Data/MovieSubtitleD3D9Fix.dll зібраний саме Zig-ом
//     (перевірено через objdump: внутрішня PE-назва модуля —
//     "zig_d3d9.dll"). Компілятор LLVM-based, а рантайм — Windows 10/11
//     UCRT (`api-ms-win-crt-*.dll`, вбудований компонент ОС із 2015 року),
//     тож жодного стороннього рантайму встановлювати не треба. Незалежна
//     перезбірка з того самого відкритого .cpp/.def на іншій машині (або
//     в CI) і звірка SHA-256 із RecordedDllSha256 — і є доказ походження:
//     підмінити код так, щоб перезбірка з нього все одно давала той самий
//     бінарник, неможливо.
//
//     ВИХІДНА НАЗВА ФАЙЛА. Рецепт компілює саме в `zig_d3d9.dll`, а НЕ
//     в `d3d9.dll` — щоб під час діагностики файл не плутався зі
//     справжньою системною d3d9.dll поруч. У теку гри як GameData\d3d9.dll
//     його кладе .iss-інсталятор — перейменування відбувається там, не тут.
//
//     ПОСТАЧАННЯ. Завантажується `zig-x86_64-windows-0.16.0.zip`
//     (~93 МБ) — офіційний реліз, адреса й SHA-256 взяті з
//     https://ziglang.org/download/index.json (машинозчитуваний реєстр
//     релізів, який публікує сам проєкт Zig, а не сторонній дзеркальний
//     сайт). Перевірено емпірично в пісочниці розробки: `zig c++ -target
//     x86-windows-gnu -shared -O2 -s d3d9_proxy.cpp d3d9_proxy.def -o
//     zig_d3d9.dll` успішно компілює цей реальний файл у коректний PE32
//     DLL з експортом рівно "Direct3DCreate9" (перевірено через objdump).
// EN: Native compilation of d3d9.dll BY THIS PROGRAM ITSELF, with no
//     external tooling — no MSYS2, no Visual Studio C++
//     workload, no application the user would have to install by hand.
//     The class downloads the OFFICIAL, portable Zig compiler from
//     ziglang.org (Zig
//     itself bundles the full Clang — i.e. a complete C/C++ compiler, not
//     just its own language), verifies its SHA-256 against a value pinned
//     in code (so an untrusted downloaded file is never executed), caches
//     it on disk (downloaded EXACTLY ONCE), and uses it to compile the
//     SAME d3d9_proxy.cpp/.def already bundled in the program
//     (MovieSubtitleD3D9FixProvenance.cs).
//
//     WHY THIS IS THE CANONICAL path: this very mechanism (run from
//     Visual Studio, with no separately installed third-party toolchain)
//     is what actually produced the binary tested in the game — which is
//     why the bundled Data/MovieSubtitleD3D9Fix.dll is built with Zig
//     (verified via objdump: its internal PE module name is
//     "zig_d3d9.dll"). The compiler is LLVM-based and the runtime is the
//     Windows 10/11 UCRT (`api-ms-win-crt-*.dll`, a built-in OS component
//     since 2015), so no separate runtime needs installing either. An
//     independent rebuild from the same open .cpp/.def on another machine
//     (or in CI), checked by SHA-256 against RecordedDllSha256, is the
//     provenance proof itself: tampering with the code so a rebuild from
//     it still produces the same binary is not possible.
//
//     OUTPUT FILE NAME. The recipe compiles to `zig_d3d9.dll`, NOT
//     `d3d9.dll` — so the file isn't confused with the real system
//     d3d9.dll sitting right next to it during diagnostics. The .iss
//     installer is what places it into the game folder as
//     GameData\d3d9.dll — the rename happens there, not here.
//
//     DISTRIBUTION. Downloads `zig-x86_64-windows-0.16.0.zip` (~93 MB) —
//     the official release, with its URL and SHA-256 taken from
//     https://ziglang.org/download/index.json (the machine-readable
//     release registry the Zig project itself publishes, not a
//     third-party mirror). Verified empirically in the development
//     sandbox: `zig c++ -target x86-windows-gnu -shared -O2 -s
//     d3d9_proxy.cpp d3d9_proxy.def -o zig_d3d9.dll` successfully compiles
//     the actual file into a correct PE32 DLL exporting exactly
//     "Direct3DCreate9" (verified via objdump).
// =============================================================================

using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace BF1LocalizationTool.Core.Bf2Widescreen;

/// <summary>
/// UA: Завантажує (з перевіркою SHA-256), кешує й запускає портативний
///     Zig-компілятор — самодостатній, без сторонніх встановлюваних
///     застосунків спосіб зібрати d3d9_proxy.cpp/.def прямо з програми.
///     Канонічний шлях і збірки, і перевірки походження — саме ним
///     зібрано вбудований у програму d3d9.dll.
/// EN: Downloads (with SHA-256 verification), caches, and runs the
///     portable Zig compiler — a self-contained way, with no separately
///     installed application, to build d3d9_proxy.cpp/.def right from the
///     program. The canonical path for both building it and verifying its
///     provenance — this is what produced the d3d9.dll bundled in the
///     program.
/// </summary>
public static class MovieSubtitleD3D9FixZigBuilder
{
    public const string ZigVersion = "0.16.0";

    // UA: Джерело — офіційний https://ziglang.org/download/index.json (не
    //     дзеркало/агрегатор третьої сторони). НЕ const (а не тому, що
    //     інтерпольований const-рядок неможливий — він дозволений з C# 10,
    //     якщо всі підстановки теж const) — просто щоб не покладатися на
    //     цю деталь мови в коді, який не можна відкомпілювати в цьому
    //     середовищі перед відправкою користувачу.
    // EN: Source — the official https://ziglang.org/download/index.json
    //     (not a third-party mirror/aggregator). NOT const (not because a
    //     constant interpolated string is impossible — it has been
    //     allowed since C# 10 as long as every placeholder is itself
    //     const) — simply to avoid relying on that language detail in code
    //     that cannot be compiled in this environment before being handed
    //     to the user.
    private static readonly string ZigDownloadUrl =
        $"https://ziglang.org/download/{ZigVersion}/zig-x86_64-windows-{ZigVersion}.zip";

    public const string ZigArchiveSha256 =
        "68659eb5f1e4eb1437a722f1dd889c5a322c9954607f5edcf337bc3684a75a7e";

    // UA: Точний рецепт компіляції ЦИМ шляхом — перевірено емпірично (див.
    //     коментар вище класу). Вихідний файл навмисно зветься
    //     zig_d3d9.dll, не d3d9.dll (див. "ВИХІДНА НАЗВА ФАЙЛА" вище).
    // EN: The exact compilation recipe for THIS path — verified
    //     empirically (see the class comment above). The output file is
    //     deliberately named zig_d3d9.dll, not d3d9.dll (see "OUTPUT FILE
    //     NAME" above).
    public const string BuildCommand =
        "zig c++ -target x86-windows-gnu -shared -O2 -s d3d9_proxy.cpp d3d9_proxy.def -o zig_d3d9.dll";

    public sealed record EnsureResult(string ZigExePath, bool WasDownloadedThisRun);

    public sealed record CompileResult(bool Success, string? OutputPath, string StdErr);

    // UA: Кеш навмисно поруч із виконуваним файлом програми (AppContext.
    //     BaseDirectory), а НЕ в %LocalAppData%. Причина: тека програми
    //     може лежати в синхронізованій OneDrive-теці, а OneDrive синхронізує
    //     файл ЛИШЕ при його реальній зміні — розпакований Zig-архів (~100
    //     МБ) після першого й ЄДИНОГО завантаження більше не змінюється,
    //     тож повторної синхронізації не буде; на безлімітному
    //     гігабітному з'єднанні цей одноразовий обсяг взагалі не проблема.
    //     Розташування поруч із .exe також робить самодостатність шляху
    //     Zig видимою й перевірюваною — не десь у прихованій
    //     системній теці профілю, а прямо в теці збірки програми. Побічний
    //     ефект, теж бажаний: `bin/`/`obj/` теки .NET-проєкту вже звично не
    //     потрапляють у git, тож ця тека кешу автоматично лишається поза
    //     репозиторієм без додаткового .gitignore-запису.
    // EN: The cache deliberately lives next to the program's own executable
    //     (AppContext.BaseDirectory), NOT under %LocalAppData%. Reason: the
    //     program's own folder can live inside a OneDrive-synced folder, and
    //     OneDrive only re-syncs a file when it actually changes — the
    //     extracted Zig archive (~100 MB) never changes again after its one
    //     and only download, so there is no repeated sync; on an unlimited
    //     gigabit connection that one-time size isn't an issue at all.
    //     Living next to the .exe also makes the Zig path's self-sufficiency
    //     literally visible and inspectable — not tucked away in a
    //     hidden per-OS-user profile folder, but right in the program's own
    //     build folder. A welcome side effect: a .NET project's `bin`/`obj`
    //     folders are already conventionally excluded from git, so this
    //     cache folder stays out of the repository with no extra
    //     .gitignore entry needed.
    public static string GetCacheRoot() =>
        Path.Combine(AppContext.BaseDirectory, "zig-cache", ZigVersion);

    /// <summary>
    /// UA: Гарантує наявність zig.exe локально: якщо вже закешовано —
    ///     нічого не завантажує. Якщо ні — качає офіційний архів,
    ///     ОБОВ'ЯЗКОВО звіряє SHA-256 перед розпакуванням (недовірений
    ///     файл видаляється й компіляція скасовується, а не мовчки
    ///     виконується), розпаковує, кешує.
    /// EN: Ensures zig.exe is available locally: if already cached,
    ///     downloads nothing. Otherwise fetches the official archive,
    ///     ALWAYS verifies its SHA-256 before extracting (an untrusted
    ///     file is deleted and the build aborted, never silently run),
    ///     extracts it, and caches it.
    /// </summary>
    public static async Task<EnsureResult> EnsureZigAvailableAsync(Action<string> log, CancellationToken ct = default)
    {
        var cacheRoot = GetCacheRoot();
        var zigExePath = Path.Combine(cacheRoot, $"zig-x86_64-windows-{ZigVersion}", "zig.exe");

        if (File.Exists(zigExePath))
        {
            log($"UA:    Знайдено закешований Zig {ZigVersion}: {zigExePath}");
            log($"EN:    Found cached Zig {ZigVersion}: {zigExePath}");
            return new EnsureResult(zigExePath, WasDownloadedThisRun: false);
        }

        Directory.CreateDirectory(cacheRoot);
        var archivePath = Path.Combine(cacheRoot, $"zig-x86_64-windows-{ZigVersion}.zip");

        log($"UA:    Компілятор не закешовано — завантажую офіційний Zig {ZigVersion} з ziglang.org");
        log("UA:    (~93 МБ, ОДИН раз; надалі кешується назавжди):");
        log($"UA:    {ZigDownloadUrl}");
        log($"EN:    Compiler not cached — downloading the official Zig {ZigVersion} from ziglang.org");
        log("EN:    (~93 MB, ONE time; cached forever afterwards):");
        log($"EN:    {ZigDownloadUrl}");

        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromMinutes(10);
            using (var response = await http.GetAsync(ZigDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength;

                await using var httpStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = File.Create(archivePath);

                // UA: Ручне читання шматками замість CopyToAsync — щоб мати
                //     періодичний вивід прогресу під час ~93 МБ завантаження
                //     (без цього програма мовчить кілька десятків секунд, і
                //     немає ознаки, що вона не зависла).
                // EN: Manual chunked read instead of CopyToAsync — to give
                //     periodic progress output during the ~93 MB download
                //     (without this the program stays silent for tens of
                //     seconds, with no sign it isn't hung).
                var buffer = new byte[81920];
                long readSoFar = 0;
                var lastReportAt = DateTime.UtcNow;
                int bytesRead;
                while ((bytesRead = await httpStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    readSoFar += bytesRead;

                    if (DateTime.UtcNow - lastReportAt < TimeSpan.FromSeconds(2))
                        continue;

                    lastReportAt = DateTime.UtcNow;
                    var readMb = readSoFar / 1024 / 1024;
                    if (totalBytes is > 0)
                    {
                        var totalMb = totalBytes.Value / 1024 / 1024;
                        var percent = (int)(readSoFar * 100 / totalBytes.Value);
                        log($"UA:    ... завантажено {readMb} МБ з {totalMb} МБ ({percent}%)");
                        log($"EN:    ... downloaded {readMb} MB of {totalMb} MB ({percent}%)");
                    }
                    else
                    {
                        log($"UA:    ... завантажено {readMb} МБ");
                        log($"EN:    ... downloaded {readMb} MB");
                    }
                }
            }
        }
        catch
        {
            if (File.Exists(archivePath))
                File.Delete(archivePath);
            throw;
        }

        var actualHash = ComputeFileSha256Hex(archivePath);
        if (!string.Equals(actualHash, ZigArchiveSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(archivePath);
            throw new InvalidOperationException(
                "UA: SHA-256 завантаженого архіву Zig НЕ ЗБІГАЄТЬСЯ із зашитим у код значенням " +
                $"(очікувано {ZigArchiveSha256}, отримано {actualHash}). Файл видалено, компіляцію " +
                "скасовано — недовірений бінарник НІКОЛИ не запускається. / " +
                "EN: The downloaded Zig archive's SHA-256 does NOT MATCH the value pinned in code " +
                $"(expected {ZigArchiveSha256}, got {actualHash}). File deleted, build aborted — " +
                "an untrusted binary is NEVER executed.");
        }

        log("UA:    SHA-256 завантаженого архіву збігається із зашитим у код значенням — файл довірено.");
        log("EN:    The downloaded archive's SHA-256 matches the value pinned in code — the file is trusted.");

        ZipFile.ExtractToDirectory(archivePath, cacheRoot);
        File.Delete(archivePath);

        if (!File.Exists(zigExePath))
        {
            throw new FileNotFoundException(
                $"UA: Після розпакування zig.exe не знайдено за очікуваним шляхом: {zigExePath} / " +
                $"EN: zig.exe not found at the expected path after extraction: {zigExePath}");
        }

        return new EnsureResult(zigExePath, WasDownloadedThisRun: true);
    }

    /// <summary>
    /// UA: Компілює вказані .cpp/.def через щойно забезпечений zig.exe,
    ///     точно рецептом BuildCommand. `log` отримує періодичні
    ///     "серцебиття" під час компіляції (перший запуск для нової цілі
    ///     будує libc++/compiler-rt з нуля й може тривати довго — без
    ///     періодичного виводу немає жодної ознаки, що процес не завис).
    /// EN: Compiles the given .cpp/.def through the just-ensured zig.exe,
    ///     exactly per the BuildCommand recipe. `log` receives periodic
    ///     "heartbeats" during compilation (the first run for a new target
    ///     builds libc++/compiler-rt from scratch and can take a while —
    ///     without periodic output there is no sign the process isn't hung).
    /// </summary>
    public static async Task<CompileResult> CompileAsync(
        string zigExePath, string workDir, string cppFileName, string defFileName, string outputFileName,
        Action<string> log, CancellationToken ct = default)
    {
        var outputPath = Path.Combine(workDir, outputFileName);
        if (File.Exists(outputPath))
            File.Delete(outputPath);

        var psi = new ProcessStartInfo
        {
            FileName = zigExePath,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = true,
        };
        foreach (var arg in new[]
                 {
                     "c++", "-target", "x86-windows-gnu", "-shared", "-O2", "-s",
                     cppFileName, defFileName, "-o", outputFileName,
                 })
        {
            psi.ArgumentList.Add(arg);
        }

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Process.Start повернув null / Process.Start returned null.");

        var stderrTask = proc.StandardError.ReadToEndAsync(ct);

        // UA: Перший запуск для нового тагрета Zig будує й кешує свою
        //     стандартну бібліотеку (libc++/compiler-rt) з нуля — повільно
        //     (десятки секунд і більше залежно від машини); наступні
        //     виклики значно швидші завдяки внутрішньому кешу Zig. Тому
        //     таймаут щедрий, а очікування — з періодичним "серцебиттям",
        //     а не одним мовчазним блокуванням на 5 хвилин.
        // EN: The first run for a new Zig target builds and caches its
        //     standard library (libc++/compiler-rt) from scratch — slow
        //     (tens of seconds or more, depending on the machine);
        //     subsequent calls are much faster thanks to Zig's own internal
        //     cache. Hence the generous timeout, and a wait broken into
        //     periodic "heartbeats" rather than one silent 5-minute block.
        var heartbeat = TimeSpan.FromSeconds(5);
        var timeout = TimeSpan.FromMinutes(5);
        var elapsed = TimeSpan.Zero;
        var exited = false;

        while (elapsed < timeout)
        {
            exited = proc.WaitForExit((int)heartbeat.TotalMilliseconds);
            if (exited)
                break;

            elapsed += heartbeat;
            log($"UA:    ... Zig ще компілює ({(int)elapsed.TotalSeconds}с; перший запуск для нової цілі будує стандартну бібліотеку з нуля — це нормально)");
            log($"EN:    ... Zig still compiling ({(int)elapsed.TotalSeconds}s; the first run for a new target builds the standard library from scratch — this is normal)");
        }

        if (!exited)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* UA: найкраще зусилля / EN: best effort */ }
            return new CompileResult(false, null, "Compilation did not finish within 5 minutes — aborted.");
        }

        var stderr = await stderrTask;
        return new CompileResult(proc.ExitCode == 0 && File.Exists(outputPath), outputPath, stderr);
    }

    private static string ComputeFileSha256Hex(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
