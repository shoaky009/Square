using Square.Extensions.CodePad;
using Square.Graphics;
using Square.Platform;
using Xunit;

namespace Square.Extensions.CodePad.Tests;

public class GutterDecorationTests
{
    [Fact]
    public void ShowGlyphMargin_DefaultsTrue_AndWidthPositive()
    {
        CodePadRegistration.RegisterDefaults();
        var pad = new CodePad { Geometry = new Rect(0, 0, 400, 200), Value = "a\nb" };
        Assert.True(pad.ShowGlyphMargin);
        Assert.True(pad.GlyphMarginWidth > 0);
    }

    [Fact]
    public void SetDecoration_ReplacesSameId_AndIndexesByLine()
    {
        CodePadRegistration.RegisterDefaults();
        var pad = new CodePad { Geometry = new Rect(0, 0, 400, 200), Value = "one\ntwo\nthree" };

        pad.SetDecoration(new CodePadLineDecoration
        {
            Id = "bp-1",
            Line = 1,
            Glyph = "●",
            GlyphColor = Color.FromRgb(255, 0, 0),
        });
        pad.SetDecoration(new CodePadLineDecoration
        {
            Id = "bp-1",
            Line = 2,
            Glyph = "●",
            GlyphColor = Color.FromRgb(0, 255, 0),
        });
        pad.SetDecoration(new CodePadLineDecoration
        {
            Id = "git-2",
            Line = 2,
            GutterColor = Color.FromRgb(0, 160, 0),
        });

        Assert.Equal(2, pad.DecorationCount);
        Assert.Empty(pad.GetDecorationsAt(1));
        Assert.Equal(2, pad.GetDecorationsAt(2).Count);
        Assert.Equal("●", pad.GetDecorationsAt(2).First(d => d.Id == "bp-1").Glyph);
    }

    [Fact]
    public void RemoveAndClearDecorations_UpdateCount()
    {
        CodePadRegistration.RegisterDefaults();
        var pad = new CodePad { Geometry = new Rect(0, 0, 400, 200), Value = "a\nb" };
        pad.SetDecorations(
        [
            new CodePadLineDecoration { Id = "a", Line = 0, Glyph = "●" },
            new CodePadLineDecoration { Id = "b", Line = 1, GutterColor = Color.FromRgb(1, 2, 3) },
        ]);
        Assert.Equal(2, pad.DecorationCount);
        Assert.True(pad.RemoveDecoration("a"));
        Assert.False(pad.RemoveDecoration("missing"));
        Assert.Equal(1, pad.DecorationCount);
        pad.ClearDecorations();
        Assert.Equal(0, pad.DecorationCount);
    }

    [Fact]
    public void GutterClick_OnGlyphMargin_RaisesEvent_WithLineAndLane()
    {
        CodePadRegistration.RegisterDefaults();
        var pad = new CodePad
        {
            Geometry = new Rect(0, 0, 400, 200),
            Value = "line0\nline1\nline2",
            ShowGlyphMargin = true,
            ShowLineNumbers = true,
            ShowFolding = false,
        };

        CodePadGutterClickEventArgs? args = null;
        pad.GutterClick += (_, e) => args = e;

        // glyph margin is leftmost; y for second line (~ padding + lineHeight)
        pad.HandlePointerDown(new Point(6, 8 + 16));
        Assert.NotNull(args);
        Assert.Equal(CodePadGutterLane.Glyph, args!.Lane);
        Assert.True(args.Line is 0 or 1 or 2);
    }

    [Fact]
    public void ResolveCursorAt_ArrowInGlyphMargin()
    {
        CodePadRegistration.RegisterDefaults();
        var pad = new CodePad
        {
            Geometry = new Rect(10, 20, 400, 200),
            Value = "hello",
            ShowGlyphMargin = true,
            ShowLineNumbers = false,
            ShowFolding = false,
        };
        Assert.Equal(CursorKind.Arrow, pad.ResolveCursorAt(new Point(14, 40)));
        Assert.Equal(CursorKind.Text, pad.ResolveCursorAt(new Point(10 + pad.GlyphMarginWidth + 20, 40)));
    }
}
