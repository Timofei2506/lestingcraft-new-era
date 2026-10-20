using UnityEngine;
using LestingCraft.World;

namespace LestingCraft.Presentation
{
    public static class SurfaceDetailMesher
    {
        public static void AddGrass(ChunkMeshData mesh, int x, int y, int z, int ox, int oz, int seed, int quality = 0)
        {
            uint hash = TerrainGenerator.Hash(ox + x, y, oz + z, seed ^ 9371);
            if (hash % 7 == 0) return;
            // Ultra increases blade density AND curvature while keeping full 16x16 quality-0 meadow well below 10,000 indices.
            int count = quality > 0 ? 5 + (int)(hash % 2) : 2 + (int)(hash % 2);
            int segments = quality > 0 ? 3 : 2;
            for (int blade = 0; blade < count; blade++)
            {
                uint h = TerrainGenerator.Hash(ox + x, blade + 17, oz + z, seed);
                float angle = (h & 1023) / 1023f * Mathf.PI * 2;
                float height = .16f + ((h >> 10) & 255) / 255f * .30f;
                float width = .009f + ((h >> 18) & 63) / 63f * .012f;
                var side = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var forward = new Vector3(-side.z, 0, side.x);
                float spread = ((h >> 24) & 127) / 127f;
                var root = new Vector3(x + .10f + ((h >> 4) & 255) / 255f * .80f,
                    y + 1.003f, z + .10f + ((h >> 12) & 255) / 255f * .80f);
                int first = mesh.Vertices.Count;
                float lean = .06f + spread * .13f;
                bool flower = hash % 19 == 0 && blade == 0;
                bool clover = !flower && hash % 11 == 0;
                for (int segment = 0; segment <= segments; segment++)
                {
                    float t = (float)segment / segments;
                    float w = width * (1 - t * t);
                    if (clover) w *= 1 + Mathf.Sin(t * Mathf.PI) * .65f;
                    var center = root + Vector3.up * (height * t) + forward * (lean * t * t);
                    Vector3 normal = (forward - Vector3.up * (2 * lean * t / height)).normalized;
                    for (int edge = 0; edge < 2; edge++)
                    {
                        mesh.Vertices.Add(center + side * (edge == 0 ? -w : w));
                        mesh.Normals.Add(normal);
                        mesh.Tangents.Add(new Vector4(side.x, 0, side.z, 1));
                        mesh.UVs.Add(new Vector3(edge, t, root.y));
                        float tint = .78f + (h % 100) / 450f;
                        Color color = new Color(tint, 1, .84f + (h % 17) / 100f, 1);
                        if (clover) color *= new Color(.82f, .97f, .85f, 1);
                        if (flower && t > .55f)
                        {
                            Color accent = (hash & 1) == 0
                                ? new Color(1, .72f, .92f, 1) : new Color(1, .94f, .57f, 1);
                            color = Color.Lerp(color, accent, Mathf.InverseLerp(.55f, .9f, t));
                        }
                        mesh.Colors.Add(color);
                    }
                    if (segment > 0)
                    {
                        int a = first + (segment - 1) * 2;
                        mesh.DetailTriangles.Add(a);
                        mesh.DetailTriangles.Add(a + 1);
                        mesh.DetailTriangles.Add(a + 3);
                        mesh.DetailTriangles.Add(a);
                        mesh.DetailTriangles.Add(a + 3);
                        mesh.DetailTriangles.Add(a + 2);
                    }
                }
            }
        }
    }
}
