namespace Square.Images.Webp.Vp8;

internal static partial class Vp8Decoder
{
    private sealed partial class Decoder
    {
        private const int PlaneY1WithY2 = 0, PlaneY2 = 1, PlaneUv = 2, PlaneY1WithoutY2 = 3;
        private const int PredDc = 0, PredTm = 1, PredVe = 2, PredHe = 3, PredRd = 4,
            PredVr = 5, PredLd = 6, PredVl = 7, PredHd = 8, PredHu = 9;

        private bool Reconstruct(int mbx, int mby)
        {
            if (segmentHeader.UpdateMap)
                segment = !first.ReadBit(segmentHeader.Probability[0])
                    ? (int)first.ReadUInt(segmentHeader.Probability[1], 1)
                    : 2 + (int)first.ReadUInt(segmentHeader.Probability[2], 1);
            var skip = useSkipProb && first.ReadBit(skipProb);
            Array.Clear(coeff);
            PrepareWorkspace(mbx, mby);
            usePredY16 = first.ReadBit(145);
            if (usePredY16) ParsePredY16(mbx); else ParsePredY4(mbx);
            ParsePredC8();
            if (!skip) skip = ParseResiduals(mbx, mby);
            else
            {
                if (usePredY16) { leftMb.NzY16 = 0; upMb[mbx].NzY16 = 0; }
                leftMb.NzMask = 0; upMb[mbx].NzMask = 0; nzDcMask = nzAcMask = 0;
            }
            ReconstructMacroblock(mbx, mby);
            CopyMacroblock(mbx, mby);
            return skip;
        }

        private void ParsePredY16(int mbx)
        {
            byte mode;
            if (!first.ReadBit(156)) mode = !first.ReadBit(163) ? (byte)PredDc : (byte)PredVe;
            else mode = !first.ReadBit(128) ? (byte)PredHe : (byte)PredTm;
            predY16 = mode;
            for (var i = 0; i < 4; i++) { upMb[mbx].Pred[i] = mode; leftMb.Pred[i] = mode; }
        }

        private void ParsePredC8()
        {
            if (!first.ReadBit(142)) predC8 = PredDc;
            else if (!first.ReadBit(114)) predC8 = PredVe;
            else if (!first.ReadBit(183)) predC8 = PredHe;
            else predC8 = PredTm;
        }

        private void ParsePredY4(int mbx)
        {
            for (var row = 0; row < 4; row++)
            {
                var left = leftMb.Pred[row];
                for (var column = 0; column < 4; column++)
                {
                    var p = (upMb[mbx].Pred[column] * 10 + left) * 9;
                    byte mode;
                    if (!first.ReadBit(Vp8Tables.PredProb[p])) mode = PredDc;
                    else if (!first.ReadBit(Vp8Tables.PredProb[p + 1])) mode = PredTm;
                    else if (!first.ReadBit(Vp8Tables.PredProb[p + 2])) mode = PredVe;
                    else if (!first.ReadBit(Vp8Tables.PredProb[p + 3]))
                    {
                        if (!first.ReadBit(Vp8Tables.PredProb[p + 4])) mode = PredHe;
                        else if (!first.ReadBit(Vp8Tables.PredProb[p + 5])) mode = PredRd;
                        else mode = PredVr;
                    }
                    else if (!first.ReadBit(Vp8Tables.PredProb[p + 6])) mode = PredLd;
                    else if (!first.ReadBit(Vp8Tables.PredProb[p + 7])) mode = PredVl;
                    else if (!first.ReadBit(Vp8Tables.PredProb[p + 8])) mode = PredHd;
                    else mode = PredHu;
                    predY4[row, column] = mode;
                    upMb[mbx].Pred[column] = left = mode;
                }
                leftMb.Pred[row] = left;
            }
        }

