using LestingCraft.World;

namespace LestingCraft.Presentation
{
    /// <summary>What a realm's ground-level motes are made of. Picks colour, size and motion.</summary>
    public enum MoteKind
    {
        /// <summary>No motes (enclosed or vacuum interiors where nothing drifts).</summary>
        None = 0,
        Pollen = 1,
        Ash = 2,
        Snow = 3,
        Dust = 4,
        Crystal = 5,
        Suction = 6,
        Bio = 7,
        Salt = 8,
        Void = 9,
        Ember = 10
    }

    /// <summary>
    /// A realm's ground-level ambient particle look: what drifts in front of the camera when
    /// nothing is falling from the sky. Engine-free so the whole table is verifiable headless,
    /// and deliberately separate from weather — weather is precipitation, this is atmosphere.
    /// </summary>
    public readonly struct MoteProfile
    {
        public readonly MoteKind Kind;
        /// <summary>Motes per second at full density, before weather damping.</summary>
        public readonly float Rate;
        /// <summary>Particle size in world units (min, max).</summary>
        public readonly float SizeMin, SizeMax;
        /// <summary>Rising (+) or falling (-) drift in m/s.</summary>
        public readonly float Vertical;
        /// <summary>Horizontal drift in m/s.</summary>
        public readonly float Lateral;
        /// <summary>Half-extent of the spawning box around the camera.</summary>
        public readonly float Box;
        /// <summary>Body colour, 0..1 sRGB.</summary>
        public readonly float R, G, B;
        /// <summary>Alpha at full density.</summary>
        public readonly float Alpha;
        /// <summary>Additive blending: motes are light, not dust.</summary>
        public readonly bool Additive;
        /// <summary>Motes brighten with the surface glow of the realm (night-only shimmer).</summary>
        public readonly bool FollowsGlow;

        public MoteProfile(MoteKind kind, float rate, float sizeMin, float sizeMax, float vertical,
            float lateral, float box, float r, float g, float b, float alpha, bool additive, bool followsGlow)
        {
            Kind = kind; Rate = rate; SizeMin = sizeMin; SizeMax = sizeMax;
            Vertical = vertical; Lateral = lateral; Box = box;
            R = r; G = g; B = b; Alpha = alpha; Additive = additive; FollowsGlow = followsGlow;
        }

        public bool Active { get { return Kind != MoteKind.None && Rate > 0f; } }
    }

    /// <summary>
    /// Realm → motes table. Also folds in weather: an active rain or snow storm damps the
    /// ground motes (they would be invisible behind the precipitation anyway) and heavy wind
    /// pushes the drift sideways, so the same realm reads differently in calm and in storm.
    /// </summary>
    public static class MoteField
    {
        public static MoteProfile For(Dimension realm)
        {
            switch (realm)
            {
                // ---- Deep Frontier ----
                case Dimension.Crystalia:
                    // Airless: no drifting air, but glacial dust still slides off the spires.
                    return new MoteProfile(MoteKind.Crystal, 7f, .020f, .045f, -.35f, .55f, 14f,
                        .74f, .93f, 1f, .70f, true, true);
                case Dimension.Emberfall:
                    return new MoteProfile(MoteKind.Ember, 26f, .014f, .034f, .85f, .90f, 16f,
                        1f, .55f, .22f, .85f, true, true);
                case Dimension.Aquaria:
                    // Salt spray and plankton drifting up off the surf.
                    return new MoteProfile(MoteKind.Salt, 16f, .010f, .024f, .30f, 1.30f, 18f,
                        .72f, .92f, .96f, .48f, false, false);
                case Dimension.Stormreach:
                    // Charged motes drag upwards into the thunderhead.
                    return new MoteProfile(MoteKind.Crystal, 22f, .012f, .028f, 1.60f, 1.10f, 18f,
                        .80f, .88f, 1f, .72f, true, true);
                case Dimension.Verdant:
                    return new MoteProfile(MoteKind.Bio, 30f, .018f, .048f, .70f, .50f, 16f,
                        .62f, 1f, .55f, .78f, true, true);
                case Dimension.Nihility:
                    return new MoteProfile(MoteKind.Void, 12f, .022f, .060f, .10f, -.70f, 20f,
                        .58f, .38f, .95f, .66f, true, true);

                // ---- legacy realms ----
                case Dimension.Lestingia:
                    return new MoteProfile(MoteKind.Pollen, 14f, .010f, .022f, .28f, .40f, 16f,
                        1f, .96f, .72f, .42f, false, false);
                case Dimension.Paradise:
                    return new MoteProfile(MoteKind.Pollen, 18f, .010f, .026f, .34f, .35f, 16f,
                        1f, .90f, .82f, .46f, false, false);
                case Dimension.Inferno:
                    return new MoteProfile(MoteKind.Ash, 34f, .012f, .030f, -.55f, .60f, 16f,
                        .42f, .38f, .36f, .60f, false, false);
                case Dimension.Mars:
                    return new MoteProfile(MoteKind.Dust, 20f, .012f, .026f, -.10f, 1.40f, 20f,
                        .82f, .48f, .30f, .48f, false, false);
                case Dimension.Venus:
                    return new MoteProfile(MoteKind.Dust, 24f, .014f, .030f, -.08f, .80f, 18f,
                        .92f, .82f, .52f, .55f, false, false);
                case Dimension.Europa:
                    return new MoteProfile(MoteKind.Snow, 12f, .012f, .028f, -.30f, .35f, 14f,
                        .86f, .94f, 1f, .55f, false, false);
                case Dimension.Titan:
                    return new MoteProfile(MoteKind.Salt, 10f, .012f, .030f, -.20f, .30f, 14f,
                        .90f, .74f, .40f, .44f, false, false);
                case Dimension.Somnia:
                    return new MoteProfile(MoteKind.Bio, 12f, .014f, .034f, .40f, .30f, 16f,
                        .78f, .82f, 1f, .52f, true, true);
                case Dimension.Radia:
                    return new MoteProfile(MoteKind.Bio, 10f, .012f, .030f, .45f, .30f, 16f,
                        .62f, 1f, .48f, .58f, true, true);
                case Dimension.AndromedaPrime:
                    return new MoteProfile(MoteKind.Void, 9f, .018f, .044f, .05f, -.50f, 20f,
                        .72f, .52f, 1f, .55f, true, true);

                // Sealed interiors and hard vacuum: nothing drifts in front of the camera.
                case Dimension.Backrooms:
                case Dimension.Dream:
                case Dimension.Moon:
                case Dimension.Mercury:
                case Dimension.Pluto:
                case Dimension.ProximaB:
                case Dimension.Trappist1e:
                case Dimension.Kepler452b:
                    return NoneProfile;

                default:
                    return new MoteProfile(MoteKind.Pollen, 12f, .010f, .022f, .28f, .40f, 16f,
                        1f, .96f, .72f, .42f, false, false);
            }
        }

