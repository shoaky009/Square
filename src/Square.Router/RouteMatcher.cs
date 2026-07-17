namespace Square.Router;

public static class RouteMatcher
{
    public static RouteMatch? Match(IEnumerable<RouteDefinition> routes, string location)
    {
        ArgumentNullException.ThrowIfNull(routes);
        var path = RouteContext.ParseLocation(location).Path;
        var segments = SplitPath(path);

        foreach (var route in OrderRoutes(routes))
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (TryMatch(route, segments, 0, parameters, out var branch))
                return new RouteMatch(branch, parameters);
        }

        return null;
    }

    private static bool TryMatch(
        RouteDefinition route,
        IReadOnlyList<string> locationSegments,
        int startIndex,
        Dictionary<string, string> parameters,
        out IReadOnlyList<RouteDefinition> branch)
    {
        var localParameters = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase);
        var routeSegments = SplitPath(route.Path);
        var index = startIndex;
        var wildcard = false;

        foreach (var segment in routeSegments)
        {
            if (segment.StartsWith("*", StringComparison.Ordinal))
            {
                var name = segment.Length == 1 ? "wildcard" : segment[1..];
                localParameters[name] = string.Join("/", locationSegments.Skip(index));
                index = locationSegments.Count;
                wildcard = true;
                break;
            }

            if (index >= locationSegments.Count)
            {
                branch = [];
                return false;
            }

            if (segment.StartsWith(":", StringComparison.Ordinal))
            {
                if (segment.Length == 1)
                {
                    branch = [];
                    return false;
                }
                localParameters[segment[1..]] = locationSegments[index++];
                continue;
            }

            if (!string.Equals(segment, locationSegments[index], StringComparison.OrdinalIgnoreCase))
            {
                branch = [];
                return false;
            }
            index++;
        }

        if (route.Children.Count > 0)
        {
            foreach (var child in OrderRoutes(route.Children))
            {
                if (!TryMatch(child, locationSegments, index, localParameters, out var childBranch)) continue;
                parameters.Clear();
                foreach (var pair in localParameters) parameters[pair.Key] = pair.Value;
                branch = new[] { route }.Concat(childBranch).ToArray();
                return true;
            }

            branch = [];
            return false;
        }

        if (!wildcard && index != locationSegments.Count)
        {
            branch = [];
            return false;
        }

        parameters.Clear();
        foreach (var pair in localParameters) parameters[pair.Key] = pair.Value;
        branch = [route];
        return true;
    }

    private static IEnumerable<RouteDefinition> OrderRoutes(IEnumerable<RouteDefinition> routes) =>
        routes.Select((route, index) => (route, index))
            .OrderBy(item => Rank(item.route.Path))
            .ThenByDescending(item => SplitPath(item.route.Path).Length)
            .ThenBy(item => item.index)
            .Select(item => item.route);

    private static int Rank(string path)
    {
        var rank = 0;
        foreach (var segment in SplitPath(path))
        {
            if (segment.StartsWith("*", StringComparison.Ordinal)) return 2;
            if (segment.StartsWith(":", StringComparison.Ordinal)) rank = Math.Max(rank, 1);
        }
        return rank;
    }

    private static string[] SplitPath(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
}
