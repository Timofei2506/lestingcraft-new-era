using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.Gameplay;

namespace LestingCraft.Runtime
{
    public enum CockpitControlId
    {
        None = 0,
        MainBusSwitch,
        FuelFeedSwitch,
        HatchSwitch,
        MasterArmCover,
        MasterArmSwitch,
        IgniteButton,
        AbortButton,
        ThrottleLever,
        FlightStick,
        HelmModeButton,
        MfdTargetPrev,
        MfdTargetNext,
        MfdCycleSystem,
        MfdToggleGalaxy,
        HyperjumpButton,
        LandOrDockButton
    }

    public struct CockpitActionResult
    {
        public bool Triggered;
        public CockpitControlId Control;
        public string MessageRu;
        public bool RequestedIgnite;
        public bool RequestedAbort;
        public bool RequestedHyperjump;
        public bool RequestedLandOrDock;
    }

    public sealed class CockpitControlHitbox : MonoBehaviour
    {
        public CockpitControlId ControlId;
        public string Label;
        public Transform MovingPivot;
    }

    public sealed class CockpitInteractiveRig : MonoBehaviour
    {
        public readonly Dictionary<CockpitControlId, CockpitControlHitbox> Controls =
            new Dictionary<CockpitControlId, CockpitControlHitbox>();

        public bool SafetyCoverOpen { get; private set; }
        public CockpitControlId? HoveredControl { get; private set; }
        public Vector2 StickDeflection { get; private set; }
        public string HoveredLabel => HoveredControl.HasValue && Controls.TryGetValue(HoveredControl.Value, out var h)
            ? h.Label : string.Empty;

        CockpitPanel panel;
        OpenSpaceHelm helm;
        Material metalDark, metalLight, redMat, greenMat, amberMat, cyanScreenMat;
        TextMesh mfdReadout;
        CockpitControlId? activeDragControl;

        public static CockpitInteractiveRig Create(Transform cabinParent, CockpitPanel cockpitPanel, OpenSpaceHelm spaceHelm)
        {
            var go = new GameObject("Interactive3DCockpitRig");
            var rig = go.AddComponent<CockpitInteractiveRig>();
            rig.Build(cabinParent, cockpitPanel, spaceHelm);
            return rig;
        }

        public void BindHelm(OpenSpaceHelm targetHelm)
        {
            helm = targetHelm;
            RefreshVisualPoses();
        }

        public void Build(Transform cabinParent, CockpitPanel cockpitPanel, OpenSpaceHelm spaceHelm)
        {
            panel = cockpitPanel;
            helm = spaceHelm;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            metalDark = MakeMat(shader, new Color(.14f, .16f, .20f), .65f, .7f);
            metalLight = MakeMat(shader, new Color(.72f, .76f, .82f), .8f, .6f);
            redMat = MakeMat(shader, new Color(.86f, .14f, .12f), .2f, .65f);
            greenMat = MakeMat(shader, new Color(.16f, .86f, .38f), .2f, .65f);
            amberMat = MakeMat(shader, new Color(.98f, .68f, .14f), .25f, .65f);
            cyanScreenMat = MakeMat(shader, new Color(.05f, .15f, .24f), .1f, .85f);

            transform.SetParent(cabinParent, false);
            transform.localPosition = Vector3.zero;

            var console = Box("PilotInteractiveConsole", transform, new Vector3(.48f, 43.08f, 5.38f),
                new Vector3(.92f, .10f, .52f), metalDark, false);
            console.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);

            AddToggleSwitch(CockpitControlId.MainBusSwitch, "ГЛАВНАЯ ШИНА ПИТАНИЯ", console.transform, new Vector3(-.31f, .06f, .12f), amberMat);
            AddToggleSwitch(CockpitControlId.FuelFeedSwitch, "ПОДАЧА КРИОТОПЛИВА", console.transform, new Vector3(-.12f, .06f, .12f), greenMat);
            AddToggleSwitch(CockpitControlId.HatchSwitch, "ГЕРМОЗАТВОР ЛЮКА", console.transform, new Vector3(.07f, .06f, .12f), amberMat);

