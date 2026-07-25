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
            var value = track.ValueAt(progress);
            _visual.Style.SetAnimated(track.Property, FormatNumber(value));
        }
    }

    private static List<AnimationTrack> BuildTracks(KeyFramesRule keyFrames)
    {
        var values = new Dictionary<string, List<AnimationStop>>(StringComparer.OrdinalIgnoreCase);
        foreach (var stop in keyFrames.Stops)
        {
            if (!TryParseProgress(stop.Selector, out var progress)) continue;
            foreach (var declaration in stop.Declarations)
            {
                if (!TryParseFloat(declaration.Value, out var value)) continue;
                if (!values.TryGetValue(declaration.Property, out var stops))
                    values.Add(declaration.Property, stops = []);
                stops.Add(new AnimationStop(progress, value));
            }
        }
        return values
            .Where(pair => pair.Value.Count >= 2)
            .Select(pair => new AnimationTrack(pair.Key, pair.Value.OrderBy(stop => stop.Progress).ToArray()))
            .ToList();
    }

    private static bool TryParseProgress(string selector, out float progress)
    {
        if (string.Equals(selector, "from", StringComparison.OrdinalIgnoreCase))
        {
            progress = 0;
            return true;
        }
        if (string.Equals(selector, "to", StringComparison.OrdinalIgnoreCase))
        {
            progress = 1;
            return true;
        }
        if (selector.EndsWith("%", StringComparison.Ordinal) &&
            float.TryParse(selector[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
        {
            progress = Math.Clamp(percent / 100f, 0f, 1f);
            return true;
        }
        progress = 0;
        return false;
    }

    private static bool TryParseFloat(string value, out float result)
    {
        var text = value.Trim();
        if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase)) text = text[..^2];
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static string FormatNumber(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private sealed record AnimationTrack(string Property, AnimationStop[] Stops)
    {
        public float ValueAt(float progress)
        {
            if (progress <= Stops[0].Progress) return Stops[0].Value;
            for (var index = 1; index < Stops.Length; index++)
            {
                var end = Stops[index];
                if (progress > end.Progress) continue;
                var start = Stops[index - 1];
                var range = end.Progress - start.Progress;
                return range <= 0 ? end.Value : start.Value + (end.Value - start.Value) * ((progress - start.Progress) / range);
            }
            return Stops[^1].Value;
        }
    }

    private readonly record struct AnimationStop(float Progress, float Value);
    private enum AnimationDirection { Normal, Reverse, Alternate, AlternateReverse }

    private static AnimationDirection ParseDirection(string value) => value.Trim().ToLowerInvariant() switch
    {
        "reverse" => AnimationDirection.Reverse,
        "alternate" => AnimationDirection.Alternate,
        "alternate-reverse" => AnimationDirection.AlternateReverse,
        _ => AnimationDirection.Normal
    };
}
