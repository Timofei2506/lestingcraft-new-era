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
    /// Authored guardians: one real voxel sculpt per world with its own anatomy, palette and
    /// articulated parts. Replaces the old stretched-cube rig; silhouettes differ without materials.
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

        // ---------- shared anatomy helpers ----------

        static void DigitigradeLeg(Sculptor s, float side, float hipX, float hipY, float scale, int plate, int joint)
        {
            // thigh up-forward, shin back-down, foot forward: reverse-jointed frost stride
            s.Path(new[] {
                side * hipX, hipY, 0f,
                side * (hipX + .28f * scale), hipY - .78f * scale, .34f * scale,
                side * (hipX + .34f * scale), hipY - 1.62f * scale, -.42f * scale,
                side * (hipX + .36f * scale), .30f * scale, .18f * scale,
                side * (hipX + .40f * scale), .12f * scale, .62f * scale },
                new[] { .34f * scale, .30f * scale, .24f * scale, .20f * scale, .26f * scale }, plate);
            s.Ellipsoid(side * (hipX + .28f * scale), hipY - .78f * scale, .34f * scale, .30f * scale, .30f * scale, .30f * scale, joint);
            s.Ellipsoid(side * (hipX + .34f * scale), hipY - 1.62f * scale, -.42f * scale, .24f * scale, .24f * scale, .24f * scale, joint);
        }

        static void RootLeg(Sculptor s, float side, int bark, int root)
        {
            for (int j = -1; j <= 1; j++)
            {
                s.Path(new[] {
                    side * .52f, 2.25f, -.08f,
                    side * (.70f + j * .12f), 1.52f, -.08f,
                    side * (.65f + j * .13f), .82f, .12f,
                    side * (.69f + j * .16f), .20f, .38f },
                    new[] { .22f, .24f, .21f, .16f }, j == 0 ? root : bark);
                s.Path(new[] {
                    side * (.69f + j * .16f), .22f, .32f,
                    side * (.72f + j * .20f), .12f, .65f,
                    side * (.79f + j * .22f), .10f, .99f - Math.Abs(j) * .11f },
                    new[] { .19f, .16f, .055f }, bark);
            }
        }

        // ---------- Lestingia / Kepler: forest wardens ----------

        public static SculptData Warden(int seed, bool variant)
        {
            var s = new Sculptor(.13f, seed);
            int bark = s.Pal("old outer bark", "bark", .77f, .69f, .57f, 0);
            int grain = s.Pal("inner root grain", "bark", 1f, .88f, .69f, 0);
            int carved = s.Pal("petrified carved wood", "mossy-stone", .96f, .88f, .69f, 0);
            int moss = s.Pal("moss in crevices", "mossy-stone", .68f, .86f, .52f, 0);
            int heart = s.Pal("amber living heart", "glow-shroom", .95f, .30f, .04f, 1.2f);
            int eye = s.Pal("ember eye", "glow-shroom", 1f, .64f, .15f, 2f);
            int pupil = s.Pal("hollow socket", "obsidian", .10f, .07f, .05f, 0);
            int charred = s.Pal("charred fissures", "bark", .35f, .31f, .26f, 0);

            for (int side = -1; side <= 1; side += 2) RootLeg(s, side, bark, grain);
            s.Part("body", 0, 0, 0);
            // bent spine and pelvic arch
            s.Path(new[] { -.55f, 2.12f, 0f, 0f, 2.27f, .05f, .55f, 2.12f, 0f }, new[] { .32f, .36f, .32f }, bark);
            s.Path(new[] { 0f, 2.10f, -.13f, -.08f, 2.80f, -.43f, .08f, 3.62f, -.43f, 0f, 4.28f, -.10f, 0f, 4.55f, 0f },
                new[] { .29f, .32f, .38f, .30f, .22f }, bark);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * .75f, 3.77f, -.03f);
                s.Path(new[] { side * .43f, 2.15f, 0f, side * .85f, 2.65f, .15f, side * 1.02f, 3.39f, .01f, side * .88f, 3.94f, -.04f, side * .32f, 4.26f, 0f },
                    new[] { .26f, .31f, .37f, .42f, .23f }, bark);
                // open rib cage: three sweeping ribs per side, heart visible between them
                for (int j = 0; j < 3; j++)
                {
                    float y = 3.74f - j * .36f;
                    s.Path(new[] { side * .12f, y, -.36f, side * .68f, y + .06f, .10f, side * .83f, y - .05f, .49f, side * .53f, y - .28f, .79f, side * .44f, y - .41f, .84f },
                        new[] { .16f, .19f, .18f, .13f, .07f }, j == 1 ? grain : bark);
                }
                // long root arm with separated fingers and claws
                s.Path(new[] { side * .75f, 3.77f, -.03f, side * 1.16f, 3.30f, .02f, side * 1.42f, 2.60f, .12f, side * 1.50f, 1.90f, .30f },
                    new[] { .38f, .36f, .30f, .24f }, bark);
                for (int f = -1; f <= 1; f++)
                    s.Path(new[] { side * 1.50f, 1.90f, .30f, side * (1.56f + f * .06f), 1.52f, .48f + f * .10f, side * (1.58f + f * .08f), 1.24f, .66f + f * .16f },
                        new[] { .14f, .10f, .05f }, grain);
                s.Part("body", 0, 0, 0);
            }
            // sunken amber heart behind the ribs
            s.Ellipsoid(0, 3.62f, .30f, .30f, .34f, .26f, heart);
            s.Ellipsoid(0, 3.62f, .46f, .16f, .18f, .12f, s.Pal("heart hot facet", "glow-shroom", 1f, .67f, .18f, 1.9f));
            // carved mask head with deep sockets and branching antler crown
            s.Part("head", 0, 4.55f, 0);
            s.Ellipsoid(0, 4.86f, .06f, .34f, .46f, .34f, carved);
            s.Ellipsoid(0, 4.62f, .30f, .22f, .26f, .18f, carved);
            s.Eyes(0, 4.94f, .34f, .16f, .10f, pupil, eye, pupil);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Path(new[] { side * .18f, 5.24f, 0f, side * .40f, 5.66f, -.10f, side * .62f, 6.02f, .04f, side * .86f, 6.30f, -.12f },
                    new[] { .12f, .10f, .07f, .04f }, bark);
                s.Path(new[] { side * .40f, 5.66f, -.10f, side * .52f, 5.92f, .22f }, new[] { .07f, .04f }, bark);
            }
            s.Part("body", 0, 0, 0);
            // Extra antler branching and mossy bark knots for a denser, more organic silhouette.
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * .18f, 5.24f, 0f, side * .34f, 5.78f, -.14f, side * .44f, 6.20f, -.30f },
                    new[] { .09f, .06f, .03f }, bark);
            for (int k = 0; k < 6; k++)
            {
                double a = k * Math.PI * 2 / 6 + .5;
                s.Ellipsoid((float)Math.Cos(a) * .62f, 3.30f + (k % 2) * .22f, (float)Math.Sin(a) * .42f,
                    .10f, .12f, .10f, k % 2 == 0 ? moss : grain);
            }
            s.Cracks(charred, 26, 7, .4f, 4.4f);
            s.Cracks(moss, 18, 5, .2f, 3.4f);
            s.Light(0, 3.62f, .60f, 1f, .38f, .055f, 3.5f, 2.7f);
            var d = s.Finish(variant ? "Elder Root Warden" : "Ancient Guardian");
            ApplyLight(s, d);
            return d;
        }

        static void ApplyLight(Sculptor s, SculptData d)
        {
            if (!s.HasLight) return;
            var l = s.LightSpec;
            d.LightX = l[0]; d.LightY = l[1]; d.LightZ = l[2];
            d.LightR = l[3]; d.LightG = l[4]; d.LightB = l[5];
            d.LightRange = l[6]; d.LightIntensity = l[7];
        }

        // ---------- Mars: tall four-armed basalt colossus ----------

        static SculptData MarsColossus(int seed)
        {
            var s = new Sculptor(.16f, seed);
            int basalt = s.Pal("stacked basalt", "basalt", .62f, .34f, .28f, 0);
            int dark = s.Pal("basalt shade", "basalt", .38f, .20f, .17f, 0);
            int crack = s.Pal("magma fissure", "magma-basalt", 1f, .42f, .12f, 1.5f);
            int obs = s.Pal("obsidian seam", "obsidian", .12f, .09f, .09f, 0);
            int core = s.Pal("forge heart", "lava", 1f, .50f, .12f, 2.0f);
            int eye = s.Pal("ember eye", "lava", 1f, .62f, .18f, 2.2f);
            int socket = s.Pal("socket", "obsidian", .08f, .06f, .06f, 0);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "leg_l" : "leg_r", side * 1.0f, 2.4f, 0);
                s.Path(new[] { side * 1.0f, 2.5f, 0f, side * 1.15f, 1.5f, .1f, side * 1.05f, .6f, -.05f, side * 1.1f, .18f, .25f },
                    new[] { .55f, .48f, .42f, .55f }, basalt);
                s.Box(side * 1.1f, .12f, .35f, .62f, .14f, .55f, dark);
            }
            s.Part("body", 0, 0, 0);
            // pelvis and tall layered torso: overlapping plates, not one box
            s.Ellipsoid(0, 2.75f, 0, 1.15f, .55f, .80f, dark);
            for (int i = 0; i < 5; i++)
            {
                float y = 3.2f + i * .62f;
                float w = 1.55f - Math.Abs(i - 2) * .12f;
                s.Box(0, y, -.10f + i * .02f, w, .34f, .78f - i * .04f, i % 2 == 0 ? basalt : dark);
                s.Box(0, y - .18f, .62f - i * .05f, w * .82f, .16f, .18f, dark);
            }
            // magma veins between plates
            for (int i = 0; i < 4; i++) s.Box(0, 3.5f + i * .62f, .74f - i * .05f, 1.05f - i * .08f, .05f, .06f, crack);
            // four arms: two massive, two smaller under-arms
            for (int pair = 0; pair < 2; pair++)
            {
                float ay = pair == 0 ? 5.5f : 4.45f;
                float ax = pair == 0 ? 2.15f : 1.70f;
                float ar = pair == 0 ? .52f : .36f;
                for (int side = -1; side <= 1; side += 2)
                {
                    string nm = (pair == 0 ? "arm_" : "arm2_") + (side < 0 ? "l" : "r");
                    s.Part(nm, side * ax, ay, 0);
                    s.Ellipsoid(side * ax, ay, 0, ar * 1.5f, ar * 1.2f, ar * 1.4f, dark);
                    s.Path(new[] { side * ax, ay, .1f, side * (ax + .45f), ay - .9f, .45f, side * (ax + .55f), ay - 1.9f, .70f },
                        new[] { ar, ar * .82f, ar * .72f }, basalt);
                    s.Box(side * (ax + .55f), ay - 2.35f, .80f, ar * 1.15f, ar * .95f, ar * 1.3f, dark);
                    for (int f = -1; f <= 1; f++)
                        s.Path(new[] { side * (ax + .55f), ay - 2.6f, .80f + f * .18f, side * (ax + .58f), ay - 3.0f, .98f + f * .26f },
                            new[] { .12f, .06f }, obs);
                }
            }
            s.Part("head", 0, 6.4f, 0);
            s.Box(0, 6.6f, .1f, .72f, .62f, .66f, basalt);
            s.Box(0, 6.42f, .68f, .55f, .16f, .12f, socket);
            s.Eyes(0, 6.62f, .88f, .30f, .13f, socket, eye, socket);
            // horn crest
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * .55f, 7.05f, 0f, side * 1.05f, 7.75f, -.15f, side * 1.35f, 8.45f, -.40f },
                    new[] { .24f, .15f, .07f }, obs);
            // Forged shoulder spikes rising between the torso and the great arms.
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * 1.30f, 5.50f, .12f, side * 1.70f, 5.92f, .02f, side * 1.95f, 6.40f, -.18f },
                    new[] { .16f, .11f, .05f }, obs);
            // Forged chest ridge over the forge heart.
            s.Path(new[] { 0f, 4.15f, .66f, 0f, 4.85f, .84f, 0f, 5.55f, .74f }, new[] { .15f, .13f, .09f }, dark);
            s.Part("body", 0, 0, 0);
            s.Ellipsoid(0, 4.6f, .78f, .42f, .46f, .30f, core);
            s.Cracks(crack, 40, 9, .6f, 6.2f);
            s.Cracks(obs, 26, 7, .6f, 6.6f);
            s.Light(0, 4.6f, 1.2f, 1f, .42f, .10f, 24f, 4.2f);
            var d = s.Finish("Red Colossus");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Venus: six-legged acid predator with sickle forelimbs ----------

        static SculptData VenusPredator(int seed)
        {
            var s = new Sculptor(.14f, seed);
            int chitin = s.Pal("storm chitin", "rad-scorpion-carapace", .42f, .46f, .55f, 0);
            int plate = s.Pal("cut plate", "scorpion-chitin", .30f, .34f, .42f, 0);
            int acid = s.Pal("acid tank", "cherenkov-crystal", .55f, .95f, .45f, .5f);
            int acidHot = s.Pal("acid core", "cherenkov-crystal", .80f, 1f, .55f, 1.2f);
            int eye = s.Pal("cluster eye", "glow-shroom", .85f, 1f, .50f, 2.1f);
            int socket = s.Pal("socket", "obsidian", .07f, .08f, .10f, 0);
            int tube = s.Pal("feed tube", "copper-cable", .55f, .48f, .30f, 0);

            // six curved legs: three per side, splayed predator stance
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 3; i++)
                {
                    float z = -.9f + i * .9f;
                    s.Part("leg_" + (side < 0 ? "l" : "r") + i, side * 1.0f, 2.0f, z);
                    s.Path(new[] { side * .9f, 2.0f, z, side * 1.9f, 2.3f, z + .2f, side * 2.6f, 1.2f, z + .35f, side * 2.9f, .14f, z + .1f },
                        new[] { .38f, .32f, .24f, .13f }, chitin);
                    s.Path(new[] { side * 2.9f, .12f, z + .1f, side * 3.05f, .10f, z - .25f }, new[] { .12f, .05f }, plate);
                }
            s.Part("body", 0, 0, 0);
            // low elongated torso of overlapping cut plates
            for (int i = 0; i < 6; i++)
            {
                float z = -1.3f + i * .55f;
                s.Ellipsoid(0, 2.15f - Math.Abs(i - 2.5f) * .06f, z, 1.15f - Math.Abs(i - 2.5f) * .10f, .78f, .42f, i % 2 == 0 ? chitin : plate);
            }
            // acid tanks on the spine with feed tubes
            for (int side = -1; side <= 1; side += 2)
            {
                s.Ellipsoid(side * .55f, 2.88f, -.35f, .34f, .42f, .58f, acid);
                s.Ellipsoid(side * .55f, 2.96f, -.35f, .17f, .24f, .34f, acidHot);
                s.Path(new[] { side * .55f, 2.6f, .30f, side * .35f, 2.35f, .70f, side * .18f, 2.2f, 1.0f }, new[] { .10f, .08f, .06f }, tube);
            }
            // sickle forelimbs: raised scythe blades
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.0f, 2.3f, 1.3f);
                s.Path(new[] { side * .9f, 2.3f, 1.2f, side * 1.5f, 2.6f, 1.9f, side * 1.7f, 3.1f, 2.3f },
                    new[] { .34f, .28f, .22f }, chitin);
                s.Path(new[] { side * 1.7f, 3.1f, 2.3f, side * 1.5f, 3.7f, 2.9f, side * .9f, 4.0f, 3.1f, side * .3f, 3.9f, 2.9f },
                    new[] { .20f, .15f, .10f, .05f }, plate);
            }
            // lowered hunter head with compound eye cluster and mandibles
            s.Part("head", 0, 2.2f, 1.7f);
            s.Ellipsoid(0, 2.25f, 1.75f, .62f, .50f, .58f, chitin);
            s.Ellipsoid(0, 2.05f, 2.20f, .40f, .30f, .30f, plate);
            for (int i = -2; i <= 2; i++)
                s.Ellipsoid(i * .16f, 2.42f, 2.16f, .07f, .07f, .06f, i == 0 ? eye : socket);
            s.Eyes(0, 2.44f, 2.20f, .30f, .10f, socket, eye, socket);
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * .30f, 1.95f, 2.30f, side * .48f, 1.75f, 2.70f, side * .30f, 1.62f, 3.0f },
                    new[] { .10f, .07f, .04f }, plate);
            s.Part("body", 0, 0, 0);
            // Spine ridge of cut plates above the low torso.
            s.Path(new[] { 0f, 2.60f, -1.15f, 0f, 2.96f, -.45f, 0f, 2.99f, .45f, 0f, 2.60f, 1.15f },
                new[] { .13f, .15f, .15f, .13f }, plate);
            s.Cracks(socket, 22, 6, 1.2f, 3.2f);
            s.Light(0, 3.0f, -.35f, .62f, 1f, .45f, 14f, 3.2f);
            var d = s.Finish("The Storm Herald");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Mercury: low six-legged mechanical scarab with mirror vanes ----------

        static SculptData MercuryScarab(int seed)
        {
            var s = new Sculptor(.13f, seed);
            int hull = s.Pal("titanium hull", "titanium-hull", .62f, .66f, .72f, 0);
            int dark = s.Pal("shadow housing", "generator-casing", .22f, .24f, .28f, 0);
            int heat = s.Pal("heat erosion", "magma-basalt", .48f, .30f, .24f, 0);
            int core = s.Pal("solar core", "lava", 1f, .55f, .15f, 2.2f);
            int lens = s.Pal("mirror vane", "titanium-hull", .95f, .97f, 1.05f, .12f);
            int eye = s.Pal("optic", "cherenkov-crystal", 1f, .75f, .30f, 2.0f);

            // six stubby powered legs
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 3; i++)
                {
                    float z = -.8f + i * .8f;
                    s.Part("leg_" + (side < 0 ? "l" : "r") + i, side * .9f, 1.0f, z);
                    s.Path(new[] { side * .8f, 1.0f, z, side * 1.5f, .9f, z + .1f, side * 1.8f, .25f, z + .2f, side * 2.1f, .10f, z },
                        new[] { .26f, .22f, .16f, .20f }, dark);
                    s.Ellipsoid(side * 1.5f, .9f, z + .1f, .20f, .20f, .20f, hull);
                }
            s.Part("body", 0, 0, 0);
            // low wide carapace: layered metal flaps
            s.Ellipsoid(0, 1.90f, 0, 1.45f, 1.10f, 1.55f, hull);
            for (int i = 0; i < 4; i++)
                s.Box(0, 2.50f + i * .10f, -.9f + i * .62f, 1.30f - i * .10f, .11f, .34f, i % 2 == 0 ? hull : dark);
            s.Ellipsoid(0, 1.50f, 0, 1.20f, .60f, 1.30f, dark);
            // solar core under the front carapace slit
            s.Ellipsoid(0, 1.75f, 1.15f, .55f, .46f, .38f, core);
            // wide flat mirror vanes fanned back from the carapace, like a scarab's solar sails
            for (int side = -1; side <= 1; side += 2)
                for (int v = 0; v < 3; v++)
                {
                    s.Part("vane_" + (side < 0 ? "l" : "r") + v, side * .6f, 2.1f, -.9f);
                    float spread = .45f + v * .55f;
                    float lift = .55f - v * .12f;
                    for (int seg = 0; seg < 5; seg++)
                    {
                        float t = seg / 4f;
                        float x = side * (.55f + spread * t);
                        float y = 2.1f + lift * t + .18f * (float)Math.Sin(t * 3f);
                        float z = -.95f - .95f * t;
                        float w = .30f + .34f * t;
                        s.Ellipsoid(x, y, z, w * .55f, .05f + .03f * t, w, lens);
                    }
                }
            // digging mandibles and optic row
            s.Part("head", 0, 1.95f, 1.5f);
            s.Ellipsoid(0, 1.95f, 1.55f, .60f, .42f, .45f, dark);
            for (int i = -1; i <= 1; i++) s.Ellipsoid(i * .28f, 2.07f, 1.92f, .09f, .09f, .07f, eye);
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * .45f, 1.65f, 1.8f, side * .70f, 1.35f, 2.2f, side * .55f, 1.15f, 2.6f }, new[] { .12f, .08f, .05f }, hull);
            s.Part("body", 0, 0, 0);
            s.Cracks(heat, 30, 8, 1.2f, 3.0f);
            s.Light(0, 1.8f, 1.3f, 1f, .5f, .15f, 16f, 3.6f);
            var d = s.Finish("Solar Core Guardian");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Moon / Europa / Pluto: digitigrade frost titans ----------

        static SculptData MoonTitan(int seed)
        {
            var s = new Sculptor(.15f, seed);
            int ice = s.Pal("dust ice plate", "glacial-ice", .78f, .80f, .86f, 0);
            int rock = s.Pal("regolith rock", "stone", .48f, .48f, .52f, 0);
            int glow = s.Pal("cold core", "cherenkov-crystal", .62f, .82f, 1f, 1.4f);
            int eye = s.Pal("pale eye", "cherenkov-crystal", .85f, .95f, 1f, 2.0f);
            int socket = s.Pal("socket", "obsidian", .08f, .08f, .10f, 0);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "leg_l" : "leg_r", side * 1.0f, 2.6f, 0);
                DigitigradeLeg(s, side, 1.0f, 2.6f, 1.15f, ice, rock);
            }
            s.Part("body", 0, 0, 0);
            s.Ellipsoid(0, 3.1f, 0, 1.35f, .8f, .95f, rock);
            for (int i = 0; i < 4; i++)
                s.Box(0, 3.5f + i * .5f, -.05f, 1.30f - i * .08f, .28f, .80f - i * .05f, i % 2 == 0 ? ice : rock);
            s.Ellipsoid(0, 4.1f, .72f, .40f, .42f, .26f, glow);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.5f, 4.3f, 0);
                s.Path(new[] { side * 1.5f, 4.3f, 0f, side * 1.9f, 3.4f, .2f, side * 2.0f, 2.5f, .5f }, new[] { .42f, .34f, .28f }, ice);
                s.Box(side * 2.0f, 2.2f, .6f, .34f, .30f, .42f, rock);
            }
            s.Part("head", 0, 5.1f, 0);
            s.Box(0, 5.3f, .1f, .62f, .55f, .58f, ice);
            s.Eyes(0, 5.42f, .62f, .26f, .11f, socket, eye, socket);
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * .40f, 5.75f, 0f, side * .70f, 6.2f, -.2f }, new[] { .14f, .06f }, ice);
            // Frost spikes rising from the shoulders between the torso and the arms.
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * 1.12f, 4.60f, 0f, side * 1.48f, 5.05f, -.08f, side * 1.62f, 5.50f, -.22f },
                    new[] { .15f, .10f, .05f }, ice);
            s.Part("body", 0, 0, 0);
            s.Cracks(glow, 24, 7, 1.0f, 5.0f);
            s.Light(0, 4.1f, 1.0f, .62f, .82f, 1f, 18f, 3.4f);
            var d = s.Finish("Лунный Титанит");
            ApplyLight(s, d);
            return d;
        }

        static SculptData EuropaShell(int seed)
        {
            var s = new Sculptor(.15f, seed);
            int shell = s.Pal("cracked shell", "glacial-ice", .70f, .84f, .92f, 0);
            int deep = s.Pal("deep ice", "glacial-ice", .42f, .62f, .80f, 0);
            int vein = s.Pal("brine vein", "cherenkov-crystal", .44f, .86f, 1f, 1.6f);
            int eye = s.Pal("abyss eye", "cherenkov-crystal", .55f, .90f, 1f, 2.1f);
            int socket = s.Pal("socket", "obsidian", .06f, .08f, .12f, 0);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "leg_l" : "leg_r", side * 1.1f, 2.4f, 0);
                DigitigradeLeg(s, side, 1.1f, 2.4f, 1.05f, shell, deep);
            }
            s.Part("body", 0, 0, 0);
            // wide shield back: a dome of cracked plates
            s.Shell(0, 3.4f, -.2f, 1.9f, 1.5f, 1.5f, .38f, shell);
            s.Ellipsoid(0, 3.2f, 0, 1.30f, 1.05f, 1.05f, deep);
            for (int i = 0; i < 3; i++)
                s.Box(0, 3.0f + i * .55f, .85f - i * .10f, 1.10f - i * .12f, .26f, .30f, shell);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.6f, 3.6f, .2f);
                s.Path(new[] { side * 1.6f, 3.6f, .2f, side * 2.0f, 2.7f, .5f, side * 2.1f, 1.9f, .8f }, new[] { .40f, .32f, .26f }, shell);
                // ice blade gauntlet
                s.Path(new[] { side * 2.1f, 1.9f, .8f, side * 2.2f, 1.5f, 1.3f, side * 2.1f, 1.2f, 1.8f }, new[] { .18f, .12f, .06f }, deep);
            }
            s.Part("head", 0, 4.45f, .95f);
            s.Ellipsoid(0, 4.55f, 1.05f, .55f, .48f, .50f, deep);
            s.Ellipsoid(0, 4.62f, 1.42f, .34f, .30f, .22f, shell);
            s.Eyes(0, 4.66f, 1.46f, .24f, .10f, socket, eye, socket);
            // Ice shards bursting out of the shield back.
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 2; k++)
                {
                    float y = 3.70f + k * .55f;
                    s.Path(new[] { side * (.70f + k * .18f), y, -1.30f, side * (.86f + k * .22f), y + .45f, -1.48f, side * (.80f + k * .20f), y + .90f, -1.28f },
                        new[] { .11f, .08f, .04f }, deep);
                }
            s.Part("body", 0, 0, 0);
            s.Cracks(vein, 46, 10, 1.0f, 5.0f);
            s.Light(0, 3.6f, .9f, .44f, .86f, 1f, 16f, 3.0f);
            var d = s.Finish("Ледяной Панцирь Европы");
            ApplyLight(s, d);
            return d;
        }

        static SculptData PlutoHermit(int seed)
        {
            var s = new Sculptor(.14f, seed);
            int crust = s.Pal("nitrogen crust", "snow", .82f, .84f, .88f, 0);
            int rock = s.Pal("tholin rock", "red-mesa", .46f, .34f, .30f, 0);
            int glow = s.Pal("faint heart", "cherenkov-crystal", .72f, .88f, 1f, 1.1f);
            int eye = s.Pal("old eye", "glow-shroom", .90f, .95f, 1f, 1.8f);
            int socket = s.Pal("socket", "obsidian", .07f, .07f, .09f, 0);
            // hunched spine with a huge ice shell on the back
            s.Path(new[] { 0f, 2.2f, .3f, 0f, 2.9f, -.2f, 0f, 3.6f, -.5f, 0f, 4.2f, -.2f }, new[] { .5f, .55f, .5f, .4f }, rock);
            s.Shell(0, 3.6f, -.85f, 1.5f, 1.4f, 1.2f, .34f, crust);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "leg_l" : "leg_r", side * .8f, 2.2f, 0);
                DigitigradeLeg(s, side, .8f, 2.2f, .95f, rock, crust);
                // cane-like long arms reaching the ground
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.1f, 3.6f, -.2f);
                s.Path(new[] { side * 1.1f, 3.6f, -.2f, side * 1.5f, 2.7f, .3f, side * 1.6f, 1.6f, .7f, side * 1.5f, .4f, .9f },
                    new[] { .34f, .28f, .22f, .18f }, rock);
                s.Ellipsoid(side * 1.5f, .3f, .95f, .26f, .18f, .30f, crust);
            }
            s.Part("head", 0, 4.35f, .45f);
            s.Ellipsoid(0, 4.55f, .75f, .48f, .42f, .44f, crust);
            s.Eyes(0, 4.62f, 1.10f, .20f, .09f, socket, eye, socket);
            // beard of ice shards
            for (int i = -2; i <= 2; i++)
                s.Path(new[] { i * .14f, 4.3f, 1.0f, i * .18f, 3.9f, 1.12f }, new[] { .07f, .04f }, crust);
            s.Ellipsoid(0, 3.1f, .35f, .30f, .30f, .24f, glow);
            s.Part("body", 0, 0, 0);
            s.Cracks(glow, 20, 6, 2.0f, 4.6f);
            s.Light(0, 3.1f, .7f, .72f, .88f, 1f, 10f, 2.2f);
            var d = s.Finish("Плутоновый Отшельник");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Floating archons ----------

        static void ArchonRobe(Sculptor s, int cloth, int trim, float topY, float width)
        {
            // tapering torn robe: no legs, ragged streams at the hem
            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                float y = topY - t * 2.6f;
                float w = width * (1f - t * .72f);
                s.Ellipsoid(0, y, 0, w, .28f, w * .8f, i % 2 == 0 ? cloth : trim);
            }
            for (int k = 0; k < 7; k++)
            {
                double a = k * Math.PI * 2 / 7 + .4;
                float x = (float)Math.Cos(a) * width * .5f, z = (float)Math.Sin(a) * width * .45f;
                s.Path(new[] { x, topY - 2.4f, z, x * 1.15f, topY - 3.1f, z * 1.15f, x * .9f, topY - 3.7f, z * .9f },
                    new[] { .16f, .10f, .05f }, cloth);
            }
            // Glowing collar ring at the neck of the robe.
            s.Ring(0, topY + .30f, 0, width * .86f, .055f, trim);
        }

        static SculptData RingedLeviathan(int seed)
        {
            var s = new Sculptor(.15f, seed);
            int hide = s.Pal("leviathan hide", "voidray-skin", .30f, .22f, .46f, 0);
            int ring = s.Pal("saturn ring fin", "andromeda-crystal", .85f, .65f, .30f, .8f);
            int glow = s.Pal("throat light", "cherenkov-crystal", .75f, .45f, 1f, 1.5f);
            int eye = s.Pal("deep eye", "glow-shroom", .95f, .75f, .35f, 2.0f);
            int socket = s.Pal("socket", "obsidian", .08f, .06f, .10f, 0);
            // coiled serpent body floating vertically
            s.Part("body", 0, 3.4f, 0);
            for (int i = 0; i < 9; i++)
            {
                float t = i / 8f;
                double a = t * Math.PI * 1.6;
                float x = (float)Math.Cos(a) * .8f * t, z = (float)Math.Sin(a) * .8f * t;
                s.Ellipsoid(x, 1.2f + t * 4.4f, z, .95f - t * .35f, .55f, .85f - t * .30f, hide);
                if (i % 2 == 1) s.Ring(x, 1.2f + t * 4.4f, z, 1.05f - t * .35f, .07f, ring);
            }
            // side fins
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "wing_l" : "wing_r", side * .9f, 4.4f, 0);
                s.Path(new[] { side * .8f, 4.4f, 0f, side * 1.8f, 4.7f, -.3f, side * 2.6f, 4.5f, -.6f }, new[] { .22f, .30f, .14f }, hide);
            }
            s.Part("head", 0, 5.8f, 0);
            s.Ellipsoid(0, 5.95f, .35f, .74f, .62f, .82f, hide);
            s.Ellipsoid(0, 5.72f, .98f, .44f, .30f, .34f, glow);
            s.Eyes(0, 6.12f, .95f, .32f, .12f, socket, eye, socket);
            s.Ring(0, 6.9f, 0, 1.7f, .09f, ring);
            s.Part("body", 0, 0, 0);
            s.Cracks(glow, 20, 6, 1.5f, 5.5f);
            s.Light(0, 5.7f, 1.0f, .75f, .45f, 1f, 14f, 3.0f);
            var d = s.Finish("Ringed Leviathan");
            ApplyLight(s, d);
            return d;
        }

        static SculptData TrappistChoir(int seed)
        {
            var s = new Sculptor(.14f, seed);
            int bell = s.Pal("resonant bell", "copper-block", .72f, .48f, .30f, 0);
            int pipe = s.Pal("choir pipe", "steel-rail", .55f, .58f, .64f, 0);
            int glow = s.Pal("resonance", "cherenkov-crystal", .78f, .62f, .92f, 1.6f);
            int eye = s.Pal("watcher eye", "glow-shroom", .90f, .80f, 1f, 2.0f);
            int socket = s.Pal("socket", "obsidian", .08f, .07f, .10f, 0);
            ArchonRobe(s, bell, pipe, 4.6f, 1.5f);
            // organ pipes around the shoulders
            for (int k = 0; k < 8; k++)
            {
                double a = k * Math.PI * 2 / 8;
                float x = (float)Math.Cos(a) * 1.05f, z = (float)Math.Sin(a) * .95f;
                float h = .7f + (k % 3) * .35f;
                s.Path(new[] { x, 4.5f, z, x * 1.1f, 4.5f + h, z * 1.1f }, new[] { .14f, .11f }, pipe);
                s.Ellipsoid(x * 1.1f, 4.55f + h, z * 1.1f, .10f, .10f, .10f, glow);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.2f, 4.2f, 0);
                s.Path(new[] { side * 1.2f, 4.2f, 0f, side * 1.7f, 3.6f, .5f, side * 1.6f, 3.2f, 1.0f }, new[] { .26f, .20f, .16f }, bell);
            }
            s.Part("head", 0, 5.1f, 0);
            s.Ellipsoid(0, 5.3f, .1f, .46f, .52f, .44f, bell);
            s.Eyes(0, 5.4f, .46f, .20f, .09f, socket, eye, socket);
            s.Ring(0, 6.1f, 0, 1.15f, .07f, glow);
            s.Light(0, 4.6f, .6f, .78f, .62f, .92f, 12f, 2.6f);
            var d = s.Finish("Траппист-Смотритель");
            ApplyLight(s, d);
            return d;
        }

        static SculptData AndromedaLord(int seed)
        {
            var s = new Sculptor(.15f, seed);
            int robe = s.Pal("elysium robe", "wraith-shroud", .34f, .26f, .52f, 0);
            int gold = s.Pal("crown gold", "ore-gold", .92f, .76f, .34f, .3f);
            int crys = s.Pal("andromeda crystal", "andromeda-crystal", .80f, .55f, 1f, 1.4f);
            int eye = s.Pal("violet eye", "glow-shroom", .95f, .70f, 1f, 2.2f);
            int socket = s.Pal("socket", "obsidian", .09f, .06f, .12f, 0);
            ArchonRobe(s, robe, gold, 4.8f, 1.7f);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.3f, 4.4f, 0);
                s.Path(new[] { side * 1.3f, 4.4f, 0f, side * 1.8f, 3.9f, .4f, side * 1.7f, 3.5f, .9f }, new[] { .28f, .22f, .18f }, robe);
                // Crystal growths on the shoulders.
                s.Ellipsoid(side * .85f, 4.80f, .25f, .15f, .22f, .15f, crys);
            }
            // crystal sceptre in the right hand
            s.Part("arm_r", 1.7f, 3.5f, .9f);
            s.Path(new[] { 1.75f, 2.9f, .95f, 1.80f, 4.6f, 1.0f }, new[] { .07f, .06f }, gold);
            s.Ellipsoid(1.80f, 4.85f, 1.0f, .22f, .34f, .22f, crys);
            s.Part("head", 0, 5.3f, 0);
            s.Ellipsoid(0, 5.5f, .1f, .44f, .50f, .42f, robe);
            s.Eyes(0, 5.6f, .44f, .19f, .09f, socket, eye, socket);
            // crown of seven crystals
            for (int k = 0; k < 7; k++)
            {
                double a = Math.PI * (0.15 + 0.7 * k / 6f);
                float x = (float)Math.Cos(a) * .52f, z = (float)Math.Sin(a) * .30f - .1f;
                s.Path(new[] { x, 5.85f, z, x * 1.25f, 6.35f + (k % 2) * .18f, z * 1.2f }, new[] { .10f, .05f }, crys);
            }
            s.Ring(0, 6.5f, 0, 1.35f, .07f, gold);
            s.Light(0, 5.0f, .6f, .80f, .55f, 1f, 14f, 3.0f);
            var d = s.Finish("Элизиумский Владыка");
            ApplyLight(s, d);
            return d;
        }

        static SculptData HeWhoHeard(int seed)
        {
            var s = new Sculptor(.14f, seed);
            int skin = s.Pal("pale dream skin", "bone-stalker-hide", .78f, .76f, .72f, 0);
            int shroud = s.Pal("sleep shroud", "wraith-shroud", .22f, .22f, .34f, 0);
            int eye = s.Pal("heard eye", "glow-shroom", .85f, .85f, 1f, 2.4f);
            int hollow = s.Pal("hollow", "obsidian", .05f, .05f, .08f, 0);
            ArchonRobe(s, shroud, shroud, 4.4f, 1.2f);
            // gaunt torso and too-long arms
            s.Ellipsoid(0, 4.3f, 0, .70f, .95f, .50f, skin);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * .8f, 4.8f, 0);
                s.Path(new[] { side * .8f, 4.8f, 0f, side * 1.2f, 3.8f, .2f, side * 1.3f, 2.6f, .5f, side * 1.2f, 1.6f, .8f },
                    new[] { .20f, .16f, .12f, .09f }, skin);
                for (int f = -1; f <= 1; f++)
                    s.Path(new[] { side * 1.2f, 1.6f, .8f, side * (1.25f + f * .05f), 1.1f, .95f + f * .10f }, new[] { .06f, .03f }, skin);
            }
            // elongated head with a single huge ear-fan and hollow face
            s.Part("head", 0, 5.3f, 0);
            s.Ellipsoid(0, 5.6f, 0, .38f, .68f, .36f, skin);
            s.Ellipsoid(0, 5.75f, .30f, .26f, .34f, .16f, hollow);
            s.Eyes(0, 5.85f, .40f, .15f, .07f, hollow, eye, hollow);
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * .36f, 5.6f, 0f, side * .80f, 5.9f, -.1f, side * 1.05f, 6.3f, -.2f }, new[] { .16f, .22f, .10f }, skin);
            s.Part("body", 0, 0, 0);
            s.Cracks(hollow, 14, 5, 2.0f, 5.0f);
            s.Light(0, 5.8f, .6f, .85f, .85f, 1f, 9f, 2.4f);
            var d = s.Finish("He Who Heard");
            ApplyLight(s, d);
            return d;
        }

        static SculptData FirstNode(int seed)
        {
            var s = new Sculptor(.14f, seed);
            int chassis = s.Pal("node chassis", "generator-casing", .26f, .28f, .34f, 0);
            int panel = s.Pal("server panel", "hazard-steel", .48f, .50f, .56f, 0);
            int cable = s.Pal("data cable", "copper-cable", .72f, .48f, .22f, 0);
            int glow = s.Pal("machine heart", "cherenkov-crystal", .55f, .90f, 1f, 1.8f);
            int eye = s.Pal("optic array", "glow-shroom", .60f, .95f, 1f, 2.2f);
            ArchonRobe(s, chassis, panel, 4.4f, 1.4f);
            // server torso with vent slots
            s.Box(0, 4.2f, 0, .85f, 1.0f, .60f, chassis);
            for (int i = 0; i < 4; i++) s.Box(0, 3.6f + i * .38f, .58f, .62f, .07f, .06f, glow);
            // cable limbs
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.0f, 4.6f, 0);
                s.Path(new[] { side * 1.0f, 4.6f, 0f, side * 1.5f, 3.9f, .3f, side * 1.4f, 3.2f, .8f }, new[] { .16f, .12f, .10f }, cable);
                s.Box(side * 1.4f, 3.05f, .85f, .18f, .16f, .22f, panel);
            }
            s.Part("head", 0, 5.3f, 0);
            s.Box(0, 5.5f, .05f, .42f, .40f, .40f, panel);
            for (int i = -1; i <= 1; i++) s.Ellipsoid(i * .18f, 5.55f, .44f, .07f, .07f, .05f, eye);
            s.Ring(0, 6.2f, 0, 1.05f, .06f, glow);
            // hanging cables from the hem
            for (int k = 0; k < 6; k++)
            {
                double a = k * Math.PI * 2 / 6;
                s.Path(new[] { (float)Math.Cos(a) * .8f, 3.3f, (float)Math.Sin(a) * .7f, (float)Math.Cos(a) * 1.1f, 2.4f, (float)Math.Sin(a) * 1.0f },
                    new[] { .07f, .04f }, cable);
            }
            s.Light(0, 4.2f, .8f, .55f, .90f, 1f, 12f, 2.8f);
            var d = s.Finish("The First Node");
            ApplyLight(s, d);
            return d;
        }

        // ---------- Ember / Radia / Backrooms / extras ----------

        static SculptData ProximaDevourer(int seed)
        {
            var s = new Sculptor(.15f, seed);
            int crust = s.Pal("tidal crust", "magma-basalt", .34f, .18f, .14f, 0);
            int core = s.Pal("devourer core", "lava", 1f, .46f, .16f, 2.2f);
            int shard = s.Pal("tidal shard", "obsidian", .16f, .10f, .10f, 0);
            int vein = s.Pal("plasma vein", "lava", 1f, .62f, .22f, 1.7f);
            // cracked floating core sphere with peeled shell plates
            s.Part("body", 0, 3.6f, 0);
            s.Ellipsoid(0, 3.6f, 0, 1.35f, 1.35f, 1.35f, core);
            for (int k = 0; k < 7; k++)
            {
                double a = k * Math.PI * 2 / 7, b = (k % 3 - 1) * .7;
                float x = (float)(Math.Cos(a) * Math.Cos(b)) * 1.30f;
                float y = 3.6f + (float)Math.Sin(b) * 1.30f;
                float z = (float)(Math.Sin(a) * Math.Cos(b)) * 1.30f;
                s.Part("plate_" + k, x, y, z);
                s.Ellipsoid(x * 1.12f, 3.6f + (y - 3.6f) * 1.12f, z * 1.12f, .55f, .55f, .30f, crust);
            }
            s.Part("body", 0, 3.6f, 0);
            s.Cracks(vein, 40, 8, 2.4f, 4.8f);
            // orbiting shards as separate articulated parts
            for (int i = 0; i < 4; i++)
            {
                s.Part("shard_" + i, 0, 3.6f + i * .3f, 0);
                double a = i * 2.1;
                s.Path(new[] { (float)Math.Cos(a) * 2.2f, 3.6f + i * .3f, (float)Math.Sin(a) * 2.2f,
                    (float)Math.Cos(a) * 2.6f, 4.1f + i * .3f, (float)Math.Sin(a) * 2.6f }, new[] { .22f, .10f }, shard);
            }
            s.Light(0, 3.6f, 0, 1f, .46f, .16f, 22f, 4.4f);
            var d = s.Finish("Проксима-Пожиратель");
            ApplyLight(s, d);
            return d;
        }

        static SculptData KeplerGardener(int seed)
        {
            var s = new Sculptor(.15f, seed);
            int trunk = s.Pal("gardener trunk", "bark", .55f, .44f, .32f, 0);
            int leaf = s.Pal("emerald canopy", "leaves", .30f, .62f, .30f, 0);
            int bloom = s.Pal("dome bloom", "cherry-blossom", .90f, .60f, .70f, .4f);
            int glow = s.Pal("sap heart", "glow-shroom", .60f, 1f, .45f, 1.5f);
            int eye = s.Pal("gentle eye", "glow-shroom", .80f, 1f, .60f, 2.0f);
            int socket = s.Pal("socket", "obsidian", .07f, .08f, .06f, 0);
            // four trunk legs
            for (int i = 0; i < 4; i++)
            {
                double a = Math.PI / 4 + i * Math.PI / 2;
                float x = (float)Math.Cos(a), z = (float)Math.Sin(a);
                s.Part("leg_" + i, x * .8f, 2.4f, z * .8f);
                s.Path(new[] { x * .7f, 2.6f, z * .7f, x * 1.1f, 1.5f, z * 1.1f, x * 1.2f, .4f, z * 1.2f },
                    new[] { .42f, .34f, .40f }, trunk);
            }
            s.Part("body", 0, 0, 0);
            s.Path(new[] { 0f, 2.4f, 0f, 0f, 3.4f, -.1f, 0f, 4.4f, 0f }, new[] { .62f, .55f, .45f }, trunk);
            // dome canopy crown
            s.Shell(0, 4.9f, 0, 1.7f, 1.2f, 1.7f, .34f, leaf);
            for (int k = 0; k < 9; k++)
            {
                double a = k * Math.PI * 2 / 9;
                s.Ellipsoid((float)Math.Cos(a) * 1.3f, 5.2f + (k % 2) * .2f, (float)Math.Sin(a) * 1.3f, .34f, .26f, .34f, k % 3 == 0 ? bloom : leaf);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * .9f, 3.8f, 0);
                s.Path(new[] { side * .9f, 3.8f, 0f, side * 1.5f, 3.1f, .4f, side * 1.6f, 2.4f, .8f }, new[] { .34f, .26f, .22f }, trunk);
                for (int f = -1; f <= 1; f++)
                    s.Path(new[] { side * 1.6f, 2.4f, .8f, side * (1.7f + f * .08f), 2.0f, 1.0f + f * .14f }, new[] { .12f, .06f }, trunk);
            }
            s.Part("head", 0, 4.6f, .3f);
            s.Ellipsoid(0, 4.75f, .55f, .42f, .44f, .40f, trunk);
            s.Eyes(0, 4.85f, .88f, .18f, .09f, socket, eye, socket);
            s.Part("body", 0, 0, 0);
            s.Ellipsoid(0, 3.6f, .50f, .26f, .30f, .20f, glow);
            s.Cracks(glow, 18, 6, 1.0f, 4.4f);
            s.Light(0, 4.9f, 0, .60f, 1f, .45f, 14f, 2.6f);
            var d = s.Finish("Кеплер-Садовник");
            ApplyLight(s, d);
            return d;
        }

        static SculptData RadiaColossus(int seed)
        {
            var s = new Sculptor(.16f, seed);
            int glass = s.Pal("trinitite glass", "vitrified-golem-crust", .55f, .62f, .40f, .35f);
            int slag = s.Pal("slag plate", "slag-carapace", .32f, .34f, .26f, 0);
            int glow = s.Pal("ash heart", "cherenkov-crystal", .62f, 1f, .38f, 1.8f);
            int eye = s.Pal("gamma eye", "glow-shroom", .75f, 1f, .45f, 2.2f);
            int socket = s.Pal("socket", "obsidian", .08f, .09f, .07f, 0);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "leg_l" : "leg_r", side * 1.1f, 2.6f, 0);
                s.Path(new[] { side * 1.1f, 2.6f, 0f, side * 1.25f, 1.5f, .1f, side * 1.15f, .72f, 0f, side * 1.2f, .42f, .3f },
                    new[] { .58f, .50f, .44f, .45f }, slag);
            }
            s.Part("body", 0, 0, 0);
            // fused glass plates over a slag frame
            s.Ellipsoid(0, 3.0f, 0, 1.30f, .70f, .90f, slag);
            for (int i = 0; i < 5; i++)
            {
                float y = 3.4f + i * .55f;
                s.Box(0, y, -.05f, 1.50f - Math.Abs(i - 2) * .10f, .30f, .80f, i % 2 == 0 ? glass : slag);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.8f, 5.0f, 0);
                s.Ellipsoid(side * 1.8f, 5.0f, 0, .62f, .50f, .58f, glass);
                s.Path(new[] { side * 1.8f, 5.0f, 0f, side * 2.2f, 4.0f, .2f, side * 2.3f, 3.0f, .5f }, new[] { .44f, .36f, .30f }, slag);
                s.Box(side * 2.3f, 2.7f, .6f, .40f, .34f, .48f, glass);
            }
            // Vitrified spikes rising from the top plates between the torso and the arms.
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * 1.00f, 5.45f, .05f, side * 1.35f, 5.95f, -.02f, side * 1.50f, 6.45f, -.16f },
                    new[] { .13f, .09f, .04f }, glass);
            s.Part("head", 0, 6.1f, 0);
            s.Box(0, 6.3f, .1f, .66f, .58f, .60f, slag);
            s.Eyes(0, 6.42f, .64f, .28f, .12f, socket, eye, socket);
            s.Part("body", 0, 0, 0);
            s.Ellipsoid(0, 4.2f, .72f, .40f, .44f, .28f, glow);
            s.Cracks(glow, 44, 10, .8f, 6.0f);
            s.Light(0, 4.2f, 1.1f, .62f, 1f, .38f, 20f, 3.8f);
            var d = s.Finish("Тринититовый Колосс");
            ApplyLight(s, d);
            return d;
        }

        static SculptData BlackBacteria(int seed)
        {
            var s = new Sculptor(.14f, seed);
            int mass = s.Pal("entity mass", "wraith-shroud", .10f, .10f, .13f, 0);
            int carpet = s.Pal("stolen carpet", "backrooms-carpet", .55f, .50f, .30f, 0);
            int eye = s.Pal("many eyes", "glow-shroom", .80f, .85f, 1f, 2.3f);
            int wet = s.Pal("wet sheen", "obsidian", .16f, .16f, .20f, .2f);
            // amorphous hunched mass with tendrils
            s.Part("body", 0, 2.6f, 0);
            s.Ellipsoid(0, 2.6f, 0, 1.5f, 1.7f, 1.2f, mass);
            s.Ellipsoid(.4f, 3.6f, -.3f, .9f, .8f, .8f, mass);
            s.Ellipsoid(-.5f, 3.2f, .3f, .8f, .7f, .7f, mass);
            for (int k = 0; k < 12; k++)
            {
                double a = k * Math.PI * 2 / 12 + .3;
                float x = (float)Math.Cos(a), z = (float)Math.Sin(a);
                s.Path(new[] { x * 1.2f, 1.6f, z * 1.0f, x * 1.9f, .8f, z * 1.6f, x * 2.3f, .25f, z * 2.0f },
                    new[] { .24f, .14f, .06f }, mass);
            }
            // many eyes scattered over the mass
            for (int k = 0; k < 16; k++)
            {
                double a = k * 2.39996;
                float x = (float)(Math.Cos(a) * 1.35f), y = 2.4f + (float)Math.Sin(a * 1.7) * 1.1f, z = (float)(Math.Sin(a) * 1.05f);
                s.Ellipsoid(x, y, z, .09f, .11f, .07f, k % 4 == 0 ? wet : eye);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.3f, 3.2f, 0);
                s.Path(new[] { side * 1.3f, 3.2f, 0f, side * 1.9f, 2.6f, .5f, side * 2.0f, 1.8f, 1.0f }, new[] { .30f, .22f, .14f }, mass);
            }
            s.Part("head", 0, 4.2f, .2f);
            s.Ellipsoid(0, 4.3f, .3f, .55f, .50f, .50f, mass);
            s.Ellipsoid(.15f, 4.4f, .72f, .14f, .16f, .10f, eye);
            s.Ellipsoid(-.22f, 4.25f, .70f, .10f, .12f, .08f, eye);
            s.Part("body", 0, 0, 0);
            s.Ellipsoid(0, 3.4f, .95f, .5f, .3f, .2f, carpet);
            s.Part("body", 0, 0, 0);
            s.Cracks(wet, 20, 6, 1.0f, 4.2f);
            s.Light(0, 3.0f, .8f, .75f, .80f, .95f, 10f, 2.2f);
            var d = s.Finish("The Black Bacteria");
            ApplyLight(s, d);
            return d;
        }

        static SculptData CorruptedAI(int seed)
        {
            var s = new Sculptor(.14f, seed);
            int chassis = s.Pal("corrupt chassis", "generator-casing", .20f, .14f, .30f, 0);
            int glitch = s.Pal("glitch plate", "hazard-steel", .55f, .30f, .70f, .5f);
            int glow = s.Pal("corrupt core", "cherenkov-crystal", .85f, .35f, 1f, 2.0f);
            int eye = s.Pal("broken optic", "glow-shroom", 1f, .40f, .95f, 2.4f);
            ArchonRobe(s, chassis, glitch, 4.5f, 1.4f);
            s.Box(0, 4.3f, 0, .80f, .95f, .55f, chassis);
            for (int i = 0; i < 5; i++)
                s.Box((i % 2 == 0 ? .18f : -.18f), 3.7f + i * .32f, .52f, .40f, .06f, .05f, glitch);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.0f, 4.7f, 0);
                s.Path(new[] { side * 1.0f, 4.7f, 0f, side * 1.5f, 4.0f, .4f, side * 1.3f, 3.3f, .9f }, new[] { .20f, .15f, .11f }, chassis);
            }
            s.Part("head", 0, 5.4f, 0);
            s.Box(0, 5.6f, .05f, .40f, .46f, .38f, chassis);
            s.Ellipsoid(.10f, 5.68f, .42f, .10f, .06f, .05f, eye);
            s.Ellipsoid(-.16f, 5.52f, .42f, .06f, .10f, .05f, eye);
            s.Ring(0, 6.3f, 0, 1.0f, .06f, glow);
            s.Part("body", 0, 0, 0);
            s.Cracks(glow, 22, 6, 2.0f, 5.2f);
            s.Light(0, 4.3f, .7f, .85f, .35f, 1f, 12f, 3.0f);
            var d = s.Finish("The Corrupted AI");
            ApplyLight(s, d);
            return d;
        }

        static SculptData VoidHorror(int seed)
        {
            var s = new Sculptor(.15f, seed);
            int voidm = s.Pal("void flesh", "wraith-shroud", .09f, .08f, .12f, 0);
            int edge = s.Pal("event edge", "obsidian", .20f, .16f, .24f, .3f);
            int glow = s.Pal("void maw", "cherenkov-crystal", 1f, .25f, .35f, 1.9f);
            int eye = s.Pal("starved eye", "glow-shroom", 1f, .55f, .60f, 2.3f);
            ArchonRobe(s, voidm, edge, 4.6f, 1.6f);
            s.Ellipsoid(0, 4.4f, 0, .95f, 1.1f, .70f, voidm);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.1f, 4.9f, 0);
                s.Path(new[] { side * 1.1f, 4.9f, 0f, side * 1.7f, 4.1f, .3f, side * 1.9f, 3.2f, .8f, side * 1.7f, 2.5f, 1.2f },
                    new[] { .26f, .20f, .14f, .08f }, voidm);
            }
            // vertical maw with teeth
            s.Part("head", 0, 5.5f, .2f);
            s.Ellipsoid(0, 5.7f, .2f, .50f, .70f, .46f, voidm);
            s.Ellipsoid(0, 5.7f, .58f, .26f, .46f, .18f, glow);
            for (int i = -2; i <= 2; i++)
            {
                s.Ellipsoid(i * .12f, 6.05f - Math.Abs(i) * .04f, .62f, .05f, .10f, .05f, edge);
                s.Ellipsoid(i * .12f, 5.35f + Math.Abs(i) * .04f, .62f, .05f, .10f, .05f, edge);
            }
            s.Eyes(0, 6.25f, .48f, .22f, .08f, voidm, eye, voidm);
            s.Part("body", 0, 0, 0);
            s.Cracks(glow, 26, 7, 1.6f, 5.4f);
            s.Light(0, 5.7f, .8f, 1f, .25f, .35f, 12f, 3.0f);
            var d = s.Finish("The Void Horror");
            ApplyLight(s, d);
            return d;
        }


        /// <summary>Small liminal-hall stalker: the wiry black bacteria mob (not the boss).</summary>
        public static SculptData BacteriaMob(int seed)
        {
            var s = new Sculptor(.08f, seed);
            int black = s.Pal("bacteria mass", "wraith-shroud", .06f, .06f, .08f, 0);
            int wire = s.Pal("wire sinew", "obsidian", .16f, .16f, .20f, 0);
            int face = s.Pal("lure face", "bone-stalker-hide", .72f, .78f, .90f, .9f);
            int eye = s.Pal("lure eye", "glow-shroom", .55f, .62f, .80f, 2.0f);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "leg_l" : "leg_r", side * .16f, .6f, 0);
                s.Path(new[] { side * .16f, .62f, 0f, side * .20f, .30f, .04f, side * .22f, .06f, .14f },
                    new[] { .09f, .07f, .10f }, black);
                s.Part(side < 0 ? "arm_l" : "arm_r", side * .34f, 1.0f, .05f);
                s.Path(new[] { side * .34f, 1.0f, .05f, side * .38f, .5f, .10f, side * .36f, .12f, .16f },
                    new[] { .08f, .06f, .05f }, black);
                for (int f = -1; f <= 1; f++)
                    s.Path(new[] { side * .36f, .12f, .16f, side * (.38f + f * .02f), -.06f, .24f + f * .06f },
                        new[] { .04f, .02f }, wire);
            }
            s.Part("body", 0, 0, 0);
            s.Path(new[] { 0f, .30f, 0f, 0f, .70f, -.02f, 0f, 1.05f, .02f }, new[] { .26f, .22f, .20f }, black);
            s.Box(0, .55f, 0, .30f, .05f, .20f, wire);
            s.Box(0, .85f, 0, .28f, .05f, .19f, wire);
            s.Part("head", 0, 1.25f, .02f);
            s.Ellipsoid(0, 1.28f, .02f, .30f, .34f, .28f, black);
            s.Ellipsoid(0, 1.26f, .26f, .18f, .22f, .06f, face);
            s.Eyes(0, 1.32f, .30f, .09f, .045f, face, eye, face);
            s.Ellipsoid(0, 1.16f, .30f, .07f, .05f, .04f, eye);
            s.Light(0, 1.25f, .45f, .62f, .70f, .88f, 5f, 1.1f);
            var d = s.Finish("Entity 23");
            ApplyLight(s, d);
            return d;
        }

        // ---------- rank-and-file drones: small readable voxel machines ----------

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
                    s.Ellipsoid(0, 0, 0, .30f, .22f, .30f, hull);
                    s.Ellipsoid(0, .02f, .26f, .12f, .10f, .08f, glow);
                    for (int i = 0; i < 4; i++)
                    {
                        double a = Math.PI / 4 + i * Math.PI / 2;
                        float x = (float)Math.Cos(a) * .34f, z = (float)Math.Sin(a) * .34f;
                        s.Part("rotor_" + i, x, .18f, z);
                        s.Path(new[] { x * .4f, .16f, z * .4f, x, .20f, z }, new[] { .05f, .04f }, dark);
                        s.Box(x, .23f, z, .16f, .015f, .05f, plate);
                    }
                    s.Part("body", 0, 0, 0);
                    break;
                case DroneKind.Combat:
                    s.Ellipsoid(0, 0, 0, .34f, .20f, .42f, hull);
                    s.Ellipsoid(0, .02f, .40f, .10f, .08f, .08f, glow);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        s.Part(side < 0 ? "wing_l" : "wing_r", side * .3f, .02f, 0);
                        s.Path(new[] { side * .3f, .02f, 0f, side * .72f, .10f, -.12f }, new[] { .08f, .16f }, plate);
                        s.Path(new[] { side * .60f, .04f, .10f, side * .66f, .04f, .42f }, new[] { .04f, .03f }, dark);
                    }
                    s.Part("body", 0, 0, 0);
                    s.Ellipsoid(0, -.04f, -.40f, .10f, .08f, .06f, hot);
                    break;
                case DroneKind.Repair:
                    s.Ellipsoid(0, .05f, 0, .28f, .24f, .28f, hull);
                    s.Ellipsoid(0, .06f, .24f, .10f, .09f, .07f, glow);
                    s.Part("arm_r", .26f, 0, .1f);
                    s.Path(new[] { .26f, 0, .1f, .42f, -.20f, .3f, .46f, -.40f, .46f }, new[] { .06f, .05f, .04f }, plate);
                    s.Ellipsoid(.46f, -.44f, .48f, .05f, .05f, .05f, hot);
                    s.Part("body", 0, 0, 0);
                    s.Ring(0, .32f, 0, .22f, .03f, glow);
                    break;
                case DroneKind.Ground:
                    for (int side = -1; side <= 1; side += 2)
                    {
                        s.Part(side < 0 ? "leg_l" : "leg_r", side * .16f, .6f, 0);
                        s.Path(new[] { side * .16f, .62f, 0f, side * .22f, .3f, .04f, side * .24f, .06f, .12f }, new[] { .09f, .07f, .10f }, hull);
                    }
                    s.Part("body", 0, 0, 0);
                    s.Box(0, .82f, 0, .26f, .24f, .20f, plate);
                    s.Part("turret", 0, 1.12f, .02f);
                    s.Box(0, 1.12f, .02f, .18f, .16f, .16f, hull);
                    s.Ellipsoid(0, 1.14f, .18f, .08f, .06f, .05f, glow);
                    s.Part("body", 0, 0, 0);
                    break;
                case DroneKind.Heavy:
                    for (int i = 0; i < 4; i++)
                    {
                        double a = Math.PI / 4 + i * Math.PI / 2;
                        float x = (float)Math.Cos(a), z = (float)Math.Sin(a);
                        s.Part("leg_" + i, x * .3f, .7f, z * .3f);
                        s.Path(new[] { x * .28f, .72f, z * .28f, x * .48f, .38f, z * .48f, x * .52f, .08f, z * .52f }, new[] { .11f, .08f, .12f }, hull);
                    }
                    s.Part("body", 0, 0, 0);
                    s.Ellipsoid(0, .95f, 0, .42f, .30f, .36f, plate);
                    s.Box(0, 1.28f, .10f, .26f, .18f, .22f, hull);
                    s.Ellipsoid(0, 1.30f, .32f, .10f, .07f, .06f, glow);
                    for (int side = -1; side <= 1; side += 2)
                        s.Path(new[] { side * .34f, 1.24f, .18f, side * .40f, 1.22f, .52f }, new[] { .06f, .05f }, dark);
                    break;
                case DroneKind.Guard:
                    s.Ellipsoid(0, 0, 0, .30f, .26f, .26f, hull);
                    s.Part("shield", 0, 0, .3f);
                    s.Shell(0, 0, .34f, .34f, .40f, .12f, .06f, plate);
                    s.Part("body", 0, 0, 0);
                    s.Ellipsoid(0, .04f, .26f, .10f, .08f, .06f, glow);
                    s.Ring(0, .32f, 0, .24f, .03f, glow);
                    break;
                default: // Builder
                    s.Ellipsoid(0, 0, 0, .30f, .24f, .30f, hull);
                    s.Ellipsoid(0, .02f, .26f, .11f, .09f, .07f, glow);
                    s.Part("arm_r", .28f, .06f, 0);
                    s.Path(new[] { .28f, .06f, 0f, .50f, .26f, .16f, .62f, 0f, .40f }, new[] { .07f, .06f, .05f }, plate);
                    s.Box(.64f, -.08f, .44f, .08f, .08f, .10f, hot);
                    s.Part("arm_l", -.28f, .06f, 0);
                    s.Path(new[] { -.28f, .06f, 0f, -.46f, -.14f, .22f }, new[] { .06f, .05f }, plate);
                    s.Part("body", 0, 0, 0);
                    break;
            }
            return s.Finish("Drone " + kind);
        }
    }
}
