using Square.Controls.Controls;
using Square.Graphics;
using Square.Rendering;
using Xunit;

namespace Square.UI.Tests;

public sealed class MeasuredBox : UIElement
{
    private readonly Size _size;

    public MeasuredBox(float width, float height)
    {
        _size = new Size(width, height);
    }

    public override Size Measure(Size availableSize) => _size;
}

public class GridLayoutTests
{
    [Fact]
    public void GridLayoutArrangesFrTracksGapAndSpans()
    {
        var root = new View();
        root.Style.Set("display", "grid");
        root.Style.Set("grid-template-columns", "1fr 2fr");
        root.Style.Set("grid-template-rows", "50px 1fr");
        root.Style.Set("gap", "10px");

        var header = new Square.Controls.Controls.Text("header");
        header.Style.Set("grid-column", "1 / span 2");
        header.Style.Set("grid-row", "1");
        var left = new Square.Controls.Controls.Text("left");
        left.Style.Set("grid-column", "1");
        left.Style.Set("grid-row", "2");
        var right = new Square.Controls.Controls.Text("right");
        right.Style.Set("grid-column", "2");
        right.Style.Set("grid-row", "2");

        root.Children.Add(header);
        root.Children.Add(left);
        root.Children.Add(right);

        var layout = new LayoutEngine();
        layout.Measure(root, new Size(310, 160));
        layout.Arrange(root, new Rect(0, 0, 310, 160));

        Assert.Equal(new Rect(0, 0, 310, 50), header.Geometry);
        Assert.Equal(new Rect(0, 60, 100, 100), left.Geometry);
        Assert.Equal(new Rect(110, 60, 200, 100), right.Geometry);
    }

    [Fact]
    public void IntrinsicWidthKeywordsUseMeasuredContentWidth()
    {
        var root = new View();
        root.Style.Set("display", "grid");
        root.Style.Set("grid-template-columns", "max-content 1fr");
        root.Style.Set("grid-template-rows", "50px");
        root.Style.Set("gap", "10px");
        var content = new MeasuredBox(75, 20);
        var fill = new MeasuredBox(10, 20);
        fill.Style.Set("grid-column", "2");

        root.Children.Add(content);
        root.Children.Add(fill);

        var layout = new LayoutEngine();
        layout.Measure(root, new Size(200, 50));
        layout.Arrange(root, new Rect(0, 0, 200, 50));

        Assert.Equal(new Rect(0, 0, 75, 50), content.Geometry);
        Assert.Equal(new Rect(85, 0, 115, 50), fill.Geometry);
    }

    [Fact]
    public void RelativeFontUnitsResolveAgainstFontSize()
    {
        var root = new View();
        root.Style.Set("font-size", "20px");
        var rem = new MeasuredBox(1, 1);
        rem.Style.Set("width", "2rem");
        rem.Style.Set("height", "1rem");
        var em = new MeasuredBox(1, 1);
        em.Style.Set("font-size", "10px");
        em.Style.Set("width", "3em");
        em.Style.Set("height", "2em");

        root.Children.Add(rem);
        root.Children.Add(em);

        var layout = new LayoutEngine();
        layout.Measure(root, new Size(200, 200));
        layout.Arrange(root, new Rect(0, 0, 200, 200));

        Assert.Equal(new Rect(0, 0, 40, 20), rem.Geometry);
        Assert.Equal(new Rect(0, 20, 30, 20), em.Geometry);
    }

    [Fact]
    public void GridMinMaxAndAutoPlacementFillCellsInOrder()
    {
        var root = new View();
        root.Style.Set("display", "grid");
        root.Style.Set("grid-template-columns", "minmax(50px, 1fr) minmax(20px, 2fr)");
        root.Style.Set("grid-template-rows", "40px 40px");
        root.Style.Set("gap", "10px");
        var first = new MeasuredBox(1, 1);
        var second = new MeasuredBox(1, 1);
        var third = new MeasuredBox(1, 1);

        root.Children.Add(first);
        root.Children.Add(second);
        root.Children.Add(third);

        var layout = new LayoutEngine();
        layout.Measure(root, new Size(230, 90));
        layout.Arrange(root, new Rect(0, 0, 230, 90));

        Assert.Equal(new Rect(0, 0, 100, 40), first.Geometry);
        Assert.Equal(new Rect(110, 0, 120, 40), second.Geometry);
        Assert.Equal(new Rect(0, 50, 100, 40), third.Geometry);
    }

    [Fact]
    public void GridTemplateAreasPlaceChildrenByNamedArea()
    {
        var root = new View();
        root.Style.Set("display", "grid");
        root.Style.Set("grid-template-columns", "100px 200px");
        root.Style.Set("grid-template-rows", "40px 60px");
        root.Style.Set("grid-template-areas", "header header | nav main");
        var header = new MeasuredBox(1, 1);
        header.Style.Set("grid-area", "header");
        var main = new MeasuredBox(1, 1);
        main.Style.Set("grid-area", "main");

        root.Children.Add(header);
        root.Children.Add(main);

        var layout = new LayoutEngine();
        layout.Measure(root, new Size(300, 100));
        layout.Arrange(root, new Rect(0, 0, 300, 100));

        Assert.Equal(new Rect(0, 0, 300, 40), header.Geometry);
        Assert.Equal(new Rect(100, 40, 200, 60), main.Geometry);
    }
}
