using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.Vulkan;
using Square.Graphics;
using Square.Text.Glyph;
using Image = Square.Graphics.Image;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Square.Backends.Vulkan;

/// <summary>
/// Vulkan GPU implementation of IRenderContext using ImGui-style batched 2D rendering.
/// All draw calls are triangulated on CPU, batched by texture/scissor, and submitted in one draw.
/// </summary>
internal sealed unsafe class VulkanRenderContext : IRenderContext, IDpiResizableRenderContext, IRenderBitmapSource
{
    private readonly VulkanDevice _device;
    private readonly VulkanSwapchain _swapchain;
    private readonly VulkanPipeline _pipeline;
    private readonly VulkanBatchRenderer _batchRenderer;
    private readonly VulkanTextureAtlas _atlas;
    private readonly VulkanReadbackBuffer _readback;
    private readonly bool _readbackEnabled;
    private readonly SystemGlyphRasterizer _glyphRasterizer = new(cacheGlyphs: false);
    private readonly Dictionary<GlyphCacheKey, CachedGlyph> _glyphCache = [];
    private readonly ConditionalWeakTable<Bitmap, CachedImage> _imageCache = new();
    private readonly List<Vertex2D> _scratchVertices = new(512);
    private readonly List<uint> _scratchIndices = new(768);

    private CommandBuffer _currentCmd;
    private bool _frameStarted;
    private bool _disposed;
    private Color _clearColor;

    // True while the window is minimized/collapsed to a degenerate (0x0) size.
    // Vulkan forbids 0-extent swapchains and 0-size buffers, so rendering is paused
    // until a valid size arrives instead of crashing in vkAllocateMemory.
    private bool _minimized;

    // Transform stack
    private readonly Stack<Matrix3x2> _transformStack = new();
    private Matrix3x2 _currentTransform;

    // Clip stack
    private readonly Stack<Rect> _clipStack = new();
    private Rect _currentClip;

    // Layer (opacity) stack
    private readonly Stack<float> _opacityStack = new();
    private float _currentOpacity = 1f;

    public Size CanvasSize { get; private set; }
    public float DpiScale { get; private set; }

    /// <summary>
    /// Enables the Vulkan validation layer + debug messenger (messages go to the console).
    /// Opt in by setting SQUARE_VULKAN_VALIDATION=1; requires the Vulkan SDK runtime layers.
    /// </summary>
    private static bool EnableValidation =>
        Environment.GetEnvironmentVariable("SQUARE_VULKAN_VALIDATION") is "1" or "true";

    internal VulkanRenderContext(RenderContextCreateInfo info)
    {
        CanvasSize = info.CanvasSize;
        DpiScale = NormalizeDpi(info.DpiScale);
        _currentTransform = Matrix3x2.CreateScale(DpiScale);

        var physicalW = ToPhysical(CanvasSize.Width, DpiScale);
        var physicalH = ToPhysical(CanvasSize.Height, DpiScale);
        _readbackEnabled = Environment.GetEnvironmentVariable("SQUARE_VULKAN_READBACK") is "1" or "true";

        _device = new VulkanDevice(info.NativeTarget
            ?? throw new VulkanException("Vulkan backend requires a NativeTarget."),
            enableValidation: EnableValidation);
        _device.ConfigureColorSampleCount(physicalW, physicalH);
        _swapchain = new VulkanSwapchain(_device, _device.Surface, physicalW, physicalH, info.VSync, _readbackEnabled);
        _pipeline = new VulkanPipeline(_device, _swapchain);
        _batchRenderer = new VulkanBatchRenderer(_device, _pipeline);
        _atlas = new VulkanTextureAtlas(_device, _pipeline);
        _readback = new VulkanReadbackBuffer(_device);
        _minimized = _swapchain.Extent.Width < 1 || _swapchain.Extent.Height < 1;
        if (_readbackEnabled)
            _readback.EnsureSize(_swapchain.Extent.Width, _swapchain.Extent.Height);

        if (!_minimized)
            _pipeline.UpdateProjection(_swapchain.Extent.Width, _swapchain.Extent.Height);
        _currentClip = new Rect(0, 0, _swapchain.Extent.Width, _swapchain.Extent.Height);
    }

    // ─── Frame lifecycle ──────────────────────────────────────────────────

    private bool EnsureFrame()
    {
        if (_frameStarted) return true;
        if (_minimized) return false;
        if (!_swapchain.AcquireNextImage())
        {
            // Surface is unavailable (window minimized / swapchain out-of-date). Pause
            // rendering; Resize() clears _minimized once a valid size arrives.
            _minimized = true;
            return false;
        }
        _batchRenderer.BeginFrame();
        _frameStarted = true;
        _clearColor = new Color(0, 0, 0, 255);
        return true;
    }

    public void Clear(Color color)
    {
        if (!EnsureFrame()) return;
        _clearColor = color;
    }

    public void Clear(Color color, Rect rect)
    {
        if (!EnsureFrame()) return;
        FillRectColor(rect, color);
    }

    public void Flush()
    {
        if (!EnsureFrame()) return;
        // Flush is a no-op; actual submission happens in Present
    }

    public void Present() => Present(null);

    public void Present(IReadOnlyList<Rect>? dirtyRects)
    {
        if (_minimized) return;
        if (dirtyRects is { Count: 0 } && !_frameStarted) return;
        if (!EnsureFrame()) return;
        SubmitFrame();
        _frameStarted = false;
    }

