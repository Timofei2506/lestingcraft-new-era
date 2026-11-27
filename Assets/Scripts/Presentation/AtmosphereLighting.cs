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
        readonly Dimension lastRealm;

        public static float CloudCoverageForRealm(Dimension realm, float baseClouds)
        {
            // 2.0: the authored catalog owns the base coverage for every body, so a new realm
            // never needs a second switch here. Weather still modulates it below.
            if (LestingCraft.World.DimensionRealm.IsDeepFrontier(realm))
                return Mathf.Clamp01(Mathf.Lerp(LestingCraft.World.PlanetCatalog.Get(realm).Clouds, 1f, baseClouds * .35f));
            switch (realm)
            {
                case Dimension.Moon:
                case Dimension.Mercury:
                case Dimension.Europa:
                case Dimension.Pluto:
                case Dimension.Dream:
                case Dimension.Backrooms:
                case Dimension.Inferno:
                    return 0f; // Airless vacuum / sealed interior / cavernous underworld — zero water clouds
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
                case Dimension.Paradise:
                    return Mathf.Lerp(0.28f, 0.52f, baseClouds); // Soft sunlit celestial cumulus wisps
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
                case Dimension.Paradise:
                    return 0f;
                case Dimension.Titan:
                    return 0.45f; // Slow cryogenic methane drizzle/flakes
                case Dimension.Crystalia:
                case Dimension.Nihility:
                case Dimension.Stormreach:
                    return 0f; // Vacuum frost / void / the storm is lightning, not rain
                case Dimension.Aquaria:
                    return 1.35f; // Warm monsoonal downpours over the archipelago
                case Dimension.Verdant:
                    return 0.85f; // Heavy spore-laden drizzle
                case Dimension.Emberfall:
                    return 0.35f; // Soot falls instead of water
                default:
                    return 1f;
            }
        }

        public static float FogDensityMultiplierForRealm(Dimension realm)
        {
            if (LestingCraft.World.DimensionRealm.IsDeepFrontier(realm))
                return LestingCraft.World.PlanetCatalog.Get(realm).FogScale;
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
                case Dimension.Backrooms:
                    return 3.60f; // Sickly yellow gloom swallows distant rooms
                case Dimension.Inferno:
                    return 2.95f; // Glowing volcanic ash & ember haze across cavernous lava seas
                case Dimension.Paradise:
                    return 0.48f; // Crisp sunlit golden-azure horizon mist
                default:
                    return 1.0f;
            }
        }

        AtmosphereLighting(double clock, Dimension realm, float clouds, float precipitation)
        {
            lastRealm = realm;
            float hour = (float)((clock / 1200 - Math.Floor(clock / 1200)) * 24);
            SolarAngle = hour * 15 - 90;
            SunDirection = -(Quaternion.Euler(SolarAngle, -32, 0) * Vector3.forward);
            MoonDirection = -SunDirection;
            float elevation = SunDirection.y;
            Daylight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.13f, .25f, elevation));
            float daylightBeam = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.025f, .30f, elevation));
            float golden = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.04f, .48f, elevation))) * Daylight;
            MoonIsPrimary = elevation < -.025f;

            var daySky = new Color(.085f, .39f, .88f);
            var dayHorizon = new Color(.48f, .80f, .98f);
            var nightSky = new Color(.005f, .012f, .036f);
            var nightHorizon = new Color(.026f, .044f, .082f);
            var warm = new Color(1f, .54f, .24f);
            var noon = new Color(1f, .965f, .88f);

            // Crystal-clear sunlit turquoise shallows transitioning into rich azure sea.
            ShallowWater = new Color(.13f, .72f, .79f);
            DeepWater = new Color(.035f, .27f, .58f);
            WaterReflection = realm == Dimension.Radia ? 0.12f : 1f;

            float effClouds = CloudCoverageForRealm(realm, clouds);
            float effPrecip = precipitation * PrecipitationScaleForRealm(realm);
            float overcast = Mathf.Clamp01(Mathf.Max(0f, effClouds - .26f) * .48f + effPrecip * .46f);

            Zenith = Color.Lerp(nightSky, Color.Lerp(daySky, new Color(.28f, .36f, .46f), overcast), Daylight);
            Horizon = Color.Lerp(nightHorizon, Color.Lerp(dayHorizon, new Color(.44f, .51f, .58f), overcast), Daylight);
            // Warm golden-hour glow along the horizon when the sun is low.
            Horizon = Color.Lerp(Horizon, new Color(1f, .72f, .36f), golden * (1 - overcast) * .56f);
            Horizon = Color.Lerp(Horizon, new Color(.88f, .42f, .22f), golden * (1 - overcast) * .16f);
            SunColor = MoonIsPrimary ? new Color(.46f, .60f, .88f) : Color.Lerp(noon, warm, golden * .82f);
            DirectIntensity = (MoonIsPrimary ? .10f : Mathf.Lerp(.10f, 2.32f, daylightBeam)) * (1 - overcast * .65f);
            // Bright, warm, sky-filled ambient bounce so shadows stay colorful and inviting instead of muddy black.
            AmbientSky = Color.Lerp(new Color(.030f, .048f, .088f), Color.Lerp(daySky, dayHorizon, .64f) * 1.14f, Daylight);
            AmbientEquator = Color.Lerp(new Color(.018f, .026f, .044f), Color.Lerp(dayHorizon, new Color(.38f, .48f, .36f), .52f), Daylight);
            AmbientGround = Color.Lerp(new Color(.009f, .013f, .022f), new Color(.20f, .25f, .16f), Daylight);

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
            else if (realm == Dimension.Backrooms)
            {
                // Level 0 Backrooms: constant fluorescent hum light, mono-yellow ambience,
                // no sun, no sky - only the damp glow between endless rooms.
                Zenith = new Color(.34f, .31f, .13f);
                Horizon = new Color(.52f, .46f, .20f);
                SunColor = new Color(.92f, .86f, .55f);
                DirectIntensity = .55f;
                AmbientSky = new Color(.32f, .29f, .15f);
                AmbientEquator = new Color(.27f, .24f, .12f);
                AmbientGround = new Color(.18f, .17f, .09f);
                ShallowWater = new Color(.30f, .27f, .12f);
                DeepWater = new Color(.12f, .11f, .05f);
            }
            else if (realm == Dimension.Inferno)
            {
                // Cavernous Basalt & Crimson Underworld lit by boiling lava oceans and soul-crystals.
                Zenith = new Color(.22f, .035f, .018f);
                Horizon = new Color(.68f, .17f, .045f);
                SunColor = new Color(1.00f, .52f, .20f);
                DirectIntensity = .62f;
                AmbientSky = new Color(.38f, .12f, .06f);
                AmbientEquator = new Color(.44f, .15f, .06f);
                AmbientGround = new Color(.32f, .10f, .04f);
                ShallowWater = new Color(.95f, .36f, .06f);
                DeepWater = new Color(.55f, .12f, .02f);
            }
            // ---------------- Deep Frontier (2.0) palettes ----------------
            else if (realm == Dimension.Crystalia)
            {
                // Airless prismic moon: black zenith, ionised aurora band, ice-blue albedo bounce.
                float auroraBand = Mathf.Clamp01(1f - Mathf.Abs(elevation - .38f) * 2.6f) * (1f - Daylight * .35f) + .22f;
                Zenith = Color.Lerp(new Color(.004f, .002f, .014f), new Color(.012f, .016f, .048f), Daylight);
                Horizon = Color.Lerp(new Color(.010f, .020f, .056f), new Color(.30f, .78f, .86f), auroraBand * .70f);
                Horizon += new Color(.22f, .06f, .42f) * auroraBand * .32f;
                SunColor = MoonIsPrimary ? new Color(.34f, .44f, .92f) : new Color(.94f, .97f, 1f);
                DirectIntensity = MoonIsPrimary ? .05f : 1.32f * daylightBeam;
                AmbientSky = Color.Lerp(new Color(.010f, .014f, .034f), new Color(.16f, .30f, .42f), Daylight);
                AmbientEquator = Color.Lerp(new Color(.014f, .010f, .038f), new Color(.42f, .30f, .62f), Daylight);
                AmbientGround = new Color(.030f, .034f, .052f);
                ShallowWater = new Color(.30f, .86f, .92f);
            }
            else if (realm == Dimension.Emberfall)
            {
                // Ash world: rust-brown overhead haze, soot sky, ember underglow from the sea below.
                Zenith = Color.Lerp(new Color(.030f, .014f, .010f), new Color(.155f, .062f, .042f), Daylight);
                Horizon = Color.Lerp(new Color(.060f, .024f, .014f), new Color(.58f, .26f, .11f), Daylight);
                Horizon += new Color(.30f, .10f, .02f) * (1f - Daylight) * .55f;
                SunColor = MoonIsPrimary ? new Color(.38f, .22f, .18f) : new Color(1f, .72f, .44f);
                DirectIntensity = MoonIsPrimary ? .05f : .86f * daylightBeam;
                AmbientSky = Color.Lerp(new Color(.034f, .016f, .012f), new Color(.30f, .15f, .09f), Daylight);
                AmbientEquator = Color.Lerp(new Color(.040f, .016f, .010f), new Color(.42f, .19f, .08f), Daylight);
                AmbientGround = new Color(.22f, .075f, .030f); // magma sea bounce
                ShallowWater = new Color(.86f, .30f, .06f);
                DeepWater = new Color(.40f, .09f, .02f);
            }
            else if (realm == Dimension.Aquaria)
            {
                // Ocean world: saturated turquoise zenith, high humidity, brilliant horizon glare.
                Zenith = Color.Lerp(new Color(.004f, .026f, .050f), new Color(.055f, .46f, .74f), Daylight);
                Horizon = Color.Lerp(new Color(.020f, .060f, .086f), new Color(.62f, .95f, .92f), Daylight);
                Horizon = Color.Lerp(Horizon, new Color(1f, .88f, .62f), golden * .52f);
                SunColor = MoonIsPrimary ? new Color(.52f, .70f, .92f) : Color.Lerp(new Color(1f, .97f, .88f), warm, golden * .74f);
                DirectIntensity = MoonIsPrimary ? .12f : Mathf.Lerp(.12f, 2.15f, daylightBeam);
                AmbientSky = Color.Lerp(new Color(.020f, .050f, .070f), new Color(.42f, .74f, .82f), Daylight);
                AmbientEquator = Color.Lerp(new Color(.014f, .038f, .046f), new Color(.30f, .66f, .64f), Daylight);
                AmbientGround = Color.Lerp(new Color(.010f, .026f, .034f), new Color(.12f, .36f, .38f), Daylight);
                ShallowWater = new Color(.22f, .86f, .86f);
                DeepWater = new Color(.02f, .34f, .58f);
            }
            else if (realm == Dimension.Stormreach)
            {
                // Permanent thunderhead: slate-teal mass, lightning-flash white horizon, dark ground bounce.
                Zenith = Color.Lerp(new Color(.020f, .026f, .034f), new Color(.105f, .145f, .185f), Daylight);
                Horizon = Color.Lerp(new Color(.040f, .050f, .062f), new Color(.38f, .44f, .50f), Daylight);
                Horizon = Color.Lerp(Horizon, new Color(.86f, .90f, 1.0f), Mathf.Clamp01(1f - elevation * 3f) * .28f);
                SunColor = MoonIsPrimary ? new Color(.42f, .52f, .70f) : new Color(.86f, .92f, 1.0f);
                DirectIntensity = MoonIsPrimary ? .04f : .58f * daylightBeam;
                AmbientSky = Color.Lerp(new Color(.028f, .036f, .046f), new Color(.20f, .25f, .30f), Daylight);
                AmbientEquator = Color.Lerp(new Color(.024f, .030f, .040f), new Color(.24f, .28f, .33f), Daylight);
                AmbientGround = new Color(.030f, .036f, .044f);
                ShallowWater = new Color(.34f, .50f, .62f);
            }
            else if (realm == Dimension.Verdant)
            {
                // Fungal super-Earth: emerald-teal light filtered through billions of caps & spores.
                Zenith = Color.Lerp(new Color(.006f, .030f, .026f), new Color(.10f, .44f, .36f), Daylight);
                Horizon = Color.Lerp(new Color(.024f, .062f, .048f), new Color(.56f, .88f, .58f), Daylight);
                Horizon += new Color(.30f, .55f, .18f) * .18f;
                SunColor = MoonIsPrimary ? new Color(.30f, .70f, .58f) : new Color(.94f, 1.0f, .78f);
                DirectIntensity = MoonIsPrimary ? .14f : 1.62f * daylightBeam;
                AmbientSky = Color.Lerp(new Color(.024f, .062f, .052f), new Color(.26f, .58f, .44f), Daylight);
                AmbientEquator = Color.Lerp(new Color(.020f, .048f, .036f), new Color(.36f, .62f, .34f), Daylight);
                AmbientGround = new Color(.055f, .075f, .040f);
                ShallowWater = new Color(.28f, .74f, .52f);
                DeepWater = new Color(.04f, .30f, .26f);
            }
            else if (realm == Dimension.Nihility)
            {
                // Broken reality: absolute black sky, violet shard glints, no bounce to speak of.
                Zenith = new Color(.001f, .001f, .004f);
                Horizon = Color.Lerp(new Color(.006f, .004f, .014f), new Color(.16f, .08f, .30f), Mathf.Clamp01(1f - Mathf.Abs(elevation) * 1.8f) * .62f);
                SunColor = MoonIsPrimary ? new Color(.40f, .30f, .74f) : new Color(.88f, .86f, 1.0f);
                DirectIntensity = MoonIsPrimary ? .02f : .92f * daylightBeam;
                AmbientSky = Color.Lerp(new Color(.006f, .006f, .014f), new Color(.05f, .035f, .10f), Daylight);
                AmbientEquator = Color.Lerp(new Color(.010f, .006f, .020f), new Color(.10f, .05f, .16f), Daylight);
                AmbientGround = new Color(.008f, .006f, .014f);
                ShallowWater = new Color(.30f, .18f, .52f);
            }
            else if (realm == Dimension.Paradise)
            {
                // Eternal Sunlit Celestial Eden: radiant azure zenith, warm golden horizon & crystal-turquoise ambrosia water.
                Daylight = 1f;
                MoonIsPrimary = false;
                SolarAngle = 52f;
                Zenith = new Color(.30f, .64f, .98f);
                Horizon = new Color(.99f, .92f, .74f);
                SunColor = new Color(1.00f, .96f, .82f);
                DirectIntensity = 1.24f;
                AmbientSky = new Color(.66f, .80f, .98f);
                AmbientEquator = new Color(.84f, .82f, .74f);
                AmbientGround = new Color(.52f, .62f, .50f);
                ShallowWater = new Color(.36f, .90f, .96f);
                DeepWater = new Color(.12f, .50f, .82f);
                WaterReflection = .92f;
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
            // Point light the sky effect at the realm's own horizon colour: lightning, aurora and
            // void seams all pick up the atmosphere instead of staying a fixed tint.
            var skyTint = Horizon * .55f + AmbientEquator * .45f;
            Shader.SetGlobalColor("_LCSkyTint", skyTint);
            // Realms whose sky carries a lightning effect get the flash flag for shaders that want
            // to react to a strike (water highlights, boss telegraphs); everything else gets 0.
            Shader.SetGlobalFloat("_LCStormFlash", SkyEffects.For(lastRealm).Effect == SkyEffect.Lightning ? 1f : 0f);
        }

        /// <summary>Realm of the last applied sample, so per-realm globals can be refreshed.</summary>
        public Dimension LastRealm { get { return lastRealm; } }
    }
}
