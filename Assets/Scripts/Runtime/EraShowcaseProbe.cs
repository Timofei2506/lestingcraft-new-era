using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;

namespace LestingCraft.Runtime
{
    /// <summary>
    /// Renders 8 authentic 1920x1080 in-engine Ultra URP gameplay screenshots covering
    /// every major feature in LestingCraft New Era (era-v8 / rules13 / contentVersion 8).
    /// Activated strictly when "-lc-showcase" is passed on the command line.
    /// </summary>
    public sealed class EraShowcaseProbe : MonoBehaviour
    {
        bool finishing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-lc-showcase") < 0) return;
            var obj = new GameObject("Era v8 Showcase Capture");
            DontDestroyOnLoad(obj);
            obj.AddComponent<EraShowcaseProbe>();
        }

        void Awake()
        {
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        void OnLog(string condition, string trace, LogType type)
        {
            if (!finishing && (type == LogType.Exception || type == LogType.Error))
            {
                Debug.LogWarning("[Showcase] Observed runtime error: " + condition + "\n" + trace);
            }
        }

        IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 1000;
            Screen.SetResolution(1920, 1080, false);
            yield return null;
            yield return null;

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Fail("GraphicsDeviceType.Null cannot render showcase screenshots.");
                yield break;
            }
            Debug.Log("[Showcase] GPU: " + SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName +
                      " (" + Screen.width + "x" + Screen.height + ")");

