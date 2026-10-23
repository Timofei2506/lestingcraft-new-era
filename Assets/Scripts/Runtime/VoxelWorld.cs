using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using LestingCraft.World;

namespace LestingCraft.Runtime
{
    public sealed class VoxelWorld : MonoBehaviour
    {
        sealed class View
        {
            public ChunkData Data;
            public GameObject Object, Specials;
            public Mesh Visual, Collision;
            public MeshFilter Filter;
            public MeshCollider Collider;
            public readonly Dictionary<Vector3Int, ushort> SpecialCells = new Dictionary<Vector3Int, ushort>();
            public readonly List<Vector3> Fires = new List<Vector3>();
            public readonly List<Collider> SpecialColliders = new List<Collider>();
            public bool SpecialsDirty = true, CollisionReady, Near;
            public JobHandle Bake;
            public bool Baking;
        }

        struct BakeJob : IJob
        {
            public int MeshId;
            public void Execute() { Physics.BakeMesh(MeshId, false); }
        }

        // Unity-owned world/skylight/fluid state is sampled only on the main thread.
        // The worker sees private arrays and never touches dictionaries or Unity objects.
        sealed class Snapshot
        {
            public readonly ChunkData Chunk = new ChunkData(new ushort[ChunkData.BlockCount]);
            public readonly ushort[] Blocks = new ushort[18 * 256 * 18];
            public readonly byte[] Lights = new byte[18 * 256 * 18];
            public readonly float[] Heights = new float[18 * 256 * 18];
            public int X, Z;
            public static int Index(int x, int y, int z) { return x + 1 + 18 * (z + 1 + 18 * y); }
            public ushort Block(int x, int y, int z)
            {
                if (y < 0 || y >= 256) return 0;
                return Blocks[Index(x - X, y, z - Z)];
            }
            public byte Light(int x, int y, int z)
            {
                if (y < 0) return 0;
                if (y >= 256) return 15;
                return Lights[Index(x - X, y, z - Z)];
            }
            public float Height(int x, int y, int z)
            {
                if (y < 0 || y >= 256) return 1;
                return Heights[Index(x - X, y, z - Z)];
            }
        }

        readonly Dictionary<Vector2Int, View> chunks = new Dictionary<Vector2Int, View>();
        readonly Dictionary<Vector3Int, ushort> edits = new Dictionary<Vector3Int, ushort>();
        readonly HashSet<Vector2Int> dirty = new HashSet<Vector2Int>();
        readonly Stopwatch frameWatch = new Stopwatch();
        readonly List<Vector2Int> remove = new List<Vector2Int>();
        Material material, waterMaterial, grassMaterial, objectMaterial, crystalMaterial, glassMaterial;
        NativeArray<ushort> pendingBuffer;
        JobHandle pendingJob;
        IEnumerator work;
        Task<ChunkMeshData> meshTask;
        int detailQuality, editRevision, budgetFrame = -1;
        bool destroying;
        const double BudgetMilliseconds = 4.0;

        public int ContentVersion { get; private set; }
        public int Seed { get; private set; }
        public Dimension Dimension { get; private set; }
        public bool Online { get; private set; }
        public bool Ready { get; private set; }
        public int LoadedChunks { get { return chunks.Count; } }
        public Transform Player;
        public VoxelSkylight Skylight { get; } = new VoxelSkylight();
        public Func<int,int,int,float> FluidHeight;
        LestingCraft.Presentation.VoxelSkyVolume skyVolume;
        int Radius { get { return LestingCraft.Presentation.VisualSettings.Current.ViewDistance; } }

        public void Initialize(int seed, Dimension dimension, bool online, int contentVersion = 7)
        {
            Seed = seed; ContentVersion = contentVersion; Dimension = dimension; Online = online;
            detailQuality = LestingCraft.Presentation.VisualSettings.Current.Preset;
            crystalMaterial = new Material(Resources.Load<Material>("Generated/Crystal"));
            glassMaterial = new Material(Resources.Load<Material>("Generated/Glass"));
            objectMaterial = new Material(Resources.Load<Material>("Generated/VoxelObjects"));
            material = new Material(Resources.Load<Material>("Generated/VoxelPBR"));
            waterMaterial = new Material(Resources.Load<Material>("Generated/Water"));
            grassMaterial = new Material(Resources.Load<Material>("Generated/Grass"));
            RenderSettings.fog = false;
            RenderSettings.fogMode = FogMode.Linear;
            skyVolume = gameObject.AddComponent<LestingCraft.Presentation.VoxelSkyVolume>();
            skyVolume.World = this;
            Skylight.Changed = (x, z) =>
            {
                dirty.Add(new Vector2Int(x, z));
                if (skyVolume) skyVolume.InvalidateStreaming();
            };
        }

