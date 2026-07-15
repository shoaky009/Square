using System.Numerics;
using Square.Graphics;

namespace Square.Backends;

internal sealed class RenderContext : IRenderContext
{
    private readonly Bitmap _bitmap;
    private readonly float _dpiScale;
    private readonly Stack<Rect> _clipStack = new();
    private readonly Stack<Matrix3x2> _transformStack = new();

    public Size CanvasSize => new(_bitmap.Width, _bitmap.Height);
    public float DpiScale => _dpiScale;

    internal RenderContext(Bitmap bitmap, float dpiScale)
    {
        _bitmap = bitmap;
        _dpiScale = dpiScale;
    }

    public void PushTransform(Matrix3x2 matrix) => _transformStack.Push(matrix);
    public void PopTransform() { if (_transformStack.Count > 0) _transformStack.Pop(); }

    public void PushClip(Rect rect) => _clipStack.Push(rect);
    public void PushClip(Geometry geometry) => _clipStack.Push(geometry is RectGeometry rg ? rg.Rect : Rect.Empty);
    public void PopClip() { if (_clipStack.Count > 0) _clipStack.Pop(); }

    public void Clear(Color color)
    {
        var span = _bitmap.Pixels.AsSpan();
        var pr = (byte)(color.R * color.A / 255);
        var pg = (byte)(color.G * color.A / 255);
        var pb = (byte)(color.B * color.A / 255);
        for (int i = 0; i < span.Length; i += 4)
        {
            span[i] = pb;
            span[i + 1] = pg;
            span[i + 2] = pr;
            span[i + 3] = color.A;
        }
    }

    public void FillRect(Rect rect, Brush brush)
    {
        if (brush is not SolidColorBrush sc) return;
        var clipped = ClipRect(rect);
        if (clipped.IsEmpty) return;
        BlendRect(clipped, sc.Color);
    }

    public void DrawRect(Rect rect, Pen pen)
    {
        if (pen.Width <= 0) return;
        var w = (int)Math.Ceiling(pen.Width);
        var color = (pen.Brush as SolidColorBrush)?.Color ?? Color.Black;

        BlendRect(new Rect(rect.X, rect.Y, rect.Width, w), color);
        BlendRect(new Rect(rect.X, rect.Bottom - w, rect.Width, w), color);
        BlendRect(new Rect(rect.X, rect.Y + w, w, rect.Height - w * 2), color);
        BlendRect(new Rect(rect.Right - w, rect.Y + w, w, rect.Height - w * 2), color);
    }

    public void FillGeometry(Geometry geometry, Brush brush)
    {
        if (brush is not SolidColorBrush sc) return;
        switch (geometry)
        {
            case RectGeometry rg:
                FillRect(rg.Rect, brush);
                break;
            case RoundedRectGeometry rrg:
                FillRoundedRect(rrg.Rect, rrg.RadiusX, rrg.RadiusY, sc.Color);
                break;
            case EllipseGeometry eg:
                FillEllipse(eg.Center, eg.RadiusX, eg.RadiusY, sc.Color);
                break;
        }
    }

    public void DrawGeometry(Geometry geometry, Pen pen)
    {
        switch (geometry)
        {
            case RectGeometry rg:
                DrawRect(rg.Rect, pen);
                break;
            case RoundedRectGeometry rrg:
                DrawRoundedRect(rrg.Rect, rrg.RadiusX, rrg.RadiusY, pen);
                break;
            case EllipseGeometry eg:
                DrawEllipse(eg.Center, eg.RadiusX, eg.RadiusY, pen);
                break;
        }
    }

    public void FillPath(PathGeometry path, Brush brush)
    {
        if (brush is not SolidColorBrush sc) return;
        var points = FlattenPath(path);
        if (points.Count < 3) return;
        FillPolygon(points, sc.Color);
    }

    public void DrawPath(PathGeometry path, Pen pen)
    {
        var color = (pen.Brush as SolidColorBrush)?.Color ?? Color.Black;
        var points = FlattenPath(path);
        if (points.Count < 2) return;
        DrawPolyline(points, pen.Width, color);
    }

