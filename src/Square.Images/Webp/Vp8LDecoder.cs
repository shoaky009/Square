using Square.Graphics;

namespace Square.Images.Webp;

internal static class Vp8LDecoder
{
    private static readonly byte[] LengthOrder = [17,18,0,1,2,3,4,5,16,6,7,8,9,10,11,12,13,14,15];
    private static readonly byte[] Plane = [0x18,0x07,0x17,0x19,0x28,0x06,0x27,0x29,0x16,0x1a,0x26,0x2a,0x38,0x05,0x37,0x39,0x15,0x1b,0x36,0x3a,0x25,0x2b,0x48,0x04,0x47,0x49,0x14,0x1c,0x35,0x3b,0x46,0x4a,0x24,0x2c,0x58,0x45,0x4b,0x34,0x3c,0x03,0x57,0x59,0x13,0x1d,0x56,0x5a,0x23,0x2d,0x44,0x4c,0x55,0x5b,0x33,0x3d,0x68,0x02,0x67,0x69,0x12,0x1e,0x66,0x6a,0x22,0x2e,0x54,0x5c,0x43,0x4d,0x65,0x6b,0x32,0x3e,0x78,0x01,0x77,0x79,0x53,0x5d,0x11,0x1f,0x64,0x6c,0x42,0x4e,0x76,0x7a,0x21,0x2f,0x75,0x7b,0x31,0x3f,0x63,0x6d,0x52,0x5e,0x00,0x74,0x7c,0x41,0x4f,0x10,0x20,0x62,0x6e,0x30,0x73,0x7d,0x51,0x5f,0x40,0x72,0x7e,0x61,0x6f,0x50,0x71,0x7f,0x60,0x70];

    public static Bitmap Decode(ReadOnlySpan<byte> data, ImageDecoderOptions options)
    {
        var reader = new Bits(data);
        if (reader.Read(8) != 0x2f) throw Bad("signature");
        var width = reader.Read(14) + 1; var height = reader.Read(14) + 1;
        _ = reader.Read(1); if (reader.Read(3) != 0) throw Bad("version");
        options.ValidateDimensions(width, height);
        var transforms = new List<Tx>(); var used = 0; var codedWidth = width;
        while (reader.Read(1) != 0)
        {
            var type = reader.Read(2); if ((used & 1 << type) != 0) throw Bad("duplicate transform"); used |= 1 << type;
            if (type is 0 or 1)
            {
                var bits = reader.Read(3) + 2;
                transforms.Add(new Tx(type, codedWidth, height, bits,
                    DecodeImage(ref reader, Div(codedWidth, 1 << bits), Div(height, 1 << bits), false, options), null, 0));
            }
            else if (type == 2) transforms.Add(new Tx(type, codedWidth, height, 0, null, null, 0));
            else
            {
                var count = reader.Read(8) + 1; var palette = DecodeImage(ref reader, count, 1, false, options);
                for (var i = 1; i < palette.Length; i++) palette[i] = Add(palette[i], palette[i - 1]);
                var widthBits = count <= 2 ? 3 : count <= 4 ? 2 : count <= 16 ? 1 : 0;
                transforms.Add(new Tx(type, codedWidth, height, 0, null, palette, widthBits)); codedWidth = Div(codedWidth, 1 << widthBits);
            }
        }
        var pixels = DecodeImage(ref reader, codedWidth, height, true, options);
        for (var i = transforms.Count - 1; i >= 0; i--) pixels = Inverse(transforms[i], pixels);
        if (pixels.Length != checked(width * height)) throw Bad("transform dimensions");
        var bitmap = new Bitmap(width, height);
        for (var i = 0; i < pixels.Length; i++) { var p = pixels[i]; var o = i * 4; bitmap.Pixels[o] = (byte)p; bitmap.Pixels[o + 1] = (byte)(p >> 8); bitmap.Pixels[o + 2] = (byte)(p >> 16); bitmap.Pixels[o + 3] = (byte)(p >> 24); }
        return bitmap;
    }

