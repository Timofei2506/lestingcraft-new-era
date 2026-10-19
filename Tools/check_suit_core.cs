using System;
using LestingCraft.Gameplay;

// Stage 4 harness: cyberpunk suit rules (full set, helmet seal, oxygen by activity, thermal range, vacuum damage). Pure C#, runs under mono.
class SuitCheck
{
    static int tests;
    static void Check(bool x, string name) { tests++; if (!x) throw new Exception(name); }

    static int[] Full(int tier) { return new[] { tier, tier, tier, tier, tier, tier }; }

    static double Minutes(SuitTier tier, SuitActivity act)
    {
        var spec = SpaceSuit.Spec(tier);
        var state = new SuitState(spec);
        int worn0 = (int)tier;
        int[] worn = Full(worn0);
        int secs = 0;
        while (!state.Empty && secs < 100000)
        {
            SuitRules.Step(state, worn, act, true, true, 20, 1.0);
            secs++;
        }
        return secs / 60.0;
    }

    static void Main()
    {
        // 1. Full set and tier rules.
        int[] five = { 1, 1, 1, 1, 1, 0 };
        Check(SpaceSuit.EffectiveTier(five) == null, "five pieces are not a suit");
        Check(SpaceSuit.EffectiveTier(new[] { 3, 3, 3, 3, 3, 3 }) == SuitTier.Mk3, "full Mk3 set");
        Check(SpaceSuit.EffectiveTier(new[] { 3, 3, 2, 3, 3, 3 }) == SuitTier.Mk2, "mixed set uses the lowest tier");
        Check(SpaceSuit.EffectiveTier(new[] { 3, 3, 3, 3, 3, 4 }) == null, "unknown tier is rejected");
        Check(SpaceSuit.EffectiveTier(null) == null, "null is not a suit");
        Check(SpaceSuit.PieceCount == 6, "six pieces");

        // 2. Oxygen by activity: Mk1 tank 90 min at walking pace.
        double walk = Minutes(SuitTier.Mk1, SuitActivity.Walk);
        Check(Math.Abs(walk - 90) < 0.02, "walk drains 90 min tank in 90 min, got " + walk);
        double rest = Minutes(SuitTier.Mk1, SuitActivity.Rest);
        Check(Math.Abs(rest - 180) < 0.05, "rest doubles the time to 180 min, got " + rest);
        double run = Minutes(SuitTier.Mk1, SuitActivity.Run);
        Check(Math.Abs(run - 45) < 0.02, "run halves to 45 min, got " + run);
        double jet = Minutes(SuitTier.Mk1, SuitActivity.JetPack);
        Check(Math.Abs(jet - 60) < 0.02, "jet pack use 1.5x gives 60 min, got " + jet);
        double mk3walk = Minutes(SuitTier.Mk3, SuitActivity.Walk);
        Check(Math.Abs(mk3walk - 240) < 0.02, "Mk3 tank 240 min, got " + mk3walk);

        // 3. Vacuum needs a full set and a sealed helmet.
        var s1 = new SuitState(SpaceSuit.Spec(SuitTier.Mk1));
        double dmgOpen = SuitRules.Step(s1, Full(1), SuitActivity.Walk, true, false, 20, 1.0);
        Check(dmgOpen >= SpaceSuit.LethalDamage, "open helmet in vacuum is lethal");
        double dmgIncomplete = SuitRules.Step(s1, five, SuitActivity.Walk, true, true, 20, 1.0);
        Check(dmgIncomplete >= SpaceSuit.LethalDamage, "incomplete suit in vacuum is lethal");
        double dmgAtmo = SuitRules.Step(s1, five, SuitActivity.Run, false, false, 20, 1.0);
        Check(dmgAtmo == 0.0, "atmosphere does not hurt and drains nothing");
        Check(Math.Abs(s1.OxygenMinutes - 90) < 1e-9, "atmosphere keeps the tank full");

        // 4. Empty tank damages in vacuum.
        var empty = new SuitState(SpaceSuit.Spec(SuitTier.Mk1)) { OxygenMinutes = 0 };
        double dmgEmpty = SuitRules.Step(empty, Full(1), SuitActivity.Rest, true, true, 20, 1.0);
        Check(dmgEmpty > 0 && dmgEmpty < SpaceSuit.LethalDamage, "empty tank hurts without killing in one second");
        Check(empty.OxygenMinutes == 0, "oxygen never goes negative");

        // 5. Thermal range: Mk1 tolerates up to 90 C, so a sunlit Moon at 120 C hurts, Mk3 up to 125 C does not.
        var t1 = new SuitState(SpaceSuit.Spec(SuitTier.Mk1));
        double hot1 = SuitRules.Step(t1, Full(1), SuitActivity.Rest, true, true, 120, 1.0);
        Check(hot1 > 0, "Mk1 in 120 C sun takes damage");
        var t3 = new SuitState(SpaceSuit.Spec(SuitTier.Mk3));
        double hot3 = SuitRules.Step(t3, Full(3), SuitActivity.Rest, true, true, 120, 1.0);
        Check(hot3 == 0, "Mk3 in 120 C sun is fine");
        double night = SuitRules.Step(t3, Full(3), SuitActivity.Rest, true, true, -170, 1.0);
        Check(night > 0, "Mk3 at -170 C night takes damage");

        // 6. Spec numbers are internally consistent: higher tier, more oxygen and fuel and a wider range.
        var s2 = SpaceSuit.Spec(SuitTier.Mk2);
        var s3 = SpaceSuit.Spec(SuitTier.Mk3);
        Check(s2.TankMinutes > SpaceSuit.Spec(SuitTier.Mk1).TankMinutes && s3.TankMinutes > s2.TankMinutes, "tank grows with tier");
        Check(s3.JetFuelKg > s2.JetFuelKg && s2.JetFuelKg > SpaceSuit.Spec(SuitTier.Mk1).JetFuelKg, "jet fuel grows with tier");
        Check(s3.RadiationShield > s2.RadiationShield, "shielding grows with tier");

        Console.WriteLine("budget: Mk1 walk " + walk.ToString("F1") + " min, rest " + rest.ToString("F1") + " min, run " + run.ToString("F1") + " min, jet " + jet.ToString("F1") + " min");
        Console.WriteLine("PASS: " + tests + " suit checks (full set, oxygen by activity, vacuum, thermal range, tier scaling)");
    }
}
