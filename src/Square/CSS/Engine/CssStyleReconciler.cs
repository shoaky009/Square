using Square.UI;
using Square.UI.ElementApi;

namespace Square.CSS.Engine;

public static class CssStyleReconciler
{
    private static readonly object Gate = new();
    private static readonly object ApplyGate = new();
    private static readonly List<StyleScope> Scopes = [];
    private static readonly HashSet<Element> DirtyElements = [];
    [ThreadStatic]
    private static int _applying;

    static CssStyleReconciler()
    {
        Element.StyleInvalidated += MarkDirty;
    }

    public static bool HasWork
    {
        get { lock (Gate) return DirtyElements.Count > 0; }
    }

    internal static void RegisterScope(CssEngine engine, Element root)
    {
        _ = HasWork; // Ensures the static constructor subscribed to Element.StyleInvalidated.
        lock (Gate)
        {
            foreach (var scope in Scopes)
            {
                if (ReferenceEquals(scope.Engine, engine) && ReferenceEquals(scope.Root, root))
                    return;
            }
            Scopes.Add(new StyleScope(engine, root));
        }
    }

    public static void Flush()
    {
        lock (ApplyGate)
        {
            StyleScope[] scopes;
            lock (Gate)
            {
                if (DirtyElements.Count == 0) return;
                var dirtyElements = DirtyElements.ToArray();
                DirtyElements.Clear();
                var dirtyRoots = dirtyElements.Select(FindTreeRoot).ToHashSet();
                scopes = Scopes
                    .Where(scope => dirtyRoots.Contains(FindTreeRoot(scope.Root)))
                    .ToArray();
            }

            _applying++;
            try
            {
                var styleRoots = new HashSet<Element>();
                foreach (var scope in scopes)
                    styleRoots.Add(scope.Root);
                var styleSnapshots = styleRoots.Select(CaptureStyleSnapshot).ToArray();

                using (Element.SuppressInvalidation())
                {
                    foreach (var root in styleRoots)
                        ClearCascadedSubtree(root);

                    foreach (var scope in scopes)
                        scope.Engine.ApplyStylesToTreeCore(scope.Root);
                }

                foreach (var snapshot in styleSnapshots)
                    ApplyStyleDifferences(snapshot);
            }
            finally
            {
                _applying--;
            }
        }
    }

    private static void MarkDirty(Element element)
    {
        if (_applying > 0) return;
        lock (Gate)
            DirtyElements.Add(element);
    }

    private static void ClearCascadedSubtree(Element element)
    {
        element.Style.ClearCascaded();
        foreach (var child in element.Children)
            ClearCascadedSubtree(child);
    }

    private static Element FindTreeRoot(Element element)
    {
        while (element.Parent != null)
            element = element.Parent;
        return element;
    }

    private static StyleSnapshot CaptureStyleSnapshot(Element element)
    {
        var properties = element.Style.GetAll();
        var children = element.Children.Select(CaptureStyleSnapshot).ToArray();
        return new StyleSnapshot(element, properties, children);
    }

    private static void ApplyStyleDifferences(StyleSnapshot snapshot)
    {
        foreach (var child in snapshot.Children)
            ApplyStyleDifferences(child);

        var current = snapshot.Element.Style.GetAll()
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var invalidation = ElementInvalidation.None;
        foreach (var property in snapshot.Properties.Keys.Concat(current.Keys).Distinct(StringComparer.Ordinal))
        {
            snapshot.Properties.TryGetValue(property, out var previousValue);
            current.TryGetValue(property, out var currentValue);
            if (!string.Equals(previousValue, currentValue, StringComparison.Ordinal))
                invalidation |= StyleInvalidation.ForProperty(property);
        }

        if (invalidation != ElementInvalidation.None)
            snapshot.Element.Invalidate(invalidation);
    }

    private sealed record StyleSnapshot(
        Element Element,
        IReadOnlyDictionary<string, string> Properties,
        IReadOnlyList<StyleSnapshot> Children);

    private readonly record struct StyleScope(CssEngine Engine, Element Root);
}
