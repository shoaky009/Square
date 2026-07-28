using Square.Extensions.CodePad;
using Square.Graphics;
using Xunit;

namespace Square.Extensions.CodePad.Tests;

public class FoldEditTests
{
    private static CodePad Create(string text)
    {
        CodePadRegistration.RegisterDefaults();
        return new CodePad
        {
            Geometry = new Rect(0, 0, 400, 300),
            Language = "csharp",
            Value = text,
            ShowFolding = true,
            ShowLineNumbers = true,
        };
    }

    [Fact]
    public void SelectCollapsedFoldAt_SelectsEntireFoldRange()
    {
        var pad = Create("aa\n{\n  bb\n  cc\n}\ndd\n");
        Assert.True(pad.CollapseFoldAt(1));
        Assert.True(pad.SelectCollapsedFoldAt(1));
        Assert.True(pad.SelectionLength > 0);
        Assert.Contains("{", pad.SelectedText);
        Assert.Contains("bb", pad.SelectedText);
        Assert.Contains("cc", pad.SelectedText);
        Assert.Contains("}", pad.SelectedText);
    }

    [Fact]
    public void DeleteSelection_OnFoldSelection_RemovesHiddenLines()
    {
        var pad = Create("aa\n{\n  bb\n  cc\n}\ndd\n");
        Assert.True(pad.CollapseFoldAt(1));
        Assert.True(pad.SelectCollapsedFoldAt(1));
        Assert.True(pad.DeleteSelection());
        Assert.DoesNotContain("bb", pad.Value);
        Assert.DoesNotContain("cc", pad.Value);
        Assert.Contains("aa", pad.Value);
        Assert.Contains("dd", pad.Value);
        Assert.Equal(0, pad.SelectionLength);
    }

    [Fact]
    public void Typing_OverFoldSelection_ReplacesWholeFold()
    {
        var pad = Create("aa\n{\n  bb\n}\ndd\n");
        Assert.True(pad.CollapseFoldAt(1));
        Assert.True(pad.SelectCollapsedFoldAt(1));
        pad.HandleTextInput("X");
        Assert.DoesNotContain("bb", pad.Value);
        Assert.Contains("X", pad.Value);
        Assert.Contains("aa", pad.Value);
        Assert.Contains("dd", pad.Value);
    }

    [Fact]
    public void DeleteForward_OnCollapsedFoldHeader_RemovesFoldBody()
    {
        var pad = Create("{\n  hidden\n}\nafter\n");
        Assert.True(pad.CollapseFoldAt(0));
        Assert.True(pad.TryGetFoldDocumentRange(0, out var foldStart, out _));
        // caret only on fold header, no selection
        pad.SelectRange(foldStart, foldStart);
        Assert.Equal(0, pad.SelectionLength);
        pad.HandleKey(46); // Delete
        Assert.DoesNotContain("hidden", pad.Value);
        Assert.Contains("after", pad.Value);
    }

    [Fact]
    public void Backspace_OnCollapsedFoldHeader_RemovesFoldBody()
    {
        var pad = Create("before\n{\n  hidden\n}\n");
        Assert.True(pad.CollapseFoldAt(1));
        Assert.True(pad.TryGetFoldDocumentRange(1, out var foldStart, out var foldEnd));
        // caret just after fold header open (still on header line)
        var headerEnd = pad.Model.GetLineStart(1) + pad.Model.GetLineContent(1).Length;
        pad.SelectRange(headerEnd, headerEnd);
        pad.HandleKey(8); // Backspace
        Assert.DoesNotContain("hidden", pad.Value);
        Assert.Contains("before", pad.Value);
    }

    [Fact]
    public void PartialSelectionIntersectingFold_SelectedTextIncludesBody()
    {
        var pad = Create("pre\n{\n  body\n}\npost\n");
        Assert.True(pad.CollapseFoldAt(1));
        Assert.True(pad.TryGetFoldDocumentRange(1, out var foldStart, out _));
        // only select '{' on fold header
        pad.SelectRange(foldStart, foldStart + 1);
        Assert.Equal(1, pad.SelectionLength);
        // SelectedText expands for cut/copy
        Assert.Contains("body", pad.SelectedText);
        Assert.Contains("{", pad.SelectedText);
        Assert.Contains("}", pad.SelectedText);
    }

    [Fact]
    public void PartialSelectionIntersectingFold_DeleteRemovesWholeFold()
    {
        var pad = Create("pre\n{\n  body\n}\npost\n");
        Assert.True(pad.CollapseFoldAt(1));
        Assert.True(pad.TryGetFoldDocumentRange(1, out var foldStart, out _));
        pad.SelectRange(foldStart, foldStart + 1);
        Assert.True(pad.DeleteSelection());
        Assert.DoesNotContain("body", pad.Value);
        Assert.Contains("pre", pad.Value);
        Assert.Contains("post", pad.Value);
    }

    [Fact]
    public void CutViaDeleteSelection_IncludesFoldBodyInSelectedText()
    {
        var pad = Create("x\n{\n  y\n}\nz\n");
        Assert.True(pad.CollapseFoldAt(1));
        Assert.True(pad.SelectCollapsedFoldAt(1));
        Assert.Contains("y", pad.SelectedText);
        Assert.True(pad.CanCutSelection);
        Assert.True(pad.DeleteSelection());
        Assert.DoesNotContain("y", pad.Value);
        Assert.Contains("x", pad.Value);
        Assert.Contains("z", pad.Value);
    }

    [Fact]
    public void TryGetFoldDocumentRange_FalseWhenNotFoldable()
    {
        var pad = Create("no braces here\n");
        Assert.False(pad.TryGetFoldDocumentRange(0, out _, out _));
    }

    [Fact]
    public void ShiftGutterClick_SelectsFold()
    {
        var pad = Create("aa\n{\n  bb\n}\ncc\n");
        pad.ShowGlyphMargin = false;
        pad.ShowLineNumbers = true;
        pad.ShowFolding = true;
        Assert.True(pad.CollapseFoldAt(1));
        var foldX = pad.LineNumberGutterWidth + pad.FoldingGutterWidth / 2f;
        var foldY = 12 + 20; // second document line-ish
        // Shift+click fold gutter
        Assert.False(pad.HandlePointerDown(new Point(foldX, foldY), extendSelection: true));
        Assert.True(pad.SelectionLength > 0);
        Assert.Contains("bb", pad.SelectedText);
    }
}
