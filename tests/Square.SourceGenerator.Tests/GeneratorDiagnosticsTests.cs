using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Square.Compiler;
using Xunit;

namespace Square.Compiler.Tests;

public class GeneratorDiagnosticsTests
{
    [Fact]
    public void ReportsMissingRequiredPropAtCustomComponentUsage()
    {
        const string source = """
            <template><RequiredCard /></template>
            <script lang="csharp">
              [Prop(Required = true)]
              public ObservableValue<string> Title { get; set; } = new("");
            </script>
            """;
        const string usage = "<template><RequiredCard /></template>";

        var diagnostics = RunGenerator(
            new InMemoryAdditionalText("RequiredCard.sqx", source),
            new InMemoryAdditionalText("Usage.sqx", usage));

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SQX0003");
    }

    [Fact]
    public void AcceptsRequiredPropWhenCallerProvidesIt()
    {
        const string component = """
            <template><View /></template>
            <script lang="csharp">
              [Prop(Required = true)]
              public ObservableValue<string> Title { get; set; } = new("");
            </script>
            """;

        var diagnostics = RunGenerator(
            new InMemoryAdditionalText("RequiredCard.sqx", component),
            new InMemoryAdditionalText("Usage.sqx", "<template><RequiredCard Title=\"Hello\" /></template>"));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SQX0003");
    }

    [Fact]
    public void ReportsDuplicateRefNamesInSameComponent()
    {
        const string source = "<template><View ref={MyBtn}><Button ref={MyBtn} /></View></template>";

        var diagnostics = RunGenerator(new InMemoryAdditionalText("DuplicateRef.sqx", source));

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SQX0006");
    }

    [Fact]
    public void AcceptsUniqueRefNames()
    {
        const string source = "<template><View ref={Root}><Button ref={SaveBtn} /></View></template>";

        var diagnostics = RunGenerator(new InMemoryAdditionalText("UniqueRef.sqx", source));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SQX0006");
    }

    [Fact]
    public void ReportsMatchOutsideSwitchAsInvalidParent()
    {
        const string source = "<template><Match when={true}><Text text=\"x\" /></Match></template>";

        var diagnostics = RunGenerator(new InMemoryAdditionalText("BadMatch.sqx", source));

        // 根级 SkipStandalone → SQXD005（也可伴随 SQXD003）
        Assert.Contains(diagnostics, d => d.Id is "SQXD003" or "SQXD005");
    }

    [Fact]
    public void ReportsShowMissingWhenAttribute()
    {
        const string source = "<template><Show><Text text=\"x\" /></Show></template>";

        var diagnostics = RunGenerator(new InMemoryAdditionalText("BadShow.sqx", source));

        Assert.Contains(diagnostics, d => d.Id == "SQXD002");
    }

    [Fact]
    public void AcceptsShowWithWhenAttribute()
    {
        const string source = "<template><Show when={true}><Text text=\"x\" /></Show></template>";

        var diagnostics = RunGenerator(new InMemoryAdditionalText("OkShow.sqx", source));

        Assert.DoesNotContain(diagnostics, d => d.Id is "SQXD002" or "SQXD003" or "SQXD005");
    }

    [Fact]
    public void ReportsRouteAtRootAsIllegalStandalone()
    {
        const string source = "<template><Route path=\"/\" component={Page} /></template>";

        var diagnostics = RunGenerator(new InMemoryAdditionalText("BadRoute.sqx", source));

        Assert.Contains(diagnostics, d => d.Id is "SQXD003" or "SQXD005");
    }

    [Fact]
    public void ReportsPropTypeMismatchForIntPropWithStringConstant()
    {
        const string component = """
            <template><View /></template>
            <script lang="csharp">
              [Prop]
              public ObservableValue<int> Count { get; set; } = new(0);
            </script>
            """;

        var diagnostics = RunGenerator(
            new InMemoryAdditionalText("TypedCard.sqx", component),
            new InMemoryAdditionalText("Usage.sqx", "<template><TypedCard Count=\"hello\" /></template>"));

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "SQX0007");
    }

    [Fact]
    public void AcceptsCorrectPropTypeForIntWithStringLiteral()
    {
        const string component = """
            <template><View /></template>
            <script lang="csharp">
              [Prop]
              public ObservableValue<int> Count { get; set; } = new(0);
            </script>
            """;

        var diagnostics = RunGenerator(
            new InMemoryAdditionalText("TypedCard.sqx", component),
            new InMemoryAdditionalText("Usage.sqx", "<template><TypedCard Count=\"42\" /></template>"));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SQX0007");
    }

    private static ImmutableArray<Diagnostic> RunGenerator(params AdditionalText[] files)
    {
        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [CSharpSyntaxTree.ParseText("public sealed class Placeholder { }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new SqxGenerator().AsSourceGenerator()],
            files,
            (CSharpParseOptions?)compilation.SyntaxTrees.First().Options);
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult().Diagnostics;
    }

    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText GetText(CancellationToken cancellationToken = default) =>
            SourceText.From(content, Encoding.UTF8);
    }
}
