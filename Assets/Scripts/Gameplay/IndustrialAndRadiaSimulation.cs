using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.World;

namespace LestingCraft.Gameplay
{
    public struct PowerGridSummary
    {
        public int GeneratorCount, ReactorCount, SolarCount, BatteryCount, CableCount, ConsumerCount;
        public float GenerationKw, DemandKw, StoredKwh, CapacityKwh, MaxReactorTempC;
        public bool Powered => GenerationKw + StoredKwh * 10f >= DemandKw && (GenerationKw > 0.01f || StoredKwh > 0.01f);
        public bool Overheating => MaxReactorTempC > 780f;
    }

    public sealed class RadiaBossState
    {
        public int Index;
        public string NameRu;
        public Vector3 HomePos;
        public Vector3 Position;
        public float Yaw;
        public int MaxHp;
        public int Hp;
        public float AttackCooldown;
        public float DashTimer;
        public bool Dead => Hp <= 0;
    }

    public sealed class FalloutZone
    {
        public Vector3 Center;
        public float Radius;
        public float RemainingSeconds;
    }

    /// <summary>
    /// Pure simulation state & rules for Era v9:
    /// 1) Real Electrical Power Grid (kW generation, copper cable graph, LiFePO4 battery storage, water-cooled VVER Nuclear Reactor).
    /// 2) Ionizing Radiation Dosimetry (mSv/h, lead-block shielding attenuation, 1000 mSv threshold -> Radia Abyss transport).
    /// 3) Radia Irradiated Abyss ("Черенковский Затон"): 6 submerged Cherenkov cores + 3 tough Raid Bosses + Sluice Dam Purge escape.
    /// </summary>
    public sealed class IndustrialAndRadiaSimulation
    {
        public const int RequiredCherenkovCores = 6;
        public const float PurgeDurationSeconds = 20f;

        // Dosimetry (0..1 where 1.0 = 1000 mSv acute radiation threshold -> transport into Radia Abyss)
        public float RadiationDose01;
        public float CurrentMillisievertsPerHour;
        public float PostEscapeImmunityTimer;

        // Power grid snapshot
        public PowerGridSummary Grid;
        public readonly HashSet<Vector3Int> EnergizedNodes = new HashSet<Vector3Int>();
        float batteryChargeKwh = 45f;
        float reactorCoreTempC = 285f;

        // Active nuclear fallout zones created by Secret Warhead 666 or reactor meltdown
        public readonly List<FalloutZone> FalloutZones = new List<FalloutZone>();

        // Radia Abyss progression & 3 Raid Bosses
        public Dimension ReturnDimension = Dimension.Lestingia;
        public Vector3 ReturnPosition = new Vector3(0f, 70f, 0f);
        public int HarvestedSubmergedCores;
        public int BossesDefeatedMask; // bit 0: West Helix Chimera, bit 1: East Helix Colossus, bit 2: Dam Archon
        public bool PurgeActive;
        public bool EscapePortalOpen;
        public float PurgeRemainingSeconds;
        public readonly RadiaBossState[] Bosses = new RadiaBossState[3];

        public IndustrialAndRadiaSimulation()
        {
            ResetRadiaEncounter();
        }

        public int DefeatedBossCount =>
            ((BossesDefeatedMask & 1) != 0 ? 1 : 0) +
            ((BossesDefeatedMask & 2) != 0 ? 1 : 0) +
            ((BossesDefeatedMask & 4) != 0 ? 1 : 0);

        public bool CanTriggerDamPurge =>
            HarvestedSubmergedCores >= RequiredCherenkovCores && BossesDefeatedMask == 7;

