using LestingCraft.World;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Pure description of a realm's ambience: which procedural loops run, how loud, and how
    /// often the realm's own one-shot detail fires (thunder, spore ticks, ember crackle…).
    /// No UnityEngine types, so the whole mix is headless-checkable.
    /// </summary>
    public readonly struct SoundscapeProfile
    {
        /// <summary>Primary looping ambience clip key (see GameAudio.BuildClipBank).</summary>
        public readonly string Primary;
        /// <summary>Secondary looping layer key, or null when the realm has only one.</summary>
        public readonly string Secondary;
        /// <summary>Mix level of the primary layer, 0..1.</summary>
        public readonly float PrimaryVolume;
        /// <summary>Mix level of the secondary layer, 0..1.</summary>
        public readonly float SecondaryVolume;
        /// <summary>Repeating one-shot key for this realm, or null.</summary>
        public readonly string Detail;
        /// <summary>Seconds between detail one-shots (mean).</summary>
        public readonly float DetailPeriod;
        /// <summary>Whether the detail one-shot follows the lightning strike window.</summary>
        public readonly bool DetailFollowsLightning;

        public SoundscapeProfile(string primary, string secondary, float primaryVolume,
            float secondaryVolume, string detail, float detailPeriod, bool detailFollowsLightning)
        {
            Primary = primary;
            Secondary = secondary;
            PrimaryVolume = primaryVolume;
            SecondaryVolume = secondaryVolume;
            Detail = detail;
            DetailPeriod = detailPeriod;
            DetailFollowsLightning = detailFollowsLightning;
        }

        public bool HasSecondary { get { return !string.IsNullOrEmpty(Secondary) && SecondaryVolume > 0f; } }
        public bool HasDetail { get { return !string.IsNullOrEmpty(Detail) && DetailPeriod > 0f; } }
    }

    /// <summary>
    /// Realm → ambience table. Each Deep Frontier world gets its own pair of layers so the six
    /// new dimensions do not all sound like the overworld wind they inherited in 1.x.
    /// </summary>
    public static class RealmSoundscape
    {
        /// <summary>Ambience for a realm. Space and Radia keep their existing special cases.</summary>
        public static SoundscapeProfile For(Dimension realm)
        {
            switch (realm)
            {
                // ---- Deep Frontier (2.0) ----
                case Dimension.Crystalia:
                    // Airless: almost no air noise, thin resonant shimmer and ice ticks.
                    return new SoundscapeProfile("amb_crystal", "amb_space", .30f, .10f, "ice_tick", 6.5f, false);
                case Dimension.Emberfall:
                    // Low furnace rumble with soot wind over it, ember pops underneath.
                    return new SoundscapeProfile("amb_ash", "amb_wind", .48f, .22f, "ember_pop", 2.2f, false);
                case Dimension.Aquaria:
                    // Surf washing over reefs, with deeper swell underneath.
                    return new SoundscapeProfile("amb_ocean", "amb_rain", .46f, .18f, "wave_crash", 7.0f, false);
                case Dimension.Stormreach:
                    // Thunder is the detail layer and it must land with the visible strike.
                    return new SoundscapeProfile("amb_storm", "amb_wind", .44f, .30f, "thunder", 5.5f, true);
                case Dimension.Verdant:
                    // Deep organic hum, wet spore ticks.
                    return new SoundscapeProfile("amb_fungal", "amb_wind", .42f, .16f, "spore_tick", 3.0f, false);
                case Dimension.Nihility:
                    // Sub-bass drone and inverted shimmer; nothing living.
                    return new SoundscapeProfile("amb_void", "amb_space", .38f, .16f, "void_pulse", 9.0f, false);

                // ---- legacy realms ----
                case Dimension.Radia:
                    return new SoundscapeProfile("amb_radia", null, .42f, 0f, "radia_geiger", .9f, false);
                case Dimension.Inferno:
                    return new SoundscapeProfile("amb_ash", "amb_wind", .46f, .24f, "ember_pop", 2.6f, false);
                case Dimension.Paradise:
                    return new SoundscapeProfile("amb_paradise", "amb_wind", .34f, .12f, "nature_bird", 4.5f, false);
                case Dimension.Backrooms:
                    return new SoundscapeProfile("amb_hum", null, .30f, 0f, null, 0f, false);
                case Dimension.Dream:
                    return new SoundscapeProfile("amb_void", "amb_space", .28f, .14f, "void_pulse", 11f, false);

                default:
                    // Overworld and the remaining bodies: wind, plus rain when the weather is wet.
                    return new SoundscapeProfile("amb_wind", null, .42f, 0f, "nature_bird", 9f, false);
            }
        }

        /// <summary>Ambience while in orbit or on EVA: the world's mix is replaced by vacuum.</summary>
        public static SoundscapeProfile Space { get { return new SoundscapeProfile("amb_space", null, .35f, 0f, null, 0f, false); } }

        /// <summary>Every loop key the table can ask for; GameAudio must synthesize all of them.</summary>
        public static readonly string[] LoopKeys =
        {
            "amb_wind", "amb_rain", "amb_space", "amb_radia", "amb_crystal", "amb_ash",
            "amb_ocean", "amb_storm", "amb_fungal", "amb_void", "amb_paradise", "amb_hum"
        };

        /// <summary>Every one-shot detail key the table can ask for.</summary>
        public static readonly string[] DetailKeys =
        {
            "ice_tick", "ember_pop", "wave_crash", "thunder", "spore_tick", "void_pulse",
            "radia_geiger", "nature_bird"
        };

        /// <summary>
        /// True when falling precipitation can actually reach the player in this realm. Airless,
        /// sealed and lightning worlds never rain, yet WeatherSample.At still returns wet weather
        /// for them (it only special-cases the Solar System ladder), so every consumer has to ask
        /// this before believing the sample.
        /// </summary>
        public static bool WeatherReaches(Dimension realm)
        {
            switch (realm)
            {
                case Dimension.Moon:
                case Dimension.Mercury:
                case Dimension.Dream:
                case Dimension.Crystalia:   // vacuum: frost, not weather
                case Dimension.Stormreach: // the storm is lightning, not rain
                case Dimension.Nihility:   // broken reality, no atmosphere
                case Dimension.Backrooms:  // sealed interior
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// True on the frame a lightning strike begins. Mirrors the sky shader exactly
        /// (time = clock * .05, strike = pow(saturate(sin(t*1.35 + seed)*.5+.5), 14)) using the
        /// same realm seed, so thunder and the visible flash cannot drift apart.
        /// </summary>
        public static bool StrikeBegins(double clock, Dimension realm)
        {
            float seed = SkyEffects.Seed(realm);
            return StrikeAt(clock, seed) >= StrikeThreshold && StrikeAt(clock - StrikeStepSeconds, seed) < StrikeThreshold;
        }

        /// <summary>Raw strike envelope the shader uses, 0..1.</summary>
        public static float StrikeAt(double clock, float seed)
        {
            double t = clock * .05;
            double s = System.Math.Sin(t * 1.35 + seed) * .5 + .5;
            if (s < 0) s = 0;
            return (float)System.Math.Pow(s, 14.0);
        }

        /// <summary>Envelope value that counts as a visible flash.</summary>
        public const float StrikeThreshold = .35f;
        /// <summary>One world-clock minute, the granularity the driver samples at.</summary>
        public const double StrikeStepSeconds = 1.0;
        /// <summary>Seconds a thunderclap lags its flash — it sounds like weather, not a firework.</summary>
        public const float ThunderLag = .9f;
    }
}
