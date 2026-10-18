using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Pausewell.Controls;

namespace Pausewell.Desktop;

internal static class CrossFlowVerification
{
    private static T Find<T>(Window window,string id) where T:DependencyObject{window.UpdateLayout();return SmokeVerification.Descendants<T>(window).Single(x=>AutomationProperties.GetAutomationId(x)==id);}
    private static void Click(Window window,string id)=>Find<Button>(window,id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Toggle(CheckBox input)=>((IToggleProvider)new CheckBoxAutomationPeer(input).GetPattern(PatternInterface.Toggle)).Toggle();
    public static async Task Run(DesktopController app,Action<string,bool,object?> verify,Func<Window,string,Task> shot)
    {
        if(!app.IsIsolated)throw new InvalidOperationException("跨页面验证仅使用隔离数据。");
        void Check(string name,bool passed)=>verify(name,passed,null);
        app.Main.Navigate("Today");var plan=app.Engine.State.Plans.First(p=>p.Enabled&&p.Schedule==ScheduleKind.Interval);
        Toggle(Find<CheckBox>(app.Main,"Enabled_"+plan.Id));Check("提醒开关辅助功能停用写入真实数据库",!app.Store.Load()!.Plans.Single(p=>p.Id==plan.Id).Enabled);
        app.Main.Navigate("Medicine");app.Main.Navigate("Today");Check("跨页面返回保持已保存的停用状态",Find<CheckBox>(app.Main,"Enabled_"+plan.Id).IsChecked==false);
        Toggle(Find<CheckBox>(app.Main,"Enabled_"+plan.Id));Check("提醒开关辅助功能重新启用恢复计时",app.Store.Load()!.Plans.Single(p=>p.Id==plan.Id).Enabled&&app.Engine.State.Plans.Single(p=>p.Id==plan.Id).NextDue>DateTimeOffset.UtcNow);
        var ended=app.Engine.State.Plans.First(p=>p.Schedule==ScheduleKind.Once&&!p.Enabled&&p.OnceAt<DateTimeOffset.UtcNow);Toggle(Find<CheckBox>(app.Main,"Enabled_"+ended.Id));
        Check("已结束一次性事项拒绝直接重启且开关回滚",!app.Store.Load()!.Plans.Single(p=>p.Id==ended.Id).Enabled&&Find<CheckBox>(app.Main,"Enabled_"+ended.Id).IsChecked==false);app.Main.ClearError();

        app.Main.Navigate("History");await Task.Delay(100);var from=Find<IsoDatePicker>(app.Main,"HistoryFrom");from.SetRawInput("2026-02-30");
        var preview=app.Preview(HabitKind.Water,ReminderStyle.Card);await Task.Delay(100);preview.CloseWithoutAction();await Task.Delay(100);
        Check("关闭提醒预览不重建记录筛选草稿",ReferenceEquals(from,Find<IsoDatePicker>(app.Main,"HistoryFrom"))&&from.Input!.Text=="2026-02-30");
        app.Main.Hide();app.ShowMain();await Task.Delay(100);Check("托盘恢复保留未应用的记录筛选",ReferenceEquals(from,Find<IsoDatePicker>(app.Main,"HistoryFrom"))&&from.Input!.Text=="2026-02-30");
        preview=app.Preview(HabitKind.Water,ReminderStyle.Card);app.ShowQueue();var queue=app.CurrentQueue!;await Task.Delay(100);
        Check("进入待办清单收起原提醒窗口且不误记完成",!preview.IsVisible&&app.OpenReminders.Count==0&&queue.IsVisible);Click(queue,"CloseQueue");await Task.Delay(100);
        Check("关闭待办清单保留主窗口筛选草稿",ReferenceEquals(from,Find<IsoDatePicker>(app.Main,"HistoryFrom"))&&from.Input!.Text=="2026-02-30");await shot(app.Main,"90-history-return");Click(app.Main,"ResetHistory");

        app.Main.ShowAppearance();app.Main.SelectTheme("rose");var sample=Find<TextBox>(app.Main,"ThemeSampleInput");sample.Focus();sample.SelectAll();var before=app.Store.Load()!.Preferences.Theme;
        preview=app.Preview(HabitKind.Water,ReminderStyle.Card);preview.CloseWithoutAction();app.Main.Hide();app.ShowMain();await Task.Delay(100);
        Check("提醒关闭与托盘返回保留外观控件及局部预览",ReferenceEquals(sample,Find<TextBox>(app.Main,"ThemeSampleInput"))&&app.Main.Resources.Contains("Theme.ambient")&&app.Store.Load()!.Preferences.Theme==before&&Theme.Current==before);app.Main.CancelSettings();
        var mode=new ModeWindow(app,true){Owner=app.Main};mode.Show();await Task.Delay(100);Toggle(mode.ScopeInput);
        Check("暂停范围的辅助功能操作同步说明文案",SmokeVerification.Descendants<TextBlock>(mode).Any(t=>t.Text=="普通提醒和服药提示都暂停，原有记录保留。"));mode.Close();

        app.CloseReminders();app.Engine.Defer(app.Engine.State.Occurrences.Where(o=>o.Status is OccurrenceStatus.Pending or OccurrenceStatus.Deferred).Select(o=>o.Id),DateTimeOffset.UtcNow,60);app.Engine.DismissMeetingSummary(DateTimeOffset.UtcNow);
        app.Main.Navigate("Today");var schedules=app.Engine.State.Plans.ToDictionary(p=>p.Id,p=>p.NextDue);var count=app.Engine.State.Occurrences.Count;
        var now=DateTimeOffset.UtcNow;var recovery=new ReminderPlan{Name="调度写入失败恢复（独立验证）",Kind=HabitKind.Other,Schedule=ScheduleKind.Once,OnceAt=now.AddSeconds(1),Style=ReminderStyle.Card};app.Engine.SavePlan(recovery,now);
        using(var writer=new IntegrationVerification.WriterLock(app.Store.FilePath))
        {
            var deadline=DateTimeOffset.UtcNow.AddSeconds(7);while(!app.SchedulingFaulted&&DateTimeOffset.UtcNow<deadline)await Task.Delay(100);
            Check("真实调度写入失败显示暂缓且不产生半条记录",app.SchedulingFaulted&&Find<Button>(app.Main,"RetryScheduling").IsVisible&&app.Store.Load()!.Occurrences.Count==count);
            Click(app.Main,"RetryScheduling");Check("故障未解除时重试仍保持可见错误和原计划",app.SchedulingFaulted&&app.Store.Load()!.Occurrences.Count==count);await shot(app.Main,"91-scheduler-fault");
        }
        app.Main.ClearError();Check("其他页面错误清理不掩盖调度故障",Find<Button>(app.Main,"RetryScheduling").IsVisible);
        Click(app.Main,"RetryScheduling");await Task.Delay(150);var reminder=app.OpenReminders.Single(w=>SmokeVerification.Descendants<TextBlock>(w).Any(t=>t.Text==recovery.Name));
        Check("解除写锁后重试恢复真实提醒且不平移其他计划",!app.SchedulingFaulted&&app.Engine.State.Occurrences.Single(o=>o.PlanId==recovery.Id).Status==OccurrenceStatus.Pending&&schedules.All(pair=>app.Engine.State.Plans.Single(p=>p.Id==pair.Key).NextDue==pair.Value));await shot(reminder,"92-scheduler-recovered");Click(reminder,"Complete_Other");
        await Task.Delay(1200);Check("恢复后的单次确认只写入一条完成记录",app.Store.Load()!.Occurrences.Count(o=>o.PlanId==recovery.Id)==1&&app.Engine.State.Occurrences.Single(o=>o.PlanId==recovery.Id).Status==OccurrenceStatus.Completed&&!app.Engine.State.Plans.Single(p=>p.Id==recovery.Id).Enabled&&app.OpenReminders.Count==0);
        Check("跨页面走查后 SQLite 完整",app.Store.IntegrityCheck()=="ok");
    }
}
