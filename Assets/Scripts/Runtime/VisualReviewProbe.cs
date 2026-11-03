using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;
using LestingCraft.World;

namespace LestingCraft.Runtime
{
    /// <summary>
    /// Reproducible visual review pass for the forest/reference iteration.
    /// Activated only by -lc-visual-review. Captures real player frames, not editor mockups.
    /// The natural forest shot is deliberately NOT a staged biome showroom.
    /// </summary>
    public sealed class VisualReviewProbe : MonoBehaviour
    {
        bool finishing;
        float hardDeadline;
        string outDir;
        bool spaceGallery;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-lc-visual-review") < 0 && Array.IndexOf(args, "-lc-space-review") < 0) return;
            var obj = new GameObject("Forest visual review probe");
            DontDestroyOnLoad(obj);
            var probe = obj.AddComponent<VisualReviewProbe>();
            probe.spaceGallery = Array.IndexOf(args, "-lc-space-review") >= 0;
        }

        void Awake()
        {
            Application.logMessageReceived += OnLog;
            hardDeadline = Time.realtimeSinceStartup + 600f;
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }

        void OnLog(string condition, string trace, LogType type)
        {
            if (!finishing && (type == LogType.Exception || type == LogType.Error))
                Fail("Unhandled Unity " + type + ": " + condition + "\n" + trace);
        }

        void Update()
        {
            if (!finishing && Time.realtimeSinceStartup > hardDeadline)
                Fail("Visual review exceeded 600s wall deadline.");
        }

        IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
            Screen.SetResolution(1600, 900, false);
            yield return null;
            yield return null;
            if (spaceGallery) { yield return SpaceGallery(); yield break; }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Fail("A graphics device is required for visual review.");
                yield break;
            }
            var game = FindFirstObjectByType<PrototypeGame>();
            var ui = FindFirstObjectByType<GameUI>();
            if (!game || !ui) { Fail("PrototypeGame or GameUI missing."); yield break; }
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((!ui.IsReady || !SplashScreen.isFinished) && Time.realtimeSinceStartup < deadline) yield return null;
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "VisualReview"));
            Directory.CreateDirectory(outDir);

            // Honest desktop High preset; no hidden software-GPU downgrade of the look.
            VisualSettings.Current.HighDefault();
            VisualSettings.Current.VolumetricQuality = 1;
            VisualSettings.Current.Save();

            yield return Capture(Path.Combine(outDir, "01-menu.png"), null);
            game.ShowcaseStartFreshWorld();
            yield return null;
            deadline = Time.realtimeSinceStartup + 240f;
            while ((!game.WorldReady || !game.Session || !game.Session.World.IsIdleForShowcase) && Time.realtimeSinceStartup < deadline)
            {
                if (game.Session)
                {
                    game.Session.Data.clock = 500;
                    game.Session.World.ShowcasePumpSync();
                }
                yield return null;
            }
            if (!game.WorldReady || !game.Session) { Fail("World failed to initialize for visual review."); yield break; }
            deadline = Time.realtimeSinceStartup + 60f;
            while (game.StoryLocked && Time.realtimeSinceStartup < deadline) yield return null;

            var player = game.Session.Player;
            var cam = Camera.main;
            var hands = player.GetComponent<FirstPersonHands>();
            player.enabled = false;
            var inv = game.Session.Inventory;
            for (int i = 0; i < inv.slots.Length; i++) inv.slots[i] = new ItemStack();
            inv.slots[0] = new ItemStack { item = 100, count = 1, durability = Items.Get(100).Durability };
            inv.slots[1] = new ItemStack { item = 4, count = 16 };
            inv.slots[2] = new ItemStack { item = 1, count = 16 };
            inv.slots[3] = new ItemStack { item = 18, count = 16 };
            inv.selected = 0;
            inv.revision++;
            game.Session.Vitals.health = 100;
            game.Session.Vitals.hunger = 94;
            game.Session.Vitals.stamina = 100;
            game.Session.Notice = "";

            int seed = game.Session.World.Seed;
            int content = game.Session.World.ContentVersion;
            int bestX = 0, bestZ = 0, bestY = 50, bestScore = -1;
            Vector3 bestDir = Vector3.forward;
            for (int x = -96; x <= 96; x += 4)
                for (int z = -96; z <= 96; z += 4)
                {
                    int h = TerrainGenerator.Height(x, z, seed, Dimension.Lestingia);
                    if (h < 42 || h > 66) continue;
                    if (TerrainGenerator.SampleContent(x, h, z, seed, Dimension.Lestingia, content) != BlockRegistry.GrassId) continue;
                    if (TerrainGenerator.SampleContent(x, h + 1, z, seed, Dimension.Lestingia, content) != 0) continue;
                    bool clearColumn = true;
                    for (int dy = 2; dy <= 14; dy++)
                    {
                        int id = TerrainGenerator.SampleContent(x, h + dy, z, seed, Dimension.Lestingia, content);
                        if (id == BlockRegistry.WoodId || id == BlockRegistry.LeavesId) { clearColumn = false; break; }
                    }
                    if (!clearColumn) continue;
                    int score = 0;
                    Vector3 dir = Vector3.zero;
                    for (int dx = -10; dx <= 10; dx += 2)
                        for (int dz = -10; dz <= 10; dz += 2)
                            for (int dy = 2; dy <= 12; dy += 2)
                            {
                                int id = TerrainGenerator.SampleContent(x + dx, h + dy, z + dz, seed, Dimension.Lestingia, content);
                                if (id == BlockRegistry.LeavesId) { score++; dir += new Vector3(dx, 0, dz); }
                                else if (id == BlockRegistry.WoodId) score += 2;
                            }
                    if (score > bestScore)
                    {
                        bestScore = score; bestX = x; bestZ = z; bestY = h;
                        bestDir = dir.sqrMagnitude > .01f ? dir.normalized : Vector3.forward;
                    }
                }
            if (bestScore < 12) { Fail("Could not find a natural forest clearing for seed 2506."); yield break; }
            Debug.Log("[VisualReview] Forest clearing: " + bestX + "," + bestY + "," + bestZ + " score=" + bestScore);

            Vector3 eye = new Vector3(bestX + .5f, bestY + 2.6f, bestZ + .5f);
            Vector3 viewDir = ChooseViewDirection(eye, seed, content, bestDir);
            player.TeleportTo(eye - Vector3.up * 1.6f, false);
            player.transform.rotation = Quaternion.LookRotation(viewDir, Vector3.up);
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation((eye + viewDir * 12 + Vector3.down * 1.2f) - eye, Vector3.up);
            cam.fieldOfView = 70;
            game.Session.Data.clock = 500;
            yield return WaitWorld(game.Session.World, 90);
            ui.ShowHUD();
            var contentRoot = ui.transform.Find("1600x900 Interface/Content");
            if (contentRoot) contentRoot.gameObject.SetActive(false);
            yield return Capture(Path.Combine(outDir, "02-forest-reference.png"), hands);
            if (contentRoot) contentRoot.gameObject.SetActive(true);
            yield return Capture(Path.Combine(outDir, "03-forest-hud.png"), hands);
            if (hands) { if (hands.ViewCamera) hands.ViewCamera.enabled = false; hands.enabled = false; }
            if (contentRoot) contentRoot.gameObject.SetActive(false);

            // Model review: choose natural open stages with a verified line of sight instead of
            // hiding the subject behind the nearest trunk. The world itself is not edited.
            Vector3 forward = viewDir;
            Vector3 bossPos, bossCam;
            Debug.Log("[VisualReview] Searching boss arena...");
            if (!FindReviewStage(eye, seed, content, forward, out bossPos, out bossCam, 13f, 4.5f, 3f, 3, 8, 9f, 20f))
            { Fail("Could not find an unobstructed boss review arena."); yield break; }
            var boss = PlanetaryBossController.SpawnForPlanet(game.Session.World.transform, Dimension.Lestingia, game.Session.World, seed, false, bossPos);
            if (!boss) { Fail("Lestingia boss did not spawn for visual review."); yield break; }
            Debug.Log("[VisualReview] Boss arena: " + bossPos);
            boss.PlaceArena(bossPos);
            cam.transform.position = bossCam;
            cam.fieldOfView = 65;
            cam.transform.LookAt(bossPos + Vector3.up * 4.2f);
            yield return WaitWorld(game.Session.World, 45);
            // Drive the real telegraph state so the shockwave ring is visible without damaging the player.
            Vector3 viewer = bossPos + (bossCam - bossPos).normalized * 5f;
            viewer.y = bossPos.y + 1f;
            boss.TickBoss(5f, viewer, null);
            boss.TickBoss(.45f, viewer, null);
            yield return null;
            yield return Capture(Path.Combine(outDir, "05-boss-ring.png"), null);

            // Wildlife gets the arena floor: it is carved open, lit and free of trunks, so the
            // coats and the new heads are judged on their own instead of in deep canopy shade.
            if (boss) boss.gameObject.SetActive(false);
            Vector3 arenaCentre = bossPos + Vector3.up * 1.0f;
            int[] kinds = { 0, 1, 3, 4, 6, 10 };
            AnimalVisual[] cast = new AnimalVisual[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                var animal = new GameObject("ReviewAnimal" + kinds[i]).AddComponent<AnimalVisual>();
                animal.transform.position = bossPos + new Vector3((i - (kinds.Length - 1) * .5f) * 2.1f, .1f, 4.6f);
                // Nose points to local -Z, and the camera sits at +Z: keep yaw 0 so the faces
                // are what the player sees, not three rumps in a row.
                animal.transform.rotation = Quaternion.identity;
                animal.Build(kinds[i]);
                animal.Animate(.016f, 0f);
                cast[i] = animal;
            }
            cam.transform.position = bossPos + new Vector3(0f, 3.1f, 12.5f);
            cam.fieldOfView = 55;
            cam.transform.LookAt(arenaCentre + Vector3.up * .2f);
            yield return WaitWorld(game.Session.World, 20);
            yield return Capture(Path.Combine(outDir, "04-wildlife-models.png"), null);
            for (int i = 0; i < cast.Length; i++) if (cast[i]) Destroy(cast[i].gameObject);
            yield return null;

            // All five archetypes stand in the same arena, on the same light, none hidden behind
            // a boss rig. This has to happen while the world camera is live: once the game returns
            // to the menu the background camera takes over and every frame renders the same.
            var lineRoot = new GameObject("BossSculptLineup");
            lineRoot.transform.position = bossPos + new Vector3(0f, .1f, 3.2f);
            string[] keys = { "warden", "colossus", "archon", "titan", "ember" };
            var lineMaterial = Resources.Load<Material>("Generated/VoxelObjects");
            for (int i = 0; i < keys.Length; i++)
            {
                var built = BossSculpture.Build(lineRoot.transform, keys[i], lineMaterial);
                built.Root.transform.localPosition = new Vector3((i - 2) * 3.9f, 0f, 0f);
                built.Root.transform.localRotation = Quaternion.Euler(0f, 22f, 0f);
            }
            // Pivot the arena so we look back into the world, not past it at the sky horizon.
            float lineupYaw = Quaternion.LookRotation(-(bossCam - bossPos).normalized, Vector3.up).eulerAngles.y;
            lineRoot.transform.rotation = Quaternion.Euler(0f, lineupYaw, 0f);
            cam.transform.position = bossPos + Quaternion.Euler(0f, lineupYaw, 0f) * new Vector3(0f, 5.2f, 17.5f);
            cam.fieldOfView = 52;
            cam.transform.LookAt(bossPos + Vector3.up * 2.8f);
            yield return WaitWorld(game.Session.World, 20);
            yield return Capture(Path.Combine(outDir, "08-boss-lineup.png"), null);
            Destroy(lineRoot);
            yield return null;

            game.Session.Inventory.slots[4] = new ItemStack { item = 55, count = 2 };
            game.Session.Inventory.slots[5] = new ItemStack { item = 56, count = 4 };
            game.Session.Inventory.revision++;
            if (contentRoot) contentRoot.gameObject.SetActive(true);
            game.ToggleInventory();
            yield return null;
            yield return null;
            if (ui.ScreenName != "Inventory") { Fail("Inventory page did not open: " + ui.ScreenName); yield break; }
            ui.ShowItemTooltip(100, new Vector2(620, 480), Items.Get(100).Durability / 2);
            yield return null;
            yield return Capture(Path.Combine(outDir, "06-inventory-tooltip.png"), null);

            game.ReturnToMenu();
            deadline = Time.realtimeSinceStartup + 30f;
            while (game.InWorld && Time.realtimeSinceStartup < deadline) yield return null;
            ui.ShowHome();
            foreach (var button in ui.GetComponentsInChildren<Button>())
                if (button.name == "Home.Settings") { button.onClick.Invoke(); break; }
            yield return Capture(Path.Combine(outDir, "07-settings-presets.png"), null);

            Debug.Log("[VisualReview] PASS: 8 real-player review frames saved to " + outDir);
            finishing = true;
            Application.Quit(0);
        }

        /// <summary>Space review: the real cockpit rig with the real celestial dome, five compositions.</summary>
        IEnumerator SpaceGallery()
        {
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "VisualReview"));
            Directory.CreateDirectory(outDir);
            var game = FindFirstObjectByType<PrototypeGame>();
            var ui = FindFirstObjectByType<GameUI>();
            if (!game || !ui) { Fail("PrototypeGame or GameUI missing."); yield break; }
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((!ui.IsReady || !SplashScreen.isFinished) && Time.realtimeSinceStartup < deadline) yield return null;
            var contentRoot = ui.transform.Find("1600x900 Interface/Content");
            if (contentRoot) contentRoot.gameObject.SetActive(false);
            var cam = Camera.main;
            if (!cam) { Fail("No main camera in space review."); yield break; }

            VisualSettings.Current.HighDefault();
            VisualSettings.Current.VolumetricQuality = 1;
            VisualSettings.Current.Save();

            var helm = game.SpaceHelm;
            helm.Active = true;
            helm.CurrentSystem = StarSystemId.SolarSystem;
            helm.Rotation = Quaternion.Euler(-6f, 34f, 0f);
            helm.PositionKm = new SpaceVector(CelestialAtlas.AstronomicalUnitKm + 8200.0, 900.0, 0.0);
            helm.VelocityKmPerSec = new SpaceVector(0.0, 0.0, 7.66);
            var visuals = SpaceFlightVisuals.Ensure(null);
            visuals.SetOpenSpaceFlightActive(helm, null, cam.transform);
            yield return null;
            yield return null;
            yield return Capture(Path.Combine(outDir, "09-space-orbit.png"), null);

            // Outer-system composition: ringed giant and its moons.
            helm.CurrentSystem = StarSystemId.SolarSystem;
            helm.PositionKm = CelestialAtlas.WorldPositionKm(CelestialBodyId.Saturn, 12.0) + new SpaceVector(900000.0, 220000.0, 900000.0);
            helm.Rotation = Quaternion.Euler(-12f, 128f, 0f);
            visuals.SetOpenSpaceFlightActive(helm, null, cam.transform);
            yield return null;
            yield return null;
            yield return Capture(Path.Combine(outDir, "10-space-saturn.png"), null);

            // Deep space: Andromeda rim with the procedural dome skyline.
            helm.CurrentGalaxy = GalaxyId.Andromeda;
            helm.CurrentSystem = StarSystemId.AndromedaCore;
            helm.PositionKm = new SpaceVector(2.4e9, 1.2e8, -8.0e8);
            helm.Rotation = Quaternion.Euler(-4f, 208f, 0f);
            visuals.SetOpenSpaceFlightActive(helm, null, cam.transform);
            yield return null;
            yield return null;
            yield return Capture(Path.Combine(outDir, "11-space-andromeda.png"), null);

            // Hyperjump framing: the same place the player sees during a jump.
            helm.InHyperjump = true;
            helm.HyperjumpProgress = .45f;
            visuals.SetOpenSpaceFlightActive(helm, null, cam.transform);
            yield return null;
            yield return Capture(Path.Combine(outDir, "12-space-hyperjump.png"), null);
            helm.InHyperjump = false;
            helm.HyperjumpProgress = 0f;

            // Orbital station and Earth, the busiest composition.
            helm.CurrentGalaxy = GalaxyId.MilkyWay;
            helm.CurrentSystem = StarSystemId.SolarSystem;
            helm.PositionKm = CelestialAtlas.WorldPositionKm(CelestialBodyId.Earth, 12.0) + new SpaceVector(16000.0, 4200.0, 16000.0);
            helm.Rotation = Quaternion.Euler(-10f, 292f, 0f);
            visuals.SetOpenSpaceFlightActive(helm, null, cam.transform);
            yield return null;
            yield return null;
            yield return Capture(Path.Combine(outDir, "13-space-earth-station.png"), null);

            Debug.Log("[VisualReview] PASS: 5 space review frames saved to " + outDir);
            finishing = true;
            Application.Quit(0);
        }

        static int BlockAt(Vector3 p, int seed, int content)
        {
            return TerrainGenerator.SampleContent(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z), seed, Dimension.Lestingia, content);
        }

        static bool Occludes(Vector3 p, int seed, int content)
        {
            int id = BlockAt(p, seed, content);
            return id != 0 && id != BlockRegistry.GrassId;
        }

        static bool ClearLine(Vector3 a, Vector3 b, int seed, int content)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) * 2f));
            for (int i = 1; i < steps; i++)
                if (Occludes(Vector3.Lerp(a, b, i / (float)steps), seed, content)) return false;
            return true;
        }

        static bool OpenColumn(Vector3 ground, int radius, int height, int seed, int content)
        {
            int gx = Mathf.FloorToInt(ground.x), gz = Mathf.FloorToInt(ground.z), gy = Mathf.FloorToInt(ground.y);
            for (int dx = -radius; dx <= radius; dx++)
                for (int dz = -radius; dz <= radius; dz++)
                {
                    int h = TerrainGenerator.Height(gx + dx, gz + dz, seed, Dimension.Lestingia);
                    if (Mathf.Abs(h - (gy - 1)) > 1) return false;
                    for (int y = 0; y <= height; y++)
                        if (BlockAt(new Vector3(gx + dx + .5f, gy + y, gz + dz + .5f), seed, content) != 0) return false;
                }
            return true;
        }

        static Vector3 ChooseViewDirection(Vector3 eye, int seed, int content, Vector3 fallback)
        {
            Vector3 bestDir = new Vector3(fallback.x, 0, fallback.z).normalized;
            if (bestDir.sqrMagnitude < .5f) bestDir = Vector3.forward;
            float best = float.NegativeInfinity;
            for (int i = 0; i < 24; i++)
            {
                Vector3 dir = Quaternion.Euler(0, i * 15f, 0) * Vector3.forward;
                float score = 0; bool clear = true;
                for (int s = 2; s <= 16; s += 2)
                    for (int h = -1; h <= 2; h++)
                    {
                        int id = BlockAt(eye + dir * s + Vector3.up * h, seed, content);
                        if (id == BlockRegistry.WoodId) { score -= 12; clear = false; }
                        else if (id == BlockRegistry.LeavesId) score += h >= 1 ? 2 : -4;
                        else if (id == 0) score += 1;
                    }
                if (clear) score += 20;
                if (score > best) { best = score; bestDir = dir; }
            }
            return bestDir;
        }

        static bool FindReviewStage(Vector3 eye, int seed, int content, Vector3 preferred, out Vector3 stage, out Vector3 camera,
            float distance, float height, float lookHeight, int radius, int clearance, float minRadius, float maxRadius)
        {
            stage = camera = Vector3.zero;
            float best = float.NegativeInfinity;
            for (float r = minRadius; r <= maxRadius; r += 2f)
                for (int a = 0; a < 12; a++)
                {
                    Vector3 dir = Quaternion.Euler(0, a * 30f, 0) * preferred;
                    Vector3 candidate = eye + dir * r;
                    int gx = Mathf.RoundToInt(candidate.x), gz = Mathf.RoundToInt(candidate.z);
                    int gy = TerrainGenerator.Height(gx, gz, seed, Dimension.Lestingia);
                    Vector3 ground = new Vector3(gx + .5f, gy + 1, gz + .5f);
                    if (!OpenColumn(ground, radius, clearance, seed, content)) continue;
                    for (int c = 0; c < 8; c++)
                    {
                        Vector3 camDir = Quaternion.Euler(0, c * 45f, 0) * preferred;
                        Vector3 cam = ground - camDir * distance + Vector3.up * height;
                        if (Occludes(cam, seed, content)) continue;
                        if (!ClearLine(cam, ground + Vector3.up * lookHeight, seed, content)) continue;
                        if (lookHeight > 2f && !ClearLine(cam, ground + Vector3.up * (lookHeight + 3f), seed, content)) continue;
                        float score = 20 - Mathf.Abs(r - (minRadius + maxRadius) * .5f) - Vector3.Angle(camDir, preferred) * .05f;
                        if (score > best) { best = score; stage = ground; camera = cam; }
                    }
                }
            return best > float.NegativeInfinity;
        }

        IEnumerator WaitWorld(VoxelWorld world, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            yield return null;
            if (world) world.ShowcasePumpSync();
            while (world && !world.IsIdleForShowcase && Time.realtimeSinceStartup < deadline)
            {
                world.ShowcasePumpSync();
                yield return null;
            }
        }

        IEnumerator Capture(string path, FirstPersonHands hands)
        {
            if (finishing) yield break;
            if (hands && hands.ViewCamera) hands.ViewCamera.enabled = true;
            else { var anyHands = FindFirstObjectByType<FirstPersonHands>(); if (anyHands && anyHands.ViewCamera) anyHands.ViewCamera.enabled = false; }
            yield return new WaitForEndOfFrame();
            var snap = ScreenCapture.CaptureScreenshotAsTexture();
            if (!snap) { Fail("CaptureScreenshotAsTexture returned null: " + path); yield break; }
            if (!HasDetail(snap)) { Destroy(snap); Fail("Uniform/empty screenshot: " + path); yield break; }
            byte[] png = ImageConversion.EncodeToPNG(snap);
            Destroy(snap);
            File.WriteAllBytes(path, png);
            Debug.Log("[VisualReview] CAPTURED " + Path.GetFileName(path) + " (" + png.Length + " bytes)");
        }

        static bool HasDetail(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            long sum = 0, sumSq = 0;
            int count = 0;
            for (int i = 0; i < pixels.Length; i += 97)
            {
                int l = (pixels[i].r + pixels[i].g + pixels[i].b) / 3;
                sum += l; sumSq += (long)l * l; count++;
            }
            if (count == 0) return false;
            double mean = (double)sum / count;
            double variance = (double)sumSq / count - mean * mean;
            return variance > 25;
        }

        void Fail(string message)
        {
            if (finishing) return;
            finishing = true;
            Debug.LogError("[VisualReview] FAIL: " + message);
            Application.Quit(2);
        }
    }
}
