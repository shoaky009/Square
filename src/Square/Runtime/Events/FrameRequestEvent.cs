namespace Square.Events;

/// <summary>
/// 请求下一帧渲染的事件（Square 扩展类型 <c>requestframe</c>，非标准 DOM）。
/// 由 Canvas 等控件派发，宿主合并调度。
/// </summary>
public sealed class FrameRequestEvent : Event
{
    /// <summary>创建帧请求；<paramref name="framesPerSecond"/> 限制在 1–240。</summary>
    public FrameRequestEvent(double framesPerSecond = 60d)
        : base(StandardEvents.RequestFrame, new EventInit { Bubbles = true, Cancelable = false })
    {
        FramesPerSecond = Math.Clamp(framesPerSecond, 1d, 240d);
        Delay = TimeSpan.FromSeconds(1d / FramesPerSecond);
    }

    public FrameRequestEvent(TimeSpan delay)
        : base(StandardEvents.RequestFrame, new EventInit { Bubbles = true, Cancelable = false })
    {
        if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay));
        Delay = delay;
        FramesPerSecond = delay > TimeSpan.Zero ? 1d / delay.TotalSeconds : 240d;
    }

    /// <summary>期望帧率。</summary>
    public double FramesPerSecond { get; }

    /// <summary>帧间隔秒数（1 / FramesPerSecond）。</summary>
    public double IntervalSeconds => Delay.TotalSeconds;

    /// <summary>请求距离当前时刻的精确延迟。</summary>
    public TimeSpan Delay { get; }
}
