namespace Square.Images.Jpeg;

internal static class InverseDct
{
    private static readonly float[] Cos = BuildCos();
    private static readonly float Scale0 = 1f / (2f * MathF.Sqrt(2f));
    private static readonly float Scale1 = 1f / 2f;

    private static float[] BuildCos()
    {
        var cos = new float[64];
        for (var u = 0; u < 8; u++)
            for (var x = 0; x < 8; x++)
                cos[u * 8 + x] = MathF.Cos((2 * x + 1) * u * MathF.PI / 16f);
        return cos;
    }

    public static void Transform(ReadOnlySpan<int> input, Span<int> output)
    {
        Span<float> temp = stackalloc float[64];
        for (var v = 0; v < 8; v++)
        {
            for (var x = 0; x < 8; x++)
            {
                var sum = 0f;
                for (var u = 0; u < 8; u++)
                {
                    var value = input[v * 8 + u];
                    if (value != 0) sum += (u == 0 ? Scale0 : Scale1) * value * Cos[u * 8 + x];
                }
                temp[v * 8 + x] = sum;
            }
        }
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var sum = 0f;
                for (var v = 0; v < 8; v++)
                {
                    var value = temp[v * 8 + x];
                    if (value != 0f) sum += (v == 0 ? Scale0 : Scale1) * value * Cos[v * 8 + y];
                }
                var sample = (int)MathF.Round(sum + 128f);
                if (sample < 0) sample = 0; else if (sample > 255) sample = 255;
                output[y * 8 + x] = sample;
            }
        }
    }
}