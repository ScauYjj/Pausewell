using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pausewell.Controls;

public static class FieldState
{
    public static readonly DependencyProperty ErrorProperty=DependencyProperty.RegisterAttached("Error",typeof(string),typeof(FieldState),new FrameworkPropertyMetadata("",Changed));
    public static readonly DependencyProperty HasErrorProperty=DependencyProperty.RegisterAttached("HasError",typeof(bool),typeof(FieldState),new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty PlaceholderProperty=DependencyProperty.RegisterAttached("Placeholder",typeof(string),typeof(FieldState),new FrameworkPropertyMetadata(""));
    public static string GetError(DependencyObject o)=>(string)o.GetValue(ErrorProperty);
    public static void SetError(DependencyObject o,string v)=>o.SetValue(ErrorProperty,v);
    public static bool GetHasError(DependencyObject o)=>(bool)o.GetValue(HasErrorProperty);
    public static void SetHasError(DependencyObject o,bool v)=>o.SetValue(HasErrorProperty,v);
    public static string GetPlaceholder(DependencyObject o)=>(string)o.GetValue(PlaceholderProperty);
    public static void SetPlaceholder(DependencyObject o,string v)=>o.SetValue(PlaceholderProperty,v);
    private static void Changed(DependencyObject o,DependencyPropertyChangedEventArgs e){SetHasError(o,!string.IsNullOrEmpty((string)e.NewValue));AutomationProperties.SetHelpText(o,(string)e.NewValue);}
}

public static class CalendarActions
{
    public static RoutedUICommand Today {get;}=new("今天","Today",typeof(CalendarActions));
    public static RoutedUICommand Clear {get;}=new("清除日期","Clear",typeof(CalendarActions));
    public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(CalendarActions),new PropertyMetadata(false,Attach));
    public static readonly DependencyProperty OwnerProperty=DependencyProperty.RegisterAttached("Owner",typeof(IsoDatePicker),typeof(CalendarActions));
    public static readonly DependencyProperty AllowClearProperty=DependencyProperty.RegisterAttached("AllowClear",typeof(bool),typeof(CalendarActions),new PropertyMetadata(false));
    public static bool GetEnabled(DependencyObject o)=>(bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o,bool v)=>o.SetValue(EnabledProperty,v);
    public static IsoDatePicker? GetOwner(DependencyObject o)=>(IsoDatePicker?)o.GetValue(OwnerProperty);
    public static void SetOwner(DependencyObject o,IsoDatePicker v)=>o.SetValue(OwnerProperty,v);
    public static bool GetAllowClear(DependencyObject o)=>(bool)o.GetValue(AllowClearProperty);
    public static void SetAllowClear(DependencyObject o,bool v)=>o.SetValue(AllowClearProperty,v);
    private static void Attach(DependencyObject o,DependencyPropertyChangedEventArgs e)
    {
        if(o is not Calendar c||!(bool)e.NewValue)return;
        bool Allowed()=>c.IsEnabled&&(!c.DisplayDateStart.HasValue||DateTime.Today>=c.DisplayDateStart.Value.Date)&&(!c.DisplayDateEnd.HasValue||DateTime.Today<=c.DisplayDateEnd.Value.Date)&&!c.BlackoutDates.Contains(DateTime.Today);
        c.CommandBindings.Add(new CommandBinding(Today,(_,a)=>{c.DisplayMode=CalendarMode.Month;c.DisplayDate=DateTime.Today;c.SelectedDate=DateTime.Today;if(GetOwner(c) is {} owner)owner.IsDropDownOpen=false;a.Handled=true;},(_,a)=>{a.CanExecute=Allowed();a.Handled=true;}));
        c.CommandBindings.Add(new CommandBinding(Clear,(_,a)=>{c.SelectedDate=null;if(GetOwner(c) is {} owner){owner.SelectedDate=null;owner.Text="";owner.IsDropDownOpen=false;}a.Handled=true;},(_,a)=>{a.CanExecute=GetAllowClear(c)&&c.SelectedDate!=null;a.Handled=true;}));
    }
}