    public void DrawText(TextLayout text, Point origin, Brush brush)
    {
        if (brush is not SolidColorBrush sc) return;
        if (string.IsNullOrEmpty(text.Text)) return;
        RenderText(text, origin, sc.Color);
    }

    public void DrawImage(Image image, Rect dest, Rect? source = null)
    {
        if (image is not Bitmap src) return;
        var srcRect = source ?? new Rect(0, 0, src.Width, src.Height);
        BlendBitmap(src, srcRect, dest);
    }

    public void PushLayer(Rect bounds, float opacity) { }
    public void PopLayer() { }

    public void Flush() { }
    public void Present() { }
    public void Dispose() { }

    internal Bitmap GetBitmap() => _bitmap;

    // ── 核心：像素混合 ──

    private void BlendRect(Rect rect, Color color)
    {
        var pr = (byte)(color.R * color.A / 255);
        var pg = (byte)(color.G * color.A / 255);
        var pb = (byte)(color.B * color.A / 255);
        var alpha = color.A;
        if (alpha == 0) return;

        var x0 = Math.Max(0, (int)Math.Round(rect.X));
        var y0 = Math.Max(0, (int)Math.Round(rect.Y));
        var x1 = Math.Min(_bitmap.Width, (int)Math.Round(rect.Right));
        var y1 = Math.Min(_bitmap.Height, (int)Math.Round(rect.Bottom));
        if (x0 >= x1 || y0 >= y1) return;

        for (int y = y0; y < y1; y++)
        {
            var span = _bitmap.GetRow(y);
            for (int x = x0; x < x1; x++)
            {
                var idx = x * 4;
                if (alpha == 255)
                {
                    span[idx] = pb;
                    span[idx + 1] = pg;
                    span[idx + 2] = pr;
                    span[idx + 3] = 255;
                }
                else
                {
                    var dstA = span[idx + 3];
                    var outA = (byte)(alpha + (dstA * (255 - alpha) / 255));
                    if (outA == 0) continue;
                    span[idx] = (byte)((pb * 255 + span[idx] * dstA * (255 - alpha) / 255) / outA);
                    span[idx + 1] = (byte)((pg * 255 + span[idx + 1] * dstA * (255 - alpha) / 255) / outA);
                    span[idx + 2] = (byte)((pr * 255 + span[idx + 2] * dstA * (255 - alpha) / 255) / outA);
                    span[idx + 3] = outA;
                }
            }
        }
    }

    private void BlendPixel(int x, int y, Color color)
    {
        if (x < 0 || x >= _bitmap.Width || y < 0 || y >= _bitmap.Height) return;
        var idx = y * _bitmap.Stride + x * 4;
        var span = _bitmap.Pixels.AsSpan(idx, 4);
        var alpha = color.A;
        if (alpha == 0) return;
        var pr = (byte)(color.R * alpha / 255);
        var pg = (byte)(color.G * alpha / 255);
        var pb = (byte)(color.B * alpha / 255);
        if (alpha == 255)
        {
            span[0] = pb; span[1] = pg; span[2] = pr; span[3] = 255;
        }
        else
        {
            var dstA = span[3];
            var outA = (byte)(alpha + (dstA * (255 - alpha) / 255));
            if (outA == 0) return;
            span[0] = (byte)((pb * 255 + span[0] * dstA * (255 - alpha) / 255) / outA);
            span[1] = (byte)((pg * 255 + span[1] * dstA * (255 - alpha) / 255) / outA);
            span[2] = (byte)((pr * 255 + span[2] * dstA * (255 - alpha) / 255) / outA);
            span[3] = outA;
        }
    }

    // ── 裁剪 ──

    private Rect ClipRect(Rect rect)
    {
        if (_clipStack.Count == 0) return rect;
        var clip = _clipStack.Peek();
        return Rect.Intersect(rect, clip);
    }

    // ── 圆角矩形 ──

