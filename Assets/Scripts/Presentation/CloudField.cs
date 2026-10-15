using UnityEngine;
namespace LestingCraft.Presentation
{
    /// <summary>Seeded seamless value noise, not repeated trigonometric cloud stamps.</summary>
    public static class CloudField
    {
        static float Hash(int x,int y,int z,int period)
        {
            x=(x%period+period)%period;y=(y%period+period)%period;z=(z%period+period)%period;
            unchecked {uint h=(uint)(x*374761393+y*668265263+z*2147483647)^0x94ab31u;h=(h^(h>>13))*1274126177u;return (h^(h>>16))/4294967295f;}
        }
        static float Fade(float t)=>t*t*t*(t*(t*6-15)+10);
        public static float Noise(float x,float y,float z,int period)
        {
            int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y),iz=Mathf.FloorToInt(z);float u=Fade(x-ix),v=Fade(y-iy),w=Fade(z-iz);
            return Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(Hash(ix,iy,iz,period),Hash(ix+1,iy,iz,period),u),Mathf.Lerp(Hash(ix,iy+1,iz,period),Hash(ix+1,iy+1,iz,period),u),v),Mathf.Lerp(Mathf.Lerp(Hash(ix,iy,iz+1,period),Hash(ix+1,iy,iz+1,period),u),Mathf.Lerp(Hash(ix,iy+1,iz+1,period),Hash(ix+1,iy+1,iz+1,period),u),v),w);
        }
        public static float Shape(float x,float y,float z)=>Noise(x*4,y*4,z*4,4)*.55f+Noise(x*8+7,y*8+3,z*8+11,8)*.30f+Noise(x*16,y*16,z*16,16)*.15f;
        public static Vector4 Offset(double clock,int seed,float wind)
        {float t=(float)(clock%86400),phase=(seed%997)*.17f,speed=.9f+Mathf.Clamp01(wind)*1.5f;return new Vector4(t*speed+phase,0,t*speed*.37f-phase,0);}
    }
}
