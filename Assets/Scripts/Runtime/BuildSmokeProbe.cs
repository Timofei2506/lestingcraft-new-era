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
    public sealed partial class BuildSmokeProbe : MonoBehaviour
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
        {
            if(!finishing&&(type==LogType.Exception||type==LogType.Error||type==LogType.Assert))
            {
                // A thrown UI callback can terminate Start() itself; do not leave
                // a broken game spamming exceptions for the entire CI timeout.
                failure=condition;finishing=true;
                Debug.LogError("[Smoke] FAIL runtime exception: "+condition);
                Application.Quit(2);
            }
        }
        IEnumerator Start()
        {
            yield return null;
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null){Fail("A real graphics device is required; -nographics cannot test this UI.");yield break;}
            Debug.Log("[Smoke] Graphics: "+SystemInfo.graphicsDeviceType+" / "+SystemInfo.graphicsDeviceName);
            var game=FindFirstObjectByType<PrototypeGame>();var ui=FindFirstObjectByType<GameUI>();
            if(!game||!Camera.main||!ui||!ui.IsReady||failure!=null){Fail("Bootstrap/menu initialization: "+failure);yield break;}
            if(!MenuArtwork.Backdrop){Fail("Menu backdrop is missing.");yield break;}
            foreach(var name in new[]{"VoxelPBR","VoxelObjects","Water","Grass","Sky","Atmosphere","Precipitation","Fire","Smoke","Crystal","Glass","StoryBlood"})
            {
                var material=Resources.Load<Material>("Generated/"+name);
                if(!material||!material.shader||!material.shader.isSupported){Fail("Missing/unsupported material: "+name);yield break;}
            }
            foreach(var arrayName in new[]{"Albedo","Normal","Mask"})
            {var array=Resources.Load<Texture2DArray>("Generated/"+arrayName);if(!array||array.depth!=SurfaceLayers.Names.Length||array.width!=512){Fail("Incomplete current PBR array: "+arrayName);yield break;}}
            foreach(var name in new[]{"Home.Play","Home.Coop","Home.Settings","Home.Quit"})
                if(!FindButton(ui,name)){Fail("Home control missing: "+name);yield break;}
            // Capture the real menu from the executable, after the splash, not a mock image.
            float deadline=Time.realtimeSinceStartup+30;
            while(!SplashScreen.isFinished&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForEndOfFrame();
            string folder=Path.Combine(Application.dataPath,"..","SmokeResults");Directory.CreateDirectory(folder);
            string diagnostics=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","Logs","GraphicsDiagnostics"));Directory.CreateDirectory(diagnostics);
            string screenshot=Path.GetFullPath(Path.Combine(folder,"menu.png"));ScreenCapture.CaptureScreenshot(screenshot);
            yield return WaitForScreenshot(screenshot);
            if(!ScreenshotReady(screenshot)){Fail("Menu screenshot write timeout.");yield break;}
            if(!ScreenshotHasDetail(screenshot)){Fail("Menu screenshot is uniform/empty.");yield break;}
            Debug.Log("[Smoke] MENU_SCREENSHOT: "+screenshot);
            FindButton(ui,"Home.Play").onClick.Invoke();if(ui.ScreenName!="World"){Fail("Play button did not open mode selection.");yield break;}
            ui.ShowHome();FindButton(ui,"Home.Coop").onClick.Invoke();if(ui.ScreenName!="Rooms"){Fail("Coop button did not open rooms.");yield break;}
            ui.ShowHome();FindButton(ui,"Home.Settings").onClick.Invoke();if(ui.ScreenName!="Settings"){Fail("Settings button did not open settings.");yield break;}
            foreach(int id in ItemArtwork.AllIds)if(!ItemArtwork.Get(id)){Fail("Item icon missing: "+id);yield break;}
            ui.ShowHome();game.SelectedMode=0;
            // CI runs on a software rasterizer (WARP), not on target hardware. Use the
            // minimum supported view distance (the settings slider range is 2..5) so this
            // probe still streams a complete world (5x5 chunks) without its duration
            // depending on the frame rate of a software GPU. The Ultra graphics checks
            // later in this probe restore full settings before their screenshots.
            if(VisualSettings.Current!=null){VisualSettings.Current.ViewDistance=2;VisualSettings.Current.Save();}
            game.PlayOffline();
            // A reproducible clear-day baseline; only the explicit smoke process fixes its clock.
            deadline=Time.realtimeSinceStartup+300;
            float warmupStart=Time.realtimeSinceStartup;
            float lastLog=warmupStart;
            while(!game.WorldReady&&failure==null&&Time.realtimeSinceStartup<deadline)
            {if(game.Session)game.Session.Data.clock=420;
             // Drive the exact same Generate/Rebuild/mesher code synchronously (the
             // pump the showcase build already uses) instead of waiting on LateUpdate's
             // 3 ms-per-frame budget at ~2 FPS under WARP. Generation, skylight and
             // meshing work is unchanged and fully exercised; only the artificial
             // frame-rate dependency that timed out the old probe is removed.
             if(game.Session&&game.Session.World)game.Session.World.ShowcasePumpSync();
             if(Time.realtimeSinceStartup-lastLog>10f)
             {Debug.Log("[Smoke] World warmup: ready="+game.WorldReady+" chunks="+game.ChunkCount+" weather="+(game.Session?.Weather!=null)+" failure="+(failure??"null")+" elapsed="+(Time.realtimeSinceStartup-warmupStart)+"s");
              lastLog=Time.realtimeSinceStartup;}
             yield return null;}
            if(failure!=null||!game.Session||!game.WorldReady||game.ChunkCount<5||!game.Session.Weather)
            {string detail=" failure="+(failure??"null")+" session="+(game.Session!=null)+" worldReady="+game.WorldReady+" chunkCount="+game.ChunkCount+" weather="+(game.Session?.Weather!=null)+" elapsed="+(Time.realtimeSinceStartup-warmupStart)+"s";
             Fail("World/meshes/weather initialization:"+detail);yield break;}
            if(RenderSettings.fog){Fail("Duplicate linear fog must be disabled.");yield break;}
            // The compulsory ruined-house opening remains in the game AND in this native test.
            // Older probes assumed an immediately unlocked survival world and failed after chapter one.
            // Opening cinematic can be heavier after era-v9, allow 90s with periodic log
            deadline=Time.realtimeSinceStartup+90;
            lastLog=Time.realtimeSinceStartup;
            while(game.StoryLocked&&failure==null&&Time.realtimeSinceStartup<deadline)
            {if(Time.realtimeSinceStartup-lastLog>10f){Debug.Log("[Smoke] Opening warmup: storyLocked="+game.StoryLocked+" failure="+(failure??"null")); lastLog=Time.realtimeSinceStartup;} yield return null;}
            if(game.StoryLocked||failure!=null){Fail("Mandatory opening did not finish its initial cinematic: "+failure+" elapsed="+(Time.realtimeSinceStartup-(deadline-90))+"s");yield break;}
            var livingWorld=game.Session.World;
            if(livingWorld.ContentVersion!=9||!game.Session.Living||!game.Session.Living.Enabled||game.Session.Vitals.body.hp==null||game.Session.Vitals.body.hp.Length!=6)
            {Fail("Current living-world runtime systems missing: content="+livingWorld.ContentVersion+" (expected 9), living="+(game.Session.Living!=null)+", livingEnabled="+(game.Session.Living&&game.Session.Living.Enabled)+", bodyZones="+(game.Session.Vitals.body.hp==null?-1:game.Session.Vitals.body.hp.Length));yield break;}
            var modelProbe=new GameObject("Smoke-only native villager and cat mesh checks");
            modelProbe.transform.position=game.Session.Player.transform.position+Vector3.down*20;
            for(int kind=0;kind<2;kind++)
            {var actor=new GameObject(kind==0?"Native villager probe":"Native cat probe");actor.transform.SetParent(modelProbe.transform,false);actor.AddComponent<SettlerVisual>().Build(kind,1);
             var renderers=actor.GetComponentsInChildren<MeshRenderer>();if(renderers.Length<5){Fail("Incomplete native settler mesh: "+kind);yield break;}
             foreach(var renderer in renderers)if(!renderer.sharedMaterial||!renderer.sharedMaterial.shader.isSupported){Fail("Unsupported settler material");yield break;}}
            Destroy(modelProbe);
            if(CreativeCatalog.Categories.Length!=8||CreativeCatalog.Categories[7]!="Космос")
            {Fail("Creative site blueprints absent from the catalogue");yield break;}
            Debug.Log("[Smoke] LIVING6: rules"+GameRules.Version+", content"+game.Session.World.ContentVersion+", complete PBR arrays="+SurfaceLayers.Names.Length+", mandatory opening, six saved body zones, native villager/cat meshes and materials loaded");
            var player=game.Session.Player;player.enabled=false;
            // Exercise REAL Unity objects, not only the pure C# shuttle checks: cabin walls, moving lift,
            // interactive doors, six 3D chairs and two instrument text screens. Keep it off-camera.
            var shuttleProbe=new GameObject("Smoke-only walkable shuttle");
            shuttleProbe.transform.position=player.transform.position+Vector3.down*120;
            var shuttle=shuttleProbe.AddComponent<ShuttleInteriorPreview>();shuttle.Initialize(player);
            int shuttleColliders=shuttleProbe.GetComponentsInChildren<Collider>().Length;
            if(shuttleColliders<55||shuttleProbe.GetComponentsInChildren<TextMesh>().Length<2)
            {Fail("Walkable shuttle geometry, chairs or MFDs missing: "+shuttleColliders+" colliders");yield break;}
            foreach(var mesh in shuttleProbe.GetComponentsInChildren<MeshRenderer>())
                if(!mesh.sharedMaterial||!mesh.sharedMaterial.shader||!mesh.sharedMaterial.shader.isSupported)
                {Fail("Shuttle cabin material unsupported: "+mesh.name);yield break;}
            shuttle.Session.UpdatePosition(0,0,0);
            if(!shuttle.Session.BoardLift()||!shuttle.Session.Elevator.RequestUp())
            {Fail("Shuttle lift boarding or call failed");yield break;}
            shuttle.Session.Elevator.Advance(3f);
            if(Mathf.Abs(shuttle.Session.Elevator.Height-14f)>.01f)
            {Fail("Shuttle lift must travel continuously, 14m after 3s");yield break;}
            shuttle.Session.Elevator.Advance(3f);
            if(shuttle.Session.Elevator.State!=ElevatorState.Top||!shuttle.Session.SetDoor(InteriorDoor.Outer,true)||shuttle.Session.SetDoor(InteriorDoor.Inner,true))
            {Fail("Shuttle lift destination or airlock interlock failed");yield break;}
            Destroy(shuttleProbe);
            Debug.Log("[Smoke] SHUTTLE_INTERIOR: "+shuttleColliders+" physical colliders, six chairs, two MFDs, 28m animated lift and airlock interlock");
            Camera.main.transform.localRotation=Quaternion.Euler(10,35,0);
            var fireCell=Vector3Int.FloorToInt(player.transform.position+new Vector3(1,0,2));game.Session.World.Edit(fireCell.x,fireCell.y,fireCell.z,15);
            yield return null;yield return null;yield return new WaitForEndOfFrame();
            var placed=GameObject.Find("Placed voxel model 15");if(!placed||placed.GetComponent<MeshFilter>().sharedMesh!=VoxelItemModels.MeshFor(15)){Fail("Placed campfire must use authored icon geometry");yield break;}
            Debug.Log("[Smoke] CAMPFIRE_PARITY: same shared mesh as icon/held model, no opaque cube");
            var crystalCell=Vector3Int.FloorToInt(player.transform.position)+new Vector3Int(2,0,3);
            game.Session.World.Edit(crystalCell.x,crystalCell.y-1,crystalCell.z,1);
            game.Session.World.Edit(crystalCell.x,crystalCell.y,crystalCell.z,11);
            game.Session.World.Edit(crystalCell.x,crystalCell.y+1,crystalCell.z,0);
            yield return null;yield return null;
            var crystal=GameObject.Find("Placed crystal clusters");if(!crystal||!crystal.GetComponent<MeshCollider>().sharedMesh){Fail("Authored crystal render/collision missing");yield break;}
            game.Session.World.Edit(crystalCell.x,crystalCell.y,crystalCell.z-1,0);
            var crystalOrigin=(Vector3)crystalCell+new Vector3(.5f,.5f,-1);
            if(!VoxelRaycast.Trace(crystalOrigin,Vector3.forward,3,game.Session.World.GetBlock,out var crystalHit)||crystalHit.Cell!=crystalCell){Fail("Native crystal voxel targeting mismatch");yield break;}
            if(!game.Session.Animals||game.Session.Animals.Save().animals.Length==0){Fail("Runtime wildlife did not spawn");yield break;}
            var wildlifeKinds=new System.Collections.Generic.HashSet<int>();foreach(var animal in game.Session.Animals.Save().animals)if(!animal.dead)wildlifeKinds.Add(animal.species);
            if(wildlifeKinds.Count<3||FindObjectsByType<AnimalVisual>(FindObjectsSortMode.None).Length<3){Fail("Deer / hare / boar runtime visuals missing");yield break;}
            if(crystal.GetComponent<MeshRenderer>().sharedMaterial.shader.name!="LestingCraft/CrystalGlass"){Fail("Crystal must use transmitting sparkle shader");yield break;}
            Debug.Log("[Smoke] FOUNDATION11: real deer/hare/boar runtime meshes; transmitting crystal; PBR layers="+SurfaceLayers.Names.Length+"; content version="+game.Session.World.ContentVersion);
            deadline=Time.realtimeSinceStartup+25;CampfireEffects effects=null;
            while(!effects&&Time.realtimeSinceStartup<deadline){effects=FindFirstObjectByType<CampfireEffects>();yield return null;}
            if(!effects||!effects.Smoke||!effects.Sparks){Fail("Campfire smoke/spark emitters missing");yield break;}
            effects.Smoke.Simulate(1,true,false,true);effects.Smoke.Play();effects.Sparks.Simulate(.8f,true,false,true);effects.Sparks.Play();
            if(effects.Smoke.particleCount==0||effects.Sparks.particleCount==0){Fail("Campfire emitters did not produce live particles");yield break;}
            var rendererData=Resources.Load<UnityEngine.Rendering.Universal.UniversalRendererData>("Generated/Renderer");bool ssao=false;
            foreach(var feature in rendererData.rendererFeatures)if(feature is UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion&&feature.isActive)ssao=true;
            if(!ssao){Fail("Serialized SSAO feature is missing/inactive");yield break;}
            var mainCameraData=Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var fogEvidence=GameObject.Find("Volumetric light shafts / height fog").GetComponent<MeshRenderer>();
            fogEvidence.enabled=false;mainCameraData.renderPostProcessing=false;Shader.SetGlobalFloat("_LCAmbientOcclusionProbe",1);
            yield return null;yield return null;yield return new WaitForEndOfFrame();
            string aoShot=Path.GetFullPath(Path.Combine(diagnostics,"ssao-gpu-proof.png"));ScreenCapture.CaptureScreenshot(aoShot);yield return WaitForScreenshot(aoShot);
            Shader.SetGlobalFloat("_LCAmbientOcclusionProbe",0);mainCameraData.renderPostProcessing=true;fogEvidence.enabled=true;
            var aoReadback=new Texture2D(2,2,TextureFormat.RGBA32,false);aoReadback.LoadImage(File.ReadAllBytes(aoShot));int aoPixels=0,occludedPixels=0;
            foreach(var pixel in aoReadback.GetPixels32())if(pixel.r<30&&pixel.b<30&&pixel.g>20){aoPixels++;if(pixel.g<250)occludedPixels++;}Destroy(aoReadback);
            if(aoPixels<2048||occludedPixels<32){Fail("GPU SSAO output has no active sampling/occlusion: green="+aoPixels+", occluded="+occludedPixels);yield break;}
            Debug.Log("[Smoke] FOUNDATION10: live smoke="+effects.Smoke.particleCount+" sparks="+effects.Sparks.particleCount+"; shared crystal shape/render/collision/targeting; GPU SSAO="+aoPixels+" sampled pixels, "+occludedPixels+" occluded pixels");
            yield return null;yield return new WaitForEndOfFrame();
            var hands=player.GetComponent<FirstPersonHands>();
            if(!hands||!hands.ViewCamera||!hands.ViewCamera.enabled){Fail("First-person hand camera not active.");yield break;}
            var body=ui.GetComponentInChildren<HealthSilhouette>();
            if(!body||body.Zones.Length!=6){Fail("HUD silhouette missing.");yield break;}
            game.Session.Inventory.Add(1,8);game.Session.Inventory.selected=Array.FindIndex(game.Session.Inventory.slots,stack=>stack.item==1&&stack.count>0);
            yield return null;yield return new WaitForEndOfFrame();
            Rect heldRect=hands.HeldViewportBounds();
            if(heldRect.width<=0||heldRect.width>.28f||heldRect.height>.38f||heldRect.Contains(new Vector2(.5f,.5f)))
            {Fail("Held block obscures gameplay: "+heldRect);yield break;}
            Debug.Log("[Smoke] VISUAL_FIX: held block viewport="+heldRect+", no center obstruction; actual frame "+Screen.width+"x"+Screen.height);
            string worldshot=Path.GetFullPath(Path.Combine(folder,"world-hud.png"));ScreenCapture.CaptureScreenshot(worldshot);
            yield return WaitForScreenshot(worldshot);
            if(!ScreenshotReady(worldshot)){Fail("World screenshot write timeout.");yield break;}
            if(!ScreenshotHasDetail(worldshot)){Fail("World screenshot has no detail.");yield break;}
            Debug.Log("[Smoke] WORLD_SCREENSHOT: "+worldshot);
            foreach(int sample in new[]{4,7,8,44,47,48,113,135,138,140,141,142,143,144,145,149,153,164})game.Session.Inventory.Add(sample,3);
            game.ToggleInventory();yield return null;yield return null;
            if(!ui.InventoryRectsDoNotOverlap(out var layoutReason)||ui.InventoryTextPixelSize<13.5f)
            {Fail("Unreadable/overlapping inventory: "+layoutReason+"; text="+ui.InventoryTextPixelSize);yield break;}
            foreach(var label in ui.GetComponentsInChildren<Text>())if(label.transform.parent.name.StartsWith("Inventory.Slot.")&&!string.IsNullOrEmpty(label.text)&&label.fontSize>=15&&label.cachedTextGenerator.vertexCount<=0){Fail("Inventory stack/durability text was truncated: "+label.transform.parent.name);yield break;}
            Debug.Log("[Smoke] READABLE_UI: 1280 design, primary text="+ui.InventoryTextPixelSize+" real pixels; cells in-frame, no cell/portrait overlap");
            foreach(int gear in new[]{104,105,106,107,108,130,131,132})
            {var bag=game.Session.Inventory;bag.Add(gear,1);int index=Array.FindIndex(bag.slots,s=>s.item==gear&&s.count>0);ui.InventoryClick(index,false,true);if(bag.equipment[Equipment.Slot(gear)].item!=gear){Fail("Native shift-equip failed: "+gear);yield break;}}
            yield return null;
            var summary=GameObject.Find("Equipment summary");if(!summary||!summary.GetComponent<Text>().text.Contains("50%")){Fail("Live armor protection summary missing");yield break;}
            var helmetHover=Array.Find(ui.GetComponentsInChildren<ItemHover>(),h=>h.Item==105);
            if(!helmetHover||helmetHover.Durability!=180){Fail("Equipment tooltip has no real durability");yield break;}
            ui.ShowItemTooltip(105,new Vector2(1000,400),helmetHover.Durability);
            var gearTip=GameObject.Find("Item name tooltip");if(!gearTip||!gearTip.GetComponentInChildren<Text>().text.Contains("180 / 180")){Fail("Armor durability tooltip missing");yield break;}
            Debug.Log("[Smoke] EQUIPMENT: eight independent clothing/armor slots equipped through actual inventory clicks");
            ItemHover item=null;foreach(var hover in ui.GetComponentsInChildren<ItemHover>())if(hover.Item>0){item=hover;break;}
            if(!item){Fail("Inventory hover target missing.");yield break;}
            var point=RectTransformUtility.WorldToScreenPoint(null,item.transform.position)+new Vector2(30,-30);
            ExecuteEvents.Execute(item.gameObject,new PointerEventData(EventSystem.current){position=point},ExecuteEvents.pointerEnterHandler);
            var tooltip=GameObject.Find("Item name tooltip");
            if(!tooltip||!tooltip.GetComponentInChildren<Text>().text.StartsWith(Items.Get(item.Item).Name)){Fail("Item tooltip did not show its name.");yield break;}
            yield return null;yield return null;
            string inventoryshot=Path.GetFullPath(Path.Combine(folder,"inventory-tooltip.png"));
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(inventoryshot);
            yield return WaitForScreenshot(inventoryshot);
            if(!ScreenshotReady(inventoryshot)||!ScreenshotHasDetail(inventoryshot)||failure!=null){Fail("Inventory screenshot/runtime errors: "+failure);yield break;}
            Debug.Log("[Smoke] INVENTORY_SCREENSHOT: "+inventoryshot);
            // Explicit smoke fixtures only: exercise native inventory events and the station UI, not a mock-up.
            var inventory=game.Session.Inventory;inventory.Add(4,4);
            if(!game.Session.FillRecipe(0,2)){Fail("Could not fill logs recipe");yield break;}
            ui.InventoryClick(57,false,false);
            if(inventory.cursor.item!=120||inventory.cursor.count!=4){Fail("Native result click did not craft planks");yield break;}
            if(!inventory.AddStack(inventory.cursor)){Fail("Fixture bag full");yield break;}inventory.cursor=new ItemStack();inventory.revision++;
            if(!game.Session.FillRecipe(8,2)){Fail("Could not fill workbench recipe");yield break;}
            ui.InventoryClick(57,false,true);
            if(inventory.Count(16)!=1){Fail("Shift-craft did not make a workbench");yield break;}
            game.Resume();
            var benchCell=Vector3Int.FloorToInt(player.transform.position+Vector3.forward*2+Vector3.up);
            game.Session.World.Edit(benchCell.x,benchCell.y,benchCell.z,16);
            game.OpenStation(16,benchCell);
            if(!ui.InventoryOpen){Fail("Placed workbench did not open");yield break;}
            inventory.Add(1,12);inventory.Add(121,8);
            if(!game.Session.FillRecipe(3,3)){Fail("Workbench 3x3 recipe did not fill");yield break;}
            yield return null;yield return new WaitForEndOfFrame();
            if(!ui.InventoryRectsDoNotOverlap(out var craftLayout)){Fail("Craft layout: "+craftLayout);yield break;}
            string craftShot=Path.GetFullPath(Path.Combine(folder,"workbench-3x3.png"));ScreenCapture.CaptureScreenshot(craftShot);yield return WaitForScreenshot(craftShot);
            if(!ScreenshotReady(craftShot)||!ScreenshotHasDetail(craftShot)){Fail("Workbench screenshot invalid");yield break;}
            ui.InventoryClick(57,false,true);
            if(inventory.Count(101)!=1){Fail("Workbench result was not crafted");yield break;}
            game.Resume();game.Session.World.Edit(benchCell.x,benchCell.y,benchCell.z,17);game.OpenStation(17,benchCell);
            inventory.furnace.input=new ItemStack{item=110,count=2};inventory.furnace.fuel=new ItemStack{item=4,count=1};inventory.revision++;
            if(!ui.InventoryOpen||!game.Session.StationNearby(17,benchCell))
            {Fail("Furnace fixture did not open at the placed block");yield break;}
            // WARP renders at ~2 FPS: Unity caps deltaTime, so 20 wall seconds may contain
            // fewer than eight simulation seconds. Observe the REAL session update, never Step() it here.
            double cookingStartSimulation=Time.timeAsDouble,cookingStartWall=Time.realtimeSinceStartupAsDouble;
            float lastCookingProgress=-1;
            while(game.Session.Inventory.furnace.output.count==0&&failure==null&&
                !CookingWaitExpired(cookingStartSimulation,cookingStartWall,Time.timeAsDouble,Time.realtimeSinceStartupAsDouble))
            {
                var stove=game.Session.Inventory.furnace;
                if(Mathf.Abs(stove.progress-lastCookingProgress)>=.9f)
                {Debug.Log("[Smoke] FURNACE_WAIT: "+CookingState(game,ui,cookingStartSimulation,cookingStartWall));lastCookingProgress=stove.progress;}
                yield return null;
            }
            var cooked=game.Session.Inventory.furnace;
            if(cooked.output.item!=112||cooked.output.count<1||cooked.input.count!=1||cooked.fuel.count!=0||failure!=null)
            {Fail("Live furnace did not cook and consume ingredients correctly: "+CookingState(game,ui,cookingStartSimulation,cookingStartWall)+" / "+failure);yield break;}
            Debug.Log("[Smoke] FURNACE_COOKED: "+CookingState(game,ui,cookingStartSimulation,cookingStartWall));
            yield return new WaitForEndOfFrame();
            if(!ui.InventoryRectsDoNotOverlap(out var furnaceLayout)){Fail("Furnace layout: "+furnaceLayout);yield break;}
            yield return null;yield return null;
            string furnaceShot=Path.GetFullPath(Path.Combine(folder,"furnace-cooking.png"));ScreenCapture.CaptureScreenshot(furnaceShot);yield return WaitForScreenshot(furnaceShot);
            if(!ScreenshotReady(furnaceShot)||!ScreenshotHasDetail(furnaceShot)||failure!=null){Fail("Furnace screenshot/runtime errors: "+failure);yield break;}
            Debug.Log("[Smoke] CRAFTING_SCREENSHOTS: "+craftShot+" / "+furnaceShot);
            game.Resume();VisualSettings.Current.Ultra();yield return null;yield return null;yield return new WaitForEndOfFrame();
            string ultraShot=Path.GetFullPath(Path.Combine(folder,"world-ultra.png"));ScreenCapture.CaptureScreenshot(ultraShot);yield return WaitForScreenshot(ultraShot);
            if(!ScreenshotReady(ultraShot)||!ScreenshotHasDetail(ultraShot)||failure!=null){Fail("Ultra graphics frame/runtime errors: "+failure);yield break;}
            Debug.Log("[Smoke] ULTRA: 4096 shadows / 40 cloud steps / 32 volume steps / no hardware RT claim");
            // Reproducible in-world water fixture, not a painted reflection or a mock render.
            var savedPosition=Camera.main.transform.position;var savedRotation=Camera.main.transform.rotation;
            var pool=Vector3Int.FloorToInt(player.transform.position);var world=game.Session.World;
            for(int z=-1;z<=0;z++)for(int x=2;x<=8;x++)
            {world.Edit(pool.x+x,pool.y-1,pool.z+z,1);for(int y=0;y<5;y++)world.Edit(pool.x+x,pool.y+y,pool.z+z,0);}
            for(int z=1;z<=10;z++)for(int x=3;x<=7;x++)
            {
                world.Edit(pool.x+x,pool.y-2,pool.z+z,1);world.Edit(pool.x+x,pool.y-1,pool.z+z,7);
                for(int y=0;y<4;y++)world.Edit(pool.x+x,pool.y+y,pool.z+z,0);
            }
            for(int x=3;x<=7;x++)for(int y=0;y<3;y++)world.Edit(pool.x+x,pool.y+y,pool.z+10,17);
            Camera.main.transform.position=(Vector3)pool+new Vector3(5.5f,1.9f,-.5f);
            Camera.main.transform.LookAt((Vector3)pool+new Vector3(5.5f,.15f,6));
            for(int frame=0;frame<5;frame++)yield return null;
            yield return new WaitForEndOfFrame();string waterShot=Path.GetFullPath(Path.Combine(diagnostics,"water-fixture.png"));ScreenCapture.CaptureScreenshot(waterShot);yield return WaitForScreenshot(waterShot);
            var cameraData=Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();cameraData.renderPostProcessing=false;
            fogEvidence.enabled=false;Shader.SetGlobalFloat("_LCWaterReflectionProbe",1);yield return null;yield return null;yield return new WaitForEndOfFrame();
            string reflectionMask=Path.GetFullPath(Path.Combine(diagnostics,"water-ssr-proof.png"));ScreenCapture.CaptureScreenshot(reflectionMask);yield return WaitForScreenshot(reflectionMask);
            Shader.SetGlobalFloat("_LCWaterReflectionProbe",0);fogEvidence.enabled=true;cameraData.renderPostProcessing=true;Camera.main.transform.SetPositionAndRotation(savedPosition,savedRotation);
            var readback=new Texture2D(2,2,TextureFormat.RGBA32,false);readback.LoadImage(File.ReadAllBytes(reflectionMask));int hits=0;
            foreach(var pixel in readback.GetPixels32())if(pixel.r>175&&pixel.b>175&&pixel.g<55)hits++;Destroy(readback);
            if(hits<32||failure!=null){Fail("SSR fixture did not reflect on-screen scene geometry: "+hits+" pixels / "+failure);yield break;}
            Debug.Log("[Smoke] WATER_SSR_READBACK: "+hits+" actual ray/depth hits in real EXE; magenta=scene reflection, blue=sky fallback; four-step crossing refinement; NOT hardware ray tracing");
            yield return ProbeUsability(game,ui,folder);if(finishing)yield break;
            finishing=true;
            Debug.Log("[Smoke] PASS: graphics device, menu navigation, all item icons, full world streaming, single fog pass, six health zones, active hand viewmodel, inventory hover tooltip, real 2x2/3x3 crafting, timed furnace cooking live smoke/sparks, authored crystal collision/targeting, real GPU SSAO sampling/occlusion, SSR hit-mask readback and eleven gameplay screenshots, diagnostic masks/fixture kept separately in private build logs. WARP is not a user-GPU/FPS benchmark.");
            Application.Quit(0);
        }
        // A bounded simulation budget AND a wall-clock guard: a stalled/paused furnace must still fail.
        public static bool CookingWaitExpired(double startedSimulation,double startedWall,double simulation,double wall)
            =>simulation-startedSimulation>=FurnaceState.CookSeconds+6||wall-startedWall>=150;
        static string CookingState(PrototypeGame game,GameUI ui,double simulationStart,double wallStart)
        {
            var session=game.Session;var f=session.Inventory.furnace;
            float distance=Vector3.Distance(session.Player.transform.position,new Vector3(f.x+.5f,f.y+.5f,f.z+.5f));
            return $"sim={Time.timeAsDouble-simulationStart:F2}s wall={Time.realtimeSinceStartupAsDouble-wallStart:F2}s progress={f.progress:F2} burn={f.burn:F2} input={f.input.item}x{f.input.count} fuel={f.fuel.item}x{f.fuel.count} output={f.output.item}x{f.output.count} block={session.World.GetBlock(f.x,f.y,f.z)} distance={distance:F2} ready={session.World.Ready} paused={game.Paused} ui={ui.ScreenName} dead={session.Vitals.dead}";
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