    private static uint[] DecodeImage(ref Bits reader, int width, int height, bool top, ImageDecoderOptions options)
    {
        options.ValidateDimensions(width, height);
        var cacheBits = reader.Read(1) != 0 ? reader.Read(4) : 0;
        if (cacheBits is > 0 and (< 1 or > 11)) throw Bad($"color cache size {cacheBits}");
        var cache = cacheBits == 0 ? null : new uint[1 << cacheBits];
        uint[]? meta = null; var metaBits = 0; var metaWidth = 0; var groupCount = 1;
        if (top && reader.Read(1) != 0)
        {
            metaBits = reader.Read(3) + 2; metaWidth = Div(width, 1 << metaBits);
            meta = DecodeImage(ref reader, metaWidth, Div(height, 1 << metaBits), false, options);
            var maximum = 0; for (var i = 0; i < meta.Length; i++) { meta[i] = meta[i] >> 8 & 0xffff; maximum = Math.Max(maximum, (int)meta[i]); }
            groupCount = maximum + 1; if (groupCount > Math.Min(65536, checked(width * height))) throw Bad("Huffman groups");
        }
        var groups = new Group[groupCount];
        for (var i = 0; i < groups.Length; i++) groups[i] = new Group(Tree(ref reader, 280 + (cache?.Length ?? 0)), Tree(ref reader, 256), Tree(ref reader, 256), Tree(ref reader, 256), Tree(ref reader, 40));
        var output = new uint[checked(width * height)]; var position = 0;
        while (position < output.Length)
        {
            var x = position % width; var y = position / width; var group = groups[meta == null ? 0 : meta[(y >> metaBits) * metaWidth + (x >> metaBits)]];
            var symbol = group.G.Read(ref reader);
            if (symbol < 256)
            {
                var red = group.R.Read(ref reader); var blue = group.B.Read(ref reader); var alpha = group.A.Read(ref reader);
                Emit((uint)(alpha << 24 | red << 16 | symbol << 8 | blue));
            }
            else if (symbol < 280)
            {
                var length = Prefix(symbol - 256, ref reader); var distance = MapDistance(width, Prefix(group.D.Read(ref reader), ref reader));
                if (distance > position || length > output.Length - position) throw Bad("backward reference");
                for (var i = 0; i < length; i++) Emit(output[position - distance]);
            }
            else { var index = symbol - 280; if (cache == null || index >= cache.Length) throw Bad("cache index"); Emit(cache[index]); }
        }
        return output;
        void Emit(uint pixel) { output[position++] = pixel; if (cache != null) cache[unchecked(0x1e35a7bdu * pixel >> (32 - cacheBits))] = pixel; }
    }

    private static Huff Tree(ref Bits reader, int alphabet)
    {
        var lengths = new byte[alphabet];
        if (reader.Read(1) != 0)
        {
            var count = reader.Read(1) + 1; var symbol = reader.Read(reader.Read(1) == 0 ? 1 : 8); Set(symbol);
            if (count == 2) Set(reader.Read(8));
        }
        else
        {
            Span<byte> helperLengths = stackalloc byte[19]; var count = reader.Read(4) + 4;
            for (var i = 0; i < count; i++) helperLengths[LengthOrder[i]] = (byte)reader.Read(3);
            var helper = new Huff(helperLengths); var max = reader.Read(1) == 0 ? alphabet : 2 + reader.Read(2 + 2 * reader.Read(3));
            if (max > alphabet) throw Bad("Huffman limit"); var index = 0; var previous = 8;
            while (index < alphabet && max-- > 0)
            {
                var value = helper.Read(ref reader);
                if (value < 16) { lengths[index++] = (byte)value; if (value != 0) previous = value; }
                else { var repeat = value switch { 16 => 3 + reader.Read(2), 17 => 3 + reader.Read(3), 18 => 11 + reader.Read(7), _ => throw Bad("code length") }; if (index + repeat > alphabet) throw Bad("Huffman repeat"); var length = value == 16 ? previous : 0; while (repeat-- > 0) lengths[index++] = (byte)length; }
            }
        }
        return new Huff(lengths);
        void Set(int symbol) { if (symbol >= alphabet) throw Bad("Huffman symbol"); lengths[symbol] = 1; }
    }

