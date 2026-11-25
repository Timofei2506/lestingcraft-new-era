using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using LestingCraft.World;

namespace LestingCraft.Gameplay
{
    [Serializable]
    public sealed class RitualTreeRecord
    {
        public int X, Y, Z;
        public ushort SaplingId;
        public bool UsedBoneMeal;
        public bool Grown;
        public bool Burning;
        public bool ArchFormed;
        public float AgeSeconds;
        public float BurnedSeconds;
        public int BurnedBlocks;
    }

    [Serializable]
    public sealed class ActiveHellArch
    {
        public int Ax, Ay, Az;
        public int Bx, By, Bz;
    }

    [Serializable]
    public sealed class ActiveParadiseWell
    {
        public int X0, Z0, RimY;
    }

    /// <summary>
    /// Authoritative LestingCraft Portal Rituals (zero Minecraft obsidian frames):
    /// 1. Hell (Dimension.Inferno): Grow 2 trees WITHOUT bone meal at a distance of 7 blocks,
    ///    set both on fire, and ONLY once both trees have burned (at least partially) do their
    ///    charred trunks fuse into an Arch to Hell.
    /// 2. Paradise (Dimension.Paradise): Dig a 2x2 square pit 5 blocks deep, fill it with water,
    ///    surround its rim with any flowers (94..97), throw/drop a diamond (149) into it,
    ///    and jump in to ascend to Paradise.
    /// </summary>
    public sealed class PortalRitualState
    {
        public const float NaturalGrowSeconds = 16f;
        public const float MinBurnSecondsForArch = 3.0f;
        public const int MinBurnedBlocksForArch = 3;
        public const int ParadisePitDepth = 5;

        public readonly List<RitualTreeRecord> Trees = new List<RitualTreeRecord>();
        public readonly List<ActiveHellArch> HellArches = new List<ActiveHellArch>();
        public readonly List<ActiveParadiseWell> ParadiseWells = new List<ActiveParadiseWell>();

        float burnStageAccumulator;

        public void Clear()
        {
            Trees.Clear();
            HellArches.Clear();
            ParadiseWells.Clear();
        }

        public static bool IsRitualTwinDistance(int ax, int ay, int az, int bx, int by, int bz)
        {
            if (Math.Abs(ay - by) > 2) return false;
            int dx = ax - bx, dz = az - bz;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            // Accepts exact 7-block center spacing (e.g. x=0 and x=7) AND 7-block clear gap between trunks (x=0 and x=8)
            return dist >= 6.5f && dist <= 8.25f;
        }

        public string RegisterSapling(int x, int y, int z, ushort saplingId)
        {
            if (!BlockRegistry.IsSapling(saplingId)) return null;
            for (int i = Trees.Count - 1; i >= 0; i--)
                if (Trees[i].X == x && Trees[i].Y == y && Trees[i].Z == z)
                    Trees.RemoveAt(i);

            var rec = new RitualTreeRecord
            {
                X = x,
                Y = y,
                Z = z,
                SaplingId = saplingId,
                UsedBoneMeal = false,
                Grown = false,
                Burning = false,
                ArchFormed = false,
                AgeSeconds = 0f,
                BurnedSeconds = 0f,
                BurnedBlocks = 0
            };
            Trees.Add(rec);

            for (int i = 0; i < Trees.Count - 1; i++)
            {
                var other = Trees[i];
                if (!other.ArchFormed && !other.UsedBoneMeal && IsRitualTwinDistance(x, y, z, other.X, other.Y, other.Z))
                    return "🌱 Саженец посажен в 7 блоках от второго дерева! Дождитесь их естественного роста без костной муки.";
            }
            return null;
        }

        public bool TryApplyBoneMeal(int x, int y, int z, Func<int, int, int, ushort> getBlock, Action<int, int, int, ushort> setBlock, out string message)
        {
            ushort id = getBlock(x, y, z);
            if (!BlockRegistry.IsSapling(id))
            {
                message = null;
                return false;
            }
            RitualTreeRecord rec = FindTreeExact(x, y, z);
            if (rec == null)
            {
                rec = new RitualTreeRecord { X = x, Y = y, Z = z, SaplingId = id };
                Trees.Add(rec);
            }
            rec.UsedBoneMeal = true;
            rec.Grown = true;
            BuildGrownTree(rec, setBlock);
            message = "🌿 Дерево мгновенно выросло от костной муки (для ритуала Ада нужны деревья, выросшие БЕЗ костной муки).";
            return true;
        }

