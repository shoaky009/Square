using Square.Graphics;
using Square.Runtime;
using Square.UI.ElementApi;
using Square.UI.Properties;

namespace Square.UI;

public abstract class Visual : IComponentLifecycle, ILayoutLifecycle
{
    private Visual? _parent;
    private Rect _geometry;
    private bool _isVisible = true;
    private bool _isLayoutDirty = true;
    private bool _isVisualDirty = true;

    public bool IsLayoutDirty => _isLayoutDirty;
    public bool IsVisualDirty => _isVisualDirty;

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
            InvalidateVisual();
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
        InvalidateVisual();
    }

    public void BindProperty<T>(string name, Func<T> getter)
    {
        Properties.MarkBound(name);
        var value = getter();
        Properties.SetValue(name, value);
        InvalidateVisual();
    }

    public void AddEventListener(string eventName, Action handler)
    {
        Properties.SetValue($"__event_{eventName}", handler);
    }

    public void RemoveEventListener(string eventName)
    {
        Properties.RemoveValue($"__event_{eventName}");
    }

    internal bool TryGetEventListener(string eventName, out Action? handler)
    {
        if (Properties.TryGetValue($"__event_{eventName}", out Action h))
        {
            handler = h;
            return true;
        }
        handler = null;
        return false;
    }

    public void RaiseEvent(string eventName)
    {
        if (TryGetEventListener(eventName, out var handler)) handler?.Invoke();
    }

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