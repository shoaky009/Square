using Square.UI;

namespace Square.Router;

/// <summary>路由定义。</summary>
public sealed class RouteDefinition
{
    /// <summary>构造路由定义。</summary>
    public RouteDefinition(string path, Func<UIElement>? componentFactory = null)
    {
        Path = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        ComponentFactory = componentFactory;
    }

    /// <summary>路径模式（支持 :param 和 *wildcard）。</summary>
    public string Path { get; }
    /// <summary>组件工厂。</summary>
    public Func<UIElement>? ComponentFactory { get; }
    /// <summary>子路由。</summary>
    public List<RouteDefinition> Children { get; } = [];
}

/// <summary>路由匹配结果。</summary>
public sealed class RouteMatch
{
    internal RouteMatch(IReadOnlyList<RouteDefinition> branch, IReadOnlyDictionary<string, string> parameters)
    {
        Branch = branch;
        Parameters = parameters;
    }

    /// <summary>匹配的分支链。</summary>
    public IReadOnlyList<RouteDefinition> Branch { get; }
    /// <summary>提取的路径参数。</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }
}