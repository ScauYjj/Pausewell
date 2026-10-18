using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Pausewell.Wireframes;

public static class Entry
{
    [STAThread]
    public static void Main(string[] args)
    {
        var app = new Application();
        var window = new ReviewWindow();
        var i = Array.IndexOf(args, "--export");
        if (i >= 0 && i + 1 < args.Length)
        {
            var directory = Path.GetFullPath(args[i + 1]);
            window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(() => { try { if (args.Contains("--round-two")) window.ExportRoundTwo(directory); else window.Export(directory); app.Shutdown(0); } catch (Exception ex) { Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "error.txt"), ex.ToString()); app.Shutdown(1); } }, DispatcherPriority.ApplicationIdle);
        }
        app.Run(window);
    }
}

enum ReviewPage { Setup, Home, Add, Reminder, Meeting, Medicine, History, Settings, FixedTimes }
enum RunMode { Running, Paused, Meeting }
sealed class SamplePlan
{
    public string Id = Guid.NewGuid().ToString("N");
    public string Name = "";
    public string Kind = "喝水";
    public int Minutes;
    public string Style = "醒目弹窗";
    public bool Enabled = true;
    public DateTime Next;
    public bool OfficeOnly;
    public TimeSpan Start = new(9, 0, 0), End = new(18, 0, 0);
    public bool Deferred;
    public bool FixedTimes;
    public List<TimeSpan> Times = [];
}

public sealed partial class ReviewWindow : Window
{
    private readonly Grid _root = new();
    private readonly ContentControl _product = new();
    private readonly StackPanel _notes = new();
    private readonly TextBlock _demoClock = new();
    private readonly List<SamplePlan> _plans = [];
    private readonly Dictionary<ReviewPage, Button> _nav = [];
    private ReviewPage _page;
    private RunMode _mode = RunMode.Running;
    private DateTime _now = new(2026, 10, 15, 9, 0, 0);
    private DateTime _modeUntil;
    private DateTime? _bufferUntil;
    private string _name = "喝水", _kind = "喝水", _minutes = "30", _style = "醒目弹窗";
    private string _start = "09:00", _end = "18:00";
    private bool _office, _submitted;
    private string _notice = "";
    private TextBlock? _summary, _error;
    private Button? _save;
    private SamplePlan? _due;
    private bool _activityStarted, _activityReady;
    private readonly TextBlock _stepLabel = new();

