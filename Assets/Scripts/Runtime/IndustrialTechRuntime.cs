using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.Gameplay;
using LestingCraft.World;
using LestingCraft.Presentation;

namespace LestingCraft.Runtime
{
    /// <summary>
    /// Renders & drives all 3D Era v9 runtime actors & VFX:
    /// - 3D Connected Copper Power Wires + Electric Floodlight beams
    /// - 3D Drivable Electric Train ("ЭР-Воксель" Locomotive + Cargo Wagon) along Steel/Powered Rails (85/86)
    /// - 3D Recon Quad-Rotor Drone (with live FPV airborne camera mode [V]!)
    /// - 3D Autonomous Bipedal Industrial Robot ("Титан-М" mining + combat companion)
    /// - Modern C-4 Demolition Charges (87) & SECRET THERMONUKE 666 (95m 3D Mushroom Cloud + Shockwave + Trinitite Crater)
    /// - 3 Colossal Articulated Raid Bosses + Falling Radioactive Ash + Sluice Escape Rift inside Dimension.Radia!
    /// </summary>
    public sealed class IndustrialTechRuntime : MonoBehaviour
    {
        public readonly IndustrialAndRadiaSimulation Sim = new IndustrialAndRadiaSimulation();

        sealed class ArmedCharge
        {
            public Vector3 Position;
            public float Timer;
            public bool IsThermonuke666;
            public GameObject Visual;
            public Light Beacon;
        }

        sealed class MushroomCloudVfx
        {
            public GameObject Root;
            public Transform Fireball, Stem, Cap, ShockRing;
            public Mesh ShockRingMesh;
            public Light BlastLight;
            public float Elapsed;
            public float Duration = 34f;
        }

        sealed class ElectricTrainActor
        {
            public GameObject Root;
            public Transform LocoBody, WagonBody;
            public Light Headlight;
            public Vector3 TrackPos;
            public Vector2 TrackDir = Vector2.up;
            public float Speed;
            public bool PlayerMounted;
        }

        sealed class QuadDroneActor
        {
            public GameObject Root;
            public readonly Transform[] Rotors = new Transform[4];
            public Camera DroneCam;
            public Light SearchLight;
            public bool FpvActive;
        }

        sealed class IndustrialRobotActor
        {
            public GameObject Root;
            public Transform LeftLeg, RightLeg, LeftArm, RightArm, HeadVisor;
            public float WalkPhase;
            public float ActionTimer;
        }

        readonly List<ArmedCharge> activeCharges = new List<ArmedCharge>();
        readonly List<MushroomCloudVfx> activeNukes = new List<MushroomCloudVfx>();
        readonly List<ElectricTrainActor> trains = new List<ElectricTrainActor>();
        readonly List<IndustrialRobotActor> robots = new List<IndustrialRobotActor>();
        readonly GameObject[] bossVisuals = new GameObject[3];
        readonly List<GameObject> wirePool = new List<GameObject>();

        QuadDroneActor activeDrone;
        GameObject escapeRiftVisual;
        ParticleSystem radiaAshParticles;
        Material darkSteelMat, hazardYellowMat, copperMat, cyanGlowMat, redBeaconMat, whiteTrussMat, blastFireMat, smokeCloudMat, shockWaveMat;

        public bool IsRidingTrain { get; private set; }
        public bool IsDroneFpv => activeDrone != null && activeDrone.FpvActive;
        public float ActiveNukeFlash01 { get; private set; }
        public int ActiveTrainCount => trains.Count;
        public int ActiveRobotCount => robots.Count;
        public bool HasActiveDrone => activeDrone != null && activeDrone.Root != null;

        public static IndustrialTechRuntime Ensure(Transform parent)
        {
            var found = FindAnyObjectByType<IndustrialTechRuntime>();
            if (found) return found;
            var go = new GameObject("IndustrialTechRuntime");
            if (parent) go.transform.SetParent(parent, false);
            var comp = go.AddComponent<IndustrialTechRuntime>();
            comp.InitMaterials();
            return comp;
        }

        void InitMaterials()
        {
            if (darkSteelMat) return;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            darkSteelMat = CreateMat(sh, new Color(0.14f, 0.16f, 0.20f), Color.black, 0.78f, 0.56f);
            hazardYellowMat = CreateMat(sh, new Color(0.92f, 0.72f, 0.12f), Color.black, 0.55f, 0.50f);
            copperMat = CreateMat(sh, new Color(0.88f, 0.46f, 0.18f), new Color(0.16f, 0.07f, 0.02f), 0.85f, 0.65f);
            cyanGlowMat = CreateMat(sh, new Color(0.22f, 0.86f, 1.0f), new Color(0.45f, 2.20f, 3.10f), 0.1f, 0.85f);
            redBeaconMat = CreateMat(sh, new Color(1.0f, 0.16f, 0.12f), new Color(2.8f, 0.25f, 0.18f), 0.1f, 0.8f);
            whiteTrussMat = CreateMat(sh, new Color(0.86f, 0.89f, 0.92f), Color.black, 0.45f, 0.45f);
            blastFireMat = CreateMat(sh, new Color(1.0f, 0.62f, 0.18f), new Color(4.2f, 1.95f, 0.45f), 0.0f, 0.25f);
            // Warm light-grey plume: the old 0.34 grey rendered as pure-black blobs
            // at dusk/night (showcase shot 12). Brighter albedo + warm emissive keeps
            // the cloud readable against a dark sky without changing its daylight look.
            smokeCloudMat = CreateMat(sh, new Color(0.55f, 0.52f, 0.48f), new Color(0.38f, 0.24f, 0.13f), 0.0f, 0.08f);
            shockWaveMat = CreateMat(sh, new Color(1.0f, 0.78f, 0.43f), new Color(2.6f, 1.15f, 0.28f), 0.0f, 0.12f);
        }

