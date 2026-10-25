using System.Collections.Generic;
using UnityEngine;
using LestingCraft.Gameplay;

namespace LestingCraft.Runtime
{
    /// <summary>
    /// Renders the endless procedural universe: nearby star systems with their worlds, atmospheres,
    /// rings, black holes, nebulae, derelicts and the fixed galaxy catalogue on the sky.
    /// Everything is rebuilt from integer sector keys, so nothing has to be streamed or saved.
    /// </summary>
    public sealed class SpaceProceduralDome : MonoBehaviour
    {
        const double LightYearKm = 9460730472580800.0;
        const float MinStarHalfAngle = 0.0042f;
        const float MinWorldHalfAngle = 0.0019f;

        sealed class SystemView
        {
            public GameObject Root;
            public GameObject Star;
            public readonly List<GameObject> Worlds = new List<GameObject>();
            public readonly List<GameObject> Features = new List<GameObject>();
            public Transform Accretion;
            public bool Built;
        }

        readonly Dictionary<UniverseKey, SystemView> views = new Dictionary<UniverseKey, SystemView>();
        readonly List<GameObject> galaxyPlates = new List<GameObject>();
        readonly List<Material> owned = new List<Material>();
        readonly List<Texture2D> textures = new List<Texture2D>();
        Transform skyRoot;
        bool galaxiesBuilt;

        /// <summary>One-line cockpit report about the nearest world, refreshed every frame.</summary>
        public string SkyLine { get; private set; } = "";
        public string NearestSystemName { get; private set; } = "";
        public bool HasSky { get; private set; }

        static readonly string[] StarClassRu = { "коричневый карлик", "красный карлик", "оранжевый карлик", "жёлтый карлик", "белая звезда", "голубой гигант", "красный гигант", "белый карлик", "нейтронная звезда", "чёрная дыра" };

        public void Bind(Transform domeRoot)
        {
            if (skyRoot == domeRoot) return;
            skyRoot = domeRoot;
            if (!skyRoot) return;
            foreach (var view in views.Values) if (view.Root) view.Root.transform.SetParent(skyRoot, false);
            foreach (var plate in galaxyPlates) if (plate) plate.transform.SetParent(skyRoot, false);
        }

        public void HideAll()
        {
            HasSky = false;
            SkyLine = "";
            foreach (var view in views.Values) if (view.Root) view.Root.SetActive(false);
            foreach (var plate in galaxyPlates) if (plate) plate.SetActive(false);
        }

