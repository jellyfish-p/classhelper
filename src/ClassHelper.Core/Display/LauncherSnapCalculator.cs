namespace ClassHelper.Core.Display;

public readonly record struct ScreenBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

public readonly record struct WindowBounds(double Left, double Top, double Width, double Height);

public readonly record struct SnappedPosition(double Left, double Top, ScreenEdge Edge);

public enum ScreenEdge
{
    Left,
    Top,
    Right,
    Bottom
}

public static class LauncherSnapCalculator
{
    public static SnappedPosition Snap(WindowBounds window, ScreenBounds screen, double margin = 12)
    {
        var distances = new Dictionary<ScreenEdge, double>
        {
            [ScreenEdge.Left] = Math.Abs(window.Left - screen.Left),
            [ScreenEdge.Top] = Math.Abs(window.Top - screen.Top),
            [ScreenEdge.Right] = Math.Abs(screen.Right - (window.Left + window.Width)),
            [ScreenEdge.Bottom] = Math.Abs(screen.Bottom - (window.Top + window.Height))
        };

        var edge = distances.MinBy(pair => pair.Value).Key;
        var clampedLeft = Math.Clamp(window.Left, screen.Left + margin, screen.Right - window.Width - margin);
        var clampedTop = Math.Clamp(window.Top, screen.Top + margin, screen.Bottom - window.Height - margin);

        return edge switch
        {
            ScreenEdge.Left => new SnappedPosition(screen.Left + margin, clampedTop, edge),
            ScreenEdge.Top => new SnappedPosition(clampedLeft, screen.Top + margin, edge),
            ScreenEdge.Right => new SnappedPosition(screen.Right - window.Width - margin, clampedTop, edge),
            ScreenEdge.Bottom => new SnappedPosition(clampedLeft, screen.Bottom - window.Height - margin, edge),
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };
    }
}
