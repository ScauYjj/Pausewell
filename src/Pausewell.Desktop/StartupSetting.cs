using Microsoft.Win32;
namespace Pausewell.Desktop;
internal static class StartupSetting
{
    public static void Set(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);
        if(!enabled){key.DeleteValue("Pausewell",false);return;}
        var exe=Environment.ProcessPath??throw new InvalidOperationException("无法确定启动程序。");
        if(System.IO.Path.GetFileNameWithoutExtension(exe).Equals("dotnet",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("请使用发布后的 Pausewell.exe 设置开机启动。");
        key.SetValue("Pausewell","\""+exe+"\" --background");
    }
}
