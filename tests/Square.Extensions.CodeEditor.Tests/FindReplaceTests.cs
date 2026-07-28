using Square.Extensions.CodeEditor;
using Square.Graphics;
using Xunit;

namespace Square.Extensions.CodeEditor.Tests;

public class FindReplaceTests
{
    private static CodeEditor Create(string text)
    {
        CodeEditorRegistration.RegisterDefaults();
        return new CodeEditor
        {
            Geometry = new Rect(0, 0, 400, 200),
            Value = text,
            ShowFolding = false,
        };
    }

    [Fact]
    public void FindNext_WrapsAround()
    {
        var pad = Create("one two one");
        Assert.True(pad.FindNext("one"));
        Assert.Equal(0, pad.SelectionStart);
        Assert.Equal(3, pad.SelectionLength);
        Assert.True(pad.FindNext());
        Assert.Equal(8, pad.SelectionStart);
        Assert.True(pad.FindNext());
        Assert.Equal(0, pad.SelectionStart);
    }

    [Fact]
    public void FindPrevious_FindsEarlierMatch()
    {
        var pad = Create("aa bb aa");
        pad.FindNext("aa");
        pad.FindNext();
        Assert.Equal(6, pad.SelectionStart);
        Assert.True(pad.FindPrevious());
        Assert.Equal(0, pad.SelectionStart);
    }

    [Fact]
    public void FindMatchCase_RespectsCase()
    {
        var pad = Create("Foo foo FOO");
        pad.FindMatchCase = true;
        Assert.True(pad.FindNext("foo"));
        Assert.Equal(4, pad.SelectionStart);
        Assert.False(pad.FindNext("FOO") && pad.SelectionStart == 0);
        Assert.True(pad.FindNext("FOO"));
        Assert.Equal(8, pad.SelectionStart);
    }

    [Fact]
    public void ReplaceNext_ReplacesCurrentThenFindsNext()
    {
        var pad = Create("a-a-a");
        pad.FindNext("a");
        Assert.True(pad.ReplaceNext("a", "b"));
        Assert.Contains("b", pad.Value);
        Assert.True(pad.Value.Count(c => c == 'b') >= 1);
    }

    [Fact]
    public void ReplaceAll_ReplacesEveryMatch()
    {
        var pad = Create("x x x");
        var count = pad.ReplaceAll("x", "y");
        Assert.Equal(3, count);
        Assert.Equal("y y y", pad.Value);
    }

    [Fact]
    public void ReplaceAll_NoMatch_ReturnsZero()
    {
        var pad = Create("hello");
        Assert.Equal(0, pad.ReplaceAll("zzz", "a"));
        Assert.Equal("hello", pad.Value);
    }
}
