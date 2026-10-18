using System.Text.Json;
using Pausewell.Core;
namespace Pausewell.Presentation;
public interface IPlanContext
{
 ReminderEngine Engine {get;}
 DateTimeOffset Now {get;}
}
public static class PlanDraft
{
 public static ReminderPlan Clone(ReminderPlan plan)=>JsonSerializer.Deserialize<ReminderPlan>(JsonSerializer.Serialize(plan,StateJson.Options),StateJson.Options)!;
}