            var game = FindFirstObjectByType<PrototypeGame>();
            var ui = FindFirstObjectByType<GameUI>();
            if (!game || !ui)
            {
                Fail("PrototypeGame or GameUI missing.");
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + 30f;
            while ((!ui.IsReady || !SplashScreen.isFinished) && Time.realtimeSinceStartup < deadline)
                yield return null;

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ShowcaseResults"));
            Directory.CreateDirectory(outDir);

            // Launch deterministic survival world (seed 2506) directly on Ultra preset,
            // keeping cameras muted ONLY during background chunk streaming so CPU software
            // rasterization does not throttle VoxelWorld.LateUpdate() chunk meshing.
            bool isSoftwareGpu = (SystemInfo.graphicsDeviceName ?? "").IndexOf("llvmpipe", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isSoftwareGpu)
            {
                VisualSettings.Current.HighDefault();
                VisualSettings.Current.AmbientOcclusion = false;
                VisualSettings.Current.VolumetricQuality = 0;
                VisualSettings.Current.Save();
                Shader.SetGlobalFloat("_LCParallaxSteps", 3f);
                Shader.SetGlobalFloat("_LCVolumetricSteps", 3f);
                Shader.SetGlobalFloat("_LCCloudSteps", 6f);
            }
            else
            {
                VisualSettings.Current.Ultra();
            }
            ui.ShowHome();
            game.ShowcaseStartFreshWorld();
            yield return null;

            var cam = Camera.main;
            var player = game.Session ? game.Session.Player : null;
            var hands = player ? player.GetComponent<FirstPersonHands>() : null;
            SetCamerasActive(cam, hands, false);

            deadline = Time.realtimeSinceStartup + 180f;
            if (game.Session && game.Session.World)
                game.Session.World.ShowcasePumpSync();
            while ((!game.WorldReady || !game.Session || !game.Session.World.IsIdleForShowcase) &&
                   Time.realtimeSinceStartup < deadline)
            {
                if (game.Session)
                {
                    game.Session.Data.clock = 570.0;
                    game.Session.World.ShowcasePumpSync();
                }
                yield return null;
            }
            Debug.Log("[Showcase] Initial world streaming complete: ready=" + game.WorldReady +
                      " chunks=" + game.ChunkCount + " t=" + Time.realtimeSinceStartup.ToString("F1") + "s");
            if (!game.WorldReady || !game.Session || game.ChunkCount < 5)
            {
                Fail("World failed to initialize for showcase (chunks=" + game.ChunkCount + ").");
                yield break;
            }

            // Wait for compulsory ruined-house intro cinematic lock while cameras are muted (~1000 FPS)
            deadline = Time.realtimeSinceStartup + 60f;
            while (game.StoryLocked && Time.realtimeSinceStartup < deadline)
                yield return null;

            player = game.Session.Player;
            hands = player.GetComponent<FirstPersonHands>();
            player.enabled = false;

            // Populate hotbar with signature era-v8 items so HUD bar looks authentic in every gameplay shot
            var inv = game.Session.Inventory;
            for (int i = 0; i < inv.slots.Length; i++) inv.slots[i] = new ItemStack();
            int[] signatureItems =
            {
                55, 56, 54, 57, 59, 67, 66, 101, 102,
                58, 60, 61, 62, 63, 64, 65, 68, 69
            };
            for (int i = 0; i < signatureItems.Length; i++)
                inv.slots[i] = new ItemStack { item = signatureItems[i], count = signatureItems[i] >= 100 ? 1 : 16 };
            inv.selected = 0;
            inv.revision++;
            game.Session.Vitals.health = 100f;
            game.Session.Vitals.hunger = 94f;
            game.Session.Vitals.stamina = 100f;

            Vector3 origin = player.transform.position;
            int baseX = Mathf.Clamp(Mathf.RoundToInt(origin.x) + 42, -160, 160);
            int baseZ = Mathf.Clamp(Mathf.RoundToInt(origin.z) + 42, -160, 160);

            // =========================================================================
            // SHOT 1: Upgraded Village, Shingled Houses, Bed Interior, Lantern Street
            //         Poles (zero campfires on posts!) & Diverse Villager Professions
            // =========================================================================
            SetCamerasActive(cam, hands, false);
            MoveBoth(player, cam, new Vector3(baseX, 90f, baseZ), new Vector3(baseX, 80f, baseZ + 8f));
            yield return WaitWorldMeshed(game.Session.World, 35f);
            int groundY = FindSurfaceY(game.Session.World, baseX, baseZ);
            GameObject villageRoot = BuildVillageShowcaseScene(game.Session.World, baseX, groundY, baseZ);
            inv.selected = 0;
            inv.revision++;
            Vector3 shot1Cam = new Vector3(baseX - 9.5f, groundY + 4.35f, baseZ - 12.5f);
            Vector3 shot1Look = new Vector3(baseX + 2.5f, groundY + 2.8f, baseZ + 3.5f);
            MoveBoth(player, cam, shot1Cam, shot1Look);
            yield return WaitWorldMeshed(game.Session.World, 35f);
            yield return RenderAndCapture(cam, hands, true, Path.Combine(outDir, "01-village-houses-lantern-poles.png"), () =>
            {
                game.Session.Data.clock = 760.0;
                game.Session.Notice = "Обновлённая деревня: черепичные крыши, кровати в домах, уличные фонари и 10 профессий жителей";
                cam.fieldOfView = 66f;
                cam.farClipPlane = 450f;
                MoveBoth(player, cam, shot1Cam, shot1Look);
            });
            Destroy(villageRoot);

            // =========================================================================
            // SHOT 2: New Biomes (Cherry Grove, Bioluminescent Glade, Red Mesa, Glacial
            //         Ice), New v8 Blocks & 20 Articulated 3D Wildlife Species
            // =========================================================================
            int bioX = Mathf.Clamp(baseX + 86, -180, 180), bioZ = Mathf.Clamp(baseZ - 22, -180, 180);
            MoveBoth(player, cam, new Vector3(bioX, 90f, bioZ), new Vector3(bioX, 80f, bioZ + 8f));
            yield return WaitWorldMeshed(game.Session.World, 35f);
            int bioY = FindSurfaceY(game.Session.World, bioX, bioZ);
            GameObject wildlifeRoot = BuildBiomesAndWildlifeScene(game.Session.World, bioX, bioY, bioZ);
            inv.selected = 5;
            inv.revision++;
            Vector3 shot2Cam = new Vector3(bioX, bioY + 4.25f, bioZ - 13.5f);
            Vector3 shot2Look = new Vector3(bioX, bioY + 2.25f, bioZ + 4.5f);
            MoveBoth(player, cam, shot2Cam, shot2Look);
            yield return WaitWorldMeshed(game.Session.World, 35f);
            yield return RenderAndCapture(cam, hands, true, Path.Combine(outDir, "02-biomes-cherry-blocks-and-20-animals.png"), () =>
            {
                game.Session.Data.clock = 615.0;
                game.Session.Notice = "Новые биомы (Вишнёвая роща, Биолюминесцентная чаща, Меса, Ледник), 16 блоков и 20 видов животных";
                cam.fieldOfView = 68f;
                MoveBoth(player, cam, shot2Cam, shot2Look);
            });
            Destroy(wildlifeRoot);

            // =========================================================================
            // SHOT 3: Cosmodrome Launch Complex & Walkable Vertical Shuttle Orbiter
            // =========================================================================
            int padX = Mathf.Clamp(baseX - 88, -170, 170), padZ = Mathf.Clamp(baseZ + 76, -170, 170);
            MoveBoth(player, cam, new Vector3(padX - 24f, 92f, padZ - 28f), new Vector3(padX, 82f, padZ));
            yield return WaitWorldMeshed(game.Session.World, 35f);
            game.ShowcaseSpawnCosmodrome(padX, padZ);
            yield return null;
            var launchVis = game.LaunchComplexVisual;
            Vector3 shuttleBase = launchVis ? launchVis.ShuttleRoot.position : new Vector3(padX, 68f, padZ);
            Vector3 shot3Cam = shuttleBase + new Vector3(-25f, 10.5f, -31f);
            Vector3 shot3Look = shuttleBase + new Vector3(0.5f, 22.5f, 1.5f);
            ClearSightCorridor(game.Session.World, shot3Cam, shot3Look, 6);
            MoveBoth(player, cam, shot3Cam, shot3Look);
            yield return WaitWorldMeshed(game.Session.World, 35f);
            yield return RenderAndCapture(cam, hands, true, Path.Combine(outDir, "03-cosmodrome-orbiter-launch-complex.png"), () =>
            {
                game.Session.Data.clock = 570.0;
                game.Session.Notice = "Космодром Лестингии: стартовый стол, кабель-мачта, лифт экипажа и вертикальный орбитальный шаттл";
                cam.fieldOfView = 68f;
                MoveBoth(player, cam, shot3Cam, shot3Look);
            });

            // =========================================================================
            // SHOT 4: Seated First-Person Shuttle Flight Deck in Open Space — Real
            //         Panoramic Windows Showing Cubic Voxel Saturn Outside + Redesigned
            //         3-MFD Dashboard, Center Pedestal, Switches, Guard, Throttle & Stick!
            // =========================================================================
            game.ShowcaseOpenSpace(false);
            var helm = game.SpaceHelm;
            var spaceVis = SpaceFlightVisuals.Ensure(null);
            Transform shipRoot = game.LaunchComplexVisual ? game.LaunchComplexVisual.ShuttleRoot : null;
            var cabin = launchVis ? launchVis.Cabin : null;
            GameObject cockpitFillObj = null;
            if (cabin)
            {
                cabin.BindHelm(helm);
                helm.SelectTarget(CelestialBodyId.Saturn);
                helm.SetThrottle(0.78f);
                cabin.InteractiveRig.ActivateControl(CockpitControlId.MasterArmCover);
                cockpitFillObj = new GameObject("CockpitShowcaseKeyLight");
                cockpitFillObj.transform.SetParent(cabin.transform, false);
                cockpitFillObj.transform.localPosition = new Vector3(0.05f, 44.45f, 4.95f);
                var fill = cockpitFillObj.AddComponent<Light>();
                fill.type = LightType.Point;
                fill.color = new Color(0.95f, 0.97f, 1.0f);
                fill.range = 6.8f;
                fill.intensity = 2.15f;
                fill.shadows = LightShadows.None;
            }
            yield return RenderAndCapture(cam, hands, false, Path.Combine(outDir, "04-cockpit-interactive-3d-controls.png"), () =>
            {
                if (shipRoot) shipRoot.rotation = Quaternion.identity;
                helm.SelectTarget(CelestialBodyId.Saturn);
                SpaceVector saturnPos = CelestialAtlas.WorldPositionKm(CelestialBodyId.Saturn, helm.OrbitTimeDays);
                helm.PositionKm = saturnPos - new SpaceVector(18000.0, 9000.0, 130000.0);
                helm.SetThrottle(0.78f);
                helm.Rotation = Quaternion.identity;

                if (cabin)
                {
                    Vector3 eyeWorld = cabin.transform.TransformPoint(new Vector3(-0.38f, 43.82f, 4.68f));
                    Vector3 lookWorld = cabin.transform.TransformPoint(new Vector3(0.10f, 43.46f, 6.20f));
                    player.transform.SetParent(null, true);
                    player.TeleportTo(eyeWorld - Vector3.up * 1.5f, false);
                    cam.fieldOfView = 75f;
                    cam.nearClipPlane = 0.04f;
                    cam.farClipPlane = 1200f;
                    cam.transform.position = eyeWorld;
                    cam.transform.LookAt(lookWorld);

                    spaceVis.SetOpenSpaceFlightActive(helm, shipRoot, cam.transform);
                    var saturnInView = GameObject.Find("Atlas_saturn");
                    if (saturnInView)
                    {
                        saturnInView.transform.localPosition = new Vector3(11.5f, 9.5f, 54f);
                        saturnInView.transform.localRotation = Quaternion.Euler(22f, 36f, -12f);
                        saturnInView.transform.localScale = Vector3.one * 1.12f;
                    }
                    var marsSide = GameObject.Find("Atlas_mars");
                    if (marsSide)
                    {
                        marsSide.SetActive(true);
                        marsSide.transform.localPosition = new Vector3(38f, 5.5f, 28f);
                        marsSide.transform.localRotation = Quaternion.Euler(18f, -28f, 10f);
                        marsSide.transform.localScale = Vector3.one * 1.35f;
                    }

                    if (cabin.InteractiveRig)
                    {
                        cabin.InteractiveRig.RefreshVisualPoses(0.05f);
                        var stick = cabin.InteractiveRig.transform.Find("FlightStickRoot/FlightStickPivot");
                        if (stick) stick.localRotation = Quaternion.Euler(8f, 0f, -6f);
                    }
                }
                game.Session.Notice = "Обновлённая кабина шаттла: панорамные окна в космос, вид на кубический Сатурн, 3 экрана MFD, РУД и РУС";
            });

            // =========================================================================
            // SHOT 4B: Full Shuttle Interior Walkthrough View (Aft Flight-Deck Catwalk
            //          Looking Forward Across Twin Crew Seats, Safety Railing, Inter-Deck
            //          Ladder, Side Viewports, Sloped 3-MFD Console & Open Space Outside!)
            // =========================================================================
            yield return RenderAndCapture(cam, hands, false, Path.Combine(outDir, "04b-shuttle-cabin-interior-ergonomics.png"), () =>
            {
                if (shipRoot) shipRoot.rotation = Quaternion.identity;
                helm.SelectTarget(CelestialBodyId.Saturn);
                SpaceVector saturnPos = CelestialAtlas.WorldPositionKm(CelestialBodyId.Saturn, helm.OrbitTimeDays);
                helm.PositionKm = saturnPos - new SpaceVector(18000.0, 9000.0, 130000.0);
                helm.SetThrottle(0.78f);
                helm.Rotation = Quaternion.identity;

                if (cabin)
                {
                    Vector3 catwalkEye = cabin.transform.TransformPoint(new Vector3(0.50f, 44.32f, 2.08f));
                    Vector3 catwalkLook = cabin.transform.TransformPoint(new Vector3(0.50f, 43.38f, 5.72f));
                    player.transform.SetParent(null, true);
                    player.TeleportTo(catwalkEye - Vector3.up * 1.5f, false);
                    cam.fieldOfView = 82f;
                    cam.nearClipPlane = 0.05f;
                    cam.farClipPlane = 1200f;
                    cam.transform.position = catwalkEye;
                    cam.transform.LookAt(catwalkLook);

                    spaceVis.SetOpenSpaceFlightActive(helm, shipRoot, cam.transform);
                    var saturnInView = GameObject.Find("Atlas_saturn");
                    if (saturnInView)
                    {
                        saturnInView.transform.localPosition = new Vector3(5.0f, 10.5f, 56f);
                        saturnInView.transform.localRotation = Quaternion.Euler(22f, 36f, -12f);
                        saturnInView.transform.localScale = Vector3.one * 1.18f;
                    }
                    var marsPort = GameObject.Find("Atlas_mars");
                    if (marsPort)
                    {
                        marsPort.SetActive(true);
                        marsPort.transform.localPosition = new Vector3(-34f, 6.0f, 26f);
                        marsPort.transform.localRotation = Quaternion.Euler(18f, -28f, 10f);
                        marsPort.transform.localScale = Vector3.one * 1.35f;
                    }
                    if (cabin.InteractiveRig)
                        cabin.InteractiveRig.RefreshVisualPoses(0.05f);
                }
                game.Session.Notice = "Интерьер шаттла: кресла экипажа, межпалубная шахта с лестницей, боковые иллюминаторы и панорамный обзор";
            });
            if (cockpitFillObj) Destroy(cockpitFillObj);
            cam.nearClipPlane = 0.1f;

            // =========================================================================
            // SHOT 5: Open Space Manual Flight in the Solar System — Giant Cubic Voxel
            //         Saturn with Square Voxel Rings, Starfield & Windowed Orbiter
            // =========================================================================
            player.transform.SetParent(null, true);
            yield return RenderAndCapture(cam, hands, false, Path.Combine(outDir, "05-open-space-saturn-solar-system.png"), () =>
            {
                helm.SelectTarget(CelestialBodyId.Saturn);
                SpaceVector saturnPos = CelestialAtlas.WorldPositionKm(CelestialBodyId.Saturn, helm.OrbitTimeDays);
                helm.PositionKm = saturnPos - new SpaceVector(78000.0, 26000.0, 145000.0);
                helm.SetThrottle(0.86f);
                helm.Boost = true;
                helm.Rotation = Quaternion.identity;
                if (shipRoot)
                {
                    shipRoot.rotation = Quaternion.Euler(68f, 26f, -14f);
                    cam.fieldOfView = 64f;
                    cam.farClipPlane = 1200f;
                    Vector3 chaseCamPos = shipRoot.position + new Vector3(-22f, 16f, -46f);
                    player.TeleportTo(chaseCamPos, false);
                    cam.transform.position = chaseCamPos;
                    spaceVis.SetOpenSpaceFlightActive(helm, shipRoot, cam.transform);

                    var saturnObj = GameObject.Find("Atlas_saturn");
                    if (saturnObj)
                    {
                        saturnObj.transform.localScale = Vector3.one * 1.42f;
                        saturnObj.transform.localRotation = Quaternion.Euler(24f, 38f, -14f);
                        Vector3 aim = Vector3.Lerp(shipRoot.position + new Vector3(6f, 14f, 12f), saturnObj.transform.position, 0.72f);
                        cam.transform.LookAt(aim);
                    }
                }
                game.Session.Notice = "Открытый космос [Солнечная система]: кубический воксельный Сатурн и квадратные кольца • 18 400 м/с";
            });

            // =========================================================================
            // SHOT 6: Intergalactic Flight in Galaxy #2 — Andromeda (M31) & Cubic
            //         Crystalline Planet "Andromeda Prime" + Square Rings + Spiral M31
            // =========================================================================
            helm.SelectTarget(CelestialBodyId.AndromedaPrime);
            helm.StartHyperjump(CelestialBodyId.AndromedaPrime);
            for (int step = 0; step < 30; step++)
                helm.Step(0.5f, 0f, 0f, 0f);
            yield return RenderAndCapture(cam, hands, false, Path.Combine(outDir, "06-andromeda-galaxy-prime.png"), () =>
            {
                helm.SelectTarget(CelestialBodyId.AndromedaPrime);
                SpaceVector primePos = CelestialAtlas.WorldPositionKm(CelestialBodyId.AndromedaPrime, helm.OrbitTimeDays);
                helm.PositionKm = primePos - new SpaceVector(-62000.0, 22000.0, 128000.0);
                helm.SetThrottle(0.92f);
                helm.Boost = true;
                helm.Rotation = Quaternion.identity;
                if (shipRoot)
                {
                    shipRoot.rotation = Quaternion.Euler(64f, -24f, 12f);
                    Vector3 chaseCamPos = shipRoot.position + new Vector3(24f, 15f, -44f);
                    player.TeleportTo(chaseCamPos, false);
                    cam.transform.position = chaseCamPos;
                    spaceVis.SetOpenSpaceFlightActive(helm, shipRoot, cam.transform);

                    var primeObj = GameObject.Find("Atlas_andromeda_prime");
                    var galaxyObj = GameObject.Find("NeighborSpiralGalaxyBackdrop");
                    if (primeObj)
                    {
                        primeObj.transform.localPosition = new Vector3(14f, 6.5f, 52f);
                        primeObj.transform.localScale = Vector3.one * 1.62f;
                        primeObj.transform.localRotation = Quaternion.Euler(24f, -36f, 14f);
                        if (galaxyObj && primeObj.transform.parent == galaxyObj.transform.parent)
                        {
                            galaxyObj.transform.localPosition = new Vector3(-24f, 18f, 92f);
                            galaxyObj.transform.LookAt(cam.transform.position);
                            galaxyObj.transform.Rotate(52f, 14f, 22f, Space.Self);
                        }
                        Vector3 aim = Vector3.Lerp(shipRoot.position + new Vector3(-2f, 14f, 10f), primeObj.transform.position, 0.70f);
                        cam.transform.LookAt(aim);
                    }
                }
                game.Session.Notice = "Галактика Андромеды (M31): система Ядра Андромеды • Планета Андромеда Прайм и спиральный рукав";
            });

            helm.Disengage();
            game.SetOuterSpaceSurfaceHidden(false);
            spaceVis.SetOpenSpaceFlightActive(null, shipRoot, cam.transform);
            if (shipRoot) shipRoot.rotation = Quaternion.identity;

            // =========================================================================
            // SHOT 7: Eerie "Dream" Dimension — Sleep Paralysis, Towering Black Monoliths
            //         & Glowing-Eyed Shadow Watcher in Swirling Fog (Isolated from Co-op)
            // =========================================================================
            game.ShowcaseEnterDream();
            yield return WaitWorldMeshed(game.Session.World, 35f);
            Vector3 dp = player.transform.position;
            int dx = Mathf.RoundToInt(dp.x), dz = Mathf.RoundToInt(dp.z);
            int dy = FindSurfaceY(game.Session.World, dx, dz);
            var dreamLightObj = BuildDreamCorridorAndMonoliths(game.Session.World, dx, dy, dz);
            MoveBoth(player, cam, new Vector3(dx, dy + 2.35f, dz - 3.5f), new Vector3(dx, dy + 2.45f, dz + 10.5f));
            yield return WaitWorldMeshed(game.Session.World, 35f);
            yield return RenderAndCapture(cam, hands, false, Path.Combine(outDir, "07-dream-dimension-sleep-paralysis.png"), () =>
            {
                spaceVis.SyncDreamWatcher(game.Dream, cam.transform);
                var watcherObj = GameObject.Find("DreamShadowWatcher");
                if (watcherObj)
                {
                    watcherObj.transform.position = new Vector3(dx, dy + 1.0f, dz + 10.5f);
                    watcherObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                }
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.24f, 0.20f, 0.34f);
                RenderSettings.fogDensity = 0.020f;
                cam.backgroundColor = new Color(0.24f, 0.20f, 0.34f);
                cam.fieldOfView = 66f;
                cam.farClipPlane = 240f;
                MoveBoth(player, cam, new Vector3(dx, dy + 2.35f, dz - 3.5f), new Vector3(dx, dy + 2.45f, dz + 10.5f));
                game.Session.Notice = "Измерение «Сон» [Сонный паралич]: вы не можете пошевелиться... В тумане замер Наблюдатель (изоляция от мира)";
            });
            if (dreamLightObj) Destroy(dreamLightObj);

