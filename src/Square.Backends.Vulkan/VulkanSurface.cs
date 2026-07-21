using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Square.Graphics;

namespace Square.Backends.Vulkan;

/// <summary>
/// Creates Vulkan surfaces from platform native render targets.
/// </summary>
internal static unsafe class VulkanSurface
{
    public static SurfaceKHR Create(VulkanDevice device, INativeRenderTarget target)
    {
        return target switch
        {
            Win32VulkanRenderTarget win32 => CreateWin32(device, win32),
            X11VulkanRenderTarget x11 => CreateX11(device, x11),
            _ => throw new VulkanException($"Unsupported native render target '{target.Kind}'.")
        };
    }

    private static SurfaceKHR CreateWin32(VulkanDevice device, Win32VulkanRenderTarget target)
    {
        if (!device.Api.TryGetInstanceExtension(device.Instance, out KhrWin32Surface ext))
            throw new VulkanException("VK_KHR_win32_surface extension not available.");

        var createInfo = new Win32SurfaceCreateInfoKHR(StructureType.Win32SurfaceCreateInfoKhr)
        {
            Hinstance = target.InstanceHandle,
            Hwnd = target.WindowHandle
        };

        var result = ext.CreateWin32Surface(device.Instance, in createInfo, null, out var surface);
        VulkanDevice.ThrowIfFailed(result, "vkCreateWin32SurfaceKHR");
        return surface;
    }

    private static SurfaceKHR CreateX11(VulkanDevice device, X11VulkanRenderTarget target)
    {
        if (!device.Api.TryGetInstanceExtension(device.Instance, out KhrXlibSurface ext))
            throw new VulkanException("VK_KHR_xlib_surface extension not available.");

        var createInfo = new XlibSurfaceCreateInfoKHR(StructureType.XlibSurfaceCreateInfoKhr)
        {
            Dpy = (nint*)target.DisplayHandle,
            Window = (nint)target.WindowHandle
        };

        var result = ext.CreateXlibSurface(device.Instance, in createInfo, null, out var surface);
        VulkanDevice.ThrowIfFailed(result, "vkCreateXlibSurfaceKHR");
        return surface;
    }

    public static void Destroy(VulkanDevice device, SurfaceKHR surface)
    {
        if (surface.Handle != 0)
            device.KhrSurface.DestroySurface(device.Instance, surface, null);
    }
}
