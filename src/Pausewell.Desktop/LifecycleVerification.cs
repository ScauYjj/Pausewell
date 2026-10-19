using System.Windows.Automation;

namespace Pausewell.Desktop;

internal static class LifecycleVerification
{
    static Button Find(Window w,string id){w.UpdateLayout();return SmokeVerification.Descendants<Button>(w).Single(b=>AutomationProperties.GetAutomationId(b)==id);}
    static void Click(Button b)=>b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static async Task<T> Wait<T>() where T:Window
    {
        for(var i=0;i<40;i++){var w=Application.Current.Windows.OfType<T>().FirstOrDefault();if(w!=null)return w;await Task.Delay(50);}
        throw new Exception("未出现预期窗口："+typeof(T).Name);
    }
    public static async Task Run(DesktopController app,Action<string,bool,object?> verify,Func<Window,string,Task> shot)
    {
        if(!app.IsIsolated)throw new InvalidOperationException("场景验收只能使用隔离数据。");
        void Check(string name,bool ok)=>verify(name,ok,null);
        var now=DateTimeOffset.UtcNow;app.CloseReminders();app.CurrentQueue?.Close();app.Engine.BeginMeeting(now.AddMinutes(30),now,true);
        foreach(var kind in new[]{HabitKind.Water,HabitKind.Other,HabitKind.Medication})
        {
            now=DateTimeOffset.UtcNow;
            var plan=kind==HabitKind.Medication?new ReminderPlan{Name="删除验证·药物",Kind=kind,Schedule=ScheduleKind.FixedTimes,Times=[app.Engine.Local(now).ToString("HH:mm")]}:
                kind==HabitKind.Other?new ReminderPlan{Name="删除验证·待办",Kind=kind,Schedule=ScheduleKind.Once,OnceAt=now.AddSeconds(-3)}:
                new ReminderPlan{Name="删除验证·习惯",Kind=kind,Schedule=ScheduleKind.Interval,IntervalMinutes=1};
            var savedAt=kind==HabitKind.Water?now.AddMinutes(-1):now.AddMinutes(-2);
            app.Engine.SavePlan(plan,savedAt,true);app.Engine.Evaluate(savedAt);app.Engine.Evaluate(now);
            var record=app.Engine.State.Occurrences.Single(o=>o.PlanId==plan.Id);
            app.Main.Navigate(kind==HabitKind.Medication?"Medicine":"Today");
            var rowDelete=Find(app.Main,"Delete_"+plan.Id);rowDelete.BringIntoView();
            _=app.Main.Dispatcher.BeginInvoke(()=>Click(rowDelete));var dialog=await Wait<DeletePlanWindow>();
            Check(kind+"删除窗口明确说明保留记录",SmokeVerification.Descendants<TextBlock>(dialog).Any(t=>t.Text.Contains("保留")));
            await shot(dialog,"110-delete-"+kind);Click(dialog.CancelButton);
            Check(kind+"取消删除保留计划与原结果",app.Engine.State.Plans.Any(p=>p.Id==plan.Id)&&app.Engine.State.Occurrences.Single(o=>o.Id==record.Id).Status==OccurrenceStatus.Pending);
            _=app.Main.Dispatcher.BeginInvoke(()=>Click(Find(app.Main,"Delete_"+plan.Id)));dialog=await Wait<DeletePlanWindow>();
            using(var writer=new IntegrationVerification.WriterLock(app.Store.FilePath))Click(dialog.DeleteButton);
            Check(kind+"删除写入失败保留窗口与计划",dialog.IsVisible&&!dialog.Deleted&&app.Engine.State.Plans.Any(p=>p.Id==plan.Id));
            Click(dialog.DeleteButton);
            Check(kind+"删除后列表消失且历史保留",dialog.Deleted&&!app.Engine.State.Plans.Any(p=>p.Id==plan.Id)&&app.Store.Load()!.DeletedPlans.Any(p=>p.Id==plan.Id)&&app.Engine.State.Occurrences.Any(o=>o.Id==record.Id));
            Check(kind+"删除没有误记完成",app.Engine.State.Occurrences.Single(o=>o.Id==record.Id).Status==(kind==HabitKind.Medication?OccurrenceStatus.NeedsReview:OccurrenceStatus.Cancelled));
            if(kind==HabitKind.Medication)
            {
                var reminder=app.OpenItems([app.Engine.State.Occurrences.Single(o=>o.Id==record.Id)]);
                Check("删除药物后仍可打开核对历史，且不会强制全屏",reminder.IsVisible&&!reminder.Strong);Click(Find(reminder,"MedicationUnsure"));
            }
        }
        now=DateTimeOffset.UtcNow;
        var activity=new ReminderPlan{Name="返回后的活动验证",Kind=HabitKind.Activity,Schedule=ScheduleKind.Interval,IntervalMinutes=30,Style=ReminderStyle.Gentle};
        app.Engine.SavePlan(activity,now.AddMinutes(-30),true);app.Engine.Evaluate(now);var item=app.Engine.State.Occurrences.Single(o=>o.PlanId==activity.Id);
        app.Engine.StartActivity(item.Id,now.AddSeconds(-45));app.Engine.MarkUnavailable(now);app.Engine.Recover(now.AddSeconds(1),RecoveryReason.SystemReturn);
        var window=app.OpenItems([app.Engine.State.Occurrences.Single(o=>o.Id==item.Id)]);await Task.Delay(120);
        Check("原生活动返回保留45秒且不自动开始",app.Engine.ActivityElapsed(item.Id,DateTimeOffset.UtcNow)==45&&app.Engine.State.Occurrences.Single(o=>o.Id==item.Id).Activity!.State==ActivityState.Interrupted);
        Check("原生活动允许已活动直接确认",Find(window,"Complete_Activity").IsEnabled);await shot(window,"111-activity-resume");
        window.CloseWithoutAction();window=app.OpenItems([app.Engine.State.Occurrences.Single(o=>o.Id==item.Id)]);
        Check("重建原生提醒窗口不重置累计活动",app.Engine.ActivityElapsed(item.Id,DateTimeOffset.UtcNow)==45);
        Click(Find(window,"Complete_Activity"));Check("本人确认活动后保存独立结果",app.Store.Load()!.Occurrences.Single(o=>o.Id==item.Id).Status==OccurrenceStatus.Completed);

        var form=new PlanEditor(app,null,HabitKind.Other){Owner=app.Main};form.Show();
        form.NameInput.Text="有截止的待办示例";form.ScheduleInput.SelectedIndex=2;form.OnceDate.SelectedDate=app.Engine.Local(DateTimeOffset.UtcNow.AddHours(1)).Date;form.OnceTime.Text=app.Engine.Local(DateTimeOffset.UtcNow.AddHours(1)).ToString("HH:mm");
        form.OnceDate.SetRawInput(app.Engine.Local(DateTimeOffset.UtcNow.AddHours(1)).ToString("yyyy-MM-dd"));
        form.HasDeadline.IsChecked=true;form.HasDeadline.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
        form.DeadlineDate.SetRawInput(app.Engine.Local(DateTimeOffset.UtcNow.AddHours(1)).ToString("yyyy-MM-dd"));form.DeadlineTime.Text=app.Engine.Local(DateTimeOffset.UtcNow.AddMinutes(10)).ToString("HH:mm");form.Save();
        Check("待办截止早于提醒时刻时原生表单拒绝保存",!form.Saved&&form.IsVisible&&form.ValidationMessage.Length>0);
        form.DeadlineDate.SetRawInput(app.Engine.Local(DateTimeOffset.UtcNow.AddHours(2)).ToString("yyyy-MM-dd"));form.DeadlineTime.Text=app.Engine.Local(DateTimeOffset.UtcNow.AddHours(2)).ToString("HH:mm");
        await shot(form,"112-todo-deadline");form.Save();verify("待办用途和截止通过原生表单保存",form.Saved&&form.SavedPlan is {IsHabit:false,ValidUntil:not null},form.ValidationMessage);
        app.Main.Navigate("Today");await shot(app.Main,"113-home-refined");
        app.ToggleAway();Check("主动离开停止呈现",app.Engine.State.IsAway&&app.OpenReminders.Count==0);await shot(app.Main,"114-away");app.ToggleAway();
        Check("主动返回显示内联摘要而不弹历史窗口",!app.Engine.State.IsAway&&app.Engine.State.RecoverySummaryVisible&&app.OpenReminders.Count==0);
        Check("场景与删除验收后 SQLite 完整",app.Store.IntegrityCheck()=="ok");
    }
}