        private bool ParseResiduals(int mbx, int mby)
        {
            var reader = tokens[mby & (tokens.Length - 1)];
            var q = quant[segment];
            var plane = PlaneY1WithoutY2;
            if (usePredY16)
            {
                var nz = ParseBlock(reader, PlaneY2, leftMb.NzY16 + upMb[mbx].NzY16,
                    q.Y2Dc, q.Y2Ac, false, 384);
                leftMb.NzY16 = upMb[mbx].NzY16 = nz;
                InverseWht();
                plane = PlaneY1WithY2;
            }

            Span<byte> leftNz = stackalloc byte[4];
            Span<byte> upNz = stackalloc byte[4];
            Span<byte> dc = stackalloc byte[4];
            Span<byte> ac = stackalloc byte[4];
            Unpack(leftMb.NzMask & 15, leftNz);
            Unpack(upMb[mbx].NzMask & 15, upNz);
            uint dcMask = 0, acMask = 0;
            var baseIndex = 0;
            for (var row = 0; row < 4; row++)
            {
                var nz = leftNz[row];
                for (var column = 0; column < 4; column++)
                {
                    nz = ParseBlock(reader, plane, nz + upNz[column], q.Y1Dc, q.Y1Ac, usePredY16, baseIndex);
                    upNz[column] = ac[column] = nz;
                    dc[column] = coeff[baseIndex] == 0 ? (byte)0 : (byte)1;
                    baseIndex += 16;
                }
                leftNz[row] = nz;
                dcMask |= Pack(dc) << (row * 4);
                acMask |= Pack(ac) << (row * 4);
            }
            var leftMask = Pack(leftNz);
            var upMask = Pack(upNz);

            Unpack(leftMb.NzMask >> 4, leftNz);
            Unpack(upMb[mbx].NzMask >> 4, upNz);
            for (var color = 0; color < 4; color += 2)
            {
                for (var row = 0; row < 2; row++)
                {
                    var nz = leftNz[row + color];
                    for (var column = 0; column < 2; column++)
                    {
                        nz = ParseBlock(reader, PlaneUv, nz + upNz[column + color], q.UvDc, q.UvAc, false, baseIndex);
                        upNz[column + color] = nz;
                        ac[row * 2 + column] = nz;
                        dc[row * 2 + column] = coeff[baseIndex] == 0 ? (byte)0 : (byte)1;
                        baseIndex += 16;
                    }
                    leftNz[row + color] = nz;
                }
                dcMask |= Pack(dc) << (16 + color * 2);
                acMask |= Pack(ac) << (16 + color * 2);
            }
            leftMask |= Pack(leftNz) << 4;
            upMask |= Pack(upNz) << 4;
            leftMb.NzMask = (byte)leftMask;
            upMb[mbx].NzMask = (byte)upMask;
            nzDcMask = dcMask; nzAcMask = acMask;
            return dcMask == 0 && acMask == 0;
        }

        private byte ParseBlock(BoolDecoder reader, int plane, int context, int dcQuant, int acQuant,
            bool skipFirst, int coefficientBase)
        {
            var n = skipFirst ? 1 : 0;
            var p = TokenIndex(plane, Vp8Tables.Bands[n], context, 0);
            if (!reader.ReadBit(tokenProb[p])) return 0;
            while (n != 16)
            {
                n++;
                if (!reader.ReadBit(tokenProb[p + 1])) { p = TokenIndex(plane, Vp8Tables.Bands[n], 0, 0); continue; }
                uint value;
                if (!reader.ReadBit(tokenProb[p + 2]))
                {
                    value = 1; p = TokenIndex(plane, Vp8Tables.Bands[n], 1, 0);
                }
                else
                {
                    if (!reader.ReadBit(tokenProb[p + 3]))
                        value = !reader.ReadBit(tokenProb[p + 4]) ? 2u : 3u + reader.ReadUInt(tokenProb[p + 5], 1);
                    else if (!reader.ReadBit(tokenProb[p + 6]))
                        value = !reader.ReadBit(tokenProb[p + 7]) ? 5u + reader.ReadUInt(159, 1)
                            : 7u + 2 * reader.ReadUInt(165, 1) + reader.ReadUInt(145, 1);
                    else
                    {
                        var b1 = (int)reader.ReadUInt(tokenProb[p + 8], 1);
                        var b0 = (int)reader.ReadUInt(tokenProb[p + 9 + b1], 1);
                        var category = 2 * b1 + b0;
                        value = 0;
                        for (var i = 0; Vp8Tables.CategoryProb[category, i] != 0; i++)
                            value = value * 2 + reader.ReadUInt(Vp8Tables.CategoryProb[category, i], 1);
                        value += (uint)(3 + (8 << category));
                    }
                    p = TokenIndex(plane, Vp8Tables.Bands[n], 2, 0);
                }
                var zigzag = Vp8Tables.ZigZag[n - 1];
                var valueSigned = checked((int)value * (zigzag == 0 ? dcQuant : acQuant));
                if (reader.ReadBit(128)) valueSigned = -valueSigned;
                coeff[coefficientBase + zigzag] = unchecked((short)valueSigned);
                if (n == 16 || !reader.ReadBit(tokenProb[p])) return 1;
            }
            return 1;
        }

        private static int TokenIndex(int plane, int band, int context, int probability)
            => (((plane * 8 + band) * 3 + context) * 11) + probability;

        private static void Unpack(int value, Span<byte> output)
        {
            for (var i = 0; i < 4; i++) output[i] = (byte)(value >> i & 1);
        }

