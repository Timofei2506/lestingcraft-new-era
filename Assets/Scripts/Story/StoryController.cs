using System;using UnityEngine;using UnityEngine.UI;using UnityEngine.InputSystem;using LestingCraft.Runtime;using LestingCraft.Networking;using LestingCraft.Gameplay;using LestingCraft.Presentation;
namespace LestingCraft.Story
{
 public sealed class StoryController:MonoBehaviour
 {
  public PrototypeGame Game;public StoryState State {get;private set;}public bool Locked=>State!=null&&StoryRules.Locked(State);public bool Enabled=>State!=null;
  public string ActiveObjective=>Enabled&&State!=null?StoryRules.Objective(State):"";
  // Scene-time impacts of the three knocks in the stage 1 witness scene (subtitle: "три удара" from t=9).
  static readonly float[] KnockTimes={9f,9.9f,10.8f};
  StoryContent content;StoryActors actors;Camera cameraView;Canvas canvas;Text quest,speaker,subtitle,hint,boss;Image panel,topBar,bottomBar;Button next,choiceA,choiceB;bool journalHidden;bool arenaPlaced;bool wasCinematic;bool readySent;int shownRevision=-1,oldStage=-1;long pauseAt;long remoteTime;float received;float attackTime=-10;Vector3 cameraHome;Quaternion cameraRotation;
  long Now=>Game.Online&&remoteTime>0?remoteTime+(long)((Time.unscaledTime-received)*1000):DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
  public void Initialize(PrototypeGame game,StoryState network=null,long serverNow=0)
  {
   Game=game;if((game.Session.Mode!=GameMode.Story&&!(game.Session.World.ContentVersion>=5&&!game.Session.Creative))||game.Session.World.ContentVersion<4||game.Session.World.Dimension!=LestingCraft.World.Dimension.Lestingia)return;
   content=JsonUtility.FromJson<StoryContent>(Resources.Load<TextAsset>("Story/ChapterOne").text);State=network??game.Session.Data.story??new StoryState();game.Session.Data.story=State;cameraView=game.Session.Player.Camera;cameraHome=cameraView.transform.localPosition;cameraRotation=cameraView.transform.localRotation;
   remoteTime=serverNow;received=Time.unscaledTime;actors=gameObject.AddComponent<StoryActors>();actors.Initialize(game.Session.World);BuildUI();TeleportCheckpoint();Apply(State,serverNow);
  }
  void TeleportCheckpoint(){int s=State.stage;Vector2 p=s<4?new Vector2(-32,-30):s==4&&!State.bridgeSolved?new Vector2(-8,7):s==7||s==8?new Vector2(48,-90):s<10||s==14?new Vector2(96,53):new Vector2(176,-96);Game.Session.Player.TeleportTo(new Vector3(p.x+.5f,StoryWorld.Floor(Game.Session.World.Seed)+1.05f,p.y+.5f),true);}
  public void Apply(StoryState state,long serverNow)
  {if(!Enabled||state==null||state.revision<State.revision)return;State=state;Game.Session.Data.story=state;if(serverNow>0){remoteTime=serverNow;received=Time.unscaledTime;}if(state.stage==14){Game.Session.Data.campaign.chapter=1;Game.Session.Data.campaign.unlockedMask=3;}}
  public void Action(string action,int arg=0)
  {
   if(!Enabled)return;if(Game.Online){Game.SendStory(new NetPacket{type="story_action",action=action,arg=arg,expectedRevision=State.revision,stage=State.stage,line=State.line,weapon=Game.Session.Inventory.Active.item});return;}
   var copy=State.Copy();if(StoryRules.Act(copy,action,arg,Now,content.Lines(State.stage).Length)){Apply(copy,Now);Game.Session.Checkpoint();}
  }
  public bool Attack(Ray ray,float maxDistance)
  {
   if(!Enabled||State.stage!=11)return false;var bounds=new Bounds(new Vector3(176.5f,StoryWorld.Floor(Game.Session.World.Seed)+5.5f,-111.5f),new Vector3(4.8f,7,3.5f));
   if(!bounds.IntersectRay(ray,out float distance)||distance>maxDistance)return false;
   if(VoxelRaycast.Trace(ray.origin,ray.direction,Mathf.Max(0,distance-.1f),Game.Session.World.GetBlock,out _))return false;
   if(Time.unscaledTime-attackTime<.56f)return true;attackTime=Time.unscaledTime;
   if(Game.Session.Inventory.Active.item!=103&&!Weapons.Has(Game.Session.Inventory.Active.item)){Game.Session.Notice="Возьмите боевое оружие";return true;}if(State.pylons!=7){Game.Session.Notice="Сначала зажгите все три жаровни";return true;}Action("hit");return true;
  }
  void Update()
  {
   if(!Enabled||!Game.Session)return;
   if(!Game.Online&&Game.Paused){if(canvas)canvas.enabled=false;if(pauseAt==0)pauseAt=Now;return;}if(pauseAt>0){long dt=Now-pauseAt;pauseAt=0;if(State.sceneAt>0)State.sceneAt+=dt;if(State.impactAt>0)State.impactAt+=dt;if(State.nextAttack>0)State.nextAttack+=dt;if(State.vulnerableUntil>0)State.vulnerableUntil+=dt;}
   if(!Game.WorldReady){if(quest)quest.text="Глава I · Подготовка мира…";return;}
   if(!readySent){readySent=true;if(Game.Online)Game.SendStory(new NetPacket{type="story_ready"});else Action("ready");}
   if(!Game.Online){var copy=State.Copy();if(StoryRules.Tick(copy,Now))Apply(copy,Now);TickOfflineBoss();}
   if(oldStage!=State.stage){oldStage=State.stage;if(State.stage==4||State.stage==5||State.stage==7||State.stage==10){Vector2 p=State.stage==4?new Vector2(-28,-25):State.stage==7?new Vector2(48,-90):new Vector2(96,53);Game.Session.Player.SetCheckpoint(new Vector3(p.x,StoryWorld.Floor(Game.Session.World.Seed)+1.05f,p.y));}Game.RefreshStoryControl();}
   // Story objectives live exclusively inside the ESC pause menu & Story Journal — never cluttering the main HUD!
   if(quest)quest.gameObject.SetActive(false);
   actors.Apply(State,Now);if(State.stage==10&&State.sceneAt>0&&!arenaPlaced){arenaPlaced=true;Game.Session.Player.TeleportTo(new Vector3(176,StoryWorld.Floor(Game.Session.World.Seed)+1.05f,-99),true);}GrantRewards();if(Game.Paused){canvas.enabled=false;return;}canvas.enabled=true;RefreshUI();Cinematic();
   if(!Locked&&!Game.Paused&&!Game.ChatOpen&&Keyboard.current!=null&&Keyboard.current.fKey.wasPressedThisFrame)
   {if(Interaction(out string action,out int arg,out _))Action(action,arg);}
  }
  void TickOfflineBoss()
  {
   if(State.stage!=11)return;var s=State.Copy();long now=Now;var p=Game.Session.Player.transform.position;
   if(s.impactAt>0&&now>=s.impactAt){if(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(s.attackX,s.attackZ))<(s.bossPhase==0?2.5f:3.4f))Game.Session.Damage(s.bossPhase==0?14:20);s.impactAt=0;}
   if(now>=s.nextAttack&&Vector2.Distance(new Vector2(p.x,p.z),new Vector2(176,-112))<22){s.attackSerial++;s.attackX=p.x;s.attackZ=p.z;s.impactAt=now+1200;s.nextAttack=now+(s.bossPhase==0?4400:3200);s.attack=s.bossPhase+1;}
   State=s;Game.Session.Data.story=s;
  }
  void GrantRewards()
  {
   var data=Game.Session.Data.player;int pending=State.rewards&~data.claimedStoryRewards;if(pending==0)return;var copy=Game.Session.Inventory.Clone();
   bool ok=true;if((pending&1)!=0)ok&=copy.Add(120,3)&&copy.Add(121,2)&&copy.Add(122,3);
   if((pending&2)!=0)ok&=copy.Add(111,1)&&copy.Add(123,1)&&copy.Add(148,2);
   if((pending&4)!=0)ok&=copy.Add(103,1)&&copy.Add(104,1)&&copy.Add(148,2)&&copy.Add(111,State.choice==2?5:3);
   if(!ok){Game.Session.Notice="Награда ждёт: освободите место в инвентаре";return;}
   if((pending&8)!=0)Game.Session.Progress.Add("bosses");data.inventory=copy;data.claimedStoryRewards|=pending;copy.revision++;Game.Session.Checkpoint();
  }
  bool Interaction(out string action,out int arg,out string label)
  {
   action="";arg=0;label="";Vector2 p=new Vector2(Game.Session.Player.transform.position.x,Game.Session.Player.transform.position.z);Vector2 at=Vector2.zero;
   if(State.stage==2){if((State.searched&1)==0&&Vector2.Distance(p,new Vector2(-35,-29))<3.5f){action="crate";at=new Vector2(-35,-29);label="Обыскать треснувший ящик";}else if((State.searched&2)==0&&Vector2.Distance(p,new Vector2(-28,-33))<3.5f){action="satchel";at=new Vector2(-28,-33);label="Обыскать дорожную сумку";}else if(State.searched==3){action="witness";at=new Vector2(-34,-34);label="Подойти к раненым";}}
   else if(State.stage==4&&!State.bridgeSolved){Vector2[] controls={new Vector2(-3,8),new Vector2(-1,12),new Vector2(4,6)};for(int i=0;i<3;i++)if(Vector2.Distance(p,controls[i])<3.5f){action="weight";arg=i;at=controls[i];label="Противовес "+new[]{2,3,5}[i]+" · на замке высечено 7";break;}}
   else if(State.stage==4){action="village";at=new Vector2(96,50);label="Говорить с Верой";}
   else if(State.stage==6){action="mill";at=new Vector2(48,-90);label="Говорить с Мирой";}
   else if(State.stage==8){for(int i=0;i<3;i++)if(Vector2.Distance(p,new Vector2(42+i*6,-87))<3.5f){action="rune";arg=i;at=new Vector2(42+i*6,-87);label=new[]{"Печать ворона","Печать узла","Печать солнца"}[i];break;}}
   else if(State.stage==9&&State.line<0){action="smith";at=new Vector2(108,60);label="Передать сердечник Илье";}
   else if(State.stage==10&&State.sceneAt==0){action="altar";at=new Vector2(176,-112);label="Разбудить корневую печать";}
   else if(State.stage==11){Vector2[] points={new Vector2(168,-112),new Vector2(184,-112),new Vector2(176,-104)};for(int i=0;i<3;i++)if((State.pylons&(1<<i))==0&&Vector2.Distance(p,points[i])<3.5f){action="pylon";arg=i;at=points[i];label="Зажечь жаровню";break;}}
   if(action==""||Vector2.Distance(p,at)>3.5f)return false;
   Vector3 end=new Vector3(at.x+.5f,StoryWorld.Floor(Game.Session.World.Seed)+(action=="rune"?3.2f:action=="pylon"?4.2f:action=="altar"?2.4f:1.4f),at.y+.5f),start=cameraView.transform.position,dir=end-start;
   if(VoxelRaycast.Trace(start,dir.normalized,Mathf.Max(0,dir.magnitude-.9f),(x,y,z)=>{ushort b=Game.Session.World.GetBlock(x,y,z);return b==5||b==44||b==15?(ushort)0:b;},out _))return false;return true;
  }
  void Cinematic()
  {
   bool cinematic=State.stage==1||State.stage==12||State.stage==10&&State.sceneAt>0;
   topBar.gameObject.SetActive(cinematic);bottomBar.gameObject.SetActive(cinematic);
   if(StoryRules.Dialogue(State.stage)&&State.line>=0){if(!wasCinematic)cameraRotation=cameraView.transform.localRotation;wasCinematic=true;DialogueCamera();return;}
   if(!cinematic){if(wasCinematic){cameraView.transform.localPosition=cameraHome;cameraView.transform.localRotation=cameraRotation;wasCinematic=false;}if(cameraView.transform.parent==Game.Session.Player.transform&&Locked){cameraView.transform.localPosition=cameraHome;cameraView.transform.localRotation=cameraRotation;}return;}
   if(!wasCinematic)cameraRotation=cameraView.transform.localRotation;wasCinematic=true;
   float t=(Now-State.sceneAt)/1000f,y=StoryWorld.Floor(Game.Session.World.Seed);Vector3 from,to,look;
   if(State.stage==1){from=new Vector3(-32.3f,y+1.18f,-30.6f);to=new Vector3(-31.5f,y+2.62f,-29.5f);look=StoryActors.WitnessPosition(true,Game.Session.World.Seed)+new Vector3(-.3f,.15f,1);float a=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-2)/9));cameraView.transform.position=Vector3.Lerp(from,to,a)+Vector3.up*Mathf.Sin(t*19)*.012f*(1-a)+CutscenePath.Shake(t,KnockTimes,.03f);cameraView.transform.rotation=Quaternion.LookRotation(look-cameraView.transform.position)*Quaternion.Euler(0,0,Mathf.Lerp(-27,0,a));subtitle.text=t<3?"Сначала — пыль. Потом — чьё-то дыхание.":t<9?"Балка поддаётся. Вы выбираетесь из-под обломков.":"Где-то под полом раздаются три удара.";}
   else if(State.stage==10){cameraView.transform.position=CutscenePath.Sample(t,(0f,new Vector3(166,y+4,-100)),(6f,new Vector3(171,y+7.4f,-98)),(12f,new Vector3(172,y+7,-106)));cameraView.transform.LookAt(new Vector3(176,y+5.5f,-112));subtitle.text=t<5?"Мира: Он держал дверь. Кто научил его открывать её?":"Страж корней · Тот, кто помнит чужие голоса";}
   else {cameraView.transform.position=CutscenePath.Sample(t,(0f,new Vector3(171,y+4,-106)),(8f,new Vector3(172.6f,y+3.1f,-108.4f)),(16f,new Vector3(174,y+2,-104)));cameraView.transform.LookAt(new Vector3(176,y+1,-103));subtitle.text=t<5?"Мира: Не подходите! Я ещё держу...":t<10?"Звук колокола обрывается. Корни замирают.":"Илья: Мира? Мы же договорились.";}
   panel.gameObject.SetActive(true);speaker.text=content.title;
  }
  void DialogueCamera()
  {
   var lines=content.Lines(State.stage);if(State.line>=lines.Length)return;string who=lines[State.line].speaker;if(State.stage==5&&State.line==4&&State.choice==1)who="Вера";float y=StoryWorld.Floor(Game.Session.World.Seed);Vector3 at,from;
   if(State.stage==3){at=StoryActors.WitnessPosition(who!="Марта",Game.Session.World.Seed)+(who=="Марта"?new Vector3(-.5f,.1f,-1.2f):new Vector3(-.35f,.1f,1.2f));from=new Vector3(-32.9f,y+3.3f,-29.6f);if(who=="Вы"){at=new Vector3(-32,y+1.3f,-34);from=new Vector3(-31.5f,y+3,-29.2f);}}
   else if(State.stage==7){at=new Vector3(48,y+2.55f,-90);from=new Vector3(45.7f,y+2.7f,-86.2f);if(who=="Голос из шахты"){from=new Vector3(48,y+2.7f,-88);at=new Vector3(48,y+.5f,-96);}}
   else if(State.stage==9){at=who=="Мира"?new Vector3(106,y+2.55f,59):who=="Вера"?new Vector3(109,y+2.55f,60):new Vector3(108,y+2.55f,60);from=new Vector3(110.7f,y+2.75f,63.1f);}
   else if(State.stage==13){at=who=="Вера"?new Vector3(179,y+2.6f,-100):new Vector3(174,y+2.6f,-101);from=new Vector3(176,y+2.8f,-97);if(who=="Вы"){at=new Vector3(176,y+1.3f,-103);from=new Vector3(172.8f,y+3,-99.2f);}}
   else if(who=="Илья"){at=new Vector3(108,y+2.55f,60);from=new Vector3(110.7f,y+2.75f,63.1f);}
   else{at=who=="Мира"?new Vector3(94,y+2.55f,52):new Vector3(96,y+2.55f,50);from=new Vector3(93,y+2.8f,55);}
   from+=Vector3.right*Mathf.Sin(Time.unscaledTime*.35f)*.04f;cameraView.transform.position=from;cameraView.transform.LookAt(at);
  }
  void RefreshUI()
  {
   quest.text=StoryRules.Objective(State);bool dialog=StoryRules.Dialogue(State.stage)&&State.line>=0;bool cinematic=State.stage==1||State.stage==12||State.stage==10&&State.sceneAt>0;panel.gameObject.SetActive(dialog||cinematic);hint.text=!Locked&&Interaction(out _,out _,out string label)?"F · "+label:"";
   boss.text=State.stage==11?"СТРАЖ КОРНЕЙ  "+State.bossHealth+" / "+State.bossMax+(State.pylons==7?"   СЕРДЦЕ ОТКРЫТО":"   ПЕЧАТЬ ЗАКРЫТА"):"";
   if(dialog){var lines=content.Lines(State.stage);if(State.line<lines.Length){var line=lines[State.line];speaker.text=line.speaker;subtitle.text=line.text;if(State.stage==5&&State.line==4){speaker.text=State.choice==1?"Вера":"Мира";subtitle.text=State.choice==1?"На карте её пометки... до самого святилища. Значит, она знала, куда идёт.":"Вы попытались помочь им обоим. Спасибо, что не прошли мимо.";}bool choice=line.choice;next.gameObject.SetActive(!choice);choiceA.gameObject.SetActive(choice&&(!Game.Online||Game.IsHost));choiceB.gameObject.SetActive(choice&&(!Game.Online||Game.IsHost));next.GetComponentInChildren<Text>().text=Game.Online?"Продолжить · готово "+State.acked+" / "+Mathf.Max(1,State.waiting):"Продолжить →";if(choice&&Game.Online&&!Game.IsHost)hint.text="Общее решение принимает хозяин комнаты";}}
   else {next.gameObject.SetActive(false);choiceA.gameObject.SetActive(false);choiceB.gameObject.SetActive(false);}
   if(shownRevision!=State.revision){shownRevision=State.revision;Game.RefreshStoryControl();}
  }
  Text Text(Transform parent,string name,int size,Vector2 pos,Vector2 extent,TextAnchor anchor=TextAnchor.UpperLeft)
  {var o=new GameObject(name,typeof(RectTransform),typeof(Text));o.transform.SetParent(parent,false);var rt=o.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(.5f,0);rt.anchoredPosition=pos;rt.sizeDelta=extent;var text=o.GetComponent<Text>();text.font=Resources.Load<Font>("UI/Manrope")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=size;text.color=new Color(.94f,.94f,.88f);text.supportRichText=false;text.alignment=anchor;text.raycastTarget=false;return text;}
  Image Image(Transform parent,string name,Vector2 pos,Vector2 extent,Color color){var o=new GameObject(name,typeof(RectTransform),typeof(Image));o.transform.SetParent(parent,false);var r=o.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,0);r.anchoredPosition=pos;r.sizeDelta=extent;var image=o.GetComponent<Image>();image.color=color;return image;}
  Button Button(string text,float x,Action action){var image=Image(panel.transform,text,new Vector2(x,30),new Vector2(280,44),new Color(.25f,.29f,.25f));var b=image.gameObject.AddComponent<Button>();b.onClick.AddListener(()=>action());var label=Text(image.transform,text,17,new Vector2(0,22),new Vector2(270,38),TextAnchor.MiddleCenter);label.text=text;return b;}
  void BuildUI()
  {
   var root=new GameObject("Shared chapter interface",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=110;var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1600,900);
   quest=Text(root.transform,"Chapter objective",18,new Vector2(435,224),new Vector2(530,90));hint=Text(root.transform,"Interaction",21,new Vector2(0,282),new Vector2(850,54),TextAnchor.MiddleCenter);boss=Text(root.transform,"Guardian health",24,new Vector2(0,820),new Vector2(1250,46),TextAnchor.MiddleCenter);
   panel=Image(root.transform,"Dialogue",new Vector2(0,139),new Vector2(1360,238),new Color(.025f,.04f,.045f,.96f));speaker=Text(panel.transform,"Speaker",21,new Vector2(0,202),new Vector2(1250,35));subtitle=Text(panel.transform,"Subtitle",23,new Vector2(0,130),new Vector2(1250,110));
   next=Button("Продолжить →",470,()=>Action("ack"));choiceA=Button(content.choiceA,150,()=>Action("choice",1));choiceB=Button(content.choiceB,470,()=>Action("choice",2));
   panel.gameObject.SetActive(false);next.gameObject.SetActive(false);choiceA.gameObject.SetActive(false);choiceB.gameObject.SetActive(false);
   topBar=Image(root.transform,"Cinematic top",new Vector2(0,862),new Vector2(1700,76),Color.black);bottomBar=Image(root.transform,"Cinematic bottom",new Vector2(0,25),new Vector2(1700,50),Color.black);topBar.gameObject.SetActive(false);bottomBar.gameObject.SetActive(false);
  }
  void OnDestroy(){if(cameraView&&Game&&Game.Session){cameraView.transform.localPosition=cameraHome;cameraView.transform.localRotation=Quaternion.identity;}}
 }
}
