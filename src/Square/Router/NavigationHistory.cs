namespace Square.Router;

/// <summary>导航历史接口。</summary>
public interface INavigationHistory
{
    /// <summary>当前位置。</summary>
    string Current { get; }
    /// <summary>能否后退。</summary>
    bool CanGoBack { get; }
    /// <summary>能否前进。</summary>
    bool CanGoForward { get; }
    /// <summary>历史变化事件。</summary>
    event Action<string>? Changed;

    /// <summary>压入新位置。</summary>
    void Push(string location);
    /// <summary>替换当前位置。</summary>
    void Replace(string location);
    /// <summary>后退。</summary>
    /// <returns>成功返回 true。</returns>
    bool Back();
    /// <summary>前进。</summary>
    /// <returns>成功返回 true。</returns>
    bool Forward();
}

/// <summary>内存导航历史实现。</summary>
public sealed class MemoryNavigationHistory : INavigationHistory
{
    private readonly List<string> _entries;
    private int _index;

    /// <summary>构造内存导航历史。</summary>
    public MemoryNavigationHistory(string initialLocation = "/")
    {
        _entries = [Normalize(initialLocation)];
    }

    /// <inheritdoc/>
    public string Current => _entries[_index];
    /// <inheritdoc/>
    public bool CanGoBack => _index > 0;
    /// <inheritdoc/>
    public bool CanGoForward => _index + 1 < _entries.Count;
    /// <inheritdoc/>
    public event Action<string>? Changed;

    /// <inheritdoc/>
    public void Push(string location)
    {
        location = Normalize(location);
        if (_index + 1 < _entries.Count) _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
        _entries.Add(location);
        _index++;
        Changed?.Invoke(Current);
    }

    /// <inheritdoc/>
    public void Replace(string location)
    {
        _entries[_index] = Normalize(location);
        Changed?.Invoke(Current);
    }

    /// <inheritdoc/>
    public bool Back()
    {
        if (!CanGoBack) return false;
        _index--;
        Changed?.Invoke(Current);
        return true;
    }

    /// <inheritdoc/>
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
