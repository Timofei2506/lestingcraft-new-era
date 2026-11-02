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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-lc-visual-review") < 0) return;
            var obj = new GameObject("Forest visual review probe");
            DontDestroyOnLoad(obj);
            obj.AddComponent<VisualReviewProbe>();
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
            var modelRoot = new GameObject("VisualReviewModels");
            Vector3 forward = viewDir;
            Vector3 stage, modelCam;
            if (!FindReviewStage(eye, seed, content, forward, out stage, out modelCam, 8f, 3.2f, 1.0f, 2, 4, 5f, 14f))
            { Fail("Could not find an unobstructed wildlife review stage."); yield break; }
            modelRoot.transform.position = stage;
            Vector3 animalRight = Vector3.Cross(Vector3.up, (stage - modelCam).normalized).normalized;
            int[] kinds = { 0, 3, 5 };
            for (int i = 0; i < kinds.Length; i++)
            {
                var animal = new GameObject("ReviewAnimal" + kinds[i]).AddComponent<AnimalVisual>();
                animal.transform.SetParent(modelRoot.transform, false);
                animal.transform.localPosition = animalRight * ((i - 1) * 2.2f);
                animal.Build(kinds[i]);
                animal.Animate(.016f, 0);
            }
            cam.transform.position = modelCam;
            cam.fieldOfView = 60;
            cam.transform.LookAt(stage + Vector3.up * .9f);
            yield return Capture(Path.Combine(outDir, "04-wildlife-models.png"), null);
            Destroy(modelRoot);

            Vector3 bossPos, bossCam;
            if (!FindReviewStage(eye, seed, content, forward, out bossPos, out bossCam, 18f, 6f, 4f, 3, 8, 10f, 30f))
            { Fail("Could not find an unobstructed boss review arena."); yield break; }
            var boss = PlanetaryBossController.SpawnForPlanet(game.Session.World.transform, Dimension.Lestingia, game.Session.World, seed, false, bossPos);
            if (!boss) { Fail("Lestingia boss did not spawn for visual review."); yield break; }
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

            Debug.Log("[VisualReview] PASS: 7 real-player review frames saved to " + outDir);
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
            for (float r = minRadius; r <= maxRadius; r += 1f)
                for (int a = 0; a < 24; a++)
                {
                    Vector3 dir = Quaternion.Euler(0, a * 15f, 0) * preferred;
                    Vector3 candidate = eye + dir * r;
                    int gx = Mathf.RoundToInt(candidate.x), gz = Mathf.RoundToInt(candidate.z);
                    int gy = TerrainGenerator.Height(gx, gz, seed, Dimension.Lestingia);
                    Vector3 ground = new Vector3(gx + .5f, gy + 1, gz + .5f);
                    if (!OpenColumn(ground, radius, clearance, seed, content)) continue;
                    for (int c = 0; c < 16; c++)
                    {
                        Vector3 camDir = Quaternion.Euler(0, c * 22.5f, 0) * preferred;
                        Vector3 cam = ground - camDir * distance + Vector3.up * height;
                        if (Occludes(cam, seed, content)) continue;
                        if (!ClearLine(cam, ground + Vector3.up * lookHeight, seed, content)) continue;
                        if (lookHeight > 2f)
                        {
                            Vector3 camRight = Vector3.Cross(Vector3.up, camDir).normalized;
                            bool wideClear = ClearLine(cam, ground + Vector3.up * (lookHeight + 3f), seed, content);
                            for (int side = -1; side <= 1 && wideClear; side += 2)
                            {
                                Vector3 edge = ground + camRight * (side * 2.6f);
                                wideClear = ClearLine(cam, edge + Vector3.up * lookHeight, seed, content) &&
                                            ClearLine(cam, edge + Vector3.up * (lookHeight + 2.5f), seed, content);
                            }
                            if (!wideClear) continue;
                        }
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
