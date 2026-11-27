namespace LestingCraft.Presentation
{
    public enum SurfaceSoundKind
    {
        Stone = 0,
        Grass = 1,
        Dirt = 2,
        Wood = 3,
        Snow = 4,
        Water = 5,
        Metal = 6
    }

    /// <summary>
    /// Footstep material for a block id. Pure and engine-free so the mapping can be verified
    /// headless - it is the only thing deciding what a player hears when they walk across the
    /// frontier's crystal, kelp, glass and void surfaces.
    /// </summary>
    public static class SurfaceSounds
    {
        // Frontier block ids (see ExpandedContent): 115 prism crystal, 116 aurora ice,
        // 117 soot dune, 118 magma shore, 119 kelp weave, 124 reef shelf, 125 tidal ruin,
        // 126 charge glass, 127 storm iron, 128 mycelium cap, 129 spore loam, 133 void shard.
        public const ushort CrystaliaPrism = 115, CrystaliaIce = 116, EmberfallSoot = 117,
            EmberfallMagmaShore = 118, AquariaKelp = 119, AquariaReef = 124, AquariaRuin = 125,
            StormreachGlass = 126, StormreachIron = 127, VerdantMycelium = 128,
            VerdantSporeLoam = 129, NihilityVoidShard = 133;

        public static SurfaceSoundKind Classify(ushort blockId)
        {
            switch (blockId)
            {
                case 7: case 8:
                    return SurfaceSoundKind.Water;
                case 3: case 5: case 44: case 23: case 24:
                    return SurfaceSoundKind.Grass;
                case 2: case 6: case 13: case 27: case 34:
                    return SurfaceSoundKind.Dirt;
                case 4: case 12: case 16: case 18:
                    return SurfaceSoundKind.Wood;
                case 14: case 32: case 33:
                    return SurfaceSoundKind.Snow;

                // ---- Deep Frontier (2.0) ----
                case CrystaliaPrism:   // ringing crystal
                case StormreachGlass:  // charged glass: a hard, brittle ring
                    return SurfaceSoundKind.Metal;
                case CrystaliaIce:     // glacial sheet: crisp, like packed snow
                    return SurfaceSoundKind.Snow;
                case AquariaKelp:      // wet, springy weave
                case VerdantMycelium:  // soft organic caps
                case VerdantSporeLoam: // spongy loam
                    return SurfaceSoundKind.Grass;
                case EmberfallSoot:    // loose, muffled ash
                    return SurfaceSoundKind.Dirt;
                case EmberfallMagmaShore:
                case AquariaReef:
                case AquariaRuin:
                    return SurfaceSoundKind.Stone;
                case StormreachIron:   // riveted lattice
                    return SurfaceSoundKind.Metal;
                case NihilityVoidShard: // a void shard rings like struck glass
                    return SurfaceSoundKind.Metal;

                default:
                    if (blockId >= 54 && blockId <= 69) return SurfaceSoundKind.Metal;
                    return SurfaceSoundKind.Stone;
            }
        }
    }
}
