using LestingCraft.World;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// One world guardian's voice: which clips it uses and how they are pitched. Engine-free, so
    /// the six frontier bosses can be asserted to sound different from each other and from the
    /// 1.x bosses without opening Unity.
    /// </summary>
    public readonly struct BossVoice
    {
        /// <summary>Clip played when the boss takes a hit.</summary>
        public readonly string Hit;
        /// <summary>Clip played when the boss dies.</summary>
        public readonly string Death;
        /// <summary>Pitch centre for the hit, so small/big guardians read differently.</summary>
        public readonly float Pitch;
        /// <summary>Gain multiplier applied on top of the mob-hit level.</summary>
        public readonly float Gain;

        public BossVoice(string hit, string death, float pitch, float gain)
        {
            Hit = hit; Death = death; Pitch = pitch; Gain = gain;
        }

        /// <summary>True when the voice is a realm-specific synth pair rather than the generic chord.</summary>
        public bool IsCustom
        {
            get
            {
                return !string.IsNullOrEmpty(Hit) && !string.IsNullOrEmpty(Death)
                    && Hit.StartsWith("boss_") && Death.StartsWith("boss_");
            }
        }
    }

    /// <summary>
    /// Realm → guardian voice. Every Deep Frontier world gets its own pair of synth clips, so a
    /// lightning archon does not grunt like a rock golem.
    /// </summary>
    public static class BossVoices
    {
        /// <summary>Clip keys the table can ask for; GameAudio must synthesize all of them.</summary>
        public static readonly string[] Keys =
        {
            "boss_hit_crystal", "boss_death_crystal",
            "boss_hit_cinder", "boss_death_cinder",
            "boss_hit_abyss", "boss_death_abyss",
            "boss_hit_thunder", "boss_death_thunder",
            "boss_hit_mycelium", "boss_death_mycelium",
            "boss_hit_void", "boss_death_void"
        };

        public static BossVoice For(Dimension realm)
        {
            switch (realm)
            {
                case Dimension.Crystalia:   // Prism Sovereign: glass ringing, high and brittle
                    return new BossVoice("boss_hit_crystal", "boss_death_crystal", 1.26f, 1f);
                case Dimension.Emberfall:   // Cinder Titan: furnace roar, low and slow
                    return new BossVoice("boss_hit_cinder", "boss_death_cinder", .82f, 1.12f);
                case Dimension.Aquaria:     // Abyssal Maw: wet gurgle from below
                    return new BossVoice("boss_hit_abyss", "boss_death_abyss", .72f, 1.08f);
                case Dimension.Stormreach:  // Thunder Archon: crack of the storm itself
                    return new BossVoice("boss_hit_thunder", "boss_death_thunder", 1.10f, 1.16f);
                case Dimension.Verdant:     // fungal colossus: organic moan
                    return new BossVoice("boss_hit_mycelium", "boss_death_mycelium", .92f, 1.04f);
                case Dimension.Nihility:    // broken-reality guardian: reversed void swell
                    return new BossVoice("boss_hit_void", "boss_death_void", .68f, 1.20f);
                default:
                    // 1.x guardians keep the shipped impact/kill chord.
                    return new BossVoice("hit_flesh", "hit_kill", 1f, 1f);
            }
        }

        /// <summary>True when the realm has its own recorded synth voice rather than the generic chord.</summary>
        public static bool IsFrontierVoice(Dimension realm) { return For(realm).IsCustom; }
    }
}
