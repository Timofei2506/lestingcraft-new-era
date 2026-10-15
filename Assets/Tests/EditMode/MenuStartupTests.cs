using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LestingCraft.Runtime;
using LestingCraft.Presentation;

namespace LestingCraft.Tests
{
    public sealed class MenuStartupTests
    {
        [Test] public void UIChromeDoesNotDependOnRoundRectPNG()
        {
            Assert.IsNotNull(MenuArtwork.Rounded);Assert.IsNotNull(MenuArtwork.Outline);
            Assert.AreEqual(64,MenuArtwork.Rounded.texture.width);Assert.Greater(MenuArtwork.Rounded.border.x,0);
            Assert.AreEqual(TextureWrapMode.Clamp,MenuArtwork.Rounded.texture.wrapMode);
        }
        [Test] public void MenuBackdropIsAnExplicitGeneratedTextureAsset()
        {
            var texture=Resources.Load<Texture2D>("Generated/MenuBackdrop");Assert.IsNotNull(texture);
            Assert.GreaterOrEqual(texture.width,1280);Assert.GreaterOrEqual(texture.height,720);Assert.IsNotNull(MenuArtwork.Backdrop);
        }
        [Test] public void MenuReadabilityGradientIsGeneratedWithoutSourceTexture()
        {Assert.IsNotNull(MenuArtwork.Shade);Assert.Greater(MenuArtwork.Shade.GetPixel(0,4).a,MenuArtwork.Shade.GetPixel(127,4).a);}
        [Test] public void ActualInterfaceConstructsAndMainButtonsNavigate()
        {
            var owner=new GameObject("Inactive UI test owner");owner.SetActive(false);
            var interfaceObject=new GameObject("UI startup test");var previous=EventSystem.current;
            try
            {
                var game=owner.AddComponent<PrototypeGame>();VisualSettings.Load();
                var ui=interfaceObject.AddComponent<GameUI>();ui.Initialize(game);
                Assert.IsTrue(ui.IsReady);Assert.AreEqual("Home",ui.ScreenName);
                var art=interfaceObject.transform.Find("Menu Art").GetComponent<RawImage>();Assert.IsNotNull(art.texture);
                var button=Find(ui,"Home.Play");Assert.IsNotNull(button);button.onClick.Invoke();Assert.AreEqual("World",ui.ScreenName);
                ui.ShowHome();Find(ui,"Home.Coop").onClick.Invoke();Assert.AreEqual("Rooms",ui.ScreenName);
                ui.ShowHome();Find(ui,"Home.Settings").onClick.Invoke();Assert.AreEqual("Settings",ui.ScreenName);
                ui.ShowHome();Assert.IsNotNull(Find(ui,"Home.Quit"));
                Assert.Greater(interfaceObject.GetComponentsInChildren<Text>().Length,10);
            }
            finally
            {
                Object.DestroyImmediate(interfaceObject);Object.DestroyImmediate(owner);
                if(!previous&&EventSystem.current)Object.DestroyImmediate(EventSystem.current.gameObject);
            }
        }
        static Button Find(GameUI ui,string name)
        {foreach(var button in ui.GetComponentsInChildren<Button>())if(button.name==name)return button;return null;}
    }
}
