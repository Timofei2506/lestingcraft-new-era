using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.InputSystem;
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
        readonly string[] dimensions={"Лестингия","Луна","Марс","Венера","Меркурий"};
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
            if(!Session)return;
            if(text.StartsWith("/planet-preview ",StringComparison.OrdinalIgnoreCase))
            {
                if(online||IsSurfacePreview||!Session.Creative||!world||!world.Ready)
                {Session.Notice="Осмотр планеты: только из готового офлайн-мира в творческом режиме";return;}
                string name=text.Substring("/planet-preview ".Length).Trim().ToLowerInvariant();
                int realm=name=="moon"?1:name=="mars"?2:name=="venus"?3:name=="mercury"?4:-1;
                if(realm<0){Session.Notice="Укажите moon, mars, venus или mercury";return;}
                int originalSeed=world.Seed;double originalClock=Session.Data.clock;
                // QA-only instant inspection. This is NOT a space journey; save the origin before leaving.
                Leave();IsSurfacePreview=true;
                StartWorld(originalSeed,realm,false,null,originalClock);
                Session.Notice="ОСМОТР ПЛАНЕТЫ: временный мир без сохранения. Это НЕ космический перелёт. Основное сохранение доступно через меню.";
                return;
            }
            if(text=="/shuttle-demo")
            {
                if(online||IsSurfacePreview||!Session.Creative||!world||!world.Ready){Session.Notice="Демонстрация кабины: только в готовом офлайн-мире Лестингии в творческом режиме";return;}
                if(ShuttleDemo){Session.Notice="Демонстрационная кабина уже создана";return;}
                var node=new GameObject("Space · walkable Discovery prototype");
                node.transform.position=player.transform.position+player.transform.forward*6f;
                ShuttleDemo=node.AddComponent<ShuttleInteriorPreview>();ShuttleDemo.Initialize(player);
                Session.Notice="Кабина установлена: E — кнопки и люки, левый Shift — встать с кресла";
                return;
            }
            var result=ChatCommands.Execute(text,Session.Creative,!Online,Session.Inventory,world.Seed,player.transform.position,PlayerCount);
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
        public bool TradeOpen=>ui&&ui.TradeOpen;
        public bool EditingText=>ui&&ui.EditingText;
        public void OpenTrade(string actor){if(!Session||StoryLocked||Session.Vitals.dead||!Session.Living.TryTrader(actor,out var trader))return;Session.Notice="";Session.Living.Hint="";paused=true;SetCursor(false);ui.ShowTrade(actor);}
        bool paused=true,busy,online;
        VoxelWorld world; FirstPersonController player; RoomClient client;
        Vector3Int? sharedLaunchSite;LaunchSiteVisual launchVisual;string launchBlueprint;bool offlineShuttleSeat;
        NetShuttleLift liftSnapshot;bool hatchOpen;string[] shuttleSeats=new string[2];float lastLiftRequest=-10f;
        NetSpaceMission flightSnapshot;int flightSeatIndex=-1;float lastFlightHeartbeat,lastFlightControl;
        public bool ShuttleSeated=>offlineShuttleSeat||flightSnapshot!=null&&flightSnapshot.active||
            !string.IsNullOrEmpty(selfId)&&Array.Exists(shuttleSeats,id=>id==selfId);
        public void RequestShuttlePreflight()
        {
            if(offlineShuttleSeat&&!online&&Session){Session.Notice="Полёт ещё не готов: старт в офлайн-мире заблокирован";return;}
            if(flightSnapshot!=null&&flightSnapshot.active||!ShuttleSeated||!online||!client||!client.Connected||!Session)return;
            client.Send(new NetPacket{type="space_preflight"});
            Session.Notice="Запрашиваю проверку экипажа. Запуск в этой сборке заблокирован.";
        }
        public void HandleMoonFlightControls(Keyboard keyboard)
        {
            if(keyboard==null||!InMoonFlight||!flightSnapshot.pilot||!client||!client.Connected)return;
            if(keyboard.pKey.wasPressedThisFrame&&flightSnapshot.phase=="cruise")
                client.Send(new NetPacket{type="space_autopilot",on=!flightSnapshot.autopilot});
            if(keyboard.lKey.wasPressedThisFrame&&flightSnapshot.phase=="landing-ready")
                client.Send(new NetPacket{type="space_land"});
            if(Time.unscaledTime-lastFlightControl<.5f)return;
            bool manual=keyboard.leftArrowKey.isPressed||keyboard.rightArrowKey.isPressed||
                keyboard.upArrowKey.isPressed||keyboard.downArrowKey.isPressed;
            bool burn=flightSnapshot.phase=="approach"&&keyboard.spaceKey.isPressed;
            if(!manual&&!burn)return;
            lastFlightControl=Time.unscaledTime;
            double vertical=(keyboard.upArrowKey.isPressed?0.35:0)-(keyboard.downArrowKey.isPressed?0.35:0);
            double lateral=(keyboard.rightArrowKey.isPressed?0.35:0)-(keyboard.leftArrowKey.isPressed?0.35:0);
            client.Send(new NetPacket{type="space_control",heading=new[]{1d,vertical,lateral},
                landingThrottle=burn?1d:0d});
        }
        public LaunchSiteVisual LaunchSite=>launchVisual;
        long revision; float lastPosition,previousYaw,lastHeartbeat;Vector3 previousPosition;bool sentPosition;int previousEquipment;
        Material avatarMaterial;
        GameUI ui;bool leaving,saveAcknowledged;
        public ShuttleInteriorPreview ShuttleDemo {get;private set;}
        public bool InWorld=>world;
        public bool IsSurfacePreview {get;private set;}
        public bool Paused=>paused;
        public bool Online=>online;public string LocalPeerId=>selfId;
        public void SendAnimalHit(string animal,int weapon,int requestId){if(client&&client.Connected)client.Send(new NetPacket{type="animal_hit",animal=animal,weapon=weapon,requestId=requestId});}
        void ApplyHatch(bool open){hatchOpen=open;if(launchVisual&&launchVisual.Cabin)launchVisual.Cabin.SyncHatch(open);}
        void ApplySeats(string[] seats){if(seats!=null&&seats.Length==2)shuttleSeats=seats;}
        // A flight snapshot moves the approved hull. Local and remote occupants are
        // children of that same solid hull, not independently teleported between sectors.
        // This path stays unreachable in public rooms until the Moon surface is ready.
        void ApplyMoonFlight(NetSpaceMission mission)
        {
            if(mission==null||!mission.active||!sharedLaunchSite.HasValue||!player||!world)return;
            int claimed=Array.IndexOf(shuttleSeats,selfId);
            if(claimed>=0)flightSeatIndex=claimed;
            if(flightSeatIndex<0||flightSeatIndex>=2)return;
            if(!launchVisual)
                launchVisual=LaunchSiteVisual.Create(world.transform,sharedLaunchSite.Value,world.Seed);
            if(!LaunchSiteVisual.TryMoonFlightOffset(mission,out var ignored))return;
            if(player.transform.parent!=launchVisual.ShuttleRoot)
                player.transform.SetParent(launchVisual.ShuttleRoot,true);
            for(int i=0;i<shuttleSeats.Length;i++)
            {
                var id=shuttleSeats[i];
                if(!string.IsNullOrEmpty(id)&&avatars.TryGetValue(id,out var remote)&&remote&&
                   remote.transform.parent!=launchVisual.ShuttleRoot)
                    remote.transform.SetParent(launchVisual.ShuttleRoot,true);
            }
            string previous=flightSnapshot?.phase;
            if(!launchVisual.SyncMoonFlight(mission))return;
            mission.pilot=flightSnapshot!=null?flightSnapshot.pilot:flightSeatIndex==0;
            mission.seatIndex=flightSeatIndex;
            flightSnapshot=mission;
            if(Session&&previous!=mission.phase)
                Session.Notice="Общий корабль · "+mission.phase+" · "+mission.elapsed.ToString("0")+" / 1800 с";
        }
        void ApplyFlightCrewJoin(NetPacket packet)
        {
            if(flightSnapshot==null||!flightSnapshot.active||!launchVisual||
               packet.id==selfId||packet.seatIndex<0||packet.seatIndex>=2||
               string.IsNullOrEmpty(packet.id)||!sharedLaunchSite.HasValue)return;
            double sx,sy,sz;
            VerticalShuttleWalk.Seat(packet.seatIndex,out sx,out sy,out sz);
            var site=sharedLaunchSite.Value;
            SetAvatar(new NetPlayer{id=packet.id,name=packet.name,
                x=site.x+(float)sx,y=site.y+2+(float)sy+PlayerCapsule.EyeHeight,
                z=site.z+(float)sz});
            if(avatars.TryGetValue(packet.id,out var remote))
            {
                remote.transform.SetParent(launchVisual.ShuttleRoot,false);
                remote.transform.localPosition=new Vector3((float)sx,(float)sy,(float)sz);
            }
        }
        void ApplyLift(NetShuttleLift state)
        {
            if(state==null||!state.active)return;
            liftSnapshot=state;
            if(launchVisual&&launchVisual.Cabin)
                launchVisual.Cabin.SyncLift(state,player,selfId);
        }
        /// <summary>E near the real lift asks the room, never moves the lift locally.</summary>
        public bool TryInteractLaunch()
        {
            if(!online)return TryInteractOfflineLaunch();
            if(!client||!client.Connected||!player||!launchVisual||!launchVisual.Cabin||
               liftSnapshot==null||!liftSnapshot.active||!Session)return false;
            if(flightSnapshot!=null&&flightSnapshot.active)
            {Session.Notice="Экипаж в полёте: выход откроется после посадки на поверхность";return true;}
            var cabin=launchVisual.Cabin;
            var local=cabin.transform.InverseTransformPoint(player.transform.position);
            for(int i=0;i<VerticalShuttleWalk.SeatCount;i++)
            {
                double sx,sy,sz;VerticalShuttleWalk.Seat(i,out sx,out sy,out sz);
                if(Mathf.Abs(local.x-(float)sx)>1f||Mathf.Abs(local.z-(float)sz)>1f||
                   Mathf.Abs(local.y-(float)sy)>1.3f)continue;
                if(Time.unscaledTime-lastLiftRequest<.4f)return true;
                if(!string.IsNullOrEmpty(shuttleSeats[i])&&shuttleSeats[i]!=selfId)
                {Session.Notice="Это место занято вторым игроком";return true;}
                string command=shuttleSeats[i]==selfId?"stand":"sit";
                lastLiftRequest=Time.unscaledTime;
                client.Send(new NetPacket{type="space_seat",action=command,index=i});
                Session.Notice=command=="sit"?"Запрос места в кабине":"Встать с места";
                return true;
            }
            if(Mathf.Abs(local.x+9)<3.5f&&Mathf.Abs(local.z-3)<1.3f&&Mathf.Abs(local.y-28)<2)
            {
                if(Time.unscaledTime-lastLiftRequest<.4f)return true;
                lastLiftRequest=Time.unscaledTime;
                client.Send(new NetPacket{type="space_hatch",open=!hatchOpen});
                Session.Notice=hatchOpen?"Запрос закрытия люка":"Запрос открытия люка";
                return true;
            }
            if(Mathf.Abs(local.x-VerticalShuttleWalk.LiftX)>1.5f||Mathf.Abs(local.z-3)>1.5f)return false;
            if(Time.unscaledTime-lastLiftRequest<.4f)return true;
            if(cabin.Lift.IsMoving){Session.Notice="Лифт движется — дождитесь остановки";return true;}
            bool ground=Mathf.Abs(local.y-VerticalShuttleWalk.LiftGroundY)<1.15f;
            bool top=Mathf.Abs(local.y-(VerticalShuttleWalk.LiftGroundY+VerticalShuttleWalk.LiftTravel))<1.15f;
            if(!ground&&!top)return false;
            string action;
            if(liftSnapshot.occupied)
            {
                if(liftSnapshot.riderId!=selfId){Session.Notice="Лифт занят другим участником";return true;}
                action="exit";
            }
            else if(cabin.Lift.State==ElevatorState.Ground&&ground)action="up";
            else if(cabin.Lift.State==ElevatorState.Top&&top)action="down";
            else if(cabin.Lift.State==ElevatorState.Top&&ground)action="recall";
            else {Session.Notice="Войдите на платформу лифта";return true;}
            lastLiftRequest=Time.unscaledTime;client.Send(new NetPacket{type="space_lift",action=action});
            Session.Notice=action=="up"?"Запрос подъёма лифта":action=="down"?"Запрос спуска лифта":
                action=="exit"?"Выход с лифта — проходите пешком":"Возврат пустого лифта для следующего игрока";
            return true;
        }
        // The offline creative world uses the same colliding cabin and 25 m
        // mechanical lift, but never fabricates a server flight or a Moon arrival.
        bool TryInteractOfflineLaunch()
        {
            if(!player||!launchVisual||!launchVisual.Cabin||!Session||!Session.Creative)return false;
            var cabin=launchVisual.Cabin;
            var local=cabin.transform.InverseTransformPoint(player.transform.position);
            for(int i=0;i<VerticalShuttleWalk.SeatCount;i++)
            {
                double sx,sy,sz;VerticalShuttleWalk.Seat(i,out sx,out sy,out sz);
                if(Mathf.Abs(local.x-(float)sx)>1f||Mathf.Abs(local.z-(float)sz)>1f||Mathf.Abs(local.y-(float)sy)>1.3f)continue;
                offlineShuttleSeat=!offlineShuttleSeat;
                Session.Notice=offlineShuttleSeat?"Вы в кабине. Полёт пока заблокирован. E — встать.":"Вы встали с кресла";
                return true;
            }
            if(Mathf.Abs(local.x+9)<3.5f&&Mathf.Abs(local.z-3)<1.3f&&Mathf.Abs(local.y-28)<2)
            {cabin.SyncHatch(!cabin.HatchOpen);Session.Notice=cabin.HatchOpen?"Люк открыт — проходите пешком":"Люк закрыт";return true;}
            if(Mathf.Abs(local.x-VerticalShuttleWalk.LiftX)>1.5f||Mathf.Abs(local.z-3)>1.5f)return false;
            if(cabin.Lift.IsMoving){Session.Notice="Лифт движется";return true;}
            bool ground=Mathf.Abs(local.y-VerticalShuttleWalk.LiftGroundY)<1.15f;
            bool top=Mathf.Abs(local.y-(VerticalShuttleWalk.LiftGroundY+VerticalShuttleWalk.LiftTravel))<1.15f;
            if(!ground&&!top)return false;
            if(cabin.Lift.Occupied)
            {cabin.Lift.Exit();Session.Notice="Выйдите с платформы пешком";return true;}
            bool ok=ground&&cabin.Lift.State==ElevatorState.Ground?cabin.RequestUp(player):
                top&&cabin.Lift.State==ElevatorState.Top?cabin.RequestDown(player):
                ground&&cabin.Lift.State==ElevatorState.Top&&cabin.RecallEmptyLift();
            Session.Notice=ok?"Лифт в пути — стойте на платформе":"Войдите на свободную платформу";
            return true;
        }
        public bool IsConnecting=>busy;
        public string LastError=>lastError;
        public string ServerLabel=>endpoint.Replace("https://","").Replace("http://","");
        public string WorldSeed=>seedText;
        public string PlayerName {get=>playerName;set=>playerName=value;}
        public string RoomCode {get=>code;set=>code=value.Trim();}
        public string WorldLabel=>world?dimensions[(int)world.Dimension]+"  /  SEED "+world.Seed+
            (sharedLaunchSite.HasValue?"  ·  Космодром X="+sharedLaunchSite.Value.x+" Z="+sharedLaunchSite.Value.z:""):"";
        public int PlayerCount=>avatars.Count+1;
        public int ChunkCount=>world?world.LoadedChunks:0;
        public bool WorldReady=>world&&world.Ready;
        public bool LaunchPlacementArmed=>!string.IsNullOrEmpty(launchBlueprint);
        public void ArmLaunchPlacement(string blueprint)
        {
            if(Session==null||!Session.Creative||IsSurfacePreview||
               (blueprint!="pad"&&blueprint!="shuttle"))return;
            launchBlueprint=blueprint;
            Session.Notice="Чертёж комплекса: ПКМ по земле в начале будущей рампы (стол и шаттл ставятся вместе).";
        }
        public void PlaceLaunchAt(Vector3Int ground)
        {
            if(!LaunchPlacementArmed||!Session||!Session.Creative||!world||!world.Ready||IsSurfacePreview)return;
            int natural=TerrainGenerator.Height(ground.x,ground.z,world.Seed,Dimension.Lestingia);
            if(Mathf.Abs(ground.y-natural)>2||Mathf.Abs(player.transform.position.y-(natural+1))>6)
            {Session.Notice="Выберите землю вблизи персонажа, не постройку и не дерево";return;}
            int x=ground.x+20,z=ground.z+110;
            if(x < -320||x > 320||z < -260||z > 320)
            {Session.Notice="Недостаточно места для платформы и рампы: выберите другое место";return;}
            if(online)
            {
                if(!client||!client.Connected){Session.Notice="Нет соединения с комнатой";return;}
                client.Send(new NetPacket{type="space_site_place",action=launchBlueprint,x=ground.x,y=ground.y,z=ground.z});
                Session.Notice="Сервер проверяет точку постройки…";
            }
            else
            {
                var site=LaunchSiteVisual.OfflineSite(world.Seed,x,z);
                if(site.y<40||site.y>140){Session.Notice="Высота для безопасной платформы здесь недоступна";return;}
                SetLaunchSite(site);
                Session.Data.launchPlaced=true;Session.Data.launchX=x;Session.Data.launchZ=z;
                Session.Checkpoint();Session.Notice="Общий чертёж установлен: идите по рампе к шаттлу. Полёт пока заблокирован.";
            }
            launchBlueprint=null;
        }
        void SetLaunchSite(Vector3Int site)
        {
            if(launchVisual){Destroy(launchVisual.gameObject);launchVisual=null;}
            sharedLaunchSite=site;offlineShuttleSeat=false;
            // A relocated pad must not retain the old hatch/lift/cockpit state.
            hatchOpen=false;liftSnapshot=null;shuttleSeats=new string[2];
        }
        public bool HasSharedLaunchSite=>sharedLaunchSite.HasValue;
        public Vector3Int? LaunchSitePosition=>sharedLaunchSite;
        public bool InMoonFlight=>flightSnapshot!=null&&flightSnapshot.active;
        public string MoonFlightStatus=>flightSnapshot==null?"":
            "ЛУНА · "+flightSnapshot.phase+" · "+flightSnapshot.elapsed.ToString("0")+" / 1800 с\n"+
            (flightSnapshot.pilot?"P — автопилот · стрелки — курс · Space — тормоз · L — посадка":"Вы — второй член экипажа");
        public ushort SelectedBlock=>Session?Items.Get(Session.Inventory.Active.item).Block:(ushort)1;
        public string ModeName=>GameRules.ModeNames[Session?(int)Session.Mode:selectedMode];
        public void PlayOffline()
        {if(busy||world)return;IsSurfacePreview=false;int seed=Array.IndexOf(Environment.GetCommandLineArgs(),"-lc-smoke-test")>=0?2506:Guid.NewGuid().GetHashCode();seedText=seed.ToString();StartWorld(seed,0,false);}
        public void ContinueOffline()
        {
            if(busy||world)return;resumeSave=LocalSave.Load();if(resumeSave==null){Fail("Сохранение повреждено");return;}
            if(resumeSave.campaign.runEnded){resumeSave=null;Fail("Хардкорный забег завершён. Создайте новый.");return;}
            IsSurfacePreview=false;selectedMode=resumeSave.mode;StartWorld(resumeSave.seed,0,false);
        }
        public void ToggleInventory()
        {
            if(flightSnapshot!=null&&flightSnapshot.active)
            {if(Session)Session.Notice="Инвентарь полёта пока недоступен; экипаж остаётся в креслах";return;}
            if(!Session||StoryLocked||Session.Vitals.dead||Session.Data.campaign.runEnded)return;
            if(ui.InventoryOpen){Resume();return;}paused=true;SetCursor(false);ui.ShowInventory();
        }
        public void OpenStation(int id,Vector3Int cell)
        {
            if(!Session||!Session.StationNearby(id,cell))return;
            if(id==17){var f=Session.Inventory.furnace;f.x=cell.x;f.y=cell.y;f.z=cell.z;f.realm=(int)Session.World.Dimension;}
            paused=true;SetCursor(false);ui.ShowInventory(id,cell);
        }
        public void UseStation(int id,Vector3Int cell){if(Session)Session.UseStation(id,cell);}
        public void OpenDeathScreen(){paused=true;SetCursor(false);ui.ShowDeathScreen();}
        public void SendSave(PlayerSave save,bool force=false)
        {
            // Ground inventory was checkpointed before ignition. A flight crew may
            // not overwrite its terrestrial save with a fabricated ground position.
            if(flightSnapshot!=null&&flightSnapshot.active)return;
            if(client&&client.Connected)client.Send(new NetPacket{type="state",save=save,force=force});
        }
        public void CreateRoom(){if(!busy)StartCoroutine(CreateRoomRoutine());}
        public void JoinRoom(){if(!busy)JoinRoomInternal();}
        public void Resume(){if(!world||Session&&(Session.Vitals.dead||Session.Data.campaign.runEnded))return;if(Session&&Session.Living)Session.Living.Hint="";paused=false;SetCursor(true);ui.ShowHUD();}
        public void ReturnToMenu(){if(!leaving)StartCoroutine(LeaveAfterCheckpoint());}
        IEnumerator LeaveAfterCheckpoint()
        {
            leaving=true;paused=true;SetCursor(false);
            if(online&&client&&client.Connected&&Session&&!Session.ServerRunEnded&&
               !(flightSnapshot!=null&&flightSnapshot.active))
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
        void StartWorld(int seed,int realm,bool multiplayer,PlayerSave savedPlayer=null,double clock=420,int contentVersion=7,StoryState story=null,long serverNow=0,NetLaunchSite approvedSite=null,Vector3? cockpitRestore=null)
        {
            // Multiplayer and persisted worlds remain on Lestingia; planet inspection is explicitly offline and unsaved.
            if(multiplayer)IsSurfacePreview=false;
            realm=multiplayer||!IsSurfacePreview||realm<1||realm>4?0:realm;
            world=new GameObject("World").AddComponent<VoxelWorld>();online=multiplayer;world.Initialize(seed,(Dimension)realm,multiplayer,resumeSave?.contentVersion??contentVersion);
            sharedLaunchSite=multiplayer&&realm==0&&approvedSite!=null&&approvedSite.active&&
                approvedSite.x>=-320&&approvedSite.x<=320&&approvedSite.z>=-260&&approvedSite.z<=320&&
                approvedSite.y>=40&&approvedSite.y<=140?
                new Vector3Int(approvedSite.x,approvedSite.y,approvedSite.z):(Vector3Int?)null;
            if(!multiplayer&&realm==0&&!IsSurfacePreview&&resumeSave!=null&&resumeSave.launchPlaced)
                sharedLaunchSite=LaunchSiteVisual.OfflineSite(seed,resumeSave.launchX,resumeSave.launchZ);
            var camera=Camera.main;camera.backgroundColor=new Color(.42f,.66f,.78f);
            RenderSettings.fogColor=camera.backgroundColor;
            var obj=new GameObject("Player");
            // On reconnect only: restore a previously server-verified crew member
            // directly in their cockpit seat before any gameplay frame is rendered.
            obj.transform.position=cockpitRestore??FindSpawn(seed,(Dimension)realm,world.ContentVersion);
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
                    // A docked station still has no walkable Unity client. For a lunar
                    // flight, restore only a verified cockpit seat and a valid continuous
                    // hull projection; never spawn a flying crew member on Earth.
                    if(packet.station!=null&&packet.station.active||
                       packet.space!=null&&packet.space.active&&
                       (packet.launchSite==null||!packet.launchSite.active||
                        packet.space.seatIndex<0||packet.space.seatIndex>=2||
                        !LaunchSiteVisual.TryMoonFlightOffset(packet.space,out var offset)))
                    {Fail("Невозможно восстановить общий корабль или станцию: обновите клиент. Публичный релиз заблокирован.");Destroy(client.gameObject);busy=false;return;}
                    if(!GameRules.AcceptsRoom(packet.rulesVersion,packet.serverRulesVersion)){Fail("Несовместимые правила комнаты. Обновите сервер; для нового содержимого нужна новая комната");Destroy(client.gameObject);return;}
                    if(packet.contentVersion>7||packet.contentVersion<0||packet.rulesVersion>=10&&packet.contentVersion!=7||packet.rulesVersion==9&&packet.contentVersion!=6||packet.rulesVersion==8&&packet.contentVersion!=5||packet.rulesVersion==7&&packet.contentVersion!=4||packet.rulesVersion==6&&packet.contentVersion!=3||packet.rulesVersion==5&&packet.contentVersion!=2){Fail("Несовместимая версия содержимого мира");Destroy(client.gameObject);return;}
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
                case "player":if(Session)Session.Living.Apply(packet);if(Story&&packet.story!=null)Story.Apply(packet.story,packet.serverNow);if(Session)Session.Animals.Apply(packet);ApplyLift(packet.lift);SetAvatar(new NetPlayer{id=packet.id,name=packet.name,x=packet.x,y=packet.y,z=packet.z,yaw=packet.yaw,offhand=packet.offhand,weapon=packet.weapon,equipment=packet.equipment});break;
                case "space_site_place":
                    if(packet.launchSite!=null&&packet.launchSite.active&&
                       packet.launchSite.x>=-320&&packet.launchSite.x<=320&&
                       packet.launchSite.z>=-260&&packet.launchSite.z<=320&&
                       packet.launchSite.y>=40&&packet.launchSite.y<=140&&world)
                    {SetLaunchSite(new Vector3Int(packet.launchSite.x,packet.launchSite.y,packet.launchSite.z));
                     ApplyLift(packet.lift);if(Session)Session.Notice="Комплекс установлен в общей комнате: рампа, стол и шаттл. Полёт пока заблокирован.";}
                    break;
                case "space_lift":ApplyLift(packet.lift);break;
                case "space_hatch":ApplyHatch(packet.hatchOpen);break;
                case "space_seat":ApplySeats(packet.seats);break;
                case "space_mission":if(packet.space!=null&&(ShuttleSeated||flightSnapshot!=null))
                    ApplyMoonFlight(packet.space);break;
                case "space_crew_join":ApplyFlightCrewJoin(packet);break;
                case "space_preflight":if(Session&&packet.id==selfId)
                    Session.Notice=packet.ready?"Экипаж из двух человек проверен сервером. Запуск и полёт ещё не подключены.":
                        "Предстартовая проверка не пройдена: "+string.Join("; ",packet.issues??Array.Empty<string>());
                    break;
                case "leave":if(avatars.TryGetValue(packet.id,out var avatar))Destroy(avatar);avatars.Remove(packet.id);targets.Remove(packet.id);break;
                case "state_saved":if(packet.id==selfId)saveAcknowledged=true;if(Session&&packet.campaign!=null)Session.Data.campaign=packet.campaign;break;
                case "run_ended":if(packet.id==selfId)saveAcknowledged=true;if(Session){Session.ServerRunEnded=true;Session.Data.campaign.runEnded=true;Session.Notice="Хардкор завершён: погиб участник команды";OpenDeathScreen();}break;
                case "error":if(Session)Session.Living.Apply(packet);Fail(packet.message);if(Session){if(string.IsNullOrEmpty(packet.code)||packet.code=="edit")Session.RejectEdit(packet.message);else{Session.Notice=packet.message;if(packet.code=="animal_hit")Session.Animals.Reject(packet.requestId);}}break;
            }
        }
        void BeginNetworkWorld(NetPacket packet)
        {
            Vector3? seatRestore=null;
            if(packet.space!=null&&packet.space.active&&packet.launchSite!=null)
            {
                double sx,sy,sz;
                VerticalShuttleWalk.Seat(packet.space.seatIndex,out sx,out sy,out sz);
                seatRestore=new Vector3(packet.launchSite.x+(float)sx,
                    packet.launchSite.y+2+(float)sy,packet.launchSite.z+(float)sz);
                flightSeatIndex=packet.space.seatIndex;
            }
            InLobby=false;
            StartWorld(packet.seed,packet.dimension,true,packet.save,packet.clock,packet.contentVersion<=0?1:packet.contentVersion,packet.story,packet.serverNow,packet.launchSite,seatRestore);
            revision=packet.rev;Session.Animals.Apply(packet);ApplyLift(packet.lift);ApplyHatch(packet.hatchOpen);ApplySeats(packet.seats);
            if(packet.campaign!=null)Session.Data.campaign=packet.campaign;if(packet.runEnded){Session.ServerRunEnded=true;Session.Data.campaign.runEnded=true;OpenDeathScreen();}
            if(packet.edits!=null)foreach(var edit in packet.edits)world.Edit(edit.x,edit.y,edit.z,(ushort)edit.block);
            Session.Living.Apply(packet);
            if(packet.players!=null)foreach(var peer in packet.players)SetAvatar(peer);
            if(packet.space!=null&&packet.space.active)ApplyMoonFlight(packet.space);
            status="Комната подключена • "+code;
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
            // Only the server-chosen site is placed. Both peers reconstruct the same
            // approved, colliding geometry as they approach; no launch is enabled yet.
            if(sharedLaunchSite.HasValue&&!launchVisual&&world&&world.Ready&&player&&
               new Vector2(player.transform.position.x-sharedLaunchSite.Value.x,
                           player.transform.position.z-sharedLaunchSite.Value.z).sqrMagnitude<140f*140f)
            {
                launchVisual=LaunchSiteVisual.Create(world.transform,sharedLaunchSite.Value,world.Seed);
                ApplyLift(liftSnapshot);
                ApplyHatch(hatchOpen);
            }
            foreach(var pair in avatars)if(pair.Value&&targets.TryGetValue(pair.Key,out var target))
            {
                if(flightSnapshot!=null&&flightSnapshot.active&&launchVisual&&
                   pair.Value.transform.parent==launchVisual.ShuttleRoot)continue;
                pair.Value.transform.position=Vector3.Lerp(pair.Value.transform.position,target,Time.deltaTime*12);
            }
            if(online&&client&&client.Connected&&flightSnapshot!=null&&flightSnapshot.active)
            {
                if(Time.unscaledTime-lastFlightHeartbeat>=.75f)
                {client.Send(new NetPacket{type="space_heartbeat"});lastFlightHeartbeat=Time.unscaledTime;}
            }
            else if(online&&client&&client.Connected&&player&&world.Ready&&Time.unscaledTime-lastPosition>=.2f)
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
            if(ShuttleDemo){Destroy(ShuttleDemo.gameObject);ShuttleDemo=null;}
            if(player){var hands=player.GetComponent<FirstPersonHands>();if(hands)hands.Release();Destroy(player.gameObject);}if(world)Destroy(world.gameObject);
            client=null;world=null;player=null;launchBlueprint=null;offlineShuttleSeat=false;sharedLaunchSite=null;launchVisual=null;liftSnapshot=null;hatchOpen=false;
            shuttleSeats=new string[2];flightSnapshot=null;flightSeatIndex=-1;lastFlightHeartbeat=0;lastFlightControl=0;
            online=false;busy=false;paused=true;SetCursor(false);status="";lastError="";WorldAtmosphere.Clear();
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
