// =============================================================================
// BF1LocalizationTool.GUI — Services/AutoSaveService.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Автоматично зберігає поточний стан перекладу в CSV кожні N хвилин.
//     Файл: autosave/autosave_YYYYMMDD_HHMMSS.csv поруч з .exe.
//     При запуску перевіряє чи є незбережений автозбереження і пропонує відновити.
// EN: Automatically saves current translation state to CSV every N minutes.
//     File: autosave/autosave_YYYYMMDD_HHMMSS.csv next to .exe.
//     On startup, checks for unsaved autosave and offers to restore.
// =============================================================================

using System.Text;

using System.IO;

namespace BF1LocalizationTool.GUI.Services;

public class AutoSaveService : IDisposable
{
    private readonly string _autoSaveDir;
    private System.Timers.Timer? _timer;
    private Func<string?>? _getContent;
    private bool _disposed;

    // UA: Інтервал автозбереження в хвилинах
    // EN: Autosave interval in minutes
    public int IntervalMinutes { get; set; } = 5;

    public event Action<string>? AutoSaved;

    public AutoSaveService()
    {
        _autoSaveDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "autosave");
    }

    // -------------------------------------------------------------------------
    // UA: Запускає автозбереження. content — функція що повертає CSV-вміст.
    // EN: Starts autosave. content — function that returns CSV content.
    // -------------------------------------------------------------------------
    public void Start(Func<string?> getContent)
    {
        _getContent = getContent;
        _timer      = new System.Timers.Timer(IntervalMinutes * 60_000);
        _timer.Elapsed += OnTimer;
        _timer.AutoReset = true;
        _timer.Start();
        SimpleLogger.Info($"AutoSave started, interval={IntervalMinutes}min");
    }

    public void Stop()
    {
        _timer?.Stop();
        SimpleLogger.Info("AutoSave stopped");
    }

    // -------------------------------------------------------------------------
    // UA: Перевіряє чи є файли автозбереження і повертає найновіший
    // EN: Checks if autosave files exist and returns the most recent one
    // -------------------------------------------------------------------------
    public string? FindLatestAutosave()
    {
        if (!Directory.Exists(_autoSaveDir)) return null;

        return Directory.GetFiles(_autoSaveDir, "autosave_*.csv")
            .OrderByDescending(f => f)
            .FirstOrDefault();
    }

    // -------------------------------------------------------------------------
    // UA: Видаляє всі файли автозбереження (після успішного збереження)
    // EN: Deletes all autosave files (after successful save)
    // -------------------------------------------------------------------------
    public void ClearAutosaves()
    {
        if (!Directory.Exists(_autoSaveDir)) return;
        foreach (var f in Directory.GetFiles(_autoSaveDir, "autosave_*.csv"))
        {
            try { File.Delete(f); }
            catch { /* ігноруємо / ignore */ }
        }
    }

    private void OnTimer(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            var content = _getContent?.Invoke();
            if (string.IsNullOrEmpty(content)) return;

            Directory.CreateDirectory(_autoSaveDir);
            var path = Path.Combine(_autoSaveDir,
                $"autosave_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            File.WriteAllText(path, content, Encoding.UTF8);
            SimpleLogger.Info($"AutoSave written: {path}");
            AutoSaved?.Invoke(path);

            // UA: Залишаємо лише останні 5 автозбережень
            // EN: Keep only the last 5 autosaves
            CleanupOldAutosaves(5);
        }
        catch (Exception ex)
        {
            SimpleLogger.Error("AutoSave failed", ex);
        }
    }

    private void CleanupOldAutosaves(int keepCount)
    {
        var files = Directory.GetFiles(_autoSaveDir, "autosave_*.csv")
            .OrderByDescending(f => f)
            .Skip(keepCount)
            .ToList();

        foreach (var f in files)
        {
            try { File.Delete(f); }
            catch { /* ігноруємо / ignore */ }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _timer?.Dispose();
        _disposed = true;
    }
}
