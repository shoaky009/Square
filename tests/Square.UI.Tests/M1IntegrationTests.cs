using Square.Controls.Controls;
using Square.Controls.Primitives;
using Square.Events;
using Square.Graphics;
using Square.Rendering;
using Square.Runtime;
using Square.Runtime.Binding;
using Square.Router;
using Square.Sample;
using Square.UI;
using Xunit;
using RouterControl = Square.Router.Router;

namespace Square.UI.Tests;

public class M1IntegrationTests
{
    [Fact]
    public void GeneratedComponentBuildsNestedTreeAndAppliesStyles()
    {
        var component = new Main();

        component.BuildElementTree();

        var root = Assert.IsType<View>(Assert.Single(component.Children));
        var tabs = Assert.Single(root.QueryAll<Tabs>());
        var controlsPage = Assert.Single(root.QueryAll<ControlsSamplesPage>());
        var textPage = Assert.Single(root.QueryAll<TextSamplesPage>());
        var button = Assert.Single(controlsPage.QueryAll<Button>());
        var inputs = root.QueryAll<Input>();
        var input = inputs[0];
        Assert.Equal("Button - add activity", button.TextContent);
        Assert.Equal("flex", root.Style.Get("display"));
        Assert.Contains("16", root.Style.Get("padding"));
        Assert.Equal(2, root.Children.Count);
        Assert.Equal(6, tabs.QueryAll<Button>().Count(control => control.ClassList.Contains("tab-button")));
        Assert.Equal(3, inputs.Count);
        Assert.Equal(2, root.QueryAll<TextArea>().Count);
        Assert.Equal("14px", inputs[1].Style.Get("line-height"));
        Assert.Equal("#067647", inputs[2].Style.Get("color"));
        Assert.Equal("20px", inputs[2].Style.Get("font-size"));
        Assert.Equal("Default line-height - editable text", input.Value);
        Assert.StartsWith("TextArea - 22px line-height", root.QueryAll<TextArea>()[0].Value);
        Assert.Single(root.QueryAll<CheckBox>());
        Assert.Equal(2, root.QueryAll<Radio>().Count);
        var select = Assert.Single(root.QueryAll<Select>());
        Assert.Single(root.QueryAll<Square.Controls.Controls.Image>());
        Assert.Single(root.QueryAll<Canvas>());
        var router = Assert.Single(root.QueryAll<RouterControl>());
        Assert.Single(root.QueryAll<OverflowSamplesPage>());
        Assert.Equal("/", router.Current?.Path);
        Assert.Single(router.QueryAll<RouteHomePage>());
        Assert.Equal(["Blue", "Green", "Orange"], select.Options);

        textPage.Name.Value = "Square";
        Assert.Equal("Square", input.Value);
    }

    [Fact]
    public void GeneratedEventsUpdateShowForAndInputBinding()
    {
        var component = new Main();
        component.BuildElementTree();
        var root = Assert.IsType<View>(Assert.Single(component.Children));
        var controlsPage = Assert.Single(root.QueryAll<ControlsSamplesPage>());
        var textPage = Assert.Single(root.QueryAll<TextSamplesPage>());
        var button = Assert.Single(controlsPage.QueryAll<Button>());
        var input = Assert.Single(textPage.QueryAll<Input>(), editor => editor.ClassList.Contains("editor-default"));

        input.SelectAll();
        input.HandleKey('A');
        Assert.Equal("A", textPage.Name.Value);

        button.DispatchEvent(StandardEvents.CreateClick());
        Reconciler.Current.Flush();
        Assert.True(controlsPage.LastEventSourceWasButton.Value);
        Assert.True(controlsPage.ShowCount.Value);
        Assert.Equal(2, controlsPage.Items.Count);
        Assert.Contains(root.QueryAll<Square.Controls.Controls.Text>(), text => text.TextContent == "Show: button clicked");
        Assert.Contains(root.QueryAll<Square.Controls.Controls.Text>(), text => text.TextContent == "Click 1");
    }

