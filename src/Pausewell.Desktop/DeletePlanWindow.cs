using Pausewell.Presentation;

namespace Pausewell.Desktop;

public sealed class DeletePlanWindow : MaterialWindow
{
    public Button DeleteButton { get; }
    public Button CancelButton { get; }
    public bool Deleted { get; private set; }
    public DeletePlanWindow(DesktopController app, ReminderPlan plan)
    {
        Title = "删除提醒"; Width = 520; Height = 420; MinWidth = 430; MinHeight = 340;
        var body = new StackPanel();
        body.Children.Add(Ui.Text(plan.Name, 20, true, margin: new(0, 0, 0, 12)));
        body.Children.Add(Ui.Text("删除后停止这项计划的所有后续提醒。已有执行记录会保留，可在“记录”中查看。", 14, brush: "Muted"));
        if (plan.Kind == HabitKind.Medication) body.Children.Add(Ui.Notice("未核实的服药记录仍会保留。删除计划不会记为已服用，也不会修改其他药物安排。"));
        else body.Children.Add(Ui.Notice("这项计划尚未处理的事项会取消，不能通过旧编辑窗口重新启用。"));
        var error = TaskUi.Error();
        CancelButton = Ui.Button("保留提醒", Close, id: "CancelDeletePlan"); CancelButton.IsCancel = true;
        DeleteButton = Ui.Button("删除提醒", () =>
        {
            try
            {
                app.Engine.DeletePlan(plan.Id, DateTimeOffset.UtcNow); Deleted = true;
                app.CloseReminders(); app.CurrentQueue?.Close(); app.Main.Feedback("已删除提醒，执行记录已保留。"); Close();
            }
            catch (Exception ex) { error.Text = "删除未保存，提醒仍保留：" + TaskUi.Failure(ex); }
        }, id: "ConfirmDeletePlan");
        DeleteButton.SetResourceReference(ForegroundProperty, "Color.danger");
        var footer = new StackPanel(); footer.Children.Add(error); footer.Children.Add(TaskUi.Actions(CancelButton, DeleteButton));
        SetBody(TaskUi.Layout("删除这项提醒？", "只删除计划，保留历史结果。", body, footer)); Loaded += (_, _) => CancelButton.Focus();
    }
}
