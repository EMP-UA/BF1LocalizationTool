// =============================================================================
// BF1LocalizationTool.Diagnostic — DiagnosticReport.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: ЄДИНИЙ спільний хелпер логування й збереження для ВСІХ діагностичних
//     команд. Одночасно пише кожен рядок у консоль (жива робота) І
//     накопичує його для збереження у файл — при об'ємних звітах (сотні
//     рядків) це зручніше за прокручування консолі.
//
//     Файл зберігається у СПІЛЬНУ папку "diagnostic-output" БІЛЯ .exe (не
//     поруч із вхідним core.lvl — свідомий вибір, щоб не смітити в теці з
//     ігровими файлами і мати всі звіти в одному місці).
// EN: THE single shared logging/saving helper for ALL diagnostic commands.
//     It simultaneously writes each line to the console (live progress)
//     AND accumulates it for saving to a file — for large reports
//     (hundreds of lines) that's more convenient than scrolling the
//     console.
//
//     The file is saved to a SHARED "diagnostic-output" folder NEXT TO
//     the .exe (not next to the input core.lvl — a deliberate choice, so
//     it doesn't clutter the game-files folder and all reports live in
//     one place).
// =============================================================================

using System.Text;

namespace BF1LocalizationTool.Diagnostic;

public sealed class DiagnosticReport
{
    private readonly List<string> _lines = [];
    private readonly string _commandName;

    public DiagnosticReport(string commandName)
    {
        _commandName = commandName;
    }

    // UA: Пише рядок одночасно в консоль і в буфер звіту. Виклик без
    //     аргументів (report.Log()) — порожній рядок, еквівалент
    //     Console.WriteLine().
    // EN: Writes a line to both the console and the report buffer. A
    //     parameterless call (report.Log()) — an empty line, equivalent
    //     to Console.WriteLine().
    public void Log(string line = "")
    {
        Console.WriteLine(line);
        _lines.Add(line);
    }

    // UA: Зберігає накопичений звіт у "{тека exe}/diagnostic-output/
    //     {commandName}_{yyyyMMdd_HHmmss}.txt" і друкує шлях в кінці
    //     (укр+англ, як у решті виводу). Повертає шлях для можливого
    //     подальшого використання (напр. відкрити файл автоматично).
    // EN: Saves the accumulated report to "{exe folder}/diagnostic-output/
    //     {commandName}_{yyyyMMdd_HHmmss}.txt" and prints the path at the
    //     end (UA+EN, matching the rest of the output). Returns the path
    //     for possible further use (e.g. auto-opening the file).
    public string Save()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "diagnostic-output");
        Directory.CreateDirectory(dir);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var path = Path.Combine(dir, $"{_commandName}_{stamp}.txt");

        File.WriteAllText(path, string.Join(Environment.NewLine, _lines), Encoding.UTF8);

        Console.WriteLine();
        Console.WriteLine($"UA: Звіт збережено у: {path}");
        Console.WriteLine($"EN: Report saved to: {path}");

        return path;
    }
}
