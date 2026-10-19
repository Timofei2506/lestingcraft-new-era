using System;
using System.Collections.Generic;

namespace LestingCraft.Gameplay
{
    public enum PanelControl { MainBus, FuelFeed, Arm, Hatch, Ignite, Abort }

    public enum LampState { Off, Green, Amber, Red }

    public struct ControlView
    {
        public PanelControl Control;
        public string Label;
        /// <summary>Latched state for switches (hatch: true means closed). Buttons are always false.</summary>
        public bool On;
        public bool Enabled;
        /// <summary>Why the control is locked right now. Null when it can be pressed.</summary>
        public string Blocker;
    }

    /// <summary>
    /// Presentation model of the cockpit panel. It holds no Unity types: the view layer draws the output and forwards presses.
    /// Every rule comes from LaunchControl, so the panel can never allow an action that the sequence would refuse.
    /// Reasons are Russian strings shown to the player.
    /// </summary>
    public sealed class CockpitPanel
    {
        /// <summary>Limit the stack must not exceed, in g. Above it the lamp goes red.</summary>
        public const double GLimit = 3.0;
        public const double GWarning = 2.5;

        readonly LaunchControl ctl;

        public CockpitPanel(LaunchControl control)
        {
            if (control == null) throw new ArgumentNullException("control");
            ctl = control;
        }

        public LaunchControl Control { get { return ctl; } }

        static string Label(PanelControl c)
        {
            switch (c)
            {
                case PanelControl.MainBus: return "Главная шина";
                case PanelControl.FuelFeed: return "Подача топлива";
                case PanelControl.Arm: return "Взвод";
                case PanelControl.Hatch: return "Люк";
                case PanelControl.Ignite: return "Зажигание";
                default: return "Отмена";
            }
        }

        bool Editable { get { return ctl.Phase == LaunchPhase.Standby || ctl.Phase == LaunchPhase.Ready; } }

        /// <summary>Null when the control may be pressed now, otherwise the reason it is locked.</summary>
        public string Blocker(PanelControl c)
        {
            switch (c)
            {
                case PanelControl.MainBus:
                case PanelControl.FuelFeed:
                case PanelControl.Arm:
                case PanelControl.Hatch:
                    if (!Editable) return "Переключатели заблокированы после начала отсчёта";
                    break;
            }
            switch (c)
            {
                case PanelControl.FuelFeed:
                    if (!ctl.IsOn(PanelSwitch.FuelFeed) && !ctl.IsOn(PanelSwitch.MainBus)) return "Сначала включите главную шину";
                    break;
                case PanelControl.Arm:
                    if (ctl.IsOn(PanelSwitch.Arm)) return null;
                    if (!ctl.IsOn(PanelSwitch.FuelFeed)) return "Сначала включите подачу топлива";
                    if (ctl.HatchOpen) return "Закройте люк";
                    if (!ctl.Cabin.CommanderSeated) return "Командир должен сидеть на месте 0";
                    break;
                case PanelControl.Hatch:
                    if (ctl.HatchOpen && ctl.Cabin.Count == 0) return "В кабине никого нет, люк не закрыть";
                    break;
                case PanelControl.Ignite:
                    if (ctl.Phase == LaunchPhase.Ready) return null;
                    return "Не готово: " + string.Join(", ", MissingForLaunch());
                case PanelControl.Abort:
                    if (ctl.Phase != LaunchPhase.Countdown) return "Отмена возможна только в отсчёте";
                    break;
            }
            return null;
        }

        /// <summary>Conditions that still block ignition, in the order the panel checks them.</summary>
        public List<string> MissingForLaunch()
        {
            var list = new List<string>();
            if (ctl.Phase == LaunchPhase.Ready) return list;
            if (ctl.Phase != LaunchPhase.Standby) { list.Add("запуск уже идёт"); return list; }
            if (!ctl.IsOn(PanelSwitch.MainBus)) list.Add("нет главной шины");
            if (!ctl.IsOn(PanelSwitch.FuelFeed)) list.Add("нет подачи топлива");
            if (!ctl.IsOn(PanelSwitch.Arm)) list.Add("не взведено");
            if (ctl.HatchOpen) list.Add("люк открыт");
            if (!ctl.Cabin.CommanderSeated) list.Add("нет командира");
            return list;
        }

        public ControlView View(PanelControl c)
        {
            string blocker = Blocker(c);
            var v = new ControlView { Control = c, Label = Label(c), Blocker = blocker, Enabled = blocker == null };
            switch (c)
            {
                case PanelControl.MainBus: v.On = ctl.IsOn(PanelSwitch.MainBus); break;
                case PanelControl.FuelFeed: v.On = ctl.IsOn(PanelSwitch.FuelFeed); break;
                case PanelControl.Arm: v.On = ctl.IsOn(PanelSwitch.Arm); break;
                case PanelControl.Hatch: v.On = ctl.IsOn(PanelSwitch.Hatch); break;
                default: v.On = false; break;
            }
            return v;
        }

        /// <summary>Presses a control. Returns null on success, otherwise the reason the press was refused.</summary>
        public string Press(PanelControl c)
        {
            string blocker = Blocker(c);
            if (blocker != null) return blocker;
            switch (c)
            {
                case PanelControl.MainBus: ctl.SetSwitch(PanelSwitch.MainBus, !ctl.IsOn(PanelSwitch.MainBus)); break;
                case PanelControl.FuelFeed: ctl.SetSwitch(PanelSwitch.FuelFeed, !ctl.IsOn(PanelSwitch.FuelFeed)); break;
                case PanelControl.Arm: ctl.SetSwitch(PanelSwitch.Arm, !ctl.IsOn(PanelSwitch.Arm)); break;
                case PanelControl.Hatch: ctl.SetSwitch(PanelSwitch.Hatch, !ctl.IsOn(PanelSwitch.Hatch)); break;
                case PanelControl.Ignite: if (!ctl.Ignite()) return "Зажигание отклонено"; break;
                case PanelControl.Abort: if (!ctl.Abort()) return "Отмена отклонена"; break;
            }
            return null;
        }

        /// <summary>Mission clock: T-00:00:10 before ignition, T+00:02:05 after. Hours, minutes and seconds.</summary>
        public string MissionClock()
        {
            double t = ctl.T;
            char sign = t < 0 ? '-' : '+';
            long s = (long)Math.Floor(Math.Abs(t) + 1e-9);
            return string.Format("T{0}{1:D2}:{2:D2}:{3:D2}", sign, s / 3600, (s / 60) % 60, s % 60);
        }

        public string PhaseLabel()
        {
            switch (ctl.Phase)
            {
                case LaunchPhase.Standby: return "Ожидание";
                case LaunchPhase.Ready: return "Готов к зажиганию";
                case LaunchPhase.Countdown: return "Отсчёт";
                case LaunchPhase.Ascent: return "Подъём";
                default: return "Орбита";
            }
        }

        public LampState LaunchLamp()
        {
            switch (ctl.Phase)
            {
                case LaunchPhase.Ready: return LampState.Green;
                case LaunchPhase.Countdown: return LampState.Amber;
                case LaunchPhase.Ascent: return LampState.Amber;
                case LaunchPhase.Orbit: return LampState.Green;
                default: return LampState.Off;
            }
        }

        /// <summary>Acceleration lamp: green below the warning level, amber above it, red above the 3 g limit.</summary>
        public static LampState GLamp(double g)
        {
            if (g > GLimit) return LampState.Red;
            if (g > GWarning) return LampState.Amber;
            return LampState.Green;
        }
    }
}
