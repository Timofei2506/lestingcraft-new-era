using System;
using UnityEngine;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Camera path maths for scripted story scenes. Pure functions of scene time, so online and offline
    /// clients (and EditMode tests) produce the same frame for the same story timestamp.
    /// </summary>
    public static class CutscenePath
    {
        /// <summary>Catmull-Rom spline through keyed points (time in seconds). Clamps outside the key range.</summary>
        public static Vector3 Sample(float t, params (float time, Vector3 value)[] keys)
        {
            if (keys == null || keys.Length == 0) return Vector3.zero;
            if (keys.Length == 1 || t <= keys[0].time) return keys[0].value;
            int last = keys.Length - 1;
            if (t >= keys[last].time) return keys[last].value;

            int i = 0;
            while (i < last - 1 && t >= keys[i + 1].time) i++;

            Vector3 p1 = keys[i].value;
            Vector3 p2 = keys[i + 1].value;
            Vector3 p0 = i > 0 ? keys[i - 1].value : p1;
            Vector3 p3 = i + 2 <= last ? keys[i + 2].value : p2;

            float span = keys[i + 1].time - keys[i].time;
            float u = span > 0f ? Mathf.Clamp01((t - keys[i].time) / span) : 1f;
            return CatmullRom(p0, p1, p2, p3, u);
        }

        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u;
            float u3 = u2 * u;
            return 0.5f * (2f * p1
                + (-p0 + p2) * u
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        /// <summary>Decaying camera shake from impacts at the given scene times (seconds). Deterministic, no RNG.</summary>
        public static Vector3 Shake(float t, float[] impactTimes, float amplitude, float decay = 2.6f, float frequency = 16f)
        {
            if (impactTimes == null || amplitude <= 0f) return Vector3.zero;
            Vector3 total = Vector3.zero;
            for (int i = 0; i < impactTimes.Length; i++)
            {
                float d = t - impactTimes[i];
                if (d < 0f) continue;
                float wave = Mathf.Exp(-decay * d) * Mathf.Sin(2f * Mathf.PI * frequency * d);
                // A fixed direction per impact keeps the motion deterministic and varied.
                Vector2 dir = new Vector2(Mathf.Cos(i * 2.1f + 0.4f), Mathf.Sin(i * 1.3f + 1.1f));
                total += new Vector3(dir.x, dir.y, 0f) * (amplitude * wave);
            }
            return total;
        }
    }
}
