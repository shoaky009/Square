namespace Square.Graphics;

/// <summary>Provides decoded still-image or animation frames to controls.</summary>
public interface IImageFrameSource : IDisposable
{
    int Width { get; }
    int Height { get; }
    int FrameCount { get; }
    int PlayCount { get; }
    Bitmap GetFrame(int index);
    TimeSpan GetFrameDuration(int index);
}

/// <summary>Loads image frame sources without coupling Square core to a codec package.</summary>
public interface IImageSourceLoader
{
    bool CanLoad(string source);
    ValueTask<IImageFrameSource> LoadAsync(string source, CancellationToken cancellationToken = default);
}

/// <summary>图像源加载器注册表：维护可用加载器并按源字符串分发加载请求。</summary>
public static class ImageSourceLoaderRegistry
{
    private static readonly object Sync = new();
    private static readonly List<IImageSourceLoader> Loaders = [];

    /// <summary>注册一个图像源加载器（重复注册会被忽略）。</summary>
    public static void Register(IImageSourceLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        lock (Sync)
        {
            if (!Loaders.Contains(loader)) Loaders.Add(loader);
        }
    }

    /// <summary>移除已注册的加载器，返回是否成功移除。</summary>
    public static bool Unregister(IImageSourceLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        lock (Sync) return Loaders.Remove(loader);
    }

    /// <summary>按源字符串加载图像帧源；若无加载器可处理则抛出 <see cref="NotSupportedException"/>。</summary>
    public static async ValueTask<IImageFrameSource> LoadAsync(string source,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        IImageSourceLoader[] loaders;
        lock (Sync) loaders = [.. Loaders];

        foreach (var loader in loaders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (loader.CanLoad(source)) return await loader.LoadAsync(source, cancellationToken).ConfigureAwait(false);
        }

        throw new NotSupportedException($"No image source loader is registered for '{source}'.");
    }
}
