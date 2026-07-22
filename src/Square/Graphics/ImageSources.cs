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

public static class ImageSourceLoaderRegistry
{
    private static readonly object Sync = new();
    private static readonly List<IImageSourceLoader> Loaders = [];

    public static void Register(IImageSourceLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        lock (Sync)
        {
            if (!Loaders.Contains(loader)) Loaders.Add(loader);
        }
    }

    public static bool Unregister(IImageSourceLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        lock (Sync) return Loaders.Remove(loader);
    }

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
