namespace Square.Runtime.State;

public interface IReactiveValue<T>
{
    T Value { get; }

    IDisposable Subscribe(Action<T> callback, ReactiveSubscriptionOptions? options = null);
}
