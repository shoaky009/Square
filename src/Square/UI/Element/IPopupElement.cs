using Square.Graphics;

namespace Square.UI;

/// <summary>弹出层元素接口（Square 扩展；用于 Tooltip/Menu 等浮层）。</summary>
public interface IPopupElement
{
    /// <summary>是否以布局覆盖层形式渲染。</summary>
    bool IsLayoutOverlay => true;
    /// <summary>弹窗是否处于打开状态。</summary>
    bool IsPopupOpen { get; }
    /// <summary>弹窗边界矩形。</summary>
    Rect PopupBounds { get; }
    /// <summary>在弹窗外按下指针时是否关闭弹窗。</summary>
    bool DismissOnPointerDownOutside { get; }
    /// <summary>按 ESC 时是否关闭弹窗。</summary>
    bool CloseOnEscape { get; }
    /// <summary>判断指定点是否在弹窗交互区内。</summary>
    bool ContainsPopupInteraction(Point point);
    /// <summary>处理弹窗收到的键盘事件。</summary>
    bool HandlePopupKey(int keyCode, bool shift, bool control, bool alt);
    /// <summary>将弹窗坐标点映射到内容坐标。</summary>
    Point MapPointToContent(Point point);
    /// <summary>关闭弹窗。</summary>
    void ClosePopup();
    /// <summary>对弹窗进行命中测试。</summary>
    Element? HitTestPopup(Point point);
    /// <summary>绘制弹窗。</summary>
    void PaintPopup(IRenderContext context);
}