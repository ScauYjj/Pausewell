using System.IO;
using System.Windows;
using Pausewell.Controls;
namespace Pausewell.MaterialLab;
public partial class App:Application
{
 protected override void OnStartup(StartupEventArgs e)
 {
  base.OnStartup(e);
  try{
   var resources=DesignTheme.CreateResources();Resources.MergedDictionaries.Add(resources);
   var window=new MaterialWindow(resources);MainWindow=window;
   if(e.Args.Contains("--verify")){var index=Array.IndexOf(e.Args,"--report-dir");var directory=index>=0&&index+1<e.Args.Length?e.Args[index+1]:"artifacts/material-lab/verification";window.Loaded+=async(_,_)=>await MaterialVerification.Run(window,Path.GetFullPath(directory));}
   window.Show();
  }catch(Exception ex){File.WriteAllText(Path.Combine(Path.GetTempPath(),"Pausewell-material-lab-startup.txt"),ex.ToString());MessageBox.Show("材质样板启动失败："+ex.Message);Shutdown(1);}
 }
}
