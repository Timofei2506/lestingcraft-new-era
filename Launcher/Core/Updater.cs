using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace LestingCraft.Launcher;
public sealed class Updater
{
 public const string Endpoint="https://lestingcraft.kroterallitir.workers.dev/api/launcher/latest";
 public string Root {get;}
 public InstalledGame? Installed {get;private set;}
 readonly HttpClient http;readonly string feedUrl;Process? running;readonly SemaphoreSlim gate=new(1);
 public Updater(HttpClient client,string root,string feed=Endpoint){http=client;Root=Path.GetFullPath(root);feedUrl=feed;Directory.CreateDirectory(Root);LoadInstalled();}
 void LoadInstalled()
 {
  try{var value=JsonSerializer.Deserialize<InstalledGame>(File.ReadAllText(Path.Combine(Root,"current.json")),GameManifest.Json);if(value!=null&&Regex.IsMatch(value.Directory,@"^[a-f0-9]{32}$")&&Regex.IsMatch(value.Build,@"^[a-f0-9]{40}$")&&File.Exists(Path.Combine(Root,"versions",value.Directory,"LestingCraft.exe")))Installed=value;}catch(IOException){}catch(JsonException){}
 }
 public bool Running
 {
  get
  {
   if(running!=null&&!running.HasExited)return true;if(Installed==null)return false;
   string exe=Path.Combine(Root,"versions",Installed.Directory,"LestingCraft.exe");
   foreach(var p in Process.GetProcessesByName("LestingCraft"))using(p){try{if(string.Equals(p.MainModule?.FileName,exe,StringComparison.OrdinalIgnoreCase))return true;}catch(System.ComponentModel.Win32Exception){return true;}catch(InvalidOperationException){}}
   return false;
  }
 }
 public void Play()
 {
  if(Installed==null||Running)return;string dir=Path.Combine(Root,"versions",Installed.Directory);
  running=Process.Start(new ProcessStartInfo(Path.Combine(dir,"LestingCraft.exe")){WorkingDirectory=dir,UseShellExecute=false});
 }
 public static Uri ArchiveUri(string value)
 {
  if(!Uri.TryCreate(value,UriKind.Absolute,out var u)||u.Scheme!="https"||!string.IsNullOrEmpty(u.UserInfo)||(!u.Host.EndsWith(".blob.core.windows.net",StringComparison.OrdinalIgnoreCase)&&!u.Host.EndsWith(".actions.githubusercontent.com",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Недоверенный адрес пакета.");return u;
 }
 public async Task<(ChannelFeed Feed,GameManifest Manifest)> Check(string invite,CancellationToken ct)
 {
  using var request=new HttpRequestMessage(HttpMethod.Get,feedUrl);if(!string.IsNullOrWhiteSpace(invite))request.Headers.Authorization=new("Bearer",invite.Trim());
  using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
  if((int)response.StatusCode==401)throw new InvalidOperationException("Нужен код приглашения в тестовый канал.");
  if((int)response.StatusCode==503)throw new InvalidOperationException("Новый тестовый пакет пока не готов. Установленную игру можно запустить офлайн.");
  response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>256*1024)throw new InvalidDataException("Слишком большой ответ канала.");
  using var body=await response.Content.ReadAsStreamAsync(ct);var feed=await JsonSerializer.DeserializeAsync<ChannelFeed>(body,GameManifest.Json,ct)??throw new InvalidDataException("Нет данных канала.");
  ArchiveUri(feed.Archive);var manifestUrl=ArchiveUri(feed.ManifestArchive);
  using var mr=await http.GetAsync(manifestUrl,HttpCompletionOption.ResponseHeadersRead,ct);mr.EnsureSuccessStatusCode();
  if(mr.Content.Headers.ContentLength>2*1024*1024)throw new InvalidDataException("Слишком большой манифест.");
  await using var ms=new MemoryStream();await using(var input=await mr.Content.ReadAsStreamAsync(ct)){byte[] buffer=new byte[8192];int n;while((n=await input.ReadAsync(buffer,ct))>0){if(ms.Length+n>2*1024*1024)throw new InvalidDataException("Слишком большой манифест.");await ms.WriteAsync(buffer.AsMemory(0,n),ct);}}
  ms.Position=0;using var zip=new ZipArchive(ms,ZipArchiveMode.Read);var entry=zip.GetEntry("game-manifest.json")??throw new InvalidDataException("Манифест не найден.");if(entry.Length>1024*1024)throw new InvalidDataException("Манифест превышает лимит.");
  await using var stream=entry.Open();var manifest=await JsonSerializer.DeserializeAsync<GameManifest>(stream,GameManifest.Json,ct)??throw new InvalidDataException("Некорректный манифест.");
  manifest.Validate();if(manifest.Build!=feed.Build)throw new InvalidDataException("Версии канала и пакета различаются.");return(feed,manifest);
 }
 static void NoLinks(string root)
 {
  if(!Directory.Exists(root))return;
  if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new IOException("Ссылки в папке установки запрещены.");
  foreach(string dir in Directory.EnumerateDirectories(root)){if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)throw new IOException("Ссылки в папке установки запрещены.");NoLinks(dir);}
  foreach(string file in Directory.EnumerateFiles(root))if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Ссылки в папке установки запрещены.");
 }
 async Task<bool> Matches(string path,GameFile file,CancellationToken ct)
 {
  if(!File.Exists(path)||new FileInfo(path).Length!=file.Size)return false;
  await using var input=File.OpenRead(path);return Convert.ToHexString(await SHA256.HashDataAsync(input,ct)).Equals(file.Sha256,StringComparison.OrdinalIgnoreCase);
 }
 public async Task Install(ChannelFeed feed,GameManifest manifest,IProgress<UpdateProgress> progress,CancellationToken ct,string? existingFolder=null)
 {
  await gate.WaitAsync(ct);string stage=Path.Combine(Root,"staging-"+Guid.NewGuid().ToString("N"));
  try
  {
   manifest.Validate();if(feed.Build!=manifest.Build)throw new InvalidDataException("Разные версии пакета.");if(Running)throw new InvalidOperationException("Сначала закройте игру.");NoLinks(Root);
   string? old=existingFolder??(Installed==null?null:Path.Combine(Root,"versions",Installed.Directory));if(old!=null)NoLinks(old);
   Directory.CreateDirectory(stage);long total=manifest.Files.Sum(f=>f.Size),done=0;RangeZip? archive=null;
   foreach(var file in manifest.Files)
   {
    ct.ThrowIfCancellationRequested();string target=GameManifest.Inside(stage,file.Path),source=old==null?"":GameManifest.Inside(old,file.Path);
    progress.Report(new("Проверка / обновление: "+file.Path,done,total));
    if(old!=null&&await Matches(source,file,ct)){Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target);if(!await Matches(target,file,ct))throw new InvalidDataException("Копия файла изменилась во время проверки.");}
    else {if(archive==null){archive=new RangeZip(http,ArchiveUri(feed.Archive));await archive.Open(ct);}await archive.Extract(file,target,ct,n=>progress.Report(new("Загрузка: "+file.Path,done+n,total)));}
    done+=file.Size;progress.Report(new("Файлы проверены",done,total));
   }
   // Complete version first; only then atomically switch the small pointer. Saves live in Unity LocalLow, never here.
   await File.WriteAllTextAsync(Path.Combine(stage,"install-manifest.json"),JsonSerializer.Serialize(manifest),ct);
   ct.ThrowIfCancellationRequested();string id=Guid.NewGuid().ToString("N"),versions=Path.Combine(Root,"versions");Directory.CreateDirectory(versions);Directory.Move(stage,Path.Combine(versions,id));
   var next=new InstalledGame(id,manifest.Build);string pointer=Path.Combine(Root,"current.json"),temp=Path.Combine(Root,"current.next");
   await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(next),ct);
   if(File.Exists(pointer))File.Copy(pointer,Path.Combine(Root,"previous.json"),true);
   File.Move(temp,pointer,true);Installed=next;progress.Report(new("Игра готова",total,total));
  }
  finally {if(Directory.Exists(stage))Directory.Delete(stage,true);gate.Release();}
 }
 public void Rollback()
 {
  if(Running)throw new InvalidOperationException("Сначала закройте игру.");string previous=Path.Combine(Root,"previous.json");if(!File.Exists(previous))throw new InvalidOperationException("Предыдущей версии пока нет.");
  NoLinks(Root);var candidate=JsonSerializer.Deserialize<InstalledGame>(File.ReadAllText(previous),GameManifest.Json);
  if(candidate==null||!Regex.IsMatch(candidate.Directory,@"^[a-f0-9]{32}$")||!Regex.IsMatch(candidate.Build,@"^[a-f0-9]{40}$")||!File.Exists(Path.Combine(Root,"versions",candidate.Directory,"LestingCraft.exe")))throw new InvalidDataException("Предыдущая версия повреждена.");
  var saved=Installed;string current=Path.Combine(Root,"current.json"),next=Path.Combine(Root,"rollback.next");File.Copy(previous,next,true);File.Move(next,current,true);Installed=candidate;
  if(saved!=null)File.WriteAllText(previous,JsonSerializer.Serialize(saved));
 }
}