    private void SubmitFrame()
    {
        // Draw calls can add glyphs to the atlas, so upload after the frame has been built.
        _atlas.Flush();

        var api = _device.Api;
        var cmd = AllocateCommandBuffer();
        _currentCmd = cmd;

        var beginInfo = new CommandBufferBeginInfo(StructureType.CommandBufferBeginInfo)
        {
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };
        VulkanDevice.ThrowIfFailed(api.BeginCommandBuffer(cmd, in beginInfo), "vkBeginCommandBuffer");

        // Begin render pass
        var clearValue = new ClearValue(new ClearColorValue(
            _clearColor.R / 255f, _clearColor.G / 255f, _clearColor.B / 255f, _clearColor.A / 255f));

        var rpBegin = new RenderPassBeginInfo(StructureType.RenderPassBeginInfo)
        {
            RenderPass = _swapchain.RenderPass,
            Framebuffer = _swapchain.CurrentFramebuffer,
            RenderArea = new Rect2D(new Offset2D(0, 0), _swapchain.Extent),
            ClearValueCount = 1,
            PClearValues = &clearValue
        };
        api.CmdBeginRenderPass(cmd, in rpBegin, SubpassContents.Inline);

        // Set dynamic viewport
        var viewport = new Viewport(0, 0, _swapchain.Extent.Width, _swapchain.Extent.Height, 0, 1);
        api.CmdSetViewport(cmd, 0, 1, in viewport);

        // Render batched geometry
        _batchRenderer.Render(cmd, _atlas, _swapchain.Extent);

        api.CmdEndRenderPass(cmd);

        // Copy the presented frame into the host-visible readback buffer so tooling can
        // capture GPU-accurate screenshots. The image is in PresentSrcKhr layout here
        // (render pass FinalLayout); RecordCopy transitions it and restores it for present.
        if (_readbackEnabled)
            _readback.RecordCopy(cmd, _swapchain.CurrentImage);

        VulkanDevice.ThrowIfFailed(api.EndCommandBuffer(cmd), "vkEndCommandBuffer");

        // Submit
        var waitSemaphore = _swapchain.CurrentImageAvailableSemaphore;
        var signalSemaphore = _swapchain.CurrentRenderFinishedSemaphore;
        var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;

        var submitInfo = new SubmitInfo(StructureType.SubmitInfo)
        {
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &waitSemaphore,
            PWaitDstStageMask = &waitStage,
            CommandBufferCount = 1,
            PCommandBuffers = &cmd,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &signalSemaphore
        };

        var fence = _swapchain.CurrentInFlightFence;
        // The fence is reset here (not in AcquireNextImage) so that a skipped frame never
        // leaves it reset-but-unsignalled, which would deadlock the next WaitForFences.
        VulkanDevice.ThrowIfFailed(api.ResetFences(_device.Device, 1, in fence), "vkResetFences");
        VulkanDevice.ThrowIfFailed(api.QueueSubmit(_device.GraphicsQueue, 1, in submitInfo, fence), "vkQueueSubmit");

        // Present
        _swapchain.Present();

        // Present waits for the render-finished semaphore and then idles the present queue,
        // so the submitted command buffer is no longer pending here.
        api.FreeCommandBuffers(_device.Device, _device.CommandPool, 1, in cmd);
    }

    private CommandBuffer AllocateCommandBuffer()
    {
        var allocInfo = new CommandBufferAllocateInfo(StructureType.CommandBufferAllocateInfo)
        {
            CommandPool = _device.CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };
        VulkanDevice.ThrowIfFailed(_device.Api.AllocateCommandBuffers(_device.Device, in allocInfo, out var cmd), "vkAllocateCommandBuffers");
        return cmd;
    }

    // ─── Transform ────────────────────────────────────────────────────────

    public void PushTransform(Matrix3x2 matrix)
    {
        if (!EnsureFrame()) return;
        _transformStack.Push(_currentTransform);
        _currentTransform = matrix * _currentTransform;
    }

    public void PopTransform()
    {
        if (!EnsureFrame()) return;
        if (_transformStack.Count > 0)
            _currentTransform = _transformStack.Pop();
    }

    // ─── Clip ─────────────────────────────────────────────────────────────

    public void PushClip(Rect rect)
    {
        if (!EnsureFrame()) return;
        _clipStack.Push(_currentClip);
        _currentClip = IntersectRects(_currentClip, TransformRect(rect));
    }

    public void PushClip(Geometry geometry)
    {
        if (!EnsureFrame()) return;
        _clipStack.Push(_currentClip);
        var bounds = geometry switch
        {
            RectGeometry r => r.Rect,
            RoundedRectGeometry rr => rr.Rect,
            EllipseGeometry e => new Rect(e.Center.X - e.RadiusX, e.Center.Y - e.RadiusY, e.RadiusX * 2, e.RadiusY * 2),
            PathGeometry p => GetPathBounds(p),
            _ => _currentClip
        };
        _currentClip = IntersectRects(_currentClip, TransformRect(bounds));
    }

    public void PopClip()
    {
        if (!EnsureFrame()) return;
        if (_clipStack.Count > 0)
            _currentClip = _clipStack.Pop();
    }

    // ─── Drawing operations ───────────────────────────────────────────────

    public void FillRect(Rect rect, Brush brush)
    {
        if (!EnsureFrame()) return;
        if (rect.IsEmpty) return;
        var color = ResolveBrushColor(brush, rect.Center);
        FillRectColor(rect, color);
    }

