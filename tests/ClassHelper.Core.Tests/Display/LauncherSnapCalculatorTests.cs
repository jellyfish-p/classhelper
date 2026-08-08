using ClassHelper.Core.Display;

namespace ClassHelper.Core.Tests.Display;

public sealed class LauncherSnapCalculatorTests
{
    [Fact]
    public void Snap_UsesNearestRightEdge()
    {
        var result = LauncherSnapCalculator.Snap(
            new WindowBounds(1815, 400, 64, 278),
            new ScreenBounds(0, 0, 1920, 1080));

        Assert.Equal(ScreenEdge.Right, result.Edge);
        Assert.Equal(1844, result.Left);
        Assert.Equal(400, result.Top);
    }

    [Fact]
    public void Snap_ClampsLauncherInsideVisibleWorkArea()
    {
        var result = LauncherSnapCalculator.Snap(
            new WindowBounds(-400, -300, 64, 278),
            new ScreenBounds(0, 0, 1920, 1040));

        Assert.Equal(ScreenEdge.Top, result.Edge);
        Assert.Equal(12, result.Left);
        Assert.Equal(12, result.Top);
    }

    [Fact]
    public void Snap_SupportsNegativeCoordinateScreens()
    {
        var result = LauncherSnapCalculator.Snap(
            new WindowBounds(-1260, 400, 64, 278),
            new ScreenBounds(-1280, 0, 1280, 1024));

        Assert.Equal(ScreenEdge.Left, result.Edge);
        Assert.Equal(-1268, result.Left);
    }
}
