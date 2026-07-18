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

    [Fact]
    public void StyleReconcilerReappliesDynamicClassMatchesAndRemovals()
    {
        var sheet = new CssParser(new CssTokenizer(".active { color: red; width: 120px; }").Tokenize()).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);
        var text = new Square.Controls.Controls.Text("item");
        engine.ApplyStylesToTree(text);

        text.ClassList.Add("active");
        CssStyleReconciler.Flush();
        Assert.Equal("red", text.Style.Get("color"));
        Assert.Equal("120px", text.Style.Get("width"));

        text.ClassList.Remove("active");
        CssStyleReconciler.Flush();
        Assert.Null(text.Style.Get("color"));
        Assert.Null(text.Style.Get("width"));
    }

    [Fact]
    public void IdSelectorMatchesElementIdProperty()
    {
        var sheet = new CssParser(new CssTokenizer("#target { color: blue; }").Tokenize()).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);
        var text = new Square.Controls.Controls.Text("item") { Id = "target" };

        engine.ApplyStylesToTree(text);

        Assert.Equal("blue", text.Style.Get("color"));
    }

    [Fact]
    public void ChildCombinatorOnlyMatchesDirectChildren()
    {
        var css = "View > Text { padding: 7px; }";
        var sheet = new CssParser(new CssTokenizer(css).Tokenize()).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);

        var root = new View();
        var mid = new Square.Controls.Controls.Text("mid");
        var directChild = new Square.Controls.Controls.Text("direct");
        var grandChild = new Square.Controls.Controls.Text("grand");
        root.Children.Add(mid);
        root.Children.Add(directChild);
        mid.Children.Add(grandChild);

        engine.ApplyStylesToTree(root);

        Assert.Equal("7px", directChild.Style.Get("padding"));
        Assert.Null(grandChild.Style.Get("padding"));
    }

    [Fact]
    public void ImportantDeclarationOverridesSpecificity()
    {
        var css = ".high-specificity { color: blue; } Text { color: red !important; }";
        var sheet = new CssParser(new CssTokenizer(css).Tokenize()).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);

        var text = new Square.Controls.Controls.Text();
        text.ClassList.Add("high-specificity");

        engine.ApplyStyles(text);

        Assert.Equal("red", text.Style.Get("color"));
    }

    [Fact]
    public void NthChildPseudoClassMatchesCorrectIndex()
    {
        var css = "View > Text:nth-child(2) { color: red; }";
        var sheet = new CssParser(new CssTokenizer(css).Tokenize()).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);

        var root = new View();
        var t1 = new Square.Controls.Controls.Text("1");
        var t2 = new Square.Controls.Controls.Text("2");
        var t3 = new Square.Controls.Controls.Text("3");
        root.Children.Add(t1);
        root.Children.Add(t2);
        root.Children.Add(t3);

        engine.ApplyStylesToTree(root);

        Assert.Null(t1.Style.Get("color"));
        Assert.Equal("red", t2.Style.Get("color"));
        Assert.Null(t3.Style.Get("color"));
    }

    [Fact]
    public void AttributeSelectorMatchesPropertyPresenceAndValue()
    {
        var css = "Button[IsDisabled] { opacity: 0.5; } Button[variant=primary] { color: blue; }";
        var sheet = new CssParser(new CssTokenizer(css).Tokenize()).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);

        var disabled = new Button();
        disabled.IsDisabled = true;
        var primary = new Button();
        primary.SetProperty("variant", "primary");
        var secondary = new Button();
        secondary.SetProperty("variant", "secondary");

        engine.ApplyStyles(disabled);
        engine.ApplyStyles(primary);
        engine.ApplyStyles(secondary);

        Assert.Equal("0.5", disabled.Style.Get("opacity"));
        Assert.Equal("blue", primary.Style.Get("color"));
        Assert.Null(secondary.Style.Get("color"));
    }

    [Fact]
    public void ActiveThemeVariablesOverrideStylesheetVariablesWhenStylesAreReapplied()
    {
        var css = ":root { --primary: #111111; } Text { color: var(--primary); }";
        var sheet = new CssParser(new CssTokenizer(css).Tokenize()).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);
        engine.RegisterTheme("dark", new Dictionary<string, string> { ["--primary"] = "#eeeeee" });
        var text = new Square.Controls.Controls.Text();

        engine.ApplyStyles(text);
        Assert.Equal("#111111", text.Style.Get("color"));

        engine.SetTheme("dark");
        engine.ApplyStyles(text);

        Assert.Equal("#eeeeee", text.Style.Get("color"));
    }

    [Fact]
    public void ThemeProviderSwitchesThemeAndReappliesStylesToTree()
    {
        var css = ":root { --primary: #111111; } Text { color: var(--primary); }";
        var engine = new CssEngine();
        engine.LoadStyleSheet(new CssParser(new CssTokenizer(css).Tokenize()).Parse());
        engine.RegisterTheme("dark", new Dictionary<string, string> { ["--primary"] = "#eeeeee" });
        var root = new View();
        var text = new Square.Controls.Controls.Text("hello");
        root.Children.Add(text);
        var provider = new ThemeProvider(engine, root);

        provider.ApplyTheme(null);
        Assert.Equal("#111111", text.Style.Get("color"));

        provider.ApplyTheme("dark");

        Assert.Equal("#eeeeee", text.Style.Get("color"));
    }
}
