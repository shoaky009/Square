namespace Square.Extensions.Routing;

public static class RouteMatcher
{
    public static RouteMatch? Match(IEnumerable<RouteDefinition> routes, string location)
    {
        var path = RouteLocation.Parse(location).Path;
        var segments = Split(path);
        foreach (var route in Order(routes))
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (TryMatch(route, segments, 0, parameters, [], out var branch))
                return new RouteMatch(branch, parameters);
        }
        return null;
    }

    private static bool TryMatch(
        RouteDefinition route,
        IReadOnlyList<string> segments,
        int start,
        Dictionary<string, string> parameters,
        List<RouteMatchEntry> parents,
        out IReadOnlyList<RouteMatchEntry> branch)
    {
        var local = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase);
        var index = start;
        var wildcard = false;
        foreach (var segment in Split(route.Path))
        {
            if (segment.StartsWith('*'))
            {
                local[segment.Length == 1 ? "wildcard" : segment[1..]] = string.Join("/", segments.Skip(index));
                index = segments.Count;
                wildcard = true;
                break;
            }
            if (index >= segments.Count) { branch = []; return false; }
            if (segment.StartsWith(':'))
            {
                if (segment.Length == 1) { branch = []; return false; }
                local[segment[1..]] = segments[index++];
            }
            else if (!string.Equals(segment, segments[index++], StringComparison.OrdinalIgnoreCase))
            {
                branch = [];
                return false;
            }
        }

        var matchedPath = index == 0 ? "/" : "/" + string.Join("/", segments.Take(index));
        var current = new List<RouteMatchEntry>(parents) { new(route, matchedPath) };
        if (route.Children.Count > 0)
        {
            foreach (var child in Order(route.Children))
                if (TryMatch(child, segments, index, local, current, out branch))
                {
                    Copy(local, parameters);
                    return true;
                }
            branch = [];
            return false;
        }
        if (!wildcard && index != segments.Count) { branch = []; return false; }
        Copy(local, parameters);
        branch = current;
        return true;
    }

    private static void Copy(Dictionary<string, string> source, Dictionary<string, string> target)
    {
        target.Clear();
        foreach (var pair in source) target[pair.Key] = pair.Value;
    }

    private static IEnumerable<RouteDefinition> Order(IEnumerable<RouteDefinition> routes) =>
        routes.Select((route, index) => (route, index))
            .OrderBy(item => Rank(item.route.Path))
            .ThenByDescending(item => Split(item.route.Path).Length)
            .ThenBy(item => item.index)
            .Select(item => item.route);

    private static int Rank(string path)
    {
        var rank = 0;
        foreach (var segment in Split(path))
        {
            if (segment.StartsWith('*')) return 2;
            if (segment.StartsWith(':')) rank = 1;
        }
        return rank;
    }

    private static string[] Split(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Uri.UnescapeDataString).ToArray();
}
