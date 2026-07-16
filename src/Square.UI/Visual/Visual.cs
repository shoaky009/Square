using Square.Events;
using Square.Graphics;
using Square.Runtime;
using Square.Runtime.Binding;
using Square.UI.ElementApi;
using Square.UI.Properties;

namespace Square.UI;

public abstract class Visual : IComponentLifecycle, ILayoutLifecycle, IEventTarget
{
    private Visual? _parent;
    private readonly Dictionary<EventDefinition, List<EventHandlerEntry>> _eventHandlers = [];
    private readonly List<LegacyHandlerEntry> _legacyHandlers = [];
    private Rect _geometry;
    private bool _isVisible = true;
    private bool _isLayoutDirty = true;
    private bool _isVisualDirty = true;
    private int _zIndex;
    private readonly List<IDisposable> _bindings = [];

    public bool IsLayoutDirty => _isLayoutDirty;
    public bool IsVisualDirty => _isVisualDirty;
    public virtual int ZIndex
    {
        get => _zIndex;
        set
        {
            if (_zIndex == value) return;
            _zIndex = value;
            _parent?.InvalidateVisual();
        }
    }

    public PropertyStore Properties { get; } = new();
    public StyleAccessor Style { get; }
    public ClassListAccessor ClassList { get; }
    public ChildrenCollection Children { get; }

    public Visual? Parent
    {
        get => _parent;
        internal set => _parent = value;
    }

