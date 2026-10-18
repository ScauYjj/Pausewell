using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Pausewell.ComponentLab;
internal static class NativeCapture
{
    [StructLayout(LayoutKind.Sequential)]private struct Bounds {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]private static extern bool GetClientRect(IntPtr handle,out Bounds bounds);
    [DllImport("user32.dll")]private static extern bool PrintWindow(IntPtr handle,IntPtr dc,uint flags);
    [DllImport("user32.dll")]private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")]private static extern int ReleaseDC(IntPtr window,IntPtr dc);
    [DllImport("gdi32.dll")]private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")]private static extern IntPtr CreateCompatibleBitmap(IntPtr dc,int width,int height);
    [DllImport("gdi32.dll")]private static extern IntPtr SelectObject(IntPtr dc,IntPtr bitmap);
    [DllImport("gdi32.dll")]private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(IntPtr bitmap);
    public static void Save(FrameworkElement element,string path)
    {
        element.UpdateLayout();var source=PresentationSource.FromVisual(element) as HwndSource??throw new InvalidOperationException("截图没有关联到原生窗口。");var handle=source.Handle;
        if(!GetClientRect(handle,out var rect)||rect.Right<=0||rect.Bottom<=0)throw new InvalidOperationException("窗口客户区无效。");
        var screen=GetDC(IntPtr.Zero);var memory=CreateCompatibleDC(screen);var bitmap=CreateCompatibleBitmap(screen,rect.Right,rect.Bottom);var previous=SelectObject(memory,bitmap);
        try{if(!PrintWindow(handle,memory,3))throw new InvalidOperationException("原生窗口截图失败。");BitmapSource capture=Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());if(source.RootVisual is not Window){var inset=(int)Math.Round(24*NativeInput.GetDpiForWindow(handle)/96.0);if(capture.PixelWidth>inset*2&&capture.PixelHeight>inset*2)capture=new CroppedBitmap(capture,new Int32Rect(inset,inset,capture.PixelWidth-inset*2,capture.PixelHeight-inset*2));}var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(capture));using var output=File.Create(path);encoder.Save(output);}
        finally{SelectObject(memory,previous);DeleteObject(bitmap);DeleteDC(memory);ReleaseDC(IntPtr.Zero,screen);}
    }
}
