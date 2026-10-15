using System;
using System.Collections;
using UnityEngine;
using LestingCraft.Presentation;

namespace LestingCraft.Runtime
{
    /// <summary>Opt-in CI check of the actual Windows executable, not a visual/performance benchmark.</summary>
    public sealed class BuildSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-lc-smoke-test")<0)return;
            new GameObject("Cloud build smoke probe").AddComponent<BuildSmokeProbe>();
        }
        IEnumerator Start()
        {
            yield return null;
            var game=FindFirstObjectByType<PrototypeGame>();
            if(!game||!Camera.main||!FindFirstObjectByType<GameUI>()){Fail("Bootstrap/menu/camera missing");yield break;}
            foreach(var name in new[]{"VoxelPBR","Water","Grass","Sky","Atmosphere","Precipitation","Fire"})
            {
                var material=Resources.Load<Material>("Generated/"+name);
                if(!material||!material.shader){Fail("Missing material/shader: "+name);yield break;}
            }
            if(!Resources.Load<Texture2DArray>("Generated/Albedo")){Fail("Missing texture arrays");yield break;}
            // This flag is cloud-test-only. No network room is created; no user PC is involved.
            game.SelectedMode=2;game.PlayOffline();
            float deadline=Time.realtimeSinceStartup+80;
            while(game.ChunkCount<5&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!game.Session||game.ChunkCount<5||!game.Session.Weather){Fail("World/meshes/weather did not initialize");yield break;}
            Debug.Log("[Smoke] PASS: Windows menu, generated resources, world chunks and weather initialized. GPU appearance is NOT tested.");
            Application.Quit(0);
        }
        static void Fail(string reason){Debug.LogError("[Smoke] FAIL: "+reason);Application.Quit(2);}
    }
}
