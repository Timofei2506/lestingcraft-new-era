using System;
using System.Collections.Generic;
using LestingCraft.World;

namespace LestingCraft.Gameplay
{
    /// <summary>One palette entry of a voxel sculpt: a real PBR layer name plus tint and emission.</summary>
    public struct SculptPal
    {
        public string Name;
        public string Layer;
        public float R, G, B;
        public float Emission;
        public SculptPal(string name, string layer, float r, float g, float b, float emission)
        { Name = name; Layer = layer; R = r; G = g; B = b; Emission = emission; }
    }

    /// <summary>One articulated part: its pivot in model metres and its owned cells (x,y,z,palette).</summary>
    public struct SculptPartData
    {
        public string Name;
        public float PX, PY, PZ;
        public int[] Cells;
    }

    /// <summary>Complete deterministic sculpt: palette, articulated parts and one optional local light.</summary>
    public sealed class SculptData
    {
        public string Name;
        public float Unit;
        public SculptPal[] Palette;
        public SculptPartData[] Parts;
        public float LightX, LightY, LightZ, LightR, LightG, LightB, LightRange, LightIntensity;
        public int CellCount;
        public float MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
        public float Height { get { return MaxY - MinY; } }
        public float Width { get { return MaxX - MinX; } }
        public float Depth { get { return MaxZ - MinZ; } }
    }

    /// <summary>
    /// Authoring-time voxel sculpt builder. Pure C# (no Unity types) so the same geometry is
    /// compiled by the game, the mono headless checks and the offline JSON exporter.
    /// Cells are bevelled cubes on a uniform grid; silhouettes stay voxel geometry.
    /// </summary>
    public sealed class Sculptor
    {
        readonly float unit;
        readonly List<SculptPal> palette = new List<SculptPal>();
        readonly List<string> partOrder = new List<string>();
        readonly Dictionary<string, float[]> pivots = new Dictionary<string, float[]>();
        readonly Dictionary<string, Dictionary<long, int>> maps = new Dictionary<string, Dictionary<long, int>>();
        string current = "body";
        readonly Random rng;

        public Sculptor(float unit, int seed)
        {
            this.unit = unit;
            rng = new Random(seed & 0x7fffffff);
            Part("body", 0, 0, 0);
        }

        public float Unit { get { return unit; } }
        public int Seed { get { return rng.Next(); } }

        public int Pal(string name, string layer, float r, float g, float b, float emission)
        {
            for (int i = 0; i < palette.Count; i++)
                if (palette[i].Name == name) return i;
            palette.Add(new SculptPal(name, layer, r, g, b, emission));
            return palette.Count - 1;
        }

        public void Part(string name, float px, float py, float pz)
        {
            current = name;
            if (!maps.ContainsKey(name))
            {
                maps[name] = new Dictionary<long, int>();
                partOrder.Add(name);
                pivots[name] = new[] { px, py, pz };
            }
        }

        static long Key(int x, int y, int z)
        {
            return ((long)(x + 1024) << 40) | ((long)(y + 1024) << 20) | (long)(z + 1024);
        }

        void Put(int x, int y, int z, int mat)
        {
            maps[current][Key(x, y, z)] = mat;
        }

        void Set(float fx, float fy, float fz, int mat)
        {
            Put((int)Math.Floor(fx / unit), (int)Math.Floor(fy / unit), (int)Math.Floor(fz / unit), mat);
        }

        bool Has(float fx, float fy, float fz)
        {
            int x = (int)Math.Floor(fx / unit), y = (int)Math.Floor(fy / unit), z = (int)Math.Floor(fz / unit);
            return maps[current].ContainsKey(Key(x, y, z));
        }

        /// <summary>Bevelled ellipsoid brush; bevel &gt; 1 rounds the voxel cross-section.</summary>
        public void Ellipsoid(float cx, float cy, float cz, float rx, float ry, float rz, int mat, float bevel = 1.16f)
        {
            if (rx <= 0 || ry <= 0 || rz <= 0) return;
            int x0 = (int)Math.Floor((cx - rx) / unit), x1 = (int)Math.Ceiling((cx + rx) / unit);
            int y0 = (int)Math.Floor((cy - ry) / unit), y1 = (int)Math.Ceiling((cy + ry) / unit);
            int z0 = (int)Math.Floor((cz - rz) / unit), z1 = (int)Math.Ceiling((cz + rz) / unit);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                    {
                        float dx = ((x + .5f) * unit - cx) / rx, dy = ((y + .5f) * unit - cy) / ry, dz = ((z + .5f) * unit - cz) / rz;
                        float m = Math.Max(Math.Max(Math.Abs(dx), Math.Abs(dy)), Math.Abs(dz));
                        if (m <= 1 && dx * dx + dy * dy + dz * dz <= bevel) Put(x, y, z, mat);
                    }
        }

        public void Box(float cx, float cy, float cz, float hx, float hy, float hz, int mat)
        {
            int x0 = (int)Math.Floor((cx - hx) / unit), x1 = (int)Math.Floor((cx + hx) / unit);
            int y0 = (int)Math.Floor((cy - hy) / unit), y1 = (int)Math.Floor((cy + hy) / unit);
            int z0 = (int)Math.Floor((cz - hz) / unit), z1 = (int)Math.Floor((cz + hz) / unit);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                        Put(x, y, z, mat);
        }

        /// <summary>Shell only: an ellipsoid minus a smaller inner one (hollow tanks, carapaces).</summary>
        public void Shell(float cx, float cy, float cz, float rx, float ry, float rz, float thick, int mat)
        {
            if (rx <= 0 || ry <= 0 || rz <= 0) return;
            int x0 = (int)Math.Floor((cx - rx) / unit), x1 = (int)Math.Ceiling((cx + rx) / unit);
            int y0 = (int)Math.Floor((cy - ry) / unit), y1 = (int)Math.Ceiling((cy + ry) / unit);
            int z0 = (int)Math.Floor((cz - rz) / unit), z1 = (int)Math.Ceiling((cz + rz) / unit);
            float ix = rx - thick, iy = ry - thick, iz = rz - thick;
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                    {
                        float dx = ((x + .5f) * unit - cx) / rx, dy = ((y + .5f) * unit - cy) / ry, dz = ((z + .5f) * unit - cz) / rz;
                        float m = Math.Max(Math.Max(Math.Abs(dx), Math.Abs(dy)), Math.Abs(dz));
                        if (m > 1 || dx * dx + dy * dy + dz * dz > 1.16f) continue;
                        if (ix > 0 && iy > 0 && iz > 0)
                        {
                            float ex = ((x + .5f) * unit - cx) / ix, ey = ((y + .5f) * unit - cy) / iy, ez = ((z + .5f) * unit - cz) / iz;
                            if (ex * ex + ey * ey + ez * ez < 1.0f) continue;
                        }
                        Put(x, y, z, mat);
                    }
        }

        /// <summary>Catmull-Rom limb sampled into bevelled cubes; radii interpolate per control point.</summary>
        public void Path(float[] pts, float[] radii, int mat)
        {
            int n = pts.Length / 3;
            if (n < 2) return;
            for (int i = 0; i < n - 1; i++)
            {
                float[] p0 = At(pts, i - 1, n), p1 = At(pts, i, n), p2 = At(pts, i + 1, n), p3 = At(pts, i + 2, n);
                float len = Dist(p1, p2);
                int steps = Math.Max(2, (int)Math.Ceiling(len / (unit * .38f))) + 1;
                for (int j = 0; j <= steps; j++)
                {
                    float t = j / (float)steps, t2 = t * t, t3 = t2 * t;
                    float cx = .5f * (2 * p1[0] + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3);
                    float cy = .5f * (2 * p1[1] + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3);
                    float cz = .5f * (2 * p1[2] + (-p0[2] + p2[2]) * t + (2 * p0[2] - 5 * p1[2] + 4 * p2[2] - p3[2]) * t2 + (-p0[2] + 3 * p1[2] - 3 * p2[2] + p3[2]) * t3);
                    float r = radii[i] * (1 - t) + radii[i + 1] * t;
                    Ellipsoid(cx, cy, cz, r, r, r, mat);
                }
            }
        }

        static float[] At(float[] pts, int i, int n)
        {
            int k = i < 0 ? 0 : i >= n ? n - 1 : i;
            return new[] { pts[k * 3], pts[k * 3 + 1], pts[k * 3 + 2] };
        }

