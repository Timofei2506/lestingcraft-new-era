using System;

namespace LestingCraft.World
{
    // All surface coordinates are local metres. The same seed and coordinates always produce the same column.
    // These are geological starter biomes, not authored hero locations or finished planetary atmospheres.
    public enum PlanetBody { Moon = 1, Mars = 2, Venus = 3, Mercury = 4 }
    public enum PlanetSurface : byte { Regolith, Mare, Ejecta, RedDust, IronRidge, Canyon, SulfurPlain, BasaltFlow, Caldera, ScorchedRock, Crater }
    public struct PlanetColumn
    {
        public int Height;
        public PlanetSurface Surface;
    }

    public static class PlanetTerrain
    {
        public const int Version = 1;
        static float N(float x,float z,int seed) { return MoonTerrain.ValueNoise(x,z,seed); }
        static float Clamp(float x,float min,float max) { return Math.Max(min,Math.Min(max,x)); }
        static float Smooth(float x) { x=Clamp(x,0,1);return x*x*(3-2*x); }
        static int Floor(float x) { return (int)Math.Floor(x); }

        public static PlanetColumn Column(int x,int z,int seed,PlanetBody body)
        {
            if(body==PlanetBody.Moon)
            {
                var moon=MoonTerrain.Column(x,z,seed);
                return new PlanetColumn {Height=moon.Height,Surface=moon.Surface==MoonSurface.Mare?PlanetSurface.Mare:moon.Surface==MoonSurface.Ejecta?PlanetSurface.Ejecta:PlanetSurface.Regolith};
            }
            float broad=N(x/220f,z/220f,seed^17183);
            float medium=N(x/61f,z/61f,seed^33749);
            float fine=N(x/14f,z/14f,seed^54101);
            float h;PlanetSurface surface;
            switch(body)
            {
                case PlanetBody.Mars:
                    // Valles-like incised valleys, broad dusty plains and raised iron-rich ridges.
                    float fold=Math.Abs(N(x/300f,z/170f,seed^16217)-.5f);
                    float canyon=1-Smooth((fold-.045f)/.10f);
                    h=51+(broad-.5f)*27+(medium-.5f)*14+(fine-.5f)*3-canyon*18;
                    surface=canyon>.64f?PlanetSurface.Canyon:h>51?PlanetSurface.IronRidge:PlanetSurface.RedDust;
                    break;
                case PlanetBody.Venus:
                    // Heavily resurfaced highlands, shield flows and occasional volcanic calderas.
                    float flow=N(x/105f,z/105f,seed^28411);
                    h=48+(broad-.5f)*29+(medium-.5f)*10+(fine-.5f)*2;
                    surface=flow>.61f?PlanetSurface.BasaltFlow:PlanetSurface.SulfurPlain;
                    int vx=Floor(x/112f),vz=Floor(z/112f);
                    for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)
                    {
                        int cx=vx+dx,cz=vz+dz;
                        uint hash=MoonTerrain.Hash(cx,cz,seed^61927);
                        if(hash%5!=0)continue;
                        float px=cx*112+24+(hash>>8)%64,pz=cz*112+24+(hash>>16)%64;
                        float d=(float)Math.Sqrt((x-px)*(x-px)+(z-pz)*(z-pz));
                        if(d<17)
                        {
                            float t=d/17;
                            h+=t<.72f?-11*(1-Smooth(t/.72f)):5*(1-Smooth((t-.72f)/.28f));
                            if(t<.65f)surface=PlanetSurface.Caldera;
                        }
                    }
                    break;
                case PlanetBody.Mercury:
                    h=43+(broad-.5f)*19+(medium-.5f)*11+(fine-.5f)*2;
                    surface=PlanetSurface.ScorchedRock;
                    int mx=Floor(x/48f),mz=Floor(z/48f);
                    for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)
                    {
                        var c=MoonTerrain.CraterInCell(mx+dx,mz+dz,seed^78221);
                        if(!c.Present)continue;
                        float fx=x+.5f-c.X,fz=z+.5f-c.Z;
                        float d=(float)Math.Sqrt(fx*fx+fz*fz);
                        if(d<c.Radius*3f)
                        {
                            h+=MoonTerrain.CraterDelta(c,d);
                            if(d<c.Radius*1.3f)surface=PlanetSurface.Crater;
                        }
                    }
                    break;
                default: throw new ArgumentOutOfRangeException("body");
            }
            return new PlanetColumn {Height=Math.Max(12,Math.Min(105,(int)Math.Round(h))),Surface=surface};
        }

        // Existing atlas block IDs only. No terrestrial vegetation, standing water, or Earth ores on airless surfaces.
        // Return no molten lava at spawn height: Venus' calderas are solid until heat/suit gameplay is available.
        public static ushort Block(int y,PlanetColumn column,PlanetBody body)
        {
            if(y<0||y>=256)return 0;
            if(y==0)return 9;
            if(y>column.Height)return 0;
            if(y<column.Height-5)return body==PlanetBody.Venus?(ushort)10:(ushort)1;
            if(y<column.Height-2)return body==PlanetBody.Mars?(ushort)43:body==PlanetBody.Venus?(ushort)10:(ushort)1;
            switch(column.Surface)
            {
                case PlanetSurface.Mare:case PlanetSurface.BasaltFlow:case PlanetSurface.Caldera:return 10;
                case PlanetSurface.IronRidge:return 43;
                case PlanetSurface.Canyon:case PlanetSurface.RedDust:case PlanetSurface.SulfurPlain:return 6;
                case PlanetSurface.Crater:return 29;
                default:return 32;
            }
        }
    }
}
