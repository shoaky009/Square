using Square.Graphics;

namespace Square.Platform;

public static class PlatformScreenshot
{
    public static Bitmap CaptureByProcessId(int processId)
    {
        if (processId <= 0)
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be greater than zero.");

        if (TryCaptureByProcessId(processId, out var bitmap) && bitmap is not null)
            return bitmap;

        throw new InvalidOperationException($"No capturable top-level window found for process id {processId}.");
    }

    public static bool TryCaptureByProcessId(int processId, out Bitmap? bitmap)
    {
        bitmap = null;
        if (processId <= 0)
            return false;

#if PLATFORM_WIN32
        return Win32.Win32WindowScreenshot.TryCaptureByProcessId(processId, out bitmap);
#elif PLATFORM_X11
        return X11.X11WindowScreenshot.TryCaptureByProcessId(processId, out bitmap);
#else
        return false;
#endif
    }
}
