using System.Buffers.Binary;
using Square.Graphics;

namespace Square.Images.Gif;

internal static class GifDecoder
{
    public static ImageDocument Decode(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        var reader = new GifReader(data);
        var signature = reader.Read(6);
        if (!signature.SequenceEqual("GIF87a"u8) && !signature.SequenceEqual("GIF89a"u8))
            throw new InvalidDataException("Invalid GIF signature.");
        var screenWidth = reader.ReadUInt16();
        var screenHeight = reader.ReadUInt16();
        options.ValidateDimensions(screenWidth, screenHeight);
        var packed = reader.ReadByte();
        var backgroundIndex = reader.ReadByte();
        _ = reader.ReadByte();
        var globalPalette = (packed & 0x80) != 0 ? reader.ReadPalette(1 << ((packed & 7) + 1)) : null;
        GifControl? control = null;
        var loopCount = 1;
        var canvas = new Bitmap(screenWidth, screenHeight);
        var items = new List<ImageItem>();
        GifDisposal? pendingDisposal = null;
        Bitmap? restoreCanvas = null;
        long totalDecodedBytes = 0;

        try
        {
            while (!reader.End)
            {
                switch (reader.ReadByte())
                {
                    case 0x21:
                        ReadExtension(ref reader, ref control, ref loopCount, options);
                        break;
                    case 0x2C:
                        if (items.Count >= options.MaxItemCount) throw new InvalidDataException("GIF frame count exceeds the configured limit.");
                        ApplyDisposal(canvas, pendingDisposal, restoreCanvas, globalPalette, backgroundIndex);
                        restoreCanvas?.Dispose();
                        restoreCanvas = null;
                        if (items.Count == 0 && control is not { Transparent: true } && globalPalette != null && backgroundIndex < globalPalette.Length / 3)
                            Fill(canvas, globalPalette, backgroundIndex);
                        var frame = ReadImage(ref reader, options, canvas, globalPalette, control);
                        if (frame.Disposal == 3) restoreCanvas = Clone(canvas);
                        DrawFrame(canvas, frame);
                        var snapshot = Clone(canvas);
                        totalDecodedBytes = checked(totalDecodedBytes + snapshot.Pixels.Length);
                        if (totalDecodedBytes > options.MaxTotalDecodedBytes)
                        {
                            snapshot.Dispose();
                            throw new InvalidDataException("GIF decoded frames exceed the configured total byte limit.");
                        }
                        items.Add(new ImageItem(items.Count, snapshot, 32, TimeSpan.FromMilliseconds(frame.Delay * 10L)));
                        pendingDisposal = new GifDisposal(frame.Left, frame.Top, frame.Width, frame.Height, frame.Disposal);
                        control = null;
                        break;
                    case 0x3B:
                        if (items.Count == 0) throw new InvalidDataException("GIF contains no raster image.");
                        canvas.Dispose();
                        restoreCanvas?.Dispose();
                        var totalDuration = TimeSpan.FromTicks(items.Sum(static item => item.Duration.Ticks));
                        var animation = new ImageAnimationInfo(loopCount == 0, loopCount == 0 ? 0 : loopCount, totalDuration);
                        return new ImageDocument(ImageFormat.Gif,
                            items.Count > 1 ? ImageDocumentKind.Animation : ImageDocumentKind.Still,
                            items.ToArray(), 0, items.Count > 1 ? animation : null);
                    default:
                        throw new InvalidDataException("GIF contains an invalid block marker.");
                }
            }
            throw new InvalidDataException("GIF is missing a trailer.");
        }
        catch
        {
            canvas.Dispose();
            restoreCanvas?.Dispose();
            foreach (var item in items) item.Dispose();
            throw;
        }
    }

