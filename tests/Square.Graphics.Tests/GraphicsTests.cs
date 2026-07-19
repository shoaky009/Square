using System;
using Square.Graphics;
using Square.Graphics.Codecs;
using System.Buffers.Binary;
using System.IO;
using Xunit;

namespace Square.Graphics.Tests;

public class ColorTests
{
    [Fact]
    public void ParseHex3()
    {
        var c = Color.Parse("#f00");
        Assert.Equal(255, c.R);
        Assert.Equal(0, c.G);
        Assert.Equal(0, c.B);
        Assert.Equal(255, c.A);
    }

    [Fact]
    public void ParseHex6()
    {
        var c = Color.Parse("#0078d4");
        Assert.Equal(0, c.R);
        Assert.Equal(120, c.G);
        Assert.Equal(212, c.B);
    }

    [Fact]
    public void ParseHex8()
    {
        var c = Color.Parse("#FF0078d4");
        Assert.Equal(255, c.A);
        Assert.Equal(0, c.R);
    }

    [Fact]
    public void Equality()
    {
        Assert.Equal(Color.Red, Color.FromRgb(255, 0, 0));
        Assert.NotEqual(Color.Red, Color.Blue);
    }

    [Fact]
    public void ToPackedBgra()
    {
        var c = Color.FromRgba(1, 2, 3, 4);
        var packed = c.ToPackedBgra();
        Assert.Equal(4u, (packed >> 24) & 0xFF);
    }
}

public class RectTests
{
    [Fact]
    public void Contains()
    {
        var r = new Rect(10, 10, 100, 100);
        Assert.True(r.Contains(50, 50));
        Assert.False(r.Contains(5, 5));
    }

    [Fact]
    public void IntersectsWith()
    {
        var a = new Rect(0, 0, 100, 100);
        var b = new Rect(50, 50, 100, 100);
        Assert.True(a.IntersectsWith(b));
    }

    [Fact]
    public void Union()
    {
        var a = new Rect(0, 0, 50, 50);
        var b = new Rect(100, 100, 50, 50);
        var u = Rect.Union(a, b);
        Assert.Equal(0, u.X);
        Assert.Equal(0, u.Y);
        Assert.Equal(150, u.Width);
        Assert.Equal(150, u.Height);
    }

    [Fact]
    public void Intersect()
    {
        var a = new Rect(0, 0, 100, 100);
        var b = new Rect(50, 50, 100, 100);
        var i = Rect.Intersect(a, b);
        Assert.Equal(50, i.X);
        Assert.Equal(50, i.Y);
        Assert.Equal(50, i.Width);
        Assert.Equal(50, i.Height);
    }
}

public class SizeTests
{
    [Fact]
    public void Arithmetic()
    {
        var a = new Size(100, 200);
        var b = new Size(50, 100);
        Assert.Equal(new Size(150, 300), a + b);
        Assert.Equal(new Size(50, 100), a - b);
        Assert.Equal(new Size(200, 400), a * 2);
    }

    [Fact]
    public void IsEmpty()
    {
        Assert.False(new Size(100, 100).IsEmpty);
        Assert.True(Size.Empty.IsEmpty);
    }
}

public class TextLayoutTests
{
    [Fact]
    public void MeasuresHalfWidthAndFullWidthCharacters()
    {
        var font = new Font("Segoe UI", 20);

        Assert.Equal(20, new TextLayout("AB", font).Measure().Width);
        Assert.Equal(40, new TextLayout("ＡＢ", font).Measure().Width);
        Assert.Equal(20, new TextLayout("ｱｲ", font).Measure().Width);
        Assert.Equal(40, new TextLayout("アイ", font).Measure().Width);
        Assert.Equal(30, new TextLayout("A中", font).Measure().Width);
    }
}

public class BitmapCodecTests
{
    [Fact]
    public void SavesBitmapAsPng()
    {
        using var bitmap = new Bitmap(1, 1);
        var pixel = bitmap.GetPixel(0, 0);
        pixel[0] = 3;
        pixel[1] = 2;
        pixel[2] = 1;
        pixel[3] = 4;

        using var stream = new MemoryStream();
        BitmapPngEncoder.Save(bitmap, stream);
        var bytes = stream.ToArray();

        Assert.Equal([137, 80, 78, 71, 13, 10, 26, 10], bytes[..8]);
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(bytes, 12, 4));
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
        Assert.Contains(bytes, b => b == (byte)'I');
    }

    [Fact]
    public void ConvertsTopDownBmpToPng()
    {
        var directory = Path.Combine(Path.GetTempPath(), "square-codec-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var bmpPath = Path.Combine(directory, "source.bmp");
            var pngPath = Path.Combine(directory, "target.png");
            File.WriteAllBytes(bmpPath, CreateTopDownBmp());

            BmpPngConverter.Convert(bmpPath, pngPath);

            var bytes = File.ReadAllBytes(pngPath);
            Assert.Equal([137, 80, 78, 71, 13, 10, 26, 10], bytes[..8]);
            Assert.True(bytes.Length > 50);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] CreateTopDownBmp()
    {
        var bytes = new byte[14 + 40 + 8];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2, 4), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10, 4), 54);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14, 4), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18, 4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22, 4), -1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28, 2), 24);
        bytes[54] = 30;
        bytes[55] = 20;
        bytes[56] = 10;
        bytes[57] = 60;
        bytes[58] = 50;
        bytes[59] = 40;
        return bytes;
    }
}
