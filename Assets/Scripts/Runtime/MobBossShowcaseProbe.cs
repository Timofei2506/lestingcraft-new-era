using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.World;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;

namespace LestingCraft.Runtime
{
    /// <summary>
    /// Renders real in-engine screenshots of every wildlife mob and every boss/drone/bacteria
    /// model rendered against a plain neutral-gray backdrop, framed in an isometric editor-style
    /// view so the voxel models read clearly. Activated strictly when "-lc-mobs" is passed on the
    /// command line. Produces ShowcaseResults/mobs/*.png and ShowcaseResults/bosses/*.png at 1920x1080.
    /// </summary>
    public sealed class MobBossShowcaseProbe : MonoBehaviour
    {
        static bool finishing;
        Camera showcaseCam;
        Light sun;
        GameObject floorRoot;
        PrototypeGame game;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-lc-mobs") < 0) return;
            var obj = new GameObject("MobBoss Showcase Capture");
            DontDestroyOnLoad(obj);
            obj.AddComponent<MobBossShowcaseProbe>();
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
                Fail("Unhandled Unity " + type + ": " + condition + "\n" + trace);
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

            game = FindFirstObjectByType<PrototypeGame>();
            var ui = FindFirstObjectByType<GameUI>();
            if (!game || !ui)
            {
                Fail("PrototypeGame or GameUI missing.");
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + 30f;
            while ((!ui.IsReady || !SplashScreen.isFinished) && Time.realtimeSinceStartup < deadline)
                yield return null;

            // Software rasterisation on CI: drop expensive volumetrics so the voxel floor
            // and models render fast and deterministically.
            if ((SystemInfo.graphicsDeviceName ?? "").IndexOf("llvmpipe", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                VisualSettings.Current.HighDefault();
                VisualSettings.Current.AmbientOcclusion = false;
                VisualSettings.Current.VolumetricQuality = 0;
                VisualSettings.Current.Save();
            }
            else
            {
                VisualSettings.Current.Ultra();
            }

            ui.ShowHome();
            game.ShowcaseStartFreshWorld();
            yield return null;

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
            if (!game.WorldReady || !game.Session || game.ChunkCount < 5)
            {
                Fail("World failed to initialize for mob showcase (chunks=" + game.ChunkCount + ").");
                yield break;
            }

            var world = game.Session.World;
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ShowcaseResults"));
            string mobsDir = Path.Combine(outDir, "mobs");
            string bossesDir = Path.Combine(outDir, "bosses");
            Directory.CreateDirectory(mobsDir);
            Directory.CreateDirectory(bossesDir);

            // ---- Flat voxel floor + showcase camera + sun ----
            int fx = Mathf.Clamp(Mathf.RoundToInt(game.Session.Player.transform.position.x) + 60, -160, 160);
            int fz = Mathf.Clamp(Mathf.RoundToInt(game.Session.Player.transform.position.z) + 60, -160, 160);
            int gy = FindSurfaceY(world, fx, fz);
            BuildFlatFloor(world, fx, gy, fz, 26);

            showcaseCam = CreateShowcaseCamera();
            sun = CreateSun();
            yield return new WaitForEndOfFrame();

            // ---- Wildlife mobs ----
            var mobRoot = new GameObject("MobShowcaseRig");
            for (int kind = 0; kind < AnimalVisual.SpeciesNames.Length; kind++)
            {
                string file = Path.Combine(mobsDir, Pad(kind) + "-" + Safe(AnimalVisual.SpeciesNames[kind]) + ".png");
                yield return CaptureModel(mobRoot, file, () =>
                {
                    var go = new GameObject("Mob_" + kind);
                    go.transform.SetParent(mobRoot.transform, false);
                    go.transform.position = new Vector3(fx, gy + .02f, fz);
                    var vis = go.AddComponent<AnimalVisual>();
                    vis.Build(kind);
                    return go;
                });
                Destroy(mobRoot, 0.01f);
                mobRoot = new GameObject("MobShowcaseRig");
            }

            // ---- Bosses, drones, bacteria ----
            var bossRoot = new GameObject("BossShowcaseRig");
            int bi = 0;
            foreach (var profile in BossCatalog.All())
            {
                string file = Path.Combine(bossesDir, Pad(bi) + "-" + Safe(profile.Title) + ".png");
                yield return CaptureModel(bossRoot, file, () =>
                {
                    var data = BossSculpts.For(profile.Dimension, 1000 + bi);
                    return BuildSculpt(bossRoot, data, profile.Scale);
                });
                bi++;
            }
            foreach (var profile in BossCatalog.ExtraProfiles)
            {
                string file = Path.Combine(bossesDir, Pad(bi) + "-" + Safe(profile.Title) + ".png");
                yield return CaptureModel(bossRoot, file, () =>
                {
                    var data = BossSculpts.Extra(profile.Name, 2000 + bi);
                    return BuildSculpt(bossRoot, data, profile.Scale);
                });
                bi++;
            }
            int di = 0;
            foreach (var kind in (DroneKind[])Enum.GetValues(typeof(DroneKind)))
            {
                var profile = DroneRoster.For(kind);
                string file = Path.Combine(bossesDir, Pad(bi) + "-" + Safe(profile.Codename) + ".png");
                yield return CaptureModel(bossRoot, file, () =>
                {
                    var data = BossSculpts.Drone(kind);
                    return BuildSculpt(bossRoot, data, profile.Size);
                });
                bi++; di++;
            }
            {
                string file = Path.Combine(bossesDir, Pad(bi) + "-BlackBacteria.png");
                yield return CaptureModel(bossRoot, file, () =>
                {
                    var data = BossSculpts.BacteriaMob(4242);
                    return BuildSculpt(bossRoot, data, 1.0f);
                });
            }

            Destroy(bossRoot, 0.01f);
            Debug.Log("[MobShowcase] PASS: saved mob + boss screenshots to " + outDir);
            finishing = true;
            Application.Quit(0);
        }

        static GameObject BuildSculpt(GameObject parent, SculptData data, float scale)
        {
            // BossSculpture.Build is static: it creates its own host GameObject parented to
            // parent.transform, so we just call it and apply the archetype scale afterwards.
            var sculpt = BossSculpture.Build(parent.transform, data, BossSculpture.SharedVoxelMaterial());
            sculpt.transform.localScale = Vector3.one * scale;
            return sculpt.gameObject;
        }

        /// <summary>Clears a flat voxel plate area so the showcase renders against a plain gray
        /// backdrop (no floor or terrain) — the model floats centered on solid gray.</summary>
        static void BuildFlatFloor(VoxelWorld world, int cx, int gy, int cz, int half)
        {
            for (int dx = -half; dx <= half; dx++)
                for (int dz = -half; dz <= half; dz++)
                {
                    for (int y = gy - 2; y <= gy + 16; y++)
                        world.ShowcaseEditUnbounded(cx + dx, y, cz + dz, 0);
                }
        }

        static Camera CreateShowcaseCamera()
        {
            var camObj = new GameObject("MobShowcaseCamera");
            camObj.transform.position = new Vector3(0, 12, 12);
            camObj.transform.LookAt(Vector3.zero);
            var cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 42f;
            cam.nearClipPlane = .1f;
            cam.farClipPlane = 400f;
            cam.allowHDR = false;
            // Plain neutral-gray backdrop so voxel models read clearly, like a product turnaround.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.70f, .71f, .73f, 1f);
            cam.enabled = false;
            return cam;
        }

        static Light CreateSun()
        {
            var go = new GameObject("MobShowcaseSun");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, .96f, .88f);
            light.intensity = 2.2f;
            light.shadows = LightShadows.Soft;
            light.shadowBias = .03f;
            light.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
            return light;
        }

