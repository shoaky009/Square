namespace Square.Images.Webp.Vp8;

internal static partial class Vp8Decoder
{
    private sealed partial class Decoder
    {
        private void ComputeFilterParams()
        {
            for (var segmentIndex = 0; segmentIndex < 4; segmentIndex++)
            for (var mode = 0; mode < 2; mode++)
            {
                var baseLevel = (int)filterHeader.Level;
                if (segmentHeader.Enabled)
                {
                    baseLevel = segmentHeader.FilterStrength[segmentIndex];
                    if (segmentHeader.Relative) baseLevel += filterHeader.Level;
                }
                var level = baseLevel;
                if (filterHeader.UseDelta)
                {
                    level += filterHeader.RefDelta[0];
                    if (mode != 0) level += filterHeader.ModeDelta[0];
                }
                var result = new FilterParam { Inner = mode != 0 };
                if (level > 0)
                {
                    level = Math.Min(level, 63);
                    var inner = level;
                    if (filterHeader.Sharpness > 0)
                    {
                        inner >>= filterHeader.Sharpness > 4 ? 2 : 1;
                        inner = Math.Min(inner, 9 - filterHeader.Sharpness);
                    }
                    inner = Math.Max(inner, 1);
                    result.InnerLevel = (byte)inner;
                    result.Level = (byte)(2 * level + inner);
                    result.HighEdgeLevel = level < 15 ? (byte)0 : level < 40 ? (byte)1 : (byte)2;
                }
                filterParams[segmentIndex, mode] = result;
            }
        }

        private void SimpleFilter()
        {
            for (var mby = 0; mby < mbHeight; mby++) for (var mbx = 0; mbx < mbWidth; mbx++)
            {
                var f = perMbFilterParams[mby * mbWidth + mbx];
                if (f.Level == 0) continue;
                var index = (mby * yStride + mbx) * 16;
                if (mbx > 0) Filter2(yPlane, f.Level + 4, index, yStride, 1);
                if (f.Inner)
                {
                    Filter2(yPlane, f.Level, index + 4, yStride, 1);
                    Filter2(yPlane, f.Level, index + 8, yStride, 1);
                    Filter2(yPlane, f.Level, index + 12, yStride, 1);
                }
                if (mby > 0) Filter2(yPlane, f.Level + 4, index, 1, yStride);
                if (f.Inner)
                {
                    Filter2(yPlane, f.Level, index + yStride * 4, 1, yStride);
                    Filter2(yPlane, f.Level, index + yStride * 8, 1, yStride);
                    Filter2(yPlane, f.Level, index + yStride * 12, 1, yStride);
                }
            }
        }

        private void NormalFilter()
        {
            for (var mby = 0; mby < mbHeight; mby++) for (var mbx = 0; mbx < mbWidth; mbx++)
            {
                var f = perMbFilterParams[mby * mbWidth + mbx];
                if (f.Level == 0) continue;
                var yIndex = (mby * yStride + mbx) * 16;
                var cIndex = (mby * cStride + mbx) * 8;
                if (mbx > 0)
                {
                    Filter246(yPlane, 16, f.Level + 4, f.InnerLevel, f.HighEdgeLevel, yIndex, yStride, 1, false);
                    Filter246(cbPlane, 8, f.Level + 4, f.InnerLevel, f.HighEdgeLevel, cIndex, cStride, 1, false);
                    Filter246(crPlane, 8, f.Level + 4, f.InnerLevel, f.HighEdgeLevel, cIndex, cStride, 1, false);
                }
                if (f.Inner)
                {
                    Filter246(yPlane, 16, f.Level, f.InnerLevel, f.HighEdgeLevel, yIndex + 4, yStride, 1, true);
                    Filter246(yPlane, 16, f.Level, f.InnerLevel, f.HighEdgeLevel, yIndex + 8, yStride, 1, true);
                    Filter246(yPlane, 16, f.Level, f.InnerLevel, f.HighEdgeLevel, yIndex + 12, yStride, 1, true);
                    Filter246(cbPlane, 8, f.Level, f.InnerLevel, f.HighEdgeLevel, cIndex + 4, cStride, 1, true);
                    Filter246(crPlane, 8, f.Level, f.InnerLevel, f.HighEdgeLevel, cIndex + 4, cStride, 1, true);
                }
                if (mby > 0)
                {
                    Filter246(yPlane, 16, f.Level + 4, f.InnerLevel, f.HighEdgeLevel, yIndex, 1, yStride, false);
                    Filter246(cbPlane, 8, f.Level + 4, f.InnerLevel, f.HighEdgeLevel, cIndex, 1, cStride, false);
                    Filter246(crPlane, 8, f.Level + 4, f.InnerLevel, f.HighEdgeLevel, cIndex, 1, cStride, false);
                }
                if (f.Inner)
                {
                    Filter246(yPlane, 16, f.Level, f.InnerLevel, f.HighEdgeLevel, yIndex + yStride * 4, 1, yStride, true);
                    Filter246(yPlane, 16, f.Level, f.InnerLevel, f.HighEdgeLevel, yIndex + yStride * 8, 1, yStride, true);
                    Filter246(yPlane, 16, f.Level, f.InnerLevel, f.HighEdgeLevel, yIndex + yStride * 12, 1, yStride, true);
                    Filter246(cbPlane, 8, f.Level, f.InnerLevel, f.HighEdgeLevel, cIndex + cStride * 4, 1, cStride, true);
                    Filter246(crPlane, 8, f.Level, f.InnerLevel, f.HighEdgeLevel, cIndex + cStride * 4, 1, cStride, true);
                }
            }
        }

