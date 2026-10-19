using System.Globalization;

namespace Pausewell.Core;

public sealed partial class ReminderEngine
{
    private readonly IStateStore _store;
    private DateTimeOffset _scan;
    private TimeZoneInfo _zone;
    public AppState State { get; private set; }
    public TimeZoneInfo Zone => _zone;

    public ReminderEngine(IStateStore store, DateTimeOffset now, TimeZoneInfo? timeZone = null)
    {
        _store = store;
        _zone = timeZone ?? TimeZoneInfo.Local;
        var loaded = store.Load();
        State = loaded ?? NewState(now);
        if (loaded == null) { State.TimeZoneId = _zone.Id; State.TimeZoneRules = _zone.ToSerializedString(); }
        if (State.SchemaVersion is < 1 or > 4) throw new InvalidOperationException("数据版本不兼容，未覆盖原文件。");
        Migrate();
        _scan = State.LastScan == default ? now : State.LastScan;
        SynchronizeTimeZone(_zone, now);
        if (State.Running) Recover(now, RecoveryReason.Launch);
    }

    public static AppState NewState(DateTimeOffset now) => new()
    {
        LastScan = now,
        LastObservedAt = now,
        Plans = []
    };

    private void Commit(AppState candidate)
    {
        if (candidate.Audit.Count > 500) candidate.Audit = candidate.Audit.TakeLast(500).ToList();
        _store.Save(candidate); // Publish only after durable save. Failed writes do not mutate live state.
        State = candidate;
    }

    private void Change(Action<AppState> action, DateTimeOffset now)
    {
        var candidate = StateJson.Clone(State);
        action(candidate);
        candidate.LastScan = _scan > now ? now : _scan;
        Commit(candidate);
    }

    private static void Log(AppState s, DateTimeOffset now, string action, string detail, string? id = null) => s.Audit.Add(new(now, action, detail, id));
    private static bool Active(Occurrence o) => o.Status is OccurrenceStatus.Pending or OccurrenceStatus.Deferred;
    public DateTimeOffset Local(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, Zone);
    public DateTimeOffset At(DateOnly date, TimeOnly time)
    {
        var wall = date.ToDateTime(time, DateTimeKind.Unspecified);
        // A skipped DST minute moves to the first valid minute; a repeated minute fires once.
        while (Zone.IsInvalidTime(wall)) wall = wall.AddMinutes(1);
        var offset = Zone.IsAmbiguousTime(wall) ? Zone.GetAmbiguousTimeOffsets(wall).Max() : Zone.GetUtcOffset(wall);
        return new DateTimeOffset(wall, offset);
    }

    public void Validate(ReminderPlan p)
    {
        if (string.IsNullOrWhiteSpace(p.Name) || p.Name.Trim().Length > 80) throw new ArgumentException("名称请填写 1 至 80 个字符。");
        if (p.Note.Length > 1000) throw new ArgumentException("说明不能超过 1000 个字符。");
        if (p.Kind == HabitKind.Medication && p.ValidUntil != null) throw new ArgumentException("服药计划不使用待办截止时间。");
        if (p.IsHabit && p.ValidUntil != null) throw new ArgumentException("习惯提醒使用默认有效窗口，不设置待办截止时间。");
        if (p.Schedule == ScheduleKind.Once && p.ValidUntil <= p.OnceAt) throw new ArgumentException("截止时间必须晚于提醒时间。");
        if (p.StartsOn is { } start && p.EndsOn is { } end && end < start) throw new ArgumentException("结束日期不能早于开始日期。");
        if (p.Kind == HabitKind.Medication && p.Schedule != ScheduleKind.FixedTimes) throw new ArgumentException("药物请使用明确固定时刻，不自动换算等间隔。");
        if (p.Kind == HabitKind.Medication && (p.WorkdaysOnly || p.WindowStart != null || p.WindowEnd != null)) throw new ArgumentException("药物不继承工作日或办公时段限制。");
        if (p.Schedule == ScheduleKind.Interval && (p.IntervalMinutes < 1 || p.IntervalMinutes > 1440)) throw new ArgumentException("间隔应在 1 至 1440 分钟之间。");
        if (p.Schedule == ScheduleKind.Once && p.OnceAt == null) throw new ArgumentException("一次性提醒需填写日期和时间。");
        if (p.Schedule == ScheduleKind.Once && (p.WorkdaysOnly || p.WindowStart != null || p.WindowEnd != null || p.StartsOn != null || p.EndsOn != null)) throw new ArgumentException("一次性提醒按指定时刻执行，不继承循环时段限制。");
        if (p.ActivitySeconds < 1 || p.ActivitySeconds > 3600) throw new ArgumentException("活动时长应在 1 至 3600 秒之间。");
        if (p.Schedule == ScheduleKind.FixedTimes)
        {
            if (p.Times.Count == 0 || p.Times.Count > 24 || p.Times.Distinct().Count() != p.Times.Count) throw new ArgumentException("请填写 1 至 24 个不重复的时刻。");
            foreach (var t in p.Times) ParseTime(t);
        }
        if ((p.WindowStart == null) != (p.WindowEnd == null)) throw new ArgumentException("有效时段的开始和结束需同时填写。");
        if (p.WindowStart != null && ParseTime(p.WindowStart) >= ParseTime(p.WindowEnd!)) throw new ArgumentException("本版有效时段需在同一天内，结束晚于开始。");
    }

