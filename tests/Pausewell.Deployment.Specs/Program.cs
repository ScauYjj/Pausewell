using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Automation;
using Microsoft.Win32;
using Pausewell.Core;
using Pausewell.Storage;

var workspace = Path.GetFullPath(args[0]);
var releaseRoot = Path.Combine(workspace, "artifacts", "packages");
var testRoot = Path.Combine(releaseRoot, "verification", Guid.NewGuid().ToString("N"), "中文 空格路径");
Directory.CreateDirectory(testRoot);
var extracted = Path.Combine(testRoot, "解压安装包");
System.IO.Compression.ZipFile.ExtractToDirectory(Path.Combine(releaseRoot, "Pausewell-0.3.10-win-x64-install.zip"), extracted);
var bundle = Path.Combine(extracted, "Pausewell-0.3.10-win-x64");
var helper = Path.Combine(bundle, "maintenance", "Pausewell.Maintenance.exe");
var root = Path.Combine(testRoot, "程序");
var data = Path.Combine(testRoot, "数据");
var checks = new List<object>();
var started = DateTimeOffset.UtcNow;
Process? app = null;
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
void Check(string name, bool passed) { checks.Add(new { name, passed }); Console.WriteLine((passed ? "PASS " : "FAIL ") + name); if (!passed) throw new Exception(name); }
(int Code, string Output, string Error) Execute(string file, params string[] arguments)
{
    using var process = new Process { StartInfo = new(file) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
    foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
    process.Start();
    var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(60000)) { process.Kill(); throw new Exception("Own verification child exceeded timeout."); }
    var capturedOutput = output.GetAwaiter().GetResult(); var capturedError = error.GetAwaiter().GetResult();
    File.AppendAllText(Path.Combine(testRoot, "commands.jsonl"), JsonSerializer.Serialize(new { file, arguments, code = process.ExitCode, output = capturedOutput, error = capturedError }) + Environment.NewLine);
    return (process.ExitCode, capturedOutput, capturedError);
}
(int Code, string Output, string Error) Manage(string action, string? source = null, bool confirm = false, string? atBundle = null, string? atRoot = null, string? atData = null)
{
    var arguments = new List<string> { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(atBundle ?? bundle, "Manage-Pausewell.ps1"), "-Action", action, "-InstallRoot", atRoot ?? root, "-DataRoot", atData ?? data, "-Isolated" };
    if (source != null) arguments.AddRange(["-SourcePath", source]); if (confirm) arguments.Add("-ConfirmDataRestore");
    return Execute("powershell.exe", arguments.ToArray());
}
AppState Read() { using var store = new SqliteStateStore(Path.Combine(data, "pausewell.db")); return store.Load()!; }
void Save(AppState state) { using var store = new SqliteStateStore(Path.Combine(data, "pausewell.db")); store.Save(state); }
string State() => JsonSerializer.Serialize(Read(), StateJson.Options);
string RegistryValue(string key, string name) { using var registry = Registry.CurrentUser.OpenSubKey(key); return registry?.GetValue(name)?.ToString() ?? "<absent>"; }
var startupBefore = RegistryValue(@"Software\Microsoft\Windows\CurrentVersion\Run", "Pausewell");
var uninstallBefore = RegistryValue(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Pausewell", "InstallLocation");
var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "间歇 Pausewell.lnk");
var shortcutBefore = File.Exists(shortcut) ? Hash(shortcut) : "<absent>";
var success = false;
try
{
    var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(bundle, "pausewell-package.json")))!;
    var publishedApp = Path.Combine(workspace, "artifacts", "publish", "win-x64", "Pausewell.exe");
    var publishedHash = Hash(publishedApp);
    Check("ZIP contains the current locally published desktop executable", Hash(Path.Combine(bundle, "Pausewell.exe")) == publishedHash);
    var probe = Execute(helper, "probe", data);
    Check("Published self-contained maintenance EXE runs directly and missing data remains absent", probe.Code == 0 && !Directory.Exists(data));
    var install = Manage("install");
    Check("Fresh install succeeds from fully extracted ZIP with Chinese and space paths", install.Code == 0);
    Check("Installed main program is byte-identical to verified release", Hash(Path.Combine(root, "Pausewell.exe")) == Hash(Path.Combine(bundle, "Pausewell.exe")));
    Check("Fresh install does not create a reminder database or start reminders", !File.Exists(Path.Combine(data, "pausewell.db")));
    Check("Repeat install succeeds", Manage("install").Code == 0);
    var now = DateTimeOffset.UtcNow;
    var state = new AppState { Running = false, LastScan = now, Preferences = new() { Theme = "violet", StartupEnabled = false },
        Plans = [new() { Id = "once", Name = "私有一次事项", Kind = HabitKind.Other, Schedule = ScheduleKind.Once, OnceAt = now.AddDays(1), EffectiveAt = now },
                 new() { Id = "medicine", Name = "私有药名", Kind = HabitKind.Medication, Schedule = ScheduleKind.FixedTimes, Times = ["08:00", "20:00"], EffectiveAt = now }],
        Occurrences = [new() { Id = "record", PlanId = "medicine", PlanName = "私有药名", Kind = HabitKind.Medication, DueAt = now.AddMinutes(-10), Status = OccurrenceStatus.Completed, ActualAt = now.AddMinutes(-8), ResolvedAt = now, Corrections = [new(now, OccurrenceStatus.NeedsReview, null, null, OccurrenceStatus.Completed, now.AddMinutes(-8), "已核实的私有原因")] }] };
    Save(state);
    var before = State();
    // A managed current-version fixture keeps this suite reproducible from a fresh clone.
    // Cross-version v0.3.9 evidence belongs to the historical beta baseline.
    var previousApp = publishedApp;
    File.Copy(previousApp, Path.Combine(root, "Pausewell.exe"), true);
    var oldManifest = JsonNode.Parse(manifest.ToJsonString())!; oldManifest["version"] = "0.3.10";
    foreach (var entry in oldManifest["files"]!.AsArray()) if (entry!["path"]!.GetValue<string>() == "Pausewell.exe") entry["sha256"] = Hash(previousApp);
    File.WriteAllText(Path.Combine(root, "pausewell-package.json"), oldManifest.ToJsonString());
    var upgrade = Manage("install");
    Check("Managed installation updates successfully", upgrade.Code == 0);
    var upgradeBackup = JsonNode.Parse(upgrade.Output)!["backup"]!.GetValue<string>();
    Check("Upgrade retains exact plans, once schedule, medicine times, actual and confirmed times, corrections and theme", State() == before);
    Check("Upgrade saves verified old executable and consistent database snapshot", Hash(Path.Combine(upgradeBackup, "Pausewell.exe")) == Hash(previousApp) && Execute(helper, "probe", upgradeBackup).Code == 0);
    var newest = Read(); newest.Occurrences.Add(new() { Id = "new-record", PlanId = "once", Status = OccurrenceStatus.Completed, ResolvedAt = now.AddMinutes(1), ActualAt = now.AddMinutes(1) }); Save(newest);
    var withNewRecord = State();
    var rollback = Manage("rollback", upgradeBackup);
    Check("Program rollback restores exact allowlisted backed-up executable", rollback.Code == 0 && Hash(Path.Combine(root, "Pausewell.exe")) == Hash(previousApp));
    Check("Program rollback keeps latest new execution record", State() == withNewRecord);
    Check("Rolled-back program can upgrade again", Manage("install").Code == 0 && State() == withNewRecord);
    var rejectedRestore = Manage("restore", Path.Combine(upgradeBackup, "pausewell.db"));
    Check("Data restore requires separate explicit confirmation", rejectedRestore.Code != 0 && State() == withNewRecord);
    var restored = Manage("restore", Path.Combine(upgradeBackup, "pausewell.db"), true);
    Check("Explicit data restore restores selected state without evaluating reminders", restored.Code == 0 && State() == before);
    var recovery = JsonNode.Parse(restored.Output)!["recovery"]!.GetValue<string>();
    using (var store = new SqliteStateStore(Path.Combine(recovery, "pausewell.db"))) Check("Data restore retains pre-restore new records in another consistent snapshot", store.Load()!.Occurrences.Count == 2 && store.IntegrityCheck() == "ok");
    var manual = Path.Combine(data, "backups", "manual-test.db");
    Check("Manual backup succeeds", Manage("backup", manual).Code == 0);
    using (var liveStore = new SqliteStateStore(Path.Combine(data, "pausewell.db")))
    {
        var walState = liveStore.Load()!; walState.Preferences.Theme = "rose"; liveStore.Save(walState);
        var walSnapshot = Path.Combine(data, "backups", "wal-test.db");
        Check("Consistent snapshot includes committed WAL pages while SQLite connection stays open", Manage("backup", walSnapshot).Code == 0);
        using var walStore = new SqliteStateStore(walSnapshot);
        Check("WAL snapshot has the current committed value and passes integrity check", walStore.Load()!.Preferences.Theme == "rose" && walStore.IntegrityCheck() == "ok");
    }
    Save(state);
    var manualHash = Hash(manual);
    Check("Existing backup cannot be overwritten", Manage("backup", manual).Code != 0 && Hash(manual) == manualHash);
    var invalid = Path.Combine(testRoot, "invalid.db"); File.WriteAllText(invalid, "This is not a database.");
    Check("Corrupt restore is rejected without changing current state", Manage("restore", invalid, true).Code != 0 && State() == before);
    var incompatible = Read(); incompatible.SchemaVersion = 99; Save(incompatible);
    var incompatibleHash = Hash(Path.Combine(data, "pausewell.db")); var programHash = Hash(Path.Combine(root, "Pausewell.exe"));
    Check("Unknown newer schema blocks install and preserves original bytes", Manage("install").Code != 0 && Hash(Path.Combine(data, "pausewell.db")) == incompatibleHash && Hash(Path.Combine(root, "Pausewell.exe")) == programHash);
    Save(state);
    var occupied = Path.Combine(testRoot, "foreign-directory"); Directory.CreateDirectory(occupied); File.WriteAllText(Path.Combine(occupied, "user.txt"), "keep");
    Check("An unmanaged directory is rejected without deleting user files", Manage("install", atRoot: occupied).Code != 0 && File.ReadAllText(Path.Combine(occupied, "user.txt")) == "keep");
    Check("Program and data roots cannot overlap", Manage("install", atRoot: data).Code != 0 && State() == before);
    // Corrupt one small packaged script to test package verification without duplicating large EXEs.
    var installCmd = Path.Combine(bundle, "Install.cmd"); var originalCmd = File.ReadAllBytes(installCmd); File.AppendAllText(installCmd, "tamper");
    try { Check("Altered install payload is rejected before replacing installed files", Manage("install").Code != 0 && Hash(Path.Combine(root, "Pausewell.exe")) == programHash && State() == before); }
    finally { File.WriteAllBytes(installCmd, originalCmd); }
    File.WriteAllText(Path.Combine(root, "user-note.txt"), "keep");
    Check("Uninstall refuses unowned files instead of recursively deleting them", Manage("uninstall").Code != 0 && File.ReadAllText(Path.Combine(root, "user-note.txt")) == "keep");
    File.Delete(Path.Combine(root, "user-note.txt"));
    app = new Process { StartInfo = new(Path.Combine(root, "Pausewell.exe")) { UseShellExecute = false } }; app.StartInfo.ArgumentList.Add("--data-dir"); app.StartInfo.ArgumentList.Add(data); app.Start();
    var deadline = DateTime.UtcNow.AddSeconds(30);
    while (DateTime.UtcNow < deadline && !app.HasExited) { app.Refresh(); if (app.MainWindowHandle != IntPtr.Zero) break; Thread.Sleep(100); }
    Check("Installed application opens as a real Windows window", !app.HasExited && app.MainWindowHandle != IntPtr.Zero);
    Check("Running application blocks upgrade without terminating it", Manage("install").Code != 0 && !app.HasExited);
    Check("Running application blocks uninstall without terminating it", Manage("uninstall").Code != 0 && !app.HasExited);
    Check("Running application blocks data restore", Manage("restore", manual, true).Code != 0 && !app.HasExited);
    var window = AutomationElement.FromHandle(app.MainWindowHandle);
    void Invoke(string id)
    {
        AutomationElement? element = null; var until = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < until) { element = window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, id)); if (element != null) break; Thread.Sleep(100); }
        if (element == null) throw new Exception("UIA control missing: " + id);
        ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke(); Thread.Sleep(300);
    }
    Invoke("NavSettings"); Invoke("QuitApp");
    Check("Installed app exits normally through settings", app.WaitForExit(15000) && app.ExitCode == 0);
    app.Dispose(); app = null;
    var finalState = State();
    Check("Uninstall succeeds through installed management script", Manage("uninstall", atBundle: root).Code == 0 && !Directory.Exists(root));
    Check("Uninstall retains exact final database and all upgrade/restore backups", State() == finalState && File.Exists(manual) && File.Exists(Path.Combine(upgradeBackup, "Pausewell.exe")) && File.Exists(Path.Combine(recovery, "pausewell.db")));
    Check("Reinstall after uninstall reuses preserved data without rewriting it", Manage("install").Code == 0 && State() == finalState);
    Check("Isolated tests leave real startup, uninstall registration and shortcut unchanged", RegistryValue(@"Software\Microsoft\Windows\CurrentVersion\Run", "Pausewell") == startupBefore && RegistryValue(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Pausewell", "InstallLocation") == uninstallBefore && (File.Exists(shortcut) ? Hash(shortcut) : "<absent>") == shortcutBefore);
    Check("Source published executable remains unchanged by installation tests", Hash(publishedApp) == publishedHash);
    success = true;
}
finally
{
    // Only our own isolated test app may be stopped on failure, never another instance.
    if (app != null) { if (!app.HasExited && app.MainModule?.FileName == Path.Combine(root, "Pausewell.exe")) app.Kill(); app.Dispose(); }
    File.WriteAllText(Path.Combine(releaseRoot, "deployment-test-results.json"), JsonSerializer.Serialize(new { success, startedAt = started, finishedAt = DateTimeOffset.UtcNow, testRoot, archiveSha256 = Hash(Path.Combine(releaseRoot, "Pausewell-0.3.10-win-x64-install.zip")), checks, realUserInstallTested = false }, new JsonSerializerOptions { WriteIndented = true }));
}
return success ? 0 : 1;
