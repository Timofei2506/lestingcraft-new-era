using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.Gameplay;
using LestingCraft.Runtime;

namespace LestingCraft.Presentation
{
    /// <summary>Camera-local precipitation, global deterministic weather, true sampled-depth/shadow light shafts.</summary>
    public sealed class DynamicWeather : MonoBehaviour
    {
        public SurvivalSession Session;
        Camera cameraView;Material fog;GameObject fogPlane;ParticleSystem rain,snow;float timer;Light sunlight;Material sky;
        readonly System.Collections.Generic.List<GameObject> fires=new System.Collections.Generic.List<GameObject>();
        public WeatherSample Sample {get;private set;}
        public void Initialize(SurvivalSession session,Camera view)
        {
            Session=session;cameraView=view;sunlight=RenderSettings.sun;sky=RenderSettings.skybox;
            fog=new Material(Resources.Load<Material>("Generated/Atmosphere"));
            fogPlane=GameObject.CreatePrimitive(PrimitiveType.Quad);fogPlane.name="Volumetric light shafts / height fog";Destroy(fogPlane.GetComponent<Collider>());
            fogPlane.transform.SetParent(view.transform,false);fogPlane.transform.localPosition=Vector3.forward;
            var renderer=fogPlane.GetComponent<MeshRenderer>();renderer.sharedMaterial=fog;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            rain=Precipitation("Rain",false);snow=Precipitation("Snow",true);
        }
        ParticleSystem Precipitation(string name,bool isSnow)
        {
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);var ps=obj.AddComponent<ParticleSystem>();ps.Stop();
            var main=ps.main;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=isSnow?1.7f:26;main.startLifetime=isSnow?7:.8f;
            main.startSize=isSnow?.08f:.025f;main.startColor=isSnow?new Color(.92f,.96f,1,.75f):new Color(.68f,.8f,.9f,.4f);main.maxParticles=isSnow?700:1800;
            main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(24,.3f,24);shape.rotation=new Vector3(90,0,0);
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=isSnow?.7f:2;velocity.y=isSnow?-1.7f:-26;velocity.z=.2f;
            main.startSpeed=0;var emission=ps.emission;emission.rateOverTime=0;
            var collision=ps.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.World;collision.mode=ParticleSystemCollisionMode.Collision3D;collision.lifetimeLoss=1;collision.collidesWith=~0;collision.quality=ParticleSystemCollisionQuality.Low;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Resources.Load<Material>("Generated/Precipitation");
            renderer.renderMode=isSnow?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.Stretch;renderer.lengthScale=isSnow?1:5;renderer.velocityScale=.03f;renderer.shadowCastingMode=ShadowCastingMode.Off;
            ps.Play();return ps;
        }
        void LateUpdate()
        {
            if(!Session||!cameraView)return;
            Sample=WeatherSample.At(Session.Data.seed,(int)Session.World.Dimension,Session.Data.clock);
            rain.transform.position=snow.transform.position=cameraView.transform.position+Vector3.up*12;
            var re=rain.emission;re.rateOverTime=Session.Sheltered?0:(Sample.Kind==WeatherKind.Rain||Sample.Kind==WeatherKind.Storm)?Sample.Precipitation*1400:0;
            var se=snow.emission;se.rateOverTime=Session.Sheltered?0:Sample.Kind==WeatherKind.Snow?Sample.Precipitation*80:0;
            float hour=(float)(Session.Data.clock%1200)/1200*24,angle=hour*15-90;
            sunlight.transform.rotation=Quaternion.Euler(angle,-32,0);float day=Mathf.Clamp01(Mathf.Sin((hour-6)*Mathf.PI/12));
            sunlight.intensity=Mathf.Lerp(.045f,1.6f,day)*(1-Sample.Clouds*.50f);
            sunlight.color=Color.Lerp(new Color(.55f,.65f,1),new Color(1,.89f,.74f),day);
            sky.SetVector("_SunDirection",-sunlight.transform.forward);sky.SetColor("_SunColor",sunlight.color*(.12f+day));sky.SetFloat("_CloudCoverage",Sample.Clouds);sky.SetFloat("_CloudSteps",VisualSettings.Current.VolumetricQuality==0?12:24);
            sky.SetColor("_Zenith",Color.Lerp(new Color(.007f,.015f,.04f),new Color(.13f,.33f,.51f),day));
            var horizon=Color.Lerp(new Color(.03f,.045f,.07f),new Color(.43f,.59f,.66f),day);horizon=Color.Lerp(horizon,new Color(.24f,.29f,.32f),Sample.Precipitation*.55f);
            sky.SetColor("_Horizon",horizon);RenderSettings.fogColor=horizon;
            RenderSettings.ambientSkyColor=Color.Lerp(new Color(.025f,.04f,.07f),new Color(.4f,.53f,.62f),day);
            RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.012f,.019f,.028f),new Color(.19f,.23f,.21f),day);
            fog.SetFloat("_Density",.004f+Sample.Precipitation*.009f);fog.SetFloat("_Steps",VisualSettings.Current.VolumetricQuality==0?8:16);fog.SetColor("_FogTint",horizon);
            Shader.SetGlobalFloat("_FogHeight",LestingCraft.World.TerrainGenerator.Height(0,0,Session.Data.seed,Session.World.Dimension)+12);
            Shader.SetGlobalFloat("_GlobalWetness",Sample.Kind==WeatherKind.Snow?0:Sample.Precipitation);
            float height=Mathf.Tan(cameraView.fieldOfView*Mathf.Deg2Rad*.5f)*2;fogPlane.transform.localScale=new Vector3(height*cameraView.aspect,height,1);
            timer+=Time.deltaTime;if(timer>2){timer=0;RefreshFires();}
        }
        void RefreshFires()
        {
            foreach(var fire in fires)if(fire)Destroy(fire);fires.Clear();
            foreach(var point in Session.World.NearbyFires(cameraView.transform.position))
            {
                if(fires.Count>=8)break;var obj=new GameObject("Campfire light");obj.transform.SetParent(transform);obj.transform.position=point;var light=obj.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.42f,.10f);light.intensity=2.5f;light.range=9;light.shadows=LightShadows.None;
                var ps=obj.AddComponent<ParticleSystem>();var main=ps.main;main.startLifetime=.65f;main.startSpeed=.8f;main.startSize=.22f;main.startColor=new Color(1,.4f,.06f,.55f);main.maxParticles=64;var emission=ps.emission;emission.rateOverTime=18;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.radius=.25f;shape.angle=12;shape.rotation=new Vector3(-90,0,0);
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=Resources.Load<Material>("Generated/Fire");fires.Add(obj);
            }
        }
        void OnDestroy(){Shader.SetGlobalFloat("_GlobalWetness",0);if(fogPlane)Destroy(fogPlane);if(fog)Destroy(fog);}
    }
}