        /// <summary>Called every frame while the open-space helm is active.</summary>
        public void Sync(OpenSpaceHelm helm, Transform viewer)
        {
            if (helm == null || !helm.Active) { HideAll(); return; }
            if (!skyRoot)
            {
                skyRoot = new GameObject("ProceduralUniverse").transform;
                skyRoot.gameObject.transform.SetParent(transform, false);
            }
            if (viewer) skyRoot.position = viewer.position;
            Quaternion invShipRot = Quaternion.Inverse(helm.Rotation);
            SpaceVector ship = helm.PositionKm;

            BuildGalaxies(invShipRot);

            var nearest = ProceduralUniverse.Nearest(ship, 18, 4);
            var live = new HashSet<UniverseKey>();
            string line = "";
            string nearestName = "";
            for (int i = 0; i < nearest.Length; i++)
            {
                var system = nearest[i];
                live.Add(system.Key);
                if (!views.TryGetValue(system.Key, out var view))
                {
                    view = new SystemView();
                    views[system.Key] = view;
                }
                if (!view.Built) BuildSystem(system, view);
                if (!view.Root) continue;
                view.Root.SetActive(true);

                SpaceVector delta = system.AnchorKm - ship;
                double distanceKm = System.Math.Max(system.StarRadiusKm * 1.4, delta.Magnitude);
                float renderDistance = RenderDistance(distanceKm);
                Vector3 dir = invShipRot * ToVector(delta.Normalized);
                view.Root.transform.localPosition = dir * renderDistance;

                // Star: angular size, but never smaller than a visible point.
                double starAngle = System.Math.Min(0.94, System.Math.Max(1e-7, system.StarRadiusKm / distanceKm));
                float starHalf = Mathf.Max(renderDistance * MinStarHalfAngle, renderDistance * Mathf.Tan((float)System.Math.Asin(starAngle)));
                if (view.Star) view.Star.transform.localScale = Vector3.one * starHalf;

                // Worlds share the same monotonic depth ordering, so nothing pops through the hull.
                var planets = ProceduralUniverse.Planets(system);
                double phaseDays = helm.OrbitTimeDays;
                for (int p = 0; p < planets.Length && p < view.Worlds.Count; p++)
                {
                    var world = planets[p];
                    var body = view.Worlds[p];
                    if (!body) continue;
                    double angle = world.OrbitPhaseRad + phaseDays / System.Math.Max(0.25, world.OrbitDays) * System.Math.PI * 2.0;
                    double au = world.OrbitAu;
                    var orbitKm = new SpaceVector(System.Math.Cos(angle) * au * 149597870.7,
                        System.Math.Sin(angle * 0.5) * au * 149597870.7 * 0.18, System.Math.Sin(angle) * au * 149597870.7);
                    SpaceVector deltaWorld = system.AnchorKm + orbitKm - ship;
                    double worldDistance = System.Math.Max(world.RadiusKm * 1.05, deltaWorld.Magnitude);
                    float worldRender = RenderDistance(worldDistance);
                    body.transform.localPosition = invShipRot * ToVector(deltaWorld.Normalized) * worldRender;
                    double worldAngle = System.Math.Min(0.94, System.Math.Max(1e-7, world.RadiusKm / worldDistance));
                    float worldHalf = Mathf.Max(worldRender * MinWorldHalfAngle, worldRender * Mathf.Tan((float)System.Math.Asin(worldAngle)));
                    float baseHalf = world.HasRings ? 7.8f : Mathf.Clamp((float)System.Math.Log10(System.Math.Max(200.0, world.RadiusKm)) * 1.55f, 2.2f, 8.2f);
                    body.transform.localScale = Vector3.one * (worldHalf / Mathf.Max(0.1f, baseHalf));

                    // Sun direction for this world, in dome space: from the world to its star.
                    SpaceVector toStar = system.AnchorKm - (system.AnchorKm + orbitKm);
                    Vector3 sunDir = invShipRot * ToVector(toStar.Normalized);
                    if (sunDir.sqrMagnitude < 0.001f) sunDir = Vector3.up;
                    float flux = system.Star == StarKind.BlackHole ? 0.02f
                        : Mathf.Clamp(1.05f / Mathf.Pow(Mathf.Max(0.18f, (float)au), 0.55f), 0.16f, 2.4f);
                    PaintWorld(body, sunDir.normalized, flux, world);

                    if (p == 0 && i == 0)
                    {
                        nearestName = system.Name;
                        line = system.Name + " · " + StarClassRu[(int)system.Star] + " · " + world.Name + " · " + ProceduralUniverse.Describe(world);
                    }
                }

                // Points of interest ride along with the system root.
                var features = ProceduralUniverse.Structures(system);
                for (int f = 0; f < features.Length && f < view.Features.Count; f++)
                {
                    var feature = features[f];
                    var node = view.Features[f];
                    if (!node) continue;
                    SpaceVector featureDelta = system.AnchorKm + feature.OffsetKm - ship;
                    double featureDistance = System.Math.Max(feature.SizeKm, featureDelta.Magnitude);
                    float featureRender = RenderDistance(featureDistance);
                    node.transform.localPosition = invShipRot * ToVector(featureDelta.Normalized) * featureRender;
                    float angular = featureRender * Mathf.Tan((float)System.Math.Asin((float)System.Math.Min(0.9, feature.SizeKm / featureDistance)));
                    node.transform.localScale = Vector3.one * Mathf.Max(featureRender * 0.0016f, angular);
                }

                if (view.Accretion)
                {
                    view.Accretion.Rotate(Vector3.up, 42f * Time.deltaTime, Space.Self);
                    var material = view.Accretion.GetComponentInChildren<MeshRenderer>();
                    if (material && material.sharedMaterial && material.sharedMaterial.HasProperty("_Pulse"))
                        material.sharedMaterial.SetFloat("_Pulse", 0.12f);
                }
            }

            // Retire systems the ship has left so the dome never grows without bound.
            foreach (var pair in views)
            {
                if (live.Contains(pair.Key)) continue;
                if (pair.Value.Root) pair.Value.Root.SetActive(false);
            }

            HasSky = true;
            NearestSystemName = nearestName;
            SkyLine = line;
        }

