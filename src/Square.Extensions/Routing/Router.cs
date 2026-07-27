using System.Runtime.CompilerServices;
using Square.Hosting;

namespace Square.Extensions.Routing;

public enum RouteGuardAction
{
    Allow,
    Cancel,
    Redirect
}

public readonly record struct RouteGuardResult(RouteGuardAction Action, string? Location = null)
{
    public static RouteGuardResult Allow => new(RouteGuardAction.Allow);
    public static RouteGuardResult Cancel => new(RouteGuardAction.Cancel);
    public static RouteGuardResult Redirect(string location) => new(RouteGuardAction.Redirect, location);
}

public sealed class Router : IDisposable
{
    private readonly List<RouteDefinition> _routes = [];
    private readonly List<Func<RouteLocation, RouteLocation?, RouteGuardResult>> _guards = [];
    private bool _started;
    private bool _processingHistory;
    private bool _disposed;

    internal Router(AppWindow window, string initialPath)
    {
        Window = window;
        History = new MemoryNavigationHistory(initialPath);
        History.Changed += OnHistoryChanged;
        window.Closed += Dispose;
    }

    public AppWindow Window { get; }
    public INavigationHistory History { get; set; }
    public RouteLocation? Current { get; private set; }
    public IReadOnlyList<RouteDefinition> Routes => _routes;
    public event Action<RouteLocation>? Navigated;
    internal event Action<RouteLocation, RouteLocation?>? RouteChanged;

    internal void Configure(Action<RouteCollectionBuilder> configure)
    {
        if (_started) throw new InvalidOperationException("Routes must be configured before navigation starts.");
        configure(new RouteCollectionBuilder(_routes));
    }

    public IDisposable BeforeEach(Func<RouteLocation, RouteLocation?, RouteGuardResult> guard)
    {
        ArgumentNullException.ThrowIfNull(guard);
        _guards.Add(guard);
        return new GuardRegistration(_guards, guard);
    }

    public bool Navigate(string location, bool replace = false) => NavigateCore(location, replace, 0);
    public bool Replace(string location) => Navigate(location, replace: true);
    public bool Back() { EnsureStarted(); return History.Back(); }
    public bool Forward() { EnsureStarted(); return History.Forward(); }

    internal void Start()
    {
        if (_started) return;
        _started = true;
        Activate(History.Current, Current, runGuards: true, redirectDepth: 0);
    }

    internal RouteMatch? Match(string location) => RouteMatcher.Match(_routes, location);

    private bool NavigateCore(string location, bool replace, int redirectDepth)
    {
        EnsureStarted();
        if (!TryResolve(location, Current, redirectDepth, out var target, out var redirect)) return false;
        if (redirect != null) return NavigateCore(redirect, replace: true, redirectDepth + 1);
        if (target == null) return false;
        if (replace) History.Replace(target.FullPath); else History.Push(target.FullPath);
        return true;
    }

    private void OnHistoryChanged(string location)
    {
        if (_processingHistory) return;
        var previous = Current;
        if (!TryResolve(location, previous, 0, out var target, out var redirect) || target == null)
        {
            if (previous != null && !string.Equals(History.Current, previous.FullPath, StringComparison.Ordinal))
            {
                _processingHistory = true;
                History.Replace(previous.FullPath);
                _processingHistory = false;
            }
            return;
        }
        if (redirect != null)
        {
            _processingHistory = true;
            History.Replace(redirect);
            _processingHistory = false;
            Activate(redirect, previous, runGuards: true, redirectDepth: 1);
            return;
        }
        Commit(target, previous);
    }

    private void Activate(string location, RouteLocation? from, bool runGuards, int redirectDepth)
    {
        if (!TryResolve(location, from, redirectDepth, out var target, out var redirect) || target == null) return;
        if (redirect != null)
        {
            if (redirectDepth >= 16) throw new InvalidOperationException("Router redirect limit exceeded.");
            _processingHistory = true;
            History.Replace(redirect);
            _processingHistory = false;
            Activate(redirect, from, runGuards, redirectDepth + 1);
            return;
        }
        Commit(target, from);
    }

    private bool TryResolve(
        string location,
        RouteLocation? from,
        int redirectDepth,
        out RouteLocation? target,
        out string? redirect)
    {
        if (redirectDepth >= 16) throw new InvalidOperationException("Router redirect limit exceeded.");
        var match = Match(location);
        if (match == null) { target = null; redirect = null; return false; }
        var parsed = RouteLocation.Parse(location);
        target = new RouteLocation(location, parsed.Path, match.Parameters, parsed.Query, match.Branch);
        foreach (var guard in _guards.ToArray())
        {
            var result = guard(target, from);
            if (result.Action == RouteGuardAction.Cancel) { target = null; redirect = null; return false; }
            if (result.Action == RouteGuardAction.Redirect)
            {
                redirect = result.Location ?? "/";
                return true;
            }
        }
        redirect = null;
        return true;
    }

    private void Commit(RouteLocation target, RouteLocation? previous)
    {
        Current = target;
        RouteChanged?.Invoke(target, previous);
        Navigated?.Invoke(target);
    }

    private void EnsureStarted() { if (!_started) Start(); }

    public void ClearCache() => RouterRegistry.ForEachView(Window, static view => view.ClearCache());
    public void RemoveCache(string matchedPath) => RouterRegistry.ForEachView(Window, view => view.RemoveCache(matchedPath));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        History.Changed -= OnHistoryChanged;
        Window.Closed -= Dispose;
        RouterRegistry.ForEachView(Window, static view => view.Shutdown());
        _guards.Clear();
        RouteChanged = null;
    }

    private sealed class GuardRegistration(
        List<Func<RouteLocation, RouteLocation?, RouteGuardResult>> guards,
        Func<RouteLocation, RouteLocation?, RouteGuardResult> guard) : IDisposable
    {
        public void Dispose() => guards.Remove(guard);
    }
}

internal static class RouterRegistry
{
    private static readonly ConditionalWeakTable<AppWindow, Router> Routers = new();
    private static readonly ConditionalWeakTable<AppWindow, List<WeakReference<RouterView>>> Views = new();

    public static Router Set(AppWindow window, Router router)
    {
        if (Routers.TryGetValue(window, out _)) throw new InvalidOperationException("The window already has a router.");
        Routers.Add(window, router);
        return router;
    }

    public static Router? Get(AppWindow window) => Routers.TryGetValue(window, out var router) ? router : null;

    public static void RegisterView(AppWindow window, RouterView view)
    {
        var views = Views.GetOrCreateValue(window);
        views.Add(new WeakReference<RouterView>(view));
    }

    public static void ForEachView(AppWindow window, Action<RouterView> action)
    {
        if (!Views.TryGetValue(window, out var views)) return;
        for (var i = views.Count - 1; i >= 0; i--)
        {
            if (views[i].TryGetTarget(out var view)) action(view);
            else views.RemoveAt(i);
        }
    }
}

public static class RouterWindowExtensions
{
    public static Router UseRouter(
        this AppWindow window,
        Action<RouteCollectionBuilder> configure,
        string initialPath = "/")
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(configure);
        var router = RouterRegistry.Set(window, new Router(window, initialPath));
        router.Configure(configure);
        return router;
    }
}