    [Fact]
    public void GeneratedControlsPageScrollsWhenActivityItemsExceedPanelHeight()
    {
        var component = new Main();
        component.BuildElementTree();
        ((IComponentLifecycle)component).OnAttached();
        var root = Assert.IsType<View>(Assert.Single(component.Children));
        var tabs = Assert.Single(root.QueryAll<Tabs>());
        tabs.SelectedIndex = 1;
        var controlsPage = Assert.Single(root.QueryAll<ControlsSamplesPage>());
        var button = Assert.Single(controlsPage.QueryAll<Button>());
        var tabPanels = Assert.Single(root.QueryAll<View>(), view => view.ClassList.Contains("tab-panels"));

        for (var i = 0; i < 60; i++)
            button.DispatchEvent(StandardEvents.CreateClick());
        Reconciler.Current.Flush();

        var layout = new LayoutEngine();
        layout.Measure(root, new Size(900, 900));
        layout.Arrange(root, new Rect(0, 0, 900, 900));

        Assert.True(tabPanels.ScrollContentSize.Height > tabPanels.Geometry.Height);
        Assert.True(tabPanels.ScrollBy(0, 120));
        Assert.True(controlsPage.QueryAll<Button>().All(item => item.Geometry.Height >= 36));
        ((IComponentLifecycle)component).OnDetached();
    }

    [Fact]
    public void TextInputsAcceptChineseAndJapaneseText()
    {
        var input = new Input();
        var textArea = new TextArea();

        input.HandleTextInput("中文");
        input.HandleTextInput("日本語");
        textArea.HandleTextInput("中文\n日本語");

        Assert.Equal("中文日本語", input.Value);
        Assert.Equal("中文\n日本語", textArea.Value);
    }

    [Fact]
    public void TextInputDoesNotTreatKeypadVirtualKeysAsCharacters()
    {
        var input = new Input();

        input.HandleKey(0x6A); // VK_MULTIPLY was previously inserted as 'j'.
        input.HandleKey(0x6B); // VK_ADD was previously inserted as 'k'.
        input.HandleKey(0x6D); // VK_SUBTRACT was previously inserted as 'm'.
        input.HandleKey(0x6E); // VK_DECIMAL was previously inserted as 'n'.
        input.HandleKey(0x6F); // VK_DIVIDE was previously inserted as 'o'.

        Assert.Equal("", input.Value);
    }

    [Fact]
    public void TextEditorSupportsKeyboardSelectionAndReplacement()
    {
        var input = new Input { Value = "A中B", Geometry = new Rect(0, 0, 200, 36) };
        input.Focus();

        input.HandleKey(36, control: true);
        input.HandleKey(39);
        input.HandleKey(39, shift: true);

        Assert.Equal(1, input.SelectionStart);
        Assert.Equal(1, input.SelectionLength);
        Assert.Equal("中", input.SelectedText);

        input.HandleTextInput("日");
        Assert.Equal("A日B", input.Value);
        Assert.Equal(2, input.CaretIndex);

        input.HandleKey(65, control: true);
        Assert.Equal("A日B", input.SelectedText);
        Assert.True(input.DeleteSelection());
        Assert.Equal("", input.Value);
    }

    [Fact]
    public void TextInputCutDeletesSelectionAndPasswordDisablesCopyCut()
    {
        var input = new Input { Value = "secret" };
        input.SelectAll();

        Assert.True(input.CanCopySelection);
        Assert.True(input.CanCutSelection);
        input.HandleKey(88, control: true);
        Assert.Equal("", input.Value);

        var password = new Input { Type = "password", Value = "secret" };
        password.SelectAll();

        Assert.False(password.CanCopySelection);
        Assert.False(password.CanCutSelection);
        Assert.Equal("secret", password.SelectedText);
    }

    [Fact]
    public void UserSelectTextEnablesSelectableTextAndInheritsToChildren()
    {
        var parent = new View();
        var child = new Square.Controls.Controls.Text("copy me") { Geometry = new Rect(0, 0, 200, 24) };
        parent.Children.Add(child);

        Assert.False(child.IsUserSelectText());

        parent.Style.Set("user-select", "text");
        Assert.True(child.IsUserSelectText());
        Assert.Equal("copy me", Assert.IsAssignableFrom<ITextSelectable>(child).SelectableText);

        child.Style.Set("user-select", "none");
        Assert.False(child.IsUserSelectText());
    }

