using System.Diagnostics;
using Square.Backends;
using Square.Controls.Controls;
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

    private void RequestRender() => _renderRequested = true;

    private void RenderFrame()
    {
        _renderRequested = false;
        if (_host == null || _renderContext == null) return;

        if (!string.IsNullOrEmpty(_document.Title) && _hostCreateInfo.Title != _document.Title)
        {
            // Title sync for platforms that read create-info; host may already be open.
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
            RenderFullFrame();
        }
        else
        {
            _displayTree.UpdateDirty();
            var dirty = _displayTree.CollectDirtyRects();
            if (dirty.Count == 0)
            {
                // 无节点标脏时仍全量重绘一帧，避免“状态已变但未 InvalidatePaint”时界面卡住
                // （与脏区优化前“每次 RenderFrame 都清屏重放命令”的行为对齐）
                RenderFullFrame();
            }
            else
            {
                var clientArea = Math.Max(1f, size.Width * size.Height);
                var dirtyArea = 0f;
                foreach (var r in dirty) dirtyArea += DisplayTree.Area(r);
                if (dirtyArea / clientArea > 0.45f)
                {
                    RenderFullFrame();
                }
                else
                {
                    var union = dirty[0];
                    for (var i = 1; i < dirty.Count; i++)
                        union = DisplayTree.Union(union, dirty[i]);
                    // 局部绘制进软件缓冲
                    _renderContext.Clear(Background, union);
                    _renderContext.PushClip(union);
                    _displayTree.Render(_renderContext, union);
                    _renderContext.PopClip();
                    _renderContext.Flush();
                    // Present：优先局部；若平台忽略则仍应更新窗口。同时提交 union 保证至少一块区域。
                    _renderContext.Present(dirty);
                }
            }
        }

        if (_focusedEditor != null) _host.SetTextInputRect(_focusedEditor.CaretRect);
    }

    private void RenderFullFrame()
    {
        if (_renderContext == null) return;
        _renderContext.Clear(Background);
        _displayTree.Render(_renderContext);
        _renderContext.Flush();
        _renderContext.Present(null);
    }

    private void HandleWheel(Point point, int delta)
    {
        _root.HitTest(point)?.DispatchTrusted(StandardEvents.CreateWheel());
        RenderFrame();
    }

    private void HandleMouse(Point point, MouseAction action)
    {
        if (_host == null) return;

        var hit = _root.HitTest(point);
        if (action == MouseAction.Move)
        {
            _host.Cursor = hit is ITextEditor ? CursorKind.Text : CursorKind.Arrow;
            var needsRender = _isSelectingText && _focusedEditor != null;
            if (needsRender) _focusedEditor!.HandlePointerMove(point);
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
            RenderFrame();
            return;
        }

        if (action != MouseAction.Down) return;

        _pointerDownTarget = hit;
        hit?.DispatchTrusted(StandardEvents.CreatePointerDown());
        UpdateTextFocus(hit, point);

        foreach (var select in _root.QueryAll<Select>())
            if (hit != select) select.CloseDropDown();

        if (hit is Select selected) selected.HandlePointerDown(point);
        RenderFrame();
    }

    private void UpdateTextFocus(Element? hit, Point point)
    {
        if (_host == null) return;

        if (hit is ITextEditor editor && hit is UIElement editorElement)
        {
            if (_focusedInput != editorElement)
            {
                _focusedInput?.Unfocus();
                _focusedInput = editorElement;
                _focusedEditor = editor;
                _focusedInput.Focus();
            }
            editor.HandlePointerDown(point, _host.Modifiers.HasFlag(KeyModifiers.Shift));
            _isSelectingText = true;
            return;
        }

        _focusedInput?.Unfocus();
        _focusedInput = null;
        _focusedEditor = null;
        _isSelectingText = false;
    }

    private void HandleKey(int keyCode, KeyAction action)
    {
        if (_host == null) return;

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

        // Reconciler flush：在布局/绘制前统一处理批量结构更新
        var reconcilerHadWork = Reconciler.Current.HasWork;
        if (reconcilerHadWork)
            Reconciler.Current.Flush();

        var needsRender = (dueTargets != null && dueTargets.Count > 0)
            || _renderRequested
            || reconcilerHadWork
            || Dispatcher.HasWork;
        Dispatcher.Run();
        if (_focusedEditor?.ToggleCaretBlink() == true) needsRender = true;
        if (needsRender) RenderFrame();
    }
}
