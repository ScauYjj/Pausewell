using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pausewell.Wireframes;

public sealed partial class ReviewWindow
{
    private readonly List<MedicineExample> _medicines = [];
    private readonly List<ReviewRecord> _records = [];
    private ReviewSettings _settings = new(), _settingsDraft = new();
    private SamplePlan? _editingPlan;
    private ReviewRecord? _focusedRecord;
    private string _recordOperation = "", _recordError = "", _actualTime = "13:00";
    private DateTime _actualDate = new(2026,10,15);
    private bool _timeUnknown, _failNextRecordSave, _returnToQueue;
    private string _alertKind = "喝水";
    private bool _strongAlert;
    private string _modeStage = "edit";
    private RunMode _modeDraft = RunMode.Meeting;
    private string _durationText = "60";
    private bool _pauseMedicines, _meetingSilent;

    private void ResetExtended()
    {
        _holdTimer?.Stop();
        _medicines.Clear(); _records.Clear(); _settings = new(); _settingsDraft = new();
        _focusedRecord = null; _editingPlan = null; _recordOperation = _recordError = "";
        _pauseMedicines = _meetingSilent = _returnToQueue = _failNextRecordSave = false;
        _medicineEditor = false; _historyDetail = null;
    }
    private void SeedMedicines()
    {
        if (_medicines.Count != 0) return;
        _medicines.Add(new() { Name = "药物 A（示例）", Times = [new(8,0,0),new(13,0,0),new(20,0,0)] });
        _medicines.Add(new() { Name = "药物 B（示例）", Times = [new(8,0,0),new(20,0,0)], Enabled = false });
    }
    private void PrepareExtra(ReviewPage page)
    {
        if (page == ReviewPage.Reminder) PrepareReminder("喝水");
        if (page == ReviewPage.Meeting) { Seed(); SeedMedicines(); _modeDraft = _mode == RunMode.Paused ? RunMode.Paused : RunMode.Meeting; _modeStage = _mode == RunMode.Running ? "edit" : "active"; _draftPauseMedicines=_pauseMedicines;_draftMeetingSilent=_meetingSilent; }
        if (page == ReviewPage.Medicine) { SeedMedicines(); _medicineEditor = false; _medFormError = ""; }
        if (page == ReviewPage.History) { SeedHistory(); _historyDetail = null; _correcting = false; }
        if (page == ReviewPage.Settings) { _settingsDraft = _settings.Copy(); }
    }
    private string[] ExtraPoints() => _page switch
    {
        ReviewPage.Reminder => ["先看要做什么，再看完成、延期和退出。", "活动计时结束还要确认；如厕允许当前无需要。", "药物逐次核对；不确定就保留待核实。", "全屏紧急退出保留未完成状态，不锁住系统。"],
        ReviewPage.Meeting => ["开始前说清哪些提醒会受影响。", "普通暂停与会议模式分别解释恢复规则。", "服药默认保留原计划；完全静音必须主动选择。", "会后先收尾，再汇总普通事项，药物单独核对。"],
        ReviewPage.Medicine => ["每种药独立设置明确时刻，不自动换算等间隔。", "用户录入已有用药要求，软件不生成剂量建议。", "首次从下一个未来时刻开始，过去时刻不算新建计划的漏项。", "修改或停用计划也保留既有待核实记录。"],
        ReviewPage.History => ["区分原计划、实际发生时间和确认时间。", "待核实和未执行不算完成。", "更正保留原值、原因和更正时间。", "更正记录不移动下一次固定服药时刻。"],
        ReviewPage.Settings => ["运行偏好与每条提醒的具体规则分开。", "随开机启动默认不勾选。", "默认工作时段只影响以后新增的普通提醒。", "保存与取消结果明确，开关在本稿中只作用于示例。"],
        _ => ["明确固定时刻与完成后间隔的区别。", "普通固定时刻完成得晚，后续计划不整体后移。", "名称和时刻校验在提交时给出。", "本页可供每日固定事项复用，不混入药物说明。"]
    };
    private UIElement ExtraPage() => _page switch
    {
        ReviewPage.Reminder => ReminderReviewPage(), ReviewPage.Meeting => MeetingPage(),
        ReviewPage.Medicine => MedicinePage(), ReviewPage.History => HistoryPage(),
        ReviewPage.Settings => SettingsPage(), _ => FixedTimesPage()
    };
    private void AddExtraControls()
    {
        if (_page == ReviewPage.Reminder)
        {
            foreach (var label in new[] { "喝水", "活动", "如厕", "全屏", "服药", "恢复后待核实", "保存失败" })
                AddNoteAction("查看：" + label, () => { PrepareReminder(label); Render(); });
            AddNoteAction("重新演示当前事项", () => { if (_focusedRecord != null) _records.Remove(_focusedRecord); PrepareReminder(_alertKind); Render(); });
            if (_alertKind == "活动") AddNoteAction("模拟活动计时结束", () => { _activityStarted = true; _activityReady = true; Render(); }, "FinishActivityDemo");
        }
        if (_page == ReviewPage.Meeting)
        {
            AddNoteAction("查看：开始会议", () => { _modeDraft = RunMode.Meeting; _modeStage = "edit"; Render(); });
            AddNoteAction("查看：暂停普通提醒", () => { _modeDraft = RunMode.Paused; _modeStage = "edit"; Render(); });
            AddNoteAction("模拟到期，进入收尾缓冲", SimulateModeEnd, "SimulateModeEnd");
            if (_modeStage == "buffer") AddNoteAction("模拟 30 秒收尾结束", ShowMeetingQueue, "FinishBuffer");
            AddNoteAction("直接查看：会后待办", () => { SeedMeetingQueue(); _modeStage = "queue"; Render(); });
        }
        if (_page == ReviewPage.Medicine)
        {
            AddNoteAction("查看：本次服药提醒", () => { _page = ReviewPage.Reminder; PrepareReminder("服药"); Render(); });
            AddNoteAction("查看：重启后待核实", () => { _page = ReviewPage.Reminder; PrepareReminder("恢复后待核实"); Render(); });
        }
        if (_page == ReviewPage.History)
        {
            AddNoteAction("查看：待核实记录", ShowReviewExample);
            AddNoteAction("查看：没有记录", () => { _historyEmptyDemo = true; _historyDetail=null; Render(); });
            AddNoteAction("返回示例记录", () => { _historyEmptyDemo=false; _historyDetail=null; Render(); });
        }
    }
    private UIElement PageShell(string title, string subtitle, UIElement content, UIElement? footer = null)
    {
        var root = new DockPanel(); var header=ProductHeader(); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
        if (footer != null) { var f=new Border { Child=footer, Padding=new Thickness(26,14,26,20), BorderBrush=B("#CCCCCC"), BorderThickness=new Thickness(0,1,0,0) }; DockPanel.SetDock(f,Dock.Bottom); root.Children.Add(f); }
        var body=new StackPanel { Margin=new Thickness(26,22,26,22) };
        body.Children.Add(T(title,25,true)); body.Children.Add(T(subtitle,13,color:"#646464",margin:new(0,8,0,20)));
        if(_notice.Length>0){body.Children.Add(Box(T(_notice,13),"#F4F4F4",new Thickness(12))); Gap(body,16);}
        body.Children.Add(content); root.Children.Add(new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }); return root;
    }
    private void Field(Panel parent,string caption,UIElement control)
    {
        parent.Children.Add(T(caption,13,true,margin:new(0,12,0,7))); parent.Children.Add(control);
        if(control is Control c) AutomationProperties.SetName(c,caption);
    }
    private TextBox Input(string value,string id,Action<string> changed, bool multiline=false)
    {
        var input=new TextBox { Text=value,MaxLength=multiline?1000:100,AcceptsReturn=multiline,TextWrapping=multiline?TextWrapping.Wrap:TextWrapping.NoWrap,MinHeight=multiline?64:36 };
        AutomationProperties.SetAutomationId(input,id); input.TextChanged+=(_,_)=>changed(input.Text); return input;
    }
    private StackPanel Actions(params Button[] buttons)
    {
        var panel=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
        foreach(var b in buttons){b.Margin=new Thickness(8,0,0,0);panel.Children.Add(b);} return panel;
    }
    private static bool ParseClock(string value,out TimeSpan clock)
    {
        clock=default;
        if(!DateTime.TryParseExact(value,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var t))return false;
        clock=t.TimeOfDay;return true;
    }
    private DateTime NextFixed(IEnumerable<TimeSpan> times,DateTime after,DateTime? starts=null,DateTime? ends=null)
    {
        var date=after.Date; if(starts?.Date>date) date=starts.Value.Date;
        for(var d=0;d<370;d++,date=date.AddDays(1))
        {
            if(ends is {} end && date>end.Date)return DateTime.MaxValue;
            foreach(var time in times.Order())if(date.Add(time)>after)return date.Add(time);
        }
        return DateTime.MaxValue;
    }
    private ReviewRecord GetRecord(string planId,string name,string kind,bool medication,DateTime planned)
    {
        var existing=_records.FirstOrDefault(r=>r.PlanId==planId&&r.Planned==planned);
        if(existing!=null)return existing;
        var record=new ReviewRecord { PlanId=planId,Name=name,Kind=kind,Medication=medication,Planned=planned }; _records.Add(record);return record;
    }
    private void TrackOrdinary(SamplePlan plan,string status)
    {
        var record=GetRecord(plan.Id,plan.Name,plan.Kind,false,plan.Next);record.Status=status;
        if(status=="已完成"){record.Actual=_now;record.Confirmed=_now;}
        else if(status=="当前无需要")record.Confirmed=_now;
        else if(status=="已延期")record.NotifyAfter=_now.AddMinutes(5);
    }
    private void EditOrdinary(SamplePlan p)
    {
        if(p.FixedTimes){OpenFixedOrdinary();_fixedEditing=p;_fixedName=p.Name;_fixedTimeText=string.Join("、",p.Times.Select(t=>t.ToString(@"hh\:mm")));Render();return;}
        _editingPlan=p;_name=p.Name;_kind=p.Kind;_minutes=p.Minutes.ToString();_style=p.Style;_office=p.OfficeOnly;_start=p.Start.ToString(@"hh\:mm");_end=p.End.ToString(@"hh\:mm");_page=ReviewPage.Add;_submitted=false;Render();
    }

    private void PrepareReminder(string scenario)
    {
        _alertKind=scenario;_recordOperation=_recordError="";_timeUnknown=false;_activityStarted=_activityReady=false;_strongAlert=scenario=="全屏";_failNextRecordSave=scenario=="保存失败";_returnToQueue=false;
        if(scenario is "服药" or "恢复后待核实" or "保存失败")
        {
            SeedMedicines();var med=_medicines[0];_now=new(2026,10,15,scenario=="恢复后待核实"?14:13,0,0);
            _focusedRecord=GetRecord(med.Id,med.Name,"服药",true,_now.Date.AddHours(13));
            var last=GetRecord(med.Id,med.Name,"服药",true,_now.Date.AddHours(8));if(!last.Resolved){last.Status="已服用";last.Confirmed=_now.Date.AddHours(8).AddMinutes(5);last.Actual=_now.Date.AddHours(8).AddMinutes(4);}
            if(scenario=="恢复后待核实"&&!_focusedRecord.Resolved)_focusedRecord.Status="待核实";
            _actualTime=_now.ToString("HH:mm");_actualDate=_now.Date;
        }
        else
        {
            Seed();var kind=scenario=="全屏"?"喝水":scenario;
            var plan=_plans.FirstOrDefault(p=>p.Kind==kind);
            if(plan==null){plan=new(){Name=kind=="如厕"?"检查是否需要如厕":"起来活动",Kind=kind,Minutes=30,Next=new(2026,10,15,9,30,0)};_plans.Add(plan);}
            _now=new(2026,10,15,9,30,0);_focusedRecord=GetRecord(plan.Id,plan.Name,kind,false,_now);
        }
        _due=null;_notice="";
    }
    private UIElement ReminderReviewPage()
    {
        var record=_focusedRecord;
        if(record==null)return PageShell("没有待处理提醒","可以从右侧选择一个示例。",new StackPanel());
        var content=new StackPanel();
        if(record.Resolved)
        {
            content.Children.Add(Box(T($"本次已经记录：{record.Status}\n原计划 {record.Planned:MM-dd HH:mm}，确认 {record.Confirmed:HH:mm}。",17,true)));
            Gap(content,18); content.Children.Add(T("同一事项不会再次作为新任务要求完成。需要修改时，请更正既有记录。",13));
            return PageShell("本次处理结果","记录已经保留，后续计划按各自规则继续。",content,Actions(Btn("查看这条记录",()=>OpenRecord(record)),Btn("返回首页",()=>Navigate(ReviewPage.Home),true)));
        }
        if(record.Medication)return MedicineReminder(record);
        var inner=new StackPanel();inner.Children.Add(T(record.Name,28,true));
        inner.Children.Add(T($"原计划 {record.Planned:HH:mm} · 当前示例时间 {_now:HH:mm}",13,color:"#646464",margin:new(0,8,0,18)));
        if(record.Kind=="活动")
        {
            inner.Children.Add(T(_activityReady?"活动计时已结束，请确认实际完成情况。":_activityStarted?"活动计时中 · 2:00":"本次活动设定为 2 分钟。",16,true));Gap(inner,14);
            var start=Btn(_activityStarted?"计时进行中":"开始活动计时",()=>{_activityStarted=true;Render();},id:"StartActivityReview");start.IsEnabled=!_activityStarted;inner.Children.Add(start);Gap(inner,12);
        }
        else inner.Children.Add(T(record.Kind=="如厕"?"检查一下是否有如厕需要；当前没有需要也可以如实记录。":"方便时喝点水，完成后再确认。",15));
        Gap(inner,22);
        var done=Btn(record.Kind=="喝水"?"确认已喝水":"确认已完成",()=>ResolveOrdinaryReview("已完成"),true,"ResolveOrdinary");done.IsEnabled=record.Kind!="活动"||(_activityStarted&&_activityReady);
        var buttons=new WrapPanel();done.Margin=new Thickness(0,0,10,10);buttons.Children.Add(done);
        var defer=Btn("5 分钟后再提醒",()=>ResolveOrdinaryReview("已延期"),id:"DeferOrdinary");defer.Margin=new Thickness(0,0,0,10);buttons.Children.Add(defer);inner.Children.Add(buttons);
        if(record.Kind=="如厕")inner.Children.Add(Btn("当前无需要",()=>ResolveOrdinaryReview("当前无需要"),id:"NoToiletNeed"));
        Gap(inner,15);inner.Children.Add(T("延期只改变这次提醒的再次出现时间，不算完成。",12,color:"#646464"));
        if(_strongAlert)
        {
            Gap(inner,18);inner.Children.Add(T("全屏强提醒 · 紧急工作时可退出，记录仍保持未完成。",13,true));
            var escape=Btn("长按 2 秒，紧急退出",()=>{},id:"EmergencyExitReview");AttachEmergencyHold(escape);inner.Children.Add(escape);
        }
        content.Children.Add(Box(inner,_strongAlert?"#E5E5E5":"#FFFFFF",new Thickness(26)));
        if(record.Status=="已延期")content.Children.Add(T($"本次已延期，将在 {record.NotifyAfter:HH:mm} 再提醒；原计划仍为 {record.Planned:HH:mm}。",13,margin:new(0,16,0,0)));
        return PageShell(_strongAlert?"全屏提醒示意":"到期提醒",_strongAlert?"在本评审框内示意覆盖，不会覆盖你的桌面。":"每种事项有清楚的处理结果，关闭和延期都不算完成。",content);
    }
    private void ResolveOrdinaryReview(string status)
    {
        var r=_focusedRecord!;if(r.Resolved)return;r.Status=status;
        if(status=="已延期")r.NotifyAfter=_now.AddMinutes(5);
        else{r.Confirmed=_now;if(status=="已完成")r.Actual=_now;}
        var plan=_plans.FirstOrDefault(p=>p.Id==r.PlanId);
        if(plan!=null)plan.Next=status=="已延期"?_now.AddMinutes(5):plan.FixedTimes?NextFixed(plan.Times,_now):Eligible(plan,_now.AddMinutes(plan.Minutes));
        _notice=status=="已延期"?$"已延期至 {r.NotifyAfter:HH:mm}；原计划 {r.Planned:HH:mm}，未记录完成。":$"已记录：{status}。"+(plan!=null?$"下次 {plan.Next:HH:mm}。":"");
        if(_returnToQueue){_page=ReviewPage.Meeting;_modeStage="queue";}else _page=ReviewPage.Home;
        Render();
    }
    private System.Windows.Threading.DispatcherTimer? _holdTimer;
    private void AttachEmergencyHold(Button button)
    {
        System.Diagnostics.Stopwatch? hold=null;
        void Stop(){_holdTimer?.Stop();hold=null;button.Content="长按 2 秒，紧急退出";}
        void Start()
        {
            if(hold!=null)return;hold=System.Diagnostics.Stopwatch.StartNew();
            _holdTimer?.Stop();_holdTimer=new(){Interval=TimeSpan.FromMilliseconds(100)};
            _holdTimer.Tick+=(_,_)=>{if(hold==null)return;button.Content=$"继续按住 {Math.Max(0,2-hold.Elapsed.TotalSeconds):0.0} 秒";if(hold.Elapsed.TotalSeconds>=2){Stop();ResolveOrdinaryReview("已延期");}};_holdTimer.Start();
        }
        button.PreviewMouseLeftButtonDown+=(_,e)=>{Start();button.CaptureMouse();e.Handled=true;};
        button.PreviewMouseLeftButtonUp+=(_,e)=>{Stop();button.ReleaseMouseCapture();e.Handled=true;};button.LostMouseCapture+=(_,_)=>Stop();
        button.PreviewKeyDown+=(_,e)=>{if(e.Key is System.Windows.Input.Key.Space or System.Windows.Input.Key.Enter){Start();e.Handled=true;}};
        button.PreviewKeyUp+=(_,e)=>{if(e.Key is System.Windows.Input.Key.Space or System.Windows.Input.Key.Enter){Stop();e.Handled=true;}};
        button.LostKeyboardFocus+=(_,_)=>Stop();
    }
}
