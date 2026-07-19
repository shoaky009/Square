using Square.Graphics;

namespace Square.Platform.X11;

internal static class X11WindowScreenshot
{
    public static bool TryCaptureByProcessId(int processId, out Bitmap? bitmap)
    {
        bitmap = null;
        return false;
    }
}
