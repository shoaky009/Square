using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Square.Compiler.Diagnostics;
using Square.Compiler.Parser;

namespace Square.Compiler.Directives;

/// <summary>
/// 结构指令语义校验：父子关系、必需属性、SkipStandalone 非法位置、未知 Pattern。
/// </summary>
internal static class DirectiveValidator
{
    public static void Validate(
        SourceProductionContext context,
        string filePath,
        string content,
        SqxDocument document,
        DirectiveCatalog catalog)
    {
        ValidateNodes(context, filePath, content, document.Template.Roots, parentDirectiveId: null, catalog);
    }

    private static void ValidateNodes(
        SourceProductionContext context,
        string filePath,
        string content,
        IEnumerable<SqxNode> nodes,
        string parentDirectiveId,
        DirectiveCatalog catalog)
    {
        foreach (var node in nodes)
        {
            if (node is not SqxElement element) continue;

            if (catalog.TryGet(element.TagName, out var descriptor))
            {
                var id = descriptor.TagName;

                // SQXD004: unknown pattern
                if (!IsKnownPattern(descriptor.Pattern) &&
                    !IsKnownTagFallback(id))
                {
                    Report(context, filePath, content, element,
                        SqxDiagnostics.SQXD004_UnknownPattern, id, descriptor.Pattern ?? "");
                }

                // SQXD002: required primary attribute
                if (!string.IsNullOrEmpty(descriptor.PrimaryAttribute) &&
                    RequiresPrimaryAttribute(id))
                {
                    var attr = FindAttr(element, descriptor.PrimaryAttribute);
                    if (attr == null || string.IsNullOrWhiteSpace(attr.RawValue))
                    {
                        Report(context, filePath, content, element,
                            SqxDiagnostics.SQXD002_MissingRequiredAttribute, id, descriptor.PrimaryAttribute);
                    }
                }

                // SQXD003 / SQXD005: parent constraints
                if (!IsValidParent(id, descriptor, parentDirectiveId))
                {
                    if (parentDirectiveId == null && descriptor.SkipStandaloneEmit)
                    {
                        Report(context, filePath, content, element,
                            SqxDiagnostics.SQXD005_IllegalStandalone, id);
                    }
                    else
                    {
                        var expected = DescribeExpectedParent(id, descriptor);
                        Report(context, filePath, content, element,
                            SqxDiagnostics.SQXD003_InvalidParent, id, expected);
                    }
                }

                if (!descriptor.AllowedChildTags.IsDefaultOrEmpty)
                {
                    foreach (var child in element.Children.OfType<SqxElement>())
                    {
                        var childId = child.TagName;
                        if (catalog.TryGet(child.TagName, out var childDescriptor))
                            childId = childDescriptor.TagName;
                        if (descriptor.AllowedChildTags.Any(allowed =>
                            string.Equals(allowed, childId, StringComparison.Ordinal))) continue;
                        Report(context, filePath, content, child,
                            SqxDiagnostics.SQXD006_InvalidChild,
                            id,
                            string.Join(", ", descriptor.AllowedChildTags.Select(tag => "<" + tag + ">")),
                            child.TagName);
                    }
                }

                ValidateNodes(context, filePath, content, element.Children, id, catalog);
            }
            else
            {
                ValidateNodes(context, filePath, content, element.Children, parentDirectiveId: null, catalog);
            }
        }
    }

    private static bool RequiresPrimaryAttribute(string directiveId) =>
        directiveId is "Show" or "For" or "Index"; // Match 的 when 可选（default 分支）

    private static bool IsKnownPattern(string pattern) =>
        pattern is "ControlFlowAttach" or "SlotOutlet";

    private static bool IsKnownTagFallback(string id) =>
        id is "Show" or "For" or "Index" or "Switch" or "Match" or "Slot";

    /// <summary>
    /// Match → Switch。
    /// </summary>
    private static bool IsValidParent(string id, DirectiveDescriptor descriptor, string parentDirectiveId)
    {
        if (string.IsNullOrEmpty(descriptor.ParentTag) && !descriptor.SkipStandaloneEmit)
            return true;

        if (id == "Match")
            return parentDirectiveId == "Switch";

        if (!string.IsNullOrEmpty(descriptor.ParentTag))
            return string.Equals(parentDirectiveId, descriptor.ParentTag, StringComparison.Ordinal);

        // SkipStandalone 但无 ParentTag：仅禁止根级
        return parentDirectiveId != null || !descriptor.SkipStandaloneEmit;
    }

    private static string DescribeExpectedParent(string id, DirectiveDescriptor descriptor)
    {
        if (id == "Match") return "Switch";
        return descriptor.ParentTag ?? "(非根)";
    }

    private static SqxAttribute FindAttr(SqxElement element, string name) =>
        element.Attributes.FirstOrDefault(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

    private static void Report(
        SourceProductionContext context,
        string filePath,
        string content,
        SqxElement element,
        DiagnosticDescriptor descriptor,
        params object[] messageArgs)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            descriptor,
            CreateLocation(filePath, content, element.Line, element.Column),
            messageArgs));
    }

    private static Location CreateLocation(string filePath, string content, int line, int column)
    {
        var source = SourceText.From(content, System.Text.Encoding.UTF8);
        var lineIndex = Math.Max(0, Math.Min(line - 1, source.Lines.Count - 1));
        var textLine = source.Lines[lineIndex];
        var position = Math.Min(textLine.End, textLine.Start + Math.Max(0, column - 1));
        var span = new TextSpan(position, 0);
        return Location.Create(filePath, span, source.Lines.GetLinePositionSpan(span));
    }
}
