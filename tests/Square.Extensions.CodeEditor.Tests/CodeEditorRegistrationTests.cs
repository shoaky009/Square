using Square.Extensions.CodeEditor;
using Xunit;

namespace Square.Extensions.CodeEditor.Tests;

public class CodeEditorRegistrationTests
{
    [Fact]
    public void RegisterDefaults_IsIdempotent_AndCodeEditorDefaults()
    {
        CodeEditorRegistration.RegisterDefaults();
        CodeEditorRegistration.RegisterDefaults();

        var pad = new CodeEditor();
        Assert.Equal("plaintext", pad.Language);
        Assert.Equal(4, pad.TabSize);
        Assert.True(pad.InsertSpaces);
        Assert.True(pad.ShowLineNumbers);
    }

    [Fact]
    public void LanguageRegistry_HasBuiltIns()
    {
        CodeEditorRegistration.RegisterDefaults();

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
        CodeEditorRegistration.RegisterDefaults();
        Assert.NotNull(CodeEditorThemeRegistry.Get("default-light"));
        Assert.NotNull(CodeEditorThemeRegistry.Get("default-dark"));
        Assert.NotNull(CodeEditorThemeRegistry.Get(null));
    }
}
