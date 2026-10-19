using Pausewell.Controls;
using Pausewell.Presentation;
using System.Windows.Automation;
using System.Windows.Threading;

namespace Pausewell.Desktop;

public sealed class ModeWindow : MaterialWindow
{
    private readonly DesktopController _app;
    private readonly bool _pause;
    private readonly Dictionary<int,RadioButton> _choices=[];
    private bool _submitted,_submitting;
    private readonly DispatcherTimer _clock=new(){Interval=TimeSpan.FromSeconds(1)};
    public TextBox MinutesInput {get;}=Ui.Input("60","持续分钟","ModeMinutes");
    public CheckBox ScopeInput {get;}=new();
    public Button ApplyButton {get;}
    public TextBlock ErrorMessage {get;}=TaskUi.Error();
    public TextBlock RecoveryLabel {get;}=Ui.Text("",16,true);
    private readonly TextBlock _scopeHint=Ui.Text("",13,brush:"Muted");
    public ModeWindow(DesktopController app,bool pause)
    {
        _app=app;_pause=pause;Title=pause?"暂停普通提醒":"开始会议";Width=584;Height=650;MinWidth=500;MinHeight=480;
        var body=new StackPanel();var choices=new WrapPanel{Margin=new(0,0,0,16)};
        foreach(var duration in new[]{15,30,60,120})
        {
            var choice=new RadioButton{Content=duration+" 分钟",GroupName="ModeDuration",IsChecked=duration==60,Margin=new(0,0,8,8)};
            AutomationProperties.SetAutomationId(choice,"ModeDuration"+duration);choice.Checked+=(_,_)=>{if(MinutesInput.Text!=duration.ToString())MinutesInput.Text=duration.ToString();};_choices[duration]=choice;choices.Children.Add(choice);
        }
        body.Children.Add(Ui.Text("持续多久",18,true,margin:new(0,0,0,12)));body.Children.Add(choices);body.Children.Add(TaskUi.Field("自定义分钟（1 至 720）",MinutesInput));
        var resume=new StackPanel();resume.Children.Add(RecoveryLabel);resume.Children.Add(Ui.Text(pause?"普通间隔从恢复时刻重新计算；固定及一次性时刻保留。":"普通事项会后汇总，结束后有 30 秒收尾时间。",13,brush:"Muted",margin:new(0,8,0,0)));body.Children.Add(Ui.Panel(resume,new(16)));Ui.Gap(body,24);
        ScopeInput.Content=pause?"同时暂停服药提示":"会议期间完全静音";AutomationProperties.SetAutomationId(ScopeInput,"ModeScope");body.Children.Add(ScopeInput);_scopeHint.Margin=new(0,8,0,0);body.Children.Add(_scopeHint);
        ApplyButton=Ui.Button(pause?"开始暂停":"开始会议",Apply,true,"ApplyMode");var footer=new StackPanel();footer.Children.Add(ErrorMessage);footer.Children.Add(TaskUi.Actions(Ui.Button("取消",Close,id:"CancelMode"),ApplyButton));
        SetBody(TaskUi.Layout(Title,pause?"选定恢复时间，届时自动恢复普通提醒。":"先安排会议时长，再明确期间的提醒方式。",body,footer));
        MinutesInput.TextChanged+=(_,_)=>UpdatePreview();ScopeInput.Checked+=(_,_)=>UpdatePreview();ScopeInput.Unchecked+=(_,_)=>UpdatePreview();Loaded+=(_,_)=>{MinutesInput.Focus();_clock.Start();};_clock.Tick+=(_,_)=>UpdateRecovery();Closed+=(_,_)=>_clock.Stop();UpdatePreview();
    }
    private void UpdatePreview()
    {
        var valid=int.TryParse(MinutesInput.Text,out var duration)&&duration>=1&&duration<=720;
        foreach(var pair in _choices)pair.Value.IsChecked=valid&&pair.Key==duration;
        if(_submitted)FieldState.SetError(MinutesInput,valid?"":"请输入 1 至 720 的整数分钟。");
        ErrorMessage.Text="";UpdateRecovery();
        _scopeHint.Text=_pause?(ScopeInput.IsChecked==true?"普通提醒和服药提示都暂停，原有记录保留。":"服药继续按原计划提示，不受普通暂停影响。"):(ScopeInput.IsChecked==true?"会议期间普通提醒和服药提示均不打扰，原计划保留。":"普通提醒会后处理；服药使用轻提示，遵循药名隐私设置。");
    }
    private void UpdateRecovery()
    {
        RecoveryLabel.Text=int.TryParse(MinutesInput.Text,out var duration)&&duration>=1&&duration<=720?$"预计{(_pause?"恢复":"结束")}：{_app.Engine.Local(DateTimeOffset.UtcNow.AddMinutes(duration)):MM-dd HH:mm}":"填写有效分钟后显示恢复时间。";
    }
    private void Apply()
    {
        if(_submitting)return;_submitted=true;UpdatePreview();
        if(!int.TryParse(MinutesInput.Text,out var duration)||duration<1||duration>720){MinutesInput.Focus();return;}
        _submitting=true;ApplyButton.IsEnabled=false;
        try
        {
            var now=DateTimeOffset.UtcNow;if(_pause)_app.Engine.BeginPause(now.AddMinutes(duration),ScopeInput.IsChecked==true,now);else _app.Engine.BeginMeeting(now.AddMinutes(duration),now,ScopeInput.IsChecked==true);
            if(_pause&&ScopeInput.IsChecked!=true)_app.CloseOrdinaryReminders();else _app.CloseReminders();_app.Main.Refresh();Close();
        }
        catch(Exception ex){ErrorMessage.Text="未开始，输入已保留："+TaskUi.Failure(ex);}
        finally{_submitting=false;ApplyButton.IsEnabled=true;}
    }
}
public sealed class QueueWindow : MaterialWindow
{
    private readonly DesktopController _app;private readonly StackPanel _items=new();
    private readonly TextBlock _error=TaskUi.Error();
    public QueueWindow(DesktopController app)
    {
        _app=app;Title="待处理清单";Width=784;Height=720;MinWidth=580;MinHeight=480;
        var footer=new StackPanel();footer.Children.Add(_error);footer.Children.Add(Ui.Text("返回首页会保留本次处理状态；需要延期时请明确选择。",12,brush:"Muted",margin:new(0,0,0,8)));footer.Children.Add(TaskUi.Actions(Ui.Button("返回首页",Close,id:"CloseQueue")));
        SetBody(TaskUi.Layout("待处理清单","普通事项逐项处理，服药记录单独核对。",_items,footer));
        Closing+=(_,e)=>{if(app.Quitting)return;try{app.Engine.DismissMeetingSummary(DateTimeOffset.UtcNow);}catch(Exception ex){e.Cancel=true;_error.Text="未保存，清单仍保留："+TaskUi.Failure(ex);}};
        Refresh();
    }
    private void Refresh()
    {
        _items.Children.Clear();var all=_app.Engine.State.Occurrences.Where(o=>o.Status is OccurrenceStatus.Pending or OccurrenceStatus.Deferred or OccurrenceStatus.NeedsReview).OrderBy(o=>o.Kind==HabitKind.Medication?1:0).ThenBy(o=>o.DueAt).ToList();
        if(all.Count==0){_items.Children.Add(Ui.Notice("当前没有待处理事项。"));return;}
        if(all.Any(o=>o.Kind!=HabitKind.Medication))_items.Children.Add(Ui.Text("普通事项",18,true,margin:new(0,0,0,12)));
        var med=false;foreach(var o in all)
        {
            if(o.Kind==HabitKind.Medication&&!med){_items.Children.Add(Ui.Text("服药记录单独核对",19,true,margin:new(0,22,0,12)));med=true;}
            var row=new DockPanel();var b=Ui.Button(o.Kind==HabitKind.Medication?"核对本次记录":"处理",()=>{_app.OpenItems([o]);Hide();var active=_app.OpenReminders.LastOrDefault();if(active!=null)active.Closed+=(_,_)=>{if(IsLoaded){Show();Refresh();}};});DockPanel.SetDock(b,Dock.Right);row.Children.Add(b);var labels=new StackPanel();labels.Children.Add(Ui.Text(o.PlanName,16,true));labels.Children.Add(Ui.Text($"原计划 {_app.Engine.Local(o.DueAt):MM-dd HH:mm} · {Ui.Status(o)}",13,brush:"Muted",margin:new(0,7,0,0)));row.Children.Add(labels);var panel=Ui.Panel(row);panel.Margin=new(0,0,0,12);_items.Children.Add(panel);
        }
    }
}
