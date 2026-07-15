namespace Square.Platform.Win32;

public sealed class Win32PlatformFactory : IPlatformFactory
{
    public string Name => "Win32";

    public IPlatformHost CreateHost(PlatformHostCreateInfo info)
    {
        return new Win32Host(info);
    }
}