        static Vector3 ToVector(SpaceVector v) { return new Vector3((float)v.X, (float)v.Y, (float)v.Z); }

        static float RenderDistance(double distanceKm)
        {
            return Mathf.Lerp(20f, 295f, Mathf.InverseLerp(2.0f, 10.2f, (float)System.Math.Log10(distanceKm + 1.0)));
        }

        // ------------------------------------------------------------------ building

        void BuildSystem(UniverseSystem system, SystemView view)
        {
            view.Built = true;
            var root = new GameObject("System_" + system.Name);
            root.transform.SetParent(skyRoot ? skyRoot : transform, false);
            view.Root = root;

            var planets = ProceduralUniverse.Planets(system);

            // Star (or black hole) at the local origin.
            float starBase = system.Star == StarKind.BlackHole ? 3.4f : system.Star == StarKind.BlueGiant || system.Star == StarKind.RedGiant ? 9.6f : 6.4f;
            var star = NewCube("Star_" + system.Name, root.transform, starBase);
            view.Star = star;
            if (system.Star == StarKind.BlackHole)
            {
                star.GetComponent<MeshRenderer>().sharedMaterial = Opaque("BlackHoleCore", new Color(0.012f, 0.010f, 0.016f), 0.9f, 0.1f);
                var disc = new GameObject("AccretionDisc");
                disc.transform.SetParent(root.transform, false);
                disc.transform.localRotation = Quaternion.Euler(74f, 18f, 0f);
                var discMesh = disc.AddComponent<MeshFilter>();
                discMesh.sharedMesh = RingMesh(2.0f, 6.2f, 40);
                var discRenderer = disc.AddComponent<MeshRenderer>();
                discRenderer.sharedMaterial = Glow("AccretionGlow", DiscTexture(system.Key.X, system.Key.Y, system.Key.Z), new Color(1f, 0.62f, 0.26f, 0.9f), 1.9f);
                discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                discRenderer.receiveShadows = false;
                view.Accretion = disc.transform;
            }
            else
            {
                Color tint = new Color(system.StarR, system.StarG, system.StarB);
                var material = Lit("StarSurface", tint, tint * 2.6f, 0.05f, 0.4f);
                star.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            for (int i = 0; i < planets.Length; i++)
            {
                var world = planets[i];
                var node = new GameObject("World_" + world.Name);
                node.transform.SetParent(root.transform, false);
                float half = world.HasRings ? 7.8f : Mathf.Clamp((float)System.Math.Log10(System.Math.Max(200.0, world.RadiusKm)) * 1.55f, 2.2f, 8.2f);
                var cube = NewCube("Surface", node.transform, half);
                var surface = cube.GetComponent<MeshRenderer>();
                var material = PlanetMaterial(world, out var texture);
                surface.sharedMaterial = material;
                surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                surface.receiveShadows = false;

                if (world.AtmosphereDensity > 0.35f)
                {
                    var halo = NewCube("Atmosphere", node.transform, half * 1.055f);
                    var haloRenderer = halo.GetComponent<MeshRenderer>();
                    haloRenderer.sharedMaterial = Glow("Atmosphere_" + world.Name, HaloTexture(), new Color(world.AtmoR, world.AtmoG, world.AtmoB, 0.42f * world.AtmosphereDensity), 0.85f);
                    haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                if (world.HasRings)
                {
                    var ring = new GameObject("Rings");
                    ring.transform.SetParent(node.transform, false);
                    ring.transform.localRotation = Quaternion.Euler(22f, 12f, -8f);
                    ring.AddComponent<MeshFilter>().sharedMesh = RingMesh((float)world.RingInnerScale, (float)world.RingOuterScale, 32);
                    var ringRenderer = ring.AddComponent<MeshRenderer>();
                    ringRenderer.sharedMaterial = Glow("RingGlow_" + world.Name, RingTexture(world.Seed),
                        new Color(world.SurfaceR, world.SurfaceG, world.SurfaceB, 0.55f), 1.15f);
                    ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    ring.transform.localScale = Vector3.one * 3.1f;
                }

                view.Worlds.Add(node);
                _ = texture;
            }

            var features = ProceduralUniverse.Structures(system);
            for (int i = 0; i < features.Length; i++)
            {
                var feature = features[i];
                var node = new GameObject("Feature_" + feature.Kind + "_" + i);
                node.transform.SetParent(root.transform, false);
                Color colour = new Color(feature.R, feature.G, feature.B);
                switch (feature.Kind)
                {
                    case StructureKind.AsteroidField:
                        for (int rock = 0; rock < 7; rock++)
                        {
                            var chunk = NewCube("Rock" + rock, node.transform, 1f);
                            float spread = 1.4f + (rock % 3) * 0.9f;
                            chunk.transform.localPosition = new Vector3(Mathf.Sin(rock * 2.1f) * spread, (rock % 2) * 0.7f, Mathf.Cos(rock * 1.7f) * spread);
                            chunk.transform.localScale = Vector3.one * (0.55f + (rock % 3) * 0.3f);
                            chunk.GetComponent<MeshRenderer>().sharedMaterial = Lit("AsteroidRock", colour * 0.8f, Color.black, 0.05f, 0.18f);
                        }
                        break;
                    case StructureKind.Wormhole:
                        node.AddComponent<MeshFilter>().sharedMesh = RingMesh(1.5f, 2.6f, 28);
                        var wormhole = node.AddComponent<MeshRenderer>();
                        wormhole.sharedMaterial = Glow("WormholeGlow", HaloTexture(), new Color(feature.R, feature.G, feature.B, 0.95f), 2.2f);
                        wormhole.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        break;
                    case StructureKind.Monolith:
                        var monolith = NewCube("Monolith", node.transform, 1f);
                        monolith.transform.localScale = new Vector3(0.55f, 3.2f, 0.35f);
                        monolith.GetComponent<MeshRenderer>().sharedMaterial = Lit("MonolithStone", colour * 0.5f, colour * 0.32f, 0.25f, 0.35f);
                        break;
                    case StructureKind.Comet:
                        var nucleus = NewCube("Nucleus", node.transform, 0.7f);
                        nucleus.GetComponent<MeshRenderer>().sharedMaterial = Lit("CometIce", new Color(0.86f, 0.92f, 1f), new Color(0.25f, 0.32f, 0.4f), 0.05f, 0.7f);
                        var tail = NewCube("Tail", node.transform, 1f);
                        tail.transform.localScale = new Vector3(0.4f, 0.4f, 5.4f);
                        tail.transform.localPosition = new Vector3(0f, 0f, -2.6f);
                        tail.GetComponent<MeshRenderer>().sharedMaterial = Glow("CometTail", HaloTexture(), new Color(0.62f, 0.86f, 1f, 0.5f), 1.1f);
                        break;
                    case StructureKind.Derelict:
                        for (int part = 0; part < 4; part++)
                        {
                            var chunk = NewCube("Wreck" + part, node.transform, 0.8f);
                            chunk.transform.localPosition = new Vector3(Mathf.Sin(part * 1.9f) * 1.1f, Mathf.Cos(part * 2.3f) * 0.5f, Mathf.Cos(part * 1.3f) * 1.1f);
                            chunk.transform.localRotation = Quaternion.Euler(part * 21f, part * 37f, part * 13f);
                            chunk.transform.localScale = new Vector3(1.1f, 0.35f, 0.7f);
                            chunk.GetComponent<MeshRenderer>().sharedMaterial = Lit("WreckHull", colour, Color.black, 0.7f, 0.3f);
                        }
                        break;
                    case StructureKind.Station:
                    case StructureKind.PirateOutpost:
                        var hull = NewCube("Hull", node.transform, 1f);
                        hull.transform.localScale = new Vector3(1.6f, 0.7f, 1.6f);
                        hull.GetComponent<MeshRenderer>().sharedMaterial = Lit("StationHull", colour, Color.black, 0.75f, 0.45f);
                        var lights = NewCube("Lights", node.transform, 1f);
                        lights.transform.localScale = new Vector3(1.7f, 0.12f, 1.7f);
                        lights.GetComponent<MeshRenderer>().sharedMaterial = Glow("StationLights", HaloTexture(), new Color(feature.R, feature.G, feature.B, 0.9f), 1.8f);
                        break;
                    case StructureKind.Probe:
                        var probe = NewCube("Probe", node.transform, 0.6f);
                        probe.GetComponent<MeshRenderer>().sharedMaterial = Lit("ProbeShell", colour, colour * 0.5f, 0.8f, 0.5f);
                        break;
                    default:
                        for (int block = 0; block < 5; block++)
                        {
                            var chunk = NewCube("Ruin" + block, node.transform, 0.8f);
                            chunk.transform.localPosition = new Vector3((block % 3) - 1f, (block / 3) * 1.1f - 0.3f, Mathf.Sin(block) * 0.8f);
                            chunk.transform.localScale = new Vector3(1.4f, 0.8f, 1.4f);
                            chunk.GetComponent<MeshRenderer>().sharedMaterial = Lit("RuinStone", colour * 0.7f, Color.black, 0.05f, 0.2f);
                        }
                        break;
                }
                view.Features.Add(node);
            }

            if (system.HasNebula)
            {
                var nebulaRoot = new GameObject("Nebula_" + system.Name);
                nebulaRoot.transform.SetParent(root.transform, false);
                var colour = new Color(system.NebulaR, system.NebulaG, system.NebulaB, 0.30f);
                var nebulaTexture = NebulaTexture((int)(system.Key.X * 31 + system.Key.Y * 17 + system.Key.Z));
                for (int puff = 0; puff < 3; puff++)
                {
                    var cloud = NewCube("Cloud" + puff, nebulaRoot.transform, 1f);
                    cloud.transform.localScale = Vector3.one * (26f + puff * 14f);
                    cloud.transform.localPosition = new Vector3(Mathf.Sin(puff * 2.1f) * 12f, Mathf.Cos(puff * 1.7f) * 6f, Mathf.Cos(puff * 2.7f) * 12f);
                    var renderer = cloud.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = Glow("NebulaGlow_" + puff, nebulaTexture, colour, 0.55f);
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                view.Features.Add(nebulaRoot);
            }
        }

        void BuildGalaxies(Quaternion invShipRot)
        {
            var catalogue = ProceduralUniverse.Galaxies();
            if (!galaxiesBuilt)
            {
                galaxiesBuilt = true;
                for (int i = 0; i < catalogue.Length; i++)
                {
                    var galaxy = catalogue[i];
                    var plate = NewCube("Galaxy_" + galaxy.Name, skyRoot ? skyRoot : transform, 1f);
                    plate.transform.localScale = Vector3.one * Mathf.Max(4f, 30f - i * 2.6f);
                    var renderer = plate.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = Glow("GalaxyGlow_" + i, GalaxyTexture(galaxy.Name.Length * 37 + i * 11),
                        new Color(galaxy.R, galaxy.G, galaxy.B, 0.34f), 0.95f);
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    galaxyPlates.Add(plate);
                }
            }
            for (int i = 0; i < catalogue.Length && i < galaxyPlates.Count; i++)
            {
                var galaxy = catalogue[i];
                var plate = galaxyPlates[i];
                if (!plate) continue;
                plate.SetActive(true);
                Vector3 direction = new Vector3((float)galaxy.X, (float)galaxy.Y, (float)galaxy.Z);
                plate.transform.localPosition = invShipRot * direction.normalized * 312f;
                plate.transform.localRotation = invShipRot * Quaternion.Euler(i * 23f, i * 41f, i * 17f);
            }
        }

        // ------------------------------------------------------------------ helpers

        GameObject NewCube(string name, Transform parent, float halfExtent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * halfExtent;
            go.AddComponent<MeshFilter>().sharedMesh = CubeMesh();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.receiveShadows = false;
            return go;
        }

        static Mesh cubeMesh;
        static Mesh CubeMesh()
        {
            if (cubeMesh) return cubeMesh;
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uv = new List<Vector2>(24);
            var triangles = new List<int>(36);
            Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3[] rights = { Vector3.back, Vector3.forward, Vector3.right, Vector3.right, Vector3.right, Vector3.left };
            Vector3[] ups = { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };
            for (int f = 0; f < 6; f++)
            {
                Vector3 centre = faces[f] * 0.5f, right = rights[f] * 0.5f, up = ups[f] * 0.5f;
                int start = vertices.Count;
                vertices.Add(centre - right - up); vertices.Add(centre + right - up);
                vertices.Add(centre + right + up); vertices.Add(centre - right + up);
                for (int k = 0; k < 4; k++) normals.Add(faces[f]);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            }
            cubeMesh = new Mesh { name = "ProceduralCube" };
            cubeMesh.SetVertices(vertices); cubeMesh.SetNormals(normals); cubeMesh.SetUVs(0, uv);
            cubeMesh.SetTriangles(triangles, 0); cubeMesh.RecalculateBounds();
            return cubeMesh;
        }

        static Mesh RingMesh(float inner, float outer, int segments)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                vertices.Add(new Vector3(cos * inner, 0f, sin * inner)); uv.Add(new Vector2(i / (float)segments, 0f));
                vertices.Add(new Vector3(cos * outer, 0f, sin * outer)); uv.Add(new Vector2(i / (float)segments, 1f));
                normals.Add(Vector3.up); normals.Add(Vector3.up);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                triangles.Add(a); triangles.Add(a + 2); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(a + 2); triangles.Add(a + 3);
            }
            var mesh = new Mesh { name = "ProceduralRing" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }

        Material Lit(string name, Color baseColor, Color emission, float metallic, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
            material.color = baseColor;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0.001f)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
            }
            owned.Add(material);
            return material;
        }

