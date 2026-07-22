using System.Text;
using Square.Compiler.Directives;
using Square.Compiler.Parser;

namespace Square.Compiler.Emit;

/// <summary>
/// Catalog-driven directive emission (D2). Patterns map to declarative templates.
/// </summary>
internal sealed class DirectiveEmitPipeline
{
    private readonly StringBuilder _sb;
    private readonly DirectiveCatalog _catalog;
    private readonly Action<List<SqxNode>, string, string, string> _emitNodes;
    private readonly Action<List<SqxNode>, string, string> _emitFactoryBody;
    private readonly Action<string, SqxAttribute, string, string> _emitAttribute;
    private readonly Func<string> _nextVariable;
    private readonly Dictionary<string, int> _fieldIndex = new Dictionary<string, int>(StringComparer.Ordinal);

    public DirectiveEmitPipeline(
        StringBuilder sb,
        DirectiveCatalog catalog,
        Action<List<SqxNode>, string, string, string> emitNodes,
        Action<List<SqxNode>, string, string> emitFactoryBody,
        Action<string, SqxAttribute, string, string> emitAttribute,
        Func<string> nextVariable)
    {
        _sb = sb;
        _catalog = catalog;
        _emitNodes = emitNodes;
        _emitFactoryBody = emitFactoryBody;
        _emitAttribute = emitAttribute;
        _nextVariable = nextVariable;
    }

    public void ResetFieldIndexes() => _fieldIndex.Clear();

    public int NextFieldIndex(string prefix)
    {
        if (!_fieldIndex.TryGetValue(prefix, out var i)) i = 0;
        _fieldIndex[prefix] = i + 1;
        return i;
    }

    public bool TryEmit(SqxElement element, string indent, string parentName, string localName)
    {
        if (!_catalog.TryGet(element.TagName, out var descriptor))
            return false;

        if (descriptor.SkipStandaloneEmit)
            return true; // consumed (e.g. Match/Route handled by parent)

        switch (descriptor.Pattern)
        {
            case "ControlFlowAttach":
                EmitControlFlow(descriptor, element, indent, parentName, localName);
                return true;
            case "SlotOutlet":
                EmitSlotOutlet(descriptor, element, indent, parentName, localName);
                return true;
            case "RouterTree":
                EmitRouterTree(element, indent, parentName, localName);
                return true;
            default:
                // Fall back by resolved tag name for known built-ins
                if (descriptor.TagName == "Show" || descriptor.TagName == "For" || descriptor.TagName == "Switch")
                {
                    EmitControlFlow(descriptor, element, indent, parentName, localName);
                    return true;
                }
                if (descriptor.TagName == "Slot")
                {
                    EmitSlotOutlet(descriptor, element, indent, parentName, localName);
                    return true;
                }
                if (descriptor.TagName == "Router")
                {
                    EmitRouterTree(element, indent, parentName, localName);
                    return true;
                }
                return false;
        }
    }

    private void EmitControlFlow(
        DirectiveDescriptor descriptor,
        SqxElement element,
        string indent,
        string parentName,
        string localName)
    {
        var tag = descriptor.TagName;
        var prefix = string.IsNullOrEmpty(descriptor.FieldPrefix) ? "_dir" : descriptor.FieldPrefix;
        var index = NextFieldIndex(prefix);
        var field = prefix + index;

        if (tag == "Show")
        {
            var condition = FindAttr(element, descriptor.PrimaryAttribute ?? "when")?.RawValue
                            ?? "new ObservableValue<bool>(false)";
            _sb.AppendLine(indent + field + " = new ShowNode(" + condition + ", () =>");
            _sb.AppendLine(indent + "{");
            _emitFactoryBody(element.Children, indent + "    ", localName);
            _sb.AppendLine(indent + "});");
            _sb.AppendLine(indent + field + ".AttachTo(" + parentName + ");");
            return;
        }

        if (tag == "For")
        {
            var source = FindAttr(element, descriptor.PrimaryAttribute ?? "each")?.RawValue
                         ?? "System.Array.Empty<object>()";
            _sb.AppendLine(indent + field + " = ForNode.Create(" + source + ", it =>");
            _sb.AppendLine(indent + "{");
            _emitFactoryBody(element.Children, indent + "    ", "it");
            _sb.AppendLine(indent + "});");
            _sb.AppendLine(indent + field + ".AttachTo(" + parentName + ");");
            return;
        }

        if (tag == "Switch")
        {
            _sb.AppendLine(indent + field + " = new SwitchNode(() => 0);");
            foreach (var child in element.Children)
            {
                if (child is not SqxElement matchElement) continue;
                if (!_catalog.TryGet(matchElement.TagName, out var childDesc) || childDesc.TagName != "Match")
                    continue;

                var when = FindAttr(matchElement, childDesc.PrimaryAttribute ?? "when")?.RawValue;
                if (when != null)
                {
                    _sb.AppendLine(indent + field + ".AddBranch(() => " + when + ", () =>");
                    _sb.AppendLine(indent + "{");
                    _emitFactoryBody(matchElement.Children, indent + "    ", localName);
                    _sb.AppendLine(indent + "});");
                }
                else
                {
                    _sb.AppendLine(indent + field + ".AddDefault(() =>");
                    _sb.AppendLine(indent + "{");
                    _emitFactoryBody(matchElement.Children, indent + "    ", localName);
                    _sb.AppendLine(indent + "});");
                }
            }
            _sb.AppendLine(indent + field + ".AttachTo(" + parentName + ");");
        }
    }

