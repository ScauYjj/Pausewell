namespace Pausewell.Core;

public sealed partial class ReminderEngine
{
    public static readonly string[] ThemeIds = ["champagne", "violet", "copper", "rose", "blue"];
    public void SavePreferences(UserPreferences preferences, bool allScreens, bool sound, DateTimeOffset now)
    {
        if (!ThemeIds.Contains(preferences.Theme)) throw new ArgumentException("无法识别该主题。");
        Change(s => { s.Preferences = new() { Theme = preferences.Theme, HideMedicationNames = preferences.HideMedicationNames, CloseToTray = preferences.CloseToTray, NewOrdinaryOfficeHours = preferences.NewOrdinaryOfficeHours, StartupEnabled = preferences.StartupEnabled }; s.CoverAllScreens = allScreens; s.SoundEnabled = sound; }, now);
    }
    public void ResumeOnLaunch(DateTimeOffset now)
    {
        if (!State.Running && State.Plans.Any(p => p.Enabled)) SetRunning(true, now);
        if (State.IsAway) Recover(now, RecoveryReason.Launch);
    }
    public void SetEnabled(string id, bool enabled, DateTimeOffset now)
    {
        Change(s =>
        {
            var p = s.Plans.Single(p => p.Id == id); p.Enabled = enabled;
            if(enabled&&p.Schedule==ScheduleKind.Once&&(p.OnceAt<=now||s.Occurrences.Any(o=>o.PlanId==id&&o.Revision==p.Revision)))throw new ArgumentException("这次提醒已到期或已处理，请编辑并设置新的未来时间。");
            if (enabled && p.Schedule == ScheduleKind.Interval) p.NextDue = Eligible(p, now.AddMinutes(p.IntervalMinutes));
            if (!enabled)
                foreach (var o in s.Occurrences.Where(o => o.PlanId == id && Active(o)))
                { o.Status = o.Kind == HabitKind.Medication ? OccurrenceStatus.NeedsReview : OccurrenceStatus.Cancelled; o.ResolvedAt = o.Kind == HabitKind.Medication ? null : now; }
            Log(s, now, "启停", $"{p.Name}：{(enabled ? "开启" : "关闭")}");
        }, now);
    }
    public void BeginPause(DateTimeOffset until, bool includeMedication, DateTimeOffset now)
    {
        if (until <= now || until > now.AddHours(12)) throw new ArgumentException("暂停时长应在 1 至 720 分钟之间。");
        Change(s => { InterruptActivities(s, now); s.PauseUntil = until; s.PauseMedication = includeMedication; s.MeetingUntil = null; s.BufferUntil = null; s.ShowMeetingSummary = false; Log(s, now, "暂停", $"普通提醒暂停至 {Local(until):HH:mm}" + (includeMedication ? "，同时暂停服药提示" : "，服药按原计划提示")); }, now);
    }
    public void ResumePause(DateTimeOffset now)
    {
        Change(s => ResumePauseState(s, now), now);
    }
    private void ResumePauseState(AppState s, DateTimeOffset now)
    {
            var medsPaused = s.PauseMedication;
            s.PauseUntil = null; s.PauseMedication = false;
            s.HabitSegmentStart = now;
            foreach (var p in s.Plans.Where(p => p.Enabled && p.IsHabit && p.Schedule == ScheduleKind.Interval))
                p.NextDue = NextHabit(p, now);
            foreach (var o in s.Occurrences.Where(Active))
            {
                if (Habit(o)) EndOccurrence(o, now, "暂停结束，旧习惯不补办");
                else if (o.Kind != HabitKind.Medication) { o.RecoveryOnly = true; o.SuppressStrong = true; }
                else if (medsPaused && o.DueAt <= now) o.Status = OccurrenceStatus.NeedsReview;
            }
            Log(s, now, "恢复", "普通间隔从恢复时刻重新计算，药物时刻保留");
    }
    public void ConfigureMeeting(bool silent, DateTimeOffset now) => Change(s => { s.MeetingSilent = silent; s.PauseUntil = null; s.PauseMedication = false; }, now);
    public void DismissMeetingSummary(DateTimeOffset now) => Change(s => s.ShowMeetingSummary = false, now);
    public void CorrectRecord(string id, OccurrenceStatus status, DateTimeOffset? actualAt, string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("请填写更正原因。");
        if (reason.Trim().Length > 500) throw new ArgumentException("更正原因不能超过 500 字。");
        if (actualAt > now) throw new ArgumentException("实际执行时间不能晚于当前时间。");
        var old = State.Occurrences.Single(o => o.Id == id);
        if (old.Status is OccurrenceStatus.Pending or OccurrenceStatus.Deferred) throw new ArgumentException("待处理事项请先处理，再更正记录。");
        if (status is not (OccurrenceStatus.Completed or OccurrenceStatus.Skipped or OccurrenceStatus.NeedsReview or OccurrenceStatus.NotNeeded)) throw new ArgumentException("不支持该更正状态。");
        if (status == OccurrenceStatus.NeedsReview && old.Kind != HabitKind.Medication) throw new ArgumentException("待核实仅适用于药物。");
        if (status == OccurrenceStatus.NotNeeded && old.Kind != HabitKind.Toilet) throw new ArgumentException("当前无需仅适用于如厕。");
        Change(s =>
        {
            var o = s.Occurrences.Single(o => o.Id == id);
            var actual = status == OccurrenceStatus.Completed ? actualAt : null;
            o.Corrections.Add(new(now, o.Status, o.ActualAt, o.ResolvedAt, status, actual, reason.Trim()));
            o.Status = status; o.ActualAt = actual; o.ResolvedAt = status == OccurrenceStatus.NeedsReview ? null : now;
            Log(s, now, "更正", reason.Trim(), id);
        }, now);
    }
}
