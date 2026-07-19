namespace Square.UI;

/// <summary>
/// DOM text node, aligned with <c>Text</c> / <c>#text</c>.
/// This is distinct from <c>Square.Controls.Controls.Text</c>, which is a UIElement control.
/// </summary>
public sealed class Text : CharacterData
{
    public Text(string data = "") : base(data)
    {
    }

    public override NodeType NodeTypeValue => NodeType.Text;

    public override string NodeName => "#text";
}
