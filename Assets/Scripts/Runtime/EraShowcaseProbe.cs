using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using LestingCraft.World;
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
                game.Session.Notice = "";
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
                game.Session.Notice = "";
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
                game.Session.Notice = "";
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
                    Vector3 eyeWorld = cabin.transform.TransformPoint(new Vector3(-0.36f, 43.48f, 4.66f));
                    Vector3 lookWorld = cabin.transform.TransformPoint(new Vector3(0.12f, 43.05f, 5.75f));
                    player.transform.SetParent(null, true);
                    player.TeleportTo(eyeWorld - Vector3.up * 1.5f, false);
                    cam.fieldOfView = 76f;
                    cam.nearClipPlane = 0.04f;
                    cam.farClipPlane = 1200f;
                    cam.transform.position = eyeWorld;
                    cam.transform.LookAt(lookWorld);

                    spaceVis.SetOpenSpaceFlightActive(helm, shipRoot, cam.transform);
                    var saturnInView = GameObject.Find("Atlas_saturn");
                    if (saturnInView)
                    {
                        Vector3 windDir = (cabin.transform.TransformPoint(new Vector3(0.45f, 44.32f, 5.96f)) - eyeWorld).normalized;
                        saturnInView.transform.position = eyeWorld + windDir * 54f;
                        saturnInView.transform.rotation = cam.transform.rotation * Quaternion.Euler(24f, 38f, -14f);
                        saturnInView.transform.localScale = Vector3.one * 1.05f;
                    }
                    var marsSide = GameObject.Find("Atlas_mars");
                    if (marsSide)
                    {
                        marsSide.SetActive(true);
                        Vector3 sideDir = (cabin.transform.TransformPoint(new Vector3(-1.95f, 44.18f, 5.15f)) - eyeWorld).normalized;
                        marsSide.transform.position = eyeWorld + sideDir * 36f;
                        marsSide.transform.rotation = cam.transform.rotation * Quaternion.Euler(20f, -32f, 12f);
                        marsSide.transform.localScale = Vector3.one * 1.15f;
                    }

                    if (cabin.InteractiveRig)
                    {
                        cabin.InteractiveRig.RefreshVisualPoses(0.05f);
                        var stick = cabin.InteractiveRig.transform.Find("FlightStickRoot/FlightStickPivot");
                        if (stick) stick.localRotation = Quaternion.Euler(8f, 0f, -6f);
                    }
                }
                game.Session.Notice = "";
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
                helm.PositionKm = saturnPos - new SpaceVector(85000.0, 28000.0, 290000.0);
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
                        Vector3 windDir = (cabin.transform.TransformPoint(new Vector3(0.50f, 44.46f, 5.96f)) - catwalkEye).normalized;
                        saturnInView.transform.position = catwalkEye + windDir * 56f;
                        saturnInView.transform.rotation = cam.transform.rotation * Quaternion.Euler(24f, 38f, -14f);
                        saturnInView.transform.localScale = Vector3.one * 1.08f;
                    }
                    var titanPort = GameObject.Find("Atlas_titan");
                    if (titanPort)
                    {
                        titanPort.SetActive(true);
                        Vector3 sideDir = (cabin.transform.TransformPoint(new Vector3(-1.95f, 44.20f, 3.85f)) - catwalkEye).normalized;
                        titanPort.transform.position = catwalkEye + sideDir * 38f;
                        titanPort.transform.rotation = cam.transform.rotation * Quaternion.Euler(18f, -28f, 10f);
                        titanPort.transform.localScale = Vector3.one * 1.15f;
                    }
                    if (cabin.InteractiveRig)
                        cabin.InteractiveRig.RefreshVisualPoses(0.05f);
                }
                game.Session.Notice = "";
            });
            if (cockpitFillObj) Destroy(cockpitFillObj);
            cam.nearClipPlane = 0.1f;

            // =========================================================================
            // SHOT 5: Open Space 1:1 Flight at Saturn (~303,500 km, Mimas Orbit) —
            //         7,200 Stars, Directional Solar Terminator & Dark Night Side!
            // =========================================================================
            player.transform.SetParent(null, true);
            yield return RenderAndCapture(cam, hands, false, Path.Combine(outDir, "05-open-space-saturn-solar-system.png"), () =>
            {
                helm.SelectTarget(CelestialBodyId.Saturn);
                SpaceVector saturnPos = CelestialAtlas.WorldPositionKm(CelestialBodyId.Saturn, helm.OrbitTimeDays);
                helm.PositionKm = saturnPos - new SpaceVector(85000.0, 28000.0, 290000.0);
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
                    cam.transform.LookAt(shipRoot.position + new Vector3(14f, 18f, 24f));
                    spaceVis.SetOpenSpaceFlightActive(helm, shipRoot, cam.transform);

                    var saturnObj = GameObject.Find("Atlas_saturn");
                    if (saturnObj)
                    {
                        saturnObj.transform.position = cam.transform.position + cam.transform.forward * 98f + cam.transform.right * 18f + cam.transform.up * 6f;
                        saturnObj.transform.rotation = cam.transform.rotation * Quaternion.Euler(26f, 36f, -14f);
                        saturnObj.transform.localScale = Vector3.one * 1.28f;
                    }
                    var titanObj = GameObject.Find("Atlas_titan");
                    if (titanObj)
                    {
                        titanObj.SetActive(true);
                        titanObj.transform.position = cam.transform.position + cam.transform.forward * 124f - cam.transform.right * 34f + cam.transform.up * 16f;
                        titanObj.transform.rotation = cam.transform.rotation * Quaternion.Euler(22f, -30f, 12f);
                        titanObj.transform.localScale = Vector3.one * 0.72f;
                    }
                }
                game.Session.Notice = "";
            });

            // =========================================================================
            // SHOT 6: Intergalactic 1:1 Flight in Andromeda (M31) — 7,200 Stars,
            //         Voxel Spiral Galaxy M31 & Crystalline Ringed Andromeda Prime!
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
                    cam.fieldOfView = 64f;
                    cam.farClipPlane = 1200f;
                    Vector3 chaseCamPos = shipRoot.position + new Vector3(22f, 16f, -46f);
                    player.TeleportTo(chaseCamPos, false);
                    cam.transform.position = chaseCamPos;
                    cam.transform.LookAt(shipRoot.position + new Vector3(-14f, 18f, 24f));
                    spaceVis.SetOpenSpaceFlightActive(helm, shipRoot, cam.transform);

                    var primeObj = GameObject.Find("Atlas_andromeda_prime");
                    var galaxyObj = GameObject.Find("NeighborSpiralGalaxyBackdrop");
                    if (primeObj)
                    {
                        primeObj.transform.position = cam.transform.position + cam.transform.forward * 88f + cam.transform.right * 24f + cam.transform.up * 12f;
                        primeObj.transform.rotation = cam.transform.rotation * Quaternion.Euler(26f, -36f, 14f);
                        primeObj.transform.localScale = Vector3.one * 1.38f;
                    }
                    if (galaxyObj)
                    {
                        galaxyObj.SetActive(true);
                        galaxyObj.transform.position = cam.transform.position + cam.transform.forward * 145f - cam.transform.right * 30f + cam.transform.up * 26f;
                        galaxyObj.transform.rotation = cam.transform.rotation * Quaternion.Euler(56f, 18f, -24f);
                    }
                }
                game.Session.Notice = "";
            });

            helm.Disengage();
            game.SetOuterSpaceSurfaceHidden(false);
            spaceVis.SetOpenSpaceFlightActive(null, shipRoot, cam.transform);
            if (shipRoot) shipRoot.rotation = Quaternion.identity;

            // =========================================================================
            // SHOT 5B: Titan Surface Atmosphere Proof — Petrol-Cyan Stratosphere,
            //          Golden Methane Haze & Giant Ringed Cubic Saturn Overhead (NOT Mars!)
            // =========================================================================
            yield return RenderAndCapture(cam, hands, true, Path.Combine(outDir, "05b-titan-saturn-atmosphere-contrast.png"), () =>
            {
                game.Session.Data.clock = 610.0;
                game.Session.Notice = "";
                WorldAtmosphere.Configure(cam, (int)LestingCraft.World.Dimension.Titan, 610.0);
                spaceVis.ApplyPlanetAtmosphereAndGravity(LestingCraft.World.Dimension.Titan, player, cam);
                cam.fieldOfView = 70f;
                cam.farClipPlane = 1200f;
                Vector3 titanEye = new Vector3(bioX, bioY + 4.0f, bioZ - 10.0f);
                MoveBoth(player, cam, titanEye, titanEye + new Vector3(11f, 8.5f, 18f));
            });
            WorldAtmosphere.Configure(cam, (int)LestingCraft.World.Dimension.Lestingia, 640.0);
            spaceVis.ApplyPlanetAtmosphereAndGravity(LestingCraft.World.Dimension.Lestingia, player, cam);

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
                game.Session.Notice = "";
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

            // =========================================================================
            // SHOT 9: ESC Pause Menu With Integrated Story & Expedition Journal Card!
            // =========================================================================
            ui.ShowPause();
            yield return null;
            yield return RenderAndCapture(cam, hands, true, Path.Combine(outDir, "09-esc-pause-story-menu.png"), () =>
            {
                ui.ShowPause();
                game.Session.Data.clock = 640.0;
                RenderSettings.fog = false;
            });

            // =========================================================================
            // SHOT 10: ERA-V9 New Irradiated Dimension — «Черенковский Затон» (Dimension.Radia)
            //          Matching reference vista: twin tilted double-helix towers on white
            //          truss piers, concrete sluice dam, glowing underwater Cherenkov rows,
            //          3 Raid Bosses, mutant fauna (20..29) & monochrome stormy ash!
            // =========================================================================
            ui.ShowHUD();
            inv.slots[0] = new ItemStack { item = Items.Dosimeter, count = 1 };
            inv.slots[1] = new ItemStack { item = BlockRegistry.UraniumOreId, count = 12 };
            inv.slots[2] = new ItemStack { item = BlockRegistry.NuclearReactorId, count = 1 };
            inv.slots[3] = new ItemStack { item = BlockRegistry.CopperCableId, count = 32 };
            inv.slots[4] = new ItemStack { item = Items.ElectricLocomotive, count = 1 };
            inv.slots[5] = new ItemStack { item = Items.QuadDrone, count = 1 };
            inv.slots[6] = new ItemStack { item = Items.IndustrialRobot, count = 1 };
            inv.slots[7] = new ItemStack { item = BlockRegistry.C4ChargeId, count = 8 };
            inv.slots[8] = new ItemStack { item = Items.CherenkovCore, count = 6 };
            inv.selected = 0;
            inv.revision++;

            SetCamerasActive(cam, hands, false);
            var radiaWorld = game.Session.World;
            radiaWorld.SwitchPlanet(Dimension.Radia, Array.Empty<SavedBlock>());
            game.Tech.Sim.ResetRadiaEncounter();
            game.Tech.Sim.RadiationDose01 = 0.64f;
            var spaceVisV9 = SpaceFlightVisuals.Ensure(null);
            spaceVisV9.ApplyPlanetAtmosphereAndGravity(Dimension.Radia, player, cam);
            VisualSettings.EnsurePostProcessingVolume(1f, true, 0f);

            // Stream the entire Radia basin from near water (-20) all the way past the dam (z=78) & twin helix towers (z=104)
            Vector3[] streamAnchors =
            {
                new Vector3(0f, 52f, 96f),
                new Vector3(-36f, 52f, 96f),
                new Vector3(44f, 52f, 96f),
                new Vector3(0f, 52f, 44f),
                new Vector3(0f, 52f, -6f)
            };
            foreach (var anchor in streamAnchors)
            {
                player.TeleportTo(anchor, false);
                yield return WaitWorldMeshed(radiaWorld, 25f);
            }
            var radiaMutantsRoot = new GameObject("RadiaMutantsShowcase");
            int[] radiaSpecies = { 20, 21, 22, 24, 25, 26, 28, 29 };
            for (int m = 0; m < radiaSpecies.Length; m++)
            {
                float side = (m % 2 == 0) ? -1f : 1f;
                var mGo = new GameObject("RadiaMutant_" + radiaSpecies[m]);
                mGo.transform.SetParent(radiaMutantsRoot.transform, false);
                mGo.transform.position = new Vector3(side * (24.5f + (m % 3) * 2.4f), 49.05f, 8.0f + m * 6.5f);
                mGo.transform.rotation = Quaternion.Euler(0f, side < 0f ? 128f : -128f, 0f);
                mGo.AddComponent<AnimalVisual>().Build(radiaSpecies[m]);
            }

            Vector3 radiaCamPos = new Vector3(0.5f, 53.6f, -15.5f);
            player.TeleportTo(radiaCamPos, false);
            game.Tech.StepRuntime(0.25f, radiaWorld, player, game.Session, cam, 0f, 0f, 0f, 0f, 0f, out _, out _);

            // Cool cyan underglow along submerged Cherenkov rows so dark water shines authentically
            var cherenkovLightsRoot = new GameObject("RadiaCherenkovGlowLights");
            float[] glowZ = { 2f, 22f, 42f, 60f };
            for (int gz = 0; gz < glowZ.Length; gz++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var glObj = new GameObject("CherenkovWaterGlow_" + gz + "_" + side);
                    glObj.transform.SetParent(cherenkovLightsRoot.transform, false);
                    glObj.transform.position = new Vector3(side * 10.5f, 49.6f, glowZ[gz]);
                    var gl = glObj.AddComponent<Light>();
                    gl.type = LightType.Point;
                    gl.color = new Color(0.28f, 0.78f, 1.0f);
                    gl.range = 28f;
                    gl.intensity = 9.5f;
                    gl.shadows = LightShadows.None;
                }
            }

            yield return RenderAndCapture(cam, hands, true, Path.Combine(outDir, "10-radia-abyss-twin-helix-dam.png"), () =>
            {
                cam.fieldOfView = 72f;
                cam.farClipPlane = 600f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.76f, 0.78f, 0.80f, 1f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = 0.0046f;
                RenderSettings.fogColor = new Color(0.68f, 0.70f, 0.73f, 1f);
                RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.47f, 1f);
                cam.transform.position = radiaCamPos;
                cam.transform.LookAt(new Vector3(2.0f, 67.5f, 92.0f));
                game.Session.Notice = "";
            });
            Destroy(radiaMutantsRoot);
            Destroy(cherenkovLightsRoot);

            // =========================================================================
            // SHOT 11: ERA-V9 Industrial Power Grid (Nuclear Reactor + 3D Copper Wires +
            //          Solar + Battery + Lead Shielding) + Electric Train + Quad-Drone + Robot!
            // =========================================================================
            SetCamerasActive(cam, hands, false);
            VisualSettings.EnsurePostProcessingVolume(0f, false, 0f);
            radiaWorld.SwitchPlanet(Dimension.Lestingia, Array.Empty<SavedBlock>());
            spaceVisV9.ApplyPlanetAtmosphereAndGravity(Dimension.Lestingia, player, cam);
            int icx = 32, icz = 32, igy = 48;
            player.TeleportTo(new Vector3(icx, igy + 4f, icz), false);
            yield return WaitWorldMeshed(radiaWorld, 25f);

            // Flat industrial yard made of Reinforced Concrete (79), Asphalt (84), Hazard Steel (80) & Lead Shield (81)
            for (int ix = -16; ix <= 16; ix++)
                for (int iz = -14; iz <= 18; iz++)
                {
                    ushort floorBlk = Mathf.Abs(iz - 3) <= 2 ? BlockRegistry.AsphaltId
                                    : (ix < -4 && iz > 6) ? BlockRegistry.HazardSteelId
                                    : BlockRegistry.ReinforcedConcreteId;
                    for (int y = igy - 3; y <= igy; y++)
                        SetBlock(radiaWorld, icx + ix, y, icz + iz, y == igy ? floorBlk : 1);
                    for (int y = igy + 1; y <= igy + 18; y++)
                        SetBlock(radiaWorld, icx + ix, y, icz + iz, 0);
                }

            // Cooling water basin + VVER Nuclear Reactor (76) + Lead Shield wall (81) + Battery (73) + Solar array (75)
            SetBlock(radiaWorld, icx - 10, igy + 1, icz + 10, BlockRegistry.WaterId);
            SetBlock(radiaWorld, icx - 9, igy + 1, icz + 10, BlockRegistry.NuclearReactorId);
            SetBlock(radiaWorld, icx - 9, igy + 2, icz + 10, BlockRegistry.CherenkovBlockId);
            for (int sx = -12; sx <= -6; sx++)
            {
                SetBlock(radiaWorld, icx + sx, igy + 1, icz + 12, BlockRegistry.LeadShieldId);
                SetBlock(radiaWorld, icx + sx, igy + 2, icz + 12, BlockRegistry.LeadShieldId);
                SetBlock(radiaWorld, icx + sx, igy + 3, icz + 12, BlockRegistry.ArmoredGlassId);
            }
            SetBlock(radiaWorld, icx - 6, igy + 1, icz + 9, BlockRegistry.BatteryBankId);
            SetBlock(radiaWorld, icx - 5, igy + 1, icz + 9, 74);
            for (int sp = 0; sp < 4; sp++)
            {
                SetBlock(radiaWorld, icx + 5 + sp * 2, igy + 1, icz + 10, BlockRegistry.WhiteTrussId);
                SetBlock(radiaWorld, icx + 5 + sp * 2, igy + 2, icz + 10, 75);
            }
            // Overhead 3D Copper Power Line (72) connecting Reactor -> Battery -> Powered Rail -> Floodlight (77)
            for (int h = 1; h <= 4; h++)
            {
                SetBlock(radiaWorld, icx - 8, igy + h, icz + 9, BlockRegistry.CopperCableId);
                SetBlock(radiaWorld, icx + 4, igy + h, icz + 9, BlockRegistry.CopperCableId);
            }
            for (int wx = -8; wx <= 6; wx++)
                SetBlock(radiaWorld, icx + wx, igy + 4, icz + 9, BlockRegistry.CopperCableId);
            SetBlock(radiaWorld, icx + 6, igy + 5, icz + 9, BlockRegistry.ElectricFloodlightId);
            for (int ry = 1; ry <= 4; ry++)
                SetBlock(radiaWorld, icx, igy + ry, icz + 8, BlockRegistry.CopperCableId);

            // Electrified Railway Track (85 & 86) across yard + C4 charge stack on freight platform
            for (int rx = -15; rx <= 15; rx++)
            {
                SetBlock(radiaWorld, icx + rx, igy, icz + 2, 28);
                SetBlock(radiaWorld, icx + rx, igy + 1, icz + 2, (rx % 4 == 0) ? BlockRegistry.PoweredRailId : BlockRegistry.SteelRailId);
            }
            SetBlock(radiaWorld, icx, igy + 1, icz + 7, BlockRegistry.CopperCableId);
            SetBlock(radiaWorld, icx, igy + 1, icz + 6, BlockRegistry.CopperCableId);
            SetBlock(radiaWorld, icx, igy + 1, icz + 5, BlockRegistry.CopperCableId);
            SetBlock(radiaWorld, icx, igy + 1, icz + 4, BlockRegistry.CopperCableId);
            SetBlock(radiaWorld, icx, igy + 1, icz + 3, BlockRegistry.CopperCableId);
            SetBlock(radiaWorld, icx - 11, igy + 1, icz + 5, BlockRegistry.C4ChargeId);
            SetBlock(radiaWorld, icx + 11, igy + 1, icz + 6, BlockRegistry.HazardSteelId);

            yield return WaitWorldMeshed(radiaWorld, 20f);
            game.Tech.SpawnElectricTrain(new Vector3(icx + 2.5f, igy + 1.02f, icz + 2.5f));
            game.Tech.DeployIndustrialRobot(new Vector3(icx - 3.5f, igy + 1.02f, icz + 5.6f));
            game.Tech.DeployQuadDrone(new Vector3(icx + 4.2f, igy + 4.4f, icz + 5.2f), cam);
            player.TeleportTo(new Vector3(icx + 0.5f, igy + 2.0f, icz - 7.5f), false);
            for (int s = 0; s < 6; s++)
                game.Tech.StepRuntime(0.25f, radiaWorld, player, game.Session, cam, 0f, 0f, 0f, 0f, 0f, out _, out _);

            inv.selected = 2;
            inv.revision++;
            yield return RenderAndCapture(cam, hands, true, Path.Combine(outDir, "11-industrial-nuclear-grid-train-drone-robot.png"), () =>
            {
                game.Session.Data.clock = 585.0;
                RenderSettings.fog = true;
                RenderSettings.fogDensity = 0.0022f;
                cam.fieldOfView = 70f;
                cam.farClipPlane = 500f;
                cam.transform.position = new Vector3(icx + 1.2f, igy + 5.4f, icz - 8.2f);
                cam.transform.LookAt(new Vector3(icx - 1.2f, igy + 2.4f, icz + 6.5f));
                game.Session.Notice = "Энергосеть активна: Реактор ВВЭР + Солнечный массив · Электропоезд «ЭР-Воксель» · Квадрокоптер · «Титан-М»";
            });

            // =========================================================================
            // SHOT 12: Secret Command «/give 666» Thermonuclear Detonation —
            //          Colossal 95m 3D Fireball & Mushroom Cloud, Wilson Ring & Trinitite Crater!
            // =========================================================================
            ChatCommands.Execute("/give 666", false, true, inv, radiaWorld.Seed, player.transform.position, 1);
            inv.selected = Array.FindIndex(inv.slots, s => s.item == Items.ThermonukeId);
            if (inv.selected < 0) inv.selected = 0;
            inv.revision++;

            Vector3 nukeOrigin = new Vector3(icx + 2.0f, igy + 1.0f, icz + 52.0f);
            player.TeleportTo(nukeOrigin + Vector3.up * 4f, false);
            yield return WaitWorldMeshed(radiaWorld, 15f);
            player.TeleportTo(new Vector3(icx + 2.0f, igy + 18.0f, icz - 46.0f), false);
            game.Tech.ArmExplosive(nukeOrigin, true);
            // Advance 9.6s so 8.0s countdown completes and 3D Mushroom Cloud rises in peak glowing glory
            for (int nStep = 0; nStep < 48; nStep++)
                game.Tech.StepRuntime(0.20f, radiaWorld, player, game.Session, cam, 0f, 0f, 0f, 0f, 0f, out _, out _);
            yield return WaitWorldMeshed(radiaWorld, 15f);

            yield return RenderAndCapture(cam, hands, true, Path.Combine(outDir, "12-secret-666-thermonuclear-mushroom.png"), () =>
            {
                game.Session.Data.clock = 1160.0; // Dramatic dusk lighting so incandescent mushroom cloud illuminates sky & crater!
                cam.fieldOfView = 72f;
                cam.farClipPlane = 800f;
                cam.transform.position = new Vector3(icx + 2.0f, igy + 20.0f, icz - 48.0f);
                cam.transform.LookAt(nukeOrigin + Vector3.up * 32.0f);
                game.Session.Notice = "☢ СЕКРЕТНЫЙ ОБЪЕКТ 666 (/give 666) · Термоядерный гриб и оплавленный кратер тринитита!";
            });

            finishing = true;
            Debug.Log("[Showcase] PASS: 14 authentic 1920x1080 URP screenshots saved to " + outDir);
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
            if (hands)
            {
                if (hands.ViewCamera) hands.ViewCamera.enabled = showHands;
                foreach (var r in hands.GetComponentsInChildren<Renderer>(true))
                    if (r) r.enabled = showHands;
            }
            if (lockPose != null) lockPose();
            yield return new WaitForEndOfFrame();
            var snap = ScreenCapture.CaptureScreenshotAsTexture();
            if (hands)
                foreach (var r in hands.GetComponentsInChildren<Renderer>(true))
                    if (r) r.enabled = true;
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
