namespace Square.Backends.Vulkan;

public sealed class VulkanException : Exception
{
    public VulkanException(string message) : base(message) { }
    public VulkanException(string message, Exception inner) : base(message, inner) { }
}
