using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.Gameplay;

namespace LestingCraft.Presentation
{
    // Wildlife are real surface-culled voxel sculptures: each species is authored through
    // <see cref="AnimalSculpts"/> (the same Sculptor engine as planetary bosses) and built by
    // <see cref="BossSculpture"/>, so animals read as rounded voxel models instead of flat
    // box drawings. Articulated parts ("body", "head", "leg0..N", "tail", "wingL/R", "earL/R")
    // are the pivots this class animates; rotation belongs to transforms, never to mesh vertices.
    public sealed class AnimalVisual : MonoBehaviour
    {
        public readonly List<Transform> Legs = new List<Transform>();
        public readonly List<Transform> Wings = new List<Transform>();
        public Transform Head;
        public int Species { get { return species; } }

        public static readonly string[] SpeciesFurNames = AnimalSculpts.SpeciesFurNames;
        public static readonly string[] SpeciesNames = AnimalSculpts.SpeciesNames;

        BossSculpture sculpt;
        Transform rig, body, tail;
        readonly List<Transform> ears = new List<Transform>();
        int species;
        Vector3 last;
        float elapsed, filteredSpeed, animationClock;

        /// <summary>Canonical coat tint per species, exposed for callers that need it.</summary>
        public Color CoatTint => AnimalSculpts.CoatTints[species];

        static Material SharedMaterial
        {
            get { return BossSculpture.SharedVoxelMaterial(); }
        }

        static int Surface(string name)
        {
            int layer = Array.IndexOf(SurfaceLayers.Names, name);
            if (layer < 0) throw new InvalidOperationException("Missing animal material: " + name);
            return layer;
        }

        public static int FurLayerForSpecies(int kind)
        {
            if (kind < 0 || kind >= SpeciesFurNames.Length) throw new ArgumentOutOfRangeException(nameof(kind));
            return Surface(SpeciesFurNames[kind]);
        }

        public void Build(int kind)
        {
            if (sculpt) throw new InvalidOperationException("An animal visual is built once and then pooled.");
            species = Mathf.Clamp(kind, 0, SpeciesNames.Length - 1);
            int fur = FurLayerForSpecies(kind);
            if (fur < 0) throw new InvalidOperationException("Invalid coat layer for species " + kind);
            var data = AnimalSculpts.For(species);
            sculpt = BossSculpture.Build(transform, data, SharedMaterial);
            rig = sculpt.transform;

            // Map the sculpture's named parts onto the animation rig. Missing parts (e.g. a
            // swimmer without legs) simply leave the corresponding list empty.
            Head = sculpt.FindPart("head");
            body = sculpt.FindPart("body");
            tail = sculpt.FindPart("tail");
            for (int i = 0; i < 16; i++)
            {
                Transform leg = sculpt.FindPart("leg" + i);
                if (leg) Legs.Add(leg); else break;
            }
            Transform wl = sculpt.FindPart("wingL"), wr = sculpt.FindPart("wingR");
            if (wl) Wings.Add(wl);
            if (wr) Wings.Add(wr);
            Transform el = sculpt.FindPart("earL"), er = sculpt.FindPart("earR");
            if (el) ears.Add(el);
            if (er) ears.Add(er);

            ResetMotion();
        }

        public void ResetMotion()
        {
            last = transform.position;
            filteredSpeed = 0;
            animationClock = 0;
            if (rig) { rig.localPosition = Vector3.zero; rig.localRotation = Quaternion.identity; rig.localScale = Vector3.one; }
        }

        public void SetViewDistance(float distance)
        {
            if (!rig) return;
            float scale = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(48, 56, distance));
            rig.localScale = Vector3.one * scale;
        }

        public void Animate(float dt) { Animate(dt, 0); }

