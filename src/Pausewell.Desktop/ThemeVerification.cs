using Pausewell.Controls;
using Pausewell.MaterialLab;
using System.Windows.Media.Imaging;

namespace Pausewell.Desktop;

internal static class ThemeVerification
{
    private static Color Resource(FrameworkElement element,string key)=>((SolidColorBrush)element.FindResource(key)).Color;
    private static string ScenePixel(AmbientScene scene)
    {
        scene.UpdateLayout();
        var bitmap=new RenderTargetBitmap(32,32,96,96,PixelFormats.Pbgra32);
        bitmap.Render(scene);var pixels=new byte[32*32*4];bitmap.CopyPixels(pixels,32*4,0);
        return Convert.ToHexString(pixels.AsSpan((16*32+16)*4,4));
    }
    public static async Task Run(DesktopController app,Action<string,bool,object?> verify,Func<Window,string,Task> shot)
    {
        if(!app.IsIsolated)throw new InvalidOperationException("换肤验证仅允许隔离数据。");
        var schedule=string.Join(";",app.Engine.State.Plans.Select(p=>$"{p.Id}:{p.NextDue}:{p.Revision}:{p.Enabled}"));
        app.Main.ShowAppearance();var original=app.Engine.State.Preferences.Theme;
        var originalEnvironment=app.Main.AmbientLayer.Environment;
        app.Main.SelectTheme("blue");await Task.Delay(100);
        var material=SmokeVerification.Descendants<GlassSurface>(app.Main).First(x=>x.Profile.Name=="content");
        verify("预览同时更新环境背景和阅读层底色",app.Main.AmbientLayer.Environment!=originalEnvironment&&material.AppliedTint.R==Resource(app.Main,"Theme.glassTint").R&&Resource(app.Main,"Surface")==Resource(app.Main,"Color.surfaceSolid"),null);
        verify("整体预览不提前保存或更改全局主题",app.Store.Load()!.Preferences.Theme==original&&Theme.Current==original,null);
        app.Main.CancelSettings();
        verify("取消预览恢复环境背景及表面",app.Main.AmbientLayer.Environment==originalEnvironment&&!app.Main.Resources.Contains("Color.surfaceSolid"),null);
        app.Main.SelectTheme("blue");app.Main.Navigate("Today");
        verify("离开设置放弃预览并恢复背景",app.Main.AmbientLayer.Environment==originalEnvironment,null);

        var pixels=new HashSet<string>();var surfaces=new HashSet<Color>();
        foreach(var theme in Theme.All)
        {
            app.Main.ShowAppearance();app.Main.SelectTheme(theme.Id);app.Main.SaveUserSettings();app.Main.Navigate("Today");await Task.Delay(150);
            verify(theme.Name+"同步保存背景、控件和阅读层",app.Store.Load()!.Preferences.Theme==theme.Id&&app.Main.AmbientLayer.Environment==Resource(app.Main,"Theme.ambient")&&app.Main.AmbientLayer.Accent==Resource(app.Main,"Theme.toggle"),null);
            pixels.Add(ScenePixel(app.Main.AmbientLayer));surfaces.Add(Resource(app.Main,"Surface"));
            await shot(app.Main,"30-theme-home-"+theme.Id);
            if(theme.Id is "violet" or "blue")
            {
                var form=new PlanEditor(app,null){Owner=app.Main};form.Show();await Task.Delay(100);
                verify(theme.Name+"编辑窗口继承整套主题",form.Ambient.Environment==app.Main.AmbientLayer.Environment&&form.Ambient.Accent==app.Main.AmbientLayer.Accent&&Resource(form,"Color.surfaceSolid")==Resource(app.Main,"Surface"),null);
                await shot(form,"31-theme-editor-"+theme.Id);form.VerificationClose=true;form.Close();
            }
        }
        verify("五种主题背景实际渲染像素均有差别",pixels.Count==5,pixels.ToArray());
        verify("五种主题卡片表面均有对应底色",surfaces.Count==5,surfaces.Select(x=>x.ToString()).ToArray());

        var active=new PlanEditor(app,null){Owner=app.Main};active.Show();await Task.Delay(100);active.NameInput.Text="换肤不丢失草稿";active.NoteInput.Text="保留未保存内容";
        var reminder=app.Preview(HabitKind.Water,ReminderStyle.Card);
        app.Main.ShowAppearance();app.Main.SelectTheme("rose");app.Main.SaveUserSettings();await Task.Delay(150);
        verify("已打开表单立即更新背景且不丢草稿",active.Ambient.Environment==Resource(app.Main,"Theme.ambient")&&active.NameInput.Text=="换肤不丢失草稿"&&active.NoteInput.Text=="保留未保存内容",null);
        verify("已打开提醒使用新主题表面且保持同一窗口",app.OpenReminders.Contains(reminder)&&SmokeVerification.Descendants<Border>(reminder).Any(x=>x.Background is SolidColorBrush b&&b.Color==Resource(app.Main,"Surface")),null);
        await shot(reminder,"32-theme-existing-reminder");reminder.CloseWithoutAction();active.VerificationClose=true;active.Close();
        app.Main.ShowAppearance();app.Main.SelectTheme("champagne");app.Main.SaveUserSettings();app.Main.Navigate("Today");
        verify("整体换肤不改变计划时间、版本或启停",schedule==string.Join(";",app.Engine.State.Plans.Select(p=>$"{p.Id}:{p.NextDue}:{p.Revision}:{p.Enabled}")),null);
    }
}
