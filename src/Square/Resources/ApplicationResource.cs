using System.Reflection;

namespace Square.Resources;

/// <summary>应用程序资源访问入口。</summary>
public static class ApplicationResource
{
    /// <summary>打开入口程序集的资源流。</summary>
    public static Stream Open(string path)
        => Open(path, Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly());

    /// <summary>打开指定程序集的资源流。</summary>
    /// <exception cref="FileNotFoundException">资源未找到。</exception>
    public static Stream Open(string path, Assembly assetAssembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(assetAssembly);
        if (Path.IsPathRooted(path)) return File.OpenRead(path);

        var relativePath = NormalizeRelativePath(path);
        var filePath = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(filePath)) return File.OpenRead(filePath);

        var suffix = ".Assets." + relativePath.Replace('/', '.');
        var resourceName = assetAssembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(suffix, StringComparison.Ordinal));
        return resourceName == null
            ? throw new FileNotFoundException($"Application resource '{path}' was not found.", path)
            : assetAssembly.GetManifestResourceStream(resourceName)
              ?? throw new FileNotFoundException($"Application resource '{path}' could not be opened.", path);
    }

    /// <summary>读取入口程序集资源的全部字节。</summary>
    public static byte[] ReadAllBytes(string path)
    {
        using var stream = Open(path);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>读取指定程序集资源的全部字节。</summary>
    public static byte[] ReadAllBytes(string path, Assembly assetAssembly)
    {
        using var stream = Open(path, assetAssembly);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string NormalizeRelativePath(string path)
    {
        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment == ".."))
            throw new ArgumentException("Resource paths must stay within the application root.", nameof(path));
        return string.Join('/', segments.Where(segment => segment != "."));
    }
}