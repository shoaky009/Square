using Square.Graphics;
using Square.UI;
using Square.Controls.Controls;
using Xunit;

namespace Square.UI.Tests;

public class QueryTests
{
    [Fact]
    public void QueryByType()
    {
        var view = new View();
        var btn = new Button();
        var text = new Square.Controls.Controls.Text();
        view.Children.Add(btn);
        view.Children.Add(text);

        var found = view.Query<Button>();
        Assert.NotNull(found);
        Assert.Same(btn, found);
    }

    [Fact]
    public void QueryByTypeAndClass()
    {
        var view = new View();
        var btn1 = new Button();
        btn1.ClassList.Add("primary");
        var btn2 = new Button();
        view.Children.Add(btn1);
        view.Children.Add(btn2);

        var found = view.Query<Button>("primary");
        Assert.NotNull(found);
        Assert.Same(btn1, found);
    }

    [Fact]
    public void QueryAllByType()
    {
        var view = new View();
        var btn1 = new Button();
        var btn2 = new Button();
        var text = new Square.Controls.Controls.Text();
        view.Children.Add(btn1);
        view.Children.Add(btn2);
        view.Children.Add(text);

        var all = view.QueryAll<Button>();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void QueryNested()
    {
        var outer = new View();
        var inner = new View();
        var btn = new Button();
        outer.Children.Add(inner);
        inner.Children.Add(btn);

        var found = outer.Query<Button>();
        Assert.NotNull(found);
        Assert.Same(btn, found);
    }

    [Fact]
    public void QueryNotFound()
    {
        var view = new View();
        var text = new Square.Controls.Controls.Text();
        view.Children.Add(text);

        var found = view.Query<Button>();
        Assert.Null(found);
    }

    [Fact]
    public void VisualStateSet()
    {
        var btn = new Button();
        Assert.False(btn.HasState(VisualState.Hover));
        btn.SetState(VisualState.Hover, true);
        Assert.True(btn.HasState(VisualState.Hover));
        btn.SetState(VisualState.Hover, false);
        Assert.False(btn.HasState(VisualState.Hover));
    }
}