using Square.Backends;
using Square.Controls.Controls;
using Square.Events;
using Square.Graphics;
using Square.Layout.Engine;
using Square.Platform;
using Square.Rendering;
using Square.Runtime;
using Square.UI;

namespace Square.Sample;

public static class Program
{
    public static void Main()
    {
        System.Console.WriteLine("Square Framework - M1 Window Demo");

        BackendRegistration.RegisterDefaults();
        PlatformRegistration.RegisterDefaults();
        var dispatcher = new Dispatcher();
        SampleSignals.Initialize(dispatcher);

        // Build generated component from .sqx
        var main = new Main();
        main.BuildVisualTree();
        ((IComponentLifecycle)main).OnAttached();

        // Create Win32 window
        var platform = PlatformRegistry.Get();
        var host = platform.CreateHost(new PlatformHostCreateInfo
        {
            Title = "Square Framework",
            Width = 900,
            Height = 980
        });

        host.Show();
        var ctx = host.CreateRenderContext();
        var layout = new LayoutEngine();
        var renderTree = new RenderTree();
        UIElement? focusedInput = null;
        ITextEditor? focusedEditor = null;
        var isSelectingText = false;
        var scheduledFrames = new Dictionary<Visual, double>();
        var appClock = System.Diagnostics.Stopwatch.StartNew();
        main.AddEventListener(StandardEvents.RequestFrame, (_, e) =>
        {
            if (e.OriginalSource is Visual target)
            {
                var requestedTime = appClock.Elapsed.TotalSeconds + e.IntervalSeconds;
                if (!scheduledFrames.TryGetValue(target, out var current) || requestedTime < current)
                    scheduledFrames[target] = requestedTime;
            }
            e.Handled = true;
        });

        void RenderFrame()
        {
            var size = host.ClientSize;
            var needsLayout = main.IsLayoutDirty || main.Geometry.Size != size;
            if (needsLayout)
            {
                layout.Measure(main, size);
                layout.Arrange(main, new Rect(0, 0, size.Width, size.Height));
                renderTree.BuildFrom(main);
            }
            else
            {
                renderTree.UpdateDirty();
            }
            ctx.Clear(Color.White);
            renderTree.Render(ctx);
            ctx.Flush();
            ctx.Present();
            if (focusedEditor != null) host.SetTextInputRect(focusedEditor.CaretRect);
        }

        host.SizeChanged += _ => RenderFrame();

        host.WheelEvent += (pt, delta) =>
        {
            var hit = main.HitTest(pt);
            hit?.RaiseEvent(StandardEvents.Wheel, new RoutedEventArgs());
            RenderFrame();
        };

        Point? pointerDownPoint = null;
        Visual? pointerDownTarget = null;

        host.MouseEvent += (pt, action) =>
        {
            var hit = main.HitTest(pt);
            if (action == MouseAction.Move)
            {
                host.Cursor = hit is ITextEditor ? CursorKind.Text : CursorKind.Arrow;
                var needsRender = isSelectingText && focusedEditor != null;
                if (needsRender) focusedEditor!.HandlePointerMove(pt);
                foreach (var select in main.QueryAll<Select>())
                    needsRender |= select.HandlePointerMove(pt);
                if (needsRender) RenderFrame();
                return;
            }

            if (action == MouseAction.Up)
            {
                if (isSelectingText && focusedEditor != null)
                {
                    focusedEditor.HandlePointerUp(pt);
                    isSelectingText = false;
                }
                if (pointerDownTarget != null && hit == pointerDownTarget)
                {
                    hit?.RaiseEvent(StandardEvents.Click, new RoutedEventArgs());
                }
                pointerDownPoint = null;
                pointerDownTarget = null;
                RenderFrame();
                return;
            }

            if (action == MouseAction.Down)
            {
                pointerDownPoint = pt;
                pointerDownTarget = hit;
                hit?.RaiseEvent(StandardEvents.PointerDown, new RoutedEventArgs());

                if (hit is ITextEditor editor && hit is UIElement editorElement)
                {
                    if (focusedInput != editorElement)
                    {
                        focusedInput?.Unfocus();
                        focusedInput = editorElement;
                        focusedEditor = editor;
                        focusedInput.Focus();
                    }
                    editor.HandlePointerDown(pt, host.Modifiers.HasFlag(KeyModifiers.Shift));
                    isSelectingText = true;
                }
                else if (focusedInput != null)
                {
                    focusedInput.Unfocus();
                    focusedInput = null;
                    focusedEditor = null;
                    isSelectingText = false;
                }

                foreach (var select in main.QueryAll<Select>())
                    if (hit != select) select.CloseDropDown();

                if (hit is Select selected) selected.HandlePointerDown(pt);
                RenderFrame();
            }
        };

        host.KeyEvent += (keyCode, action) =>
        {
            var keyEvent = action == KeyAction.Down ? StandardEvents.KeyDown : StandardEvents.KeyUp;
            focusedInput?.RaiseEvent(keyEvent, new RoutedEventArgs());

            if (action != KeyAction.Down || focusedEditor == null) return;
            var shift = host.Modifiers.HasFlag(KeyModifiers.Shift);
            var control = host.Modifiers.HasFlag(KeyModifiers.Control);

            if (control && keyCode == 67)
            {
                if (focusedEditor.SelectionLength > 0) host.SetClipboardText(focusedEditor.SelectedText);
            }
            else if (control && keyCode == 88)
            {
                if (focusedEditor.SelectionLength > 0)
                {
                    host.SetClipboardText(focusedEditor.SelectedText);
                    focusedEditor.DeleteSelection();
                }
            }
            else if (control && keyCode == 86)
            {
                focusedEditor.HandleTextInput(host.GetClipboardText());
            }
            else if (control && keyCode == 65)
            {
                focusedEditor.SelectAll();
            }
            else if (control || keyCode is 8 or 13 or 35 or 36 or 37 or 38 or 39 or 40 or 46)
            {
                focusedEditor.HandleKey(keyCode, shift, control);
            }
            else
            {
                return;
            }
            RenderFrame();
        };

        host.TextInput += text =>
        {
            if (focusedEditor == null) return;
            focusedEditor.HandleTextInput(text);
            RenderFrame();
        };

        host.Tick += () =>
        {
            var now = appClock.Elapsed.TotalSeconds;
            var dueTargets = scheduledFrames.Where(pair => now >= pair.Value).Select(pair => pair.Key).ToArray();
            foreach (var target in dueTargets)
            {
                scheduledFrames.Remove(target);
                target.InvalidateVisual();
            }
            var needsRender = dispatcher.HasWork || dueTargets.Length > 0;
            dispatcher.Run();
            if (focusedEditor?.ToggleCaretBlink() == true)
            {
                needsRender = true;
            }
            if (needsRender) RenderFrame();
        };

        // Initial render
        ((IComponentLifecycle)main).OnLoaded();
        RenderFrame();

        // Message loop
        host.PumpEvents();

        ((IComponentLifecycle)main).OnUnloaded();
        ((IComponentLifecycle)main).OnDetached();

        System.Console.WriteLine("Window closed. Demo complete.");
    }
}
