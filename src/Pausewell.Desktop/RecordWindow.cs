using Pausewell.Controls;
using Pausewell.Presentation;
using System.Windows.Automation;

namespace Pausewell.Desktop;

public sealed class RecordWindow : MaterialWindow
{
    private readonly DesktopController _app;
    private readonly string _id;
    private readonly bool _readOnly;
    private bool _editing,_saving;
    public IsoDatePicker? ActualDate {get;private set;}
    public TextBox? ActualTime {get;private set;}
    public TextBox? ReasonInput {get;private set;}
    public CheckBox? UnknownTime {get;private set;}
    public ComboBox? StatusInput {get;private set;}
    public TextBlock ErrorMessage {get;private set;}=TaskUi.Error();
    public Button? SaveButton {get;private set;}
    public RecordWindow(DesktopController app,string id,bool readOnly=false)
    {
        _app=app;_id=id;_readOnly=readOnly;Title=readOnly?"只读记录详情":"记录详情";Width=680;Height=780;MinWidth=560;MinHeight=480;Render();
    }
    private string StatusLabel(Occurrence record,OccurrenceStatus status)=>status switch
    {
        OccurrenceStatus.Completed=>record.Kind==HabitKind.Medication?"已服用":"已完成",
        OccurrenceStatus.Skipped=>record.Kind==HabitKind.Medication?"未服用":"未执行",
        _=>Labels.Status(status)
    };
    private string At(DateTimeOffset? value)=>value is {} at?_app.Engine.Local(at).ToString("yyyy-MM-dd HH:mm"):"未记录";
    private void Render()
    {
        var record=_app.Engine.State.Occurrences.Single(o=>o.Id==_id);Title=_editing?"更正记录":_readOnly?"只读记录详情":"记录详情";
        var body=new StackPanel();body.Children.Add(TaskUi.Summary(record.PlanName,Ui.Status(record)+" · 原计划 "+At(record.DueAt)));Ui.Gap(body,24);
        if(record.EndReason.Length>0)body.Children.Add(Ui.Notice(record.EndReason));
        if(record.Activity is {} activity)body.Children.Add(Ui.Notice($"辅助计时累计 {(int)activity.AccumulatedSeconds} 秒。{activity.EndReason}。计时本身不代表已完成。"));
        ErrorMessage=TaskUi.Error();var footer=new StackPanel();footer.Children.Add(ErrorMessage);
        if(_editing)
        {
            var states=new List<OccurrenceStatus>{OccurrenceStatus.Completed,OccurrenceStatus.Skipped};
            if(record.Kind==HabitKind.Medication)states.Add(OccurrenceStatus.NeedsReview);if(record.Kind==HabitKind.Toilet)states.Add(OccurrenceStatus.NotNeeded);
            StatusInput=new ComboBox{ItemsSource=states.Select(s=>StatusLabel(record,s)).ToArray(),SelectedIndex=Math.Max(0,states.IndexOf(record.Status))};PageUi.Id(StatusInput,"CorrectionStatus","更正后的状态");body.Children.Add(TaskUi.Field("更正后的状态",StatusInput));
            var local=_app.Engine.Local(record.ActualAt??DateTimeOffset.UtcNow);
            ActualDate=new(){SelectedDate=local.Date};PageUi.Id(ActualDate,"CorrectionActualDate","更正实际日期");
            ActualTime=Ui.Input(local.ToString("HH:mm"),"更正实际时间","CorrectionActualTime");
            body.Children.Add(PageUi.Pair(TaskUi.Field("实际执行日期",ActualDate),TaskUi.Field("实际时刻 HH:mm",ActualTime)));
            UnknownTime=new CheckBox{Content="记不清具体执行时间",IsChecked=record.ActualAt==null};PageUi.Id(UnknownTime,"CorrectionUnknownTime","记不清具体执行时间");body.Children.Add(UnknownTime);
            body.Children.Add(Ui.Text("仅已完成或已服用时记录实际时间。不确定时可留空，不会自动填成确认时间。",13,brush:"Muted",margin:new(0,8,0,24)));
            ReasonInput=Ui.Input("","更正原因","CorrectionReason");ReasonInput.AcceptsReturn=true;ReasonInput.TextWrapping=TextWrapping.Wrap;ReasonInput.MinHeight=88;ReasonInput.MaxHeight=180;ReasonInput.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
            body.Children.Add(TaskUi.Field("更正原因（必填，最多 500 字）",ReasonInput));
            ReasonInput.TextChanged+=(_,_)=>{FieldState.SetError(ReasonInput,"");ErrorMessage.Text="";};ActualTime.TextChanged+=(_,_)=>{FieldState.SetError(ActualTime,"");ErrorMessage.Text="";};
            void Adjust()
            {
                var completed=states[StatusInput.SelectedIndex]==OccurrenceStatus.Completed;UnknownTime.IsEnabled=completed;ActualDate.IsEnabled=ActualTime.IsEnabled=completed&&UnknownTime.IsChecked!=true;
                FieldState.SetError(ActualDate,"");FieldState.SetError(ActualTime,"");ErrorMessage.Text="";
            }
            UnknownTime.Checked+=(_,_)=>Adjust();UnknownTime.Unchecked+=(_,_)=>Adjust();StatusInput.SelectionChanged+=(_,_)=>Adjust();Adjust();
            SaveButton=Ui.Button("保存更正",Save,true,"SaveCorrection");
            footer.Children.Add(Ui.Text("取消会放弃本次更正。保存后保留修改历史，后续计划不变。",12,brush:"Muted",margin:new(0,0,0,8)));
            footer.Children.Add(TaskUi.Actions(Ui.Button("取消更正",()=>{_editing=false;Render();},id:"CancelCorrection"),SaveButton));
            void Save()
            {
                if(_saving)return;ErrorMessage.Text="";FieldState.SetError(ReasonInput,"");FieldState.SetError(ActualTime,"");
                var status=states[StatusInput.SelectedIndex];DateTimeOffset? actual=null;
                if(string.IsNullOrWhiteSpace(ReasonInput.Text)||ReasonInput.Text.Trim().Length>500){FieldState.SetError(ReasonInput,"请填写 1 至 500 字的更正原因。");ErrorMessage.Text="未保存，请补充更正原因。";ReasonInput.Focus();return;}
                if(status==OccurrenceStatus.Completed&&UnknownTime.IsChecked!=true)
                {
                    if(!ActualDate.TryCommit()){ErrorMessage.Text="未保存，请修正实际日期。";ActualDate.Focus();return;}
                    try{actual=_app.Engine.At(DateOnly.FromDateTime(ActualDate.SelectedDate!.Value),ReminderEngine.ParseTime(ActualTime.Text.Trim()));}
                    catch(ArgumentException ex){FieldState.SetError(ActualTime,ex.Message);ErrorMessage.Text="未保存，请修正实际时刻。";ActualTime.Focus();return;}
                    if(actual>DateTimeOffset.UtcNow){FieldState.SetError(ActualTime,"实际时间不能晚于现在。");ErrorMessage.Text="未保存，请核对日期和时间。";ActualTime.Focus();return;}
                }
                _saving=true;SaveButton!.IsEnabled=false;
                try{_app.Engine.CorrectRecord(_id,status,actual,ReasonInput.Text,DateTimeOffset.UtcNow);_editing=false;Render();_app.Main.RefreshIfChanged();}
                catch(Exception ex){ErrorMessage.Text="未保存，填写内容已保留："+TaskUi.Failure(ex);}
                finally{_saving=false;if(SaveButton!=null)SaveButton.IsEnabled=true;}
            }
        }
        else
        {
            var times=new StackPanel();
            foreach(var (label,value) in new[]{("原计划时间",At(record.DueAt)),("实际执行时间",record.ActualAt==null?"未记录或不确定":At(record.ActualAt)),("本次确认时间",record.ResolvedAt==null?"尚未确认":At(record.ResolvedAt))})
            {times.Children.Add(Ui.Text(label,12,brush:"Muted"));times.Children.Add(Ui.Text(value,15,margin:new(0,6,0,16)));}
            body.Children.Add(Ui.Panel(times,new(16)));Ui.Gap(body,24);
            if(!string.IsNullOrWhiteSpace(record.Note)){body.Children.Add(Ui.Text("事项说明",14,true));body.Children.Add(Ui.Text(record.Note,14,margin:new(0,8,0,24)));}
            var actions=new List<UIElement>{Ui.Button("返回",Close,id:"CloseRecord")};
            if(!_readOnly&&record.Status is (OccurrenceStatus.Pending or OccurrenceStatus.Deferred or OccurrenceStatus.NeedsReview))actions.Add(Ui.Button("处理本次记录",()=>{Close();_app.OpenItems([record]);},true,"HandleRecord"));
            if(!_readOnly&&record.Status is not (OccurrenceStatus.Pending or OccurrenceStatus.Deferred))actions.Add(Ui.Button("更正记录",()=>{_editing=true;Render();},id:"CorrectRecord"));
            footer.Children.Add(TaskUi.Actions(actions.ToArray()));
            if(record.Corrections.Count==0)body.Children.Add(Ui.Text("暂无更正记录。",13,brush:"Muted"));
        }
        if(record.Corrections.Count>0)
        {
            body.Children.Add(Ui.Text("修改历史",18,true,margin:new(0,0,0,12)));
            foreach(var change in record.Corrections.AsEnumerable().Reverse())
            {
                var history=new StackPanel();history.Children.Add(Ui.Text(At(change.At),12,brush:"Muted"));history.Children.Add(Ui.Text(StatusLabel(record,change.Before)+" → "+StatusLabel(record,change.After),14,true,margin:new(0,8,0,0)));
                history.Children.Add(Ui.Text("更正前实际："+At(change.BeforeActual)+"\n更正后实际："+At(change.AfterActual)+"\n更正前确认："+At(change.BeforeConfirmed),12,brush:"Muted",margin:new(0,8,0,0)));
                history.Children.Add(Ui.Text("原因："+change.Reason,14,margin:new(0,12,0,0)));var card=Ui.Panel(history,new(16));card.Margin=new(0,0,0,12);body.Children.Add(card);
            }
        }
        SetBody(TaskUi.Layout(_editing?"更正本次记录":_readOnly?"只读记录详情":"记录详情",_editing?"按实际情况修改，原计划时间始终保留。":_readOnly?"仅供查阅，不会修改当前提醒或执行记录。":"时间使用中国标准时间，实际执行与确认分别保存。",body,footer));
    }
}
