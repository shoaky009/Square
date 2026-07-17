using Square.Graphics;
using Square.UI;

namespace Square.Rendering;

public enum DisplayMode { Block, Flex, Grid, None }
public enum FlexDirection { Row, Column, RowReverse, ColumnReverse }
public enum JustifyContent { FlexStart, Center, FlexEnd, SpaceBetween, SpaceAround }
public enum AlignItems { Stretch, FlexStart, Center, FlexEnd }

public sealed class ComputedStyle
{
    public DisplayMode Display { get; set; } = DisplayMode.Block;
    public FlexDirection FlexDirection { get; set; } = FlexDirection.Row;
    public JustifyContent JustifyContent { get; set; } = JustifyContent.FlexStart;
    public AlignItems AlignItems { get; set; } = AlignItems.Stretch;
    public float FlexGrow { get; set; }
    public float FlexShrink { get; set; } = 1f;
    public float FlexBasis { get; set; } = float.NaN;
    public float Gap { get; set; }
    public float Width { get; set; } = float.NaN;
    public float Height { get; set; } = float.NaN;
    public float Padding { get; set; }
    public float Margin { get; set; }
    public string GridTemplateColumns { get; set; } = "";
    public string GridTemplateRows { get; set; } = "";
    public int GridColumn { get; set; } = 1;
    public int GridRow { get; set; } = 1;
    public int GridColumnSpan { get; set; } = 1;
    public int GridRowSpan { get; set; } = 1;
    public string GridArea { get; set; } = "";
}

public sealed class LayoutEngine
{
    public void Measure(Visual visual, Size availableSize)
    {
        var style = GetComputedStyle(visual);
        if (!visual.IsVisible || style.Display == DisplayMode.None) { visual.ClearLayoutDirty(); return; }

        var inner = availableSize;
        inner = new Size(
            Math.Max(0, inner.Width - style.Padding * 2),
            Math.Max(0, inner.Height - style.Padding * 2));

        if (style.Display == DisplayMode.Flex)
            MeasureFlex(visual, style, inner);
        else if (style.Display == DisplayMode.Grid)
            MeasureGrid(visual, style, inner);
        else
            MeasureBlock(visual, style, inner);

        visual.ClearLayoutDirty();
    }

    private void MeasureBlock(Visual visual, ComputedStyle style, Size available)
    {
        foreach (var child in visual.Children.Where(child => child.IsVisible))
            Measure(child, available);
    }

    private void MeasureFlex(Visual visual, ComputedStyle style, Size available)
    {
        var isRow = style.FlexDirection is FlexDirection.Row or FlexDirection.RowReverse;
        var mainAxis = isRow ? available.Width : available.Height;
        var crossAxis = isRow ? available.Height : available.Width;
        var gap = style.Gap;

        var totalFlex = 0f;
        var totalBase = 0f;
        var children = visual.Children.Where(child => child.IsVisible).ToArray();
        var count = children.Length;

        foreach (var child in children)
        {
            var cs = GetComputedStyle(child);
            var basis = !float.IsNaN(cs.FlexBasis) ? cs.FlexBasis : 0;
            totalBase += basis;
            totalFlex += cs.FlexGrow;
        }

        var remaining = mainAxis - totalBase - gap * Math.Max(0, count - 1);
        var unit = totalFlex > 0 ? remaining / totalFlex : 0;

        foreach (var child in children)
        {
            var cs = GetComputedStyle(child);
            var size = !float.IsNaN(cs.FlexBasis) ? cs.FlexBasis : 0;
            size += cs.FlexGrow * unit;
            var childAvail = isRow ? new Size(size, crossAxis) : new Size(crossAxis, size);
            Measure(child, childAvail);
        }
    }

    public void Arrange(Visual visual, Rect finalRect)
    {
        var style = GetComputedStyle(visual);
        if (!visual.IsVisible || style.Display == DisplayMode.None) return;

        var inner = finalRect.Inflate(-style.Padding, -style.Padding);

        if (style.Display == DisplayMode.Flex)
            ArrangeFlex(visual, style, inner);
        else if (style.Display == DisplayMode.Grid)
            ArrangeGrid(visual, style, inner);
        else
            ArrangeBlock(visual, style, inner);

        visual.Arrange(finalRect);
    }

