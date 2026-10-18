using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace Pausewell.Controls;

public static class DesignTheme
{
    public static string Current { get; private set; }="champagne";
    public static event Action? Changed;
    public static readonly string[] Ids=["champagne","violet","copper","rose","blue"];
    public static readonly string[] Names=["暖香槟","灰紫","陶土橙","雾玫瑰","雾蓝"];
    public static ResourceDictionary CreateResources()
    {
        var resources=new ResourceDictionary { Source=new Uri("/Pausewell.Controls;component/Styles/Controls.xaml",UriKind.Relative) };
        foreach(var name in new[]{"calendar","check","chevron-down","chevron-left","chevron-right","reminder","medicine","history","settings"})
        {
            using var stream=Application.GetResourceStream(new Uri($"pack://application:,,,/Pausewell.Controls;component/Assets/{name}.svg"))!.Stream;
            var doc=XDocument.Load(stream);var vb=doc.Root!.Attribute("viewBox")!.Value.Split(' ').Select(double.Parse).ToArray();
            var group=new DrawingGroup();foreach(var path in doc.Descendants().Where(e=>e.Name.LocalName=="path"))group.Children.Add(new GeometryDrawing(Brushes.Black,null,Geometry.Parse(path.Attribute("d")!.Value)));
            group.Transform=new ScaleTransform(24/vb[2],24/vb[3]);var icon=new DrawingImage(group);icon.Freeze();resources["Icon."+name]=icon;
            if(name=="check"){var light=group.Clone();foreach(var d in light.Children.OfType<GeometryDrawing>())d.Brush=Brushes.White;var white=new DrawingImage(light);white.Freeze();resources["Icon.check-light"]=white;}
        }
        Apply(resources,Current);return resources;
    }
    public static void Apply(ResourceDictionary target,string id,bool publish=true)
    {
        using var stream=Application.GetResourceStream(new Uri("pack://application:,,,/Pausewell.Controls;component/Assets/tokens.json"))!.Stream;
        using var json=JsonDocument.Parse(stream);var root=json.RootElement;var colors=root.GetProperty("color");var theme=root.GetProperty("themes").GetProperty(id);
        foreach(var color in colors.EnumerateObject())target["Color."+color.Name]=Brush(color.Value.GetString()!);
        foreach(var color in theme.EnumerateObject().Where(p=>p.Name!="label"))target["Theme."+color.Name]=Brush(color.Value.GetString()!);
        var ambient=((SolidColorBrush)target["Theme.ambient"]).Color;
        var accent=((SolidColorBrush)target["Theme.toggle"]).Color;
        // Theme the complete neutral palette as well as action colors. Keep the approved default.
        if(id!="champagne")
        {
            void Neutral(string key,Color color)=>target["Color."+key]=Frozen(color);
            Neutral("surfaceSolid",Mix(ambient,Colors.White,.78));
            Neutral("surfaceFallback",Mix(ambient,Colors.White,.58));
            Neutral("border",Mix(ambient,Colors.Gray,.22));
            Neutral("inputBoundary",Mix(ambient,Colors.Gray,.8));
            Neutral("divider",Mix(ambient,Colors.White,.25));
            Neutral("fieldHover",Mix(ambient,Colors.White,.35));
            Neutral("disabledSurface",Mix(ambient,Colors.White,.42));
            Neutral("textSelection",Mix(ambient,accent,.25));
            Neutral("focus",Mix(accent,Colors.Black,.3));
            Neutral("scrollThumb",Mix(accent,Colors.Gray,.45));
            Neutral("scrollRailHover",Mix(ambient,Colors.White,.2));
        }
        target["Color.textSelection"]=Frozen(Mix(accent,Colors.Black,.26));
        target["Color.onTextSelection"]=Frozen(Colors.White);
        target["Theme.glassTint"]=Frozen(id=="champagne"?Colors.White:Mix(ambient,Colors.White,.65));
        target["Theme.materialShade"]=Frozen(id=="champagne"?Color.FromRgb(61,49,34):Mix(accent,Colors.Black,.6));
        target["Theme.materialShadow"]=Frozen(id=="champagne"?Color.FromRgb(49,41,30):Mix(accent,Colors.Black,.65));
        target["Theme.navigationSelection"]=Frozen(Color.FromArgb(215,255,255,255));
        target["Theme.tableHeader"]=Frozen(id=="champagne"?Color.FromRgb(245,245,245):Mix(ambient,Colors.White,.48));
        target["UI.Font"]=new FontFamily(root.GetProperty("typography").GetProperty("uiFamily").GetString());
        if(publish){Current=id;Changed?.Invoke();}
    }
    public static Color Mix(Color from,Color to,double amount)=>Color.FromRgb((byte)Math.Round(from.R+(to.R-from.R)*amount),(byte)Math.Round(from.G+(to.G-from.G)*amount),(byte)Math.Round(from.B+(to.B-from.B)*amount));
    private static SolidColorBrush Frozen(Color color){var brush=new SolidColorBrush(color);brush.Freeze();return brush;}
    public static SolidColorBrush Brush(string text){var b=(SolidColorBrush)new BrushConverter().ConvertFromString(text)!;b.Freeze();return b;}
}
