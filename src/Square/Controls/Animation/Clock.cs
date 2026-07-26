namespace Square.Controls.Animation;

/// <summary>基于 <see cref="System.Diagnostics.Stopwatch"/> 的计时器，提供累计与增量秒数。</summary>
public sealed class Clock
{
    private long _startTicks;
    private long _lastTicks;
    private bool _running;

    /// <summary>启动计时器。</summary>
    public void Start()
    {
        _startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        _lastTicks = _startTicks;
        _running = true;
    }

    /// <summary>停止计时器。</summary>
    public void Stop() => _running = false;

    /// <summary>自启动以来的累计秒数。</summary>
    public double ElapsedSeconds
    {
        get
        {
            if (!_running) return 0;
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            return (now - _startTicks) / (double)System.Diagnostics.Stopwatch.Frequency;
        }
    }

    /// <summary>距上次读取的增量秒数（每次读取后更新基准）。</summary>
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
