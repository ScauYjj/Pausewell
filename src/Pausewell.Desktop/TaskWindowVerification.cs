using Pausewell.Controls;
using Pausewell.Presentation;
using System.Windows.Automation;

namespace Pausewell.Desktop;

internal static class TaskWindowVerification
{
    private static Button Button(Window window,string id){window.UpdateLayout();return SmokeVerification.Descendants<Button>(window).Single(b=>AutomationProperties.GetAutomationId(b)==id);}
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    private static bool InSurface(MaterialWindow window,FrameworkElement element)
    {
        window.UpdateLayout();var point=element.TranslatePoint(new Point(),window.Surface.ContentHost);
        return element.IsVisible&&point.X>=-1&&point.Y>=-1&&point.X+element.ActualWidth<=window.Surface.ContentHost.ActualWidth+1&&point.Y+element.ActualHeight<=window.Surface.ContentHost.ActualHeight+1;
    }
    public static async Task Run(DesktopController app,Action<string,bool,object?> verify,Func<Window,string,Task> shot)
    {
        if(!app.IsIsolated)throw new InvalidOperationException("任务窗口验证仅使用隔离数据。");
        var now=DateTimeOffset.UtcNow;app.Engine.EndMeeting(now.AddSeconds(-31));app.Engine.Defer(app.Engine.State.Occurrences.Where(o=>o.Status is OccurrenceStatus.Pending or OccurrenceStatus.Deferred).Select(o=>o.Id),now,60);app.Engine.DismissMeetingSummary(now);
        var before=app.Engine.State.Plans.Count;
        var invalid=new ModeWindow(app,false){Owner=app.Main};invalid.Show();await Task.Delay(150);
        verify("会议窗口初始无错误并使用统一材质",invalid.ErrorMessage.Text.Length==0&&invalid.Surface.Profile.Name=="popup",null);
        invalid.MinutesInput.Text="0";Click(invalid.ApplyButton);
        verify("无效会议时长就地报错且未开始会议",invalid.IsVisible&&FieldState.GetHasError(invalid.MinutesInput)&&app.Engine.State.MeetingUntil==null,null);await shot(invalid,"50-meeting-invalid");
        var quick=SmokeVerification.Descendants<RadioButton>(invalid).Single(r=>AutomationProperties.GetAutomationId(r)=="ModeDuration30");quick.IsChecked=true;
        verify("快捷时长填写和校验状态同步",invalid.MinutesInput.Text=="30"&&!FieldState.GetHasError(invalid.MinutesInput)&&invalid.RecoveryLabel.Text.Contains("预计结束"),null);
        invalid.Height=480;await Task.Delay(100);verify("紧凑会议窗口保留操作区",InSurface(invalid,invalid.ApplyButton),null);await shot(invalid,"51-meeting-compact");Click(Button(invalid,"CancelMode"));
        verify("会议取消不改变计划和运行模式",app.Engine.State.Plans.Count==before&&app.Engine.State.MeetingUntil==null,null);

        var failed=new ModeWindow(app,false){Owner=app.Main};failed.Show();await Task.Delay(100);failed.MinutesInput.Text="15";failed.ScopeInput.IsChecked=true;
        using(var writer=new IntegrationVerification.WriterLock(app.Store.FilePath))Click(failed.ApplyButton);
        verify("会议写入失败保留窗口时长及静音选择",failed.IsVisible&&failed.MinutesInput.Text=="15"&&failed.ScopeInput.IsChecked==true&&failed.ErrorMessage.Text.Contains("未开始")&&app.Engine.State.MeetingUntil==null,null);await shot(failed,"52-meeting-save-failure");Click(failed.ApplyButton);
        verify("解除写锁后会议可重试并保持静音范围",!failed.IsVisible&&app.Engine.State.MeetingSilent&&app.Engine.State.MeetingUntil>DateTimeOffset.UtcNow.AddMinutes(14),null);
        app.Engine.EndMeeting(DateTimeOffset.UtcNow.AddSeconds(-31));app.Engine.DismissMeetingSummary(DateTimeOffset.UtcNow);
        var pause=new ModeWindow(app,true){Owner=app.Main};pause.Show();await Task.Delay(100);await shot(pause,"53-pause");var medicinePreview=app.Preview(HabitKind.Medication,ReminderStyle.Card);Click(pause.ApplyButton);
        verify("普通暂停仍保留服药窗口且有恢复期限",medicinePreview.IsVisible&&app.Engine.State.PauseUntil>DateTimeOffset.UtcNow&&!app.Engine.State.PauseMedication,null);medicinePreview.CloseWithoutAction();app.Engine.ResumePause(DateTimeOffset.UtcNow);

        var changed=false;var confirmation=new MedicineConfirmation(app,new(){PlanName="药物窗口验证（独立示例）",Kind=HabitKind.Medication,DueAt=DateTimeOffset.UtcNow},true,()=>changed=true){Owner=app.Main};confirmation.Show();await Task.Delay(100);confirmation.ActualTime.Text="25:90";Click(confirmation.SaveButton);
        verify("服药无效时刻不提交且错误指向输入",confirmation.IsVisible&&!changed&&FieldState.GetHasError(confirmation.ActualTime),null);await shot(confirmation,"54-medicine-invalid");
        confirmation.ActualDate.SetRawInput(app.Engine.Local(DateTimeOffset.UtcNow).Date.AddDays(1).ToString("yyyy-MM-dd"));confirmation.ActualTime.Text="12:00";Click(confirmation.SaveButton);
        verify("未来实际服用时间不能保存",confirmation.IsVisible&&!changed&&confirmation.ErrorMessage.Text.Contains("未保存"),null);
        confirmation.UnknownTime.IsChecked=true;confirmation.UnknownTime.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));confirmation.Height=480;await Task.Delay(100);
        verify("未知实际时间禁用日期时刻并保留固定保存区",!confirmation.ActualDate.IsEnabled&&!confirmation.ActualTime.IsEnabled&&InSurface(confirmation,confirmation.SaveButton),null);await shot(confirmation,"55-medicine-compact");Click(confirmation.SaveButton);
        verify("服药预览确认仍不写入记录",changed&&!confirmation.IsVisible,null);

        var draft=new ModeWindow(app,true){Owner=app.Main};draft.Show();await Task.Delay(100);draft.MinutesInput.Text="47";draft.ScopeInput.IsChecked=true;
        var active=app.Preview(HabitKind.Activity,ReminderStyle.Card);Click(Button(active,"StartActivity"));var complete=Button(active,"Complete_Activity");
        foreach(var theme in new[]{"violet","blue","champagne"})
        {
            app.Main.ShowAppearance();app.Main.SelectTheme(theme);app.Main.SaveUserSettings();await Task.Delay(100);
            var environment=((SolidColorBrush)app.Main.FindResource("Theme.ambient")).Color;
            verify(theme+"任务窗口原地换肤保留输入和计时控件",draft.Ambient.Environment==environment&&active.Ambient.Environment==environment&&draft.MinutesInput.Text=="47"&&draft.ScopeInput.IsChecked==true&&app.OpenReminders.Contains(active)&&ReferenceEquals(complete,Button(active,"Complete_Activity")),null);
            await shot(draft,"56-mode-theme-"+theme);await shot(active,"57-reminder-theme-"+theme);
        }
        active.Height=480;await Task.Delay(100);verify("紧凑活动提醒主操作和延期均在固定区",InSurface(active,complete)&&InSurface(active,Button(active,"DeferReminder")),null);await shot(active,"58-reminder-compact");active.CloseWithoutAction();draft.Close();

        var records=app.Engine.State.Occurrences.Count;var batch=new ReminderWindow(app,[new(){PlanName="喝水",Kind=HabitKind.Water,DueAt=DateTimeOffset.UtcNow},new(){PlanName="如厕",Kind=HabitKind.Toilet,DueAt=DateTimeOffset.UtcNow}],true){Owner=app.Main};batch.Show();await Task.Delay(100);
        verify("多项提醒保留全局延期操作",InSurface(batch,Button(batch,"DeferReminder")),null);await shot(batch,"58-batch-reminder");Click(Button(batch,"Complete_Water"));verify("多项提醒完成一项后仍保留其余事项",batch.IsVisible,null);Click(Button(batch,"Complete_Toilet"));verify("多项预览全部完成后关闭且不写入记录",!batch.IsVisible&&app.Engine.State.Occurrences.Count==records,null);
        now=DateTimeOffset.UtcNow;app.Engine.BeginMeeting(now.AddMinutes(15),now,true);
        var occurrence=app.Engine.State.Occurrences.First(o=>o.Kind==HabitKind.Medication&&o.Status==OccurrenceStatus.Completed);
        app.Engine.CorrectRecord(occurrence.Id,OccurrenceStatus.NeedsReview,null,"隔离窗口验证：重新核对实际情况",now);
        var done=false;var actual=new MedicineConfirmation(app,occurrence,false,()=>done=true){Owner=app.Main};actual.Show();await Task.Delay(100);actual.UnknownTime.IsChecked=true;actual.UnknownTime.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
        using(var writer=new IntegrationVerification.WriterLock(app.Store.FilePath))Click(actual.SaveButton);
        verify("服药写入失败不误记已服用并保留填写",actual.IsVisible&&!done&&actual.UnknownTime.IsChecked==true&&app.Engine.State.Occurrences.Single(o=>o.Id==occurrence.Id).Status==OccurrenceStatus.NeedsReview,null);await shot(actual,"59-medicine-save-failure");Click(actual.SaveButton);
        verify("服药重试分别保存未知实际时间及确认时间",done&&app.Store.Load()!.Occurrences.Single(o=>o.Id==occurrence.Id).ActualAt==null&&app.Store.Load()!.Occurrences.Single(o=>o.Id==occurrence.Id).ResolvedAt!=null,null);
        app.Engine.CorrectRecord(occurrence.Id,OccurrenceStatus.NeedsReview,null,"隔离清单验证：保留待核实状态",DateTimeOffset.UtcNow);var pending=app.Engine.State.Occurrences.Single(o=>o.Id==occurrence.Id);
        var ordinary=new ReminderPlan{Name="会后一次性待办",Kind=HabitKind.Other,Schedule=ScheduleKind.Once,OnceAt=DateTimeOffset.UtcNow.AddSeconds(1)};app.Engine.SavePlan(ordinary,DateTimeOffset.UtcNow,true);await Task.Delay(1500);app.Engine.Evaluate(DateTimeOffset.UtcNow);
        app.ShowQueue();var queue=app.CurrentQueue!;await Task.Delay(100);await shot(queue,"60-queue");queue.Height=480;
        verify("会后清单区分普通事项和待核实服药",SmokeVerification.Descendants<TextBlock>(queue).Any(x=>x.Text=="普通事项")&&SmokeVerification.Descendants<TextBlock>(queue).Any(x=>x.Text=="服药记录单独核对"),null);
        verify("紧凑清单保留返回操作",InSurface(queue,Button(queue,"CloseQueue")),null);await shot(queue,"60-queue-compact");
        using(var writer=new IntegrationVerification.WriterLock(app.Store.FilePath))Click(Button(queue,"CloseQueue"));
        verify("清单延期写入失败保持窗口和待核实记录",queue.IsVisible&&app.Engine.State.Occurrences.Single(o=>o.Id==pending.Id).Status==OccurrenceStatus.NeedsReview,null);Click(Button(queue,"CloseQueue"));
        verify("清单返回保留待核实及普通待办，不擅自延期",app.CurrentQueue==null&&app.Store.Load()!.Occurrences.Single(o=>o.Id==pending.Id).Status==OccurrenceStatus.NeedsReview&&app.Store.Load()!.Occurrences.Single(o=>o.PlanId==ordinary.Id).Status==OccurrenceStatus.Pending,null);
        app.Engine.EndMeeting(DateTimeOffset.UtcNow.AddSeconds(-31));app.Engine.Defer(app.Engine.State.Occurrences.Where(o=>o.Status is OccurrenceStatus.Pending or OccurrenceStatus.Deferred).Select(o=>o.Id),DateTimeOffset.UtcNow,60);app.Engine.DismissMeetingSummary(DateTimeOffset.UtcNow);app.Main.ClearError();app.Main.Navigate("Today");
        verify("任务窗口操作后 SQLite 完整",app.Store.IntegrityCheck()=="ok",null);
    }
}
