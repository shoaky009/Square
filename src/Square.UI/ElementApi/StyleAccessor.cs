namespace Square.UI.ElementApi;

public sealed class StyleAccessor
{
    private readonly Visual _owner;
    private Dictionary<string, StyleEntry>? _styles;

    internal StyleAccessor(Visual owner) { _owner = owner; }

    public void Set(string property, string value)
    {
        SetCascaded(property, value, int.MaxValue);
    }

    public bool SetCascaded(string property, string value, int specificity)
    {
        _styles ??= [];
        if (_styles.TryGetValue(property, out var current) && current.Specificity > specificity)
            return false;
        _styles[property] = new StyleEntry(value, specificity);
        _owner.InvalidateVisual();
        return true;
    }

    public string? Get(string property)
    {
        if (_styles != null && _styles.TryGetValue(property, out var entry))
            return entry.Value;
        return null;
    }

    public void Remove(string property)
    {
        if (_styles == null) return;
        _styles.Remove(property);
        _owner.InvalidateVisual();
    }

    public void Clear()
    {
        if (_styles == null) return;
        _styles.Clear();
        _owner.InvalidateVisual();
    }

    public void ClearCascaded()
    {
        if (_styles == null) return;
        foreach (var property in _styles.Where(pair => pair.Value.Specificity < int.MaxValue).Select(pair => pair.Key).ToArray())
            _styles.Remove(property);
        _owner.InvalidateVisual();
    }

    public IReadOnlyDictionary<string, string> GetAll() => _styles == null
        ? new Dictionary<string, string>()
        : _styles.ToDictionary(pair => pair.Key, pair => pair.Value.Value);

    private readonly record struct StyleEntry(string Value, int Specificity);
}
