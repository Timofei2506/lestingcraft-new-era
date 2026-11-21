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
    /// Renders clean studio 1920x1080 screenshots of all 34 wildlife mobs and all 26 boss/drone/bacteria
    /// voxel sculptures in the real game binary when "-lc-mobs" is passed on the command line.
    /// </summary>
    public sealed class MobBossShowcaseProbe : MonoBehaviour
    {
        // Layer 29 avoids FirstPersonHands.Layer (30) while isolating showcase subjects from terrain.
        const int ShowcaseLayer = 29;

        static bool finishing;
        Camera showcaseCam;
        Light keyLight, fillLight;
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

            // Freeze DynamicWeather and hide UI canvases + world directional lights so studio keyLight is URP MainLight.
            ui.gameObject.SetActive(false);
            foreach (var dw in FindObjectsByType<DynamicWeather>(FindObjectsSortMode.None))
                if (dw) dw.enabled = false;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas) canvas.enabled = false;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l && l.type == LightType.Directional) l.enabled = false;

            Shader.SetGlobalVector("_EnvironmentZenith", new Vector4(.34f, .42f, .54f, 1f));
            Shader.SetGlobalVector("_EnvironmentHorizon", new Vector4(.44f, .48f, .54f, 1f));
            Shader.SetGlobalVector("_EnvironmentSkyTop", new Vector4(.34f, .42f, .54f, 1f));
            Shader.SetGlobalVector("_EnvironmentSkyHorizon", new Vector4(.44f, .48f, .54f, 1f));
            Shader.SetGlobalVector("_EnvironmentGroundBounce", new Vector4(.24f, .23f, .22f, 1f));
            Shader.SetGlobalFloat("_EnvironmentDaylight", .95f);

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ShowcaseResults"));
            string mobsDir = Path.Combine(outDir, "mobs");
            string bossesDir = Path.Combine(outDir, "bosses");
            Directory.CreateDirectory(mobsDir);
            Directory.CreateDirectory(bossesDir);

            showcaseCam = CreateShowcaseCamera();
            keyLight = CreateStudioLight("MobShowcaseKey", new Color(1f, .96f, .90f), 1.65f, Quaternion.Euler(42f, -142f, 0f), LightShadows.Soft);
            fillLight = CreateStudioLight("MobShowcaseRimFill", new Color(.68f, .80f, .98f), .62f, Quaternion.Euler(24f, 38f, 0f), LightShadows.None);
            yield return new WaitForEndOfFrame();

            var stageRoot = new GameObject("ShowcaseStageRig");
            stageRoot.transform.position = new Vector3(0f, 180f, 0f);

            // ---- 34 Wildlife mobs ----
            for (int kind = 0; kind < AnimalVisual.SpeciesNames.Length; kind++)
            {
                int capturedKind = kind;
                string file = Path.Combine(mobsDir, Pad(kind) + "-" + Safe(AnimalVisual.SpeciesNames[kind]) + ".png");
                yield return CaptureModel(file, () =>
                {
                    var go = new GameObject("Mob_" + capturedKind);
                    go.transform.SetParent(stageRoot.transform, false);
                    var vis = go.AddComponent<AnimalVisual>();
                    vis.Build(capturedKind);
                    return go;
                });
            }

            // ---- 16 Planetary Bosses + 2 Extra Bosses + 7 Drones + BlackBacteria ----
            int bi = 0;
            foreach (var profile in BossCatalog.All())
            {
                var capturedProfile = profile;
                int seed = 1000 + bi;
                string file = Path.Combine(bossesDir, Pad(bi) + "-" + Safe(profile.Title) + ".png");
                yield return CaptureModel(file, () =>
                {
                    var data = BossSculpts.For(capturedProfile.Dimension, seed);
                    return BuildSculpt(stageRoot, data, capturedProfile.Scale);
                });
                bi++;
            }
            foreach (var profile in BossCatalog.ExtraProfiles)
            {
                var capturedProfile = profile;
                int seed = 2000 + bi;
                string file = Path.Combine(bossesDir, Pad(bi) + "-" + Safe(profile.Title) + ".png");
                yield return CaptureModel(file, () =>
                {
                    var data = BossSculpts.Extra(capturedProfile.Name, seed);
                    return BuildSculpt(stageRoot, data, capturedProfile.Scale);
                });
                bi++;
            }
            foreach (var kind in (DroneKind[])Enum.GetValues(typeof(DroneKind)))
            {
                var capturedKind = kind;
                var profile = DroneRoster.For(kind);
                string file = Path.Combine(bossesDir, Pad(bi) + "-" + Safe(profile.Codename) + ".png");
                yield return CaptureModel(file, () =>
                {
                    var data = BossSculpts.Drone(capturedKind);
                    return BuildSculpt(stageRoot, data, profile.Size);
                });
                bi++;
            }
            {
                string file = Path.Combine(bossesDir, Pad(bi) + "-BlackBacteria.png");
                yield return CaptureModel(file, () =>
                {
                    var data = BossSculpts.BacteriaMob(4242);
                    return BuildSculpt(stageRoot, data, 1.0f);
                });
            }

            DestroyImmediate(stageRoot);
            Debug.Log("[MobShowcase] PASS: saved mob + boss screenshots to " + outDir);
            finishing = true;
            Application.Quit(0);
        }

        static GameObject BuildSculpt(GameObject parent, SculptData data, float scale)
        {
            var sculpt = BossSculpture.Build(parent.transform, data, BossSculpture.SharedVoxelMaterial());
            sculpt.transform.localScale = Vector3.one * scale;
            return sculpt.gameObject;
        }

        static Camera CreateShowcaseCamera()
        {
            var camObj = new GameObject("MobShowcaseCamera");
            var cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 34f;
            cam.nearClipPlane = .08f;
            cam.farClipPlane = 400f;
            cam.allowHDR = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.22f, .25f, .30f, 1f);
            cam.cullingMask = 1 << ShowcaseLayer;
            cam.enabled = false;
            return cam;
        }

        static Light CreateStudioLight(string name, Color color, float intensity, Quaternion rotation, LightShadows shadows)
        {
            var go = new GameObject(name);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
            light.shadowBias = .02f;
            light.cullingMask = 1 << ShowcaseLayer;
            go.transform.rotation = rotation;
            return light;
        }

        IEnumerator CaptureModel(string fullPath, Func<GameObject> spawn)
        {
            Debug.Log("[MobShowcase] START: " + Path.GetFileName(fullPath));
            var model = spawn();
            var trs = model.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
                if (trs[i]) trs[i].gameObject.layer = ShowcaseLayer;

            yield return null;
            yield return null;

            Bounds bounds = MeasureBounds(model);
            float span = Mathf.Max(.45f, Mathf.Max(bounds.size.y, Mathf.Max(bounds.size.x, bounds.size.z) * .82f));
            float dist = span * 2.15f;
            // Models face +Z: position camera on +X/+Z front-three-quarter angle looking back at bounds.center.
            Vector3 target = bounds.center;
            Vector3 dir = new Vector3(.60f, .42f, .68f).normalized;
            showcaseCam.transform.position = target + dir * dist;
            showcaseCam.transform.LookAt(target);

            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam && cam != showcaseCam) cam.enabled = false;
            showcaseCam.enabled = true;
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            var snap = ScreenCapture.CaptureScreenshotAsTexture();
            showcaseCam.enabled = false;
            if (model) DestroyImmediate(model);

            if (!snap)
            {
                Fail("CaptureScreenshotAsTexture returned null: " + fullPath);
                yield break;
            }
            byte[] png = ImageConversion.EncodeToPNG(snap);
            DestroyImmediate(snap);
            File.WriteAllBytes(fullPath, png);
            if (!ScreenshotReady(fullPath))
            {
                Fail("Failed writing PNG screenshot: " + fullPath);
                yield break;
            }
            Debug.Log("[MobShowcase] CAPTURED: " + fullPath);
        }

        static Bounds MeasureBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            Bounds total = new Bounds(model.transform.position + Vector3.up * .8f, Vector3.one);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i]) continue;
                if (!any) { total = renderers[i].bounds; any = true; }
                else total.Encapsulate(renderers[i].bounds);
            }
            return total;
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
