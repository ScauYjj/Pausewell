using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pausewell.Desktop;

// Only these typed methods can add fields. Names, notes and exception messages
// are deliberately absent from the local diagnostic format.
public sealed class LocalDiagnostics
{
    private readonly long _limit;
    private readonly object _gate=new();
    private readonly string _run=Guid.NewGuid().ToString("N");
    private static readonly JsonSerializerOptions Json=new(){DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull};
    public bool Enabled {get;}
    public bool LastWriteFailed {get;private set;}
    public string DirectoryPath {get;}
    public string CurrentPath=>Path.Combine(DirectoryPath,"runtime.jsonl");
    public string PreviousPath=>Path.Combine(DirectoryPath,"runtime.previous.jsonl");
    internal LocalDiagnostics(string directory,bool enabled,long limit=1024*1024){DirectoryPath=Path.GetFullPath(directory);Enabled=enabled;_limit=limit;}
    private bool Write(string eventName,object fields)
    {
        if(!Enabled)return false;
        lock(_gate)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                var line=JsonSerializer.Serialize(new{utc=DateTimeOffset.UtcNow,run=_run,eventName,fields},Json)+"\n";var bytes=Encoding.UTF8.GetBytes(line);
                if(bytes.Length>_limit){LastWriteFailed=true;return false;}
                if(File.Exists(CurrentPath)&&new FileInfo(CurrentPath).Length+bytes.Length>_limit)File.Move(CurrentPath,PreviousPath,true);
                using var stream=new FileStream(CurrentPath,FileMode.Append,FileAccess.Write,FileShare.Read);stream.Write(bytes);LastWriteFailed=false;return true;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException){LastWriteFailed=true;return false;}
        }
    }
    public bool Start(Window window)
    {
        var dpi=VisualTreeHelper.GetDpi(window);return Write("start",new{version=typeof(App).Assembly.GetName().Version?.ToString(),os=Environment.OSVersion.Version.ToString(),screens=System.Windows.Forms.Screen.AllScreens.Length,dpiX=dpi.PixelsPerInchX,dpiY=dpi.PixelsPerInchY,renderTier=RenderCapability.Tier>>16,highContrast=SystemParameters.HighContrast,workWidth=SystemParameters.WorkArea.Width,workHeight=SystemParameters.WorkArea.Height});
    }
    public bool Sample(int plans,int occurrences,int windows)
    {
        if(!Enabled)return false;
        try{using var process=Process.GetCurrentProcess();return Write("sample",new{plans,occurrences,windows,privateBytes=process.PrivateMemorySize64,workingSetBytes=process.WorkingSet64,cpuSeconds=process.TotalProcessorTime.TotalSeconds});}
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException){LastWriteFailed=true;return false;}
    }
    public bool Session(bool locked,bool verification=false)=>Write(locked?"session_lock":"session_unlock",new{origin=verification?"verification":"os"});
    public bool Power(bool suspended,bool verification=false)=>Write(suspended?"suspend":"resume",new{origin=verification?"verification":"os"});
    public bool SchedulerFault(Exception error)=>Write("scheduler_fault",new{type=error.GetType().Name});
    public bool SchedulerRetry(bool success)=>Write("scheduler_retry",new{success});
    public void Shown(IEnumerable<Occurrence> items,bool preview)
    {
        foreach(var item in items)Write(preview?"preview_shown":"reminder_shown",new{occurrence=preview?null:item.Id,dueUtc=preview?(DateTimeOffset?)null:item.DueAt,style=item.Style.ToString(),delaySeconds=preview?(double?)null:Math.Max(0,(DateTimeOffset.UtcNow-item.DueAt).TotalSeconds)});
    }
    public bool Resolved(string id,OccurrenceStatus status)=>Write("reminder_resolved",new{occurrence=id,status=status.ToString()});
    public bool Deferred(int count)=>Write("reminder_deferred",new{count});
    public bool Exit()=>Write("exit",new{normal=true});
}
