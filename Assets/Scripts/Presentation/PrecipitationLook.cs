using LestingCraft.World;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Visual identity of falling precipitation. WeatherSample only says "it is raining / snowing
    /// / storming"; this says what the falling stuff actually looks like in that realm, so soot
    /// over Emberfall does not arrive as bright blue water streaks.
    /// </summary>
    public enum PrecipitationStyle
    {
        WaterRain = 0,
        Snow = 1,
        /// <summary>Dark, slow, tumbling flakes of soot and ash.</summary>
        Ash = 2,
        /// <summary>Fine spore-laden drizzle: greenish, slow, low opacity.</summary>
        Spores = 3,
        /// <summary>Heavy monsoonal drops: long, fast, bright.</summary>
        Monsoon = 4,
        /// <summary>Cryogenic methane drizzle: orange tint, thick, slow.</summary>
        Methane = 5,
        /// <summary>Iron-oxide dust falling out of a thin Martian sky.</summary>
        Dust = 6
    }

    public readonly struct PrecipitationLook
    {
        public readonly PrecipitationStyle Style;
        /// <summary>Streak/flake length multiplier.</summary>
        public readonly float Length;
        /// <summary>Fall speed multiplier applied to the base rain (-26) / snow (-1.7) velocity.</summary>
        public readonly float Fall;
        /// <summary>Size multiplier.</summary>
        public readonly float Size;
        /// <summary>Straight down (rain streaks) vs tumbling flakes.</summary>
        public readonly bool Flakes;
        /// <summary>Channel values 0..1 for the particle tint at full opacity.</summary>
        public readonly float R, G, B, Alpha;

        public PrecipitationLook(PrecipitationStyle style, float length, float fall, float size,
            bool flakes, float r, float g, float b, float alpha)
        {
            Style = style; Length = length; Fall = fall; Size = size; Flakes = flakes;
            R = r; G = g; B = b; Alpha = alpha;
        }

        /// <summary>
        /// True when the look tumbles like a flake rather than streaking like a drop. Driving the
        /// renderer mode from here keeps the two in step instead of them being decided twice.
        /// </summary>
        public bool Billboard { get { return Flakes; } }
    }

    /// <summary>
    /// Realm → precipitation look. Only realms where precipitation actually reaches the ground
    /// (see RealmSoundscape.WeatherReaches) have a distinctive look; airless and sealed realms
    /// still resolve to the default so a stray sample can never spawn bright water rain in vacuum.
    /// </summary>
    public static class PrecipitationLooks
    {
        public static PrecipitationLook For(Dimension realm)
        {
            switch (realm)
            {
                case Dimension.Emberfall:
                    // Soot: dark, slow, small tumbling flakes. Never blue, never bright.
                    return new PrecipitationLook(PrecipitationStyle.Ash, .55f, .30f, .85f, true,
                        .20f, .16f, .15f, .70f);
                case Dimension.Verdant:
                    return new PrecipitationLook(PrecipitationStyle.Spores, .80f, .70f, .75f, true,
                        .62f, .88f, .48f, .48f);
                case Dimension.Aquaria:
                    return new PrecipitationLook(PrecipitationStyle.Monsoon, 1.45f, 1.20f, 1.15f, false,
                        .74f, .86f, .96f, .40f);
                case Dimension.Titan:
                    return new PrecipitationLook(PrecipitationStyle.Methane, 1.10f, .55f, 1.05f, true,
                        .92f, .70f, .34f, .62f);
                case Dimension.Mars:
                    return new PrecipitationLook(PrecipitationStyle.Dust, .70f, .45f, .80f, false,
                        .82f, .48f, .28f, .50f);
                default:
                    // Overworld rain and snow keep the look they had; the cold bodies reuse snow.
                    return new PrecipitationLook(PrecipitationStyle.WaterRain, 1f, 1f, 1f, false,
                        .68f, .80f, .90f, .32f);
            }
        }

        /// <summary>
        /// Look for a realm combined with the sample: snow worlds always tumble, and a realm's
        /// own look wins over the generic water/snow pair, so Emberfall's soot is soot in a storm.
        /// </summary>
        public static PrecipitationLook Resolve(Dimension realm, bool snowing)
        {
            var look = For(realm);
            if (!snowing) return look;
            // Snow: reuse the realm's tint and speed character but as tumbling flakes.
            return new PrecipitationLook(PrecipitationStyle.Snow, look.Length * .9f, look.Fall * .55f,
                look.Size * 1.2f, true, look.R * .85f + .15f, look.G * .85f + .15f, look.B * .85f + .15f,
                look.Alpha + .15f);
        }

        /// <summary>
        /// Airless and sealed realms never show precipitation, whatever the sample claims. Storm
        /// reach is excluded too: its weather is the lightning, not the rain.
        /// </summary>
        public static bool Visible(Dimension realm, float precipitation)
        {
            return precipitation > .01f && RealmSoundscape.WeatherReaches(realm);
        }
    }
}
