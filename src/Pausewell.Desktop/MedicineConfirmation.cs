using Pausewell.Controls;
using Pausewell.Presentation;
using System.Windows.Automation;

namespace Pausewell.Desktop;

public sealed class MedicineConfirmation : MaterialWindow
{
    public IsoDatePicker ActualDate {get;}=new();
    public TextBox ActualTime {get;}
    public CheckBox UnknownTime {get;}=new(){Content="记不清具体服用时间"};
    public Button SaveButton {get;}
    public TextBlock ErrorMessage {get;}=TaskUi.Error();
    private bool _saving;
    public MedicineConfirmation(DesktopController app,Occurrence item,bool preview,Action done)
    {
        Title="保存服用记录";Width=600;Height=680;MinWidth=520;MinHeight=480;Topmost=true;
        var local=app.Engine.Local(DateTimeOffset.UtcNow);ActualDate.SelectedDate=local.Date;
        AutomationProperties.SetAutomationId(ActualDate,"ActualMedicineDate");AutomationProperties.SetName(ActualDate,"实际服用日期");AutomationProperties.SetAutomationId(UnknownTime,"UnknownMedicineTime");
        ActualTime=Ui.Input(local.ToString("HH:mm"),"实际服用时间","ActualMedicineTime");
        var body=new StackPanel();body.Children.Add(TaskUi.Summary(item.PlanName,$"原计划 {app.Engine.Local(item.DueAt):MM-dd HH:mm}"));Ui.Gap(body,24);
        body.Children.Add(PageUi.Pair(TaskUi.Field("实际服用日期",ActualDate),TaskUi.Field("实际时刻 HH:mm",ActualTime)));body.Children.Add(UnknownTime);
        body.Children.Add(Ui.Text("实际服用时间与本次确认时间分别保存，之后的原计划时刻保持不变。",13,brush:"Muted",margin:new(0,12,0,0)));
        SaveButton=Ui.Button("保存服用记录",Save,true,"ConfirmMedicineRecord");
        var footer=new StackPanel();footer.Children.Add(ErrorMessage);footer.Children.Add(TaskUi.Actions(Ui.Button("返回核对",Close,id:"CancelMedicineRecord"),SaveButton));
        SetBody(TaskUi.Layout("记录本次已服用",preview?"当前为预览，不会写入真实执行记录。":"核对实际情况后保存本次记录。",body,footer));
        void AdjustUnknown(){ActualDate.IsEnabled=ActualTime.IsEnabled=UnknownTime.IsChecked!=true;FieldState.SetError(ActualDate,"");FieldState.SetError(ActualTime,"");ErrorMessage.Text="";}
        UnknownTime.Checked+=(_,_)=>AdjustUnknown();UnknownTime.Unchecked+=(_,_)=>AdjustUnknown();
        void Save()
        {
            if(_saving||SaveButton==null)return;ErrorMessage.Text="";FieldState.SetError(ActualDate,"");FieldState.SetError(ActualTime,"");
            DateTimeOffset? actual=null;
            if(UnknownTime.IsChecked!=true)
            {
                if(!ActualDate.TryCommit()){ErrorMessage.Text="未保存，请修正实际服用日期。";ActualDate.Focus();return;}
                try{actual=app.Engine.At(DateOnly.FromDateTime(ActualDate.SelectedDate!.Value),ReminderEngine.ParseTime(ActualTime.Text.Trim()));}
                catch(ArgumentException ex){FieldState.SetError(ActualTime,ex.Message);ErrorMessage.Text="未保存，请修正实际服用时间。";ActualTime.Focus();return;}
                if(actual>DateTimeOffset.UtcNow){FieldState.SetError(ActualTime,"实际时间不能晚于现在。");ErrorMessage.Text="未保存，请核对日期和时间。";ActualTime.Focus();return;}
            }
            _saving=true;SaveButton.IsEnabled=false;
            try{if(!preview)app.Engine.Resolve(item.Id,OccurrenceStatus.Completed,DateTimeOffset.UtcNow,actual);Close();done();}
            catch(Exception ex){ErrorMessage.Text="未保存，填写内容已保留："+TaskUi.Failure(ex);}
            finally{_saving=false;SaveButton.IsEnabled=true;}
        }
    }
}