        public void ResetRadiaEncounter()
        {
            HarvestedSubmergedCores = 0;
            BossesDefeatedMask = 0;
            PurgeActive = false;
            EscapePortalOpen = false;
            PurgeRemainingSeconds = 0f;

            Bosses[0] = new RadiaBossState
            {
                Index = 0,
                NameRu = "ХИМЕРА СПИРАЛИ α (Страж Западной Градирни)",
                HomePos = new Vector3(-27f, 50f, 56f),
                Position = new Vector3(-27f, 50f, 56f),
                MaxHp = 420,
                Hp = 420
            };
            Bosses[1] = new RadiaBossState
            {
                Index = 1,
                NameRu = "КОЛОСС ГРАДИРНИ β (Страж Восточной Спирали)",
                HomePos = new Vector3(31f, 50f, 56f),
                Position = new Vector3(31f, 50f, 56f),
                MaxHp = 480,
                Hp = 480
            };
            Bosses[2] = new RadiaBossState
            {
                Index = 2,
                NameRu = "АРХОНТ ЗАТОНА · ОБЪЕКТ №0 (Хранитель Шлюза)",
                HomePos = new Vector3(0f, 50f, 65f),
                Position = new Vector3(0f, 50f, 65f),
                MaxHp = 650,
                Hp = 650
            };
        }

        public static bool IsElectricalBlock(ushort id) =>
            id == BlockRegistry.CopperCableId || id == BlockRegistry.BatteryBankId ||
            id == BlockRegistry.FuelGeneratorId || id == BlockRegistry.SolarArrayId ||
            id == BlockRegistry.NuclearReactorId || id == BlockRegistry.ElectricFloodlightId ||
            id == BlockRegistry.PoweredRailId;

        private static float ComputeGammaAttenuation(Func<int, int, int, ushort> getBlock, Vector3 src, Vector3 dst)
        {
            float atten = 1f;
            int prevX = int.MinValue, prevY = int.MinValue, prevZ = int.MinValue;
            int sx = Mathf.FloorToInt(src.x), sy = Mathf.FloorToInt(src.y), sz = Mathf.FloorToInt(src.z);
            for (int step = 1; step <= 8; step++)
            {
                float t = step / 9f;
                int bx = Mathf.FloorToInt(src.x + (dst.x - src.x) * t);
                int by = Mathf.FloorToInt(src.y + (dst.y - src.y) * t);
                int bz = Mathf.FloorToInt(src.z + (dst.z - src.z) * t);
                if (bx == prevX && by == prevY && bz == prevZ) continue;
                prevX = bx; prevY = by; prevZ = bz;
                if (bx == sx && by == sy && bz == sz) continue;
                ushort b = getBlock(bx, by, bz);
                if (b == BlockRegistry.LeadShieldId) atten *= 0.10f;
                else if (b == BlockRegistry.ReinforcedConcreteId) atten *= 0.55f;
                else if (b == BlockRegistry.WaterId) atten *= 0.65f;
            }
            return atten;
        }