    private void FillRectColor(Rect rect, Color color)
    {
        if (rect.IsEmpty) return;
        var (u0, v0, u1, v1) = VulkanTextureAtlas.WhitePixelUV;

        var tl = TransformPoint(new Point(rect.X, rect.Y));
        var tr = TransformPoint(new Point(rect.Right, rect.Y));
        var br = TransformPoint(new Point(rect.Right, rect.Bottom));
        var bl = TransformPoint(new Point(rect.X, rect.Bottom));

        var packed = PackColor(color);
        Span<Vertex2D> vertices =
        [
            new(tl.X, tl.Y, u0, v0, packed),
            new(tr.X, tr.Y, u1, v0, packed),
            new(br.X, br.Y, u1, v1, packed),
            new(bl.X, bl.Y, u0, v1, packed)
        ];
        ReadOnlySpan<uint> indices = [0, 1, 2, 0, 2, 3];
        AddBatch(vertices, indices);
    }

    public void DrawRect(Rect rect, Pen pen)
    {
        if (!EnsureFrame()) return;
        if (rect.IsEmpty || pen.Width <= 0) return;
        var w = pen.Width;
        // Draw as 4 filled rects (top, right, bottom, left)
        FillRect(new Rect(rect.X, rect.Y, rect.Width, w), pen.Brush);
        FillRect(new Rect(rect.Right - w, rect.Y, w, rect.Height), pen.Brush);
        FillRect(new Rect(rect.X, rect.Bottom - w, rect.Width, w), pen.Brush);
        FillRect(new Rect(rect.X, rect.Y, w, rect.Height), pen.Brush);
    }

    public void FillPath(PathGeometry path, Brush brush)
    {
        if (!EnsureFrame()) return;
        var contours = FlattenPath(path);
        if (contours.Count == 0) return;

        var tess = Triangulate(contours);
        var triangleVertexCount = tess.ElementCount * 3;
        if (triangleVertexCount == 0) return;

        var bounds = GetPathBounds(path);
        var color = ResolveBrushColor(brush, bounds.Center);
        var packed = PackColor(color);
        var (u0, v0, u1, v1) = VulkanTextureAtlas.WhitePixelUV;

        var vertices = ArrayPool<Vertex2D>.Shared.Rent(triangleVertexCount);
        var indices = ArrayPool<uint>.Shared.Rent(triangleVertexCount);
        try
        {
            for (var i = 0; i < triangleVertexCount; i++)
            {
                var vertex = tess.Vertices[tess.Elements[i]].Position;
                var p = TransformPoint(new Point(vertex.X, vertex.Y));
                vertices[i] = new Vertex2D(p.X, p.Y, u0, v0, packed);
                indices[i] = (uint)i;
            }
            AddBatch(vertices.AsSpan(0, triangleVertexCount), indices.AsSpan(0, triangleVertexCount));
        }
        finally
        {
            ArrayPool<Vertex2D>.Shared.Return(vertices);
            ArrayPool<uint>.Shared.Return(indices);
        }
    }

    public void DrawPath(PathGeometry path, Pen pen)
    {
        if (!EnsureFrame()) return;
        if (pen.Width <= 0) return;
        // Stroke as filled outline: expand path by pen width
        var contours = FlattenPath(path);
        if (contours.Count == 0) return;

        var color = ResolveBrushColor(pen.Brush, GetPathBounds(path).Center);
        var packed = PackColor(color);
        var (u0, v0, u1, v1) = VulkanTextureAtlas.WhitePixelUV;

        _scratchVertices.Clear();
        _scratchIndices.Clear();

        foreach (var contour in contours)
        {
            if (contour.Count < 2) continue;
            StrokeContour(contour, pen.Width / 2f, packed, u0, v0, u1, v1, _scratchVertices, _scratchIndices);
        }

        if (_scratchVertices.Count > 0)
            AddBatch(CollectionsMarshal.AsSpan(_scratchVertices), CollectionsMarshal.AsSpan(_scratchIndices));
    }

    public void FillGeometry(Geometry geometry, Brush brush)
    {
        if (!EnsureFrame()) return;
        switch (geometry)
        {
            case RectGeometry rect:
                FillRect(rect.Rect, brush);
                break;
            case RoundedRectGeometry rounded:
                FillRoundedRect(rounded.Rect, rounded.RadiusX, rounded.RadiusY, brush);
                break;
            case EllipseGeometry ellipse:
                FillEllipse(ellipse.Center, ellipse.RadiusX, ellipse.RadiusY, brush);
                break;
            case PathGeometry path:
                FillPath(path, brush);
                break;
        }
    }

    public void DrawGeometry(Geometry geometry, Pen pen)
    {
        if (!EnsureFrame()) return;
        switch (geometry)
        {
            case RectGeometry rect:
                DrawRect(rect.Rect, pen);
                break;
            case RoundedRectGeometry rounded:
                DrawRoundedRect(rounded.Rect, rounded.RadiusX, rounded.RadiusY, pen);
                break;
            case EllipseGeometry ellipse:
                DrawEllipse(ellipse.Center, ellipse.RadiusX, ellipse.RadiusY, pen);
                break;
            case PathGeometry path:
                DrawPath(path, pen);
                break;
        }
    }

