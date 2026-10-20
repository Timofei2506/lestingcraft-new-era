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
        // Seated pilot eye in VerticalShuttleInteriorVisual local coordinates (Seat 0 at -0.40, 42.0, 5.35)
        public static readonly Vector3 PilotEyeLocal = new Vector3(-0.40f, 43.62f, 5.35f);

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
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            // Every container & hitbox pivot has strict uniform localScale == Vector3.one (pure rigid SE(3)),
            // preventing PhysX non-uniform shear under rotation and preserving 1:1 physical meter proportions!
            var consoleRoot = CreateNode("PilotConsoleRoot", transform, new Vector3(0.24f, 43.34f, 5.63f),
                Quaternion.Euler(-62f, -56f, 0f));
            VisualMesh("PilotInteractiveConsolePlate", consoleRoot, new Vector3(0f, -0.025f, 0f),
                new Vector3(0.76f, 0.05f, 0.44f), metalDark);

            AddToggleSwitch(CockpitControlId.MainBusSwitch, "ГЛАВНАЯ ШИНА ПИТАНИЯ", consoleRoot, new Vector3(-0.25f, 0.01f, 0.11f), amberMat);
            AddToggleSwitch(CockpitControlId.FuelFeedSwitch, "ПОДАЧА КРИОТОПЛИВА", consoleRoot, new Vector3(-0.09f, 0.01f, 0.11f), greenMat);
            AddToggleSwitch(CockpitControlId.HatchSwitch, "ГЕРМОЗАТВОР ЛЮКА", consoleRoot, new Vector3(0.07f, 0.01f, 0.11f), amberMat);

            AddToggleSwitch(CockpitControlId.MasterArmSwitch, "ТУМБЛЕР «ГЛАВНЫЙ ВЗВОД»", consoleRoot, new Vector3(0.24f, 0.01f, 0.11f), redMat);
            var guardPivot = CreateNode("MasterArmGuardPivot", consoleRoot, new Vector3(0.24f, 0.02f, 0.155f), Quaternion.identity);
            var guardHit = HitboxNode("MasterArmCoverHitbox", guardPivot, new Vector3(0f, 0.045f, -0.045f), new Vector3(0.115f, 0.105f, 0.125f));
            VisualMesh("MasterArmCoverMesh", guardHit.transform, Vector3.zero, new Vector3(0.105f, 0.095f, 0.115f), redMat);
            RegisterHitbox(guardHit, CockpitControlId.MasterArmCover, "ЗАЩИТНЫЙ КОЛПАК «ВЗВОД»", guardPivot);

            AddPushButton(CockpitControlId.IgniteButton, "КНОПКА «ПУСК ДВИГАТЕЛЕЙ»", consoleRoot, new Vector3(-0.21f, 0.01f, -0.09f), greenMat, 1.15f);
            AddPushButton(CockpitControlId.AbortButton, "АВАРИЙНЫЙ СБРОС (ABORT)", consoleRoot, new Vector3(0.0f, 0.01f, -0.09f), redMat, 1.10f);
            AddPushButton(CockpitControlId.HelmModeButton, "РЕЖИМ ШТУРВАЛА: РУЧНОЙ / АВТО", consoleRoot, new Vector3(0.21f, 0.01f, -0.09f), amberMat, 1.05f);

            // Port HOTAS Throttle Quadrant (left hand of Pilot Seat 0, clear of restraint bar)
            var quadrantRoot = CreateNode("ThrottleQuadrantRoot", transform, new Vector3(-0.84f, 43.12f, 5.56f), Quaternion.Euler(0f, 16f, 0f));
            VisualMesh("ThrottleQuadrantHousing", quadrantRoot, new Vector3(0f, -0.04f, 0f), new Vector3(0.15f, 0.10f, 0.36f), metalDark);
            var throttlePivot = CreateNode("ThrottlePivot", quadrantRoot, new Vector3(0f, 0.02f, -0.08f), Quaternion.identity);
            VisualMesh("ThrottleStem", throttlePivot, new Vector3(0f, 0.09f, 0f), new Vector3(0.028f, 0.18f, 0.028f), metalLight);
            var gripHit = HitboxNode("ThrottleGripHitbox", throttlePivot, new Vector3(0f, 0.19f, 0f), new Vector3(0.14f, 0.075f, 0.075f));
            VisualMesh("ThrottleGripMesh", gripHit.transform, Vector3.zero, new Vector3(0.125f, 0.055f, 0.055f), amberMat);
            RegisterHitbox(gripHit, CockpitControlId.ThrottleLever, "РУД — РЫЧАГ ТЯГИ (ЛКМ / КОЛЕСО МЫШИ)", throttlePivot);

            // Center-Starboard 6-DOF Flight Stick (right hand of Pilot Seat 0, clear of restraint bar)
            var stickRoot = CreateNode("FlightStickRoot", transform, new Vector3(-0.04f, 43.02f, 5.54f), Quaternion.identity);
            VisualMesh("FlightStickBase", stickRoot, new Vector3(0f, -0.03f, 0f), new Vector3(0.12f, 0.06f, 0.12f), metalDark);
            var stickPivot = CreateNode("FlightStickPivot", stickRoot, Vector3.zero, Quaternion.identity);
            VisualMesh("FlightStickColumn", stickPivot, new Vector3(0f, 0.13f, 0f), new Vector3(0.03f, 0.26f, 0.03f), metalLight);
            var stickHit = HitboxNode("FlightStickGripHitbox", stickPivot, new Vector3(0f, 0.28f, 0.01f), new Vector3(0.095f, 0.125f, 0.095f));
            VisualMesh("FlightStickGripMesh", stickHit.transform, Vector3.zero, new Vector3(0.075f, 0.105f, 0.075f), redMat);
            RegisterHitbox(stickHit, CockpitControlId.FlightStick, "РУС — ШТУРВАЛ ОРИЕНТАЦИИ 6-DOF (ЗАЖАТЬ ЛКМ)", stickPivot);

            // Primary 3D Onboard Navigation Computer (MFD) directly ahead of Pilot Seat 0 (-0.40, 43.62, 5.35),
            // positioned cleanly proud of the static cabin backing panel at z=5.72 (front z=5.68).
            var mfdRoot = CreateNode("PilotMfdRoot", transform, new Vector3(-0.42f, 43.58f, 5.64f), Quaternion.identity);
            VisualMesh("PilotMfdHousing", mfdRoot, Vector3.zero, new Vector3(0.64f, 0.40f, 0.04f), metalDark);
            VisualMesh("PilotMfdScreen", mfdRoot, new Vector3(0f, 0.025f, -0.022f), new Vector3(0.56f, 0.26f, 0.008f), cyanScreenMat);

            var readoutGo = new GameObject("MfdReadoutText");
            readoutGo.transform.SetParent(mfdRoot, false);
            readoutGo.transform.localPosition = new Vector3(-0.26f, 0.135f, -0.030f);
            readoutGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            readoutGo.transform.localScale = Vector3.one * 0.0075f;
            mfdReadout = readoutGo.AddComponent<TextMesh>();
            mfdReadout.characterSize = 1f;
            mfdReadout.fontSize = 26;
            mfdReadout.color = new Color(0.58f, 0.96f, 1f);

            AddBezelButton(CockpitControlId.MfdTargetPrev, "◀ ПРЕД. ПЛАНЕТА / ЦЕЛЬ", mfdRoot, new Vector3(-0.24f, -0.145f, -0.032f), metalLight);
            AddBezelButton(CockpitControlId.MfdTargetNext, "СЛЕД. ПЛАНЕТА / ЦЕЛЬ ▶", mfdRoot, new Vector3(-0.12f, -0.145f, -0.032f), metalLight);
            AddBezelButton(CockpitControlId.MfdCycleSystem, "★ ЗВЁЗДНАЯ СИСТЕМА", mfdRoot, new Vector3(0.0f, -0.145f, -0.032f), amberMat);
            AddBezelButton(CockpitControlId.MfdToggleGalaxy, "✦ ГАЛАКТИКА (МЛЕЧНЫЙ ПУТЬ / M31)", mfdRoot, new Vector3(0.12f, -0.145f, -0.032f), amberMat);
            AddBezelButton(CockpitControlId.HyperjumpButton, "⚡ ГИПЕРПРЫЖОК", mfdRoot, new Vector3(0.24f, -0.145f, -0.032f), greenMat);
            AddBezelButton(CockpitControlId.LandOrDockButton, "▼ ПОСАДКА / СТЫКОВКА", mfdRoot, new Vector3(0.24f, 0.145f, -0.032f), greenMat);

            RefreshVisualPoses();
        }

        static Transform CreateNode(string name, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        static GameObject HitboxNode(string name, Transform parent, Vector3 localPos, Vector3 meterSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var box = go.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = meterSize;
            return go;
        }

        static GameObject VisualMesh(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = size;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr) mr.sharedMaterial = mat;
            var c = go.GetComponent<Collider>();
            if (c) SafeDestroy(c);
            return go;
        }

        void AddToggleSwitch(CockpitControlId id, string label, Transform parent, Vector3 pos, Material tipMat)
        {
            VisualMesh(id + "_Base", parent, pos, new Vector3(0.065f, 0.02f, 0.065f), metalLight);
            var pivot = CreateNode(id + "_Pivot", parent, pos + new Vector3(0f, 0.012f, 0f), Quaternion.identity);
            var leverHit = HitboxNode(id + "_Hitbox", pivot, new Vector3(0f, 0.042f, 0f), new Vector3(0.062f, 0.095f, 0.062f));
            VisualMesh(id + "_LeverMesh", leverHit.transform, Vector3.zero, new Vector3(0.024f, 0.082f, 0.024f), tipMat);
            RegisterHitbox(leverHit, id, label, pivot);
        }

        void AddPushButton(CockpitControlId id, string label, Transform parent, Vector3 pos, Material mat, float scale)
        {
            VisualMesh(id + "_Collar", parent, pos, new Vector3(0.095f * scale, 0.018f, 0.095f * scale), metalLight);
            var plungerHit = HitboxNode(id + "_Hitbox", parent, pos + new Vector3(0f, 0.026f, 0f), new Vector3(0.092f * scale, 0.052f, 0.092f * scale));
            VisualMesh(id + "_CapMesh", plungerHit.transform, Vector3.zero, new Vector3(0.072f * scale, 0.036f, 0.072f * scale), mat);
            RegisterHitbox(plungerHit, id, label, plungerHit.transform);
        }

        void AddBezelButton(CockpitControlId id, string label, Transform parent, Vector3 pos, Material mat)
        {
            var btnHit = HitboxNode(id + "_Hitbox", parent, pos, new Vector3(0.096f, 0.056f, 0.038f));
            VisualMesh(id + "_BezelMesh", btnHit.transform, Vector3.zero, new Vector3(0.086f, 0.044f, 0.022f), mat);
            RegisterHitbox(btnHit, id, label, btnHit.transform);
        }

        void RegisterHitbox(GameObject go, CockpitControlId id, string label, Transform movingPivot)
        {
            var hb = go.AddComponent<CockpitControlHitbox>();
            hb.ControlId = id;
            hb.Label = label;
            hb.MovingPivot = movingPivot;
            Controls[id] = hb;
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

            // Crucial in both EditMode (autoSyncTransforms=false) and high-velocity 12,000 m/s open-space flight in Update()!
            Physics.SyncTransforms();
            if (Physics.Raycast(aimRay, out RaycastHit hit, 3.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider)
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
                cover.MovingPivot.localRotation = Quaternion.Euler(SafetyCoverOpen ? -96f : 0f, 0f, 0f);
            if (Controls.TryGetValue(CockpitControlId.MasterArmSwitch, out var armSw))
            {
                var armCol = armSw.GetComponent<BoxCollider>();
                if (armCol) armCol.enabled = SafetyCoverOpen;
            }

            if (Controls.TryGetValue(CockpitControlId.ThrottleLever, out var thr) && thr.MovingPivot)
            {
                float t = helm != null ? helm.Throttle : 0f;
                thr.MovingPivot.localPosition = new Vector3(0f, 0.02f, Mathf.Lerp(-0.10f, 0.10f, t));
                thr.MovingPivot.localRotation = Quaternion.Euler(Mathf.Lerp(24f, -26f, t), 0f, 0f);
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
            Physics.SyncTransforms();
        }

        void SetToggleAngle(CockpitControlId id, bool on)
        {
            if (Controls.TryGetValue(id, out var c) && c.MovingPivot)
                c.MovingPivot.localRotation = Quaternion.Euler(on ? 24f : -24f, 0f, 0f);
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
