using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace Pausewell.Desktop;

public sealed record ThemePalette(string Id, string Name, string Primary, string Toggle, string Soft);
public static class Theme
{
    public static readonly ThemePalette[] All = [new("champagne","暖香槟","#343434","#9B8D70","#F3EEE5"),new("violet","灰紫","#795280","#795280","#F1EAF3"),new("copper","陶土橙","#AA5B32","#AA5B32","#F7ECE4"),new("rose","雾玫瑰","#A0526B","#A0526B","#F7EAF0"),new("blue","雾蓝","#4C6D91","#4C6D91","#EBF2F8")];
    public static ThemePalette Get(string id) => All.FirstOrDefault(p => p.Id==id) ?? All[0];
    public static SolidColorBrush Brush(string c) => (SolidColorBrush)new BrushConverter().ConvertFromString(c)!;
    public static string Current { get; private set; } = "champagne";
    public static void Apply(string id) { Current=Get(id).Id; Write(Application.Current.Resources,id);Pausewell.Controls.DesignTheme.Apply(Application.Current.Resources,Current); }
    public static BitmapImage Picture(string id) => new(new Uri($"pack://application:,,,/Assets/Themes/{Get(id).Id}.png"));
    public static void Write(ResourceDictionary resources, string id)
    {
        var p=Get(id);Pausewell.Controls.DesignTheme.Apply(resources,p.Id,false);
        resources["Ink"]=resources["Color.textPrimary"];resources["Muted"]=resources["Color.textSecondary"];resources["Line"]=resources["Color.border"];resources["PrimaryBrush"]=resources["Theme.primary"];resources["ToggleBrush"]=resources["Theme.toggle"];resources["SoftBrush"]=resources["Theme.soft"];resources["Surface"]=resources["Color.surfaceSolid"];resources["WindowBase"]=resources["Theme.ambient"];
        // Reuse the approved raster's empty material strip, never its controls or text.
        resources["GlassBrush"]=new ImageBrush(Picture(id)) { Viewbox=new Rect(0.038,0.122,0.132,0.084),ViewboxUnits=BrushMappingMode.RelativeToBoundingBox,Stretch=Stretch.Fill,Opacity=.83 };
    }
    public static void ClearPreview(ResourceDictionary resources)
    { foreach(var key in resources.Keys.Cast<object>().Where(k=>k is string s&&(s.StartsWith("Color.")||s.StartsWith("Theme.")||s=="UI.Font"||new[]{"Ink","Muted","Line","PrimaryBrush","ToggleBrush","SoftBrush","Surface","WindowBase","GlassBrush"}.Contains(s))).ToArray())resources.Remove(key); }
}

