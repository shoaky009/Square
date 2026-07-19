namespace Square.UI;

/// <summary>
/// DOM character data node base, aligned with <c>CharacterData</c>.
/// </summary>
public abstract class CharacterData : Node
{
    private string _data;

    protected CharacterData(string data = "")
    {
        _data = data ?? "";
    }

    public string Data
    {
        get => _data;
        set => _data = value ?? "";
    }

    public int Length => _data.Length;

    public string SubstringData(int offset, int count)
    {
        ValidateOffset(offset);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        return _data.Substring(offset, Math.Min(count, _data.Length - offset));
    }

    public void AppendData(string data) => _data += data ?? "";

    public void InsertData(int offset, string data)
    {
        ValidateOffset(offset);
        _data = _data.Insert(offset, data ?? "");
    }

    public void DeleteData(int offset, int count)
    {
        ValidateOffset(offset);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        _data = _data.Remove(offset, Math.Min(count, _data.Length - offset));
    }

    public void ReplaceData(int offset, int count, string data)
    {
        DeleteData(offset, count);
        InsertData(offset, data);
    }

    private void ValidateOffset(int offset)
    {
        if (offset < 0 || offset > _data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
    }
}