        private static void Filter2(byte[] pixels, int level, int index, int along, int across)
        {
            for (var count = 0; count < 16; count++, index += along)
            {
                var p1 = pixels[index - 2 * across]; var p0 = pixels[index - across];
                var q0 = pixels[index]; var q1 = pixels[index + across];
                if ((Math.Abs(p0 - q0) << 1) + (Math.Abs(p1 - q1) >> 1) > level) continue;
                var a = 3 * (q0 - p0) + Clamp127(p1 - q1);
                var a1 = Clamp15((a + 4) >> 3); var a2 = Clamp15((a + 3) >> 3);
                pixels[index - across] = Clip8(p0 + a2); pixels[index] = Clip8(q0 - a1);
            }
        }

        private static void Filter246(byte[] pixels, int count, int level, int innerLevel, int highEdgeLevel,
            int index, int along, int across, bool fourNotSix)
        {
            for (; count > 0; count--, index += along)
            {
                var p3 = pixels[index - 4 * across]; var p2 = pixels[index - 3 * across];
                var p1 = pixels[index - 2 * across]; var p0 = pixels[index - across];
                var q0 = pixels[index]; var q1 = pixels[index + across];
                var q2 = pixels[index + 2 * across]; var q3 = pixels[index + 3 * across];
                if ((Math.Abs(p0 - q0) << 1) + (Math.Abs(p1 - q1) >> 1) > level) continue;
                if (Math.Abs(p3 - p2) > innerLevel || Math.Abs(p2 - p1) > innerLevel ||
                    Math.Abs(p1 - p0) > innerLevel || Math.Abs(q1 - q0) > innerLevel ||
                    Math.Abs(q2 - q1) > innerLevel || Math.Abs(q3 - q2) > innerLevel) continue;
                if (Math.Abs(p1 - p0) > highEdgeLevel || Math.Abs(q1 - q0) > highEdgeLevel)
                {
                    var a = 3 * (q0 - p0) + Clamp127(p1 - q1);
                    var a1 = Clamp15((a + 4) >> 3); var a2 = Clamp15((a + 3) >> 3);
                    pixels[index - across] = Clip8(p0 + a2); pixels[index] = Clip8(q0 - a1);
                }
                else if (fourNotSix)
                {
                    var a = 3 * (q0 - p0);
                    var a1 = Clamp15((a + 4) >> 3); var a2 = Clamp15((a + 3) >> 3); var a3 = (a1 + 1) >> 1;
                    pixels[index - 2 * across] = Clip8(p1 + a3); pixels[index - across] = Clip8(p0 + a2);
                    pixels[index] = Clip8(q0 - a1); pixels[index + across] = Clip8(q1 - a3);
                }
                else
                {
                    var a = Clamp127(3 * (q0 - p0) + Clamp127(p1 - q1));
                    var a1 = (27 * a + 63) >> 7; var a2 = (18 * a + 63) >> 7; var a3 = (9 * a + 63) >> 7;
                    pixels[index - 3 * across] = Clip8(p2 + a3); pixels[index - 2 * across] = Clip8(p1 + a2);
                    pixels[index - across] = Clip8(p0 + a1); pixels[index] = Clip8(q0 - a1);
                    pixels[index + across] = Clip8(q1 - a2); pixels[index + 2 * across] = Clip8(q2 - a3);
                }
            }
        }

        private static int Clamp15(int value) => Math.Clamp(value, -16, 15);
        private static int Clamp127(int value) => Math.Clamp(value, -128, 127);
    }
}
