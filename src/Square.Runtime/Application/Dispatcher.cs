namespace Square.Runtime;

public enum DispatcherPriority
{
    Idle = 0,
    Normal = 1,
    High = 2
}

public sealed class Dispatcher
{
    private readonly Queue<Action> _queue = new();
    private readonly object _lock = new();
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;

    public bool CheckAccess() => Environment.CurrentManagedThreadId == _ownerThreadId;

    public void VerifyAccess()
    {
        if (!CheckAccess())
            throw new InvalidOperationException("The Dispatcher queue can only be drained by its owning thread.");
    }

    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_lock) _queue.Enqueue(action);
    }

    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Invoke(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        return completion.Task;
    }

    public void Run()
    {
        VerifyAccess();
        while (true)
        {
            Action? action;
            lock (_lock)
            {
                if (_queue.Count == 0) break;
                action = _queue.Dequeue();
            }
            action?.Invoke();
        }
    }

    public bool HasWork
    {
        get { lock (_lock) return _queue.Count > 0; }
    }
}
