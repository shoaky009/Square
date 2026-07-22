using System.Buffers.Binary;
using System.IO.Compression;

namespace Square.Images.Tests;

internal static class CodecTestData
{
    internal readonly record struct GifFrameData(int Left, int Top, int Width, int Height, byte[] Indices,
        ushort Delay = 0, byte Disposal = 0, bool Transparent = false, byte TransparentIndex = 0);
    internal readonly record struct IconVariantData(int Width, int Height, int BitCount, byte[] XorPixels,
        byte[] AndMask, byte[]? Palette = null, ushort HotspotX = 0, ushort HotspotY = 0);
    internal readonly record struct TiffPageData(int Width, int Height, ushort Photometric, ushort Samples,
        ushort Bits, byte[] Pixels, ushort Orientation = 1, uint RowsPerStrip = uint.MaxValue,
        ushort[]? ColorMap = null, ushort ExtraSample = 0);
    internal readonly record struct ApngFrameData(int Width, int Height, int X, int Y, byte[] Raw,
        ushort DelayNumerator, ushort DelayDenominator = 1000, byte Dispose = 0, byte Blend = 0);
    internal readonly record struct WebpFrameData(int X, int Y, int Width, int Height, int DurationMilliseconds,
        byte[] Vp8L, bool DisposeToBackground = false, bool NoBlend = false);

    public static byte[] Gif(int screenWidth, int screenHeight, byte[] globalPalette, int left, int top,
        int width, int height, byte[] indices, bool transparent = false, byte transparentIndex = 0,
        bool interlaced = false, byte[]? localPalette = null)
    {
        using var stream = new MemoryStream();
        stream.Write("GIF89a"u8);
        WriteUInt16(stream, screenWidth); WriteUInt16(stream, screenHeight);
        var globalSize = TableSize(globalPalette.Length / 3);
        stream.WriteByte((byte)(0x80 | (globalSize.Exponent & 7)));
        stream.WriteByte(0); stream.WriteByte(0); stream.Write(PadPalette(globalPalette, globalSize.Entries));
        if (transparent)
        {
            stream.Write([0x21, 0xF9, 0x04, 0x01, 0, 0, transparentIndex, 0]);
        }
        stream.WriteByte(0x2C);
        WriteUInt16(stream, left); WriteUInt16(stream, top); WriteUInt16(stream, width); WriteUInt16(stream, height);
        var localSize = localPalette == null ? default : TableSize(localPalette.Length / 3);
        stream.WriteByte((byte)((localPalette != null ? 0x80 | localSize.Exponent : 0) | (interlaced ? 0x40 : 0)));
        if (localPalette != null) stream.Write(PadPalette(localPalette, localSize.Entries));
        var paletteEntries = localPalette?.Length / 3 ?? globalPalette.Length / 3;
        var minimum = Math.Max(2, (int)Math.Ceiling(Math.Log2(Math.Max(2, paletteEntries))));
        stream.WriteByte((byte)minimum);
        var compressed = GifLzw(indices, minimum);
        for (var offset = 0; offset < compressed.Length;)
        {
            var count = Math.Min(255, compressed.Length - offset);
            stream.WriteByte((byte)count); stream.Write(compressed, offset, count); offset += count;
        }
        stream.WriteByte(0); stream.WriteByte(0x3B);
        return stream.ToArray();
    }

