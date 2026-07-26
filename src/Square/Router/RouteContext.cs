using Square.UI;

namespace Square.Router;

/// <summary>路由上下文，包含路径、参数和查询字符串。</summary>
public sealed class RouteContext
{
    internal const string PropertyName = "__route_context";

    internal RouteContext(
        string location,
        string path,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyDictionary<string, string> query)
    {
        Location = location;
        Path = path;
        Parameters = parameters;
        Query = query;
    }

    /// <summary>完整位置（含查询字符串）。</summary>
    public string Location { get; }
    /// <summary>路径部分。</summary>
    public string Path { get; }
    /// <summary>路径参数。</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }
    /// <summary>查询字符串参数。</summary>
    public IReadOnlyDictionary<string, string> Query { get; }

    /// <summary>从元素树向上查找所属路由上下文。</summary>
    public static RouteContext? Find(Element Element)
    {
        for (Element? current = Element; current != null; current = current.Parent)
            if (current.Properties.TryGetValue(PropertyName, out RouteContext context)) return context;
        return null;
    }

    internal static (string Path, IReadOnlyDictionary<string, string> Query) ParseLocation(string location)
    {
        location = string.IsNullOrWhiteSpace(location) ? "/" : location.Trim();
        var hashIndex = location.IndexOf('#');
        if (hashIndex >= 0) location = location[..hashIndex];
        var queryIndex = location.IndexOf('?');
        var path = queryIndex >= 0 ? location[..queryIndex] : location;
        if (string.IsNullOrEmpty(path)) path = "/";
        if (!path.StartsWith("/", StringComparison.Ordinal)) path = "/" + path;

        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (queryIndex >= 0 && queryIndex + 1 < location.Length)
        {
            foreach (var part in location[(queryIndex + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = part.IndexOf('=');
                var key = separator < 0 ? part : part[..separator];
                var value = separator < 0 ? "" : part[(separator + 1)..];
                query[Decode(key)] = Decode(value);
            }
        }

        return (path, query);
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
}
