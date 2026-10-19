using Pausewell.Core;

static class TimeZoneSpecs
{
    public static void Run(Action<string, Action> run)
    {
        var alaska = TimeZoneInfo.CreateCustomTimeZone("Alaskan Standard Time", TimeSpan.FromHours(-9), "Alaska", "Alaska");
        var now = new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(-9));
        void Check(bool value) { if (!value) throw new Exception("时区规则断言失败"); }
        run("默认时区遵循 Windows 本地规则，包括关闭夏令时", () =>
        {
            var e = new ReminderEngine(new Store(), now);
            Check(e.Zone.Id == TimeZoneInfo.Local.Id && e.Zone.HasSameRules(TimeZoneInfo.Local));
            Check(e.Local(now) == TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local));
            Check(e.Local(now).Offset == TimeZoneInfo.Local.GetUtcOffset(now));
        });
        run("旧中国时区迁移后按本地固定时刻触发且不补列旧时刻", () =>
        {
            var p = new ReminderPlan { Kind = HabitKind.Medication, Name = "独立测试", Schedule = ScheduleKind.FixedTimes, Times = ["09:30"], EffectiveAt = now.AddDays(-2) };
            var oldDue = new DateTimeOffset(2026, 10, 19, 9, 30, 0, TimeSpan.FromHours(8));
            var old = new Occurrence { Id = $"{p.Id}:1:2026-10-19:09:30", PlanId = p.Id, Revision = 1, Kind = HabitKind.Medication, DueAt = oldDue, Status = OccurrenceStatus.Completed, ActualAt = oldDue, ResolvedAt = oldDue };
            var store = new Store { Value = new() { TimeZoneId = TestZones.China.Id, Running = true, LastScan = now.AddDays(-1), LastObservedAt = now, Plans = [p], Occurrences = [old] } };
            var e = new ReminderEngine(store, now, alaska);
            Check(e.State.TimeZoneId == alaska.Id && e.State.Occurrences.Count == 1);
            Check(e.State.Occurrences.Single().DueAt == oldDue && e.State.Occurrences.Single().Status == OccurrenceStatus.Completed);
            Check(e.Preview(p, now, 1).Single() == now.AddMinutes(30));
            var item = e.Evaluate(now.AddMinutes(30)).Single();
            Check(item.DueAt == now.AddMinutes(30) && e.Local(item.DueAt).Hour == 9);
            e.Evaluate(now.AddMinutes(30)); Check(e.State.Occurrences.Count == 2);
        });
        run("历史固定时刻旧格式身份不会因升级重复生成", () =>
        {
            var p = new ReminderPlan { Kind = HabitKind.Water, Schedule = ScheduleKind.FixedTimes, Times = ["09:30"], EffectiveAt = now.AddDays(-1) };
            var due = now.AddMinutes(30);
            var store = new Store { Value = new() { TimeZoneId = alaska.Id, Running = true, LastScan = now, LastObservedAt = now, Plans = [p], Occurrences = [new() { Id = $"{p.Id}:1:2026-10-19:09:30", PlanId = p.Id, Revision = 1, Kind = p.Kind, Purpose = PlanPurpose.Habit, DueAt = due, Status = OccurrenceStatus.Completed }] } };
            var e = new ReminderEngine(store, now, alaska); e.Evaluate(due);
            Check(e.State.Occurrences.Count == 1);
        });
        run("时区切换保留无时段间隔、一次性及截止的绝对时间点", () =>
        {
            var habit = new ReminderPlan { Name = "喝水测试", Kind = HabitKind.Water, Schedule = ScheduleKind.Interval, NextDue = now.AddMinutes(30) };
            var todo = new ReminderPlan { Name = "待办测试", Kind = HabitKind.Other, Schedule = ScheduleKind.Once, OnceAt = now.AddHours(2), ValidUntil = now.AddHours(4) };
            var store = new Store(); var e = new ReminderEngine(store, now, TestZones.China);
            e.SavePlan(habit, now); e.SavePlan(todo, now);
            var interval = e.State.Plans.Single(p => p.Id == habit.Id).NextDue;
            e.SynchronizeTimeZone(alaska, now);
            Check(e.State.Plans.Single(p => p.Id == habit.Id).NextDue == interval);
            var saved = e.State.Plans.Single(p => p.Id == todo.Id);
            Check(saved.OnceAt == todo.OnceAt && saved.ValidUntil == todo.ValidUntil && saved.Revision == 1);
        });
        run("办公时段按新时区重新校验未来间隔", () =>
        {
            var beforeWork = now.AddMinutes(-40);
            var p = new ReminderPlan { Kind = HabitKind.Water, Schedule = ScheduleKind.Interval, WindowStart = "09:00", WindowEnd = "18:00", NextDue = beforeWork.AddMinutes(30) };
            var store = new Store { Value = new() { TimeZoneId = TestZones.China.Id, LastScan = beforeWork, Plans = [p] } };
            var e = new ReminderEngine(store, beforeWork, alaska);
            Check(e.State.Plans.Single().NextDue == now);
        });
        run("相同时区标识下夏令时规则变更仍生效", () =>
        {
            var catalog = TimeZoneInfo.FindSystemTimeZoneById(alaska.Id);
            var e = new ReminderEngine(new Store(), now, catalog);
            Check(e.SynchronizeTimeZone(alaska, now));
            Check(e.Local(now).Offset == TimeSpan.FromHours(-9));
            Check(!e.SynchronizeTimeZone(alaska, now.AddSeconds(1)));
        });
        run("时区保存失败不改变运行时区和扫描起点", () =>
        {
            var store = new Store(); var e = new ReminderEngine(store, now, TestZones.China);
            var oldRules = e.State.TimeZoneRules; store.Fail = true;
            try { e.SynchronizeTimeZone(alaska, now); throw new Exception("错误接受写入失败"); } catch (IOException) { }
            Check(e.Zone.Id == TestZones.China.Id && e.State.TimeZoneRules == oldRules && e.State.LastScan == now);
        });
        run("夏令时跳过的固定时刻顺延到首个存在的分钟", () =>
        {
            var catalog = TimeZoneInfo.FindSystemTimeZoneById(alaska.Id);
            var e = new ReminderEngine(new Store(), now, catalog);
            var at = e.At(new DateOnly(2026, 3, 8), new TimeOnly(2, 30));
            Check(e.Local(at).Hour == 3 && e.Local(at).Minute == 0 && !catalog.IsInvalidTime(e.Local(at).DateTime));
        });
        run("夏令时重复的本地时刻只生成一次固定提醒", () =>
        {
            var catalog = TimeZoneInfo.FindSystemTimeZoneById(alaska.Id);
            var start = new DateTimeOffset(2026, 11, 1, 0, 30, 0, TimeSpan.FromHours(-8));
            var e = new ReminderEngine(new Store(), start, catalog);
            var p = new ReminderPlan { Name = "夏令时测试", Kind = HabitKind.Medication, Schedule = ScheduleKind.FixedTimes, Times = ["01:30"] };
            e.SavePlan(p, start, true);
            var first = e.Evaluate(start.AddHours(1)).Single();
            Check(first.DueAt == start.AddHours(1)); e.Evaluate(start.AddHours(2));
            Check(e.State.Occurrences.Count == 1);
        });
        run("无法识别的旧时区不阻止使用 Windows 时区启动", () =>
        {
            var store = new Store { Value = new() { TimeZoneId = "removed-zone", LastScan = now } };
            var e = new ReminderEngine(store, now);
            Check(e.Zone.HasSameRules(TimeZoneInfo.Local) && store.Value!.TimeZoneId == TimeZoneInfo.Local.Id);
        });
    }
    private sealed class Store : IStateStore
    {
        public AppState? Value; public bool Fail;
        public AppState? Load() => Value == null ? null : StateJson.Clone(Value);
        public void Save(AppState state) { if (Fail) throw new IOException("模拟时区保存失败"); Value = StateJson.Clone(state); }
    }
}