    [Fact]
    public void PointerHitTestingAndMultilineCaretsUseSharedMetrics()
    {
        var input = new Input { Value = "你好", Geometry = new Rect(10, 10, 200, 36) };
        var textArea = new TextArea { Value = "你好\n还是", Geometry = new Rect(10, 60, 200, 76) };
        input.Focus();
        textArea.Focus();

        input.HandlePointerDown(new Point(47, 20));
        input.HandlePointerUp(new Point(47, 20));
        textArea.HandleKey(36, control: true);
        textArea.HandleKey(35);

        Assert.Equal(2, input.CaretIndex);
        Assert.Equal(2, textArea.CaretIndex);
        Assert.Equal(input.CaretRect.X, textArea.CaretRect.X);
        Assert.Equal(input.CaretRect.Height, textArea.CaretRect.Height);
        Assert.Equal(12, input.CaretRect.Y - input.Geometry.Y);
        Assert.Equal(10, textArea.CaretRect.Y - textArea.Geometry.Y);
        Assert.Equal(13, input.CaretRect.Height);

        textArea.HandleKey(40);
        Assert.Equal(5, textArea.CaretIndex);
        Assert.Equal(27, textArea.CaretRect.Y - textArea.Geometry.Y);
    }

    [Fact]
    public void TextEditorsUseCssLineHeightColorAndChromeLikeSelectionDefaults()
    {
        var input = new Input { Geometry = new Rect(0, 0, 220, 44), Value = "Square" };
        input.Style.Set("font-size", "14px");
        input.Style.Set("line-height", "28px");
        input.Style.Set("color", "#067647");
        input.Focus();

        Assert.Equal(Color.FromRgb(51, 144, 255), input.SelectionBackground);
        Assert.Equal(Color.White, input.SelectionForeground);
        Assert.Equal(10, input.CaretRect.Y - input.Geometry.Y);
        Assert.Equal(24, input.CaretRect.Height);

        input.Style.Set("line-height", "2");
        Assert.Equal(24, input.CaretRect.Height);
    }

    [Fact]
    public void TextSelectionCollapsesWhenEditorLosesFocus()
    {
        var input = new Input { Value = "selected" };
        input.Focus();
        input.SelectAll();

        input.Unfocus();

        Assert.Equal(0, input.SelectionLength);
        Assert.Equal(input.CaretIndex, input.SelectionStart);
    }

    [Fact]
    public void GeneratedSampleControlsUpdateBoundState()
    {
        var component = new Main();
        component.BuildElementTree();
        SampleSignals.Initialize(new Dispatcher());
        ((IComponentLifecycle)component).OnAttached();

        var textPage = Assert.Single(component.QueryAll<TextSamplesPage>());
        var controlsPage = Assert.Single(component.QueryAll<ControlsSamplesPage>());
        var textArea = textPage.QueryAll<TextArea>()[0];
        var checkBox = Assert.Single(component.QueryAll<CheckBox>());
        var radios = component.QueryAll<Radio>();
        var select = Assert.Single(component.QueryAll<Select>());
        var image = Assert.Single(component.QueryAll<Square.Controls.Controls.Image>());
        var canvas = Assert.Single(component.QueryAll<Canvas>());

        textArea.SelectAll();
        textArea.HandleKey('N');
        textArea.HandleKey(13);
        textArea.HandleKey('X');
        checkBox.DispatchEvent(StandardEvents.CreateClick());
        radios[1].DispatchEvent(StandardEvents.CreateClick());
        select.Geometry = new Rect(10, 10, 200, 36);
        select.HandlePointerDown(new Point(20, 20));
        select.HandlePointerDown(new Point(20, 81));

        Assert.Equal("N\nX", textPage.Notes.Value);
        Assert.True(controlsPage.Accepted.Value);
        Assert.False(controlsPage.OptionA.Value);
        Assert.True(controlsPage.OptionB.Value);
        Assert.Equal("Green", controlsPage.SelectedValue.Value);
        Assert.NotNull(image.ImageContent);
        Assert.Null(canvas.DrawContent);
        ((IComponentLifecycle)component).OnDetached();
    }

    [Fact]
    public void GeneratedComponentsProjectDefaultNamedAndFallbackSlotsWithoutWrapperViews()
    {
        var card = new SlotCard();
        card.Slots.Set("header", parent => parent.Children.Add(new Square.Controls.Controls.Text("Named header")));
        card.Slots.Set("", parent =>
        {
            parent.Children.Add(new Square.Controls.Controls.Text("First body"));
            parent.Children.Add(new Square.Controls.Controls.Text("Second body"));
        });

        card.BuildElementTree();

        var root = Assert.IsType<View>(Assert.Single(card.Children));
        Assert.Equal(2, root.Children.Count);
        var header = Assert.IsType<View>(root.Children[0]);
        var content = Assert.IsType<View>(root.Children[1]);
        Assert.Equal("Named header", Assert.IsType<Square.Controls.Controls.Text>(Assert.Single(header.Children)).TextContent);
        Assert.Equal(2, content.Children.Count);
        Assert.All(content.Children, child => Assert.IsType<Square.Controls.Controls.Text>(child));

        var fallbackCard = new SlotCard();
        fallbackCard.BuildElementTree();
        Assert.Contains(fallbackCard.QueryAll<Square.Controls.Controls.Text>(), text => text.TextContent == "Fallback header");
        Assert.Contains(fallbackCard.QueryAll<Square.Controls.Controls.Text>(), text => text.TextContent == "Fallback content");
    }

