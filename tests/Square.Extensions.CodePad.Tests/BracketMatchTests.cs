using Square.Extensions.CodePad;
using Square.Graphics;
using Xunit;

namespace Square.Extensions.CodePad.Tests;

public class BracketMatchTests
{
    [Fact]
    public void FindsMatchingBrace_Forward()
    {
        CodePadRegistration.RegisterDefaults();
        var model = new CodePad { Value = "a(b)c" }.Model;
        var config = LanguageConfiguration.CLike("//", null);
        Assert.True(BracketMatcher.TryFindMatch(model, config, 2, out var open, out var close));
        Assert.Equal(1, open);
        Assert.Equal(3, close);
    }

    [Fact]
    public void FindsMatchingBrace_Backward()
    {
        CodePadRegistration.RegisterDefaults();
        var model = new CodePad { Value = "{ x }" }.Model;
        var config = LanguageConfiguration.CLike("//", null);
        // caret after closing brace
        Assert.True(BracketMatcher.TryFindMatch(model, config, 5, out var open, out var close));
        Assert.Equal(0, open);
        Assert.Equal(4, close);
    }

    [Fact]
    public void NestedBraces_MatchCorrectPair()
    {
        var model = new CodePad { Value = "(() )" }.Model;
        // text: ( ( ) space )
        // idx:  0 1 2 3     4
        var config = LanguageConfiguration.CLike("//", null);
        // caret just after inner open at index 1 → match to index 2
        Assert.True(BracketMatcher.TryFindMatch(model, config, 2, out var open, out var close));
        Assert.Equal(1, open);
        Assert.Equal(2, close);
        // caret after outer open
        Assert.True(BracketMatcher.TryFindMatch(model, config, 1, out open, out close));
        Assert.Equal(0, open);
        Assert.Equal(4, close);
    }

    [Fact]
    public void NoMatch_ReturnsFalse()
    {
        var model = new CodePad { Value = "hello" }.Model;
        var config = LanguageConfiguration.CLike("//", null);
        Assert.False(BracketMatcher.TryFindMatch(model, config, 2, out _, out _));
    }

    [Fact]
    public void HighlightFlags_Toggle()
    {
        var pad = new CodePad { Geometry = new Rect(0, 0, 200, 100) };
        Assert.True(pad.HighlightMatchingBrackets);
        Assert.True(pad.HighlightFindMatches);
        pad.HighlightMatchingBrackets = false;
        pad.HighlightFindMatches = false;
        Assert.False(pad.HighlightMatchingBrackets);
        Assert.False(pad.HighlightFindMatches);
    }
}
