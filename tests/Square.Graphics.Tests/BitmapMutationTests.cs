using Square.Graphics;
using Xunit;

namespace Square.Graphics.Tests;

public sealed class BitmapMutationTests
{
    [Fact]
    public void SetPixelsUpdatesContentVersion()
    {
        using var bitmap = new Bitmap(1, 1);

        bitmap.SetPixels([1, 2, 3, 4]);
        var firstVersion = bitmap.ContentVersion;
        bitmap.MarkDirty();

        Assert.Equal([1, 2, 3, 4], bitmap.Pixels);
        Assert.Equal(1, firstVersion);
        Assert.Equal(2, bitmap.ContentVersion);
    }

    [Fact]
    public void CopyPixelsRequiresMatchingDimensions()
    {
        using var source = new Bitmap(1, 1);
        using var destination = new Bitmap(2, 1);

        Assert.Throws<ArgumentException>(() => destination.CopyPixelsFrom(source));
    }
}
