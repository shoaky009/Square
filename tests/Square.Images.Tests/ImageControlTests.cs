using Square.Controls;
using Square.Events;
using Square.Graphics;
using Square.Runtime;
using Square.UI;
using System.Collections.Concurrent;
using Xunit;
using ImageControl = Square.Controls.Image;

namespace Square.Images.Tests;

public sealed class ImageControlTests
{
    [Fact]
    public void SourceLoadsLocalImageAndRaisesLoad()
    {
        var path = WriteTempImage(CodecTestData.Png(1, 1, 8, 6, 0, [0, 1, 2, 3, 4]));
        try
        {
            var document = new UIDocument();
            var image = document.CreateElement<ImageControl>();
            document.Body.Children.Add(image);
            var loaded = false;
            image.AddEventListener("load", () => loaded = true);
            image.Source = path;

            ((IComponentLifecycle)document.Body).OnAttached();
            DrainUntil(document, () => loaded);

            Assert.True(loaded);
            Assert.Null(image.Error);
            Assert.Equal(new Size(1, 1), image.Measure(Size.Empty));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InvalidSourceRaisesLoadErrorWithoutThrowingOnDispatcher()
    {
        var document = new UIDocument();
        var image = document.CreateElement<ImageControl>();
        document.Body.Children.Add(image);
        var failed = false;
        image.AddEventListener("loaderror", () => failed = true);
        image.Source = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.png");

        ((IComponentLifecycle)document.Body).OnAttached();
        DrainUntil(document, () => failed);

        Assert.True(failed);
        Assert.IsType<FileNotFoundException>(image.Error);
    }

    [Fact]
    public void AnimatedSourceRequestsExactFrameDelay()
    {
        var palette = new byte[] { 255, 0, 0, 0, 255, 0 };
        var gif = CodecTestData.GifAnimation(1, 1, palette,
        [
            new CodecTestData.GifFrameData(0, 0, 1, 1, [0], Delay: 5),
            new CodecTestData.GifFrameData(0, 0, 1, 1, [1], Delay: 12)
        ]);
        var path = WriteTempImage(gif);
        try
        {
            var document = new UIDocument();
            var image = document.CreateElement<ImageControl>();
            document.Body.Children.Add(image);
            FrameRequestEvent? request = null;
            document.Body.AddEventListener(StandardEvents.RequestFrame, e => request = e as FrameRequestEvent);
            var loaded = false;
            image.AddEventListener("load", () => loaded = true);
            image.Source = path;

            ((IComponentLifecycle)document.Body).OnAttached();
            DrainUntil(document, () => loaded);

            Assert.NotNull(request);
            Assert.Equal(TimeSpan.FromMilliseconds(50), request!.Delay);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ManualImageContentKeepsSourceAsFallbackTextWithoutLoadingIt()
    {
        var document = new UIDocument();
        var image = document.CreateElement<ImageControl>();
        using var bitmap = new Bitmap(1, 1);
        image.Source = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.png");
        image.ImageContent = bitmap;
        document.Body.Children.Add(image);

        ((IComponentLifecycle)document.Body).OnAttached();
        DrainFor(document, TimeSpan.FromMilliseconds(100));

        Assert.Null(image.Error);
        Assert.Equal(new Size(1, 1), image.Measure(Size.Empty));
    }

    [Fact]
    public void ChangingSourceCancelsAndDiscardsThePreviousLoad()
    {
        using var loader = new ControlledImageSourceLoader();
        var document = new UIDocument();
        var image = document.CreateElement<ImageControl>();
        document.Body.Children.Add(image);
        ((IComponentLifecycle)document.Body).OnAttached();
        var loadCount = 0;
        image.AddEventListener("load", () => loadCount++);

        image.Source = loader.Source("first");
        var first = loader.WaitForRequest("first");
        image.Source = loader.Source("second");
        var second = loader.WaitForRequest("second");

        Assert.True(first.CancellationToken.IsCancellationRequested);
        var current = new TestImageFrameSource(2, 1);
        second.Completion.SetResult(current);
        DrainUntil(document, () => loadCount == 1);

        var stale = new TestImageFrameSource(1, 1);
        first.Completion.SetResult(stale);
        DrainUntil(document, () => stale.IsDisposed);

        Assert.True(stale.IsDisposed);
        Assert.False(current.IsDisposed);
        Assert.Equal(1, loadCount);
        Assert.Null(image.Error);
        Assert.Equal(new Size(2, 1), image.Measure(Size.Empty));

        ((IComponentLifecycle)document.Body).OnDetached();
        Assert.True(current.IsDisposed);
    }

    [Fact]
    public void DetachingDuringLoadCancelsAndDisposesLateResult()
    {
        using var loader = new ControlledImageSourceLoader();
        var document = new UIDocument();
        var image = document.CreateElement<ImageControl>();
        document.Body.Children.Add(image);
        var loadCount = 0;
        var errorCount = 0;
        image.AddEventListener("load", () => loadCount++);
        image.AddEventListener("loaderror", () => errorCount++);
        ((IComponentLifecycle)document.Body).OnAttached();

        image.Source = loader.Source("detach");
        var request = loader.WaitForRequest("detach");
        ((IComponentLifecycle)document.Body).OnDetached();

        Assert.True(request.CancellationToken.IsCancellationRequested);
        var late = new TestImageFrameSource(3, 1);
        request.Completion.SetResult(late);
        DrainUntil(document, () => late.IsDisposed);

        Assert.True(late.IsDisposed);
        Assert.Equal(0, loadCount);
        Assert.Equal(0, errorCount);
        Assert.Null(image.Error);
        Assert.Equal(new Size(160, 96), image.Measure(Size.Empty));
    }

    private static string WriteTempImage(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"square-image-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static void DrainUntil(UIDocument document, Func<bool> predicate)
    {
        var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!predicate() && DateTime.UtcNow < timeout)
        {
            document.Context.Dispatcher.Run();
            Thread.Sleep(10);
        }
        document.Context.Dispatcher.Run();
    }

    private static void DrainFor(UIDocument document, TimeSpan duration)
    {
        var timeout = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < timeout)
        {
            document.Context.Dispatcher.Run();
            Thread.Sleep(10);
        }
    }

    private sealed class ControlledImageSourceLoader : IImageSourceLoader, IDisposable
    {
        private readonly string _scheme = $"squaretest{Guid.NewGuid():N}";
        private readonly ConcurrentDictionary<string, LoadRequest> _requests = new();

        public ControlledImageSourceLoader() => ImageSourceLoaderRegistry.Register(this);

        public string Source(string name) => $"{_scheme}:///{name}";

        public bool CanLoad(string source) => source.StartsWith($"{_scheme}:", StringComparison.Ordinal);

        public ValueTask<IImageFrameSource> LoadAsync(string source, CancellationToken cancellationToken = default)
        {
            var name = new Uri(source).AbsolutePath.TrimStart('/');
            var request = new LoadRequest(cancellationToken);
            if (!_requests.TryAdd(name, request)) throw new InvalidOperationException($"Duplicate request '{name}'.");
            return new ValueTask<IImageFrameSource>(request.Completion.Task);
        }

        public LoadRequest WaitForRequest(string name)
        {
            var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < timeout)
            {
                if (_requests.TryGetValue(name, out var request)) return request;
                Thread.Sleep(10);
            }
            throw new TimeoutException($"Image request '{name}' was not started.");
        }

        public void Dispose() => ImageSourceLoaderRegistry.Unregister(this);
    }

    private sealed class LoadRequest(CancellationToken cancellationToken)
    {
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public TaskCompletionSource<IImageFrameSource> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class TestImageFrameSource : IImageFrameSource
    {
        private readonly Bitmap _bitmap;

        public TestImageFrameSource(int width, int height)
        {
            Width = width;
            Height = height;
            _bitmap = new Bitmap(width, height);
        }

        public int Width { get; }
        public int Height { get; }
        public int FrameCount => 1;
        public int PlayCount => 1;
        public bool IsDisposed { get; private set; }

        public Bitmap GetFrame(int index) => index == 0
            ? _bitmap
            : throw new ArgumentOutOfRangeException(nameof(index));

        public TimeSpan GetFrameDuration(int index) => index == 0
            ? TimeSpan.Zero
            : throw new ArgumentOutOfRangeException(nameof(index));

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _bitmap.Dispose();
        }
    }
}
