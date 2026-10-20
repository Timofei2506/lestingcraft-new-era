using System;
using UnityEngine;
using LestingCraft.World;

namespace LestingCraft.Gameplay
{
    public enum DreamPhase
    {
        Awake = 0,
        SleepParalysis = 1,
        NightmareWalk = 2,
        WakingGasp = 3
    }

    public sealed class DreamAnomaly
    {
        public const float ParalysisDuration = 6.5f;
        public const float MaxNightmareDuration = 68f;
        public const float WakingGaspDuration = 1.6f;

        public float SpawnX => 0.5f;
        public float SpawnY => 45.15f;
        public float SpawnZ => 0.5f;

        public DreamPhase Phase { get; private set; } = DreamPhase.Awake;
        public float PhaseElapsed { get; private set; }
        public double PhaseSeconds => PhaseElapsed;
        public float TotalDreamTime { get; private set; }
        public Dimension ReturnDimension { get; private set; } = Dimension.Lestingia;
        public float ReturnX { get; private set; }
        public float ReturnY { get; private set; }
        public float ReturnZ { get; private set; }
        public Vector3 ReturnPosition => new Vector3(ReturnX, ReturnY, ReturnZ);
        public float StalkerX { get; private set; }
        public float StalkerY { get; private set; } = 45f;
        public float StalkerZ { get; private set; }
        public Vector3 StalkerPosition => new Vector3(StalkerX, StalkerY, StalkerZ);

        int lastTriggerDay = -999;

        public bool IsActive => Phase != DreamPhase.Awake;
        public bool IsParalyzed => Phase == DreamPhase.SleepParalysis;
        public bool CanWalkInDream => Phase == DreamPhase.NightmareWalk;
        public bool AllowsWorldOrItemInteraction => !IsActive;
        public bool IsolatesFromMultiplayer => IsActive;
        public float ParalysisTremor =>
            Phase == DreamPhase.SleepParalysis
                ? Mathf.Sin(PhaseElapsed * 29f) * 0.45f
                : 0f;

        public static float ComputeSleepDreamProbability(float stamina, float comfort, bool stormy)
        {
            float chance = 0.14f;
            if (stormy) chance += 0.06f;
            if (stamina < 30f) chance += 0.05f;
            if (comfort < 35f) chance += 0.03f;
            return Mathf.Clamp(chance, 0.05f, 0.30f);
        }

        public static bool RollDreamTrigger(int seed, int eventSalt, float probability)
        {
            uint h = TerrainGenerator.Hash(eventSalt, 13, eventSalt * 7 + 3, seed ^ 0x51EE9);
            float u = (h & 65535u) / 65536f;
            return u < probability;
        }

        public bool TryEnterFromBedSleep(int seed, int dayIdx, float stamina, float comfort, bool stormy,
            Dimension currentDim, float rx, float ry, float rz)
        {
            if (IsActive || dayIdx == lastTriggerDay) return false;
            float chance = ComputeSleepDreamProbability(stamina, comfort, stormy);
            if (!RollDreamTrigger(seed, dayIdx * 31 + 7, chance)) return false;
            lastTriggerDay = dayIdx;
            EnterDream(currentDim, rx, ry, rz);
            return true;
        }

        public bool TryEnterFromExhaustionCollapse(int seed, double worldClock, float stamina, float hunger,
            Dimension currentDim, float rx, float ry, float rz)
        {
            if (IsActive) return false;
            double hour = (worldClock / 60.0) % 24.0;
            bool night = hour >= 20.0 || hour <= 5.0;
            if (!night || stamina > 3.5f || hunger > 18f) return false;
            int dayIdx = (int)Math.Floor(worldClock / 1440.0);
            if (dayIdx == lastTriggerDay) return false;
            if (!RollDreamTrigger(seed, dayIdx * 97 + 19, 0.16f)) return false;
            lastTriggerDay = dayIdx;
            EnterDream(currentDim, rx, ry, rz);
            return true;
        }

        public void EnterDream(Dimension returnDimension, float rx, float ry, float rz)
        {
            ReturnDimension = returnDimension;
            ReturnX = rx; ReturnY = ry; ReturnZ = rz;
            Phase = DreamPhase.SleepParalysis;
            PhaseElapsed = 0f;
            TotalDreamTime = 0f;
            StalkerX = 0.5f; StalkerY = 45f; StalkerZ = 5.8f;
        }

        public static bool ShouldStalkerAdvance(Vector3 playerForward, Vector3 toStalker)
        {
            if (toStalker.sqrMagnitude < 0.001f) return false;
            float dot = Vector3.Dot(playerForward.normalized, toStalker.normalized);
            return dot < 0.42f;
        }

        public bool Step(float dt, Vector3 playerDreamPos, Vector3 playerForward)
        {
            if (!IsActive || dt <= 0f) return false;
            PhaseElapsed += dt;
            TotalDreamTime += dt;

            if (Phase == DreamPhase.SleepParalysis)
            {
                if (PhaseElapsed >= ParalysisDuration)
                {
                    Phase = DreamPhase.NightmareWalk;
                    PhaseElapsed = 0f;
                    StalkerX = -6.5f; StalkerY = 45f; StalkerZ = 18.5f;
                }
                return false;
            }

            if (Phase == DreamPhase.NightmareWalk)
            {
                Vector3 toStalker = StalkerPosition - playerDreamPos;
                float dist = toStalker.magnitude;
                if (ShouldStalkerAdvance(playerForward, toStalker) && dist > 1.7f)
                {
                    Vector3 flatDir = new Vector3(-toStalker.x, 0f, -toStalker.z).normalized;
                    StalkerX += flatDir.x * dt * 3.1f;
                    StalkerZ += flatDir.z * dt * 3.1f;
                }

                bool reachedExitDoor = Mathf.Abs(playerDreamPos.x) < 2.5f && Mathf.Abs(playerDreamPos.z - 46f) < 2.5f;
                bool stalkerGrabbed = dist <= 1.75f && PhaseElapsed > 3f;
                if (reachedExitDoor || stalkerGrabbed || TotalDreamTime >= MaxNightmareDuration)
                {
                    Phase = DreamPhase.WakingGasp;
                    PhaseElapsed = 0f;
                }
                return false;
            }

            if (Phase == DreamPhase.WakingGasp && PhaseElapsed >= WakingGaspDuration)
            {
                Phase = DreamPhase.Awake;
                PhaseElapsed = 0f;
                return true;
            }
            return false;
        }

        public void ForceWake()
        {
            Phase = DreamPhase.Awake;
            PhaseElapsed = 0f;
        }

        public void ForceReset() => ForceWake();
    }
}
