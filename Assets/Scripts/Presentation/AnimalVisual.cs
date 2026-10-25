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
            "bison-fur","goat-fur","lynx-fur","beaver-fur","frog-skin","eagle-feathers",
            "owl-feathers","penguin-plumage","camel-coat","scorpion-chitin",
            "turtle-shell","moose-fur","capybara-fur","voidray-skin",
            "ash-hound-hide","cherenkov-leech-skin","rad-scorpion-carapace","mutant-boar-hide","slag-carapace",
            "fallout-crow-plumage","gamma-ray-membrane","bone-stalker-hide","vitrified-golem-crust","wraith-shroud",
            "wolf-fur","scorpion-chitin","turtle-shell","voidray-skin"
        };
        public static readonly string[] SpeciesNames =
        {
            "Олень","Заяц","Кабан","Волк","Бурый медведь","Лисица",
            "Зубр","Горный козёл","Рысь","Бобр","Жаба","Орёл",
            "Полярная сова","Северный пингвин","Верблюд","Песчаный скорпион",
            "Болотная черепаха","Северный лось","Капибара","Эфирный скат",
            "Пепельная гончая","Черенковская пиявка","Радио-скорпион","Двухголовый секач","Шлаковый краб",
            "Пепельный ворон","Гамма-скат","Костяной ловчий","Тринититовый голем","Фантом Спирали",
            "Ледяной волк","Стеклянный паук","Грибной жук","Небесный кит"
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
        readonly List<Transform> ears = new List<Transform>();
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

            float[] widths =      { .48f, .3f, .62f, .48f, 1.12f, .32f, 1.2f, .43f, .47f, .55f, .62f, .4f,   .38f, .42f, .72f, .52f, .68f, .92f, .54f, .96f,  .54f, .46f, .66f, .78f, .86f, .44f, 1.08f, .62f, 1.28f, .66f,  .58f, .64f, .40f, 1.10f };
            float[] lengths =     { 1.05f,.45f,1.08f,1.15f,1.6f,  .83f, 1.85f,.85f, .88f, .8f,  .61f, .65f,  .46f, .40f, 1.55f,.76f, .78f, 1.75f,.94f, 1.15f, 1.32f, .92f, .96f, 1.38f, .96f, .72f, 1.30f, 1.25f, 1.48f, 1.05f, 1.42f, .90f, .58f, 2.30f };
            float[] bodyHeights = { .55f, .3f, .55f, .48f, 1.05f, .36f, 1.05f,.54f, .47f, .43f, .25f, .4f,   .42f, .68f, .74f, .22f, .38f, .88f, .50f, .20f,  .56f, .30f, .30f, .72f, .46f, .44f, .24f,  .68f, 1.24f, .98f,  .56f, .22f, .18f, .62f };
            float[] bodyYs =      { .94f, .24f,.52f, .72f, .98f,  .46f, 1.08f,.78f, .61f, .32f, .22f, .4f,   .38f, .48f, 1.18f,.24f, .28f, 1.26f,.42f, .34f,  .80f, .22f, .30f, .72f, .36f, .44f, .36f,  1.24f, 1.32f, 1.15f, .74f, .20f, .16f, 1.05f };
            float width = widths[kind], length = lengths[kind], bh = bodyHeights[kind], by = bodyYs[kind];

            body = Attach("Body", rig, Vector3.zero, p =>
            {
                Box(p, 0, by, 0, width, bh, length);
                Mark(p, 0, by - bh * .38f, .03f, width * .77f, bh * .23f, length * .8f, cream, underfur);
                if (kind >= 20 && kind < 30)
                {
                    // Bioluminescent cyan Cherenkov spine shards & radiation pustules on all 10 Radia Abyss mutants!
                    int cher = Surface("cherenkov-crystal");
                    for (int sp = 0; sp < 4; sp++)
                        Mark(p, 0, by + bh * .56f, -length * .32f + sp * length * .22f, width * .24f, .22f + (sp % 2) * .08f, .14f,
                            new Color(.25f, .88f, 1f), cher);
                    if (kind == 22 || kind == 24) // RadScorpion & SlagCrab heavy pincers
                    {
                        for (int side = -1; side <= 1; side += 2)
                            Mark(p, side * width * .62f, by, length * .55f, .26f, .20f, .42f, new Color(.35f, .92f, 1f), fur);
                    }
                    if (kind == 28 || kind == 29) // Vitrified Golem & Wraith towering armored shoulders
                        Box(p, 0, by + bh * .34f, .12f, width * 1.24f, bh * .48f, length * .55f);
                }
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
                if (kind == 11 || kind == 12)
                {
                    Box(p, 0, .52f, .12f, .34f, .38f, .36f);
                    Mark(p, 0, .31f, .14f, .3f, .22f, .38f, Color.white, underfur);
                }
                if (kind == 13) // Penguin white belly bib
                {
                    Mark(p, 0, .46f, .17f, .32f, .58f, .10f, Color.white, underfur);
                }
                if (kind == 14) // Camel twin humps + arched neck column
                {
                    Box(p, 0, 1.72f, .32f, .52f, .42f, .46f);
                    Box(p, 0, 1.70f, -.34f, .50f, .40f, .44f);
                    Box(p, 0, 1.48f, .78f, .30f, .68f, .30f);
                }
                if (kind == 15) // Scorpion armored pincer arms
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Mark(p, side * .34f, .26f, .44f, .14f, .11f, .36f, dark, keratin);
                        Mark(p, side * .38f, .26f, .72f, .22f, .14f, .26f, new Color(.35f, .20f, .10f), keratin);
                    }
                }
                if (kind == 16) // Turtle domed hexagonal shell rim
                {
                    Box(p, 0, .48f, 0, .54f, .18f, .60f);
                    Mark(p, 0, .14f, 0, .74f, .08f, .84f, new Color(.45f, .38f, .20f), keratin);
                }
                if (kind == 17) // Moose towering shoulder hump + dewlap
                {
                    Box(p, 0, 1.76f, .44f, .68f, .44f, .62f);
                }
                if (kind == 19) // VoidRay cosmic dorsal crest
                {
                    Mark(p, 0, .46f, 0, .28f, .08f, .88f, new Color(.22f, .92f, 1f), fur);
                }
            });

            float[] headYs =      { 1.4f, .38f, .62f, .98f, 1.31f, .65f, 1.17f, 1.1f, .82f, .43f, .29f, .68f,  .66f, .90f, 1.88f, .26f, .28f, 1.72f, .56f, .35f,  1.06f, .28f, .34f, .92f, .42f, .72f, .38f, 1.68f, 2.02f, 1.78f,  .95f, .28f, .22f, 1.05f };
            float[] headWidths =  { .25f, .25f, .42f, .32f, .62f,  .25f, .69f,  .26f, .37f, .39f, .68f, .28f,  .36f, .30f, .34f,  .32f, .24f, .42f,  .34f, .52f,  .36f,  .34f, .38f, .56f, .44f, .32f, .58f, .42f,  .64f,  .44f,  .40f, .30f, .26f, .55f };
            float[] headHeights = { .3f,  .23f, .35f, .32f, .48f,  .26f, .6f,   .32f, .31f, .28f, .2f,  .28f,  .32f, .28f, .34f,  .18f, .20f, .46f,  .34f, .18f,  .34f,  .22f, .22f, .44f, .24f, .30f, .20f, .44f,  .52f,  .46f,  .34f, .24f, .20f, .42f };
            headPosition = new Vector3(0, headYs[kind], length * (kind == 14 ? .58f : .48f));
            float hw = headWidths[kind], hh = headHeights[kind];
            Head = Attach("Head", rig, headPosition, p =>
            {
                Box(p, 0, 0, 0, hw, hh, .38f);
                if (kind == 11 || kind == 12 || kind == 13)
                {
                    Mark(p, 0, -.02f, .24f, .14f, .12f, kind == 13 ? .28f : .19f,
                        kind == 13 ? new Color(.95f, .50f, .12f) : new Color(.82f, .61f, .2f), keratin);
                    if (kind == 12) // Owl ear tufts & luminous amber eyes
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Box(p, side * .14f, .21f, -.02f, .08f, .14f, .08f);
                            Mark(p, side * .10f, .03f, .195f, .095f, .095f, .02f, new Color(1f, .78f, .14f), keratin);
                        }
                }
                else if (kind == 17) // Moose broad palmate antlers + bell dewlap
                {
                    Box(p, 0, -.08f, .28f, .30f, .30f, .36f);
                    Box(p, 0, -.30f, .08f, .10f, .28f, .14f);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Mark(p, side * .38f, .28f, -.04f, .52f, .09f, .32f, cream * .78f, keratin);
                        for (int tine = 0; tine < 4; tine++)
                            Mark(p, side * (.24f + tine * .11f), .40f, -.14f + tine * .09f, .06f, .20f, .06f, cream * .82f, keratin);
                    }
                }
                else if (kind != 10 && kind != 19)
                {
                    float snout = kind == 18 ? .30f : kind == 3 || kind == 5 ? .32f : kind == 2 ? .29f : .2f;
                    Box(p, 0, -.055f, .23f, hw * (kind == 18 ? .86f : .63f), hh * (kind == 18 ? .76f : .48f), snout);
                    Mark(p, 0, -.035f, .23f + snout * .5f, hw * .46f, hh * .27f, .035f,
                        kind == 2 ? cream * .7f : dark, keratin);
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    float ex = hw * .51f;
                    Mark(p, side * ex, hh * .17f, .095f, .025f, .065f, .075f,
                        kind >= 20 && kind < 30 || kind == 19 ? new Color(.22f, .95f, 1f) : new Color(.69f, .5f, .2f), keratin);
                    Mark(p, side * (ex + .014f), hh * .17f, .105f, .012f, .046f, .04f, Color.black, keratin);
                    if (kind == 0)
                    {
                        Mark(p, side * .09f, .36f, -.1f, .045f, .45f, .045f, cream * .65f, keratin);
                        Mark(p, side * .17f, .49f, -.1f, .2f, .04f, .04f, cream * .7f, keratin);
                    }
                    if (kind == 18) // Capybara tiny rounded ears on top-back of head
                        Box(p, side * .13f, hh * .55f, -.08f, .07f, .09f, .08f);
                }
            });

            int legCount = (kind == 19 || kind == 21 || kind == 26) ? 0
                         : (kind == 11 || kind == 12 || kind == 13 || kind == 25 || kind == 28 || kind == 29) ? 2
                         : (kind == 15 || kind == 22 || kind == 24) ? 6 : 4;
            float[] legLengths = { .65f, .19f, .36f, .53f, .58f, .32f, .64f, .56f, .43f, .19f, .16f, .2f, .18f, .16f, .86f, .18f, .14f, .88f, .22f, 0f,
                                   .62f, 0f, .24f, .52f, .28f, .22f, 0f, .96f, .82f, .86f,
                                   .62f, .30f, .18f, .18f };
            float ll = legLengths[kind];
            for (int i = 0; i < legCount; i++)
            {
                int legIndex = i;
                bool rear = i < legCount / 2;
                float thick = kind == 4 || kind == 6 || kind == 17 ? .22f : kind == 0 || kind == 7 ? .095f : .12f;
                float zOff = legCount == 2 ? .06f : legCount == 6 ? (-length * .34f + (i / 2) * length * .34f) : (rear ? -1 : 1) * length * .32f;
                Vector3 position = new Vector3((i % 2 == 0 ? -1 : 1) * width * .36f, ll, zOff);
                Transform leg = Attach("Leg" + i, rig, position, p =>
                {
                    Box(p, 0, -ll * .42f, 0, thick, ll * .82f, thick * 1.25f);
                    Mark(p, 0, -ll * .9f, .045f, thick * 1.25f, .08f,
                        kind == 13 ? .26f : .18f,
                        kind == 13 ? new Color(.95f, .50f, .12f) : dark, keratin);
                });
                leg.name = "Articulated leg " + legIndex;
                Legs.Add(leg);
                legPositions.Add(position);
            }

            tail = Attach("Tail", rig, new Vector3(0, by, -length * .5f), p =>
            {
                if (kind == 15) // Scorpion curved segmented tail + stinger
                {
                    for (int seg = 0; seg < 5; seg++)
                        Box(p, 0, .08f + seg * .10f, -.08f - seg * .05f + (seg > 2 ? (seg - 2) * .11f : 0), .12f, .12f, .14f);
                    Mark(p, 0, .58f, .02f, .08f, .10f, .18f, new Color(.85f, .22f, .12f), keratin);
                }
                else if (kind == 19) // VoidRay long luminous whip tail
                {
                    Mark(p, 0, 0, -.55f, .08f, .06f, 1.10f, new Color(.22f, .88f, 1f), fur);
                }
                else if (kind == 9)
                {
                    Mark(p, 0, -.17f, -.36f, .4f, .075f, .65f, dark, keratin);
                }
                else if (kind == 11 || kind == 12)
                {
                    for (int i = 0; i < 5; i++)
                        Box(p, (i - 2) * .085f, -.035f, -.18f, .085f, .05f, .38f);
                }
                else
                {
                    Box(p, 0, -.03f, -.12f, .14f, .14f, .24f);
                }
            });

            if (kind == 11 || kind == 12 || kind == 13 || kind == 19 || kind == 25 || kind == 26 || kind == 33)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    int wingSide = side;
                    bool ray = kind == 19 || kind == 26;
                    float wy = kind == 13 ? .54f : ray ? .35f : kind == 33 ? .70f : .48f;
                    float span = kind == 13 ? .14f : ray ? 1.05f : kind == 33 ? 1.25f : .64f;
                    Transform wing = Attach("Wing" + side, rig, new Vector3(side * width * .45f, wy, .03f), p =>
                    {
                        Box(p, wingSide * span * .5f, kind == 13 ? -.16f : 0, -.02f,
                            kind == 13 ? .08f : span, kind == 13 ? .38f : .08f, ray ? .92f : .42f);
                    });
                    Transform tip = Attach("WingTip" + side, wing, new Vector3(side * span, 0, -.02f), p =>
                    {
                        Mark(p, wingSide * .22f, 0, -.03f, .44f, .06f, ray ? .55f : .28f,
                            ray || kind == 25 ? new Color(.25f, .95f, 1f) : cream * .55f, fur);
                    });
                    Wings.Add(wing);
                    wingTips.Add(tip);
                }
            }

            BuildSpeciesDetail(kind, width, length, bh, by);
            ResetMotion();
        }

        /// <summary>
        /// Silhouette detail that separates species at a glance: ears, horns, beards, manes
        /// and brushes. Without it the rig reads as one rounded box with different fur.
        /// </summary>
        void BuildSpeciesDetail(int kind, float width, float length, float bodyHeight, float bodyY)
        {
            if (kind == 3 || kind == 4 || kind == 5 || kind == 8)
            {
                float ear = kind == 5 ? .17f : kind == 8 ? .15f : .12f;
                float earHeight = kind == 5 ? .26f : kind == 8 ? .30f : kind == 4 ? .16f : .22f;
                for (int side = -1; side <= 1; side += 2)
                {
                    int s = side;
                    var earTransform = Attach("Ear" + (side > 0 ? "R" : "L"), rig, new Vector3(s * width * .30f, bodyY + bodyHeight * .62f, length * .12f), p =>
                    {
                        Mark(p, 0, 0, 0, ear, earHeight, ear * .7f, coat * .97f, fur);
                        Mark(p, 0, earHeight * .18f, ear * .16f, ear * .62f, earHeight * .5f, ear * .3f, cream * .92f, underfur);
                    });
                    ears.Add(earTransform);
                }
            }
            if (kind == 4 || kind == 3 || kind == 6)
            {
                // Shoulder mantle: bears, wolves and bison carry a heavier front.
                Attach("Mantle", rig, new Vector3(0, bodyY + bodyHeight * .42f, length * .10f), p =>
                {
                    Mark(p, 0, 0, 0, width * 1.16f, bodyHeight * .42f, length * .55f, coat * .92f, fur);
                });
            }
            if (kind == 6 || kind == 7)
            {
                // Horns: bison curve up and out, goats sweep back.
                for (int side = -1; side <= 1; side += 2)
                {
                    float s = side;
                    Attach("Horn" + (side > 0 ? "R" : "L"), rig, new Vector3(s * width * .34f, bodyY + bodyHeight * .68f, length * .34f), p =>
                    {
                        if (kind == 6)
                        {
                            Mark(p, s * .04f, .02f, 0, .10f, .16f, .10f, cream * .82f, keratin);
                            Mark(p, s * .13f, .13f, 0, .12f, .12f, .09f, cream * .76f, keratin);
                            Mark(p, s * .23f, .20f, 0, .10f, .08f, .08f, cream * .70f, keratin);
                        }
                        else
                        {
                            Mark(p, 0, .06f, -.02f, .09f, .18f, .09f, cream * .74f, keratin);
                            Mark(p, 0, .20f, -.10f, .08f, .16f, .08f, cream * .66f, keratin);
                            Mark(p, 0, .32f, -.20f, .07f, .14f, .07f, cream * .60f, keratin);
                        }
                    });
                }
            }
            if (kind == 7)
            {
                Attach("Beard", rig, new Vector3(0, bodyY - bodyHeight * .42f, length * .42f), p =>
                {
                    Mark(p, 0, -.08f, 0, .10f, .24f, .10f, cream * .68f, underfur);
                });
            }
            if (kind == 5 || kind == 8 || kind == 3)
            {
                // Fox brush, lynx bob, wolf plume: one tail mesh, three shapes.
                Attach("Brush", tail, new Vector3(0, -0.04f, -.30f), p =>
                {
                    if (kind == 5)
                    {
                        Mark(p, 0, 0, -.16f, .20f, .20f, .40f, coat * .96f, fur);
                        Mark(p, 0, 0, -.40f, .13f, .13f, .18f, cream, underfur);
                    }
                    else if (kind == 8)
                    {
                        Mark(p, 0, 0, -.10f, .13f, .13f, .18f, coat * .96f, fur);
                        Mark(p, 0, 0, -.19f, .08f, .08f, .06f, dark, keratin);
                    }
                    else
                    {
                        Mark(p, 0, .04f, -.18f, .16f, .16f, .34f, coat * .94f, fur);
                    }
                });
            }
            if (kind == 30)
            {
                // Frost wolf: pale mane and a heavier ruff behind the head.
                Attach("FrostMane", rig, new Vector3(0, bodyY + bodyHeight * .34f, length * .30f), p =>
                {
                    for (int i = 0; i < 4; i++)
                        Mark(p, (i % 2 == 0 ? -1 : 1) * width * .30f, .05f, -.05f + i * .10f,
                            .10f, .20f, .12f, cream * .96f, underfur);
                });
                Attach("FrostRuff", rig, new Vector3(0, bodyY + bodyHeight * .10f, length * .40f), p =>
                {
                    Mark(p, 0, 0, 0, width * 1.10f, bodyHeight * .34f, length * .22f, cream, underfur);
                });
            }
            if (kind == 31)
            {
                // Glass spider: eight thin legs and a faceted abdomen.
                for (int leg = 0; leg < 8; leg++)
                {
                    int index = leg;
                    Attach("SpiderLeg" + leg, rig, new Vector3((leg % 2 == 0 ? -1 : 1) * width * .55f, bodyY, length * (.34f - (leg / 2) * .22f)), p =>
                    {
                        float side = index % 2 == 0 ? -1f : 1f;
                        Mark(p, side * .05f, -.10f, 0, .05f, .34f, .05f, coat * .92f, fur);
                        Mark(p, side * .22f, -.26f, 0, .34f, .05f, .05f, coat * .84f, fur);
                    });
                }
            }
            if (kind == 32)
            {
                // Fungus beetle: domed shell with a row of caps.
                Attach("BeetleShell", rig, new Vector3(0, bodyY + bodyHeight * .30f, 0), p =>
                {
                    Mark(p, 0, 0, 0, width * 1.22f, bodyHeight * .62f, length * .92f, coat * .94f, fur);
                    for (int i = 0; i < 3; i++)
                        Mark(p, 0, bodyHeight * .34f, -length * .24f + i * length * .24f,
                            width * .30f, .06f, width * .30f, cream * .92f, underfur);
                });
            }
            if (kind == 33)
            {
                // Sky whale: broad flukes and a softly glowing belly strip.
                int glow = Surface("cherenkov-crystal");
                Attach("WhaleBelly", rig, new Vector3(0, bodyY - bodyHeight * .42f, 0), p =>
                {
                    for (int i = 0; i < 5; i++)
                        Mark(p, 0, 0, -length * .34f + i * length * .17f, width * .74f, .05f, .09f,
                            new Color(.36f, .88f, 1f), glow);
                });
                Attach("WhaleFlukes", tail, new Vector3(0, 0, -.22f), p =>
                {
                    Mark(p, -.36f, 0, -.06f, .52f, .06f, .22f, coat * .92f, fur);
                    Mark(p, .36f, 0, -.06f, .52f, .06f, .22f, coat * .92f, fur);
                });
                Attach("WhaleFinL", rig, new Vector3(-width * .56f, bodyY, length * .10f), p =>
                    Mark(p, -.12f, -.06f, 0, .34f, .05f, .22f, coat * .88f, fur));
                Attach("WhaleFinR", rig, new Vector3(width * .56f, bodyY, length * .10f), p =>
                    Mark(p, .12f, -.06f, 0, .34f, .05f, .22f, coat * .88f, fur));
            }
            if (kind == 0)
            {
                Attach("RumpPatch", rig, new Vector3(0, bodyY + .02f, -length * .46f), p =>
                {
                    Mark(p, 0, 0, -.02f, width * .52f, bodyHeight * .42f, .06f, cream, underfur);
                });
            }
        }

        public void ResetMotion()
        {
            last = transform.position;
            filteredSpeed = 0;
            animationClock = 0;
            if (rig) { rig.localPosition = Vector3.zero; rig.localScale = Vector3.one; }
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
            bool hopper = species == 1 || species == 10 || species == 21;
            bool flyer = species == 11 || species == 12 || species == 19 || species == 25 || species == 26 || species == 33;
            float hop = hopper ? Mathf.Max(0, Mathf.Sin(gait)) * (species == 10 ? .2f : .13f) * Mathf.Clamp01(filteredSpeed) : 0;
            rig.localPosition = new Vector3(0, hop, 0);
            for (int i = 0; i < Legs.Count; i++)
            {
                float phase = hopper ? (i < 2 ? 0 : .65f) : i % 2 == 0 ? 0 : Mathf.PI;
                float angle = flyer ? -27 : Mathf.Sin(gait + phase) * amplitude;
                Legs[i].localPosition = legPositions[i];
                Legs[i].localRotation = Quaternion.Euler(angle, 0, 0);
            }
            float waddleRoll = species == 13 ? Mathf.Sin(gait) * 7f * Mathf.Clamp01(filteredSpeed) : 0;
            body.localRotation = Quaternion.Euler(hopper ? Mathf.Sin(gait) * amplitude * .12f : 0, 0, waddleRoll);
            // Breathing: a 2% chest swell reads as "alive" even when the animal stands still.
            float breath = 1f + Mathf.Sin(elapsed * 1.9f) * .018f;
            body.localScale = new Vector3(1f, breath, 1f);
            Head.localPosition = headPosition;
            Head.localRotation = Quaternion.Euler(Mathf.Sin(elapsed * .7f) * 3,
                Mathf.Sin(elapsed * .4f + headPosition.y) * (species == 12 ? 22f : 7f), 0);
            // Ears twitch independently of the head, a cheap but convincing life cue.
            float twitch = Mathf.Sin(elapsed * 3.7f) * 6f + Mathf.Sin(elapsed * 1.3f) * 3f;
            for (int i = 0; i < ears.Count; i++)
                ears[i].localRotation = Quaternion.Euler(0, 0, (i == 0 ? -1 : 1) * twitch);
            float tailPitch = species == 3 || species == 5 ? 22 : species == 9 ? -8 : 0;
            tail.localRotation = Quaternion.Euler(tailPitch, Mathf.Sin(elapsed * 2.1f) * (species == 10 ? 0 : 8), 0);
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
                    wingTips[i].localRotation = Quaternion.Euler(0, side * 8,
                        side * (Mathf.Sin(elapsed * rate - .65f) * 16 * flapWeight));
                }
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
