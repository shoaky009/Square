using Square.UI;

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
            var clearedRoots = new HashSet<Element>();
            foreach (var scope in scopes)
            {
                if (!clearedRoots.Add(scope.Root)) continue;
                ClearCascadedSubtree(scope.Root);
            }

            foreach (var scope in scopes)
                scope.Engine.ApplyStylesToTreeCore(scope.Root);
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

    private readonly record struct StyleScope(CssEngine Engine, Element Root);
}
