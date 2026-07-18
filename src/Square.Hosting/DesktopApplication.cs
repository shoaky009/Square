using System.Diagnostics;
using Square.Backends;
using Square.Controls.Controls;
using Square.CSS.Engine;
using Square.Events;
using Square.Graphics;
using Square.Rendering;
using Square.Platform;
using Square.Runtime;
using Square.UI;
using Reconciler = Square.UI.Reconciler;

namespace Square.Hosting;

public sealed class DesktopApplication : Application
{
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
    private bool _isSelectingText;
    private Element? _pointerDownTarget;
    private readonly List<UIElement> _hoverPath = [];
    private readonly List<UIElement> _activePath = [];
    private bool _renderRequested;

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
        PlatformRegistration.RegisterDefaults();
        Square.Controls.Registration.ControlRegistration.RegisterDefaults();

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
            _host.PumpEvents();
        }
        finally
        {
            if (_root.IsLoaded) lifecycle.OnUnloaded();
            lifecycle.OnDetached();
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
                    _renderContext.PopClip();
                    RenderDiagnosticsOverlay();
                    _renderContext.Flush();
                    // Present：优先局部；若平台忽略则仍应更新窗口。同时提交 union 保证至少一块区域。
                    _renderContext.Present(ShowRenderDiagnosticsOverlay ? null : dirty);
                }
            }
        }

        if (_focusedEditor != null) _host.SetTextInputRect(_focusedEditor.CaretRect);
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
        RenderDiagnosticsOverlay();
        _renderContext.Flush();
        _renderContext.Present(null);
    }

    private void RenderDiagnosticsOverlay()
    {
        if (!ShowRenderDiagnosticsOverlay || _renderContext == null) return;

        var diagnostics = LastRenderDiagnostics;
        var panel = new Rect(8, 8, 300, 86);
        _renderContext.FillRect(panel, new SolidColorBrush(Color.FromRgba(20, 24, 28, 220)));
        _renderContext.DrawRect(panel, Pen.FromColor(Color.FromRgb(80, 180, 255)));

        DrawOverlayText($"mode: {diagnostics.Mode} / {(diagnostics.UsedFullFrame ? "full" : "dirty")}", 16, 16);
        DrawOverlayText($"reason: {diagnostics.Reason}", 16, 34);
        DrawOverlayText($"dirty: {diagnostics.DirtyRectCount}, area: {diagnostics.DirtyAreaRatio:P1}", 16, 52);
        DrawOverlayText($"union: {FormatRect(diagnostics.DirtyUnion)}", 16, 70);

        if (ShowDirtyUnionOverlay && !diagnostics.DirtyUnion.IsEmpty)
            _renderContext.DrawRect(diagnostics.DirtyUnion, Pen.FromColor(Color.FromRgba(255, 64, 64, 220), 2));
    }

    private void DrawOverlayText(string text, float x, float y)
    {
        _renderContext!.DrawText(
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
        hit?.DispatchTrusted(StandardEvents.CreateWheel());
        RenderFrame();
    }

    private void HandleMouse(Point point, MouseAction action)
    {
        if (_host == null) return;

        var hit = HitTest(point);
        if (action == MouseAction.Move)
        {
            var needsRender = UpdateHoverPath(hit);
            _host.Cursor = ResolveCursor(hit);
            if (_isSelectingText && _focusedEditor != null)
            {
                _focusedEditor.HandlePointerMove(point);
                needsRender = true;
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
                _focusedEditor.HandlePointerUp(point);
                _isSelectingText = false;
            }
            if (_pointerDownTarget != null && hit == _pointerDownTarget)
                hit?.DispatchTrusted(StandardEvents.CreateClick());
            _pointerDownTarget = null;
            ClearActivePath();
            RenderFrame();
            return;
        }

        if (action != MouseAction.Down) return;

        _pointerDownTarget = hit;
        UpdateHoverPath(hit);
        UpdateActivePath(hit);
        hit?.DispatchTrusted(StandardEvents.CreatePointerDown());
        UpdateFocus(hit, point);

        foreach (var select in _root.QueryAll<Select>())
            if (hit != select) select.CloseDropDown();

        if (hit is Select selected) selected.HandlePointerDown(point);
        RenderFrame();
    }

    private void UpdateFocus(Element? hit, Point point)
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
            editor.HandlePointerDown(point, _host.Modifiers.HasFlag(KeyModifiers.Shift));
            _isSelectingText = true;
            return;
        }

        _isSelectingText = false;
    }

    private static bool IsFocusable(UIElement element) => element.IsEnabled && element is
        ITextEditor or Button or CheckBox or Radio or Select or Link;

    private static UIElement? FindFocusableAncestor(Element? hit)
    {
        for (var current = hit; current != null; current = current.Parent)
            if (current is UIElement element && IsFocusable(element))
                return element;
        return null;
    }

    private static CursorKind ResolveCursor(Element? hit) => hit is ITextEditor ? CursorKind.Text : CursorKind.Arrow;

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

        _focusedInput?.DispatchTrusted(
            action == KeyAction.Down ? StandardEvents.CreateKeyDown() : StandardEvents.CreateKeyUp());
        if (action != KeyAction.Down || _focusedEditor == null) return;

        var shift = _host.Modifiers.HasFlag(KeyModifiers.Shift);
        var control = _host.Modifiers.HasFlag(KeyModifiers.Control);
        if (control && keyCode == 67)
        {
            if (_focusedEditor.SelectionLength > 0)
                _host.SetClipboardText(_focusedEditor.SelectedText);
        }
        else if (control && keyCode == 88)
        {
            if (_focusedEditor.SelectionLength > 0)
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

    private void HandleTextInput(string text)
    {
        _focusedEditor?.HandleTextInput(text);
        RenderFrame();
    }

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
