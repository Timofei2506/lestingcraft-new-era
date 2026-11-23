using UnityEngine;
using UnityEngine.UI;
using LestingCraft.Gameplay;
using LestingCraft.Runtime;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Tactile combat HUD layer: target creature nameplate + delayed-trail HP bar,
    /// responsive 4-tick crosshair hitmarker, and pooled 3D world-space damage popups.
    /// Zero raycast targets so it never intercepts clicks or UI automation.
    /// </summary>
    public sealed class CombatFeedbackOverlay : MonoBehaviour
    {
        public static CombatFeedbackOverlay Instance { get; private set; }

        Canvas canvas;
        CanvasGroup targetGroup;
        Text targetTitle;
        Text targetHpLabel;
        RectTransform hpFill;
        RectTransform hpTrail;
        Image hpFillImage;

        CanvasGroup hitmarkerGroup;
        readonly Image[] hitTicks = new Image[4];
        float hitmarkerTimer;
        bool hitmarkerKill;

        struct DamagePopup
        {
            public GameObject root;
            public RectTransform rt;
            public Text label;
            public Vector3 worldPos;
            public float age;
            public float duration;
            public bool active;
        }

        readonly DamagePopup[] popups = new DamagePopup[10];
        int popupCursor;

        string trackedMobId;
        string trackedTitle;
        int trackedHp;
        int trackedMaxHp = 1;
        bool trackedHostile;
        float trackedUntil;
        float trailRatio = 1f;

        public static CombatFeedbackOverlay Ensure(Transform parent = null)
        {
            if (Instance) return Instance;
            var go = new GameObject("Combat Feedback Overlay");
            if (parent) go.transform.SetParent(parent, false);
            Instance = go.AddComponent<CombatFeedbackOverlay>();
            Instance.Build();
            return Instance;
        }

        void Awake()
        {
            if (Instance && Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
                return;
            }
            Instance = this;
            Build();
        }

        void Build()
        {
            if (canvas) return;
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 86;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;

            Font font = Resources.Load<Font>("UI/Manrope") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1) Top-center target creature HP bar
            var barRoot = new GameObject("Target Mob Bar", typeof(RectTransform), typeof(CanvasGroup));
            barRoot.transform.SetParent(transform, false);
            targetGroup = barRoot.GetComponent<CanvasGroup>();
            targetGroup.alpha = 0f;
            targetGroup.blocksRaycasts = false;
            targetGroup.interactable = false;
            var barRt = barRoot.GetComponent<RectTransform>();
            barRt.anchorMin = barRt.anchorMax = new Vector2(.5f, 1f);
            barRt.pivot = new Vector2(.5f, 1f);
            barRt.anchoredPosition = new Vector2(0, -26f);
            barRt.sizeDelta = new Vector2(360f, 54f);

            var bg = MakeImage("BG", barRt, new Color(.03f, .05f, .08f, .80f));
            Stretch(bg.rectTransform, 0, 0, 0, 0);

            targetTitle = MakeText("Mob Name", barRt, font, 16, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(.95f, .97f, 1f));
            targetTitle.rectTransform.anchorMin = new Vector2(0, 1);
            targetTitle.rectTransform.anchorMax = new Vector2(1, 1);
            targetTitle.rectTransform.pivot = new Vector2(.5f, 1);
            targetTitle.rectTransform.offsetMin = new Vector2(12, -28);
            targetTitle.rectTransform.offsetMax = new Vector2(-95, -4);

            targetHpLabel = MakeText("Mob HP", barRt, font, 14, FontStyle.Normal, TextAnchor.MiddleRight, new Color(.78f, .85f, .92f));
            targetHpLabel.rectTransform.anchorMin = new Vector2(0, 1);
            targetHpLabel.rectTransform.anchorMax = new Vector2(1, 1);
            targetHpLabel.rectTransform.pivot = new Vector2(.5f, 1);
            targetHpLabel.rectTransform.offsetMin = new Vector2(220, -28);
            targetHpLabel.rectTransform.offsetMax = new Vector2(-12, -4);

            var track = MakeImage("HP Track", barRt, new Color(.12f, .16f, .22f, .95f));
            track.rectTransform.anchorMin = new Vector2(0, 0);
            track.rectTransform.anchorMax = new Vector2(1, 0);
            track.rectTransform.pivot = new Vector2(.5f, 0);
            track.rectTransform.offsetMin = new Vector2(12, 10);
            track.rectTransform.offsetMax = new Vector2(-12, 20);

            var trailImg = MakeImage("HP Trail", track.rectTransform, new Color(1f, .86f, .45f, .90f));
            hpTrail = trailImg.rectTransform;
            Stretch(hpTrail, 0, 0, 0, 0);

            hpFillImage = MakeImage("HP Fill", track.rectTransform, new Color(.25f, .85f, .52f, 1f));
            hpFill = hpFillImage.rectTransform;
            Stretch(hpFill, 0, 0, 0, 0);

            // 2) Crosshair 4-tick diagonal hitmarker
            var hmRoot = new GameObject("Crosshair Hitmarker", typeof(RectTransform), typeof(CanvasGroup));
            hmRoot.transform.SetParent(transform, false);
            hitmarkerGroup = hmRoot.GetComponent<CanvasGroup>();
            hitmarkerGroup.alpha = 0f;
            hitmarkerGroup.blocksRaycasts = false;
            var hmRt = hmRoot.GetComponent<RectTransform>();
            hmRt.anchorMin = hmRt.anchorMax = new Vector2(.5f, .5f);
            hmRt.sizeDelta = new Vector2(36, 36);

            float[] angles = { 45f, 135f, 225f, 315f };
            for (int i = 0; i < 4; i++)
            {
                var tick = MakeImage("Tick" + i, hmRt, Color.white);
                tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(.5f, .5f);
                tick.rectTransform.sizeDelta = new Vector2(3f, 11f);
                float rad = angles[i] * Mathf.Deg2Rad;
                tick.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * 9f;
                tick.rectTransform.localRotation = Quaternion.Euler(0, 0, -angles[i]);
                hitTicks[i] = tick;
            }

            // 3) Pre-allocated floating damage number pool
            for (int i = 0; i < popups.Length; i++)
            {
                var pObj = new GameObject("DmgPopup_" + i, typeof(RectTransform));
                pObj.transform.SetParent(transform, false);
                var prt = pObj.GetComponent<RectTransform>();
                prt.anchorMin = prt.anchorMax = Vector2.zero;
                prt.sizeDelta = new Vector2(140, 36);
                var txt = MakeText("Value", prt, font, 21, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
                Stretch(txt.rectTransform, 0, 0, 0, 0);
                var shadow = txt.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, .85f);
                shadow.effectDistance = new Vector2(1.5f, -1.5f);
                pObj.SetActive(false);
                popups[i] = new DamagePopup { root = pObj, rt = prt, label = txt };
            }
        }

        static Image MakeImage(string name, Transform parent, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = col;
            img.raycastTarget = false;
            return img;
        }

        static Text MakeText(string name, Transform parent, Font font, int size, FontStyle style, TextAnchor anchor, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = col;
            t.raycastTarget = false;
            t.supportRichText = false;
            return t;
        }

        static void Stretch(RectTransform rt, float l, float b, float r, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -top);
        }

        public static void NotifyHit(Vector3 worldPoint, int damage, bool killed, string targetName = null, int currentHp = 0, int maxHp = 0, bool hostile = true)
        {
            if (!Instance) return;
            Instance.hitmarkerTimer = killed ? 0.28f : 0.18f;
            Instance.hitmarkerKill = killed;
            Color c = killed ? new Color(1f, .78f, .24f) : Color.white;
            for (int i = 0; i < Instance.hitTicks.Length; i++)
                if (Instance.hitTicks[i]) Instance.hitTicks[i].color = c;

            if (damage > 0)
            {
                int slot = Instance.popupCursor;
                Instance.popupCursor = (Instance.popupCursor + 1) % Instance.popups.Length;
                ref var p = ref Instance.popups[slot];
                p.worldPos = worldPoint + new Vector3(Random.Range(-.18f, .18f), .15f, Random.Range(-.18f, .18f));
                p.age = 0f;
                p.duration = killed ? 1.05f : 0.80f;
                p.active = true;
                p.label.text = killed ? "-" + damage + " ★" : "-" + damage;
                p.label.color = killed ? new Color(1f, .82f, .28f) : new Color(1f, .42f, .36f);
                p.root.SetActive(true);
            }

            if (!string.IsNullOrEmpty(targetName) && maxHp > 0)
            {
                if (Instance.trackedTitle != targetName) Instance.trailRatio = 1f;
                Instance.trackedTitle = targetName;
                Instance.trackedHp = Mathf.Max(0, currentHp);
                Instance.trackedMaxHp = Mathf.Max(1, maxHp);
                Instance.trackedHostile = hostile;
                Instance.trackedUntil = Time.time + (killed ? 1.1f : 3.5f);
            }
        }

        public void TickAimTarget(PrototypeGame game)
        {
            var session = game ? game.Session : null;
            var player = session ? session.Player : null;
            var cam = player ? player.Camera : null;
            bool hudVisible = session != null && !game.Paused && !game.InventoryOpen && !game.StationTradeOpen && cam != null;
            if (!hudVisible)
            {
                if (targetGroup) targetGroup.alpha = 0f;
                if (hitmarkerGroup) hitmarkerGroup.alpha = 0f;
                return;
            }

            // Raycast up to 14m to inspect hovered wildlife even before hitting
            AnimalState hovered = null;
            if (session.Animals && session.Animals.TryInspect(new Ray(cam.transform.position, cam.transform.forward), 14f, out hovered) && hovered != null)
            {
                string speciesName = AnimalSculpts.SpeciesNames[Mathf.Clamp(hovered.species, 0, AnimalSculpts.SpeciesNames.Length - 1)];
                bool mutant = WildlifeSimulation.IsRadiaMutant(hovered.species);
                bool aggr = WildlifeSimulation.IsAggressive(hovered.species);
                string title = mutant ? speciesName + " · Мутант" : aggr ? speciesName + " · Хищник" : speciesName;
                if (trackedMobId != hovered.id)
                {
                    trackedMobId = hovered.id;
                    trailRatio = Mathf.Clamp01(hovered.health / (float)Mathf.Max(1, WildlifeSimulation.MaxHealth(hovered.species)));
                }
                trackedTitle = title;
                trackedHp = hovered.health;
                trackedMaxHp = WildlifeSimulation.MaxHealth(hovered.species);
                trackedHostile = mutant || aggr;
                trackedUntil = Mathf.Max(trackedUntil, Time.time + 0.25f);
            }

            // Update Target Bar
            bool showBar = Time.time < trackedUntil && trackedMaxHp > 0;
            float wantAlpha = showBar ? 1f : 0f;
            targetGroup.alpha = Mathf.MoveTowards(targetGroup.alpha, wantAlpha, Time.deltaTime * 6f);
            if (targetGroup.alpha > 0.01f)
            {
                targetTitle.text = trackedTitle ?? "";
                targetHpLabel.text = trackedHp + " / " + trackedMaxHp + " HP";
                float ratio = Mathf.Clamp01(trackedHp / (float)Mathf.Max(1, trackedMaxHp));
                trailRatio = Mathf.MoveTowards(trailRatio, ratio, Time.deltaTime * 0.85f);
                if (trailRatio < ratio) trailRatio = ratio;
                hpFill.anchorMax = new Vector2(ratio, 1f);
                hpTrail.anchorMax = new Vector2(trailRatio, 1f);
                hpFillImage.color = trackedHostile ? new Color(.92f, .32f, .28f) : new Color(.24f, .82f, .52f);
            }

            // Update Crosshair Hitmarker
            if (hitmarkerTimer > 0f)
            {
                hitmarkerTimer -= Time.deltaTime;
                float u = Mathf.Clamp01(hitmarkerTimer / (hitmarkerKill ? 0.28f : 0.18f));
                hitmarkerGroup.alpha = u;
                float dist = Mathf.Lerp(13f, 8f, u);
                float[] angles = { 45f, 135f, 225f, 315f };
                for (int i = 0; i < 4; i++)
                {
                    float rad = angles[i] * Mathf.Deg2Rad;
                    hitTicks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * dist;
                }
            }
            else if (hitmarkerGroup.alpha > 0f)
            {
                hitmarkerGroup.alpha = 0f;
            }

            // Update 3D floating damage popups
            float scaleFactor = canvas.scaleFactor > 0.01f ? canvas.scaleFactor : 1f;
            for (int i = 0; i < popups.Length; i++)
            {
                if (!popups[i].active) continue;
                ref var p = ref popups[i];
                p.age += Time.deltaTime;
                if (p.age >= p.duration)
                {
                    p.active = false;
                    p.root.SetActive(false);
                    continue;
                }
                float t = p.age / p.duration;
                Vector3 risen = p.worldPos + Vector3.up * (t * 0.85f);
                Vector3 sp = cam.WorldToScreenPoint(risen);
                if (sp.z <= 0f) { p.root.SetActive(false); continue; }
                if (!p.root.activeSelf) p.root.SetActive(true);
                p.rt.anchoredPosition = new Vector2(sp.x / scaleFactor, sp.y / scaleFactor);
                var c = p.label.color;
                c.a = 1f - t * t;
                p.label.color = c;
                float popScale = t < 0.18f ? Mathf.Lerp(0.75f, 1.18f, t / 0.18f) : Mathf.Lerp(1.18f, 0.95f, (t - 0.18f) / 0.82f);
                p.rt.localScale = Vector3.one * popScale;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