        /// <summary>Evaluates real connected 3D electrical circuits around the player and updates reactor thermohydraulics.</summary>
        public void StepPowerAndRadiation(
            float dt,
            Vector3 playerPos,
            Dimension currentDim,
            float daylight01,
            Inventory inv,
            Func<int, int, int, ushort> getBlock,
            out bool triggerRadiaTransport)
        {
            triggerRadiaTransport = false;
            if (dt <= 0f || getBlock == null) return;

            if (PostEscapeImmunityTimer > 0f)
                PostEscapeImmunityTimer = Mathf.Max(0f, PostEscapeImmunityTimer - dt);

            for (int i = FalloutZones.Count - 1; i >= 0; i--)
            {
                FalloutZones[i].RemainingSeconds -= dt;
                if (FalloutZones[i].RemainingSeconds <= 0f) FalloutZones.RemoveAt(i);
            }

            EnergizedNodes.Clear();
            var summary = new PowerGridSummary();
            int px = Mathf.FloorToInt(playerPos.x), py = Mathf.FloorToInt(playerPos.y), pz = Mathf.FloorToInt(playerPos.z);

            float ambientMsvHr = currentDim == Dimension.Radia ? 65f : 0f;
            bool reactorWaterCooled = false;

            // Scan local 18x12x18 volume around player for electrical networks, uranium ore, reactors & lead shielding
            var allElectrical = new List<Vector3Int>();
            int leadShieldBlocksNear = 0;

            for (int y = py - 6; y <= py + 6; y++)
            {
                if (y < 1 || y >= 255) continue;
                for (int z = pz - 16; z <= pz + 16; z++)
                for (int x = px - 16; x <= px + 16; x++)
                {
                    ushort b = getBlock(x, y, z);
                    if (b == 0) continue;
                    if (b == BlockRegistry.LeadShieldId)
                    {
                        float d2 = (x + 0.5f - playerPos.x) * (x + 0.5f - playerPos.x) + (z + 0.5f - playerPos.z) * (z + 0.5f - playerPos.z);
                        if (d2 < 36f) leadShieldBlocksNear++;
                    }
                    if (b == BlockRegistry.UraniumOreId || b == BlockRegistry.UraniumBlockId)
                    {
                        var srcPos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                        float dist = Mathf.Max(1.2f, Vector3.Distance(playerPos, srcPos));
                        if (dist < 9.5f)
                        {
                            float losAtten = ComputeGammaAttenuation(getBlock, srcPos, playerPos + new Vector3(0f, 0.5f, 0f));
                            ambientMsvHr += losAtten * (b == BlockRegistry.UraniumBlockId ? 680f : 310f) / (dist * dist);
                        }
                    }
                    if (IsElectricalBlock(b))
                    {
                        var node = new Vector3Int(x, y, z);
                        allElectrical.Add(node);
                        if (b == BlockRegistry.CopperCableId) summary.CableCount++;
                        else if (b == BlockRegistry.FuelGeneratorId) { summary.GeneratorCount++; summary.GenerationKw += 45f; }
                        else if (b == BlockRegistry.SolarArrayId) { summary.SolarCount++; summary.GenerationKw += 22f * Mathf.Clamp01(0.15f + daylight01 * 0.85f); }
                        else if (b == BlockRegistry.BatteryBankId) { summary.BatteryCount++; summary.CapacityKwh += 250f; }
                        else if (b == BlockRegistry.ElectricFloodlightId) { summary.ConsumerCount++; summary.DemandKw += 6f; }
                        else if (b == BlockRegistry.PoweredRailId) { summary.ConsumerCount++; summary.DemandKw += 12f; }
                        else if (b == BlockRegistry.NuclearReactorId)
                        {
                            summary.ReactorCount++;
                            bool hasWater =
                                getBlock(x + 1, y, z) == BlockRegistry.WaterId || getBlock(x - 1, y, z) == BlockRegistry.WaterId ||
                                getBlock(x, y, z + 1) == BlockRegistry.WaterId || getBlock(x, y, z - 1) == BlockRegistry.WaterId ||
                                getBlock(x, y - 1, z) == BlockRegistry.WaterId;
                            if (hasWater) reactorWaterCooled = true;
                            summary.GenerationKw += hasWater ? 640f : 480f;

                            bool leadCased =
                                getBlock(x + 1, y, z) == BlockRegistry.LeadShieldId &&
                                getBlock(x - 1, y, z) == BlockRegistry.LeadShieldId;
                            var reactorPos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                            float dist = Mathf.Max(1.2f, Vector3.Distance(playerPos, reactorPos));
                            if (dist < 13f)
                            {
                                float losAtten = ComputeGammaAttenuation(getBlock, reactorPos, playerPos + new Vector3(0f, 0.5f, 0f));
                                ambientMsvHr += losAtten * (leadCased ? 95f : (hasWater ? 780f : 2200f)) / (dist * dist);
                            }
                        }
                    }
                }
            }

            if (summary.ReactorCount > 0)
            {
                float targetTemp = reactorWaterCooled ? 315f : 890f;
                reactorCoreTempC = Mathf.MoveTowards(reactorCoreTempC, targetTemp, dt * 38f);
            }
            else
            {
                reactorCoreTempC = Mathf.MoveTowards(reactorCoreTempC, 25f, dt * 25f);
            }
            summary.MaxReactorTempC = reactorCoreTempC;

            // Update battery bank charge/discharge balance
            float netKw = summary.GenerationKw - summary.DemandKw;
            batteryChargeKwh = Mathf.Clamp(batteryChargeKwh + (netKw * dt) / 3600f * 60f, 0f, Mathf.Max(10f, summary.CapacityKwh));
            summary.StoredKwh = summary.BatteryCount > 0 ? batteryChargeKwh : 0f;

            // Flood-fill connected components from generators/reactors/solar/charged batteries!
            var nodeSet = new HashSet<Vector3Int>(allElectrical);
            var queue = new Queue<Vector3Int>();
            foreach (var n in allElectrical)
            {
                ushort b = getBlock(n.x, n.y, n.z);
                if (b == BlockRegistry.FuelGeneratorId || b == BlockRegistry.SolarArrayId ||
                    b == BlockRegistry.NuclearReactorId || (b == BlockRegistry.BatteryBankId && summary.StoredKwh > 0.1f))
                {
                    EnergizedNodes.Add(n);
                    queue.Enqueue(n);
                }
            }
            Vector3Int[] dirs = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    var nxt = cur + dirs[d];
                    if (nodeSet.Contains(nxt) && EnergizedNodes.Add(nxt))
                        queue.Enqueue(nxt);
                }
            }
            Grid = summary;

