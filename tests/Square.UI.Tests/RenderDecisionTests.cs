using Square.Graphics;
using Square.Hosting;
using Xunit;

namespace Square.UI.Tests;

public class RenderDecisionTests
{
    [Fact]
    public void AutoUsesDirtyRegionWhenUnderThresholds()
    {
        var diagnostics = RenderDecision.Decide(
            RenderMode.Auto,
            [new Rect(0, 0, 10, 10)],
            new Size(100, 100),
            maxDirtyRectCount: 4,
            maxDirtyAreaRatio: 0.2f);

        Assert.False(diagnostics.UsedFullFrame);
        Assert.Equal("DirtyRegion", diagnostics.Reason);
        Assert.Equal(0.01f, diagnostics.DirtyAreaRatio, precision: 4);
        Assert.Equal(new Rect(0, 0, 10, 10), diagnostics.DirtyUnion);
    }

    [Fact]
    public void AutoFallsBackWhenDirtyAreaIsTooLarge()
    {
        var diagnostics = RenderDecision.Decide(
            RenderMode.Auto,
            [new Rect(0, 0, 60, 60)],
            new Size(100, 100),
            maxDirtyRectCount: 4,
            maxDirtyAreaRatio: 0.2f);

        Assert.True(diagnostics.UsedFullFrame);
        Assert.Equal("DirtyAreaTooLarge", diagnostics.Reason);
    }

    [Fact]
    public void AutoFallsBackWhenDirtyRectCountIsTooLarge()
    {
        var diagnostics = RenderDecision.Decide(
            RenderMode.Auto,
            [
                new Rect(0, 0, 1, 1),
                new Rect(2, 0, 1, 1),
                new Rect(4, 0, 1, 1)
            ],
            new Size(100, 100),
            maxDirtyRectCount: 2,
            maxDirtyAreaRatio: 0.2f);

        Assert.True(diagnostics.UsedFullFrame);
        Assert.Equal("TooManyDirtyRects", diagnostics.Reason);
    }

    [Fact]
    public void DirtyRegionModeIgnoresAutoThresholds()
    {
        var diagnostics = RenderDecision.Decide(
            RenderMode.DirtyRegion,
            [new Rect(0, 0, 100, 100)],
            new Size(100, 100),
            maxDirtyRectCount: 0,
            maxDirtyAreaRatio: 0f);

        Assert.False(diagnostics.UsedFullFrame);
        Assert.Equal("DirtyRegion", diagnostics.Reason);
    }
}
