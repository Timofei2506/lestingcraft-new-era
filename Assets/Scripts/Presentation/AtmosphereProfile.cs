using UnityEngine;

namespace LestingCraft.Presentation
{
    /// <summary>One participating-medium pass, not linear fog plus volumetric fog stacked together.</summary>
    public static class AtmosphereProfile
    {
        public static float Density(float rain,float underwater=0)
            =>Mathf.Lerp(.00035f+Mathf.Clamp01(rain)*.0025f,.16f,Mathf.Clamp01(underwater));
        public static float OpacityAt(float distance,float rain)
            =>1-Mathf.Exp(-Density(rain)*Mathf.Max(0,distance-8));
    }
}
