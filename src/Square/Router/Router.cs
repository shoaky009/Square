using Square.Controls;
using Square.UI;

namespace Square.Router;

public sealed class Router : View
{
    private bool _started;

    public string InitialPath { get; set; } = "/";
    public List<RouteDefinition> Routes { get; } = [];
    public INavigationHistory? History { get; set; }
    public RouteContext? Current { get; private set; }
    public event Action<RouteContext>? Navigated;

    public void Start()
    {
        if (_started) return;
        _started = true;
        History ??= new MemoryNavigationHistory(InitialPath);
        History.Changed += Activate;
        Activate(History.Current);
    }

    public bool Navigate(string location, bool replace = false)
    {
        EnsureStarted();
        if (RouteMatcher.Match(Routes, location) == null) return false;
        if (replace) History!.Replace(location);
        else History!.Push(location);
        return true;
    }

    public bool Replace(string location) => Navigate(location, replace: true);

    public bool Back()
    {
        EnsureStarted();
        return History!.Back();
    }

    public bool Forward()
    {
        EnsureStarted();
        return History!.Forward();
    }

    protected override void OnAttachedCore()
    {
        base.OnAttachedCore();
        Start();
    }

    protected override void OnDetachedCore()
    {
        if (_started && History != null)
            History.Changed -= Activate;
        _started = false;
        base.OnDetachedCore();
    }

    private void EnsureStarted()
    {
        if (!_started) Start();
    }

    private void Activate(string location)
    {
        var match = RouteMatcher.Match(Routes, location);
        if (match == null) return;
        var parsed = RouteContext.ParseLocation(location);
        var context = new RouteContext(location, parsed.Path, match.Parameters, parsed.Query);
        var root = BuildBranch(match.Branch, context);

        Children.Clear();
        Current = context;
        SetProperty(RouteContext.PropertyName, context);
        if (root != null) Children.Add(root);
        Navigated?.Invoke(context);
    }

    private static UIElement? BuildBranch(IReadOnlyList<RouteDefinition> branch, RouteContext context)
    {
        UIElement? nested = null;
        for (var i = branch.Count - 1; i >= 0; i--)
        {
            var factory = branch[i].ComponentFactory;
            if (factory == null) continue;
            var page = factory();
            page.SetProperty(RouteContext.PropertyName, context);
            if (nested != null)
            {
                var captured = nested;
                page.Slots.Set("", parent => parent.Children.Add(captured));
            }
            page.BuildElementTree();
            nested = page;
        }
        return nested;
    }
}
