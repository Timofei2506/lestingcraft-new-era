using System;
using System.Collections;
using System.IO;
using UnityEngine;
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
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
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
            player.TeleportTo(eye - Vector3.up * 1.6f, false);
            player.transform.rotation = Quaternion.LookRotation(new Vector3(bestDir.x, 0, bestDir.z).normalized, Vector3.up);
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation((eye + bestDir * 12 + Vector3.down * 1.2f) - eye, Vector3.up);
            cam.fieldOfView = 70;
            game.Session.Data.clock = 500;
            yield return WaitWorld(game.Session.World, 90);
            ui.ShowHUD();
            var contentRoot = ui.transform.Find("1600x900 Interface/Content");
            if (contentRoot) contentRoot.gameObject.SetActive(false);
            yield return Capture(Path.Combine(outDir, "02-forest-reference.png"), hands);
            if (contentRoot) contentRoot.gameObject.SetActive(true);
            yield return Capture(Path.Combine(outDir, "03-forest-hud.png"), hands);

            // Model review: one articulated animal group and one planetary boss ring.
            var modelRoot = new GameObject("VisualReviewModels");
            Vector3 stage = eye + new Vector3(bestDir.x, 0, bestDir.z).normalized * 7f;
            stage.y = TerrainGenerator.Height(Mathf.RoundToInt(stage.x), Mathf.RoundToInt(stage.z), seed, Dimension.Lestingia) + 1;
            modelRoot.transform.position = stage;
            int[] kinds = { 0, 3, 5 };
            for (int i = 0; i < kinds.Length; i++)
            {
                var animal = new GameObject("ReviewAnimal" + kinds[i]).AddComponent<AnimalVisual>();
                animal.transform.SetParent(modelRoot.transform, false);
                animal.transform.localPosition = new Vector3((i - 1) * 2.2f, 0, 0);
                animal.Build(kinds[i]);
                animal.Animate(.016f, 0);
            }
            cam.transform.position = stage + new Vector3(0, 2.2f, -7);
            cam.transform.LookAt(stage + Vector3.up * .9f);
            yield return Capture(Path.Combine(outDir, "04-wildlife-models.png"), null);
            Destroy(modelRoot);

            var boss = PlanetaryBossController.SpawnForPlanet(game.Session.World.transform, Dimension.Lestingia, game.Session.World, seed, false,
                stage - new Vector3(34, 0, 22));
            if (boss)
            {
                Vector3 bossPos = boss.transform.position;
                cam.transform.position = bossPos + new Vector3(0, 4.5f, -13);
                cam.transform.LookAt(bossPos + Vector3.up * 3.6f);
                yield return WaitWorld(game.Session.World, 45);
                yield return Capture(Path.Combine(outDir, "05-boss-ring.png"), null);
            }

            game.Session.Inventory.slots[4] = new ItemStack { item = 55, count = 2 };
            game.Session.Inventory.slots[5] = new ItemStack { item = 56, count = 4 };
            game.Session.Inventory.revision++;
            ui.ShowInventory();
            ui.ShowItemTooltip(100, new Vector2(620, 480), Items.Get(100).Durability / 2);
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
