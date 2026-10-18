using System.Windows;
using System.Windows.Controls;

namespace Pausewell.Wireframes;

public sealed partial class ReviewWindow
{
    private readonly List<string> _queueIds=[];
    private string _modeFormError="";
    private bool _draftPauseMedicines,_draftMeetingSilent;

    private void OpenModeEditor(RunMode mode)
    {
        Seed();SeedMedicines();_modeDraft=mode;_modeStage="edit";_durationText="30";_draftPauseMedicines=_pauseMedicines;_draftMeetingSilent=_meetingSilent;_modeFormError="";_page=ReviewPage.Meeting;Render();
    }
    private UIElement MeetingPage()
    {
        if(_modeStage=="queue")return MeetingQueuePage();
        var body=new StackPanel();
        if(_modeStage=="active")
        {
            body.Children.Add(Box(T(ModeText(),23,true)));Gap(body,16);
            body.Children.Add(T(_mode==RunMode.Meeting?"普通提醒不全屏、不响铃；到期事项保留到会后处理。":"普通间隔暂缓，到期恢复时重新计时。",15));
            body.Children.Add(T(_mode==RunMode.Meeting?(_meetingSilent?"本次会议完全静音。药物原计划保留，会后需要核对。":"药物到期只使用隐私提示，不展示药名、不全屏、不主动响铃。"):(_pauseMedicines?"本次也暂停服药提示。缺少确认的剂次恢复后保留待核实。":"服药提醒未暂停，仍按原计划时刻进行。"),14,margin:new(0,12,0,15)));
            if(_mode==RunMode.Meeting)
            {
                var sample=new StackPanel();sample.Children.Add(T("会议期间的提示示意",12,color:"#646464"));sample.Children.Add(T(_meetingSilent?"完全静音：不会即时显示提示。":_settings.HideMedicineNames?"个人计划需要处理，方便时请打开核对。":"药物 A（示例）有一项待核对记录。",16,true,margin:new(0,10,0,0)));body.Children.Add(Box(sample,"#F4F4F4"));
            }
            return PageShell(_mode==RunMode.Meeting?"会议模式已开始":"普通提醒已暂停","结束时间始终可见，你可以延长或提前结束。",body,Actions(Btn("延长 15 分钟",()=>{_modeUntil=_modeUntil.AddMinutes(15);_notice=$"已延长至 {_modeUntil:HH:mm}，原计划时刻未改变。";Render();},id:"ExtendMode"),Btn("返回首页，保持当前模式",()=>{_page=ReviewPage.Home;Render();}),Btn(_mode==RunMode.Meeting?"结束会议":"现在恢复",EndModeToBuffer,true,"EndMode")));
        }
        if(_modeStage=="buffer")
        {
            body.Children.Add(Box(T($"会议已结束，收尾至 {_bufferUntil:HH:mm:ss}",23,true)));Gap(body,18);
            body.Children.Add(T("保留 30 秒处理收尾工作，之后展示一份待处理清单。普通事项合并展示，每项单独记录；药物单独核对。",15));
            return PageShell("先留一点收尾时间","普通提醒不连续补发过去次数。",body,Actions(Btn("现在查看待办",ShowMeetingQueue,true,"ShowQueueNow")));
        }
        if(_modeStage=="finished")
        {
            body.Children.Add(Box(T("已恢复提醒",23,true)));Gap(body,16);body.Children.Add(T(_notice,15));
            return PageShell("暂停已经结束","普通间隔重新计时；药物原计划不平移。",body,Actions(Btn("返回首页",()=>{_page=ReviewPage.Home;Render();},true)));
        }
        var choices=new WrapPanel();foreach(var (text,mode) in new[]{("我要开会",RunMode.Meeting),("暂停普通提醒",RunMode.Paused)}){var m=mode;var b=Btn(text,()=>{_modeDraft=m;Render();},_modeDraft==m);b.Margin=new Thickness(0,0,10,0);choices.Children.Add(b);}body.Children.Add(choices);Gap(body,16);
        TextBlock until=T("",14,true);void UpdateUntil(){until.Text=int.TryParse(_durationText,out var m)&&m is >0 and <=720?$"预计 {_now.AddMinutes(m):HH:mm} 自动恢复。":"请选择 1 至 720 分钟。";}
        var durations=new WrapPanel();foreach(var duration in new[]{15,30,60,90}){var d=duration;var b=Btn(d+" 分钟",()=>{_durationText=d.ToString();Render();},_durationText==d.ToString());b.Margin=new Thickness(0,0,8,8);durations.Children.Add(b);}Field(body,"持续多久",durations);
        var input=Input(_durationText,"ModeMinutes",v=>{_durationText=v;UpdateUntil();});input.Width=100;input.HorizontalAlignment=HorizontalAlignment.Left;body.Children.Add(input);UpdateUntil();body.Children.Add(until);Gap(body,22);
        if(_modeDraft==RunMode.Meeting)
        {
            body.Children.Add(T("会议期间怎样处理提醒",16,true));Gap(body,10);
            body.Children.Add(T("普通事项：不全屏、不响铃，到期后保留待办，会后汇总。",14));
            var silent=new CheckBox { Content="会议期间也关闭服药的隐私提示",IsChecked=_draftMeetingSilent,Margin=new Thickness(0,14,0,8) };
            var explain=T(_draftMeetingSilent?"本次会议不会即时显示服药提示；原计划保留，会后核对未确认记录。":"服药默认保留隐私提示，不显示药名。会议只改变呈现，不改变原计划时刻。",13,color:"#646464");
            silent.Click+=(_,_)=>{_draftMeetingSilent=silent.IsChecked==true;explain.Text=_draftMeetingSilent?"本次会议不会即时显示服药提示；原计划保留，会后核对未确认记录。":"服药默认保留隐私提示，不显示药名。会议只改变呈现，不改变原计划时刻。";};body.Children.Add(silent);body.Children.Add(explain);
        }
        else
        {
            body.Children.Add(T("默认只暂停普通提醒",16,true));Gap(body,10);body.Children.Add(T("普通间隔恢复后重新计时；普通固定时刻按未来原计划继续。服药仍按既有时刻提示。",14));
            var all=new CheckBox { Content="本次同时暂停服药提示",IsChecked=_draftPauseMedicines,Margin=new Thickness(0,14,0,8) };
            var explain=T(_draftPauseMedicines?"本次服药提示也会暂停。缺少确认的剂次保留待核实，恢复时不自动要求补服。":"服药提示保持运行。",13,color:"#646464");
            all.Click+=(_,_)=>{_draftPauseMedicines=all.IsChecked==true;explain.Text=_draftPauseMedicines?"本次服药提示也会暂停。缺少确认的剂次保留待核实，恢复时不自动要求补服。":"服药提示保持运行。";};body.Children.Add(all);body.Children.Add(explain);
        }
        if(_modeFormError.Length>0)body.Children.Add(T(_modeFormError,13,true,margin:new(0,12,0,0)));
        return PageShell("临时无法执行时，先设好恢复时间","关闭这个页面不代表开始模式，只有确认后才生效。",body,Actions(Btn("取消，返回首页",()=>{_page=ReviewPage.Home;Render();}),Btn(_modeDraft==RunMode.Meeting?"开始会议模式":"确认暂停",ApplyMode,true,"ApplyMode")));
    }
    private void ApplyMode()
    {
        if(!int.TryParse(_durationText,out var minutes)||minutes is <1 or >720){_modeFormError="请填写 1 至 720 的整数分钟。";Render();return;}
        _mode=_modeDraft;_pauseMedicines=_mode==RunMode.Paused&&_draftPauseMedicines;_meetingSilent=_mode==RunMode.Meeting&&_draftMeetingSilent;_modeUntil=_now.AddMinutes(minutes);_bufferUntil=null;_modeStage="active";_modeFormError="";_notice=$"已设置至 {_modeUntil:HH:mm} 自动恢复。";Render();
    }
    private void RestoreMedicineNotice()
    {
        if(_pauseMedicines)foreach(var r in _records.Where(r=>r.Medication&&!r.Resolved&&r.Planned<=_now))r.Status="待核实";
        _pauseMedicines=false;
        foreach(var p in _plans.Where(p=>p.Enabled&&p.FixedTimes))p.Next=NextFixed(p.Times,_now);
    }
    private void EndModeToBuffer()=>FinishMode(false);
    private void SimulateModeEnd()=>FinishMode(true);
    private void FinishMode(bool atDeadline)
    {
        if(_mode==RunMode.Running){_mode=_modeDraft;_modeUntil=_now.AddMinutes(int.TryParse(_durationText,out var m)&&m>0?m:60);}
        if(atDeadline)_now=_modeUntil;
        var wasPause=_mode==RunMode.Paused;Restore();_modeStage=wasPause?"finished":"buffer";Render();
    }
    private void ShowMeetingQueue()
    {
        if(_bufferUntil is {} until&&_now<until)_now=until;SeedMeetingQueue();_modeStage="queue";Render();
    }
    private void SeedMeetingQueue()
    {
        Seed();SeedMedicines();_mode=RunMode.Running;_queueIds.Clear();_notice="收尾已结束，待办已汇总，请逐项处理。";
        if(_now.Hour<13)_now=_now.Date.AddHours(13).AddSeconds(30);
        foreach(var (kind,time) in new[]{("喝水",new TimeSpan(12,30,0)),("活动",new TimeSpan(12,45,0))})
        {
            var plan=_plans.FirstOrDefault(p=>p.Kind==kind);if(plan==null){plan=new(){Name=kind=="喝水"?"喝水":"起来活动",Kind=kind,Minutes=30};_plans.Add(plan);}
            var record=GetRecord(plan.Id,plan.Name,kind,false,_now.Date.Add(time));_queueIds.Add(record.Id);
        }
        var med=_medicines[0];var drug=GetRecord(med.Id,med.Name,"服药",true,_now.Date.AddHours(13));if(!drug.Resolved)drug.Status="待核实";_queueIds.Add(drug.Id);
    }
    private UIElement MeetingQueuePage()
    {
        var body=new StackPanel();body.Children.Add(T("普通事项合并展示，每项独立处理；不会把错过的次数连续补发。",14));Gap(body,18);
        var items=_records.Where(r=>_queueIds.Contains(r.Id)).ToList();
        foreach(var r in items.Where(r=>!r.Medication))body.Children.Add(QueueRow(r));
        Gap(body,18);body.Children.Add(T("服药记录单独核对",17,true));body.Children.Add(T("原计划时刻保留。缺少确认不代表一定未服用。",13,color:"#646464",margin:new(0,8,0,12)));
        foreach(var r in items.Where(r=>r.Medication))body.Children.Add(QueueRow(r));
        return PageShell("会后待处理","已经恢复正常提醒，先处理当前这份清单。",body,Actions(Btn("返回首页，保留待办",()=>{_page=ReviewPage.Home;_notice="待办继续保留，可从记录中核对；未记为完成。";Render();},true)));
    }
    private UIElement QueueRow(ReviewRecord record)
    {
        var row=new Grid { Margin=new Thickness(0,10,0,10) };row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var text=new StackPanel();text.Children.Add(T(record.Name,16,true));text.Children.Add(T($"原计划 {record.Planned:HH:mm} · {record.Status}"+(record.Status=="已延期"?$"，{record.NotifyAfter:HH:mm} 再提醒":""),13,color:"#646464",margin:new(0,7,0,0)));row.Children.Add(text);
        var handle=Btn(record.Resolved?"查看记录":record.Medication?"核对本次记录":"处理",()=>{if(record.Resolved){OpenRecord(record);return;}_focusedRecord=record;_alertKind=record.Medication?"服药":record.Kind;_recordOperation=_recordError="";_returnToQueue=true;_actualDate=_now.Date;_actualTime=_now.ToString("HH:mm");_activityStarted=_activityReady=false;_page=ReviewPage.Reminder;Render();});Grid.SetColumn(handle,1);row.Children.Add(handle);
        return new Border { Child=row,BorderBrush=B("#CCCCCC"),BorderThickness=new Thickness(0,1,0,0) };
    }
}