    private void FillRoundedRect(Rect rect, float rx, float ry, Color color)
    {
        var cx = rect.X + rect.Width / 2f;
        var cy = rect.Y + rect.Height / 2f;
        rx = Math.Min(rx, rect.Width / 2f);
        ry = Math.Min(ry, rect.Height / 2f);

        // 中心矩形
        BlendRect(new Rect(rect.X + rx, rect.Y, rect.Width - rx * 2, rect.Height), color);
        // 左右矩形
        BlendRect(new Rect(rect.X, rect.Y + ry, rx, rect.Height - ry * 2), color);
        BlendRect(new Rect(rect.Right - rx, rect.Y + ry, rx, rect.Height - ry * 2), color);
        // 四角圆弧
        FillEllipse(new Point(rect.X + rx, rect.Y + ry), rx, ry, color);
        FillEllipse(new Point(rect.Right - rx, rect.Y + ry), rx, ry, color);
        FillEllipse(new Point(rect.X + rx, rect.Bottom - ry), rx, ry, color);
        FillEllipse(new Point(rect.Right - rx, rect.Bottom - ry), rx, ry, color);
    }

    private void DrawRoundedRect(Rect rect, float rx, float ry, Pen pen)
    {
        var color = (pen.Brush as SolidColorBrush)?.Color ?? Color.Black;
        var w = pen.Width;
        // 顶底
        BlendRect(new Rect(rect.X + rx, rect.Y, rect.Width - rx * 2, w), color);
        BlendRect(new Rect(rect.X + rx, rect.Bottom - w, rect.Width - rx * 2, w), color);
        // 左右
        BlendRect(new Rect(rect.X, rect.Y + ry, w, rect.Height - ry * 2), color);
        BlendRect(new Rect(rect.Right - w, rect.Y + ry, w, rect.Height - ry * 2), color);
    }

    // ── 椭圆 ──

    private void FillEllipse(Point center, float rx, float ry, Color color)
    {
        if (rx <= 0 || ry <= 0) return;
        var x0 = (int)Math.Floor(center.X - rx);
        var x1 = (int)Math.Ceiling(center.X + rx);
        var y0 = (int)Math.Floor(center.Y - ry);
        var y1 = (int)Math.Ceiling(center.Y + ry);
        var rx2 = rx * rx;
        var ry2 = ry * ry;

        for (int y = y0; y <= y1; y++)
        {
            var dy = y - center.Y;
            var dx2 = rx2 * (1 - dy * dy / ry2);
            if (dx2 < 0) continue;
            var dx = Math.Sqrt(dx2);
            for (int x = (int)(center.X - dx); x <= (int)(center.X + dx); x++)
                BlendPixel(x, y, color);
        }
    }

    private void DrawEllipse(Point center, float rx, float ry, Pen pen)
    {
        var color = (pen.Brush as SolidColorBrush)?.Color ?? Color.Black;
        if (rx <= 0 || ry <= 0) return;
        var steps = Math.Max(32, (int)(2 * Math.PI * Math.Max(rx, ry)));
        var prevX = (double)(center.X + rx);
        var prevY = (double)center.Y;
        for (int i = 1; i <= steps; i++)
        {
            var angle = 2 * Math.PI * i / steps;
            var x = center.X + rx * Math.Cos(angle);
            var y = center.Y + ry * Math.Sin(angle);
            DrawLine(prevX, prevY, x, y, pen.Width, color);
            prevX = x; prevY = y;
        }
    }

    // ── 线段 ──

    private void DrawLine(double x0, double y0, double x1, double y1, float width, Color color)
    {
        var w = Math.Max(1, (int)Math.Ceiling(width));
        var dx = x1 - x0;
        var dy = y1 - y0;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.01) return;
        var nx = -dy / len * w / 2;
        var ny = dx / len * w / 2;

