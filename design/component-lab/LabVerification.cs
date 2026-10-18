using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pausewell.Controls;

namespace Pausewell.ComponentLab;
public static class LabVerification
{
    public static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T typed)yield return typed;foreach(var nested in Descendants<T>(child))yield return nested;}}
    private static T Part<T>(Control control,string name) where T:class=>control.Template.FindName(name,control) as T??throw new InvalidOperationException("缺少模板部件："+name);
    public static void Capture(FrameworkElement visual,string path)
    {
        NativeCapture.Save(visual,path);
    }
    private static object Provider(UIElement control,PatternInterface pattern)=>UIElementAutomationPeer.CreatePeerForElement(control)?.GetPattern(pattern)??throw new InvalidOperationException("控件缺少自动化模式："+pattern);
    private static void Invoke(Button control)=>((IInvokeProvider)Provider(control,PatternInterface.Invoke)).Invoke();
    public static async Task Run(LabWindow w,string directory)
    {
        Directory.CreateDirectory(directory);var checks=new List<object>();var started=DateTimeOffset.Now;var success=true;var originalCursor=NativeInput.CursorPosition;
        void Verify(string name,bool passed,object? detail=null){checks.Add(new{name,passed,detail});if(!passed)throw new InvalidOperationException(name);}
        void Shot(string name,FrameworkElement? visual=null)=>Capture(visual??(FrameworkElement)w.Content,Path.Combine(directory,name+".png"));
        try
        {
            await Task.Delay(300);Verify("独立样板不引用业务核心与数据库",!typeof(LabWindow).Assembly.GetReferencedAssemblies().Any(a=>a.Name is "Pausewell.Core" or "Pausewell.Storage"));
            Verify("输入和下拉均达到 40 DIP 且高度一致",w.PlanName.ActualHeight>=40&&Math.Abs(w.PlanName.ActualHeight-w.Kind.ActualHeight)<1.5,new{input=w.PlanName.ActualHeight,select=w.Kind.ActualHeight});
            Verify("输入使用圆角模板",Part<Border>(w.PlanName,"Shell").CornerRadius.TopLeft==10);Verify("主按钮白字与内容字体不被隐式样式覆盖",Descendants<TextBlock>(w.Save).All(t=>t.Foreground is SolidColorBrush b&&b.Color==Colors.White));Shot("01-form");
            var before=w.SaveCount;w.PlanName.Text="";w.Interval.Text="0";w.TimeInput.Text="25:70";Verify("无效提交阻止保存且保留输入",!w.TrySave()&&w.SaveCount==before&&w.TimeInput.Text=="25:70"&&FieldState.GetHasError(w.PlanName)&&FieldState.GetHasError(w.Interval));await Task.Delay(80);Shot("02-errors");
            w.PlanName.Text="喝水";w.Interval.Text="1441";w.TimeInput.Text="08:30";Verify("分钟上边界被校验",!w.TrySave()&&FieldState.GetHasError(w.Interval));w.Interval.Text="1.5";Verify("小数分钟被校验",!w.TrySave());w.Interval.Text="1440";w.Note.Text=new string('字',1001);Verify("备注超长经过提交路径阻止",!w.TrySave()&&FieldState.GetHasError(w.Note));w.Note.Text="";Verify("合法提交只更新内存",w.TrySave()&&w.SaveCount==before+1&&w.LastSavedName=="喝水");w.Interval.Text="30";
            ((IToggleProvider)Provider(w.EnablePlan,PatternInterface.Toggle)).Toggle();Verify("复选框自动化切换有效",w.EnablePlan.IsChecked==false);((IToggleProvider)Provider(w.EnablePlan,PatternInterface.Toggle)).Toggle();
            var expand=(IExpandCollapseProvider)Provider(w.More,PatternInterface.ExpandCollapse);expand.Expand();await Task.Delay(80);Verify("展开器实际展开且同步文字",w.More.IsExpanded&&w.More.Header as string=="收起设置");Shot("03-expanded-form");expand.Collapse();Verify("展开器实际折叠",!w.More.IsExpanded);
            w.ReminderStyle.IsDropDownOpen=true;await Task.Delay(160);var selectPopup=Part<Popup>(w.ReminderStyle,"PART_Popup");Verify("下拉真实 Popup 显示",selectPopup.IsOpen&&selectPopup.Child.IsVisible);Shot("04-dropdown",(FrameworkElement)selectPopup.Child);
            var selected=(ComboBoxItem)w.ReminderStyle.ItemContainerGenerator.ContainerFromIndex(1);Verify("下拉选中项包含可见标记",Descendants<Image>(selected).Any(i=>i.Visibility==Visibility.Visible));
            var saved=w.SaveCount;Verify("样板窗口可接收测试输入",NativeInput.Activate(w));NativeInput.Key(0x1B);await Task.Delay(120);Verify("Esc 仅关闭下拉，不退出窗口",!w.ReminderStyle.IsDropDownOpen&&w.IsVisible);
            w.ReminderStyle.Focus();w.ReminderStyle.IsDropDownOpen=true;await Task.Delay(80);NativeInput.Key(0x28);await Task.Delay(80);NativeInput.Key(0x0D);await Task.Delay(160);Verify("Enter 完成下拉选择，不提交外层",!w.ReminderStyle.IsDropDownOpen&&w.SaveCount==saved,new{open=w.ReminderStyle.IsDropDownOpen,saved=w.SaveCount,expected=saved,focused=Keyboard.FocusedElement?.GetType().Name});
            w.Pages.SelectedIndex=1;await Task.Delay(160);Shot("05-selection-page");
            w.LongOptions.IsDropDownOpen=true;await Task.Delay(120);var longPopup=Part<Popup>(w.LongOptions,"PART_Popup");var options=Descendants<ScrollViewer>(longPopup.Child).First();Verify("长选项弹层使用内部滚动",options.ScrollableHeight>0);var option=(ComboBoxItem)w.LongOptions.ItemContainerGenerator.ContainerFromIndex(3);Shot("06-long-dropdown",(FrameworkElement)longPopup.Child);w.LongOptions.Focus();NativeInput.Key(0x24);await Task.Delay(60);NativeInput.Key(0x28);await Task.Delay(60);NativeInput.Key(0x28);await Task.Delay(60);NativeInput.Key(0x28);await Task.Delay(60);NativeInput.Key(0x0D);await Task.Delay(100);Verify("选项可通过原生键盘切换",w.LongOptions.SelectedIndex==3);w.LongOptions.IsDropDownOpen=false;
            w.EmptyOptions.IsDropDownOpen=true;await Task.Delay(100);var emptyPopup=Part<Popup>(w.EmptyOptions,"PART_Popup");Verify("空下拉显示解释行",Descendants<TextBlock>(emptyPopup.Child).Any(t=>t.IsVisible&&t.Text=="暂无可选项"));Shot("07-empty-dropdown",(FrameworkElement)emptyPopup.Child);w.EmptyOptions.IsDropDownOpen=false;
            w.StartDate.SetRawInput("2028/02/29");Verify("合法闰日可输入",w.StartDate.TryCommit()&&w.StartDate.SelectedDate==new DateTime(2028,2,29));await Task.Delay(80);Verify("日期归一为 yyyy-MM-dd",w.StartDate.Input!.Text=="2028-02-29");
            w.StartDate.Input!.Focus();await Task.Delay(60);w.StartDate.SetRawInput("2026-02-29");Verify("非法日期不替换为其他日期",!w.StartDate.TryCommit()&&w.StartDate.SelectedDate==new DateTime(2028,2,29));w.Save.Focus();await Task.Delay(140);Verify("失焦仍保留非法日期与错误",w.StartDate.Input!.Text=="2026-02-29"&&FieldState.GetHasError(w.StartDate));Shot("08-invalid-date");
            w.StartDate.SetRawInput("2026-10-16");Verify("纠正日期后清除错误",w.StartDate.TryCommit()&&!FieldState.GetHasError(w.StartDate));w.StartDate.IsDropDownOpen=true;await Task.Delay(140);var datePopup=Part<Popup>(w.StartDate,"PART_Popup");var calendar=w.StartDate.CalendarView!;Verify("日历真实 Popup 与内部模板存在",datePopup.IsOpen&&calendar.IsVisible&&calendar.FirstDayOfWeek==DayOfWeek.Monday&&Part<CalendarItem>(calendar,"PART_CalendarItem")!=null);var calendarShell=Part<Border>(calendar,"CalendarShell");Verify("日历阅读面符合 304 × 384 基准",Math.Abs(calendarShell.ActualWidth-304)<1&&Math.Abs(calendarShell.ActualHeight-384)<1,new{width=calendarShell.ActualWidth,height=calendarShell.ActualHeight});Shot("09-calendar-month",calendar);
            var calendarItem=Part<CalendarItem>(calendar,"PART_CalendarItem");Invoke(Part<Button>(calendarItem,"PART_HeaderButton"));await Task.Delay(120);Verify("月份视图真实切换且显示十二个月",calendar.DisplayMode==CalendarMode.Year&&Descendants<CalendarButton>(calendar).Count(b=>b.IsVisible)==12);Shot("10-calendar-year",calendar);Invoke(Part<Button>(calendarItem,"PART_HeaderButton"));await Task.Delay(100);Verify("年份视图真实切换且保留自定义模板",calendar.DisplayMode==CalendarMode.Decade&&Part<Grid>(calendarItem,"PART_YearView").Visibility==Visibility.Visible);Shot("11-calendar-decade",calendar);
            calendar.DisplayMode=CalendarMode.Month;calendar.DisplayDate=new DateTime(2026,10,1);await Task.Delay(80);var dateButton=Descendants<CalendarDayButton>(calendar).Single(b=>b.DataContext is DateTime d&&d.Date==new DateTime(2026,10,20));dateButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(80);Verify("日期按钮可实际选择",w.StartDate.SelectedDate==new DateTime(2026,10,20));w.StartDate.IsDropDownOpen=false;
            ((IToggleProvider)Provider(w.UseEnd,PatternInterface.Toggle)).Toggle();Verify("结束日期受复选框启用",w.EndDate.IsEnabled);w.EndDate.SetRawInput("2026-10-16");Verify("日期范围错误保留两个值",!w.ValidateDates()&&w.StartDate.SelectedDate==new DateTime(2026,10,20)&&w.EndDate.SelectedDate==new DateTime(2026,10,16));w.EndDate.SetRawInput("2026-10-21");Verify("修正范围后有效",w.ValidateDates());w.EndDate.IsDropDownOpen=true;await Task.Delay(100);var optionalCalendar=w.EndDate.CalendarView!;var clear=Descendants<Button>(optionalCalendar).Single(b=>AutomationProperties.GetAutomationId(b)=="CalendarClear");Invoke(clear);await Task.Delay(80);Verify("可选日期清除后关闭弹层",w.EndDate.SelectedDate==null&&!w.EndDate.IsDropDownOpen);((IToggleProvider)Provider(w.UseEnd,PatternInterface.Toggle)).Toggle();
            Verify("日历不可选日期保留原生状态",Descendants<CalendarDayButton>(w.StandaloneCalendar).Any(b=>b.IsBlackedOut));
            foreach(var theme in DesignTheme.Ids){DesignTheme.Apply(w.Palette,theme);await Task.Delay(60);Verify("主题资源应用 "+theme,DesignTheme.Current==theme&&w.PlanName.Text=="喝水");Shot("12-theme-"+theme);}DesignTheme.Apply(w.Palette,"champagne");
            w.Pages.SelectedIndex=2;await Task.Delay(150);Shot("13-states");var radio=w.Counts[0];((ISelectionItemProvider)Provider(radio,PatternInterface.SelectionItem)).Select();Verify("分段选项互斥",w.Counts.Count(r=>r.IsChecked==true)==1&&radio.IsChecked==true);
            Verify("无溢出区域不显示滚动条",w.ShortScroll.ComputedVerticalScrollBarVisibility==Visibility.Collapsed&&w.ShortScroll.ScrollableHeight==0);Verify("溢出区域显示细滚动条",w.LongScroll.ComputedVerticalScrollBarVisibility==Visibility.Visible&&Math.Abs(Part<ScrollBar>(w.LongScroll,"PART_VerticalScrollBar").ActualWidth-14)<1,new{visibility=w.LongScroll.ComputedVerticalScrollBarVisibility.ToString(),extent=w.LongScroll.ExtentHeight,viewport=w.LongScroll.ViewportHeight,scroll=w.LongScroll.ScrollableHeight,width=Part<ScrollBar>(w.LongScroll,"PART_VerticalScrollBar").ActualWidth});
            var scrollProvider=(IScrollProvider)Provider(w.LongScroll,PatternInterface.Scroll);scrollProvider.Scroll(ScrollAmount.NoAmount,ScrollAmount.LargeIncrement);await Task.Delay(80);Verify("原生滚动自动化模式有效",w.LongScroll.VerticalOffset>0);
            w.LongScroll.ScrollToHome();w.LongScroll.Focus();NativeInput.Key(0x22);await Task.Delay(120);Verify("PageDown 键盘滚动有效",w.LongScroll.VerticalOffset>0);
            w.LongScroll.ScrollToHome();await Task.Delay(60);NativeInput.Move(w.LongScroll.PointToScreen(new Point(60,100)));await Task.Delay(80);NativeInput.Wheel(-120);await Task.Delay(160);Verify("滚轮滚动有效",w.LongScroll.VerticalOffset>0);
            w.LongScroll.ScrollToHome();await Task.Delay(80);var bar=Part<ScrollBar>(w.LongScroll,"PART_VerticalScrollBar");var track=Part<Track>(bar,"PART_Track");var thumb=track.Thumb;var point=thumb.PointToScreen(new Point(thumb.ActualWidth/2,thumb.ActualHeight/2));NativeInput.Move(point);await Task.Delay(80);var widthBefore=w.LongScroll.ActualWidth;NativeInput.Down();await Task.Delay(80);NativeInput.Move(new Point(point.X,point.Y+80));await Task.Delay(100);NativeInput.Up();await Task.Delay(80);Verify("滑块拖动有效且不挤动内容",w.LongScroll.VerticalOffset>0&&Math.Abs(w.LongScroll.ActualWidth-widthBefore)<0.1);Shot("14-scroll-dragged");
            var readOnly=Descendants<TextBox>(w).Single(t=>t.IsReadOnly);readOnly.Focus();var readOnlyText=readOnly.Text;NativeInput.Text("X");await Task.Delay(100);Verify("只读字段阻止编辑",readOnly.Text==readOnlyText);
            w.Pages.SelectedIndex=0;await Task.Delay(100);Keyboard.Focus((TabItem)w.Pages.Items[0]);await Task.Delay(80);NativeInput.Key(0x27);await Task.Delay(120);Verify("导航方向键切换页面",w.Pages.SelectedIndex==1);Verify("键盘导航显示独立焦点覆盖层",AdornerLayer.GetAdornerLayer((TabItem)w.Pages.Items[1])?.GetAdorners((TabItem)w.Pages.Items[1])?.Length>0);Shot("15-keyboard-focus");await NativeInput.Click((TabItem)w.Pages.Items[0]);Verify("鼠标切换导航保留选中",w.Pages.SelectedIndex==0);Shot("16-pointer-selected");
            w.Width=900;w.Height=650;await Task.Delay(120);Verify("紧凑窗口保存与导航仍可见",w.Save.IsVisible&&w.Save.ActualHeight>=40&&w.Pages.IsVisible);Shot("17-compact");w.Width=1280;w.Height=960;
        }
        catch(Exception ex){success=false;checks.Add(new{name="异常",passed=false,error=ex.ToString()});}
        finally
        {
            NativeInput.Up();NativeInput.Move(originalCursor);
            File.WriteAllText(Path.Combine(directory,"native-test-results.json"),JsonSerializer.Serialize(new{success,startedAt=started,finishedAt=DateTimeOffset.Now,dpi=NativeInput.GetDpiForWindow(new WindowInteropHelper(w).Handle),checks,note="独立原生样板。实际控件、Popup、原生自动化模式及本机输入事件；不代表全部缩放、屏幕阅读器、输入法和最终玻璃材质已验收。"},new JsonSerializerOptions{WriteIndented=true}));Application.Current.Shutdown(success?0:1);
        }
    }
}
