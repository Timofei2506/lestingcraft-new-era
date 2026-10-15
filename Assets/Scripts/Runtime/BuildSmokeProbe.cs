using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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
            foreach(var name in new[]{"VoxelPBR","Water","Grass","Sky","Atmosphere","Precipitation","Fire"})
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
            deadline=Time.realtimeSinceStartup+20;
            while(!File.Exists(screenshot)&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!File.Exists(screenshot)){Fail("Menu screenshot was not written.");yield break;}
            yield return new WaitForSecondsRealtime(.5f);
            if(!ScreenshotHasDetail(screenshot)){Fail("Menu screenshot is uniform/empty.");yield break;}
            Debug.Log("[Smoke] MENU_SCREENSHOT: "+screenshot);
            FindButton(ui,"Home.Play").onClick.Invoke();if(ui.ScreenName!="World"){Fail("Play button did not open mode selection.");yield break;}
            ui.ShowHome();FindButton(ui,"Home.Coop").onClick.Invoke();if(ui.ScreenName!="Rooms"){Fail("Coop button did not open rooms.");yield break;}
            ui.ShowHome();FindButton(ui,"Home.Settings").onClick.Invoke();if(ui.ScreenName!="Settings"){Fail("Settings button did not open settings.");yield break;}
            ui.ShowHome();game.SelectedMode=2;game.PlayOffline();
            deadline=Time.realtimeSinceStartup+150;
            while(game.ChunkCount<5&&failure==null&&Time.realtimeSinceStartup<deadline)yield return null;
            if(failure!=null||!game.Session||game.ChunkCount<5||!game.Session.Weather){Fail("World/meshes/weather initialization: "+failure);yield break;}
            finishing=true;
            Debug.Log("[Smoke] PASS: graphics device, nonempty menu screenshot, Play/Coop/Settings navigation, resources, five world chunks and weather. No FPS/visual-quality claim.");
            Application.Quit(0);
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
