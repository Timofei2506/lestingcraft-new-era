using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
namespace LestingCraft.Launcher.Desktop;
public partial class MainWindow:Window
{
 readonly Updater updater;readonly HttpClient http;readonly string root;ChannelFeed? feed;GameManifest? manifest;CancellationTokenSource? cancellation;bool busy;
 public MainWindow()
 {
  InitializeComponent();root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LestingCraftLauncher");
  http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=true}){Timeout=TimeSpan.FromMinutes(12)};http.DefaultRequestHeaders.UserAgent.ParseAdd("LestingCraftLauncher/0.1");updater=new Updater(http,root);
  try{string path=Path.Combine(root,"invite.dat");if(File.Exists(path))Invite.Password=Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser));}catch(CryptographicException){}
  if(Invite.Password.Length==0){string bootstrap=Path.Combine(AppContext.BaseDirectory,"test-access.key");if(File.Exists(bootstrap)){string key=File.ReadAllText(bootstrap).Trim();if(System.Text.RegularExpressions.Regex.IsMatch(key,@"^[a-f0-9]{64}$"))Invite.Password=key;}}
  Refresh();Loaded+=async(_,_)=>{if(Environment.GetCommandLineArgs().Contains("--smoke"))Status.Text="Интерфейс готов. Игра пока не установлена.";else await CheckChannel();};
  var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};timer.Tick+=(_,_)=>Refresh();timer.Start();
  Closing+=(_,e)=>{if(busy){cancellation?.Cancel();e.Cancel=true;Status.Text="Отменяю операцию. Прежняя установка сохраняется.";}else{timer.Stop();http.Dispose();}};
 }
 void Refresh()
 {
  VersionLabel.Text=updater.Installed==null?"Установленная версия: пока нет":"Установлено: "+updater.Installed.Build[..8];
  bool running=updater.Running;Play.IsEnabled=!busy&&updater.Installed!=null&&!running;Play.Content=running?"Игра запущена":"Играть  →";
  Update.IsEnabled=!busy&&!running&&manifest!=null;Check.IsEnabled=!busy;Import.IsEnabled=!busy&&manifest!=null;Cancel.IsEnabled=busy;Invite.IsEnabled=!busy;
  Update.Content=updater.Installed?.Build==manifest?.Build?"Починить файлы":"Обновить";
 }
 async Task Run(Func<CancellationToken,Task> action)
 {
  if(busy)return;busy=true;cancellation=new CancellationTokenSource();Refresh();
  try{await action(cancellation.Token);}catch(OperationCanceledException){Status.Text="Операция отменена. Прежняя игра не изменена.";}catch(Exception e){Status.Text=e is HttpRequestException?"Не удалось связаться с каналом. Установленную игру можно запустить офлайн.":e.Message;}finally{busy=false;cancellation.Dispose();cancellation=null;Progress.IsIndeterminate=false;Refresh();}
 }
 async Task CheckChannel()=>await Run(async ct=>
 {
  Status.Text="Проверка канала обновлений…";Progress.IsIndeterminate=true;
  (feed,manifest)=await updater.Check(Invite.Password,ct);
  File.WriteAllBytes(Path.Combine(root,"invite.dat"),ProtectedData.Protect(Encoding.UTF8.GetBytes(Invite.Password),null,DataProtectionScope.CurrentUser));
  Status.Text=updater.Installed?.Build==manifest.Build?"Установлена текущая версия. При необходимости можно проверить и восстановить файлы.":"Доступна версия "+manifest.Build[..8]+". Нажми «Обновить».";
 });
 async Task Install(string? existing=null)=>await Run(async ct=>
 {
  // Refresh short-lived artifact URLs immediately before every installation.
  (feed,manifest)=await updater.Check(Invite.Password,ct);Progress.IsIndeterminate=false;
  await updater.Install(feed,manifest,new Progress<UpdateProgress>(p=>{Status.Text=p.Message;Progress.Value=p.Percent;}),ct,existing);
  Status.Text="Всё готово. Приятной экспедиции!";
 });
 async void CheckClick(object sender,RoutedEventArgs e)=>await CheckChannel();
 async void UpdateClick(object sender,RoutedEventArgs e)=>await Install();
 async void ImportClick(object sender,RoutedEventArgs e){var dialog=new OpenFolderDialog{Title="Папка с уже скачанной LestingCraft.exe"};if(dialog.ShowDialog(this)==true){if(!File.Exists(Path.Combine(dialog.FolderName,"LestingCraft.exe"))){Status.Text="В этой папке нет LestingCraft.exe.";return;}await Install(dialog.FolderName);}}
 void PlayClick(object sender,RoutedEventArgs e){try{updater.Play();Status.Text="Игра запущена. Лаунчер можно оставить открытым.";}catch(Exception ex){Status.Text=ex.Message;}Refresh();}
 void RollbackClick(object sender,RoutedEventArgs e){if(busy)return;try{updater.Rollback();Status.Text="Возвращена предыдущая версия.";}catch(Exception ex){Status.Text=ex.Message;}Refresh();}
 void CancelClick(object sender,RoutedEventArgs e)=>cancellation?.Cancel();
}
