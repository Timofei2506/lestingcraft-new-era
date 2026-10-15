using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LestingCraft.Presentation
{
    [Serializable] public sealed class VisualSettings
    {
        public int Preset; // 0: quality Medium, 1: Ultra. Never selected automatically.
        public int ViewDistance=3;
        public float RenderScale=1;
        public int ShadowResolution=2048;
        public bool PostEffects=true;
        public bool AmbientOcclusion=true;
        public int VolumetricQuality=1;
        public float Sensitivity=.10f;
        public float FieldOfView=75;
        public static VisualSettings Current {get;private set;}
        public static void Load()
        {
            try{Current=JsonUtility.FromJson<VisualSettings>(PlayerPrefs.GetString("VisualSettings",""));}catch{}
            if(Current==null)Current=new VisualSettings();Current.Clamp();
        }
        public void Clamp()
        {VolumetricQuality=Mathf.Clamp(VolumetricQuality,0,2);ViewDistance=Mathf.Clamp(ViewDistance,2,5);RenderScale=Mathf.Clamp(RenderScale,.85f,1.5f);Sensitivity=Mathf.Clamp(Sensitivity,.04f,.25f);FieldOfView=Mathf.Clamp(FieldOfView,60,100);Preset=Mathf.Clamp(Preset,0,1);ShadowResolution=ShadowResolution>=4096?4096:ShadowResolution<2048?1024:2048;}
        public void Save(){Clamp();PlayerPrefs.SetString("VisualSettings",JsonUtility.ToJson(this));PlayerPrefs.Save();Apply();}
        public void Apply()
        {
            var asset=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(asset){asset.renderScale=RenderScale;asset.mainLightShadowmapResolution=ShadowResolution;asset.shadowDistance=ViewDistance*(Preset==1?30:22);asset.shadowCascadeCount=Preset==1?4:2;}
            var renderer=Resources.Load<UniversalRendererData>("Generated/Renderer");
            if(renderer)foreach(var feature in renderer.rendererFeatures)if(feature is ScreenSpaceAmbientOcclusion)feature.SetActive(AmbientOcclusion);
            Shader.SetGlobalFloat("_LCParallaxSteps",Preset==1?16:8);Shader.SetGlobalFloat("_LCWaterSteps",Preset==1?24:12);
            Shader.SetGlobalFloat("_LCGrassRange",Preset==1?64:42);Shader.SetGlobalFloat("_LCUltra",Preset);
            if(Camera.main)
            {
                Camera.main.fieldOfView=FieldOfView;
                var extra=Camera.main.GetComponent<UniversalAdditionalCameraData>();if(extra)extra.renderPostProcessing=PostEffects;
            }
        }
        public static bool HardwareRayTracing=>SystemInfo.supportsRayTracing;
        public static bool PipelineRayTracing=>false; // URP 17 has no hardware RT renderer. Never mislabel SSR as RTX.
        public void Ultra(){Preset=1;ViewDistance=4;RenderScale=1.15f;ShadowResolution=4096;PostEffects=true;AmbientOcclusion=true;VolumetricQuality=2;Save();}
        public void HighDefault(){Preset=0;ViewDistance=3;RenderScale=1;ShadowResolution=2048;PostEffects=true;AmbientOcclusion=true;VolumetricQuality=1;Sensitivity=.1f;FieldOfView=75;Save();}
    }
    public sealed class AtmosphereLifetime : MonoBehaviour
    {
        public Material Sky;
        void OnDestroy(){if(RenderSettings.skybox==Sky)RenderSettings.skybox=null;if(Sky)Destroy(Sky);}
    }
    public static class WorldAtmosphere
    {
        public static void Configure(Camera camera,int realm,double clock=420)
        {
            var old=GameObject.Find("WorldAtmosphere");if(old)UnityEngine.Object.Destroy(old);
            var root=new GameObject("WorldAtmosphere");
            var sunlight=new GameObject("Sun");sunlight.transform.SetParent(root.transform);sunlight.transform.rotation=Quaternion.Euler(38,-32,0);
            var light=sunlight.AddComponent<Light>();light.type=LightType.Directional;light.shadows=LightShadows.Soft;light.shadowStrength=.82f;light.shadowBias=.04f;light.shadowNormalBias=.25f;
            RenderSettings.sun=light;RenderSettings.ambientMode=AmbientMode.Trilight;
            var sky=new Material(Resources.Load<Material>("Generated/Sky"));
            RenderSettings.skybox=sky;camera.clearFlags=CameraClearFlags.Skybox;
            AtmosphereLighting.At(clock,(LestingCraft.World.Dimension)realm,.28f,0).Apply(light,clock);
            root.AddComponent<AtmosphereLifetime>().Sky=sky;
            RenderSettings.fog=false;RenderSettings.fogMode=FogMode.Linear;
            var volume=root.AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=Resources.Load<VolumeProfile>("Generated/WorldVolume");
            var data=camera.GetComponent<UniversalAdditionalCameraData>();if(!data)data=camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing=true;data.requiresDepthTexture=true;data.requiresColorTexture=true;
            data.antialiasing=AntialiasingMode.None;camera.allowHDR=true;VisualSettings.Current.Apply();
        }
        public static void Clear(){var obj=GameObject.Find("WorldAtmosphere");if(obj)UnityEngine.Object.Destroy(obj);}
    }
}
