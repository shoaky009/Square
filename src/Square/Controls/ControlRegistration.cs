using Square.Controls;
using Square.UI;

namespace Square.Controls;

public static class ControlRegistration
{
    private static bool _registered;

    public static void RegisterDefaults()
    {
        if (_registered) return;
        _registered = true;

        UIDocument.RegisterElement("View", static () => new View());
        UIDocument.RegisterElement("ScrollViewer", static () => new ScrollViewer());
        UIDocument.RegisterElement("Popup", static () => new Popup());
        UIDocument.RegisterElement("Dialog", static () => new Dialog());
        UIDocument.RegisterElement("MenuBar", static () => new MenuBar());
        UIDocument.RegisterElement("Menu", static () => new Menu());
        UIDocument.RegisterElement("ContextMenu", static () => new ContextMenu());
        UIDocument.RegisterElement("MenuItem", static () => new MenuItem());
        UIDocument.RegisterElement("MenuSeparator", static () => new MenuSeparator());
        UIDocument.RegisterElement("Text", static () => new Controls.Text());
        UIDocument.RegisterElement("List", static () => new Controls.List());
        UIDocument.RegisterElement("ListItem", static () => new ListItem());
        UIDocument.RegisterElement("Tree", static () => new Tree());
        UIDocument.RegisterElement("TreeItem", static () => new TreeItem());
        UIDocument.RegisterElement("Swiper", static () => new Swiper());
        UIDocument.RegisterElement("Link", static () => new Controls.Link());
        UIDocument.RegisterElement("Button", static () => new Button());
        UIDocument.RegisterElement("Input", static () => new Input());
        UIDocument.RegisterElement("TextArea", static () => new TextArea());
        UIDocument.RegisterElement("CheckBox", static () => new CheckBox());
        UIDocument.RegisterElement("Radio", static () => new Radio());
        UIDocument.RegisterElement("Select", static () => new Select());
        UIDocument.RegisterElement("Image", static () => new Controls.Image());
        UIDocument.RegisterElement("Canvas", static () => new Canvas());
        UIDocument.RegisterElement("UI", static () => new UIRootElement());
        UIDocument.RegisterElement("Head", static () => new UIHeadElement());
        UIDocument.RegisterElement("Body", static () => new UIBodyElement());
    }
}
