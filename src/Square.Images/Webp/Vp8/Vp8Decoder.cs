using Square.Graphics;

namespace Square.Images.Webp.Vp8;

internal static partial class Vp8Decoder
{
    internal static Bitmap Decode(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (data.Length > options.MaxEncodedBytes)
            throw new InvalidDataException("VP8 data exceeds the configured encoded byte limit.");
        return new Decoder(data.ToArray(), options).Decode();
    }

    private sealed partial class Decoder
    {
        private readonly byte[] data;
        private readonly ImageDecoderOptions options;
        private BoolDecoder first = null!;
        private BoolDecoder[] tokens = [];
        private int width, height, mbWidth, mbHeight, offset, segment;
        private readonly SegmentHeader segmentHeader = new();
        private readonly FilterHeader filterHeader = new();
        private readonly Quant[] quant = [new(), new(), new(), new()];
        private readonly byte[] tokenProb = (byte[])Vp8Tables.DefaultTokenProb.Clone();
        private bool useSkipProb, usePredY16;
        private byte skipProb, predY16, predC8;
        private MbState leftMb = new();
        private MbState[] upMb = [];
        private readonly byte[,] predY4 = new byte[4, 4];
        private uint nzDcMask, nzAcMask;
        private readonly short[] coeff = new short[400];
        private readonly byte[] work = new byte[26 * 32];
        private byte[] yPlane = [], cbPlane = [], crPlane = [];
        private int yStride, cStride;
        private FilterParam[,] filterParams = new FilterParam[4, 2];
        private FilterParam[] perMbFilterParams = [];

        internal Decoder(byte[] data, ImageDecoderOptions options)
        {
            this.data = data;
            this.options = options;
        }

        internal Bitmap Decode()
        {
            ParseFrameHeader();
            options.ValidateDimensions(width, height);
            mbWidth = checked((width + 15) >> 4);
            mbHeight = checked((height + 15) >> 4);
            yStride = checked(mbWidth * 16);
            cStride = checked(mbWidth * 8);
            var paddedHeight = checked(mbHeight * 16);
            var yBytes = checked(yStride * paddedHeight);
            var cBytes = checked(cStride * (paddedHeight >> 1));
            var workingBytes = checked((long)yBytes + 2L * cBytes + (long)mbWidth * mbHeight * 8);
            if (yBytes > Array.MaxLength || cBytes > Array.MaxLength)
                throw Bad("padded image exceeds the configured byte limit");
            if (workingBytes > options.MaxDecodedBytes)
                throw Bad("padded image exceeds the configured decoded byte limit");
            yPlane = new byte[yBytes]; cbPlane = new byte[cBytes]; crPlane = new byte[cBytes];
            upMb = new MbState[mbWidth];
            for (var i = 0; i < upMb.Length; i++) upMb[i] = new MbState();
            perMbFilterParams = new FilterParam[checked(mbWidth * mbHeight)];
            ParseHeaders();
            for (var mby = 0; mby < mbHeight; mby++)
            {
                leftMb = new MbState();
                for (var mbx = 0; mbx < mbWidth; mbx++)
                {
                    var skip = Reconstruct(mbx, mby);
                    var parameter = filterParams[segment, usePredY16 ? 0 : 1];
                    parameter.Inner |= !skip;
                    perMbFilterParams[mby * mbWidth + mbx] = parameter;
                }
            }
            EnsureComplete();
            if (filterHeader.Level != 0)
            {
                if (filterHeader.Simple) SimpleFilter(); else NormalFilter();
            }
            return ToBitmap();
        }

        private void ParseFrameHeader()
        {
            if (data.Length < 10) throw Bad("truncated frame header");
            var tag = data[0] | data[1] << 8 | data[2] << 16;
            if ((tag & 1) != 0) throw Bad("interframes are not supported");
            var version = tag >> 1 & 7;
            if (version > 3) throw Bad("invalid version");
            var firstLength = tag >> 5;
            if (data[3] != 0x9d || data[4] != 0x01 || data[5] != 0x2a) throw Bad("invalid keyframe sync code");
            width = data[6] | (data[7] & 0x3f) << 8;
            height = data[8] | (data[9] & 0x3f) << 8;
            if (width == 0 || height == 0) throw Bad("invalid dimensions");
            offset = 10;
            if (firstLength > data.Length - offset) throw Bad("truncated first partition");
            first = new BoolDecoder(data, offset, firstLength);
            offset += firstLength;
        }