        public static int ChunkCoordinate(int value) { return Mathf.FloorToInt(value / 16f); }
        public bool Contains(int x, int y, int z)
        {
            int bound = ContentVersion >= 4 ? 384 : 64;
            return y >= 0 && y < 256 && Mathf.Abs(x) < 32000 && Mathf.Abs(z) < 32000 &&
                (!Online || (x >= -bound && x < bound && z >= -bound && z < bound));
        }
        public bool Loaded(Vector3 p)
        {
            return chunks.ContainsKey(new Vector2Int(ChunkCoordinate(Mathf.FloorToInt(p.x)),
                ChunkCoordinate(Mathf.FloorToInt(p.z))));
        }
        public ushort GetBlock(int x, int y, int z)
        {
            if (!Contains(x, y, z)) return 0;
            ushort value;
            if (edits.TryGetValue(new Vector3Int(x, y, z), out value)) return value;
            var key = new Vector2Int(ChunkCoordinate(x), ChunkCoordinate(z));
            View view;
            if (chunks.TryGetValue(key, out view)) return view.Data.GetBlock(x - key.x * 16, y, z - key.y * 16);
            return TerrainGenerator.SampleContent(x, y, z, Seed, Dimension, ContentVersion);
        }
        public bool InFluid(Vector3 point, int fluid)
        {
            var p = Vector3Int.FloorToInt(point);
            if (GetBlock(p.x, p.y, p.z) != fluid) return false;
            float h = FluidHeight != null && GetBlock(p.x, p.y + 1, p.z) != fluid ? FluidHeight(p.x, p.y, p.z) : 1;
            return point.y - p.y < h;
        }
        public bool CanAcceptEdit(Vector3Int cell)
        {
            return edits.ContainsKey(cell) || edits.Count < (Online ? 4096 : 8192);
        }
        public bool IsIdleForShowcase
        {
            get { return Ready && dirty.Count == 0 && meshTask == null; }
        }
        public void ShowcasePumpSync()
        {
            if (detailQuality != LestingCraft.Presentation.VisualSettings.Current.Preset)
            {
                detailQuality = LestingCraft.Presentation.VisualSettings.Current.Preset;
                foreach (var key in chunks.Keys) dirty.Add(key);
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Vector2Int center = Center();
            while (sw.ElapsedMilliseconds < 120000)
            {
                Skylight.Process();
                foreach (var pair in chunks)
                {
                    View v = pair.Value;
                    if (v.Baking)
                    {
                        v.Bake.Complete();
                        v.Baking = false;
                        v.CollisionReady = true;
                        if (v.Near)
                        {
                            v.Collider.sharedMesh = v.Collision;
                            v.Collider.enabled = true;
                        }
                    }
                }
                if (work == null)
                {
                    Vector2Int key;
                    if (FindDirty(center, out key)) work = Rebuild(key, chunks[key]);
                    else if (FindMissing(center, out key)) work = Generate(key);
                    else
                    {
                        Ready = true;
                        break;
                    }
                }
                if (work != null)
                {
                    if (pendingBuffer.IsCreated) pendingJob.Complete();
                    if (meshTask != null) meshTask.Wait();
                    if (!work.MoveNext())
                    {
                        var disposable = work as IDisposable;
                        if (disposable != null) disposable.Dispose();
                        work = null;
                    }
                }
            }
        }
        public void ShowcaseEditUnbounded(int x, int y, int z, ushort block)
        {
            if (!Contains(x, y, z) || y == 0 || block == BlockRegistry.BedrockId) return;
            if (GetBlock(x, y, z) == block) return;
            var pos = new Vector3Int(x, y, z);
            edits[pos] = block;
            editRevision++;
            var key = new Vector2Int(ChunkCoordinate(x), ChunkCoordinate(z));
            View view;
            if (chunks.TryGetValue(key, out view))
            {
                var local = new Vector3Int(x - key.x * 16, y, z - key.y * 16);
                view.Data.SetBlock(local.x, y, local.z, block);
                if (BlockRegistry.IsCustomPlacedModel(block)) view.SpecialCells[local] = block;
                else view.SpecialCells.Remove(local);
                view.SpecialsDirty = true;
            }
            Skylight.Edit(x, y, z);
            if (skyVolume) skyVolume.Invalidate();
            dirty.Add(key);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx < 0 && x - key.x * 16 != 0 || dx > 0 && x - key.x * 16 != 15) continue;
                    if (dz < 0 && z - key.y * 16 != 0 || dz > 0 && z - key.y * 16 != 15) continue;
                    var affected = key + new Vector2Int(dx, dz);
                    dirty.Add(affected);
                    View adjacent;
                    if (chunks.TryGetValue(affected, out adjacent)) adjacent.SpecialsDirty = true;
                }
        }
        public void Edit(int x, int y, int z, ushort block)
        {
            Edit(x, y, z, block, true);
        }

