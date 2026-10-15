using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.Gameplay;
using LestingCraft.World;

namespace LestingCraft.Presentation
{
    /// <summary>Smoothed visual weather; survival continues using the unchanged deterministic WeatherSample.</summary>
    public sealed class DynamicWeather : MonoBehaviour
    {
        public SurvivalSession Session;
        Camera cameraView;Material fog;GameObject fogPlane;ParticleSystem rain,snow;float timer;Light sunlight;Material sky;
        float clouds,precipitation,wind,wetness,snowAmount,submerged;
        readonly Dictionary<Vector3,GameObject> fires=new Dictionary<Vector3,GameObject>();
        public WeatherSample Sample {get;private set;}
        public void Initialize(SurvivalSession session,Camera view)
        {
            Session=session;cameraView=view;sunlight=RenderSettings.sun;sky=RenderSettings.skybox;
            Sample=WeatherSample.At(session.Data.seed,(int)session.World.Dimension,session.Data.clock);
            clouds=Sample.Clouds;precipitation=Sample.Precipitation;wind=Sample.Wind;
            fog=new Material(Resources.Load<Material>("Generated/Atmosphere"));
            fogPlane=GameObject.CreatePrimitive(PrimitiveType.Quad);fogPlane.name="Volumetric light shafts / height fog";Destroy(fogPlane.GetComponent<Collider>());
            fogPlane.transform.SetParent(view.transform,false);fogPlane.transform.localPosition=Vector3.forward;
            var renderer=fogPlane.GetComponent<MeshRenderer>();renderer.sharedMaterial=fog;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            rain=Precipitation("Rain",false);snow=Precipitation("Snow",true);
        }
        ParticleSystem Precipitation(string name,bool isSnow)
        {
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);var ps=obj.AddComponent<ParticleSystem>();ps.Stop();
            var main=ps.main;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=0;main.startLifetime=isSnow?7:.8f;
            main.startSize=isSnow?.075f:.022f;main.startColor=isSnow?new Color(.92f,.96f,1,.72f):new Color(.68f,.8f,.9f,.32f);main.maxParticles=isSnow?700:1800;
            main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(24,.3f,24);
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=isSnow?.7f:2;velocity.y=isSnow?-1.7f:-26;velocity.z=.2f;
            var emission=ps.emission;emission.rateOverTime=0;
            var collision=ps.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.World;collision.mode=ParticleSystemCollisionMode.Collision3D;collision.lifetimeLoss=1;collision.collidesWith=~0;collision.quality=ParticleSystemCollisionQuality.Low;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Resources.Load<Material>("Generated/Precipitation");
            renderer.renderMode=isSnow?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.Stretch;renderer.lengthScale=isSnow?1:5;renderer.velocityScale=.03f;renderer.shadowCastingMode=ShadowCastingMode.Off;
            ps.Play();return ps;
        }
        void LateUpdate()
        {
            if(!Session||!cameraView||!sunlight||!sky)return;
            Sample=WeatherSample.At(Session.Data.seed,(int)Session.World.Dimension,Session.Data.clock);
            float dt=Mathf.Min(Time.deltaTime,.25f),blend=1-Mathf.Exp(-dt/6);
            clouds=Mathf.Lerp(clouds,Sample.Clouds,blend);precipitation=Mathf.Lerp(precipitation,Sample.Precipitation,blend);wind=Mathf.Lerp(wind,Sample.Wind,blend);
            snowAmount=Mathf.Lerp(snowAmount,Sample.Kind==WeatherKind.Snow?precipitation:0,blend);
            wetness=Mathf.MoveTowards(wetness,Sample.Kind==WeatherKind.Snow?0:precipitation,dt*(precipitation>wetness?.035f:.007f));
            var cell=Vector3Int.FloorToInt(cameraView.transform.position);
            bool inWater=Session.World.GetBlock(cell.x,cell.y,cell.z)==BlockRegistry.WaterId;
            submerged=Mathf.MoveTowards(submerged,inWater?1:0,dt*5);
            float windX=Mathf.Sin((float)(Session.Data.clock/90)+Session.Data.seed%1000)*wind;
            var rainWind=rain.velocityOverLifetime;rainWind.x=windX*3;rainWind.z=wind;
            var snowWind=snow.velocityOverLifetime;snowWind.x=windX*1.5f;snowWind.z=wind*.3f;
            Shader.SetGlobalFloat("_GlobalWindStrength",wind);Shader.SetGlobalFloat("_GlobalWetness",wetness);Shader.SetGlobalFloat("_GlobalSnow",snowAmount);
            rain.transform.position=snow.transform.position=cameraView.transform.position+Vector3.up*12;
            float outside=Session.Sheltered||inWater?0:1;
            var re=rain.emission;re.rateOverTime=outside*(precipitation-snowAmount)*1400;
            var se=snow.emission;se.rateOverTime=outside*snowAmount*80;
            var light=AtmosphereLighting.At(Session.Data.clock,Session.World.Dimension,clouds,precipitation);light.Apply(sunlight,Session.Data.clock);
            sky.SetFloat("_CloudCoverage",clouds);sky.SetFloat("_CloudSteps",VisualSettings.Current.VolumetricQuality==0?12:24);
            fog.SetFloat("_Density",Mathf.Lerp(.0025f+precipitation*.008f,.20f,submerged));
            fog.SetFloat("_Steps",VisualSettings.Current.VolumetricQuality==0?8:16);
            fog.SetColor("_FogTint",Color.Lerp(light.Horizon,light.DeepWater*2,submerged));fog.SetFloat("_Submerged",submerged);
            Shader.SetGlobalFloat("_FogHeight",Session.World.Dimension==Dimension.Skyreach?86:44);
            float height=Mathf.Tan(cameraView.fieldOfView*Mathf.Deg2Rad*.5f)*2;
            fogPlane.transform.localScale=new Vector3(height*cameraView.aspect*1.01f,height*1.01f,1);
            timer+=dt;if(timer>2){timer=0;RefreshFires();}
            foreach(var pair in fires)
            {
                var source=pair.Value.GetComponent<Light>();
                source.intensity=3.2f+Mathf.PerlinNoise(pair.Key.x*.7f+(float)Session.Data.clock*2,pair.Key.z)*1.3f;
            }
        }
        void RefreshFires()
        {
            var points=Session.World.NearbyFires(cameraView.transform.position);points.Sort((a,b)=>(a-cameraView.transform.position).sqrMagnitude.CompareTo((b-cameraView.transform.position).sqrMagnitude));
            if(points.Count>8)points.RemoveRange(8,points.Count-8);
            var remove=new List<Vector3>();foreach(var key in fires.Keys)if(!points.Contains(key))remove.Add(key);
            foreach(var key in remove){if(fires[key])Destroy(fires[key]);fires.Remove(key);}
            foreach(var point in points)
            {
                if(fires.ContainsKey(point))continue;
                var obj=new GameObject("Persistent campfire light");obj.transform.SetParent(transform);obj.transform.position=point;
                var source=obj.AddComponent<Light>();source.type=LightType.Point;source.color=new Color(1,.39f,.075f);source.intensity=3.6f;source.range=9;source.shadows=LightShadows.None;
                var ps=obj.AddComponent<ParticleSystem>();var main=ps.main;main.startLifetime=.65f;main.startSpeed=.8f;main.startSize=.22f;main.startColor=new Color(1,.4f,.06f,.55f);main.maxParticles=64;
                var emission=ps.emission;emission.rateOverTime=18;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.radius=.25f;shape.angle=12;shape.rotation=new Vector3(-90,0,0);
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=Resources.Load<Material>("Generated/Fire");fires.Add(point,obj);
            }
        }
        void OnDestroy()
        {
            Shader.SetGlobalFloat("_GlobalWetness",0);Shader.SetGlobalFloat("_GlobalSnow",0);Shader.SetGlobalFloat("_GlobalWindStrength",0);
            if(fogPlane)Destroy(fogPlane);if(fog)Destroy(fog);
        }
    }
}