    [Fact]
    public void GeneratedNestedRouterNavigatesWithParamsQueryLinksAndHistory()
    {
        var component = new Main();
        component.BuildElementTree();
        ((IComponentLifecycle)component).OnAttached();
        var router = Assert.Single(component.QueryAll<RouterControl>());

        var layout = new LayoutEngine();
        layout.Measure(component, new Size(900, 980));
        layout.Arrange(component, new Rect(0, 0, 900, 980));
        var routeLinks = router.QueryAll<Square.Router.Link>();
        Assert.Equal(2, routeLinks.Count);
        Assert.Same(routeLinks[0].Parent, routeLinks[1].Parent);
        Assert.Equal("flex", routeLinks[0].Parent?.Style.Get("display"));
        Assert.Equal("row", routeLinks[0].Parent?.Style.Get("flex-direction"));
        Assert.Equal(routeLinks[0].Geometry.Y, routeLinks[1].Geometry.Y);
        var visibleShell = new RouteShell();
        visibleShell.BuildElementTree();
        ((IComponentLifecycle)visibleShell).OnAttached();
        layout.Measure(visibleShell, new Size(600, 180));
        layout.Arrange(visibleShell, new Rect(0, 0, 600, 180));
        var visibleLinks = visibleShell.QueryAll<Square.Router.Link>();
        Assert.True(visibleLinks[1].Geometry.X >= visibleLinks[0].Geometry.Right + 6f,
            $"first={visibleLinks[0].Geometry}, second={visibleLinks[1].Geometry}");

        var userLink = routeLinks.Single(link => link.To.Contains("users"));
        userLink.DispatchEvent(StandardEvents.CreateClick());

        Assert.Equal("/users/42", router.Current?.Path);
        Assert.Equal("42", router.Current?.Parameters["id"]);
        Assert.Equal("profile", router.Current?.Query["tab"]);
        var userPage = Assert.Single(router.QueryAll<RouteUserPage>());
        Assert.Same(router.Current, RouteContext.Find(userPage));
        Assert.Contains(userPage.QueryAll<Square.Controls.Controls.Text>(),
            text => text.TextContent == "Current route: /users/42  tab=profile");

        Assert.True(router.Back());
        Assert.Equal("/", router.Current?.Path);
        Assert.Single(router.QueryAll<RouteHomePage>());
        Assert.True(router.Forward());
        Assert.Single(router.QueryAll<RouteUserPage>());
    }

    [Fact]
    public void RouteMatcherPrioritizesStaticThenParameterThenWildcard()
    {
        var staticRoute = new RouteDefinition("users/settings");
        var parameterRoute = new RouteDefinition("users/:id");
        var wildcardRoute = new RouteDefinition("*");
        var routes = new[] { wildcardRoute, parameterRoute, staticRoute };

        Assert.Same(staticRoute, RouteMatcher.Match(routes, "/users/settings")?.Branch[^1]);
        var parameterMatch = RouteMatcher.Match(routes, "/users/42");
        Assert.Same(parameterRoute, parameterMatch?.Branch[^1]);
        Assert.Equal("42", parameterMatch?.Parameters["id"]);
        Assert.Same(wildcardRoute, RouteMatcher.Match(routes, "/other/path")?.Branch[^1]);
    }

    [Fact]
    public void RouterSwapsAttachedPagesUsingVisualLifecycle()
    {
        var attached = new List<string>();
        var detached = new List<string>();
        var router = new RouterControl();
        router.Routes.Add(new RouteDefinition("/", () => new TrackingPage("home", attached, detached)));
        router.Routes.Add(new RouteDefinition("other", () => new TrackingPage("other", attached, detached)));
        router.Start();
        ((IComponentLifecycle)router).OnAttached();

        Assert.Equal(["home"], attached);
        Assert.True(router.Navigate("/other"));
        Assert.Equal(["home", "other"], attached);
        Assert.Equal(["home"], detached);
    }

