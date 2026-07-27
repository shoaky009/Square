using Square.UI;

namespace Square.Extensions.Routing;

public sealed class RouteLocation
{
    internal const string PropertyName = "__routing_location";

    internal RouteLocation(
        string fullPath,
        string path,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyDictionary<string, string> query,
        IReadOnlyList<RouteMatchEntry> matched)
    {
        FullPath = fullPath;
        Path = path;
        Parameters = parameters;
        Query = query;
        Matched = matched;
    }

    public string FullPath { get; }
    public string Path { get; }
    public IReadOnlyDictionary<string, string> Parameters { get; }
    public IReadOnlyDictionary<string, string> Query { get; }
    public IReadOnlyList<RouteMatchEntry> Matched { get; }

    public T? GetMeta<T>(int depth = -1)
    {
        if (Matched.Count == 0) return default;
        var index = depth < 0 ? Matched.Count - 1 : depth;
        return index >= 0 && index < Matched.Count && Matched[index].Definition.Meta is T value ? value : default;
    }

    public static RouteLocation? Find(Element element)
    {
        for (Element? current = element; current != null; current = current.Parent)
            if (current.Properties.TryGetValue(PropertyName, out RouteLocation value)) return value;
        return null;
    }

    internal static (string Path, IReadOnlyDictionary<string, string> Query) Parse(string location)
    {
        location = string.IsNullOrWhiteSpace(location) ? "/" : location.Trim();
        var hash = location.IndexOf('#');
        if (hash >= 0) location = location[..hash];
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

public interface IRouteAware
{
    void OnRouteActivated(RouteLocation route) { }
    void OnRouteDeactivated(RouteLocation route) { }
    void OnRouteUpdated(RouteLocation to, RouteLocation from) { }
}
