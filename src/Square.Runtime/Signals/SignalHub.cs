using System.Collections.Concurrent;

namespace Square.Runtime.Signals;

public sealed class SignalHub
{
    private readonly ConcurrentDictionary<string, object> _signals = new(StringComparer.Ordinal);

    public static SignalHub Default { get; } = new();

    public Signal<T> Get<T>(string name, T initialValue = default!)
    {
        var key = NormalizeName(name);
        var signal = _signals.GetOrAdd(key, _ => new Signal<T>(initialValue));
        if (signal is Signal<T> typed) return typed;

        throw new InvalidOperationException(
            $"Signal '{key}' is already registered with a different value type.");
    }

    public bool Remove<T>(string name)
    {
        var key = NormalizeName(name);
        if (!_signals.TryGetValue(key, out var signal)) return false;
        if (signal is not Signal<T>)
            throw new InvalidOperationException(
                $"Signal '{key}' is registered with a different value type.");
        return _signals.TryRemove(key, out _);
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim();
    }
}
