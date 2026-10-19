using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Pausewell.Storage;

namespace Pausewell.Desktop;

public partial class App : System.Windows.Application
{
    private Mutex? _instance;
    private DesktopController? _controller;
    private bool _verificationStarted;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Resources.MergedDictionaries.Add(Pausewell.Controls.DesignTheme.CreateResources());
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Pausewell.Presentation;component/WorkbenchStyles.xaml",UriKind.Relative)});
        Resources["StandardButton"]=Resources["ButtonBase"];Resources["Primary"]=Resources["PrimaryButton"];Resources["Toggle"]=Pausewell.Presentation.PageUi.ToggleStyle();
        string? Value(string name) { var i = Array.IndexOf(e.Args, name); return i >= 0 && i + 1 < e.Args.Length ? e.Args[i + 1] : null; }
        var smoke = e.Args.Contains("--smoke");
        var reportDir = Path.GetFullPath(Value("--report-dir") ?? Path.Combine(Environment.CurrentDirectory, "artifacts", "gui-smoke"));
        var dataDir = Path.GetFullPath(Value("--data-dir") ?? (smoke ? Path.Combine(reportDir, "isolated-data", Guid.NewGuid().ToString("N")) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),e.Args.Contains("--demo")?"PausewellPreview":"PausewellPrototype")));
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dataDir.ToUpperInvariant())))[..16];
        _instance = new Mutex(true, "Local\\Pausewell.Prototype." + key, out var first);
        if (!first) { MessageBox.Show("这个数据目录的间歇已在运行。请在系统托盘中打开。", "间歇"); Shutdown(); return; }
        try
        {
            var store = new SqliteStateStore(Path.Combine(dataDir, "pausewell.db"));
            var saved = store.Load();
            if(saved != null && (saved.SchemaVersion is 1 or 2 or 3 || saved.TimeZoneId != TimeZoneInfo.Local.Id || saved.TimeZoneRules.Length > 0 && saved.TimeZoneRules != TimeZoneInfo.Local.ToSerializedString()))store.Backup();
            if(e.Args.Contains("--demo")&&store.Load()==null)
            {
                var now=DateTimeOffset.UtcNow;
                store.Save(new AppState { Running=true,LastScan=now,Plans=[
                    new(){Name="喝水",Kind=HabitKind.Water,Schedule=ScheduleKind.Interval,IntervalMinutes=30,EffectiveAt=now,NextDue=now.AddMinutes(30)},
                    new(){Name="起来活动",Kind=HabitKind.Activity,Schedule=ScheduleKind.Interval,IntervalMinutes=45,EffectiveAt=now,NextDue=now.AddMinutes(45)},
                    new(){Name="如厕提醒",Kind=HabitKind.Toilet,Schedule=ScheduleKind.Interval,IntervalMinutes=60,EffectiveAt=now,NextDue=now.AddMinutes(60)},
                    new(){Name="药物 A（演示，未启用）",Kind=HabitKind.Medication,Schedule=ScheduleKind.FixedTimes,Enabled=false,Times=["08:00","13:00","20:00"],EffectiveAt=now}
                ]});
            }
            var engine = new ReminderEngine(store, DateTimeOffset.UtcNow);
            _controller = new DesktopController(engine, store, dataDir, smoke || e.Args.Contains("--demo") || Value("--data-dir")!=null,smoke||e.Args.Contains("--diagnostics"));
            MainWindow = _controller.Main;
            if (smoke) _controller.Main.Loaded += async (_, _) => {if(_verificationStarted)return;_verificationStarted=true;await SmokeVerification.Run(_controller, reportDir);};
            if(!e.Args.Contains("--background")||smoke)_controller.Main.Show();
        }
        catch (Exception ex) { MessageBox.Show("启动失败，未重置或覆盖既有数据。\n" + ex.Message, "间歇", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
