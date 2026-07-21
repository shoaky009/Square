using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Loader;
using Silk.NET.Vulkan;

namespace Square.Backends.Vulkan;

internal static class VulkanApi
{
    public static Vk Create()
    {
        var library = new UnmanagedLibrary(GetLibraryNames(), NativeLibraryLoader.Instance, PassthroughPathResolver.Instance);
        return new Vk(new DefaultNativeContext(library));
    }

    internal static string[] GetLibraryNames()
        => OperatingSystem.IsWindows()
            ? ["vulkan-1.dll"]
            : ["libvulkan.so.1", "libvulkan.so"];

    private sealed class NativeLibraryLoader : LibraryLoader
    {
        public static NativeLibraryLoader Instance { get; } = new();

        protected override nint CoreLoadNativeLibrary(string name)
            => NativeLibrary.TryLoad(name, out var handle) ? handle : 0;

        protected override void CoreFreeNativeLibrary(nint handle)
            => NativeLibrary.Free(handle);

        protected override nint CoreLoadFunctionPointer(nint handle, string functionName)
            => NativeLibrary.TryGetExport(handle, functionName, out var address) ? address : 0;
    }

    private sealed class PassthroughPathResolver : PathResolver
    {
        public static PassthroughPathResolver Instance { get; } = new();

        public override IEnumerable<string> EnumeratePossibleLibraryLoadTargets(string name)
        {
            yield return name;
        }
    }
}