        public void Animate(float dt, float distance)
        {
            if (dt <= 0 || !rig || !Head) return;
            float speed = new Vector2(transform.position.x - last.x, transform.position.z - last.z).magnitude / Mathf.Max(.001f, dt);
            last = transform.position;
            filteredSpeed = Mathf.Lerp(filteredSpeed, Mathf.Min(speed, 8), 1 - Mathf.Exp(-dt * 12));
            elapsed += dt;
            animationClock += dt;
            float interval = distance > 48 ? .25f : distance > 36 ? .1f : distance > 24 ? .05f : 0;
            if (animationClock < interval) return;
            float step = animationClock;
            animationClock = 0;
            AnimGait += step * Mathf.Clamp(filteredSpeed * 6, 0, 18);
            float g = AnimGait;
            float amplitude = Mathf.Min(34, filteredSpeed * 12);
            bool hopper = species == 1 || species == 10 || species == 21;
            bool flyer = species == 11 || species == 12 || species == 19 || species == 25 || species == 26 || species == 33;
            float hop = hopper ? Mathf.Max(0, Mathf.Sin(g)) * (species == 10 ? .2f : .13f) * Mathf.Clamp01(filteredSpeed) : 0;
            rig.localPosition = new Vector3(0, hop, 0);
            for (int i = 0; i < Legs.Count; i++)
            {
                float phase = hopper ? (i < 2 ? 0 : .65f) : i % 2 == 0 ? 0 : Mathf.PI;
                float angle = flyer ? -27 : Mathf.Sin(g + phase) * amplitude;
                Legs[i].localRotation = Quaternion.Euler(angle, 0, 0);
            }
            float waddleRoll = species == 13 ? Mathf.Sin(g) * 7f * Mathf.Clamp01(filteredSpeed) : 0;
            if (body) body.localRotation = Quaternion.Euler(hopper ? Mathf.Sin(g) * amplitude * .12f : 0, 0, waddleRoll);
            // Breathing: a 2% chest swell reads as "alive" even when the animal stands still.
            if (body)
            {
                float breath = 1f + Mathf.Sin(elapsed * 1.9f) * .018f;
                body.localScale = new Vector3(1f, breath, 1f);
            }
            Head.localRotation = Quaternion.Euler(Mathf.Sin(elapsed * .7f) * 3,
                Mathf.Sin(elapsed * .4f) * (species == 12 ? 22f : 7f), 0);
            // Ears twitch independently of the head, a cheap but convincing life cue.
            float twitch = Mathf.Sin(elapsed * 3.7f) * 6f + Mathf.Sin(elapsed * 1.3f) * 3f;
            for (int i = 0; i < ears.Count; i++)
                ears[i].localRotation = Quaternion.Euler(0, 0, (i == 0 ? -1 : 1) * twitch);
            float tailPitch = species == 3 || species == 5 ? 22 : species == 9 ? -8 : 0;
            if (tail) tail.localRotation = Quaternion.Euler(tailPitch, Mathf.Sin(elapsed * 2.1f) * (species == 10 ? 0 : 8), 0);
            if (Wings.Count > 0)
            {
                float rate = species == 19 ? 1.8f : species == 13 ? 4.0f : 5.2f;
                float flapWeight = species == 13 ? Mathf.Clamp01(filteredSpeed) :
                    Mathf.SmoothStep(0, 1, (Mathf.Sin(elapsed * .65f) + .35f) * 1.1f);
                float flap = Mathf.Sin(elapsed * rate) * (species == 19 ? 16 : 27) * flapWeight;
                for (int i = 0; i < Wings.Count; i++)
                {
                    float side = i == 0 ? -1 : 1;
                    Wings[i].localRotation = Quaternion.Euler(0, -side * 5, side * (7 + flap));
                }
            }
        }

        float AnimGait;

        void OnDestroy()
        {
            if (sculpt)
            {
                if (Application.isPlaying) Destroy(sculpt);
                else DestroyImmediate(sculpt);
            }
            sculpt = null;
        }

        // ---- Flat-box part builder kept for settler / NPC rigs (SettlerVisual reuses it). ----
        public sealed class Part
        {
            static readonly Vector3[] D =
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
            };
            static readonly int[][] C =
            {
                new[] {1,2,6,5}, new[] {4,7,3,0}, new[] {7,6,2,3},
                new[] {0,1,5,4}, new[] {5,6,7,4}, new[] {0,3,2,1}
            };
            static readonly Vector3[] Corners =
            {
                new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(0,1,0),
                new Vector3(0,0,1),new Vector3(1,0,1),new Vector3(1,1,1),new Vector3(0,1,1)
            };
            static Material material;
            static bool materialLoaded;

            public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(), uv = new List<Vector3>();
            public readonly List<Vector4> t = new List<Vector4>();
            public readonly List<Color32> c = new List<Color32>();
            public readonly List<int> tri = new List<int>();

            public void Box(Vector3 center, Vector3 size, Color color, int layer)
            {
                for (int f = 0; f < 6; f++)
                {
                    int start = v.Count;
                    Vector3 tangent = Vector3.Scale(Corners[C[f][3]] - Corners[C[f][0]], size).normalized;
                    Color32 shade = color;
                    shade.a = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 local = Vector3.Scale(Corners[C[f][i]] - Vector3.one * .5f, size);
                        Vector3 world = local + center;
                        v.Add(world);
                        n.Add(D[f]);
                        Vector2 t2 = f < 2 ? new Vector2(world.z, world.y) : f < 4 ? new Vector2(world.x, world.z) : new Vector2(world.x, world.y);
                        uv.Add(new Vector3(t2.x * .9f, t2.y * .9f, layer));
                        t.Add(new Vector4(tangent.x, tangent.y, tangent.z, -1));
                        c.Add(shade);
                    }
                    tri.Add(start); tri.Add(start + 1); tri.Add(start + 2);
                    tri.Add(start); tri.Add(start + 2); tri.Add(start + 3);
                }
            }

            internal Mesh CreateMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (v.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetColors(c);
                mesh.SetUVs(0, uv); mesh.SetTangents(t); mesh.SetTriangles(tri, 0);
                mesh.RecalculateBounds();
                return mesh;
            }

            public GameObject Build(string name, Transform parent, Vector3 pos)
            {
                Mesh mesh = CreateMesh(name);
                var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                g.transform.SetParent(parent, false);
                g.transform.localPosition = pos;
                g.GetComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = g.GetComponent<MeshRenderer>();
                if (!materialLoaded)
                {
                    material = Resources.Load<Material>("Generated/VoxelObjects");
                    if (!material) material = BossSculpture.SharedVoxelMaterial();
                    materialLoaded = true;
                }
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                g.AddComponent<AnimalMeshLifetime>().Mesh = mesh;
                return g;
            }
        }
    }

    public sealed class AnimalMeshLifetime : MonoBehaviour
    {
        public Mesh Mesh;
        void OnDestroy()
        {
            if (!Mesh) return;
            if (Application.isPlaying) Destroy(Mesh);
            else DestroyImmediate(Mesh);
        }
    }
}
