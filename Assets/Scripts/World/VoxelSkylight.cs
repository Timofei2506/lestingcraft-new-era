using System;
using System.Collections.Generic;

namespace LestingCraft.World
{
    /// <summary>
    /// Dynamic 0..15 sky light, not camera-depth fog or constant underground ambient.
    /// Unknown chunks are dark boundaries; registration joins the propagation queues.
    ///
    /// 2.0 performance pass — the propagation semantics are unchanged (the headless
    /// <c>Tools/check_nature_core.cs</c> suite still runs the sealed-cave, seam-join,
    /// unload and negative-coordinate cases), but three hot costs are gone:
    ///  • a one-entry chunk cache removes ~6 dictionary lookups per processed voxel;
    ///  • <see cref="CopyVolume"/> resolves eight row pointers per Z row instead of one
    ///    lookup per column (1.57M lookups per upload become ~8K);
    ///  • <see cref="Changed"/> now fires once per touched chunk per <see cref="Process"/>
    ///    call instead of once per changed voxel. Consumers only mark chunks dirty, so
    ///    deduplicating is behaviour-preserving and removes up to 32K callbacks/frame.
    /// </summary>
    public sealed class VoxelSkylight
    {
        struct Cell : IEquatable<Cell>
        {
            public int x, y, z;
            public Cell(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
            public bool Equals(Cell b) => x == b.x && y == b.y && z == b.z;
            public override bool Equals(object b) => b is Cell && Equals((Cell)b);
            public override int GetHashCode() { unchecked { return x * 734287 ^ y * 912931 ^ z * 438289; } }
        }

        sealed class Field
        {
            public int x, z;
            public ChunkData Data;
            public readonly byte[] Sky = new byte[65536], Source = new byte[65536];
        }

        readonly Dictionary<long, Field> fields = new Dictionary<long, Field>();
        readonly Queue<Cell> queue = new Queue<Cell>();
        readonly HashSet<Cell> queued = new HashSet<Cell>();
        readonly HashSet<long> changedKeys = new HashSet<long>();

        // One-entry resolver cache: propagation almost always walks inside one chunk.
        long cachedKey = long.MinValue;
        Field cachedField;

        public Action<int, int> Changed;
        public int Pending => queue.Count;
        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
        static int Chunk(int x) => x >= 0 ? x / 16 : (x - 15) / 16;

        Field FieldFor(int cx, int cz)
        {
            long key = Key(cx, cz);
            if (key == cachedKey) return cachedField;
            fields.TryGetValue(key, out Field resolved);
            cachedKey = key; cachedField = resolved;
            return resolved;
        }

        bool Find(int x, int y, int z, out Field field, out int index)
        {
            int cx = Chunk(x), cz = Chunk(z);
            index = y >= 0 && y < 256 ? ChunkData.IndexOf(x - cx * 16, y, z - cz * 16) : 0;
            if (y < 0 || y >= 256) { field = null; return false; }
            field = FieldFor(cx, cz);
            return field != null;
        }

        public byte Get(int x, int y, int z)
        {
            if (y >= 256) return 15;
            if (y < 0) return 0;
            return Find(x, y, z, out var f, out int i) ? f.Sky[i] : (byte)0;
        }

        public static int Loss(ushort id) => id == 0 || id == 15 || id == 5 ? 1 : id == 7 ? 2 : id == 44 ? 1 : id == 11 ? 3 : 16;

        void Add(int x, int y, int z)
        {
            if (!Find(x, y, z, out var f, out _)) return;
            var c = new Cell(x, y, z);
            if (queued.Add(c)) queue.Enqueue(c);
        }

        void Around(int x, int y, int z)
        {
            Add(x, y, z); Add(x + 1, y, z); Add(x - 1, y, z);
            Add(x, y + 1, z); Add(x, y - 1, z);
            Add(x, y, z + 1); Add(x, y, z - 1);
        }

        void Column(Field f, int x, int z, bool initial)
        {
            int sky = 15;
            for (int y = 255; y >= 0; y--)
            {
                int i = ChunkData.IndexOf(x, y, z);
                ushort id = f.Data.GetBlock(x, y, z);
                int loss = Loss(id);
                if (loss >= 16) sky = 0;
                else if (loss > 1 || id == 5) sky = Math.Max(0, sky - loss);
                byte level = (byte)sky;
                if (initial) { f.Source[i] = level; f.Sky[i] = level; }
                else if (f.Source[i] != level) { f.Source[i] = level; Around(f.x * 16 + x, y, f.z * 16 + z); }
            }
        }

        public void Register(int cx, int cz, ChunkData data)
        {
            var f = new Field { x = cx, z = cz, Data = data };
            fields[Key(cx, cz)] = f;
            cachedKey = long.MinValue; cachedField = null;
            for (int z = 0; z < 16; z++)
                for (int x = 0; x < 16; x++)
                    Column(f, x, z, true);
            // Single strided pass over the raw array: no bounds checks, no virtual calls.
            ushort[] blocks = data.RawBlocks;
            for (int y = 0; y < 256; y++)
            {
                int layer = y * 256;
                for (int z = 0; z < 16; z++)
                {
                    int row = layer + z * 16;
                    for (int x = 0; x < 16; x++)
                    {
                        int i = row + x;
                        if (f.Source[i] < 15 && Loss(blocks[i]) < 16) Add(cx * 16 + x, y, cz * 16 + z);
                    }
                }
            }
            Border(cx, cz);
            Changed?.Invoke(cx, cz);
        }

