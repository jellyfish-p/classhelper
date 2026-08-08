using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ClassHelper.App.Services;
using ClassHelper.App.ViewModels;
using ClassHelper.Core.RollCall;
using ClassHelper.Core.RosterImport;
using ClassHelper.Core.Updates;
using Microsoft.Win32;

namespace ClassHelper.App;

public partial class MainWindow : Window
{
    private readonly AppController _controller;
    private readonly MainViewModel _viewModel;
    private readonly GitHubUpdateService _updateService = new();
    private UpdateCheckResult? _availableUpdate;
    private bool _allowClose;
    private bool _automaticUpdateCheckStarted;
    private bool _settingsInitialized;

    public MainWindow(AppController controller, MainViewModel viewModel)
    {
        _controller = controller;
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        AutoStartCheckBox.IsChecked = AutoStartService.IsEnabled();
        UpdateChannelComboBox.ItemsSource = CreateUpdateChannelChoices();
        UpdateChannelComboBox.SelectedValue = _viewModel.UpdateChannel;
        AutoCheckUpdatesCheckBox.IsChecked = _viewModel.CheckUpdatesOnStartup;
        UpdateStatusText.Text = $"当前版本 {_viewModel.CurrentVersionDisplay} · {AppBuildInfo.Runtime} · {DeploymentLabel()}";
        Loaded += MainWindow_Loaded;
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

    protected override void OnClosed(EventArgs e)
    {
        _updateService.Dispose();
        base.OnClosed(e);
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

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_automaticUpdateCheckStarted || !_viewModel.CheckUpdatesOnStartup)
        {
            return;
        }

        _automaticUpdateCheckStarted = true;
        await CheckForUpdatesAsync();
    }

    private async void UpdateSettings_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_settingsInitialized || UpdateChannelComboBox.SelectedValue is not UpdateChannel channel)
        {
            return;
        }

        _viewModel.UpdateChannel = channel;
        _availableUpdate = null;
        DownloadUpdateButton.Visibility = Visibility.Collapsed;
        UpdateStatusText.Text = "更新通道已更改，点击“检查更新”刷新结果";
        await SaveUpdatePreferencesAsync("更新通道已保存");
    }

    private async void AutoCheckUpdates_Changed(object sender, RoutedEventArgs e)
    {
        if (!_settingsInitialized)
        {
            return;
        }

        _viewModel.CheckUpdatesOnStartup = AutoCheckUpdatesCheckBox.IsChecked == true;
        await SaveUpdatePreferencesAsync(_viewModel.CheckUpdatesOnStartup
            ? "已启用启动时检查更新"
            : "已关闭启动时检查更新");
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e) =>
        await CheckForUpdatesAsync();

    private void DownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_availableUpdate is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_availableUpdate.Asset.DownloadUrl) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"无法打开更新下载：{exception.Message}";
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        CheckForUpdatesButton.IsEnabled = false;
        DownloadUpdateButton.Visibility = Visibility.Collapsed;
        UpdateStatusText.Text = "正在连接 GitHub 检查更新…";

        try
        {
            _availableUpdate = await _updateService.CheckAsync(
                _viewModel.UpdateChannel,
                CancellationToken.None);

            if (_availableUpdate is null)
            {
                UpdateStatusText.Text = $"当前 {_viewModel.CurrentVersionDisplay} 已是所选通道的最新兼容版本";
                _viewModel.StatusMessage = "没有发现可用更新";
                return;
            }

            UpdateStatusText.Text = $"发现 v{_availableUpdate.Version}，已匹配 {AppBuildInfo.Runtime} · {DeploymentLabel()}";
            DownloadUpdateButton.Visibility = Visibility.Visible;
            _viewModel.StatusMessage = $"发现新版本 v{_availableUpdate.Version}";
        }
        catch (Exception exception)
        {
            UpdateStatusText.Text = "暂时无法检查更新，请稍后重试";
            _viewModel.StatusMessage = $"检查更新失败：{exception.Message}";
        }
        finally
        {
            CheckForUpdatesButton.IsEnabled = true;
        }
    }

    private async Task SaveUpdatePreferencesAsync(string successMessage)
    {
        try
        {
            await _viewModel.SaveUpdatePreferencesAsync();
            _viewModel.StatusMessage = successMessage;
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"更新设置保存失败：{exception.Message}";
        }
    }

    private static UpdateChannelChoice[] CreateUpdateChannelChoices() =>
    [
        new(UpdateChannel.Alpha, "Alpha · 包含全部后续稳定层级"),
        new(UpdateChannel.Beta, "Beta · 包含 Beta、预发行和稳定版"),
        new(UpdateChannel.Prerelease, "预发行 · 包含 RC、Preview 和稳定版"),
        new(UpdateChannel.Stable, "Stable · 仅稳定版")
    ];

    private static string DeploymentLabel() => AppBuildInfo.Deployment == UpdateDeployment.SelfContained
        ? "内含 .NET"
        : "依赖 .NET";

    private sealed record UpdateChannelChoice(UpdateChannel Value, string Label);
}
