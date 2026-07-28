using Square.Extensions.CodeEditor;
using Square.Graphics;
using Xunit;

namespace Square.Extensions.CodeEditor.Tests;

public class MultiCursorSelectionTests
{
    private static CodeEditor Create(string text = "hello world\nfoo bar")
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
    public void MultiCursor_ShiftRight_ExtendsEachSelection()
    {
        var pad = Create("abcd\nefgh");
        // carets at start of each line: 0 and 5
        pad.SetCursors(
        [
            CodeEditorCursor.Collapsed(0),
            CodeEditorCursor.Collapsed(5),
        ]);
        Assert.Equal(2, pad.CursorCount);

        pad.HandleKey(39, shift: true); // Shift+Right
        var cursors = pad.Cursors;
        Assert.Equal(2, cursors.Count);
        Assert.All(cursors, c => Assert.Equal(1, c.SelectionLength));
        Assert.Contains(cursors, c => c.SelectionStart == 0 && c.Caret == 1);
        Assert.Contains(cursors, c => c.SelectionStart == 5 && c.Caret == 6);
    }

    [Fact]
    public void MultiCursor_ShiftCtrlRight_SelectsWordAtEach()
    {
        var pad = Create("hello world\nfoo bar");
        // carets at 0 ("hello") and after newline (12 = start of "foo")
        pad.SetCursors(
        [
            CodeEditorCursor.Collapsed(0),
            CodeEditorCursor.Collapsed(12),
        ]);
        pad.HandleKey(39, shift: true, control: true); // Shift+Ctrl+Right
        var cursors = pad.Cursors;
        Assert.Equal(2, cursors.Count);
        Assert.All(cursors, c => Assert.True(c.SelectionLength > 1));
        // word select may include trailing space depending on NextWord
        var texts = cursors.Select(c => pad.Value.Substring(c.SelectionStart, c.SelectionLength).TrimEnd()).ToArray();
        Assert.Contains("hello", texts);
        Assert.Contains("foo", texts);
    }

    [Fact]
    public void MultiCursor_ShiftLeft_ExtendsBackward()
    {
        var pad = Create("abcd");
        pad.SetCursors([CodeEditorCursor.Collapsed(2), CodeEditorCursor.Collapsed(4)]);
        pad.HandleKey(37, shift: true); // Shift+Left
        var cursors = pad.Cursors;
        Assert.All(cursors, c => Assert.Equal(1, c.SelectionLength));
    }

    [Fact]
    public void MultiCursor_ArrowWithoutShift_CollapsesSelections()
    {
        var pad = Create("abcd");
        pad.SetCursors(
        [
            new CodeEditorCursor(2, 0), // selected "ab"
            CodeEditorCursor.Collapsed(3),
        ]);
        pad.HandleKey(39, shift: false); // Right without shift
        var cursors = pad.Cursors;
        Assert.All(cursors, c => Assert.True(c.IsCollapsed));
    }

    [Fact]
    public void MultiCursor_ShiftHome_SelectsToLineStart()
    {
        var pad = Create("hello\nworld");
        // carets mid-line
        pad.SetCursors(
        [
            CodeEditorCursor.Collapsed(3),  // in "hello"
            CodeEditorCursor.Collapsed(9),  // in "world" (hello\n = 6, +3)
        ]);
        pad.HandleKey(36, shift: true); // Shift+Home
        var cursors = pad.Cursors;
        Assert.All(cursors, c => Assert.True(c.SelectionLength > 0));
        Assert.All(cursors, c => Assert.True(c.Caret <= c.Anchor || c.SelectionStart == c.Caret));
    }

    [Fact]
    public void MultiCursor_TypeAfterShiftSelect_ReplacesEachSelection()
    {
        var pad = Create("aa bb\ncc dd");
        pad.SetCursors(
        [
            CodeEditorCursor.Collapsed(0),
            CodeEditorCursor.Collapsed(6),
        ]);
        pad.HandleKey(39, shift: true, control: true); // select word at each
        pad.HandleTextInput("X");
        Assert.StartsWith("X", pad.Value);
        Assert.Contains("X", pad.Value);
        Assert.DoesNotContain("aa", pad.Value);
        Assert.DoesNotContain("cc", pad.Value);
    }
}