    private void ArrangeBlock(Visual visual, ComputedStyle style, Rect inner)
    {
        var y = inner.Top;
        foreach (var child in visual.Children.Where(child => child.IsVisible))
        {
            var size = MeasureWithStyle(child, inner.Size);
            Arrange(child, new Rect(inner.Left, y, size.Width, size.Height));
            y += size.Height;
        }
    }

    private void ArrangeFlex(Visual visual, ComputedStyle style, Rect inner)
    {
        var isRow = style.FlexDirection is FlexDirection.Row or FlexDirection.RowReverse;
        var gap = style.Gap;
        var children = visual.Children.Where(child => child.IsVisible).ToArray();
        var count = children.Length;

        var sizes = new float[count];
        var total = 0f;
        for (int i = 0; i < count; i++)
        {
            var s = MeasureWithStyle(children[i], inner.Size);
            sizes[i] = isRow ? s.Width : s.Height;
            total += sizes[i];
        }

        var mainSize = isRow ? inner.Width : inner.Height;
        var remaining = mainSize - total - gap * Math.Max(0, count - 1);

        var (start, step) = style.JustifyContent switch
        {
            JustifyContent.Center => (remaining / 2f, 0f),
            JustifyContent.FlexEnd => (remaining, 0f),
            JustifyContent.SpaceBetween => (0f, count > 1 ? remaining / (count - 1) : 0f),
            JustifyContent.SpaceAround => (count > 0 ? remaining / count / 2f : 0f, count > 0 ? remaining / count : 0f),
            _ => (0f, 0f)
        };

        var pos = start;
        for (int i = 0; i < count; i++)
        {
            var child = children[i];
            var size = MeasureWithStyle(child, inner.Size);
            float x, y, w, h;
            if (isRow)
            {
                x = inner.Left + pos;
                w = sizes[i];
                h = style.AlignItems switch
                {
                    AlignItems.Center => size.Height,
                    AlignItems.FlexEnd => size.Height,
                    _ => inner.Height
                };
                y = style.AlignItems switch
                {
                    AlignItems.Center => inner.Top + (inner.Height - size.Height) / 2f,
                    AlignItems.FlexEnd => inner.Bottom - size.Height,
                    _ => inner.Top
                };
            }
            else
            {
                y = inner.Top + pos;
                h = sizes[i];
                w = style.AlignItems switch
                {
                    AlignItems.Center => size.Width,
                    AlignItems.FlexEnd => size.Width,
                    _ => inner.Width
                };
                x = style.AlignItems switch
                {
                    AlignItems.Center => inner.Left + (inner.Width - size.Width) / 2f,
                    AlignItems.FlexEnd => inner.Right - size.Width,
                    _ => inner.Left
                };
            }
            Arrange(child, new Rect(x, y, w, h));
            pos += sizes[i] + gap + step;
        }
    }

    private void MeasureGrid(Visual visual, ComputedStyle style, Size available)
    {
        var gap = style.Gap;
        var cols = ParseGridTemplate(style.GridTemplateColumns, available.Width, gap);
        var rows = ParseGridTemplate(style.GridTemplateRows, available.Height, gap);
        ApplyIntrinsicGridTracks(visual, style.GridTemplateColumns, cols, isColumns: true);
        ApplyIntrinsicGridTracks(visual, style.GridTemplateRows, rows, isColumns: false);
        RecomputeFlexibleGridTracks(style.GridTemplateColumns, cols, available.Width, gap);
        RecomputeFlexibleGridTracks(style.GridTemplateRows, rows, available.Height, gap);

        var colCount = cols.Length;
        var rowCount = rows.Length;
        if (colCount == 0) colCount = 1;
        if (rowCount == 0) rowCount = 1;

        var effectiveCols = new float[colCount];
        var effectiveRows = new float[rowCount];
        for (int i = 0; i < colCount; i++) effectiveCols[i] = cols.Length > i ? cols[i] : Math.Max(0, available.Width - gap * Math.Max(0, colCount - 1)) / colCount;
        for (int i = 0; i < rowCount; i++) effectiveRows[i] = rows.Length > i ? rows[i] : Math.Max(0, available.Height - gap * Math.Max(0, rowCount - 1)) / rowCount;

        var visibleChildren = visual.Children.Where(child => child.IsVisible).ToArray();
        var areas = ParseGridAreas(visual.Style.Get("grid-template-areas"));
        for (var childIndex = 0; childIndex < visibleChildren.Length; childIndex++)
        {
            var child = visibleChildren[childIndex];
            var cs = GetComputedStyle(child);
            ApplyAutoOrAreaPlacement(child, childIndex, colCount, cs, areas);
            var col = Math.Min(Math.Max(0, cs.GridColumn - 1), colCount - 1);
            var row = Math.Min(Math.Max(0, cs.GridRow - 1), rowCount - 1);
            var colSpan = Math.Min(cs.GridColumnSpan, colCount - col);
            var rowSpan = Math.Min(cs.GridRowSpan, rowCount - row);

            var w = 0f; for (int i = 0; i < colSpan; i++) w += effectiveCols[col + i];
            w += gap * Math.Max(0, colSpan - 1);
            var h = 0f; for (int i = 0; i < rowSpan; i++) h += effectiveRows[row + i];
            h += gap * Math.Max(0, rowSpan - 1);
            Measure(child, new Size(w, h));
        }
    }

