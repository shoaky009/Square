namespace Square.Platform.Win32;

public sealed class Win32PlatformFactory : IPlatformFactory, IPlatformScreenshotProvider
{
    public string Name => "Win32";

    public IPlatformHost CreateHost(PlatformHostCreateInfo info)
    {
        return new Win32Host(info);
    }

    public bool TryCaptureByProcessId(int processId, out Graphics.Bitmap? bitmap)
    {
        return Win32WindowScreenshot.TryCaptureByProcessId(processId, out bitmap);
    }
}
