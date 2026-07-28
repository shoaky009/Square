using Square.Extensions.CodePad;
using Xunit;

namespace Square.Extensions.CodePad.Tests;

public class CodePadRegistrationTests
{
    [Fact]
    public void RegisterDefaults_IsIdempotent_AndCodePadDefaults()
    {
        CodePadRegistration.RegisterDefaults();
        CodePadRegistration.RegisterDefaults();

        var pad = new CodePad();
        Assert.Equal("plaintext", pad.Language);
        Assert.Equal(4, pad.TabSize);
        Assert.True(pad.InsertSpaces);
        Assert.True(pad.ShowLineNumbers);
    }

    [Fact]
    public void LanguageRegistry_HasBuiltIns()
    {
        CodePadRegistration.RegisterDefaults();

        Assert.True(LanguageRegistry.TryGet("plaintext", out _));
        Assert.True(LanguageRegistry.TryGet("csharp", out _));
        Assert.True(LanguageRegistry.TryGet("json", out _));
        Assert.Equal("csharp", LanguageRegistry.GuessLanguage("Program.cs"));
        Assert.Equal("json", LanguageRegistry.GuessLanguage(".json"));
        Assert.Equal("python", LanguageRegistry.GuessLanguage("app.py"));
    }

    [Fact]
    public void ThemeRegistry_HasDefaultThemes()
    {
        CodePadRegistration.RegisterDefaults();
        Assert.NotNull(CodePadThemeRegistry.Get("default-light"));
        Assert.NotNull(CodePadThemeRegistry.Get("default-dark"));
        Assert.NotNull(CodePadThemeRegistry.Get(null));
    }
}
