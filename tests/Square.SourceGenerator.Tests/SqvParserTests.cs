using Square.Compiler.Parser;
using Xunit;

namespace Square.Compiler.Tests;

public class SqvParserTests
{
    [Fact]
    public void LexerPreservesDynamicArgumentTokenAndAbsoluteOffset()
    {
        var tokens = new SqvLexer("<Text :[name]=\"Value\" />", 40).Tokenize();
        var attribute = Assert.Single(tokens, token => token.Text == ":[name]");

        Assert.Equal(SqvTokenType.Identifier, attribute.Type);
        Assert.Equal(6, attribute.Offset);
    }

    [Fact]
    public void TemplateParserPreservesExpressionAndAttributePositions()
    {
        var roots = SqvTemplateParser.Parse("<Text :text=\"Title\">{{ Title }}</Text>", 100);
        var element = Assert.IsType<SqxElement>(Assert.Single(roots));
        var attribute = Assert.Single(element.Attributes);
        var expression = Assert.IsType<SqxExpression>(Assert.Single(element.Children));

        Assert.Equal(100, element.Position);
        Assert.Equal(106, attribute.Position);
        Assert.Equal(120, expression.Position);
    }

    [Fact]
    public void TemplateParserRewritesForAndIfWithSourcePositions()
    {
        const string source = "<View><Text v-for=\"item in Items\">{{ item }}</Text><Text v-if=\"Ready\">yes</Text></View>";

        var root = Assert.IsType<SqxElement>(Assert.Single(SqvTemplateParser.Parse(source, 20)));
        var forDirective = Assert.IsType<SqvForDirective>(root.Children[0]);
        var ifDirective = Assert.IsType<SqvIfChainDirective>(root.Children[1]);

        Assert.Equal(32, forDirective.Position);
        Assert.Equal(77, ifDirective.Position);
        Assert.Equal(77, Assert.Single(ifDirective.Branches).Position);
    }

    [Fact]
    public void ValidatorFindsDuplicateBindingInsideForDirective()
    {
        var roots = SqvTemplateParser.Parse(
            "<Input v-for=\"item in Items\" value=\"a\" :value=\"item\" />",
            0);

        var exception = Assert.Throws<SqxParseException>(() => SqvValidator.Validate(roots));

        Assert.Equal("SQV0005", exception.DiagnosticId);
    }

    [Fact]
    public void TemplateParserPromotesKeyToForDirective()
    {
        var roots = SqvTemplateParser.Parse(
            "<Text :key=\"item.Id\" v-for=\"item in Items\">{{ item.Name }}</Text>",
            10);

        var directive = Assert.IsType<SqvForDirective>(Assert.Single(roots));
        var element = Assert.IsType<SqxElement>(Assert.Single(directive.Children));

        Assert.Equal("item.Id", directive.KeyExpression);
        Assert.Equal(16, directive.KeyPosition);
        Assert.DoesNotContain(element.Attributes, attribute => attribute.Name == "__vfor_key");
    }

    [Fact]
    public void ValidatorRejectsInvalidKeyExpression()
    {
        var roots = SqvTemplateParser.Parse(
            "<Text v-for=\"item in Items\" :key=\"item.\" />",
            0);

        var exception = Assert.Throws<SqxParseException>(() => SqvValidator.Validate(roots));

        Assert.Equal("SQV0009", exception.DiagnosticId);
    }
}
