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
        OrbitalStation = 5
    }

    /// <summary>
    /// Renders procedural 3D deep-space celestial bodies, starfields, rocket plumes,
    /// planetary skies, the walkable 3D Orbital Station interior with 3 Astronaut traders,
    /// and articulated 3D Planetary Guardian Bosses on Mars, Venus, and Mercury.
    /// </summary>
    public sealed class SpaceFlightVisuals : MonoBehaviour
    {
        readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();

        GameObject spaceDomeRoot;
        MeshRenderer domeRenderer;
        GameObject starfieldObj;
        GameObject sunObj;
        readonly Dictionary<SpaceTargetKind, GameObject> planetSpheres = new Dictionary<SpaceTargetKind, GameObject>();

        GameObject plumeRoot;
        Transform mainFlame, leftPlume, rightPlume, plasmaShield;
        Light engineLight;

        GameObject stationRoot;
        readonly List<Transform> stationAstronauts = new List<Transform>();

        public StationDocking ActiveStationDock { get; private set; }
        public Vector3 StationSpawnWorldPos { get; private set; }
        public Vector3 StationReturnHatchWorldPos { get; private set; }
        public bool IsStationSpawned => stationRoot != null;

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
            Shader sh = additive
                ? (Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"))
                : (Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit"));
            var m = new Material(sh) { name = label, color = Color.white };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            ownedObjects.Add(m);
            return m;
        }

        void BuildCelestialRig()
        {
            if (spaceDomeRoot) return;
            spaceDomeRoot = new GameObject("CelestialSpaceDome");
            spaceDomeRoot.transform.SetParent(transform, false);

            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            backdrop.name = "DeepSpaceBackdrop";
            backdrop.transform.SetParent(spaceDomeRoot.transform, false);
            backdrop.transform.localScale = new Vector3(-420f, 420f, 420f);
            DestroyImmediate(backdrop.GetComponent<Collider>());
            domeRenderer = backdrop.GetComponent<MeshRenderer>();
            domeRenderer.sharedMaterial = MakeLitMaterial("SpaceSkyBackdrop", new Color(0.01f, 0.014f, 0.028f), new Color(0.008f, 0.012f, 0.025f), 0f, 0f);
            domeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            domeRenderer.receiveShadows = false;

            starfieldObj = new GameObject("ProceduralStarfield");
            starfieldObj.transform.SetParent(spaceDomeRoot.transform, false);
            var sfFilter = starfieldObj.AddComponent<MeshFilter>();
            sfFilter.sharedMesh = BuildStarfieldMesh(1600, 195f, 2506);
            var sfRenderer = starfieldObj.AddComponent<MeshRenderer>();
            sfRenderer.sharedMaterial = MakeVertexColorMaterial("StarfieldMat");
            sfRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sfRenderer.receiveShadows = false;

            sunObj = CreateCelestialBody("SolarStar", 14f, SpaceTargetKind.Mercury, true);
            planetSpheres[SpaceTargetKind.Earth] = CreateCelestialBody("Planet_Lestingia", 9f, SpaceTargetKind.Earth, false);
            planetSpheres[SpaceTargetKind.Moon] = CreateCelestialBody("Planet_Moon", 4.5f, SpaceTargetKind.Moon, false);
            planetSpheres[SpaceTargetKind.Mars] = CreateCelestialBody("Planet_Mars", 6.2f, SpaceTargetKind.Mars, false);
            planetSpheres[SpaceTargetKind.Venus] = CreateCelestialBody("Planet_Venus", 8.2f, SpaceTargetKind.Venus, false);
            planetSpheres[SpaceTargetKind.Mercury] = CreateCelestialBody("Planet_Mercury", 4.2f, SpaceTargetKind.Mercury, false);

            spaceDomeRoot.SetActive(false);
        }

        GameObject CreateCelestialBody(string name, float radius, SpaceTargetKind paletteKind, bool isSun)
        {
            var go = new GameObject(name);
            go.transform.SetParent(spaceDomeRoot.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildPlanetSphereMesh(28, 18, radius, paletteKind, isSun);
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
                cols.Add(c); cols.Add(c); cols.Add(c); cols.Add(c);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
            var mesh = new Mesh { name = "ProceduralStars" };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            ownedObjects.Add(mesh);
            return mesh;
        }

        Mesh BuildPlanetSphereMesh(int lonSegs, int latSegs, float radius, SpaceTargetKind kind, bool isSun)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();

            for (int lat = 0; lat <= latSegs; lat++)
            {
                float v = (float)lat / latSegs;
                float phi = (v - 0.5f) * Mathf.PI;
                for (int lon = 0; lon <= lonSegs; lon++)
                {
                    float u = (float)lon / lonSegs;
                    float theta = u * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Cos(phi) * Mathf.Cos(theta), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(theta));
                    verts.Add(n * radius);
                    norms.Add(n);
                    cols.Add(SamplePlanetSurfaceColor(kind, isSun, n));
                }
            }
            int stride = lonSegs + 1;
            for (int lat = 0; lat < latSegs; lat++)
            {
                for (int lon = 0; lon < lonSegs; lon++)
                {
                    int a = lat * stride + lon;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
            var mesh = new Mesh { name = "PlanetSphere_" + kind };
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

                mainFlame = MakePlumeCone("MainEnginePlume", plumeRoot.transform, new Vector3(0.5f, -5.5f, 3.2f), new Vector3(2.6f, 9.5f, 2.6f), flameMat);
                leftPlume = MakePlumeCone("BoosterPlumePort", plumeRoot.transform, new Vector3(-2.1f, -4.2f, 3.2f), new Vector3(1.5f, 6.8f, 1.5f), flameMat);
                rightPlume = MakePlumeCone("BoosterPlumeStarboard", plumeRoot.transform, new Vector3(3.1f, -4.2f, 3.2f), new Vector3(1.5f, 6.8f, 1.5f), flameMat);
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
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            DestroyImmediate(go.GetComponent<Collider>());
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
                        kv.Value.transform.localPosition = new Vector3(18f, 52f, dist);
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

        public void ApplyPlanetAtmosphereAndGravity(Dimension dim, FirstPersonController player, Camera cam)
        {
            BuildCelestialRig();
            if (player) player.GravityScale = GravityForDimension(dim);
            if (plumeRoot) plumeRoot.SetActive(false);

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
            if (!collider) DestroyImmediate(go.GetComponent<Collider>());
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

            // Radiant weak-point core in chest
            coreSphere = Part(PrimitiveType.Sphere, "RadiantCore", bodyPivot, new Vector3(0f, 4.25f, 0.85f), new Vector3(1.25f, 1.25f, 0.9f), coreMat);

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

            haloRing = Part(PrimitiveType.Cylinder, "OrbitalCrownRing", bodyPivot, new Vector3(0f, 7.35f, 0f), new Vector3(3.4f, 0.06f, 3.4f), coreMat);
            shockwaveRing = Part(PrimitiveType.Cylinder, "TelegraphShockwave", transform, new Vector3(0f, 0.08f, 0f), new Vector3(1f, 0.05f, 1f), coreMat);
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
            DestroyImmediate(go.GetComponent<Collider>());
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
                Destroy(gameObject, 0.1f);
            }
            return true;
        }

        void OnDestroy()
        {
            for (int i = 0; i < bossMaterials.Count; i++)
                if (bossMaterials[i]) Destroy(bossMaterials[i]);
            bossMaterials.Clear();
        }
    }
}
