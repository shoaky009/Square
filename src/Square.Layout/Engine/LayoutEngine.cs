using Square.Graphics;
using Square.UI;

namespace Square.Layout.Engine;

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
}

public sealed class LayoutEngine
{
    public void Measure(Visual visual, Size availableSize)
    {
        var style = GetComputedStyle(visual);
        if (style.Display == DisplayMode.None) { visual.ClearLayoutDirty(); return; }

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
        foreach (var child in visual.Children)
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
        var count = visual.Children.Count;

        foreach (var child in visual.Children)
        {
            var cs = GetComputedStyle(child);
            var basis = !float.IsNaN(cs.FlexBasis) ? cs.FlexBasis : 0;
            totalBase += basis;
            totalFlex += cs.FlexGrow;
        }

        var remaining = mainAxis - totalBase - gap * Math.Max(0, count - 1);
        var unit = totalFlex > 0 ? remaining / totalFlex : 0;

        foreach (var child in visual.Children)
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
        if (style.Display == DisplayMode.None) return;

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
        foreach (var child in visual.Children)
        {
            var size = child.Measure(inner.Size);
            Arrange(child, new Rect(inner.Left, y, size.Width, size.Height));
            y += size.Height;
        }
    }

    private void ArrangeFlex(Visual visual, ComputedStyle style, Rect inner)
    {
        var isRow = style.FlexDirection is FlexDirection.Row or FlexDirection.RowReverse;
        var gap = style.Gap;
        var count = visual.Children.Count;

        var sizes = new float[count];
        var total = 0f;
        for (int i = 0; i < count; i++)
        {
            var s = visual.Children[i].Measure(inner.Size);
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
            var child = visual.Children[i];
            var size = child.Measure(inner.Size);
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
        var cols = ParseGridTemplate(style.GridTemplateColumns, available.Width);
        var rows = ParseGridTemplate(style.GridTemplateRows, available.Height);
        var gap = style.Gap;

        var colCount = cols.Length;
        var rowCount = rows.Length;
        if (colCount == 0) colCount = 1;
        if (rowCount == 0) rowCount = 1;

        var effectiveCols = new float[colCount];
        var effectiveRows = new float[rowCount];
        for (int i = 0; i < colCount; i++) effectiveCols[i] = cols.Length > i ? cols[i] : available.Width / colCount;
        for (int i = 0; i < rowCount; i++) effectiveRows[i] = rows.Length > i ? rows[i] : available.Height / rowCount;

        int childIdx = 0;
        foreach (var child in visual.Children)
        {
            var cs = GetComputedStyle(child);
            var col = Math.Min(cs.GridColumn - 1, colCount - 1);
            var row = Math.Min(cs.GridRow - 1, rowCount - 1);
            var colSpan = Math.Min(cs.GridColumnSpan, colCount - col);
            var rowSpan = Math.Min(cs.GridRowSpan, rowCount - row);

            var w = 0f; for (int i = 0; i < colSpan; i++) w += effectiveCols[col + i] + gap;
            var h = 0f; for (int i = 0; i < rowSpan; i++) h += effectiveRows[row + i] + gap;
            Measure(child, new Size(w, h));
            childIdx++;
        }
    }

    private void ArrangeGrid(Visual visual, ComputedStyle style, Rect inner)
    {
        var cols = ParseGridTemplate(style.GridTemplateColumns, inner.Width);
        var rows = ParseGridTemplate(style.GridTemplateRows, inner.Height);
        var gap = style.Gap;

        var colCount = Math.Max(1, cols.Length);
        var rowCount = Math.Max(1, rows.Length);

        var colX = new float[colCount + 1];
        colX[0] = inner.Left;
        for (int i = 0; i < colCount; i++) colX[i + 1] = colX[i] + (cols.Length > i ? cols[i] : inner.Width / colCount) + gap;

        var rowY = new float[rowCount + 1];
        rowY[0] = inner.Top;
        for (int i = 0; i < rowCount; i++) rowY[i + 1] = rowY[i] + (rows.Length > i ? rows[i] : inner.Height / rowCount) + gap;

        foreach (var child in visual.Children)
        {
            var cs = GetComputedStyle(child);
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

    private static float[] ParseGridTemplate(string template, float available)
    {
        if (string.IsNullOrEmpty(template)) return [];
        var parts = template.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i].Trim();
            if (p.EndsWith("fr"))
            {
                if (float.TryParse(p[..^2], out var fr))
                    result[i] = available * fr / parts.Length;
                else
                    result[i] = available / parts.Length;
            }
            else if (p.EndsWith("px"))
            {
                if (float.TryParse(p[..^2], out var px)) result[i] = px;
            }
            else if (p == "auto")
            {
                result[i] = available / parts.Length;
            }
            else if (float.TryParse(p, out var val))
            {
                result[i] = val;
            }
        }
        return result;
    }

    private static ComputedStyle GetComputedStyle(Visual visual)
    {
        var style = new ComputedStyle();
        var display = visual.Style.Get("display");
        if (display == "flex") style.Display = DisplayMode.Flex;
        if (display == "grid") style.Display = DisplayMode.Grid;
        if (display == "none") style.Display = DisplayMode.None;

        var gap = visual.Style.Get("gap");
        if (gap != null && float.TryParse(gap.TrimEnd('p', 'x'), out var gapVal)) style.Gap = gapVal;

        var gridCols = visual.Style.Get("grid-template-columns");
        if (gridCols != null) style.GridTemplateColumns = gridCols;
        var gridRows = visual.Style.Get("grid-template-rows");
        if (gridRows != null) style.GridTemplateRows = gridRows;

        var gridCol = visual.Style.Get("grid-column");
        if (gridCol != null && int.TryParse(gridCol, out var gc)) style.GridColumn = gc;
        var gridRow = visual.Style.Get("grid-row");
        if (gridRow != null && int.TryParse(gridRow, out var gr)) style.GridRow = gr;

        return style;
    }
}