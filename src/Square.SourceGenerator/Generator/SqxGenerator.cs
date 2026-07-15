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

        var parsed = sqxFiles.Select((file, ct) =>
        {
            var sourceText = file.GetText(ct);
            return new SqxInput(file.Path, sourceText?.ToString() ?? "");
        });

        context.RegisterSourceOutput(parsed, (spc, input) =>
        {
            string code;
            try
            {
                var doc = SqxParser.Parse(input.Content, input.Path);
                var emitter = new ComponentEmitter(doc);
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

            var hintName = Path.GetFileNameWithoutExtension(input.Path) + "_" + Math.Abs(input.Path.GetHashCode()) + ".g.cs";
            spc.AddSource(hintName, SourceText.From(code, Encoding.UTF8));
        });
    }

    private sealed class SqxInput
    {
        public string Path { get; }
        public string Content { get; }
        public SqxInput(string path, string content) { Path = path; Content = content; }
    }
}