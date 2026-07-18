using Square.Directives;

namespace Square.Router.Directives;

[SqxDirective("Router", Pattern = "RouterTree", AllowedChildTags = new[] { "Route" })]
public static class RouterDirectiveMarker;

[SqxDirective("Route", ParentTag = "Router", SkipStandaloneEmit = true, PrimaryAttribute = "path")]
public static class RouteDirectiveMarker;
