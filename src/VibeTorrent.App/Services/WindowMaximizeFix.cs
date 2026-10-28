using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VibeTorrent.App.Services;

/// <summary>
/// Borderless WPF windows maximize to the monitor rectangle, which puts the
/// bottom edge behind the taskbar. Constrain WM_GETMINMAXINFO to the work area
/// of the monitor that actually contains the window.
/// </summary>
public static class WindowMaximizeFix
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 2;

    public static void Attach(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        HwndSource.FromHwnd(handle)?.AddHook(
            (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (msg != WmGetMinMaxInfo)
                {
                    return IntPtr.Zero;
                }

                var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
                if (monitor == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (!GetMonitorInfo(monitor, ref info))
                {
                    return IntPtr.Zero;
                }

                var minMax = Marshal.PtrToStructure<MinMaxInfo>(lParam);
                minMax.MaxPosition.X = info.Work.Left - info.Monitor.Left;
                minMax.MaxPosition.Y = info.Work.Top - info.Monitor.Top;
                minMax.MaxSize.X = info.Work.Right - info.Work.Left;
                minMax.MaxSize.Y = info.Work.Bottom - info.Work.Top;
                minMax.MaxTrackSize.X = minMax.MaxSize.X;
                minMax.MaxTrackSize.Y = minMax.MaxSize.Y;
                Marshal.StructureToPtr(minMax, lParam, false);
                handled = true;
                return IntPtr.Zero;
            });
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }
}