    private static void ReadExtension(ref GifReader reader, ref GifControl? control, ref int loopCount, ImageDecoderOptions options)
    {
        var label = reader.ReadByte();
        switch (label)
        {
            case 0xF9:
                if (reader.ReadByte() != 4) throw new InvalidDataException("Invalid GIF graphic control extension.");
                var packed = reader.ReadByte();
                var delay = reader.ReadUInt16();
                var transparentIndex = reader.ReadByte();
                if (reader.ReadByte() != 0) throw new InvalidDataException("Invalid GIF graphic control terminator.");
                var disposal = (packed >> 2) & 7;
                if (disposal > 3) disposal = 0;
                control = new GifControl((packed & 1) != 0, transparentIndex, delay, disposal);
                break;
            case 0xFF:
                if (reader.ReadByte() != 11) throw new InvalidDataException("Invalid GIF application extension.");
                var identifier = reader.Read(11);
                if (identifier.SequenceEqual("NETSCAPE2.0"u8) || identifier.SequenceEqual("ANIMEXTS1.0"u8))
                {
                    if (reader.ReadByte() != 3 || reader.ReadByte() != 1) throw new InvalidDataException("Invalid GIF loop extension.");
                    var repeats = reader.ReadUInt16();
                    if (reader.ReadByte() != 0) throw new InvalidDataException("Invalid GIF loop extension terminator.");
                    loopCount = repeats == 0 ? 0 : checked(repeats + 1);
                }
                else reader.SkipSubBlocks(options.MaxChunkBytes);
                break;
            case 0x01:
                if (reader.ReadByte() != 12) throw new InvalidDataException("Invalid GIF plain text extension.");
                reader.Skip(12); reader.SkipSubBlocks(options.MaxChunkBytes); control = null;
                break;
            default:
                reader.SkipSubBlocks(options.MaxChunkBytes);
                break;
        }
    }

    private static GifFrame ReadImage(ref GifReader reader, ImageDecoderOptions options, Bitmap canvas,
        byte[]? globalPalette, GifControl? control)
    {
        var left = reader.ReadUInt16(); var top = reader.ReadUInt16();
        var width = reader.ReadUInt16(); var height = reader.ReadUInt16();
        options.ValidateDimensions(width, height);
        var packed = reader.ReadByte();
        var interlaced = (packed & 0x40) != 0;
        var palette = (packed & 0x80) != 0 ? reader.ReadPalette(1 << ((packed & 7) + 1)) : globalPalette;
        if (palette == null) throw new InvalidDataException("GIF image has no color table.");
        if (control is { Transparent: true } transparent && transparent.Index >= palette.Length / 3)
            throw new InvalidDataException("GIF transparent color index is outside the color table.");

        var indices = new byte[checked(width * height)];
        var minimumCodeSize = reader.ReadByte();
        if (minimumCodeSize is < 2 or > 8) throw new InvalidDataException("Invalid GIF LZW minimum code size.");
        var blocks = new SubBlockReader(ref reader, options.MaxChunkBytes);
        DecodeLzw(ref blocks, minimumCodeSize, indices.Length, index => indices[index.Position] = index.Value);
        blocks.Drain();
        reader.Offset = blocks.Offset;
        if (interlaced)
        {
            var reordered = new byte[indices.Length];
            for (var rasterY = 0; rasterY < height; rasterY++)
                indices.AsSpan(rasterY * width, width).CopyTo(reordered.AsSpan(InterlaceRow(rasterY, height) * width, width));
            indices = reordered;
        }
        return new GifFrame(left, top, width, height, palette, indices, control?.Transparent == true ? control.Value.Index : null,
            control?.Delay ?? 0, control?.Disposal ?? 0, canvas.Width, canvas.Height);
    }

    private static void DrawFrame(Bitmap canvas, GifFrame frame)
    {
        for (var imageY = 0; imageY < frame.Height; imageY++)
        {
            var y = frame.Top + imageY;
            if (y >= frame.CanvasHeight) continue;
            for (var imageX = 0; imageX < frame.Width; imageX++)
            {
                var paletteIndex = frame.Indices[imageY * frame.Width + imageX];
                if (frame.TransparentIndex == paletteIndex) continue;
                if (paletteIndex >= frame.Palette.Length / 3) throw new InvalidDataException("GIF color index is outside the color table.");
                var x = frame.Left + imageX;
                if (x >= frame.CanvasWidth) continue;
                var destination = canvas.GetPixel(x, y);
                destination[0] = frame.Palette[paletteIndex * 3 + 2];
                destination[1] = frame.Palette[paletteIndex * 3 + 1];
                destination[2] = frame.Palette[paletteIndex * 3];
                destination[3] = 255;
            }
        }
    }

