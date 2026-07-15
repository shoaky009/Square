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

    public void Invoke(Action action)
    {
        lock (_lock) _queue.Enqueue(action);
    }

    public void Run()
    {
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