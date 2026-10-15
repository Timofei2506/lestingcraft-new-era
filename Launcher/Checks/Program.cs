using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LestingCraft.Launcher;
static class Program
{
 static int assertions;
 static void Check(bool condition,string label){assertions++;if(!condition)throw new Exception(label);}
 static void Reject(Action a,string label){try{a();throw new Exception("Accepted: "+label);}catch(InvalidDataException){assertions++;}}
 static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
 static byte[] Zip(Dictionary<string,byte[]> files,CompressionLevel level=CompressionLevel.Fastest)
 {using var ms=new MemoryStream();using(var z=new ZipArchive(ms,ZipArchiveMode.Create,true))foreach(var p in files){using var s=z.CreateEntry(p.Key,level).Open();s.Write(p.Value);}return ms.ToArray();}
 sealed class Fake:HttpMessageHandler
 {
  public byte[] Archive=[];public byte[] ManifestZip=[];public ChannelFeed? Feed;public long Transferred;public bool IgnoreRange;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   byte[] b=request.RequestUri!.AbsolutePath switch{"/feed"=>JsonSerializer.SerializeToUtf8Bytes(Feed),"/manifest.zip"=>ManifestZip,_=>Archive};
   var range=request.Headers.Range?.Ranges.Single();int start=0,end=b.Length-1;bool partial=range!=null&&!IgnoreRange;
   if(partial){start=range!.From.HasValue?(int)range.From.Value:Math.Max(0,b.Length-(int)range.To!.Value);end=range.From.HasValue?(int)(range.To??(b.Length-1)):b.Length-1;}
   if(request.RequestUri.AbsolutePath=="/game.zip")Transferred+=end-start+1;
   var response=new HttpResponseMessage(partial?HttpStatusCode.PartialContent:HttpStatusCode.OK){Content=new ByteArrayContent(b[start..(end+1)])};if(partial)response.Content.Headers.ContentRange=new ContentRangeHeaderValue(start,end,b.Length);return Task.FromResult(response);
  }
  public GameManifest Publish(string build,Dictionary<string,byte[]> files)
  {
   var m=new GameManifest(1,build,"LestingCraft.exe",files.Select(p=>new GameFile(p.Key,p.Value.Length,Hash(p.Value))).ToArray());
   Archive=Zip(files);ManifestZip=Zip(new(){{"game-manifest.json",JsonSerializer.SerializeToUtf8Bytes(m)}});Feed=new(build,"https://fixture.blob.core.windows.net/game.zip","https://fixture.blob.core.windows.net/manifest.zip");return m;
  }
 }
 static async Task Main()
 {
  foreach(var bad in new[]{"../a","/a","a\\b","C:/evil","a/../b","a//b","file:stream","con.txt","a/aux","a.","a /b","a\n"})Reject(()=>GameManifest.SafePath(bad),bad);
  Check(GameManifest.SafePath("LestingCraft_Data/Managed/Assembly-CSharp.dll").Contains('/'),"normal nested path");
  Reject(()=>Updater.ArchiveUri("http://fixture.blob.core.windows.net/x"),"http rejected");Reject(()=>Updater.ArchiveUri("https://evil.example/x"),"foreign host rejected");
  using var handler=new Fake();using var http=new HttpClient(handler);string root=Path.Combine(Path.GetTempPath(),"lc-launcher-check-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(root);string saves=Path.Combine(root,"UnityLocalLow");Directory.CreateDirectory(saves);await File.WriteAllTextAsync(Path.Combine(saves,"world.json"),"precious save");
  try
  {
   byte[] shared=new byte[400000];RandomNumberGenerator.Fill(shared);var files=new Dictionary<string,byte[]>{{"LestingCraft.exe",Encoding.UTF8.GetBytes("first executable")},{"LestingCraft_Data/shared.dat",shared}};
   string a=new('a',40),b=new('b',40),c=new('c',40);handler.Publish(a,files);var u=new Updater(http,Path.Combine(root,"launcher"),"https://test.invalid/feed");var first=await u.Check("invite",default);
   await u.Install(first.Feed,first.Manifest,new Progress<UpdateProgress>(),default);Check(u.Installed?.Build==a,"initial install");
   string old=u.Installed!.Directory;Check(File.Exists(Path.Combine(u.Root,"versions",old,"LestingCraft.exe")),"EXE exists");
   files["LestingCraft.exe"]=Encoding.UTF8.GetBytes("second executable");handler.Publish(b,files);handler.Transferred=0;var second=await u.Check("invite",default);await u.Install(second.Feed,second.Manifest,new Progress<UpdateProgress>(),default);
   Check(u.Installed?.Build==b,"update switches build");Check(handler.Transferred<handler.Archive.Length/2,"unchanged big asset is not downloaded");Check(Directory.Exists(Path.Combine(u.Root,"versions",old)),"previous version retained");
   u.Rollback();Check(u.Installed?.Build==a,"rollback works");u.Rollback();Check(u.Installed?.Build==b,"forward rollback works");
   string pointer=File.ReadAllText(Path.Combine(u.Root,"current.json"));handler.Publish(c,files);var corrupt=await u.Check("invite",default);corrupt.Manifest.Files[0]=corrupt.Manifest.Files[0] with {Sha256=new string('0',64)};
   bool rejected=false;try{await u.Install(corrupt.Feed,corrupt.Manifest,new Progress<UpdateProgress>(),default);}catch(InvalidDataException){rejected=true;}Check(rejected,"corrupt checksum rejected");Check(File.ReadAllText(Path.Combine(u.Root,"current.json"))==pointer,"corruption does not switch active install");
   using var cancel=new CancellationTokenSource();cancel.Cancel();try{await u.Install(second.Feed,second.Manifest,new Progress<UpdateProgress>(),cancel.Token);}catch(OperationCanceledException){}
   Check(File.ReadAllText(Path.Combine(u.Root,"current.json"))==pointer,"cancel does not switch install");
   handler.IgnoreRange=true;rejected=false;try{var z=new RangeZip(http,new Uri(handler.Feed!.Archive));await z.Open(default);}catch(InvalidDataException){rejected=true;}Check(rejected,"200 instead of range rejected");handler.IgnoreRange=false;
   Check(await File.ReadAllTextAsync(Path.Combine(saves,"world.json"))=="precious save","Unity saves untouched");
   File.WriteAllText(Path.Combine(u.Root,"previous.json"),"{\"Directory\":\"../../escape\",\"Build\":\""+a+"\"}");Reject(()=>u.Rollback(),"invalid rollback path");Check(File.ReadAllText(Path.Combine(u.Root,"current.json"))==pointer,"bad rollback does not corrupt pointer");
   var duplicate=new GameManifest(1,a,"LestingCraft.exe",[new("LestingCraft.exe",1,new('a',64)),new("lestingcraft.exe",1,new('a',64))]);Reject(duplicate.Validate,"case-insensitive duplicate path");
   // Stored payloads as well as deflated ones are accepted and hash-checked.
   handler.Archive=Zip(files,CompressionLevel.NoCompression);var stored=new RangeZip(http,new Uri(handler.Feed!.Archive));await stored.Open(default);var storedFile=second.Manifest.Files[0];await stored.Extract(storedFile,Path.Combine(root,"stored.exe"),default);Check(Hash(await File.ReadAllBytesAsync(Path.Combine(root,"stored.exe")))==storedFile.Sha256,"stored ZIP extraction");
   Console.WriteLine($"PASS: {assertions} launcher checks — paths, ranges, changed files, hashes, rollback, cancellation, offline install state, save isolation.");
  }
  finally{Directory.Delete(root,true);}
 }
}
