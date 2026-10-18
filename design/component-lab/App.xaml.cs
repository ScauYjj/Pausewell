using System.IO;
using System.Windows;
using Pausewell.Controls;

namespace Pausewell.ComponentLab;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var styles=DesignTheme.CreateResources();Resources.MergedDictionaries.Add(styles);
            var window=new LabWindow(styles);MainWindow=window;
            var smoke=Array.IndexOf(e.Args,"--verify");if(smoke>=0){var i=Array.IndexOf(e.Args,"--report-dir");var dir=i>=0&&i+1<e.Args.Length?e.Args[i+1]:"artifacts/component-lab/verification";window.Loaded+=async(_,_)=>await LabVerification.Run(window,Path.GetFullPath(dir));}
            window.Show();
        }
        catch(Exception ex){var path=Path.Combine(Path.GetTempPath(),"Pausewell-component-lab-startup.txt");File.WriteAllText(path,ex.ToString());MessageBox.Show("样板启动失败："+ex.Message+"\n详情："+path);Shutdown(1);}
    }
}
