using System;
using UnityEngine;
using LestingCraft.World;

namespace LestingCraft.Presentation
{
    /// <summary>Pure, cyclic lighting sample. The world clock, not wall time, drives both clients.</summary>
    public readonly struct AtmosphereLighting
    {
        public readonly Vector3 SunDirection, MoonDirection;
        public readonly Color Zenith, Horizon, SunColor, AmbientSky, AmbientEquator, AmbientGround, ShallowWater, DeepWater;
        public readonly float Daylight, DirectIntensity, SolarAngle;
        public readonly bool MoonIsPrimary;
        AtmosphereLighting(double clock, Dimension realm, float clouds, float precipitation)
        {
            float hour=(float)((clock/1200-Math.Floor(clock/1200))*24);
            SolarAngle=hour*15-90;
            SunDirection=-(Quaternion.Euler(SolarAngle,-32,0)*Vector3.forward);
            MoonDirection=-SunDirection;
            float elevation=SunDirection.y;
            Daylight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.13f,.25f,elevation));
            float daylightBeam=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.025f,.30f,elevation));
            float golden=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.04f,.48f,elevation)))*Daylight;
            MoonIsPrimary=elevation<-.025f;
            var daySky=new Color(.115f,.29f,.49f);var dayHorizon=new Color(.56f,.66f,.70f);
            var nightSky=new Color(.004f,.009f,.027f);var nightHorizon=new Color(.023f,.036f,.064f);
            var warm=new Color(1,.42f,.17f);var noon=new Color(1,.94f,.83f);
            ShallowWater=new Color(.18f,.43f,.36f);DeepWater=new Color(.018f,.095f,.12f);
            float overcast=Mathf.Clamp01(clouds*.38f+precipitation*.40f);
            Zenith=Color.Lerp(nightSky,Color.Lerp(daySky,new Color(.24f,.29f,.34f),overcast),Daylight);
            Horizon=Color.Lerp(nightHorizon,Color.Lerp(dayHorizon,new Color(.32f,.37f,.40f),overcast),Daylight);
            Horizon=Color.Lerp(Horizon,new Color(.65f,.29f,.15f),golden*(1-overcast)*.25f);
            SunColor=MoonIsPrimary?new Color(.44f,.57f,.85f):Color.Lerp(noon,warm,golden*.8f);
            DirectIntensity=(MoonIsPrimary?.11f:Mathf.Lerp(.08f,1.9f,daylightBeam))*(1-overcast*.70f);
            AmbientSky=Color.Lerp(new Color(.035f,.055f,.095f),Color.Lerp(daySky,dayHorizon,.62f)*.82f,Daylight);
            AmbientEquator=Color.Lerp(new Color(.018f,.026f,.043f),Color.Lerp(dayHorizon,new Color(.19f,.22f,.18f),.66f),Daylight);
            AmbientGround=Color.Lerp(new Color(.009f,.013f,.022f),new Color(.075f,.085f,.063f),Daylight);
            if(realm==Dimension.Moon||realm==Dimension.Mercury)
            {
                // No blue atmosphere even in full sunlight. Black sky, exposed sun and low bounce light.
                Zenith=new Color(.001f,.002f,.006f);Horizon=new Color(.002f,.003f,.008f);
                SunColor=new Color(1,.96f,.86f);DirectIntensity=MoonIsPrimary?.02f:1.85f*daylightBeam;
                AmbientSky=new Color(.018f,.022f,.031f);AmbientEquator=AmbientSky;AmbientGround=new Color(.009f,.011f,.014f);
            }
            else if(realm==Dimension.Mars)
            {
                Zenith=Color.Lerp(nightSky,new Color(.36f,.26f,.22f),Daylight);
                Horizon=Color.Lerp(nightHorizon,new Color(.73f,.38f,.24f),Daylight);
                SunColor=MoonIsPrimary?new Color(.25f,.26f,.35f):new Color(1,.72f,.49f);
                DirectIntensity*=.77f;AmbientSky=Color.Lerp(AmbientSky,new Color(.53f,.30f,.23f),Daylight*.68f);
                AmbientEquator=Color.Lerp(AmbientEquator,Horizon,Daylight*.52f);
            }
            else if(realm==Dimension.Venus)
            {
                Zenith=Color.Lerp(nightSky,new Color(.58f,.36f,.12f),Daylight);
                Horizon=Color.Lerp(nightHorizon,new Color(.76f,.54f,.23f),Daylight);
                SunColor=new Color(.99f,.74f,.44f);DirectIntensity*=.30f;
                AmbientSky=Color.Lerp(AmbientSky,new Color(.58f,.40f,.23f),Daylight*.85f);
                AmbientEquator=Color.Lerp(AmbientEquator,Horizon,Daylight*.75f);
            }
        }
        public static AtmosphereLighting At(double clock,Dimension realm,float clouds,float precipitation)
            =>new AtmosphereLighting(clock,realm,Mathf.Clamp01(clouds),Mathf.Clamp01(precipitation));
        public void Apply(Light primary,double clock)
        {
            if(primary)
            {
                primary.transform.rotation=Quaternion.Euler(SolarAngle+(MoonIsPrimary?180:0),-32,0);
                primary.color=SunColor;primary.intensity=DirectIntensity;
            }
            RenderSettings.ambientSkyColor=AmbientSky;RenderSettings.ambientEquatorColor=AmbientEquator;RenderSettings.ambientGroundColor=AmbientGround;
            RenderSettings.fogColor=Horizon;
            Shader.SetGlobalColor("_EnvironmentZenith",Zenith);Shader.SetGlobalColor("_EnvironmentHorizon",Horizon);
            Shader.SetGlobalVector("_EnvironmentSunDirection",SunDirection);Shader.SetGlobalVector("_EnvironmentMoonDirection",MoonDirection);
            Shader.SetGlobalColor("_EnvironmentSunColor",MoonIsPrimary?new Color(1,.78f,.52f):SunColor);
            Shader.SetGlobalFloat("_EnvironmentDaylight",Daylight);Shader.SetGlobalFloat("_EnvironmentTime",(float)(clock%86400));
            Shader.SetGlobalColor("_EnvironmentShallowWater",ShallowWater);Shader.SetGlobalColor("_EnvironmentDeepWater",DeepWater);
        }
    }
}
