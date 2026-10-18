namespace Pausewell.Wireframes;

sealed class MedicineExample
{
    public string Id = Guid.NewGuid().ToString("N");
    public string Name = "";
    public string Instructions = "";
    public List<TimeSpan> Times = [];
    public DateTime Starts = new(2026, 10, 15);
    public DateTime? Ends;
    public bool Enabled = true;
}

sealed class ReviewRecord
{
    public string Id = Guid.NewGuid().ToString("N");
    public string PlanId = "", Name = "", Kind = "", Status = "待处理";
    public bool Medication;
    public DateTime Planned;
    public DateTime? Confirmed, Actual, NotifyAfter;
    public List<string> Changes = [];
    public bool Resolved => Status is "已完成" or "已服用" or "明确未服用" or "当前无需要" or "未执行";
}

sealed class ReviewSettings
{
    public bool Startup, Sound, NewOffice;
    public bool HideMedicineNames = true;
    public string CloseAction = "继续在托盘运行";
    public string Monitor = "鼠标所在的显示器";
    public ReviewSettings Copy() => (ReviewSettings)MemberwiseClone();
}
