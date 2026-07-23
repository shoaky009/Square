using Square.UI;
using Square.UI.ElementApi;

namespace Square.CSS.Engine;

public static class CssStyleReconciler
{
    private static readonly object Gate = new();
    private static readonly List<StyleScope> Scopes = [];
    private static readonly HashSet<Element> DirtyElements = [];
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
        StyleScope[] scopes;
        lock (Gate)
        {
            if (DirtyElements.Count == 0) return;
            DirtyElements.Clear();
            scopes = Scopes.ToArray();
        }

        _applying++;
        try
        {
            var layoutRoots = new HashSet<Element>();
            foreach (var scope in scopes)
                layoutRoots.Add(FindTreeRoot(scope.Root));
            var layoutSnapshots = layoutRoots.Select(CaptureLayoutSnapshot).ToArray();

            var clearedRoots = new HashSet<Element>();
            foreach (var scope in scopes)
            {
                if (!clearedRoots.Add(scope.Root)) continue;
                ClearCascadedSubtree(scope.Root);
            }

            foreach (var scope in scopes)
                scope.Engine.ApplyStylesToTreeCore(scope.Root);

            foreach (var snapshot in layoutSnapshots)
                RestoreLayoutDirtyState(snapshot);
        }
        finally
        {
            _applying--;
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

    private static LayoutSnapshot CaptureLayoutSnapshot(Element element)
    {
        var properties = element.Style.GetAll()
            .Where(pair => (StyleInvalidation.ForProperty(pair.Key) & ElementInvalidation.Layout) != 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var children = element.Children.Select(CaptureLayoutSnapshot).ToArray();
        return new LayoutSnapshot(element, element.IsLayoutDirty, properties, children);
    }

    private static bool RestoreLayoutDirtyState(LayoutSnapshot snapshot)
    {
        var childNeedsLayout = false;
        foreach (var child in snapshot.Children)
            childNeedsLayout |= RestoreLayoutDirtyState(child);

        var current = snapshot.Element.Style.GetAll()
            .Where(pair => (StyleInvalidation.ForProperty(pair.Key) & ElementInvalidation.Layout) != 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var ownLayoutChanged = snapshot.LayoutProperties.Count != current.Count ||
            snapshot.LayoutProperties.Any(pair => !current.TryGetValue(pair.Key, out var value) || value != pair.Value);
        var needsLayout = snapshot.WasLayoutDirty || ownLayoutChanged || childNeedsLayout;
        if (!needsLayout) snapshot.Element.ClearLayoutDirty();
        return needsLayout;
    }

    private sealed record LayoutSnapshot(
        Element Element,
        bool WasLayoutDirty,
        IReadOnlyDictionary<string, string> LayoutProperties,
        IReadOnlyList<LayoutSnapshot> Children);

    private readonly record struct StyleScope(CssEngine Engine, Element Root);
}
