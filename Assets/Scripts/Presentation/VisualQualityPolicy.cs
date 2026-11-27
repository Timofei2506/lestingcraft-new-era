using System;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Pure, engine-free quality policy. Shared by runtime, editor bake and headless tests so
    /// mobile/desktop budgets and legacy preference migration cannot drift apart.
    /// Values are explicit user choices; nothing here silently downgrades quality by FPS.
    ///
    /// Schema 3 (2.0 "Frontier") adds two desktop tiers above the old Cinematic ceiling and a
    /// per-preset LOD band, so the wider view radius that 2.0 allows is actually affordable:
    /// the extra rings skip collider bakes, vegetation detail and authored-object rigs.
    /// </summary>
    public static class VisualQualityPolicy
    {
        public const int SchemaVersion = 3;
        public const int Mobile = 0;
        public const int Balanced = 1;
        public const int High = 2;
        public const int Ultra = 3;
        public const int Cinematic = 4;
        public const int Frontier = 5;
        public const int Extreme = 6;

        public struct Profile
        {
            public int DetailQuality;
            public int ShadowResolution;
            public int ShadowCascades;
            public int ParallaxSteps;
            public int WaterSteps;
            public int GrassRange;
            public int CloudSteps;
            public int FogSteps;
            public float ShadowDistanceMultiplier;
            public bool UltraEffects;
            /// <summary>Chunks beyond this ring are meshed but never bake colliders or rig objects.</summary>
            public int DetailRing;
            /// <summary>World-space distance where surface detail (grass) starts fading out.</summary>
            public float DetailFadeStart;
            public float DetailFadeEnd;
        }

        public static int DefaultPreset(bool mobile) { return mobile ? Balanced : High; }

        public static int ClampPreset(int preset, bool mobile)
        {
            int max = mobile ? Ultra : Extreme;
            return preset < Mobile ? Mobile : preset > max ? max : preset;
        }

        /// <summary>
        /// Legacy prefs used 0=High, 1=Ultra (schema 0/1) and a 5-step ladder in schema 2.
        /// Schema 2 values are kept as-is because the first five ids are unchanged.
        /// </summary>
        public static int MigratePreset(int preset, int schema)
        {
            if (schema >= 3) return preset;
            if (schema == 2) return preset;
            return preset <= 0 ? High : Ultra;
        }

        public static int MaxViewDistance(bool mobile) { return mobile ? 4 : 7; }
        public static int MinViewDistance { get { return 2; } }

        public static int ClampViewDistance(int chunks, bool mobile)
        {
            int max = MaxViewDistance(mobile);
            return chunks < MinViewDistance ? MinViewDistance : chunks > max ? max : chunks;
        }

        public static float ClampRenderScale(float scale, bool mobile)
        {
            float min = mobile ? .65f : .85f;
            float max = mobile ? 1.0f : 1.5f;
            return scale < min ? min : scale > max ? max : scale;
        }

        public static int ClampShadowResolution(int resolution, bool mobile)
        {
            if (mobile) return resolution >= 2048 ? 2048 : resolution >= 1024 ? 1024 : 512;
            return resolution >= 8192 ? 8192 : resolution >= 4096 ? 4096 : resolution >= 2048 ? 2048 : 1024;
        }

        public static Profile For(int preset) { return For(preset, false); }

        /// <summary>Platform-aware budgets. Mobile tops out at the desktop Ultra row.</summary>
        public static Profile For(int preset, bool mobile)
        {
            if (mobile)
            {
                switch (ClampPreset(preset, true))
                {
                    case Mobile:
                        return P(0, 512, 1, 0, 6, 22, 10, 6, 16, false, 2, 18f, 30f);
                    case Balanced:
                        return P(1, 1024, 2, 4, 8, 32, 16, 12, 20, false, 2, 24f, 40f);
                    case High:
                        return P(2, 1024, 2, 4, 8, 32, 16, 12, 20, false, 3, 30f, 48f);
                    default:
                        return P(3, 2048, 3, 6, 10, 42, 24, 20, 24, false, 3, 34f, 54f);
                }
            }
            switch (ClampPreset(preset, false))
            {
                case Mobile:
                    return P(0, 512, 1, 0, 6, 22, 10, 6, 16, false, 2, 18f, 30f);
                case Balanced:
                    return P(1, 1024, 2, 4, 8, 32, 16, 12, 20, false, 2, 24f, 40f);
                case High:
                    return P(2, 2048, 3, 8, 12, 48, 28, 24, 26, false, 3, 40f, 62f);
                case Ultra:
                    return P(3, 4096, 4, 14, 18, 72, 44, 40, 30, true, 4, 52f, 78f);
                case Cinematic:
                    return P(4, 4096, 4, 18, 22, 88, 60, 56, 34, true, 5, 64f, 96f);
                case Frontier:
                    return P(4, 8192, 4, 22, 26, 100, 72, 68, 38, true, 5, 76f, 112f);
                default:
                    return P(5, 8192, 4, 28, 32, 128, 84, 80, 42, true, 6, 92f, 140f);
            }
        }

        static Profile P(int detail, int shadowRes, int cascades, int parallax, int water, int grass,
            int cloudSteps, int fogSteps, float shadowDistance, bool ultraEffects, int detailRing,
            float fadeStart, float fadeEnd)
        {
            return new Profile
            {
                DetailQuality = detail,
                ShadowResolution = shadowRes,
                ShadowCascades = cascades,
                ParallaxSteps = parallax,
                WaterSteps = water,
                GrassRange = grass,
                CloudSteps = cloudSteps,
                FogSteps = fogSteps,
                ShadowDistanceMultiplier = shadowDistance,
                UltraEffects = ultraEffects,
                DetailRing = detailRing,
                DetailFadeStart = fadeStart,
                DetailFadeEnd = fadeEnd
            };
        }

        public static string DisplayName(int preset)
        {
            switch (ClampPreset(preset, false))
            {
                case Mobile: return "Мобильный";
                case Balanced: return "Сбалансированный";
                case High: return "Высокий";
                case Ultra: return "Ultra";
                case Cinematic: return "Кино";
                case Frontier: return "Фронтир";
                default: return "Экстрим";
            }
        }

        public static string ShortName(int preset)
        {
            switch (ClampPreset(preset, false))
            {
                case Mobile: return "MOB";
                case Balanced: return "BAL";
                case High: return "HIGH";
                case Ultra: return "ULTRA";
                case Cinematic: return "CINE";
                case Frontier: return "FRONT";
                default: return "XTREME";
            }
        }

        /// <summary>Preset ladder in UI order, desktop first five are also valid on mobile.</summary>
        public static readonly int[] Ladder = { Mobile, Balanced, High, Ultra, Cinematic, Frontier, Extreme };
    }
}