    public void DrawText(TextLayout text, Point origin, Brush brush)
    {
        if (!EnsureFrame()) return;
        if (string.IsNullOrEmpty(text.Text)) return;
        var color = brush is SolidColorBrush solid ? solid.Color : Color.Black;
        var packed = PackColor(color);

        if (IsDpiOnlyTransform())
        {
            DrawPixelAlignedText(text, origin, packed);
            return;
        }

        var x = origin.X;
        var y = origin.Y;
        var lineHeight = text.Font.Size * text.LineHeight;

        foreach (var rune in text.Text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                x = origin.X;
                y += lineHeight;
                continue;
            }
            if (!rune.IsBmp) { x += text.Font.Size * 0.5f; continue; }

            var glyph = GetOrRasterizeGlyph(text.Font, (char)rune.Value);
            if (glyph is not { } resolvedGlyph) { x += text.Font.Size * 0.5f; continue; }

            if (resolvedGlyph.AtlasW > 0 && resolvedGlyph.AtlasH > 0)
            {
                var gx = x + resolvedGlyph.OffsetX;
                var gy = y + resolvedGlyph.OffsetY;
                var (u0, v0, u1, v1) = _atlas.GetUV(resolvedGlyph.AtlasX, resolvedGlyph.AtlasY, resolvedGlyph.AtlasW, resolvedGlyph.AtlasH);

                var tl = TransformPoint(new Point(gx, gy));
                var tr = TransformPoint(new Point(gx + resolvedGlyph.DrawWidth, gy));
                var br = TransformPoint(new Point(gx + resolvedGlyph.DrawWidth, gy + resolvedGlyph.DrawHeight));
                var bl = TransformPoint(new Point(gx, gy + resolvedGlyph.DrawHeight));

                Span<Vertex2D> verts =
                [
                    new(tl.X, tl.Y, u0, v0, packed),
                    new(tr.X, tr.Y, u1, v0, packed),
                    new(br.X, br.Y, u1, v1, packed),
                    new(bl.X, bl.Y, u0, v1, packed)
                ];
                ReadOnlySpan<uint> idx = [0, 1, 2, 0, 2, 3];
                AddBatch(verts, idx);
            }
            x += resolvedGlyph.Advance;
        }
    }

    public void DrawImage(Image image, Rect dest, Rect? source = null)
    {
        if (!EnsureFrame()) return;
        if (image is not Bitmap bitmap || bitmap.IsDisposed) return;

        // Cache the atlas region per bitmap so re-rendering the same image (e.g. on every
        // caret-blink full-frame redraw) reuses one allocation instead of leaking a fresh
        // region each frame until the atlas fills up and throws. Atlas allocation is
        // append-only, so a cached region stays valid for the atlas lifetime.
        if (!_imageCache.TryGetValue(bitmap, out var cached))
        {
            var (ax, ay) = _atlas.Allocate(bitmap.Width, bitmap.Height);

            _atlas.WriteBgraRegion(ax, ay, bitmap.Width, bitmap.Height, bitmap.Pixels);
            cached = new CachedImage { AtlasX = ax, AtlasY = ay, Width = bitmap.Width, Height = bitmap.Height };
            _imageCache.Add(bitmap, cached);
        }

        var (u0, v0, u1, v1) = _atlas.GetUV(cached.AtlasX, cached.AtlasY, cached.Width, cached.Height);
        // Adjust UVs for source rect
        if (source.HasValue)
        {
            var su0 = source.Value.X / cached.Width;
            var sv0 = source.Value.Y / cached.Height;
            var su1 = source.Value.Right / cached.Width;
            var sv1 = source.Value.Bottom / cached.Height;
            u0 = (cached.AtlasX + su0 * cached.Width) / (float)VulkanTextureAtlas.AtlasWidth;
            v0 = (cached.AtlasY + sv0 * cached.Height) / (float)VulkanTextureAtlas.AtlasHeight;
            u1 = (cached.AtlasX + su1 * cached.Width) / (float)VulkanTextureAtlas.AtlasWidth;
            v1 = (cached.AtlasY + sv1 * cached.Height) / (float)VulkanTextureAtlas.AtlasHeight;
        }

        var white = 0xFFFFFFFFu;
        var tl = TransformPoint(new Point(dest.X, dest.Y));
        var tr = TransformPoint(new Point(dest.Right, dest.Y));
        var br = TransformPoint(new Point(dest.Right, dest.Bottom));
        var bl = TransformPoint(new Point(dest.X, dest.Bottom));

        Span<Vertex2D> verts =
        [
            new(tl.X, tl.Y, u0, v0, white),
            new(tr.X, tr.Y, u1, v0, white),
            new(br.X, br.Y, u1, v1, white),
            new(bl.X, bl.Y, u0, v1, white)
        ];
        ReadOnlySpan<uint> idx = [0, 1, 2, 0, 2, 3];
        AddBatch(verts, idx);
    }

    // ─── Layer / Opacity ──────────────────────────────────────────────────

    public void PushLayer(Rect bounds, float opacity)
    {
        if (!EnsureFrame()) return;
        _opacityStack.Push(_currentOpacity);
        _currentOpacity *= Math.Clamp(opacity, 0, 1);
    }

    public void PopLayer()
    {
        if (!EnsureFrame()) return;
        if (_opacityStack.Count > 0)
            _currentOpacity = _opacityStack.Pop();
    }

    // ─── Resize ───────────────────────────────────────────────────────────

    public void Resize(Size canvasSize) => Resize(canvasSize, DpiScale);

    public void Resize(Size canvasSize, float dpiScale)
    {
        if (_frameStarted)
        {
            SubmitFrame();
            _frameStarted = false;
        }

        DpiScale = NormalizeDpi(dpiScale);
        CanvasSize = canvasSize;
        if (canvasSize.Width <= 0 || canvasSize.Height <= 0)
        {
            _minimized = true;
            return;
        }
        var w = ToPhysical(canvasSize.Width, DpiScale);
        var h = ToPhysical(canvasSize.Height, DpiScale);
        if (w < 1 || h < 1)
        {
            // Degenerate size (window minimized/collapsed): skip swapchain recreation
            // and pause rendering until a valid size arrives.
            _minimized = true;
            return;
        }
        _minimized = false;
        var sampleCountChanged = _device.ConfigureColorSampleCount(w, h);
        _swapchain.Recreate(w, h);
        if (sampleCountChanged)
            _pipeline.RecreateGraphicsPipeline();
        _pipeline.UpdateProjection(_swapchain.Extent.Width, _swapchain.Extent.Height);
        if (_readbackEnabled)
            _readback.EnsureSize(_swapchain.Extent.Width, _swapchain.Extent.Height);
        _transformStack.Clear();
        _clipStack.Clear();
        _opacityStack.Clear();
        _currentTransform = Matrix3x2.CreateScale(DpiScale);
        _currentOpacity = 1f;
        _currentClip = new Rect(0, 0, _swapchain.Extent.Width, _swapchain.Extent.Height);
        _frameStarted = false;
    }

    // ─── Capture (IRenderBitmapSource) ───────────────────────────────────

    /// <summary>
    /// Returns the most recently presented frame as read back from the GPU.
    /// Unlike a software re-render, this reflects the actual Vulkan output.
    /// </summary>
    public bool IsCaptureAvailable => _readbackEnabled;

    public Bitmap CaptureBitmap()
    {
        if (!_readbackEnabled)
            throw new VulkanException("GPU readback is disabled. Set SQUARE_VULKAN_READBACK=1 before starting the application to enable it.");
        return _readback.CaptureBitmap();
    }

    private void DrawPixelAlignedText(TextLayout text, Point origin, uint packed)
    {
        var physicalOrigin = TransformPoint(origin);
        var lineStart = (int)MathF.Round(physicalOrigin.X);
        var x = lineStart;
        var y = (int)MathF.Round(physicalOrigin.Y);
        var lineHeight = Math.Max(1, (int)MathF.Round(text.Font.Size * text.LineHeight * DpiScale));

        foreach (var rune in text.Text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                x = lineStart;
                y += lineHeight;
                continue;
            }
            if (!rune.IsBmp) { x += Math.Max(1, (int)MathF.Round(text.Font.Size * DpiScale * 0.5f)); continue; }

            var glyph = GetOrRasterizeGlyph(text.Font, (char)rune.Value);
            if (glyph is not { } resolvedGlyph)
            {
                x += Math.Max(1, (int)MathF.Round(text.Font.Size * DpiScale * 0.5f));
                continue;
            }

            if (resolvedGlyph.AtlasW > 0 && resolvedGlyph.AtlasH > 0)
            {
                var left = x + resolvedGlyph.PhysicalOffsetX - resolvedGlyph.FilterBorder;
                var top = y + resolvedGlyph.PhysicalOffsetY - resolvedGlyph.FilterBorder;
                var right = left + resolvedGlyph.AtlasW;
                var bottom = top + resolvedGlyph.AtlasH;
                var (u0, v0, u1, v1) = _atlas.GetUV(
                    resolvedGlyph.AtlasX, resolvedGlyph.AtlasY, resolvedGlyph.AtlasW, resolvedGlyph.AtlasH);

                Span<Vertex2D> vertices =
                [
                    new(left, top, u0, v0, packed),
                    new(right, top, u1, v0, packed),
                    new(right, bottom, u1, v1, packed),
                    new(left, bottom, u0, v1, packed)
                ];
                ReadOnlySpan<uint> indices = [0, 1, 2, 0, 2, 3];
                AddBatch(vertices, indices);
            }
            x += resolvedGlyph.PhysicalAdvance;
        }
    }

    private bool IsDpiOnlyTransform()
    {
        const float tolerance = 0.0001f;
        return MathF.Abs(_currentTransform.M11 - DpiScale) < tolerance &&
               MathF.Abs(_currentTransform.M22 - DpiScale) < tolerance &&
               MathF.Abs(_currentTransform.M12) < tolerance &&
               MathF.Abs(_currentTransform.M21) < tolerance;
    }

    // ─── Dispose ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _device.Api.DeviceWaitIdle(_device.Device);
        _readback.Dispose();
        _batchRenderer.Dispose();
        _atlas.Dispose();
        _pipeline.Dispose();
        _swapchain.Dispose();
        _device.Dispose();
    }

    // ─── Shape helpers ────────────────────────────────────────────────────

    private void FillRoundedRect(Rect rect, float rx, float ry, Brush brush)
    {
        rx = Math.Min(rx, rect.Width / 2);
        ry = Math.Min(ry, rect.Height / 2);
        if (rx <= 0 || ry <= 0) { FillRect(rect, brush); return; }

        var path = CreateRoundedRectPath(rect, rx, ry);
        FillPath(path, brush);
    }

    private void DrawRoundedRect(Rect rect, float rx, float ry, Pen pen)
    {
        rx = Math.Min(rx, rect.Width / 2);
        ry = Math.Min(ry, rect.Height / 2);
        if (rx <= 0 || ry <= 0) { DrawRect(rect, pen); return; }

        var path = CreateRoundedRectPath(rect, rx, ry);
        DrawPath(path, pen);
    }

    private void FillEllipse(Point center, float radiusX, float radiusY, Brush brush)
    {
        const int segments = 32;
        var color = ResolveBrushColor(brush, center);
        var packed = PackColor(color);
        var (u0, v0, u1, v1) = VulkanTextureAtlas.WhitePixelUV;

        Span<Vertex2D> vertices = stackalloc Vertex2D[segments + 1];
        Span<uint> indices = stackalloc uint[segments * 3];

        var c = TransformPoint(center);
        vertices[0] = new Vertex2D(c.X, c.Y, u0, v0, packed);

        for (var i = 0; i <= segments; i++)
        {
            var angle = (float)(i * 2 * Math.PI / segments);
            var px = center.X + radiusX * MathF.Cos(angle);
            var py = center.Y + radiusY * MathF.Sin(angle);
            var tp = TransformPoint(new Point(px, py));
            if (i < segments)
                vertices[i + 1] = new Vertex2D(tp.X, tp.Y, u0, v0, packed);
        }

        for (var i = 0; i < segments; i++)
        {
            indices[i * 3] = 0;
            indices[i * 3 + 1] = (uint)(i + 1);
            indices[i * 3 + 2] = (uint)(i + 2 > segments ? 1 : i + 2);
        }
        AddBatch(vertices, indices);
    }

    private void DrawEllipse(Point center, float radiusX, float radiusY, Pen pen)
    {
        const int segments = 32;
        var color = ResolveBrushColor(pen.Brush, center);
        var packed = PackColor(color);
        var (u0, v0, u1, v1) = VulkanTextureAtlas.WhitePixelUV;
        var halfW = pen.Width / 2f;

        Span<Vertex2D> vertices = stackalloc Vertex2D[segments * 4];
        Span<uint> indices = stackalloc uint[segments * 6];
        var vertexCount = 0;
        var indexCount = 0;

        for (var i = 0; i < segments; i++)
        {
            var a0 = (float)(i * 2 * Math.PI / segments);
            var a1 = (float)((i + 1) * 2 * Math.PI / segments);

            var innerX0 = center.X + (radiusX - halfW) * MathF.Cos(a0);
            var innerY0 = center.Y + (radiusY - halfW) * MathF.Sin(a0);
            var outerX0 = center.X + (radiusX + halfW) * MathF.Cos(a0);
            var outerY0 = center.Y + (radiusY + halfW) * MathF.Sin(a0);
            var innerX1 = center.X + (radiusX - halfW) * MathF.Cos(a1);
            var innerY1 = center.Y + (radiusY - halfW) * MathF.Sin(a1);
            var outerX1 = center.X + (radiusX + halfW) * MathF.Cos(a1);
            var outerY1 = center.Y + (radiusY + halfW) * MathF.Sin(a1);

            var baseIdx = (uint)vertexCount;
            var p0 = TransformPoint(new Point(innerX0, innerY0));
            var p1 = TransformPoint(new Point(outerX0, outerY0));
            var p2 = TransformPoint(new Point(outerX1, outerY1));
            var p3 = TransformPoint(new Point(innerX1, innerY1));

            vertices[vertexCount++] = new Vertex2D(p0.X, p0.Y, u0, v0, packed);
            vertices[vertexCount++] = new Vertex2D(p1.X, p1.Y, u0, v0, packed);
            vertices[vertexCount++] = new Vertex2D(p2.X, p2.Y, u0, v0, packed);
            vertices[vertexCount++] = new Vertex2D(p3.X, p3.Y, u0, v0, packed);

            indices[indexCount++] = baseIdx; indices[indexCount++] = baseIdx + 1; indices[indexCount++] = baseIdx + 2;
            indices[indexCount++] = baseIdx; indices[indexCount++] = baseIdx + 2; indices[indexCount++] = baseIdx + 3;
        }
        AddBatch(vertices[..vertexCount], indices[..indexCount]);
    }

    private static PathGeometry CreateRoundedRectPath(Rect rect, float rx, float ry)
    {
        // Approximate arcs with line segments
        var path = PathGeometry.Create();
        const int arcSegments = 8;

        var l = rect.X; var t = rect.Y; var r = rect.Right; var b = rect.Bottom;

        path.MoveTo(new Point(l + rx, t));
        path.LineTo(new Point(r - rx, t));
        AddArc(path, r - rx, t + ry, rx, ry, -MathF.PI / 2, MathF.PI / 2, arcSegments);
        path.LineTo(new Point(r, b - ry));
        AddArc(path, r - rx, b - ry, rx, ry, 0, MathF.PI / 2, arcSegments);
        path.LineTo(new Point(l + rx, b));
        AddArc(path, l + rx, b - ry, rx, ry, MathF.PI / 2, MathF.PI / 2, arcSegments);
        path.LineTo(new Point(l, t + ry));
        AddArc(path, l + rx, t + ry, rx, ry, MathF.PI, MathF.PI / 2, arcSegments);
        path.Close();
        return path;
    }

    private static void AddArc(PathGeometry path, float cx, float cy, float rx, float ry, float startAngle, float sweep, int segments)
    {
        for (var i = 1; i <= segments; i++)
        {
            var angle = startAngle + sweep * i / segments;
            path.LineTo(new Point(cx + rx * MathF.Cos(angle), cy + ry * MathF.Sin(angle)));
        }
    }

    // ─── Triangulation ────────────────────────────────────────────────────

    private List<List<Point>> FlattenPath(PathGeometry path)
    {
        var contours = new List<List<Point>>();
        var current = new List<Point>();
        Point first = default;

        foreach (var cmd in path.Commands)
        {
            switch (cmd)
            {
                case MoveToCmd move:
                    if (current.Count > 0) contours.Add(current);
                    current = [move.Point];
                    first = move.Point;
                    break;
                case LineToCmd line:
                    current.Add(line.Point);
                    break;
                case ArcToCmd arc:
                    FlattenArc(current, arc);
                    break;
                case CloseCmd:
                    if (current.Count > 0)
                    {
                        if (current[^1] != first) current.Add(first);
                        contours.Add(current);
                        current = [];
                    }
                    break;
            }
        }
        if (current.Count > 0) contours.Add(current);
        return contours;
    }

    private static void FlattenArc(List<Point> contour, ArcToCmd arc)
    {
        var cx = arc.Oval.X + arc.Oval.Width / 2;
        var cy = arc.Oval.Y + arc.Oval.Height / 2;
        var rx = arc.Oval.Width / 2;
        var ry = arc.Oval.Height / 2;
        var startRad = arc.StartAngle * MathF.PI / 180f;
        var sweepRad = arc.SweepAngle * MathF.PI / 180f;
        const int segments = 16;

        for (var i = 1; i <= segments; i++)
        {
            var angle = startRad + sweepRad * i / segments;
            contour.Add(new Point(cx + rx * MathF.Cos(angle), cy + ry * MathF.Sin(angle)));
        }
    }

    private static LibTessDotNet.Tess Triangulate(List<List<Point>> contours)
    {
        // Use LibTessDotNet for polygon triangulation
        var tess = new LibTessDotNet.Tess();

        foreach (var contour in contours)
        {
            if (contour.Count < 3) continue;
            var points = new LibTessDotNet.ContourVertex[contour.Count];
            for (var i = 0; i < contour.Count; i++)
                points[i] = new LibTessDotNet.ContourVertex
                {
                    Position = new LibTessDotNet.Vec3 { X = contour[i].X, Y = contour[i].Y, Z = 0 }
                };
            tess.AddContour(points, LibTessDotNet.ContourOrientation.Original);
        }

        tess.Tessellate(LibTessDotNet.WindingRule.EvenOdd, LibTessDotNet.ElementType.Polygons, 3);

        return tess;
    }

    private void StrokeContour(List<Point> contour, float halfWidth, uint packed,
        float u0, float v0, float u1, float v1, List<Vertex2D> vertices, List<uint> indices)
    {
        for (var i = 0; i < contour.Count - 1; i++)
        {
            var p0 = contour[i];
            var p1 = contour[i + 1];
            var dx = p1.X - p0.X;
            var dy = p1.Y - p0.Y;
            var len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) continue;

            var nx = -dy / len * halfWidth;
            var ny = dx / len * halfWidth;

            var baseIdx = (uint)vertices.Count;
            var a = TransformPoint(new Point(p0.X + nx, p0.Y + ny));
            var b = TransformPoint(new Point(p0.X - nx, p0.Y - ny));
            var c = TransformPoint(new Point(p1.X - nx, p1.Y - ny));
            var d = TransformPoint(new Point(p1.X + nx, p1.Y + ny));

            vertices.Add(new Vertex2D(a.X, a.Y, u0, v0, packed));
            vertices.Add(new Vertex2D(b.X, b.Y, u0, v0, packed));
            vertices.Add(new Vertex2D(c.X, c.Y, u0, v0, packed));
            vertices.Add(new Vertex2D(d.X, d.Y, u0, v0, packed));

            indices.Add(baseIdx); indices.Add(baseIdx + 1); indices.Add(baseIdx + 2);
            indices.Add(baseIdx); indices.Add(baseIdx + 2); indices.Add(baseIdx + 3);
        }
    }

    // ─── Glyph cache ──────────────────────────────────────────────────────

    private readonly record struct GlyphCacheKey(string Family, float Size, int Weight, int Style, char Char);

    private struct CachedGlyph
    {
        public int AtlasX, AtlasY, AtlasW, AtlasH;
        public int PhysicalOffsetX, PhysicalOffsetY, PhysicalAdvance, FilterBorder;
        public float OffsetX, OffsetY, DrawWidth, DrawHeight, Advance;
    }

    private sealed class CachedImage
    {
        public int AtlasX, AtlasY, Width, Height;
    }

    private CachedGlyph? GetOrRasterizeGlyph(Font font, char ch)
    {
        var physicalSize = font.Size * DpiScale;
        var key = new GlyphCacheKey(font.Family, physicalSize, (int)font.Weight, (int)font.Style, ch);
        if (_glyphCache.TryGetValue(key, out var cached)) return cached;

        var rasterized = _glyphRasterizer.Rasterize(font.WithSize(physicalSize), ch);
        if (rasterized == null) return null;

        var glyph = new CachedGlyph
        {
            PhysicalOffsetX = rasterized.OffsetX,
            PhysicalOffsetY = rasterized.OffsetY,
            PhysicalAdvance = rasterized.AdvanceX,
            OffsetX = rasterized.OffsetX / DpiScale,
            OffsetY = rasterized.OffsetY / DpiScale,
            Advance = rasterized.AdvanceX / DpiScale
        };

        if (rasterized.Width > 0 && rasterized.Height > 0)
        {
            const int filterBorder = 1;
            var atlasWidth = rasterized.Width + filterBorder * 2;
            var atlasHeight = rasterized.Height + filterBorder * 2;
            var (ax, ay) = _atlas.Allocate(atlasWidth, atlasHeight);
            _atlas.WritePaddedCoverageRegion(ax, ay, rasterized.Width, rasterized.Height,
                rasterized.Stride, rasterized.Coverage, filterBorder);
            glyph.AtlasX = ax;
            glyph.AtlasY = ay;
            glyph.AtlasW = atlasWidth;
            glyph.AtlasH = atlasHeight;
            glyph.FilterBorder = filterBorder;
            glyph.DrawWidth = atlasWidth / DpiScale;
            glyph.DrawHeight = atlasHeight / DpiScale;
            glyph.OffsetX -= filterBorder / DpiScale;
            glyph.OffsetY -= filterBorder / DpiScale;
        }

        _glyphCache[key] = glyph;
        return glyph;
    }

    // ─── Utility ──────────────────────────────────────────────────────────

    private void AddBatch(ReadOnlySpan<Vertex2D> vertices, ReadOnlySpan<uint> indices)
    {
        var clip = _currentClip;
        if (clip.IsEmpty) return;

        var left = (int)MathF.Floor(clip.X);
        var top = (int)MathF.Floor(clip.Y);
        var right = (int)MathF.Ceiling(clip.Right);
        var bottom = (int)MathF.Ceiling(clip.Bottom);
        _batchRenderer.AddBatch(vertices, indices, 0,
            left, top, right - left, bottom - top);
    }

    private Point TransformPoint(Point p)
    {
        var x = p.X * _currentTransform.M11 + p.Y * _currentTransform.M21 + _currentTransform.M31;
        var y = p.X * _currentTransform.M12 + p.Y * _currentTransform.M22 + _currentTransform.M32;
        return new Point(x, y);
    }

    private Rect TransformRect(Rect rect)
    {
        var tl = TransformPoint(new Point(rect.X, rect.Y));
        var br = TransformPoint(new Point(rect.Right, rect.Bottom));
        var tr = TransformPoint(new Point(rect.Right, rect.Y));
        var bl = TransformPoint(new Point(rect.X, rect.Bottom));
        var minX = Math.Min(Math.Min(tl.X, tr.X), Math.Min(bl.X, br.X));
        var minY = Math.Min(Math.Min(tl.Y, tr.Y), Math.Min(bl.Y, br.Y));
        var maxX = Math.Max(Math.Max(tl.X, tr.X), Math.Max(bl.X, br.X));
        var maxY = Math.Max(Math.Max(tl.Y, tr.Y), Math.Max(bl.Y, br.Y));
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static Rect IntersectRects(Rect a, Rect b)
    {
        var x = Math.Max(a.X, b.X);
        var y = Math.Max(a.Y, b.Y);
        var r = Math.Min(a.Right, b.Right);
        var bot = Math.Min(a.Bottom, b.Bottom);
        return new Rect(x, y, Math.Max(0, r - x), Math.Max(0, bot - y));
    }

    private Color ResolveBrushColor(Brush brush, Point at)
    {
        var color = brush switch
        {
            SolidColorBrush solid => solid.Color,
            LinearGradientBrush linear => SampleGradient(linear.Stops, linear.SpreadMethod,
                ProjectGradientOffset(at, linear.Start, linear.End)),
            RadialGradientBrush radial => SampleGradient(radial.Stops, radial.SpreadMethod,
                radial.Radius > 0 ? MathF.Sqrt(MathF.Pow(at.X - radial.Center.X, 2) + MathF.Pow(at.Y - radial.Center.Y, 2)) / radial.Radius : 0),
            _ => Color.Transparent
        };
        // Apply layer opacity
        if (_currentOpacity < 1f)
            color = new Color(color.R, color.G, color.B, (byte)(color.A * _currentOpacity));
        return color;
    }

    private static float ProjectGradientOffset(Point p, Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lenSq = dx * dx + dy * dy;
        if (lenSq <= float.Epsilon) return 0;
        return ((p.X - start.X) * dx + (p.Y - start.Y) * dy) / lenSq;
    }

    private static Color SampleGradient(GradientStop[] stops, GradientSpreadMethod spread, float offset)
    {
        if (stops.Length == 0) return Color.Transparent;
        offset = spread switch
        {
            GradientSpreadMethod.Repeat => offset - MathF.Floor(offset),
            GradientSpreadMethod.Reflect => ReflectOffset(offset),
            _ => Math.Clamp(offset, 0, 1)
        };
        GradientStop? minimum = null;
        GradientStop? maximum = null;
        GradientStop? lower = null;
        GradientStop? upper = null;
        foreach (var stop in stops)
        {
            if (minimum == null || stop.Offset < minimum.Offset) minimum = stop;
            if (maximum == null || stop.Offset >= maximum.Offset) maximum = stop;
            if (stop.Offset < offset && (lower == null || stop.Offset >= lower.Offset)) lower = stop;
            if (stop.Offset >= offset && (upper == null || stop.Offset < upper.Offset)) upper = stop;
        }
        if (offset <= minimum!.Offset) return minimum.Color;
        if (offset >= maximum!.Offset) return maximum.Color;
        lower ??= minimum;
        upper ??= maximum;
        var range = upper.Offset - lower.Offset;
        var t = range <= float.Epsilon ? 0 : (offset - lower.Offset) / range;
        return LerpColor(lower.Color, upper.Color, t);
    }

    private static float ReflectOffset(float t)
    {
        t = Math.Abs(t % 2f);
        return t > 1f ? 2f - t : t;
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return new Color(
            (byte)MathF.Round(a.R + (b.R - a.R) * t),
            (byte)MathF.Round(a.G + (b.G - a.G) * t),
            (byte)MathF.Round(a.B + (b.B - a.B) * t),
            (byte)MathF.Round(a.A + (b.A - a.A) * t));
    }

    private uint PackColor(Color c)
    {
        // RGBA8 packed as uint (R in lowest byte for R8G8B8A8Unorm vertex attribute)
        return (uint)(c.R | (c.G << 8) | (c.B << 16) | (c.A << 24));
    }

    private static Rect GetPathBounds(PathGeometry path)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var cmd in path.Commands)
        {
            Point? p = cmd switch
            {
                MoveToCmd m => m.Point,
                LineToCmd l => l.Point,
                ArcToCmd a => a.Oval.Center,
                _ => null
            };
            if (p is null) continue;
            minX = Math.Min(minX, p.Value.X);
            minY = Math.Min(minY, p.Value.Y);
            maxX = Math.Max(maxX, p.Value.X);
            maxY = Math.Max(maxY, p.Value.Y);
        }
        if (minX > maxX) return Rect.Empty;
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static uint ToPhysical(float logical, float dpi) => (uint)Math.Max(1, MathF.Ceiling(logical * dpi));
    private static float NormalizeDpi(float dpi) => float.IsFinite(dpi) && dpi > 0 ? dpi : 1f;
}
