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
        // Seated pilot eye in VerticalShuttleInteriorVisual local coordinates:
        // 0.67 m above dashboard glareshield (y=43.15) with panoramic front & side visibility
        // AND direct unobstructed 3D sightlines onto all 16 cockpit controls!
        public static readonly Vector3 PilotEyeLocal = new Vector3(-0.40f, 43.82f, 4.88f);

        public readonly Dictionary<CockpitControlId, CockpitControlHitbox> Controls =
            new Dictionary<CockpitControlId, CockpitControlHitbox>();

        public bool SafetyCoverOpen { get; private set; }
        public CockpitControlId? HoveredControl { get; private set; }
        public Vector2 StickDeflection { get; private set; }
        public string HoveredLabel => HoveredControl.HasValue && Controls.TryGetValue(HoveredControl.Value, out var h)
            ? h.Label : string.Empty;

        CockpitPanel panel;
        OpenSpaceHelm helm;
        Material metalDark, metalLight, redMat, greenMat, amberMat, cyanScreenMat, pfdHorizonSky, pfdHorizonGnd, ledGreenGlow;
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
            metalDark = MakeMat(shader, new Color(.10f, .13f, .18f), .68f, .62f);
            metalLight = MakeMat(shader, new Color(.70f, .75f, .82f), .82f, .64f);
            redMat = MakeMat(shader, new Color(.88f, .15f, .13f), .22f, .65f, 0.35f);
            greenMat = MakeMat(shader, new Color(.16f, .86f, .42f), .22f, .65f, 0.55f);
            amberMat = MakeMat(shader, new Color(.98f, .66f, .14f), .25f, .65f, 0.45f);
            cyanScreenMat = MakeMat(shader, new Color(.03f, .14f, .24f), .08f, .88f, 1.15f);
            pfdHorizonSky = MakeMat(shader, new Color(.12f, .46f, .84f), .05f, .80f, 0.95f);
            pfdHorizonGnd = MakeMat(shader, new Color(.48f, .28f, .12f), .05f, .80f, 0.75f);
            ledGreenGlow = MakeMat(shader, new Color(.22f, .96f, .58f), .05f, .80f, 1.45f);

            transform.SetParent(cabinParent, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            // ================================================================================
            // 1. SLOPED GLASS-COCKPIT INSTRUMENT DASHBOARD (ALL BELOW y=43.15 GLAIRSHIELD!)
            // ================================================================================
            var pfdRoot = CreateNode("PilotPfdRoot", transform, new Vector3(-0.92f, 42.94f, 5.56f), Quaternion.Euler(26f, 0f, 0f));
            VisualMesh("PfdBezel", pfdRoot, Vector3.zero, new Vector3(0.42f, 0.31f, 0.025f), metalDark);
            VisualMesh("PfdSkyHalf", pfdRoot, new Vector3(0f, 0.058f, -0.015f), new Vector3(0.36f, 0.115f, 0.006f), pfdHorizonSky);
            VisualMesh("PfdGroundHalf", pfdRoot, new Vector3(0f, -0.058f, -0.015f), new Vector3(0.36f, 0.115f, 0.006f), pfdHorizonGnd);
            VisualMesh("PfdHorizonLine", pfdRoot, new Vector3(0f, 0f, -0.019f), new Vector3(0.34f, 0.008f, 0.004f), ledGreenGlow);
            VisualMesh("PfdPitchBarUp", pfdRoot, new Vector3(0f, 0.045f, -0.019f), new Vector3(0.14f, 0.005f, 0.004f), metalLight);
            VisualMesh("PfdReticle", pfdRoot, new Vector3(0f, 0f, -0.021f), new Vector3(0.08f, 0.012f, 0.004f), amberMat);

            var mfdRoot = CreateNode("PilotMfdRoot", transform, new Vector3(-0.34f, 42.94f, 5.56f), Quaternion.Euler(26f, 0f, 0f));
            VisualMesh("PilotMfdHousing", mfdRoot, Vector3.zero, new Vector3(0.56f, 0.32f, 0.025f), metalDark);
            VisualMesh("PilotMfdScreen", mfdRoot, new Vector3(0f, 0.024f, -0.015f), new Vector3(0.49f, 0.21f, 0.006f), cyanScreenMat);

            var readoutGo = new GameObject("MfdReadoutText");
            readoutGo.transform.SetParent(mfdRoot, false);
            readoutGo.transform.localPosition = new Vector3(-0.225f, 0.108f, -0.021f);
            readoutGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            readoutGo.transform.localScale = Vector3.one * 0.0062f;
            mfdReadout = readoutGo.AddComponent<TextMesh>();
            mfdReadout.characterSize = 1f;
            mfdReadout.fontSize = 24;
            mfdReadout.color = new Color(0.68f, 0.98f, 1f);

            AddBezelButton(CockpitControlId.MfdTargetPrev, "◀ ПРЕД. ПЛАНЕТА / ЦЕЛЬ", mfdRoot, new Vector3(-0.20f, -0.118f, -0.020f), metalLight);
            AddBezelButton(CockpitControlId.MfdTargetNext, "СЛЕД. ПЛАНЕТА / ЦЕЛЬ ▶", mfdRoot, new Vector3(-0.11f, -0.118f, -0.020f), metalLight);
            AddBezelButton(CockpitControlId.MfdCycleSystem, "★ ЗВЁЗДНАЯ СИСТЕМА", mfdRoot, new Vector3(-0.02f, -0.118f, -0.020f), amberMat);
            AddBezelButton(CockpitControlId.MfdToggleGalaxy, "✦ ГАЛАКТИКА (МЛЕЧНЫЙ ПУТЬ / M31)", mfdRoot, new Vector3(0.07f, -0.118f, -0.020f), amberMat);
            AddBezelButton(CockpitControlId.HyperjumpButton, "⚡ ГИПЕРПРЫЖОК", mfdRoot, new Vector3(0.16f, -0.118f, -0.020f), greenMat);
            AddBezelButton(CockpitControlId.LandOrDockButton, "▼ ПОСАДКА / СТЫКОВКА", mfdRoot, new Vector3(0.22f, 0.112f, -0.020f), greenMat);

            var eicasRoot = CreateNode("CopilotEicasRoot", transform, new Vector3(1.38f, 42.94f, 5.56f), Quaternion.Euler(26f, 0f, 0f));
            VisualMesh("EicasBezel", eicasRoot, Vector3.zero, new Vector3(0.52f, 0.31f, 0.025f), metalDark);
            VisualMesh("EicasScreen", eicasRoot, new Vector3(0f, 0.01f, -0.015f), new Vector3(0.45f, 0.24f, 0.006f), cyanScreenMat);
            for (int b = 0; b < 4; b++)
            {
                float hgt = 0.07f + (b % 3) * 0.045f;
                VisualMesh("EicasBar" + b, eicasRoot, new Vector3(-0.14f + b * 0.095f, -0.06f + hgt * 0.5f, -0.020f),
                    new Vector3(0.042f, hgt, 0.004f), b < 3 ? ledGreenGlow : amberMat);
            }

            // ================================================================================
            // 2. ERGONOMIC CENTER PEDESTAL CONSOLE (Between Seat 0 & Seat 1, y=42.85!)
            // ================================================================================
            var consoleRoot = CreateNode("PilotConsoleRoot", transform, new Vector3(0.48f, 42.85f, 5.18f),
                Quaternion.Euler(-12f, -16f, 0f));
            VisualMesh("PilotInteractiveConsolePlate", consoleRoot, new Vector3(0f, -0.012f, 0f),
                new Vector3(0.74f, 0.024f, 0.54f), metalDark);

            // Twin-Lever Space Shuttle Throttle Quadrant (РУД) on left side of center pedestal
            var quadrantRoot = CreateNode("ThrottleQuadrantRoot", consoleRoot, new Vector3(-0.24f, 0.01f, -0.02f), Quaternion.identity);
            VisualMesh("ThrottleQuadrantHousing", quadrantRoot, new Vector3(0f, 0.025f, 0f), new Vector3(0.15f, 0.055f, 0.30f), metalDark);
            VisualMesh("ThrottleSlotL", quadrantRoot, new Vector3(-0.028f, 0.054f, 0f), new Vector3(0.014f, 0.006f, 0.22f), metalLight);
            VisualMesh("ThrottleSlotR", quadrantRoot, new Vector3(0.028f, 0.054f, 0f), new Vector3(0.014f, 0.006f, 0.22f), metalLight);
            var throttlePivot = CreateNode("ThrottlePivot", quadrantRoot, new Vector3(0f, 0.04f, -0.05f), Quaternion.identity);
            VisualMesh("ThrottleStemL", throttlePivot, new Vector3(-0.028f, 0.075f, 0f), new Vector3(0.016f, 0.14f, 0.020f), metalLight);
            VisualMesh("ThrottleStemR", throttlePivot, new Vector3(0.028f, 0.075f, 0f), new Vector3(0.016f, 0.14f, 0.020f), metalLight);
            var gripHit = HitboxNode("ThrottleGripHitbox", throttlePivot, new Vector3(0f, 0.15f, 0f), new Vector3(0.13f, 0.06f, 0.06f));
            VisualMesh("ThrottleGripMesh", gripHit.transform, Vector3.zero, new Vector3(0.115f, 0.036f, 0.042f), amberMat);
            RegisterHitbox(gripHit, CockpitControlId.ThrottleLever, "РУД — РЫЧАГ ТЯГИ (ЛКМ / КОЛЕСО МЫШИ)", throttlePivot);

            // Row 1 (z = +0.16): Systems Toggles
            AddToggleSwitch(CockpitControlId.MainBusSwitch, "ГЛАВНАЯ ШИНА ПИТАНИЯ", consoleRoot, new Vector3(-0.05f, 0.005f, 0.16f), amberMat);
            AddToggleSwitch(CockpitControlId.FuelFeedSwitch, "ПОДАЧА КРИОТОПЛИВА", consoleRoot, new Vector3(0.09f, 0.005f, 0.16f), greenMat);
            AddToggleSwitch(CockpitControlId.HatchSwitch, "ГЕРМОЗАТВОР ЛЮКА", consoleRoot, new Vector3(0.23f, 0.005f, 0.16f), amberMat);

            // Row 2 (z = +0.01): Guarded Master Arm Switch + Red Flip-Open Safety Cover
            AddToggleSwitch(CockpitControlId.MasterArmSwitch, "ТУМБЛЕР «ГЛАВНЫЙ ВЗВОД»", consoleRoot, new Vector3(0.22f, 0.005f, 0.00f), redMat);
            var guardPivot = CreateNode("MasterArmGuardPivot", consoleRoot, new Vector3(0.22f, 0.015f, 0.048f), Quaternion.identity);
            var guardHit = HitboxNode("MasterArmCoverHitbox", guardPivot, new Vector3(0f, 0.034f, -0.048f), new Vector3(0.088f, 0.078f, 0.098f));
            VisualMesh("MasterArmCoverMesh", guardHit.transform, Vector3.zero, new Vector3(0.074f, 0.062f, 0.084f), redMat);
            RegisterHitbox(guardHit, CockpitControlId.MasterArmCover, "ЗАЩИТНЫЙ КОЛПАК «ВЗВОД»", guardPivot);

            // Row 3 (z = -0.15): Ignition, Abort & Helm Mode Pushbuttons
            AddPushButton(CockpitControlId.IgniteButton, "КНОПКА «ПУСК ДВИГАТЕЛЕЙ»", consoleRoot, new Vector3(-0.06f, 0.005f, -0.15f), greenMat, 0.85f);
            AddPushButton(CockpitControlId.AbortButton, "АВАРИЙНЫЙ СБРОС (ABORT)", consoleRoot, new Vector3(0.08f, 0.005f, -0.15f), redMat, 0.82f);
            AddPushButton(CockpitControlId.HelmModeButton, "РЕЖИМ ШТУРВАЛА: РУЧНОЙ / АВТО", consoleRoot, new Vector3(0.22f, 0.005f, -0.15f), amberMat, 0.80f);

            // ================================================================================
            // 3. PILOT 6-DOF ROTATIONAL HAND CONTROLLER (РУС / Flight Stick at Pilot Seat 0!)
            // ================================================================================
            var stickRoot = CreateNode("FlightStickRoot", transform, new Vector3(-0.40f, 42.54f, 5.26f), Quaternion.identity);
            VisualMesh("FlightStickPedestal", stickRoot, new Vector3(0f, -0.22f, 0f), new Vector3(0.065f, 0.44f, 0.065f), metalDark);
            VisualMesh("FlightStickBoot", stickRoot, new Vector3(0f, 0.01f, 0f), new Vector3(0.105f, 0.04f, 0.105f), metalDark);
            var stickPivot = CreateNode("FlightStickPivot", stickRoot, Vector3.zero, Quaternion.identity);
            VisualMesh("FlightStickColumn", stickPivot, new Vector3(0f, 0.095f, 0f), new Vector3(0.024f, 0.19f, 0.024f), metalLight);
            var stickHit = HitboxNode("FlightStickGripHitbox", stickPivot, new Vector3(0f, 0.21f, 0.01f), new Vector3(0.085f, 0.12f, 0.085f));
            VisualMesh("FlightStickGripBody", stickHit.transform, Vector3.zero, new Vector3(0.046f, 0.105f, 0.054f), metalDark);
            VisualMesh("FlightStickTriggerCap", stickHit.transform, new Vector3(0f, 0.056f, 0.008f), new Vector3(0.050f, 0.022f, 0.058f), redMat);
            RegisterHitbox(stickHit, CockpitControlId.FlightStick, "РУС — ШТУРВАЛ ОРИЕНТАЦИИ 6-DOF (ЗАЖАТЬ ЛКМ)", stickPivot);

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
            VisualMesh(id + "_Base", parent, pos, new Vector3(0.048f, 0.014f, 0.048f), metalLight);
            var pivot = CreateNode(id + "_Pivot", parent, pos + new Vector3(0f, 0.010f, 0f), Quaternion.identity);
            var leverHit = HitboxNode(id + "_Hitbox", pivot, new Vector3(0f, 0.032f, 0f), new Vector3(0.056f, 0.078f, 0.056f));
            VisualMesh(id + "_StemMesh", leverHit.transform, new Vector3(0f, -0.010f, 0f), new Vector3(0.012f, 0.048f, 0.012f), metalLight);
            VisualMesh(id + "_TipMesh", leverHit.transform, new Vector3(0f, 0.020f, 0f), new Vector3(0.020f, 0.022f, 0.020f), tipMat);
            RegisterHitbox(leverHit, id, label, pivot);
        }

        void AddPushButton(CockpitControlId id, string label, Transform parent, Vector3 pos, Material mat, float scale)
        {
            VisualMesh(id + "_Collar", parent, pos, new Vector3(0.072f * scale, 0.014f, 0.072f * scale), metalLight);
            var plungerHit = HitboxNode(id + "_Hitbox", parent, pos + new Vector3(0f, 0.020f, 0f), new Vector3(0.074f * scale, 0.044f, 0.074f * scale));
            VisualMesh(id + "_CapMesh", plungerHit.transform, Vector3.zero, new Vector3(0.054f * scale, 0.024f, 0.054f * scale), mat);
            RegisterHitbox(plungerHit, id, label, plungerHit.transform);
        }

        void AddBezelButton(CockpitControlId id, string label, Transform parent, Vector3 pos, Material mat)
        {
            var btnHit = HitboxNode(id + "_Hitbox", parent, pos, new Vector3(0.076f, 0.044f, 0.030f));
            VisualMesh(id + "_BezelMesh", btnHit.transform, Vector3.zero, new Vector3(0.068f, 0.030f, 0.016f), mat);
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

        static Material MakeMat(Shader sh, Color c, float metallic, float smooth, float emission = 0f)
        {
            var m = new Material(sh) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (emission > 0.001f)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * emission);
            }
            return m;
        }

        public CockpitActionResult StepMouseInteraction(
            Ray aimRay,
            bool leftPressedThisFrame,
            bool leftHeld,
            bool leftReleasedThisFrame,
            Vector2 pointerDelta,
            float scrollWheelDelta)
        {
            if (leftReleasedThisFrame)
                EndDrag();

            CockpitControlId hovered;
            string lbl;
            RaycastControl(aimRay, out hovered, out lbl, 4.0f);

            if (Mathf.Abs(scrollWheelDelta) > 0.001f)
                ScrollThrottle(scrollWheelDelta);

            if (leftPressedThisFrame && hovered != CockpitControlId.None)
            {
                activeDragControl = hovered;
                return ActivateControl(hovered);
            }

            if (leftHeld && activeDragControl.HasValue && pointerDelta.sqrMagnitude > 1e-6f)
                DragControl(pointerDelta, 0.016f);

            return default(CockpitActionResult);
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
                    res.MessageRu = SafetyCoverOpen ? "Защитный колпак «ВЗВОД» откинут" : "Защитный колпак «ВЗВОД» закрыт";
                    break;
                case CockpitControlId.MasterArmSwitch:
                    if (!SafetyCoverOpen)
                    {
                        SafetyCoverOpen = true;
                        res.MessageRu = "Сначала откинут защитный колпак — нажмите ещё раз для взвода!";
                        break;
                    }
                    if (panel != null) panel.Press(PanelControl.Arm);
                    res.MessageRu = "Главный взвод зажигания: " + (panel != null && panel.Arm ? "ВЗВЕДЁН" : "СНЯТ");
                    break;
                case CockpitControlId.IgniteButton:
                    if (panel != null) panel.Press(PanelControl.Ignite);
                    res.RequestedIgnite = true;
                    res.MessageRu = "Команда ПУСК отправлена главным двигателям!";
                    break;
                case CockpitControlId.AbortButton:
                    if (panel != null) panel.Press(PanelControl.Abort);
                    if (helm != null) { helm.SetThrottle(0f); helm.Velocity = Vector3.zero; }
                    res.RequestedAbort = true;
                    res.MessageRu = "АВАРИЙНЫЙ СБРОС ТЯГИ!";
                    break;
                case CockpitControlId.HelmModeButton:
                    if (helm != null)
                    {
                        helm.SetThrottle(helm.Throttle > 0.1f ? 0f : 0.65f);
                        res.MessageRu = helm.Throttle > 0.1f ? "Маршевая тяга включена (65%)" : "Тяга переведена на холостой ход";
                    }
                    break;
                case CockpitControlId.ThrottleLever:
                    if (helm != null)
                    {
                        float next = helm.Throttle + 0.25f;
                        if (next > 1.05f) next = 0f;
                        helm.SetThrottle(Mathf.Clamp01(next));
                        res.MessageRu = "РУД: тяга двигателей " + Mathf.RoundToInt(helm.Throttle * 100f) + "%";
                    }
                    break;
                case CockpitControlId.MfdTargetPrev:
                    if (helm != null)
                    {
                        var b = helm.CycleTarget(-1);
                        res.MessageRu = "Цель навигации: " + b.NameRu;
                    }
                    break;
                case CockpitControlId.MfdTargetNext:
                    if (helm != null)
                    {
                        var b = helm.CycleTarget(1);
                        res.MessageRu = "Цель навигации: " + b.NameRu;
                    }
                    break;
                case CockpitControlId.MfdCycleSystem:
                    if (helm != null)
                    {
                        var sysList = CelestialAtlas.SystemsInGalaxy(helm.CurrentGalaxy);
                        int cur = Array.IndexOf(sysList, helm.CurrentSystem);
                        var nextSys = sysList[(cur + 1 + sysList.Length) % sysList.Length];
                        helm.StartHyperjump(nextSys);
                        res.RequestedHyperjump = true;
                        res.MessageRu = "Выбрана система: " + CelestialAtlas.GetSystem(nextSys).NameRu;
                    }
                    break;
                case CockpitControlId.MfdToggleGalaxy:
                    if (helm != null)
                    {
                        var nextGal = helm.CurrentGalaxy == GalaxyId.MilkyWay ? GalaxyId.Andromeda : GalaxyId.MilkyWay;
                        helm.JumpToGalaxy(nextGal);
                        res.RequestedHyperjump = true;
                        res.MessageRu = "МЕЖГАЛАКТИЧЕСКИЙ ПРЫЖОК → " + CelestialAtlas.GalaxyNameRu(nextGal);
                    }
                    break;
                case CockpitControlId.HyperjumpButton:
                    if (helm != null)
                    {
                        helm.StartHyperjump(helm.TargetBody);
                        res.RequestedHyperjump = true;
                        res.MessageRu = "ГИПЕРПРЫЖОК К «" + CelestialAtlas.GetBody(helm.TargetBody).NameRu + "»!";
                    }
                    break;
                case CockpitControlId.LandOrDockButton:
                    res.RequestedLandOrDock = true;
                    res.MessageRu = "Запрос посадки / стыковки…";
                    break;
            }
            RefreshVisualPoses();
            return res;
        }

        public bool RaycastControl(Ray ray, out CockpitControlId hitId, out string label, float maxDist = 4.0f)
        {
            hitId = CockpitControlId.None;
            label = string.Empty;
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, maxDist))
            {
                var hb = hit.collider ? hit.collider.GetComponent<CockpitControlHitbox>() : null;
                if (hb)
                {
                    hitId = hb.ControlId;
                    label = hb.Label;
                }
            }
            HoveredControl = hitId == CockpitControlId.None ? (CockpitControlId?)null : hitId;
            return hitId != CockpitControlId.None;
        }

        public CockpitActionResult ClickRay(Ray ray, float maxDist = 4.0f)
        {
            CockpitControlId id;
            string lbl;
            if (!RaycastControl(ray, out id, out lbl, maxDist))
                return default(CockpitActionResult);
            activeDragControl = id;
            return ActivateControl(id);
        }

        public void DragControl(Vector2 pointerDelta, float dt)
        {
            if (!activeDragControl.HasValue || helm == null) return;
            if (activeDragControl.Value == CockpitControlId.ThrottleLever)
            {
                helm.SetThrottle(Mathf.Clamp01(helm.Throttle + pointerDelta.y * 0.008f));
                RefreshVisualPoses(dt);
            }
            else if (activeDragControl.Value == CockpitControlId.FlightStick)
            {
                Vector2 next = StickDeflection + pointerDelta * 0.015f;
                float len = next.magnitude;
                StickDeflection = len > 1f ? next / len : next;
                helm.Step(Mathf.Max(0.016f, dt), -StickDeflection.y, StickDeflection.x, -StickDeflection.x * 0.35f);
                RefreshVisualPoses(dt);
            }
        }

        public void EndDrag()
        {
            activeDragControl = null;
            StickDeflection = Vector2.zero;
            RefreshVisualPoses();
        }

        public void ScrollThrottle(float wheelDelta)
        {
            if (helm == null || Mathf.Abs(wheelDelta) < 0.001f) return;
            helm.SetThrottle(Mathf.Clamp01(helm.Throttle + Mathf.Sign(wheelDelta) * 0.08f));
            RefreshVisualPoses();
        }

        public void RefreshVisualPoses(float dt = 0.016f)
        {
            SetSwitchAngle(CockpitControlId.MainBusSwitch, panel != null && panel.MainBus);
            SetSwitchAngle(CockpitControlId.FuelFeedSwitch, panel != null && panel.FuelFeed);
            SetSwitchAngle(CockpitControlId.HatchSwitch, panel != null && panel.HatchSealed);
            SetSwitchAngle(CockpitControlId.MasterArmSwitch, panel != null && panel.Arm);

            CockpitControlHitbox armHb;
            if (Controls.TryGetValue(CockpitControlId.MasterArmSwitch, out armHb))
            {
                var armBox = armHb.GetComponent<BoxCollider>();
                if (armBox) armBox.enabled = SafetyCoverOpen;
            }

            CockpitControlHitbox coverHb;
            if (Controls.TryGetValue(CockpitControlId.MasterArmCover, out coverHb) && coverHb.MovingPivot)
                coverHb.MovingPivot.localRotation = Quaternion.Euler(SafetyCoverOpen ? -98f : 0f, 0f, 0f);

            CockpitControlHitbox thrHb;
            if (Controls.TryGetValue(CockpitControlId.ThrottleLever, out thrHb) && thrHb.MovingPivot)
            {
                float t = helm != null ? helm.Throttle : 0f;
                thrHb.MovingPivot.localPosition = new Vector3(0f, 0.04f, Mathf.Lerp(-0.085f, 0.085f, t));
                thrHb.MovingPivot.localRotation = Quaternion.Euler(Mathf.Lerp(-18f, 22f, t), 0f, 0f);
            }

            CockpitControlHitbox stickHb;
            if (Controls.TryGetValue(CockpitControlId.FlightStick, out stickHb) && stickHb.MovingPivot)
                stickHb.MovingPivot.localRotation = Quaternion.Euler(StickDeflection.y * 18f, 0f, -StickDeflection.x * 18f);

            if (mfdReadout && helm != null)
            {
                var body = CelestialAtlas.GetBody(helm.TargetBody);
                var sys = CelestialAtlas.GetSystem(helm.CurrentSystem);
                mfdReadout.text =
                    "НАВИГАТОР ЭРА-V8 · " + CelestialAtlas.GalaxyNameRu(helm.CurrentGalaxy).ToUpperInvariant() + "\n" +
                    "СИСТЕМА: " + sys.NameRu + "\n" +
                    "ЦЕЛЬ:    " + body.NameRu + "\n" +
                    "ТЯГА:    " + Mathf.RoundToInt(helm.Throttle * 100f) + "%   СКОР: " + helm.Velocity.magnitude.ToString("0") + " М/С";
            }
        }

        void SetSwitchAngle(CockpitControlId id, bool on)
        {
            CockpitControlHitbox hb;
            if (Controls.TryGetValue(id, out hb) && hb.MovingPivot)
                hb.MovingPivot.localRotation = Quaternion.Euler(on ? 28f : -28f, 0f, 0f);
        }

        static void SafeDestroy(UnityEngine.Object obj)
        {
            if (!obj) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        void OnDestroy()
        {
            SafeDestroy(metalDark);
            SafeDestroy(metalLight);
            SafeDestroy(redMat);
            SafeDestroy(greenMat);
            SafeDestroy(amberMat);
            SafeDestroy(cyanScreenMat);
            SafeDestroy(pfdHorizonSky);
            SafeDestroy(pfdHorizonGnd);
            SafeDestroy(ledGreenGlow);
        }
    }
}
