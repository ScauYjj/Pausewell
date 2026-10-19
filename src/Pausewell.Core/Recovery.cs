namespace Pausewell.Core;

public sealed partial class ReminderEngine
{
    public static readonly TimeSpan ShortInterruption = TimeSpan.FromMinutes(5);
    private static bool Habit(Occurrence o) => o.Kind != HabitKind.Medication && (o.Purpose ?? (o.Kind == HabitKind.Other ? PlanPurpose.Todo : PlanPurpose.Habit)) == PlanPurpose.Habit;
    private void Migrate()
    {
        foreach (var p in State.Plans.Concat(State.DeletedPlans)) p.Purpose ??= p.IsHabit ? PlanPurpose.Habit : PlanPurpose.Todo;
        foreach (var o in State.Occurrences)
        {
            var p = State.Plans.Concat(State.DeletedPlans).FirstOrDefault(p => p.Id == o.PlanId);
            o.Purpose ??= p?.Purpose ?? (o.Kind == HabitKind.Other ? PlanPurpose.Todo : PlanPurpose.Habit);
            o.ExpiresAt ??= p is not null ? Expiry(p, o.DueAt) : null;
        }
        State.SchemaVersion = 4;
    }
    private static DateTimeOffset? Expiry(ReminderPlan p, DateTimeOffset due) => p.Kind == HabitKind.Medication ? null : p.IsHabit
        ? due.AddMinutes(p.Schedule == ScheduleKind.Interval ? Math.Min(p.IntervalMinutes, 15) : 15)
        : p.ValidUntil;
    private static void EndOccurrence(Occurrence o, DateTimeOffset now, string reason)
    {
        o.Status = OccurrenceStatus.Expired; o.ResolvedAt = now; o.EndReason = reason;
        EndActivity(o, now, reason);
    }
    private static void EndActivity(Occurrence o, DateTimeOffset now, string reason)
    {
        if (o.Activity is not { State: not ActivityState.Ended } a) return;
        Accumulate(a, now); a.State = ActivityState.Ended; a.StartedAt = null; a.EndReason = reason;
    }
    private static void Accumulate(ActivitySession a, DateTimeOffset now)
    {
        if (a.State == ActivityState.Running && a.StartedAt is {} start)
            a.AccumulatedSeconds += Math.Max(0, (now - start).TotalSeconds);
        a.StartedAt = null;
    }
    private static void InterruptActivities(AppState s, DateTimeOffset now)
    {
        foreach (var o in s.Occurrences.Where(o => o.Activity?.State == ActivityState.Running))
        {
            var a = o.Activity!; Accumulate(a, now); a.State = ActivityState.Interrupted; a.InterruptedAt = now;
        }
    }
    private DateTimeOffset NextHabit(ReminderPlan p, DateTimeOffset now)
    {
        if (p.EndsOn is {} end && DateOnly.FromDateTime(Local(now).Date) > end) return p.NextDue ?? now;
        if (!InWindow(p, now))
        {
            var next = Eligible(p, now).AddMinutes(p.IntervalMinutes);
            return InWindow(p, next) ? next : Eligible(p, next);
        }
        return Eligible(p, now.AddMinutes(p.IntervalMinutes));
    }
    public void MarkUnavailable(DateTimeOffset now, bool manual = false)
    {
        Change(s => { s.UnavailableSince ??= now; s.IsAway |= manual; s.LastObservedAt = now; InterruptActivities(s, now); Log(s, now, "离开", manual ? "主动离开，返回后重新安排习惯" : "系统中断，暂停呈现"); }, now);
    }
    public void Recover(DateTimeOffset now, RecoveryReason reason)
    {
        if (reason == RecoveryReason.SystemReturn && State.UnavailableSince == null && State.LastRecoveryAt is {} previous && now >= previous && now - previous < TimeSpan.FromSeconds(1)) return;
        var last = State.UnavailableSince ?? State.LastObservedAt ?? State.LastScan;
        var newSegment = reason == RecoveryReason.ManualReturn || State.IsAway
            || now - last >= ShortInterruption || Local(now).Date != Local(last).Date
            || reason == RecoveryReason.Launch && State.CleanExit;
        var changed = StateJson.Clone(State);
        // A crashed running timer is only credited up to the last observed heartbeat.
        InterruptActivities(changed, last > now ? now : last);
        changed.UnavailableSince = null; changed.IsAway = false; changed.CleanExit = false;
        changed.LastObservedAt = now;
        changed.LastRecoveryAt = now;
        if (changed.PauseUntil <= now) ResumePauseState(changed, now);
        if (newSegment)
        {
            changed.HabitSegmentStart = now;
            foreach (var p in changed.Plans.Where(p => p.Enabled && p.IsHabit && p.Schedule == ScheduleKind.Interval)) p.NextDue = NextHabit(p, now);
        }
        foreach (var o in changed.Occurrences.Where(o => Active(o) || o.Status == OccurrenceStatus.NeedsReview))
        {
            if (Habit(o) && (newSegment || o.ExpiresAt <= now)) EndOccurrence(o, now, "离开或提醒已过时，未记录完成");
            else
            {
                o.RecoveryOnly = true; o.SuppressStrong = true;
                if (o.Kind == HabitKind.Medication && o.DueAt <= now) o.Status = OccurrenceStatus.NeedsReview;
            }
        }
        changed.RecoverySummaryVisible = changed.Plans.Count > 0 || changed.Occurrences.Any(o => o.Status == OccurrenceStatus.NeedsReview);
        changed.RecoverySummary = newSegment ? "日常提醒已重新安排，旧习惯不需要补做。待办与待核实记录保留。" : "提醒已继续，尚未到期的安排保持。";
        Log(changed, now, "恢复", changed.RecoverySummary);
        Scan(now, recovery: true, restored: changed);
    }
    public void DismissRecovery(DateTimeOffset now) => Change(s => { s.RecoverySummaryVisible = false; s.ShowMeetingSummary = false; }, now);
    public void DeletePlan(string id, DateTimeOffset now)
    {
        if (State.DeletedPlans.Any(p => p.Id == id)) return;
        Change(s =>
        {
            var p = s.Plans.Single(p => p.Id == id); s.Plans.Remove(p); p.Enabled = false; s.DeletedPlans.Add(p);
            foreach (var o in s.Occurrences.Where(o => o.PlanId == id && Active(o)))
            {
                EndActivity(o, now, "计划已删除");
                o.Status = o.Kind == HabitKind.Medication ? OccurrenceStatus.NeedsReview : OccurrenceStatus.Cancelled;
                o.ResolvedAt = o.Kind == HabitKind.Medication ? null : now; o.EndReason = "计划已删除，历史保留";
                o.RecoveryOnly = true;
            }
            Log(s, now, "删除", $"已删除计划：{p.Name}；停止后续提醒，保留执行及待核实记录");
        }, now);
    }
    public void StartActivity(string id, DateTimeOffset now)
    {
        Change(s =>
        {
            var o = s.Occurrences.Single(o => o.Id == id);
            if (!Active(o) || o.Kind != HabitKind.Activity || o.ExpiresAt <= now) throw new InvalidOperationException("本次活动提醒已结束。");
            var a = o.Activity;
            if (a?.State == ActivityState.Running) return;
            if (a?.State == ActivityState.Ended || a?.InterruptedAt is {} at && now - at >= ShortInterruption) o.Activity = null;
            o.Activity ??= new() { OccurrenceId = id };
            o.Activity.State = ActivityState.Running; o.Activity.StartedAt = now; o.Activity.InterruptedAt = null;
            if (o.ExpiresAt is {} expiry && expiry < now.AddSeconds(o.ActivitySeconds - o.Activity.AccumulatedSeconds).Add(ShortInterruption))
                o.ExpiresAt = now.AddSeconds(o.ActivitySeconds - o.Activity.AccumulatedSeconds).Add(ShortInterruption);
            Log(s, now, "活动", "本人开始或继续辅助计时", id);
        }, now);
    }
    public void PauseActivity(string id, DateTimeOffset now) => Change(s =>
    {
        var o = s.Occurrences.Single(o => o.Id == id);
        if (o.Activity is not { State: ActivityState.Running } a) return;
        Accumulate(a, now); a.State = ActivityState.Interrupted; a.InterruptedAt = now;
    }, now);
    public double ActivityElapsed(string id, DateTimeOffset now)
    {
        var a = State.Occurrences.FirstOrDefault(o => o.Id == id)?.Activity;
        return a == null ? 0 : a.AccumulatedSeconds + (a.State == ActivityState.Running && a.StartedAt is {} at ? Math.Max(0, (now - at).TotalSeconds) : 0);
    }
    public void Exit(DateTimeOffset now) => Change(s => { InterruptActivities(s, now); s.LastObservedAt = now; s.CleanExit = true; }, now);
}