        void Border(int cx, int cz)
        {
            for (int y = 0; y < 256; y++)
                for (int a = 0; a < 16; a++)
                {
                    Add(cx * 16 - 1, y, cz * 16 + a); Add(cx * 16 + 16, y, cz * 16 + a);
                    Add(cx * 16 + a, y, cz * 16 - 1); Add(cx * 16 + a, y, cz * 16 + 16);
                }
        }

        public void Remove(int cx, int cz)
        {
            fields.Remove(Key(cx, cz));
            cachedKey = long.MinValue; cachedField = null;
            Border(cx, cz);
        }

        public void Edit(int x, int y, int z)
        {
            if (!Find(x, y, z, out var f, out _)) return;
            Column(f, x - f.x * 16, z - f.z * 16, false);
            Around(x, y, z);
        }

        public void CopyVolume(byte[] pixels, int ox, int oy, int oz, int width, int height, int depth)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            int chunksPerRow = (width + 15) / 16 + 2;
            var rowFields = new Field[chunksPerRow];
            for (int z = 0; z < depth; z++)
            {
                int wz = oz + z, cz = Chunk(wz), lz = wz - cz * 16;
                // Resolve this Z row's chunk fields once (at most eight per row).
                int firstCx = Chunk(ox) - 1;
                for (int k = 0; k < chunksPerRow; k++)
                {
                    if (!fields.TryGetValue(Key(firstCx + k, cz), out var f)) { rowFields[k] = null; continue; }
                    if (lz < 0 || lz > 15) { rowFields[k] = null; continue; }
                    rowFields[k] = f;
                }
                for (int y = 0; y < height; y++)
                {
                    int wy = oy + y;
                    int rowStart = width * (y + height * z);
                    if (wy >= 256)
                    {
                        for (int x = 0; x < width; x++) pixels[rowStart + x] = 255;
                        continue;
                    }
                    if (wy < 0)
                    {
                        for (int x = 0; x < width; x++) pixels[rowStart + x] = 0;
                        continue;
                    }
                    int sourceRow = wy * 256 + lz * 16;
                    for (int x = 0; x < width; x++)
                    {
                        int wx = ox + x, cx = Chunk(wx);
                        Field f = rowFields[cx - firstCx];
                        pixels[rowStart + x] = f == null ? (byte)0 : (byte)(f.Sky[sourceRow + (wx - cx * 16)] * 17);
                    }
                }
            }
        }

        // Main-thread immutable snapshot copy. Only nine dictionary lookups per chunk,
        // rather than a lookup for each of the 82,944 padded voxels.
        public void CopyPaddedChunk(int cx, int cz, byte[] destination)
        {
            if (destination == null || destination.Length != 18 * 256 * 18) throw new ArgumentException("Invalid padded skylight buffer");
            Array.Clear(destination, 0, destination.Length);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!fields.TryGetValue(Key(cx + dx, cz + dz), out var f)) continue;
                    int x0 = dx < 0 ? -1 : dx > 0 ? 16 : 0, x1 = dx == 0 ? 15 : x0;
                    int z0 = dz < 0 ? -1 : dz > 0 ? 16 : 0, z1 = dz == 0 ? 15 : z0;
                    for (int y = 0; y < 256; y++)
                        for (int z = z0; z <= z1; z++)
                            Array.Copy(f.Sky, ChunkData.IndexOf(x0 - dx * 16, y, z - dz * 16),
                                destination, x0 + 1 + 18 * (z + 1 + 18 * y), x1 - x0 + 1);
                }
        }

        public int Process(int budget = 32768)
        {
            int processed = 0;
            changedKeys.Clear();
            while (queue.Count > 0 && processed++ < budget)
            {
                var p = queue.Dequeue();
                queued.Remove(p);
                if (!Find(p.x, p.y, p.z, out var f, out int i)) continue;
                int loss = Loss(f.Data.GetBlock(p.x - f.x * 16, p.y, p.z - f.z * 16)), level = f.Source[i];
                if (loss < 16)
                {
                    level = Math.Max(level, Get(p.x + 1, p.y, p.z) - loss);
                    level = Math.Max(level, Get(p.x - 1, p.y, p.z) - loss);
                    level = Math.Max(level, Get(p.x, p.y + 1, p.z) - loss);
                    level = Math.Max(level, Get(p.x, p.y - 1, p.z) - loss);
                    level = Math.Max(level, Get(p.x, p.y, p.z + 1) - loss);
                    level = Math.Max(level, Get(p.x, p.y, p.z - 1) - loss);
                }
                else level = 0;
                byte next = (byte)Math.Max(0, level);
                if (f.Sky[i] == next) continue;
                f.Sky[i] = next;
                changedKeys.Add(Key(f.x, f.z));
                Around(p.x, p.y, p.z);
            }
            // One notification per touched chunk: consumers only mark a chunk for re-meshing.
            if (changedKeys.Count != 0 && Changed != null)
            {
                var notify = Changed;
                foreach (long key in changedKeys) notify((int)(key >> 32), (int)(key & 0xffffffff));
            }
            return processed;
        }
    }
}
