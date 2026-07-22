using Square.Controls;
using Square.Graphics;
using Square.Rendering;
using Xunit;

namespace Square.UI.Tests;

public class FlexSizingTests
{
    [Fact]
    public void ExplicitColumnHeightsDoNotShrinkByDefault()
    {
        var root = new View();
        root.Style.Set("display", "flex");
        root.Style.Set("flex-direction", "column");
        var tabList = new View();
        tabList.Style.Set("height", "42px");
        var panels = new View();
        panels.Style.Set("height", "792px");
        root.Children.Add(tabList);
        root.Children.Add(panels);

        Layout(root, new Size(400, 500));

        Assert.Equal(42, tabList.Geometry.Height);
        Assert.Equal(792, panels.Geometry.Height);
    }

    [Fact]
    public void ExplicitFlexShrinkStillAllowsFixedHeightToShrink()
    {
        var root = new View();
        root.Style.Set("display", "flex");
        root.Style.Set("flex-direction", "column");
        var first = new View();
        first.Style.Set("height", "300px");
        first.Style.Set("flex-shrink", "1");
        var second = new View();
        second.Style.Set("height", "300px");
        second.Style.Set("flex-shrink", "1");
        root.Children.Add(first);
        root.Children.Add(second);

        Layout(root, new Size(400, 400));

        Assert.Equal(200, first.Geometry.Height);
        Assert.Equal(200, second.Geometry.Height);
    }

    [Fact]
    public void OverflowAutoContentKeepsExplicitChildHeights()
    {
        var scroller = new View();
        scroller.Style.Set("display", "flex");
        scroller.Style.Set("flex-direction", "column");
        scroller.Style.Set("overflow-y", "auto");
        var first = new View();
        first.Style.Set("height", "180px");
        var second = new View();
        second.Style.Set("height", "180px");
        scroller.Children.Add(first);
        scroller.Children.Add(second);

        Layout(scroller, new Size(400, 200));

        Assert.Equal(180, first.Geometry.Height);
        Assert.Equal(180, second.Geometry.Height);
        Assert.Equal(360, scroller.ScrollContentSize.Height);
        Assert.Equal(160, scroller.ScrollContentSize.Height - scroller.Geometry.Height);
    }

    private static void Layout(View root, Size size)
    {
        var layout = new LayoutEngine();
        layout.Measure(root, size);
        layout.Arrange(root, new Rect(0, 0, size.Width, size.Height));
    }
}
