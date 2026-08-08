using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ClassHelper.App.Services;
using ClassHelper.App.ViewModels;
using ClassHelper.Core.RollCall;

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
