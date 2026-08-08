using System.Windows;
using ClassHelper.App.ViewModels;

namespace ClassHelper.App.Services;

public sealed class AppController : IDisposable
{
    private readonly ClassroomWorkspace _workspace;
    private readonly MainViewModel _mainViewModel;
    private readonly MainWindow _mainWindow;
    private readonly BannerWindow _bannerWindow;
    private readonly LauncherWindow _launcherWindow;
    private bool _disposed;

    public AppController(ClassroomWorkspace workspace)
    {
        _workspace = workspace;
        _mainViewModel = new MainViewModel(workspace);
        _mainWindow = new MainWindow(this, _mainViewModel);
        _bannerWindow = new BannerWindow(workspace);
        _launcherWindow = new LauncherWindow(this);
    }

    public void Start()
    {
        _bannerWindow.Show();
        _launcherWindow.Show();
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    public void ShowMainWindow(int pageIndex = 0)
    {
        _mainViewModel.RefreshToday();
        _mainWindow.NavigateTo(pageIndex);
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void ShowRollCall(ClassHelper.Core.RollCall.RollCallMode mode = ClassHelper.Core.RollCall.RollCallMode.BalancedRound) =>
        new RollCallWindow(_workspace, mode).Show();

    public void ToggleBanner(bool isVisible)
    {
        if (isVisible)
        {
            _bannerWindow.Show();
        }
        else
        {
            _bannerWindow.Hide();
        }
    }

    public void Exit()
    {
        Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _mainWindow.AllowClose();
        _launcherWindow.Close();
        _bannerWindow.Close();
        _mainWindow.Close();
    }
}
