using System.Text.Json;
using Pausewell.Core;
using Pausewell.Storage;

var results = new List<object>();
var failed = 0;
var origin = new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.FromHours(8));

void Run(string name, Action test)
{
    try { test(); results.Add(new { name, passed = true }); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; results.Add(new { name, passed = false, error = ex.Message }); Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}
void Assert(bool condition, string message = "断言失败") { if (!condition) throw new Exception(message); }
ReminderPlan Habit() => new() { Name = "喝水", Kind = HabitKind.Water, Schedule = ScheduleKind.Interval, IntervalMinutes = 30, EffectiveAt = origin, NextDue = origin.AddMinutes(30) };
ReminderPlan Drug() => new() { Name = "药物甲", Kind = HabitKind.Medication, Schedule = ScheduleKind.FixedTimes, Times = ["09:30", "13:00", "20:00"], EffectiveAt = origin };
(ReminderEngine Engine, MemoryStore Store) Boot(ReminderPlan? p = null)
{
    var state = new AppState { Running = true, LastScan = origin, Plans = [p ?? Habit()] };
    var store = new MemoryStore { Value = state };
    return (new(store, origin), store);
}

Run("完成后间隔：09:37 完成，下次 10:07", () =>
{
    var (e, _) = Boot(); var o = e.Evaluate(origin.AddMinutes(30)).Single(); e.Resolve(o.Id, OccurrenceStatus.Completed, origin.AddMinutes(37));
    Assert(e.State.Plans.Single().NextDue == origin.AddMinutes(67));
});
Run("延期保持身份，不记录完成", () =>
{
    var (e, _) = Boot(); var o = e.Evaluate(origin.AddMinutes(30)).Single(); e.Defer([o.Id], origin.AddMinutes(30));
    Assert(e.Evaluate(origin.AddMinutes(34)).Count == 0); Assert(e.Evaluate(origin.AddMinutes(35)).Single().Id == o.Id);
    Assert(e.State.Occurrences.Single().ResolvedAt == null && e.State.Occurrences.Single().Status == OccurrenceStatus.Deferred);
});
Run("忽略后不积攒多个普通事项", () =>
{
    var (e, _) = Boot(); e.Evaluate(origin.AddMinutes(30)); e.Evaluate(origin.AddHours(3)); Assert(e.State.Occurrences.Count == 1);
});
Run("已显示不等于已完成，五分钟内不重复打扰", () =>
{
    var (e, _) = Boot(); var o = e.Evaluate(origin.AddMinutes(30)).Single(); e.MarkPresented([o.Id], origin.AddMinutes(30));
    Assert(e.Evaluate(origin.AddMinutes(31)).Count == 0); Assert(e.State.Occurrences.Single().Status == OccurrenceStatus.Pending);
});
Run("固定药物晚确认不平移下一次", () =>
{
    var (e, _) = Boot(Drug()); var o = e.Evaluate(origin.AddMinutes(30)).Single(); e.Resolve(o.Id, OccurrenceStatus.Completed, origin.AddMinutes(50));
    var next = e.Evaluate(origin.AddHours(4)).Single(); Assert(e.Local(next.DueAt).Hour == 13);
});
Run("同一固定时刻多次扫描不重复生成", () =>
{
    var (e, _) = Boot(Drug()); e.Evaluate(origin.AddMinutes(30)); e.Evaluate(origin.AddMinutes(30)); Assert(e.State.Occurrences.Count == 1);
});
Run("重复完成幂等，不移动下次时间", () =>
{
    var (e, _) = Boot(); var o = e.Evaluate(origin.AddMinutes(30)).Single(); e.Resolve(o.Id, OccurrenceStatus.Completed, origin.AddMinutes(30));
    e.Resolve(o.Id, OccurrenceStatus.Completed, origin.AddMinutes(40)); Assert(e.State.Plans.Single().NextDue == origin.AddMinutes(60));
});
Run("会议抑制普通弹窗，到期缓冲后恢复", () =>
{
    var (e, _) = Boot(); e.BeginMeeting(origin.AddHours(1), origin); Assert(e.Evaluate(origin.AddMinutes(30)).Count == 0);
    Assert(e.Evaluate(origin.AddHours(1)).Count == 0); Assert(e.State.MeetingUntil == null);
    Assert(e.Evaluate(origin.AddHours(1).AddSeconds(30)).Count == 0 && e.State.RecoverySummaryVisible && e.State.Occurrences.All(o=>o.Status!=OccurrenceStatus.Completed));
});
Run("会议不改变药物时刻和确认状态", () =>
{
    var (e, _) = Boot(Drug()); e.BeginMeeting(origin.AddHours(1), origin); var o = e.Evaluate(origin.AddMinutes(30)).Single();
    Assert(o.DueAt == origin.AddMinutes(30) && o.Status == OccurrenceStatus.Pending);
});
Run("重启恢复的药物进入待核实，不自动要求补服", () =>
{
    var (_, store) = Boot(Drug()); var e = new ReminderEngine(store, origin.AddHours(5));
    Assert(e.State.Occurrences.Count == 2 && e.State.Occurrences.All(o => o.Status == OccurrenceStatus.NeedsReview));
    Assert(e.Evaluate(origin.AddHours(5)).Count == 0);
});
Run("已完成事项重启后不再次提示", () =>
{
    var (e, store) = Boot(Drug()); var o = e.Evaluate(origin.AddMinutes(30)).Single(); e.Resolve(o.Id, OccurrenceStatus.Completed, origin.AddMinutes(31));
    var restart = new ReminderEngine(store, origin.AddMinutes(40)); Assert(restart.State.Occurrences.Single().Status == OccurrenceStatus.Completed);
    Assert(restart.Evaluate(origin.AddMinutes(40)).Count == 0);
});
Run("不确定服药不会写入已服用", () =>
{
    var (e, _) = Boot(Drug()); var o = e.Evaluate(origin.AddMinutes(30)).Single(); e.Resolve(o.Id, OccurrenceStatus.NeedsReview, origin.AddMinutes(30));
    Assert(e.State.Occurrences.Single().Status == OccurrenceStatus.NeedsReview && e.Evaluate(origin.AddHours(1)).Count == 0);
});
Run("不接受药物等间隔计划", () =>
{
    var (e, _) = Boot(); var p = Drug(); p.Schedule = ScheduleKind.Interval;
    try { e.SavePlan(p, origin); throw new Exception("错误接受药物等间隔"); } catch (ArgumentException) { }
});
Run("不接受药物办公时段继承", () =>
{
    var (e, _) = Boot(); var p = Drug(); p.WorkdaysOnly = true;
    try { e.SavePlan(p, origin); throw new Exception("错误接受药物工作日限制"); } catch (ArgumentException) { }
});
Run("拒绝重复固定时刻", () =>
{
    var (e, _) = Boot(); var p = Drug(); p.Times = ["08:00", "08:00"];
    try { e.Validate(p); throw new Exception("接受重复时刻"); } catch (ArgumentException) { }
});
Run("工作日窗口：周五下班后顺延周一", () =>
{
    var (e, _) = Boot(); var p = Habit(); p.WorkdaysOnly = true; p.WindowStart = "09:00"; p.WindowEnd = "18:00";
    var next = e.Eligible(p, new DateTimeOffset(2026, 10, 16, 18, 20, 0, TimeSpan.FromHours(8)));
    Assert(e.Local(next) == new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(8)));
});
Run("固定时刻跨午夜按中国时区生成", () =>
{
    var (e, _) = Boot(Drug()); var p = Drug(); p.Times = ["00:10"];
    var next = e.Preview(p, new DateTimeOffset(2026, 10, 15, 23, 59, 0, TimeSpan.FromHours(8)), 1).Single();
    Assert(e.Local(next).Day == 16 && e.Local(next).Minute == 10);
});
Run("时钟回拨不重复固定事项", () =>
{
    var (e, _) = Boot(Drug()); e.Evaluate(origin.AddMinutes(30)); e.Evaluate(origin.AddMinutes(20)); e.Evaluate(origin.AddMinutes(30)); Assert(e.State.Occurrences.Count == 1);
});
Run("不同药物同时到期仍是独立事项", () =>
{
    var (e, _) = Boot(Drug()); var p = Drug(); p.Name = "药物乙"; e.SavePlan(p, origin); e.Evaluate(origin.AddMinutes(30));
    var first = e.State.Occurrences.First(); e.Resolve(first.Id, OccurrenceStatus.Completed, origin.AddMinutes(30));
    Assert(e.State.Occurrences.Count(o => o.Status == OccurrenceStatus.Completed) == 1 && e.State.Occurrences.Count(o => o.Status == OccurrenceStatus.Pending) == 1);
});
Run("修改规则保留旧记录并取消旧待办", () =>
{
    var (e, _) = Boot(); e.Evaluate(origin.AddMinutes(30)); var p = e.State.Plans.Single(); p = JsonSerializer.Deserialize<ReminderPlan>(JsonSerializer.Serialize(p, StateJson.Options), StateJson.Options)!;
    p.IntervalMinutes = 60; e.SavePlan(p, origin.AddMinutes(35)); Assert(e.State.Occurrences.Single().Status == OccurrenceStatus.Cancelled && e.State.Plans.Single().Revision == 2);
});
Run("保存失败不提交完成状态或下次时间", () =>
{
    var (e, store) = Boot(); var o = e.Evaluate(origin.AddMinutes(30)).Single(); store.Fail = true;
    try { e.Resolve(o.Id, OccurrenceStatus.Completed, origin.AddMinutes(37)); throw new Exception("未报告保存异常"); } catch (IOException) { }
    Assert(e.State.Occurrences.Single().Status == OccurrenceStatus.Pending && e.State.Plans.Single().NextDue == origin.AddMinutes(30));
});
Run("超过 30 天恢复明确提示历史缺口", () =>
{
    var (_, store) = Boot(Drug()); var e = new ReminderEngine(store, origin.AddDays(40));
    Assert(e.State.RecoveryNotice.Contains("30 天") && e.State.Occurrences.All(o => o.Status == OccurrenceStatus.NeedsReview));
});
Run("暂停不完成事项，再次开启重新安排普通间隔", () =>
{
    var (e, _) = Boot(); e.Evaluate(origin.AddMinutes(30)); e.SetRunning(false, origin.AddMinutes(31)); Assert(e.Evaluate(origin.AddHours(2)).Count == 0);
    e.SetRunning(true, origin.AddHours(2)); Assert(e.State.Plans.Single().NextDue == origin.AddMinutes(150)); Assert(e.State.Occurrences.All(o => o.Status != OccurrenceStatus.Completed));
});
Run("修改或停用药物计划不抹掉未核对事项", () =>
{
    var (e, _) = Boot(Drug()); e.Evaluate(origin.AddMinutes(30));
    var p = JsonSerializer.Deserialize<ReminderPlan>(JsonSerializer.Serialize(e.State.Plans.Single(), StateJson.Options), StateJson.Options)!;
    p.Enabled = false; e.SavePlan(p, origin.AddMinutes(40));
    Assert(e.State.Occurrences.Single().Status == OccurrenceStatus.NeedsReview && e.State.Occurrences.Single().ResolvedAt == null);
});
Run("普通固定事项恢复时不堆积多个补打卡", () =>
{
    var p = Drug(); p.Kind = HabitKind.Water; var (e, _) = Boot(p);
    e.Evaluate(origin.AddHours(5)); Assert(e.State.Occurrences.Count(o => o.Status == OccurrenceStatus.Pending) == 0);
    Assert(e.State.Occurrences.All(o => o.Status == OccurrenceStatus.Expired));
});
Run("普通待办在办公时段外不重复弹出", () =>
{
    var p = Habit(); p.WindowStart = "09:00"; p.WindowEnd = "18:00"; var (e, _) = Boot(p);
    e.Evaluate(origin.AddMinutes(30)); Assert(e.Evaluate(origin.AddHours(10)).Count == 0);
    Assert(e.State.Occurrences.Single().Status == OccurrenceStatus.Expired);
});
Run("全局暂停期间会议模式仍到期恢复", () =>
{
    var (e, _) = Boot(); e.SetRunning(false, origin); e.BeginMeeting(origin.AddMinutes(1), origin);
    Assert(e.Evaluate(origin.AddMinutes(2)).Count == 0 && e.State.MeetingUntil == null && !e.State.Running);
});
Run("SQLite 实际事务保存、重开和完整性检查", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "Pausewell.Specs", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "state.db"); var p = Drug(); p.Name = "中文药物 ' 引号测试";
    using (var store = new SqliteStateStore(path))
    {
        store.Save(new AppState { Running = true, LastScan = origin, Plans = [p] });
        var e = new ReminderEngine(store, origin); var o = e.Evaluate(origin.AddMinutes(30)).Single(); e.Resolve(o.Id, OccurrenceStatus.Completed, origin.AddMinutes(31));
        Assert(store.IntegrityCheck() == "ok");
    }
    using (var reopened = new SqliteStateStore(path)) { var s = reopened.Load()!; Assert(s.Plans.Single().Name == p.Name && s.Occurrences.Single().Status == OccurrenceStatus.Completed); }
});

