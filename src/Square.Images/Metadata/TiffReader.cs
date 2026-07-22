using System.Buffers.Binary;

namespace Square.Images.Metadata;

internal readonly record struct TiffEntry(ushort Tag, ushort Type, uint Count, int ValueOffset);
internal readonly record struct TiffDirectory(TiffEntry[] Entries, uint NextOffset);

internal ref struct TiffReader
{
    private readonly ReadOnlySpan<byte> _data;
    public bool LittleEndian { get; }
    public uint FirstIfdOffset { get; }

    public TiffReader(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8) throw new InvalidDataException("TIFF header is truncated.");
        LittleEndian = data[0] == (byte)'I' && data[1] == (byte)'I';
        var bigEndian = data[0] == (byte)'M' && data[1] == (byte)'M';
        if (!LittleEndian && !bigEndian) throw new InvalidDataException("Invalid TIFF byte order.");
        _data = data;
        if (ReadUInt16(2) != 42) throw new InvalidDataException("Unsupported TIFF signature.");
        FirstIfdOffset = ReadUInt32(4);
    }

    public TiffDirectory ReadDirectory(uint offset, ImageDecoderOptions options, HashSet<uint> visited, ref int totalTags)
    {
        if (!visited.Add(offset)) throw new InvalidDataException("TIFF IFD chain contains a cycle.");
        if (offset > _data.Length - 2) throw new InvalidDataException("TIFF IFD offset is outside the data block.");
        var count = ReadUInt16((int)offset);
        totalTags = checked(totalTags + count);
        if (totalTags > options.MaxExifTagCount) throw new InvalidDataException("TIFF tag count exceeds the configured limit.");
        var entriesOffset = checked((long)offset + 2);
        var nextOffsetPosition = checked(entriesOffset + count * 12L);
        if (nextOffsetPosition + 4 > _data.Length) throw new InvalidDataException("TIFF IFD is truncated.");
        var entries = new TiffEntry[count];
        for (var i = 0; i < count; i++)
        {
            var entryOffset = checked((int)(entriesOffset + i * 12L));
            entries[i] = new TiffEntry(ReadUInt16(entryOffset), ReadUInt16(entryOffset + 2),
                ReadUInt32(entryOffset + 4), entryOffset + 8);
        }
        return new TiffDirectory(entries, ReadUInt32((int)nextOffsetPosition));
    }

    public TiffEntry? Find(TiffDirectory directory, ushort tag)
    {
        foreach (var entry in directory.Entries)
            if (entry.Tag == tag) return entry;
        return null;
    }

    public uint GetSingle(TiffEntry entry)
    {
        if (entry.Count != 1) throw new InvalidDataException($"TIFF tag 0x{entry.Tag:X4} must contain one value.");
        return GetValues(entry)[0];
    }

    public uint[] GetValues(TiffEntry entry)
    {
        var size = entry.Type switch { 1 => 1, 3 => 2, 4 => 4, _ => 0 };
        if (size == 0) throw new InvalidDataException($"Unsupported TIFF field type {entry.Type}.");
        var bytes = checked((long)entry.Count * size);
        if (bytes > int.MaxValue) throw new InvalidDataException("TIFF field is too large.");
        var offset = bytes <= 4 ? entry.ValueOffset : checked((int)ReadUInt32(entry.ValueOffset));
        if (offset < 0 || bytes > _data.Length - offset) throw new InvalidDataException("TIFF field data is truncated.");
        var values = new uint[checked((int)entry.Count)];
        for (var i = 0; i < values.Length; i++)
            values[i] = entry.Type switch
            {
                1 => _data[offset + i],
                3 => ReadUInt16(offset + i * 2),
                4 => ReadUInt32(offset + i * 4),
                _ => 0
            };
        return values;
    }

    public ReadOnlySpan<byte> Slice(uint offset, uint length)
    {
        if (offset > _data.Length || length > _data.Length - offset) throw new InvalidDataException("TIFF strip data is truncated.");
        return _data.Slice((int)offset, (int)length);
    }

    private ushort ReadUInt16(int offset)
    {
        if ((uint)offset > (uint)(_data.Length - 2)) throw new InvalidDataException("TIFF offset is outside the data block.");
        return LittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(offset, 2))
            : BinaryPrimitives.ReadUInt16BigEndian(_data.Slice(offset, 2));
    }

    private uint ReadUInt32(int offset)
    {
        if ((uint)offset > (uint)(_data.Length - 4)) throw new InvalidDataException("TIFF offset is outside the data block.");
        return LittleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(offset, 4))
            : BinaryPrimitives.ReadUInt32BigEndian(_data.Slice(offset, 4));
    }
}
