using System.Windows.Automation;
using Pausewell.Presentation;
namespace Pausewell.Desktop;
public sealed class PlanEditor:PlanForm
{
 private sealed class Context(DesktopController controller):IPlanContext
 {
  public ReminderEngine Engine=>controller.Engine;
  public DateTimeOffset Now=>DateTimeOffset.UtcNow;
 }
 public string ValidationMessage=>SummaryError.Text;
 public PlanEditor(DesktopController app,ReminderPlan? plan,HabitKind? kind=null)
  :base(new Context(app),plan,(plan?.Kind??kind)==HabitKind.Medication,kind)
 {
  AutomationProperties.SetAutomationId(NameInput,"PlanName");AutomationProperties.SetAutomationId(IntervalInput,"IntervalMinutes");AutomationProperties.SetAutomationId(DurationInput,"ActivitySeconds");AutomationProperties.SetAutomationId(NoteInput,"PlanNote");AutomationProperties.SetAutomationId(SaveButton,"SavePlan");AutomationProperties.SetAutomationId(CancelButton,"CancelPlan");
 }
 public void Save()=>TrySave();
}