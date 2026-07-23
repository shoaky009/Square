using Square.Platform;

namespace Square.Extensions;

internal sealed class NativeFilePickerProvider : IFilePickerProvider
{
    public IReadOnlyList<string> OpenFiles(IPlatformHost host, OpenFilePickerOptions options)
    {
#if PLATFORM_WIN32
        if (host is not IPlatformNativeWindow { Handle: not 0 } window)
            throw new InvalidOperationException("The native application window is not available.");
        return Win32OpenFileDialog.Show(window.Handle, options);
#else
        throw new PlatformNotSupportedException("Opening files is not supported by Square.Extensions on X11.");
#endif
    }
}
