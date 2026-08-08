using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ClassHelper.App.Services;
using ClassHelper.App.ViewModels;
using ClassHelper.Core.RollCall;
using ClassHelper.Core.RosterImport;
using Microsoft.Win32;

namespace ClassHelper.App;

public partial class MainWindow : Window
{
    private readonly AppController _controller;
    private readonly MainViewModel _viewModel;
    private bool _allowClose;
    private bool _settingsInitialized;

    public MainWindow(AppController controller, MainViewModel viewModel)
    {
        _controller = controller;
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        AutoStartCheckBox.IsChecked = AutoStartService.IsEnabled();
        _settingsInitialized = true;
    }

    public void NavigateTo(int pageIndex)
    {
        pageIndex = Math.Clamp(pageIndex, 0, 4);
        MainTabs.SelectedIndex = pageIndex;

        var selectedNavigation = pageIndex switch
        {
            0 => OverviewNav,
            1 => RollCallNav,
            2 => RosterNav,
            3 => SettingsNav,
            4 => AboutNav,
            _ => OverviewNav
        };
        selectedNavigation.IsChecked = true;
    }

    public void AllowClose() => _allowClose = true;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && int.TryParse(tag, out var pageIndex))
        {
            NavigateTo(pageIndex);
        }
    }

    private void StartIndependent_Click(object sender, RoutedEventArgs e) =>
        _controller.ShowRollCall(RollCallMode.IndependentRandom);

    private void StartBalanced_Click(object sender, RoutedEventArgs e) =>
        _controller.ShowRollCall(RollCallMode.BalancedRound);

    private void AddMember_Click(object sender, RoutedEventArgs e) => _viewModel.AddRosterMember();

    private async void SaveRoster_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var originalContent = button.Content;
        button.IsEnabled = false;
        button.Content = "正在保存";

        try
        {
            await _viewModel.SaveRosterAsync();
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"名单保存失败：{exception.Message}";
        }
        finally
        {
            button.Content = originalContent;
            button.IsEnabled = true;
        }
    }

    private void PreviewRoster_Click(object sender, RoutedEventArgs e) =>
        _viewModel.ImportRosterText(RosterPasteBox.Text);

    private async void ImportRosterFile_Click(object sender, RoutedEventArgs e)
    {
        var fileDialog = new OpenFileDialog
        {
            Title = "选择名单文件",
            Filter = "名单文件 (*.xlsx;*.csv)|*.xlsx;*.csv|Excel 工作簿 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv",
            CheckFileExists = true,
            Multiselect = false
        };

        if (fileDialog.ShowDialog(this) != true)
        {
            return;
        }

        var button = sender as Button;
        var originalContent = button?.Content;
        if (button is not null)
        {
            button.IsEnabled = false;
            button.Content = "正在识别";
        }

        _viewModel.StatusMessage = $"正在识别 {Path.GetFileName(fileDialog.FileName)}";
        try
        {
            var document = await Task.Run(() => RosterFileImporter.Load(fileDialog.FileName));
            var previewWindow = new RosterImportWindow(document) { Owner = this };
            if (previewWindow.ShowDialog() == true && previewWindow.Result is { } result)
            {
                _viewModel.ImportRosterMembers(result.Members, document.SourceName);
            }
            else
            {
                _viewModel.StatusMessage = "已取消文件导入，当前名单未更改";
            }
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"名单导入失败：{exception.Message}";
            MessageBox.Show(
                this,
                exception.Message,
                "无法导入名单",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            if (button is not null)
            {
                button.Content = originalContent;
                button.IsEnabled = true;
            }
        }
    }

    private void AutoStart_Changed(object sender, RoutedEventArgs e)
    {
        if (!_settingsInitialized)
        {
            return;
        }

        try
        {
            AutoStartService.SetEnabled(AutoStartCheckBox.IsChecked == true);
            _viewModel.StatusMessage = AutoStartCheckBox.IsChecked == true
                ? "已启用开机自启"
                : "已关闭开机自启";
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"无法修改开机自启：{exception.Message}";
        }
    }
}