    public static TimeOnly ParseTime(string value)
    {
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)) throw new ArgumentException("时刻请使用 HH:mm，例如 08:00。");
        return result;
    }

    private bool ValidDate(ReminderPlan p, DateOnly date) => (p.StartsOn == null || date >= p.StartsOn) && (p.EndsOn == null || date <= p.EndsOn) && (!p.WorkdaysOnly || date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
    private bool InWindow(ReminderPlan p, DateTimeOffset at)
    {
        var local = Local(at);
        return ValidDate(p, DateOnly.FromDateTime(local.DateTime)) && (p.WindowStart == null || (TimeOnly.FromDateTime(local.DateTime) >= ParseTime(p.WindowStart) && TimeOnly.FromDateTime(local.DateTime) < ParseTime(p.WindowEnd!)));
    }

    public DateTimeOffset Eligible(ReminderPlan p, DateTimeOffset candidate)
    {
        if (InWindow(p, candidate)) return candidate;
        var local = Local(candidate);
        var date = DateOnly.FromDateTime(local.DateTime);
        if (p.StartsOn is { } first && date < first) date = first;
        for (var i = 0; i < 9; i++, date = date.AddDays(1))
        {
            var start = At(date, p.WindowStart == null ? TimeOnly.MinValue : ParseTime(p.WindowStart));
            if (start >= candidate && ValidDate(p, date)) return start;
        }
        throw new InvalidOperationException("无法计算有效提醒时段。");
    }

    public List<DateTimeOffset> Preview(ReminderPlan p, DateTimeOffset now, int count = 3)
    {
        Validate(p);
        var values = new List<DateTimeOffset>();
        if(p.Schedule==ScheduleKind.Once){if(count>0&&p.Enabled&&p.OnceAt>now)values.Add(p.OnceAt.Value);return values;}
        if (p.Schedule == ScheduleKind.Interval)
        {
            var next = p.NextDue is { } due && due > now ? due : now.AddMinutes(p.IntervalMinutes);
            for (var i = 0; i < count; i++) { next = Eligible(p, next); if(p.ValidUntil<=next)break;values.Add(next); next = next.AddMinutes(p.IntervalMinutes); }
        }
        else
        {
            var date = DateOnly.FromDateTime(Local(now).DateTime);
            if (p.StartsOn is { } first && date < first) date = first;
            for (var day = 0; day < 14 && values.Count < count; day++)
            {
                var d = date.AddDays(day);
                foreach (var t in p.Times.Order())
                {
                    var candidate = At(d, ParseTime(t));
                    if (candidate > now && InWindow(p, candidate) && !(p.ValidUntil<=candidate)) values.Add(candidate);
                    if (values.Count == count) break;
                }
            }
        }
        return values;
    }

    public void SavePlan(ReminderPlan input, DateTimeOffset now, bool startIfNeeded = false)
    {
        Validate(input);
        if (State.DeletedPlans.Any(p => p.Id == input.Id)) throw new InvalidOperationException("该计划已删除，请关闭旧表单后新建提醒。");
        if (input.ValidUntil <= now) throw new ArgumentException("截止时间必须晚于当前时间。");
        if (input.ValidUntil != null && Preview(input, now, 1).Count == 0) throw new ArgumentException("截止前没有可执行的提醒，请调整时刻或截止时间。");
        if(input.Schedule==ScheduleKind.Once&&input.Enabled&&input.OnceAt<=now)throw new ArgumentException("一次性提醒需选择未来的日期和时间。");
        Change(s =>
        {
            var old = s.Plans.FirstOrDefault(p => p.Id == input.Id);
            var copy = System.Text.Json.JsonSerializer.Deserialize<ReminderPlan>(System.Text.Json.JsonSerializer.Serialize(input, StateJson.Options), StateJson.Options)!;
            copy.Name = copy.Name.Trim();
            copy.Purpose ??= copy.IsHabit ? PlanPurpose.Habit : PlanPurpose.Todo;
            copy.Revision = old == null ? 1 : old.Revision + 1;
            copy.EffectiveAt = now;
            copy.NextDue = copy.Schedule == ScheduleKind.Interval ? Eligible(copy, now.AddMinutes(copy.IntervalMinutes)) : null;
            foreach (var item in s.Occurrences.Where(o => o.PlanId == input.Id && (Active(o) || o.Status == OccurrenceStatus.NeedsReview)))
            {
                item.Status = item.Kind == HabitKind.Medication ? OccurrenceStatus.NeedsReview : OccurrenceStatus.Cancelled;
                item.ResolvedAt = item.Kind == HabitKind.Medication ? null : now;
            }
            s.Plans.RemoveAll(p => p.Id == input.Id);
            s.Plans.Add(copy);
            if(startIfNeeded && copy.Enabled) s.Running = true;
            Log(s, now, "计划", $"已保存：{copy.Name}（规则 {copy.Revision}）");
        }, now);
    }

    public void SetRunning(bool running, DateTimeOffset now)
    {
        Change(s =>
        {
            s.Running = running;
            if (running)
                foreach (var p in s.Plans.Where(p => p.Enabled && p.IsHabit && p.Schedule == ScheduleKind.Interval))
                {
                    foreach (var o in s.Occurrences.Where(o => o.PlanId == p.Id && Active(o))) EndOccurrence(o, now, "开始新的提醒时段");
                    p.NextDue = NextHabit(p, now);
                }
            Log(s, now, "运行", running ? "已开始提醒" : "已暂停提醒，未记录任何事项完成");
        }, now);
        if (running) Evaluate(now, recovery: true);
    }

    public void BeginMeeting(DateTimeOffset until, DateTimeOffset now, bool silent = false)
    {
        if (until <= now || until > now.AddHours(12)) throw new ArgumentException("会议时长应在 12 小时以内，且结束时间晚于现在。");
        Change(s => { InterruptActivities(s, now); s.MeetingUntil = until; s.MeetingSilent = silent; s.BufferUntil = null; s.PauseUntil = null; s.PauseMedication = false; s.ShowMeetingSummary = false; Log(s, now, "会议", $"会议模式至 {Local(until):HH:mm}"); }, now);
    }

    public void EndMeeting(DateTimeOffset now) => Change(s => FinishMeeting(s, now), now);
    private void FinishMeeting(AppState s, DateTimeOffset now)
    {
        s.MeetingUntil = null; s.ShowMeetingSummary = true;
        s.BufferUntil = now.AddSeconds(30);
        s.RecoverySummaryVisible = true; s.RecoverySummary = "会议已结束，30 秒后可查看一次当前活动提示。待办和用药记录保留。";
        var activity = s.Plans.FirstOrDefault(p => p.Enabled && p.IsHabit && p.Kind == HabitKind.Activity && InWindow(p, now)
            && (p.NextDue <= now || s.Occurrences.Any(o => o.PlanId == p.Id && Active(o))));
        foreach (var o in s.Occurrences.Where(Active))
        {
            if (Habit(o)) EndOccurrence(o, now, "会议中的旧习惯已结束，未记录完成");
            else { o.NotifyAfter = s.BufferUntil.Value; o.RecoveryOnly = true; o.SuppressStrong = true; }
        }
        foreach (var p in s.Plans.Where(p => p.Enabled && p.IsHabit && p.Schedule == ScheduleKind.Interval)) p.NextDue = NextHabit(p, now);
        if (activity != null)
        {
            var current = Create(activity, s.BufferUntil.Value); current.RecoveryOnly = true; current.SuppressStrong = true;
            s.Occurrences.Add(current);
        }
        Log(s, now, "会议", "会后只保留一个当前活动入口，不补办旧习惯");
    }

    public IReadOnlyList<Occurrence> Evaluate(DateTimeOffset now, bool recovery = false)
    {
        if (State.PauseUntil is { } pauseEnd && pauseEnd <= now) { recovery |= State.PauseMedication; ResumePause(now); }
        return Scan(now, recovery);
    }
    private IReadOnlyList<Occurrence> Scan(DateTimeOffset now, bool recovery, AppState? restored = null)
    {
        var source = restored ?? State;
        if (!source.Running || source.IsAway)
        {
            if (source.MeetingUntil is { } pausedEnd && pausedEnd <= now)
            { var paused = StateJson.Clone(source); FinishMeeting(paused, now); Commit(paused); }
            else if (restored != null) Commit(restored);
            return [];
        }
        var s = StateJson.Clone(source);
        var changed = restored != null;
        if (s.MeetingUntil is { } end && end <= now) { FinishMeeting(s, now); changed = true; }
        var lower = _scan > now ? now : _scan;
        if (lower < now.AddDays(-30)) { lower = now.AddDays(-30); s.RecoveryNotice = "超过 30 天未运行，循环提醒仅补列最近 30 天；一次性提醒保留原待办。"; changed = true; }
        foreach (var p in s.Plans.Where(p => p.Enabled))
        {
            if (p.Kind != HabitKind.Medication && s.PauseUntil > now) continue;
            if (p.EndsOn is { } last && DateOnly.FromDateTime(Local(now).DateTime) > last && p.Schedule == ScheduleKind.Interval) continue;
            if (p.Schedule == ScheduleKind.Interval)
            {
                if (p.NextDue == null) { p.NextDue = Eligible(p, now.AddMinutes(p.IntervalMinutes)); changed = true; }
                if (p.NextDue <= now && (!p.IsHabit || !s.Occurrences.Any(o => o.PlanId == p.Id && Active(o))))
                {
                    if (!InWindow(p, now)) { p.NextDue = Eligible(p, now); changed = true; continue; }
                    var due = p.NextDue!.Value;
                    if (!p.IsHabit && p.ValidUntil <= due) { p.Enabled=false;p.NextDue=null;changed=true;continue; }
                    var created = Create(p, due);
                    if (recovery || !p.IsHabit && due < now - ShortInterruption) { created.RecoveryOnly = true; created.SuppressStrong = true; }
                    s.Occurrences.Add(created); changed = true;
                    if (!p.IsHabit) p.NextDue = Eligible(p, due.AddMinutes(p.IntervalMinutes));
                }
            }
            else if(p.Schedule==ScheduleKind.Once)
            {
                var id=$"{p.Id}:{p.Revision}:once";
                if(p.OnceAt is {} due&&due<=now&&!s.Occurrences.Any(o=>o.Id==id)){var item=Create(p,due);item.Id=id;if(recovery||due<now-ShortInterruption){item.RecoveryOnly=true;item.SuppressStrong=true;}s.Occurrences.Add(item);changed=true;}
            }
            else
            {
                var from = lower > p.EffectiveAt ? lower : p.EffectiveAt;
                if (p.IsHabit && s.HabitSegmentStart is {} segment && segment > from) from = segment;
                var date = DateOnly.FromDateTime(Local(from).DateTime);
                var lastDate = DateOnly.FromDateTime(Local(now).DateTime);
                for (; date <= lastDate; date = date.AddDays(1))
                {
                    foreach (var time in p.Times)
                    {
                        var due = At(date, ParseTime(time));
                        if (p.ValidUntil <= due) continue;
                        var id = $"{p.Id}:{p.Revision}:{date:yyyy-MM-dd}:{time}:{due.UtcTicks}";
                        if (due < from || due > now || due <= p.EffectiveAt || !InWindow(p, due) || s.Occurrences.Any(o => o.Id == id || o.PlanId == p.Id && o.Revision == p.Revision && o.DueAt == due)) continue;
                        var item = Create(p, due); item.Id = id;
                        if ((recovery || due < now - ShortInterruption) && p.Kind == HabitKind.Medication) item.Status = OccurrenceStatus.NeedsReview;
                        if (recovery || !p.IsHabit && due < now - ShortInterruption) { item.RecoveryOnly = true; item.SuppressStrong = true; }
                        s.Occurrences.Add(item); changed = true;
                    }
                }
            }
        }
        if (recovery)
        {
            foreach (var item in s.Occurrences.Where(o => o.Kind == HabitKind.Medication && Active(o) && o.DueAt <= now))
            { item.Status = OccurrenceStatus.NeedsReview; changed = true; }
        }
        foreach (var o in s.Occurrences.Where(Active).ToList())
        {
            var p = s.Plans.FirstOrDefault(p => p.Id == o.PlanId);
            if (o.ExpiresAt <= now || Habit(o) && (s.HabitSegmentStart > o.DueAt || p != null && !InWindow(p, now)))
            {
                EndOccurrence(o, now, "提醒已过期，未记录完成"); changed = true;
                if (p is { IsHabit: true, Schedule: ScheduleKind.Interval }) p.NextDue = NextHabit(p, now);
                if (p?.Schedule == ScheduleKind.Once) p.Enabled = false;
            }
            else if (o.Activity is { State: ActivityState.Interrupted, InterruptedAt: {} interrupted } && now - interrupted >= ShortInterruption)
            { EndActivity(o, now, "超过短中断继续窗口"); changed = true; }
        }
        foreach (var group in s.Occurrences.Where(o => Habit(o) && Active(o)).GroupBy(o => o.PlanId))
        {
            foreach (var older in group.OrderByDescending(o => o.DueAt).Skip(1))
            { EndOccurrence(older, now, "已被当前习惯提醒替代"); changed = true; }
        }
        foreach (var p in s.Plans.Where(p=>p.Enabled&&p.ValidUntil<=now)) { p.Enabled=false;p.NextDue=null;changed=true; }
        if (s.LastObservedAt == null || now - s.LastObservedAt >= TimeSpan.FromSeconds(15)) { s.LastObservedAt = now; changed = true; }
        if (changed) { s.LastScan = now; Commit(s); }
        _scan = now;
        if (State.BufferUntil > now) return [];
        return State.Occurrences.Where(o => Active(o) && !o.RecoveryOnly && o.NotifyAfter <= now
                && (o.Kind == HabitKind.Medication || State.Plans.Any(p => p.Id == o.PlanId && p.Enabled && InWindow(p, now)))
                && (State.MeetingUntil == null || (o.Kind == HabitKind.Medication && o.LastPresentedAt == null)))
            .Where(o => State.PauseUntil == null || (!State.PauseMedication && o.Kind == HabitKind.Medication))
            .Where(o => !(State.MeetingUntil != null && State.MeetingSilent))
            .OrderBy(o => o.DueAt).ToList();
    }

    private static Occurrence Create(ReminderPlan p, DateTimeOffset due) => new()
    {
        PlanId = p.Id, Revision = p.Revision, PlanName = p.Name, Kind = p.Kind, Style = p.Style,
        ActivitySeconds = p.ActivitySeconds, Note = p.Note, DueAt = due, NotifyAfter = due
        , Purpose = p.Purpose ?? (p.IsHabit ? PlanPurpose.Habit : PlanPurpose.Todo), ExpiresAt = Expiry(p, due)
    };

    public void MarkPresented(IEnumerable<string> ids, DateTimeOffset now)
    {
        var set = ids.ToHashSet();
        Change(s => { foreach (var item in s.Occurrences.Where(o => set.Contains(o.Id) && Active(o))) { item.LastPresentedAt = now; item.NotifyAfter = now.AddMinutes(5); Log(s, now, "呈现", "已请求展示，未确认用户已阅读", item.Id); } }, now);
    }

    public void Defer(IEnumerable<string> ids, DateTimeOffset now, int minutes = 5)
    {
        if (minutes < 1 || minutes > 1440) throw new ArgumentException("延期应在 1 至 1440 分钟之间。");
        var set = ids.ToHashSet();
        Change(s => { foreach (var o in s.Occurrences.Where(o => set.Contains(o.Id) && (Active(o) || o.Status==OccurrenceStatus.NeedsReview))) { EndActivity(o, now, "用户延期本次活动"); o.Status = OccurrenceStatus.Deferred; o.RecoveryOnly = false; o.NotifyAfter = now.AddMinutes(minutes); Log(s, now, "延期", $"{minutes} 分钟，未完成", o.Id); } }, now);
    }

    public void Resolve(string id, OccurrenceStatus result, DateTimeOffset now, DateTimeOffset? actualAt = null)
    {
        if (result is not (OccurrenceStatus.Completed or OccurrenceStatus.Skipped or OccurrenceStatus.NeedsReview or OccurrenceStatus.NotNeeded)) throw new ArgumentException("无效的处理结果。");
        if (actualAt > now) throw new ArgumentException("实际执行时间不能晚于当前时间。");
        var current = State.Occurrences.FirstOrDefault(o => o.Id == id) ?? throw new ArgumentException("事项不存在。");
        if (result == OccurrenceStatus.NeedsReview && current.Kind != HabitKind.Medication) throw new ArgumentException("待核实状态仅适用于药物事项。");
        if (result == OccurrenceStatus.NotNeeded && current.Kind != HabitKind.Toilet) throw new ArgumentException("当前无需仅适用于如厕事项。");
        if (current.Status is OccurrenceStatus.Completed or OccurrenceStatus.Skipped or OccurrenceStatus.Cancelled or OccurrenceStatus.NotNeeded or OccurrenceStatus.Expired) return;
        if (current.ExpiresAt <= now) throw new InvalidOperationException("这次提醒已过期，请查看当前安排。");
        Change(s =>
        {
            var o = s.Occurrences.Single(o => o.Id == id);
            EndActivity(o, now, "本人明确处理");
            o.Status = result; o.ResolvedAt = result == OccurrenceStatus.NeedsReview ? null : now;
            o.ActualAt = result == OccurrenceStatus.Completed ? actualAt : null;
            Log(s, now, "处理", $"{o.PlanName}：{Labels.Status(result)}（此为确认时间）", o.Id);
            var plan = s.Plans.FirstOrDefault(p => p.Id == o.PlanId && p.Revision == o.Revision);
            if (plan is { IsHabit: true, Schedule: ScheduleKind.Interval } && result != OccurrenceStatus.NeedsReview) plan.NextDue = NextHabit(plan, now);
            if(plan?.Schedule==ScheduleKind.Once){plan.Enabled=false;plan.NextDue=null;}
        }, now);
    }

    public void Preferences(bool allScreens, bool sound, DateTimeOffset now) => Change(s => { s.CoverAllScreens = allScreens; s.SoundEnabled = sound; }, now);
    public void Checkpoint(DateTimeOffset now) { var s = StateJson.Clone(State); s.LastScan = _scan > now ? now : _scan; Commit(s); }
}
