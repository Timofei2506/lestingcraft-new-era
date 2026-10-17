using UnityEngine;
namespace LestingCraft.World
{
 // Analytic surface matches Water.shader, including world-clock wrap and weather energy.
 // This couples swimming, breathing and underwater fog to the displaced surface, not a flat cube.
 public static class WaterSurface
 {
  public static float Offset(float x,float z,double clock,float wind)
  {float t=(float)(clock%86400),energy=.78f+wind*.55f;return (Mathf.Sin(x*.23f+z*.12f-t*1.18f)*.115f+Mathf.Sin(x*-.37f+z*.51f-t*1.73f)*.060f+Mathf.Sin(x*.91f-z*.73f+t*2.34f)*.021f)*energy;}
 }
}