    private static void ApplyDisposal(Bitmap canvas, GifDisposal? disposal, Bitmap? restoreCanvas,
        byte[]? globalPalette, int backgroundIndex)
    {
        if (disposal == null || disposal.Value.Method is 0 or 1) return;
        if (disposal.Value.Method == 3 && restoreCanvas != null)
        {
            restoreCanvas.Pixels.CopyTo(canvas.Pixels, 0);
            return;
        }
        var color = globalPalette != null && backgroundIndex < globalPalette.Length / 3
            ? (B: globalPalette[backgroundIndex * 3 + 2], G: globalPalette[backgroundIndex * 3 + 1], R: globalPalette[backgroundIndex * 3], A: (byte)255)
            : (B: (byte)0, G: (byte)0, R: (byte)0, A: (byte)0);
        var right = Math.Min(canvas.Width, disposal.Value.Left + disposal.Value.Width);
        var bottom = Math.Min(canvas.Height, disposal.Value.Top + disposal.Value.Height);
        for (var y = disposal.Value.Top; y < bottom; y++)
            for (var x = disposal.Value.Left; x < right; x++)
            {
                var pixel = canvas.GetPixel(x, y);
                pixel[0] = color.B; pixel[1] = color.G; pixel[2] = color.R; pixel[3] = color.A;
            }
    }

    private static Bitmap Clone(Bitmap source)
    {
        var clone = new Bitmap(source.Width, source.Height);
        source.Pixels.CopyTo(clone.Pixels, 0);
        return clone;
    }

    private static void DecodeLzw(ref SubBlockReader source, int minimumSize, int expected,
        Action<GifIndex> output)
    {
        Span<ushort> prefix = stackalloc ushort[4096];
        Span<byte> suffix = stackalloc byte[4096];
        Span<byte> stack = stackalloc byte[4096];
        var clear = 1 << minimumSize; var end = clear + 1; var firstFree = clear + 2;
        var codeSize = minimumSize + 1; var nextCode = firstFree; var previous = -1; var produced = 0;
        var bitReader = new GifBitReader(source);
        while (true)
        {
            var code = bitReader.ReadCode(codeSize);
            if (code < 0)
            {
                source = bitReader.Source;
                if (produced == expected) return;
                throw new InvalidDataException("GIF LZW stream ended before all pixels were decoded.");
            }
            if (code == clear)
            {
                codeSize = minimumSize + 1; nextCode = firstFree; previous = -1;
                continue;
            }
            if (code == end)
            {
                if (produced != expected) throw new InvalidDataException("GIF LZW stream ended before all pixels were decoded.");
                source = bitReader.Source;
                return;
            }
            if (previous < 0)
            {
                if (code >= clear) throw new InvalidDataException("Invalid first GIF LZW code.");
                Emit((byte)code);
                previous = code;
                continue;
            }

            var special = code == nextCode;
            if (code > nextCode || special && nextCode >= 4096) throw new InvalidDataException("Invalid GIF LZW dictionary code.");
            var count = Expand(special ? previous : code, clear, nextCode, prefix, suffix, stack, out var first);
            for (var i = count - 1; i >= 0; i--) Emit(stack[i]);
            if (special) Emit(first);
            if (nextCode < 4096)
            {
                prefix[nextCode] = (ushort)previous; suffix[nextCode] = first; nextCode++;
                if (nextCode == 1 << codeSize && codeSize < 12) codeSize++;
            }
            previous = code;
        }

        void Emit(byte value)
        {
            if (produced >= expected) throw new InvalidDataException("GIF LZW stream produced too many pixels.");
            output(new GifIndex(produced++, value));
        }
    }

