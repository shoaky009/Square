namespace Square.Runtime.State;

/// <summary>基于选择器从 <see cref="Store{TState}"/> 派生的响应值视图。</summary>
public sealed class StoreSelector<TState, TValue> : IReactiveValue<TValue>, IDisposable
{
    private readonly object _gate = new();
    private readonly List<ReactiveSubscription<TValue>> _subscriptions = [];
    private readonly Func<TState, TValue> _selector;
    private readonly IEqualityComparer<TValue> _comparer;
    private readonly IDisposable _sourceSubscription;
    private TValue _value;
    private long _version;
    private bool _disposed;

    internal StoreSelector(
        Store<TState> source,
        Func<TState, TValue> selector,
        IEqualityComparer<TValue>? comparer)
    {
        ArgumentNullException.ThrowIfNull(selector);
        _selector = selector;
        _comparer = comparer ?? EqualityComparer<TValue>.Default;
        _value = source.Read(selector);
        _sourceSubscription = source.Subscribe(
            SourceChanged,
            new ReactiveSubscriptionOptions { EmitCurrent = true });
    }

    /// <summary>当前选择值。</summary>
    public TValue Value
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _value;
            }
        }
    }

    /// <summary>订阅选择值变更；按选项决定是否立即派发当前值。</summary>
    public IDisposable Subscribe(Action<TValue> callback, ReactiveSubscriptionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(callback);
        options ??= new ReactiveSubscriptionOptions();

        var subscription = new ReactiveSubscription<TValue>(
            callback,
            options.Dispatcher,
            options.OnError,
            Remove);
        TValue current;
        long version;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _subscriptions.Add(subscription);
            current = _value;
            version = _version;
        }

        subscription.RegisterCancellation(options.CancellationToken);
        if (options.EmitCurrent) subscription.Dispatch(current, version);
        return subscription;
    }

    /// <summary>订阅变更（不接收值的便捷重载）。</summary>
    public IDisposable SubscribeChanged(Action callback, ReactiveSubscriptionOptions? options = null) =>
        Subscribe(_ => callback(), options);

    /// <summary>释放选择器并解除源订阅。</summary>
    public void Dispose()
    {
        ReactiveSubscription<TValue>[] subscriptions;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            subscriptions = [.. _subscriptions];
            _subscriptions.Clear();
        }

        _sourceSubscription.Dispose();
        foreach (var subscription in subscriptions)
            subscription.Dispose();
    }

    private void SourceChanged(TState state)
    {
        var value = _selector(state);
        ReactiveSubscription<TValue>[] subscriptions;
        long version;
        lock (_gate)
        {
            if (_disposed || _comparer.Equals(_value, value)) return;
            _value = value;
            version = ++_version;
            subscriptions = [.. _subscriptions];
        }

        foreach (var subscription in subscriptions)
            subscription.Dispatch(value, version);
    }

    private void Remove(ReactiveSubscription<TValue> subscription)
    {
        lock (_gate) _subscriptions.Remove(subscription);
    }
}