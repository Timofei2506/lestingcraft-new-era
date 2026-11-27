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
        Camera cameraView;Material fog;GameObject fogPlane;ParticleSystem rain,snow,motes;float timer;Light sunlight;Material sky;
        MoteKind lastMoteKind=MoteKind.None;
        float clouds,precipitation,wind,wetness,snowAmount,submerged;
        int lastSkyMode=-1;float lastSkyStrength=-1f;
        readonly float[] lastGlow=new float[8];
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
            motes=GroundMotes();
        }
        ParticleSystem Precipitation(string name,bool isSnow)
        {
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);var ps=obj.AddComponent<ParticleSystem>();ps.Stop();
            var main=ps.main;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=0;main.startLifetime=isSnow?7:1.7f;main.prewarm=true;
            main.startSize=isSnow?new ParticleSystem.MinMaxCurve(.045f,.085f):new ParticleSystem.MinMaxCurve(.010f,.019f);main.startColor=isSnow?new Color(.92f,.96f,1,.72f):new Color(.68f,.8f,.9f,.32f);main.maxParticles=isSnow?700:1800;
            main.startRotation=isSnow?new ParticleSystem.MinMaxCurve(0,Mathf.PI*2):new ParticleSystem.MinMaxCurve(0);
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=isSnow?new Vector3(30,16,30):new Vector3(32,24,32);
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=isSnow?.7f:2;velocity.y=isSnow?-1.7f:-26;velocity.z=.2f;
            var emission=ps.emission;emission.rateOverTime=0;
            var collision=ps.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.World;collision.mode=ParticleSystemCollisionMode.Collision3D;collision.lifetimeLoss=1;collision.collidesWith=~0;collision.quality=ParticleSystemCollisionQuality.Low;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Resources.Load<Material>(isSnow?"Generated/Precipitation":"Generated/RainStreaks");
            renderer.renderMode=isSnow?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.Stretch;renderer.lengthScale=isSnow?1:1.2f;renderer.velocityScale=isSnow?0:.012f;renderer.cameraVelocityScale=0;renderer.shadowCastingMode=ShadowCastingMode.Off;
            ps.Play();return ps;
        }

        /// <summary>
        /// Feed the mote system from the table each frame: density, drift, colour and size all
        /// react to weather, wind and the realm's own surface glow. Kind changes (a realm switch)
        /// rebuild the cheap per-kind properties once instead of every frame.
        /// </summary>
        void UpdateMotes(float glow)
        {
            if(!motes)return;
            bool inSpace=Session!=null&&Session.World!=null&&
                (Session.World.Dimension==Dimension.Moon||Session.World.Dimension==Dimension.Mercury||Session.World.Dimension==Dimension.Pluto);
            var p=MoteField.Resolve(Session!=null&&Session.World?Session.World.Dimension:Dimension.Lestingia,
                precipitation,wind,glow,Session!=null&&Session.Sheltered);
            if(inSpace||submerged>.5f)p=MoteField.NoneProfile;
            var main=motes.main;
            if(p.Kind!=lastMoteKind)
            {
                lastMoteKind=p.Kind;
                main.startSize=new ParticleSystem.MinMaxCurve(p.SizeMin,p.SizeMax);
                main.maxParticles=Mathf.Max(8,MoteField.MaxParticles(p));
                var rd=motes.GetComponent<ParticleSystemRenderer>();
                var want=Resources.Load<Material>("Generated/Motes");
                if(want)rd.sharedMaterial=want;
                main.startColor=new Color(p.R,p.G,p.B,p.Alpha);
                var v0=motes.velocityOverLifetime;v0.x=p.Lateral;v0.y=p.Vertical;v0.z=0f;
                var s0=motes.shape;s0.scale=new Vector3(p.Box,p.Box*.75f,p.Box);
            }
            var emission=motes.emission;
            emission.rateOverTime=p.Active?p.Rate:0f;
            if(p.Active)
            {
                main.startColor=new Color(p.R,p.G,p.B,p.Alpha);
                var v=motes.velocityOverLifetime;
                float wx=Mathf.Sin((float)(Session!=null?Session.Data.clock/90f:0f)+(Session!=null?Session.Data.seed%1000:0))*wind;
                v.x=p.Lateral+wx*1.2f;v.y=p.Vertical;v.z=wx*.4f;
            }
        }

        /// <summary>
        /// Ground-level ambient motes: what drifts in front of the camera when nothing is falling
        /// from the sky (pollen, embers, spores, void dust). Look and density come from the
        /// engine-free MoteField table, so realms differ without touching this code.
        /// </summary>
        ParticleSystem GroundMotes()
        {
            var obj=new GameObject("Ground motes");obj.transform.SetParent(transform,false);
            var ps=obj.AddComponent<ParticleSystem>();ps.Stop();
            var main=ps.main;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startSpeed=0;main.startLifetime=6f;main.prewarm=true;main.maxParticles=400;
            main.startRotation=new ParticleSystem.MinMaxCurve(0);
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(16,12,16);
            var emission=ps.emission;emission.rateOverTime=0;
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            var noise=ps.noise;noise.enabled=true;noise.strength=.32f;noise.frequency=.24f;noise.scrollSpeed=.12f;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial=Resources.Load<Material>("Generated/Motes");
            renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
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
            float precipScale=AtmosphereLighting.PrecipitationScaleForRealm(Session.World.Dimension);
            float effPrecip=precipitation*precipScale;
            float effSnow=snowAmount*precipScale;
            Shader.SetGlobalFloat("_GlobalWindStrength",wind);Shader.SetGlobalFloat("_GlobalWetness",wetness*precipScale);Shader.SetGlobalFloat("_GlobalSnow",effSnow);
            // Rain rings on open water: a real weather cue the surface did not have before.
            Shader.SetGlobalFloat("_LCWaterRain",Mathf.Max(0f,effPrecip-effSnow)*(Session.Sheltered?0f:1f));
            rain.transform.position=snow.transform.position=cameraView.transform.position+Vector3.up*15;
            float outside=Session.Sheltered||inWater?0:1;
            var re=rain.emission;re.rateOverTime=outside*Mathf.Max(0f,effPrecip-effSnow)*1400;
            var se=snow.emission;se.rateOverTime=outside*effSnow*80;
            float realmClouds=AtmosphereLighting.CloudCoverageForRealm(Session.World.Dimension,clouds);
            var light=AtmosphereLighting.At(Session.Data.clock,Session.World.Dimension,clouds,precipitation);light.Apply(sunlight,Session.Data.clock);
            sky.SetVector("_CloudOffset",CloudField.Offset(Session.Data.clock,Session.Data.seed,wind));sky.SetFloat("_CloudCoverage",realmClouds);sky.SetFloat("_CloudSteps",VisualSettings.Current.CloudSteps);
            Shader.SetGlobalFloat("_LCSunGlareWeather",1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.9f,realmClouds))*.95f);
            // Frontier sky effects: one global packet per frame, resolved from the pure
            // SkyEffects table so the skybox shader keeps a single branch and the choice of
            // effect per realm is unit-testable without Unity.
            SkyEffects.Resolve(Session.World.Dimension,Session.Data.clock,light.Daylight,out int skyMode,out float skyStrength,out float skyTime,out float skySeed);
            if(skyMode!=lastSkyMode||skyStrength>0f||lastSkyStrength>0f)
            {
                Shader.SetGlobalFloat("_LCSkyEffect",skyMode);
                Shader.SetGlobalFloat("_LCSkyStrength",skyStrength);
                Shader.SetGlobalVector("_LCSkyParams",new Vector4(skyTime+skySeed,1f,0f,skySeed));
                lastSkyMode=skyMode;lastSkyStrength=skyStrength;
            }
            // Living-surface glow: one float per surface for the current realm, resolved from the
            // engine-free SurfaceGlows table. Surfaces the realm does not use are pushed to 0 so a
            // realm switch can never leave a previous world's glow switched on.
            var glowProfile=SurfaceGlows.ForRealm(Session.World.Dimension,light.Daylight);
            foreach(var kind in SurfaceGlows.All)
            {
                float value=kind==glowProfile.Kind?glowProfile.StrengthAt(light.Daylight):0f;
                if(Mathf.Abs(value-lastGlow[(int)kind])<.004f)continue;
                Shader.SetGlobalFloat(SurfaceGlows.GlobalName(kind),value);
                lastGlow[(int)kind]=value;
            }

            UpdateMotes(glowProfile.StrengthAt(light.Daylight));
            fog.SetFloat("_Density",AtmosphereProfile.Density(effPrecip,submerged)*AtmosphereLighting.FogDensityMultiplierForRealm(Session.World.Dimension));
            fog.SetFloat("_Steps",VisualSettings.Current.FogSteps);
            fog.SetColor("_FogTint",Color.Lerp(light.Horizon,light.DeepWater*2,submerged));fog.SetFloat("_Submerged",submerged);
            Shader.SetGlobalFloat("_FogHeight",44);
            float height=Mathf.Tan(cameraView.fieldOfView*Mathf.Deg2Rad*.5f)*2;
            fogPlane.transform.localScale=new Vector3(height*cameraView.aspect*1.01f,height*1.01f,1);
            timer+=dt;if(timer>2){timer=0;RefreshFires();}
            foreach(var pair in fires)
            {
                pair.Value.GetComponent<CampfireEffects>().SetWind(windX,wind);
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
                var source=obj.AddComponent<Light>();source.type=LightType.Point;source.color=new Color(1,.39f,.075f);source.intensity=3.6f;source.range=9;source.shadows=LightShadows.Soft;source.shadowResolution=UnityEngine.Rendering.LightShadowResolution.Medium;source.shadowBias=.035f;source.shadowNormalBias=.15f;
                obj.AddComponent<CampfireEffects>().Build();fires.Add(point,obj);
            }
        }
        void OnDestroy()
        {
            Shader.SetGlobalFloat("_GlobalWetness",0);Shader.SetGlobalFloat("_GlobalSnow",0);Shader.SetGlobalFloat("_GlobalWindStrength",0);Shader.SetGlobalFloat("_LCWaterRain",0);
            Shader.SetGlobalFloat("_LCSkyEffect",0);Shader.SetGlobalFloat("_LCSkyStrength",0);
            foreach(var kind in SurfaceGlows.All){Shader.SetGlobalFloat(SurfaceGlows.GlobalName(kind),0f);lastGlow[(int)kind]=0f;}
            if(fogPlane)Destroy(fogPlane);if(fog)Destroy(fog);
        }
    }
}
