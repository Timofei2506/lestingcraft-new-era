using System;
namespace LestingCraft.Gameplay
{
    /// <summary>
    /// Stage-1 space fuel chain: crude oil -> refinery -> kerosene; compressed air -> cryo -> liquid oxygen;
    /// kerosene + liquid oxygen -> rocket propellant. Pure logic, no Unity.
    /// Mass model: one item unit = 1 kg. Every process either conserves mass or declares its loss in Process.Loss.
    /// Values are game defaults (see Космос-дизайн-этап1.md, решения по умолчанию), not a plant design.
    /// </summary>
    public static class FuelChain
    {
        public const int CrudeOil = 170, Gasoline = 171, Diesel = 172, Kerosene = 173, LiquidOxygen = 174, RocketFuel = 175, Residue = 176, CompressedAir = 177;

        public readonly struct Amount
        {
            public readonly int Item, Count;
            public Amount(int item, int count) { Item = item; Count = count; }
        }

        public sealed class Process
        {
            public readonly string Name;
            public readonly float Seconds;
            public readonly Amount[] Inputs, Outputs;
            public Process(string name, float seconds, Amount[] inputs, Amount[] outputs)
            {
                Name = name; Seconds = seconds; Inputs = inputs; Outputs = outputs;
            }
            public int InputMass { get { int n = 0; foreach (var a in Inputs) n += a.Count; return n; } }
            public int OutputMass { get { int n = 0; foreach (var a in Outputs) n += a.Count; return n; } }
            /// <summary>Declared mass loss per batch. Refinery and mixing conserve mass; cryo removes nitrogen and argon.</summary>
            public int Loss { get { return InputMass - OutputMass; } }
        }

        /// <summary>6 crude -> 1 gasoline, 2 diesel, 2 kerosene, 1 residue (tar). Mass conserved.</summary>
        public static readonly Process Distillation = new Process("Перегонка нефти", 15f,
            new[] { new Amount(CrudeOil, 6) },
            new[] { new Amount(Gasoline, 1), new Amount(Diesel, 2), new Amount(Kerosene, 2), new Amount(Residue, 1) });

        /// <summary>9 kg air -> 2 kg O2 (22.2 % vs 23.2 % in real air). The other 7 kg (N2, Ar) are declared loss.</summary>
        public static readonly Process Liquefaction = new Process("Криоконденсация кислорода", 20f,
            new[] { new Amount(CompressedAir, 9) },
            new[] { new Amount(LiquidOxygen, 2) });

        /// <summary>2 kerosene + 5 LOX -> 7 propellant. O/F = 2.5 (RP-1/LOX real ≈ 2.56). Mass conserved.</summary>
        public static readonly Process PropellantMix = new Process("Смешение ракетного топлива", 10f,
            new[] { new Amount(Kerosene, 2), new Amount(LiquidOxygen, 5) },
            new[] { new Amount(RocketFuel, 7) });

        public static readonly Process[] All = { Distillation, Liquefaction, PropellantMix };

        /// <summary>Whole batches the bag can supply right now.</summary>
        public static int MaxBatches(Inventory inv, Process process)
        {
            if (inv == null || process == null) return 0;
            int max = int.MaxValue;
            foreach (var a in process.Inputs) { int n = inv.Count(a.Item) / a.Count; if (n < max) max = n; }
            return max == int.MaxValue ? 0 : max;
        }

        /// <summary>
        /// Runs whole batches atomically: all inputs are consumed and all outputs added, or the inventory stays unchanged.
        /// Works on a copy, so a full bag or a short input never leaves half a batch.
        /// </summary>
        public static bool Run(Inventory inv, Process process, int batches)
        {
            if (inv == null || process == null || batches <= 0 || batches > MaxBatches(inv, process)) return false;
            var copy = inv.Clone();
            foreach (var a in process.Inputs) if (!copy.Remove(a.Item, a.Count * batches)) return false;
            foreach (var a in process.Outputs) if (!copy.Add(a.Item, a.Count * batches)) return false;
            inv.slots = copy.slots;
            inv.revision++;
            return true;
        }
    }
}
