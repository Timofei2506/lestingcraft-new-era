using System;using System.Collections;using System.Collections.Generic;using System.Text;using UnityEngine;using UnityEngine.Networking;
namespace LestingCraft.Gameplay
{
 [Serializable] public sealed class AchievementDefinition {public string id,name,category,counter,description,title;public int target,xp;}
 [Serializable] public sealed class AchievementCatalog {public AchievementDefinition[] achievements;}
 [Serializable] public sealed class ProfileStat {public string key;public int value;}
 [Serializable] public sealed class AccountProfile {public string uid,username,title,titleName,integrity;public int xp,level,revision;public string[] achievements;public ProfileStat[] stats;}
 [Serializable] public sealed class AccountReply {public bool ok;public string token,recoveryCode,message,batchId;public AccountProfile profile;}
 [Serializable] public sealed class AccountRequest {public string username,password,newPassword,recoveryCode,title;}
 [Serializable] public sealed class ProfileEvent {public string key,mode;public int value;}
 [Serializable] public sealed class ProfileBatch {public string batchId,deviceId;public int sequence;public ProfileEvent[] events;}
 [Serializable] public sealed class ProfileQueue {public int sequence;public ProfileEvent[] active;public ProfileBatch pending;}
 public sealed class AccountClient:MonoBehaviour
 {
  public string Endpoint {get;private set;}public AccountProfile Profile {get;private set;}public string Message="Войдите до экспедиции, чтобы сохранять достижения в аккаунт.";public string RecoveryCode {get;private set;}public bool Busy {get;private set;}public bool Logged=>Profile!=null;public AchievementDefinition[] Achievements {get;private set;}
  string token,device;float nextSync,nextSave;ProfileQueue queue=new ProfileQueue();readonly Dictionary<string,ProfileEvent> active=new Dictionary<string,ProfileEvent>();
  public void Initialize(string endpoint){Endpoint=(endpoint??"").TrimEnd('/');Achievements=JsonUtility.FromJson<AchievementCatalog>(Resources.Load<TextAsset>("Achievements").text).achievements;}
  public void Authenticate(string action,string username,string password,string recovery,Action done){if(Busy)return;SaveQueue();StartCoroutine(Request(action,new AccountRequest{username=username,password=password,recoveryCode=recovery},()=>{if(Logged){active.Clear();string key="LC.ProfileQueue."+Profile.uid;try{queue=JsonUtility.FromJson<ProfileQueue>(PlayerPrefs.GetString(key,""))??new ProfileQueue();}catch{queue=new ProfileQueue();}if(queue.active!=null)foreach(var e in queue.active)active[e.key]=e;device=PlayerPrefs.GetString("LC.ProfileDevice."+Profile.uid,"");if(device.Length!=32){device=Guid.NewGuid().ToString("N");PlayerPrefs.SetString("LC.ProfileDevice."+Profile.uid,device);}SaveQueue();}done?.Invoke();}));}
  public void ChangeTitle(string id,Action done){if(Logged&&!Busy)StartCoroutine(Request("profile",new AccountRequest{title=id},done));}
  public void ChangePassword(string current,string next,Action done){if(Logged&&!Busy)StartCoroutine(Request("password",new AccountRequest{password=current,newPassword=next},done));}
  public void Delete(string password,Action done){if(Logged&&!Busy)StartCoroutine(Request("delete",new AccountRequest{password=password},()=>{if(Message=="Готово"){PlayerPrefs.DeleteKey("LC.ProfileQueue."+Profile.uid);PlayerPrefs.DeleteKey("LC.ProfileDevice."+Profile.uid);token=null;Profile=null;active.Clear();}done?.Invoke();}));}
  public void DismissRecovery(){RecoveryCode=null;}
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
