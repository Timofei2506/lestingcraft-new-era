using System;

namespace LestingCraft.World
{
    /// <summary>
    /// Procedural multi-sublevel liminal maze for Dimension.Backrooms (15):
    /// - Zone 0: Level 0 "The Lobby" (mustard wallpaper #90, damp carpet #91, asymmetric partition baffles, office cubicles)
    /// - Zone 1: Level 37 "The Poolrooms" (gleaming white porcelain marble #42, tiled colonnades, sunken warm pools #7)
    /// - Zone 2: Level 1 "Habitable Concrete Warehouse" (reinforced concrete #79, hazard steel #80, supply crates)
    /// - Zone 3: Level 6 "Lights Out" (pitch-dark slate #29 corridors with dead fluorescent fixtures)
    /// - Zone 4: Level "!" "Red Emergency Corridor" (crimson alarm halls #62 with hazard ribs)
    /// Pure deterministic C# with zero Unity dependencies.
    /// </summary>
    public static class BackroomsTerrain
    {
        public const int CeilingBottom = 72;
        public const int CeilingTop = 73;
        public const int WallTop = 71;

        public const ushort BedrockId = 9;
        public const ushort WallpaperId = 90;
        public const ushort CarpetId = 91;
        public const ushort PlasterId = 40;
        public const ushort MarbleTileId = 42;
        public const ushort SlateId = 29;
        public const ushort RedRoomId = 62;
        public const ushort ConcreteId = 79;
        public const ushort HazardSteelId = 80;
        public const ushort AsphaltId = 83;
        public const ushort LightId = 56;
        public const ushort FloodlightId = 77;
        public const ushort WaterId = 7;
        public const ushort PlanksId = 18;
        public const ushort WorkbenchId = 16;
        public const ushort BookshelfId = 57;
        public const ushort ExitBeaconId = 67;

        const int Cell = 8;
        const int ZoneCell = 48; // 6x6 rooms per sub-level sector

        static int Div(int a, int b) { return a >= 0 ? a / b : (a - b + 1) / b; }
        static int Mod(int a, int b) { int r = a % b; return r < 0 ? r + b : r; }

        static uint Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(seed ^ (x * 374761393) ^ (y * 668265263) ^ (z * 2147483647));
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        /// <summary>
        /// Sub-level zone at world (x, z):
        /// 0 = Level 0 (Lobby), 1 = Level 37 (Poolrooms), 2 = Level 1 (Warehouse),
        /// 3 = Level 6 (Lights Out), 4 = Level "!" (Red Alarm Sector).
        /// Central spawn area (|x| &lt; 36, |z| &lt; 36) is always classic Level 0 Lobby.
        /// </summary>
        public static int ZoneAt(int x, int z, int seed)
        {
            if (Math.Abs(x) < 36 && Math.Abs(z) < 36) return 0;
            int zx = Div(x, ZoneCell), zz = Div(z, ZoneCell);
            uint h = Hash(zx, 101, zz, seed ^ 0x5B3C);
            uint roll = h % 20u;
            if (roll < 9u) return 0;   // 45% Classic Level 0 Lobby
            if (roll < 13u) return 1;  // 20% Level 37 Poolrooms
            if (roll < 16u) return 2;  // 15% Level 1 Concrete Warehouse
            if (roll < 18u) return 3;  // 10% Level 6 Lights Out
            return 4;                  // 10% Level "!" Red Emergency Halls
        }

        public static string SublevelTitle(int zone)
        {
            switch (zone)
            {
                case 1: return "Уровень 37 · «Бассейны» (Poolrooms)";
                case 2: return "Уровень 1 · «Обитаемый склад»";
                case 3: return "Уровень 6 · «Полная темнота»";
                case 4: return "Уровень «!» · «Аварийный сектор»";
                default: return "Уровень 0 · «Жёлтое лобби»";
            }
        }

        public static bool IsRedRoom(int x, int z, int seed)
        {
            int zone = ZoneAt(x, z, seed);
            if (zone == 4) return true;
            int rx = Div(x, Cell), rz = Div(z, Cell);
            if (Math.Abs(rx) <= 2 && Math.Abs(rz) <= 2) return false;
            return (Hash(rx, 5, rz, seed) % 29u) == 0u;
        }

