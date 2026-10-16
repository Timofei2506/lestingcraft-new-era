using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using LestingCraft.World;
using LestingCraft.Networking;
using LestingCraft.Presentation;
using LestingCraft.Gameplay;
using LestingCraft.Story;

namespace LestingCraft.Runtime
{
    [Serializable] class NetworkConfig {public string endpoint;}
    [Serializable] class CreateRoomRequest {public string ownerKey;public int seed,dimension=0,version=1,rulesVersion=GameRules.Version,mode;}
    public sealed class PrototypeGame : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){if(!FindAnyObjectByType<PrototypeGame>())new GameObject("LestingCraft").AddComponent<PrototypeGame>();}
        readonly string[] dimensions={"Лестингия","Пеплоземье","Небокрай","Склеп Эха","Разлом Хаоса"};
        readonly Dictionary<string,GameObject> avatars=new Dictionary<string,GameObject>();
        readonly Dictionary<string,Vector3> targets=new Dictionary<string,Vector3>();
        string endpoint="",code="",playerName="Player",seedText="2506",status="",selfId="",lastError="";
        int dimension,selectedMode;SessionSave resumeSave;
        public StoryController Story {get;private set;}public bool StoryLocked=>Story&&Story.Locked;
        public bool InLobby {get;private set;}public bool IsHost {get;private set;}public string HostId {get;private set;}public int LobbyRevision {get;private set;}
        public NetPlayer[] LobbyPlayers {get;private set;}=Array.Empty<NetPlayer>();NetPacket pendingWelcome;
        public void SendStory(NetPacket p){if(client&&client.Connected)client.Send(p);}
        public void StartLobby(){SendStory(new NetPacket{type="lobby_start"});}
        public void RefreshStoryControl(){if(world&&!paused&&!ChatOpen)SetCursor(!StoryLocked);}
        string PlayerKey(){string key=PlayerPrefs.GetString("LC.PlayerKey","");if(key.Length!=32){key=Guid.NewGuid().ToString("N");PlayerPrefs.SetString("LC.PlayerKey",key);PlayerPrefs.Save();}return key;}
        void ApplyLobby(NetPacket p){HostId=p.hostId;IsHost=HostId==selfId;LobbyPlayers=p.players??Array.Empty<NetPlayer>();LobbyRevision++;}
        public AccountClient Accounts {get;private set;}
        public SurvivalSession Session {get;private set;}
        public int SelectedMode {get=>selectedMode;set=>selectedMode=Mathf.Clamp(value,0,3);}
        public bool HasSave=>LocalSave.Exists;
        public bool ChatOpen {get;private set;}
        readonly Queue<string> chatLog=new Queue<string>();public int ChatRevision {get;private set;}public float ChatLastTime {get;private set;}float chatSentAt=-10;
        public string ChatLog=>string.Join("\n",chatLog);
        public void OpenChat()
        {
            if(!world||paused)return;
            ChatOpen=true;SetCursor(false);ui.SetChatOpen(true);
        }
        public void CloseChat(){ChatOpen=false;if(ui)ui.SetChatOpen(false);SetCursor(world&&!paused);}
        public void SubmitChat()
        {
            string text=ui.ChatText.Trim();
            if(text.StartsWith("/")){ExecuteCommand(text);CloseChat();return;}
            if(!online&&text.Length>0){AddChat(new NetPacket{name=playerName,message=text});CloseChat();return;}
            if(text.Length>0&&online&&client&&client.Connected)
            {
                if(Time.unscaledTime-chatSentAt<1.2f){Session.Notice="Подождите секунду перед следующим сообщением";return;}
                Session.Progress.Add("chat");chatSentAt=Time.unscaledTime;client.Send(new NetPacket{type="chat",message=text});
            }
            CloseChat();
        }
        void ExecuteCommand(string text)
        {
            if(!Session)return;var result=ChatCommands.Execute(text,Session.Creative,!Online,Session.Inventory,world.Seed,player.transform.position,PlayerCount);
            if(result.Action=="clear"){chatLog.Clear();ChatRevision++;}
            else if(result.Action=="save")Session.Checkpoint();
            else if(result.Action=="time")Session.Data.clock=result.Value;
            AddChat(new NetPacket{name="Система",message=result.Message});
        }
        void AddChat(NetPacket packet)
        {
            string name=(packet.name??"Player").Replace("\n"," ").Replace("\r"," ");
            string text=(packet.message??"").Replace("\n"," ").Replace("\r"," ");
            if(name.Length>24)name=name.Substring(0,24);if(text.Length>240)text=text.Substring(0,240);
            chatLog.Enqueue(name+": "+text);while(chatLog.Count>6)chatLog.Dequeue();ChatRevision++;ChatLastTime=Time.unscaledTime;
        }
        public bool InventoryOpen=>ui&&ui.InventoryOpen;
        bool paused=true,busy,online;
        VoxelWorld world; FirstPersonController player; RoomClient client;
        long revision; float lastPosition,previousYaw,lastHeartbeat;Vector3 previousPosition;bool sentPosition;int previousEquipment;
        Material avatarMaterial;
        GameUI ui;bool leaving,saveAcknowledged;
        public bool InWorld=>world;
        public bool Paused=>paused;
        public bool Online=>online;public string LocalPeerId=>selfId;
        public void SendAnimalHit(string animal,int weapon,int requestId){if(client&&client.Connected)client.Send(new NetPacket{type="animal_hit",animal=animal,weapon=weapon,requestId=requestId});}
        public bool IsConnecting=>busy;
        public string LastError=>lastError;
        public string ServerLabel=>endpoint.Replace("https://","").Replace("http://","");
        public string WorldSeed=>seedText;
        public string PlayerName {get=>playerName;set=>playerName=value;}
        public string RoomCode {get=>code;set=>code=value.Trim();}
        public string WorldLabel=>world?dimensions[(int)world.Dimension]+"  /  SEED "+world.Seed:"";
        public int PlayerCount=>avatars.Count+1;
        public int ChunkCount=>world?world.LoadedChunks:0;
        public bool WorldReady=>world&&world.Ready;
        public ushort SelectedBlock=>Session?Items.Get(Session.Inventory.Active.item).Block:(ushort)1;
        public string ModeName=>GameRules.ModeNames[Session?(int)Session.Mode:selectedMode];
        public void PlayOffline()
        {if(busy||world)return;int seed=Array.IndexOf(Environment.GetCommandLineArgs(),"-lc-smoke-test")>=0?2506:Guid.NewGuid().GetHashCode();seedText=seed.ToString();StartWorld(seed,0,false);}
        public void ContinueOffline()
        {
            if(busy||world)return;resumeSave=LocalSave.Load();if(resumeSave==null){Fail("Сохранение повреждено");return;}
            if(resumeSave.campaign.runEnded){resumeSave=null;Fail("Хардкорный забег завершён. Создайте новый.");return;}
            selectedMode=resumeSave.mode;StartWorld(resumeSave.seed,0,false);
        }
        public void ToggleInventory()
        {
            if(!Session||StoryLocked||Session.Vitals.dead||Session.Data.campaign.runEnded)return;
            if(ui.InventoryOpen){Resume();return;}paused=true;SetCursor(false);ui.ShowInventory();
        }
        public void OpenStation(int id,Vector3Int cell)
        {
            if(!Session||!Session.StationNearby(id,cell))return;
            if(id==17){var f=Session.Inventory.furnace;f.x=cell.x;f.y=cell.y;f.z=cell.z;f.realm=(int)Session.World.Dimension;}
            paused=true;SetCursor(false);ui.ShowInventory(id,cell);
        }
        public void OpenDeathScreen(){paused=true;SetCursor(false);ui.ShowDeathScreen();}
        public void SendSave(PlayerSave save,bool force=false){if(client&&client.Connected)client.Send(new NetPacket{type="state",save=save,force=force});}
        public void CreateRoom(){if(!busy)StartCoroutine(CreateRoomRoutine());}
        public void JoinRoom(){if(!busy)JoinRoomInternal();}
        public void Resume(){if(!world||Session&&(Session.Vitals.dead||Session.Data.campaign.runEnded))return;paused=false;SetCursor(true);ui.ShowHUD();}
        public void ReturnToMenu(){if(!leaving)StartCoroutine(LeaveAfterCheckpoint());}
        IEnumerator LeaveAfterCheckpoint()
        {
            leaving=true;paused=true;SetCursor(false);
            if(online&&client&&client.Connected&&Session&&!Session.ServerRunEnded)
            {
                saveAcknowledged=false;SendSave(Session.Data.player,true);float deadline=Time.unscaledTime+2;
                while(!saveAcknowledged&&Time.unscaledTime<deadline)yield return null;
                if(!saveAcknowledged)Debug.LogWarning("Exit checkpoint not acknowledged; last periodic save is retained");
            }
            Leave();leaving=false;ui.ShowHome();
        }
        void Fail(string message){status=message;lastError=message;busy=false;}
        void Awake()
        {
            Debug.Log("[Presentation] Bootstrap started");
            var config=Resources.Load<TextAsset>("NetworkConfig");if(config)endpoint=JsonUtility.FromJson<NetworkConfig>(config.text).endpoint;
            Accounts=gameObject.AddComponent<AccountClient>();Accounts.Initialize(endpoint);
            Application.targetFrameRate=60;
            VisualSettings.Load();
            gameObject.AddComponent<VisualCapture>();
            var pipeline=Resources.Load<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Generated/HighDefinition");
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            Debug.Log("[Presentation] Pipeline configured");
            var camera=new GameObject("Menu Camera").AddComponent<Camera>();camera.backgroundColor=new Color(.055f,.09f,.15f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();
            SetCursor(false);
            ui=new GameObject("Game Interface").AddComponent<GameUI>();
            try{ui.Initialize(this);}
            catch(Exception error){Debug.LogException(error);StartupErrorPanel.Show();return;}
            VisualSettings.Current.Apply();
            Debug.Log("[Presentation] Interface ready");
        }
        void SetCursor(bool locked){if(StoryLocked)locked=false;Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!locked;if(player)player.Active=locked;}
        public void TogglePause(){if(!world||Session&&(Session.Vitals.dead||Session.Data.campaign.runEnded))return;paused=!paused;SetCursor(!paused);}
        public void RequestEdit(Vector3Int cell,ushort block)
        {
            if(Story&&Story.Enabled&&StoryWorld.Protected(cell.x,cell.z)){Session.RejectEdit("Сюжетная постройка защищена; стройте за её пределами");return;}
            if(!world||!world.CanAcceptEdit(cell)){Fail("Достигнут лимит изменений этого среза");if(Session)Session.RejectEdit(lastError);return;}
            if(online)
            {if(client&&client.Connected)client.Send(new NetPacket {type="edit",x=cell.x,y=cell.y,z=cell.z,block=block});else {Fail("Нет соединения: изменения заблокированы");if(Session)Session.RejectEdit(lastError);}}
            else {world.Edit(cell.x,cell.y,cell.z,block);if(Session)Session.ConfirmEdit(cell);}
        }
        void StartWorld(int seed,int realm,bool multiplayer,PlayerSave savedPlayer=null,double clock=420,int contentVersion=6,StoryState story=null,long serverNow=0)
        {
            world=new GameObject("World").AddComponent<VoxelWorld>();online=multiplayer;world.Initialize(seed,(Dimension)realm,multiplayer,resumeSave?.contentVersion??contentVersion);
            var camera=Camera.main;camera.backgroundColor=realm==1?new Color(.19f,.08f,.07f):realm==3||realm==4?new Color(.075f,.045f,.14f):new Color(.42f,.66f,.78f);
            RenderSettings.fogColor=camera.backgroundColor;
            var obj=new GameObject("Player");obj.transform.position=FindSpawn(seed,(Dimension)realm,world.ContentVersion);
            var controller=obj.AddComponent<CharacterController>();PlayerCapsule.Configure(controller);
            player=obj.AddComponent<FirstPersonController>();player.World=world;player.Game=this;player.Camera=camera;
            camera.transform.SetParent(obj.transform,false);camera.transform.localPosition=new Vector3(0,PlayerCapsule.EyeHeight,0);camera.nearClipPlane=.05f;camera.farClipPlane=160;
            dimension=realm;seedText=seed.ToString();camera.farClipPlane=256;
            WorldAtmosphere.Configure(camera,realm,resumeSave?.clock??clock);
            obj.AddComponent<FirstPersonHands>().Initialize(player,camera);
            world.Player=obj.transform;paused=false;SetCursor(true);busy=false;status="Генерируется мир…";lastError="";
            var sessionObject=new GameObject("Survival and weather");sessionObject.transform.SetParent(world.transform,false);
            Session=sessionObject.AddComponent<SurvivalSession>();Session.Initialize(this,world,player,selectedMode,savedPlayer,resumeSave,clock);resumeSave=null;Story=sessionObject.AddComponent<StoryController>();Story.Initialize(this,story,serverNow);obj.AddComponent<LocalPlayerAppearance>().Initialize(this);
            if(Session.Vitals.dead||Session.Data.campaign.runEnded)OpenDeathScreen();else ui.ShowHUD();
        }
        IEnumerator CreateRoomRoutine()
        {
            if(!ValidateEndpoint())yield break;
            int seed=Guid.NewGuid().GetHashCode();seedText=seed.ToString();
            busy=true;lastError="";status="Создаю комнату…";
            using(var request=new UnityWebRequest(endpoint.TrimEnd('/')+"/api/rooms","POST"))
            {
                var body=System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new CreateRoomRequest {ownerKey=PlayerKey(),seed=seed,dimension=0,mode=selectedMode}));
                request.uploadHandler=new UploadHandlerRaw(body);request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/json");request.timeout=20;
                yield return request.SendWebRequest();busy=false;
                if(request.result!=UnityWebRequest.Result.Success){Fail("Комната не создана: "+request.responseCode+" "+request.error);yield break;}
                var result=JsonUtility.FromJson<NetPacket>(request.downloadHandler.text);code=result.code;JoinRoomInternal();
            }
        }
        bool ValidateEndpoint()
        {
            if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)||(uri.Scheme!="https"&&uri.Scheme!="http")){Fail("Укажи https:// адрес сервера");return false;}
