// =============================================================================
// BF1LocalizationTool.GUI — Services/SimpleLogger.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Простий файловий логер. Пише в logs/bf1tool_YYYYMMDD.log поруч з .exe.
// EN: Simple file logger. Writes to logs/bf1tool_YYYYMMDD.log next to .exe.
// =============================================================================

using System.IO;

namespace BF1LocalizationTool.GUI.Services;

public static class SimpleLogger
{
    private static readonly string LogDir = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "logs");

    private static string LogPath =>
        Path.Combine(LogDir, $"bf1tool_{DateTime.Now:yyyyMMdd}.log");

    private static readonly object _lock = new();

    public static void Info(string message)  => Write("INFO ", message);
    public static void Warn(string message)  => Write("WARN ", message);
    public static void Error(string message, Exception? ex = null)
    {
        Write("ERROR", message);
        if (ex is not null)
            Write("STACK", ex.ToString());
    }

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";
            lock (_lock)
                File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch { /* UA: логер не має падати сам по собі / EN: logger must not crash itself */ }
    }
}
