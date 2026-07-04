// =============================================================================
// BF1LocalizationTool.GUI — MainWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================

using System.IO;
using BF1LocalizationTool.Core.Localization;
using BF1LocalizationTool.Core.Models;
using BF1LocalizationTool.GUI.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace BF1LocalizationTool.GUI;

public partial class MainWindow : Window
{
    // =========================================================================
    // UA: ПОЛЯ / EN: FIELDS
    // =========================================================================

    private readonly LvlLocalizationService _service  = new();
    private readonly AutoSaveService        _autoSave = new();
    private ObservableCollection<EntryRow>  _allRows  = [];
    private ICollectionView?                _view;

    private string _sourceLang = "english";
    private string _targetLang = "uk_english";
    private bool   _isDarkTheme = true;
    private double _gridFontSize = 13;

    // UA: Властивість для прив'язки розміру шрифту з XAML
    // EN: Property for font size binding from XAML
    public double FontSizeGrid
    {
        get => _gridFontSize;
        set { _gridFontSize = value; RefreshGrid(); }
    }

    // =========================================================================
    // UA: ІНІЦІАЛІЗАЦІЯ / EN: INITIALIZATION
    // =========================================================================

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _autoSave.AutoSaved += path =>
            Dispatcher.InvokeAsync(() =>
                TxtAutoSaveStatus.Text = $"✓ autosave {DateTime.Now:HH:mm}");

