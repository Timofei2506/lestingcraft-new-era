using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.World;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;

namespace LestingCraft.Runtime
{
    public enum SpaceTargetKind
    {
        Earth = 0,
        Moon = 1,
        Mars = 2,
        Venus = 3,
        Mercury = 4,
        OrbitalStation = 5,
        Europa = 6,
        Titan = 7,
        Pluto = 8,
        ProximaB = 9,
        Trappist1e = 10,
        Kepler452b = 11,
        AndromedaPrime = 12,
        Somnia = 13
    }

    /// <summary>
    /// Renders procedural 3D deep-space celestial bodies across all 7 Star Systems and both Galaxies
    /// (Milky Way & Andromeda M31), planetary rings, asteroid belt, spiral galaxy disc, rocket plumes,
    /// Orbital Station interior, Planetary Guardian Bosses, and the Dream Anomaly's Shadow Watcher.
    /// </summary>
    public sealed class SpaceFlightVisuals : MonoBehaviour
    {
        readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();

        GameObject spaceDomeRoot;
        MeshRenderer domeRenderer;
        GameObject cachedWorldAtmosphere;
        GameObject starfieldObj;
        GameObject sunObj;
        GameObject spiralGalaxyObj;
        GameObject asteroidBeltObj;
        GameObject dreamWatcherRoot;
        readonly Dictionary<SpaceTargetKind, GameObject> planetSpheres = new Dictionary<SpaceTargetKind, GameObject>();
        readonly Dictionary<CelestialBodyId, GameObject> atlasBodyObjects = new Dictionary<CelestialBodyId, GameObject>();

        GameObject plumeRoot;
        Transform mainFlame, leftPlume, rightPlume, plasmaShield;
        Light engineLight;

        GameObject stationRoot;
        readonly List<Transform> stationAstronauts = new List<Transform>();

        public StationDocking ActiveStationDock { get; private set; }
        public Vector3 StationSpawnWorldPos { get; private set; }
        public Vector3 StationReturnHatchWorldPos { get; private set; }
        public bool IsStationSpawned => stationRoot != null;
        public int SpawnedAtlasBodyCount => atlasBodyObjects.Count;

        public static string TargetNameRu(SpaceTargetKind target)
        {
            switch (target)
            {
                case SpaceTargetKind.Earth: return "Лестингия (Земля)";
                case SpaceTargetKind.Moon: return "Луна";
                case SpaceTargetKind.Mars: return "Марс";
                case SpaceTargetKind.Venus: return "Венера";
                case SpaceTargetKind.Mercury: return "Меркурий";
                case SpaceTargetKind.OrbitalStation: return "Орбитальная станция";
                case SpaceTargetKind.Europa: return "Европа (Юпитер II)";
                case SpaceTargetKind.Titan: return "Титан (Сатурн VI)";
                case SpaceTargetKind.Pluto: return "Плутон";
                case SpaceTargetKind.ProximaB: return "Проксима Центавра b";
                case SpaceTargetKind.Trappist1e: return "TRAPPIST-1e";
                case SpaceTargetKind.Kepler452b: return "Кеплер-452b";
                case SpaceTargetKind.AndromedaPrime: return "Андромеда Прайм (M31)";
                case SpaceTargetKind.Somnia: return "Сомния (Разлом M31)";
                default: return "Космос";
            }
        }

        public static string TargetWireKey(SpaceTargetKind target)
        {
            switch (target)
            {
                case SpaceTargetKind.Earth: return "earth";
                case SpaceTargetKind.Moon: return "moon";
                case SpaceTargetKind.Mars: return "mars";
                case SpaceTargetKind.Venus: return "venus";
                case SpaceTargetKind.Mercury: return "mercury";
                case SpaceTargetKind.OrbitalStation: return "station";
                case SpaceTargetKind.Europa: return "europa";
                case SpaceTargetKind.Titan: return "titan";
                case SpaceTargetKind.Pluto: return "pluto";
                case SpaceTargetKind.ProximaB: return "proxima_b";
                case SpaceTargetKind.Trappist1e: return "trappist_1e";
                case SpaceTargetKind.Kepler452b: return "kepler_452b";
                case SpaceTargetKind.AndromedaPrime: return "andromeda_prime";
                case SpaceTargetKind.Somnia: return "somnia";
                default: return "moon";
            }
        }

        public static SpaceTargetKind ParseWireKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return SpaceTargetKind.Moon;
            switch (key.Trim().ToLowerInvariant())
            {
                case "earth":
                case "lestingia": return SpaceTargetKind.Earth;
                case "mars": return SpaceTargetKind.Mars;
                case "venus": return SpaceTargetKind.Venus;
                case "mercury": return SpaceTargetKind.Mercury;
                case "station": return SpaceTargetKind.OrbitalStation;
                case "europa": return SpaceTargetKind.Europa;
                case "titan": return SpaceTargetKind.Titan;
                case "pluto": return SpaceTargetKind.Pluto;
                case "proxima_b":
                case "proximab": return SpaceTargetKind.ProximaB;
                case "trappist_1e":
                case "trappist1e": return SpaceTargetKind.Trappist1e;
                case "kepler_452b":
                case "kepler452b": return SpaceTargetKind.Kepler452b;
                case "andromeda_prime":
                case "andromeda": return SpaceTargetKind.AndromedaPrime;
                case "somnia": return SpaceTargetKind.Somnia;
                default: return SpaceTargetKind.Moon;
            }
        }

        public static Dimension DimensionForTarget(SpaceTargetKind target)
        {
            switch (target)
            {
                case SpaceTargetKind.Earth: return Dimension.Lestingia;
                case SpaceTargetKind.Moon: return Dimension.Moon;
                case SpaceTargetKind.Mars: return Dimension.Mars;
                case SpaceTargetKind.Venus: return Dimension.Venus;
                case SpaceTargetKind.Mercury: return Dimension.Mercury;
                case SpaceTargetKind.Europa: return Dimension.Europa;
                case SpaceTargetKind.Titan: return Dimension.Titan;
                case SpaceTargetKind.Pluto: return Dimension.Pluto;
                case SpaceTargetKind.ProximaB: return Dimension.ProximaB;
                case SpaceTargetKind.Trappist1e: return Dimension.Trappist1e;
                case SpaceTargetKind.Kepler452b: return Dimension.Kepler452b;
                case SpaceTargetKind.AndromedaPrime: return Dimension.AndromedaPrime;
                case SpaceTargetKind.Somnia: return Dimension.Somnia;
                default: return Dimension.Lestingia;
            }
        }

        public static SpaceTargetKind TargetForBody(CelestialBodyId bodyId)
        {
            switch (bodyId)
            {
                case CelestialBodyId.Earth: return SpaceTargetKind.Earth;
                case CelestialBodyId.OrbitalStation: return SpaceTargetKind.OrbitalStation;
                case CelestialBodyId.Moon: return SpaceTargetKind.Moon;
                case CelestialBodyId.Mars: return SpaceTargetKind.Mars;
                case CelestialBodyId.Venus: return SpaceTargetKind.Venus;
                case CelestialBodyId.Mercury: return SpaceTargetKind.Mercury;
                case CelestialBodyId.Europa: return SpaceTargetKind.Europa;
                case CelestialBodyId.Titan: return SpaceTargetKind.Titan;
                case CelestialBodyId.Pluto: return SpaceTargetKind.Pluto;
                case CelestialBodyId.ProximaB: return SpaceTargetKind.ProximaB;
                case CelestialBodyId.Trappist1e: return SpaceTargetKind.Trappist1e;
                case CelestialBodyId.Kepler452b: return SpaceTargetKind.Kepler452b;
                case CelestialBodyId.AndromedaPrime: return SpaceTargetKind.AndromedaPrime;
                case CelestialBodyId.Somnia: return SpaceTargetKind.Somnia;
                default: return SpaceTargetKind.Moon;
            }
        }

        public static float GravityForDimension(Dimension dim)
        {
            switch (dim)
            {
                case Dimension.Moon: return 0.22f;
                case Dimension.Mars: return 0.42f;
                case Dimension.Venus: return 0.90f;
                case Dimension.Mercury: return 0.40f;
                case Dimension.Europa: return 0.14f;
                case Dimension.Titan: return 0.18f;
                case Dimension.Pluto: return 0.08f;
                case Dimension.ProximaB: return 1.07f;
                case Dimension.Trappist1e: return 0.93f;
                case Dimension.Kepler452b: return 1.60f;
                case Dimension.AndromedaPrime: return 1.12f;
                case Dimension.Somnia: return 0.62f;
                case Dimension.Dream: return 0.86f;
                default: return 1.0f;
            }
        }

        public static SpaceFlightVisuals Ensure(Transform parent)
        {
            var existing = FindAnyObjectByType<SpaceFlightVisuals>();
            if (existing) return existing;
            var go = new GameObject("SpaceFlightVisuals");
            if (parent) go.transform.SetParent(parent, false);
            var comp = go.AddComponent<SpaceFlightVisuals>();
            comp.BuildCelestialRig();
            return comp;
        }

