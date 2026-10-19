using System.ComponentModel;
using System.Windows.Threading;
using Microsoft.Win32;
using Pausewell.Storage;
using Forms = System.Windows.Forms;

namespace Pausewell.Desktop;

public sealed class DesktopController : IDisposable
{
    public ReminderEngine Engine { get; }
    public MainWindow Main { get; }
    public string DataDirectory { get; }
    public SqliteStateStore Store { get; }
    public bool Quitting { get; private set; }
    public bool IsIsolated { get; }
    public LocalDiagnostics Diagnostics {get;}
    public IReadOnlyList<ReminderWindow> OpenReminders => _reminders;
    private readonly List<ReminderWindow> _reminders = [];
    private readonly Forms.NotifyIcon _tray;
    private readonly System.Drawing.Icon _trayIcon;
    private readonly DispatcherTimer _timer;
    private bool _sessionLocked,_suspended,_faulted;
    private bool _locked=>_sessionLocked||_suspended||Engine.State.IsAway;
    private DateTimeOffset _nextDiagnosticSample=DateTimeOffset.MinValue;
    private bool _diagnosticStarted;
    private int _timeZoneTicks;
    public bool SchedulingFaulted=>_faulted;
    public string SchedulingFailure {get;private set;}="";
    private QueueWindow? _queue;
    public QueueWindow? CurrentQueue=>_queue;

    public DesktopController(ReminderEngine engine, SqliteStateStore store, string dataDirectory, bool isolated = false,bool diagnostics=false)
    {
        Engine = engine; Store = store; DataDirectory = dataDirectory; IsIsolated = isolated;
        Diagnostics=new LocalDiagnostics(Path.Combine(dataDirectory,"diagnostics"),diagnostics);
        Engine.ResumeOnLaunch(DateTimeOffset.UtcNow); Theme.Apply(Engine.State.Preferences.Theme);
        Main = new MainWindow(this);
        Main.Loaded+=(_,_)=>{if(!_diagnosticStarted){_diagnosticStarted=true;Diagnostics.Start(Main);}};
        _trayIcon = MakeIcon();
        _tray = new Forms.NotifyIcon { Text = "间歇 · 提醒已恢复", Icon = _trayIcon, Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开间歇", null, (_, _) => Dispatch(ShowMain));
        menu.Items.Add("离开 / 返回", null, (_, _) => Dispatch(ToggleAway));
        menu.Items.Add("会议 30 分钟", null, (_, _) => Dispatch(() => Meeting(30)));
        menu.Items.Add("结束会议", null, (_, _) => Dispatch(EndMeeting));
        menu.Items.Add("退出应用", null, (_, _) => Dispatch(() => Quit()));
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => Dispatch(ShowMain);
        _tray.BalloonTipClicked += (_, _) => Dispatch(ShowMain);
        SystemEvents.SessionSwitch += SessionChanged;
        SystemEvents.PowerModeChanged += PowerChanged;
        SystemEvents.TimeChanged += TimeChanged;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private static System.Drawing.Icon MakeIcon()
    {
        using var bitmap = new System.Drawing.Bitmap(32, 32);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(System.Drawing.Color.Transparent);
        using var green = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(52, 52, 52));
        using var light = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(243, 238, 229));
        graphics.FillEllipse(green, 0, 0, 31, 31); graphics.FillRectangle(light, 10, 8, 4, 16); graphics.FillRectangle(light, 18, 8, 4, 16);
        var handle = bitmap.GetHicon();
        using var temporary = System.Drawing.Icon.FromHandle(handle);
        var icon = (System.Drawing.Icon)temporary.Clone();
        NativeWindows.DestroyIcon(handle);
        return icon;
    }

    private void Dispatch(Action action) => Main.Dispatcher.BeginInvoke(action);
    public void ShowMain() { Main.Show(); Main.WindowState = WindowState.Normal; Main.Activate(); Main.RefreshIfChanged(); Main.RefreshClock(); }
    public void Run(Action action)
    {
        try { action(); Main.ClearError(); Main.RefreshIfChanged(); Main.RefreshClock(); }
        catch (Exception ex) { Main.ReportError(TaskUi.Failure(ex)); }
    }

