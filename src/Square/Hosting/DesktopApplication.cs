using System.Diagnostics;
using Square.Backends;
using Square.Controls;
using Square.CSS.Engine;
using Square.Events;
using Square.Graphics;
using Square.Rendering;
using Square.Platform;
using Square.Runtime;
using Square.UI;
using Reconciler = Square.UI.Reconciler;

namespace Square.Hosting;

public sealed class DesktopApplication : Application, IRenderBackendApplication
{
    private static readonly Color DefaultSelectionBackground = Color.FromRgb(51, 144, 255);
    private static readonly Color DefaultSelectionForeground = Color.White;

    private readonly UIDocument _document;
    private readonly Element _root;
    private readonly PlatformHostCreateInfo _hostCreateInfo;
    private readonly LayoutEngine _layout = new();
    private readonly DisplayTree _displayTree = new();
    private readonly Dictionary<Element, double> _scheduledFrames = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private IPlatformHost? _host;
    private IRenderContext? _renderContext;
    private UIElement? _focusedInput;
    private ITextEditor? _focusedEditor;
    private TextSelectionState? _textSelection;
    private bool _isSelectingText;
    private Element? _pointerDownTarget;
    private Element? _lastClickTarget;
    private Point _lastClickPoint;
    private double _lastClickSeconds = double.NegativeInfinity;
    private readonly List<UIElement> _hoverPath = [];
    private readonly List<UIElement> _activePath = [];
    private bool _renderRequested;
    private KeyModifiers? _toolingModifiers;

