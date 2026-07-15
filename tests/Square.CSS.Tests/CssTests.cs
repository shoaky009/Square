using Square.CSS.Tokenizer;
using Square.CSS.Engine;
using Xunit;

namespace Square.CSS.Tests;

public class CssTokenizerTests
{
    [Fact]
    public void TokenizeSelector()
    {
        var tokens = new CssTokenizer(".my-class { }").Tokenize();
        Assert.Contains(tokens, t => t.Type == CssTokenType.Dot);
        Assert.Contains(tokens, t => t.Type == CssTokenType.Identifier && t.Text == "my-class");
    }

    [Fact]
    public void TokenizeHash()
    {
        var tokens = new CssTokenizer("#main { }").Tokenize();
        Assert.Contains(tokens, t => t.Type == CssTokenType.Hash && t.Text == "main");
    }

    [Fact]
    public void TokenizeAtKeyword()
    {
        var tokens = new CssTokenizer("@keyframes fade { }").Tokenize();
        Assert.Contains(tokens, t => t.Type == CssTokenType.AtKeyword && t.Text == "keyframes");
    }

    [Fact]
    public void TokenizeNumber()
    {
        var tokens = new CssTokenizer("16px").Tokenize();
        Assert.Contains(tokens, t => t.Type == CssTokenType.Number && t.Text == "16");
        Assert.Contains(tokens, t => t.Type == CssTokenType.Unit && t.Text == "px");
    }

    [Fact]
    public void TokenizeString()
    {
        var tokens = new CssTokenizer("\"hello\"").Tokenize();
        Assert.Contains(tokens, t => t.Type == CssTokenType.String && t.Text == "hello");
    }

    [Fact]
    public void TokenizeComment()
    {
        var tokens = new CssTokenizer("/* comment */ View { }").Tokenize();
        Assert.DoesNotContain(tokens, t => t.Type == CssTokenType.Comment);
        Assert.Contains(tokens, t => t.Type == CssTokenType.Identifier && t.Text == "View");
    }
}

public class CssParserTests
{
    [Fact]
    public void ParseSingleRule()
    {
        var tokens = new CssTokenizer("View { color: red; }").Tokenize();
        var sheet = new CssParser(tokens).Parse();
        Assert.Single(sheet.Rules);
        Assert.Equal("color", sheet.Rules[0].Declarations[0].Property);
        Assert.Equal("red", sheet.Rules[0].Declarations[0].Value.Trim());
    }

    [Fact]
    public void ParseMultipleDeclarations()
    {
        var tokens = new CssTokenizer("View { color: red; padding: 16px; }").Tokenize();
        var sheet = new CssParser(tokens).Parse();
        Assert.Equal(2, sheet.Rules[0].Declarations.Count);
    }

    [Fact]
    public void ParseMultipleRules()
    {
        var tokens = new CssTokenizer("View { color: red; } .cls { padding: 8px; }").Tokenize();
        var sheet = new CssParser(tokens).Parse();
        Assert.Equal(2, sheet.Rules.Count);
    }

    [Fact]
    public void ParseCompoundSelector()
    {
        var tokens = new CssTokenizer("View Text { color: red; }").Tokenize();
        var sheet = new CssParser(tokens).Parse();
        Assert.Single(sheet.Rules);
        Assert.True(sheet.Rules[0].Selector.Steps.Count >= 1);
    }
}