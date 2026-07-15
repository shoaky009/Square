namespace Square.Runtime;

public abstract class Application
{
    private static Application? _current;
    public static Application Current => _current ?? throw new InvalidOperationException("Application not started");

    public static bool IsStarted => _current != null;

    public Dispatcher Dispatcher { get; } = new();
    public bool IsRunning { get; private set; }

    protected Application()
    {
        _current = this;
    }

    public void Run()
    {
        if (IsRunning) throw new InvalidOperationException("Application already running");
        IsRunning = true;
        OnStart();
        RunCore();
        OnExit();
        IsRunning = false;
    }

    public void Shutdown()
    {
        IsRunning = false;
    }

    protected abstract void RunCore();

    protected virtual void OnStart() { }
    protected virtual void OnExit() { }
}