            var guardPivot = new GameObject("MasterArmGuardPivot").transform;
            guardPivot.SetParent(console.transform, false);
            guardPivot.localPosition = new Vector3(.27f, .065f, .15f);
            var guardCap = Box("MasterArmCoverMesh", guardPivot, new Vector3(0f, .035f, -.03f), new Vector3(.10f, .07f, .11f), redMat, true);
            RegisterHitbox(guardCap, CockpitControlId.MasterArmCover, "ЗАЩИТНЫЙ КОЛПАК «ВЗВОД»", guardPivot);
            AddToggleSwitch(CockpitControlId.MasterArmSwitch, "ТУМБЛЕР «ГЛАВНЫЙ ВЗВОД»", console.transform, new Vector3(.27f, .055f, .12f), redMat);

            AddPushButton(CockpitControlId.IgniteButton, "КНОПКА «ПУСК ДВИГАТЕЛЕЙ»", console.transform, new Vector3(-.22f, .06f, -.08f), greenMat, 1.25f);
            AddPushButton(CockpitControlId.AbortButton, "АВАРИЙНЫЙ СБРОС (ABORT)", console.transform, new Vector3(-.02f, .06f, -.08f), redMat, 1.15f);
            AddPushButton(CockpitControlId.HelmModeButton, "РЕЖИМ ШТУРВАЛА: РУЧНОЙ / АВТО", console.transform, new Vector3(.22f, .06f, -.08f), amberMat, 1.0f);

            var quadrant = Box("ThrottleQuadrant", transform, new Vector3(-.14f, 42.92f, 5.02f), new Vector3(.16f, .14f, .44f), metalDark, false);
            var throttlePivot = new GameObject("ThrottlePivot").transform;
            throttlePivot.SetParent(quadrant.transform, false);
            throttlePivot.localPosition = new Vector3(0f, .05f, -.12f);
            Box("ThrottleStem", throttlePivot, new Vector3(0f, .10f, 0f), new Vector3(.03f, .20f, .03f), metalLight, false);
            var grip = Box("ThrottleGrip", throttlePivot, new Vector3(0f, .21f, 0f), new Vector3(.12f, .055f, .055f), amberMat, true);
            RegisterHitbox(grip, CockpitControlId.ThrottleLever, "РУД — РЫЧАГ ТЯГИ (ЛКМ / КОЛЕСО МЫШИ)", throttlePivot);

            var stickBase = Box("FlightStickBase", transform, new Vector3(.48f, 42.56f, 4.92f), new Vector3(.14f, .08f, .14f), metalDark, false);
            var stickPivot = new GameObject("FlightStickPivot").transform;
            stickPivot.SetParent(stickBase.transform, false);
            stickPivot.localPosition = new Vector3(0f, .04f, 0f);
            Box("FlightStickColumn", stickPivot, new Vector3(0f, .18f, 0f), new Vector3(.035f, .36f, .035f), metalLight, false);
            var stickHead = Box("FlightStickGrip", stickPivot, new Vector3(0f, .37f, .02f), new Vector3(.09f, .11f, .09f), redMat, true);
            RegisterHitbox(stickHead, CockpitControlId.FlightStick, "РУС — ШТУРВАЛ ОРИЕНТАЦИИ 6-DOF (ЗАЖАТЬ ЛКМ)", stickPivot);

            var mfdFrame = Box("PilotMfdHousing", transform, new Vector3(-.38f, 43.56f, 5.66f), new Vector3(.66f, .42f, .06f), metalDark, false);
            Box("PilotMfdScreen", mfdFrame.transform, new Vector3(0f, .03f, -.035f), new Vector3(.58f, .28f, .01f), cyanScreenMat, false);

            var readoutGo = new GameObject("MfdReadoutText");
            readoutGo.transform.SetParent(mfdFrame.transform, false);
            readoutGo.transform.localPosition = new Vector3(-.265f, .145f, -.045f);
            readoutGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            readoutGo.transform.localScale = Vector3.one * 0.0115f;
            mfdReadout = readoutGo.AddComponent<TextMesh>();
            mfdReadout.characterSize = 1f;
            mfdReadout.fontSize = 26;
            mfdReadout.color = new Color(.58f, .96f, 1f);

