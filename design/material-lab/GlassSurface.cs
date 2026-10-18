using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace Pausewell.MaterialLab;
public sealed class GlassSurface:Grid
{
 private readonly Grid _face=new();
 private readonly Border _sample=new(){IsHitTestVisible=false};
 private readonly Border _tint=new(){IsHitTestVisible=false};
 private readonly Border _edge=new(){BorderThickness=new(1),IsHitTestVisible=false};
 private readonly Border _inner=new(){Margin=new(1),BorderThickness=new(1),IsHitTestVisible=false};
 private readonly List<Border> _shadows=[];
 private readonly VisualBrush _brush=new(){ViewboxUnits=BrushMappingMode.Absolute,Stretch=Stretch.Fill,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top,AutoLayoutContent=false};
 private Point _lastPoint=new(double.NaN,double.NaN);private Size _lastSize;
 public MaterialProfile Profile {get;}
 public FrameworkElement BackdropSource {get;}
 public Border ContentHost {get;}=new(){Padding=new(24)};
 public bool Reduced {get;private set;}
 public bool Inactive {get;private set;}
 public bool UsesBlur=>_sample.Visibility==Visibility.Visible&&_sample.Effect is BlurEffect;
 public Rect SampleBounds=>_brush.Viewbox;
 public int SampleUpdates {get;private set;}
 public Color AppliedTint=>(_tint.Background as SolidColorBrush)?.Color??Colors.Transparent;
 public GlassSurface(FrameworkElement backdrop,MaterialProfile profile)
 {
  BackdropSource=backdrop;Profile=profile;
  foreach(var shadow in profile.Shadows){var b=new Border{Background=Brushes.White,CornerRadius=new(profile.Radius),IsHitTestVisible=false,Effect=new DropShadowEffect{Color=(Color)ColorConverter.ConvertFromString("#31291E"),BlurRadius=shadow.Blur,ShadowDepth=shadow.Y,Direction=270,Opacity=shadow.Opacity,RenderingBias=RenderingBias.Performance}};_shadows.Add(b);Children.Add(b);}
  Children.Add(_face);_face.Children.Add(_sample);_face.Children.Add(_tint);_face.Children.Add(_edge);_face.Children.Add(_inner);Children.Add(ContentHost);
  _brush.Visual=backdrop;_sample.Background=_brush;_sample.Margin=new(-32);
  _edge.CornerRadius=new(profile.Radius);_inner.CornerRadius=new(Math.Max(0,profile.Radius-1));
  LayoutUpdated+=(_,_)=>UpdateSampling();SizeChanged+=(_,_)=>UpdateClip();Loaded+=(_,_)=>{UpdateClip();UpdateSampling();};
  Configure(false,false);
 }
 private void UpdateClip(){_face.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,ActualWidth),Math.Max(0,ActualHeight)),Profile.Radius,Profile.Radius);}
 public void UpdateSampling()
 {
  if(!IsLoaded||!BackdropSource.IsLoaded||ActualWidth<=0||ActualHeight<=0)return;
  var point=TranslatePoint(new Point(0,0),BackdropSource);var size=RenderSize;
  if(point==_lastPoint&&size==_lastSize)return;
  _lastPoint=point;_lastSize=size;_brush.Viewbox=new Rect(point.X-32,point.Y-32,size.Width+64,size.Height+64);SampleUpdates++;
 }
 public void Configure(bool reduced,bool inactive)
 {
  Reduced=reduced;Inactive=inactive;
  Color Palette(string key,Color fallback)=>TryFindResource(key) is SolidColorBrush brush?brush.Color:fallback;
  var tint=Palette("Theme.glassTint",Colors.White);var shade=Palette("Theme.materialShade",Color.FromRgb(61,49,34));
  _sample.Visibility=(!reduced&&Profile.Blur>0)?Visibility.Visible:Visibility.Collapsed;
  _sample.Effect=reduced?null:new BlurEffect{Radius=Profile.Blur,KernelType=KernelType.Gaussian,RenderingBias=RenderingBias.Performance};
  _tint.Background=SystemParameters.HighContrast?SystemColors.WindowBrush:reduced?new SolidColorBrush(Palette("Color.surfaceSolid",Color.FromRgb(252,251,248))):new SolidColorBrush(Color.FromArgb((byte)Math.Round(255*(inactive?Math.Max(.86,Profile.Tint):Profile.Tint)),tint.R,tint.G,tint.B));
  _edge.BorderBrush=new LinearGradientBrush(new GradientStopCollection{new(Colors.White,0),new(Color.FromArgb((byte)(255*(inactive?.5:Profile.Light)),255,255,255),.18),new(Color.FromArgb((byte)(255*Profile.Shade),shade.R,shade.G,shade.B),1)},new Point(.15,0),new Point(.85,1));
  _inner.BorderBrush=new LinearGradientBrush(Color.FromArgb(110,255,255,255),Colors.Transparent,new Point(0,0),new Point(0,1));
  if(SystemParameters.HighContrast){_edge.BorderBrush=SystemColors.WindowTextBrush;_inner.BorderBrush=Brushes.Transparent;}
  for(var i=0;i<_shadows.Count;i++){var effect=(DropShadowEffect)_shadows[i].Effect;effect.Color=Palette("Theme.materialShadow",Color.FromRgb(49,41,30));effect.Opacity=Profile.Shadows[i].Opacity*(inactive?.5:1);}
 }
}