    private static uint[] Inverse(Tx tx, uint[] pixels)
    {
        if (tx.Type == 2) { for (var i = 0; i < pixels.Length; i++) { var p = pixels[i]; var g = (byte)(p >> 8); pixels[i] = p & 0xff00ff00u | (uint)(byte)((p >> 16) + g) << 16 | (byte)(p + g); } return pixels; }
        if (tx.Type == 1) { Color(pixels, tx); return pixels; }
        if (tx.Type == 0) { Predict(pixels, tx); return pixels; }
        var packedWidth = Div(tx.Width, 1 << tx.WidthBits); var output = new uint[checked(tx.Width * tx.Height)]; var bits = 8 >> tx.WidthBits; var mask = (1 << bits) - 1;
        for (var y = 0; y < tx.Height; y++) for (var x = 0; x < tx.Width; x++) { var packed = pixels[y * packedWidth + (x >> tx.WidthBits)]; var index = ((int)(packed >> 8) >> ((x & ((1 << tx.WidthBits) - 1)) * bits)) & mask; output[y * tx.Width + x] = index < tx.Palette!.Length ? tx.Palette[index] : 0; }
        return output;
    }

    private static void Color(uint[] pixels, Tx tx)
    {
        var w = Div(tx.Width, 1 << tx.Bits);
        for (var y = 0; y < tx.Height; y++) for (var x = 0; x < tx.Width; x++) { var i = y * tx.Width + x; var p = pixels[i]; var t = tx.Data![(y >> tx.Bits) * w + (x >> tx.Bits)]; var g = (byte)(p >> 8); var r = unchecked((byte)((p >> 16) + Delta((byte)t, g))); var b = unchecked((byte)(p + Delta((byte)(t >> 8), g) + Delta((byte)(t >> 16), r))); pixels[i] = p & 0xff00ff00u | (uint)r << 16 | b; }
    }

    private static void Predict(uint[] pixels, Tx tx)
    {
        var mw = Div(tx.Width, 1 << tx.Bits);
        for (var y = 0; y < tx.Height; y++) for (var x = 0; x < tx.Width; x++)
        {
            var i = y * tx.Width + x; uint p;
            if (x == 0 && y == 0) p = 0xff000000; else if (y == 0) p = pixels[i - 1]; else if (x == 0) p = pixels[i - tx.Width];
            else { var m = (byte)(tx.Data![(y >> tx.Bits) * mw + (x >> tx.Bits)] >> 8); var l = pixels[i - 1]; var t = pixels[i - tx.Width]; var tl = pixels[i - tx.Width - 1]; var tr = x + 1 < tx.Width ? pixels[i - tx.Width + 1] : pixels[y * tx.Width]; p = m switch { 0 => 0xff000000, 1 => l, 2 => t, 3 => tr, 4 => tl, 5 => Avg(Avg(l,tr),t), 6 => Avg(l,tl), 7 => Avg(l,t), 8 => Avg(tl,t), 9 => Avg(t,tr), 10 => Avg(Avg(l,tl),Avg(t,tr)), 11 => Select(l,t,tl), 12 => Clamp(l,t,tl,false), 13 => Clamp(Avg(l,t),tl,0,true), _ => throw Bad("predictor") }; }
            pixels[i] = Add(pixels[i], p);
        }
    }

