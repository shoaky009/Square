namespace Square.UI;

/// <summary>
/// Minimal document selection model. Square currently supports a single active range.
/// </summary>
public sealed class Selection
{
    private readonly List<Range> _ranges = [];

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
        _ranges.Clear();
        _ranges.Add(range);
    }

    public void RemoveAllRanges() => _ranges.Clear();

    public override string ToString() => _ranges.Count == 0 ? string.Empty : _ranges[0].ToString();
}
