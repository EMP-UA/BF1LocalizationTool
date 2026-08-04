// =============================================================================
// BF1LocalizationTool.GUI — CompareWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Порівняння ДВОХ core.lvl за текстовими рядками локалізації (по хешу,
//     не по позиції — порядок записів між файлами може відрізнятись).
//     Призначення:
//       1. Цілісність: підтвердити, що шрифтова ін'єкція кирилиці
//          (GenerateLocalizedCoreCommand) НЕ зачепила жодного тексту —
//          "Файл A" (оригінал) і "Файл B" (шрифто-патчений вивід) мають
//          дати 0 відмінностей по ВСІХ мовах.
//       2. Візуальний огляд: побачити КОНКРЕТНО які рядки відрізняються
//          між двома білдами/версіями (майбутні порівняння версій моду),
//          в тих самих двох колонках, що й звична пара Оригінал/Переклад
//          у MainWindow — лише тепер це Файл A/Файл B.
//     Технічні рядки (TechnicalStringService, той самий сервіс що й у
//     MainWindow — не дублюється) позначені окремо, щоб відрізняти
//     "щось спотворилось у службовому рядку" від "змінився текст, що
//     реально бачить гравець".
// EN: Compares TWO core.lvl files by localization text (by hash, not by
//     position — entry order can differ between files).
//     Purpose:
//       1. Integrity: confirm that Cyrillic font injection
//          (GenerateLocalizedCoreCommand) touched NO text — "File A"
//          (original) and "File B" (font-patched output) should show 0
//          differences across ALL languages.
//       2. Visual review: see EXACTLY which strings differ between two
//          builds/versions (future mod-version comparisons), in the same
//          two-column layout as the familiar Original/Translation pair in
//          MainWindow — just now it's File A/File B.
//     Technical strings (TechnicalStringService, the SAME service used by
//     MainWindow — not duplicated) are flagged separately, to tell "a
//     service string got corrupted" apart from "text the player actually
//     sees changed".
// =============================================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using BF1LocalizationTool.Core.Localization;
using BF1LocalizationTool.GUI.Services;
using Microsoft.Win32;

namespace BF1LocalizationTool.GUI;

public partial class CompareWindow : Window
{
    private readonly LvlLocalizationService _serviceA = new();
    private readonly LvlLocalizationService _serviceB = new();

    private string? _pathA;
    private string? _pathB;

    private ObservableCollection<CompareRow> _rows = [];
    private ICollectionView? _view;

    public CompareWindow()
    {
        InitializeComponent();
    }

    // =========================================================================
    // UA: ВІДКРИТТЯ ФАЙЛІВ / EN: OPENING FILES
    // =========================================================================

    private async void BtnOpenA_Click(object sender, RoutedEventArgs e) =>
        await OpenFile(isFileA: true);

    private async void BtnOpenB_Click(object sender, RoutedEventArgs e) =>
        await OpenFile(isFileA: false);

