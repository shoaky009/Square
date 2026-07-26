namespace Square.UI;

/// <summary>
/// DOM text node, aligned with <c>Text</c> / <c>#text</c>.
/// This is distinct from <c>Square.Controls.Text</c>, which is a UIElement control.
/// </summary>
public sealed class Text : CharacterData
{
    /// <summary>构造文本节点并指定初始数据。</summary>
    public Text(string data = "") : base(data)
    {
    }

    /// <inheritdoc />
    public override NodeType NodeTypeValue => NodeType.Text;

    /// <inheritdoc />
    public override string NodeName => "#text";
}