    private static int Expand(int code, int clear, int nextCode, ReadOnlySpan<ushort> prefix,
        ReadOnlySpan<byte> suffix, Span<byte> stack, out byte first)
    {
        var count = 0; var current = code;
        while (current >= clear)
        {
            if (current >= nextCode || count >= stack.Length) throw new InvalidDataException("Invalid GIF LZW prefix chain.");
            stack[count++] = suffix[current]; current = prefix[current];
        }
        first = (byte)current; stack[count++] = first;
        return count;
    }

    private static int InterlaceRow(int rasterRow, int height)
    {
        var count = (height + 7) / 8;
        if (rasterRow < count) return rasterRow * 8;
        rasterRow -= count; count = height > 4 ? (height - 4 + 7) / 8 : 0;
        if (rasterRow < count) return 4 + rasterRow * 8;
        rasterRow -= count; count = height > 2 ? (height - 2 + 3) / 4 : 0;
        if (rasterRow < count) return 2 + rasterRow * 4;
        rasterRow -= count; return 1 + rasterRow * 2;
    }

    private static void Fill(Bitmap bitmap, byte[] palette, int index)
    {
        var blue = palette[index * 3 + 2]; var green = palette[index * 3 + 1]; var red = palette[index * 3];
        for (var offset = 0; offset < bitmap.Pixels.Length; offset += 4)
        {
            bitmap.Pixels[offset] = blue; bitmap.Pixels[offset + 1] = green;
            bitmap.Pixels[offset + 2] = red; bitmap.Pixels[offset + 3] = 255;
        }
    }

    private readonly record struct GifControl(bool Transparent, byte Index, ushort Delay, int Disposal);
    private readonly record struct GifDisposal(int Left, int Top, int Width, int Height, int Method);
    private readonly record struct GifFrame(int Left, int Top, int Width, int Height, byte[] Palette, byte[] Indices,
        byte? TransparentIndex, ushort Delay, int Disposal, int CanvasWidth, int CanvasHeight);
    private readonly record struct GifIndex(int Position, byte Value);

    private ref struct GifReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _offset;

        public GifReader(ReadOnlySpan<byte> data) => _data = data;

        public bool End => _offset >= _data.Length;
        public int Offset { get => _offset; set => _offset = value; }
        public byte ReadByte() => Read(1)[0];
        public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Read(2));
        public ReadOnlySpan<byte> Read(int count)
        {
            if (count < 0 || _offset > _data.Length - count) throw new InvalidDataException("GIF data is truncated.");
            var result = _data.Slice(_offset, count); _offset += count; return result;
        }
        public void Skip(int count) => _ = Read(count);
        public byte[] ReadPalette(int entries) => Read(checked(entries * 3)).ToArray();
        public void SkipSubBlocks(int limit)
        {
            long total = 0;
            while (true)
            {
                var length = ReadByte(); if (length == 0) return;
                total += length; if (total > limit) throw new InvalidDataException("GIF sub-block chain exceeds the configured limit.");
                Skip(length);
            }
        }
    }

    private ref struct SubBlockReader
    {
        private GifReader _reader;
        private int _remaining;
        private long _total;
        private readonly int _limit;
        private bool _ended;

        public SubBlockReader(ref GifReader reader, int limit) { _reader = reader; _limit = limit; }
        public int Offset => _reader.Offset;
        public int ReadByte()
        {
            if (_ended) return -1;
            if (_remaining == 0)
            {
                _remaining = _reader.ReadByte();
                if (_remaining == 0) { _ended = true; return -1; }
                _total += _remaining;
                if (_total > _limit) throw new InvalidDataException("GIF image data exceeds the configured chunk limit.");
            }
            _remaining--; return _reader.ReadByte();
        }
        public void Drain() { while (ReadByte() >= 0) { } }
    }

    private ref struct GifBitReader
    {
        public SubBlockReader Source;
        private uint _bits;
        private int _count;

        public GifBitReader(SubBlockReader source) => Source = source;
        public int ReadCode(int width)
        {
            while (_count < width)
            {
                var value = Source.ReadByte(); if (value < 0) return -1;
                _bits |= (uint)value << _count; _count += 8;
            }
            var code = (int)(_bits & ((1u << width) - 1)); _bits >>= width; _count -= width; return code;
        }
    }
}
