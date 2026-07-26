using Square.Directives;

namespace Square.Controls.Directives;

/// <summary>标记 <c>Show</c> 指令：条件渲染节点。</summary>
[SqxDirective("Show", Pattern = "ControlFlowAttach", RuntimeTypeName = "ShowNode", FieldPrefix = "_show", PrimaryAttribute = "when")]
public static class ShowDirectiveMarker;

/// <summary>标记 <c>For</c> 指令：列表循环渲染节点。</summary>
[SqxDirective("For", Pattern = "ControlFlowAttach", RuntimeTypeName = "ForNode", FieldPrefix = "_for", PrimaryAttribute = "each")]
public static class ForDirectiveMarker;

/// <summary>标记 <c>Index</c> 指令：按下标循环渲染节点。</summary>
[SqxDirective("Index", Pattern = "ControlFlowAttach", RuntimeTypeName = "IForNode", FieldPrefix = "_index", PrimaryAttribute = "each")]
public static class IndexDirectiveMarker;

/// <summary>标记 <c>Switch</c> 指令：多分支渲染节点。</summary>
[SqxDirective("Switch", Pattern = "ControlFlowAttach", RuntimeTypeName = "SwitchNode", FieldPrefix = "_switch", AllowedChildTags = new[] { "Match" })]
public static class SwitchDirectiveMarker;

/// <summary>标记 <c>Match</c> 指令：作为 <c>Switch</c> 的分支。</summary>
[SqxDirective("Match", ParentTag = "Switch", SkipStandaloneEmit = true, PrimaryAttribute = "when")]
public static class MatchDirectiveMarker;

/// <summary>标记 <c>Slot</c> 指令：插槽出口，别名 <c>Outlet</c>。</summary>
[SqxDirective("Slot", Aliases = new[] { "Outlet" }, Pattern = "SlotOutlet", PrimaryAttribute = "name")]
public static class SlotDirectiveMarker;
