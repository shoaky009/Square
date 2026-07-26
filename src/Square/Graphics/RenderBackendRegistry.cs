using System.Collections.Generic;

namespace Square.Graphics;

/// <summary>渲染后端注册表（进程全局，线程安全）。</summary>
public static class RenderBackendRegistry
{
    private static readonly Dictionary<string, IRenderBackendFactory> _factories = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();
    private static IRenderBackendFactory? _default;

    /// <summary>注册后端工厂。</summary>
    public static void Register(IRenderBackendFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (string.IsNullOrWhiteSpace(factory.Name))
            throw new ArgumentException("Render backend name cannot be empty", nameof(factory));

        lock (Gate)
        {
            _factories[factory.Name] = factory;
            if (_default == null || string.Equals(_default.Name, factory.Name, StringComparison.OrdinalIgnoreCase))
                _default = factory;
        }
    }

    /// <summary>按名称获取后端工厂。</summary>
    /// <exception cref="KeyNotFoundException">未注册该名称。</exception>
    public static IRenderBackendFactory Get(string name)
    {
        lock (Gate)
        {
            if (_factories.TryGetValue(name, out var f)) return f;
            throw new KeyNotFoundException($"Render backend '{name}' not registered");
        }
    }

    /// <summary>默认后端工厂。</summary>
    public static IRenderBackendFactory Default
    {
        get
        {
            lock (Gate) return _default ?? throw new InvalidOperationException("No render backend registered");
        }
    }

    /// <summary>设置默认后端。</summary>
    public static void SetDefault(string name)
    {
        lock (Gate)
        {
            if (!_factories.TryGetValue(name, out var factory))
                throw new KeyNotFoundException($"Render backend '{name}' not registered");
            _default = factory;
        }
    }

    /// <summary>尝试按名称获取后端工厂。</summary>
    public static bool TryGet(string name, out IRenderBackendFactory? factory)
    {
        lock (Gate) return _factories.TryGetValue(name, out factory);
    }

    /// <summary>已注册的后端名称集合。</summary>
    public static IReadOnlyCollection<string> AvailableNames
    {
        get
        {
            lock (Gate) return _factories.Keys.ToArray();
        }
    }
}