    [Fact]
    public void HitTestAndDispatchEventReachButton()
    {
        var root = new View { Geometry = new Rect(0, 0, 200, 100) };
        var button = new Button { Geometry = new Rect(20, 20, 80, 40) };
        root.Children.Add(button);
        var clicks = 0;
        button.AddEventListener("click", () => clicks++);

        var hit = root.HitTest(new Point(30, 30));
        hit?.DispatchEvent(StandardEvents.CreateClick());

        Assert.Same(button, hit);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void OverflowVisibleAllowsHitTestingChildrenOutsideParentBoundsAndHiddenClipsThem()
    {
        var root = new View { Geometry = new Rect(0, 0, 100, 100) };
        var parent = new View { Geometry = new Rect(0, 0, 10, 10) };
        var child = new Button { Geometry = new Rect(12, 0, 20, 10) };
        root.Children.Add(parent);
        parent.Children.Add(child);

        Assert.Same(child, root.HitTest(new Point(13, 5)));

        parent.Style.Set("overflow", "hidden");

        Assert.Same(root, root.HitTest(new Point(13, 5)));
    }

    [Fact]
    public void OverflowAxisClipsHitTestingOnlyOnSpecifiedAxis()
    {
        var root = new View { Geometry = new Rect(0, 0, 100, 100) };
        var parent = new View { Geometry = new Rect(0, 0, 10, 10) };
        var verticalOverflow = new Button { Geometry = new Rect(0, 12, 10, 10) };
        var horizontalOverflow = new Button { Geometry = new Rect(12, 0, 10, 10) };
        root.Children.Add(parent);
        parent.Children.Add(verticalOverflow);
        parent.Children.Add(horizontalOverflow);

        parent.Style.Set("overflow-x", "hidden");

        Assert.Same(verticalOverflow, root.HitTest(new Point(5, 13)));
        Assert.Same(root, root.HitTest(new Point(13, 5)));
    }

    [Fact]
    public void WheelDefaultActionScrollsNearestOverflowContainer()
    {
        var scroller = new View { Geometry = new Rect(0, 0, 100, 40) };
        scroller.Style.Set("overflow-y", "auto");
        scroller.SetScrollContentSize(new Size(100, 140));
        var child = new Button { Geometry = new Rect(0, 80, 100, 20) };
        scroller.Children.Add(child);

        child.DispatchTrusted(StandardEvents.CreateWheel(0, 30));

        Assert.Equal(30, scroller.ScrollTop);
    }

    [Fact]
    public void WheelPreventDefaultSkipsOverflowScrolling()
    {
        var scroller = new View { Geometry = new Rect(0, 0, 100, 40) };
        scroller.Style.Set("overflow-y", "auto");
        scroller.SetScrollContentSize(new Size(100, 140));
        var child = new Button { Geometry = new Rect(0, 80, 100, 20) };
        child.AddEventListener(StandardEvents.Wheel, e => e.PreventDefault());
        scroller.Children.Add(child);

        child.DispatchTrusted(StandardEvents.CreateWheel(0, 30));

        Assert.Equal(0, scroller.ScrollTop);
    }

    [Fact]
    public void EventCapturesThenBubblesLikeDom()
    {
        var root = new View();
        var panel = new View();
        var button = new Button();
        root.Children.Add(panel);
        panel.Children.Add(button);
        var calls = new List<string>();

        root.AddEventListener(StandardEvents.PointerDown, e => calls.Add($"root:{e.EventPhase}"), useCapture: true);
        root.AddEventListener(StandardEvents.PointerDown, e => calls.Add($"root:{e.EventPhase}"));
        panel.AddEventListener(StandardEvents.PointerDown, e => calls.Add($"panel:{e.EventPhase}"), useCapture: true);
        panel.AddEventListener(StandardEvents.PointerDown, e => calls.Add($"panel:{e.EventPhase}"));
        button.AddEventListener(StandardEvents.PointerDown, e => calls.Add($"button:{e.EventPhase}"));

        button.DispatchEvent(StandardEvents.CreatePointerDown());

        Assert.Equal([
            $"root:{EventPhase.CapturingPhase}",
            $"panel:{EventPhase.CapturingPhase}",
            $"button:{EventPhase.AtTarget}",
            $"panel:{EventPhase.BubblingPhase}",
            $"root:{EventPhase.BubblingPhase}"
        ], calls);
    }

    [Fact]
    public void StopPropagationPreventsParentHandlers()
    {
        var root = new View();
        var button = new Button();
        root.Children.Add(button);
        var rootCalls = 0;
        var buttonCalls = 0;
        root.AddEventListener(StandardEvents.Click, _ => rootCalls++);
        button.AddEventListener(StandardEvents.Click, e =>
        {
            buttonCalls++;
            e.StopPropagation();
        });

        button.DispatchEvent(StandardEvents.CreateClick());

        Assert.Equal(1, buttonCalls);
        Assert.Equal(0, rootCalls);
    }

    [Fact]
    public void StringEventApiBubblesWithDefaultClickInit()
    {
        var root = new View();
        var button = new Button();
        root.Children.Add(button);
        var calls = 0;
        root.AddEventListener("click", () => calls++);

        button.DispatchEvent(StandardEvents.CreateClick());

        Assert.Equal(1, calls);
    }

    [Fact]
    public void CustomStringEventsBubbleWhenConfigured()
    {
        var root = new View();
        var button = new Button();
        root.Children.Add(button);
        var calls = 0;
        root.AddEventListener("saved", () => calls++);

        button.DispatchEvent(new Event("saved", new EventInit { Bubbles = true }));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void StopPropagationDoesNotBlockEarlierCaptureOnAncestors()
    {
        var root = new View();
        var panel = new View();
        var button = new Button();
        root.Children.Add(panel);
        panel.Children.Add(button);
        var calls = new List<string>();
        root.AddEventListener(StandardEvents.Click, _ => calls.Add("root-capture"), useCapture: true);
        panel.AddEventListener(StandardEvents.Click, e =>
        {
            calls.Add("panel");
            e.StopPropagation();
        });
        root.AddEventListener(StandardEvents.Click, _ => calls.Add("root-bubble"));

        button.DispatchEvent(StandardEvents.CreateClick());

        Assert.Equal(["root-capture", "panel"], calls);
    }

    [Fact]
    public void DuplicateActionHandlersAreDedupedByDomRules()
    {
        var button = new Button();
        var calls = 0;
        Action handler = () => calls++;
        // DOM: same function + same capture is not added twice
        button.AddEventListener("click", handler);
        button.AddEventListener("click", handler);

        button.DispatchEvent(StandardEvents.CreateClick());
        button.RemoveEventListener("click", handler);
        button.DispatchEvent(StandardEvents.CreateClick());

        Assert.Equal(1, calls);
    }

    [Fact]
    public void ActionAndEventHandlersCanBeRemovedSymmetrically()
    {
        var button = new Button();
        var calls = 0;
        Action noArg = () => calls++;
        Action<Event> oneArg = _ => calls++;
        button.AddEventListener("click", noArg);
        button.AddEventListener("click", oneArg);

        button.RemoveEventListener("click", noArg);
        button.RemoveEventListener("click", oneArg);
        button.DispatchEvent(StandardEvents.CreateClick());

        Assert.Equal(0, calls);
    }

    [Fact]
    public void CanvasRequestFrameBubblesToTheVisualRoot()
    {
        var root = new View();
        var canvas = new Canvas();
        root.Children.Add(canvas);
        var requests = 0;
        EventTarget? source = null;
        root.AddEventListener(StandardEvents.RequestFrame, e =>
        {
            requests++;
            source = e.Target;
        });

        canvas.RequestFrame();

        Assert.Equal(1, requests);
        Assert.Same(canvas, source);
    }

    [Fact]
    public void CanvasRequestAnimationFrameCarriesCallbackAndFrameRate()
    {
        var root = new View();
        var canvas = new Canvas();
        root.Children.Add(canvas);
        FrameRequestEvent? request = null;
        root.AddEventListener(StandardEvents.RequestFrame, e => request = e as FrameRequestEvent);
        Action<IRenderContext, Rect> draw = (_, _) => { };

        canvas.RequestAnimationFrame(draw, fps: 5);

        Assert.Null(canvas.DrawContent);
        Assert.NotNull(request);
        Assert.Equal(5, request!.FramesPerSecond);
        Assert.Same(canvas, request.Target);
    }

    [Fact]
    public void SelectDoesNotCloseWhenPointerUpRaisesClickAfterOpeningOnPointerDown()
    {
        var select = new Select
        {
            Geometry = new Rect(10, 10, 200, 36),
            Options = ["Blue", "Green"]
        };

        select.HandlePointerDown(new Point(20, 20));
        select.DispatchEvent(StandardEvents.CreateClick());

        Assert.True(select.IsOpen);
    }

    [Fact]
    public void SelectOpensPopupAndChoosesClickedArrayOption()
    {
        var root = new View { Geometry = new Rect(0, 0, 300, 240) };
        var select = new Select
        {
            Geometry = new Rect(20, 20, 200, 36),
            Options = ["Blue", "Green", "Orange"],
            Value = "Blue"
        };
        root.Children.Add(select);
        var changes = 0;
        select.AddEventListener("change", () => changes++);

        select.HandlePointerDown(new Point(30, 30));
        var tree = new DisplayTree();
        tree.BuildFrom(root);

        Assert.True(select.IsOpen);
        Assert.Equal(1000, select.ZIndex);
        Assert.NotSame(select, root.HitTest(new Point(30, 91)));
        Assert.Same(select, tree.HitTestPopups(new Point(30, 91)));

        select.HandlePointerDown(new Point(30, 91));

        Assert.Equal("Green", select.Value);
        Assert.False(select.IsOpen);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void LayoutReflowsWhenViewportSizeChanges()
    {
        var component = new Main();
        component.BuildElementTree();
        var layout = new LayoutEngine();
        var tabs = Assert.Single(component.QueryAll<Tabs>());

        layout.Measure(component, new Size(400, 300));
        layout.Arrange(component, new Rect(0, 0, 400, 300));
        var initialWidth = tabs.Geometry.Width;

        layout.Measure(component, new Size(720, 480));
        layout.Arrange(component, new Rect(0, 0, 720, 480));

        Assert.Equal(720, component.Geometry.Width);
        Assert.True(tabs.Geometry.Width > initialWidth);
        Assert.Equal(688, tabs.Geometry.Width);
    }

    [Fact]
    public void TabsSelectionInvalidatesLayoutForRetainedRenderTreeRebuild()
    {
        var tabs = new Tabs();
        var firstButton = new Button("First");
        var secondButton = new Button("Second");
        var firstPage = new View();
        var secondPage = new View();
        tabs.Slots.Set("tabs", parent =>
        {
            parent.Children.Add(firstButton);
            parent.Children.Add(secondButton);
        });
        tabs.Slots.Set("", parent =>
        {
            parent.Children.Add(firstPage);
            parent.Children.Add(secondPage);
        });
        tabs.BuildElementTree();
        ((IComponentLifecycle)tabs).OnAttached();
        var layout = new LayoutEngine();
        layout.Measure(tabs, new Size(600, 500));
        layout.Arrange(tabs, new Rect(0, 0, 600, 500));

        Assert.False(tabs.IsLayoutDirty);

        secondButton.DispatchEvent(StandardEvents.CreateClick());

        Assert.True(tabs.IsLayoutDirty);
        Assert.False(firstPage.IsVisible);
        Assert.True(secondPage.IsVisible);
        ((IComponentLifecycle)tabs).OnDetached();
    }

    [Fact]
    public void TabsUseNamedAndDefaultSlotsAndPreservePageInstances()
    {
        var tabs = new Tabs();
        var firstButton = new Button("First");
        var secondButton = new Button("Second");
        var firstPage = new View();
        var secondPage = new View();
        tabs.Slots.Set("tabs", parent =>
        {
            parent.Children.Add(firstButton);
            parent.Children.Add(secondButton);
        });
        tabs.Slots.Set("", parent =>
        {
            parent.Children.Add(firstPage);
            parent.Children.Add(secondPage);
        });

        tabs.BuildElementTree();
        ((IComponentLifecycle)tabs).OnAttached();

        Assert.True(firstPage.IsVisible);
        Assert.False(secondPage.IsVisible);
        Assert.True(firstButton.ClassList.Contains("selected"));

        var layout = new LayoutEngine();
        layout.Measure(tabs, new Size(600, 500));
        layout.Arrange(tabs, new Rect(0, 0, 600, 500));
        Assert.True(secondPage.Geometry.IsEmpty);

        secondButton.DispatchEvent(StandardEvents.CreateClick());
        layout.Measure(tabs, new Size(600, 500));
        layout.Arrange(tabs, new Rect(0, 0, 600, 500));

        Assert.False(firstPage.IsVisible);
        Assert.True(secondPage.IsVisible);
        Assert.False(secondPage.Geometry.IsEmpty);
        Assert.InRange(secondPage.Geometry.Y, 0, 500);
        Assert.Same(secondPage, tabs.QueryAll<View>().Single(view => view == secondPage));
        Assert.Equal(1, tabs.SelectedIndex);
        Assert.Equal("#ffffff", secondButton.Style.Get("background"));
        ((IComponentLifecycle)tabs).OnDetached();
    }

    [Fact]
    public void SignalCrossesComponentsAndReturnsBackgroundPublishToUiDispatcher()
    {
        var dispatcher = new Dispatcher();
        SampleSignals.Initialize(dispatcher);
        SampleSignals.Activity.Publish("initial", force: true);
        var publisher = new SignalPublisher();
        var subscriber = new SignalSubscriber();
        publisher.BuildElementTree();
        subscriber.BuildElementTree();
        ((IComponentLifecycle)publisher).OnAttached();
        ((IComponentLifecycle)subscriber).OnAttached();

        Assert.Equal("initial", subscriber.Received.Value);
        Assert.Equal(Environment.CurrentManagedThreadId.ToString(), subscriber.DeliveryThread.Value.Split(' ')[^1]);

        var worker = new Thread(() => SampleSignals.Activity.Publish("from worker"));
        worker.Start();
        worker.Join();

        Assert.Equal("initial", subscriber.Received.Value);
        dispatcher.Run();
        Assert.Equal("from worker", subscriber.Received.Value);

        ((IComponentLifecycle)subscriber).OnDetached();
        ((IComponentLifecycle)publisher).OnDetached();
    }

    [Fact]
    public void GeneratedSampleLaysOutTheSelectedSignalsPageInsideTheViewport()
    {
        var dispatcher = new Dispatcher();
        SampleSignals.Initialize(dispatcher);
        var component = new Main();
        component.BuildElementTree();
        ((IComponentLifecycle)component).OnAttached();
        var signalsButton = Assert.Single(
            component.QueryAll<Button>(),
            button => button.ClassList.Contains("tab-button") && button.TextContent == "Signals");

        signalsButton.DispatchEvent(StandardEvents.CreateClick());
        var layout = new LayoutEngine();
        layout.Measure(component, new Size(900, 940));
        layout.Arrange(component, new Rect(0, 0, 900, 940));

        var page = Assert.Single(component.QueryAll<SignalsSamplesPage>());
        var subscriber = Assert.Single(component.QueryAll<SignalSubscriber>());
        Assert.True(page.IsVisible);
        Assert.False(subscriber.Geometry.IsEmpty);
        Assert.InRange(subscriber.Geometry.Bottom, 1, 940);
        ((IComponentLifecycle)component).OnDetached();
    }

    [Fact]
    public void ShowAndForReactToObservableSources()
    {
        var root = new View();
        var visible = new ObservableValue<bool>(false);
        var shown = new Square.Controls.Controls.Text("shown");
        var show = new ShowNode(visible, () => shown);
        show.AttachTo(root);

        var items = new ObservableCollection<string> { "a" };
        var nodes = new Dictionary<string, Square.Controls.Controls.Text>();
        var loop = ForNode.Create(items, item => nodes[item] = new Square.Controls.Controls.Text(item));
        loop.AttachTo(root);

        ((IComponentLifecycle)root).OnAttached();
        visible.Value = true;
        items.Add("b");
        Reconciler.Current.Flush();

        Assert.True(shown.IsAttached);
        Assert.Equal(new[] { "a", "b" }, root.QueryAll<Square.Controls.Controls.Text>().Where(text => text != shown).Select(text => text.TextContent));

        items.Move(1, 0);
        Reconciler.Current.Flush();
        Assert.Same(nodes["b"], root.Children[1]);

        visible.Value = false;
        Reconciler.Current.Flush();
        Assert.False(shown.IsAttached);
    }

    [Fact]
    public void ReconcilerFlushProcessesDirtyWorkScheduledByUpdate()
    {
        Reconciler.Current.Reset();
        var root = new View();
        var child = new View();
        root.Children.Add(child);
        root.ClearLayoutDirty();
        child.ClearLayoutDirty();

        Reconciler.Current.ScheduleUpdate(child.ScheduleReconcile);
        Reconciler.Current.Flush();

        Assert.True(child.IsLayoutDirty);
        Assert.True(root.IsLayoutDirty);
        Assert.False(Reconciler.Current.HasWork);
    }

    private sealed class TrackingPage(
        string name,
        List<string> attached,
        List<string> detached) : UIElement
    {
        protected override void OnAttachedCore() => attached.Add(name);
        protected override void OnDetachedCore() => detached.Add(name);
    }
}
