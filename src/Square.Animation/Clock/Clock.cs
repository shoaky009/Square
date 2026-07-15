namespace Square.Animation.Clock;

public sealed class Clock
{
    private long _startTicks;
    private long _lastTicks;
    private bool _running;

    public void Start()
    {
        _startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        _lastTicks = _startTicks;
        _running = true;
    }

    public void Stop() => _running = false;

    public double ElapsedSeconds
    {
        get
        {
            if (!_running) return 0;
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            return (now - _startTicks) / (double)System.Diagnostics.Stopwatch.Frequency;
        }
    }

    public double DeltaSeconds
    {
        get
        {
            if (!_running) return 0;
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            var delta = (now - _lastTicks) / (double)System.Diagnostics.Stopwatch.Frequency;
            _lastTicks = now;
            return delta;
        }
    }
}