using Square.Markup.Ast;
using Square.Markup.Parser;
using Xunit;

namespace Square.Markup.Tests;

public class SqxParserTests
{
    [Fact]
    public void ParseSimpleElement()
    {
        var parser = new SqxParser();
        var doc = parser.Parse("<template><View></View></template>", "Test.sqx");
        Assert.Single(doc.Template.Roots);
        var el = Assert.IsType<SqxElement>(doc.Template.Roots[0]);
        Assert.Equal("View", el.TagName);
    }

    [Fact]
    public void ParseNestedElements()
    {
        var parser = new SqxParser();
        var doc = parser.Parse("<template><View><Text>Hi</Text></View></template>", "Test.sqx");
        Assert.Single(doc.Template.Roots);
        var view = Assert.IsType<SqxElement>(doc.Template.Roots[0]);
        Assert.Single(view.Children);
        var text = Assert.IsType<SqxElement>(view.Children[0]);
        Assert.Equal("Text", text.TagName);
    }

    [Fact]
    public void ParseAttributes()
    {
        var parser = new SqxParser();
        var doc = parser.Parse("<template><Button ref={MyBtn} onClick={OnClick}>Click</Button></template>", "Test.sqx");
        var btn = Assert.IsType<SqxElement>(doc.Template.Roots[0]);
        Assert.Equal(2, btn.Attributes.Count);
        Assert.Equal("ref", btn.Attributes[0].Name);
        Assert.Equal("MyBtn", btn.Attributes[0].Value?.Content);
        Assert.True(btn.Attributes[0].Value?.IsExpression);
    }

    [Fact]
    public void ParseStringAttribute()
    {
        var parser = new SqxParser();
        var doc = parser.Parse("<template><Text text=\"Hello\">Hi</Text></template>", "Test.sqx");
        var text = Assert.IsType<SqxElement>(doc.Template.Roots[0]);
        Assert.Equal("Hello", text.Attributes[0].Value?.Content);
        Assert.False(text.Attributes[0].Value?.IsExpression);
    }

    [Fact]
    public void ParseScript()
    {
        var parser = new SqxParser();
        var doc = parser.Parse("<template><View/></template><script lang=\"csharp\">int x = 1;</script>", "Test.sqx");
        Assert.NotNull(doc.Script);
        Assert.Equal("csharp", doc.Script.Language);
        Assert.Contains("int x = 1;", doc.Script.Code);
    }

    [Fact]
    public void ParseStyle()
    {
        var parser = new SqxParser();
        var doc = parser.Parse("<template><View/></template><style>View { color: red; }</style>", "Test.sqx");
        Assert.NotNull(doc.Style);
        Assert.Contains("color: red", doc.Style.Css);
    }

    [Fact]
    public void ParseSelfClosing()
    {
        var parser = new SqxParser();
        var doc = parser.Parse("<template><View /><Text /></template>", "Test.sqx");
        Assert.Equal(2, doc.Template.Roots.Count);
    }

    [Fact]
    public void ParseShowPrimitive()
    {
        var parser = new SqxParser();
        var doc = parser.Parse("<template><Show when={Visible}><Text>Hi</Text></Show></template>", "Test.sqx");
        var show = Assert.IsType<SqxElement>(doc.Template.Roots[0]);
        Assert.Equal(SqxNodeKind.Show, show.Kind);
    }

    [Fact]
    public void ParseForPrimitive()
    {
        var parser = new SqxParser();
        var doc = parser.Parse("<template><For each={Items}><Text>Item</Text></For></template>", "Test.sqx");
        var forNode = Assert.IsType<SqxElement>(doc.Template.Roots[0]);
        Assert.Equal(SqxNodeKind.For, forNode.Kind);
    }

    [Fact]
    public void ParseExpressionInterpolation()
    {
        var parser = new SqxParser();
        var doc = parser.Parse("<template><Text>{Name}</Text></template>", "Test.sqx");
        var text = Assert.IsType<SqxElement>(doc.Template.Roots[0]);
        Assert.Single(text.Children);
        var expr = Assert.IsType<SqxExpression>(text.Children[0]);
        Assert.Equal("Name", expr.Expression);
    }
}