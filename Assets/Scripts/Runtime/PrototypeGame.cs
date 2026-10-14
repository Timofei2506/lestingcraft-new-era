using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using LestingCraft.World;
using LestingCraft.Networking;
using LestingCraft.Presentation;
using LestingCraft.Gameplay;

namespace LestingCraft.Runtime
{
    [Serializable] class NetworkConfig {public string endpoint;}
    [Serializable] class CreateRoomRequest {public int seed,dimension=0,version=1,rulesVersion=GameRules.Version,mode;}
    public sealed class PrototypeGame : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){if(!FindAnyObjectByType<PrototypeGame>())new GameObject("LestingCraft").AddComponent<PrototypeGame>();}
        readonly string[] dimensions={"Лестингия","Пеплоземье","Небокрай","Склеп Эха","Разлом Хаоса"};
        readonly Dictionary<string,GameObject> avatars=new Dictionary<string,GameObject>();
        readonly Dictionary<string,Vector3> targets=new Dictionary<string,Vector3>();
        string endpoint="",code="",playerName="Player",seedText="2506",status="",selfId="",lastError="";
        int dimension,selectedMode;SessionSave resumeSave;
        public SurvivalSession Session {get;private set;}
        public int SelectedMode {get=>selectedMode;set=>selectedMode=Mathf.Clamp(value,0,3);}
        public bool HasSave=>LocalSave.Exists;
        public bool InventoryOpen=>ui&&ui.InventoryOpen;
        bool paused=true,busy,online;
        VoxelWorld world; FirstPersonController player; RoomClient client;
        long revision; float lastPosition,previousYaw;Vector3 previousPosition;bool sentPosition;
        Material avatarMaterial;
        GameUI ui;bool leaving,saveAcknowledged;
        public bool InWorld=>world;
        public bool Paused=>paused;
        public bool Online=>online;
        public bool IsConnecting=>busy;
        public string LastError=>lastError;
        public string ServerLabel=>endpoint.Replace("https://","").Replace("http://","");
        public string WorldSeed {get=>seedText;set=>seedText=value;}
        public string PlayerName {get=>playerName;set=>playerName=value;}
        public string RoomCode {get=>code;set=>code=value.Trim();}
        public string WorldLabel=>world?dimensions[(int)world.Dimension]+"  /  SEED "+world.Seed:"";
        public int PlayerCount=>avatars.Count+1;
        public int ChunkCount=>world?world.LoadedChunks:0;
        public bool WorldReady=>world&&world.Ready;
        public ushort SelectedBlock=>Session?Items.Get(Session.Inventory.Active.item).Block:(ushort)1;
        public string ModeName=>GameRules.ModeNames[Session?(int)Session.Mode:selectedMode];
        public void PlayOffline()
        {if(busy||world)return;if(int.TryParse(seedText,out var seed))StartWorld(seed,0,false);else Fail("Seed должен быть целым числом");}
        public void ContinueOffline()
        {
            if(busy||world)return;resumeSave=LocalSave.Load();if(resumeSave==null){Fail("Сохранение повреждено");return;}
            if(resumeSave.campaign.runEnded){resumeSave=null;Fail("Хардкорный забег завершён. Создайте новый.");return;}
            selectedMode=resumeSave.mode;StartWorld(resumeSave.seed,0,false);
        }
        public void ToggleInventory()
        {
            if(!Session||Session.Vitals.dead||Session.Data.campaign.runEnded)return;
            if(ui.InventoryOpen){Resume();return;}paused=true;SetCursor(false);ui.ShowInventory();
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
            Application.targetFrameRate=60;
            VisualSettings.Load();
            var pipeline=Resources.Load<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Generated/HighDefinition");
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            Debug.Log("[Presentation] Pipeline configured");
            var camera=new GameObject("Menu Camera").AddComponent<Camera>();camera.backgroundColor=new Color(.055f,.09f,.15f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();
            SetCursor(false);
            ui=new GameObject("Game Interface").AddComponent<GameUI>();ui.Initialize(this);
            VisualSettings.Current.Apply();
            Debug.Log("[Presentation] Interface ready");
        }
        void SetCursor(bool locked){Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!locked;if(player)player.Active=locked;}
        public void TogglePause(){if(!world||Session&&(Session.Vitals.dead||Session.Data.campaign.runEnded))return;paused=!paused;SetCursor(!paused);}
        public void RequestEdit(Vector3Int cell,ushort block)
        {
            if(!world||!world.CanAcceptEdit(cell)){Fail("Достигнут лимит изменений этого среза");if(Session)Session.RejectEdit(lastError);return;}
            if(online)
            {if(client&&client.Connected)client.Send(new NetPacket {type="edit",x=cell.x,y=cell.y,z=cell.z,block=block});else {Fail("Нет соединения: изменения заблокированы");if(Session)Session.RejectEdit(lastError);}}
            else {world.Edit(cell.x,cell.y,cell.z,block);if(Session)Session.ConfirmEdit(cell);}
        }
        void StartWorld(int seed,int realm,bool multiplayer,PlayerSave savedPlayer=null,double clock=420)
        {
            world=new GameObject("World").AddComponent<VoxelWorld>();online=multiplayer;world.Initialize(seed,(Dimension)realm,multiplayer);
            var camera=Camera.main;camera.backgroundColor=realm==1?new Color(.19f,.08f,.07f):realm==3||realm==4?new Color(.075f,.045f,.14f):new Color(.42f,.66f,.78f);
            RenderSettings.fogColor=camera.backgroundColor;
            var obj=new GameObject("Player");obj.transform.position=FindSpawn(seed,(Dimension)realm);
            var controller=obj.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=.3f;controller.center=new Vector3(0,.9f,0);controller.stepOffset=.45f;
            player=obj.AddComponent<FirstPersonController>();player.World=world;player.Game=this;player.Camera=camera;
            camera.transform.SetParent(obj.transform,false);camera.transform.localPosition=new Vector3(0,1.62f,0);camera.nearClipPlane=.05f;camera.farClipPlane=160;
            dimension=realm;seedText=seed.ToString();camera.farClipPlane=256;
            WorldAtmosphere.Configure(camera,realm);
            world.Player=obj.transform;paused=false;SetCursor(true);busy=false;status="Генерируется мир…";lastError="";
            var sessionObject=new GameObject("Survival and weather");sessionObject.transform.SetParent(world.transform,false);
            Session=sessionObject.AddComponent<SurvivalSession>();Session.Initialize(this,world,player,selectedMode,savedPlayer,resumeSave,clock);resumeSave=null;
            if(Session.Vitals.dead||Session.Data.campaign.runEnded)OpenDeathScreen();else ui.ShowHUD();
        }
        IEnumerator CreateRoomRoutine()
        {
            if(!ValidateEndpoint())yield break;
            if(!int.TryParse(seedText,out var seed)){Fail("Seed должен быть целым числом");yield break;}
            busy=true;lastError="";status="Создаю комнату…";
            using(var request=new UnityWebRequest(endpoint.TrimEnd('/')+"/api/rooms","POST"))
            {
                var body=System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new CreateRoomRequest {seed=seed,dimension=0,mode=selectedMode}));
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
            string key=PlayerPrefs.GetString("LC.PlayerKey","");if(key.Length!=32){key=Guid.NewGuid().ToString("N");PlayerPrefs.SetString("LC.PlayerKey",key);PlayerPrefs.Save();}
            client.Connect(endpoint,code,string.IsNullOrWhiteSpace(playerName)?"Player":playerName,key);
        }
        void HandleMessage(NetPacket packet)
        {
            if(packet==null)return;
            switch(packet.type)
            {
                case "welcome":
                    if(packet.version!=TerrainGenerator.Version){Fail("Несовместимая версия генератора");Destroy(client.gameObject);busy=false;return;}
                    if(packet.rulesVersion!=GameRules.Version){Fail("Сервер ещё не обновлён для режимов выживания");Destroy(client.gameObject);return;}
                    selectedMode=packet.mode;selfId=packet.id;StartWorld(packet.seed,packet.dimension,true,packet.save,packet.clock);revision=packet.rev;
                    if(packet.campaign!=null)Session.Data.campaign=packet.campaign;
                    if(packet.runEnded){Session.ServerRunEnded=true;Session.Data.campaign.runEnded=true;OpenDeathScreen();}
                    if(packet.edits!=null)foreach(var edit in packet.edits)world.Edit(edit.x,edit.y,edit.z,(ushort)edit.block);
                    if(packet.players!=null)foreach(var peer in packet.players)SetAvatar(peer);
                    status="Комната подключена • "+code;break;
                case "edit":
                    if(world&&packet.rev>revision){revision=packet.rev;world.Edit((int)packet.x,(int)packet.y,(int)packet.z,(ushort)packet.block);if(packet.id==selfId&&Session)Session.ConfirmEdit(new Vector3Int((int)packet.x,(int)packet.y,(int)packet.z));}break;
                case "player":SetAvatar(new NetPlayer{id=packet.id,name=packet.name,x=packet.x,y=packet.y,z=packet.z,yaw=packet.yaw});break;
                case "leave":if(avatars.TryGetValue(packet.id,out var avatar))Destroy(avatar);avatars.Remove(packet.id);targets.Remove(packet.id);break;
                case "state_saved":if(packet.id==selfId)saveAcknowledged=true;if(Session&&packet.campaign!=null)Session.Data.campaign=packet.campaign;break;
                case "run_ended":if(packet.id==selfId)saveAcknowledged=true;if(Session){Session.ServerRunEnded=true;Session.Data.campaign.runEnded=true;Session.Notice="Хардкор завершён: погиб участник команды";OpenDeathScreen();}break;
                case "error":Fail(packet.message);if(Session)Session.RejectEdit(packet.message);break;
            }
        }
        void SetAvatar(NetPlayer peer)
        {
            if(peer.id==selfId||!world)return;
            if(!avatars.TryGetValue(peer.id,out var avatar))
            {
                avatar=new GameObject("Peer "+peer.name);avatar.AddComponent<CharacterVisual>().Build();
                avatars.Add(peer.id,avatar);avatar.transform.position=new Vector3(peer.x,peer.y-1.62f,peer.z);
                var text=new GameObject("Name").AddComponent<TextMesh>();text.transform.SetParent(avatar.transform,false);text.transform.localPosition=new Vector3(0,2.1f,0);text.text=peer.name;text.characterSize=.13f;text.anchor=TextAnchor.MiddleCenter;
            }
            targets[peer.id]=new Vector3(peer.x,peer.y-1.62f,peer.z);avatar.transform.rotation=Quaternion.Euler(0,peer.yaw,0);
        }
        void Update()
        {
            if(world&&world.Ready&&status=="Генерируется мир…")status="Мир готов — нажми «Продолжить»";
            foreach(var pair in avatars)if(pair.Value&&targets.TryGetValue(pair.Key,out var target))
            {pair.Value.transform.position=Vector3.Lerp(pair.Value.transform.position,target,Time.deltaTime*12);var label=pair.Value.GetComponentInChildren<TextMesh>();if(label&&Camera.main)label.transform.rotation=Camera.main.transform.rotation;}
            if(online&&client&&client.Connected&&player&&world.Ready&&Time.unscaledTime-lastPosition>=.2f)
            {
                var pos=player.Camera.transform.position;
                if(!sentPosition||(pos-previousPosition).sqrMagnitude>.0009f||Mathf.Abs(Mathf.DeltaAngle(previousYaw,player.transform.eulerAngles.y))>1f)
                {client.Send(new NetPacket{type="position",x=pos.x,y=pos.y,z=pos.z,yaw=player.transform.eulerAngles.y});previousPosition=pos;previousYaw=player.transform.eulerAngles.y;sentPosition=true;}
                lastPosition=Time.unscaledTime;
            }
        }
        void Leave()
        {
            if(Session&&!online)Session.Checkpoint();Session=null;
            if(client)Destroy(client.gameObject);
            foreach(var a in avatars.Values)if(a)Destroy(a);avatars.Clear();targets.Clear();
            var camera=Camera.main;if(camera){camera.transform.SetParent(null);camera.clearFlags=CameraClearFlags.SolidColor;}
            if(player)Destroy(player.gameObject);if(world)Destroy(world.gameObject);
            client=null;world=null;player=null;online=false;busy=false;paused=true;SetCursor(false);status="";lastError="";WorldAtmosphere.Clear();
        }
        static Vector3 FindSpawn(int seed,Dimension realm)
        {
            for(int r=0;r<8;r++)for(int z=-r;z<=r;z++)for(int x=-r;x<=r;x++)
            {
                int h=TerrainGenerator.Height(x,z,seed,realm);
                if(TerrainGenerator.Sample(x,h+1,z,seed,realm)==0&&TerrainGenerator.Sample(x,h+2,z,seed,realm)==0)return new Vector3(x+.5f,h+1.2f,z+.5f);
            }
            return new Vector3(.5f,TerrainGenerator.Height(0,0,seed,realm)+10,.5f);
        }
        void OnApplicationQuit(){if(Session)Session.Checkpoint();}
        void OnDestroy(){if(avatarMaterial)Destroy(avatarMaterial);}
    }
}