    public DesktopApplication(UIDocument document, PlatformHostCreateInfo hostCreateInfo)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(hostCreateInfo);
        _document = document;
        _root = document.DocumentElement;
        _hostCreateInfo = hostCreateInfo;
        if (!string.IsNullOrEmpty(document.Title))
            hostCreateInfo.Title = document.Title;
        else if (!string.IsNullOrEmpty(hostCreateInfo.Title))
            document.Title = hostCreateInfo.Title;
    }

    /// <summary>Compatibility: wrap a content root into a new <see cref="UIDocument"/> Body.</summary>
    public DesktopApplication(Element contentRoot, PlatformHostCreateInfo hostCreateInfo)
        : this(WrapContent(contentRoot), hostCreateInfo)
    {
    }

    public UIDocument Document => _document;
    public string RenderBackend
    {
        get => _hostCreateInfo.RenderBackend;
        set
        {
            if (IsRunning) throw new InvalidOperationException("The render backend cannot be changed while the application is running.");
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _hostCreateInfo.RenderBackend = value;
        }
    }
    public Color Background { get; set; } = Color.White;
    public RenderMode RenderingMode { get; set; } = RenderMode.FullFrame;
    public int MaxDirtyRectCount { get; set; } = 16;
    public float MaxDirtyAreaRatio { get; set; } = 0.35f;
    public bool ShowRenderDiagnosticsOverlay { get; set; }
    public bool ShowDirtyUnionOverlay { get; set; } = true;
    public RenderDiagnostics LastRenderDiagnostics { get; private set; } =
        new(RenderMode.FullFrame, true, "NotRendered", 0, 0, Rect.Empty);
    public event Action<int, KeyAction>? GlobalKeyEvent;

    private static UIDocument WrapContent(Element contentRoot)
    {
        ArgumentNullException.ThrowIfNull(contentRoot);
        var document = new UIDocument();
        document.Body.Children.Add(contentRoot);
        return document;
    }

    protected override void RunCore()
    {
        BackendRegistration.RegisterDefaults();
        Square.Controls.ControlRegistration.RegisterDefaults();

        _document.Build();
        var lifecycle = (IComponentLifecycle)_root;
        // 先注册帧调度，再 OnAttached：组件在 OnAttached 里 RequestAnimationFrame 才能被调度
        _root.AddEventListener(StandardEvents.RequestFrame, HandleFrameRequest);
        _document.AddEventListener(StandardEvents.RequestFrame, HandleFrameRequest);
        lifecycle.OnAttached();
        try
        {
            _host = PlatformRegistry.Get().CreateHost(_hostCreateInfo);
            AttachHostEvents(_host);

            _host.Show();
            _renderContext = _host.CreateRenderContext();
            lifecycle.OnLoaded();
            RenderFrame();
            _host.ShowAfterFirstFrame();
            _host.PumpEvents();
        }
        finally
        {
            if (_root.IsLoaded) lifecycle.OnUnloaded();
            lifecycle.OnDetached();
            _renderContext?.Dispose();
            _renderContext = null;
            _host?.Dispose();
            _host = null;
        }
    }

    private void AttachHostEvents(IPlatformHost host)
    {
        host.SizeChanged += _ => RenderFrame();
        host.WheelEvent += HandleWheel;
        host.MouseEvent += HandleMouse;
        host.KeyEvent += HandleKey;
        host.TextInput += HandleTextInput;
        host.Tick += HandleTick;
    }

    private void HandleFrameRequest(Event e)
    {
        // Target 为派发源；不要用 CurrentTarget（冒泡到 root 时已是 root）。
        // 只登记到期时间，不立刻 _renderRequested——否则会在每个 WM_TIMER(16ms)
        // 都做全窗口软件 Clear+Present，动画 CPU 极高。
        if (e is FrameRequestEvent args && e.Target is Element target)
        {
            var requestedTime = _clock.Elapsed.TotalSeconds + args.IntervalSeconds;
            if (!_scheduledFrames.TryGetValue(target, out var current) || requestedTime < current)
                _scheduledFrames[target] = requestedTime;
        }
        e.StopPropagation();
    }

    public void RequestRender() => _renderRequested = true;

    public void Close()
    {
        if (_host == null) return;
        if (Dispatcher.CheckAccess()) _host.Close();
        else Dispatcher.Invoke(() => _host?.Close());
    }

    public Task InjectPointerAsync(ToolingPointerInput input) => Dispatcher.InvokeAsync(() =>
    {
        WithToolingModifiers(input.Modifiers, () => HandleMouse(input.Position, input.Action));
    });

    public Task InjectKeyAsync(ToolingKeyInput input) => Dispatcher.InvokeAsync(() =>
    {
        WithToolingModifiers(input.Modifiers, () => HandleKey(input.KeyCode, input.Action));
    });

    public Task InjectTextAsync(string text) => Dispatcher.InvokeAsync(() => HandleTextInput(text ?? ""));

    public Task InjectWheelAsync(ToolingWheelInput input) => Dispatcher.InvokeAsync(() =>
    {
        WithToolingModifiers(input.Modifiers, () => HandleWheel(input.Position, input.Delta));
    });

    public Task<Bitmap> CaptureRendererBitmapAsync()
    {
        var completion = new TaskCompletionSource<Bitmap>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.Invoke(() =>
        {
            try
            {
                if (_host == null || _renderContext == null)
                    throw new InvalidOperationException("The application must be running before renderer capture is available.");

                // Prefer the live frame from the active render context. For GPU backends
                // (e.g. Vulkan) this reads back the actual presented frame, so the capture
                // reflects real GPU output instead of a software re-render — which is what
                // makes GPU-side rendering bugs visible in tooling screenshots.
                if (_renderContext is IRenderBitmapSource { IsCaptureAvailable: true } liveSource)
                {
                    completion.SetResult(liveSource.CaptureBitmap());
                    return;
                }

                // Fallback: re-render the display tree into a software capture context.
                using var captureContext = new RenderBackendFactory().CreateContext(new RenderContextCreateInfo
                {
                    CanvasSize = _host.ClientSize,
                    DpiScale = _host.DpiScale
                });
                captureContext.Clear(Background);
                _displayTree.Render(captureContext);
                RenderTextSelection(captureContext);
                RenderDiagnosticsOverlay(captureContext);
                captureContext.Flush();
                completion.SetResult(((IRenderBitmapSource)captureContext).CaptureBitmap());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        return completion.Task;
    }

    public Task<ElementInspectionSnapshot> CaptureInspectionSnapshotAsync(bool includeSourcePaths = true, bool includeTextContent = true) =>
        InvokeOnDispatcherAsync(() => new ElementInspectionSnapshot(CreateInspectionNode(_root, includeSourcePaths, includeTextContent, includeChildren: true)));

    public Task<ElementInspectionNode?> InspectElementAsync(int debugId, bool includeSourcePaths = true, bool includeTextContent = true) =>
        InvokeOnDispatcherAsync(() => FindElementByDebugId(_root, debugId) is { } element
            ? CreateInspectionNode(element, includeSourcePaths, includeTextContent, includeChildren: true)
            : null);

    public Task<ElementInspectionNode?> HitTestInspectionAsync(Point point, bool includeSourcePaths = true, bool includeTextContent = true) =>
        InvokeOnDispatcherAsync(() => HitTest(point) is { } element
            ? CreateInspectionNode(element, includeSourcePaths, includeTextContent, includeChildren: false)
            : null);

    private void RenderFrame()
    {
        _renderRequested = false;
        if (_host == null || _renderContext == null) return;

        RunUpdatePass();

        if (!string.IsNullOrEmpty(_document.Title) && _hostCreateInfo.Title != _document.Title)
        {
            _hostCreateInfo.Title = _document.Title;
            _host.Title = _document.Title;
        }

        var size = _host.ClientSize;
        var layoutDirty = _root.IsLayoutDirty || _root.Geometry.Size != size;
        if (layoutDirty)
        {
            _layout.Measure(_root, size);
            _layout.Arrange(_root, new Rect(0, 0, size.Width, size.Height));
            // Body fills client area after head (height 0 this phase)
            _document.Body.Geometry = new Rect(0, 0, size.Width, size.Height);
            _displayTree.BuildFrom(_root);
        }
        else
        {
            _displayTree.UpdateDirty();
        }

        if (RenderingMode == RenderMode.FullFrame || layoutDirty)
        {
            LastRenderDiagnostics = new RenderDiagnostics(
                RenderingMode,
                true,
                layoutDirty ? "LayoutDirty" : "ModeFullFrame",
                0,
                1f,
                new Rect(0, 0, size.Width, size.Height));
            RenderFullFrame();
        }
        else
        {
            var dirty = _displayTree.CollectDirtyRects();
            if (dirty.Count == 0)
            {
                // 无节点标脏时仍全量重绘一帧，避免“状态已变但未 InvalidatePaint”时界面卡住
                // （与脏区优化前“每次 RenderFrame 都清屏重放命令”的行为对齐）
                LastRenderDiagnostics = new RenderDiagnostics(
                    RenderingMode,
                    true,
                    "NoDirtyRects",
                    0,
                    1f,
                    new Rect(0, 0, size.Width, size.Height));
                RenderFullFrame();
            }
            else
            {
                LastRenderDiagnostics = RenderDecision.Decide(
                    RenderingMode,
                    dirty,
                    size,
                    MaxDirtyRectCount,
                    MaxDirtyAreaRatio);

                if (LastRenderDiagnostics.UsedFullFrame)
                {
                    RenderFullFrame();
                }
                else
                {
                    // 局部绘制进软件缓冲
                    _renderContext.Clear(Background, LastRenderDiagnostics.DirtyUnion);
                    _renderContext.PushClip(LastRenderDiagnostics.DirtyUnion);
                    _displayTree.Render(_renderContext, LastRenderDiagnostics.DirtyUnion);
                    RenderTextSelection(_renderContext);
                    _renderContext.PopClip();
                    RenderDiagnosticsOverlay(_renderContext);
                    _renderContext.Flush();
                    // Present：优先局部；若平台忽略则仍应更新窗口。同时提交 union 保证至少一块区域。
                    _renderContext.Present(ShowRenderDiagnosticsOverlay ? null : dirty);
                }
            }
        }

        if (_focusedEditor != null)
            _host.SetTextInputRect(MapContentRectToScreen(_focusedInput, _focusedEditor.CaretRect));
    }

    private static ElementInspectionNode CreateInspectionNode(Element element, bool includeSourcePaths, bool includeTextContent, bool includeChildren)
    {
        var children = includeChildren
            ? element.Children.Select(child => CreateInspectionNode(child, includeSourcePaths, includeTextContent, includeChildren: true)).ToArray()
            : [];
        return new ElementInspectionNode(
            element.DebugId,
            element.DebugInfo?.TagName ?? element.TagName,
            element.Id,
            element.DebugInfo?.ComponentName,
            element.Geometry,
            new ElementInspectionState(
                element.HasState(ElementState.Hover),
                element.HasState(ElementState.Focus),
                element.HasState(ElementState.Active),
                element.HasState(ElementState.Disabled)),
            CreateInspectionSource(element.DebugInfo, includeSourcePaths),
            includeTextContent ? ReadElementText(element) : null,
            element.Children.Count,
            children);
    }

    private Task<T> InvokeOnDispatcherAsync<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.Invoke(() =>
        {
            try
            {
                completion.SetResult(action());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        return completion.Task;
    }

    private static string? ReadElementText(Element element)
    {
        var textContent = element.GetProperty<string>("TextContent");
        return string.IsNullOrEmpty(textContent) ? null : textContent;
    }

    private static ElementInspectionSource? CreateInspectionSource(ElementDebugInfo? debugInfo, bool includeSourcePaths)
    {
        if (debugInfo == null) return null;
        return new ElementInspectionSource(
            debugInfo.SourceId,
            includeSourcePaths ? debugInfo.SourcePath : null,
            debugInfo.StartLine,
            debugInfo.StartColumn,
            debugInfo.EndLine,
            debugInfo.EndColumn,
            debugInfo.Kind.ToString());
    }

    private static Element? FindElementByDebugId(Element element, int debugId)
    {
        if (element.DebugId == debugId) return element;
        foreach (var child in element.Children)
        {
            var found = FindElementByDebugId(child, debugId);
            if (found != null) return found;
        }
        return null;
    }

    private bool RunUpdatePass()
    {
        var hadWork = Dispatcher.HasWork || Reconciler.Current.HasWork;
        Dispatcher.Run();
        if (Reconciler.Current.HasWork)
        {
            hadWork = true;
            Reconciler.Current.Flush();
        }
        if (CssStyleReconciler.HasWork)
        {
            hadWork = true;
            CssStyleReconciler.Flush();
        }
        return hadWork;
    }

    private Element? HitTest(Point point) => _displayTree.HitTestPopups(point) ?? _root.HitTest(point);

    private void RenderFullFrame()
    {
        if (_renderContext == null) return;
        _renderContext.Clear(Background);
        _displayTree.Render(_renderContext);
        RenderTextSelection(_renderContext);
        RenderDiagnosticsOverlay(_renderContext);
        _renderContext.Flush();
        _renderContext.Present(null);
    }

    private void RenderDiagnosticsOverlay(IRenderContext context)
    {
        if (!ShowRenderDiagnosticsOverlay) return;

        var diagnostics = LastRenderDiagnostics;
        var panel = new Rect(8, 8, 300, 86);
        context.FillRect(panel, new SolidColorBrush(Color.FromRgba(20, 24, 28, 220)));
        context.DrawRect(panel, Pen.FromColor(Color.FromRgb(80, 180, 255)));

        DrawOverlayText(context, $"mode: {diagnostics.Mode} / {(diagnostics.UsedFullFrame ? "full" : "dirty")}", 16, 16);
        DrawOverlayText(context, $"reason: {diagnostics.Reason}", 16, 34);
        DrawOverlayText(context, $"dirty: {diagnostics.DirtyRectCount}, area: {diagnostics.DirtyAreaRatio:P1}", 16, 52);
        DrawOverlayText(context, $"union: {FormatRect(diagnostics.DirtyUnion)}", 16, 70);

        if (ShowDirtyUnionOverlay && !diagnostics.DirtyUnion.IsEmpty)
            context.DrawRect(diagnostics.DirtyUnion, Pen.FromColor(Color.FromRgba(255, 64, 64, 220), 2));
    }

    private static void DrawOverlayText(IRenderContext context, string text, float x, float y)
    {
        context.DrawText(
            new TextLayout(text, new Font("Segoe UI", 12)),
            new Point(x, y),
            new SolidColorBrush(Color.White));
    }

    private static string FormatRect(Rect rect) => rect.IsEmpty
        ? "empty"
        : $"{rect.X:0},{rect.Y:0} {rect.Width:0}x{rect.Height:0}";

    private void HandleWheel(Point point, int delta)
    {
        var hit = HitTest(point);
        if (UpdateHoverPath(hit)) RequestRender();
        hit?.DispatchTrusted(StandardEvents.CreateWheel(0, -delta));
        RenderFrame();
    }

    private void HandleMouse(Point point, MouseAction action)
    {
        if (_host == null) return;

        var hit = HitTest(point);
        if (action == MouseAction.Down && _displayTree.DismissPopupsOutside(point))
            RequestRender();
        if (action == MouseAction.Move)
        {
            var needsRender = UpdateHoverPath(hit);
            _host.Cursor = ResolveCursor(hit);
            if (_isSelectingText && _focusedEditor != null)
            {
                _focusedEditor.HandlePointerMove(MapPointerPoint(_focusedInput, point));
                needsRender = true;
            }
            else if (_textSelection is { IsSelecting: true } selection)
            {
                needsRender |= UpdateTextSelection(selection, point);
            }
            foreach (var select in _root.QueryAll<Select>())
                needsRender |= select.HandlePointerMove(point);
            if (needsRender) RequestRender();
            return;
        }

        if (action == MouseAction.Up)
        {
            if (_isSelectingText && _focusedEditor != null)
            {
                _focusedEditor.HandlePointerUp(MapPointerPoint(_focusedInput, point));
                _isSelectingText = false;
            }
            if (_textSelection is { IsSelecting: true } selection)
            {
                UpdateTextSelection(selection, point);
                selection.IsSelecting = false;
                SyncDocumentSelection(selection);
            }
            if (_pointerDownTarget != null && hit == _pointerDownTarget)
                hit?.DispatchTrusted(StandardEvents.CreateClick());
            _pointerDownTarget = null;
            ClearActivePath();
            RenderFrame();
            return;
        }

        if (action != MouseAction.Down) return;

        var elapsed = _clock.Elapsed.TotalSeconds - _lastClickSeconds;
        var deltaX = point.X - _lastClickPoint.X;
        var deltaY = point.Y - _lastClickPoint.Y;
        var isDoubleClick = ReferenceEquals(hit, _lastClickTarget) && elapsed <= 0.5 &&
            deltaX * deltaX + deltaY * deltaY <= 25;
        _lastClickTarget = isDoubleClick ? null : hit;
        _lastClickPoint = point;
        _lastClickSeconds = _clock.Elapsed.TotalSeconds;
        _pointerDownTarget = hit;
        UpdateHoverPath(hit);
        UpdateActivePath(hit);
        hit?.DispatchTrusted(StandardEvents.CreatePointerDown());
        UpdateFocus(hit, point, isDoubleClick);

        if (hit is Select selected) selected.HandlePointerDown(point);
        RenderFrame();
    }

    private void UpdateFocus(Element? hit, Point point, bool selectWord)
    {
        if (_host == null) return;

        var focusTarget = FindFocusableAncestor(hit);

        if (_focusedInput != focusTarget)
        {
            _focusedInput?.Unfocus();
            _focusedInput = focusTarget;
            _focusedEditor = focusTarget as ITextEditor;
            _focusedInput?.Focus();
        }

        if (hit is ITextEditor editor && hit is UIElement editorElement)
        {
            ClearDocumentSelection();
            var editorPoint = MapPointerPoint(editorElement, point);
            editor.HandlePointerDown(editorPoint, CurrentModifiers.HasFlag(KeyModifiers.Shift));
            if (selectWord) editor.SelectWordAt(editorPoint);
            _isSelectingText = true;
            return;
        }

        if (TryStartTextSelection(hit, point, selectWord))
        {
            _focusedInput?.Unfocus();
            _focusedInput = null;
            _focusedEditor = null;
            _isSelectingText = false;
            return;
        }

        _isSelectingText = false;
        ClearDocumentSelection();
    }

    private void ClearDocumentSelection()
    {
        if (_textSelection == null && _document.GetSelection().RangeCount == 0) return;
        _textSelection = null;
        _document.GetSelection().RemoveAllRanges();
        RequestRender();
    }

    private static bool IsFocusable(UIElement element) => element.IsEnabled &&
        (element is ITextEditor or Button or CheckBox or Radio or Select or Link);

    private static Point MapPointerPoint(Element? target, Point point)
    {
        for (var current = target?.Parent; current != null; current = current.Parent)
            if (current is IPopupElement popup) return popup.MapPointToContent(point);
        return point;
    }

    private static Rect MapContentRectToScreen(Element? target, Rect rect)
    {
        for (var current = target?.Parent; current != null; current = current.Parent)
        {
            if (current is not IPopupElement popup) continue;
            var origin = popup.MapPointToContent(Point.Zero);
            return rect.Offset(-origin.X, -origin.Y);
        }
        return rect;
    }

    private static UIElement? FindFocusableAncestor(Element? hit)
    {
        for (var current = hit; current != null; current = current.Parent)
            if (current is UIElement element && IsFocusable(element))
                return element;
        return null;
    }

    private static CursorKind ResolveCursor(Element? hit) => hit is ITextEditor || FindUserSelectRoot(hit) != null ? CursorKind.Text : CursorKind.Arrow;

    private bool UpdateHoverPath(Element? hit) => UpdateStatePath(_hoverPath, hit, ElementState.Hover);

    private bool UpdateActivePath(Element? hit) => UpdateStatePath(_activePath, hit, ElementState.Active);

    private bool ClearActivePath() => UpdateStatePath(_activePath, null, ElementState.Active);

    private static bool UpdateStatePath(List<UIElement> currentPath, Element? hit, ElementState state)
    {
        var nextPath = BuildElementPath(hit);
        if (PathsEqual(currentPath, nextPath)) return false;

        foreach (var element in currentPath)
        {
            if (!nextPath.Contains(element))
                element.SetState(state, false);
        }

        for (var i = nextPath.Count - 1; i >= 0; i--)
        {
            if (!currentPath.Contains(nextPath[i]))
                nextPath[i].SetState(state, true);
        }

        currentPath.Clear();
        currentPath.AddRange(nextPath);
        return true;
    }

    private static List<UIElement> BuildElementPath(Element? hit)
    {
        var path = new List<UIElement>();
        for (var current = hit; current != null; current = current.Parent)
            if (current is UIElement uiElement)
                path.Add(uiElement);
        return path;
    }

    private static bool PathsEqual(List<UIElement> left, List<UIElement> right)
    {
        if (left.Count != right.Count) return false;
        for (var i = 0; i < left.Count; i++)
            if (!ReferenceEquals(left[i], right[i]))
                return false;
        return true;
    }

    private void HandleKey(int keyCode, KeyAction action)
    {
        if (_host == null) return;

        GlobalKeyEvent?.Invoke(keyCode, action);

        var shift = CurrentModifiers.HasFlag(KeyModifiers.Shift);
        var control = CurrentModifiers.HasFlag(KeyModifiers.Control);
        var alt = CurrentModifiers.HasFlag(KeyModifiers.Alt);

        if (action == KeyAction.Down && _displayTree.HandlePopupKey(keyCode, shift, control, alt))
        {
            RenderFrame();
            return;
        }

        if (action == KeyAction.Down && keyCode == 27 && _displayTree.DismissTopmostPopupOnEscape())
        {
            SyncFocusedInputFromTree();
            RenderFrame();
            return;
        }

        SyncFocusedInputFromTree();

        _focusedInput?.DispatchTrusted(
            action == KeyAction.Down
                ? StandardEvents.CreateKeyDown(keyCode, shift, control, alt)
                : StandardEvents.CreateKeyUp(keyCode, shift, control, alt));
        if (action != KeyAction.Down) return;

        if (_focusedEditor == null)
        {
            if (control && keyCode == 67)
            {
                var text = GetSelectedUserText();
                if (!string.IsNullOrEmpty(text)) _host.SetClipboardText(text);
            }
            RenderFrame();
            return;
        }

        if (control && keyCode == 67)
        {
            if (_focusedEditor.CanCopySelection && _focusedEditor.SelectionLength > 0)
                _host.SetClipboardText(_focusedEditor.SelectedText);
        }
        else if (control && keyCode == 88)
        {
            if (_focusedEditor.CanCutSelection && _focusedEditor.SelectionLength > 0)
            {
                _host.SetClipboardText(_focusedEditor.SelectedText);
                _focusedEditor.HandleKey(keyCode, shift, control);
            }
        }
        else if (control && keyCode == 86)
        {
            var text = _host.GetClipboardText();
            if (!string.IsNullOrEmpty(text))
                _focusedEditor.HandleTextInput(text);
        }
        else
        {
            _focusedEditor.HandleKey(keyCode, shift, control);
        }
        RenderFrame();
    }

    private void SyncFocusedInputFromTree()
    {
        var focused = _root.QueryAll<UIElement>().LastOrDefault(element => element.IsFocused);
        if (ReferenceEquals(_focusedInput, focused)) return;
        _focusedInput = focused;
        _focusedEditor = focused as ITextEditor;
    }

    private void HandleTextInput(string text)
    {
        _focusedEditor?.HandleTextInput(text);
        RenderFrame();
    }

    private KeyModifiers CurrentModifiers => _toolingModifiers ?? _host?.Modifiers ?? KeyModifiers.None;

    private void WithToolingModifiers(KeyModifiers modifiers, Action action)
    {
        var previous = _toolingModifiers;
        _toolingModifiers = modifiers;
        try
        {
            action();
        }
        finally
        {
            _toolingModifiers = previous;
        }
    }

    private bool TryStartTextSelection(Element? hit, Point point, bool selectWord = false)
    {
        var root = FindUserSelectRoot(hit);
        if (root == null) return false;

        var selection = new TextSelectionState(root, CollectSelectableText(root));
        var selectionPoint = FindTextSelectionPoint(selection, hit, point);
        if (selectionPoint.Index < 0) return false;

        selection.Anchor = selectionPoint;
        selection.Focus = selectionPoint;
        if (selectWord)
        {
            var item = selection.Items[selectionPoint.Index];
            var (start, end) = FindDocumentWordAt(item.Text, selectionPoint.Offset);
            selection.Anchor = new TextSelectionPoint(selectionPoint.Index, start);
            selection.Focus = new TextSelectionPoint(selectionPoint.Index, end);
        }
        selection.IsSelecting = true;
        _textSelection = selection;
        SyncDocumentSelection(selection);
        RequestRender();
        return true;
    }

    private static (int Start, int End) FindDocumentWordAt(string text, int offset)
    {
        if (text.Length == 0) return (0, 0);
        var index = Math.Clamp(offset, 0, text.Length - 1);
        if (!char.IsLetterOrDigit(text[index]) && text[index] != '_') return (index, index + 1);
        var start = index;
        var end = index + 1;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_')) start--;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
        return (start, end);
    }

    private bool UpdateTextSelection(TextSelectionState selection, Point point)
    {
        var selectionPoint = FindTextSelectionPoint(selection, HitTest(point), point);
        if (selectionPoint.Index < 0 || selectionPoint == selection.Focus) return false;
        selection.Focus = selectionPoint;
        SyncDocumentSelection(selection);
        RequestRender();
        return true;
    }

    private static Element? FindUserSelectRoot(Element? element)
    {
        Element? candidate = null;
        for (var current = element; current != null; current = current.Parent)
        {
            var value = current.Style.Get("user-select")?.Trim();
            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) return null;
            if (string.Equals(value, "text", StringComparison.OrdinalIgnoreCase)) candidate = current;
        }

        return candidate;
    }

    private List<TextSelectionItem> CollectSelectableText(Element root)
    {
        var items = new List<TextSelectionItem>();
        var fragmentsByElement = _displayTree.CollectTextFragments(root)
            .GroupBy(fragment => fragment.Element)
            .ToDictionary(group => group.Key, group => group.ToList());
        CollectSelectableText(root, items, fragmentsByElement);
        return items;
    }

    private static void CollectSelectableText(Element element, List<TextSelectionItem> items, Dictionary<Element, List<TextFragment>> fragmentsByElement)
    {
        if (!element.IsVisible || !element.IsUserSelectText()) return;
        var selectableStart = items.Count;
        if (fragmentsByElement.TryGetValue(element, out var fragments))
        {
            foreach (var fragment in fragments)
                items.Add(new TextSelectionItem(element, fragment.Text, fragment.Bounds, fragment));
        }
        else if (element is ITextSelectable selectable && !string.IsNullOrEmpty(selectable.SelectableText))
            items.Add(new TextSelectionItem(element, selectable.SelectableText, selectable.SelectableTextBounds, null));
        foreach (var child in element.Children)
            CollectSelectableText(child, items, fragmentsByElement);
        if (items.Count > selectableStart + 1 && element is ITextSelectable)
            items.RemoveAt(selectableStart);
    }

    private static TextSelectionPoint FindTextSelectionPoint(TextSelectionState selection, Element? hit, Point point)
    {
        for (var current = hit; current != null; current = current.Parent)
        {
            var direct = selection.Items.FindLastIndex(item => ReferenceEquals(item.Element, current) && !item.Bounds.IsEmpty && item.Bounds.Contains(point));
            if (direct >= 0) return CreateSelectionPoint(selection.Items[direct], direct, point);
        }

        var containing = selection.Items
            .Select((item, index) => (item, index))
            .Where(pair => !pair.item.Bounds.IsEmpty && pair.item.Bounds.Contains(point))
            .OrderBy(pair => pair.item.Bounds.Width * pair.item.Bounds.Height)
            .Select(pair => pair.index)
            .FirstOrDefault(-1);
        if (containing >= 0) return CreateSelectionPoint(selection.Items[containing], containing, point);
        if (selection.Items.Count == 0) return new TextSelectionPoint(-1, 0);

        var bestIndex = -1;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < selection.Items.Count; i++)
        {
            var bounds = selection.Items[i].Bounds;
            var dy = point.Y < bounds.Top ? bounds.Top - point.Y : point.Y > bounds.Bottom ? point.Y - bounds.Bottom : 0;
            var dx = point.X < bounds.Left ? bounds.Left - point.X : point.X > bounds.Right ? point.X - bounds.Right : 0;
            var distance = dx * dx + dy * dy;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            bestIndex = i;
        }

        return bestIndex < 0
            ? new TextSelectionPoint(-1, 0)
            : CreateSelectionPoint(selection.Items[bestIndex], bestIndex, point);
    }

    private static TextSelectionPoint CreateSelectionPoint(TextSelectionItem item, int index, Point point)
    {
        if (item.Fragment != null)
            return new TextSelectionPoint(index, item.Fragment.HitTestOffset(point));
        var midpoint = item.Bounds.X + item.Bounds.Width / 2f;
        return new TextSelectionPoint(index, point.X < midpoint ? 0 : item.Text.Length);
    }

    private string GetSelectedUserText()
    {
        var documentSelectionText = _document.GetSelection().ToString();
        if (!string.IsNullOrEmpty(documentSelectionText)) return documentSelectionText;

        if (_textSelection == null || _textSelection.Items.Count == 0) return "";
        var (start, end) = GetOrderedSelectionPoints(_textSelection);
        if (start.Index < 0 || end.Index < 0) return "";
        if (start.Index == end.Index)
        {
            var item = _textSelection.Items[start.Index];
            return start.Offset == end.Offset ? "" : item.Text[start.Offset..end.Offset];
        }

        var selected = new List<string>();
        for (var i = start.Index; i <= end.Index; i++)
        {
            var item = _textSelection.Items[i];
            if (i == start.Index) selected.Add(item.Text[start.Offset..]);
            else if (i == end.Index) selected.Add(item.Text[..end.Offset]);
            else selected.Add(item.Text);
        }
        return string.Join(Environment.NewLine, selected.Where(text => text.Length > 0));
    }

    private void SyncDocumentSelection(TextSelectionState selection)
    {
        var documentSelection = _document.GetSelection();
        documentSelection.RemoveAllRanges();
        if (selection.Items.Count == 0) return;

        var (startPoint, endPoint) = GetOrderedSelectionPoints(selection);
        var start = startPoint.Index;
        var end = endPoint.Index;
        if (start < 0 || end < 0) return;
        var startItem = selection.Items[start];
        var endItem = selection.Items[end];
        var startElement = startItem.Element;
        var endElement = endItem.Element;
        if (startElement.OwnerDocument != _document || endElement.OwnerDocument != _document) return;

        var range = _document.CreateRange();
        if (TryGetTextNodeForSelectionItem(startItem, out var startText) &&
            TryGetTextNodeForSelectionItem(endItem, out var endText))
        {
            range.SetStart(startText, Math.Clamp(startPoint.Offset, 0, startText.Length));
            range.SetEnd(endText, Math.Clamp(endPoint.Offset, 0, endText.Length));
        }
        else
        {
            range.SetStart(startElement, 0);
            range.SetEnd(endElement, endElement.ChildNodes.Count);
        }
        documentSelection.AddRange(range);
    }

    private static bool TryGetTextNodeForSelectionItem(TextSelectionItem item, out Square.UI.Text textNode)
    {
        var match = item.Element.ChildNodes.OfType<Square.UI.Text>().FirstOrDefault(node => node.Data == item.Text)
            ?? item.Element.ChildNodes.OfType<Square.UI.Text>().FirstOrDefault();
        if (match == null)
        {
            textNode = null!;
            return false;
        }

        textNode = match;
        return true;
    }

    private void RenderTextSelection(IRenderContext context)
    {
        if (_textSelection == null || _textSelection.Items.Count == 0) return;
        var (startPoint, endPoint) = GetOrderedSelectionPoints(_textSelection);
        if (startPoint.Index < 0 || endPoint.Index < 0) return;
        for (var i = startPoint.Index; i <= endPoint.Index; i++)
        {
            var item = _textSelection.Items[i];
            var startOffset = i == startPoint.Index ? startPoint.Offset : 0;
            var endOffset = i == endPoint.Index ? endPoint.Offset : item.Text.Length;
            if (startOffset == endOffset) continue;
            var background = ResolveSelectionColor(item.Element, foreground: false);
            var foreground = ResolveSelectionColor(item.Element, foreground: true);
            var backgroundBrush = new SolidColorBrush(background);
            var foregroundBrush = new SolidColorBrush(foreground);
            if (item.Fragment == null)
            {
                context.FillRect(item.Bounds, backgroundBrush);
                continue;
            }

            foreach (var character in item.Fragment.Characters)
            {
                if (character.EndOffset <= startOffset || character.StartOffset >= endOffset) continue;
                context.FillRect(character.SelectionBounds, backgroundBrush);
                var selectedText = item.Text[character.StartOffset..character.EndOffset];
                context.DrawText(
                    new TextLayout(selectedText, item.Fragment.Font),
                    character.Bounds.Position,
                    foregroundBrush);
            }
        }
    }

    private static Color ResolveSelectionColor(Element element, bool foreground)
    {
        var value = foreground
            ? FindStyleInPath(element, "selection-color")
            : FindStyleInPath(element, "selection-background") ?? FindStyleInPath(element, "selection-background-color");
        if (!string.IsNullOrWhiteSpace(value))
        {
            try { return Color.Parse(value.Replace(" ", "")); }
            catch (FormatException) { }
        }
        return foreground ? DefaultSelectionForeground : DefaultSelectionBackground;
    }

    private static string? FindStyleInPath(Element element, string property)
    {
        for (var current = element; current != null; current = current.Parent)
        {
            var value = current.Style.Get(property);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    private static (TextSelectionPoint Start, TextSelectionPoint End) GetOrderedSelectionPoints(TextSelectionState selection)
    {
        var anchor = selection.Anchor;
        var focus = selection.Focus;
        if (anchor.Index < focus.Index || anchor.Index == focus.Index && anchor.Offset <= focus.Offset)
            return (anchor, focus);
        return (focus, anchor);
    }

    private sealed class TextSelectionState(Element root, List<TextSelectionItem> items)
    {
        public Element Root { get; } = root;
        public List<TextSelectionItem> Items { get; } = items;
        public TextSelectionPoint Anchor { get; set; }
        public TextSelectionPoint Focus { get; set; }
        public bool IsSelecting { get; set; }
    }

    private readonly record struct TextSelectionItem(Element Element, string Text, Rect Bounds, TextFragment? Fragment);

    private readonly record struct TextSelectionPoint(int Index, int Offset);

    private void HandleTick()
    {
        var now = _clock.Elapsed.TotalSeconds;
        // 避免每 tick 分配 LINQ 数组
        List<Element>? dueTargets = null;
        foreach (var pair in _scheduledFrames)
        {
            if (now < pair.Value) continue;
            dueTargets ??= [];
            dueTargets.Add(pair.Key);
        }
        if (dueTargets != null)
        {
            foreach (var target in dueTargets)
            {
                _scheduledFrames.Remove(target);
                target.InvalidatePaint();
            }
        }

        var needsRender = (dueTargets != null && dueTargets.Count > 0)
            || _renderRequested
            || Reconciler.Current.HasWork
            || CssStyleReconciler.HasWork
            || Dispatcher.HasWork;
        if (_focusedEditor?.ToggleCaretBlink() == true) needsRender = true;
        if (needsRender) RenderFrame();
    }
}
