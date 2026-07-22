namespace Square.Platform.X11;

public sealed class X11PlatformFactory : IPlatformFactory, IPlatformScreenshotProvider
{
    public string Name => "X11";

    public IPlatformHost CreateHost(PlatformHostCreateInfo info)
    {
        return new X11Host(info);
    }

    public bool TryCaptureByProcessId(int processId, out Graphics.Bitmap? bitmap)
    {
        return X11WindowScreenshot.TryCaptureByProcessId(processId, out bitmap);
    }
}
