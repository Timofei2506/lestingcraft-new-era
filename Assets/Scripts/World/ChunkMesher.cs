using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.Presentation;

namespace LestingCraft.World
{
    public sealed class ChunkMeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector3> UVs = new List<Vector3>();
        public readonly List<Vector4> Tangents = new List<Vector4>();
        public readonly List<int> OpaqueTriangles = new List<int>();
        public readonly List<int> WaterTriangles = new List<int>();
        public readonly List<int> TransparentTriangles = new List<int>();
        public readonly List<int> DetailTriangles = new List<int>();
        public readonly List<Color32> Colors = new List<Color32>();
        public readonly List<int> Triangles = new List<int>();
        public readonly List<int> ColliderTriangles = new List<int>();
        public int FaceCount { get { return Triangles.Count / 6; } }

        public Mesh CreateMesh(bool collision = false)
        {
            var mesh = new Mesh();
            WriteMesh(mesh, collision);
            return mesh;
        }

        // Only call on the Unity main thread, and never while this mesh is being baked.
        public void WriteMesh(Mesh mesh, bool collision = false)
        {
            mesh.Clear(false);
            mesh.indexFormat = Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(Vertices);
            if (collision)
            {
                mesh.subMeshCount = 1;
                mesh.SetTriangles(ColliderTriangles, 0, false);
            }
            else
            {
                mesh.subMeshCount = 4;
                mesh.SetTriangles(OpaqueTriangles, 0, false);
                mesh.SetTriangles(WaterTriangles, 1, false);
                mesh.SetTriangles(DetailTriangles, 2, false);
                mesh.SetTriangles(TransparentTriangles, 3, false);
                mesh.SetNormals(Normals);
                mesh.SetColors(Colors);
                mesh.SetUVs(0, UVs);
                mesh.SetTangents(Tangents);
            }
            mesh.RecalculateBounds();
            if (!collision && (DetailTriangles.Count != 0 || WaterTriangles.Count != 0))
            {
                Bounds bounds = mesh.bounds;
                bounds.Expand(.4f);
                mesh.bounds = bounds;
            }
        }
    }

    public static class ChunkMesher
    {
        static readonly Vector3Int[] Directions =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };

        static readonly Vector3[][] Corners =
        {
            new[] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },
            new[] { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0) },
            new[] { new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0), new Vector3(0,1,0) },
            new[] { new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(0,0,1) },
            new[] { new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1), new Vector3(0,0,1) },
            new[] { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) }
        };

        // All greedy face/AO/lighting queries address this cache, including chunk corners.
        sealed class Cache
        {
            readonly ushort[] blocks = new ushort[18 * 256 * 18];
            readonly byte[] light = new byte[18 * 256 * 18];

            public Cache(ChunkData chunk, int ox, int oz, Func<int, int, int, ushort> neighbor,
                Func<int, int, int, byte> skylight)
            {
                for (int y = 0; y < 256; y++)
                    for (int z = -1; z <= 16; z++)
                        for (int x = -1; x <= 16; x++)
                        {
                            int index = Index(x, y, z);
                            blocks[index] = ChunkData.InBounds(x, y, z)
                                ? chunk.GetBlock(x, y, z) : neighbor(ox + x, y, oz + z);
                            light[index] = skylight == null ? (byte)15 : skylight(ox + x, y, oz + z);
                        }
            }

            static int Index(int x, int y, int z) { return x + 1 + 18 * (z + 1 + 18 * y); }
            public ushort Block(int x, int y, int z)
            {
                return y < 0 || y >= 256 ? (ushort)0 : blocks[Index(x, y, z)];
            }
            public byte Light(int x, int y, int z)
            {
                return y < 0 ? (byte)0 : y >= 256 ? (byte)15 : light[Index(x, y, z)];
            }
        }

        struct Face
        {
            public ushort Id;
            public int Layer;
            public uint AO, Sky;
            public float Bottom, Top;
            public bool Solid;

            public bool Same(Face other)
            {
                return Id != 0 && Id == other.Id && Layer == other.Layer &&
                    AO == other.AO && Sky == other.Sky && Bottom == other.Bottom &&
                    Top == other.Top && Solid == other.Solid;
            }

            // Repeated nonconstant vertex gradients cannot be replaced by one interpolated
            // gradient without changing lighting. Keep those tiles separate along that axis.
            public bool AlongU
            {
                get
                {
                    return Id != 15 && Byte(AO, 0) == Byte(AO, 3) &&
                        Byte(AO, 1) == Byte(AO, 2) && Byte(Sky, 0) == Byte(Sky, 3) &&
                        Byte(Sky, 1) == Byte(Sky, 2);
                }
            }
            public bool AlongV
            {
                get
                {
                    return Id != 15 && Byte(AO, 0) == Byte(AO, 1) &&
                        Byte(AO, 3) == Byte(AO, 2) && Byte(Sky, 0) == Byte(Sky, 1) &&
                        Byte(Sky, 3) == Byte(Sky, 2);
                }
            }
        }

        static byte Byte(uint value, int corner) { return (byte)(value >> (corner * 8)); }

        public static Color32 Palette(ushort id)
        {
            switch (id)
            {
                case 1: return new Color32(119,133,146,255);
                case 2: return new Color32(133,91,60,255);
                case 3: return new Color32(105,177,89,255);
                case 4: return new Color32(111,75,46,255);
                case 5: return new Color32(61,135,76,255);
                case 6: return new Color32(226,206,143,255);
                case 7: return new Color32(59,137,197,255);
                case 8: return new Color32(246,105,47,255);
                case 9: return new Color32(39,45,56,255);
                case 10: return new Color32(79,68,78,255);
                case 11: return new Color32(174,108,224,255);
                case 12: return new Color32(105,211,216,255);
                case 13: return new Color32(194,160,103,255);
                case 14: return new Color32(224,236,244,255);
                case 15: return new Color32(153,91,43,255);
                case 16: return new Color32(139,101,61,255);
                case 17: return new Color32(106,103,97,255);
                case 18: return new Color32(154,113,68,255);
                default: return new Color32(255,0,255,255);
            }
        }

        public static ChunkMeshData Build(ChunkData chunk, int ox, int oz,
            Func<int,int,int,ushort> neighbor, int seed = 0, bool decorate = false,
            int detailQuality = 0, Func<int,int,int,byte> skylight = null,
            Func<int,int,int,float> fluidHeight = null, bool greedy = false)
        {
            if (greedy)
                return BuildGreedy(chunk, ox, oz, neighbor, seed, decorate, detailQuality, skylight, fluidHeight);
            return BuildUnits(chunk, ox, oz, neighbor, seed, decorate, detailQuality, skylight, fluidHeight);
        }

        // The compatibility path deliberately preserves the original face order, lighting,
        // fluid geometry, collider inclusion and authored-object treatment.
        static ChunkMeshData BuildUnits(ChunkData chunk, int ox, int oz,
            Func<int,int,int,ushort> neighbor, int seed, bool decorate, int detailQuality,
            Func<int,int,int,byte> skylight, Func<int,int,int,float> fluidHeight)
        {
            var data = new ChunkMeshData();
            var grassCells = decorate ? new List<Vector3Int>() : null;
            for (int y = 0; y < ChunkData.SizeY; y++)
                for (int z = 0; z < 16; z++)
                    for (int x = 0; x < 16; x++)
                    {
                        ushort id = chunk.GetBlock(x, y, z);
                        if (id == 0 || id == BlockRegistry.CrystalId) continue;
                        bool solid = BlockRegistry.IsSolid(id);
                        for (int face = 0; face < 6; face++)
                        {
                            var dir = Directions[face];
                            int nx = x + dir.x, ny = y + dir.y, nz = z + dir.z;
                            ushort other = ChunkData.InBounds(nx, ny, nz)
                                ? chunk.GetBlock(nx, ny, nz) : neighbor(ox + nx, ny, oz + nz);
                            bool liquid = id == 7 || id == 8;
                            float ownHeight = liquid && fluidHeight != null ? fluidHeight(ox + x, y, oz + z) : 1;
                            float adjacentHeight = liquid && other == id && fluidHeight != null
                                ? fluidHeight(ox + nx, ny, oz + nz) : 1;
                            if ((BlockRegistry.Occludes(other) && other != 5) ||
                                ((!solid || id == 44 || id == 5) && other == id &&
                                (!liquid || face == 2 || face == 3 || ownHeight <= adjacentHeight))) continue;
                            int start = data.Vertices.Count;
                            Vector3 u = Corners[face][3] - Corners[face][0];
                            Vector3 v = Corners[face][1] - Corners[face][0];
                            int layer = SurfaceLayers.ForCellFace(id, face, ox + x, y, oz + z, seed);
                            for (int c = 0; c < 4; c++)
                            {
                                float ux = c >= 2 ? 1 : 0, vy = c == 1 || c == 2 ? 1 : 0;
                                Vector3Int su = Vector3Int.RoundToInt(u * (ux == 0 ? -1 : 1));
                                Vector3Int sv = Vector3Int.RoundToInt(v * (vy == 0 ? -1 : 1));
                                int a = Solid(chunk, ox, oz, nx + su.x, ny + su.y, nz + su.z, neighbor);
                                int b = Solid(chunk, ox, oz, nx + sv.x, ny + sv.y, nz + sv.z, neighbor);
                                int corner = Solid(chunk, ox, oz, nx + su.x + sv.x, ny + su.y + sv.y, nz + su.z + sv.z, neighbor);
                                int ao = a == 1 && b == 1 ? 3 : a + b + corner;
                                byte shade = (byte)(255 - ao * 30);
                                Vector3 position = Corners[face][c];
                                if (liquid && fluidHeight != null && neighbor(ox + x, y + 1, oz + z) != id)
                                    position.y = other == id && face != 2 && face != 3
                                        ? Mathf.Lerp(adjacentHeight, ownHeight, position.y) : position.y * ownHeight;
                                if (id == 15) position.y *= .34f;
                                data.Vertices.Add(new Vector3(x, y, z) + position);
                                data.Normals.Add(dir);
                                data.Colors.Add(new Color32(shade, (byte)((skylight != null ? skylight(ox + nx, ny, oz + nz) : 15) * 17), shade, 255));
                                data.UVs.Add(new Vector3(ux, vy, layer));
                                data.Tangents.Add(new Vector4(u.x, u.y, u.z, -1));
                            }
                            AddIndices(data, id, solid, start);
                            if (decorate && id == BlockRegistry.GrassId && face == 2 && other == 0)
                                grassCells.Add(new Vector3Int(x, y, z));
                        }
                    }
            if (grassCells != null)
                foreach (var cell in grassCells)
                    SurfaceDetailMesher.AddGrass(data, cell.x, cell.y, cell.z, ox, oz, seed, detailQuality);
            return data;
        }

        static ChunkMeshData BuildGreedy(ChunkData chunk, int ox, int oz,
            Func<int,int,int,ushort> neighbor, int seed, bool decorate, int quality,
            Func<int,int,int,byte> skylight, Func<int,int,int,float> fluidHeight)
        {
            var cache = new Cache(chunk, ox, oz, neighbor, skylight);
            var data = new ChunkMeshData();
            var mask = new Face[16 * 256];
            for (int face = 0; face < 6; face++)
            {
                bool horizontal = face == 2 || face == 3;
                int slices = horizontal ? 256 : 16;
                int rows = horizontal ? 16 : 256;
                Vector3 u = Corners[face][3] - Corners[face][0];
                Vector3 v = Corners[face][1] - Corners[face][0];
                for (int slice = 0; slice < slices; slice++)
                {
                    for (int b = 0; b < rows; b++)
                        for (int a = 0; a < 16; a++)
                        {
                            Vector3Int cell = Cell(face, slice, a, b);
                            mask[a + b * 16] = MakeFace(cache, cell, face, ox, oz, seed, fluidHeight);
                            if (decorate && face == 2 && mask[a + b * 16].Id == BlockRegistry.GrassId &&
                                cache.Block(cell.x, cell.y + 1, cell.z) == 0)
                                SurfaceDetailMesher.AddGrass(data, cell.x, cell.y, cell.z, ox, oz, seed, quality);
                        }
                    for (int b = 0; b < rows; b++)
                        for (int a = 0; a < 16; a++)
                        {
                            Face tile = mask[a + b * 16];
                            if (tile.Id == 0) continue;
                            int width = 1, height = 1;
                            if (tile.AlongU)
                                while (a + width < 16 && tile.Same(mask[a + width + b * 16])) width++;
                            // Vertical stacking of shortened fluid faces would erase gaps/steps.
                            bool stack = horizontal || (tile.Bottom == 0 && tile.Top == 1);
                            if (tile.AlongV && stack)
                            {
                                bool grow = true;
                                while (b + height < rows && grow)
                                {
                                    for (int k = 0; k < width; k++)
                                        if (!tile.Same(mask[a + k + (b + height) * 16])) { grow = false; break; }
                                    if (grow) height++;
                                }
                            }
                            Vector3Int cell = Cell(face, slice, a, b);
                            int start = data.Vertices.Count;
                            for (int c = 0; c < 4; c++)
                            {
                                float ux = c >= 2 ? 1 : 0, vy = c == 1 || c == 2 ? 1 : 0;
                                Vector3 p = Corners[face][c];
                                p.y = Mathf.Lerp(tile.Bottom, tile.Top, p.y);
                                p += u * ((width - 1) * ux) + v * ((height - 1) * vy);
                                data.Vertices.Add((Vector3)cell + p);
                                data.Normals.Add(Directions[face]);
                                byte shade = Byte(tile.AO, c);
                                data.Colors.Add(new Color32(shade, Byte(tile.Sky, c), shade, 255));
                                data.UVs.Add(new Vector3(ux * width, vy * height, tile.Layer));
                                data.Tangents.Add(new Vector4(u.x, u.y, u.z, -1));
                            }
                            AddIndices(data, tile.Id, tile.Solid, start);
                            for (int j = 0; j < height; j++)
                                for (int k = 0; k < width; k++)
                                    mask[a + k + (b + j) * 16] = default(Face);
                        }
                }
            }
            return data;
        }

        static Vector3Int Cell(int face, int slice, int a, int b)
        {
            switch (face)
            {
                case 0: return new Vector3Int(slice, b, a);
                case 1: return new Vector3Int(slice, b, 15 - a);
                case 2: return new Vector3Int(b, slice, 15 - a);
                case 3: return new Vector3Int(b, slice, a);
                case 4: return new Vector3Int(15 - a, b, slice);
                default: return new Vector3Int(a, b, slice);
            }
        }

        static Face MakeFace(Cache cache, Vector3Int p, int face, int ox, int oz,
            int seed, Func<int,int,int,float> fluidHeight)
        {
            ushort id = cache.Block(p.x, p.y, p.z);
            if (id == 0 || id == BlockRegistry.CrystalId) return default(Face);
            Vector3Int n = p + Directions[face];
            ushort other = cache.Block(n.x, n.y, n.z);
            bool solid = BlockRegistry.IsSolid(id), liquid = id == 7 || id == 8;
            float own = liquid && fluidHeight != null ? fluidHeight(ox + p.x, p.y, oz + p.z) : 1;
            float adjacent = liquid && other == id && fluidHeight != null
                ? fluidHeight(ox + n.x, n.y, oz + n.z) : 1;
            if ((BlockRegistry.Occludes(other) && other != 5) ||
                ((!solid || id == 44 || id == 5) && other == id &&
                (!liquid || face == 2 || face == 3 || own <= adjacent))) return default(Face);
            Face result = new Face
            {
                Id = id, Solid = solid, Top = id == 15 ? .34f : 1,
                Layer = SurfaceLayers.ForCellFace(id, face, ox + p.x, p.y, oz + p.z, seed)
            };
            if (liquid && fluidHeight != null && cache.Block(p.x, p.y + 1, p.z) != id)
            {
                result.Top = own;
                result.Bottom = other == id && face != 2 && face != 3 ? adjacent : 0;
            }
            Vector3 u = Corners[face][3] - Corners[face][0];
            Vector3 v = Corners[face][1] - Corners[face][0];
            for (int c = 0; c < 4; c++)
            {
                Vector3Int su = Vector3Int.RoundToInt(u * (c >= 2 ? 1 : -1));
                Vector3Int sv = Vector3Int.RoundToInt(v * (c == 1 || c == 2 ? 1 : -1));
                Vector3Int a = n + su, b = n + sv, d = n + su + sv;
                int sa = BlockRegistry.Occludes(cache.Block(a.x, a.y, a.z)) ? 1 : 0;
                int sb = BlockRegistry.Occludes(cache.Block(b.x, b.y, b.z)) ? 1 : 0;
                int sc = BlockRegistry.Occludes(cache.Block(d.x, d.y, d.z)) ? 1 : 0;
                int ao = sa == 1 && sb == 1 ? 3 : sa + sb + sc;
                result.AO |= (uint)(255 - ao * 30) << (c * 8);
                // Average propagated light at the four cells meeting this vertex.
                int light = cache.Light(n.x, n.y, n.z) + cache.Light(a.x, a.y, a.z) +
                    cache.Light(b.x, b.y, b.z) + cache.Light(d.x, d.y, d.z);
                result.Sky |= (uint)((light * 17 + 2) / 4) << (c * 8);
            }
            return result;
        }

        static int Solid(ChunkData chunk, int ox, int oz, int x, int y, int z, Func<int,int,int,ushort> neighbor)
        {
            return BlockRegistry.Occludes(ChunkData.InBounds(x, y, z)
                ? chunk.GetBlock(x, y, z) : neighbor(ox + x, y, oz + z)) ? 1 : 0;
        }

        static void AddIndices(ChunkMeshData data, ushort id, bool solid, int start)
        {
            AddFace(data.Triangles, start);
            if (id != 15)
                AddFace(id == 44 ? data.TransparentTriangles :
                    id == BlockRegistry.WaterId ? data.WaterTriangles : data.OpaqueTriangles, start);
            if (solid) AddFace(data.ColliderTriangles, start);
        }

        static void AddFace(List<int> indices, int start)
        {
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
        }
    }
}
