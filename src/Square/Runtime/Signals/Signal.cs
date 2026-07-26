namespace Square.Runtime.Signals;

/// <summary>可订阅的信号值，发布时通知订阅者并跟踪版本。</summary>
public sealed class Signal<T>
{
    private readonly object _gate = new();
    private readonly List<Subscription> _subscriptions = [];
    private readonly IEqualityComparer<T> _comparer;
    private T _value;
    private long _version;

    /// <summary>用初始值与可选相等比较器创建信号。</summary>
    public Signal(T initialValue, IEqualityComparer<T>? comparer = null)
    {
        _value = initialValue;
        _comparer = comparer ?? EqualityComparer<T>.Default;
    }

    /// <summary>用默认值创建信号。</summary>
    public Signal() : this(default!) { }

    /// <summary>当前值；设置时发布新值。</summary>
    public T Value
    {
        get
        {
            lock (_gate) return _value;
        }
        set => Publish(value);
    }

    /// <summary>发布新值；若与当前值相等且未强制则不发布。</summary>
    public bool Publish(T value, bool force = false)
    {
        Subscription[] subscribers;
        long version;
        lock (_gate)
        {
            if (!force && _comparer.Equals(_value, value)) return false;
            _value = value;
            version = ++_version;
            subscribers = [.. _subscriptions];
        }

        foreach (var subscriber in subscribers)
            subscriber.Dispatch(value, version);
        return true;
    }

    /// <summary>基于当前值计算新值并发布。</summary>
    public T Update(Func<T, T> update, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(update);

        Subscription[] subscribers;
        T value;
        long version;
        lock (_gate)
        {
            value = update(_value);
            if (!force && _comparer.Equals(_value, value)) return _value;
            _value = value;
            version = ++_version;
            subscribers = [.. _subscriptions];
        }

        foreach (var subscriber in subscribers)
            subscriber.Dispatch(value, version);
        return value;
    }

    /// <summary>订阅值变更；可选调度器与是否立即派发当前值。</summary>
    public IDisposable Subscribe(Action<T> handler, Dispatcher? dispatcher = null, bool emitCurrent = false)
    {
        ArgumentNullException.ThrowIfNull(handler);

        Subscription subscription;
        T current;
        long version;
        lock (_gate)
        {
            subscription = new Subscription(this, handler, dispatcher);
            _subscriptions.Add(subscription);
            current = _value;
            version = _version;
        }

        if (emitCurrent) subscription.Dispatch(current, version);
        return subscription;
    }

    private void Remove(Subscription subscription)
    {
        lock (_gate) _subscriptions.Remove(subscription);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly object _gate = new();
        private Signal<T>? _owner;
        private Action<T>? _handler;
        private readonly Dispatcher? _dispatcher;
        private long _lastVersion = -1;

        public Subscription(Signal<T> owner, Action<T> handler, Dispatcher? dispatcher)
        {
            _owner = owner;
            _handler = handler;
            _dispatcher = dispatcher;
        }

        public void Dispatch(T value, long version)
        {
            if (_dispatcher != null && !_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(() => Deliver(value, version));
                return;
            }

            Deliver(value, version);
        }

        public void Dispose()
        {
            Signal<T>? owner;
            lock (_gate)
            {
                owner = _owner;
                _owner = null;
                _handler = null;
            }
            owner?.Remove(this);
        }

        private void Deliver(T value, long version)
        {
            lock (_gate)
            {
                if (_handler == null || version <= _lastVersion) return;
                _lastVersion = version;
                _handler(value);
            }
        }
    }
}