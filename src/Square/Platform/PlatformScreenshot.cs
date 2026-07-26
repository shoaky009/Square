using Square.Graphics;

namespace Square.Platform;

/// <summary>平台截图辅助方法。</summary>
public static class PlatformScreenshot
{
    /// <summary>按进程 ID 截取窗口位图。</summary>
    /// <exception cref="InvalidOperationException">找不到可截取的窗口。</exception>
    public static Bitmap CaptureByProcessId(int processId)
    {
        if (processId <= 0)
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be greater than zero.");

        if (TryCaptureByProcessId(processId, out var bitmap) && bitmap is not null)
            return bitmap;

        throw new InvalidOperationException($"No capturable top-level window found for process id {processId}.");
    }

    /// <summary>尝试按进程 ID 截取窗口位图。</summary>
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