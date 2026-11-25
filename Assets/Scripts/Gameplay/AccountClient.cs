using System;using System.Collections;using System.Collections.Generic;using System.Text;using UnityEngine;using UnityEngine.Networking;
namespace LestingCraft.Gameplay
{
 [Serializable] public sealed class AchievementDefinition {public string id,name,category,counter,description,title;public int target,xp;}
 [Serializable] public sealed class AchievementCatalog {public AchievementDefinition[] achievements;}
 [Serializable] public sealed class ProfileStat {public string key;public int value;}
 [Serializable] public sealed class AccountProfile {public string uid,username,avatar,bio,title,titleName,integrity;public int xp,level,revision;public string[] achievements;public ProfileStat[] stats;}
 [Serializable] public sealed class AccountCard {public string username,avatar,bio,title,titleName;public int xp,level,achievementCount;public ProfileStat[] stats;}
 [Serializable] public sealed class AccountReply {public bool ok;public string token,recoveryCode,message,batchId;public AccountProfile profile;public AccountCard card;}
 [Serializable] public sealed class AccountRequest {public string username,password,newPassword,recoveryCode,title,avatar,bio;}
 [Serializable] public sealed class ProfileEvent {public string key,mode;public int value;}
 [Serializable] public sealed class ProfileBatch {public string batchId,deviceId;public int sequence;public ProfileEvent[] events;}
 [Serializable] public sealed class ProfileQueue {public int sequence;public ProfileEvent[] active;public ProfileBatch pending;}
 public sealed class AccountClient:MonoBehaviour
 {
  public string Endpoint {get;private set;}public AccountProfile Profile {get;private set;}public string Message="Войдите до экспедиции, чтобы сохранять достижения в аккаунт.";public string RecoveryCode {get;private set;}public bool Busy {get;private set;}public bool Logged=>Profile!=null;public AchievementDefinition[] Achievements {get;private set;}
  public readonly Dictionary<string,AccountCard> Cards=new Dictionary<string,AccountCard>(StringComparer.OrdinalIgnoreCase);
  string token,device;float nextSync,nextSave;ProfileQueue queue=new ProfileQueue();readonly Dictionary<string,ProfileEvent> active=new Dictionary<string,ProfileEvent>();
  public static int XpFloorForLevel(int level){int l=Math.Max(1,level);return 100*l*(l-1);}
  public static float XpProgress01(int xp,int level){int lo=XpFloorForLevel(level),hi=XpFloorForLevel(level+1);return hi>lo?Mathf.Clamp01((xp-lo)/(float)(hi-lo)):1f;}
  public void Initialize(string endpoint){Endpoint=(endpoint??"").TrimEnd('/');Achievements=JsonUtility.FromJson<AchievementCatalog>(Resources.Load<TextAsset>("Achievements").text).achievements;}
  public void Authenticate(string action,string username,string password,string recovery,Action done){if(Busy)return;SaveQueue();StartCoroutine(Request(action,new AccountRequest{username=(username??"").Trim().TrimStart('@'),password=password,recoveryCode=recovery},()=>{if(Logged){active.Clear();string key="LC.ProfileQueue."+Profile.uid;try{queue=JsonUtility.FromJson<ProfileQueue>(PlayerPrefs.GetString(key,""))??new ProfileQueue();}catch{queue=new ProfileQueue();}if(queue.active!=null)foreach(var e in queue.active)active[e.key]=e;device=PlayerPrefs.GetString("LC.ProfileDevice."+Profile.uid,"");if(device.Length!=32){device=Guid.NewGuid().ToString("N");PlayerPrefs.SetString("LC.ProfileDevice."+Profile.uid,device);}SaveQueue();CloudRefreshQuiet();}done?.Invoke();}));}
  public void ChangeTitle(string id,Action done){UpdateProfile(id,null,null,done);}
  public void UpdateProfile(string title,string avatar,string bio,Action done)
  {
   if(!Logged||Busy){done?.Invoke();return;}
   var req=new AccountRequest{
    title=title??(Profile.title??""),
    avatar=string.IsNullOrEmpty(avatar)?(string.IsNullOrEmpty(Profile.avatar)?"pioneer":Profile.avatar):avatar,
    bio=bio??(Profile.bio??"")
   };
   StartCoroutine(Request("profile",req,done));
  }
  public void LookupCard(string username,Action<AccountCard,string> done)
  {
   string clean=(username??"").Trim().TrimStart('@');
   if(!Logged||string.IsNullOrEmpty(clean)){done?.Invoke(null,"Войдите в аккаунт для просмотра карточки игрока.");return;}
   StartCoroutine(LookupCardRoutine(clean,done));
  }
  IEnumerator LookupCardRoutine(string username,Action<AccountCard,string> done)
  {
   Uri address;if(!Uri.TryCreate(Endpoint,UriKind.Absolute,out address)||(address.Scheme!="https"&&!address.IsLoopback)){done?.Invoke(null,"Для аккаунта нужен HTTPS-сервис.");yield break;}
   string body="{\"username\":\""+Esc(username)+"\"}";
   using(var request=new UnityWebRequest(Endpoint+"/api/accounts/card","POST"))
   {
    request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));request.downloadHandler=new DownloadHandlerBuffer();
    request.SetRequestHeader("Content-Type","application/json");if(!string.IsNullOrEmpty(token))request.SetRequestHeader("Authorization","Bearer "+token);request.timeout=15;
    yield return request.SendWebRequest();
    AccountReply reply=null;try{reply=JsonUtility.FromJson<AccountReply>(request.downloadHandler.text);}catch{}
    if(request.result==UnityWebRequest.Result.Success&&reply!=null&&reply.ok&&reply.card!=null)
    {
     Cards[reply.card.username]=reply.card;
     done?.Invoke(reply.card,null);
    }
    else done?.Invoke(null,reply?.message??"Профиль игрока пока недоступен.");
   }
  }
  public void ChangePassword(string current,string next,Action done){if(Logged&&!Busy)StartCoroutine(Request("password",new AccountRequest{password=current,newPassword=next},done));}
  public void Delete(string password,Action done){if(Logged&&!Busy)StartCoroutine(Request("delete",new AccountRequest{password=password},()=>{if(Message=="Готово"){PlayerPrefs.DeleteKey("LC.ProfileQueue."+Profile.uid);PlayerPrefs.DeleteKey("LC.ProfileDevice."+Profile.uid);token=null;Profile=null;active.Clear();}done?.Invoke();}));}
  public void DismissRecovery(){RecoveryCode=null;}

  // === Economy and friends through the game server (Supabase lives behind it) ===
  public CloudState Wallet {get;private set;}public CloudBoard Board {get;private set;}public CloudFriendsReply Friends {get;private set;}
  public string CloudMessage="";public bool CloudBusy {get;private set;}
  public bool CloudReady=>Logged&&Wallet!=null;
  public void CloudState(Action done=null)=>Cloud("state","{}",json=>{Wallet=JsonUtility.FromJson<CloudState>(json);},done,false);
  public void CloudMarket(Action done=null)=>Cloud("market","{}",json=>{Board=JsonUtility.FromJson<CloudBoard>(json);},done,false);
  public void CloudFriends(Action done=null)=>Cloud("friends","{}",json=>{Friends=JsonUtility.FromJson<CloudFriendsReply>(json);},done,false);
  public void CloudBuy(string code,Action done=null)=>Cloud("buy",CodeBody(code),json=>{Wallet=JsonUtility.FromJson<CloudState>(json);},done,true);
  public void CloudEquip(string code,Action done=null)=>Cloud("equip",CodeBody(code),json=>{Wallet=JsonUtility.FromJson<CloudState>(json);},done,true);
  public void CloudList(string code,long price,Action done=null)=>Cloud("list","{\"code\":\""+Esc(code)+"\",\"price\":"+price+"}",null,done,true);
  public void CloudCancel(string id,Action done=null)=>Cloud("cancel","{\"id\":\""+Esc(id)+"\"}",null,done,true);
  public void CloudPurchase(string id,Action done=null)=>Cloud("purchase","{\"id\":\""+Esc(id)+"\"}",json=>{Wallet=JsonUtility.FromJson<CloudState>(json);},done,true);
  public void CloudFriendRequest(string key,Action done=null)=>Cloud("friend/request","{\"key\":\""+Esc(key)+"\"}",null,done,true);
  /// <summary>Adds a friend by the nickname shown in the profile — no keys for players.</summary>
  public void CloudFriendRequestByName(string username,Action done=null,string message=null)
  {
   string clean=(username??"").Trim().TrimStart('@');
   string msg=string.IsNullOrWhiteSpace(message)?"":",\"message\":\""+Esc(message.Trim())+"\"";
   Cloud("friend/request","{\"username\":\""+Esc(clean)+"\""+msg+"}",null,done,true);
  }
  public void CloudFriendAccept(long id,Action done=null)=>Cloud("friend/accept","{\"id\":"+id+"}",null,done,true);
  public void CloudFriendDecline(long id,Action done=null)=>Cloud("friend/decline","{\"id\":"+id+"}",null,done,true);
  public void CloudFriendRemove(string key,Action done=null)=>Cloud("friend/remove","{\"key\":\""+Esc(key)+"\"}",null,done,true);
  static string CodeBody(string code){return "{\"code\":\""+Esc(code)+"\"}";}
  static string Esc(string value){return (value??"").Replace("\\","\\\\").Replace("\"","\\\"");}
  /// <summary>
  /// One call to /api/cloud/&lt;action&gt;. The player never sees Supabase: the game
  /// server holds the key and turns the account token into a player identity.
  /// After a successful change the wallet is reloaded so the UI shows the truth.
  /// </summary>
  void Cloud(string action,string body,Action<string> parse,Action done,bool refresh=true)
  {
   if(CloudBusy)return;
   if(!Logged){CloudMessage="Войдите в аккаунт, чтобы открыть кошелёк, магазин и друзей.";done?.Invoke();return;}
   StartCoroutine(CloudRoutine(action,body,parse,done,refresh));
  }
  IEnumerator CloudRoutine(string action,string body,Action<string> parse,Action done,bool refresh)
  {
   if(!CloudEndpointOk()){done?.Invoke();yield break;}
   CloudBusy=true;
   string error=null,json=null;
   yield return Post(action,body,text=>json=text,message=>error=message);
   if(error!=null)CloudMessage=error;
   else
   {
    CloudMessage="";
    if(parse!=null)try{parse(json);}catch{CloudMessage="Сервер вернул неожиданный ответ.";}
    if(refresh)
    {
     yield return Post("state","{}",text=>{try{Wallet=JsonUtility.FromJson<CloudState>(text);}catch{}},message=>CloudMessage=message);
     // A change in one domain reloads it: the next screen must not show stale data.
     if(action.StartsWith("friend"))
      yield return Post("friends","{}",text=>{try{Friends=JsonUtility.FromJson<CloudFriendsReply>(text);}catch{}},message=>CloudMessage=message);
     else if(action=="market"||action=="list"||action=="cancel"||action=="purchase")
      yield return Post("market","{}",text=>{try{Board=JsonUtility.FromJson<CloudBoard>(text);}catch{}},message=>CloudMessage=message);
    }
   }
   CloudBusy=false;done?.Invoke();
  }
  IEnumerator Post(string action,string body,Action<string> ok,Action<string> fail)
  {
   using(var request=new UnityWebRequest(Endpoint+"/api/cloud/"+action,"POST"))
   {
    request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));request.downloadHandler=new DownloadHandlerBuffer();
    request.SetRequestHeader("Content-Type","application/json");if(!string.IsNullOrEmpty(token))request.SetRequestHeader("Authorization","Bearer "+token);request.timeout=25;
    yield return request.SendWebRequest();
    string json=request.downloadHandler!=null?request.downloadHandler.text:"";
    if(request.result!=UnityWebRequest.Result.Success)
    {
     string message=null;try{message=JsonUtility.FromJson<CloudReply>(json)?.message;}catch{}
     fail(string.IsNullOrEmpty(message)?(request.responseCode==401?"Сессия истекла — войдите снова.":"Сервис экономики недоступен."):message);
     yield break;
    }
    ok(json);
   }
  }
  bool CloudEndpointOk()
  {
   Uri address;if(!Uri.TryCreate(Endpoint,UriKind.Absolute,out address)||(address.Scheme!="https"&&!address.IsLoopback)){CloudMessage="Для экономики нужен HTTPS-сервис.";return false;}
   return true;
  }
  /// <summary>Reloads wallet, market or friends without blocking the UI.</summary>
  public void CloudRefreshQuiet(){if(Logged&&!CloudBusy)StartCoroutine(CloudRoutine("state","{}",json=>{try{Wallet=JsonUtility.FromJson<CloudState>(json);}catch{}},null,false));}
  public void Logout(Action done){if(!Logged||Busy)return;SaveQueue();StartCoroutine(Request("logout",new AccountRequest(),()=>{token=null;Profile=null;RecoveryCode=null;active.Clear();Message="Вы вышли из аккаунта. Несинхронизированные события сохранены локально для следующего входа.";done?.Invoke();}));}
  public int Counter(string key){if(Profile?.stats==null)return 0;foreach(var s in Profile.stats)if(s.key==key)return s.value;return 0;}
  public void Record(string key,int value=1,bool maximum=false){if(!Logged||value<=0)return;if(!active.TryGetValue(key,out var e)){e=new ProfileEvent{key=key,mode=maximum?"max":"add"};active[key]=e;}e.value=maximum?Math.Max(e.value,value):Math.Min(1000000,e.value+value);}
  void SaveQueue(){if(!Logged)return;queue.active=new List<ProfileEvent>(active.Values).ToArray();PlayerPrefs.SetString("LC.ProfileQueue."+Profile.uid,JsonUtility.ToJson(queue));PlayerPrefs.Save();}
  ProfileEvent[] TakeEvents(){var list=new List<ProfileEvent>();foreach(var e in active.Values){list.Add(e);if(list.Count==128)break;}foreach(var e in list)active.Remove(e.key);return list.ToArray();}
  public void Sync(Action done=null)
  {
   if(!Logged||Busy){done?.Invoke();return;}if(queue.pending==null&&active.Count==0){done?.Invoke();return;}
   if(queue.pending==null){queue.pending=new ProfileBatch{batchId=Guid.NewGuid().ToString("N"),deviceId=device,sequence=queue.sequence+1,events=TakeEvents()};SaveQueue();}
   var batch=queue.pending;StartCoroutine(Request("progress",batch,()=>{if(Message=="Готово"){queue.sequence=batch.sequence;queue.pending=null;SaveQueue();}done?.Invoke();}));
  }
  IEnumerator Request(string path,object body,Action done)
  {
   Uri address;if(!Uri.TryCreate(Endpoint,UriKind.Absolute,out address)||(address.Scheme!="https"&&!address.IsLoopback)){Message="Для аккаунта нужен HTTPS-сервис.";done?.Invoke();yield break;}
   Busy=true;Message="Соединение с сервисом аккаунтов…";string json=JsonUtility.ToJson(body);
   // Passwords/tokens are memory-only. No auth query strings, PlayerPrefs credentials or secret logging.
   using(var request=new UnityWebRequest(Endpoint+"/api/accounts/"+path,"POST"))
   {request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/json");if(!string.IsNullOrEmpty(token))request.SetRequestHeader("Authorization","Bearer "+token);request.timeout=25;yield return request.SendWebRequest();AccountReply result=null;try{result=JsonUtility.FromJson<AccountReply>(request.downloadHandler.text);}catch{}if(path=="progress"&&result!=null&&result.batchId!=((ProfileBatch)body).batchId){result.ok=false;result.message="Некорректное подтверждение пакета.";}if(request.result==UnityWebRequest.Result.Success&&result!=null&&result.ok){if(!string.IsNullOrEmpty(result.token))token=result.token;if(result.profile!=null)Profile=result.profile;if(!string.IsNullOrEmpty(result.recoveryCode))RecoveryCode=result.recoveryCode;Message="Готово";}else Message=result?.message??"Сервис аккаунтов недоступен. Игра без входа остаётся доступной.";}
   Busy=false;nextSync=Time.unscaledTime+30;done?.Invoke();
  }
  void Update(){if(!Logged)return;if(Time.unscaledTime>nextSave){nextSave=Time.unscaledTime+10;SaveQueue();}if(Time.unscaledTime>nextSync&&!Busy)Sync();}
  void OnApplicationQuit(){SaveQueue();}
 }
}
