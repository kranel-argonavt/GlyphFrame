using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GlyphFrame.App.Views;

internal static class WindowsCaptionTheme
{
    private const int UseImmersiveDarkMode = 20;
    private const int LegacyUseImmersiveDarkMode = 19;

    public static void Apply(Window window)
    {
        nint handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
        {
            return;
        }

        int enabled = 1;
        if (DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
        {
            _ = DwmSetWindowAttribute(handle, LegacyUseImmersiveDarkMode, ref enabled, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint windowHandle, int attribute, ref int value, int valueSize);
}
