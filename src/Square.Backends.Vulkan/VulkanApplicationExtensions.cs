using Square.Graphics;

namespace Square.Backends.Vulkan;

public static class VulkanApplicationExtensions
{
    public static T UseVulkanBackend<T>(this T application)
        where T : IRenderBackendApplication
    {
        ArgumentNullException.ThrowIfNull(application);
        VulkanRegistration.Register();
        application.RenderBackend = "Vulkan";
        return application;
    }
}