        private void ParseHeaders()
        {
            _ = first.ReadBit(128); // color space
            _ = first.ReadBit(128); // pixel clamp
            ParseSegmentation();
            ParseFilterHeader();
            var partitionCount = 1 << (int)first.ReadUInt(128, 2);
            ParseTokenPartitions(partitionCount);
            ParseQuantization();
            _ = first.ReadBit(128); // refresh last frame buffer
            for (var i = 0; i < tokenProb.Length; i++)
                if (first.ReadBit(Vp8Tables.TokenProbUpdateProb[i])) tokenProb[i] = (byte)first.ReadUInt(128, 8);
            useSkipProb = first.ReadBit(128);
            if (useSkipProb) skipProb = (byte)first.ReadUInt(128, 8);
            if (first.Truncated) throw Bad("truncated control partition");
        }

        private void ParseSegmentation()
        {
            segmentHeader.Enabled = first.ReadBit(128);
            if (!segmentHeader.Enabled) return;
            segmentHeader.UpdateMap = first.ReadBit(128);
            if (first.ReadBit(128))
            {
                segmentHeader.Relative = !first.ReadBit(128);
                for (var i = 0; i < 4; i++) segmentHeader.Quantizer[i] = (sbyte)first.ReadOptionalInt(128, 7);
                for (var i = 0; i < 4; i++) segmentHeader.FilterStrength[i] = (sbyte)first.ReadOptionalInt(128, 6);
            }
            if (!segmentHeader.UpdateMap) return;
            for (var i = 0; i < 3; i++) segmentHeader.Probability[i] = first.ReadBit(128) ? (byte)first.ReadUInt(128, 8) : (byte)255;
        }

        private void ParseFilterHeader()
        {
            filterHeader.Simple = first.ReadBit(128);
            filterHeader.Level = (sbyte)first.ReadUInt(128, 6);
            filterHeader.Sharpness = (byte)first.ReadUInt(128, 3);
            filterHeader.UseDelta = first.ReadBit(128);
            if (filterHeader.UseDelta && first.ReadBit(128))
            {
                for (var i = 0; i < 4; i++) filterHeader.RefDelta[i] = (sbyte)first.ReadOptionalInt(128, 6);
                for (var i = 0; i < 4; i++) filterHeader.ModeDelta[i] = (sbyte)first.ReadOptionalInt(128, 6);
            }
            if (filterHeader.Level != 0) ComputeFilterParams();
        }

        private void ParseTokenPartitions(int count)
        {
            var tableBytes = checked(3 * (count - 1));
            if (tableBytes > data.Length - offset) throw Bad("truncated token partition table");
            var lengths = new int[count];
            var remaining = data.Length - offset - tableBytes;
            for (var i = 0; i < count - 1; i++)
            {
                var p = offset + i * 3;
                lengths[i] = data[p] | data[p + 1] << 8 | data[p + 2] << 16;
                if (lengths[i] > remaining) throw Bad("invalid token partition length");
                remaining -= lengths[i];
            }
            lengths[^1] = remaining;
            offset += tableBytes;
            tokens = new BoolDecoder[count];
            for (var i = 0; i < count; i++)
            {
                tokens[i] = new BoolDecoder(data, offset, lengths[i]);
                offset += lengths[i];
            }
        }

        private void ParseQuantization()
        {
            var baseQ = (int)first.ReadUInt(128, 7);
            var y1Dc = first.ReadOptionalInt(128, 4);
            var y2Dc = first.ReadOptionalInt(128, 4);
            var y2Ac = first.ReadOptionalInt(128, 4);
            var uvDc = first.ReadOptionalInt(128, 4);
            var uvAc = first.ReadOptionalInt(128, 4);
            for (var i = 0; i < 4; i++)
            {
                var q = baseQ;
                if (segmentHeader.Enabled) q = segmentHeader.Relative ? q + segmentHeader.Quantizer[i] : segmentHeader.Quantizer[i];
                quant[i].Y1Dc = Vp8Tables.DequantDc[Math.Clamp(q + y1Dc, 0, 127)];
                quant[i].Y1Ac = Vp8Tables.DequantAc[Math.Clamp(q, 0, 127)];
                quant[i].Y2Dc = 2 * Vp8Tables.DequantDc[Math.Clamp(q + y2Dc, 0, 127)];
                quant[i].Y2Ac = Math.Max(8, Vp8Tables.DequantAc[Math.Clamp(q + y2Ac, 0, 127)] * 155 / 100);
                quant[i].UvDc = Vp8Tables.DequantDc[Math.Clamp(q + uvDc, 0, 117)];
                quant[i].UvAc = Vp8Tables.DequantAc[Math.Clamp(q + uvAc, 0, 127)];
            }
        }