public static class Ui
{
    public static SolidColorBrush Brush(string value)=>Theme.Brush(value);
    public static T Resource<T>(T element, DependencyProperty property,string key) where T:FrameworkElement { element.SetResourceReference(property,key);return element; }
    public static TextBlock Text(string text,double size=14,bool bold=false,string brush="Ink",Thickness? margin=null) => Resource(new TextBlock { Text=text,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=margin??new() },TextBlock.ForegroundProperty,brush);
    public static Button Button(string title,Action action,bool primary=false,string? id=null)
    {
        var b=new Button { Content=title,Style=(Style)Application.Current.FindResource(primary?"Primary":"StandardButton"),Margin=new Thickness(0,0,8,0) };
        if(id!=null)System.Windows.Automation.AutomationProperties.SetAutomationId(b,id);
        b.Click+=(_,_)=>action();return b;
    }
    public static Button RowAction(string title, Action action, string id, bool destructive = false)
    {
        var button = Button(title, action, id: id);
        button.SetResourceReference(FrameworkElement.StyleProperty, destructive ? "RowDeleteButton" : "RowActionButton");
        button.Margin = new(0);
        System.Windows.Automation.AutomationProperties.SetName(button, title);
        var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var glyph = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(destructive
                ? "M4,6 L20,6 M9,6 L9,3 L15,3 L15,6 M6,6 L7,21 L17,21 L18,6 M10,10 L10,17 M14,10 L14,17"
                : "M14,5 L19,10 M4,20 L5,15 L16,4 C17,3 18,3 19,4 L20,5 C21,6 21,7 20,8 L9,19 Z"),
            StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round, Stretch = Stretch.Uniform, Width = 14, Height = 14, Margin = new(0, 0, 6, 0)
        };
        glyph.SetBinding(System.Windows.Shapes.Path.StrokeProperty, new System.Windows.Data.Binding(nameof(Control.Foreground)) { Source = button });
        var label = new TextBlock { Text = title, FontSize = 13, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center };
        label.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(nameof(Control.Foreground)) { Source = button });
        content.Children.Add(glyph); content.Children.Add(label); button.Content = content;
        return button;
    }
    public static FrameworkElement Icon(string name,double size=22,string brush="Ink")
    {
        var stream=Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/Icons/{name}.svg"))?.Stream ?? throw new InvalidDataException("缺少图标："+name);
        using(stream)
        {
            var doc=XDocument.Load(stream);var grid=new Grid { Width=24,Height=24 };
            foreach(var path in doc.Descendants().Where(e=>e.Name.LocalName=="path"))
            {
                var p=new System.Windows.Shapes.Path { Data=Geometry.Parse(path.Attribute("d")!.Value) };p.SetResourceReference(System.Windows.Shapes.Path.FillProperty,brush);grid.Children.Add(p);
            }
            return new Viewbox { Child=grid,Width=size,Height=size,Stretch=Stretch.Uniform };
        }
    }
    public static FrameworkElement KindIcon(HabitKind kind,double size=24)=>Icon(kind switch{HabitKind.Activity=>"activity",HabitKind.Water=>"water",HabitKind.Toilet=>"toilet",HabitKind.Medication=>"medicine",_=>"other"},size);
    public static Border IconCircle(HabitKind kind,double size=48)
    { var b=new Border { Width=size,Height=size,CornerRadius=new(size/2),Child=KindIcon(kind,size*.48) };b.SetResourceReference(Border.BackgroundProperty,"SoftBrush");return b; }
    public static TextBox Input(string text,string name,string id,Action<string>? changed=null)
    { var t=new TextBox { Text=text };System.Windows.Automation.AutomationProperties.SetName(t,name);System.Windows.Automation.AutomationProperties.SetAutomationId(t,id);if(changed!=null)t.TextChanged+=(_,_)=>changed(t.Text);return t; }
    public static void Field(Panel panel,string name,FrameworkElement input)
    { panel.Children.Add(Text(name,14,true,margin:new(0,16,0,8)));System.Windows.Automation.AutomationProperties.SetName(input,name);panel.Children.Add(input); }
    public static Border Panel(UIElement child,Thickness? padding=null)
    { var p=new Border { Child=child,CornerRadius=new(16),Padding=padding??new(24),BorderThickness=new(1) };p.SetResourceReference(Border.BackgroundProperty,"Surface");p.SetResourceReference(Border.BorderBrushProperty,"Line");return p; }
    public static Border Notice(string text,string brush="SoftBrush")
    { var b=new Border { Child=Text(text,13),Padding=new(14),CornerRadius=new(9),Margin=new(0,0,0,16) };b.SetResourceReference(Border.BackgroundProperty,brush);return b; }
    public static StackPanel Actions(params UIElement[] children)
    { var p=new StackPanel { Orientation=Orientation.Horizontal };foreach(var c in children)p.Children.Add(c);return p; }
    public static ScrollViewer Scroll(UIElement content)=>new() { Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,8,8) };
    public static void Gap(Panel p,double height=16)=>p.Children.Add(new Border { Height=height });
    public static string Rule(ReminderPlan p,DateTimeOffset? localOnce=null)=>p.Schedule==ScheduleKind.Once?$"仅一次 · {(localOnce??p.OnceAt):yyyy-MM-dd HH:mm}":p.Schedule==ScheduleKind.Interval?(p.IsHabit?$"完成后 {p.IntervalMinutes} 分钟":$"每 {p.IntervalMinutes} 分钟"):string.Join(" / ",p.Times);
    public static string Status(Occurrence o)=>o.Status switch { OccurrenceStatus.Completed when o.Kind==HabitKind.Medication=>"已服用",OccurrenceStatus.Skipped when o.Kind==HabitKind.Medication=>"未服用",_=>Labels.Status(o.Status) };
}