            // Wake up cleanly back into sunlit Lestingia!
            game.ShowcaseExitDream();
            yield return WaitWorldMeshed(game.Session.World, 35f);

            // =========================================================================
            // SHOT 8: Live In-Game Inventory UI, Full Armor 3D Character Preview &
            //         All 16 New v8 Blocks + Craftable Bed Tooltip
            // =========================================================================
            int[] gearItems = { 104, 105, 106, 107, 108, 130, 131, 132 };
            for (int g = 0; g < gearItems.Length; g++)
            {
                inv.equipment[Equipment.Slot(gearItems[g])] = new ItemStack
                {
                    item = gearItems[g],
                    count = 1,
                    durability = Items.Get(gearItems[g]).Durability
                };
            }
            inv.slots[0] = new ItemStack { item = 55, count = 2 };  // Кровать
            inv.slots[1] = new ItemStack { item = 56, count = 8 };  // Фонарь
            inv.slots[2] = new ItemStack { item = 54, count = 16 }; // Факел
            inv.slots[3] = new ItemStack { item = 57, count = 6 };  // Книжный шкаф
            inv.slots[4] = new ItemStack { item = 59, count = 24 }; // Черепица
            inv.slots[5] = new ItemStack { item = 58, count = 16 }; // Резной дуб
            inv.slots[6] = new ItemStack { item = 60, count = 16 }; // Листва сакуры
            inv.slots[7] = new ItemStack { item = 61, count = 12 }; // Биогриб
            inv.slots[8] = new ItemStack { item = 67, count = 9 };  // Кристалл Андромеды
            inv.slots[9] = new ItemStack { item = 62, count = 20 }; // Красный песчаник
            inv.slots[10] = new ItemStack { item = 63, count = 20 }; // Вечный лёд
            inv.slots[11] = new ItemStack { item = 64, count = 12 }; // Магмовый базальт
            inv.slots[12] = new ItemStack { item = 65, count = 20 }; // Лавандовый дёрн
            inv.slots[13] = new ItemStack { item = 66, count = 16 }; // Титановая обшивка
            inv.slots[14] = new ItemStack { item = 68, count = 8 };  // Монолит Сна
            inv.slots[15] = new ItemStack { item = 69, count = 16 }; // Камень Сна
            inv.revision++;

