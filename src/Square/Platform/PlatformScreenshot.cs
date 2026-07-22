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

        return PlatformRegistry.TryGet(out var factory)
            && factory is IPlatformScreenshotProvider screenshotProvider
            && screenshotProvider.TryCaptureByProcessId(processId, out bitmap);
    }
}
