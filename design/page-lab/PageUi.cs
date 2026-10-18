using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Xml.Linq;
using Pausewell.Controls;
using Pausewell.Core;
using Pausewell.MaterialLab;
namespace Pausewell.PageLab;
public static class PageUi
{
 public static readonly IReadOnlyDictionary<string,MaterialProfile> Profiles=MaterialProfile.Load("Pausewell.PageLab");
 public static TextBlock Text(string text,double size=14,bool bold=false,string color="Color.textPrimary",Thickness? margin=null){var t=new TextBlock{Text=text,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=margin??new()};t.SetResourceReference(TextBlock.ForegroundProperty,color);return t;}
 public static void Id(DependencyObject e,string id,string name){AutomationProperties.SetAutomationId(e,id);AutomationProperties.SetName(e,name);}
 public static Button Button(string text,string id,Action action,bool primary=false){var button=new Button{Content=text};Id(button,id,text);if(primary)button.SetResourceReference(FrameworkElement.StyleProperty,"PrimaryButton");button.Click+=(_,_)=>action();return button;}
 public static TextBox Input(string text,string id,string label){var t=new TextBox{Text=text};Id(t,id,label);return t;}
 public static Border Paper(UIElement child,Thickness? padding=null){var p=new Border{Child=child,Padding=padding??new(16),CornerRadius=new(12),BorderThickness=new(1)};p.SetResourceReference(Border.BackgroundProperty,"Color.surfaceSolid");p.SetResourceReference(Border.BorderBrushProperty,"Color.border");return p;}
 public static FrameworkElement Field(string label,FrameworkElement input){var g=new StackPanel{Margin=new(0,0,0,24)};if(label.Length>0)g.Children.Add(Text(label,bold:true,margin:new(0,0,0,8)));g.Children.Add(input);var error=Text("",12,color:"Color.danger",margin:new(0,6,0,0));var style=new Style(typeof(TextBlock),(Style)Application.Current.FindResource(typeof(TextBlock)));var empty=new Trigger{Property=TextBlock.TextProperty,Value=""};empty.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Collapsed));style.Triggers.Add(empty);error.Style=style;error.SetBinding(TextBlock.TextProperty,new Binding{Source=input,Path=new PropertyPath("(0)",FieldState.ErrorProperty)});g.Children.Add(error);return g;}
 public static Grid Pair(UIElement left,UIElement right,double gap=24){var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new(){Width=new GridLength(gap)});grid.ColumnDefinitions.Add(new());grid.Children.Add(left);Grid.SetColumn(right,2);grid.Children.Add(right);return grid;}
 public static Image Glyph(string name,double size=20){using var stream=Application.GetResourceStream(new Uri($"pack://application:,,,/Pausewell.PageLab;component/Assets/{name}.svg"))!.Stream;var document=XDocument.Load(stream);var drawing=new DrawingGroup();foreach(var path in document.Descendants().Where(e=>e.Name.LocalName=="path"))drawing.Children.Add(new GeometryDrawing(DesignTheme.Brush("#292824"),null,Geometry.Parse(path.Attribute("d")!.Value)));drawing.Freeze();return new Image{Source=new DrawingImage(drawing),Width=size,Height=size};}
 public static string KindIcon(HabitKind kind)=>kind switch{HabitKind.Water=>"water",HabitKind.Activity=>"activity",HabitKind.Toilet=>"toilet",HabitKind.Medication=>"medicine",_=>"other"};
 public static Border Badge(HabitKind kind,double size=48){var badge=new Border{Width=size,Height=size,CornerRadius=new(size/2),Child=Glyph(KindIcon(kind),size*.5)};badge.SetResourceReference(Border.BackgroundProperty,"Theme.soft");return badge;}
 public static Style ToggleStyle()=>(Style)System.Windows.Markup.XamlReader.Parse("""
 <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="CheckBox"><Setter Property="FocusVisualStyle" Value="{DynamicResource FocusRing}"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><StackPanel Orientation="Horizontal" Height="36"><Border x:Name="Track" Width="48" Height="28" Background="{DynamicResource Color.offTrack}" CornerRadius="14" VerticalAlignment="Center"><Ellipse x:Name="Thumb" Width="22" Height="22" Fill="White" Margin="3" HorizontalAlignment="Left"/></Border><ContentPresenter Margin="8,0,0,0" VerticalAlignment="Center"/></StackPanel><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Track" Property="Background" Value="{DynamicResource Theme.toggle}"/><Setter TargetName="Thumb" Property="HorizontalAlignment" Value="Right"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 """);
 public static void ApplyMaterial(GlassSurface surface,bool active){surface.Configure(SystemParameters.HighContrast||!SystemParameters.UIEffects||(RenderCapability.Tier>>16)==0,!active);}
}
