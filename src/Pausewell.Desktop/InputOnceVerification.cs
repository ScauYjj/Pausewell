using Pausewell.Controls;
using Pausewell.ComponentLab;
using System.Windows.Automation;
using System.Windows.Media.Imaging;

namespace Pausewell.Desktop;

internal static class InputOnceVerification
{
    private static double Luma(Color color)
    {double Linear(byte channel){var n=channel/255d;return n<=.04045?n/12.92:Math.Pow((n+.055)/1.055,2.4);}return .2126*Linear(color.R)+.7152*Linear(color.G)+.0722*Linear(color.B);}
    private static double Contrast(Color a,Color b){var x=Luma(a);var y=Luma(b);return(Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
    private static bool Readable(TextBox input)=>input.SelectionBrush is SolidColorBrush background&&input.SelectionTextBrush is SolidColorBrush text&&Contrast(background.Color,text.Color)>=4.5;
    private static bool SelectedGlyphsVisible(Window window,TextBox input,string path)
    {
        SmokeVerification.Capture((FrameworkElement)window.Content,path);
        using var stream=File.OpenRead(path);var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);var frame=new FormatConvertedBitmap(decoder.Frames[0],PixelFormats.Bgra32,null,0);
        var first=input.GetRectFromCharacterIndex(0);var last=input.GetRectFromCharacterIndex(input.Text.Length,true);
        var origin=((FrameworkElement)window.Content).PointToScreen(new Point());var a=input.PointToScreen(new Point(first.X+1,first.Y+1));var b=input.PointToScreen(new Point(last.X-1,first.Bottom-1));
        var rect=new Int32Rect((int)Math.Ceiling(a.X-origin.X),(int)Math.Ceiling(a.Y-origin.Y),Math.Max(1,(int)(b.X-a.X)),Math.Max(1,(int)(b.Y-a.Y)));
        var pixels=new byte[rect.Width*rect.Height*4];frame.CopyPixels(rect,pixels,rect.Width*4,0);var light=0;var dark=0;
        for(var i=0;i<pixels.Length;i+=4){if(pixels[i]>215&&pixels[i+1]>215&&pixels[i+2]>215)light++;if(pixels[i]<180&&pixels[i+1]<180&&pixels[i+2]<180)dark++;}
        var count=rect.Width*rect.Height;return light>count*.02&&light<count*.5&&dark>count*.5;
    }
    public static async Task Run(DesktopController app,Action<string,bool,object?> verify,Func<Window,string,Task> shot)
    {
        if(!app.IsIsolated)throw new InvalidOperationException("输入和一次性验证仅使用隔离数据。");
        var field=new PlanEditor(app,null){Owner=app.Main};field.Show();await Task.Delay(100);field.NameInput.Text="选中后仍能看清这段文字";
        foreach(var theme in Theme.All)
        {
            app.Main.ShowAppearance();app.Main.SelectTheme(theme.Id);app.Main.SaveUserSettings();NativeInput.Activate(field);field.NameInput.Focus();field.NameInput.SelectAll();await Task.Delay(100);
            verify(theme.Name+"输入选区文字对比及实际选中",Readable(field.NameInput)&&Readable(field.IntervalInput)&&Readable(field.NoteInput)&&field.NameInput.SelectionLength==field.NameInput.Text.Length&&field.NameInput.IsSelectionActive,null);
            await shot(field,"40-selection-"+theme.Id);
            verify(theme.Name+"真实选区截图包含可见字形",SelectedGlyphsVisible(field,field.NameInput,Path.Combine(app.DataDirectory,"selection-"+theme.Id+".png"))&&AppContext.TryGetSwitch("Switch.System.Windows.Controls.Text.UseAdornerForTextboxSelectionRendering",out var adorner)&&!adorner,null);
        }
        field.VerificationClose=true;field.Close();
        var medicine=new PlanEditor(app,null,HabitKind.Medication){Owner=app.Main};medicine.Show();await Task.Delay(150);medicine.TimeFields.Inputs[0].Text="08:00";NativeInput.Activate(medicine);medicine.Starts.Input!.Focus();medicine.Starts.Input.SelectAll();
        verify("日期及时间输入采用明确的选中文字颜色",Readable(medicine.Starts.Input)&&Readable(medicine.TimeFields.Inputs[0]),null);await shot(medicine,"41-date-selection");medicine.VerificationClose=true;medicine.Close();
        app.Main.ShowAppearance();app.Main.SelectTheme("champagne");app.Main.SaveUserSettings();app.Main.Navigate("Today");

        var custom=new PlanEditor(app,null){Owner=app.Main};custom.Show();await Task.Delay(100);custom.KindInput.SelectedIndex=3;custom.UpdateLayout();
        verify("自定义显示内容区并清除未修改的模板名称",custom.CustomSection.IsVisible&&custom.NameInput.Text==""&&AutomationProperties.GetName(custom.NameInput)=="提醒标题",null);
        custom.CustomContentInput.Text="带上签字页，提交前再核对一次。";custom.KindInput.SelectedIndex=0;custom.KindInput.SelectedIndex=3;
        verify("切换类型保留自定义内容草稿",custom.CustomContentInput.Text=="带上签字页，提交前再核对一次。",null);
        custom.NameInput.Text="提交材料";custom.ScheduleInput.SelectedIndex=2;custom.UpdateLayout();
        verify("仅一次显示日期时刻并隐藏循环限制",custom.OnceSection.IsVisible&&!custom.IntervalSection.IsVisible&&!custom.More.IsVisible,null);
        custom.OnceDate.SelectedDate=app.Engine.Local(DateTimeOffset.UtcNow).Date.AddDays(-1);custom.OnceTime.Text="10:00";
        verify("一次性表单阻止过去时间且保留输入",!custom.TrySave()&&FieldState.GetHasError(custom.OnceTime)&&custom.CustomContentInput.Text.Length>0,null);
        var later=app.Engine.Local(DateTimeOffset.UtcNow.AddMinutes(10)).AddDays(1);custom.OnceDate.SelectedDate=later.Date;await Task.Delay(150);
        verify("报错后选择另一天不会被旧日期覆盖",custom.OnceDate.SelectedDate==later.Date,null);
        custom.OnceTime.Text=later.ToString("HH:mm");await shot(custom,"42-custom-once-editor");
        verify("自定义一次性表单提交 SQLite",custom.TrySave(),null);var saved=app.Store.Load()!.Plans.Single(p=>p.Name=="提交材料");
        verify("保存自定义标题、正文、日期时刻与仅一次规则",saved.Kind==HabitKind.Other&&saved.Schedule==ScheduleKind.Once&&saved.OnceAt!=null&&saved.Note=="带上签字页，提交前再核对一次。"&&!saved.WorkdaysOnly&&saved.WindowStart==null,null);
        var edit=new PlanEditor(app,saved){Owner=app.Main};edit.Show();await Task.Delay(100);
        verify("重新编辑恢复自定义内容和一次性日期",edit.CustomContentInput.Text==saved.Note&&edit.ScheduleInput.SelectedIndex==2&&edit.OnceDate.SelectedDate==app.Engine.Local(saved.OnceAt!.Value).Date,null);edit.VerificationClose=true;edit.Close();

        // Clear only synthetic test-mode backlog, then exercise the real clock and native reminder.
        var now=DateTimeOffset.UtcNow;app.Engine.EndMeeting(now.AddSeconds(-31));app.Engine.Defer(app.Engine.State.Occurrences.Where(o=>o.Status is OccurrenceStatus.Pending or OccurrenceStatus.Deferred).Select(o=>o.Id),now,60);app.Engine.DismissMeetingSummary(now);
        saved.OnceAt=now.AddSeconds(3);app.Engine.SavePlan(saved,now,true);
        var timeout=now.AddSeconds(12);while(!app.OpenReminders.Any()&&DateTimeOffset.UtcNow<timeout)await Task.Delay(150);
        var reminder=app.OpenReminders.FirstOrDefault();
        verify("真实时钟触发一次性自定义弹窗",reminder!=null&&!reminder.IsPreview&&SmokeVerification.Descendants<TextBlock>(reminder).Any(x=>x.Text==saved.Name)&&SmokeVerification.Descendants<TextBlock>(reminder).Any(x=>x.Text==saved.Note),null);
        await shot(reminder!,"43-custom-once-reminder");var complete=SmokeVerification.Descendants<Button>(reminder!).Single(b=>AutomationProperties.GetAutomationId(b)=="Complete_Other");complete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        verify("一次性确认后结束并保留完整记录",!app.Engine.State.Plans.Single(p=>p.Id==saved.Id).Enabled&&app.Store.Load()!.Occurrences.Single(o=>o.PlanId==saved.Id&&o.Revision==2).Status==OccurrenceStatus.Completed,null);
        await Task.Delay(1300);verify("后续调度不再生成第二个一次性事项",app.OpenReminders.Count==0&&app.Engine.State.Occurrences.Count(o=>o.PlanId==saved.Id&&o.Revision==2)==1,null);
        app.Main.Navigate("Today");await shot(app.Main,"44-once-finished");
    }
}
