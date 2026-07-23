namespace Square.Runtime.State;

public interface IReactiveSource
{
    IDisposable SubscribeChanged(Action callback, ReactiveSubscriptionOptions? options = null);
}

public interface IReactiveValue<T> : IReactiveSource
{
    T Value { get; }

    IDisposable Subscribe(Action<T> callback, ReactiveSubscriptionOptions? options = null);
}
