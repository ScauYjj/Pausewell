using System.Diagnostics;
using System.Windows.Automation;
using Pausewell.Presentation;

namespace Pausewell.Desktop;

public partial class MainWindow
{
    private bool _appearance,_settingsLoaded;
    private UserPreferences _draft=new();
    private bool _draftScreens,_draftSound;
    private string _themeDraft="champagne";
    private static UserPreferences CopyPrefs(UserPreferences p)=>new(){Theme=p.Theme,HideMedicationNames=p.HideMedicationNames,CloseToTray=p.CloseToTray,NewOrdinaryOfficeHours=p.NewOrdinaryOfficeHours,StartupEnabled=p.StartupEnabled};

    private UIElement SettingsPage()
    {
        var state=_app.Engine.State;
        if(!_settingsLoaded){_draft=CopyPrefs(state.Preferences);_themeDraft=_draft.Theme;_draftScreens=state.CoverAllScreens;_draftSound=state.SoundEnabled;_settingsLoaded=true;}
        var root=new DockPanel();var header=new StackPanel();header.Children.Add(Ui.Text("设置",28,true));
        header.Children.Add(Ui.Text("调整日常运行与外观，保存后生效。",14,brush:"Muted",margin:new(0,8,0,16)));
        var tabs=new WrapPanel();
        foreach(var (label,id,appearance) in new[]{("通用","GeneralSettings",false),("外观","AppearanceSettings",true)})
        {
            var tab=Ui.Button(label,()=>{_appearance=appearance;ClearError();Refresh();},id:id);tab.MinWidth=88;
            if(_appearance==appearance){tab.SetResourceReference(BackgroundProperty,"Theme.soft");tab.SetResourceReference(BorderBrushProperty,"Theme.toggle");tab.FontWeight=FontWeights.SemiBold;}
            tabs.Children.Add(tab);
        }
        header.Children.Add(tabs);header.Margin=new(0,0,0,24);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var footer=new DockPanel();var hint=Ui.Text("通用与外观一并保存；离开设置会放弃未保存的修改。",12,brush:"Muted",margin:new(0,0,16,0));hint.VerticalAlignment=VerticalAlignment.Center;
        var actions=TaskUi.Actions(Ui.Button("取消修改",CancelSettings,id:"CancelSettings"),Ui.Button("保存设置",SaveUserSettings,true,"SaveSettings"));DockPanel.SetDock(actions,Dock.Right);footer.Children.Add(actions);footer.Children.Add(hint);footer.Margin=new(0,16,0,0);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var body=new StackPanel();if(_notice.Length>0)body.Children.Add(Ui.Notice(_notice));
        if(_appearance)AppearanceSettingsBody(body);else GeneralSettingsBody(body);
        root.Children.Add(Ui.Scroll(body));return root;
    }

