namespace Square.Images.Jpeg;

internal sealed class JpegBitReader
{
    private readonly byte[] _data;
    private int _offset;
    private ulong _bits;
    private int _count;
    private bool _marker;
    private int _markerValue;

    public JpegBitReader(ReadOnlySpan<byte> data, int offset)
    {
        _data = data.ToArray();
        _offset = offset;
    }

    private void Fill()
    {
        while (_count <= 24 && !_marker)
        {
            if (_offset >= _data.Length) return;
            var value = _data[_offset++];
            if (value != 0xFF)
            {
                _bits = (_bits << 8) | value;
                _count += 8;
                continue;
            }
            if (_offset >= _data.Length) throw new InvalidDataException("JPEG entropy data is truncated after 0xFF.");
            var next = _data[_offset++];
            if (next == 0)
            {
                _bits = (_bits << 8) | 0xFF;
                _count += 8;
            }
            else
            {
                _marker = true;
                _markerValue = 0xFF00 | next;
                break;
            }
        }
    }

    public int Receive(int length)
    {
        if (_count < length) Fill();
        if (_count < length) throw new InvalidDataException("JPEG entropy data is truncated.");
        _count -= length;
        return (int)((_bits >> _count) & ((1UL << length) - 1UL));
    }

    public int DecodeHuffman(JpegHuffmanTable.Table table)
    {
        var code = 0;
        for (var length = 1; length <= 16; length++)
        {
            if (_count < length) Fill();
            if (_count < length) throw new InvalidDataException("JPEG entropy data is truncated.");
            var bit = (int)((_bits >> (_count - 1)) & 1UL);
            _count--;
            code = (code << 1) | bit;
            if (code <= table.MaxCode[length])
            {
                var index = table.ValueOffset[length] + (code - table.MinCode[length]);
                return table.Values[index];
            }
        }
        throw new InvalidDataException("JPEG Huffman code is invalid.");
    }

    public int Extend(int value, int length)
    {
        if (length == 0) return 0;
        var extended = value;
        if (extended < (1 << (length - 1))) extended -= (1 << length) - 1;
        return extended;
    }

    public void CheckRestart(int expected)
    {
        if (_marker)
        {
            if (_markerValue != expected) throw new InvalidDataException("JPEG restart marker was expected.");
            ResetState();
            return;
        }
        _bits = 0;
        _count = 0;
        while (_offset < _data.Length && _data[_offset] != 0xFF) _offset++;
        if (_offset + 1 >= _data.Length) throw new InvalidDataException("JPEG restart marker is missing.");
        var marker = 0xFF00 | _data[_offset + 1];
        _offset += 2;
        if (marker != expected) throw new InvalidDataException("JPEG restart marker was expected.");
        ResetState();
    }

    private void ResetState()
    {
        _bits = 0;
        _count = 0;
        _marker = false;
        _markerValue = 0;
    }
}