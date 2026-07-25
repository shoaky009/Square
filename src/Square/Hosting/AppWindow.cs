using Square.Graphics;
using Square.Platform;
using Square.Rendering;
using Square.Runtime;
using Square.Runtime.State;
using Square.UI;

namespace Square.Hosting;

/// <summary>A stable, dispatcher-aware facade over the native application window.</summary>
public sealed class AppWindow : IRenderBackendApplication
{
    private readonly object _gate = new();
    private Dispatcher _dispatcher;
    private IAppWindowRuntime? _runtime;
    private bool _applicationBound;
    private IPlatformHost? _host;
    private readonly UIDocument _document;
    private readonly int _initialWidth;
    private readonly int _initialHeight;
    private string _title;
    private Size _clientSize;
    private float _dpiScale = 1f;
    private AppWindowState _state;
    private bool _isClosed;
    private bool _closeRequested;
    private Action<object?>? _dialogCompletion;
    private object? _dialogResult;
    private bool _hasDialogResult;

    public AppWindow(string title, int width = 800, int height = 600)
    {
        ArgumentNullException.ThrowIfNull(title);
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        _title = title;
        _initialWidth = width;
        _initialHeight = height;
        _clientSize = new Size(width, height);
        _document = new UIDocument { Title = title };
        _document.AppWindow = this;
        _dispatcher = _document.Context.Dispatcher;
    }

    public string Title
    {
        get
        {
            lock (_gate) return _title;
        }
        set
        {
            value ??= "";
            lock (_gate) _title = value;
            _document.Title = value;
            Post(host => host.Title = value);
        }
    }

    public Document Document => _document;

    internal UIDocument WindowDocument => _document;

    public Element? Content { get; private set; }

    public UIElement? CustomTitleBar { get; private set; }

    public TitleStyle TitleStyle { get; set; } = TitleStyle.System;

    public BorderStyle BorderStyle { get; set; } = BorderStyle.Resizable;

    internal IntPtr OwnerHandle { get; set; }

    internal bool IsModal { get; set; }

    public string RenderBackend { get; set; } = "Software";

    public SoftwareRenderSurfaceKind SoftwareSurface { get; set; } = SoftwareRenderSurfaceKind.Auto;

    public Color Background { get; set; } = Color.White;

    public RenderMode RenderingMode { get; set; } = RenderMode.FullFrame;

    public int MaxDirtyRectCount { get; set; } = 16;

    public float MaxDirtyAreaRatio { get; set; } = 0.35f;

    public bool ShowRenderDiagnosticsOverlay { get; set; }

    public bool ShowDirtyUnionOverlay { get; set; } = true;

    public RenderDiagnostics LastRenderDiagnostics { get; internal set; } =
        new(RenderMode.FullFrame, true, "NotRendered", 0, 0, Rect.Empty);

    public Dispatcher Dispatcher => _dispatcher;

    public StoreScope Stores => _document.Context.Stores;

    public Size ClientSize
    {
        get
        {
            lock (_gate) return _clientSize;
        }
    }

    public float DpiScale
    {
        get
        {
            lock (_gate) return _dpiScale;
        }
    }

    public AppWindowState State
    {
        get
        {
            lock (_gate) return _state;
        }
    }

    public bool IsClosed
    {
        get
        {
            lock (_gate) return _isClosed;
        }
    }

    public IntPtr NativeWindow
    {
        get
        {
            lock (_gate)
                return _host is IPlatformNativeWindow nativeWindow ? nativeWindow.Handle : IntPtr.Zero;
        }
    }

    public event Action<Size>? SizeChanged;
    public event Action<AppWindowState>? StateChanged;
    public event Action? Closed;
    public event Action<int, KeyAction>? GlobalKeyEvent;

    public void Load(Element content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (_runtime?.IsRunning == true)
            throw new InvalidOperationException("Window content cannot be replaced while the application is running.");
        if (content.ParentNode != null && !ReferenceEquals(content, Content))
            throw new InvalidOperationException("Window content already has a parent.");
        if (ReferenceEquals(content, Content)) return;

        if (Content != null) _document.Body.Children.Remove(Content);
        Content = content;
        _document.Body.Children.Add(content);
    }

    public void LoadCustomTitleBar(UIElement titleBar)
    {
        ArgumentNullException.ThrowIfNull(titleBar);
        if (_runtime?.IsRunning == true)
            throw new InvalidOperationException("The custom title bar must be loaded before the application starts.");
        if (titleBar.ParentNode != null && !ReferenceEquals(titleBar, CustomTitleBar))
            throw new InvalidOperationException("The custom title bar already has a parent.");
        if (ReferenceEquals(titleBar, CustomTitleBar)) return;

        if (CustomTitleBar != null) _document.Head.Children.Remove(CustomTitleBar);
        CustomTitleBar = titleBar;
        _document.Head.Children.Add(titleBar);
        TitleStyle = TitleStyle.Custom;
    }