        private static uint Pack(ReadOnlySpan<byte> values)
            => (uint)(values[0] | values[1] << 1 | values[2] << 2 | values[3] << 3);

        private void PrepareWorkspace(int mbx, int mby)
        {
            if (mbx == 0)
            {
                for (var row = 0; row < 17; row++) W(row, 7, 129);
                for (var row = 17; row < 26; row++) { W(row, 7, 129); W(row, 23, 129); }
            }
            else
            {
                for (var row = 0; row < 17; row++) W(row, 7, R(row, 23));
                for (var row = 17; row < 26; row++) { W(row, 7, R(row, 15)); W(row, 23, R(row, 31)); }
            }
            if (mby == 0)
            {
                for (var x = 7; x < 28; x++) W(0, x, 127);
                for (var x = 7; x < 16; x++) W(17, x, 127);
                for (var x = 23; x < 32; x++) W(17, x, 127);
            }
            else
            {
                var yBase = (mby * 16 - 1) * yStride + mbx * 16;
                for (var x = 0; x < 16; x++) W(0, 8 + x, yPlane[yBase + x]);
                var cBase = (mby * 8 - 1) * cStride + mbx * 8;
                for (var x = 0; x < 8; x++) { W(17, 8 + x, cbPlane[cBase + x]); W(17, 24 + x, crPlane[cBase + x]); }
                for (var x = 16; x < 20; x++)
                    W(0, 8 + x, yPlane[yBase + (mbx == mbWidth - 1 ? 15 : x)]);
            }
            for (var row = 4; row < 16; row += 4)
                for (var x = 24; x < 28; x++) W(row, x, R(0, x));
        }

        private void ReconstructMacroblock(int mbx, int mby)
        {
            if (usePredY16)
            {
                PredictLarge(1, 8, 16, BorderMode(mbx, mby, predY16));
                for (var row = 0; row < 4; row++) for (var column = 0; column < 4; column++)
                    AddResidual(4 * row + 1, 4 * column + 8, 16 * (4 * row + column), 1u << (4 * row + column));
            }
            else
            {
                for (var row = 0; row < 4; row++) for (var column = 0; column < 4; column++)
                {
                    var block = 4 * row + column;
                    Predict4(4 * row + 1, 4 * column + 8, predY4[row, column]);
                    AddResidual(4 * row + 1, 4 * column + 8, 16 * block, 1u << block);
                }
            }
            var chromaMode = BorderMode(mbx, mby, predC8);
            PredictLarge(18, 8, 8, chromaMode);
            AddResidual8(18, 8, 256);
            PredictLarge(18, 24, 8, chromaMode);
            AddResidual8(18, 24, 320);
        }

        private void AddResidual(int row, int column, int coefficientBase, uint mask)
        {
            if ((nzAcMask & mask) != 0) InverseDct(row, column, coefficientBase);
            else if ((nzDcMask & mask) != 0) InverseDctDc(row, column, coefficientBase);
        }

        private void AddResidual8(int row, int column, int coefficientBase)
        {
            for (var y = 0; y < 2; y++) for (var x = 0; x < 2; x++)
            {
                var blockMask = 1u << (16 + ((coefficientBase - 256) / 64) * 4 + y * 2 + x);
                if ((nzAcMask & blockMask) != 0) InverseDct(row + 4 * y, column + 4 * x, coefficientBase + 16 * (2 * y + x));
                else if ((nzDcMask & blockMask) != 0) InverseDctDc(row + 4 * y, column + 4 * x, coefficientBase + 16 * (2 * y + x));
            }
        }

        private void CopyMacroblock(int mbx, int mby)
        {
            for (var row = 0; row < 16; row++)
                work.AsSpan((1 + row) * 32 + 8, 16).CopyTo(yPlane.AsSpan((mby * 16 + row) * yStride + mbx * 16, 16));
            for (var row = 0; row < 8; row++)
            {
                work.AsSpan((18 + row) * 32 + 8, 8).CopyTo(cbPlane.AsSpan((mby * 8 + row) * cStride + mbx * 8, 8));
                work.AsSpan((18 + row) * 32 + 24, 8).CopyTo(crPlane.AsSpan((mby * 8 + row) * cStride + mbx * 8, 8));
            }
        }

        private static int BorderMode(int mbx, int mby, int mode)
        {
            if (mode != PredDc) return mode;
            if (mbx == 0) return mby == 0 ? 12 : 11;
            return mby == 0 ? 10 : PredDc;
        }

        private byte R(int row, int column) => work[row * 32 + column];
        private void W(int row, int column, byte value) => work[row * 32 + column] = value;
        private void W(int row, int column, int value) => W(row, column, (byte)value);
    }
}