    private void EmitSlotOutlet(
        DirectiveDescriptor descriptor,
        SqxElement element,
        string indent,
        string parentName,
        string localName)
    {
        var name = FindAttr(element, descriptor.PrimaryAttribute ?? "name")?.RawValue ?? "";
        _sb.AppendLine(indent + "if (!Slots.Render(\"" + Escape(name) + "\", " + parentName + "))");
        _sb.AppendLine(indent + "{");
        _emitNodes(element.Children, indent + "    ", parentName, localName);
        _sb.AppendLine(indent + "}");
    }

    private void EmitRouterTree(SqxElement element, string indent, string parentName, string localName)
    {
        var refAttr = FindAttr(element, "ref");
        var isRef = refAttr != null && !string.IsNullOrWhiteSpace(refAttr.RawValue);
        var variableName = isRef ? refAttr.RawValue : _nextVariable();
        if (isRef)
            _sb.AppendLine(indent + variableName + " = new Square.Router.Router();");
        else
            _sb.AppendLine(indent + "var " + variableName + " = new Square.Router.Router();");

        var initialPath = FindAttr(element, "initialPath");
        if (initialPath != null)
        {
            if (initialPath.IsExpression)
                _sb.AppendLine(indent + variableName + ".InitialPath = " + initialPath.RawValue + ";");
            else
                _sb.AppendLine(indent + variableName + ".InitialPath = \"" + Escape(initialPath.RawValue ?? "/") + "\";");
        }

        foreach (var attribute in element.Attributes)
        {
            if (attribute.Name == "initialPath" || attribute.Name == "ref") continue;
            _emitAttribute(variableName, attribute, indent, localName);
        }

        foreach (var route in element.Children.OfType<SqxElement>())
        {
            if (!_catalog.TryGet(route.TagName, out var childDesc) || childDesc.TagName != "Route")
                continue;
            var routeName = EmitRouteDefinition(route, indent);
            _sb.AppendLine(indent + variableName + ".Routes.Add(" + routeName + ");");
        }

        _sb.AppendLine(indent + variableName + ".Start();");
        _sb.AppendLine(indent + parentName + ".Children.Add(" + variableName + ");");
    }

    private string EmitRouteDefinition(SqxElement element, string indent)
    {
        var variableName = _nextVariable();
        var path = FindAttr(element, "path")?.RawValue ?? "/";
        var component = FindAttr(element, "component")?.RawValue;
        var factory = string.IsNullOrWhiteSpace(component) ? "null" : "() => new " + component + "()";
        _sb.AppendLine(indent + "var " + variableName + " = new Square.Router.RouteDefinition(\"" + Escape(path) + "\", " + factory + ");");

        foreach (var child in element.Children.OfType<SqxElement>())
        {
            if (!_catalog.TryGet(child.TagName, out var childDesc) || childDesc.TagName != "Route")
                continue;
            var childName = EmitRouteDefinition(child, indent);
            _sb.AppendLine(indent + variableName + ".Children.Add(" + childName + ");");
        }

        return variableName;
    }

    private static SqxAttribute FindAttr(SqxElement element, string name) =>
        element.Attributes.FirstOrDefault(attribute =>
            string.Equals(attribute.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
}
