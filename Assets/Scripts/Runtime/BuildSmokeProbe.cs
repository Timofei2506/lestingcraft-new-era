using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LestingCraft.Gameplay;
using UnityEngine.Rendering;
using LestingCraft.Presentation;

namespace LestingCraft.Runtime
{
    /// <summary>Actual graphics-device UI startup test. WARP in CI is not a performance benchmark.</summary>
    public sealed class BuildSmokeProbe : MonoBehaviour
    {
        string failure;bool finishing;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-lc-smoke-test")<0)return;
            var obj=new GameObject("Cloud build smoke probe");DontDestroyOnLoad(obj);obj.AddComponent<BuildSmokeProbe>();
        }
        void Awake(){Application.logMessageReceived+=OnLog;}
        void OnLog(string condition,string trace,LogType type)
        {if(!finishing&&(type==LogType.Exception||type==LogType.Error||type==LogType.Assert))failure=condition;}
        IEnumerator Start()
        {
            yield return null;
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null){Fail("A real graphics device is required; -nographics cannot test this UI.");yield break;}
            Debug.Log("[Smoke] Graphics: "+SystemInfo.graphicsDeviceType+" / "+SystemInfo.graphicsDeviceName);
            var game=FindFirstObjectByType<PrototypeGame>();var ui=FindFirstObjectByType<GameUI>();
            if(!game||!Camera.main||!ui||!ui.IsReady||failure!=null){Fail("Bootstrap/menu initialization: "+failure);yield break;}
            if(!MenuArtwork.Backdrop){Fail("Menu backdrop is missing.");yield break;}
            foreach(var name in new[]{"VoxelPBR","VoxelObjects","Water","Grass","Sky","Atmosphere","Precipitation","Fire"})
            {
                var material=Resources.Load<Material>("Generated/"+name);
                if(!material||!material.shader||!material.shader.isSupported){Fail("Missing/unsupported material: "+name);yield break;}
            }
            if(!Resources.Load<Texture2DArray>("Generated/Albedo")){Fail("Missing texture arrays");yield break;}
            foreach(var name in new[]{"Home.Play","Home.Coop","Home.Settings","Home.Quit"})
                if(!FindButton(ui,name)){Fail("Home control missing: "+name);yield break;}
            // Capture the real menu from the executable, after the splash, not a mock image.
            float deadline=Time.realtimeSinceStartup+30;
            while(!SplashScreen.isFinished&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForEndOfFrame();
            string folder=Path.Combine(Application.dataPath,"..","SmokeResults");Directory.CreateDirectory(folder);
            string screenshot=Path.GetFullPath(Path.Combine(folder,"menu.png"));ScreenCapture.CaptureScreenshot(screenshot);
            yield return WaitForScreenshot(screenshot);
            if(!ScreenshotReady(screenshot)){Fail("Menu screenshot write timeout.");yield break;}
            if(!ScreenshotHasDetail(screenshot)){Fail("Menu screenshot is uniform/empty.");yield break;}
            Debug.Log("[Smoke] MENU_SCREENSHOT: "+screenshot);
            FindButton(ui,"Home.Play").onClick.Invoke();if(ui.ScreenName!="World"){Fail("Play button did not open mode selection.");yield break;}
            ui.ShowHome();FindButton(ui,"Home.Coop").onClick.Invoke();if(ui.ScreenName!="Rooms"){Fail("Coop button did not open rooms.");yield break;}
            ui.ShowHome();FindButton(ui,"Home.Settings").onClick.Invoke();if(ui.ScreenName!="Settings"){Fail("Settings button did not open settings.");yield break;}
            foreach(int id in ItemArtwork.AllIds)if(!ItemArtwork.Get(id)){Fail("Item icon missing: "+id);yield break;}
            ui.ShowHome();game.SelectedMode=0;game.PlayOffline();
            // A reproducible clear-day baseline; only the explicit smoke process fixes its clock.
            deadline=Time.realtimeSinceStartup+210;
            while(!game.WorldReady&&failure==null&&Time.realtimeSinceStartup<deadline)
            {if(game.Session)game.Session.Data.clock=420;yield return null;}
            if(failure!=null||!game.Session||!game.WorldReady||game.ChunkCount<5||!game.Session.Weather){Fail("World/meshes/weather initialization: "+failure);yield break;}
            if(RenderSettings.fog){Fail("Duplicate linear fog must be disabled.");yield break;}
            var player=game.Session.Player;player.enabled=false;
            Camera.main.transform.localRotation=Quaternion.Euler(10,35,0);
            yield return null;yield return new WaitForEndOfFrame();
            var hands=player.GetComponent<FirstPersonHands>();
            if(!hands||!hands.ViewCamera||!hands.ViewCamera.enabled){Fail("First-person hand camera not active.");yield break;}
            var body=ui.GetComponentInChildren<HealthSilhouette>();
            if(!body||body.Zones.Length!=6){Fail("HUD silhouette missing.");yield break;}
            string worldshot=Path.GetFullPath(Path.Combine(folder,"world-hud.png"));ScreenCapture.CaptureScreenshot(worldshot);
            yield return WaitForScreenshot(worldshot);
            if(!ScreenshotReady(worldshot)){Fail("World screenshot write timeout.");yield break;}
            if(!ScreenshotHasDetail(worldshot)){Fail("World screenshot has no detail.");yield break;}
            Debug.Log("[Smoke] WORLD_SCREENSHOT: "+worldshot);
            game.ToggleInventory();yield return null;
            ItemHover item=null;foreach(var hover in ui.GetComponentsInChildren<ItemHover>())if(hover.Item>0){item=hover;break;}
            if(!item){Fail("Inventory hover target missing.");yield break;}
            var point=RectTransformUtility.WorldToScreenPoint(null,item.transform.position)+new Vector2(30,-30);
            ExecuteEvents.Execute(item.gameObject,new PointerEventData(EventSystem.current){position=point},ExecuteEvents.pointerEnterHandler);
            var tooltip=GameObject.Find("Item name tooltip");
            if(!tooltip||tooltip.GetComponentInChildren<Text>().text!=Items.Get(item.Item).Name){Fail("Item tooltip did not show its name.");yield break;}
            string inventoryshot=Path.GetFullPath(Path.Combine(folder,"inventory-tooltip.png"));
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(inventoryshot);
            yield return WaitForScreenshot(inventoryshot);
            if(!ScreenshotReady(inventoryshot)||!ScreenshotHasDetail(inventoryshot)||failure!=null){Fail("Inventory screenshot/runtime errors: "+failure);yield break;}
            Debug.Log("[Smoke] INVENTORY_SCREENSHOT: "+inventoryshot);
            finishing=true;
            Debug.Log("[Smoke] PASS: graphics device, menu navigation, all item icons, full world streaming, single fog pass, six health zones, active hand viewmodel, inventory hover tooltip and three executable screenshots. WARP is not a user-GPU/FPS benchmark.");
            Application.Quit(0);
        }
        static IEnumerator WaitForScreenshot(string path)
        {
            // CaptureScreenshot writes asynchronously; one fixed second is not a completion signal on WARP.
            float deadline=Time.realtimeSinceStartup+25;
            while(!ScreenshotReady(path)&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;
        }
        static bool ScreenshotReady(string path)
        {
            try
            {
                // An exclusive read succeeds only after the native PNG writer closes its handle.
                using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))return file.Length>1024;
            }
            catch(IOException){return false;}
            catch(UnauthorizedAccessException){return false;}
        }
        static Button FindButton(GameUI ui,string name)
        {foreach(var button in ui.GetComponentsInChildren<Button>())if(button.name==name)return button;return null;}
        static bool ScreenshotHasDetail(string path)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!texture.LoadImage(File.ReadAllBytes(path)))return false;
                float min=1,max=0;
                for(int y=1;y<12;y++)for(int x=1;x<20;x++)
                {var color=texture.GetPixel(x*texture.width/20,y*texture.height/12);float value=color.grayscale;min=Mathf.Min(min,value);max=Mathf.Max(max,value);}
                return texture.width>=640&&texture.height>=360&&max-min>.10f;
            }
            finally{Destroy(texture);}
        }
        void Fail(string reason){finishing=true;Debug.LogError("[Smoke] FAIL: "+reason);Application.Quit(2);}
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
