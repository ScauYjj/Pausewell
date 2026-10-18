using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Pausewell.Controls;
using Pausewell.Core;
using Pausewell.MaterialLab;
using static Pausewell.PageLab.PageUi;
namespace Pausewell.PageLab;
public sealed class PageWindow:Window
{
 public PreviewSession Session {get;}
 public AmbientScene Ambient {get;}=new();
 public GlassSurface Navigation {get;}
 public GlassSurface Reading {get;}
 public ScrollViewer BodyViewer {get;}=new();
 public ComboBox ThemeChoice {get;}=new(){ItemsSource=DesignTheme.Names,SelectedIndex=0,Width=136};
 public TextBlock Feedback {get;}=Text("仅本次会话 · 重开重置 · 不触发提醒",12,color:"Color.textSecondary");
 public TextBlock PageTitle {get;private set;}=null!;
 public int SelectedPage {get;private set;}
 public PlanForm? CurrentEditor {get;private set;}
 public Button AddButton {get;private set;}=null!;
 public Button MeetingButton {get;private set;}=null!;
 public Button PauseButton {get;private set;}=null!;
 public List<CheckBox> PlanToggles {get;}=[];
 public List<Button> EditButtons {get;}=[];
 public bool VerificationClosing {get;set;}
 private readonly List<Border> _navFaces=[];
 private readonly StackPanel _body=new();
 public PageWindow(PreviewSession session)
 {
  System.Windows.Automation.AutomationProperties.SetAutomationId(Feedback,"PageFeedback");Session=session;Title="间歇 · 三个基准页面样板";Width=1200;Height=900;MinWidth=900;MinHeight=650;WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new("Microsoft YaHei UI");FontSize=14;UseLayoutRounding=true;Background=DesignTheme.Brush("#F3EEE5");
  var root=new Grid();root.Children.Add(Ambient);var layout=new DockPanel{Margin=new(16)};
  var band=new DockPanel{Margin=new(8,0,8,8)};DockPanel.SetDock(ThemeChoice,Dock.Right);band.Children.Add(ThemeChoice);band.Children.Add(Text("独立页面样板 · 演示时间 10 月 16 日 09:00",12,color:"Color.textSecondary",margin:new(0,12,16,0)));DockPanel.SetDock(band,Dock.Top);layout.Children.Add(band);Id(ThemeChoice,"PageTheme","页面样板主题");ThemeChoice.SelectionChanged+=(_,_)=>{DesignTheme.Apply(Application.Current.Resources.MergedDictionaries[0],DesignTheme.Ids[ThemeChoice.SelectedIndex]);RefreshMaterial();CurrentEditor?.RefreshMaterial();};
  Navigation=new(Ambient,Profiles["navigation"]){Height=56,Width=592,Margin=new(0,8,0,24),HorizontalAlignment=HorizontalAlignment.Center};Navigation.ContentHost.Padding=new(4);var nav=new UniformGrid{Columns=4};
  var names=new[]{"提醒","服药","记录","设置"};var icons=new[]{"reminder","medicine","history","settings"};for(var i=0;i<4;i++){var index=i;var button=Button(names[i],"PageNav"+i,()=>SelectPage(index));button.SetResourceReference(StyleProperty,"NavigationButton");var header=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};header.Children.Add(Glyph(icons[i]));header.Children.Add(Text(names[i],margin:new(8,0,0,0)));button.Content=header;if(i>=2){button.IsEnabled=false;button.Opacity=.45;ToolTipService.SetShowOnDisabled(button,true);button.ToolTip="将在后续页面阶段接入";}var face=new Border{CornerRadius=new(24),Margin=new(1),Child=button};_navFaces.Add(face);nav.Children.Add(face);}Navigation.ContentHost.Child=nav;DockPanel.SetDock(Navigation,Dock.Top);layout.Children.Add(Navigation);
  Reading=new(Ambient,Profiles["content"]);Reading.ContentHost.Padding=new(24);var inside=new DockPanel();Feedback.Margin=new(0,16,0,0);DockPanel.SetDock(Feedback,Dock.Bottom);inside.Children.Add(Feedback);BodyViewer.Content=_body;inside.Children.Add(BodyViewer);Reading.ContentHost.Child=inside;layout.Children.Add(Reading);root.Children.Add(layout);Content=root;
  Activated+=(_,_)=>RefreshMaterial();Deactivated+=(_,_)=>RefreshMaterial();Refresh();
 }
 public void RefreshMaterial(){if(Application.Current.TryFindResource("Theme.ambient") is SolidColorBrush color)Ambient.Environment=color.Color;Ambient.InvalidateVisual();ApplyMaterial(Navigation,IsActive);ApplyMaterial(Reading,IsActive);}
 public void SelectPage(int index){if(index>1)return;SelectedPage=index;BodyViewer.ScrollToHome();Refresh();}
 public void Refresh()
 {
  _body.Children.Clear();PlanToggles.Clear();EditButtons.Clear();for(var i=0;i<_navFaces.Count;i++)_navFaces[i].Background=i==SelectedPage?new SolidColorBrush(Color.FromArgb(215,255,255,255)):Brushes.Transparent;
  var medication=SelectedPage==1;var state=Session.Engine.State;var plans=state.Plans.Where(p=>(p.Kind==HabitKind.Medication)==medication).ToList();var enabled=plans.Count(p=>p.Enabled);
  var heading=new DockPanel{Margin=new(0,0,0,20)};var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};if(!medication){MeetingButton=Button(state.MeetingUntil!=null?"结束会议":"开会 30 分钟","PageMeeting",()=>ChangeMode(true));PauseButton=Button(state.PauseUntil!=null?"恢复普通提醒":"暂停 30 分钟","PagePause",()=>ChangeMode(false));MeetingButton.Margin=new(0,0,8,0);actions.Children.Add(MeetingButton);actions.Children.Add(PauseButton);}DockPanel.SetDock(actions,Dock.Right);heading.Children.Add(actions);var words=new StackPanel();PageTitle=Text(medication?"服药计划":state.PauseUntil!=null?"普通提醒已暂停":state.MeetingUntil!=null?"会议中":enabled==0?"暂无运行中的提醒":"提醒中",28,true);words.Children.Add(PageTitle);words.Children.Add(Text(medication?"按已有用药安排记录，每个时刻独立提醒。":state.PauseUntil!=null?"暂停至 09:30，药物计划保持原时刻。":state.MeetingUntil!=null?"会议至 09:30，普通提醒暂缓呈现。":$"当前有 {enabled} 条普通提醒在运行。",color:"Color.textSecondary",margin:new(0,8,0,0)));heading.Children.Add(words);_body.Children.Add(heading);
  if(!medication)_body.Children.Add(Hero(plans));
  var group=new DockPanel{Margin=new(0,24,0,12)};AddButton=Button(medication?"＋ 新增药物计划":"＋ 新增提醒","PageAdd",()=>OpenEditor(null,medication),true);DockPanel.SetDock(AddButton,Dock.Right);group.Children.Add(AddButton);group.Children.Add(Text(medication?"我的药物计划":"我的提醒",18,true,margin:new(0,8,0,0)));_body.Children.Add(group);
  if(plans.Count==0){var empty=new StackPanel{Margin=new(0,24,0,24)};empty.Children.Add(Text(medication?"尚未添加药物计划":"还没有提醒事项",18,true));empty.Children.Add(Text(medication?"按已有安排添加名称和明确时刻。":"从喝水、起来活动或自己的事项开始。",color:"Color.textSecondary",margin:new(0,8,0,0)));_body.Children.Add(Paper(empty,new(24)));}
  else{var list=new StackPanel();list.Children.Add(Header());foreach(var p in plans)list.Children.Add(Row(p));_body.Children.Add(Paper(list,new(0)));}
 }
 private FrameworkElement Hero(List<ReminderPlan> plans)
 {
  var candidates=plans.Where(p=>p.Enabled).Select(p=>(Plan:p,Times:Session.Engine.Preview(p,Session.Now,1))).Where(x=>x.Times.Count>0).OrderBy(x=>x.Times[0]).ToList();var hero=new StackPanel();hero.Children.Add(Text("下一次普通提醒",color:"Color.textSecondary",margin:new(0,0,0,16)));var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(80)});row.ColumnDefinitions.Add(new(){Width=new GridLength(160)});row.ColumnDefinitions.Add(new());
  var plan=candidates.FirstOrDefault().Plan;row.Children.Add(Badge(plan?.Kind??HabitKind.Water,80));var when=new StackPanel{Margin=new(16,0,0,0),VerticalAlignment=VerticalAlignment.Center};when.Children.Add(Text(plan?.Name??"暂无普通提醒",16,true));var paused=Session.Engine.State.PauseUntil!=null;var time=Text(paused?"恢复后计算":plan==null?"—":Session.Engine.Local(candidates[0].Times[0]).ToString("HH:mm"),paused?20:44,true);time.FontFamily=new("Segoe UI");Id(time,"HeroTime","下一次普通提醒时间");when.Children.Add(time);Grid.SetColumn(when,1);row.Children.Add(when);var detail=Text(plan==null?"新增提醒，或开启已有事项。":Session.Engine.State.PauseUntil!=null?"暂停期间不呈现普通提醒，恢复后重新计算间隔。":Session.Engine.State.MeetingUntil!=null?"会议期间暂缓，结束后按待办逐项处理。":plan.Schedule==ScheduleKind.Interval?$"确认完成后，再等 {plan.IntervalMinutes} 分钟。":"每天按已填写时刻提醒。",color:"Color.textSecondary");detail.VerticalAlignment=VerticalAlignment.Center;detail.Margin=new(24,0,0,0);Grid.SetColumn(detail,2);row.Children.Add(detail);hero.Children.Add(row);var paper=Paper(hero,new(24));paper.MinHeight=160;return paper;
 }
 private static Grid Columns(){var g=new Grid();g.ColumnDefinitions.Add(new());g.ColumnDefinitions.Add(new(){Width=new GridLength(144)});g.ColumnDefinitions.Add(new(){Width=new GridLength(120)});g.ColumnDefinitions.Add(new(){Width=new GridLength(72)});return g;}
 private FrameworkElement Header(){var row=Columns();row.MinHeight=40;row.Margin=new(16,0,16,0);var labels=new[]{"事项 / 循环规则","下次提醒","状态",""};for(var i=0;i<4;i++){var t=Text(labels[i],12,color:"Color.textSecondary");t.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(t,i);row.Children.Add(t);}return new Border{Background=DesignTheme.Brush("#F7F4EE"),CornerRadius=new(12,12,0,0),Child=row};}
 private FrameworkElement Row(ReminderPlan plan)
 {
  var row=Columns();row.MinHeight=80;row.Margin=new(16,0,16,0);var label=new Grid();label.ColumnDefinitions.Add(new(){Width=new GridLength(48)});label.ColumnDefinitions.Add(new());var badge=Badge(plan.Kind);badge.VerticalAlignment=VerticalAlignment.Center;label.Children.Add(badge);var words=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new(16,8,16,8)};var name=Text(plan.Name,16,true);name.MaxHeight=48;name.TextTrimming=TextTrimming.CharacterEllipsis;name.ToolTip=plan.Name;words.Children.Add(name);words.Children.Add(Text(plan.Schedule==ScheduleKind.Interval?$"确认完成后 {plan.IntervalMinutes} 分钟 · {Labels.Style(plan.Style)}":string.Join(" / ",plan.Times.Order()),color:"Color.textSecondary"));Grid.SetColumn(words,1);label.Children.Add(words);row.Children.Add(label);
  var future=Session.Engine.Preview(plan,Session.Now,1);var text=!plan.Enabled?"已停用":plan.Kind!=HabitKind.Medication&&Session.Engine.State.PauseUntil!=null?"恢复后计算":future.Count==0?"范围内无未来时刻":Session.Engine.Local(future[0]).ToString("MM-dd HH:mm");var next=Text(text,14);next.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(next,1);row.Children.Add(next);
  var toggle=new CheckBox{Content=plan.Enabled?"开启":"关闭",IsChecked=plan.Enabled,Style=ToggleStyle(),VerticalAlignment=VerticalAlignment.Center};Id(toggle,"PlanToggle_"+plan.Id,plan.Name+"启停");toggle.Click+=(_,_)=>{try{Session.Engine.SetEnabled(plan.Id,toggle.IsChecked==true,Session.Now);Refresh();Feedback.Text="已更新本次会话的启停状态。";}catch(Exception ex){toggle.IsChecked=plan.Enabled;Feedback.Text="未更新："+ex.Message;}};PlanToggles.Add(toggle);Grid.SetColumn(toggle,2);row.Children.Add(toggle);var edit=Button("编辑","Edit_"+plan.Id,()=>OpenEditor(plan,plan.Kind==HabitKind.Medication));edit.VerticalAlignment=VerticalAlignment.Center;EditButtons.Add(edit);Grid.SetColumn(edit,3);row.Children.Add(edit);var frame=new Border{Child=row,BorderThickness=new(0,1,0,0)};frame.SetResourceReference(Border.BorderBrushProperty,"Color.divider");return frame;
 }
 public PlanForm OpenEditor(ReminderPlan? plan,bool medication)
 {
  if(CurrentEditor!=null)return CurrentEditor;
  var editor=new PlanForm(Session,plan,medication){Owner=this};CurrentEditor=editor;IsEnabled=false;editor.Closed+=(_,_)=>{CurrentEditor=null;IsEnabled=true;Refresh();if(editor.Saved)Feedback.Text="已保存“"+editor.SavedName+"”，本次会话列表已更新。";Activate();AddButton.Focus();};editor.Show();return editor;
 }
 private void ChangeMode(bool meeting){try{var e=Session.Engine;if(meeting){if(e.State.MeetingUntil!=null)e.EndMeeting(Session.Now);else e.BeginMeeting(Session.Now.AddMinutes(30),Session.Now);}else{if(e.State.PauseUntil!=null)e.ResumePause(Session.Now);else e.BeginPause(Session.Now.AddMinutes(30),false,Session.Now);}Refresh();Feedback.Text="模式已更新，仅作用于本次会话。";}catch(Exception ex){Feedback.Text="未更新："+ex.Message;}}
}
