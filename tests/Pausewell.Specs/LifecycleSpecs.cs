using Pausewell.Core;

static class LifecycleSpecs
{
    public static void Run(Action<string, Action> run)
    {
        var at = new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.FromHours(8));
        ReminderPlan Habit() => new() { Name = "起来活动", Kind = HabitKind.Activity, Schedule = ScheduleKind.Interval, IntervalMinutes = 30 };
        ReminderPlan Todo(DateTimeOffset? end = null) => new() { Name = "交材料", Kind = HabitKind.Other, Schedule = ScheduleKind.Once, OnceAt = at.AddMinutes(10), ValidUntil = end };
        (ReminderEngine e, Store s) Boot(params ReminderPlan[] plans)
        {
            var s = new Store(); var e = new ReminderEngine(s, at, TestZones.China);
            foreach (var p in plans) e.SavePlan(p, at, true);
            return (e, s);
        }
        void Check(bool c) { if (!c) throw new Exception("场景规则断言失败"); }
        run("跨周重开不补办旧习惯，仍有效待办保留且不自动全屏", () =>
        {
            var (e, s) = Boot(Habit(), Todo()); e.Evaluate(at.AddMinutes(30)); e.Exit(at.AddMinutes(31));
            var next = at.AddDays(3); var r = new ReminderEngine(s, next, TestZones.China);
            Check(r.State.Occurrences.Single(o => o.Kind == HabitKind.Activity).Status == OccurrenceStatus.Expired);
            Check(r.State.Occurrences.Single(o => o.Kind == HabitKind.Other).Status == OccurrenceStatus.Pending);
            Check(r.State.Plans.Single(p => p.IsHabit).NextDue == next.AddMinutes(30));
            Check(r.Evaluate(next).Count == 0 && r.State.RecoverySummaryVisible);
        });
        run("短锁屏保留未来安排，多次返回不重复事项", () =>
        {
            var (e, _) = Boot(Habit()); e.MarkUnavailable(at.AddMinutes(10));
            e.Recover(at.AddMinutes(11), RecoveryReason.SystemReturn); e.Recover(at.AddMinutes(11), RecoveryReason.SystemReturn);
            Check(e.State.Plans.Single().NextDue == at.AddMinutes(30) && e.State.Occurrences.Count == 0);
        });
        run("短崩溃恢复不延后未来时刻", () =>
        {
            var (e, s) = Boot(Habit()); e.Evaluate(at.AddMinutes(10));
            var r = new ReminderEngine(s, at.AddMinutes(11), TestZones.China); Check(r.State.Plans.Single().NextDue == at.AddMinutes(30));
        });
        run("主动离开无补办，会议到点也不越过离开状态", () =>
        {
            var (e, _) = Boot(Habit()); e.BeginMeeting(at.AddHours(1), at); e.MarkUnavailable(at.AddMinutes(10), true);
            Check(e.Evaluate(at.AddHours(2)).Count == 0);
            e.Recover(at.AddHours(2), RecoveryReason.ManualReturn);
            Check(!e.State.IsAway && e.State.Plans.Single().NextDue == at.AddHours(2).AddMinutes(30));
        });
        run("有截止的一次性待办过期，无截止持续保留", () =>
        {
            var (e, _) = Boot(Todo(at.AddHours(1))); e.Evaluate(at.AddHours(2));
            Check(e.State.Occurrences.Single().Status == OccurrenceStatus.Expired && !e.State.Plans.Single().Enabled);
            var (other, _) = Boot(Todo()); other.Evaluate(at.AddDays(40)); Check(other.State.Occurrences.Single().Status == OccurrenceStatus.Pending);
        });
        run("同一固定时刻待办各次独立，不被普通习惯合并", () =>
        {
            var (e, _) = Boot(new ReminderPlan { Name = "交接", Kind = HabitKind.Other, Schedule = ScheduleKind.FixedTimes, Times = ["09:10", "09:20"] });
            e.Evaluate(at.AddMinutes(25)); Check(e.State.Occurrences.Count(o => o.Status == OccurrenceStatus.Pending) == 2);
        });
        run("活动中断保留45秒，不把离线算活动且不自动开始", () =>
        {
            var (e, s) = Boot(Habit()); var due = at.AddMinutes(30); var o = e.Evaluate(due).Single();
            e.StartActivity(o.Id, due); e.MarkUnavailable(due.AddSeconds(45));
            e.Recover(due.AddMinutes(2), RecoveryReason.SystemReturn);
            Check(e.ActivityElapsed(o.Id, due.AddMinutes(2)) == 45 && e.State.Occurrences.Single().Activity!.State == ActivityState.Interrupted);
            e.StartActivity(o.Id, due.AddMinutes(2)); Check(e.ActivityElapsed(o.Id, due.AddMinutes(2).AddSeconds(75)) == 120);
            Check(e.State.Occurrences.Single().Status != OccurrenceStatus.Completed);
        });
        run("活动无需重新计时也能明确自报完成", () =>
        {
            var (e, _) = Boot(Habit()); var due = at.AddMinutes(30); var o = e.Evaluate(due).Single();
            e.Resolve(o.Id, OccurrenceStatus.Completed, due, due); Check(e.State.Occurrences.Single().Status == OccurrenceStatus.Completed);
        });
        run("较长离开结束旧活动，不重开倒计时或假记完成", () =>
        {
            var (e, _) = Boot(Habit()); var due = at.AddMinutes(30); var o = e.Evaluate(due).Single(); e.StartActivity(o.Id, due);
            e.MarkUnavailable(due.AddSeconds(45)); e.Recover(due.AddHours(2), RecoveryReason.SystemReturn);
            Check(e.State.Occurrences.Single().Status == OccurrenceStatus.Expired && e.State.Occurrences.Single().Activity!.State == ActivityState.Ended);
        });
        run("活动计时可超过短提醒间隔，不因1分钟窗口中途丢失", () =>
        {
            var p = Habit(); p.IntervalMinutes = 1; var (e, _) = Boot(p); var due = at.AddMinutes(1); var o = e.Evaluate(due).Single();
            e.StartActivity(o.Id, due); e.Evaluate(due.AddSeconds(120)); Check(e.State.Occurrences.Single().Status == OccurrenceStatus.Pending);
        });
        run("延期不让过时习惯永久有效", () =>
        {
            var (e, _) = Boot(Habit()); var due = at.AddMinutes(30); var o = e.Evaluate(due).Single();
            e.Defer([o.Id], due, 60); e.Evaluate(due.AddMinutes(16)); Check(e.State.Occurrences.Single().Status == OccurrenceStatus.Expired);
        });
        foreach (var kind in new[] { HabitKind.Water, HabitKind.Other, HabitKind.Medication })
        run($"删除{kind}停止后续并保留历史", () =>
        {
            var p = kind == HabitKind.Medication ? new ReminderPlan { Name = "演示药物", Kind = kind, Schedule = ScheduleKind.FixedTimes, Times = ["09:10", "20:00"] }
                : kind == HabitKind.Other ? Todo() : new ReminderPlan { Name = "喝水", Kind = kind, Schedule = ScheduleKind.Interval, IntervalMinutes = 10 };
            var (e, s) = Boot(p); e.Evaluate(at.AddMinutes(10)); e.DeletePlan(p.Id, at.AddMinutes(11)); e.DeletePlan(p.Id, at.AddMinutes(11));
            Check(e.State.Plans.Count == 0 && e.State.DeletedPlans.Count == 1 && e.State.Occurrences.Count == 1);
            Check(e.State.Occurrences.Single().Status == (kind == HabitKind.Medication ? OccurrenceStatus.NeedsReview : OccurrenceStatus.Cancelled));
            Check(new ReminderEngine(s, at.AddDays(1), TestZones.China).Evaluate(at.AddDays(1)).Count == 0);
            try { e.SavePlan(p, at.AddMinutes(12)); throw new Exception("旧表单复活了已删除计划"); } catch (InvalidOperationException) { }
        });
        run("删除失败原子保留计划和待办", () =>
        {
            var (e, s) = Boot(Todo()); e.Evaluate(at.AddMinutes(10)); s.Fail = true;
            try { e.DeletePlan(e.State.Plans.Single().Id, at.AddMinutes(11)); throw new Exception("未报告失败"); } catch (IOException) { }
            Check(e.State.Plans.Count == 1 && e.State.DeletedPlans.Count == 0 && e.State.Occurrences.Single().Status == OccurrenceStatus.Pending);
        });
        run("删除服药计划仍可核对历史记录", () =>
        {
            var p = new ReminderPlan { Name = "演示药物", Kind = HabitKind.Medication, Schedule = ScheduleKind.FixedTimes, Times = ["09:10"] };
            var (e, _) = Boot(p); e.Evaluate(at.AddMinutes(10)); e.DeletePlan(p.Id, at.AddMinutes(11));
            e.Resolve(e.State.Occurrences.Single().Id, OccurrenceStatus.Completed, at.AddMinutes(12), at.AddMinutes(10));
            Check(e.State.Plans.Count == 0 && e.State.Occurrences.Single().Status == OccurrenceStatus.Completed);
        });
        run("恢复保存失败不发布半套新状态", () =>
        {
            var (e, s) = Boot(Habit()); var before = e.State.Plans.Single().NextDue; s.Fail = true;
            try { e.Recover(at.AddHours(2), RecoveryReason.SystemReturn); throw new Exception("未报告失败"); } catch (IOException) { }
            Check(e.State.Plans.Single().NextDue == before && !e.State.RecoverySummaryVisible);
        });
        run("旧自定义保守迁为待办，已完成历史不丢", () =>
        {
            var p = Todo(); var s = new Store { Value = new() { TimeZoneId = TestZones.China.Id, SchemaVersion = 3, LastScan = at, Plans = [p], Occurrences = [new() { PlanId = p.Id, Kind = HabitKind.Other, Status = OccurrenceStatus.Completed }] } };
            var e = new ReminderEngine(s, at, TestZones.China); Check(e.State.SchemaVersion == 4 && !e.State.Plans.Single().IsHabit && e.State.Occurrences.Single().Status == OccurrenceStatus.Completed);
        });
        run("恢复和补列在一个保存事务里，不发布中间结果", () =>
        {
            var (e,s)=Boot(Habit(),Todo());var before=s.Saves;e.Recover(at.AddHours(2),RecoveryReason.SystemReturn);
            Check(s.Saves==before+1&&e.State.Occurrences.Any(o=>o.Kind==HabitKind.Other));
        });
        run("截止后循环待办停止后续生成", () =>
        {
            var p=new ReminderPlan{Name="临时交接",Kind=HabitKind.Other,Schedule=ScheduleKind.Interval,IntervalMinutes=10,ValidUntil=at.AddMinutes(25)};
            var (e,_)=Boot(p);e.Evaluate(at.AddMinutes(10));e.Evaluate(at.AddMinutes(20));e.Evaluate(at.AddMinutes(30));var count=e.State.Occurrences.Count;
            e.Evaluate(at.AddDays(1));Check(!e.State.Plans.Single().Enabled&&e.State.Occurrences.Count==count&&e.State.Occurrences.All(o=>o.Status==OccurrenceStatus.Expired));
        });
        run("已结束日期的习惯计划不阻断软件启动", () =>
        {
            var p=Habit();p.EndsOn=DateOnly.FromDateTime(at.DateTime);var(e,s)=Boot(p);e.Exit(at.AddHours(1));
            var r=new ReminderEngine(s,at.AddDays(3), TestZones.China);Check(r.Evaluate(at.AddDays(3)).Count==0);
        });
    }
    private sealed class Store : IStateStore
    {
        public AppState? Value; public bool Fail; public int Saves;
        public AppState? Load() => Value == null ? null : StateJson.Clone(Value);
        public void Save(AppState state) { Saves++;if (Fail) throw new IOException("模拟保存失败"); Value = StateJson.Clone(state); }
    }
}