        RitualTreeRecord FindTreeExact(int x, int y, int z)
        {
            for (int i = 0; i < Trees.Count; i++)
                if (Trees[i].X == x && Trees[i].Y == y && Trees[i].Z == z)
                    return Trees[i];
            return null;
        }

        public RitualTreeRecord FindGrownTreeContaining(int x, int y, int z)
        {
            for (int i = 0; i < Trees.Count; i++)
            {
                var t = Trees[i];
                if (!t.Grown || t.ArchFormed) continue;
                if (Math.Abs(x - t.X) <= 2 && Math.Abs(z - t.Z) <= 2 && y >= t.Y && y <= t.Y + 7)
                    return t;
            }
            return null;
        }

        public static void BuildGrownTree(RitualTreeRecord t, Action<int, int, int, ushort> setBlock)
        {
            ushort leaves = t.SaplingId == BlockRegistry.SakuraSaplingId ? BlockRegistry.CherryLeavesId : BlockRegistry.LeavesId;
            for (int ry = 0; ry <= 5; ry++)
                setBlock(t.X, t.Y + ry, t.Z, BlockRegistry.WoodId);

            for (int ry = 3; ry <= 6; ry++)
            {
                int rad = ry == 6 ? 1 : 2;
                for (int dz = -rad; dz <= rad; dz++)
                for (int dx = -rad; dx <= rad; dx++)
                {
                    if (dx == 0 && dz == 0 && ry <= 5) continue;
                    if (Math.Abs(dx) == rad && Math.Abs(dz) == rad && ry != 4) continue;
                    setBlock(t.X + dx, t.Y + ry, t.Z + dz, leaves);
                }
            }
        }

        RitualTreeRecord TryDiscoverNaturalTreeAt(int hitX, int hitY, int hitZ, Func<int, int, int, ushort> getBlock)
        {
            for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int tx = hitX + dx, tz = hitZ + dz;
                int baseY = -1;
                for (int sy = hitY; sy >= hitY - 7 && sy >= 1; sy--)
                {
                    if (getBlock(tx, sy, tz) == BlockRegistry.WoodId) baseY = sy;
                    else if (baseY != -1) break;
                }
                if (baseY < 1) continue;
                if (FindTreeExact(tx, baseY, tz) != null) continue;
                int woodCount = 0, leafCount = 0;
                for (int ry = 0; ry <= 5; ry++)
                    if (getBlock(tx, baseY + ry, tz) == BlockRegistry.WoodId) woodCount++;
                for (int ry = 3; ry <= 6; ry++)
                for (int lz = -2; lz <= 2; lz++)
                for (int lx = -2; lx <= 2; lx++)
                {
                    ushort lb = getBlock(tx + lx, baseY + ry, tz + lz);
                    if (lb == BlockRegistry.LeavesId || lb == BlockRegistry.CherryLeavesId) leafCount++;
                }
                if (woodCount >= 4 && leafCount >= 6)
                {
                    var rec = new RitualTreeRecord
                    {
                        X = tx, Y = baseY, Z = tz,
                        SaplingId = BlockRegistry.OakSaplingId,
                        UsedBoneMeal = false,
                        Grown = true
                    };
                    Trees.Add(rec);
                    return rec;
                }
            }
            return null;
        }

        public bool TryIgniteTreeAt(int hitX, int hitY, int hitZ, Func<int, int, int, ushort> getBlock, Action<int, int, int, ushort> setBlock, out string message)
        {
            var tree = FindGrownTreeContaining(hitX, hitY, hitZ) ?? TryDiscoverNaturalTreeAt(hitX, hitY, hitZ, getBlock);
            if (tree == null)
            {
                message = null;
                return false;
            }
            if (tree.Burning)
            {
                message = "🔥 Это дерево уже пылает!";
                return true;
            }
            tree.Burning = true;
            tree.BurnedSeconds = 0f;
            // Place visible flame ember in upper crown immediately
            setBlock(tree.X, tree.Y + 6, tree.Z, BlockRegistry.CampfireId);
            if (tree.UsedBoneMeal)
                message = "🔥 Дерево вспыхнуло, но оно выращено с костной мукой — Арка Преисподней из него не сплетётся!";
            else
                message = "🔥 Дерево подожжено! Когда оба естественных дерева обгорят, они сплетутся в Арку в Ад…";
            return true;
        }

