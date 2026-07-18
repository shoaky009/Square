namespace Square.UI.Properties;

using Square.UI;

internal static class PropertyInvalidation
{
    public static ElementInvalidation ForProperty(string name)
    {
        return name switch
        {
            "TextContent" or "Marker" or "ImageContent" or "Options" or "Items" or
                "Value" or "Placeholder" => ElementInvalidation.Layout,

            "Id" => ElementInvalidation.Style | ElementInvalidation.Layout | ElementInvalidation.HitTest,
            "IsChecked" or "IsDisabled" => ElementInvalidation.Style | ElementInvalidation.Paint,
            "SelectionBackground" or "SelectionForeground" => ElementInvalidation.Paint,
            _ => ElementInvalidation.Layout
        };
    }
}
