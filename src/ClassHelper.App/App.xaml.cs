using System.Windows;
using ClassHelper.App.Services;
using ClassHelper.App.ViewModels;

namespace ClassHelper.App;

public partial class App : Application
{
    private AppController? _controller;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (UpdateInstaller.IsApplyUpdateRequest(e.Args))
        {
            try
            {
                await UpdateInstaller.ApplyUpdateAsync(e.Args, CancellationToken.None);
                Shutdown();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"更新安装失败，原程序会尽量保持不变。\n\n{exception.Message}",
                    "课堂助手更新",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(2);
            }

            return;
        }

        if (UpdateInstaller.IsCleanupUpdateRequest(e.Args))
        {
            await UpdateInstaller.CleanupUpdateAsync(e.Args, CancellationToken.None);
        }

        try
        {
            var store = new JsonClassroomStore();
            var data = await store.LoadAsync(CancellationToken.None);
            var workspace = new ClassroomWorkspace(data, store);
            _controller = new AppController(workspace);

            if (e.Args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
            {
                var rollCallWindow = new RollCallWindow(workspace, ClassHelper.Core.RollCall.RollCallMode.BalancedRound);
                rollCallWindow.Close();
                _controller.Dispose();
                Shutdown();
                return;
            }

            _controller.Start();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"课堂助手启动失败。\n\n{exception.Message}",
                "课堂助手",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        base.OnExit(e);
    }
}
