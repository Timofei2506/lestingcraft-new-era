using System;
using UnityEngine;
using LestingCraft.World;

namespace LestingCraft.Presentation
{
    /// <summary>Pure, cyclic lighting sample with physically distinct atmospheres & skies across all 13 realms.</summary>
    public readonly struct AtmosphereLighting
    {
        public readonly Vector3 SunDirection, MoonDirection;
        public readonly Color Zenith, Horizon, SunColor, AmbientSky, AmbientEquator, AmbientGround, ShallowWater, DeepWater;
        public readonly float Daylight, DirectIntensity, SolarAngle, WaterReflection;
        public readonly bool MoonIsPrimary;

        public static float CloudCoverageForRealm(Dimension realm, float baseClouds)
        {
            switch (realm)
            {
                case Dimension.Moon:
                case Dimension.Mercury:
                case Dimension.Europa:
                case Dimension.Pluto:
                case Dimension.Dream:
                    return 0f; // Airless vacuum / frozen exosphere — crystal-clear starry sky, zero water clouds
                case Dimension.Mars:
                    return Mathf.Clamp01(baseClouds * 0.22f); // Thin high CO2-ice cirrus wisps
                case Dimension.Venus:
                    return Mathf.Lerp(0.88f, 0.99f, baseClouds); // Unbroken super-rotating H2SO4 cloud deck
                case Dimension.Radia:
                    return 0.94f; // Heavy overcast storm ceiling over Cherenkov Basin (image-1.png)
                case Dimension.Titan:
                    return Mathf.Lerp(0.62f, 0.86f, baseClouds); // Dense cryogenic methane-nitrogen haze layers
                case Dimension.Somnia:
                    return Mathf.Lerp(0.45f, 0.78f, baseClouds);
                case Dimension.AndromedaPrime:
                    return Mathf.Clamp01(baseClouds * 0.45f);
                default:
                    return Mathf.Clamp01(baseClouds);
            }
        }

        public static float PrecipitationScaleForRealm(Dimension realm)
        {
            switch (realm)
            {
                case Dimension.Moon:
                case Dimension.Mercury:
                case Dimension.Mars:
                case Dimension.Venus:
                case Dimension.Europa:
                case Dimension.Pluto:
                case Dimension.Dream:
                    return 0f;
                case Dimension.Titan:
                    return 0.45f; // Slow cryogenic methane drizzle/flakes
                default:
                    return 1f;
            }
        }

        public static float FogDensityMultiplierForRealm(Dimension realm)
        {
            switch (realm)
            {
                case Dimension.Moon:
                case Dimension.Mercury:
                    return 0.02f; // Airless vacuum: razor-sharp distant horizon
                case Dimension.Europa:
                case Dimension.Pluto:
                    return 0.18f; // Subtle icy horizon haze only
                case Dimension.Mars:
                    return 1.35f; // Suspended iron-oxide dust veil
                case Dimension.Titan:
                    return 3.40f; // Thick orange-gold photochemical smog near ground
                case Dimension.Venus:
                    return 4.20f; // Crushing dense atmospheric scattering
                case Dimension.Radia:
                    return 2.85f; // Dense monochrome storm mist silhouetting the twin helix towers
                case Dimension.Somnia:
                    return 2.60f;
                default:
                    return 1.0f;
            }
        }

        AtmosphereLighting(double clock, Dimension realm, float clouds, float precipitation)
        {
            float hour = (float)((clock / 1200 - Math.Floor(clock / 1200)) * 24);
            SolarAngle = hour * 15 - 90;
            SunDirection = -(Quaternion.Euler(SolarAngle, -32, 0) * Vector3.forward);
            MoonDirection = -SunDirection;
            float elevation = SunDirection.y;
            Daylight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.13f, .25f, elevation));
            float daylightBeam = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.025f, .30f, elevation));
            float golden = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.04f, .48f, elevation))) * Daylight;
            MoonIsPrimary = elevation < -.025f;

            var daySky = new Color(.115f, .29f, .49f);
            var dayHorizon = new Color(.56f, .66f, .70f);
            var nightSky = new Color(.004f, .009f, .027f);
            var nightHorizon = new Color(.023f, .036f, .064f);
            var warm = new Color(1f, .42f, .17f);
            var noon = new Color(1f, .94f, .83f);

            ShallowWater = new Color(.18f, .43f, .36f);
            DeepWater = new Color(.018f, .095f, .12f);
            WaterReflection = realm == Dimension.Radia ? 0.12f : 1f;

            float effClouds = CloudCoverageForRealm(realm, clouds);
            float effPrecip = precipitation * PrecipitationScaleForRealm(realm);
            float overcast = Mathf.Clamp01(effClouds * .38f + effPrecip * .40f);

            Zenith = Color.Lerp(nightSky, Color.Lerp(daySky, new Color(.24f, .29f, .34f), overcast), Daylight);
            Horizon = Color.Lerp(nightHorizon, Color.Lerp(dayHorizon, new Color(.32f, .37f, .40f), overcast), Daylight);
            Horizon = Color.Lerp(Horizon, new Color(.65f, .29f, .15f), golden * (1 - overcast) * .25f);
            SunColor = MoonIsPrimary ? new Color(.44f, .57f, .85f) : Color.Lerp(noon, warm, golden * .8f);
            DirectIntensity = (MoonIsPrimary ? .09f : Mathf.Lerp(.08f, 1.9f, daylightBeam)) * (1 - overcast * .70f);
            AmbientSky = Color.Lerp(new Color(.025f, .040f, .072f), Color.Lerp(daySky, dayHorizon, .62f) * .82f, Daylight);
            AmbientEquator = Color.Lerp(new Color(.014f, .020f, .034f), Color.Lerp(dayHorizon, new Color(.19f, .22f, .18f), .66f), Daylight);
            AmbientGround = Color.Lerp(new Color(.007f, .010f, .018f), new Color(.075f, .085f, .063f), Daylight);

            if (realm == Dimension.Moon)
            {
                // Airless lunar vacuum: pitch-black sky even at noon, harsh white sunlight & deep dark shadows
                Zenith = new Color(.001f, .0015f, .005f);
                Horizon = new Color(.002f, .003f, .008f);
                SunColor = new Color(1.0f, .98f, .94f);
                DirectIntensity = MoonIsPrimary ? .015f : 1.95f * daylightBeam;
                AmbientSky = new Color(.014f, .017f, .026f);
                AmbientEquator = AmbientSky;
                AmbientGround = new Color(.008f, .009f, .012f);
            }
            else if (realm == Dimension.Mercury)
            {
                // Scorched vacuum at 0.39 AU: pitch-black sky, blinding solar glare (2.75x) & warm horizon bounce
                Zenith = new Color(.002f, .0015f, .003f);
                Horizon = Color.Lerp(new Color(.004f, .003f, .004f), new Color(.065f, .042f, .024f), Daylight);
                SunColor = new Color(1.0f, .95f, .82f);
                DirectIntensity = MoonIsPrimary ? .01f : 2.75f * daylightBeam;
                AmbientSky = Color.Lerp(new Color(.008f, .008f, .012f), new Color(.055f, .044f, .032f), Daylight);
                AmbientEquator = AmbientSky;
                AmbientGround = new Color(.018f, .014f, .010f);
            }
            else if (realm == Dimension.Mars)
            {
                // Martian dust atmosphere: butterscotch-salmon daytime sky & authentic cool blue twilight sunset!
                Zenith = Color.Lerp(new Color(.005f, .006f, .016f), new Color(.35f, .23f, .18f), Daylight);
                Horizon = Color.Lerp(new Color(.018f, .024f, .045f), new Color(.74f, .42f, .26f), Daylight);
                Horizon = Color.Lerp(Horizon, new Color(.34f, .44f, .60f), golden * .62f);
                SunColor = MoonIsPrimary ? new Color(.22f, .25f, .36f) : Color.Lerp(new Color(1f, .80f, .62f), new Color(.68f, .80f, 1.0f), golden * .75f);
                DirectIntensity = (MoonIsPrimary ? .03f : 1.32f * daylightBeam);
                AmbientSky = Color.Lerp(new Color(.015f, .016f, .026f), new Color(.48f, .28f, .20f), Daylight);
                AmbientEquator = Color.Lerp(new Color(.012f, .013f, .020f), Horizon * .65f, Daylight);
            }
            else if (realm == Dimension.Venus)
            {
                // Crushing sulfuric greenhouse: deep mustard-amber overcast & glowing orange horizon
                Zenith = Color.Lerp(new Color(.045f, .026f, .008f), new Color(.56f, .38f, .10f), Daylight);
                Horizon = Color.Lerp(new Color(.085f, .048f, .014f), new Color(.84f, .60f, .20f), Daylight);
                SunColor = new Color(1.0f, .76f, .38f);
                DirectIntensity = MoonIsPrimary ? .04f : .48f * daylightBeam;
                AmbientSky = Color.Lerp(new Color(.05f, .03f, .01f), new Color(.64f, .46f, .18f), Daylight);
                AmbientEquator = Color.Lerp(new Color(.04f, .025f, .01f), Horizon * .82f, Daylight);
                ShallowWater = new Color(.62f, .42f, .12f);
                DeepWater = new Color(.28f, .14f, .04f);
            }
            else if (realm == Dimension.Europa)
            {
                // Cryogenic icy moon of Jupiter at 5.2 AU: deep starry abyssal blue-black sky & crisp glacial ice bounce
                Zenith = Color.Lerp(new Color(.001f, .003f, .010f), new Color(.014f, .038f, .086f), Daylight);
                Horizon = Color.Lerp(new Color(.004f, .010f, .026f), new Color(.095f, .20f, .34f), Daylight);
                SunColor = MoonIsPrimary ? new Color(.22f, .30f, .46f) : new Color(.90f, .95f, 1.0f);
                DirectIntensity = MoonIsPrimary ? .025f : .72f * daylightBeam;
                AmbientSky = Color.Lerp(new Color(.012f, .022f, .044f), new Color(.10f, .19f, .30f), Daylight);
                AmbientEquator = Color.Lerp(new Color(.010f, .018f, .034f), new Color(.14f, .24f, .36f), Daylight);
                AmbientGround = new Color(.035f, .065f, .095f);
            }
            else if (realm == Dimension.Titan)
            {
                // Saturn's moon Titan (9.6 AU) — completely distinct from Mars!
                // Deep petrol-cyan methane upper stratosphere (CH4 red absorption) + golden-honey tholin horizon haze!
                Zenith = Color.Lerp(new Color(.004f, .018f, .028f), new Color(.045f, .185f, .245f), Daylight);
                Horizon = Color.Lerp(new Color(.028f, .022f, .012f), new Color(.68f, .52f, .22f), Daylight);
                SunColor = MoonIsPrimary ? new Color(.14f, .18f, .22f) : new Color(.98f, .80f, .44f);
                DirectIntensity = MoonIsPrimary ? .02f : .52f * daylightBeam;
                AmbientSky = Color.Lerp(new Color(.016f, .032f, .042f), new Color(.16f, .26f, .28f), Daylight);
                AmbientEquator = Color.Lerp(new Color(.022f, .018f, .010f), new Color(.44f, .35f, .18f), Daylight);
                ShallowWater = new Color(.10f, .08f, .05f); // Liquid methane/ethane lakes
                DeepWater = new Color(.03f, .025f, .018f);
            }
            else if (realm == Dimension.Pluto)
            {
                // Outer Kuiper / Ice-Giant twilight at 39.5 AU: true deep-space darkness + New Horizons cyan nitrogen rim!
                Zenith = new Color(.001f, .002f, .008f);
                Horizon = Color.Lerp(new Color(.004f, .010f, .028f), new Color(.045f, .145f, .295f), Daylight);
                SunColor = new Color(.82f, .90f, 1.0f);
                DirectIntensity = MoonIsPrimary ? .012f : .34f * daylightBeam;
                AmbientSky = Color.Lerp(new Color(.008f, .012f, .026f), new Color(.032f, .065f, .125f), Daylight);
                AmbientEquator = AmbientSky;
                AmbientGround = new Color(.010f, .015f, .028f);
            }
            else if (realm == Dimension.ProximaB)
            {
                // Tidally locked red-dwarf world: deep wine-violet zenith & eternal crimson flare horizon
                Zenith = Color.Lerp(new Color(.012f, .004f, .018f), new Color(.14f, .035f, .085f), Daylight);
                Horizon = Color.Lerp(new Color(.055f, .014f, .022f), new Color(.78f, .24f, .18f), Daylight);
                SunColor = new Color(1.0f, .44f, .26f);
                DirectIntensity = MoonIsPrimary ? .03f : 1.05f * daylightBeam;
                AmbientSky = Color.Lerp(new Color(.024f, .010f, .026f), new Color(.32f, .11f, .13f), Daylight);
                AmbientEquator = Color.Lerp(new Color(.030f, .012f, .018f), Horizon * .60f, Daylight);
            }
            else if (realm == Dimension.Trappist1e)
            {
                // Resonant oceanic exoplanet: deep amethyst zenith & soft rose-lavender horizon
                Zenith = Color.Lerp(new Color(.008f, .006f, .024f), new Color(.13f, .10f, .32f), Daylight);
                Horizon = Color.Lerp(new Color(.028f, .018f, .048f), new Color(.58f, .36f, .56f), Daylight);
                SunColor = new Color(1.0f, .62f, .52f);
                DirectIntensity = MoonIsPrimary ? .03f : .95f * daylightBeam;
                AmbientSky = Color.Lerp(new Color(.020f, .016f, .042f), new Color(.26f, .20f, .38f), Daylight);
                AmbientEquator = Color.Lerp(new Color(.022f, .016f, .036f), Horizon * .62f, Daylight);
                ShallowWater = new Color(.18f, .28f, .48f);
            }
            else if (realm == Dimension.Kepler452b)
            {
                // Ancient lush bioluminescent super-Earth: emerald-teal sky & warm golden sunlight
                Zenith = Color.Lerp(new Color(.004f, .022f, .032f), new Color(.065f, .34f, .38f), Daylight);
                Horizon = Color.Lerp(new Color(.018f, .058f, .068f), new Color(.48f, .74f, .66f), Daylight);
                SunColor = MoonIsPrimary ? new Color(.32f, .78f, .70f) : new Color(1.0f, .92f, .72f);
                DirectIntensity = MoonIsPrimary ? .14f : 1.85f * daylightBeam;
                AmbientSky = Color.Lerp(new Color(.024f, .068f, .078f), new Color(.18f, .46f, .44f), Daylight);
                AmbientEquator = Color.Lerp(new Color(.018f, .048f, .052f), Horizon * .65f, Daylight);
                ShallowWater = new Color(.10f, .56f, .52f);
            }
            else if (realm == Dimension.AndromedaPrime)
            {
                // Crystalline M31 world: royal deep-violet cosmos zenith & luminous neon-cyan/lilac horizon
                Zenith = Color.Lerp(new Color(.008f, .004f, .026f), new Color(.075f, .038f, .22f), Daylight);
                Horizon = Color.Lerp(new Color(.028f, .016f, .068f), new Color(.38f, .24f, .68f), Daylight);
                SunColor = MoonIsPrimary ? new Color(.48f, .38f, .92f) : new Color(.92f, .86f, 1.0f);
                DirectIntensity = MoonIsPrimary ? .10f : 1.55f * daylightBeam;
                AmbientSky = Color.Lerp(new Color(.026f, .018f, .065f), new Color(.20f, .14f, .42f), Daylight);
                AmbientEquator = Color.Lerp(new Color(.022f, .024f, .058f), new Color(.28f, .26f, .56f), Daylight);
            }
            else if (realm == Dimension.Somnia || realm == Dimension.Dream)
            {
                // Pale pulsar / sleep-paralysis mist
                Zenith = new Color(.045f, .042f, .068f);
                Horizon = new Color(.165f, .155f, .225f);
                SunColor = new Color(.78f, .80f, .96f);
                DirectIntensity = realm == Dimension.Dream ? .16f : .44f;
                AmbientSky = new Color(.085f, .080f, .125f);
                AmbientEquator = new Color(.115f, .108f, .160f);
                AmbientGround = new Color(.035f, .032f, .050f);
            }
            else if (realm == Dimension.Radia)
            {
                // Overcast monochrome storm sky, mirror-black flooded water & piercing cyan underglow (image-1.png!)
                Zenith = new Color(.18f, .19f, .20f);
                Horizon = new Color(.34f, .35f, .37f);
                SunColor = new Color(.48f, .50f, .52f);
                DirectIntensity = .24f;
                AmbientSky = new Color(.12f, .13f, .14f);
                AmbientEquator = new Color(.09f, .10f, .12f);
                AmbientGround = new Color(.025f, .03f, .04f);
                ShallowWater = new Color(.022f, .034f, .050f);
                DeepWater = new Color(.004f, .008f, .014f);
            }
        }

        public static AtmosphereLighting At(double clock, Dimension realm, float clouds, float precipitation)
            => new AtmosphereLighting(clock, realm, Mathf.Clamp01(clouds), Mathf.Clamp01(precipitation));

        public void Apply(Light primary, double clock)
        {
            if (primary)
            {
                primary.transform.rotation = Quaternion.Euler(SolarAngle + (MoonIsPrimary ? 180 : 0), -32, 0);
                primary.color = SunColor;
                primary.intensity = DirectIntensity;
            }
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.fogColor = Horizon;
            Shader.SetGlobalColor("_EnvironmentZenith", Zenith);
            Shader.SetGlobalColor("_EnvironmentHorizon", Horizon);
            Shader.SetGlobalVector("_EnvironmentSunDirection", SunDirection);
            Shader.SetGlobalVector("_EnvironmentMoonDirection", MoonDirection);
            Shader.SetGlobalColor("_EnvironmentSunColor", MoonIsPrimary ? new Color(1, .78f, .52f) : SunColor);
            Shader.SetGlobalFloat("_EnvironmentDaylight", Daylight);
            Shader.SetGlobalFloat("_EnvironmentTime", (float)(clock % 86400));
            Shader.SetGlobalColor("_EnvironmentShallowWater", ShallowWater);
            Shader.SetGlobalColor("_EnvironmentDeepWater", DeepWater);
            Shader.SetGlobalFloat("_EnvironmentWaterReflection", WaterReflection);
        }
    }
}
