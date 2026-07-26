namespace Square.Runtime.State;

/// <summary>响应式订阅的可选配置。</summary>
public sealed class ReactiveSubscriptionOptions
{
    /// <summary>用于派发通知的调度器；为 null 时在发布线程同步调用。</summary>
    public Dispatcher? Dispatcher { get; init; }

    /// <summary>是否在订阅时立即派发当前值。</summary>
    public bool EmitCurrent { get; init; }

    /// <summary>取消令牌；触发时移除订阅。</summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>回调抛出异常时的错误处理器。</summary>
    public Action<Exception>? OnError { get; init; }
}