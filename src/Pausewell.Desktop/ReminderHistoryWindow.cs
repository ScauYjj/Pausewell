using Pausewell.Presentation;
using System.Windows.Input;

namespace Pausewell.Desktop;

public sealed class ReminderHistoryWindow : MaterialWindow
{
    public ReminderHistoryWindow(DesktopController app,Occurrence current,bool preview)
    {
        Title="查阅执行记录";Width=744;Height=720;MinWidth=560;MinHeight=480;Topmost=true;
        var body=new StackPanel();body.Children.Add(TaskUi.Summary(current.PlanName,"原计划 "+app.Engine.Local(current.DueAt).ToString("yyyy-MM-dd HH:mm")));Ui.Gap(body,24);
        var records=preview?[]:app.Engine.State.Occurrences.Where(o=>o.PlanId==current.PlanId).OrderByDescending(o=>o.DueAt).ToList();
        if(records.Count==0)body.Children.Add(Ui.Notice(preview?"当前为界面预览，不读取真实服药历史。":"这条计划还没有执行记录。"));
        else
        {
            body.Children.Add(Ui.Text($"共 {records.Count} 条"+(records.Count>100?"，显示最近 100 条":""),12,brush:"Muted",margin:new(0,0,0,12)));
            foreach(var record in records.Take(100))
            {
                var row=new DockPanel();var details=Ui.Button("只读详情",()=>new RecordWindow(app,record.Id,readOnly:true){Owner=this,Topmost=true}.ShowDialog(),id:"ReminderHistory_"+record.Id);details.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(details,Dock.Right);row.Children.Add(details);
                var labels=new StackPanel();labels.Children.Add(Ui.Text(Ui.Status(record),16,true));labels.Children.Add(Ui.Text("原计划 "+app.Engine.Local(record.DueAt).ToString("MM-dd HH:mm")+(record.ActualAt is {} actual?" · 实际 "+app.Engine.Local(actual).ToString("MM-dd HH:mm"):" · 实际未记录"),13,brush:"Muted",margin:new(0,8,12,0)));
                if(record.ResolvedAt is {} confirmed)labels.Children.Add(Ui.Text("确认 "+app.Engine.Local(confirmed).ToString("MM-dd HH:mm"),12,brush:"Muted",margin:new(0,6,0,0)));row.Children.Add(labels);
                var card=Ui.Panel(row,new(16));card.Margin=new(0,0,0,12);body.Children.Add(card);
            }
        }
        var footer=new StackPanel();footer.Children.Add(Ui.Text("查阅不会确认、延期或更正任何事项。关闭后返回当前提醒。",12,brush:"Muted",margin:new(0,0,0,8)));footer.Children.Add(TaskUi.Actions(Ui.Button("返回当前提醒",Close,id:"ReturnToReminder")));
        SetBody(TaskUi.Layout("查阅执行记录","仅查看当前计划的记录，时间使用中国标准时间。",body,footer));PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}};
    }
}
