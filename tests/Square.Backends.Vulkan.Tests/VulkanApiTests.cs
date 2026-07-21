using Xunit;

namespace Square.Backends.Vulkan.Tests;

public sealed class VulkanApiTests
{
    [Fact]
    public void LibraryNamesUseSystemVulkanLoader()
    {
        var names = VulkanApi.GetLibraryNames();

        if (OperatingSystem.IsWindows())
            Assert.Equal(["vulkan-1.dll"], names);
        else
            Assert.Equal(["libvulkan.so.1", "libvulkan.so"], names);
    }
}
