// =============================================================================
// BF1LocalizationTool.GUI — TranslationConflictWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Модальний діалог для ручного вирішення конфліктів перенесення
//     перекладу між іграми (MainWindow.BtnTransferFromOtherGame_Click →
//     CrossGameTranslationTransfer.BuildPlan). Один рядок — один
//     англійський оригінал, для якого в донорі знайдено КІЛЬКА різних
//     українських перекладів; список варіантів завжди починається з
//     SkipMarker (типово обраний), щоб конфлікт, лишений без уваги, НЕ
//     переносився мовчки.
//
//     Викликач читає Resolutions ЛИШЕ якщо ShowDialog() повернув true —
//     Cancel закриває вікно без DialogResult=true, і виклик Resolutions
//     після Cancel не передбачений (MainWindow перевіряє результат перед
//     використанням).
// EN: Modal dialog for manually resolving cross-game translation-transfer
//     conflicts (MainWindow.BtnTransferFromOtherGame_Click →
//     CrossGameTranslationTransfer.BuildPlan). One row = one English
//     original for which the donor has SEVERAL distinct Ukrainian
//     translations; the option list always starts with SkipMarker
//     (selected by default), so a conflict left untouched is NOT
//     silently transferred.
//
//     The caller reads Resolutions ONLY if ShowDialog() returned true —
//     Cancel closes the window without DialogResult=true, and reading
//     Resolutions after Cancel isn't expected (MainWindow checks the
//     result before using it).
// =============================================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace BF1LocalizationTool.GUI;

public partial class TranslationConflictWindow : Window
{
    public const string SkipMarker = "— UA: пропустити / EN: skip —";

    public ObservableCollection<ConflictRow> Rows { get; } = [];

    // UA: Заповнюється лише в BtnApply_Click — саме тому OK читати після
    //     ShowDialog() == true.
    // EN: Populated only in BtnApply_Click — that's why it's safe to read
    //     after ShowDialog() == true.
    public IReadOnlyDictionary<string, string> Resolutions { get; private set; } =
        new Dictionary<string, string>();

    public TranslationConflictWindow(IReadOnlyDictionary<string, IReadOnlyList<string>> conflicts)
    {
        InitializeComponent();

        foreach (var (original, candidates) in conflicts.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var options = new List<string> { SkipMarker };
            options.AddRange(candidates);
            Rows.Add(new ConflictRow
            {
                Original = original,
                Options  = options,
                Selected = SkipMarker
            });
        }

        GridConflicts.ItemsSource = Rows;
        TxtSummary.Text =
            $"UA: Конфліктних рядків: {Rows.Count} / EN: Conflicting strings: {Rows.Count}";
    }

    private void BtnSkipAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in Rows)
            row.Selected = SkipMarker;
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        var resolutions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in Rows)
        {
            if (row.Selected != SkipMarker)
                resolutions[row.Original] = row.Selected;
        }

        Resolutions   = resolutions;
        DialogResult  = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

// UA: Один рядок таблиці конфліктів — англійський оригінал + список
//     варіантів (перший завжди SkipMarker) + обраний варіант.
// EN: One row of the conflicts grid — English original + list of options
//     (the first is always SkipMarker) + the chosen option.
public class ConflictRow : INotifyPropertyChanged
{
    public required string Original { get; init; }
    public required List<string> Options { get; init; }

    private string _selected = TranslationConflictWindow.SkipMarker;
    public string Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