        /// <summary>
        /// Final per-frame look for a realm after weather and the living-surface glow are folded
        /// in. Precipitation damps the motes, wind stretches the lateral drift, and glow-following
        /// motes brighten with the realm's own surface glow (so they are nearly invisible at noon).
        /// </summary>
        public static MoteProfile Resolve(Dimension realm, float precipitation, float wind,
            float glow, bool sheltered)
        {
            var p = For(realm);
            if (!p.Active) return p;
            if (sheltered) precipitation *= .35f;
            // Motes survive a drizzle and disappear in a downpour.
            float weatherDamp = 1f - Clamp01(precipitation) * .80f;
            float rate = p.Rate * weatherDamp;
            float alpha = p.Alpha * (0.35f + 0.65f * weatherDamp);
            float lateral = p.Lateral + Clamp01(wind) * 1.6f * (p.Lateral < 0f ? -1f : 1f);
            float vertical = p.Vertical;
            if (p.FollowsGlow)
            {
                // Glow-following motes also swell in brightness, not in number: density stays
                // readable while their presence at noon drops to a whisper.
                float g = Clamp01(glow);
                alpha = alpha * (0.45f + 0.55f * g);
                vertical = vertical * (0.6f + 0.4f * g);
            }
            return new MoteProfile(p.Kind, rate, p.SizeMin, p.SizeMax, vertical, lateral, p.Box,
                p.R, p.G, p.B, alpha, p.Additive, p.FollowsGlow);
        }

        /// <summary>Emission rate in particles/second that a particle system should be fed.</summary>
        public static float EmissionRate(Dimension realm, float precipitation, float wind, float glow, bool sheltered)
        {
            var p = Resolve(realm, precipitation, wind, glow, sheltered);
            return p.Active ? p.Rate : 0f;
        }

        /// <summary>Upper bound on live particles, so the system can never overrun its budget.</summary>
        public static int MaxParticles(MoteProfile p)
        {
            if (!p.Active) return 0;
            // Lifetime is fixed at 6 s in the consumer; keep a 25% headroom for rain damping.
            int budget = (int)(p.Rate * 6f * 1.25f) + 8;
            return budget > 400 ? 400 : budget;
        }

        /// <summary>Explicitly no motes (vacuum EVA, sealed interiors) without going through a realm id.</summary>
        public static MoteProfile NoneProfile
        {
            get { return new MoteProfile(MoteKind.None, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false, false); }
        }

        static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
    }
}