    public static byte[] GifAnimation(int screenWidth, int screenHeight, byte[] globalPalette,
        GifFrameData[] frames, ushort repeatCount = 0)
    {
        using var stream = new MemoryStream();
        stream.Write("GIF89a"u8);
        WriteUInt16(stream, screenWidth); WriteUInt16(stream, screenHeight);
        var globalSize = TableSize(globalPalette.Length / 3);
        stream.WriteByte((byte)(0x80 | (globalSize.Exponent & 7)));
        stream.WriteByte(0); stream.WriteByte(0); stream.Write(PadPalette(globalPalette, globalSize.Entries));
        stream.Write([0x21, 0xFF, 0x0B]); stream.Write("NETSCAPE2.0"u8);
        stream.Write([0x03, 0x01]); WriteUInt16(stream, repeatCount); stream.WriteByte(0);
        foreach (var frame in frames)
        {
            var packed = (byte)((frame.Disposal & 7) << 2 | (frame.Transparent ? 1 : 0));
            stream.Write([0x21, 0xF9, 0x04, packed]);
            WriteUInt16(stream, frame.Delay); stream.WriteByte(frame.TransparentIndex); stream.WriteByte(0);
            stream.WriteByte(0x2C);
            WriteUInt16(stream, frame.Left); WriteUInt16(stream, frame.Top);
            WriteUInt16(stream, frame.Width); WriteUInt16(stream, frame.Height); stream.WriteByte(0);
            var minimum = Math.Max(2, (int)Math.Ceiling(Math.Log2(Math.Max(2, globalPalette.Length / 3))));
            stream.WriteByte((byte)minimum);
            var compressed = GifLzw(frame.Indices, minimum);
            for (var offset = 0; offset < compressed.Length;)
            {
                var count = Math.Min(255, compressed.Length - offset);
                stream.WriteByte((byte)count); stream.Write(compressed, offset, count); offset += count;
            }
            stream.WriteByte(0);
        }
        stream.WriteByte(0x3B);
        return stream.ToArray();
    }

