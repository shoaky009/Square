using System.Globalization;
using Square.CSS.Ast;
using Square.UI;

namespace Square.CSS.Engine;

public sealed class CssAnimationTimeline
{
    private readonly Element _visual;
    private readonly List<AnimationTrack> _tracks;
    private readonly float _duration;
    private readonly float _delay;
    private readonly int _iterationCount;
    private readonly AnimationDirection _direction;
    private readonly Func<float, float> _easing;
    private float _elapsed;
    private bool _running;

    internal CssAnimationTimeline(Element Element, KeyFramesRule keyFrames, float duration, Func<float, float> easing, float delay = 0, int iterationCount = 1, string direction = "normal")
    {
        _visual = Element;
        _duration = Math.Max(0.0001f, duration);
        _delay = Math.Max(0, delay);
        _iterationCount = Math.Max(1, iterationCount);
        _direction = ParseDirection(direction);
        _easing = easing;
        _tracks = BuildTracks(keyFrames);
    }

    public bool IsComplete => _elapsed >= _delay + _duration * _iterationCount;

    public void Start()
    {
        _elapsed = 0;
        _running = true;
        Apply(GetDirectedProgress(0));
    }

    public void Tick(float deltaSeconds)
    {
        if (!_running) return;
        _elapsed = Math.Min(_delay + _duration * _iterationCount, _elapsed + Math.Max(0, deltaSeconds));
        if (_elapsed >= _delay)
            Apply(_easing(GetDirectedProgress(_elapsed - _delay)));
        if (IsComplete) _running = false;
    }

    private float GetDirectedProgress(float activeElapsed)
    {
        if (_iterationCount != int.MaxValue && activeElapsed >= _duration * _iterationCount)
            activeElapsed = _duration * _iterationCount;
        var iteration = Math.Min(_iterationCount - 1, (int)MathF.Floor(activeElapsed / _duration));
        var local = Math.Clamp((activeElapsed - iteration * _duration) / _duration, 0f, 1f);
        if (_iterationCount != int.MaxValue && activeElapsed >= _duration * _iterationCount)
            local = 1f;
        var reverse = _direction switch
        {
            AnimationDirection.Reverse => true,
            AnimationDirection.Alternate => iteration % 2 == 1,
            AnimationDirection.AlternateReverse => iteration % 2 == 0,
            _ => false
        };
        return reverse ? 1f - local : local;
    }

    private void Apply(float progress)
    {
        foreach (var track in _tracks)
        {
            var value = track.From + (track.To - track.From) * progress;
            _visual.Style.Set(track.Property, FormatNumber(value));
        }
    }

    private static List<AnimationTrack> BuildTracks(KeyFramesRule keyFrames)
    {
        var from = FindStop(keyFrames, "from") ?? FindStop(keyFrames, "0%") ?? keyFrames.Stops.FirstOrDefault();
        var to = FindStop(keyFrames, "to") ?? FindStop(keyFrames, "100%") ?? keyFrames.Stops.LastOrDefault();
        if (from == null || to == null) return [];

        var tracks = new List<AnimationTrack>();
        foreach (var start in from.Declarations)
        {
            var end = to.Declarations.FirstOrDefault(d => string.Equals(d.Property, start.Property, StringComparison.OrdinalIgnoreCase));
            if (end == null) continue;
            if (!TryParseFloat(start.Value, out var startValue) || !TryParseFloat(end.Value, out var endValue)) continue;
            tracks.Add(new AnimationTrack(start.Property, startValue, endValue));
        }
        return tracks;
    }

    private static KeyFrameStop? FindStop(KeyFramesRule keyFrames, string selector) =>
        keyFrames.Stops.FirstOrDefault(stop => string.Equals(stop.Selector, selector, StringComparison.OrdinalIgnoreCase));

    private static bool TryParseFloat(string value, out float result)
    {
        var text = value.Trim();
        if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase)) text = text[..^2];
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static string FormatNumber(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private sealed record AnimationTrack(string Property, float From, float To);
    private enum AnimationDirection { Normal, Reverse, Alternate, AlternateReverse }

    private static AnimationDirection ParseDirection(string value) => value.Trim().ToLowerInvariant() switch
    {
        "reverse" => AnimationDirection.Reverse,
        "alternate" => AnimationDirection.Alternate,
        "alternate-reverse" => AnimationDirection.AlternateReverse,
        _ => AnimationDirection.Normal
    };
}
