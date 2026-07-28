using Square.Extensions.CodePad;
using Xunit;

namespace Square.Extensions.CodePad.Tests;

public class TextModelTests
{
    [Fact]
    public void SetValue_TracksLines()
    {
        var model = new CodePad().Model;
        model.SetValue("a\nb\nc");
        Assert.Equal(3, model.LineCount);
        Assert.Equal("a", model.GetLineContent(0));
        Assert.Equal("b", model.GetLineContent(1));
        Assert.Equal("c", model.GetLineContent(2));
        Assert.Equal(0, model.GetLineStart(0));
        Assert.Equal(2, model.GetLineStart(1));
        Assert.Equal(4, model.GetLineStart(2));
    }

    [Fact]
    public void Replace_InsertsAndDeletes()
    {
        var pad = new CodePad();
        pad.Model.SetValue("hello");
        pad.Model.Replace(5, 0, " world");
        Assert.Equal("hello world", pad.Model.GetValue());
        pad.Model.Replace(5, 6, "");
        Assert.Equal("hello", pad.Model.GetValue());
    }

    [Fact]
    public void PositionMapping_RoundTrips()
    {
        var model = new CodePad().Model;
        model.SetValue("ab\ncd\nef");
        Assert.Equal((0, 0), model.GetPositionAt(0));
        Assert.Equal((0, 2), model.GetPositionAt(2));
        Assert.Equal((1, 0), model.GetPositionAt(3));
        Assert.Equal((2, 2), model.GetPositionAt(8));
        Assert.Equal(3, model.GetOffsetAt(1, 0));
        Assert.Equal(5, model.GetOffsetAt(1, 2));
    }

    [Fact]
    public void UndoRedo_RestoresText()
    {
        var pad = new CodePad { Geometry = new Square.Graphics.Rect(0, 0, 400, 200) };
        pad.Model.SetValue("abc");
        pad.SelectAll();
        pad.HandleTextInput("hello");
        Assert.Equal("hello", pad.Value);
        Assert.True(pad.Undo());
        Assert.Equal("abc", pad.Value);
        Assert.True(pad.Redo());
        Assert.Equal("hello", pad.Value);
    }

    [Fact]
    public void LargeDocument_LineAccessIsStable()
    {
        var lines = Enumerable.Range(0, 5000).Select(i => $"line-{i}");
        var text = string.Join("\n", lines);
        var model = new CodePad().Model;
        model.SetValue(text);
        Assert.Equal(5000, model.LineCount);
        Assert.Equal("line-0", model.GetLineContent(0));
        Assert.Equal("line-2500", model.GetLineContent(2500));
        Assert.Equal("line-4999", model.GetLineContent(4999));
        model.Replace(model.GetLineStart(2500), model.GetLineContent(2500).Length, "changed");
        Assert.Equal("changed", model.GetLineContent(2500));
        Assert.Equal(5000, model.LineCount);
    }

    [Fact]
    public void ManyEdits_PieceTableKeepsContent()
    {
        var model = new CodePad().Model;
        model.SetValue("");
        for (var i = 0; i < 200; i++)
            model.Replace(model.Length, 0, i % 10 == 0 ? "\n" : "x");
        var value = model.GetValue();
        Assert.Equal(200, value.Length);
        Assert.True(model.LineCount > 1);
    }
}
