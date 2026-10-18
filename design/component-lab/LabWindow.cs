using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Pausewell.Controls;
using Calendar=System.Windows.Controls.Calendar;

namespace Pausewell.ComponentLab;
public sealed class LabWindow : Window
{
    public ResourceDictionary Palette {get;}
    public TabControl Pages {get;}=new();
    public TextBox PlanName {get;}=new(){Text="喝水",MaxLength=80};
    public TextBox Interval {get;}=new(){Text="30"};
    public TextBox TimeInput {get;}=new(){Text="08:30"};
    public TextBox Note {get;}=new(){AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=80,MaxHeight=160,MaxLength=1000,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    public ComboBox Kind {get;}=new(){ItemsSource=new[]{"喝水","起来活动","如厕提醒","自定义"},SelectedIndex=0};
    public ComboBox ReminderStyle {get;}=new(){ItemsSource=new[]{"轻提示","醒目弹窗","全屏提醒"},SelectedIndex=1};
    public ComboBox LongOptions {get;}=new(){ItemsSource=Enumerable.Range(1,18).Select(i=>i==4?"选项 4：较长的说明会自然换行，不缩小字号，也不挤进箭头区域。":"选项 "+i).ToList(),SelectedIndex=0};
    public ComboBox EmptyOptions {get;}=new();
    public IsoDatePicker StartDate {get;}=new(){SelectedDate=DateTime.Today};
    public IsoDatePicker EndDate {get;}=new(){AllowClear=true,IsEnabled=false};
    public CheckBox UseEnd {get;}=new(){Content="设置结束日期"};
    public CheckBox EnablePlan {get;}=new(){Content="启用此提醒",IsChecked=true};
    public Expander More {get;}=new(){Header="更多设置"};
    public Calendar StandaloneCalendar {get;}=new(){SelectedDate=DateTime.Today,DisplayDate=DateTime.Today};
    public ScrollViewer LongScroll {get;}=new(){Height=280,Focusable=true};
    public ScrollViewer ShortScroll {get;}=new(){Height=100,Focusable=true};
    public Button Save {get;}=new(){Content="保存样板"};
    public TextBlock Status {get;}=new(){Text="样板仅在内存中操作，不创建提醒，也不连接提醒数据库。",TextWrapping=TextWrapping.Wrap};
    public int SaveCount {get;private set;}
    public List<RadioButton> Counts {get;}=[];
    private TextBlock? _countHeading;
    public string LastSavedName {get;private set;}="";
    public LabWindow(ResourceDictionary palette)
    {
        Palette=palette;Title="间歇 · 原生组件样板 v0.3.1";Width=1280;Height=960;MinWidth=900;MinHeight=650;WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new("Microsoft YaHei UI");FontSize=14;Background=DesignTheme.Brush("#F3F0E9");UseLayoutRounding=true;Foreground=DesignTheme.Brush("#292824");
        var root=new DockPanel{Margin=new(24)};var title=new Grid{Margin=new(0,0,0,20)};title.ColumnDefinitions.Add(new());title.ColumnDefinitions.Add(new(){Width=GridLength.Auto});var heading=new StackPanel();heading.Children.Add(Text("原生组件样板",28,true));heading.Children.Add(Text("按 v0.3.1 规范检查真实控件。材质和正式页面将在后续单独评审。",14,false,"Color.textSecondary",new(0,8,0,0)));title.Children.Add(heading);
        var theme=new ComboBox{ItemsSource=DesignTheme.Names,SelectedIndex=0,Width=160,VerticalAlignment=VerticalAlignment.Center};Id(theme,"ThemeChoice","样板主题");theme.SelectionChanged+=(_,_)=>DesignTheme.Apply(Palette,DesignTheme.Ids[theme.SelectedIndex]);Grid.SetColumn(theme,1);title.Children.Add(theme);DockPanel.SetDock(title,Dock.Top);root.Children.Add(title);
        var footer=new Grid{Margin=new(0,16,0,0)};footer.ColumnDefinitions.Add(new());footer.ColumnDefinitions.Add(new(){Width=GridLength.Auto});Status.SetResourceReference(TextBlock.ForegroundProperty,"Color.textSecondary");Status.VerticalAlignment=VerticalAlignment.Center;Status.Margin=new(0,0,24,0);footer.Children.Add(Status);Save.SetResourceReference(StyleProperty,"PrimaryButton");Save.Click+=(_,_)=>TrySave();Id(Save,"SaveSample","保存样板，不创建提醒");Grid.SetColumn(Save,1);footer.Children.Add(Save);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        SetupTabs();Pages.Items.Add(Tab("普通表单","reminder",FormPage()));Pages.Items.Add(Tab("下拉与日期","history",SelectionPage()));Pages.Items.Add(Tab("状态与滚动","settings",StatesPage()));root.Children.Add(Pages);Content=root;
        UseEnd.Checked+=(_,_)=>EndDate.IsEnabled=true;UseEnd.Unchecked+=(_,_)=>{EndDate.IsEnabled=false;FieldState.SetError(EndDate,"");};More.Expanded+=(_,_)=>More.Header="收起设置";More.Collapsed+=(_,_)=>More.Header="更多设置";
        Id(PlanName,"PlanName","提醒名称");Id(Interval,"Interval","间隔分钟");Id(TimeInput,"TimeInput","明确时刻");Id(Note,"Note","备注");Id(Kind,"Kind","提醒事项");Id(ReminderStyle,"ReminderStyle","提醒方式");Id(StartDate,"StartDate","开始");Id(EndDate,"EndDate","结束");Id(UseEnd,"UseEnd","设置结束日期");Id(EnablePlan,"EnablePlan","启用此提醒");Id(More,"More","更多设置");Id(LongOptions,"LongOptions","长选项");Id(EmptyOptions,"EmptyOptions","暂无选项");Id(LongScroll,"LongScroll","溢出滚动区");Id(ShortScroll,"ShortScroll","无溢出滚动区");
    }
    private void SetupTabs()
    {
        Pages.BorderThickness=new(0);Pages.Background=Brushes.Transparent;
        var template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="TabControl"><Grid><Grid.RowDefinitions><RowDefinition Height="72"/><RowDefinition/></Grid.RowDefinitions><Border Background="{DynamicResource Color.surfaceFallback}" CornerRadius="28" Padding="4" HorizontalAlignment="Center" VerticalAlignment="Top"><TabPanel IsItemsHost="True"/></Border><Border Grid.Row="1" Background="{DynamicResource Color.surfaceSolid}" BorderBrush="{DynamicResource Color.border}" BorderThickness="1" CornerRadius="16" Padding="24"><ContentPresenter x:Name="PART_SelectedContentHost" ContentSource="SelectedContent"/></Border></Grid></ControlTemplate>
        """);Pages.Template=template;
        var style=(Style)System.Windows.Markup.XamlReader.Parse("""
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="TabItem"><Setter Property="FocusVisualStyle" Value="{DynamicResource PillFocusRing}"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TabItem"><Border Name="Face" CornerRadius="24" Background="Transparent" Padding="24,12" Margin="2"><ContentPresenter ContentSource="Header"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Face" Property="Background" Value="{DynamicResource Color.fieldHover}"/></Trigger><Trigger Property="IsSelected" Value="True"><Setter TargetName="Face" Property="Background" Value="{DynamicResource Color.surfaceSolid}"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
        """);Pages.ItemContainerStyle=style;
    }
    private TabItem Tab(string name,string icon,UIElement content){var item=new TabItem{Content=content};var label=new StackPanel{Orientation=Orientation.Horizontal};label.Children.Add(new Image{Source=(ImageSource)Palette["Icon."+icon],Width=20,Height=20,Margin=new(0,0,8,0)});label.Children.Add(Text(name));item.Header=label;Id(item,"Tab"+Pages.Items.Count,name);return item;}
    private UIElement FormPage()
    {
        var body=new StackPanel();body.Children.Add(Text("编辑普通提醒",24,true));body.Children.Add(Text("保存只记录在此样板的内存中，便于验证输入和错误状态。",14,false,"Color.textSecondary",new(0,8,0,24)));
        var grid=TwoColumns();var left=new StackPanel();var right=new StackPanel();Field(left,"提醒名称",PlanName);Field(left,"要提醒做什么",Kind);Field(left,"间隔分钟（1 至 1440）",Interval);Field(left,"提醒方式",ReminderStyle);Field(right,"明确时刻（HH:mm）",TimeInput);Field(right,"备注（可选，最多 1000 字）",Note);right.Children.Add(EnablePlan);
        var extra=new StackPanel();extra.Children.Add(new CheckBox{Content="仅周一至周五"});extra.Children.Add(new CheckBox{Content="限定每天提醒时段"});extra.Children.Add(Text("展开器、复选与主体滚动保持同一套样式。",14,false,"Color.textSecondary"));More.Content=extra;More.Margin=new(0,16,0,0);right.Children.Add(More);grid.Children.Add(left);Grid.SetColumn(right,2);grid.Children.Add(right);body.Children.Add(grid);return new ScrollViewer{Content=body};
    }
    private UIElement SelectionPage()
    {
        var body=new StackPanel();body.Children.Add(Text("下拉、日期与完整弹层",24,true));body.Children.Add(Text("点开下拉和日历，检查内部状态；点击月份标题切换到月份与年份选择。",14,false,"Color.textSecondary",new(0,8,0,24)));
        var grid=TwoColumns();var left=new StackPanel();var right=new StackPanel();Field(left,"长选项与内部滚动",LongOptions);Field(left,"无选项",EmptyOptions);
        Field(left,"开始日期（yyyy-MM-dd）",StartDate);UseEnd.Margin=new(0,0,0,8);left.Children.Add(UseEnd);Field(left,"结束日期（可选）",EndDate);var validate=new Button{Content="检查日期范围",HorizontalAlignment=HorizontalAlignment.Left};Id(validate,"ValidateDates","检查日期范围");validate.Click+=(_,_)=>ValidateDates();left.Children.Add(validate);right.Children.Add(Text("日历：今天、选中与不可选日期",18,true));StandaloneCalendar.BlackoutDates.Add(new CalendarDateRange(DateTime.Today.AddDays(2)));right.Children.Add(StandaloneCalendar);var buttons=new WrapPanel();foreach(var mode in new[]{CalendarMode.Month,CalendarMode.Year,CalendarMode.Decade}){var b=new Button{Content=mode==CalendarMode.Month?"日期":mode==CalendarMode.Year?"月份":"年份",Margin=new(0,0,8,0)};b.Click+=(_,_)=>StandaloneCalendar.DisplayMode=mode;Id(b,"Mode"+mode,b.Content.ToString()!);buttons.Children.Add(b);}right.Children.Add(buttons);right.Children.Add(Text("示例将后天设为不可选，便于检查禁用状态；不影响日期输入字段。",14,false,"Color.textSecondary",new(0,16,0,0)));grid.Children.Add(left);Grid.SetColumn(right,2);grid.Children.Add(right);body.Children.Add(grid);return new ScrollViewer{Content=body};
    }
    private UIElement StatesPage()
    {
        var body=new StackPanel();body.Children.Add(Text("状态组合与滚动",24,true));body.Children.Add(Text("使用 Tab、方向键、Space 和滚轮，检查选中、焦点和滚动的区别。",14,false,"Color.textSecondary",new(0,8,0,24)));var grid=TwoColumns();var left=new StackPanel();var right=new StackPanel();
        Field(left,"禁用",new TextBox{Text="醒目弹窗",IsEnabled=false});Field(left,"只读，可以复制",new TextBox{Text="只读值，不会因查看被清空。",IsReadOnly=true});var error=new TextBox{Text="25:70"};FieldState.SetError(error,"请输入有效时刻，例如 08:30。");Field(left,"错误状态",error);
        var counts=new WrapPanel();_countHeading=Text("互斥次数选择（当前 3 次）",14,true,margin:new(0,0,0,8));var countGroup=new StackPanel{Margin=new(0,0,0,24)};countGroup.Children.Add(_countHeading);countGroup.Children.Add(counts);for(var i=1;i<=4;i++){var count=i;var radio=new RadioButton{Content=i+" 次",GroupName="Counts",IsChecked=i==3,Margin=new(0,0,8,0)};radio.Checked+=(_,_)=>_countHeading.Text=$"互斥次数选择（当前 {count} 次）";Id(radio,"Count"+i,i+" 次");Counts.Add(radio);counts.Children.Add(radio);}left.Children.Add(countGroup);left.Children.Add(new CheckBox{Content="混合态示例",IsThreeState=true,IsChecked=null});left.Children.Add(new CheckBox{Content="禁用的已选状态",IsChecked=true,IsEnabled=false});
        var items=new StackPanel();for(var i=1;i<=30;i++){var row=new Border{Padding=new(12,10,12,10),BorderBrush=DesignTheme.Brush("#E8E4DD"),BorderThickness=new(0,0,0,1),Child=Text("示例记录 "+i+" · 可滚轮或拖动滑块")};items.Children.Add(row);}LongScroll.Content=items;Field(right,"有溢出：细滚动条",LongScroll);ShortScroll.Content=Text("内容较少，不应显示滚动条。",14,false,"Color.textSecondary");Field(right,"无溢出：不显示轨道",ShortScroll);grid.Children.Add(left);Grid.SetColumn(right,2);grid.Children.Add(right);body.Children.Add(grid);return new ScrollViewer{Content=body};
    }
    public bool ValidateDates()
    {
        var valid=StartDate.TryCommit();if(UseEnd.IsChecked==true){valid=EndDate.TryCommit()&&valid;if(valid&&EndDate.SelectedDate is {} end&&StartDate.SelectedDate is {} start&&end<start){FieldState.SetError(EndDate,"结束日期不能早于开始日期。");valid=false;}}
        Status.Text=valid?"日期有效。输入保持原值，不会创建服药计划。":"日期未通过检查，请修正对应字段。";return valid;
    }
    public bool TrySave()
    {
        FieldState.SetError(PlanName,string.IsNullOrWhiteSpace(PlanName.Text)||PlanName.Text.Length>80?"请填写 1 至 80 字的提醒名称。":"");FieldState.SetError(Interval,!int.TryParse(Interval.Text,out var minutes)||minutes<1||minutes>1440?"请输入 1 至 1440 的整数分钟。":"");FieldState.SetError(TimeInput,!DateTime.TryParseExact(TimeInput.Text,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)?"请输入有效时刻，例如 08:30。":"");FieldState.SetError(Note,Note.Text.Length>1000?"备注最多 1000 字。":"");
        if(new[]{PlanName,Interval,TimeInput,Note}.Any(FieldState.GetHasError)){Status.Text="未保存，请修正输入。已有输入未清空。";return false;}SaveCount++;LastSavedName=PlanName.Text;Status.Text=$"样板已保存 {SaveCount} 次：{PlanName.Text}。只在内存中，不创建真实提醒。";return true;
    }
    public static void Id(DependencyObject o,string id,string name){AutomationProperties.SetAutomationId(o,id);AutomationProperties.SetName(o,name);}
    public static TextBlock Text(string s,double size=14,bool bold=false,string brush="Color.textPrimary",Thickness? margin=null){var t=new TextBlock{Text=s,FontSize=size,TextWrapping=TextWrapping.Wrap,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,Margin=margin??new()};t.SetResourceReference(TextBlock.ForegroundProperty,brush);return t;}
    private static Grid TwoColumns(){var g=new Grid();g.ColumnDefinitions.Add(new());g.ColumnDefinitions.Add(new(){Width=new GridLength(32)});g.ColumnDefinitions.Add(new());return g;}
    private static void Field(Panel parent,string title,FrameworkElement input){var group=new StackPanel{Margin=new(0,0,0,24)};group.Children.Add(Text(title,14,true,margin:new(0,0,0,8)));group.Children.Add(input);var error=Text("",12,false,"Color.danger",new(0,6,0,0));var style=new Style(typeof(TextBlock),(Style)Application.Current.FindResource(typeof(TextBlock)));var empty=new Trigger{Property=TextBlock.TextProperty,Value=""};empty.Setters.Add(new Setter(VisibilityProperty,Visibility.Collapsed));style.Triggers.Add(empty);error.Style=style;error.SetBinding(TextBlock.TextProperty,new Binding{Source=input,Path=new PropertyPath("(0)",FieldState.ErrorProperty)});group.Children.Add(error);parent.Children.Add(group);}
}
