using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Xml.Linq;
using Pausewell.Controls;
namespace Pausewell.MaterialLab;
public sealed class MaterialWindow:Window
{
 public ResourceDictionary Palette {get;}
 public IReadOnlyDictionary<string,MaterialProfile> Profiles {get;}=MaterialProfile.Load();
 public AmbientScene Ambient {get;}=new();
 public Grid BaseScene {get;}=new();
 public Grid Stage {get;}=new();
 public Grid ModalLayer {get;}=new(){Visibility=Visibility.Collapsed};
 public ComboBox EnvironmentChoice {get;}=new(){ItemsSource=new[]{"暖色环境","明亮环境","对比测试环境"},SelectedIndex=0,Width=148};
 public ComboBox ThemeChoice {get;}=new(){ItemsSource=DesignTheme.Names,SelectedIndex=0,Width=124};
 public CheckBox ReducedChoice {get;}=new(){Content="减少透明度"};
 public CheckBox InactiveChoice {get;}=new(){Content="模拟失焦"};
 public Slider LightChoice {get;}=new(){Minimum=0,Maximum=1,Value=.36,Width=112,VerticalAlignment=VerticalAlignment.Center};
 public Button PreviewButton {get;}=new(){Content="预览提醒弹窗"};
 public Button CompareButton {get;}=new(){Content="材质透射对照"};
 public Button ConfirmButton {get;}=new(){Content="确认已喝水"};
 public Button DeferButton {get;}=new(){Content="5 分钟后再提醒"};
 public Button ReturnButton {get;}=new(){Content="返回样板"};
 public GlassSurface Navigation {get;private set;}=null!;
 public GlassSurface ContentSurface {get;private set;}=null!;
 public GlassSurface Dialog {get;private set;}=null!;
 public TextBlock Status {get;}=Text("材质样板 · 固定演示时间 09:00 · 不创建提醒",12,"Color.textSecondary");
 public int PreviewConfirmCount {get;private set;}
 public int PreviewDeferCount {get;private set;}
 public int SelectedNavigation {get;private set;}
 public bool ForcedFallback=>SystemParameters.HighContrast||!SystemParameters.UIEffects||(RenderCapability.Tier>>16)==0;
 public bool EffectiveReduced=>ReducedChoice.IsChecked==true||ForcedFallback;
 private readonly List<Button> _navButtons=[];
 private readonly List<Border> _navFaces=[];
 private readonly List<GlassSurface> _surfaces=[];
 private readonly List<CheckBox> _enabled=[];
 private readonly TextBlock _running=Text("当前有 3 条普通提醒在运行。",14,"Color.textSecondary");
 private readonly TextBlock _next=Text("09:30",44);
 private readonly TextBlock _heroName=Text("喝水",18);
 private readonly TextBlock _heroDetail=Text("还有 30 分钟 · 确认完成后再等 30 分钟。",14,"Color.textSecondary");
 private IInputElement? _previousFocus;
 private readonly ComboBox[] _settings;
 public MaterialWindow(ResourceDictionary palette)
 {
  Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Pausewell.MaterialLab;component/WorkbenchStyles.xaml",UriKind.Relative)});Palette=palette;Title="间歇 · 材质与光影样板";Width=1200;Height=940;MinWidth=900;MinHeight=650;WindowStartupLocation=WindowStartupLocation.CenterScreen;UseLayoutRounding=true;FontFamily=new("Microsoft YaHei UI");FontSize=14;Foreground=DesignTheme.Brush("#292824");Background=DesignTheme.Brush("#F7F4EE");
  _settings=[EnvironmentChoice,ThemeChoice];
  var root=new DockPanel();var controls=new Border{Padding=new(20,12,20,12),Background=DesignTheme.Brush("#FCFBF8"),BorderBrush=DesignTheme.Brush("#D9D5CD"),BorderThickness=new(0,0,0,1)};var toolStack=new StackPanel();var tools=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};
  tools.Children.Add(Text("材质与光影",18,bold:true,margin:new(0,8,24,8)));AddTool(tools,EnvironmentChoice);AddTool(tools,ThemeChoice);AddTool(tools,ReducedChoice);AddTool(tools,InactiveChoice);AddTool(tools,PreviewButton);AddTool(tools,CompareButton);toolStack.Children.Add(tools);
  var detail=new DockPanel{Margin=new(0,8,0,0)};var light=new StackPanel{Orientation=Orientation.Horizontal};light.Children.Add(Text("环境光位置",12,"Color.textSecondary",margin:new(0,0,8,0)));light.Children.Add(LightChoice);DockPanel.SetDock(light,Dock.Right);detail.Children.Add(light);detail.Children.Add(Status);toolStack.Children.Add(detail);controls.Child=toolStack;DockPanel.SetDock(controls,Dock.Top);root.Children.Add(controls);
  Stage.ClipToBounds=true;BaseScene.Children.Add(Ambient);Stage.Children.Add(BaseScene);BuildScene();BuildModal();Stage.Children.Add(ModalLayer);root.Children.Add(Stage);Content=root;
  Id(EnvironmentChoice,"EnvironmentChoice","材质背景");Id(ThemeChoice,"ThemeChoice","材质主题");Id(ReducedChoice,"ReducedChoice","减少透明度");Id(InactiveChoice,"InactiveChoice","模拟失焦");Id(LightChoice,"LightChoice","环境光位置");Id(PreviewButton,"PreviewReminder","预览提醒弹窗");Id(ConfirmButton,"ConfirmPreview","确认样板喝水");Id(DeferButton,"DeferPreview","延期样板提醒");Id(ReturnButton,"ReturnPreview","返回材质样板");Id(Status,"MaterialStatus","样板状态");
  Id(CompareButton,"CompareMaterial","显示独立材质透射对照");CompareButton.Click+=(_,_)=>ShowPreview(true);
  EnvironmentChoice.SelectionChanged+=(_,_)=>{Ambient.Mode=EnvironmentChoice.SelectedIndex;Ambient.InvalidateVisual();};ThemeChoice.SelectionChanged+=(_,_)=>{DesignTheme.Apply(Palette,DesignTheme.Ids[ThemeChoice.SelectedIndex]);ApplyState();};ReducedChoice.Checked+=(_,_)=>ApplyState();ReducedChoice.Unchecked+=(_,_)=>ApplyState();InactiveChoice.Checked+=(_,_)=>ApplyState();InactiveChoice.Unchecked+=(_,_)=>ApplyState();LightChoice.ValueChanged+=(_,_)=>{Ambient.LightPosition=LightChoice.Value;Ambient.InvalidateVisual();};PreviewButton.Click+=(_,_)=>ShowPreview();ConfirmButton.Click+=(_,_)=>ClosePreview("已确认样板喝水。演示计数 +1，不写入真实记录。",true);DeferButton.Click+=(_,_)=>ClosePreview("样板已延期 5 分钟，未计为完成。",false);ReturnButton.Click+=(_,_)=>ClosePreview("已返回材质样板，未记录任何完成。");
  PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&ModalLayer.IsVisible){ClosePreview("已退出弹窗预览，未记录完成。");e.Handled=true;}};
  Activated+=(_,_)=>ApplyState();Deactivated+=(_,_)=>ApplyState();SystemParameters.StaticPropertyChanged+=OnSystemParametersChanged;Closed+=(_,_)=>SystemParameters.StaticPropertyChanged-=OnSystemParametersChanged;ApplyState();
 }
 private void BuildScene()
 {
  var layout=new Grid{Margin=new(16)};layout.RowDefinitions.Add(new(){Height=new GridLength(88)});layout.RowDefinitions.Add(new());
  Navigation=new(Ambient,Profiles["navigation"]){Height=56,Width=592,VerticalAlignment=VerticalAlignment.Center};Navigation.ContentHost.Padding=new(4);_surfaces.Add(Navigation);
  var nav=new UniformGrid{Columns=4};var names=new[]{"提醒","服药","记录","设置"};var icons=new[]{"reminder","medicine","history","settings"};
  for(var i=0;i<4;i++){var index=i;var button=new Button{Background=Brushes.Transparent,BorderThickness=new(0),Padding=new(12,8,12,8),MinHeight=46};button.SetResourceReference(StyleProperty,"NavigationButton");var header=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};header.Children.Add(Glyph(icons[i],20));header.Children.Add(Text(names[i],14,margin:new(8,0,0,0)));button.Content=header;Id(button,"Nav"+i,names[i]+"材质示例");var face=new Border{CornerRadius=new(24),Margin=new(1),Child=button};_navButtons.Add(button);_navFaces.Add(face);nav.Children.Add(face);button.Click+=(_,_)=>{SelectedNavigation=index;UpdateNavigation();Status.Text="已切换“"+names[index]+"”导航选中态，内容保留为材质示例。";};}
  Navigation.ContentHost.Child=nav;layout.Children.Add(Navigation);
  ContentSurface=new(Ambient,Profiles["content"]);ContentSurface.ContentHost.Padding=new(24);_surfaces.Add(ContentSurface);Grid.SetRow(ContentSurface,1);layout.Children.Add(ContentSurface);BaseScene.Children.Add(layout);
  var inside=new DockPanel();var footer=Text("演示场景，不触发提醒。点击顶部“预览提醒弹窗”检查任务浮层。",12,"Color.textSecondary",margin:new(0,16,0,0));DockPanel.SetDock(footer,Dock.Bottom);inside.Children.Add(footer);
  var body=new StackPanel();var title=new Grid{Margin=new(0,0,0,20)};title.ColumnDefinitions.Add(new());title.ColumnDefinitions.Add(new(){Width=GridLength.Auto});var words=new StackPanel();words.Children.Add(Text("提醒中",28,bold:true));words.Children.Add(_running);title.Children.Add(words);var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};var meeting=new Button{Content="我要开会",Margin=new(0,0,8,0)};meeting.Click+=(_,_)=>Status.Text="当前仅评审材质，会议流程沿用已确认的产品交互。";var pause=new Button{Content="暂停普通提醒"};pause.Click+=(_,_)=>Status.Text="当前仅评审材质，未暂停正在运行的真实提醒。";actions.Children.Add(meeting);actions.Children.Add(pause);Grid.SetColumn(actions,1);title.Children.Add(actions);body.Children.Add(title);
  var hero=new Border{MinHeight=160,Padding=new(24),BorderThickness=new(1),CornerRadius=new(12)};hero.SetResourceReference(Border.BorderBrushProperty,"Color.border");hero.Background=new SolidColorBrush(Color.FromArgb(195,255,255,255));var heroBody=new StackPanel();heroBody.Children.Add(Text("下一次普通提醒",14,"Color.textSecondary",margin:new(0,0,0,16)));var heroLine=new Grid();heroLine.ColumnDefinitions.Add(new(){Width=new GridLength(80)});heroLine.ColumnDefinitions.Add(new(){Width=new GridLength(144)});heroLine.ColumnDefinitions.Add(new());heroLine.Children.Add(Badge("water",80));var time=new StackPanel{Margin=new(16,0,0,0),VerticalAlignment=VerticalAlignment.Center};time.Children.Add(_heroName);_next.FontFamily=new("Segoe UI");_next.FontWeight=FontWeights.SemiBold;time.Children.Add(_next);Grid.SetColumn(time,1);heroLine.Children.Add(time);var description=new Border{BorderThickness=new(1,0,0,0),Padding=new(24,0,0,0),Margin=new(16,12,0,12),VerticalAlignment=VerticalAlignment.Center,Child=_heroDetail};description.SetResourceReference(Border.BorderBrushProperty,"Color.divider");Grid.SetColumn(description,2);heroLine.Children.Add(description);heroBody.Children.Add(heroLine);hero.Child=heroBody;body.Children.Add(hero);
  var groupTitle=new DockPanel{Margin=new(0,24,0,12)};var add=new Button{Content="＋ 新增提醒"};add.SetResourceReference(StyleProperty,"PrimaryButton");add.Click+=(_,_)=>Status.Text="当前为材质样板，下一阶段将制作完整编辑页面。";DockPanel.SetDock(add,Dock.Right);groupTitle.Children.Add(add);groupTitle.Children.Add(Text("我的提醒",18,bold:true,margin:new(0,8,0,0)));body.Children.Add(groupTitle);
  var rows=new StackPanel();rows.Children.Add(ListHeader());var kinds=new[]{"water","activity","toilet"};var namesList=new[]{"喝水","起来活动","如厕提醒"};var times=new[]{"09:30","09:45","10:00"};var minutes=new[]{30,45,60};for(var i=0;i<3;i++)rows.Children.Add(ReminderRow(kinds[i],namesList[i],times[i],minutes[i],i));var list=new Border{BorderThickness=new(1),CornerRadius=new(12),Child=rows};list.SetResourceReference(Border.BorderBrushProperty,"Color.border");body.Children.Add(list);
  inside.Children.Add(new ScrollViewer{Content=body});ContentSurface.ContentHost.Child=inside;UpdateNavigation();
 }
 private Grid Columns(){var g=new Grid();g.ColumnDefinitions.Add(new());g.ColumnDefinitions.Add(new(){Width=new GridLength(136)});g.ColumnDefinitions.Add(new(){Width=new GridLength(120)});g.ColumnDefinitions.Add(new(){Width=new GridLength(72)});return g;}
 private UIElement ListHeader(){var row=Columns();row.Margin=new(16,0,16,0);row.MinHeight=40;var titles=new[]{"事项 / 循环规则","下次提醒","状态",""};for(var i=0;i<4;i++){var t=Text(titles[i],12,"Color.textSecondary");t.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(t,i);row.Children.Add(t);}return new Border{Background=new SolidColorBrush(Color.FromArgb(55,221,216,206)),Child=row,CornerRadius=new(12,12,0,0)};}
 private UIElement ReminderRow(string icon,string name,string time,int minutes,int index)
 {
  var row=Columns();row.Margin=new(16,12,16,12);row.MinHeight=56;var label=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};label.Children.Add(Badge(icon,48));var texts=new StackPanel{Margin=new(16,0,8,0)};texts.Children.Add(Text(name,16,bold:true));texts.Children.Add(Text($"确认完成后 {minutes} 分钟",14,"Color.textSecondary"));label.Children.Add(texts);row.Children.Add(label);var next=Text(time,16);next.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(next,1);row.Children.Add(next);
  var toggle=new CheckBox{IsChecked=true,Content="开启",VerticalAlignment=VerticalAlignment.Center};toggle.Style=ToggleStyle();Id(toggle,"Enable"+index,name+"样板启停");_enabled.Add(toggle);toggle.Checked+=(_,_)=>UpdateEnabled();toggle.Unchecked+=(_,_)=>UpdateEnabled();Grid.SetColumn(toggle,2);row.Children.Add(toggle);var edit=new Button{Content="编辑",VerticalAlignment=VerticalAlignment.Center};edit.Click+=(_,_)=>Status.Text="“"+name+"”的完整编辑页面将在材质确认后接入。";Grid.SetColumn(edit,3);row.Children.Add(edit);var outer=new Border{Child=row,BorderThickness=new(0,1,0,0)};outer.SetResourceReference(Border.BorderBrushProperty,"Color.divider");return outer;
 }
 private static Style ToggleStyle()=> (Style)System.Windows.Markup.XamlReader.Parse("""
 <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="CheckBox"><Setter Property="FocusVisualStyle" Value="{DynamicResource FocusRing}"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><StackPanel Orientation="Horizontal" Height="36"><Border x:Name="Track" Width="48" Height="28" Background="{DynamicResource Color.offTrack}" CornerRadius="14" VerticalAlignment="Center"><Ellipse x:Name="Thumb" Width="22" Height="22" Margin="3" Fill="White" HorizontalAlignment="Left"/></Border><ContentPresenter Margin="8,0,0,0" VerticalAlignment="Center"/></StackPanel><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Track" Property="Background" Value="{DynamicResource Theme.toggle}"/><Setter TargetName="Thumb" Property="HorizontalAlignment" Value="Right"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 """);
 private void UpdateEnabled(){foreach(var check in _enabled)check.Content=check.IsChecked==true?"开启":"关闭";var count=_enabled.Count(c=>c.IsChecked==true);_running.Text=$"当前有 {count} 条普通提醒在运行。";var index=_enabled.FindIndex(c=>c.IsChecked==true);_heroName.Text=index<0?"暂无普通提醒":new[]{"喝水","起来活动","如厕提醒"}[index];_next.Text=index<0?"—":new[]{"09:30","09:45","10:00"}[index];_heroDetail.Text=index<0?"开启任一事项可查看下次提醒样式。":"演示时间，确认完成后再计算间隔。";}
 private void BuildModal()
 {
  ModalLayer.Children.Add(new Border{Background=new SolidColorBrush(Color.FromArgb(46,24,22,20))});
  Dialog=new(BaseScene,Profiles["popup"]){Width=480,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MaxHeight=680,Margin=new(24)};_surfaces.Add(Dialog);Dialog.ContentHost.Padding=new(32);
  var root=new DockPanel();var actions=new StackPanel{Margin=new(0,24,0,0)};ConfirmButton.MinHeight=44;ConfirmButton.SetResourceReference(StyleProperty,"PrimaryButton");DeferButton.MinHeight=44;DeferButton.Margin=new(0,8,0,0);ReturnButton.Margin=new(0,8,0,0);actions.Children.Add(ConfirmButton);actions.Children.Add(DeferButton);actions.Children.Add(ReturnButton);actions.Children.Add(Text("延期不计为完成 · 本窗口为材质预览",12,"Color.textSecondary",margin:new(0,16,0,0)));DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
  var body=new StackPanel{HorizontalAlignment=HorizontalAlignment.Stretch};body.Children.Add(Text("间歇",14,"Color.textSecondary"));var badge=Badge("water",96);badge.HorizontalAlignment=HorizontalAlignment.Center;badge.Margin=new(0,24,0,20);body.Children.Add(badge);var title=Text("该喝水了",28,bold:true);title.TextAlignment=TextAlignment.Center;body.Children.Add(title);var planned=Text("原计划 09:30",14,"Color.textSecondary",margin:new(0,8,0,20));planned.TextAlignment=TextAlignment.Center;body.Children.Add(planned);var well=new Border{Background=DesignTheme.Brush("#FCFBF8"),CornerRadius=new(12),Padding=new(16),Child=Text("方便时喝点水，完成后再确认。\n确认完成后，再等 30 分钟提醒。",14)};body.Children.Add(well);root.Children.Add(new ScrollViewer{Content=body});Dialog.ContentHost.Child=root;ModalLayer.Children.Add(Dialog);KeyboardNavigation.SetTabNavigation(ModalLayer,KeyboardNavigationMode.Cycle);FocusManager.SetIsFocusScope(ModalLayer,true);
 }
 public void ShowPreview(bool compare=false){if(ModalLayer.IsVisible)return;_previousFocus=Keyboard.FocusedElement;ContentSurface.Visibility=compare?Visibility.Hidden:Visibility.Visible;BaseScene.IsEnabled=false;foreach(var c in _settings)c.IsEnabled=false;ReducedChoice.IsEnabled=false;InactiveChoice.IsEnabled=false;PreviewButton.IsEnabled=false;CompareButton.IsEnabled=false;LightChoice.IsEnabled=false;Dialog.MaxHeight=Math.Max(300,Stage.ActualHeight-48);ModalLayer.Visibility=Visibility.Visible;ApplyState();Dispatcher.BeginInvoke(()=>ConfirmButton.Focus());}
 public void ClosePreview(string text,bool? complete=null){if(!ModalLayer.IsVisible)return;if(complete==true)PreviewConfirmCount++;if(complete==false)PreviewDeferCount++;ModalLayer.Visibility=Visibility.Collapsed;ContentSurface.Visibility=Visibility.Visible;BaseScene.IsEnabled=true;foreach(var c in _settings)c.IsEnabled=true;ReducedChoice.IsEnabled=true;InactiveChoice.IsEnabled=true;PreviewButton.IsEnabled=true;CompareButton.IsEnabled=true;LightChoice.IsEnabled=true;Status.Text=text;if(_previousFocus is {} previous)Keyboard.Focus(previous);else PreviewButton.Focus();}
 private void UpdateNavigation(){for(var i=0;i<_navFaces.Count;i++){_navFaces[i].Background=i==SelectedNavigation?new SolidColorBrush(Color.FromArgb(210,255,255,255)):Brushes.Transparent;_navFaces[i].Effect=i==SelectedNavigation?new DropShadowEffect{BlurRadius=6,ShadowDepth=1,Opacity=.06,Color=Colors.Black}:null;}}
 private void OnSystemParametersChanged(object? sender,PropertyChangedEventArgs e)=>ApplyState();
 public void ApplyState()
 {
  DesignTheme.Apply(Palette,DesignTheme.Ids[ThemeChoice.SelectedIndex]);
  if(Palette["Theme.ambient"] is SolidColorBrush env)Ambient.Environment=env.Color;Ambient.InvalidateVisual();var inactive=InactiveChoice.IsChecked==true||!IsActive;
  foreach(var surface in _surfaces)surface.Configure(EffectiveReduced,inactive);
  if(SystemParameters.HighContrast){Palette["Color.textPrimary"]=SystemColors.WindowTextBrush;Palette["Color.textSecondary"]=SystemColors.WindowTextBrush;Palette["Color.surfaceSolid"]=SystemColors.WindowBrush;}
 }
 private static void AddTool(Panel parent,FrameworkElement element){element.Margin=new(0,0,16,0);element.VerticalAlignment=VerticalAlignment.Center;parent.Children.Add(element);}
 public static void Id(DependencyObject e,string id,string name){AutomationProperties.SetAutomationId(e,id);AutomationProperties.SetName(e,name);}
 public static TextBlock Text(string text,double size=14,string color="Color.textPrimary",bool bold=false,Thickness? margin=null){var t=new TextBlock{Text=text,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=margin??new()};t.SetResourceReference(TextBlock.ForegroundProperty,color);return t;}
 private static Image Glyph(string name,double size){using var stream=Application.GetResourceStream(new Uri($"pack://application:,,,/Pausewell.MaterialLab;component/Assets/{name}.svg"))!.Stream;var doc=XDocument.Load(stream);var drawing=new DrawingGroup();foreach(var p in doc.Descendants().Where(x=>x.Name.LocalName=="path"))drawing.Children.Add(new GeometryDrawing(DesignTheme.Brush("#292824"),null,Geometry.Parse(p.Attribute("d")!.Value)));drawing.Freeze();return new Image{Source=new DrawingImage(drawing),Width=size,Height=size};}
 private static Border Badge(string name,double size){var badge=new Border{Width=size,Height=size,CornerRadius=new(size/2),Child=Glyph(name,size*.50),BorderThickness=new(1)};badge.SetResourceReference(Border.BackgroundProperty,"Theme.soft");badge.BorderBrush=new SolidColorBrush(Color.FromArgb(180,255,255,255));return badge;}
}
