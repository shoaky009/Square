namespace Square.Runtime.State;

public sealed class ReactiveSubscriptionOptions
{
    public Dispatcher? Dispatcher { get; init; }

    public bool EmitCurrent { get; init; }

    public CancellationToken CancellationToken { get; init; }

    public Action<Exception>? OnError { get; init; }
}
