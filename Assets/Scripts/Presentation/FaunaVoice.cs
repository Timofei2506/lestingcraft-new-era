namespace LestingCraft.Presentation
{
    /// <summary>
    /// How a creature sounds. Deliberately coarse: the frontier's six new species plus the Radia
    /// mutants are the ones that must not share a grazing-deer grunt, everything else keeps the
    /// shipped generic calls.
    /// </summary>
    public enum FaunaVoice
    {
        /// <summary>Generic peaceful mammal (fallback for the 1.x species).</summary>
        Mammal = 0,
        Bird = 1,
        Reptile = 2,
        /// <summary>Insectile chitter (scorpions, spiders).</summary>
        Chitin = 3,
        /// <summary>Mutant/abomination: wet, wrong.</summary>
        Mutant = 4,
        /// <summary>Crystal creature: glassy overtones (Призматическая рысь).</summary>
        Crystal = 5,
        /// <summary>Deep-bodied leviathan call (Геодовый левиафан).</summary>
        Leviathan = 6,
        /// <summary>Flickering ember soul (Пепельный огонёк).</summary>
        Ember = 7,
        /// <summary>Static-laced storm beast (Грозовой ящер).</summary>
        Storm = 8,
        /// <summary>Hollow fungal drone (Споровый олень).</summary>
        Spore = 9,
        /// <summary>Reversed, airless whisper (Пустотный призрак).</summary>
        Void = 10
    }

    /// <summary>
    /// Species id → voice. Pure (ids only, no Unity), so the mapping is verifiable headless and
    /// cannot silently fall back to the deer for a whole new continent's fauna.
    /// </summary>
    public static class FaunaVoices
    {
        // Deep Frontier species ids (AnimalSculpts.SpeciesNames 42..47).
        public const int PrismCat = 42, GeodeLeviathan = 43, AshFlicker = 44,
            StormWyrm = 45, SporeStag = 46, VoidWraith = 47;

        /// <summary>Species with a distinctly frontier body plan.</summary>
        public static bool IsFrontier(int species) { return species >= PrismCat && species <= VoidWraith; }

        public static FaunaVoice For(int species)
        {
            switch (species)
            {
                // ---- Deep Frontier ----
                case PrismCat:      return FaunaVoice.Crystal;
                case GeodeLeviathan:return FaunaVoice.Leviathan;
                case AshFlicker:    return FaunaVoice.Ember;
                case StormWyrm:     return FaunaVoice.Storm;
                case SporeStag:     return FaunaVoice.Spore;
                case VoidWraith:    return FaunaVoice.Void;

                // ---- Radia abyss mutants (20..29) ----
                case 21: case 22: case 26: case 27: case 29:
                    return FaunaVoice.Mutant;

                // ---- legacy fauna, pinned to what already shipped ----
                case 11: case 14: case 15: case 16: case 23: case 24: case 25:
                    return FaunaVoice.Reptile;
                case 5: case 6: case 7: case 8: case 9: case 10: case 17: case 18: case 19: case 20:
                    return FaunaVoice.Mammal;
                case 12: case 13:
                    return FaunaVoice.Bird;

                case 28: // Витрифицированный голем: inert glass body, no voice of its own
                    return FaunaVoice.Crystal;

                default:
                    // Unknown/legacy ids keep the shipped mammal call rather than going silent.
                    return FaunaVoice.Mammal;
            }
        }

        /// <summary>Synth clip prefix for a voice, e.g. "fauna_crystal".</summary>
        public static string Key(FaunaVoice voice)
        {
            switch (voice)
            {
                case FaunaVoice.Bird: return "nature_bird";
                case FaunaVoice.Chitin: return "fauna_chitin";
                case FaunaVoice.Mutant: return "fauna_mutant";
                case FaunaVoice.Crystal: return "fauna_crystal";
                case FaunaVoice.Leviathan: return "fauna_leviathan";
                case FaunaVoice.Ember: return "fauna_ember";
                case FaunaVoice.Storm: return "fauna_storm";
                case FaunaVoice.Spore: return "fauna_spore";
                case FaunaVoice.Void: return "fauna_void";
                case FaunaVoice.Reptile: return "fauna_reptile";
                default: return "fauna_mammal";
            }
        }

        /// <summary>True when the voice key is a creature call, not an ambience loop.</summary>
        public static bool IsCreatureCall(string key)
        {
            return key == "fauna_mammal" || key == "fauna_reptile" || key == "fauna_chitin"
                || key == "fauna_mutant" || key == "fauna_crystal" || key == "fauna_leviathan"
                || key == "fauna_ember" || key == "fauna_storm" || key == "fauna_spore"
                || key == "fauna_void";
        }
    }
}