    public void ToggleRunning() => Run(() =>
    {
        Engine.SetRunning(!Engine.State.Running, DateTimeOffset.UtcNow);
        CloseReminders();
    });
    public void Meeting(int minutes) => Run(() => { Engine.BeginMeeting(DateTimeOffset.UtcNow.AddMinutes(minutes), DateTimeOffset.UtcNow); CloseReminders(); });
    public void EndMeeting() => Run(() => Engine.EndMeeting(DateTimeOffset.UtcNow));
    public void ToggleAway() => Run(() =>
    {
        if (Engine.State.IsAway) Engine.Recover(DateTimeOffset.UtcNow, RecoveryReason.ManualReturn);
        else Engine.MarkUnavailable(DateTimeOffset.UtcNow, true);
        CloseReminders(); _tray.Text = Engine.State.IsAway ? "间歇 · 已离开" : "间歇 · 提醒已恢复";
    });

    private void Tick()
    {
        if (++_timeZoneTicks >= 30) { _timeZoneTicks = 0; RefreshSystemTimeZone(); }
        Main.RefreshClock();
        if(Diagnostics.Enabled&&DateTimeOffset.UtcNow>=_nextDiagnosticSample){_nextDiagnosticSample=DateTimeOffset.UtcNow.AddMinutes(1);Diagnostics.Sample(Engine.State.Plans.Count,Engine.State.Occurrences.Count,_reminders.Count);}
        if (_locked || _faulted || Quitting) return;
        try
        {
            var due = Engine.Evaluate(DateTimeOffset.UtcNow);
            if (Engine.State.MeetingUntil == null) _tray.Text = Engine.State.PauseUntil != null ? "间歇 · 普通提醒已暂停" : "间歇 · 提醒中";
            Main.RefreshIfChanged();
            if(_queue!=null)return;
            if (_reminders.Count != 0 || due.Count == 0) return;
            if (Engine.State.MeetingUntil != null)
            {
                Engine.MarkPresented(due.Select(o => o.Id), DateTimeOffset.UtcNow);
                _tray.Text = "间歇 · 有个人计划待处理（会议中）";
                Balloon("间歇 · 个人计划", Engine.State.Preferences.HideMedicationNames ? "有一项个人计划到期，请在方便时查看。" : string.Join("、",due.Select(o=>o.PlanName)), false);
                return;
            }
            var gentle = due.Where(o => o.Style == ReminderStyle.Gentle || o.SuppressStrong).ToList();
            if (gentle.Count != 0)
            {
                Engine.MarkPresented(gentle.Select(o => o.Id), DateTimeOffset.UtcNow);
                Balloon("间歇提醒", "有待处理事项。请打开间歇查看并确认。", Engine.State.SoundEnabled);
            }
            var cards = due.Except(gentle).ToList();
            if (cards.Count != 0)
            {
                var meds = cards.Where(o => o.Kind == HabitKind.Medication).ToList();
                OpenItems(meds.Count > 0 ? [meds[0]] : cards.Where(o => o.Kind != HabitKind.Medication).ToList());
            }
        }
        catch (Exception ex) { _faulted = true; SchedulingFailure=TaskUi.Failure(ex); Diagnostics.SchedulerFault(ex);Main.RefreshClock(); }
    }

    private void TimeChanged(object? sender, EventArgs args) => Dispatch(RefreshSystemTimeZone);
    private void RefreshSystemTimeZone()
    {
        if (Quitting) return;
        try
        {
            TimeZoneInfo.ClearCachedData();
            if (Engine.SynchronizeTimeZone(TimeZoneInfo.Local, DateTimeOffset.UtcNow))
            { CloseReminders(); Main.RefreshIfChanged(); }
            Main.RefreshClock();
        }
        catch (Exception ex) { _faulted = true; SchedulingFailure = TaskUi.Failure(ex); Main.RefreshClock(); }
    }

    public bool RetryScheduling()
    {
        if(_locked||Quitting)return false;
        _faulted=false;Tick();Main.RefreshClock();Diagnostics.SchedulerRetry(!_faulted);return !_faulted;
    }

    public void Balloon(string title, string text, bool sound)
    {
        _tray.BalloonTipTitle = title; _tray.BalloonTipText = text; _tray.BalloonTipIcon = Forms.ToolTipIcon.None;
        _tray.ShowBalloonTip(6000);
        if (sound) System.Media.SystemSounds.Asterisk.Play();
    }

    public ReminderWindow Preview(HabitKind kind, ReminderStyle style)
    {
        var item = new Occurrence { PlanName = kind == HabitKind.Medication ? "药物 A（界面示例）" : kind==HabitKind.Water?"喝水":kind==HabitKind.Toilet?"如厕提醒":"起来活动", Kind = kind, Style = style, DueAt = DateTimeOffset.UtcNow, NotifyAfter = DateTimeOffset.UtcNow, ActivitySeconds = 5, Note = "这是预览，不改变真实计划或执行记录。" };
        return ShowReminder([item], preview: true);
    }

