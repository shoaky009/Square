using System;
using Square.Platform;
using Xunit;

namespace Square.Platform.Tests;

public class PlatformScreenshotTests
{
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