        private Bitmap ToBitmap()
        {
            var bitmap = new Bitmap(width, height);
            try
            {
                for (var py = 0; py < height; py++)
                for (var px = 0; px < width; px++)
                {
                    var yy = yPlane[py * yStride + px];
                    var u = cbPlane[(py >> 1) * cStride + (px >> 1)];
                    var v = crPlane[(py >> 1) * cStride + (px >> 1)];
                    var destination = bitmap.GetPixel(px, py);
                    destination[2] = ConvertYuvToRed(yy, v);
                    destination[1] = ConvertYuvToGreen(yy, u, v);
                    destination[0] = ConvertYuvToBlue(yy, u);
                    destination[3] = 255;
                }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }

        private void EnsureComplete()
        {
            if (first.Truncated || tokens.Any(static p => p.Truncated)) throw Bad("truncated VP8 partition");
        }

        private static byte ConvertYuvToRed(int y, int v) => ClipYuv(
            MultiplyHigh(y, 19077) + MultiplyHigh(v, 26149) - 14234);
        private static byte ConvertYuvToGreen(int y, int u, int v) => ClipYuv(
            MultiplyHigh(y, 19077) - MultiplyHigh(u, 6419) - MultiplyHigh(v, 13320) + 8708);
        private static byte ConvertYuvToBlue(int y, int u) => ClipYuv(
            MultiplyHigh(y, 19077) + MultiplyHigh(u, 33050) - 17685);
        private static int MultiplyHigh(int value, int coefficient) => value * coefficient >> 8;
        private static byte ClipYuv(int value) => (byte)((value & ~16383) == 0
            ? value >> 6
            : value < 0 ? 0 : 255);
        private static byte Clip8(int value) => (byte)Math.Clamp(value, 0, 255);
        private static InvalidDataException Bad(string reason) => new($"Invalid VP8 keyframe: {reason}.");
    }

    private sealed class BoolDecoder
    {
        private readonly byte[] data;
        private readonly int end;
        private int position, bitCount;
        private uint rangeMinusOne = 254, bits;
        internal bool Truncated { get; private set; }

        internal BoolDecoder(byte[] data, int offset, int length)
        {
            this.data = data; position = offset; end = checked(offset + length);
        }

        internal bool ReadBit(int probability)
        {
            if (bitCount < 8)
            {
                if (position >= end) { Truncated = true; return false; }
                bits |= (uint)data[position++] << (8 - bitCount);
                bitCount += 8;
            }
            var split = rangeMinusOne * (uint)probability >> 8;
            split++;
            var bit = bits >= split << 8;
            if (bit) { rangeMinusOne -= split; bits -= split << 8; }
            else rangeMinusOne = split - 1;
            if (rangeMinusOne < 127)
            {
                var shift = Vp8Tables.BoolShift[rangeMinusOne];
                rangeMinusOne = Vp8Tables.BoolRangeMinusOne[rangeMinusOne];
                bits <<= shift; bitCount -= shift;
            }
            return bit;
        }

        internal uint ReadUInt(int probability, int count)
        {
            uint value = 0;
            while (count-- > 0) if (ReadBit(probability)) value |= 1u << count;
            return value;
        }

        internal int ReadOptionalInt(int probability, int count)
        {
            if (!ReadBit(probability)) return 0;
            var value = (int)ReadUInt(probability, count);
            return ReadBit(probability) ? -value : value;
        }
    }

    private sealed class SegmentHeader
    {
        internal bool Enabled, UpdateMap, Relative;
        internal readonly sbyte[] Quantizer = new sbyte[4], FilterStrength = new sbyte[4];
        internal readonly byte[] Probability = [255, 255, 255];
    }

    private sealed class FilterHeader
    {
        internal bool Simple, UseDelta;
        internal sbyte Level;
        internal byte Sharpness;
        internal readonly sbyte[] RefDelta = new sbyte[4], ModeDelta = new sbyte[4];
    }

    private sealed class Quant
    {
        internal int Y1Dc, Y1Ac, Y2Dc, Y2Ac, UvDc, UvAc;
    }

    private sealed class MbState
    {
        internal readonly byte[] Pred = new byte[4];
        internal byte NzMask, NzY16;
    }

    private struct FilterParam
    {
        internal byte Level, InnerLevel, HighEdgeLevel;
        internal bool Inner;
    }
}