    public ReminderWindow OpenItems(IReadOnlyList<Occurrence> items)
    {
        if (items.Count == 0) throw new ArgumentException("没有可处理事项。");
        CloseReminders();
        var active = items.Where(o => o.Status != OccurrenceStatus.NeedsReview).ToList();
        if (active.Count != 0) Engine.MarkPresented(active.Select(o => o.Id), DateTimeOffset.UtcNow);
        return ShowReminder(items, preview: false);
    }

    private ReminderWindow ShowReminder(IReadOnlyList<Occurrence> items, bool preview)
    {
        var window = new ReminderWindow(this, items, preview);
        _reminders.Add(window);
        window.Closed += (_, _) => { _reminders.Remove(window); Main.RefreshIfChanged(); Main.RefreshClock(); };
        window.Show();
        Diagnostics.Shown(items,preview);
        if (!preview && Engine.State.SoundEnabled && Engine.State.MeetingUntil == null && !items.Any(o=>o.SuppressStrong)) System.Media.SystemSounds.Asterisk.Play();
        return window;
    }

    public void CloseReminders() { foreach (var window in _reminders.ToArray()) window.CloseWithoutAction(); }
    public void ShowQueue()
    {
        CloseReminders();
        if(_queue!=null){_queue.Show();_queue.Activate();return;}
        ShowMain();_queue=new QueueWindow(this){Owner=Main};
        _queue.Closed+=(_,_)=>{_queue=null;Main.RefreshIfChanged();Main.RefreshClock();};_queue.Show();
    }
    public void CloseOrdinaryReminders() { foreach(var window in _reminders.Where(w=>!w.HasMedication).ToArray())window.CloseWithoutAction(); }
    public void SaveSettings(UserPreferences preferences, bool screens, bool sound)
    {
        var old=Engine.State.Preferences;
        if(IsIsolated && preferences.StartupEnabled!=old.StartupEnabled)throw new InvalidOperationException("独立数据模式不修改开机启动。");
        var startupChanged=!IsIsolated && old.StartupEnabled!=preferences.StartupEnabled;
        if(startupChanged)StartupSetting.Set(preferences.StartupEnabled);
        try{Engine.SavePreferences(preferences,screens,sound,DateTimeOffset.UtcNow);}
        catch{if(startupChanged)StartupSetting.Set(old.StartupEnabled);throw;}
        Theme.Apply(preferences.Theme);
    }

    private void SessionChanged(object sender, SessionSwitchEventArgs e) => Dispatch(()=>HandleSession(e.Reason));
    internal void HandleSession(SessionSwitchReason reason,bool verification=false)
    {
        if(reason==SessionSwitchReason.SessionLock){Diagnostics.Session(true,verification);_sessionLocked=true;Run(()=>Engine.MarkUnavailable(DateTimeOffset.UtcNow));CloseReminders();}
        if(reason==SessionSwitchReason.SessionUnlock){Diagnostics.Session(false,verification);_sessionLocked=false;if(!_suspended)Recover();}
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) => Dispatch(()=>HandlePower(e.Mode));
    internal void HandlePower(PowerModes mode,bool verification=false)
    {
        if(mode==PowerModes.Suspend){Diagnostics.Power(true,verification);_suspended=true;Run(()=>Engine.MarkUnavailable(DateTimeOffset.UtcNow));CloseReminders();}
        if(mode==PowerModes.Resume){Diagnostics.Power(false,verification);_suspended=false;if(!_sessionLocked)Recover();}
    }
    private void Recover() { Run(() => {Engine.Recover(DateTimeOffset.UtcNow, RecoveryReason.SystemReturn);_faulted=false;}); }

    public bool Quit(int exitCode = 0)
    {
        try { Engine.Exit(DateTimeOffset.UtcNow); }
        catch (Exception ex) { ShowMain(); Main.ReportError("退出前保存失败，应用仍在运行。" + ex.Message); return false; }
        Quitting = true; _timer.Stop(); CloseReminders(); _queue?.Close();Diagnostics.Exit(); Main.Close(); System.Windows.Application.Current.Shutdown(exitCode); return true;
    }
    public void Dispose()
    {
        _timer.Stop(); SystemEvents.SessionSwitch -= SessionChanged; SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.TimeChanged -= TimeChanged;
        _tray.Visible = false; _tray.Dispose(); _trayIcon.Dispose(); Store.Dispose();
    }
}
