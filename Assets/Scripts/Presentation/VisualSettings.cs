using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LestingCraft.Presentation
{
    [Serializable]
    public sealed class VisualSettings
    {
        public int Preset;
        public int ViewDistance = 3;
        public float RenderScale = 1;
        public int ShadowResolution = 2048;
        public bool PostEffects = true;
        public bool AmbientOcclusion = true;
        public int VolumetricQuality = 1;
        public float Sensitivity = .10f;
        public float FieldOfView = 75;
        public static VisualSettings Current { get; private set; }

        public static void Load() { Current = FromJson(PlayerPrefs.GetString("VisualSettings", "")); }
        public static VisualSettings FromJson(string json)
        {
            VisualSettings parsed = null;
            try { parsed = JsonUtility.FromJson<VisualSettings>(json); }
            catch (ArgumentException) { }
            if (parsed == null) parsed = new VisualSettings();
            if (string.IsNullOrEmpty(json) || !json.Contains("\"AmbientOcclusion\"")) parsed.AmbientOcclusion = true;
            parsed.Clamp();
            return parsed;
        }
        public void Clamp()
        {
            VolumetricQuality = Mathf.Clamp(VolumetricQuality, 0, 2);
            ViewDistance = Mathf.Clamp(ViewDistance, 2, 5);
            RenderScale = Mathf.Clamp(RenderScale, .85f, 1.5f);
            Sensitivity = Mathf.Clamp(Sensitivity, .04f, .25f);
            FieldOfView = Mathf.Clamp(FieldOfView, 60, 100);
            Preset = Mathf.Clamp(Preset, 0, 1);
            ShadowResolution = ShadowResolution >= 4096 ? 4096 : ShadowResolution < 2048 ? 1024 : 2048;
        }
        public void Save()
        {
            Clamp();
            PlayerPrefs.SetString("VisualSettings", JsonUtility.ToJson(this));
            PlayerPrefs.Save();
            Apply();
        }
        public void Apply()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset)
            {
                asset.renderScale = RenderScale;
                asset.mainLightShadowmapResolution = ShadowResolution;
                asset.shadowDistance = ViewDistance * (Preset == 1 ? 30 : 26);
                asset.shadowCascadeCount = Preset == 1 ? 4 : 3;
                // SMAA is the final edge filter; avoid paying for MSAA and SMAA together.
                asset.msaaSampleCount = 1;
            }
            var renderer = Resources.Load<UniversalRendererData>("Generated/Renderer");
            if (renderer)
                foreach (var feature in renderer.rendererFeatures)
                    if (feature is ScreenSpaceAmbientOcclusion) feature.SetActive(AmbientOcclusion);
            Shader.SetGlobalFloat("_LCParallaxSteps", Preset == 1 ? 12 : 6);
            Shader.SetGlobalFloat("_LCWaterSteps", Preset == 1 ? 16 : 10);
            Shader.SetGlobalFloat("_LCGrassRange", Preset == 1 ? 64 : 42);
            Shader.SetGlobalFloat("_LCUltra", Preset);
            Shader.SetGlobalFloat("_LCVolumetricSteps", VolumetricQuality == 2 ? 12 : 6);
            Shader.SetGlobalFloat("_LCCloudSteps", VolumetricQuality == 2 ? 32 : VolumetricQuality == 0 ? 12 : 20);
            if (Camera.main)
            {
                Camera.main.fieldOfView = FieldOfView;
                var extra = Camera.main.GetComponent<UniversalAdditionalCameraData>();
                if (extra)
                {
                    extra.renderPostProcessing = PostEffects;
                    extra.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    extra.antialiasingQuality = AntialiasingQuality.High;
                }
            }
        }
        static Volume globalPostVolume;
        static Bloom globalBloom;
        static Tonemapping globalTonemap;
        static ColorAdjustments globalColorAdj;
        static Vignette globalVignette;

        public static void EnsurePostProcessingVolume(float radiationDose01 = 0f, bool inRadiaAbyss = false, float nukeFlash01 = 0f)
        {
            if (!globalPostVolume)
            {
                var go = GameObject.Find("LestingCraftPostVolume");
                if (!go)
                {
                    go = new GameObject("LestingCraftPostVolume");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                }
                globalPostVolume = go.GetComponent<Volume>() ?? go.AddComponent<Volume>();
                globalPostVolume.isGlobal = true;
                globalPostVolume.priority = 10f;
                var profile = globalPostVolume.sharedProfile ?? ScriptableObject.CreateInstance<VolumeProfile>();
                globalPostVolume.sharedProfile = profile;

                if (!profile.TryGet(out globalBloom)) globalBloom = profile.Add<Bloom>(true);
                globalBloom.threshold.Override(0.75f);
                globalBloom.intensity.Override(1.15f);
                globalBloom.scatter.Override(0.80f);

                if (!profile.TryGet(out globalTonemap)) globalTonemap = profile.Add<Tonemapping>(true);
                globalTonemap.mode.Override(TonemappingMode.ACES);

                if (!profile.TryGet(out globalColorAdj)) globalColorAdj = profile.Add<ColorAdjustments>(true);
                if (!profile.TryGet(out globalVignette)) globalVignette = profile.Add<Vignette>(true);
            }

            if (globalBloom != null)
                globalBloom.intensity.Override(Mathf.Lerp(inRadiaAbyss ? 1.05f : 0.95f, 3.6f, Mathf.Clamp01(nukeFlash01)));

            if (globalColorAdj != null)
            {
                // Radia stays cold and moody, but the old -0.25 EV / -46 sat / 0.44
                // vignette stack crushed every dark surface to pure black (showcase
                // shot 10). Lift exposure and soften the vignette; hue is unchanged.
                globalColorAdj.postExposure.Override(Mathf.Lerp(inRadiaAbyss ? -0.08f : 0.18f, 3.8f, Mathf.Clamp01(nukeFlash01)));
                globalColorAdj.contrast.Override(inRadiaAbyss ? 22f : 14f);
                globalColorAdj.saturation.Override(inRadiaAbyss ? -38f : Mathf.Lerp(14f, -35f, Mathf.Clamp01(radiationDose01)));
                globalColorAdj.colorFilter.Override(inRadiaAbyss
                    ? new Color(0.86f, 0.92f, 0.98f, 1f)
                    : Color.Lerp(Color.white, new Color(0.82f, 1.0f, 0.86f, 1f), Mathf.Clamp01(radiationDose01 * 0.7f)));
            }

            if (globalVignette != null)
            {
                float vig = inRadiaAbyss ? 0.34f : Mathf.Lerp(0.17f, 0.52f, Mathf.Clamp01(radiationDose01));
                globalVignette.intensity.Override(vig);
                globalVignette.smoothness.Override(0.58f);
                globalVignette.color.Override(inRadiaAbyss ? new Color(0.02f, 0.03f, 0.05f) : new Color(0.03f, 0.06f, 0.03f));
            }
        }

        public static bool HardwareRayTracing { get { return SystemInfo.supportsRayTracing; } }
        public static bool PipelineRayTracing { get { return false; } }
        public void Ultra()
        {
            Preset = 1; ViewDistance = 4; RenderScale = 1.0f; ShadowResolution = 4096;
            PostEffects = true; AmbientOcclusion = true; VolumetricQuality = 2; Save();
        }
        public void HighDefault()
        {
            Preset = 0; ViewDistance = 3; RenderScale = 1; ShadowResolution = 2048;
            PostEffects = true; AmbientOcclusion = true; VolumetricQuality = 1;
            Sensitivity = .1f; FieldOfView = 75; Save();
        }
    }

    public sealed class AtmosphereLifetime : MonoBehaviour
    {
        public Material Sky;
        void OnDestroy()
        {
            if (RenderSettings.skybox == Sky) RenderSettings.skybox = null;
            if (Sky) Destroy(Sky);
        }
    }

    public static class WorldAtmosphere
    {
        public static void Configure(Camera camera, int realm, double clock = 420)
        {
            var old = GameObject.Find("WorldAtmosphere");
            if (old) UnityEngine.Object.Destroy(old);
            var root = new GameObject("WorldAtmosphere");
            var sunlight = new GameObject("Sun");
            sunlight.transform.SetParent(root.transform);
            sunlight.transform.rotation = Quaternion.Euler(38, -32, 0);
            var light = sunlight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 1;
            light.shadowBias = .04f;
            light.shadowNormalBias = .08f;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            var sky = new Material(Resources.Load<Material>("Generated/Sky"));
            RenderSettings.skybox = sky;
            camera.clearFlags = CameraClearFlags.Skybox;
            AtmosphereLighting.At(clock, (LestingCraft.World.Dimension)realm, .28f, 0).Apply(light, clock);
            root.AddComponent<AtmosphereLifetime>().Sky = sky;
            RenderSettings.fog = false;
            RenderSettings.fogMode = FogMode.Linear;
            var volume = root.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = Resources.Load<VolumeProfile>("Generated/WorldVolume");
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (!data) data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.requiresDepthTexture = true;
            data.requiresColorTexture = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            camera.allowHDR = true;
            if (VisualSettings.Current == null) VisualSettings.Load();
            VisualSettings.Current.Apply();
        }
        public static void Clear()
        {
            var obj = GameObject.Find("WorldAtmosphere");
            if (obj) UnityEngine.Object.Destroy(obj);
        }
    }
}
