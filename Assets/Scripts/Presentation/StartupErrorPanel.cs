using UnityEngine;
using UnityEngine.UI;

namespace LestingCraft.Presentation
{
    /// <summary>Never leave a silent blue screen if the normal interface cannot initialize.</summary>
    public static class StartupErrorPanel
    {
        public static void Show()
        {
            var root=new GameObject("Startup error screen",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1000;
            var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);
            var panel=new GameObject("Error background",typeof(RectTransform),typeof(Image));panel.transform.SetParent(root.transform,false);
            var rt=panel.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;panel.GetComponent<Image>().color=new Color(.025f,.045f,.055f);
            var text=new GameObject("Startup diagnostic",typeof(RectTransform),typeof(Text));text.transform.SetParent(root.transform,false);
            rt=text.GetComponent<RectTransform>();rt.anchorMin=new Vector2(.12f,.2f);rt.anchorMax=new Vector2(.88f,.8f);rt.offsetMin=rt.offsetMax=Vector2.zero;
            var label=text.GetComponent<Text>();label.font=Resources.Load<Font>("UI/Manrope");if(!label.font)label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize=25;label.color=Color.white;label.alignment=TextAnchor.MiddleLeft;
            label.text="LESTINGCRAFT: NEW ERA\n\nНе удалось создать интерфейс.\nЭто ошибка запуска, а не загрузка мира.\n\nПодробности записаны в Player.log.\nЗакройте игру через Alt+F4 и передайте лог разработчику.";
        }
    }
}
