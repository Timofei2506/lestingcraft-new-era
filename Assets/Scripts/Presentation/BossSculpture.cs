using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LestingCraft.Presentation
{
    [Serializable] public sealed class BossPalette { public string name; public int layer; public float[] tint; public float emission; }
    [Serializable] public sealed class BossPart { public string name; public int[] pivot; public int[] cells; public float[] rotation; }
    [Serializable] public sealed class BossSculptDefinition
    {
        public string key; public string name; public float unit; public BossPalette[] palette;
        public int[] cells; public BossPart[] parts;
        // Anchors arrive as a flat name/cell list so JsonUtility can read a dictionary-free shape.
        public string[] anchorNames; public int[] anchorCells;
    }
    [Serializable] sealed class BossSculptDocument { public int version; public BossSculptDefinition[] sculpts; }

    /// <summary>
    /// Surface-culled voxel sculpture for one boss archetype: body plus separately animated parts
    /// (head, jaw, arms, halo). One mesh per part, shared material, no per-voxel GameObjects.
    /// The same JSON is rendered by the offline preview so art can be judged before a build.
    /// </summary>
    public sealed class BossSculpture : MonoBehaviour
    {
        public sealed class Built
        {
            public GameObject Root;
            public BossSculptDefinition Definition;
            public readonly List<Transform> Parts = new List<Transform>();
            public readonly Dictionary<string, Vector3> Anchors = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            public Vector3 BoundsMin, BoundsMax;
            public Transform Find(string name) { for (int i = 0; i < Parts.Count; i++) if (Parts[i] && Parts[i].name == name) return Parts[i]; return null; }
        }

        static BossSculptDocument document;

        public static BossSculptDefinition Definition(string key)
        {
            if (document == null) document = Parse(RequireCatalogue());
            var all = document.sculpts;
            for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].key == key) return all[i];
            throw new InvalidOperationException("No boss sculpture for archetype " + key);
        }

        static string RequireCatalogue()
        {
            var asset = Resources.Load<TextAsset>("Models/BossSculpts");
            if (!asset) throw new InvalidOperationException("Boss sculpture catalogue missing");
            return asset.text;
        }

        /// <summary>
        /// The catalogue is a single line of JSON (hundreds of KB). JsonUtility's nesting limit is
        /// reached on one line, so sculpts are split into per-archetype objects and parsed one by one.
        /// </summary>
        static BossSculptDocument Parse(string json)
        {
            var result = new BossSculptDocument { sculpts = new BossSculptDefinition[0] };
            int at = json.IndexOf("\"sculpts\":", StringComparison.Ordinal);
            if (at < 0) throw new InvalidOperationException("Boss sculpture catalogue malformed");
            int open = json.IndexOf('[', at);
            int close = json.LastIndexOf(']');
            if (open < 0 || close <= open) throw new InvalidOperationException("Boss sculpture catalogue malformed");
            var list = new List<BossSculptDefinition>(5);
            int i = open + 1;
            while (i < close)
            {
                int start = json.IndexOf('{', i);
                if (start < 0 || start >= close) break;
                int end = MatchBrace(json, start);
                if (end < 0) throw new InvalidOperationException("Boss sculpture object truncated");
                string chunk = json.Substring(start, end - start + 1);
                int anchors = chunk.IndexOf(",\"anchors\":", StringComparison.Ordinal);
                if (anchors > 0) chunk = chunk.Substring(0, anchors) + "}";
                list.Add(JsonUtility.FromJson<BossSculptDefinition>(chunk));
                i = end + 1;
            }
            result.sculpts = list.ToArray();
            if (result.sculpts.Length == 0) throw new InvalidOperationException("Boss sculpture catalogue is empty");
            return result;
        }

        static int MatchBrace(string json, int start)
        {
            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        /// <summary>True when the catalogue parses and every sculpt keeps its palette indices in range.</summary>
        public static bool Validate(string json, out string error)
        {
            error = "";
            BossSculptDocument parsed;
            try { parsed = Parse(json); }
            catch (Exception exception) { error = exception.Message; return false; }
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sculpt in parsed.sculpts)
            {
                if (sculpt == null) { error = "null sculpt"; return false; }
                if (string.IsNullOrEmpty(sculpt.key) || !keys.Add(sculpt.key)) { error = "duplicate or empty key: " + sculpt.key; return false; }
                if (sculpt.unit <= 0f) { error = "bad unit in " + sculpt.key; return false; }
                if (sculpt.palette == null || sculpt.palette.Length == 0) { error = "empty palette in " + sculpt.key; return false; }
                if (!CellsValid(sculpt.cells, sculpt.palette.Length, sculpt.key, "body", ref error)) return false;
                if (sculpt.parts != null)
                    foreach (var part in sculpt.parts)
                    {
                        if (part == null || string.IsNullOrEmpty(part.name)) { error = "unnamed part in " + sculpt.key; return false; }
                        if (!CellsValid(part.cells, sculpt.palette.Length, sculpt.key, part.name, ref error)) return false;
                        if (part.pivot == null || part.pivot.Length != 3) { error = "bad pivot in " + sculpt.key + "/" + part.name; return false; }
                    }
                if (sculpt.anchorNames == null || sculpt.anchorCells == null ||
                    sculpt.anchorCells.Length != sculpt.anchorNames.Length * 3) { error = "anchors in " + sculpt.key; return false; }
            }
            return true;
        }

        static bool CellsValid(int[] cells, int palette, string key, string part, ref string error)
        {
            if (cells == null || cells.Length == 0 || cells.Length % 4 != 0) { error = "empty cells in " + key + "/" + part; return false; }
            for (int i = 0; i < cells.Length; i += 4)
                if (cells[i + 3] < 0 || cells[i + 3] >= palette) { error = "palette index out of range in " + key + "/" + part; return false; }
            return true;
        }

        public static Built Build(Transform parent, string key, Material shared)
        {
            return Build(parent, Definition(key), shared);
        }

        /// <summary>Same build path, but from a definition the caller already parsed (tests, tools).</summary>
        public static Built Build(Transform parent, BossSculptDefinition definition, Material shared)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var go = new GameObject(definition.name + " · voxel sculpt", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var built = new Built { Root = go, Definition = definition };
            var body = CreateGeometry(definition.unit, definition.cells, definition.palette, true);
            Assign(go, body, shared);

            if (definition.parts != null)
            {
                for (int i = 0; i < definition.parts.Length; i++)
                {
                    var part = definition.parts[i];
                    if (part == null || part.cells == null || part.cells.Length == 0) continue;
                    var pivot = new GameObject(part.name);
                    pivot.transform.SetParent(go.transform, false);
                    pivot.transform.localPosition = Cell(part.pivot, definition.unit);
                    var meshGo = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
                    meshGo.transform.SetParent(pivot.transform, false);
                    // Geometry is authored in model space: cancel the pivot offset so parts stay aligned.
                    meshGo.transform.localPosition = -pivot.transform.localPosition;
                    Assign(meshGo, CreateGeometry(definition.unit, part.cells, definition.palette, false), shared);
                    built.Parts.Add(pivot.transform);
                }
            }

            if (definition.anchorNames != null && definition.anchorCells != null)
                for (int i = 0; i < Mathf.Min(definition.anchorNames.Length, definition.anchorCells.Length / 3); i++)
                    built.Anchors[definition.anchorNames[i]] = new Vector3(definition.anchorCells[i * 3], definition.anchorCells[i * 3 + 1], definition.anchorCells[i * 3 + 2]) * definition.unit;

            built.BoundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            built.BoundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.sharedMesh) continue;
                built.BoundsMin = Vector3.Min(built.BoundsMin, filter.sharedMesh.bounds.min);
                built.BoundsMax = Vector3.Max(built.BoundsMax, filter.sharedMesh.bounds.max);
            }
            // Palette colours already come from BossCatalog; the block is reserved for hit flashes.
            return built;
        }

        static Vector3 Cell(int[] pivot, float unit)
        {
            if (pivot == null || pivot.Length < 3) return Vector3.zero;
            return new Vector3(pivot[0], pivot[1], pivot[2]) * unit;
        }

        static void Assign(GameObject go, Mesh mesh, Material shared)
        {
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = shared;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            go.AddComponent<SculptMeshLifetime>().Mesh = mesh;
        }

        public static Mesh CreateGeometry(float unit, int[] flat, BossPalette[] palette, bool body)
        {
            if (flat == null || flat.Length % 4 != 0) throw new InvalidOperationException("Malformed sculpture cells");
            var occupied = new Dictionary<Vector3Int, int>(flat.Length / 4);
            for (int i = 0; i < flat.Length; i += 4)
            {
                int index = flat[i + 3];
                if (index < 0 || index >= palette.Length) throw new InvalidOperationException("Invalid sculpture palette index");
                occupied[new Vector3Int(flat[i], flat[i + 1], flat[i + 2])] = index;
            }
            var vertices = new List<Vector3>(flat.Length);
            var normals = new List<Vector3>(flat.Length);
            var uv = new List<Vector3>(flat.Length);
            var tangents = new List<Vector4>(flat.Length);
            var colors = new List<Color>(flat.Length);
            var triangles = new List<int>(flat.Length * 3 / 2);
            foreach (var pair in occupied)
                for (int face = 0; face < 6; face++)
                {
                    var normal = BlockGeometry.Directions[face];
                    if (occupied.ContainsKey(pair.Key + normal)) continue;
                    int start = vertices.Count;
                    var entry = palette[pair.Value];
                    Color tint = new Color(Linear(entry.tint[0]), Linear(entry.tint[1]), Linear(entry.tint[2]), entry.emission);
                    var tangent = BlockGeometry.Corners[face][3] - BlockGeometry.Corners[face][0];
                    for (int k = 0; k < 4; k++)
                    {
                        Vector3 v = ((Vector3)pair.Key + BlockGeometry.Corners[face][k]) * unit;
                        vertices.Add(v);
                        normals.Add(normal);
                        colors.Add(tint);
                        // Continuous metre-space grain so a limb is one material, not a stack of tiles.
                        Vector2 t = face < 2 ? new Vector2(v.z, v.y) : face < 4 ? new Vector2(v.x, v.z) : new Vector2(v.x, v.y);
                        uv.Add(new Vector3(t.x * .8f, t.y * .8f, entry.layer));
                        tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, -1f));
                    }
                    foreach (int index in new[] { 0, 1, 2, 0, 2, 3 }) triangles.Add(start + index);
                }
            var mesh = new Mesh
            {
                name = body ? "Boss sculpt body" : "Boss sculpt part",
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTangents(tangents);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static float Linear(float v) { return v <= .04045f ? v / 12.92f : (float)Math.Pow((v + .055f) / 1.055f, 2.4); }

        /// <summary>Boss vitals for the HUD: highest point and chest core in world space.</summary>
        public static float Height(Built built) { return built == null ? 0f : built.BoundsMax.y - built.BoundsMin.y; }
    }

    public sealed class SculptMeshLifetime : MonoBehaviour
    {
        public Mesh Mesh;
        void OnDestroy()
        {
            if (!Mesh) return;
            if (Application.isPlaying) Destroy(Mesh); else DestroyImmediate(Mesh);
        }
    }
}
