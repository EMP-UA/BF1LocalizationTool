// =============================================================================
// BF1LocalizationTool.GUI — ReviewMarkPromptWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Модальне вікно-запит одного рядка тексту для масового проставлення
//     ДОВІЛЬНОГО тексту вичитки (не готової позначки +/-/+/-) кільком
//     виділеним рядкам одразу — пункт контекстного меню MainWindow
//     «Вичитка для виділених → Власний текст…».
//
//     Викликач читає ReviewText ЛИШЕ якщо ShowDialog() повернув true.
//     Порожній текст — дозволений і означає очищення позначки.
// EN: A modal single-line text prompt for bulk-setting a FREE-FORM review
//     text (not a ready-made +/-/+/- mark) on several selected rows at once
//     — the MainWindow context-menu item "Review for selected → Custom
//     text…".
//
//     The caller reads ReviewText ONLY if ShowDialog() returned true. An
//     empty text is allowed and means clearing the mark.
// =============================================================================

using System.Windows;

namespace BF1LocalizationTool.GUI;

public partial class ReviewMarkPromptWindow : Window
{
    // UA: Введений текст (обрізаний по краях) — заповнюється лише якщо
    //     DialogResult == true.
    // EN: The entered text (trimmed) — populated only if DialogResult == true.
    public string ReviewText { get; private set; } = string.Empty;

    // UA: selectedCount — кількість виділених рядків, показується в
    //     підказці; initialText — стартове значення поля (спільна позначка
    //     виділених рядків, якщо вона в усіх однакова).
    // EN: selectedCount — the number of selected rows, shown in the hint;
    //     initialText — the field's starting value (the selected rows'
    //     common mark, if they all share one).
    public ReviewMarkPromptWindow(int selectedCount, string initialText)
    {
        InitializeComponent();

        TxtSelectionInfo.Text =
            $"UA: Виділено рядків: {selectedCount} / EN: Selected rows: {selectedCount}";
        TxtReviewText.Text = initialText;
        TxtReviewText.SelectAll();
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        ReviewText = (TxtReviewText.Text ?? string.Empty).Trim();
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