            AddBezelButton(CockpitControlId.MfdTargetPrev, "◀ ПРЕД. ПЛАНЕТА / ЦЕЛЬ", mfdFrame.transform, new Vector3(-.24f, -.15f, -.04f), metalLight);
            AddBezelButton(CockpitControlId.MfdTargetNext, "СЛЕД. ПЛАНЕТА / ЦЕЛЬ ▶", mfdFrame.transform, new Vector3(-.12f, -.15f, -.04f), metalLight);
            AddBezelButton(CockpitControlId.MfdCycleSystem, "★ ЗВЁЗДНАЯ СИСТЕМА", mfdFrame.transform, new Vector3(.0f, -.15f, -.04f), amberMat);
            AddBezelButton(CockpitControlId.MfdToggleGalaxy, "✦ ГАЛАКТИКА (МЛЕЧНЫЙ ПУТЬ / M31)", mfdFrame.transform, new Vector3(.12f, -.15f, -.04f), amberMat);
            AddBezelButton(CockpitControlId.HyperjumpButton, "⚡ ГИПЕРПРЫЖОК", mfdFrame.transform, new Vector3(.24f, -.15f, -.04f), greenMat);
            AddBezelButton(CockpitControlId.LandOrDockButton, "▼ ПОСАДКА / СТЫКОВКА", mfdFrame.transform, new Vector3(.24f, .15f, -.04f), greenMat);

            RefreshVisualPoses();
        }

        void AddToggleSwitch(CockpitControlId id, string label, Transform parent, Vector3 pos, Material tipMat)
        {
            Box(id + "_Base", parent, pos, new Vector3(.07f, .025f, .07f), metalLight, false);
            var pivot = new GameObject(id + "_Pivot").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = pos + new Vector3(0f, .015f, 0f);
            var lever = Box(id + "_Lever", pivot, new Vector3(0f, .04f, 0f), new Vector3(.026f, .085f, .026f), tipMat, true);
            var col = lever.GetComponent<BoxCollider>();
            if (col) col.size = new Vector3(2.4f, 1.5f, 2.4f);
            RegisterHitbox(lever, id, label, pivot);
        }

        void AddPushButton(CockpitControlId id, string label, Transform parent, Vector3 pos, Material mat, float scale)
        {
            var collar = Box(id + "_Collar", parent, pos, new Vector3(.10f * scale, .02f, .10f * scale), metalLight, false);
            var plunger = Box(id + "_Cap", collar.transform, new Vector3(0f, .022f, 0f), new Vector3(.075f * scale, .035f, .075f * scale), mat, true);
            var col = plunger.GetComponent<BoxCollider>();
            if (col) col.size = new Vector3(1.6f, 2.2f, 1.6f);
            RegisterHitbox(plunger, id, label, plunger.transform);
        }

        void AddBezelButton(CockpitControlId id, string label, Transform parent, Vector3 pos, Material mat)
        {
            var btn = Box(id + "_Bezel", parent, pos, new Vector3(.095f, .05f, .025f), mat, true);
            var col = btn.GetComponent<BoxCollider>();
            if (col) col.size = new Vector3(1.15f, 1.35f, 3.0f);
            RegisterHitbox(btn, id, label, btn.transform);
        }

        void RegisterHitbox(GameObject go, CockpitControlId id, string label, Transform movingPivot)
        {
            var hb = go.AddComponent<CockpitControlHitbox>();
            hb.ControlId = id;
            hb.Label = label;
            hb.MovingPivot = movingPivot;
            Controls[id] = hb;
        }

