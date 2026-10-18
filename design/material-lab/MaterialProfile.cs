using System.Text.Json;
using System.Windows;
namespace Pausewell.MaterialLab;
public sealed record ShadowProfile(double Y,double Blur,double Opacity);
public sealed record MaterialProfile(string Name,double Tint,double Blur,double Light,double Shade,double Radius,ShadowProfile[] Shadows)
{
 public static IReadOnlyDictionary<string,MaterialProfile> Load(string tuningAssembly="Pausewell.MaterialLab")
 {
  using var stream=Application.GetResourceStream(new Uri("pack://application:,,,/Pausewell.Controls;component/Assets/tokens.json"))!.Stream;
  using var json=JsonDocument.Parse(stream);var root=json.RootElement;var material=root.GetProperty("material");var elevation=root.GetProperty("elevation");
  MaterialProfile Read(string key,double radius){var item=material.GetProperty(key);return new(key,item.GetProperty("tintOpacity").GetDouble(),item.TryGetProperty("backdropBlurDip",out var blur)?blur.GetDouble():0,item.TryGetProperty("edgeLightOpacity",out var light)?light.GetDouble():.7,item.TryGetProperty("edgeShadeOpacity",out var shade)?shade.GetDouble():.06,radius,elevation.GetProperty(item.GetProperty("elevation").GetString()!).EnumerateArray().Select(x=>new ShadowProfile(x.GetProperty("y").GetDouble(),x.GetProperty("blur").GetDouble(),x.GetProperty("opacity").GetDouble())).ToArray());}
  using var tuningStream=Application.GetResourceStream(new Uri($"pack://application:,,,/{tuningAssembly};component/Assets/material-tuning.json"))!.Stream;
  using var tuning=JsonDocument.Parse(tuningStream);var values=tuning.RootElement;
  return new Dictionary<string,MaterialProfile>{{"content",Read("content",16)},{"navigation",Read("navigation",28) with{Tint=values.GetProperty("navigationTintOpacity").GetDouble()}},{"popup",Read("popup",24) with{Tint=values.GetProperty("popupTintOpacity").GetDouble()}}};
 }
}
