using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using LestingCraft.World;
using LestingCraft.Networking;
using LestingCraft.Presentation;

namespace LestingCraft.Runtime
{
    [Serializable] class NetworkConfig {public string endpoint;}
    [Serializable] class CreateRoomRequest {public int seed,dimension,version=1;}
    public sealed class PrototypeGame : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){if(!FindAnyObjectByType<PrototypeGame>())new GameObject("LestingCraft").AddComponent<PrototypeGame>();}
        readonly string[] dimensions={"Лестингия","Пеплоземье","Небокрай","Склеп Эха","Разлом Хаоса"};
        readonly Dictionary<string,GameObject> avatars=new Dictionary<string,GameObject>();
        readonly Dictionary<string,Vector3> targets=new Dictionary<string,Vector3>();
        string endpoint="",code="",playerName="Player",seedText="2506",status="",selfId="",lastError="";
        int dimension;
        bool paused=true,busy,online;
        VoxelWorld world; FirstPersonController player; RoomClient client;
        long revision; float lastPosition,previousYaw;Vector3 previousPosition;bool sentPosition;
        Material avatarMaterial;
        GameUI ui;
        public bool InWorld=>world;
        public bool Paused=>paused;
        public bool Online=>online;
        public bool IsConnecting=>busy;
        public string LastError=>lastError;
        public string ServerLabel=>endpoint.Replace("https://","").Replace("http://","");
        public int SelectedRealm {get=>dimension;set=>dimension=Mathf.Clamp(value,0,4);}
        public string WorldSeed {get=>seedText;set=>seedText=value;}
        public string PlayerName {get=>playerName;set=>playerName=value;}
        public string RoomCode {get=>code;set=>code=value.Trim();}
        public string WorldLabel=>world?dimensions[(int)world.Dimension]+"  /  SEED "+world.Seed:"";
        public int PlayerCount=>avatars.Count+1;
        public int ChunkCount=>world?world.LoadedChunks:0;
        public ushort SelectedBlock=>player?player.Selected:(ushort)1;
        public void PlayOffline()
        {if(busy||world)return;if(int.TryParse(seedText,out var seed))StartWorld(seed,dimension,false);else Fail("Seed должен быть целым числом");}
        public void CreateRoom(){if(!busy)StartCoroutine(CreateRoomRoutine());}
        public void JoinRoom(){if(!busy)JoinRoomInternal();}
        public void Resume(){if(!world)return;paused=false;SetCursor(true);ui.ShowHUD();}
        public void ReturnToMenu(){Leave();ui.ShowHome();}
        void Fail(string message){status=message;lastError=message;busy=false;}
        void Awake()
        {
            var config=Resources.Load<TextAsset>("NetworkConfig");if(config)endpoint=JsonUtility.FromJson<NetworkConfig>(config.text).endpoint;
            Application.targetFrameRate=60;
            VisualSettings.Load();
            var pipeline=Resources.Load<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Generated/HighDefinition");
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            var camera=new GameObject("Menu Camera").AddComponent<Camera>();camera.backgroundColor=new Color(.055f,.09f,.15f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();
            SetCursor(false);
            ui=new GameObject("Game Interface").AddComponent<GameUI>();ui.Initialize(this);
            VisualSettings.Current.Apply();
        }
        void SetCursor(bool locked){Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!locked;if(player)player.Active=locked;}
        public void TogglePause(){if(!world)return;paused=!paused;SetCursor(!paused);}
        public void RequestEdit(Vector3Int cell,ushort block)
        {
            if(online)
            {if(client&&client.Connected)client.Send(new NetPacket {type="edit",x=cell.x,y=cell.y,z=cell.z,block=block});else Fail("Нет соединения: изменения заблокированы");}
            else world.Edit(cell.x,cell.y,cell.z,block);
        }
        void StartWorld(int seed,int realm,bool multiplayer)
        {
            world=new GameObject("World").AddComponent<VoxelWorld>();online=multiplayer;world.Initialize(seed,(Dimension)realm,multiplayer);
            var camera=Camera.main;camera.backgroundColor=realm==1?new Color(.19f,.08f,.07f):realm==3||realm==4?new Color(.075f,.045f,.14f):new Color(.42f,.66f,.78f);
            RenderSettings.fogColor=camera.backgroundColor;
            var obj=new GameObject("Player");obj.transform.position=new Vector3(.5f,TerrainGenerator.Height(0,0,seed,(Dimension)realm)+10,.5f);
            var controller=obj.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=.3f;controller.center=new Vector3(0,.9f,0);controller.stepOffset=.45f;
            player=obj.AddComponent<FirstPersonController>();player.World=world;player.Game=this;player.Camera=camera;
            camera.transform.SetParent(obj.transform,false);camera.transform.localPosition=new Vector3(0,1.62f,0);camera.nearClipPlane=.05f;camera.farClipPlane=160;
            dimension=realm;seedText=seed.ToString();camera.farClipPlane=256;
            WorldAtmosphere.Configure(camera,realm);
            world.Player=obj.transform;paused=false;SetCursor(true);busy=false;status="Генерируется мир…";lastError="";
            ui.ShowHUD();
        }
        IEnumerator CreateRoomRoutine()
        {
            if(!ValidateEndpoint())yield break;
            if(!int.TryParse(seedText,out var seed)){Fail("Seed должен быть целым числом");yield break;}
            busy=true;lastError="";status="Создаю комнату…";
            using(var request=new UnityWebRequest(endpoint.TrimEnd('/')+"/api/rooms","POST"))
            {
                var body=System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new CreateRoomRequest {seed=seed,dimension=dimension}));
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
            client.Connect(endpoint,code,string.IsNullOrWhiteSpace(playerName)?"Player":playerName);
        }
        void HandleMessage(NetPacket packet)
        {
            if(packet==null)return;
            switch(packet.type)
            {
                case "welcome":
                    if(packet.version!=TerrainGenerator.Version){Fail("Несовместимая версия генератора");Destroy(client.gameObject);busy=false;return;}
                    selfId=packet.id;StartWorld(packet.seed,packet.dimension,true);revision=packet.rev;
                    if(packet.edits!=null)foreach(var edit in packet.edits)world.Edit(edit.x,edit.y,edit.z,(ushort)edit.block);
                    if(packet.players!=null)foreach(var peer in packet.players)SetAvatar(peer);
                    status="Комната подключена • "+code;break;
                case "edit":
                    if(world&&packet.rev>revision){revision=packet.rev;world.Edit((int)packet.x,(int)packet.y,(int)packet.z,(ushort)packet.block);}break;
                case "player":SetAvatar(new NetPlayer{id=packet.id,name=packet.name,x=packet.x,y=packet.y,z=packet.z,yaw=packet.yaw});break;
                case "leave":if(avatars.TryGetValue(packet.id,out var avatar))Destroy(avatar);avatars.Remove(packet.id);targets.Remove(packet.id);break;
                case "error":Fail(packet.message);break;
            }
        }
        void SetAvatar(NetPlayer peer)
        {
            if(peer.id==selfId||!world)return;
            if(!avatars.TryGetValue(peer.id,out var avatar))
            {
                avatar=GameObject.CreatePrimitive(PrimitiveType.Capsule);avatar.name="Peer "+peer.name;Destroy(avatar.GetComponent<Collider>());
                if(!avatarMaterial){avatarMaterial=new Material(Resources.Load<Material>("Generated/Avatar"));}
                avatar.GetComponent<MeshRenderer>().sharedMaterial=avatarMaterial;avatar.transform.localScale=new Vector3(.6f,.9f,.6f);
                avatars.Add(peer.id,avatar);avatar.transform.position=new Vector3(peer.x,peer.y-.72f,peer.z);
                var text=new GameObject("Name").AddComponent<TextMesh>();text.transform.SetParent(avatar.transform,false);text.transform.localPosition=new Vector3(0,1.7f,0);text.text=peer.name;text.characterSize=.13f;text.anchor=TextAnchor.MiddleCenter;
            }
            targets[peer.id]=new Vector3(peer.x,peer.y-.72f,peer.z);avatar.transform.rotation=Quaternion.Euler(0,peer.yaw,0);
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
            if(client)Destroy(client.gameObject);
            foreach(var a in avatars.Values)if(a)Destroy(a);avatars.Clear();targets.Clear();
            var camera=Camera.main;if(camera){camera.transform.SetParent(null);camera.clearFlags=CameraClearFlags.SolidColor;}
            if(player)Destroy(player.gameObject);if(world)Destroy(world.gameObject);
            client=null;world=null;player=null;online=false;busy=false;paused=true;SetCursor(false);status="";lastError="";WorldAtmosphere.Clear();
        }
        void OnDestroy(){if(avatarMaterial)Destroy(avatarMaterial);}
    }
}
