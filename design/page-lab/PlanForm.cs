using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Pausewell.Controls;
using Pausewell.Core;
using Pausewell.MaterialLab;
using static Pausewell.PageLab.PageUi;
namespace Pausewell.PageLab;
public sealed class PlanForm:Window
{
 private readonly PreviewSession _session;
 private readonly ReminderPlan _original;
 private readonly bool _medication,_new;
 private bool _building=true,_submitted,_allowClose,_saving,_reading;
 private string _baseline="";
 private readonly HabitKind[] _kinds=[HabitKind.Water,HabitKind.Activity,HabitKind.Toilet,HabitKind.Other];
 public TextBox NameInput {get;}=Input("","FormName","提醒名称");
 public ComboBox KindInput {get;}=new(){ItemsSource=new[]{"喝水","起来活动","如厕提醒","自定义"},SelectedIndex=0};
 public ComboBox ScheduleInput {get;}=new(){ItemsSource=new[]{"确认完成后间隔","每天固定时刻"},SelectedIndex=0};
 public TextBox IntervalInput {get;}=Input("30","FormInterval","间隔分钟");
 public TextBox DurationInput {get;}=Input("120","FormDuration","活动秒数");
 public ComboBox StyleInput {get;}=new(){ItemsSource=new[]{"托盘轻提醒","醒目弹窗","全屏提醒"},SelectedIndex=1};
 public TextBox NoteInput {get;}=Input("","FormNote","备注");
 public CheckBox EnabledInput {get;}=new(){Content="启用此提醒",IsChecked=true};
 public CheckBox Workdays {get;}=new(){Content="仅周一至周五"};
 public CheckBox UseWindow {get;}=new(){Content="限定每天提醒时段"};
 public TextBox WindowStart {get;}=Input("09:00","WindowStart","每天开始时刻");
 public TextBox WindowEnd {get;}=Input("18:00","WindowEnd","每天结束时刻");
 public IsoDatePicker Starts {get;}=new();
 public PlanDatePicker Ends {get;}=new(){AllowClear=true,IsEnabled=false};
 public CheckBox HasEnd {get;}=new(){Content="设置结束日期"};
 public TimeFields TimeFields {get;}
 public Expander More {get;}=new(){Header="更多设置"};
 public TextBlock Preview {get;}=Text("",14,color:"Color.textSecondary");
 public TextBlock SummaryError {get;}=Text("",14,color:"Color.danger");
 public Button SaveButton {get;}
 public Button CancelButton {get;}
 public Button DiscardButton {get;}
 public Border DiscardNotice {get;}
 public ScrollViewer BodyScroll {get;}=new();
 public FrameworkElement IntervalSection {get;private set;}=null!;
 public FrameworkElement DurationSection {get;private set;}=null!;
 public FrameworkElement TimeSection {get;private set;}=null!;
 public FrameworkElement WindowSection {get;private set;}=null!;
 public bool Saved {get;private set;}
 public string SavedName {get;private set;}="";
 public bool IsDirty=>CaptureDraft()!=_baseline;
 public AmbientScene Ambient {get;}=new();
 public GlassSurface Surface {get;}
 public bool VerificationClose {get;set;}
 public PlanForm(PreviewSession session,ReminderPlan? plan,bool medication)
 {
  _session=session;_medication=medication;_new=plan==null;_original=plan==null?new(){Name=medication?"":"喝水",Kind=medication?HabitKind.Medication:HabitKind.Water,Schedule=medication?ScheduleKind.FixedTimes:ScheduleKind.Interval,StartsOn=medication?DateOnly.FromDateTime(session.Now.Date):null}:PreviewSession.Clone(plan);
  Title=(_new?"新增":"编辑")+(medication?"药物计划":"提醒");Width=medication?784:704;Height=medication?900:860;MinWidth=medication?640:600;MinHeight=540;MaxHeight=SystemParameters.WorkArea.Height-48;WindowStartupLocation=WindowStartupLocation.CenterOwner;FontFamily=new("Microsoft YaHei UI");FontSize=14;UseLayoutRounding=true;
  NameInput.Text=_original.Name;NameInput.MaxLength=80;KindInput.SelectedIndex=Math.Max(0,Array.IndexOf(_kinds,_original.Kind));ScheduleInput.SelectedIndex=(int)_original.Schedule;IntervalInput.Text=_original.IntervalMinutes.ToString();DurationInput.Text=_original.ActivitySeconds.ToString();StyleInput.SelectedIndex=(int)_original.Style;NoteInput.Text=_original.Note;NoteInput.AcceptsReturn=true;NoteInput.TextWrapping=TextWrapping.Wrap;NoteInput.MinHeight=80;NoteInput.MaxHeight=140;NoteInput.MaxLength=1000;NoteInput.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;EnabledInput.IsChecked=_original.Enabled;Workdays.IsChecked=_original.WorkdaysOnly;UseWindow.IsChecked=_original.WindowStart!=null;WindowStart.Text=_original.WindowStart??"09:00";WindowEnd.Text=_original.WindowEnd??"18:00";Starts.SelectedDate=(_original.StartsOn??DateOnly.FromDateTime(session.Now.Date)).ToDateTime(TimeOnly.MinValue);Ends.SelectedDate=_original.EndsOn?.ToDateTime(TimeOnly.MinValue);HasEnd.IsChecked=_original.EndsOn!=null;Ends.IsEnabled=HasEnd.IsChecked==true;
  TimeFields=new(_original.Times,medication?"Drug":"Fixed");if(_new&&medication)TimeFields.SetCount(2);
  foreach(var (control,id,label) in new (DependencyObject,string,string)[]{(KindInput,"FormKind","提醒事项"),(ScheduleInput,"FormSchedule","循环方式"),(StyleInput,"FormStyle","提醒方式"),(EnabledInput,"FormEnabled","启用此提醒"),(Workdays,"FormWorkdays","仅工作日"),(UseWindow,"FormUseWindow","限定每天时段"),(Starts,"FormStarts","开始"),(Ends,"FormEnds","结束"),(HasEnd,"FormHasEnd","设置结束日期"),(More,"FormMore","更多设置")})Id(control,id,label);
  SaveButton=Button("保存","FormSave",()=>TrySave(),true);CancelButton=Button("取消","FormCancel",RequestCancel);DiscardButton=Button("放弃修改","FormDiscard",()=>{_allowClose=true;Close();});var keep=Button("继续编辑","FormKeepEditing",()=>{DiscardNotice!.Visibility=Visibility.Collapsed;NameInput.Focus();});var discard=new StackPanel();discard.Children.Add(Text("修改尚未保存。要放弃这次修改吗？",14,true));var discardActions=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(0,8,0,0)};discardActions.Children.Add(keep);DiscardButton.Margin=new(8,0,0,0);discardActions.Children.Add(DiscardButton);discard.Children.Add(discardActions);DiscardNotice=Paper(discard);DiscardNotice.Visibility=Visibility.Collapsed;DiscardNotice.Margin=new(0,0,0,12);
  var root=new Grid();root.Children.Add(Ambient);Surface=new(Ambient,Profiles["content"]){Margin=new(16)};Surface.ContentHost.Padding=new(24);root.Children.Add(Surface);var layout=new DockPanel();var header=new StackPanel{Margin=new(0,0,0,24)};header.Children.Add(Text(Title,24,true));header.Children.Add(Text(medication?"按已有安排填写明确时刻，每次记录独立保留。":"设置后自动恢复提醒，确认完成后重新计算间隔。",14,color:"Color.textSecondary",margin:new(0,8,0,0)));DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);
  var emptyErrorStyle=new Style(typeof(TextBlock),(Style)Application.Current.FindResource(typeof(TextBlock)));var emptyError=new Trigger{Property=TextBlock.TextProperty,Value=""};emptyError.Setters.Add(new Setter(VisibilityProperty,Visibility.Collapsed));emptyErrorStyle.Triggers.Add(emptyError);SummaryError.Style=emptyErrorStyle;var footer=new StackPanel{Margin=new(0,16,0,0)};footer.Children.Add(DiscardNotice);Preview.Margin=new(0,0,0,8);footer.Children.Add(Preview);SummaryError.Margin=new(0,0,0,8);footer.Children.Add(SummaryError);var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};actions.Children.Add(CancelButton);SaveButton.Margin=new(8,0,0,0);actions.Children.Add(SaveButton);footer.Children.Add(Pair(EnabledInput,actions,16));DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
  BodyScroll.Margin=new(-4);var fields=new StackPanel{Margin=new(4)};fields.Children.Add(Field(medication?"药物名称":"提醒名称",NameInput));
  IntervalSection=Field("间隔分钟（1 至 1440）",IntervalInput);DurationSection=Field("活动时长（秒，1 至 3600）",DurationInput);TimeSection=new StackPanel();((StackPanel)TimeSection).Children.Add(Text("每日明确时刻",18,true,margin:new(0,0,0,8)));((StackPanel)TimeSection).Children.Add(Text(medication?"次数只决定时刻数量，不自动换算服药间隔。":"每天按这些时刻提醒，完成确认不移动后续计划。",12,color:"Color.textSecondary",margin:new(0,0,0,12)));((StackPanel)TimeSection).Children.Add(TimeFields);
  if(medication){fields.Children.Add(TimeSection);var dateLabels=Pair(Text("开始日期",bold:true),HasEnd);((FrameworkElement)dateLabels.Children[0]).VerticalAlignment=VerticalAlignment.Center;dateLabels.MinHeight=32;dateLabels.Margin=new(0,0,0,8);fields.Children.Add(dateLabels);fields.Children.Add(Pair(Field("",Starts),Field("",Ends)));}
  else{fields.Children.Add(Pair(Field("要提醒做什么",KindInput),IntervalSection));fields.Children.Add(TimeSection);fields.Children.Add(DurationSection);}
  fields.Children.Add(Field("提醒方式",StyleInput));
  if(!medication){var advanced=new StackPanel{Margin=new(0,16,0,0)};advanced.Children.Add(Field("循环方式",ScheduleInput));advanced.Children.Add(Workdays);UseWindow.Margin=new(0,8,0,16);advanced.Children.Add(UseWindow);WindowSection=Pair(Field("开始时刻 HH:mm",WindowStart),Field("结束时刻 HH:mm",WindowEnd));advanced.Children.Add(WindowSection);More.Content=advanced;More.Margin=new(0,0,0,24);fields.Children.Add(More);}else WindowSection=new Border();
  fields.Children.Add(Field(medication?"已有用药说明（可选）":"备注（可选）",NoteInput));BodyScroll.Content=fields;layout.Children.Add(BodyScroll);Surface.ContentHost.Child=layout;Content=root;
  foreach(var input in new[]{NameInput,IntervalInput,DurationInput,NoteInput,WindowStart,WindowEnd})input.TextChanged+=(_,_)=>UpdatePreview();KindInput.SelectionChanged+=(_,_)=>{AdjustFields();UpdatePreview();};ScheduleInput.SelectionChanged+=(_,_)=>{AdjustFields();UpdatePreview();};StyleInput.SelectionChanged+=(_,_)=>UpdatePreview();TimeFields.Changed+=UpdatePreview;foreach(var check in new[]{EnabledInput,Workdays,UseWindow,HasEnd})check.Click+=(_,_)=>{Ends.IsEnabled=HasEnd.IsChecked==true;AdjustFields();UpdatePreview();};Starts.SelectedDateChanged+=(_,_)=>UpdatePreview();Ends.SelectedDateChanged+=(_,_)=>UpdatePreview();More.Expanded+=(_,_)=>More.Header="收起设置";More.Collapsed+=(_,_)=>More.Header="更多设置";
  Ends.AdditionalError=()=>!_submitted||HasEnd.IsChecked!=true?null:Ends.SelectedDate==null?"请选择有效的结束日期。":Ends.SelectedDate is {} last&&Starts.SelectedDate is {} first&&last<first?"结束日期不能早于开始日期。":null;Closing+=OnClosing;PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&!KindInput.IsDropDownOpen&&!StyleInput.IsDropDownOpen&&!ScheduleInput.IsDropDownOpen&&!Starts.IsDropDownOpen&&!Ends.IsDropDownOpen){RequestCancel();e.Handled=true;}};Activated+=(_,_)=>RefreshMaterial();Deactivated+=(_,_)=>RefreshMaterial();_building=false;AdjustFields();UpdatePreview();Loaded+=(_,_)=>{_baseline=CaptureDraft();RefreshMaterial();NameInput.Focus();};
 }
 public void RefreshMaterial(){if(Application.Current.TryFindResource("Theme.ambient") is SolidColorBrush brush)Ambient.Environment=brush.Color;Ambient.InvalidateVisual();ApplyMaterial(Surface,IsActive);}
 private void AdjustFields(){IntervalSection.Visibility=!_medication&&ScheduleInput.SelectedIndex==0?Visibility.Visible:Visibility.Collapsed;TimeSection.Visibility=_medication||ScheduleInput.SelectedIndex==1?Visibility.Visible:Visibility.Collapsed;DurationSection.Visibility=!_medication&&KindInput.SelectedIndex==1?Visibility.Visible:Visibility.Collapsed;WindowSection.Visibility=UseWindow.IsChecked==true?Visibility.Visible:Visibility.Collapsed;}
 private ReminderPlan? Read(bool show){if(_reading)return null;_reading=true;try{return ReadCore(show);}finally{_reading=false;}}
 private ReminderPlan? ReadCore(bool show)
 {
  var valid=true;void Check(DependencyObject control,bool condition,string error){valid&=condition;if(show)FieldState.SetError(control,condition?"":error);}
  Check(NameInput,!string.IsNullOrWhiteSpace(NameInput.Text)&&NameInput.Text.Trim().Length<=80,"请填写 1 至 80 字的名称。");Check(NoteInput,NoteInput.Text.Length<=1000,"说明最多 1000 字。");
  var p=PreviewSession.Clone(_original);p.NextDue=null;p.Name=NameInput.Text.Trim();p.Kind=_medication?HabitKind.Medication:_kinds[KindInput.SelectedIndex];p.Schedule=_medication?ScheduleKind.FixedTimes:(ScheduleKind)ScheduleInput.SelectedIndex;p.Style=(ReminderStyle)StyleInput.SelectedIndex;p.Enabled=EnabledInput.IsChecked==true;p.Note=NoteInput.Text;
  if(p.Schedule==ScheduleKind.Interval){Check(IntervalInput,int.TryParse(IntervalInput.Text,out var interval)&&interval>=1&&interval<=1440,"请输入 1 至 1440 的整数分钟。");p.IntervalMinutes=interval;}else{valid=TimeFields.Validate(show)&&valid;p.Times=TimeFields.Values();}
  if(p.Kind==HabitKind.Activity){Check(DurationInput,int.TryParse(DurationInput.Text,out var seconds)&&seconds>=1&&seconds<=3600,"请输入 1 至 3600 的整数秒数。");p.ActivitySeconds=seconds;}
  p.WorkdaysOnly=!_medication&&Workdays.IsChecked==true;p.WindowStart=null;p.WindowEnd=null;
  if(!_medication&&UseWindow.IsChecked==true){var from=TimeOnly.TryParseExact(WindowStart.Text,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var first);var to=TimeOnly.TryParseExact(WindowEnd.Text,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var last);Check(WindowStart,from,"请输入 HH:mm，如 09:00。");Check(WindowEnd,to&&(!from||last>first),"同一天内结束时刻须晚于开始。");p.WindowStart=WindowStart.Text;p.WindowEnd=WindowEnd.Text;}
  if(_medication){var startOk=show?Starts.TryCommit():DateTime.TryParseExact(Starts.Input?.Text??Starts.SelectedDate?.ToString("yyyy-MM-dd"),new[]{"yyyy-MM-dd","yyyy/MM/dd"},CultureInfo.InvariantCulture,DateTimeStyles.None,out _);valid&=startOk;if(Starts.SelectedDate is {} start)p.StartsOn=DateOnly.FromDateTime(start);if(HasEnd.IsChecked==true){var endOk=show?Ends.TryCommit():Ends.SelectedDate!=null;valid&=endOk;if(endOk&&Ends.SelectedDate is {} end){p.EndsOn=DateOnly.FromDateTime(end);Check(Ends,p.EndsOn>=p.StartsOn,"结束日期不能早于开始日期。");}else{Check(Ends,false,"请选择有效的结束日期。");}}else{p.EndsOn=null;if(show)FieldState.SetError(Ends,"");}}
  if(!valid)return null;try{_session.Engine.Validate(p);return p;}catch(ArgumentException ex){if(show)SummaryError.Text=ex.Message;return null;}
 }
 public void UpdatePreview()
 {
  if(_building||_saving)return;SaveButton.Content=_new?(EnabledInput.IsChecked==true?"保存并启用":"保存，保持停用"):"保存修改";var p=Read(_submitted);
  if(p==null){Preview.Text="填写有效内容后显示首次提醒时间。";return;}
  try{var times=_session.Engine.Preview(p,_session.Now,p.Schedule==ScheduleKind.Interval?1:3);Preview.Text=!p.Enabled?"保存后保持停用，不产生新提醒。":p.Kind!=HabitKind.Medication&&_session.Engine.State.PauseUntil!=null?"普通提醒保持暂停，恢复后再计算间隔。":times.Count==0?"日期范围内没有未来时刻，仍可保存计划。":(p.Schedule==ScheduleKind.Interval?"首次提醒：":"接下来：")+string.Join(" / ",times.Select(t=>_session.Engine.Local(t).ToString("MM-dd HH:mm")))+(p.Schedule==ScheduleKind.FixedTimes?"。各时刻独立，晚确认不平移后续计划。":"。确认完成后重新计算。");if(p.Enabled&&_session.Engine.State.MeetingUntil!=null&&p.Kind!=HabitKind.Medication)Preview.Text+=" 会议模式保持。";}catch(ArgumentException ex){Preview.Text=ex.Message;}
 }
 public bool TrySave()
 {
  if(_saving||Saved)return false;_submitted=true;SummaryError.Text="";var plan=Read(true);if(plan==null){SummaryError.Text="未保存，请修正对应字段。输入已保留。";var error=FindError();error?.BringIntoView();error?.Focus();return false;}
  _saving=true;SaveButton.IsEnabled=false;try{_session.Engine.SavePlan(plan,_session.Now,true);Saved=true;SavedName=plan.Name;_allowClose=true;Close();return true;}catch(Exception ex){SummaryError.Text="未保存："+ex.Message;return false;}finally{_saving=false;if(!Saved)SaveButton.IsEnabled=true;}
 }
 private FrameworkElement? FindError()=>new FrameworkElement[]{NameInput,IntervalInput,DurationInput,NoteInput,WindowStart,WindowEnd,Starts,Ends}.Concat(TimeFields.Inputs).FirstOrDefault(x=>x.IsVisible&&FieldState.GetHasError(x));
 public void RequestCancel(){if(!IsDirty){_allowClose=true;Close();}else{DiscardNotice.Visibility=Visibility.Visible;DiscardButton.Focus();}}
 private void OnClosing(object? sender,CancelEventArgs e){if(!_allowClose&&!VerificationClose&&IsDirty){e.Cancel=true;DiscardNotice.Visibility=Visibility.Visible;DiscardButton.Focus();}}
 private string CaptureDraft()=>JsonSerializer.Serialize(new{ NameInput.Text,kind=KindInput.SelectedIndex,schedule=ScheduleInput.SelectedIndex,interval=IntervalInput.Text,duration=DurationInput.Text,style=StyleInput.SelectedIndex,note=NoteInput.Text,enabled=EnabledInput.IsChecked,workdays=Workdays.IsChecked,useWindow=UseWindow.IsChecked,start=WindowStart.Text,end=WindowEnd.Text,times=TimeFields.Values(),starts=Starts.Input?.Text??Starts.SelectedDate?.ToString("yyyy-MM-dd"),hasEnd=HasEnd.IsChecked,ends=Ends.Input?.Text??Ends.SelectedDate?.ToString("yyyy-MM-dd")??""});
}
