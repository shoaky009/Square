using Square.CSS.Ast;
using Square.CSS.Engine;
using Square.CSS.Tokenizer;
using Square.UI;
using Xunit;

namespace Square.CSS.Tests;

public class PseudoClassTests
{
    [Fact]
    public void ParsePseudoClass()
    {
        var tokens = new CssTokenizer("Button:hover { color: red; }").Tokenize();
        var sheet = new CssParser(tokens).Parse();
        Assert.Single(sheet.Rules);
        var parts = sheet.Rules[0].Selector.Steps[0].Selector.Parts;
        Assert.Contains(parts, p => p.Kind == SimpleSelectorKind.PseudoClass && p.Name == "hover");
    }

    [Fact]
    public void ParseMultiplePseudoClasses()
    {
        var tokens = new CssTokenizer("Button:hover:focus { color: red; }").Tokenize();
        var sheet = new CssParser(tokens).Parse();
        var parts = sheet.Rules[0].Selector.Steps[0].Selector.Parts;
        Assert.Equal(3, parts.Count);
        Assert.Contains(parts, p => p.Kind == SimpleSelectorKind.PseudoClass && p.Name == "hover");
        Assert.Contains(parts, p => p.Kind == SimpleSelectorKind.PseudoClass && p.Name == "focus");
    }

    [Fact]
    public void MatchHoverState()
    {
        var tokens = new CssTokenizer("Button:hover { color: red; }").Tokenize();
        var sheet = new CssParser(tokens).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);

        var btn = new Square.Controls.Controls.Button();
        btn.SetState(VisualState.Hover, true);
        engine.ApplyStyles(btn);
        Assert.Equal("red", btn.Style.Get("color"));
    }

    [Fact]
    public void NoMatchWhenNoHover()
    {
        var tokens = new CssTokenizer("Button:hover { color: red; }").Tokenize();
        var sheet = new CssParser(tokens).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);

        var btn = new Square.Controls.Controls.Button();
        engine.ApplyStyles(btn);
        Assert.Null(btn.Style.Get("color"));
    }

    [Fact]
    public void ParseKeyFrames()
    {
        var css = "@keyframes fade { from { opacity: 0; } to { opacity: 1; } }";
        var tokens = new CssTokenizer(css).Tokenize();
        var sheet = new CssParser(tokens).Parse();
        Assert.Single(sheet.KeyFrames);
        Assert.Equal("fade", sheet.KeyFrames[0].Name);
        Assert.Equal(2, sheet.KeyFrames[0].Stops.Count);
    }
}