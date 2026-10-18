using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Pausewell.ComponentLab;
internal static class NativeInput
{
    [StructLayout(LayoutKind.Sequential)]private struct Keyboard {public ushort Key,Scan;public uint Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)]private struct Mouse {public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Explicit)]private struct Union {[FieldOffset(0)]public Keyboard Keyboard;[FieldOffset(0)]public Mouse Mouse;}
    [StructLayout(LayoutKind.Sequential)]private struct Input {public uint Type;public Union Data;}
    [DllImport("user32.dll")]private static extern uint SendInput(uint count,Input[] inputs,int size);
    [DllImport("user32.dll")]private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
    [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]private static extern bool AttachThreadInput(uint first,uint second,bool attach);
    [DllImport("user32.dll")]private static extern bool BringWindowToTop(IntPtr window);
    [DllImport("user32.dll")]private static extern bool SetCursorPos(int x,int y);
    [StructLayout(LayoutKind.Sequential)]private struct CursorPoint {public int X,Y;}
    [DllImport("user32.dll")]private static extern bool GetCursorPos(out CursorPoint point);
    private static bool _mouseHeld;
    public static Point CursorPosition {get{GetCursorPos(out var p);return new(p.X,p.Y);}}
    [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr window);
    public static bool Activate(Window window)
    {
        var h=new WindowInteropHelper(window).Handle;var thread=GetWindowThreadProcessId(GetForegroundWindow(),out var process);if(process==Environment.ProcessId)return true;
        var current=GetCurrentThreadId();var attached=thread!=0&&thread!=current&&AttachThreadInput(current,thread,true);
        try{BringWindowToTop(h);SetForegroundWindow(h);window.Activate();}finally{if(attached)AttachThreadInput(current,thread,false);}
        GetWindowThreadProcessId(GetForegroundWindow(),out process);return process==Environment.ProcessId;
    }
    public static void Move(Point p)=>SetCursorPos((int)p.X,(int)p.Y);
    private static void Send(params Input[] input){GetWindowThreadProcessId(GetForegroundWindow(),out var process);if(process!=Environment.ProcessId)throw new InvalidOperationException("样板失去前台，未向其他应用发送输入。");DirectSend(input);}
    private static void DirectSend(params Input[] input){if(SendInput((uint)input.Length,input,Marshal.SizeOf<Input>())!=input.Length)throw new InvalidOperationException("Windows 未接受样板输入事件。");}
    public static void Key(ushort key)=>Send(new(){Type=1,Data=new(){Keyboard=new(){Key=key}}},new(){Type=1,Data=new(){Keyboard=new(){Key=key,Flags=2}}});
    public static void Text(string text){foreach(var c in text)Send(new(){Type=1,Data=new(){Keyboard=new(){Scan=c,Flags=4}}},new(){Type=1,Data=new(){Keyboard=new(){Scan=c,Flags=6}}});}
    public static void Down(){Send(new Input(){Data=new(){Mouse=new(){Flags=2}}});_mouseHeld=true;}
    public static void Up(){if(!_mouseHeld)return;DirectSend(new Input(){Data=new(){Mouse=new(){Flags=4}}});_mouseHeld=false;}
    public static void Wheel(int delta)=>Send(new Input(){Data=new(){Mouse=new(){Flags=0x800,Data=unchecked((uint)delta)}}});
    public static async Task Click(FrameworkElement target){Move(target.PointToScreen(new Point(target.ActualWidth/2,target.ActualHeight/2)));await Task.Delay(60);Down();Up();await Task.Delay(100);}
}
