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
            return ((long)(uint)(x + 1024) << 40) | ((long)(uint)(y + 1024) << 20) | (uint)(z + 1024);
        }

        void Put(int x, int y, int z, int mat)
        {
            maps[current][Key(x, y, z)] = mat;
        }

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

        /// <summary>Tapered capsule segment between two 3D points — crisp limbs, horns, ribs and spines.</summary>
        public void Tube(float x0, float y0, float z0, float x1, float y1, float z1, float r0, float r1, int mat)
        {
            float dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
            float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            int steps = Math.Max(2, (int)Math.Ceiling(len / (unit * .42f)));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float r = r0 + (r1 - r0) * t;
                Ellipsoid(x0 + dx * t, y0 + dy * t, z0 + dz * t, r, r, r, mat);
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

        public void Ring(float cx, float cy, float cz, float radius, float tube, int mat)
        {
            int steps = Math.Max(16, (int)(radius / unit * 2.2f));
            for (int i = 0; i < steps; i++)
            {
                double a = i * Math.PI * 2 / steps;
                Ellipsoid(cx + (float)Math.Cos(a) * radius, cy, cz + (float)Math.Sin(a) * radius, tube, tube, tube, mat);
            }
        }

        public void RingVertical(float cx, float cy, float cz, float radius, float tube, int mat)
        {
            int steps = Math.Max(16, (int)(radius / unit * 2.2f));
            for (int i = 0; i < steps; i++)
            {
                double a = i * Math.PI * 2 / steps;
                Ellipsoid(cx + (float)Math.Cos(a) * radius, cy + (float)Math.Sin(a) * radius, cz, tube, tube, tube, mat);
            }
        }

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
                        long inner = Key(x - dx, y, z - dz);
                        if (maps[current].ContainsKey(inner)) maps[current][inner] = mat;
                    }
                }
            }
        }

        public void Eyes(float hx, float hy, float hz, float sep, float r, int sclera, int iris, int pupil)
        {
            int glint = Pal("eye glint", "glow-shroom", 1f, .97f, .88f, 2.6f);
            for (int side = -1; side <= 1; side += 2)
            {
                Ellipsoid(side * sep, hy, hz - r * .38f, r * 1.12f, r * .82f, r * .52f, pupil);
                Ellipsoid(side * sep + hx * 0, hy, hz, r, r, r * .7f, sclera);
                Ellipsoid(side * sep, hy, hz + r * .55f, r * .62f, r * .62f, r * .5f, iris);
                Ellipsoid(side * sep, hy, hz + r * .95f, r * .28f, r * .28f, r * .3f, pupil);
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
                    if (wx < minX) minX = wx; if (wx + unit > maxX) maxX = wx + unit;
                    if (wy < minY) minY = wy; if (wy + unit > maxY) maxY = wy + unit;
                    if (wz < minZ) minZ = wz; if (wz + unit > maxZ) maxZ = wz + unit;
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
            if (hasLight)
            {
                data.LightX = lightX; data.LightY = lightY; data.LightZ = lightZ;
                data.LightR = lightR; data.LightG = lightG; data.LightB = lightB;
                data.LightRange = lightRange; data.LightIntensity = lightIntensity;
            }
            return data;
        }

        public void Light(float x, float y, float z, float r, float g, float b, float range, float intensity)
        {
            lightX = x; lightY = y; lightZ = z; lightR = r; lightG = g; lightB = b; lightRange = range; lightIntensity = intensity;
            hasLight = true;
        }

        bool hasLight;
        float lightX, lightY, lightZ, lightR, lightG, lightB, lightRange, lightIntensity;
    }

    /// <summary>
    /// Fine-voxel organic and mechanical planetary guardians, extra bosses, drones and Backrooms stalker.
    /// Every world guardian has a distinct silhouette, layered PBR armour and emissive weak points.
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
                case Dimension.Inferno: return InfernalArchfiend(seed);
                default: return Warden(seed, false);
            }
        }

        public static SculptData Extra(string name, int seed)
        {
            if (name == "The Corrupted AI") return CorruptedAI(seed);
            if (name == "The Void Horror") return VoidHorror(seed);
            return Warden(seed, true);
        }

        static void DigitigradeLeg(Sculptor s, float side, float hipX, float hipY, float scale, int plate, int joint)
        {
            s.Path(new[] {
                side * hipX, hipY, 0f,
                side * (hipX + .26f * scale), hipY - .76f * scale, .32f * scale,
                side * (hipX + .32f * scale), hipY - 1.56f * scale, -.38f * scale,
                side * (hipX + .34f * scale), .28f * scale, .18f * scale,
                side * (hipX + .38f * scale), .14f * scale, .58f * scale },
                new[] { .32f * scale, .28f * scale, .22f * scale, .19f * scale, .24f * scale }, plate);
            s.Ellipsoid(side * (hipX + .26f * scale), hipY - .76f * scale, .32f * scale, .28f * scale, .28f * scale, .28f * scale, joint);
            s.Ellipsoid(side * (hipX + .32f * scale), hipY - 1.56f * scale, -.38f * scale, .22f * scale, .22f * scale, .22f * scale, joint);
        }

        static void RootLeg(Sculptor s, float side, int bark, int root)
        {
            s.Part(side < 0 ? "leg_l" : "leg_r", side * .52f, 2.25f, -.08f);
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
                    side * (.79f + j * .22f), .10f, .96f - Math.Abs(j) * .11f },
                    new[] { .18f, .15f, .06f }, bark);
            }
        }

        static void ArchonRobe(Sculptor s, int cloth, int trim, float topY, float width)
        {
            s.Part("body", 0, 0, 0);
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                float y = topY - t * 2.55f;
                float w = width * (1f - t * .68f);
                s.Ellipsoid(0, y, 0, w, .26f, w * .82f, i % 2 == 0 ? cloth : trim);
            }
            for (int k = 0; k < 8; k++)
            {
                double a = k * Math.PI * 2 / 8 + .35;
                float x = (float)Math.Cos(a) * width * .48f, z = (float)Math.Sin(a) * width * .44f;
                s.Path(new[] { x, topY - 2.35f, z, x * 1.15f, topY - 3.05f, z * 1.15f, x * .92f, topY - 3.65f, z * .92f },
                    new[] { .15f, .10f, .055f }, cloth);
            }
            s.Ring(0, topY + .28f, 0, width * .84f, .065f, trim);
        }

        // ---------- 1. Lestingia: Ancient Root Warden ----------

        public static SculptData Warden(int seed, bool variant)
        {
            var s = new Sculptor(.11f, seed);
            int bark = s.Pal("old outer bark", "bark", .76f, .67f, .54f, 0);
            int grain = s.Pal("inner root grain", "bark", .98f, .86f, .66f, 0);
            int carved = s.Pal("petrified carved wood", "mossy-stone", .94f, .86f, .68f, 0);
            int moss = s.Pal("moss in crevices", "mossy-stone", .56f, .82f, .44f, 0);
            int heart = s.Pal("amber living heart", "glow-shroom", 1f, .36f, .06f, 1.5f);
            int eye = s.Pal("ember eye", "glow-shroom", 1f, .68f, .18f, 2.2f);
            int pupil = s.Pal("hollow socket", "obsidian", .10f, .07f, .05f, 0);
            int charred = s.Pal("charred fissures", "bark", .32f, .27f, .22f, 0);

            for (int side = -1; side <= 1; side += 2) RootLeg(s, side, bark, grain);
            s.Part("body", 0, 0, 0);
            s.Path(new[] { -.55f, 2.12f, 0f, 0f, 2.27f, .05f, .55f, 2.12f, 0f }, new[] { .32f, .36f, .32f }, bark);
            s.Path(new[] { 0f, 2.10f, -.13f, -.06f, 2.85f, -.38f, .06f, 3.65f, -.38f, 0f, 4.30f, -.08f, 0f, 4.58f, 0f },
                new[] { .30f, .34f, .40f, .32f, .24f }, bark);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Path(new[] { side * .44f, 2.15f, 0f, side * .86f, 2.68f, .14f, side * 1.04f, 3.42f, .02f, side * .90f, 3.98f, -.04f, side * .32f, 4.28f, 0f },
                    new[] { .26f, .32f, .38f, .42f, .24f }, bark);
                for (int j = 0; j < 3; j++)
                {
                    float y = 3.78f - j * .36f;
                    s.Path(new[] { side * .12f, y, -.34f, side * .70f, y + .06f, .10f, side * .84f, y - .04f, .48f, side * .52f, y - .26f, .78f },
                        new[] { .15f, .18f, .17f, .10f }, j == 1 ? grain : bark);
                }
                s.Part(side < 0 ? "arm_l" : "arm_r", side * .95f, 3.85f, -.02f);
                s.Ellipsoid(side * 1.02f, 4.02f, -.02f, .38f, .28f, .36f, moss);
                s.Path(new[] { side * .95f, 3.85f, -.02f, side * 1.28f, 3.25f, .06f, side * 1.48f, 2.52f, .16f, side * 1.54f, 1.82f, .32f },
                    new[] { .36f, .34f, .28f, .22f }, bark);
                for (int f = -1; f <= 1; f++)
                    s.Path(new[] { side * 1.54f, 1.82f, .32f, side * (1.60f + f * .07f), 1.44f, .50f + f * .11f, side * (1.62f + f * .09f), 1.16f, .68f + f * .16f },
                        new[] { .13f, .09f, .05f }, grain);
                s.Part("body", 0, 0, 0);
            }

            s.Ellipsoid(0, 3.58f, .28f, .32f, .36f, .26f, heart);
            s.Ellipsoid(0, 3.58f, .46f, .17f, .20f, .13f, eye);

            s.Part("head", 0, 4.58f, 0);
            s.Ellipsoid(0, 4.88f, .06f, .36f, .46f, .35f, carved);
            s.Ellipsoid(0, 4.62f, .30f, .22f, .24f, .18f, carved);
            s.Eyes(0, 4.94f, .35f, .16f, .10f, pupil, eye, pupil);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Path(new[] { side * .18f, 5.26f, 0f, side * .44f, 5.72f, -.08f, side * .72f, 6.12f, .06f, side * .94f, 6.42f, -.10f },
                    new[] { .13f, .10f, .075f, .045f }, bark);
                s.Path(new[] { side * .44f, 5.72f, -.08f, side * .56f, 6.02f, .24f }, new[] { .075f, .045f }, grain);
            }
            s.Part("body", 0, 0, 0);
            s.Cracks(charred, 28, 7, .4f, 4.4f);
            s.Cracks(moss, 22, 6, .4f, 4.1f);
            s.Light(0, 3.58f, .60f, 1f, .42f, .08f, 8f, 2.8f);
            return s.Finish(variant ? "Elder Root Warden" : "Ancient Guardian");
        }

        // ---------- 2. Mars: Four-Armed Obsidian-Basalt Forge Colossus ----------

        static SculptData MarsColossus(int seed)
        {
            var s = new Sculptor(.12f, seed);
            int basalt = s.Pal("forged basalt", "basalt", .58f, .30f, .24f, 0);
            int dark = s.Pal("volcanic iron", "obsidian", .22f, .14f, .13f, 0);
            int crack = s.Pal("magma fissure", "magma-basalt", 1f, .40f, .10f, 1.6f);
            int obs = s.Pal("obsidian horn", "obsidian", .10f, .08f, .09f, 0);
            int core = s.Pal("forge furnace", "lava", 1f, .54f, .14f, 2.3f);
            int eye = s.Pal("molten visor", "lava", 1f, .68f, .20f, 2.4f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "leg_l" : "leg_r", side * 1.05f, 2.5f, 0);
                s.Path(new[] { side * 1.05f, 2.55f, 0f, side * 1.22f, 1.55f, .16f, side * 1.12f, .65f, -.08f, side * 1.16f, .18f, .28f },
                    new[] { .54f, .46f, .38f, .44f }, basalt);
                s.Ellipsoid(side * 1.22f, 1.55f, .24f, .36f, .32f, .34f, dark);
                for (int t = -1; t <= 1; t++)
                    s.Tube(side * 1.16f + t * .16f, .18f, .36f, side * 1.18f + t * .20f, .08f, .72f, .13f, .07f, obs);
            }

            s.Part("body", 0, 0, 0);
            s.Ellipsoid(0, 2.78f, 0, 1.18f, .56f, .82f, dark);
            s.Ellipsoid(0, 4.45f, -.06f, 1.48f, 1.38f, .92f, basalt);
            // Recessed blazing forge furnace framed by volcanic ribs!
            s.Ellipsoid(0, 4.55f, .48f, .66f, .78f, .46f, core);
            for (int r = 0; r < 4; r++)
            {
                float ry = 3.65f + r * .52f;
                for (int side = -1; side <= 1; side += 2)
                    s.Path(new[] { side * 1.35f, ry + .10f, .10f, side * .92f, ry + .04f, .68f, side * .32f, ry - .08f, .86f },
                        new[] { .20f, .16f, .11f }, r % 2 == 0 ? dark : basalt);
            }
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * 1.28f, 5.55f, .05f, side * 1.75f, 6.20f, -.10f, side * 2.05f, 6.85f, -.32f },
                    new[] { .22f, .14f, .06f }, obs);

            // Upper massive war-arms + lower secondary arms
            for (int pair = 0; pair < 2; pair++)
            {
                float ay = pair == 0 ? 5.40f : 4.15f;
                float ax = pair == 0 ? 1.95f : 1.55f;
                float ar = pair == 0 ? .48f : .34f;
                for (int side = -1; side <= 1; side += 2)
                {
                    string nm = (pair == 0 ? "arm_" : "arm2_") + (side < 0 ? "l" : "r");
                    s.Part(nm, side * ax, ay, 0);
                    s.Ellipsoid(side * ax, ay + .12f, 0, ar * 1.45f, ar * 1.15f, ar * 1.35f, dark);
                    s.Path(new[] { side * ax, ay, .08f, side * (ax + .48f), ay - .92f, .38f, side * (ax + .54f), ay - 1.88f, .72f },
                        new[] { ar, ar * .84f, ar * .78f }, basalt);
                    s.Ellipsoid(side * (ax + .54f), ay - 2.18f, .82f, ar * .95f, ar * .85f, ar * 1.05f, dark);
                    for (int f = -1; f <= 1; f++)
                        s.Tube(side * (ax + .54f), ay - 2.40f, .84f + f * .18f, side * (ax + .50f), ay - 2.86f, 1.08f + f * .22f, .12f, .06f, obs);
                }
            }

            s.Part("head", 0, 6.05f, .08f);
            s.Ellipsoid(0, 6.45f, .14f, .68f, .62f, .64f, dark);
            s.Box(0, 6.48f, .68f, .46f, .11f, .12f, eye);
            s.Eyes(0, 6.52f, .70f, .26f, .11f, obs, eye, obs);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Path(new[] { side * .48f, 6.85f, .06f, side * .98f, 7.60f, -.12f, side * 1.28f, 8.35f, -.38f },
                    new[] { .22f, .15f, .07f }, obs);
                s.Tube(side * .34f, 6.12f, .58f, side * .46f, 6.38f, .86f, .10f, .05f, obs);
            }

            s.Part("body", 0, 0, 0);
            s.Cracks(crack, 48, 9, .8f, 6.0f);
            s.Light(0, 4.55f, 1.1f, 1f, .44f, .12f, 20f, 4.0f);
            return s.Finish("Red Colossus");
        }

        // ---------- 3. Venus: Six-Legged Acid Storm Mantis ----------

        static SculptData VenusPredator(int seed)
        {
            var s = new Sculptor(.11f, seed);
            int chitin = s.Pal("storm chitin", "rad-scorpion-carapace", .36f, .44f, .52f, 0);
            int plate = s.Pal("cut plate", "scorpion-chitin", .22f, .28f, .36f, 0);
            int acid = s.Pal("acid tank", "cherenkov-crystal", .52f, .96f, .42f, .8f);
            int acidHot = s.Pal("acid core", "cherenkov-crystal", .82f, 1f, .52f, 1.8f);
            int eye = s.Pal("cluster eye", "glow-shroom", .85f, 1f, .48f, 2.2f);
            int socket = s.Pal("socket", "obsidian", .07f, .08f, .10f, 0);
            int tube = s.Pal("feed tube", "copper-cable", .58f, .50f, .30f, 0);

            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 3; i++)
                {
                    float z = -.95f + i * .92f;
                    s.Part("leg_" + (side < 0 ? "l" : "r") + i, side * .95f, 1.95f, z);
                    s.Path(new[] { side * .88f, 1.95f, z, side * 1.88f, 2.38f, z + .18f, side * 2.58f, 1.18f, z + .32f, side * 2.86f, .12f, z + .08f },
                        new[] { .34f, .28f, .21f, .11f }, chitin);
                    s.Tube(side * 1.88f, 2.38f, z + .18f, side * 2.08f, 2.76f, z + .10f, .12f, .05f, plate);
                }

            s.Part("body", 0, 0, 0);
            for (int i = 0; i < 6; i++)
            {
                float z = -1.35f + i * .54f;
                s.Ellipsoid(0, 2.12f - Math.Abs(i - 2.5f) * .05f, z, 1.12f - Math.Abs(i - 2.5f) * .09f, .72f, .42f, i % 2 == 0 ? chitin : plate);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                s.Ellipsoid(side * .54f, 2.86f, -.32f, .34f, .42f, .58f, acid);
                s.Ellipsoid(side * .54f, 2.95f, -.32f, .18f, .24f, .34f, acidHot);
                s.Path(new[] { side * .54f, 2.60f, .28f, side * .36f, 2.34f, .70f, side * .18f, 2.20f, 1.05f }, new[] { .09f, .075f, .06f }, tube);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * .98f, 2.28f, 1.28f);
                s.Path(new[] { side * .92f, 2.28f, 1.22f, side * 1.52f, 2.68f, 1.88f, side * 1.66f, 3.24f, 2.28f },
                    new[] { .30f, .25f, .20f }, chitin);
                s.Path(new[] { side * 1.66f, 3.24f, 2.28f, side * 1.42f, 3.82f, 2.85f, side * .82f, 3.45f, 3.12f, side * .35f, 2.65f, 2.95f },
                    new[] { .19f, .15f, .10f, .05f }, acidHot);
            }

            s.Part("head", 0, 2.18f, 1.68f);
            s.Ellipsoid(0, 2.24f, 1.76f, .58f, .46f, .56f, chitin);
            s.Eyes(0, 2.40f, 2.18f, .28f, .10f, socket, eye, socket);
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * .32f, 1.96f, 2.28f, side * .52f, 1.78f, 2.68f, side * .28f, 1.65f, 2.98f },
                    new[] { .11f, .08f, .045f }, plate);

            s.Part("body", 0, 0, 0);
            s.Cracks(socket, 24, 6, 1.2f, 3.1f);
            s.Light(0, 2.95f, -.32f, .62f, 1f, .45f, 14f, 3.2f);
            return s.Finish("The Storm Herald");
        }

        // ---------- 4. Mercury: Solar Mirror Scarab ----------

        static SculptData MercuryScarab(int seed)
        {
            var s = new Sculptor(.105f, seed);
            int hull = s.Pal("titanium hull", "titanium-hull", .66f, .70f, .76f, 0);
            int dark = s.Pal("shadow housing", "generator-casing", .22f, .24f, .28f, 0);
            int gold = s.Pal("solar foil", "ore-gold", .94f, .72f, .26f, 0);
            int core = s.Pal("solar core", "lava", 1f, .58f, .16f, 2.3f);
            int lens = s.Pal("mirror vane", "titanium-hull", .95f, .98f, 1.0f, .25f);
            int eye = s.Pal("optic", "cherenkov-crystal", 1f, .78f, .32f, 2.1f);

            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 3; i++)
                {
                    float z = -.82f + i * .82f;
                    s.Part("leg_" + (side < 0 ? "l" : "r") + i, side * .92f, 1.02f, z);
                    s.Path(new[] { side * .84f, 1.02f, z, side * 1.54f, .94f, z + .10f, side * 1.88f, .28f, z + .18f, side * 2.10f, .10f, z },
                        new[] { .25f, .21f, .16f, .19f }, dark);
                    s.Ellipsoid(side * 1.54f, .94f, z + .10f, .20f, .20f, .20f, gold);
                }

            s.Part("body", 0, 0, 0);
            s.Ellipsoid(0, 1.88f, 0, 1.42f, 1.02f, 1.52f, hull);
            for (int i = 0; i < 4; i++)
                s.Box(0, 2.48f + i * .09f, -.88f + i * .60f, 1.24f - i * .10f, .10f, .32f, i % 2 == 0 ? gold : dark);
            s.Ellipsoid(0, 1.48f, 0, 1.18f, .56f, 1.28f, dark);
            s.Ellipsoid(0, 1.76f, 1.18f, .52f, .44f, .36f, core);

            for (int side = -1; side <= 1; side += 2)
                for (int v = 0; v < 3; v++)
                {
                    s.Part("vane_" + (side < 0 ? "l" : "r") + v, side * .60f, 2.12f, -.88f);
                    float spread = .48f + v * .56f;
                    float lift = .58f - v * .12f;
                    for (int seg = 0; seg < 5; seg++)
                    {
                        float t = seg / 4f;
                        float x = side * (.55f + spread * t);
                        float y = 2.12f + lift * t + .16f * (float)Math.Sin(t * 3f);
                        float z = -.92f - .96f * t;
                        float w = .28f + .32f * t;
                        s.Ellipsoid(x, y, z, w * .56f, .05f + .025f * t, w, v == 1 ? gold : lens);
                    }
                }

            s.Part("head", 0, 1.92f, 1.48f);
            s.Ellipsoid(0, 1.94f, 1.56f, .58f, .40f, .44f, dark);
            for (int i = -1; i <= 1; i++)
                s.Ellipsoid(i * .26f, 2.06f, 1.92f, .09f, .09f, .07f, eye);
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * .44f, 1.66f, 1.78f, side * .72f, 1.38f, 2.22f, side * .48f, 1.20f, 2.58f },
                    new[] { .13f, .09f, .05f }, gold);

            s.Light(0, 1.80f, 1.30f, 1f, .56f, .18f, 16f, 3.6f);
            return s.Finish("Solar Core Guardian");
        }

        // ---------- 5. Moon: Lunar Regolith & Sapphire Cryo-Titan ----------

        static SculptData MoonTitan(int seed)
        {
            var s = new Sculptor(.115f, seed);
            int ice = s.Pal("lunar anorthosite", "glacial-ice", .82f, .86f, .94f, 0);
            int rock = s.Pal("mare basalt", "stone", .42f, .44f, .50f, 0);
            int glow = s.Pal("lunar sapphire core", "cherenkov-crystal", .48f, .82f, 1f, 1.8f);
            int eye = s.Pal("lunar visor", "cherenkov-crystal", .78f, .94f, 1f, 2.2f);
            int socket = s.Pal("socket", "obsidian", .08f, .09f, .12f, 0);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "leg_l" : "leg_r", side * .98f, 2.55f, 0);
                DigitigradeLeg(s, side, .98f, 2.55f, 1.12f, ice, rock);
            }

            s.Part("body", 0, 0, 0);
            s.Ellipsoid(0, 2.95f, 0, 1.18f, .68f, .86f, rock);
            s.Ellipsoid(0, 4.18f, -.04f, 1.38f, 1.05f, .88f, ice);
            for (int i = 0; i < 3; i++)
                s.Box(0, 3.55f + i * .52f, .56f - i * .04f, 1.08f - i * .08f, .18f, .24f, rock);
            s.Ellipsoid(0, 4.18f, .68f, .38f, .44f, .26f, glow);
            // Lunar crescent halo behind shoulders
            s.RingVertical(0, 5.35f, -.48f, 1.35f, .10f, glow);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.52f, 4.45f, 0);
                s.Ellipsoid(side * 1.56f, 4.62f, 0, .52f, .42f, .48f, rock);
                s.Tube(side * 1.56f, 4.92f, -.04f, side * 1.88f, 5.55f, -.22f, .14f, .06f, ice);
                s.Path(new[] { side * 1.52f, 4.45f, 0f, side * 1.92f, 3.45f, .22f, side * 2.02f, 2.42f, .54f },
                    new[] { .38f, .32f, .28f }, ice);
                s.Box(side * 2.02f, 2.08f, .62f, .32f, .30f, .38f, rock);
                for (int f = -1; f <= 1; f++)
                    s.Tube(side * 2.02f, 1.82f, .64f + f * .14f, side * 1.96f, 1.46f, .82f + f * .16f, .09f, .05f, glow);
            }

            s.Part("head", 0, 5.15f, .04f);
            s.Ellipsoid(0, 5.45f, .12f, .56f, .52f, .54f, ice);
            s.Eyes(0, 5.48f, .58f, .24f, .10f, socket, eye, socket);
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * .38f, 5.82f, .04f, side * .68f, 6.32f, -.18f }, new[] { .13f, .055f }, rock);

            s.Part("body", 0, 0, 0);
            s.Cracks(glow, 26, 7, 1.2f, 5.0f);
            s.Light(0, 4.18f, 1.0f, .58f, .84f, 1f, 18f, 3.4f);
            return s.Finish("Лунный Титанит");
        }

        // ---------- 6. Europa: Abyssal Glacial Carapace ----------

        static SculptData EuropaShell(int seed)
        {
            var s = new Sculptor(.115f, seed);
            int shell = s.Pal("cracked shell", "glacial-ice", .72f, .86f, .94f, 0);
            int deep = s.Pal("deep ice", "glacial-ice", .38f, .60f, .82f, 0);
            int vein = s.Pal("brine vein", "cherenkov-crystal", .42f, .88f, 1f, 1.7f);
            int eye = s.Pal("abyss eye", "cherenkov-crystal", .58f, .94f, 1f, 2.2f);
            int socket = s.Pal("socket", "obsidian", .06f, .08f, .12f, 0);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "leg_l" : "leg_r", side * 1.08f, 2.4f, 0);
                DigitigradeLeg(s, side, 1.08f, 2.4f, 1.05f, shell, deep);
            }
            s.Part("body", 0, 0, 0);
            s.Shell(0, 3.45f, -.22f, 1.85f, 1.45f, 1.48f, .36f, shell);
            s.Ellipsoid(0, 3.22f, 0, 1.26f, 1.02f, 1.02f, deep);
            for (int i = 0; i < 3; i++)
                s.Box(0, 3.0f + i * .52f, .84f - i * .10f, 1.05f - i * .12f, .22f, .28f, shell);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.56f, 3.58f, .20f);
                s.Path(new[] { side * 1.56f, 3.58f, .20f, side * 1.98f, 2.68f, .52f, side * 2.08f, 1.88f, .82f },
                    new[] { .38f, .30f, .24f }, shell);
                s.Path(new[] { side * 2.08f, 1.88f, .82f, side * 2.18f, 1.46f, 1.34f, side * 2.04f, 1.18f, 1.86f },
                    new[] { .18f, .12f, .055f }, vein);
            }

            s.Part("head", 0, 4.42f, .92f);
            s.Ellipsoid(0, 4.54f, 1.05f, .54f, .46f, .50f, deep);
            s.Ellipsoid(0, 4.62f, 1.40f, .34f, .28f, .22f, shell);
            s.Eyes(0, 4.66f, 1.45f, .23f, .10f, socket, eye, socket);

            s.Part("body", 0, 0, 0);
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 3; k++)
                {
                    float y = 3.55f + k * .46f;
                    s.Tube(side * (.65f + k * .16f), y, -1.20f, side * (.92f + k * .22f), y + .78f, -1.52f, .13f, .05f, k == 1 ? vein : deep);
                }
            s.Cracks(vein, 42, 9, 1.2f, 4.9f);
            s.Light(0, 3.6f, .9f, .44f, .86f, 1f, 16f, 3.0f);
            return s.Finish("Ледяной Панцирь Европы");
        }

        // ---------- 7. Titan: Ringed Abyssal Leviathan ----------

        static SculptData RingedLeviathan(int seed)
        {
            var s = new Sculptor(.115f, seed);
            int hide = s.Pal("leviathan hide", "voidray-skin", .26f, .22f, .48f, 0);
            int belly = s.Pal("methane plates", "glacial-ice", .42f, .68f, .78f, 0);
            int ring = s.Pal("saturn ring fin", "andromeda-crystal", .92f, .68f, .28f, 1.1f);
            int glow = s.Pal("throat light", "cherenkov-crystal", .68f, .48f, 1f, 1.8f);
            int eye = s.Pal("deep eye", "glow-shroom", 1f, .78f, .32f, 2.2f);
            int socket = s.Pal("socket", "obsidian", .08f, .06f, .10f, 0);

            s.Part("body", 0, 3.2f, 0);
            for (int i = 0; i < 11; i++)
            {
                float t = i / 10f;
                double a = t * Math.PI * 1.75;
                float x = (float)Math.Cos(a) * .78f * (1f - t * .35f);
                float z = (float)Math.Sin(a) * .78f * (1f - t * .35f);
                float y = .75f + t * 4.75f;
                s.Ellipsoid(x, y, z, .92f - t * .30f, .48f, .84f - t * .28f, i % 3 == 0 ? belly : hide);
                if (i % 2 == 1) s.Ring(x, y, z, 1.06f - t * .32f, .065f, ring);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "wing_l" : "wing_r", side * .88f, 4.35f, 0);
                s.Path(new[] { side * .80f, 4.35f, .08f, side * 1.85f, 4.72f, -.24f, side * 2.75f, 4.42f, -.62f },
                    new[] { .24f, .28f, .11f }, hide);
                s.Tube(side * .88f, 4.46f, .12f, side * 2.68f, 4.50f, -.56f, .08f, .04f, ring);
            }

            s.Part("head", 0, 5.75f, 0);
            s.Ellipsoid(0, 5.95f, .32f, .72f, .58f, .80f, hide);
            s.Ellipsoid(0, 5.70f, .94f, .44f, .28f, .34f, glow);
            s.Eyes(0, 6.10f, .92f, .32f, .11f, socket, eye, socket);
            s.Ring(0, 6.82f, 0, 1.62f, .085f, ring);

            s.Part("body", 0, 3.2f, 0);
            s.Cracks(glow, 24, 6, 1.2f, 5.5f);
            s.Light(0, 5.7f, 1.0f, .75f, .48f, 1f, 14f, 3.0f);
            return s.Finish("Ringed Leviathan");
        }

        // ---------- 8. Pluto: Nitrogen-Shell Cryo Hermit ----------

        static SculptData PlutoHermit(int seed)
        {
            var s = new Sculptor(.11f, seed);
            int crust = s.Pal("nitrogen crust", "snow", .84f, .88f, .94f, 0);
            int rock = s.Pal("tholin rock", "red-mesa", .48f, .32f, .28f, 0);
            int glow = s.Pal("faint heart", "cherenkov-crystal", .68f, .88f, 1f, 1.4f);
            int eye = s.Pal("old eye", "glow-shroom", .88f, .95f, 1f, 2.0f);
            int socket = s.Pal("socket", "obsidian", .07f, .07f, .09f, 0);

            s.Path(new[] { 0f, 2.2f, .3f, 0f, 2.9f, -.2f, 0f, 3.6f, -.5f, 0f, 4.2f, -.2f }, new[] { .48f, .54f, .48f, .38f }, rock);
            s.Shell(0, 3.62f, -.82f, 1.48f, 1.38f, 1.18f, .32f, crust);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "leg_l" : "leg_r", side * .80f, 2.2f, 0);
                DigitigradeLeg(s, side, .80f, 2.2f, .95f, rock, crust);
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.08f, 3.58f, -.18f);
                s.Path(new[] { side * 1.08f, 3.58f, -.18f, side * 1.48f, 2.68f, .28f, side * 1.58f, 1.58f, .68f, side * 1.48f, .38f, .88f },
                    new[] { .32f, .26f, .20f, .16f }, rock);
                s.Ellipsoid(side * 1.48f, .26f, .92f, .24f, .16f, .28f, crust);
            }

            s.Part("head", 0, 4.32f, .44f);
            s.Ellipsoid(0, 4.52f, .74f, .46f, .40f, .44f, crust);
            s.Eyes(0, 4.60f, 1.08f, .20f, .09f, socket, eye, socket);
            for (int i = -2; i <= 2; i++)
                s.Tube(i * .13f, 4.28f, .98f, i * .16f, 3.84f, 1.10f, .065f, .035f, crust);

            s.Part("body", 0, 0, 0);
            s.Ellipsoid(0, 3.08f, .34f, .28f, .28f, .22f, glow);
            s.Cracks(glow, 22, 6, 2.0f, 4.6f);
            s.Light(0, 3.1f, .7f, .72f, .88f, 1f, 10f, 2.4f);
            return s.Finish("Плутоновый Отшельник");
        }

        // ---------- 9. Proxima B: Tidally-Locked Stellar Maw & Orbiting Shards ----------

        static SculptData ProximaDevourer(int seed)
        {
            var s = new Sculptor(.115f, seed);
            int crust = s.Pal("tidal crust", "magma-basalt", .34f, .17f, .14f, 0);
            int core = s.Pal("devourer core", "lava", 1f, .46f, .14f, 2.3f);
            int shard = s.Pal("tidal shard", "obsidian", .14f, .10f, .11f, 0);
            int vein = s.Pal("plasma vein", "lava", 1f, .68f, .22f, 1.9f);
            int tooth = s.Pal("obsidian fang", "polished-stone", .82f, .74f, .66f, 0);

            s.Part("body", 0, 3.6f, 0);
            s.Ellipsoid(0, 3.6f, 0, 1.32f, 1.32f, 1.28f, core);
            s.Shell(0, 3.6f, -.18f, 1.48f, 1.46f, 1.36f, .28f, crust);
            // Jagged obsidian fang ring around the front stellar maw + blazing central eye
            s.RingVertical(0, 3.6f, 1.12f, .88f, .16f, shard);
            for (int k = 0; k < 10; k++)
            {
                double a = k * Math.PI * 2 / 10;
                float fx = (float)Math.Cos(a) * .84f, fy = 3.6f + (float)Math.Sin(a) * .84f;
                s.Tube(fx, fy, 1.18f, fx * .56f, 3.6f + (fy - 3.6f) * .56f, 1.34f, .09f, .04f, tooth);
            }
            s.Ellipsoid(0, 3.6f, .96f, .38f, .38f, .24f, vein);

            for (int k = 0; k < 6; k++)
            {
                double a = k * Math.PI * 2 / 6 + .4;
                float x = (float)Math.Cos(a) * 1.42f, z = (float)Math.Sin(a) * 1.32f;
                s.Part("plate_" + k, x, 3.6f, z);
                s.Ellipsoid(x * 1.08f, 3.6f + (k % 2 == 0 ? .35f : -.35f), z * 1.08f, .44f, .52f, .28f, crust);
            }
            s.Part("body", 0, 3.6f, 0);
            s.Cracks(vein, 44, 8, 2.2f, 5.0f);

            // Six orbiting monolith shards animated by PlanetaryBossController
            for (int i = 0; i < 6; i++)
            {
                double a = i * Math.PI * 2 / 6;
                float sx = (float)Math.Cos(a) * 2.25f, sz = (float)Math.Sin(a) * 2.25f;
                float sy = 2.9f + (i % 3) * .65f;
                s.Part("shard_" + i, sx, sy, sz);
                s.Tube(sx, sy - .42f, sz, sx * 1.08f, sy + .48f, sz * 1.08f, .20f, .08f, shard);
                s.Ellipsoid(sx, sy, sz, .14f, .22f, .14f, vein);
            }

            s.Light(0, 3.6f, .8f, 1f, .48f, .16f, 22f, 4.2f);
            return s.Finish("Проксима-Пожиратель");
        }

        // ---------- 10. Trappist-1e: Bronze Organ-Pipe Resonance Archon ----------

        static SculptData TrappistChoir(int seed)
        {
            var s = new Sculptor(.11f, seed);
            int bell = s.Pal("resonant bell", "copper-block", .76f, .50f, .30f, 0);
            int pipe = s.Pal("choir pipe", "steel-rail", .58f, .62f, .68f, 0);
            int glow = s.Pal("resonance", "cherenkov-crystal", .74f, .62f, .96f, 1.7f);
            int eye = s.Pal("watcher eye", "glow-shroom", .90f, .82f, 1f, 2.1f);
            int socket = s.Pal("socket", "obsidian", .08f, .07f, .10f, 0);

            ArchonRobe(s, bell, pipe, 4.55f, 1.46f);
            for (int k = 0; k < 10; k++)
            {
                double a = k * Math.PI * 2 / 10;
                float x = (float)Math.Cos(a) * 1.04f, z = (float)Math.Sin(a) * .94f;
                float h = .72f + (k % 3) * .36f;
                s.Tube(x, 4.48f, z, x * 1.12f, 4.48f + h, z * 1.12f, .13f, .10f, pipe);
                s.Ellipsoid(x * 1.12f, 4.54f + h, z * 1.12f, .095f, .095f, .095f, glow);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.22f, 4.18f, 0);
                s.Path(new[] { side * 1.22f, 4.18f, 0f, side * 1.72f, 3.58f, .48f, side * 1.62f, 3.12f, .98f },
                    new[] { .25f, .20f, .15f }, bell);
            }
            s.Part("head", 0, 5.08f, 0);
            s.Ellipsoid(0, 5.30f, .10f, .45f, .52f, .44f, bell);
            s.Eyes(0, 5.40f, .46f, .19f, .09f, socket, eye, socket);
            s.Ring(0, 6.12f, 0, 1.16f, .065f, glow);
            s.Light(0, 4.6f, .6f, .78f, .62f, .94f, 12f, 2.6f);
            return s.Finish("Траппист-Смотритель");
        }

        // ---------- 11. Kepler-452b: Ancient Canopy Gardener ----------

        static SculptData KeplerGardener(int seed)
        {
            var s = new Sculptor(.12f, seed);
            int trunk = s.Pal("gardener trunk", "bark", .56f, .44f, .32f, 0);
            int leaf = s.Pal("emerald canopy", "leaves", .28f, .64f, .30f, 0);
            int bloom = s.Pal("dome bloom", "cherry-blossom", .94f, .62f, .72f, .45f);
            int glow = s.Pal("sap heart", "glow-shroom", .58f, 1f, .44f, 1.6f);
            int eye = s.Pal("gentle eye", "glow-shroom", .82f, 1f, .60f, 2.0f);
            int socket = s.Pal("socket", "obsidian", .07f, .08f, .06f, 0);

            for (int i = 0; i < 4; i++)
            {
                double a = Math.PI / 4 + i * Math.PI / 2;
                float x = (float)Math.Cos(a), z = (float)Math.Sin(a);
                s.Part("leg_" + i, x * .82f, 2.4f, z * .82f);
                s.Path(new[] { x * .72f, 2.58f, z * .72f, x * 1.12f, 1.48f, z * 1.12f, x * 1.24f, .36f, z * 1.24f },
                    new[] { .40f, .33f, .38f }, trunk);
            }
            s.Part("body", 0, 0, 0);
            s.Path(new[] { 0f, 2.38f, 0f, 0f, 3.42f, -.08f, 0f, 4.42f, 0f }, new[] { .60f, .54f, .44f }, trunk);
            s.Shell(0, 4.92f, 0, 1.72f, 1.18f, 1.72f, .34f, leaf);
            for (int k = 0; k < 10; k++)
            {
                double a = k * Math.PI * 2 / 10;
                s.Ellipsoid((float)Math.Cos(a) * 1.32f, 5.22f + (k % 2) * .20f, (float)Math.Sin(a) * 1.32f,
                    .34f, .26f, .34f, k % 3 == 0 ? bloom : leaf);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * .92f, 3.82f, 0);
                s.Path(new[] { side * .92f, 3.82f, 0f, side * 1.52f, 3.08f, .42f, side * 1.62f, 2.38f, .82f },
                    new[] { .32f, .25f, .20f }, trunk);
                for (int f = -1; f <= 1; f++)
                    s.Tube(side * 1.62f, 2.38f, .82f, side * (1.72f + f * .08f), 1.96f, 1.04f + f * .14f, .11f, .055f, trunk);
            }
            s.Part("head", 0, 4.58f, .30f);
            s.Ellipsoid(0, 4.74f, .54f, .42f, .44f, .40f, trunk);
            s.Eyes(0, 4.84f, .86f, .18f, .09f, socket, eye, socket);
            s.Part("body", 0, 0, 0);
            s.Ellipsoid(0, 3.58f, .50f, .26f, .30f, .20f, glow);
            s.Cracks(glow, 20, 6, 1.0f, 4.4f);
            s.Light(0, 4.9f, 0, .60f, 1f, .45f, 14f, 2.6f);
            return s.Finish("Кеплер-Садовник");
        }

        // ---------- 12. Andromeda Prime: Elysium Crystal Sovereign ----------

        static SculptData AndromedaLord(int seed)
        {
            var s = new Sculptor(.115f, seed);
            int robe = s.Pal("elysium robe", "wraith-shroud", .32f, .24f, .54f, 0);
            int gold = s.Pal("crown gold", "ore-gold", .94f, .78f, .34f, .25f);
            int crys = s.Pal("andromeda crystal", "andromeda-crystal", .82f, .54f, 1f, 1.6f);
            int eye = s.Pal("violet eye", "glow-shroom", .96f, .72f, 1f, 2.2f);
            int socket = s.Pal("socket", "obsidian", .09f, .06f, .12f, 0);

            ArchonRobe(s, robe, gold, 4.78f, 1.66f);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.28f, 4.42f, 0);
                s.Path(new[] { side * 1.28f, 4.42f, 0f, side * 1.78f, 3.88f, .42f, side * 1.68f, 3.48f, .92f },
                    new[] { .27f, .21f, .17f }, robe);
                s.Tube(side * .88f, 4.76f, .20f, side * 1.18f, 5.34f, .10f, .14f, .06f, crys);
            }
            s.Part("arm_r", 1.28f, 4.42f, 0);
            s.Tube(1.72f, 2.65f, .96f, 1.78f, 4.75f, 1.02f, .065f, .060f, gold);
            s.Ellipsoid(1.78f, 4.98f, 1.02f, .22f, .34f, .22f, crys);

            s.Part("head", 0, 5.28f, 0);
            s.Ellipsoid(0, 5.50f, .10f, .44f, .50f, .42f, robe);
            s.Eyes(0, 5.60f, .44f, .19f, .09f, socket, eye, socket);
            for (int k = 0; k < 7; k++)
            {
                double a = Math.PI * (0.15 + 0.7 * k / 6f);
                float x = (float)Math.Cos(a) * .50f, z = (float)Math.Sin(a) * .30f - .08f;
                s.Tube(x, 5.84f, z, x * 1.28f, 6.40f + (k % 2) * .18f, z * 1.22f, .095f, .045f, crys);
            }
            s.Ring(0, 6.52f, 0, 1.34f, .065f, gold);
            s.Light(0, 5.0f, .6f, .82f, .55f, 1f, 14f, 3.0f);
            return s.Finish("Элизиумский Владыка");
        }

        // ---------- 13. Somnia: He Who Heard ----------

        static SculptData HeWhoHeard(int seed)
        {
            var s = new Sculptor(.11f, seed);
            int skin = s.Pal("pale dream skin", "bone-stalker-hide", .80f, .78f, .74f, 0);
            int shroud = s.Pal("sleep shroud", "wraith-shroud", .20f, .22f, .36f, 0);
            int eye = s.Pal("heard eye", "glow-shroom", .86f, .88f, 1f, 2.4f);
            int hollow = s.Pal("hollow", "obsidian", .05f, .05f, .08f, 0);

            ArchonRobe(s, shroud, skin, 4.38f, 1.22f);
            s.Ellipsoid(0, 4.32f, 0, .68f, .92f, .48f, skin);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * .82f, 4.78f, 0);
                s.Path(new[] { side * .82f, 4.78f, 0f, side * 1.22f, 3.78f, .22f, side * 1.32f, 2.58f, .52f, side * 1.22f, 1.58f, .82f },
                    new[] { .19f, .15f, .12f, .085f }, skin);
                for (int f = -1; f <= 1; f++)
                    s.Tube(side * 1.22f, 1.58f, .82f, side * (1.26f + f * .06f), 1.08f, .98f + f * .10f, .055f, .030f, skin);
            }
            s.Part("head", 0, 5.28f, 0);
            s.Ellipsoid(0, 5.62f, 0, .38f, .66f, .36f, skin);
            s.Ellipsoid(0, 5.74f, .28f, .24f, .34f, .16f, hollow);
            s.Eyes(0, 5.84f, .38f, .14f, .07f, hollow, eye, hollow);
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * .36f, 5.62f, 0f, side * .82f, 5.92f, -.10f, side * 1.08f, 6.34f, -.20f },
                    new[] { .15f, .22f, .09f }, skin);
            s.Part("body", 0, 0, 0);
            s.Cracks(hollow, 16, 5, 2.0f, 5.0f);
            s.Light(0, 5.8f, .6f, .85f, .88f, 1f, 9f, 2.4f);
            return s.Finish("He Who Heard");
        }

        // ---------- 14. Dream: The First Node ----------

        static SculptData FirstNode(int seed)
        {
            var s = new Sculptor(.11f, seed);
            int chassis = s.Pal("node chassis", "generator-casing", .24f, .28f, .34f, 0);
            int panel = s.Pal("server panel", "hazard-steel", .50f, .52f, .58f, 0);
            int cable = s.Pal("data cable", "copper-cable", .76f, .48f, .22f, 0);
            int glow = s.Pal("machine heart", "cherenkov-crystal", .48f, .92f, 1f, 1.9f);
            int eye = s.Pal("optic array", "glow-shroom", .58f, .96f, 1f, 2.3f);

            ArchonRobe(s, chassis, panel, 4.42f, 1.38f);
            s.Box(0, 4.22f, 0, .82f, .98f, .58f, chassis);
            for (int i = 0; i < 4; i++)
                s.Box(0, 3.62f + i * .38f, .56f, .60f, .065f, .06f, glow);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.02f, 4.58f, 0);
                s.Path(new[] { side * 1.02f, 4.58f, 0f, side * 1.52f, 3.88f, .32f, side * 1.42f, 3.18f, .82f },
                    new[] { .16f, .12f, .10f }, cable);
                s.Box(side * 1.42f, 3.02f, .86f, .18f, .16f, .22f, panel);
            }
            s.Part("head", 0, 5.28f, 0);
            s.Box(0, 5.50f, .05f, .42f, .40f, .40f, panel);
            for (int i = -1; i <= 1; i++)
                s.Ellipsoid(i * .18f, 5.54f, .44f, .075f, .075f, .05f, eye);
            s.Ring(0, 6.20f, 0, 1.04f, .06f, glow);
            s.Light(0, 4.2f, .8f, .52f, .92f, 1f, 12f, 2.8f);
            return s.Finish("The First Node");
        }

        // ---------- 15. Radia: Trinitite Meltdown Colossus ----------

        static SculptData RadiaColossus(int seed)
        {
            var s = new Sculptor(.12f, seed);
            int glass = s.Pal("trinitite glass", "vitrified-golem-crust", .50f, .64f, .38f, .30f);
            int slag = s.Pal("slag plate", "slag-carapace", .30f, .32f, .26f, 0);
            int lead = s.Pal("lead containment", "lead-shield", .38f, .42f, .46f, 0);
            int glow = s.Pal("cherenkov fuel rod", "cherenkov-crystal", .45f, 1f, .52f, 2.1f);
            int eye = s.Pal("gamma visor", "glow-shroom", .72f, 1f, .45f, 2.4f);
            int socket = s.Pal("socket", "obsidian", .08f, .09f, .07f, 0);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "leg_l" : "leg_r", side * 1.08f, 2.55f, 0);
                s.Path(new[] { side * 1.08f, 2.55f, 0f, side * 1.24f, 1.48f, .12f, side * 1.14f, .68f, -.04f, side * 1.18f, .24f, .30f },
                    new[] { .54f, .46f, .40f, .44f }, slag);
                s.Box(side * 1.24f, 1.52f, .26f, .32f, .28f, .16f, lead);
            }

            s.Part("body", 0, 0, 0);
            s.Ellipsoid(0, 2.95f, 0, 1.26f, .66f, .88f, slag);
            s.Ellipsoid(0, 4.42f, -.08f, 1.44f, 1.32f, .90f, slag);
            // Exposed vertical Cherenkov fuel rod bundle inside breached lead ribcage!
            for (int rod = -1; rod <= 1; rod++)
                s.Tube(rod * .28f, 3.65f, .52f, rod * .28f, 5.15f, .52f, .13f, .13f, glow);
            for (int r = 0; r < 3; r++)
            {
                float ry = 3.82f + r * .56f;
                for (int side = -1; side <= 1; side += 2)
                    s.Tube(side * 1.28f, ry, .18f, side * .48f, ry, .74f, .15f, .11f, lead);
            }
            for (int side = -1; side <= 1; side += 2)
                s.Tube(side * 1.02f, 5.45f, -.12f, side * 1.42f, 6.45f, -.28f, .18f, .07f, glass);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.78f, 4.95f, 0);
                s.Ellipsoid(side * 1.78f, 5.05f, 0, .58f, .48f, .54f, lead);
                s.Path(new[] { side * 1.78f, 4.95f, 0f, side * 2.18f, 3.92f, .24f, side * 2.26f, 2.88f, .56f },
                    new[] { .42f, .36f, .32f }, slag);
                s.Ellipsoid(side * 2.28f, 2.48f, .68f, side > 0 ? .52f : .38f, .44f, .52f, glass);
            }

            s.Part("head", 0, 5.88f, .06f);
            s.Box(0, 6.18f, .12f, .58f, .48f, .54f, lead);
            s.Eyes(0, 6.24f, .62f, .25f, .11f, socket, eye, socket);

            s.Part("body", 0, 0, 0);
            s.Cracks(glow, 42, 9, .8f, 5.8f);
            s.Light(0, 4.4f, 1.1f, .55f, 1f, .45f, 20f, 3.8f);
            return s.Finish("Тринититовый Колосс");
        }

        // ---------- 16. Backrooms: Towering Tangled Wire-Sinew Entity ----------

        static SculptData BlackBacteria(int seed)
        {
            var s = new Sculptor(.11f, seed);
            int mass = s.Pal("entity mass", "wraith-shroud", .10f, .10f, .13f, 0);
            int wire = s.Pal("wire sinew", "obsidian", .22f, .22f, .28f, 0);
            int mask = s.Pal("pale lure mask", "bone-stalker-hide", .82f, .80f, .72f, 0);
            int carpet = s.Pal("stolen carpet", "backrooms-carpet", .62f, .56f, .34f, 0);
            int eye = s.Pal("many eyes", "glow-shroom", .88f, .92f, 1f, 2.4f);

            s.Part("body", 0, 2.6f, 0);
            // sprawling floor root-cables rising into an asymmetric rib-cabled horror
            for (int k = 0; k < 10; k++)
            {
                double a = k * Math.PI * 2 / 10 + .25;
                float x = (float)Math.Cos(a), z = (float)Math.Sin(a);
                s.Path(new[] { x * .55f, 2.15f, z * .50f, x * 1.25f, .95f, z * 1.15f, x * 1.85f, .16f, z * 1.70f },
                    new[] { .22f, .14f, .065f }, k % 2 == 0 ? mass : wire);
            }
            s.Path(new[] { 0f, 1.6f, -.1f, -.18f, 2.8f, -.15f, .14f, 3.95f, .08f }, new[] { .62f, .78f, .58f }, mass);
            for (int r = 0; r < 5; r++)
            {
                float ry = 2.1f + r * .42f;
                s.Ring(0, ry, 0, .78f - Math.Abs(r - 2) * .08f, .09f, wire);
            }
            s.Ellipsoid(-.48f, 3.35f, .58f, .28f, .34f, .14f, mask);
            s.Ellipsoid(-.48f, 3.42f, .70f, .09f, .11f, .07f, eye);
            s.Ellipsoid(.42f, 2.75f, .64f, .38f, .24f, .18f, carpet);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.05f, 3.75f, .05f);
                s.Path(new[] { side * 1.05f, 3.75f, .05f, side * 1.68f, 2.85f, .42f, side * 1.82f, 1.65f, .88f },
                    new[] { .24f, .18f, .12f }, mass);
                for (int f = -1; f <= 1; f++)
                    s.Tube(side * 1.82f, 1.65f, .88f, side * (1.88f + f * .10f), .95f, 1.18f + f * .14f, .07f, .035f, wire);
            }

            s.Part("head", 0, 4.18f, .16f);
            s.Ellipsoid(0, 4.48f, .24f, .48f, .56f, .44f, mass);
            s.Ellipsoid(0, 4.46f, .62f, .32f, .40f, .12f, mask);
            s.Ellipsoid(.13f, 4.56f, .72f, .09f, .12f, .06f, eye);
            s.Ellipsoid(-.14f, 4.44f, .72f, .08f, .10f, .06f, eye);
            for (int side = -1; side <= 1; side += 2)
                s.Tube(side * .28f, 4.92f, .18f, side * .58f, 5.58f, -.08f, .08f, .04f, wire);

            s.Light(0, 3.6f, .9f, .80f, .85f, 1f, 10f, 2.2f);
            return s.Finish("The Black Bacteria");
        }

        // ---------- 17. Inferno: Winged Horned Obsidian-Magma Archfiend ----------

        static SculptData InfernalArchfiend(int seed)
        {
            var s = new Sculptor(.115f, seed);
            int obsidian = s.Pal("obsidian plate", "obsidian", .14f, .11f, .13f, 0);
            int basalt = s.Pal("basalt hide", "basalt", .28f, .22f, .21f, 0);
            int crimson = s.Pal("crimson carapace", "red-mesa", .72f, .22f, .14f, 0);
            int magma = s.Pal("magma veins", "magma-basalt", .98f, .44f, .14f, 1.5f);
            int core = s.Pal("hellfire heart", "lava", 1.00f, .66f, .18f, 2.8f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "leg_l" : "leg_r", side * .62f, 1.85f, 0);
                s.Path(new[] { side * .62f, 1.85f, .06f, side * .74f, 1.05f, -.22f, side * .68f, .22f, .14f },
                    new[] { .34f, .26f, .22f }, basalt);
                s.Box(side * .48f, .02f, -.06f, side * .88f, .24f, .44f, obsidian);
                s.Tube(side * .68f, 1.15f, -.24f, side * .72f, 1.52f, -.48f, .10f, .04f, crimson);
            }

            s.Part("body", 0, 2.55f, 0);
            s.Ellipsoid(0, 2.58f, 0, .86f, 1.08f, .62f, basalt);
            s.Ellipsoid(0, 3.12f, .08f, 1.06f, .72f, .68f, obsidian);
            // Glowing hellfire furnace core inside ribcage
            s.Ellipsoid(0, 2.86f, .46f, .38f, .44f, .24f, core);
            for (int r = 0; r < 4; r++)
                s.Ring(0, 2.35f + r * .28f, .18f, .68f - r * .04f, .075f, crimson);
            // Jagged spine ridges & tail
            for (int k = 0; k < 5; k++)
                s.Tube(0, 2.2f + k * .36f, -.48f, 0, 2.45f + k * .38f, -.88f, .11f, .04f, magma);
            s.Path(new[] { 0f, 1.92f, -.44f, 0f, 1.25f, -1.18f, 0f, .72f, -1.92f }, new[] { .22f, .14f, .07f }, obsidian);

            // Swept volcanic bat-wings
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "wing_l" : "wing_r", side * .82f, 3.38f, -.32f);
                s.Path(new[] { side * .82f, 3.38f, -.32f, side * 1.95f, 4.35f, -.62f, side * 2.75f, 3.45f, -.88f },
                    new[] { .16f, .12f, .08f }, obsidian);
                for (int fin = 0; fin < 3; fin++)
                    s.Tube(side * (1.55f + fin * .45f), 4.05f - fin * .22f, -.58f,
                        side * (1.72f + fin * .48f), 2.35f - fin * .18f, -.76f, .11f, .05f, fin == 1 ? magma : crimson);
            }

            // Massive clawed magma gauntlets
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.18f, 3.28f, .10f);
                s.Ellipsoid(side * 1.22f, 3.42f, .08f, .38f, .34f, .36f, crimson);
                s.Path(new[] { side * 1.18f, 3.22f, .10f, side * 1.54f, 2.38f, .38f, side * 1.42f, 1.52f, .78f },
                    new[] { .28f, .24f, .26f }, basalt);
                s.Ellipsoid(side * 1.42f, 1.44f, .82f, .30f, .28f, .32f, magma);
                for (int c = -1; c <= 1; c++)
                    s.Tube(side * (1.42f + c * .12f), 1.32f, .96f, side * (1.46f + c * .15f), .96f, 1.24f, .07f, .03f, obsidian);
            }

            s.Part("head", 0, 3.88f, .22f);
            s.Ellipsoid(0, 4.18f, .28f, .46f, .50f, .48f, obsidian);
            s.Ellipsoid(0, 4.02f, .56f, .30f, .24f, .26f, crimson);
            s.Ellipsoid(-.16f, 4.26f, .68f, .09f, .08f, .07f, core);
            s.Ellipsoid(.16f, 4.26f, .68f, .09f, .08f, .07f, core);
            // Sweeping twin demon horns
            for (int side = -1; side <= 1; side += 2)
                s.Path(new[] { side * .28f, 4.48f, .24f, side * .78f, 4.98f, -.08f, side * .54f, 5.56f, .32f },
                    new[] { .15f, .10f, .045f }, magma);

            s.Light(0, 3.2f, .8f, 1.0f, .42f, .12f, 16f, 3.4f);
            return s.Finish("Infernal Archfiend");
        }

        // ---------- Extras: Corrupted AI & Void Horror ----------

        static SculptData CorruptedAI(int seed)
        {
            var s = new Sculptor(.11f, seed);
            int chassis = s.Pal("corrupt chassis", "generator-casing", .20f, .15f, .32f, 0);
            int glitch = s.Pal("glitch plate", "hazard-steel", .58f, .32f, .74f, .5f);
            int glow = s.Pal("corrupt core", "cherenkov-crystal", .88f, .35f, 1f, 2.1f);
            int eye = s.Pal("broken optic", "glow-shroom", 1f, .42f, .96f, 2.4f);

            ArchonRobe(s, chassis, glitch, 4.48f, 1.38f);
            s.Box(0, 4.28f, 0, .78f, .94f, .54f, chassis);
            for (int i = 0; i < 5; i++)
                s.Box((i % 2 == 0 ? .18f : -.18f), 3.68f + i * .32f, .52f, .40f, .06f, .05f, glitch);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.02f, 4.68f, 0);
                s.Path(new[] { side * 1.02f, 4.68f, 0f, side * 1.52f, 3.98f, .40f, side * 1.32f, 3.28f, .88f },
                    new[] { .20f, .15f, .11f }, chassis);
            }
            s.Part("head", 0, 5.38f, 0);
            s.Box(0, 5.58f, .05f, .40f, .46f, .38f, chassis);
            s.Ellipsoid(.10f, 5.66f, .42f, .10f, .06f, .05f, eye);
            s.Ellipsoid(-.16f, 5.50f, .42f, .06f, .10f, .05f, eye);
            s.Ring(0, 6.28f, 0, 1.02f, .06f, glow);
            s.Part("body", 0, 0, 0);
            s.Cracks(glow, 24, 6, 2.0f, 5.2f);
            s.Light(0, 4.3f, .7f, .85f, .35f, 1f, 12f, 3.0f);
            return s.Finish("The Corrupted AI");
        }

        static SculptData VoidHorror(int seed)
        {
            var s = new Sculptor(.115f, seed);
            int voidm = s.Pal("void flesh", "wraith-shroud", .11f, .09f, .15f, 0);
            int edge = s.Pal("event edge", "obsidian", .26f, .18f, .30f, .3f);
            int glow = s.Pal("void maw", "cherenkov-crystal", 1f, .24f, .36f, 2.0f);
            int eye = s.Pal("starved eye", "glow-shroom", 1f, .56f, .62f, 2.3f);

            ArchonRobe(s, voidm, edge, 4.58f, 1.56f);
            s.Ellipsoid(0, 4.38f, 0, .92f, 1.08f, .68f, voidm);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "arm_l" : "arm_r", side * 1.12f, 4.88f, 0);
                s.Path(new[] { side * 1.12f, 4.88f, 0f, side * 1.72f, 4.08f, .32f, side * 1.92f, 3.18f, .82f, side * 1.72f, 2.48f, 1.22f },
                    new[] { .25f, .19f, .14f, .08f }, voidm);
            }
            s.Part("head", 0, 5.48f, .20f);
            s.Ellipsoid(0, 5.68f, .20f, .48f, .68f, .44f, voidm);
            s.Ellipsoid(0, 5.68f, .56f, .25f, .44f, .18f, glow);
            for (int i = -2; i <= 2; i++)
            {
                s.Ellipsoid(i * .12f, 6.02f - Math.Abs(i) * .04f, .60f, .05f, .10f, .05f, edge);
                s.Ellipsoid(i * .12f, 5.34f + Math.Abs(i) * .04f, .60f, .05f, .10f, .05f, edge);
            }
            s.Eyes(0, 6.22f, .46f, .22f, .08f, voidm, eye, voidm);
            s.Part("body", 0, 0, 0);
            s.Cracks(glow, 28, 7, 1.6f, 5.4f);
            s.Light(0, 5.7f, .8f, 1f, .25f, .35f, 12f, 3.0f);
            return s.Finish("The Void Horror");
        }

        // ---------- Liminal Backrooms Stalker Mob ----------

        public static SculptData BacteriaMob(int seed)
        {
            var s = new Sculptor(.06f, seed);
            int black = s.Pal("bacteria mass", "wraith-shroud", .08f, .08f, .11f, 0);
            int wire = s.Pal("wire sinew", "obsidian", .22f, .22f, .28f, 0);
            int face = s.Pal("lure face", "bone-stalker-hide", .78f, .82f, .92f, .85f);
            int eye = s.Pal("lure eye", "glow-shroom", .58f, .68f, .88f, 2.1f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "leg_l" : "leg_r", side * .16f, .62f, 0);
                s.Path(new[] { side * .16f, .62f, 0f, side * .20f, .32f, .04f, side * .22f, .06f, .14f },
                    new[] { .085f, .065f, .09f }, black);
                s.Part(side < 0 ? "arm_l" : "arm_r", side * .34f, 1.06f, .04f);
                s.Path(new[] { side * .34f, 1.06f, .04f, side * .40f, .58f, .10f, side * .38f, .20f, .18f },
                    new[] { .075f, .055f, .045f }, black);
                for (int f = -1; f <= 1; f++)
                    s.Tube(side * .38f, .20f, .18f, side * (.40f + f * .03f), .04f, .28f + f * .05f, .035f, .022f, wire);
            }
            s.Part("body", 0, 0, 0);
            s.Path(new[] { 0f, .58f, 0f, 0f, .86f, -.02f, 0f, 1.14f, .02f }, new[] { .22f, .19f, .21f }, black);
            for (int r = 0; r < 3; r++)
                s.Ring(0, .66f + r * .18f, 0, .22f, .035f, wire);

            s.Part("head", 0, 1.28f, .02f);
            s.Ellipsoid(0, 1.42f, .02f, .26f, .30f, .24f, black);
            s.Ellipsoid(0, 1.40f, .22f, .16f, .20f, .06f, face);
            s.Eyes(0, 1.45f, .26f, .08f, .04f, face, eye, face);
            s.Light(0, 1.4f, .42f, .62f, .72f, .90f, 5f, 1.1f);
            return s.Finish("Entity 23");
        }

        // ---------- 7 Aerospace & Ground Drones ----------

        public static SculptData Drone(DroneKind kind)
        {
            var s = new Sculptor(.055f, 777 + (int)kind * 31);
            int hull = s.Pal("drone hull", "generator-casing", .32f, .36f, .42f, 0);
            int plate = s.Pal("drone plate", "titanium-hull", .62f, .66f, .74f, 0);
            int hazard = s.Pal("hazard trim", "hazard-steel", .88f, .68f, .18f, 0);
            int glow = s.Pal("drone glow", "cherenkov-crystal", .45f, .88f, 1f, 2.0f);
            int hot = s.Pal("engine hot", "lava", 1f, .54f, .18f, 1.8f);
            int dark = s.Pal("drone shadow", "obsidian", .14f, .15f, .18f, 0);

            switch (kind)
            {
                case DroneKind.Scout:
                    s.Ellipsoid(0, .24f, 0, .28f, .18f, .30f, hull);
                    s.Ellipsoid(0, .34f, -.02f, .20f, .08f, .22f, plate);
                    s.Ellipsoid(0, .24f, .28f, .11f, .09f, .08f, glow);
                    for (int i = 0; i < 4; i++)
                    {
                        double a = Math.PI / 4 + i * Math.PI / 2;
                        float x = (float)Math.Cos(a) * .36f, z = (float)Math.Sin(a) * .36f;
                        s.Part("rotor_" + i, x, .32f, z);
                        s.Tube(x * .42f, .28f, z * .42f, x, .32f, z, .045f, .040f, dark);
                        s.Ring(x, .34f, z, .14f, .032f, plate);
                        s.Box(x, .35f, z, .12f, .016f, .032f, hazard);
                    }
                    break;

                case DroneKind.Combat:
                    s.Ellipsoid(0, .30f, 0, .30f, .18f, .44f, hull);
                    s.Ellipsoid(0, .32f, .40f, .11f, .08f, .09f, glow);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        s.Part(side < 0 ? "wing_l" : "wing_r", side * .26f, .32f, 0);
                        s.Tube(side * .26f, .32f, .06f, side * .78f, .36f, -.16f, .11f, .065f, plate);
                        s.Tube(side * .54f, .24f, .04f, side * .58f, .24f, .46f, .045f, .035f, dark); // underwing railgun
                    }
                    s.Part("body", 0, 0, 0);
                    s.Ellipsoid(-.10f, .28f, -.42f, .07f, .07f, .06f, hot);
                    s.Ellipsoid(.10f, .28f, -.42f, .07f, .07f, .06f, hot);
                    break;

                case DroneKind.Repair:
                    s.Ellipsoid(0, .52f, 0, .28f, .26f, .28f, plate);
                    s.Ellipsoid(0, .52f, .25f, .11f, .10f, .07f, glow);
                    s.Ring(0, .82f, 0, .24f, .038f, glow);
                    s.Part("arm_r", .24f, .46f, .08f);
                    s.Path(new[] { .24f, .46f, .08f, .40f, .28f, .28f, .44f, .10f, .44f }, new[] { .06f, .05f, .04f }, dark);
                    s.Ellipsoid(.44f, .08f, .46f, .055f, .055f, .055f, hot);
                    break;

                case DroneKind.Ground:
                    for (int side = -1; side <= 1; side += 2)
                    {
                        s.Part(side < 0 ? "leg_l" : "leg_r", side * .18f, .62f, 0);
                        s.Path(new[] { side * .18f, .62f, 0f, side * .24f, .34f, -.08f, side * .22f, .06f, .12f },
                            new[] { .09f, .075f, .10f }, hull);
                    }
                    s.Part("body", 0, 0, 0);
                    s.Box(0, .82f, 0, .26f, .22f, .22f, plate);
                    s.Box(0, .74f, .20f, .22f, .05f, .04f, hazard);
                    s.Part("turret", 0, 1.12f, .02f);
                    s.Ellipsoid(0, 1.14f, .02f, .20f, .16f, .20f, hull);
                    s.Ellipsoid(0, 1.15f, .20f, .09f, .07f, .05f, glow);
                    s.Tube(.16f, 1.14f, .12f, .18f, 1.14f, .42f, .04f, .035f, dark);
                    break;

                case DroneKind.Heavy:
                    for (int i = 0; i < 4; i++)
                    {
                        double a = Math.PI / 4 + i * Math.PI / 2;
                        float x = (float)Math.Cos(a), z = (float)Math.Sin(a);
                        s.Part("leg_" + i, x * .30f, .72f, z * .30f);
                        s.Path(new[] { x * .28f, .72f, z * .28f, x * .50f, .40f, z * .50f, x * .54f, .08f, z * .54f },
                            new[] { .11f, .085f, .12f }, hull);
                    }
                    s.Part("body", 0, 0, 0);
                    s.Ellipsoid(0, .96f, 0, .42f, .28f, .38f, plate);
                    s.Box(0, 1.28f, .08f, .26f, .18f, .22f, hull);
                    s.Ellipsoid(0, 1.30f, .30f, .11f, .075f, .06f, glow);
                    for (int side = -1; side <= 1; side += 2)
                        s.Tube(side * .32f, 1.22f, .14f, side * .36f, 1.22f, .56f, .065f, .05f, dark);
                    break;

                case DroneKind.Guard:
                    s.Ellipsoid(0, .54f, 0, .30f, .28f, .28f, hull);
                    s.Ellipsoid(0, .58f, .26f, .10f, .08f, .06f, glow);
                    s.Ring(0, .88f, 0, .25f, .035f, glow);
                    s.Part("shield", 0, .50f, .30f);
                    s.Shell(0, .50f, .34f, .38f, .42f, .14f, .065f, plate);
                    s.Box(0, .50f, .42f, .28f, .05f, .03f, hazard);
                    break;

                default: // Builder
                    s.Ellipsoid(0, .38f, 0, .30f, .24f, .30f, hazard);
                    s.Ellipsoid(0, .40f, .27f, .11f, .09f, .07f, glow);
                    s.Part("arm_r", .28f, .42f, 0);
                    s.Path(new[] { .28f, .42f, 0f, .48f, .52f, .18f, .58f, .26f, .42f }, new[] { .07f, .06f, .05f }, plate);
                    s.Box(.60f, .20f, .46f, .075f, .075f, .09f, hot);
                    s.Part("arm_l", -.28f, .42f, 0);
                    s.Path(new[] { -.28f, .42f, 0f, -.46f, .24f, .26f }, new[] { .065f, .05f }, dark);
                    break;
            }
            return s.Finish("Drone " + kind);
        }
    }
}