        SimpleLogger.Info("BF1LocalizationTool started");
        CheckForAutosave();
    }

    private void CheckForAutosave()
    {
        var latest = _autoSave.FindLatestAutosave();
        if (latest is null) return;

        var result = System.Windows.MessageBox.Show(
            $"UA: Знайдено автозбереження:\n{latest}\n\nВідновити?\n\n" +
            $"EN: Autosave found:\n{latest}\n\nRestore?",
            "UA: Автозбереження / EN: Autosave",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            TxtStatus.Text = $"UA: Відкрийте core.lvl і імпортуйте: {latest} / EN: Open core.lvl then import: {latest}";
    }

    // =========================================================================
    // UA: ВІДКРИТИ / EN: OPEN
    // =========================================================================

    private async void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "UA: Відкрити core.lvl / EN: Open core.lvl",
            Filter = "LVL files (*.lvl)|*.lvl|All files (*.*)|*.*",
            FileName = "core.lvl"
        };
        if (dlg.ShowDialog() != true) return;

        SetBusy(true, "UA: Завантаження... / EN: Loading...");
        SimpleLogger.Info($"Opening: {dlg.FileName}");

        try
        {
            await _service.LoadAsync(dlg.FileName);

            await Dispatcher.InvokeAsync(() =>
            {
                PopulateLanguageSelectors();
                RefreshGrid();
                SetButtonsEnabled(true);

                var gameLabel = _service.GameVersion == GameVersion.BF1
                    ? "BF1 (2004)" : "BF2 (2005)";
                Title = $"BF1 Localization Tool — EMP_UA [{gameLabel}]";
                SetStatus($"UA: Завантажено [{gameLabel}]: {dlg.FileName} / EN: Loaded [{gameLabel}]: {dlg.FileName}");
            });

            _autoSave.Start(BuildCsvContent);
            SimpleLogger.Info($"Loaded OK: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error("Load failed", ex);
            System.Windows.MessageBox.Show(
                $"UA: Помилка завантаження:\n{ex.Message}\n\nEN: Load error:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("UA: Помилка завантаження / EN: Load error");
        }
        finally { SetBusy(false); }
    }

    // =========================================================================
    // UA: ЗБЕРЕГТИ / EN: SAVE
    // =========================================================================

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title    = "UA: Зберегти core.lvl — ОБЕРІТЬ ПАПКУ МОДА, не оригінал гри! / " +
                       "EN: Save core.lvl — CHOOSE YOUR MOD FOLDER, not the game's original!",
            Filter   = "LVL files (*.lvl)|*.lvl",
            FileName = "core.lvl"
        };
        if (dlg.ShowDialog() != true) return;

        SetBusy(true, "UA: Збереження... / EN: Saving...");
        GridEntries.CommitEdit(DataGridEditingUnit.Row, true);
        ApplyRowEditsToService();

        // UA: Попередження про кирилицю у BF1 (Latin1 мовчки замінить на '?')
        // EN: Warning about Cyrillic in BF1 (Latin1 silently replaces with '?')
        if (_service.GameVersion == GameVersion.BF1)
        {
            var hasCyrillic = _allRows.Any(r =>
                r.Translation != null &&
                System.Text.RegularExpressions.Regex.IsMatch(r.Translation, @"\p{IsCyrillic}"));
            if (hasCyrillic)
            {
                var warn = System.Windows.MessageBox.Show(
    """
    UA: Увага! BF1 не підтримує кирилицю без модифікації шрифтів.
    Всі українські букви будуть збережені як '?'.
    Все одно зберегти?

    EN: Warning! BF1 does not support Cyrillic without font modification.
    All Ukrainian letters will be saved as '?'.
    Save anyway?
    """,
    "UA: Увага / EN: Warning",
    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (warn != MessageBoxResult.Yes) { SetBusy(false); return; }
            }
        }

        try
        {
            await _service.SaveAsync(dlg.FileName, _targetLang);
            _autoSave.ClearAutosaves();
            SetStatus($"UA: Збережено [{_targetLang}]: {dlg.FileName} / EN: Saved [{_targetLang}]: {dlg.FileName}");
            SimpleLogger.Info($"Saved [{_targetLang}]: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error("Save failed", ex);
            System.Windows.MessageBox.Show(
                $"UA: Помилка збереження:\n{ex.Message}\n\nEN: Save error:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { SetBusy(false); }
    }

    // =========================================================================
    // UA: ЕКСПОРТ / ІМПОРТ CSV / EN: EXPORT / IMPORT CSV
    // =========================================================================

    private async void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title    = "UA: Експорт CSV / EN: Export CSV",
            Filter   = "CSV (*.csv)|*.csv",
            FileName = $"bf1_{_sourceLang}.csv"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            ApplyRowEditsToService();
            await _service.ExportCsvAsync(_sourceLang, dlg.FileName);
            SetStatus($"UA: Експортовано: {dlg.FileName} / EN: Exported: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error("Export failed", ex);
            System.Windows.MessageBox.Show(ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnImportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "UA: Імпорт CSV / EN: Import CSV",
            Filter = "CSV (*.csv)|*.csv"
        };
        if (dlg.ShowDialog() != true) return;

        SetBusy(true, "UA: Імпорт... / EN: Importing...");
        try
        {
            await _service.ImportCsvAsync(_targetLang, dlg.FileName);
            RefreshGrid();
            SetStatus($"UA: Імпортовано в [{_targetLang}] / EN: Imported into [{_targetLang}]");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error("Import failed", ex);
            System.Windows.MessageBox.Show(ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { SetBusy(false); }
    }

    // =========================================================================
    // UA: МОВНІ СЕЛЕКТОРИ / EN: LANGUAGE SELECTORS
    // =========================================================================

    private void CmbSourceLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbSourceLang.SelectedItem is string lang)
        { _sourceLang = lang; RefreshGrid(); }
    }

    private void CmbTargetLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbTargetLang.SelectedItem is string lang)
        { _targetLang = lang; RefreshGrid(); }
    }

    // =========================================================================
    // UA: ФІЛЬТРИ / EN: FILTERS
    // =========================================================================

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _view?.Refresh();
        UpdateProgressDisplay();
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        _view?.Refresh();
        UpdateProgressDisplay();
    }

    private bool FilterRow(object obj)
    {
        if (obj is not EntryRow row) return false;

        if (RbUntranslated.IsChecked == true && (row.IsTranslated || row.IsTechnical)) return false;
        if (RbTranslated.IsChecked   == true && !row.IsTranslated)  return false;
        if (RbTechnical.IsChecked    == true && !row.IsTechnical)    return false;
        if (RbInvalid.IsChecked      == true && row.IsValid)         return false;

        var search = TxtSearch.Text?.Trim();
        if (string.IsNullOrEmpty(search)) return true;

        return row.Original?.Contains(search,    StringComparison.OrdinalIgnoreCase) == true
            || row.Translation?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
            || row.HashDisplay.Contains(search,  StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // UA: КНОПКИ РЕДАГУВАННЯ / EN: EDIT BUTTONS
    // =========================================================================

    private void BtnCopySourceToTarget_Click(object sender, RoutedEventArgs e)
    {
        if (GridEntries.SelectedItem is EntryRow row)
        {
            row.Translation = row.Original;
            _view?.Refresh();
            UpdateProgressDisplay();
        }
    }

    private void BtnMarkTechnical_Click(object sender, RoutedEventArgs e)
    {
        if (GridEntries.SelectedItem is EntryRow row)
        {
            row.IsTechnical = !row.IsTechnical;
            _view?.Refresh();
            UpdateProgressDisplay();
        }
    }

    private void BtnAutoMarkTechnical_Click(object sender, RoutedEventArgs e)
    {
        var count = 0;
        foreach (var row in _allRows.Where(r => !r.IsTechnical))
        {
            if (TechnicalStringService.IsTechnical(row.Original))
            { row.IsTechnical = true; count++; }
        }
        _view?.Refresh();
        UpdateProgressDisplay();
        SetStatus($"UA: Позначено технічних: {count} / EN: Marked technical: {count}");
    }

    // =========================================================================
    // UA: ТЕМА / ШРИФТ / EN: THEME / FONT
    // =========================================================================

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        _isDarkTheme = !_isDarkTheme;
        ApplyTheme();
        BtnTheme.Content = _isDarkTheme
            ? "☾ UA: Темна / EN: Dark"
            : "☀ UA: Світла / EN: Light";
    }

    private void ApplyTheme()
    {
        // UA: Замінюємо brush-об'єкти цілком — {DynamicResource} підхопить нові значення.
        //     Стилі DataGrid/ComboBox/TextBox використовують {DynamicResource} тому теж оновляться.
        //     ВАЖЛИВО: саме заміна об'єкта (не зміна Color) спрацьовує з DynamicResource.
        // EN: Replace brush objects entirely — {DynamicResource} will pick up new values.
        //     DataGrid/ComboBox/TextBox styles use {DynamicResource} so they update too.
        //     IMPORTANT: object replacement (not Color change) works with DynamicResource.
        var res = Application.Current.Resources;
        if (_isDarkTheme)
        {
            res["BgWindowBrush"]      = new SolidColorBrush(Color.FromRgb(0x0D, 0x0A, 0x14));
            res["BgPanelBrush"]       = new SolidColorBrush(Color.FromRgb(0x1A, 0x15, 0x25));
            res["BgRowAltBrush"]      = new SolidColorBrush(Color.FromRgb(0x22, 0x1C, 0x30));
            res["TextPrimaryBrush"]   = new SolidColorBrush(Color.FromRgb(0xE8, 0xE0, 0xF0));
            res["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x90, 0x80, 0xA8));
            // UA: Темна тема — яскравий бірюзовий, чітко виділяється на темному фоні
            // EN: Dark theme — bright cyan, clearly stands out on dark background
            res["TechnicalBrush"]     = new SolidColorBrush(Color.FromRgb(0x4F, 0xD9, 0xE8));
        }
        else
        {
            res["BgWindowBrush"]      = new SolidColorBrush(Color.FromRgb(0xF0, 0xEB, 0xF8));
            res["BgPanelBrush"]       = new SolidColorBrush(Color.FromRgb(0xE0, 0xD8, 0xEE));
            res["BgRowAltBrush"]      = new SolidColorBrush(Color.FromRgb(0xD0, 0xC8, 0xE0));
            res["TextPrimaryBrush"]   = new SolidColorBrush(Color.FromRgb(0x1A, 0x0A, 0x2E));
            res["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x50, 0x40, 0xA0));
            // UA: Світла тема — насичений темно-бірюзовий, чітко виділяється на світлому фоні
            // EN: Light theme — saturated dark teal, clearly stands out on light background
            res["TechnicalBrush"]     = new SolidColorBrush(Color.FromRgb(0x0A, 0x6E, 0x7D));
        }
    }

    private void BtnFontDecrease_Click(object sender, RoutedEventArgs e)
    {
        if (_gridFontSize > 9) { _gridFontSize -= 1; GridEntries.FontSize = _gridFontSize; }
    }

    private void BtnFontIncrease_Click(object sender, RoutedEventArgs e)
    {
        if (_gridFontSize < 24) { _gridFontSize += 1; GridEntries.FontSize = _gridFontSize; }
    }

    // =========================================================================
    // UA: DATAGRID ПОДІЇ / EN: DATAGRID EVENTS
    // =========================================================================

    private void GridEntries_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.Item is EntryRow row && e.EditingElement is TextBox tb)
        {
            row.Translation = string.IsNullOrWhiteSpace(tb.Text) ? null : tb.Text;
            row.Validate();
        }
        UpdateProgressDisplay();
    }

    private void GridEntries_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = GridEntries.SelectedItem is EntryRow;
        BtnCopySourceToTarget.IsEnabled = hasSelection;
        BtnMarkTechnical.IsEnabled      = hasSelection;
    }

    private void GridEntries_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        // UA: Сірий фон для технічних рядків
        // EN: Gray background for technical rows
        if (e.Row.Item is EntryRow row && row.IsTechnical)
            e.Row.Opacity = 0.6;
    }

    // =========================================================================
    // UA: КОНТЕКСТНЕ МЕНЮ / EN: CONTEXT MENU
    // =========================================================================

    // UA: Виділяємо рядок при правому кліку перед показом меню
    // EN: Select row on right-click before showing context menu
    private void GridEntries_PreviewMouseRightButtonDown(object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        var row = FindVisualParent<DataGridRow>((DependencyObject)e.OriginalSource);
        if (row?.Item is EntryRow)
        {
            row.IsSelected = true;
            GridEntries.CurrentItem = row.Item;
        }
    }

    private void CtxCopyOriginal_Click(object sender, RoutedEventArgs e)
    {
        if (GridEntries.SelectedItem is EntryRow row)
            Clipboard.SetText(row.Original);
    }

    private void CtxPasteAsTranslation_Click(object sender, RoutedEventArgs e)
    {
        if (GridEntries.SelectedItem is EntryRow row)
        {
            row.Translation = row.Original;
            _view?.Refresh();
            UpdateProgressDisplay();
        }
    }

    private void CtxClearTranslation_Click(object sender, RoutedEventArgs e)
    {
        if (GridEntries.SelectedItem is EntryRow row)
        {
            row.Translation = null;
            _view?.Refresh();
            UpdateProgressDisplay();
        }
    }

    private void CtxCopyHash_Click(object sender, RoutedEventArgs e)
    {
        if (GridEntries.SelectedItem is EntryRow row)
            Clipboard.SetText(row.HashDisplay);
    }

    // UA: Знаходить батьківський елемент заданого типу у візуальному дереві
    // EN: Finds parent element of given type in the visual tree
    private static T? FindVisualParent<T>(DependencyObject child)
        where T : DependencyObject
    {
        var current = child;
        while (current != null)
        {
            if (current is T result) return result;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    // =========================================================================
    // UA: ЗАКРИТТЯ ВІКНА / EN: WINDOW CLOSING
    // =========================================================================

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _autoSave.Stop();
        _autoSave.Dispose();
        SimpleLogger.Info("BF1LocalizationTool closed");
    }

    // =========================================================================
    // UA: ВНУТРІШНІ МЕТОДИ / EN: INTERNAL METHODS
    // =========================================================================

    private void PopulateLanguageSelectors()
    {
        CmbSourceLang.SelectionChanged -= CmbSourceLang_SelectionChanged;
        CmbTargetLang.SelectionChanged -= CmbTargetLang_SelectionChanged;

        var langs = _service.AvailableLanguages;
        CmbSourceLang.ItemsSource = null;
        CmbTargetLang.ItemsSource = null;
        CmbSourceLang.ItemsSource = langs;
        CmbTargetLang.ItemsSource = langs;

        var list      = langs.ToList();
        var sourceIdx = list.FindIndex(l => l.Equals("english",    StringComparison.OrdinalIgnoreCase));
        var targetIdx = list.FindIndex(l => l.Equals("uk_english", StringComparison.OrdinalIgnoreCase));

        CmbSourceLang.SelectedIndex = sourceIdx >= 0 ? sourceIdx : 0;
        CmbTargetLang.SelectedIndex = targetIdx >= 0 ? targetIdx : Math.Max(0, list.Count - 1);

        if (CmbSourceLang.SelectedItem is string src) _sourceLang = src;
        if (CmbTargetLang.SelectedItem is string tgt) _targetLang = tgt;

        CmbSourceLang.IsEnabled = true;
        CmbTargetLang.IsEnabled = true;

        CmbSourceLang.SelectionChanged += CmbSourceLang_SelectionChanged;
        CmbTargetLang.SelectionChanged += CmbTargetLang_SelectionChanged;
    }

    private void RefreshGrid()
    {
        var sourceFile = _service.GetLanguageFile(_sourceLang);
        var targetFile = _service.GetLanguageFile(_targetLang);
        if (sourceFile is null) return;

        _allRows = new ObservableCollection<EntryRow>(
            sourceFile.Entries.Select(e =>
            {
                var targetEntry = targetFile?.GetByHash(e.Hash);
                // UA: Якщо оригінал і ціль — одна мова, переклад порожній (редагування з нуля)
                // EN: If source and target are same language, translation is empty (edit from scratch)
                var translation = _sourceLang == _targetLang
                    ? null
                    : targetEntry?.Translation ?? targetEntry?.Original;

                var row = new EntryRow
                {
                    Hash        = e.Hash,
                    Original    = e.Original,
                    IsTechnical = TechnicalStringService.IsTechnical(e.Original)
                };
                row.SetLoadedTranslation(translation);
                return row;
            }));

        _view = CollectionViewSource.GetDefaultView(_allRows);
        _view.Filter = FilterRow;
        GridEntries.ItemsSource = _view;
        GridEntries.FontSize    = _gridFontSize;

        UpdateProgressDisplay();
        BtnAutoMarkTechnical.IsEnabled = true;
    }

    private void ApplyRowEditsToService()
    {
        var targetFile = _service.GetLanguageFile(_targetLang);
        if (targetFile is null) return;

        foreach (var row in _allRows)
        {
            var entry = targetFile.GetByHash(row.Hash);
            if (entry is not null)
                entry.Translation = string.IsNullOrWhiteSpace(row.Translation)
                    ? null : row.Translation;
        }
    }

    private string? BuildCsvContent()
    {
        if (_allRows.Count == 0) return null;
        var sb = new StringBuilder();
        sb.AppendLine("Hash,Original,Translation");
        foreach (var row in _allRows)
        {
            var orig = row.Original.Replace("\"", "\"\"");
            var tran = (row.Translation ?? string.Empty).Replace("\"", "\"\"");
            sb.AppendLine($"0x{row.Hash:x8},\"{orig}\",\"{tran}\"");
        }
        return sb.ToString();
    }

    private void UpdateProgressDisplay()
    {
        // UA: Guard — метод може викликатись до завершення InitializeComponent()
        // EN: Guard — method may be called before InitializeComponent() completes
        if (TxtProgress is null || TxtValidationWarnings is null || TxtFilterCount is null)
            return;

        var total       = _allRows.Count;
        var technical   = _allRows.Count(r => r.IsTechnical);
        var toTranslate = total - technical;
        var translated  = _allRows.Count(r => r.IsTranslated && !r.IsTechnical);
        var edits       = _allRows.Count(r => r.IsModified);
        var warnings    = _allRows.Count(r => !r.IsValid && !r.IsTechnical);
        var pct         = toTranslate == 0 ? 0.0 : (double)translated / toTranslate * 100;

        // UA: Лічильники у кнопках фільтрів — як в EaW
        // EN: Counts in filter buttons — like EaW
        var untranslated = _allRows.Count(r => !r.IsTranslated && !r.IsTechnical);
        RbAll.Content          = $"UA: Усі / EN: All · {total}";
        RbUntranslated.Content = $"UA: Без пер. / EN: Untranslated · {untranslated}";
        RbTranslated.Content   = $"UA: Перекл. / EN: Translated · {translated}";
        RbTechnical.Content    = $"⚙ UA: Техн. / EN: Technical · {technical}";
        RbInvalid.Content      = $"⚠ UA: Проблемні / EN: Issues · {warnings}";

        // UA: Основний лічильник у статус-барі
        // EN: Main counter in status bar
        TxtProgress.Text = $"{translated}/{toTranslate} · {pct:F1}%  |  UA:{edits} правок / EN:{edits} edits";

        if (warnings > 0)
        {
            TxtValidationWarnings.Text       = $"⚠ {warnings}";
            TxtValidationWarnings.Visibility = Visibility.Visible;
        }
        else
            TxtValidationWarnings.Visibility = Visibility.Collapsed;

        var visible = (_view as ListCollectionView)?.Count ?? _allRows.Count;
        TxtFilterCount.Text = $"UA: Показано / EN: Showing: {visible}";
    }

    private void SetStatus(string msg) => TxtStatus.Text = msg;

    private void SetBusy(bool busy, string? statusMsg = null)
    {
        IsEnabled = !busy;
        Cursor    = busy ? System.Windows.Input.Cursors.Wait : null;
        if (statusMsg is not null) SetStatus(statusMsg);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        BtnSave.IsEnabled      = enabled;
        BtnExportCsv.IsEnabled = enabled;
        BtnImportCsv.IsEnabled = enabled;
    }
}