        static float Dist(float[] a, float[] b)
        {
            float dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>Horizontal ring (halo, collar, tank band) around the Y axis.</summary>
        public void Ring(float cx, float cy, float cz, float radius, float tube, int mat)
        {
            int steps = Math.Max(16, (int)(radius / unit * 2.2f));
            for (int i = 0; i < steps; i++)
            {
                double a = i * Math.PI * 2 / steps;
                Ellipsoid(cx + (float)Math.Cos(a) * radius, cy, cz + (float)Math.Sin(a) * radius, tube, tube, tube, mat);
            }
        }

        /// <summary>Vertical ring in the XZ-tilted plane used by floating crown halos.</summary>
        public void RingVertical(float cx, float cy, float cz, float radius, float tube, int mat)
        {
            int steps = Math.Max(16, (int)(radius / unit * 2.2f));
            for (int i = 0; i < steps; i++)
            {
                double a = i * Math.PI * 2 / steps;
                Ellipsoid(cx + (float)Math.Cos(a) * radius, cy + (float)Math.Sin(a) * radius, cz, tube, tube, tube, mat);
            }
        }

        /// <summary>Random crack/fissure walks over an existing surface: deep dark veins.</summary>
        public void Cracks(int mat, int count, int length, float y0, float y1)
        {
            var keys = new List<long>(maps[current].Keys);
            if (keys.Count == 0) return;
            int placed = 0, attempts = 0;
            while (placed < count && attempts < count * 14 + 8)
            {
                attempts++;
                long start = keys[rng.Next(keys.Count)];
                int x = (int)((start >> 40) & 0xfffff) - 1024;
                int y = (int)((start >> 20) & 0xfffff) - 1024;
                int z = (int)(start & 0xfffff) - 1024;
                float fy = y * unit;
                if (fy < y0 || fy > y1) continue;
                placed++;
                int dx = rng.Next(3) - 1, dz = rng.Next(3) - 1;
                if (dx == 0 && dz == 0) dx = 1;
                for (int s = 0; s < length; s++)
                {
                    if (rng.Next(10) < 3) dx = -dx;
                    if (rng.Next(10) < 3) dz = -dz;
                    int dy = rng.Next(3) - 1;
                    x += dx; y += dy; z += dz;
                    long k = Key(x, y, z);
                    if (maps[current].ContainsKey(k)) maps[current][k] = mat;
                    else
                    {
                        // carve one cell inward so the vein reads as a fissure, not paint
                        long inner = Key(x - dx, y, z - dz);
                        if (maps[current].ContainsKey(inner)) maps[current][inner] = mat;
                    }
                }
            }
        }

        /// <summary>Two symmetric eyes with a deep socket, cornea ring, iris, dark pupil and a glint.</summary>
        public void Eyes(float hx, float hy, float hz, float sep, float r, int sclera, int iris, int pupil)
        {
            int glint = Pal("eye glint", "glow-shroom", 1f, .97f, .88f, 2.6f);
            for (int side = -1; side <= 1; side += 2)
            {
                // Deep socket behind the eye: reads as set into the head instead of painted on.
                Ellipsoid(side * sep, hy, hz - r * .38f, r * 1.12f, r * .82f, r * .52f, pupil);
                Ellipsoid(side * sep + hx * 0, hy, hz, r, r, r * .7f, sclera);
                Ellipsoid(side * sep, hy, hz + r * .55f, r * .62f, r * .62f, r * .5f, iris);
                Ellipsoid(side * sep, hy, hz + r * .95f, r * .28f, r * .28f, r * .3f, pupil);
                // Specular glint facet above the iris.
                Ellipsoid(side * sep + r * .30f, hy + r * .32f, hz + r * .70f, r * .15f, r * .15f, r * .11f, glint);
            }
        }

        public int Rng(int max) { return rng.Next(max); }

        public SculptData Finish(string name)
        {
            var parts = new SculptPartData[partOrder.Count];
            int total = 0;
            float minX = 1e9f, minY = 1e9f, minZ = 1e9f, maxX = -1e9f, maxY = -1e9f, maxZ = -1e9f;
            for (int i = 0; i < partOrder.Count; i++)
            {
                string pn = partOrder[i];
                var map = maps[pn];
                var cells = new int[map.Count * 4];
                int k = 0;
                foreach (var pair in map)
                {
                    int x = (int)((pair.Key >> 40) & 0xfffff) - 1024;
                    int y = (int)((pair.Key >> 20) & 0xfffff) - 1024;
                    int z = (int)(pair.Key & 0xfffff) - 1024;
                    cells[k++] = x; cells[k++] = y; cells[k++] = z; cells[k++] = pair.Value;
                    float wx = x * unit, wy = y * unit, wz = z * unit;
                    if (wx < minX) minX = wx; if (wx > maxX) maxX = wx + unit;
                    if (wy < minY) minY = wy; if (wy > maxY) maxY = wy + unit;
                    if (wz < minZ) minZ = wz; if (wz > maxZ) maxZ = wz + unit;
                }
                total += map.Count;
                var piv = pivots[pn];
                parts[i] = new SculptPartData { Name = pn, PX = piv[0], PY = piv[1], PZ = piv[2], Cells = cells };
            }
            var data = new SculptData
            {
                Name = name,
                Unit = unit,
                Palette = palette.ToArray(),
                Parts = parts,
                CellCount = total,
                MinX = minX, MinY = minY, MinZ = minZ, MaxX = maxX, MaxY = maxY, MaxZ = maxZ
            };
            return data;
        }

        public void Light(float x, float y, float z, float r, float g, float b, float range, float intensity)
        {
            // stored on the next Finish via fields below
            lightX = x; lightY = y; lightZ = z; lightR = r; lightG = g; lightB = b; lightRange = range; lightIntensity = intensity;
            hasLight = true;
        }

        bool hasLight;
        float lightX, lightY, lightZ, lightR, lightG, lightB, lightRange, lightIntensity;

        internal bool HasLight { get { return hasLight; } }
        internal float[] LightSpec { get { return new[] { lightX, lightY, lightZ, lightR, lightG, lightB, lightRange, lightIntensity }; } }
    }

    /// <summary>
    /// Authored guardians: clean, readable voxel sculpts — blocky anatomy with clear silhouettes,
    /// articulated limbs, blocky heads and emissive weak points. One sculpt per world; combat rules
    /// and part names the controllers rely on (arm_l/arm_r, arm2_l/arm2_r, head, shard_*, leg_*) are kept.
    /// </summary>
    public static class BossSculpts
    {
        public static SculptData For(Dimension dimension, int seed)
        {
            switch (dimension)
            {
                case Dimension.Lestingia: return Warden(seed, false);
                case Dimension.Mars: return MarsColossus(seed);
                case Dimension.Venus: return VenusPredator(seed);
                case Dimension.Mercury: return MercuryScarab(seed);
                case Dimension.Moon: return MoonTitan(seed);
                case Dimension.Europa: return EuropaShell(seed);
                case Dimension.Titan: return RingedLeviathan(seed);
                case Dimension.Pluto: return PlutoHermit(seed);
                case Dimension.ProximaB: return ProximaDevourer(seed);
                case Dimension.Trappist1e: return TrappistChoir(seed);
                case Dimension.Kepler452b: return KeplerGardener(seed);
                case Dimension.AndromedaPrime: return AndromedaLord(seed);
                case Dimension.Somnia: return HeWhoHeard(seed);
                case Dimension.Dream: return FirstNode(seed);
                case Dimension.Radia: return RadiaColossus(seed);
                case Dimension.Backrooms: return BlackBacteria(seed);
                default: return Warden(seed, false);
            }
        }

        /// <summary>Earth's extra canon pair and any future add-on guardian.</summary>
        public static SculptData Extra(string name, int seed)
        {
            if (name == "The Corrupted AI") return CorruptedAI(seed);
            if (name == "The Void Horror") return VoidHorror(seed);
            return Warden(seed, true);
        }

        // ---------- shared blocky anatomy helpers ----------

        /// <summary>Blocky arm: upper box, lower box and a rounded elbow joint. Hangs below the shoulder pivot.</summary>
        static void Arm(Sculptor s, float sx, float sy, float sz, float len, float t, int up, int lo, int joint)
        {
            s.Box(sx, sy - len * .5f, sz, t * .5f, len * .5f, t * .5f, up);
            s.Box(sx, sy - len * 1.42f, sz, t * .62f, len * .52f, t * .62f, lo);
            s.Ellipsoid(sx, sy - len * .94f, sz, t * .5f, t * .5f, t * .5f, joint, 1.05f);
        }

        /// <summary>Blocky leg: thigh, shin, blocky foot and a rounded knee. Total height = len so the
        /// foot lands at y = sy - len. Creates its own leg_l/leg_r part so the controller sees a walker.</summary>
        static void Leg(Sculptor s, float sx, float sy, float sz, float len, float t, int up, int lo, int foot, int joint)
        {
            s.Part(sx < 0 ? "leg_l" : "leg_r", sx, sy, sz);
            s.Box(sx, sy - len * .05f, sz, t * .6f, len * .12f, t * .6f, up);
            s.Box(sx, sy - len * .30f, sz, t * .5f, len * .30f, t * .5f, up);
            s.Box(sx, sy - len * .80f, sz, t * .55f, len * .25f, t * .55f, lo);
            s.Box(sx, sy - len * .98f, sz + t * .12f, t * .8f, t * .22f, t * 1.0f, foot);
            s.Ellipsoid(sx, sy - len * .58f, sz, t * .5f, t * .5f, t * .5f, joint, 1.05f);
        }