    private async Task OpenFile(bool isFileA)
    {
        var dlg = new OpenFileDialog
        {
            Title    = isFileA
                ? "UA: Файл A (оригінал) / EN: File A (original)"
                : "UA: Файл B (змінений) / EN: File B (modified)",
            Filter   = "LVL files (*.lvl)|*.lvl|All files (*.*)|*.*",
            FileName = "core.lvl"
        };
        if (dlg.ShowDialog() != true) return;

        TxtSummary.Text = "UA: Завантаження... / EN: Loading...";

        try
        {
            if (isFileA)
            {
                await _serviceA.LoadAsync(dlg.FileName);
                _pathA = dlg.FileName;
                TxtPathA.Text = dlg.FileName;
            }
            else
            {
                await _serviceB.LoadAsync(dlg.FileName);
                _pathB = dlg.FileName;
                TxtPathB.Text = dlg.FileName;
            }

            SimpleLogger.Info($"Compare: loaded {(isFileA ? "A" : "B")}: {dlg.FileName}");
            PopulateLanguages();
        }
        catch (Exception ex)
        {
            SimpleLogger.Error("Compare: load failed", ex);
            System.Windows.MessageBox.Show(
                $"UA: Помилка завантаження:\n{ex.Message}\n\nEN: Load error:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            TxtSummary.Text = "UA: Помилка завантаження / EN: Load error";
        }
    }

    // =========================================================================
    // UA: МОВНИЙ СЕЛЕКТОР — лише мови, наявні В ОБОХ файлах / EN: LANGUAGE
    //     SELECTOR — only languages present in BOTH files
    // =========================================================================

    private void PopulateLanguages()
    {
        if (_pathA is null || _pathB is null) return;

        CmbLang.SelectionChanged -= CmbLang_SelectionChanged;

        var common = _serviceA.AvailableLanguages
            .Intersect(_serviceB.AvailableLanguages, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        CmbLang.ItemsSource = common;

        var idx = common.FindIndex(l => l.Equals("english", StringComparison.OrdinalIgnoreCase));
        CmbLang.SelectedIndex = idx >= 0 ? idx : (common.Count > 0 ? 0 : -1);
        CmbLang.IsEnabled = common.Count > 0;

        CmbLang.SelectionChanged += CmbLang_SelectionChanged;

        if (common.Count == 0)
        {
            TxtSummary.Text =
                "UA: Немає спільних мов між файлами A і B — порівнювати нічого. / " +
                "EN: No common languages between files A and B — nothing to compare.";
            return;
        }

        RunComparison();
    }

    private void CmbLang_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RunComparison();

    // =========================================================================
    // UA: ПОРІВНЯННЯ — по (ХЕШУ, ПОРЯДКОВОМУ НОМЕРУ серед записів з тим
    //     самим хешем), не просто по хешу. ЕМПІРИЧНО ПІДТВЕРДЖЕНО
    //     (побайтовий парсинг core.lvl, обидві гри): той самий 32-бітний
    //     хеш навмисно використовується ДЛЯ ДВОХ РІЗНИХ рядків тексту
    //     (напр. BF1 english 0xf2e2b10d = і "SQUAD COMMANDS", і
    //     "SQUAD\r\nCOMMANDS" — короткий/переносний варіант того самого
    //     UI-елемента; 53 таких пари лише в BF1 english). Порівняння
    //     ЛИШЕ по хешу (Distinct на хешах, GetByHash без ordinal) мовчки
    //     схлопнуло б обидва варіанти в один рядок.
    // EN: COMPARISON — by (HASH, ORDINAL position among entries sharing
    //     that same hash), not hash alone. EMPIRICALLY CONFIRMED (byte-
    //     level parsing of core.lvl, both games): the same 32-bit hash is
    //     deliberately used for TWO DIFFERENT strings (e.g. BF1 english
    //     0xf2e2b10d = both "SQUAD COMMANDS" and "SQUAD\r\nCOMMANDS" —
    //     short/wrapped variant of the same UI element; 53 such pairs in
    //     BF1 english alone). Comparing by hash ALONE (Distinct on
    //     hashes, GetByHash without ordinal) would silently collapse both
    //     variants into one row.
    // =========================================================================

    private void RunComparison()
    {
        if (CmbLang.SelectedItem is not string lang) return;

        var fileA = _serviceA.GetLanguageFile(lang);
        var fileB = _serviceB.GetLanguageFile(lang);
        if (fileA is null || fileB is null) return;

        var allKeys = BuildOrdinalKeys(fileA).Union(BuildOrdinalKeys(fileB)).Distinct();

        var rows = new List<CompareRow>();
        foreach (var (hash, ordinal) in allKeys)
        {
            var a = fileA.GetByHash(hash, ordinal);
            var b = fileB.GetByHash(hash, ordinal);

            string status;
            if (a is null) status = "MissingA";
            else if (b is null) status = "MissingB";
            else status = a.Original == b.Original ? "Identical" : "Different";

            // UA: Технічність визначаємо з того, що реально є (перевага —
            //     файл A, як "джерело істини"); той самий сервіс, що в MainWindow.
            // EN: Technicality is determined from whatever text is actually
            //     present (preferring file A as the "source of truth"); the
            //     SAME service used by MainWindow.
            var textForTechCheck = a?.Original ?? b?.Original ?? string.Empty;

            rows.Add(new CompareRow
            {
                Hash        = hash,
                Ordinal     = ordinal,
                TextA       = a?.Original,
                TextB       = b?.Original,
                IsTechnical = TechnicalStringService.IsTechnical(textForTechCheck),
                Status      = status
            });
        }

        _rows = new ObservableCollection<CompareRow>(rows.OrderBy(r => r.Hash).ThenBy(r => r.Ordinal));
        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = FilterRow;
        GridCompare.ItemsSource = _view;

        UpdateSummary();
    }

    // UA: Для кожного запису файлу — (хеш, номер входження серед записів
    //     з тим самим хешем, у порядку файлу).
    // EN: For each entry in the file — (hash, occurrence number among
    //     entries sharing that same hash, in file order).
    private static List<(uint Hash, int Ordinal)> BuildOrdinalKeys(
        BF1LocalizationTool.Core.Models.LocalizationFile file)
    {
        var counter = new Dictionary<uint, int>();
        var keys = new List<(uint, int)>();
        foreach (var entry in file.Entries)
        {
            var ordinal = counter.TryGetValue(entry.Hash, out var n) ? n : 0;
            counter[entry.Hash] = ordinal + 1;
            keys.Add((entry.Hash, ordinal));
        }
        return keys;
    }

    // =========================================================================
    // UA: ФІЛЬТРИ / EN: FILTERS
    // =========================================================================

    private bool FilterRow(object obj)
    {
        if (obj is not CompareRow row) return false;

        if (RbDiffOnly.IsChecked == true && row.Status == "Identical") return false;
        if (RbMissingOnly.IsChecked == true && row.Status is not ("MissingA" or "MissingB")) return false;

        var search = TxtSearch.Text?.Trim();
        if (string.IsNullOrEmpty(search)) return true;

        return row.TextA?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
            || row.TextB?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
            || row.HashDisplay.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        _view?.Refresh();
        UpdateSummary();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _view?.Refresh();
        UpdateSummary();
    }

    // =========================================================================
    // UA: ЗВЕДЕННЯ — окремо рахує технічні серед відмінних, щоб одразу
    //     бачити, чи "зіпсувалось" щось службове, чи реальний текст.
    // EN: SUMMARY — separately counts technical rows among the differing
    //     ones, to immediately see whether something "broke" in service
    //     strings or in real player-facing text.
    // =========================================================================

    private void UpdateSummary()
    {
        // UA: Guard — RbAll IsChecked="True" в XAML викликає FilterChanged
        //     (а тому й цей метод) ще ПІД ЧАС InitializeComponent(), до
        //     того як TxtSummary (оголошений нижче в дереві) прив'язався
        //     до поля — той самий захист, що й у MainWindow.UpdateProgressDisplay.
        // EN: Guard — RbAll IsChecked="True" in XAML triggers FilterChanged
        //     (and thus this method) DURING InitializeComponent(), before
        //     TxtSummary (declared later in the tree) is wired to its field
        //     — the same guard used in MainWindow.UpdateProgressDisplay.
        if (TxtSummary is null)
            return;

        var total          = _rows.Count;
        var identical      = _rows.Count(r => r.Status == "Identical");
        var different       = _rows.Count(r => r.Status == "Different");
        var missingA       = _rows.Count(r => r.Status == "MissingA");
        var missingB       = _rows.Count(r => r.Status == "MissingB");
        var diffTechnical    = _rows.Count(r => r.Status != "Identical" && r.IsTechnical);
        var diffNonTechnical = _rows.Count(r => r.Status != "Identical" && !r.IsTechnical);
        var visible          = (_view as ListCollectionView)?.Count ?? total;

        TxtSummary.Text =
            $"UA: Всього {total} · Однакові {identical} · Відмінні {different} " +
            $"(техн. {diffTechnical} / реальний текст {diffNonTechnical}) · " +
            $"Лише в A {missingA} · Лише в B {missingB} · Показано {visible}\n" +
            $"EN: Total {total} · Identical {identical} · Different {different} " +
            $"(tech {diffTechnical} / real text {diffNonTechnical}) · " +
            $"Only in A {missingA} · Only in B {missingB} · Showing {visible}";

        if (total > 0 && identical == total)
            TxtSummary.Text += "\nUA: ✓ Файли ІДЕНТИЧНІ за текстом цієї мови. / EN: ✓ Files are IDENTICAL in this language's text.";
    }
}

// =============================================================================
// UA: Рядок порівняння
// EN: Comparison row
// =============================================================================
public class CompareRow
{
    public uint   Hash        { get; init; }

    // UA: Порядковий номер серед записів з ТИМ САМИМ хешем (0 = перший).
    //     ОБОВ'ЯЗКОВИЙ — див. коментар над RunComparison: один хеш може
    //     належати ДВОМ різним рядкам тексту.
    // EN: Position among entries sharing the SAME hash (0 = first).
    //     REQUIRED — see the comment above RunComparison: one hash can
    //     belong to TWO different strings.
    public int    Ordinal     { get; init; }
    public string HashDisplay => Ordinal == 0 ? $"0x{Hash:x8}" : $"0x{Hash:x8} (#{Ordinal + 1})";
    public string? TextA      { get; init; }
    public string? TextB      { get; init; }
    public bool   IsTechnical { get; init; }

    // UA: "Identical" | "Different" | "MissingA" (є лише в B) | "MissingB" (є лише в A)
    // EN: "Identical" | "Different" | "MissingA" (only in B) | "MissingB" (only in A)
    public string Status { get; init; } = string.Empty;

    public Brush StatusColor => Status switch
    {
        "Identical" => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x7D)),
        "Different" => new SolidColorBrush(Color.FromRgb(0xCF, 0x66, 0x79)),
        _           => new SolidColorBrush(Color.FromRgb(0xE6, 0xA8, 0x32)), // MissingA/MissingB
    };

    public string StatusLabel => Status switch
    {
        "Identical" => "= UA: Однаково / EN: Identical",
        "Different" => "≠ UA: Відрізняється / EN: Different",
        "MissingA"  => "→B UA: Є лише у файлі B / EN: Only present in file B",
        "MissingB"  => "→A UA: Є лише у файлі A / EN: Only present in file A",
        _           => Status
    };
}