            // Inventory radioactive cargo check (carrying unshielded Uranium Ore / Fuel Rods!)
            if (inv != null)
            {
                int uOre = inv.Count(BlockRegistry.UraniumOreId);
                int uRods = inv.Count(Items.UraniumRod) + inv.Count(BlockRegistry.UraniumBlockId) * 9;
                if (uOre > 0 || uRods > 0)
                    ambientMsvHr += uOre * 85f + uRods * 140f;
            }

            // Nuclear fallout craters
            foreach (var fz in FalloutZones)
            {
                float d = Vector3.Distance(playerPos, fz.Center);
                if (d < fz.Radius)
                    ambientMsvHr += Mathf.Lerp(4800f, 420f, d / Mathf.Max(1f, fz.Radius));
            }

            // Lead Shield blocks around player attenuate scattered gamma radiation by up to 88%!
            float shieldFactor = Mathf.Clamp(1f - leadShieldBlocksNear * 0.44f, 0.12f, 1f);
            CurrentMillisievertsPerHour = ambientMsvHr * shieldFactor;

            if (currentDim != Dimension.Radia && PostEscapeImmunityTimer <= 0f)
            {
                if (CurrentMillisievertsPerHour > 25f)
                {
                    // Accumulate acute radiation dose smoothly (standing in heavy fallout / unshielded core = ~4-8 seconds to full poisoning!)
                    RadiationDose01 = Mathf.Clamp01(RadiationDose01 + (CurrentMillisievertsPerHour / 2600f) * dt);
                    if (RadiationDose01 >= 1f)
                        triggerRadiaTransport = true;
                }
                else
                {
                    RadiationDose01 = Mathf.Max(0f, RadiationDose01 - dt * 0.035f);
                }
            }
        }

        public void StepRadiaBossesAndPurge(float dt, Vector3 playerPos, Action<float, string> damagePlayer)
        {
            if (dt <= 0f) return;

            for (int i = 0; i < Bosses.Length; i++)
            {
                var b = Bosses[i];
                if (b == null || b.Dead) continue;

                b.AttackCooldown = Mathf.Max(0f, b.AttackCooldown - dt);
                b.DashTimer = Mathf.Max(0f, b.DashTimer - dt);

                Vector3 toPlayer = playerPos - b.Position;
                float horizDist = new Vector2(toPlayer.x, toPlayer.z).magnitude;
                float leashDist = Vector3.Distance(playerPos, b.HomePos);

                Vector3 target = leashDist < 38f ? new Vector3(playerPos.x, b.HomePos.y, playerPos.z) : b.HomePos;
                Vector3 moveDir = target - b.Position;
                moveDir.y = 0f;
                if (moveDir.sqrMagnitude > 0.05f)
                {
                    float moveSpeed = b.DashTimer > 0f ? 11.5f : (4.2f + i * 0.55f);
                    b.Yaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                    b.Position += moveDir.normalized * (moveSpeed * dt);
                }

                // Periodic dash charge when player enters mid-range
                if (horizDist > 6f && horizDist < 24f && b.DashTimer <= 0f && b.AttackCooldown <= 0.1f)
                    b.DashTimer = 0.65f;

                float reach = 3.4f + i * 0.45f;
                if (horizDist <= reach && Mathf.Abs(playerPos.y - b.Position.y) < 4.5f && b.AttackCooldown <= 0f)
                {
                    b.AttackCooldown = 1.35f;
                    damagePlayer?.Invoke(18f + i * 5f, b.NameRu);
                }
            }

            if (PurgeActive && !EscapePortalOpen)
            {
                PurgeRemainingSeconds = Mathf.Max(0f, PurgeRemainingSeconds - dt);
                if (PurgeRemainingSeconds <= 0f)
                    EscapePortalOpen = true;
            }
        }

        public bool TryHitBoss(Ray aimRay, int weaponDamage, out string statusMessage)
        {
            statusMessage = null;
            int dmg = Mathf.Max(8, weaponDamage);
            for (int i = 0; i < Bosses.Length; i++)
            {
                var b = Bosses[i];
                if (b == null || b.Dead) continue;
                Bounds bounds = new Bounds(b.Position + Vector3.up * 2.1f, new Vector3(2.8f, 4.4f, 2.8f));
                if (bounds.IntersectRay(aimRay, out float dist) && dist <= 7.5f)
                {
                    b.Hp = Mathf.Max(0, b.Hp - dmg);
                    if (b.Dead)
                    {
                        BossesDefeatedMask |= (1 << i);
                        statusMessage = $"★ ПОВЕРЖЕН БОСС ЗАТОНА: {b.NameRu}! Ключей Спирали: {DefeatedBossCount}/3";
                    }
                    else
                    {
                        statusMessage = $"{b.NameRu}: {b.Hp} / {b.MaxHp} ОЗ (-{dmg})";
                    }
                    return true;
                }
            }
            return false;
        }

        public string TryActivateDamTerminal()
        {
            if (EscapePortalOpen)
                return "ШЛЮЗ ОТКРЫТ! Шагните в белый столб дезактивации над терминалом, чтобы вырваться из Затона!";
            if (PurgeActive)
                return $"ИДЁТ ПРОДУВКА ГЕРМОШЛЮЗА: продержитесь ещё {PurgeRemainingSeconds:0} сек!";
            if (HarvestedSubmergedCores < RequiredCherenkovCores)
                return $"ОТКАЗ ШЛЮЗА: добудьте со дна бассейна светящиеся Черенковские излучатели ({HarvestedSubmergedCores}/{RequiredCherenkovCores})!";
            if (BossesDefeatedMask != 7)
                return $"ОТКАЗ ШЛЮЗА: уничтожьте всех 3 Стражей Спирали и Плотины ({DefeatedBossCount}/3)!";

            PurgeActive = true;
            PurgeRemainingSeconds = PurgeDurationSeconds;
            return "⚠ СИРЕНА ПЛОТИНЫ: ПРОДУВКА ШЛЮЗА ЗАПУЩЕНА! ПРОДЕРЖИТЕСЬ 20 СЕКУНД ДО ОТКРЫТИЯ ВЫХОДА!";
        }
    }
}
