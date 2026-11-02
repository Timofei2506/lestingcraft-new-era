using UnityEngine;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Keeps the fixed-design uGUI frame inside Screen.safeArea (notches, cutouts, gesture bars).
    /// Desktop safe area equals the display, so this is a no-op there. The CanvasScaler still
    /// owns reference-resolution scaling; this component only adds a uniform safety fit.
    /// </summary>
    public sealed class UiSafeArea : MonoBehaviour
    {
        Canvas canvas;
        RectTransform frame;
        Rect lastSafe;
        Vector2 lastDesign;

        public void Initialize(Canvas owner, RectTransform target)
        {
            canvas = owner;
            frame = target;
            lastSafe = new Rect(-1, -1, -1, -1);
        }

        void LateUpdate()
        {
            if (!canvas || !frame) return;
            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) return;
            Vector2 design = frame.sizeDelta;
            if (design.x <= 0 || design.y <= 0) return;
            if (safe == lastSafe && design == lastDesign) return;
            lastSafe = safe;
            lastDesign = design;
            float scale = Mathf.Max(.01f, canvas.scaleFactor);
            float fit = Mathf.Min(1f, Mathf.Min(safe.width / (design.x * scale), safe.height / (design.y * scale)));
            frame.localScale = new Vector3(fit, fit, 1f);
            Vector2 screenCenter = new Vector2(Screen.width, Screen.height) * .5f;
            frame.anchoredPosition = (safe.center - screenCenter) / scale;
        }
    }
}