        /// <summary>Blocky digitigrade leg: thigh, shin angled back, clawed foot. Total height = len.</summary>
        static void DigitLeg(Sculptor s, float sx, float sy, float sz, float len, float t, int up, int lo, int foot, int joint)
        {
            s.Part(sx < 0 ? "leg_l" : "leg_r", sx, sy, sz);
            s.Box(sx, sy - len * .05f, sz, t * .6f, len * .12f, t * .6f, up);
            s.Box(sx, sy - len * .30f, sz, t * .5f, len * .30f, t * .5f, up);
            s.Box(sx, sy - len * .80f, sz - t * .08f, t * .55f, len * .25f, t * .55f, lo);
            s.Box(sx, sy - len * .98f, sz + t * .35f, t * .8f, t * .22f, t * .9f, foot);
            s.Ellipsoid(sx, sy - len * .58f, sz - t * .05f, t * .5f, t * .5f, t * .5f, joint, 1.05f);
        }

        /// <summary>Blocky torso: a stacked chest + waist with a clear silhouette.</summary>
        static void Torso(Sculptor s, float cx, float cy, float cz, float w, float h, float d, int chest, int waist)
        {
            s.Box(cx, cy + h * .28f, cz, w * .5f, h * .36f, d * .5f, chest);
            s.Box(cx, cy - h * .22f, cz, w * .58f, h * .26f, d * .58f, waist);
        }

        /// <summary>Blocky head: a face block with a jaw and a brow, reads clearly as a head.</summary>
        static void Head(Sculptor s, float cx, float cy, float cz, float w, float h, float d, int face, int jaw, int brow)
        {
            s.Box(cx, cy, cz, w * .5f, h * .5f, d * .5f, face);
            s.Box(cx, cy - h * .34f, cz + d * .12f, w * .5f, h * .22f, d * .24f, jaw);
            s.Box(cx, cy + h * .30f, cz, w * .56f, h * .16f, d * .56f, brow);
        }

        /// <summary>Copy the sculpt's local light spec onto the finished SculptData.</summary>
        static void ApplyLight(Sculptor s, SculptData d)
        {
            if (!s.HasLight) return;
            var l = s.LightSpec;
            d.LightX = l[0]; d.LightY = l[1]; d.LightZ = l[2];
            d.LightR = l[3]; d.LightG = l[4]; d.LightB = l[5];
            d.LightRange = l[6]; d.LightIntensity = l[7];
        }

        // ---------- Lestingia / Kepler: forest wardens ----------

