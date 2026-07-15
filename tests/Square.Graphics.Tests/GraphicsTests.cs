using Square.Graphics;
using Xunit;

namespace Square.Graphics.Tests;

public class ColorTests
{
    [Fact]
    public void ParseHex3()
    {
        var c = Color.Parse("#f00");
        Assert.Equal(255, c.R);
        Assert.Equal(0, c.G);
        Assert.Equal(0, c.B);
        Assert.Equal(255, c.A);
    }

    [Fact]
    public void ParseHex6()
    {
        var c = Color.Parse("#0078d4");
        Assert.Equal(0, c.R);
        Assert.Equal(120, c.G);
        Assert.Equal(212, c.B);
    }

    [Fact]
    public void ParseHex8()
    {
        var c = Color.Parse("#FF0078d4");
        Assert.Equal(255, c.A);
        Assert.Equal(0, c.R);
    }

    [Fact]
    public void Equality()
    {
        Assert.Equal(Color.Red, Color.FromRgb(255, 0, 0));
        Assert.NotEqual(Color.Red, Color.Blue);
    }

    [Fact]
    public void ToPackedBgra()
    {
        var c = Color.FromRgba(1, 2, 3, 4);
        var packed = c.ToPackedBgra();
        Assert.Equal(4u, (packed >> 24) & 0xFF);
    }
}

public class RectTests
{
    [Fact]
    public void Contains()
    {
        var r = new Rect(10, 10, 100, 100);
        Assert.True(r.Contains(50, 50));
        Assert.False(r.Contains(5, 5));
    }

    [Fact]
    public void IntersectsWith()
    {
        var a = new Rect(0, 0, 100, 100);
        var b = new Rect(50, 50, 100, 100);
        Assert.True(a.IntersectsWith(b));
    }

    [Fact]
    public void Union()
    {
        var a = new Rect(0, 0, 50, 50);
        var b = new Rect(100, 100, 50, 50);
        var u = Rect.Union(a, b);
        Assert.Equal(0, u.X);
        Assert.Equal(0, u.Y);
        Assert.Equal(150, u.Width);
        Assert.Equal(150, u.Height);
    }

    [Fact]
    public void Intersect()
    {
        var a = new Rect(0, 0, 100, 100);
        var b = new Rect(50, 50, 100, 100);
        var i = Rect.Intersect(a, b);
        Assert.Equal(50, i.X);
        Assert.Equal(50, i.Y);
        Assert.Equal(50, i.Width);
        Assert.Equal(50, i.Height);
    }
}

public class SizeTests
{
    [Fact]
    public void Arithmetic()
    {
        var a = new Size(100, 200);
        var b = new Size(50, 100);
        Assert.Equal(new Size(150, 300), a + b);
        Assert.Equal(new Size(50, 100), a - b);
        Assert.Equal(new Size(200, 400), a * 2);
    }

    [Fact]
    public void IsEmpty()
    {
        Assert.False(new Size(100, 100).IsEmpty);
        Assert.True(Size.Empty.IsEmpty);
    }
}