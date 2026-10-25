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
        SpaceProceduralDome proceduralDome;
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
        /// <summary>Nearest procedural system + world report for the cockpit, empty outside deep space.</summary>
        public string ProceduralSkyLine => proceduralDome != null ? proceduralDome.SkyLine : "";

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
                case Dimension.Radia: return 1.04f;
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

        Material starfieldMat;

        Material MakeStarfieldMaterial(string label, float exposure = 1.45f)
        {
            Shader sh = Shader.Find("LestingCraft/SpaceStarfield")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default");
            var m = new Material(sh) { name = label, color = Color.white };
            if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", exposure);
            if (m.HasProperty("_TwinkleTime")) m.SetFloat("_TwinkleTime", 0f);
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
            spaceKeyLight.intensity = 2.05f;
            spaceKeyLight.shadows = LightShadows.Soft;

            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backdrop.name = "DeepSpaceBackdrop";
            backdrop.transform.SetParent(spaceDomeRoot.transform, false);
            backdrop.transform.localScale = new Vector3(-720f, 720f, 720f);
            SafeDestroy(backdrop.GetComponent<Collider>());
            domeRenderer = backdrop.GetComponent<MeshRenderer>();
            domeRenderer.sharedMaterial = MakeLitMaterial("SpaceSkyBackdrop", new Color(0.004f, 0.006f, 0.014f), new Color(0.002f, 0.004f, 0.010f), 0f, 0f);
            domeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            domeRenderer.receiveShadows = false;
            domeRenderer.enabled = false;

            starfieldObj = new GameObject("ProceduralStarfield");
            starfieldObj.transform.SetParent(spaceDomeRoot.transform, false);
            var sfFilter = starfieldObj.AddComponent<MeshFilter>();
            sfFilter.sharedMesh = BuildStarfieldMesh(7200, 340f, 2506);
            var sfRenderer = starfieldObj.AddComponent<MeshRenderer>();
            starfieldMat = MakeStarfieldMaterial("StarfieldMat", 1.55f);
            sfRenderer.sharedMaterial = starfieldMat;
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

        static float Hash31(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ seed * 26544357);
                h ^= h >> 13;
                h *= 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffu) / 65535f;
            }
        }

        static float ValueNoise3D(Vector3 p, int seed)
        {
            int ix = Mathf.FloorToInt(p.x), iy = Mathf.FloorToInt(p.y), iz = Mathf.FloorToInt(p.z);
            float fx = p.x - ix, fy = p.y - iy, fz = p.z - iz;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);
            float c000 = Hash31(ix, iy, iz, seed), c100 = Hash31(ix + 1, iy, iz, seed);
            float c010 = Hash31(ix, iy + 1, iz, seed), c110 = Hash31(ix + 1, iy + 1, iz, seed);
            float c001 = Hash31(ix, iy, iz + 1, seed), c101 = Hash31(ix + 1, iy, iz + 1, seed);
            float c011 = Hash31(ix, iy + 1, iz + 1, seed), c111 = Hash31(ix + 1, iy + 1, iz + 1, seed);
            float x00 = Mathf.Lerp(c000, c100, fx), x10 = Mathf.Lerp(c010, c110, fx);
            float x01 = Mathf.Lerp(c001, c101, fx), x11 = Mathf.Lerp(c011, c111, fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        static float Fbm3D(Vector3 p, int seed, int octaves = 4)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * ValueNoise3D(p, seed + i * 97);
                norm += amp;
                p *= 2.07f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        void BuildAtlasCubePbrMaps(CelestialBodyDef body, Color primary, Color secondary, out Texture2D albedoTex, out Texture2D normalSpecTex)
        {
            const int tile = 96; // 16x higher texel resolution per cubic face than legacy 24x24!
            const int w = tile * 3, h = tile * 2;
            int seed = (int)body.Id * 13 + 7;
            var pixels = new Color32[w * h];
            var heights = new float[w * h];
            var specs = new float[w * h];

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
                        float polar = Mathf.Abs(n.y);

                        float cont = Fbm3D(n * 2.85f, seed, 5);
                        float detail = Fbm3D(n * 11.5f, seed + 401, 4);
                        float ridge = 1f - Mathf.Abs(Fbm3D(n * 6.4f, seed + 811, 3) * 2f - 1f);
                        float craterCell = ValueNoise3D(n * 14.0f, seed + 1201);
                        float craterRim = Mathf.Clamp01(1f - Mathf.Abs(craterCell - 0.72f) * 14f);
                        float craterBowl = craterCell > 0.72f ? -0.35f * Mathf.Clamp01((craterCell - 0.72f) * 5f) : 0f;

                        Color c;
                        float hgt = 0.5f;
                        float spec = 0.04f;

                        if (body.IsStar)
                        {
                            float plasma = Fbm3D(n * 8.0f, seed, 4);
                            float flare = Mathf.Pow(Mathf.Clamp01(ridge), 2.2f);
                            c = Color.Lerp(secondary, Color.Lerp(primary, Color.white, 0.72f), Mathf.Clamp01(plasma * 0.75f + flare * 0.45f));
                            hgt = plasma;
                            spec = 0.0f;
                        }
                        else if (body.Id == CelestialBodyId.Earth || body.Id == CelestialBodyId.Kepler452b)
                        {
                            bool iceCap = polar + (detail - 0.5f) * 0.14f > 0.81f;
                            if (iceCap)
                            {
                                c = Color.Lerp(new Color(0.86f, 0.93f, 0.99f), Color.white, detail);
                                hgt = 0.66f + 0.15f * detail;
                                spec = 0.55f;
                            }
                            else if (cont > 0.485f)
                            {
                                float elev = Mathf.Clamp01((cont - 0.485f) * 3.1f + ridge * 0.28f);
                                Color lush = body.Id == CelestialBodyId.Kepler452b ? new Color(0.11f, 0.52f, 0.42f) : new Color(0.14f, 0.46f, 0.19f);
                                Color arid = new Color(0.58f, 0.49f, 0.31f);
                                Color mount = new Color(0.50f, 0.48f, 0.46f);
                                c = Color.Lerp(lush, arid, Mathf.Clamp01((1f - polar) * detail * 1.15f));
                                if (elev > 0.62f) c = Color.Lerp(c, mount, (elev - 0.62f) * 2.2f);
                                if (elev > 0.84f) c = Color.Lerp(c, new Color(0.95f, 0.97f, 1.0f), (elev - 0.84f) * 5.0f);
                                hgt = 0.52f + elev * 0.48f;
                                spec = 0.05f;
                            }
                            else
                            {
                                float shelf = Mathf.Clamp01((cont - 0.34f) / 0.145f);
                                c = Color.Lerp(new Color(0.035f, 0.145f, 0.42f), new Color(0.11f, 0.46f, 0.78f), shelf * shelf);
                                hgt = 0.42f;
                                spec = 0.92f;
                            }
                        }
                        else if (body.HasRings || body.RadiusKm > 15000.0)
                        {
                            float turb = (detail - 0.5f) * 0.22f + Mathf.Sin(n.x * 5.5f + n.z * 5.1f) * 0.05f;
                            float bands = 0.5f + 0.5f * Mathf.Sin((n.y + turb) * 17.5f + seed * 0.4f);
                            float fine = 0.5f + 0.5f * Mathf.Sin((n.y + turb * 1.6f) * 42f);
                            c = Color.Lerp(secondary, Color.Lerp(primary, Color.white, 0.22f * fine), bands);
                            if (body.Id == CelestialBodyId.Jupiter)
                            {
                                float spot = Vector2.Distance(new Vector2(s * 0.8f, (t + 0.22f) * 1.35f), new Vector2(0.14f, 0f));
                                if (face == 4 && spot < 0.26f)
                                    c = Color.Lerp(new Color(0.86f, 0.31f, 0.17f), new Color(0.96f, 0.62f, 0.42f), spot / 0.26f);
                            }
                            hgt = 0.45f + 0.28f * bands + 0.15f * fine;
                            spec = 0.22f;
                        }
                        else
                        {
                            // High-definition rocky / icy worlds (Moon maria & highland craters, Mars canyons, Europa cracks)
                            float maria = Mathf.SmoothStep(0.36f, 0.66f, cont);
                            c = Color.Lerp(secondary * 0.86f, primary * 1.06f, maria);
                            c *= 0.88f + 0.24f * detail;
                            c = Color.Lerp(c, c * 1.24f, craterRim * 0.65f);
                            if (body.Id == CelestialBodyId.Europa)
                            {
                                float lineae = Mathf.Pow(Mathf.Clamp01(ridge), 6f);
                                c = Color.Lerp(c, new Color(0.56f, 0.34f, 0.24f), lineae * 0.65f);
                                spec = 0.72f;
                            }
                            else if (polar > 0.84f && body.Id == CelestialBodyId.Mars)
                            {
                                c = Color.Lerp(c, new Color(0.95f, 0.94f, 0.92f), (polar - 0.84f) * 5.5f);
                                spec = 0.45f;
                            }
                            hgt = Mathf.Clamp01(0.45f + 0.30f * maria + 0.18f * detail + 0.26f * craterRim + craterBowl);
                        }

                        c.a = 1f;
                        int idx = (oy + py) * w + (ox + px);
                        pixels[idx] = c;
                        heights[idx] = hgt;
                        specs[idx] = spec;
                    }
                }
            }

            var normals = new Color32[w * h];
            for (int face = 0; face < 6; face++)
            {
                int ox = (face % 3) * tile;
                int oy = (face / 3) * tile;
                for (int py = 0; py < tile; py++)
                {
                    int py0 = Mathf.Max(0, py - 1), py1 = Mathf.Min(tile - 1, py + 1);
                    for (int px = 0; px < tile; px++)
                    {
                        int px0 = Mathf.Max(0, px - 1), px1 = Mathf.Min(tile - 1, px + 1);
                        float hL = heights[(oy + py) * w + (ox + px0)];
                        float hR = heights[(oy + py) * w + (ox + px1)];
                        float hD = heights[(oy + py0) * w + (ox + px)];
                        float hU = heights[(oy + py1) * w + (ox + px)];
                        Vector3 nrm = new Vector3((hL - hR) * 2.6f, (hD - hU) * 2.6f, 1f).normalized;
                        int idx = (oy + py) * w + (ox + px);
                        normals[idx] = new Color32(
                            (byte)Mathf.Clamp(Mathf.RoundToInt((nrm.x * 0.5f + 0.5f) * 255f), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt((nrm.y * 0.5f + 0.5f) * 255f), 0, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt((nrm.z * 0.5f + 0.5f) * 255f), 128, 255),
                            (byte)Mathf.Clamp(Mathf.RoundToInt(specs[idx] * 255f), 0, 255));
                    }
                }
            }

            albedoTex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8
            };
            albedoTex.SetPixels32(pixels);
            albedoTex.Apply(true, false);
            ownedObjects.Add(albedoTex);

            normalSpecTex = new Texture2D(w, h, TextureFormat.RGBA32, true, true)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8
            };
            normalSpecTex.SetPixels32(normals);
            normalSpecTex.Apply(true, false);
            ownedObjects.Add(normalSpecTex);
        }

        Texture2D BuildCloudCubeTexture(int seed, Color cloudTint)
        {
            const int tile = 80;
            const int w = tile * 3, h = tile * 2;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4
            };
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
                        Vector3 swirl = n + new Vector3(Mathf.Sin(n.y * 7f), 0f, Mathf.Cos(n.y * 7f)) * 0.22f;
                        float cld = Fbm3D(swirl * 4.4f, seed, 4);
                        bool filled = cld > 0.53f;
                        float shade = Mathf.Clamp01(0.80f + (cld - 0.53f) * 1.1f);
                        pixels[(oy + py) * w + (ox + px)] = filled
                            ? (Color32)new Color(cloudTint.r * shade, cloudTint.g * shade, cloudTint.b * shade, 1f)
                            : new Color32(0, 0, 0, 0);
                    }
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            ownedObjects.Add(tex);
            return tex;
        }

        Texture2D BuildSquarePlanetaryRingTexture(Color cA, Color cB)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float nz = ((y + 0.5f) / size) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    float sqDist = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(nz));
                    bool insideHole = sqDist < 0.515f || sqDist > 0.992f;
                    bool cassiniGap = sqDist > 0.752f && sqDist < 0.808f;
                    bool enckeGap = sqDist > 0.916f && sqDist < 0.936f;
                    float fineRing = 0.5f + 0.5f * Mathf.Sin(sqDist * 210f) * Mathf.Cos(sqDist * 73f);
                    float density = (insideHole || cassiniGap || enckeGap) ? 0f : (0.55f + 0.45f * fineRing);
                    Color c = Color.Lerp(cB * 0.82f, Color.Lerp(cA, Color.white, 0.55f), fineRing);
                    c.a = density > 0.34f ? 1f : 0f;
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
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
            mesh.RecalculateTangents();
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

        Material MakePlanetVoxelMaterial(string label, Texture2D tex, Color tint, float emissionGain, float cutoff = 0.42f, Texture2D bumpSpec = null, Color atmo = default, float cityGlow = 0f)
        {
            var sh = Shader.Find("LestingCraft/SpacePlanetVoxel");
            if (sh != null)
            {
                var m = new Material(sh) { name = label, color = tint };
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (bumpSpec != null && m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", bumpSpec);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                if (m.HasProperty("_AtmoColor")) m.SetColor("_AtmoColor", atmo);
                if (m.HasProperty("_CityGlow")) m.SetFloat("_CityGlow", cityGlow);
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
            BuildAtlasCubePbrMaps(body, cA, cB, out var tex, out var bumpSpec);
            Color atmo = body.Id == CelestialBodyId.Earth ? new Color(0.28f, 0.62f, 1.0f, 0.72f)
                       : body.Id == CelestialBodyId.Mars ? new Color(0.95f, 0.54f, 0.34f, 0.35f)
                       : body.Id == CelestialBodyId.Venus ? new Color(1.0f, 0.86f, 0.56f, 0.55f)
                       : body.Id == CelestialBodyId.Titan ? new Color(0.32f, 0.82f, 0.88f, 0.62f)
                       : body.Id == CelestialBodyId.Kepler452b ? new Color(0.24f, 0.88f, 0.72f, 0.65f)
                       : body.Id == CelestialBodyId.AndromedaPrime ? new Color(0.76f, 0.48f, 1.0f, 0.68f)
                       : body.IsStar ? new Color(1.0f, 0.78f, 0.35f, 0.95f)
                       : new Color(0f, 0f, 0f, 0f);
            float cityGlow = body.Id == CelestialBodyId.Earth ? 1.0f : body.Id == CelestialBodyId.Kepler452b ? 0.55f : 0f;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildVoxelCubeMesh(halfExtent, "AtlasVoxelCube_" + (int)body.Id);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MakePlanetVoxelMaterial("AtlasSurface_" + body.WireKey, tex, Color.white, body.IsStar ? 1.05f : 0.06f, 0.05f, bumpSpec, atmo, cityGlow);
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
            var mesh = new Mesh { name = "SpiralGalaxyM31", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            ownedObjects.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MakeStarfieldMaterial("SpiralGalaxyMat", 1.15f);
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
            Quaternion galacticPlane = Quaternion.Euler(62.87f, 28.0f, -19.0f);
            for (int i = 0; i < count; i++)
            {
                bool milkyWayBand = i % 3 == 0;
                Vector3 dir;
                if (milkyWayBand)
                {
                    float galLon = (float)(rng.NextDouble() * Math.PI * 2.0);
                    float galLat = (float)((rng.NextDouble() + rng.NextDouble() - 1.0) * 0.24);
                    Vector3 gDir = new Vector3(
                        Mathf.Cos(galLat) * Mathf.Cos(galLon),
                        Mathf.Sin(galLat),
                        Mathf.Cos(galLat) * Mathf.Sin(galLon));
                    dir = (galacticPlane * gDir).normalized;
                }
                else
                {
                    double u = rng.NextDouble() * 2.0 - 1.0;
                    double theta = rng.NextDouble() * Math.PI * 2.0;
                    float rxy = Mathf.Sqrt(Mathf.Max(0f, 1f - (float)(u * u)));
                    dir = new Vector3(rxy * Mathf.Cos((float)theta), (float)u, rxy * Mathf.Sin((float)theta));
                }

                Vector3 center = dir * radius;
                Vector3 right = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 up = Vector3.Cross(right, dir).normalized;
                float roll = (float)rng.NextDouble();
                float size = roll > 0.985f ? Mathf.Lerp(1.15f, 2.05f, (float)rng.NextDouble())
                           : Mathf.Lerp(0.28f, 0.88f, (float)(rng.NextDouble() * rng.NextDouble()));
                float temp = (float)rng.NextDouble();
                Color c = temp < 0.18f ? new Color(0.68f, 0.82f, 1.0f, 1f)     // O / B blue-white
                        : temp < 0.62f ? new Color(0.98f, 0.98f, 0.96f, 1f)    // A / F pure white
                        : temp < 0.84f ? new Color(1.0f, 0.90f, 0.68f, 1f)     // G solar yellow-white
                        : temp < 0.95f ? new Color(1.0f, 0.72f, 0.46f, 1f)     // K orange star
                                       : new Color(1.0f, 0.50f, 0.36f, 1f);    // M red giant
                float mag = milkyWayBand
                    ? Mathf.Lerp(0.32f, 0.92f, (float)rng.NextDouble())
                    : Mathf.Lerp(0.42f, 1.0f, (float)rng.NextDouble());
                c.r *= mag; c.g *= mag; c.b *= mag;
                c.a = Mathf.Clamp01(mag);
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
            var mesh = new Mesh { name = "ProceduralStars", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
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

            // Endless procedural universe: nearby systems, their worlds, black holes, nebulae and galaxies.
            if (!proceduralDome)
            {
                var domeObject = new GameObject("ProceduralUniverseDome");
                domeObject.transform.SetParent(transform, false);
                proceduralDome = domeObject.AddComponent<SpaceProceduralDome>();
            }
            if (proceduralDome)
            {
                proceduralDome.Bind(spaceDomeRoot ? spaceDomeRoot.transform : transform);
                proceduralDome.Sync(helm, viewer);
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

        static float BaseMeshHalfExtent(CelestialBodyDef body) =>
            body.IsStar ? 9.6f : body.HasRings ? 7.8f : Mathf.Clamp((float)Math.Log10(Math.Max(200.0, body.RadiusKm)) * 1.55f, 2.2f, 8.2f);

        static void ApplyPlanetShaderLighting(GameObject root, Vector3 sunDirWorld, float solarFlux, float nightAmbient, float distantStarGlow)
        {
            if (!root) return;
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var mat = renderers[i] ? renderers[i].sharedMaterial : null;
                if (!mat) continue;
                if (mat.HasProperty("_SunDirWS")) mat.SetVector("_SunDirWS", new Vector4(sunDirWorld.x, sunDirWorld.y, sunDirWorld.z, 0f));
                if (mat.HasProperty("_SolarFlux")) mat.SetFloat("_SolarFlux", solarFlux);
                if (mat.HasProperty("_NightAmbient")) mat.SetFloat("_NightAmbient", nightAmbient);
                if (mat.HasProperty("_EmissionGain")) mat.SetFloat("_EmissionGain", distantStarGlow);
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
                if (proceduralDome) proceduralDome.HideAll();
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
                // True airless space darkness on shadowed shuttle hulls & night hemispheres!
                RenderSettings.ambientLight = new Color(0.011f, 0.014f, 0.022f);
                if (domeRenderer) domeRenderer.enabled = false;
                if (Camera.main && Camera.main.farClipPlane < 1200f) Camera.main.farClipPlane = 1200f;

                Quaternion invShipRot = Quaternion.Inverse(helm.Rotation);
                var sysDef = CelestialAtlas.GetSystem(helm.CurrentSystem);
                SpaceVector toStarKm = new SpaceVector(0, 0, 0) - helm.PositionKm;
                double starDistKm = Math.Max(700000.0, toStarKm.Magnitude);
                double starAu = starDistKm / CelestialAtlas.AstronomicalUnitKm;
                float solarFluxAtShip = Mathf.Clamp(1.0f / Mathf.Pow((float)Math.Max(0.22, starAu), 0.62f), 0.14f, 2.65f);

                Vector3 sunDirWorld = (invShipRot * new Vector3(
                    (float)toStarKm.Normalized.X, (float)toStarKm.Normalized.Y, (float)toStarKm.Normalized.Z)).normalized;
                if (sunDirWorld.sqrMagnitude < 0.001f) sunDirWorld = new Vector3(0.45f, 0.72f, -0.53f).normalized;

                if (spaceKeyLight)
                {
                    spaceKeyLight.enabled = true;
                    spaceKeyLight.color = Color.Lerp(sysDef.PrimaryStarColor, Color.white, 0.45f);
                    spaceKeyLight.intensity = 2.15f * solarFluxAtShip;
                    spaceKeyLight.transform.rotation = Quaternion.LookRotation(-sunDirWorld, Vector3.up);
                }

                spaceDomeRoot.SetActive(true);
                if (viewer) spaceDomeRoot.transform.position = viewer.position;
                bool jumping = helm.InHyperjump;
                Color deepSky = helm.CurrentGalaxy == GalaxyId.Andromeda
                    ? new Color(0.004f, 0.003f, 0.012f)
                    : new Color(0.002f, 0.003f, 0.008f);
                if (jumping)
                    deepSky = Color.Lerp(deepSky, new Color(0.14f, 0.36f, 0.78f), Mathf.Sin((float)helm.HyperjumpProgress * Mathf.PI));
                if (Camera.main)
                {
                    Camera.main.clearFlags = CameraClearFlags.SolidColor;
                    Camera.main.backgroundColor = deepSky;
                }
                if (starfieldObj)
                {
                    starfieldObj.SetActive(true);
                    starfieldObj.transform.localRotation = invShipRot;
                    if (starfieldMat)
                    {
                        if (starfieldMat.HasProperty("_TwinkleTime")) starfieldMat.SetFloat("_TwinkleTime", Time.time);
                        if (starfieldMat.HasProperty("_Exposure")) starfieldMat.SetFloat("_Exposure", Mathf.Lerp(1.85f, 1.20f, Mathf.Clamp01(solarFluxAtShip * 0.45f)));
                    }
                }
                if (sunObj) sunObj.SetActive(false);
                foreach (var kv in planetSpheres) if (kv.Value) kv.Value.SetActive(false);
                if (spiralGalaxyObj)
                {
                    spiralGalaxyObj.SetActive(true);
                    Vector3 galDir = invShipRot * new Vector3(-0.34f, 0.24f, 0.91f).normalized;
                    spiralGalaxyObj.transform.localPosition = galDir * 305f;
                    spiralGalaxyObj.transform.localRotation = invShipRot * Quaternion.Euler(58f, -18f, 24f);
                }
                if (asteroidBeltObj)
                {
                    // Asteroid belt only visible when ship is genuinely near ~2.2..3.3 AU (320..500 Mkm from Sun)!
                    bool nearBelt = helm.CurrentSystem == StarSystemId.SolarSystem && starDistKm > 3.1e8 && starDistKm < 5.1e8;
                    asteroidBeltObj.SetActive(nearBelt);
                    if (nearBelt) asteroidBeltObj.transform.localRotation = invShipRot;
                }

                // True 1:1 perspective angular projection of every star, planet, moon & station (ZERO HUD clutter!)
                Vector3 starWorldRendered = spaceDomeRoot.transform.position + sunDirWorld * 300f;
                foreach (var body in CelestialAtlas.Bodies)
                {
                    if (!atlasBodyObjects.TryGetValue(body.Id, out var obj) || !obj) continue;
                    bool inSys = body.System == helm.CurrentSystem;
                    obj.SetActive(inSys);
                    if (!inSys) continue;

                    SpaceVector bodyWorldKm = CelestialAtlas.WorldPositionKm(body.Id, helm.OrbitTimeDays);
                    SpaceVector deltaKm = bodyWorldKm - helm.PositionKm;
                    double distKm = Math.Max(body.RadiusKm * 1.04, deltaKm.Magnitude);

                    // Monotonic depth ordering from 20 m (orbiting right overhead) to 295 m (billions of km away)
                    float renderDist = Mathf.Lerp(20f, 295f, Mathf.InverseLerp(2.0f, 10.2f, (float)Math.Log10(distKm + 1.0)));
                    SpaceVector dir = deltaKm.Normalized;
                    Vector3 rel = invShipRot * new Vector3((float)dir.X, (float)dir.Y, (float)dir.Z) * renderDist;
                    obj.transform.localPosition = rel;

                    // Exact 1:1 astronomical angular radius: tan(asin(R / d))
                    double sinAng = Math.Min(0.94, Math.Max(1e-7, body.RadiusKm / distKm));
                    float trueHalfWorld = renderDist * Mathf.Tan((float)Math.Asin(sinAng));
                    // Distant planets (> millions of km away) gleam as bright natural naked-eye planetary stars without fake HUD boxes
                    float minStarPointHalf = renderDist * (body.IsStar ? 0.0042f : 0.0019f);
                    float finalHalfWorld = Mathf.Max(minStarPointHalf, trueHalfWorld);
                    float baseHalf = BaseMeshHalfExtent(body);
                    obj.transform.localScale = Vector3.one * (finalHalfWorld / Mathf.Max(0.1f, baseHalf));

                    // Compute true solar illumination vector at the planet's own heliocentric position!
                    double bodyAu = Math.Max(0.20, bodyWorldKm.Magnitude / CelestialAtlas.AstronomicalUnitKm);
                    float bodyFlux = body.IsStar ? 1.0f : Mathf.Clamp(1.05f / Mathf.Pow((float)bodyAu, 0.55f), 0.22f, 2.25f);
                    Vector3 bodyToSunWS;
                    if (body.IsStar)
                        bodyToSunWS = Vector3.up;
                    else
                    {
                        SpaceVector toSunFromBody = new SpaceVector(0, 0, 0) - bodyWorldKm;
                        Vector3 sDir = invShipRot * new Vector3(
                            (float)toSunFromBody.Normalized.X, (float)toSunFromBody.Normalized.Y, (float)toSunFromBody.Normalized.Z);
                        bodyToSunWS = sDir.sqrMagnitude > 0.001f ? sDir.normalized : (starWorldRendered - obj.transform.position).normalized;
                    }

                    // Self-emission: stars blaze brightly; unresolved distant planets shine as bright point stars; close resolved planets have dark night sides!
                    float unresolvedBlend = 1f - Mathf.Clamp01((trueHalfWorld - minStarPointHalf) / Mathf.Max(0.001f, minStarPointHalf * 2.5f));
                    float selfGlow = body.IsStar ? 1.35f : Mathf.Lerp(0.0f, 1.05f, unresolvedBlend);
                    float nightAmb = body.IsStar ? 0.85f : 0.015f;
                    ApplyPlanetShaderLighting(obj, bodyToSunWS, bodyFlux, nightAmb, selfGlow);
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

        public void ApplyPlanetAtmosphereAndGravity(Dimension dim, FirstPersonController player, Camera cam, bool stationInterior = false)
        {
            BuildCelestialRig();
            bool showStationSky = stationInterior && IsStationSpawned && dim == Dimension.Lestingia;
            if (player) player.GravityScale = GravityForDimension(dim);
            if (plumeRoot) plumeRoot.SetActive(false);
            foreach (var kv in atlasBodyObjects) if (kv.Value) kv.Value.SetActive(false);
            if (spiralGalaxyObj) spiralGalaxyObj.SetActive(dim == Dimension.AndromedaPrime || dim == Dimension.Somnia);
            if (asteroidBeltObj) asteroidBeltObj.SetActive(false);

            if (dim == Dimension.Dream || dim == Dimension.Radia)
            {
                if (spaceDomeRoot) spaceDomeRoot.SetActive(false);
                Color heavyFog = dim == Dimension.Radia
                    ? new Color(0.55f, 0.58f, 0.60f)   // Exact overcast monochrome storm fog matching image-1.png!
                    : new Color(0.17f, 0.15f, 0.23f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = heavyFog;
                RenderSettings.fogDensity = dim == Dimension.Radia ? 0.0155f : 0.038f;
                if (cam) cam.backgroundColor = heavyFog;
                return;
            }

            if (dreamWatcherRoot) dreamWatcherRoot.SetActive(false);

            if (dim == Dimension.Lestingia && !showStationSky)
            {
                // Ground view: never leave the space-flight starfield or full-size planets hanging in the sky.
                // Keep one correctly sub-degree cubic Moon only when it is above the local horizon.
                if (spaceDomeRoot)
                {
                    spaceDomeRoot.SetActive(true);
                    if (cam) spaceDomeRoot.transform.position = cam.transform.position;
                }
                if (spaceKeyLight) spaceKeyLight.enabled = false;
                if (domeRenderer) domeRenderer.enabled = false;
                if (starfieldObj) starfieldObj.SetActive(false);
                if (sunObj) sunObj.SetActive(false);
                foreach (var kv in planetSpheres) if (kv.Value) kv.Value.SetActive(false);
                foreach (var kv in atlasBodyObjects) if (kv.Value) kv.Value.SetActive(false);
                if (spiralGalaxyObj) spiralGalaxyObj.SetActive(false);
                if (asteroidBeltObj) asteroidBeltObj.SetActive(false);
                RenderSettings.fog = false;
                if (cam) cam.backgroundColor = new Color(0.42f, 0.66f, 0.78f);

                GameObject moon;
                if (cam && atlasBodyObjects.TryGetValue(CelestialBodyId.Moon, out moon) && moon)
                {
                    Vector3 sunDirection = RenderSettings.sun
                        ? -RenderSettings.sun.transform.forward
                        : Vector3.down;
                    Vector3 moonDirection = (-sunDirection).normalized;
                    bool moonAboveHorizon = moonDirection.y > 0.035f;
                    moon.SetActive(moonAboveHorizon);
                    if (moonAboveHorizon)
                    {
                        // Camera.farClipPlane is 256 m in the surface world. A 2.0 m voxel Moon at 220 m subtends ~0.52°.
                        moon.transform.localPosition = moonDirection * 220f;
                        moon.transform.localScale = Vector3.one * 0.20f;
                        ApplyPlanetShaderLighting(moon, sunDirection, 0.16f, 0.025f, 0f);
                    }
                }
                return;
            }

            if (spaceDomeRoot)
            {
                spaceDomeRoot.SetActive(true);
                if (player) spaceDomeRoot.transform.position = player.transform.position;
            }
            if (spaceKeyLight) spaceKeyLight.enabled = false;
            if (domeRenderer) domeRenderer.enabled = false;
            foreach (var kv in planetSpheres) if (kv.Value) kv.Value.SetActive(false);

            bool showDaytimeStars = showStationSky || dim == Dimension.Moon || dim == Dimension.Mercury ||
                                    dim == Dimension.Europa || dim == Dimension.Pluto || dim == Dimension.AndromedaPrime || dim == Dimension.Somnia;
            float sunScale = 1.0f;
            CelestialBodyId? heroSkyParent = null;
            float heroSkyScale = 1.0f;

            if (showStationSky)
            {
                heroSkyParent = CelestialBodyId.Earth;
                heroSkyScale = 3.2f;
            }
            else if (dim == Dimension.Moon)
            {
                heroSkyParent = CelestialBodyId.Earth;
                heroSkyScale = 1.15f;
            }
            else if (dim == Dimension.Mars)
            {
                sunScale = 0.66f;
            }
            else if (dim == Dimension.Venus)
            {
                sunScale = 0.45f;
            }
            else if (dim == Dimension.Mercury)
            {
                sunScale = 2.60f;
            }
            else if (dim == Dimension.Europa)
            {
                // Jupiter looms enormous over Europa's glacial ridges!
                sunScale = 0.24f;
                heroSkyParent = CelestialBodyId.Jupiter;
                heroSkyScale = 2.35f;
            }
            else if (dim == Dimension.Titan)
            {
                // Ringed cubic Saturn hangs majestically in Titan's petrol-cyan & honey-gold sky!
                sunScale = 0.16f;
                heroSkyParent = CelestialBodyId.Saturn;
                heroSkyScale = 2.45f;
            }
            else if (dim == Dimension.Pluto)
            {
                sunScale = 0.08f;
                heroSkyParent = CelestialBodyId.Uranus;
                heroSkyScale = 0.85f;
            }
            else if (dim == Dimension.ProximaB)
            {
                sunScale = 1.95f;
                heroSkyParent = CelestialBodyId.CentauriPrime;
                heroSkyScale = 1.15f;
            }
            else if (dim == Dimension.Trappist1e)
            {
                sunScale = 1.55f;
                heroSkyParent = CelestialBodyId.Trappist1d;
                heroSkyScale = 1.65f;
            }
            else if (dim == Dimension.Kepler452b)
            {
                sunScale = 1.08f;
            }
            else if (dim == Dimension.AndromedaPrime || dim == Dimension.Somnia)
            {
                sunScale = dim == Dimension.Somnia ? 0.55f : 1.35f;
                if (spiralGalaxyObj)
                {
                    spiralGalaxyObj.SetActive(true);
                    spiralGalaxyObj.transform.localPosition = new Vector3(-48f, 96f, 210f);
                    spiralGalaxyObj.transform.localRotation = Quaternion.Euler(48f, -22f, 20f);
                }
            }

            if (starfieldObj)
            {
                starfieldObj.SetActive(showDaytimeStars);
                starfieldObj.transform.localRotation = Quaternion.identity;
                if (starfieldMat && starfieldMat.HasProperty("_Exposure"))
                    starfieldMat.SetFloat("_Exposure", dim == Dimension.AndromedaPrime || dim == Dimension.Somnia ? 0.95f : 1.50f);
            }
            if (sunObj)
            {
                sunObj.SetActive(showDaytimeStars);
                sunObj.transform.localScale = Vector3.one * sunScale;
                sunObj.transform.localPosition = new Vector3(-115f, 108f, 145f);
            }
            if (heroSkyParent.HasValue && atlasBodyObjects.TryGetValue(heroSkyParent.Value, out var skyPlanet) && skyPlanet)
            {
                skyPlanet.SetActive(true);
                skyPlanet.transform.localPosition = showStationSky ? new Vector3(0f, -68f, 44f) : new Vector3(44f, 88f, 155f);
                skyPlanet.transform.localRotation = Quaternion.Euler(22f, 34f, -12f);
                skyPlanet.transform.localScale = Vector3.one * heroSkyScale;
                ApplyPlanetShaderLighting(skyPlanet, new Vector3(-0.62f, 0.54f, 0.56f).normalized, 1.15f, 0.025f, 0.04f);
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
        public string BossTitle { get; private set; }
        int lootSeed;

        public static PlanetaryBossController SpawnForPlanet(
            Transform parent,
            Dimension dim,
            VoxelWorld world,
            int seed,
            bool alreadyDefeated,
            Vector3 nearAnchor)
        {
            if (alreadyDefeated || !LestingCraft.Gameplay.BossCatalog.HasBoss(dim))
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
            boss.lootSeed = seed;
            boss.BuildSculpt(dim);
            return boss;
        }

        void BuildSculpt(Dimension dim)
        {
            // Every world has its own named guardian; the catalogue stays pure data so it is testable.
            var profile = LestingCraft.Gameplay.BossCatalog.For(dim);
            BossName = profile.Name;
            MaxHealth = profile.MaxHealth;
            var armorCol = new Color(profile.ArmorR, profile.ArmorG, profile.ArmorB);
            var trimCol = new Color(profile.TrimR, profile.TrimG, profile.TrimB);
            var coreCol = new Color(profile.CoreR, profile.CoreG, profile.CoreB);
            transform.localScale = Vector3.one * Mathf.Max(0.6f, profile.Scale);
            BossTitle = profile.Title;
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

            ShapeForArchetype(LestingCraft.Gameplay.BossCatalog.For(dim).Archetype);

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

        /// <summary>Small silhouette differences so guardians of different worlds do not look cloned.</summary>
        void ShapeForArchetype(LestingCraft.Gameplay.BossArchetype archetype)
        {
            if (!bodyPivot) return;
            var leftLeg = bodyPivot.Find("LeftLeg");
            var rightLeg = bodyPivot.Find("RightLeg");
            var chest = bodyPivot.Find("ArmoredChest");
            var core = bodyPivot.Find("RadiantCore");
            var helm = bodyPivot.Find("GuardianHelm");
            if (leftLeg) leftLeg.localScale = new Vector3(0.95f, 1f, 1.05f);
            if (rightLeg) rightLeg.localScale = new Vector3(0.95f, 1f, 1.05f);
            switch (archetype)
            {
                case LestingCraft.Gameplay.BossArchetype.Archon:
                    // Floating caster: stubby legs, longer torso, wider crown.
                    if (leftLeg) { leftLeg.localScale = new Vector3(1f, 0.42f, 1f); leftLeg.localPosition = new Vector3(-1.05f, 2.05f, 0f); }
                    if (rightLeg) { rightLeg.localScale = new Vector3(1f, 0.42f, 1f); rightLeg.localPosition = new Vector3(1.05f, 2.05f, 0f); }
                    if (chest) { chest.localScale = new Vector3(3.2f, 3.0f, 1.95f); chest.localPosition = new Vector3(0f, 4.55f, 0f); }
                    if (core) { core.localScale = new Vector3(1.6f, 1.6f, 1.05f); core.localPosition = new Vector3(0f, 4.7f, 0.95f); }
                    if (haloRing) haloRing.localScale = new Vector3(4.6f, 0.10f, 4.6f);
                    break;
                case LestingCraft.Gameplay.BossArchetype.Warden:
                    // Forest guardian: heavy shoulders, longer arms, lower head.
                    if (chest) { chest.localScale = new Vector3(3.9f, 2.3f, 2.15f); chest.localPosition = new Vector3(0f, 4.05f, 0f); }
                    if (helm) helm.localPosition = new Vector3(0f, 5.85f, 0.16f);
                    if (leftArm) { leftArm.localPosition = new Vector3(-2.75f, 4.75f, 0f); leftArm.localScale = new Vector3(1f, 1.18f, 1f); }
                    if (rightArm) { rightArm.localPosition = new Vector3(2.75f, 4.75f, 0f); rightArm.localScale = new Vector3(1f, 1.18f, 1f); }
                    break;
                case LestingCraft.Gameplay.BossArchetype.Titan:
                    // Frost titan: wide stance, taller body.
                    if (leftLeg) { leftLeg.localScale = new Vector3(1.35f, 1.22f, 1.3f); leftLeg.localPosition = new Vector3(-1.25f, 1.35f, 0f); }
                    if (rightLeg) { rightLeg.localScale = new Vector3(1.35f, 1.22f, 1.3f); rightLeg.localPosition = new Vector3(1.25f, 1.35f, 0f); }
                    if (chest) { chest.localScale = new Vector3(4.05f, 2.4f, 2.1f); chest.localPosition = new Vector3(0f, 4.5f, 0f); }
                    if (helm) helm.localPosition = new Vector3(0f, 6.35f, 0.12f);
                    break;
                case LestingCraft.Gameplay.BossArchetype.Ember:
                    // Burning core: compact chassis, oversized weak point.
                    if (chest) chest.localScale = new Vector3(2.9f, 2.1f, 1.8f);
                    if (core) { core.localScale = new Vector3(1.9f, 1.9f, 1.1f); core.localPosition = new Vector3(0f, 4.35f, 1.0f); }
                    if (leftArm) leftArm.localScale = new Vector3(0.85f, 0.9f, 0.85f);
                    if (rightArm) rightArm.localScale = new Vector3(0.85f, 0.9f, 0.85f);
                    break;
            }
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
                LestingCraft.Gameplay.BossCatalog.LootFor(BossDimension, lootSeed, out int diamonds, out int crystals, out int shards);
                drops = new[]
                {
                    new ItemStack { item = 142, count = diamonds }, // Алмазы
                    new ItemStack { item = 11, count = crystals },  // Кристаллы
                    new ItemStack { item = 123, count = shards },   // Слитки металла
                    new ItemStack { item = 111, count = 6 }         // Провизия
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