            SetCamerasActive(cam, hands, true);
            game.Session.Data.clock = 640.0;
            RenderSettings.fog = false;
            game.ToggleInventory();
            yield return null;
            yield return null;
            ItemHover bedHover = Array.Find(ui.GetComponentsInChildren<ItemHover>(), h => h.Item == 55);
            if (bedHover)
            {
                Vector2 pt = RectTransformUtility.WorldToScreenPoint(null, bedHover.transform.position) + new Vector2(28f, -28f);
                ExecuteEvents.Execute(bedHover.gameObject, new PointerEventData(EventSystem.current) { position = pt }, ExecuteEvents.pointerEnterHandler);
            }
            yield return RenderAndCapture(cam, hands, true, Path.Combine(outDir, "08-inventory-new-blocks-and-bed.png"), () =>
            {
                game.Session.Data.clock = 640.0;
                RenderSettings.fog = false;
            });

            finishing = true;
            Debug.Log("[Showcase] PASS: 8 authentic 1920x1080 URP screenshots saved to " + outDir);
            Application.Quit(0);
            yield return null;
            Environment.Exit(0);
        }

        static void SetCamerasActive(Camera cam, FirstPersonHands hands, bool active)
        {
            if (cam) cam.enabled = active;
            if (hands && hands.ViewCamera) hands.ViewCamera.enabled = active;
        }

        static IEnumerator WaitWorldMeshed(VoxelWorld world, float maxWallSeconds)
        {
            yield return null;
            if (world) world.ShowcasePumpSync();
            yield return null;
            float deadline = Time.realtimeSinceStartup + maxWallSeconds;
            while (world && !world.IsIdleForShowcase && Time.realtimeSinceStartup < deadline)
            {
                world.ShowcasePumpSync();
                yield return null;
            }
        }

        IEnumerator RenderAndCapture(Camera cam, FirstPersonHands hands, bool showHands, string fullPath, Action lockPose)
        {
            Debug.Log("[Showcase] START: " + Path.GetFileName(fullPath) + " (" + Time.realtimeSinceStartup.ToString("F1") + "s)");
            SetCamerasActive(cam, hands, false);
            for (int i = 0; i < 2; i++)
            {
                if (lockPose != null) lockPose();
                yield return null;
            }
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l && l.type != LightType.Directional) l.shadows = LightShadows.None;
            if (cam) cam.enabled = true;
            if (hands && hands.ViewCamera) hands.ViewCamera.enabled = showHands;
            if (lockPose != null) lockPose();
            yield return new WaitForEndOfFrame();
            var snap = ScreenCapture.CaptureScreenshotAsTexture();
            SetCamerasActive(cam, hands, false);
            if (!snap)
            {
                Fail("CaptureScreenshotAsTexture returned null: " + fullPath);
                yield break;
            }
            byte[] png = ImageConversion.EncodeToPNG(snap);
            Destroy(snap);
            File.WriteAllBytes(fullPath, png);
            if (!ScreenshotReady(fullPath))
            {
                Fail("Failed writing PNG screenshot: " + fullPath);
                yield break;
            }
            Debug.Log("[Showcase] CAPTURED: " + fullPath + " (" + Time.realtimeSinceStartup.ToString("F1") + "s)");
        }

        static void MoveBoth(FirstPersonController player, Camera cam, Vector3 camWorld, Vector3 lookTarget)
        {
            player.TeleportTo(camWorld - Vector3.up * 1.6f, false);
            cam.transform.position = camWorld;
            cam.transform.LookAt(lookTarget);
        }

        static int FindSurfaceY(VoxelWorld world, int x, int z)
        {
            for (int y = 118; y > 8; y--)
            {
                int b = world.GetBlock(x, y, z);
                if (b != 0 && b != 7 && b != 6) return y;
            }
            return 64;
        }

        static void SetBlock(VoxelWorld world, int x, int y, int z, int id)
        {
            world.ShowcaseEditUnbounded(x, y, z, (ushort)id);
        }

        static void ClearSightCorridor(VoxelWorld world, Vector3 from, Vector3 to, int halfWidth)
        {
            int steps = Mathf.CeilToInt(Vector3.Distance(from, to));
            for (int i = 0; i <= steps; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, (float)i / Mathf.Max(1, steps));
                int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y), cz = Mathf.RoundToInt(p.z);
                for (int dx = -halfWidth; dx <= halfWidth; dx++)
                    for (int dy = -2; dy <= halfWidth; dy++)
                        for (int dz = -halfWidth; dz <= halfWidth; dz++)
                        {
                            int b = world.GetBlock(cx + dx, cy + dy, cz + dz);
                            if (b == 4 || b == 6 || b == 60)
                                SetBlock(world, cx + dx, cy + dy, cz + dz, 0);
                        }
            }
        }

        static GameObject BuildVillageShowcaseScene(VoxelWorld world, int cx, int gy, int cz)
        {
            var actorRoot = new GameObject("ShowcaseVillageActors");
            for (int dx = -16; dx <= 18; dx++)
                for (int dz = -16; dz <= 18; dz++)
                {
                    for (int y = gy - 3; y <= gy; y++)
                        SetBlock(world, cx + dx, y, cz + dz, y == gy ? 2 : 3);
                    for (int y = gy + 1; y <= gy + 14; y++)
                        SetBlock(world, cx + dx, y, cz + dz, 0);
                }

            for (int dz = -12; dz <= 14; dz++)
                for (int dx = -3; dx <= 1; dx++)
                    SetBlock(world, cx + dx, gy, cz + dz, 1);

            int[] poleZ = { -9, -2, 5, 11 };
            for (int i = 0; i < poleZ.Length; i++)
            {
                int pz = cz + poleZ[i];
                int pxL = cx - 4, pxR = cx + 2;
                for (int h = 1; h <= 3; h++)
                {
                    SetBlock(world, pxL, gy + h, pz, 58);
                    SetBlock(world, pxR, gy + h, pz, 58);
                }
                SetBlock(world, pxL + 1, gy + 3, pz, 5);
                SetBlock(world, pxL + 1, gy + 2, pz, 56);
                SetBlock(world, pxR, gy + 4, pz, 54);
            }

            int hx0 = cx + 4, hx1 = cx + 13, hz0 = cz - 4, hz1 = cz + 6;
            for (int x = hx0; x <= hx1; x++)
                for (int z = hz0; z <= hz1; z++)
                {
                    SetBlock(world, x, gy, z, 5);
                    bool corner = (x == hx0 || x == hx1) && (z == hz0 || z == hz1);
                    bool wall = x == hx0 || x == hx1 || z == hz0 || z == hz1;
                    if (wall)
                    {
                        for (int h = 1; h <= 4; h++)
                        {
                            bool frontOpening = (x == hx0 && z >= hz0 + 2 && z <= hz1 - 2 && h <= 3)
                                             || (z == hz0 && x >= hx0 + 2 && x <= hx0 + 5 && h <= 3);
                            if (!frontOpening)
                                SetBlock(world, x, gy + h, z, corner ? 58 : (h == 2 || h == 3) && ((x + z) % 3 == 0) ? 14 : 5);
                        }
                    }
                }
            for (int layer = 0; layer <= 5; layer++)
                for (int x = hx0 - 1 + layer; x <= hx1 + 1 - layer; x++)
                    for (int z = hz0 - 1; z <= hz1 + 1; z++)
                        SetBlock(world, x, gy + 5 + layer, z, 59);

            SetBlock(world, hx0 + 2, gy + 1, hz0 + 3, 55);
            SetBlock(world, hx0 + 2, gy + 1, hz0 + 4, 55);
            SetBlock(world, hx0 + 4, gy + 1, hz0 + 5, 57);
            SetBlock(world, hx0 + 5, gy + 1, hz0 + 5, 57);
            SetBlock(world, hx0 + 4, gy + 2, hz0 + 5, 57);
            SetBlock(world, hx0 + 2, gy + 1, hz0 + 1, 16);
            SetBlock(world, hx0 + 3, gy + 3, hz0 + 2, 56);

            int bx0 = cx - 12, bx1 = cx - 6, bz0 = cz + 2, bz1 = cz + 10;
            for (int x = bx0; x <= bx1; x++)
                for (int z = bz0; z <= bz1; z++)
                {
                    SetBlock(world, x, gy, z, 5);
                    if (x == bx0 || x == bx1 || z == bz0 || z == bz1)
                        for (int h = 1; h <= 4; h++)
                            SetBlock(world, x, gy + h, z, ((x == bx0 || x == bx1) && (z == bz0 || z == bz1)) ? 58 : 5);
                }
            for (int layer = 0; layer <= 3; layer++)
                for (int x = bx0 - 1 + layer; x <= bx1 + 1 - layer; x++)
                    for (int z = bz0 - 1; z <= bz1 + 1; z++)
                        SetBlock(world, x, gy + 5 + layer, z, 59);

            Vector3[] spots =
            {
                new Vector3(cx - 1.4f, gy + 1f, cz - 6.0f),
                new Vector3(cx + 0.6f, gy + 1f, cz - 3.5f),
                new Vector3(cx - 2.3f, gy + 1f, cz - 0.5f),
                new Vector3(cx + 2.4f, gy + 1f, cz - 2.0f),
                new Vector3(cx - 0.6f, gy + 1f, cz + 3.0f),
                new Vector3(cx + 1.5f, gy + 1f, cz + 6.5f),
                new Vector3(cx - 2.6f, gy + 1f, cz + 7.5f),
                new Vector3(cx + 5.6f, gy + 1f, cz - 1.5f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var npc = new GameObject("ShowcaseVillager_" + i);
                npc.transform.SetParent(actorRoot.transform, false);
                npc.transform.position = spots[i];
                npc.transform.rotation = Quaternion.Euler(0f, 195f + (i % 3 - 1) * 32f, 0f);
                npc.AddComponent<SettlerVisual>().Build(0, i + 1);
            }
            for (int c = 0; c < 2; c++)
            {
                var cat = new GameObject("ShowcaseVillageCat_" + c);
                cat.transform.SetParent(actorRoot.transform, false);
                cat.transform.position = new Vector3(cx - 2.9f + c * 4.4f, gy + 1f, cz - 5.2f + c * 2.5f);
                cat.transform.rotation = Quaternion.Euler(0f, 160f + c * 50f, 0f);
                cat.AddComponent<SettlerVisual>().Build(1, c + 1);
            }
            return actorRoot;
        }

        static GameObject BuildBiomesAndWildlifeScene(VoxelWorld world, int cx, int gy, int cz)
        {
            var wildlifeRoot = new GameObject("ShowcaseWildlife20");
            for (int dx = -18; dx <= 18; dx++)
                for (int dz = -16; dz <= 18; dz++)
                {
                    int topBlock = dx < -6 ? 65
                                 : dx > 7 && dz > 2 ? 62
                                 : dx > 9 && dz <= 2 ? 63
                                 : 2;
                    for (int y = gy - 3; y <= gy; y++)
                        SetBlock(world, cx + dx, y, cz + dz, y == gy ? topBlock : 3);
                    for (int y = gy + 1; y <= gy + 15; y++)
                        SetBlock(world, cx + dx, y, cz + dz, 0);
                }

            int[,] trees = { { -9, 6 }, { -3, 9 }, { 5, 10 }, { 12, 8 } };
            for (int t = 0; t < 4; t++)
            {
                int tx = cx + trees[t, 0], tz = cz + trees[t, 1];
                for (int h = 1; h <= 5; h++) SetBlock(world, tx, gy + h, tz, 4);
                for (int lx = -3; lx <= 3; lx++)
                    for (int lz = -3; lz <= 3; lz++)
                        for (int ly = 4; ly <= 7; ly++)
                            if (lx * lx + lz * lz + (ly - 5) * (ly - 5) <= 10)
                                SetBlock(world, tx + lx, gy + ly, tz + lz, 60);
            }

            SetBlock(world, cx - 10, gy + 1, cz - 1, 61);
            SetBlock(world, cx - 8, gy + 1, cz + 2, 61);
            SetBlock(world, cx - 11, gy + 1, cz + 3, 61);
            for (int h = 1; h <= 3; h++)
            {
                SetBlock(world, cx + 11, gy + h, cz + 4, 62);
                SetBlock(world, cx + 12, gy + h, cz + 5, 62);
                SetBlock(world, cx + 12, gy + h, cz - 2, 63);
            }
            SetBlock(world, cx + 9, gy + 1, cz - 3, 67);
            SetBlock(world, cx + 9, gy + 2, cz - 3, 67);
            SetBlock(world, cx + 7, gy + 1, cz - 4, 64);

            int[] groundSpecies = { 13, 14, 15, 16, 17, 18, 0, 5, 4, 3, 2, 1 };
            for (int i = 0; i < groundSpecies.Length; i++)
            {
                int row = i / 6;
                int col = i % 6;
                float ax = cx - 8.5f + col * 3.4f + (row == 1 ? 1.2f : 0f);
                float az = cz - 6.5f + row * 4.8f;
                var go = new GameObject("ShowcaseAnimal_" + groundSpecies[i]);
                go.transform.SetParent(wildlifeRoot.transform, false);
                go.transform.position = new Vector3(ax, gy + 1.02f, az);
                go.transform.rotation = Quaternion.Euler(0f, 198f + (col % 3 - 1) * 26f, 0f);
                go.AddComponent<AnimalVisual>().Build(groundSpecies[i]);
            }
            int[] flyers = { 12, 19, 8 };
            for (int f = 0; f < flyers.Length; f++)
            {
                var go = new GameObject("ShowcaseFlyer_" + flyers[f]);
                go.transform.SetParent(wildlifeRoot.transform, false);
                go.transform.position = new Vector3(cx - 5.5f + f * 5.8f, gy + 5.2f + (f == 1 ? 0.9f : 0f), cz - 1.5f + f * 1.6f);
                go.transform.rotation = Quaternion.Euler(8f, 205f - f * 22f, -10f);
                go.AddComponent<AnimalVisual>().Build(flyers[f]);
            }
            return wildlifeRoot;
        }

        static GameObject BuildDreamCorridorAndMonoliths(VoxelWorld world, int cx, int gy, int cz)
        {
            for (int dx = -12; dx <= 12; dx++)
                for (int dz = -8; dz <= 20; dz++)
                {
                    SetBlock(world, cx + dx, gy, cz + dz, (Mathf.Abs(dx) <= 2) ? 68 : 69);
                    for (int y = gy + 1; y <= gy + 14; y++)
                        SetBlock(world, cx + dx, y, cz + dz, 0);
                }

            int[] pillarZ = { 2, 7, 13, 17 };
            for (int i = 0; i < pillarZ.Length; i++)
            {
                int pz = cz + pillarZ[i];
                int height = 6 + (i % 2) * 3;
                for (int h = 1; h <= height; h++)
                {
                    SetBlock(world, cx - 5, gy + h, pz, 68);
                    SetBlock(world, cx + 5, gy + h, pz, 68);
                }
                if (i == 2)
                {
                    for (int x = -5; x <= 5; x++)
                        SetBlock(world, cx + x, gy + height + 1, pz, 68);
                }
            }

            var mistLightRoot = new GameObject("DreamShowcaseMistLights");
            var backLightObj = new GameObject("WatcherRimMistLight");
            backLightObj.transform.SetParent(mistLightRoot.transform, false);
            backLightObj.transform.position = new Vector3(cx, gy + 4.2f, cz + 12.5f);
            var bl = backLightObj.AddComponent<Light>();
            bl.type = LightType.Point;
            bl.color = new Color(0.66f, 0.42f, 0.92f);
            bl.range = 44f;
            bl.intensity = 14.0f;
            bl.shadows = LightShadows.None;
            return mistLightRoot;
        }

        static bool ScreenshotReady(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    return fs.Length > 4096;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        void Fail(string msg)
        {
            finishing = true;
            Debug.LogError("[Showcase] FAIL: " + msg);
            Application.Quit(2);
        }
    }
}