        public void Step(float dt, Func<int, int, int, ushort> getBlock, Action<int, int, int, ushort> setBlock, out string notice)
        {
            notice = null;
            if (dt <= 0f) return;

            // 1. Tick natural sapling growth (strictly WITHOUT bone meal)
            for (int i = Trees.Count - 1; i >= 0; i--)
            {
                var t = Trees[i];
                if (t.Grown || t.ArchFormed) continue;
                ushort cur = getBlock(t.X, t.Y, t.Z);
                if (!BlockRegistry.IsSapling(cur))
                {
                    Trees.RemoveAt(i);
                    continue;
                }
                t.AgeSeconds += dt;
                if (t.AgeSeconds >= NaturalGrowSeconds)
                {
                    t.Grown = true;
                    BuildGrownTree(t, setBlock);
                    for (int j = 0; j < Trees.Count; j++)
                    {
                        if (j == i) continue;
                        var other = Trees[j];
                        if (other.Grown && !other.UsedBoneMeal && !t.UsedBoneMeal && !other.ArchFormed &&
                            IsRitualTwinDistance(t.X, t.Y, t.Z, other.X, other.Y, other.Z))
                        {
                            notice = "🌳 Оба естественных дерева выросли в 7 блоках друг от друга! Сожгите их факелом или костром, чтобы открыть Арку в Ад!";
                            break;
                        }
                    }
                    if (notice == null)
                        notice = "🌳 Саженец вырос естественным путём!";
                }
            }

            // 2. Tick burning trees & progressive crown charring
            burnStageAccumulator += dt;
            bool doCharPass = burnStageAccumulator >= 0.75f;
            if (doCharPass) burnStageAccumulator = 0f;

            for (int i = 0; i < Trees.Count; i++)
            {
                var t = Trees[i];
                if (!t.Burning || t.ArchFormed) continue;
                float prev = t.BurnedSeconds;
                t.BurnedSeconds += dt;
                // Deterministically char crown blocks as BurnedSeconds crosses 0.8s, 1.6s, 2.4s, 3.2s...
                int targetChar = Mathf.Min(12, Mathf.FloorToInt(t.BurnedSeconds / 0.75f));
                while (t.BurnedBlocks < targetChar)
                {
                    CharNextCrownBlock(t, t.BurnedBlocks, getBlock, setBlock);
                    t.BurnedBlocks++;
                }
                // Bonemealed trees burn down completely after 6s without forming any portal
                if (t.UsedBoneMeal && prev < 6f && t.BurnedSeconds >= 6f)
                {
                    BurnDownBonemealedTree(t, setBlock);
                }
            }

            // 3. Check if any twin pair of NON-BONEMEALED burning trees have both burned at least partially!
            for (int i = 0; i < Trees.Count; i++)
            {
                var a = Trees[i];
                if (!a.Grown || a.UsedBoneMeal || !a.Burning || a.ArchFormed) continue;
                if (a.BurnedSeconds < MinBurnSecondsForArch || a.BurnedBlocks < MinBurnedBlocksForArch) continue;

                for (int j = i + 1; j < Trees.Count; j++)
                {
                    var b = Trees[j];
                    if (!b.Grown || b.UsedBoneMeal || !b.Burning || b.ArchFormed) continue;
                    if (b.BurnedSeconds < MinBurnSecondsForArch || b.BurnedBlocks < MinBurnedBlocksForArch) continue;
                    if (!IsRitualTwinDistance(a.X, a.Y, a.Z, b.X, b.Y, b.Z)) continue;

                    a.ArchFormed = true;
                    b.ArchFormed = true;
                    FormCharredHellArch(a, b, setBlock);
                    HellArches.Add(new ActiveHellArch
                    {
                        Ax = a.X, Ay = a.Y, Az = a.Z,
                        Bx = b.X, By = b.Y, Bz = b.Z
                    });
                    notice = "🔥 Обгоревшие деревья сплелись в обугленную Арку Преисподней! Шагните под арку, чтобы войти в Ад!";
                    break;
                }
            }
        }

