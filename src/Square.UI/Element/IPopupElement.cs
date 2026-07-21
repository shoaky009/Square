using Square.Graphics;

namespace Square.UI;

public interface IPopupElement
{
    bool IsPopupOpen { get; }
    Rect PopupBounds { get; }
    bool DismissOnPointerDownOutside { get; }
    bool CloseOnEscape { get; }
    bool ContainsPopupInteraction(Point point);
    bool HandlePopupKey(int keyCode, bool shift, bool control, bool alt);
    Point MapPointToContent(Point point);
    void ClosePopup();
    Element? HitTestPopup(Point point);
    void PaintPopup(IRenderContext context);
}
