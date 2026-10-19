using Pausewell.MaterialLab;
using Pausewell.Presentation;
using System.Globalization;
using System.Windows.Automation;

namespace Pausewell.Desktop;

public partial class MainWindow : Window
{
    private readonly DesktopController _app;
    private string _page="Today", _signature="", _notice="";
    private TextBlock? _status, _nextName, _nextTime, _nextHint, _modeText;
    private TextBlock? _activityProgress;
    private string? _inlineActivityId;
    private Border? _nextIcon;
    private readonly Dictionary<string,Button> _nav=[];
    private readonly Dictionary<string,Border> _navFaces=[];
    private readonly GlassSurface _navigationSurface,_readingSurface;
    public MainWindow(DesktopController app)
    {
        _app=app;InitializeComponent();Title="间歇 · Windows v"+typeof(App).Assembly.GetName().Version!.ToString(3);
        _navigationSurface=new GlassSurface(AmbientLayer,PageUi.Profiles["navigation"]){Width=592,Height=56,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};_navigationSurface.ContentHost.Padding=new(4);NavigationHost.Children.Remove(NavigationBar);_navigationSurface.ContentHost.Child=NavigationBar;NavigationBar.HorizontalAlignment=HorizontalAlignment.Center;NavigationHost.Children.Add(_navigationSurface);
        _readingSurface=new GlassSurface(AmbientLayer,PageUi.Profiles["content"]);_readingSurface.ContentHost.Padding=new(24);ReadingHost.Children.Remove(ReadingLayout);_readingSurface.ContentHost.Child=ReadingLayout;ReadingHost.Children.Add(_readingSurface);Activated+=(_,_)=>RefreshMaterial();Deactivated+=(_,_)=>RefreshMaterial();
        Pausewell.Controls.DesignTheme.Changed+=RefreshMaterial;Closed+=(_,_)=>Pausewell.Controls.DesignTheme.Changed-=RefreshMaterial;
        foreach(var (page,title,icon) in new[]{("Today","提醒","reminder"),("Medicine","服药","medicine"),("History","记录","history"),("Settings","设置","settings")})
        {
            var b=Ui.Button(title,()=>Navigate(page),id:"Nav"+page);b.SetResourceReference(StyleProperty,"NavigationButton");b.FocusVisualStyle=(Style)FindResource("PillFocusRing");b.BorderThickness=new(0);b.Padding=new(12,8,12,8);b.Margin=new(0);b.Width=144;b.Height=48;
            var row=Ui.Actions(Ui.Icon(icon),Ui.Text(title,15));row.Children[1].SetValue(MarginProperty,new Thickness(10,0,0,0));b.Content=row;_nav[page]=b;var face=new Border{CornerRadius=new(24),Child=b};_navFaces[page]=face;NavigationBar.Children.Add(face);
        }
        Closing+=(_,e)=>{ if(_app.Quitting)return; e.Cancel=true; if(_app.Engine.State.Preferences.CloseToTray)Hide();else Dispatcher.BeginInvoke(()=>_app.Quit()); };
        Navigate("Today");
    }
    private void RefreshMaterial(){if(TryFindResource("Theme.ambient") is SolidColorBrush brush)AmbientLayer.Environment=brush.Color;if(TryFindResource("Theme.toggle") is SolidColorBrush accent)AmbientLayer.Accent=accent.Color;PageUi.ApplyMaterial(_navigationSurface,IsActive);PageUi.ApplyMaterial(_readingSurface,IsActive);}
    public void ReportError(string message){ErrorText.Text=message;ErrorBox.Visibility=Visibility.Visible;}
    public void ClearError()=>ErrorBox.Visibility=Visibility.Collapsed;
    private void RetryScheduling_Click(object sender,RoutedEventArgs e)=>_app.RetryScheduling();
    public void Feedback(string message){_notice=message;Refresh();}
    public void Navigate(string page)
    {
        if(_page=="Settings"){Theme.ClearPreview(Resources);_settingsLoaded=false;}
        _page=page=="Plans"?"Today":page;_notice="";
        foreach(var (key,b) in _nav){if(key==_page)_navFaces[key].SetResourceReference(Border.BackgroundProperty,"Theme.navigationSelection");else _navFaces[key].Background=Brushes.Transparent;b.FontWeight=key==_page?FontWeights.SemiBold:FontWeights.Normal;}
        Refresh();
    }
    public void RefreshIfChanged()
    {
        var s=_app.Engine.State;var key=$"{s.Running}:{s.IsAway}:{s.RecoverySummaryVisible}:{s.MeetingUntil}:{s.PauseUntil}:{s.BufferUntil>DateTimeOffset.UtcNow}:{s.ShowMeetingSummary}:{s.Plans.Count}:{s.Occurrences.Count}:{s.Audit.LastOrDefault()?.At}:{string.Join(',',s.Occurrences.Select(o=>o.Status+":"+o.Activity?.State))}";
        if(key==_signature)return;_signature=key;if(_page=="History")RefreshHistoryRows();else if(_page!="Settings")Refresh();
    }
    public void Refresh()
    {
        RefreshMaterial();
        _status=_nextName=_nextTime=_nextHint=_modeText=_activityProgress=null;_inlineActivityId=null;
        PageHost.Content=_page switch{"Medicine"=>MedicinePage(),"History"=>HistoryPage(),"Settings"=>SettingsPage(),_=>HomePage()};
        RefreshClock();
    }
    public void RefreshClock()
    {
        var e=_app.Engine;var s=e.State;var now=DateTimeOffset.UtcNow;
        if(_activityProgress!=null&&_inlineActivityId!=null){var o=s.Occurrences.FirstOrDefault(o=>o.Id==_inlineActivityId);if(o!=null){var remaining=Math.Max(0,o.ActivitySeconds-(int)e.ActivityElapsed(o.Id,now));_activityProgress.Text=o.Activity?.State==ActivityState.Running?$"剩余辅助计时 {remaining/60:00}:{remaining%60:00}":o.Activity?.State==ActivityState.Interrupted?$"已计时 {(int)e.ActivityElapsed(o.Id,now)} 秒，中断期间未计入。":"辅助计时可选，按实际情况确认。";}}
        SchedulerFaultBox.Visibility=_app.SchedulingFaulted?Visibility.Visible:Visibility.Collapsed;
        if(_app.SchedulingFaulted)SchedulerFaultText.Text="提醒调度暂缓："+_app.SchedulingFailure+"\n修复后点击重试，期间不会自动记录完成。";
        if(_status!=null)_status.Text=s.IsAway?"离开一下":s.MeetingUntil>now?"会议中":s.PauseUntil>now?"普通提醒已暂停":!s.Running?"提醒已停止":s.Plans.Any(p=>p.Enabled&&p.Kind!=HabitKind.Medication)?"提醒中":"暂无运行中的提醒";
        if(_modeText!=null)_modeText.Text=s.MeetingUntil is {} m&&m>now?$"会议至 {e.Local(m):HH:mm}，普通事项会后汇总。":s.PauseUntil is {} p&&p>now?$"普通提醒暂停至 {e.Local(p):HH:mm}。"+(s.PauseMedication?"服药提示也暂停。":"服药按原计划提示。"):s.BufferUntil>now?"会议已结束，收尾 30 秒后展示待办。":$"当前有 {s.Plans.Count(p=>p.Enabled&&p.Kind!=HabitKind.Medication)} 条普通提醒在运行。";
        if(_nextName!=null)
        {
            var pending=s.Occurrences.Where(o=>o.Kind!=HabitKind.Medication&&o.Purpose==PlanPurpose.Habit&&o.Status is OccurrenceStatus.Pending or OccurrenceStatus.Deferred).OrderBy(o=>o.DueAt).FirstOrDefault();
            var next=s.Plans.Where(p=>p.Enabled&&p.IsHabit).Select(p=>(Plan:p,At:Next(p,now))).Where(x=>x.At!=null).OrderBy(x=>x.At).FirstOrDefault();
            if(s.PauseUntil>now){_nextName.Text="恢复后重新计时";_nextTime!.Text=e.Local(s.PauseUntil.Value).ToString("HH:mm");_nextHint!.Text="普通间隔从恢复时刻计算，固定时刻保留原计划。";}
            else if(pending!=null){SetNextIcon(pending.Kind);_nextName.Text=pending.PlanName;_nextTime!.Text="待处理";_nextHint!.Text=$"原计划 {e.Local(pending.DueAt):HH:mm} · "+(s.MeetingUntil>now?"会后汇总处理":pending.Status==OccurrenceStatus.Deferred?$"{e.Local(pending.NotifyAfter):HH:mm} 再提醒":"完成或延期后会保留本次结果。");}
            else if(next.Plan!=null){SetNextIcon(next.Plan.Kind);_nextName.Text=next.Plan.Name;_nextTime!.Text=e.Local(next.At!.Value).ToString(next.At.Value.ToOffset(e.Local(now).Offset).Date==e.Local(now).Date?"HH:mm":"MM-dd HH:mm");_nextHint!.Text=$"还有 {Math.Max(1,Math.Ceiling((next.At.Value-now).TotalMinutes))} 分钟 · "+(next.Plan.Schedule==ScheduleKind.Interval?$"确认完成后再等 {next.Plan.IntervalMinutes} 分钟。":next.Plan.Schedule==ScheduleKind.Once?"仅一次，确认后不再循环。":"每天固定时刻，晚确认不平移后续计划。");}
            else{_nextName.Text="暂无普通提醒";_nextTime!.Text="";_nextHint!.Text="新增一个提醒，或开启已有事项。";}
        }
        if(_nextTime!=null)_nextTime.FontSize=_nextTime.Text.Contains(" ")?24:34;
        if(_app.SchedulingFaulted){if(_status!=null)_status.Text="提醒调度暂缓";if(_modeText!=null)_modeText.Text="已启用的计划和待办保留，点击上方“重试调度”恢复。";}
        RuntimeLabel.Text=(_app.IsIsolated?"独立试用数据 · 真实提醒可触发 · ":"")+(s.Preferences.CloseToTray?"关闭窗口后继续在托盘运行；退出应用后不再提醒。":"关闭窗口将退出应用，退出后不再提醒。");
    }
    private void SetNextIcon(HabitKind kind){if(_nextIcon!=null&&!Equals(_nextIcon.Tag,kind)){_nextIcon.Tag=kind;_nextIcon.Child=Ui.KindIcon(kind,34);}}
    private DateTimeOffset? Next(ReminderPlan p,DateTimeOffset now)
    {
        if(!p.Enabled)return null;
        if(p.EndsOn is {} end&&DateOnly.FromDateTime(_app.Engine.Local(now).Date)>end)return null;
        return p.Schedule==ScheduleKind.Once?p.OnceAt:p.Schedule==ScheduleKind.Interval?p.NextDue:_app.Engine.Preview(p,now,1).Cast<DateTimeOffset?>().FirstOrDefault();
    }
    private UIElement HomePage()
    {
        var body=new StackPanel();var s=_app.Engine.State;
        var header=new Grid();header.ColumnDefinitions.Add(new());header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var labels=new StackPanel();_status=Ui.Text("",28,true);_modeText=Ui.Text("",15,brush:"Muted",margin:new(0,8,20,0));labels.Children.Add(_status);labels.Children.Add(_modeText);header.Children.Add(labels);
        var actions=Ui.Actions(Ui.Button(s.IsAway?"我回来了":"离开一下",_app.ToggleAway,id:"ToggleAway"),Ui.Button("我要开会",()=>OpenMode(false),id:"Meeting30"),Ui.Button("暂停",()=>OpenMode(true),id:"PauseOrdinary"));actions.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(actions,1);header.Children.Add(actions);body.Children.Add(header);Ui.Gap(body,20);
        AddRecoverySurfaces(body);
        if(s.IsAway){body.Children.Add(TaskUi.Summary("回来后，继续照顾自己","日常习惯暂时不打扰。返回后重新安排，旧提醒不会变成补办任务。待办与服药记录保留。"));return Ui.Scroll(body);}
        if(_notice.Length>0)body.Children.Add(Ui.Notice(_notice));
        if(s.Plans.Count==0)
        {
            var intro=new StackPanel();intro.Children.Add(Ui.Text("设置一次，以后打开自动恢复提醒",22,true));intro.Children.Add(Ui.Text("先从喝水、活动或如厕选一件事，确认时间后即可开始。",14,brush:"Muted",margin:new(0,12,0,22)));
            intro.Children.Add(Ui.Actions(Ui.Button("设置喝水提醒",()=>EditPlan(null,HabitKind.Water),true,"FirstWater"),Ui.Button("起来活动",()=>EditPlan(null,HabitKind.Activity)),Ui.Button("如厕提醒",()=>EditPlan(null,HabitKind.Toilet))));body.Children.Add(Ui.Panel(intro,new(28)));return Ui.Scroll(body);
        }
        if(s.MeetingUntil!=null||s.PauseUntil!=null)body.Children.Add(Ui.Actions(Ui.Button("延长 15 分钟",ExtendMode,id:"ExtendMode"),Ui.Button("提前结束",EndMode,id:"EndMode")));
        Ui.Gap(body,8);var nextrow=new Grid();nextrow.ColumnDefinitions.Add(new(){Width=new(76)});nextrow.ColumnDefinitions.Add(new());nextrow.ColumnDefinitions.Add(new(){Width=new(142)});
        _nextIcon=Ui.IconCircle(HabitKind.Water,58);_nextIcon.VerticalAlignment=VerticalAlignment.Center;_nextIcon.HorizontalAlignment=HorizontalAlignment.Left;nextrow.Children.Add(_nextIcon);var nextLabels=new StackPanel();nextLabels.Children.Add(Ui.Text("下一次日常提醒",12,brush:"Muted"));_nextName=Ui.Text("",18,true,margin:new(0,4,0,0));_nextHint=Ui.Text("",12,brush:"Muted",margin:new(0,7,14,0));nextLabels.Children.Add(_nextName);nextLabels.Children.Add(_nextHint);Grid.SetColumn(nextLabels,1);nextrow.Children.Add(nextLabels);_nextTime=Ui.Text("",34);_nextTime.FontFamily=new("Segoe UI");_nextTime.TextAlignment=TextAlignment.Right;_nextTime.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(_nextTime,2);nextrow.Children.Add(_nextTime);body.Children.Add(Ui.Panel(nextrow,new(22)));
        var pending=s.Occurrences.Where(o=>o.Kind!=HabitKind.Medication&&o.Status is OccurrenceStatus.Pending or OccurrenceStatus.Deferred).ToList();
        if(pending.Count>0&&s.MeetingUntil==null&&!(s.BufferUntil>DateTimeOffset.UtcNow)||s.ShowMeetingSummary&&s.MeetingUntil==null&&!(s.BufferUntil>DateTimeOffset.UtcNow))
        {Ui.Gap(body,12);body.Children.Add(Ui.Button(s.ShowMeetingSummary?"查看会后待办":"处理当前待办",()=>OpenQueue(),id:"OpenQueue"));}
        Ui.Gap(body,24);body.Children.Add(SectionHeading("我的习惯提醒",Ui.Button("新增提醒",()=>EditPlan(null),true,"NewPlan")));Ui.Gap(body,12);
        var ordinary=s.Plans.Where(p=>p.IsHabit).ToList();
        if(ordinary.Count==0)body.Children.Add(Ui.Text("还没有普通提醒。",14,brush:"Muted"));else
        {
            var columns=new Grid();foreach(var w in new[]{new GridLength(64),new GridLength(1,GridUnitType.Star),new GridLength(104),new GridLength(86),new GridLength(136)})columns.ColumnDefinitions.Add(new(){Width=w});
            var title=Ui.Text("事项 / 循环规则",13,brush:"Muted");Grid.SetColumnSpan(title,2);columns.Children.Add(title);var time=Ui.Text("下次提醒",13,brush:"Muted");Grid.SetColumn(time,2);columns.Children.Add(time);var status=Ui.Text("状态",13,brush:"Muted");Grid.SetColumn(status,3);columns.Children.Add(status);var list=new StackPanel();var tableHeader=new Border { Child=columns,Padding=new(24,12,8,12),CornerRadius=new(10,10,0,0) };tableHeader.SetResourceReference(Border.BackgroundProperty,"Theme.tableHeader");list.Children.Add(tableHeader);
            foreach(var p in ordinary)list.Children.Add(PlanRow(p));body.Children.Add(Ui.Panel(list,new(0)));
        }
        Ui.Gap(body,24);body.Children.Add(SectionHeading("待办事项",Ui.Button("新增待办",()=>EditPlan(null,HabitKind.Other),id:"NewTodo")));Ui.Gap(body,12);
        var todos=s.Plans.Where(p=>p.Kind!=HabitKind.Medication&&!p.IsHabit).ToList();
        if(todos.Count==0)body.Children.Add(Ui.Notice("没有需要补办的日常习惯。待办事项可单独新增，按有效期保留。"));
        else{var list=new StackPanel();foreach(var p in todos)list.Children.Add(PlanRow(p));body.Children.Add(Ui.Panel(list,new(0)));}
        return Ui.Scroll(body);
    }
    private UIElement MedicinePage()
    {
        var body=new StackPanel();body.Children.Add(SectionHeading("服药计划",Ui.Button("新增药物计划",()=>EditPlan(null,HabitKind.Medication),true,"AddMedicine")));body.Children.Add(Ui.Text("每种药物独立设置明确时刻，逐次确认。",14,brush:"Muted",margin:new(0,10,0,20)));
        var pending=_app.Engine.State.Occurrences.Where(o=>o.Kind==HabitKind.Medication&&o.Status==OccurrenceStatus.NeedsReview).ToList();
        if(pending.Count>0){body.Children.Add(Ui.Notice($"有 {pending.Count} 次服药记录待核实。缺少确认不代表实际未服用。"));body.Children.Add(Ui.Button("核对待核实记录",()=>{_historyStatus=OccurrenceStatus.NeedsReview;Navigate("History");},id:"ReviewMedicine"));Ui.Gap(body);}
        var plans=_app.Engine.State.Plans.Where(p=>p.Kind==HabitKind.Medication).ToList();if(plans.Count==0)body.Children.Add(Ui.Notice("还没有服药计划。按你已有的安排填写药名和时刻后开始。"));
        if(plans.Count>0){var list=new StackPanel();foreach(var p in plans)list.Children.Add(PlanRow(p));body.Children.Add(Ui.Panel(list,new(0)));}return Ui.Scroll(body);
    }
    private FrameworkElement PlanRow(ReminderPlan p)
    {
        var row=new Grid { Margin=new(0,0,0,0) };foreach(var w in new[]{new GridLength(64),new GridLength(1,GridUnitType.Star),new GridLength(104),new GridLength(86),new GridLength(136)})row.ColumnDefinitions.Add(new(){Width=w});
        row.Children.Add(Ui.IconCircle(p.Kind,48));var text=new StackPanel();var name=Ui.Text(p.Name,16,true);name.MaxHeight=48;name.TextTrimming=TextTrimming.CharacterEllipsis;name.ToolTip=p.Name;text.Children.Add(name);text.Children.Add(Ui.Text(Ui.Rule(p,p.OnceAt is {} onceAt?_app.Engine.Local(onceAt):null)+" · "+Labels.Style(p.Style),14,brush:"Muted",margin:new(0,6,10,0)));
        if(p.StartsOn!=null||p.EndsOn!=null)text.Children.Add(Ui.Text($"{p.StartsOn?.ToString("yyyy-MM-dd")??"不限"} 至 {p.EndsOn?.ToString("yyyy-MM-dd")??"长期"}",12,brush:"Muted",margin:new(0,4,0,0)));
        if(p.Kind!=HabitKind.Medication)text.Children.Add(Ui.Text(p.IsHabit?"过时不补办":p.ValidUntil is {} cutoff?$"有效至 {_app.Engine.Local(cutoff):MM-dd HH:mm}":"持续保留，直到处理",12,brush:"Muted",margin:new(0,4,0,0)));
        Grid.SetColumn(text,1);row.Children.Add(text);var now=DateTimeOffset.UtcNow;
        var item=_app.Engine.State.Occurrences.FirstOrDefault(o=>o.PlanId==p.Id&&o.Status is OccurrenceStatus.Pending or OccurrenceStatus.Deferred or OccurrenceStatus.NeedsReview);
        FrameworkElement next;
        if(item!=null)next=Ui.Button(item.Status==OccurrenceStatus.NeedsReview?"待核实":"处理",()=>_app.Run(()=>_app.OpenItems([item])),id:"Handle_"+p.Id);
        else{var at=Next(p,now);next=Ui.Text(!p.Enabled?p.Schedule==ScheduleKind.Once&&_app.Engine.State.Occurrences.Any(o=>o.PlanId==p.Id&&o.Revision==p.Revision&&o.Status is OccurrenceStatus.Completed or OccurrenceStatus.Skipped or OccurrenceStatus.NotNeeded)?"已结束":"已停用":at is {} t?_app.Engine.Local(t).ToString(_app.Engine.Local(t).Date==_app.Engine.Local(now).Date?"HH:mm":"MM-dd HH:mm"):"计划已结束",16);}
        next.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(next,2);row.Children.Add(next);
        var toggle=new CheckBox { Content=p.Enabled?"开启":"关闭",IsChecked=p.Enabled,Style=(Style)FindResource("Toggle"),VerticalAlignment=VerticalAlignment.Center };AutomationProperties.SetName(toggle,p.Name+"启用状态");AutomationProperties.SetAutomationId(toggle,"Enabled_"+p.Id);
        var syncing=false;
        void ChangeEnabled(object sender,RoutedEventArgs args)
        {
            if(syncing)return;var desired=toggle.IsChecked==true;
            if(_app.Engine.State.Plans.Single(x=>x.Id==p.Id).Enabled==desired)return;
            _app.Run(()=>_app.Engine.SetEnabled(p.Id,desired,DateTimeOffset.UtcNow));
            var actual=_app.Engine.State.Plans.Single(x=>x.Id==p.Id).Enabled;syncing=true;
            try{toggle.IsChecked=actual;toggle.Content=actual?"开启":"关闭";}finally{syncing=false;}
        }
        toggle.Checked+=ChangeEnabled;toggle.Unchecked+=ChangeEnabled;Grid.SetColumn(toggle,3);row.Children.Add(toggle);
        var edit=Ui.RowAction("编辑",()=>EditPlan(p),"Edit_"+p.Id);var delete=Ui.RowAction("删除",()=>new DeletePlanWindow(_app,p){Owner=this}.ShowDialog(),"Delete_"+p.Id,true);
        edit.Margin=new(0,0,4,0);AutomationProperties.SetName(edit,"编辑"+p.Name);AutomationProperties.SetName(delete,"删除"+p.Name);
        var operations=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Children={edit,delete}};Grid.SetColumn(operations,4);row.Children.Add(operations);
        var outer=new Border { Child=row,BorderThickness=new(0,0,0,1),Padding=new(24,16,8,16) };outer.SetResourceReference(Border.BorderBrushProperty,"Line");return outer;
    }
    private static Grid SectionHeading(string title,Button action)
    {var g=new Grid();g.Children.Add(Ui.Text(title,21,true,margin:new(0,7,0,0)));action.HorizontalAlignment=HorizontalAlignment.Right;g.Children.Add(action);return g;}
    public PlanEditor EditPlan(ReminderPlan? p,HabitKind? kind=null)
    {var editor=new PlanEditor(_app,p,kind) { Owner=this };editor.ShowDialog();if(editor.Saved)_notice=editor.SavedPlan?.Enabled!=true?"已保存，保持停用。":_app.Engine.State.PauseUntil!=null?"已保存，普通提醒保持暂停。":_app.Engine.State.MeetingUntil!=null?"已保存，会议模式保持不变。":"已保存，其他计划的时间保持不变。";Refresh();return editor;}
    public void OpenMode(bool pause){var dialog=new ModeWindow(_app,pause) { Owner=this };dialog.ShowDialog();Refresh();}
    private void ExtendMode()=>_app.Run(()=>{var now=DateTimeOffset.UtcNow;var s=_app.Engine.State;if(s.PauseUntil is {} p)_app.Engine.BeginPause(p.AddMinutes(15),s.PauseMedication,now);else if(s.MeetingUntil is {} m)_app.Engine.BeginMeeting(m.AddMinutes(15),now,s.MeetingSilent);});
    private void EndMode()=>_app.Run(()=>{if(_app.Engine.State.PauseUntil!=null)_app.Engine.ResumePause(DateTimeOffset.UtcNow);else _app.Engine.EndMeeting(DateTimeOffset.UtcNow);});
    public void OpenQueue()=>_app.ShowQueue();
    public static SolidColorBrush Brush(string value)=>Theme.Brush(value);
    public static TextBlock Text(string text,double size,FontWeight? weight=null,string color="#24262A",Thickness? margin=null)=>new(){Text=text,FontSize=size,FontWeight=weight??FontWeights.Normal,Foreground=Brush(color),TextWrapping=TextWrapping.Wrap,Margin=margin??new()};
}
