namespace Square.UI.ElementApi;

public sealed class StyleAccessor
{
    private readonly Visual _owner;
    private Dictionary<string, string>? _inlineStyles;

    internal StyleAccessor(Visual owner) { _owner = owner; }

    public void Set(string property, string value)
    {
        _inlineStyles ??= [];
        _inlineStyles[property] = value;
        _owner.InvalidateVisual();
    }

    public string? Get(string property)
    {
        if (_inlineStyles != null && _inlineStyles.TryGetValue(property, out var v))
            return v;
        return null;
    }

    public void Remove(string property)
    {
        if (_inlineStyles == null) return;
        _inlineStyles.Remove(property);
        _owner.InvalidateVisual();
    }

    public void Clear()
    {
        if (_inlineStyles == null) return;
        _inlineStyles.Clear();
        _owner.InvalidateVisual();
    }

    public IReadOnlyDictionary<string, string> GetAll() => _inlineStyles ?? [];
}