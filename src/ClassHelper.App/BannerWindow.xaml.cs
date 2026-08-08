using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ClassHelper.App.ViewModels;

namespace ClassHelper.App;

public partial class BannerWindow : Window
{
    private static readonly nint HwndBottom = 1;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public BannerWindow(ClassroomWorkspace workspace)
    {
        _ = workspace;
        InitializeComponent();
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
        _timer.Tick += (_, _) => RefreshClock();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + Math.Max(12, (workArea.Width - Width) / 2);
        Top = workArea.Top + 12;
        RefreshClock();
        _timer.Start();
        MoveToDesktopLayer();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLong(handle, GwlExStyle);
        NativeMethods.SetWindowLong(handle, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExNoActivate);
    }

    private void RefreshClock()
    {
        var now = DateTime.Now;
        var culture = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
        DateText.Text = $"{now:M 月 d 日} {culture.DateTimeFormat.GetDayName(now.DayOfWeek)}";
        TimeText.Text = now.ToString("HH:mm");

        if (now.Second % 5 == 0)
        {
            MoveToDesktopLayer();
        }
    }

    private void MoveToDesktopLayer()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != nint.Zero)
        {
            NativeMethods.SetWindowPos(handle, HwndBottom, 0, 0, 0, 0, SwpNoActivate | SwpNoMove | SwpNoSize);
        }
    }

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
        internal static partial int GetWindowLong(nint window, int index);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
        internal static partial int SetWindowLong(nint window, int index, int newLong);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetWindowPos(
            nint window,
            nint insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);
    }
}
