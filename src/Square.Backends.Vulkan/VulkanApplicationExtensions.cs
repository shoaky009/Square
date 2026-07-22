using Square.Graphics;

namespace Square.Backends.Vulkan;

public static class VulkanApplicationExtensions
{
    public static T UseVulkanBackend<T>(this T window)
        where T : IRenderBackendApplication
    {
        ArgumentNullException.ThrowIfNull(window);
        VulkanRegistration.Register();
        window.RenderBackend = "Vulkan";
        return window;
    }
}
