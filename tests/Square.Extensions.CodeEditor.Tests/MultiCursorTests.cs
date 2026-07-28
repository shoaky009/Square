using Square.Extensions.CodeEditor;
using Square.Graphics;
using Xunit;

namespace Square.Extensions.CodeEditor.Tests;

public class MultiCursorTests
{
    private static CodeEditor Create(string text = "one\ntwo\nthree")
    {
        CodeEditorRegistration.RegisterDefaults();
        return new CodeEditor
        {
            Geometry = new Rect(0, 0, 400, 300),
            Value = text,
            ShowLineNumbers = false,
            ShowFolding = false,
            ShowGlyphMargin = false,
            ShowOverviewRuler = false,
        };
    }

    [Fact]
    public void AddCursor_IncreasesCursorCount()
    {
        var pad = Create();
        Assert.Equal(1, pad.CursorCount);
        Assert.False(pad.HasMultiCursors);
        Assert.True(pad.AddCursor(4)); // start of "two"
        Assert.True(pad.HasMultiCursors);
        Assert.Equal(2, pad.CursorCount);
        // duplicate ignored
        Assert.False(pad.AddCursor(4));
        Assert.Equal(2, pad.CursorCount);
    }

    [Fact]
    public void ClearExtraCursors_ReturnsToSingle()
    {
        var pad = Create();
        pad.AddCursor(4);
        pad.AddCursor(8);
        Assert.True(pad.CursorCount >= 2);
        pad.ClearExtraCursors();
        Assert.False(pad.HasMultiCursors);
        Assert.Equal(1, pad.CursorCount);
    }

    [Fact]
    public void AltClick_AddsCursor_WithoutStartingDrag()
    {
        var pad = Create("hello\nworld");
        pad.Focus();
        // primary at start
        pad.HandlePointerDown(new Point(20, 12));
        pad.HandlePointerUp(new Point(20, 12));
        // Alt+click second line
        var started = pad.HandlePointerDown(new Point(20, 30), extendSelection: false, addCursor: true);
        Assert.False(started);
        Assert.True(pad.HasMultiCursors);
    }

    [Fact]
    public void Typing_WithMultiCursors_InsertsAtEach()
    {
        var pad = Create("ab\ncd");
        // caret at 0, add at start of second line (3 = after "ab\n")
        pad.SelectRange(0, 0);
        Assert.True(pad.AddCursor(3));
        pad.HandleTextInput("X");
        Assert.Equal("Xab\nXcd", pad.Value.Replace("\r", ""));
        Assert.True(pad.CursorCount >= 2);
    }

    [Fact]
    public void Delete_WithMultiCursors_DeletesAtEach()
    {
        var pad = Create("ab\ncd");
        pad.SelectRange(0, 0);
        pad.AddCursor(3);
        pad.HandleKey(46); // Delete
        Assert.Equal("b\nd", pad.Value.Replace("\r", ""));
    }

    [Fact]
    public void Backspace_WithMultiCursors_DeletesBeforeEach()
    {
        var pad = Create("ab\ncd");
        // carets after first char of each line
        pad.SelectRange(1, 1);
        pad.AddCursor(4); // after 'c' in "cd" (ab\n = 3, +1 = 4)
        pad.HandleKey(8);
        Assert.Equal("b\nd", pad.Value.Replace("\r", ""));
    }

    [Fact]
    public void Escape_ClearsExtraCursors()
    {
        var pad = Create();
        pad.AddCursor(4);
        Assert.True(pad.HasMultiCursors);
        pad.HandleKey(27);
        Assert.False(pad.HasMultiCursors);
    }

    [Fact]
    public void NormalClick_ClearsExtraCursors()
    {
        var pad = Create();
        pad.AddCursor(4);
        Assert.True(pad.HasMultiCursors);
        pad.HandlePointerDown(new Point(20, 12));
        Assert.False(pad.HasMultiCursors);
    }

    [Fact]
    public void SetCursors_ReplacesState()
    {
        var pad = Create("aaaa");
        pad.SetCursors(
        [
            CodeEditorCursor.Collapsed(0),
            CodeEditorCursor.Collapsed(2),
            CodeEditorCursor.Collapsed(3),
        ]);
        Assert.Equal(3, pad.CursorCount);
        pad.HandleTextInput("Z");
        // carets at 0,2,3 of "aaaa" → Z aa Z a Z a
        Assert.Equal("ZaaZaZa", pad.Value);
    }

    [Fact]
    public void MultiCursor_InsertAtLineStarts()
    {
        var pad = Create("a\nb\nc");
        // offsets: 0, 2 (after a\n), 4 (after b\n)
        pad.SetCursors(
        [
            CodeEditorCursor.Collapsed(0),
            CodeEditorCursor.Collapsed(2),
            CodeEditorCursor.Collapsed(4),
        ]);
        pad.HandleTextInput(">");
        Assert.Equal(">a\n>b\n>c", pad.Value.Replace("\r", ""));
    }
}