        public static bool IsDarkRoom(int x, int z, int seed)
        {
            int zone = ZoneAt(x, z, seed);
            if (zone == 3) return true;
            int rx = Div(x, Cell), rz = Div(z, Cell);
            if (Math.Abs(rx) <= 2 && Math.Abs(rz) <= 2) return false;
            return !IsRedRoom(x, z, seed) && (Hash(rx, 6, rz, seed) % 13u) == 0u;
        }

        public static int FloorTop(int x, int z, int seed)
        {
            if (Math.Abs(x) <= 24 && Math.Abs(z) <= 24) return 64;
            int zone = ZoneAt(x, z, seed);
            float n = MoonTerrain.ValueNoise(x * .009f, z * .009f, seed ^ 19391);
            int step = (int)Math.Round((n - .5f) * 10f);
            if (zone == 1) step = Math.Max(-3, Math.Min(2, step)); // Level Poolrooms stays gently terraced
            return 64 + Math.Max(-5, Math.Min(5, step));
        }

        static bool HasWall(int rx, int rz, bool vertical, int seed)
        {
            if (Math.Abs(rx) <= 1 && Math.Abs(rz) <= 1) return false;
            uint h = Hash(rx, vertical ? 1 : 2, rz, seed);
            return (h % 10u) < 4u;
        }

        static bool DoorAt(int rx, int rz, bool vertical, int local, int seed)
        {
            int centre = 2 + (int)(Hash(rx, vertical ? 3 : 4, rz, seed) % 4u);
            return Math.Abs(local - centre) <= 1;
        }

        static bool SpawnPlaza(int x, int z)
        {
            return Math.Abs(x) <= 10 && Math.Abs(z) <= 10;
        }

        public static void ExitDoor(int nearX, int nearZ, int seed, out int doorX, out int doorZ)
        {
            int sx = Div(nearX, 160), sz = Div(nearZ, 160);
            uint h = Hash(sx, 77, sz, seed);
            int rx = sx * 20 + 6 + (int)(h % 8u);
            int rz = sz * 20 + 6 + (int)((h >> 8) % 8u);
            if (Math.Abs(rx) <= 2 && Math.Abs(rz) <= 2) rx += 6;
            doorX = rx * Cell + 4;
            doorZ = rz * Cell + 4;
        }

        public static bool IsExitThreshold(int x, int z, int seed)
        {
            ExitDoor(x, z, seed, out int dx, out int dz);
            return Math.Abs(x - dx) <= 1 && Math.Abs(z - dz) <= 1;
        }

        static ushort WallBlockForZone(int zone, bool red, bool dark, int y, int floorTop)
        {
            if (red || zone == 4)
                return y == floorTop + 1 ? HazardSteelId : RedRoomId;
            if (dark || zone == 3)
                return SlateId;
            if (zone == 1)
                return ((y - floorTop) == 3) ? PlasterId : MarbleTileId;
            if (zone == 2)
                return (y == floorTop + 1) ? HazardSteelId : ConcreteId;
            return WallpaperId;
        }

        static ushort FloorBlock(int x, int z, int seed)
        {
            if (IsExitThreshold(x, z, seed)) return PlasterId;
            int zone = ZoneAt(x, z, seed);
            if (IsRedRoom(x, z, seed)) return RedRoomId;
            if (IsDarkRoom(x, z, seed)) return SlateId;
            if (zone == 1) return ((x + z) & 3) == 0 ? PlasterId : MarbleTileId;
            if (zone == 2) return AsphaltId;
            return CarpetId;
        }

