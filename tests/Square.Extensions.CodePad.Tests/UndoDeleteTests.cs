using Square.Extensions.CodePad;
using Square.Graphics;
using Xunit;

namespace Square.Extensions.CodePad.Tests;

public class UndoDeleteTests
{
    private static CodePad Create(string text)
    {
        CodePadRegistration.RegisterDefaults();
        return new CodePad
        {
            Geometry = new Rect(0, 0, 400, 200),
            Value = text,
            ShowLineNumbers = false,
            ShowFolding = false,
            ShowGlyphMargin = false,
            ShowOverviewRuler = false,
        };
    }

    [Fact]
    public void SingleCursor_DeleteThenUndo_RestoresTextAndCaretAtDeleteStart()
    {
        var pad = Create("abcdef");
        pad.SelectRange(2, 2); // caret before 'c'
        Assert.Equal(2, pad.CaretIndex);

        pad.HandleKey(46); // Del -> abdef, caret stays 2
        pad.HandleKey(46); // Del -> abef
        pad.HandleKey(46); // Del -> abf
        Assert.Equal("abf", pad.Value);
        Assert.Equal(2, pad.CaretIndex);

        Assert.True(pad.Undo());
        Assert.Equal("abcdef", pad.Value);
        // 删除前光标在 2，撤销后应回到 2（删除起点 = 删除前位置）
        Assert.Equal(2, pad.CaretIndex);
    }

    [Fact]
    public void SingleCursor_BackspaceThenUndo_RestoresCaretToPreBackspacePosition()
    {
        var pad = Create("abcdef");
        pad.SelectRange(4, 4); // before 'e' — will backspace 'd' then 'c'
        pad.HandleKey(8);
        Assert.Equal(3, pad.CaretIndex);
        pad.HandleKey(8);
        Assert.Equal("abef", pad.Value);
        Assert.Equal(2, pad.CaretIndex);

        Assert.True(pad.Undo());
        Assert.Equal("abcdef", pad.Value);
        // 连续 Backspace 前光标在 4（不是 coalesce 的起点 2）
        Assert.Equal(4, pad.CaretIndex);
    }

    [Fact]
    public void MultiCursor_Delete_DeletesOneCharAtEachCursor()
    {
        var pad = Create("aabbcc");
        pad.SetCursors(
        [
            CodePadCursor.Collapsed(0),
            CodePadCursor.Collapsed(2),
            CodePadCursor.Collapsed(4),
        ]);
        pad.HandleKey(46);
        Assert.Equal("abc", pad.Value);
        Assert.Equal(3, pad.CursorCount);
    }

    [Fact]
    public void MultiCursor_Delete_ThenUndo_RestoresAllCursors()
    {
        var pad = Create("aabbcc");
        pad.SetCursors(
        [
            CodePadCursor.Collapsed(0),
            CodePadCursor.Collapsed(2),
            CodePadCursor.Collapsed(4),
        ]);
        pad.HandleKey(46);
        Assert.Equal("abc", pad.Value);
        Assert.Equal(3, pad.CursorCount);

        Assert.True(pad.Undo());
        Assert.Equal("aabbcc", pad.Value);
        // 撤销后应恢复多个光标（在各删除点）
        Assert.True(pad.CursorCount >= 2, $"expected multi cursors after undo, got {pad.CursorCount}");
        Assert.Contains(0, pad.Cursors.Select(c => c.Caret));
        Assert.Contains(2, pad.Cursors.Select(c => c.Caret));
        Assert.Contains(4, pad.Cursors.Select(c => c.Caret));
    }

    [Fact]
    public void MultiCursor_DeleteTwice_ThenUndo_RestoresAll()
    {
        var pad = Create("aabbcc");
        pad.SetCursors(
        [
            CodePadCursor.Collapsed(0),
            CodePadCursor.Collapsed(2),
            CodePadCursor.Collapsed(4),
        ]);
        pad.HandleKey(46);
        Assert.Equal("abc", pad.Value);
        pad.HandleKey(46);
        Assert.Equal("", pad.Value);

        Assert.True(pad.Undo());
        Assert.Equal("abc", pad.Value);
        Assert.True(pad.Undo());
        Assert.Equal("aabbcc", pad.Value);
    }

    [Fact]
    public void MultiCursor_TypeThenUndo_RestoresCursors()
    {
        var pad = Create("a\nb\nc");
        pad.SetCursors(
        [
            CodePadCursor.Collapsed(0),
            CodePadCursor.Collapsed(2),
            CodePadCursor.Collapsed(4),
        ]);
        pad.HandleTextInput("X");
        Assert.Equal("Xa\nXb\nXc", pad.Value.Replace("\r", ""));
        Assert.True(pad.Undo());
        Assert.Equal("a\nb\nc", pad.Value.Replace("\r", ""));
        Assert.True(pad.CursorCount >= 2);
    }

    [Fact]
    public void SingleCursor_InsertThenUndo_CaretAtInsertStart()
    {
        var pad = Create("ab");
        pad.SelectRange(1, 1);
        pad.HandleTextInput("XY");
        Assert.Equal("aXYb", pad.Value);
        Assert.True(pad.Undo());
        Assert.Equal("ab", pad.Value);
        Assert.Equal(1, pad.CaretIndex);
    }
}
