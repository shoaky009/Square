namespace Square.Graphics.Codecs;

internal static class Crc32
{
    private static readonly uint[] Table = CreateTable();

    public static uint Compute(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var crc = 0xFFFFFFFFu;
        crc = Update(crc, first);
        crc = Update(crc, second);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint Update(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            table[i] = value;
        }

        return table;
    }
}