    private static int Prefix(int s, ref Bits r) { if (s < 4) return s + 1; var n = (s - 2) >> 1; return ((2 + (s & 1)) << n) + r.Read(n) + 1; }
    private static int MapDistance(int width, int code) { if (code > 120) return code - 120; if (code < 1) throw Bad("distance"); var p = Plane[code - 1]; return Math.Max(1, (p >> 4) * width + 8 - (p & 15)); }
    private static int Div(int a, int b) => (a + b - 1) / b;
    private static int Delta(byte a, byte b) => unchecked((sbyte)a) * unchecked((sbyte)b) >> 5;
    private static uint Add(uint a,uint b) => (uint)(byte)(a+b)|(uint)(byte)((a>>8)+(b>>8))<<8|(uint)(byte)((a>>16)+(b>>16))<<16|(uint)(byte)((a>>24)+(b>>24))<<24;
    private static uint Avg(uint a,uint b) => (uint)(((byte)a+(byte)b)>>1)|(uint)(((byte)(a>>8)+(byte)(b>>8))>>1)<<8|(uint)(((byte)(a>>16)+(byte)(b>>16))>>1)<<16|(uint)(((byte)(a>>24)+(byte)(b>>24))>>1)<<24;
    private static uint Select(uint l,uint t,uint tl) { var dl=0;var dt=0;for(var s=0;s<32;s+=8){var e=(byte)(l>>s)+(byte)(t>>s)-(byte)(tl>>s);dl+=Math.Abs(e-(byte)(l>>s));dt+=Math.Abs(e-(byte)(t>>s));}return dl<dt?l:t; }
    private static uint Clamp(uint a,uint b,uint c,bool half){uint r=0;for(var s=0;s<32;s+=8){var av=(byte)(a>>s);var v=half?av+(av-(byte)(b>>s))/2:av+(byte)(b>>s)-(byte)(c>>s);r|=(uint)Math.Clamp(v,0,255)<<s;}return r;}
    private static InvalidDataException Bad(string part) => new($"Invalid VP8L {part}.");
    private readonly record struct Tx(int Type,int Width,int Height,int Bits,uint[]? Data,uint[]? Palette,int WidthBits);
    private readonly record struct Group(Huff G,Huff R,Huff B,Huff A,Huff D);

    private sealed class Huff
    {
        private readonly int[] _count=new int[16],_first=new int[16],_offset=new int[16],_symbols; private readonly int _single=-1;
        public Huff(ReadOnlySpan<byte> lengths){var total=0;for(var i=0;i<lengths.Length;i++)if(lengths[i]>0){if(lengths[i]>15)throw Bad("Huffman length");_count[lengths[i]]++;total++;}if(total==0)throw Bad("empty Huffman tree");_symbols=new int[total];if(total==1){for(var i=0;i<lengths.Length;i++)if(lengths[i]!=0){_single=i;break;}return;}var open=1;var code=0;var offset=0;for(var l=1;l<=15;l++){open=(open<<1)-_count[l];if(open<0)throw Bad("oversubscribed Huffman tree");_first[l]=code;_offset[l]=offset;offset+=_count[l];code=(code+_count[l])<<1;}if(open!=0)throw Bad("incomplete Huffman tree");var next=(int[])_offset.Clone();for(var s=0;s<lengths.Length;s++)if(lengths[s]>0)_symbols[next[lengths[s]]++]=s;}
        public int Read(ref Bits r){if(_single>=0)return _single;var code=0;for(var l=1;l<=15;l++){code=code<<1|r.Read(1);var d=code-_first[l];if((uint)d<(uint)_count[l])return _symbols[_offset[l]+d];}throw Bad("Huffman code");}
    }
    private ref struct Bits{private readonly ReadOnlySpan<byte> _data;private int _pos;public Bits(ReadOnlySpan<byte>d)=>_data=d;public int Read(int n){if(n is <0 or >24||(long)_pos+n>(long)_data.Length*8)throw Bad("bitstream");var v=0;for(var i=0;i<n;i++,_pos++)v|=((_data[_pos>>3]>>(_pos&7))&1)<<i;return v;}}
}
