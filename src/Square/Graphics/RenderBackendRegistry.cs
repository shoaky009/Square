using System.Collections.Generic;

namespace Square.Graphics;

public static class RenderBackendRegistry
{
    private static readonly Dictionary<string, IRenderBackendFactory> _factories = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();
    private static IRenderBackendFactory? _default;

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

    public static IRenderBackendFactory Get(string name)
    {
        lock (Gate)
        {
            if (_factories.TryGetValue(name, out var f)) return f;
            throw new KeyNotFoundException($"Render backend '{name}' not registered");
        }
    }

    public static IRenderBackendFactory Default
    {
        get
        {
            lock (Gate) return _default ?? throw new InvalidOperationException("No render backend registered");
        }
    }

    public static void SetDefault(string name)
    {
        lock (Gate)
        {
            if (!_factories.TryGetValue(name, out var factory))
                throw new KeyNotFoundException($"Render backend '{name}' not registered");
            _default = factory;
        }
    }

    public static bool TryGet(string name, out IRenderBackendFactory? factory)
    {
        lock (Gate) return _factories.TryGetValue(name, out factory);
    }

    public static IReadOnlyCollection<string> AvailableNames
    {
        get
        {
            lock (Gate) return _factories.Keys.ToArray();
        }
    }
}