        static void SafeDestroy(UnityEngine.Object obj)
        {
            if (!obj) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        Material MakeLitMaterial(string label, Color baseColor, Color emission, float metallic = 0.1f, float smoothness = 0.25f)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh) { name = label, color = baseColor };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0.001f)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
            }
            ownedObjects.Add(m);
            return m;
        }

        Material MakeVertexColorMaterial(string label, bool additive = false)
        {
            Shader sh = Shader.Find("LestingCraft/SmokeParticles")
                ?? (additive
                    ? (Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"))
                    : (Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Lit")));
            var m = new Material(sh) { name = label, color = Color.white };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", Texture2D.whiteTexture);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            ownedObjects.Add(m);
            return m;
        }

        Light spaceKeyLight;

        void BuildCelestialRig()
        {
            if (spaceDomeRoot) return;
            spaceDomeRoot = new GameObject("CelestialSpaceDome");
            spaceDomeRoot.transform.SetParent(transform, false);

            var sunRigLight = new GameObject("DeepSpaceSolarKeyLight");
            sunRigLight.transform.SetParent(spaceDomeRoot.transform, false);
            sunRigLight.transform.localRotation = Quaternion.Euler(26f, -38f, 0f);
            spaceKeyLight = sunRigLight.AddComponent<Light>();
            spaceKeyLight.type = LightType.Directional;
            spaceKeyLight.color = new Color(1.0f, 0.96f, 0.90f);
            spaceKeyLight.intensity = 2.15f;
            spaceKeyLight.shadows = LightShadows.None;

            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backdrop.name = "DeepSpaceBackdrop";
            backdrop.transform.SetParent(spaceDomeRoot.transform, false);
            backdrop.transform.localScale = new Vector3(-420f, 420f, 420f);
            SafeDestroy(backdrop.GetComponent<Collider>());
            domeRenderer = backdrop.GetComponent<MeshRenderer>();
            domeRenderer.sharedMaterial = MakeLitMaterial("SpaceSkyBackdrop", new Color(0.008f, 0.012f, 0.026f), new Color(0.006f, 0.010f, 0.022f), 0f, 0f);
            domeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            domeRenderer.receiveShadows = false;

            starfieldObj = new GameObject("ProceduralStarfield");
            starfieldObj.transform.SetParent(spaceDomeRoot.transform, false);
            var sfFilter = starfieldObj.AddComponent<MeshFilter>();
            sfFilter.sharedMesh = BuildStarfieldMesh(2400, 195f, 2506);
            var sfRenderer = starfieldObj.AddComponent<MeshRenderer>();
            sfRenderer.sharedMaterial = MakeVertexColorMaterial("StarfieldMat");
            sfRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sfRenderer.receiveShadows = false;

            sunObj = CreateCelestialBody("SolarStar", 11.5f, SpaceTargetKind.Mercury, true);
            planetSpheres[SpaceTargetKind.Earth] = CreateCelestialBody("Planet_Lestingia", 8.0f, SpaceTargetKind.Earth, false);
            planetSpheres[SpaceTargetKind.Moon] = CreateCelestialBody("Planet_Moon", 4.0f, SpaceTargetKind.Moon, false);
            planetSpheres[SpaceTargetKind.Mars] = CreateCelestialBody("Planet_Mars", 5.6f, SpaceTargetKind.Mars, false);
            planetSpheres[SpaceTargetKind.Venus] = CreateCelestialBody("Planet_Venus", 7.4f, SpaceTargetKind.Venus, false);
            planetSpheres[SpaceTargetKind.Mercury] = CreateCelestialBody("Planet_Mercury", 3.8f, SpaceTargetKind.Mercury, false);

            // Full 24-body 3D Cubic Voxel Celestial Atlas across Solar System, Extrasolar Systems & Andromeda M31
            foreach (var body in CelestialAtlas.Bodies)
            {
                var node = CreateAtlasBodyVisual(body);
                atlasBodyObjects[body.Id] = node;
                node.SetActive(false);
            }
            spiralGalaxyObj = CreateSpiralGalaxyBackdrop();
            asteroidBeltObj = CreateAsteroidBeltVisual();

            spaceDomeRoot.SetActive(false);
        }

        static Vector3 CubeFaceDirection(int face, float s, float t)
        {
            switch (face)
            {
                case 0: return new Vector3(1f, t, -s);
                case 1: return new Vector3(-1f, t, s);
                case 2: return new Vector3(s, 1f, -t);
                case 3: return new Vector3(s, -1f, t);
                case 4: return new Vector3(s, t, 1f);
                default: return new Vector3(-s, t, -1f);
            }
        }

        Texture2D BuildAtlasCubeTexture(CelestialBodyDef body, Color primary, Color secondary)
        {
            const int tile = 24;
            const int w = tile * 3, h = tile * 2;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            int seed = (int)body.Id * 13 + 7;
            var pixels = new Color32[w * h];

            for (int face = 0; face < 6; face++)
            {
                int ox = (face % 3) * tile;
                int oy = (face / 3) * tile;
                for (int py = 0; py < tile; py++)
                {
                    float t = ((py + 0.5f) / tile) * 2f - 1f;
                    for (int px = 0; px < tile; px++)
                    {
                        float s = ((px + 0.5f) / tile) * 2f - 1f;
                        Vector3 raw = CubeFaceDirection(face, s, t);
                        Vector3 n = raw.normalized;
                        float polar = Mathf.Abs(raw.y);
                        int hsh = ((px * 73856093) ^ (py * 19349663) ^ (face * 83492791) ^ (seed * 26544357)) & 0x7fffffff;
                        float jitter = ((hsh % 100) / 100f - 0.5f) * 0.14f;

                        float bandFreq = body.HasRings || body.RadiusKm > 15000.0 ? 8.5f : 4.5f;
                        float swirl = Mathf.Sin(raw.x * 3.1f + raw.z * 2.9f + seed) * 0.14f;
                        float band = Mathf.Floor((Mathf.Sin((raw.y + swirl) * bandFreq + seed * 0.4f) * 0.5f + 0.5f) * 5f) / 4f;
                        float cont = Mathf.Sin(n.x * 3.9f + seed * 0.7f) * Mathf.Cos(n.z * 3.6f - seed * 0.3f)
                                   + Mathf.Sin(n.y * 4.7f + n.x * 2.2f) * 0.45f;
                        Color c;
                        if (body.IsStar)
                        {
                            float gran = ((hsh >> 4) % 8) / 7f;
                            c = Color.Lerp(primary, Color.white, 0.24f + 0.56f * gran);
                        }
                        else if (body.Id == CelestialBodyId.Earth)
                        {
                            c = polar > 0.78f ? new Color(0.93f, 0.97f, 1.0f)
                              : cont > 0.05f
                                ? (cont > 0.42f ? new Color(0.52f, 0.46f, 0.28f) : new Color(0.15f, 0.52f, 0.22f))
                                : (cont > -0.16f ? new Color(0.12f, 0.46f, 0.84f) : new Color(0.05f, 0.22f, 0.58f));
                        }
                        else if (body.HasRings || body.RadiusKm > 15000.0)
                        {
                            c = Color.Lerp(primary, secondary, Mathf.Clamp01(band));
                            if (body.Id == CelestialBodyId.Jupiter && face == 4 && Mathf.Abs(s - 0.18f) < 0.28f && Mathf.Abs(t + 0.22f) < 0.18f)
                                c = new Color(0.88f, 0.33f, 0.18f);
                        }
                        else
                        {
                            float stepVal = Mathf.Floor(Mathf.Clamp01(cont * 0.55f + 0.50f) * 4f) / 3f;
                            c = Color.Lerp(primary, secondary, stepVal);
                            if (polar > 0.82f && !body.IsStar)
                                c = Color.Lerp(c, new Color(0.90f, 0.94f, 0.98f), 0.75f);
                        }
                        c = new Color(Mathf.Clamp01(c.r + jitter), Mathf.Clamp01(c.g + jitter), Mathf.Clamp01(c.b + jitter), 1f);
                        if (px == 0 || py == 0 || px == tile - 1 || py == tile - 1)
                            c = Color.Lerp(c, c * 0.80f, 0.65f);
                        c.a = 1f;
                        pixels[(oy + py) * w + (ox + px)] = c;
                    }
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            ownedObjects.Add(tex);
            return tex;
        }

        Texture2D BuildCloudCubeTexture(int seed, Color cloudTint)
        {
            const int tile = 16;
            const int w = tile * 3, h = tile * 2;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            var pixels = new Color32[w * h];
            for (int face = 0; face < 6; face++)
            {
                int ox = (face % 3) * tile;
                int oy = (face / 3) * tile;
                for (int py = 0; py < tile; py++)
                {
                    float t = ((py + 0.5f) / tile) * 2f - 1f;
                    for (int px = 0; px < tile; px++)
                    {
                        float s = ((px + 0.5f) / tile) * 2f - 1f;
                        Vector3 n = CubeFaceDirection(face, s, t).normalized;
                        float cld = Mathf.Sin(n.x * 5.2f + seed) * Mathf.Cos(n.z * 4.8f - seed * 0.5f) + Mathf.Sin(n.y * 6.4f) * 0.35f;
                        bool filled = cld > 0.28f;
                        pixels[(oy + py) * w + (ox + px)] = filled
                            ? (Color32)new Color(cloudTint.r, cloudTint.g, cloudTint.b, 1f)
                            : new Color32(0, 0, 0, 0);
                    }
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            ownedObjects.Add(tex);
            return tex;
        }

        Texture2D BuildSquarePlanetaryRingTexture(Color cA, Color cB)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float nz = ((y + 0.5f) / size) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    float sqDist = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(nz));
                    bool insideHole = sqDist < 0.52f || sqDist > 0.99f;
                    bool cassiniGap = sqDist > 0.755f && sqDist < 0.815f;
                    bool enckeGap = sqDist > 0.915f && sqDist < 0.940f;
                    int bandIdx = Mathf.FloorToInt(sqDist * 32f);
                    float bandTone = ((bandIdx * 37) % 7) / 6f;
                    float density = (insideHole || cassiniGap || enckeGap) ? 0f : (sqDist < 0.755f ? 0.96f : 0.82f);
                    Color c = Color.Lerp(cB * 0.88f, Color.Lerp(cA, Color.white, 0.46f), bandTone);
                    c.a = density > 0.3f ? 1f : 0f;
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            ownedObjects.Add(tex);
            return tex;
        }

        Mesh BuildVoxelCubeMesh(float halfExtent, string meshName)
        {
            const float padU = 0.35f / 72f;
            const float padV = 0.35f / 48f;
            var verts = new List<Vector3>(24);
            var norms = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var cols = new List<Color>(24);
            var tris = new List<int>(36);

            Vector3[] faceNormals = {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
            };
            Vector3[] faceRight = {
                Vector3.back, Vector3.forward, Vector3.right, Vector3.right, Vector3.right, Vector3.left
            };
            Vector3[] faceUp = {
                Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up
            };

            for (int f = 0; f < 6; f++)
            {
                float u0 = (f % 3) / 3f + padU, u1 = ((f % 3) + 1) / 3f - padU;
                float v0 = (f / 3) / 2f + padV, v1 = ((f / 3) + 1) / 2f - padV;
                Vector3 c = faceNormals[f] * halfExtent;
                Vector3 r = faceRight[f] * halfExtent;
                Vector3 u = faceUp[f] * halfExtent;
                int b = verts.Count;
                verts.Add(c - r - u); verts.Add(c + r - u); verts.Add(c + r + u); verts.Add(c - r + u);
                for (int k = 0; k < 4; k++) { norms.Add(faceNormals[f]); cols.Add(Color.white); }
                uvs.Add(new Vector2(u0, v0)); uvs.Add(new Vector2(u1, v0));
                uvs.Add(new Vector2(u1, v1)); uvs.Add(new Vector2(u0, v1));
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }

            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            ownedObjects.Add(mesh);
            return mesh;
        }

        Mesh BuildSquareRingPlateMesh(float outerHalf, float thickness)
        {
            var verts = new List<Vector3>(24);
            var norms = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var tris = new List<int>(36);
            float h = thickness * 0.5f;

            int bt = verts.Count;
            verts.Add(new Vector3(-outerHalf, h, -outerHalf)); verts.Add(new Vector3(outerHalf, h, -outerHalf));
            verts.Add(new Vector3(outerHalf, h, outerHalf));   verts.Add(new Vector3(-outerHalf, h, outerHalf));
            for (int k = 0; k < 4; k++) norms.Add(Vector3.up);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
            tris.Add(bt); tris.Add(bt + 2); tris.Add(bt + 1); tris.Add(bt); tris.Add(bt + 3); tris.Add(bt + 2);

            int bb = verts.Count;
            verts.Add(new Vector3(-outerHalf, -h, -outerHalf)); verts.Add(new Vector3(-outerHalf, -h, outerHalf));
            verts.Add(new Vector3(outerHalf, -h, outerHalf));   verts.Add(new Vector3(outerHalf, -h, -outerHalf));
            for (int k = 0; k < 4; k++) norms.Add(Vector3.down);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
            tris.Add(bb); tris.Add(bb + 2); tris.Add(bb + 1); tris.Add(bb); tris.Add(bb + 3); tris.Add(bb + 2);

            Vector3[] rimN = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            Vector3[] rimR = { Vector3.right, Vector3.left, Vector3.back, Vector3.forward };
            for (int s = 0; s < 4; s++)
            {
                int br = verts.Count;
                Vector3 c = rimN[s] * outerHalf;
                Vector3 r = rimR[s] * outerHalf;
                verts.Add(c - r - Vector3.up * h); verts.Add(c + r - Vector3.up * h);
                verts.Add(c + r + Vector3.up * h); verts.Add(c - r + Vector3.up * h);
                for (int k = 0; k < 4; k++) norms.Add(rimN[s]);
                uvs.Add(new Vector2(0.95f, 0.5f)); uvs.Add(new Vector2(0.95f, 0.5f));
                uvs.Add(new Vector2(0.95f, 0.5f)); uvs.Add(new Vector2(0.95f, 0.5f));
                tris.Add(br); tris.Add(br + 2); tris.Add(br + 1); tris.Add(br); tris.Add(br + 3); tris.Add(br + 2);
            }

            var mesh = new Mesh { name = "SquareVoxelPlanetaryRing" };
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            ownedObjects.Add(mesh);
            return mesh;
        }

        Material MakePlanetVoxelMaterial(string label, Texture2D tex, Color tint, float emissionGain, float cutoff = 0.42f)
        {
            var sh = Shader.Find("LestingCraft/SpacePlanetVoxel");
            if (sh != null)
            {
                var m = new Material(sh) { name = label, color = tint };
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                if (m.HasProperty("_EmissionGain")) m.SetFloat("_EmissionGain", emissionGain);
                if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", cutoff);
                m.mainTexture = tex;
                ownedObjects.Add(m);
                return m;
            }
            var fallback = MakeLitMaterial(label, tint, tint * emissionGain, 0.04f, 0.14f);
            if (fallback.HasProperty("_BaseMap")) fallback.SetTexture("_BaseMap", tex);
            fallback.mainTexture = tex;
            return fallback;
        }

        GameObject CreateAtlasBodyVisual(CelestialBodyDef body)
        {
            var go = new GameObject("Atlas_" + body.WireKey);
            go.transform.SetParent(spaceDomeRoot.transform, false);
            go.transform.localRotation = Quaternion.Euler(14f, 32f, -8f);
            float halfExtent = body.IsStar ? 9.6f : body.HasRings ? 7.8f : Mathf.Clamp((float)Math.Log10(Math.Max(200.0, body.RadiusKm)) * 1.55f, 2.2f, 8.2f);
            Color cA = new Color(body.ColorR, body.ColorG, body.ColorB, 1f);
            Color cB = new Color(body.SecondaryR, body.SecondaryG, body.SecondaryB, 1f);
            var tex = BuildAtlasCubeTexture(body, cA, cB);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildVoxelCubeMesh(halfExtent, "AtlasVoxelCube_" + (int)body.Id);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MakePlanetVoxelMaterial("AtlasSurface_" + body.WireKey, tex, Color.white, body.IsStar ? 0.95f : 0.18f, 0.05f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            bool hasClouds = body.Id == CelestialBodyId.Earth || body.Id == CelestialBodyId.Venus ||
                             body.Id == CelestialBodyId.Titan || body.Id == CelestialBodyId.Kepler452b ||
                             body.Id == CelestialBodyId.AndromedaPrime;
            if (hasClouds)
            {
                var clouds = new GameObject("CloudCube_" + body.WireKey);
                clouds.transform.SetParent(go.transform, false);
                var cloudTint = body.Id == CelestialBodyId.AndromedaPrime
                    ? new Color(0.86f, 0.78f, 1.0f)
                    : body.Id == CelestialBodyId.Titan ? new Color(0.96f, 0.80f, 0.52f) : Color.white;
                var cloudTex = BuildCloudCubeTexture((int)body.Id * 7 + 3, cloudTint);
                clouds.AddComponent<MeshFilter>().sharedMesh = BuildVoxelCubeMesh(halfExtent * 1.055f, "CloudShell_" + body.WireKey);
                var cmr = clouds.AddComponent<MeshRenderer>();
                cmr.sharedMaterial = MakePlanetVoxelMaterial("CloudsCutout_" + body.WireKey, cloudTex, Color.white, 0.20f, 0.45f);
                cmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            if (body.HasRings)
            {
                var ring = new GameObject("Rings_" + body.WireKey);
                ring.transform.SetParent(go.transform, false);
                ring.transform.localRotation = Quaternion.Euler(body.Id == CelestialBodyId.Uranus ? 97.8f : 24f, 16f, -10f);
                var ringTex = BuildSquarePlanetaryRingTexture(cA, cB);
                var rmf = ring.AddComponent<MeshFilter>();
                float outerHalf = halfExtent * (float)body.RingOuterScale;
                rmf.sharedMesh = BuildSquareRingPlateMesh(outerHalf, halfExtent * 0.065f);
                var rmr = ring.AddComponent<MeshRenderer>();
                rmr.sharedMaterial = MakePlanetVoxelMaterial("SquareRingsCutout_" + body.WireKey, ringTex, Color.white, 0.30f, 0.35f);
                rmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rmr.receiveShadows = false;
            }
            return go;
        }

        GameObject CreateSpiralGalaxyBackdrop()
        {
            var go = new GameObject("NeighborSpiralGalaxyBackdrop");
            go.transform.SetParent(spaceDomeRoot.transform, false);
            go.transform.localPosition = new Vector3(-56f, 38f, 146f);
            go.transform.localRotation = Quaternion.Euler(58f, -18f, 24f);
            var verts = new List<Vector3>(6400);
            var uvs = new List<Vector2>(6400);
            var cols = new List<Color>(6400);
            var tris = new List<int>(9600);
            var rng = new System.Random(31031);
            for (int i = 0; i < 1500; i++)
            {
                int arm = i % 2;
                float r = Mathf.Pow((float)rng.NextDouble(), 0.68f) * 44f;
                float ang = arm * Mathf.PI + r * 0.21f + ((float)rng.NextDouble() - 0.5f) * 0.62f;
                Vector3 p = new Vector3(Mathf.Cos(ang) * r, ((float)rng.NextDouble() - 0.5f) * (2.2f - r * 0.03f), Mathf.Sin(ang) * r * 0.62f);
                float sz = Mathf.Lerp(2.35f, 0.48f, r / 44f);
                bool h2Knot = rng.NextDouble() < 0.14 && r > 8f;
                Color c = r < 7f
                    ? Color.Lerp(new Color(1.0f, 0.95f, 0.80f, 0.92f), new Color(1.0f, 0.84f, 0.56f, 0.80f), r / 7f)
                    : h2Knot
                        ? new Color(0.95f, 0.38f, 0.72f, 0.78f)
                        : Color.Lerp(new Color(0.72f, 0.80f, 1.0f, 0.74f), new Color(0.42f, 0.52f, 0.98f, 0.52f), r / 44f);
                int b = verts.Count;
                verts.Add(p + new Vector3(-sz, 0f, -sz)); verts.Add(p + new Vector3(sz, 0f, -sz));
                verts.Add(p + new Vector3(sz, 0f, sz));   verts.Add(p + new Vector3(-sz, 0f, sz));
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
                cols.Add(c); cols.Add(c); cols.Add(c); cols.Add(c);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }
            var mesh = new Mesh { name = "SpiralGalaxyM31" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            ownedObjects.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MakeVertexColorMaterial("SpiralGalaxyMat", true);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return go;
        }

        GameObject CreateAsteroidBeltVisual()
        {
            var go = new GameObject("SolarAsteroidBelt");
            go.transform.SetParent(spaceDomeRoot.transform, false);
            var verts = new List<Vector3>(7680);
            var norms = new List<Vector3>(7680);
            var uvs = new List<Vector2>(7680);
            var cols = new List<Color>(7680);
            var tris = new List<int>(11520);
            var rng = new System.Random(413999);
            Vector3[] fn = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3[] fr = { Vector3.back, Vector3.forward, Vector3.right, Vector3.right, Vector3.right, Vector3.left };
            Vector3[] fu = { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };
            for (int i = 0; i < 320; i++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2.0);
                float r = 74f + ((float)rng.NextDouble() - 0.5f) * 9f;
                Vector3 p = new Vector3(Mathf.Cos(a) * r, ((float)rng.NextDouble() - 0.5f) * 3.4f, Mathf.Sin(a) * r);
                float s = 0.28f + (float)rng.NextDouble() * 0.62f;
                Quaternion rot = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, 0f);
                Color baseC = Color.Lerp(new Color(0.42f, 0.39f, 0.36f, 1f), new Color(0.66f, 0.62f, 0.55f, 1f), (float)rng.NextDouble());
                for (int f = 0; f < 6; f++)
                {
                    float shade = f == 2 ? 1.0f : f == 3 ? 0.55f : 0.80f;
                    Color fc = new Color(baseC.r * shade, baseC.g * shade, baseC.b * shade, 1f);
                    int b = verts.Count;
                    Vector3 n = rot * fn[f], right = rot * fr[f] * s, up = rot * fu[f] * s;
                    verts.Add(p + n * s - right - up); verts.Add(p + n * s + right - up);
                    verts.Add(p + n * s + right + up); verts.Add(p + n * s - right + up);
                    for (int k = 0; k < 4; k++) { norms.Add(n); cols.Add(fc); }
                    uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
                }
            }
            var mesh = new Mesh { name = "AsteroidBeltVoxelCubes" };
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            ownedObjects.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MakeVertexColorMaterial("AsteroidBeltMat");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return go;
        }

        GameObject CreateCelestialBody(string name, float halfExtent, SpaceTargetKind paletteKind, bool isSun)
        {
            var go = new GameObject(name);
            go.transform.SetParent(spaceDomeRoot.transform, false);
            go.transform.localRotation = Quaternion.Euler(16f, 28f, -10f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildPlanetCubeMesh(12, halfExtent, paletteKind, isSun);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = isSun
                ? MakeLitMaterial(name + "_Mat", new Color(1f, 0.92f, 0.68f), new Color(2.4f, 1.85f, 0.95f), 0f, 0.1f)
                : MakeVertexColorMaterial(name + "_VertexMat");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        Mesh BuildStarfieldMesh(int count, float radius, int seed)
        {
            var verts = new List<Vector3>(count * 4);
            var uvs = new List<Vector2>(count * 4);
            var cols = new List<Color>(count * 4);
            var tris = new List<int>(count * 6);
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                double u = rng.NextDouble() * 2.0 - 1.0;
                double theta = rng.NextDouble() * Math.PI * 2.0;
                float rxy = Mathf.Sqrt(Mathf.Max(0f, 1f - (float)(u * u)));
                Vector3 dir = new Vector3(rxy * Mathf.Cos((float)theta), (float)u, rxy * Mathf.Sin((float)theta));
                Vector3 center = dir * radius;
                Vector3 right = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 up = Vector3.Cross(right, dir).normalized;
                float size = Mathf.Lerp(0.18f, 0.72f, (float)(rng.NextDouble() * rng.NextDouble()));
                float temp = (float)rng.NextDouble();
                Color c = temp < 0.25f ? new Color(0.74f, 0.85f, 1.0f, 1f)
                        : temp < 0.72f ? new Color(0.98f, 0.97f, 0.93f, 1f)
                        : temp < 0.90f ? new Color(1.0f, 0.86f, 0.62f, 1f)
                                       : new Color(1.0f, 0.66f, 0.50f, 1f);
                c *= Mathf.Lerp(0.55f, 1.0f, (float)rng.NextDouble());
                c.a = 1f;
                int b = verts.Count;
                verts.Add(center - right * size - up * size);
                verts.Add(center + right * size - up * size);
                verts.Add(center + right * size + up * size);
                verts.Add(center - right * size + up * size);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
                cols.Add(c); cols.Add(c); cols.Add(c); cols.Add(c);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
            var mesh = new Mesh { name = "ProceduralStars" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            ownedObjects.Add(mesh);
            return mesh;
        }

        Mesh BuildPlanetCubeMesh(int gridPerFace, float halfExtent, SpaceTargetKind kind, bool isSun)
        {
            int cap = 6 * gridPerFace * gridPerFace * 4;
            var verts = new List<Vector3>(cap);
            var norms = new List<Vector3>(cap);
            var cols = new List<Color>(cap);
            var tris = new List<int>(6 * gridPerFace * gridPerFace * 6);
            Vector3[] fn = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3[] fr = { Vector3.back, Vector3.forward, Vector3.right, Vector3.right, Vector3.right, Vector3.left };
            Vector3[] fu = { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };
            float cell = (2f * halfExtent) / gridPerFace;

            for (int f = 0; f < 6; f++)
            {
                float shade = isSun ? 1f : (f == 2 ? 1.0f : f == 3 ? 0.62f : f == 4 ? 0.92f : 0.76f);
                for (int gy = 0; gy < gridPerFace; gy++)
                {
                    for (int gx = 0; gx < gridPerFace; gx++)
                    {
                        float s = ((gx + 0.5f) / gridPerFace) * 2f - 1f;
                        float t = ((gy + 0.5f) / gridPerFace) * 2f - 1f;
                        Vector3 sampleDir = CubeFaceDirection(f, s, t).normalized;
                        Color c = SamplePlanetSurfaceColor(kind, isSun, sampleDir) * shade;
                        c.a = 1f;
                        Vector3 center = fn[f] * halfExtent + fr[f] * (s * halfExtent) + fu[f] * (t * halfExtent);
                        Vector3 rx = fr[f] * (cell * 0.5f), uy = fu[f] * (cell * 0.5f);
                        int b = verts.Count;
                        verts.Add(center - rx - uy); verts.Add(center + rx - uy);
                        verts.Add(center + rx + uy); verts.Add(center - rx + uy);
                        for (int k = 0; k < 4; k++) { norms.Add(fn[f]); cols.Add(c); }
                        tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                        tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
                    }
                }
            }
            var mesh = new Mesh { name = "PlanetVoxelCube_" + kind };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            ownedObjects.Add(mesh);
            return mesh;
        }

        static Color SamplePlanetSurfaceColor(SpaceTargetKind kind, bool isSun, Vector3 n)
        {
            if (isSun) return new Color(1f, 0.90f, 0.58f, 1f);
            float band = Mathf.Sin(n.y * 9.5f + n.x * 2.2f) * 0.5f + 0.5f;
            float continent = Mathf.Sin(n.x * 4.1f + 0.7f) * Mathf.Cos(n.z * 3.7f - 0.4f) + Mathf.Sin(n.y * 3.1f) * 0.35f;
            float polar = Mathf.Abs(n.y);
            switch (kind)
            {
                case SpaceTargetKind.Earth:
                    if (polar > 0.80f) return new Color(0.92f, 0.96f, 1.0f);
                    if (continent > 0.08f) return Color.Lerp(new Color(0.16f, 0.46f, 0.19f), new Color(0.46f, 0.42f, 0.25f), band * 0.5f);
                    return Color.Lerp(new Color(0.06f, 0.23f, 0.58f), new Color(0.14f, 0.44f, 0.78f), band * 0.4f);
                case SpaceTargetKind.Moon:
                    return Color.Lerp(new Color(0.36f, 0.37f, 0.40f), new Color(0.74f, 0.75f, 0.78f), Mathf.Clamp01(continent * 0.6f + 0.5f));
                case SpaceTargetKind.Mars:
                    if (polar > 0.86f) return new Color(0.94f, 0.92f, 0.90f);
                    return Color.Lerp(new Color(0.48f, 0.19f, 0.11f), new Color(0.84f, 0.42f, 0.22f), Mathf.Clamp01(continent * 0.55f + 0.5f));
                case SpaceTargetKind.Venus:
                    return Color.Lerp(new Color(0.76f, 0.58f, 0.28f), new Color(0.96f, 0.86f, 0.62f), band);
                default: // Mercury
                    return Color.Lerp(new Color(0.32f, 0.29f, 0.27f), new Color(0.64f, 0.59f, 0.53f), Mathf.Clamp01(continent * 0.65f + 0.5f));
            }
        }

        void EnsurePlumes(Transform shuttleTransform)
        {
            if (!shuttleTransform) return;
            if (!plumeRoot)
            {
                plumeRoot = new GameObject("ShuttleEnginePlumes");
                var flameMat = MakeLitMaterial("RocketPlumeFlame", new Color(1f, 0.65f, 0.22f), new Color(3.2f, 1.6f, 0.45f), 0f, 0.2f);
                var plasmaMat = MakeLitMaterial("ReentryPlasma", new Color(1f, 0.42f, 0.16f), new Color(2.5f, 0.85f, 0.25f), 0f, 0.15f);

                mainFlame = MakePlumeCone("MainEnginePlume", plumeRoot.transform, new Vector3(0.5f, -5.5f, 3.2f), new Vector3(2.4f, 9.5f, 2.4f), flameMat);
                leftPlume = MakePlumeCone("BoosterPlumePort", plumeRoot.transform, new Vector3(-2.1f, -4.2f, 3.2f), new Vector3(1.4f, 6.8f, 1.4f), flameMat);
                rightPlume = MakePlumeCone("BoosterPlumeStarboard", plumeRoot.transform, new Vector3(3.1f, -4.2f, 3.2f), new Vector3(1.4f, 6.8f, 1.4f), flameMat);
                plasmaShield = MakePlumeCone("AtmosphericPlasmaEnvelope", plumeRoot.transform, new Vector3(0.5f, 47f, 4.2f), new Vector3(7.4f, 5.2f, 7.4f), plasmaMat);

                var lightGo = new GameObject("RocketExhaustLight");
                lightGo.transform.SetParent(plumeRoot.transform, false);
                lightGo.transform.localPosition = new Vector3(0.5f, -3f, 3.2f);
                engineLight = lightGo.AddComponent<Light>();
                engineLight.type = LightType.Point;
                engineLight.color = new Color(1f, 0.68f, 0.28f);
                engineLight.range = 42f;
                engineLight.intensity = 6f;
            }
            if (plumeRoot.transform.parent != shuttleTransform)
            {
                plumeRoot.transform.SetParent(shuttleTransform, false);
                plumeRoot.transform.localPosition = Vector3.zero;
                plumeRoot.transform.localRotation = Quaternion.identity;
            }
        }

        static Transform MakePlumeCone(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            SafeDestroy(go.GetComponent<Collider>());
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        public void SetSpaceFlightActive(
            bool inFlight,
            SpaceTargetKind target,
            Dimension currentOrigin,
            FlightStage stage,
            double progress01,
            double altitudeMeters,
            double speedMps,
            bool retroBurning,
            Transform shuttleTransform,
            Transform viewer)
        {
            BuildCelestialRig();
            if (!inFlight)
            {
                if (plumeRoot) plumeRoot.SetActive(false);
                return;
            }

            if (spaceDomeRoot)
            {
                spaceDomeRoot.SetActive(true);
                if (viewer) spaceDomeRoot.transform.position = viewer.position;
                float spaceBlend = Mathf.Clamp01((float)(altitudeMeters / 900.0));
                Color skyCol = Color.Lerp(new Color(0.14f, 0.30f, 0.56f), new Color(0.008f, 0.011f, 0.024f), spaceBlend);
                if (domeRenderer && domeRenderer.sharedMaterial.HasProperty("_BaseColor"))
                    domeRenderer.sharedMaterial.SetColor("_BaseColor", skyCol);
                if (starfieldObj) starfieldObj.SetActive(spaceBlend > 0.22f);
                if (sunObj)
                {
                    sunObj.SetActive(spaceBlend > 0.15f);
                    sunObj.transform.localPosition = new Vector3(-115f, 82f, 130f);
                }

                SpaceTargetKind originKind = (SpaceTargetKind)(int)currentOrigin;
                foreach (var kv in planetSpheres)
                {
                    bool isOrigin = kv.Key == originKind;
                    bool isDest = kv.Key == target || (target == SpaceTargetKind.OrbitalStation && kv.Key == SpaceTargetKind.Earth);
                    kv.Value.SetActive(isOrigin || isDest);
                    kv.Value.transform.Rotate(0f, Time.deltaTime * 4.5f, 0f, Space.Self);
                    if (isDest)
                    {
                        float dist = Mathf.Lerp(175f, 36f, Mathf.SmoothStep(0f, 1f, (float)progress01));
                        kv.Value.transform.localPosition = new Vector3(8f, 16f, dist);
                    }
                    else if (isOrigin)
                    {
                        float dist = Mathf.Lerp(38f, 185f, Mathf.SmoothStep(0f, 1f, (float)progress01));
                        kv.Value.transform.localPosition = new Vector3(-34f, -48f, -dist);
                    }
                }
            }

            EnsurePlumes(shuttleTransform);
            if (plumeRoot)
            {
                bool engineOn = stage == FlightStage.Ascent || retroBurning || progress01 < 0.35;
                plumeRoot.SetActive(engineOn || stage == FlightStage.Approach);
                float flicker = 0.86f + 0.24f * Mathf.Sin(Time.time * 47f);
                if (mainFlame) mainFlame.localScale = new Vector3(2.6f, (engineOn ? 9.8f : 2.2f) * flicker, 2.6f);
                if (leftPlume) leftPlume.gameObject.SetActive(stage == FlightStage.Ascent);
                if (rightPlume) rightPlume.gameObject.SetActive(stage == FlightStage.Ascent);
                if (plasmaShield) plasmaShield.gameObject.SetActive(stage == FlightStage.Approach && retroBurning);
                if (engineLight) engineLight.intensity = engineOn ? 6.5f * flicker : 1.2f;
            }
        }

        public void SetOpenSpaceFlightActive(OpenSpaceHelm helm, Transform shuttleTransform, Transform viewer)
        {
            BuildCelestialRig();
            if (helm == null || !helm.Active)
            {
                if (spaceDomeRoot) spaceDomeRoot.SetActive(false);
                if (spaceKeyLight) spaceKeyLight.enabled = false;
                foreach (var kv in atlasBodyObjects) if (kv.Value) kv.Value.SetActive(false);
                if (spiralGalaxyObj) spiralGalaxyObj.SetActive(false);
                if (asteroidBeltObj) asteroidBeltObj.SetActive(false);
                if (cachedWorldAtmosphere) cachedWorldAtmosphere.SetActive(true);
                if (Camera.main) Camera.main.clearFlags = CameraClearFlags.Skybox;
                return;
            }

            if (spaceDomeRoot)
            {
                if (!cachedWorldAtmosphere) cachedWorldAtmosphere = GameObject.Find("WorldAtmosphere");
                if (cachedWorldAtmosphere) cachedWorldAtmosphere.SetActive(false);
                RenderSettings.fog = false;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.24f);
                if (domeRenderer) domeRenderer.enabled = false;
                if (Camera.main && Camera.main.farClipPlane < 900f) Camera.main.farClipPlane = 900f;
                if (spaceKeyLight)
                {
                    spaceKeyLight.enabled = true;
                    spaceKeyLight.intensity = 2.55f;
                }
                spaceDomeRoot.SetActive(true);
                if (viewer) spaceDomeRoot.transform.position = viewer.position;
                bool jumping = helm.InHyperjump;
                Color deepSky = helm.CurrentGalaxy == GalaxyId.Andromeda
                    ? new Color(0.012f, 0.007f, 0.030f)
                    : new Color(0.004f, 0.007f, 0.018f);
                if (jumping)
                    deepSky = Color.Lerp(deepSky, new Color(0.18f, 0.42f, 0.85f), Mathf.Sin((float)helm.HyperjumpProgress * Mathf.PI));
                if (Camera.main)
                {
                    Camera.main.clearFlags = CameraClearFlags.SolidColor;
                    Camera.main.backgroundColor = deepSky;
                }
                if (starfieldObj) starfieldObj.SetActive(true);
                if (sunObj) sunObj.SetActive(false);
                foreach (var kv in planetSpheres) if (kv.Value) kv.Value.SetActive(false);
                if (spiralGalaxyObj)
                {
                    spiralGalaxyObj.SetActive(true);
                    spiralGalaxyObj.transform.Rotate(0f, Time.deltaTime * 1.4f, 0f, Space.Self);
                }
                if (asteroidBeltObj)
                {
                    bool showBelt = helm.CurrentSystem == StarSystemId.SolarSystem;
                    asteroidBeltObj.SetActive(showBelt);
                    if (showBelt) asteroidBeltObj.transform.Rotate(0f, Time.deltaTime * 0.9f, 0f, Space.Self);
                }

                // Map real million-km vectors in the active StarSystem into compressed 3D sky-dome coordinates around the shuttle
                Quaternion invShipRot = Quaternion.Inverse(helm.Rotation);
                foreach (var body in CelestialAtlas.Bodies)
                {
                    if (!atlasBodyObjects.TryGetValue(body.Id, out var obj) || !obj) continue;
                    bool inSys = body.System == helm.CurrentSystem;
                    obj.SetActive(inSys);
                    if (!inSys) continue;
                    SpaceVector deltaKm = CelestialAtlas.WorldPositionKm(body.Id, helm.OrbitTimeDays) - helm.PositionKm;
                    double distKm = Math.Max(1.0, deltaKm.Magnitude);
                    // Logarithmic compression keeps nearby moons large & approachable while distant outer giants stay visible on the ecliptic!
                    float renderDist = Mathf.Clamp(22f + (float)Math.Log10(distKm / Math.Max(1000.0, body.RadiusKm * 2.0) + 1.0) * 34f, 18f, 182f);
                    SpaceVector dir = deltaKm.Normalized;
                    Vector3 rel = invShipRot * new Vector3((float)dir.X, (float)dir.Y, (float)dir.Z) * renderDist;
                    obj.transform.localPosition = rel;
                    obj.transform.Rotate(0f, Time.deltaTime * 5.0f, 0f, Space.Self);
                }
            }

            EnsurePlumes(shuttleTransform);
            if (plumeRoot)
            {
                bool engineOn = helm.Throttle > 0.04f || helm.InHyperjump;
                plumeRoot.SetActive(engineOn);
                float flicker = 0.85f + 0.25f * Mathf.Sin(Time.time * 49f);
                if (mainFlame) mainFlame.localScale = new Vector3(2.6f, Mathf.Lerp(2.4f, 11.2f, helm.Throttle) * flicker, 2.6f);
                if (leftPlume) leftPlume.gameObject.SetActive(helm.Boost);
                if (rightPlume) rightPlume.gameObject.SetActive(helm.Boost);
                if (plasmaShield) plasmaShield.gameObject.SetActive(helm.InHyperjump);
                if (engineLight) engineLight.intensity = engineOn ? Mathf.Lerp(1.6f, 7.5f, helm.Throttle) * flicker : 0.6f;
            }
        }

        public void SyncDreamWatcher(DreamAnomaly dream, Transform viewer)
        {
            if (dream == null || !dream.IsActive)
            {
                if (dreamWatcherRoot) dreamWatcherRoot.SetActive(false);
                return;
            }
            if (!dreamWatcherRoot)
            {
                dreamWatcherRoot = new GameObject("DreamShadowWatcher");
                dreamWatcherRoot.transform.SetParent(transform, false);
                var shadowMat = MakeLitMaterial("DreamShadowSilhouette", new Color(0.012f, 0.010f, 0.018f), new Color(0.028f, 0.014f, 0.045f), 0f, 0.05f);
                var eyeMat = MakeLitMaterial("DreamShadowEyes", new Color(0.95f, 0.96f, 1.0f), new Color(2.4f, 2.2f, 2.8f), 0f, 0.9f);

                var torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
                torso.name = "WatcherTorso";
                torso.transform.SetParent(dreamWatcherRoot.transform, false);
                torso.transform.localPosition = new Vector3(0f, 1.24f, 0f);
                torso.transform.localScale = new Vector3(0.48f, 2.38f, 0.24f);
                SafeDestroy(torso.GetComponent<Collider>());
                torso.GetComponent<MeshRenderer>().sharedMaterial = shadowMat;

                var shoulders = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shoulders.name = "WatcherShoulders";
                shoulders.transform.SetParent(dreamWatcherRoot.transform, false);
                shoulders.transform.localPosition = new Vector3(0f, 2.18f, 0f);
                shoulders.transform.localScale = new Vector3(0.84f, 0.22f, 0.26f);
                SafeDestroy(shoulders.GetComponent<Collider>());
                shoulders.GetComponent<MeshRenderer>().sharedMaterial = shadowMat;

                for (int s = -1; s <= 1; s += 2)
                {
                    var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    arm.name = "WatcherArm" + s;
                    arm.transform.SetParent(dreamWatcherRoot.transform, false);
                    arm.transform.localPosition = new Vector3(s * 0.44f, 1.26f, 0.04f);
                    arm.transform.localRotation = Quaternion.Euler(0f, 0f, s * -5f);
                    arm.transform.localScale = new Vector3(0.14f, 1.86f, 0.14f);
                    SafeDestroy(arm.GetComponent<Collider>());
                    arm.GetComponent<MeshRenderer>().sharedMaterial = shadowMat;
                }

                var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
                head.name = "WatcherHead";
                head.transform.SetParent(dreamWatcherRoot.transform, false);
                head.transform.localPosition = new Vector3(0f, 2.62f, 0f);
                head.transform.localScale = new Vector3(0.32f, 0.42f, 0.28f);
                SafeDestroy(head.GetComponent<Collider>());
                head.GetComponent<MeshRenderer>().sharedMaterial = shadowMat;

                for (int i = -1; i <= 1; i += 2)
                {
                    var eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    eye.name = "WatcherEye" + i;
                    eye.transform.SetParent(dreamWatcherRoot.transform, false);
                    eye.transform.localPosition = new Vector3(i * 0.078f, 2.65f, 0.146f);
                    eye.transform.localScale = new Vector3(0.052f, 0.028f, 0.02f);
                    SafeDestroy(eye.GetComponent<Collider>());
                    eye.GetComponent<MeshRenderer>().sharedMaterial = eyeMat;
                }

                var auraGo = new GameObject("WatcherAuraLight");
                auraGo.transform.SetParent(dreamWatcherRoot.transform, false);
                auraGo.transform.localPosition = new Vector3(0f, 2.2f, 0.6f);
                var aura = auraGo.AddComponent<Light>();
                aura.type = LightType.Point;
                aura.color = new Color(0.58f, 0.34f, 0.78f);
                aura.range = 14f;
                aura.intensity = 1.65f;
                aura.shadows = LightShadows.None;
            }
            dreamWatcherRoot.SetActive(true);
            dreamWatcherRoot.transform.position = new Vector3(dream.StalkerX, dream.StalkerY - 1f, dream.StalkerZ);
            if (viewer)
            {
                Vector3 toViewer = viewer.position - dreamWatcherRoot.transform.position;
                toViewer.y = 0f;
                if (toViewer.sqrMagnitude > 0.001f)
                    dreamWatcherRoot.transform.rotation = Quaternion.LookRotation(toViewer.normalized, Vector3.up);
            }
        }

        public void ApplyPlanetAtmosphereAndGravity(Dimension dim, FirstPersonController player, Camera cam)
        {
            BuildCelestialRig();
            if (player) player.GravityScale = GravityForDimension(dim);
            if (plumeRoot) plumeRoot.SetActive(false);
            foreach (var kv in atlasBodyObjects) if (kv.Value) kv.Value.SetActive(false);
            if (spiralGalaxyObj) spiralGalaxyObj.SetActive(dim == Dimension.AndromedaPrime || dim == Dimension.Somnia);
            if (asteroidBeltObj) asteroidBeltObj.SetActive(false);

            if (dim == Dimension.Dream)
            {
                if (spaceDomeRoot) spaceDomeRoot.SetActive(false);
                Color dreamFog = new Color(0.17f, 0.15f, 0.23f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = dreamFog;
                RenderSettings.fogDensity = 0.038f;
                if (cam) cam.backgroundColor = dreamFog;
                return;
            }

            if (dreamWatcherRoot) dreamWatcherRoot.SetActive(false);

            if (dim == Dimension.Lestingia && !IsStationSpawned)
            {
                if (spaceDomeRoot) spaceDomeRoot.SetActive(false);
                RenderSettings.fog = false;
                if (cam) cam.backgroundColor = new Color(0.42f, 0.66f, 0.78f);
                return;
            }

            if (spaceDomeRoot)
            {
                spaceDomeRoot.SetActive(true);
                if (player) spaceDomeRoot.transform.position = player.transform.position;
            }

            Color sky = new Color(0.01f, 0.013f, 0.026f);
            Color emit = new Color(0.008f, 0.01f, 0.02f);
            bool showStars = true;
            SpaceTargetKind overheadBody = SpaceTargetKind.Earth;
            float sunScale = 1.0f;

            if (IsStationSpawned)
            {
                sky = new Color(0.006f, 0.010f, 0.022f);
                overheadBody = SpaceTargetKind.Earth;
            }
            else if (dim == Dimension.Moon)
            {
                sky = new Color(0.008f, 0.010f, 0.018f);
                overheadBody = SpaceTargetKind.Earth;
            }
            else if (dim == Dimension.Mars)
            {
                sky = new Color(0.46f, 0.24f, 0.14f);
                emit = new Color(0.28f, 0.13f, 0.07f);
                showStars = false;
                overheadBody = SpaceTargetKind.Moon;
                sunScale = 0.72f;
            }
            else if (dim == Dimension.Venus)
            {
                sky = new Color(0.68f, 0.48f, 0.18f);
                emit = new Color(0.42f, 0.27f, 0.08f);
                showStars = false;
                overheadBody = SpaceTargetKind.Earth;
                sunScale = 0.55f;
            }
            else if (dim == Dimension.Mercury)
            {
                sky = new Color(0.018f, 0.014f, 0.012f);
                overheadBody = SpaceTargetKind.Venus;
                sunScale = 2.35f;
            }
            else if (dim == Dimension.Europa)
            {
                sky = new Color(0.012f, 0.024f, 0.048f);
                sunScale = 0.28f;
            }
            else if (dim == Dimension.Titan)
            {
                sky = new Color(0.58f, 0.36f, 0.14f);
                emit = new Color(0.32f, 0.18f, 0.06f);
                showStars = false;
                sunScale = 0.22f;
            }
            else if (dim == Dimension.Pluto)
            {
                sky = new Color(0.008f, 0.010f, 0.022f);
                sunScale = 0.10f;
            }
            else if (dim == Dimension.ProximaB || dim == Dimension.Trappist1e)
            {
                sky = new Color(0.24f, 0.08f, 0.09f);
                emit = new Color(0.14f, 0.04f, 0.05f);
                sunScale = 1.45f;
            }
            else if (dim == Dimension.Kepler452b)
            {
                sky = new Color(0.18f, 0.36f, 0.44f);
                sunScale = 1.08f;
            }
            else if (dim == Dimension.AndromedaPrime || dim == Dimension.Somnia)
            {
                sky = new Color(0.06f, 0.03f, 0.14f);
                emit = new Color(0.04f, 0.02f, 0.10f);
                sunScale = dim == Dimension.Somnia ? 0.65f : 1.75f;
            }

            if (cam) cam.backgroundColor = sky;
            RenderSettings.fog = false;
            if (domeRenderer)
            {
                if (domeRenderer.sharedMaterial.HasProperty("_BaseColor")) domeRenderer.sharedMaterial.SetColor("_BaseColor", sky);
                if (domeRenderer.sharedMaterial.HasProperty("_EmissionColor")) domeRenderer.sharedMaterial.SetColor("_EmissionColor", emit);
            }
            if (starfieldObj) starfieldObj.SetActive(showStars);
            if (sunObj)
            {
                sunObj.SetActive(true);
                sunObj.transform.localScale = Vector3.one * sunScale;
                sunObj.transform.localPosition = new Vector3(-95f, 92f, 110f);
            }
            foreach (var kv in planetSpheres)
            {
                bool show = kv.Key == overheadBody && (IsStationSpawned || dim == Dimension.Moon || dim == Dimension.Mercury);
                kv.Value.SetActive(show);
                if (show)
                {
                    kv.Value.transform.localPosition = IsStationSpawned ? new Vector3(0f, -68f, 42f) : new Vector3(35f, 96f, 115f);
                }
            }
        }

        public void SpawnOrbitalStationInterior(int worldSeed, Vector3 origin)
        {
            DespawnOrbitalStationInterior();
            stationRoot = new GameObject("OrbitalStation_WalkableHub");
            stationRoot.transform.position = origin;

            var dock = new StationDocking();
            dock.TryLatch(OrbitalStation.Port, new SpaceVector(0, 0, 0), new SpaceVector(1, 0, 0), new SpaceVector(-1, 0, 0), true, true);
            dock.Advance(OrbitalStation.PressurizationSeconds);
            dock.SetHatch(true);
            ActiveStationDock = dock;

            // Load approved 3D voxel Space/Station asset + build colliding station deck & modules
            var assetText = Resources.Load<TextAsset>("Space/Station") ?? Resources.Load<TextAsset>("Models/Station");
            if (assetText != null && assetText.bytes != null && assetText.bytes.Length > 32)
            {
                var stationAsset = SpaceVoxelAsset.Read(assetText.bytes);
                var stationMeshObj = new GameObject("ApprovedStationHullMesh");
                stationMeshObj.transform.SetParent(stationRoot.transform, false);
                stationMeshObj.transform.localPosition = new Vector3(-stationAsset.X * 0.5f, 0f, -stationAsset.Z * 0.5f);
                var builtMesh = BuildSpaceAssetMesh(stationAsset);
                stationMeshObj.AddComponent<MeshFilter>().sharedMesh = builtMesh;
                var mr = stationMeshObj.AddComponent<MeshRenderer>();
                mr.sharedMaterial = MakeVertexColorMaterial("StationHullVertexMat");
                var mc = stationMeshObj.AddComponent<MeshCollider>();
                mc.sharedMesh = builtMesh;
            }

            // Build spacious pressurized observation & trading concourse inside the station hub
            var deckMat = MakeLitMaterial("StationDeckAlloy", new Color(0.22f, 0.29f, 0.36f), Color.black, 0.55f, 0.52f);
            var wallMat = MakeLitMaterial("StationModuleBulkhead", new Color(0.76f, 0.81f, 0.87f), Color.black, 0.15f, 0.35f);
            var glassMat = MakeLitMaterial("StationCupolaGlass", new Color(0.18f, 0.45f, 0.62f, 0.5f), new Color(0.05f, 0.16f, 0.26f), 0.1f, 0.9f);
            var accentMat = MakeLitMaterial("StationHatchGlow", new Color(0.12f, 0.72f, 0.95f), new Color(0.25f, 1.35f, 1.85f), 0.2f, 0.6f);

            MakeBox("ConcourseFloor", stationRoot.transform, new Vector3(0f, -0.25f, 0f), new Vector3(22f, 0.5f, 22f), deckMat, true);
            MakeBox("ConcourseCeiling", stationRoot.transform, new Vector3(0f, 5.25f, 0f), new Vector3(22f, 0.5f, 22f), wallMat, true);
            MakeBox("WallNorthCupola", stationRoot.transform, new Vector3(0f, 2.5f, 11f), new Vector3(22f, 5f, 0.4f), glassMat, true);
            MakeBox("WallSouthAirlock", stationRoot.transform, new Vector3(0f, 2.5f, -11f), new Vector3(22f, 5f, 0.4f), wallMat, true);
            MakeBox("WallWest", stationRoot.transform, new Vector3(-11f, 2.5f, 0f), new Vector3(0.4f, 5f, 22f), wallMat, true);
            MakeBox("WallEast", stationRoot.transform, new Vector3(11f, 2.5f, 0f), new Vector3(0.4f, 5f, 22f), wallMat, true);
            MakeBox("ShuttleDockingHatch", stationRoot.transform, new Vector3(0f, 1.6f, -10.55f), new Vector3(2.4f, 3.0f, 0.35f), accentMat, true);

            for (int lx = -6; lx <= 6; lx += 6)
            {
                var lgo = new GameObject("StationModuleLight_" + lx);
                lgo.transform.SetParent(stationRoot.transform, false);
                lgo.transform.localPosition = new Vector3(lx, 4.4f, 0f);
                var lt = lgo.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(0.88f, 0.94f, 1.0f);
                lt.range = 18f;
                lt.intensity = 2.4f;
            }

            string[] titles = {
                StationAstronauts.Profession(0) + " · [E] Обмен",
                StationAstronauts.Profession(1) + " · [E] Обмен",
                StationAstronauts.Profession(2) + " · [E] Обмен"
            };
            Vector3[] spots = { new Vector3(-5.2f, 0f, 4.5f), new Vector3(0f, 0f, 6.2f), new Vector3(5.2f, 0f, 4.5f) };
            stationAstronauts.Clear();
            for (int i = 0; i < 3; i++)
            {
                var npc = new GameObject("StationAstronaut_" + i);
                npc.transform.SetParent(stationRoot.transform, false);
                npc.transform.localPosition = spots[i];
                npc.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var vis = npc.AddComponent<CharacterVisual>();
                vis.Build();
                vis.SetEquipment(new[] { 130, 104, 131, 132, 105, 106, 107, 108 });
                var col = npc.AddComponent<CapsuleCollider>();
                col.height = 1.8f;
                col.radius = 0.42f;
                col.center = new Vector3(0f, 0.9f, 0f);
                BuildFloatingLabel(npc.transform, titles[i], new Vector3(0f, 2.25f, 0f), new Color(0.55f, 0.92f, 1f));
                stationAstronauts.Add(npc.transform);
            }

            BuildFloatingLabel(stationRoot.transform, "ШЛЮЗ ШАТТЛА · [E] Вернуться в корабль", new Vector3(0f, 3.4f, -10.1f), new Color(1f, 0.85f, 0.42f));
            StationSpawnWorldPos = origin + new Vector3(0f, 0.6f, -6.5f);
            StationReturnHatchWorldPos = origin + new Vector3(0f, 1.2f, -10.2f);
        }

        public void DespawnOrbitalStationInterior()
        {
            stationAstronauts.Clear();
            ActiveStationDock = null;
            if (stationRoot)
            {
                Destroy(stationRoot);
                stationRoot = null;
            }
        }

        public int GetNearestAstronautRole(Vector3 playerWorldPos, float maxDist = 4.2f)
        {
            if (!stationRoot) return -1;
            int best = -1;
            float bestSq = maxDist * maxDist;
            for (int i = 0; i < stationAstronauts.Count; i++)
            {
                var t = stationAstronauts[i];
                if (!t) continue;
                float sq = (t.position - playerWorldPos).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = i;
                }
            }
            return best;
        }

        Mesh BuildSpaceAssetMesh(SpaceVoxelAsset asset)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            Vector3Int[] dirs = {
                new Vector3Int(1,0,0), new Vector3Int(-1,0,0),
                new Vector3Int(0,1,0), new Vector3Int(0,-1,0),
                new Vector3Int(0,0,1), new Vector3Int(0,0,-1)
            };
            Vector3[][] faceVerts = {
                new[]{new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(1,1,1),new Vector3(1,0,1)},
                new[]{new Vector3(0,0,1),new Vector3(0,1,1),new Vector3(0,1,0),new Vector3(0,0,0)},
                new[]{new Vector3(0,1,0),new Vector3(0,1,1),new Vector3(1,1,1),new Vector3(1,1,0)},
                new[]{new Vector3(0,0,1),new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,0,1)},
                new[]{new Vector3(1,0,1),new Vector3(1,1,1),new Vector3(0,1,1),new Vector3(0,0,1)},
                new[]{new Vector3(0,0,0),new Vector3(0,1,0),new Vector3(1,1,0),new Vector3(1,0,0)}
            };
            for (int x = 0; x < asset.X; x++)
                for (int y = 0; y < asset.Y; y++)
                    for (int z = 0; z < asset.Z; z++)
                    {
                        byte m = asset.At(x, y, z);
                        if (m == 0) continue;
                        var pc = asset.Palette[m];
                        Color c = new Color(pc.R / 255f, pc.G / 255f, pc.B / 255f, pc.A / 255f);
                        for (int f = 0; f < 6; f++)
                        {
                            var d = dirs[f];
                            if (asset.At(x + d.x, y + d.y, z + d.z) != 0) continue;
                            int baseIdx = verts.Count;
                            float shade = d.y > 0 ? 1.0f : d.y < 0 ? 0.58f : 0.82f;
                            Color shaded = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                            for (int k = 0; k < 4; k++)
                            {
                                verts.Add(new Vector3(x, y, z) + faceVerts[f][k]);
                                norms.Add(new Vector3(d.x, d.y, d.z));
                                cols.Add(shaded);
                            }
                            tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
                            tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
                        }
                    }
            var mesh = new Mesh { name = "StationApprovedMesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            ownedObjects.Add(mesh);
            return mesh;
        }

        static Transform MakeBox(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) SafeDestroy(go.GetComponent<Collider>());
            return go.transform;
        }

        static void BuildFloatingLabel(Transform parent, string text, Vector3 localOffset, Color color)
        {
            var go = new GameObject("WorldLabel");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localOffset;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = 0.14f;
            tm.fontSize = 36;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
        }

        void Update()
        {
            if (spaceDomeRoot && spaceDomeRoot.activeSelf && Camera.main)
            {
                spaceDomeRoot.transform.position = Camera.main.transform.position;
            }
        }

        void OnDestroy()
        {
            DespawnOrbitalStationInterior();
            for (int i = 0; i < ownedObjects.Count; i++)
                if (ownedObjects[i]) Destroy(ownedObjects[i]);
            ownedObjects.Clear();
        }
    }

    /// <summary>
    /// Articulated 3D Planetary Guardian Bosses on Mars, Venus, and Mercury with
    /// glowing weak-point cores, telegraphed shockwaves, hit flashes, and rare rewards.
    /// </summary>
    public sealed class PlanetaryBossController : MonoBehaviour
    {
        readonly List<Material> bossMaterials = new List<Material>();
        Transform bodyPivot, coreSphere, leftArm, rightArm, haloRing, shockwaveRing;
        Light coreLight;
        float attackTimer = 4.5f;
        float telegraphTimer;
        float hitFlashTimer;
        Color baseCoreEmission;

        public Dimension BossDimension { get; private set; }
        public string BossName { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public bool Defeated { get; private set; }
        public Vector3 ArenaPosition { get; private set; }

        public static PlanetaryBossController SpawnForPlanet(
            Transform parent,
            Dimension dim,
            VoxelWorld world,
            int seed,
            bool alreadyDefeated,
            Vector3 nearAnchor)
        {
            if (alreadyDefeated || (dim != Dimension.Mars && dim != Dimension.Venus && dim != Dimension.Mercury))
                return null;

            int bx = Mathf.RoundToInt(nearAnchor.x) + 34;
            int bz = Mathf.RoundToInt(nearAnchor.z) + 22;
            int groundY = TerrainGenerator.Height(bx, bz, seed, dim);
            Vector3 arena = new Vector3(bx + 0.5f, groundY + 1.1f, bz + 0.5f);

            var go = new GameObject("PlanetaryGuardianBoss_" + dim);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.position = arena;

            var boss = go.AddComponent<PlanetaryBossController>();
            boss.BossDimension = dim;
            boss.ArenaPosition = arena;
            boss.BuildSculpt(dim);
            return boss;
        }

        void BuildSculpt(Dimension dim)
        {
            Color armorCol, trimCol, coreCol;
            if (dim == Dimension.Mars)
            {
                BossName = "Железный Колосс Марса";
                MaxHealth = 260;
                armorCol = new Color(0.42f, 0.16f, 0.12f);
                trimCol = new Color(0.68f, 0.34f, 0.18f);
                coreCol = new Color(1.0f, 0.38f, 0.12f);
            }
            else if (dim == Dimension.Venus)
            {
                BossName = "Серный Левиафан Венеры";
                MaxHealth = 320;
                armorCol = new Color(0.32f, 0.26f, 0.11f);
                trimCol = new Color(0.85f, 0.68f, 0.18f);
                coreCol = new Color(0.95f, 0.85f, 0.22f);
            }
            else
            {
                BossName = "Солнечный Архонт Меркурия";
                MaxHealth = 380;
                armorCol = new Color(0.18f, 0.19f, 0.24f);
                trimCol = new Color(0.92f, 0.72f, 0.32f);
                coreCol = new Color(0.35f, 0.88f, 1.0f);
            }
            Health = MaxHealth;
            baseCoreEmission = coreCol * 2.8f;

            var armorMat = MakeMat("BossArmor", armorCol, Color.black, 0.72f, 0.45f);
            var trimMat = MakeMat("BossTrim", trimCol, coreCol * 0.45f, 0.80f, 0.55f);
            var coreMat = MakeMat("BossCore", coreCol, baseCoreEmission, 0.1f, 0.85f);

            bodyPivot = new GameObject("BossArticulatedRoot").transform;
            bodyPivot.SetParent(transform, false);

            // Legs & armored torso
            Part(PrimitiveType.Cube, "LeftLeg", bodyPivot, new Vector3(-1.05f, 1.2f, 0f), new Vector3(0.95f, 2.4f, 1.05f), armorMat);
            Part(PrimitiveType.Cube, "RightLeg", bodyPivot, new Vector3(1.05f, 1.2f, 0f), new Vector3(0.95f, 2.4f, 1.05f), armorMat);
            Part(PrimitiveType.Cube, "PelvisPlate", bodyPivot, new Vector3(0f, 2.55f, 0f), new Vector3(2.9f, 0.75f, 1.5f), trimMat);
            Part(PrimitiveType.Cube, "ArmoredChest", bodyPivot, new Vector3(0f, 4.15f, 0f), new Vector3(3.6f, 2.5f, 1.95f), armorMat);
            Part(PrimitiveType.Cube, "LeftPauldron", bodyPivot, new Vector3(-2.35f, 5.1f, 0f), new Vector3(1.45f, 1.25f, 2.15f), trimMat);
            Part(PrimitiveType.Cube, "RightPauldron", bodyPivot, new Vector3(2.35f, 5.1f, 0f), new Vector3(1.45f, 1.25f, 2.15f), trimMat);

            // Crowned guardian head + glowing visor
            Part(PrimitiveType.Cube, "GuardianHelm", bodyPivot, new Vector3(0f, 6.05f, 0.1f), new Vector3(1.45f, 1.35f, 1.45f), armorMat);
            Part(PrimitiveType.Cube, "VisorSlit", bodyPivot, new Vector3(0f, 6.12f, 0.78f), new Vector3(1.18f, 0.26f, 0.18f), coreMat);
            Part(PrimitiveType.Cube, "CrownCrestLeft", bodyPivot, new Vector3(-0.65f, 6.95f, 0.1f), new Vector3(0.24f, 0.95f, 0.45f), trimMat);
            Part(PrimitiveType.Cube, "CrownCrestRight", bodyPivot, new Vector3(0.65f, 6.95f, 0.1f), new Vector3(0.24f, 0.95f, 0.45f), trimMat);

            // Radiant weak-point core in chest (voxel cube!)
            coreSphere = Part(PrimitiveType.Cube, "RadiantCore", bodyPivot, new Vector3(0f, 4.25f, 0.85f), new Vector3(1.25f, 1.25f, 0.9f), coreMat);

            // Articulated twin arms
            leftArm = new GameObject("LeftArmPivot").transform;
            leftArm.SetParent(bodyPivot, false);
            leftArm.localPosition = new Vector3(-2.55f, 4.85f, 0f);
            Part(PrimitiveType.Cube, "LeftForearm", leftArm, new Vector3(0f, -1.65f, 0.35f), new Vector3(0.92f, 3.1f, 1.05f), armorMat);
            Part(PrimitiveType.Cube, "LeftGauntletBlade", leftArm, new Vector3(0f, -3.1f, 0.55f), new Vector3(1.15f, 0.85f, 1.45f), coreMat);

            rightArm = new GameObject("RightArmPivot").transform;
            rightArm.SetParent(bodyPivot, false);
            rightArm.localPosition = new Vector3(2.55f, 4.85f, 0f);
            Part(PrimitiveType.Cube, "RightForearm", rightArm, new Vector3(0f, -1.65f, 0.35f), new Vector3(0.92f, 3.1f, 1.05f), armorMat);
            Part(PrimitiveType.Cube, "RightGauntletBlade", rightArm, new Vector3(0f, -3.1f, 0.55f), new Vector3(1.15f, 0.85f, 1.45f), coreMat);

            haloRing = Part(PrimitiveType.Cube, "OrbitalCrownRing", bodyPivot, new Vector3(0f, 7.35f, 0f), new Vector3(3.4f, 0.10f, 3.4f), coreMat);
            shockwaveRing = Part(PrimitiveType.Cube, "TelegraphShockwave", transform, new Vector3(0f, 0.08f, 0f), new Vector3(1f, 0.06f, 1f), coreMat);
            shockwaveRing.gameObject.SetActive(false);

            var hitBox = gameObject.AddComponent<CapsuleCollider>();
            hitBox.center = new Vector3(0f, 3.6f, 0f);
            hitBox.radius = 2.4f;
            hitBox.height = 7.4f;

            var lgo = new GameObject("BossCoreLight");
            lgo.transform.SetParent(bodyPivot, false);
            lgo.transform.localPosition = new Vector3(0f, 4.3f, 1.4f);
            coreLight = lgo.AddComponent<Light>();
            coreLight.type = LightType.Point;
            coreLight.color = coreCol;
            coreLight.range = 24f;
            coreLight.intensity = 4.2f;
        }

        Material MakeMat(string name, Color col, Color emission, float metal, float smooth)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh) { name = name, color = col };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (emission.maxColorComponent > 0.001f)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
            }
            bossMaterials.Add(m);
            return m;
        }

        static Transform Part(PrimitiveType prim, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(prim);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (Application.isPlaying) Destroy(go.GetComponent<Collider>());
            else DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        public void TickBoss(float dt, Vector3 playerPos, Action<float, Vector3, string> onHitPlayer)
        {
            if (Defeated || !bodyPivot) return;

            Vector3 toPlayer = playerPos - transform.position;
            Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
            float dist = flat.magnitude;

            if (dist < 64f && dist > 0.25f)
            {
                Quaternion targetRot = Quaternion.LookRotation(flat.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * 3.5f);
                if (dist > 6.5f)
                {
                    float moveSpeed = BossDimension == Dimension.Mercury ? 3.4f : 2.5f;
                    Vector3 next = transform.position + flat.normalized * (moveSpeed * dt);
                    transform.position = next;
                }
            }

            float t = Time.time;
            bodyPivot.localPosition = new Vector3(0f, Mathf.Sin(t * 2.2f) * 0.14f, 0f);
            if (haloRing) haloRing.Rotate(0f, dt * 55f, 0f, Space.Self);

            if (hitFlashTimer > 0f)
            {
                hitFlashTimer -= dt;
                if (coreSphere)
                {
                    var cm = coreSphere.GetComponent<MeshRenderer>().sharedMaterial;
                    if (cm.HasProperty("_EmissionColor"))
                        cm.SetColor("_EmissionColor", hitFlashTimer > 0f ? Color.white * 5f : baseCoreEmission);
                }
            }

            if (dist > 34f)
            {
                telegraphTimer = 0f;
                if (shockwaveRing) shockwaveRing.gameObject.SetActive(false);
                if (leftArm) leftArm.localRotation = Quaternion.Euler(Mathf.Sin(t * 2.4f) * 18f, 0f, 0f);
                if (rightArm) rightArm.localRotation = Quaternion.Euler(-Mathf.Sin(t * 2.4f) * 18f, 0f, 0f);
                return;
            }

            if (telegraphTimer > 0f)
            {
                telegraphTimer -= dt;
                float p = 1f - Mathf.Clamp01(telegraphTimer / 1.15f);
                if (leftArm) leftArm.localRotation = Quaternion.Euler(Mathf.Lerp(-75f, 35f, p), 0f, 0f);
                if (rightArm) rightArm.localRotation = Quaternion.Euler(Mathf.Lerp(-75f, 35f, p), 0f, 0f);
                if (shockwaveRing)
                {
                    shockwaveRing.gameObject.SetActive(true);
                    float rad = Mathf.Lerp(2f, 16f, p);
                    shockwaveRing.localScale = new Vector3(rad, 0.06f, rad);
                }
                if (telegraphTimer <= 0f)
                {
                    if (shockwaveRing) shockwaveRing.gameObject.SetActive(false);
                    bool airborneDodge = playerPos.y - transform.position.y > 1.35f;
                    if (dist < 14.5f && !airborneDodge && onHitPlayer != null)
                    {
                        float dmg = BossDimension == Dimension.Mercury ? 18f : BossDimension == Dimension.Venus ? 15f : 12f;
                        onHitPlayer(dmg, flat.normalized, BossName + " обрушил ударную волну! (Подпрыгивайте в низкой гравитации!)");
                    }
                }
                return;
            }

            attackTimer -= dt;
            if (attackTimer <= 0f)
            {
                attackTimer = BossDimension == Dimension.Mercury ? 3.6f : 4.4f;
                telegraphTimer = 1.15f;
            }
        }

        static bool RayHitsBox(Ray ray, Vector3 min, Vector3 max, float maxDist)
        {
            float tmin = 0f, tmax = maxDist;
            for (int axis = 0; axis < 3; axis++)
            {
                float o = ray.origin[axis], d = ray.direction[axis];
                if (Mathf.Abs(d) < 1e-6f)
                {
                    if (o < min[axis] || o > max[axis]) return false;
                }
                else
                {
                    float inv = 1f / d;
                    float t1 = (min[axis] - o) * inv;
                    float t2 = (max[axis] - o) * inv;
                    if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                    if (t1 > tmin) tmin = t1;
                    if (t2 < tmax) tmax = t2;
                    if (tmin > tmax) return false;
                }
            }
            return tmax >= 0f && tmin <= maxDist;
        }

        public bool TryHitBoss(Ray aimRay, float maxReach, int weaponDamage, out bool defeatedNow, out ItemStack[] drops)
        {
            defeatedNow = false;
            drops = Array.Empty<ItemStack>();
            if (Defeated) return false;

            Vector3 center = transform.position + new Vector3(0f, 3.6f, 0f);
            Vector3 half = new Vector3(2.6f, 3.8f, 2.6f);
            bool hitPhysics = Physics.Raycast(aimRay, out var hit, maxReach) && (hit.transform == transform || hit.transform.IsChildOf(transform));
            if (!hitPhysics && !RayHitsBox(aimRay, center - half, center + half, maxReach))
                return false;

            int applied = Mathf.Max(8, weaponDamage);
            Health = Mathf.Max(0, Health - applied);
            hitFlashTimer = 0.16f;
            if (Health <= 0)
            {
                Defeated = true;
                defeatedNow = true;
                drops = new[]
                {
                    new ItemStack { item = 142, count = BossDimension == Dimension.Mercury ? 10 : 6 }, // Алмазы
                    new ItemStack { item = 11, count = 16 },                                           // Кристаллы
                    new ItemStack { item = 123, count = 12 },                                          // Слитки металла
                    new ItemStack { item = 111, count = 6 }                                            // Провизия
                };
                if (Application.isPlaying) Destroy(gameObject, 0.1f);
                else DestroyImmediate(gameObject);
            }
            return true;
        }

        void OnDestroy()
        {
            for (int i = 0; i < bossMaterials.Count; i++)
                if (bossMaterials[i])
                {
                    if (Application.isPlaying) Destroy(bossMaterials[i]);
                    else DestroyImmediate(bossMaterials[i]);
                }
            bossMaterials.Clear();
        }
    }
}
