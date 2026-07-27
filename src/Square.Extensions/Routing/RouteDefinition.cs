using Square.UI;

namespace Square.Extensions.Routing;

public sealed class RouteDefinition
{
    internal RouteDefinition(string path, Func<UIElement> pageFactory)
    {
        Path = string.IsNullOrWhiteSpace(path) ? "" : path.Trim();
        PageFactory = pageFactory;
    }

    public string Path { get; }
    public string? Name { get; set; }
    public bool KeepAlive { get; set; }
    public object? Meta { get; set; }
    public Func<RouteLocation, string>? CacheKeySelector { get; set; }
    public List<RouteDefinition> Children { get; } = [];
    internal Func<UIElement> PageFactory { get; }
}

public sealed class RouteCollectionBuilder
{
    private readonly List<RouteDefinition> _routes;

    internal RouteCollectionBuilder(List<RouteDefinition> routes) => _routes = routes;

    public RouteDefinition Map(
        string path,
        Func<UIElement> pageFactory,
        Action<RouteDefinitionBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(pageFactory);
        var definition = new RouteDefinition(path, pageFactory);
        _routes.Add(definition);
        configure?.Invoke(new RouteDefinitionBuilder(definition));
        return definition;
    }
}

public sealed class RouteDefinitionBuilder
{
    private readonly RouteDefinition _definition;

    internal RouteDefinitionBuilder(RouteDefinition definition) => _definition = definition;

    public string? Name { get => _definition.Name; set => _definition.Name = value; }
    public bool KeepAlive { get => _definition.KeepAlive; set => _definition.KeepAlive = value; }
    public object? Meta { get => _definition.Meta; set => _definition.Meta = value; }
    public Func<RouteLocation, string>? CacheKeySelector
    {
        get => _definition.CacheKeySelector;
        set => _definition.CacheKeySelector = value;
    }

    public RouteDefinition Map(
        string path,
        Func<UIElement> pageFactory,
        Action<RouteDefinitionBuilder>? configure = null) =>
        new RouteCollectionBuilder(_definition.Children).Map(path, pageFactory, configure);
}

public sealed class RouteMatch
{
    internal RouteMatch(
        IReadOnlyList<RouteMatchEntry> branch,
        IReadOnlyDictionary<string, string> parameters)
    {
        Branch = branch;
        Parameters = parameters;
    }

    public IReadOnlyList<RouteMatchEntry> Branch { get; }
    public IReadOnlyDictionary<string, string> Parameters { get; }
}

public sealed class RouteMatchEntry
{
    internal RouteMatchEntry(RouteDefinition definition, string matchedPath)
    {
        Definition = definition;
        MatchedPath = matchedPath;
    }

    public RouteDefinition Definition { get; }
    public string MatchedPath { get; }
}
