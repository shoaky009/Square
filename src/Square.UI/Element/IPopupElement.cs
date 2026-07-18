using Square.Graphics;

namespace Square.UI;

public interface IPopupElement
{
    bool IsPopupOpen { get; }
    Rect PopupBounds { get; }
    Element? HitTestPopup(Point point);
    void PaintPopup(IRenderContext context);
}