    private static void SettingsGroup(Panel body,string title,string description,params UIElement[] rows)
    {
        body.Children.Add(Ui.Text(title,18,true));body.Children.Add(Ui.Text(description,13,brush:"Muted",margin:new(0,8,0,12)));
        var content=new StackPanel();for(var i=0;i<rows.Length;i++)
        {
            if(i>0){var line=new Border{Height=1,Margin=new(16,0,16,0)};line.SetResourceReference(Border.BackgroundProperty,"Line");content.Children.Add(line);}content.Children.Add(rows[i]);
        }
        var group=Ui.Panel(content,new(0));group.Margin=new(0,0,0,24);body.Children.Add(group);
    }
    private CheckBox SettingSwitch(string title,string detail,bool value,Action<bool> set,string id,out Grid row)
    {
        var toggle=new CheckBox{IsChecked=value,Style=(Style)FindResource("Toggle"),VerticalAlignment=VerticalAlignment.Center,Margin=new(24,0,0,0)};
        AutomationProperties.SetAutomationId(toggle,id);AutomationProperties.SetName(toggle,title);AutomationProperties.SetHelpText(toggle,detail);
        toggle.Checked+=(_,_)=>set(true);toggle.Unchecked+=(_,_)=>set(false);
        row=new Grid{Margin=new(16)};row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var labels=new StackPanel();labels.Children.Add(Ui.Text(title,14,true));labels.Children.Add(Ui.Text(detail,13,brush:"Muted",margin:new(0,6,0,0)));row.Children.Add(labels);Grid.SetColumn(toggle,1);row.Children.Add(toggle);return toggle;
    }
    private void GeneralSettingsBody(StackPanel body)
    {
        var startup=SettingSwitch("随 Windows 登录启动",_app.IsIsolated?"独立试用中不修改系统启动设置。":"登录后自动运行，并恢复已启用的提醒。",_draft.StartupEnabled,v=>_draft.StartupEnabled=v,"StartupSetting",out var startupRow);startup.IsEnabled=!_app.IsIsolated;
        SettingSwitch("关闭窗口后继续提醒","关闭主窗口后驻留托盘；退出应用后停止提醒。",_draft.CloseToTray,v=>_draft.CloseToTray=v,"CloseToTray",out var trayRow);
        SettingsGroup(body,"日常运行","设置一次，之后打开软件自动恢复已启用的提醒。",startupRow,trayRow);
        SettingSwitch("允许提醒音","实际提醒弹出时播放提示音。",_draftSound,v=>_draftSound=v,"SoundSetting",out var soundRow);
        SettingSwitch("全屏覆盖所有显示器","仅影响选择了全屏方式的提醒。",_draftScreens,v=>_draftScreens=v,"AllScreensSetting",out var screenRow);
        SettingSwitch("外部通知隐藏药名","外部通知使用通用提示，应用内仍显示药名。",_draft.HideMedicationNames,v=>_draft.HideMedicationNames=v,"HideMedicineNames",out var privacyRow);
        SettingSwitch("新增普通提醒默认使用办公时段","周一至周五 09:00 至 18:00；只影响以后新增的普通提醒，药物不继承。",_draft.NewOrdinaryOfficeHours,v=>_draft.NewOrdinaryOfficeHours=v,"DefaultOffice",out var officeRow);
        SettingsGroup(body,"提醒偏好","每条提醒仍可单独选择提醒方式与执行时段。",soundRow,screenRow,privacyRow,officeRow);
        var previews=new WrapPanel{Margin=new(16)};
        foreach(var (label,kind,style,id) in new[]{("喝水",HabitKind.Water,ReminderStyle.Card,"PreviewWater"),("活动",HabitKind.Activity,ReminderStyle.Card,"PreviewActivity"),("服药",HabitKind.Medication,ReminderStyle.Card,"PreviewMedication"),("全屏",HabitKind.Activity,ReminderStyle.Fullscreen,"PreviewFullscreen")})
        {var button=Ui.Button(label,()=>_app.Preview(kind,style),id:id);button.Margin=new(0,0,8,8);previews.Children.Add(button);}
        SettingsGroup(body,"提醒预览","预览使用已保存的主题，不写入执行记录。",previews);
        var local=new WrapPanel{Margin=new(16)};local.Children.Add(Ui.Button("打开本地数据文件夹",()=>Process.Start(new ProcessStartInfo(_app.DataDirectory){UseShellExecute=true}),id:"OpenData"));local.Children.Add(Ui.Button("退出应用",()=>_app.Quit(),id:"QuitApp"));
        if(_app.Diagnostics.Enabled){var log=Ui.Button("打开诊断文件夹",()=>_app.Run(()=>{Directory.CreateDirectory(_app.Diagnostics.DirectoryPath);Process.Start(new ProcessStartInfo(_app.Diagnostics.DirectoryPath){UseShellExecute=true});}),id:"OpenDiagnostics");log.ToolTip="本地运行日志，不包含事项名称、药名或备注。";local.Children.Add(log);}
        SettingsGroup(body,"本地数据与应用","数据保存在本机。退出应用后，已启用的提醒将在下次打开时恢复。",local);
    }
    private void AppearanceSettingsBody(StackPanel body)
    {
        body.Children.Add(Ui.Text("主题外观",18,true));body.Children.Add(Ui.Text("选择后预览本窗口的背景、材质与控件，保存后应用到整个软件。",14,brush:"Muted",margin:new(0,8,0,16)));
        var choices=new WrapPanel();
        foreach(var theme in Theme.All)
        {
            // The small scene uses real semantic colors without scaling text from a mockup.
            var palette=new ResourceDictionary();Theme.Write(palette,theme.Id);
            var scene=new Grid{Height=76,Margin=new(0,0,0,12),ClipToBounds=true};
            scene.Children.Add(new Border{Background=(Brush)palette["Theme.ambient"],CornerRadius=new(8)});
            scene.Children.Add(new Border{Background=(Brush)palette["Color.surfaceSolid"],CornerRadius=new(6),Margin=new(12,12,12,10),BorderBrush=(Brush)palette["Color.border"],BorderThickness=new(1)});
            var strips=new StackPanel{Margin=new(22,23,22,18)};strips.Children.Add(new Border{Background=(Brush)palette["Color.textPrimary"],Width=48,Height=4,HorizontalAlignment=HorizontalAlignment.Left,CornerRadius=new(2)});
            strips.Children.Add(new Border{Background=(Brush)palette["Theme.toggle"],Width=28,Height=14,HorizontalAlignment=HorizontalAlignment.Right,CornerRadius=new(7),Margin=new(0,8,0,0)});scene.Children.Add(strips);
            var content=new StackPanel();content.Children.Add(scene);content.Children.Add(Ui.Text(theme.Name+(theme.Id=="champagne"?" · 默认":""),14,true));content.Children.Add(Ui.Text(theme.Id==_themeDraft?"已选择":"选择",12,brush:"Muted",margin:new(0,6,0,0)));
            var choice=Ui.Button(theme.Name,()=>SelectTheme(theme.Id),id:"Theme_"+theme.Id);choice.Content=content;choice.Width=168;choice.Margin=new(0,0,12,12);choice.Padding=new(12);choice.BorderThickness=new(theme.Id==_themeDraft?2:1);choice.SetResourceReference(BorderBrushProperty,theme.Id==_themeDraft?"PrimaryBrush":"Line");choices.Children.Add(choice);
        }
        body.Children.Add(choices);Ui.Gap(body,12);body.Children.Add(Ui.Text("当前预览",18,true));
        var sample=new StackPanel();sample.Children.Add(Ui.Text("安静地提醒，清楚地确认",16,true));sample.Children.Add(Ui.Text("背景、阅读表面、输入框与开关使用同一主题。",13,brush:"Muted",margin:new(0,8,0,16)));
        var field=Ui.Input("喝水提醒","主题输入框样本","ThemeSampleInput");field.IsReadOnly=true;field.IsTabStop=false;sample.Children.Add(field);
        var toggle=new CheckBox{IsChecked=true,Content="已开启",Style=(Style)FindResource("Toggle"),IsHitTestVisible=false,Focusable=false};AutomationProperties.SetName(toggle,"主题开关外观样本");
        var save=Ui.Button("主按钮",()=>{},true);save.IsHitTestVisible=false;save.Focusable=false;var row=new DockPanel{Margin=new(0,16,0,0)};DockPanel.SetDock(save,Dock.Right);row.Children.Add(save);row.Children.Add(toggle);sample.Children.Add(row);
        var panel=Ui.Panel(sample,new(20));panel.Margin=new(0,12,0,0);body.Children.Add(panel);
        body.Children.Add(Ui.Text("配色不影响提醒时间和记录。取消会恢复已保存的主题，下次打开沿用已保存的外观。",13,brush:"Muted",margin:new(0,16,0,0)));
    }
    public void SelectTheme(string id)
    {
        _draft.Theme=id;_themeDraft=id;Theme.Write(Resources,id);RefreshMaterial();
        foreach(var button in SmokeVerification.Descendants<Button>(PageHost).Where(b=>AutomationProperties.GetAutomationId(b).StartsWith("Theme_")))
        {
            var selected=AutomationProperties.GetAutomationId(button)=="Theme_"+id;button.BorderThickness=new(selected?2:1);button.SetResourceReference(BorderBrushProperty,selected?"PrimaryBrush":"Line");
            if(button.Content is StackPanel panel&&panel.Children[^1] is TextBlock label)label.Text=selected?"已选择":"选择";
        }
    }
    public void SaveUserSettings()
    {
        try{_app.SaveSettings(_draft,_draftScreens,_draftSound);Theme.ClearPreview(Resources);_settingsLoaded=false;ClearError();_notice="设置已保存，下次打开继续沿用。";Refresh();}
        catch(Exception ex){ReportError("未保存，修改已保留："+TaskUi.Failure(ex));}
    }
    public void CancelSettings(){Theme.ClearPreview(Resources);_settingsLoaded=false;_notice="已取消本次修改。";ClearError();Refresh();}
    public void ShowAppearance(){_appearance=true;Navigate("Settings");}
}
