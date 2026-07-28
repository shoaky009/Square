using Square.Extensions.CodePad;
using Square.Graphics;
using Square.Text;
using Xunit;

namespace Square.Extensions.CodePad.Tests;

public class WordWrapTests
{
    [Fact]
    public void WrapLine_BreaksLongLine()
    {
        var font = FontManager.Instance.FromCss("monospace", "13px", null, null, 13f);
        var line = new string('a', 80);
        var segs = CodePadViewLayout.WrapLine(line, font, 4, 100f);
        Assert.True(segs.Count > 1);
        Assert.Equal(0, segs[0].Start);
        Assert.Equal(line.Length, segs[^1].End);
        for (var i = 1; i < segs.Count; i++)
            Assert.Equal(segs[i - 1].End, segs[i].Start);
    }

    [Fact]
    public void WordWrap_IncreasesViewRows()
    {
        CodePadRegistration.RegisterDefaults();
        var longLine = new string('x', 200);
        var pad = new CodePad
        {
            Geometry = new Rect(0, 0, 200, 300),
            Value = longLine,
            WordWrap = false,
            ShowFolding = false,
            ShowLineNumbers = false,
        };

        // Force layout without wrap: 1 document line => 1 view row path via paint/hit
        pad.WordWrap = false;
        var noWrapRows = CountRows(pad);
        pad.WordWrap = true;
        var wrapRows = CountRows(pad);
        Assert.True(wrapRows > noWrapRows);
        Assert.Equal(1, pad.Model.LineCount);
    }

    [Fact]
    public void WordWrap_HitTestAndCaretStayOnSameDocumentLine()
    {
        CodePadRegistration.RegisterDefaults();
        var pad = new CodePad
        {
            Geometry = new Rect(0, 0, 180, 300),
            Value = new string('a', 120),
            WordWrap = true,
            ShowFolding = false,
            ShowLineNumbers = false,
        };

        // click lower in editor should still map into document line 0
        var offset = InvokeHit(pad, new Point(40, 40));
        Assert.InRange(offset, 0, pad.Model.Length);
        Assert.Equal(0, pad.Model.GetLineNumberAt(offset));
    }

    [Fact]
    public void ToggleWordWrap_PropertyRoundTrips()
    {
        var pad = new CodePad();
        Assert.False(pad.WordWrap);
        pad.ToggleWordWrap();
        Assert.True(pad.WordWrap);
        pad.WordWrap = false;
        Assert.False(pad.WordWrap);
    }

    private static int CountRows(CodePad pad)
    {
        // Use reflection on private EnsureViewLayout path via caret rect which builds layout
        pad.SelectAll();
        _ = pad.CaretRect;
        var field = typeof(CodePad).GetField("_viewLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        var layout = field!.GetValue(pad);
        Assert.NotNull(layout);
        return Assert.IsType<CodePadViewLayout>(layout).RowCount;
    }

    private static int InvokeHit(CodePad pad, Point point)
    {
        // HitTestOffset is private; use pointer down to set caret
        pad.HandlePointerDown(point);
        pad.HandlePointerUp(point);
        return pad.CaretIndex;
    }
}