        Material Opaque(string name, Color baseColor, float metallic, float smoothness)
        {
            return Lit(name, baseColor, Color.black, metallic, smoothness);
        }

        Material Glow(string name, Texture2D texture, Color tint, float intensity)
        {
            var shader = Shader.Find("LestingCraft/ProceduralGlow") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (material.HasProperty("_Intensity")) material.SetFloat("_Intensity", intensity);
            owned.Add(material);
            return material;
        }

        Material PlanetMaterial(UniversePlanet world, out Texture2D texture)
        {
            texture = PlanetTexture(world);
            var shader = Shader.Find("LestingCraft/SpacePlanetVoxel");
            if (!shader) return Lit("FallbackWorld", new Color(world.SurfaceR, world.SurfaceG, world.SurfaceB), Color.black, 0.1f, 0.3f);
            var material = new Material(shader) { name = "ProceduralWorld_" + world.Name };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_AtmoColor", new Color(world.AtmoR, world.AtmoG, world.AtmoB, world.AtmosphereDensity * 0.85f));
            material.SetFloat("_CityGlow", world.HasLife ? 1.0f : 0f);
            material.SetFloat("_SpecStrength", world.Class == WorldClass.Ocean || world.Class == WorldClass.Ice ? 0.7f : 0.25f);
            material.SetFloat("_Cutoff", 0.05f);
            owned.Add(material);
            return material;
        }

