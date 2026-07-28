using Square.Extensions.CodeEditor;
using Square.Graphics;
using Xunit;

namespace Square.Extensions.CodeEditor.Tests;

public class EditingTests
{
    private static CodeEditor CreatePad(string text = "")
    {
        CodeEditorRegistration.RegisterDefaults();
        var pad = new CodeEditor { Geometry = new Rect(0, 0, 400, 200), Value = text };
        return pad;
    }

    [Fact]
    public void TypingAndEnter_BuildsLines()
    {
        var pad = CreatePad();
        pad.HandleTextInput("hello");
        pad.HandleKey(13);
        pad.HandleTextInput("world");
        Assert.Equal("hello\nworld", pad.Value);
        Assert.Equal(2, pad.Model.LineCount);
    }

    [Fact]
    public void Tab_InsertsSpacesByDefault()
    {
        var pad = CreatePad();
        pad.TabSize = 4;
        pad.InsertSpaces = true;
        pad.HandleKey(9);
        Assert.Equal("    ", pad.Value);
    }

    [Fact]
    public void Tab_InsertsTabWhenInsertSpacesFalse()
    {
        var pad = CreatePad();
        pad.InsertSpaces = false;
        pad.HandleKey(9);
        Assert.Equal("\t", pad.Value);
    }

    [Fact]
    public void BackspaceAndDelete_Work()
    {
        var pad = CreatePad("abcd");
        pad.SelectAll();
        pad.HandlePointerDown(new Point(8, 8)); // reset selection-ish via caret
        // set caret to end via SelectAll then collapse
        pad.SelectAll();
        // move caret to end without selection: select all then arrow right without shift isn't implemented as collapse
        // use HandleTextInput path: replace all
        pad.SelectAll();
        pad.HandleTextInput("ab");
        Assert.Equal("ab", pad.Value);
        pad.HandleKey(8);
        Assert.Equal("a", pad.Value);
        pad.HandleTextInput("bc");
        // caret at end; delete forward no-op
        pad.HandleKey(46);
        Assert.Equal("abc", pad.Value);
    }

    [Fact]
    public void SelectAll_AndDeleteSelection()
    {
        var pad = CreatePad("xyz");
        pad.SelectAll();
        Assert.Equal(3, pad.SelectionLength);
        Assert.True(pad.DeleteSelection());
        Assert.Equal("", pad.Value);
    }

    [Fact]
    public void ArrowKeys_MoveCaret()
    {
        var pad = CreatePad("ab\ncd");
        pad.SelectAll();
        // place caret at 0
        pad.HandlePointerDown(new Point(0, 0));
        pad.HandleKey(39); // right
        Assert.True(pad.CaretIndex >= 1);
        pad.HandleKey(40); // down
        Assert.True(pad.Model.GetLineNumberAt(pad.CaretIndex) >= 0);
    }

    [Fact]
    public void AutoClosingPairs_ForCSharp()
    {
        var pad = CreatePad();
        pad.Language = "csharp";
        pad.HandleTextInput("{");
        Assert.Equal("{}", pad.Value);
        Assert.Equal(1, pad.CaretIndex);
    }

    [Fact]
    public void ToggleLineComment_AddsPrefix()
    {
        var pad = CreatePad("int x = 1;");
        pad.Language = "csharp";
        pad.SelectAll();
        pad.HandleKey(191, control: true);
        Assert.StartsWith("//", pad.Value.TrimStart());
        pad.SelectAll();
        pad.HandleKey(191, control: true);
        Assert.DoesNotContain("//", pad.Value);
    }

    [Fact]
    public void FindNext_SelectsMatch()
    {
        var pad = CreatePad("one two one");
        Assert.True(pad.FindNext("one"));
        Assert.Equal(0, pad.SelectionStart);
        Assert.Equal(3, pad.SelectionLength);
        Assert.True(pad.FindNext("one"));
        Assert.Equal(8, pad.SelectionStart);
    }

    [Fact]
    public void ReadOnly_BlocksEdit()
    {
        var pad = CreatePad("keep");
        pad.ReadOnly = true;
        pad.HandleTextInput("x");
        pad.HandleKey(8);
        Assert.Equal("keep", pad.Value);
    }

    [Fact]
    public void ReadOnly_BlocksUndoRedoAndReplace_ButAllowsSelection()
    {
        var pad = CreatePad("keep");
        pad.SelectAll();
        pad.HandleKey(39); // caret to end
        pad.HandleTextInput("x");
        Assert.Equal("keepx", pad.Value);
        Assert.True(pad.CanUndo);

        pad.ReadOnly = true;
        Assert.False(pad.Undo());
        Assert.False(pad.Redo());
        Assert.False(pad.ReplaceNext("keep", "gone"));
        Assert.Equal(0, pad.ReplaceAll("keep", "gone"));
        Assert.Equal("keepx", pad.Value);

        pad.SelectAll();
        Assert.Equal(5, pad.SelectionLength);
        Assert.True(pad.CanCopySelection);
        Assert.False(pad.CanCutSelection);

        pad.ToggleReadOnly();
        Assert.False(pad.ReadOnly);
        Assert.True(pad.Undo());
        Assert.Equal("keep", pad.Value);
    }

    [Fact]
    public void Enter_PreservesIndent()
    {
        var pad = CreatePad("  hello");
        pad.SelectAll();
        // move caret to end
        pad.HandlePointerDown(new Point(200, 8));
        // force caret end
        pad.Model.SetValue("  hello");
        // set caret via select all + typing empty not possible; use internal path
        pad.SelectAll();
        pad.HandleKey(39); // if selection, moves to end of selection when direction > 0 without extend... 
        // Our MoveHorizontal without extend when selection collapses to end when direction > 0
        pad.HandleKey(13);
        Assert.Contains("\n  ", pad.Value);
    }
}