        var points = new List<(double x, double y)>
        {
            (x0 + nx, y0 + ny),
            (x1 + nx, y1 + ny),
            (x1 - nx, y1 - ny),
            (x0 - nx, y0 - ny)
        };
        FillPolygonPoints(points, color);
    }

    private void DrawPolyline(List<(double x, double y)> points, float width, Color color)
    {
        for (int i = 0; i < points.Count - 1; i++)
            DrawLine(points[i].x, points[i].y, points[i + 1].x, points[i + 1].y, width, color);
    }

    // ── 多边形填充（扫描线） ──

    private void FillPolygon(List<(double x, double y)> points, Color color)
    {
        FillPolygonPoints(points, color);
    }

    private void FillPolygonPoints(List<(double x, double y)> points, Color color)
    {
        if (points.Count < 3) return;
        var minY = (int)Math.Floor(points.Min(p => p.y));
        var maxY = (int)Math.Ceiling(points.Max(p => p.y));
        minY = Math.Max(0, minY);
        maxY = Math.Min(_bitmap.Height - 1, maxY);

        for (int y = minY; y <= maxY; y++)
        {
            var yc = y + 0.5;
            var intersections = new List<double>();
            for (int i = 0; i < points.Count; i++)
            {
                var (x0, y0) = points[i];
                var (x1, y1) = points[(i + 1) % points.Count];
                if ((y0 <= yc && y1 > yc) || (y1 <= yc && y0 > yc))
                {
                    var t = (yc - y0) / (y1 - y0);
                    intersections.Add(x0 + t * (x1 - x0));
                }
            }
            intersections.Sort();
            for (int i = 0; i < intersections.Count - 1; i += 2)
            {
                var xa = (int)Math.Round(intersections[i]);
                var xb = (int)Math.Round(intersections[i + 1]);
                for (int x = xa; x <= xb; x++)
                    BlendPixel(x, y, color);
            }
        }
    }

    // ── 路径展开 ──

    private List<(double x, double y)> FlattenPath(PathGeometry path)
    {
        var points = new List<(double x, double y)>();
        double curX = 0, curY = 0;
        foreach (var cmd in path.Commands)
        {
            switch (cmd)
            {
                case MoveToCmd m:
                    curX = m.Point.X; curY = m.Point.Y;
                    points.Add((curX, curY));
                    break;
                case LineToCmd l:
                    curX = l.Point.X; curY = l.Point.Y;
                    points.Add((curX, curY));
                    break;
                case ArcToCmd a:
                    var steps = 16;
                    for (int i = 0; i <= steps; i++)
                    {
                        var t = (double)i / steps;
                        var angle = a.StartAngle + t * a.SweepAngle;
                        var x = a.Oval.X + a.Oval.Width / 2 + a.Oval.Width / 2 * Math.Cos(angle * Math.PI / 180);
                        var y = a.Oval.Y + a.Oval.Height / 2 + a.Oval.Height / 2 * Math.Sin(angle * Math.PI / 180);
                        points.Add((x, y));
                        curX = x; curY = y;
                    }
                    break;
                case CloseCmd:
                    if (points.Count > 0)
                        points.Add(points[0]);
                    break;
            }
        }
        return points;
    }

    // ── 位图混合 ──

    private void BlendBitmap(Bitmap src, Rect srcRect, Rect dest)
    {
        var dx0 = (int)Math.Round(dest.X);
        var dy0 = (int)Math.Round(dest.Y);
        var dw = (int)Math.Round(dest.Width);
        var dh = (int)Math.Round(dest.Height);
        var sx0 = (int)Math.Round(srcRect.X);
        var sy0 = (int)Math.Round(srcRect.Y);
        var sw = (int)Math.Round(srcRect.Width);
        var sh = (int)Math.Round(srcRect.Height);
        if (sw <= 0 || sh <= 0 || dw <= 0 || dh <= 0) return;

        for (int dy = 0; dy < dh; dy++)
        {
            var sy = sy0 + dy * sh / dh;
            if (sy < 0 || sy >= src.Height) continue;
            var dstY = dy0 + dy;
            if (dstY < 0 || dstY >= _bitmap.Height) continue;
            var dstSpan = _bitmap.GetRow(dstY);
            for (int dx = 0; dx < dw; dx++)
            {
                var sx = sx0 + dx * sw / dw;
                if (sx < 0 || sx >= src.Width) continue;
                var dstX = dx0 + dx;
                if (dstX < 0 || dstX >= _bitmap.Width) continue;
                var srcIdx = sy * src.Stride + sx * 4;
                var di = dstX * 4;
                var sa = src.Pixels[srcIdx + 3];
                if (sa == 0) continue;
                var sr = src.Pixels[srcIdx + 2];
                var sg = src.Pixels[srcIdx + 1];
                var sb = src.Pixels[srcIdx];
                if (sa == 255)
                {
                    dstSpan[di] = sb;
                    dstSpan[di + 1] = sg;
                    dstSpan[di + 2] = sr;
                    dstSpan[di + 3] = 255;
                }
                else
                {
                    var da = dstSpan[di + 3];
                    var outA = (byte)(sa + da * (255 - sa) / 255);
                    if (outA == 0) continue;
                    dstSpan[di] = (byte)((sb * 255 + dstSpan[di] * da * (255 - sa) / 255) / outA);
                    dstSpan[di + 1] = (byte)((sg * 255 + dstSpan[di + 1] * da * (255 - sa) / 255) / outA);
                    dstSpan[di + 2] = (byte)((sr * 255 + dstSpan[di + 2] * da * (255 - sa) / 255) / outA);
                    dstSpan[di + 3] = outA;
                }
            }
        }
    }

    // ── 文字光栅化（简易字形） ──

    private void RenderText(TextLayout textLayout, Point origin, Color color)
    {
        var text = textLayout.Text;
        var fontSize = textLayout.Font.Size;
        var lineHeight = fontSize * textLayout.LineHeight;
        var charWidth = fontSize * 0.6f;

        var x = (int)Math.Round(origin.X);
        var y = (int)Math.Round(origin.Y);

        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\n') { x = (int)Math.Round(origin.X); y += (int)Math.Round(lineHeight); continue; }
            if (c == ' ') { x += (int)Math.Round(charWidth); continue; }
            DrawGlyph(c, x, y, fontSize, color);
            x += (int)Math.Round(charWidth);
        }
    }

    private void DrawGlyph(char c, int x, int y, float size, Color color)
    {
        // 简易点阵字形：用字符码点生成 5x7 位图模式
        var pattern = GetGlyphPattern(c);
        var pixelSize = Math.Max(1, (int)Math.Round(size / 8));
        var offsetX = x;
        var offsetY = y + (int)Math.Round(size * 0.2);

        for (int row = 0; row < 7; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                if ((pattern[row] >> (4 - col) & 1) != 0)
                {
                    for (int py = 0; py < pixelSize; py++)
                        for (int px = 0; px < pixelSize; px++)
                            BlendPixel(offsetX + col * pixelSize + px, offsetY + row * pixelSize + py, color);
                }
            }
        }
    }

    // 5x7 点阵字形表（ASCII 常用字符）
    private static readonly Dictionary<char, byte[]> GlyphPatterns = new()
    {
        ['A'] = new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
        ['B'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E },
        ['C'] = new byte[] { 0x0E, 0x11, 0x01, 0x01, 0x01, 0x11, 0x0E },
        ['D'] = new byte[] { 0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E },
        ['E'] = new byte[] { 0x1F, 0x01, 0x01, 0x0E, 0x01, 0x01, 0x1F },
        ['F'] = new byte[] { 0x1F, 0x01, 0x01, 0x0E, 0x01, 0x01, 0x01 },
        ['G'] = new byte[] { 0x0E, 0x11, 0x01, 0x0D, 0x11, 0x11, 0x0E },
        ['H'] = new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
        ['I'] = new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E },
        ['J'] = new byte[] { 0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C },
        ['K'] = new byte[] { 0x11, 0x11, 0x09, 0x07, 0x09, 0x11, 0x11 },
        ['L'] = new byte[] { 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x1F },
        ['M'] = new byte[] { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 },
        ['N'] = new byte[] { 0x11, 0x11, 0x19, 0x15, 0x13, 0x11, 0x11 },
        ['O'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
        ['P'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x01, 0x01, 0x01 },
        ['Q'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x09, 0x16 },
        ['R'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x09, 0x11, 0x11 },
        ['S'] = new byte[] { 0x0E, 0x11, 0x01, 0x0E, 0x10, 0x11, 0x0E },
        ['T'] = new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 },
        ['U'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
        ['V'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 },
        ['W'] = new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11 },
        ['X'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 },
        ['Y'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 },
        ['Z'] = new byte[] { 0x1F, 0x10, 0x08, 0x04, 0x02, 0x01, 0x1F },
    };

    private static byte[] GetGlyphPattern(char c)
    {
        var uc = char.ToUpperInvariant(c);
        return GlyphPatterns.TryGetValue(uc, out var p) ? p : new byte[] { 0x1F, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1F };
    }
}