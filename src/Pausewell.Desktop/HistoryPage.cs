using System.Windows.Automation;
using Pausewell.Controls;
using Pausewell.Presentation;

namespace Pausewell.Desktop;

public partial class MainWindow
{
    private OccurrenceStatus? _historyStatus;
    private HabitKind? _historyKind;
    private DateTime _historyFrom,_historyTo;
    private bool _historyInitialized,_historyBuilding;
    private StackPanel? _historyRows;
    private IsoDatePicker? _historyFromInput,_historyToInput;
    private ComboBox? _historyKindInput,_historyStatusInput;
    private TextBlock? _historyFilterHint;
    private static readonly OccurrenceStatus?[] HistoryStates=[null,OccurrenceStatus.Pending,OccurrenceStatus.Deferred,OccurrenceStatus.Completed,OccurrenceStatus.Skipped,OccurrenceStatus.NeedsReview,OccurrenceStatus.Cancelled,OccurrenceStatus.NotNeeded];

    private UIElement HistoryPage()
    {
        if(!_historyInitialized){_historyTo=_app.Engine.Local(DateTimeOffset.UtcNow).Date;_historyFrom=_historyTo.AddDays(-6);_historyInitialized=true;}
        _historyBuilding=true;
        var root=new DockPanel();var header=new StackPanel();header.Children.Add(Ui.Text("执行记录",28,true));header.Children.Add(Ui.Text("分别保留原计划、实际执行和确认时间。更正记录不会移动后续计划。",14,brush:"Muted",margin:new(0,8,0,20)));
        if(_app.Engine.State.RecoveryNotice.Length>0)header.Children.Add(Ui.Notice(_app.Engine.State.RecoveryNotice));
        var filters=new WrapPanel();
        _historyKindInput=new ComboBox{ItemsSource=new[]{"全部事项","活动","喝水","如厕","服药","自定义"},SelectedIndex=_historyKind==null?0:(int)_historyKind+1};PageUi.Id(_historyKindInput,"HistoryKind","记录事项类型");
        _historyStatusInput=new ComboBox{ItemsSource=new[]{"全部状态","待处理","已延期","已完成 / 已服用","未执行 / 未服用","待核实","已取消","当前无需"},SelectedIndex=Array.IndexOf(HistoryStates,_historyStatus)};PageUi.Id(_historyStatusInput,"HistoryStatus","记录状态");
        _historyFromInput=new(){SelectedDate=_historyFrom};PageUi.Id(_historyFromInput,"HistoryFrom","记录开始日期");
        _historyToInput=new(){SelectedDate=_historyTo};PageUi.Id(_historyToInput,"HistoryTo","记录结束日期");
        foreach(var (label,input) in new (string,FrameworkElement)[]{("事项类型",_historyKindInput),("执行状态",_historyStatusInput),("开始日期",_historyFromInput),("结束日期",_historyToInput)})
        {var field=PageUi.Field(label,input);field.Width=184;field.Margin=new(0,0,12,12);filters.Children.Add(field);}
        header.Children.Add(filters);
        var operations=new DockPanel();var buttons=TaskUi.Actions(Ui.Button("最近 7 天",ResetHistoryFilters,id:"ResetHistory"),Ui.Button("应用筛选",()=>ApplyHistoryFilters(),true,"ApplyHistory"));DockPanel.SetDock(buttons,Dock.Right);operations.Children.Add(buttons);
        _historyFilterHint=Ui.Text("按原计划日期筛选，使用中国标准时间。",12,brush:"Muted",margin:new(0,6,12,0));AutomationProperties.SetAutomationId(_historyFilterHint,"HistoryFilterHint");operations.Children.Add(_historyFilterHint);header.Children.Add(operations);
        header.Margin=new(0,0,0,20);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        _historyRows=new StackPanel();root.Children.Add(Ui.Scroll(_historyRows));
        _historyKindInput.SelectionChanged+=(_,_)=>MarkHistoryDirty();_historyStatusInput.SelectionChanged+=(_,_)=>MarkHistoryDirty();
        _historyFromInput.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((_,_)=>MarkHistoryDirty()));_historyToInput.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((_,_)=>MarkHistoryDirty()));
        _historyBuilding=false;RefreshHistoryRows();return root;
    }
    private void MarkHistoryDirty()
    {
        if(_historyBuilding||_historyFilterHint==null||_historyFromInput==null||_historyToInput==null)return;
        var unchanged=_historyKindInput!.SelectedIndex==(_historyKind==null?0:(int)_historyKind+1)&&_historyStatusInput!.SelectedIndex==Array.IndexOf(HistoryStates,_historyStatus)
            &&(_historyFromInput.Input?.Text??_historyFromInput.Text)==_historyFrom.ToString("yyyy-MM-dd")&&(_historyToInput.Input?.Text??_historyToInput.Text)==_historyTo.ToString("yyyy-MM-dd");
        _historyFilterHint.Text=unchanged?"按原计划日期筛选，使用中国标准时间。":"列表保留上次结果，点击“应用筛选”更新修改后的条件。";
    }
    private bool ApplyHistoryFilters()
    {
        if(_historyFromInput==null||_historyToInput==null||_historyKindInput==null||_historyStatusInput==null)return false;
        var fromOk=_historyFromInput.TryCommit();var toOk=_historyToInput.TryCommit();
        if(!fromOk||!toOk){_historyFilterHint!.Text="日期未生效，请修正标记的字段。列表保留上次结果。";return false;}
        if(_historyFromInput.SelectedDate>_historyToInput.SelectedDate){FieldState.SetError(_historyToInput,"结束日期不能早于开始日期。");_historyFilterHint!.Text="日期范围未生效，列表保留上次结果。";return false;}
        _historyFrom=_historyFromInput.SelectedDate!.Value;_historyTo=_historyToInput.SelectedDate!.Value;
        _historyKind=_historyKindInput.SelectedIndex==0?null:(HabitKind)(_historyKindInput.SelectedIndex-1);_historyStatus=HistoryStates[_historyStatusInput.SelectedIndex];
        _historyFilterHint!.Text="按原计划日期筛选，使用中国标准时间。";RefreshHistoryRows();return true;
    }
    private void ResetHistoryFilters()
    {
        if(_historyFromInput==null||_historyToInput==null)return;
        var today=_app.Engine.Local(DateTimeOffset.UtcNow).Date;_historyFromInput.SetRawInput(today.AddDays(-6).ToString("yyyy-MM-dd"));_historyToInput.SetRawInput(today.ToString("yyyy-MM-dd"));_historyKindInput!.SelectedIndex=0;_historyStatusInput!.SelectedIndex=0;ApplyHistoryFilters();
    }
    private void RefreshHistoryRows()
    {
        if(_historyRows==null)return;_historyRows.Children.Clear();
        var records=_app.Engine.State.Occurrences.Where(o=>(_historyKind==null||o.Kind==_historyKind)&&(_historyStatus==null||o.Status==_historyStatus)&&_app.Engine.Local(o.DueAt).Date>=_historyFrom.Date&&_app.Engine.Local(o.DueAt).Date<=_historyTo.Date).OrderByDescending(o=>o.DueAt).ToList();
        _historyRows.Children.Add(Ui.Text($"共 {records.Count} 条"+(records.Count>200?"，当前展示最近 200 条":"")+$" · {_historyFrom:MM-dd} 至 {_historyTo:MM-dd}",12,brush:"Muted",margin:new(0,0,0,12)));
        if(records.Count==0){_historyRows.Children.Add(TaskUi.Summary("暂无符合条件的记录","可以调整日期和状态，或使用“最近 7 天”恢复筛选。"));return;}
        foreach(var day in records.Take(200).GroupBy(o=>_app.Engine.Local(o.DueAt).Date))
        {
            _historyRows.Children.Add(Ui.Text(day.Key.ToString("yyyy-MM-dd"),14,true,margin:new(0,0,0,12)));var group=new StackPanel();var first=true;
            foreach(var record in day)
            {
                if(!first){var line=new Border{Height=1,Margin=new(16,0,16,0)};line.SetResourceReference(Border.BackgroundProperty,"Line");group.Children.Add(line);}first=false;
                var row=new DockPanel{Margin=new(16)};
                var button=Ui.Button("查看详情",()=>{new RecordWindow(_app,record.Id){Owner=this}.ShowDialog();RefreshHistoryRows();},id:"Record_"+record.Id);button.VerticalAlignment=VerticalAlignment.Center;button.Margin=new(16,0,0,0);DockPanel.SetDock(button,Dock.Right);row.Children.Add(button);
                var badge=Ui.IconCircle(record.Kind,40);badge.Margin=new(0,0,12,0);badge.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(badge,Dock.Left);row.Children.Add(badge);
                var text=new StackPanel();var heading=new WrapPanel();heading.Children.Add(Ui.Text(record.PlanName,16,true,margin:new(0,0,12,4)));
                var state=Ui.Text(Ui.Status(record),12);var pill=new Border{Child=state,Padding=new(8,3,8,3),CornerRadius=new(6),VerticalAlignment=VerticalAlignment.Center};pill.SetResourceReference(Border.BackgroundProperty,"SoftBrush");heading.Children.Add(pill);text.Children.Add(heading);
                text.Children.Add(Ui.Text($"原计划 {_app.Engine.Local(record.DueAt):HH:mm}"+(record.ActualAt is {} actual?$" · 实际 {_app.Engine.Local(actual):MM-dd HH:mm}":" · 实际未记录")+(record.ResolvedAt is {} confirmed?$" · 确认 {_app.Engine.Local(confirmed):MM-dd HH:mm}":""),12,brush:"Muted",margin:new(0,6,0,0)));row.Children.Add(text);group.Children.Add(row);
            }
            var card=Ui.Panel(group,new(0));card.Margin=new(0,0,0,24);_historyRows.Children.Add(card);
        }
    }
}
