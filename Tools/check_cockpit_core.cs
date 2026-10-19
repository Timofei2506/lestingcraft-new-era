using System;
using LestingCraft.Gameplay;

// Stage 1.4 UI harness: cockpit panel model. Every press must follow the launch sequence rules. Pure C#, runs under mono.
class CockpitCheck
{
    static int tests;
    static void Check(bool x, string name) { tests++; if (!x) throw new Exception(name); }

    static void Main()
    {
        // Fresh panel: bus is the only control that can be pressed.
        var ctl = new LaunchControl();
        var panel = new CockpitPanel(ctl);
        Check(panel.View(PanelControl.MainBus).Enabled, "bus enabled at start");
        string r = panel.Blocker(PanelControl.FuelFeed);
        Check(r == "Сначала включите главную шину", "fuel feed needs the bus, got " + r);
        Check(panel.Press(PanelControl.FuelFeed) != null, "press without bus is refused");
        Check(!ctl.IsOn(PanelSwitch.FuelFeed), "refused press changes nothing");
        string ign = panel.Blocker(PanelControl.Ignite);
        Check(ign.StartsWith("Не готово:") && ign.Contains("нет главной шины") && ign.Contains("нет командира"), "ignite lists every missing condition: " + ign);
        Check(panel.Press(PanelControl.Ignite) != null && ctl.Phase == LaunchPhase.Standby, "ignite refused in standby");

        // Bus on, then fuel feed, then the hatch is still open so arming is refused.
        Check(panel.Press(PanelControl.MainBus) == null && ctl.IsOn(PanelSwitch.MainBus), "bus on");
        Check(panel.Press(PanelControl.FuelFeed) == null && ctl.IsOn(PanelSwitch.FuelFeed), "fuel feed on");
        Check(panel.Blocker(PanelControl.Arm) == "Закройте люк", "arm blocked while hatch open");
        Check(panel.Press(PanelControl.Hatch) != null, "closing the hatch with nobody aboard is refused");

        // Board the commander through the control model's rules, then close the hatch and arm.
        Check(ctl.Board(0, 1), "commander boards seat 0");
        Check(panel.Blocker(PanelControl.Arm) == "Закройте люк", "commander aboard but hatch still open: arm waits for the seal");
        Check(panel.Press(PanelControl.Hatch) == null && ctl.IsOn(PanelSwitch.Hatch), "hatch closes with crew aboard");
        Check(panel.Press(PanelControl.Arm) == null && ctl.IsOn(PanelSwitch.Arm), "arm on");
        Check(ctl.Phase == LaunchPhase.Ready, "ready after the chain");
        Check(panel.Blocker(PanelControl.Ignite) == null && panel.View(PanelControl.Ignite).Enabled, "ignite enabled when ready");
        Check(panel.LaunchLamp() == LampState.Green, "green lamp when ready");

        // Switches stay live in Ready: pulling the bus cuts fuel feed and arm.
        Check(panel.Press(PanelControl.MainBus) == null, "bus off is allowed in Ready");
        Check(!ctl.IsOn(PanelSwitch.FuelFeed) && !ctl.IsOn(PanelSwitch.Arm), "bus off cuts dependants");
        Check(panel.Press(PanelControl.MainBus) == null && ctl.IsOn(PanelSwitch.MainBus), "bus back on");
        Check(panel.Press(PanelControl.FuelFeed) == null && ctl.IsOn(PanelSwitch.FuelFeed), "fuel feed back on");
        Check(panel.Press(PanelControl.Arm) == null && ctl.IsOn(PanelSwitch.Arm), "arm back on, hatch still closed with commander");
        Check(ctl.Phase == LaunchPhase.Ready, "ready again");

        // Ignite starts the countdown, abort returns to standby.
        Check(panel.Press(PanelControl.Abort) != null, "abort refused before countdown");
        Check(panel.Press(PanelControl.Ignite) == null && ctl.Phase == LaunchPhase.Countdown, "ignite starts countdown");
        Check(panel.MissionClock() == "T-00:00:10", "clock starts at T-00:00:10, got " + panel.MissionClock());
        Check(panel.Blocker(PanelControl.MainBus) == "Переключатели заблокированы после начала отсчёта", "switches locked in countdown");
        Check(panel.Press(PanelControl.MainBus) != null && ctl.IsOn(PanelSwitch.MainBus), "bus cannot be cut in countdown");
        Check(panel.LaunchLamp() == LampState.Amber, "amber lamp in countdown");
        ctl.Advance(4);
        Check(panel.MissionClock() == "T-00:00:06", "clock counts down, got " + panel.MissionClock());
        Check(panel.Press(PanelControl.Abort) == null && ctl.Phase == LaunchPhase.Standby, "abort in countdown returns to standby");
        Check(!ctl.IsOn(PanelSwitch.Arm), "abort disarms");
        Check(panel.PhaseLabel() == "Ожидание", "standby label");

        // Full run: rebuild to Ready, ignite, fly through ascent and into orbit.
        ctl.SetSwitch(PanelSwitch.Arm, true);
        Check(panel.Press(PanelControl.Ignite) == null, "second ignite");
        ctl.Advance(10);
        Check(ctl.Phase == LaunchPhase.Ascent && panel.MissionClock() == "T+00:00:00", "ignition at T+0");
        Check(panel.Press(PanelControl.MainBus) != null, "no switch changes in ascent");
        ctl.Advance(125);
        Check(panel.MissionClock() == "T+00:02:05", "clock in ascent, got " + panel.MissionClock());
        ctl.Advance(600);
        Check(ctl.Phase == LaunchPhase.Orbit && panel.PhaseLabel() == "Орбита" && panel.LaunchLamp() == LampState.Green, "orbit reached");
        Check(panel.Press(PanelControl.Ignite) != null && panel.Press(PanelControl.Abort) != null, "no ignite or abort in orbit");

        // Clock formats hours.
        Check(new CockpitPanel(new LaunchControl()).MissionClock() == "T-00:00:10", "standby clock formatting");

        // Acceleration lamp against the 3 g limit.
        Check(CockpitPanel.GLamp(1.0) == LampState.Green, "1 g green");
        Check(CockpitPanel.GLamp(2.6) == LampState.Amber, "2.6 g amber");
        Check(CockpitPanel.GLamp(3.05) == LampState.Red, "above 3 g red");
        Check(CockpitPanel.GLamp(3.0) == LampState.Amber, "exactly 3 g is still amber, not above the limit");

        Console.WriteLine("PASS: " + tests + " cockpit checks (locks, reasons, countdown, abort, ascent, lamps)");
    }
}
