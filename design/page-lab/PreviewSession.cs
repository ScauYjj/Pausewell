using System.IO;
using System.Text.Json;
using Pausewell.Core;
namespace Pausewell.PageLab;
public sealed class PreviewSession
{
 public DateTimeOffset Now {get;}=new(2026,10,16,9,0,0,TimeSpan.FromHours(8));
 public MemoryStateStore Store {get;}=new();
 public ReminderEngine Engine {get;}
 public PreviewSession(bool seed=true)
 {
  Engine=new(Store,Now);if(!seed)return;
  foreach(var (kind,name,minutes) in new[]{(HabitKind.Water,"喝水",30),(HabitKind.Activity,"起来活动",45),(HabitKind.Toilet,"如厕提醒",60)})Engine.SavePlan(new(){Kind=kind,Name=name,Schedule=ScheduleKind.Interval,IntervalMinutes=minutes},Now,true);
  Engine.SavePlan(new(){Kind=HabitKind.Medication,Name="药物 A（示例）",Schedule=ScheduleKind.FixedTimes,Times=["08:00","20:00"],StartsOn=new(2026,10,16),Enabled=false},Now);
 }
 public static ReminderPlan Clone(ReminderPlan p)=>JsonSerializer.Deserialize<ReminderPlan>(JsonSerializer.Serialize(p,StateJson.Options),StateJson.Options)!;
 public sealed class MemoryStateStore:IStateStore
 {
  private AppState? _saved;
  public bool FailNextSave {get;set;}
  public int SaveCount {get;private set;}
  public AppState? Load()=>_saved==null?null:StateJson.Clone(_saved);
  public void Save(AppState state){if(FailNextSave){FailNextSave=false;throw new IOException("演示保存失败，请重试。输入已保留。");}_saved=StateJson.Clone(state);SaveCount++;}
 }
}
