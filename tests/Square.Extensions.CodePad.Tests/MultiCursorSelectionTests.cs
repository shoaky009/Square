using Square.Extensions.CodePad;
using Square.Graphics;
using Xunit;

namespace Square.Extensions.CodePad.Tests;

public class MultiCursorSelectionTests
{
    private static CodePad Create(string text = "hello world\nfoo bar")
    {
        CodePadRegistration.RegisterDefaults();
        return new CodePad
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
            CodePadCursor.Collapsed(0),
            CodePadCursor.Collapsed(5),
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
            CodePadCursor.Collapsed(0),
            CodePadCursor.Collapsed(12),
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
        pad.SetCursors([CodePadCursor.Collapsed(2), CodePadCursor.Collapsed(4)]);
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
            new CodePadCursor(2, 0), // selected "ab"
            CodePadCursor.Collapsed(3),
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
            CodePadCursor.Collapsed(3),  // in "hello"
            CodePadCursor.Collapsed(9),  // in "world" (hello\n = 6, +3)
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
            CodePadCursor.Collapsed(0),
            CodePadCursor.Collapsed(6),
        ]);
        pad.HandleKey(39, shift: true, control: true); // select word at each
        pad.HandleTextInput("X");
        Assert.StartsWith("X", pad.Value);
        Assert.Contains("X", pad.Value);
        Assert.DoesNotContain("aa", pad.Value);
        Assert.DoesNotContain("cc", pad.Value);
    }
}
