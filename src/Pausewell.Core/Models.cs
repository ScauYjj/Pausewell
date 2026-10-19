using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pausewell.Core;

public enum HabitKind { Activity, Water, Toilet, Medication, Other }
public enum ScheduleKind { Interval, FixedTimes, Once }
public enum ReminderStyle { Gentle, Card, Fullscreen }
public enum PlanPurpose { Habit, Todo }
public enum OccurrenceStatus { Pending, Deferred, Completed, Skipped, NeedsReview, Cancelled, NotNeeded, Expired }
public enum RecoveryReason { Launch, SystemReturn, ManualReturn }
public enum ActivityState { Running, Interrupted, Finished, Ended }
public sealed class ActivitySession
{
    public string OccurrenceId { get; set; } = "";
    public ActivityState State { get; set; }
    public double AccumulatedSeconds { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? InterruptedAt { get; set; }
    public string EndReason { get; set; } = "";
}

public sealed class ReminderPlan
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Revision { get; set; } = 1;
    public string Name { get; set; } = "";
    public HabitKind Kind { get; set; }
    public PlanPurpose? Purpose { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
    [JsonIgnore] public bool IsHabit => Kind != HabitKind.Medication && (Purpose ?? (Kind == HabitKind.Other ? PlanPurpose.Todo : PlanPurpose.Habit)) == PlanPurpose.Habit;
    public ScheduleKind Schedule { get; set; }
    public ReminderStyle Style { get; set; } = ReminderStyle.Card;
    public int IntervalMinutes { get; set; } = 30;
    public List<string> Times { get; set; } = [];
    public DateTimeOffset? OnceAt { get; set; }
    public bool Enabled { get; set; } = true;
    public bool WorkdaysOnly { get; set; }
    public string? WindowStart { get; set; }
    public string? WindowEnd { get; set; }
    public int ActivitySeconds { get; set; } = 120;
    public string Note { get; set; } = "";
    public DateTimeOffset EffectiveAt { get; set; }
    public DateTimeOffset? NextDue { get; set; }
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
}

public sealed class Occurrence
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PlanId { get; set; } = "";
    public int Revision { get; set; }
    public string PlanName { get; set; } = "";
    public HabitKind Kind { get; set; }
    public ReminderStyle Style { get; set; }
    public string Note { get; set; } = "";
    public int ActivitySeconds { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset NotifyAfter { get; set; }
    public DateTimeOffset? LastPresentedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public OccurrenceStatus Status { get; set; }
    public DateTimeOffset? ActualAt { get; set; }
    public PlanPurpose? Purpose { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool RecoveryOnly { get; set; }
    public bool SuppressStrong { get; set; }
    public string EndReason { get; set; } = "";
    public ActivitySession? Activity { get; set; }
    public List<RecordCorrection> Corrections { get; set; } = [];
}

public sealed record RecordCorrection(DateTimeOffset At, OccurrenceStatus Before, DateTimeOffset? BeforeActual, DateTimeOffset? BeforeConfirmed, OccurrenceStatus After, DateTimeOffset? AfterActual, string Reason);

public sealed class UserPreferences
{
    public string Theme { get; set; } = "champagne";
    public bool HideMedicationNames { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool NewOrdinaryOfficeHours { get; set; }
    public bool StartupEnabled { get; set; }
}

public sealed record AuditEntry(DateTimeOffset At, string Action, string Detail, string? OccurrenceId = null);

public sealed class AppState
{
    public int SchemaVersion { get; set; } = 4;
    public string TimeZoneId { get; set; } = "China Standard Time";
    public bool Running { get; set; }
    public bool CoverAllScreens { get; set; }
    public bool SoundEnabled { get; set; }
    public DateTimeOffset LastScan { get; set; }
    public DateTimeOffset? LastObservedAt { get; set; }
    public bool CleanExit { get; set; }
    public DateTimeOffset? UnavailableSince { get; set; }
    public bool IsAway { get; set; }
    public DateTimeOffset? HabitSegmentStart { get; set; }
    public DateTimeOffset? LastRecoveryAt { get; set; }
    public bool RecoverySummaryVisible { get; set; }
    public string RecoverySummary { get; set; } = "";
    public DateTimeOffset? MeetingUntil { get; set; }
    public DateTimeOffset? BufferUntil { get; set; }
    public DateTimeOffset? PauseUntil { get; set; }
    public bool PauseMedication { get; set; }
    public bool MeetingSilent { get; set; }
    public bool ShowMeetingSummary { get; set; }
    public UserPreferences Preferences { get; set; } = new();
    public string RecoveryNotice { get; set; } = "";
    public List<ReminderPlan> Plans { get; set; } = [];
    public List<ReminderPlan> DeletedPlans { get; set; } = [];
    public List<Occurrence> Occurrences { get; set; } = [];
    public List<AuditEntry> Audit { get; set; } = [];
}

public interface IStateStore
{
    AppState? Load();
    void Save(AppState state);
}

public static class StateJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() }, WriteIndented = false
    };
    public static AppState Clone(AppState value) => JsonSerializer.Deserialize<AppState>(JsonSerializer.Serialize(value, Options), Options)!;
}

public static class Labels
{
    public static string Kind(HabitKind k) => k switch { HabitKind.Activity => "活动", HabitKind.Water => "喝水", HabitKind.Toilet => "如厕", HabitKind.Medication => "服药", _ => "自定义" };
    public static string Status(OccurrenceStatus s) => s switch { OccurrenceStatus.Pending => "待处理", OccurrenceStatus.Deferred => "已延期", OccurrenceStatus.Completed => "已确认完成", OccurrenceStatus.Skipped => "明确未执行", OccurrenceStatus.NeedsReview => "待核实", OccurrenceStatus.NotNeeded => "当前无需", OccurrenceStatus.Expired => "已过期", _ => "已取消" };
    public static string Style(ReminderStyle s) => s switch { ReminderStyle.Gentle => "托盘轻提醒", ReminderStyle.Fullscreen => "全屏提醒", _ => "醒目弹窗" };
}
