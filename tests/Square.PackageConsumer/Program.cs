using Square.PackageConsumer;
using Square.Platform;

var component = new Main();
component.BuildElementTree();

if (component.Children.Count != 1)
    throw new InvalidOperationException("The packaged source generator did not build the SQX component.");

if (!component.CodeBehindLoaded)
    throw new InvalidOperationException("The SQX code-behind partial class was not compiled.");

var platform = PlatformRegistry.Get();
#if PLATFORM_WIN32
if (platform.Name != "Win32")
    throw new InvalidOperationException("The Win32 platform package was not automatically registered.");
#elif PLATFORM_X11
if (platform.Name != "X11")
    throw new InvalidOperationException("The X11 platform package was not automatically registered.");
#endif
