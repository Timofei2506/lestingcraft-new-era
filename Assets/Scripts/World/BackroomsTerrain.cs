using System;

namespace LestingCraft.World
{
    /// <summary>
    /// Level 0 of the Backrooms (community canon): endless randomly segmented rooms with
    /// mono-yellow wallpaper, damp carpet floor, buzzing fluorescent ceiling panels, and
    /// rare red rooms that must not be entered. Pure deterministic math - no allocation,
    /// usable from Burst jobs and from gameplay code alike. All blocks reuse the existing
    /// baked palette (no new textures needed): 43 sandstone carpet, 28 limestone wallpaper,
    /// 30 marble ceiling tiles, 56 lantern fluorescents, 62 red terracotta for red rooms.
    /// Floors step gently between 16x16 regions (rare ledges of the canon) so the level has
    /// real relief while every room stays walkable.
    /// </summary>
    public static class BackroomsTerrain
    {
        public const int CeilingBottom = 72; // fluorescent panels sit here
        public const int CeilingTop = 73;
        public const int WallTop = 71;

        const ushort Carpet = 43, Wallpaper = 28, CeilingTile = 30, Fluorescent = 56, RedWall = 62;

        static uint Hash(int a, int b, int c, int seed)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= (uint)a * 0x8da6b343u; h = (h << 13) | (h >> 19);
                h ^= (uint)b * 0xd8163841u; h = (h << 17) | (h >> 15);
                h ^= (uint)c * 0xcb1ab31fu; h *= 0x9e3779b1u;
                return h ^ (h >> 16);
            }
        }

        static float Smoother(float t) => t * t * (3 - 2 * t);

        /// <summary>Region floor level: the ledges that make the endless rooms readable.</summary>
        static float Level(int rx, int rz, int seed) => 64f + (Hash(rx, 11, rz, seed ^ 0xF100) % 11) - 5;

        /// <summary>Walkable carpet height: smooth-step blend between neighbouring region levels.</summary>
        public static int FloorTop(int x, int z, int seed)
        {
            int rx = x >> 4, rz = z >> 4;
            float fx = (x - rx * 16) / 16f, fz = (z - rz * 16) / 16f;
            float a = Level(rx, rz, seed), b = Level(rx + 1, rz, seed);
            float c = Level(rx, rz + 1, seed), d = Level(rx + 1, rz + 1, seed);
            float sx = Smoother(fx), sz = Smoother(fz);
            float v = (a * (1 - sx) + b * sx) * (1 - sz) + (c * (1 - sx) + d * sx) * sz;
            return (int)Math.Round(v);
        }

        /// <summary>Room-cell identity used for red rooms and lighting.</summary>
        static uint Room(int x, int z, int seed) => Hash(x >> 3, 51, z >> 3, seed ^ 0x8BAC);

        /// <summary>True for the rare red-wallpaper rooms of Level 0.</summary>
        public static bool IsRedRoom(int x, int z, int seed) => Room(x, z, seed) % 23 == 0;

        /// <summary>Spawn plaza around the origin stays wide open and lit.</summary>
        static bool SpawnPlaza(int x, int z) => Math.Abs(x) <= 10 && Math.Abs(z) <= 10;

        /// <summary>Every 256x256 region hides one bright exit doorway.</summary>
        public static void ExitDoor(int x, int z, int seed, out int ex, out int ez)
        {
            int rx = x >> 8, rz = z >> 8;
            ex = rx * 256 + (int)(Hash(rx, 42, rz, seed ^ 0xE217) % 250) + 3;
            ez = rz * 256 + (int)((Hash(rx, 43, rz, seed ^ 0xE217) >> 8) % 250) + 3;
            if (rx == 0 && rz == 0) { if (ex < 48) ex += 48; if (ez < 48) ez += 48; } // never on spawn
        }

        /// <summary>Standing on the exit threshold (within 2 blocks) leads back to reality.</summary>
        public static bool IsExitThreshold(int x, int z, int seed)
        {
            ExitDoor(x, z, seed, out int ex, out int ez);
            return Math.Abs(x - ex) <= 2 && Math.Abs(z - ez) <= 2;
        }

        /// <summary>Carpet block id at the floor of this column (also the exit marker floor).</summary>
        public static ushort FloorBlock(int x, int z, int seed)
        {
            ExitDoor(x, z, seed, out int ex, out int ez);
            if (Math.Abs(x - ex) <= 1 && Math.Abs(z - ez) <= 1) return 40; // polished threshold
            if (IsRedRoom(x, z, seed) && !SpawnPlaza(x, z)) return RedWall;
            return Carpet;
        }

        public static ushort Block(int x, int y, int z, int seed, int floorTop)
        {
            if (y == 0) return 9;
            if (y > CeilingTop) return 0;
            if (y < floorTop) return 1;
            if (y == floorTop) return FloorBlock(x, z, seed);

            ExitDoor(x, z, seed, out int ex, out int ez);
            bool exitCell = Math.Abs(x - ex) <= 1 && Math.Abs(z - ez) <= 1;
            bool red = !exitCell && !SpawnPlaza(x, z) && IsRedRoom(x, z, seed);

            if (y >= CeilingBottom)
            {
                // Fluorescent panels above room centres (never in red rooms), exit always glows.
                bool roomLight = Room(x, z, seed) % 4 == 0;
                int mx = ((x % 8) + 8) % 8, mz = ((z % 8) + 8) % 8;
                bool inPanel = mx >= 2 && mx <= 5 && mz >= 2 && mz <= 5;
                if (y == CeilingBottom && (exitCell || (roomLight && inPanel && !red))) return Fluorescent;
                return red ? RedWall : CeilingTile;
            }

            // Walls on the 8-grid borders: most segments solid, some open into doorways.
            int wx = ((x % 8) + 8) % 8, wz = ((z % 8) + 8) % 8;
            bool borderX = wx == 0, borderZ = wz == 0;
            bool segX = Hash(x >> 3, 77, z, seed ^ 0x51A1) % 10 < 7;
            bool segZ = Hash(x, 78, z >> 3, seed ^ 0x51A2) % 10 < 7;
            bool wall = (borderX && segX) || (borderZ && segZ);
            if (SpawnPlaza(x, z)) wall = false;
            if (exitCell) wall = false;
            if (wall) return red ? RedWall : Wallpaper;

            // Interior pillars give the endless rooms some depth cues.
            if (!borderX && !borderZ && !SpawnPlaza(x, z) && Hash(x, 79, z, seed ^ 0x3301) % 31 == 0)
                return red ? RedWall : Wallpaper;
            return 0;
        }

        /// <summary>
        /// Deterministic no-clip accident during an unlucky night: exhaustion, hunger or a
        /// soaked body while sleeping in Lestingia can drop the player into the Backrooms.
        /// </summary>
        public static bool ShouldNoclip(int seed, int dayIdx, float stamina, float hunger, float wetness)
        {
            uint roll = Hash(seed, dayIdx, 13, 0x5C1D);
            return (stamina < 55f || hunger < 50f || wetness > 0.25f) && roll % 100 < 18;
        }
    }
}
