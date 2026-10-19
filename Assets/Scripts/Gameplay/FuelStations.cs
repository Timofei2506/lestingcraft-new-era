using System;

namespace LestingCraft.Gameplay
{
    /// <summary>
    /// Stage 1.3 space stations 49..53. Pure logic, no Unity.
    /// Oil well and compressor: one portion per press (6 kg crude oil or 9 kg air), added to the bag.
    /// Refinery, cryo unit and mixer: as many whole batches as the inputs and the bag allow, via FuelChain.
    /// Every action is atomic: the inventory changes only when the whole portion or batch fits.
    /// Timers and the process UI are not part of this step.
    /// </summary>
    public static class FuelStations
    {
        public const int OilWell = 49, Compressor = 50, Refinery = 51, Cryo = 52, Mixer = 53;
        public const int OilPortion = 6, AirPortion = 9;
        /// <summary>Room content version that first allows stations 49..53 (matches the server maxBlock for version 7).</summary>
        public const int RoomVersion = 7;

        public static bool Has(int id) => id >= OilWell && id <= Mixer;

        /// <summary>Uses a station and returns the player-facing notice.</summary>
        public static string Use(Inventory inv, int station)
        {
            if (inv == null) return "";
            switch (station)
            {
                case OilWell: return Portion(inv, FuelChain.CrudeOil, OilPortion, "Скважина: +6 сырой нефти");
                case Compressor: return Portion(inv, FuelChain.CompressedAir, AirPortion, "Компрессор: +9 сжатого воздуха");
                case Refinery: return Batches(inv, FuelChain.Distillation, "Перегонка");
                case Cryo: return Batches(inv, FuelChain.Liquefaction, "Криоконденсация");
                case Mixer: return Batches(inv, FuelChain.PropellantMix, "Смешение");
                default: return "";
            }
        }

        static string Portion(Inventory inv, int item, int count, string done)
        {
            return inv.Add(item, count) ? done : "Нет места в инвентаре";
        }

        static string Batches(Inventory inv, FuelChain.Process process, string title)
        {
            int most = FuelChain.MaxBatches(inv, process);
            if (most <= 0) return title + ": не хватает сырья для одной партии";
            // Try the largest count first; a full bag can still take fewer whole batches.
            for (int n = most; n > 0; n--)
                if (FuelChain.Run(inv, process, n)) return title + ": партий " + n;
            return title + ": нет места для результата";
        }
    }
}
