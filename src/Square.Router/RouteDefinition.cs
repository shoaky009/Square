using Square.UI;

namespace Square.Router;

public sealed class RouteDefinition
{
    public RouteDefinition(string path, Func<UIElement>? componentFactory = null)
    {
        Path = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        ComponentFactory = componentFactory;
    }

    public string Path { get; }
    public Func<UIElement>? ComponentFactory { get; }
    public List<RouteDefinition> Children { get; } = [];
}

public sealed class RouteMatch
{
    internal RouteMatch(IReadOnlyList<RouteDefinition> branch, IReadOnlyDictionary<string, string> parameters)
    {
        Branch = branch;
        Parameters = parameters;
    }

    public IReadOnlyList<RouteDefinition> Branch { get; }
    public IReadOnlyDictionary<string, string> Parameters { get; }
}
