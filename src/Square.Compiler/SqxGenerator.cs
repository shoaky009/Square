using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Square.Compiler.Directives;
using Square.Compiler.Emit;
using Square.Compiler.Parser;

namespace Square.Compiler;

[Generator]
public sealed class SqxGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var inputs = context.AdditionalTextsProvider
            .Where(file => IsTemplateFile(file.Path))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select((pair, cancellationToken) =>
            {
                var file = pair.Left;
                pair.Right.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace);
                return new SqxInput(
                    file.Path,
                    file.GetText(cancellationToken)?.ToString() ?? "",
                    rootNamespace ?? "Square.Sample");
            })
            .Collect();

        // Compilation drives directive catalog refresh (metadata scan of referenced assemblies).
        var compilationAndInputs = context.CompilationProvider.Combine(inputs);

        context.RegisterSourceOutput(compilationAndInputs, static (productionContext, pair) =>
        {
            var compilation = pair.Left;
            var files = pair.Right;
            DirectiveCatalog catalog;
            try
            {
                catalog = DirectiveCatalog.FromCompilation(compilation);
            }
            catch (Exception ex)
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.SqxDiagnostics.SQXD001_DuplicateDirective,
                    Location.None,
                    ex.Message));
                catalog = DirectiveCatalog.BuiltIn;
            }

            var contracts = BuildPropContracts(files);
            foreach (var file in files)
                Generate(productionContext, file, contracts, catalog);
        });
    }

    private static void Generate(
        SourceProductionContext context,
        SqxInput input,
        IReadOnlyDictionary<string, PropContract[]> contracts,
        DirectiveCatalog catalog)
    {
        string code;
        try
        {
            var document = ParseDocument(input);
            ValidateRequiredProps(context, input, document, contracts);
            ValidateRefNames(context, input, document);
            DirectiveValidator.Validate(context, input.Path, input.Content, document, catalog);
            code = new ComponentEmitter(document, input.Namespace).Emit();
        }
        catch (SqxParseException exception)
        {
            code = $"// Generator error: {exception.Message}\n// Path: {input.Path}";
            var source = SourceText.From(input.Content, Encoding.UTF8);
            var position = Math.Max(0, Math.Min(exception.Position, source.Length));
            var span = new TextSpan(position, 0);
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.SqxDiagnostics.SQX0001_SyntaxError,
                Location.Create(input.Path, span, source.Lines.GetLinePositionSpan(span)),
                exception.Message));
        }
        catch (Exception exception)
        {
            code = $"// Generator error: {exception.Message}\n// Path: {input.Path}";
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.SqxDiagnostics.SQX0001_SyntaxError,
                Location.None,
                exception.Message));
        }

        var hintName = Path.GetFileNameWithoutExtension(input.Path) + "_" + StableHash(input.Path) + ".g.cs";
        context.AddSource(hintName, SourceText.From(code, Encoding.UTF8));
    }

    private static IReadOnlyDictionary<string, PropContract[]> BuildPropContracts(ImmutableArray<SqxInput> inputs)
    {
        var contracts = new Dictionary<string, PropContract[]>(StringComparer.Ordinal);
        foreach (var input in inputs)
        {
            var script = ExtractScript(input.Content);
            if (script == null) continue;
            var matches = Regex.Matches(
                script,
                @"\[Prop(?:Attribute)?\s*(?:\((?<options>[^)]*)\))?\]\s*(?:public|internal|protected|private)?\s*(?<type>[A-Za-z_][A-Za-z0-9_<>?., ]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{");
            var props = new List<PropContract>();
            foreach (Match match in matches)
            {
                var options = match.Groups["options"].Value;
                props.Add(new PropContract(
                    match.Groups["name"].Value,
                    match.Groups["type"].Value.Trim(),
                    options.Contains("Required", StringComparison.OrdinalIgnoreCase) &&
                    options.Contains("true", StringComparison.OrdinalIgnoreCase)));
            }
            if (props.Count > 0)
                contracts[Path.GetFileNameWithoutExtension(input.Path)] = props.ToArray();
        }
        return contracts;
    }

    private static SqxDocument ParseDocument(SqxInput input) =>
        input.Path.EndsWith(".sqv", StringComparison.OrdinalIgnoreCase)
            ? SqvParser.Parse(input.Content, input.Path)
            : SqxParser.Parse(input.Content, input.Path);

    private static bool IsTemplateFile(string path) =>
        path.EndsWith(".sqx", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".sqv", StringComparison.OrdinalIgnoreCase);

    private static string ExtractScript(string source)
    {
        var start = source.IndexOf("<script", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        var openEnd = source.IndexOf('>', start);
        if (openEnd < 0) return null;
        var close = source.IndexOf("</script", openEnd, StringComparison.OrdinalIgnoreCase);
        return close < 0 ? null : source.Substring(openEnd + 1, close - openEnd - 1);
    }

    private static void ValidateRequiredProps(
        SourceProductionContext context,
        SqxInput input,
        SqxDocument document,
        IReadOnlyDictionary<string, PropContract[]> contracts)
    {
        foreach (var element in EnumerateElements(document.Template.Roots))
        {
            if (!contracts.TryGetValue(element.TagName, out var props)) continue;
            foreach (var prop in props)
            {
                var attr = element.Attributes.FirstOrDefault(a =>
                    string.Equals(a.Name, prop.Name, StringComparison.OrdinalIgnoreCase));
                if (attr == null)
                {
                    if (prop.Required)
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.SqxDiagnostics.SQX0003_RequiredPropMissing,
                            CreateLocation(input, element.Line, element.Column),
                            element.TagName,
                            prop.Name));
                    continue;
                }
                if (!attr.IsExpression && !string.IsNullOrEmpty(attr.RawValue))
                {
                    var innerType = ExtractInnerType(prop.TypeName);
                    if (!IsAssignableTo(innerType, attr.RawValue))
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.SqxDiagnostics.SQX0007_PropTypeMismatch,
                            CreateLocation(input, element.Line, element.Column),
                            prop.Name));
                }
            }
        }
    }

    private static string ExtractInnerType(string typeName)
    {
        var open = typeName.IndexOf('<');
        var close = typeName.LastIndexOf('>');
        return open >= 0 && close > open ? typeName.Substring(open + 1, close - open - 1).Trim() : typeName;
    }

    private static bool IsAssignableTo(string innerType, string value)
    {
        if (string.IsNullOrEmpty(innerType)) return true;
        if (innerType == "string") return true;
        if (innerType == "int" || innerType == "Int32")
            return int.TryParse(value, out _);
        if (innerType == "float" || innerType == "Single")
            return float.TryParse(value, out _);
        if (innerType == "double" || innerType == "Double")
            return double.TryParse(value, out _);
        if (innerType == "bool" || innerType == "Boolean")
            return bool.TryParse(value, out _);
        return true;
    }

    private static IEnumerable<SqxElement> EnumerateElements(IEnumerable<SqxNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is not SqxElement element) continue;
            yield return element;
            foreach (var child in EnumerateElements(element.Children))
                yield return child;
        }
    }

    private static void ValidateRefNames(
        SourceProductionContext context,
        SqxInput input,
        SqxDocument document)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in EnumerateElements(document.Template.Roots))
        {
            var refAttr = element.Attributes.FirstOrDefault(
                a => string.Equals(a.Name, "ref", StringComparison.OrdinalIgnoreCase));
            if (refAttr == null || string.IsNullOrWhiteSpace(refAttr.RawValue)) continue;
            if (!seen.Add(refAttr.RawValue))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.SqxDiagnostics.SQX0006_RefNameConflict,
                    CreateLocation(input, element.Line, element.Column),
                    refAttr.RawValue));
            }
        }
    }

    private static Location CreateLocation(SqxInput input, int line, int column)
    {
        var source = SourceText.From(input.Content, Encoding.UTF8);
        var lineIndex = Math.Max(0, Math.Min(line - 1, source.Lines.Count - 1));
        var textLine = source.Lines[lineIndex];
        var position = Math.Min(textLine.End, textLine.Start + Math.Max(0, column - 1));
        var span = new TextSpan(position, 0);
        return Location.Create(input.Path, span, source.Lines.GetLinePositionSpan(span));
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value)
                hash = (hash ^ character) * 16777619u;
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

    private sealed class PropContract
    {
        public string Name { get; }
        public string TypeName { get; }
        public bool Required { get; }

        public PropContract(string name, string typeName, bool required)
        {
            Name = name;
            TypeName = typeName;
            Required = required;
        }
    }
}
