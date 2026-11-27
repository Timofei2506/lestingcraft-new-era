using System;
using LestingCraft.World;

namespace LestingCraft.Presentation
{
    /// <summary>Which atmospheric sky effect a realm renders (see AtmosphericSky.shader).</summary>
    public enum SkyEffect
    {
        None = 0,
        Aurora = 1,        // Crystalia: ionised curtains over an airless prismic moon
        Lightning = 2,     // Stormreach: permanent thunderhead
        Spores = 3,        // Verdant: luminous spores drifting under the caps
        VoidFractures = 4, // Nihility: seams of broken reality
        EmberUpdraft = 5,  // Emberfall: sparks and heat shimmer over the soot sea
        SeaHaze = 6        // Aquaria: bioluminescent haze and salt shimmer over open water
    }

    /// <summary>
    /// Pure, engine-free description of a realm's sky. Kept out of the shader driver so the
    /// choice of effect, its intensity, its speed and the day/night modulation can all be
    /// asserted by headless checks — a new realm only needs one row here.
    /// </summary>
    public readonly struct SkyEffectProfile
    {
        public readonly SkyEffect Effect;
        /// <summary>Peak intensity handed to the shader (0 = effect off).</summary>
        public readonly float Strength;
        /// <summary>Animation speed in "seconds per unit": higher is slower.</summary>
        public readonly float Frequency;
        /// <summary>Whether the effect fades out in local daylight (aurora) or is always on.</summary>
        public readonly bool DaylightFade;
        /// <summary>How strongly the effect is boosted at local night, 0..1.</summary>
        public readonly float NightBoost;

        public SkyEffectProfile(SkyEffect effect, float strength, float frequency, bool daylightFade, float nightBoost)
        {
            Effect = effect;
            Strength = strength;
            Frequency = frequency;
            DaylightFade = daylightFade;
            NightBoost = nightBoost;
        }

        /// <summary>Effective strength for the current daylight factor (0 night … 1 noon).</summary>
        public float StrengthAt(float daylight)
        {
            if (Strength <= 0f || Effect == SkyEffect.None) return 0f;
            float d = daylight < 0f ? 0f : daylight > 1f ? 1f : daylight;
            if (!DaylightFade) return Strength;
            return Strength * (1f - d * (1f - NightBoost));
        }

        public bool VisibleAt(float daylight) { return StrengthAt(daylight) > .002f; }
    }

    /// <summary>
    /// Realm → sky effect table. Every realm has a row; realms without a special effect return
    /// <see cref="SkyEffect.None"/> so the shader branch is skipped entirely.
    /// </summary>
    public static class SkyEffects
    {
        public static SkyEffectProfile For(Dimension realm)
        {
            switch (realm)
            {
                // ---- Deep Frontier (2.0) ----
                case Dimension.Crystalia:
                    // Airless: the aurora is the only light in the sky and blooms when the sun sets.
                    return new SkyEffectProfile(SkyEffect.Aurora, 1.15f, 1f, true, .22f);
                case Dimension.Emberfall:
                    // Fire-lit haze, stronger at night when the soot sea glows.
                    return new SkyEffectProfile(SkyEffect.EmberUpdraft, .85f, 1f, true, .55f);
                case Dimension.Stormreach:
                    // The storm never stops and lightning is the point of the realm.
                    return new SkyEffectProfile(SkyEffect.Lightning, 1.35f, 1f, false, 1f);
                case Dimension.Aquaria:
                    // Salt haze over open water: only reads while the sun is up.
                    return new SkyEffectProfile(SkyEffect.SeaHaze, .80f, 1f, false, 1f);
                case Dimension.Verdant:
                    // Spores are brightest in daylight, filtered through the canopy.
                    return new SkyEffectProfile(SkyEffect.Spores, .70f, 1f, false, 1f);
                case Dimension.Nihility:
                    // No sun to speak of: the seams are constant.
                    return new SkyEffectProfile(SkyEffect.VoidFractures, 1.05f, 1f, false, 1f);

                // ---- Legacy realms that already had a distinctive sky ----
                case Dimension.Somnia:
                    return new SkyEffectProfile(SkyEffect.Aurora, .45f, 1f, true, .35f);
                case Dimension.Dream:
                    return new SkyEffectProfile(SkyEffect.Spores, .38f, 1f, false, 1f);
                case Dimension.AndromedaPrime:
                    return new SkyEffectProfile(SkyEffect.VoidFractures, .42f, 1f, false, 1f);
                case Dimension.Radia:
                    return new SkyEffectProfile(SkyEffect.EmberUpdraft, .32f, 1f, true, .40f);

                default:
                    return new SkyEffectProfile(SkyEffect.None, 0f, 1f, false, 1f);
            }
        }

        /// <summary>True when the realm renders any procedural sky effect.</summary>
        public static bool HasEffect(Dimension realm) { return For(realm).Effect != SkyEffect.None; }

        /// <summary>
        /// Deterministic shader seed for a realm, so two realms never animate identically even
        /// when they share an effect (Somnia's aurora must not be Crystalia's aurora).
        /// </summary>
        public static float Seed(Dimension realm)
        {
            int id = (int)realm;
            unchecked
            {
                uint h = (uint)(id * 2654435761u);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h % 1000u) / 1000f * 6.2831853f;
            }
        }

        /// <summary>Shader-ready parameter packet: mode, strength, time, seed.</summary>
        public static void Resolve(Dimension realm, double clock, float daylight,
            out int mode, out float strength, out float time, out float seed)
        {
            var profile = For(realm);
            mode = (int)profile.Effect;
            strength = profile.StrengthAt(daylight);
            // The world clock advances 1 per in-game minute; scaled so a cycle reads well.
            time = (float)(clock * .05);
            seed = Seed(realm);
        }
    }
}
