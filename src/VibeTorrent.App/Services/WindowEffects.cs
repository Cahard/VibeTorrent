using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VibeTorrent.App.Services;

internal static class WindowEffects
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmcpRound = 2;

    public static void ApplyWindow(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var light = ThemeService.IsLight;
            var enabled = light ? 0 : 1;
            _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
            var corner = DwmcpRound;
            _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref corner, sizeof(int));

            // Microsoft documentation treats these three colour attributes as
            // COLORREF values; keeping them close to the app background avoids a
            // bright native frame flash on startup and during resize.
            var border = light ? ToColorRef(0xF4, 0xF7, 0xFB) : ToColorRef(0x0A, 0x0C, 0x17);
            var caption = border;
            var text = light ? ToColorRef(0x11, 0x18, 0x27) : ToColorRef(0xF5, 0xF7, 0xFF);
            _ = DwmSetWindowAttribute(handle, DwmwaBorderColor, ref border, sizeof(int));
            _ = DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref caption, sizeof(int));
            _ = DwmSetWindowAttribute(handle, DwmwaTextColor, ref text, sizeof(int));
        }
        catch (Exception)
        {
            // DWM is optional: the custom rendered chrome still looks correct.
        }
    }

    public static void ApplyDarkWindow(Window window) => ApplyWindow(window);

    private static int ToColorRef(byte r, byte g, byte b) => r | (g << 8) | (b << 16);

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int valueSize);
}