        static void PaintWorld(GameObject body, Vector3 sunDir, float flux, UniversePlanet world)
        {
            if (!body) return;
            var renderers = body.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i] ? renderers[i].sharedMaterial : null;
                if (!material) continue;
                if (material.HasProperty("_SunDirWS")) material.SetVector("_SunDirWS", new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
                if (material.HasProperty("_SolarFlux")) material.SetFloat("_SolarFlux", flux);
                if (material.HasProperty("_NightAmbient")) material.SetFloat("_NightAmbient", 0.015f);
                if (material.HasProperty("_EmissionGain")) material.SetFloat("_EmissionGain", 0.02f);
            }
        }

        // ------------------------------------------------------------------ textures

        Texture2D PlanetTexture(UniversePlanet world)
        {
            const int size = 96;
            var pixels = new Color32[size * size];
            var palette = new Color(world.SurfaceR, world.SurfaceG, world.SurfaceB);
            var secondary = new Color(world.SecondaryR, world.SecondaryG, world.SecondaryB);
            int seed = world.Seed;
            bool bands = world.Class == WorldClass.GasGiant || world.Class == WorldClass.IceGiant;
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float n = Fbm(u * 6.2f + seed * 0.013f, v * 6.2f + seed * 0.007f, seed, 4);
                    float colourMix;
                    if (bands)
                    {
                        float band = Mathf.Sin(v * 19f + n * 2.4f) * 0.5f + 0.5f;
                        colourMix = Mathf.Clamp01(band * 0.85f + n * 0.25f);
                    }
                    else if (world.Class == WorldClass.Ocean)
                        colourMix = Mathf.Clamp01((n - 0.46f) * 6.5f);
                    else if (world.Class == WorldClass.Lava)
                        colourMix = Mathf.Clamp01((n - 0.58f) * 6f);
                    else
                        colourMix = Mathf.Clamp01((n - 0.35f) * 2.1f);
                    Color pixel = Color.Lerp(secondary, palette, colourMix);
                    // Ice caps and lava veins read clearly from orbit.
                    if ((world.Class == WorldClass.Ocean || world.Class == WorldClass.Rock || world.Class == WorldClass.Ice) && v < 0.09f)
                        pixel = Color.Lerp(pixel, new Color(0.92f, 0.96f, 1f), (0.09f - v) / 0.09f * 0.85f);
                    if (world.Class == WorldClass.Lava && colourMix > 0.72f)
                        pixel = Color.Lerp(pixel, new Color(1f, 0.45f, 0.12f), (colourMix - 0.72f) * 3f);
                    pixel.a = 1f;
                    pixels[y * size + x] = pixel;
                }
            }
            return MakeTexture("World_" + world.Name, size, pixels);
        }

        Texture2D NebulaTexture(int seed)
        {
            const int size = 96;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1) * 2f - 1f, v = y / (float)(size - 1) * 2f - 1f;
                    float radius = Mathf.Sqrt(u * u + v * v);
                    float noise = Fbm(x / 18f + seed * 0.31f, y / 18f + seed * 0.17f, seed, 4);
                    float alpha = Mathf.Clamp01((1f - radius) * 1.15f) * Mathf.Clamp01(noise * 1.8f - 0.35f);
                    var pixel = new Color(1f, 1f, 1f, alpha * 0.9f);
                    pixels[y * size + x] = pixel;
                }
            return MakeTexture("Nebula_" + seed, size, pixels);
        }

        Texture2D HaloTexture()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1) * 2f - 1f, v = y / (float)(size - 1) * 2f - 1f;
                    float radius = Mathf.Sqrt(u * u + v * v);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2.2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            return MakeTexture("Halo", size, pixels);
        }

        Texture2D GalaxyTexture(int seed)
        {
            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1) * 2f - 1f, v = y / (float)(size - 1) * 2f - 1f;
                    float radius = Mathf.Sqrt(u * u + v * v);
                    float angle = Mathf.Atan2(v, u);
                    float spiral = Mathf.Sin(angle * 2f + radius * 9.5f + seed * 0.11f) * 0.5f + 0.5f;
                    float arms = Mathf.Pow(spiral, 2.4f) * Mathf.Clamp01(1f - radius) * Mathf.Clamp01(1f - radius * 0.4f);
                    float core = Mathf.Pow(Mathf.Clamp01(1f - radius * 3.4f), 2f);
                    float alpha = Mathf.Clamp01(arms * 0.9f + core);
                    pixels[y * size + x] = new Color(1f, 0.96f, 0.92f, alpha);
                }
            return MakeTexture("Galaxy_" + seed, size, pixels);
        }

        Texture2D RingTexture(int seed)
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float v = y / (float)(size - 1);
                    float u = x / (float)(size - 1);
                    float gap = Mathf.Abs(Mathf.Sin(v * 22f + Fbm(u * 8f, v * 8f, seed, 3) * 3f));
                    float alpha = Mathf.Clamp01(gap * 1.3f - 0.25f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            return MakeTexture("Rings_" + seed, size, pixels);
        }

        Texture2D DiscTexture(long x, long y, long z)
        {
            const int size = 96;
            var pixels = new Color32[size * size];
            int seed = (int)((x * 73856093L) ^ (y * 19349663L) ^ (z * 83492791L));
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    float u = px / (float)(size - 1) * 2f - 1f, v = py / (float)(size - 1) * 2f - 1f;
                    float radius = Mathf.Sqrt(u * u + v * v);
                    float angle = Mathf.Atan2(v, u);
                    float streak = Mathf.Sin(angle * 14f + radius * 26f + Fbm(radius * 6f, angle * 2f, seed, 3) * 4f) * 0.5f + 0.5f;
                    float ring = Mathf.Clamp01((radius - 0.28f) * 2.4f) * Mathf.Clamp01((1f - radius) * 2.2f);
                    float alpha = Mathf.Clamp01(ring * (0.45f + streak * 0.65f));
                    pixels[py * size + px] = new Color(1f, 1f, 1f, alpha);
                }
            return MakeTexture("Accretion", size, pixels);
        }

        Texture2D MakeTexture(string name, int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            textures.Add(texture);
            return texture;
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093 ^ y * 19349663 ^ seed * 83492791);
                h ^= h >> 13; h *= 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffu) / 65535f;
            }
        }

        static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed);
            float c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float x, float y, int seed, int octaves)
        {
            float sum = 0f, amplitude = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(x, y, seed + i * 97) * amplitude;
                norm += amplitude;
                x *= 2.07f; y *= 2.07f; amplitude *= 0.5f;
            }
            return norm > 0.0001f ? sum / norm : 0f;
        }

        void OnDestroy()
        {
            for (int i = 0; i < owned.Count; i++) if (owned[i]) Destroy(owned[i]);
            for (int i = 0; i < textures.Count; i++) if (textures[i]) Destroy(textures[i]);
            owned.Clear(); textures.Clear();
        }
    }
}
