using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
namespace LestingCraft.Launcher;
/// <summary>Reads ZIP directory with HTTP Range; only changed file payloads are transferred.</summary>
public sealed class RangeZip(HttpClient http,Uri url)
{
 public sealed record Entry(string Name,ushort Method,ushort Flags,uint Compressed,uint Size,uint Offset);
 readonly Dictionary<string,Entry> entries=new(StringComparer.OrdinalIgnoreCase);
 static ushort U16(ReadOnlySpan<byte>b,int p)=>BinaryPrimitives.ReadUInt16LittleEndian(b[p..]);
 static uint U32(ReadOnlySpan<byte>b,int p)=>BinaryPrimitives.ReadUInt32LittleEndian(b[p..]);
 static void CheckResponse(HttpResponseMessage r,long? start,long? end)
 {
  if(r.StatusCode!=HttpStatusCode.PartialContent||r.Content.Headers.ContentRange is not { } range||!range.HasLength||!range.HasRange||(start.HasValue&&range.From!=start)||(end.HasValue&&range.To!=end))throw new InvalidDataException("Сервер не поддерживает безопасную частичную загрузку. Установка не изменена.");
 }
 async Task<byte[]> Bytes(long? start,long end,int limit,CancellationToken ct)
 {
  using var req=new HttpRequestMessage(HttpMethod.Get,url);req.Headers.Range=start.HasValue?new RangeHeaderValue(start,end):new RangeHeaderValue(null,end);
  using var response=await http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct);CheckResponse(response,start,start.HasValue?end:null);
  long length=response.Content.Headers.ContentRange!.To!.Value-response.Content.Headers.ContentRange.From!.Value+1;
  if(length>limit||length<=0)throw new InvalidDataException("Слишком большой диапазон ZIP.");
  await using var source=await response.Content.ReadAsStreamAsync(ct);var bytes=new byte[(int)length];await source.ReadExactlyAsync(bytes,ct);return bytes;
 }
 public async Task Open(CancellationToken ct)
 {
  byte[] tail=await Bytes(null,65557,65557,ct);int eocd=-1;
  for(int i=tail.Length-22;i>=0;i--)if(U32(tail,i)==0x06054b50&&i+22+U16(tail,i+20)==tail.Length){eocd=i;break;}
  if(eocd<0)throw new InvalidDataException("Не найдена таблица ZIP.");
  int count=U16(tail,eocd+10);uint length=U32(tail,eocd+12),offset=U32(tail,eocd+16);
  if(U16(tail,eocd+4)!=0||U16(tail,eocd+6)!=0||count==65535||length==uint.MaxValue||offset==uint.MaxValue||count>20000||length>16*1024*1024||length<46)throw new InvalidDataException("Неподдерживаемый ZIP64/разбитый архив.");
  byte[] directory=await Bytes(offset,(long)offset+length-1,16*1024*1024,ct);int p=0;
  for(int i=0;i<count;i++)
  {
   if(p+46>directory.Length||U32(directory,p)!=0x02014b50)throw new InvalidDataException("Повреждена таблица ZIP.");
   ushort flags=U16(directory,p+8),method=U16(directory,p+10);int n=U16(directory,p+28),extra=U16(directory,p+30),comment=U16(directory,p+32);
   if(p+46+n+extra+comment>directory.Length||(flags&1)!=0||method is not (0 or 8))throw new InvalidDataException("Неподдерживаемый элемент ZIP.");
   string name=Encoding.UTF8.GetString(directory,p+46,n);
   if(!name.EndsWith('/')){GameManifest.SafePath(name);if(!entries.TryAdd(name,new(name,method,flags,U32(directory,p+20),U32(directory,p+24),U32(directory,p+42))))throw new InvalidDataException("Повторяющийся путь ZIP.");}
   p+=46+n+extra+comment;
  }
 }
 public async Task Extract(GameFile file,string target,CancellationToken ct,Action<long>? downloaded=null)
 {
  if(!entries.TryGetValue(file.Path,out var entry)||entry.Size!=file.Size||entry.Compressed>2L*1024*1024*1024)throw new InvalidDataException("Файл не соответствует манифесту.");
  byte[] header=await Bytes(entry.Offset,entry.Offset+29,30,ct);
  if(U32(header,0)!=0x04034b50||U16(header,8)!=entry.Method||(U16(header,6)&1)!=0)throw new InvalidDataException("Повреждён заголовок файла.");
  long start=(long)entry.Offset+30+U16(header,26)+U16(header,28);
  Directory.CreateDirectory(Path.GetDirectoryName(target)!);
  if(entry.Compressed==0){if(file.Size!=0)throw new InvalidDataException("Пустой ZIP payload.");await File.WriteAllBytesAsync(target,[],ct);}
  else
  {
   using var req=new HttpRequestMessage(HttpMethod.Get,url);req.Headers.Range=new RangeHeaderValue(start,start+entry.Compressed-1);
   using var response=await http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct);CheckResponse(response,start,start+entry.Compressed-1);
   await using var source=await response.Content.ReadAsStreamAsync(ct);
   await using Stream decoded=entry.Method==8?new DeflateStream(source,CompressionMode.Decompress,leaveOpen:true):source;
   await using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true);byte[] buffer=new byte[81920];long written=0;
   int n;while((n=await decoded.ReadAsync(buffer,ct))>0){written+=n;if(written>file.Size)throw new InvalidDataException("Файл превышает размер манифеста.");await output.WriteAsync(buffer.AsMemory(0,n),ct);downloaded?.Invoke(written);}
   if(written!=file.Size)throw new InvalidDataException("Неполная загрузка.");
  }
  await using var check=File.OpenRead(target);string hash=Convert.ToHexString(await SHA256.HashDataAsync(check,ct)).ToLowerInvariant();
  if(hash!=file.Sha256)throw new InvalidDataException("Контрольная сумма SHA-256 не совпала. Прежняя игра сохранена.");
 }
}