        static void CharNextCrownBlock(RitualTreeRecord t, int stepIndex, Func<int, int, int, ushort> getBlock, Action<int, int, int, ushort> setBlock)
        {
            int[,] offsets =
            {
                { 1, 5, 0 }, { -1, 5, 0 }, { 0, 5, 1 }, { 0, 5, -1 },
                { 2, 4, 0 }, { -2, 4, 0 }, { 0, 4, 2 }, { 0, 4, -2 },
                { 1, 4, 1 }, { -1, 4, -1 }, { 1, 3, 0 }, { -1, 3, 0 }
            };
            int idx = stepIndex % 12;
            int bx = t.X + offsets[idx, 0], by = t.Y + offsets[idx, 1], bz = t.Z + offsets[idx, 2];
            ushort cur = getBlock(bx, by, bz);
            if (cur == BlockRegistry.LeavesId || cur == BlockRegistry.CherryLeavesId || cur == BlockRegistry.WoodId)
                setBlock(bx, by, bz, (stepIndex % 4 == 0) ? BlockRegistry.MagmaBasaltId : (ushort)0);
        }

        static void BurnDownBonemealedTree(RitualTreeRecord t, Action<int, int, int, ushort> setBlock)
        {
            for (int ry = 2; ry <= 6; ry++)
            for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
                setBlock(t.X + dx, t.Y + ry, t.Z + dz, 0);
            setBlock(t.X, t.Y, t.Z, BlockRegistry.BasaltId);
            setBlock(t.X, t.Y + 1, t.Z, BlockRegistry.CampfireId);
        }

