using System.Runtime.CompilerServices;
using Square.Graphics;

namespace Square.Images;

internal sealed class ImageDocumentFrameSource(ImageDocument document) : IImageFrameSource
{
    private ImageDocument? _document = document;

    public int Width => GetDocument().PrimaryItem.Width;
    public int Height => GetDocument().PrimaryItem.Height;
    public int FrameCount => GetDocument().Items.Count;
    public int PlayCount => GetDocument().Animation is { } animation
        ? animation.LoopsForever ? 0 : animation.PlayCount
        : 1;

    public Bitmap GetFrame(int index) => GetDocument().GetBitmap(index);
    public TimeSpan GetFrameDuration(int index)
    {
        var current = GetDocument();
        if ((uint)index >= (uint)current.Items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        return current.Items[index].Duration;
    }

    public void Dispose() => Interlocked.Exchange(ref _document, null)?.Dispose();

    private ImageDocument GetDocument() => _document ?? throw new ObjectDisposedException(nameof(ImageDocumentFrameSource));
}

internal sealed class LocalImageSourceLoader : IImageSourceLoader
{
    public bool CanLoad(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        return !Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.IsFile;
    }

    public async ValueTask<IImageFrameSource> LoadAsync(string source, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var path = ResolvePath(source);
        var document = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decoded = ImageDecoder.Decode(path);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return decoded;
            }
            catch
            {
                decoded.Dispose();
                throw;
            }
        }, cancellationToken).ConfigureAwait(false);
        return new ImageDocumentFrameSource(document);
    }

    private static string ResolvePath(string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            if (!uri.IsFile) throw new NotSupportedException("Only local image file paths are supported.");
            return uri.LocalPath;
        }

        return Path.GetFullPath(source, Environment.CurrentDirectory);
    }
}

internal static class ImageSourceRegistration
{
    private static readonly LocalImageSourceLoader Loader = new();

#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Register() => ImageSourceLoaderRegistry.Register(Loader);
#pragma warning restore CA2255
}
