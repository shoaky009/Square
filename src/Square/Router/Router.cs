using Square.Controls;
using Square.UI;

namespace Square.Router;

/// <summary>路由控件，根据当前路径激活匹配的组件。</summary>
public sealed class Router : View
{
    private bool _started;

    /// <summary>初始路径。</summary>
    public string InitialPath { get; set; } = "/";
    /// <summary>路由定义列表。</summary>
    public List<RouteDefinition> Routes { get; } = [];
    /// <summary>导航历史。</summary>
    public INavigationHistory? History { get; set; }
    /// <summary>当前路由上下文。</summary>
    public RouteContext? Current { get; private set; }
    /// <summary>导航完成事件。</summary>
    public event Action<RouteContext>? Navigated;

    /// <summary>启动路由。</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        History ??= new MemoryNavigationHistory(InitialPath);
        History.Changed += Activate;
        Activate(History.Current);
    }

    /// <summary>导航到指定路径。</summary>
    /// <returns>匹配成功返回 true。</returns>
    public bool Navigate(string location, bool replace = false)
    {
        EnsureStarted();
        if (RouteMatcher.Match(Routes, location) == null) return false;
        if (replace) History!.Replace(location);
        else History!.Push(location);
        return true;
    }

    /// <summary>替换当前路径。</summary>
    public bool Replace(string location) => Navigate(location, replace: true);

    /// <summary>后退。</summary>
    public bool Back()
    {
        EnsureStarted();
        return History!.Back();
    }

    /// <summary>前进。</summary>
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