    public void Close()
    {
        lock (_gate) _closeRequested = true;
        Post(static host => host.Close());
    }

    public Task CloseAsync() => InvokeAsync(static host => host.Close());

    public Task MinimizeAsync() => InvokeAsync(static host => host.Minimize());

    public Task MaximizeAsync() => InvokeAsync(static host => host.Maximize());

    public Task RestoreAsync() => InvokeAsync(static host => host.Restore());

    public Task BeginMoveAsync() => InvokeAsync(static host => host.BeginMove());

    public void Open(Element content, Size? size = null)
    {
        var child = CreateChildWindow(content, size, isModal: false);
        StartChildWindow(child, failure: null);
    }

    public Task<object?> OpenDialog(Element content, Size? size = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        var child = CreateChildWindow(content, size, isModal: true);
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        child._dialogCompletion = value => completion.TrySetResult(value);
        StartChildWindow(child, exception => completion.TrySetException(exception));
        return completion.Task;
    }

    public async Task<T?> OpenDialog<T>(Element content, Size? size = null)
    {
        var result = await OpenDialog(content, size).ConfigureAwait(false);
        if (result == null) return default;
        if (result is T typedResult) return typedResult;
        throw new InvalidOperationException(
            $"The dialog result is '{result.GetType().FullName}', not '{typeof(T).FullName}'.");
    }

    public void CloseDialog<T>(T result)
    {
        lock (_gate)
        {
            if (_dialogCompletion == null)
                throw new InvalidOperationException("This window was not opened as a modal dialog.");
            _dialogResult = result;
            _hasDialogResult = true;
        }
        Close();
    }

    public void Minimize() => Post(static host => host.Minimize());

    public void Maximize() => Post(static host => host.Maximize());

    public void Restore() => Post(static host => host.Restore());

    public void RequestRender() => RequireRuntime().RequestRender();

    public Task InjectPointerAsync(DevToolsPointerInput input) => RequireRuntime().InjectPointerAsync(input);

    public Task InjectKeyAsync(DevToolsKeyInput input) => RequireRuntime().InjectKeyAsync(input);

    public Task InjectTextAsync(string text) => RequireRuntime().InjectTextAsync(text);

    public Task InjectWheelAsync(DevToolsWheelInput input) => RequireRuntime().InjectWheelAsync(input);

    public Task<Bitmap> CaptureRendererBitmapAsync() => RequireRuntime().CaptureRendererBitmapAsync();

    public Task<ElementInspectionSnapshot> CaptureInspectionSnapshotAsync(
        bool includeSourcePaths = true,
        bool includeTextContent = true) =>
        RequireRuntime().CaptureInspectionSnapshotAsync(includeSourcePaths, includeTextContent);

    public Task<ElementInspectionNode?> InspectElementAsync(
        int debugId,
        bool includeSourcePaths = true,
        bool includeTextContent = true) =>
        RequireRuntime().InspectElementAsync(debugId, includeSourcePaths, includeTextContent);

    public Task<ElementInspectionNode?> HitTestInspectionAsync(
        Point point,
        bool includeSourcePaths = true,
        bool includeTextContent = true) =>
        RequireRuntime().HitTestInspectionAsync(point, includeSourcePaths, includeTextContent);

    internal PlatformHostCreateInfo CreateHostInfo() => new()
    {
        Title = Title,
        Width = _initialWidth,
        Height = _initialHeight,
        RenderBackend = RenderBackend,
        SoftwareSurface = SoftwareSurface,
        TitleStyle = TitleStyle,
        BorderStyle = BorderStyle,
        OwnerHandle = OwnerHandle,
        IsModal = IsModal
    };

