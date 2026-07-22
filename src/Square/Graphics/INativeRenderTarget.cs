namespace Square.Graphics;

public enum NativeRenderTargetKind
{
    Win32Vulkan,
    X11Vulkan
}

public interface INativeRenderTarget
{
    NativeRenderTargetKind Kind { get; }
    IntPtr WindowHandle { get; }
    IntPtr DisplayHandle { get; }
    int Screen { get; }
}

public sealed record Win32VulkanRenderTarget(IntPtr WindowHandle, IntPtr InstanceHandle) : INativeRenderTarget
{
    public NativeRenderTargetKind Kind => NativeRenderTargetKind.Win32Vulkan;
    public IntPtr DisplayHandle => InstanceHandle;
    public int Screen => 0;
}

public sealed record X11VulkanRenderTarget(IntPtr DisplayHandle, IntPtr WindowHandle, int Screen) : INativeRenderTarget
{
    public NativeRenderTargetKind Kind => NativeRenderTargetKind.X11Vulkan;
}
