// =============================================================================
// BF1LocalizationTool.Core — Bf2Widescreen/MovieSubtitleD3D9FixProvenance.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (не генерує ігрових файлів — лише діагностичні дані) / DIAGNOSTIC (generates no game files — diagnostic data only)
// =============================================================================
// UA: ЦЕЙ САМЕ КЛАС — конкретна відповідь на питання "звідки взявся
//     d3d9.dll", яку можна показати модерації NexusMods (чи будь-кому
//     іншому). Він не встановлює й не видаляє нічого в теці гри (файл ставиться
//     .iss-інсталятором, а не кодом) — єдина його робота: дістати з
//     ВБУДОВАНИХ РЕСУРСІВ ЦІЄЇ САМОЇ збірки BF1LocalizationTool і
//     скомпільований файл, і вихідний код (.cpp/.def), з яких він
//     зібраний, порахувати їхні SHA-256 наживо (не переписати з
//     документації) — і, за бажанням виклику, дати
//     GenerateD3D9FixProvenanceReportCommand (BF1LocalizationTool.Diagnostic)
//     ПЕРЕЗІБРАТИ той самий код тим самим компілятором і звірити результат
//     байт-у-байт із тим, що реально лежить у програмі. Це найсильніший
//     можливий доказ походження: не голослівне запевнення в надійності
//     компілятора, а відтворюваність, яку модератор (чи будь-хто) може
//     перевірити сам.
//
//     Конкретний пункт меню, що використовує цей клас (опосередковано,
//     через GenerateD3D9FixProvenanceReportCommand.cs): "Відео та
//     субтитри" -> "★★ ПОХОДЖЕННЯ d3d9.dll" у
//     BF1LocalizationTool.Diagnostic/Program.cs.
//
//     Джерело .cpp/.def вбудоване НЕ як окрема копія в теці Core, а через
//     <EmbeddedResource Include="..\tools\...\d3d9_proxy.cpp" Link="..."/>
//     у BF1LocalizationTool.Core.csproj — тобто це РІВНО ТОЙ САМИЙ файл,
//     що лежить у репозиторії й з якого фактично збирався бінарник, без
//     ризику розбіжності між "показаною" копією і реальною.
//
//     Цей клас лише читає вбудовані ресурси САМОЇ ПРОГРАМИ (жодного
//     побічного ефекту на диску користувача) — встановлення файлу в теку
//     гри виконує .iss-інсталятор, як і решта файлів локалізації, а не
//     код цього застосунку.
// EN: THIS CLASS IS the concrete answer to "where did d3d9.dll come from",
//     the one that can be shown to NexusMods moderation (or anyone else).
//     It does not install or remove anything in the game folder (the file
//     is placed by the .iss installer, not by code) — its only job is to pull,
//     from THIS SAME BF1LocalizationTool build's OWN EMBEDDED RESOURCES,
//     both the compiled file and the source code (.cpp/.def) it was built
//     from, hash them live (not copy a number from documentation) — and,
//     when the caller wants it,
//     GenerateD3D9FixProvenanceReportCommand (BF1LocalizationTool.Diagnostic)
//     REBUILDS that same source with the same compiler and diffs the
//     result byte-for-byte against what is actually bundled in the
//     program. That is the strongest provenance proof there is: not
//     a bare assurance that the compiler can be trusted, but a
//     reproducibility any moderator (or anyone) can check themselves.
//
//     The concrete menu item that uses this class (indirectly, through
//     GenerateD3D9FixProvenanceReportCommand.cs): "Movies & subtitles" ->
//     "★★ PROVENANCE of d3d9.dll" in
//     BF1LocalizationTool.Diagnostic/Program.cs.
//
//     The .cpp/.def source is bundled NOT as a separate copy inside
//     Core's own folder, but via
//     <EmbeddedResource Include="..\tools\...\d3d9_proxy.cpp" Link="..."/>
//     in BF1LocalizationTool.Core.csproj — i.e. it is EXACTLY the same
//     file that lives in the repository and that the binary was actually
//     built from, with no risk of a "shown" copy drifting from the real
//     one.
//
//     This class only reads THE PROGRAM'S OWN embedded resources (no side
//     effects on the user's disk at all) — placing the file into the game
//     folder is the .iss installer's job, like every other localization
//     file, not this application's code.
// =============================================================================

using System;
using System.IO;
using System.Security.Cryptography;

namespace BF1LocalizationTool.Core.Bf2Widescreen;

/// <summary>
/// UA: Дає доступ до вбудованих (у цю саму збірку BF1LocalizationTool)
///     копій скомпільованого d3d9-фіксу й ЙОГО ВЛАСНОГО вихідного коду —
///     для перевірки походження. Жодних побічних ефектів на диску: лише
///     читання ресурсів збірки.
/// EN: Exposes the copies of the compiled d3d9 fix and ITS OWN source
///     code embedded in this very BF1LocalizationTool build — for
///     provenance verification. No side effects on disk: reads the build's
///     own resources only.
/// </summary>
public static class MovieSubtitleD3D9FixProvenance
{
    private const string DllResourceName = "BF1LocalizationTool.Core.Bf2Widescreen.Data.MovieSubtitleD3D9Fix.dll";
    private const string CppSourceResourceName = "BF1LocalizationTool.Core.Bf2Widescreen.Data.Source.d3d9_proxy.cpp";
    private const string DefSourceResourceName = "BF1LocalizationTool.Core.Bf2Widescreen.Data.Source.d3d9_proxy.def";

    /// <summary>
    /// UA: SHA-256 бінарника, що ЗАРАЗ вбудований у цю збірку
    ///     BF1LocalizationTool — зібраний тулчейном Zig (рецепт у
    ///     MovieSubtitleD3D9FixZigBuilder.BuildCommand), підтверджений
    ///     двома незалежними Zig-перезбірками з ідентичного .cpp/.def на
    ///     реальній машині користувача і живим тестом у грі.
    ///     Онови це значення, лише якщо свідомо змінюєш d3d9_proxy.cpp/.def
    ///     і перезбираєш — інакше GenerateD3D9FixProvenanceReportCommand
    ///     правильно повідомить про розбіжність.
    /// EN: SHA-256 of the binary CURRENTLY bundled in this
    ///     BF1LocalizationTool build — built with the Zig toolchain (recipe
    ///     in MovieSubtitleD3D9FixZigBuilder.BuildCommand), confirmed by two
    ///     independent Zig rebuilds of identical .cpp/.def on the user's
    ///     real machine and a live in-game test. Update this value only
    ///     when you deliberately change d3d9_proxy.cpp/.def and rebuild —
    ///     otherwise GenerateD3D9FixProvenanceReportCommand will correctly
    ///     report a mismatch.
    /// </summary>
    public const string RecordedDllSha256 = "d652359a9b5102352e867d54626731a6ac226841a40b5312c63088fbe07db9e4";

    public static byte[] GetBundledDllBytes() => ReadResource(DllResourceName);

    public static byte[] GetBundledCppSourceBytes() => ReadResource(CppSourceResourceName);

    public static byte[] GetBundledDefSourceBytes() => ReadResource(DefSourceResourceName);

    public static string ComputeSha256Hex(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static byte[] ReadResource(string name)
    {
        using var stream = typeof(MovieSubtitleD3D9FixProvenance).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Вбудований ресурс не знайдено / Embedded resource not found: {name}");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
