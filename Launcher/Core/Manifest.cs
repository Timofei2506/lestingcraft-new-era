using System.Text.Json;
using System.Text.RegularExpressions;
namespace LestingCraft.Launcher;
public sealed record GameFile(string Path,long Size,string Sha256);
public sealed record GameManifest(int Schema,string Build,string Executable,GameFile[] Files)
{
 public static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true};
 public static string SafePath(string path)
 {
  if(string.IsNullOrWhiteSpace(path)||path.Length>220||path.Contains('\\')||path.StartsWith('/')||path.Split('/').Any(s=>s is "" or "." or ".."||s.EndsWith('.')||s.EndsWith(' ')||s.Any(c=>c<32||"<>:\"|?*".Contains(c))||Regex.IsMatch(s,@"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(\.|$)",RegexOptions.IgnoreCase)))throw new InvalidDataException("Небезопасный путь в пакете.");
  return path;
 }
 public void Validate()
 {
  if(Schema!=1||!Regex.IsMatch(Build??"",@"^[a-f0-9]{40}$")||Files is null||Files.Length is <1 or >15000)throw new InvalidDataException("Некорректный манифест.");
  if(SafePath(Executable)!="LestingCraft.exe")throw new InvalidDataException("Неверный исполняемый файл.");
  var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
  foreach(var f in Files){SafePath(f.Path);if(!paths.Add(f.Path)||f.Size<0||f.Size>2L*1024*1024*1024||!Regex.IsMatch(f.Sha256??"",@"^[a-f0-9]{64}$"))throw new InvalidDataException("Некорректный файл манифеста.");total=checked(total+f.Size);}
  if(total>8L*1024*1024*1024||!paths.Contains(Executable))throw new InvalidDataException("Некорректный размер или состав игры.");
 }
 public static string Inside(string root,string relative)
 {
  SafePath(relative);string result=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative.Replace('/',System.IO.Path.DirectorySeparatorChar)));
  string prefix=System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar;
  if(!result.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Путь вне папки игры.");return result;
 }
}
public sealed record ChannelFeed(string Build,string Archive,string ManifestArchive);
public sealed record InstalledGame(string Directory,string Build);
public sealed record UpdateProgress(string Message,long Done,long Total)
{public double Percent=>Total>0?Math.Clamp(100d*Done/Total,0,100):0;}
