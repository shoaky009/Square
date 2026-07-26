using Square.Runtime.State;

namespace Square.Runtime.Binding;

/// <summary>简单的可观察值容器，值变化时通知订阅者。</summary>
public sealed class ObservableValue<T> : IReactiveValue<T>
{
    private T _value;
    private Action<T>? _changed;

    /// <summary>用初始值创建实例。</summary>
    public ObservableValue(T value) { _value = value; }
    /// <summary>用默认值创建实例。</summary>
    public ObservableValue() : this(default!) { }

    /// <summary>当前值；设置时若变化则触发变更通知。</summary>
    public T Value
    {
        get => _value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(_value, value)) return;
            _value = value;
            _changed?.Invoke(_value);
        }
    }

    /// <summary>订阅值变更；返回可取消订阅的句柄。</summary>
    public IDisposable Subscribe(Action<T> handler)
    {
        _changed += handler;
        return new Subscription(() => _changed -= handler);
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(Action<T> callback, ReactiveSubscriptionOptions? options = null) => Subscribe(callback);

    /// <inheritdoc/>
    public IDisposable SubscribeChanged(Action callback, ReactiveSubscriptionOptions? options = null) =>
        Subscribe(_ => callback());

    /// <summary>手动触发变更通知（使用当前值）。</summary>
    public void Notify() => _changed?.Invoke(_value);

    /// <summary>将值隐式包装为可观察值。</summary>
    public static implicit operator ObservableValue<T>(T value) => new(value);
    /// <summary>将可观察值隐式解包为值。</summary>
    public static implicit operator T(ObservableValue<T> ov) => ov._value;

    /// <summary>返回当前值的字符串表示。</summary>
    public override string ToString() => _value?.ToString() ?? "";

    private sealed class Subscription : IDisposable
    {
        private Action? _unsubscribe;
        public Subscription(Action unsubscribe) { _unsubscribe = unsubscribe; }
        public void Dispose() { _unsubscribe?.Invoke(); _unsubscribe = null; }
    }
}