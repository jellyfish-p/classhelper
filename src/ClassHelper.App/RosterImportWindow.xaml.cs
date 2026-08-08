using System.Windows;
using System.Windows.Controls;
using ClassHelper.Core.RosterImport;

namespace ClassHelper.App;

public partial class RosterImportWindow : Window
{
    private readonly RosterImportDocument _document;
    private bool _initialized;

    public RosterImportWindow(RosterImportDocument document)
    {
        _document = document;
        InitializeComponent();

        SourceText.Text = $"{document.SourceName}  ·  {document.SheetName}";
        RecognitionText.Text = document.HeaderRowNumber is { } headerRow
            ? $"已将第 {headerRow} 行识别为表头，可在上方修正列映射"
            : "未识别到明确表头，已根据内容推断列映射";

        NameColumnBox.ItemsSource = document.Columns;
        var optionalColumns = new[] { new RosterImportColumn(-1, "不导入") }.Concat(document.Columns).ToList();
        NumberColumnBox.ItemsSource = optionalColumns;
        ActiveColumnBox.ItemsSource = optionalColumns;

        NameColumnBox.SelectedValue = document.SuggestedNameColumnIndex;
        NumberColumnBox.SelectedValue = document.SuggestedNumberColumnIndex ?? -1;
        ActiveColumnBox.SelectedValue = document.SuggestedActiveColumnIndex ?? -1;

        _initialized = true;
        UpdatePreview();
    }

    public RosterImportResult? Result { get; private set; }

    private void Mapping_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initialized)
        {
            UpdatePreview();
        }
    }

    private void UpdatePreview()
    {
        if (NameColumnBox.SelectedValue is not int nameColumn)
        {
            Result = null;
            PreviewGrid.ItemsSource = null;
            CountText.Text = "请选择姓名列";
            ConfirmButton.IsEnabled = false;
            return;
        }

        var numberColumn = GetOptionalColumn(NumberColumnBox);
        var activeColumn = GetOptionalColumn(ActiveColumnBox);
        Result = RosterImportMapper.Map(_document, nameColumn, numberColumn, activeColumn);
        PreviewGrid.ItemsSource = Result.Members;
        CountText.Text = $"将导入 {Result.Members.Count} 人";
        WarningText.Text = Result.Warnings.Count == 0
            ? string.Empty
            : string.Join("；", Result.Warnings);
        ConfirmButton.Content = $"导入 {Result.Members.Count} 人";
        ConfirmButton.IsEnabled = Result.Members.Count > 0;
    }

    private static int? GetOptionalColumn(ComboBox comboBox) =>
        comboBox.SelectedValue is int { } index && index >= 0 ? index : null;

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (Result is not { Members.Count: > 0 })
        {
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
