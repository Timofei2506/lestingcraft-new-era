using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LestingCraft.Presentation
{
    // All authored geometry consists of flat-shaded boxes. Rotation belongs to
    // articulated transforms, never to mesh normals or baked box vertices.
    public sealed class AnimalVisual : MonoBehaviour
    {
        public readonly List<Transform> Legs = new List<Transform>();
        public readonly List<Transform> Wings = new List<Transform>();
        public Transform Head;
        public int Species { get { return species; } }

        public static readonly string[] SpeciesFurNames =
        {
            "deer-fur","hare-fur","boar-bristles","wolf-fur","bear-fur","fox-fur",
            "bison-fur","goat-fur","lynx-fur","beaver-fur","frog-skin","eagle-feathers"
        };
        public static readonly string[] SpeciesNames =
        {
            "Олень","Заяц","Кабан","Волк","Бурый медведь","Лисица",
            "Зубр","Горный козёл","Рысь","Бобр","Жаба","Орёл"
        };

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

        sealed class CachedMesh
        {
            public Mesh mesh;
            public int references;
        }
        static readonly Dictionary<string, CachedMesh> Meshes = new Dictionary<string, CachedMesh>();

        Transform rig, body, tail;
        readonly List<Transform> wingTips = new List<Transform>();
        readonly List<Vector3> legPositions = new List<Vector3>();
        readonly List<string> ownedMeshes = new List<string>();
        int species, fur, underfur, keratin;
        Vector3 last, headPosition;
        float gait, elapsed, filteredSpeed, animationClock;
        Color coat = Color.white;
        readonly Color cream = new Color(.94f, .89f, .8f);
        readonly Color dark = new Color(.19f, .17f, .15f);

        static Material SharedMaterial
        {
            get
            {
                if (!materialLoaded)
                {
                    material = Resources.Load<Material>("Generated/VoxelObjects");
                    materialLoaded = true;
                }
                return material;
            }
        }

        public sealed class Part
        {
            public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(), uv = new List<Vector3>();
            public readonly List<Vector4> t = new List<Vector4>();
            public readonly List<Color32> c = new List<Color32>();
            public readonly List<int> tri = new List<int>();

            public void Box(Vector3 center, Vector3 size, Color color, int layer)
            {
                for (int f = 0; f < 6; f++)
                {
                    int start = v.Count;
                    Vector3 u = Vector3.Scale(Corners[C[f][3]] - Corners[C[f][0]], size);
                    Vector3 w = Vector3.Scale(Corners[C[f][1]] - Corners[C[f][0]], size);
                    Vector3 tangent = u.normalized;
                    Color32 shade = color;
                    shade.a = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        v.Add(Vector3.Scale(Corners[C[f][i]] - Vector3.one * .5f, size) + center);
                        n.Add(D[f]);
                        uv.Add(new Vector3(i >= 2 ? u.magnitude * 3 : 0,
                            i == 1 || i == 2 ? w.magnitude * 3 : 0, layer));
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
                GameObject g = MeshObject(name, parent, pos, mesh);
                g.AddComponent<AnimalMeshLifetime>().Mesh = mesh;
                return g;
            }
        }

        static GameObject MeshObject(string name, Transform parent, Vector3 position, Mesh mesh)
        {
            var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            g.transform.SetParent(parent, false);
            g.transform.localPosition = position;
            g.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = g.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = SharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return g;
        }

        Transform Attach(string name, Transform parent, Vector3 position, Action<Part> author)
        {
            string key = species + ":" + name;
            CachedMesh entry;
            if (!Meshes.TryGetValue(key, out entry) || !entry.mesh)
            {
                var part = new Part();
                author(part);
                entry = new CachedMesh { mesh = part.CreateMesh("Wildlife/" + key) };
                Meshes[key] = entry;
            }
            entry.references++;
            ownedMeshes.Add(key);
            return MeshObject(name, parent, position, entry.mesh).transform;
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

        void Box(Part p, float x, float y, float z, float w, float h, float d)
        {
            p.Box(new Vector3(x, y, z), new Vector3(w, h, d), coat, fur);
        }

        void Mark(Part p, float x, float y, float z, float w, float h, float d, Color color, int layer)
        {
            p.Box(new Vector3(x, y, z), new Vector3(w, h, d), color, layer);
        }

        public void Build(int kind)
        {
            if (rig) throw new InvalidOperationException("An animal visual is built once and then pooled.");
            fur = FurLayerForSpecies(kind);
            species = kind;
            underfur = Surface("animal-underfur");
            keratin = Surface("animal-keratin");
            rig = new GameObject("Articulated wildlife").transform;
            rig.SetParent(transform, false);

            float[] widths = { .48f, .3f, .62f, .48f, 1.12f, .32f, 1.2f, .43f, .47f, .55f, .62f, .4f };
            float[] lengths = { 1.05f, .45f, 1.08f, 1.15f, 1.6f, .83f, 1.85f, .85f, .88f, .8f, .61f, .65f };
            float[] bodyHeights = { .55f, .3f, .55f, .48f, 1.05f, .36f, 1.05f, .54f, .47f, .43f, .25f, .4f };
            float[] bodyYs = { .94f, .24f, .52f, .72f, .98f, .46f, 1.08f, .78f, .61f, .32f, .22f, .4f };
            float width = widths[kind], length = lengths[kind], bh = bodyHeights[kind], by = bodyYs[kind];
            body = Attach("Body", rig, Vector3.zero, p =>
            {
                Box(p, 0, by, 0, width, bh, length);
                Mark(p, 0, by - bh * .38f, .03f, width * .77f, bh * .23f, length * .8f, cream, underfur);
                if (kind == 0)
                {
                    Box(p, 0, 1.22f, .4f, .27f, .55f, .29f);
                    for (int i = 0; i < 12; i++)
                        Mark(p, (i % 2 == 0 ? -1 : 1) * .246f, .9f + (i / 6) * .17f,
                            -.4f + ((i / 2) % 3) * .28f, .016f, .055f, .09f, cream, underfur);
                }
                if (kind == 2)
                    for (int i = 0; i < 8; i++)
                        Mark(p, 0, .84f, -.45f + i * .12f, .06f, .08f + (i % 2) * .04f, .07f, dark, fur);
                if (kind == 3)
                {
                    Box(p, 0, .8f, .36f, .58f, .62f, .4f);
                    Mark(p, 0, .49f, .53f, .27f, .22f, .18f, cream, underfur);
                }
                if (kind == 4)
                {
                    Box(p, 0, 1.32f, .36f, 1.02f, .62f, .8f);
                    Box(p, 0, .85f, -.6f, .94f, .85f, .44f);
                }
                if (kind == 5)
                    Mark(p, 0, .47f, .37f, .26f, .36f, .18f, Color.white, underfur);
                if (kind == 6)
                {
                    Box(p, 0, 1.52f, .38f, 1.12f, .76f, 1.02f);
                    Box(p, 0, .8f, .6f, 1.22f, .8f, .65f);
                    for (int i = 0; i < 7; i++)
                        Box(p, -.48f + i * .16f, .37f, .62f, .12f, .3f + (i % 2) * .1f, .25f);
                }
                if (kind == 7)
                    Mark(p, 0, .82f, .36f, .36f, .47f, .2f, cream, underfur);
                if (kind == 8)
                    for (int side = -1; side <= 1; side += 2)
                        for (int i = 0; i < 10; i++)
                            Mark(p, side * .24f, .49f + i % 3 * .1f, -.36f + i / 3 * .22f,
                                .018f, .047f, .065f, dark, fur);
                if (kind == 9)
                    Box(p, 0, .34f, -.28f, .61f, .48f, .38f);
                if (kind == 10)
                {
                    Mark(p, 0, .21f, .31f, .36f, .25f, .22f, new Color(.82f, .8f, .42f), underfur);
                    for (int i = 0; i < 8; i++)
                        Mark(p, (i % 2 == 0 ? -1 : 1) * .22f, .36f, -.22f + i / 2 * .12f,
                            .08f, .035f, .075f, dark, fur);
                }
                if (kind == 11)
                {
                    Box(p, 0, .55f, .15f, .33f, .4f, .39f);
                    Mark(p, 0, .31f, .14f, .3f, .2f, .42f, cream * .65f, underfur);
                }
            });

            float[] headYs = { 1.4f, .38f, .62f, .98f, 1.31f, .65f, 1.17f, 1.1f, .82f, .43f, .29f, .68f };
            float[] headWidths = { .25f, .25f, .42f, .32f, .62f, .25f, .69f, .26f, .37f, .39f, .68f, .28f };
            float[] headHeights = { .3f, .23f, .35f, .32f, .48f, .26f, .6f, .32f, .31f, .28f, .2f, .28f };
            headPosition = new Vector3(0, headYs[kind], length * .48f);
            float hw = headWidths[kind], hh = headHeights[kind];
            Head = Attach("Head", rig, headPosition, p =>
            {
                Box(p, 0, 0, 0, hw, hh, kind == 10 ? .39f : .38f);
                if (kind == 11)
                {
                    Mark(p, 0, -.01f, .24f, .15f, .13f, .23f, new Color(.82f, .61f, .2f), keratin);
                    Mark(p, 0, -.095f, .33f, .09f, .14f, .09f, dark, keratin);
                }
                else if (kind != 10)
                {
                    float snout = kind == 3 || kind == 5 ? .32f : kind == 2 ? .29f : .2f;
                    Box(p, 0, -.055f, .23f, hw * .63f, hh * .48f, snout);
                    Mark(p, 0, -.035f, .23f + snout * .5f, hw * .46f, hh * .27f, .035f,
                        kind == 2 ? cream * .7f : dark, keratin);
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    if (kind == 10)
                    {
                        Box(p, side * .23f, .13f, .04f, .18f, .18f, .21f);
                        Mark(p, side * .23f, .15f, .153f, .13f, .12f, .018f, cream, keratin);
                        Mark(p, side * .23f, .15f, .165f, .07f, .095f, .012f, Color.black, keratin);
                        Mark(p, side * .21f, .18f, .175f, .025f, .027f, .01f, Color.white, keratin);
                    }
                    else
                    {
                        float ex = hw * .51f;
                        Mark(p, side * ex, hh * .17f, .095f, .025f, .065f, .075f,
                            new Color(.69f, .5f, .2f), keratin);
                        Mark(p, side * (ex + .014f), hh * .17f, .105f, .012f, .046f, .04f, Color.black, keratin);
                        Mark(p, side * (ex + .022f), hh * .17f + .014f, .116f, .008f, .015f, .017f, Color.white, keratin);
                    }
                    if (kind != 10 && kind != 11)
                    {
                        float earHeight = kind == 1 ? .35f : kind == 3 || kind == 5 ? .23f : .14f;
                        float earWidth = kind == 0 ? .19f : kind == 4 ? .19f : .11f;
                        float ex = hw * .4f;
                        Box(p, side * ex, hh * .5f + earHeight * .3f, -.06f, earWidth, earHeight, .11f);
                        Mark(p, side * ex, hh * .5f + earHeight * .3f, .002f, earWidth * .55f,
                            earHeight * .7f, .015f, kind == 1 ? new Color(1, .82f, .8f) : cream, underfur);
                        if (kind == 3 || kind == 5 || kind == 8)
                            Box(p, side * ex, hh * .5f + earHeight * .8f, -.06f, earWidth * .5f, .075f, .08f);
                        if (kind == 4 || kind == 9)
                            Box(p, side * ex, hh * .5f + earHeight * .82f, -.06f, earWidth * .65f, .045f, .08f);
                    }
                    if (kind == 0)
                    {
                        Mark(p, side * .09f, .36f, -.1f, .045f, .45f, .045f, cream * .65f, keratin);
                        Mark(p, side * .17f, .49f, -.1f, .2f, .04f, .04f, cream * .7f, keratin);
                        for (int i = 0; i < 3; i++)
                            Mark(p, side * (.09f + i * .08f), .59f + i * .035f, -.1f,
                                .035f, .19f, .035f, cream * .7f, keratin);
                    }
                    if (kind == 2)
                    {
                        Mark(p, side * .17f, -.065f, .32f, .055f, .19f, .055f, cream, keratin);
                        Mark(p, side * .17f, .025f, .29f, .045f, .055f, .1f, cream, keratin);
                    }
                    if (kind == 6)
                    {
                        Mark(p, side * .4f, .2f, -.08f, .25f, .1f, .12f, cream, keratin);
                        Mark(p, side * .52f, .29f, -.08f, .09f, .24f, .1f, cream, keratin);
                        Mark(p, side * .49f, .41f, -.08f, .12f, .07f, .08f, dark, keratin);
                    }
                    if (kind == 7)
                        for (int i = 0; i < 5; i++)
                            Mark(p, side * (.1f + i * .012f), .24f + i * .065f, -.08f - i * .065f,
                                .07f - i * .009f, .12f, .13f, cream * .65f, keratin);
                    if (kind == 8)
                    {
                        Mark(p, side * .15f, .38f, -.06f, .035f, .18f, .035f, dark, fur);
                        Box(p, side * .2f, -.06f, -.02f, .17f, .16f, .22f);
                    }
                    if (kind == 9)
                        Mark(p, side * .045f, -.14f, .34f, .07f, .15f, .035f,
                            new Color(1, .52f, .12f), keratin);
                }
                if (kind == 6 || kind == 7)
                    Box(p, 0, -hh * .5f - .13f, .12f, kind == 6 ? .3f : .13f, .29f, .19f);
                if (kind == 10)
                    Mark(p, 0, -.06f, .204f, .53f, .018f, .02f, dark, keratin);
            });

            int legCount = kind == 11 ? 2 : 4;
            float[] legLengths = { .65f, .19f, .36f, .53f, .58f, .32f, .64f, .56f, .43f, .19f, .16f, .2f };
            float ll = legLengths[kind];
            for (int i = 0; i < legCount; i++)
            {
                int legIndex = i;
                bool rear = i < 2;
                float thick = kind == 4 || kind == 6 ? .24f : kind == 0 || kind == 7 ? .095f : .12f;
                Vector3 position = new Vector3((i % 2 == 0 ? -1 : 1) * width * .35f,
                    ll, kind == 11 ? .08f : (rear ? -1 : 1) * length * .32f);
                Transform leg = Attach("Leg" + i, rig, position, p =>
                {
                    Box(p, 0, -ll * .42f, 0, thick, ll * .8f, thick * 1.25f);
                    if ((kind == 1 || kind == 10) && rear)
                    {
                        Box(p, 0, -ll * .2f, -.05f, thick * 1.9f, ll * .85f, .25f);
                        Box(p, 0, -ll * .65f, .06f, thick, .08f, .29f);
                    }
                    Mark(p, 0, -ll * .9f, .045f, thick * 1.22f, .08f,
                        kind == 1 || kind == 10 ? .27f : .18f, kind == 1 ? cream : coat,
                        kind == 1 ? underfur : keratin);
                    if (kind == 4 || kind == 8 || kind == 9 || kind == 10 || kind == 11)
                        for (int toe = 0; toe < 3; toe++)
                            Mark(p, (toe - 1) * thick * .34f, -ll * .93f, .15f,
                                .025f, .035f, kind == 11 ? .19f : .09f,
                                kind == 11 ? new Color(.8f, .6f, .2f) : dark, keratin);
                    if (kind == 3 || kind == 5)
                        Mark(p, 0, -ll * .75f, .015f, thick * 1.05f, ll * .24f, .15f, dark, fur);
                });
                leg.name = "Articulated leg " + legIndex;
                Legs.Add(leg);
                legPositions.Add(position);
            }

            tail = Attach("Tail", rig, new Vector3(0, by, -length * .5f), p =>
            {
                switch (kind)
                {
                    case 0: Box(p, 0, 0, -.1f, .12f, .1f, .23f); break;
                    case 1: Mark(p, 0, 0, -.1f, .19f, .17f, .19f, Color.white, underfur); break;
                    case 2:
                        Box(p, 0, .02f, -.14f, .045f, .05f, .28f);
                        Box(p, .05f, .07f, -.27f, .13f, .05f, .05f);
                        Box(p, .1f, .11f, -.23f, .04f, .12f, .05f);
                        break;
                    case 3:
                    case 5:
                        Box(p, 0, -.07f, -.27f, kind == 5 ? .24f : .2f, .24f, .58f);
                        Mark(p, 0, -.1f, -.6f, .19f, .2f, .2f, kind == 5 ? Color.white : dark,
                            kind == 5 ? underfur : fur);
                        break;
                    case 4: Box(p, 0, -.02f, -.08f, .18f, .17f, .18f); break;
                    case 6:
                        Box(p, 0, -.22f, -.08f, .075f, .48f, .08f);
                        Box(p, 0, -.48f, -.08f, .18f, .22f, .17f);
                        break;
                    case 7: Box(p, 0, .05f, -.12f, .13f, .17f, .24f); break;
                    case 8:
                        Box(p, 0, -.03f, -.1f, .16f, .16f, .2f);
                        Mark(p, 0, -.03f, -.22f, .15f, .15f, .07f, dark, fur);
                        break;
                    case 9:
                        Mark(p, 0, -.17f, -.36f, .4f, .075f, .65f, dark, keratin);
                        for (int i = 0; i < 6; i++)
                            Mark(p, 0, -.128f, -.13f - i * .09f, .36f, .012f, .018f, cream * .3f, keratin);
                        break;
                    case 10: Box(p, 0, -.03f, -.035f, .24f, .13f, .12f); break;
                    case 11:
                        for (int i = 0; i < 5; i++)
                            Box(p, (i - 2) * .095f, -.035f, -.2f - (2 - Math.Abs(i - 2)) * .035f,
                                .09f, .055f, .46f);
                        break;
                }
            });

            if (kind == 11)
                for (int side = -1; side <= 1; side += 2)
                {
                    int wingSide = side;
                    Transform wing = Attach("Wing" + side, rig, new Vector3(side * .18f, .51f, .03f), p =>
                    {
                        Box(p, wingSide * .31f, 0, -.03f, .65f, .095f, .44f);
                        for (int i = 0; i < 5; i++)
                            Box(p, wingSide * (.08f + i * .12f), -.025f, -.24f, .115f, .055f, .33f);
                    });
                    Transform tip = Attach("WingTip" + side, wing, new Vector3(side * .62f, 0, -.02f), p =>
                    {
                        Box(p, wingSide * .2f, 0, -.03f, .43f, .07f, .32f);
                        for (int i = 0; i < 6; i++)
                            Mark(p, wingSide * (.16f + i * .095f), -.01f, -.13f - i * .035f,
                                .085f, .045f, .42f - i * .035f, cream * .45f, fur);
                    });
                    Wings.Add(wing);
                    wingTips.Add(tip);
                }

            ResetMotion();
        }

        public void ResetMotion()
        {
            last = transform.position;
            filteredSpeed = 0;
            animationClock = 0;
            if (rig) { rig.localPosition = Vector3.zero; rig.localScale = Vector3.one; }
        }

        // Geometry shrinks only in the outer culling band; the authoritative
        // collision root never changes scale. Animation is throttled, not AI.
        public void SetViewDistance(float distance)
        {
            if (!rig) return;
            float scale = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(48, 56, distance));
            rig.localScale = Vector3.one * scale;
        }

        public void Animate(float dt) { Animate(dt, 0); }

        public void Animate(float dt, float distance)
        {
            if (dt <= 0 || !rig) return;
            float speed = new Vector2(transform.position.x - last.x, transform.position.z - last.z).magnitude / Mathf.Max(.001f, dt);
            last = transform.position;
            filteredSpeed = Mathf.Lerp(filteredSpeed, Mathf.Min(speed, 8), 1 - Mathf.Exp(-dt * 12));
            elapsed += dt;
            animationClock += dt;
            float interval = distance > 48 ? .25f : distance > 36 ? .1f : distance > 24 ? .05f : 0;
            if (animationClock < interval) return;
            float step = animationClock;
            animationClock = 0;
            gait += step * Mathf.Clamp(filteredSpeed * 6, 0, 18);
            float amplitude = Mathf.Min(34, filteredSpeed * 12);
            bool hopper = species == 1 || species == 10;
            float hop = hopper ? Mathf.Max(0, Mathf.Sin(gait)) * (species == 10 ? .2f : .13f) * Mathf.Clamp01(filteredSpeed) : 0;
            rig.localPosition = new Vector3(0, hop, 0);
            for (int i = 0; i < Legs.Count; i++)
            {
                float phase = hopper ? (i < 2 ? 0 : .65f) : i == 0 || i == 3 ? 0 : Mathf.PI;
                float angle = species == 11 ? -27 : Mathf.Sin(gait + phase) * amplitude;
                Legs[i].localPosition = legPositions[i];
                Legs[i].localRotation = Quaternion.Euler(angle, 0, 0);
            }
            body.localRotation = Quaternion.Euler(hopper ? Mathf.Sin(gait) * amplitude * .12f : 0, 0, 0);
            Head.localPosition = headPosition;
            Head.localRotation = Quaternion.Euler(Mathf.Sin(elapsed * .7f) * 3,
                Mathf.Sin(elapsed * .4f + headPosition.y) * 7, 0);
            float tailPitch = species == 3 || species == 5 ? 22 : species == 9 ? -8 : 0;
            tail.localRotation = Quaternion.Euler(tailPitch, Mathf.Sin(elapsed * 2.1f) * (species == 10 ? 0 : 8), 0);
            if (species == 11)
            {
                float flapWeight = Mathf.SmoothStep(0, 1, (Mathf.Sin(elapsed * .65f) + .2f) * 1.2f);
                float flap = Mathf.Sin(elapsed * 5.2f) * 27 * flapWeight;
                for (int i = 0; i < Wings.Count; i++)
                {
                    float side = i == 0 ? -1 : 1;
                    Wings[i].localRotation = Quaternion.Euler(0, -side * 5, side * (7 + flap));
                    wingTips[i].localRotation = Quaternion.Euler(0, side * 8,
                        side * (Mathf.Sin(elapsed * 5.2f - .65f) * 18 * flapWeight));
                }
                body.localRotation = Quaternion.Euler(Mathf.Sin(elapsed * .8f) * 5, 0, 0);
            }
        }

        void OnDestroy()
        {
            foreach (string key in ownedMeshes)
            {
                CachedMesh entry;
                if (!Meshes.TryGetValue(key, out entry)) continue;
                if (--entry.references > 0) continue;
                if (entry.mesh)
                {
                    if (Application.isPlaying) Destroy(entry.mesh);
                    else DestroyImmediate(entry.mesh);
                }
                Meshes.Remove(key);
            }
            ownedMeshes.Clear();
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