        static Material CreateMat(Shader sh, Color baseCol, Color emission, float metallic, float smooth)
        {
            var m = new Material(sh) { color = baseCol };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (emission.maxColorComponent > 0.01f)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        static Transform PartPrimitive(PrimitiveType shape, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        static Transform PartCube(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
            => PartPrimitive(PrimitiveType.Cube, name, parent, pos, scale, mat);

        static Transform PartSphere(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
            => PartPrimitive(PrimitiveType.Sphere, name, parent, pos, scale, mat);

        static Transform PartCylinder(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
            => PartPrimitive(PrimitiveType.Cylinder, name, parent, pos, scale, mat);

        static Transform PartShockwaveAnnulus(string name, Transform parent, Material mat, out Mesh mesh)
        {
            const int segments = 64;
            const float innerRadius = 0.98f;
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(a), z = Mathf.Sin(a);
                vertices[i * 2] = new Vector3(x, 0f, z);
                vertices[i * 2 + 1] = new Vector3(x * innerRadius, 0f, z * innerRadius);
                int next = ((i + 1) % segments) * 2;
                int t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = i * 2 + 1; triangles[t + 2] = next;
                triangles[t + 3] = next; triangles[t + 4] = i * 2 + 1; triangles[t + 5] = next + 1;
            }
            mesh = new Mesh { name = name + "Mesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        // =========================================================================
        // 1. DEPLOY TRAINS, DRONES, ROBOTS, C-4 CHARGES & SECRET THERMONUKE 666!
        // =========================================================================

        public bool SpawnElectricTrain(Vector3 worldPos)
        {
            InitMaterials();
            var root = new GameObject("ElectricTrain_ERVoxel");
            root.transform.SetParent(transform, false);
            root.transform.position = worldPos;

            var loco = new GameObject("LocomotiveUnit").transform;
            loco.SetParent(root.transform, false);
            PartCube("Chassis", loco, new Vector3(0f, 0.36f, 0f), new Vector3(1.55f, 0.28f, 3.60f), darkSteelMat);
            PartCube("BodyBlue", loco, new Vector3(0f, 1.15f, 0f), new Vector3(1.48f, 1.32f, 3.40f), whiteTrussMat);
            PartCube("HazardStripe", loco, new Vector3(0f, 0.76f, 0f), new Vector3(1.52f, 0.24f, 3.44f), hazardYellowMat);
            PartCube("Windshield", loco, new Vector3(0f, 1.42f, 1.71f), new Vector3(1.24f, 0.46f, 0.06f), cyanGlowMat);
            PartCube("PantographBase", loco, new Vector3(0f, 1.88f, -0.4f), new Vector3(0.75f, 0.12f, 0.95f), copperMat);
            PartCube("PantographArm", loco, new Vector3(0f, 2.24f, -0.4f), new Vector3(0.95f, 0.62f, 0.10f), copperMat);
            for (int side = -1; side <= 1; side += 2)
                for (int wz = -1; wz <= 1; wz += 2)
                    PartCube("Wheel", loco, new Vector3(side * 0.62f, 0.18f, wz * 1.15f), new Vector3(0.14f, 0.36f, 0.42f), darkSteelMat);

            var hlGo = new GameObject("TrainHeadlight");
            hlGo.transform.SetParent(loco, false);
            hlGo.transform.localPosition = new Vector3(0f, 1.15f, 1.82f);
            var hl = hlGo.AddComponent<Light>();
            hl.type = LightType.Spot;
            hl.spotAngle = 58f;
            hl.range = 42f;
            hl.intensity = 4.5f;
            hl.color = new Color(0.88f, 0.96f, 1.0f);

            // Coupled heavy cargo gondola carriage trailing behind the locomotive!
            var wagon = new GameObject("CoupledCargoCarriage").transform;
            wagon.SetParent(root.transform, false);
            wagon.localPosition = new Vector3(0f, 0f, -3.55f);
            PartCube("Coupler", wagon, new Vector3(0f, 0.36f, 1.65f), new Vector3(0.24f, 0.16f, 0.70f), darkSteelMat);
            PartCube("WagonBed", wagon, new Vector3(0f, 0.36f, 0f), new Vector3(1.48f, 0.24f, 2.95f), darkSteelMat);
            PartCube("WallL", wagon, new Vector3(-0.68f, 0.88f, 0f), new Vector3(0.12f, 0.82f, 2.90f), hazardYellowMat);
            PartCube("WallR", wagon, new Vector3(0.68f, 0.88f, 0f), new Vector3(0.12f, 0.82f, 2.90f), hazardYellowMat);
            PartCube("CargoCrate", wagon, new Vector3(0f, 0.78f, 0f), new Vector3(1.05f, 0.62f, 2.10f), copperMat);
            for (int side = -1; side <= 1; side += 2)
                for (int wz = -1; wz <= 1; wz += 2)
                    PartCube("BogieWheel", wagon, new Vector3(side * 0.62f, 0.18f, wz * 0.95f), new Vector3(0.14f, 0.36f, 0.40f), darkSteelMat);

            trains.Add(new ElectricTrainActor
            {
                Root = root, LocoBody = loco, WagonBody = wagon, Headlight = hl,
                TrackPos = worldPos, TrackDir = Vector2.up
            });
            return true;
        }

        public bool ToggleMountNearestTrain(Vector3 playerPos, out string msg)
        {
            if (IsRidingTrain)
            {
                foreach (var t in trains) t.PlayerMounted = false;
                IsRidingTrain = false;
                msg = "Вы вышли из кабины электровоза «ЭР-Воксель».";
                return true;
            }
            foreach (var t in trains)
            {
                if (t.Root && Vector3.Distance(playerPos, t.TrackPos) <= 4.6f)
                {
                    t.PlayerMounted = true;
                    IsRidingTrain = true;
                    msg = "Кабина электровоза «ЭР-Воксель»: [W / S] тяга вперёд/назад по рельсам, [F] выход!";
                    return true;
                }
            }
            msg = null;
            return false;
        }

        public bool DeployQuadDrone(Vector3 spawnPos, Camera mainCam)
        {
            InitMaterials();
            if (activeDrone != null && activeDrone.Root)
            {
                activeDrone.Root.transform.position = spawnPos + Vector3.up * 1.8f;
                return true;
            }
            var root = new GameObject("ReconQuadDrone");
            root.transform.SetParent(transform, false);
            root.transform.position = spawnPos + Vector3.up * 1.8f;

            PartCube("Fuselage", root.transform, Vector3.zero, new Vector3(0.42f, 0.16f, 0.56f), darkSteelMat);
            PartCube("GimbalEye", root.transform, new Vector3(0f, -0.06f, 0.30f), new Vector3(0.16f, 0.14f, 0.16f), cyanGlowMat);
            PartCube("ArmX", root.transform, Vector3.zero, new Vector3(0.96f, 0.05f, 0.08f), whiteTrussMat);
            PartCube("ArmZ", root.transform, Vector3.zero, new Vector3(0.08f, 0.05f, 0.96f), whiteTrussMat);

            var drone = new QuadDroneActor { Root = root };
            Vector3[] rPos = { new Vector3(-0.46f, 0.07f, -0.46f), new Vector3(0.46f, 0.07f, -0.46f), new Vector3(-0.46f, 0.07f, 0.46f), new Vector3(0.46f, 0.07f, 0.46f) };
            for (int i = 0; i < 4; i++)
                drone.Rotors[i] = PartCube("Rotor" + i, root.transform, rPos[i], new Vector3(0.40f, 0.02f, 0.07f), hazardYellowMat);

            var lightGo = new GameObject("DroneSearchlight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.10f, 0.25f);
            lightGo.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
            drone.SearchLight = lightGo.AddComponent<Light>();
            drone.SearchLight.type = LightType.Spot;
            drone.SearchLight.range = 32f;
            drone.SearchLight.spotAngle = 64f;
            drone.SearchLight.intensity = 3.2f;
            drone.SearchLight.color = new Color(0.65f, 0.94f, 1.0f);

            var camGo = new GameObject("DroneFpvCamera");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0.02f, 0.34f);
            drone.DroneCam = camGo.AddComponent<Camera>();
            drone.DroneCam.fieldOfView = 84f;
            if (mainCam) drone.DroneCam.depth = mainCam.depth + 2f;
            drone.DroneCam.enabled = false;

            activeDrone = drone;
            return true;
        }

        public bool ToggleDroneFpv(out string msg)
        {
            if (activeDrone == null || !activeDrone.Root)
            {
                msg = null;
                return false;
            }
            activeDrone.FpvActive = !activeDrone.FpvActive;
            if (activeDrone.DroneCam) activeDrone.DroneCam.enabled = activeDrone.FpvActive;
            msg = activeDrone.FpvActive
                ? "КАМЕРА КВАДРОКОПТЕРА АКТИВИРОВАНА: [WASD + Space/C] воздушная разведка, [V] возврат к персонажу!"
                : "Управление возвращено персонажу — квадрокоптер переведён в режим сопровождения.";
            return true;
        }

        public bool DeployIndustrialRobot(Vector3 spawnPos)
        {
            InitMaterials();
            var root = new GameObject("IndustrialRobot_TitanM");
            root.transform.SetParent(transform, false);
            root.transform.position = spawnPos;

            var torso = PartCube("Torso", root.transform, new Vector3(0f, 1.48f, 0f), new Vector3(0.92f, 0.96f, 0.58f), whiteTrussMat);
            PartCube("ChestCore", torso, new Vector3(0f, 0.08f, 0.52f), new Vector3(0.36f, 0.32f, 0.08f), cyanGlowMat);
            var head = PartCube("Head", root.transform, new Vector3(0f, 2.18f, 0f), new Vector3(0.54f, 0.42f, 0.48f), darkSteelMat);
            PartCube("Visor", head, new Vector3(0f, 0.05f, 0.52f), new Vector3(0.76f, 0.30f, 0.06f), cyanGlowMat);

            var lLeg = PartCube("LeftLeg", root.transform, new Vector3(-0.26f, 0.52f, 0f), new Vector3(0.28f, 1.04f, 0.34f), darkSteelMat);
            var rLeg = PartCube("RightLeg", root.transform, new Vector3(0.26f, 0.52f, 0f), new Vector3(0.28f, 1.04f, 0.34f), darkSteelMat);
            var lArm = PartCube("LeftArm", root.transform, new Vector3(-0.62f, 1.45f, 0.12f), new Vector3(0.24f, 0.88f, 0.30f), hazardYellowMat);
            var rArm = PartCube("RightArm", root.transform, new Vector3(0.62f, 1.45f, 0.12f), new Vector3(0.24f, 0.88f, 0.30f), hazardYellowMat);

            robots.Add(new IndustrialRobotActor
            {
                Root = root, LeftLeg = lLeg, RightLeg = rLeg, LeftArm = lArm, RightArm = rArm, HeadVisor = head
            });
            return true;
        }

        public void ArmExplosive(Vector3 worldPos, bool thermonuke666)
        {
            InitMaterials();
            var go = new GameObject(thermonuke666 ? "ArmedThermonuke_666" : "ArmedDemolitionCharge_C4");
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos;

            if (thermonuke666)
            {
                PartCube("BaseCradle", go.transform, new Vector3(0f, 0.12f, 0f), new Vector3(1.15f, 0.24f, 1.15f), hazardYellowMat);
                PartCube("PhysicsPackage", go.transform, new Vector3(0f, 0.82f, 0f), new Vector3(0.86f, 1.18f, 0.86f), darkSteelMat);
                PartCube("NoseCone", go.transform, new Vector3(0f, 1.56f, 0f), new Vector3(0.52f, 0.36f, 0.52f), redBeaconMat);
                for (int s = -1; s <= 1; s += 2)
                    PartCube("Fin" + s, go.transform, new Vector3(s * 0.52f, 0.45f, 0f), new Vector3(0.12f, 0.65f, 0.52f), redBeaconMat);
            }
            else
            {
                PartCube("C4Pack", go.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.76f, 0.42f, 0.64f), darkSteelMat);
                PartCube("DetonatorLED", go.transform, new Vector3(0f, 0.46f, 0f), new Vector3(0.28f, 0.08f, 0.20f), redBeaconMat);
            }

            var bGo = new GameObject("ChargeBeaconLight");
            bGo.transform.SetParent(go.transform, false);
            bGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var bl = bGo.AddComponent<Light>();
            bl.type = LightType.Point;
            bl.color = new Color(1f, 0.16f, 0.12f);
            bl.range = thermonuke666 ? 26f : 9f;
            bl.intensity = thermonuke666 ? 5f : 2.2f;

            activeCharges.Add(new ArmedCharge
            {
                Position = worldPos,
                Timer = thermonuke666 ? 8.0f : 3.8f,
                IsThermonuke666 = thermonuke666,
                Visual = go,
                Beacon = bl
            });
        }

        // =========================================================================
        // 2. MAIN PER-FRAME RUNTIME STEP (WIRES, VEHICLES, BOMBS & RADIA ABYSS!)
        // =========================================================================
        public void StepRuntime(
            float dt,
            VoxelWorld world,
            FirstPersonController player,
            SurvivalSession session,
            Camera mainCam,
            float forwardAxis,
            float strafeAxis,
            float verticalAxis,
            float mouseX,
            float mouseY,
            out bool requestRadiaEntry,
            out bool requestRadiaExit)
        {
            requestRadiaEntry = false;
            requestRadiaExit = false;
            if (!world || !player || session == null) return;
            InitMaterials();
            var proto = FindAnyObjectByType<PrototypeGame>();
            if (proto && proto.LaunchComplexVisual && !proto.SpaceHelm.Active)
                proto.LaunchComplexVisual.gameObject.SetActive(world.Dimension == Dimension.Lestingia);

            float daylight = RenderSettings.ambientIntensity > 0f ? Mathf.Clamp01(RenderSettings.ambientSkyColor.grayscale * 2.2f) : 0.75f;
            Sim.StepPowerAndRadiation(
                dt,
                player.transform.position,
                world.Dimension,
                daylight,
                session.Inventory,
                (x, y, z) => world.GetBlock(x, y, z),
                out requestRadiaEntry);

            Sync3DPowerWires(world);
            StepTrains(dt, world, player, forwardAxis);
            StepDrone(dt, player, forwardAxis, strafeAxis, verticalAxis, mouseX, mouseY);
            StepRobots(dt, player, session);
            StepExplosivesAndMushroomClouds(dt, world, player, session);
            StepRadiaVisualsAndBosses(dt, world, player, session, out requestRadiaExit);

            // Presentation singletons require a play-mode scene; keep runtime simulation safe in EditMode tests.
            if (Application.isPlaying)
                VisualSettings.EnsurePostProcessingVolume(
                    Sim.RadiationDose01,
                    world.Dimension == Dimension.Radia,
                    ActiveNukeFlash01);
        }

        void Sync3DPowerWires(VoxelWorld world)
        {
            int used = 0;
            Vector3Int[] posDirs = { Vector3Int.right, Vector3Int.up, new Vector3Int(0, 0, 1) };
            foreach (var node in Sim.EnergizedNodes)
            {
                for (int d = 0; d < 3; d++)
                {
                    var nb = node + posDirs[d];
                    if (!Sim.EnergizedNodes.Contains(nb)) continue;
                    if (used >= 160) break;
                    while (wirePool.Count <= used)
                    {
                        var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        seg.name = "PowerBusCableSegment";
                        seg.transform.SetParent(transform, false);
                        var col = seg.GetComponent<Collider>();
                        if (col) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
                        seg.GetComponent<MeshRenderer>().sharedMaterial = copperMat;
                        wirePool.Add(seg);
                    }
                    var wire = wirePool[used++];
                    wire.SetActive(true);
                    Vector3 a = new Vector3(node.x + 0.5f, node.y + 0.5f, node.z + 0.5f);
                    Vector3 b = new Vector3(nb.x + 0.5f, nb.y + 0.5f, nb.z + 0.5f);
                    wire.transform.position = (a + b) * 0.5f;
                    wire.transform.localScale = d == 0 ? new Vector3(1.02f, 0.14f, 0.14f)
                                              : d == 1 ? new Vector3(0.14f, 1.02f, 0.14f)
                                                       : new Vector3(0.14f, 0.14f, 1.02f);
                }
            }
            for (int i = used; i < wirePool.Count; i++)
                if (wirePool[i]) wirePool[i].SetActive(false);
        }

        void StepTrains(float dt, VoxelWorld world, FirstPersonController player, float throttleInput)
        {
            foreach (var t in trains)
            {
                if (!t.Root) continue;
                int rx = Mathf.FloorToInt(t.TrackPos.x), ry = Mathf.FloorToInt(t.TrackPos.y), rz = Mathf.FloorToInt(t.TrackPos.z);
                ushort railBlock = world.GetBlock(rx, ry, rz);
                if (!BlockRegistry.IsRail(railBlock) && BlockRegistry.IsRail(world.GetBlock(rx, ry - 1, rz)))
                    railBlock = world.GetBlock(rx, --ry, rz);

                bool energizedRail = railBlock == BlockRegistry.PoweredRailId;
                float maxSpeed = energizedRail ? 24f : 13f;

                if (t.PlayerMounted)
                {
                    if (Mathf.Abs(throttleInput) > 0.05f)
                        t.Speed = Mathf.MoveTowards(t.Speed, throttleInput * maxSpeed, dt * (energizedRail ? 15f : 8f));
                    else
                        t.Speed = Mathf.MoveTowards(t.Speed, 0f, dt * 5.5f);
                }
                else
                {
                    t.Speed = Mathf.MoveTowards(t.Speed, 0f, dt * 6f);
                }

                if (Mathf.Abs(t.Speed) > 0.02f)
                {
                    Vector2 moveDir = t.Speed >= 0f ? t.TrackDir : -t.TrackDir;
                    Vector3 next = t.TrackPos + new Vector3(moveDir.x, 0f, moveDir.y) * (Mathf.Abs(t.Speed) * dt);
                    int nx = Mathf.FloorToInt(next.x), nz = Mathf.FloorToInt(next.z);
                    int foundY = -999;
                    for (int dy = 1; dy >= -1; dy--)
                        if (BlockRegistry.IsRail(world.GetBlock(nx, ry + dy, nz))) { foundY = ry + dy; break; }

                    if (foundY > -900)
                    {
                        t.TrackPos = new Vector3(next.x, foundY + 0.35f, next.z);
                    }
                    else
                    {
                        // Check perpendicular railway turn!
                        Vector2 left = new Vector2(-moveDir.y, moveDir.x);
                        Vector2 right = new Vector2(moveDir.y, -moveDir.x);
                        if (BlockRegistry.IsRail(world.GetBlock(rx + Mathf.RoundToInt(left.x), ry, rz + Mathf.RoundToInt(left.y))))
                            t.TrackDir = t.Speed >= 0f ? left : -left;
                        else if (BlockRegistry.IsRail(world.GetBlock(rx + Mathf.RoundToInt(right.x), ry, rz + Mathf.RoundToInt(right.y))))
                            t.TrackDir = t.Speed >= 0f ? right : -right;
                        else
                            t.Speed = 0f;
                    }
                }

                t.Root.transform.position = t.TrackPos;
                if (t.TrackDir.sqrMagnitude > 0.01f)
                    t.Root.transform.rotation = Quaternion.Slerp(t.Root.transform.rotation,
                        Quaternion.LookRotation(new Vector3(t.TrackDir.x, 0f, t.TrackDir.y), Vector3.up), 1f - Mathf.Exp(-dt * 10f));

                if (t.PlayerMounted)
                {
                    var cc = player.GetComponent<CharacterController>();
                    if (cc) cc.enabled = false;
                    player.transform.position = t.TrackPos + t.Root.transform.up * 0.95f + t.Root.transform.forward * 0.65f;
                    if (cc) cc.enabled = true;
                }
            }
        }

        void StepDrone(float dt, FirstPersonController player, float fwd, float strafe, float vert, float mx, float my)
        {
            if (activeDrone == null || !activeDrone.Root) return;
            for (int i = 0; i < 4; i++)
                if (activeDrone.Rotors[i])
                    activeDrone.Rotors[i].Rotate(0f, dt * 1650f * (i % 2 == 0 ? 1f : -1f), 0f, Space.Self);

            if (activeDrone.FpvActive)
            {
                Transform dtTf = activeDrone.Root.transform;
                Vector3 e = dtTf.eulerAngles;
                float pitch = e.x > 180f ? e.x - 360f : e.x;
                pitch = Mathf.Clamp(pitch - my * 0.14f, -82f, 82f);
                dtTf.rotation = Quaternion.Euler(pitch, e.y + mx * 0.14f, 0f);
                Vector3 move = dtTf.forward * fwd + dtTf.right * strafe + Vector3.up * vert;
                dtTf.position += move * (18f * dt);
            }
            else
            {
                Vector3 hover = player.transform.position + new Vector3(Mathf.Cos(Time.time * 1.1f) * 2.1f, 2.35f + Mathf.Sin(Time.time * 2.4f) * 0.18f, Mathf.Sin(Time.time * 1.1f) * 2.1f);
                activeDrone.Root.transform.position = Vector3.Lerp(activeDrone.Root.transform.position, hover, 1f - Mathf.Exp(-dt * 4.5f));
                activeDrone.Root.transform.rotation = Quaternion.Euler(0f, Time.time * 42f, 0f);
            }
        }

        void StepRobots(float dt, FirstPersonController player, SurvivalSession session)
        {
            foreach (var r in robots)
            {
                if (!r.Root) continue;
                r.ActionTimer -= dt;

                // Prioritize nearest hostile mutant or Radia boss within 18m!
                Vector3 goal = player.transform.position - player.transform.forward * 2.6f + player.transform.right * 1.8f;
                bool attacking = false;

                for (int i = 0; i < Sim.Bosses.Length; i++)
                {
                    var b = Sim.Bosses[i];
                    if (b != null && !b.Dead && Vector3.Distance(r.Root.transform.position, b.Position) < 20f)
                    {
                        goal = b.Position;
                        attacking = true;
                        if (Vector3.Distance(r.Root.transform.position, b.Position) < 4.2f && r.ActionTimer <= 0f)
                        {
                            b.Hp = Mathf.Max(0, b.Hp - 18);
                            if (b.Dead) Sim.BossesDefeatedMask |= (1 << i);
                            r.ActionTimer = 0.85f;
                        }
                        break;
                    }
                }

                if (!attacking && session.Animals != null)
                {
                    foreach (var a in session.Animals.Animals)
                    {
                        if (!a.dead && WildlifeSimulation.IsRadiaMutant(a.species))
                        {
                            Vector3 ap = new Vector3(a.x, a.y, a.z);
                            if (Vector3.Distance(r.Root.transform.position, ap) < 18f)
                            {
                                goal = ap;
                                attacking = true;
                                if (Vector3.Distance(r.Root.transform.position, ap) < 3.4f && r.ActionTimer <= 0f)
                                {
                                    a.health = Math.Max(0, a.health - 22);
                                    if (a.health == 0) a.dead = true;
                                    r.ActionTimer = 0.75f;
                                }
                                break;
                            }
                        }
                    }
                }

                Vector3 delta = goal - r.Root.transform.position;
                delta.y = 0f;
                if (delta.magnitude > 1.35f)
                {
                    r.WalkPhase += dt * 7.5f;
                    r.Root.transform.position += delta.normalized * ((attacking ? 6.2f : 4.5f) * dt);
                    r.Root.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                }
                r.Root.transform.position = new Vector3(r.Root.transform.position.x, player.transform.position.y, r.Root.transform.position.z);
                float swing = Mathf.Sin(r.WalkPhase) * 28f;
                if (r.LeftLeg) r.LeftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
                if (r.RightLeg) r.RightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
                if (r.LeftArm) r.LeftArm.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 0f);
                if (r.RightArm) r.RightArm.localRotation = Quaternion.Euler(swing * 0.8f, 0f, 0f);
            }
        }

        static void DestroyGeneratedObject(UnityEngine.Object generated)
        {
            if (!generated) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(generated);
            else UnityEngine.Object.DestroyImmediate(generated);
        }

        void OnDestroy()
        {
            for (int i = 0; i < activeNukes.Count; i++)
            {
                var nuke = activeNukes[i];
                if (nuke == null) continue;
                DestroyGeneratedObject(nuke.Root);
                DestroyGeneratedObject(nuke.ShockRingMesh);
            }
            activeNukes.Clear();
        }

        void StepExplosivesAndMushroomClouds(float dt, VoxelWorld world, FirstPersonController player, SurvivalSession session)
        {
            for (int i = activeCharges.Count - 1; i >= 0; i--)
            {
                var ch = activeCharges[i];
                ch.Timer -= dt;
                if (ch.Beacon)
                {
                    float freq = ch.IsThermonuke666 ? 14f : 9f;
                    ch.Beacon.intensity = (0.5f + 0.5f * Mathf.Sin(Time.time * freq)) * (ch.IsThermonuke666 ? 9f : 3.5f);
                }
                if (ch.Timer > 0f) continue;

                DetonateCharge(ch, world, player, session);
                DestroyGeneratedObject(ch.Visual);
                activeCharges.RemoveAt(i);
            }

            ActiveNukeFlash01 = 0f;
            for (int i = activeNukes.Count - 1; i >= 0; i--)
            {
                var m = activeNukes[i];
                m.Elapsed += dt;
                float t = m.Elapsed / m.Duration;
                if (t >= 1f)
                {
                    DestroyGeneratedObject(m.Root);
                    DestroyGeneratedObject(m.ShockRingMesh);
                    activeNukes.RemoveAt(i);
                    continue;
                }
                ActiveNukeFlash01 = Mathf.Max(ActiveNukeFlash01, Mathf.Clamp01(1f - m.Elapsed / 2.6f));
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(m.Elapsed / 9.5f));
                if (m.Fireball)
                {
                    float s = Mathf.Lerp(10f, 36f, rise) * (1f - t * 0.35f);
                    m.Fireball.localPosition = new Vector3(0f, Mathf.Lerp(6f, 64f, rise), 0f);
                    m.Fireball.localScale = new Vector3(s, s * 0.72f, s);
                }
                if (m.Cap)
                {
                    float cs = Mathf.Lerp(8f, 58f, rise);
                    m.Cap.localPosition = new Vector3(0f, Mathf.Lerp(8f, 70f, rise), 0f);
                    m.Cap.localScale = new Vector3(cs, Mathf.Lerp(4f, 18f, rise), cs);
                    m.Cap.Rotate(0f, dt * 14f, 0f, Space.Self);
                }
                if (m.Stem)
                {
                    float hgt = Mathf.Lerp(6f, 68f, rise);
                    m.Stem.localPosition = new Vector3(0f, hgt * 0.5f, 0f);
                    m.Stem.localScale = new Vector3(Mathf.Lerp(5f, 15f, rise), hgt, Mathf.Lerp(5f, 15f, rise));
                }
                if (m.ShockRing)
                {
                    float ring = Mathf.Lerp(6f, 165f, Mathf.Clamp01(m.Elapsed / 4.2f));
                    m.ShockRing.localScale = new Vector3(ring, 2.2f, ring);
                    m.ShockRing.gameObject.SetActive(m.Elapsed < 5.0f);
                }
                if (m.BlastLight)
                    m.BlastLight.intensity = Mathf.Lerp(48f, 0f, t);
            }
        }

        void DetonateCharge(ArmedCharge ch, VoxelWorld world, FirstPersonController player, SurvivalSession session)
        {
            int cx = Mathf.FloorToInt(ch.Position.x), cy = Mathf.FloorToInt(ch.Position.y), cz = Mathf.FloorToInt(ch.Position.z);
            int radius = ch.IsThermonuke666 ? 11 : 5;
            float r2 = radius * radius;
            float linerR2 = (radius + 1.35f) * (radius + 1.35f);

            for (int z = cz - radius - 1; z <= cz + radius + 1; z++)
            for (int x = cx - radius - 1; x <= cx + radius + 1; x++)
            {
                bool touchedCol = false;
                for (int y = Mathf.Max(2, cy - radius); y <= Mathf.Min(250, cy + radius); y++)
                {
                    float d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy) + (z - cz) * (z - cz);
                    if (d2 > linerR2) continue;
                    ushort cur = world.GetBlock(x, y, z);
                    if (cur == 0 || cur == BlockRegistry.BedrockId || cur == BlockRegistry.RadiaTerminalId) continue;

                    if (d2 <= r2)
                    {
                        world.Edit(x, y, z, BlockRegistry.AirId, false);
                        touchedCol = true;
                        if (!ch.IsThermonuke666 && (x + y + z) % 4 == 0 && session?.Inventory != null)
                            session.Inventory.Add(Items.ToolDrop(cur), 1);
                    }
                    else if (ch.IsThermonuke666 && BlockRegistry.IsSolid(cur))
                    {
                        // Fuse crater walls & floor into black Vitrified Trinitite Glass (Block 88)!
                        world.Edit(x, y, z, BlockRegistry.TrinititeSlagId, false);
                        touchedCol = true;
                    }
                }
                if (touchedCol)
                    world.FlushColumnSkylight(x, cy, z);
            }

            float blastDist = Vector3.Distance(player.transform.position, ch.Position);
            float killRadius = ch.IsThermonuke666 ? 38f : 8.5f;
            if (blastDist < killRadius && !session.Creative)
            {
                float dmg = Mathf.Lerp(ch.IsThermonuke666 ? 250f : 65f, 8f, blastDist / killRadius);
                session.Vitals.Damage(dmg, session.Mode);
            }

            if (ch.IsThermonuke666)
            {
                Sim.FalloutZones.Add(new FalloutZone { Center = ch.Position, Radius = 44f, RemainingSeconds = 240f });
                var root = new GameObject("Thermonuke666_MushroomCloud");
                root.transform.SetParent(transform, false);
                root.transform.position = ch.Position;
                // Smooth 3D plume: a rising spherical stem, incandescent fireball, and a multi-lobed cap.
                var stem = PartSphere("MushroomStem", root.transform, Vector3.up * 6f, new Vector3(8f, 12f, 8f), smokeCloudMat);
                var ball = PartSphere("NuclearFireball", root.transform, Vector3.up * 12f, new Vector3(14f, 10f, 14f), blastFireMat);
                var cap = new GameObject("PyrocumulusCap").transform;
                cap.SetParent(root.transform, false);
                cap.localPosition = Vector3.up * 15f;
                Vector3[] lobePositions =
                {
                    Vector3.zero, new Vector3(-0.34f, -0.08f, 0f), new Vector3(0.34f, -0.08f, 0f),
                    new Vector3(0f, -0.06f, -0.34f), new Vector3(0f, -0.06f, 0.34f),
                    new Vector3(-0.20f, 0.11f, 0.19f), new Vector3(0.20f, 0.11f, -0.19f)
                };
                Vector3[] lobeScales =
                {
                    new Vector3(0.74f, 0.82f, 0.74f), new Vector3(0.50f, 0.62f, 0.52f), new Vector3(0.50f, 0.62f, 0.52f),
                    new Vector3(0.52f, 0.60f, 0.52f), new Vector3(0.52f, 0.60f, 0.52f),
                    new Vector3(0.42f, 0.52f, 0.44f), new Vector3(0.42f, 0.52f, 0.44f)
                };
                for (int lobe = 0; lobe < lobePositions.Length; lobe++)
                    PartSphere("PyrocumulusLobe_" + lobe, cap, lobePositions[lobe], lobeScales[lobe], smokeCloudMat);
                var ring = PartShockwaveAnnulus("SupersonicWilsonRing", root.transform, shockWaveMat, out Mesh ringMesh);
                ring.localPosition = Vector3.up * 1.2f;
                var lGo = new GameObject("ThermonukeFlashLight");
                lGo.transform.SetParent(root.transform, false);
                lGo.transform.localPosition = Vector3.up * 28f;
                var lt = lGo.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(1.0f, 0.78f, 0.42f);
                lt.range = 190f;
                lt.intensity = 48f;
                activeNukes.Add(new MushroomCloudVfx { Root = root, Fireball = ball, Stem = stem, Cap = cap, ShockRing = ring, ShockRingMesh = ringMesh, BlastLight = lt });
                session.Notice = "☢ ДЕТОНАЦИЯ ОБЪЕКТА 666! Кратер остеклован в тринитит — эпицентр заражён радиацией!";
            }
            else
            {
                session.Notice = "Фугасный заряд С-4 сдетонировал!";
            }
        }

        // =========================================================================
        // 3. RADIA IRRADIATED ABYSS ("ЧЕРЕНКОВСКИЙ ЗАТОН") BOSSES, ASH & PORTAL
        // =========================================================================
        void StepRadiaVisualsAndBosses(float dt, VoxelWorld world, FirstPersonController player, SurvivalSession session, out bool exitRadia)
        {
            exitRadia = false;
            bool inRadia = world.Dimension == Dimension.Radia;

            // Drifting grey radioactive fallout ash particles matching image-1.png
            if (inRadia && !radiaAshParticles)
            {
                var ashGo = new GameObject("RadiaFalloutAshParticles");
                ashGo.transform.SetParent(transform, false);
                radiaAshParticles = ashGo.AddComponent<ParticleSystem>();
                var main = radiaAshParticles.main;
                main.maxParticles = 900;
                main.startLifetime = 6.5f;
                main.startSpeed = 1.6f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
                main.startColor = new Color(0.82f, 0.85f, 0.88f, 0.65f);
                main.gravityModifier = 0.14f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var shape = radiaAshParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(54f, 18f, 54f);
                var em = radiaAshParticles.emission;
                em.rateOverTime = 140f;
                var psr = ashGo.GetComponent<ParticleSystemRenderer>();
                psr.sharedMaterial = whiteTrussMat;
            }
            if (radiaAshParticles)
            {
                radiaAshParticles.gameObject.SetActive(inRadia);
                if (inRadia) radiaAshParticles.transform.position = player.transform.position + Vector3.up * 8f;
            }

            for (int i = 0; i < 3; i++)
            {
                var b = Sim.Bosses[i];
                bool show = inRadia && b != null && !b.Dead;
                if (show && !bossVisuals[i])
                    bossVisuals[i] = BuildColossalBossVisual(i);
                if (bossVisuals[i])
                {
                    bossVisuals[i].SetActive(show);
                    if (show)
                    {
                        bossVisuals[i].transform.position = b.Position;
                        bossVisuals[i].transform.rotation = Quaternion.Euler(0f, b.Yaw, 0f);
                    }
                }
            }

            if (!inRadia)
            {
                if (escapeRiftVisual) escapeRiftVisual.SetActive(false);
                return;
            }

            Sim.StepRadiaBossesAndPurge(dt, player.transform.position, (dmg, bossName) =>
            {
                if (!session.Creative)
                {
                    session.Vitals.Damage(dmg, session.Mode);
                    session.Notice = $"⚡ {bossName} наносит сокрушительный удар!";
                }
            });

            if (Sim.EscapePortalOpen)
            {
                if (!escapeRiftVisual)
                {
                    escapeRiftVisual = new GameObject("RadiaDecontaminationRift");
                    escapeRiftVisual.transform.SetParent(transform, false);
                    escapeRiftVisual.transform.position = new Vector3(0f, 58f, 70f);
                    PartCube("RiftPillar", escapeRiftVisual.transform, Vector3.up * 3.5f, new Vector3(2.8f, 9.0f, 1.2f), cyanGlowMat);
                    PartCube("RiftFrameL", escapeRiftVisual.transform, new Vector3(-1.8f, 3.5f, 0f), new Vector3(0.55f, 9.4f, 1.4f), whiteTrussMat);
                    PartCube("RiftFrameR", escapeRiftVisual.transform, new Vector3(1.8f, 3.5f, 0f), new Vector3(0.55f, 9.4f, 1.4f), whiteTrussMat);
                }
                escapeRiftVisual.SetActive(true);
                if (Vector3.Distance(player.transform.position, new Vector3(0f, 58f, 70f)) < 3.6f)
                    exitRadia = true;
            }
            else if (escapeRiftVisual)
            {
                escapeRiftVisual.SetActive(false);
            }
        }

        GameObject BuildColossalBossVisual(int index)
        {
            var go = new GameObject("RadiaBoss_" + index);
            go.transform.SetParent(transform, false);
            float s = 1.0f + index * 0.16f;
            PartCube("LegL", go.transform, new Vector3(-0.68f * s, 0.95f * s, 0f), new Vector3(0.52f * s, 1.90f * s, 0.62f * s), darkSteelMat);
            PartCube("LegR", go.transform, new Vector3(0.68f * s, 0.95f * s, 0f), new Vector3(0.52f * s, 1.90f * s, 0.62f * s), darkSteelMat);
            PartCube("ArmoredTorso", go.transform, new Vector3(0f, 2.55f * s, 0f), new Vector3(1.85f * s, 1.65f * s, 1.20f * s), index == 0 ? whiteTrussMat : darkSteelMat);
            PartCube("CherenkovReactorHeart", go.transform, new Vector3(0f, 2.60f * s, 0.62f * s), new Vector3(0.78f * s, 0.85f * s, 0.14f * s), cyanGlowMat);
            PartCube("BladeArmL", go.transform, new Vector3(-1.32f * s, 2.35f * s, 0.45f * s), new Vector3(0.42f * s, 1.75f * s, 0.95f * s), hazardYellowMat);
            PartCube("BladeArmR", go.transform, new Vector3(1.32f * s, 2.35f * s, 0.45f * s), new Vector3(0.42f * s, 1.75f * s, 0.95f * s), cyanGlowMat);
            PartCube("CrownHead", go.transform, new Vector3(0f, 3.72f * s, 0.10f * s), new Vector3(0.88f * s, 0.72f * s, 0.82f * s), darkSteelMat);
            PartCube("CrownEye", go.transform, new Vector3(0f, 3.76f * s, 0.54f * s), new Vector3(0.68f * s, 0.18f * s, 0.08f * s), redBeaconMat);

            var lGo = new GameObject("BossCherenkovAura");
            lGo.transform.SetParent(go.transform, false);
            lGo.transform.localPosition = new Vector3(0f, 2.8f * s, 0.8f);
            var lt = lGo.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = new Color(0.18f, 0.82f, 1.0f);
            lt.range = 18f;
            lt.intensity = 3.6f;
            return go;
        }
    }
}
