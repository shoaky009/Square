using System.Numerics;
using System.Xml;
using Square.Graphics.Svg;
using Xunit;

namespace Square.Graphics.Tests;

public sealed class SvgImageTests
{
    [Fact]
    public void ParsesIntrinsicSizeAndDrawsShapes()
    {
        using var image = SvgImage.Parse("""
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="20" viewBox="0 0 40 20">
              <rect x="1" y="2" width="10" height="8" fill="#123456" />
              <circle cx="20" cy="10" r="4" fill="none" stroke="red" stroke-width="2" />
            </svg>
            """);
        using var context = new RecordingRenderContext();

        image.Draw(context, new Rect(0, 0, 80, 80));

        Assert.Equal(40, image.Width);
        Assert.Equal(20, image.Height);
        Assert.Equal(1, context.FillCount);
        Assert.Equal(1, context.StrokeCount);
        Assert.Equal(1, context.TransformDepthMaximum);
        Assert.Equal(new Rect(0, 0, 80, 80), context.LastClip);
    }

    [Fact]
    public void ParsesGroupsTransformsStylesAndCurves()
    {
        using var image = SvgImage.Parse("""
            <svg viewBox="0 0 100 100">
              <g transform="translate(5,10) scale(2)" style="fill:#00ff00;stroke:#0000ff;stroke-width:3">
                <path d="M 0 0 C 10 0 10 20 20 20 Q 30 20 40 0 Z" />
              </g>
            </svg>
            """);
        using var context = new RecordingRenderContext();

        image.Draw(context, new Rect(0, 0, 100, 100));

        Assert.Equal(1, context.FillCount);
        Assert.Equal(1, context.StrokeCount);
        Assert.Equal(2, context.TransformDepthMaximum);
        Assert.IsType<PathGeometry>(context.LastGeometry);
        Assert.True(((PathGeometry)context.LastGeometry!).Commands.Count > 20);
    }

    [Fact]
    public void RejectsDtdAndInvalidRoot()
    {
        Assert.Throws<XmlException>(() => SvgImage.Parse("<!DOCTYPE svg [<!ENTITY x 'x'>]><svg>&x;</svg>"));
        Assert.Throws<InvalidDataException>(() => SvgImage.Parse("<html />"));
    }

    private sealed class RecordingRenderContext : IRenderContext
    {
        private int _transformDepth;
        public Size CanvasSize => new(100, 100);
        public float DpiScale => 1;
        public int FillCount { get; private set; }
        public int StrokeCount { get; private set; }
        public int TransformDepthMaximum { get; private set; }
        public Rect LastClip { get; private set; }
        public Geometry? LastGeometry { get; private set; }

        public void PushTransform(Matrix3x2 matrix)
        {
            _transformDepth++;
            TransformDepthMaximum = Math.Max(TransformDepthMaximum, _transformDepth);
        }
        public void PopTransform() => _transformDepth--;
        public void PushClip(Rect rect) => LastClip = rect;
        public void PushClip(Geometry geometry) { }
        public void PopClip() { }
        public void FillRect(Rect rect, Brush brush) { }
        public void DrawRect(Rect rect, Pen pen) { }
        public void FillPath(PathGeometry path, Brush brush) { FillCount++; LastGeometry = path; }
        public void DrawPath(PathGeometry path, Pen pen) { StrokeCount++; LastGeometry = path; }
        public void FillGeometry(Geometry geometry, Brush brush) { FillCount++; LastGeometry = geometry; }
        public void DrawGeometry(Geometry geometry, Pen pen) { StrokeCount++; LastGeometry = geometry; }
        public void DrawText(TextLayout text, Point origin, Brush brush) { }
        public void DrawImage(Image image, Rect dest, Rect? source = null) { }
        public void PushLayer(Rect bounds, float opacity) { }
        public void PopLayer() { }
        public void Clear(Color color) { }
        public void Clear(Color color, Rect rect) { }
        public void Flush() { }
        public void Present() { }
        public void Present(IReadOnlyList<Rect>? dirtyRects) { }
        public void Dispose() { }
    }
}
