using Pausewell.Presentation;

namespace Pausewell.Desktop;

internal static class TaskUi
{
    public static string Failure(Exception error)=>error is IOException
        ?error.Message.Contains("locked",StringComparison.OrdinalIgnoreCase)?"数据文件暂时被占用，请稍后重试。":"无法写入本地数据，请检查数据目录后重试。"
        :error.Message;
    public static DockPanel Layout(string title,string description,UIElement body,UIElement footer)
    {
        var layout=new DockPanel{LastChildFill=true};
        var header=new StackPanel{Margin=new(0,0,0,24)};
        header.Children.Add(Ui.Text(title,24,true));header.Children.Add(Ui.Text(description,14,brush:"Muted",margin:new(0,8,0,0)));
        DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);
        var fixedFooter=new Border{Child=footer,Margin=new(0,16,0,0)};DockPanel.SetDock(fixedFooter,Dock.Bottom);layout.Children.Add(fixedFooter);
        var scroll=Ui.Scroll(body);scroll.Margin=new(-4);scroll.Padding=new(4,4,8,4);layout.Children.Add(scroll);return layout;
    }
    public static WrapPanel Actions(params UIElement[] controls)
    {
        var row=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};foreach(var control in controls){if(control is FrameworkElement e)e.Margin=new(8,4,0,0);row.Children.Add(control);}return row;
    }
    public static TextBlock Error()
    {
        var label=Ui.Text("",13,brush:"Color.danger",margin:new(0,0,0,8));
        var style=new Style(typeof(TextBlock),(Style)Application.Current.FindResource(typeof(TextBlock)));
        var empty=new Trigger{Property=TextBlock.TextProperty,Value=""};empty.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Collapsed));style.Triggers.Add(empty);label.Style=style;return label;
    }
    public static FrameworkElement Field(string label,FrameworkElement input)=>PageUi.Field(label,input);
    public static Border Summary(string title,string detail)
    {
        var panel=new StackPanel();panel.Children.Add(Ui.Text(title,18,true));panel.Children.Add(Ui.Text(detail,13,brush:"Muted",margin:new(0,8,0,0)));return Ui.Panel(panel,new(16));
    }
}
