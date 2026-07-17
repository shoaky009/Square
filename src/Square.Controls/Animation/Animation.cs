namespace Square.Controls.Animation;

public sealed class Animation<T>
{
    private readonly Func<float, T> _interpolate;
    private readonly float _duration;
    private readonly Func<float, float> _easing;
    private readonly Action<T> _onUpdate;
    private float _elapsed;
    private bool _running;

    public bool IsComplete => _elapsed >= _duration;

    public Animation(Func<T, T, float, T> interpolate, T from, T to, float duration, Func<float, float> easing, Action<T> onUpdate)
    {
        _interpolate = t => interpolate(from, to, t);
        _duration = duration;
        _easing = easing;
        _onUpdate = onUpdate;
    }

    public void Start() { _elapsed = 0; _running = true; }
    public void Stop() => _running = false;

    public void Update(float deltaSeconds)
    {
        if (!_running) return;
        _elapsed += deltaSeconds;
        var t = Math.Clamp(_elapsed / _duration, 0f, 1f);
        _onUpdate(_interpolate(_easing(t)));
        if (t >= 1f) _running = false;
    }
}