        public void Edit(int x, int y, int z, ushort block, bool rebuildLight)
        {
            int maxAllowed = Online && ContentVersion < 2 ? 18 :
                ContentVersion >= 8 ? LestingCraft.Gameplay.ExpandedContent.MaxBlock :
                ContentVersion >= 7 ? 53 :
                ContentVersion >= 5 ? 48 : 46;
            if (!Contains(x, y, z) || y == 0 || block == BlockRegistry.BedrockId || block > maxAllowed) return;
            var pos = new Vector3Int(x, y, z);
            if (!CanAcceptEdit(pos)) return;
            edits[pos] = block;
            editRevision++;
            var key = new Vector2Int(ChunkCoordinate(x), ChunkCoordinate(z));
            View view;
            if (chunks.TryGetValue(key, out view))
            {
                var local = new Vector3Int(x - key.x * 16, y, z - key.y * 16);
                view.Data.SetBlock(local.x, y, local.z, block);
                if (BlockRegistry.IsCustomPlacedModel(block)) view.SpecialCells[local] = block;
                else view.SpecialCells.Remove(local);
                view.SpecialsDirty = true;
            }
            if (rebuildLight)
            {
                Skylight.Edit(x, y, z);
                if (skyVolume) skyVolume.Invalidate();
            }
            // Corner AO and vertex skylight also depend on diagonal chunks.
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx < 0 && x - key.x * 16 != 0 || dx > 0 && x - key.x * 16 != 15) continue;
                    if (dz < 0 && z - key.y * 16 != 0 || dz > 0 && z - key.y * 16 != 15) continue;
                    var affected = key + new Vector2Int(dx, dz);
                    dirty.Add(affected);
                    View adjacent;
                    if (chunks.TryGetValue(affected, out adjacent)) adjacent.SpecialsDirty = true;
                }
        }

        public void FlushColumnSkylight(int x, int y, int z)
        {
            Skylight.Edit(x, y, z);
            if (skyVolume) skyVolume.Invalidate();
        }

        Vector2Int Center()
        {
            return Player ? new Vector2Int(ChunkCoordinate(Mathf.FloorToInt(Player.position.x)),
                ChunkCoordinate(Mathf.FloorToInt(Player.position.z))) : Vector2Int.zero;
        }

        bool Near(Vector2Int key, Vector2Int center)
        {
            if (Mathf.Abs(key.x - center.x) > 2 || Mathf.Abs(key.y - center.y) > 2) return false;
            if (!Player) return true;
            Vector3 p = Player.position;
            float dx = Mathf.Max(key.x * 16 - p.x, 0, p.x - (key.x * 16 + 16));
            float dz = Mathf.Max(key.y * 16 - p.z, 0, p.z - (key.y * 16 + 16));
            return dx * dx + dz * dz <= 32 * 32;
        }

        bool BudgetExpired { get { return frameWatch.Elapsed.TotalMilliseconds >= BudgetMilliseconds; } }

        void LateUpdate()
        {
            if (destroying || !material) return;
            if (budgetFrame != Time.frameCount)
            {
                budgetFrame = Time.frameCount;
                frameWatch.Restart();
            }
            // This is a cooperative CPU budget. Unity mesh upload, object creation and
            // Skylight.Process are indivisible APIs; a stopwatch cannot preempt them.
            // Profiling on target hardware is required before claiming a hard frame bound.
            Skylight.Process();
            if (detailQuality != LestingCraft.Presentation.VisualSettings.Current.Preset)
            {
                detailQuality = LestingCraft.Presentation.VisualSettings.Current.Preset;
                foreach (var key in chunks.Keys) dirty.Add(key);
            }
            Vector2Int center = Center();
            foreach (var pair in chunks)
            {
                View v = pair.Value;
                bool near = Near(pair.Key, center);
                if (v.Near != near)
                {
                    v.Near = near;
                    foreach (Collider collider in v.SpecialColliders)
                        if (collider) collider.enabled = near;
                    if (near && !v.CollisionReady && !v.Baking) dirty.Add(pair.Key);
                }
                if (v.Baking && v.Bake.IsCompleted)
                {
                    v.Bake.Complete();
                    v.Baking = false;
                    v.CollisionReady = true;
                    if (near)
                    {
                        v.Collider.sharedMesh = v.Collision;
                        v.Collider.enabled = true;
                    }
                }
                if (!near)
                {
                    v.Collider.enabled = false;
                    v.Collider.sharedMesh = null;
                }
                else if (v.CollisionReady && !v.Baking && v.Collider.sharedMesh != v.Collision)
                {
                    v.Collider.sharedMesh = v.Collision;
                    v.Collider.enabled = true;
                }
            }
            RenderSettings.fogStartDistance = Mathf.Max(18, Radius * 10);
            RenderSettings.fogEndDistance = Radius * 16;
            if (BudgetExpired) return;
            // Spend the whole per-frame streaming budget: when one chunk step
            // finishes, start the next in the same frame while budget remains.
            // Previously a single MoveNext ran per frame, so streaming throughput
            // was capped by frame rate and most of the budget went idle. The
            // budget itself is only slightly wider (3 -> 4 ms) and still bounds
            // main-thread chunk work per frame.
            while (!BudgetExpired)
            {
                if (work == null)
                {
                    Vector2Int key;
                    if (FindDirty(center, out key)) work = Rebuild(key, chunks[key]);
                    else if (FindMissing(center, out key)) work = Generate(key);
                    else
                    {
                        Ready = true;
                        remove.Clear();
                        foreach (var pair in chunks)
                            if (Mathf.Abs(pair.Key.x - center.x) > Radius + 1 ||
                                Mathf.Abs(pair.Key.y - center.y) > Radius + 1) remove.Add(pair.Key);
                        if (remove.Count != 0)
                        {
                            key = remove[0];
                            View v = chunks[key];
                            if (!v.Baking || v.Bake.IsCompleted)
                            {
                                DisposeView(v);
                                chunks.Remove(key);
                                dirty.Remove(key);
                                Skylight.Remove(key.x, key.y);
                                if (skyVolume) skyVolume.InvalidateStreaming();
                            }
                        }
                        break;
                    }
                }
                bool advanced;
                try { advanced = work.MoveNext(); }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, this);
                    var disposable = work as IDisposable;
                    if (disposable != null) disposable.Dispose();
                    work = null;
                    break;
                }
                if (advanced) break; // yielded: waiting on a job/task or on the budget
                var finished = work as IDisposable;
                if (finished != null) finished.Dispose();
                work = null;
            }
        }

        bool FindDirty(Vector2Int center, out Vector2Int result)
        {
            result = default(Vector2Int);
            int best = int.MaxValue;
            foreach (var key in dirty)
            {
                View v;
                if (!chunks.TryGetValue(key, out v) || v.Baking) continue;
                int distance = (key - center).sqrMagnitude;
                if (distance < best) { best = distance; result = key; }
            }
            return best != int.MaxValue;
        }

        bool FindMissing(Vector2Int center, out Vector2Int result)
        {
            for (int ring = 0; ring <= Radius; ring++)
                for (int dz = -ring; dz <= ring; dz++)
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;
                        var key = center + new Vector2Int(dx, dz);
                        if (!chunks.ContainsKey(key) && Contains(key.x * 16, 1, key.y * 16))
                        {
                            result = key;
                            return true;
                        }
                    }
            result = default(Vector2Int);
            return false;
        }

        IEnumerator Generate(Vector2Int key)
        {
            pendingBuffer = new NativeArray<ushort>(ChunkData.BlockCount, Allocator.Persistent);
            var job = new GenerateChunkJob
            {
                OriginX = key.x * 16, OriginZ = key.y * 16, Seed = Seed,
                Dimension = Dimension, ContentVersion = ContentVersion, Blocks = pendingBuffer
            };
            pendingJob = job.Schedule(256, 16);
            while (!pendingJob.IsCompleted) yield return null;
            pendingJob.Complete();
            var data = new ChunkData(pendingBuffer.ToArray());
            pendingBuffer.Dispose();
            pendingBuffer = default(NativeArray<ushort>);
            var v = new View { Data = data };
            // Record special blocks once, incrementally, rather than scanning during rebuilds.
            for (int y = 0; y < 256; y++)
            {
                for (int z = 0; z < 16; z++)
                    for (int x = 0; x < 16; x++)
                    {
                        ushort id = data.GetBlock(x, y, z);
                        if (BlockRegistry.IsCustomPlacedModel(id)) v.SpecialCells[new Vector3Int(x, y, z)] = id;
                    }
                if (BudgetExpired) yield return null;
            }
            foreach (var edit in edits)
                if (ChunkCoordinate(edit.Key.x) == key.x && ChunkCoordinate(edit.Key.z) == key.y)
                {
                    var local = new Vector3Int(edit.Key.x - key.x * 16, edit.Key.y, edit.Key.z - key.y * 16);
                    data.SetBlock(local.x, local.y, local.z, edit.Value);
                    if (BlockRegistry.IsCustomPlacedModel(edit.Value)) v.SpecialCells[local] = edit.Value;
                    else v.SpecialCells.Remove(local);
                }
            if (BudgetExpired) yield return null;
            v.Object = new GameObject("Chunk " + key.x + " " + key.y);
            v.Object.transform.SetParent(transform, false);
            v.Object.transform.localPosition = new Vector3(key.x * 16, 0, key.y * 16);
            v.Filter = v.Object.AddComponent<MeshFilter>();
            var renderer = v.Object.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { material, waterMaterial, grassMaterial, glassMaterial };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            v.Collider = v.Object.AddComponent<MeshCollider>();
            v.Collider.enabled = false;
            v.Near = Near(key, Center());
            chunks.Add(key, v);
            Skylight.Register(key.x, key.y, data);
            dirty.Add(key);
            if (skyVolume) skyVolume.InvalidateStreaming();
        }

        IEnumerator Rebuild(Vector2Int key, View v)
        {
            dirty.Remove(key);
            int revision = editRevision, quality = detailQuality;
            var snapshot = new Snapshot { X = key.x * 16, Z = key.y * 16 };
            Func<int,int,int,float> fluid = FluidHeight;
            for (int y = 0; y < 256; y++)
            {
                for (int z = -1; z <= 16; z++)
                {
                    for (int x = -1; x <= 16; x++)
                    {
                        int wx = snapshot.X + x, wz = snapshot.Z + z;
                        int index = Snapshot.Index(x, y, z);
                        ushort id = x >= 0 && x < 16 && z >= 0 && z < 16
                            ? v.Data.GetBlock(x, y, z) : GetBlock(wx, y, wz);
                        snapshot.Blocks[index] = id;
                        snapshot.Lights[index] = Skylight.Get(wx, y, wz);
                        snapshot.Heights[index] = fluid != null && (id == 7 || id == 8) ? fluid(wx, y, wz) : 1;
                        if (x >= 0 && x < 16 && z >= 0 && z < 16) snapshot.Chunk.SetBlock(x, y, z, id);
                    }
                    if (BudgetExpired) yield return null;
                }
            }
            if (revision != editRevision || dirty.Contains(key))
            {
                dirty.Add(key);
                yield break;
            }
            int seed = Seed;
            bool decorate = Dimension == LestingCraft.World.Dimension.Lestingia;
            meshTask = Task.Run(() => ChunkMesher.Build(snapshot.Chunk, snapshot.X, snapshot.Z,
                snapshot.Block, seed, decorate, quality, snapshot.Light,
                fluid == null ? (Func<int,int,int,float>)null : snapshot.Height, true));
            while (!meshTask.IsCompleted) yield return null;
            Task<ChunkMeshData> completed = meshTask;
            meshTask = null;
            if (completed.IsFaulted)
            {
                dirty.Add(key);
                throw completed.Exception;
            }
            ChunkMeshData mesh = completed.Result;
            if (revision != editRevision || quality != detailQuality || dirty.Contains(key))
            {
                dirty.Add(key);
                yield break;
            }
            if (BudgetExpired) yield return null;
            if (!v.Visual) v.Visual = new Mesh { name = "Chunk visual " + key };
            mesh.WriteMesh(v.Visual);
            v.Filter.sharedMesh = v.Visual;
            yield return null;
            // Detach before modifying a reused collision mesh. Never mutate it during BakeMesh.
            v.Collider.enabled = false;
            v.Collider.sharedMesh = null;
            v.CollisionReady = false;
            if (v.Near && mesh.ColliderTriangles.Count != 0)
            {
                if (!v.Collision) v.Collision = new Mesh { name = "Chunk collision " + key };
                mesh.WriteMesh(v.Collision, true);
                v.Bake = new BakeJob { MeshId = v.Collision.GetInstanceID() }.Schedule();
                v.Baking = true;
            }
            if (v.SpecialsDirty)
            {
                yield return null;
                RebuildSpecials(key, v);
            }
        }

        void RebuildSpecials(Vector2Int key, View v)
        {
            if (v.Specials)
            {
                v.Specials.SetActive(false);
                Destroy(v.Specials);
            }
            v.Specials = new GameObject("Authored placed objects");
            v.Specials.transform.SetParent(v.Object.transform, false);
            v.Fires.Clear();
            v.SpecialColliders.Clear();
            var crystals = new List<Vector3>();
            foreach (var pair in v.SpecialCells)
            {
                Vector3Int p = pair.Key;
                if (pair.Value == 15)
                {
                    LestingCraft.Presentation.PlacedVoxelModel.Create(15, v.Specials.transform, p, objectMaterial);
                    v.Fires.Add(new Vector3(key.x * 16 + p.x + .5f, p.y + .42f, key.y * 16 + p.z + .5f));
                }
                else if (pair.Value == BlockRegistry.TorchBlockId || pair.Value == BlockRegistry.BedBlockId || pair.Value == BlockRegistry.LanternBlockId)
                {
                    var placedObj = LestingCraft.Presentation.PlacedVoxelModel.Create(pair.Value, v.Specials.transform, p, objectMaterial);
                    if (pair.Value == BlockRegistry.TorchBlockId || pair.Value == BlockRegistry.LanternBlockId)
                    {
                        var lampGo = new GameObject(pair.Value == BlockRegistry.LanternBlockId ? "LanternLight" : "TorchLight");
                        lampGo.transform.SetParent(placedObj.transform, false);
                        lampGo.transform.localPosition = new Vector3(0, .62f, 0);
                        var light = lampGo.AddComponent<Light>();
                        light.type = LightType.Point;
                        light.color = pair.Value == BlockRegistry.LanternBlockId ? new Color(1f, .84f, .52f) : new Color(1f, .72f, .34f);
                        light.intensity = pair.Value == BlockRegistry.LanternBlockId ? 1.55f : 1.25f;
                        light.range = pair.Value == BlockRegistry.LanternBlockId ? 10.5f : 8.5f;
                        light.shadows = LightShadows.None;
                        v.Fires.Add(new Vector3(key.x * 16 + p.x + .5f, p.y + .6f, key.y * 16 + p.z + .5f));
                    }
                }
                else
                {
                    int x = key.x * 16 + p.x, z = key.y * 16 + p.z;
                    if (!BlockRegistry.Occludes(GetBlock(x + 1, p.y, z)) ||
                        !BlockRegistry.Occludes(GetBlock(x - 1, p.y, z)) ||
                        !BlockRegistry.Occludes(GetBlock(x, p.y + 1, z)) ||
                        !BlockRegistry.Occludes(GetBlock(x, p.y - 1, z)) ||
                        !BlockRegistry.Occludes(GetBlock(x, p.y, z + 1)) ||
                        !BlockRegistry.Occludes(GetBlock(x, p.y, z - 1))) crystals.Add(p);
                }
            }
            LestingCraft.Presentation.PlacedVoxelModel.CreateCrystalBatch(v.Specials.transform, crystals, crystalMaterial);
            v.Specials.GetComponentsInChildren<Collider>(true, v.SpecialColliders);
            foreach (var collider in v.SpecialColliders) collider.enabled = v.Near;
            v.SpecialsDirty = false;
        }

        public LestingCraft.Gameplay.SavedBlock[] ExportEdits()
        {
            var result = new List<LestingCraft.Gameplay.SavedBlock>();
            foreach (var edit in edits)
                result.Add(new LestingCraft.Gameplay.SavedBlock
                {
                    realm = (int)Dimension, x = edit.Key.x, y = edit.Key.y, z = edit.Key.z, block = edit.Value
                });
            return result.ToArray();
        }
        public LestingCraft.Gameplay.SavedBlock[] ExportAllMergedEdits(LestingCraft.Gameplay.SavedBlock[] existingAllRealms)
        {
            var result = new List<LestingCraft.Gameplay.SavedBlock>();
            if (existingAllRealms != null)
            {
                int currentRealm = (int)Dimension;
                for (int i = 0; i < existingAllRealms.Length; i++)
                {
                    var b = existingAllRealms[i];
                    if (b != null && b.realm != currentRealm) result.Add(b);
                }
            }
            foreach (var edit in edits)
                result.Add(new LestingCraft.Gameplay.SavedBlock
                {
                    realm = (int)Dimension, x = edit.Key.x, y = edit.Key.y, z = edit.Key.z, block = edit.Value
                });
            return result.ToArray();
        }
        public void SwitchPlanet(Dimension targetDimension, LestingCraft.Gameplay.SavedBlock[] allSavedBlocks = null)
        {
            var disposable = work as IDisposable;
            if (disposable != null) disposable.Dispose();
            work = null;
            pendingJob.Complete();
            if (pendingBuffer.IsCreated) pendingBuffer.Dispose();
            if (meshTask != null)
            {
                try { meshTask.Wait(250); } catch { }
                meshTask = null;
            }
            foreach (var pair in chunks)
            {
                Skylight.Remove(pair.Key.x, pair.Key.y);
                DisposeView(pair.Value);
            }
            chunks.Clear();
            dirty.Clear();
            edits.Clear();
            editRevision++;
            Dimension = targetDimension;
            Ready = false;
            if (allSavedBlocks != null)
            {
                int targetRealm = (int)targetDimension;
                for (int i = 0; i < allSavedBlocks.Length; i++)
                {
                    var e = allSavedBlocks[i];
                    if (e != null && e.realm == targetRealm && Contains(e.x, e.y, e.z))
                        edits[new Vector3Int(e.x, e.y, e.z)] = (ushort)e.block;
                }
            }
            RenderSettings.fog = false;
            if (skyVolume) skyVolume.Invalidate();
        }
        public bool NearCampfire(Vector3 point, float radius = 6)
        {
            float squared = radius * radius;
            foreach (var view in chunks.Values)
                foreach (var p in view.Fires)
                    if ((point - p).sqrMagnitude < squared) return true;
            return false;
        }
        public List<Vector3> NearbyFires(Vector3 point)
        {
            var result = new List<Vector3>();
            foreach (var view in chunks.Values)
                foreach (var p in view.Fires)
                    if ((point - p).sqrMagnitude < 24 * 24) result.Add(p);
            return result;
        }

        void DisposeView(View v)
        {
            if (v.Baking) { v.Bake.Complete(); v.Baking = false; }
            if (v.Collider) v.Collider.sharedMesh = null;
            if (v.Visual) Destroy(v.Visual);
            if (v.Collision) Destroy(v.Collision);
            if (v.Object) Destroy(v.Object);
        }
        void OnDestroy()
        {
            destroying = true;
            var disposable = work as IDisposable;
            if (disposable != null) disposable.Dispose();
            work = null;
            pendingJob.Complete();
            if (pendingBuffer.IsCreated) pendingBuffer.Dispose();
            // A mesh worker owns only its snapshot. Observe faults without blocking teardown.
            if (meshTask != null)
                meshTask.ContinueWith(task => { var observed = task.Exception; },
                    TaskContinuationOptions.OnlyOnFaulted);
            Skylight.Changed = null;
            foreach (var v in chunks.Values) DisposeView(v);
            if (material) Destroy(material);
            if (waterMaterial) Destroy(waterMaterial);
            if (grassMaterial) Destroy(grassMaterial);
            if (objectMaterial) Destroy(objectMaterial);
            if (crystalMaterial) Destroy(crystalMaterial);
            if (glassMaterial) Destroy(glassMaterial);
        }
    }
}
