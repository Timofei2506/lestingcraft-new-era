using System;

namespace LestingCraft.Gameplay
{
    /// <summary>Six suit pieces. Worn as an array of tiers, 0 means the slot is empty.</summary>
    public enum SuitPiece { Helmet = 0, Chest = 1, Gloves = 2, Legs = 3, Boots = 4, Backpack = 5 }

    public enum SuitTier { Mk1 = 1, Mk2 = 2, Mk3 = 3 }

    public enum SuitActivity { Rest, Walk, Run, JetPack }

    public readonly struct SuitSpec
    {
        public readonly SuitTier Tier;
        public readonly string Name;
        /// <summary>Oxygen in minutes at walking pace.</summary>
        public readonly double TankMinutes;
        /// <summary>Surface temperature range the suit regulates, in degrees Celsius.</summary>
        public readonly double MinTempC, MaxTempC;
        public readonly double JetFuelKg;
        public readonly double RadiationShield;
        public SuitSpec(SuitTier tier, string name, double tankMinutes, double minTempC, double maxTempC, double jetFuelKg, double radiationShield)
        {
            Tier = tier; Name = name; TankMinutes = tankMinutes; MinTempC = minTempC; MaxTempC = maxTempC; JetFuelKg = jetFuelKg; RadiationShield = radiationShield;
        }
    }

    /// <summary>
    /// Stage 4 suit rules (cyberpunk set). Names and numbers are placeholders for the owner to approve, game tuning rather than physics.
    /// Helmet seal and full set are required for vacuum. Oxygen drains by activity, damage follows when it runs out or the suit is out of range.
    /// </summary>
    public static class SpaceSuit
    {
        public const int PieceCount = 6;
        /// <summary>Damage at or above this value is lethal in one step.</summary>
        public const double LethalDamage = 100.0;

        public static SuitSpec Spec(SuitTier tier)
        {
            switch (tier)
            {
                case SuitTier.Mk2: return new SuitSpec(tier, "Неон Mk II", 150, -60, 110, 8, 0.7);
                case SuitTier.Mk3: return new SuitSpec(tier, "Синдикат Mk III", 240, -80, 125, 10, 0.85);
                default: return new SuitSpec(SuitTier.Mk1, "Ночная смена Mk I", 90, -40, 90, 6, 0.5);
            }
        }

        /// <summary>Lowest tier among the worn pieces, or null if any piece is missing.</summary>
        public static SuitTier? EffectiveTier(int[] worn)
        {
            if (worn == null || worn.Length != PieceCount) return null;
            int min = int.MaxValue;
            for (int i = 0; i < PieceCount; i++)
            {
                if (worn[i] < 1 || worn[i] > 3) return null;
                if (worn[i] < min) min = worn[i];
            }
            return (SuitTier)min;
        }

        public static double ConsumptionMultiplier(SuitActivity a)
        {
            switch (a)
            {
                case SuitActivity.Rest: return 0.5;
                case SuitActivity.Walk: return 1.0;
                case SuitActivity.Run: return 2.0;
                default: return 1.5;
            }
        }
    }

    public sealed class SuitState
    {
        /// <summary>Oxygen left, in minutes at walking pace.</summary>
        public double OxygenMinutes;
        public SuitState(SuitSpec spec) { OxygenMinutes = spec.TankMinutes; }
        public bool Empty { get { return OxygenMinutes <= 0; } }
    }

    public static class SuitRules
    {
        /// <summary>
        /// One step. Returns the damage taken this step (HP). The caller treats damage at or above SpaceSuit.LethalDamage as death.
        /// vacuum: the player is outside a pressurised volume. helmetSealed: the visor is closed.
        /// </summary>
        public static double Step(SuitState state, int[] worn, SuitActivity activity, bool vacuum, bool helmetSealed, double envTempC, double dt)
        {
            SuitTier? tier = SpaceSuit.EffectiveTier(worn);
            if (!vacuum) return 0.0;
            if (!tier.HasValue || !helmetSealed) return SpaceSuit.LethalDamage;

            SuitSpec spec = SpaceSuit.Spec(tier.Value);
            double damage = 0.0;
            if (state.OxygenMinutes > 0)
            {
                state.OxygenMinutes = Math.Max(0.0, state.OxygenMinutes - SpaceSuit.ConsumptionMultiplier(activity) * dt / 60.0);
            }
            else
            {
                damage += 4.0 * dt;
            }
            if (envTempC < spec.MinTempC || envTempC > spec.MaxTempC) damage += 1.0 * dt;
            return damage;
        }
    }
}
