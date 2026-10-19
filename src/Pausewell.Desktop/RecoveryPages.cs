namespace Pausewell.Desktop;

public partial class MainWindow
{
    private void AddRecoverySurfaces(StackPanel body)
    {
        var s = _app.Engine.State; var now = DateTimeOffset.UtcNow;
        if (s.RecoverySummaryVisible)
        {
            var copy = new StackPanel(); copy.Children.Add(Ui.Text("欢迎回来，提醒已整理", 16, true));
            copy.Children.Add(Ui.Text(s.RecoverySummary, 13, brush: "Muted", margin: new(0, 6, 0, 12)));
            var todo = s.Occurrences.Count(o => o.Purpose == PlanPurpose.Todo && o.Kind != HabitKind.Medication && o.Status is OccurrenceStatus.Pending or OccurrenceStatus.Deferred);
            var meds = s.Occurrences.Count(o => o.Kind == HabitKind.Medication && o.Status == OccurrenceStatus.NeedsReview);
            var summaryActions = Ui.Actions();
            if (todo > 0) summaryActions.Children.Add(Ui.Button($"查看 {todo} 项待办", OpenQueue, id: "RecoveryTodos"));
            if (meds > 0) summaryActions.Children.Add(Ui.Button($"核对 {meds} 条服药记录", () => { _historyStatus = OccurrenceStatus.NeedsReview; Navigate("History"); }, id: "RecoveryMedication"));
            summaryActions.Children.Add(Ui.Button("收起", () => _app.Run(() => _app.Engine.DismissRecovery(now)), id: "DismissRecovery")); copy.Children.Add(summaryActions);
            var surface = Ui.Panel(copy, new(18)); surface.SetResourceReference(Border.BackgroundProperty, "SoftBrush"); surface.Margin = new(0, 0, 0, 20); body.Children.Add(surface);
        }
        if (s.IsAway || s.MeetingUntil > now || s.PauseUntil > now || s.BufferUntil > now) return;
        var item = s.Occurrences.FirstOrDefault(o => o.Kind == HabitKind.Activity && o.DueAt <= now && o.NotifyAfter <= now && o.Status is OccurrenceStatus.Pending or OccurrenceStatus.Deferred);
        if (item == null) return;
        var card = new StackPanel();
        card.Children.Add(Ui.Text(item.Activity?.State == ActivityState.Interrupted ? "上次活动计时已中断" : "现在活动一下吧", 20, true));
        _inlineActivityId = item.Id; _activityProgress = Ui.Text("", 14, brush: "Muted", margin: new(0, 10, 0, 16)); card.Children.Add(_activityProgress);
        var actions = new WrapPanel();
        actions.Children.Add(Ui.Button(item.Activity?.State == ActivityState.Interrupted ? "继续计时" : "开始活动", () => _app.Run(() =>
        {
            _app.Engine.StartActivity(item.Id, DateTimeOffset.UtcNow); _app.OpenItems([_app.Engine.State.Occurrences.Single(o => o.Id == item.Id)]);
        }), true, "InlineStartActivity"));
        actions.Children.Add(Ui.Button("刚刚已活动", () => _app.Run(() => {var confirmed=DateTimeOffset.UtcNow;_app.Engine.Resolve(item.Id, OccurrenceStatus.Completed, confirmed, confirmed);}), id: "InlineActivityDone"));
        actions.Children.Add(Ui.Button("5 分钟后", () => _app.Run(() => _app.Engine.Defer([item.Id], DateTimeOffset.UtcNow)), id: "InlineActivityLater"));
        actions.Children.Add(Ui.Button("这次略过", () => _app.Run(() => _app.Engine.Resolve(item.Id, OccurrenceStatus.Skipped, DateTimeOffset.UtcNow)), id: "InlineActivitySkip"));
        card.Children.Add(actions); card.Children.Add(Ui.Text("计时只是辅助，只有本人确认才记录完成。", 12, brush: "Muted", margin: new(0, 12, 0, 0)));
        var panel = Ui.Panel(card, new(22)); panel.SetResourceReference(Border.BackgroundProperty, "SoftBrush"); panel.Margin = new(0, 0, 0, 20); body.Children.Add(panel);
    }
}