        public static SculptData Warden(int seed, bool variant)
        {
            var s = new Sculptor(.13f, seed);
            int bark = s.Pal("old outer bark", "bark", .62f, .52f, .38f, 0);
            int grain = s.Pal("inner root grain", "bark", .85f, .72f, .52f, 0);
            int carved = s.Pal("petrified carved wood", "mossy-stone", .86f, .78f, .58f, 0);
            int moss = s.Pal("moss in crevices", "mossy-stone", .58f, .78f, .42f, 0);
            int heart = s.Pal("amber living heart", "glow-shroom", .95f, .30f, .04f, 1.2f);
            int heartHot = s.Pal("heart hot facet", "glow-shroom", 1f, .67f, .18f, 1.9f);
            int eye = s.Pal("ember eye", "glow-shroom", 1f, .64f, .15f, 2f);
            int pupil = s.Pal("hollow socket", "obsidian", .10f, .07f, .05f, 0);
            int charred = s.Pal("charred fissures", "bark", .32f, .28f, .22f, 0);

            // blocky rooted legs
            Leg(s, -.42f, 1.7f, 0f, 1.7f, .62f, bark, grain, grain, grain);
            Leg(s, .42f, 1.7f, 0f, 1.7f, .62f, bark, grain, grain, grain);
            s.Part("body", 0, 0, 0);
            Torso(s, 0, 2.7f, 0f, 1.5f, 1.5f, 1.1f, bark, grain);
            // mossy stone shoulder pauldrons
            s.Box(-.92f, 3.1f, 0f, .42f, .34f, .5f, moss);
            s.Box(.92f, 3.1f, 0f, .42f, .34f, .5f, moss);
            // arms
            s.Part("arm_l", -.78f, 3.35f, 0f);
            Arm(s, -.78f, 3.35f, 0f, 1.5f, .46f, bark, grain, grain);
            s.Part("arm_r", .78f, 3.35f, 0f);
            Arm(s, .78f, 3.35f, 0f, 1.5f, .46f, bark, grain, grain);
            // amber heart behind a blocky ribcage
            s.Box(0, 2.85f, .42f, .2f, .26f, .18f, heart);
            s.Ellipsoid(0, 2.85f, .56f, .12f, .14f, .1f, heartHot);
            // carved mask head
            s.Part("head", 0, 4.05f, 0f);
            Head(s, 0, 4.05f, .04f, .9f, .72f, .7f, carved, carved, carved);
            s.Eyes(0, 4.05f, .40f, .20f, .12f, pupil, eye, pupil);
            // branching antler crown
            for (int side = -1; side <= 1; side += 2)
            {
                s.Box(side * .30f, 4.55f, 0f, .07f, .55f, .07f, bark);
                s.Box(side * .30f, 4.78f, .12f, .07f, .42f, .07f, bark);
                s.Box(side * .42f, 4.92f, .20f, .07f, .30f, .07f, bark);
            }
            s.Part("body", 0, 0, 0);
            s.Cracks(charred, 22, 6, .4f, 4.2f);
            s.Cracks(moss, 16, 5, .2f, 3.2f);
            s.Light(0, 2.85f, .7f, 1f, .38f, .055f, 3.5f, 2.7f);
            var d = s.Finish("Warden");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Mars: rusted red colossus ----------

        static SculptData MarsColossus(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int body = s.Pal("rusted hull", "metal-ingot", .72f, .42f, .30f, 0);
            int dark = s.Pal("baked plating", "metal-ingot", .52f, .26f, .16f, 0);
            int joint = s.Pal("arc joint", "generator-casing", .30f, .30f, .32f, 0);
            int eye = s.Pal("core visor", "lava", 1f, .45f, .12f, 2f);
            int glow = s.Pal("heat vent", "lava", 1f, .55f, .20f, 1.6f);
            int metal = s.Pal("polished steel", "hazard-steel", .62f, .60f, .60f, 0);

            Leg(s, -.5f, 1.9f, 0f, 1.9f, .68f, body, dark, dark, joint);
            Leg(s, .5f, 1.9f, 0f, 1.9f, .68f, body, dark, dark, joint);
            s.Part("body", 0, 0, 0);
            Torso(s, 0, 3.0f, 0f, 1.7f, 1.6f, 1.2f, body, dark);
            s.Box(0, 3.6f, .45f, .5f, .4f, .3f, glow);
            // blocky pauldrons
            s.Box(-1.0f, 3.5f, 0f, .5f, .45f, .55f, dark);
            s.Box(1.0f, 3.5f, 0f, .5f, .45f, .55f, dark);
            s.Part("arm_l", -.88f, 3.7f, 0f);
            Arm(s, -.88f, 3.7f, 0f, 1.7f, .5f, body, dark, joint);
            s.Part("arm_r", .88f, 3.7f, 0f);
            Arm(s, .88f, 3.7f, 0f, 1.7f, .5f, body, dark, joint);
            // second pair of arms
            s.Part("arm2_l", -.95f, 3.2f, -.1f);
            Arm(s, -.95f, 3.2f, -.1f, 1.4f, .42f, dark, metal, joint);
            s.Part("arm2_r", .95f, 3.2f, -.1f);
            Arm(s, .95f, 3.2f, -.1f, 1.4f, .42f, dark, metal, joint);
            s.Part("head", 0, 4.4f, .05f);
            Head(s, 0, 4.4f, .05f, .85f, .6f, .6f, body, dark, dark);
            s.Box(0, 4.45f, .38f, .34f, .12f, .08f, eye);
            s.Part("body", 0, 0, 0);
            s.Cracks(dark, 20, 6, .5f, 4.0f);
            s.Light(0, 3.6f, .75f, 1f, .55f, .20f, 3.5f, 2.2f);
            var d = s.Finish("Mars Colossus");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Venus: golden predator ----------

        static SculptData VenusPredator(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int gold = s.Pal("golden hide", "copper-block", .85f, .66f, .32f, 0);
            int dark = s.Pal("shadowed underbelly", "metal-ingot", .55f, .36f, .20f, 0);
            int joint = s.Pal("sinew joint", "generator-casing", .45f, .34f, .20f, 0);
            int eye = s.Pal("predator eye", "lava", 1f, .60f, .10f, 2.2f);
            int claw = s.Pal("ivory claw", "bone-stalker-hide", .88f, .80f, .66f, 0);

            Leg(s, -.45f, 1.8f, .05f, 1.8f, .62f, gold, dark, dark, joint);
            Leg(s, .45f, 1.8f, .05f, 1.8f, .62f, gold, dark, dark, joint);
            s.Part("body", 0, 0, 0);
            Torso(s, 0, 2.9f, 0f, 1.6f, 1.5f, 1.05f, gold, dark);
            // arched back plates
            for (int i = -1; i <= 1; i++) s.Box(i * .45f, 3.5f, -.15f, .35f, .2f, .35f, gold);
            s.Part("arm_l", -.82f, 3.3f, 0f);
            Arm(s, -.82f, 3.3f, 0f, 1.6f, .46f, gold, dark, joint);
            s.Box(-.82f, 1.85f, .28f, .12f, .12f, .12f, claw);
            s.Part("arm_r", .82f, 3.3f, 0f);
            Arm(s, .82f, 3.3f, 0f, 1.6f, .46f, gold, dark, joint);
            s.Box(.82f, 1.85f, .28f, .12f, .12f, .12f, claw);
            s.Part("head", 0, 4.2f, .12f);
            Head(s, 0, 4.2f, .12f, .8f, .66f, .6f, gold, dark, gold);
            // jaw fangs
            s.Box(-.14f, 3.94f, .42f, .07f, .14f, .07f, claw);
            s.Box(.14f, 3.94f, .42f, .07f, .14f, .07f, claw);
            s.Eyes(0, 4.32f, .44f, .20f, .11f, dark, eye, dark);
            s.Part("body", 0, 0, 0);
            s.Cracks(dark, 18, 6, .5f, 3.9f);
            s.Light(0, 3.4f, .55f, 1f, .60f, .10f, 3.0f, 2.0f);
            var d = s.Finish("Venus Predator");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Mercury: mercury scarab ----------

        static SculptData MercuryScarab(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int silver = s.Pal("mercury shell", "hazard-steel", .78f, .80f, .86f, 0);
            int dark = s.Pal("shadowed seam", "obsidian", .22f, .24f, .28f, 0);
            int joint = s.Pal("joint ligament", "generator-casing", .50f, .52f, .58f, 0);
            int core = s.Pal("mercury core", "cherenkov-crystal", .60f, .90f, 1f, 2f);
            int mandible = s.Pal("glass mandible", "crystal", .70f, .85f, 1f, 0);

            DigitLeg(s, -.5f, 1.7f, .1f, 1.7f, .5f, silver, dark, dark, joint);
            DigitLeg(s, .5f, 1.7f, .1f, 1.7f, .5f, silver, dark, dark, joint);
            s.Part("body", 0, 0, 0);
            // beetle body: domed chest + smaller rear
            s.Box(0, 2.6f, 0f, .7f, .6f, 1.0f, silver);
            s.Box(0, 2.5f, -.75f, .5f, .5f, .6f, dark);
            s.Box(0, 3.1f, 0f, .72f, .18f, 1.0f, dark);
            s.Box(0, 2.5f, .75f, .3f, .35f, .2f, core);
            // blocky wings folded
            s.Box(-.85f, 2.7f, -.1f, .25f, .5f, .9f, silver);
            s.Box(.85f, 2.7f, -.1f, .25f, .5f, .9f, silver);
            s.Part("head", 0, 3.7f, .1f);
            Head(s, 0, 3.7f, .1f, .75f, .55f, .6f, silver, dark, dark);
            // mandibles
            s.Box(-.18f, 3.6f, .55f, .08f, .25f, .3f, mandible);
            s.Box(.18f, 3.6f, .55f, .08f, .25f, .3f, mandible);
            s.Eyes(0, 3.78f, .42f, .16f, .1f, dark, core, dark);
            s.Part("body", 0, 0, 0);
            s.Cracks(dark, 14, 6, .5f, 3.6f);
            s.Light(0, 2.5f, .95f, .60f, .90f, 1f, 3.0f, 2.0f);
            var d = s.Finish("Mercury Scarab");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Moon: grey titan ----------

        static SculptData MoonTitan(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int grey = s.Pal("lunar rock", "stone", .58f, .58f, .60f, 0);
            int dark = s.Pal("basalt plate", "obsidian", .32f, .32f, .36f, 0);
            int joint = s.Pal("regolith joint", "generator-casing", .45f, .45f, .48f, 0);
            int eye = s.Pal("crater glow", "glow-shroom", .70f, .85f, 1f, 2f);
            int crack = s.Pal("fissure glow", "lava", 1f, .6f, .2f, 1.6f);

            Leg(s, -.52f, 2.0f, 0f, 2.0f, .72f, grey, dark, dark, joint);
            Leg(s, .52f, 2.0f, 0f, 2.0f, .72f, grey, dark, dark, joint);
            s.Part("body", 0, 0, 0);
            Torso(s, 0, 3.2f, 0f, 1.8f, 1.7f, 1.3f, grey, dark);
            // cratered chest
            s.Ellipsoid(0, 3.4f, .5f, .35f, .3f, .3f, crack);
            s.Box(-1.0f, 3.7f, 0f, .55f, .5f, .6f, dark);
            s.Box(1.0f, 3.7f, 0f, .55f, .5f, .6f, dark);
            s.Part("arm_l", -.92f, 3.85f, 0f);
            Arm(s, -.92f, 3.85f, 0f, 1.8f, .52f, grey, dark, joint);
            s.Part("arm_r", .92f, 3.85f, 0f);
            Arm(s, .92f, 3.85f, 0f, 1.8f, .52f, grey, dark, joint);
            s.Part("head", 0, 4.65f, 0f);
            Head(s, 0, 4.65f, 0f, .9f, .7f, .7f, grey, dark, dark);
            // helmet visor
            s.Box(0, 4.72f, .34f, .42f, .12f, .08f, eye);
            s.Part("body", 0, 0, 0);
            s.Cracks(dark, 22, 7, .5f, 4.4f);
            s.Light(0, 3.4f, .8f, .70f, .85f, 1f, 3.5f, 2.0f);
            var d = s.Finish("Moon Titan");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Europa: ice crustacean ----------

        static SculptData EuropaShell(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int ice = s.Pal("europa ice", "glacial-ice", .78f, .88f, .95f, 0);
            int deep = s.Pal("deep freeze", "glacial-ice", .55f, .72f, .85f, 0);
            int joint = s.Pal("frozen joint", "generator-casing", .60f, .75f, .85f, 0);
            int glow = s.Pal("glow vent", "cherenkov-crystal", .60f, .90f, 1f, 2f);
            int dark = s.Pal("ice shadow", "obsidian", .28f, .38f, .48f, 0);

            DigitLeg(s, -.48f, 1.6f, .08f, 1.6f, .55f, ice, deep, deep, joint);
            DigitLeg(s, .48f, 1.6f, .08f, 1.6f, .55f, ice, deep, deep, joint);
            s.Part("body", 0, 0, 0);
            // domed shell
            s.Box(0, 2.5f, 0f, .8f, .8f, 1.1f, ice);
            s.Box(0, 3.0f, 0f, .72f, .35f, 1.0f, deep);
            s.Box(0, 2.5f, .75f, .3f, .4f, .2f, glow);
            // blocky side plates
            s.Box(-.95f, 2.5f, 0f, .28f, .6f, 1.0f, ice);
            s.Box(.95f, 2.5f, 0f, .28f, .6f, 1.0f, ice);
            s.Part("head", 0, 3.6f, .1f);
            Head(s, 0, 3.6f, .1f, .8f, .6f, .7f, ice, deep, ice);
            s.Eyes(0, 3.72f, .44f, .2f, .1f, dark, glow, dark);
            s.Part("body", 0, 0, 0);
            s.Cracks(deep, 16, 6, .5f, 3.4f);
            s.Light(0, 2.5f, .95f, .60f, .90f, 1f, 3.0f, 2.0f);
            var d = s.Finish("Europa Shell");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Titan: ringed leviathan (floating robed entity) ----------

        static SculptData RingedLeviathan(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int robe = s.Pal("void robe", "mossy-stone", .30f, .26f, .36f, 0);
            int dark = s.Pal("dark hood", "obsidian", .16f, .13f, .20f, 0);
            int trim = s.Pal("ring trim", "cherenkov-crystal", .55f, .85f, 1f, 2f);
            int glow = s.Pal("leviathan core", "cherenkov-crystal", .60f, .90f, 1f, 2.2f);
            int face = s.Pal("empty face", "bone-stalker-hide", .55f, .55f, .60f, 0);

            s.Part("body", 0, 0, 0);
            // tapered robe (floating, no legs)
            s.Box(0, 2.2f, 0f, .7f, .9f, .7f, robe);
            s.Box(0, 1.2f, 0f, .85f, .8f, .85f, robe);
            s.Box(0, .2f, 0f, 1.0f, .7f, 1.0f, robe);
            // glowing collar ring at the neck
            s.Ring(0, 2.85f, 0f, .6f, .12f, trim);
            s.Box(0, 3.0f, 0f, .5f, .4f, .5f, dark);
            // arms from the robe
            s.Part("arm_l", -.7f, 2.6f, 0f);
            Arm(s, -.7f, 2.6f, 0f, 1.7f, .4f, robe, dark, dark);
            s.Part("arm_r", .7f, 2.6f, 0f);
            Arm(s, .7f, 2.6f, 0f, 1.7f, .4f, robe, dark, dark);
            // second pair
            s.Part("arm2_l", -.6f, 2.2f, .05f);
            Arm(s, -.6f, 2.2f, .05f, 1.5f, .38f, robe, dark, dark);
            s.Part("arm2_r", .6f, 2.2f, .05f);
            Arm(s, .6f, 2.2f, .05f, 1.5f, .38f, robe, dark, dark);
            s.Part("head", 0, 3.5f, 0f);
            Head(s, 0, 3.5f, 0f, .85f, .7f, .6f, dark, dark, dark);
            s.Box(0, 3.55f, .32f, .34f, .1f, .06f, glow);
            // orbiting shards
            for (int i = 0; i < 4; i++)
            {
                s.Part("shard_" + i, 0, 3.2f + i * .3f, 0);
                double a = i * 2.1;
                s.Box((float)Math.Cos(a) * 1.5f, 3.2f + i * .3f, (float)Math.Sin(a) * 1.5f, .14f, .14f, .14f, trim);
            }
            s.Part("body", 0, 0, 0);
            s.Cracks(dark, 16, 6, .4f, 3.2f);
            s.Light(0, 2.85f, 0f, .60f, .90f, 1f, 4.0f, 2.2f);
            var d = s.Finish("Ringed Leviathan");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Pluto: rocky hermit crab ----------

        static SculptData PlutoHermit(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int rock = s.Pal("plutonian rock", "stone", .48f, .42f, .46f, 0);
            int shell = s.Pal("fossil shell", "mossy-stone", .62f, .55f, .48f, 0);
            int joint = s.Pal("rocky joint", "generator-casing", .40f, .36f, .42f, 0);
            int glow = s.Pal("radioactive glow", "lava", 1f, .55f, .20f, 1.8f);
            int dark = s.Pal("cave shadow", "obsidian", .20f, .18f, .22f, 0);

            DigitLeg(s, -.5f, 1.5f, .05f, 1.5f, .5f, rock, dark, dark, joint);
            DigitLeg(s, .5f, 1.5f, .05f, 1.5f, .5f, rock, dark, dark, joint);
            s.Part("body", 0, 0, 0);
            // hermit body + shell
            s.Box(0, 2.1f, 0f, .75f, .7f, .9f, rock);
            s.Box(0, 2.7f, 0f, .8f, .4f, .85f, shell);
            s.Box(0, 2.7f, .5f, .3f, .3f, .2f, glow);
            // claws
            s.Part("arm_l", -.9f, 2.2f, .1f);
            Arm(s, -.9f, 2.2f, .1f, 1.2f, .4f, rock, dark, joint);
            s.Box(-.9f, 1.1f, .35f, .16f, .14f, .16f, shell);
            s.Part("arm_r", .9f, 2.2f, .1f);
            Arm(s, .9f, 2.2f, .1f, 1.2f, .4f, rock, dark, joint);
            s.Box(.9f, 1.1f, .35f, .16f, .14f, .16f, shell);
            s.Part("head", 0, 3.2f, .1f);
            Head(s, 0, 3.2f, .1f, .75f, .55f, .6f, rock, dark, rock);
            s.Eyes(0, 3.32f, .42f, .18f, .1f, dark, glow, dark);
            s.Part("body", 0, 0, 0);
            s.Cracks(dark, 18, 6, .4f, 3.0f);
            s.Light(0, 2.7f, .7f, 1f, .55f, .20f, 3.0f, 2.0f);
            var d = s.Finish("Pluto Hermit");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Proxima B: dark devourer ----------

        static SculptData ProximaDevourer(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int hide = s.Pal("devourer hide", "wraith-shroud", .14f, .12f, .16f, 0);
            int dark = s.Pal("shadowed flesh", "obsidian", .08f, .07f, .10f, 0);
            int joint = s.Pal("obsidian joint", "obsidian", .28f, .24f, .30f, 0);
            int eye = s.Pal("devourer eye", "lava", 1f, .40f, .10f, 2.2f);
            int maw = s.Pal("glowing maw", "lava", 1f, .30f, .05f, 1.8f);

            Leg(s, -.48f, 1.8f, 0f, 1.8f, .6f, hide, dark, dark, joint);
            Leg(s, .48f, 1.8f, 0f, 1.8f, .6f, hide, dark, dark, joint);
            s.Part("body", 0, 0, 0);
            Torso(s, 0, 3.0f, 0f, 1.7f, 1.6f, 1.1f, hide, dark);
            // hunched back
            s.Box(0, 3.6f, -.2f, .8f, .4f, .6f, hide);
            s.Part("arm_l", -.85f, 3.4f, 0f);
            Arm(s, -.85f, 3.4f, 0f, 1.8f, .5f, hide, dark, joint);
            s.Part("arm_r", .85f, 3.4f, 0f);
            Arm(s, .85f, 3.4f, 0f, 1.8f, .5f, hide, dark, joint);
            s.Part("arm2_l", -.9f, 3.0f, -.05f);
            Arm(s, -.9f, 3.0f, -.05f, 1.5f, .44f, dark, hide, joint);
            s.Part("arm2_r", .9f, 3.0f, -.05f);
            Arm(s, .9f, 3.0f, -.05f, 1.5f, .44f, dark, hide, joint);
            s.Part("head", 0, 4.4f, .08f);
            Head(s, 0, 4.4f, .08f, .85f, .6f, .65f, hide, dark, dark);
            s.Box(0, 4.3f, .4f, .4f, .2f, .2f, maw);
            s.Eyes(0, 4.5f, .42f, .18f, .1f, dark, eye, dark);
            s.Part("body", 0, 0, 0);
            s.Cracks(dark, 20, 7, .5f, 4.2f);
            s.Light(0, 4.3f, .55f, 1f, .30f, .05f, 3.2f, 2.2f);
            var d = s.Finish("Proxima Devourer");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Trappist-1e: crystal choir ----------

        static SculptData TrappistChoir(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int crystal = s.Pal("crystal flesh", "crystal", .50f, .80f, .90f, 0);
            int dark = s.Pal("resonant shadow", "obsidian", .18f, .22f, .30f, 0);
            int joint = s.Pal("resonant joint", "cherenkov-crystal", .55f, .85f, 1f, 0);
            int glow = s.Pal("choir glow", "cherenkov-crystal", .60f, .95f, 1f, 2.2f);
            int eye = s.Pal("choir eye", "glow-shroom", 1f, .90f, .80f, 2f);

            Leg(s, -.45f, 1.8f, 0f, 1.8f, .58f, crystal, dark, dark, joint);
            Leg(s, .45f, 1.8f, 0f, 1.8f, .58f, crystal, dark, dark, joint);
            s.Part("body", 0, 0, 0);
            Torso(s, 0, 3.0f, 0f, 1.6f, 1.6f, 1.1f, crystal, dark);
            // crystal growths on the chest
            s.Box(0, 3.5f, .3f, .3f, .4f, .2f, glow);
            s.Box(-.5f, 3.6f, .1f, .25f, .4f, .25f, crystal);
            s.Box(.5f, 3.6f, .1f, .25f, .4f, .25f, crystal);
            s.Part("arm_l", -.82f, 3.3f, 0f);
            Arm(s, -.82f, 3.3f, 0f, 1.7f, .46f, crystal, dark, joint);
            s.Part("arm_r", .82f, 3.3f, 0f);
            Arm(s, .82f, 3.3f, 0f, 1.7f, .46f, crystal, dark, joint);
            s.Part("head", 0, 4.4f, 0f);
            Head(s, 0, 4.4f, 0f, .85f, .66f, .6f, crystal, dark, crystal);
            s.Eyes(0, 4.52f, .38f, .18f, .1f, dark, eye, dark);
            s.Part("body", 0, 0, 0);
            s.Cracks(dark, 16, 6, .5f, 4.0f);
            s.Light(0, 3.5f, .5f, .60f, .95f, 1f, 3.5f, 2.0f);
            var d = s.Finish("Trappist Choir");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Kepler-452b: gardener ----------

        static SculptData KeplerGardener(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int leaf = s.Pal("gardener flesh", "leaves", .40f, .62f, .30f, 0);
            int bark = s.Pal("wooden stem", "bark", .62f, .48f, .30f, 0);
            int joint = s.Pal("root joint", "generator-casing", .40f, .46f, .30f, 0);
            int flower = s.Pal("bloom glow", "glow-shroom", 1f, .70f, .30f, 2f);
            int eye = s.Pal("gardener eye", "glow-shroom", .80f, 1f, .40f, 2f);

            Leg(s, -.46f, 1.7f, 0f, 1.7f, .6f, bark, leaf, leaf, joint);
            Leg(s, .46f, 1.7f, 0f, 1.7f, .6f, bark, leaf, leaf, joint);
            s.Part("body", 0, 0, 0);
            Torso(s, 0, 2.8f, 0f, 1.6f, 1.5f, 1.1f, leaf, bark);
            // leafy canopy
            s.Box(0, 3.6f, 0f, .9f, .4f, .9f, leaf);
            s.Box(-.4f, 3.9f, 0f, .5f, .35f, .5f, leaf);
            s.Box(.4f, 3.9f, 0f, .5f, .35f, .5f, leaf);
            s.Box(0, 4.1f, 0f, .3f, .3f, .3f, flower);
            s.Part("arm_l", -.8f, 3.2f, 0f);
            Arm(s, -.8f, 3.2f, 0f, 1.6f, .44f, bark, leaf, joint);
            s.Part("arm_r", .8f, 3.2f, 0f);
            Arm(s, .8f, 3.2f, 0f, 1.6f, .44f, bark, leaf, joint);
            s.Part("head", 0, 4.3f, 0f);
            Head(s, 0, 4.3f, 0f, .8f, .6f, .6f, leaf, bark, leaf);
            s.Eyes(0, 4.42f, .38f, .17f, .1f, bark, eye, bark);
            s.Part("body", 0, 0, 0);
            s.Cracks(bark, 14, 5, .4f, 3.8f);
            s.Light(0, 4.1f, 0f, 1f, .70f, .30f, 3.2f, 2.0f);
            var d = s.Finish("Kepler Gardener");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Andromeda Prime: cosmic lord (floating) ----------

        static SculptData AndromedaLord(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int vd = s.Pal("void robe", "obsidian", .12f, .10f, .18f, 0);
            int star = s.Pal("starlight trim", "cherenkov-crystal", .70f, .85f, 1f, 2.2f);
            int crown = s.Pal("cosmic crown", "crystal", .55f, .70f, .95f, 0);
            int eye = s.Pal("cosmic eye", "cherenkov-crystal", .60f, .95f, 1f, 2.4f);
            int dark = s.Pal("void shadow", "obsidian", .06f, .05f, .10f, 0);

            s.Part("body", 0, 0, 0);
            // floating robed body
            s.Box(0, 2.2f, 0f, .75f, .9f, .75f, vd);
            s.Box(0, 1.2f, 0f, .9f, .85f, .9f, vd);
            s.Box(0, .2f, 0f, 1.05f, .7f, 1.05f, vd);
            s.Ring(0, 2.9f, 0f, .62f, .1f, star);
            s.Box(0, 3.1f, 0f, .5f, .4f, .5f, dark);
            s.Part("arm_l", -.75f, 2.6f, 0f);
            Arm(s, -.75f, 2.6f, 0f, 1.7f, .42f, vd, dark, dark);
            s.Part("arm_r", .75f, 2.6f, 0f);
            Arm(s, .75f, 2.6f, 0f, 1.7f, .42f, vd, dark, dark);
            s.Part("arm2_l", -.65f, 2.2f, .05f);
            Arm(s, -.65f, 2.2f, .05f, 1.5f, .38f, vd, dark, dark);
            s.Part("arm2_r", .65f, 2.2f, .05f);
            Arm(s, .65f, 2.2f, .05f, 1.5f, .38f, vd, dark, dark);
            s.Part("head", 0, 3.6f, 0f);
            Head(s, 0, 3.6f, 0f, .9f, .72f, .6f, dark, dark, crown);
            s.Box(0, 3.68f, .34f, .38f, .1f, .06f, eye);
            for (int i = 0; i < 4; i++)
            {
                s.Part("shard_" + i, 0, 3.3f + i * .28f, 0);
                double a = i * 2.1;
                s.Box((float)Math.Cos(a) * 1.7f, 3.3f + i * .28f, (float)Math.Sin(a) * 1.7f, .14f, .14f, .14f, star);
            }
            s.Part("body", 0, 0, 0);
            s.Cracks(dark, 14, 6, .4f, 3.4f);
            s.Light(0, 2.9f, 0f, .60f, .95f, 1f, 4.5f, 2.2f);
            var d = s.Finish("Andromeda Lord");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Somnia: he who heard ----------

        static SculptData HeWhoHeard(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int dream = s.Pal("dream shroud", "wraith-shroud", .22f, .20f, .34f, 0);
            int dark = s.Pal("night shadow", "obsidian", .10f, .09f, .16f, 0);
            int joint = s.Pal("dream joint", "generator-casing", .30f, .30f, .45f, 0);
            int eye = s.Pal("hearing eye", "glow-shroom", .80f, .85f, 1f, 2.2f);
            int pulse = s.Pal("dream pulse", "cherenkov-crystal", .55f, .85f, 1f, 2f);

            Leg(s, -.45f, 1.7f, 0f, 1.7f, .58f, dream, dark, dark, joint);
            Leg(s, .45f, 1.7f, 0f, 1.7f, .58f, dream, dark, dark, joint);
            s.Part("body", 0, 0, 0);
            Torso(s, 0, 2.8f, 0f, 1.6f, 1.6f, 1.1f, dream, dark);
            s.Box(0, 3.4f, .3f, .3f, .4f, .25f, pulse);
            s.Part("arm_l", -.82f, 3.2f, 0f);
            Arm(s, -.82f, 3.2f, 0f, 1.7f, .46f, dream, dark, joint);
            s.Part("arm_r", .82f, 3.2f, 0f);
            Arm(s, .82f, 3.2f, 0f, 1.7f, .46f, dream, dark, joint);
            s.Part("head", 0, 4.3f, 0f);
            Head(s, 0, 4.3f, 0f, .85f, .66f, .6f, dark, dream, dark);
            s.Box(0, 4.36f, .36f, .4f, .1f, .06f, eye);
            s.Part("body", 0, 0, 0);
            s.Cracks(dark, 16, 6, .5f, 4.0f);
            s.Light(0, 3.4f, .4f, .80f, .85f, 1f, 3.2f, 2.0f);
            var d = s.Finish("He Who Heard");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Dream: first node (floating machine) ----------

        static SculptData FirstNode(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int metal = s.Pal("node casing", "hazard-steel", .48f, .52f, .58f, 0);
            int dark = s.Pal("node shadow", "obsidian", .18f, .20f, .24f, 0);
            int joint = s.Pal("node joint", "generator-casing", .35f, .38f, .42f, 0);
            int core = s.Pal("first core", "cherenkov-crystal", .60f, .95f, 1f, 2.4f);
            int eye = s.Pal("node eye", "cherenkov-crystal", .60f, .95f, 1f, 2.2f);

            s.Part("body", 0, 0, 0);
            // floating machine core + housing
            s.Box(0, 2.4f, 0f, .8f, .8f, .8f, metal);
            s.Box(0, 3.1f, 0f, .6f, .4f, .6f, dark);
            s.Box(0, 2.4f, .45f, .35f, .35f, .2f, core);
            s.Box(0, 1.4f, 0f, .9f, .5f, .9f, dark);
            // arm struts
            s.Part("arm_l", -.8f, 2.6f, 0f);
            Arm(s, -.8f, 2.6f, 0f, 1.5f, .4f, metal, dark, joint);
            s.Part("arm_r", .8f, 2.6f, 0f);
            Arm(s, .8f, 2.6f, 0f, 1.5f, .4f, metal, dark, joint);
            s.Part("arm2_l", -.7f, 2.1f, -.05f);
            Arm(s, -.7f, 2.1f, -.05f, 1.3f, .36f, dark, metal, joint);
            s.Part("arm2_r", .7f, 2.1f, -.05f);
            Arm(s, .7f, 2.1f, -.05f, 1.3f, .36f, dark, metal, joint);
            s.Part("head", 0, 3.7f, 0f);
            Head(s, 0, 3.7f, 0f, .7f, .55f, .6f, metal, dark, dark);
            s.Box(0, 3.76f, .32f, .3f, .1f, .06f, eye);
            for (int i = 0; i < 4; i++)
            {
                s.Part("shard_" + i, 0, 3.4f + i * .26f, 0);
                double a = i * 2.1;
                s.Box((float)Math.Cos(a) * 1.6f, 3.4f + i * .26f, (float)Math.Sin(a) * 1.6f, .13f, .13f, .13f, core);
            }
            s.Part("body", 0, 0, 0);
            s.Cracks(dark, 14, 5, .5f, 3.4f);
            s.Light(0, 2.4f, .55f, .60f, .95f, 1f, 4.0f, 2.2f);
            var d = s.Finish("First Node");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Radia: radiant colossus ----------

        static SculptData RadiaColossus(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int bright = s.Pal("radiant plate", "hazard-steel", .85f, .82f, .78f, 0);
            int gold = s.Pal("sun metal", "copper-block", .95f, .70f, .35f, 0);
            int joint = s.Pal("radiant joint", "generator-casing", .60f, .55f, .45f, 0);
            int core = s.Pal("radiant core", "lava", 1f, .75f, .30f, 2.4f);
            int eye = s.Pal("radiant eye", "lava", 1f, .70f, .25f, 2.2f);

            Leg(s, -.52f, 2.0f, 0f, 2.0f, .72f, bright, gold, gold, joint);
            Leg(s, .52f, 2.0f, 0f, 2.0f, .72f, bright, gold, gold, joint);
            s.Part("body", 0, 0, 0);
            Torso(s, 0, 3.2f, 0f, 1.8f, 1.7f, 1.3f, bright, gold);
            s.Box(0, 3.6f, .45f, .5f, .45f, .3f, core);
            s.Box(-1.0f, 3.7f, 0f, .55f, .5f, .6f, gold);
            s.Box(1.0f, 3.7f, 0f, .55f, .5f, .6f, gold);
            s.Part("arm_l", -.92f, 3.85f, 0f);
            Arm(s, -.92f, 3.85f, 0f, 1.8f, .52f, bright, gold, joint);
            s.Part("arm_r", .92f, 3.85f, 0f);
            Arm(s, .92f, 3.85f, 0f, 1.8f, .52f, bright, gold, joint);
            s.Part("arm2_l", -.98f, 3.3f, -.05f);
            Arm(s, -.98f, 3.3f, -.05f, 1.5f, .46f, gold, bright, joint);
            s.Part("arm2_r", .98f, 3.3f, -.05f);
            Arm(s, .98f, 3.3f, -.05f, 1.5f, .46f, gold, bright, joint);
            s.Part("head", 0, 4.65f, 0f);
            Head(s, 0, 4.65f, 0f, .9f, .7f, .7f, bright, gold, gold);
            s.Box(0, 4.72f, .34f, .42f, .12f, .08f, eye);
            s.Part("body", 0, 0, 0);
            s.Cracks(gold, 16, 6, .5f, 4.4f);
            s.Light(0, 3.6f, .75f, 1f, .75f, .30f, 3.8f, 2.2f);
            var d = s.Finish("Radia Colossus");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Backrooms: black bacteria ----------

        static SculptData BlackBacteria(int seed)
        {
            var s = new Sculptor(.12f, seed);
            int black = s.Pal("bacteria mass", "wraith-shroud", .05f, .05f, .07f, 0);
            int wire = s.Pal("wire sinew", "obsidian", .14f, .14f, .18f, 0);
            int face = s.Pal("lure face", "bone-stalker-hide", .66f, .72f, .84f, .9f);
            int eye = s.Pal("lure eye", "glow-shroom", .55f, .62f, .80f, 2.0f);

            s.Part("body", 0, 0, 0);
            // tall wiry body
            s.Box(0, 2.0f, 0f, .45f, 1.3f, .3f, black);
            s.Box(0, 1.2f, 0f, .55f, .7f, .35f, black);
            // wiry legs
            Leg(s, -.28f, 1.0f, 0f, 1.0f, .32f, wire, black, black, wire);
            Leg(s, .28f, 1.0f, 0f, 1.0f, .32f, wire, black, black, wire);
            // long arms hanging to the floor
            s.Part("arm_l", -.55f, 2.4f, 0f);
            Arm(s, -.55f, 2.4f, 0f, 2.4f, .3f, black, wire, wire);
            s.Part("arm_r", .55f, 2.4f, 0f);
            Arm(s, .55f, 2.4f, 0f, 2.4f, .3f, black, wire, wire);
            s.Part("head", 0, 3.0f, .05f);
            Head(s, 0, 3.0f, .05f, .7f, .6f, .6f, black, face, black);
            s.Eyes(0, 3.05f, .36f, .16f, .09f, black, eye, black);
            s.Part("body", 0, 0, 0);
            s.Light(0, 3.0f, .45f, .62f, .70f, .88f, 4.5f, 1.1f);
            var d = s.Finish("Entity 23");
            ApplyLight(s, d);
            return d;
        }

        /// <summary>Backrooms swarm rank-and-file: a clean blocky black bacteria figure.</summary>
        public static SculptData BacteriaMob(int seed)
        {
            var s = new Sculptor(.11f, seed);
            int black = s.Pal("bacteria mass", "wraith-shroud", .05f, .05f, .07f, 0);
            int wire = s.Pal("wire sinew", "obsidian", .14f, .14f, .18f, 0);
            int face = s.Pal("lure face", "bone-stalker-hide", .66f, .72f, .84f, .9f);
            int eye = s.Pal("lure eye", "glow-shroom", .55f, .62f, .80f, 2.0f);

            s.Part("body", 0, 0, 0);
            s.Box(0, 1.7f, 0f, .4f, 1.0f, .26f, black);
            s.Box(0, .95f, 0f, .5f, .55f, .32f, black);
            Leg(s, -.24f, .85f, 0f, .85f, .28f, wire, black, black, wire);
            Leg(s, .24f, .85f, 0f, .85f, .28f, wire, black, black, wire);
            s.Part("arm_l", -.48f, 2.1f, 0f);
            Arm(s, -.48f, 2.1f, 0f, 2.1f, .26f, black, wire, wire);
            s.Part("arm_r", .48f, 2.1f, 0f);
            Arm(s, .48f, 2.1f, 0f, 2.1f, .26f, black, wire, wire);
            s.Part("head", 0, 2.7f, .05f);
            Head(s, 0, 2.7f, .05f, .62f, .52f, .52f, black, face, black);
            s.Eyes(0, 2.74f, .30f, .14f, .08f, black, eye, black);
            s.Part("body", 0, 0, 0);
            s.Light(0, 2.7f, .4f, .62f, .70f, .88f, 4.0f, 1.0f);
            var d = s.Finish("Entity 23 swarm");
            ApplyLight(s, d);
            return d;
        }

        // ---------- extras: corrupted AI / void horror ----------

        static SculptData CorruptedAI(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int metal = s.Pal("corrupted hull", "generator-casing", .35f, .38f, .42f, 0);
            int dark = s.Pal("glitch shadow", "obsidian", .12f, .12f, .16f, 0);
            int joint = s.Pal("corrupted joint", "generator-casing", .30f, .30f, .34f, 0);
            int glitch = s.Pal("glitch light", "cherenkov-crystal", .55f, .85f, 1f, 2.4f);
            int eye = s.Pal("ai eye", "cherenkov-crystal", .60f, .95f, 1f, 2.2f);

            Leg(s, -.48f, 1.8f, 0f, 1.8f, .6f, metal, dark, dark, joint);
            Leg(s, .48f, 1.8f, 0f, 1.8f, .6f, metal, dark, dark, joint);
            s.Part("body", 0, 0, 0);
            Torso(s, 0, 3.0f, 0f, 1.7f, 1.6f, 1.1f, metal, dark);
            s.Box(0, 3.5f, .3f, .35f, .4f, .25f, glitch);
            s.Part("arm_l", -.85f, 3.4f, 0f);
            Arm(s, -.85f, 3.4f, 0f, 1.7f, .48f, metal, dark, joint);
            s.Part("arm_r", .85f, 3.4f, 0f);
            Arm(s, .85f, 3.4f, 0f, 1.7f, .48f, metal, dark, joint);
            s.Part("arm2_l", -.9f, 3.0f, -.05f);
            Arm(s, -.9f, 3.0f, -.05f, 1.5f, .42f, dark, metal, joint);
            s.Part("arm2_r", .9f, 3.0f, -.05f);
            Arm(s, .9f, 3.0f, -.05f, 1.5f, .42f, dark, metal, joint);
            s.Part("head", 0, 4.4f, 0f);
            Head(s, 0, 4.4f, 0f, .85f, .66f, .6f, metal, dark, dark);
            s.Box(0, 4.48f, .34f, .36f, .12f, .06f, eye);
            s.Part("body", 0, 0, 0);
            s.Cracks(dark, 18, 6, .5f, 4.2f);
            s.Light(0, 3.5f, .4f, .55f, .85f, 1f, 3.5f, 2.0f);
            var d = s.Finish("The Corrupted AI");
            ApplyLight(s, d);
            return d;
        }

        static SculptData VoidHorror(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int vd = s.Pal("void flesh", "obsidian", .06f, .05f, .10f, 0);
            int dark = s.Pal("void shadow", "wraith-shroud", .12f, .10f, .18f, 0);
            int joint = s.Pal("void joint", "obsidian", .24f, .22f, .30f, 0);
            int eye = s.Pal("void eye", "cherenkov-crystal", .60f, .95f, 1f, 2.6f);
            int maw = s.Pal("void maw", "lava", 1f, .20f, .05f, 2f);

            Leg(s, -.5f, 1.8f, 0f, 1.8f, .62f, vd, dark, dark, joint);
            Leg(s, .5f, 1.8f, 0f, 1.8f, .62f, vd, dark, dark, joint);
            s.Part("body", 0, 0, 0);
            Torso(s, 0, 3.0f, 0f, 1.8f, 1.7f, 1.2f, vd, dark);
            s.Box(0, 3.6f, -.15f, .8f, .4f, .6f, vd);
            s.Part("arm_l", -.9f, 3.4f, 0f);
            Arm(s, -.9f, 3.4f, 0f, 1.9f, .52f, vd, dark, joint);
            s.Part("arm_r", .9f, 3.4f, 0f);
            Arm(s, .9f, 3.4f, 0f, 1.9f, .52f, vd, dark, joint);
            s.Part("arm2_l", -.95f, 3.0f, -.05f);
            Arm(s, -.95f, 3.0f, -.05f, 1.6f, .46f, dark, vd, joint);
            s.Part("arm2_r", .95f, 3.0f, -.05f);
            Arm(s, .95f, 3.0f, -.05f, 1.6f, .46f, dark, vd, joint);
            s.Part("head", 0, 4.5f, .05f);
            Head(s, 0, 4.5f, .05f, .9f, .72f, .7f, vd, dark, vd);
            s.Box(0, 4.4f, .42f, .45f, .22f, .22f, maw);
            s.Eyes(0, 4.6f, .42f, .18f, .1f, dark, eye, dark);
            s.Part("body", 0, 0, 0);
            s.Cracks(dark, 20, 7, .5f, 4.4f);
            s.Light(0, 4.4f, .55f, .60f, .95f, 1f, 3.6f, 2.2f);
            var d = s.Finish("The Void Horror");
            ApplyLight(s, d);
            return d;
        }

        // ---------- rank-and-file drones: clean blocky voxel machines ----------

        public static SculptData Drone(DroneKind kind)
        {
            var s = new Sculptor(.07f, 777);
            int hull = s.Pal("drone hull", "generator-casing", .30f, .33f, .38f, 0);
            int plate = s.Pal("drone plate", "hazard-steel", .50f, .52f, .58f, 0);
            int glow = s.Pal("drone glow", "cherenkov-crystal", .55f, .85f, 1f, 1.8f);
            int hot = s.Pal("engine hot", "lava", 1f, .55f, .20f, 1.6f);
            int dark = s.Pal("drone shadow", "obsidian", .12f, .12f, .15f, 0);
            switch (kind)
            {
                case DroneKind.Scout:
                    s.Box(0, 0, 0f, .34f, .2f, .30f, hull);
                    s.Box(0, .05f, .30f, .16f, .1f, .08f, glow);
                    for (int i = 0; i < 4; i++)
                    {
                        double a = Math.PI / 4 + i * Math.PI / 2;
                        float x = (float)Math.Cos(a) * .30f, z = (float)Math.Sin(a) * .30f;
                        s.Part("rotor_" + i, x, .12f, z);
                        s.Box(x, .14f, z, .14f, .02f, .05f, plate);
                        s.Box(x * .5f, .18f, z * .5f, .05f, .04f, .05f, dark);
                    }
                    s.Part("body", 0, 0, 0);
                    break;
                case DroneKind.Combat:
                    s.Box(0, 0, 0f, .36f, .18f, .44f, hull);
                    s.Box(0, .03f, .42f, .12f, .08f, .08f, glow);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        s.Part(side < 0 ? "wing_l" : "wing_r", side * .30f, .02f, 0f);
                        s.Box(side * .30f, .02f, 0f, .42f, .04f, .20f, plate);
                        s.Box(side * .60f, .02f, .02f, .30f, .03f, .16f, dark);
                        s.Box(side * .66f, .02f, .30f, .04f, .04f, .10f, dark);
                    }
                    s.Part("body", 0, 0, 0);
                    s.Box(0, -.04f, -.40f, .10f, .08f, .06f, hot);
                    break;
                case DroneKind.Repair:
                    s.Box(0, .03f, 0f, .30f, .24f, .30f, hull);
                    s.Box(0, .05f, .26f, .12f, .10f, .08f, glow);
                    s.Part("arm_r", .26f, 0f, .1f);
                    s.Box(.26f, 0f, .1f, .18f, .16f, .18f, plate);
                    s.Box(.44f, -.16f, .26f, .12f, .10f, .12f, plate);
                    s.Box(.46f, -.22f, .26f, .10f, .08f, .10f, hot);
                    s.Part("body", 0, 0, 0);
                    s.Ring(0, .34f, 0f, .22f, .04f, glow);
                    break;
                case DroneKind.Ground:
                    for (int side = -1; side <= 1; side += 2)
                    {
                        s.Part(side < 0 ? "leg_l" : "leg_r", side * .16f, .45f, 0f);
                        s.Box(side * .16f, .35f, 0f, .1f, .35f, .1f, hull);
                        s.Box(side * .16f, .05f, .05f, .12f, .10f, .16f, plate);
                    }
                    s.Part("body", 0, 0, 0);
                    s.Box(0, .7f, 0f, .28f, .26f, .22f, plate);
                    s.Part("turret", 0, 1.0f, .02f);
                    s.Box(0, 1.0f, .02f, .18f, .16f, .16f, hull);
                    s.Box(0, 1.02f, .20f, .08f, .06f, .05f, glow);
                    s.Part("body", 0, 0, 0);
                    break;
                case DroneKind.Heavy:
                    for (int i = 0; i < 4; i++)
                    {
                        double a = Math.PI / 4 + i * Math.PI / 2;
                        float x = (float)Math.Cos(a) * .3f, z = (float)Math.Sin(a) * .3f;
                        s.Part("leg_" + i, x, .55f, z);
                        s.Box(x, .45f, z, .14f, .45f, .14f, hull);
                        s.Box(x, .08f, z, .18f, .14f, .18f, plate);
                    }
                    s.Part("body", 0, 0, 0);
                    s.Box(0, .95f, 0f, .42f, .30f, .36f, plate);
                    s.Box(0, 1.2f, .1f, .26f, .18f, .22f, hull);
                    s.Box(0, 1.22f, .32f, .10f, .07f, .06f, glow);
                    for (int side = -1; side <= 1; side += 2)
                        s.Box(side * .34f, 1.18f, .30f, .08f, .08f, .22f, dark);
                    break;
                case DroneKind.Guard:
                    s.Box(0, 0, 0f, .32f, .28f, .28f, hull);
                    s.Part("shield", 0, 0, .3f);
                    s.Box(0, 0, .42f, .34f, .40f, .10f, plate);
                    s.Part("body", 0, 0, 0);
                    s.Box(0, .04f, .26f, .10f, .08f, .06f, glow);
                    s.Ring(0, .34f, 0f, .24f, .04f, glow);
                    break;
                default: // Builder
                    s.Box(0, 0, 0f, .32f, .26f, .32f, hull);
                    s.Box(0, .03f, .28f, .12f, .10f, .08f, glow);
                    s.Part("arm_r", .28f, .06f, 0f);
                    s.Box(.28f, .06f, 0f, .16f, .14f, .16f, plate);
                    s.Box(.46f, -.10f, .30f, .12f, .10f, .12f, plate);
                    s.Box(.48f, -.16f, .30f, .10f, .08f, .10f, hot);
                    s.Part("arm_l", -.28f, .06f, 0f);
                    s.Box(-.28f, .06f, 0f, .16f, .14f, .16f, plate);
                    s.Box(-.42f, -.10f, .22f, .12f, .10f, .12f, plate);
                    s.Part("body", 0, 0, 0);
                    break;
            }
            return s.Finish("Drone " + kind);
        }
    }
}
