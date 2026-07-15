namespace Square.UI.ElementApi;

public sealed class ClassListAccessor
{
    private readonly Visual _owner;
    private HashSet<string>? _classes;

    internal ClassListAccessor(Visual owner) { _owner = owner; }

    public void Add(string className)
    {
        _classes ??= [];
        if (_classes.Add(className)) _owner.InvalidateVisual();
    }

    public void Remove(string className)
    {
        if (_classes == null) return;
        if (_classes.Remove(className)) _owner.InvalidateVisual();
    }

    public void Toggle(string className)
    {
        if (Contains(className)) Remove(className);
        else Add(className);
    }

    public void Toggle(string className, bool force)
    {
        if (force) Add(className);
        else Remove(className);
    }

    public bool Contains(string className) => _classes?.Contains(className) ?? false;

    public string ToClassString() => _classes == null ? "" : string.Join(' ', _classes);

    public void Clear()
    {
        if (_classes == null) return;
        _classes.Clear();
        _owner.InvalidateVisual();
    }

    public IReadOnlyCollection<string> GetAll() => _classes ?? [];
}
