using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Media.Imaging;

namespace Pausewell.Desktop;

public static class SmokeVerification
{
    public static void Capture(FrameworkElement visual,string path)
    {
        visual.UpdateLayout();
        visual.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        Pausewell.ComponentLab.NativeCapture.Save(visual,path);
    }
    public static IEnumerable<T> Descendants<T>(DependencyObject parent) where T:DependencyObject
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T typed)yield return typed;foreach(var nested in Descendants<T>(child))yield return nested;}}
    private static Button ButtonById(Window window,string id){window.UpdateLayout();return Descendants<Button>(window).Single(b=>AutomationProperties.GetAutomationId(b)==id);}
    private static TextBox InputById(Window window,string id){window.UpdateLayout();return Descendants<TextBox>(window).Single(b=>AutomationProperties.GetAutomationId(b)==id);}
    private static void Click(Button b){if(!b.IsEnabled)throw new Exception("按钮未启用："+b.Content);b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    public static async Task Run(DesktopController app,string directory)
    {
        Directory.CreateDirectory(directory);var checks=new List<object>();var started=DateTimeOffset.UtcNow;var success=true;
        void Verify(string name,bool condition,object? detail=null){checks.Add(new{name,passed=condition,detail});if(!condition)throw new Exception(name);}
        async Task Shot(Window w,string name){await Task.Delay(150);Capture((FrameworkElement)w.Content,Path.Combine(directory,name+".png"));}
        try
        {
            await Task.Delay(500);Verify("首次无自动建立的计划，也不启动空调度",app.Engine.State.Plans.Count==0&&!app.Engine.State.Running);await Shot(app.Main,"01-first-run");
            var editor=new PlanEditor(app,null){Owner=app.Main};editor.Show();await Task.Delay(200);Verify("初次表单不提前报错",editor.ValidationMessage.Length==0);editor.NameInput.Text="";Click(editor.SaveButton);Verify("无效提交不保存且解释原因",!editor.Saved&&editor.ValidationMessage.Length>0&&app.Engine.State.Plans.Count==0);editor.NameInput.Text="喝水";editor.IntervalInput.Text="30";Verify("修正有效字段后清除过期校验提示",editor.ValidationMessage.Length==0);await Shot(editor,"02-editor");Click(editor.SaveButton);Verify("首次保存开始提醒并持久化",editor.Saved&&app.Engine.State.Running&&app.Store.Load()!.Plans.Count==1);
            var water=app.Engine.State.Plans.Single();var nextWater=water.NextDue;
            var activityEditor=new PlanEditor(app,null,HabitKind.Activity){Owner=app.Main};activityEditor.Show();activityEditor.NameInput.Text="起来活动";activityEditor.IntervalInput.Text="45";Click(activityEditor.SaveButton);Verify("新增不重置既有普通计划",app.Engine.State.Plans.Single(p=>p.Id==water.Id).NextDue==nextWater);
            var toilet=new PlanEditor(app,null,HabitKind.Toilet){Owner=app.Main};toilet.Show();toilet.NameInput.Text="如厕提醒";toilet.IntervalInput.Text="60";Click(toilet.SaveButton);
            var drugEditor=new PlanEditor(app,null,HabitKind.Medication){Owner=app.Main};drugEditor.Show();await Task.Delay(150);Verify("药物时刻初始为空且标签可访问",InputById(drugEditor,"MedicineTime0").Text==""&&AutomationProperties.GetName(InputById(drugEditor,"MedicineTime0")).Length>0);drugEditor.NameInput.Text="药物 A（示例）";drugEditor.TimeFields.SetCount(3);foreach(var (i,time) in new[]{(0,"08:00"),(1,"13:00"),(2,"20:00")})InputById(drugEditor,"MedicineTime"+i).Text=time;await Shot(drugEditor,"03-medicine-editor");Click(drugEditor.SaveButton);Verify("保存独立药物与明确时刻",drugEditor.Saved&&app.Engine.State.Plans.Single(p=>p.Kind==HabitKind.Medication).Times.Count==3);
            app.Main.Refresh();app.Main.Width=1004;app.Main.Height=866;await Task.Delay(100);await Shot(app.Main,"04-home");app.Main.Width=1200;app.Main.Height=860;app.Main.Navigate("Medicine");await Shot(app.Main,"05-medicine-list");
            await IntegrationVerification.Run(app,(name,passed,detail)=>Verify(name,passed,detail),Shot);
            await ThemeVerification.Run(app,(name,passed,detail)=>Verify(name,passed,detail),Shot);
            app.Main.ShowAppearance();app.Main.SelectTheme("violet");Verify("外观预览不发布全局主题",Theme.Current=="champagne"&&app.Engine.State.Preferences.Theme=="champagne");app.Main.CancelSettings();Verify("取消外观恢复原主题",!app.Main.Resources.Contains("GlassBrush")&&app.Engine.State.Preferences.Theme=="champagne");
            foreach(var theme in Theme.All){app.Main.SelectTheme(theme.Id);await Task.Delay(60);await Shot(app.Main,"06-theme-"+theme.Id);}app.Main.SaveUserSettings();Verify("保存主题写入 SQLite 并应用全局",app.Store.Load()!.Preferences.Theme=="blue"&&Theme.Current=="blue");app.Main.SelectTheme("champagne");app.Main.SaveUserSettings();await Shot(app.Main,"07-appearance-saved");
            var mode=new ModeWindow(app,false){Owner=app.Main};mode.Show();await Shot(mode,"08-meeting-config");Click(ButtonById(mode,"ApplyMode"));Verify("会议具备明确期限且调度继续运行",app.Engine.State.MeetingUntil>DateTimeOffset.UtcNow&&app.Engine.State.Running);await Shot(app.Main,"09-meeting-active");
            var during=new PlanEditor(app,null){Owner=app.Main};during.Show();during.NameInput.Text="会中新增";Verify("会中新增说明保持会议",(string)during.SaveButton.Content=="保存，保持会议模式");Click(during.SaveButton);Verify("会中新增不结束会议",app.Engine.State.MeetingUntil!=null);
            app.Engine.BeginMeeting(DateTimeOffset.UtcNow.AddSeconds(2),DateTimeOffset.UtcNow);await Task.Delay(3300);Verify("实际调度器自动结束会议进入缓冲",app.Engine.State.MeetingUntil==null&&app.Engine.State.BufferUntil>DateTimeOffset.UtcNow);
            var duringPauseMedicine=app.Preview(HabitKind.Medication,ReminderStyle.Card);var pause=new ModeWindow(app,true){Owner=app.Main};pause.Show();await Shot(pause,"10-pause-config");Click(ButtonById(pause,"ApplyMode"));Verify("普通暂停期限不停止药物",app.Engine.State.PauseUntil!=null&&!app.Engine.State.PauseMedication&&app.Engine.State.Running);Verify("只暂停普通提醒时保留已开的药物窗口",duringPauseMedicine.IsVisible);duringPauseMedicine.CloseWithoutAction();app.Engine.BeginPause(DateTimeOffset.UtcNow.AddSeconds(2),false,DateTimeOffset.UtcNow);await Task.Delay(3300);Verify("暂停自动恢复并重新计算普通间隔",app.Engine.State.PauseUntil==null&&app.Engine.State.Plans.Single(p=>p.Id==water.Id).NextDue>DateTimeOffset.UtcNow.AddMinutes(29));
            var activity=app.Preview(HabitKind.Activity,ReminderStyle.Card);await Task.Delay(150);await Shot(activity,"11-activity");var complete=ButtonById(activity,"Complete_Activity");Verify("活动未计时允许本人明确确认",complete.IsEnabled);Click(ButtonById(activity,"StartActivity"));app.Main.ShowAppearance();app.Main.SelectTheme("violet");app.Main.SaveUserSettings();Verify("换肤不重建已打开的提醒和活动控件",app.OpenReminders.Contains(activity)&&complete.IsEnabled);await Task.Delay(5300);Verify("真实活动计时结束仍等待明确确认",complete.IsEnabled&&activity.IsVisible);Click(complete);Verify("活动预览不写入记录",app.Engine.State.Occurrences.Count==0);app.Main.SelectTheme("champagne");app.Main.SaveUserSettings();
            var drug=app.Preview(HabitKind.Medication,ReminderStyle.Card);await Task.Delay(100);await Shot(drug,"12-medicine-reminder");_ = app.Main.Dispatcher.BeginInvoke(()=>Click(ButtonById(drug,"Complete_Medication")));await Task.Delay(180);var confirmation=Application.Current.Windows.OfType<MedicineConfirmation>().Single();await Shot(confirmation,"13-medicine-confirm");Click(ButtonById(confirmation,"ConfirmMedicineRecord"));Verify("药物预览二次确认不写入记录",app.Engine.State.Occurrences.Count==0&&app.OpenReminders.Count==0);
            var card=app.Preview(HabitKind.Water,ReminderStyle.Card);await Task.Delay(120);await Shot(card,"14-water-reminder");Click(ButtonById(card,"Complete_Water"));
            app.Engine.Preferences(true,false,DateTimeOffset.UtcNow);var full=app.Preview(HabitKind.Activity,ReminderStyle.Fullscreen);await Task.Delay(300);Verify("全屏覆盖与多屏遮罩数量",full.Topmost&&full.Strong&&full.MaskCount==System.Windows.Forms.Screen.AllScreens.Length-1,NativeWindows.Bounds(full));await Shot(full,"15-fullscreen");full.Close();Verify("全屏普通关闭不误记完成",full.IsVisible);full.BeginEmergencyHold();await Task.Delay(2300);Verify("长按紧急退出可用",!full.IsVisible);
            var now=DateTimeOffset.UtcNow;var local=app.Engine.Local(now);var nextMinute=new DateTimeOffset(local.Year,local.Month,local.Day,local.Hour,local.Minute,0,local.Offset).AddMinutes(1);app.Engine.SavePlan(new(){Name="真实到期验证",Kind=HabitKind.Water,Schedule=ScheduleKind.FixedTimes,Times=[nextMinute.ToString("HH:mm")],Style=ReminderStyle.Card},now);
            app.Engine.SavePlan(new(){Name="测试药物（独立数据）",Kind=HabitKind.Medication,Schedule=ScheduleKind.FixedTimes,Times=[nextMinute.ToString("HH:mm")],Style=ReminderStyle.Card},now);
            var timeout=now.AddSeconds(76);while(app.OpenReminders.Count==0&&DateTimeOffset.UtcNow<timeout)await Task.Delay(250);var live=app.OpenReminders.FirstOrDefault();Verify("真实时钟到期由调度器展示窗口",live!=null&&!live.IsPreview,new{intendedAt=nextMinute,observedAt=DateTimeOffset.UtcNow});await Shot(live!,"16-live-medicine");
            _ = app.Main.Dispatcher.BeginInvoke(()=>Click(ButtonById(live!,"Complete_Medication")));await Task.Delay(180);var realConfirm=Application.Current.Windows.OfType<MedicineConfirmation>().Single();Click(ButtonById(realConfirm,"ConfirmMedicineRecord"));var savedDrug=app.Store.Load()!.Occurrences.Single(o=>o.Kind==HabitKind.Medication&&o.Status==OccurrenceStatus.Completed);Verify("实际服药记录分别保存实际与确认时间",savedDrug.ActualAt!=null&&savedDrug.ResolvedAt!=null);
            var waterTimeout=DateTimeOffset.UtcNow.AddSeconds(5);while(app.OpenReminders.Count==0&&DateTimeOffset.UtcNow<waterTimeout)await Task.Delay(150);var liveWater=app.OpenReminders.FirstOrDefault();Verify("同一时刻普通事项仍独立处理",liveWater!=null&&!liveWater.IsPreview);await Shot(liveWater!,"16-live-reminder");Click(ButtonById(liveWater!,"Complete_Water"));Verify("真实界面确认提交 SQLite",app.Store.Load()!.Occurrences.Count(o=>o.Status==OccurrenceStatus.Completed)==2);
            app.Main.Navigate("History");await Shot(app.Main,"17-history");var record=app.Engine.State.Occurrences.Single(o=>o.Kind==HabitKind.Water&&o.Status==OccurrenceStatus.Completed);var detail=new RecordWindow(app,record.Id){Owner=app.Main};detail.Show();await Shot(detail,"18-record-detail");Click(ButtonById(detail,"CorrectRecord"));await Shot(detail,"19-record-correction");Click(ButtonById(detail,"SaveCorrection"));Verify("更正无原因不保存",app.Engine.State.Occurrences.Single(o=>o.Id==record.Id).Corrections.Count==0);InputById(detail,"CorrectionReason").Text="核对后修正实际时间";Click(ButtonById(detail,"SaveCorrection"));Verify("更正保留修改历史",app.Engine.State.Occurrences.Single(o=>o.Id==record.Id).Corrections.Count==1);detail.Close();
            var queueNow=DateTimeOffset.UtcNow;app.Engine.BeginMeeting(queueNow.AddMinutes(1),queueNow);var queuePlan=new ReminderPlan{Name="会后汇总验证",Kind=HabitKind.Water,Schedule=ScheduleKind.Interval,IntervalMinutes=1};app.Engine.SavePlan(queuePlan,queueNow.AddMinutes(-2));app.Engine.Evaluate(queueNow);app.Engine.EndMeeting(queueNow.AddSeconds(-31));await Task.Delay(1400);Verify("会后使用内联摘要，不自动弹出清单",app.CurrentQueue==null&&app.Engine.State.RecoverySummaryVisible);await Shot(app.Main,"22-meeting-summary");Verify("会议中的过时习惯结束且未误记完成",app.Engine.State.Occurrences.Single(o=>o.PlanId==queuePlan.Id).Status==OccurrenceStatus.Expired&&app.OpenReminders.Count==0);
            app.Main.OpenQueue();var manualQueue=app.CurrentQueue;app.Main.OpenQueue();await Task.Delay(1100);Verify("手动清单复用同一窗口并阻止重复弹窗",manualQueue!=null&&app.CurrentQueue==manualQueue&&app.OpenReminders.Count==0);Click(ButtonById(manualQueue!,"CloseQueue"));
            var silentNow=DateTimeOffset.UtcNow;app.Engine.BeginMeeting(silentNow.AddMinutes(5),silentNow,true);app.Main.Navigate("Today");await Task.Delay(100);var previousEnd=app.Engine.State.MeetingUntil;Click(ButtonById(app.Main,"ExtendMode"));Verify("延长会议保留完全静音选择",app.Engine.State.MeetingSilent&&app.Engine.State.MeetingUntil==previousEnd!.Value.AddMinutes(15));app.Engine.EndMeeting(DateTimeOffset.UtcNow);
            var backup=app.Store.Backup();using(var reopened=new Pausewell.Storage.SqliteStateStore(backup))Verify("一致性备份可重开且包含记录",reopened.IntegrityCheck()=="ok"&&reopened.Load()!.Occurrences.Count==app.Engine.State.Occurrences.Count);
            app.Main.Navigate("Settings");await Shot(app.Main,"20-general-settings");app.Main.Navigate("Today");app.Main.Width=900;app.Main.Height=650;await Task.Delay(150);await Shot(app.Main,"21-compact-home");Verify("紧凑窗口内容可滚动，导航保留",app.Main.IsVisible&&ButtonById(app.Main,"NavSettings").ActualWidth>0);app.Main.Width=1200;app.Main.Height=860;
            app.Engine.Checkpoint(DateTimeOffset.UtcNow);var restarted=new ReminderEngine(app.Store,DateTimeOffset.UtcNow);restarted.ResumeOnLaunch(DateTimeOffset.UtcNow);Verify("重开恢复提醒与已保存主题",restarted.State.Running&&restarted.State.Preferences.Theme=="champagne");Verify("数据库完整性",app.Store.IntegrityCheck()=="ok");app.Main.Hide();app.ShowMain();Verify("托盘同源恢复窗口",app.Main.IsVisible);
            await InputOnceVerification.Run(app,(name,passed,detail)=>Verify(name,passed,detail),Shot);
            await TaskWindowVerification.Run(app,(name,passed,detail)=>Verify(name,passed,detail),Shot);
            await HistorySettingsVerification.Run(app,(name,passed,detail)=>Verify(name,passed,detail),Shot);
            await CrossFlowVerification.Run(app,(name,passed,detail)=>Verify(name,passed,detail),Shot);
            await OfficeTrialVerification.Run(app,(name,passed,detail)=>Verify(name,passed,detail),Shot);
            await LifecycleVerification.Run(app,(name,passed,detail)=>Verify(name,passed,detail),Shot);
        }
        catch(Exception ex){success=false;checks.Add(new{name="异常",passed=false,error=ex.ToString()});}
        finally
        {
            File.WriteAllText(Path.Combine(directory,"gui-test-results.json"),JsonSerializer.Serialize(new{success,startedAt=started,finishedAt=DateTimeOffset.UtcNow,screens=System.Windows.Forms.Screen.AllScreens.Length,checks,note="独立数据。真实 WPF 控件、计时器、SQLite 与一次实际时钟到期；不代表真实锁屏、睡眠、投屏或全部设备已验收。"},new JsonSerializerOptions{WriteIndented=true}));app.Quit(success?0:1);
        }
    }
}
