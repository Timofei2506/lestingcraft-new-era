using System;

namespace LestingCraft.World
{
    /// <summary>Surface material of a Moon column. Block ids are assigned when the dimension is wired.</summary>
    public enum MoonSurface : byte { Regolith = 0, Mare = 1, Ejecta = 2 }

    public struct MoonColumn
    {
        public int Height;
        public MoonSurface Surface;
    }

    /// <summary>
    /// Stage 1.6 generator: Moon heightfield as a pure function of (seed, x, z). No Unity types, so the harness runs it under mono.
    /// Layers: highlands (fractal noise), flat maria (basins with low noise), craters (one per grid cell, bowl + rim + ejecta).
    /// Version is part of the save format and the network protocol once the dimension is wired.
    /// </summary>
    public static class MoonTerrain
    {
        public const int Version = 1;
        public const int HighlandLevel = 44;
        public const int MareLevel = 37;
        public const int MinHeight = 20;
        public const int MaxHeight = 80;

        /// <summary>One crater per cell of this size. Maximum radius plus rim must stay inside the neighbour search.</summary>
        public const int CellSize = 48;
        public const float MinRadius = 3f;
        public const float MaxRadius = 14f;
        /// <summary>Probability that a cell holds a crater.</summary>
        const float CraterChance = 0.65f;

        public struct Crater
        {
            public bool Present;
            public float X, Z, Radius, Depth;
            public bool Fresh;
        }

        public static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)x * 374761393u ^ (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        static float Unit(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFFFFu) / 16777215f;

        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Math.Max(0f, Math.Min(1f, (x - edge0) / (edge1 - edge0)));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Smooth value noise in [0, 1].</summary>
        public static float ValueNoise(float x, float y, int seed)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float tx = Fade(x - ix), ty = Fade(y - iy);
            float a = Unit(ix, iy, seed), b = Unit(ix + 1, iy, seed);
            float c = Unit(ix, iy + 1, seed), d = Unit(ix + 1, iy + 1, seed);
            return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), ty);
        }

        /// <summary>Fractal noise (sum of octaves, gain 0.5), normalised to [0, 1].</summary>
        public static float Fbm(float x, float y, int seed, int octaves)
        {
            float sum = 0f, amp = 1f, norm = 0f, f = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * ValueNoise(x * f, y * f, seed + i * 977);
                norm += amp;
                amp *= 0.5f;
                f *= 2.03f;
            }
            return sum / norm;
        }

        /// <summary>Crater of grid cell (cx, cz). Cells without a crater return Present = false.</summary>
        public static Crater CraterInCell(int cx, int cz, int seed)
        {
            var c = new Crater();
            if (Unit(cx, cz, seed + 1) > CraterChance) return c;
            float u = Unit(cx, cz, seed + 2);
            c.Present = true;
            c.Radius = MinRadius + (MaxRadius - MinRadius) * u * u * u;
            float jitter = CellSize / 2f - MaxRadius;
            c.X = cx * CellSize + CellSize / 2f + (Unit(cx, cz, seed + 3) * 2f - 1f) * jitter;
            c.Z = cz * CellSize + CellSize / 2f + (Unit(cx, cz, seed + 4) * 2f - 1f) * jitter;
            c.Depth = 0.3f * c.Radius * (0.8f + 0.4f * Unit(cx, cz, seed + 5));
            c.Fresh = Unit(cx, cz, seed + 6) > 0.4f;
            return c;
        }

        /// <summary>
        /// Height change caused by one crater at distance dist from its centre. Negative inside, positive on the rim.
        /// Continuous everywhere: bowl from -Depth at the centre up to the rim crest at t = 1, rim falls to zero at t = 1.3,
        /// ejecta rises from zero at the crest and fades out before t = 3.
        /// </summary>
        public static float CraterDelta(in Crater c, float dist)
        {
            float t = dist / c.Radius;
            float rimHeight = 0.35f * c.Depth;
            if (t < 1f) return -c.Depth + (c.Depth + rimHeight) * t * t;
            float d = 0f;
            if (t < 1.3f)
            {
                float r = (t - 1f) / 0.3f;
                d += rimHeight * (1f - r * r);
            }
            // Ejecta starts at zero on the crest and fades out by t = 3.
            if (t < 3f) d += rimHeight * 0.6f * (t - 1f) * (float)Math.Exp(-(t - 1f) * 3f);
            return d;
        }

        /// <summary>Share of the surface that is maria, in [0, 1]. 1 means fully flat basalt plain.</summary>
        public static float MareMask(float x, float z, int seed)
        {
            float v = Fbm(x / 150f, z / 150f, seed + 101, 3);
            return Smooth(0.52f, 0.60f, v);
        }

        public static MoonColumn Column(int x, int z, int seed)
        {
            float fx = x + 0.5f, fz = z + 0.5f;
            float mare = MareMask(fx, fz, seed);
            float highland = HighlandLevel + (Fbm(fx / 90f, fz / 90f, seed + 11, 4) - 0.5f) * 46f
                + (Fbm(fx / 14f, fz / 14f, seed + 23, 3) - 0.5f) * 4f;
            float mareLevel = MareLevel + (Fbm(fx / 24f, fz / 24f, seed + 31, 2) - 0.5f) * 1.2f;
            float h = Lerp(highland, mareLevel, mare);

            int cellX = (int)Math.Floor(x / (float)CellSize), cellZ = (int)Math.Floor(z / (float)CellSize);
            bool ejecta = false;
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var c = CraterInCell(cellX + dx, cellZ + dz, seed);
                if (!c.Present) continue;
                float ddx = fx - c.X, ddz = fz - c.Z;
                float dist = (float)Math.Sqrt(ddx * ddx + ddz * ddz);
                if (dist > c.Radius * 3f) continue;
                h += CraterDelta(c, dist);
                float t = dist / c.Radius;
                if (c.Fresh && c.Radius <= 10f && t >= 1f && t < 2f) ejecta = true;
            }

            int height = (int)Math.Round(h);
            if (height < MinHeight) height = MinHeight;
            if (height > MaxHeight) height = MaxHeight;
            var col = new MoonColumn { Height = height };
            col.Surface = mare > 0.5f ? MoonSurface.Mare : ejecta ? MoonSurface.Ejecta : MoonSurface.Regolith;
            return col;
        }
    }
}
