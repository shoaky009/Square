namespace Square.Controls.Animation;

/// <summary>驱动单值在指定时长内按缓动函数从起始值过渡到结束值的动画。</summary>
public sealed class Animation<T>
{
    private readonly Func<float, T> _interpolate;
    private readonly float _duration;
    private readonly Func<float, float> _easing;
    private readonly Action<T> _onUpdate;
    private float _elapsed;
    private bool _running;

    /// <summary>动画是否已完成。</summary>
    public bool IsComplete => _elapsed >= _duration;

    /// <summary>初始化 <see cref="Animation{T}"/> 的新实例。</summary>
    public Animation(Func<T, T, float, T> interpolate, T from, T to, float duration, Func<float, float> easing, Action<T> onUpdate)
    {
        _interpolate = t => interpolate(from, to, t);
        _duration = duration;
        _easing = easing;
        _onUpdate = onUpdate;
    }

    /// <summary>开始或重新开始动画。</summary>
    public void Start() { _elapsed = 0; _running = true; }
    /// <summary>停止动画。</summary>
    public void Stop() => _running = false;

    /// <summary>推进动画时间，触发更新回调。</summary>
    public void Update(float deltaSeconds)
    {
        if (!_running) return;
        _elapsed += deltaSeconds;
        var t = Math.Clamp(_elapsed / _duration, 0f, 1f);
        _onUpdate(_interpolate(_easing(t)));
        if (t >= 1f) _running = false;
    }
}
