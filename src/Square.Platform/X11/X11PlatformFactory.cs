namespace Square.Platform.X11;

public sealed class X11PlatformFactory : IPlatformFactory
{
    public string Name => "X11";

    public IPlatformHost CreateHost(PlatformHostCreateInfo info)
    {
        return new X11Host(info);
    }
}