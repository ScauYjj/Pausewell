using System.IO;
using System.Windows;
using Pausewell.Controls;
namespace Pausewell.PageLab;
public partial class App:Application
{
 protected override void OnStartup(StartupEventArgs e)
 {
  base.OnStartup(e);
  try{
   Resources.MergedDictionaries.Add(DesignTheme.CreateResources());
   Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Pausewell.PageLab;component/WorkbenchStyles.xaml",UriKind.Relative)});
   var window=new PageWindow(new PreviewSession(!e.Args.Contains("--empty")));MainWindow=window;
   if(e.Args.Contains("--verify")){var index=Array.IndexOf(e.Args,"--report-dir");var directory=index>=0&&index+1<e.Args.Length?e.Args[index+1]:"artifacts/page-lab/verification";window.Loaded+=async(_,_)=>await PageVerification.Run(window,Path.GetFullPath(directory));}
   window.Show();
  }catch(Exception ex){File.WriteAllText(Path.Combine(Path.GetTempPath(),"Pausewell-page-lab-startup.txt"),ex.ToString());MessageBox.Show("页面样板启动失败："+ex.Message);Shutdown(1);}
 }
}