    private void ArrangeGrid(Visual visual, ComputedStyle style, Rect inner)
    {
        var gap = style.Gap;
        var cols = ParseGridTemplate(style.GridTemplateColumns, inner.Width, gap);
        var rows = ParseGridTemplate(style.GridTemplateRows, inner.Height, gap);
        ApplyIntrinsicGridTracks(visual, style.GridTemplateColumns, cols, isColumns: true);
        ApplyIntrinsicGridTracks(visual, style.GridTemplateRows, rows, isColumns: false);
        RecomputeFlexibleGridTracks(style.GridTemplateColumns, cols, inner.Width, gap);
        RecomputeFlexibleGridTracks(style.GridTemplateRows, rows, inner.Height, gap);

        var colCount = Math.Max(1, cols.Length);
        var rowCount = Math.Max(1, rows.Length);

        var colX = new float[colCount + 1];
        colX[0] = inner.Left;
        for (int i = 0; i < colCount; i++) colX[i + 1] = colX[i] + (cols.Length > i ? cols[i] : Math.Max(0, inner.Width - gap * Math.Max(0, colCount - 1)) / colCount) + gap;

        var rowY = new float[rowCount + 1];
        rowY[0] = inner.Top;
        for (int i = 0; i < rowCount; i++) rowY[i + 1] = rowY[i] + (rows.Length > i ? rows[i] : Math.Max(0, inner.Height - gap * Math.Max(0, rowCount - 1)) / rowCount) + gap;

        var visibleChildren = visual.Children.Where(child => child.IsVisible).ToArray();
        var areas = ParseGridAreas(visual.Style.Get("grid-template-areas"));
        for (var childIndex = 0; childIndex < visibleChildren.Length; childIndex++)
        {
            var child = visibleChildren[childIndex];
            var cs = GetComputedStyle(child);
            ApplyAutoOrAreaPlacement(child, childIndex, colCount, cs, areas);
            var col = Math.Min(Math.Max(0, cs.GridColumn - 1), colCount - 1);
            var row = Math.Min(Math.Max(0, cs.GridRow - 1), rowCount - 1);
            var colEnd = Math.Min(col + cs.GridColumnSpan, colCount);
            var rowEnd = Math.Min(row + cs.GridRowSpan, rowCount);

            var x = colX[col];
            var y = rowY[row];
            var w = colX[colEnd] - colX[col] - gap;
            var h = rowY[rowEnd] - rowY[row] - gap;
            Arrange(child, new Rect(x, y, w, h));
        }
    }

