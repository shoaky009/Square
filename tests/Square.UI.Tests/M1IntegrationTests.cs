using System;
using System.Collections.Generic;
using Square.Controls;
using Square.Controls.Primitives;
using Square.Events;
using Square.Graphics;
using Square.Platform;
using Square.Rendering;
using Square.Runtime;
using Square.Runtime.Binding;
using Square.Resources;
using Square.Router;
using Square.Sample;
using Square.Sample.Components;
using Square.UI;
using Xunit;
using RouterControl = Square.Router.Router;

namespace Square.UI.Tests;

public class M1IntegrationTests
{
    [Fact]
    public void TextMeasuresWithinAvailableWidthAndWrapsHeight()
    {
        var text = new Square.Controls.Text("abcdefgh") { FontSize = 20 };

        var unconstrained = text.Measure(new Size(float.MaxValue, float.MaxValue));
        var constrained = text.Measure(new Size(20, 200));

        Assert.Equal(20, constrained.Width);
        Assert.True(constrained.Height > unconstrained.Height);
    }

    [Fact]
    public void GeneratedComponentBuildsNestedTreeAndAppliesStyles()
    {
        var component = new Main();

        component.BuildElementTree();

        var root = Assert.IsType<View>(Assert.Single(component.Children));
        var tabs = Assert.Single(root.QueryAll<Tabs>());
        var controlsPage = Assert.Single(root.QueryAll<ControlsSamplesPage>());
        var textPage = Assert.Single(root.QueryAll<TextSamplesPage>());
        var button = Assert.Single(controlsPage.QueryAll<Button>(), item => item.TextContent == "Button - add activity");
        var inputs = root.QueryAll<Input>();
        var input = inputs[0];
        Assert.Equal("Button - add activity", button.TextContent);
        Assert.Equal("flex", root.Style.Get("display"));
        Assert.Contains("16", root.Style.Get("padding"));
        Assert.Equal(4, root.Children.Count);
        Assert.IsType<MenuBar>(root.Children[0]);
        Assert.IsType<Tabs>(root.Children[3]);
        Assert.Equal(7, tabs.QueryAll<Button>().Count(control => control.ClassList.Contains("tab-button")));
        Assert.Equal(4, inputs.Count);
        Assert.Equal(2, root.QueryAll<TextArea>().Count);
        Assert.Equal("14px", inputs[1].Style.Get("line-height"));
        Assert.Equal("#067647", inputs[2].Style.Get("color"));
        Assert.Equal("20px", inputs[2].Style.Get("font-size"));
        Assert.Equal("Default line-height - editable text", input.Value);
        Assert.StartsWith("TextArea - 22px line-height", root.QueryAll<TextArea>()[0].Value);
        Assert.Single(root.QueryAll<CheckBox>());
        Assert.Equal(2, root.QueryAll<Radio>().Count);
        var select = Assert.Single(root.QueryAll<Select>());
        Assert.Single(root.QueryAll<Square.Controls.Image>());
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
    public void OverlaySampleIncludesPopupDialogAndContextMenu()
    {
        var page = new OverlaySamplesPage();

        page.BuildElementTree();

        Assert.Single(page.QueryAll<Popup>(), popup => popup.GetType() == typeof(Popup));
        Assert.Single(page.QueryAll<Dialog>());
        var contextMenu = Assert.Single(page.QueryAll<ContextMenu>());
        Assert.Equal(7, contextMenu.QueryAll<MenuItem>().Count);
        Assert.Single(contextMenu.QueryAll<MenuSeparator>());
    }

    [Fact]
    public void MainMenuBarFillsViewportAndKeepsConfiguredBackground()
    {
        var main = new Main();
        main.BuildElementTree();
        var layout = new LayoutEngine();

        layout.Measure(main, new Size(900, 980));
        layout.Arrange(main, new Rect(0, 0, 900, 980));

        var bar = Assert.Single(main.QueryAll<MenuBar>());
        Assert.Equal("#dbeafe", bar.Style.Get("background-color"));
        Assert.True(bar.Geometry.Width >= 800,
            $"bar={bar.Geometry}, parent={bar.Parent?.Geometry}, width={bar.Style.Get("width")}, align={bar.Style.Get("align-self")}");
        var items = bar.Children.OfType<MenuItem>().ToArray();
        Assert.All(items, item => Assert.InRange(item.Geometry.Width, 56, 80));
        Assert.Equal(items[0].Geometry.Right, items[1].Geometry.X);

        items[0].DispatchEvent(StandardEvents.CreateClick());
        var menu = Assert.IsType<Menu>(items[0].Submenu);
        Assert.True(menu.IsOpen);
        Assert.Equal(240, menu.PopupBounds.Width);
        Assert.Equal(105, menu.PopupBounds.Height);
        Assert.Equal(3, menu.Items.Count);
        Assert.Single(menu.Children.OfType<MenuSeparator>());
        Assert.All(menu.Items, item => Assert.True(item.Geometry.Height > 0));

        var export = Assert.Single(menu.Items, item => item.TextContent == "Export");
        export.DispatchEvent(StandardEvents.CreateClick());
        var exportMenu = Assert.IsType<Menu>(export.Submenu);
        Assert.Equal(96, exportMenu.PopupBounds.Height);
        Assert.Equal(menu.PopupBounds.Right, exportMenu.PopupBounds.X);
        Assert.Equal(menu.PopupBounds.Y + export.Geometry.Y - menu.Geometry.Y, exportMenu.PopupBounds.Y);

        var advanced = Assert.Single(exportMenu.Items, item => item.TextContent == "Advanced");
        advanced.DispatchEvent(StandardEvents.CreateClick());
        var advancedMenu = Assert.IsType<Menu>(advanced.Submenu);
        Assert.Equal(32, advancedMenu.PopupBounds.Height);
        Assert.Equal(exportMenu.PopupBounds.Right, advancedMenu.PopupBounds.X);
        Assert.Equal(exportMenu.PopupBounds.Y + advanced.Geometry.Y - exportMenu.Geometry.Y, advancedMenu.PopupBounds.Y);
    }

    [Fact]
    public void DocumentLayoutKeepsTopLevelMenuItemsContentSized()
    {
        var document = new UIDocument();
        document.Body.Children.Add(new Main());
        document.Build();
        var layout = new LayoutEngine();

        layout.Measure(document.DocumentElement, new Size(900, 980));
        layout.Arrange(document.DocumentElement, new Rect(0, 0, 900, 980));

        var bar = Assert.Single(document.DocumentElement.QueryAll<MenuBar>());
        var items = bar.Children.OfType<MenuItem>().ToArray();
        Assert.Equal(56, items[0].Geometry.Width);
        Assert.Equal(56, items[1].Geometry.Width);
        Assert.Equal(items[0].Geometry.Right, items[1].Geometry.X);
    }

    [Fact]
    public void GeneratedEventsUpdateShowForAndInputBinding()
    {
        var component = new Main();
        component.BuildElementTree();
        var root = Assert.IsType<View>(Assert.Single(component.Children));
        var controlsPage = Assert.Single(root.QueryAll<ControlsSamplesPage>());
        var textPage = Assert.Single(root.QueryAll<TextSamplesPage>());
        var button = Assert.Single(controlsPage.QueryAll<Button>(), item => item.TextContent == "Button - add activity");
        var input = Assert.Single(textPage.QueryAll<Input>(), editor => editor.ClassList.Contains("editor-default"));

        input.SelectAll();
        input.HandleTextInput("A");
        Assert.Equal("A", textPage.Name.Value);

        button.DispatchEvent(StandardEvents.CreateClick());
        Reconciler.Current.Flush();
        Assert.True(controlsPage.LastEventSourceWasButton.Value);
        Assert.True(controlsPage.ShowCount.Value);
        Assert.Equal(2, controlsPage.Items.Count);
        Assert.Contains(root.QueryAll<Square.Controls.Text>(), text => text.TextContent == "Show: button clicked");
        Assert.Contains(root.QueryAll<Square.Controls.Text>(), text => text.TextContent == "Click 1");
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
        var button = Assert.Single(controlsPage.QueryAll<Button>(), item => item.TextContent == "Button - add activity");
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
    public void PrintableKeyIsInsertedOnlyByTextInput()
    {
        var input = new Input();

        input.HandleKey('A');
        Assert.Equal("", input.Value);

        input.HandleTextInput("a");
        Assert.Equal("a", input.Value);
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
        var child = new Square.Controls.Text("copy me") { Geometry = new Rect(0, 0, 200, 24) };
        parent.Children.Add(child);

        Assert.False(child.IsUserSelectText());

        parent.Style.Set("user-select", "text");
        Assert.True(child.IsUserSelectText());
        Assert.Equal("copy me", Assert.IsAssignableFrom<ITextSelectable>(child).SelectableText);

        child.Style.Set("user-select", "none");
        Assert.False(child.IsUserSelectText());
    }

    [Fact]
    public void DocumentTextSelectionTracksNestedScrollOffsetsAndClip()
    {
        var outer = new ScrollViewer { Geometry = new Rect(10, 20, 300, 180) };
        outer.SetScrollContentSize(new Size(500, 600));
        outer.ScrollTo(25, 40);
        var inner = new ScrollViewer { Geometry = new Rect(30, 60, 240, 120) };
        inner.SetScrollContentSize(new Size(240, 400));
        inner.ScrollTo(0, 35);
        var text = new Square.Controls.Text("selected") { Geometry = new Rect(40, 180, 100, 24) };
        outer.Children.Add(inner);
        inner.Children.Add(text);

        var offsetMethod = typeof(Square.Hosting.DesktopApplication).GetMethod(
            "GetTextSelectionVisualOffset",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var clipMethod = typeof(Square.Hosting.DesktopApplication).GetMethod(
            "GetTextSelectionClip",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;

        var offset = Assert.IsType<Point>(offsetMethod.Invoke(null, [text]));
        var clip = Assert.IsType<Rect>(clipMethod.Invoke(null, [text]));

        Assert.Equal(new Point(0, -75), offset);
        Assert.Equal(new Rect(30, 20, 240, 120), clip);
    }

    [Fact]
    public void InvalidCssColorCanBeRejectedWithoutThrowing()
    {
        Assert.False(Color.TryParse("inherit", out _));
        Assert.False(Color.TryParse("not-a-color", out _));
        Assert.True(Color.TryParse("#369", out var shortColor));
        Assert.Equal(Color.FromRgb(51, 102, 153), shortColor);
        Assert.True(Color.TryParse("#212529", out var fullColor));
        Assert.Equal(Color.FromRgb(33, 37, 41), fullColor);
    }

    [Fact]
    public void ApplicationResourcesPreferPublicThenRootThenAssets()
    {
        var assembly = typeof(M1IntegrationTests).Assembly;

        var preferred = System.Text.Encoding.UTF8.GetString(
            ApplicationResource.ReadAllBytes("resource-priority.txt", assembly)).Trim();
        var embedded = System.Text.Encoding.UTF8.GetString(
            ApplicationResource.ReadAllBytes("embedded-only.txt", assembly)).Trim();

        Assert.Equal("public", preferred);
        Assert.Equal("embedded", embedded);
        Assert.Contains(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith("Assets.resource-priority.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void LinkUsesHandCursorAndDispatchesActivation()
    {
        var link = new TrackingLink("Open", "https://example.test");
        var selectableRoot = new View();
        selectableRoot.Style.Set("user-select", "text");
        selectableRoot.Children.Add(link);
        var resolveCursor = typeof(Square.Hosting.DesktopApplication).GetMethod(
            "ResolveCursor",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;

        link.Style.ClearCascaded();
        var cursor = Assert.IsType<CursorKind>(resolveCursor.Invoke(null, [link]));
        link.DispatchEvent(StandardEvents.CreateClick());

        Assert.Equal(CursorKind.Hand, cursor);
        Assert.Equal(1, link.ActivationCount);

        link.IsEnabled = false;
        link.Style.ClearCascaded();
        cursor = Assert.IsType<CursorKind>(resolveCursor.Invoke(null, [link]));
        link.DispatchEvent(StandardEvents.CreateClick());
        Assert.Equal(CursorKind.Arrow, cursor);
        Assert.Equal(1, link.ActivationCount);
    }

    [Fact]
    public void CssCursorOverridesDefaultsAndIsResolvedFromAncestors()
    {
        var parent = new View();
        var child = new Square.Controls.Text("child");
        parent.Children.Add(child);
        var resolveCursor = typeof(Square.Hosting.DesktopApplication).GetMethod(
            "ResolveCursor",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;

        parent.Style.Set("cursor", "pointer");
        Assert.Equal(CursorKind.Hand, Assert.IsType<CursorKind>(resolveCursor.Invoke(null, [child])));

        child.Style.Set("cursor", "text");
        Assert.Equal(CursorKind.Text, Assert.IsType<CursorKind>(resolveCursor.Invoke(null, [child])));

        child.Style.Set("cursor", "default");
        Assert.Equal(CursorKind.Arrow, Assert.IsType<CursorKind>(resolveCursor.Invoke(null, [child])));

        var link = new TrackingLink("Open", "https://example.test");
        link.Style.Set("cursor", "text");
        Assert.Equal(CursorKind.Text, Assert.IsType<CursorKind>(resolveCursor.Invoke(null, [link])));
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
        Assert.Equal(10, input.CaretRect.Y - input.Geometry.Y);
        Assert.Equal(8, textArea.CaretRect.Y - textArea.Geometry.Y);
        Assert.Equal(17, input.CaretRect.Height);

        textArea.HandleKey(40);
        Assert.Equal(5, textArea.CaretIndex);
        Assert.Equal(25, textArea.CaretRect.Y - textArea.Geometry.Y);
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
        Assert.Equal(8, input.CaretRect.Y - input.Geometry.Y);
        Assert.Equal(28, input.CaretRect.Height);

        input.SelectAll();
        var getSelectionRects = typeof(TextEditorBase).GetMethod(
            "GetSelectionRects",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var selection = Assert.Single((List<Rect>)getSelectionRects.Invoke(input, [input.Value])!);
        Assert.Equal(input.CaretRect.Y, selection.Y);
        Assert.Equal(input.CaretRect.Height, selection.Height);

        input.Style.Set("line-height", "2");
        Assert.Equal(28, input.CaretRect.Height);
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
        var image = Assert.Single(component.QueryAll<Square.Controls.Image>());
        var canvas = Assert.Single(component.QueryAll<Canvas>());

        textArea.SelectAll();
        textArea.HandleTextInput("N");
        textArea.HandleKey(13);
        textArea.HandleTextInput("X");
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
        card.Slots.Set("header", parent => parent.Children.Add(new Square.Controls.Text("Named header")));
        card.Slots.Set("", parent =>
        {
            parent.Children.Add(new Square.Controls.Text("First body"));
            parent.Children.Add(new Square.Controls.Text("Second body"));
        });

        card.BuildElementTree();

        var root = Assert.IsType<View>(Assert.Single(card.Children));
        Assert.Equal(2, root.Children.Count);
        var header = Assert.IsType<View>(root.Children[0]);
        var content = Assert.IsType<View>(root.Children[1]);
        Assert.Equal("Named header", Assert.IsType<Square.Controls.Text>(Assert.Single(header.Children)).TextContent);
        Assert.Equal(2, content.Children.Count);
        Assert.All(content.Children, child => Assert.IsType<Square.Controls.Text>(child));

        var fallbackCard = new SlotCard();
        fallbackCard.BuildElementTree();
        Assert.Contains(fallbackCard.QueryAll<Square.Controls.Text>(), text => text.TextContent == "Fallback header");
        Assert.Contains(fallbackCard.QueryAll<Square.Controls.Text>(), text => text.TextContent == "Fallback content");
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
        Assert.Contains(userPage.QueryAll<Square.Controls.Text>(),
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
    public void ScrollViewerDefaultsToVerticalOverflowAndClampsOffsets()
    {
        var scroller = new ScrollViewer { Geometry = new Rect(0, 0, 100, 40) };
        scroller.SetScrollContentSize(new Size(180, 140));

        scroller.ScrollTo(50, 200);

        Assert.Equal(0, scroller.HorizontalOffset);
        Assert.Equal(100, scroller.VerticalOffset);
        Assert.Equal(80, scroller.ScrollableWidth);
        Assert.Equal(100, scroller.ScrollableHeight);
        Assert.Equal(100, scroller.ViewportWidth);
        Assert.Equal(40, scroller.ViewportHeight);
    }

    [Fact]
    public void ScrollViewerDispatchesScrollWhenOffsetChanges()
    {
        var scroller = new ScrollViewer { Geometry = new Rect(0, 0, 100, 40) };
        scroller.SetScrollContentSize(new Size(100, 140));
        var events = 0;
        scroller.AddEventListener(StandardEvents.Scroll, _ => events++);

        scroller.ScrollToBottom();
        scroller.ScrollToBottom();
        scroller.ScrollToTop();

        Assert.Equal(2, events);
        Assert.Equal(0, scroller.VerticalOffset);
    }

    [Fact]
    public void ScrollViewerWheelUsesExistingOverflowDefaultAction()
    {
        var scroller = new ScrollViewer { Geometry = new Rect(0, 0, 100, 40) };
        scroller.SetScrollContentSize(new Size(100, 140));
        var child = new Button { Geometry = new Rect(0, 80, 100, 20) };
        scroller.Children.Add(child);

        child.DispatchTrusted(StandardEvents.CreateWheel(0, 30));

        Assert.Equal(30, scroller.VerticalOffset);
    }

    [Fact]
    public void ScrollViewerDefaultOverflowCanBeOverriddenByCss()
    {
        var scroller = new ScrollViewer { Geometry = new Rect(0, 0, 100, 40) };
        scroller.Style.SetCascaded("overflow-x", "auto", 10);
        scroller.Style.SetCascaded("overflow-y", "hidden", 10);
        scroller.SetScrollContentSize(new Size(180, 140));

        scroller.ScrollTo(50, 50);

        Assert.Equal(50, scroller.HorizontalOffset);
        Assert.Equal(0, scroller.VerticalOffset);
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
    public void SelectKeyboardOpensNavigatesAndChoosesOption()
    {
        var select = new Select
        {
            Options = ["Blue", "Green", "Orange"],
            Value = "Green"
        };
        var changes = 0;
        select.AddEventListener("change", () => changes++);

        var open = StandardEvents.CreateKeyDown(32);
        select.DispatchEvent(open);

        Assert.True(select.IsOpen);
        Assert.True(open.DefaultPrevented);
        Assert.True(select.HandlePopupKey(40, false, false, false));
        Assert.True(select.HandlePopupKey(13, false, false, false));
        Assert.Equal("Orange", select.Value);
        Assert.False(select.IsOpen);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void SelectKeyboardSupportsHomeEndAndEscapeWithoutChangingValue()
    {
        var select = new Select
        {
            Options = ["Blue", "Green", "Orange"],
            Value = "Green"
        };
        var changes = 0;
        select.AddEventListener("change", () => changes++);

        Assert.True(select.HandleKey(40, alt: true));
        Assert.True(select.HandlePopupKey(35, false, false, false));
        Assert.True(select.HandlePopupKey(27, false, false, false));

        Assert.Equal("Green", select.Value);
        Assert.False(select.IsOpen);
        Assert.Equal(0, changes);

        Assert.True(select.HandleKey(13));
        Assert.True(select.HandlePopupKey(36, false, false, false));
        Assert.True(select.HandlePopupKey(32, false, false, false));

        Assert.Equal("Blue", select.Value);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void SelectDoesNotHandleKeyboardWhenDisabledOrEmpty()
    {
        var disabled = new Select { Options = ["Blue"], IsDisabled = true };
        var empty = new Select();

        Assert.False(disabled.HandleKey(13));
        Assert.False(disabled.IsOpen);
        Assert.False(empty.HandleKey(32));
        Assert.False(empty.IsOpen);
    }

    [Fact]
    public void SelectDoesNotRaiseChangeWhenChoosingCurrentOption()
    {
        var select = new Select
        {
            Options = ["Blue", "Green"],
            Value = "Blue"
        };
        var changes = 0;
        select.AddEventListener("change", () => changes++);

        Assert.True(select.HandleKey(13));
        Assert.True(select.HandlePopupKey(13, false, false, false));

        Assert.Equal("Blue", select.Value);
        Assert.False(select.IsOpen);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void PopupPositionsBelowAnchorAndHitTestsTranslatedChildren()
    {
        var root = new View { Geometry = new Rect(0, 0, 400, 300) };
        var anchor = new Button { Geometry = new Rect(100, 40, 80, 30) };
        var popup = new Popup
        {
            Geometry = new Rect(0, 0, 120, 60),
            Anchor = anchor,
            Placement = PopupPlacement.Bottom,
            Alignment = PopupAlignment.Center,
            VerticalOffset = 6
        };
        var child = new Button { Geometry = new Rect(10, 10, 80, 30) };
        popup.Children.Add(child);
        root.Children.Add(anchor);
        root.Children.Add(popup);
        popup.Open();
        var tree = new DisplayTree();
        tree.BuildFrom(root);

        Assert.Equal(new Rect(80, 76, 120, 60), popup.PopupBounds);
        Assert.Same(child, tree.HitTestPopups(new Point(95, 90)));
        Assert.Same(root, root.HitTest(new Point(95, 90)));
    }

    [Theory]
    [InlineData(PopupPlacement.Top, 109, 34)]
    [InlineData(PopupPlacement.Left, 26, 81)]
    [InlineData(PopupPlacement.Right, 184, 81)]
    public void PopupSupportsTopLeftAndRightPlacement(PopupPlacement placement, float expectedX, float expectedY)
    {
        var anchor = new Button { Geometry = new Rect(100, 80, 80, 30) };
        var popup = new Popup
        {
            Geometry = new Rect(0, 0, 70, 40),
            Anchor = anchor,
            Placement = placement,
            Alignment = PopupAlignment.Center,
            HorizontalOffset = 4,
            VerticalOffset = 6
        };
        popup.Open();

        Assert.Equal(new Rect(expectedX, expectedY, 70, 40), popup.PopupBounds);
    }

    [Fact]
    public void DisplayTreeDismissesPopupOnlyOutsidePopupAndAnchor()
    {
        var root = new View { Geometry = new Rect(0, 0, 400, 300) };
        var anchor = new Button { Geometry = new Rect(100, 40, 80, 30) };
        var popup = new Popup { Geometry = new Rect(0, 0, 120, 60), Anchor = anchor };
        root.Children.Add(anchor);
        root.Children.Add(popup);
        var tree = new DisplayTree();

        popup.Open();
        tree.BuildFrom(root);
        Assert.False(tree.DismissPopupsOutside(new Point(110, 50)));
        Assert.True(popup.IsOpen);
        Assert.False(tree.DismissPopupsOutside(new Point(110, 90)));
        Assert.True(popup.IsOpen);

        Assert.True(tree.DismissPopupsOutside(new Point(300, 250)));
        Assert.False(popup.IsOpen);
    }

    [Fact]
    public void PopupOpenCloseEventsFireOnlyWhenStateChanges()
    {
        var popup = new Popup { Geometry = new Rect(0, 0, 100, 40) };
        var opens = 0;
        var closes = 0;
        popup.AddEventListener("open", _ => opens++);
        popup.AddEventListener("close", _ => closes++);

        popup.Open();
        popup.Open();
        popup.Close();
        popup.Close();

        Assert.Equal(1, opens);
        Assert.Equal(1, closes);
    }

    [Fact]
    public void ModalDialogCentersInDocumentAndBlocksBackdropHitTesting()
    {
        var document = new UIDocument();
        document.Body.Geometry = new Rect(0, 0, 400, 300);
        var underlying = new Button { Geometry = new Rect(10, 10, 100, 40) };
        var dialog = new Dialog { Geometry = new Rect(0, 0, 160, 100) };
        document.Body.Children.Add(underlying);
        document.Body.Children.Add(dialog);
        dialog.Open();
        var tree = new DisplayTree();
        tree.BuildFrom(document.Body);

        Assert.Equal(new Rect(0, 0, 400, 300), dialog.PopupBounds);
        Assert.Same(dialog, tree.HitTestPopups(new Point(20, 20)));
        Assert.Same(dialog, tree.HitTestPopups(new Point(130, 110)));
    }

    [Fact]
    public void ClosedDialogDoesNotInterceptPopupHitTesting()
    {
        var root = new View { Geometry = new Rect(0, 0, 400, 300) };
        var button = new Button { Geometry = new Rect(20, 20, 120, 36) };
        var dialog = new Dialog { Geometry = new Rect(0, 0, 240, 140) };
        root.Children.Add(button);
        root.Children.Add(dialog);
        var tree = new DisplayTree();
        tree.BuildFrom(root);

        Assert.Null(tree.HitTestPopups(new Point(40, 30)));
        Assert.Same(button, root.HitTest(new Point(40, 30)));
    }

    [Fact]
    public void DialogMapsScreenPointerCoordinatesForTextSelection()
    {
        var document = new UIDocument();
        document.Ui.Geometry = new Rect(0, 0, 500, 300);
        var dialog = new Dialog { Geometry = new Rect(0, 0, 300, 140) };
        var input = new Input { Value = "selectable text", Geometry = new Rect(20, 40, 220, 36) };
        dialog.Children.Add(input);
        document.Body.Children.Add(dialog);
        dialog.Open();
        var contentX = (document.Ui.Geometry.Width - dialog.Geometry.Width) / 2f;
        var contentY = (document.Ui.Geometry.Height - dialog.Geometry.Height) / 2f;

        input.HandlePointerDown(dialog.MapPointToContent(new Point(contentX + 30, contentY + 58)));
        input.HandlePointerMove(dialog.MapPointToContent(new Point(contentX + 130, contentY + 58)));
        input.HandlePointerUp(dialog.MapPointToContent(new Point(contentX + 130, contentY + 58)));

        Assert.True(input.SelectionLength > 0);
        Assert.NotEmpty(input.SelectedText);
    }

    [Fact]
    public void DialogBackdropDismissIsConfigurable()
    {
        var document = new UIDocument();
        document.Body.Geometry = new Rect(0, 0, 400, 300);
        var dialog = new Dialog { Geometry = new Rect(0, 0, 160, 100), CloseOnBackdropClick = true };
        document.Body.Children.Add(dialog);
        dialog.Open();
        var tree = new DisplayTree();
        tree.BuildFrom(document.Body);

        Assert.True(tree.DismissPopupsOutside(new Point(20, 20)));
        Assert.False(dialog.IsOpen);

        dialog.CloseOnBackdropClick = false;
        dialog.Open();
        Assert.False(tree.DismissPopupsOutside(new Point(20, 20)));
        Assert.True(dialog.IsOpen);
    }

    [Fact]
    public void DialogEscapeClosesTopmostEligiblePopup()
    {
        var root = new View { Geometry = new Rect(0, 0, 400, 300) };
        var first = new Dialog { Geometry = new Rect(0, 0, 160, 100) };
        var second = new Dialog { Geometry = new Rect(0, 0, 120, 80), CloseOnEscape = false };
        root.Children.Add(first);
        root.Children.Add(second);
        first.Open();
        second.Open();
        var tree = new DisplayTree();
        tree.BuildFrom(root);

        Assert.True(tree.DismissTopmostPopupOnEscape());
        Assert.False(first.IsOpen);
        Assert.True(second.IsOpen);
    }

    [Fact]
    public void DialogMovesFocusInsideAndRestoresPreviousFocus()
    {
        var document = new UIDocument();
        document.Body.Geometry = new Rect(0, 0, 400, 300);
        var trigger = new Button { Geometry = new Rect(10, 10, 80, 30) };
        var dialog = new Dialog { Geometry = new Rect(0, 0, 160, 100) };
        var input = new Input { Geometry = new Rect(10, 10, 120, 36) };
        dialog.Children.Add(input);
        document.Body.Children.Add(trigger);
        document.Body.Children.Add(dialog);
        ((IComponentLifecycle)document.Body).OnAttached();
        trigger.Focus();

        dialog.Open();

        Assert.False(trigger.IsFocused);
        Assert.True(input.IsFocused);

        dialog.Close();

        Assert.False(input.IsFocused);
        Assert.True(trigger.IsFocused);
    }

    [Fact]
    public void MenuCheckItemsToggleIndependentlyAndCanStayOpen()
    {
        var menu = new Menu { Geometry = new Rect(0, 0, 220, 100) };
        var grid = new MenuItem { TextContent = "Grid", IsCheckable = true, Geometry = new Rect(0, 0, 220, 32) };
        var guides = new MenuItem
        {
            TextContent = "Guides",
            IsCheckable = true,
            StaysOpenOnClick = true,
            Geometry = new Rect(0, 32, 220, 32)
        };
        menu.Children.Add(grid);
        menu.Children.Add(guides);
        menu.OpenAt(new Point(10, 10));

        guides.DispatchEvent(StandardEvents.CreateClick());

        Assert.True(guides.IsChecked);
        Assert.False(grid.IsChecked);
        Assert.True(menu.IsOpen);

        grid.DispatchEvent(StandardEvents.CreateClick());

        Assert.True(grid.IsChecked);
        Assert.True(guides.IsChecked);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void MenuRadioGroupsAreExclusiveAcrossNestedMenuTree()
    {
        var rootMenu = new Menu { Geometry = new Rect(0, 0, 220, 120) };
        var light = new MenuItem { TextContent = "Light", GroupName = "theme", Geometry = new Rect(0, 0, 220, 32) };
        var owner = new MenuItem { TextContent = "More", Geometry = new Rect(0, 32, 220, 32) };
        var submenu = new Menu { Geometry = new Rect(0, 0, 220, 80) };
        var dark = new MenuItem { TextContent = "Dark", GroupName = "theme", Geometry = new Rect(0, 0, 220, 32) };
        var accent = new MenuItem { TextContent = "Blue", GroupName = "accent", Geometry = new Rect(0, 32, 220, 32) };
        submenu.Children.Add(dark);
        submenu.Children.Add(accent);
        owner.Children.Add(submenu);
        rootMenu.Children.Add(light);
        rootMenu.Children.Add(owner);

        light.DispatchEvent(StandardEvents.CreateClick());
        dark.DispatchEvent(StandardEvents.CreateClick());
        accent.DispatchEvent(StandardEvents.CreateClick());

        Assert.False(light.IsChecked);
        Assert.True(dark.IsChecked);
        Assert.True(accent.IsChecked);
    }

    [Fact]
    public void MenuItemCommandExecutesAfterCancelableClickAndClosesTree()
    {
        var menu = new Menu { Geometry = new Rect(0, 0, 220, 80) };
        var item = new MenuItem { TextContent = "Run", Geometry = new Rect(0, 0, 220, 32) };
        var executions = 0;
        item.Command = _ => executions++;
        menu.Children.Add(item);
        menu.OpenAt(new Point(0, 0));

        item.DispatchEvent(StandardEvents.CreateClick());

        Assert.Equal(1, executions);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void MenuItemPreventDefaultSkipsActivation()
    {
        var menu = new Menu { Geometry = new Rect(0, 0, 220, 80) };
        var item = new MenuItem { TextContent = "Run", IsCheckable = true, Geometry = new Rect(0, 0, 220, 32) };
        var executions = 0;
        item.Command = _ => executions++;
        item.AddEventListener(StandardEvents.Click, e => e.PreventDefault());
        menu.Children.Add(item);
        menu.OpenAt(new Point(0, 0));

        item.DispatchEvent(StandardEvents.CreateClick());

        Assert.False(item.IsChecked);
        Assert.Equal(0, executions);
        Assert.True(menu.IsOpen);
    }

    [Fact]
    public void MenuSupportsThreeLevelsAndClosesWholeTreeFromLeaf()
    {
        var root = new Menu { Geometry = new Rect(0, 0, 220, 80) };
        var firstOwner = new MenuItem { TextContent = "Export", Geometry = new Rect(0, 0, 220, 32) };
        var first = new Menu { Geometry = new Rect(0, 0, 220, 80) };
        var secondOwner = new MenuItem { TextContent = "Advanced", Geometry = new Rect(0, 0, 220, 32) };
        var second = new Menu { Geometry = new Rect(0, 0, 220, 80) };
        var leaf = new MenuItem { TextContent = "PDF", Geometry = new Rect(0, 0, 220, 32) };
        second.Children.Add(leaf);
        secondOwner.Children.Add(second);
        first.Children.Add(secondOwner);
        firstOwner.Children.Add(first);
        root.Children.Add(firstOwner);

        root.OpenAt(new Point(0, 0));
        firstOwner.DispatchEvent(StandardEvents.CreateClick());
        secondOwner.DispatchEvent(StandardEvents.CreateClick());

        Assert.True(root.IsOpen);
        Assert.True(first.IsOpen);
        Assert.True(second.IsOpen);

        leaf.DispatchEvent(StandardEvents.CreateClick());

        Assert.False(root.IsOpen);
        Assert.False(first.IsOpen);
        Assert.False(second.IsOpen);
    }

    [Fact]
    public void MenuBarHoverSwitchesOnlyAfterMenuModeStarts()
    {
        var bar = new MenuBar();
        var file = new MenuItem { TextContent = "File" };
        var edit = new MenuItem { TextContent = "Edit" };
        var fileMenu = new Menu { Geometry = new Rect(0, 0, 200, 80) };
        var editMenu = new Menu { Geometry = new Rect(0, 0, 200, 80) };
        file.Children.Add(fileMenu);
        edit.Children.Add(editMenu);
        bar.Children.Add(file);
        bar.Children.Add(edit);

        edit.SetState(ElementState.Hover, true);
        Assert.False(editMenu.IsOpen);

        file.DispatchEvent(StandardEvents.CreateClick());
        Assert.True(file.HasState(ElementState.Open));
        edit.SetState(ElementState.Hover, false);
        edit.SetState(ElementState.Hover, true);

        Assert.False(fileMenu.IsOpen);
        Assert.False(file.HasState(ElementState.Open));
        Assert.True(editMenu.IsOpen);
        Assert.True(edit.HasState(ElementState.Open));
        Assert.Equal(1, bar.ActiveIndex);

        bar.CloseMenus();
        Assert.False(edit.HasState(ElementState.Open));
    }

    [Fact]
    public void MenuItemMeasureKeepsLabelAndShortcutSeparated()
    {
        var item = new MenuItem
        {
            TextContent = "Clear document",
            ShortcutText = "Ctrl+Shift+Delete"
        };
        var label = new Square.Graphics.TextLayout(
            item.TextContent,
            new Square.Graphics.Font("Segoe UI", 14)).Measure();
        var shortcut = new Square.Graphics.TextLayout(
            item.ShortcutText,
            new Square.Graphics.Font("Segoe UI", 12)).Measure();

        var measured = item.Measure(new Size(1000, 32));

        Assert.True(measured.Width >= 54 + label.Width + 20 + shortcut.Width);
    }

    [Fact]
    public void MenuKeyboardNavigationSkipsDisabledItemsAndActivatesSelection()
    {
        var menu = new Menu { Geometry = new Rect(0, 0, 220, 120) };
        var disabled = new MenuItem { TextContent = "Disabled", IsDisabled = true };
        var first = new MenuItem { TextContent = "First" };
        var second = new MenuItem { TextContent = "Second", IsCheckable = true, StaysOpenOnClick = true };
        menu.Children.Add(disabled);
        menu.Children.Add(first);
        menu.Children.Add(second);
        menu.OpenAt(new Point(0, 0));

        Assert.True(menu.HandleKey(40));
        Assert.Same(first, menu.ActiveItem);
        Assert.True(menu.HandleKey(40));
        Assert.Same(second, menu.ActiveItem);
        Assert.True(menu.HandleKey(13));

        Assert.True(second.IsChecked);
        Assert.True(menu.IsOpen);

        Assert.True(menu.HandleKey(36));
        Assert.Same(first, menu.ActiveItem);
        Assert.True(menu.HandleKey(35));
        Assert.Same(second, menu.ActiveItem);
    }

    [Fact]
    public void MenuKeyboardRightOpensSubmenuAndLeftReturnsToParent()
    {
        var root = new Menu { Geometry = new Rect(0, 0, 220, 80) };
        var owner = new MenuItem { TextContent = "More" };
        var submenu = new Menu { Geometry = new Rect(0, 0, 180, 80) };
        var child = new MenuItem { TextContent = "Child" };
        submenu.Children.Add(child);
        owner.Children.Add(submenu);
        root.Children.Add(owner);
        root.OpenAt(new Point(0, 0));
        Assert.True(root.HandleKey(40));

        Assert.True(root.HandleKey(39));
        Assert.True(submenu.IsOpen);
        Assert.Same(child, submenu.ActiveItem);

        Assert.True(submenu.HandleKey(37));
        Assert.False(submenu.IsOpen);
        Assert.Same(owner, root.ActiveItem);
    }

    [Fact]
    public void DisplayTreeRoutesKeyboardToDeepestOpenMenu()
    {
        var root = new View { Geometry = new Rect(0, 0, 400, 300) };
        var menu = new Menu { Geometry = new Rect(0, 0, 220, 80) };
        var item = new MenuItem { TextContent = "Toggle", IsCheckable = true, StaysOpenOnClick = true };
        menu.Children.Add(item);
        root.Children.Add(menu);
        menu.OpenAt(new Point(0, 0));
        var tree = new DisplayTree();
        tree.BuildFrom(root);

        Assert.True(tree.HandlePopupKey(40, false, false, false));
        Assert.True(tree.HandlePopupKey(32, false, false, false));

        Assert.True(item.IsChecked);
    }

    [Fact]
    public void MenuFlipsAtViewportEdges()
    {
        var document = new UIDocument();
        document.Body.Geometry = new Rect(0, 0, 300, 200);
        var bar = new MenuBar();
        var bottomOwner = new MenuItem { Geometry = new Rect(20, 170, 80, 28) };
        var bottomMenu = new Menu { Geometry = new Rect(0, 0, 140, 100) };
        bottomOwner.Children.Add(bottomMenu);
        bar.Children.Add(bottomOwner);
        document.Body.Children.Add(bar);
        bottomMenu.OpenFor(bottomOwner);

        Assert.True(bottomMenu.PopupBounds.Bottom <= bottomOwner.Geometry.Y);

        var rightOwner = new MenuItem { Geometry = new Rect(270, 40, 28, 32) };
        var rightMenu = new Menu { Geometry = new Rect(0, 0, 120, 100) };
        var parentMenu = new Menu { Geometry = new Rect(0, 0, 160, 100) };
        parentMenu.Children.Add(rightOwner);
        rightOwner.Children.Add(rightMenu);
        document.Body.Children.Add(parentMenu);
        rightMenu.OpenFor(rightOwner);

        Assert.True(rightMenu.PopupBounds.Right <= rightOwner.Geometry.X);
    }

    [Fact]
    public void NestedMenuDoesNotFlipWhenDocumentViewportHasRoom()
    {
        var document = new UIDocument();
        document.Ui.Geometry = new Rect(0, 0, 900, 600);
        document.Body.Geometry = new Rect(0, 0, 480, 600);
        var parentMenu = new Menu { Geometry = new Rect(0, 0, 240, 96) };
        var owner = new MenuItem { Geometry = new Rect(0, 64, 240, 32) };
        var childMenu = new Menu { Geometry = new Rect(0, 0, 240, 32) };
        owner.Children.Add(childMenu);
        parentMenu.Children.Add(owner);
        document.Body.Children.Add(parentMenu);
        parentMenu.OpenAt(new Point(248, 96));

        childMenu.OpenFor(owner);

        Assert.Equal(parentMenu.PopupBounds.Right, childMenu.PopupBounds.X);
        Assert.Equal(parentMenu.PopupBounds.Y + 64, childMenu.PopupBounds.Y);
    }

    [Fact]
    public void KeyboardEventCarriesKeyCodeAndModifiers()
    {
        var target = new Button();
        KeyboardEvent? received = null;
        target.AddEventListener<KeyboardEvent>(StandardEvents.KeyDown, e => received = e);

        target.DispatchTrusted(StandardEvents.CreateKeyDown(40, shiftKey: true, controlKey: true));

        Assert.NotNull(received);
        Assert.Equal(40, received!.KeyCode);
        Assert.True(received.ShiftKey);
        Assert.True(received.ControlKey);
        Assert.False(received.AltKey);
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
        var shown = new Square.Controls.Text("shown");
        var show = new ShowNode(visible, () => shown);
        show.AttachTo(root);

        var items = new ObservableCollection<string> { "a" };
        var nodes = new Dictionary<string, Square.Controls.Text>();
        var loop = ForNode.Create(items, item => nodes[item] = new Square.Controls.Text(item));
        loop.AttachTo(root);

        ((IComponentLifecycle)root).OnAttached();
        visible.Value = true;
        items.Add("b");
        Reconciler.Current.Flush();

        Assert.True(shown.IsAttached);
        Assert.Equal(new[] { "a", "b" }, root.QueryAll<Square.Controls.Text>().Where(text => text != shown).Select(text => text.TextContent));

        items.Move(1, 0);
        Reconciler.Current.Flush();
        Assert.Same(nodes["b"], root.Children[1]);

        visible.Value = false;
        Reconciler.Current.Flush();
        Assert.False(shown.IsAttached);
    }

    [Fact]
    public void SwitchNodeReactsToObservableBranchCondition()
    {
        Reconciler.Current.Reset();
        var root = new View();
        var selected = new ObservableValue<bool>(false);
        var matched = new Square.Controls.Text("matched");
        var fallback = new Square.Controls.Text("fallback");
        var node = new SwitchNode();
        node.AddBranch(selected, () => selected, () => matched);
        node.AddDefault(() => fallback);
        node.AttachTo(root);
        ((IComponentLifecycle)root).OnAttached();
        Reconciler.Current.Flush();

        Assert.Same(fallback, Assert.Single(root.Children));

        selected.Value = true;
        Reconciler.Current.Flush();
        Assert.Same(matched, Assert.Single(root.Children));

        selected.Value = false;
        Reconciler.Current.Flush();
        Assert.Same(fallback, Assert.Single(root.Children));

        node.Dispose();
        selected.Value = true;
        Reconciler.Current.Flush();
        Assert.Empty(root.Children);
    }

    [Fact]
    public void ShowNodeRendersAndReusesFallback()
    {
        var root = new View();
        var visible = new ObservableValue<bool>(false);
        var content = new Square.Controls.Text("content");
        var fallback = new Square.Controls.Text("fallback");
        var node = new ShowNode(visible, () => content, () => fallback);
        node.AttachTo(root);

        Assert.Same(fallback, Assert.Single(root.Children));
        visible.Value = true;
        Reconciler.Current.Flush();
        Assert.Same(content, Assert.Single(root.Children));
        visible.Value = false;
        Reconciler.Current.Flush();
        Assert.Same(fallback, Assert.Single(root.Children));
        node.Dispose();
    }

    [Fact]
    public void ForNodeRendersFallbackWhenCollectionIsEmpty()
    {
        var root = new View();
        var items = new ObservableCollection<string>();
        var fallback = new Square.Controls.Text("empty");
        var loop = ForNode.Create(items, item => new Square.Controls.Text(item), () => fallback);
        loop.AttachTo(root);

        Assert.Same(fallback, Assert.Single(root.Children));
        items.Add("value");
        Reconciler.Current.Flush();
        Assert.Equal("value", Assert.IsType<Square.Controls.Text>(Assert.Single(root.Children)).TextContent);
        items.Clear();
        Reconciler.Current.Flush();
        Assert.Same(fallback, Assert.Single(root.Children));
        loop.Dispose();
    }

    [Fact]
    public void ComputedBindingUpdatesFromAllReactiveSources()
    {
        var text = new Square.Controls.Text();
        var first = new ObservableValue<string>("Ada");
        var last = new ObservableValue<string>("Lovelace");
        text.BindProperty("TextContent", () => "Hello " + first + " " + last, first, last);

        Assert.Equal("Hello Ada Lovelace", text.TextContent);
        first.Value = "Grace";
        Assert.Equal("Hello Grace Lovelace", text.TextContent);
        last.Value = "Hopper";
        Assert.Equal("Hello Grace Hopper", text.TextContent);
    }

    [Fact]
    public void ForNodeIndexedBuildReceivesIndices()
    {
        var root = new View();
        var items = new ObservableCollection<string> { "a", "b" };
        var captured = new List<(string, int)>();
        var loop = ForNode.Create(items, (item, index) =>
        {
            captured.Add((item, index));
            return new Square.Controls.Text(item + index);
        });
        loop.AttachTo(root);
        ((IComponentLifecycle)root).OnAttached();

        Assert.Equal(new[] { "a0", "b1" },
            root.QueryAll<Square.Controls.Text>().Select(text => text.TextContent));
        Assert.Equal(new[] { ("a", 0), ("b", 1) }, captured);

        items.Insert(0, "z");
        Reconciler.Current.Flush();
        // 新插入的 z 获得其插入索引 0；已有节点文本保持不变（运行时按需重建，不重排已有索引）。
        Assert.Equal("z0", root.QueryAll<Square.Controls.Text>().First().TextContent);
        Assert.Equal(3, root.QueryAll<Square.Controls.Text>().Count());

        loop.Dispose();
    }

    [Fact]
    public void KeyedForNodePreservesIdentityWhenItemsMove()
    {
        var root = new View();
        var attached = new List<string>();
        var detached = new List<string>();
        var first = new KeyedItem(1, "first");
        var second = new KeyedItem(2, "second");
        var items = new ObservableCollection<KeyedItem> { first, second };
        var nodes = new Dictionary<int, TrackingText>();
        var loop = ForNode.Create(items, item => item.Id, item =>
            nodes[item.Id] = new TrackingText(item.Name, attached, detached));
        loop.AttachTo(root);
        ((IComponentLifecycle)root).OnAttached();

        items.Move(1, 0);
        Reconciler.Current.Flush();

        Assert.Same(nodes[2], root.Children[0]);
        Assert.Same(nodes[1], root.Children[1]);
        Assert.True(nodes[1].IsAttached);
        Assert.True(nodes[2].IsAttached);
        Assert.Equal(new[] { "first", "second" }, attached);
        Assert.Empty(detached);
        loop.Dispose();
    }

    [Fact]
    public void KeyedForNodeRebuildsWhenSameKeyGetsNewItemInstance()
    {
        var root = new View();
        var items = new ObservableCollection<KeyedItem> { new(1, "old") };
        var loop = ForNode.Create(items, item => item.Id, item => new Square.Controls.Text(item.Name));
        loop.AttachTo(root);
        var original = Assert.IsType<Square.Controls.Text>(Assert.Single(root.Children));

        items[0] = new KeyedItem(1, "new");
        Reconciler.Current.Flush();

        var replacement = Assert.IsType<Square.Controls.Text>(Assert.Single(root.Children));
        Assert.NotSame(original, replacement);
        Assert.Equal("new", replacement.TextContent);
        loop.Dispose();
    }

    [Fact]
    public void KeyedForNodeRejectsDuplicateKeys()
    {
        var items = new ObservableCollection<KeyedItem>
        {
            new(1, "first"),
            new(1, "duplicate")
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ForNode.Create(items, item => item.Id, item => new Square.Controls.Text(item.Name)));

        Assert.Contains("Duplicate key", exception.Message);
    }

    [Fact]
    public void IndexNodePreservesPositionsAndUpdatesValues()
    {
        var root = new View();
        var items = new ObservableCollection<string> { "a", "b" };
        var loop = IndexNode.Create(items, (item, index) => new Square.Controls.Text(item + index));
        loop.AttachTo(root);

        items.Insert(0, "z");
        Reconciler.Current.Flush();

        Assert.Equal(new[] { "z0", "a1", "b2" },
            root.QueryAll<Square.Controls.Text>().Select(text => text.TextContent));
        loop.Dispose();
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

    private sealed record KeyedItem(int Id, string Name);

    private sealed class TrackingLink(string text, string href) : Square.Controls.Link(text, href)
    {
        public int ActivationCount { get; private set; }

        protected override void Activate()
        {
            if (!IsEnabled) return;
            ActivationCount++;
        }
    }

    private sealed class TrackingText(
        string text,
        List<string> attached,
        List<string> detached) : Square.Controls.Text(text)
    {
        protected override void OnAttachedCore() => attached.Add(TextContent);
        protected override void OnDetachedCore() => detached.Add(TextContent);
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
