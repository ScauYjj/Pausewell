using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Pausewell.Wireframes;

public sealed partial class ReviewWindow
{
    private bool _medicineEditor, _medSubmitted;
    private MedicineExample? _editingMedicine;
    private string _medName="",_medInstructions="",_medFormError="";
    private List<string> _medTimeText=["",""];
    private DateTime _medStarts=new(2026,10,15);
    private DateTime? _medEnds;
    private bool _medHasEnd;
    private TextBlock? _medPreview,_medErrorView;
    private string _fixedName="",_fixedTimeText="12:00",_fixedError="";
    private SamplePlan? _fixedEditing;

    private void BeginMedicineEdit(MedicineExample? medicine)
    {
        _editingMedicine=medicine;_medicineEditor=true;_medSubmitted=false;_medFormError="";
        _medName=medicine?.Name??"";_medInstructions=medicine?.Instructions??"";
        _medTimeText=medicine?.Times.Select(t=>t.ToString(@"hh\:mm")).ToList()??["",""];
        _medStarts=medicine?.Starts??_now.Date;_medEnds=medicine?.Ends;_medHasEnd=medicine?.Ends!=null;Render();
    }
    private UIElement MedicinePage()
    {
        if(_medicineEditor)return MedicineForm();
        var body=new StackPanel();
        var title=new DockPanel();var add=Btn("添加药物",()=>BeginMedicineEdit(null),true,"AddMedicine");DockPanel.SetDock(add,Dock.Right);title.Children.Add(add);
        title.Children.Add(T("每种药物有自己的时刻与记录。",14,margin:new(0,8,0,0)));body.Children.Add(title);Gap(body,18);
        foreach(var med in _medicines)
        {
            var row=new Grid { Margin=new Thickness(0,16,0,16) };row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            var info=new StackPanel();info.Children.Add(T(med.Name,18,true));
            info.Children.Add(T("每天 "+string.Join(" / ",med.Times.Select(t=>t.ToString(@"hh\:mm"))),14,margin:new(0,8,0,0)));
            var next=NextFixed(med.Times,_now,med.Starts,med.Ends);var reviews=_records.Count(r=>r.PlanId==med.Id&&r.Status=="待核实");
            info.Children.Add(T((!med.Enabled?"已停用":next==DateTime.MaxValue?"已结束":$"下次 {next:MM-dd HH:mm}")+(reviews>0?$" · {reviews} 条待核实":""),13,color:"#646464",margin:new(0,8,0,0)));
            info.Children.Add(T($"开始 {med.Starts:MM-dd} · "+(med.Ends is {} end?$"至 {end:MM-dd}":"未设结束日期"),12,color:"#646464",margin:new(0,6,0,0)));row.Children.Add(info);
            var actions=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
            var edit=Btn("编辑",()=>BeginMedicineEdit(med));actions.Children.Add(edit);
            var toggle=Btn(med.Enabled?"停用":"启用",()=>{med.Enabled=!med.Enabled;if(!med.Enabled)foreach(var record in _records.Where(r=>r.PlanId==med.Id&&!r.Resolved))record.Status="待核实";_notice=$"已{(med.Enabled?"启用":"停用")}“{med.Name}”。既有记录与待核实事项保留。";Render();});toggle.Margin=new Thickness(8,0,0,0);actions.Children.Add(toggle);Grid.SetColumn(actions,1);row.Children.Add(actions);
            body.Children.Add(new Border { Child=row,BorderBrush=B("#CCCCCC"),BorderThickness=new Thickness(0,1,0,0) });
        }
        Gap(body,22);body.Children.Add(Box(T("药物不继承普通习惯的工作日与办公时段。普通暂停默认不影响服药计划；会议中按隐私提示策略处理。",13),"#F4F4F4"));
        return PageShell("服药计划","按已有用药要求填写；这里只负责提醒与记录，不生成剂量或补服建议。",body);
    }
    private UIElement MedicineForm()
    {
        var body=new StackPanel();
        Field(body,"药物名称",Input(_medName,"MedicineName",v=>{_medName=v;UpdateMedicinePreview();}));
        Field(body,"每天有哪些明确时刻",T("次数只决定要填写几个时刻，不自动换算成等间隔。",12,color:"#646464"));
        var counts=new WrapPanel();foreach(var count in new[]{1,2,3,4}){var c=count;var b=Btn($"{c} 次",()=>{while(_medTimeText.Count<c)_medTimeText.Add("");while(_medTimeText.Count>c)_medTimeText.RemoveAt(_medTimeText.Count-1);Render();},_medTimeText.Count==c);b.Margin=new Thickness(0,0,8,8);counts.Children.Add(b);}counts.Children.Add(Btn("增加一个时刻",()=>{if(_medTimeText.Count<24)_medTimeText.Add("");Render();}));body.Children.Add(counts);
        var times=new WrapPanel();for(var i=0;i<_medTimeText.Count;i++){var index=i;var group=new StackPanel { Width=125,Margin=new Thickness(0,0,12,10) };group.Children.Add(T($"第 {i+1} 次 · HH:mm",12,color:"#646464",margin:new(0,0,0,5)));var input=Input(_medTimeText[i],"MedicineTime"+i,v=>{_medTimeText[index]=v;UpdateMedicinePreview();});AutomationProperties.SetName(input,$"第 {i+1} 次服药时刻");group.Children.Add(input);times.Children.Add(group);}body.Children.Add(times);
        var dates=new WrapPanel();var firstDate=new DatePicker { SelectedDate=_medStarts,Width=150 };firstDate.SelectedDateChanged+=(_,_)=>{_medStarts=firstDate.SelectedDate??_now.Date;UpdateMedicinePreview();};var dateGroup=new StackPanel { Margin=new Thickness(0,0,20,0) };Field(dateGroup,"开始日期",firstDate);dates.Children.Add(dateGroup);
        var endGroup=new StackPanel();var hasEnd=new CheckBox { Content="设置结束日期",IsChecked=_medHasEnd,Margin=new Thickness(0,12,0,7) };endGroup.Children.Add(hasEnd);var endDate=new DatePicker { SelectedDate=_medEnds,Width=150,IsEnabled=_medHasEnd };endDate.SelectedDateChanged+=(_,_)=>{_medEnds=endDate.SelectedDate;UpdateMedicinePreview();};hasEnd.Click+=(_,_)=>{_medHasEnd=hasEnd.IsChecked==true;endDate.IsEnabled=_medHasEnd;UpdateMedicinePreview();};endGroup.Children.Add(endDate);dates.Children.Add(endGroup);body.Children.Add(dates);
        Field(body,"既有用药说明（可选，按已有要求录入）",Input(_medInstructions,"MedicineInstructions",v=>_medInstructions=v,true));
        body.Children.Add(T("未填写时刻不会自动采用示例时间。餐前餐后等要求先作为说明保存。",12,color:"#646464",margin:new(0,12,0,0)));
        _medPreview=T("",13);_medErrorView=T("",12,true,margin:new(0,8,0,0));
        var footer=new StackPanel();footer.Children.Add(Box(_medPreview,"#F4F4F4",new Thickness(12)));footer.Children.Add(_medErrorView);
        var saveText=_mode==RunMode.Meeting?"保存，保持会议模式":_mode==RunMode.Paused&&_pauseMedicines?"保存，等待暂停结束":_editingMedicine!=null?"保存修改":"保存并启用服药提醒";
        footer.Children.Add(Actions(Btn("取消",()=>{_medicineEditor=false;Render();},id:"CancelMedicine"),Btn(saveText,SaveMedicine,true,"SaveMedicine")));UpdateMedicinePreview();
        return PageShell(_editingMedicine==null?"添加药物":"编辑药物计划","固定时刻逐次记录。修改计划不会移动后续固定时刻，也不会删除旧记录。",body,footer);
    }
    private bool ValidateMedicine(out List<TimeSpan> times,out string error)
    {
        times=[];error="";
        if(string.IsNullOrWhiteSpace(_medName)){error="请填写药物名称。";return false;}
        if(_medTimeText.Count is <1 or >24){error="请填写 1 至 24 个明确时刻。";return false;}
        foreach(var text in _medTimeText){if(!ParseClock(text,out var t)){error="每个时刻都请使用 HH:mm，例如按已有要求填写 08:00。";return false;}times.Add(t);}
        if(times.Distinct().Count()!=times.Count){error="同一种药物的时刻不能重复。";return false;}
        if(_medHasEnd&&(_medEnds==null||_medEnds.Value.Date<_medStarts.Date)){error="结束日期不能早于开始日期，请明确选择。";return false;}
        return true;
    }
    private void UpdateMedicinePreview()
    {
        if(_medPreview==null||_medErrorView==null)return;
        if(!ValidateMedicine(out var times,out var error)){_medPreview.Text="填写名称、明确时刻和适用日期后，会显示接下来的计划。";_medErrorView.Text=_medSubmitted?error:"";return;}
        _medErrorView.Text="";var after=_now;var values=new List<string>();
        for(var i=0;i<3;i++){var next=NextFixed(times,after,_medStarts,_medHasEnd?_medEnds:null);if(next==DateTime.MaxValue)break;values.Add(next.ToString("MM-dd HH:mm"));after=next;}
        var context=_mode==RunMode.Paused?(_pauseMedicines?"当前也暂停服药提醒；原计划时刻保留，到期缺少确认的事项恢复后待核实。":"当前仅暂停普通提醒，服药仍按原时刻提示。"):_mode==RunMode.Meeting?"会议模式继续生效，到期按本次会议的隐私或静音设置处理。":"保存后从下一个未来时刻开始；不把新建前的时刻计作漏项。";
        _medPreview.Text=(values.Count==0?"此日期范围内已没有未来时刻，保存后视为已结束。":"接下来的原计划："+string.Join(" / ",values))+"\n"+context;
    }
    private void SaveMedicine()
    {
        _medSubmitted=true;if(!ValidateMedicine(out var times,out var error)){_medFormError=error;UpdateMedicinePreview();return;}
        var med=_editingMedicine??new MedicineExample();
        if(_editingMedicine!=null)foreach(var r in _records.Where(r=>r.PlanId==med.Id&&!r.Resolved))r.Status="待核实";
        med.Name=_medName.Trim();med.Instructions=_medInstructions;med.Times=times.Order().ToList();med.Starts=_medStarts.Date;med.Ends=_medHasEnd?_medEnds?.Date:null;
        if(_editingMedicine==null)_medicines.Add(med);med.Enabled=true;_medicineEditor=false;
        var next=NextFixed(med.Times,_now,med.Starts,med.Ends);_notice=$"已保存“{med.Name}”。"+(next==DateTime.MaxValue?"计划已结束，不再产生新提醒。":$"下一原计划 {next:MM-dd HH:mm}。")+"既有记录保留。";Render();
    }
    private UIElement MedicineReminder(ReviewRecord r)
    {
        var body=new StackPanel();var med=_medicines.First(m=>m.Id==r.PlanId);
        body.Children.Add(T(r.Name,25,true));body.Children.Add(T($"本次原计划 {r.Planned:MM-dd HH:mm} · 后续时刻不随确认时间平移",13,color:"#646464",margin:new(0,8,0,14)));
        var last=_records.Where(x=>x.PlanId==r.PlanId&&x.Status=="已服用"&&x.Id!=r.Id).OrderByDescending(x=>x.Planned).FirstOrDefault();
        body.Children.Add(T(last!=null?$"最近确认：{last.Confirmed:MM-dd HH:mm}；实际服用："+(last.Actual is {} at?at.ToString("MM-dd HH:mm"):"时间不详"):"没有既有服用确认记录。",13));
        if(med.Instructions.Length>0)body.Children.Add(T("既有说明："+med.Instructions,13,margin:new(0,8,0,0)));
        Gap(body,18);
        if(r.Status=="待核实")body.Children.Add(Box(T("本次尚未确认。可能已经服用，也可能尚未服用，请先核对实际情况。软件不据此安排补服。",14,true),"#F1F1F1"));
        if(r.Status=="已延期")body.Children.Add(Box(T($"已延后核对提醒至 {r.NotifyAfter:HH:mm}。只延后通知，原计划 {r.Planned:HH:mm} 未变。",14),"#F1F1F1"));
        if(_recordOperation is "taken" or "not")
        {
            Gap(body,15);body.Children.Add(T(_recordOperation=="taken"?"记录已经发生的服用情况":"记录本次明确未服用",18,true));
            if(_recordOperation=="taken")
            {
                var date=new DatePicker { SelectedDate=_actualDate,Width=150,HorizontalAlignment=HorizontalAlignment.Left };date.SelectedDateChanged+=(_,_)=>_actualDate=date.SelectedDate??_now.Date;Field(body,"实际服用日期",date);
                var time=Input(_actualTime,"ActualMedicineTime",v=>_actualTime=v);time.IsEnabled=!_timeUnknown;Field(body,"实际服用时刻（HH:mm）",time);
                var unknown=new CheckBox { Content="记不清具体服用时间",IsChecked=_timeUnknown,Margin=new Thickness(0,12,0,0) };unknown.Click+=(_,_)=>{_timeUnknown=unknown.IsChecked==true;time.IsEnabled=!_timeUnknown;};body.Children.Add(unknown);
                body.Children.Add(T("记不清时间时保留为时间不详；系统确认时间另行记录。",12,color:"#646464",margin:new(0,8,0,0)));
            }
            else body.Children.Add(T("仅记录你已经核实的本次情况；这不会自动改变下一次计划。",14,margin:new(0,10,0,0)));
            if(_recordError.Length>0){Gap(body,15);body.Children.Add(Box(T(_recordError,14,true),"#F1F1F1"));}
            var status=_recordOperation=="taken"?"已服用":"明确未服用";
            return PageShell("确认本次记录","先核对药物与原计划，保存的是记录，不是新的服药指令。",body,Actions(Btn("返回核对",()=>{_recordOperation="";Render();}),Btn(_recordError.Length>0?"重试保存记录":"确认保存记录",()=>StoreMedicineOutcome(status),true,"ConfirmMedicineRecord")));
        }
        Gap(body,22);var choices=new WrapPanel();
        foreach(var pair in new[]{("记录已服用","taken"),("记录本次未服用","not")}){var p=pair;var b=Btn(p.Item1,()=>{_recordOperation=p.Item2;_actualDate=_now.Date;_actualTime=_now.ToString("HH:mm");_recordError="";Render();},p.Item2=="taken",p.Item2=="taken"?"RecordTaken":"RecordNotTaken");b.Margin=new Thickness(0,0,8,10);choices.Children.Add(b);}
        var unsure=Btn("不确定是否服用",()=>StoreMedicineOutcome("待核实"),id:"UnsureMedicine");unsure.Margin=new Thickness(0,0,0,10);choices.Children.Add(unsure);body.Children.Add(choices);
        var defer=Btn("10 分钟后再提醒我核对",()=>{r.Status="已延期";r.NotifyAfter=_now.AddMinutes(10);_notice=$"核对提醒延至 {r.NotifyAfter:HH:mm}；原计划和下一次时刻未改变。";if(_returnToQueue){_page=ReviewPage.Meeting;_modeStage="queue";}else _page=ReviewPage.Home;Render();},id:"DeferMedicine");body.Children.Add(defer);
        Gap(body,16);body.Children.Add(Btn("查看既有记录",()=>{_historyType="服药";_historyDetail=null;_page=ReviewPage.History;Render();}));
        return PageShell(r.Status=="待核实"?"先核对本次实际情况":"核对本次服药","这里只记录你确认的事实；没有一键全部服用。",body);
    }
    private void StoreMedicineOutcome(string outcome)
    {
        var r=_focusedRecord!;if(r.Resolved){_notice="本次已经记录，请在历史中更正，避免重复记录。";Render();return;}
        DateTime? actual=null;
        if(outcome=="已服用"&&!_timeUnknown)
        {
            if(!ParseClock(_actualTime,out var clock)){_recordError="实际时间请使用 HH:mm。";Render();return;}
            actual=_actualDate.Date.Add(clock);if(actual>_now){_recordError="实际服用时间不能晚于当前示例时间。";Render();return;}
        }
        if(_failNextRecordSave){_failNextRecordSave=false;_recordError="记录未保存，本次状态尚未确认。若已服用，请不要因这条提示再次服用；可重试保存记录。";Render();return;}
        var before=r.Status;r.Status=outcome;r.Confirmed=outcome=="待核实"?null:_now;r.Actual=outcome=="已服用"?actual:null;
        r.Changes.Add($"{_now:MM-dd HH:mm} 用户记录：{before} → {outcome}");
        var med=_medicines.First(m=>m.Id==r.PlanId);var next=NextFixed(med.Times,_now,med.Starts,med.Ends);
        _recordOperation=_recordError="";_notice=$"已记录：{outcome}。本次原计划 {r.Planned:HH:mm} 保留。"+(next==DateTime.MaxValue?"此后没有计划时刻。":$"下一原计划 {next:MM-dd HH:mm} 未改变。");
        if(_returnToQueue){_page=ReviewPage.Meeting;_modeStage="queue";}else _page=ReviewPage.Home;Render();
    }
    private void OpenFixedOrdinary()
    {
        _fixedEditing=_editingPlan;_fixedName=_name;_fixedTimeText="12:00";_fixedError="";_page=ReviewPage.FixedTimes;Render();
    }
    private UIElement FixedTimesPage()
    {
        var body=new StackPanel();Field(body,"提醒名称",Input(_fixedName,"FixedName",v=>_fixedName=v));Field(body,"每天的明确时刻（HH:mm，多个用顿号或逗号隔开）",Input(_fixedTimeText,"FixedTimes",v=>_fixedTimeText=v));
        body.Children.Add(T("例如每天某个时刻安排午间休息。每个时刻独立，晚确认不会平移后续计划。",13,color:"#646464",margin:new(0,12,0,12)));
        body.Children.Add(T(_mode==RunMode.Paused?"当前普通提醒暂停。保存后仍暂停，恢复后只按未来固定时刻继续，不连续补发过去次数。":_mode==RunMode.Meeting?"当前会议模式继续，到期事项会在会后汇总。":"保存后从下一个未来时刻开始。",13));
        if(_fixedError.Length>0)body.Children.Add(T(_fixedError,13,true,margin:new(0,15,0,0)));
        return PageShell(_fixedEditing==null?"添加固定时刻提醒":"编辑固定时刻提醒","适用于普通事项；药物请从独立的服药计划入口设置。",body,Actions(Btn("返回间隔提醒",()=>{_page=ReviewPage.Add;Render();}),Btn(_mode==RunMode.Paused?"保存，保持暂停":"保存固定时刻提醒",SaveFixedOrdinary,true,"SaveFixedOrdinary")));
    }
    private void SaveFixedOrdinary()
    {
        var values=_fixedTimeText.Split([',','，','、',';','；'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);var times=new List<TimeSpan>();
        if(string.IsNullOrWhiteSpace(_fixedName)||values.Length==0){_fixedError="请填写名称和至少一个时刻。";Render();return;}
        foreach(var text in values){if(!ParseClock(text,out var t)){_fixedError="时刻请使用 HH:mm。";Render();return;}times.Add(t);}
        if(times.Distinct().Count()!=times.Count){_fixedError="同一提醒的时刻不能重复。";Render();return;}
        var p=_fixedEditing??new SamplePlan();p.Name=_fixedName.Trim();p.Kind="自定义";p.FixedTimes=true;p.Times=times.Order().ToList();p.Next=NextFixed(p.Times,_mode==RunMode.Paused?_modeUntil:_now);if(_fixedEditing==null)_plans.Add(p);
        _notice=$"已保存“{p.Name}”，下一个固定时刻 {p.Next:MM-dd HH:mm}。当前模式未改变。";_page=ReviewPage.Home;Render();
    }
}
