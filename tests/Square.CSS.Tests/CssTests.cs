using Square.CSS.Tokenizer;
using Square.CSS.Engine;
using Square.Controls.Controls;
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
        Assert.Equal(2, sheet.Rules[0].Selector.Steps.Count);
    }

    [Fact]
    public void ApplyDescendantSelectorVariablesAndInheritance()
    {
        var css = "View { --accent: #123456; color: var(--accent); } View Text { font-size: 20px; }";
        var sheet = new CssParser(new CssTokenizer(css).Tokenize()).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);
        var root = new View();
        var child = new Square.Controls.Controls.Text("child");
        root.Children.Add(child);

        engine.ApplyStylesToTree(root);

        Assert.Equal("#123456", root.Style.Get("color"));
        Assert.Equal("#123456", child.Style.Get("color"));
        Assert.Equal("20px", child.Style.Get("font-size"));
    }

    [Fact]
    public void LaterRuleWinsWhenSpecificityMatches()
    {
        var sheet = new CssParser(new CssTokenizer("Text { color: #111111; } Text { color: #222222; }").Tokenize()).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);
        var text = new Square.Controls.Controls.Text();

        engine.ApplyStyles(text);

        Assert.Equal("#222222", text.Style.Get("color"));
    }

    [Fact]
    public void SpecificitySurvivesStylesAppliedByNestedComponentEngines()
    {
        var innerSheet = new CssParser(new CssTokenizer(".route-links { display: flex; flex-direction: row; }").Tokenize()).Parse();
        var outerSheet = new CssParser(new CssTokenizer("View { display: flex; flex-direction: column; }").Tokenize()).Parse();
        var view = new View();
        view.ClassList.Add("route-links");
        var innerEngine = new CssEngine();
        innerEngine.LoadStyleSheet(innerSheet);
        var outerEngine = new CssEngine();
        outerEngine.LoadStyleSheet(outerSheet);

        innerEngine.ApplyStyles(view);
        outerEngine.ApplyStyles(view);

        Assert.Equal("row", view.Style.Get("flex-direction"));
    }

    [Fact]
    public void InlineStyleRemainsHigherPriorityThanStyleSheets()
    {
        var sheet = new CssParser(new CssTokenizer(".target { color: red; }").Tokenize()).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);
        var text = new Square.Controls.Controls.Text();
        text.ClassList.Add("target");
        text.Style.Set("color", "green");

        engine.ApplyStyles(text);

        Assert.Equal("green", text.Style.Get("color"));
    }
}
