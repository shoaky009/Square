using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Square.SourceGenerator.Emit;
using Square.SourceGenerator.Parser;

namespace Square.SourceGenerator.Generator;

[Generator]
public sealed class SqxGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var sqxFiles = context.AdditionalTextsProvider
            .Where(f => f.Path.EndsWith(".sqx", StringComparison.OrdinalIgnoreCase));

        var parsed = sqxFiles.Combine(context.AnalyzerConfigOptionsProvider).Select((pair, ct) =>
        {
            var file = pair.Left;
            var sourceText = file.GetText(ct);
            pair.Right.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace);
            return new SqxInput(file.Path, sourceText?.ToString() ?? "", rootNamespace ?? "Square.Sample");
        });

        context.RegisterSourceOutput(parsed, (spc, input) =>
        {
            string code;
            try
            {
                var doc = SqxParser.Parse(input.Content, input.Path);
                var emitter = new ComponentEmitter(doc, input.Namespace);
                code = emitter.Emit();
            }
            catch (Exception ex)
            {
                code = $"// Generator error: {ex.Message}\n// Path: {input.Path}";
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.SqxDiagnostics.SQX0001_SyntaxError,
                    Location.None,
                    ex.Message));
            }

            var hintName = Path.GetFileNameWithoutExtension(input.Path) + "_" + StableHash(input.Path) + ".g.cs";
            spc.AddSource(hintName, SourceText.From(code, Encoding.UTF8));
        });
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in value)
                hash = (hash ^ c) * 16777619u;
            return hash;
        }
    }

    private sealed class SqxInput
    {
        public string Path { get; }
        public string Content { get; }
        public string Namespace { get; }
        public SqxInput(string path, string content, string namespaceName)
        {
            Path = path;
            Content = content;
            Namespace = namespaceName;
        }
    }
}
