namespace Square.UI;

public delegate void RenderFragment(Visual parent);

public sealed class SlotCollection
{
    private readonly Dictionary<string, RenderFragment> _fragments = new(StringComparer.Ordinal);
    private readonly HashSet<string> _rendered = new(StringComparer.Ordinal);

    public void Set(string? name, RenderFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        var key = NormalizeName(name);
        if (_rendered.Contains(key))
            throw new InvalidOperationException($"Slot '{DisplayName(key)}' has already been rendered.");
        _fragments[key] = fragment;
    }

    public bool Contains(string? name) => _fragments.ContainsKey(NormalizeName(name));

    public bool Render(string? name, Visual parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        var key = NormalizeName(name);
        if (!_fragments.TryGetValue(key, out var fragment)) return false;
        if (!_rendered.Add(key))
            throw new InvalidOperationException($"Slot '{DisplayName(key)}' can only be rendered once.");
        fragment(parent);
        return true;
    }

    private static string NormalizeName(string? name) => name?.Trim() ?? "";
    private static string DisplayName(string name) => name.Length == 0 ? "default" : name;
}
