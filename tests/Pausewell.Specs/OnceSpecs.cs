using Pausewell.Core;
using Pausewell.Storage;

static class OnceSpecs
{
    public static void Run(Action<string,Action> run)
    {
        var at=new DateTimeOffset(2026,10,16,9,0,0,TimeSpan.FromHours(8));
        ReminderPlan Plan()=>new(){Name="交材料",Kind=HabitKind.Other,Schedule=ScheduleKind.Once,OnceAt=at.AddMinutes(10),Note="带上签字页"};
        (ReminderEngine,TestStore) Boot(){var store=new TestStore();var engine=new ReminderEngine(store,at);engine.SavePlan(Plan(),at,true);return(engine,store);}
        void Check(bool value){if(!value)throw new Exception("一次性规则断言失败");}
        run("一次性到期仅生成一项，完成后不循环",()=>{var(e,_)=Boot();Check(e.Evaluate(at.AddMinutes(9)).Count==0);var item=e.Evaluate(at.AddMinutes(10)).Single();e.Evaluate(at.AddMinutes(11));Check(e.State.Occurrences.Count==1);e.Resolve(item.Id,OccurrenceStatus.Completed,at.AddMinutes(11));Check(!e.State.Plans.Single().Enabled&&e.Evaluate(at.AddDays(3)).Count==0);});
        run("一次性延期保留原时刻和事项身份",()=>{var(e,_)=Boot();var item=e.Evaluate(at.AddMinutes(10)).Single();e.Defer([item.Id],at.AddMinutes(10));Check(e.Evaluate(at.AddMinutes(14)).Count==0);var again=e.Evaluate(at.AddMinutes(15)).Single();Check(again.Id==item.Id&&again.DueAt==at.AddMinutes(10)&&e.State.Occurrences.Count==1);});
        run("关机错过一次性时刻，重开保留待办",()=>{var(_,store)=Boot();var reopened=new ReminderEngine(store,at.AddDays(40));var item=reopened.State.Occurrences.Single();Check(item.DueAt==at.AddMinutes(10)&&item.Status==OccurrenceStatus.Pending);var again=new ReminderEngine(store,at.AddDays(40).AddMinutes(1));Check(again.State.Occurrences.Count==1);});
        run("会议中的一次性事项会后仍保留",()=>{var(e,_)=Boot();e.BeginMeeting(at.AddMinutes(20),at);Check(e.Evaluate(at.AddMinutes(10)).Count==0);e.EndMeeting(at.AddMinutes(20));Check(e.Evaluate(at.AddMinutes(20).AddSeconds(30)).Count==0&&e.State.Occurrences.Single().DueAt==at.AddMinutes(10)&&e.State.Occurrences.Single().Status==OccurrenceStatus.Pending);});
        run("暂停恢复不取消已到期的一次性待办",()=>{var(e,_)=Boot();var item=e.Evaluate(at.AddMinutes(10)).Single();e.BeginPause(at.AddMinutes(20),false,at.AddMinutes(11));Check(e.Evaluate(at.AddMinutes(12)).Count==0);Check(e.Evaluate(at.AddMinutes(20)).Count==0&&e.State.Occurrences.Single().Id==item.Id&&e.State.Occurrences.Single().Status==OccurrenceStatus.Pending);});
        run("暂停期间到期的一次性事项恢复后保留，不强制补弹",()=>{var(e,_)=Boot();e.BeginPause(at.AddMinutes(20),false,at);Check(e.Evaluate(at.AddMinutes(12)).Count==0&&e.State.Occurrences.Count==0);Check(e.Evaluate(at.AddMinutes(20)).Count==0&&e.State.Occurrences.Single().DueAt==at.AddMinutes(10)&&e.State.Occurrences.Single().Status==OccurrenceStatus.Pending);});
        run("一次性拒绝过去时刻，停用后过期不可直接重启",()=>{var(e,_)=Boot();var p=Plan();p.OnceAt=at;try{e.SavePlan(p,at,true);throw new Exception("接受过去时间");}catch(ArgumentException){}var id=e.State.Plans.Single().Id;e.SetEnabled(id,false,at);try{e.SetEnabled(id,true,at.AddHours(1));throw new Exception("接受过期重启");}catch(ArgumentException){}Check(e.State.Plans.Count==1&&!e.State.Plans.Single().Enabled);});
        run("一次性保存失败不改变计划和启动状态",()=>{var store=new TestStore{Fail=true};var e=new ReminderEngine(store,at);try{e.SavePlan(Plan(),at,true);throw new Exception("接受写入失败");}catch(IOException){}Check(!e.State.Running&&e.State.Plans.Count==0);});
        run("一次性编辑改期取消旧待办，按新规则触发",()=>{var(e,_)=Boot();e.Evaluate(at.AddMinutes(10));var p=StateJson.Clone(e.State).Plans.Single();p.OnceAt=at.AddMinutes(30);e.SavePlan(p,at.AddMinutes(11));Check(e.State.Occurrences.Single().Status==OccurrenceStatus.Cancelled);var item=e.Evaluate(at.AddMinutes(30)).Single();Check(item.Revision==2&&item.DueAt==p.OnceAt&&e.State.Occurrences.Count==2);});
        run("一次性不继承工作日，药物仍拒绝一次性规则",()=>{var(e,_)=Boot();var p=Plan();p.WorkdaysOnly=true;try{e.Validate(p);throw new Exception("接受工作日限制");}catch(ArgumentException){}p.WorkdaysOnly=false;p.Kind=HabitKind.Medication;try{e.Validate(p);throw new Exception("接受一次性药物");}catch(ArgumentException){}});
        run("SQLite 重开保留自定义一次性内容及完成结果",()=>{var dir=Path.Combine(Path.GetTempPath(),"Pausewell.Once",Guid.NewGuid().ToString("N"));using var store=new SqliteStateStore(Path.Combine(dir,"once.db"));var e=new ReminderEngine(store,at);e.SavePlan(Plan(),at,true);var reopened=new ReminderEngine(store,at.AddMinutes(10));var item=reopened.State.Occurrences.Single();Check(reopened.Evaluate(at.AddMinutes(10)).Count==0);Check(item.PlanName=="交材料"&&item.Note=="带上签字页");reopened.Resolve(item.Id,OccurrenceStatus.Completed,at.AddMinutes(11));var again=new ReminderEngine(store,at.AddDays(2));Check(again.State.SchemaVersion==4&&again.Evaluate(at.AddDays(2)).Count==0&&store.IntegrityCheck()=="ok");});
    }
    private sealed class TestStore:IStateStore
    {public AppState? Value;public bool Fail;public AppState? Load()=>Value==null?null:StateJson.Clone(Value);public void Save(AppState state){if(Fail)throw new IOException("模拟失败");Value=StateJson.Clone(state);}}
}
