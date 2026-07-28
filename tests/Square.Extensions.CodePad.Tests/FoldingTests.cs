using Square.Extensions.CodePad;
using Square.Graphics;
using Xunit;

namespace Square.Extensions.CodePad.Tests;

public class FoldingTests
{
    private static CodePad Create(string text, string language = "csharp")
    {
        CodePadRegistration.RegisterDefaults();
        return new CodePad
        {
            Geometry = new Rect(0, 0, 400, 300),
            Language = language,
            Value = text,
            ShowFolding = true,
            ShowLineNumbers = true,
        };
    }

    [Fact]
    public void BracketRegions_DetectMultiLineBlocks()
    {
        var pad = Create("class A {\n  void M() {\n    int x = 1;\n  }\n}\n");
        Assert.True(pad.FoldRegionCount >= 1);
    }

    [Fact]
    public void CollapseExpand_HidesInnerLines()
    {
        var pad = Create("{\n  a\n  b\n}\n");
        Assert.True(pad.FoldRegionCount >= 1);
        Assert.True(pad.CollapseFoldAt(0));
        pad.HandlePointerDown(new Point(80, 20));
        Assert.True(pad.ExpandFoldAt(0));
        Assert.True(pad.CollapseFoldAt(0));
        Assert.True(pad.ExpandFoldAt(0));
    }

    [Fact]
    public void ToggleFolding_PropertyDisablesGutter()
    {
        var pad = Create("{\n  x\n}\n");
        Assert.True(pad.ShowFolding);
        Assert.True(pad.FoldingGutterWidth > 0);
        pad.ShowFolding = false;
        Assert.Equal(0, pad.FoldingGutterWidth);
        pad.ToggleFolding();
        Assert.True(pad.ShowFolding);
    }

    [Fact]
    public void CollapseAll_AndExpandAll()
    {
        var pad = Create("{\n  {\n    x\n  }\n}\n");
        pad.CollapseAllFolds();
        pad.ExpandAllFolds();
        Assert.True(pad.FoldRegionCount >= 1);
    }

    [Fact]
    public void CollapseFold_PreservesExactSelectionOffsets()
    {
        var pad = Create("aa\n{\n  bb\n  cc\n}\ndd\n");
        pad.SelectAll();
        var start = pad.SelectionStart;
        var length = pad.SelectionLength;
        Assert.True(length > 0);

        Assert.True(pad.CollapseFoldAt(1));
        Assert.Equal(start, pad.SelectionStart);
        Assert.Equal(length, pad.SelectionLength);

        Assert.True(pad.ExpandFoldAt(1));
        Assert.Equal(start, pad.SelectionStart);
        Assert.Equal(length, pad.SelectionLength);
    }

    [Fact]
    public void CollapseFold_DoesNotMoveSelectionEnds_EvenIfOnFoldBody()
    {
        var pad = Create("{\n  line1\n  line2\n}\n");
        pad.SelectAll();
        var start = pad.SelectionStart;
        var len = pad.SelectionLength;
        var caret = pad.CaretIndex;
        Assert.True(pad.CollapseFoldAt(0));
        Assert.Equal(start, pad.SelectionStart);
        Assert.Equal(len, pad.SelectionLength);
        Assert.Equal(caret, pad.CaretIndex);
    }

    [Fact]
    public void GutterFoldClick_DoesNotStartDragOrClearSelection()
    {
        var pad = Create("hello world\n{\n  a\n  b\n}\n");
        pad.ShowGlyphMargin = false;
        pad.ShowLineNumbers = true;
        pad.ShowFolding = true;
        pad.SelectAll();
        var selected = pad.SelectionLength;
        Assert.True(selected > 0);

        var foldX = pad.LineNumberGutterWidth + pad.FoldingGutterWidth / 2f;
        var foldY = 12 + 18;
        Assert.False(pad.HandlePointerDown(new Point(foldX, foldY)));
        Assert.Equal(selected, pad.SelectionLength);

        pad.HandlePointerDown(new Point(foldX, foldY));
        pad.HandlePointerDown(new Point(foldX, foldY));
        Assert.Equal(selected, pad.SelectionLength);
    }

    [Fact]
    public void ToggleFold_MultipleTimes_PreservesSelectAllRange()
    {
        var pad = Create("prefix\n{\n  a\n  b\n}\nsuffix\n");
        pad.SelectAll();
        var start = pad.SelectionStart;
        var length = pad.SelectionLength;
        var caret = pad.CaretIndex;
        for (var i = 0; i < 5; i++)
            pad.ToggleFoldAt(1);
        Assert.Equal(start, pad.SelectionStart);
        Assert.Equal(length, pad.SelectionLength);
        Assert.Equal(caret, pad.CaretIndex);
    }

    [Fact]
    public void SampleLikeDocument_SelectAll_ThenCollapse_KeepsRange()
    {
        var text =
            "using System;\n\nnamespace Demo;\n\n/// <summary>CodePad C# sample.</summary>\n" +
            "public sealed class Program\n{\n    public static void Main(string[] args)\n    {\n" +
            "        var message = \"Hello, CodePad!\";\n        Console.WriteLine(message);\n    }\n}\n";
        var pad = Create(text);
        pad.SelectAll();
        var start = pad.SelectionStart;
        var length = pad.SelectionLength;
        Assert.True(length > 50);

        for (var line = 0; line < pad.Model.LineCount; line++)
        {
            if (pad.CollapseFoldAt(line))
            {
                Assert.Equal(start, pad.SelectionStart);
                Assert.Equal(length, pad.SelectionLength);
            }
        }
        pad.ExpandAllFolds();
        Assert.Equal(start, pad.SelectionStart);
        Assert.Equal(length, pad.SelectionLength);
    }

    [Fact]
    public void HtmlTags_CreateFoldRegions()
    {
        var pad = Create("<div>\n  <span>hi</span>\n</div>\n", "html");
        Assert.True(pad.FoldRegionCount >= 1);
        Assert.True(pad.CollapseFoldAt(0));
    }

    [Fact]
    public void Engine_DocumentVisualMapping()
    {
        var engine = new FoldingEngine();
        var model = new CodePad { Value = "{\n  a\n  b\n}\n" }.Model;
        engine.Recompute(model, LanguageConfiguration.CLike("//", null), "csharp");
        Assert.True(engine.Regions.Count >= 1);
        var start = engine.Regions[0].StartLine;
        Assert.True(engine.ToggleAt(start));
        Assert.True(engine.IsCollapsed(start));
        Assert.True(engine.VisibleLineCount < model.LineCount);
        Assert.True(engine.IsLineHidden(start + 1));
        var visual = engine.DocumentToVisual(start);
        Assert.Equal(start, engine.VisualToDocument(visual));
    }
}
