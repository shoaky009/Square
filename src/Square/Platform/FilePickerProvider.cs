namespace Square.Platform;

internal interface IFilePickerProvider
{
    IReadOnlyList<string> OpenFiles(IPlatformHost host, OpenFilePickerOptions options);
}

internal static class FilePickerProvider
{
    public static IFilePickerProvider? Current { get; set; }

    public static IReadOnlyList<string> OpenFiles(IPlatformHost host, OpenFilePickerOptions options)
    {
        var provider = Current ?? throw new PlatformNotSupportedException(
            "No file picker provider is registered. Reference Square.Extensions and call ExtensionRegistration.RegisterDefaults().");
        return provider.OpenFiles(host, options);
    }
}
