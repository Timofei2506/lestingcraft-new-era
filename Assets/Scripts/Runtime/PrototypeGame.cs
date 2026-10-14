using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using LestingCraft.World;
using LestingCraft.Networking;

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
        string endpoint="",code="",playerName="Player",seedText="2506",status="Прототип • генерация v1",selfId="";
        int dimension;
        bool paused=true,busy,online;
        VoxelWorld world; FirstPersonController player; RoomClient client;
        long revision; float lastPosition;Vector3 previousPosition;bool sentPosition;
        Material avatarMaterial;
        GUIStyle title,label,button;
        void Awake()
        {
            var config=Resources.Load<TextAsset>("NetworkConfig");if(config)endpoint=JsonUtility.FromJson<NetworkConfig>(config.text).endpoint;
            Application.targetFrameRate=60;
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline=null;
            QualitySettings.renderPipeline=null;
            var camera=new GameObject("Menu Camera").AddComponent<Camera>();camera.backgroundColor=new Color(.055f,.09f,.15f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();
            SetCursor(false);
        }
        void SetCursor(bool locked){Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!locked;if(player)player.Active=locked;}
        public void TogglePause(){paused=!paused;SetCursor(!paused);}
        public void RequestEdit(Vector3Int cell,ushort block)
        {
            if(online)
            {if(client&&client.Connected)client.Send(new NetPacket {type="edit",x=cell.x,y=cell.y,z=cell.z,block=block});else status="Нет соединения: изменения заблокированы";}
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
            world.Player=obj.transform;paused=true;SetCursor(false);busy=false;status="Генерируется мир…";
        }
        IEnumerator CreateRoom()
        {
            if(!ValidateEndpoint())yield break;
            if(!int.TryParse(seedText,out var seed)){status="Seed должен быть целым числом";yield break;}
            busy=true;status="Создаю комнату…";
            using(var request=new UnityWebRequest(endpoint.TrimEnd('/')+"/api/rooms","POST"))
            {
                var body=System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new CreateRoomRequest {seed=seed,dimension=dimension}));
                request.uploadHandler=new UploadHandlerRaw(body);request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/json");request.timeout=20;
                yield return request.SendWebRequest();busy=false;
                if(request.result!=UnityWebRequest.Result.Success){status="Комната не создана: "+request.responseCode+" "+request.error;yield break;}
                var result=JsonUtility.FromJson<NetPacket>(request.downloadHandler.text);code=result.code;JoinRoom();
            }
        }
        bool ValidateEndpoint()
        {
            if(!Uri.TryCreate(endpoint,out var uri)||(uri.Scheme!="https"&&uri.Scheme!="http")){status="Укажи https:// адрес сервера";return false;}
#if UNITY_WEBGL && !UNITY_EDITOR
            if(uri.Scheme!="https"){status="Для браузерного билда нужен HTTPS-сервер";return false;}
#endif
            return true;
        }
        void JoinRoom()
        {
            if(!ValidateEndpoint())return;
            if(code.Trim().Length!=32){status="Код комнаты — 32 символа";return;}
            busy=true;status="Подключение…";revision=0;sentPosition=false;
            client=new GameObject("LestingCraftNetwork").AddComponent<RoomClient>();
            client.Message+=HandleMessage;
            client.Status+=s=>{if(s=="@closed"||s!="@open"){if(s!="@open"){busy=false;status=s=="@closed"?"Соединение закрыто — выйди в меню и подключись снова":s;}}};
            client.Connect(endpoint,code,string.IsNullOrWhiteSpace(playerName)?"Player":playerName);
        }
        void HandleMessage(NetPacket packet)
        {
            if(packet==null)return;
            switch(packet.type)
            {
                case "welcome":
                    if(packet.version!=TerrainGenerator.Version){status="Несовместимая версия генератора";Destroy(client.gameObject);busy=false;return;}
                    selfId=packet.id;StartWorld(packet.seed,packet.dimension,true);revision=packet.rev;
                    if(packet.edits!=null)foreach(var edit in packet.edits)world.Edit(edit.x,edit.y,edit.z,(ushort)edit.block);
                    if(packet.players!=null)foreach(var peer in packet.players)SetAvatar(peer);
                    status="Комната подключена • "+code;break;
                case "edit":
                    if(world&&packet.rev>revision){revision=packet.rev;world.Edit((int)packet.x,(int)packet.y,(int)packet.z,(ushort)packet.block);}break;
                case "player":SetAvatar(new NetPlayer{id=packet.id,name=packet.name,x=packet.x,y=packet.y,z=packet.z,yaw=packet.yaw});break;
                case "leave":if(avatars.TryGetValue(packet.id,out var avatar))Destroy(avatar);avatars.Remove(packet.id);targets.Remove(packet.id);break;
                case "error":status=packet.message;break;
            }
        }
        void SetAvatar(NetPlayer peer)
        {
            if(peer.id==selfId||!world)return;
            if(!avatars.TryGetValue(peer.id,out var avatar))
            {
                avatar=GameObject.CreatePrimitive(PrimitiveType.Capsule);avatar.name="Peer "+peer.name;Destroy(avatar.GetComponent<Collider>());
                if(!avatarMaterial){avatarMaterial=new Material(Resources.Load<Shader>("Voxel"));avatarMaterial.SetColor("_Tint",new Color(.4f,.9f,.85f));}
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
                if(!sentPosition||(pos-previousPosition).sqrMagnitude>.0009f)
                {client.Send(new NetPacket{type="position",x=pos.x,y=pos.y,z=pos.z,yaw=player.transform.eulerAngles.y});previousPosition=pos;sentPosition=true;}
                lastPosition=Time.unscaledTime;
            }
        }
        void Leave()
        {
            if(client)Destroy(client.gameObject);
            foreach(var a in avatars.Values)if(a)Destroy(a);avatars.Clear();targets.Clear();
            var camera=Camera.main;if(camera)camera.transform.SetParent(null);
            if(player)Destroy(player.gameObject);if(world)Destroy(world.gameObject);
            client=null;world=null;player=null;online=false;busy=false;paused=true;SetCursor(false);status="Отключено. Офлайн-изменения не сохраняются.";
        }
        void OnGUI()
        {
            if(title==null){title=new GUIStyle(GUI.skin.label){fontSize=28,fontStyle=FontStyle.Bold};label=new GUIStyle(GUI.skin.label){fontSize=15,wordWrap=true};button=new GUIStyle(GUI.skin.button){fontSize=16};}
            if(world&&!paused)
            {
                GUI.Label(new Rect(Screen.width/2-8,Screen.height/2-15,20,30),"+",title);
                GUI.Box(new Rect(16,16,410,100),"");GUI.Label(new Rect(28,24,386,90),$"LESTINGCRAFT • {dimensions[(int)world.Dimension]}\nSeed {world.Seed}  |  Чанки {world.LoadedChunks}  |  {(online?"CO-OP":"OFFLINE")}\nБлок: {BlockRegistry.Get(player.Selected).Name} • 1–9\nWASD / мышь / Space / Shift / ЛКМ / ПКМ / Esc",label);
                if(online)GUI.Label(new Rect(16,Screen.height-48,Screen.width-32,40),status,label);return;
            }
            float width=Mathf.Min(640,Screen.width-32);GUILayout.BeginArea(new Rect((Screen.width-width)/2,30,width,Screen.height-50),GUI.skin.box);
            GUILayout.Label("LESTINGCRAFT: NEW ERA",title);GUILayout.Label("WORLD LAB • CO-OP PROTOTYPE",label);GUILayout.Space(12);
            if(world)
            {
                GUILayout.Label($"{dimensions[(int)world.Dimension]} • seed {world.Seed} • {world.LoadedChunks} чанков",label);
                if(online){GUILayout.Label("Код для друзей (комната приватна по этому коду):",label);code=GUILayout.TextField(code);}
                GUI.enabled=world.Ready;if(GUILayout.Button("Продолжить",button,GUILayout.Height(40))){paused=false;SetCursor(true);}GUI.enabled=true;
                if(GUILayout.Button("Выйти в меню",button,GUILayout.Height(34)))Leave();
            }
            else
            {
                GUILayout.Label("Seed мира",label);seedText=GUILayout.TextField(seedText);
                dimension=GUILayout.SelectionGrid(dimension,dimensions,3);GUILayout.Space(8);
                GUI.enabled=!busy;
                if(GUILayout.Button("Играть офлайн",button,GUILayout.Height(38)))
                {if(int.TryParse(seedText,out var seed))StartWorld(seed,dimension,false);else status="Seed должен быть целым числом";}
                GUILayout.Label("Сервер кооператива",label);endpoint=GUILayout.TextField(endpoint);
                GUILayout.Label("Имя игрока",label);playerName=GUILayout.TextField(playerName,24);
                if(GUILayout.Button("Создать комнату для друзей",button,GUILayout.Height(38)))StartCoroutine(CreateRoom());
                GUILayout.Label("Код комнаты",label);code=GUILayout.TextField(code,32);
                if(GUILayout.Button("Подключиться по коду",button,GUILayout.Height(38)))JoinRoom();GUI.enabled=true;
            }
            GUILayout.Space(12);GUILayout.Label(status,label);
            GUILayout.Label("Это технический срез: без боссов, крафта и инвентаря. Вода/лава пока статичны. Мультиплеер ограничен миром 128×128 и 8 игроками.",label);
            GUILayout.EndArea();
        }
        void OnDestroy(){if(avatarMaterial)Destroy(avatarMaterial);}
    }
}