    public static byte[] Png(int width, int height, byte depth, byte colorType, byte interlace,
        byte[] raw, byte[]? palette = null, byte[]? transparency = null)
    {
        using var stream = new MemoryStream();
        stream.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header[0..4], width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..8], height);
        header[8] = depth; header[9] = colorType; header[10] = 0; header[11] = 0; header[12] = interlace;
        Chunk(stream, "IHDR"u8, header);
        if (palette != null) Chunk(stream, "PLTE"u8, palette);
        if (transparency != null) Chunk(stream, "tRNS"u8, transparency);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true)) zlib.Write(raw);
        Chunk(stream, "IDAT"u8, compressed.ToArray());
        Chunk(stream, "IEND"u8, []);
        return stream.ToArray();
    }

    public static byte[] Apng(int width, int height, ApngFrameData[] frames, uint playCount = 0)
    {
        using var stream = new MemoryStream();
        stream.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header[0..4], width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..8], height);
        header[8] = 8; header[9] = 6;
        Chunk(stream, "IHDR"u8, header);
        Span<byte> animation = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(animation[0..4], (uint)frames.Length);
        BinaryPrimitives.WriteUInt32BigEndian(animation[4..8], playCount);
        Chunk(stream, "acTL"u8, animation);
        uint sequence = 0;
        var control = new byte[26];
        for (var i = 0; i < frames.Length; i++)
        {
            var frame = frames[i];
            BinaryPrimitives.WriteUInt32BigEndian(control.AsSpan(0, 4), sequence++);
            BinaryPrimitives.WriteInt32BigEndian(control.AsSpan(4, 4), frame.Width);
            BinaryPrimitives.WriteInt32BigEndian(control.AsSpan(8, 4), frame.Height);
            BinaryPrimitives.WriteInt32BigEndian(control.AsSpan(12, 4), frame.X);
            BinaryPrimitives.WriteInt32BigEndian(control.AsSpan(16, 4), frame.Y);
            BinaryPrimitives.WriteUInt16BigEndian(control.AsSpan(20, 2), frame.DelayNumerator);
            BinaryPrimitives.WriteUInt16BigEndian(control.AsSpan(22, 2), frame.DelayDenominator);
            control[24] = frame.Dispose; control[25] = frame.Blend;
            Chunk(stream, "fcTL"u8, control);
            var compressed = Zlib(frame.Raw);
            if (i == 0) Chunk(stream, "IDAT"u8, compressed);
            else
            {
                var frameData = new byte[compressed.Length + 4];
                BinaryPrimitives.WriteUInt32BigEndian(frameData.AsSpan(0, 4), sequence++);
                compressed.CopyTo(frameData, 4);
                Chunk(stream, "fdAT"u8, frameData);
            }
        }
        Chunk(stream, "IEND"u8, []);
        return stream.ToArray();
    }

    public static byte[] AnimatedWebp(int width, int height, WebpFrameData[] frames, ushort loopCount = 0,
        uint background = 0)
    {
        using var body = new MemoryStream();
        Span<byte> vp8x = stackalloc byte[10];
        vp8x[0] = 2;
        WriteUInt24(vp8x[4..7], width - 1); WriteUInt24(vp8x[7..10], height - 1);
        WebpChunk(body, "VP8X"u8, vp8x);
        Span<byte> anim = stackalloc byte[6];
        BinaryPrimitives.WriteUInt32LittleEndian(anim[0..4], background);
        BinaryPrimitives.WriteUInt16LittleEndian(anim[4..6], loopCount);
        WebpChunk(body, "ANIM"u8, anim);
        var header = new byte[16];
        foreach (var frame in frames)
        {
            using var payload = new MemoryStream();
            Array.Clear(header);
            WriteUInt24(header.AsSpan(0, 3), frame.X / 2); WriteUInt24(header.AsSpan(3, 3), frame.Y / 2);
            WriteUInt24(header.AsSpan(6, 3), frame.Width - 1); WriteUInt24(header.AsSpan(9, 3), frame.Height - 1);
            WriteUInt24(header.AsSpan(12, 3), frame.DurationMilliseconds);
            header[15] = (byte)((frame.DisposeToBackground ? 1 : 0) | (frame.NoBlend ? 2 : 0));
            payload.Write(header); WebpChunk(payload, "VP8L"u8, frame.Vp8L);
            WebpChunk(body, "ANMF"u8, payload.ToArray());
        }
        var bytes = new byte[12 + body.Length];
        "RIFF"u8.CopyTo(bytes); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), (uint)(bytes.Length - 8));
        "WEBP"u8.CopyTo(bytes.AsSpan(8)); body.ToArray().CopyTo(bytes, 12); return bytes;
    }

    public static byte[] ExtractVp8L(byte[] webp)
    {
        if (!webp.AsSpan(12, 4).SequenceEqual("VP8L"u8)) throw new ArgumentException("Expected simple VP8L WebP.");
        var length = BinaryPrimitives.ReadInt32LittleEndian(webp.AsSpan(16, 4));
        return webp.AsSpan(20, length).ToArray();
    }

    public static byte[] Bmp(int width, int signedHeight, int bits, byte[] rows)
    {
        var bytesPerPixel = bits / 8;
        var stride = ((width * bytesPerPixel) + 3) / 4 * 4;
        var height = Math.Abs(signedHeight);
        var bytes = new byte[54 + stride * height];
        bytes[0] = (byte)'B'; bytes[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2, 4), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10, 4), 54);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14, 4), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18, 4), width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22, 4), signedHeight);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28, 2), (ushort)bits);
        rows.CopyTo(bytes, 54);
        return bytes;
    }

    public static byte[] Jpeg(int width, int height, int components, int[][] blockDcValues)
    {
        using var stream = new MemoryStream();
        stream.Write([(byte)0xFF, (byte)0xD8]);
        WriteQuantizationTable(stream, 0);
        if (components == 3) WriteQuantizationTable(stream, 1);
        WriteHuffmanTable(stream, dc: true, tableId: 0);
        WriteHuffmanTable(stream, dc: false, tableId: 0);
        if (components == 3)
        {
            WriteHuffmanTable(stream, dc: true, tableId: 1);
            WriteHuffmanTable(stream, dc: false, tableId: 1);
        }
        WriteFrameHeader(stream, width, height, components);
        WriteScan(stream, components, blockDcValues);
        stream.Write([(byte)0xFF, (byte)0xD9]);
        return stream.ToArray();
    }

    public static byte[] JpegWithExifOrientation(int width, int height, int components, int[][] blockDcValues,
        int orientation, bool littleEndian = true)
        => JpegWithExif(width, height, components, blockDcValues, ExifTiff(orientation, littleEndian));

    public static byte[] JpegWithExif(int width, int height, int components, int[][] blockDcValues, byte[] tiff)
    {
        var jpeg = Jpeg(width, height, components, blockDcValues);
        using var stream = new MemoryStream();
        stream.Write(jpeg, 0, 2);
        stream.Write([0xFF, 0xE1]);
        Span<byte> length = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, checked((ushort)(2 + 6 + tiff.Length)));
        stream.Write(length); stream.Write("Exif\0\0"u8); stream.Write(tiff);
        stream.Write(jpeg, 2, jpeg.Length - 2);
        return stream.ToArray();
    }

    public static byte[] ExifTiff(int orientation, bool littleEndian)
    {
        var tiff = new byte[26];
        tiff[0] = tiff[1] = littleEndian ? (byte)'I' : (byte)'M';
        WriteExifUInt16(tiff.AsSpan(2, 2), 42, littleEndian);
        WriteExifUInt32(tiff.AsSpan(4, 4), 8, littleEndian);
        WriteExifUInt16(tiff.AsSpan(8, 2), 1, littleEndian);
        WriteExifUInt16(tiff.AsSpan(10, 2), 0x0112, littleEndian);
        WriteExifUInt16(tiff.AsSpan(12, 2), 3, littleEndian);
        WriteExifUInt32(tiff.AsSpan(14, 4), 1, littleEndian);
        WriteExifUInt16(tiff.AsSpan(18, 2), orientation, littleEndian);
        WriteExifUInt32(tiff.AsSpan(22, 4), 0, littleEndian);
        return tiff;
    }

    private static void WriteExifUInt16(Span<byte> destination, int value, bool littleEndian)
    {
        if (littleEndian) BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)value);
        else BinaryPrimitives.WriteUInt16BigEndian(destination, (ushort)value);
    }

    private static void WriteExifUInt32(Span<byte> destination, uint value, bool littleEndian)
    {
        if (littleEndian) BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
        else BinaryPrimitives.WriteUInt32BigEndian(destination, value);
    }

    public static byte[] Ico(int width, int height, int bitCount, byte[] xorPixels, byte[]? andMask = null, byte[]? palette = null)
        => IconContainer(1, [new IconVariantData(width, height, bitCount, xorPixels,
            andMask ?? new byte[((width + 31) / 32 * 4) * height], palette)]);

    public static byte[] IconContainer(int type, IconVariantData[] variants)
    {
        using var stream = new MemoryStream();
        WriteUInt16(stream, 0); WriteUInt16(stream, type); WriteUInt16(stream, variants.Length);
        var images = new byte[variants.Length][];
        var imageOffset = 6 + variants.Length * 16;
        for (var i = 0; i < variants.Length; i++)
        {
            var variant = variants[i];
            images[i] = IconBmpImage(variant);
            var paletteEntries = variant.BitCount <= 8 ? 1 << variant.BitCount : 0;
            var entry = new byte[16];
            entry[0] = (byte)(variant.Width >= 256 ? 0 : variant.Width);
            entry[1] = (byte)(variant.Height >= 256 ? 0 : variant.Height);
            entry[2] = variant.BitCount <= 8 && paletteEntries < 256 ? (byte)paletteEntries : (byte)0;
            if (type == 2)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(4, 2), variant.HotspotX);
                BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(6, 2), variant.HotspotY);
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(4, 2), 1);
                BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(6, 2), (ushort)variant.BitCount);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(8, 4), (uint)images[i].Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(12, 4), (uint)imageOffset);
            stream.Write(entry);
            imageOffset += images[i].Length;
        }
        foreach (var image in images) stream.Write(image);
        return stream.ToArray();
    }

    public static byte[] Tiff(TiffPageData[] pages, bool littleEndian = true)
    {
        var layouts = new TiffPageLayout[pages.Length];
        var offset = 8;
        for (var i = 0; i < pages.Length; i++)
        {
            var page = pages[i];
            var rowsPerStrip = page.RowsPerStrip == uint.MaxValue ? (uint)page.Height : page.RowsPerStrip;
            var strips = checked((page.Height + (int)rowsPerStrip - 1) / (int)rowsPerStrip);
            var entryCount = 11 + (page.ColorMap != null ? 1 : 0) + (page.ExtraSample != 0 ? 1 : 0);
            var ifdSize = 2 + entryCount * 12 + 4;
            var bitsBytes = page.Samples > 1 ? page.Samples * 2 : 0;
            var stripArrayBytes = strips > 1 ? strips * 4 * 2 : 0;
            var colorMapBytes = (page.ColorMap?.Length ?? 0) * 2;
            layouts[i] = new TiffPageLayout(offset, entryCount, ifdSize, bitsBytes, stripArrayBytes,
                colorMapBytes, rowsPerStrip, strips);
            offset = checked(offset + ifdSize + bitsBytes + stripArrayBytes + colorMapBytes + page.Pixels.Length);
        }

        var result = new byte[offset];
        result[0] = result[1] = littleEndian ? (byte)'I' : (byte)'M';
        WriteTiffUInt16(result.AsSpan(2, 2), 42, littleEndian);
        WriteTiffUInt32(result.AsSpan(4, 4), pages.Length == 0 ? 0u : 8u, littleEndian);
        for (var i = 0; i < pages.Length; i++) WriteTiffPage(result, pages[i], layouts[i],
            i + 1 < layouts.Length ? (uint)layouts[i + 1].IfdOffset : 0, littleEndian);
        return result;
    }

    private static void WriteTiffPage(byte[] result, TiffPageData page, TiffPageLayout layout,
        uint nextIfd, bool littleEndian)
    {
        var rowsPerStrip = layout.RowsPerStrip;
        var rowBytes = checked((page.Width * page.Samples * page.Bits + 7) / 8);
        var auxOffset = layout.IfdOffset + layout.IfdSize;
        var bitsOffset = auxOffset;
        var stripOffsetsArray = bitsOffset + layout.BitsBytes;
        var stripCountsArray = stripOffsetsArray + (layout.Strips > 1 ? layout.Strips * 4 : 0);
        var colorMapOffset = stripOffsetsArray + layout.StripArrayBytes;
        var pixelOffset = colorMapOffset + layout.ColorMapBytes;
        var stripOffsets = new uint[layout.Strips];
        var stripCounts = new uint[layout.Strips];
        var consumed = 0;
        for (var strip = 0; strip < layout.Strips; strip++)
        {
            var rows = Math.Min((int)rowsPerStrip, page.Height - strip * (int)rowsPerStrip);
            stripOffsets[strip] = (uint)(pixelOffset + consumed);
            stripCounts[strip] = (uint)(rows * rowBytes);
            consumed += rows * rowBytes;
        }
        if (consumed != page.Pixels.Length) throw new InvalidOperationException("TIFF test pixels do not match dimensions.");

        WriteTiffUInt16(result.AsSpan(layout.IfdOffset, 2), (ushort)layout.EntryCount, littleEndian);
        var entryOffset = layout.IfdOffset + 2;
        WriteTiffEntry(result, ref entryOffset, 256, 4, 1, (uint)page.Width, littleEndian);
        WriteTiffEntry(result, ref entryOffset, 257, 4, 1, (uint)page.Height, littleEndian);
        if (page.Samples == 1) WriteTiffEntry(result, ref entryOffset, 258, 3, 1, page.Bits, littleEndian);
        else WriteTiffEntry(result, ref entryOffset, 258, 3, page.Samples, (uint)bitsOffset, littleEndian);
        WriteTiffEntry(result, ref entryOffset, 259, 3, 1, 1, littleEndian);
        WriteTiffEntry(result, ref entryOffset, 262, 3, 1, page.Photometric, littleEndian);
        WriteTiffEntry(result, ref entryOffset, 273, 4, (uint)layout.Strips,
            layout.Strips == 1 ? stripOffsets[0] : (uint)stripOffsetsArray, littleEndian);
        WriteTiffEntry(result, ref entryOffset, 274, 3, 1, page.Orientation, littleEndian);
        WriteTiffEntry(result, ref entryOffset, 277, 3, 1, page.Samples, littleEndian);
        WriteTiffEntry(result, ref entryOffset, 278, 4, 1, rowsPerStrip, littleEndian);
        WriteTiffEntry(result, ref entryOffset, 279, 4, (uint)layout.Strips,
            layout.Strips == 1 ? stripCounts[0] : (uint)stripCountsArray, littleEndian);
        WriteTiffEntry(result, ref entryOffset, 284, 3, 1, 1, littleEndian);
        if (page.ColorMap != null) WriteTiffEntry(result, ref entryOffset, 320, 3,
            (uint)page.ColorMap.Length, (uint)colorMapOffset, littleEndian);
        if (page.ExtraSample != 0) WriteTiffEntry(result, ref entryOffset, 338, 3, 1, page.ExtraSample, littleEndian);
        WriteTiffUInt32(result.AsSpan(layout.IfdOffset + 2 + layout.EntryCount * 12, 4), nextIfd, littleEndian);

        if (page.Samples > 1)
            for (var sample = 0; sample < page.Samples; sample++)
                WriteTiffUInt16(result.AsSpan(bitsOffset + sample * 2, 2), page.Bits, littleEndian);
        if (layout.Strips > 1)
            for (var strip = 0; strip < layout.Strips; strip++)
            {
                WriteTiffUInt32(result.AsSpan(stripOffsetsArray + strip * 4, 4), stripOffsets[strip], littleEndian);
                WriteTiffUInt32(result.AsSpan(stripCountsArray + strip * 4, 4), stripCounts[strip], littleEndian);
            }
        if (page.ColorMap != null)
            for (var value = 0; value < page.ColorMap.Length; value++)
                WriteTiffUInt16(result.AsSpan(colorMapOffset + value * 2, 2), page.ColorMap[value], littleEndian);
        page.Pixels.CopyTo(result, pixelOffset);
    }

    private static void WriteTiffEntry(byte[] result, ref int offset, ushort tag, ushort type, uint count,
        uint value, bool littleEndian)
    {
        WriteTiffUInt16(result.AsSpan(offset, 2), tag, littleEndian);
        WriteTiffUInt16(result.AsSpan(offset + 2, 2), type, littleEndian);
        WriteTiffUInt32(result.AsSpan(offset + 4, 4), count, littleEndian);
        if (type == 3 && count == 1)
        {
            WriteTiffUInt16(result.AsSpan(offset + 8, 2), (ushort)value, littleEndian);
            result[offset + 10] = result[offset + 11] = 0;
        }
        else WriteTiffUInt32(result.AsSpan(offset + 8, 4), value, littleEndian);
        offset += 12;
    }

    private static void WriteTiffUInt16(Span<byte> destination, ushort value, bool littleEndian)
    {
        if (littleEndian) BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
        else BinaryPrimitives.WriteUInt16BigEndian(destination, value);
    }

    private static void WriteTiffUInt32(Span<byte> destination, uint value, bool littleEndian)
    {
        if (littleEndian) BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
        else BinaryPrimitives.WriteUInt32BigEndian(destination, value);
    }

    private readonly record struct TiffPageLayout(int IfdOffset, int EntryCount, int IfdSize, int BitsBytes,
        int StripArrayBytes, int ColorMapBytes, uint RowsPerStrip, int Strips);

    private static byte[] IconBmpImage(IconVariantData variant)
    {
        using var stream = new MemoryStream();
        var dib = new byte[40];
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(0, 4), 40);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(4, 4), variant.Width);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(8, 4), variant.Height * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(12, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(14, 2), (ushort)variant.BitCount);
        BinaryPrimitives.WriteUInt32LittleEndian(dib.AsSpan(32, 4), (uint)(variant.XorPixels.Length + variant.AndMask.Length));
        stream.Write(dib);
        var paletteEntries = variant.BitCount <= 8 ? 1 << variant.BitCount : 0;
        if (variant.Palette != null) stream.Write(variant.Palette);
        else if (paletteEntries > 0) stream.Write(new byte[paletteEntries * 4]);
        stream.Write(variant.XorPixels); stream.Write(variant.AndMask);
        return stream.ToArray();
    }

    private static void WriteSegment(Stream stream, int marker, ReadOnlySpan<byte> data)
    {
        stream.WriteByte(0xFF); stream.WriteByte((byte)marker);
        Span<byte> length = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)(data.Length + 2));
        stream.Write(length); stream.Write(data);
    }

    private static void WriteQuantizationTable(Stream stream, int tableId)
    {
        Span<byte> data = stackalloc byte[65];
        data[0] = (byte)tableId;
        for (var i = 1; i <= 64; i++) data[i] = 1;
        WriteSegment(stream, 0xDB, data);
    }

    private static void WriteHuffmanTable(Stream stream, bool dc, int tableId)
    {
        byte[] counts;
        byte[] values;
        if (dc)
        {
            counts = [0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0];
            values = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
        }
        else
        {
            counts = [0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
            values = [0, 0xF0];
        }
        var data = new byte[1 + 16 + values.Length];
        data[0] = (byte)((dc ? 0 : 1) << 4 | tableId);
        counts.CopyTo(data, 1);
        values.CopyTo(data, 17);
        WriteSegment(stream, 0xC4, data);
    }

    private static void WriteFrameHeader(Stream stream, int width, int height, int components)
    {
        var data = new byte[6 + 3 * components];
        data[0] = 8;
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(1, 2), (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(3, 2), (ushort)width);
        data[5] = (byte)components;
        for (var i = 0; i < components; i++)
        {
            data[6 + i * 3] = (byte)(i + 1);
            data[7 + i * 3] = 0x11;
            data[8 + i * 3] = (byte)(i == 0 ? 0 : 1);
        }
        WriteSegment(stream, 0xC0, data);
    }

    private static void WriteScan(Stream stream, int components, int[][] blockDcValues)
    {
        var header = new byte[4 + components * 2];
        header[0] = (byte)components;
        for (var i = 0; i < components; i++)
        {
            header[1 + i * 2] = (byte)(i + 1);
            header[2 + i * 2] = (byte)(i == 0 ? 0x00 : 0x11);
        }
        header[1 + components * 2] = 0;
        header[2 + components * 2] = 63;
        header[3 + components * 2] = 0;
        WriteSegment(stream, 0xDA, header);
        var blocksPerComponent = blockDcValues[0].Length;
        var bits = new List<bool>();
        var predicted = new int[components];
        for (var blockIdx = 0; blockIdx < blocksPerComponent; blockIdx++)
        {
            for (var c = 0; c < components; c++)
            {
                var dc = blockDcValues[c][blockIdx];
                var diff = dc - predicted[c];
                predicted[c] = dc;
                var (category, payload) = EncodeDc(diff);
                EmitHuffmanCode(bits, category, dc: true);
                EmitValue(bits, payload, category);
                EmitHuffmanCode(bits, 0x00, dc: false);
            }
        }
        while (bits.Count % 8 != 0) bits.Add(true);
        for (var i = 0; i < bits.Count; i += 8)
        {
            var value = 0;
            for (var bit = 0; bit < 8; bit++) if (bits[i + bit]) value |= 1 << (7 - bit);
            stream.WriteByte((byte)value);
            if (value == 0xFF) stream.WriteByte(0);
        }
    }

    private static (int Category, int Payload) EncodeDc(int value)
    {
        if (value == 0) return (0, 0);
        var abs = Math.Abs(value);
        var category = 0;
        while ((1 << category) <= abs) category++;
        var payload = value < 0 ? value + (1 << category) - 1 : value;
        return (category, payload);
    }

    private static void EmitHuffmanCode(List<bool> bits, int symbol, bool dc)
    {
        if (dc)
        {
            (int Bits, int Length) code = symbol switch
            {
                0 => (0, 2),
                1 => (0b010, 3),
                2 => (0b011, 3),
                3 => (0b100, 3),
                4 => (0b101, 3),
                5 => (0b110, 3),
                6 => (0b1110, 4),
                7 => (0b11110, 5),
                8 => (0b111110, 6),
                9 => (0b1111110, 7),
                10 => (0b11111110, 8),
                11 => (0b111111110, 9),
                _ => throw new NotSupportedException("DC category out of range.")
            };
            for (var i = code.Length - 1; i >= 0; i--) bits.Add(((code.Bits >> i) & 1) != 0);
        }
        else
        {
            if (symbol == 0) bits.AddRange([false, false]);
            else if (symbol == 0xF0) bits.AddRange([false, true, false]);
            else throw new NotSupportedException("Test only encodes EOB and ZRL AC symbols.");
        }
    }

    private static void EmitValue(List<bool> bits, int value, int length)
    {
        for (var i = length - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
    }

    private static void Chunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length); stream.Write(type); stream.Write(data);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc(type, data));
        stream.Write(crc);
    }

    private static byte[] Zlib(ReadOnlySpan<byte> raw)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true)) zlib.Write(raw);
        return compressed.ToArray();
    }

    private static void WebpChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        stream.Write(type);
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(size, data.Length); stream.Write(size); stream.Write(data);
        if ((data.Length & 1) != 0) stream.WriteByte(0);
    }

    private static void WriteUInt24(Span<byte> destination, int value)
    {
        destination[0] = (byte)value; destination[1] = (byte)(value >> 8); destination[2] = (byte)(value >> 16);
    }

    private static uint Crc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in type) crc = Step(crc, value);
        foreach (var value in data) crc = Step(crc, value);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint Step(uint crc, byte value)
    {
        crc ^= value;
        for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        return crc;
    }

    private static byte[] GifLzw(ReadOnlySpan<byte> indices, int minimum)
    {
        var clear = 1 << minimum; var end = clear + 1; var next = clear + 2; var width = minimum + 1;
        var codes = new List<(int Code, int Width)> { (clear, width) };
        for (var i = 0; i < indices.Length; i++)
        {
            codes.Add((indices[i], width));
            if (i == 0) continue;
            if (next < 4096)
            {
                next++;
                if (next == 1 << width && width < 12) width++;
            }
        }
        codes.Add((end, width));
        var result = new List<byte>(); uint bits = 0; var bitCount = 0;
        foreach (var item in codes)
        {
            bits |= (uint)item.Code << bitCount; bitCount += item.Width;
            while (bitCount >= 8) { result.Add((byte)bits); bits >>= 8; bitCount -= 8; }
        }
        if (bitCount > 0) result.Add((byte)bits);
        return result.ToArray();
    }

    private static (int Entries, int Exponent) TableSize(int count)
    {
        var entries = 2; var exponent = 0;
        while (entries < count) { entries <<= 1; exponent++; }
        return (entries, exponent);
    }

    private static byte[] PadPalette(byte[] palette, int entries)
    {
        var result = new byte[entries * 3]; palette.CopyTo(result, 0); return result;
    }

    private static void WriteUInt16(Stream stream, int value)
    {
        stream.WriteByte((byte)value); stream.WriteByte((byte)(value >> 8));
    }
}
