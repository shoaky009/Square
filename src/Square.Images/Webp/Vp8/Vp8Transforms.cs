namespace Square.Images.Webp.Vp8;

internal static partial class Vp8Decoder
{
    private sealed partial class Decoder
    {
        private void InverseDct(int row, int column, int coefficientBase)
        {
            const int c1 = 85627, c2 = 35468;
            Span<int> matrix = stackalloc int[16];
            for (var i = 0; i < 4; i++)
            {
                var a = coeff[coefficientBase] + coeff[coefficientBase + 8];
                var b = coeff[coefficientBase] - coeff[coefficientBase + 8];
                var c = (coeff[coefficientBase + 4] * c2 >> 16) - (coeff[coefficientBase + 12] * c1 >> 16);
                var d = (coeff[coefficientBase + 4] * c1 >> 16) + (coeff[coefficientBase + 12] * c2 >> 16);
                matrix[i * 4] = a + d; matrix[i * 4 + 1] = b + c;
                matrix[i * 4 + 2] = b - c; matrix[i * 4 + 3] = a - d;
                coefficientBase++;
            }
            for (var y = 0; y < 4; y++)
            {
                var dc = matrix[y] + 4;
                var a = dc + matrix[8 + y]; var b = dc - matrix[8 + y];
                var c = (matrix[4 + y] * c2 >> 16) - (matrix[12 + y] * c1 >> 16);
                var d = (matrix[4 + y] * c1 >> 16) + (matrix[12 + y] * c2 >> 16);
                W(row + y, column, Clip8(R(row + y, column) + ((a + d) >> 3)));
                W(row + y, column + 1, Clip8(R(row + y, column + 1) + ((b + c) >> 3)));
                W(row + y, column + 2, Clip8(R(row + y, column + 2) + ((b - c) >> 3)));
                W(row + y, column + 3, Clip8(R(row + y, column + 3) + ((a - d) >> 3)));
            }
        }

        private void InverseDctDc(int row, int column, int coefficientBase)
        {
            var dc = (coeff[coefficientBase] + 4) >> 3;
            for (var y = 0; y < 4; y++) for (var x = 0; x < 4; x++)
                W(row + y, column + x, Clip8(R(row + y, column + x) + dc));
        }

        private void InverseWht()
        {
            Span<int> matrix = stackalloc int[16];
            for (var i = 0; i < 4; i++)
            {
                var a0 = coeff[384 + i] + coeff[396 + i];
                var a1 = coeff[388 + i] + coeff[392 + i];
                var a2 = coeff[388 + i] - coeff[392 + i];
                var a3 = coeff[384 + i] - coeff[396 + i];
                matrix[i] = a0 + a1; matrix[8 + i] = a0 - a1;
                matrix[4 + i] = a3 + a2; matrix[12 + i] = a3 - a2;
            }
            var output = 0;
            for (var i = 0; i < 4; i++)
            {
                var dc = matrix[i * 4] + 3;
                var a0 = dc + matrix[i * 4 + 3];
                var a1 = matrix[i * 4 + 1] + matrix[i * 4 + 2];
                var a2 = matrix[i * 4 + 1] - matrix[i * 4 + 2];
                var a3 = dc - matrix[i * 4 + 3];
                coeff[output] = (short)((a0 + a1) >> 3);
                coeff[output + 16] = (short)((a3 + a2) >> 3);
                coeff[output + 32] = (short)((a0 - a1) >> 3);
                coeff[output + 48] = (short)((a3 - a2) >> 3);
                output += 64;
            }
        }
    }
}
