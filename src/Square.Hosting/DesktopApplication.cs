using System.Diagnostics;
using Square.Backends;
using Square.Controls.Controls;
using Square.Events;
using Square.Graphics;
using Square.Rendering;
using Square.Platform;
using Square.Runtime;
using Square.UI;

namespace Square.Hosting;

public sealed class DesktopApplication : Application
{
    private readonly Visual _root;
    private readonly PlatformHostCreateInfo _hostCreateInfo;
    private readonly LayoutEngine _layout = new();
    private readonly RenderTree _renderTree = new();
    private readonly Dictionary<Visual, double> _scheduledFrames = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private IPlatformHost? _host;
    private IRenderContext? _renderContext;
    private UIElement? _focusedInput;
    private ITextEditor? _focusedEditor;
    private bool _isSelectingText;

    public DesktopApplication(Visual root, PlatformHostCreateInfo hostCreateInfo)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(hostCreateInfo);
        _root = root;
        _hostCreateInfo = hostCreateInfo;
    }

    public Color Background { get; set; } = Color.White;

    protected override void RunCore()
    {
        BackendRegistration.RegisterDefaults();
        PlatformRegistration.RegisterDefaults();

        _root.BuildVisualTree();
        var lifecycle = (IComponentLifecycle)_root;
        lifecycle.OnAttached();
        try
        {
            _host = PlatformRegistry.Get().CreateHost(_hostCreateInfo);
            AttachHostEvents(_host);
            _root.AddEventListener(StandardEvents.RequestFrame, HandleFrameRequest);

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

    private void HandleFrameRequest(object? sender, FrameRequestEventArgs args)
    {
        if (args.OriginalSource is Visual target)
        {
            var requestedTime = _clock.Elapsed.TotalSeconds + args.IntervalSeconds;
            if (!_scheduledFrames.TryGetValue(target, out var current) || requestedTime < current)
                _scheduledFrames[target] = requestedTime;
        }
        args.Handled = true;
    }

    private bool _renderRequested;

    private void RequestRender()
    {
        _renderRequested = true;
    }

    private void RenderFrame()
    {
        _renderRequested = false;
        if (_host == null || _renderContext == null) return;

        var size = _host.ClientSize;
        if (_root.IsLayoutDirty || _root.Geometry.Size != size)
        {
            _layout.Measure(_root, size);
            _layout.Arrange(_root, new Rect(0, 0, size.Width, size.Height));
            _renderTree.BuildFrom(_root);
        }
        else
        {
            _renderTree.UpdateDirty();
        }

        _renderContext.Clear(Background);
        _renderTree.Render(_renderContext);
        _renderContext.Flush();
        _renderContext.Present();
        if (_focusedEditor != null) _host.SetTextInputRect(_focusedEditor.CaretRect);
    }

    private void HandleWheel(Point point, int delta)
    {
        _root.HitTest(point)?.RaiseEvent(StandardEvents.Wheel, new RoutedEventArgs());
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
                hit?.RaiseEvent(StandardEvents.Click, new RoutedEventArgs());
            _pointerDownTarget = null;
            RenderFrame();
            return;
        }

        if (action != MouseAction.Down) return;

        _pointerDownTarget = hit;
        hit?.RaiseEvent(StandardEvents.PointerDown, new RoutedEventArgs());
        UpdateTextFocus(hit, point);

        foreach (var select in _root.QueryAll<Select>())
            if (hit != select) select.CloseDropDown();

        if (hit is Select selected) selected.HandlePointerDown(point);
        RenderFrame();
    }

    private Visual? _pointerDownTarget;

    private void UpdateTextFocus(Visual? hit, Point point)
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

        var keyEvent = action == KeyAction.Down ? StandardEvents.KeyDown : StandardEvents.KeyUp;
        _focusedInput?.RaiseEvent(keyEvent, new RoutedEventArgs());
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
                _focusedEditor.DeleteSelection();
            }
        }
        else if (control && keyCode == 86)
        {
            _focusedEditor.HandleTextInput(_host.GetClipboardText());
        }
        else if (control && keyCode == 65)
        {
            _focusedEditor.SelectAll();
        }
        else if (control || keyCode is 8 or 13 or 35 or 36 or 37 or 38 or 39 or 40 or 46)
        {
            _focusedEditor.HandleKey(keyCode, shift, control);
        }
        else
        {
            return;
        }
        RenderFrame();
    }

    private void HandleTextInput(string text)
    {
        if (_focusedEditor == null) return;
        _focusedEditor.HandleTextInput(text);
        RenderFrame();
    }

    private void HandleTick()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dueTargets = _scheduledFrames
            .Where(pair => now >= pair.Value)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var target in dueTargets)
        {
            _scheduledFrames.Remove(target);
            target.InvalidateVisual();
        }

        var needsRender = Dispatcher.HasWork || dueTargets.Length > 0 || _renderRequested;
        Dispatcher.Run();
        if (_focusedEditor?.ToggleCaretBlink() == true) needsRender = true;
        if (needsRender) RenderFrame();
    }
}