        public static ushort Block(int x, int y, int z, int seed, int floorTop)
        {
            if (y <= 0) return BedrockId;
            if (y >= CeilingTop + 1) return 0;

            int zone = ZoneAt(x, z, seed);
            bool red = IsRedRoom(x, z, seed);
            bool dark = IsDarkRoom(x, z, seed);
            int rx = Div(x, Cell), rz = Div(z, Cell);
            int lx = Mod(x, Cell), lz = Mod(z, Cell);

            // Exit doorway overrides everything so it is never buried inside a wall
            ExitDoor(x, z, seed, out int ex, out int ez);
            int edx = x - ex, edz = z - ez;
            if (Math.Abs(edx) <= 2 && Math.Abs(edz) <= 2)
            {
                if (y < floorTop) return BedrockId;
                if (y == floorTop) return PlasterId;
                if (y >= CeilingBottom) return PlasterId;
                if (edz == -2 && Math.Abs(edx) <= 1 && y <= floorTop + 4)
                {
                    if (Math.Abs(edx) == 1 || y == floorTop + 4) return PlasterId;
                    return y == floorTop + 3 ? ExitBeaconId : (ushort)0;
                }
                return 0;
            }

            if (y < floorTop) return (zone == 1 && y == floorTop - 1) ? MarbleTileId : BedrockId;
            if (y == floorTop) return FloorBlock(x, z, seed);

            // Ceiling slab with fluorescent/floodlight fixtures
            if (y >= CeilingBottom)
            {
                if (y == CeilingBottom && lx >= 3 && lx <= 4 && lz >= 3 && lz <= 4)
                {
                    if (dark)
                    {
                        // Level 6: 90% lights dead, rare faint emergency lamp
                        return (Hash(rx, 19, rz, seed) % 10u) == 0u ? LightId : SlateId;
                    }
                    // Classic Level 0 occasional burnt-out fluorescent fixture
                    if (zone == 0 && !SpawnPlaza(x, z) && (Hash(rx, 21, rz, seed) % 8u) == 0u)
                        return PlasterId;
                    return (zone == 1 || zone == 2) ? FloodlightId : LightId;
                }
                if (dark) return SlateId;
                if (zone == 1) return MarbleTileId;
                if (zone == 2) return ConcreteId;
                return PlasterId;
            }

            // Open arrival plaza at (0, 0) so spawn and tests always have clear headroom
            if (SpawnPlaza(x, z)) return 0;

            ushort wallMat = WallBlockForZone(zone, red, dark, y, floorTop);

            // Outer cell boundary walls with off-center doorways
            bool westWall = lx == 0 && HasWall(rx, rz, true, seed) && !(DoorAt(rx, rz, true, lz, seed) && y <= floorTop + 3);
            bool southWall = lz == 0 && HasWall(rx, rz, false, seed) && !(DoorAt(rx, rz, false, lx, seed) && y <= floorTop + 3);
            if (westWall || southWall) return wallMat;

            uint roomHash = Hash(rx, 9, rz, seed);

            // Sub-level interior features!
            if (zone == 1)
            {
                // Level 37 Poolrooms: sunken warm crystal water basins (lx 2..5, lz 2..5) + tiled corner pillars
                if ((lx == 1 || lx == 6) && (lz == 1 || lz == 6) && (roomHash % 3u) == 0u)
                    return MarbleTileId;
                if (y == floorTop + 1 && (roomHash % 5u) < 3u)
                {
                    if (lx >= 2 && lx <= 5 && lz >= 2 && lz <= 5) return WaterId;
                }
                return 0;
            }

            if (zone == 2)
            {
                // Level 1 Concrete Warehouse: steel H-columns & survivor supply crates/workbenches
                if (lx == 4 && lz == 4 && (roomHash % 4u) == 0u) return HazardSteelId;
                if (y == floorTop + 1 && lx == 2 && lz == 6 && (roomHash % 6u) == 0u) return WorkbenchId;
                if (y <= floorTop + 2 && lx == 6 && lz == 2 && (roomHash % 5u) == 0u) return PlanksId;
                return 0;
            }

            // Classic Level 0 / Dark / Red rooms: non-Euclidean asymmetric L-baffles, structural pillars & rare office cubicles
            uint shape = roomHash % 14u;
            if (shape == 0u && lx == 4 && lz == 4)
                return wallMat;
            if (shape == 1u && lx == 4 && lz >= 2 && lz <= 5 && y <= WallTop)
                return wallMat; // Free-standing partition wall inside room
            if (shape == 2u && lz == 4 && lx >= 2 && lx <= 5 && y <= WallTop)
                return wallMat;
            if (shape == 3u && zone == 0 && y <= floorTop + 2 && lx == 6 && lz == 6)
                return y == floorTop + 1 ? BookshelfId : PlanksId; // Abandoned office shelf
            if (shape == 4u && zone == 0 && y == floorTop + 1 && lx == 2 && lz == 6)
                return PlanksId; // Abandoned office desk

            return 0;
        }

        public static bool ShouldNoclip(int seed, int step, float hunger, float stamina)
        {
            if (hunger > 45f && stamina > 35f) return false;
            return (Hash(step, 99, 0, seed) % 64u) == 0u;
        }
    }
}
