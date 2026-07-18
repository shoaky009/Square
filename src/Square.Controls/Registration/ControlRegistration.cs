using Square.Controls.Controls;
using Square.UI;

namespace Square.Controls.Registration;

public static class ControlRegistration
{
    private static bool _registered;

    public static void RegisterDefaults()
    {
        if (_registered) return;
        _registered = true;

        UIDocument.RegisterElement("View", static () => new View());
        UIDocument.RegisterElement("Text", static () => new Controls.Text());
        UIDocument.RegisterElement("ListItem", static () => new ListItem());
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
