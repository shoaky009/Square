using Square.Directives;

namespace Square.Router.Directives;

/// <summary>Router 指令标记。</summary>
[SqxDirective("Router", Pattern = "RouterTree", AllowedChildTags = new[] { "Route" })]
public static class RouterDirectiveMarker;

/// <summary>Route 指令标记。</summary>
[SqxDirective("Route", ParentTag = "Router", SkipStandaloneEmit = true, PrimaryAttribute = "path")]
public static class RouteDirectiveMarker;
