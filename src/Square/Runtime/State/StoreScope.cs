namespace Square.Runtime.State;

/// <summary>存储作用域，按类型注册并按父子层级查找存储。</summary>
public sealed class StoreScope : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, object> _stores = [];
    private readonly List<StoreScope> _children = [];
    private StoreScope? _parent;
    private bool _disposed;

    /// <summary>创建根作用域。</summary>
    public StoreScope()
    {
    }

    private StoreScope(StoreScope parent)
    {
        _parent = parent;
    }

    /// <summary>注册存储实例；同类型重复注册将抛出异常。</summary>
    public TStore Add<TStore>(TStore store) where TStore : class
    {
        ArgumentNullException.ThrowIfNull(store);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_stores.TryAdd(typeof(TStore), store))
                throw new InvalidOperationException(
                    $"A store for {typeof(TStore)} is already registered in this scope.");
        }

        return store;
    }

    /// <summary>创建并注册 <see cref="Store{TState}"/>。</summary>
    public Store<TState> Add<TState>(
        TState initialState,
        IEqualityComparer<TState>? comparer = null)
    {
        return Add(new Store<TState>(initialState, comparer));
    }

    /// <summary>获取已注册的存储；不存在则抛出异常。</summary>
    public TStore Get<TStore>() where TStore : class
    {
        if (TryGet<TStore>(out var store)) return store;
        throw new KeyNotFoundException($"No store for {typeof(TStore)} is registered in this scope hierarchy.");
    }

    /// <summary>尝试按层级查找存储。</summary>
    public bool TryGet<TStore>(out TStore store) where TStore : class
    {
        StoreScope? parent;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_stores.TryGetValue(typeof(TStore), out var candidate))
            {
                store = (TStore)candidate;
                return true;
            }

            parent = _parent;
        }

        if (parent != null) return parent.TryGet(out store);
        store = null!;
        return false;
    }

    /// <summary>创建子作用域。</summary>
    public StoreScope CreateChild()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var child = new StoreScope(this);
            _children.Add(child);
            return child;
        }
    }

    /// <summary>释放作用域及其子作用域与所含可释放存储。</summary>
    public void Dispose()
    {
        StoreScope[] children;
        object[] stores;
        StoreScope? parent;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            children = [.. _children];
            stores = [.. _stores.Values];
            _children.Clear();
            _stores.Clear();
            parent = _parent;
            _parent = null;
        }

        parent?.RemoveChild(this);
        foreach (var child in children)
            child.Dispose();
        foreach (var store in stores)
            if (store is IDisposable disposable) disposable.Dispose();
    }

    private void RemoveChild(StoreScope child)
    {
        lock (_gate) _children.Remove(child);
    }
}