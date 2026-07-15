using System.Collections.Generic;

namespace Square.Graphics;

public static class RenderBackendRegistry
{
    private static readonly Dictionary<string, IRenderBackendFactory> _factories = new();
    private static IRenderBackendFactory? _default;

    public static void Register(IRenderBackendFactory factory)
    {
        _factories[factory.Name] = factory;
        _default ??= factory;
    }

    public static IRenderBackendFactory Get(string name)
    {
        if (_factories.TryGetValue(name, out var f)) return f;
        throw new KeyNotFoundException($"Render backend '{name}' not registered");
    }

    public static IRenderBackendFactory Default =>
        _default ?? throw new InvalidOperationException("No render backend registered");

    public static bool TryGet(string name, out IRenderBackendFactory? factory) =>
        _factories.TryGetValue(name, out factory);

    public static IReadOnlyCollection<string> AvailableNames => _factories.Keys;
}