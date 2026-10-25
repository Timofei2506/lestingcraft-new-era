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
    public sealed partial class PrototypeGame : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){if(!FindAnyObjectByType<PrototypeGame>())new GameObject("LestingCraft").AddComponent<PrototypeGame>();}
        readonly string[] dimensions={
            "Лестингия","Луна","Марс","Венера","Меркурий",
            "Европа","Титан","Плутон","Проксима b","TRAPPIST-1e","Кеплер-452b","Андромеда Прайм","Сомния","Измерение «Сон»","Черенковский Затон"
        };
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

        // === FULL COSMOS + 3D MOUSE COCKPIT + DREAM ANOMALY + INDUSTRIAL TECH & RADIA ABYSS ===
        public readonly OpenSpaceHelm SpaceHelm = new OpenSpaceHelm();
        public readonly DreamAnomaly Dream = new DreamAnomaly();
        public IndustrialTechRuntime Tech => techRuntime ? techRuntime : (techRuntime = IndustrialTechRuntime.Ensure(null));
        IndustrialTechRuntime techRuntime;
        public bool CockpitMouseMode { get; private set; }
        public bool InDreamDimension => Dream.IsActive || (world && world.Dimension == Dimension.Dream);
        public bool InRadiaDimension => world && world.Dimension == Dimension.Radia;
        int storyCodexIndex;
        int storyProgressionMask;

        static readonly SpaceTargetKind[] TargetCycleOrder =
        {
            SpaceTargetKind.Moon,
            SpaceTargetKind.OrbitalStation,
            SpaceTargetKind.Mars,
            SpaceTargetKind.Venus,
            SpaceTargetKind.Mercury,
            SpaceTargetKind.Europa,
            SpaceTargetKind.Titan,
            SpaceTargetKind.Pluto,
            SpaceTargetKind.ProximaB,
            SpaceTargetKind.Trappist1e,
            SpaceTargetKind.Kepler452b,
            SpaceTargetKind.AndromedaPrime,
            SpaceTargetKind.Somnia,
            SpaceTargetKind.Earth
        };
        int selectedTargetIdx;
        SpaceFlightVisuals spaceVisuals;
        PlanetaryBossController activePlanetBoss;
        SpaceNavigation offlineNav;
        bool offlineFlightActive;
        bool offlineWarp30x;
        double offlineFlightCountdown;
        bool offlineRetroBurning;
        bool dockedAtStation;
        int stationTradeRequestSeq = 1;
        float lastReadyAckSent;

        public SpaceTargetKind SelectedSpaceTarget => TargetCycleOrder[selectedTargetIdx % TargetCycleOrder.Length];
        public bool DockedAtOrbitalStation => dockedAtStation;
        public bool StationTradeOpen => ui && ui.IsStationTradeOpen;
        public string CurrentPlanetTitle => world ? dimensions[Mathf.Clamp((int)world.Dimension, 0, dimensions.Length - 1)] : "Космос";

        public void OpenChat()
        {
            if(!world||paused||InDreamDimension)return;
            ChatOpen=true;SetCursor(false);ui.SetChatOpen(true);
        }
        public void CloseChat(){ChatOpen=false;if(ui)ui.SetChatOpen(false);SetCursor(world&&!paused&&!CockpitMouseMode);}
        public void SubmitChat()
        {
            if(InDreamDimension){CloseChat();return;}
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
                int realm=name=="moon"?1:name=="mars"?2:name=="venus"?3:name=="mercury"?4:
                          name=="europa"?5:name=="titan"?6:name=="pluto"?7:name=="proxima"?8:
                          name=="trappist"?9:name=="kepler"?10:name=="andromeda"?11:name=="somnia"?12:name=="dream"?13:-1;
                if(realm<0){Session.Notice="Укажите moon, mars, venus, mercury, europa, titan, pluto, proxima, trappist, kepler, andromeda, somnia или dream";return;}
                int originalSeed=world.Seed;double originalClock=Session.Data.clock;
                Leave();IsSurfacePreview=true;
                StartWorld(originalSeed,realm,false,null,originalClock);
                Session.Notice="ОСМОТР ПЛАНЕТЫ: временный мир без сохранения.";
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
        public void CloseStationTrade(){if(!ui)return;Resume();}

        bool paused=true,busy,online;
        VoxelWorld world; FirstPersonController player; RoomClient client;
        Vector3Int? sharedLaunchSite;LaunchSiteVisual launchVisual;string launchBlueprint;bool offlineShuttleSeat;
        NetShuttleLift liftSnapshot;bool hatchOpen;string[] shuttleSeats=new string[2];float lastLiftRequest=-10f;
        NetSpaceMission flightSnapshot;int flightSeatIndex=-1;float lastFlightHeartbeat,lastFlightControl;

        public bool ShuttleSeated=>offlineShuttleSeat||offlineFlightActive||
            (flightSnapshot!=null&&flightSnapshot.active)||
            (!string.IsNullOrEmpty(selfId)&&Array.Exists(shuttleSeats,id=>id==selfId));

        public bool InMoonFlight=>offlineFlightActive||SpaceHelm.Active||(flightSnapshot!=null&&flightSnapshot.active);

        public string BossHudStatus
        {
            get
            {
                if(InDreamDimension&&Dream.IsParalyzed)
                    return "Сонный паралич ("+Math.Max(0.0,6.5-Dream.PhaseSeconds).ToString("0.0")+" с)";
                if(!activePlanetBoss||activePlanetBoss.Defeated||!player)return "";
                float d=Vector3.Distance(player.transform.position,activePlanetBoss.transform.position);
                if(d>28f)return "";
                return $"{activePlanetBoss.BossName} · {activePlanetBoss.Health} / {activePlanetBoss.MaxHealth}";
            }
        }

        public string SpaceHudStatus
        {
            get
            {
                if(InDreamDimension)
                    return Dream.IsParalyzed
                        ? "Вы не можете пошевелиться и взаимодействовать с миром · Связь с игроками оборвана"
                        : "Вы во Сне: взаимодействие с блоками/предметами и мультиплеер полностью отключены";
                string targetLabel=SpaceFlightVisuals.TargetNameRu(SelectedSpaceTarget);
                if(dockedAtStation)
                    return "ОРБИТАЛЬНАЯ СТАНЦИЯ · СТЫКОВКА АКТИВНА\nE у астронавтов — обмен · Z — невесомость (0g) · E у южного шлюза — в шаттл";
                if(SpaceHelm.Active)
                {
                    var body=CelestialAtlas.GetBody(SpaceHelm.TargetBody);
                    string jumpInfo=SpaceHelm.InHyperjump
                        ? $"ГИПЕРПРЫЖОК {(int)(SpaceHelm.HyperjumpProgress*100)}% → {body.NameRu}"
                        : (SpaceHelm.InLandingRange?"ГОТОВ К ПОСАДКЕ / СТЫКОВКЕ [L]!":$"Дистанция {SpaceHelm.DistanceToTargetKm():0} км");
                    string procedural=spaceVisuals?spaceVisuals.ProceduralSkyLine:"";
                    return $"ОТКРЫТЫЙ КОСМОС 6-DOF · {CelestialAtlas.GalaxyNameRu(SpaceHelm.CurrentGalaxy)} · {CelestialAtlas.GetSystem(SpaceHelm.CurrentSystem).NameRu}\n"+
                           (string.IsNullOrEmpty(procedural)?"":procedural+"\n")+
                           $"Цель: {body.NameRu} · {jumpInfo} · Тяга {(int)(SpaceHelm.Throttle*100)}%\n"+
                           "[M/Tab] Мышь по 3D-приборам · [WASD+Стрелки+Q/E] Штурвал · [J] Гиперпрыжок · [V] Галактика M31 · [L] Посадка";
                }
                if(offlineFlightActive&&offlineNav!=null)
                {
                    if(offlineFlightCountdown>0)
                        return $"ЦЕЛЬ: {targetLabel} · ОБРАТНЫЙ ОТСЧЁТ T-{offlineFlightCountdown:0.0} с\nПристегните ремни — двигатели выходят на тягу!";
                    string st=offlineNav.Stage==FlightStage.Ascent?"Выведение на орбиту":
                              offlineNav.Stage==FlightStage.Cruise?"Межпланетный перелёт":
                              offlineNav.Stage==FlightStage.Approach?"Сближение (зажмите Space для торможения!)":
                              "ГОТОВ К ПОСАДКЕ / СТЫКОВКЕ (нажмите L!)";
                    return $"{targetLabel.ToUpperInvariant()} · {st}\n"+
                           $"Время {offlineNav.Elapsed:0}/{offlineNav.Duration:0} с · Варп {(offlineWarp30x?"30×":"1×")} · Тормозной импульс {Math.Min(10.0,offlineNav.LandingBurn):0.0}/10 с\n"+
                           "[O] Свободный полёт (Открытый космос) · [M] Мышь 3D-кабины · [T] Варп 30× · [Space] Тормоз · [L] Посадка";
                }
                if(flightSnapshot!=null&&flightSnapshot.active)
                {
                    return "КООП-ПОЛЁТ → "+SpaceFlightVisuals.TargetNameRu(SpaceFlightVisuals.ParseWireKey(flightSnapshot.to)).ToUpperInvariant()+
                           " · "+flightSnapshot.phase+" · "+flightSnapshot.elapsed.ToString("0")+" / "+flightSnapshot.seconds.ToString("0")+" с\n"+
                           $"Торможение: {flightSnapshot.landingBurn:0.0}/20 с · "+
                           (flightSnapshot.pilot?"[M] Мышь 3D-кабины · [T] Варп · [P] Автопилот · [Space] Тормоз · [L] Посадка":"Вы — второй пилот экипажа");
                }
                if(ShuttleSeated)
                {
                    return $"КАБИНА ШАТТЛА · ЦЕЛЬ: {targetLabel.ToUpperInvariant()}"+
                           (CockpitMouseMode?" · [РЕЖИМ МЫШИ 3D-ПРИБОРОВ АКТИВЕН]":"")+"\n"+
                           "[M / Tab] Управление мышью по 3D-тумблерам, РУД и MFD · [G] Цель · [R] ПУСК · [O] Открытый космос · [E] Встать";
                }
                return "";
            }
        }

        public string MoonFlightStatus => SpaceHudStatus;

        public void SetCockpitMouseMode(bool active)
        {
            CockpitMouseMode=active&&ShuttleSeated;
            SetCursor(world&&!paused&&!ChatOpen&&!CockpitMouseMode);
            if(Session)
                Session.Notice=CockpitMouseMode
                    ? "Режим мыши 3D-кабины: нажимайте тумблеры, колпак ВЗВОД, ПУСК, РУД (колесо/перетаскивание), РУС и экран MFD!"
                    : "Обзор головой возвращён ([M] или [Tab] — снова курсор по 3D-приборам)";
        }

        public void HandleSeatedCockpitMouse(Camera cam,Mouse mouse,bool cursorFree)
        {
            if(!cam||mouse==null||!launchVisual||!launchVisual.Cabin||!launchVisual.Cabin.InteractiveRig)return;
            var rig=launchVisual.Cabin.InteractiveRig;
            rig.BindHelm(SpaceHelm);
            Vector2 pointer=cursorFree
                ? mouse.position.ReadValue()
                : new Vector2(Screen.width*0.5f,Screen.height*0.5f);
            Ray ray=cam.ScreenPointToRay(new Vector3(pointer.x,pointer.y,0f));
            var act=rig.StepMouseInteraction(
                ray,
                mouse.leftButton.wasPressedThisFrame,
                mouse.leftButton.isPressed,
                mouse.leftButton.wasReleasedThisFrame,
                mouse.delta.ReadValue(),
                mouse.scroll.ReadValue().y*0.02f);
            if(act.Triggered)ApplyCockpitAction(act);
        }

        public void ApplyCockpitAction(CockpitActionResult act)
        {
            if(!Session)return;
            if(!string.IsNullOrEmpty(act.MessageRu))Session.Notice=act.MessageRu;
            if(launchVisual&&launchVisual.Cabin&&act.Control==CockpitControlId.HatchSwitch)
                launchVisual.Cabin.SyncHatch(!launchVisual.Cabin.Panel.HatchSealed);
            if(act.RequestedIgnite)
            {
                if(SpaceHelm.Active)SpaceHelm.StartHyperjump(SpaceHelm.TargetBody);
                else RequestShuttlePreflight();
            }
            if(act.RequestedAbort&&offlineFlightActive&&offlineFlightCountdown>0)
            {
                offlineFlightActive=false;offlineNav=null;offlineFlightCountdown=0;
                if(launchVisual)launchVisual.SyncSpaceFlightOffset(0f,0f);
                Session.Notice="АВАРИЙНЫЙ СБРОС: обратный отсчёт остановлен до отрыва!";
            }
            if(act.Control==CockpitControlId.MfdTargetPrev||act.Control==CockpitControlId.MfdTargetNext||
               act.Control==CockpitControlId.MfdCycleSystem||act.Control==CockpitControlId.MfdToggleGalaxy)
            {
                SyncTargetIdxFromAtlasBody(SpaceHelm.TargetBody);
            }
            if(act.RequestedHyperjump&&!SpaceHelm.Active)
            {
                EnterOpenSpaceFlight(true);
            }
            if(act.RequestedLandOrDock)
            {
                if(SpaceHelm.Active)TryLandFromOpenSpace();
                else if(offlineFlightActive&&offlineNav!=null)
                {
                    if(SelectedSpaceTarget==SpaceTargetKind.OrbitalStation)CompleteOfflineStationDocking();
                    else if(offlineNav.Land())CompleteOfflinePlanetLanding(SelectedSpaceTarget);
                }
            }
        }

        void SyncTargetIdxFromAtlasBody(CelestialBodyId bodyId)
        {
            var kind=SpaceFlightVisuals.TargetForBody(bodyId);
            for(int i=0;i<TargetCycleOrder.Length;i++)
                if(TargetCycleOrder[i]==kind){selectedTargetIdx=i;return;}
        }

        public void EnterOpenSpaceFlight(bool immediateHyperjump=false)
        {
            if(!launchVisual||!player||!world||!Session)return;
            Session.Checkpoint();
            offlineFlightActive=false;
            offlineNav=null;
            offlineShuttleSeat=true;
            CelestialBodyId originBody=CelestialBodyId.Earth;
            foreach(var b in CelestialAtlas.Bodies)
                if(b.HasSurface&&b.TargetDimension==world.Dimension){originBody=b.Id;break;}
            if(!SpaceHelm.Active)
                SpaceHelm.EnterOrbitAround(originBody);
            SpaceHelm.SelectTarget(CelestialAtlas.ParseWireKey(SpaceFlightVisuals.TargetWireKey(SelectedSpaceTarget)));
            SpaceHelm.SetManualControl(true);
            SpaceHelm.SetThrottle(0.45f);
            if(launchVisual.Cabin)
            {
                launchVisual.Cabin.SyncHatch(false);
                launchVisual.Cabin.BindHelm(SpaceHelm);
            }
            if(player.transform.parent!=launchVisual.ShuttleRoot)
                player.transform.SetParent(launchVisual.ShuttleRoot,true);
            launchVisual.SyncSpaceFlightOffset(1450f,0.01f);
            SetOuterSpaceSurfaceHidden(true);
            if(immediateHyperjump)
                SpaceHelm.StartHyperjump(SpaceHelm.TargetBody);
            spaceVisuals=SpaceFlightVisuals.Ensure(null);
            spaceVisuals.SetOpenSpaceFlightActive(SpaceHelm,launchVisual.ShuttleRoot,player.transform);
            Session.Notice="РУЧНОЙ ШТУРВАЛ ОТКРЫТОГО КОСМОСА АКТИВЕН! Вся Солнечная система, 7 звёздных систем и Галактика Андромеды M31 перед вами!";
        }

        public void SetOuterSpaceSurfaceHidden(bool hidden)
        {
            if(!world)return;
            Transform shuttle=launchVisual?launchVisual.ShuttleRoot:null;
            foreach(var r in world.GetComponentsInChildren<Renderer>(true))
            {
                if(shuttle&&r.transform.IsChildOf(shuttle))continue;
                r.enabled=!hidden;
            }
            foreach(var l in world.GetComponentsInChildren<Light>(true))
            {
                if(shuttle&&l.transform.IsChildOf(shuttle))continue;
                l.enabled=!hidden;
            }
        }

        void UpdateOpenSpaceFlight(float dt)
        {
            if(!SpaceHelm.Active||!launchVisual)return;
            var kb=Keyboard.current;
            Vector2 stick=launchVisual.Cabin&&launchVisual.Cabin.InteractiveRig?launchVisual.Cabin.InteractiveRig.StickDeflection:Vector2.zero;
            float pitchIn=stick.y+(kb!=null?((kb.upArrowKey.isPressed?1f:0f)-(kb.downArrowKey.isPressed?1f:0f)):0f);
            float yawIn=stick.x+(kb!=null?((kb.rightArrowKey.isPressed||kb.dKey.isPressed?1f:0f)-(kb.leftArrowKey.isPressed||kb.aKey.isPressed?1f:0f)):0f);
            float rollIn=kb!=null?((kb.qKey.isPressed?1f:0f)-(kb.eKey.isPressed?1f:0f)):0f;
            if(kb!=null)
            {
                if(kb.wKey.isPressed){SpaceHelm.SetThrottle(SpaceHelm.Throttle+dt*0.65f);SpaceHelm.RetroBrake=false;}
                if(kb.sKey.isPressed)SpaceHelm.SetThrottle(SpaceHelm.Throttle-dt*0.85f);
                SpaceHelm.RetroBrake=kb.spaceKey.isPressed||kb.xKey.isPressed;
                SpaceHelm.ResetTimeWarp(); // Direct continuous flight — zero Time Warp speedups!
            }
            SpaceHelm.Boost=kb!=null&&kb.leftShiftKey.isPressed;
            SpaceHelm.Step(dt,Mathf.Clamp(pitchIn,-1f,1f),Mathf.Clamp(yawIn,-1f,1f),Mathf.Clamp(rollIn,-1f,1f));
            launchVisual.SyncSpaceFlightOffset(1450f,SpaceHelm.InHyperjump?0.05f:0.008f*SpaceHelm.Throttle);
            if(launchVisual.Cabin&&launchVisual.Cabin.InteractiveRig)
                launchVisual.Cabin.InteractiveRig.RefreshVisualPoses(dt);
            spaceVisuals=SpaceFlightVisuals.Ensure(null);
            spaceVisuals.SetOpenSpaceFlightActive(SpaceHelm,launchVisual.ShuttleRoot,player?player.transform:null);
            EvaluateStoryRadio();
        }

        void TryLandFromOpenSpace()
        {
            var body=CelestialAtlas.GetBody(SpaceHelm.TargetBody);
            if(!SpaceHelm.InLandingRange)
            {
                if(SpaceHelm.StartHyperjump(SpaceHelm.TargetBody))
                    Session.Notice="Гиперпрыжок к «"+body.NameRu+"» для выхода на посадочную орбиту!";
                return;
            }
            if(body.IsDockableStation)
            {
                SpaceHelm.Disengage();
                SetOuterSpaceSurfaceHidden(false);
                CompleteOfflineStationDocking();
                return;
            }
            if(!body.HasSurface)
            {
                Session.Notice="Газовый гигант / звезда «"+body.NameRu+"» не имеет твёрдой коры — выберите его спутник (Европа / Титан) или планету!";
                return;
            }
            var targetKind=SpaceFlightVisuals.TargetForBody(body.Id);
            SpaceHelm.Disengage();
            SetOuterSpaceSurfaceHidden(false);
            CompleteOfflinePlanetLanding(targetKind);
        }

        public void HandleSeatedShuttleInput(Keyboard keyboard)
        {
            if(keyboard==null||!Session)return;
            if(keyboard.oKey.wasPressedThisFrame&&!online)
            {
                if(SpaceHelm.Active)
                {
                    Session.Notice="Открытый космос активен: нажмите [J] для гиперпрыжка к цели и [L] для посадки/стыковки!";
                }
                else EnterOpenSpaceFlight(false);
                return;
            }
            if(SpaceHelm.Active)
            {
                if(keyboard.gKey.wasPressedThisFrame)
                {
                    var b=SpaceHelm.CycleTarget(1);
                    SyncTargetIdxFromAtlasBody(b.Id);
                    Session.Notice="Цель MFD: "+b.NameRu+" ("+CelestialAtlas.GetSystem(b.System).NameRu+")";
                }
                if(keyboard.jKey.wasPressedThisFrame)
                {
                    if(SpaceHelm.StartHyperjump(SpaceHelm.TargetBody))
                        Session.Notice="ГИПЕРПРЫЖОК → "+CelestialAtlas.GetBody(SpaceHelm.TargetBody).NameRu+"!";
                }
                if(keyboard.vKey.wasPressedThisFrame)
                {
                    var nextGal=SpaceHelm.CurrentGalaxy==GalaxyId.MilkyWay?GalaxyId.Andromeda:GalaxyId.MilkyWay;
                    SpaceHelm.JumpToGalaxy(nextGal);
                    SyncTargetIdxFromAtlasBody(SpaceHelm.TargetBody);
                    Session.Notice="МЕЖГАЛАКТИЧЕСКИЙ ПРЫЖОК → "+CelestialAtlas.GalaxyNameRu(nextGal)+"!";
                }
                if(keyboard.lKey.wasPressedThisFrame)
                    TryLandFromOpenSpace();
                return;
            }
            if(!InMoonFlight)
            {
                if(keyboard.gKey.wasPressedThisFrame)
                {
                    selectedTargetIdx=(selectedTargetIdx+1)%TargetCycleOrder.Length;
                    if(SelectedSpaceTarget!=SpaceTargetKind.OrbitalStation&&world&&SpaceFlightVisuals.DimensionForTarget(SelectedSpaceTarget)==world.Dimension)
                        selectedTargetIdx=(selectedTargetIdx+1)%TargetCycleOrder.Length;
                    SpaceHelm.SelectTarget(CelestialAtlas.ParseWireKey(SpaceFlightVisuals.TargetWireKey(SelectedSpaceTarget)));
                    Session.Notice="Выбрана цель полёта: "+SpaceFlightVisuals.TargetNameRu(SelectedSpaceTarget)+" · Нажмите [R] или кнопку ПУСК в 3D-кабине!";
                }
                if(keyboard.rKey.wasPressedThisFrame)
                    RequestShuttlePreflight();
                return;
            }

            if(online)
            {
                HandleMoonFlightControls(keyboard);
                return;
            }

            // Offline full playable flight controls
            if(keyboard.tKey.wasPressedThisFrame)
            {
                offlineWarp30x=!offlineWarp30x;
                Session.Notice=offlineWarp30x?"Ускорение времени 30× включено":"Реальное время полёта 1×";
            }
            if(offlineNav==null||offlineFlightCountdown>0)return;

            if(keyboard.pKey.wasPressedThisFrame&&offlineNav.Stage==FlightStage.Cruise)
            {
                offlineNav.SetAutopilot(!offlineNav.Autopilot);
                Session.Notice=offlineNav.Autopilot?"Автопилот включён":"Ручное управление курсом";
            }
            offlineRetroBurning=keyboard.spaceKey.isPressed;
            if(keyboard.lKey.wasPressedThisFrame)
            {
                if(SelectedSpaceTarget==SpaceTargetKind.OrbitalStation&&(offlineNav.Stage==FlightStage.Approach||offlineNav.Stage==FlightStage.LandingReady||offlineNav.Elapsed>120))
                {
                    CompleteOfflineStationDocking();
                    return;
                }
                if(offlineNav.Land())
                {
                    CompleteOfflinePlanetLanding(SelectedSpaceTarget);
                    return;
                }
                Session.Notice=offlineNav.LandingBurn<10.0
                    ? $"Удерживайте [Space] для тормозного импульса ({offlineNav.LandingBurn:0.0} / 10.0 с) перед посадкой!"
                    : "Подлетаем к точке посадки — включите [T] Варп 30× для быстрого подлёта!";
            }
        }

        public void RequestShuttlePreflight()
        {
            if(InMoonFlight||!ShuttleSeated||!Session||!world)return;
            var target=SelectedSpaceTarget;
            if(target!=SpaceTargetKind.OrbitalStation&&(int)target==(int)world.Dimension)
            {
                selectedTargetIdx=(selectedTargetIdx+1)%TargetCycleOrder.Length;
                target=SelectedSpaceTarget;
            }

            if(!online)
            {
                BeginOfflineLaunch(target);
                return;
            }

            if(!client||!client.Connected)return;
            client.Send(new NetPacket{type="space_flight_ready",protocol=2});
            if(target==SpaceTargetKind.OrbitalStation)
                client.Send(new NetPacket{type="space_dock_station"});
            else
                client.Send(new NetPacket{type="space_ignite",target=SpaceFlightVisuals.TargetWireKey(target)});
            Session.Notice="Запрос пуска к цели «"+SpaceFlightVisuals.TargetNameRu(target)+"» отправлен серверу (нужны оба пилота в креслах)…";
        }

        void BeginOfflineLaunch(SpaceTargetKind target)
        {
            if(!launchVisual||!player||!world||!Session)return;
            Session.Checkpoint();
            // Extrasolar, outer Solar, or intergalactic targets launch straight into 6-DOF Open Space + Hyperjump!
            if((int)target>=6)
            {
                EnterOpenSpaceFlight(true);
                return;
            }
            SpaceDestination originDest=(SpaceDestination)Mathf.Clamp((int)world.Dimension,0,4);
            SpaceVector startVec=SpaceMap.Center(originDest);
            SpaceVector destVec=target==SpaceTargetKind.OrbitalStation
                ? OrbitalStation.Port
                : SpaceMap.Center((SpaceDestination)(int)target);
            double tripSeconds=target==SpaceTargetKind.OrbitalStation?240.0:
                               target==SpaceTargetKind.Moon||world.Dimension==Dimension.Moon?2004.0:
                               2400.0;

            offlineNav=new SpaceNavigation(startVec);
            if(!offlineNav.Ignite(destVec,tripSeconds))
            {
                destVec=startVec+new SpaceVector(384400000.0,0,0);
                offlineNav.Ignite(destVec,2004.0);
            }
            offlineNav.SetAutopilot(true);
            offlineFlightCountdown=3.5;
            offlineWarp30x=false;
            offlineFlightActive=true;
            offlineShuttleSeat=true;
            CockpitMouseMode=false;
            if(launchVisual.Cabin)launchVisual.Cabin.SyncHatch(false);
            if(player.transform.parent!=launchVisual.ShuttleRoot)
                player.transform.SetParent(launchVisual.ShuttleRoot,true);
            spaceVisuals=SpaceFlightVisuals.Ensure(null);
            Session.Notice="Обратный отсчёт запущен! Цель: "+SpaceFlightVisuals.TargetNameRu(target)+" · Непрерывный полёт в реальном времени ([O] Штурвал космоса)!";
        }

        void UpdateOfflineSpaceFlight(float dt)
        {
            if(!offlineFlightActive||offlineNav==null||!launchVisual)return;
            if(offlineFlightCountdown>0)
            {
                offlineFlightCountdown-=dt;
                launchVisual.SyncSpaceFlightOffset(0f,0.035f);
                spaceVisuals=SpaceFlightVisuals.Ensure(null);
                spaceVisuals.SetSpaceFlightActive(true,SelectedSpaceTarget,world.Dimension,FlightStage.Ascent,0.0,10.0,0.0,false,launchVisual.ShuttleRoot,player?player.transform:null);
                if(offlineFlightCountdown<=0)
                    Session.Notice="ПОЕХАЛИ! Отрыв от стартового стола — выход на опорную орбиту!";
                return;
            }

            double simDt=dt*(offlineWarp30x?30.0:1.0);
            var kb=Keyboard.current;
            double vSteer=kb!=null?((kb.upArrowKey.isPressed?0.35:0)-(kb.downArrowKey.isPressed?0.35:0)):0;
            double hSteer=kb!=null?((kb.rightArrowKey.isPressed?0.35:0)-(kb.leftArrowKey.isPressed?0.35:0)):0;
            SpaceVector manualDir=new SpaceVector(1.0,vSteer,hSteer).Normalized;

            // Step in small chunks so stage boundaries & braking integrate accurately under 30x warp
            int subSteps=offlineWarp30x?10:1;
            double stepDt=simDt/subSteps;
            bool allowAutoAssistBurn=offlineRetroBurning||offlineNav.Stage==FlightStage.Approach||offlineNav.Stage==FlightStage.LandingReady;
            for(int i=0;i<subSteps;i++)
            {
                if(offlineNav.Stage==FlightStage.Approach&&offlineNav.Autopilot)
                    offlineNav.Step(stepDt,manualDir,offlineRetroBurning?1.0:0.45);
                else if(offlineNav.Stage==FlightStage.LandingReady&&offlineRetroBurning&&offlineNav.LandingBurn<10.5)
                    offlineNav.Step(stepDt,manualDir,1.0);
                else
                    offlineNav.Step(stepDt,manualDir,offlineRetroBurning?1.0:0.0);
            }

            double p01=Math.Max(0.0,Math.Min(1.0,offlineNav.Elapsed/Math.Max(1.0,offlineNav.Duration)));
            float alt=p01<0.32?Mathf.Lerp(0f,1350f,(float)(p01/0.32)):
                      p01>0.88?Mathf.Lerp(1350f,18f,(float)((p01-0.88)/0.12)):1350f;
            float shake=offlineNav.Stage==FlightStage.Ascent?0.045f:(offlineRetroBurning?0.025f:0.006f);
            launchVisual.SyncSpaceFlightOffset(alt,shake);

            double speed=offlineNav.Stage==FlightStage.LandingReady?12.0:(offlineNav.Stage==FlightStage.Approach?840.0:7850.0);
            spaceVisuals=SpaceFlightVisuals.Ensure(null);
            spaceVisuals.SetSpaceFlightActive(true,SelectedSpaceTarget,world.Dimension,offlineNav.Stage,p01,alt,speed,allowAutoAssistBurn&&offlineRetroBurning,launchVisual.ShuttleRoot,player?player.transform:null);
        }

        void CompleteOfflinePlanetLanding(SpaceTargetKind target)
        {
            if(!world||!player||!Session)return;
            Dimension targetDim=SpaceFlightVisuals.DimensionForTarget(target);
            player.transform.SetParent(null,true);
            offlineFlightActive=false;
            SpaceHelm.Disengage();
            CockpitMouseMode=false;
            offlineNav=null;
            offlineRetroBurning=false;
            dockedAtStation=false;
            player.ZeroGActive=false;

            if(activePlanetBoss){Destroy(activePlanetBoss.gameObject);activePlanetBoss=null;}
            if(spaceVisuals)spaceVisuals.DespawnOrbitalStationInterior();

            // Persist all blocks built on previous planet before switching worlds!
            Session.Data.blocks=world.ExportAllMergedEdits(Session.Data.blocks);
            Session.Data.activeDimension=(int)targetDim;
            Session.Data.dockedAtStation=false;
            dimension=(int)targetDim;

            if(launchVisual){Destroy(launchVisual.gameObject);launchVisual=null;}
            world.SwitchPlanet(targetDim,Session.Data.blocks);

            Vector3Int landingSite=targetDim==Dimension.Lestingia&&Session.Data.launchPlaced
                ? LaunchSiteVisual.OfflineSite(world.Seed,Session.Data.launchX,Session.Data.launchZ,Dimension.Lestingia)
                : targetDim==Dimension.Lestingia
                    ? LaunchSiteVisual.OfflineSite(world.Seed,LaunchSiteVisual.PadX,LaunchSiteVisual.PadZ,Dimension.Lestingia)
                    : LaunchSiteVisual.OfflineSite(world.Seed,0,0,targetDim);

            sharedLaunchSite=landingSite;
            launchVisual=LaunchSiteVisual.Create(world.transform,landingSite,world.Seed,targetDim);
            launchVisual.Cabin.SyncHatch(true);
            launchVisual.Cabin.BindHelm(SpaceHelm);
            hatchOpen=true;

            double sx,sy,sz;
            VerticalShuttleWalk.Seat(0,out sx,out sy,out sz);
            Vector3 cockpitWorld=launchVisual.Cabin.transform.TransformPoint(new Vector3((float)sx,(float)sy+0.25f,(float)sz-0.46f));
            player.TeleportTo(cockpitWorld,true);
            offlineShuttleSeat=true;
            SetCursor(true);

            spaceVisuals=SpaceFlightVisuals.Ensure(null);
            spaceVisuals.SetSpaceFlightActive(false,target,targetDim,FlightStage.Landed,1.0,0.0,0.0,false,launchVisual.ShuttleRoot,player.transform);
            spaceVisuals.ApplyPlanetAtmosphereAndGravity(targetDim,player,Camera.main);
            SpawnBossIfApplicable(targetDim);

            Session.Checkpoint();
            EvaluateStoryRadio();
            Session.Notice="Посадка на «"+dimensions[(int)targetDim]+"» успешна! Нажмите [E], чтобы встать с кресла и выйти через шлюз!";
        }

        void CompleteOfflineStationDocking()
        {
            if(!player||!world||!Session)return;
            player.transform.SetParent(null,true);
            offlineFlightActive=false;
            offlineNav=null;
            offlineShuttleSeat=false;
            dockedAtStation=true;
            player.ZeroGActive=false;
            if(activePlanetBoss){Destroy(activePlanetBoss.gameObject);activePlanetBoss=null;}

            spaceVisuals=SpaceFlightVisuals.Ensure(null);
            Vector3 stationOrigin=new Vector3(0f,320f,0f);
            spaceVisuals.SpawnOrbitalStationInterior(world.Seed,stationOrigin);
            spaceVisuals.ApplyPlanetAtmosphereAndGravity(world.Dimension,player,Camera.main,true);
            player.TeleportTo(spaceVisuals.StationSpawnWorldPos,true);
            Session.Data.dockedAtStation=true;
            Session.Checkpoint();
            Session.Notice="Стыковка с Орбитальной станцией завершена! Подойдите к астронавтам и нажмите [E] для обмена (Z — 0g)";
        }

        public bool TryInteractStation()
        {
            if(!dockedAtStation||!spaceVisuals||!player||!Session)return false;
            int role=spaceVisuals.GetNearestAstronautRole(player.transform.position,4.2f);
            if(role>=0)
            {
                paused=true;
                SetCursor(false);
                ui.ShowStationTrade(role);
                return true;
            }
            if(Vector3.Distance(player.transform.position,spaceVisuals.StationReturnHatchWorldPos)<3.6f)
            {
                if(online&&client&&client.Connected)
                {
                    client.Send(new NetPacket{type="space_depart_station"});
                    return true;
                }
                DepartStationToShuttle();
                return true;
            }
            return false;
        }

        void DepartStationToShuttle()
        {
            dockedAtStation=false;
            if(player)player.ZeroGActive=false;
            if(spaceVisuals)spaceVisuals.DespawnOrbitalStationInterior();
            if(world&&player)
            {
                if(!sharedLaunchSite.HasValue)
                    sharedLaunchSite=LaunchSiteVisual.OfflineSite(world.Seed,LaunchSiteVisual.PadX,LaunchSiteVisual.PadZ,world.Dimension);
                if(!launchVisual)
                    launchVisual=LaunchSiteVisual.Create(world.transform,sharedLaunchSite.Value,world.Seed,world.Dimension);
                double sx,sy,sz;
                VerticalShuttleWalk.Seat(0,out sx,out sy,out sz);
                Vector3 seatWorld=launchVisual.Cabin.transform.TransformPoint(new Vector3((float)sx,(float)sy+0.25f,(float)sz-0.46f));
                player.TeleportTo(seatWorld,true);
                offlineShuttleSeat=true;
                spaceVisuals.ApplyPlanetAtmosphereAndGravity(world.Dimension,player,Camera.main);
                Session.Data.dockedAtStation=false;
                Session.Checkpoint();
                Session.Notice="Вы вернулись в кабину шаттла на орбите. Выберите любую планету [G] и нажмите [R] для пуска!";
            }
        }

        public void ExecuteStationTrade(int astronautRole)
        {
            if(!Session||!dockedAtStation)return;
            if(online&&client&&client.Connected)
            {
                int reqId=stationTradeRequestSeq++;
                client.Send(new NetPacket{type="station_trade",role=astronautRole,requestId=reqId});
                Session.Notice="Запрос обмена отправлен на сервер орбитальной станции…";
                return;
            }
            var dock=spaceVisuals?spaceVisuals.ActiveStationDock:null;
            SpaceVector nearPos=StationAstronauts.Position(astronautRole);
            if(dock!=null&&StationAstronauts.TryTrade(dock,true,astronautRole,nearPos,Session.Inventory,out var updated))
            {
                Session.Data.player.inventory=updated;
                Session.Inventory.revision++;
                var offer=StationAstronauts.Offer(astronautRole);
                Session.Notice="Обмен выполнен: получено "+offer.OutputCount+" × "+Items.Get(offer.Output).Name+"!";
                Session.Checkpoint();
            }
            else
            {
                Session.Notice="Недостаточно ресурсов в инвентаре или нет свободного слота.";
            }
        }

        void SpawnBossIfApplicable(Dimension dim)
        {
            if(activePlanetBoss){Destroy(activePlanetBoss.gameObject);activePlanetBoss=null;}
            if(!world||!Session)return;
            bool defeated=(Session.Data.defeatedPlanetBossMask&(1<<(int)dim))!=0;
            Vector3 anchor=sharedLaunchSite.HasValue?(Vector3)sharedLaunchSite.Value:Vector3.zero;
            activePlanetBoss=PlanetaryBossController.SpawnForPlanet(world.transform,dim,world,world.Seed,defeated,anchor);
        }

        public bool TryAttackPlanetaryBoss(Ray aimRay)
        {
            if(Session&&InRadiaDimension&&Tech)
            {
                int held=Session.Inventory.Active.count>0?Session.Inventory.Active.item:0;
                int radiaDmg=Weapons.Has(held)?Weapons.Get(held).Damage:(Items.IsPick(held)?11:14);
                if(Tech.Sim.TryHitBoss(aimRay,radiaDmg,out string bossMsg))
                {
                    Session.Notice=bossMsg;
                    return true;
                }
            }
            if(!activePlanetBoss||activePlanetBoss.Defeated||!Session)return false;
            int activeItem=Session.Inventory.Active.count>0?Session.Inventory.Active.item:0;
            int weaponDmg=Weapons.Has(activeItem)?Weapons.Get(activeItem).Damage:(Items.IsPick(activeItem)?9:12);
            if(!activePlanetBoss.TryHitBoss(aimRay,7.5f,weaponDmg,out bool killedNow,out var drops))return false;
            if(killedNow)
            {
                Session.Data.defeatedPlanetBossMask|=1<<(int)activePlanetBoss.BossDimension;
                foreach(var d in drops)
                {
                    if(!Session.Inventory.Add(d.item,d.count)&&Session.Living)
                        Session.Living.SpawnDrop(d.item,d.count,0,activePlanetBoss.ArenaPosition+Vector3.up*1.5f,Vector3.up*2.5f);
                }
                Session.Notice="ПОБЕДА! "+activePlanetBoss.BossName+" повержен! Получены редкие алмазы и кристаллы!";
                activePlanetBoss=null;
                Session.Checkpoint();
            }
            else
            {
                Session.Notice=$"Удар по боссу «{activePlanetBoss.BossName}»! Осталось HP: {activePlanetBoss.Health}/{activePlanetBoss.MaxHealth}";
            }
            return true;
        }

        public void QuickPlaceCosmodromeNearPlayer()
        {
            if(!world||!player||!Session||online)return;
            int px=Mathf.Clamp(Mathf.RoundToInt(player.transform.position.x)+20,-280,280);
            int pz=Mathf.Clamp(Mathf.RoundToInt(player.transform.position.z)+110,-220,280);
            var site=LaunchSiteVisual.OfflineSite(world.Seed,px,pz,world.Dimension);
            SetLaunchSite(site);
            if(world.Dimension==Dimension.Lestingia)
            {
                Session.Data.launchPlaced=true;
                Session.Data.launchX=px;
                Session.Data.launchZ=pz;
            }
            Session.Checkpoint();
            Session.Notice="Космодром развёрнут прямо перед вами! Поднимитесь по эстакаде к лифту шаттла.";
        }

        public void HandleMoonFlightControls(Keyboard keyboard)
        {
            if(keyboard==null||!InMoonFlight||!flightSnapshot.pilot||!client||!client.Connected)return;
            if(keyboard.tKey.wasPressedThisFrame)
                client.Send(new NetPacket{type="space_warp",warp=true});
            if(keyboard.pKey.wasPressedThisFrame&&flightSnapshot.phase=="cruise")
                client.Send(new NetPacket{type="space_autopilot",on=!flightSnapshot.autopilot});
            if(keyboard.lKey.wasPressedThisFrame&&(flightSnapshot.phase=="landing-ready"||flightSnapshot.phase=="landed"))
            {
                if(flightSnapshot.phase=="landed")
                    client.Send(new NetPacket{type="space_complete_landing"});
                else
                {
                    client.Send(new NetPacket{type="space_land"});
                    client.Send(new NetPacket{type="space_complete_landing"});
                }
            }
            if(Time.unscaledTime-lastFlightControl<.35f)return;
            bool manual=keyboard.leftArrowKey.isPressed||keyboard.rightArrowKey.isPressed||
                keyboard.upArrowKey.isPressed||keyboard.downArrowKey.isPressed;
            bool burn=(flightSnapshot.phase=="approach"||flightSnapshot.phase=="landing-ready")&&keyboard.spaceKey.isPressed;
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

        void ApplyMoonFlight(NetSpaceMission mission)
        {
            if(mission==null||!mission.active||!sharedLaunchSite.HasValue||!player||!world)return;
            int claimed=Array.IndexOf(shuttleSeats,selfId);
            if(claimed>=0)flightSeatIndex=claimed;
            if(flightSeatIndex<0||flightSeatIndex>=2)return;
            if(!launchVisual)
                launchVisual=LaunchSiteVisual.Create(world.transform,sharedLaunchSite.Value,world.Seed,world.Dimension);
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

            double p01=mission.seconds>0?Math.Max(0d,Math.Min(1d,mission.elapsed/mission.seconds)):0d;
            FlightStage st=mission.phase=="ascent"?FlightStage.Ascent:
                           mission.phase=="cruise"?FlightStage.Cruise:
                           mission.phase=="approach"?FlightStage.Approach:
                           mission.phase=="landed"?FlightStage.Landed:FlightStage.LandingReady;
            spaceVisuals=SpaceFlightVisuals.Ensure(null);
            spaceVisuals.SetSpaceFlightActive(true,SpaceFlightVisuals.ParseWireKey(mission.to),world.Dimension,st,p01,launchVisual.ShuttleRoot.localPosition.y,5500.0,mission.landingBurn>0,launchVisual.ShuttleRoot,player.transform);

            if(Session&&previous!=mission.phase)
                Session.Notice="Кооп-корабль · "+mission.phase+" · "+mission.elapsed.ToString("0")+" / "+mission.seconds.ToString("0")+" с";
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
                if(Mathf.Abs(local.x-(float)sx)>1.1f||Mathf.Abs(local.z-(float)sz)>1.1f||
                   Mathf.Abs(local.y-(float)sy)>1.4f)continue;
                if(Time.unscaledTime-lastLiftRequest<.4f)return true;
                if(!string.IsNullOrEmpty(shuttleSeats[i])&&shuttleSeats[i]!=selfId)
                {Session.Notice="Это место занято вторым игроком";return true;}
                string command=shuttleSeats[i]==selfId?"stand":"sit";
                lastLiftRequest=Time.unscaledTime;
                client.Send(new NetPacket{type="space_seat",action=command,index=i});
                if(command=="sit")
                {
                    cabin.BindHelm(SpaceHelm);
                    player.TeleportTo(cabin.transform.TransformPoint(new Vector3((float)sx,(float)sy+0.25f,(float)sz-0.46f)));
                    client.Send(new NetPacket{type="space_flight_ready",protocol=2});
                }
                Session.Notice=command=="sit"?"Вы в кресле пилота · Наводите мышь на 3D-тумблеры, РУД, РУС и экран MFD (ЛКМ / колесо) · [E] Встать":"Вы встали с кресла";
                return true;
            }
            if(Mathf.Abs(local.x+9)<3.5f&&Mathf.Abs(local.z-3)<1.5f&&Mathf.Abs(local.y-28)<2.2f)
            {
                if(Time.unscaledTime-lastLiftRequest<.4f)return true;
                lastLiftRequest=Time.unscaledTime;
                client.Send(new NetPacket{type="space_hatch",open=!hatchOpen});
                Session.Notice=hatchOpen?"Запрос закрытия люка":"Запрос открытия люка";
                return true;
            }
            if(Mathf.Abs(local.x-VerticalShuttleWalk.LiftX)>1.6f||Mathf.Abs(local.z-3)>1.6f)return false;
            if(Time.unscaledTime-lastLiftRequest<.4f)return true;
            if(cabin.Lift.IsMoving){Session.Notice="Лифт движется — дождитесь остановки";return true;}
            bool ground=Mathf.Abs(local.y-VerticalShuttleWalk.LiftGroundY)<1.25f;
            bool top=Mathf.Abs(local.y-(VerticalShuttleWalk.LiftGroundY+VerticalShuttleWalk.LiftTravel))<1.25f;
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
                action=="exit"?"Выход с лифта — проходите пешком":"Возврат пустого лифта";
            return true;
        }

        // Works across ALL offline modes (Survival, Hardcore, Creative, Story)!
        bool TryInteractOfflineLaunch()
        {
            if(!player||!launchVisual||!launchVisual.Cabin||!Session)return false;
            var cabin=launchVisual.Cabin;
            var local=cabin.transform.InverseTransformPoint(player.transform.position);
            for(int i=0;i<VerticalShuttleWalk.SeatCount;i++)
            {
                double sx,sy,sz;VerticalShuttleWalk.Seat(i,out sx,out sy,out sz);
                if(Mathf.Abs(local.x-(float)sx)>1.15f||Mathf.Abs(local.z-(float)sz)>1.15f||Mathf.Abs(local.y-(float)sy)>1.45f)continue;
                offlineShuttleSeat=!offlineShuttleSeat;
                if(offlineShuttleSeat)
                {
                    cabin.BindHelm(SpaceHelm);
                    player.TeleportTo(cabin.transform.TransformPoint(new Vector3((float)sx,(float)sy+0.25f,(float)sz-0.46f)));
                }
                Session.Notice=offlineShuttleSeat
                    ? "Вы в кресле пилота! Наводите прицел/мышь на 3D-тумблеры, РУД, штурвал и бортовой экран MFD (ЛКМ / колесо мыши) · [E] Встать"
                    : "Вы встали с кресла пилота";
                return true;
            }
            if(Mathf.Abs(local.x+9)<3.5f&&Mathf.Abs(local.z-3)<1.5f&&Mathf.Abs(local.y-28)<2.2f)
            {cabin.SyncHatch(!cabin.HatchOpen);Session.Notice=cabin.HatchOpen?"Шлюзовой люк открыт — проходите к шахте кабины (Space — подъём по лестнице)":"Шлюзовой люк герметизирован";return true;}
            if(Mathf.Abs(local.x-VerticalShuttleWalk.LiftX)>1.6f||Mathf.Abs(local.z-3)>1.6f)return false;
            if(cabin.Lift.IsMoving){Session.Notice="Лифт движется";return true;}
            bool ground=Mathf.Abs(local.y-VerticalShuttleWalk.LiftGroundY)<1.25f;
            bool top=Mathf.Abs(local.y-(VerticalShuttleWalk.LiftGroundY+VerticalShuttleWalk.LiftTravel))<1.25f;
            if(!ground&&!top)return false;
            if(cabin.Lift.Occupied)
            {cabin.Lift.Exit();Session.Notice="Выйдите с платформы к люку шаттла";return true;}
            bool ok=ground&&cabin.Lift.State==ElevatorState.Ground?cabin.RequestUp(player):
                top&&cabin.Lift.State==ElevatorState.Top?cabin.RequestDown(player):
                ground&&cabin.Lift.State==ElevatorState.Top&&cabin.RecallEmptyLift();
            Session.Notice=ok?"Лифт поднимается к шлюзу орбитального корабля…":"Встаньте в центр платформы лифта";
            return true;
        }

        public bool IsConnecting=>busy;
        public string LastError=>lastError;
        public string WorldSeed=>seedText;
        public string PlayerName {get=>playerName;set=>playerName=value;}
        public string RoomCode {get=>code;set=>code=value.Trim();}
        public string WorldLabel=>world?dimensions[Mathf.Clamp((int)world.Dimension,0,dimensions.Length-1)]+"  /  SEED "+world.Seed+
            (sharedLaunchSite.HasValue?"  ·  Космодром X="+sharedLaunchSite.Value.x+" Z="+sharedLaunchSite.Value.z:""):"";
        public int PlayerCount=>avatars.Count+1;
        public int ChunkCount=>world?world.LoadedChunks:0;
        public bool WorldReady=>world&&world.Ready;
        public bool LaunchPlacementArmed=>!string.IsNullOrEmpty(launchBlueprint);
        public void ArmLaunchPlacement(string blueprint)
        {
            if(Session==null||IsSurfacePreview||(blueprint!="pad"&&blueprint!="shuttle"))return;
            launchBlueprint=blueprint;
            Session.Notice="Чертёж космодрома: ПКМ по земле в начале будущей эстакады (или нажмите F9).";
        }
        public void PlaceLaunchAt(Vector3Int ground)
        {
            if(!LaunchPlacementArmed||!Session||!world||!world.Ready||IsSurfacePreview)return;
            int natural=TerrainGenerator.Height(ground.x,ground.z,world.Seed,world.Dimension);
            if(Mathf.Abs(ground.y-natural)>3||Mathf.Abs(player.transform.position.y-(natural+1))>7)
            {Session.Notice="Выберите землю вблизи персонажа, не постройку и не дерево";return;}
            int x=ground.x+20,z=ground.z+110;
            if(x < -320||x > 320||z < -260||z > 320)
            {Session.Notice="Недостаточно места для платформы и эстакады: выберите другое место";return;}
            if(online)
            {
                if(!client||!client.Connected){Session.Notice="Нет соединения с комнатой";return;}
                client.Send(new NetPacket{type="space_site_place",action=launchBlueprint,x=ground.x,y=ground.y,z=ground.z});
                Session.Notice="Сервер проверяет точку постройки…";
            }
            else
            {
                var site=LaunchSiteVisual.OfflineSite(world.Seed,x,z,world.Dimension);
                SetLaunchSite(site);
                if(world.Dimension==Dimension.Lestingia){Session.Data.launchPlaced=true;Session.Data.launchX=x;Session.Data.launchZ=z;}
                Session.Checkpoint();Session.Notice="Космодром установлен! Идите по эстакаде к лифту шаттла (G — цель, R — пуск).";
            }
            launchBlueprint=null;
        }
        void SetLaunchSite(Vector3Int site)
        {
            if(launchVisual){Destroy(launchVisual.gameObject);launchVisual=null;}
            sharedLaunchSite=site;offlineShuttleSeat=false;
            hatchOpen=false;liftSnapshot=null;shuttleSeats=new string[2];
        }
        public bool HasSharedLaunchSite=>sharedLaunchSite.HasValue;
        public Vector3Int? LaunchSitePosition=>sharedLaunchSite;
        public ushort SelectedBlock=>Session&&world?SurvivalSession.PlaceableBlockForItem(Session.Inventory.Active.item,world.ContentVersion):(ushort)1;

        // === COZY BED SLEEP, STORY CODEX & SECRET "DREAM" ANOMALY ===
        public void ReadNextStoryCodex()
        {
            if(!Session)return;
            var entries=StoryChapters.Entries;
            storyCodexIndex=storyCodexIndex%entries.Length;
            var entry=entries[storyCodexIndex];
            storyCodexIndex=(storyCodexIndex+1)%entries.Length;
            Session.Notice=$"[{entry.ChapterRu} · {entry.TitleRu}] ({entry.SpeakerRu}): {entry.BodyRu}";
        }

        public string PauseStorySummary
        {
            get
            {
                string ch1 = Story && !string.IsNullOrEmpty(Story.ActiveObjective)
                    ? Story.ActiveObjective
                    : "Глава I · Тихий Брод: исследуйте деревню, святилище и постройте или найдите космодром.";
                string flightLine = SpaceHelm != null && SpaceHelm.Active
                    ? $"\n\nНАВИГАЦИЯ: {CelestialAtlas.GetBody(SpaceHelm.TargetBody).NameRu} · {CelestialAtlas.FormatDistanceRu(SpaceHelm.DistanceToTargetKm())} · {SpaceHelm.SpeedKmPerSec:0.0} км/с"
                    : (sharedLaunchSite.HasValue ? $"\n\nКосмодром: X {sharedLaunchSite.Value.x} · Z {sharedLaunchSite.Value.z} (лифт X {sharedLaunchSite.Value.x - 20})" : "");
                return ch1 + flightLine;
            }
        }

        void EvaluateStoryRadio()
        {
            if(!Session||!world)return;
            var g=SpaceHelm.Active?SpaceHelm.CurrentGalaxy:
                  (world.Dimension==Dimension.AndromedaPrime||world.Dimension==Dimension.Somnia)?GalaxyId.Andromeda:GalaxyId.MilkyWay;
            // Unlock journal chapters silently — story lives in ESC menu without cluttering the HUD!
            StoryChapters.EvaluateProgressionTransmission(
                world.Dimension,g,Session.Data.player.dreamVisits,
                Session.Inventory.Count(BlockRegistry.AndromedaCrystalId)>0,ref storyProgressionMask);
        }

        public void UseBed(Vector3Int cell)
        {
            if(!Session||!world||!player||InDreamDimension||InMoonFlight||ShuttleSeated)return;
            Session.Data.player.hasBedRespawn=true;
            Session.Data.player.bedX=cell.x;
            Session.Data.player.bedY=cell.y;
            Session.Data.player.bedZ=cell.z;
            player.SetCheckpoint(new Vector3(cell.x+0.5f,cell.y+1.15f,cell.z+0.5f));

            double hour=(Session.Data.clock/60.0)%24.0;
            bool night=hour>=18.5||hour<=5.5;
            if(!night)
            {
                Session.Checkpoint();
                Session.Notice="Точка возрождения установлена у кровати! Спать можно после заката (18:30–05:30).";
                return;
            }

            int dayIdx=(int)Math.Floor(Session.Data.clock/1440.0);
            var wSample=WeatherSample.At(Session.Data.seed,(int)world.Dimension,Session.Data.clock);
            boolenteredDream=Dream.TryEnterFromBedSleep(
                Session.Data.seed,dayIdx,Session.Vitals.stamina,Session.Vitals.comfort,wSample.Precipitation>0.35f,
                world.Dimension,cell.x+0.5f,cell.y+1.15f,cell.z+0.5f);
            if(boolenteredDream)
            {
                EnterDreamSequence();
                return;
            }

            if(!online)
                Session.Data.clock=(dayIdx+1)*1440.0+360.0; // Sunrise 06:00
            Session.Vitals.stamina=100f;
            Session.Vitals.comfort=Mathf.Max(Session.Vitals.comfort,85f);
            Session.Checkpoint();
            Session.Notice="Вы крепко проспали до рассвета в тёплой постели · Силы полностью восстановлены!";
        }

        bool boolenteredDream;
        public bool TryTriggerExhaustionDream(int seed,double clock,float stamina,float hunger)
        {
            if(!world||!player||!Session||InDreamDimension||InMoonFlight||ShuttleSeated||dockedAtStation)return false;
            if(!Dream.TryEnterFromExhaustionCollapse(seed,clock,stamina,hunger,world.Dimension,player.transform.position.x,player.transform.position.y,player.transform.position.z))
                return false;
            EnterDreamSequence();
            return true;
        }

        public void EnterDreamSequence()
        {
            if(!world||!player||!Session)return;
            if(ChatOpen)CloseChat();
            if(InventoryOpen){paused=false;ui.ShowHUD();}
            Session.ResetMining();
            Session.Data.blocks=world.ExportAllMergedEdits(Session.Data.blocks);
            Session.Data.player.dreamVisits++;
            if(online&&client&&client.Connected)
                client.Send(new NetPacket{type="dream_state",on=true,dream=true});
            foreach(var kv in avatars)if(kv.Value)kv.Value.SetActive(false);

            if(activePlanetBoss){Destroy(activePlanetBoss.gameObject);activePlanetBoss=null;}
            if(launchVisual){Destroy(launchVisual.gameObject);launchVisual=null;}

            world.SwitchPlanet(Dimension.Dream,Array.Empty<SavedBlock>());
            dimension=(int)Dimension.Dream;
            player.TeleportTo(new Vector3(Dream.SpawnX,Dream.SpawnY,Dream.SpawnZ),false);
            player.transform.rotation=Quaternion.identity;
            spaceVisuals=SpaceFlightVisuals.Ensure(null);
            spaceVisuals.ApplyPlanetAtmosphereAndGravity(Dimension.Dream,player,Camera.main);
            spaceVisuals.SyncDreamWatcher(Dream,player.transform);
            SetCursor(true);
            Session.Notice="СОННЫЙ ПАРАЛИЧ: грудь сдавило, тело не слушается… кто-то стоит в тумане впереди…";
        }

        void ExitDreamSequence()
        {
            if(!world||!player||!Session)return;
            Dimension wakeDim=Dream.ReturnDimension==Dimension.Dream?Dimension.Lestingia:Dream.ReturnDimension;
            Vector3 wakePos=new Vector3(Dream.ReturnX,Dream.ReturnY,Dream.ReturnZ);
            if(online&&client&&client.Connected)
                client.Send(new NetPacket{type="dream_state",on=false,dream=false});

            world.SwitchPlanet(wakeDim,Session.Data.blocks);
            dimension=(int)wakeDim;
            Session.Data.activeDimension=(int)wakeDim;
            if(!online&&wakeDim==Dimension.Lestingia)
            {
                int dayIdx=(int)Math.Floor(Session.Data.clock/1440.0);
                Session.Data.clock=(dayIdx+1)*1440.0+360.0;
            }
            Session.Vitals.stamina=Mathf.Max(Session.Vitals.stamina,75f);
            player.TeleportTo(wakePos,true);
            spaceVisuals=SpaceFlightVisuals.Ensure(null);
            spaceVisuals.SyncDreamWatcher(Dream,player.transform);
            spaceVisuals.ApplyPlanetAtmosphereAndGravity(wakeDim,player,Camera.main);
            if(sharedLaunchSite.HasValue&&!launchVisual)
                launchVisual=LaunchSiteVisual.Create(world.transform,sharedLaunchSite.Value,world.Seed,wakeDim);
            Session.Checkpoint();
            EvaluateStoryRadio();
            Session.Notice="Вы резко очнулись в холодном поту… Сон рассеялся, связь с реальностью восстановлена.";
        }

        void UpdateDreamAnomaly(float dt)
        {
            if(!Dream.IsActive||!player||!world)return;
            DreamPhase before=Dream.Phase;
            bool woke=Dream.Step(dt,player.transform.position,player.Camera?player.Camera.transform.forward:player.transform.forward);
            if(before==DreamPhase.SleepParalysis&&Dream.Phase==DreamPhase.NightmareWalk&&Session)
                Session.Notice="Оцепенение спало — вы можете идти! Ищите светлую арку пробуждения и смотрите на силуэт, чтобы он замирал!";
            if(spaceVisuals)spaceVisuals.SyncDreamWatcher(Dream,player.transform);
            if(woke)ExitDreamSequence();
        }

        public void EnterRadiaDimension()
        {
            if(!world||!player||!Session||InRadiaDimension)return;
            if(ChatOpen)CloseChat();
            if(InventoryOpen){paused=false;ui.ShowHUD();}
            Session.ResetMining();
            Session.Data.blocks=world.ExportAllMergedEdits(Session.Data.blocks);

            Tech.Sim.ReturnDimension=world.Dimension==Dimension.Dream?Dimension.Lestingia:world.Dimension;
            Tech.Sim.ReturnPosition=player.transform.position;
            Tech.Sim.ResetRadiaEncounter();

            if(activePlanetBoss){Destroy(activePlanetBoss.gameObject);activePlanetBoss=null;}
            if(launchVisual){Destroy(launchVisual.gameObject);launchVisual=null;}

            // Ensure player is never soft-locked inside Radia Abyss without a pickaxe & blade to harvest cores and fight the 3 bosses!
            if(Session.Inventory.Count(101)==0&&Session.Inventory.Count(150)==0&&Session.Inventory.Count(151)==0)
                Session.Inventory.Add(150,1);
            if(Session.Inventory.Count(152)==0&&Session.Inventory.Count(153)==0)
                Session.Inventory.Add(152,1);

            world.SwitchPlanet(Dimension.Radia,Array.Empty<SavedBlock>());
            dimension=(int)Dimension.Radia;
            player.TeleportTo(new Vector3(0.5f,50.2f,-11.5f),false);
            player.transform.rotation=Quaternion.identity;
            spaceVisuals=SpaceFlightVisuals.Ensure(null);
            var radiaCamera=Camera.main;
            if(radiaCamera)WorldAtmosphere.Configure(radiaCamera,(int)Dimension.Radia,Session.Data.clock);
            spaceVisuals.ApplyPlanetAtmosphereAndGravity(Dimension.Radia,player,radiaCamera);
            VisualSettings.EnsurePostProcessingVolume(1f,true,0f);
            SetCursor(true);
            Session.Notice="☢ ЛУЧЕВОЙ КОЛЛАПС -> ЧЕРЕНКОВСКИЙ ЗАТОН! Чтобы открыть шлюз плотины: добудьте 6 светящихся излучателей со дна и убейте всех 3 Боссов Спирали!";
        }

        public void ExitRadiaDimension()
        {
            if(!world||!player||!Session)return;
            Dimension retDim=Tech.Sim.ReturnDimension==Dimension.Radia?Dimension.Lestingia:Tech.Sim.ReturnDimension;
            Vector3 retPos=Tech.Sim.ReturnPosition;
            Tech.Sim.RadiationDose01=0f;
            Tech.Sim.PostEscapeImmunityTimer=90f;
            Tech.Sim.FalloutZones.Clear();

            world.SwitchPlanet(retDim,Session.Data.blocks);
            dimension=(int)retDim;
            Session.Data.activeDimension=(int)retDim;
            Session.Vitals.health=100f;
            Session.Vitals.stamina=100f;
            Session.Inventory.Add(Items.CherenkovCore,4);
            player.TeleportTo(retPos+Vector3.up*0.6f,true);
            spaceVisuals=SpaceFlightVisuals.Ensure(null);
            var returnCamera=Camera.main;
            if(returnCamera)WorldAtmosphere.Configure(returnCamera,(int)retDim,Session.Data.clock);
            spaceVisuals.ApplyPlanetAtmosphereAndGravity(retDim,player,returnCamera);
            if(sharedLaunchSite.HasValue&&!launchVisual&&retDim!=Dimension.Radia)
                launchVisual=LaunchSiteVisual.Create(world.transform,sharedLaunchSite.Value,world.Seed,retDim);
            VisualSettings.EnsurePostProcessingVolume(0f,false,0f);
            Session.Checkpoint();
            Session.Notice="★ ПРОРЫВ ГЕРМОШЛЮЗА ЗАВЕРШЁН! Вы вырвались из Черенковского Затона с полной дезактивацией и 4 Ядрами Черенкова!";
        }

        public bool TryUseIndustrialItemOrBlock(bool hasTarget,Vector3Int hitCell,Vector3Int placeCell)
        {
            if(!Session||!world||!player)return false;
            int activeId=Session.Inventory.Active.count>0?Session.Inventory.Active.item:0;

            if(hasTarget)
            {
                ushort targetBlock=world.GetBlock(hitCell.x,hitCell.y,hitCell.z);
                if(targetBlock==BlockRegistry.RadiaTerminalId)
                {
                    Session.Notice=Tech.Sim.TryActivateDamTerminal();
                    return true;
                }
                if(targetBlock==BlockRegistry.C4ChargeId)
                {
                    world.Edit(hitCell.x,hitCell.y,hitCell.z,BlockRegistry.AirId);
                    Tech.ArmExplosive(new Vector3(hitCell.x+0.5f,hitCell.y,hitCell.z+0.5f),false);
                    Session.Notice="⚡ Таймер заряда С-4 взведён (3,8 сек) — отойдите в укрытие!";
                    return true;
                }
                if(activeId==Items.ElectricLocomotive&&BlockRegistry.IsRail(targetBlock))
                {
                    Tech.SpawnElectricTrain(new Vector3(hitCell.x+0.5f,hitCell.y+0.35f,hitCell.z+0.5f));
                    if(!Session.Creative)Session.Inventory.Remove(Items.ElectricLocomotive,1);
                    Session.Notice="Электровоз «ЭР-Воксель» + грузовой вагон установлены на рельсы! Нажмите [F], чтобы вести поезд!";
                    return true;
                }
                if(activeId==Items.ThermonukeId)
                {
                    Tech.ArmExplosive(new Vector3(placeCell.x+0.5f,placeCell.y,placeCell.z+0.5f),true);
                    Session.Inventory.Remove(Items.ThermonukeId,1);
                    Session.Notice="☢ ВНИМАНИЕ! ОБЪЕКТ 666 ВЗВЕДЁН — ДО ДЕТОНАЦИИ 8,0 СЕКУНД! НЕМЕДЛЕННО ПОКИНЬТЕ ЗОНУ!";
                    return true;
                }
            }

            if(activeId==Items.QuadDrone)
            {
                Tech.DeployQuadDrone(player.transform.position+player.transform.forward*1.8f,Camera.main);
                if(!Session.Creative)Session.Inventory.Remove(Items.QuadDrone,1);
                Session.Notice="Квадрокоптер запущен в воздух! Нажмите [V] для переключения в живую FPV-камеру дрона!";
                return true;
            }
            if(activeId==Items.IndustrialRobot)
            {
                Vector3 rPos=hasTarget?new Vector3(placeCell.x+0.5f,placeCell.y,placeCell.z+0.5f):player.transform.position+player.transform.forward*2.0f;
                Tech.DeployIndustrialRobot(rPos);
                if(!Session.Creative)Session.Inventory.Remove(Items.IndustrialRobot,1);
                Session.Notice="Шагающий робот-автоматон «Титан-М» развёрнут: сопровождение и огневая поддержка активны!";
                return true;
            }
            if(activeId==Items.Dosimeter)
            {
                Session.Notice=$"Дозиметр «Припять»: фон {Tech.Sim.CurrentMillisievertsPerHour:0} мЗв/ч · накопленная доза {Tech.Sim.RadiationDose01*100f:0}% · энергосеть {Tech.Sim.Grid.GenerationKw:0} кВт";
                return true;
            }
            return false;
        }
        public string ModeName=>GameRules.ModeNames[Session?(int)Session.Mode:selectedMode];
        public LaunchSiteVisual LaunchComplexVisual=>launchVisual;
        public SpaceFlightVisuals SpaceVisuals=>spaceVisuals;
        public void ShowcaseStartFreshWorld()
        {
            if(world)Leave();
            IsSurfacePreview=false;
            selectedMode=0;
            seedText="2506";
            StartWorld(2506,0,false);
        }
        public void ShowcaseSpawnCosmodrome(int baseX,int baseZ)
        {
            if(!Session||!world)return;
            var site=LaunchSiteVisual.OfflineSite(world.Seed,baseX,baseZ,world.Dimension);
            SetLaunchSite(site);
            launchVisual=LaunchSiteVisual.Create(world.transform,site,world.Seed,world.Dimension);
            if(world.Dimension==Dimension.Lestingia){Session.Data.launchPlaced=true;Session.Data.launchX=baseX;Session.Data.launchZ=baseZ;}
        }
        public void ShowcaseOpenSpace(bool autoJumpAndromeda){EnterOpenSpaceFlight(autoJumpAndromeda);}
        public void ShowcaseEnterDream(){if(!Session||!WorldReady||Dream.IsActive)return;Dream.EnterDream(world.Dimension,player.transform.position.x,player.transform.position.y,player.transform.position.z);EnterDreamSequence();}
        public void ShowcaseExitDream(){if(Dream.IsActive){Dream.ForceWake();ExitDreamSequence();}}
        public void PlayOffline()
        {if(busy||world)return;IsSurfacePreview=false;int seed=(Array.IndexOf(Environment.GetCommandLineArgs(),"-lc-smoke-test")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-lc-showcase")>=0)?2506:Guid.NewGuid().GetHashCode();seedText=seed.ToString();StartWorld(seed,0,false);}
        public void ContinueOffline()
        {
            if(busy||world)return;resumeSave=LocalSave.Load();if(resumeSave==null){Fail("Сохранение повреждено");return;}
            if(resumeSave.campaign.runEnded){resumeSave=null;Fail("Хардкорный забег завершён. Создайте новый.");return;}
            IsSurfacePreview=false;selectedMode=resumeSave.mode;StartWorld(resumeSave.seed,resumeSave.activeDimension,false);
        }
        public void ToggleInventory()
        {
            if(InMoonFlight)
            {if(Session)Session.Notice="Во время активного манёвра экипаж пристёгнут в креслах";return;}
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
        void SetCursor(bool locked)
        {
            if(StoryLocked)locked=false;
            bool cursorVisible=!locked||CockpitMouseMode;
            Cursor.lockState=cursorVisible?CursorLockMode.None:CursorLockMode.Locked;
            Cursor.visible=cursorVisible;
            if(player)player.Active=locked||CockpitMouseMode;
        }
        public void TogglePause(){if(!world||Session&&(Session.Vitals.dead||Session.Data.campaign.runEnded))return;paused=!paused;SetCursor(!paused);}
        public void RequestEdit(Vector3Int cell,ushort block)
        {
            if(InDreamDimension){if(Session)Session.RejectEdit("Во сне нельзя взаимодействовать с миром");return;}
            if(Story&&Story.Enabled&&world.Dimension==Dimension.Lestingia&&StoryWorld.Protected(cell.x,cell.z)){Session.RejectEdit("Сюжетная постройка защищена; стройте за её пределами");return;}
            if(!world||!world.CanAcceptEdit(cell)){Fail("Достигнут лимит изменений мира");if(Session)Session.RejectEdit(lastError);return;}
            if(online)
            {if(client&&client.Connected)client.Send(new NetPacket {type="edit",x=cell.x,y=cell.y,z=cell.z,block=block});else {Fail("Нет соединения: изменения заблокированы");if(Session)Session.RejectEdit(lastError);}}
            else {world.Edit(cell.x,cell.y,cell.z,block);if(Session)Session.ConfirmEdit(cell);}
        }
        void StartWorld(int seed,int realm,bool multiplayer,PlayerSave savedPlayer=null,double clock=420,int contentVersion=9,StoryState story=null,long serverNow=0,NetLaunchSite approvedSite=null,Vector3? cockpitRestore=null)
        {
            if(multiplayer)IsSurfacePreview=false;
            realm=Mathf.Clamp(realm,0,14);
            bool restoreStation=!multiplayer&&resumeSave!=null&&resumeSave.dockedAtStation;
            world=new GameObject("World").AddComponent<VoxelWorld>();online=multiplayer;world.Initialize(seed,(Dimension)realm,multiplayer,resumeSave?.contentVersion??contentVersion);

            if(multiplayer&&approvedSite!=null&&approvedSite.active)
                sharedLaunchSite=new Vector3Int(approvedSite.x,approvedSite.y,approvedSite.z);
            else if(!multiplayer&&!IsSurfacePreview)
            {
                if(realm==0&&resumeSave!=null&&resumeSave.launchPlaced)
                    sharedLaunchSite=LaunchSiteVisual.OfflineSite(seed,resumeSave.launchX,resumeSave.launchZ,Dimension.Lestingia);
                else if(realm==0)
                    sharedLaunchSite=LaunchSiteVisual.OfflineSite(seed,LaunchSiteVisual.PadX,LaunchSiteVisual.PadZ,Dimension.Lestingia);
                else
                    sharedLaunchSite=LaunchSiteVisual.OfflineSite(seed,0,0,(Dimension)realm);
            }
            else sharedLaunchSite=null;

            var camera=Camera.main;camera.backgroundColor=new Color(.42f,.66f,.78f);
            RenderSettings.fogColor=camera.backgroundColor;
            var obj=new GameObject("Player");
            obj.transform.position=cockpitRestore??FindSpawn(seed,(Dimension)realm,world.ContentVersion);
            var controller=obj.AddComponent<CharacterController>();PlayerCapsule.Configure(controller);
            player=obj.AddComponent<FirstPersonController>();player.World=world;player.Game=this;player.Camera=camera;
            camera.transform.SetParent(obj.transform,false);camera.transform.localPosition=new Vector3(0,PlayerCapsule.EyeHeight,0);camera.nearClipPlane=.05f;camera.farClipPlane=256;
            dimension=realm;seedText=seed.ToString();
            WorldAtmosphere.Configure(camera,realm,resumeSave?.clock??clock);
            obj.AddComponent<FirstPersonHands>().Initialize(player,camera);
            world.Player=obj.transform;paused=false;SetCursor(true);busy=false;status="Генерируется мир…";lastError="";
            var sessionObject=new GameObject("Survival and weather");sessionObject.transform.SetParent(world.transform,false);
            Session=sessionObject.AddComponent<SurvivalSession>();Session.Initialize(this,world,player,selectedMode,savedPlayer,resumeSave,clock);resumeSave=null;
            Story=sessionObject.AddComponent<StoryController>();Story.Initialize(this,story,serverNow);
            obj.AddComponent<LocalPlayerAppearance>().Initialize(this);

            spaceVisuals=SpaceFlightVisuals.Ensure(null);
            spaceVisuals.ApplyPlanetAtmosphereAndGravity((Dimension)realm,player,camera);
            if(restoreStation)
                CompleteOfflineStationDocking();
            else if(!IsSurfacePreview)
                SpawnBossIfApplicable((Dimension)realm);

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
                    if(world)
                    {
                        revision=packet.rev;
                        if(packet.edits!=null)foreach(var edit in packet.edits)world.Edit(edit.x,edit.y,edit.z,(ushort)edit.block);
                        if(Session){Session.Living.Apply(packet);Session.Animals.Apply(packet);}
                        if(packet.players!=null)foreach(var peer in packet.players)SetAvatar(peer);
                        break;
                    }
                    if(packet.version!=TerrainGenerator.Version){Fail("Несовместимая версия генератора");Destroy(client.gameObject);busy=false;return;}
                    if(!GameRules.AcceptsRoom(packet.rulesVersion,packet.serverRulesVersion)){Fail("Несовместимые правила комнаты. Обновите сервер; для нового содержимого нужна новая комната");Destroy(client.gameObject);return;}
                    if(packet.contentVersion>9||packet.contentVersion<0||packet.rulesVersion>=14&&packet.contentVersion!=9||packet.rulesVersion==13&&packet.contentVersion!=8||(packet.rulesVersion>=10&&packet.rulesVersion<=12&&packet.contentVersion!=7)||packet.rulesVersion==9&&packet.contentVersion!=6||packet.rulesVersion==8&&packet.contentVersion!=5||packet.rulesVersion==7&&packet.contentVersion!=4||packet.rulesVersion==6&&packet.contentVersion!=3||packet.rulesVersion==5&&packet.contentVersion!=2){Fail("Несовместимая версия содержимого мира");Destroy(client.gameObject);return;}
                    selectedMode=packet.mode;selfId=packet.id;IsHost=packet.host;pendingWelcome=packet;
                    if(packet.lobby&&!packet.started){online=true;busy=false;InLobby=true;ui.ShowLobby();break;}BeginNetworkWorld(packet);break;
                case "lobby":ApplyLobby(packet);break;
                case "lobby_start":ApplyLobby(packet);if(InLobby&&pendingWelcome!=null){InLobby=false;pendingWelcome.story=packet.story;pendingWelcome.serverNow=packet.serverNow;pendingWelcome.clock=packet.clock>0?packet.clock:420;BeginNetworkWorld(pendingWelcome);}break;
                case "story":if(Story&&!InDreamDimension){Story.Apply(packet.story,packet.serverNow);Session.Animals.Apply(packet);if(packet.campaign!=null)Session.Data.campaign=packet.campaign;}break;
                case "edit":
                    if(world&&!InDreamDimension&&packet.rev>revision){revision=packet.rev;world.Edit((int)packet.x,(int)packet.y,(int)packet.z,(ushort)packet.block);if(Session)Session.Living.Apply(packet);if(packet.id==selfId&&Session)Session.ConfirmEdit(new Vector3Int((int)packet.x,(int)packet.y,(int)packet.z));}break;
                case "living_saved":case "living":if(Session&&!InDreamDimension)Session.Living.Apply(packet);break;
                case "animal_hit":if(Session&&!InDreamDimension){Session.Animals.Apply(packet);}break;
                case "chat":if(!InDreamDimension)AddChat(packet);break;
                case "player":if(!InDreamDimension){if(Session)Session.Living.Apply(packet);if(Story&&packet.story!=null)Story.Apply(packet.story,packet.serverNow);if(Session)Session.Animals.Apply(packet);ApplyLift(packet.lift);SetAvatar(new NetPlayer{id=packet.id,name=packet.name,x=packet.x,y=packet.y,z=packet.z,yaw=packet.yaw,offhand=packet.offhand,weapon=packet.weapon,equipment=packet.equipment});}break;
                case "space_site_place":
                    if(packet.launchSite!=null&&packet.launchSite.active&&world)
                    {SetLaunchSite(new Vector3Int(packet.launchSite.x,packet.launchSite.y,packet.launchSite.z));
                     ApplyLift(packet.lift);if(Session)Session.Notice="Космодром установлен в общей комнате: идите по эстакаде к шаттлу!";}
                    break;
                case "space_lift":ApplyLift(packet.lift);break;
                case "space_hatch":ApplyHatch(packet.hatchOpen);break;
                case "space_seat":ApplySeats(packet.seats);break;
                case "space_mission":if(packet.space!=null&&(ShuttleSeated||flightSnapshot!=null))
                    ApplyMoonFlight(packet.space);break;
                case "space_landed":
                    if(world&&player&&Session)
                    {
                        var landedKind=SpaceFlightVisuals.ParseWireKey(packet.planet);
                        flightSnapshot=null;
                        CompleteOfflinePlanetLanding(landedKind);
                    }
                    break;
                case "space_station_docked":
                    flightSnapshot=null;
                    CompleteOfflineStationDocking();
                    break;
                case "space_station_departed":
                    DepartStationToShuttle();
                    break;
                case "station_trade":
                    if(Session&&packet.save!=null)
                    {
                        Session.Data.player=packet.save;
                        Session.Inventory.revision++;
                        Session.Notice="Обмен на орбитальной станции подтверждён сервером!";
                    }
                    break;
                case "space_crew_join":ApplyFlightCrewJoin(packet);break;
                case "space_preflight":if(Session&&packet.id==selfId)
                    Session.Notice=packet.ready?"Экипаж подтверждён сервером! Нажмите [R] для пуска!":
                        "Предстартовая проверка: "+string.Join("; ",packet.issues??Array.Empty<string>());
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
            if(packet.station!=null&&packet.station.active)CompleteOfflineStationDocking();
            status="Мир подключён • "+code;
            BeginHostMigration();
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
            foreach(var peerAvatar in avatars.Values)if(peerAvatar)peerAvatar.SetActive(!StoryLocked&&!InDreamDimension);
            if(world&&world.Ready&&status=="Генерируется мир…")status="Мир готов — нажми «Продолжить»";

            if(!InDreamDimension&&!InRadiaDimension&&!dockedAtStation&&sharedLaunchSite.HasValue&&!launchVisual&&world&&world.Ready&&player&&
               new Vector2(player.transform.position.x-sharedLaunchSite.Value.x,
                           player.transform.position.z-sharedLaunchSite.Value.z).sqrMagnitude<185f*185f)
            {
                launchVisual=LaunchSiteVisual.Create(world.transform,sharedLaunchSite.Value,world.Seed,world.Dimension);
                if(launchVisual.Cabin)launchVisual.Cabin.BindHelm(SpaceHelm);
                ApplyLift(liftSnapshot);
                ApplyHatch(hatchOpen);
            }

            if(world&&player&&Session&&(!paused||online))
            {
                var kb=Keyboard.current;
                var ms=Mouse.current;
                float fwd=kb!=null?((kb.wKey.isPressed?1f:0f)-(kb.sKey.isPressed?1f:0f)):0f;
                float str=kb!=null?((kb.dKey.isPressed?1f:0f)-(kb.aKey.isPressed?1f:0f)):0f;
                float vrt=kb!=null?((kb.spaceKey.isPressed?1f:0f)-(kb.cKey.isPressed?1f:0f)):0f;
                Vector2 md=ms!=null?ms.delta.ReadValue():Vector2.zero;
                Tech.StepRuntime(Mathf.Min(Time.deltaTime,0.1f),world,player,Session,Camera.main,fwd,str,vrt,md.x,md.y,out bool enterRadia,out bool exitRadia);
                if(exitRadia)ExitRadiaDimension();
                else if(enterRadia&&!SpaceHelm.Active&&!offlineFlightActive&&!dockedAtStation)EnterRadiaDimension();
            }

            if(Dream.IsActive)
                UpdateDreamAnomaly(Mathf.Min(Time.deltaTime,0.1f));

            if(SpaceHelm.Active)
                UpdateOpenSpaceFlight(Mathf.Min(Time.deltaTime,0.1f));
            else if(offlineFlightActive)
                UpdateOfflineSpaceFlight(Mathf.Min(Time.deltaTime,0.1f));

            if(activePlanetBoss&&!activePlanetBoss.Defeated&&player&&Session&&(!paused||online))
            {
                activePlanetBoss.TickBoss(Mathf.Min(Time.deltaTime,0.1f),player.transform.position,(dmg,dir,msg)=>
                {
                    Session.Impact(dmg,BodyPart.Torso,dir);
                    Session.Notice=msg;
                });
            }

            foreach(var pair in avatars)if(pair.Value&&targets.TryGetValue(pair.Key,out var target))
            {
                if(flightSnapshot!=null&&flightSnapshot.active&&launchVisual&&
                   pair.Value.transform.parent==launchVisual.ShuttleRoot)continue;
                pair.Value.transform.position=Vector3.Lerp(pair.Value.transform.position,target,Time.deltaTime*12);
            }

            // Refresh co-op protocol-2 flight readiness every 0.8s while seated in cockpit!
            if(online&&client&&client.Connected&&ShuttleSeated&&!InMoonFlight&&Time.unscaledTime-lastReadyAckSent>=0.8f)
            {
                client.Send(new NetPacket{type="space_flight_ready",protocol=2});
                lastReadyAckSent=Time.unscaledTime;
            }

            if(online&&client&&client.Connected&&flightSnapshot!=null&&flightSnapshot.active)
            {
                if(Time.unscaledTime-lastFlightHeartbeat>=.75f)
                {client.Send(new NetPacket{type="space_heartbeat"});lastFlightHeartbeat=Time.unscaledTime;}
            }
            else if(online&&client&&client.Connected&&!InDreamDimension&&!dockedAtStation&&player&&world.Ready&&Time.unscaledTime-lastPosition>=.2f)
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
            Dream.ForceReset();SpaceHelm.Disengage();CockpitMouseMode=false;
            if(Session&&!online)Session.Checkpoint();Session=null;
            if(client)Destroy(client.gameObject);
            foreach(var a in avatars.Values)if(a)Destroy(a);avatars.Clear();targets.Clear();
            var camera=Camera.main;if(camera){camera.transform.SetParent(null);camera.clearFlags=CameraClearFlags.SolidColor;}
            if(ShuttleDemo){Destroy(ShuttleDemo.gameObject);ShuttleDemo=null;}
            if(activePlanetBoss){Destroy(activePlanetBoss.gameObject);activePlanetBoss=null;}
            if(techRuntime){Destroy(techRuntime.gameObject);techRuntime=null;}
            if(spaceVisuals){Destroy(spaceVisuals.gameObject);spaceVisuals=null;}
            if(player){player.transform.SetParent(null);var hands=player.GetComponent<FirstPersonHands>();if(hands)hands.Release();Destroy(player.gameObject);}
            if(world)Destroy(world.gameObject);
            client=null;world=null;player=null;launchBlueprint=null;offlineShuttleSeat=false;sharedLaunchSite=null;launchVisual=null;liftSnapshot=null;hatchOpen=false;
            offlineFlightActive=false;offlineNav=null;dockedAtStation=false;
            shuttleSeats=new string[2];flightSnapshot=null;flightSeatIndex=-1;lastFlightHeartbeat=0;lastFlightControl=0;
            online=false;busy=false;paused=true;SetCursor(false);status="";lastError="";WorldAtmosphere.Clear();
        }
        static Vector3 FindSpawn(int seed,Dimension realm,int contentVersion)
        {
            if(realm==Dimension.Radia)return new Vector3(0.5f,50.2f,-11.5f);
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
