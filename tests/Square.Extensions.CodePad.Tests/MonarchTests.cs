using Square.Extensions.CodePad;
using Xunit;

namespace Square.Extensions.CodePad.Tests;

public class MonarchTests
{
    public MonarchTests()
    {
        CodePadRegistration.RegisterDefaults();
    }

    [Fact]
    public void CSharp_KeywordsAndStrings()
    {
        var tokenizer = MonarchTokenizer.FromJson(BuiltInLanguagesForTest.CSharp);
        var state = "root";
        var tokens = tokenizer.TokenizeLine("public class Foo { }", ref state);
        Assert.Contains(tokens, t => t.Type.Contains("keyword", StringComparison.Ordinal));
        Assert.True(tokens.Count >= 3);
    }

    [Fact]
    public void CSharp_BlockComment_SpansLines()
    {
        var tokenizer = MonarchTokenizer.FromJson(BuiltInLanguagesForTest.CSharp);
        var state = "root";
        var line1 = tokenizer.TokenizeLine("/* hello", ref state);
        Assert.Contains(line1, t => t.Type.Contains("comment", StringComparison.Ordinal));
        Assert.Contains("comment", state, StringComparison.Ordinal);
        var line2 = tokenizer.TokenizeLine("world */ int x", ref state);
        Assert.Contains(line2, t => t.Type.Contains("comment", StringComparison.Ordinal));
    }

    [Fact]
    public void Json_KeysAndStrings()
    {
        var tokenizer = MonarchTokenizer.FromJson(BuiltInLanguagesForTest.Json);
        var state = "root";
        var tokens = tokenizer.TokenizeLine("{\"a\": 1}", ref state);
        Assert.NotEmpty(tokens);
        Assert.Contains(tokens, t =>
            t.Type.Contains("key", StringComparison.Ordinal) ||
            t.Type.Contains("string", StringComparison.Ordinal) ||
            t.Type.Contains("number", StringComparison.Ordinal));
    }

    [Fact]
    public void Theme_ResolvesTokenColors()
    {
        var theme = CodePadThemeRegistry.Get("default-dark");
        var keyword = theme.ResolveTokenColor("keyword.cs");
        var fallback = theme.ResolveTokenColor("unknown.token");
        Assert.NotEqual(keyword, fallback);
        Assert.Equal(theme.EditorForeground, fallback);
    }

    [Fact]
    public void Configuration_CLike_HasPairs()
    {
        Assert.True(LanguageRegistry.TryGet("csharp", out var contribution));
        Assert.NotNull(contribution);
        Assert.NotNull(contribution!.Configuration);
        Assert.Equal("//", contribution.Configuration!.LineComment);
        Assert.NotNull(contribution.Configuration.AutoClosingPairs);
        Assert.Contains(contribution.Configuration.AutoClosingPairs!, p => p.Open == "{");

        var config = LanguageRegistry.ResolveConfiguration("csharp");
        Assert.Equal("//", config.LineComment);
    }

    [Fact]
    public void Registry_ResolvesCSharpTokenizer()
    {
        var tokenizer = LanguageRegistry.ResolveTokenizer("csharp");
        Assert.IsType<MonarchTokenizer>(tokenizer);
    }
}

/// <summary>Test-local copy of minimal monarch defs (mirrors BuiltInLanguages).</summary>
internal static class BuiltInLanguagesForTest
{
    public const string CSharp = """
    {
      "defaultToken": "source",
      "tokenPostfix": ".cs",
      "keywords": ["public","class"],
      "tokenizer": {
        "root": [
          ["\\/\\*", "comment", "@comment"],
          ["\\/\\/.*$", "comment"],
          ["\"", "string", "@string"],
          ["[a-zA-Z_]\\w*", { "cases": { "@keywords": "keyword", "@default": "identifier" } }],
          ["\\d+", "number"],
          ["[{}()\\[\\]]", "delimiter.bracket"],
          ["\\s+", "white"]
        ],
        "comment": [
          ["[^\\*]+", "comment"],
          ["\\*\\/", "comment", "@pop"],
          ["\\*", "comment"]
        ],
        "string": [
          ["[^\\\\\"]+", "string"],
          ["\"", "string", "@pop"]
        ]
      }
    }
    """;

    public const string Json = """
    {
      "defaultToken": "source",
      "tokenPostfix": ".json",
      "tokenizer": {
        "root": [
          ["\\s+", "white"],
          ["[{}\\[\\]]", "delimiter.bracket"],
          ["[,:]", "delimiter"],
          ["\"([^\"\\\\]|\\\\.)*\"(?=\\s*:)", "key"],
          ["\"([^\"\\\\]|\\\\.)*\"", "string"],
          ["-?\\d+", "number"],
          ["true|false|null", "keyword"]
        ]
      }
    }
    """;
}