// =============================================================================
// UA: Рядок DataGrid
// EN: DataGrid row
// =============================================================================
public class EntryRow : INotifyPropertyChanged
{
    public uint   Hash        { get; init; }
    public string HashDisplay => $"0x{Hash:x8}";
    public string Original    { get; init; } = string.Empty;

    private string? _translation;
    private string? _originalTranslation; // UA: значення при завантаженні / EN: value at load time

    public string? Translation
    {
        get => _translation;
        set
        {
            if (_translation == value) return;
            _translation = value;
            OnPropertyChanged(nameof(Translation));
            OnPropertyChanged(nameof(TranslationDisplay));
            OnPropertyChanged(nameof(IsTranslated));
            OnPropertyChanged(nameof(IsModified));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(StatusTooltip));
            Validate();
        }
    }

    // UA: Текст що відображається в колонці перекладу:
    //     технічні — повідомлення, інші — сам переклад
    // EN: Text shown in translation column:
    //     technical — message, others — translation itself
    public string TranslationDisplay =>
        IsTechnical
            ? "⚙ UA: Технічний — не перекладати! / EN: Technical — do not translate!"
            : (Translation ?? string.Empty);

    // UA: true якщо переклад змінився після завантаження
    // EN: true if translation changed since load
    public bool IsModified => _translation != _originalTranslation;

    public void SetLoadedTranslation(string? value)
    {
        _translation         = value;
        _originalTranslation = value;
        OnPropertyChanged(nameof(Translation));
        OnPropertyChanged(nameof(TranslationDisplay));
        OnPropertyChanged(nameof(IsTranslated));
        OnPropertyChanged(nameof(IsModified));
        Validate();
    }

    private bool _isTechnical;
    public bool IsTechnical
    {
        get => _isTechnical;
        set
        {
            if (_isTechnical == value) return;
            _isTechnical = value;
            OnPropertyChanged(nameof(IsTechnical));
            OnPropertyChanged(nameof(TranslationDisplay));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(StatusTooltip));
        }
    }

    public bool IsTranslated => !string.IsNullOrWhiteSpace(Translation);

    // UA: Результат валідації
    // EN: Validation result
    private bool   _isValid = true;
    private string _validationMessage = string.Empty;

    public bool   IsValid           => _isValid;
    public bool   HasValidationError => !_isValid;
    public string ValidationMessage  => _validationMessage;

    public void Validate()
    {
        var result = ValidationService.Validate(Original, Translation);
        _isValid           = result.IsValid;
        _validationMessage = result.Message;
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(HasValidationError));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(StatusColor));
    }

    // UA: Колір статусного кружечка
    // EN: Status dot color
    public Brush StatusColor
    {
        get
        {
            if (!_isValid)    return new SolidColorBrush(Color.FromRgb(0xE6, 0xA8, 0x32)); // warning
            if (IsTechnical)  return new SolidColorBrush(Color.FromRgb(0x7A, 0x9E, 0xB5)); // technical
            if (IsTranslated) return new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x7D)); // translated
            return new SolidColorBrush(Color.FromRgb(0xCF, 0x66, 0x79));                   // untranslated
        }
    }

    public string StatusTooltip =>
        !_isValid    ? $"UA: Проблема / EN: Issue: {_validationMessage}" :
        IsTechnical  ? "UA: Технічний / EN: Technical" :
        IsTranslated ? "UA: Перекладено / EN: Translated" :
                       "UA: Не перекладено / EN: Untranslated";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
