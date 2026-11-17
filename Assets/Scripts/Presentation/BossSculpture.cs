using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.Gameplay;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Builds one coherent surface-culled voxel sculpt from pure <see cref="SculptData"/>:
    /// every articulated part becomes a single mesh (no per-voxel GameObjects, no hidden
    /// internal faces), textured through the shared PBR voxel material by layer index,
    /// tinted and emissive through vertex colours exactly like the Root Guardian sculpture.
    /// </summary>
    public sealed class BossSculpture : MonoBehaviour
    {
        readonly Dictionary<string, Transform> parts = new Dictionary<string, Transform>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly Dictionary<Mesh, Color[]> baseColors = new Dictionary<Mesh, Color[]>();
        Light localLight;
        Material shared;

        public Transform FindPart(string name)
        {
            Transform t;
            return parts.TryGetValue(name, out t) ? t : null;
        }

        public bool HasPart(string name) { return parts.ContainsKey(name); }

        public bool HasPartPrefix(string prefix)
        {
            foreach (var key in parts.Keys) if (key.StartsWith(prefix)) return true;
            return false;
        }

        public static Material SharedVoxelMaterial()
        {
            var mat = Resources.Load<Material>("Generated/VoxelObjects");
            if (!mat)
            {
                var shader = Shader.Find("LestingCraft/VoxelObjects");
                if (shader) mat = new Material(shader) { name = "Sculpt voxel PBR fallback" };
            }
            return mat;
        }

        public static BossSculpture Build(Transform parent, SculptData data, Material material)
        {
            var host = new GameObject("Sculpt_" + data.Name);
            if (parent) host.transform.SetParent(parent, false);
            var sculpt = host.AddComponent<BossSculpture>();
            sculpt.shared = material ? material : SharedVoxelMaterial();
            sculpt.BuildParts(data);
            return sculpt;
        }

        void BuildParts(SculptData data)
        {
            float unit = data.Unit;
            var layerIndex = new Dictionary<string, int>();
            for (int i = 0; i < data.Palette.Length; i++)
            {
                int idx = System.Array.IndexOf(SurfaceLayers.Names, data.Palette[i].Layer);
                layerIndex[data.Palette[i].Name] = idx < 0 ? 0 : idx;
            }
            for (int p = 0; p < data.Parts.Length; p++)
            {
                var part = data.Parts[p];
                if (part.Cells == null || part.Cells.Length == 0) continue;
                var occupied = new Dictionary<long, int>(part.Cells.Length / 4);
                for (int i = 0; i < part.Cells.Length; i += 4)
                {
                    long key = ((long)(part.Cells[i] + 512) << 40) | ((long)(part.Cells[i + 1] + 512) << 20) | (long)(part.Cells[i + 2] + 512);
                    occupied[key] = part.Cells[i + 3];
                }
                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var uvs = new List<Vector3>();
                var tangents = new List<Vector4>();
                var colors = new List<Color>();
                var triangles = new List<int>();
                foreach (var pair in occupied)
                {
                    int cx = (int)((pair.Key >> 40) & 0x3ff) - 512;
                    int cy = (int)((pair.Key >> 20) & 0x3ff) - 512;
                    int cz = (int)(pair.Key & 0x3ff) - 512;
                    var pal = data.Palette[pair.Value];
                    var tint = new Color(Linear(pal.R), Linear(pal.G), Linear(pal.B), pal.Emission);
                    int layer = layerIndex[pal.Name];
                    for (int f = 0; f < 6; f++)
                    {
                        var dir = BlockGeometry.Directions[f];
                        long nk = ((long)(cx + dir.x + 512) << 40) | ((long)(cy + dir.y + 512) << 20) | (long)(cz + dir.z + 512);
                        if (occupied.ContainsKey(nk)) continue;
                        int start = vertices.Count;
                        var tangent = BlockGeometry.Corners[f][3] - BlockGeometry.Corners[f][0];
                        for (int k = 0; k < 4; k++)
                        {
                            Vector3 world = new Vector3(cx + BlockGeometry.Corners[f][k].x,
                                cy + BlockGeometry.Corners[f][k].y, cz + BlockGeometry.Corners[f][k].z) * unit;
                            vertices.Add(world - new Vector3(part.PX, part.PY, part.PZ));
                            normals.Add(dir);
                            colors.Add(tint);
                            Vector2 t = f < 2 ? new Vector2(world.z, world.y) : f < 4 ? new Vector2(world.x, world.z) : new Vector2(world.x, world.y);
                            uvs.Add(new Vector3(t.x * .8f, t.y * .8f, layer));
                            tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, -1));
                        }
                        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                    }
                }
                if (vertices.Count == 0) continue;
                var mesh = new Mesh { name = data.Name + " " + part.Name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTangents(tangents);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                meshes.Add(mesh);
                baseColors[mesh] = colors.ToArray();
                var go = new GameObject("Part_" + part.Name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(part.PX, part.PY, part.PZ);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = shared;
                parts[part.Name] = go.transform;
            }
            if (data.LightRange > 0)
            {
                var lamp = new GameObject("Sculpt local light");
                lamp.transform.SetParent(transform, false);
                lamp.transform.localPosition = new Vector3(data.LightX, data.LightY, data.LightZ);
                localLight = lamp.AddComponent<Light>();
                localLight.type = LightType.Point;
                localLight.color = new Color(data.LightR, data.LightG, data.LightB);
                localLight.range = data.LightRange;
                localLight.intensity = data.LightIntensity;
                localLight.shadows = LightShadows.Soft;
                localLight.shadowBias = .03f;
            }
        }

        static float Linear(float v) { return v <= .04045f ? v / 12.92f : (float)Mathf.Pow((v + .055f) / 1.055f, 2.4f); }

        /// <summary>Multiplies emissive strength of every palette entry (hit flash, core pulse).</summary>
        public void SetEmissionBoost(float boost)
        {
            for (int i = 0; i < meshes.Count; i++)
            {
                var mesh = meshes[i];
                var baseCol = baseColors[mesh];
                var cur = mesh.colors;
                if (cur == null || cur.Length != baseCol.Length) cur = (Color[])baseCol.Clone();
                for (int k = 0; k < cur.Length; k++) cur[k].a = baseCol[k].a * boost;
                mesh.colors = cur;
            }
            if (localLight) localLight.intensity = localLight.intensity; // light handled by caller pulse
        }

        public float LightIntensity
        {
            get { return localLight ? localLight.intensity : 0f; }
            set { if (localLight) localLight.intensity = value; }
        }

        void OnDestroy()
        {
            for (int i = 0; i < meshes.Count; i++) if (meshes[i]) Destroy(meshes[i]);
            meshes.Clear();
        }
    }
}
