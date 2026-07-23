using Square.Directives;

namespace Square.Controls.Directives;

[SqxDirective("Show", Pattern = "ControlFlowAttach", RuntimeTypeName = "ShowNode", FieldPrefix = "_show", PrimaryAttribute = "when")]
public static class ShowDirectiveMarker;

[SqxDirective("For", Pattern = "ControlFlowAttach", RuntimeTypeName = "ForNode", FieldPrefix = "_for", PrimaryAttribute = "each")]
public static class ForDirectiveMarker;

[SqxDirective("Index", Pattern = "ControlFlowAttach", RuntimeTypeName = "IForNode", FieldPrefix = "_index", PrimaryAttribute = "each")]
public static class IndexDirectiveMarker;

[SqxDirective("Switch", Pattern = "ControlFlowAttach", RuntimeTypeName = "SwitchNode", FieldPrefix = "_switch", AllowedChildTags = new[] { "Match" })]
public static class SwitchDirectiveMarker;

[SqxDirective("Match", ParentTag = "Switch", SkipStandaloneEmit = true, PrimaryAttribute = "when")]
public static class MatchDirectiveMarker;

[SqxDirective("Slot", Aliases = new[] { "Outlet" }, Pattern = "SlotOutlet", PrimaryAttribute = "name")]
public static class SlotDirectiveMarker;
