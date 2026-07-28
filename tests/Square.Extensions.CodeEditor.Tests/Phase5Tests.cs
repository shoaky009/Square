using Square.Extensions.CodeEditor;
using Square.Graphics;
using Square.Platform;
using Square.UI;
using Xunit;

namespace Square.Extensions.CodeEditor.Tests;

public class Phase5Tests
{
    [Fact]
    public void InvalidatePaint_LocalRect_IsPartialDirty()
    {
        var el = new CodeEditor { Geometry = new Rect(0, 0, 200, 100) };
        el.ClearPaintDirty();
        Assert.False(el.NeedsPaint);
        el.InvalidatePaint(new Rect(10, 20, 4, 16));
        Assert.True(el.NeedsPaint);
        Assert.False(el.IsPaintFullDirty);
        Assert.Single(el.PaintDirtyRects);
        el.InvalidatePaint();
        Assert.True(el.IsPaintFullDirty);
        Assert.Empty(el.PaintDirtyRects);
    }

    [Fact]
    public void ToggleCaretBlink_UsesPartialPaintDirty()
    {
        CodeEditorRegistration.RegisterDefaults();
        var pad = new CodeEditor
        {
            Geometry = new Rect(0, 0, 400, 200),
            Value = "hello",
        };
        pad.Focus();
        pad.ClearPaintDirty();
        pad.ResetCaretBlink();
        Assert.True(pad.NeedsPaint);
        // reset uses caret-local invalidation when geometry available
        Assert.False(pad.IsPaintFullDirty);
        Assert.NotEmpty(pad.PaintDirtyRects);
    }

    [Fact]
    public void OverviewRuler_Toggle_AndWidth()
    {
        CodeEditorRegistration.RegisterDefaults();
        var pad = new CodeEditor { Geometry = new Rect(0, 0, 400, 200), Value = "a\nb\nc" };
        Assert.True(pad.ShowOverviewRuler);
        Assert.True(pad.OverviewRulerGutterWidth > 0);
        pad.ToggleOverviewRuler();
        Assert.False(pad.ShowOverviewRuler);
        Assert.Equal(0, pad.OverviewRulerGutterWidth);
    }

    [Fact]
    public void FindMatchCount_AndLines()
    {
        CodeEditorRegistration.RegisterDefaults();
        var pad = new CodeEditor { Geometry = new Rect(0, 0, 400, 200), Value = "one\ntwo one\none" };
        pad.FindQuery = "one";
        Assert.Equal(3, pad.FindMatchCount);
        var lines = pad.GetFindMatchLines();
        Assert.Equal(new[] { 0, 1, 2 }, lines);
        Assert.True(pad.FindNext());
        Assert.Equal(1, pad.FindMatchIndex);
    }

    [Fact]
    public void FindPanelVisible_Toggle()
    {
        var pad = new CodeEditor();
        Assert.False(pad.FindPanelVisible);
        pad.ToggleFindPanel();
        Assert.True(pad.FindPanelVisible);
    }

    [Fact]
    public void Decoration_SupportOverviewColor()
    {
        CodeEditorRegistration.RegisterDefaults();
        var pad = new CodeEditor { Geometry = new Rect(0, 0, 400, 200), Value = "a\nb" };
        pad.SetDecoration(new CodeEditorLineDecoration
        {
            Id = "x",
            Line = 1,
            OverviewRulerColor = Color.FromRgb(255, 0, 0),
            GutterColor = Color.FromRgb(0, 255, 0),
        });
        Assert.Equal(Color.FromRgb(255, 0, 0), pad.GetDecorationsAt(1)[0].OverviewRulerColor);
    }

    [Fact]
    public void ThemeRegistry_RegisterCustomTheme()
    {
        CodeEditorThemeRegistry.Register("phase5-test", new CodeEditorTheme
        {
            EditorBackground = Color.FromRgb(1, 2, 3),
            EditorForeground = Color.FromRgb(4, 5, 6),
            OverviewRulerBackground = Color.FromRgb(7, 8, 9),
        });
        var theme = CodeEditorThemeRegistry.Get("phase5-test");
        Assert.Equal(1, theme.EditorBackground.R);
        Assert.Equal(9, theme.OverviewRulerBackground.B);
    }

    [Fact]
    public void ResolveCursor_ArrowOnOverviewRuler()
    {
        CodeEditorRegistration.RegisterDefaults();
        var pad = new CodeEditor
        {
            Geometry = new Rect(0, 0, 400, 200),
            Value = "hello",
            ShowOverviewRuler = true,
            ShowScrollBars = false,
            ShowGlyphMargin = false,
            ShowLineNumbers = false,
            ShowFolding = false,
        };
        Assert.Equal(CursorKind.Arrow, pad.ResolveCursorAt(new Point(395, 40)));
        Assert.Equal(CursorKind.Text, pad.ResolveCursorAt(new Point(40, 40)));
    }
}
