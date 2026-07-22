using System.Runtime.InteropServices;
using Square.Graphics;

namespace Square.Platform.X11;

internal static class X11WindowScreenshot
{
    public static bool TryCaptureByProcessId(int processId, out Bitmap? bitmap)
    {
        bitmap = null;
        var display = X11Api.OpenDisplay(null);
        if (display == IntPtr.Zero) return false;
        try
        {
            var pidAtom = X11Api.InternAtom(display, "_NET_WM_PID", true);
            if (pidAtom == IntPtr.Zero) return false;
            var window = FindWindow(display, X11Api.DefaultRootWindow(display), pidAtom, processId);
            if (window == IntPtr.Zero || !X11Api.GetGeometry(
                    display, window, out _, out _, out _, out var width, out var height, out _, out _)
                || width == 0 || height == 0)
                return false;

            var image = X11Api.GetImage(display, window, 0, 0, width, height, X11Api.AllPlanes, X11Api.ZPixmap);
            if (image == IntPtr.Zero) return false;
            try
            {
                var result = new Bitmap(checked((int)width), checked((int)height));
                for (var y = 0; y < result.Height; y++)
                {
                    var row = result.GetRow(y);
                    for (var x = 0; x < result.Width; x++)
                    {
                        var pixel = X11Api.GetPixel(image, x, y);
                        var offset = x * 4;
                        row[offset] = (byte)pixel;
                        row[offset + 1] = (byte)(pixel >> 8);
                        row[offset + 2] = (byte)(pixel >> 16);
                        row[offset + 3] = 255;
                    }
                }
                bitmap = result;
                return true;
            }
            finally { X11Api.DestroyImage(image); }
        }
        finally { X11Api.CloseDisplay(display); }
    }

    private static IntPtr FindWindow(IntPtr display, IntPtr window, IntPtr pidAtom, int processId)
    {
        if (GetProcessId(display, window, pidAtom) == processId) return window;
        if (!X11Api.QueryTree(display, window, out _, out _, out var children, out var count) || children == IntPtr.Zero)
            return IntPtr.Zero;
        try
        {
            for (var i = 0; i < count; i++)
            {
                var child = Marshal.ReadIntPtr(children, i * IntPtr.Size);
                var found = FindWindow(display, child, pidAtom, processId);
                if (found != IntPtr.Zero) return found;
            }
            return IntPtr.Zero;
        }
        finally { X11Api.Free(children); }
    }

    private static int GetProcessId(IntPtr display, IntPtr window, IntPtr pidAtom)
    {
        var rc = X11Api.GetWindowProperty(
            display, window, pidAtom, 0, 1, false, IntPtr.Zero,
            out _, out var format, out var count, out _, out var data);
        if (rc != X11Api.Success || data == IntPtr.Zero) return 0;
        try
        {
            return format == 32 && count > 0 ? unchecked((int)Marshal.ReadInt64(data)) : 0;
        }
        finally { X11Api.Free(data); }
    }
}
