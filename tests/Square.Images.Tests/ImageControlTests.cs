using Square.Controls;
using Square.Events;
using Square.Graphics;
using Square.Runtime;
using Square.UI;
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
}
