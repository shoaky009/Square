using Square.Controls;
using Square.Extensions.Routing;
using Square.Hosting;
using Square.Runtime;
using Square.UI;
using Xunit;

namespace Square.UI.Tests;

public class RoutingTests
{
    [Fact]
    public void KeepAliveCachesParameterizedPagesByMatchedPathAndReusesQueryChanges()
    {
        var window = new AppWindow("KeepAlive");
        var pages = new List<RouteAwarePage>();
        var router = window.UseRouter(routes =>
        {
            routes.Map("users/:id", () =>
            {
                var page = new RouteAwarePage();
                pages.Add(page);
                return page;
            }, route => route.KeepAlive = true);
        }, "/users/1?tab=a");
        var view = new RouterView();
        window.Load(view);
        ((IComponentLifecycle)view).OnAttached();

        var first = Assert.Single(pages);
        Assert.True(router.Navigate("/users/1?tab=b"));
        Assert.Single(pages);
        Assert.Same(first, Assert.Single(view.Children));
        Assert.Equal(1, first.UpdateCount);

        Assert.True(router.Navigate("/users/2"));
        var second = Assert.IsType<RouteAwarePage>(Assert.Single(view.Children));
        Assert.NotSame(first, second);
        Assert.Equal(2, pages.Count);

        Assert.True(router.Navigate("/users/1"));
        Assert.Same(first, Assert.Single(view.Children));
        Assert.Equal(2, pages.Count);
        Assert.True(first.ActivationCount >= 2);
        Assert.True(first.DeactivationCount >= 1);
    }

    [Fact]
    public void NestedRouterViewsRenderMatchingDepths()
    {
        var window = new AppWindow("Nested");
        var router = window.UseRouter(routes =>
        {
            routes.Map("users", static () => new RouteLayout(), route =>
                route.Map(":id", static () => new RouteLeaf()));
        }, "/users/42");
        var rootView = new RouterView();
        window.Load(rootView);
        ((IComponentLifecycle)rootView).OnAttached();

        var layout = Assert.IsType<RouteLayout>(Assert.Single(rootView.Children));
        var nested = Assert.Single(layout.QueryAll<RouterView>());
        Assert.IsType<RouteLeaf>(Assert.Single(nested.Children));
        Assert.Equal("42", router.Current?.Parameters["id"]);
    }

    [Fact]
    public void RouterViewCanConfigureWindowRouterThroughRefStyleApi()
    {
        var window = new AppWindow("Ref configuration");
        var view = new RouterView();
        window.Load(view);
        view.ConfigureOnce(routes => routes.Map("/", static () => new RouteLeaf()));
        ((IComponentLifecycle)view).OnAttached();

        Assert.IsType<RouteLeaf>(Assert.Single(view.Children));
        Assert.Equal("/", view.Current?.Path);
    }

    private sealed class RouteAwarePage : View, IRouteAware
    {
        public int ActivationCount { get; private set; }
        public int DeactivationCount { get; private set; }
        public int UpdateCount { get; private set; }
        public void OnRouteActivated(RouteLocation route) => ActivationCount++;
        public void OnRouteDeactivated(RouteLocation route) => DeactivationCount++;
        public void OnRouteUpdated(RouteLocation to, RouteLocation from) => UpdateCount++;
    }

    private sealed class RouteLayout : View
    {
        public override void BuildElementTree()
        {
            if (Children.Count == 0) Children.Add(new RouterView());
        }
    }

    private sealed class RouteLeaf : View;
}