    public Rect Geometry
    {
        get => _geometry;
        set
        {
            if (_geometry == value) return;
            _geometry = value;
            InvalidateVisual();
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value) return;
            _isVisible = value;
            InvalidateLayout();
        }
    }

    public VisualState State { get; private set; }

    public void SetState(VisualState flag, bool on)
    {
        if (on) State |= flag;
        else State &= ~flag;
        InvalidateVisual();
    }

    public bool HasState(VisualState flag) => State.Has(flag);

    public bool IsAttached { get; private set; }
    public bool IsLoaded { get; private set; }

    protected Visual()
    {
        Style = new StyleAccessor(this);
        ClassList = new ClassListAccessor(this);
        Children = new ChildrenCollection(this);
    }

    public T? GetProperty<T>(string name)
    {
        if (Properties.TryGetValue(name, out T value)) return value;
        return default;
    }

    public void SetProperty<T>(string name, T value)
    {
        Properties.SetValue(name, value);
        OnPropertyChanged(name);
        ((IComponentLifecycle)this).OnPropChanged(name);
        InvalidateVisual();
    }

    public void BindProperty<T>(string name, Func<T> getter)
    {
        Properties.MarkBound(name);
        var value = getter();
        Properties.SetValue(name, value);
        InvalidateVisual();
    }

    public void BindProperty<T>(string name, ObservableValue<T> source)
    {
        Properties.MarkBound(name);
        SetBoundValue(name, source.Value);
        _bindings.Add(source.Subscribe(value => SetBoundValue(name, value)));
    }

    private void SetBoundValue<T>(string name, T value)
    {
        Properties.SetValue(name, value);
        OnPropertyChanged(name);
        ((IComponentLifecycle)this).OnPropChanged(name);
        InvalidateVisual();
    }

    public void AddEventListener<TEventArgs>(
        RoutedEvent<TEventArgs> routedEvent,
        RoutedEventHandler<TEventArgs> handler,
        bool handledEventsToo = false)
        where TEventArgs : RoutedEventArgs
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        ArgumentNullException.ThrowIfNull(handler);
        if (!_eventHandlers.TryGetValue(routedEvent, out var handlers))
        {
            handlers = [];
            _eventHandlers.Add(routedEvent, handlers);
        }
        handlers.Add(new EventHandlerEntry(handler, handledEventsToo));
    }

    public void RemoveEventListener<TEventArgs>(
        RoutedEvent<TEventArgs> routedEvent,
        RoutedEventHandler<TEventArgs> handler)
        where TEventArgs : RoutedEventArgs
    {
        RemoveSingleEventListener(routedEvent, handler);
    }

    public void AddEventListener(string eventName, Action handler)
    {
        var routedEvent = ResolveEvent(eventName);
        RoutedEventHandler<RoutedEventArgs> adapter = (_, _) => handler();
        AddEventListener(routedEvent, adapter);
        _legacyHandlers.Add(new LegacyHandlerEntry(routedEvent, handler, adapter));
    }

    public void AddEventListener(string eventName, RoutedEventHandler<RoutedEventArgs> handler)
    {
        var routedEvent = ResolveEvent(eventName);
        AddEventListener(routedEvent, handler);
        _legacyHandlers.Add(new LegacyHandlerEntry(routedEvent, handler, handler));
    }

    public void AddEventListener(string eventName, Action<RoutedEventArgs> handler)
    {
        var routedEvent = ResolveEvent(eventName);
        RoutedEventHandler<RoutedEventArgs> adapter = (_, args) => handler(args);
        AddEventListener(routedEvent, adapter);
        _legacyHandlers.Add(new LegacyHandlerEntry(routedEvent, handler, adapter));
    }

    public void RemoveEventListener(string eventName)
    {
        var routedEvent = ResolveEvent(eventName);
        _eventHandlers.Remove(routedEvent);
        _legacyHandlers.RemoveAll(entry => entry.Event.Equals(routedEvent));
    }

    public void RemoveEventListener(string eventName, Action handler)
        => RemoveLegacyEventListener(eventName, handler);

    public void RemoveEventListener(string eventName, RoutedEventHandler<RoutedEventArgs> handler) =>
        RemoveLegacyEventListener(eventName, handler);

    public void RemoveEventListener(string eventName, Action<RoutedEventArgs> handler) =>
        RemoveLegacyEventListener(eventName, handler);

    public void RaiseEvent<TEventArgs>(RoutedEvent<TEventArgs> routedEvent, TEventArgs args)
        where TEventArgs : RoutedEventArgs
    {
        ArgumentNullException.ThrowIfNull(routedEvent);
        ArgumentNullException.ThrowIfNull(args);
        args.Event = routedEvent;
        args.OriginalSource ??= this;
        args.Source ??= this;

        var route = BuildRoute();
        if (routedEvent.RoutingStrategy is RoutingStrategy.Tunnel or RoutingStrategy.TunnelAndBubble)
        {
            for (var i = route.Count - 1; i > 0; i--)
                route[i].InvokeHandlers(routedEvent, args, EventPhase.Tunneling);
        }

        var phase = routedEvent.RoutingStrategy == RoutingStrategy.Direct ? EventPhase.Direct : EventPhase.AtTarget;
        InvokeHandlers(routedEvent, args, phase);

        if (routedEvent.RoutingStrategy is RoutingStrategy.Bubble or RoutingStrategy.TunnelAndBubble)
        {
            for (var i = 1; i < route.Count; i++)
                route[i].InvokeHandlers(routedEvent, args, EventPhase.Bubbling);
        }
    }

    public void RaiseEvent(string eventName)
    {
        RaiseEvent(ResolveEvent(eventName), new RoutedEventArgs());
    }

    public void RouteEvent(string eventName) => RaiseEvent(eventName);

    private static RoutedEvent<RoutedEventArgs> ResolveEvent(string eventName)
    {
        return StandardEvents.ResolveOrCreate(eventName);
    }

    private List<Visual> BuildRoute()
    {
        var route = new List<Visual>();
        for (Visual? current = this; current != null; current = current.Parent)
            route.Add(current);
        return route;
    }

    private void InvokeHandlers<TEventArgs>(RoutedEvent<TEventArgs> routedEvent, TEventArgs args, EventPhase phase)
        where TEventArgs : RoutedEventArgs
    {
        args.CurrentTarget = this;
        args.Phase = phase;
        if (!_eventHandlers.TryGetValue(routedEvent, out var handlers)) return;
        foreach (var entry in handlers.ToArray())
        {
            if (args.Handled && !entry.HandledEventsToo) continue;
            ((RoutedEventHandler<TEventArgs>)entry.Handler)(this, args);
        }
    }

    private sealed record EventHandlerEntry(Delegate Handler, bool HandledEventsToo);
    private sealed record LegacyHandlerEntry(
        EventDefinition Event,
        Delegate Handler,
        RoutedEventHandler<RoutedEventArgs> Adapter);

    private void RemoveLegacyEventListener(string eventName, Delegate handler)
    {
        var routedEvent = ResolveEvent(eventName);
        var index = _legacyHandlers.FindLastIndex(entry =>
            entry.Event.Equals(routedEvent) && Equals(entry.Handler, handler));
        if (index < 0) return;
        var adapter = _legacyHandlers[index].Adapter;
        _legacyHandlers.RemoveAt(index);
        RemoveSingleEventListener(routedEvent, adapter);
    }

    private void RemoveSingleEventListener<TEventArgs>(
        RoutedEvent<TEventArgs> routedEvent,
        RoutedEventHandler<TEventArgs> handler)
        where TEventArgs : RoutedEventArgs
    {
        if (!_eventHandlers.TryGetValue(routedEvent, out var handlers)) return;
        var index = handlers.FindLastIndex(entry => Equals(entry.Handler, handler));
        if (index >= 0) handlers.RemoveAt(index);
        if (handlers.Count == 0) _eventHandlers.Remove(routedEvent);
    }

    public virtual Visual? HitTest(Point point)
    {
        if (!IsVisible) return null;
        var inside = Geometry.Contains(point);
        if (!inside && ClipsOverflowAt(point)) return null;

        foreach (var child in Children.OrderByDescending(child => child.ZIndex))
        {
            var hit = child.HitTest(point);
            if (hit != null) return hit;
        }

        return inside ? this : null;
    }

    public bool ClipsOverflow()
    {
        var (clipX, clipY) = GetOverflowClipAxes();
        return clipX || clipY;
    }

    public Rect GetOverflowClipRect()
    {
        var (clipX, clipY) = GetOverflowClipAxes();
        if (!clipX && !clipY) return Rect.Empty;
        const float unbounded = 1_000_000f;
        return new Rect(
            clipX ? Geometry.X : -unbounded,
            clipY ? Geometry.Y : -unbounded,
            clipX ? Geometry.Width : unbounded * 2,
            clipY ? Geometry.Height : unbounded * 2);
    }

    private bool ClipsOverflowAt(Point point)
    {
        var (clipX, clipY) = GetOverflowClipAxes();
        return clipX && (point.X < Geometry.Left || point.X > Geometry.Right) ||
            clipY && (point.Y < Geometry.Top || point.Y > Geometry.Bottom);
    }

    private (bool clipX, bool clipY) GetOverflowClipAxes()
    {
        var overflow = Style.Get("overflow");
        var clipBoth = IsClippingOverflow(overflow);
        return (clipBoth || IsClippingOverflow(Style.Get("overflow-x")),
            clipBoth || IsClippingOverflow(Style.Get("overflow-y")));
    }

    private static bool IsClippingOverflow(string? value) =>
        string.Equals(value, "hidden", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "clip", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeEventName(string eventName) => eventName.ToLowerInvariant();

    public T? Query<T>(string? className = null) where T : Visual
    {
        return QueryInternal<T>(className);
    }

    public List<T> QueryAll<T>(string? className = null) where T : Visual
    {
        var result = new List<T>();
        QueryAllInternal(className, result);
        return result;
    }

    private T? QueryInternal<T>(string? className) where T : Visual
    {
        if (this is T typed && (className == null || ClassList.Contains(className)))
            return typed;
        foreach (var child in Children)
        {
            var found = child.QueryInternal<T>(className);
            if (found != null) return found;
        }
        return null;
    }

    private void QueryAllInternal<T>(string? className, List<T> result) where T : Visual
    {
        if (this is T typed && (className == null || ClassList.Contains(className)))
            result.Add(typed);
        foreach (var child in Children)
            child.QueryAllInternal(className, result);
    }

    public void InvalidateLayout()
    {
        _isLayoutDirty = true;
        _isVisualDirty = true;
        _parent?.InvalidateLayout();
    }

    public void InvalidateVisual()
    {
        _isVisualDirty = true;
    }

    public void ClearLayoutDirty() => _isLayoutDirty = false;
    public void ClearVisualDirty() => _isVisualDirty = false;

    protected virtual void OnPropertyChanged(string name) { }
    internal virtual void OnChildAdded(Visual child) { }
    internal virtual void OnChildRemoved(Visual child) { }

    public virtual Size Measure(Size availableSize) => Size.Zero;
    public virtual void Arrange(Rect finalRect) { _geometry = finalRect; }

    public virtual void Render(IRenderContext ctx) { }

    public virtual void BuildVisualTree() { }

    protected virtual void OnPropChanged(string name) { }
    protected virtual void OnAttachedCore() { }
    protected virtual void OnDetachedCore() { }

    void IComponentLifecycle.OnPropChanged(string name) => OnPropChanged(name);

    void IComponentLifecycle.OnAttached()
    {
        if (IsAttached) return;
        IsAttached = true;
        OnAttachedCore();
        foreach (var child in Children) ((IComponentLifecycle)child).OnAttached();
    }

    void IComponentLifecycle.OnDetached()
    {
        if (!IsAttached) return;
        foreach (var child in Children) ((IComponentLifecycle)child).OnDetached();
        OnDetachedCore();
        IsAttached = false;
    }

    void IComponentLifecycle.OnLoaded()
    {
        IsLoaded = true;
        foreach (var child in Children) ((IComponentLifecycle)child).OnLoaded();
    }

    void IComponentLifecycle.OnUnloaded()
    {
        IsLoaded = false;
        foreach (var child in Children) ((IComponentLifecycle)child).OnUnloaded();
    }

    void ILayoutLifecycle.OnMeasure() => Measure(_geometry.Size);
    void ILayoutLifecycle.OnArrange() => Arrange(_geometry);
}
