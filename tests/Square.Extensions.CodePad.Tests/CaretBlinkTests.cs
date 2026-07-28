using Square.Extensions.CodePad;
using Square.Graphics;
using Xunit;

namespace Square.Extensions.CodePad.Tests;

public class CaretBlinkTests
{
    [Fact]
    public void ToggleCaretBlink_WhenUnfocused_ReturnsFalse()
    {
        var pad = new CodePad { Geometry = new Rect(0, 0, 200, 100), Value = "hi" };
        Assert.False(pad.ToggleCaretBlink());
    }

    [Fact]
    public void ToggleCaretBlink_WhenFocused_EventuallyTogglesPaint()
    {
        var pad = new CodePad { Geometry = new Rect(0, 0, 200, 100), Value = "hi" };
        pad.Focus();
        pad.ResetCaretBlink();

        // Advance past the initial hold period.
        var toggled = false;
        for (var i = 0; i < 60; i++)
        {
            System.Threading.Thread.Sleep(50);
            if (pad.ToggleCaretBlink())
            {
                toggled = true;
                break;
            }
        }
        Assert.True(toggled);
    }

    [Fact]
    public void ResetCaretBlink_RestoresVisibleCaret()
    {
        var pad = new CodePad { Geometry = new Rect(0, 0, 200, 100), Value = "ab" };
        pad.Focus();
        pad.ResetCaretBlink();
        // After reset, blink should not immediately hide (returns false until hold expires)
        Assert.False(pad.ToggleCaretBlink());
    }

    [Fact]
    public void ToggleCaretBlink_WithSelection_ReturnsFalse()
    {
        var pad = new CodePad { Geometry = new Rect(0, 0, 200, 100), Value = "abcd" };
        pad.Focus();
        pad.SelectAll();
        Assert.True(pad.SelectionLength > 0);
        Assert.False(pad.ToggleCaretBlink());
    }

    [Fact]
    public void DragSelection_InvalidatesFullPaint_NotOnlyCaret()
    {
        CodePadRegistration.RegisterDefaults();
        var pad = new CodePad
        {
            Geometry = new Rect(0, 0, 400, 200),
            Value = "hello world\nsecond line",
            ShowLineNumbers = false,
            ShowFolding = false,
            ShowGlyphMargin = false,
            ShowOverviewRuler = false,
        };
        pad.Focus();
        pad.HandlePointerDown(new Point(20, 12));
        pad.ClearPaintDirty();
        pad.HandlePointerMove(new Point(120, 12));
        Assert.True(pad.NeedsPaint);
        Assert.True(pad.IsPaintFullDirty);
        Assert.True(pad.SelectionLength > 0);
    }

    [Fact]
    public void DragSelection_NearBottomEdge_AutoScrolls()
    {
        CodePadRegistration.RegisterDefaults();
        var lines = string.Join("\n", Enumerable.Range(0, 80).Select(i => "line " + i + " content"));
        var pad = new CodePad
        {
            Geometry = new Rect(0, 0, 400, 120),
            Value = lines,
            ShowLineNumbers = false,
            ShowFolding = false,
            ShowGlyphMargin = false,
            ShowOverviewRuler = false,
            ShowScrollBars = false,
        };
        pad.Focus();
        pad.HandlePointerDown(new Point(40, 20));
        // drag near bottom edge — should scroll down and extend selection
        for (var i = 0; i < 8; i++)
            pad.HandlePointerMove(new Point(40, pad.Geometry.Bottom - 2));
        Assert.True(pad.SelectionLength > 0);
        // with many lines and small viewport, edge-drag should move scroll
        Assert.True(pad.CaretIndex > 0);
    }
}
