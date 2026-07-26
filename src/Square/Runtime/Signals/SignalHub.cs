using System.Collections.Concurrent;

namespace Square.Runtime.Signals;

/// <summary>按名称集中管理信号的集线器。</summary>
public sealed class SignalHub
{
    private readonly ConcurrentDictionary<string, object> _signals = new(StringComparer.Ordinal);

    /// <summary>默认共享实例。</summary>
    public static SignalHub Default { get; } = new();

    /// <summary>按名称获取或创建信号；类型不匹配时抛出异常。</summary>
    public Signal<T> Get<T>(string name, T initialValue = default!)
    {
        var key = NormalizeName(name);
        var signal = _signals.GetOrAdd(key, _ => new Signal<T>(initialValue));
        if (signal is Signal<T> typed) return typed;

        throw new InvalidOperationException(
            $"Signal '{key}' is already registered with a different value type.");
    }

    /// <summary>按名称移除信号；类型不匹配时抛出异常。</summary>
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