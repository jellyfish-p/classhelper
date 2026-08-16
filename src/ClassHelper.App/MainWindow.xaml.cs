using System.ComponentModel;
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
    private readonly UpdateDownloadService _updateDownloadService = new();
    private UpdateCheckResult? _availableUpdate;
    private UpdateDownloadResult? _downloadedUpdate;
    private CancellationTokenSource? _updateDownloadCancellation;
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
        _updateDownloadCancellation?.Cancel();
        _updateDownloadCancellation?.Dispose();
        _updateDownloadService.Dispose();
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

    private void RemoveMember_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RosterMemberRow member })
        {
            _viewModel.RemoveRosterMember(member);
        }
    }

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

    private void GenerateNumberRange_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Roster.Count > 0)
        {
            var confirmation = MessageBox.Show(
                this,
                "生成学号列表会替换当前编辑区中的名单，尚未保存的修改也会被清除。是否继续？",
                "生成学号列表",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }
        }

        if (!_viewModel.GenerateNumberRange(NumberRangeStartTextBox.Text, NumberRangeEndTextBox.Text))
        {
            NumberRangeStartTextBox.Focus();
            NumberRangeStartTextBox.SelectAll();
        }
    }

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
        _updateDownloadCancellation?.Cancel();
        _availableUpdate = null;
        _downloadedUpdate = null;
        DownloadUpdateButton.Visibility = Visibility.Collapsed;
        UpdateDownloadProgressBar.Visibility = Visibility.Collapsed;
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

    private async void DownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_updateDownloadCancellation is not null)
        {
            _updateDownloadCancellation.Cancel();
            return;
        }

        if (_availableUpdate is null)
        {
            return;
        }

        var availableUpdate = _availableUpdate;

        if (_downloadedUpdate is not null && File.Exists(_downloadedUpdate.FilePath))
        {
            ConfirmAndStartInstall(_downloadedUpdate);
            return;
        }

        _updateDownloadCancellation = new CancellationTokenSource();
        DownloadUpdateButton.Content = "取消下载";
        CheckForUpdatesButton.IsEnabled = false;
        UpdateDownloadProgressBar.Visibility = Visibility.Visible;
        UpdateDownloadProgressBar.IsIndeterminate = true;
        var progress = new Progress<UpdateDownloadProgress>(ReportDownloadProgress);

        try
        {
            _downloadedUpdate = await _updateDownloadService.DownloadAsync(
                availableUpdate.Asset,
                availableUpdate.Version,
                progress,
                _updateDownloadCancellation.Token);
            UpdateDownloadProgressBar.IsIndeterminate = false;
            UpdateDownloadProgressBar.Value = 100;
            UpdateStatusText.Text = $"v{availableUpdate.Version} 已下载并通过 SHA-256 校验";
            _viewModel.StatusMessage = "更新下载完成，等待安装";
            DownloadUpdateButton.Content = "安装更新";
            ConfirmAndStartInstall(_downloadedUpdate);
        }
        catch (OperationCanceledException)
        {
            UpdateStatusText.Text = "更新下载已取消";
            _viewModel.StatusMessage = "已取消更新下载";
            DownloadUpdateButton.Content = "重新下载";
            UpdateDownloadProgressBar.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            UpdateStatusText.Text = "更新下载失败，可以稍后重试";
            _viewModel.StatusMessage = $"更新下载失败：{exception.Message}";
            DownloadUpdateButton.Content = "重试下载";
            UpdateDownloadProgressBar.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _updateDownloadCancellation.Dispose();
            _updateDownloadCancellation = null;
            CheckForUpdatesButton.IsEnabled = true;
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        CheckForUpdatesButton.IsEnabled = false;
        DownloadUpdateButton.Visibility = Visibility.Collapsed;
        UpdateDownloadProgressBar.Visibility = Visibility.Collapsed;
        _downloadedUpdate = null;
        UpdateStatusText.Text = "正在检查更新（GitHub 优先，国内镜像备用）…";

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
            DownloadUpdateButton.Content = "下载并安装";
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

    private void ReportDownloadProgress(UpdateDownloadProgress progress)
    {
        var sourceLabel = DownloadSourceLabel(progress.Source);
        var received = FormatBytes(progress.BytesReceived);
        if (progress.TotalBytes is > 0)
        {
            UpdateDownloadProgressBar.IsIndeterminate = false;
            UpdateDownloadProgressBar.Value = Math.Clamp(
                progress.BytesReceived * 100d / progress.TotalBytes.Value,
                0,
                100);
            UpdateStatusText.Text = $"正在从 {sourceLabel} 下载 {received} / {FormatBytes(progress.TotalBytes.Value)}";
        }
        else
        {
            UpdateDownloadProgressBar.IsIndeterminate = true;
            UpdateStatusText.Text = $"正在从 {sourceLabel} 下载 {received}";
        }
    }

    private void ConfirmAndStartInstall(UpdateDownloadResult download)
    {
        if (_availableUpdate is null)
        {
            return;
        }

        var sourceLabel = DownloadSourceLabel(download.Source);
        var confirmation = MessageBox.Show(
            this,
            $"v{_availableUpdate.Version} 已从 {sourceLabel} 下载并通过完整性校验。\n\n" +
            "现在安装吗？课堂助手会短暂关闭，完成替换后自动重新启动。",
            "安装课堂助手更新",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            UpdateStatusText.Text = $"v{_availableUpdate.Version} 已准备好，点击“安装更新”继续";
            DownloadUpdateButton.Content = "安装更新";
            return;
        }

        try
        {
            DownloadUpdateButton.IsEnabled = false;
            DownloadUpdateButton.Content = "正在启动安装";
            UpdateStatusText.Text = "正在启动更新安装程序…";
            UpdateInstaller.StartInstall(download.FilePath, _availableUpdate.Asset.Sha256);
            _controller.Exit();
        }
        catch (Exception exception)
        {
            DownloadUpdateButton.IsEnabled = true;
            DownloadUpdateButton.Content = "安装更新";
            UpdateStatusText.Text = "无法启动更新安装程序";
            _viewModel.StatusMessage = $"安装更新失败：{exception.Message}";
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)Math.Max(0, bytes);
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{value:0} {units[unitIndex]}" : $"{value:0.0} {units[unitIndex]}";
    }

    private static string DownloadSourceLabel(UpdateDownloadSource source) => source switch
    {
        UpdateDownloadSource.GitHub => "GitHub",
        UpdateDownloadSource.OssMirror => "国内镜像",
        UpdateDownloadSource.LocalCache => "本地缓存",
        _ => "未知来源"
    };

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