Run("普通暂停保留服药，到期重新安排普通间隔", () =>
{
    var (e, _) = Boot(); var drug = Drug(); e.SavePlan(drug, origin);
    e.BeginPause(origin.AddHours(1), false, origin);
    var due = e.Evaluate(origin.AddMinutes(30)); Assert(due.Count == 1 && due[0].Kind == HabitKind.Medication);
    e.Evaluate(origin.AddHours(1)); Assert(e.State.PauseUntil == null && e.State.Plans.Single(p => p.Kind == HabitKind.Water).NextDue == origin.AddMinutes(90));
});
Run("同时暂停药物提示后，缺少确认的药物待核实", () =>
{
    var (e, _) = Boot(Drug()); e.BeginPause(origin.AddHours(1), true, origin);
    Assert(e.Evaluate(origin.AddHours(1)).Count == 0); Assert(e.State.Occurrences.Single().Status == OccurrenceStatus.NeedsReview);
});
Run("药物计划日期边界与未来开始日期", () =>
{
    var (e, _) = Boot(); var p = Drug(); p.StartsOn = new(2026,10,17); p.EndsOn = new(2026,10,17);
    e.SavePlan(p, origin); Assert(e.Preview(p, origin).All(t => e.Local(t).Day == 17));
    e.Evaluate(origin.AddDays(1)); Assert(e.State.Occurrences.Count(o => o.Kind == HabitKind.Medication) == 0);
    e.Evaluate(origin.AddDays(3)); var meds=e.State.Occurrences.Where(o => o.Kind == HabitKind.Medication).ToList(); Assert(meds.Count == 3 && meds.All(o => e.Local(o.DueAt).Day == 17));
});
Run("更正保留原值且不移动未来计划", () =>
{
    var (e, _) = Boot(Drug()); var o=e.Evaluate(origin.AddMinutes(30)).Single(); e.Resolve(o.Id, OccurrenceStatus.Completed, origin.AddMinutes(40), origin.AddMinutes(35));
    var times=e.State.Plans.Single().Times.ToArray(); e.CorrectRecord(o.Id, OccurrenceStatus.Completed, origin.AddMinutes(36), "实际时间记错", origin.AddMinutes(50));
    var r=e.State.Occurrences.Single(); Assert(r.Corrections.Single().BeforeActual == origin.AddMinutes(35) && r.ActualAt == origin.AddMinutes(36) && times.SequenceEqual(e.State.Plans.Single().Times));
});
Run("更正需要原因，禁止未来实际时间", () =>
{
    var (e, _) = Boot(); var o=e.Evaluate(origin.AddMinutes(30)).Single(); e.Resolve(o.Id,OccurrenceStatus.Completed,origin.AddMinutes(31));
    try { e.CorrectRecord(o.Id,OccurrenceStatus.Skipped,null,"",origin.AddMinutes(32)); throw new Exception("接受空原因"); } catch(ArgumentException) { }
    try { e.CorrectRecord(o.Id,OccurrenceStatus.Completed,origin.AddHours(1),"修正",origin.AddMinutes(32)); throw new Exception("接受未来实际时间"); } catch(ArgumentException) { }
});
Run("换肤只改变设置，保存失败保持原主题", () =>
{
    var (e, store)=Boot(); var next=e.State.Plans.Single().NextDue;
    e.SavePreferences(new() { Theme="rose" },false,false,origin); Assert(e.State.Preferences.Theme=="rose" && e.State.Plans.Single().NextDue==next);
    store.Fail=true; try { e.SavePreferences(new() { Theme="blue" },false,false,origin); throw new Exception("未报告失败"); } catch(IOException) { }
    Assert(e.State.Preferences.Theme=="rose");
});
Run("schema 1 兼容，主题与记录不丢失", () =>
{
    var store=new MemoryStore { Value=new AppState { SchemaVersion=1, LastScan=origin, Plans=[Habit()] } };
    var e=new ReminderEngine(store,origin); Assert(e.State.SchemaVersion==4 && e.State.Preferences.Theme=="champagne" && e.State.Plans.Count==1);
    e.ResumeOnLaunch(origin); Assert(e.State.Running);
});
Run("停用一项不重置其他项计时", () =>
{
    var (e,_) = Boot(); var p=Habit(); e.SavePlan(p,origin); var next=e.State.Plans.First().NextDue;
    e.SetEnabled(p.Id,false,origin.AddMinutes(10)); Assert(e.State.Plans.First().NextDue==next);
});
Run("如厕当前无需独立记录，不能用于药物", () =>
{
    var p=Habit();p.Kind=HabitKind.Toilet;var (e,_)=Boot(p);var o=e.Evaluate(origin.AddMinutes(30)).Single();e.Resolve(o.Id,OccurrenceStatus.NotNeeded,origin.AddMinutes(30)); Assert(e.State.Occurrences.Single().Status==OccurrenceStatus.NotNeeded);
});

