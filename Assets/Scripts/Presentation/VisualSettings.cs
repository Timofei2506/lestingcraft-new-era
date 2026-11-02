using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LestingCraft.Presentation
{
    [Serializable]
    public sealed class VisualSettings
    {
        // Schema 2: explicit Mobile/Balanced/High/Ultra/Cinematic ladder.
        // Legacy schema 0/1 prefs are migrated in FromJson, not reinterpreted.
        public int SchemaVersion;
        public int Preset = VisualQualityPolicy.High;
        public int ViewDistance = 3;
        public float RenderScale = 1;
        public int ShadowResolution = 2048;
        public bool PostEffects = true;
        public bool AmbientOcclusion = true;
        public int VolumetricQuality = 1;
        public float Sensitivity = .10f;
        public float FieldOfView = 75;
        public static VisualSettings Current { get; private set; }
        public static bool IsMobile { get { return Application.isMobilePlatform; } }

        public VisualQualityPolicy.Profile Profile { get { return VisualQualityPolicy.For(Preset, IsMobile); } }
        public int DetailQuality { get { return Profile.DetailQuality; } }
        public int CloudSteps { get { return VolumetricQuality == 0 ? Mathf.Max(8, Profile.CloudSteps / 2) : VolumetricQuality == 2 ? Profile.CloudSteps + 8 : Profile.CloudSteps; } }
        public int FogSteps { get { return VolumetricQuality == 0 ? Mathf.Max(4, Profile.FogSteps / 2) : VolumetricQuality == 2 ? Profile.FogSteps + 8 : Profile.FogSteps; } }

        public static void Load() { Current = FromJson(PlayerPrefs.GetString("VisualSettings", "")); }
        public static VisualSettings FromJson(string json)
        {
            VisualSettings parsed = null;
            try { parsed = JsonUtility.FromJson<VisualSettings>(json); }
            catch (ArgumentException) { }
            if (parsed == null) parsed = new VisualSettings();
            bool empty = string.IsNullOrEmpty(json);
            bool hasSchema = !empty && json.Contains("\"SchemaVersion\"");
            int schema = hasSchema ? parsed.SchemaVersion : 0;
            parsed.Preset = VisualQualityPolicy.MigratePreset(parsed.Preset, schema);
            parsed.SchemaVersion = VisualQualityPolicy.SchemaVersion;
            if (empty)
            {
                parsed.Preset = VisualQualityPolicy.DefaultPreset(IsMobile);
                var profile = VisualQualityPolicy.For(parsed.Preset, IsMobile);
                parsed.ViewDistance = IsMobile ? 2 : 3;
                parsed.ShadowResolution = profile.ShadowResolution;
                parsed.VolumetricQuality = IsMobile ? 0 : 1;
            }
            if (empty || !json.Contains("\"AmbientOcclusion\"")) parsed.AmbientOcclusion = true;
            parsed.Clamp();
            return parsed;
        }
        public void Clamp()
        {
            Preset = VisualQualityPolicy.ClampPreset(Preset, IsMobile);
            VolumetricQuality = Mathf.Clamp(VolumetricQuality, 0, 2);
            ViewDistance = VisualQualityPolicy.ClampViewDistance(ViewDistance, IsMobile);
            RenderScale = VisualQualityPolicy.ClampRenderScale(RenderScale, IsMobile);
            Sensitivity = Mathf.Clamp(Sensitivity, .04f, .25f);
            FieldOfView = Mathf.Clamp(FieldOfView, 60, 100);
            ShadowResolution = VisualQualityPolicy.ClampShadowResolution(ShadowResolution, IsMobile);
        }
        public void Save()
        {
            Clamp();
            PlayerPrefs.SetString("VisualSettings", JsonUtility.ToJson(this));
            PlayerPrefs.Save();
            Apply();
        }
        public void ApplyPreset(int preset)
        {
            Preset = VisualQualityPolicy.ClampPreset(preset, IsMobile);
            var profile = Profile;
            ViewDistance = IsMobile ? 2 : Preset >= VisualQualityPolicy.Ultra ? 4 : 3;
            RenderScale = 1;
            ShadowResolution = profile.ShadowResolution;
            PostEffects = true;
            AmbientOcclusion = Preset >= VisualQualityPolicy.Balanced;
            VolumetricQuality = Preset >= VisualQualityPolicy.Ultra ? 2 : Preset == VisualQualityPolicy.Mobile ? 0 : 1;
            Save();
        }
        public void Apply()
        {
            var profile = Profile;
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset)
            {
                asset.renderScale = RenderScale;
                asset.mainLightShadowmapResolution = ShadowResolution;
                asset.shadowDistance = ViewDistance * profile.ShadowDistanceMultiplier;
                asset.shadowCascadeCount = profile.ShadowCascades;
                // SMAA is the final edge filter; avoid paying for MSAA and SMAA together.
                asset.msaaSampleCount = 1;
            }
            var renderer = Resources.Load<UniversalRendererData>("Generated/Renderer");
            if (renderer)
                foreach (var feature in renderer.rendererFeatures)
                    if (feature is ScreenSpaceAmbientOcclusion) feature.SetActive(AmbientOcclusion);
            Shader.SetGlobalFloat("_LCParallaxSteps", profile.ParallaxSteps);
            Shader.SetGlobalFloat("_LCWaterSteps", profile.WaterSteps);
            Shader.SetGlobalFloat("_LCGrassRange", profile.GrassRange);
            Shader.SetGlobalFloat("_LCUltra", profile.UltraEffects ? 1 : 0);
            Shader.SetGlobalFloat("_LCVolumetricSteps", FogSteps);
            Shader.SetGlobalFloat("_LCCloudSteps", CloudSteps);
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

        // Single source of truth for the natural-world grade. Editor bake and runtime agree.
        public const float BloomThreshold = .90f;
        public const float BloomIntensity = .45f;
        public const float BloomScatter = .72f;
        public const float GradeContrast = 8f;
        public const float GradeSaturation = 6f;
        public const float GradeExposure = .05f;
        public const float GradeVignette = .12f;

        public static void ConfigureBaseVolume(VolumeProfile profile)
        {
            if (!profile) return;
            var bloom = profile.TryGet(out Bloom b) ? b : profile.Add<Bloom>(true);
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(BloomScatter);
            bloom.highQualityFiltering.Override(true);
            var tone = profile.TryGet(out Tonemapping t) ? t : profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var colors = profile.TryGet(out ColorAdjustments c) ? c : profile.Add<ColorAdjustments>(true);
            colors.contrast.Override(GradeContrast);
            colors.saturation.Override(GradeSaturation);
            colors.postExposure.Override(GradeExposure);
            var split = profile.TryGet(out SplitToning s) ? s : profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(.47f, .50f, .54f));
            split.highlights.Override(new Color(.54f, .52f, .48f));
            split.balance.Override(-4f);
            var lift = profile.TryGet(out LiftGammaGain lgg) ? lgg : profile.Add<LiftGammaGain>(true);
            lift.lift.Override(Vector4.zero);
            lift.gamma.Override(new Vector4(1, 1, 1, 0));
            lift.gain.Override(new Vector4(1, 1, 1, 0));
            var grain = profile.TryGet(out FilmGrain g) ? g : profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(.06f);
            grain.response.Override(.8f);
            var aberration = profile.TryGet(out ChromaticAberration a) ? a : profile.Add<ChromaticAberration>(true);
            aberration.intensity.Override(0f);
            var vignette = profile.TryGet(out Vignette v) ? v : profile.Add<Vignette>(true);
            vignette.intensity.Override(GradeVignette);
            vignette.smoothness.Override(.58f);
        }

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
                ConfigureBaseVolume(profile);
                profile.TryGet(out globalBloom);
                profile.TryGet(out globalTonemap);
                profile.TryGet(out globalColorAdj);
                profile.TryGet(out globalVignette);
            }

            if (globalBloom != null)
                globalBloom.intensity.Override(Mathf.Lerp(inRadiaAbyss ? .70f : BloomIntensity, 3.6f, Mathf.Clamp01(nukeFlash01)));

            if (globalColorAdj != null)
            {
                globalColorAdj.postExposure.Override(Mathf.Lerp(inRadiaAbyss ? -0.08f : GradeExposure, 3.8f, Mathf.Clamp01(nukeFlash01)));
                globalColorAdj.contrast.Override(inRadiaAbyss ? 22f : GradeContrast);
                globalColorAdj.saturation.Override(inRadiaAbyss ? -38f : Mathf.Lerp(GradeSaturation, -35f, Mathf.Clamp01(radiationDose01)));
                globalColorAdj.colorFilter.Override(inRadiaAbyss
                    ? new Color(0.86f, 0.92f, 0.98f, 1f)
                    : Color.Lerp(Color.white, new Color(0.82f, 1.0f, 0.86f, 1f), Mathf.Clamp01(radiationDose01 * 0.7f)));
            }

            if (globalVignette != null)
            {
                float vig = inRadiaAbyss ? 0.34f : Mathf.Lerp(GradeVignette, 0.52f, Mathf.Clamp01(radiationDose01));
                globalVignette.intensity.Override(vig);
                globalVignette.smoothness.Override(0.58f);
                globalVignette.color.Override(inRadiaAbyss ? new Color(0.02f, 0.03f, 0.05f) : new Color(0.03f, 0.06f, 0.03f));
            }
        }

        public static bool HardwareRayTracing { get { return SystemInfo.supportsRayTracing; } }
        public static bool PipelineRayTracing { get { return false; } }
        public void Ultra() { ApplyPreset(VisualQualityPolicy.Ultra); }
        public void HighDefault() { ApplyPreset(VisualQualityPolicy.High); }
        public void Cinematic() { ApplyPreset(VisualQualityPolicy.Cinematic); }
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
            light.shadowStrength = .88f;
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
