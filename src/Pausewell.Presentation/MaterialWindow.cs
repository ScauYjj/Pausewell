using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pausewell.Controls;
using Pausewell.MaterialLab;

namespace Pausewell.Presentation;

/// <summary>Shared task-window material. Theme changes update surfaces without rebuilding content.</summary>
public class MaterialWindow : Window
{
    public AmbientScene Ambient { get; } = new();
    public GlassSurface Surface { get; }
    public Grid MaterialRoot { get; } = new();
    public MaterialWindow()
    {
        FontSize=14;SetResourceReference(FontFamilyProperty,"UI.Font");SetResourceReference(ForegroundProperty,"Color.textPrimary");
        UseLayoutRounding=true;MinHeight=400;MaxHeight=Math.Max(400,SystemParameters.WorkArea.Height-24);WindowStartupLocation=WindowStartupLocation.CenterOwner;
        MaterialRoot.Children.Add(Ambient);
        Surface=new(Ambient,PageUi.Profiles["popup"]){Margin=new(16)};
        Surface.ContentHost.Padding=new(24);MaterialRoot.Children.Add(Surface);Content=MaterialRoot;
        Activated+=(_,_)=>RefreshMaterial();Deactivated+=(_,_)=>RefreshMaterial();Loaded+=(_,_)=>RefreshMaterial();
        DesignTheme.Changed+=RefreshMaterial;Closed+=(_,_)=>DesignTheme.Changed-=RefreshMaterial;
    }
    public void SetBody(UIElement body)=>Surface.ContentHost.Child=body;
    public void RefreshMaterial()
    {
        if(TryFindResource("Theme.ambient") is SolidColorBrush environment)Ambient.Environment=environment.Color;
        if(TryFindResource("Theme.toggle") is SolidColorBrush accent)Ambient.Accent=accent.Color;
        PageUi.ApplyMaterial(Surface,IsActive);
    }
    public void CenterReadingSurface(double maxWidth)
    {
        Surface.MaxWidth=maxWidth;Surface.VerticalAlignment=VerticalAlignment.Center;Surface.Margin=new(40);
        Surface.MaxHeight=Math.Max(320,SystemParameters.WorkArea.Height-80);
    }
}
