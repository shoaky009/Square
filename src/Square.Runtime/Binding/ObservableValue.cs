namespace Square.Runtime.Binding;

public sealed class ObservableValue<T>
{
    private T _value;
    private Action<T>? _changed;

    public ObservableValue(T value) { _value = value; }
    public ObservableValue() : this(default!) { }

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

    public IDisposable Subscribe(Action<T> handler)
    {
        _changed += handler;
        return new Subscription(() => _changed -= handler);
    }

    public void Notify() => _changed?.Invoke(_value);

    public static implicit operator ObservableValue<T>(T value) => new(value);
    public static implicit operator T(ObservableValue<T> ov) => ov._value;

    public override string ToString() => _value?.ToString() ?? "";

    private sealed class Subscription : IDisposable
    {
        private Action? _unsubscribe;
        public Subscription(Action unsubscribe) { _unsubscribe = unsubscribe; }
        public void Dispose() { _unsubscribe?.Invoke(); _unsubscribe = null; }
    }
}