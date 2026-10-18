using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Calendar = System.Windows.Controls.Calendar;

namespace Pausewell.Controls;

public class IsoDatePicker : DatePicker
{
    private DatePickerTextBox? _input;
    private bool _normalizing,_restoreScheduled;
    private string? _invalidRaw;
    public Calendar? CalendarView {get;private set;}
    public DatePickerTextBox? Input=>_input;
    public static readonly DependencyProperty AllowClearProperty=DependencyProperty.Register(nameof(AllowClear),typeof(bool),typeof(IsoDatePicker),new PropertyMetadata(false));
    public bool AllowClear {get=>(bool)GetValue(AllowClearProperty);set=>SetValue(AllowClearProperty,value);}
    public IsoDatePicker()
    {
        FirstDayOfWeek=DayOfWeek.Monday;
        SelectedDateChanged+=(_,_)=>{_invalidRaw=null;FieldState.SetError(this,"");Dispatcher.BeginInvoke(Normalize,DispatcherPriority.Background);};
        PreviewLostKeyboardFocus+=(_,_)=>TryCommit();
        CalendarClosed+=(_,_)=>{if(_invalidRaw==null)Normalize();};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter&&!IsDropDownOpen){TryCommit();e.Handled=true;}};
    }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();_input=GetTemplateChild("PART_TextBox") as DatePickerTextBox;
        if(_input!=null){AutomationProperties.SetName(_input,AutomationProperties.GetName(this)+"日期输入");AutomationProperties.SetAutomationId(_input,AutomationProperties.GetAutomationId(this)+"_Input");}
        if(GetTemplateChild("PART_Popup") is Popup p&&p.Child is Calendar c){CalendarView=c;CalendarActions.SetOwner(c,this);CalendarActions.SetAllowClear(c,AllowClear);}
        Normalize();
    }
    public void SetRawInput(string raw){_invalidRaw=null;FieldState.SetError(this,"");if(_input!=null)_input.Text=raw;else Text=raw;}
    public bool TryCommit()
    {
        if(_normalizing)return true;
        // WPF restores the previous date on LostFocus. A save in that same event
        // must not accept the fallback before the rejected text is restored.
        if(_restoreScheduled&&_invalidRaw!=null)return false;
        var raw=_input?.Text??Text??"";
        if(raw.Length==0&&AllowClear){_invalidRaw=null;SelectedDate=null;FieldState.SetError(this,"");return true;}
        if(!DateTime.TryParseExact(raw.Trim(),["yyyy-MM-dd","yyyy/MM/dd","yyyy-M-d","yyyy/M/d"],CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))return Reject(raw,"请输入有效日期，例如 2026-10-16。");
        if((DisplayDateStart is {} start&&date<start.Date)||(DisplayDateEnd is {} end&&date>end.Date)||BlackoutDates.Contains(date))return Reject(raw,"此日期不在可选范围内。");
        _normalizing=true;try{_invalidRaw=null;SetCurrentValue(SelectedDateProperty,(DateTime?)date.Date);FieldState.SetError(this,"");SetCurrentValue(TextProperty,date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));if(_input!=null)_input.Text=date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);}finally{_normalizing=false;}return true;
    }
    private bool Reject(string raw,string error){_invalidRaw=raw;FieldState.SetError(this,error);RestoreInvalid();return false;}
    protected override void OnDateValidationError(DatePickerDateValidationErrorEventArgs e){e.ThrowException=false;base.OnDateValidationError(e);_invalidRaw=e.Text;FieldState.SetError(this,"请输入有效且可选的日期。");RestoreInvalid();}
    private void RestoreInvalid()
    {
        if(_restoreScheduled)return;_restoreScheduled=true;
        Dispatcher.BeginInvoke(()=>{try{if(_invalidRaw is {} raw&&_input!=null){_normalizing=true;_input.Text=raw;}}finally{_normalizing=false;_restoreScheduled=false;}},DispatcherPriority.Background);
    }
    private void Normalize(){if(_normalizing||_invalidRaw!=null||_input==null)return;_normalizing=true;try{var value=SelectedDate?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)??"";SetCurrentValue(TextProperty,value);_input.Text=value;}finally{_normalizing=false;}}
}
