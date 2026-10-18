using System.Windows;
using System.Windows.Media;
using Pausewell.Controls;
namespace Pausewell.MaterialLab;
public sealed class AmbientScene:FrameworkElement
{
 public int Mode {get;set;}
 public int RenderCount {get;private set;}
 public double LightPosition {get;set;}=.36;
 public static readonly DependencyProperty EnvironmentProperty=DependencyProperty.Register(nameof(Environment),typeof(Color),typeof(AmbientScene),new FrameworkPropertyMetadata((Color)ColorConverter.ConvertFromString("#EEE7D9"),FrameworkPropertyMetadataOptions.AffectsRender));
 public static readonly DependencyProperty AccentProperty=DependencyProperty.Register(nameof(Accent),typeof(Color),typeof(AmbientScene),new FrameworkPropertyMetadata((Color)ColorConverter.ConvertFromString("#9B8D70"),FrameworkPropertyMetadataOptions.AffectsRender));
 public Color Environment {get=>(Color)GetValue(EnvironmentProperty);set=>SetValue(EnvironmentProperty,value);}
 public Color Accent {get=>(Color)GetValue(AccentProperty);set=>SetValue(AccentProperty,value);}
 protected override void OnRender(DrawingContext dc)
 {
  var w=ActualWidth;var h=ActualHeight;if(w<=0||h<=0)return;
  RenderCount++;
  dc.PushClip(new RectangleGeometry(new Rect(0,0,w,h)));
  var basis=Mode==1?DesignTheme.Mix(Environment,Colors.White,.9):DesignTheme.Mix(Environment,Accent,Mode==2?.65:.28);
  dc.DrawRectangle(new LinearGradientBrush(basis,Mode==1?Colors.White:Environment,new Point(0,0),new Point(1,1)),null,new Rect(0,0,w,h));
  var glow=DesignTheme.Mix(Environment,Colors.White,.86);var shadeColor=DesignTheme.Mix(Accent,Colors.Black,.2);
  Color Alpha(Color color,byte alpha)=>Color.FromArgb(alpha,color.R,color.G,color.B);
  var light=new RadialGradientBrush(Alpha(glow,225),Colors.Transparent){GradientOrigin=new(.5,.5),Center=new(.5,.5),RadiusX=.5,RadiusY=.5};
  var centerX=w*(.04+LightPosition*.75);
  dc.DrawEllipse(light,null,new Point(centerX,h*.05),w*.54,h*.8);
  var warm=new RadialGradientBrush(Color.FromArgb(Mode==2?(byte)155:(byte)75,Environment.R,Environment.G,Environment.B),Colors.Transparent);
  dc.DrawEllipse(warm,null,new Point(w*.87,h*.65),w*.65,h*.85);
  // Full-scene vector planes, not a crop from the design reference.
  var geometry=new StreamGeometry();using(var path=geometry.Open()){path.BeginFigure(new Point(w*.70,-h*.15),true,true);path.BezierTo(new Point(w*.34,h*.18),new Point(w*.78,h*.68),new Point(w*.44,h*1.2),true,false);path.LineTo(new Point(w*1.2,h*1.2),true,false);path.LineTo(new Point(w*1.2,-h*.15),true,false);}geometry.Freeze();
  dc.DrawGeometry(new LinearGradientBrush(Alpha(shadeColor,Mode==2?(byte)115:(byte)35),Colors.Transparent,new Point(0,0),new Point(1,0)),null,geometry);
  var shade=new RadialGradientBrush(Alpha(shadeColor,Mode==1?(byte)10:(byte)105),Colors.Transparent);
  dc.DrawEllipse(shade,null,new Point(w*.68,h*.58),w*.40,h*.5);
  var veil=new RadialGradientBrush(Color.FromArgb(180,255,255,255),Colors.Transparent);
  dc.DrawEllipse(veil,null,new Point(w*(.48-LightPosition*.25),h*.44),w*.18,h*.75);
  if(Mode==2){var pen=new Pen(new SolidColorBrush(Alpha(shadeColor,42)),1);for(var x=0d;x<w;x+=64)dc.DrawLine(pen,new Point(x,0),new Point(x+h*.18,h));}
  dc.Pop();
 }
}
