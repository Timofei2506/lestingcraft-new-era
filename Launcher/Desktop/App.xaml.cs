using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace LestingCraft.Launcher.Desktop;
public partial class App:Application
{
 const string SmokeFolder="SmokeResults";

 protected override void OnStartup(StartupEventArgs e)
 {
  bool smoke=e.Args.Contains("--smoke");
  if(smoke)System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
  DispatcherUnhandledException+=(_,ev)=>
  {
   Report(ev.Exception,smoke);
   ev.Handled=false;
  };
  try{base.OnStartup(e);}
  catch(Exception ex){Report(ex,smoke);Shutdown(1);return;}
  if(smoke)RunSmoke();
 }

 // Crash log for testers (%LOCALAPPDATA%\LestingCraftLauncher\crash.log); smoke runs also write the proof file.
 static void Report(Exception ex,bool smoke)
 {
  try
  {
   string text=DateTime.UtcNow.ToString("u")+"\n"+ex;
   if(smoke)
   {
    Directory.CreateDirectory(SmokeFolder);
    File.WriteAllText(Path.Combine(SmokeFolder,"result.txt"),"FAIL: "+text);
   }
   else
   {
    string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LestingCraftLauncher");
    Directory.CreateDirectory(dir);
    File.AppendAllText(Path.Combine(dir,"crash.log"),text+"\n\n");
   }
  }
  catch(IOException){}
  catch(UnauthorizedAccessException){}
 }

 // Renders every page of the real window to PNG (CI artifact "Launcher-native-smoke").
 static void RunSmoke()
 {
  var shots=new (int Page,string File)[]{(0,"launcher.png"),(1,"launcher-install.png"),(2,"launcher-settings.png")};
  int step=0;
  var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2.5)};
  timer.Tick+=(_,_)=>
  {
   try
   {
    timer.Interval=TimeSpan.FromMilliseconds(700);
    if(step>=shots.Length)
    {
     timer.Stop();
     File.WriteAllText(Path.Combine(SmokeFolder,"result.txt"),"PASS: native WPF window rendered "+shots.Length+" pages");
     Current.Shutdown(0);
     return;
    }
    var window=Current.Windows.OfType<MainWindow>().FirstOrDefault()??throw new InvalidOperationException("Главное окно не найдено");
    window.ShowPage(shots[step].Page);
    window.UpdateLayout();
    Directory.CreateDirectory(SmokeFolder);
    var b=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
    b.Render(window);
    var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));
    using(var file=File.Create(Path.Combine(SmokeFolder,shots[step].File)))png.Save(file);
    step++;
   }
   catch(Exception ex)
   {
    Report(ex,true);
    Current.Shutdown(1);
   }
  };
  timer.Start();
 }
}
