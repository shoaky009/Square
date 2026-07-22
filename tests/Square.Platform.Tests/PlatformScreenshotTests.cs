using System;
using Square.Platform;
#if PLATFORM_WIN32
using Square.Platform.Win32;
#elif PLATFORM_X11
using Square.Platform.X11;
#endif
using Xunit;

namespace Square.Platform.Tests;

public class PlatformScreenshotTests
{
    public PlatformScreenshotTests()
    {
#if PLATFORM_WIN32
        PlatformRegistry.Register(new Win32PlatformFactory());
#elif PLATFORM_X11
        PlatformRegistry.Register(new X11PlatformFactory());
#endif
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryCaptureByProcessIdRejectsInvalidProcessIds(int processId)
    {
        Assert.False(PlatformScreenshot.TryCaptureByProcessId(processId, out _));
    }

    [Fact]
    public void CaptureByProcessIdRejectsInvalidProcessIds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlatformScreenshot.CaptureByProcessId(0));
    }

    [Fact]
    public void CaptureByProcessIdReportsMissingWindow()
    {
        Assert.Throws<InvalidOperationException>(() => PlatformScreenshot.CaptureByProcessId(int.MaxValue));
    }
}
