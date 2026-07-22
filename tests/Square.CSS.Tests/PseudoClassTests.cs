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

        var btn = new Square.Controls.Button();
        btn.SetState(ElementState.Hover, true);
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

        var btn = new Square.Controls.Button();
        engine.ApplyStyles(btn);
        Assert.Null(btn.Style.Get("color"));
    }

    [Fact]
    public void StyleReconcilerReappliesDynamicFocusPseudoClass()
    {
        var sheet = new CssParser(new CssTokenizer("Button:focus { color: red; width: 180px; }").Tokenize()).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);
        var btn = new Square.Controls.Button();
        engine.ApplyStylesToTree(btn);

        btn.Focus();
        CssStyleReconciler.Flush();
        Assert.Equal("red", btn.Style.Get("color"));
        Assert.Equal("180px", btn.Style.Get("width"));

        btn.Unfocus();
        CssStyleReconciler.Flush();
        Assert.Null(btn.Style.Get("color"));
        Assert.Null(btn.Style.Get("width"));
    }

    [Fact]
    public void StyleReconcilerReappliesDynamicHoverAndActivePseudoClasses()
    {
        var sheet = new CssParser(new CssTokenizer("Button:hover { color: red; } Button:active { background: blue; }").Tokenize()).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);
        var btn = new Square.Controls.Button();
        engine.ApplyStylesToTree(btn);

        btn.SetState(ElementState.Hover, true);
        CssStyleReconciler.Flush();
        Assert.Equal("red", btn.Style.Get("color"));

        btn.SetState(ElementState.Active, true);
        CssStyleReconciler.Flush();
        Assert.Equal("blue", btn.Style.Get("background"));

        btn.SetState(ElementState.Hover, false);
        btn.SetState(ElementState.Active, false);
        CssStyleReconciler.Flush();
        Assert.Null(btn.Style.Get("color"));
        Assert.Null(btn.Style.Get("background"));
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

    [Fact]
    public void AnimationShorthandExpandsIntoComputedAnimationProperties()
    {
        var css = "@keyframes fade { from { opacity: 0; } to { opacity: 1; } } Text { animation: fade 0.3s ease-in 100ms 2 reverse; }";
        var sheet = new CssParser(new CssTokenizer(css).Tokenize()).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);
        var text = new Square.Controls.Text();

        engine.ApplyStyles(text);

        Assert.NotNull(engine.GetKeyFrames("fade"));
        Assert.Equal("fade", text.Style.Get("animation-name"));
        Assert.Equal("0.3s", text.Style.Get("animation-duration"));
        Assert.Equal("ease-in", text.Style.Get("animation-timing-function"));
        Assert.Equal("100ms", text.Style.Get("animation-delay"));
        Assert.Equal("2", text.Style.Get("animation-iteration-count"));
        Assert.Equal("reverse", text.Style.Get("animation-direction"));
    }

    [Fact]
    public void AnimationRuntimeTicksKeyframesIntoVisualStyles()
    {
        var css = "@keyframes fade { from { opacity: 0; } to { opacity: 1; } } Text { animation: fade 1s linear; }";
        var sheet = new CssParser(new CssTokenizer(css).Tokenize()).Parse();
        var engine = new CssEngine();
        engine.LoadStyleSheet(sheet);
        var text = new Square.Controls.Text();
        engine.ApplyStyles(text);

        var timeline = engine.CreateAnimationTimeline(text);
        Assert.NotNull(timeline);

        timeline!.Start();
        timeline.Tick(0.5f);

        Assert.Equal("0.5", text.Style.Get("opacity"));

        timeline.Tick(0.5f);
        Assert.Equal("1", text.Style.Get("opacity"));
        Assert.True(timeline.IsComplete);
    }

    [Fact]
    public void AnimationTimelineHonorsDelayIterationsAndReverseDirection()
    {
        var css = "@keyframes fade { from { opacity: 0; } to { opacity: 1; } } Text { animation: fade 1s linear 0.5s 2 reverse; }";
        var engine = new CssEngine();
        engine.LoadStyleSheet(new CssParser(new CssTokenizer(css).Tokenize()).Parse());
        var text = new Square.Controls.Text();
        engine.ApplyStyles(text);
        var timeline = engine.CreateAnimationTimeline(text);

        timeline!.Start();
        Assert.Equal("1", text.Style.Get("opacity"));

        timeline.Tick(0.25f);
        Assert.Equal("1", text.Style.Get("opacity"));

        timeline.Tick(0.5f);
        Assert.Equal("0.75", text.Style.Get("opacity"));

        timeline.Tick(1f);
        Assert.Equal("0.75", text.Style.Get("opacity"));
        Assert.False(timeline.IsComplete);

        timeline.Tick(0.75f);
        Assert.Equal("0", text.Style.Get("opacity"));
        Assert.True(timeline.IsComplete);
    }

    [Fact]
    public void AnimationManagerStartsAndTicksAnimationsInVisualTree()
    {
        var css = "@keyframes fade { from { opacity: 0; } to { opacity: 1; } } Text { animation: fade 1s linear; }";
        var engine = new CssEngine();
        engine.LoadStyleSheet(new CssParser(new CssTokenizer(css).Tokenize()).Parse());
        var root = new Square.Controls.View();
        var text = new Square.Controls.Text("animated");
        root.Children.Add(text);
        engine.ApplyStylesToTree(root);
        var manager = new CssAnimationManager(engine);

        manager.Attach(root);
        manager.Tick(0.25f);

        Assert.Equal("0.25", text.Style.Get("opacity"));
        Assert.True(manager.HasRunningAnimations);

        manager.Tick(0.75f);
        Assert.Equal("1", text.Style.Get("opacity"));
        Assert.False(manager.HasRunningAnimations);
    }
}
