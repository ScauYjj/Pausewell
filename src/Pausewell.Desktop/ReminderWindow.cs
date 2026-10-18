using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
using Pausewell.Controls;
using Pausewell.Presentation;

namespace Pausewell.Desktop;

public sealed class ReminderWindow : MaterialWindow
{
    private readonly DesktopController _app;private readonly bool _preview,_strong;
    private readonly List<Occurrence> _remaining;private readonly List<Window> _masks=[];
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string,(Stopwatch Clock,TextBlock Label,Button Complete,int Seconds)> _activity=[];
    private readonly TextBlock _feedback=TaskUi.Error();
    private readonly StackPanel _primaryActions=new();
    private Button? _historyAction;
    private readonly Button _escape=new(){Content="长按 2 秒，紧急退出"};private Stopwatch? _hold;private bool _allowClose,_fullScreenPlaced;
    public FrameworkElement CaptureView { get; }
    public bool IsPreview=>_preview;public int MaskCount=>_masks.Count;public bool Strong=>_strong;
    public bool HasMedication=>_remaining.Any(o=>o.Kind==HabitKind.Medication);
    public ReminderWindow(DesktopController app,IReadOnlyList<Occurrence> items,bool preview)
    {
        _app=app;_preview=preview;_remaining=items.ToList();_strong=items.Any(o=>o.Style==ReminderStyle.Fullscreen);Title=preview?"间歇 · 外观预览":"间歇 · 到期提醒";Topmost=true;Width=items.Count==1?520:680;Height=720;MinWidth=430;MaxHeight=SystemParameters.WorkArea.Height-24;WindowStartupLocation=WindowStartupLocation.CenterScreen;

        if(_strong){WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;MaxHeight=double.PositiveInfinity;}
        var body=new StackPanel();
        foreach(var item in items){var card=Ui.Panel(ItemCard(item),new(20));card.Margin=new(0,0,0,12);body.Children.Add(card);}
        var secondaryActions=new WrapPanel { Margin=new(0,8,0,0) };if(_historyAction!=null)secondaryActions.Children.Add(_historyAction);secondaryActions.Children.Add(Ui.Button(preview?"结束预览":"5 分钟后再提醒",ExitWithDeferral,id:"DeferReminder"));
        if(!preview)secondaryActions.Children.Add(Ui.Button("我要开会",()=>{app.Meeting(30);}));
        if(_strong)
        {
            _escape.PreviewMouseLeftButtonDown+=(_,e)=>{BeginEmergencyHold();_escape.CaptureMouse();e.Handled=true;};_escape.PreviewMouseLeftButtonUp+=(_,e)=>{EndEmergencyHold();_escape.ReleaseMouseCapture();e.Handled=true;};_escape.LostMouseCapture+=(_,_)=>EndEmergencyHold();
            _escape.PreviewKeyDown+=(_,e)=>{if(e.Key is Key.Space or Key.Enter){if(_hold==null)BeginEmergencyHold();e.Handled=true;}};_escape.PreviewKeyUp+=(_,e)=>{if(e.Key is Key.Space or Key.Enter){EndEmergencyHold();e.Handled=true;}};_escape.LostKeyboardFocus+=(_,_)=>EndEmergencyHold();AutomationProperties.SetAutomationId(_escape,"EmergencyExit");_escape.Margin=new(0,12,0,0);
        }
        var footer=new StackPanel();footer.Children.Add(_feedback);if(items.Count==1)footer.Children.Add(_primaryActions);footer.Children.Add(secondaryActions);
        if(_strong)footer.Children.Add(_escape);footer.Children.Add(Ui.Text(preview?"预览不会写入执行记录。":"延期只改变这次提醒时间，不计为完成。",12,brush:"Muted",margin:new(0,12,0,0)));
        SetBody(TaskUi.Layout(preview?"提醒预览":items.Count==1?"到提醒时间了":$"{items.Count} 项待处理",preview?"可体验操作，关闭后返回，计划和记录保持不变。":"完成对应事项后再确认，需要时可延期处理。",body,footer));
        if(_strong)CenterReadingSurface(800);CaptureView=MaterialRoot;Closing+=OnClosing;
        _timer=new(){Interval=TimeSpan.FromMilliseconds(100)};_timer.Tick+=(_,_)=>Progress();_timer.Start();Closed+=(_,_)=>{_timer.Stop();foreach(var mask in _masks)mask.Close();};
        Loaded+=(_,_)=>{if(!_strong||_fullScreenPlaced)return;_fullScreenPlaced=true;var selected=Forms.Screen.FromPoint(Forms.Cursor.Position);NativeWindows.Place(this,selected);if(app.Engine.State.CoverAllScreens){foreach(var screen in Forms.Screen.AllScreens.Where(s=>s.DeviceName!=selected.DeviceName)){var mask=new Window { WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Content=new TextBlock { Text="间歇 · 请在主提醒窗口处理",Foreground=Brushes.White,FontSize=20,Margin=new(60) } };mask.SetResourceReference(BackgroundProperty,"Theme.materialShade");mask.Show();NativeWindows.Place(mask,screen);_masks.Add(mask);}Activate();}};
    }
    private FrameworkElement ItemCard(Occurrence item)
    {
        var panel=new StackPanel { Margin=new(0,0,0,12) };
        if(_remaining.Count==1){var circle=Ui.IconCircle(item.Kind,76);circle.HorizontalAlignment=HorizontalAlignment.Center;circle.Margin=new(0,6,0,20);panel.Children.Add(circle);}
        var heading=item.Kind switch{HabitKind.Water=>"该喝水了",HabitKind.Activity=>"起来活动一下",HabitKind.Toilet=>"看看是否需要如厕",HabitKind.Medication=>item.Status==OccurrenceStatus.NeedsReview?"核对本次服药记录":"核对本次用药安排",_=>item.PlanName};panel.Children.Add(Ui.Text(heading,28,true));
        if(item.Kind==HabitKind.Medication)panel.Children.Add(Ui.Text(item.PlanName,19,true,margin:new(0,10,0,0)));
        panel.Children.Add(Ui.Text($"原计划 {_app.Engine.Local(item.DueAt):MM-dd HH:mm}",14,brush:"Muted",margin:new(0,10,0,16)));
        if(item.Kind==HabitKind.Water)panel.Children.Add(Ui.Text("方便时喝点水，完成后再确认。",16));
        var plan=_app.Engine.State.Plans.FirstOrDefault(p=>p.Id==item.PlanId);
        if(plan?.Schedule==ScheduleKind.Once)panel.Children.Add(Ui.Text("仅这一次，确认后不再循环。",13,brush:"Muted",margin:new(0,10,0,0)));
        if(plan?.Schedule==ScheduleKind.Interval)panel.Children.Add(Ui.Text($"确认完成后，再等 {plan.IntervalMinutes} 分钟提醒。",13,brush:"Muted",margin:new(0,10,0,0)));
        if(item.Note.Length>0)panel.Children.Add(Ui.Text(item.Note,13,brush:"Muted",margin:new(0,8,0,12)));
        if(item.Kind==HabitKind.Medication)
        {
            var last=_app.Engine.State.Occurrences.Where(o=>o.PlanId==item.PlanId&&o.Status==OccurrenceStatus.Completed).OrderByDescending(o=>o.ResolvedAt).FirstOrDefault();
            panel.Children.Add(Ui.Text(last?.ResolvedAt is {} confirmed?$"最近确认：{_app.Engine.Local(confirmed):MM-dd HH:mm}"+(last.ActualAt is {} actual?$"，实际 {_app.Engine.Local(actual):MM-dd HH:mm}":"，实际时刻未记录"):"没有已确认的历史记录。",13,brush:"Muted",margin:new(0,0,0,14)));
            if(item.Status==OccurrenceStatus.NeedsReview)panel.Children.Add(Ui.Notice("缺少确认不代表实际未服用。请核对后记录本次情况。"));
        }
        var actions=new StackPanel();var complete=Ui.Button(item.Kind==HabitKind.Medication?"已服用，填写记录":item.Kind==HabitKind.Water?"确认已喝水":"确认已完成",()=>{},true,"Complete_"+item.Kind);complete.Margin=new(0,12,0,0);complete.Click+=(_,_)=>{if(item.Kind==HabitKind.Medication){var dialog=new MedicineConfirmation(_app,item,_preview,()=>Resolve(item,OccurrenceStatus.Completed,panel,recordAlreadySaved:true)){Owner=this};dialog.ShowDialog();}else Resolve(item,OccurrenceStatus.Completed,panel);};
        if(item.Kind==HabitKind.Activity)
        {
            complete.IsEnabled=false;var label=Ui.Text($"活动时长 {item.ActivitySeconds} 秒",15,margin:new(0,8,0,12));panel.Children.Add(label);var start=Ui.Button("开始活动计时",()=>{},id:"StartActivity");start.Click+=(_,_)=>{_activity[item.Id]=(Stopwatch.StartNew(),label,complete,item.ActivitySeconds);start.IsEnabled=false;};actions.Children.Add(start);
        }
        actions.Children.Add(complete);
        if(item.Kind==HabitKind.Toilet)actions.Children.Add(Ui.Button("当前无需",()=>Resolve(item,OccurrenceStatus.NotNeeded,panel),id:"NoNeed"));
        if(item.Kind==HabitKind.Medication)
        {
            var alternatives=new WrapPanel();alternatives.Children.Add(Ui.Button("记录本次未服用",()=>Resolve(item,OccurrenceStatus.Skipped,panel),id:"MedicationSkipped"));alternatives.Children.Add(Ui.Button("不确定，保留待核实",()=>Resolve(item,OccurrenceStatus.NeedsReview,panel),id:"MedicationUnsure"));actions.Children.Add(alternatives);
            var history=Ui.Button("查看执行记录",()=>{new ReminderHistoryWindow(_app,item,_preview){Owner=this}.ShowDialog();if(IsVisible)Activate();},id:"ViewReminderHistory");if(_remaining.Count==1)_historyAction=history;else actions.Children.Add(history);
        }
        var actionIndex=0;foreach(var action in actions.Children.OfType<FrameworkElement>())action.Margin=new(0,actionIndex++==0?0:8,0,0);
        if(_remaining.Count==1){foreach(var action in actions.Children.Cast<UIElement>().ToArray()){actions.Children.Remove(action);_primaryActions.Children.Add(action);}}else panel.Children.Add(actions);
        if(_remaining.Count==1&&item.Kind!=HabitKind.Medication)foreach(var label in panel.Children.OfType<TextBlock>())label.TextAlignment=TextAlignment.Center;
        return panel;
    }
    private void Resolve(Occurrence item,OccurrenceStatus result,FrameworkElement panel,bool recordAlreadySaved=false)
    {
        try
        {
            var now=DateTimeOffset.UtcNow;
            if(!_preview&&!recordAlreadySaved)_app.Engine.Resolve(item.Id,result,now,result==OccurrenceStatus.Completed?now:null);
            if(!_preview)_app.Diagnostics.Resolved(item.Id,result);
            _remaining.RemoveAll(o=>o.Id==item.Id);_activity.Remove(item.Id);panel.IsEnabled=false;panel.Opacity=.55;
            _feedback.Text="已记录："+Labels.Status(result);if(_remaining.Count==0){_allowClose=true;Close();}
        }
        catch(Exception ex){_feedback.Text="记录尚未保存，请重试保存记录。\n"+ex.Message;}
    }
    public void BeginEmergencyHold()=>_hold=Stopwatch.StartNew();
    public void EndEmergencyHold(){_hold=null;_escape.Content="长按 2 秒，紧急退出";}
    private void Progress()
    {
        foreach(var value in _activity.Values){var remaining=Math.Max(0,value.Seconds-(int)value.Clock.Elapsed.TotalSeconds);value.Label.Text=remaining==0?"计时结束，请确认是否实际完成。":$"还剩 {remaining} 秒";value.Complete.IsEnabled=remaining==0;}
        if(_hold!=null){_escape.Content=$"继续按住… {Math.Max(0,2-_hold.Elapsed.TotalSeconds):0.0} 秒";if(_hold.Elapsed.TotalSeconds>=2)ExitWithDeferral();}
    }
    private void ExitWithDeferral()
    {
        try{if(!_preview){_app.Engine.Defer(_remaining.Select(o=>o.Id),DateTimeOffset.UtcNow);_app.Diagnostics.Deferred(_remaining.Count);}_allowClose=true;Close();}catch(Exception ex){_feedback.Text="未保存延期状态："+ex.Message;EndEmergencyHold();}
    }
    private void OnClosing(object? sender,CancelEventArgs e)
    {
        if(_allowClose)return;if(_strong){e.Cancel=true;_feedback.Text="请明确处理，或长按紧急退出。";return;}
        if(!_preview)try{_app.Engine.Defer(_remaining.Select(o=>o.Id),DateTimeOffset.UtcNow);}catch(Exception ex){e.Cancel=true;_feedback.Text="未保存关闭状态："+ex.Message;}
    }
    public void CloseWithoutAction(){_allowClose=true;Close();}
}
