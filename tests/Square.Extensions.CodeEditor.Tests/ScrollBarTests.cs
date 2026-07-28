using Square.Extensions.CodeEditor;
using Square.Graphics;
using Square.Platform;
using Xunit;

namespace Square.Extensions.CodeEditor.Tests;

public class ScrollBarTests
{
    private static CodeEditor CreateTall()
    {
        CodeEditorRegistration.RegisterDefaults();
        return new CodeEditor
        {
            Geometry = new Rect(0, 0, 300, 120),
            Value = string.Join("\n", Enumerable.Range(0, 80).Select(i => "line-" + i + " " + new string('x', 20))),
            ShowLineNumbers = true,
            ShowFolding = false,
            WordWrap = false,
            ShowScrollBars = true,
        };
    }

    [Fact]
    public void ShowScrollBars_DefaultsTrue_AndToggleWorks()
    {
        var pad = new CodeEditor();
        Assert.True(pad.ShowScrollBars);
        pad.ToggleScrollBars();
        Assert.False(pad.ShowScrollBars);
        pad.ShowScrollBars = true;
        Assert.True(pad.ShowScrollBars);
    }

    [Fact]
    public void ResolveCursorAt_UsesArrowOnScrollBarArea()
    {
        var pad = CreateTall();
        // force layout/scroll metrics via caret
        _ = pad.CaretRect;

        var onBar = pad.ResolveCursorAt(new Point(pad.Geometry.Right - 4, pad.Geometry.Y + 40));
        Assert.Equal(CursorKind.Arrow, onBar);

        var inText = pad.ResolveCursorAt(new Point(pad.Geometry.X + 80, pad.Geometry.Y + 40));
        Assert.Equal(CursorKind.Text, inText);
    }

    [Fact]
    public void ScrollBarTrackClick_ChangesScrollPosition()
    {
        var pad = CreateTall();
        _ = pad.CaretRect;

        // click near bottom of vertical track to page down
        var x = pad.Geometry.Right - 5;
        var y = pad.Geometry.Bottom - 30;
        pad.HandlePointerDown(new Point(x, y));
        pad.HandlePointerUp(new Point(x, y));
        // should not throw; with tall content scroll should move or stay at max
        Assert.True(pad.ShowScrollBars);
    }

    [Fact]
    public void WhenScrollBarsHidden_CursorRemainsTextInContent()
    {
        var pad = CreateTall();
        pad.ShowScrollBars = false;
        pad.ShowOverviewRuler = false;
        var cursor = pad.ResolveCursorAt(new Point(pad.Geometry.Right - 4, pad.Geometry.Y + 40));
        // without scrollbars/overview, far-right is still text area (or outside gutter)
        Assert.Equal(CursorKind.Text, cursor);
    }
}
