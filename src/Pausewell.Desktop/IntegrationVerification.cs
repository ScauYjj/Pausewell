using System.Runtime.InteropServices;
using Pausewell.Controls;
using Pausewell.Presentation;
namespace Pausewell.Desktop;
internal static class IntegrationVerification
{
 private sealed class ZonedContext(ReminderEngine engine):IPlanContext
 {public ReminderEngine Engine=>engine;public DateTimeOffset Now=>new(2026,10,16,16,5,0,TimeSpan.Zero);}
 // Holds only the synthetic verification database. A second SQLite writer must fail atomically.
 internal sealed class WriterLock:IDisposable
 {
  private IntPtr _handle;
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)]string path,out IntPtr db,int flags,IntPtr vfs);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_exec(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,IntPtr callback,IntPtr arg,out IntPtr error);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]private static extern void sqlite3_free(IntPtr value);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_close(IntPtr db);
  public WriterLock(string path){if(sqlite3_open_v2(path,out _handle,2|0x10000,IntPtr.Zero)!=0)throw new InvalidOperationException("无法打开验证数据库锁。");if(Exec("BEGIN IMMEDIATE;")!=0){Dispose();throw new InvalidOperationException("无法锁定验证数据库。");}}
  private int Exec(string sql){var result=sqlite3_exec(_handle,sql,IntPtr.Zero,IntPtr.Zero,out var error);if(error!=IntPtr.Zero)sqlite3_free(error);return result;}
  public void Dispose(){if(_handle==IntPtr.Zero)return;Exec("ROLLBACK;");sqlite3_close(_handle);_handle=IntPtr.Zero;}
 }
 public static async Task Run(DesktopController app,Action<string,bool,object?> verify,Func<Window,string,Task> shot)
 {
  if(!app.IsIsolated)throw new InvalidOperationException("接入验证仅允许独立数据。");app.Main.Navigate("Today");
  void Check(string name,bool value,object? detail=null)=>verify(name,value,detail);
  Check("正式程序集不依赖页面演示程序集",!typeof(PlanEditor).Assembly.GetReferencedAssemblies().Any(x=>x.Name is "Pausewell.PageLab" or "Pausewell.MaterialLab"));
  var boundary=new PlanForm(new ZonedContext(app.Engine),null,true){Owner=app.Main};boundary.Show();await Task.Delay(100);Check("新药物开始日期使用 Windows 系统本地日期",boundary.Starts.SelectedDate==TimeZoneInfo.ConvertTime(new ZonedContext(app.Engine).Now,TimeZoneInfo.Local).Date);boundary.VerificationClose=true;boundary.Close();
  var count=app.Engine.State.Plans.Count;var cancel=new PlanEditor(app,null){Owner=app.Main};cancel.Show();await Task.Delay(100);cancel.NameInput.Text="应放弃的修改";cancel.RequestCancel();Check("正式表单未保存修改要求明确放弃",cancel.DiscardNotice.IsVisible&&cancel.IsVisible);await shot(cancel,"23-unsaved-changes");cancel.DiscardButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check("放弃不提交数据库",app.Store.Load()!.Plans.Count==count);
  var draft=new PlanEditor(app,null,HabitKind.Medication){Owner=app.Main};draft.Show();await Task.Delay(100);draft.TimeFields.Inputs[0].Text="08:00";draft.TimeFields.Inputs[1].Text="20:00";draft.TimeFields.SetCount(3);draft.TimeFields.Inputs[2].Text="13:00";draft.TimeFields.SetCount(1);draft.TimeFields.SetCount(3);Check("正式药物表单恢复隐藏时刻草稿",draft.TimeFields.Inputs[2].Text=="13:00");draft.NameInput.Text="重复时刻校验";draft.TimeFields.Inputs[1].Text="08:00";Check("重复药物时刻不提交且错误指向字段",!draft.TrySave()&&FieldState.GetHasError(draft.TimeFields.Inputs[1])&&app.Store.Load()!.Plans.Count==count);await shot(draft,"24-drug-time-errors");draft.VerificationClose=true;draft.Close();
  var failed=new PlanEditor(app,null){Owner=app.Main};failed.Show();await Task.Delay(100);failed.NameInput.Text="数据库失败重试验证";failed.EnabledInput.IsChecked=false;
  using(var writer=new WriterLock(app.Store.FilePath))Check("SQLite 写锁导致保存失败并保留输入",!failed.TrySave()&&failed.IsVisible&&failed.NameInput.Text=="数据库失败重试验证"&&failed.ValidationMessage.Contains("未保存")&&app.Engine.State.Plans.Count==count);
  await shot(failed,"25-database-save-failure");Check("解除写锁后原地重试成功且保持停用",failed.TrySave()&&!app.Store.Load()!.Plans.Single(x=>x.Name=="数据库失败重试验证").Enabled);var revision=app.Engine.State.Plans.Single(x=>x.Name=="数据库失败重试验证").Revision;Check("已经提交的窗口拒绝重复保存",!failed.TrySave()&&app.Engine.State.Plans.Single(x=>x.Name=="数据库失败重试验证").Revision==revision);
  var original=app.Engine.State.Plans.First(x=>x.Kind==HabitKind.Water&&x.Enabled);var before=original.NextDue;var editing=new PlanEditor(app,original){Owner=app.Main};editing.Show();await Task.Delay(100);editing.IntervalInput.Text="31";editing.VerificationClose=true;editing.Close();Check("编辑后放弃不重置原计划时间",app.Store.Load()!.Plans.Single(x=>x.Id==original.Id).NextDue==before);
  var previousEnabled=original.Enabled;var toggle=SmokeVerification.Descendants<CheckBox>(app.Main).Single(x=>System.Windows.Automation.AutomationProperties.GetAutomationId(x)=="Enabled_"+original.Id);
  using(var writer=new WriterLock(app.Store.FilePath))toggle.IsChecked=!previousEnabled;
  Check("启停写入失败时开关恢复真实状态",toggle.IsChecked==previousEnabled&&app.Engine.State.Plans.Single(x=>x.Id==original.Id).Enabled==previousEnabled);app.Main.ClearError();
  var enabled=app.Engine.State.Plans.Where(x=>x.Kind!=HabitKind.Medication&&x.Enabled).Select(x=>x.Id).ToArray();foreach(var id in enabled)app.Engine.SetEnabled(id,false,DateTimeOffset.UtcNow);app.Main.Refresh();app.Main.UpdateLayout();for(var attempt=0;attempt<20;attempt++){await Task.Delay(50);app.Main.UpdateLayout();if(SmokeVerification.Descendants<TextBlock>(app.Main).Any(x=>x.Text=="暂无运行中的提醒"))break;}Check("全部停用不再显示提醒中",SmokeVerification.Descendants<TextBlock>(app.Main).Any(x=>x.Text=="暂无运行中的提醒"),new{app.Engine.State.Running,enabled=app.Engine.State.Plans.Where(x=>x.Kind!=HabitKind.Medication&&x.Enabled).Select(x=>x.Name).ToArray(),labels=SmokeVerification.Descendants<TextBlock>(app.Main).Where(x=>x.FontSize>=21).Select(x=>x.Text).ToArray()});await shot(app.Main,"26-all-disabled");foreach(var id in enabled)app.Engine.SetEnabled(id,true,DateTimeOffset.UtcNow);app.Main.Refresh();
  app.Main.ShowAppearance();var global=((SolidColorBrush)Application.Current.FindResource("Theme.toggle")).Color;app.Main.SelectTheme("rose");Check("主题预览只覆盖主窗口语义资源",((SolidColorBrush)app.Main.Resources["Theme.toggle"]).Color!=global&&((SolidColorBrush)Application.Current.FindResource("Theme.toggle")).Color==global&&DesignTheme.Current==Theme.Current);app.Main.CancelSettings();Check("取消移除局部语义主题资源",!app.Main.Resources.Contains("Theme.toggle"));app.Main.Navigate("Today");
 }
}