        public static void FormCharredHellArch(RitualTreeRecord a, RitualTreeRecord b, Action<int, int, int, ushort> setBlock)
        {
            // Clear remaining unburned foliage around both crowns so the charred arch silhouette stands stark & dramatic
            for (int k = 0; k < 2; k++)
            {
                var t = k == 0 ? a : b;
                for (int ry = 3; ry <= 6; ry++)
                for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                    setBlock(t.X + dx, t.Y + ry, t.Z + dz, 0);

                // Charred scorched tree trunk (Basalt + MagmaBasalt glowing fissures)
                for (int ry = 0; ry <= 5; ry++)
                    setBlock(t.X, t.Y + ry, t.Z, (ry == 2 || ry == 4) ? BlockRegistry.MagmaBasaltId : BlockRegistry.BasaltId);
            }

            // Span the jagged Charred Arch between trunk A and trunk B!
            int steps = 16;
            int baseY = Math.Min(a.Y, b.Y);
            for (int s = 0; s <= steps; s++)
            {
                float u = s / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(a.X, b.X, u));
                int z = Mathf.RoundToInt(Mathf.Lerp(a.Z, b.Z, u));
                float curve = 4f * u * (1f - u); // 0 at trunks, 1 at center apex
                int archTop = baseY + 5 + Mathf.RoundToInt(curve * 2.2f);

                // Clear walkable doorway below the arch (baseY + 1 .. baseY + 3)
                if (s >= 3 && s <= steps - 3)
                {
                    for (int wy = baseY + 1; wy <= baseY + 3; wy++)
                        setBlock(x, wy, z, 0);
                }

                // Scorched bough arch crown + fiery portal veil underneath apex
                setBlock(x, archTop, z, (s % 3 == 0) ? BlockRegistry.MagmaBasaltId : BlockRegistry.BasaltId);
                if (s == steps / 2)
                    setBlock(x, archTop + 1, z, BlockRegistry.CampfireId);
                if (s >= 5 && s <= steps - 5 && archTop - 1 >= baseY + 4)
                    setBlock(x, archTop - 1, z, BlockRegistry.AndromedaCrystalId);
            }
        }

        public bool IsInsideActiveHellArch(Vector3 pos)
        {
            for (int i = 0; i < HellArches.Count; i++)
            {
                var arch = HellArches[i];
                int minY = Math.Min(arch.Ay, arch.By);
                if (pos.y < minY - 0.5f || pos.y > minY + 4.5f) continue;

                Vector2 p = new Vector2(pos.x, pos.z);
                Vector2 a = new Vector2(arch.Ax + 0.5f, arch.Az + 0.5f);
                Vector2 b = new Vector2(arch.Bx + 0.5f, arch.Bz + 0.5f);
                Vector2 ab = b - a;
                float len2 = ab.sqrMagnitude;
                if (len2 < 1f) continue;
                float t = Vector2.Dot(p - a, ab) / len2;
                if (t < 0.18f || t > 0.82f) continue; // Between the two trunks, not outside them
                Vector2 closest = a + ab * t;
                if ((p - closest).sqrMagnitude <= 1.35f * 1.35f)
                    return true;
            }
            return false;
        }

        // =========================================================================
        // PARADISE PORTAL RITUAL:
        // 2x2 dug square pit, 5 blocks deep, filled with Water (7),
        // surrounded by ANY Flowers (94..97), throw/drop a Diamond (149) into it,
        // jump in -> teleport to Dimension.Paradise!
        // =========================================================================

        /// <summary>
        /// Helper when placing Water (7) into an enclosed 2x2 pit so pouring water cleanly fills the vertical well column.
        /// </summary>
        public static void CascadeWaterInPitColumn(int x, int y, int z, Func<int, int, int, ushort> getBlock, Action<int, int, int, ushort> setBlock)
        {
            for (int dy = 1; dy <= 5; dy++)
            {
                if (y - dy <= 0) break;
                ushort below = getBlock(x, y - dy, z);
                if (below == 0) setBlock(x, y - dy, z, BlockRegistry.WaterId);
                else if (below != BlockRegistry.WaterId) break;
            }
        }

        /// <summary>
        /// Checks whether (x0, z0) is the NW corner of a valid 2x2 square pit of depth 5 (rimY - 4 .. rimY)
        /// filled with Water (7) and ringed by Flowers (94..97) around its perimeter at rimY + 1.
        /// </summary>
        public static bool IsValidFlowerWaterWellAt(int x0, int rimY, int z0, Func<int, int, int, ushort> getBlock)
        {
            if (rimY < 6 || rimY > 245) return false;

            // 1. Verify 2x2 column of depth 5 (y = rimY - 4 .. rimY) is filled with Water (7) and has a solid floor at rimY - 5
            for (int dz = 0; dz <= 1; dz++)
            for (int dx = 0; dx <= 1; dx++)
            {
                int cx = x0 + dx, cz = z0 + dz;
                ushort floor = getBlock(cx, rimY - ParadisePitDepth, cz);
                if (floor == 0 || floor == BlockRegistry.WaterId || floor == BlockRegistry.LavaId)
                    return false;
                for (int d = 0; d < ParadisePitDepth; d++)
                {
                    if (getBlock(cx, rimY - d, cz) != BlockRegistry.WaterId)
                        return false;
                }
            }

            // 2. Verify pit walls (all 8 orthogonal neighbors along depth y = rimY - 4 .. rimY) are solid ground (dug pit!)
            int[] px = { x0, x0 + 1, x0, x0 + 1, x0 - 1, x0 - 1, x0 + 2, x0 + 2 };
            int[] pz = { z0 - 1, z0 - 1, z0 + 2, z0 + 2, z0, z0 + 1, z0, z0 + 1 };
            for (int i = 0; i < 8; i++)
            {
                for (int d = 0; d < ParadisePitDepth; d++)
                {
                    ushort wall = getBlock(px[i], rimY - d, pz[i]);
                    if (wall == 0 || wall == BlockRegistry.WaterId)
                        return false;
                }
                // 3. Verify flowers placed all around the 2x2 pit rim at y = rimY + 1!
                if (!BlockRegistry.IsFlower(getBlock(px[i], rimY + 1, pz[i])))
                    return false;
            }

            return true;
        }

        public bool TryActivateParadiseWellNear(Vector3 dropPos, Func<int, int, int, ushort> getBlock, Action<int, int, int, ushort> setBlock, out ActiveParadiseWell activated)
        {
            activated = null;
            int bx = Mathf.FloorToInt(dropPos.x);
            int by = Mathf.FloorToInt(dropPos.y);
            int bz = Mathf.FloorToInt(dropPos.z);

            for (int ry = by + 2; ry >= by - 5; ry--)
            for (int ox = -2; ox <= 1; ox++)
            for (int oz = -2; oz <= 1; oz++)
            {
                int x0 = bx + ox, z0 = bz + oz;
                if (!IsValidFlowerWaterWellAt(x0, ry, z0, getBlock)) continue;

                // Seal bottom with glowing celestial crystals so the water shimmers!
                for (int dz = 0; dz <= 1; dz++)
                for (int dx = 0; dx <= 1; dx++)
                    setBlock(x0 + dx, ry - ParadisePitDepth, z0 + dz, BlockRegistry.AndromedaCrystalId);

                for (int i = 0; i < ParadiseWells.Count; i++)
                {
                    if (ParadiseWells[i].X0 == x0 && ParadiseWells[i].Z0 == z0 && ParadiseWells[i].RimY == ry)
                    {
                        activated = ParadiseWells[i];
                        return true;
                    }
                }
                activated = new ActiveParadiseWell { X0 = x0, Z0 = z0, RimY = ry };
                ParadiseWells.Add(activated);
                return true;
            }
            return false;
        }

        public bool IsPlayerInsideActivatedParadiseWell(Vector3 playerPos)
        {
            for (int i = 0; i < ParadiseWells.Count; i++)
            {
                var w = ParadiseWells[i];
                if (playerPos.x >= w.X0 - 0.15f && playerPos.x <= w.X0 + 2.15f &&
                    playerPos.z >= w.Z0 - 0.15f && playerPos.z <= w.Z0 + 2.15f &&
                    playerPos.y >= w.RimY - ParadisePitDepth - 0.2f && playerPos.y <= w.RimY + 0.55f)
                    return true;
            }
            return false;
        }

        const string PrefsPrefix = "LC.Rituals.V1.";

        public void SaveToPrefs(int seed)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < HellArches.Count; i++)
            {
                var a = HellArches[i];
                if (i > 0) sb.Append(';');
                sb.AppendFormat(CultureInfo.InvariantCulture, "H,{0},{1},{2},{3},{4},{5}", a.Ax, a.Ay, a.Az, a.Bx, a.By, a.Bz);
            }
            for (int i = 0; i < ParadiseWells.Count; i++)
            {
                var w = ParadiseWells[i];
                if (sb.Length > 0) sb.Append(';');
                sb.AppendFormat(CultureInfo.InvariantCulture, "P,{0},{1},{2}", w.X0, w.RimY, w.Z0);
            }
            PlayerPrefs.SetString(PrefsPrefix + seed, sb.ToString());
        }

        public void LoadFromPrefs(int seed)
        {
            HellArches.Clear();
            ParadiseWells.Clear();
            string raw = PlayerPrefs.GetString(PrefsPrefix + seed, "");
            if (string.IsNullOrEmpty(raw)) return;
            string[] parts = raw.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string[] tok = parts[i].Split(',');
                if (tok.Length == 7 && tok[0] == "H" &&
                    int.TryParse(tok[1], out int ax) && int.TryParse(tok[2], out int ay) && int.TryParse(tok[3], out int az) &&
                    int.TryParse(tok[4], out int bx) && int.TryParse(tok[5], out int by) && int.TryParse(tok[6], out int bz))
                {
                    HellArches.Add(new ActiveHellArch { Ax = ax, Ay = ay, Az = az, Bx = bx, By = by, Bz = bz });
                }
                else if (tok.Length == 4 && tok[0] == "P" &&
                    int.TryParse(tok[1], out int x0) && int.TryParse(tok[2], out int ry) && int.TryParse(tok[3], out int z0))
                {
                    ParadiseWells.Add(new ActiveParadiseWell { X0 = x0, RimY = ry, Z0 = z0 });
                }
            }
        }
    }
}
