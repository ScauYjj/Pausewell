using System.Windows.Automation;
using Pausewell.Controls;

namespace Pausewell.Desktop;

internal static class HistorySettingsVerification
{
    private static T Find<T>(Window window,string id) where T:DependencyObject{window.UpdateLayout();return SmokeVerification.Descendants<T>(window).Single(x=>AutomationProperties.GetAutomationId(x)==id);}
    private static void Click(Window window,string id)=>Find<Button>(window,id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Toggle(Window window,string id){var input=Find<CheckBox>(window,id);input.IsChecked=input.IsChecked!=true;input.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));}
    private static string[] Rows(Window window)=>SmokeVerification.Descendants<Button>(window).Select(x=>AutomationProperties.GetAutomationId(x)).Where(x=>x.StartsWith("Record_")).ToArray();
    private static bool Visible(Window window,FrameworkElement input){window.UpdateLayout();var point=input.TranslatePoint(new Point(),window);return input.IsVisible&&point.Y>=0&&point.Y+input.ActualHeight<=window.ActualHeight&&point.X>=0&&point.X+input.ActualWidth<=window.ActualWidth;}
    public static async Task Run(DesktopController app,Action<string,bool,object?> verify,Func<Window,string,Task> shot)
    {
        if(!app.IsIsolated)throw new InvalidOperationException("记录设置验证仅使用隔离数据。");
        void Check(string name,bool passed)=>verify(name,passed,null);
        app.Main.Navigate("History");await Task.Delay(100);
        var from=Find<IsoDatePicker>(app.Main,"HistoryFrom");var to=Find<IsoDatePicker>(app.Main,"HistoryTo");
        Check("记录初始日期使用业务时区最近七天",to.SelectedDate==app.Engine.Local(DateTimeOffset.UtcNow).Date&&from.SelectedDate==to.SelectedDate!.Value.AddDays(-6));
        Check("初始化日期格式不误报筛选已修改",Find<TextBlock>(app.Main,"HistoryFilterHint").Text=="按原计划日期筛选，使用Windows 系统时间。");
        var originalRows=Rows(app.Main);Check("日期分组后仍可打开独立记录",originalRows.Length==app.Engine.State.Occurrences.Count);
        from.Input!.Focus();from.SetRawInput("2026-02-30");Find<Button>(app.Main,"ApplyHistory").Focus();Click(app.Main,"ApplyHistory");
        await shot(app.Main,"70-history-invalid");Check("无效日期保留输入及上次筛选结果",FieldState.GetHasError(from)&&from.Input!.Text=="2026-02-30"&&Rows(app.Main).SequenceEqual(originalRows));
        app.Engine.BeginMeeting(DateTimeOffset.UtcNow.AddMinutes(15),DateTimeOffset.UtcNow,true);app.Main.RefreshIfChanged();await Task.Delay(100);
        Check("调度更新不重建正在编辑的记录筛选",ReferenceEquals(from,Find<IsoDatePicker>(app.Main,"HistoryFrom"))&&from.Input!.Text=="2026-02-30");app.Engine.EndMeeting(DateTimeOffset.UtcNow.AddSeconds(-31));app.Engine.DismissMeetingSummary(DateTimeOffset.UtcNow);
        from.SetRawInput(app.Engine.Local(DateTimeOffset.UtcNow).Date.AddDays(2).ToString("yyyy-MM-dd"));Click(app.Main,"ApplyHistory");
        Check("反向日期范围就地解释且不替换结果",FieldState.GetHasError(to)&&Rows(app.Main).SequenceEqual(originalRows));
        Click(app.Main,"ResetHistory");Find<ComboBox>(app.Main,"HistoryKind").SelectedIndex=(int)HabitKind.Medication+1;Find<ComboBox>(app.Main,"HistoryStatus").SelectedIndex=5;Click(app.Main,"ApplyHistory");
        var expected=app.Engine.State.Occurrences.Where(o=>o.Kind==HabitKind.Medication&&o.Status==OccurrenceStatus.NeedsReview).Select(o=>"Record_"+o.Id).Order().ToArray();
        Check("组合筛选返回正确的待核实服药记录",Rows(app.Main).Order().SequenceEqual(expected)&&expected.Length>0);await shot(app.Main,"71-history-filtered");
        from.SetRawInput("2000-01-01");to.SetRawInput("2000-01-02");Click(app.Main,"ApplyHistory");Check("无匹配结果显示明确空状态",Rows(app.Main).Length==0&&SmokeVerification.Descendants<TextBlock>(app.Main).Any(x=>x.Text=="暂无符合条件的记录"));await shot(app.Main,"72-history-empty");Click(app.Main,"ResetHistory");
        app.Main.Width=900;app.Main.Height=650;await Task.Delay(100);Check("紧凑记录页保留筛选和列表滚动空间",Visible(app.Main,Find<Button>(app.Main,"ApplyHistory"))&&SmokeVerification.Descendants<ScrollViewer>(app.Main).Any(s=>s.ViewportHeight>80));await shot(app.Main,"73-history-compact");app.Main.Width=1200;app.Main.Height=860;

        var record=app.Engine.State.Occurrences.First(o=>o.Kind==HabitKind.Water&&o.Status==OccurrenceStatus.Completed);var priorCount=record.Corrections.Count;var priorNext=app.Engine.State.Plans.Single(p=>p.Id==record.PlanId).NextDue;
        var detail=new RecordWindow(app,record.Id){Owner=app.Main};detail.Show();await Task.Delay(100);await shot(detail,"74-record-detail");Click(detail,"CorrectRecord");detail.UpdateLayout();await Task.Delay(100);detail.ReasonInput!.Text="核对日历与执行时间";
        detail.UnknownTime!.IsChecked=false;detail.UnknownTime.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));detail.ActualDate!.SetRawInput("2026-02-30");Click(detail,"SaveCorrection");
        await shot(detail,"74a-record-invalid");Check("更正无效日期不保存且保留原因",FieldState.GetHasError(detail.ActualDate)&&detail.ReasonInput.Text=="核对日历与执行时间"&&app.Store.Load()!.Occurrences.Single(o=>o.Id==record.Id).Corrections.Count==priorCount);
        detail.ActualDate.SetRawInput(app.Engine.Local(DateTimeOffset.UtcNow).Date.ToString("yyyy-MM-dd"));detail.ActualTime!.Text="25:90";Click(detail,"SaveCorrection");Check("更正无效时刻指向具体字段",FieldState.GetHasError(detail.ActualTime));
        detail.ActualDate.SetRawInput(app.Engine.Local(DateTimeOffset.UtcNow).Date.AddDays(1).ToString("yyyy-MM-dd"));detail.ActualTime.Text="12:00";Click(detail,"SaveCorrection");Check("更正不接受未来实际时间",FieldState.GetHasError(detail.ActualTime)&&detail.ErrorMessage.Text.Contains("未保存"));
        detail.StatusInput!.SelectedIndex=1;Check("切换到未执行禁用实际时间但保留草稿",!detail.ActualDate.IsEnabled&&!detail.ActualTime.IsEnabled&&detail.ActualTime.Text=="12:00");detail.StatusInput.SelectedIndex=0;detail.UnknownTime.IsChecked=true;detail.UnknownTime.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
        using(var writer=new IntegrationVerification.WriterLock(app.Store.FilePath))Click(detail,"SaveCorrection");
        Check("更正写入失败保留输入且记录原子回滚",detail.IsVisible&&detail.ErrorMessage.Text.Contains("未保存")&&detail.ReasonInput.Text=="核对日历与执行时间"&&app.Store.Load()!.Occurrences.Single(o=>o.Id==record.Id).Corrections.Count==priorCount);await shot(detail,"75-record-save-failure");
        detail.Height=480;await Task.Delay(100);Check("紧凑更正窗口固定保存和取消操作",Visible(detail,Find<Button>(detail,"SaveCorrection"))&&Visible(detail,Find<Button>(detail,"CancelCorrection")));await shot(detail,"76-record-compact");Click(detail,"SaveCorrection");
        var saved=app.Store.Load()!;Check("更正重试记录未知实际时间与修改历史且不平移计划",saved.Occurrences.Single(o=>o.Id==record.Id).Corrections.Count==priorCount+1&&saved.Occurrences.Single(o=>o.Id==record.Id).ActualAt==null&&saved.Plans.Single(p=>p.Id==record.PlanId).NextDue==priorNext);
        detail.Height=780;await shot(detail,"77-record-history");Click(detail,"CorrectRecord");detail.ReasonInput!.Text="取消的草稿";Click(detail,"CancelCorrection");Check("取消更正不增加修改记录",app.Store.Load()!.Occurrences.Single(o=>o.Id==record.Id).Corrections.Count==priorCount+1);detail.Close();

        app.Main.Navigate("Settings");Click(app.Main,"GeneralSettings");var before=app.Store.Load()!;var oldSound=before.SoundEnabled;Toggle(app.Main,"SoundSetting");Click(app.Main,"AppearanceSettings");app.Main.SelectTheme("rose");Click(app.Main,"GeneralSettings");
        Check("设置跨通用外观切换保留同一草稿",Find<CheckBox>(app.Main,"SoundSetting").IsChecked==!oldSound&&Theme.Current==before.Preferences.Theme&&app.Main.Resources.Contains("Theme.ambient"));
        using(var writer=new IntegrationVerification.WriterLock(app.Store.FilePath))Click(app.Main,"SaveSettings");
        Check("设置失败保持草稿预览且不发布全局",app.Store.Load()!.SoundEnabled==oldSound&&Theme.Current==before.Preferences.Theme&&app.Main.Resources.Contains("Theme.ambient")&&Find<CheckBox>(app.Main,"SoundSetting").IsChecked==!oldSound);await shot(app.Main,"78-settings-save-failure");
        Click(app.Main,"CancelSettings");Check("取消设置同时回滚通用草稿和局部主题",Find<CheckBox>(app.Main,"SoundSetting").IsChecked==oldSound&&!app.Main.Resources.Contains("Theme.ambient")&&Theme.Current==before.Preferences.Theme);
        Toggle(app.Main,"SoundSetting");Click(app.Main,"AppearanceSettings");app.Main.SelectTheme("blue");Click(app.Main,"SaveSettings");
        Check("设置保存同时持久化通用与完整主题",app.Store.Load()!.SoundEnabled==!oldSound&&app.Store.Load()!.Preferences.Theme=="blue"&&Theme.Current=="blue"&&!app.Main.Resources.Contains("Theme.ambient"));await shot(app.Main,"79-appearance");
        app.Main.Width=900;app.Main.Height=650;await Task.Delay(100);
        var firstTheme=Find<Button>(app.Main,"Theme_champagne");var lastTheme=Find<Button>(app.Main,"Theme_blue");var themeRow=(WrapPanel)firstTheme.Parent;
        Check("紧凑外观页面主题自动换行且保存可见",Visible(app.Main,Find<Button>(app.Main,"SaveSettings"))&&lastTheme.TranslatePoint(new Point(),themeRow).Y>firstTheme.TranslatePoint(new Point(),themeRow).Y&&themeRow.Children.OfType<Button>().All(b=>b.TranslatePoint(new Point(),themeRow).X+b.ActualWidth<=themeRow.ActualWidth+1));await shot(app.Main,"80-appearance-compact");
        Click(app.Main,"GeneralSettings");await shot(app.Main,"81-settings-compact");Check("紧凑通用设置保留固定操作区",Visible(app.Main,Find<Button>(app.Main,"SaveSettings"))&&Visible(app.Main,Find<Button>(app.Main,"CancelSettings")));
        Toggle(app.Main,"SoundSetting");app.Main.Navigate("Today");app.Main.Navigate("Settings");Click(app.Main,"GeneralSettings");Check("离开设置放弃未保存通用修改",Find<CheckBox>(app.Main,"SoundSetting").IsChecked==!oldSound);
        Toggle(app.Main,"SoundSetting");Click(app.Main,"AppearanceSettings");app.Main.SelectTheme("champagne");Click(app.Main,"SaveSettings");app.Main.Width=1200;app.Main.Height=860;Click(app.Main,"GeneralSettings");await shot(app.Main,"82-general-settings");app.Main.Navigate("History");await shot(app.Main,"83-history");
        Check("记录设置专项后数据库保持完整",app.Store.IntegrityCheck()=="ok");
    }
}
