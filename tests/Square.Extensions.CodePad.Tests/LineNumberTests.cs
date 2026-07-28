using Square.Extensions.CodePad;
using Square.Graphics;
using Square.Platform;
using Xunit;

namespace Square.Extensions.CodePad.Tests;

public class LineNumberTests
{
    [Fact]
    public void ShowLineNumbers_DefaultsTrue_AndGutterPositive()
    {
        CodePadRegistration.RegisterDefaults();
        var pad = new CodePad
        {
            Geometry = new Rect(0, 0, 400, 200),
            Value = "a\nb\nc",
        };
        Assert.True(pad.ShowLineNumbers);
        Assert.True(pad.LineNumberGutterWidth > 0);
    }

    [Fact]
    public void ToggleLineNumbers_HidesAndShowsGutter()
    {
        CodePadRegistration.RegisterDefaults();
        var pad = new CodePad
        {
            Geometry = new Rect(0, 0, 400, 200),
            Value = string.Join("\n", Enumerable.Range(1, 100).Select(i => "line " + i)),
        };

        var openWidth = pad.LineNumberGutterWidth;
        Assert.True(openWidth > 0);

        pad.ToggleLineNumbers();
        Assert.False(pad.ShowLineNumbers);
        Assert.Equal(0, pad.LineNumberGutterWidth);

        pad.ShowLineNumbers = true;
        Assert.True(pad.ShowLineNumbers);
        Assert.True(pad.LineNumberGutterWidth > 0);
        // more digits for 100 lines → gutter at least as wide as small docs
        Assert.True(pad.LineNumberGutterWidth >= openWidth - 0.1f);
    }

    [Fact]
    public void LineNumberGutterWidth_GrowsWithLineCountDigits()
    {
        CodePadRegistration.RegisterDefaults();
        var small = new CodePad { Geometry = new Rect(0, 0, 400, 200), Value = "a\nb" };
        var large = new CodePad
        {
            Geometry = new Rect(0, 0, 400, 200),
            Value = string.Join("\n", Enumerable.Range(1, 1000)),
        };
        Assert.True(large.LineNumberGutterWidth > small.LineNumberGutterWidth);
    }

    [Fact]
    public void ResolveCursorAt_UsesArrowInGutter_AndTextInContent()
    {
        CodePadRegistration.RegisterDefaults();
        var pad = new CodePad
        {
            Geometry = new Rect(10, 20, 400, 200),
            Value = "hello\nworld",
            ShowLineNumbers = true,
            ShowFolding = true,
        };

        var gutterWidth = pad.GlyphMarginWidth + pad.LineNumberGutterWidth + pad.FoldingGutterWidth;
        Assert.True(gutterWidth > 0);

        var gutterCursor = pad.ResolveCursorAt(new Point(10 + gutterWidth / 2f, 40));
        Assert.Equal(CursorKind.Arrow, gutterCursor);

        var textCursor = pad.ResolveCursorAt(new Point(10 + gutterWidth + 40, 40));
        Assert.Equal(CursorKind.Text, textCursor);

        pad.ShowLineNumbers = false;
        pad.ShowFolding = false;
        pad.ShowGlyphMargin = false;
        Assert.Equal(CursorKind.Text, pad.ResolveCursorAt(new Point(20, 40)));
    }
}