Run("首次保存与启动在同一事务中", () =>
{
    var store=new MemoryStore();var e=new ReminderEngine(store,origin);e.SavePlan(Habit(),origin,startIfNeeded:true);Assert(e.State.Running&&e.State.Plans.Count==1&&store.Load()!.Running);
    var failedStore=new MemoryStore { Fail=true };var failedEngine=new ReminderEngine(failedStore,origin);try{failedEngine.SavePlan(Habit(),origin,startIfNeeded:true);throw new Exception("未报告失败");}catch(IOException){}Assert(!failedEngine.State.Running&&failedEngine.State.Plans.Count==0);
});
Run("待核实药物明确延期后再通知，原计划不变", () =>
{
    var (_,store)=Boot(Drug());var e=new ReminderEngine(store,origin.AddMinutes(40));var o=e.State.Occurrences.Single();e.Defer([o.Id],origin.AddMinutes(40));Assert(e.Evaluate(origin.AddMinutes(44)).Count==0);Assert(e.Evaluate(origin.AddMinutes(45)).Single().DueAt==origin.AddMinutes(30));
});

Run("SQLite 旧版快照升级前备份并保留原计划", () =>
{
    var dir=Path.Combine(Path.GetTempPath(),"Pausewell.Specs",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);using var store=new SqliteStateStore(Path.Combine(dir,"old.db"));store.Save(new(){SchemaVersion=1,LastScan=origin,Plans=[Habit()]});var backup=store.Backup();var e=new ReminderEngine(store,origin);e.SavePreferences(new(){Theme="copper"},false,false,origin);Assert(store.Load()!.SchemaVersion==4&&store.Load()!.Plans.Count==1);using var old=new SqliteStateStore(backup);Assert(old.Load()!.SchemaVersion==1&&old.IntegrityCheck()=="ok");
});

OnceSpecs.Run(Run);
LifecycleSpecs.Run(Run);

var output = args.FirstOrDefault() ?? Path.Combine(Environment.CurrentDirectory, "artifacts", "rules-test-results.json");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, JsonSerializer.Serialize(new { executedAt = DateTimeOffset.UtcNow, total = results.Count, failed, results }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{results.Count - failed}/{results.Count} passed. Report: {output}");
return failed == 0 ? 0 : 1;

sealed class MemoryStore : IStateStore
{
    public AppState? Value;
    public bool Fail;
    public AppState? Load() => Value == null ? null : StateJson.Clone(Value);
    public void Save(AppState state) { if (Fail) throw new IOException("模拟写入失败"); Value = StateJson.Clone(state); }
}
