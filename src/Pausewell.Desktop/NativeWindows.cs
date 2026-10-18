using System.Runtime.InteropServices;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace Pausewell.Desktop;

internal static class NativeWindows
{
    public static void Place(Window window, Forms.Screen screen)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var b = screen.Bounds;
        if (!SetWindowPos(hwnd, new IntPtr(-1), b.X, b.Y, b.Width, b.Height, 0x0040)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    public static object Bounds(Window window)
    {
        GetWindowRect(new WindowInteropHelper(window).Handle, out var r);
        return new { x = r.Left, y = r.Top, width = r.Right - r.Left, height = r.Bottom - r.Top };
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
}
