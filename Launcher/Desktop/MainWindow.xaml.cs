using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
namespace LestingCraft.Launcher.Desktop;

public partial class MainWindow : Window
{
 public const string LauncherVersion = "0.2.0";
 const int JournalLimit = 400;

 readonly Updater updater;
 readonly HttpClient http;
 readonly string root;
 readonly ObservableCollection<string> journal = new();
 ChannelFeed? feed;
 GameManifest? manifest;
 CancellationTokenSource? cancellation;
 bool busy;

 public MainWindow()
 {
  InitializeComponent();
  root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LestingCraftLauncher");
  http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = TimeSpan.FromMinutes(12) };
  http.DefaultRequestHeaders.UserAgent.ParseAdd("LestingCraftLauncher/" + LauncherVersion);
  updater = new Updater(http, root);

  RootPath.Text = updater.Root;
  LogList.ItemsSource = journal;
  try { string path = Path.Combine(root, "invite.dat"); if (File.Exists(path)) Invite.Password = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser)); }
  catch (CryptographicException) { }
  if (Invite.Password.Length == 0)
  {
   string bootstrap = Path.Combine(AppContext.BaseDirectory, "test-access.key");
   if (File.Exists(bootstrap))
   {
    string key = File.ReadAllText(bootstrap).Trim();
    if (System.Text.RegularExpressions.Regex.IsMatch(key, @"^[a-f0-9]{64}$")) Invite.Password = key;
   }
  }

  Refresh();
  NavHome.IsChecked = true;
  ShowPage(0);
  Loaded += async (_, _) =>
  {
   if (Environment.GetCommandLineArgs().Contains("--smoke")) Say("Интерфейс готов. Игра пока не установлена.");
   else await CheckChannel();
  };
  var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
  timer.Tick += (_, _) => Refresh();
  timer.Start();
  Closing += (_, e) =>
  {
   if (busy) { cancellation?.Cancel(); e.Cancel = true; Say("Отменяю операцию. Прежняя установка сохраняется."); }
   else { timer.Stop(); http.Dispose(); }
  };
 }

 // ---------- Navigation ----------
 static readonly string[] PageTitles = { "Главная", "Установка", "Настройки" };

 public void ShowPage(int index)
 {
  if (PageHome == null || PageVersion == null || PageSettings == null) return;
  UIElement[] pages = { PageHome, PageVersion, PageSettings };
  for (int i = 0; i < pages.Length; i++) pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
  pages[index].BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
  {
   EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
  });
  PageTitle.Text = PageTitles[index];
 }

 void NavChecked(object sender, RoutedEventArgs e)
 {
  if (sender is RadioButton { Tag: string tag } && int.TryParse(tag, out int index)) ShowPage(index);
 }

 void TitleDrag(object sender, MouseButtonEventArgs e)
 {
  if (e.LeftButton == MouseButtonState.Pressed) DragMove();
 }

 void MinClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
 void CloseClick(object sender, RoutedEventArgs e) => Close();

 // ---------- State presentation ----------
 void Refresh()
 {
  bool installed = updater.Installed != null;
  bool running = updater.Running;
  bool current = installed && manifest != null && updater.Installed!.Build == manifest.Build;

  VersionLabel.Text = installed ? "Версия " + updater.Installed!.Build[..8] : "Игра не установлена";
  BuildDetail.Text = installed ? "Сборка " + updater.Installed!.Build : "Сборка появится после первой установки";
  ChipText.Text = installed ? "Установлена сборка " + updater.Installed!.Build[..8] : "Сборка не установлена";

  if (!installed) SetState("НЕ УСТАНОВЛЕНА", "#A3B098", "#2A3024", "Игра не установлена. Нажмите «Обновить», чтобы скачать её.");
  else if (manifest == null) SetState("ОФЛАЙН-ВЕРСИЯ", "#9CC1D8", "#1C2A33", "Офлайн-версия готова к запуску. Проверьте канал, чтобы узнать о новых сборках.");
  else if (current) SetState("АКТУАЛЬНА", "#8CC66A", "#1F2E1A", "Установлена текущая сборка канала.");
  else SetState("ДОСТУПНО ОБНОВЛЕНИЕ", "#EFBA69", "#2B2413", "Доступна сборка " + manifest.Build[..8] + ". Установленная версия не изменена.");

  SidebarState.Text = installed ? (running ? "Игра запущена" : "Офлайн-игра готова") : "Игра не установлена";

  Play.IsEnabled = !busy && installed && !running;
  Play.Content = running ? "ИГРА ЗАПУЩЕНА" : "ИГРАТЬ";
  Update.IsEnabled = !busy && !running && manifest != null;
  Update.Content = current ? "Починить файлы" : "Обновить";
  Check.IsEnabled = !busy;
  SaveKey.IsEnabled = !busy;
  Import.IsEnabled = !busy && manifest != null;
  RollbackButton.IsEnabled = !busy && !running;
  Cancel.IsEnabled = busy;
  Invite.IsEnabled = !busy;
 }

 void SetState(string tag, string foreground, string background, string detail)
 {
  StateTag.Text = tag;
  StateTag.Foreground = Brush(foreground);
  StateBorder.Background = Brush(background);
  StateBorder.ToolTip = detail;
 }

 static readonly Dictionary<string, SolidColorBrush> brushes = new();

 static SolidColorBrush Brush(string hex)
 {
  if (brushes.TryGetValue(hex, out var cached)) return cached;
  var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
  brush.Freeze();
  brushes[hex] = brush;
  return brush;
 }

 void SetBusy(bool value)
 {
  if (value)
  {
   ShimmerLayer.Visibility = Visibility.Visible;
   ShimmerTransform.BeginAnimation(TranslateTransform.XProperty,
    new DoubleAnimation(-170, 900, TimeSpan.FromSeconds(1.8)) { RepeatBehavior = RepeatBehavior.Forever });
  }
  else
  {
   ShimmerTransform.BeginAnimation(TranslateTransform.XProperty, null);
   ShimmerLayer.Visibility = Visibility.Collapsed;
   PercentText.Text = "";
  }
 }

 // ---------- Messages ----------
 void SetStatus(string text) => Status.Text = text;

 void Say(string text)
 {
  Status.Text = text;
  journal.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + text);
  while (journal.Count > JournalLimit) journal.RemoveAt(0);
  LogList.ScrollIntoView(journal[^1]);
 }

 // ---------- Operations (logic unchanged) ----------
 async Task Run(Func<CancellationToken, Task> action)
 {
  if (busy) return;
  busy = true;
  cancellation = new CancellationTokenSource();
  SetBusy(true);
  Refresh();
  try { await action(cancellation.Token); }
  catch (OperationCanceledException) { Say("Операция отменена. Прежняя игра не изменена."); }
  catch (Exception e) { Say(e is HttpRequestException ? "Не удалось связаться с каналом. Установленную игру можно запустить офлайн." : e.Message); }
  finally
  {
   busy = false;
   cancellation.Dispose();
   cancellation = null;
   SetBusy(false);
   Refresh();
  }
 }

 async Task CheckChannel() => await Run(async ct =>
 {
  Say("Проверка канала обновлений…");
  (feed, manifest) = await updater.Check(Invite.Password, ct);
  File.WriteAllBytes(Path.Combine(root, "invite.dat"), ProtectedData.Protect(Encoding.UTF8.GetBytes(Invite.Password), null, DataProtectionScope.CurrentUser));
  Say(updater.Installed?.Build == manifest.Build
   ? "Установлена текущая версия. При необходимости можно проверить и восстановить файлы."
   : "Доступна версия " + manifest.Build[..8] + ". Нажмите «Обновить».");
 });

 async Task Install(string? existing = null) => await Run(async ct =>
 {
  // Refresh short-lived artifact URLs immediately before every installation.
  SetStatus("Получаю актуальный пакет…");
  (feed, manifest) = await updater.Check(Invite.Password, ct);
  await updater.Install(feed, manifest, new Progress<UpdateProgress>(p =>
  {
   SetStatus(p.Message);
   Progress.Value = p.Percent;
   PercentText.Text = p.Percent.ToString("0") + "%";
  }), ct, existing);
  Say("Всё готово. Приятной экспедиции!");
 });

 // ---------- Handlers ----------
 async void CheckClick(object sender, RoutedEventArgs e) => await CheckChannel();
 async void UpdateClick(object sender, RoutedEventArgs e) => await Install();

 async void ImportClick(object sender, RoutedEventArgs e)
 {
  var dialog = new OpenFolderDialog { Title = "Папка с уже скачанной LestingCraft.exe" };
  if (dialog.ShowDialog(this) == true)
  {
   if (!File.Exists(Path.Combine(dialog.FolderName, "LestingCraft.exe")))
   {
    Say("В этой папке нет LestingCraft.exe.");
    return;
   }
   await Install(dialog.FolderName);
  }
 }

 void PlayClick(object sender, RoutedEventArgs e)
 {
  try { updater.Play(); Say("Игра запущена. Лаунчер можно оставить открытым."); }
  catch (Exception ex) { Say(ex.Message); }
  Refresh();
 }

 void RollbackClick(object sender, RoutedEventArgs e)
 {
  if (busy) return;
  try { updater.Rollback(); Say("Возвращена предыдущая версия."); }
  catch (Exception ex) { Say(ex.Message); }
  Refresh();
 }

 void CancelClick(object sender, RoutedEventArgs e) => cancellation?.Cancel();

 void OpenFolderClick(object sender, RoutedEventArgs e)
 {
  try
  {
   Directory.CreateDirectory(root);
   Process.Start(new ProcessStartInfo { FileName = root, UseShellExecute = true });
  }
  catch (Exception ex) { Say("Не удалось открыть папку: " + ex.Message); }
 }

 void CopyLogClick(object sender, RoutedEventArgs e)
 {
  Clipboard.SetText(string.Join(Environment.NewLine, journal));
  SetStatus("Журнал скопирован в буфер обмена.");
 }
}
