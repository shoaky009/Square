namespace Square.UI.ElementApi;

using Square.UI;

internal static class StyleInvalidation
{
    public static ElementInvalidation ForProperty(string property)
    {
        property = StyleAccessor.NormalizePropertyName(property);
        if (property.StartsWith("--", StringComparison.Ordinal))
            return ElementInvalidation.Paint;

        return property switch
        {
            "background" or "background-color" or "box-shadow" or "color" or "border-color" or "border-radius" or "caret-color" or
                "text-decoration" or "text-decoration-color" or "text-decoration-line" or "text-decoration-style" or
                "opacity" or "selection-background" or "selection-color" => ElementInvalidation.Paint,

            "z-index" or "visibility" or "overflow" or "overflow-x" or "overflow-y" or "user-select" or "cursor" =>
                ElementInvalidation.Paint | ElementInvalidation.DisplayTree | ElementInvalidation.HitTest,

            _ when IsLayoutProperty(property) => ElementInvalidation.Layout,
            _ => ElementInvalidation.Layout
        };
    }

    private static bool IsLayoutProperty(string property) => property is
        "display" or "width" or "height" or "min-width" or "min-height" or "max-width" or "max-height" or
        "margin" or "margin-left" or "margin-top" or "margin-right" or "margin-bottom" or
        "padding" or "padding-left" or "padding-top" or "padding-right" or "padding-bottom" or
        "font" or "font-size" or "font-family" or "font-weight" or "font-style" or "line-height" or
        "flex" or "flex-direction" or "flex-wrap" or "flex-grow" or "flex-shrink" or "flex-basis" or
        "justify-content" or "align-items" or "align-self" or
        "grid" or "grid-template-columns" or "grid-template-rows" or "grid-template-areas" or
        "grid-column" or "grid-row" or "grid-area" or "grid-column-span" or "grid-row-span" or
        "gap" or "row-gap" or "column-gap" or
        "position" or "left" or "top" or "right" or "bottom";
}
