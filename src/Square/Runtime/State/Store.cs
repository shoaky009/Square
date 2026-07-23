namespace Square.Runtime.State;

public sealed class Store<TState> : IReactiveValue<TState>, IDisposable
{
    private readonly object _gate = new();
    private readonly List<ReactiveSubscription<TState>> _subscriptions = [];
    private readonly IEqualityComparer<TState> _comparer;
    private TState _value;
    private long _version;
    private bool _disposed;

    public Store(TState initialState, IEqualityComparer<TState>? comparer = null)
    {
        _value = initialState;
        _comparer = comparer ?? EqualityComparer<TState>.Default;
    }

    public TState Value
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

    public long Version
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _version;
            }
        }
    }

    public bool Set(TState state)
    {
        ReactiveSubscription<TState>[] subscriptions;
        long version;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_comparer.Equals(_value, state)) return false;

            _value = state;
            version = ++_version;
            subscriptions = [.. _subscriptions];
        }

        Publish(subscriptions, state, version);
        return true;
    }

    public TState Update(Func<TState, TState> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        ReactiveSubscription<TState>[] subscriptions;
        TState state;
        long version;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            state = update(_value);
            if (_comparer.Equals(_value, state)) return _value;

            _value = state;
            version = ++_version;
            subscriptions = [.. _subscriptions];
        }

        Publish(subscriptions, state, version);
        return state;
    }

    public TResult Read<TResult>(Func<TState, TResult> read)
    {
        ArgumentNullException.ThrowIfNull(read);

        TState state;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            state = _value;
        }

        return read(state);
    }

    public StoreSelector<TState, TValue> Select<TValue>(
        Func<TState, TValue> selector,
        IEqualityComparer<TValue>? comparer = null)
    {
        ObjectDisposedException.ThrowIf(IsDisposed(), this);
        return new StoreSelector<TState, TValue>(this, selector, comparer);
    }

    public IDisposable Subscribe(Action<TState> callback, ReactiveSubscriptionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(callback);
        options ??= new ReactiveSubscriptionOptions();

        var subscription = new ReactiveSubscription<TState>(
            callback,
            options.Dispatcher,
            options.OnError,
            Remove);
        TState current;
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

    public IDisposable SubscribeChanged(Action callback, ReactiveSubscriptionOptions? options = null) =>
        Subscribe(_ => callback(), options);

    public void Dispose()
    {
        ReactiveSubscription<TState>[] subscriptions;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            subscriptions = [.. _subscriptions];
            _subscriptions.Clear();
        }

        foreach (var subscription in subscriptions)
            subscription.Dispose();
    }

    private bool IsDisposed()
    {
        lock (_gate) return _disposed;
    }

    private void Remove(ReactiveSubscription<TState> subscription)
    {
        lock (_gate) _subscriptions.Remove(subscription);
    }

    private static void Publish(
        ReactiveSubscription<TState>[] subscriptions,
        TState state,
        long version)
    {
        foreach (var subscription in subscriptions)
            subscription.Dispatch(state, version);
    }
}

internal sealed class ReactiveSubscription<T> : IDisposable
{
    private readonly object _gate = new();
    private readonly Dispatcher? _dispatcher;
    private readonly Action<Exception>? _onError;
    private Action<T>? _callback;
    private Action<ReactiveSubscription<T>>? _remove;
    private CancellationTokenRegistration _cancellationRegistration;
    private T _pendingValue = default!;
    private long _pendingVersion = -1;
    private long _deliveredVersion = -1;
    private bool _workQueued;

    public ReactiveSubscription(
        Action<T> callback,
        Dispatcher? dispatcher,
        Action<Exception>? onError,
        Action<ReactiveSubscription<T>> remove)
    {
        _callback = callback;
        _dispatcher = dispatcher;
        _onError = onError;
        _remove = remove;
    }

    public void RegisterCancellation(CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled) return;

        var registration = cancellationToken.UnsafeRegister(
            static state => ((ReactiveSubscription<T>)state!).Dispose(),
            this);
        lock (_gate)
        {
            if (_callback == null)
                registration.Dispose();
            else
                _cancellationRegistration = registration;
        }
    }

    public void Dispatch(T value, long version)
    {
        if (_dispatcher == null || _dispatcher.CheckAccess())
        {
            Deliver(value, version);
            return;
        }

        lock (_gate)
        {
            if (_callback == null || version <= _pendingVersion || version <= _deliveredVersion) return;
            _pendingValue = value;
            _pendingVersion = version;
            if (_workQueued) return;
            _workQueued = true;
        }

        _dispatcher.Invoke(DrainPending);
    }

    public void Dispose()
    {
        Action<ReactiveSubscription<T>>? remove;
        CancellationTokenRegistration registration;
        lock (_gate)
        {
            if (_callback == null) return;
            _callback = null;
            remove = _remove;
            _remove = null;
            _workQueued = false;
            registration = _cancellationRegistration;
            _cancellationRegistration = default;
        }

        remove?.Invoke(this);
        registration.Dispose();
    }

    private void DrainPending()
    {
        T value;
        long version;
        lock (_gate)
        {
            if (_callback == null)
            {
                _workQueued = false;
                return;
            }

            value = _pendingValue;
            version = _pendingVersion;
            _workQueued = false;
        }

        Deliver(value, version);
    }

    private void Deliver(T value, long version)
    {
        lock (_gate)
        {
            if (_callback == null || version <= _deliveredVersion) return;
            _deliveredVersion = version;
            try
            {
                _callback(value);
            }
            catch (Exception exception)
            {
                try
                {
                    _onError?.Invoke(exception);
                }
                catch
                {
                    // Error handlers are isolated for the same reason as subscriber callbacks.
                }
            }
        }
    }
}
