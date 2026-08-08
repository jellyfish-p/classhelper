using System.Windows;
using System.Windows.Input;
using ClassHelper.App.Services;
using ClassHelper.Core.Display;

namespace ClassHelper.App;

public partial class LauncherWindow : Window
{
    private readonly AppController _controller;

    public LauncherWindow(AppController controller)
    {
        _controller = controller;
        InitializeComponent();
        Loaded += (_, _) => PlaceInitially();
    }

    private void PlaceInitially()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 12;
        Top = workArea.Top + (workArea.Height - Height) / 2;
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
        SnapToNearestEdge();
    }

    private void SnapToNearestEdge()
    {
        var workArea = SystemParameters.WorkArea;
        var snapped = LauncherSnapCalculator.Snap(
            new WindowBounds(Left, Top, ActualWidth, ActualHeight),
            new ScreenBounds(workArea.Left, workArea.Top, workArea.Width, workArea.Height));
        Left = snapped.Left;
        Top = snapped.Top;
    }

    private void RollCall_Click(object sender, RoutedEventArgs e) => _controller.ShowRollCall();

    private void Roster_Click(object sender, RoutedEventArgs e) => _controller.ShowMainWindow(2);

    private void OpenMain_Click(object sender, RoutedEventArgs e) => _controller.ShowMainWindow();

    private void Exit_Click(object sender, RoutedEventArgs e) => _controller.Exit();
}
