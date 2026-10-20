using UnityEngine;
using LestingCraft.Runtime;

namespace LestingCraft.Presentation
{
    [DefaultExecutionOrder(100)]
    public sealed class VoxelSkyVolume : MonoBehaviour
    {
        static VoxelSkyVolume owner;
        public VoxelWorld World;
        const int Width = 128, Height = 96;
        Texture3D texture;
        byte[] pixels;
        Vector3Int origin, requestedOrigin;
        bool dirty = true, uploaded, editPending, haveRequestedOrigin;
        float streamDeadline, editDeadline, moveDeadline, nextUpload;

        // Edits have priority over streaming; streaming callbacks cannot postpone this deadline.
        public void Invalidate()
        {
            dirty = true;
            if (!editPending) editDeadline = Time.unscaledTime + .08f;
            editPending = true;
        }

        public void InvalidateStreaming()
        {
            dirty = true;
            streamDeadline = Time.unscaledTime + .65f;
        }

        void LateUpdate()
        {
            if (!World) return;
            float now = Time.unscaledTime;
            Vector3 p = World.Player ? World.Player.position : new Vector3(0, 60, 0);
            var target = new Vector3Int(
                Mathf.FloorToInt(p.x / 16) * 16 + 8 - Width / 2,
                Mathf.Clamp(Mathf.FloorToInt(p.y / 16) * 16 - 32, 0, 256 - Height),
                Mathf.FloorToInt(p.z / 16) * 16 + 8 - Width / 2);
            if (!haveRequestedOrigin || target != requestedOrigin)
            {
                requestedOrigin = target;
                haveRequestedOrigin = true;
                moveDeadline = now + .08f;
            }
            bool moved = !uploaded || target != origin;
            if (!dirty && !moved) return;
            if (now < nextUpload) return;
            bool ready = !uploaded || moved && now >= moveDeadline ||
                editPending && now >= editDeadline || dirty && now >= streamDeadline && !editPending;
            if (!ready) return;
            if (!texture)
            {
                texture = new Texture3D(Width, Height, Width, TextureFormat.R8, false)
                {
                    name = "Local propagated voxel skylight",
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                pixels = new byte[Width * Height * Width];
            }
            // CopyVolume is not documented as thread-safe. Keep it on the owner thread.
            // Apply is an indivisible driver upload, not a guaranteed fixed-time operation.
            World.Skylight.CopyVolume(pixels, target.x, target.y, target.z, Width, Height, Width);
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, false);
            origin = target;
            owner = this;
            Shader.SetGlobalTexture("_LCVoxelSky", texture);
            Shader.SetGlobalVector("_LCVoxelSkyOrigin", new Vector4(origin.x, origin.y, origin.z, 1));
            Shader.SetGlobalVector("_LCVoxelSkySize", new Vector4(Width, Height, Width, 0));
            dirty = false;
            editPending = false;
            uploaded = true;
            nextUpload = now + .08f;
        }

        void OnDestroy()
        {
            if (owner == this)
            {
                owner = null;
                Shader.SetGlobalVector("_LCVoxelSkyOrigin", Vector4.zero);
                Shader.SetGlobalTexture("_LCVoxelSky", null);
            }
            if (texture) Destroy(texture);
        }
    }
}
