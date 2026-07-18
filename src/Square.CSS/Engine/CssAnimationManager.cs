using Square.UI;

namespace Square.CSS.Engine;

public sealed class CssAnimationManager
{
    private readonly CssEngine _engine;
    private readonly List<CssAnimationTimeline> _timelines = [];

    public CssAnimationManager(CssEngine engine)
    {
        _engine = engine;
    }

    public bool HasRunningAnimations => _timelines.Any(timeline => !timeline.IsComplete);

    public void Attach(Element root)
    {
        _timelines.Clear();
        Collect(root);
        foreach (var timeline in _timelines)
            timeline.Start();
    }

    public void Tick(float deltaSeconds)
    {
        foreach (var timeline in _timelines.Where(timeline => !timeline.IsComplete).ToArray())
            timeline.Tick(deltaSeconds);
    }

    private void Collect(Element Element)
    {
        var timeline = _engine.CreateAnimationTimeline(Element);
        if (timeline != null) _timelines.Add(timeline);
        foreach (var child in Element.Children)
            Collect(child);
    }
}
