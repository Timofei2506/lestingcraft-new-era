using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LestingCraft.Runtime
{
    public sealed class MobileHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool IsHeld { get; private set; }
        int downFrame = -10;
        Image bg;
        Color idleColor;
        Color activeColor;

        public void Setup(Image image, Color idle, Color active)
        {
            bg = image;
            idleColor = idle;
            activeColor = active;
            if (bg) bg.color = idleColor;
        }

        public bool WasPressedThisFrame => downFrame == Time.frameCount;

        public void OnPointerDown(PointerEventData eventData)
        {
            IsHeld = true;
            downFrame = Time.frameCount;
            if (bg) bg.color = activeColor;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        void OnDisable()
        {
            Release();
        }

        void Release()
        {
            IsHeld = false;
            if (bg) bg.color = idleColor;
        }
    }

    /// <summary>
    /// Native glassmorphic touch HUD for Android / mobile builds.
    /// Instantiated only when Application.isMobilePlatform is true so desktop and smoke tests are untouched.
    /// </summary>
    public sealed class MobileTouchHud : MonoBehaviour
    {
        public static MobileTouchHud Current { get; private set; }

        PrototypeGame game;
        GameObject gameplayRoot;
        GameObject overlayCloseBtn;
        MobileHoldButton btnUp, btnDown, btnLeft, btnRight;
        MobileHoldButton btnJump, btnCrouch, btnMine, btnPlace, btnDrop;
        Image sprintBg;
        Text sprintLabel;
        bool sprintToggled;
        bool queuedPause, queuedInventory, queuedChat, queuedSubmitChat, queuedInteract;
        readonly Dictionary<int, bool> touchBlockedByUi = new Dictionary<int, bool>();
        Vector2 frameLookDelta;

        public Vector2 MoveVector
        {
            get
            {
                float x = (btnRight && btnRight.IsHeld ? 1f : 0f) - (btnLeft && btnLeft.IsHeld ? 1f : 0f);
                float y = (btnUp && btnUp.IsHeld ? 1f : 0f) - (btnDown && btnDown.IsHeld ? 1f : 0f);
                Vector2 v = new Vector2(x, y);
                if (v.sqrMagnitude > 1f) v.Normalize();
                return v;
            }
        }

        public Vector2 LookDelta => frameLookDelta;
        public bool JumpPressed => btnJump && btnJump.WasPressedThisFrame;
        public bool JumpHeld => btnJump && btnJump.IsHeld;
        public bool CrouchHeld => btnCrouch && btnCrouch.IsHeld;
        public bool SprintHeld => sprintToggled && MoveVector.sqrMagnitude > 0.01f;
        public bool MinePressed => btnMine && btnMine.WasPressedThisFrame;
        public bool MineHeld => btnMine && btnMine.IsHeld;
        public bool PlacePressed => btnPlace && btnPlace.WasPressedThisFrame;
        public bool DropPressed => btnDrop && btnDrop.WasPressedThisFrame;

        public bool ConsumePausePressed() { bool v = queuedPause; queuedPause = false; return v; }
        public bool ConsumeInventoryPressed() { bool v = queuedInventory; queuedInventory = false; return v; }
        public bool ConsumeChatPressed() { bool v = queuedChat; queuedChat = false; return v; }
        public bool ConsumeSubmitChatPressed() { bool v = queuedSubmitChat; queuedSubmitChat = false; return v; }
        public bool ConsumeInteractPressed() { bool v = queuedInteract; queuedInteract = false; return v; }

        public static MobileTouchHud Ensure(PrototypeGame owner)
        {
            if (!Application.isMobilePlatform || !owner) return null;
            if (Current) return Current;
            var go = new GameObject("MobileTouchHud");
            go.transform.SetParent(owner.transform, false);
            var hud = go.AddComponent<MobileTouchHud>();
            hud.Build(owner);
            Current = hud;
            return hud;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Build(PrototypeGame owner)
        {
            game = owner;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 48;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            Font font = Resources.Load<Font>("UI/Manrope") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            gameplayRoot = new GameObject("TouchGameplayRoot", typeof(RectTransform));
            gameplayRoot.transform.SetParent(transform, false);
            Stretch((RectTransform)gameplayRoot.transform);

            // Left D-Pad cluster (bottom-left)
            btnUp = CreateHold(gameplayRoot.transform, font, "ВПЕРЁД", new Vector2(136f, 214f), new Vector2(78f, 74f), false);
            btnDown = CreateHold(gameplayRoot.transform, font, "НАЗАД", new Vector2(136f, 62f), new Vector2(78f, 74f), false);
            btnLeft = CreateHold(gameplayRoot.transform, font, "ВЛЕВО", new Vector2(56f, 138f), new Vector2(78f, 74f), false);
            btnRight = CreateHold(gameplayRoot.transform, font, "ВПРАВО", new Vector2(216f, 138f), new Vector2(78f, 74f), false);

            var sprintBtn = CreateTap(gameplayRoot.transform, font, "БЕГ", new Vector2(136f, 138f), new Vector2(68f, 68f), ToggleSprint, out sprintBg, out sprintLabel);
            sprintBtn.name = "Mobile.Sprint";

            // Right Action cluster (bottom-right)
            btnMine = CreateHold(gameplayRoot.transform, font, "ЛОМАТЬ", new Vector2(-214f, 156f), new Vector2(104f, 84f), true, true);
            btnPlace = CreateHold(gameplayRoot.transform, font, "СТАВИТЬ", new Vector2(-94f, 156f), new Vector2(104f, 84f), false, true);
            btnJump = CreateHold(gameplayRoot.transform, font, "ПРЫЖОК", new Vector2(-94f, 256f), new Vector2(104f, 74f), false, true);
            btnCrouch = CreateHold(gameplayRoot.transform, font, "ВНИЗ", new Vector2(-94f, 66f), new Vector2(104f, 68f), false, true);
            btnDrop = CreateHold(gameplayRoot.transform, font, "БРОС [Q]", new Vector2(-214f, 66f), new Vector2(104f, 68f), false, true);

            // Top-center quick utility bar
            Image dummyImg; Text dummyTxt;
            CreateTapTop(gameplayRoot.transform, font, "II ПАУЗА", new Vector2(-195f, -34f), new Vector2(114f, 44f), () => { queuedPause = true; }, out dummyImg, out dummyTxt);
            CreateTapTop(gameplayRoot.transform, font, "СУМКА [E]", new Vector2(-65f, -34f), new Vector2(124f, 44f), () => { queuedInventory = true; }, out dummyImg, out dummyTxt);
            CreateTapTop(gameplayRoot.transform, font, "МИРЫ [J]", new Vector2(68f, -34f), new Vector2(120f, 44f), () => { if (game) game.OpenRealmCodexOverlay(); }, out dummyImg, out dummyTxt);
            CreateTapTop(gameplayRoot.transform, font, "ЧАТ [/]", new Vector2(190f, -34f), new Vector2(104f, 44f), () => { queuedChat = true; }, out dummyImg, out dummyTxt);

            // Hotbar quick-tap strip (1..9) aligned above bottom edge
            for (int i = 0; i < 9; i++)
            {
                int slotIdx = i;
                float offsetX = (i - 4) * 52f;
                CreateHotbarTap(gameplayRoot.transform, font, (i + 1).ToString(), new Vector2(offsetX, 26f), new Vector2(46f, 36f), () =>
                {
                    if (game && game.Session != null && game.Session.Inventory != null)
                        game.Session.Inventory.selected = slotIdx;
                });
            }

            // Universal close / send button when Chat or Inventory is open on mobile
            var closeObj = CreateTapTop(transform, font, "ГОТОВО / ×", new Vector2(0f, -34f), new Vector2(156f, 46f), () =>
            {
                if (!game) return;
                if (game.ChatOpen) queuedSubmitChat = true;
                else queuedPause = true;
            }, out dummyImg, out dummyTxt);
            overlayCloseBtn = closeObj.gameObject;
            overlayCloseBtn.SetActive(false);
        }

        void ToggleSprint()
        {
            sprintToggled = !sprintToggled;
            if (sprintBg) sprintBg.color = sprintToggled ? new Color(0.95f, 0.68f, 0.25f, 0.72f) : new Color(0.08f, 0.14f, 0.20f, 0.52f);
            if (sprintLabel) sprintLabel.color = sprintToggled ? new Color(0.12f, 0.08f, 0.02f, 1f) : new Color(0.92f, 0.96f, 0.98f, 0.95f);
        }

        void Update()
        {
            frameLookDelta = Vector2.zero;
            if (!game) return;

            bool inWorld = game.Session != null && !game.StoryLocked && !game.Paused && !game.ChatOpen;
            if (gameplayRoot && gameplayRoot.activeSelf != inWorld)
                gameplayRoot.SetActive(inWorld);

            bool showOverlayBtn = game.Session != null && (game.ChatOpen || game.InventoryOpen || game.StationTradeOpen);
            if (overlayCloseBtn && overlayCloseBtn.activeSelf != showOverlayBtn)
                overlayCloseBtn.SetActive(showOverlayBtn);

            if (!inWorld)
            {
                touchBlockedByUi.Clear();
                return;
            }

            var ts = Touchscreen.current;
            if (ts == null) return;

            foreach (var touch in ts.touches)
            {
                int id = touch.touchId.ReadValue();
                var phase = touch.phase.ReadValue();
                Vector2 pos = touch.position.ReadValue();

                if (phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(id);
                    bool leftStickZone = pos.x < Screen.width * 0.32f && pos.y < Screen.height * 0.55f;
                    touchBlockedByUi[id] = overUi || leftStickZone;
                }
                else if (phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    bool blocked;
                    if (!touchBlockedByUi.TryGetValue(id, out blocked))
                        blocked = pos.x < Screen.width * 0.32f && pos.y < Screen.height * 0.55f;
                    if (!blocked)
                        frameLookDelta += touch.delta.ReadValue() * 0.65f;
                }
                else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    touchBlockedByUi.Remove(id);
                }
            }
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static MobileHoldButton CreateHold(Transform parent, Font font, string text, Vector2 anchoredPos, Vector2 size, bool warmAccent, bool anchorRight = false)
        {
            var go = new GameObject("Touch." + text, typeof(RectTransform), typeof(Image), typeof(MobileHoldButton));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorRight ? new Vector2(1f, 0f) : Vector2.zero;
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            Color idle = warmAccent ? new Color(0.88f, 0.52f, 0.18f, 0.58f) : new Color(0.08f, 0.15f, 0.22f, 0.54f);
            Color active = warmAccent ? new Color(1.00f, 0.72f, 0.28f, 0.86f) : new Color(0.24f, 0.58f, 0.52f, 0.82f);

            var hold = go.GetComponent<MobileHoldButton>();
            hold.Setup(img, idle, active);

            AddLabel(go.transform, font, text, 15, new Color(0.95f, 0.98f, 1f, 0.96f));
            return hold;
        }

        static Button CreateTap(Transform parent, Font font, string text, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick, out Image img, out Text label)
        {
            var go = new GameObject("Tap." + text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            img = go.GetComponent<Image>();
            img.color = new Color(0.08f, 0.14f, 0.20f, 0.52f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            label = AddLabel(go.transform, font, text, 14, new Color(0.92f, 0.96f, 0.98f, 0.95f));
            return btn;
        }

        static Button CreateTapTop(Transform parent, Font font, string text, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick, out Image img, out Text label)
        {
            var go = new GameObject("TopTap." + text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            img = go.GetComponent<Image>();
            img.color = new Color(0.07f, 0.13f, 0.19f, 0.66f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            label = AddLabel(go.transform, font, text, 14, new Color(0.96f, 0.86f, 0.64f, 0.98f));
            return btn;
        }

        static void CreateHotbarTap(Transform parent, Font font, string text, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("HotbarTap." + text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = new Color(0.06f, 0.11f, 0.16f, 0.42f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            AddLabel(go.transform, font, text, 13, new Color(0.88f, 0.94f, 0.98f, 0.85f));
        }

        static Text AddLabel(Transform parent, Font font, string content, int fontSize, Color col)
        {
            var tGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            tGo.transform.SetParent(parent, false);
            Stretch((RectTransform)tGo.transform);
            var txt = tGo.GetComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = col;
            txt.raycastTarget = false;
            return txt;
        }
    }
}
