using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.World;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Live rotating 3D voxel diorama behind the Main Menu (Minecraft-style panoramic camera).
    /// Uses real ChunkMesher geometry, VoxelPBR/Water/Grass/Crystal shaders, WorldAtmosphere sky & sun,
    /// animated windmill sails, villagers, wildlife, and warm campfires. Torn down cleanly before gameplay.
    /// </summary>
    public sealed class MenuPanoramaScene : MonoBehaviour
    {
        static MenuPanoramaScene activeInstance;
        public static bool IsActive => activeInstance != null && activeInstance.isActiveAndEnabled && activeInstance.ready;

        struct StationSpot { public int Id; public Vector3 Pos; public StationSpot(int id, Vector3 pos) { Id = id; Pos = pos; } }
        struct TreeSpot { public int X, Z, Height; public bool Sakura; public TreeSpot(int x, int z, bool sakura, int h) { X = x; Z = z; Sakura = sakura; Height = h; } }
        struct CrystalSpot { public int X, Z; public CrystalSpot(int x, int z) { X = x; Z = z; } }
        const int MinChunk = -3, MaxChunk = 2; // 6x6 chunks = 96x96m sunlit valley
        const int WaterLevel = 14;
        const int PanoramaSeed = 250607;
        const double SunnyMorningClock = 525.0; // Warm, bright, colorful mid-morning sun

        Camera targetCamera;
        Light sunLight;
        Material skyMaterial;
        Transform windmillHub;
        bool ready;
        float orbitAngle = -126f;

        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Transform> idleActors = new List<Transform>();
        readonly List<Vector3> actorBasePos = new List<Vector3>();
        readonly List<float> actorBaseYaw = new List<float>();

        public static MenuPanoramaScene Ensure(Camera menuCamera)
        {
            if (!Application.isPlaying || !menuCamera) return null;
            if (activeInstance)
            {
                activeInstance.targetCamera = menuCamera;
                activeInstance.ConfigureCameraAndSky();
                activeInstance.ApplyOrbitTransform(0f);
                return activeInstance;
            }
            var voxelMat = Resources.Load<Material>("Generated/VoxelPBR");
            if (!voxelMat) return null;

            var go = new GameObject("MenuPanorama3D");
            var scene = go.AddComponent<MenuPanoramaScene>();
            scene.BuildScene(menuCamera);
            return scene;
        }

        public static void Teardown()
        {
            if (!activeInstance) return;
            var go = activeInstance.gameObject;
            activeInstance.ready = false;
            activeInstance = null;
            if (go)
            {
                go.name = "DisposedMenuPanorama3D";
                go.SetActive(false);
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        }

        void ConfigureCameraAndSky()
        {
            if (!targetCamera) return;
            WorldAtmosphere.Configure(targetCamera, 0, SunnyMorningClock);
            sunLight = RenderSettings.sun;
            skyMaterial = RenderSettings.skybox;
            targetCamera.fieldOfView = 56f;
            targetCamera.nearClipPlane = 0.15f;
            targetCamera.farClipPlane = 260f;

            Shader.SetGlobalVector("_LCVoxelSkyOrigin", Vector4.zero);
            Shader.SetGlobalFloat("_GlobalWindStrength", 0.24f);
            Shader.SetGlobalFloat("_GlobalWetness", 0f);
            Shader.SetGlobalFloat("_GlobalSnow", 0f);
            Shader.SetGlobalFloat("_LCWaterRain", 0f);
            Shader.SetGlobalFloat("_LCSunGlareWeather", 0.85f);

            if (skyMaterial)
            {
                skyMaterial.SetFloat("_CloudCoverage", 0.24f);
                skyMaterial.SetFloat("_CloudSteps", VisualSettings.Current != null ? VisualSettings.Current.CloudSteps : 22);
                skyMaterial.SetVector("_CloudOffset", new Vector4(120f, 0f, 80f, 0f));
            }
            if (sunLight)
                AtmosphereLighting.At(SunnyMorningClock, Dimension.Lestingia, 0.20f, 0f).Apply(sunLight, SunnyMorningClock);
        }

        void BuildScene(Camera menuCamera)
        {
            activeInstance = this;
            targetCamera = menuCamera;
            ConfigureCameraAndSky();

            bool isSoftwareGpu = (SystemInfo.graphicsDeviceName ?? "").IndexOf("llvmpipe", StringComparison.OrdinalIgnoreCase) >= 0
                              || (SystemInfo.graphicsDeviceName ?? "").IndexOf("Microsoft Basic Render", StringComparison.OrdinalIgnoreCase) >= 0;
            int grassQuality = isSoftwareGpu ? 1 : 2;

            var chunks = new Dictionary<Vector2Int, ChunkData>();
            for (int cz = MinChunk; cz <= MaxChunk; cz++)
                for (int cx = MinChunk; cx <= MaxChunk; cx++)
                    chunks[new Vector2Int(cx, cz)] = new ChunkData();

            Func<int, int, int, ushort> getVoxel = (wx, wy, wz) =>
            {
                if (wy < 0 || wy >= 256) return (ushort)0;
                int chunkX = wx >= 0 ? wx / 16 : (wx - 15) / 16;
                int chunkZ = wz >= 0 ? wz / 16 : (wz - 15) / 16;
                ChunkData found;
                if (!chunks.TryGetValue(new Vector2Int(chunkX, chunkZ), out found)) return (ushort)(wy <= WaterLevel ? 1 : 0);
                return found.GetBlock(wx - chunkX * 16, wy, wz - chunkZ * 16);
            };

            Action<int, int, int, ushort> setVoxel = (wx, wy, wz, id) =>
            {
                if (wy < 0 || wy >= 256) return;
                int chunkX = wx >= 0 ? wx / 16 : (wx - 15) / 16;
                int chunkZ = wz >= 0 ? wz / 16 : (wz - 15) / 16;
                ChunkData found;
                if (!chunks.TryGetValue(new Vector2Int(chunkX, chunkZ), out found)) return;
                found.SetBlock(wx - chunkX * 16, wy, wz - chunkZ * 16, id);
            };

            SculptValley(setVoxel, getVoxel);

            var skylight = new VoxelSkylight();
            foreach (var kv in chunks)
                skylight.Register(kv.Key.x, kv.Key.y, kv.Value);
            while (skylight.Pending > 0)
                skylight.Process(65536);

            var voxelMat = Resources.Load<Material>("Generated/VoxelPBR");
            var waterMat = Resources.Load<Material>("Generated/Water");
            var grassMat = Resources.Load<Material>("Generated/Grass");
            var glassMat = Resources.Load<Material>("Generated/Glass");
            var crystalMat = Resources.Load<Material>("Generated/Crystal");
            var objectMat = Resources.Load<Material>("Generated/VoxelObjects");
            var sharedMats = new[] { voxelMat, waterMat, grassMat, glassMat, glassMat };

            var crystalCells = new List<Vector3>();
            var stationCells = new List<StationSpot>();
            var firePoints = new List<Vector3>();

            for (int cz = MinChunk; cz <= MaxChunk; cz++)
            {
                for (int cx = MinChunk; cx <= MaxChunk; cx++)
                {
                    var key = new Vector2Int(cx, cz);
                    var data = chunks[key];
                    int ox = cx * 16, oz = cz * 16;

                    for (int y = 8; y < 64; y++)
                        for (int z = 0; z < 16; z++)
                            for (int x = 0; x < 16; x++)
                            {
                                ushort id = data.GetBlock(x, y, z);
                                if (id == BlockRegistry.CrystalId)
                                    crystalCells.Add(new Vector3(ox + x, y, oz + z));
                                else if (id == 15 || id == 16 || id == 17)
                                {
                                    stationCells.Add(new StationSpot(id, new Vector3(ox + x, y, oz + z)));
                                    if (id == 15) firePoints.Add(new Vector3(ox + x + 0.5f, y + 0.45f, oz + z + 0.5f));
                                }
                            }

                    var meshData = ChunkMesher.Build(
                        data, ox, oz,
                        getVoxel,
                        PanoramaSeed,
                        decorate: true,
                        detailQuality: grassQuality,
                        skylight: skylight.Get,
                        fluidHeight: (fx, fy, fz) => getVoxel(fx, fy + 1, fz) == BlockRegistry.WaterId ? 1f : 0.88f,
                        greedy: true);

                    if (meshData.Vertices.Count == 0) continue;
                    var mesh = meshData.CreateMesh(false);
                    mesh.name = "MenuChunk_" + cx + "_" + cz;
                    ownedMeshes.Add(mesh);

                    var chunkGo = new GameObject("PanoramaChunk " + cx + "," + cz, typeof(MeshFilter), typeof(MeshRenderer));
                    chunkGo.transform.SetParent(transform, false);
                    chunkGo.transform.localPosition = new Vector3(ox, 0, oz);
                    chunkGo.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = chunkGo.GetComponent<MeshRenderer>();
                    mr.sharedMaterials = sharedMats;
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    mr.receiveShadows = true;
                }
            }

            if (crystalCells.Count > 0 && crystalMat)
                PlacedVoxelModel.CreateCrystalBatch(transform, crystalCells, crystalMat);

            if (objectMat)
            {
                foreach (var st in stationCells)
                    PlacedVoxelModel.Create(st.Id, transform, st.Pos, objectMat);
            }

            foreach (var fp in firePoints)
            {
                var fireGo = new GameObject("PanoramaCampfire");
                fireGo.transform.SetParent(transform, false);
                fireGo.transform.localPosition = fp;
                var light = fireGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.52f, 0.16f);
                light.intensity = 3.4f;
                light.range = 10f;
                light.shadows = LightShadows.None;
                if (!isSoftwareGpu)
                    fireGo.AddComponent<CampfireEffects>().Build();
            }

            BuildSpinningWindmillBlades(new Vector3(17.5f, 25.5f, 13.5f), voxelMat);
            if (!isSoftwareGpu)
            {
                SpawnLivingDioramaActors(getVoxel);
                BuildSunbeamMotes();
            }

            ready = true;
            ApplyOrbitTransform(0f);
        }

        static void SculptValley(Action<int, int, int, ushort> set, Func<int, int, int, ushort> get)
        {
            // 1. Sculpt sunlit basin: turquoise central lagoon + winding river + terraced emerald hills + northern alpine peaks
            for (int wz = MinChunk * 16; wz < (MaxChunk + 1) * 16; wz++)
            {
                for (int wx = MinChunk * 16; wx < (MaxChunk + 1) * 16; wx++)
                {
                    float distCenter = Mathf.Sqrt(wx * wx + wz * wz);
                    // Winding river channel from NW waterfall (-14, -22) through center (0,0) toward SW (10, 32)
                    float riverX = Mathf.Sin(wz * 0.085f) * 5.2f - wz * 0.18f;
                    float riverDist = Mathf.Abs(wx - riverX);
                    float lagoonDist = Mathf.Sqrt((wx + 2f) * (wx + 2f) * 0.85f + (wz - 1f) * (wz - 1f) * 0.95f);

                    float n1 = Mathf.PerlinNoise((wx + 310) * 0.042f, (wz + 190) * 0.042f);
                    float n2 = Mathf.PerlinNoise((wx - 120) * 0.095f, (wz + 410) * 0.095f);
                    float hill = 15.2f + n1 * 7.5f + n2 * 2.6f;

                    // Gentle amphitheater rim so distant edges frame the horizon naturally
                    hill += Mathf.SmoothStep(16f, 44f, distCenter) * 9.5f;

                    // Northern & North-Western picturesque alpine crags (for waterfall & spire)
                    if (wz < -12)
                    {
                        float northFactor = Mathf.SmoothStep(-12f, -38f, wz);
                        hill += northFactor * (8.5f + n2 * 5.5f);
                    }

                    // Flatten cozy homestead terrace on East knoll (wx ~ 10..22, wz ~ -6..16)
                    float villageMask = 1f - Mathf.SmoothStep(6f, 13f, Vector2.Distance(new Vector2(wx, wz), new Vector2(15f, 3f)));
                    hill = Mathf.Lerp(hill, 16.2f, villageMask * 0.88f);

                    float windmillMask = 1f - Mathf.SmoothStep(4f, 9f, Vector2.Distance(new Vector2(wx, wz), new Vector2(17.5f, 13.5f)));
                    hill = Mathf.Lerp(hill, 18.2f, windmillMask * 0.85f);

                    // Carve turquoise lagoon & river channel
                    float waterCarve = Mathf.Max(
                        1f - Mathf.SmoothStep(5.5f, 12.5f, lagoonDist),
                        1f - Mathf.SmoothStep(2.6f, 6.8f, riverDist));
                    hill = Mathf.Lerp(hill, 10.2f + n2 * 1.3f, waterCarve * 0.92f);

                    int topY = Mathf.Clamp(Mathf.RoundToInt(hill), 8, 39);
                    for (int y = 0; y <= topY; y++)
                    {
                        ushort block = 1; // stone
                        if (y == topY)
                        {
                            if (topY >= 31) block = 14; // alpine snow cap
                            else if (topY >= 27) block = (ushort)((wx + wz & 1) == 0 ? 28 : 1); // sunlit limestone & stone
                            else if (topY <= WaterLevel + 1 && waterCarve > 0.22f)
                                block = (ushort)(wz < -8 ? 35 : 6); // mossy boulders near waterfall, golden sand beach elsewhere!
                            else
                                block = 3; // lush emerald grass!
                        }
                        else if (y >= topY - 2 && topY < 27)
                        {
                            block = (ushort)(topY <= WaterLevel + 1 ? 6 : 2);
                        }
                        else if (y >= 18 && topY >= 26 && ((wx * 7 + y * 13 + wz * 5) % 19 == 0))
                        {
                            block = (ushort)((wx & 1) == 0 ? 21 : 22); // copper & gold ore veins in cliffside
                        }
                        set(wx, y, wz, block);
                    }

                    for (int y = topY + 1; y <= WaterLevel; y++)
                        set(wx, y, wz, BlockRegistry.WaterId);
                }
            }

            // 2. Carve stepped mountain waterfall on NW cliff (wx = -7..-4, wz = -22..-11) cascading into lagoon
            for (int wz = -23; wz <= -11; wz++)
            {
                int streamY = Mathf.RoundToInt(Mathf.Lerp(25f, WaterLevel, Mathf.InverseLerp(-23f, -11f, wz)));
                for (int wx = -7; wx <= -4; wx++)
                {
                    bool bank = wx == -7 || wx == -4;
                    set(wx, streamY - 1, wz, (ushort)(bank ? 35 : 28));
                    if (bank)
                    {
                        set(wx, streamY, wz, 35); // mossy stone banks
                    }
                    else
                    {
                        for (int cy = streamY; cy <= 36; cy++) set(wx, cy, wz, 0);
                        for (int wy = WaterLevel; wy <= streamY; wy++)
                            set(wx, wy, wz, BlockRegistry.WaterId);
                    }
                }
            }

            // 3. Arched Cobblestone & Honey-Oak Bridge across lagoon narrows (wx = -6..7, wz = -2..1)
            for (int wx = -6; wx <= 7; wx++)
            {
                float t = Mathf.InverseLerp(-6f, 7f, wx);
                int archY = WaterLevel + 1 + Mathf.RoundToInt(Mathf.Sin(t * Mathf.PI) * 2.2f);
                for (int wz = -2; wz <= 1; wz++)
                {
                    bool rail = wz == -2 || wz == 1;
                    if (!rail)
                    {
                        set(wx, archY, wz, 18); // honey-oak planks deck
                    }
                    else
                    {
                        set(wx, archY, wz, 37); // stone-bricks curb
                        if (wx == -5 || wx == 0 || wx == 6)
                        {
                            set(wx, archY + 1, wz, 4);  // timber post
                            set(wx, archY + 2, wz, 12); // glowing turquoise aether lantern!
                        }
                    }
                }
            }

            // 4. Cozy Quietford Cottage on East sunlit terrace (wx = 11..18, wz = -3..4, baseY = 16)
            BuildCottage(set, 11, 16, -3);

            // 5. Picturesque Windmill Tower on SE knoll (center wx = 17, wz = 13, baseY = 18)
            BuildWindmillTower(set, 16, 18, 12);

            // 6. Sleek New Era Beacon Spire on NE ridge (wx = 8, wz = -25, baseY = 25)
            BuildNewEraSpire(set, 7, 24, -25);

            // 7. Cobblestone & Sandstone garden path connecting bridge -> cottage -> windmill
            int[,] pathPts = { { 7, -1 }, { 8, 0 }, { 9, 0 }, { 10, 0 }, { 10, 1 }, { 13, 5 }, { 14, 7 }, { 15, 9 }, { 15, 11 } };
            for (int i = 0; i < pathPts.GetLength(0); i++)
            {
                int px = pathPts[i, 0], pz = pathPts[i, 1];
                for (int dx = 0; dx <= 1; dx++)
                    for (int dz = 0; dz <= 1; dz++)
                        for (int y = 24; y >= 12; y--)
                        {
                            ushort b = get(px + dx, y, pz + dz);
                            if (b == 3 || b == 2 || b == 6)
                            {
                                set(px + dx, y, pz + dz, (ushort)(((px + dz) & 1) == 0 ? 43 : 36));
                                break;
                            }
                        }
            }

            // 8. Lush Sakura (block 60) & Oak (block 5) grove + glowing shoreline crystals (block 11)
            TreeSpot[] trees =
            {
                new TreeSpot(-14,   6, true,  6),
                new TreeSpot(-18,  -4, false, 7),
                new TreeSpot(-11,  15, true,  6),
                new TreeSpot( -4,  19, false, 6),
                new TreeSpot(  5,  21, true,  7),
                new TreeSpot( 23,   2, true,  6),
                new TreeSpot( 22, -10, false, 7),
                new TreeSpot(-21,  14, false, 7),
                new TreeSpot(-16, -15, true,  5),
                new TreeSpot( 17, -14, true,  6),
                new TreeSpot( -2, -18, false, 6),
                new TreeSpot( 25,  16, false, 7),
            };
            foreach (var tr in trees)
            {
                int gy = FindSurfaceY(get, tr.X, tr.Z);
                if (gy <= WaterLevel || gy >= 29) continue;
                PlantTree(set, get, tr.X, gy + 1, tr.Z, tr.Height, (ushort)(tr.Sakura ? 60 : 5));
            }

            CrystalSpot[] crystals = { new CrystalSpot(-9, 4), new CrystalSpot(-10, 5), new CrystalSpot(-7, -8), new CrystalSpot(4, 11), new CrystalSpot(-3, 12) };
            foreach (var cr in crystals)
            {
                int gy = FindSurfaceY(get, cr.X, cr.Z);
                if (gy >= WaterLevel && gy < 24 && get(cr.X, gy + 1, cr.Z) == 0)
                    set(cr.X, gy + 1, cr.Z, BlockRegistry.CrystalId);
            }

            // 9. Second village house on South-East terrace + Floating Sky Islands & Cascading Sky Waterfalls!
            BuildCottage(set, 9, 16, 8);
            BuildSkyIsland(set, get, -15, 39, 15, 9, true, 1, 0);   // Grand Sakura Sky-Shrine Island + cascading sky waterfall into lagoon!
            BuildSkyIsland(set, get, 17, 44, -20, 7, false, -1, 1); // Sunlit Northern Sky Islet + twin waterfall into alpine river!
        }

        static void BuildSkyIsland(Action<int, int, int, ushort> set, Func<int, int, int, ushort> get, int cx, int deckY, int cz, int rad, bool withShrine, int lipDirX, int lipDirZ)
        {
            for (int dz = -rad - 1; dz <= rad + 1; dz++)
                for (int dx = -rad - 1; dx <= rad + 1; dx++)
                {
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    float effR = rad + Mathf.Sin(dx * 0.5f + cz) * 0.8f + Mathf.Cos(dz * 0.5f - cx) * 0.7f;
                    if (dist > effR) continue;
                    float norm = dist / Mathf.Max(1f, effR);
                    int depth = Mathf.Max(2, Mathf.RoundToInt((1f - norm * norm) * (rad * 1.15f)));
                    for (int y = deckY - depth; y <= deckY; y++)
                    {
                        ushort block;
                        if (y == deckY)
                            block = (ushort)(((dx + dz) & 3) == 0 ? 65 : 3); // LavenderTurf & EmeraldGrass
                        else if (y == deckY - 1)
                            block = 2;
                        else if (y == deckY - depth && dist < effR * 0.48f)
                            block = (ushort)(((dx + dz) & 1) == 0 ? 67 : 12); // Glowing AndromedaCrystal & AetherStone tip!
                        else
                            block = (ushort)(y <= deckY - depth + 2 ? 12 : 28);
                        set(cx + dx, y, cz + dz, block);
                    }
                }

            if (withShrine)
            {
                for (int dz = -3; dz <= 3; dz++)
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        set(cx + dx, deckY, cz + dz, (ushort)(Mathf.Abs(dx) <= 1 && Mathf.Abs(dz) <= 1 ? BlockRegistry.WaterId : 41));
                        if (Mathf.Abs(dx) == 3 && Mathf.Abs(dz) == 3)
                        {
                            for (int py = 1; py <= 4; py++)
                                set(cx + dx, deckY + py, cz + dz, 42);
                            set(cx + dx, deckY + 5, cz + dz, 56); // Warm glowing lantern
                        }
                        if (Mathf.Abs(dx) <= 3 && Mathf.Abs(dz) <= 3)
                            set(cx + dx, deckY + 5, cz + dz, (ushort)(Mathf.Abs(dx) <= 1 && Mathf.Abs(dz) <= 1 ? 47 : 41));
                    }
                set(cx, deckY + 1, cz, 67);
                set(cx, deckY + 2, cz, 11);
                PlantTree(set, get, cx - 5, deckY + 1, cz - 3, 6, 60);
                PlantTree(set, get, cx + 4, deckY + 1, cz + 4, 5, 5);
            }
            else
            {
                PlantTree(set, get, cx, deckY + 1, cz, 6, 60);
                set(cx + 2, deckY + 1, cz - 2, 67);
                set(cx - 2, deckY + 1, cz + 2, 56);
            }

            // Cascading Sky Waterfall from spring to island lip and all the way into the valley!
            int lipX = cx + lipDirX * (rad - 1);
            int lipZ = cz + lipDirZ * (rad - 1);
            int steps = Mathf.Max(Mathf.Abs(lipX - cx), Mathf.Abs(lipZ - cz));
            for (int s = 0; s <= steps; s++)
            {
                int sx = Mathf.RoundToInt(Mathf.Lerp(cx, lipX, s / (float)Mathf.Max(1, steps)));
                int sz = Mathf.RoundToInt(Mathf.Lerp(cz, lipZ, s / (float)Mathf.Max(1, steps)));
                set(sx, deckY, sz, BlockRegistry.WaterId);
            }
            for (int oz = 0; oz <= 1; oz++)
                for (int ox = 0; ox <= 1; ox++)
                {
                    int wx = lipX + ox, wz = lipZ + oz;
                    int groundY = FindSurfaceY(get, wx, wz);
                    for (int y = Mathf.Max(WaterLevel, groundY); y <= deckY; y++)
                        set(wx, y, wz, BlockRegistry.WaterId);
                }
        }

        static int FindSurfaceY(Func<int, int, int, ushort> get, int wx, int wz)
        {
            for (int y = 40; y >= 8; y--)
            {
                ushort b = get(wx, y, wz);
                if (b != 0 && b != BlockRegistry.WaterId && b != 5 && b != 60)
                    return y;
            }
            return WaterLevel;
        }

        static void BuildCottage(Action<int, int, int, ushort> set, int x0, int y0, int z0)
        {
            int w = 7, d = 6, h = 4;
            // Solid cobblestone foundation down to terrain
            for (int z = -1; z <= d + 1; z++)
                for (int x = -1; x <= w + 1; x++)
                {
                    for (int y = y0 - 4; y <= y0; y++)
                        set(x0 + x, y, z0 + z, (ushort)(y == y0 ? 36 : 1));
                    for (int y = y0 + 1; y <= y0 + 9; y++)
                        set(x0 + x, y, z0 + z, 0);
                }

            // Walls: warm limestone plaster + honey-oak wainscotting + bark corner pillars + glass windows
            for (int y = 1; y <= h; y++)
                for (int z = 0; z <= d; z++)
                    for (int x = 0; x <= w; x++)
                    {
                        bool edgeX = x == 0 || x == w, edgeZ = z == 0 || z == d;
                        if (!edgeX && !edgeZ) continue;
                        if (edgeX && edgeZ) { set(x0 + x, y0 + y, z0 + z, 4); continue; } // bark corner beam
                        // Front door facing bridge (-X)
                        if (x == 0 && z == 3 && y <= 2) continue;
                        // Sunlit windows
                        if (y == 2 && ((x == 0 && (z == 1 || z == 5)) || (x == w && z == 3) || (edgeZ && (x == 2 || x == 5))))
                        {
                            set(x0 + x, y0 + y, z0 + z, 44);
                            continue;
                        }
                        set(x0 + x, y0 + y, z0 + z, (ushort)(y == 1 ? 18 : 28));
                    }

            // Stepped Mediterranean terracotta-shingle gable roof (block 59)
            for (int layer = 0; layer <= 3; layer++)
            {
                int ry = y0 + h + 1 + layer;
                for (int x = -1; x <= w + 1; x++)
                {
                    int zMin = layer - 1, zMax = d + 1 - layer;
                    if (zMin > zMax) continue;
                    for (int z = zMin; z <= zMax; z++)
                    {
                        bool outer = z == zMin || z == zMax || layer == 3;
                        set(x0 + x, ry, z0 + z, (ushort)(outer ? 59 : 18));
                    }
                }
            }

            // Warm clay-brick chimney + cozy porch stations + yard campfire
            for (int cy = y0 + 1; cy <= y0 + h + 5; cy++)
                set(x0 + w - 1, cy, z0 + 1, 39);
            set(x0 - 1, y0 + 1, z0 + 1, 16); // Workbench on porch
            set(x0 - 1, y0 + 1, z0 + 5, 17); // Furnace on porch
            set(x0 - 2, y0 + 1, z0 + 3, 0);  // Clear doorway
            set(x0 - 2, y0 + 1, z0 - 2, 15); // Cozy yard campfire!
        }

        static void BuildWindmillTower(Action<int, int, int, ushort> set, int x0, int y0, int z0)
        {
            for (int z = -1; z <= 4; z++)
                for (int x = -1; x <= 4; x++)
                    for (int y = y0 - 4; y <= y0; y++)
                        set(x0 + x, y, z0 + z, 36);

            for (int y = 1; y <= 8; y++)
                for (int z = 0; z <= 3; z++)
                    for (int x = 0; x <= 3; x++)
                    {
                        bool corner = (x == 0 || x == 3) && (z == 0 || z == 3);
                        bool wall = x == 0 || x == 3 || z == 0 || z == 3;
                        if (!wall) { set(x0 + x, y0 + y, z0 + z, 0); continue; }
                        if (corner && y <= 6) set(x0 + x, y0 + y, z0 + z, 4);
                        else if (y == 4 && (x == 1 || x == 2) && z == 0) set(x0 + x, y0 + y, z0 + z, 44);
                        else set(x0 + x, y0 + y, z0 + z, (ushort)(y <= 2 ? 37 : 28));
                    }

            // Terracotta cap
            for (int z = -1; z <= 4; z++)
                for (int x = -1; x <= 4; x++)
                    set(x0 + x, y0 + 9, z0 + z, 59);
            for (int z = 0; z <= 3; z++)
                for (int x = 0; x <= 3; x++)
                    set(x0 + x, y0 + 10, z0 + z, 59);
            set(x0 + 1, y0 + 11, z0 + 1, 47);
            set(x0 + 2, y0 + 11, z0 + 2, 47);
        }

        static void BuildNewEraSpire(Action<int, int, int, ushort> set, int x0, int y0, int z0)
        {
            for (int z = -1; z <= 3; z++)
                for (int x = -1; x <= 3; x++)
                    for (int y = y0 - 4; y <= y0; y++)
                        set(x0 + x, y, z0 + z, 40);

            for (int y = 1; y <= 11; y++)
            {
                if (y <= 5)
                {
                    set(x0, y0 + y, z0, 66);
                    set(x0 + 2, y0 + y, z0, 66);
                    set(x0, y0 + y, z0 + 2, 66);
                    set(x0 + 2, y0 + y, z0 + 2, 66);
                    if (y == 3 || y == 5)
                        for (int dx = 0; dx <= 2; dx++)
                            for (int dz = 0; dz <= 2; dz++)
                                set(x0 + dx, y0 + y, z0 + dz, 42);
                }
                set(x0 + 1, y0 + y, z0 + 1, (ushort)(y >= 9 ? 12 : y % 3 == 0 ? 47 : 42));
            }
        }

        static void PlantTree(Action<int, int, int, ushort> set, Func<int, int, int, ushort> get, int tx, int ty, int tz, int height, ushort foliageId)
        {
            for (int y = 0; y < height - 1; y++)
                set(tx, ty + y, tz, 4);

            int crownY = ty + height - 2;
            for (int dy = -1; dy <= 3; dy++)
            {
                int radius = dy <= 0 ? 2 : dy == 1 ? 3 : dy == 2 ? 2 : 1;
                for (int dz = -radius; dz <= radius; dz++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx * dx + dz * dz > radius * radius + 1) continue;
                        if (dx == 0 && dz == 0 && dy <= 0) continue;
                        if (get(tx + dx, crownY + dy, tz + dz) == 0)
                            set(tx + dx, crownY + dy, tz + dz, foliageId);
                    }
            }
        }

        void BuildSpinningWindmillBlades(Vector3 hubWorldPos, Material voxelMat)
        {
            if (!voxelMat) return;
            var hubGo = new GameObject("WindmillSailsHub");
            hubGo.transform.SetParent(transform, false);
            // Position just outside the NW face of the windmill tower facing the lagoon
            hubGo.transform.localPosition = new Vector3(15.35f, 25.5f, 13.5f);
            hubGo.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            windmillHub = hubGo.transform;

            var data = new ChunkMeshData();
            // Build 4 crisp voxel sails (bark spars + bright marble/limestone canvas lattice)
            int barkLayer = 4, canvasLayer = 49, copperLayer = 47;
            AddBoxToMesh(data, new Vector3(-0.35f, -0.35f, -0.45f), new Vector3(0.35f, 0.35f, 0.45f), copperLayer);
            for (int arm = 0; arm < 4; arm++)
            {
                Vector3 dir = arm == 0 ? Vector3.up : arm == 1 ? Vector3.down : arm == 2 ? Vector3.right : Vector3.left;
                Vector3 side = arm < 2 ? Vector3.right : Vector3.up;
                for (int step = 1; step <= 6; step++)
                {
                    Vector3 p = dir * (step * 0.72f);
                    AddBoxToMesh(data, p - new Vector3(0.14f, 0.14f, 0.14f), p + new Vector3(0.14f, 0.14f, 0.14f), barkLayer);
                    if (step >= 2)
                    {
                        Vector3 sailCenter = p + side * 0.52f * (arm == 0 || arm == 3 ? 1f : -1f);
                        Vector3 half = arm < 2 ? new Vector3(0.42f, 0.34f, 0.06f) : new Vector3(0.34f, 0.42f, 0.06f);
                        AddBoxToMesh(data, sailCenter - half, sailCenter + half, canvasLayer);
                    }
                }
            }

            var mesh = new Mesh { name = "MenuWindmillSails" };
            mesh.SetVertices(data.Vertices);
            mesh.SetNormals(data.Normals);
            mesh.SetTangents(data.Tangents);
            mesh.SetColors(data.Colors);
            mesh.SetUVs(0, data.UVs);
            mesh.SetTriangles(data.OpaqueTriangles, 0);
            mesh.RecalculateBounds();
            ownedMeshes.Add(mesh);

            var sailsGo = new GameObject("SailsMesh", typeof(MeshFilter), typeof(MeshRenderer));
            sailsGo.transform.SetParent(windmillHub, false);
            sailsGo.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = sailsGo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = voxelMat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        static void AddBoxToMesh(ChunkMeshData data, Vector3 min, Vector3 max, int layer)
        {
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3[][] faces =
            {
                new[] { new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, min.y, max.z) },
                new[] { new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, min.z) },
                new[] { new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z) },
                new[] { new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z) },
                new[] { new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, min.y, max.z) },
                new[] { new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, min.z) },
            };
            for (int f = 0; f < 6; f++)
            {
                int start = data.Vertices.Count;
                Vector3 tangentDir = (faces[f][3] - faces[f][0]).normalized;
                for (int c = 0; c < 4; c++)
                {
                    data.Vertices.Add(faces[f][c]);
                    data.Normals.Add(normals[f]);
                    data.Tangents.Add(new Vector4(tangentDir.x, tangentDir.y, tangentDir.z, -1f));
                    data.Colors.Add(new Color32(255, 255, 255, 255));
                    float ux = c >= 2 ? 1f : 0f, vy = (c == 1 || c == 2) ? 1f : 0f;
                    data.UVs.Add(new Vector3(ux, vy, layer));
                }
                data.OpaqueTriangles.Add(start);
                data.OpaqueTriangles.Add(start + 1);
                data.OpaqueTriangles.Add(start + 2);
                data.OpaqueTriangles.Add(start);
                data.OpaqueTriangles.Add(start + 2);
                data.OpaqueTriangles.Add(start + 3);
            }
        }

        void SpawnLivingDioramaActors(Func<int, int, int, ushort> get)
        {
            try
            {
                // Bustling villagers & town cats across homestead, plaza, bridge, and floating Sky-Shrine!
                SpawnSettler(0, 1, "menu:farmer", new Vector3(8.8f, FindSurfaceY(get, 9, 0) + 1f, 0.3f), -110f);
                SpawnSettler(0, 3, "menu:architect", new Vector3(1.5f, WaterLevel + 4.1f, -0.5f), 165f);
                SpawnSettler(0, 0, "menu:smith", new Vector3(7.6f, FindSurfaceY(get, 8, 9) + 1f, 9.4f), -82f);
                SpawnSettler(0, 2, "menu:alchemist", new Vector3(-4.5f, FindSurfaceY(get, -5, -4) + 1f, -4.2f), 38f);
                SpawnSettler(0, 3, "menu:skykeeper", new Vector3(-12.5f, 40f, 15.5f), 130f);
                SpawnSettler(1, 0, "menu:cat", new Vector3(9.4f, FindSurfaceY(get, 9, -4) + 1f, -3.8f), -45f);
                SpawnSettler(1, 0, "menu:skycat", new Vector3(-14.2f, 40f, 17.5f), 60f);

                // Abundant wildlife herds in the sunlit meadow + soaring SkyWhale & Golden Eagles overhead!
                SpawnAnimal(0, new Vector3(-11.5f, FindSurfaceY(get, -11, 9) + 1f, 9.2f), 48f);
                SpawnAnimal(0, new Vector3(-8.2f, FindSurfaceY(get, -8, 12) + 1f, 12.4f), 115f);
                SpawnAnimal(6, new Vector3(-13.4f, FindSurfaceY(get, -13, 6) + 1f, 6.1f), 22f);
                SpawnAnimal(14, new Vector3(-5.8f, FindSurfaceY(get, -6, 14) + 1f, 14.2f), -35f);
                SpawnAnimal(1, new Vector3(-14.2f, FindSurfaceY(get, -14, 2) + 1f, 2.5f), 80f);
                SpawnAnimal(33, new Vector3(-4.0f, 46.5f, -8.0f), 125f); // Majestic SkyWhale gliding past sky islands!
                SpawnAnimal(11, new Vector3(9.5f, 38.5f, -11.0f), -145f); // Golden Eagle soaring above waterfall!
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuPanorama] Optional diorama actor skipped: " + e.Message);
            }
        }

        void SpawnSettler(int kind, int job, string id, Vector3 pos, float yaw)
        {
            var go = new GameObject("PanoramaSettler_" + id);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<SettlerVisual>().Build(kind, job, id);
            idleActors.Add(go.transform);
            actorBasePos.Add(pos);
            actorBaseYaw.Add(yaw);
        }

        void SpawnAnimal(int species, Vector3 pos, float yaw)
        {
            var go = new GameObject("PanoramaAnimal_" + species);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<AnimalVisual>().Build(species, false);
            idleActors.Add(go.transform);
            actorBasePos.Add(pos);
            actorBaseYaw.Add(yaw);
        }

        void BuildSunbeamMotes()
        {
            var baseMat = Resources.Load<Material>("Generated/Precipitation");
            if (!baseMat) return;
            var moteMat = new Material(baseMat);
            ownedMaterials.Add(moteMat);

            var go = new GameObject("GoldenSunbeamMotes");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 21f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.94f, 0.62f, 0.55f),
                new Color(0.75f, 1f, 0.72f, 0.45f));
            main.maxParticles = 90;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 14f, 44f);

            var emission = ps.emission;
            emission.rateOverTime = 12f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = moteMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
        }

        void Update()
        {
            if (!ready || !targetCamera) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            orbitAngle += dt * 5.2f; // Smooth Minecraft-style 360-degree panoramic rotation
            ApplyOrbitTransform(Time.unscaledTime);

            if (windmillHub)
                windmillHub.Rotate(0f, 0f, dt * 26f, Space.Self);

            if (skyMaterial)
                skyMaterial.SetVector("_CloudOffset", new Vector4(120f + Time.unscaledTime * 1.6f, 0f, 80f + Time.unscaledTime * 0.9f, 0f));

            Shader.SetGlobalFloat("_EnvironmentTime", Time.unscaledTime);

            for (int i = 0; i < idleActors.Count; i++)
            {
                var tr = idleActors[i];
                if (!tr) continue;
                float phase = Time.unscaledTime * 0.9f + i * 1.7f;
                float walk = (i < 2) ? Mathf.Sin(phase * 0.65f) * 0.85f : 0f;
                Vector3 forward = Quaternion.Euler(0f, actorBaseYaw[i], 0f) * Vector3.forward;
                tr.localPosition = actorBasePos[i] + forward * walk;
                tr.localRotation = Quaternion.Euler(0f, actorBaseYaw[i] + Mathf.Sin(phase) * 16f, 0f);
            }
        }

        void ApplyOrbitTransform(float t)
        {
            if (!targetCamera) return;
            float rad = orbitAngle * Mathf.Deg2Rad;
            const float radius = 25.5f;
            float camY = 24.2f + Mathf.Sin(t * 0.35f) * 0.85f;
            Vector3 camPos = new Vector3(Mathf.Cos(rad) * radius, camY, Mathf.Sin(rad) * radius);
            // Sweep across both the sunlit village/lagoon AND the majestic floating Sky Islands & waterfalls!
            Vector3 lookTarget = new Vector3(
                Mathf.Cos(rad + 0.35f) * 3.2f,
                21.2f + Mathf.Sin(t * 0.24f) * 1.6f,
                Mathf.Sin(rad + 0.35f) * 3.2f);
            targetCamera.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(lookTarget - camPos, Vector3.up));
        }

        void OnDestroy()
        {
            if (activeInstance == this) activeInstance = null;
            foreach (var m in ownedMeshes)
            {
                if (!m) continue;
                if (Application.isPlaying) Destroy(m);
                else DestroyImmediate(m);
            }
            ownedMeshes.Clear();
            foreach (var mat in ownedMaterials)
            {
                if (!mat) continue;
                if (Application.isPlaying) Destroy(mat);
                else DestroyImmediate(mat);
            }
            ownedMaterials.Clear();
        }
    }
}
