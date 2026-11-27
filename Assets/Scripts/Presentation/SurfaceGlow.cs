using LestingCraft.World;

namespace LestingCraft.Presentation
{
    /// <summary>Which living-surface glow a realm's ground renders (see VoxelPBR.shader).</summary>
    public enum SurfaceGlow
    {
        None = 0,
        Mycelium = 1,    // Verdant: fungal caps pulse in the dark
        Kelp = 2,        // Aquaria: bioluminescent kelp weave
        Prism = 3,       // Crystalia: faceted crystal bands
        Aurora = 4,      // Crystalia ice fields: slow shimmer
        Void = 5,        // Nihility: seams of leaking void
        Charge = 6,      // Stormreach: charged glass arcs
        Magma = 7        // Emberfall: magma shore breathing
    }

    /// <summary>
    /// Pure glow policy: how strongly each living surface glows, and how the realm's local
    /// daylight modulates it. No UnityEngine types, so every number here is headless-checkable.
    /// </summary>
    public readonly struct SurfaceGlowProfile
    {
        public readonly SurfaceGlow Kind;
        /// <summary>Glow at local night (0..3).</summary>
        public readonly float NightStrength;
        /// <summary>Glow at local noon, as a fraction of the night strength (0..1).</summary>
        public readonly float DayFactor;

        public SurfaceGlowProfile(SurfaceGlow kind, float nightStrength, float dayFactor)
        {
            Kind = kind;
            NightStrength = nightStrength;
            DayFactor = dayFactor;
        }

        /// <summary>Effective glow for a daylight factor (0 night … 1 noon).</summary>
        public float StrengthAt(float daylight)
        {
            if (Kind == SurfaceGlow.None || NightStrength <= 0f) return 0f;
            float d = daylight < 0f ? 0f : daylight > 1f ? 1f : daylight;
            float day = DayFactor < 0f ? 0f : DayFactor > 1f ? 1f : DayFactor;
            return NightStrength * (1f + (day - 1f) * d);
        }
    }

    /// <summary>
    /// Surface → glow table and the global names the voxel shader reads. Keeping the names here
    /// (and asserting them against the shader in Tools/check_art.py) means a renamed global
    /// cannot silently turn a realm's glow off.
    /// </summary>
    public static class SurfaceGlows
    {
        /// <summary>Shader global for a glow kind, or null for <see cref="SurfaceGlow.None"/>.</summary>
        public static string GlobalName(SurfaceGlow kind)
        {
            switch (kind)
            {
                case SurfaceGlow.Mycelium: return "_LCGlowMycelium";
                case SurfaceGlow.Kelp: return "_LCGlowKelp";
                case SurfaceGlow.Prism: return "_LCGlowPrism";
                case SurfaceGlow.Aurora: return "_LCGlowAurora";
                case SurfaceGlow.Void: return "_LCGlowVoid";
                case SurfaceGlow.Charge: return "_LCGlowCharge";
                case SurfaceGlow.Magma: return "_LCGlowMagma";
                default: return null;
            }
        }

        /// <summary>All globals the voxel shader declares, in table order.</summary>
        public static readonly SurfaceGlow[] All =
        {
            SurfaceGlow.Mycelium, SurfaceGlow.Kelp, SurfaceGlow.Prism,
            SurfaceGlow.Aurora, SurfaceGlow.Void, SurfaceGlow.Charge, SurfaceGlow.Magma
        };

        /// <summary>Glow of one surface in isolation (used by tests and by the tooling).</summary>
        public static SurfaceGlowProfile For(SurfaceGlow kind)
        {
            switch (kind)
            {
                // Fungal caps and spores are clearly visible in daylight, so they only dim.
                case SurfaceGlow.Mycelium: return new SurfaceGlowProfile(kind, 1.00f, .38f);
                // Kelp glows under water all day; photosynthesis light barely competes.
                case SurfaceGlow.Kelp: return new SurfaceGlowProfile(kind, .78f, .55f);
                // Crystal catches sunlight, so its band glow is mostly a night feature.
                case SurfaceGlow.Prism: return new SurfaceGlowProfile(kind, .90f, .22f);
                case SurfaceGlow.Aurora: return new SurfaceGlowProfile(kind, .62f, .30f);
                // Nothing lights the void; the seams are constant.
                case SurfaceGlow.Void: return new SurfaceGlowProfile(kind, 1.15f, .92f);
                // Arcs only read against a dark storm.
                case SurfaceGlow.Charge: return new SurfaceGlowProfile(kind, 1.05f, .28f);
                // Magma is self-lit and reads in daylight too.
                case SurfaceGlow.Magma: return new SurfaceGlowProfile(kind, 1.30f, .70f);
                default: return new SurfaceGlowProfile(SurfaceGlow.None, 0f, 0f);
            }
        }

        /// <summary>
        /// The glow a realm's ground shows. A realm can carry several surfaces (Crystalia has
        /// crystal and ice), so the strongest one wins — the shader branches by layer anyway.
        /// </summary>
        public static SurfaceGlowProfile ForRealm(Dimension realm, float daylight)
        {
            switch (realm)
            {
                case Dimension.Crystalia:
                    return Stronger(For(SurfaceGlow.Prism), For(SurfaceGlow.Aurora), daylight);
                case Dimension.Emberfall:
                    return For(SurfaceGlow.Magma).WithDaylight(daylight);
                case Dimension.Aquaria:
                    return For(SurfaceGlow.Kelp).WithDaylight(daylight);
                case Dimension.Stormreach:
                    return For(SurfaceGlow.Charge).WithDaylight(daylight);
                case Dimension.Verdant:
                    return For(SurfaceGlow.Mycelium).WithDaylight(daylight);
                case Dimension.Nihility:
                    return For(SurfaceGlow.Void).WithDaylight(daylight);
                default:
                    return new SurfaceGlowProfile(SurfaceGlow.None, 0f, 0f);
            }
        }

        static SurfaceGlowProfile Stronger(SurfaceGlowProfile a, SurfaceGlowProfile b, float daylight)
        {
            return a.StrengthAt(daylight) >= b.StrengthAt(daylight) ? a : b;
        }
    }

    internal static class SurfaceGlowProfileExtensions
    {
        /// <summary>Re-tags a profile with a daylight factor baked in, for callers that only want a number.</summary>
        public static SurfaceGlowProfile WithDaylight(this SurfaceGlowProfile profile, float daylight)
        {
            return new SurfaceGlowProfile(profile.Kind, profile.StrengthAt(daylight), 1f);
        }
    }
}
