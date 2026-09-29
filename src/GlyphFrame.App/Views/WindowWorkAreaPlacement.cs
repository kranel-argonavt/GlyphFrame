using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace GlyphFrame.App.Views;

/// <summary>Fits an opening window to its owner's monitor (or its own nearest monitor) in DIPs.</summary>
internal static class WindowWorkAreaPlacement
{
    private const uint MonitorDefaultToNearest = 2;
    private const double WorkAreaInset = 8;

    public static void FitInitialBounds(Window window)
    {
        nint handle = new WindowInteropHelper(MonitorAnchor(window)).Handle;
        if (handle == 0)
        {
            return;
        }

        nint monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        MonitorInfo info = new() { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        HwndSource? source = HwndSource.FromHwnd(handle);
        if (source?.CompositionTarget is null)
        {
            return;
        }

        Rect workArea = ConvertWorkArea(info.Work, source.CompositionTarget.TransformFromDevice);
        Rect bounds = CalculateBounds(workArea, new Size(window.Width, window.Height),
            new Size(window.MinWidth, window.MinHeight));
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = bounds.Width;
        window.Height = bounds.Height;
        window.Left = bounds.Left;
        window.Top = bounds.Top;
    }

    internal static Window MonitorAnchor(Window window) => window.Owner ?? window;

    internal static Rect CalculateBounds(Rect workArea, Size preferred, Size minimum)
    {
        double availableWidth = Math.Max(0, workArea.Width - 2 * WorkAreaInset);
        double availableHeight = Math.Max(0, workArea.Height - 2 * WorkAreaInset);
        double width = Math.Max(minimum.Width, Math.Min(preferred.Width, availableWidth));
        double height = Math.Max(minimum.Height, Math.Min(preferred.Height, availableHeight));
        double left = workArea.Left + Math.Max(0, (workArea.Width - width) / 2);
        double top = workArea.Top + Math.Max(0, (workArea.Height - height) / 2);
        return new Rect(left, top, width, height);
    }

    internal static Rect ConvertWorkArea(Rect pixels, Matrix fromDevice)
    {
        Point topLeft = fromDevice.Transform(pixels.TopLeft);
        Point bottomRight = fromDevice.Transform(pixels.BottomRight);
        return new Rect(topLeft, bottomRight);
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint windowHandle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public static implicit operator Rect(NativeRect value) =>
            new(value.Left, value.Top, value.Right - value.Left, value.Bottom - value.Top);
    }
}
