namespace Pausewell.Core;

public sealed partial class ReminderEngine
{
    public bool SynchronizeTimeZone(TimeZoneInfo zone, DateTimeOffset now)
    {
        var rules = zone.ToSerializedString();
        var changed = State.TimeZoneId != zone.Id || State.TimeZoneRules.Length > 0 && State.TimeZoneRules != rules;
        if (!changed)
        {
            _zone = zone;
            State.TimeZoneRules = rules;
            return false;
        }

        var previous = _zone;
        _zone = zone;
        try
        {
            var candidate = StateJson.Clone(State);
            candidate.TimeZoneId = zone.Id;
            candidate.TimeZoneRules = rules;
            foreach (var p in candidate.Plans.Where(p => p.Enabled && p.Schedule == ScheduleKind.Interval))
            {
                if (p.EndsOn is {} end && end < DateOnly.FromDateTime(Local(now).DateTime)) continue;
                if (p.NextDue is {} due && due > now && !InWindow(p, due))
                    p.NextDue = Eligible(p, due);
            }
            // Keep absolute timestamps and records. Do not replay past fixed times in the new zone.
            candidate.LastScan = now;
            candidate.RecoveryNotice = "时间已跟随 Windows 系统时区，历史记录和一次性提醒的原定时间点保留。";
            Log(candidate, now, "时区", "已跟随 Windows 系统时区；后续固定时刻和办公时段按系统时间计算。");
            Commit(candidate);
            _scan = now;
            return true;
        }
        catch { _zone = previous; throw; }
    }
}