    private AppWindow CreateChildWindow(Element content, Size? size, bool isModal)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.ParentNode != null)
            throw new InvalidOperationException("Window content already has a parent.");

        var ownerHost = GetHost() ?? throw new InvalidOperationException(
            "A child window can only be opened while the owner window is running.");
        if (ownerHost is not IPlatformNativeWindow { Handle: not 0 } nativeOwner)
            throw new PlatformNotSupportedException("The current platform host does not expose a native window handle.");

        var requestedSize = size ?? new Size(480, 320);
        if (requestedSize.Width <= 0 || requestedSize.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Window width and height must be greater than zero.");

        var child = new AppWindow(content.TagName, checked((int)requestedSize.Width), checked((int)requestedSize.Height))
        {
            RenderBackend = RenderBackend,
            Background = Background,
            RenderingMode = RenderingMode,
            TitleStyle = TitleStyle,
            BorderStyle = BorderStyle,
            OwnerHandle = nativeOwner.Handle,
            IsModal = isModal
        };
        child.Load(content);
        return child;
    }

    private static void StartChildWindow(AppWindow child, Action<Exception>? failure)
    {
        var thread = new Thread(() =>
        {
            try
            {
                var application = new DesktopApplication(child);
                application.Run();
                Action<object?>? dialogCompletion;
                lock (child._gate)
                {
                    dialogCompletion = child._dialogCompletion;
                    child._dialogCompletion = null;
                }
                dialogCompletion?.Invoke(child._hasDialogResult ? child._dialogResult : null);
            }
            catch (Exception exception)
            {
                failure?.Invoke(exception);
            }
        })
        {
            IsBackground = true,
            Name = $"Square window: {child.Title}"
        };
        if (OperatingSystem.IsWindows()) thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    internal void BindApplication(Dispatcher dispatcher, IAppWindowRuntime runtime)
    {
        if (_applicationBound)
            throw new InvalidOperationException("The AppWindow is already bound to a DesktopApplication.");
        _applicationBound = true;
        _dispatcher = dispatcher;
        _runtime = runtime;
        _document.Context.Dispatcher = dispatcher;
    }

    internal void Attach(IPlatformHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        bool closeRequested;
        lock (_gate)
        {
            _host = host;
            _isClosed = false;
            _clientSize = host.ClientSize;
            _dpiScale = host.DpiScale;
            _state = host.State;
            host.Title = _title;
            closeRequested = _closeRequested;
        }

        host.SizeChanged += HandleSizeChanged;
        host.StateChanged += HandleStateChanged;
        host.Closed += HandleClosed;
        if (closeRequested) host.Close();
    }

    internal void Detach(IPlatformHost host)
    {
        host.SizeChanged -= HandleSizeChanged;
        host.StateChanged -= HandleStateChanged;
        host.Closed -= HandleClosed;
        var raiseClosed = false;
        lock (_gate)
        {
            if (ReferenceEquals(_host, host)) _host = null;
            if (!_isClosed)
            {
                _isClosed = true;
                raiseClosed = true;
            }
        }

        if (raiseClosed) Closed?.Invoke();
    }

    internal void SynchronizeTitle(string title)
    {
        lock (_gate) _title = title ?? "";
    }

    internal void RaiseGlobalKeyEvent(int keyCode, KeyAction action) =>
        GlobalKeyEvent?.Invoke(keyCode, action);

    private void HandleSizeChanged(Size size)
    {
        lock (_gate)
        {
            _clientSize = size;
            if (_host != null) _dpiScale = _host.DpiScale;
        }

        SizeChanged?.Invoke(size);
    }

    private void HandleStateChanged(AppWindowState state)
    {
        lock (_gate) _state = state;
        StateChanged?.Invoke(state);
    }

    private void HandleClosed()
    {
        lock (_gate)
        {
            if (_isClosed) return;
            _isClosed = true;
            _host = null;
        }

        Closed?.Invoke();
    }

    private void Post(Action<IPlatformHost> action)
    {
        if (_dispatcher.CheckAccess())
        {
            if (GetHost() is { } host) action(host);
            return;
        }

        _dispatcher.Invoke(() =>
        {
            if (GetHost() is { } host) action(host);
        });
    }

    private Task InvokeAsync(Action<IPlatformHost> action)
    {
        return _dispatcher.InvokeAsync(() =>
        {
            var host = GetHost();
            if (host == null)
                throw new InvalidOperationException("The native application window is not available.");
            action(host);
        });
    }

    private Task<T> InvokeAsync<T>(Func<IPlatformHost, T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_dispatcher.CheckAccess())
        {
            try
            {
                var host = GetHost();
                if (host == null)
                    throw new InvalidOperationException("The native application window is not available.");
                return Task.FromResult(action(host));
            }
            catch (Exception exception)
            {
                return Task.FromException<T>(exception);
            }
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatcher.Invoke(() =>
        {
            try
            {
                var host = GetHost();
                if (host == null)
                    throw new InvalidOperationException("The native application window is not available.");
                completion.SetResult(action(host));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        return completion.Task;
    }

    private IPlatformHost? GetHost()
    {
        lock (_gate) return _isClosed ? null : _host;
    }

    private IAppWindowRuntime RequireRuntime() =>
        _runtime ?? throw new InvalidOperationException("The AppWindow is not bound to a DesktopApplication.");
}

public enum TitleStyle
{
    System,
    Hidden,
    Custom
}

public enum BorderStyle
{
    Resizable,
    Fixed,
    None
}
