using System;
using System.Text;
using UnityEngine;
using LestingCraft.World;
using LestingCraft.Presentation;

namespace LestingCraft.Runtime
{
    /// <summary>
    /// Opt-in runtime diagnostics for the "game lags the whole PC and then crashes" report.
    /// Nothing runs unless the player launches the EXE with <c>-lc-perf-log</c>:
    /// <c>LestingCraft.exe -lc-perf-log</c>. Every five seconds one line is appended to
    /// <c>%USERPROFILE%\AppData\LocalLow\&lt;company&gt;\LestingCraft\Player.log</c> with the frame
    /// rate, managed memory, streamed chunk/mesh/collider totals and the live wildlife count.
    /// That is the data needed to tell a GPU-bound frame from a leaking world streamer
    /// without guessing, and it never changes gameplay, visuals or save data.
    /// </summary>
    public sealed class PerfLog : MonoBehaviour
    {
        const float Interval = 5f;
        float timer, worstFrame, frames;
        int samples;
        long lastManaged;
        readonly StringBuilder line = new StringBuilder(220);
        PrototypeGame game;
        bool enabledByFlag;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-lc-perf-log") < 0) return;
            var host = new GameObject("Performance log (-lc-perf-log)");
            DontDestroyOnLoad(host);
            host.AddComponent<PerfLog>().enabledByFlag = true;
        }

        void Awake()
        {
            if (!enabledByFlag) { enabled = false; return; }
            Debug.Log("[Perf] logging every " + Interval + " s; close the game after the lag reproduces");
        }

        void Update()
        {
            if (!enabledByFlag) return;
            float dt = Time.unscaledDeltaTime;
            frames++;
            if (dt > worstFrame) worstFrame = dt;
            timer += dt;
            samples++;
            if (timer < Interval) return;

            float fps = samples / Mathf.Max(.0001f, timer);
            long managed = GC.GetTotalMemory(false);
            long delta = managed - lastManaged;
            lastManaged = managed;

            if (!game) game = FindAnyObjectByType<PrototypeGame>();
            VoxelWorld world = game && game.Session ? game.Session.World : null;
            int chunks = world ? world.LoadedChunks : 0;
            int renderers = 0, meshes = 0, colliders = 0;
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) { if (r) renderers++; }
            foreach (MeshCollider c in FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)) { if (c) colliders++; }
            meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            int animals = game && game.Session && game.Session.Animals ? game.Session.Animals.Animals.Count : 0;
            int dimension = world ? (int)world.Dimension : -1;
            int viewDistance = VisualSettings.Current != null ? VisualSettings.Current.ViewDistance : 0;

            line.Clear();
            line.Append("[Perf] fps=").Append(fps.ToString("0.0"))
                .Append(" worstFrameMs=").Append((worstFrame * 1000f).ToString("0")).Append("ms")
                .Append(" managedMB=").Append((managed / 1048576f).ToString("0.0"))
                .Append(" managedDeltaMB=").Append((delta / 1048576f).ToString("+0.0;-0.0;0"))
                .Append(" chunks=").Append(chunks)
                .Append(" viewDistance=").Append(viewDistance)
                .Append(" dimensions=").Append(dimension)
                .Append(" renderers=").Append(renderers)
                .Append(" meshColliders=").Append(colliders)
                .Append(" meshesAlive=").Append(meshes)
                .Append(" animals=").Append(animals)
                .Append(" screen=").Append(Screen.width).Append('x').Append(Screen.height)
                .Append(" gpu=").Append(SystemInfo.graphicsDeviceName)
                .Append(" vramMB=").Append(SystemInfo.graphicsMemorySize);
            Debug.Log(line.ToString());

            timer = 0f; worstFrame = 0f; samples = 0;
        }
    }
}