    public ReviewWindow()
    {
        Title = "间歇 · 低保真交互评审稿";
        Width = 1440; Height = 970; MinWidth = 1160; MinHeight = 780;
        Background = B("#F1F1F1"); Foreground = B("#242424"); FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 14;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Resources.Add(typeof(Button), Buttons());
        Resources.Add(typeof(TextBox), new Style(typeof(TextBox)) { Setters = { new Setter(Control.PaddingProperty, new Thickness(10, 8, 10, 8)), new Setter(Control.FontSizeProperty, 15.0), new Setter(Control.BorderBrushProperty, B("#999999")) } });
        _root.Margin = new Thickness(26, 20, 26, 20);
        _root.RowDefinitions.Add(new() { Height = GridLength.Auto }); _root.RowDefinitions.Add(new());
        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        toolbar.ColumnDefinitions.Add(new()); toolbar.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var left = new StackPanel(); left.Children.Add(T("交互评审 · 先看懂怎么用", 23, true));
        left.Children.Add(T("基础页结构已评审，本轮补齐其他流程。灰度与示例数据仅用于交互评审。", 12, color: "#646464", margin: new(0, 6, 0, 12)));
        var tabs = new WrapPanel();
        foreach (var pair in new[] { (ReviewPage.Setup, "01 首次设置"), (ReviewPage.Home, "02 日常首页"), (ReviewPage.Add, "03 新增提醒"), (ReviewPage.Reminder, "04 到期提醒"), (ReviewPage.Meeting, "05 会议/暂停"), (ReviewPage.Medicine, "06 服药计划"), (ReviewPage.History, "07 执行记录"), (ReviewPage.Settings, "08 设置") })
        {
            var tab = Btn(pair.Item2, () => Navigate(pair.Item1), id: "Review" + pair.Item1); tab.Margin = new Thickness(0, 0, 8, 0); _nav.Add(pair.Item1, tab); tabs.Children.Add(tab);
        }
        left.Children.Add(tabs); toolbar.Children.Add(left);
        var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(Btn("从头体验", Reset, id: "ResetReview")); right.Children.Add(T("与现有提醒软件的数据分开", 11, color: "#646464", margin: new(0, 8, 0, 0)));
        Grid.SetColumn(right, 1); toolbar.Children.Add(right); _root.Children.Add(toolbar);
        var body = new Grid(); body.ColumnDefinitions.Add(new()); body.ColumnDefinitions.Add(new() { Width = new GridLength(24) }); body.ColumnDefinitions.Add(new() { Width = new GridLength(270) }); Grid.SetRow(body, 1); _root.Children.Add(body);
        var frame = new Border { Background = Brushes.White, BorderBrush = B("#999999"), BorderThickness = new Thickness(1), Child = _product }; body.Children.Add(frame);
        var noteScroll = new ScrollViewer { Content = _notes, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetColumn(noteScroll, 2); body.Children.Add(noteScroll);
        Content = _root;
        Navigate(ReviewPage.Reminder);
    }

    private static SolidColorBrush B(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    private static TextBlock T(string text, double size = 14, bool bold = false, string color = "#242424", Thickness? margin = null) => new() { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Foreground = B(color), Margin = margin ?? new() };
    private static Style Buttons() => new(typeof(Button))
    {
        Setters =
        {
            new Setter(Control.BackgroundProperty, Brushes.White), new Setter(Control.ForegroundProperty, B("#242424")),
            new Setter(Control.BorderBrushProperty, B("#999999")), new Setter(Control.PaddingProperty, new Thickness(13, 8, 13, 8)),
            new Setter(Control.FontSizeProperty, 13.0), new Setter(FrameworkElement.MinHeightProperty, 36.0),
            new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center)
        }
    };
    private Button Btn(string text, Action action, bool primary = false, string? id = null)
    {
        var button = new Button { Content = text };
        if (primary) { button.Background = B("#303030"); button.Foreground = Brushes.White; button.FontWeight = FontWeights.SemiBold; }
        if (id != null) AutomationProperties.SetAutomationId(button, id);
        button.Click += (_, _) => action(); return button;
    }
    private static Border Box(UIElement child, string color = "#FFFFFF", Thickness? padding = null) => new() { Child = child, Background = B(color), BorderBrush = B("#C5C5C5"), BorderThickness = new Thickness(1), Padding = padding ?? new Thickness(18) };
    private static void Gap(Panel parent, double h = 14) => parent.Children.Add(new Border { Height = h });

    private void Reset()
    {
        _plans.Clear(); ResetExtended(); _now = new(2026, 10, 15, 9, 0, 0); _mode = RunMode.Running; _bufferUntil = null; _notice = ""; _due = null; Navigate(ReviewPage.Setup);
    }
    private void Seed()
    {
        if (_plans.Count > 0) return;
        _plans.Add(new() { Name = "喝水", Kind = "喝水", Minutes = 30, Next = _now.AddMinutes(30) });
        _plans.Add(new() { Name = "起来活动", Kind = "活动", Minutes = 45, Next = _now.AddMinutes(45) });
    }
    private void Navigate(ReviewPage page)
    {
        _due = null; _notice = ""; _page = page;
        _editingPlan = null;
        if (page == ReviewPage.Setup) { _name = "喝水"; _kind = "喝水"; _minutes = "30"; _style = "醒目弹窗"; _office = false; _submitted = false; }
        if (page == ReviewPage.Home) Seed();
        if (page == ReviewPage.Add) { Seed(); _name = ""; _kind = "自定义"; _minutes = "30"; _style = "醒目弹窗"; _office = _settings.NewOffice; _submitted = false; }
        PrepareExtra(page);
        Render();
    }
    private void Render()
    {
        foreach (var pair in _nav) { pair.Value.Background = pair.Key == _page ? B("#303030") : Brushes.White; pair.Value.Foreground = pair.Key == _page ? Brushes.White : B("#242424"); }
        _notes.Children.Clear();
        _notes.Children.Add(T("本页评审重点", 16, true)); Gap(_notes, 16);
        var points = _page switch
        {
            ReviewPage.Setup => new[] { "只建立第一条提醒，不预先塞入已启用的示例计划。", "基本设置只回答：做什么、隔多久、怎样提醒。", "按钮直接写出结果：保存并开始提醒。", "先选择一个普通事项走通流程；服药另设专门流程。" },
            ReviewPage.Home => new[] { "先看当前是否在提醒，再看下一次时间。", "会议和暂停会显示结束时间；操作后立即反馈。", "每条提醒显示自己的时间和开关，避免把计划与全局状态混在一起。", "再次打开默认恢复，无需每天找开始按钮。" },
            ReviewPage.Add => new[] { "同一个表单，保存文案随当前运行状态改变。", "运行中：保存后启用新增事项。", "暂停中：保存后仍暂停，并解释恢复后的首次时间。", "会议中：保存不打断会议，明确到期与会后呈现的区别。" },
            _ => ExtraPoints()
        };
        foreach (var (point, index) in points.Select((p, i) => (p, i))) _notes.Children.Add(T($"{index + 1}. {point}", 13, margin: new(0, 0, 0, 15)));
        Gap(_notes, 8); _notes.Children.Add(new Border { Height = 1, Background = B("#BBBBBB") }); Gap(_notes, 15);
        _notes.Children.Add(T("演示控制", 14, true));
        _demoClock.Text = $"示例时间：10 月 15 日 {_now:HH:mm:ss}"; _demoClock.FontSize = 12; _demoClock.Foreground = B("#646464"); _demoClock.Margin = new(0, 10, 0, 12); _notes.Children.Add(_demoClock);
        if (_page is ReviewPage.Home or ReviewPage.Add)
        {
            AddNoteAction("查看：提醒中", () => SetMode(RunMode.Running));
            AddNoteAction("查看：会议至 10:00", () => SetMode(RunMode.Meeting));
            AddNoteAction("查看：暂停至 09:30", () => SetMode(RunMode.Paused));
        }
        AddExtraControls();
        if (_page == ReviewPage.Home)
        {
            AddNoteAction(_mode == RunMode.Meeting ? "模拟会议结束并汇总待办" : _mode == RunMode.Paused ? "模拟暂停到期自动恢复" : "模拟下一次提醒到期", SimulateDue, "SimulateDue");
            if (_due != null) { AddNoteAction("推进示例时间 2 分钟", () => { _now = _now.AddMinutes(2); Render(); }); if (_due.Kind == "活动") AddNoteAction("模拟活动计时结束", () => { _activityReady = true; Render(); }); }
            AddNoteAction("模拟关闭后重新打开", () => { _notice = _mode == RunMode.Running ? "已自动恢复你的提醒，无需再次开始。" : "已恢复保存状态；当前仍遵守你设定的会议或暂停期限。"; Render(); });
        }
        Gap(_notes, 18); _notes.Children.Add(T("评审范围", 13, true)); _notes.Children.Add(T("本轮新增到期处理、会议与暂停、服药计划及记录、执行历史和设置。基础页结构沿用上一轮。", 12, color: "#646464", margin: new(0, 8, 0, 10)));
        _notes.Children.Add(T("界面里不会执行真实计时、全屏覆盖、服药确认或后台提醒。", 12, color: "#646464"));
        _product.Content = _page switch { ReviewPage.Home => Home(), ReviewPage.Setup or ReviewPage.Add => Composer(), _ => ExtraPage() };
    }
    private void AddNoteAction(string text, Action action, string? id = null)
    {
        var b = Btn(text, action, id: id); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Margin = new Thickness(0, 0, 0, 7); _notes.Children.Add(b);
    }
    private UIElement ProductHeader()
    {
        var h = new DockPanel();
        if (_page != ReviewPage.Setup)
        {
            var links = new StackPanel { Orientation = Orientation.Horizontal };
            foreach(var item in new[] { ("提醒", ReviewPage.Home), ("服药", ReviewPage.Medicine), ("记录", ReviewPage.History), ("设置", ReviewPage.Settings) })
            { var b = Btn(item.Item1, () => Navigate(item.Item2), id: "Product"+item.Item2); b.Margin = new Thickness(6,0,0,0); b.Padding = new Thickness(10,4,10,4); links.Children.Add(b); }
            DockPanel.SetDock(links,Dock.Right); h.Children.Add(links);
        }
        h.Children.Add(T("间歇", 20, true)); h.Children.Add(T("  /  个人提醒", 12, color: "#646464", margin: new(8, 7, 0, 0)));
        return new Border { Child = h, Padding = new Thickness(26, 17, 26, 17), BorderBrush = B("#D0D0D0"), BorderThickness = new Thickness(0, 0, 0, 1) };
    }
    private UIElement Composer()
    {
        var first = _page == ReviewPage.Setup;
        var root = new DockPanel(); var header = ProductHeader(); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var footer = new StackPanel { Margin = new Thickness(28, 12, 28, 20) };
        _summary = T("", 13); footer.Children.Add(Box(_summary, "#F4F4F4", new Thickness(14)));
        _error = T("", 12, color: "#333333", margin: new(0, 7, 0, 0)); _error.Visibility = Visibility.Collapsed; footer.Children.Add(_error);
        var actions = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        _save = Btn("", SaveComposer, true, "SaveReminder"); _save.MinWidth = 170; DockPanel.SetDock(_save, Dock.Right); actions.Children.Add(_save);
        if (!first) { var cancel = Btn("取消", () => { _page = ReviewPage.Home; Render(); }, id: "CancelComposer"); cancel.HorizontalAlignment = HorizontalAlignment.Left; actions.Children.Add(cancel); }
        else actions.Children.Add(T("以后打开软件会自动恢复提醒。", 12, color: "#646464", margin: new(0, 9, 8, 0)));
        footer.Children.Add(actions); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var body = new StackPanel { Margin = new Thickness(28, 22, 28, 0) };
        body.Children.Add(T(first ? "先设置一件要提醒的事" : _editingPlan != null ? "编辑提醒" : "新增提醒", 25, true));
        body.Children.Add(T(first ? "设好后就可以放在后台，到时提醒你行动。" : ModeText() + "。新增事项不会改变其他提醒的计时。", 13, color: "#646464", margin: new(0, 8, 0, 18)));
        body.Children.Add(T("提醒我做什么", 14, true));
        var kinds = new WrapPanel { Margin = new Thickness(0, 9, 0, 8) };
        foreach (var kind in new[] { "喝水", "活动", "如厕", "自定义" })
        {
            var choice = Btn(kind, () => { _kind = kind; _name = kind switch { "活动" => "起来活动", "如厕" => "检查是否需要如厕", "自定义" => "", _ => "喝水" }; Render(); }, kind == _kind);
            choice.Margin = new Thickness(0, 0, 8, 0); kinds.Children.Add(choice);
        }
        body.Children.Add(kinds);
        var name = new TextBox { Text = _name, MaxLength = 80 }; AutomationProperties.SetName(name, "提醒名称"); AutomationProperties.SetAutomationId(name, "ReminderName");
        name.TextChanged += (_, _) => { _name = name.Text; Summary(); }; body.Children.Add(name); Gap(body, 18);
        body.Children.Add(T("每次完成后，隔多久再提醒", 14, true));
        var frequency = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 9, 0, 0) };
        var interval = new TextBox { Text = _minutes, Width = 105, MaxLength = 4 }; AutomationProperties.SetName(interval, "完成后间隔分钟"); AutomationProperties.SetAutomationId(interval, "IntervalMinutes");
        interval.TextChanged += (_, _) => { _minutes = interval.Text; Summary(); }; frequency.Children.Add(interval); frequency.Children.Add(T("分钟", 14, margin: new(12, 8, 0, 0))); body.Children.Add(frequency);
        body.Children.Add(T("首次从开始时计算；以后从你确认完成的时刻重新计算。", 12, color: "#646464", margin: new(0, 8, 0, 18)));
        body.Children.Add(T("怎样提醒我", 14, true));
        var style = new ComboBox { ItemsSource = new[] { "醒目弹窗", "轻提醒", "全屏提醒" }, SelectedItem = _style, Padding = new Thickness(8), Margin = new Thickness(0, 9, 0, 5), MinHeight = 36 };
        AutomationProperties.SetName(style, "提醒方式"); body.Children.Add(style);
        string StyleHelp() => _style == "全屏提醒" ? "到时覆盖工作界面，保留延期和紧急退出。" : _style == "轻提醒" ? "轻量提示，暂不覆盖工作界面。" : "到时弹出清楚的提醒，需要你明确处理。";
        var styleHelp = T(StyleHelp(), 12, color: "#646464"); body.Children.Add(styleHelp);
        style.SelectionChanged += (_, _) => { _style = style.SelectedItem?.ToString() ?? "醒目弹窗"; styleHelp.Text = StyleHelp(); Summary(); };
        var advanced = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        var office = new CheckBox { Content = "仅周一至周五的指定时段", IsChecked = _office, Margin = new Thickness(0, 0, 0, 10) };
        office.Click += (_, _) => { _office = office.IsChecked == true; Summary(); }; advanced.Children.Add(office);
        var hours = new StackPanel { Orientation = Orientation.Horizontal };
        var start = new TextBox { Text = _start, Width = 88 }; var end = new TextBox { Text = _end, Width = 88 };
        AutomationProperties.SetName(start, "有效时段开始"); AutomationProperties.SetName(end, "有效时段结束");
        start.TextChanged += (_, _) => { _start = start.Text; Summary(); }; end.TextChanged += (_, _) => { _end = end.Text; Summary(); };
        hours.Children.Add(start); hours.Children.Add(T("至", 13, margin: new(12, 8, 12, 0))); hours.Children.Add(end); advanced.Children.Add(hours);
        advanced.Children.Add(T("本轮先评审普通提醒。药物使用独立流程，不继承工作时段。", 12, color: "#646464", margin: new(0, 10, 0, 0)));
        advanced.Children.Add(Btn("改为每天固定时刻", OpenFixedOrdinary, id: "UseFixedTimes"));
        body.Children.Add(new Expander { Header = "更多设置：生效时段", Content = advanced, Margin = new Thickness(0, 17, 0, 8) });
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; root.Children.Add(scroll); Summary(); return root;
    }

    private string ModeText() => _mode switch { RunMode.Meeting => $"会议中，{_modeUntil:HH:mm} 结束", RunMode.Paused => $"{(_pauseMedicines?"所有提醒":"普通提醒")}已暂停，{_modeUntil:HH:mm} 恢复", _ => "提醒中" };
    private bool Valid(out int minutes, out string message)
    {
        message = ""; minutes = 0;
        if (string.IsNullOrWhiteSpace(_name)) { message = "请填写提醒名称。"; return false; }
        if (!int.TryParse(_minutes, out minutes) || minutes is < 1 or > 1440) { message = "请填写 1 至 1440 的整数分钟。"; return false; }
        if (_office && (!TimeSpan.TryParseExact(_start, @"hh\:mm", CultureInfo.InvariantCulture, out var start) || !TimeSpan.TryParseExact(_end, @"hh\:mm", CultureInfo.InvariantCulture, out var end) || start >= end || end >= TimeSpan.FromDays(1))) { message = "有效时段请按 HH:mm 填写，结束时间需晚于开始。"; return false; }
        return true;
    }
    private DateTime Eligible(SamplePlan p, DateTime at)
    {
        if (!p.OfficeOnly) return at;
        for (var i = 0; i < 9; i++)
        {
            if (at.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) { at = at.Date.AddDays(1).Add(p.Start); continue; }
            if (at.TimeOfDay < p.Start) return at.Date.Add(p.Start);
            if (at.TimeOfDay >= p.End) { at = at.Date.AddDays(1).Add(p.Start); continue; }
            return at;
        }
        return at;
    }
    private SamplePlan Draft(int minutes) => new() { Name = _name.Trim(), Kind = _kind, Minutes = minutes, Style = _style, OfficeOnly = _office, Start = _office ? TimeSpan.Parse(_start, CultureInfo.InvariantCulture) : new(9, 0, 0), End = _office ? TimeSpan.Parse(_end, CultureInfo.InvariantCulture) : new(18, 0, 0) };
    private void Summary()
    {
        if (_summary == null || _save == null || _error == null) return;
        _save.Content = _editingPlan != null ? "保存修改" : _page == ReviewPage.Setup ? "保存并开始提醒" : _mode == RunMode.Paused ? "保存，保持暂停" : _mode == RunMode.Meeting ? "保存，保持会议模式" : "保存并启用此提醒";
        if (!Valid(out var minutes, out var message))
        {
            _summary.Text = "填写名称和间隔后，这里会说明首次何时提醒。";
            _error.Text = _submitted ? message : ""; _error.Visibility = _submitted ? Visibility.Visible : Visibility.Collapsed; return;
        }
        _error.Visibility = Visibility.Collapsed;
        var plan = Draft(minutes); var first = Eligible(plan, _now.AddMinutes(minutes));
        var window = _office ? $"仅周一至周五 {_start} 至 {_end} 生效。" : "每天生效，不限时段。";
        if (_page != ReviewPage.Setup && _mode == RunMode.Paused)
            _summary.Text = $"目前已暂停至 {_modeUntil:HH:mm}。保存不会恢复全部提醒。\n恢复后再按 {minutes} 分钟安排首次提醒；{window}";
        else if (_page != ReviewPage.Setup && _mode == RunMode.Meeting)
            _summary.Text = $"首次到期 {first:MM-dd HH:mm}。会议至 {_modeUntil:HH:mm}，保存不会打断会议。\n到期若仍在开会，将在会后汇总提醒。每次完成后再等 {minutes} 分钟。";
        else _summary.Text = $"现在保存并开始，首次提醒：{first:MM-dd HH:mm}。\n以后每次确认完成，再等 {minutes} 分钟。{window}";
    }
    private void SaveComposer()
    {
        _submitted = true;
        if (!Valid(out var minutes, out _)) { Summary(); return; }
        var firstSetup = _page == ReviewPage.Setup;
        if (firstSetup) { _plans.Clear(); _mode = RunMode.Running; _bufferUntil = null; }
        var plan = Draft(minutes); plan.Next = Eligible(plan, (_mode == RunMode.Paused ? _modeUntil : _now).AddMinutes(minutes));
        if (_editingPlan != null) { var index = _plans.IndexOf(_editingPlan); plan.Id = _editingPlan.Id; plan.Enabled=_editingPlan.Enabled; _plans[index] = plan; }
        else _plans.Add(plan);
        _editingPlan = null;
        _notice = _mode switch { RunMode.Paused => $"已保存“{plan.Name}”，仍暂停至 {_modeUntil:HH:mm}；首次安排在恢复后的有效时段。", RunMode.Meeting => $"已保存“{plan.Name}”。仍保持会议模式，到期事项会在会后汇总。", _ => $"已开始“{plan.Name}”提醒，首次 {plan.Next:HH:mm}。每次完成后再等 {minutes} 分钟。" };
        if(!plan.Enabled)_notice=$"已保存“{plan.Name}”的修改，此提醒仍停用。其他计划未改变。";
        _page = ReviewPage.Home; Render();
    }

    private UIElement Home()
    {
        var layer = new Grid(); var root = new DockPanel(); layer.Children.Add(root); var header = ProductHeader(); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var bottom = new Border { Padding = new Thickness(26, 12, 26, 12), BorderBrush = B("#D0D0D0"), BorderThickness = new Thickness(0, 1, 0, 0), Child = T("关闭窗口后继续在托盘运行；退出应用后不再提醒。", 12, color: "#646464") }; DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        var body = new StackPanel { Margin = new Thickness(26, 22, 26, 24) };
        var top = new Grid(); top.ColumnDefinitions.Add(new()); top.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var status = new StackPanel(); status.Children.Add(T(ModeText(), 25, true));
        status.Children.Add(T(_mode == RunMode.Running ? $"当前有 {_plans.Count(p => p.Enabled)} 条普通提醒在运行。" : _mode == RunMode.Meeting ? "普通提醒到期后保留，会议结束后汇总。" : "普通间隔恢复后重新计时。"+(_pauseMedicines?"服药提示本次也暂停。":"服药仍按原时刻提示。"), 13, color: "#646464", margin: new(0, 8, 0, 0))); top.Children.Add(status);
        var modeActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        if (_mode == RunMode.Running)
        {
            var meeting = Btn("我要开会", () => ChooseDuration(RunMode.Meeting), id: "BeginMeeting"); meeting.Margin = new Thickness(0, 0, 8, 0); modeActions.Children.Add(meeting);
            modeActions.Children.Add(Btn("暂停普通提醒", () => ChooseDuration(RunMode.Paused), id: "PauseReminders"));
        }
        else modeActions.Children.Add(Btn(_mode == RunMode.Meeting ? "结束会议" : "恢复提醒", Restore, id: "RestoreReminders"));
        Grid.SetColumn(modeActions, 1); top.Children.Add(modeActions); body.Children.Add(top); Gap(body, 18);
        if (_notice.Length > 0) { body.Children.Add(Box(T(_notice, 13), "#F4F4F4", new Thickness(12))); Gap(body, 16); }
        var next = _plans.Where(p => p.Enabled).OrderBy(p => p.Next).FirstOrDefault();
        var nextPanel = new StackPanel(); nextPanel.Children.Add(T(_mode == RunMode.Paused ? "恢复后的普通提醒" : next?.Next<=_now?"当前待处理":"下一次普通提醒", 13, color: "#646464"));
        if (next == null) { nextPanel.Children.Add(T("还没有开启的提醒", 22, true, margin: new(0, 10, 0, 8))); nextPanel.Children.Add(T("添加一条提醒，或开启下方已有事项。", 13)); }
        else
        {
            nextPanel.Children.Add(T(_mode == RunMode.Paused ? $"{next.Name} · "+(next.FixedTimes?"恢复后按未来固定时刻继续":"恢复后重新计时") : $"{next.Name} · {next.Next:HH:mm}", 24, true, margin: new(0, 10, 0, 8)));
            nextPanel.Children.Add(T(_mode == RunMode.Paused ? $"{_modeUntil:HH:mm} 自动恢复。" : _mode == RunMode.Meeting && next.Next <= _modeUntil ? $"原计划 {next.Next:HH:mm} 到期，{_modeUntil:HH:mm} 会后收尾 30 秒，再汇总提醒。" : next.Deferred ? "这是本次事项的延期，不算完成。" : next.Next<=_now?"此项尚未处理，确认后再安排下一次。":$"还有 {Math.Max(0, (int)Math.Ceiling((next.Next - _now).TotalMinutes))} 分钟。"+(next.FixedTimes?"各固定时刻独立。":$"完成后再等 {next.Minutes} 分钟。"), 13, color: "#646464"));
            if(_mode==RunMode.Running&&next.Next<=_now)nextPanel.Children.Add(Btn("处理这条提醒",()=>{_focusedRecord=GetRecord(next.Id,next.Name,next.Kind,false,next.Next);_page=ReviewPage.Reminder;_alertKind=next.Kind;_returnToQueue=false;_recordOperation="";Render();}));
        }
        body.Children.Add(Box(nextPanel)); Gap(body, 23);
        var listTitle = new DockPanel(); var add = Btn("新增提醒", () => Navigate(ReviewPage.Add), true, "AddReminder"); DockPanel.SetDock(add, Dock.Right); listTitle.Children.Add(add); listTitle.Children.Add(T("我的提醒", 18, true, margin: new(0, 7, 0, 0))); body.Children.Add(listTitle); Gap(body, 12);
        var list = new StackPanel();
        var headings = RowGrid(); headings.Children.Add(T("事项 / 循环规则", 12, color: "#646464")); var h1 = T("下次提醒", 12, color: "#646464"); Grid.SetColumn(h1, 1); headings.Children.Add(h1); var h2 = T("开关", 12, color: "#646464"); Grid.SetColumn(h2, 2); headings.Children.Add(h2); list.Children.Add(headings);
        foreach (var p in _plans)
        {
            var row = RowGrid(); var name = new StackPanel(); name.Children.Add(T(p.Name, 15, true)); name.Children.Add(T((p.FixedTimes ? "每天 "+string.Join(" / ",p.Times.Select(t=>t.ToString(@"hh\:mm"))) : $"确认完成后 {p.Minutes} 分钟") + " · " + p.Style, 12, color: "#646464", margin: new(0, 5, 0, 0))); row.Children.Add(name);
            var due = T(!p.Enabled ? "已停用" : _mode == RunMode.Paused ? "暂停中" : _mode == RunMode.Meeting && p.Next <= _modeUntil ? $"{p.Next:HH:mm} 到期\n会后提醒" : p.Deferred ? $"{p.Next:HH:mm} 再提醒\n本次已延期" : p.Next.ToString("HH:mm"), 13); Grid.SetColumn(due, 1); row.Children.Add(due);
            var on = new CheckBox { Content = p.Enabled ? "开启" : "停用", IsChecked = p.Enabled, VerticalAlignment = VerticalAlignment.Center }; AutomationProperties.SetName(on, p.Name + "提醒开关");
            on.Click += (_, _) => { p.Enabled = on.IsChecked == true; if (p.Enabled) p.Next = p.FixedTimes ? NextFixed(p.Times,_now) : Eligible(p, (_mode == RunMode.Paused ? _modeUntil : _now).AddMinutes(p.Minutes)); _notice = $"已{(p.Enabled ? "开启" : "停用")}“{p.Name}”。其他提醒未改变。"; Render(); };
            var tools = new StackPanel { Orientation = Orientation.Horizontal }; tools.Children.Add(on); var edit=Btn("编辑",()=>EditOrdinary(p)); edit.Margin=new Thickness(8,0,0,0); edit.Padding=new Thickness(8,4,8,4); tools.Children.Add(edit); Grid.SetColumn(tools, 2); row.Children.Add(tools);
            list.Children.Add(new Border { Child = row, BorderBrush = B("#DDDDDD"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 15, 0, 15) });
        }
        body.Children.Add(list);
        if(_medicines.Count>0){Gap(body,18);var n=_records.Count(r=>r.Medication&&r.Status=="待核实");body.Children.Add(Btn($"服药计划 · {_medicines.Count(m=>m.Enabled)} 项开启"+(n>0?$" · {n} 条待核实":""),()=>Navigate(ReviewPage.Medicine)));}
        root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        if (_due != null) layer.Children.Add(ReminderOverlay(_due));
        return layer;
    }
    private static Grid RowGrid() { var g = new Grid { Margin = new Thickness(0, 4, 0, 4) }; g.ColumnDefinitions.Add(new()); g.ColumnDefinitions.Add(new() { Width = new GridLength(145) }); g.ColumnDefinitions.Add(new() { Width = new GridLength(150) }); return g; }
    private void ChooseDuration(RunMode mode)
    {
        OpenModeEditor(mode);
    }
    private void SetMode(RunMode mode)
    {
        _due = null; _mode = mode; _bufferUntil = null;
        _modeUntil = _now.Date.AddHours(mode == RunMode.Meeting ? 10 : 9.5);
        if (_modeUntil <= _now) _modeUntil = _now.AddMinutes(30);
        _notice = ""; Render();
    }
    private void Restore()
    {
        var paused = _mode == RunMode.Paused; _mode = RunMode.Running;
        if (paused) foreach (var p in _plans.Where(p => p.Enabled && !p.FixedTimes)) { p.Next = Eligible(p, _now.AddMinutes(p.Minutes)); p.Deferred = false; }
        RestoreMedicineNotice();
        _bufferUntil = paused ? null : _now.AddSeconds(30);
        _notice = paused ? "已恢复提醒，普通间隔从现在重新计算。" : "已结束会议，保留 30 秒收尾时间，再汇总已到期事项。"; Render();
    }
    private void SimulateDue()
    {
        if (_mode == RunMode.Paused) { _now = _modeUntil; Restore(); return; }
        if (_mode == RunMode.Meeting) { _now = _modeUntil; Restore(); _now = _now.AddSeconds(30); }
        var next = _plans.Where(p => p.Enabled).OrderBy(p => p.Next).FirstOrDefault();
        if (next == null) { _notice = "没有开启的提醒，请先添加或开启。"; Render(); return; }
        if (next.Next > _now) _now = next.Next;
        if (_bufferUntil > _now) _now = _bufferUntil.Value;
        _due = next; _activityStarted = _activityReady = false; Render();
    }
    private UIElement ReminderOverlay(SamplePlan p)
    {
        var cover = new Grid { Background = new SolidColorBrush(Color.FromArgb(105, 0, 0, 0)) };
        var panel = new StackPanel(); panel.Children.Add(T("该留一点时间给自己了", 13, color: "#646464")); panel.Children.Add(T(p.Name, 26, true, margin: new(0, 10, 0, 10)));
        panel.Children.Add(T($"本次原计划 {p.Next:HH:mm}。确认完成后，再等 {p.Minutes} 分钟。", 13)); Gap(panel, 20);
        var actions = new WrapPanel();
        if (p.Kind == "活动") { var start = Btn(_activityStarted ? "活动计时中" : "开始活动", () => { _activityStarted = true; Render(); }); start.IsEnabled = !_activityStarted; start.Margin = new Thickness(0, 0, 8, 8); actions.Children.Add(start); }
        var complete = Btn(p.Kind == "喝水" ? "确认已喝水" : "确认已完成", () => Complete(p), true, "ConfirmDone"); complete.Margin = new Thickness(0, 0, 8, 8); complete.IsEnabled = p.Kind != "活动" || (_activityStarted && _activityReady); actions.Children.Add(complete);
        var defer = Btn("5 分钟后提醒", () => { TrackOrdinary(p,"已延期"); p.Next = _now.AddMinutes(5); p.Deferred = true; _due = null; _notice = $"已延期，{p.Next:HH:mm} 再次提醒；未记录完成。"; Render(); }, id: "DeferReminder"); defer.Margin = new Thickness(0, 0, 0, 8); actions.Children.Add(defer); panel.Children.Add(actions);
        if (p.Kind == "如厕") panel.Children.Add(Btn("当前无需要", () => { TrackOrdinary(p,"当前无需要"); p.Next = Eligible(p, _now.AddMinutes(p.Minutes)); _due = null; _notice = $"已记录当前无需要，下次 {p.Next:HH:mm}。"; Render(); }));
        panel.Children.Add(T("暂时无法执行时，可以延期。关闭提醒不等于完成。", 12, color: "#646464", margin: new(0, 12, 0, 0)));
        var card = Box(panel, padding: new Thickness(26)); card.Width = 520; card.HorizontalAlignment = HorizontalAlignment.Center; card.VerticalAlignment = VerticalAlignment.Center; cover.Children.Add(card); return cover;
    }
    private void Complete(SamplePlan p)
    {
        TrackOrdinary(p,"已完成"); p.Next = p.FixedTimes ? NextFixed(p.Times,_now) : Eligible(p, _now.AddMinutes(p.Minutes)); p.Deferred = false; _due = null; _notice = $"已记录“{p.Name}”完成（{_now:HH:mm}）。下次 {p.Next:MM-dd HH:mm}"+(p.FixedTimes ? "，固定时刻未平移。" : "，从本次完成重新计时。"); Render();
    }

    private void Capture(string path)
    {
        UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth), (int)Math.Ceiling(ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var drawing = background.RenderOpen()) drawing.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));
        bitmap.Render(background); bitmap.Render(this); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    public void Export(string directory)
    {
        Directory.CreateDirectory(directory); var checks = new List<object>();
        void Check(string name, bool condition) { checks.Add(new { name, passed = condition }); if (!condition) throw new Exception(name); }
        void Snapshot(string file) { UpdateLayout(); Capture(Path.Combine(directory, file)); }
        Reset(); Snapshot("01-first-setup.png"); Check("首次打开不提前报错", _error?.Visibility == Visibility.Collapsed);
        SaveComposer(); Check("首次保存只建立一条且直接开始", _plans.Count == 1 && _page == ReviewPage.Home && _mode == RunMode.Running);
        Snapshot("02-daily.png");
        var original = _plans[0].Next; Navigate(ReviewPage.Add); _name = "午间喝水"; _kind = "喝水"; Render(); Snapshot("03-new-running.png");
        _name = ""; SaveComposer(); Check("提交无效表单才显示错误", _plans.Count == 1 && _error?.Visibility == Visibility.Visible);
        _name = "起来活动"; _kind = "活动"; _minutes = "45"; SaveComposer(); Check("新增不改变其他计划计时", _plans.Count == 2 && _plans[0].Next == original);
        _now = new(2026, 10, 15, 9, 0, 0); SetMode(RunMode.Paused); Navigate(ReviewPage.Add); _name = "检查是否需要如厕"; _kind = "如厕"; _minutes = "60"; Render(); Snapshot("04-new-paused.png");
        Check("暂停时保存按钮说明保持暂停", _save?.Content?.ToString() == "保存，保持暂停"); SaveComposer(); Check("暂停保存不恢复其他提醒", _mode == RunMode.Paused && _plans.Count == 3);
        SetMode(RunMode.Meeting); Navigate(ReviewPage.Add); _name = "放松肩颈"; _kind = "活动"; _minutes = "30"; Render(); Snapshot("05-new-meeting.png"); SaveComposer(); Check("会议保存不结束会议", _mode == RunMode.Meeting);
        _plans.Clear(); _now = new(2026, 10, 15, 9, 0, 0); _plans.Add(new() { Name = "喝水", Kind = "喝水", Minutes = 30, Next = _now.AddMinutes(30) }); SetMode(RunMode.Running); _page = ReviewPage.Home; SimulateDue(); Snapshot("06-reminder.png");
        Check("可以演示到期处理", _due != null); var due = _due!; _now = _now.AddMinutes(2); var confirmed = _now; Complete(due);
        Check("完成后显示新的确切时刻", due.Next == confirmed.AddMinutes(due.Minutes)); Snapshot("07-after-completion.png");
        _mode = RunMode.Paused; _modeUntil = _now.AddMinutes(15); _page = ReviewPage.Home; Render(); Snapshot("08-home-paused.png");
        SimulateDue(); Check("暂停结束自动恢复且重新计时", _mode == RunMode.Running && _plans.All(p => p.Next > _now));
        _mode = RunMode.Meeting; _modeUntil = _now.AddMinutes(30); Render(); Snapshot("09-home-meeting.png");
        File.WriteAllText(Path.Combine(directory, "review-checks.json"), JsonSerializer.Serialize(new { success = true, checkedAt = DateTimeOffset.UtcNow, checks, scope = "交互评审稿的页面状态检查，不是正式提醒软件的功能验收。" }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
