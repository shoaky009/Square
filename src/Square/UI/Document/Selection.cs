using Square.Events;

namespace Square.UI;

/// <summary>
/// Minimal document selection model. Square currently supports a single active range.
/// </summary>
public sealed class Selection
{
    private readonly Document _document;
    private readonly List<Range> _ranges = [];

    internal Selection(Document document) => _document = document;

    public int RangeCount => _ranges.Count;
    public bool IsCollapsed => _ranges.Count == 0 || _ranges[0].Collapsed;
    public Node? AnchorNode => _ranges.Count == 0 ? null : _ranges[0].StartContainer;
    public int AnchorOffset => _ranges.Count == 0 ? 0 : _ranges[0].StartOffset;
    public Node? FocusNode => _ranges.Count == 0 ? null : _ranges[0].EndContainer;
    public int FocusOffset => _ranges.Count == 0 ? 0 : _ranges[0].EndOffset;

    public Range GetRangeAt(int index) => _ranges[index];

    public void AddRange(Range range)
    {
        ArgumentNullException.ThrowIfNull(range);
        SetRange(range);
    }

    internal void SetRange(Range range)
    {
        _ranges.Clear();
        _ranges.Add(range);
        _document.DispatchEvent(StandardEvents.CreateSelectionChange());
    }

    public void RemoveAllRanges()
    {
        if (_ranges.Count == 0) return;
        _ranges.Clear();
        _document.DispatchEvent(StandardEvents.CreateSelectionChange());
    }

    public override string ToString() => _ranges.Count == 0 ? string.Empty : _ranges[0].ToString();
}
