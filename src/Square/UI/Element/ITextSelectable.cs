using Square.Graphics;

namespace Square.UI;

public interface ITextSelectable
{
    string SelectableText { get; }
    Rect SelectableTextBounds { get; }
}
