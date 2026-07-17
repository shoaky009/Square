namespace Square.Router;

public interface INavigationHistory
{
    string Current { get; }
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    event Action<string>? Changed;

    void Push(string location);
    void Replace(string location);
    bool Back();
    bool Forward();
}

public sealed class MemoryNavigationHistory : INavigationHistory
{
    private readonly List<string> _entries;
    private int _index;

    public MemoryNavigationHistory(string initialLocation = "/")
    {
        _entries = [Normalize(initialLocation)];
    }

    public string Current => _entries[_index];
    public bool CanGoBack => _index > 0;
    public bool CanGoForward => _index + 1 < _entries.Count;
    public event Action<string>? Changed;

    public void Push(string location)
    {
        location = Normalize(location);
        if (_index + 1 < _entries.Count) _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
        _entries.Add(location);
        _index++;
        Changed?.Invoke(Current);
    }

    public void Replace(string location)
    {
        _entries[_index] = Normalize(location);
        Changed?.Invoke(Current);
    }

    public bool Back()
    {
        if (!CanGoBack) return false;
        _index--;
        Changed?.Invoke(Current);
        return true;
    }

    public bool Forward()
    {
        if (!CanGoForward) return false;
        _index++;
        Changed?.Invoke(Current);
        return true;
    }

    private static string Normalize(string location)
    {
        if (string.IsNullOrWhiteSpace(location)) return "/";
        location = location.Trim();
        return location.StartsWith("/", StringComparison.Ordinal) ? location : "/" + location;
    }
}
