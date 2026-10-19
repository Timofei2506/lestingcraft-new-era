using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
namespace LestingCraft.Launcher.Desktop;
public partial class App:Application
{
 protected override void OnStartup(StartupEventArgs e)
 {
  if(e.Args.Contains("--smoke"))System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
  base.OnStartup(e);
  if(e.Args.Contains("--smoke"))RunSmoke();
 }

 // Renders every page of the real window to PNG (CI artifact "Launcher-native-smoke").
 static void RunSmoke()
 {
  var window=(MainWindow)Current.MainWindow;
  var shots=new (int Page,string File)[]{(0,"launcher.png"),(1,"launcher-install.png"),(2,"launcher-settings.png")};
  int step=0;
  var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2.5)};
  timer.Tick+=(_,_)=>
  {
   try
   {
    if(step>0)timer.Interval=TimeSpan.FromMilliseconds(700);
    if(step>=shots.Length)
    {
     timer.Stop();
     File.WriteAllText("SmokeResults/result.txt","PASS: native WPF window rendered "+shots.Length+" pages");
     Current.Shutdown(0);
     return;
    }
    window.ShowPage(shots[step].Page);
    window.UpdateLayout();
    Directory.CreateDirectory("SmokeResults");
    var b=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
    b.Render(window);
    var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));
    using(var file=File.Create("SmokeResults/"+shots[step].File))png.Save(file);
    step++;
   }
   catch(Exception ex)
   {
    System.Console.Error.WriteLine(ex.GetType().Name);
    Current.Shutdown(1);
   }
  };
  timer.Start();
 }
}
