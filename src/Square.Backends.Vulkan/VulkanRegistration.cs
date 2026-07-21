using Square.Graphics;

namespace Square.Backends.Vulkan;

/// <summary>
/// Static registration helper for the Vulkan backend.
/// Usage: VulkanRegistration.Register();
/// </summary>
public static class VulkanRegistration
{
    public static void Register()
        => RenderBackendRegistry.Register(new VulkanBackendFactory());
}
