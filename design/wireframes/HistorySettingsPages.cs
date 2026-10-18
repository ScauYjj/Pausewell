using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Pausewell.Wireframes;

public sealed partial class ReviewWindow
{
    private ReviewRecord? _historyDetail;
    private bool _correcting,_historyEmptyDemo,_correctionUnknown;
    private string _historyType="全部",_historyStatus="全部",_historyRange="今天";
    private string _correctionStatus="已服用",_correctionTime="",_correctionReason="",_correctionError="";
    private DateTime _correctionDate=new(2026,10,15);

    private void SeedHistory()
    {
        _historyEmptyDemo=false;
        if(_records.Count>0)return;
        Seed();SeedMedicines();if(_now.Hour<21)_now=_now.Date.AddHours(21);
        var water=_plans.First();var a=GetRecord(water.Id,water.Name,"喝水",false,_now.Date.AddHours(9.5));a.Status="已完成";a.Actual=_now.Date.AddHours(9).AddMinutes(32);a.Confirmed=a.Actual;
        var activity=GetRecord("activity-example","起来活动","活动",false,_now.Date.AddHours(10));activity.Status="未执行";activity.Confirmed=_now.Date.AddHours(10).AddMinutes(3);
        var med=_medicines[0];var morning=GetRecord(med.Id,med.Name,"服药",true,_now.Date.AddHours(8));morning.Status="已服用";morning.Actual=_now.Date.AddHours(8).AddMinutes(4);morning.Confirmed=_now.Date.AddHours(8).AddMinutes(5);
        var noon=GetRecord(med.Id,med.Name,"服药",true,_now.Date.AddHours(13));noon.Status="待核实";
        var evening=GetRecord(med.Id,med.Name,"服药",true,_now.Date.AddHours(20));evening.Status="已服用";evening.Actual=_now.Date.AddHours(20).AddMinutes(5);evening.Confirmed=_now.Date.AddHours(20).AddMinutes(6);
    }
    private void OpenRecord(ReviewRecord record)
    {
        _historyDetail=record;_page=ReviewPage.History;_correcting=false;_historyEmptyDemo=false;_notice="";Render();
    }
    private void ShowReviewExample()
    {
        SeedHistory();var record=_records.FirstOrDefault(r=>r.Status=="待核实");
        if(record==null){SeedMedicines();var med=_medicines[0];record=GetRecord(med.Id,med.Name,"服药",true,_now.Date.AddDays(-1).AddHours(13));if(!record.Resolved)record.Status="待核实";}
        OpenRecord(record);
    }
    private UIElement HistoryPage()
    {
        if(_historyDetail!=null)return RecordDetailPage(_historyDetail);
        var body=new StackPanel();var filters=new WrapPanel();
        foreach(var tuple in new[]{("事项",new[]{"全部","普通提醒","服药"},_historyType),("状态",new[]{"全部","待核实","已完成","已延期","明确未服用","未执行","当前无需要"},_historyStatus),("时间",new[]{"今天","近 7 天","全部"},_historyRange)})
        {
            var label=tuple.Item1;var combo=new ComboBox { ItemsSource=tuple.Item2,SelectedItem=tuple.Item3,Padding=new Thickness(8),Width=150,Margin=new Thickness(0,0,12,0) };
            combo.SelectionChanged+=(_,_)=>{var v=combo.SelectedItem?.ToString()??"全部";if(label=="事项")_historyType=v;else if(label=="状态")_historyStatus=v;else _historyRange=v;Render();};
            var group=new StackPanel();Field(group,label,combo);filters.Children.Add(group);
        }
        body.Children.Add(filters);Gap(body,20);
        var records=_historyEmptyDemo?[]:_records.Where(r=>(_historyType=="全部"||(_historyType=="服药")==r.Medication)&&(_historyStatus=="全部"||(_historyStatus=="已完成"?r.Status is "已完成" or "已服用":r.Status==_historyStatus))&&(_historyRange=="全部"||(_historyRange=="今天"?r.Planned.Date==_now.Date:r.Planned.Date>=_now.Date.AddDays(-6)&&r.Planned.Date<=_now.Date))).OrderByDescending(r=>r.Planned).ToList();
        if(records.Count==0)body.Children.Add(Box(T("这里还没有符合条件的记录。\n预览不会写入实际记录；也可以调整筛选条件。",16)));
        else
        {
            var header=HistoryRowGrid();header.Children.Add(T("原计划 / 事项",12,color:"#646464"));var s=T("处理结果",12,color:"#646464");Grid.SetColumn(s,1);header.Children.Add(s);var at=T("实际 / 确认",12,color:"#646464");Grid.SetColumn(at,2);header.Children.Add(at);body.Children.Add(header);
            foreach(var record in records)
            {
                var row=HistoryRowGrid();var title=new StackPanel();title.Children.Add(T(record.Name,15,true));title.Children.Add(T(record.Planned.ToString("MM-dd HH:mm"),12,color:"#646464",margin:new(0,6,0,0)));row.Children.Add(title);
                var status=T(record.Status+(record.Status=="已延期"?$"\n{record.NotifyAfter:HH:mm} 再提醒":""),13);Grid.SetColumn(status,1);row.Children.Add(status);
                var actual=T(record.Status is "已完成" or "已服用"?"实际 "+(record.Actual is {} a?a.ToString("HH:mm"):"时间不详")+$"\n确认 {record.Confirmed:HH:mm}":record.Confirmed is {} c?$"确认 {c:HH:mm}":"尚未确认",12,color:"#646464");Grid.SetColumn(actual,2);row.Children.Add(actual);
                var view=Btn(record.Status=="待核实"?"核对":"查看",()=>OpenRecord(record));view.Padding=new Thickness(9,5,9,5);Grid.SetColumn(view,3);row.Children.Add(view);
                body.Children.Add(new Border { Child=row,Padding=new Thickness(0,15,0,15),BorderBrush=B("#CCCCCC"),BorderThickness=new Thickness(0,1,0,0) });
            }
        }
        return PageShell("执行记录","如实区分完成、延期、未执行和待核实，不用打卡数量代替真实情况。",body);
    }
    private static Grid HistoryRowGrid()
    {
        var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new(){Width=new GridLength(115)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(145)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(72)});return grid;
    }
    private UIElement RecordDetailPage(ReviewRecord record)
    {
        if(_correcting)return CorrectionPage(record);
        var body=new StackPanel();body.Children.Add(T(record.Name,23,true));Gap(body,16);
        foreach(var text in new[]{"本次原计划："+record.Planned.ToString("yyyy-MM-dd HH:mm"),"当前结果："+record.Status,"实际发生时间："+(record.Actual is {} a?a.ToString("yyyy-MM-dd HH:mm"):record.Status is "已完成" or "已服用"?"已确认发生，具体时间不详":"未记录"),"确认时间："+(record.Confirmed is {} c?c.ToString("yyyy-MM-dd HH:mm"):"尚未确认")})body.Children.Add(T(text,14,margin:new(0,0,0,12)));
        if(record.Medication)
        {
            var med=_medicines.FirstOrDefault(m=>m.Id==record.PlanId);body.Children.Add(Box(T("后续计划："+(med!=null?string.Join(" / ",med.Times.Select(t=>t.ToString(@"hh\:mm"))):"原计划另存")+"\n更正本次记录不会自动移动其他剂次或安排补服。",13),"#F4F4F4"));
        }
        Gap(body,18);body.Children.Add(T("更正历史",16,true));
        if(record.Changes.Count==0)body.Children.Add(T("这条记录尚未更正。",13,color:"#646464",margin:new(0,10,0,0)));
        else foreach(var change in record.Changes)body.Children.Add(T(change,12,color:"#646464",margin:new(0,10,0,0)));
        return PageShell("记录详情","原计划、实际时间与确认时间分别保留。",body,Actions(Btn("返回记录",()=>{_historyDetail=null;Render();}),Btn(record.Resolved?"更正这条记录":"核对这条记录",()=>
        {
            if(!record.Resolved&&record.Medication){_focusedRecord=record;_recordOperation=_recordError="";_page=ReviewPage.Reminder;_alertKind="服药";_returnToQueue=false;Render();}
            else BeginCorrection(record);
        },true,"CorrectRecord")));
    }
    private void BeginCorrection(ReviewRecord record)
    {
        _correcting=true;_correctionStatus=record.Status;_correctionTime=(record.Actual??_now).ToString("HH:mm");_correctionDate=(record.Actual??_now).Date;_correctionUnknown=record.Actual==null;_correctionReason=_correctionError="";Render();
    }
    private UIElement CorrectionPage(ReviewRecord record)
    {
        var body=new StackPanel();body.Children.Add(Box(T($"{record.Name} · 原计划 {record.Planned:MM-dd HH:mm}\n当前记录：{record.Status}。原值会保留在更正历史中。",14),"#F4F4F4"));
        var states=record.Medication?new[]{"已服用","明确未服用","待核实"}:new[]{"已完成","未执行","当前无需要"};if(!states.Contains(_correctionStatus))_correctionStatus=states[0];
        var status=new ComboBox { ItemsSource=states,SelectedItem=_correctionStatus,Padding=new Thickness(8) };status.SelectionChanged+=(_,_)=>{_correctionStatus=status.SelectedItem?.ToString()??states[0];Render();};Field(body,"更正为",status);
        if(_correctionStatus is "已服用" or "已完成")
        {
            var date=new DatePicker { SelectedDate=_correctionDate,Width=160,HorizontalAlignment=HorizontalAlignment.Left };date.SelectedDateChanged+=(_,_)=>_correctionDate=date.SelectedDate??_now.Date;Field(body,"实际日期",date);
            var time=Input(_correctionTime,"CorrectionActualTime",v=>_correctionTime=v);time.IsEnabled=!_correctionUnknown;Field(body,"实际时刻（HH:mm）",time);
            var unknown=new CheckBox { Content="具体时间记不清",IsChecked=_correctionUnknown,Margin=new Thickness(0,10,0,0) };unknown.Click+=(_,_)=>{_correctionUnknown=unknown.IsChecked==true;time.IsEnabled=!_correctionUnknown;};body.Children.Add(unknown);
        }
        Field(body,"更正原因（必填）",Input(_correctionReason,"CorrectionReason",v=>_correctionReason=v,true));
        if(_correctionError.Length>0)body.Children.Add(T(_correctionError,13,true,margin:new(0,10,0,0)));
        return PageShell("更正记录","只更正已核实的情况。记录原值、原因和本次更正时间都会保留。",body,Actions(Btn("取消，不更正",()=>{_correcting=false;Render();}),Btn("保存更正",SaveCorrection,true,"SaveCorrection")));
    }
    private void SaveCorrection()
    {
        var record=_historyDetail!;
        if(string.IsNullOrWhiteSpace(_correctionReason)){_correctionError="请填写更正原因。";Render();return;}
        DateTime? actual=null;if((_correctionStatus is "已服用" or "已完成")&&!_correctionUnknown)
        {
            if(!ParseClock(_correctionTime,out var clock)){_correctionError="实际时间请使用 HH:mm。";Render();return;}
            actual=_correctionDate.Date.Add(clock);if(actual>_now){_correctionError="实际时间不能晚于当前示例时间。";Render();return;}
        }
        record.Changes.Add($"{_now:MM-dd HH:mm} 更正：{record.Status}（实际 {record.Actual:MM-dd HH:mm}）→ {_correctionStatus}（实际 {actual:MM-dd HH:mm}）。原因：{_correctionReason.Trim()}");
        record.Status=_correctionStatus;record.Actual=actual;record.Confirmed=_correctionStatus=="待核实"?null:_now;_correcting=false;_notice="已保存更正，原记录与原因已保留，后续计划未改变。";Render();
    }
    private UIElement SettingsPage()
    {
        var body=new StackPanel();
        body.Children.Add(T("启动与关闭",18,true));body.Children.Add(T("打开软件时：自动恢复已保存的提醒（按已确认的使用方式）。明确暂停与会议期限仍会保留。",13,margin:new(0,10,0,12)));
        var startup=new CheckBox { Content="随 Windows 开机打开间歇",IsChecked=_settingsDraft.Startup,Margin=new Thickness(0,5,0,12) };startup.Click+=(_,_)=>_settingsDraft.Startup=startup.IsChecked==true;body.Children.Add(startup);
        var close=new ComboBox { ItemsSource=new[]{"继续在托盘运行","询问是否退出"},SelectedItem=_settingsDraft.CloseAction,Padding=new Thickness(8) };close.SelectionChanged+=(_,_)=>_settingsDraft.CloseAction=close.SelectedItem?.ToString()??"继续在托盘运行";Field(body,"点击主窗口关闭按钮时",close);Gap(body,22);
        body.Children.Add(T("提醒与隐私",18,true));
        var sound=new CheckBox { Content="允许普通模式下的提醒音",IsChecked=_settingsDraft.Sound,Margin=new Thickness(0,12,0,8) };sound.Click+=(_,_)=>_settingsDraft.Sound=sound.IsChecked==true;body.Children.Add(sound);
        var hide=new CheckBox { Content="外部通知中隐藏药名等敏感内容",IsChecked=_settingsDraft.HideMedicineNames,Margin=new Thickness(0,5,0,8) };hide.Click+=(_,_)=>_settingsDraft.HideMedicineNames=hide.IsChecked==true;body.Children.Add(hide);
        body.Children.Add(T("主动打开服药与记录页时仍显示名称；会议和共享场景需要额外注意呈现方式。",12,color:"#646464"));
        var monitors=new ComboBox { ItemsSource=new[]{"鼠标所在的显示器","主显示器","全部显示器"},SelectedItem=_settingsDraft.Monitor,Padding=new Thickness(8) };monitors.SelectionChanged+=(_,_)=>_settingsDraft.Monitor=monitors.SelectedItem?.ToString()??"鼠标所在的显示器";Field(body,"某条事项选择全屏提醒时，覆盖哪里",monitors);Gap(body,22);
        body.Children.Add(T("以后新增的普通提醒",18,true));
        var office=new CheckBox { Content="默认仅周一至周五 09:00 至 18:00 生效",IsChecked=_settingsDraft.NewOffice,Margin=new Thickness(0,12,0,8) };office.Click+=(_,_)=>_settingsDraft.NewOffice=office.IsChecked==true;body.Children.Add(office);
        body.Children.Add(T("只影响以后新增时的默认值，不修改已有计划，也不应用到药物。周一至周五不等于法定工作日。",12,color:"#646464"));Gap(body,20);
        body.Children.Add(Box(T("提醒与记录保存在这台电脑。此阶段不涉及账号、云同步或上传健康记录。",13),"#F4F4F4"));
        return PageShell("设置","基本运行偏好集中在这里；每条事项的具体规则在对应计划中调整。",body,Actions(Btn("取消修改",()=>{_settingsDraft=_settings.Copy();_notice="未保存本次设置修改。";Render();},id:"CancelSettings"),Btn("保存设置",()=>{_settings=_settingsDraft.Copy();_notice="设置已保存。已有提醒计划与记录未改变。";Render();},true,"SaveSettings")));
    }
}
