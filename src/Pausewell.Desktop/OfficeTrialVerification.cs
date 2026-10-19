using Microsoft.Win32;
using Pausewell.Controls;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Interop;

namespace Pausewell.Desktop;

internal static class OfficeTrialVerification
{
    [DllImport("user32.dll")]private static extern bool IsWindowEnabled(IntPtr handle);
    private static T Find<T>(Window window,string id) where T:DependencyObject{window.UpdateLayout();return SmokeVerification.Descendants<T>(window).Single(x=>AutomationProperties.GetAutomationId(x)==id);}
    private static void Click(Window window,string id)=>Find<Button>(window,id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Toggle(CheckBox box)=>((IToggleProvider)new CheckBoxAutomationPeer(box).GetPattern(PatternInterface.Toggle)).Toggle();
    private static async Task<T> WaitWindow<T>() where T:Window{for(var i=0;i<40;i++){var window=Application.Current.Windows.OfType<T>().FirstOrDefault(w=>w.IsVisible);if(window!=null){window.UpdateLayout();return window;}await Task.Delay(50);}throw new InvalidOperationException("未打开预期窗口 "+typeof(T).Name);}
    public static async Task Run(DesktopController app,Action<string,bool,object?> verify,Func<Window,string,Task> shot)
    {
        if(!app.IsIsolated)throw new InvalidOperationException("办公观测验证只允许隔离数据。");
        void Check(string name,bool passed)=>verify(name,passed,null);
        app.CloseReminders();app.Engine.Defer(app.Engine.State.Occurrences.Where(o=>o.Status is OccurrenceStatus.Pending or OccurrenceStatus.Deferred).Select(o=>o.Id),DateTimeOffset.UtcNow,60);
        var record=app.Engine.State.Occurrences.First(o=>o.Kind==HabitKind.Medication&&o.Status==OccurrenceStatus.NeedsReview);
        var snapshot=new Occurrence{Id=record.Id,PlanId=record.PlanId,PlanName=record.PlanName,Kind=record.Kind,Status=record.Status,DueAt=record.DueAt,Style=ReminderStyle.Fullscreen,Note=record.Note};
        var before=app.Engine.State.Occurrences.Select(o=>$"{o.Id}:{o.Status}:{o.ActualAt}:{o.ResolvedAt}").ToArray();
        var full=app.OpenItems([snapshot]);await Task.Delay(120);_=app.Main.Dispatcher.BeginInvoke(()=>Click(full,"ViewReminderHistory"));var history=await WaitWindow<ReminderHistoryWindow>();
        Check("全屏提醒内打开的历史窗口归属原提醒且置顶",history.Owner==full&&history.Topmost&&full.IsVisible&&!IsWindowEnabled(new WindowInteropHelper(full).Handle));
        var ids=SmokeVerification.Descendants<Button>(history).Select(b=>AutomationProperties.GetAutomationId(b)).Where(id=>id.StartsWith("ReminderHistory_")).Order().ToArray();
        Check("提醒内历史只包含当前计划的记录",ids.SequenceEqual(app.Engine.State.Occurrences.Where(o=>o.PlanId==record.PlanId).Take(100).Select(o=>"ReminderHistory_"+o.Id).Order()));await shot(history,"100-fullscreen-history");
        _=app.Main.Dispatcher.BeginInvoke(()=>Click(history,"ReminderHistory_"+record.Id));var details=await WaitWindow<RecordWindow>();
        Check("提醒内记录详情只读且保持拥有者层级",details.Owner==history&&details.Topmost&&!SmokeVerification.Descendants<Button>(details).Any(b=>AutomationProperties.GetAutomationId(b) is "CorrectRecord" or "HandleRecord"));await shot(details,"101-history-readonly");Click(details,"CloseRecord");Click(history,"ReturnToReminder");await Task.Delay(100);
        Check("返回历史后原全屏提醒可继续且未更改记录",full.IsVisible&&IsWindowEnabled(new WindowInteropHelper(full).Handle)&&before.SequenceEqual(app.Engine.State.Occurrences.Select(o=>$"{o.Id}:{o.Status}:{o.ActualAt}:{o.ResolvedAt}")));full.CloseWithoutAction();
        var preview=app.Preview(HabitKind.Medication,ReminderStyle.Card);_=app.Main.Dispatcher.BeginInvoke(()=>Click(preview,"ViewReminderHistory"));history=await WaitWindow<ReminderHistoryWindow>();
        Check("提醒预览的历史窗口不读取真实计划记录",!SmokeVerification.Descendants<Button>(history).Any(b=>AutomationProperties.GetAutomationId(b).StartsWith("ReminderHistory_")));Click(history,"ReturnToReminder");preview.CloseWithoutAction();
        var confirmation=new MedicineConfirmation(app,record,true,()=>{}){Owner=app.Main};confirmation.Show();await Task.Delay(80);Toggle(confirmation.UnknownTime);
        Check("辅助功能选择未知服用时间同步禁用输入",!confirmation.ActualDate.IsEnabled&&!confirmation.ActualTime.IsEnabled);confirmation.Close();
        var editable=new RecordWindow(app,record.Id){Owner=app.Main};editable.Show();Click(editable,"CorrectRecord");editable.UpdateLayout();editable.StatusInput!.SelectedIndex=0;Toggle(editable.UnknownTime!);
        Check("辅助功能改变更正中的未知时间同步输入状态",editable.ActualDate!.IsEnabled==!(editable.UnknownTime!.IsChecked==true)&&editable.ActualTime!.IsEnabled==editable.ActualDate.IsEnabled);Click(editable,"CancelCorrection");editable.Close();

        // Future background habits can legitimately fire during these two-second lock fixtures.
        // Pause those fixtures so the assertions observe only the once-only reminder under test.
        var backgroundHabits=app.Engine.State.Plans.Where(p=>p.Enabled&&p.IsHabit).Select(p=>p.Id).ToArray();
        foreach(var id in backgroundHabits)app.Engine.SetEnabled(id,false,DateTimeOffset.UtcNow);
        foreach(var power in new[]{false,true})
        {
            var now=DateTimeOffset.UtcNow;var plan=new ReminderPlan{Name=power?"合成电源恢复验证":"合成会话恢复验证",Kind=HabitKind.Other,Schedule=ScheduleKind.Once,OnceAt=now.AddSeconds(2),Style=ReminderStyle.Card};app.Engine.SavePlan(plan,now);
            if(power)app.HandlePower(PowerModes.Suspend,true);else app.HandleSession(SessionSwitchReason.SessionLock,true);
            await Task.Delay(2300);Check((power?"合成挂起":"合成锁屏")+"处理期间不生成或展示新提醒",!app.Engine.State.Occurrences.Any(o=>o.PlanId==plan.Id)&&app.OpenReminders.Count==0);
            if(power)app.HandlePower(PowerModes.Resume,true);else app.HandleSession(SessionSwitchReason.SessionUnlock,true);
            await Task.Delay(150);Check("系统返回只呈现摘要，旧待办不自动弹窗",app.OpenReminders.Count==0&&app.Engine.State.RecoverySummaryVisible);
            var live=app.OpenItems([app.Engine.State.Occurrences.Single(o=>o.PlanId==plan.Id)]);Check((power?"合成电源":"合成会话")+"恢复保留一次性原时刻与未完成状态",app.Engine.State.Occurrences.Single(o=>o.PlanId==plan.Id).DueAt==plan.OnceAt&&app.Engine.State.Occurrences.Single(o=>o.PlanId==plan.Id).Status==OccurrenceStatus.Pending);Click(live,"Complete_Other");
        }
        var overlapNow=DateTimeOffset.UtcNow;var overlap=new ReminderPlan{Name="合成锁屏与睡眠重叠验证",Kind=HabitKind.Other,Schedule=ScheduleKind.Once,OnceAt=overlapNow.AddSeconds(2),Style=ReminderStyle.Card};app.Engine.SavePlan(overlap,overlapNow);
        app.HandleSession(SessionSwitchReason.SessionLock,true);app.HandlePower(PowerModes.Suspend,true);app.HandlePower(PowerModes.Resume,true);await Task.Delay(2300);
        Check("合成睡眠恢复不会解除仍然存在的锁屏状态",app.OpenReminders.Count==0&&!app.Engine.State.Occurrences.Any(o=>o.PlanId==overlap.Id));
        app.HandleSession(SessionSwitchReason.SessionUnlock,true);await Task.Delay(150);Check("重叠系统返回不自动补弹旧待办",app.OpenReminders.Count==0);
        var resumed=app.OpenItems([app.Engine.State.Occurrences.Single(o=>o.PlanId==overlap.Id)]);Check("合成锁屏和睡眠都结束后恢复同一原时刻",app.Engine.State.Occurrences.Single(o=>o.PlanId==overlap.Id).DueAt==overlap.OnceAt);Click(resumed,"Complete_Other");
        foreach(var id in backgroundHabits)app.Engine.SetEnabled(id,true,DateTimeOffset.UtcNow);
        Check("试用诊断启用并包含明确标注的合成信号",app.Diagnostics.Enabled&&File.ReadLines(app.Diagnostics.CurrentPath).Select(line=>JsonDocument.Parse(line)).Count(d=>d.RootElement.GetProperty("fields").TryGetProperty("origin",out var origin)&&origin.GetString()=="verification")==8);
        using(var held=new FileStream(app.Diagnostics.CurrentPath,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            var sampled=app.Diagnostics.Sample(1,1,1);preview=app.Preview(HabitKind.Water,ReminderStyle.Card);
            Check("日志写入被占用不影响提醒窗口或调度状态",!sampled&&app.Diagnostics.LastWriteFailed&&preview.IsVisible&&!app.SchedulingFaulted);preview.CloseWithoutAction();
        }
        Check("日志释放占用后可继续记录",app.Diagnostics.Sample(app.Engine.State.Plans.Count,app.Engine.State.Occurrences.Count,0)&&!app.Diagnostics.LastWriteFailed);
        var forbidden=new[]{"PlanName","Name","Note","Reason","message","dataDirectory","药物","药名"};
        var documents=File.ReadLines(app.Diagnostics.CurrentPath).Select(line=>JsonDocument.Parse(line)).ToArray();
        Check("本地诊断字段不包含事项药名备注或异常原文",documents.All(d=>d.RootElement.GetProperty("fields").EnumerateObject().All(p=>!forbidden.Contains(p.Name,StringComparer.OrdinalIgnoreCase)))&&!documents.SelectMany(d=>d.RootElement.GetProperty("fields").EnumerateObject()).Where(p=>p.Value.ValueKind==JsonValueKind.String).Any(p=>p.Value.GetString()!.Contains(record.PlanName)));foreach(var document in documents)document.Dispose();
        var rotation=new LocalDiagnostics(Path.Combine(app.DataDirectory,"retention-check"),true,768);for(var i=0;i<30;i++)rotation.Sample(i,i,0);
        Check("本地诊断滚动保留且磁盘占用有界",File.Exists(rotation.PreviousPath)&&Directory.GetFiles(rotation.DirectoryPath).Length==2&&Directory.GetFiles(rotation.DirectoryPath).Sum(f=>new FileInfo(f).Length)<2048);
        var off=new LocalDiagnostics(Path.Combine(app.DataDirectory,"disabled-check"),false);off.Sample(0,0,0);Check("未启用诊断时不创建日志目录",!Directory.Exists(off.DirectoryPath));
        var dpi=VisualTreeHelper.GetDpi(app.Main);var profile=new{capturedAt=DateTimeOffset.UtcNow,screens=System.Windows.Forms.Screen.AllScreens.Select(s=>new{s.Primary,boundsPixels=new{s.Bounds.X,s.Bounds.Y,s.Bounds.Width,s.Bounds.Height}}).ToArray(),dpiX=dpi.PixelsPerInchX,dpiY=dpi.PixelsPerInchY,renderTier=RenderCapability.Tier>>16,highContrast=SystemParameters.HighContrast,workAreaDip=new{SystemParameters.WorkArea.Width,SystemParameters.WorkArea.Height},realLockTested=false,realSleepTested=false};
        File.WriteAllText(Path.Combine(app.DataDirectory,"device-profile.json"),JsonSerializer.Serialize(profile,new JsonSerializerOptions{WriteIndented=true}));Check("设备档案使用当前实际DPI和显示器信息",dpi.PixelsPerInchX>0&&System.Windows.Forms.Screen.AllScreens.Length>0);
        Check("办公观测专项后数据库完整",app.Store.IntegrityCheck()=="ok");
    }
}