        /// <summary>Frames the showcase camera isometrically around a model of given height, then captures.</summary>
        IEnumerator CaptureModel(GameObject rig, string fullPath, Func<GameObject> spawn)
        {
            Debug.Log("[MobShowcase] START: " + Path.GetFileName(fullPath));
            var model = spawn();
            // Let the model's meshes register before measuring.
            for (int i = 0; i < 2; i++) yield return null;

            float height = MeasureHeight(model);
            float dist = Mathf.Max(2.4f, height * 3.1f);
            float camY = Mathf.Max(1.6f, height * 1.9f) + .4f;
            Vector3 look = model.transform.position;
            showcaseCam.transform.position = new Vector3(look.x + dist * .62f, look.y + camY, look.z + dist * .62f);
            showcaseCam.transform.LookAt(new Vector3(look.x, look.y + height * .45f, look.z));

            // Hide everything except the showcase camera + sun + the model rig.
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam && cam != showcaseCam) cam.enabled = false;
            showcaseCam.enabled = true;
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            var snap = ScreenCapture.CaptureScreenshotAsTexture();
            showcaseCam.enabled = false;
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
            Debug.Log("[MobShowcase] CAPTURED: " + fullPath);
        }

        static float MeasureHeight(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            float top = -1e9f, bottom = 1e9f;
            bool any = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i]) continue;
                any = true;
                top = Mathf.Max(top, renderers[i].bounds.max.y);
                bottom = Mathf.Min(bottom, renderers[i].bounds.min.y);
            }
            if (!any) return 2f;
            return Mathf.Max(.6f, top - bottom);
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

        static string Pad(int n) { return n.ToString("00"); }
        static string Safe(string s) { return s.Replace('/', '-').Replace('\\', '-').Replace(' ', '-'); }

        static bool ScreenshotReady(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var bytes = File.ReadAllBytes(path);
                return bytes.Length > 1000 && bytes.Length < 40 * 1024 * 1024 &&
                       bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
            }
            catch { return false; }
        }

        static void Fail(string message)
        {
            Debug.LogError("[MobShowcase] FAIL: " + message);
            finishing = true;
            Application.Quit(1);
        }
    }
}
