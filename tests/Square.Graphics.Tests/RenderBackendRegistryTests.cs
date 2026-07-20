using Square.Graphics;
using Xunit;

namespace Square.Graphics.Tests;

public class RenderBackendRegistryTests
{
    [Fact]
    public void SetDefaultSelectsRegisteredBackendByName()
    {
        var first = new StubFactory("RegistryTestFirst");
        var second = new StubFactory("RegistryTestSecond");
        RenderBackendRegistry.Register(first);
        RenderBackendRegistry.Register(second);

        RenderBackendRegistry.SetDefault(second.Name);

        Assert.Same(second, RenderBackendRegistry.Default);
    }

    [Fact]
    public void ReplacingDefaultBackendUpdatesDefaultInstance()
    {
        var name = "RegistryTestReplace";
        var original = new StubFactory(name);
        var replacement = new StubFactory(name);
        RenderBackendRegistry.Register(original);
        RenderBackendRegistry.SetDefault(name);

        RenderBackendRegistry.Register(replacement);

        Assert.Same(replacement, RenderBackendRegistry.Default);
        Assert.Same(replacement, RenderBackendRegistry.Get(name));
    }

    private sealed class StubFactory(string name) : IRenderBackendFactory
    {
        public string Name { get; } = name;

        public IRenderContext CreateContext(RenderContextCreateInfo info)
            => throw new NotSupportedException();
    }
}
