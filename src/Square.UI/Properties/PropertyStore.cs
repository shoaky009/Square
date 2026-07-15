namespace Square.UI.Properties;

public sealed class PropertyStore
{
    private Dictionary<string, object?>? _values;
    private HashSet<string>? _boundProperties;

    public bool TryGetValue<T>(string name, out T value)
    {
        if (_values != null && _values.TryGetValue(name, out var v) && v is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }

    public void SetValue<T>(string name, T value)
    {
        _values ??= [];
        _values[name] = value;
    }

    public bool HasValue(string name) => _values?.ContainsKey(name) ?? false;

    public void RemoveValue(string name) => _values?.Remove(name);

    public void MarkBound(string name)
    {
        _boundProperties ??= [];
        _boundProperties.Add(name);
    }

    public bool IsBound(string name) => _boundProperties?.Contains(name) ?? false;

    public IEnumerable<string> GetBoundProperties() => _boundProperties ?? [];
}