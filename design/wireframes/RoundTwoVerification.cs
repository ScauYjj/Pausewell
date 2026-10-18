using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pausewell.Wireframes;

public sealed partial class ReviewWindow
{
    private IEnumerable<T> Controls<T>(DependencyObject root) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T typed)yield return typed;foreach(var item in Controls<T>(child))yield return item;}
    }
    private Button ReviewButton(string id){UpdateLayout();return Controls<Button>(this).Single(b=>AutomationProperties.GetAutomationId(b)==id);}
    private void Press(string id){var button=ReviewButton(id);if(!button.IsEnabled)throw new Exception("按钮未启用："+id);button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));UpdateLayout();}
    public void ExportRoundTwo(string directory)
    {
        Directory.CreateDirectory(directory);var checks=new List<object>();
        void Check(string name,bool condition){checks.Add(new{name,passed=condition});if(!condition)throw new Exception(name);}
        void Shot(string name){UpdateLayout();Capture(Path.Combine(directory,name+".png"));}
        void Fresh(){Reset();_notice="";}

        Fresh();Navigate(ReviewPage.Reminder);Shot("01-water-reminder");var record=_focusedRecord!;var planned=record.Planned;
        Press("DeferOrdinary");Check("延期关闭提示，保留原计划且不记完成",_page==ReviewPage.Home&&record.Status=="已延期"&&record.Planned==planned&&record.NotifyAfter==_now.AddMinutes(5)&&record.Actual==null);
        Fresh();Navigate(ReviewPage.Reminder);PrepareReminder("活动");Render();Shot("02-activity-before");Check("活动计时未完成时确认不可用",!ReviewButton("ResolveOrdinary").IsEnabled);
        Press("StartActivityReview");_activityReady=true;Render();Shot("03-activity-ready");Check("活动结束仍需明确确认",ReviewButton("ResolveOrdinary").IsEnabled&&_focusedRecord!.Status=="待处理");Press("ResolveOrdinary");Check("活动确认返回首页并留记录",_page==ReviewPage.Home&&_focusedRecord!.Status=="已完成");
        Fresh();Navigate(ReviewPage.Reminder);PrepareReminder("如厕");Render();Shot("04-toilet");Press("NoToiletNeed");Check("当前无需要不伪造完成",_focusedRecord!.Status=="当前无需要"&&_focusedRecord.Actual==null);
        Fresh();Navigate(ReviewPage.Reminder);PrepareReminder("全屏");Render();Shot("05-fullscreen");Check("强提醒保留紧急退出入口且不覆盖桌面",ReviewButton("EmergencyExitReview")!=null&&!Topmost&&WindowStyle!=WindowStyle.None);

        Fresh();_now=_now.Date.AddHours(12);Navigate(ReviewPage.Meeting);_durationText="60";Render();Shot("06-meeting-config");ApplyMode();Shot("07-meeting-active");Check("开始会议给出准确到期时间",_mode==RunMode.Meeting&&_modeUntil==_now.AddHours(1));
        var early=_now;EndModeToBuffer();Check("提前结束不把示例时间跳到原结束时刻",_now==early&&_modeStage=="buffer");
        _mode=RunMode.Meeting;_modeUntil=_now.AddHours(1);SimulateModeEnd();Shot("08-meeting-buffer");Check("会议到期先收尾30秒",_modeStage=="buffer"&&_bufferUntil==_now.AddSeconds(30));ShowMeetingQueue();Shot("09-meeting-queue");
        Check("会后普通事项与药物分开且无自动完成",_queueIds.Count==3&&_records.Where(r=>_queueIds.Contains(r.Id)).All(r=>!r.Resolved));
        var one=_records.First(r=>_queueIds.Contains(r.Id)&&!r.Medication);_focusedRecord=one;_returnToQueue=true;ResolveOrdinaryReview("已完成");Check("会后只完成明确处理的这一项",_records.Count(r=>_queueIds.Contains(r.Id)&&r.Resolved)==1&&_page==ReviewPage.Meeting);
        Fresh();_now=_now.Date.AddHours(12);OpenModeEditor(RunMode.Paused);_draftPauseMedicines=true;_durationText="60";Render();Shot("10-pause-config");ApplyMode();Shot("11-pause-active");var medTimes=_medicines[0].Times.ToArray();var missed=GetRecord(_medicines[0].Id,_medicines[0].Name,"服药",true,_now.Date.AddHours(13));SimulateModeEnd();
        Check("同时暂停服药的未确认事项恢复后待核实",missed.Status=="待核实"&&medTimes.SequenceEqual(_medicines[0].Times));
        Fresh();OpenModeEditor(RunMode.Paused);_draftPauseMedicines=true;_page=ReviewPage.Home;Render();Check("取消模式编辑不应用暂停草稿",_mode==RunMode.Running&&!_pauseMedicines);

        Fresh();Navigate(ReviewPage.Medicine);Shot("12-medicine-list");BeginMedicineEdit(_medicines[0]);Shot("13-medicine-form");
        BeginMedicineEdit(null);SaveMedicine();Check("新药时刻不自动填示例值",_medicineEditor&&_medicines.Count==2&&_medTimeText.All(string.IsNullOrEmpty));
        _medName="新药示例";_medTimeText=["08:00","08:00"];SaveMedicine();Check("拒绝同药重复时刻",_medicines.Count==2&&_medicineEditor);
        _medTimeText=["08:00","20:00"];SaveMedicine();Check("新建只从未来时刻开始，不生成过去漏项",_medicines.Count==3&&_records.Count==0&&NextFixed(_medicines.Last().Times,_now)==_now.Date.AddHours(20));
        Fresh();Navigate(ReviewPage.Reminder);PrepareReminder("服药");Render();Shot("14-medicine-reminder");Press("RecordTaken");Shot("15-medicine-confirm");_now=_now.AddMinutes(15);_actualTime="13:10";var timesBefore=_medicines[0].Times.ToArray();Press("ConfirmMedicineRecord");
        Check("晚确认不移动后续服药时刻",_focusedRecord!.Status=="已服用"&&_focusedRecord.Actual==_now.Date.AddHours(13).AddMinutes(10)&&timesBefore.SequenceEqual(_medicines[0].Times)&&NextFixed(_medicines[0].Times,_now)==_now.Date.AddHours(20));
        var changeCount=_focusedRecord.Changes.Count;StoreMedicineOutcome("已服用");Check("重复保存同一剂次不重复记录",_focusedRecord.Changes.Count==changeCount);
        _page=ReviewPage.Reminder;Render();Shot("16-already-recorded");
        Fresh();Navigate(ReviewPage.Reminder);PrepareReminder("恢复后待核实");Render();Shot("17-medicine-review");Check("恢复后的缺少确认不推定已经或尚未服用",_focusedRecord!.Status=="待核实"&&_focusedRecord.Actual==null);
        Press("UnsureMedicine");Check("不确定保留待核实并退出提醒",_focusedRecord.Status=="待核实"&&_page==ReviewPage.Home);
        Fresh();Navigate(ReviewPage.Reminder);PrepareReminder("保存失败");Render();Press("RecordTaken");Press("ConfirmMedicineRecord");Shot("18-save-failure");
        Check("保存失败不写入服用完成",_focusedRecord!.Status=="待处理"&&_focusedRecord.Changes.Count==0&&_recordError.Contains("未保存"));Press("ConfirmMedicineRecord");Check("重试只保存记录一次",_focusedRecord.Status=="已服用"&&_focusedRecord.Changes.Count==1);

        Fresh();Navigate(ReviewPage.History);Shot("19-history");var history=_records.First(r=>r.Medication&&r.Status=="已服用");OpenRecord(history);Shot("20-record-detail");BeginCorrection(history);_correctionTime="08:07";_correctionUnknown=false;Render();Shot("21-correction");SaveCorrection();Check("更正必须填写原因",_correcting&&history.Changes.Count==0);
        var original=history.Planned;var fixedBefore=_medicines[0].Times.ToArray();_correctionReason="核对后发现时间填写有误";SaveCorrection();Shot("22-corrected-record");Check("更正保留原值原因且不移动计划",!_correcting&&history.Planned==original&&history.Changes.Count==1&&history.Changes[0].Contains(_correctionReason)&&fixedBefore.SequenceEqual(_medicines[0].Times));
        _historyDetail=null;_historyEmptyDemo=true;Render();Shot("23-history-empty");

        Fresh();Navigate(ReviewPage.Settings);Shot("24-settings");Check("开机启动默认不勾选",!_settings.Startup&&!_settingsDraft.Startup);_settingsDraft.Startup=true;Press("CancelSettings");Check("取消设置不生效",!_settings.Startup&&!_settingsDraft.Startup);_settingsDraft.NewOffice=true;Press("SaveSettings");Navigate(ReviewPage.Add);Check("默认工作时段只影响后续新增",_office&&_plans.All(p=>!p.OfficeOnly));
        Fresh();Navigate(ReviewPage.Add);_name="午间休息";OpenFixedOrdinary();_fixedName="午间休息";_fixedTimeText="12:00、18:00";Render();Shot("25-fixed-time");SaveFixedOrdinary();var fixedPlan=_plans.Last();_now=_now.Date.AddHours(12).AddMinutes(15);Complete(fixedPlan);Check("普通固定时刻晚完成也不平移后续",fixedPlan.Next==_now.Date.AddHours(18));
        File.WriteAllText(Path.Combine(directory,"review-checks-v02.json"),JsonSerializer.Serialize(new{success=true,checkedAt=DateTimeOffset.UtcNow,checks,scope="第二轮灰度交互稿状态验证。全部示例仅驻留内存，不代表正式软件已实施或验收。"},new JsonSerializerOptions{WriteIndented=true}));
    }
}
