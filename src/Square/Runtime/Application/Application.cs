namespace Square.Runtime;

public abstract class Application
{
    [ThreadStatic]
    private static Application? _current;
    public static Application Current => _current ?? throw new InvalidOperationException("Application not started");

    public static bool IsStarted => _current != null;

    public Dispatcher Dispatcher { get; } = new();
    public bool IsRunning { get; private set; }
    public event Action? Exited;

    protected Application()
    {
        _current = this;
    }

    public void Run()
    {
        if (IsRunning) throw new InvalidOperationException("Application already running");
        IsRunning = true;
        try
        {
            OnStart();
            RunCore();
        }
        finally
        {
            try
            {
                OnExit();
            }
            finally
            {
                IsRunning = false;
                Exited?.Invoke();
            }
        }
    }

    public void Shutdown()
    {
        IsRunning = false;
    }

    protected abstract void RunCore();

    protected virtual void OnStart() { }
    protected virtual void OnExit() { }
}
