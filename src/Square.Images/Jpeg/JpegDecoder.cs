using System.Buffers.Binary;
using Square.Graphics;

namespace Square.Images.Jpeg;

internal static class JpegDecoder
{
    private static readonly int[] ZigZag =
    [
        0,  1,  8, 16,  9,  2,  3, 10,
        17, 24, 32, 25, 18, 11,  4,  5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13,  6,  7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63
    ];

    public static Bitmap Decode(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8) throw new InvalidDataException("Invalid JPEG SOI marker.");
        var state = ParseMarkers(data, options);
        options.ValidateDimensions(state.Width, state.Height);
        return DecodePixels(state);
    }

    private static JpegState ParseMarkers(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        var state = new JpegState();
        var offset = 2;
        while (offset < data.Length)
        {
            if (data[offset] != 0xFF) throw new InvalidDataException("JPEG marker prefix is missing.");
            offset++;
            while (offset < data.Length && data[offset] == 0xFF) offset++;
            if (offset >= data.Length) throw new InvalidDataException("JPEG is truncated.");
            var marker = data[offset++];
            if (marker == 0xD9) break;
            if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) continue;
            if (offset + 2 > data.Length) throw new InvalidDataException("JPEG segment is truncated.");
            var length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
            if (length < 2 || offset + length > data.Length) throw new InvalidDataException("JPEG segment length is invalid.");
            var payload = data.Slice(offset + 2, length - 2);
            switch (marker)
            {
                case 0xC0: ParseFrame(payload, state, baselineOnly: true); break;
                case 0xC2: throw new InvalidDataException("Progressive JPEG is not supported.");
                case 0xC4: ParseHuffmanTable(payload, state); break;
                case 0xDB: ParseQuantizationTable(payload, state); break;
                case 0xDD:
                    if (payload.Length != 4) throw new InvalidDataException("Invalid DRI segment.");
                    state.RestartInterval = BinaryPrimitives.ReadUInt16BigEndian(payload);
                    break;
                case 0xDA: ParseScan(data, ref offset, length, payload, state, options); continue;
                case 0xFE: break;
                default:
                    if (marker >= 0xE0 && marker <= 0xEF) break;
                    throw new InvalidDataException($"Unsupported JPEG marker 0xFF{marker:X2}.");
            }
            offset += length;
        }
        if (!state.HasFrame) throw new InvalidDataException("JPEG is missing a frame header.");
        return state;
    }

    private static void ParseFrame(ReadOnlySpan<byte> payload, JpegState state, bool baselineOnly)
    {
        if (payload.Length < 8) throw new InvalidDataException("JPEG frame header is truncated.");
        if (!baselineOnly) throw new InvalidDataException("Only baseline JPEG is supported.");
        var precision = payload[0];
        if (precision != 8) throw new InvalidDataException("Only 8-bit JPEG precision is supported.");
        state.Height = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(1, 2));
        state.Width = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(3, 2));
        var componentCount = payload[5];
        if (componentCount is not (1 or 3)) throw new InvalidDataException("Only grayscale and YCbCr JPEG images are supported.");
        if (payload.Length != 6 + componentCount * 3) throw new InvalidDataException("JPEG frame header length is invalid.");
        state.Components = new JpegComponent[componentCount];
        for (var i = 0; i < componentCount; i++)
        {
            var baseIdx = 6 + i * 3;
            var id = payload[baseIdx];
            var sampling = payload[baseIdx + 1];
            var tableId = payload[baseIdx + 2];
            state.Components[i] = new JpegComponent
            {
                Id = id,
                HorizontalSampling = (sampling >> 4) & 0x0F,
                VerticalSampling = sampling & 0x0F,
                QuantizationTableId = tableId & 0x0F
            };
            if (state.Components[i].HorizontalSampling is 0 or > 4 || state.Components[i].VerticalSampling is 0 or > 4)
                throw new InvalidDataException("JPEG sampling factor is invalid.");
        }
        state.MaxHorizontal = state.Components.Max(c => c.HorizontalSampling);
        state.MaxVertical = state.Components.Max(c => c.VerticalSampling);
        state.HasFrame = true;
    }

    private static void ParseHuffmanTable(ReadOnlySpan<byte> payload, JpegState state)
    {
        var index = 0;
        while (index < payload.Length)
        {
            if (index + 17 > payload.Length) throw new InvalidDataException("JPEG Huffman table is truncated.");
            var classAndId = payload[index++];
            var tableClass = (classAndId >> 4) & 0x0F;
            var tableId = classAndId & 0x0F;
            var counts = payload.Slice(index, 16);
            index += 16;
            var total = 0;
            for (var i = 0; i < 16; i++) total += counts[i];
            if (index + total > payload.Length) throw new InvalidDataException("JPEG Huffman table values are truncated.");
            var values = payload.Slice(index, total);
            index += total;
            var table = JpegHuffmanTable.Build(counts, values);
            if (tableClass == 0) state.DcTables[tableId] = table;
            else state.AcTables[tableId] = table;
        }
    }

    private static void ParseQuantizationTable(ReadOnlySpan<byte> payload, JpegState state)
    {
        var index = 0;
        while (index < payload.Length)
        {
            if (index + 1 > payload.Length) throw new InvalidDataException("JPEG quantization table is truncated.");
            var classAndId = payload[index++];
            var precision = (classAndId >> 4) & 0x0F;
            var tableId = classAndId & 0x0F;
            if (precision != 0) throw new InvalidDataException("Only 8-bit JPEG quantization tables are supported.");
            if (index + 64 > payload.Length) throw new InvalidDataException("JPEG quantization table is truncated.");
            var values = payload.Slice(index, 64);
            index += 64;
            var table = new int[64];
            for (var i = 0; i < 64; i++) table[i] = values[i];
            state.QuantizationTables[tableId] = table;
        }
    }

    private static void ParseScan(ReadOnlySpan<byte> data, ref int offset, int length, ReadOnlySpan<byte> payload, JpegState state, ImageDecoderOptions options)
    {
        if (!state.HasFrame) throw new InvalidDataException("JPEG scan without a frame header.");
        if (payload.Length < 6) throw new InvalidDataException("JPEG scan header is truncated.");
        var componentCount = payload[0];
        if (componentCount != state.Components.Length) throw new InvalidDataException("JPEG scan component count does not match frame.");
        state.ScanComponents = new JpegScanComponent[componentCount];
        for (var i = 0; i < componentCount; i++)
        {
            var baseIdx = 1 + i * 2;
            var componentId = payload[baseIdx];
            var tableIds = payload[baseIdx + 1];
            var component = Array.Find(state.Components, c => c.Id == componentId) ?? throw new InvalidDataException("JPEG scan references an unknown component.");
            state.ScanComponents[i] = new JpegScanComponent
            {
                Component = component,
                DcTableId = (tableIds >> 4) & 0x0F,
                AcTableId = tableIds & 0x0F,
                PredictedDc = 0
            };
        }
        var scanHeaderEnd = offset + length;
        var entropyStart = scanHeaderEnd;
        var entropyEnd = FindEntropyEnd(data, entropyStart);
        DecodeScanData(data, entropyStart, entropyEnd, state, options);
        offset = entropyEnd;
    }

    private static int FindEntropyEnd(ReadOnlySpan<byte> data, int start)
    {
        var i = start;
        while (i < data.Length)
        {
            if (data[i] != 0xFF) { i++; continue; }
            if (i + 1 >= data.Length) throw new InvalidDataException("JPEG entropy data is truncated.");
            var next = data[i + 1];
            if (next == 0) { i += 2; continue; }
            return i;
        }
        throw new InvalidDataException("JPEG entropy data is missing an EOI marker.");
    }

    private static void DecodeScanData(ReadOnlySpan<byte> data, int start, int end, JpegState state, ImageDecoderOptions options)
    {
        var entropy = data.Slice(start, end - start);
        var reader = new JpegBitReader(entropy, 0);
        var mcuWidth = state.MaxHorizontal * 8;
        var mcuHeight = state.MaxVertical * 8;
        var mcusX = (state.Width + mcuWidth - 1) / mcuWidth;
        var mcusY = (state.Height + mcuHeight - 1) / mcuHeight;
        var decodedBytes = checked((long)mcusX * mcusY * mcuWidth * mcuHeight * 4);
        if (decodedBytes > options.MaxDecodedBytes) throw new InvalidDataException("JPEG decoded data exceeds the configured byte limit.");
        foreach (var component in state.Components)
        {
            var blocksX = mcusX * component.HorizontalSampling;
            var blocksY = mcusY * component.VerticalSampling;
            component.Coefficients = new int[blocksX * blocksY][];
            component.Samples = new int[blocksX * blocksY][];
            component.BlocksX = blocksX;
            component.BlocksY = blocksY;
        }
        var restartInterval = state.RestartInterval;
        var mcuIndex = 0;
        for (var my = 0; my < mcusY; my++)
        {
            for (var mx = 0; mx < mcusX; mx++)
            {
                if (restartInterval > 0 && mcuIndex > 0 && mcuIndex % restartInterval == 0)
                {
                    var expected = 0xFFD0 + ((mcuIndex / restartInterval - 1) & 7);
                    reader.CheckRestart(expected);
                    foreach (var scan in state.ScanComponents!) scan.PredictedDc = 0;
                }
                foreach (var scan in state.ScanComponents!)
                {
                    var component = scan.Component;
                    for (var vy = 0; vy < component.VerticalSampling; vy++)
                    {
                        for (var vx = 0; vx < component.HorizontalSampling; vx++)
                        {
                            var blockX = mx * component.HorizontalSampling + vx;
                            var blockY = my * component.VerticalSampling + vy;
                            var block = DecodeBlock(reader, state, scan);
                            component.Coefficients[blockY * component.BlocksX + blockX] = block;
                        }
                    }
                }
                mcuIndex++;
            }
        }
    }

    private static int[] DecodeBlock(JpegBitReader reader, JpegState state, JpegScanComponent scan)
    {
        var quant = state.QuantizationTables[scan.Component.QuantizationTableId] ?? throw new InvalidDataException("JPEG references a missing quantization table.");
        var dcTable = state.DcTables[scan.DcTableId] ?? throw new InvalidDataException("JPEG references a missing DC Huffman table.");
        var acTable = state.AcTables[scan.AcTableId] ?? throw new InvalidDataException("JPEG references a missing AC Huffman table.");
        var coefficients = new int[64];
        var dcCategory = reader.DecodeHuffman(dcTable);
        var dcDiff = reader.Extend(reader.Receive(dcCategory), dcCategory);
        scan.PredictedDc += dcDiff;
        coefficients[0] = scan.PredictedDc * quant[0];
        var k = 1;
        while (k < 64)
        {
            var symbol = reader.DecodeHuffman(acTable);
            if (symbol == 0) break;
            if (symbol == 0xF0) { k += 16; continue; }
            var run = (symbol >> 4) & 0x0F;
            var category = symbol & 0x0F;
            if (category == 0) throw new InvalidDataException("JPEG AC coefficient category is invalid.");
            k += run;
            if (k >= 64) throw new InvalidDataException("JPEG AC coefficient run is out of range.");
            var value = reader.Extend(reader.Receive(category), category);
            coefficients[ZigZag[k]] = value * quant[k];
            k++;
        }
        return coefficients;
    }

    private static Bitmap DecodePixels(JpegState state)
    {
        foreach (var component in state.Components)
            for (var i = 0; i < component.Coefficients.Length; i++)
            {
                var samples = new int[64];
                InverseDct.Transform(component.Coefficients[i], samples);
                component.Samples[i] = samples;
            }
        var bitmap = new Bitmap(state.Width, state.Height);
        try
        {
            if (state.Components.Length == 1)
            {
                var component = state.Components[0];
                for (var y = 0; y < state.Height; y++)
                {
                    for (var x = 0; x < state.Width; x++)
                    {
                        var sample = SampleComponent(component, x, y);
                        var dest = bitmap.GetPixel(x, y);
                        dest[0] = (byte)sample; dest[1] = (byte)sample; dest[2] = (byte)sample; dest[3] = 255;
                    }
                }
            }
            else
            {
                for (var y = 0; y < state.Height; y++)
                {
                    for (var x = 0; x < state.Width; x++)
                    {
                        var yValue = SampleComponent(state.Components[0], x, y);
                        var cb = SampleComponent(state.Components[1], x, y) - 128;
                        var cr = SampleComponent(state.Components[2], x, y) - 128;
                        var r = (int)MathF.Round(yValue + 1.402f * cr);
                        var g = (int)MathF.Round(yValue - 0.344136f * cb - 0.714136f * cr);
                        var b = (int)MathF.Round(yValue + 1.772f * cb);
                        var dest = bitmap.GetPixel(x, y);
                        dest[0] = ClampToByte(b);
                        dest[1] = ClampToByte(g);
                        dest[2] = ClampToByte(r);
                        dest[3] = 255;
                    }
                }
            }
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static int SampleComponent(JpegComponent component, int x, int y)
    {
        var scaleX = component.HorizontalSampling;
        var scaleY = component.VerticalSampling;
        var blockX = x / (8 * scaleX);
        var blockY = y / (8 * scaleY);
        var localX = (x % (8 * scaleX)) / scaleX;
        var localY = (y % (8 * scaleY)) / scaleY;
        if (blockX >= component.BlocksX) blockX = component.BlocksX - 1;
        if (blockY >= component.BlocksY) blockY = component.BlocksY - 1;
        var block = component.Samples[blockY * component.BlocksX + blockX];
        if (block == null) return 128;
        return block[localY * 8 + localX];
    }

    private static byte ClampToByte(int value) => value < 0 ? (byte)0 : value > 255 ? (byte)255 : (byte)value;
}