#if UNITY_WEBGL && !UNITY_EDITOR
            if(uri.Scheme!="https"){Fail("Для браузерного билда нужен HTTPS-сервер");return false;}
#endif
            return true;
        }
        void JoinRoomInternal()
        {
            if(world)return;
            if(!ValidateEndpoint())return;
            if(code.Trim().Length!=32){Fail("Код комнаты — 32 символа");return;}
            busy=true;lastError="";status="Подключение…";revision=0;sentPosition=false;
            if(client)Destroy(client.gameObject);
            client=new GameObject("LestingCraftNetwork").AddComponent<RoomClient>();
            client.Message+=HandleMessage;
            client.Status+=s=>{if(s=="@closed"||s!="@open"){if(s!="@open"){Fail(s=="@closed"?"Соединение закрыто — выйди в меню и подключись снова":s);}}};
            string key=PlayerKey();
            client.Connect(endpoint,code,string.IsNullOrWhiteSpace(playerName)?"Player":playerName,key);
        }
        void HandleMessage(NetPacket packet)
        {
            if(packet==null)return;if(Session&&packet.clock>0)Session.Data.clock=packet.clock;
            switch(packet.type)
            {
                case "welcome":
                    if(packet.version!=TerrainGenerator.Version){Fail("Несовместимая версия генератора");Destroy(client.gameObject);busy=false;return;}
                    if(!GameRules.AcceptsRoom(packet.rulesVersion,packet.serverRulesVersion)){Fail("Несовместимые правила комнаты. Обновите сервер; для нового содержимого нужна новая комната");Destroy(client.gameObject);return;}
                    if(packet.contentVersion>6||packet.contentVersion<0||packet.rulesVersion>=9&&packet.contentVersion!=6||packet.rulesVersion==8&&packet.contentVersion!=5||packet.rulesVersion==7&&packet.contentVersion!=4||packet.rulesVersion==6&&packet.contentVersion!=3||packet.rulesVersion==5&&packet.contentVersion!=2){Fail("Несовместимая версия содержимого мира");Destroy(client.gameObject);return;}
                    selectedMode=packet.mode;selfId=packet.id;IsHost=packet.host;pendingWelcome=packet;
                    if(packet.lobby&&!packet.started){online=true;busy=false;InLobby=true;ui.ShowLobby();break;}BeginNetworkWorld(packet);break;
                case "lobby":ApplyLobby(packet);break;
                case "lobby_start":ApplyLobby(packet);if(InLobby&&pendingWelcome!=null){InLobby=false;pendingWelcome.story=packet.story;pendingWelcome.serverNow=packet.serverNow;pendingWelcome.clock=packet.clock>0?packet.clock:420;BeginNetworkWorld(pendingWelcome);}break;
                case "story":if(Story){Story.Apply(packet.story,packet.serverNow);Session.Animals.Apply(packet);if(packet.campaign!=null)Session.Data.campaign=packet.campaign;}break;
                case "edit":
                    if(world&&packet.rev>revision){revision=packet.rev;world.Edit((int)packet.x,(int)packet.y,(int)packet.z,(ushort)packet.block);if(Session)Session.Living.Apply(packet);if(packet.id==selfId&&Session)Session.ConfirmEdit(new Vector3Int((int)packet.x,(int)packet.y,(int)packet.z));}break;
                case "living_saved":case "living":if(Session)Session.Living.Apply(packet);break;
                case "animal_hit":if(Session){Session.Animals.Apply(packet);}break;
                case "chat":AddChat(packet);break;
                case "player":if(Session)Session.Living.Apply(packet);if(Story&&packet.story!=null)Story.Apply(packet.story,packet.serverNow);if(Session)Session.Animals.Apply(packet);SetAvatar(new NetPlayer{id=packet.id,name=packet.name,x=packet.x,y=packet.y,z=packet.z,yaw=packet.yaw,offhand=packet.offhand,weapon=packet.weapon,equipment=packet.equipment});break;
                case "leave":if(avatars.TryGetValue(packet.id,out var avatar))Destroy(avatar);avatars.Remove(packet.id);targets.Remove(packet.id);break;
                case "state_saved":if(packet.id==selfId)saveAcknowledged=true;if(Session&&packet.campaign!=null)Session.Data.campaign=packet.campaign;break;
                case "run_ended":if(packet.id==selfId)saveAcknowledged=true;if(Session){Session.ServerRunEnded=true;Session.Data.campaign.runEnded=true;Session.Notice="Хардкор завершён: погиб участник команды";OpenDeathScreen();}break;
                case "error":if(Session)Session.Living.Apply(packet);Fail(packet.message);if(Session){if(string.IsNullOrEmpty(packet.code)||packet.code=="edit")Session.RejectEdit(packet.message);else{Session.Notice=packet.message;if(packet.code=="animal_hit")Session.Animals.Reject(packet.requestId);}}break;
            }
        }
        void BeginNetworkWorld(NetPacket packet)
        {
            InLobby=false;StartWorld(packet.seed,packet.dimension,true,packet.save,packet.clock,packet.contentVersion<=0?1:packet.contentVersion,packet.story,packet.serverNow);revision=packet.rev;Session.Animals.Apply(packet);
            if(packet.campaign!=null)Session.Data.campaign=packet.campaign;if(packet.runEnded){Session.ServerRunEnded=true;Session.Data.campaign.runEnded=true;OpenDeathScreen();}
            if(packet.edits!=null)foreach(var edit in packet.edits)world.Edit(edit.x,edit.y,edit.z,(ushort)edit.block);Session.Living.Apply(packet);if(packet.players!=null)foreach(var peer in packet.players)SetAvatar(peer);status="Комната подключена • "+code;
        }
        void SetAvatar(NetPlayer peer)
        {
            if(peer.id==selfId||!world)return;
            if(!avatars.TryGetValue(peer.id,out var avatar))
            {
                avatar=new GameObject("Peer "+peer.name);avatar.AddComponent<CharacterVisual>().Build();var body=avatar.AddComponent<CapsuleCollider>();body.height=PlayerCapsule.Height;body.radius=PlayerCapsule.Radius;body.center=Vector3.up*(PlayerCapsule.Height*.5f);
                avatars.Add(peer.id,avatar);avatar.transform.position=new Vector3(peer.x,peer.y-PlayerCapsule.EyeHeight,peer.z);
                avatar.AddComponent<PlayerNameplate>().Initialize(peer.name,world);
            }
            avatar.GetComponent<CharacterVisual>().SetEquipment(peer.equipment);avatar.GetComponent<CharacterVisual>().SetOffhand(peer.offhand);
            var weaponView=avatar.GetComponent<RemoteWeaponVisual>();if(!weaponView)weaponView=avatar.AddComponent<RemoteWeaponVisual>();weaponView.Show(peer.weapon);
            targets[peer.id]=new Vector3(peer.x,peer.y-PlayerCapsule.EyeHeight,peer.z);avatar.transform.rotation=Quaternion.Euler(0,peer.yaw,0);
        }
        void Update()
        {
            foreach(var peerAvatar in avatars.Values)if(peerAvatar)peerAvatar.SetActive(!StoryLocked);
            if(world&&world.Ready&&status=="Генерируется мир…")status="Мир готов — нажми «Продолжить»";
            foreach(var pair in avatars)if(pair.Value&&targets.TryGetValue(pair.Key,out var target))
            {pair.Value.transform.position=Vector3.Lerp(pair.Value.transform.position,target,Time.deltaTime*12);}
            if(online&&client&&client.Connected&&player&&world.Ready&&Time.unscaledTime-lastPosition>=.2f)
            {
                var pos=player.transform.position+Vector3.up*PlayerCapsule.EyeHeight;int activeItem=Session.Inventory.Active.count>0?Session.Inventory.Active.item:0;int weapon=Weapons.Has(activeItem)||Items.IsPick(activeItem)||activeItem==102||activeItem==103?activeItem:0;var equipment=new int[8];int equipmentHash=weapon*31+17+(Session.Inventory.offhand.count>0?148:0);
                for(int i=0;i<8;i++){var slot=Session.Inventory.equipment[i];equipment[i]=slot.count>0?slot.item:0;equipmentHash=unchecked(equipmentHash*31+equipment[i]);}
                if(!sentPosition||Time.unscaledTime-lastHeartbeat>=.5f||equipmentHash!=previousEquipment||(pos-previousPosition).sqrMagnitude>.0009f||Mathf.Abs(Mathf.DeltaAngle(previousYaw,player.transform.eulerAngles.y))>1f)
                {client.Send(new NetPacket{type="position",x=pos.x,y=pos.y,z=pos.z,yaw=player.transform.eulerAngles.y,weapon=weapon,equipment=equipment,offhand=Session.Inventory.offhand.count>0?148:0});lastHeartbeat=Time.unscaledTime;previousEquipment=equipmentHash;previousPosition=pos;previousYaw=player.transform.eulerAngles.y;sentPosition=true;}
                lastPosition=Time.unscaledTime;
            }
        }
        void Leave()
        {
            Story=null;InLobby=false;IsHost=false;pendingWelcome=null;LobbyPlayers=Array.Empty<NetPlayer>();
            ChatOpen=false;chatLog.Clear();ChatRevision++;
            if(Session&&!online)Session.Checkpoint();Session=null;
            if(client)Destroy(client.gameObject);
            foreach(var a in avatars.Values)if(a)Destroy(a);avatars.Clear();targets.Clear();
            var camera=Camera.main;if(camera){camera.transform.SetParent(null);camera.clearFlags=CameraClearFlags.SolidColor;}
            if(player){var hands=player.GetComponent<FirstPersonHands>();if(hands)hands.Release();Destroy(player.gameObject);}if(world)Destroy(world.gameObject);
            client=null;world=null;player=null;online=false;busy=false;paused=true;SetCursor(false);status="";lastError="";WorldAtmosphere.Clear();
        }
        static Vector3 FindSpawn(int seed,Dimension realm,int contentVersion)
        {
            for(int r=0;r<8;r++)for(int z=-r;z<=r;z++)for(int x=-r;x<=r;x++)
            {
                int h=TerrainGenerator.Height(x,z,seed,realm);
                if(BlockRegistry.IsSolid(TerrainGenerator.SampleContent(x,h,z,seed,realm,contentVersion))&&TerrainGenerator.SampleContent(x,h+1,z,seed,realm,contentVersion)==0&&TerrainGenerator.SampleContent(x,h+2,z,seed,realm,contentVersion)==0)return new Vector3(x+.5f,h+1.2f,z+.5f);
            }
            return new Vector3(.5f,TerrainGenerator.Height(0,0,seed,realm)+10,.5f);
        }
        void OnApplicationQuit(){if(Session)Session.Checkpoint();}
        void OnDestroy(){if(avatarMaterial)Destroy(avatarMaterial);}
    }
}
