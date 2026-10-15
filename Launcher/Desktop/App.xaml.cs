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
  if(e.Args.Contains("--smoke"))
  {
   var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};
   timer.Tick+=(_,_)=>{timer.Stop();try{var w=MainWindow;var b=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(w);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));Directory.CreateDirectory("SmokeResults");using var file=File.Create("SmokeResults/launcher.png");png.Save(file);File.WriteAllText("SmokeResults/result.txt","PASS: native WPF window rendered");Shutdown(0);}catch(Exception ex){Console.Error.WriteLine(ex.GetType().Name);Shutdown(1);}};timer.Start();
  }
 }
}