        static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr) mr.sharedMaterial = mat;
            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (c) SafeDestroy(c);
            }
            return go;
        }

        static Material MakeMat(Shader sh, Color c, float metallic, float smooth)
        {
            var m = new Material(sh) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        public CockpitActionResult ActivateControl(CockpitControlId id)
        {
            var res = new CockpitActionResult { Triggered = true, Control = id };
            switch (id)
            {
                case CockpitControlId.MainBusSwitch:
                    if (panel != null) panel.Press(PanelControl.MainBus);
                    res.MessageRu = "Главная шина питания: " + (panel != null && panel.MainBus ? "ВКЛ" : "ВЫКЛ");
                    break;
                case CockpitControlId.FuelFeedSwitch:
                    if (panel != null) panel.Press(PanelControl.FuelFeed);
                    res.MessageRu = "Подача криотоплива: " + (panel != null && panel.FuelFeed ? "ОТКРЫТА" : "ЗАКРЫТА");
                    break;
                case CockpitControlId.HatchSwitch:
                    if (panel != null) panel.Press(PanelControl.Hatch);
                    res.MessageRu = "Привод гермолюка переключён";
                    break;
                case CockpitControlId.MasterArmCover:
                    SafetyCoverOpen = !SafetyCoverOpen;
                    if (!SafetyCoverOpen && panel != null && panel.Arm) panel.Press(PanelControl.Arm);
                    res.MessageRu = SafetyCoverOpen ? "Защитный колпак ВЗВОД откинут" : "Защитный колпак ВЗВОД закрыт";
                    break;
                case CockpitControlId.MasterArmSwitch:
                    SafetyCoverOpen = true;
                    if (panel != null) panel.Press(PanelControl.Arm);
                    res.MessageRu = panel != null && panel.Arm ? "ГЛАВНЫЙ ВЗВОД АКТИВЕН — готов к пуску!" : "Главный взвод снят (проверьте шину, топливо и люк)";
                    break;
                case CockpitControlId.IgniteButton:
                    if (panel != null) panel.Press(PanelControl.Ignite);
                    res.RequestedIgnite = true;
                    res.MessageRu = "ЗАЖИГАНИЕ! Команда пуска передана двигателям!";
                    break;
                case CockpitControlId.AbortButton:
                    if (panel != null) panel.Press(PanelControl.Abort);
                    res.RequestedAbort = true;
                    res.MessageRu = "АВАРИЙНЫЙ СБРОС!";
                    break;
                case CockpitControlId.HelmModeButton:
                    if (helm != null) helm.SetManualControl(!helm.ManualControl);
                    res.MessageRu = helm != null && helm.ManualControl ? "Штурвал: РУЧНОЙ 6-DOF РЕЖИМ" : "Штурвал: АВТОПИЛОТ НАВЕДЕНИЯ";
                    break;
                case CockpitControlId.MfdTargetPrev:
                    if (helm != null) res.MessageRu = "Цель MFD: " + helm.CycleTarget(-1).NameRu;
                    break;
                case CockpitControlId.MfdTargetNext:
                    if (helm != null) res.MessageRu = "Цель MFD: " + helm.CycleTarget(1).NameRu;
                    break;
                case CockpitControlId.MfdCycleSystem:
                    if (helm != null)
                    {
                        var nextSys = (StarSystemId)(((int)helm.CurrentSystem + 1) % CelestialAtlas.Systems.Length);
                        helm.StartHyperjump(nextSys);
                        res.RequestedHyperjump = true;
                        res.MessageRu = "Межзвёздный вектор → " + CelestialAtlas.GetSystem(nextSys).NameRu;
                    }
                    break;
                case CockpitControlId.MfdToggleGalaxy:
                    if (helm != null)
                    {
                        var nextGal = helm.CurrentGalaxy == GalaxyId.MilkyWay ? GalaxyId.Andromeda : GalaxyId.MilkyWay;
                        helm.JumpToGalaxy(nextGal);
                        res.RequestedHyperjump = true;
                        res.MessageRu = "МЕЖГАЛАКТИЧЕСКИЙ ПЕРЕХОД → " + CelestialAtlas.GalaxyNameRu(nextGal);
                    }
                    break;
                case CockpitControlId.HyperjumpButton:
                    if (helm != null) helm.StartHyperjump(helm.TargetBody);
                    res.RequestedHyperjump = true;
                    res.MessageRu = "Гиперпрыжок к выбранному небесному телу!";
                    break;
                case CockpitControlId.LandOrDockButton:
                    res.RequestedLandOrDock = true;
                    res.MessageRu = "Запрос посадки / стыковки...";
                    break;
                default:
                    res.Triggered = false;
                    break;
            }
            RefreshVisualPoses();
            return res;
        }

        public CockpitActionResult StepMouseInteraction(
            Ray aimRay, bool mousePressed, bool mouseHeld, bool mouseReleased, Vector2 mouseDelta, float scrollDelta)
        {
            var result = default(CockpitActionResult);
            HoveredControl = null;

            if (Physics.Raycast(aimRay, out RaycastHit hit, 3.2f) && hit.collider)
            {
                var hb = hit.collider.GetComponent<CockpitControlHitbox>() ?? hit.collider.GetComponentInParent<CockpitControlHitbox>();
                if (hb != null) HoveredControl = hb.ControlId;
            }

            if (HoveredControl == CockpitControlId.ThrottleLever && Mathf.Abs(scrollDelta) > 0.001f && helm != null)
            {
                helm.SetThrottle(helm.Throttle + scrollDelta);
                result.Triggered = true;
                result.Control = CockpitControlId.ThrottleLever;
                result.MessageRu = $"Тяга двигателей (РУД): {Mathf.RoundToInt(helm.Throttle * 100f)}%";
            }

            if (mousePressed && HoveredControl.HasValue)
            {
                var ctrl = HoveredControl.Value;
                if (ctrl == CockpitControlId.ThrottleLever || ctrl == CockpitControlId.FlightStick)
                    activeDragControl = ctrl;
                else
                    result = ActivateControl(ctrl);
            }

            if (mouseHeld && activeDragControl.HasValue)
            {
                if (activeDragControl.Value == CockpitControlId.ThrottleLever && helm != null)
                {
                    helm.SetThrottle(helm.Throttle + mouseDelta.y * 0.005f);
                }
                else if (activeDragControl.Value == CockpitControlId.FlightStick)
                {
                    StickDeflection = new Vector2(
                        Mathf.Clamp(StickDeflection.x + mouseDelta.x * 0.015f, -1f, 1f),
                        Mathf.Clamp(StickDeflection.y + mouseDelta.y * 0.015f, -1f, 1f));
                }
            }
            else if (mouseReleased || !mouseHeld)
            {
                activeDragControl = null;
                StickDeflection = Vector2.Lerp(StickDeflection, Vector2.zero, 0.35f);
            }

            RefreshVisualPoses();
            return result;
        }

        public void RefreshVisualPoses(float dt = 0f)
        {
            if (panel != null)
            {
                SetToggleAngle(CockpitControlId.MainBusSwitch, panel.MainBus);
                SetToggleAngle(CockpitControlId.FuelFeedSwitch, panel.FuelFeed);
                SetToggleAngle(CockpitControlId.HatchSwitch, panel.HatchSealed);
                SetToggleAngle(CockpitControlId.MasterArmSwitch, panel.Arm);
            }
            if (Controls.TryGetValue(CockpitControlId.MasterArmCover, out var cover) && cover.MovingPivot)
                cover.MovingPivot.localRotation = Quaternion.Euler(SafetyCoverOpen ? -92f : 0f, 0f, 0f);
            if (Controls.TryGetValue(CockpitControlId.MasterArmSwitch, out var armSw))
            {
                var armCol = armSw.GetComponent<BoxCollider>();
                if (armCol) armCol.enabled = SafetyCoverOpen;
            }

            if (Controls.TryGetValue(CockpitControlId.ThrottleLever, out var thr) && thr.MovingPivot)
            {
                float t = helm != null ? helm.Throttle : 0f;
                thr.MovingPivot.localPosition = new Vector3(0f, .05f, Mathf.Lerp(-.14f, .15f, t));
                thr.MovingPivot.localRotation = Quaternion.Euler(Mathf.Lerp(26f, -28f, t), 0f, 0f);
            }

            if (Controls.TryGetValue(CockpitControlId.FlightStick, out var stick) && stick.MovingPivot)
                stick.MovingPivot.localRotation = Quaternion.Euler(StickDeflection.y * 22f, 0f, -StickDeflection.x * 22f);

            if (mfdReadout && helm != null)
            {
                var sys = CelestialAtlas.GetSystem(helm.CurrentSystem);
                var target = CelestialAtlas.GetBody(helm.TargetBody);
                string mode = helm.Active
                    ? (helm.InHyperjump ? "ГИПЕРПРЫЖОК..." : (helm.ManualControl ? "РУЧНОЙ 6-DOF" : "АВТОПИЛОТ"))
                    : (panel != null && panel.Arm ? "ГОТОВ К ПУСКУ!" : "ПРЕДПОЛЁТНЫЙ");
                mfdReadout.text =
                    $"ГАЛАКТИКА: {CelestialAtlas.GalaxyNameRu(helm.CurrentGalaxy)}\n" +
                    $"СИСТЕМА: {sys.NameRu} [{sys.SpectralType}]\n" +
                    $"ЦЕЛЬ: {target.NameRu} ({helm.DistanceToTargetKm():0} км)\n" +
                    $"РУД: {Mathf.RoundToInt(helm.Throttle * 100f)}% | СТАТУС: {mode}";
            }
        }

        void SetToggleAngle(CockpitControlId id, bool on)
        {
            if (Controls.TryGetValue(id, out var c) && c.MovingPivot)
                c.MovingPivot.localRotation = Quaternion.Euler(on ? 28f : -28f, 0f, 0f);
        }

        static void SafeDestroy(UnityEngine.Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        void OnDestroy()
        {
            SafeDestroy(metalDark);
            SafeDestroy(metalLight);
            SafeDestroy(redMat);
            SafeDestroy(greenMat);
            SafeDestroy(amberMat);
            SafeDestroy(cyanScreenMat);
        }
    }
}
