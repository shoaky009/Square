namespace Square.Images.Jpeg;

internal static class JpegHuffmanTable
{
    public sealed class Table
    {
        public readonly int[] Values = new int[256];
        public readonly int[] MinCode = new int[17];
        public readonly int[] MaxCode = new int[18];
        public readonly int[] ValueOffset = new int[17];
    }

    public static Table Build(ReadOnlySpan<byte> counts, ReadOnlySpan<byte> values)
    {
        var table = new Table();
        var code = 0;
        var index = 0;
        for (var length = 1; length <= 16; length++)
        {
            table.MinCode[length] = code;
            table.ValueOffset[length] = index;
            var count = counts[length - 1];
            for (var i = 0; i < count; i++)
            {
                if (index >= values.Length) throw new InvalidDataException("JPEG Huffman table is malformed.");
                table.Values[index] = values[index];
                code++;
                index++;
            }
            table.MaxCode[length] = code - 1;
            code <<= 1;
        }
        table.MaxCode[17] = 0x7FFFFF;
        return table;
    }
}