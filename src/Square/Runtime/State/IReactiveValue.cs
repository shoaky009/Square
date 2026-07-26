namespace Square.Runtime.State;

/// <summary>响应式源，仅支持变更订阅。</summary>
public interface IReactiveSource
{
    /// <summary>订阅变更；回调不接收值。</summary>
    IDisposable SubscribeChanged(Action callback, ReactiveSubscriptionOptions? options = null);
}

/// <summary>带当前值的响应式源。</summary>
public interface IReactiveValue<T> : IReactiveSource
{
    /// <summary>当前值。</summary>
    T Value { get; }

    /// <summary>订阅值变更。</summary>
    IDisposable Subscribe(Action<T> callback, ReactiveSubscriptionOptions? options = null);
}