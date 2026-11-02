using System;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Pure, engine-free quality policy. Shared by runtime, editor bake and headless tests so
    /// mobile/desktop budgets and legacy preference migration cannot drift apart.
    /// Values are explicit user choices; nothing here silently downgrades quality by FPS.
    /// </summary>
    public static class VisualQualityPolicy
    {
        public const int SchemaVersion = 2;
        public const int Mobile = 0;
        public const int Balanced = 1;
        public const int High = 2;
        public const int Ultra = 3;
        public const int Cinematic = 4;

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
        }

        public static int DefaultPreset(bool mobile) { return mobile ? Balanced : High; }

        public static int ClampPreset(int preset, bool mobile)
        {
            int max = mobile ? Ultra : Cinematic;
            return preset < Mobile ? Mobile : preset > max ? max : preset;
        }

        /// <summary>Legacy prefs used 0=High, 1=Ultra. Map them once, never reinterpret as Mobile/Balanced.</summary>
        public static int MigratePreset(int preset, int schema)
        {
            if (schema >= SchemaVersion) return preset;
            return preset <= 0 ? High : Ultra;
        }

        public static int ClampViewDistance(int chunks, bool mobile)
        {
            int max = mobile ? 4 : 5;
            return chunks < 2 ? 2 : chunks > max ? max : chunks;
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
            return resolution >= 4096 ? 4096 : resolution >= 2048 ? 2048 : 1024;
        }

        public static Profile For(int preset)
        {
            switch (ClampPreset(preset, false))
            {
                case Mobile:
                    return new Profile { DetailQuality = 0, ShadowResolution = 512, ShadowCascades = 1, ParallaxSteps = 0, WaterSteps = 6, GrassRange = 22, CloudSteps = 10, FogSteps = 6, ShadowDistanceMultiplier = 16, UltraEffects = false };
                case Balanced:
                    return new Profile { DetailQuality = 1, ShadowResolution = 1024, ShadowCascades = 2, ParallaxSteps = 4, WaterSteps = 8, GrassRange = 32, CloudSteps = 16, FogSteps = 12, ShadowDistanceMultiplier = 20, UltraEffects = false };
                case High:
                    return new Profile { DetailQuality = 2, ShadowResolution = 2048, ShadowCascades = 3, ParallaxSteps = 6, WaterSteps = 10, GrassRange = 42, CloudSteps = 24, FogSteps = 24, ShadowDistanceMultiplier = 26, UltraEffects = false };
                case Ultra:
                    return new Profile { DetailQuality = 3, ShadowResolution = 4096, ShadowCascades = 4, ParallaxSteps = 12, WaterSteps = 16, GrassRange = 64, CloudSteps = 40, FogSteps = 40, ShadowDistanceMultiplier = 30, UltraEffects = true };
                default:
                    return new Profile { DetailQuality = 4, ShadowResolution = 4096, ShadowCascades = 4, ParallaxSteps = 16, WaterSteps = 20, GrassRange = 80, CloudSteps = 56, FogSteps = 56, ShadowDistanceMultiplier = 34, UltraEffects = true };
            }
        }

        public static string DisplayName(int preset)
        {
            switch (ClampPreset(preset, false))
            {
                case Mobile: return "Мобильный";
                case Balanced: return "Сбалансированный";
                case High: return "Высокий";
                case Ultra: return "Ultra";
                default: return "Кино";
            }
        }
    }
}
