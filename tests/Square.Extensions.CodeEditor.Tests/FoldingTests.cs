using Square.Extensions.CodeEditor;
using Square.Graphics;
using Xunit;

namespace Square.Extensions.CodeEditor.Tests;

public class FoldingTests
{
    private static CodeEditor Create(string text, string language = "csharp")
    {
        CodeEditorRegistration.RegisterDefaults();
        return new CodeEditor
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
            "using System;\n\nnamespace Demo;\n\n/// <summary>CodeEditor C# sample.</summary>\n" +
            "public sealed class Program\n{\n    public static void Main(string[] args)\n    {\n" +
            "        var message = \"Hello, CodeEditor!\";\n        Console.WriteLine(message);\n    }\n}\n";
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
        var model = new CodeEditor { Value = "{\n  a\n  b\n}\n" }.Model;
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

    [Fact]
    public void CurlyBraceFold_UsesBlockPlaceholderAtOpeningBrace()
    {
        var engine = new FoldingEngine();
        var model = new CodeEditor { Value = "for(var i=0;i<10;i++){\n  Run();\n}\n" }.Model;

        engine.Recompute(model, LanguageConfiguration.CLike("//", null), "csharp");

        var region = Assert.Single(engine.Regions);
        Assert.Equal(21, region.StartColumn);
        Assert.Equal("{...}", region.Placeholder);
    }

    [Fact]
    public void XmlFold_UsesMarkupPlaceholderAtOpeningTag()
    {
        var engine = new FoldingEngine();
        var model = new CodeEditor { Value = "  <section id=\"main\">\n    text\n  </section>\n" }.Model;

        engine.Recompute(model, LanguageConfiguration.CLike("//", null), "xml");

        var region = Assert.Single(engine.Regions);
        Assert.Equal(20, region.StartColumn);
        Assert.Equal(" ...>", region.Placeholder);
    }

    [Fact]
    public void XmlFold_WithMultiLineAttributes_KeepsOnlyOpeningTagFirstLine()
    {
        var engine = new FoldingEngine();
        var model = new CodeEditor
        {
            Value = "<div id=\"main\"\n  class=\"panel\"\n  data-kind=\"sample\">\n  text\n</div>\n",
        }.Model;

        engine.Recompute(model, LanguageConfiguration.CLike("//", null), "xml");

        var region = Assert.Single(engine.Regions);
        Assert.Equal(0, region.StartLine);
        Assert.Equal(4, region.EndLine);
        Assert.Equal("<div id=\"main\"".Length, region.StartColumn);
        Assert.Equal(" ...>", region.Placeholder);

        Assert.True(engine.ToggleAt(0));
        Assert.True(engine.IsLineHidden(1));
        Assert.True(engine.IsLineHidden(2));
        Assert.True(engine.IsLineHidden(3));
        Assert.True(engine.IsLineHidden(4));
    }

    [Fact]
    public void XmlFold_WithMultiLineSelfClosingAttributes_CreatesAttributeRegion()
    {
        var engine = new FoldingEngine();
        var model = new CodeEditor
        {
            Value = "<input type=\"text\"\n  name=\"query\"\n  required />\n",
        }.Model;

        engine.Recompute(model, LanguageConfiguration.CLike("//", null), "xml");

        var region = Assert.Single(engine.Regions);
        Assert.Equal(0, region.StartLine);
        Assert.Equal(2, region.EndLine);
        Assert.Equal("<input type=\"text\"".Length, region.StartColumn);
        Assert.Equal(" .../>", region.Placeholder);
    }

    [Fact]
    public void JavaScriptArrayFold_UsesArrayPlaceholder()
    {
        var engine = new FoldingEngine();
        var model = new CodeEditor { Value = "var items = [\n  first,\n  second\n];\n" }.Model;

        engine.Recompute(model, LanguageConfiguration.CLike("//", null), "javascript");

        var region = Assert.Single(engine.Regions);
        Assert.Equal(12, region.StartColumn);
        Assert.Equal("[...]", region.Placeholder);
    }

    [Fact]
    public void JsonArrayFold_UsesArrayPlaceholder()
    {
        var engine = new FoldingEngine();
        var model = new CodeEditor
        {
            Value = "{\n  \"items\": [\n    1,\n    2\n  ]\n}\n",
        }.Model;

        engine.Recompute(model, LanguageConfiguration.CLike(null, null), "json");

        var arrayRegion = Assert.Single(engine.Regions, region => region.StartLine == 1);
        Assert.Equal(11, arrayRegion.StartColumn);
        Assert.Equal("[...]", arrayRegion.Placeholder);
    }

    [Fact]
    public void PythonListFold_UsesArrayPlaceholder()
    {
        CodeEditorRegistration.RegisterDefaults();
        var engine = new FoldingEngine();
        var model = new CodeEditor
        {
            Value = "items = [\n    1,\n    2\n]\n",
        }.Model;

        engine.Recompute(model, LanguageRegistry.ResolveConfiguration("python"), "python");

        var region = Assert.Single(engine.Regions);
        Assert.Equal(8, region.StartColumn);
        Assert.Equal("[...]", region.Placeholder);
    }

    [Fact]
    public void PythonIndentedBlocks_CreateNestedFoldRegions()
    {
        CodeEditorRegistration.RegisterDefaults();
        var engine = new FoldingEngine();
        var model = new CodeEditor
        {
            Value = "def fib(n):\n    for i in range(n):\n        print(i)\n    return n\n\nprint(fib(3))\n",
        }.Model;

        engine.Recompute(model, LanguageRegistry.ResolveConfiguration("python"), "python");

        var function = Assert.Single(engine.Regions, region => region.StartLine == 0);
        var loop = Assert.Single(engine.Regions, region => region.StartLine == 1);
        Assert.Equal(4, function.EndLine);
        Assert.Equal(2, loop.EndLine);
        Assert.Equal("...", function.Placeholder);
        Assert.Equal("...", loop.Placeholder);
    }
}