    private static float[] ParseGridTemplate(string template, float available, float gap)
    {
        if (string.IsNullOrEmpty(template)) return [];
        var parts = SplitGridTemplate(template);
        var result = new float[parts.Length];
        var fixedSize = 0f;
        var frTotal = 0f;
        var autoCount = 0;

        foreach (var raw in parts)
        {
            var p = raw.Trim();
            if (TryParseMinMax(p, out var min, out var frMax))
            {
                fixedSize += min;
                frTotal += frMax;
            }
            else if (p.EndsWith("fr"))
                frTotal += float.TryParse(p[..^2], out var fr) ? fr : 1f;
            else if (p.EndsWith("px"))
            {
                if (float.TryParse(p[..^2], out var px)) fixedSize += px;
            }
            else if (p == "auto")
                autoCount++;
            else if (float.TryParse(p, out var val))
                fixedSize += val;
        }

        var availableForTracks = Math.Max(0, available - gap * Math.Max(0, parts.Length - 1));
        var flexibleSpace = Math.Max(0, availableForTracks - fixedSize);
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i].Trim();
            if (TryParseMinMax(p, out var min, out var frMax))
            {
                result[i] = min + (frTotal > 0 ? flexibleSpace * frMax / frTotal : 0);
            }
            else if (p.EndsWith("fr"))
            {
                var fr = float.TryParse(p[..^2], out var parsed) ? parsed : 1f;
                result[i] = frTotal > 0 ? flexibleSpace * fr / frTotal : 0;
            }
            else if (p.EndsWith("px"))
            {
                if (float.TryParse(p[..^2], out var px)) result[i] = px;
            }
            else if (p == "auto")
            {
                result[i] = autoCount > 0 ? flexibleSpace / autoCount : 0;
            }
            else if (float.TryParse(p, out var val))
            {
                result[i] = val;
            }
        }
        return result;
    }

    private static string[] SplitGridTemplate(string template)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        for (var i = 0; i < template.Length; i++)
        {
            var ch = template[i];
            if (ch == '(') depth++;
            else if (ch == ')') depth = Math.Max(0, depth - 1);
            else if (char.IsWhiteSpace(ch) && depth == 0)
            {
                if (i > start) parts.Add(template[start..i]);
                start = i + 1;
            }
        }
        if (start < template.Length) parts.Add(template[start..]);
        return parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
    }

    private static bool TryParseMinMax(string value, out float min, out float frMax)
    {
        min = 0;
        frMax = 0;
        if (!value.StartsWith("minmax(", StringComparison.OrdinalIgnoreCase) || !value.EndsWith(')')) return false;
        var inner = value[7..^1];
        var comma = inner.IndexOf(',');
        if (comma < 0) return false;
        var minPart = inner[..comma].Trim();
        var maxPart = inner[(comma + 1)..].Trim();
        if (minPart.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            float.TryParse(minPart[..^2], out min);
        else
            float.TryParse(minPart, out min);
        if (maxPart.EndsWith("fr", StringComparison.OrdinalIgnoreCase))
            frMax = float.TryParse(maxPart[..^2], out var fr) ? fr : 1f;
        return true;
    }

    private static Dictionary<string, (int col, int row, int colSpan, int rowSpan)> ParseGridAreas(string? value)
    {
        var result = new Dictionary<string, (int col, int row, int colSpan, int rowSpan)>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(value)) return result;
        var rows = value.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var row = 0; row < rows.Length; row++)
        {
            var cells = rows[row].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var col = 0; col < cells.Length; col++)
            {
                var name = cells[col];
                if (name == ".") continue;
                if (!result.TryGetValue(name, out var area))
                    result[name] = (col + 1, row + 1, 1, 1);
                else
                    result[name] = (Math.Min(area.col, col + 1), Math.Min(area.row, row + 1), Math.Max(area.colSpan, col - area.col + 2), Math.Max(area.rowSpan, row - area.row + 2));
            }
        }
        return result;
    }

    private static void ApplyAutoOrAreaPlacement(Visual child, int childIndex, int colCount, ComputedStyle style, Dictionary<string, (int col, int row, int colSpan, int rowSpan)> areas)
    {
        if (!string.IsNullOrWhiteSpace(style.GridArea) && areas.TryGetValue(style.GridArea, out var area))
        {
            style.GridColumn = area.col;
            style.GridRow = area.row;
            style.GridColumnSpan = area.colSpan;
            style.GridRowSpan = area.rowSpan;
            return;
        }

        if (child.Style.Get("grid-column") != null || child.Style.Get("grid-row") != null) return;
        style.GridColumn = childIndex % Math.Max(1, colCount) + 1;
        style.GridRow = childIndex / Math.Max(1, colCount) + 1;
    }

    private static void ApplyIntrinsicGridTracks(Visual visual, string template, float[] tracks, bool isColumns)
    {
        if (tracks.Length == 0 || !template.Contains("content", StringComparison.OrdinalIgnoreCase)) return;
        var parts = SplitGridTemplate(template);
        for (var i = 0; i < Math.Min(parts.Length, tracks.Length); i++)
        {
            var keyword = parts[i].Trim();
            if (keyword is not ("min-content" or "max-content" or "fit-content")) continue;
            var trackIndex = i + 1;
            var size = 0f;
            foreach (var child in visual.Children.Where(child => child.IsVisible))
            {
                var childStyle = GetComputedStyle(child);
                var childTrack = isColumns ? childStyle.GridColumn : childStyle.GridRow;
                if (childTrack != trackIndex) continue;
                var measured = child.Measure(Size.Zero);
                size = Math.Max(size, isColumns ? measured.Width : measured.Height);
            }
            tracks[i] = size;
        }
    }

    private static void RecomputeFlexibleGridTracks(string template, float[] tracks, float available, float gap)
    {
        if (tracks.Length == 0) return;
        var parts = SplitGridTemplate(template);
        var frTotal = 0f;
        var fixedSize = 0f;
        for (var i = 0; i < Math.Min(parts.Length, tracks.Length); i++)
        {
            var part = parts[i].Trim();
            if (TryParseMinMax(part, out var minMaxMin, out var minMaxFr))
            {
                fixedSize += minMaxMin;
                frTotal += minMaxFr;
            }
            else if (part.EndsWith("fr"))
                frTotal += float.TryParse(part[..^2], out var fr) ? fr : 1f;
            else
                fixedSize += tracks[i];
        }
        if (frTotal <= 0) return;

        var flexibleSpace = Math.Max(0, available - gap * Math.Max(0, tracks.Length - 1) - fixedSize);
        for (var i = 0; i < Math.Min(parts.Length, tracks.Length); i++)
        {
            var part = parts[i].Trim();
            if (TryParseMinMax(part, out var min, out var minMaxFr))
            {
                tracks[i] = min + flexibleSpace * minMaxFr / frTotal;
                continue;
            }
            if (!part.EndsWith("fr")) continue;
            var fr = float.TryParse(part[..^2], out var parsed) ? parsed : 1f;
            tracks[i] = flexibleSpace * fr / frTotal;
        }
    }

    private static ComputedStyle GetComputedStyle(Visual visual) =>
        GetComputedStyle(visual, float.NaN, float.NaN);

    private static ComputedStyle GetComputedStyle(Visual visual, float parentWidth, float parentHeight)
    {
        var style = new ComputedStyle();
        var display = visual.Style.Get("display");
        if (display == "flex") style.Display = DisplayMode.Flex;
        if (display == "grid") style.Display = DisplayMode.Grid;
        if (display == "none") style.Display = DisplayMode.None;

        style.FlexDirection = visual.Style.Get("flex-direction")?.Trim() switch
        {
            "column" => FlexDirection.Column,
            "row-reverse" => FlexDirection.RowReverse,
            "column-reverse" => FlexDirection.ColumnReverse,
            _ => FlexDirection.Row
        };

        style.JustifyContent = visual.Style.Get("justify-content")?.Trim() switch
        {
            "center" => JustifyContent.Center,
            "flex-end" => JustifyContent.FlexEnd,
            "space-between" => JustifyContent.SpaceBetween,
            "space-around" => JustifyContent.SpaceAround,
            _ => JustifyContent.FlexStart
        };

        style.AlignItems = visual.Style.Get("align-items")?.Trim() switch
        {
            "center" => AlignItems.Center,
            "flex-start" => AlignItems.FlexStart,
            "flex-end" => AlignItems.FlexEnd,
            _ => AlignItems.Stretch
        };

        var gap = visual.Style.Get("gap");
        if (gap != null && float.TryParse(gap.TrimEnd('p', 'x'), out var gapVal)) style.Gap = gapVal;

        var emSize = GetFontSize(visual);
        var remSize = GetRootFontSize(visual);

        var padding = visual.Style.Get("padding");
        if (padding != null && TryParseLength(padding, parentWidth, parentHeight, emSize, remSize, out var paddingVal)) style.Padding = paddingVal;

        var margin = visual.Style.Get("margin");
        if (margin != null && TryParseLength(margin, parentWidth, parentHeight, emSize, remSize, out var marginVal)) style.Margin = marginVal;

        var width = visual.Style.Get("width");
        if (width != null && TryParseLength(width, parentWidth, parentHeight, emSize, remSize, out var widthVal)) style.Width = widthVal;

        var height = visual.Style.Get("height");
        if (height != null && TryParseLength(height, parentWidth, parentHeight, emSize, remSize, out var heightVal)) style.Height = heightVal;

        var flexGrow = visual.Style.Get("flex-grow");
        if (flexGrow != null && float.TryParse(flexGrow, out var grow)) style.FlexGrow = grow;

        var flexShrink = visual.Style.Get("flex-shrink");
        if (flexShrink != null && float.TryParse(flexShrink, out var shrink)) style.FlexShrink = shrink;

        var flexBasis = visual.Style.Get("flex-basis");
        if (flexBasis != null && TryParseLength(flexBasis, parentWidth, parentHeight, emSize, remSize, out var basis)) style.FlexBasis = basis;

        var gridCols = visual.Style.Get("grid-template-columns");
        if (gridCols != null) style.GridTemplateColumns = gridCols;
        var gridRows = visual.Style.Get("grid-template-rows");
        if (gridRows != null) style.GridTemplateRows = gridRows;

        var gridCol = visual.Style.Get("grid-column");
        if (gridCol != null) ApplyGridPlacement(gridCol, value => style.GridColumn = value, value => style.GridColumnSpan = value);
        var gridRow = visual.Style.Get("grid-row");
        if (gridRow != null) ApplyGridPlacement(gridRow, value => style.GridRow = value, value => style.GridRowSpan = value);
        var gridArea = visual.Style.Get("grid-area");
        if (gridArea != null) style.GridArea = gridArea;

        return style;
    }

    private static void ApplyGridPlacement(string value, Action<int> setStart, Action<int> setSpan)
    {
        var parts = value.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && int.TryParse(parts[0], out var start)) setStart(start);
        if (parts.Length <= 1) return;

        var spanPart = parts[1];
        if (spanPart.StartsWith("span ", StringComparison.OrdinalIgnoreCase))
            spanPart = spanPart[5..].Trim();
        if (int.TryParse(spanPart, out var span)) setSpan(Math.Max(1, span));
    }

    private static float GetFontSize(Visual visual)
    {
        var value = visual.Style.Get("font-size");
        if (value != null && TryParseLength(value, float.NaN, float.NaN, 16f, 16f, out var parsed) && !float.IsNaN(parsed))
            return parsed;
        return visual.Parent != null ? GetFontSize(visual.Parent) : 16f;
    }

    private static float GetRootFontSize(Visual visual)
    {
        var root = visual;
        while (root.Parent != null) root = root.Parent;
        return GetFontSize(root);
    }

    private static bool TryParseLength(string value, float parentSize, float viewportSize, float emSize, float remSize, out float result)
    {
        result = 0;
        if (string.IsNullOrEmpty(value)) return false;
        var text = value.Replace(" ", "").Trim();
        if (text.EndsWith("rem", StringComparison.OrdinalIgnoreCase))
        {
            if (!float.TryParse(text[..^3], out var rem)) return false;
            result = rem * remSize;
            return true;
        }
        if (text.EndsWith("em", StringComparison.OrdinalIgnoreCase))
        {
            if (!float.TryParse(text[..^2], out var em)) return false;
            result = em * emSize;
            return true;
        }
        if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            return float.TryParse(text[..^2], out result);
        if (text.EndsWith('%') && !float.IsNaN(parentSize))
        {
            if (!float.TryParse(text[..^1], out var percent)) return false;
            result = parentSize * percent / 100f;
            return true;
        }
        if (text.EndsWith("vw", StringComparison.OrdinalIgnoreCase) && !float.IsNaN(viewportSize))
        {
            if (!float.TryParse(text[..^2], out var vw)) return false;
            result = viewportSize * vw / 100f;
            return true;
        }
        if (text.EndsWith("vh", StringComparison.OrdinalIgnoreCase) && !float.IsNaN(viewportSize))
        {
            if (!float.TryParse(text[..^2], out var vh)) return false;
            result = viewportSize * vh / 100f;
            return true;
        }
        if (text.EndsWith("rp", StringComparison.OrdinalIgnoreCase) && !float.IsNaN(parentSize))
        {
            if (!float.TryParse(text[..^2], out var rp)) return false;
            result = parentSize * rp / 100f;
            return true;
        }
        if (text is "auto" or "min-content" or "max-content" or "fit-content")
        {
            result = float.NaN;
            return true;
        }
        return float.TryParse(text, out result);
    }

    private static Size MeasureWithStyle(Visual visual, Size available)
    {
        var measured = visual.Measure(available);
        var style = GetComputedStyle(visual);
        return new Size(
            float.IsNaN(style.Width) ? measured.Width : style.Width,
            float.IsNaN(style.Height) ? measured.Height : style.Height);
    }
}
