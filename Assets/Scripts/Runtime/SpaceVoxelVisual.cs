using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.Gameplay;

namespace LestingCraft.Runtime
{
    /// <summary>Renders the exact approved 3D voxel source with procedural HD spacecraft PBR hull maps,
    /// tangent-space normals, world-aligned UVs, and corner vertex ambient occlusion.</summary>
    public sealed class SpaceVoxelVisual : MonoBehaviour
    {
        Mesh mesh;
        Material[] materials;

        static Texture2D sharedTileAlbedo, sharedTileNormal;
        static Texture2D sharedMetalAlbedo, sharedMetalNormal;
        static Texture2D sharedSolarAlbedo, sharedSolarNormal;

        static readonly Vector3Int[] Directions =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };

        static readonly Vector3[][] Corners =
        {
            new[]{new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(1,1,1),new Vector3(1,0,1)},
            new[]{new Vector3(0,0,1),new Vector3(0,1,1),new Vector3(0,1,0),new Vector3(0,0,0)},
            new[]{new Vector3(0,1,1),new Vector3(1,1,1),new Vector3(1,1,0),new Vector3(0,1,0)},
            new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,0,1),new Vector3(0,0,1)},
            new[]{new Vector3(0,0,1),new Vector3(1,0,1),new Vector3(1,1,1),new Vector3(0,1,1)},
            new[]{new Vector3(1,0,0),new Vector3(0,0,0),new Vector3(0,1,0),new Vector3(1,1,0)}
        };

        static void EnsureHullPbrTextures()
        {
            if (sharedTileAlbedo != null) return;
            const int S = 256;

            // 1. Ceramic TPS Thermal Tile (4x3 staggered tiles per meter with beveled grout seams & micro-grain)
            BuildHullPair(S, "ShuttleTPS", (u, v) =>
            {
                int row = Mathf.FloorToInt(v * 4f);
                float su = (u * 3f + (row % 2) * 0.5f) % 1f;
                float sv = (v * 4f) % 1f;
                float edge = Mathf.Min(Mathf.Min(su, 1f - su), Mathf.Min(sv, 1f - sv));
                float seam = Mathf.Clamp01(edge / 0.075f);
                int th = ((row * 73856093) ^ (Mathf.FloorToInt(u * 3f + (row % 2) * 0.5f) * 19349663)) & 0x7fffffff;
                float tileVar = 0.90f + ((th % 29) / 29f) * 0.10f;
                float grain = 0.96f + 0.04f * Mathf.Sin(u * 190f + v * 170f);
                float lum = Mathf.Lerp(0.54f, tileVar * grain, seam);
                return new Vector2(lum, seam);
            }, 2.2f, out sharedTileAlbedo, out sharedTileNormal);

            // 2. Riveted Aerospace Duralumin / Structural Steel Plates
            BuildHullPair(S, "AerospaceHull", (u, v) =>
            {
                float pu = (u * 2f) % 1f, pv = (v * 2f) % 1f;
                float edge = Mathf.Min(Mathf.Min(pu, 1f - pu), Mathf.Min(pv, 1f - pv));
                float panelSeam = Mathf.Clamp01(edge / 0.045f);
                float rivetRow = Mathf.Min(Mathf.Abs(pu - 0.09f), Mathf.Abs(pu - 0.91f));
                float rivetCol = Mathf.Abs(((pv * 8f) % 1f) - 0.5f);
                bool rivet = (rivetRow < 0.03f && rivetCol < 0.14f) || (Mathf.Min(Mathf.Abs(pv - 0.09f), Mathf.Abs(pv - 0.91f)) < 0.03f && Mathf.Abs(((pu * 8f) % 1f) - 0.5f) < 0.14f);
                float brush = 0.93f + 0.07f * Mathf.Sin(v * 260f + Mathf.Sin(u * 18f) * 2f);
                float hgt = panelSeam * (rivet ? 1.25f : 0.85f);
                float lum = Mathf.Lerp(0.62f, brush * (rivet ? 1.06f : 0.98f), panelSeam);
                return new Vector2(lum, hgt);
            }, 2.4f, out sharedMetalAlbedo, out sharedMetalNormal);

            // 3. Photovoltaic Wafer Array / Gold Kapton MLI Grid
            BuildHullPair(S, "SolarKapton", (u, v) =>
            {
                float cu = (u * 6f) % 1f, cv = (v * 6f) % 1f;
                bool busBar = cu < 0.06f || cv < 0.06f;
                bool fineFinger = ((cv * 5f) % 1f) < 0.10f;
                float lum = busBar ? 1.18f : fineFinger ? 1.02f : 0.84f;
                float hgt = busBar ? 1.0f : fineFinger ? 0.72f : 0.55f;
                return new Vector2(lum, hgt);
            }, 2.0f, out sharedSolarAlbedo, out sharedSolarNormal);
        }

        static void BuildHullPair(int S, string tag, Func<float, float, Vector2> fn, float normalStrength, out Texture2D albedo, out Texture2D normal)
        {
            var alb = new Color32[S * S];
            var hgt = new float[S * S];
            for (int y = 0; y < S; y++)
            {
                float v = (y + 0.5f) / S;
                for (int x = 0; x < S; x++)
                {
                    Vector2 r = fn((x + 0.5f) / S, v);
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(r.x * 255f), 0, 255);
                    alb[y * S + x] = new Color32(b, b, b, 255);
                    hgt[y * S + x] = r.y;
                }
            }
            var nrm = new Color32[S * S];
            for (int y = 0; y < S; y++)
            {
                int ym = (y - 1 + S) % S, yp = (y + 1) % S;
                for (int x = 0; x < S; x++)
                {
                    int xm = (x - 1 + S) % S, xp = (x + 1) % S;
                    float dx = (hgt[y * S + xp] - hgt[y * S + xm]) * normalStrength;
                    float dy = (hgt[yp * S + x] - hgt[ym * S + x]) * normalStrength;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    nrm[y * S + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 128, 255),
                        255);
                }
            }
            albedo = new Texture2D(S, S, TextureFormat.RGBA32, true)
            {
                name = tag + "_Albedo", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8,
                hideFlags = HideFlags.DontSave
            };
            albedo.SetPixels32(alb);
            albedo.Apply(true, false);

            normal = new Texture2D(S, S, TextureFormat.RGBA32, true, true)
            {
                name = tag + "_Normal", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8,
                hideFlags = HideFlags.DontSave
            };
            normal.SetPixels32(nrm);
            normal.Apply(true, false);
        }

        public static SpaceVoxelVisual Create(string assetName, Transform parent, bool collision = false)
        {
            if (assetName != "Shuttle" && assetName != "LaunchPad" && assetName != "Station")
                throw new ArgumentException("Unknown approved model");
            var resource = Resources.Load<TextAsset>("Space/" + assetName);
            if (!resource) throw new InvalidOperationException("Missing approved voxel model: " + assetName);
            var data = SpaceVoxelAsset.Read(resource.bytes);
            var obj = new GameObject("Approved space model · " + assetName);
            obj.transform.SetParent(parent, false);
            var visual = obj.AddComponent<SpaceVoxelVisual>();
            visual.Build(data, collision);
            return visual;
        }

        /// <summary>Derives crew decks, tower hatch, and real panoramic cockpit windows for
        /// runtime flight visibility while keeping ShuttleCabinCarving.Make pure for asset tests.</summary>
        public static SpaceVoxelVisual CreateWalkableShuttle(Transform parent)
        {
            var resource = Resources.Load<TextAsset>("Space/Shuttle");
            if (!resource) throw new InvalidOperationException("Missing approved shuttle model");
            var data = ShuttleCabinCarving.Make(SpaceVoxelAsset.Read(resource.bytes));
            data = data.RemoveCells((gx, y, gz) => IsRuntimeWindowCutout(gx - ShuttleCabinCarving.ModelCenterX, y, gz - ShuttleCabinCarving.ModelCenterZ));
            var obj = new GameObject("Approved Shuttle · fitted crew volume");
            obj.transform.SetParent(parent, false);
            var visual = obj.AddComponent<SpaceVoxelVisual>();
            visual.Build(data, true);
            return visual;
        }

        static bool IsRuntimeWindowCutout(int lx, int y, int lz)
        {
            if (y >= 43 && y <= 47 && lx >= -4 && lx <= 4 && lz >= 5) return true;
            if (y == 47 && lx >= -2 && lx <= 2 && lz >= 4) return true;
            if (y >= 43 && y <= 46 && (lx <= -3 || lx >= 3) && lz >= 2 && lz <= 6) return true;
            if (y >= 30 && y <= 31 && lx >= 3 && lz >= 3 && lz <= 4) return true;
            return false;
        }

        public static SpaceVoxelVisual CreateWalkableLaunchPad(Transform parent)
        {
            var resource = Resources.Load<TextAsset>("Space/LaunchPad");
            if (!resource) throw new InvalidOperationException("Missing approved launch pad model");
            var data = LaunchPadLiftOpening.Make(SpaceVoxelAsset.Read(resource.bytes));
            var obj = new GameObject("Approved LaunchPad · lift well and tower doorway");
            obj.transform.SetParent(parent, false);
            var visual = obj.AddComponent<SpaceVoxelVisual>();
            visual.Build(data, true);
            return visual;
        }

        void Build(SpaceVoxelAsset data, bool collision)
        {
            EnsureHullPbrTextures();

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>[data.Palette.Length];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = new List<int>();
            Vector3 offset = new Vector3(data.X * .5f, 0, data.Z * .5f);

            for (int x = 0; x < data.X; x++)
            for (int y = 0; y < data.Y; y++)
            for (int z = 0; z < data.Z; z++)
            {
                byte cell = data.At(x, y, z);
                if (cell == 0) continue;
                for (int face = 0; face < 6; face++)
                {
                    var dir = Directions[face];
                    if (data.At(x + dir.x, y + dir.y, z + dir.z) != 0) continue;
                    int start = vertices.Count;
                    for (int c = 0; c < 4; c++)
                    {
                        Vector3 corner = new Vector3(x, y, z) + Corners[face][c];
                        vertices.Add(corner - offset);
                        normals.Add(dir);
                        Vector2 uv = face < 2
                            ? new Vector2(corner.z * 0.5f, corner.y * 0.5f)
                            : face < 4
                                ? new Vector2(corner.x * 0.5f, corner.z * 0.5f)
                                : new Vector2(corner.x * 0.5f, corner.y * 0.5f);
                        uvs.Add(uv);
                    }
                    var group = triangles[cell];
                    group.Add(start); group.Add(start + 1); group.Add(start + 2);
                    group.Add(start); group.Add(start + 2); group.Add(start + 3);
                }
            }

            mesh = new Mesh { name = "Approved authored voxels", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i, false);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (!shader) throw new InvalidOperationException("Missing surface shader");
            materials = new Material[data.Palette.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                var p = data.Palette[i];
                var material = new Material(shader) { name = "Space · " + p.Name };
                bool isWindow = p.Name.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isSolar = p.Name.IndexOf("solar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               p.Name.IndexOf("gold", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               p.Name.IndexOf("foil", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isSteel = p.Name.IndexOf("steel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               p.Name.IndexOf("nozzle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               p.Name.IndexOf("frame", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isWindow)
                {
                    Color glassCol = new Color(0.52f, 0.82f, 0.96f, 0.14f);
                    material.color = glassCol;
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", glassCol);
                    if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                    if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
                    material.SetInt("_SrcBlend", 5);
                    material.SetInt("_DstBlend", 10);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = 3000;
                    material.SetFloat("_Smoothness", 0.92f);
                }
                else
                {
                    Color baseCol = new Color32(p.R, p.G, p.B, p.A);
                    Texture2D albMap = isSolar ? sharedSolarAlbedo : isSteel ? sharedMetalAlbedo : sharedTileAlbedo;
                    Texture2D nrmMap = isSolar ? sharedSolarNormal : isSteel ? sharedMetalNormal : sharedTileNormal;
                    material.color = baseCol;
                    material.mainTexture = albMap;
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseCol);
                    if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albMap);
                    if (material.HasProperty("_BumpMap"))
                    {
                        material.SetTexture("_BumpMap", nrmMap);
                        material.EnableKeyword("_NORMALMAP");
                    }
                    material.SetFloat("_Metallic", isSolar ? 0.82f : isSteel ? 0.72f : 0.12f);
                    material.SetFloat("_Smoothness", isSolar ? 0.78f : isSteel ? 0.56f : 0.36f);
                }
                materials[i] = material;
            }
            gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
            if (collision) gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        void OnDestroy()
        {
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            if (materials != null)
                foreach (var material in materials)
                    if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
